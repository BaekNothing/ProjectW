# SessionConfig.csv.spec

## Purpose

코어 루프 실행 파라미터를 정의한다.

## Required Columns

- `session_id` (string)
- `tick_seconds` (float, >0)
- `max_decision_retry` (int, 0..10)
- `max_persist_retry` (int, 0..10)
- `persist_retry_backoff_ms` (int, 0..10000)

## Sample Row

`dev_session_001,2.0,3,3,500`

## Validation

- `tick_seconds <= 0`이면 `E-CSV-003`
