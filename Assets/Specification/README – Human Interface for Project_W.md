# README – Human Interface for Project_W

본 문서는 사람이 읽는 유일한 운영 가이드다.

이 워크스페이스는 문서를 직접 편집하거나 해석하는 방식이 아니라,
AI(GPT)에게 요청을 전달하는 방식으로 운영된다.

사람은 판단하지 않고, 원하는 상태를 요청한다.

------

## Core Rule

- 사람은 README만 읽는다
- 사람은 문서를 직접 편집하지 않는다
- 사람은 AI에게 요청만 한다

AI는 다음 순서로만 판단한다.

- System Index → SSOT → Jira → PM Log

------

## What You Can Ask

다음과 같은 요청은 허용된다.

- 현재 구조를 유지한 상태에서
  - 요약
  - 정리
  - 계획 수립
  - 리스크 분석
- SSOT를 명시적으로 참조한 뒤
  - 변경 초안 작성 요청
  - 충돌 여부 검토 요청
- PM Log와 Jira를 기준으로 한
  - 프로젝트 관리 요약
  - PMP 제출용 정리

------

## What You Cannot Ask

다음 요청은 허용되지 않는다.

- SSOT 확인 없이 규칙을 바꾸려는 요청
- 문서 우선순위를 암묵적으로 변경하는 요청
- 맥락 없는 수정 요청
  - 예: "그냥 적당히 고쳐줘"

이러한 요청이 감지되면,
AI는 작업을 중단하고 재확인을 요청한다.

------

## Mandatory Declaration (Required)

변경 또는 편집을 요청할 때,
반드시 아래 정보를 포함해야 한다.

1. 참조 문서 (System Index / SSOT / PM Log 중 명시)
2. 요청 성격 (변경 / 검토 / 요약)
3. SSOT 변경 여부 (변경 없음일 경우 명시)

이 조건이 충족되지 않으면,
AI는 요청을 유효하지 않은 명령으로 간주한다.

------

## Safety Mechanism (A + C)

본 워크스페이스는 다음 두 안전장치를 혼합해 사용한다.

### A. AI Pre-Validation (Primary)

- 모든 요청은 README 규칙 충족 여부를 우선 검사한다
- 규칙이 불명확하거나 충돌할 경우, 작업을 중단하고 재확인을 요청한다

### C. System Index / SSOT Enforcement (Secondary)

- System Index 및 SSOT와 충돌하는 요청은 자동 차단된다

두 규칙이 충돌할 경우,
A(Pre-Validation)가 항상 우선한다.

------

## How to Ask (Prompt Samples)

### Allowed

- "System Index와 SSOT–Ingame을 기준으로,
  Epic PW-EPIC-04가 규칙과 충돌하는지 검토해줘"
- "SSOT–Metadata를 변경하지 않는 선에서,
  캐릭터 Version 관리 방식 개선안을 제안해줘"
- "PM Log와 Jira Worklog를 기준으로,
  이번 달 PMP 관리 활동 요약을 만들어줘"

### Not Allowed

- "이 시스템 이상한데 그냥 고쳐줘"
- "SSOT는 나중에 보고 일단 구현부터"

------

## Operating Philosophy

- 사람은 의도를 말한다
- AI는 규칙을 지킨다
- 모든 결과는 SSOT와 Jira로 설명 가능해야 한다



## Standard Prompt – Jira Ticket Creation / Update

Jira 티켓의 생성 및 보정은 아래 형식을 사용해
AI(GPT)에게 요청한다.

사람은 완료일(Due Date) 조정과
상태(Status) 변경만 직접 수행한다.

------

### Mandatory Fields

모든 요청에는 반드시 아래 항목을 포함한다.

- Target  
  - Jira Project / Epic Key / Issue Key
- Action  
  - Create / Update / Review
- Reference  
  - System Index / SSOT / PM Log 중 명시
- Scope  
  - 생성 또는 보정 대상 범위
- SSOT Change  
  - Yes / No

필수 항목이 누락될 경우,
AI는 작업을 중단하고 재확인을 요청한다.

------

### Example – Epic 생성

Target: Jira Project PW  
Action: Create Epic  
Reference: System Index, SSOT–Ingame  
Scope: 신규 Epic 생성 및 관리 목적 정의  
SSOT Change: No  

Vertical Slice 구현을 위한 Epic을 생성해줘.  
기능 나열이 아니라,  
관리 의도 / 범위 / 리스크 중심으로 작성해줘.

------

### Example – Task 보정

Target: PW-123  
Action: Update  
Reference: SSOT–Outgame  
Scope: Description 및 Scope 보정  
SSOT Change: No  

이 Task가 상위 Epic의 관리 의도를
명확히 드러내도록 설명을 정리해줘.

------

## Human-only Actions

다음 작업만 사람이 직접 수행한다.

- Due Date 조정
- Status 전환 (To Do / In Progress / Done)

그 외 모든 티켓 내용 수정은
AI를 통해 수행한다.