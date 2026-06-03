# SSOT – Work

본 문서는 **외행성재척지원실 3과**의 업무 시스템에 대한 단일 진실원이다.

업무는 플레이어가 인력과 카드를 배치하는 대상이며, 검토 비용과 불완전 정보, 캐릭터 카드/퍽 상호작용을 실제 선택으로 만드는 핵심 단위다.

상위 규칙:

- `Assets/Specification/Ingame/SSOT – Ingame.md`
- `Assets/Specification/Ingame/SSOT – Character Possessions.md`
- `Assets/Specification/Ingame/SSOT – Characters.md`

------

## 1. Scope

업무 시스템은 다음을 정의한다.

- 업무의 기본 데이터 구조
- 업무 원형과 런타임 업무 인스턴스의 차이
- 중요도, 양, 리스크, 태그, 동시작업 가능수
- 캐릭터 카드/퍽과의 상호작용 기준
- 난이도와 조건에 따른 동적 생성 규칙
- 업무 생성 확률과 출현 조건
- AI 업무 배치 초안이 참고해야 하는 정보 범위

------

## 2. Core Concept

업무는 단순한 퀘스트가 아니다.

업무는 다음 질문을 플레이어에게 던지는 관리 대상이다.

- 지금 이 일을 누가 맡아야 하는가?
- 이 일을 얼마나 검토해야 하는가?
- 리스크를 줄이기 위해 비용을 쓸 것인가?
- AI 초안을 그대로 믿을 것인가?
- 어떤 캐릭터 카드/퍽이 이 업무와 맞물려 폭발하거나 완충될 것인가?
- 일이 많을 때 무엇을 포기할 것인가?

업무 하나는 항상 결과와 후폭풍 가능성을 함께 가진다.
업무가 무난히 끝나도 관계, 기억, 보고서, AI 대체 압력, 보스 평가에 흔적을 남길 수 있다.

------

## 3. Work Definition and Work Instance

업무 데이터는 두 층으로 나눈다.

### WorkDefinition

업무의 원형이다.

예시:

- 산소 라인 수동 우회
- 하층 거주구역 민원 묶음 처리
- 식량 합성기 보고서 backlog
- AI 제안서 검토
- 클론 베이 재생성 승인서

`WorkDefinition`은 ScriptableObject 또는 동등한 데이터 에셋으로 생산 가능해야 한다.
동적 생성기는 `WorkDefinition`을 후보 풀로 사용한다.

### WorkInstance

실제 하루에 큐에 올라온 업무다.

`WorkInstance`는 `WorkDefinition`에서 파생되지만, 생성 시점의 난이도, 보스 성향, 조직 상태, 이전 실패, 인력 상태, AI 대체 압력에 따라 수치가 달라질 수 있다.

예시:

- 같은 "산소 라인 수동 우회"라도 초반에는 낮은 리스크 사건일 수 있다.
- 후반에는 같은 원형이 "인력 피로 누적 + 보스가 속도 압박 + AI 초안 생략" 조건으로 고위험 업무가 될 수 있다.

------

## 4. Required Fields

업무는 최소한 아래 필드를 가진다.

### Identity

- `WorkId`
  - 업무 원형 또는 인스턴스를 식별하는 id
- `Title`
- `Kind`
  - 예: `incident`, `complaint`, `routine`, `audit`, `hiring`, `clone`, `boss`, `ai`
- `Subsystem`
  - 예: `O2`, `HAB`, `FOOD`, `AI`, `CLONE`, `HR`, `LEGAL`

### Management Weight

- `Importance`
  - 조직/보스/엔딩 평가에서 얼마나 중요한가
  - 높을수록 실패 시 보스 평가와 AI 대체 압력에 큰 영향을 준다.
- `Volume`
  - 업무량
  - 인력 부하, 소요 시간, 동시작업 요구량에 영향을 준다.
- `Risk`
  - 표면 리스크
  - 플레이어가 기본 정보만으로 볼 수 있는 위험도다.
- `LatentRisk`
  - 잠복 리스크
  - 검토하지 않으면 드러나지 않는 후폭풍 가능성이다.
- `Urgency`
  - 시간 압박
  - 방치 시 리스크 상승, TTL 감소, 업무 변질에 영향을 준다.

### Assignment Shape

- `RequiredAptitudes`
  - 요구 적성
  - 예: `observation`, `dexterity`, `boldness`, `intuition`, `logic`
- `RecommendedPersonnelCount`
  - 권장 투입 인원
- `MinPersonnelCount`
  - 최소 투입 인원
- `MaxPersonnelCount`
  - 최대 투입 인원
- `ConcurrentSlotCost`
  - 동시에 얼마나 많은 작업 슬롯을 점유하는가
- `ConcurrentLimit`
  - 이 업무가 동시에 병렬 처리될 수 있는 최대 분할 수
  - 1이면 한 팀만 맡을 수 있고, 2 이상이면 여러 캐릭터 또는 팀이 부분 작업을 병렬 수행할 수 있다.

### Interaction

