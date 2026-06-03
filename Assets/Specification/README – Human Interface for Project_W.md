# README – Human Interface for Project_W

본 문서는 사람이 읽는 운영 가이드다.

`Project_W`는 개발 코드명이며, 현재 게임의 작업 제목은 **외행성재척지원실 3과**다.

이 워크스페이스는 문서를 직접 임의 편집하기보다,
AI(GPT)에게 구조화된 요청을 전달해 갱신하는 방식으로 운영된다.

------

## Core Rule

- 사람은 변경 의도와 범위를 명시한다.
- 사람은 문서 우선순위를 임의로 재정의하지 않는다.
- AI는 SSOT 기준으로만 변경을 수행한다.

AI 판단 순서:

- System Index → SSOT (Ingame/Outgame/Metadata/Workflow) → Unity Implementation → Git History

------

## What You Can Ask

다음 요청은 허용된다.

- SSOT 기준 요약, 정리, 계획 수립, 리스크 분석
- 명시된 범위 내 문서 변경 초안 또는 직접 갱신
- 문서 간 충돌 검토(Ingame/Outgame/Metadata/Workflow)
- 폐기된 PM Log 기록 중 필요한 결정의 SSOT 흡수

------

## What You Cannot Ask

다음 요청은 허용되지 않는다.

- SSOT 확인 없이 규칙 변경
- 문서 우선순위 암묵 변경
- 근거/범위 없는 수정 지시

이런 요청이 감지되면 AI는 작업을 중단하고 재확인을 요청한다.

------

## Mandatory Declaration (Required)

변경 또는 편집 요청 시 반드시 아래 5개 필드를 포함한다.

1. Target
2. Action (Create | Update | Review)
3. Scope
4. Impact
5. SSOT Change (Yes | No)

필수 항목 누락 시 요청은 유효하지 않은 명령으로 간주한다.

------

## Safety Mechanism

- Pre-Validation: 입력 필드 완전성 및 우선순위 충돌 여부 검증
- SSOT Enforcement: SSOT와 충돌하는 변경 자동 차단
- Conflict Gate: 문서 충돌 해소 전 구현 변경 금지

------

## How to Ask (Prompt Samples)

### Allowed

- "Target: Assets/Specification/Ingame/SSOT – Ingame.md, Action: Review, Scope: daily assignment loop, Impact: Ingame/Metadata, SSOT Change: No"
- "Target: Assets/Specification/Ingame/SSOT – Ingame.md, Action: Update, Scope: card/deck review cost rule, Impact: Ingame/Metadata, SSOT Change: Yes"
- "Target: Assets/Specification/Project_W – System Index (AI Entry Point).md, Action: Update, Scope: decision order, Impact: 모든 영역, SSOT Change: Yes"
- "Target: Assets/Specification/Ingame/SSOT – Ingame.md, Action: Update, Scope: absorb deprecated PM Log decision, Impact: Ingame, SSOT Change: Yes"

### Not Allowed

- "그냥 적당히 고쳐줘"
- "문서는 나중에 하고 구현부터"

------

## Human-only Actions

다음은 사람이 최종 승인한다.

- 방향/우선순위 결정
- 커밋/머지 승인
- 일정/릴리즈 최종 확정

------

## AI Test, Build, and Push Operations

Unity 테스트, APK 빌드, 커밋, push를 AI/LLM에게 맡길 때는 아래 문서를 우선 읽게 한다.

- `Assets/Specification/AI Build and Git Push Guide.md`

이 문서는 Unity EditMode 테스트 러너 명령, APK 산출물 처리, `.git` 권한 문제, Unity Package Manager IPC 실패, 커밋 로그 작성, `origin/ai-integration` push 절차를 포함한다.

------

## README Implementation Status Rule

SSOT를 추가하거나 구현 상태가 바뀌면 루트 `README.md`의 `SSOT implementation status` 표를 같은 작업에서 갱신한다.

- SSOT만 있고 구현체가 없으면 `SSOT only`로 표시한다.
- 일부 구현이면 구현된 타입/시스템과 빠진 런타임 표면을 함께 적는다.
- 구현 완료 또는 범위 변경이 있으면 같은 커밋에서 README 상태를 갱신한다.

