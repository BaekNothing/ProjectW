# 03 – Intervention Boundary

본 문서는 플레이어 개입 경계 규칙을 정의한다.

------

## Allowed Intervention

- 정책(지령) 변경
- 목표 우선순위 조정
- `NightDream` 단계 루틴 재정렬

## Forbidden Intervention

- 캐릭터 직접 이동/직접 행동 명령
- Tick 경계 밖 강제 상태 전환
- 종료 조건 우회 명령

------

## Queue Rule

- 개입 명령은 즉시 적용하지 않는다.
- 명령은 대기열에 저장 후 다음 Tick에서만 반영한다.
- 동일 Tick 상충 명령 처리 우선순위:
  1. `issued_tick` 최신 우선
  2. 동일 시각이면 `priority` 높은 값 우선
  3. 그래도 동일하면 `command_id` 오름차순

------

## Conflict Rule

- `supersedes_command_id`가 지정되면 대상 명령을 무효화한다.
- 이미 적용된 명령은 supersede 대상이 될 수 없다.
- 무효화 결과는 이벤트 로그에 남긴다(`E-INT-202` 비에러 이벤트 코드).

------

## I/O Contract

### Input

- `queue[]: InterventionCommandRow`
- `current_tick`
- `current_state`

### Output

- `applied_commands[]`
- `rejected_commands[]`
- `last_applied_tick`

------

## API Contract

- `ApplyInterventions(tick, queue) -> AppliedResult`
- `AppliedResult` 필수 필드:
  - `applied_count`
  - `rejected_count`
  - `rejection_reasons[]`

------

## Visibility Rule

플레이어 UI는 최소 다음을 표시해야 한다.

- 적용 대기 명령 수
- 마지막 적용 Tick
- 최근 거부 명령 사유 1건

------

## CSV Dependency

- `InterventionCommands.csv` 및 `SessionConfig.csv`를 참조한다.
- 스키마는 `08 – CSV Schemas.md`를 따른다.
