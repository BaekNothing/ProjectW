# SSOT – Workflow Local Spec × Unity × GitHub

본 문서는 Project_W에서 **로컬 Specification 문서, Unity, GitHub의 책임 분리와 변경 흐름**을 고정하는 메타 규약이다.  
개별 시스템 규칙이 아니라 운영 계층(Workflow/Operation)의 기준을 정의한다.

------

## 1. 기본 전제 (Non-Negotiable)

- SSOT는 `Assets/Specification` 내 문서에만 존재한다.
- Unity 프로젝트는 SSOT를 해석하거나 보완하지 않고 구현만 수행한다.
- GitHub는 구현 결과와 변경 이력을 보관하지만 규칙의 출처는 아니다.

요약:

- Local Specification = 규칙과 의미의 원천
- Unity = 규칙의 집행자
- GitHub = 결과물과 변경 기록

------

## 2. Local Specification의 역할 (Source of Truth)

`Assets/Specification`은 다음을 책임진다.

- Ingame / Outgame / Metadata / Workflow 규칙 정의
- 시스템 의도, 금기, 경계 조건 명시
- 변경 사유 및 미해결 TODO 기록

### Local Specification에서만 허용되는 행위

- 시스템 규칙 추가 및 변경
- 금기(Prohibitions) 정의
- 문서 우선순위와 책임 경계 수정

### Local Specification에서 금지되는 행위

- 구현 세부 코드의 정답화
- 단일 실험 결과를 구조 규칙으로 승격
- 문서 없이 코드 상태로 정책 확정

------

## 3. Unity의 역할 (Rule Executor)

Unity 프로젝트는 SSOT에 정의된 규칙을 구현한다.

### Unity 코드 원칙

- 시스템 변경 전, 관련 Specification 업데이트가 선행되어야 한다.
- Unity 코드는 설계 의도의 재정의나 우선순위 변경을 포함하지 않는다.
- Loop 기반 MVP 규칙(`1 Tick / 2 seconds`)을 위반하는 구현은 허용되지 않는다.

### Unity Editor Refresh Policy (Local Automation)

- 기본 정책: Auto Refresh는 비활성(`Disabled`)을 기본값으로 사용한다.
- 강제 리프레시는 아래 트리거에서만 수행한다.
  - 사용자가 명시적으로 메뉴/명령으로 요청한 경우
  - Codex/자동화가 지정 시그널 파일을 생성한 경우
- 정책 의도:
  - 대규모 파일 변경 중 의도치 않은 Domain Reload 최소화
  - 리프레시 타이밍을 작업 단위(Tick/Task) 완료 시점으로 통제

### Unity ↔ SSOT 매핑 규칙

- Specification의 1개 규칙 단위는 Unity의 1개 시스템/서비스 단위와 추적 가능해야 한다.
- 파일/클래스/씬 변경은 근거 문서를 명시할 수 있어야 한다.
- 주석은 허용되나, 문서 의미를 변경하는 근거로 사용할 수 없다.

------

## 4. GitHub의 역할 (Implementation History)

GitHub는 구현 결과와 협업 이력을 관리한다.

### GitHub가 관리하는 것

- Unity 프로젝트 소스 코드
- 커밋/브랜치/PR 이력
- 빌드/배포 파이프라인 및 결과 아티팩트

### GitHub가 관리하지 않는 것

- 시스템 규칙의 원문 정의
- 설계 변경의 정책 판단
- SSOT 우선순위 결정

------

## 5. 변경 흐름 (Change Flow)

### 올바른 변경 순서

1. `Assets/Specification`에서 SSOT 수정 또는 신규 문서 갱신
2. 문서 간 충돌 점검 (Ingame/Outgame/Metadata/Workflow)
3. Unity 구현 변경
4. GitHub 커밋/PR로 변경 이력 기록

### 금지된 변경 순서

- Unity 코드 선행 후 문서 사후 정리
- PR 설명만으로 설계 변경 합의
- 문서 미갱신 상태로 규칙 적용 선언

------

## 6. QA / 리뷰 기준

리뷰 시 다음 질문을 적용한다.

- 변경 근거가 SSOT 어디에 기록되어 있는가?
- 금기(Prohibitions) 위반이 없는가?
- 문서-구현-이력 간 추적이 가능한가?

하나라도 불명확하면 구현을 보류한다.

------

## 7. 이 문서의 지위

- 본 문서는 Workflow/Operation 계층의 기준 문서다.
- Ingame / Outgame / Metadata에 공통 적용된다.
- 도구 책임 또는 변경 순서 이견 발생 시 본 문서를 우선 기준으로 판단한다.

------

요약:

> Specification은 결정하고  
> Unity는 집행하며  
> GitHub는 기록한다.
