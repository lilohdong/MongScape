# 사운드

효과음은 Kenney CC0 오디오 팩에서 골라 넣었고, 파일이 없으면 예전처럼 코드로 합성한 소리로 대신합니다. 배경음은 아직 코드 합성 음악이며, 새 곡(메인 메뉴, 광산, 감옥, 동물원, 결과)은 검수 뒤 따로 들어옵니다.

## 효과음 (지금 게임에서 쓰는 것)

위치: `Unity/Assets/Game/Content/Audio/Resources/MuhanokAudio/Sfx/`. `GameAudio.cs`가 `Resources.Load`로 읽습니다. 여러 개인 소리(착지, 발소리, 몽둥이)는 재생할 때마다 무작위로 하나를 고릅니다.

| 파일 | 쓰는 곳 | Kenney 팩 / 원본 | 게임 이득 |
| --- | --- | --- | --- |
| `jump` | 점프 | RPG Audio / cloth1 | 0.45 |
| `land_0~2` | 점프 뒤 착지 | Impact Sounds / impactSoft_medium_000~002 | 0.40 |
| `hit` | 충돌해서 잡힘 | Impact Sounds / impactPunch_heavy_000 | 1.00 |
| `bonk_0~2` | 잡힌 뒤 간수 몽둥이 | Impact Sounds / impactWood_heavy_000~002 | 0.75 |
| `cue` | 코치 안내 문구가 뜰 때 | Interface Sounds / bong_001 | 0.50 |
| `count_beep` | 카운트다운 삑 | Digital Audio / pepSound1 | 0.60 |
| `count_go` | 카운트다운 시작 | Digital Audio / highUp | 0.70 |
| `footstep_0~4` | 감옥 계단 무릎 걸음 | Impact Sounds / footstep_concrete_000~004 | 0.35 |
| `slip` | 바나나, 바닥에 미끄러짐 (계단 무릎 박자 놓침은 소리 없음) | Sci-Fi Sounds / slime_000 | 0.80 |
| `item` | 아이템 획득 | Digital Audio / powerUp7 | 0.70 |
| `shield` | 보호막이 막아 줌 | Sci-Fi Sounds / forceField_000 | 0.80 |

- 게임 이득은 효과음 음량 설정에 곱하는 값입니다(`GameAudio.cs` 위쪽 상수). 파일은 모두 피크 -1 dBFS로 맞췄습니다.
- 사이렌: Kenney에 없어서 지금처럼 코드 합성 소리를 씁니다.
- 코인 소리와 순위표 유리 깨짐은 바꾸지 않았습니다 (코인은 2차에서 없어짐).

## 2차용 효과음 (골라만 둠, 코드 연결 전)

위치: `Unity/Assets/Game/Content/Audio/SfxV2/` (Resources 밖이라 빌드에 들어가지 않습니다).

| 파일 | 쓸 곳 | Kenney 팩 / 원본 | 제안 이득 |
| --- | --- | --- | --- |
| `lock_click` | 감옥 자물쇠 따는 중 딸깍 | RPG Audio / metalClick | 0.70 |
| `lock_open` | 자물쇠 열림 | RPG Audio / metalLatch | 0.90 |
| `monkey_rescue` | 원숭이 구출 (짧게 올라가는 피치카토) | Music Jingles / jingles_PIZZI10 | 0.90 |
| `fork_blocked` | 막힌 갈림길로 기울였을 때 덜컹 | Impact Sounds / impactMetal_heavy_001 | 0.90 |
| `gate_down` | 철문 내려옴 | RPG Audio / doorClose_1 | 0.90 |
| `warning` | 경고 1회 (짧게 내려가는 피치카토) | Music Jingles / jingles_PIZZI05 | 1.00 |
| `caught` | 잡힘 (내려가는 피치카토) | Music Jingles / jingles_PIZZI01 | 1.00 |
| `new_record` | 신기록 (올라가는 피치카토) | Music Jingles / jingles_PIZZI02 | 1.00 |
| (없음) | 사자 포효 | Kenney에 없음 | |
| (없음) | 탐조등 경보 | Kenney에 없음 | |

## 출처와 다시 만들기

- 팩 주소, 라이선스(CC0), 받은 ZIP의 SHA256: [외부 리소스 안내](../THIRD_PARTY_NOTICES.md). 파일별 원본과 원본 SHA256: 각 폴더의 `SOURCES.json`.
- 다시 만들기: kenney.nl에서 팩 6개(impact-sounds, interface-sounds, digital-audio, rpg-audio, sci-fi-sounds, music-jingles)를 받아 팩 이름 폴더로 풀고 `python Tools/Audio/make_sfx.py <풀어 둔 폴더>`. 파이썬 NumPy와 ffmpeg가 필요합니다. 모노로 바꾸고, 앞뒤 무음을 자르고, 피크 -1 dBFS로 맞춰 OGG로 씁니다.
