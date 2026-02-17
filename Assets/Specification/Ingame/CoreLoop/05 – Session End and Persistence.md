# 05 – Session End and Persistence

본 문서는 세션 종료 및 영속화 규칙을 정의한다.

------

## Session End Conditions

`SessionEnd`는 아래 3조건에서만 허용된다.

1. `TotalWipe`
2. `EmergencyExtract`
3. `ObjectiveComplete`

위 조건 외 종료는 금지한다.

------

## End Priority Rule

동일 Tick에서 복수 종료 조건이 동시에 참이면 우선순위는 다음과 같다.

1. `TotalWipe`
2. `EmergencyExtract`
3. `ObjectiveComplete`

우선순위가 낮은 조건은 이벤트 로그에 `suppressed`로 기록한다.

------

## Persistence Minimum Set

세션 종료 시 최소 저장 항목:

- `session_id`
- `tick_index`
- `loop_state`
- `characters_snapshot[]`
- `event_log[]`
- `termination_result_code`
- `last_applied_tick`

------

## Failure Handling

- 저장 실패 시 종료 확정 금지.
- 상태를 `PersistenceRetry`로 전환한다.
- 재시도 정책:
  - `max_retry = SessionConfig.csv.max_persist_retry`
  - `retry_backoff_ms = SessionConfig.csv.persist_retry_backoff_ms`
- 최대 횟수 초과 시 `E-PST-399`를 기록하고 세션을 안전중지(`SafeHalt`)한다.

------

## API Contract

- `ResolveSessionEnd(context) -> SessionEndResult`
- `PersistSnapshot(snapshot) -> PersistResult`
- `SessionEndResult` 필수 필드:
  - `is_end`
  - `end_reason`
  - `suppressed_reasons[]`
- `PersistResult` 필수 필드:
  - `success`
  - `attempt_count`
  - `error_code?`

------

## CSV Dependency

- `TerminationRules.csv`, `SessionConfig.csv`를 참조한다.
- 스키마는 `08 – CSV Schemas.md`를 따른다.
