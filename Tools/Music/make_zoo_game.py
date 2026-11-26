"""Game copy of the approved zoo song (zoo_chase_v1), without the baked-in note signals.

The approved render (zoo/make_zoo_chase.py) has the jump and arms-up signal hits in the song, placed
for the first note layout only. The zoo now has six layouts, so the game gets:
  zoo_chase_v1.ogg         the same song with the signal hits left out
  zoo_sig_jump.ogg         the jump signal: kick + low floor tom + timpani (E)
  zoo_sig_armsup.ogg       the arms-up signal: crash + splash + glockenspiel (a 16th pickup, then E and B)
  zoo_signals.json         how to play them (lead time, gain)
All three use exactly the scale of the approved render (its -1 dBFS peak normalisation) plus the
same game gain (+1.9 dB, -18 LUFS reference); the signals are then lowered by 2 dB (SIG_DB) so the
unlimited sum in the game stays under -1 dBTP. Played on the music bus at the music volume they keep
nearly the balance they had in zoo-v1. The timpani now always plays E (the
song's key) instead of following the chord.

Usage: python make_zoo_game.py      (also writes the approved render to out/zoo/, unchanged)
"""
import json, os, random, shutil, subprocess, sys
import numpy as np
import mido
import musiclib as m

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "zoo"))
import make_zoo_chase as zoo  # noqa: E402

DEST = os.environ.get("MONGSCAPE_ZOO_DEST", os.path.join(m.UNITY_MUSIC, "Zoo"))
GAIN_DB, LIMIT = 1.9, 0.79   # game gain of the approved song; limiter ceiling 0.79 = -2.0 dBFS sample peak
SIG_TAIL = 1.6               # seconds kept after a signal hit
PICKUP = .25                 # arms-up glockenspiel pickup, in beats before the note
SIG_DB = -2.0                # signals sit 2 dB under the approved balance: the game sums them with the song unlimited,
                             # and at -2 dB the sum on the first layout stays under -1 dBTP (-18.6 LUFS)


def fluid(midi_path):
    raw = midi_path + ".raw.wav"
    subprocess.run([zoo.FLUID, "-ni", "-q", "-r", str(zoo.SR), "-g", "0.5", "-F", raw, zoo.SF2, midi_path], check=True)
    x = m.read_wav(raw); os.remove(raw)
    return x


def song(signals):
    """Composes and renders the song like zoo.render() (trim + fade), without the peak normalisation."""
    random.seed(160)
    zoo.events.clear(); zoo.SIGNALS = signals
    zoo.compose()
    zoo.NAME = "zoo_chase_v1" if signals else "zoo_chase_v1_nosignals"
    zoo.write_midi()
    x = fluid(zoo.OUT(".mid"))
    n = int(round((zoo.BARS * zoo.BEATS_PER_BAR * 60.0 / zoo.BPM + zoo.TAIL_SEC) * zoo.SR))
    x = x[:n] if len(x) >= n else np.vstack([x, np.zeros((n - len(x), 2), np.float32)])
    fade = int(zoo.TAIL_SEC * zoo.SR)
    x[-fade:] *= np.linspace(1, 0, fade)[:, None] ** 2
    return x


