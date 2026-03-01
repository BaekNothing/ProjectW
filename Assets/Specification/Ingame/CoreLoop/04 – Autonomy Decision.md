# 04 – Autonomy Decision

본 문서는 캐릭터 자율 행동 결정 규칙을 정의한다.

------

## Decision Inputs

- 정책(지령)
- 성향 가중치
- 상태값(체력/스트레스/트라우마)
- 현재 루프 상태
- seed(`session_id + tick_index` 기반)

------

## Selection Rule

- 매 Tick마다 후보 행동 집합을 계산한다.
- `score = base_weight * policy_multiplier * state_multiplier`를 계산한다.
- 후보 행동 선택은 가중치 기반 확률로 수행한다.
- 확률 요소는 deterministic seed 난수로 생성한다.
- Routine MVP 기본 규칙:
  - 식사 행동은 `식사 시간` AND `공복 낮음` AND `스트레스 낮음`을 동시에 만족할 때만 선택한다.
  - 수면 행동은 `수면 시간` AND `공복 낮음` AND `스트레스 낮음`을 동시에 만족할 때만 선택한다.
  - 위 조건 미충족 시 기본 후보는 `Mission`이다.

### Action Intent vs Execution

- `intended_action`: 현재 Tick에서 의사결정으로 선택된 목적 행동.
- `current_action`: 실제 실행된 행동.
- 이동이 남아 있으면 `current_action = Move`이며, `intended_action`은 유지한다.
- 목적 행동의 효과 적용은 도착 후 Tick에서만 허용된다.

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

### Output

- `selected_action`
- `candidate_actions[]`
- `decision_trace_id`
- `retry_count`

------

## API Contract

- `GetSeed(sessionId, tick) -> int`
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
