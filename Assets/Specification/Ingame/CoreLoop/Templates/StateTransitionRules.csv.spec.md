# StateTransitionRules.csv.spec

## Purpose

상태 전이 계약을 외부 설정으로 주입한다.

## Required Columns

- `from_state`
- `to_state`
- `entry_condition`
- `exit_condition`
- `guard`
- `priority`
- `enabled`

## Sample Row

`Plan,Drop,session_started=true,plan_ready=true,csv_loaded=true,100,true`

## Validation

- 금지 전이 정의가 포함되면 로드 실패(`E-STATE-199`).
