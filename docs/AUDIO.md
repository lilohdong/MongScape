# 사운드

효과음은 Kenney CC0 오디오 팩에서 골라 넣었고, 파일이 없으면 예전처럼 코드로 합성한 소리로 대신합니다. 배경음 5곡(메인 메뉴, 광산, 감옥, 동물원, 결과)은 직접 작곡해 `Unity/Assets/Game/Content/Audio/Music/`에 넣었습니다. 게임 연결 전이라 지금 게임은 아직 코드 합성 음악을 냅니다.

## 효과음 (지금 게임에서 쓰는 것)

위치: `Unity/Assets/Game/Content/Audio/Resources/MuhanokAudio/Sfx/`. `GameAudio.cs`가 `Resources.Load`로 읽습니다. 여러 개인 소리(착지, 발소리, 몽둥이)는 재생할 때마다 무작위로 하나를 고르고, 착지와 발소리는 음 높이를 ±5% 안에서 흔들어 반복감을 줄입니다.

| 파일 | 쓰는 곳 | Kenney 팩 / 원본 | 게임 이득 |
| --- | --- | --- | --- |
| `jump` | 점프 | RPG Audio / cloth1 | 0.80 |
| `land_0~2` | 점프 뒤 착지 | Impact Sounds / impactSoft_medium_000~002 | 0.60 |
| `hit` | 충돌해서 잡힘. 음악을 0.4초 동안 5 dB 낮췄다 0.5초에 걸쳐 되돌림 | Impact Sounds / impactPunch_heavy_000 + impactPlate_light_001 겹침, 포화 | 1.00 |
| `bonk_0~2` | 잡힌 뒤 연출에서 간수 몽둥이 (장식, 이때 음악은 이미 작아짐) | Impact Sounds / impactWood_heavy_000~002 | 0.75 |
| `cue` | 코치 안내 문구가 뜰 때 | Interface Sounds / question_004 | 0.75 |
| `count_beep` | 카운트다운 삑 | Digital Audio / pepSound1 | 0.90 |
| `count_go` | 카운트다운 시작 | Digital Audio / highUp | 0.75 |
| `footstep_0~4` | 감옥 계단 무릎 걸음 | Impact Sounds / footstep_concrete_000~004 | 0.60 |
| `slip` | 바나나, 바닥에 미끄러짐 (계단 무릎 박자 놓침은 소리 없음) | Digital Audio / phaserDown1 | 0.75 |
| `item` | 아이템 획득 | Digital Audio / powerUp7 | 0.80 |
| `shield` | 보호막이 막아 줌 | Sci-Fi Sounds / forceField_000 | 0.60 |
| `lion_roar` | 동물원 사자 포효 경고 (`ChaserCue`) | Kenney 아님: OpenGameArt "80 CC0 creature SFX #2" (rubberduck, CC0) / roar_04, 3반음 올림, 약한 포화, 0.81초 | 0.70 (제안) |
| `alarm_bell` | 감옥 탐조등 경보 (`ChaserCue`) | Kenney에 없어 `make_sfx.py`가 합성: 1.4 / 1.9 kHz 두 음을 4번 번갈아, 약 1초 | 0.45 (제안) |

- 게임 이득은 효과음 음량 설정에 곱하는 값입니다(`GameAudio.cs` 상수). 파일은 모두 트루 피크 -1 dBTP 이하로 맞췄습니다(4배 오버샘플링, OGG 디코드 뒤에도 확인).
- 사이렌: Kenney에 없어서 지금처럼 코드 합성 소리를 씁니다.
- 코인 소리와 순위표 유리 깨짐은 바꾸지 않았습니다 (코인은 2차에서 없어짐).

## 2차용 효과음 (골라만 둠, 코드 연결 전)

위치: `Unity/Assets/Game/Content/Audio/SfxV2/` (Resources 밖이라 빌드에 들어가지 않습니다).

| 파일 | 쓸 곳 | 출처 | 제안 이득 |
| --- | --- | --- | --- |
| `lock_click` | 감옥 자물쇠 따는 중 딸깍 | RPG Audio / metalClick | 0.70 |
| `lock_open` | 자물쇠 열림 | RPG Audio / metalLatch | 1.00 |
| `monkey_rescue` | 원숭이 구출 (짧게 올라가는 피치카토) | Music Jingles / jingles_PIZZI10 | 0.70 |
| `fork_blocked` | 막힌 갈림길로 기울였을 때 덜컹 | Impact Sounds / impactMetal_medium_003 + impactPlate_light_003 겹침, 0.3초, 포화 | 1.00 |
| `gate_down` | 철문 내려옴 | RPG Audio / doorClose_1, 포화 | 0.90 |
| `warning` | 경고 1회 (짧게 내려가는 피치카토) | Music Jingles / jingles_PIZZI05 | 1.00 |
| `caught` | 잡힘 (내려가는 피치카토) | Music Jingles / jingles_PIZZI01 | 1.00 |
| `new_record` | 신기록 (올라가는 피치카토) | Music Jingles / jingles_PIZZI02 | 1.00 |

