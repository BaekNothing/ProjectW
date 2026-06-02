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

### 2026-06-03 – Project Direction Realignment to Office 3 Card-Based Management Sim

- 범위:
  - 현재 작업 제목을 **외행성재척지원실 3과**로 고정
  - 기존 관찰형 자동 서사/Routine MVP 문서 잔재를 현재 구현 방향과 분리
  - CaseReview 기반 업무 배치, 검토 비용, 보고/피드백 루프를 최신 Ingame 기준으로 승격
- 반영된 규칙:
  - 게임은 PM 관리 경험 기반 블랙코미디 업무 배치 시뮬레이션으로 정의한다.
  - 플레이어는 플랜 수립, 업무 분배, 결과 피드백을 수행한다.
  - 개체는 행동 덱을 가지며, 하루 시작 시 카드 1장을 랜덤하게 제시한다.
  - 친밀도는 플레이어가 볼 수 있는 정보 스코프를 정의한다.
  - 검토, 면담, 서류 확인, 보고서 검토는 모두 비용을 가진다.
  - AI 제안을 모두 컨펌하면 AI 대체 압력이 상승한다.
  - 사장 유형은 난이도와 평가 기준을 바꾼다.
  - 클론 베이의 폐기/재생성은 비용 없는 리셋이 아니라 관리 실패 비용으로 취급한다.
- 추적 문서:
  - `Assets/Specification/Kickoff.md`
  - `Assets/Specification/Ingame/SSOT – Ingame.md`
  - `Assets/Specification/SSOT – Metadata.md`
  - `Assets/Specification/SSOT – Outgame.md`
  - `Assets/Specification/SSOT – Workflow Confluence × Unity × GitHub.md`
- 구현 해석:
  - 현재 `CaseReview` 구현은 최신 Ingame SSOT의 업무 배치/보고/검토 MVP를 부분 구현한 상태로 해석한다.
  - 과거 Routine/Observation MVP 기록은 히스토리로 보존하되, 현재 SSOT와 충돌하면 현재 Ingame SSOT를 우선한다.

### 2026-06-03 – Character SSOT Creation

- 범위:
  - 캐릭터 시스템의 단일 진실원 문서 생성
  - 클론 기반 인력 개체의 감정적 거리감과 관리 자산 성격 정의
  - 행동 덱, 정보 스코프, 성장, 폐기/재생성 원칙 구체화
- 반영된 규칙:
  - 캐릭터는 정을 붙일 수 있지만 대체불가능한 유일 존재는 아니다.
  - 클론 폐기는 가능하되 비용 없는 리셋이 아니다.
  - 캐릭터 성장은 순수한 레벨업이 아니라 카드, 특성, 위험 습관의 축적이다.
  - 친밀도는 정보 스코프를 넓히지만 항상 정답 루트가 아니다.
- 추적 문서:
  - `Assets/Specification/Ingame/SSOT – Characters.md`
  - `Assets/Specification/Ingame/SSOT – Ingame.md`

### 2026-06-03 – Character Possessions and Memory SSOT Split

- 범위:
  - 캐릭터가 가질 수 있는 카드/퍽/덱/특성 샘플 규칙 분리
  - 캐릭터 기억과 관계 규칙 분리
  - 캐릭터 SSOT를 상위 개요 문서로 유지하고 세부 규칙 문서를 참조하도록 정리
- 반영된 규칙:
  - 행동 카드는 단순 버프가 아니라 장점과 관리 비용을 함께 가진다.
  - 퍽은 지속 특성이며 결과, 리스크, 비용, 기억에 영향을 줄 수 있다.
  - 캐릭터는 다른 캐릭터와 플레이어 관리자의 행동을 기억할 수 있다.
  - 관계는 단순 호감도가 아니라 Trust, Affinity, Debt, Resentment, Reliability 등으로 확장 가능해야 한다.
  - 클론 재생성은 기억을 완전히 보존하지도, 완전히 지우지도 않는다.
- 추적 문서:
  - `Assets/Specification/Ingame/SSOT – Character Possessions.md`
  - `Assets/Specification/Ingame/SSOT – Character Memory and Relationships.md`
  - `Assets/Specification/Ingame/SSOT – Characters.md`

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

### 2026-03-03 – Item/Job/Affinity 통합 규칙 반영 및 문서 정합성 보강

- 범위:
  - Ingame Core에 Item(태그/소유권), Unified Job, Affinity 이벤트 시스템 반영
  - Routine MVP에서 Zone + Item Requirement 동시 게이트 적용
  - SSOT 문서 정합성 점검 및 누락 규칙 보강
- 반영된 규칙:
  - `Work/Eat/Sleep`는 모두 Atomic Job으로 취급
  - 사무실 아이템 풀은 기본 12개 생성, 필수 태그 세트 보장
  - Need 해소/미션 진행은 Zone 조건 외에 Item Requirement 충족 필요
  - 개인 물품 오남용 발견/충돌/협업 결과를 독립 Affinity 이벤트로 기록
- 추적 문서:
  - `Assets/Specification/Ingame/CoreLoop/04 – Autonomy Decision.md`
  - `Assets/Specification/Ingame/CoreLoop/11 – Test Matrix (MVP).md`
  - `Assets/Specification/Ingame/CoreLoop/12 – Spatial Interaction and Entity Scaling.md`


### 2026-03-04 – MVP 판정 기준(Go/No-Go) 확정

- 결정 사항:
  - MVP 판정은 **재미 가설 충족 + KPI 하한 충족**의 이중 게이트로 운영한다.
  - 1차 판정은 20세션, 최종 판정은 50세션 누적으로 수행한다.
- Go 기준:
  - `U01(지연 개입 긴장감)` 65% 이상
  - `U02(관계 붕괴 관찰 흥미)` 60% 이상
  - `K02(사이클 완주율)` 55% 이상
  - `K03(전멸 비율)` 45% 이하
  - `K04(실패 후 재도전율)` 70% 이상
- No-Go 기준:
  - 상기 핵심 지표 중 2개 이상 미달하거나,
  - UX 지표(U01/U02) 중 하나라도 50% 미만일 경우
- 후속 조치 원칙:
  - No-Go 시 개입 피드백 가시성/실패 보상 체감 개선을 우선 수정하고 2주 내 재측정한다.
- 추적 문서:
  - `Assets/Specification/Kickoff.md`
  - `Assets/Specification/Ingame/SSOT – Ingame.md`
  - `Assets/Specification/Ingame/CoreLoop/11 – Test Matrix (MVP).md`
