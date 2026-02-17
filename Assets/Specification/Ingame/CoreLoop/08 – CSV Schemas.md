# 08 – CSV Schemas

본 문서는 Core Loop에서 사용하는 CSV 스키마를 정의한다.

------

## Common Rules

- Encoding: UTF-8
- Delimiter: `,`
- Header: Required
- Boolean: `true|false`
- Enum은 대소문자 구분

------

## SessionConfig.csv

| Column | Type | Required | Default | Range/Rule |
|---|---|---|---|---|
| session_id | string | Yes | - | non-empty |
| tick_seconds | float | Yes | 2.0 | > 0 |
| max_decision_retry | int | Yes | 3 | 0..10 |
| max_persist_retry | int | Yes | 3 | 0..10 |
| persist_retry_backoff_ms | int | Yes | 500 | 0..10000 |

## StateTransitionRules.csv

| Column | Type | Required | Default | Range/Rule |
|---|---|---|---|---|
| from_state | enum | Yes | - | Plan/Drop/AutoNarrative/CaptainIntervention/NightDream/Resolve |
| to_state | enum | Yes | - | Drop/AutoNarrative/CaptainIntervention/NightDream/Resolve/NextCycle/SessionEnd |
| entry_condition | string | Yes | - | non-empty |
| exit_condition | string | Yes | - | non-empty |
| guard | string | Yes | - | non-empty |
| priority | int | Yes | 100 | 0..1000 |
| enabled | bool | Yes | true | true/false |

## InterventionCommands.csv

| Column | Type | Required | Default | Range/Rule |
|---|---|---|---|---|
| command_id | string | Yes | - | unique |
| issued_tick | int | Yes | - | >= 0 |
| apply_tick | int | Yes | issued_tick+1 | >= issued_tick+1 |
| command_type | enum | Yes | - | PolicyChange/ObjectPriority/DreamRoutine |
| target_scope | string | Yes | global | non-empty |
| payload_json | string | Yes | {} | valid JSON object |
| priority | int | Yes | 100 | 0..1000 |
| supersedes_command_id | string | No | "" | existing command_id or empty |

## CharacterProfiles.csv

| Column | Type | Required | Default | Range/Rule |
|---|---|---|---|---|
| character_id | string | Yes | - | unique |
| trait_weights_json | string | Yes | - | valid JSON object |
| stress | float | Yes | 0 | 0..100 |
| health | float | Yes | 100 | 0..100 |
| trauma_level | int | Yes | 0 | 0..10 |
| enabled | bool | Yes | true | true/false |

## TerminationRules.csv

| Column | Type | Required | Default | Range/Rule |
|---|---|---|---|---|
| rule_id | string | Yes | - | unique |
| condition_type | enum | Yes | - | TotalWipe/EmergencyExtract/ObjectiveComplete |
| threshold_expr | string | Yes | - | non-empty |
| result_code | string | Yes | - | non-empty |
| enabled | bool | Yes | true | true/false |
| priority | int | Yes | 100 | 낮을수록 우선 |

------

## Type Mapping (DTO)

- `CoreLoopSnapshot`: `session_id`, `tick_index`, `loop_state`, `characters[]`, `pending_interventions[]`, `last_applied_tick`
- `StateTransitionRuleRow`: `from_state`, `to_state`, `entry_condition`, `exit_condition`, `guard`, `priority`, `enabled`
- `InterventionCommandRow`: `command_id`, `issued_tick`, `apply_tick`, `command_type`, `target_scope`, `payload_json`, `priority`, `supersedes_command_id`
- `TerminationRuleRow`: `rule_id`, `condition_type`, `threshold_expr`, `result_code`, `enabled`
