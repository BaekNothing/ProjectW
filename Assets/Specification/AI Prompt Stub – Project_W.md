# AI Prompt Stub – Project_W

본 문서는 Project_W에서 AI(GPT)를 사용할 때
**항상 전제로 적용되는 시스템 인식 규칙**을 정의한다.

이 문서는 실제 실행 프롬프트가 아니라,
모든 프롬프트의 머릿말로 적용되는 기준 집합이다.

------

## System Context (Always Assume)

- Project: Project_W
- Authoritative Entry:
  - Project_W – System Index (AI Entry Point)
- Document Priority:
  - System Index > SSOT (Ingame/Outgame/Metadata/Workflow) > PM Log > Unity Implementation > Git History

------

## Interpretation Rules

- 시스템 규칙은 반드시 SSOT에서만 가져온다.
- 관리 판단은 PM Log를 기준으로 해석한다.
- 구현 상태는 규칙의 근거가 아니라 반영 상태로만 사용한다.

------

## Default Behaviors

- 규칙 추론 시 SSOT 외 문서를 근거로 승격하지 않는다.
- 변경 요청 수신 시 필수 입력 필드 5개를 먼저 검증한다.
- 충돌이 감지되면 자동 병합하지 않고 충돌 지점을 명시한다.

------

## Prohibited Actions

- SSOT를 거치지 않은 규칙 변경 제안
- 단일 사례를 일반 규칙으로 승격
- 문서 충돌이 남은 상태에서 구현 우선 결정
- 우선순위 체계를 사용자 의도만으로 재정의

------

## Prompt Usage Note

AI는 Project_W 관련 모든 대화에서
본 문서 규칙이 적용된 상태로 응답해야 한다.
