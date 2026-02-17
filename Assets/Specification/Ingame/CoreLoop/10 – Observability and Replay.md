# 10 – Observability and Replay

본 문서는 Core Loop 관측성과 리플레이 기준을 정의한다.

------

## Minimum Observability Fields

- `session_id`
- `tick_index`
- `loop_state`
- `state_transition` (`from -> to`)
- `applied_intervention_ids[]`
- `rejected_intervention_ids[]`
- `autonomy_selected_action`
- `autonomy_seed`
- `termination_evaluation_result`
- `error_codes[]`

------

## Decision Trace Contract

자율 결정 로그는 다음 필드를 갖는다.

- `decision_trace_id`
- `character_id`
- `candidate_actions[]`
- `candidate_scores[]`
- `selected_action_id`
- `blocked_actions[]`

------

## Replay Procedure

1. 동일 `session_id`, `tick_range`, CSV 입력 세트 로딩
2. 동일 seed 규칙으로 난수 재생성
3. Tick별 `selected_action_id`, `state_transition`, `applied_intervention_ids` 비교
4. 불일치 시 `E-RPL-001` 기록

------

## Replay Success Criteria

- 동일 입력 + 동일 seed에서 아래 필드가 모두 동일해야 한다.
  - 상태 전이
  - 선택 행동
  - 적용 개입 명령 집합

------

## Integration Link

- 자율 결정 계약은 `04 – Autonomy Decision.md`를 따른다.
- 오류 처리 규칙은 `09 – Error Handling and Recovery.md`를 따른다.
