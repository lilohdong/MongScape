"""Mongscape main menu loop: playful, light 'getting ready to escape'.

F major, 112 BPM, 4/4, 32 bars = 68.57 s loop.
Form: A (1-8) B (9-16) A' (17-24) bridge (25-32), bar 32 is C7 back into bar 1.
The 4-bar marimba motif (bars 1-4) is the main theme; the result loop quotes it slowly.
Layers: base (pizzicato, bass, marimba, clarinet, bassoon, glockenspiel) + rhythm (shaker, woodblock, claves; no kick).

Usage: python make_menu.py
"""
from musiclib import Song, n, SHAKER, WOOD_HI, WOOD_LO, CLAVES, TRIANGLE_OPEN, TAMB

song = Song("menu", "Menu", bpm=112, bars=32, tail_sec=3.0, seed=112)
song.sections = [("A", 1, 8), ("B", 9, 16), ("A2", 17, 24), ("bridge", 25, 32)]
song.layer("base", "Pizzicato strings, bass, marimba theme, clarinet and bassoon dialogue, glockenspiel")
song.layer("rhythm", "Shaker, woodblock and claves (no kick). Can be muted for a calmer screen")
song.state("full", "Title, menu, trait screen (default)", base=1, rhythm=1)
song.state("calm", "Optional quieter variant: rhythm layer muted", base=1, rhythm=0)
song.reference = "full"
song.target_lufs = -20.0
song.fades = {"default": 1.0}

marimba = song.track("marimba", "base", 12, volume=122, reverb=40, pan=48)
clar = song.track("clarinet", "base", 71, volume=92, reverb=45, pan=80)
bsn = song.track("bassoon", "base", 70, volume=96, reverb=40, pan=50)
pizz = song.track("pizzicato", "base", 45, volume=88, reverb=45, pan=72)
bass = song.track("bass", "base", 32, volume=82, reverb=25, pan=64)
glock = song.track("glockenspiel", "base", 9, volume=70, reverb=60, pan=88)
kit = song.track("percussion", "rhythm", 0, volume=127, reverb=30, drum=True)

CH = {  # bass root, chord tones (for pizzicato and arpeggios)
    "F": ("F2", ["F3", "A3", "C4"]), "C7": ("C2", ["E3", "G3", "Bb3"]), "C": ("C2", ["E3", "G3", "C4"]),
    "Bb": ("Bb1", ["F3", "Bb3", "D4"]), "Dm": ("D2", ["F3", "A3", "D4"]), "Am": ("A1", ["E3", "A3", "C4"]),
    "Gm": ("G1", ["G3", "Bb3", "D4"]), "A7": ("A1", ["G3", "A3", "C#4"]),
}
FIFTH = {"F": "C3", "C7": "G2", "C": "G2", "Bb": "F2", "Dm": "A2", "Am": "E2", "Gm": "D2", "A7": "E2"}
A_CHORDS = ["F", "C7", "F", "C", "F", "C7", "Bb", ("C7", "F")]
B_CHORDS = ["Dm", "Am", "Bb", "F", "Gm", "C", "A7", ("Dm", "C7")]
BR_CHORDS = ["Bb", "Bb", "Gm", "Gm", "Dm", "Dm", "C", "C7"]
CHORDS = A_CHORDS + B_CHORDS + A_CHORDS + BR_CHORDS

# The theme (bars 1-8). Bars 1-4 are the short motif the result loop reuses.
THEME = [
    [(0, "C5", .5), (1, "A4", .5), (1.5, "Bb4", .5), (2, "C5", .5), (3, "F4", .5)],
    [(0, "G4", .5), (.5, "A4", .5), (1, "Bb4", .5), (2, "A4", 1), (3.5, "G4", .5)],
    [(0, "C5", .5), (1, "A4", .5), (1.5, "Bb4", .5), (2, "C5", .5), (2.5, "D5", .5), (3, "F5", 1)],
    [(0, "E5", .5), (.5, "D5", .5), (1, "C5", 1.5)],
    [(0, "C5", .5), (1, "A4", .5), (1.5, "Bb4", .5), (2, "C5", .5), (3, "F4", .5)],
    [(0, "G4", .5), (.5, "A4", .5), (1, "Bb4", .5), (2, "A4", 1), (3.5, "G4", .5)],
    [(0, "D5", .5), (1, "Bb4", .5), (1.5, "C5", .5), (2, "D5", .5), (2.5, "F5", .5), (3, "D5", 1)],
    [(0, "C5", .5), (.5, "Bb4", .5), (1, "G4", .5), (1.5, "E4", .5), (2, "F4", 1.5)],
]


