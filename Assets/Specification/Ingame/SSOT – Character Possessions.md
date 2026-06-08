# SSOT – Character Possessions

본 문서는 **외행성재척지원실 3과**에서 캐릭터가 가질 수 있는 요소의 단일 진실원(Source of Truth)이다.

상위 규칙:

- `Assets/Specification/Ingame/SSOT – Ingame.md`
- `Assets/Specification/Ingame/SSOT – Characters.md`

본 문서는 행동 카드, 퍽, 덱, 특성 샘플의 베이스 설계를 정의한다.

------

## 1. Scope

캐릭터가 가질 수 있는 것은 다음 계층으로 나눈다.

- `ActionCard`
  - 하루 단위 업무 반응 카드
  - 아침에 덱에서 제시되는 주 행동 경향
- `Perk`
  - 지속 특성
  - 특정 조건에서 결과, 비용, 리스크, 정보 노출을 보정
- `Deck`
  - ActionCard의 묶음
  - 캐릭터 성장과 경험의 직접 표현
- `TraitSample`
  - 폐기/재생성 또는 성장 과정에서 보존 가능한 특성 샘플
  - 아직 완전한 Perk 또는 Card가 되기 전의 재료

------

## 2. Action Card Base

행동 카드는 캐릭터가 그날 업무를 대하는 방식이다.

필수 필드:

- `CardId`
- `OwnerPersonnelId`
- `Title`
- `VisibleSummary`
- `HiddenIntent`
- `Tags`
- `OutcomeModifier`
- `RiskModifier`
- `ReviewCostModifier`
- `CriticalChancePercent`
- `CriticalMultiplier`
- `MemoryHooks`

Card use rule:

- `OutcomeModifier` and `RiskModifier` are the visible low-point effect.
- On critical success, `CriticalMultiplier` adds extra positive effect: progress-increasing `OutcomeModifier` is multiplied, and risk-reducing `RiskModifier` is multiplied.
- Critical success does not amplify negative side effects such as risk-increasing `RiskModifier`.

선택 필드:

- `RequiredScope`
  - 이 카드를 제대로 읽기 위해 필요한 정보 스코프
- `BossReactionTags`
  - 특정 사장 유형이 좋아하거나 싫어하는 태그
- `GrowthHooks`
  - 결과 이후 카드 변형 또는 퍽 생성 후보

------

## 3. Action Card Families

초기 베이스 카드군은 다음을 포함한다.

### Baseline

- `Steady Work`
  - 무난히 처리한다.
  - 낮은 리스크, 낮은 보상.
- `Quiet Compliance`
  - 지시를 말없이 따른다.
  - 빠르지만 문제 제기가 줄어든다.

### Speed and Shortcut

- `Shortcut`
  - 속도와 가시적 성과를 올린다.
  - 숨은 리스크와 보고 누락 가능성이 오른다.
- `Patch First`
  - 임시 조치를 먼저 한다.
  - 긴급 업무에는 좋지만 사후 검토 비용이 오른다.

### Paperwork

- `Paper Trail`
  - 기록을 남긴다.
  - 검토 비용은 늘지만 후폭풍 리스크를 낮춘다.
- `Document Spiral`
  - 문서를 과잉 생산한다.
  - 사장 유형에 따라 유능함 또는 지연으로 평가된다.

### Social

- `Ask Around`
  - 주변 인물에게 확인한다.
  - 관계 기억을 생성하기 쉽다.
- `Blame Buffer`
  - 책임 소재를 흐린다.
  - 단기 생존에는 유리하지만 신뢰와 관계를 훼손한다.

### AI Interaction

- `AI Says Yes`
  - AI 제안을 쉽게 따른다.
  - 빠르지만 AI 대체 압력을 올릴 수 있다.
- `AI Skeptic`
  - AI 제안을 의심한다.
  - 검토 비용은 오르지만 예외 발견 가능성이 생긴다.

### Risk and Drama

- `Hero Move`
  - 큰 보상을 노리고 과감히 움직인다.
  - 성공 시 강한 성장, 실패 시 큰 후폭풍.
- `Silent Fix`
  - 보고 없이 해결한다.
  - 결과가 좋으면 편하지만 기억/보고 공백을 만든다.
- `Panic Loop`
  - 같은 확인을 반복한다.
  - 리스크 감지는 가능하지만 시간과 정신 비용이 커진다.

------

## 4. Card Design Rules

- 카드에는 최소 하나의 장점과 하나의 관리 비용이 있어야 한다.
- 카드가 항상 정답이 되어서는 안 된다.
- 위험 카드는 단순 실패 벌칙이 아니라 사건, 기억, 성장의 씨앗이어야 한다.
- 카드는 플레이어가 배치 판단을 하게 만들어야 한다.
- 카드 텍스트는 완전한 정답이 아니라 해석 가능한 신호여야 한다.
- 정보 스코프가 낮을수록 `HiddenIntent`와 `MemoryHooks`는 보이지 않거나 왜곡된다.

------

## 5. Perk Base

퍽은 캐릭터에게 지속되는 특성이다.

필수 필드:

- `PerkId`
- `Title`
- `TriggerTags`
- `OutcomeModifier`
- `RiskModifier`
- `PhysicalCostModifier`
- `MentalCostModifier`
- `ReviewCostModifier`
- `MemoryModifier`
- `Note`

선택 필드:

- `BossPreference`
- `RelationshipEffect`
- `ClonePersistence`
  - 폐기/재생성 후 계보에 남을 수 있는지

