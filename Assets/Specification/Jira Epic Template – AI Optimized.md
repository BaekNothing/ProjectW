# Work Item Template – AI Optimized

본 문서는 Project_W에서 사용하는 **작업 단위 작성 템플릿의 기준 정의**다.

이 템플릿은 사람이 보기 좋기보다,
AI(GPT)가 단일 문서만으로도 프로젝트 맥락과 관리 의도를 복원할 수 있도록 설계되었다.

------

## Work Item Summary

- 형식: [PW][Phase/Goal] 간결한 목적 서술
- 예시: [PW][Vertical Slice] Core Loop 구현

요약은 기능 나열이 아니라 **관리 단위의 목적**을 나타낸다.

------

## Work Item Description (Required Sections)

작업 설명에는 반드시 아래 섹션을 포함한다.

### 1. Context

- Project: Project_W
- Reference:
  - Project_W – System Index (AI Entry Point)
  - 관련 SSOT 문서
  - (필요 시) PM Log

------

### 2. Management Intent

- 이 작업을 수행하는 관리적 이유
- 일정, 리스크, 범위 관점의 목적

기능적 설명보다 **관리 판단의 배경**을 우선한다.

------

### 3. Scope / Non-Scope

- 포함 범위 (Scope)
- 의도적으로 제외한 것 (Non-Scope)

SSOT와 충돌하지 않도록 경계 정의를 명시한다.

------

### 4. Risks & Assumptions

- 예상 리스크
- 전제 조건

해결책이 아니라 **인식 상태**를 기록한다.

------

### 5. Evidence Link (Optional)

- 관련 커밋 / PR
- 빌드 또는 데모 링크
- 외부 계약/문서 링크

증거는 링크만 남기며, 해석은 PM Log에서 수행한다.

------

## AI Handling Rules

- 작업 단위는 **관리 단위**로 해석한다.
- 단독 문서로 의도 추론이 가능해야 한다.
- 규칙 근거는 SSOT만 사용한다.
- 구현 변경 전 문서 충돌 여부를 확인한다.
