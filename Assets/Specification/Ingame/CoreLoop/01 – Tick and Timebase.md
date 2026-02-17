# 01 – Tick and Timebase

본 문서는 Ingame 코어 루프의 시간 기준 규칙을 정의한다.

------

## Rule 1. Fixed Tick

- 시뮬레이션의 기준 주기는 `1 Tick / 2 seconds`다.
- Tick은 전 시스템의 상태 커밋 기준 단위다.

## Rule 2. Substep Handling

- 내부 연산은 Tick 내부에서 세분화(Substep)될 수 있다.
- 단, 외부 가시 상태 변화는 Tick 경계에서만 확정된다.

## Rule 3. Pause/Resume

- Pause 시 Tick 누적은 정지한다.
- Resume 후 Tick 인덱스는 연속성을 유지한다(건너뜀 금지).

## Rule 4. Determinism

- 동일 입력과 동일 시드에서 Tick 결과는 재현 가능해야 한다.

------

## Implementation Notes

- Unity `Time.deltaTime` 기반 누적 후 2초 단위 커밋 방식 권장.
- Tick 인덱스는 세션 단위 증가 정수로 관리한다.
