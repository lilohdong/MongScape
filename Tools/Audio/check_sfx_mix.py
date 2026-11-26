"""Checks that sound effects stay audible over the music (and that repeating ones stay under it).

For every song the loudest state (all tension layers on) is mixed at the default music volume,
each effect at the default effects volume times its in-game gain. The script compares the
effect's loudest 400 ms (momentary loudness, BS.1770 K-weighting) with the music's loud
moments (95th percentile of the music's momentary loudness), and writes a listening file per
song: the loudest state with the effects dropped in one after another.

Usage: python check_sfx_mix.py [out_dir]     (after make_sfx.py and the Tools/Music builds)
"""
import json, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "Music"))
import musiclib as m  # noqa: E402

AUDIO = os.path.normpath(os.path.join(HERE, "..", "..", "Unity", "Assets", "Game", "Content", "Audio"))
LIVE = os.path.join(AUDIO, "Resources", "MuhanokAudio", "Sfx")
NEXT = os.path.join(AUDIO, "SfxV2")
MUSIC_VOLUME, EFFECTS_VOLUME = .35, .7   # GameSettings defaults

# (file, gain) - live gains match GameAudio.cs; v2 gains are the proposed starting values.
EFFECTS = [
    (os.path.join(LIVE, "hit.ogg"), 1.0), (os.path.join(LIVE, "slip.ogg"), .75), (os.path.join(LIVE, "item.ogg"), .8),
    (os.path.join(LIVE, "shield.ogg"), .6), (os.path.join(LIVE, "count_go.ogg"), .75), (os.path.join(LIVE, "jump.ogg"), .8),
    (os.path.join(LIVE, "land_0.ogg"), .6), (os.path.join(LIVE, "bonk_0.ogg"), .75),
    (os.path.join(NEXT, "warning.ogg"), 1.0), (os.path.join(NEXT, "caught.ogg"), 1.0), (os.path.join(NEXT, "lock_open.ogg"), 1.0),
    (os.path.join(NEXT, "lock_click.ogg"), .7), (os.path.join(NEXT, "monkey_rescue.ogg"), .7),
    (os.path.join(NEXT, "fork_blocked.ogg"), 1.0), (os.path.join(NEXT, "gate_down.ogg"), .9), (os.path.join(NEXT, "new_record.ogg"), 1.0),
    (os.path.join(NEXT, "spotlight_alarm.ogg"), .45), (os.path.join(LIVE, "cue.ogg"), .75), (os.path.join(LIVE, "count_beep.ogg"), .9),
    ("siren", 1.0),
]
# Effects that duck the music by DUCK_DB while they play (GameAudio.Duck); margins are reported with and without.
DUCKED = {"hit.ogg", "warning.ogg", "caught.ogg", "fork_blocked.ogg", "spotlight_alarm.ogg"}
DUCK_DB = 5.0


