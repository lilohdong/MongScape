# Zoo chase song: first draft (zoo_chase_v1)

(게임 저장소 사본: 렌더 결과는 `Tools/Music/out/zoo/`에 생기고, 도구는 다른 곡과 같이 `Tools/Music/tools/`에서 찾습니다. 아래 `tools/` 설명은 원래 zoo-v1 폴더 기준입니다. 게임에 들어가는 파일은 `make_zoo_game.py`가 만든 `Unity/Assets/Game/Content/Audio/Music/Zoo/`의 사본입니다.)

BPM 160, 4/4, 40 bars = 60.0 s. The audio files are 61.5 s long and include a 1.5 s reverb fade-out.
E minor. Jungle percussion (conga, bongo, toms, shaker, agogo), a galloping synth bass, a marimba line in the verses and a flute melody in the choruses.

## Files
| File | Contents |
| --- | --- |
| `zoo_chase_v1.ogg` / `.wav` | The song (44.1 kHz stereo, peak at -1 dBFS) |
| `zoo_chase_v1_withclicks.ogg` | The song plus a click at every note. **Jump = one low click (1 kHz). Arms-up = two high clicks (2.5 kHz).** |
| `zoo_chase_v1.mid` | Source MIDI (section names are stored as markers) |
| `zoo_chase_v1.beatmap.json` | Beatmap: bpm, offset_sec, sections, and for each note `{beat, time, type, bar, beat_in_bar}` |
| `make_zoo_chase.py` | Generator script. Running it once rebuilds everything above (about 6 s). |
| `LICENSES.md` | Sources and licences for the sound bank and tools |
| `tools/` | FluidSynth 2.6.3 and GeneralUser GS 2.0.3. Only needed to regenerate; do not commit to the repo. |

How to regenerate: `pip install mido numpy`, make sure ffmpeg is on PATH, then run `python make_zoo_chase.py`.

## Notes per section
| Section | Bars | Time (s) | Jump | Arms-up | Notes |
| --- | --- | --- | --- | --- | --- |
| Intro | 1-2 | 0.0-3.0 | 0 | 0 | Tremolo strings and a tom rumble build tension; the lion appears here |
| Verse 1 | 3-10 | 3.0-15.0 | 6 | 0 | A jump on beat 1 of bars 4-9, every 4 beats. Bar 3 is left empty so the player can settle in after the cutscene. |
| Chorus 1 | 11-18 | 15.0-27.0 | 6 | 5 | Jumps and arms-ups mixed on a 2-beat grid, with some gaps to breathe |
| Break | 19-22 | 27.0-33.0 | 0 | 0 | The iron-gate sprint: 16th-note drums and bass, rising strings |
| Verse 2 | 23-32 | 33.0-48.0 | 5 | 3 | Slightly denser than verse 1 |
| Chorus 2 | 33-38 | 48.0-57.0 | 5 | 6 | The densest part: a note almost every 2 beats |
| Exit | 39-40 | 57.0-60.0 | 0 | 0 | One bar of build-up, then a final E minor hit with cymbals |
| **Total** | | | **22** | **14** | |

Rules (the script checks these with assert): no notes in the intro, break or exit; at least 4 beats (1.5 s) between consecutive jumps; at least 2 beats (0.75 s) between a jump and the next note; every note sits on a beat. Both the shortest jump gap and the shortest jump-to-next-note gap in the map are exactly at these limits.

## Hearing the notes in the music (signal sounds)
- **Jump** = kick + low floor tom + timpani (chord root), all at maximum velocity. The kick and timpani are used **only for jumps**; the ending hit is the one exception. Measured: jump beats peak around -2 dBFS, other beats around -11 to -15 dBFS.
- **Arms-up** = crash + splash + a bright glockenspiel chime (with a soft 16th-note pickup just before the beat). The crash and glockenspiel are used **only for arms-ups**; the ending hit is the one exception. Measured: in the high band, arms-up beats are 12-15 dB louder than other beats.
- Section fills use toms only, so they cannot be mistaken for a signal sound.

## Timing
- `time = offset_sec + beat × 60/160`, with `offset_sec = 0.0`, meaning beat 0 is the first sample of the audio.
- FluidSynth starts each sound 3-5 ms after its exact time, which is negligible compared with the ±0.25 s judgement window.
- The beatmap and the music are generated from the same `NOTES` list, so they cannot drift apart.

## Knobs to change (all at the top of `make_zoo_chase.py`)
| What to change | Where |
| --- | --- |
| Tempo | `BPM`. Beatmap times are recalculated automatically. To keep the song at 60 s, change `BARS` and `SECTIONS` too. |
| Section layout | `SECTIONS` (name, first bar, last bar) |
| Note placement and density | `NOTES` list of `(beat, "jump" / "arms_up")`. Bar n beat k = `(n-1)*4 + (k-1)`. Violating the gap rules stops generation with an assert. |
| Minimum gaps | `MIN_JUMP_GAP`, `MIN_AFTER_JUMP` |
| Instruments | `PROGRAMS` (GM program number per channel: 0 bass, 1 marimba, 2 flute, 3 pad, 4 timpani, 5 glockenspiel, 6 tremolo strings) |
| Instrument volume / reverb | `VOLUMES` (CC7), `REVERB` (CC91) |
| Chords | `VERSE_PROG`, `CHORUS_PROG`, `CH` |
| Signal sound volume | Velocities in the "note signals" block at the end of `compose()` |
| Groove density | In `compose()`: the shaker `step`, the `conga` list, the bongo/agogo lists |
| Reverb tail length | `TAIL_SEC` |

## Known limits of this draft
- The mix is fairly quiet (section RMS about -20 to -27 dBFS) because the big jump hits leave little headroom. If it needs to be louder in game, add a light compressor or adjust it in the engine.
- There are no sound effects such as a lion roar. Those belong to the game's sound effects.
- There is only one note layout. The plan's open question about using 2-3 layouts per song can be handled by adding more `NOTES` sets.
