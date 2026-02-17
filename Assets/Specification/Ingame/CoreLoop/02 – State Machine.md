# 02 – State Machine

본 문서는 Ingame 코어 루프 상태 전이 규칙을 정의한다.

------

## Required State Order

고정 순서:

1. `Plan`
2. `Drop`
3. `AutoNarrative`
4. `CaptainIntervention`
5. `NightDream`
6. `Resolve`

`Resolve` 이후 분기:

- `NextCycle`
- `SessionEnd`

## Transition Contract

- 상태 전이는 Tick 경계에서만 발생한다.
- 한 Tick 내 복수 상태 동시 통과는 금지한다.
- 각 상태는 `Entry Condition`, `Update Rule`, `Exit Condition`을 반드시 가진다.

## Invalid Transition

아래는 금지된다.

- `Plan -> AutoNarrative` (Drop 생략)
- `AutoNarrative -> Resolve` (CaptainIntervention/NightDream 생략)
- `Resolve -> Plan` 직접 복귀(반드시 `NextCycle` 경유)