def phrase(track, bar, notes, vel, shift=0, short=None, spread=5):
    b0 = song.bar(bar)
    for off, name, dur in notes:
        track.note(b0 + off, n(name) + shift, song.hum(vel, spread), short if short else dur * .92)


def chords_of(bar):
    c = CHORDS[bar - 1]
    return (c, c) if isinstance(c, str) else c


for bar in range(1, 33):
    b0 = song.bar(bar)
    first, second = chords_of(bar)
    sec = "A" if bar <= 8 else "B" if bar <= 16 else "A2" if bar <= 24 else "bridge"
    build = (bar - 25) / 7 if sec == "bridge" else 0

    # ---- bass: roots on 1 and 3 (walking quarters in the bridge)
    if sec == "bridge":
        root = n(CH[first][0]); walk = [root, root + 4 if first in ("Bb", "C", "C7") else root + 3, root + 7, root + 9 if bar % 2 else root + 5]
        if bar == 32:
            walk = [n("C2"), n("E2"), n("G2"), n("Bb2")]
        for k, x in enumerate(walk):
            bass.note(b0 + k, x, song.hum(92 if k == 0 else 80), .5)
    else:
        bass.note(b0, CH[first][0], song.hum(96), .55)
        bass.note(b0 + 2, CH[second][0] if second != first else FIFTH[first], song.hum(86), .55)

    # ---- pizzicato: offbeat 'oom-pah' stabs on 2 and 4, tiptoe 8ths in the bridge
    if sec == "bridge":
        tones = CH[first][1]
        for k in range(8):
            if bar == 32 and k >= 4:
                break
            pizz.note(b0 + k * .5, n(tones[k % 3]) + (12 if k % 4 == 3 else 0), song.hum(52 + 36 * build + (8 if k % 2 == 0 else 0)), .3)
    else:
        for beat, c in ((1, first), (3, second)):
            pizz.chord(b0 + beat, [n(x) for x in CH[c][1]], song.hum(70 if sec != "B" else 60), .3)
        if bar % 4 == 0 and sec != "B":
            pizz.note(b0 + 3.5, n(CH[second][1][2]) + 12, song.hum(62), .2)

    # ---- marimba
    if sec in ("A", "A2"):
        phrase(marimba, bar, THEME[(bar - 1) % 8], 96 if sec == "A" else 88, short=.4)
    elif sec == "B":   # soft broken chords under the dialogue
        tones = [n(x) + 12 for x in CH[first][1]]
        for k, i in enumerate([0, 1, 2, 1, 0, 1, 2, 1]):
            tone = [n(x) + 12 for x in CH[second][1]][i] if k >= 4 else tones[i]
            marimba.note(b0 + k * .5, tone, song.hum(48), .35)
    elif bar == 32:  # pickup run back into the theme
        for k, name in enumerate(["C5", "D5", "E5", "F5", "G5", "F5", "E5", "D5"]):
            marimba.note(b0 + 2 + k * .25, name, 60 + k * 4, .22)

    # ---- glockenspiel sparkles at phrase ends
    if bar in (4, 20):
        glock.note(b0 + 2.5, "G5", 70, .5); glock.note(b0 + 3, "C6", 80, 1)
    if bar in (8, 24):
        glock.note(b0 + 2, "F5", 75, 1); glock.note(b0 + 2, "C6", 65, 1)
    if bar == 28:
        glock.note(b0 + 3, "D6", 60, 1)

# ---- A: bassoon comic answers
for bar, notes in ((4, [(2, "E3", .5), (2.5, "F3", .5), (3, "G3", 1)]), (6, [(3, "Bb2", .5), (3.5, "C3", .5)])):
    phrase(bsn, bar, notes, 84)

# ---- B: clarinet calls, bassoon answers
B_DIALOGUE = {
    9: (clar, [(0, "A4", 1), (1, "D5", .5), (1.5, "E5", .5), (2, "F5", 1), (3, "E5", .5), (3.5, "D5", .5)]),
    10: (clar, [(0, "C5", 1), (1, "E5", 1), (2, "A4", 1.5)]),
    11: (bsn, [(0, "D3", .5), (.5, "F3", .5), (1, "Bb3", 1), (2, "A3", .5), (2.5, "G3", .5), (3, "F3", 1)]),
    12: (bsn, [(0, "A3", 1), (1, "C4", .5), (1.5, "A3", .5), (2, "F3", 1.5)]),
    13: (clar, [(0, "Bb4", .5), (.5, "D5", .5), (1, "G5", 1), (2, "F5", .5), (2.5, "D5", .5), (3, "Bb4", 1)]),
    14: (clar, [(0, "C5", .5), (.5, "E5", .5), (1, "G5", 1.5), (3, "E5", 1)]),
}
for bar, (tr, notes) in B_DIALOGUE.items():
    phrase(tr, bar, notes, 86)