- `Tags`
  - 카드/퍽/보스/보고서와 상호작용하는 핵심 태그
  - 예: `repair`, `procedure`, `paper`, `complaint`, `audit`, `ai`, `clone`, `shortcut`, `mismatch`, `emergency`
- `PerkTags`
  - 퍽 발동 조건으로 쓰는 태그
  - 구현에서는 `Tags`와 통합할 수 있지만, 설계상 "퍽 발동용 태그"는 명확히 분리 가능해야 한다.
- `CardHooks`
  - 특정 카드 계열이 결과를 바꾸는 연결점
- `BossReactionTags`
  - 보스 유형이 평가할 때 참고하는 태그
- `MemoryHooks`
  - 완료 후 캐릭터 기억/관계에 남길 수 있는 이벤트 태그

### Review Surface

- `VisibleSummary`
  - 기본 정보
- `HiddenFacts`
  - 검토, 로그, 면담, 보고서 읽기로만 드러나는 정보
- `ReviewCostProfile`
  - 이 업무를 검토하는 데 드는 시간/자원/집중력 비용
- `InformationScopeRequirement`
  - 특정 정보를 보기 위해 필요한 친밀도/권한/검토 단계

------

## 5. Dynamic Generation

업무는 기본적으로 스크립트에 의해 동적으로 생성될 수 있어야 한다.

동적 생성기는 다음 입력을 사용한다.

- 현재 Day
- 난이도
- 보스 유형
- 보스 이벤트 압력
- AI 대체 압력
- 누적 감사 평가 방향
- 인력 부족도
- 전날 실패/미검토 업무
- 누적 잠복 리스크
- 캐릭터 상태
- 클론 베이 상태
- 현재 콘텐츠 풀의 잠금 해제 상태

생성기는 `WorkDefinition` 후보 풀에서 조건을 만족하는 항목만 추린 뒤, 가중치 기반으로 `WorkInstance`를 만든다.

### Boss Event to Work

보스 이벤트는 상위 사건이며, 업무 생성기는 이를 하루 큐에서 처리 가능한 업무 인스턴스로 분해한다.

보스 이벤트가 만들 수 있는 업무 예시:

- `boss`
  - 사장의 직접 지시, 우선순위 변경, 설명 요구
- `audit`
  - 전일 선택, 미검토 보고서, AI와 다른 판단에 대한 감사
- `ai`
  - AI 자동화 제안, 대체 압력 상승, AI 요약 재검증
- `emergency`
  - 보스의 무리한 압박이나 방치된 잠복 리스크가 만든 긴급 업무
- `morale`
  - 인력 신뢰, 정서, 티타임 단서와 연결되는 업무
- `legal`
  - Alert, 책임 소재, 기록 보존과 연결되는 업무

보스 이벤트는 한 개 업무로만 나타날 필요가 없다.
하나의 보스 이벤트는 즉시 업무, 후속 감사 업무, 보고서 검토 요구, 잠복 리스크 증가를 동시에 만들 수 있다.

------

## 6. Spawn Weight

각 `WorkDefinition`은 등장확률을 가진다.

필수 필드:

- `BaseSpawnWeight`
  - 기본 등장 가중치
- `DifficultyWeightCurve`
  - 난이도별 보정
- `DayWeightCurve`
  - 날짜/진행도별 보정
- `BossWeightModifiers`
  - 보스 유형별 보정
- `ConditionModifiers`
  - 조직 상태, 이전 사건, 인력 상태에 따른 보정
- `EvaluationWeightModifiers`
  - AI 기본안 대비 플레이어 평가가 나쁜 방향으로 누적될 때 감사, AI, 보스 업무의 가중치를 높일 수 있다.

예시:

- `complaint` 업무는 초반에도 자주 등장한다.
- `clone` 업무는 클론 폐기/재생성 이후 등장확률이 오른다.
- `audit` 업무는 잠복 리스크가 높거나 보고서 미검토가 누적될수록 등장확률이 오른다.
- `emergency` 업무는 난이도와 전날 실패에 따라 등장확률이 오른다.
- `ai` 업무는 AI 대체 압력이 높을수록 자주 등장한다.
- `boss` 업무는 보스 이벤트 압력이 높거나 Alert가 누적될수록 자주 등장한다.

최종 등장 가중치 예시:

```text
FinalWeight =
  BaseSpawnWeight
  × DifficultyModifier
  × DayModifier
  × BossModifier
  × ConditionModifier
  × CooldownModifier
```

가중치는 확률 그 자체가 아니라 후보군 안에서의 상대값이다.

------

## 7. Difficulty Scaling

난이도는 업무의 수치와 구성에 영향을 준다.

난이도가 오르면 다음이 증가할 수 있다.

- `Importance`
- `Volume`
- `Risk`
- `LatentRisk`
- `Urgency`
- `RequiredAptitudes`
- `ConcurrentSlotCost`
- `ReviewCostProfile`
- `HiddenFacts`의 양
- 실패 후 후속 업무 생성 확률

난이도가 올라도 단순히 숫자만 커져서는 안 된다.
높은 난이도는 더 많은 선택 압박을 만들어야 한다.

예시:

