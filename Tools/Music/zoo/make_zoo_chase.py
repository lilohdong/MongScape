"""Mongscape zoo chase song v1 — composes MIDI + beatmap from one data source,
renders with FluidSynth + GeneralUser GS, writes WAV/OGG and a click-check OGG.

Usage:  python make_zoo_chase.py
Needs:  pip install mido numpy ; ffmpeg on PATH ;
        tools/fluidsynth/.../bin/fluidsynth.exe ; tools/GeneralUser-GS.sf2

Knobs (edit below): BPM, PROGRAMS, VOLUMES, NOTES (beatmap), SIGNAL_* velocities.
"""
import json, os, random, subprocess, sys, wave, glob
import numpy as np
import mido

# In the game repo the render output goes to Tools/Music/out/zoo/ and the tools are shared with the
# other songs (Tools/Music/tools/ or $MONGSCAPE_MUSIC_TOOLS). The composition is unchanged from zoo-v1.
TOOLS = os.environ.get("MONGSCAPE_MUSIC_TOOLS",
                       os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "tools"))
HERE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "out", "zoo")
os.makedirs(HERE, exist_ok=True)
NAME = "zoo_chase_v1"
OUT = lambda ext: os.path.join(HERE, f"{NAME}{ext}")
SF2 = os.path.join(TOOLS, "GeneralUser-GS.sf2")
FLUID = (glob.glob(os.path.join(TOOLS, "fluidsynth", "*", "bin", "fluidsynth.exe"))
         or ["fluidsynth"])[0]

# ---------------------------------------------------------------- knobs
BPM = 160
BARS = 40
BEATS_PER_BAR = 4
PPQ = 480
SR = 44100
TAIL_SEC = 1.5          # reverb ring-out kept after beat 160 (60 s)
OFFSET_SEC = 0.0        # beat 0 == sample 0 of the audio
random.seed(160)

# GM programs per channel (change instruments here)
PROGRAMS = {
    0: 38,   # Synth Bass 1   — chase bass
    1: 12,   # Marimba        — verse motif
    2: 73,   # Flute          — chorus melody
    3: 48,   # Strings        — pad
    4: 47,   # Timpani        — JUMP signal (with kick + floor tom)
    5: 9,    # Glockenspiel   — ARMS_UP signal (with crash)
    6: 44,   # Tremolo Strings — intro / break tension
}
VOLUMES = {0: 100, 1: 92, 2: 88, 3: 58, 4: 120, 5: 120, 6: 70, 9: 104}  # CC7
REVERB = {0: 20, 1: 40, 2: 50, 3: 60, 4: 40, 5: 60, 6: 50, 9: 30}       # CC91
DRUM = 9

# Sections: (name, first bar, last bar) — bars are 1-based, inclusive
SECTIONS = [
    ("intro",   1,  2),
    ("verse1",  3, 10),
    ("chorus1", 11, 18),
    ("break",   19, 22),
    ("verse2",  23, 32),
    ("chorus2", 33, 38),
    ("exit",    39, 40),
]

# Beatmap: beat index from song start (0-based, beat 0 = bar 1 beat 1).
# bar n beat k  ->  (n-1)*4 + (k-1)
J, A = "jump", "arms_up"
NOTES = (
    # verse1: jump on beat 1 of bars 4-9 (every 4 beats)
    [(b, J) for b in (12, 16, 20, 24, 28, 32)]
    # chorus1: jump/arms mixed, 2-beat grid with breathing room
    + [(40, J), (42, A), (44, J), (48, A), (50, J), (54, A),
       (56, J), (58, A), (60, J), (64, A), (66, J)]
    # verse2: a bit denser than verse1
    + [(88, J), (92, J), (94, A), (100, J), (104, A), (108, J), (116, J), (118, A)]
    # chorus2: densest, every 2 beats
    + [(128, J), (130, A), (132, J), (134, A), (136, J), (138, A),
       (140, A), (142, J), (144, A), (146, J), (148, A)]
)
MIN_JUMP_GAP = 4        # beats between consecutive jumps
MIN_AFTER_JUMP = 2      # beats between a jump and the next note

# Chords per bar (E minor)
CH = {"Em": (40, [52, 55, 59]), "C": (36, [48, 52, 55]),
      "D": (38, [50, 54, 57]), "B": (35, [47, 51, 54])}
