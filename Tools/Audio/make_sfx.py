"""Build Mongscape's sound effects from Kenney audio packs (CC0).

Usage:
    python make_sfx.py <kenney_dir>

<kenney_dir> holds the unzipped packs, one folder per pack, named after the
kenney.nl asset slug (impact-sounds/, interface-sounds/, digital-audio/,
rpg-audio/, sci-fi-sounds/, music-jingles/). Each pack is downloaded from
https://kenney.nl/assets/<slug>.

Every effect is decoded, mixed to mono, trimmed of leading/trailing silence,
given a 5 ms fade-out, true-peak-normalised to -1.2 dBTP (4x oversampled, so the
decoded OGG stays under -1 dBTP) and written as OGG Vorbis
(44.1 kHz mono). In-game loudness is set by the gain table in GameAudio.cs,
not by the files.

A few effects must cut through the loudest music (sound review round 1): they can
stack a second Kenney file (a mid/high layer over a low impact), be cut short, and
be driven into a soft saturation (tanh) that lifts their body and adds 1-6 kHz
harmonics. The searchlight alarm has no Kenney source and is synthesised here.

Needs: Python 3, NumPy, ffmpeg on PATH.
"""
import hashlib, json, os, subprocess, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
UNITY = os.path.normpath(os.path.join(HERE, "..", "..", "Unity", "Assets", "Game", "Content", "Audio"))
LIVE = os.path.join(UNITY, "Resources", "MuhanokAudio", "Sfx")   # loaded by GameAudio at runtime
NEXT = os.path.join(UNITY, "SfxV2")                               # picked for v2, not wired yet
SR = 44100
PEAK_DB = -1.2   # true peak, leaves 0.2 dB for the OGG encoder