- 업무량은 낮지만 잠복 리스크가 큰 업무
- 중요도는 낮지만 관계 기억을 망가뜨리는 업무
- AI 초안이 좋아 보이지만 특정 보스에게 치명적인 업무
- 한 명에게 맡기면 빠르지만 과부하를 만드는 업무
- 둘 이상에게 맡기면 관계 충돌 가능성이 생기는 업무

------

## 8. Tags and Interaction Rules

태그는 카드와 퍽이 업무를 읽는 언어다.

태그 설계 원칙:

- 태그는 결과 수치뿐 아니라 검토 비용, 기억, 보고서, 보스 반응에도 영향을 줄 수 있어야 한다.
- 하나의 업무에는 여러 태그가 붙을 수 있다.
- 태그는 "정답 캐릭터"를 고정하기보다, 관리 선택의 경향을 만든다.
- 같은 태그라도 카드/퍽에 따라 장점과 부작용이 함께 발생해야 한다.

기본 태그군:

- `repair`
- `procedure`
- `paper`
- `review`
- `audit`
- `complaint`
- `relation`
- `records`
- `ai`
- `clone`
- `hiring`
- `emergency`
- `routine`
- `mismatch`
- `shortcut`
- `boss`
- `legal`
- `morale`

------

## 9. Concurrency

업무는 동시작업 가능수를 가질 수 있다.

동시작업 관련 필드:

- `ConcurrentLimit`
  - 몇 개의 병렬 작업으로 나눌 수 있는가
- `ConcurrentSlotCost`
  - 조직의 작업 슬롯을 얼마나 차지하는가
- `SplitPenalty`
  - 여러 인원/팀으로 나눌 때 생기는 조율 비용
- `SoloPenalty`
  - 혼자 맡길 때 생기는 위험
- `CoordinationTags`
  - 관계/커뮤니케이션/문서화와 관련된 보정 태그

예시:

- `routine paperwork`
  - 병렬 처리 가능
  - 여러 명이 하면 빠르지만 검토 비용이 늘 수 있다.
- `emergency repair`
  - 병렬 처리 제한
  - 한 명이 빨리 처리할 수 있지만 고위험이다.
- `complaint bundle`
  - 여러 명이 나눌 수 있지만 관계/책임 분산 문제가 생긴다.
- `audit`
  - 병렬 처리하면 정보 누락 가능성이 오른다.

------

## 10. Runtime Mutation

생성된 업무는 고정된 카드가 아니다.

업무 인스턴스는 다음 상황에서 변할 수 있다.

- 시간이 지나 TTL이 줄어든다.
- 방치되면 `Urgency`, `Risk`, `LatentRisk`가 오른다.
- 검토하면 `HiddenFacts`가 일부 드러난다.
- AI 초안을 그대로 컨펌하면 일부 위험이 숨겨진 채 실행될 수 있다.
- AI 기본안과 다른 플레이어 선택은 감사 후보가 될 수 있다.
- Alert하면 보스 반응, 감사 가능성, 책임 소재 태그가 바뀔 수 있다.
- 카드/퍽이 `Risk`, `Outcome`, `ReviewCost`, `MemoryHooks`를 바꿀 수 있다.
- 실패 또는 과잉 성공은 후속 업무를 생성할 수 있다.

------

## 11. Implementation Mapping

현재 `CaseReview` 구현과의 대응:

- `EventCase`
  - 현재 런타임 업무 인스턴스에 해당한다.
- `Urgency`, `Severity`
  - 현재 구현의 중요도/긴급도 계열 수치다.
- `PhysicalCost`, `MentalCost`
  - 업무량과 인력 부하의 초기 구현이다.
- `LatentRisk`
  - 잠복 리스크다.
- `RequiredAptitudes`
  - 요구 적성이다.
- `PerkTags`
  - 카드/퍽 상호작용 태그의 초기 구현이다.
- `BaseSuccessChance`
  - 업무 원형의 기본 성공 난이도다.

추후 구현 필요:

- `WorkDefinition` ScriptableObject
- `WorkInstance` 또는 `EventCase` 확장
- 업무 생성 풀
- 조건 기반 spawn weight
- 난이도별 수치 변형
- 동시작업 가능수와 슬롯 비용
- 업무별 렌더링 리소스
- 보스 이벤트 압력과 감사 평가 방향을 생성 컨텍스트에 반영
- AI 기본안 대비 평가 결과에서 감사/후속 업무 생성

------

## 12. Prohibitions

다음은 금지한다.

- 업무를 단순 성공률 숫자만 가진 퀘스트로 만드는 것
- 난이도 상승을 단순 수치 증가로만 처리하는 것
- 태그를 단순 분류 라벨로만 쓰고 카드/퍽/기억/보스 반응과 연결하지 않는 것
- 모든 업무를 고정 수작업 데이터로만 만들고 동적 생성을 막는 것
- 동시작업 가능수를 인력 수 제한과 혼동하는 것
- 검토 비용 없이 모든 업무의 숨은 정보를 공개하는 것
- 보스 이벤트를 업무 큐에 아무 영향 없는 배경 텍스트로만 처리하는 것
- 감사 업무를 실패한 업무에만 생성하고 AI 대비 선택 차이를 무시하는 것
