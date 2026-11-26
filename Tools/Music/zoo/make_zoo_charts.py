"""Zoo chase v1: six note charts (easy, normal, hard x two layouts each) for the same song.

Usage:  python make_zoo_charts.py [--song-dir <folder with make_zoo_chase.py>] [--out <beatmap folder>] [--clicks <folder>]
Needs:  pip install mido numpy ; ffmpeg on PATH (only for the click-check OGGs)
In the repo (Tools/Music/zoo/): beatmaps go to Unity/Assets/Game/Content/Audio/Music/Zoo/ next to zoo_chase_v1.ogg;
click-check OGGs are written only with --clicks <folder> (keep them out of Unity/Assets).

The song is NOT re-rendered. Each chart only writes a beatmap JSON (same schema as zoo_chase_v1.beatmap.json,
plus difficulty/variant/audio) and a click-check OGG (the song + a click on every note of that chart).
Rules come from make_zoo_chase.py (validate): no notes in intro/break/exit, notes on beats, at least
MIN_JUMP_GAP (4) beats between jumps, at least MIN_AFTER_JUMP (2) beats after a jump. Every chart here also
keeps at least 2 beats between any two notes.

Plan 3-3: the zoo does not use the distance curve; the visit count picks the tier (1st visit easy, 2nd normal,
3rd+ hard) and one of the two layouts of that tier is picked at random.

The song has its own cue sounds on the normal_a notes (kick + timpani = jump, crash + glockenspiel = arms-up).
Easy charts are subsets of normal_a, so every easy note still has its cue (a few cues play with no obstacle).
Hard charts are normal_a plus extra notes, so every normal_a cue still lines up; the extra notes have no cue
sound of their own in the music (the game's note sound or the click marks them).
normal_b moves some notes off the cue beats; it is the "same density, different layout" set for normal.
"""
import argparse, importlib.util, json, os, subprocess, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_SONG = r"C:\Users\rhkdr\orca\workspaces\PrisonRunner\music\zoo-v1"

J, A = "jump", "arms_up"

def jumps(*b): return [(x, J) for x in b]
def arms(*b): return [(x, A) for x in b]

NORMAL_A = None  # filled from make_zoo_chase.NOTES (the passed layout, unchanged)

CHARTS = {
    # ~70% of normal_a (25 of 36), only normal_a beats (cue sounds line up)
    "easy_a": (jumps(12, 16, 24, 32)                                   # verse1
               + jumps(40, 50, 60) + arms(42, 48, 54, 64)              # chorus1
               + jumps(88, 100, 108, 116) + arms(94, 104)              # verse2
               + jumps(128, 136, 142, 146) + arms(130, 140, 144, 148)),  # chorus2
    "easy_b": (jumps(12, 20, 24, 32)
               + jumps(44, 56, 60) + arms(42, 48, 54, 64)
               + jumps(88, 100, 116) + arms(94, 104, 118)
               + jumps(128, 136, 142, 146) + arms(134, 138, 144, 148)),
    # same density as normal_a (36), different layout
    "normal_b": (jumps(12, 16, 20, 24, 28) + arms(18)
                 + jumps(42, 46, 50, 56, 60, 64) + arms(40, 44, 48, 54, 62)
                 + jumps(88, 92, 100, 108, 116) + arms(90, 102, 112)
                 + jumps(128, 132, 138, 142, 146) + arms(130, 134, 136, 140, 144, 148)),
    # ~130% of normal_a (47 of 36): normal_a + 11 extra notes
    "hard_a": "normal_a+" ,
    "hard_b": "normal_a+",
}
HARD_EXTRA = {
    "hard_a": arms(14, 30) + arms(46, 52, 62) + jumps(70) + arms(90) + jumps(96) + arms(112) + jumps(120) + arms(150),
    "hard_b": arms(18, 26, 34) + arms(52, 62, 68) + jumps(70) + arms(98, 112, 126) + jumps(124),
}
ORDER = ["easy_a", "easy_b", "normal_a", "normal_b", "hard_a", "hard_b"]

def load_song_module(song_dir):
    spec = importlib.util.spec_from_file_location("make_zoo_chase", os.path.join(song_dir, "make_zoo_chase.py"))
    m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
    return m

def check(m, name, notes):
    beats = sorted(notes)
    m.NOTES = beats
    m.validate()                                    # the song's own rules (assert)
    for (a, _), (b, _) in zip(beats, beats[1:]):
        assert b - a >= 2, f"{name}: notes closer than 2 beats at {b}"