def siren():
    """The synthesised siren loop of GameAudio.cs at full danger (volume = effects volume x 0.45)."""
    n = m.SR * 2
    f = np.where(np.arange(n) < n // 2, 650.0, 900.0)
    ph = np.cumsum(2 * np.pi * f / m.SR)
    x = np.where(np.sin(ph) > 0, .3, -.3) * .5 + np.sin(ph) * .25
    x = np.tile(x, 2).astype(np.float32) * .45
    return np.stack([x, x], 1)
UNDER = [(os.path.join(LIVE, "footstep_0.ogg"), .6)]   # must sit under the music
LOUDEST = {"Menu": "full", "Mine": "chase", "Prison": "chase", "Zoo": "run", "Result": "full"}
HIDE = ("Prison", "hide")   # the lock and alarm sounds land in the hide state: check it too


def k_filter(x):
    """BS.1770 K-weighting (pre-filter shelf + RLB high-pass) for 44.1 kHz."""
    from math import tan, pi, sqrt
    def biquad(x, b, a):
        y = np.zeros_like(x)
        x1 = x2 = y1 = y2 = np.zeros(x.shape[1])
        for i in range(len(x)):   # small inputs only (400 ms windows are summed below)
            v = b[0] * x[i] + b[1] * x1 + b[2] * x2 - a[1] * y1 - a[2] * y2
            x2, x1, y2, y1 = x1, x[i], y1, v
            y[i] = v
        return y
    fs = m.SR
    f0, G, Q = 1681.974450955533, 3.999843853973347, 0.7071752369554196
    K = tan(pi * f0 / fs); Vh = 10 ** (G / 20); Vb = Vh ** 0.4996667741545416
    a0 = 1 + K / Q + K * K
    b1 = [(Vh + Vb * K / Q + K * K) / a0, 2 * (K * K - Vh) / a0, (Vh - Vb * K / Q + K * K) / a0]
    a1 = [1, 2 * (K * K - 1) / a0, (1 - K / Q + K * K) / a0]
    f0, Q = 38.13547087602444, 0.5003270373238773
    K = tan(pi * f0 / fs)
    b2 = [1, -2, 1]
    a2 = [1, 2 * (K * K - 1) / (1 + K / Q + K * K), (1 - K / Q + K * K) / (1 + K / Q + K * K)]
    try:
        from scipy.signal import lfilter
        return lfilter(b2, a2, lfilter(b1, a1, x, axis=0), axis=0)
    except ImportError:
        return biquad(biquad(x, b1, a1), b2, a2)


def momentary(x):
    """Momentary loudness (400 ms windows, 100 ms hop), LUFS."""
    y = k_filter(x.astype(np.float64)) ** 2
    e = y.sum(axis=1)
    c = np.concatenate([[0], np.cumsum(e)])
    w, hop = int(.4 * m.SR), int(.1 * m.SR)
    if len(e) < w:
        e = np.concatenate([e, np.zeros(w - len(e))]); c = np.concatenate([[0], np.cumsum(e)])
    ms = (c[w::hop] - c[:-w:hop][:len(c[w::hop])]) / w
    return -0.691 + 10 * np.log10(ms + 1e-12)


def song_state(title, state):
    dest = os.path.join(m.UNITY_MUSIC, title)
    meta = json.load(open(next(os.path.join(dest, f) for f in os.listdir(dest) if f.endswith(".layers.json")), encoding="utf-8"))
    layers = meta["states"][state]["layers"]
    return m.state_mix({l: m.decode(os.path.join(dest, meta["layers"][l]["file"])) for l in layers if layers[l] > 0},
                       {l: v for l, v in layers.items() if v > 0})


def main(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    fx = {p: (siren() if p == "siren" else m.decode(p)) * EFFECTS_VOLUME * g for p, g in EFFECTS + UNDER}
    fx_loud = {p: float(momentary(x).max()) for p, x in fx.items()}
    rows, worst = [], {}
    for title, state in list(LOUDEST.items()) + [HIDE]:
        music = song_state(title, state) * MUSIC_VOLUME
        mm = momentary(music[: m.SR * 30])
        loud = float(np.percentile(mm, 95))
        for p, g in EFFECTS + UNDER:
            margin = fx_loud[p] - loud
            ducked = margin + DUCK_DB if os.path.basename(p) in DUCKED else None
            rows.append((title, state, os.path.basename(p), g, round(fx_loud[p], 1), round(loud, 1), round(margin, 1),
                         None if ducked is None else round(ducked, 1)))
        # listening file: effects dropped every 1.5 s over the music
        mix = music[: m.SR * 30].copy()
        for i, (p, _) in enumerate(EFFECTS + UNDER):
            at = int((1 + 1.5 * i) * m.SR)
            if at >= len(mix):
                break
            x = fx[p][: len(mix) - at]
            mix[at: at + len(x)] += x
        m.encode(mix, os.path.join(out_dir, f"sfx_over_{title.lower()}_{state}.ogg"))
    print(f"{'song':8s} {'state':7s} {'effect':22s} gain  fx LUFS  music LUFS  margin  ducked")
    for r in rows:
        print(f"{r[0]:8s} {r[1]:7s} {r[2]:22s} {r[3]:4.2f}  {r[4]:7.1f}  {r[5]:9.1f}  {r[6]:+6.1f}  " + ("" if r[7] is None else f"{r[7]:+6.1f}"))
    with open(os.path.join(out_dir, "sfx_over_music.json"), "w", encoding="utf-8") as f:
        json.dump([dict(zip(["song", "state", "effect", "gain", "effect_momentary_max_lufs",
                             "music_momentary_p95_lufs", "margin_lu", "margin_with_duck_lu"], r)) for r in rows], f, indent=2)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(m.OUT_ROOT, "sfx_check"))
