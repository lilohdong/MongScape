# 배경음 만들기

곡은 모두 파이썬 코드로 작곡한 MIDI를 FluidSynth + GeneralUser GS로 렌더해 만듭니다. 곡마다 악기 묶음(층, stem)을 따로 렌더해서, 게임은 층을 동시에 재생하고 상황에 따라 층 음량만 바꿉니다.

## 파일

| 파일 | 내용 |
| --- | --- |
| `musiclib.py` | 공통 파이프라인: 층별 MIDI, 렌더, 루프 이음매, 음량 측정과 곡 이득, `<곡>.layers.json` 쓰기 |
| `make_menu.py`, `make_mine.py`, `make_prison.py`, `make_result.py` | 곡별 작곡 스크립트 |
| `zoo/make_zoo_chase.py` | 통과된 동물원 곡 zoo-v1 원본 스크립트 (작곡 그대로, 경로만 바꿈). zoo-v1과 비트 단위로 같은 WAV가 나옵니다 |
| `zoo/README.md`, `zoo/LICENSES.md` | zoo-v1 원본 설명과 라이선스 |
| `make_zoo_game.py` | 동물원 곡의 게임용 사본 (음량만 맞춤) |
| `LICENSES.md` | 사운드폰트와 도구 라이선스 |
| `tools/` | FluidSynth와 GeneralUser-GS.sf2. 저장소에 넣지 않습니다 (아래 준비) |
| `out/` | 렌더 중간 파일과 미리듣기. 저장소에 넣지 않습니다 |

결과물은 `Unity/Assets/Game/Content/Audio/Music/<곡>/`에 들어갑니다: 층별 `<곡>_<층>.ogg`와 `<곡>.layers.json` (BPM, 루프 길이, 층과 상태, 상태별 측정값, 페이드).

## 곡과 층

| 곡 | 조성, 템포 | 루프 | 층 | 상태 (켜는 층) |
| --- | --- | --- | --- | --- |
| 메인 메뉴 | F장조, 112 | 32마디 68.57초 | base: 피치카토, 베이스, 마림바 주제, 클라리넷과 바순, 글로켄 / rhythm: 셰이커, 우드블록, 클라베스 (킥 없음) | full = base + rhythm (기본), calm = base |
| 광산 | D단조, 132 | 32마디 58.18초 | base: 슬랩 베이스 스타카토, 밴조, 기타 칙칙, 피들, 하모니카 (브리지만), 금속 타악기, 높은 현 / tension: 타이코, 스네어, 탐, 낮고 짧은 금관 | base (기본, 경찰 멂), chase = base + tension (경찰 가까움) |
| 감옥 | C단조, 96 | 16마디 40초 | base: 베이스 펄스, 낮은 현, 초침, 피아노와 낮은 클라리넷 / steps: 매 박 낮은 북 / tension: 스네어, 탐, 킥, 하이햇 / hide: 드론, 심장박동 팀파니, 작은 초침 | flat = base (평지), stairs = base + steps (계단, 기준), hide = hide만 (탐조등 숨기), chase = base + steps + tension (첫 경보 뒤 간수 추격) |
| 결과 | F장조, 100 | 12마디 28.8초 | base: 피아노 (메뉴 주제 인용), 패드, 베이스, 마림바 메아리, 글로켄 / rhythm: 브러시 킷 셰이커, 스네어, 페달 하이햇 | full (기본), calm = base |
| 동물원 | E단조, 160 | 반복 없음, 61.5초 | 한 파일 (zoo-v1 그대로) | run |

- 감옥 페이드: 숨기로 들어갈 때 0.3초, 숨기에서 나올 때 1.0초, 나머지 0.5초. 광산: chase로 0.5초, base로 1.5초. 메뉴, 결과: 1.0초.
- 광산 tension은 경찰 추격 압력에 따라 0에서 1 사이로 올려도 됩니다.

## 게임에서 재생하는 법

- 한 곡의 층은 모두 `AudioSource.PlayScheduled`로 같은 `dspTime`에 시작하고 `loop = true`. 층 파일은 샘플 수가 완전히 같아 어긋나지 않습니다.
- 상태가 바뀌면 층 음량만 `<곡>.layers.json`의 `fades` 시간 동안 옮깁니다.
- 음악 파일은 Resources 밖에 있어 아직 빌드에 들어가지 않습니다. 연결할 때 Load Type은 Streaming(또는 Compressed In Memory)을 권합니다.

## 루프 이음매

층마다 마지막 마디 뒤 리버브 꼬리(3~4초)까지 렌더한 뒤, 꼬리를 잘라 파일 앞부분에 더합니다. 그래서 파일 길이가 정확히 루프 길이이고 끝에서 처음으로 넘어갈 때 꼬리가 이어집니다. 스크립트는 이음매의 샘플 변화가 파일 안 평소 변화(99.9 백분위)의 3배를 넘으면 멈춥니다.

## 음량 기준

| 곡 | 기준 상태 | 목표 (통합 음량) | 결과 |
| --- | --- | --- | --- |
| 광산, 감옥, 동물원 | 가장 자주 듣는 상태 (광산 base, 감옥 stairs, 동물원 run) | -18 LUFS | 광산 -19.2, 감옥 -18.7, 동물원 -18.0 |
| 메인 메뉴, 결과 | full | -20 LUFS | -20.0, -20.0 |

- 모든 상태 트루 피크 -1 dBTP 이하. 넘으면 그 곡 전체 이득을 내립니다 (광산, 감옥은 추격 상태의 피크 때문에 목표보다 조금 작음).
- 긴장 층을 다 켜도 기준 상태보다 +3 LU 이내. 감옥 숨기 상태는 약 -26 LUFS.
- 층에는 리미터나 컴프레서를 걸지 않고, 곡 하나에 같은 고정 이득만 적용합니다. 그래서 층을 더하면 원래 믹스가 됩니다.
- 측정: ffmpeg `ebur128=peak=true` (ITU-R BS.1770). 게임에 들어갈 OGG를 다시 디코드해서 잽니다.
- 동물원 zoo-v1은 원본 렌더가 -19.7 LUFS, -0.9 dBTP라서 게임용 사본만 +1.9 dB 키우고 점프 신호 타격만 빠른 피크 리미터로 잡았습니다 (-18.0 LUFS, -1.8 dBTP). 박자표는 그대로 맞습니다.
- 효과음이 묻히지 않는지는 `Tools/Audio/check_sfx_mix.py`로 확인합니다 (각 곡의 가장 시끄러운 상태 위에 효과음을 얹어 비교).

## 준비와 다시 만들기

1. `pip install mido numpy scipy`, ffmpeg를 PATH에 둡니다.
2. FluidSynth 2.6.3 (https://github.com/FluidSynth/fluidsynth/releases/tag/v2.6.3, win10-x64)을 `tools/fluidsynth/` 아래에, GeneralUser GS 2.0.3 (https://github.com/mrbumpy409/GeneralUser-GS)의 `GeneralUser-GS.sf2`를 `tools/`에 둡니다. 다른 곳에 두었다면 환경 변수 `MONGSCAPE_MUSIC_TOOLS`로 그 폴더를 알려 줍니다.
3. `python make_menu.py` (광산, 감옥, 결과도 같음). 동물원은 `python zoo/make_zoo_chase.py` 뒤 `python make_zoo_game.py`.
4. 곡마다 약 10초. 측정값이 출력되고 `<곡>.layers.json`에 기록됩니다. 미리듣기(상태별 2회 반복, 감옥 상태 시연)는 `out/<곡>/preview/`에 생깁니다.
