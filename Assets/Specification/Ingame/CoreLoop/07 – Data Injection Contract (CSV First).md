# 07 – Data Injection Contract (CSV First)

본 문서는 Ingame Core Loop 데이터 주입 계약을 정의한다.

------

## Source Policy

- 데이터 소스는 `CSV First`로 고정한다.
- Excel/Google Sheet는 직접 연결하지 않는다.
- Excel/Google Sheet 데이터는 반드시 CSV export 후 반영한다.

------

## Required CSV Files

1. `SessionConfig.csv`
2. `StateTransitionRules.csv`
3. `InterventionCommands.csv`
4. `CharacterProfiles.csv`
5. `TerminationRules.csv`

------

## Load Order

1. `SessionConfig.csv`
2. `StateTransitionRules.csv`
3. `CharacterProfiles.csv`
4. `TerminationRules.csv`
5. `InterventionCommands.csv`

선행 파일 로드 실패 시 후속 파일 로드를 중단한다.

------

## Interface Contract

### `ICsvConfigProvider`

- `LoadSessionConfig() -> SessionConfig`
- `LoadStateTransitionRules() -> List<StateTransitionRuleRow>`
- `LoadInterventionRules() -> List<InterventionCommandRow>`
- `LoadTerminationRules() -> List<TerminationRuleRow>`

### `IDataSnapshotProvider`

- `LoadSnapshot(sessionId) -> CoreLoopSnapshot`

### `ISeedProvider`

- `GetSeed(sessionId, tick) -> int`

------

## Validation Rules

- 인코딩은 UTF-8이어야 한다.
- 헤더가 없으면 `E-CSV-001`.
- 필수 컬럼 누락 시 `E-CSV-002`.
- 타입 불일치 시 `E-CSV-003`.
- 필수 행 미존재 시 `E-CSV-004`.

------

## Runtime Behavior on Failure

- CSV 검증 실패 시 세션 시작을 차단한다.
- 차단 시 마지막 정상 스냅샷이 있으면 읽기 전용 복구 모드로 진입할 수 있다.