------

## 6. Perk Families

초기 베이스 퍽군은 다음을 포함한다.

### Competence

- `Procedure Anchor`
  - 절차/감사/문서 업무에서 불일치를 줄인다.
  - 정신 비용이 증가할 수 있다.
- `Signal Reader`
  - 센서, 로그, 이상 신호를 빨리 읽는다.
  - 모호한 상황에서 과민 반응할 수 있다.
- `Field Bypass`
  - 현장 복구와 긴급 대응이 빠르다.
  - 절차 공백과 보고 누락 가능성이 있다.

### Social Handling

- `Complaint Weaver`
  - 여러 민원을 처리 가능한 묶음으로 만든다.
  - 책임 소재를 흐릴 수 있다.
- `Mood Thermometer`
  - 팀 분위기를 잘 읽는다.
  - 관계 피로를 더 많이 받는다.

### Dysfunction

- `Paper Hoarder`
  - 기록은 많이 남긴다.
  - 검토 비용과 지연이 커진다.
- `Credit Leech`
  - 성공을 자신의 공로로 흡수한다.
  - 사장에게는 좋아 보일 수 있으나 동료 기억을 악화시킨다.
- `Risk Blindspot`
  - 특정 태그의 위험을 반복적으로 낮게 본다.
  - 같은 계보에서 재발할 수 있다.

### AI Attitude

- `Prompt Whisperer`
  - AI 요약과 제안을 잘 활용한다.
  - AI 대체 압력 증가와 맞물릴 수 있다.
- `Manual Loyalist`
  - 사람의 판단과 원문 검토를 선호한다.
  - 검토 비용은 크지만 예외 탐지에 강하다.

------

## 7. Deck Composition

기본 덱 원칙:

- 초기 덱은 3~5장의 행동 카드로 시작한다.
- 최소 1장은 안정 카드여야 한다.
- 최소 1장은 리스크 또는 비용을 가진 카드여야 한다.
- 덱이 커질수록 캐릭터는 풍부해지지만 예측이 어려워진다.

덱 성장 원칙:

- 성공은 효율 또는 자신감 계열 카드를 추가할 수 있다.
- 실패는 회피, 경계, 보고 왜곡, 과잉 검토 카드를 추가할 수 있다.
- 반복 업무는 숙련 카드와 무료함 카드가 함께 생길 수 있다.
- 특정 관계 기억은 사회적 카드의 추가 또는 변형을 유발할 수 있다.

------

## 8. Trait Samples

`TraitSample`은 완전한 카드나 퍽이 되기 전의 보존 가능한 흔적이다.

생성 조건:

- 큰 성공
- 큰 실패
- 반복된 업무 태그
- 강한 관계 기억
- 폐기 전 마지막 사건
- 사장 평가에 크게 걸린 사건

사용처:

- 재생성 시 일부 계보 보정
- 새 카드 후보
- 새 퍽 후보
- 사장 보고서의 소재
- AI 대체 압력 판단 근거

------

## 9. Implementation Mapping

현재 코어 구현과의 매핑:

- `ActionCard`
  - 행동 카드의 최소 구현
- `Personnel.Deck`
  - 캐릭터가 가진 행동 카드 묶음
- `PersonnelPerk`
  - 지속 특성의 최소 구현
- `EventCase.PerkTags`
  - 업무 카드와 퍽을 연결하는 태그
- `MorningCards`
  - 하루 시작 시 뽑힌 카드

향후 구현 포트:

- `TraitSample`
- 카드 희귀도
- 카드 변형 이력
- 퍽 획득 규칙
- 정보 스코프별 카드 노출 규칙
- 기억 기반 카드/퍽 생성 규칙

------

## 10. Mutation Contract

캐릭터가 보유한 카드, 퍽, 특성 샘플은 직접 컬렉션을 수정하지 않고 변경 함수로만 다룬다.

필수 변경 함수:

- 카드
  - `AddCard(card)`
  - `RemoveCard(cardId)`
- 퍽
  - `AddPerk(perk)`
  - `RemovePerk(perkId)`
- 특성 샘플
  - `AddTraitSample(sample)`
  - `RemoveTraitSample(traitSampleId)`
  - `AdjustTraitSampleStrength(traitSampleId, delta)`

설계 규칙:

- 추가 함수는 중복 ID를 방지한다.
- 제거 함수는 단건 ID를 기준으로 호출한다.
- 수치 변경 함수는 내부에서 최소/최대 범위를 보정한다.
- 외부 시스템은 변경 결과를 받아 후속 로그, UI, 성장 연출, 밸런스 검증에 사용할 수 있어야 한다.

데이터 제작 가이드:

- 새 보유 데이터 타입을 만들 때는 읽기 인터페이스와 변경 인터페이스를 함께 정의한다.
- 데이터 원형은 ScriptableObject로 생산하되, 인게임 변화는 변경 함수로만 주입한다.
- “직접 리스트를 꺼내서 수정하는 방식”은 제작 편의가 아니라 시스템 누수로 취급한다.

------

## 11. Prohibitions

다음은 금지한다.

- 카드와 퍽을 단순 능력치 보너스로만 만드는 것
- 위험 카드가 아무런 서사/기억/성장 흔적 없이 실패만 만드는 것
- 좋은 퍽이 항상 좋은 결과만 내는 것
- 덱 성장이 플레이어 판단을 줄이는 방향으로만 가는 것
- 폐기/재생성으로 카드와 퍽 리스크를 완전히 지우는 것
