# 04 – Autonomy Decision

본 문서는 캐릭터 자율 행동 결정 규칙을 정의한다.

------

## Decision Inputs

- 정책(지령)
- 성향 가중치
- 상태값(체력/스트레스/트라우마)
- 현재 루프 상태
- seed(`session_id + tick_index` 기반)
- 월드 아이템 가용성(`WorldItem.tags`, 소유권, 공용/개인 정책)
- Tick에서 생성된 원자적 Job 목록(`Work`, `Eat`, `Sleep`)

------

## Selection Rule

- 매 Tick마다 후보 행동 집합을 계산한다.
- 행동은 **원자적 Job 단위**로만 선택한다.
  - `Work`, `Eat`, `Sleep` 모두 동일한 `AtomicJob` 모델을 사용한다.
- Job 후보의 공통 게이트:
  - Zone 게이트(해당 Job Zone 도달)
  - Item Requirement 게이트(요구 태그 충족)
- Job 스코어링은 `score = base_weight * policy_multiplier * state_multiplier`를 기본으로 하되,
  Item Requirement 미충족 Job은 후보에서 제외한다.
- 후보 행동 선택은 가중치 기반 확률로 수행한다.
- 확률 요소는 deterministic seed 난수로 생성한다.
- Routine MVP 기본 규칙:
  - 식사 Job은 `식사 시간` 또는 `포만도 임계 이하`에서 생성 가능하다.
  - 수면 Job은 `수면 시간대` 또는 `스트레스 임계 이하`에서 생성 가능하다.
  - 작업 Job은 `Task.RemainingWork > 0`일 때 생성 가능하다.

### Action Intent vs Execution

- `intended_action`: 현재 Tick에서 Job 할당 결과로 선택된 목적 행동.
- `current_action`: 실제 실행된 행동.
- 이동이 남아 있으면 `current_action = Move`이며, `intended_action`은 유지한다.
- 목적 행동의 효과 적용은 도착 후 Tick에서만 허용된다.
- Mission 진행도/Need 해소는 `Zone + Item Requirement`를 동시에 만족할 때만 반영한다.

------

## Safety Rule

- 금지 상태 전이를 유발하는 행동은 무효화한다.
- 무효화 시 차순위 후보를 재평가한다.
- 최대 재평가 횟수는 `SessionConfig.csv.max_decision_retry`를 따른다.

------

## I/O Contract

### Input

- `character_profiles[]`
- `policy_context`
- `loop_state`
- `tick_index`
- `seed`
- `office_items[]` (tag/ownership/policy)
- `atomic_jobs[]`

### Output

- `selected_action`
- `candidate_actions[]`
- `decision_trace_id`
- `retry_count`

------

## API Contract

- `GetSeed(sessionId, tick) -> int`
- `BuildJobs(simTime, task, agents) -> AtomicJob[]`
- `AssignBestJob(agent, jobs, officeItems) -> AtomicJob?`
- `EvaluateAutonomy(context) -> AutonomyDecisionResult`
- `AutonomyDecisionResult` 필수 필드:
  - `selected_action_id`
  - `candidate_rank[]`
  - `blocked_actions[]`
  - `error_code?`

------

## Replay Contract

- Tick별 결정 로그는 `10 – Observability and Replay.md` 형식을 따라야 한다.
- 동일 입력 + 동일 seed에서 `selected_action_id`가 동일해야 한다.

------

## CSV Dependency

- `CharacterProfiles.csv`, `SessionConfig.csv`를 참조한다.
- 스키마는 `08 – CSV Schemas.md`를 따른다.
