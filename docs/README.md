# 문서 목록

게임 소개와 실행은 저장소 맨 위 [README](../README.md)를 먼저 본다. 이 표는 지금 기준 문서만 모은다.

| 목적 | 문서 |
| --- | --- |
| 게임을 바로 실행 | [처음 읽기](../START_HERE.md) |
| 사용 기술, 설치, 빌드 환경 | [기술 스택](TECH_STACK.md) |
| 게임, 몸동작, 얼굴 서버의 연결 | [아키텍처](ARCHITECTURE.md) |
| 로그인, 프로필, 상점, 랭킹 화면 | [게임 UI](GAME_FLOW_UI.md) |
| 전신, 상체, 키보드 조작과 보정 | [몸동작 인식](BODY_TRACKING.md) |
| 동작 녹화와 인식 점수표 | [관절 녹화](POSE_RECORDINGS.md) |
| 여러 PC용 Docker, HTTPS 서버 | [서버 배포 안내](../Services/Identity/README_DOCKER.md) |
| 설정 화면 | [설정 UI](SETTINGS_UI.md) |
| 화질과 성능 측정 | [성능 안내](PERFORMANCE.md) |
| 아트 기준 | [아트 방향](ART_DIRECTION.md), [아트 스타일 가이드](ART_STYLE_GUIDE.md), [에셋 목록](ASSET_CATALOG.md) |
| Blender 연결 | [Blender MCP](BLENDER_MCP.md) |
| 효과음과 배경음 | [사운드](AUDIO.md) |
| 사용 리소스와 공개 조건 | [외부 리소스 안내](../THIRD_PARTY_NOTICES.md), [무료 에셋 출처](FREE_ASSETS_IMPORTED.json) |
| 브랜치와 커밋 규칙 | [Git 작업 흐름](GIT_WORKFLOW.md), [작업 규칙](../AGENTS.md) |

## 옛 문서

1차 개발의 계획, 진행 기록, 보고서, V13 화면별 기획서(docx)는 [archive](archive/)에 있다. 지금 명세로 쓰지 않는다.

## 대용량 파일

`.docx`, `.blend`, `.onnx`는 새로 추가하는 파일부터 Git LFS로 올린다 (`git lfs install` 필요). 이미 들어 있는 파일은 기록을 바꾸지 않으려고 일반 Git에 그대로 둔다 (`.gitattributes` 참고).