def signal(kind, path):
    """One signal hit at beat 1 (after a one-beat lead-in for the arms-up pickup), same channels and mix as the song."""
    mid = mido.MidiFile(ticks_per_beat=zoo.PPQ, type=1)
    meta = mido.MidiTrack(); mid.tracks.append(meta)
    meta.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(zoo.BPM), time=0))
    root, triad = zoo.CH["Em"]
    notes = ([(1, zoo.DRUM, 36, 127, .5), (1, zoo.DRUM, 41, 118, .5), (1, 4, root, 127, 1.0)] if kind == "jump" else
             [(1, zoo.DRUM, 49, 127, 1.5), (1, zoo.DRUM, 55, 105, 1.0), (1 - PICKUP, 5, triad[1] + 24, 70, .2),
              (1, 5, triad[0] + 36, 127, .8), (1, 5, triad[2] + 36, 115, .8)])
    for ch in sorted({c for _, c, _, _, _ in notes}):
        tr = mido.MidiTrack(); mid.tracks.append(tr)
        msgs = [] if ch == zoo.DRUM else [(0, 0, mido.Message("program_change", channel=ch, program=zoo.PROGRAMS[ch]))]
        msgs += [(0, 0, mido.Message("control_change", channel=ch, control=7, value=zoo.VOLUMES[ch])),
                 (0, 0, mido.Message("control_change", channel=ch, control=91, value=zoo.REVERB[ch]))]
        for beat, c, note, vel, dur in notes:
            if c == ch:
                msgs.append((int(beat * zoo.PPQ), 2, mido.Message("note_on", channel=ch, note=note, velocity=vel)))
                msgs.append((int((beat + dur) * zoo.PPQ), 1, mido.Message("note_off", channel=ch, note=note, velocity=0)))
        msgs.sort(key=lambda t: (t[0], t[1])); now = 0
        for tick, _, msg in msgs:
            tr.append(msg.copy(time=tick - now)); now = tick
        tr.append(mido.MetaMessage("end_of_track", time=int((1 + SIG_TAIL * zoo.BPM / 60 + 1) * zoo.PPQ) - now))
    mid.save(path)
    x = fluid(path)
    beat = 60.0 / zoo.BPM
    start = int(round((1 - (PICKUP if kind == "arms_up" else 0)) * beat * zoo.SR))
    x = x[start: start + int((SIG_TAIL + PICKUP * beat) * zoo.SR)].copy()
    fade = int(.4 * zoo.SR)
    x[-fade:] *= np.linspace(1, 0, fade)[:, None] ** 2
    return x, (PICKUP * beat if kind == "arms_up" else 0.0)


def limited(x, out):
    tmp = out + ".tmp.wav"
    zoo.write_wav(tmp, np.clip(x, -1, 1), zoo.SR)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", tmp, "-af",
                    f"alimiter=limit={LIMIT}:attack=3:release=80:level=false:asc=false",
                    "-c:a", "libvorbis", "-q:a", "6", out], check=True)
    os.remove(tmp)


if __name__ == "__main__":
    os.makedirs(DEST, exist_ok=True)
    work = zoo.HERE
    full = song(True)
    k = 10 ** (-1 / 20) / np.abs(full).max()          # the approved render's scale
    g = k * 10 ** (GAIN_DB / 20)
    bed = song(False) * g
    limited(bed, os.path.join(DEST, "zoo_chase_v1.ogg"))
    info = {}
    for kind, name in (("jump", "zoo_sig_jump"), ("arms_up", "zoo_sig_armsup")):
        x, lead = signal(kind, os.path.join(work, name + ".mid"))
        x = x * g * 10 ** (SIG_DB / 20)
        out = os.path.join(DEST, name + ".ogg")
        m.encode(x, out)
        meas = m.measure(np.vstack([m.decode(out), np.zeros((zoo.SR, 2), np.float32)]))
        info[kind] = {"file": name + ".ogg", "lead_seconds": round(lead, 4),
                      "true_peak_dbtp": round(meas["true_peak"], 1)}
    shutil.copyfile(os.path.join(work, "zoo_chase_v1.beatmap.json"), os.path.join(DEST, "zoo_chase_v1.beatmap.json"))
    after = m.measure(m.decode(os.path.join(DEST, "zoo_chase_v1.ogg")))
    with open(os.path.join(DEST, "zoo_signals.json"), "w", encoding="utf-8") as f:
        json.dump({"signals": info,
                   "play": "Start each signal lead_seconds before its note time (beatmap time) and play it on the music bus "
                           "at the music volume (gain 1.0), so it keeps the balance it had in the approved song.",
                   "song_without_signals": {"lufs": round(after["lufs"], 1), "true_peak_dbtp": round(after["true_peak"], 1)}},
                  f, indent=2)
    data = {
        "song": "zoo_chase_v1", "bpm": 160, "loop": False, "length_seconds": 61.5,
        "playback": "Played once per zoo visit, in sync with a zoo_chase_v1.*.beatmap.json (beat 0 = first sample). "
                    "The note signals are separate effects, see zoo_signals.json.",
        "layers": {"full": {"file": "zoo_chase_v1.ogg", "meaning": "The approved song without the note signal hits"}},
        "states": {"run": {"meaning": "Zoo run", "layers": {"full": 1},
                           "lufs": round(after["lufs"], 1), "true_peak_dbtp": round(after["true_peak"], 1)}},
        "game_copy": f"approved render scale +{GAIN_DB} dB, peak limiter at {LIMIT} (sample peak)",
    }
    with open(os.path.join(DEST, "zoo_chase_v1.layers.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    print("zoo without signals", after, info)
