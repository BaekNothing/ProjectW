# 13 – Character Neuron Visualization Feasibility

## 목적

현재 문제의식은 다음 2가지로 요약된다.

1. 각 캐릭터가 지금 무엇을 하려는지/왜 그렇게 결정했는지 직관적으로 보이지 않는다.
2. 행동 원리를 조정하려고 할 때 수정 지점이 분산되어 있어 변경 비용이 높다.

본 문서는 현재 코드 구조에서 "캐릭터 뉴런 시각화(클릭 시 팝업 UI)"가 가능한지 선검토하고, 필요 시 구조 변경 범위를 제안한다.

---

## 결론 요약

- **구현 자체는 현재 구조에서도 가능**하다.
  - 이미 캐릭터 오브젝트에 2D 콜라이더가 붙고, Tick마다 의사결정이 계산되며, 결정 로그 문자열도 생성되고 있다.
- 다만 **재사용 가능한 UI 패널(데이터/뷰모델/뷰 분리)** 수준까지 가려면 중간 리팩터링이 사실상 필요하다.
- 특히 행동결정 코드가 `RoutineObservationMvpSession` 단일 클래스에 집중되어 있어,
  - "결정 근거 데이터(뉴런 입력/가중치/최종 선택)"를 구조화된 형태로 외부로 빼는 작업이 1차 선행되어야 한다.

---

## 현재 구조 진단

### 1) 행동결정 로직의 위치

- 행동결정은 `RoutineObservationMvpSession` 내부에서 수행된다.
- 핵심 메서드:
  - `ResolveCharacterAction(...)`: 스케줄/배고픔/스트레스/시간대를 근거로 `RoutineActionType` 결정.
  - `ResolveLatchedOrNewNeedAction(...)`: Need 액션 래치(고정) 처리.
  - `ResolveNeedLatchAfterAction(...)`: Need 해소 후 래치 해제.
  - `ApplyNeedsAndProgress(...)`: 행동 결과를 상태값에 반영.
- 즉, "결정"과 "실행"과 "UI 갱신"이 같은 클래스에 묶여 있다.

### 2) 관찰 가능성(Observability)

- 현재도 `LogDecision(...)`를 통해 풍부한 문자열 로그를 남긴다.
- 하지만 로그가 구조화된 데이터 객체가 아니라 문자열 중심이라,
  - UI 패널에서 바로 바인딩하기 어렵고,
  - 나중에 필드 추가/정렬/필터링 시 유지보수 비용이 높다.

### 3) 클릭 기반 확장 가능성

- 캐릭터에 `CapsuleCollider2D`가 부착되고 있어 클릭 선택의 물리적 기반은 이미 존재한다.
- 따라서 클릭 → 선택 캐릭터 식별 → 패널 표시 플로우를 추가하는 것은 기술적으로 무리 없다.

### 4) 코어 시뮬레이션과 MVP의 분리 상태

- `IngameCore/Simulation`에도 `AgentRuntimeState`, `SimulationTickEngine`가 별도로 존재한다.
- 이 경로에도 초과근무/번아웃/만복도/행복도 등의 결정 요소가 있다.
- 현재 MVP 루틴(`RoutineObservationMvpSession`)과 Core Simulation이 완전히 단일 소스로 통합되어 있지는 않다.
- 따라서 "뉴런 패널"의 기준 엔진을 무엇으로 볼지 먼저 결정해야 한다.
  - 단기: MVP 루틴 기준.
  - 중기: Core Simulation 기준으로 일원화.

---

## 요구사항 관점 feasibility

### 요구사항 A: "각 캐릭터의 뉴런 시각화"

가능. 단, 뉴런의 정의를 먼저 명시해야 한다.

권장 뉴런 모델(예시):

- 입력 뉴런(Input)
  - 시간대(근무/식사/수면 윈도우)
  - hunger/sleep/stress 현재값
  - 임계치 대비 여유도
  - 이동중 여부, 도착 여부
  - (Core 경로 선택 시) burnout/loadRatio/deadlinePressure
- 평가 뉴런(Eval)
  - Scheduled meal/sleep rule
  - Need latch 상태
  - 룰 블락 여부(예: zone 미도달)
- 출력 뉴런(Output)
  - intendedAction
  - currentAction
  - 선택 reason 코드

### 요구사항 B: "캐릭터 클릭 시 UI 팝업"

가능. 현재 구조에서 가장 낮은 비용 흐름:

1. 캐릭터 선택 컴포넌트(2D Raycast)
2. 선택된 캐릭터의 최신 Decision Snapshot 조회
3. 패널 Presenter/ViewModel 갱신
4. UI Panel show/hide

### 요구사항 C: "UI 패널 재사용성(데이터/뷰모델 분리)"

가능하지만, 여기서부터는 리팩터링 필요.

