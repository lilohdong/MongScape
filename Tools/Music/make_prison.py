"""Mongscape prison loop: low bass, quiet tension, holding the breath under the searchlight.

C minor, 96 BPM, 4/4, 16 bars = 40.0 s loop. The loop is not cut to the set length
(stairs 10 s + hide 5-7 s + flat 3 s); the game switches layers instead.
96 BPM matches the plan's knee-raise pace (80-100 per minute), so the steps layer is a pace guide.

Layers
  base     bass pulse, low string pad, clock tick (side stick), sparse piano / low clarinet motif
  steps    one low drum on every beat (stairs)
  tension  snare, toms, kick, hi-hat (guard chase)
  hide     replaces everything while hiding: low drone, heartbeat timpani, soft clock tick.
           Leaves room for the lock click and the searchlight alarm.
States: flat = base, stairs = base + steps, hide = hide only, chase = base + steps + tension.
Fades (game side): into hide 0.3 s, out of hide 1.0 s, others 0.5 s.

Usage: python make_prison.py
"""
from musiclib import Song, n, RIM, FLOOR_LO, SNARE, KICK, HAT, CRASH, TOM_HI, TOM_HM, TOM_LM, FLOOR_HI, PEDAL

song = Song("prison", "Prison", bpm=96, bars=16, tail_sec=3.5, seed=96)
song.sections = [("A", 1, 8), ("B", 9, 16)]
song.layer("base", "Bass pulse, low string pad, clock tick (side stick), sparse piano and low clarinet motif")
song.layer("steps", "One low drum on every beat: the stair pace (96 per minute)")
song.layer("tension", "Snare, toms, kick and hi-hat: the guard chase after the first alarm")
song.layer("hide", "Played alone while hiding under the searchlight: low drone, heartbeat timpani, soft clock tick")
song.state("flat", "Short flat stretch between sets (catch your breath)", base=1, steps=0, tension=0, hide=0)
song.state("stairs", "Stairs, knee raises (most heard state)", base=1, steps=1, tension=0, hide=0)
song.state("hide", "Hiding under the searchlight while the lock is picked", base=0, steps=0, tension=0, hide=1)
song.state("chase", "Guard chase: the stairs after the first alarm", base=1, steps=1, tension=1, hide=0)
song.reference = "stairs"
song.target_lufs = -18.0
song.fades = {"in_hide": .3, "out_hide": 1.0, "default": .5}

bass = song.track("bass", "base", 33, volume=84, reverb=20, pan=64)
pad = song.track("low strings", "base", 48, volume=84, reverb=55, pan=60)
tick = song.track("clock tick", "base", 0, volume=100, reverb=35, drum=True)
piano = song.track("piano", "base", 0, volume=120, reverb=55, pan=70)
clar = song.track("low clarinet", "base", 71, volume=92, reverb=50, pan=56)
steps = song.track("step drum", "steps", 0, volume=110, reverb=30, drum=True)
drums = song.track("chase drums", "tension", 0, volume=90, reverb=30, drum=True)
drone = song.track("drone", "hide", 43, volume=70, reverb=60, pan=64)
trem = song.track("tremolo", "hide", 44, volume=79, reverb=70, pan=70)
heart = song.track("heartbeat", "hide", 47, volume=70, reverb=30, pan=64)
softtick = song.track("soft tick", "hide", 0, volume=96, reverb=40, drum=True)

CH = {"Cm": ("C2", ["C3", "Eb3", "G3"]), "Ab": ("Ab1", ["C3", "Eb3", "Ab3"]), "Fm": ("F1", ["C3", "F3", "Ab3"]),
      "G": ("G1", ["B2", "D3", "G3"]), "Eb": ("Eb2", ["Bb2", "Eb3", "G3"]), "G7": ("G1", ["B2", "F3", "Ab3"])}
CHORDS = ["Cm", "Cm", "Ab", "Ab", "Fm", "Fm", "G", "G", "Cm", "Eb", "Ab", "G", "Fm", "Ab", "G", "G7"]

