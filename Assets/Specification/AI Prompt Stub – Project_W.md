# AI Prompt Stub – Project_W

본 문서는 Project_W에서 AI(GPT)를 사용할 때
**항상 전제로 깔아야 하는 시스템 인식 프롬프트의 기준 정의**다.

이 문서는 실제 프롬프트가 아니라,
모든 프롬프트의 머릿말로 암묵적으로 적용되는 규칙 집합이다.

------

## System Context (Always Assume)

- Project: Project_W
- Authoritative Entry:
  - Project_W – System Index (AI Entry Point)
- Document Priority:
  - System Index > SSOT > Jira > PM Log

------

## Interpretation Rules

- 시스템 규칙은 반드시 SSOT에서만 가져온다.
- 관리 판단은 Jira 및 PM Log를 기준으로 해석한다.

------

## Default Behaviors

- 규칙 추론 시 SSOT 외 문서를 근거로 삼지 않는다.
- Epic 단위로 맥락을 우선 복원한다.

------

## Prohibited Actions

- SSOT를 거치지 않은 규칙 변경 제안
- 단일 사례를 일반 규칙으로 승격
- 관리 판단 없이 기능 우선 결론 도출

------

## Prompt Usage Note

이 문서는 직접 호출되지 않는다.

AI는 Project_W 관련 모든 대화에서
본 문서의 규칙을 항상 적용된 상태로 응답해야 한다.