VERSE_PROG = ["Em", "Em", "C", "D"]
CHORUS_PROG = ["Em", "C", "D", "B"]

# ---------------------------------------------------------------- helpers
def section_of(bar):
    for name, a, b in SECTIONS:
        if a <= bar <= b:
            return name
    raise ValueError(bar)

def bar_of_beat(beat):
    return int(beat // BEATS_PER_BAR) + 1

def chord_for_bar(bar):
    sec = section_of(bar)
    first = next(a for n, a, b in SECTIONS if n == sec)
    i = bar - first
    if sec.startswith("verse"):
        return VERSE_PROG[i % 4]
    if sec.startswith("chorus"):
        return CHORUS_PROG[i % 4]
    if sec == "break":
        return ["Em", "Em", "C", "B"][i]
    if sec == "exit":
        return ["C", "Em"][i]
    return "Em"

def sec_time(beat):
    return OFFSET_SEC + beat * 60.0 / BPM

def validate():
    beats = sorted(NOTES)
    assert len({b for b, _ in beats}) == len(beats), "two notes on one beat"
    last_jump = None
    prev = None
    for b, t in beats:
        sec = section_of(bar_of_beat(b))
        assert sec not in ("intro", "break", "exit"), f"note in {sec} at beat {b}"
        assert b == int(b), "notes must sit on beats"
        if t == J and last_jump is not None:
            assert b - last_jump >= MIN_JUMP_GAP, f"jumps too close at {b}"
        if prev and prev[1] == J:
            assert b - prev[0] >= MIN_AFTER_JUMP, f"note too soon after jump at {b}"
        if t == J:
            last_jump = b
        prev = (b, t)

# ---------------------------------------------------------------- composition
events = []  # (beat, channel, note, velocity, duration_beats)

def add(beat, ch, note, vel, dur):
    vel = max(1, min(127, int(vel)))
    events.append((beat, ch, note, vel, dur))

def hum(v, spread=6):
    return v + random.randint(-spread, spread)

def compose():
    note_at = dict(NOTES)
    total_beats = BARS * BEATS_PER_BAR

    for bar in range(1, BARS + 1):
        sec = section_of(bar)
        b0 = (bar - 1) * BEATS_PER_BAR
        chord = chord_for_bar(bar)
        root, triad = CH[chord]
        chorus = sec.startswith("chorus")
        dense = sec == "chorus2"

        # ---------- intro: tension, timpani-free rumble, tremolo strings
        if sec == "intro":
            add(b0, 6, 40, 50 + (bar - 1) * 25, 4)
            add(b0, 6, 47, 45 + (bar - 1) * 25, 4)
            add(b0, 0, 28, 70, 4)          # low E drone
            for s in range(16):            # low tom rumble (not kick, not timpani)
                add(b0 + s * 0.25, DRUM, 45 if s % 2 else 43, 25 + (bar - 1) * 30 + s * 2, 0.2)
            if bar == 2:                   # pickup fill into verse
                for k, n in enumerate([62, 63, 64, 60, 61, 62, 63, 64]):
                    add(b0 + 2 + k * 0.25, DRUM, n, 70 + k * 5, 0.2)
            continue

        # ---------- exit: C stab drive then final Em hit
        if sec == "exit":
            if bar == 39:
                for e in range(8):
                    add(b0 + e * 0.5, 0, (36 if e < 4 else 38) + (12 if e % 2 else 0), hum(100), 0.45)
                    add(b0 + e * 0.5, DRUM, 82, hum(60), 0.2)
                for k, n in enumerate([50, 48, 47, 45, 43, 41, 50, 48, 47, 45, 43, 41, 47, 45, 43, 41]):
                    add(b0 + k * 0.25, DRUM, n, 60 + k * 3, 0.2)
                add(b0, 3, 48, 70, 2); add(b0, 3, 52, 70, 2); add(b0, 3, 55, 70, 2)
                add(b0 + 2, 3, 50, 75, 2); add(b0 + 2, 3, 54, 75, 2); add(b0 + 2, 3, 57, 75, 2)
                add(b0 + 3, 2, 71, 90, 1)
            else:  # final hit
                add(b0, DRUM, 36, 127, 1); add(b0, DRUM, 49, 120, 4); add(b0, DRUM, 57, 110, 4)
                add(b0, 4, 40, 127, 2)
                add(b0, 0, 28, 120, 3); add(b0, 0, 40, 110, 3)
                for n in (52, 55, 59, 64):
                    add(b0, 3, n, 95, 3.5)
                add(b0, 2, 76, 105, 3.5)
                add(b0, 1, 76, 110, 1); add(b0, 1, 64, 100, 1)
            continue

        # ---------- break: iron gate sprint, drums pile on
        if sec == "break":
            i = bar - 19
            for s in range(16):
                beat = b0 + s * 0.25
                add(beat, 0, root - 12 + (12 if s % 4 == 2 else 0), hum(80 + i * 8), 0.22)
                tom = [50, 48, 47, 45][s % 4] if i >= 2 else [63, 62, 64, 62][s % 4]
                add(beat, DRUM, tom, hum(55 + i * 12 + (15 if s % 4 == 0 else 0)), 0.2)
                add(beat, DRUM, 82, hum(55), 0.15)
                if i >= 1 and s % 2 == 1:
                    add(beat, DRUM, 60 if s % 4 == 1 else 61, hum(70 + i * 8), 0.15)
            add(b0, 6, 52 + i * 2, 60 + i * 15, 4)        # rising tremolo strings
            add(b0, 6, 59 + i * 2, 55 + i * 15, 4)
            if bar == 22:                                  # last bar: 16th snare/tom roll
                for s in range(8):
                    add(b0 + 2 + s * 0.25, DRUM, 38, 60 + s * 8, 0.2)
            continue

        # ---------- verse / chorus groove
        # shaker
        step = 0.25 if dense else 0.5
        for k in range(int(4 / step)):
            add(b0 + k * step, DRUM, 82 if chorus or sec == "verse2" else 70,
                hum(62 if k % 2 == 0 else 45), 0.1)
        # conga pattern (tumbao-ish), quieter on downbeats so jump hits stand out
        conga = [(0.5, 62, 55), (1.0, 64, 70), (1.5, 63, 80), (2.5, 62, 55),
                 (3.0, 64, 72), (3.5, 63, 85)]
        for off, n, v in conga:
            add(b0 + off, DRUM, n, hum(v), 0.2)
        # bongos in choruses / verse2
        if chorus or sec == "verse2":
            for off, n in [(0.75, 60), (1.75, 61), (2.75, 60), (3.25, 60), (3.75, 61)]:
                add(b0 + off, DRUM, n, hum(58 if not dense else 70), 0.15)
        # agogo color in choruses
        if chorus:
            for off, n in [(0.5, 67), (1.5, 68), (2.0, 67), (3.0, 68)]:
                add(b0 + off, DRUM, n, hum(45), 0.15)
        # light snare backbeat only in chorus2 (densest)
        if dense:
            for off in (1.0, 3.0):
                add(b0 + off, DRUM, 38, hum(60), 0.2)

        # bass: chase gallop (8th + 16th push)
        if chorus:
            pat = [(0, 0), (0.5, 12), (1, 0), (1.5, 12), (2, 0), (2.5, 12), (3, 0), (3.5, 12)]
        else:
            pat = [(0, 0), (0.5, 0), (0.75, 0), (1, 12), (1.5, 0), (2, 0), (2.5, 0), (2.75, 0), (3, 12), (3.5, 0)]
        for off, o in pat:
            add(b0 + off, 0, root + o, hum(96 if off in (0, 2) else 80), 0.22 if off % 0.5 else 0.42)

        # pad
        if chorus:
            for n in triad:
                add(b0, 3, n, 60, 4)
        elif sec == "verse2":
            add(b0, 3, triad[1], 50, 4); add(b0, 3, triad[2], 50, 4)

        # marimba motif (verses), sparse offbeat arpeggio
        if sec.startswith("verse"):
            r, t, f = triad[0] + 12, triad[1] + 12, triad[2] + 12
            motif = [(0.5, f), (1.0, t + 12 - 12), (1.5, r + 12), (2.5, f), (3.0, t), (3.5, r + 12)]
            if (bar % 2) == 0:
                motif = motif[:4] + [(3.0, f + 5 if chord == "Em" else f), (3.5, t + 12)]
            for off, n in motif:
                add(b0 + off, 1, n, hum(78), 0.4)
            if sec == "verse2":   # flute long tones as counter-line
                add(b0, 2, triad[1] + 12, 62, 3.5)

        # flute chorus melody (4-bar phrase)
        if chorus:
            phrase = {
                "Em": [(0, 71, 1), (1, 67, .5), (1.5, 69, .5), (2, 71, 1), (3, 74, 1)],
                "C":  [(0, 76, 1.5), (1.5, 74, .5), (2, 72, 1), (3, 71, 1)],
                "D":  [(0, 69, 1), (1, 66, .5), (1.5, 67, .5), (2, 69, 1), (3, 74, 1)],
                "B":  [(0, 75, 1.5), (1.5, 71, .5), (2, 66, 2)],
            }[chord]
            up = 12 if dense and (bar - 33) >= 4 else 0
            for off, n, d in phrase:
                add(b0 + off, 2, n + up, hum(92), d * 0.95)
            # marimba doubles the melody an octave down in chorus2
            if dense:
                for off, n, d in phrase:
                    add(b0 + off, 1, n - 12, hum(70), min(d, 0.5))

        # fill on last bar of a verse/chorus (toms on beats 3-4, never kick/crash)
        last_of_sec = bar == next(b for n, a, b in SECTIONS if n == sec)
        if last_of_sec:
            for k, n in enumerate([50, 48, 47, 45, 43, 41, 43, 41]):
                beat = b0 + 2 + k * 0.25
                if beat not in note_at:
                    add(beat, DRUM, n, 70 + k * 6, 0.2)

    # ---------- note signals (from NOTES; the only kick/timpani/crash/glock in the groove)
    for beat, kind in NOTES:
        root, triad = CH[chord_for_bar(bar_of_beat(beat))]
        if kind == J:
            add(beat, DRUM, 36, 127, 0.5)      # kick
            add(beat, DRUM, 41, 118, 0.5)      # low floor tom
            add(beat, 4, root, 127, 1.0)       # timpani on chord root
        else:
            add(beat, DRUM, 49, 127, 1.5)      # crash
            add(beat, DRUM, 55, 105, 1.0)      # splash
            add(beat - 0.25, 5, triad[1] + 24, 70, 0.2)        # pickup 16th
            add(beat, 5, triad[0] + 36, 127, 0.8)              # bright landing
            add(beat, 5, triad[2] + 36, 115, 0.8)

def write_midi():
    mid = mido.MidiFile(ticks_per_beat=PPQ, type=1)
    meta = mido.MidiTrack(); mid.tracks.append(meta)
    meta.append(mido.MetaMessage("track_name", name=NAME, time=0))
    meta.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(BPM), time=0))
    meta.append(mido.MetaMessage("time_signature", numerator=4, denominator=4, time=0))
    # section markers
    t_prev = 0
    for name, a, b in SECTIONS:
        t = (a - 1) * BEATS_PER_BAR * PPQ
        meta.append(mido.MetaMessage("marker", text=name, time=t - t_prev)); t_prev = t
    end_tick = int((BARS * BEATS_PER_BAR + TAIL_SEC * BPM / 60.0) * PPQ)
    meta.append(mido.MetaMessage("end_of_track", time=end_tick - t_prev))

    chans = sorted({e[1] for e in events})
    for ch in chans:
        tr = mido.MidiTrack(); mid.tracks.append(tr)
        msgs = []
        if ch != DRUM:
            msgs.append((0, 0, mido.Message("program_change", channel=ch, program=PROGRAMS[ch])))
        msgs.append((0, 0, mido.Message("control_change", channel=ch, control=7, value=VOLUMES[ch])))
        msgs.append((0, 0, mido.Message("control_change", channel=ch, control=91, value=REVERB[ch])))
        for beat, c, n, v, d in events:
            if c != ch:
                continue
            on = int(round(beat * PPQ)); off = int(round((beat + d) * PPQ))
            msgs.append((on, 2, mido.Message("note_on", channel=ch, note=n, velocity=v)))
            msgs.append((off, 1, mido.Message("note_off", channel=ch, note=n, velocity=0)))
        msgs.sort(key=lambda x: (x[0], x[1]))
        now = 0
        for t, _, m in msgs:
            tr.append(m.copy(time=t - now)); now = t
        tr.append(mido.MetaMessage("end_of_track", time=max(0, end_tick - now)))
    mid.save(OUT(".mid"))

