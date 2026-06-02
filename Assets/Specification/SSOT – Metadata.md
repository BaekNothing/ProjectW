# SSOT – Metadata

본 문서는 Project_W에서 생성·관리되는 모든 메타데이터의 단일 진실원(Source of Truth) 이다.

`Project_W`는 개발 코드명이며, 현재 게임의 작업 제목은 **외행성재척지원실 3과**다.

여기서 말하는 메타데이터란, 플레이 결과나 규칙 그 자체가 아니라,
그 결과와 규칙을 식별·비교·추적하기 위해 부여되는 정보 전반을 의미한다.

본 문서는 규칙 문서이며,
본 문서를 수정하지 않는 한 어떤 사례도 규칙으로 승격되지 않는다.

------

## Scope

다음 항목들은 반드시 본 문서의 규칙을 따른다.

- 캐릭터 Version / Snapshot 식별 규칙
- 세션 ID, 분기 ID, 되감기 ID 체계
- 빌드 식별자(Build ID) 및 배포 단위
- 리소스 팩, 번들, 데이터 스키마 버전
- 데이터 Export/Import 단위 식별
- 개체 행동 덱과 카드 획득 이력
- 친밀도 기반 정보 스코프 단계
- 클론 폐기/재생성 이력
- 사장 유형 및 AI 대체 압력 기록
- 검토 비용과 플레이어 판단 로그

------

## Non-Goals

- 특정 사례의 결과 정당화
- 규칙 외부 로그로부터의 규칙 추론

------

## Required Metadata Families

향후 구현은 다음 메타데이터 군을 추적 가능하게 설계한다.

- `PersonnelId`: 개체 식별자
- `CloneLineageId`: 폐기/재생성 계보
- `DeckId`: 개체 행동 덱 식별자
- `CardId`: 행동 카드 또는 업무 카드 식별자
- `AffinityScope`: 친밀도 기반 정보 스코프
- `BossArchetype`: 사장 유형
- `ReplacementPressure`: AI 대체 압력
- `ReviewCost`: 검토 행동 비용
