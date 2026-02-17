# Project_W – System Index (AI Entry Point)

본 문서는 **AI(GPT)가 Project_W의 문서 구조와 판단 순서를 복원하기 위한 단일 진입점**이다.

이 문서는 개별 시스템 상세를 직접 정의하지 않는다.
규칙의 원문은 SSOT 문서에 있으며, 본 문서는 참조 순서와 운영 규칙만 고정한다.

------

## AI Decision Order (Authoritative)

AI는 반드시 아래 순서로만 판단한다.

1. **Project_W – System Index (본 문서)**
2. **SSOT – Ingame / Outgame / Metadata / Workflow**
3. **PM Log – Project Management Records**
4. **Unity Implementation (Assets, ProjectSettings, Packages)**
5. **Git History (Commit/PR/Tag)**

이 순서는 고정이며, 어떠한 요청도 우선순위를 재정의할 수 없다.

------

## Document Roles

- **System Index**
  - 문서 우선순위, AI 운영 규칙, 거부 조건 정의
- **SSOT – Ingame / Outgame / Metadata / Workflow**
  - 시스템 규칙의 단일 진실원
- **PM Log**
  - 프로젝트 관리 의사결정 기록 및 증빙 해석
- **Unity Implementation**
  - SSOT 실행 결과물
- **Git History**
  - 구현 변경 이력 및 협업 근거

------

## AI Enforcement Rules

- AI는 SSOT를 거치지 않은 규칙 변경 요청을 거부한다.
- AI는 문서 우선순위를 암묵적으로 바꾸는 요청을 거부한다.
- AI는 필수 입력 필드(Target/Action/Scope/Impact/SSOT Change) 누락 시 작업을 중단한다.
- AI는 문서 충돌(Ingame/Outgame/Metadata/Workflow) 해소 전 구현 변경을 시작하지 않는다.

------

## Sync Rule

문서와 구현 간 불일치가 발견되면 다음 순서로 처리한다.

1. SSOT 문서 갱신 여부 확인
2. 미갱신이면 SSOT 먼저 수정
3. 이후 Unity 구현 반영
4. Git 이력에 근거 문서 경로 명시
