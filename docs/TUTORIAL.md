# 튜토리얼 (2차)

기획서 6장 기준. 튜토리얼은 세 장이고, 들어가면 한 장만 하고 메뉴로 돌아온다.

| 장 | 길이 | 단계 |
| --- | --- | --- |
| 1 기본 (광산) | 약 2분 | 카메라 확인, 보정, 제자리 달리기, 레인 이동, 점프, 숙이기, 구출, 바나나, 경고 규칙(그림), X자 일시정지, 수레와 갈림길 |
| 2 동물원 연습 | 약 1분 | 싱크 조절(박자에 맞춰 점프 4번), 노트 한 절(점프와 만세), 사자 백미러(그림) |
| 3 감옥 연습 | 약 40초 | 계단 한 번, 탐조등 숨기 한 세트와 자물쇠 게이지, 경보 규칙(그림) |

## 진행과 저장
- 1장을 깨면 2장, 2장을 깨면 3장이 열린다. 깬 장은 언제든 다시 할 수 있다. 맵 잠금과는 묶지 않는다.
- 저장은 PC의 `UserSettings.TutorialMarks`(주인은 회원 계정 id, 게스트는 `guest`). 회원의 1장은 계정의 `tutorial_done`에도 저장한다.
- 1장을 안 깬 사람(게스트, 새 회원)은 "게임 시작"에서 장 선택 화면을 한 번 본다. "건너뛰기"가 있다.
- 1.x에서 넘어온 회원은 짧은 1장(보정, 구출, 바나나와 경고 규칙)을 한 번 권유받는다.
- 싱크 조절 값은 `UserSettings.SyncOffsetMs`에 저장된다.

## 코드 위치
- `Domain/TutorialScript.cs`: 장, 단계, 통과 조건, 한 줄 문구, 시범 클립, 진행 규칙(`TutorialProgress`), 장별 코스(`TutorialCourse`)
- `Domain/TutorialStations.cs`: 구출 팔 읽기(`ArmPose`), 박자(`BeatSync`, `BeatVerse`), 숨기(`HideSet`)
- `Presentation/Flow/TutorialDirector.cs`: 진행, 실패와 다시 하기, 3번 실패하면 관절 비교
- `Presentation/Flow/TutorialPictures.cs`: 구출, 박자, 경고 규칙, 백미러, 탐조등, 경보 그림
- `Presentation/UI/Overlay/TutorialOfferScreen.cs`: 장 카드 3개 화면
- `Bootstrap/GameBootstrap.Tutorial.cs`: 게임과의 연결
- `Editor/MongscapeTutorialClips.cs`: 시범 클립 13개를 `Monkey.controller`에 넣는 도구 (메뉴 Mongscape/Add Tutorial Demo Clips)

## 시범 원숭이
왼쪽 아래에 거울처럼 뒤집어 보여 준다. 아트 2차 시범 11개와 X자, T자를 쓴다. 키보드는 시범 없이 키 이름만 보인다.
같은 단계를 3번 틀리면 시범 창이 커지고 내 관절과 시범 관절을 나란히 비교한다.