phrase(clar, 15, [(0, "E5", 1), (1, "C#5", 1), (2, "A4", 1), (3, "G4", 1)], 84)
phrase(bsn, 15, [(0, "A2", 1), (1, "C#3", 1), (2, "E3", 1), (3, "G3", 1)], 80)
phrase(clar, 16, [(0, "F4", .5), (.5, "A4", .5), (1, "D5", 1), (2, "E5", .5), (2.5, "D5", .5), (3, "C5", .5), (3.5, "Bb4", .5)], 84)
phrase(bsn, 16, [(0, "D3", 1), (2, "C3", 1), (3, "E3", 1)], 78)

# ---- A': clarinet doubles the theme an octave down, bassoon holds chord thirds
for bar in range(17, 25):
    phrase(clar, bar, THEME[bar - 17], 66, shift=-12)
    c1, c2 = chords_of(bar)
    bsn.note(song.bar(bar), n(CH[c1][1][0]) - 12 + (0 if c1 != "C7" else 0), 58, 1.9)
    bsn.note(song.bar(bar) + 2, n(CH[c2][1][1]) - 12, 54, 1.9)

# ---- bridge: bassoon solo (25-28), clarinet (29-31)
BRIDGE = {
    25: (bsn, [(0, "F3", .5), (.5, "D3", .5), (1, "Bb2", 1), (2, "C3", .5), (2.5, "D3", .5), (3, "F3", 1)]),
    26: (bsn, [(0, "G3", .5), (.5, "F3", .5), (1, "D3", 2)]),
    27: (bsn, [(0, "D3", .5), (.5, "Bb2", .5), (1, "G2", 1), (2, "A2", .5), (2.5, "Bb2", .5), (3, "D3", 1)]),
    28: (bsn, [(0, "E3", .5), (.5, "D3", .5), (1, "Bb2", 2)]),
    29: (clar, [(0, "A4", .5), (.5, "A4", .5), (1, "D5", .5), (1.5, "A4", .5), (2, "F5", 1), (3, "E5", 1)]),
    30: (clar, [(0, "D5", .5), (.5, "E5", .5), (1, "F5", .5), (1.5, "A5", .5), (2, "G5", 2)]),
    31: (clar, [(0, "G5", 1), (1, "E5", 1), (2, "C5", 1), (3, "G4", 1)]),
}
for bar, (tr, notes) in BRIDGE.items():
    phrase(tr, bar, notes, 88)
phrase(bsn, 29, [(0, "D3", 4)], 60); phrase(bsn, 30, [(0, "A2", 4)], 60); phrase(bsn, 31, [(0, "C3", 4)], 62)
phrase(bsn, 32, [(0, "Bb2", 2)], 66)

# ---- rhythm layer: shaker 8ths, woodblock tick-tock, claves in B, triangle at phrase starts
for bar in range(1, 33):
    b0 = song.bar(bar)
    sec = "A" if bar <= 8 else "B" if bar <= 16 else "A2" if bar <= 24 else "bridge"
    for k in range(8):
        kit.note(b0 + k * .5, SHAKER, song.hum(84 if k % 2 == 0 else 62), .1)
    if sec in ("A", "A2"):
        kit.note(b0 + 1, WOOD_HI, song.hum(96), .1); kit.note(b0 + 3, WOOD_LO, song.hum(92), .1)
        if bar % 2 == 0:
            kit.note(b0 + 3.5, WOOD_HI, song.hum(70), .1)
    elif sec == "B":
        for off in (0, 1.5, 3):
            kit.note(b0 + off, CLAVES, song.hum(78), .1)
    else:
        for k in range(4):
            kit.note(b0 + k + .5, WOOD_HI if k % 2 == 0 else WOOD_LO, song.hum(72 + (bar - 25) * 4), .1)
        if bar == 32:
            for k in range(8):
                kit.note(b0 + 2 + k * .25, TAMB, 60 + k * 7, .1)
    if bar in (1, 17, 25, 29):
        kit.note(b0, TRIANGLE_OPEN, 72, 1)

if __name__ == "__main__":
    song.build()
