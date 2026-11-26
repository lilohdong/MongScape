# 배경음 라이선스

메인 메뉴, 광산, 감옥, 결과, 동물원 곡은 이 프로젝트가 직접 작곡했습니다. 작곡은 `make_*.py` 코드이고, 곡과 MIDI, 박자표는 프로젝트 소유입니다. 오디오(OGG)는 그 MIDI를 아래 사운드폰트로 렌더한 것입니다.

| 항목 | 버전 | 출처 | 라이선스 | 상업 게임에 쓰기 |
| --- | --- | --- | --- | --- |
| GeneralUser GS (사운드폰트, S. Christian Collins) | 2.0.3 (License v2.0) | https://github.com/mrbumpy409/GeneralUser-GS | "You may use GeneralUser GS without restriction for your own music creation, private or commercial." 들어 있는 모든 샘플도 음악 제작과 그 녹음 판매에 쓸 수 있다고 적혀 있습니다. 원문: 사운드폰트와 함께 받는 `GeneralUser-GS-LICENSE.txt` | 렌더한 음악은 가능 (우리 경우) |
| FluidSynth (렌더러) | 2.6.3 win10-x64 | https://github.com/FluidSynth/fluidsynth/releases/tag/v2.6.3 | LGPL-2.1 | 만들 때만 쓰고 게임에 넣지 않음. 렌더한 오디오에는 LGPL이 적용되지 않음 |
| mido | 최신 | https://github.com/mido/mido | MIT | 만들 때만 |
| NumPy, SciPy | 2.x, 1.x | https://numpy.org, https://scipy.org | BSD-3-Clause | 만들 때만 |
| FFmpeg + libvorbis (OGG 인코딩, 음량 측정) | 9.0.2 | https://ffmpeg.org, https://xiph.org/vorbis | FFmpeg GPL 빌드, libvorbis BSD | 만들 때만. 결과물에는 적용되지 않음 |

GeneralUser GS 라이선스에는 저자가 모든 샘플의 출처를 100% 확신할 수는 없다는 말이 있습니다. 상용 패키지에서 가져온 샘플은 없고 2000년 이후 소유권 문제 제기가 없었다고 덧붙입니다. 이 단서는 .sf2 파일 자체를 소프트웨어에 넣을 때의 이야기이고, 우리는 사운드폰트를 배포하지 않고 렌더한 오디오만 넣습니다.

출처 표기 의무는 없습니다. 넣는다면: "Instrument samples: GeneralUser GS by S. Christian Collins".

`tools/` (fluidsynth.exe, GeneralUser-GS.sf2)는 다시 만들 때만 필요하며 저장소에 넣지 않습니다.
