# 15 – Gate Mandatory Test Mapping

본 문서는 `06 – Prototype Gate.md`의 Gate 필수 테스트 ID를 Unity Test 메서드와 1:1(또는 명시적 N:1)로 추적하기 위한 기준 문서다.

------

## Gate Mandatory Mapping

| Test ID | Gate Item | Unity Test Method | Location | Category Rule | Related Spec |
|---|---|---|---|---|---|
| T01 | 상태 머신 순서 강제 | `T01_ThreeConsecutiveCycles_EnforcesRequiredCoreLoopOrder` | `Assets/Tests/EditMode/RoutineObservationMvpSessionTests.cs` | `[Category("GateMandatory")]` | `Assets/Specification/Ingame/CoreLoop/02 – State Machine.md` |
| T02 | 플레이어 직접 조작 경로 제거(개입 반영은 Tick 경계 이후) | `T02_InterventionApplyTiming_AppliesFromNextTickOnly` | `Assets/Tests/EditMode/IngameCoreInterventionQueueTests.cs` | `[Category("GateMandatory")]` | `Assets/Specification/Ingame/CoreLoop/03 – Intervention Boundary.md` |
| T03 | 세션 종료 3조건 동작 | `T03_ObjectiveCompleteEnd_WhenNoHigherPriorityReason` | `Assets/Tests/EditMode/IngameCoreSessionEndPersistenceTests.cs` | `[Category("GateMandatory")]` | `Assets/Specification/Ingame/CoreLoop/05 – Session End and Persistence.md` |
| T10 | 고정 Tick(`2s`) 유지(일시정지/복귀 후 Tick 연속성) | `T10_PauseResume_MaintainsTickIndexContinuity` | `Assets/Tests/EditMode/RoutineObservationMvpSessionTests.cs` | `[Category("GateMandatory")]` | `Assets/Specification/Ingame/CoreLoop/01 – Tick and Timebase.md` |
| T11 | 개입 대기열 Tick 경계 반영/충돌 처리 | `T11_ConflictingInterventions_UsesTieBreakOrderConsistently` | `Assets/Tests/EditMode/IngameCoreInterventionQueueTests.cs` | `[Category("GateMandatory")]` | `Assets/Specification/Ingame/CoreLoop/03 – Intervention Boundary.md` |
| T21 | 종료 영속화 최소 세트 저장 | `T21_PersistRetry_TransitionsToRetryThenSuccess` | `Assets/Tests/EditMode/IngameCoreSessionEndPersistenceTests.cs` | `[Category("GateMandatory")]` | `Assets/Specification/Ingame/CoreLoop/05 – Session End and Persistence.md` |
| T22 | 금지 전이 차단 | `T22_InvalidTransitionReject_StateMachineRejectsForbiddenTransition` | `Assets/Tests/EditMode/IngameCoreSimulationTests.cs` | `[Category("GateMandatory")]` | `Assets/Specification/Ingame/CoreLoop/02 – State Machine.md` |
| T23 | seed 재현성 보장 | `T23_DeterministicReplay_LogCoreFieldsMatchForSameSeedAndInput` | `Assets/Tests/EditMode/IngameCoreSimulationTests.cs` | `[Category("GateMandatory")]` | `Assets/Specification/Ingame/CoreLoop/10 – Observability and Replay.md` |

------

## Naming / Category Lock Rules

- Gate 필수 테스트 메서드명은 반드시 `T##_` 접두사를 가진다.
- Gate 필수 테스트는 반드시 `[Category("GateMandatory")]`를 가진다.
- Unity Test Runner 결과 파싱 시 우선순위는 아래 순서를 따른다.
  1. `[Category("GateMandatory")]`
  2. 메서드명/FullName의 `T##` 패턴
  3. 본 문서의 정적 매핑 테이블

------

## Runner Scope

- 현재 Gate 필수 테스트는 EditMode에 위치한다.
- PlayMode는 현재 Gate 필수 세트에 포함되지 않지만, 향후 추가 시 동일 규칙(`T##_` + `[Category("GateMandatory")]`)을 강제한다.
