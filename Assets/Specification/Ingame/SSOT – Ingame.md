# SSOT – Ingame

본 문서는 Project_W Ingame 영역의 상위 SSOT 인덱스다.

코어 루프 구현 판단은 본 문서와 `CoreLoop/01..11` 세부 문서를 함께 참조해야 한다.
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

------

## Ingame Decision Order

Ingame 구현 판단은 아래 순서를 따른다.

1. 본 문서(`SSOT – Ingame`)
2. CoreLoop 세부 문서(`01..11`)
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
