# SSOT – Ingame

본 문서는 Project_W Ingame 영역의 상위 SSOT 인덱스다.

코어 루프 구현 판단은 본 문서와 `CoreLoop/01..14` 세부 문서를 함께 참조해야 한다.
이 문서가 우선순위와 범위를 고정하며, 세부 규칙은 하위 문서가 계약 형태로 정의한다.

------

## Authority

- 본 문서는 Ingame 영역의 상위 진입점이다.
- Core Loop 세부 규칙은 `Assets/Specification/Ingame/CoreLoop` 하위 문서에 위임된다.
- 하위 문서 간 충돌 시 우선순위는 다음과 같다.
  1. `02 – State Machine`
  2. `05 – Session End and Persistence`
  3. `03 – Intervention Boundary`
  4. 기타 CoreLoop 문서

------

## Scope

- 캐릭터 행동 모델
- 자율 루틴 / 정책 반영 방식
- 원자적 Job 모델(`Work/Eat/Sleep`)
- 월드 Item(태그/소유권/공용·개인 정책)
- 호감도(Affinity) 이벤트 누적 규칙
- 위기 상황 및 저력 발현 조건
- 누적 변화(퇴적)의 처리 방식
- 코어 루프 상태 전이 및 종료 조건
- CSV 기반 외부 데이터 주입 규칙

------

## Non-Goals

- 밸런스 수치의 미세 조정
- 특정 사례의 결과 정당화
- 서사적 해석의 일반화
- Combat/Resource/Event 고도화

------

## Core Loop Spec Map

1. `Assets/Specification/Ingame/CoreLoop/01 – Tick and Timebase.md`
2. `Assets/Specification/Ingame/CoreLoop/02 – State Machine.md`
3. `Assets/Specification/Ingame/CoreLoop/03 – Intervention Boundary.md`
4. `Assets/Specification/Ingame/CoreLoop/04 – Autonomy Decision.md`
5. `Assets/Specification/Ingame/CoreLoop/05 – Session End and Persistence.md`
6. `Assets/Specification/Ingame/CoreLoop/06 – Prototype Gate.md`
7. `Assets/Specification/Ingame/CoreLoop/07 – Data Injection Contract (CSV First).md`
8. `Assets/Specification/Ingame/CoreLoop/08 – CSV Schemas.md`
9. `Assets/Specification/Ingame/CoreLoop/09 – Error Handling and Recovery.md`
10. `Assets/Specification/Ingame/CoreLoop/10 – Observability and Replay.md`
11. `Assets/Specification/Ingame/CoreLoop/11 – Test Matrix (MVP).md`
12. `Assets/Specification/Ingame/CoreLoop/12 – Spatial Interaction and Entity Scaling.md`
13. `Assets/Specification/Ingame/CoreLoop/13 – Character Neuron Visualization Feasibility.md`
14. `Assets/Specification/Ingame/CoreLoop/14 – Character Rig and Part Swap Pipeline.md`

------

## Ingame Decision Order

Ingame 구현 판단은 아래 순서를 따른다.

1. 본 문서(`SSOT – Ingame`)
2. CoreLoop 세부 문서(`01..14`)
3. `SSOT – Workflow Local Spec × Unity × GitHub`
4. Unity 구현 코드

------

## Explicit Defaults

- Data Source: CSV First
- CSV Encoding: UTF-8
- CSV Delimiter: `,`
- CSV Header: Required
- Tick Cadence: `1 Tick / 2 seconds`
- Seed Rule: `seed = hash(session_id + ":" + tick_index)`

------

## Definition of Done (MVP Core Loop)

- `CoreLoop` 문서에서 입력 스키마, 상태 전이, 개입 큐, 영속화, 테스트가 모두 정의된다.
- 테스트 매트릭스의 필수 테스트가 전부 통과되어야 완료로 간주한다.
- 문서만으로 구현자가 로딩/평가/저장 흐름을 작성할 수 있어야 한다.

------

## MVP 재미 검증 계약

### MVP 재미 가설

1. **지연 개입의 긴장감**: 실시간 직접 조작이 아닌 지연 개입 구조가 “판단 지연 비용”을 만들며, 이것이 핵심 긴장 요소로 작동해야 한다.
2. **관계 붕괴 관찰의 흥미**: 동일 목표 조건에서도 관계/트라우마 누적에 따라 전개가 달라지고, 플레이어는 차이를 관찰하는 과정에서 재미를 체감해야 한다.

### 1사이클 완료 정의

- **시작 조건**: `Port 불시착` 이후 자동 서사 Tick이 최초 실행되고, 개입 큐가 활성화된 시점.
- **종료 조건**: `SessionEnd`가 `전멸`, `긴급 탈출`, `목표 완수` 중 하나로 확정되고, 결과가 영속화된 시점.
- **완료 시간 목표**: 1사이클 **15~20분(목표 중앙값 18분)**.

### 플레이어 핵심 선택 3개

1. **언제 개입하는가**: 지금 개입해 즉시 리스크를 낮출지, 더 많은 맥락을 기다릴지 선택.
2. **무엇에 개입하는가**: 생존/목표 달성/관계 안정 중 우선 대상 선택.
3. **왜 개입하는가**: 현재 판 승리 극대화인지, 다음 판 메타 이득 확보인지 개입 의도 선택.

### 실패 잔존 보상(최소)

1. **메타 보상 1단위 이상**: 실패 판 종료 시에도 다음 판 개입 보조에 쓰일 최소 재화 지급.
2. **적응 특성 샘플 1개 보존**: 실패 과정에서 확인된 행동/트라우마 변형 1개를 다음 판 선택지로 남김.

### 성공/실패 KPI 목표값 (MVP)

- **성공 KPI**
  - 사이클 완주율: **55% 이상**
  - 첫 3판 이내 재도전율: **70% 이상**
  - 개입 유의미 체감(5점 척도 4~5): **65% 이상**
- **실패 KPI**
  - 전멸 비율: **45% 이하**
  - 1판 종료 후 이탈률: **30% 이하**
  - 실패 보상 부족 응답(5점 척도 1~2): **25% 이하**

### 테스트 대상 페르소나

1. **전략 최적화형**: 규칙 학습과 승률 개선을 우선하는 플레이어.
2. **서사 관찰형**: 관계 변화와 붕괴 서사의 다양성을 우선 체감하는 플레이어.
