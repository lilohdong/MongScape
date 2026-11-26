"""Shared pipeline for Mongscape's layered loop music.

A song is composed as code into tracks. Every track belongs to one layer (stem).
Each layer is written as its own MIDI file and rendered with FluidSynth +
GeneralUser GS, so every layer can be played alone in the game. All layer
files of one song have exactly the same sample count and are played together
(Unity AudioSource.PlayScheduled at one dspTime); a game state only changes
layer volumes.

Seamless loops: each layer is rendered with a reverb tail after the last bar.
The tail is cut off and added onto the start of the file, so the file length is
exactly the loop length and the end flows into the start without a click.

Loudness: one fixed gain per song (the same for every layer, no limiter or
compressor) sets the song's reference state to its target integrated
loudness (EBU R128 / ITU-R BS.1770, measured with ffmpeg ebur128). If any state
would go above the true-peak ceiling, the song gain is lowered instead.

Tools: FluidSynth and GeneralUser-GS.sf2 are looked up in $MONGSCAPE_MUSIC_TOOLS
or Tools/Music/tools/ (not committed). ffmpeg must be on PATH. Python: mido, numpy.
"""
import glob, json, os, random, re, subprocess, wave
import numpy as np
import mido

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.environ.get("MONGSCAPE_MUSIC_TOOLS", os.path.join(HERE, "tools"))
SF2 = os.path.join(TOOLS, "GeneralUser-GS.sf2")
FLUID = (glob.glob(os.path.join(TOOLS, "fluidsynth", "*", "bin", "fluidsynth.exe"))
         + glob.glob(os.path.join(TOOLS, "fluidsynth", "*", "bin", "fluidsynth")) or ["fluidsynth"])[0]
UNITY_MUSIC = os.path.normpath(os.path.join(HERE, "..", "..", "Unity", "Assets", "Game", "Content", "Audio", "Music"))
OUT_ROOT = os.path.join(HERE, "out")
SR = 44100
PPQ = 480
DRUM = 9
TRUE_PEAK_CEILING = -1.0

# GM drum keys
KICK, RIM, SNARE, CLAP, FLOOR_LO, FLOOR_HI, TOM_LO, TOM_LM, TOM_HM, TOM_HI = 36, 37, 38, 39, 41, 43, 45, 47, 48, 50
HAT, PEDAL, OPEN_HAT, CRASH, RIDE, RIDE_BELL, TAMB, COWBELL, SPLASH = 42, 44, 46, 49, 51, 53, 54, 56, 55
BONGO_HI, BONGO_LO, CONGA_MUTE, CONGA_HI, CONGA_LO = 60, 61, 62, 63, 64
AGOGO_HI, AGOGO_LO, CABASA, MARACAS, CLAVES, WOOD_HI, WOOD_LO = 67, 68, 69, 70, 75, 76, 77
TRIANGLE_MUTE, TRIANGLE_OPEN, SHAKER = 80, 81, 82

NOTE_NAMES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def n(name):
    """'C4' -> 60, 'Bb3' -> 58, 'F#5' -> 78."""
    m = re.fullmatch(r"([A-G])([b#]?)(-?\d)", name)
    base = NOTE_NAMES[m.group(1)] + {"b": -1, "#": 1, "": 0}[m.group(2)]
    return 12 * (int(m.group(3)) + 1) + base


class Track:
    def __init__(self, song, name, layer, program, volume=100, reverb=40, pan=64, drum=False, chorus=0):
        self.song, self.name, self.layer = song, name, layer
        self.program, self.volume, self.reverb, self.pan, self.drum, self.chorus = program, volume, reverb, pan, drum, chorus
        self.events = []  # (beat, note, velocity, duration_beats)

    def note(self, beat, note, vel, dur):
        if isinstance(note, str):
            note = n(note)
        self.events.append((float(beat), int(note), max(1, min(127, int(round(vel)))), float(dur)))
        return self

    def chord(self, beat, notes, vel, dur):
        for x in notes:
            self.note(beat, x, vel, dur)
        return self


