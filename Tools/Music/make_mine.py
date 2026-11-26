"""Mongscape mine loop: rattling cart rhythm, metal percussion, police chase tension.

D minor, 132 BPM, 4/4, 32 bars = 58.18 s loop.
Form: A (1-16, banjo lead, two different 8-bar halves) B (17-28, fiddle lead over a banjo roll)
bridge (29-32, stop-time with a short harmonica phrase, back into A).
The cart rumble (MineCart_Rumble.wav) owns the low end, so the bass is short slap staccato and
there is no low sustained pad.
Layers: base (bass, banjo, guitar chug, fiddle, harmonica, light metal percussion, high strings in B)
+ tension (taiko, snare, toms, low short brass stabs) for when the police are close.

Usage: python make_mine.py
"""
from musiclib import Song, n, HAT, PEDAL, AGOGO_HI, AGOGO_LO, COWBELL, SNARE, CRASH, TOM_HI, TOM_HM, TOM_LM, TOM_LO, FLOOR_HI, KICK, RIDE_BELL

song = Song("mine", "Mine", bpm=132, bars=32, tail_sec=3.0, seed=132)
song.sections = [("A1", 1, 8), ("A2", 9, 16), ("B", 17, 28), ("bridge", 29, 32)]
song.layer("base", "Slap bass staccato, banjo, guitar chug, fiddle, harmonica (bridge only), hi-hat chug and agogo, high strings in B")
song.layer("tension", "Taiko, snare, toms, low short brass stabs: police close behind")
song.state("base", "Mine start and mine fork, police far (default)", base=1, tension=0)
song.state("chase", "Police close behind (raise the tension layer with chase pressure)", base=1, tension=1)
song.reference = "base"
song.target_lufs = -18.0
song.fades = {"in_chase": .5, "in_base": 1.5}
# Sound review round 1: the tension layer's lowest hits masked the effects and set the true peak.
song.layer_eq = {"tension": [(80, -4.0)]}

bass = song.track("slap bass", "base", 36, volume=80, reverb=15, pan=64)
banjo = song.track("banjo", "base", 105, volume=110, reverb=30, pan=44)
guitar = song.track("guitar chug", "base", 25, volume=100, reverb=25, pan=90)
fiddle = song.track("fiddle", "base", 110, volume=82, reverb=45, pan=72)
harp = song.track("harmonica", "base", 22, volume=92, reverb=45, pan=60)
strings = song.track("high strings", "base", 48, volume=80, reverb=60, pan=64)
metal = song.track("metal percussion", "base", 0, volume=110, reverb=25, drum=True)
taiko = song.track("taiko", "tension", 116, volume=84, reverb=35, pan=64)
drums = song.track("drums", "tension", 0, volume=88, reverb=30, drum=True)
brass = song.track("brass stabs", "tension", 61, volume=106, reverb=35, pan=58)

CH = {"Dm": ("D2", ["D4", "F4", "A4"]), "Bb": ("Bb1", ["D4", "F4", "Bb4"]), "C": ("C2", ["E4", "G4", "C5"]),
      "Gm": ("G1", ["D4", "G4", "Bb4"]), "A7": ("A1", ["C#4", "E4", "G4"]), "F": ("F2", ["C4", "F4", "A4"])}
CHORDS = (["Dm", "Dm", "Bb", "C", "Dm", "Dm", "Gm", "A7", "Dm", "F", "Bb", "C", "Gm", "Dm", "A7", "A7"]
          + ["Bb", "C", "Dm", "Dm", "Bb", "C", "A7", "A7", "Gm", "Gm", "Dm", "Dm", "Bb", "C", "A7", "A7"])