연결할 때 지킬 것 (사운드 검수 1회차):
- `warning`, `caught`, `fork_blocked`, `alarm_bell`, `lion_roar`가 날 때도 `GameAudio.Duck()`으로 음악을 잠깐 낮춥니다.
- 바나나 밟기는 `slip`과 `warning`이 같이 나므로, `warning`을 약 0.15초 늦게 냅니다.
- `fork_blocked`는 막힌 쪽으로 계속 기울이면 반복되므로 같은 소리 재생 간격을 최소 0.4초로 막습니다.

## 크기 기준

`Tools/Audio/check_sfx_mix.py`가 각 곡의 가장 시끄러운 상태(음악 0.35, 효과음 0.7 x 이득) 위에서 효과음 최대 순간 음량과 음악 순간 음량(95 백분위)의 차이를 잽니다. 결과와 곡 위에 얹은 시험 파일은 실행할 때 지정한 폴더에 생깁니다.

| 묶음 | 목표 | 해당 |
| --- | --- | --- |
| 반드시 들림 | +10 LU 이상 | caught, warning, alarm_bell, lion_roar |
| 분명히 들림 | +6~9 LU | hit, fork_blocked, gate_down, monkey_rescue, new_record, item, shield, count_go, count_beep, cue, slip, 사이렌 |
| 음악 아래 은은하게 | 0 아래 | jump, land, footstep, lock_click |

`lock_open`, `lock_click`은 감옥 숨기 상태(드론만 남음) 기준으로 봅니다.

## 배경음

| 곡 | 폴더 | 층 (파일) | 상태 | 루프 | 음량 (기준 상태) |
| --- | --- | --- | --- | --- | --- |
| 메인 메뉴 | `Music/Menu` | base, rhythm | full, calm | 68.57초 | full -20.0 LUFS |
| 광산 | `Music/Mine` | base, tension | base, chase | 58.18초 | base -18.0 LUFS, chase +1.9 LU |
| 감옥 | `Music/Prison` | base, steps, tension, hide | flat, stairs, hide, chase | 40.0초 | stairs -18.7 LUFS, hide -25.9 |
| 결과 | `Music/Result` | base, rhythm | full, calm | 28.8초 | full -20.0 LUFS |
| 동물원 | `Music/Zoo` | 노트 신호음을 뺀 곡 `zoo_chase_v1.ogg` + 신호음 `zoo_sig_jump.ogg`, `zoo_sig_armsup.ogg` | run | 반복 없음 61.5초 | 곡만 -20.4 LUFS, 신호음 포함 약 -18.6 |

- 곡마다 `<곡>.layers.json`에 층의 뜻, 상태별 층 음량, 상태별 측정값(LUFS, 트루 피크), 페이드 시간이 있습니다. 모든 상태 트루 피크 -1 dBTP 이하.
- 재생: 한 곡의 층을 모두 `AudioSource.PlayScheduled`로 같은 시각에 시작하고 반복, 상태가 바뀌면 층 음량만 페이드. Load Type은 Streaming으로 맞춰 두었습니다.
- 동물원 신호음: 배치가 6벌이라 곡에서 빼고 따로 둡니다. 박자표 노트 시각보다 `zoo_signals.json`의 `lead_seconds`만큼 먼저(점프 0초, 만세 0.094초, 글로켄 앞꾸밈음 때문) 음악 음량으로 재생하면 통과된 zoo-v1의 균형과 거의 같습니다. 둘을 리미터 없이 더해도 -1 dBTP를 넘지 않게 신호음은 2 dB 낮춰 두었습니다.
- 감옥 페이드: 숨기로 0.3초, 숨기에서 나올 때 1.0초, 나머지 0.5초. 광산: chase로 0.5초, base로 1.5초.
- 작곡, 렌더, 음량 기준, 다시 만들기: [Tools/Music/README.md](../Tools/Music/README.md). 라이선스: [Tools/Music/LICENSES.md](../Tools/Music/LICENSES.md).
- 검수: 사운드 담당 측정 검수 통과(광산 2회차, 나머지 1회차), 오너 청취 통과.

## 출처와 다시 만들기

- 팩 주소, 라이선스(CC0), 받은 ZIP의 SHA256: [외부 리소스 안내](../THIRD_PARTY_NOTICES.md). 파일별 원본, 겹친 파일, 처리, 원본 SHA256: 각 폴더의 `SOURCES.json`.
- 다시 만들기: kenney.nl에서 팩 6개(impact-sounds, interface-sounds, digital-audio, rpg-audio, sci-fi-sounds, music-jingles)를 받아 팩 이름 폴더로 풀고 `python Tools/Audio/make_sfx.py <풀어 둔 폴더>`. 파이썬 NumPy와 ffmpeg가 필요합니다. 모노로 바꾸고, 앞뒤 무음을 자르고, 일부는 겹치고 포화시킨 뒤 트루 피크 -1 dBTP 이하로 맞춰 OGG로 씁니다.
