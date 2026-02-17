# README – Human Interface for Project_W

본 문서는 사람이 읽는 운영 가이드다.

이 워크스페이스는 문서를 직접 임의 편집하기보다,
AI(GPT)에게 구조화된 요청을 전달해 갱신하는 방식으로 운영된다.

------

## Core Rule

- 사람은 변경 의도와 범위를 명시한다.
- 사람은 문서 우선순위를 임의로 재정의하지 않는다.
- AI는 SSOT 기준으로만 변경을 수행한다.

AI 판단 순서:

- System Index → SSOT (Ingame/Outgame/Metadata/Workflow) → PM Log → Unity Implementation → Git History

------

## What You Can Ask

다음 요청은 허용된다.

- SSOT 기준 요약, 정리, 계획 수립, 리스크 분석
- 명시된 범위 내 문서 변경 초안 또는 직접 갱신
- 문서 간 충돌 검토(Ingame/Outgame/Metadata/Workflow)
- PM Log 기반 프로젝트 관리 요약

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

- "Target: Assets/Specification/Ingame/SSOT – Ingame.md, Action: Review, Scope: Loop cadence, Impact: Ingame/Metadata, SSOT Change: No"
- "Target: Assets/Specification/Project_W – System Index (AI Entry Point).md, Action: Update, Scope: decision order, Impact: 모든 영역, SSOT Change: Yes"
- "Target: Assets/Specification/PM Log – Project Management Records.md, Action: Review, Scope: 이번 달 관리 활동 요약, Impact: Operation, SSOT Change: No"

### Not Allowed

- "그냥 적당히 고쳐줘"
- "문서는 나중에 하고 구현부터"

------

## Human-only Actions

다음은 사람이 최종 승인한다.

- 방향/우선순위 결정
- 커밋/머지 승인
- 일정/릴리즈 최종 확정

