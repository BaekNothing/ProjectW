# InterventionCommands.csv.spec

## Purpose

플레이어 개입 명령을 Tick 경계 큐로 주입한다.

## Required Columns

- `command_id`
- `issued_tick`
- `apply_tick`
- `command_type`
- `target_scope`
- `payload_json`
- `priority`
- `supersedes_command_id`

## Sample Row

`cmd_0001,5,6,PolicyChange,global,{"policy":"safe"},200,`

## Validation

- `apply_tick < issued_tick + 1`이면 `E-CSV-003`.
