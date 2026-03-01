# PM Log – Project Management Records

본 문서는 Project_W의 프로젝트 관리 활동 기록(PM Log) 을 위한 문서다.

이 문서는 게임 설계나 시스템 규칙을 정의하지 않으며,
개발 결과의 정당화나 서사적 해석을 목적으로 하지 않는다.
주된 목적은 프로젝트 관리 활동의 추적, 회고, 증빙이다.

특히 본 로그는 PMP 자격 요건 충족을 위한
프로젝트 관리 시간 및 의사결정 기록을 명확히 분리·보존하는 것을 전제로 한다.

------

## Scope

다음 항목들은 본 문서 또는 하위 로그에서 다룬다.

- 일정 계획 수립 및 변경
- 리소스 배분 및 역할 조정
- 외주 범위 정의 및 의사결정
- 리스크 식별, 대응, 결과
- 의사결정 기록 및 근거

------

## Non-Goals

- 시스템 규칙 정의(SSOT 영역)
- 특정 사례의 결과 정당화
- 서사적 해석의 일반화

------

## Evidence Principle

- 사실 기록: GitHub Commit/PR, Unity 테스트 로그, 빌드 아티팩트
- 해석 및 관리 판단: PM Log

관리 판단은 반드시 추적 가능한 근거 링크(커밋, PR, 빌드, 문서 경로)와 함께 남긴다.

------

## Change Records

### 2026-03-01 – Ingame Routine MVP Rule Tightening + Editor Refresh Control

- 범위:
  - Ingame Routine 행동 규칙 정교화
  - Spatial/Need 기반 Zone 계약 고정
  - Unity Editor 리프레시 운영 정책 통제
- 반영된 규칙:
  - Zone 탐색은 이름 기반이 아닌 `zone_id + tags + boundary` 기반으로 고정
  - 욕구 해소는 해당 need 태그 Zone boundary 내부에서만 허용
  - `Move -> Action` 순서 강제 (미도달 시 `current_action=Move`)
  - 액션 수행 위치는 Zone action slot을 사용해 비중첩 우선
  - 식사/수면은 시간 조건 + 공복/스트레스 조건 동시 만족 시에만 선택
  - Auto Refresh 기본 비활성, 명시 트리거에서만 리프레시
- 추적 문서:
  - `Assets/Specification/Ingame/CoreLoop/04 – Autonomy Decision.md`
  - `Assets/Specification/Ingame/CoreLoop/11 – Test Matrix (MVP).md`
  - `Assets/Specification/Ingame/CoreLoop/12 – Spatial Interaction and Entity Scaling.md`
  - `Assets/Specification/SSOT – Workflow Confluence × Unity × GitHub.md`
