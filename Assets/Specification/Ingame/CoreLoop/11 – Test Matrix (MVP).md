# 11 – Test Matrix (MVP)

본 문서는 Core Loop MVP 검증 테스트의 단일 기준이다.

------

## Functional Tests

| Test ID | Scenario | Input | Expected Result |
|---|---|---|---|
| T01 | Full loop 3 cycles | valid CSV set | `Plan -> ... -> Resolve -> NextCycle` 3회 연속 성공 |
| T02 | Intervention apply timing | command apply_tick = current+1 | 명령은 다음 Tick에서만 반영 |
| T03 | Objective complete end | objective condition true | `SessionEnd(ObjectiveComplete)` + persist success |

## Boundary Tests

| Test ID | Scenario | Input | Expected Result |
|---|---|---|---|
| T10 | Pause/Resume stability | pause/resume 반복 | Tick 인덱스 연속성 유지 |
| T11 | Conflicting interventions | 동일 Tick 상충 명령 3개 | 최신/priority/ID 규칙으로 일관 처리 |
| T12 | Guard false handling | transition guard=false | 상태 유지 + 로그 기록 |

## Failure/Recovery Tests

| Test ID | Scenario | Input | Expected Result |
|---|---|---|---|
| T20 | Missing CSV column | required column 제거 | startup block + `E-CSV-002` |
| T21 | Persist retry success | 1회 저장 실패 강제 | `PersistenceRetry` 후 성공 |
| T22 | Invalid transition reject | 금지 전이 시도 | 전이 거부 + 대체 루트 유지 |
| T23 | Deterministic replay | 동일 seed/입력 2회 실행 | 동일 결과 재현 |

------

## Mandatory Pass Set

- `T01`, `T02`, `T03`, `T10`, `T11`, `T21`, `T22`, `T23`

위 테스트가 전부 PASS일 때만 MVP Gate 통과로 판정한다.

------

## Traceability

- Gate 매핑: `06 – Prototype Gate.md`
- 상태 계약: `02 – State Machine.md`
- 개입 계약: `03 – Intervention Boundary.md`
- 종료/영속화: `05 – Session End and Persistence.md`