BANJO_A = {
    1: [(0, "A4", .5), (.5, "D5", .5), (1, "F5", .5), (1.5, "E5", .5), (2, "D5", .5), (2.5, "A4", .5), (3, "C5", .5), (3.5, "D5", .5)],
    2: [(0, "F5", 1), (1, "E5", .5), (1.5, "D5", .5), (2, "E5", 1.5), (3.5, "A4", .5)],
    3: [(0, "D5", .5), (.5, "F5", .5), (1, "Bb5", .5), (1.5, "A5", .5), (2, "F5", .5), (2.5, "D5", .5), (3, "F5", 1)],
    4: [(0, "E5", .5), (.5, "G5", .5), (1, "E5", .5), (1.5, "D5", .5), (2, "C5", 1), (3, "E5", .5), (3.5, "G5", .5)],
    5: [(0, "A5", 1), (1, "F5", .5), (1.5, "D5", .5), (2, "A4", .5), (2.5, "D5", .5), (3, "F5", .5), (3.5, "A5", .5)],
    6: [(0, "G5", .5), (.5, "F5", .5), (1, "E5", .5), (1.5, "D5", .5), (2, "E5", .5), (2.5, "F5", .5), (3, "E5", 1)],
    7: [(0, "D5", .5), (.5, "G5", .5), (1, "Bb5", 1), (2, "A5", .5), (2.5, "G5", .5), (3, "F5", .5), (3.5, "E5", .5)],
    8: [(0, "E5", .5), (.5, "C#5", .5), (1, "A4", 1), (2, "G5", .5), (2.5, "E5", .5), (3, "C#5", .5), (3.5, "A4", .5)],
    9: [(0, "F5", .75), (.75, "E5", .25), (1, "D5", .5), (1.5, "F5", .5), (2.5, "A5", .5), (3, "G5", .5), (3.5, "F5", .5)],
    10: [(0, "A5", .75), (.75, "G5", .25), (1, "F5", .5), (1.5, "C5", .5), (2, "F5", 1), (3, "A5", 1)],
    11: [(0, "Bb5", .75), (.75, "A5", .25), (1, "F5", .5), (1.5, "D5", .5), (2, "Bb4", 1), (3, "D5", .5), (3.5, "F5", .5)],
    12: [(0, "G5", 1.5), (1.5, "E5", .5), (2, "C5", .5), (2.5, "E5", .5), (3, "G5", 1)],
    13: [(0, "Bb5", .5), (.5, "A5", .5), (1, "G5", .5), (1.5, "D5", .5), (2, "Bb4", .5), (2.5, "D5", .5), (3, "G5", 1)],
    14: [(0, "F5", .5), (.5, "E5", .5), (1, "D5", .5), (1.5, "A4", .5), (2, "D5", 1), (3, "F5", .5), (3.5, "A5", .5)],
    15: [(0, "G5", .5), (.5, "E5", .5), (1, "C#5", .5), (1.5, "E5", .5), (2, "A5", 1), (3, "G5", .5), (3.5, "E5", .5)],
    16: [(0, "C#5", 1), (1, "E5", 1), (2, "A4", 2)],
}
FIDDLE_B = {
    17: [(0, "D5", 1.5), (1.5, "F5", .5), (2, "Bb5", 2)],
    18: [(0, "A5", 1), (1, "G5", 1), (2, "E5", 1.5), (3.5, "C5", .5)],
    19: [(0, "D5", .5), (.5, "E5", .5), (1, "F5", 1), (2, "A5", 1), (3, "F5", 1)],
    20: [(0, "E5", 2), (2, "D5", 2)],
    21: [(0, "F5", 1.5), (1.5, "G5", .5), (2, "A5", 1), (3, "Bb5", 1)],
    22: [(0, "C6", 1.5), (1.5, "Bb5", .5), (2, "G5", 1), (3, "E5", 1)],
    23: [(0, "A5", 1), (1, "G5", .5), (1.5, "F5", .5), (2, "E5", 1), (3, "C#5", 1)],
    24: [(0, "E5", 3), (3, "A4", .5), (3.5, "C#5", .5)],
    25: [(0, "D5", .5), (.5, "G5", .5), (1, "Bb5", 1), (2, "A5", .5), (2.5, "G5", .5), (3, "F5", 1)],
    26: [(0, "D5", 2), (2, "G5", 1), (3, "F5", 1)],
    27: [(0, "E5", .5), (.5, "F5", .5), (1, "A5", 1.5), (2.5, "G5", .5), (3, "F5", 1)],
    28: [(0, "D5", 3)],
}
HARMONICA = {
    29: [(0, "F5", .5), (.5, "D5", .5), (1, "F5", .5), (1.5, "G5", .5), (2, "A5", 1.5)],
    30: [(0, "G5", .5), (.5, "E5", .5), (1, "C5", .5), (1.5, "E5", .5), (2, "G5", 1.5)],
}


def phrase(track, bar, notes, vel, dur_scale=.9):
    b0 = song.bar(bar)
    for off, name, dur in notes:
        track.note(b0 + off, name, song.hum(vel), dur * dur_scale)


