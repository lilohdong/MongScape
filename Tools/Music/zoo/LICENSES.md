# Licences: zoo_chase_v1

The song is our own composition. It was written as code in `make_zoo_chase.py`, so the composition, the MIDI and the beatmap belong to the project.
The audio (WAV/OGG) was rendered from that MIDI with the sound bank below.

| Item | Version | Source | Licence | Use in a commercial game |
| --- | --- | --- | --- | --- |
| **GeneralUser GS** (SoundFont, S. Christian Collins) | 2.0.3 (License v2.0) | https://github.com/mrbumpy409/GeneralUser-GS, http://www.schristiancollins.com | *"You may use GeneralUser GS without restriction for your own music creation, private or commercial."* The licence also states that every contained sample allows full use in music production, including selling recordings made with it. Full text: `tools/GeneralUser-GS-LICENSE.txt` | OK for rendered music (our case). |
| **FluidSynth** (renderer) | 2.6.3 win10-x64 | https://github.com/FluidSynth/fluidsynth/releases/tag/v2.6.3 | LGPL-2.1 | We only use it as a build tool and do not ship it with the game. The audio it renders is not covered by the LGPL. |
| **mido** (Python MIDI) | pip latest | https://github.com/mido/mido | MIT | Build tool only. |
| **NumPy** | 2.x | https://numpy.org | BSD-3-Clause | Build tool only. |
| **FFmpeg** + libvorbis (OGG encoding) | 9.0.2 (gyan.dev full build, installed with winget) | https://ffmpeg.org, https://xiph.org/vorbis | FFmpeg: GPL build. libvorbis: BSD | Build tool only. Encoded output is not covered by these licences. |

## Note on GeneralUser GS
The licence says the author "cannot be 100% sure where all of the samples originated". It adds that none came from commercial packages and that no ownership complaint has been made since 2000.
That caveat is about **embedding the .sf2 file in a software product**. We do not ship the SoundFont. We only ship audio rendered from it, which is "music creation, private or commercial" and allowed without restriction.
No attribution is required. An optional credit line: "Instrument samples: GeneralUser GS by S. Christian Collins".

## Not included in the game build
`tools/` (fluidsynth.exe, GeneralUser-GS.sf2) exists only to regenerate the song and must not be committed to the game repo. Download them again from the links above if needed.
