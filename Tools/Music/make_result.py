"""Mongscape result loop: warm, proud wrap-up after a run.

F major, 100 BPM, 4/4, 12 bars = 28.8 s loop.
Bars 1-4 quote the main menu motif (same notes, slower and legato, on piano), bars 5-8 answer it,
bars 9-12 are a gentle cadence back to the top.
Layers: base (piano, soft pad, bass, marimba echo, glockenspiel) + rhythm (shaker, brushes, pedal hi-hat; no kick).

Usage: python make_result.py
"""
from musiclib import Song, n, SHAKER, SNARE, PEDAL, TRIANGLE_OPEN
from make_menu import THEME

song = Song("result", "Result", bpm=100, bars=12, tail_sec=4.0, seed=100)
song.sections = [("motif", 1, 4), ("answer", 5, 8), ("cadence", 9, 12)]
song.layer("base", "Piano melody and broken chords, soft pad, bass, marimba echo, glockenspiel")
song.layer("rhythm", "Shaker, brush snare and pedal hi-hat (no kick)")
song.state("full", "Result screen and ranking (default)", base=1, rhythm=1)
song.state("calm", "Optional quieter variant: rhythm layer muted", base=1, rhythm=0)
song.reference = "full"
song.target_lufs = -20.0
song.fades = {"default": 1.0}

piano = song.track("piano", "base", 0, volume=116, reverb=60, pan=60)
pianolh = song.track("piano left hand", "base", 0, volume=104, reverb=60, pan=56)
pad = song.track("warm pad", "base", 89, volume=70, reverb=70, pan=64)
bass = song.track("bass", "base", 32, volume=80, reverb=30, pan=64)
marimba = song.track("marimba echo", "base", 12, volume=112, reverb=55, pan=84)
glock = song.track("glockenspiel", "base", 9, volume=64, reverb=70, pan=90)
kit = song.track("brushes", "rhythm", 40, volume=110, reverb=40, drum=True)   # GS brush kit

CH = {"F": ("F2", ["F3", "A3", "C4"]), "C7": ("C2", ["E3", "G3", "Bb3"]), "C": ("C2", ["E3", "G3", "C4"]),
      "Bb": ("Bb1", ["F3", "Bb3", "D4"]), "Gm7": ("G1", ["F3", "Bb3", "D4"]), "Dm": ("D2", ["F3", "A3", "D4"])}
CHORDS = ["F", "C7", "F", "C", "Bb", "F", "Gm7", "C7", "F", "Bb", "C7", "F"]
MELODY = {
    5: [(0, "D5", 1), (1, "F5", 1), (2, "D5", .5), (2.5, "C5", .5), (3, "Bb4", 1)],
    6: [(0, "A4", 1.5), (1.5, "C5", .5), (2, "F5", 2)],
    7: [(0, "Bb4", .5), (.5, "A4", .5), (1, "G4", 1), (2, "D5", 1), (3, "C5", 1)],
    8: [(0, "Bb4", 1), (1, "G4", 1), (2, "E5", 2)],
    9: [(0, "F5", 1.5), (1.5, "E5", .5), (2, "C5", 2)],
    10: [(0, "D5", 1), (1, "Bb4", 1), (2, "F4", 2)],
    11: [(0, "G4", 1), (1, "A4", .5), (1.5, "Bb4", .5), (2, "E4", 2)],
    12: [(0, "F4", 3)],
}

for bar in range(1, 13):
    b0 = song.bar(bar)
    chord = CHORDS[bar - 1]
    root = n(CH[chord][0]); tones = [n(x) for x in CH[chord][1]]
    notes = THEME[bar - 1] if bar <= 4 else MELODY[bar]
    for k, (off, name, dur) in enumerate(notes):   # legato: hold each note until the next one
        nxt = notes[k + 1][0] if k + 1 < len(notes) else 4
        piano.note(b0 + off, name, song.hum(84 if k == 0 else 74, 4), max(dur, nxt - off) * .97)
    for k, i in enumerate([0, 1, 2, 1, 0, 1, 2, 1]):   # left hand broken chord
        pianolh.note(b0 + k * .5, tones[i] - (12 if i == 0 else 0), song.hum(52 if k % 4 == 0 else 42, 3), .9)
    pad.chord(b0, [t + 12 for t in tones], 58, 3.95)
    bass.note(b0, root, song.hum(78), 1.8); bass.note(b0 + 2, root + 7, song.hum(70), 1.8)
    if bar in (2, 4, 8):   # marimba echoes the end of the phrase
        for k, name in enumerate(["C6", "A5", "F5"] if bar != 8 else ["E6", "C6", "G5"]):
            marimba.note(b0 + 2.5 + k * .5, name, 76 - k * 8, .4)
    if bar in (1, 9):
        glock.note(b0, "A5", 56, 2); glock.note(b0, "F6", 50, 2)
    if bar == 12:
        glock.note(b0 + 2, "C6", 52, 2)

    # rhythm: shaker 8ths, brush backbeat, pedal hat on the offbeats
    for k in range(8):
        kit.note(b0 + k * .5, SHAKER, song.hum(88 if k % 2 == 0 else 66, 3), .1)
    for off in (1, 3):
        kit.note(b0 + off, SNARE, song.hum(92), .2)
    for off in (.5, 1.5, 2.5, 3.5):
        kit.note(b0 + off, PEDAL, song.hum(70), .1)
    if bar in (1, 9):
        kit.note(b0, TRIANGLE_OPEN, 54, 1)

if __name__ == "__main__":
    song.build()
