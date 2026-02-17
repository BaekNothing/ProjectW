# 02 – State Machine

본 문서는 Ingame 코어 루프 상태 전이 규칙을 정의한다.

------

## Required State Order

1. `Plan`
2. `Drop`
3. `AutoNarrative`
4. `CaptainIntervention`
5. `NightDream`
6. `Resolve`

`Resolve` 이후 분기:

- `NextCycle`
- `SessionEnd`

------

## Transition Contract

- 상태 전이는 Tick 경계에서만 발생한다.
- 한 Tick 내 복수 상태 동시 통과는 금지한다.
- 각 상태는 Entry/Update/Exit/Guard/OnFail을 가져야 한다.

------

## State Transition Table (MVP)

| From | To | Entry | Update | Exit | Guard | Side Effect | OnFail |
|---|---|---|---|---|---|---|---|
| Plan | Drop | session_started=true | assign_initial_policies | plan_ready=true | csv_loaded=true | queue_init | hold_state + E-STATE-101 |
| Drop | AutoNarrative | drop_not_processed | apply_drop_events | drop_done=true | drop_event_set_valid=true | event_log_append | fallback_drop_seed + E-STATE-102 |
| AutoNarrative | CaptainIntervention | auto_phase_open=true | ai_act + relation_update | intervention_window_open=true | autonomy_result_valid=true | autonomy_log_append | skip_invalid_actions + E-STATE-103 |
| CaptainIntervention | NightDream | intervention_window_open=true | apply_intervention_queue | intervention_window_closed=true | queue_apply_ok=true | intervention_log_append | defer_unapplied + E-INT-201 |
| NightDream | Resolve | dream_phase_open=true | reorder_routines | dream_done=true | dream_rules_valid=true | dream_log_append | keep_previous_routine + E-STATE-104 |
| Resolve | NextCycle | resolve_started=true | evaluate_session_end | end_condition=false | snapshot_ready=true | snapshot_write_attempt | retry_persist + E-PST-301 |
| Resolve | SessionEnd | resolve_started=true | evaluate_session_end | end_condition=true | termination_rule_match=true | finalize_session | hold_state + E-END-401 |

------

## Invalid Transition

- `Plan -> AutoNarrative`
- `AutoNarrative -> Resolve`
- `Resolve -> Plan` (반드시 NextCycle 경유)

금지 전이 시 `E-STATE-199`를 기록하고 현재 상태를 유지한다.

------

## API Contract

- `EvaluateTransition(currentState, context) -> TransitionDecision`
- `TransitionDecision` 필수 필드:
  - `next_state`
  - `applied_guard`
  - `side_effects[]`
  - `error_code?`

------

## CSV Dependency

- `StateTransitionRules.csv`를 상태 전이 정책 소스로 사용한다.
- 스키마는 `08 – CSV Schemas.md`를 따른다.
