# SSOT – Ingame

본 문서는 Project_W Ingame 영역의 상위 SSOT 인덱스다.

코어 루프 구현 규칙은 하위 문서로 분리되며,
구현 판단은 반드시 본 문서와 링크된 하위 SSOT를 함께 참조한다.

------

## Authority

- 본 문서는 Ingame 영역의 상위 진입점이다.
- 세부 규칙은 `CoreLoop` 하위 문서에 위임된다.
- 하위 문서 간 충돌 시 본 문서의 우선순위 규칙을 따른다.

------

## Scope

- 캐릭터 행동 모델
- 자율 루틴 / 정책 반영 방식
- 위기 상황 및 저력 발현 조건
- 누적 변화(퇴적)의 처리 방식
- 코어 루프 상태 전이 및 종료 조건

------

## Non-Goals

- 밸런스 수치의 미세 조정
- 특정 사례의 결과 정당화
- 서사적 해석의 일반화

------

## Core Loop Spec Map

아래 문서는 코어 루프 구현 단위 SSOT다.

1. `Assets/Specification/Ingame/CoreLoop/01 – Tick and Timebase.md`
2. `Assets/Specification/Ingame/CoreLoop/02 – State Machine.md`
3. `Assets/Specification/Ingame/CoreLoop/03 – Intervention Boundary.md`
4. `Assets/Specification/Ingame/CoreLoop/04 – Autonomy Decision.md`
5. `Assets/Specification/Ingame/CoreLoop/05 – Session End and Persistence.md`
6. `Assets/Specification/Ingame/CoreLoop/06 – Prototype Gate.md`

------

## Ingame Decision Order

Ingame 구현 판단은 아래 순서를 따른다.

1. 본 문서(`SSOT – Ingame`)
2. `CoreLoop/01..06` 세부 문서
3. 상위 메타 규칙 문서(`SSOT – Workflow Local Spec × Unity × GitHub`)
4. Unity 구현 코드

------

## Change Rationale

- 단일 문서에 규칙이 과밀하면 구현 시 추적성이 급격히 낮아진다.
- 구현 단위 분리는 작업 범위/검증 범위/책임 경계를 명확히 한다.

------

## Open TODO

- TODO: 코어 루프 외 하위 도메인(Combat/Resource/Event)도 동일 포맷으로 분리.
- TODO: Outgame/Metadata와의 교차 참조 표준 템플릿 정의.