for bar in range(1, 33):
    b0 = song.bar(bar)
    chord = CHORDS[bar - 1]
    root = n(CH[chord][0]); tones = [n(x) for x in CH[chord][1]]
    sec = "A1" if bar <= 8 else "A2" if bar <= 16 else "B" if bar <= 28 else "bridge"
    stop = sec == "bridge" and bar <= 30

    # ---- bass: short slap staccato, 'train' pattern; stop-time hits in the bridge
    if stop:
        for off in (0, 2.5):
            bass.note(b0 + off, root, song.hum(84), .2)
    elif bar >= 31:
        for k, x in enumerate([0, 0, 4, 4, 7, 7, 10, 12] if bar == 32 else [0, 12, 0, 12, 0, 12, 0, 12]):
            bass.note(b0 + k * .5, root + x, song.hum(86 if k % 2 == 0 else 76), .2)
    else:
        for off, x in ((0, 0), (.5, 0), (1.5, 12), (2, 0), (2.5, 7), (3.5, 12)):
            bass.note(b0 + off, root + x, song.hum(100 if off in (0, 2) else 84), .2)

    # ---- banjo: lead in A, forward roll under the fiddle in B and at the end of the bridge
    if sec in ("A1", "A2"):
        phrase(banjo, bar, BANJO_A[bar], 96, .8)
    elif sec == "B" or bar >= 31:
        roll = [tones[0], tones[1], tones[2] + 12, tones[0], tones[1] + 12, tones[2], tones[0] + 12, tones[1]]
        for k, x in enumerate(roll):
            banjo.note(b0 + k * .5, x, song.hum(70 if bar < 31 else 72 + k), .45)
    elif stop:
        banjo.chord(b0, tones, 70, .4); banjo.chord(b0 + 2.5, tones, 66, .4)

    # ---- guitar chug on the offbeats (A only)
    if sec in ("A1", "A2"):
        for off in (.5, 1.5, 2.5, 3.5):
            guitar.chord(b0 + off, [tones[0] - 12, tones[1] - 12, tones[2] - 12], song.hum(72), .15)

    # ---- fiddle (B), harmonica (bridge), high strings (B)
    if bar in FIDDLE_B:
        phrase(fiddle, bar, FIDDLE_B[bar], 96, .95)
    if bar in HARMONICA:
        phrase(harp, bar, HARMONICA[bar], 92, .9)
    if sec == "B":
        strings.chord(b0, [tones[1] + 12, tones[2] + 12], 58, 3.9)

    # ---- light metal percussion: hi-hat 16th chug, agogo, cowbell
    if stop:
        metal.note(b0, RIDE_BELL, 72, .3); metal.note(b0 + 2.5, RIDE_BELL, 68, .3)
    else:
        for k in range(16):
            metal.note(b0 + k * .25, HAT, song.hum([112, 64, 86, 70][k % 4], 4), .1)
        metal.note(b0 + 3.5, AGOGO_HI if bar % 2 else AGOGO_LO, song.hum(84), .2)
        metal.note(b0 + 1.5, AGOGO_LO, song.hum(64), .2)
        if bar % 4 == 1:
            metal.note(b0, COWBELL, 82, .2)

    # ================= tension layer
    if stop:
        taiko.note(b0, "D2", 100, 1); taiko.note(b0 + 2.5, "D2", 94, 1)
        drums.note(b0, CRASH, 80, 2) if bar == 29 else None
        drums.note(b0 + 2.5, SNARE, 92, .2)
        brass.chord(b0 + 2.5, [root + 12, root + 19], 90, .3)
        continue
    for off, v in ((0, 92), (1.5, 84), (2, 92), (3.5, 88)):
        taiko.note(b0 + off, "D2", song.hum(v), .5)
    if bar % 2 == 0:
        taiko.note(b0 + 2.75, "A1", song.hum(80), .3)
    for off in (1, 3):
        drums.note(b0 + off, SNARE, song.hum(92), .2)
    for off in (.75, 2.25, 2.75, 3.75):
        drums.note(b0 + off, SNARE, song.hum(40, 6), .1)
    drums.note(b0 + 2.5, KICK, song.hum(78), .2)   # beat 1 belongs to the taiko
    if bar in (1, 17):
        drums.note(b0, CRASH, 96, 2)
    if bar % 4 == 0:   # tom fill, last beat
        for k, t in enumerate([TOM_HI, TOM_HM, TOM_LM, FLOOR_HI]):
            drums.note(b0 + 3 + k * .25, t, 84 + k * 8, .2)
    # low, short brass stabs: every other bar on the 'and' of 2 and of 4
    if bar % 2 == 0:
        brass.chord(b0 + 1.5, [root + 12, root + 19], song.hum(92), .25)
        brass.chord(b0 + 3.5, [root + 12, root + 19], song.hum(98), .25)
    if bar >= 31:
        for k in range(4):
            brass.chord(b0 + k, [root + 12, root + 16 + (bar - 31) * 3], 80 + k * 4, .25)

if __name__ == "__main__":
    song.build()
