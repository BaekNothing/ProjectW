# SSOT – Character Memory and Relationships

본 문서는 **외행성재척지원실 3과**의 캐릭터 기억과 관계 시스템에 대한 단일 진실원(Source of Truth)이다.

상위 규칙:

- `Assets/Specification/Ingame/SSOT – Ingame.md`
- `Assets/Specification/Ingame/SSOT – Characters.md`

본 문서는 캐릭터가 다른 캐릭터와 플레이어 관리자를 어떻게 기억하고, 그 기억이 관계와 행동에 어떻게 영향을 주는지를 정의한다.

------

## 1. Memory Premise

캐릭터는 다른 캐릭터를 기억할 수 있다.

이 기억은 완전한 인간적 회상이 아니라, 업무와 감정과 보고서가 뒤섞인 관리 가능한 흔적이다.
기억은 캐릭터를 더 풍부하게 만들지만, 플레이어가 모든 기억을 완전히 읽을 수 있어서는 안 된다.

기억의 목적:

- 같은 인력 배치가 매번 다르게 느껴지게 한다.
- 관계가 업무 결과에 영향을 주게 한다.
- 캐릭터 성장과 카드/퍽 획득의 근거를 만든다.
- 클론 폐기/재생성이 단순 리셋이 아니게 만든다.

------

## 2. Memory Types

기억은 다음 타입을 가진다.

### Work Memory

- 함께 처리한 업무
- 성공/실패 경험
- 누가 부담을 떠안았는지
- 누가 보고를 누락했는지

### Social Memory

- 도움, 배신, 무시, 칭찬, 공개 망신
- 면담에서 언급된 감정
- 팀 내 농담 또는 불편한 별명

### Manager Memory

- 플레이어가 누구를 믿었는지
- 플레이어가 누구를 버렸는지
- 플레이어가 보고서를 읽었는지 넘겼는지
- 플레이어가 AI 제안을 그대로 컨펌했는지

### Clone Memory

- 같은 계보의 이전 개체에서 남은 흔적
- 완전한 기억 이전이 아니라 성향, 습관, 공포, 이상한 익숙함으로 표현한다.
- 과하게 진지한 영혼 이전 서사가 되어서는 안 된다.

------

## 3. Memory Record

기억은 최소한 다음 필드를 가진다.

- `MemoryId`
- `OwnerPersonnelId`
  - 기억을 가진 캐릭터
- `TargetId`
  - 기억의 대상
  - 다른 캐릭터, 플레이어, 사장, AI, 특정 업무일 수 있다.
- `MemoryType`
- `Valence`
  - 긍정/부정 방향
- `Intensity`
  - 강도
- `Decay`
  - 시간에 따른 약화 정도
- `Tags`
- `SourceEventId`
- `DayCreated`
- `VisibleScope`
  - 플레이어가 이 기억을 보기 위해 필요한 정보 스코프

------

## 4. Relationship Model

관계는 기억의 누적 결과를 압축한 운영 상태다.

기본 축:

- `Trust`
  - 같이 일해도 된다고 느끼는 정도
- `Affinity`
  - 정서적 호감 또는 편함
- `Debt`
  - 빚졌다고 느끼는 정도
- `Resentment`
  - 원망 또는 억울함
- `Reliability`
  - 업무 파트너로 예상 가능한 정도

현재 구현의 `PersonnelRelationship.Trust`와 `Affinity`는 최소 구현이다.
향후 `Debt`, `Resentment`, `Reliability`를 확장할 수 있어야 한다.

------

## 5. Memory Creation

기억은 다음 상황에서 생성된다.

- 같은 업무에 함께 배치됨
- 고위험 업무를 단독으로 떠맡음
- 누군가의 카드 때문에 결과가 악화됨
- 누군가의 카드 때문에 결과가 좋아짐
- 보고 누락 또는 책임 회피가 발생함
- 플레이어가 개별 보고서를 검토함
- 플레이어가 보고서를 무시함
- 플레이어가 캐릭터를 폐기하거나 재생성함
- 사장 평가에서 특정 캐릭터가 언급됨

기억 생성 원칙:

- 모든 행동이 기억을 만들 필요는 없다.
- 기억은 플레이어 판단에 영향을 줄 정도의 사건에만 생성한다.
- 기억은 로그처럼 완전하지 않고, 감정적으로 왜곡될 수 있다.

공동 업무 기억:

- 같은 업무에 함께 배치된 캐릭터는 업무 결과와 별개로 관계 접점을 만든다.
- 모든 공동 배치가 별도 `CharacterMemory`를 만들지는 않지만, 관계 수치는 작은 폭으로 변할 수 있다.
- 다음 상황은 기억 생성 후보가 된다.
  - 고위험 업무를 함께 성공 또는 실패함
  - 한 캐릭터의 카드/퍽이 다른 캐릭터의 부담을 줄이거나 늘림
  - 한 캐릭터가 보고 누락, 책임 회피, 과잉 개입으로 다른 캐릭터에게 피해를 줌
  - 반복적으로 같은 조합이 배치되어 익숙함, 피로감, 의존, 불신이 누적됨