PIANO = {
    1: [(0, "G4", .5), (1, "Ab4", .5), (1.5, "G4", .5), (3, "Eb4", 1)],
    2: [(.5, "D4", .5), (1, "C4", 1.5)],
    3: [(0, "C5", .5), (1, "Bb4", .5), (1.5, "Ab4", .5), (3, "Eb4", 1)],
    4: [(.5, "F4", .5), (1, "G4", 2)],
    5: [(0, "Ab4", .5), (1, "G4", .5), (1.5, "F4", .5), (3, "C4", 1)],
    6: [(.5, "Eb4", .5), (1, "D4", 1.5)],
    7: [(0, "B3", .5), (1, "D4", .5), (1.5, "F4", .5), (2, "Ab4", 1), (3, "G4", 1)],
    8: [(2, "B3", .5), (2.5, "D4", .5)],
}
CLARINET = {
    9: [(0, "C4", 1.5), (1.5, "Eb4", .5), (2, "G4", 2)],
    10: [(0, "Bb3", 1), (1, "G3", 1), (2, "Eb4", 2)],
    11: [(0, "C4", 1), (1, "Ab3", 1), (2, "Eb4", 2)],
    12: [(0, "D4", 2), (2, "B3", 2)],
    13: [(0, "F4", 1), (1, "Ab4", 1), (2, "C5", 1), (3, "Ab4", 1)],
    14: [(0, "G4", 2), (2, "Eb4", 2)],
    15: [(0, "D4", 1), (1, "F4", 1), (2, "Ab4", 1), (3, "B3", 1)],
    16: [(0, "D4", 2)],
}

for bar in range(1, 17):
    b0 = song.bar(bar)
    chord = CHORDS[bar - 1]
    root = n(CH[chord][0]); tones = [n(x) for x in CH[chord][1]]

    # ---- base
    for k in range(8):   # bass pulse: 8ths, root with a fifth on the 'and' of 2
        x = root + (7 if k == 3 else 12 if k == 6 else 0)
        bass.note(b0 + k * .5, x, song.hum(84 if k % 2 == 0 else 66, 4), .4)
    pad.chord(b0, tones, 64 if bar <= 8 else 70, 3.95)
    for k in range(4):   # clock tick on every beat
        tick.note(b0 + k, RIM, song.hum(92 if k == 0 else 78, 3), .1)
    if bar in PIANO:
        for off, name, dur in PIANO[bar]:
            piano.note(b0 + off, name, song.hum(82), dur * .9)
    if bar in CLARINET:
        for off, name, dur in CLARINET[bar]:
            clar.note(b0 + off, name, song.hum(72), dur * .95)

    # ---- steps: one low drum per beat, beat 1 a little stronger
    for k in range(4):
        steps.note(b0 + k, FLOOR_LO, song.hum(94 if k == 0 else 82, 3), .3)

    # ---- tension: guard chase drums
    for k in range(16):
        drums.note(b0 + k * .25, HAT, song.hum([84, 40, 60, 44][k % 4], 4), .1)
    for off in (0, .75, 2, 2.5):
        drums.note(b0 + off, KICK, song.hum(86 if off in (0, 2) else 72), .2)
    for off in (1, 3):
        drums.note(b0 + off, SNARE, song.hum(90), .2)
    for off in (1.75, 3.25, 3.75):
        drums.note(b0 + off, SNARE, song.hum(44, 6), .1)
    if bar in (1, 9):
        drums.note(b0, CRASH, 84, 2)
    if bar % 4 == 0:
        for k, t in enumerate([TOM_HI, TOM_HM, TOM_LM, FLOOR_HI]):
            drums.note(b0 + 3 + k * .25, t, 80 + k * 8, .2)

    # ---- hide: drone, heartbeat, soft tick
    if bar % 4 == 1:
        drone.chord(b0, [n("C2"), n("G2")], 70, 15.9)
        trem.chord(b0, [n("C4"), n("Eb4")] if bar <= 8 else [n("B3"), n("D4")], 46, 15.9)
    for k in range(4):
        heart.note(b0 + k, "C2", song.hum(78, 3), .3)
        heart.note(b0 + k + .375, "C2", song.hum(56, 3), .3)
        softtick.note(b0 + k + .5, RIM, song.hum(60, 2), .1)

DEMO = [("stairs", 10), ("hide", 5), ("flat", 3), ("stairs", 10), ("hide", 6), ("flat", 3),
        ("chase", 10), ("hide", 7), ("flat", 3)]

if __name__ == "__main__":
    song.build(demo=DEMO)