class Song:
    def __init__(self, name, title, bpm, bars, beats_per_bar=4, tail_sec=3.0, seed=1):
        self.name, self.title, self.bpm, self.bars, self.bpb = name, title, bpm, bars, beats_per_bar
        self.tail_sec = tail_sec
        self.tracks = []
        self.layers = {}      # layer -> meaning
        self.states = {}      # state -> {layer: volume}
        self.reference = None  # state measured against the loudness target
        self.target_lufs = -18.0
        self.fades = {}
        self.sections = []
        self.layer_eq = {}    # layer -> [(freq_hz, gain_db)] fixed low shelves for the whole layer
        self.rng = random.Random(seed)

    # ---------- composition helpers
    def track(self, *a, **k):
        t = Track(self, *a, **k)
        self.tracks.append(t)
        return t

    def layer(self, name, meaning):
        self.layers[name] = meaning

    def state(self, name, meaning, **volumes):
        self.states[name] = {"meaning": meaning, "layers": volumes}

    def hum(self, v, spread=5):
        return v + self.rng.randint(-spread, spread)

    def bar(self, b):
        """Beat at which 1-based bar b starts."""
        return (b - 1) * self.bpb

    @property
    def loop_beats(self):
        return self.bars * self.bpb

    @property
    def loop_sec(self):
        return self.loop_beats * 60.0 / self.bpm

    @property
    def loop_samples(self):
        return int(round(self.loop_sec * SR))

    # ---------- MIDI
    def write_layer_midi(self, layer, path):
        tracks = [t for t in self.tracks if t.layer == layer]
        mid = mido.MidiFile(ticks_per_beat=PPQ, type=1)
        meta = mido.MidiTrack(); mid.tracks.append(meta)
        meta.append(mido.MetaMessage("track_name", name=f"{self.name}_{layer}", time=0))
        meta.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(self.bpm), time=0))
        meta.append(mido.MetaMessage("time_signature", numerator=self.bpb, denominator=4, time=0))
        t_prev = 0
        for name, a, _ in self.sections:
            t = self.bar(a) * PPQ
            meta.append(mido.MetaMessage("marker", text=name, time=t - t_prev)); t_prev = t
        end_tick = int((self.loop_beats + self.tail_sec * self.bpm / 60.0) * PPQ)
        meta.append(mido.MetaMessage("end_of_track", time=max(0, end_tick - t_prev)))
        free = [c for c in range(16) if c != DRUM]
        drum_programs = {t.program for t in tracks if t.drum}
        assert len(drum_programs) <= 1, f"{self.name}/{layer}: one drum kit per layer"
        for t in tracks:
            ch = DRUM if t.drum else free.pop(0)
            tr = mido.MidiTrack(); mid.tracks.append(tr)
            tr.append(mido.MetaMessage("track_name", name=t.name, time=0))
            msgs = [(0, 0, mido.Message("program_change", channel=ch, program=t.program))]
            for cc, v in ((7, t.volume), (91, t.reverb), (10, t.pan), (93, t.chorus)):
                msgs.append((0, 0, mido.Message("control_change", channel=ch, control=cc, value=v)))
            for beat, note, vel, dur in t.events:
                assert 0 <= beat < self.loop_beats, f"{t.name}: note at beat {beat} outside the loop"
                on = int(round(beat * PPQ)); off = max(on + 1, int(round((beat + dur) * PPQ)))
                msgs.append((on, 2, mido.Message("note_on", channel=ch, note=note, velocity=vel)))
                msgs.append((off, 1, mido.Message("note_off", channel=ch, note=note, velocity=0)))
            msgs.sort(key=lambda x: (x[0], x[1]))
            now = 0
            for tick, _, m in msgs:
                tr.append(m.copy(time=tick - now)); now = tick
            tr.append(mido.MetaMessage("end_of_track", time=max(0, end_tick - now)))
        mid.save(path)

    # ---------- render
    def render_layer(self, layer, work):
        midi = os.path.join(work, f"{self.name}_{layer}.mid")
        self.write_layer_midi(layer, midi)
        raw = os.path.join(work, f"_{self.name}_{layer}_raw.wav")
        subprocess.run([FLUID, "-ni", "-q", "-r", str(SR), "-g", "0.5", "-F", raw, SF2, midi], check=True)
        x = read_wav(raw); os.remove(raw)
        for freq, gain_db in self.layer_eq.get(layer, []):
            x = low_shelf(x, freq, gain_db)   # filter the raw render, before the wrap, so the seam stays clean
        loop, tail = self.loop_samples, int(round(self.tail_sec * SR))
        need = loop + tail
        if len(x) < need:
            x = np.vstack([x, np.zeros((need - len(x), 2), np.float32)])
        out = x[:loop].copy()
        out[:tail] += x[loop:need]     # wrap the reverb tail onto the loop start
        return out

    def build(self, demo=None):
        """Render all layers, set the song gain, write OGGs + layers.json, previews and measurements."""
        work = os.path.join(OUT_ROOT, self.name); os.makedirs(work, exist_ok=True)
        stems = {layer: self.render_layer(layer, work) for layer in self.layers}
        mixes = {s: state_mix(stems, v["layers"]) for s, v in self.states.items()}
        ref = measure(mixes[self.reference])
        gain_db = self.target_lufs - ref["lufs"]
        peaks = {s: measure(m)["true_peak"] + gain_db for s, m in mixes.items()}
        worst = max(peaks.values())
        if worst > TRUE_PEAK_CEILING - .4:    # keep 0.4 dB for OGG encoding overshoot
            gain_db -= worst - (TRUE_PEAK_CEILING - .4)
        g = 10 ** (gain_db / 20)
        dest = os.path.join(UNITY_MUSIC, self.title); os.makedirs(dest, exist_ok=True)
        files = {}
        for layer, x in stems.items():
            files[layer] = f"{self.name}_{layer}.ogg"
            encode(x * g, os.path.join(dest, files[layer]))
        # measure what the game will actually play: the decoded OGG layers mixed per state
        decoded = {layer: decode(os.path.join(dest, f)) for layer, f in files.items()}
        report, alone = {}, {}
        for layer, x in decoded.items():
            m = measure(x)
            alone[layer] = {"lufs": round(m["lufs"], 1), "true_peak_dbtp": round(m["true_peak"], 1)}
            seam = seam_jump(x)
            assert seam < 3.0, f"{self.name}/{layer}: loop seam jump {seam:.1f}x the typical step"
        for s, v in self.states.items():
            m = measure(state_mix(decoded, v["layers"]))
            report[s] = {"lufs": round(m["lufs"], 1), "true_peak_dbtp": round(m["true_peak"], 1)}
        data = {
            "song": self.name, "bpm": self.bpm, "time_signature": [self.bpb, 4], "bars": self.bars,
            "loop_seconds": round(self.loop_sec, 4), "loop_samples": self.loop_samples, "sample_rate": SR,
            "playback": "Start every layer with AudioSource.PlayScheduled at the same dspTime, loop = true. "
                        "A state only sets layer volumes.",
            "song_gain_db": round(gain_db, 2), "reference_state": self.reference, "target_lufs": self.target_lufs,
            "sections": [{"name": a, "bars": [b, c]} for a, b, c in self.sections],
            "layers": {l: {"file": files[l], "meaning": self.layers[l], "alone": alone[l]} for l in self.layers},
            "states": {s: {"meaning": v["meaning"], "layers": v["layers"], **report[s]} for s, v in self.states.items()},
            "fades": self.fades,
            "layer_eq": {l: [{"low_shelf_hz": f, "gain_db": g} for f, g in v] for l, v in self.layer_eq.items()},
        }
        with open(os.path.join(dest, f"{self.name}.layers.json"), "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        # previews (not for the game): every state looped twice, plus an optional state demo
        prev = os.path.join(work, "preview"); os.makedirs(prev, exist_ok=True)
        for s, v in self.states.items():
            mix = state_mix(decoded, v["layers"])
            encode(np.vstack([mix, mix]), os.path.join(prev, f"{self.name}_{s}_x2.ogg"))
        if demo:
            encode(demo_mix(decoded, self.states, demo, self.fades), os.path.join(prev, f"{self.name}_demo.ogg"))
        print(f"{self.name}: {self.loop_sec:.2f}s loop, gain {gain_db:+.1f} dB")
        for s, r in report.items():
            print(f"   {s:10s} {r['lufs']:6.1f} LUFS  {r['true_peak_dbtp']:5.1f} dBTP")
        return data


def low_shelf(x, freq, gain_db, q=.707):
    """RBJ cookbook low shelf (a fixed EQ for a whole layer: linear, so layers still add up)."""
    from scipy.signal import lfilter
    from math import cos, sin, pi, sqrt
    A = 10 ** (gain_db / 40); w = 2 * pi * freq / SR; alpha = sin(w) / (2 * q); c = cos(w)
    b = [A * ((A + 1) - (A - 1) * c + 2 * sqrt(A) * alpha), 2 * A * ((A - 1) - (A + 1) * c),
         A * ((A + 1) - (A - 1) * c - 2 * sqrt(A) * alpha)]
    a = [(A + 1) + (A - 1) * c + 2 * sqrt(A) * alpha, -2 * ((A - 1) + (A + 1) * c), (A + 1) + (A - 1) * c - 2 * sqrt(A) * alpha]
    return lfilter([v / a[0] for v in b], [v / a[0] for v in a], x, axis=0).astype(np.float32)


def seam_jump(x):
    """Sample step across the loop point (end -> start) relative to the 99.9th percentile step inside the file."""
    steps = np.abs(np.diff(x, axis=0)).max(axis=1)
    typical = np.percentile(steps, 99.9) + 1e-9
    return float(np.abs(x[0] - x[-1]).max() / typical)


def state_mix(stems, volumes):
    out = None
    for layer, vol in volumes.items():
        if vol <= 0:
            continue
        out = stems[layer] * vol if out is None else out + stems[layer] * vol
    return out


def demo_mix(stems, states, timeline, fades):
    """timeline: [(state, seconds)]. Layer volumes move linearly over a fade time: 'out_<previous state>' if set,
    else 'in_<new state>', else 'default' (seconds)."""
    loop = len(next(iter(stems.values())))
    total = int(sum(sec for _, sec in timeline) * SR)
    out = np.zeros((total, 2), np.float32)
    vol = {l: 0.0 for l in stems}
    first = states[timeline[0][0]]["layers"]
    vol.update({l: first.get(l, 0.0) for l in stems})
    pos, prev_state = 0, timeline[0][0]
    block = 441
    for state, sec in timeline:
        target = {l: states[state]["layers"].get(l, 0.0) for l in stems}
        fade = fades.get(f"out_{prev_state}", fades.get(f"in_{state}", fades.get("default", .5)))
        step = block / SR / max(fade, 1e-3)
        end = pos + int(sec * SR)
        while pos < end:
            k = min(block, end - pos)
            idx = (np.arange(pos, pos + k)) % loop
            for l, x in stems.items():
                v0 = vol[l]
                d = target[l] - v0
                v1 = v0 + max(-step, min(step, d))
                if v0 or v1:
                    out[pos:pos + k] += x[idx] * np.linspace(v0, v1, k, dtype=np.float32)[:, None]
                vol[l] = v1
            pos += k
        prev_state = state
    return out


# ---------- audio io + measurement
def read_wav(p):
    with wave.open(p) as w:
        assert w.getsampwidth() == 2
        x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768
        return x.reshape(-1, w.getnchannels())


def encode(x, out, quality=6):
    os.makedirs(os.path.dirname(out), exist_ok=True)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", str(x.shape[1]), "-i", "-",
                    "-c:a", "libvorbis", "-q:a", str(quality), out], input=x.astype(np.float32).tobytes(), check=True)