def beatmap(m, name, notes, base):
    notes = sorted(notes)
    out = [{"beat": b, "time": round(m.sec_time(b), 4), "type": t, "bar": m.bar_of_beat(b),
            "beat_in_bar": int(b % m.BEATS_PER_BAR) + 1} for b, t in notes]
    sections = []
    for sec, a, b in m.SECTIONS:
        s_beat, e_beat = (a - 1) * m.BEATS_PER_BAR, b * m.BEATS_PER_BAR
        ns = [n for n in out if s_beat <= n["beat"] < e_beat]
        sections.append({"name": sec, "start_bar": a, "end_bar": b, "start_beat": s_beat, "end_beat": e_beat,
                         "start_time": round(m.sec_time(s_beat), 4), "end_time": round(m.sec_time(e_beat), 4),
                         "jump": sum(n["type"] == J for n in ns), "arms_up": sum(n["type"] == A for n in ns)})
    gaps_j = [b - a for (a, ta), (b, tb) in zip(notes, notes[1:]) if ta == J]
    jb = [b for b, t in notes if t == J]
    cue = set(base)
    data = {
        "song": m.NAME, "chart": f"{m.NAME}.{name}", "difficulty": name.split("_")[0], "variant": name.split("_")[1],
        "audio": f"{m.NAME}.ogg",
        "bpm": m.BPM, "time_signature": [4, 4], "bars": m.BARS, "offset_sec": m.OFFSET_SEC,
        "beat_unit": "beat 0 = bar 1 beat 1; time = offset_sec + beat * 60 / bpm",
        "length_sec": m.BARS * m.BEATS_PER_BAR * 60.0 / m.BPM,
        "audio_length_sec": m.BARS * m.BEATS_PER_BAR * 60.0 / m.BPM + m.TAIL_SEC,
        "counts": {"jump": len(jb), "arms_up": len(notes) - len(jb), "total": len(notes)},
        "rules": {"min_jump_gap_beats": m.MIN_JUMP_GAP, "min_after_jump_beats": m.MIN_AFTER_JUMP,
                  "min_note_gap_beats": 2,
                  "shortest_jump_to_jump": min(b - a for a, b in zip(jb, jb[1:])),
                  "shortest_after_jump": min(gaps_j) if gaps_j else None},
        "notes_on_music_cue": sum((n["beat"], n["type"]) in cue for n in out),
        "sections": sections,
        "notes": out,
    }
    return data

def click_ogg(m, x, sr, notes, dst):
    c = x * 0.6
    def click(freq, ms=25):
        t = np.arange(int(sr * ms / 1000)) / sr
        return np.sin(2 * np.pi * freq * t) * np.exp(-t * 180)
    for b, kind in notes:                               # same clicks as zoo_chase_v1_withclicks.ogg
        i = int(round(m.sec_time(b) * sr))
        k = click(1000) if kind == J else np.concatenate([click(2500), np.zeros(int(sr * 0.03)), click(2500)])
        c[i:i + len(k)] += 0.7 * k[:, None]
    tmp = dst + ".tmp.wav"
    m.write_wav(tmp, c, sr)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp, "-c:a", "libvorbis", "-q:a", "6", dst], check=True)
    os.remove(tmp)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--song-dir", default=HERE if os.path.exists(os.path.join(HERE, "make_zoo_chase.py")) else DEFAULT_SONG)
    repo_zoo = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Unity", "Assets", "Game", "Content", "Audio", "Music", "Zoo"))
    in_repo = os.path.isdir(repo_zoo)
    ap.add_argument("--out", default=repo_zoo if in_repo else HERE)
    ap.add_argument("--clicks", default=None if in_repo else HERE, help="folder for the click-check OGGs")
    ap.add_argument("--no-audio", action="store_true", help="beatmaps only, skip the click-check OGGs")
    a = ap.parse_args()
    if a.no_audio: a.clicks = None
    m = load_song_module(a.song_dir)
    base = sorted(m.NOTES)
    charts = {}
    for name in ORDER:
        if name == "normal_a":
            charts[name] = list(base)
        elif name.startswith("hard"):
            charts[name] = base + HARD_EXTRA[name]
        else:
            charts[name] = CHARTS[name]
        check(m, name, charts[name])
    for name in ("easy_a", "easy_b"):
        assert set(charts[name]) <= set(base), f"{name} must be a subset of normal_a"
    x = sr = None
    if a.clicks:
        wav = os.path.join(a.song_dir, f"{m.NAME}.wav")
        if not os.path.exists(wav):                     # repo: only the OGG is committed (next to the beatmaps)
            ogg = next(p for p in (os.path.join(a.song_dir, f"{m.NAME}.ogg"), os.path.join(a.out, f"{m.NAME}.ogg"))
                       if os.path.exists(p))
            wav = os.path.join(a.clicks, f"_{m.NAME}_decoded.wav"); os.makedirs(a.clicks, exist_ok=True)
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", ogg, "-c:a", "pcm_s16le", wav], check=True)
            x, sr = m.read_wav(wav); os.remove(wav)
        else:
            x, sr = m.read_wav(wav)
    os.makedirs(a.out, exist_ok=True)
    summary = []
    for name in ORDER:
        d = beatmap(m, name, charts[name], base)
        with open(os.path.join(a.out, f"{m.NAME}.{name}.beatmap.json"), "w", encoding="utf-8") as f:
            json.dump(d, f, indent=2, ensure_ascii=False)
        if x is not None:
            click_ogg(m, x, sr, sorted(charts[name]), os.path.join(a.clicks, f"{m.NAME}.{name}_withclicks.ogg"))
        c = d["counts"]
        summary.append((name, c["total"], c["jump"], c["arms_up"], round(100 * c["total"] / len(base)),
                        d["rules"]["shortest_jump_to_jump"], d["rules"]["shortest_after_jump"], d["notes_on_music_cue"],
                        [(s["name"], s["jump"], s["arms_up"]) for s in d["sections"] if s["jump"] or s["arms_up"]]))
    print("chart      total jump arms  %normal  minJJ minAfterJ onCue  sections")
    for s in summary:
        print(f"{s[0]:<10} {s[1]:>5} {s[2]:>4} {s[3]:>4} {s[4]:>7}% {s[5]:>6} {s[6]:>9} {s[7]:>5}  {s[8]}")

if __name__ == "__main__":
    main()
