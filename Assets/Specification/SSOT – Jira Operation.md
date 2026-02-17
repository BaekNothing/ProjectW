본 문서는 Project_W에서 Jira를 사용하는 방식에 대한
단일 진실원(Source of Truth)이다.

Jira는 설계 도구가 아니며,
규칙을 정의하거나 해석하는 장소가 아니다.

본 문서는 AI(GPT)가 Jira를 생성·보정·해석할 때
반드시 참조해야 하는 운영 계약이다.

------

## 1. Jira의 지위

- Jira는 **Execution Log**다
- Jira는 **규칙의 출처가 아니다**
- Jira는 **의사결정의 근거가 아니라 결과 기록**이다

규칙과 의미는 항상 다음에서만 정의된다.

- System Index
- SSOT (Ingame / Outgame / Metadata)

------

## 2. Issue Type 정의

### Epic

- Epic은 **관리 단위**다
- Epic은 “큰 기능”이 아니라
  **관리 목적을 가진 작업 묶음**이다

Epic은 반드시 다음 정보를 포함해야 한다.

- Context
  - System Index 및 관련 SSOT 참조
- Management Intent
  - 왜 이 Epic이 필요한가 (관리 관점)
- Scope / Non-Scope
- Risks & Assumptions

Epic은 설계 문서를 대체하지 않는다.

------

### Story / Task

- Story / Task는 **집행 단위**다
- 설계 의도, 규칙 설명, 의미 해석을 포함하지 않는다
- 반드시 상위 Epic을 가진다

Story / Task는 다음만을 기술한다.

- 무엇을 집행하는가
- 어떤 산출물이 나오는가

------

## 3. AI의 Jira Handling Rules

AI는 Jira를 다룰 때 반드시 다음 순서를 따른다.

1. System Index 확인
2. SSOT 확인
3. 본 문서(SSOT – Jira Operation) 확인
4. Jira Issue 생성 / 보정 / 검토 수행

AI는 다음 행위를 수행할 수 있다.

- Epic / Story / Task 생성
- Description, Scope, Risk 보정
- 상위 Epic과의 정합성 수정
- 관리 관점 요약

------

## 4. Human-only Operations

다음 작업만 사람이 직접 수행한다.

- Due Date 조정
- Status 변경 (To Do / In Progress / Done 등)

사람은 Jira Issue의
설명, 범위, 관리 의도를 직접 수정하지 않는다.

------

## 5. 금지 사항

다음 행위는 금지된다.

- Jira에서 규칙을 정의하거나 변경하는 행위
- SSOT를 참조하지 않은 상태에서 Epic 생성
- Jira Issue를 설계 문서처럼 사용하는 행위
- 단일 사례를 일반 규칙으로 승격하는 서술

위반이 감지될 경우,
AI는 작업을 중단하고 재확인을 요청한다.

------

## 6. 이 문서의 지위

- 본 문서는 SSOT – Outgame의 하위 규약이다
- Jira 관련 판단에서 **항상 우선 참조**된다
- 본 문서와 Jira Issue 간 충돌 시,
  **본 문서가 항상 우선**한다