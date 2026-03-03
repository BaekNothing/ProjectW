# 11 – Test Matrix (MVP)

본 문서는 Core Loop MVP 검증 테스트의 단일 기준이다.

------

## Functional Tests

| Test ID | Scenario | Input | Expected Result |
|---|---|---|---|
| T01 | Full loop 3 cycles | valid CSV set | `Plan -> ... -> Resolve -> NextCycle` 3회 연속 성공 |
| T02 | Intervention apply timing | command apply_tick = current+1 | 명령은 다음 Tick에서만 반영 |
| T03 | Objective complete end | objective condition true | `SessionEnd(ObjectiveComplete)` + persist success |
| T04 | Unified atomic job build | task + agent needs | `Work/Eat/Sleep`가 동일 Job 모델로 생성 |
| T05 | Office item bootstrap | office item count=12 | 필수 태그(`desk/computer/bed/pillow/blanket/table/tray/cup`) 존재 |

## Boundary Tests

| Test ID | Scenario | Input | Expected Result |
|---|---|---|---|
| T10 | Pause/Resume stability | pause/resume 반복 | Tick 인덱스 연속성 유지 |
| T11 | Conflicting interventions | 동일 Tick 상충 명령 3개 | 최신/priority/ID 규칙으로 일관 처리 |
| T12 | Guard false handling | transition guard=false | 상태 유지 + 로그 기록 |
| T13 | Move then action gating | action target 미도달 상태 | `current_action=Move`, 목적 행동 효과 미적용 |
| T14 | Need zone boundary check | need 태그 Zone boundary 밖 | 욕구 해소 미적용 |
| T15 | Action slot de-overlap | 동일 Zone 동시 행동 2명 | target/action slot 비중첩 유지 |
| T16 | Item requirement gate | zone inside + item missing | 행동 타입 일치해도 효과 미적용 |
| T17 | Personal item preference | personal + public item 공존 | 본인 개인 물품 우선 사용 |
| T18 | Carry capacity gate | 한 캐릭터가 3개 운반 시도 | 기본 용량 2개 초과는 거부 |

## Failure/Recovery Tests

| Test ID | Scenario | Input | Expected Result |
|---|---|---|---|
| T20 | Missing CSV column | required column 제거 | startup block + `E-CSV-002` |
| T21 | Persist retry success | 1회 저장 실패 강제 | `PersistenceRetry` 후 성공 |
| T22 | Invalid transition reject | 금지 전이 시도 | 전이 거부 + 대체 루트 유지 |
| T23 | Deterministic replay | 동일 seed/입력 2회 실행 | 동일 결과 재현 |
| T24 | Personal item misuse observed | 타인 개인 물품 사용 + 소유자 발견 | 소유자→사용자 호감도 하락 |
| T25 | Item conflict escalation | 제지 후 다툼 발생 | 양방향 호감도 추가 하락 |
| T26 | Work outcome affinity | 빠른 완료/과중/도움 이벤트 | 이벤트 타입별 호감도 증감 반영 |

------

## Mandatory Pass Set

- `T01`, `T02`, `T03`, `T10`, `T11`, `T21`, `T22`, `T23`
- Routine/Spatial MVP 확장 시 `T13`, `T14`, `T15`를 추가 필수로 포함한다.
- Item/Job/Affinity MVP 확장 시 `T04`, `T05`, `T16`, `T24`, `T25`, `T26`를 추가 필수로 포함한다.

위 테스트가 전부 PASS일 때만 MVP Gate 통과로 판정한다.

------

## Traceability

- Gate 매핑: `06 – Prototype Gate.md`
- 상태 계약: `02 – State Machine.md`
- 개입 계약: `03 – Intervention Boundary.md`
- 종료/영속화: `05 – Session End and Persistence.md`
- 자율 결정/Job 계약: `04 – Autonomy Decision.md`
- Spatial/Need/Item 게이트: `12 – Spatial Interaction and Entity Scaling.md`
