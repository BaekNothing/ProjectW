# 01 – Tick and Timebase

본 문서는 Ingame 코어 루프의 시간 기준 규칙을 정의한다.

------

## Rule 1. Fixed Tick

- 시뮬레이션 기준 주기는 `1 Tick / 2 seconds`다.
- Tick은 시스템 상태 커밋 단위다.
- Tick 번호는 `0`부터 시작하는 정수다.

## Rule 2. Substep Handling

- 내부 연산은 Tick 내부 Substep으로 실행될 수 있다.
- 외부 가시 상태는 Tick 경계에서만 확정된다.
- Substep 실패는 Tick 실패로 집계한다.

## Rule 3. Pause/Resume

- Pause 시 Tick 누적은 정지한다.
- Resume 후 Tick 인덱스는 연속성을 유지한다.
- Pause 중에도 명령 수신은 허용되며, 적용은 Resume 후 다음 Tick에서 수행한다.

## Rule 4. Determinism

- 동일 입력 + 동일 seed이면 동일 Tick 결과를 보장해야 한다.
- 난수 사용 지점은 로그에 기록해야 한다.

------

## I/O Contract

### Inputs

- `tick_index` (int, >= 0)
- `delta_accumulator_ms` (int, >= 0)
- `session_config.tick_seconds` (float, default 2.0)

### Outputs

- `tick_committed` (bool)
- `next_tick_index` (int)
- `tick_time_ms` (int)

------

## Failure Rules

- `tick_seconds <= 0`이면 실행 시작을 차단한다.
- Tick 커밋 실패 시 에러코드 `E-TIME-001`을 기록한다.

------

## CSV Dependency

- `SessionConfig.csv`의 `tick_seconds` 값을 참조한다.
- 스키마는 `08 – CSV Schemas.md`를 따른다.