- 공동 업무 기억은 최소한 업무 id, 함께 배치된 대상, 결과 방향, 감정 강도, 관련 태그를 남긴다.
- 관계 변화는 양방향이 동일할 필요가 없다. A가 B를 신뢰하게 되었어도 B는 A에게 부담이나 원망을 느낄 수 있다.
- 기억은 `PersonnelId` 기준으로 기록하되, 클론 계보 해석이 필요한 경우 `CloneLineageId` 태그를 함께 남길 수 있다.

------

## 6. Memory Effects

기억은 다음에 영향을 줄 수 있다.

- 팀 배치 결과
- 행동 카드 드로우 가중치
- 퍽 획득 후보
- 관계 수치 변화
- 정보 스코프 상승 또는 하락
- 보고서 문장 톤
- 플레이어에 대한 신뢰
- 클론 재생성 후 남는 성향 샘플

예시:

- 함께 큰 사고를 막은 기억
  - Trust 상승
  - 협업 카드 등장 확률 증가
- 누군가가 책임을 떠넘긴 기억
  - Resentment 상승
  - 같은 팀 배치 시 리스크 증가
- 플레이어가 보고서를 계속 안 읽은 기억
  - TrustToManager 하락
  - AI 대체 압력과 별개로 관리자 불신 증가

------

## 7. Visibility

플레이어는 모든 기억을 볼 수 없다.

정보 스코프별 노출:

- `Surface`
  - 관계 수치의 매우 거친 표시만 가능
- `Working`
  - 최근 기억의 태그 일부 표시
- `Trusted`
  - 강한 기억의 원인과 대상 일부 표시
- `Compromised`
  - 많은 기억이 보이지만 해석이 왜곡되거나 과몰입을 유발할 수 있음

원칙:

- 기억은 플레이어에게 완전한 진실로 제공되지 않는다.
- 같은 사건도 캐릭터마다 다르게 기억할 수 있다.
- AI 요약은 기억의 감정적 맥락을 누락할 수 있다.

------

## 8. Relationship Effects on Assignment

팀 배치에서 관계는 다음 방식으로 작동한다.

- 높은 Trust
  - 협업 안정성 증가
  - 보고 누락 감소
- 높은 Affinity
  - 정신 비용 감소 가능
  - 과도하면 편들기 또는 눈감아주기 가능
- 높은 Debt
  - 단기 협조 가능
  - 장기적으로 부담 또는 조작 가능성
- 높은 Resentment
  - 충돌, 책임 회피, 카드 악화 가능성
- 높은 Reliability
  - 결과 변동성 감소

관계는 단순 보너스가 아니다.
좋은 관계도 과하면 판단 왜곡이나 편파를 만든다.

공동 업무에 따른 관계 변화:

- 성공한 공동 업무
  - 서로의 기여가 명확하면 `Trust`와 `Reliability`가 오른다.
  - 한쪽의 기여만 과하게 보이면 다른 쪽의 `Debt` 또는 `Resentment`가 오를 수 있다.
- 실패한 공동 업무
  - 실패 원인이 불명확하면 양쪽 모두 `Trust`가 내려가거나 보고서가 왜곡될 수 있다.
  - 특정 캐릭터의 카드/퍽/상태가 원인으로 드러나면 대상에 대한 `Resentment`가 오른다.
- 반복 공동 배치
  - 안정적인 조합은 `Trust`와 `Reliability`를 만든다.
  - 지나친 반복은 의존, 편들기, 지루함, 특정 조합 없이는 불안해하는 상태를 만들 수 있다.
- 관계 변화는 업무 태그, 위험도, 검토 수준, 보고서 확인 여부, 정보 스코프에 의해 보정된다.
- 플레이어가 보고서를 검토하지 않으면 실제 원인과 캐릭터가 기억하는 원인이 어긋날 수 있다.

------

## 9. Clone Memory

재생성된 클론은 이전 개체의 기억을 완전히 보존하지 않는다.

재생성 기준:

- 재생성은 기존 `PersonnelId`의 연장이 아니라 같은 `CloneLineageId`에서 새 개체를 만드는 행위다.
- 재생성된 개체는 새 `PersonnelId` 또는 새 `Version`을 가진다.
- 이전 개체의 관계와 기억은 새 개체에게 그대로 복사하지 않는다.
- 이전 개체가 가진 `Relationships`와 `Memories`는 활성 런타임 관계에서 제거되거나 보관 이력으로 이동한다.
- 새 개체는 기본 캐릭터 데이터, 보존 허용 카드/퍽/특성 샘플, 계보 잔향만을 초기 상태로 받는다.

허용되는 잔존:

- 특정 태그에 대한 선호/회피
- 설명하기 어려운 익숙함
- 위험 습관
- 일부 TraitSample
- 특정 캐릭터에 대한 묘한 호감 또는 불편함

다른 캐릭터가 기억하는 재생성:

- 재생성 대상의 기억은 대부분 지워지지만, 주변 캐릭터의 기억은 자동으로 지워지지 않는다.
- 주변 캐릭터는 "이전 개체가 폐기/재생성되었다"는 사건을 기억할 수 있다.
- 주변 캐릭터의 기억은 새 개체를 완전히 같은 인물로 취급하지 않지만, 같은 `CloneLineageId`에 대한 경계, 불편함, 기대를 만들 수 있다.
- 따라서 재생성은 대상 캐릭터에게는 관계 리셋에 가깝지만, 조직 전체에는 리셋이 아니다.
- 플레이어가 재생성을 자주 사용하면 관리자 신뢰, 조직 분위기, AI 대체 압력, 사장 평가에 누적 흔적이 남는다.

금지되는 잔존:

- 이전 개체의 모든 사건을 정확히 기억하는 것
- 폐기 비용을 무력화하는 완전 기억 이전
- 클론을 영혼 불멸 서사로 만드는 것

------

## 10. Manager Memory

캐릭터는 플레이어의 관리 행동을 기억할 수 있다.

기억 대상:

- 보고서를 읽었는가
- 면담했는가
- AI 제안을 그대로 믿었는가
- 위험 업무를 누구에게 떠넘겼는가
- 폐기/재생성을 얼마나 쉽게 했는가
- 특정 캐릭터만 편애했는가

효과:

- `TrustToManager`
- 정보 스코프 변화
- 보고 누락 가능성
- 과잉 충성 또는 태업 카드 등장
- 사장 보고서의 인력 리스크 문장

------

## 11. Implementation Mapping

현재 코어 구현과의 매핑:

- `PersonnelRelationship`
  - 관계 최소 구현
- `Trust`
  - 관계 안정성의 최소 축
- `Affinity`
  - 정서적 호감의 최소 축
- `TruthFrame`
  - 사건의 객관적 기록 후보
- `VisibleLog`
  - 플레이어에게 보이는 왜곡 가능 기록

향후 구현 포트:

- `CharacterMemory`
- `MemoryType`
- `MemoryValence`
- `RelationshipDebt`
- `RelationshipResentment`
- `RelationshipReliability`
- 기억 기반 카드 드로우 보정
- 기억 기반 퍽 획득
- 정보 스코프별 기억 노출

------

## 12. Mutation Contract

기억과 관계는 게임 내 사건, 배치 결과, 면담, 보고 검토, 클론 처리에 의해 계속 변한다.
따라서 외부 시스템은 내부 레코드를 직접 수정하지 않고 변경 함수로만 변화를 주입한다.

필수 변경 함수:

- 기억
  - `AddMemoryRecord(memory)`
  - `RemoveMemory(memoryId)`
  - `SetMemoryStat(memoryId, stat, value)`
  - `AdjustMemoryStat(memoryId, stat, delta)`
- 관계
  - `SetRelationshipStat(targetPersonnelId, stat, value)`
  - `AdjustRelationshipStat(targetPersonnelId, stat, delta)`
  - `RemoveRelationship(targetPersonnelId)`
- 재생성
  - `ArchiveMemoriesForRegeneration(sourcePersonnelId, cloneLineageId)`
  - `ArchiveRelationshipsForRegeneration(sourcePersonnelId, cloneLineageId)`
  - `CreateRegeneratedClone(cloneLineageId, regenerationPolicy)`
  - `ApplyLineageResidue(targetPersonnelId, cloneLineageId, residue)`
  - `AddRegenerationMemory(observerPersonnelId, sourcePersonnelId, regeneratedPersonnelId)`

수치 규칙:

- 기억의 `Intensity`, `Decay`는 0~100 범위로 보정한다.
- 관계의 `Trust`, `Affinity`, `Debt`, `Resentment`, `Reliability`는 -100~100 범위로 보정한다.
- `Adjust` 계열 함수는 “좋아짐/나빠짐”을 모두 표현할 수 있어야 한다.
- 관계 수치 조정은 필요한 경우 대상 관계를 생성할 수 있다.
- 재생성은 기본적으로 새 개체의 관계 수치를 0 또는 base 정의값에서 시작하게 한다.
- 계보 잔향은 낮은 강도의 trait/memory hook으로만 주입하고, 원본 기억 전문을 복사하지 않는다.

데이터 제작 가이드:

- 새 기억/관계 축을 추가할 때는 enum key와 변경 함수를 함께 확장한다.
- UI, 이벤트 처리, 성장 시스템, AI 추천 시스템은 동일한 변경 인터페이스만 사용한다.
- 기억과 관계를 직접 수정하는 임시 코드는 테스트나 에디터 생성기를 제외하고 금지한다.

------

## 13. Prohibitions

다음은 금지한다.

- 관계를 단순 호감도 하나로 축소하는 것
- 기억을 완전하고 객관적인 로그로만 취급하는 것
- 모든 기억을 플레이어에게 무료로 공개하는 것
- 클론 재생성이 모든 기억과 관계를 깨끗하게 리셋하는 것
- 반대로 클론 재생성이 완전 기억 이전으로 작동하는 것
- 좋은 관계를 항상 순수 보너스로 만드는 것
- 나쁜 관계를 항상 순수 패널티로만 만드는 것