def write_beatmap():
    notes = [{"beat": b, "time": round(sec_time(b), 4), "type": t, "bar": bar_of_beat(b),
              "beat_in_bar": int(b % BEATS_PER_BAR) + 1} for b, t in sorted(NOTES)]
    sections = []
    for name, a, b in SECTIONS:
        s_beat, e_beat = (a - 1) * BEATS_PER_BAR, b * BEATS_PER_BAR
        ns = [n for n in notes if s_beat <= n["beat"] < e_beat]
        sections.append({"name": name, "start_bar": a, "end_bar": b,
                         "start_beat": s_beat, "end_beat": e_beat,
                         "start_time": round(sec_time(s_beat), 4), "end_time": round(sec_time(e_beat), 4),
                         "jump": sum(n["type"] == J for n in ns),
                         "arms_up": sum(n["type"] == A for n in ns)})
    data = {
        "song": NAME, "bpm": BPM, "time_signature": [4, 4], "bars": BARS,
        "offset_sec": OFFSET_SEC,
        "beat_unit": "beat 0 = bar 1 beat 1; time = offset_sec + beat * 60 / bpm",
        "length_sec": BARS * BEATS_PER_BAR * 60.0 / BPM,
        "audio_length_sec": BARS * BEATS_PER_BAR * 60.0 / BPM + TAIL_SEC,
        "counts": {"jump": sum(n["type"] == J for n in notes),
                   "arms_up": sum(n["type"] == A for n in notes)},
        "sections": sections,
        "notes": notes,
    }
    with open(OUT(".beatmap.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    return data

def read_wav(p):
    with wave.open(p) as w:
        assert w.getsampwidth() == 2
        x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768
        return x.reshape(-1, w.getnchannels()), w.getframerate()

def write_wav(p, x, sr):
    y = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(p, "wb") as w:
        w.setnchannels(y.shape[1]); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes(y.tobytes())

def render():
    raw = os.path.join(HERE, f"_{NAME}_raw.wav")
    subprocess.run([FLUID, "-ni", "-q", "-r", str(SR), "-g", "0.5", "-F", raw, SF2, OUT(".mid")],
                   check=True)
    x, sr = read_wav(raw); os.remove(raw)
    n = int(round((BARS * BEATS_PER_BAR * 60.0 / BPM + TAIL_SEC) * sr))
    x = x[:n] if len(x) >= n else np.vstack([x, np.zeros((n - len(x), x.shape[1]), np.float32)])
    fade = int(TAIL_SEC * sr)
    x[-fade:] *= np.linspace(1, 0, fade)[:, None] ** 2
    x *= 10 ** (-1 / 20) / max(1e-9, np.abs(x).max())   # peak -1 dBFS
    write_wav(OUT(".wav"), x, sr)

    # click-check mix: jump = low 1 kHz click, arms_up = high 2.5 kHz double click
    c = x * 0.6
    def click(freq, ms=25):
        t = np.arange(int(sr * ms / 1000)) / sr
        return np.sin(2 * np.pi * freq * t) * np.exp(-t * 180)
    for b, kind in NOTES:
        i = int(round(sec_time(b) * sr))
        k = click(1000) if kind == J else np.concatenate([click(2500), np.zeros(int(sr * 0.03)), click(2500)])
        c[i:i + len(k)] += 0.7 * k[:, None]
    write_wav(os.path.join(HERE, f"_{NAME}_clicks.wav"), c, sr)

    for src, dst in [(OUT(".wav"), OUT(".ogg")),
                     (os.path.join(HERE, f"_{NAME}_clicks.wav"), OUT("_withclicks.ogg"))]:
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", src, "-c:a", "libvorbis",
                        "-q:a", "6", dst], check=True)
    os.remove(os.path.join(HERE, f"_{NAME}_clicks.wav"))
    return x, sr

def section_loudness(x, sr):
    """RMS (dBFS) per section, to sanity-check the arrangement's energy curve."""
    out = []
    for name, a, b in SECTIONS:
        i, j = int(sec_time((a - 1) * 4) * sr), int(sec_time(b * 4) * sr)
        out.append((name, round(20 * np.log10(np.sqrt((x[i:j] ** 2).mean()) + 1e-9), 1)))
    return out

if __name__ == "__main__":
    validate()
    compose()
    write_midi()
    data = write_beatmap()
    x, sr = render()
    print(json.dumps(data["counts"]), [(s["name"], s["jump"], s["arms_up"]) for s in data["sections"]])
    print("section RMS dBFS:", section_loudness(x, sr))