# (output name, pack slug, file inside the pack, what it is for)
LIVE_SFX = [
    ("jump",        "rpg-audio",        "cloth1.ogg",                 "Jump: a cloth whoosh"),
    ("land_0",      "impact-sounds",    "impactSoft_medium_000.ogg",  "Landing after a jump"),
    ("land_1",      "impact-sounds",    "impactSoft_medium_001.ogg",  "Landing after a jump"),
    ("land_2",      "impact-sounds",    "impactSoft_medium_002.ogg",  "Landing after a jump"),
    ("hit",         "impact-sounds",    "impactPunch_heavy_000.ogg",  "Collision / caught",
     {"layer": ("impact-sounds", "impactMetal_medium_001.ogg", 1.0), "drive": 3.0}),
    ("bonk_0",      "impact-sounds",    "impactWood_heavy_000.ogg",   "Guard's baton hits during the capture scene"),
    ("bonk_1",      "impact-sounds",    "impactWood_heavy_001.ogg",   "Guard's baton hits during the capture scene"),
    ("bonk_2",      "impact-sounds",    "impactWood_heavy_002.ogg",   "Guard's baton hits during the capture scene"),
    ("cue",         "interface-sounds", "question_004.ogg",           "Coach hint: soft falling two-note tone"),
    ("count_beep",  "digital-audio",    "pepSound1.ogg",              "Countdown beep"),
    ("count_go",    "digital-audio",    "highUp.ogg",                 "Countdown start"),
    ("footstep_0",  "impact-sounds",    "footstep_concrete_000.ogg",  "Knee step on the prison stairs"),
    ("footstep_1",  "impact-sounds",    "footstep_concrete_001.ogg",  "Knee step on the prison stairs"),
    ("footstep_2",  "impact-sounds",    "footstep_concrete_002.ogg",  "Knee step on the prison stairs"),
    ("footstep_3",  "impact-sounds",    "footstep_concrete_003.ogg",  "Knee step on the prison stairs"),
    ("footstep_4",  "impact-sounds",    "footstep_concrete_004.ogg",  "Knee step on the prison stairs"),
    ("slip",        "digital-audio",    "phaserDown1.ogg",            "Slipping on a banana: cartoon slide down"),
    ("item",        "digital-audio",    "powerUp7.ogg",               "Item pickup"),
    ("shield",      "sci-fi-sounds",    "forceField_000.ogg",         "Shield saves the runner"),
    # Chaser cues (GameAudio.ChaserCue). Not Kenney: owner decision 2026-11-27 allows another CC0 source for the roar.
    ("lion_roar",   "oga-80-cc0-creture-sfx-2", "roar_04.ogg",        "Lion roar warning in the zoo (cartoon voice roar, pitched up 3 semitones)",
     {"pitch": 3, "drive": 2.0}),
]
NEXT_SFX = [
    ("lock_click",    "rpg-audio",       "metalClick.ogg",             "Prison lock: click while picking"),
    ("lock_open",     "rpg-audio",       "metalLatch.ogg",             "Prison lock: opens"),
    ("monkey_rescue", "music-jingles",   "jingles_PIZZI10.ogg",        "A monkey is rescued (short rising pizzicato)"),
    ("fork_blocked",  "impact-sounds",   "impactMetal_medium_003.ogg", "Leaning into a blocked fork: short metal clunk",
     {"layer": ("impact-sounds", "impactPlate_light_003.ogg", .8), "max_sec": .3, "drive": 6.0}),
    ("gate_down",     "rpg-audio",       "doorClose_1.ogg",            "Iron gate comes down", {"drive": 3.0}),
    ("warning",       "music-jingles",   "jingles_PIZZI05.ogg",        "First warning (short falling pizzicato)"),
    ("caught",        "music-jingles",   "jingles_PIZZI01.ogg",        "Caught (falling pizzicato)"),
    ("new_record",    "music-jingles",   "jingles_PIZZI02.ogg",        "New record (rising pizzicato)"),
]
MISSING = [("siren", "Police siren (kept as the synthesised loop in GameAudio.cs)")]
# Sources outside Kenney: folder name in <kenney_dir> -> (page, author, licence). The roar pack is unzipped from
# https://opengameart.org/sites/default/files/80-CC0-creature-sfx-2.zip into oga-80-cc0-creture-sfx-2/.
OTHER = {"oga-80-cc0-creture-sfx-2": ("https://opengameart.org/content/80-cc0-creture-sfx-2", "rubberduck (OpenGameArt)", "CC0-1.0")}
SYNTH = [("alarm_bell", "Prison searchlight alarm (GameAudio.ChaserCue): two sharp tones (1.4 / 1.9 kHz) alternating, 4 times, about 1 s")]


def find(pack_dir, name):
    for root, _, files in os.walk(pack_dir):
        if name in files:
            return os.path.join(root, name)
    raise FileNotFoundError(f"{name} not found in {pack_dir}")


def decode(path):
    r = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"],
                       capture_output=True, check=True)
    return np.frombuffer(r.stdout, np.float32).copy()


def clean(x):
    gate = np.abs(x).max() * 10 ** (-50 / 20)
    idx = np.nonzero(np.abs(x) > gate)[0]
    x = x[max(0, idx[0] - 32): idx[-1] + 1]
    fade = min(len(x), int(SR * .005))
    x[-fade:] *= np.linspace(1, 0, fade)
    return x * (10 ** (PEAK_DB / 20) / true_peak(x))


def true_peak(x):
    from scipy.signal import resample_poly
    return max(float(np.abs(x).max()), float(np.abs(resample_poly(x, 4, 1)).max()))


def encode(x, out):
    """Writes the OGG; if the decoded file's true peak is above -1 dBTP (encoder overshoot), lowers it and writes again."""
    os.makedirs(os.path.dirname(out), exist_ok=True)
    for _ in range(4):
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
                        "-c:a", "libvorbis", "-q:a", "6", out], input=x.astype(np.float32).tobytes(), check=True)
        over = true_peak(decode(out)) / 10 ** (-1.0 / 20)
        if over <= 1.0:
            return
        x = x / (over * 1.01)
    raise RuntimeError(f"{out}: true peak stays above -1 dBTP")


def sha(path):
    return hashlib.sha256(open(path, "rb").read()).hexdigest()


