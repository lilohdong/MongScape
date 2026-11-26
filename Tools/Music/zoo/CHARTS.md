# Zoo chase v1 note charts (easy, normal, hard x 2)

Same song (`zoo_chase_v1.ogg`, BPM 160, 40 bars = 60 s). Only the note layout changes. Plan 3-3: the zoo leaves the distance curve out; the visit count picks the tier (1st visit easy, 2nd normal, 3rd and later hard) and one of the two layouts in that tier is picked at random.

| Chart | Notes | Jump | Arms-up | vs normal | Notes on a music cue |
| --- | --- | --- | --- | --- | --- |
| easy_a | 25 | 15 | 10 | 69% | 25 |
| easy_b | 25 | 14 | 11 | 69% | 25 |
| normal_a | 36 | 22 | 14 | 100% (the passed layout, unchanged) | 36 |
| normal_b | 36 | 21 | 15 | 100% | 24 |
| hard_a | 47 | 25 | 22 | 131% | 36 |
| hard_b | 47 | 24 | 23 | 131% | 36 |

Rules (checked by assert in every chart): no notes in the intro, break or exit, every note on a beat, at least 4 beats between jumps, at least 2 beats after a jump, and at least 2 beats between any two notes.

## Files
- `Unity/Assets/Game/Content/Audio/Music/Zoo/zoo_chase_v1.<chart>.beatmap.json`: same schema as `zoo_chase_v1.beatmap.json`, plus `chart`, `difficulty`, `variant`, `audio`, `rules` and `notes_on_music_cue`.
- `zoo_chase_v1.<chart>_withclicks.ogg` (not committed, made with `--clicks`): the song plus a click on every note of that chart, for checking by ear. Jump = one low click (1 kHz), arms-up = two high clicks (2.5 kHz).
- `make_zoo_charts.py` (this folder): rebuilds the six beatmaps into `Unity/Assets/Game/Content/Audio/Music/Zoo/` in a few seconds. It loads `make_zoo_chase.py` (same folder) for the song constants and rules; the song is never re-rendered. `python Tools/Music/zoo/make_zoo_charts.py`, add `--clicks <folder outside Unity/Assets>` to also write the click-check OGGs (decoded from the committed OGG).

## Music cues
The song has cue sounds baked in on the normal_a notes (kick + timpani for a jump, crash + glockenspiel for an arms-up).
- Easy charts use only normal_a beats, so every easy note has its cue. A few cues play where easy has no note.
- Hard charts are normal_a plus 11 extra notes, so all the original cues still line up. The extra notes have no cue of their own in the music, so the game's note sound or the visual has to carry them.
- normal_b moves 12 notes off the cue beats. It is the "same density, different layout" set. If the off-cue notes feel wrong when you listen, use normal_a only for the normal tier.
