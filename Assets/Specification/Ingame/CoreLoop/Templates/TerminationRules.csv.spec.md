# TerminationRules.csv.spec

## Purpose

세션 종료 판정 규칙을 주입한다.

## Required Columns

- `rule_id`
- `condition_type`
- `threshold_expr`
- `result_code`
- `enabled`
- `priority`

## Sample Row

`term_001,TotalWipe,alive_count==0,END_TOTAL_WIPE,true,10`

## Validation

- `condition_type`가 허용 enum 외 값이면 `E-CSV-003`.
