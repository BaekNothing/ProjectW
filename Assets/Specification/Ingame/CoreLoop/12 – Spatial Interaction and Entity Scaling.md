# 12 – Spatial Interaction and Entity Scaling

본 문서는 물리엔진 없이 좌표 기반 이동/탐색과 이벤트 상호작용을 사용할 때의 설계 기준을 정의한다.

------

## Goal

- Unity Physics 의존 없이도 예측 가능한 상호작용 루프를 유지한다.
- 캐릭터/오브젝트(예: 화분, 펜)를 동일한 상호작용 엔티티 모델로 취급한다.
- 현재 규모에서는 OOP 기반으로 구현하고, 고밀도 시뮬레이션 시 ECS/DOTS 전환 기준을 명확히 둔다.

------

## Core Position

- `Physics Trigger` 대신 `좌표 기반 검색 + 이벤트 발행` 방식은 타당하다.
- 단, 탐색 비용과 순서 비결정성 문제를 막기 위해 아래 계약을 강제한다.

1. 탐색은 Tick 경계에서만 수행한다.
2. 탐색 결과는 거리/우선순위/ID 정렬로 결정론적으로 확정한다.
3. 상호작용은 직접 호출 대신 `InteractionEvent` 큐를 통해 적용한다.

------

## Spatial Query Contract (No Physics)

### Input

- `agent_position`
- `interaction_radius`
- `interaction_mask` (entity category bitset)
- `tick_index`

### Output

- `candidates[]` (거리 오름차순)
- `selected_target_id`
- `query_trace_id`

### Zone Anchor Contract (Tag + Boundary)

- Zone은 이름이 아니라 `zone_id + tags[] + boundary`로 식별한다.
- 캐릭터/시뮬레이션은 Zone 이름 문자열(`MissionZone`, `SleepZone` 등)에 의존하지 않는다.
- Zone 최소 필드:
  - `zone_id`
  - `tags[]` (예: `zone.mission`, `need.hunger`, `need.sleep`)
  - `boundary` (`Collider` 또는 `Collider2D`)
  - `position` (anchor position)

### Need Resolution Contract

- 욕구 해소는 해당 `need` 태그 Zone의 `boundary` 내부에서만 허용된다.
- `need.hunger`: 식사/간식 계열 행동의 해소 조건.
- `need.sleep`: 수면 계열 행동의 해소 조건.
- `boundary` 밖에서는 같은 행동 타입이어도 해소를 적용하지 않는다.

### Deterministic Rule

동일 반경 내 다수 후보 발견 시 아래 순서로 선택한다.

1. 거리(`distance_sq`) 오름차순
2. `interaction_priority` 내림차순
3. `entity_id` 오름차순

------

## Unified Interactable Entity Model

소형 집기(화분, 펜) 포함 모든 상호작용 대상을 동일 인터페이스로 취급한다.

### Required Fields

- `entity_id`
- `entity_type` (Character, Furniture, Prop, Tool, etc.)
- `position`
- `interaction_radius`
- `interaction_priority`
- `interaction_tags[]`
- `state_flags` (pickable, usable, blocked, broken 등)

### Interface Contract

- `CanInteract(actor, context) -> bool`
- `BuildInteractionOptions(actor, context) -> InteractionOption[]`
- `ApplyInteraction(option, context) -> InteractionResult`

------

## Event-Driven Interaction Contract

### Queue Rule

- 상호작용 결과는 즉시 월드 상태를 변경하지 않는다.
- `InteractionEvent` 큐에 기록한 뒤 Tick 적용 단계에서 일괄 반영한다.

### Event Minimum Fields

- `session_id`
- `tick_index`
- `event_id`
- `actor_id`
- `target_id`
- `interaction_type`
- `payload`
- `result_code`

### Failure Rule

- 타깃 소실/무효 상태이면 이벤트를 `rejected`로 기록하고 다음 후보 재평가.
- 재평가 한도는 `SessionConfig.max_decision_retry`를 따른다.

------

## Move-Then-Act Contract (MVP)

- 행동 실행 순서는 `Move -> Action` 고정이다.
- 목적 행동(`Eat`, `Sleep`, `Mission` 등)은 목표 위치 도착 전에는 실행할 수 없다.
- 도착 전 `current_action`은 항상 `Move`로 기록한다.
- 이동 중 캐릭터 겹침은 허용한다.
- 단, 실제 액션 수행 시에는 Zone 내 액션 슬롯 오프셋을 적용해 가급적 비중첩을 유지한다.

------

## Performance Guidance (Without ECS)

현 단계(소수 캐릭터 + 수십~수백 오브젝트)에서는 ECS/DOTS 없이도 충분하다.

### Recommended Baseline

- 공간 인덱스: Uniform Grid 또는 Spatial Hash 2D
- Tick당 탐색 대상 제한: 같은/인접 셀만 조회
- Entity 데이터는 `struct-like runtime state`로 분리하고, MonoBehaviour는 표시/입력 브릿지로만 사용

### When to Consider ECS/DOTS

아래가 동시에 만족되면 전환 검토한다.

1. 상호작용 엔티티 수가 1,000+로 증가
2. Tick당 쿼리 수가 수만 단위
3. 메인스레드 CPU 병목으로 프레임/틱 안정성이 깨짐
4. Burst + Jobs 최적화로 이득이 명확한 순수 데이터 루프가 존재

------

## Adoption Strategy

1. **Phase 1 (Now)**: OOP + 좌표 쿼리 + 이벤트 큐 확정
2. **Phase 2**: Spatial Hash, object pooling, 로그/리플레이 기반 병목 계측
3. **Phase 3**: 병목이 확인된 query/apply 루프만 ECS/DOTS로 부분 이관

`ECS/DOTS first`가 아니라 `측정 후 병목 구간에만 도입`을 기본 원칙으로 한다.

------

## Integration Links

- 시간/틱 경계: `01 – Tick and Timebase.md`
- 자율 행동 결정: `04 – Autonomy Decision.md`
- 관측/리플레이: `10 – Observability and Replay.md`
- 데이터 주입: `07 – Data Injection Contract (CSV First).md`