def process(kenney, x, opts):
    if "pitch" in opts:   # shift pitch, keep the length (resample, then time-stretch back with ffmpeg atempo)
        r = 2 ** (opts["pitch"] / 12)
        y = subprocess.run(["ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-", "-af",
                            f"asetrate={SR * r:.0f},aresample={SR},atempo={1 / r:.6f}", "-f", "f32le", "-"],
                           input=x.astype(np.float32).tobytes(), capture_output=True, check=True).stdout
        x = np.frombuffer(y, np.float32).copy()
    if "layer" in opts:
        pack, src, gain = opts["layer"]
        y = clean(decode(find(os.path.join(kenney, pack), src))) * gain
        n = max(len(x), len(y))
        x = np.pad(x, (0, n - len(x))) + np.pad(y, (0, n - len(y)))
    if "max_sec" in opts:
        x = x[: int(opts["max_sec"] * SR)].copy()
        fade = int(SR * .04)
        x[-fade:] *= np.linspace(1, 0, fade)
    if "drive" in opts:
        x = np.tanh(opts["drive"] * x / np.abs(x).max()) / np.tanh(opts["drive"])
    return clean(x)


def alarm():
    t = np.arange(int(SR * .12)) / SR
    def beep(f):
        sq = np.sign(np.sin(2 * np.pi * f * t)) * .35 + np.sin(2 * np.pi * f * t) * .65
        env = np.minimum(1, t / .004) * np.minimum(1, (t[-1] - t) / .01)
        return sq * env
    x = np.concatenate([np.concatenate([beep(1400), beep(1900)]) for _ in range(4)])
    return clean(x.astype(np.float32))


def build(kenney, table, folder, synth=()):
    rows = []
    for entry in table:
        name, pack, src, use = entry[:4]
        opts = entry[4] if len(entry) > 4 else {}
        path = find(os.path.join(kenney, pack), src)
        x = process(kenney, clean(decode(path)), opts)
        out = os.path.join(folder, name + ".ogg")
        encode(x, out)
        row = {"file": name + ".ogg", "use": use, "pack": pack, "source": f"https://kenney.nl/assets/{pack}",
               "original": src, "original_sha256": sha(path), "seconds": round(len(x) / SR, 3), "license": "CC0-1.0"}
        if pack in OTHER:
            row["source"], row["author"], row["license"] = OTHER[pack]
            row["download"] = "https://opengameart.org/sites/default/files/80-CC0-creature-sfx-2.zip"
        if "pitch" in opts:
            row.setdefault("processing", {})["pitch_semitones"] = opts["pitch"]
        if "layer" in opts:
            lpack, lsrc, lgain = opts["layer"]
            lpath = find(os.path.join(kenney, lpack), lsrc)
            row["layered_with"] = {"pack": lpack, "original": lsrc, "original_sha256": sha(lpath), "gain": lgain}
        if "max_sec" in opts or "drive" in opts:
            row.setdefault("processing", {}).update({k: opts[k] for k in ("max_sec", "drive") if k in opts})
        rows.append(row)
        print(f"{name:14s} <- {pack}/{src}  {len(x) / SR:.2f}s")
    for name, use in synth:
        x = alarm()
        encode(x, os.path.join(folder, name + ".ogg"))
        rows.append({"file": name + ".ogg", "use": use, "source": "Synthesised by Tools/Audio/make_sfx.py (project)",
                     "seconds": round(len(x) / SR, 3), "license": "Project"})
        print(f"{name:14s} <- synthesised  {len(x) / SR:.2f}s")
    with open(os.path.join(folder, "SOURCES.json"), "w", encoding="utf-8") as f:
        json.dump({"license": "CC0-1.0 (Creative Commons Zero). Kenney (www.kenney.nl) unless an entry names another source",
                   "effects": rows,
                   "missing_in_kenney": [{"name": n, "use": u} for n, u in MISSING]}, f, indent=2)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    build(sys.argv[1], LIVE_SFX, LIVE, SYNTH)
    build(sys.argv[1], NEXT_SFX, NEXT)