def decode(path, channels=2):
    r = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", str(channels), "-ar", str(SR), "-f", "f32le", "-"],
                       capture_output=True, check=True)
    return np.frombuffer(r.stdout, np.float32).reshape(-1, channels).copy()


def measure(x):
    """Integrated loudness (LUFS) and true peak (dBTP) via ffmpeg ebur128."""
    ch = x.shape[1] if x.ndim == 2 else 1
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-f", "f32le", "-ar", str(SR), "-ac", str(ch), "-i", "-",
                        "-af", "ebur128=peak=true", "-f", "null", "-"],
                       input=x.astype(np.float32).tobytes(), capture_output=True, check=True)
    err = r.stderr.decode("utf-8", "replace")
    summary = err[err.rfind("Summary:"):]
    lufs = float(re.search(r"I:\s+(-?[\d.]+|-inf) LUFS", summary).group(1))
    peak = float(re.search(r"True peak:\s+Peak:\s+(-?[\d.]+|-inf) dBFS", summary).group(1))
    return {"lufs": lufs, "true_peak": peak}


def track_report(song):
    """Debug aid: loudness of every track rendered alone (run: python -c "import make_x, musiclib; musiclib.track_report(make_x.song)")."""
    work = os.path.join(OUT_ROOT, song.name, "tracks"); os.makedirs(work, exist_ok=True)
    keep = [(t, t.layer) for t in song.tracks]
    try:
        for t, layer in keep:
            for u, l in keep:
                u.layer = "_solo" if u is t else "_off"
            x = song.render_layer("_solo", work)
            m = measure(x)
            print(f"   {layer:8s} {t.name:14s} {m['lufs']:6.1f} LUFS  {m['true_peak']:5.1f} dBTP")
    finally:
        for t, layer in keep:
            t.layer = layer


def peak_bars(song, state, top=8):
    """Debug aid: loudest bars (sample peak dBFS) of a state, from the built OGG layers."""
    dest = os.path.join(UNITY_MUSIC, song.title)
    layers = song.states[state]["layers"]
    x = state_mix({l: decode(os.path.join(dest, f"{song.name}_{l}.ogg")) for l in layers}, layers)
    spb = int(SR * 60 / song.bpm * song.bpb)
    pk = [(round(20 * np.log10(np.abs(x[k * spb:(k + 1) * spb]).max() + 1e-9), 1), k + 1) for k in range(song.bars)]
    return sorted(pk, reverse=True)[:top]
