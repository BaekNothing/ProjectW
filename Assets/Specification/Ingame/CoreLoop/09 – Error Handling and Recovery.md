# 09 – Error Handling and Recovery

본 문서는 Core Loop 오류 처리 및 복구 규칙을 정의한다.

------

## Error Code Table

| Code | Category | Meaning | Runtime Action |
|---|---|---|---|
| E-CSV-001 | CSV | Header missing | startup block |
| E-CSV-002 | CSV | Required column missing | startup block |
| E-CSV-003 | CSV | Type mismatch | startup block |
| E-CSV-004 | CSV | Required row missing | startup block |
| E-TIME-001 | Time | Tick commit failure | retry current tick |
| E-STATE-101 | State | Plan entry failure | hold state |
| E-STATE-102 | State | Drop processing failure | fallback seed path |
| E-STATE-103 | State | Autonomy phase failure | invalid action skip |
| E-STATE-104 | State | NightDream failure | keep previous routine |
| E-STATE-199 | State | Invalid transition attempted | transition reject |
| E-INT-201 | Intervention | Queue apply failed | defer next tick |
| E-PST-301 | Persist | Snapshot write failed | persistence retry |
| E-PST-399 | Persist | Retry exhausted | safe halt |
| E-END-401 | End | Termination rule mismatch | hold resolve state |

------

## Recovery Strategy

- Startup 단계 에러(CSV 계열)는 세션 시작 차단이 기본 동작이다.
- Runtime 단계 에러는 상태 보존 + 재시도 정책을 따른다.
- Persist 계열 에러는 `max_persist_retry` 소진 전까지 재시도한다.

------

## Safe Halt Rule

아래 조건이면 `SafeHalt` 전환:

- `E-PST-399` 발생
- 같은 Tick에서 치명 에러 3회 연속 발생

`SafeHalt`에서는 입력 수신만 허용하고 상태 변경은 차단한다.

------

## Logging Contract

모든 에러 로그는 최소 필드를 포함한다.

- `session_id`
- `tick_index`
- `loop_state`
- `error_code`
- `error_message`
- `recover_action`
- `timestamp_utc`