- 최소 분리 단위:
  - `DecisionSnapshot` (순수 데이터 DTO)
  - `INeuronDataProvider` (세션/엔진에서 스냅샷 제공)
  - `NeuronPanelViewModel` (표시용 정규화/포맷팅)
  - `NeuronPanelView` (UGUI/TMP 렌더링)
- 기존 `RoutineObservationMvpSession`는 Provider 역할만 담당하도록 축소하는 것이 바람직.

---

## 구조 변경 필요 범위 (권장안)

## Option 1. "MVP 한정" 최소 변경 (빠름)

### 변경 범위

- `RoutineObservationMvpSession`에 구조화된 결정 스냅샷 생성 추가
- 캐릭터 선택 컨트롤러(클릭 감지) 추가
- 뉴런 패널 ViewModel/View 추가

### 장점

- 구현 속도 빠름
- 기존 루프 로직 영향 작음

### 단점

- 결정 로직이 여전히 `RoutineObservationMvpSession`에 집중
- 이후 Core Simulation 통합 시 재작업 가능성

### 예상 작업량

- 소~중 (파일 4~7개 추가/수정)

---

## Option 2. "결정 엔진 추출" 중간 리팩터링 (권장)

### 변경 범위

- 의사결정만 별도 서비스로 추출
  - 예: `RoutineDecisionEngine.Evaluate(binding, tickContext)`
- 반환형을 `DecisionSnapshot` + `DecisionEvaluation` 형태로 표준화
- `RoutineObservationMvpSession`는 tick orchestration과 이동/연출 중심으로 축소
- UI는 엔진 출력 데이터를 그대로 소비

### 장점

- 행동원리 튜닝 포인트가 명확해짐
- 테스트 작성 용이(결정 엔진 단위테스트)
- 재사용 UI 요구와 가장 잘 맞음

### 단점

- 초기 리팩터링 비용이 Option1보다 큼

### 예상 작업량

- 중 (파일 8~14개 수준 수정/추가, 테스트 보강 권장)

---

## Option 3. "Core Simulation 일원화" 큰 변경

### 변경 범위

- MVP 루틴 의사결정을 Core Simulation(`SimulationTickEngine`/`AgentRuntimeState`) 중심으로 통합
- 시각화는 Core의 공식 Decision Trace를 참조

### 장점

- 장기적으로 단일 진실원(SSOT) 확보

### 단점

- 범위 큼, 일정 리스크 큼
- 기존 MVP 연출 코드와 접합 비용 발생

### 예상 작업량

- 대 (시스템 레벨 리팩터링)

---

## 추천 실행 순서

1. **Option 2 기준으로 착수** (결정 엔진 추출 + 스냅샷 표준화)
2. 첫 UI는 디버그 패널 형태로 빠르게 띄워 사용성 검증
3. 검증 후 UGUI/TMP 정식 패널로 교체
4. 이후 Core 통합 여부를 별도 마일스톤으로 분리

---

## 제안 데이터 계약 (초안)

```csharp
public sealed class CharacterDecisionSnapshot
{
    public string CharacterId;
    public int Tick;
    public RoutineActionType ScheduledAction;
    public RoutineActionType IntendedAction;
    public RoutineActionType CurrentAction;
    public string DecisionReasonCode;

    public float Hunger;
    public float Sleep;
    public float Stress;
    public float HungerThreshold;
    public float SleepThreshold;
    public float StressThreshold;

    public bool IsScheduledMeal;
    public bool IsScheduledSleep;
    public bool IsHungry;
    public bool IsStressed;
    public bool HasNeedLatch;
    public RoutineActionType LatchedNeedAction;

    public string TargetZoneId;
    public bool IsMoving;
    public bool CanResolveNeed;
}
```

이 스냅샷 하나만 있으면 패널, 로그, 리플레이에 공통 재사용이 가능하다.

---

## 리스크 및 완화

- 리스크: 현재 rule이 if-else 체인 중심이라 "뉴런" 느낌의 가중치 모델과 표현이 어색할 수 있음.
  - 완화: 1단계는 rule-node 시각화(조건 노드 + 결과 노드)로 정의.
- 리스크: 로그 문자열과 스냅샷 이중관리.
  - 완화: 스냅샷을 원천으로 로그 문자열 생성하도록 단일화.
- 리스크: 클릭 선택이 카메라/레이어 설정에 따라 불안정.
  - 완화: 전용 Layer + Physics2DRaycaster + 선택 하이라이트 추가.

---

## 최종 판단

- 질문한 범위는 **현 구조에서 구현 가능**하다.
- 다만 "재사용 가능한 뉴런 UI"와 "행동원리 조정 용이성"을 동시에 달성하려면,
  - 최소한 결정 데이터를 `RoutineObservationMvpSession` 밖으로 노출/표준화하는 **중간 리팩터링(Option 2)** 이 필요하다.
- 즉, "UI 먼저"보다 "결정 스냅샷 계약 먼저"가 실패 확률이 낮다.
