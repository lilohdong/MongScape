"""Game copy of the approved zoo song (zoo_chase_v1).

zoo/make_zoo_chase.py renders the song exactly as approved (-19.7 LUFS, true peak -0.9 dBTP).
To match the other songs' reference level (-18 LUFS) without clipping, this step raises it by
1.9 dB and catches only the loudest hits (the jump signal hits) with a fast peak limiter.
The arrangement and timing are unchanged, so the beatmap still matches sample for sample.

Usage: python zoo/make_zoo_chase.py && python make_zoo_game.py
"""
import json, os, shutil, subprocess
import musiclib as m

SRC = os.path.join(m.OUT_ROOT, "zoo")
DEST = os.path.join(m.UNITY_MUSIC, "Zoo")
GAIN_DB, LIMIT = 1.9, 0.79   # limiter ceiling 0.79 = -2.0 dBFS sample peak, leaves room for true peak

if __name__ == "__main__":
    os.makedirs(DEST, exist_ok=True)
    out = os.path.join(DEST, "zoo_chase_v1.ogg")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", os.path.join(SRC, "zoo_chase_v1.wav"),
                    "-af", f"volume={GAIN_DB}dB,alimiter=limit={LIMIT}:attack=3:release=80:level=false:asc=false",
                    "-c:a", "libvorbis", "-q:a", "6", out], check=True)
    shutil.copyfile(os.path.join(SRC, "zoo_chase_v1.beatmap.json"), os.path.join(DEST, "zoo_chase_v1.beatmap.json"))
    before = m.measure(m.decode(os.path.join(SRC, "zoo_chase_v1.ogg")))
    after = m.measure(m.decode(out))
    data = {
        "song": "zoo_chase_v1", "bpm": 160, "loop": False, "length_seconds": 61.5,
        "playback": "Played once per zoo visit, in sync with zoo_chase_v1.beatmap.json (beat 0 = first sample).",
        "layers": {"full": {"file": "zoo_chase_v1.ogg", "meaning": "The whole approved song (no separate layers)"}},
        "states": {"run": {"meaning": "Zoo run", "layers": {"full": 1},
                           "lufs": round(after["lufs"], 1), "true_peak_dbtp": round(after["true_peak"], 1)}},
        "source_render": {"lufs": round(before["lufs"], 1), "true_peak_dbtp": round(before["true_peak"], 1)},
        "game_copy": f"+{GAIN_DB} dB, peak limiter at {LIMIT} (sample peak)",
    }
    with open(os.path.join(DEST, "zoo_chase_v1.layers.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    print("zoo source", before, "-> game copy", after)
