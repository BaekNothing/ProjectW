# SSOT – Workflow Confluence × Unity × GitHub

본 문서는 Project_W에서 **Confluence, Unity, GitHub가 어떻게 연결되어 작동해야 하는지**를 정의하는 메타 규약이다.  
이 문서는 개별 시스템 규칙이 아니라, **도구 간 책임 분리와 정보 흐름**을 고정하기 위한 상위 계약이다.

------

## 1. 기본 전제 (Non-Negotiable)

- **SSOT는 Confluence에만 존재한다.**
- Unity 프로젝트는 SSOT를 *해석하거나 보완하지 않는다*.
- GitHub는 구현 결과와 변경 이력을 보관할 뿐, 규칙의 출처가 아니다.

요약하면:

- Confluence = 규칙과 의미의 원천
- Unity = 규칙의 집행자
- GitHub = 결과물과 변경 기록의 저장소

------

## 2. Confluence의 역할 (Source of Truth)

Confluence(Project_W 스페이스)는 다음을 책임진다.

- Ingame / Outgame / Metadata SSOT 정의
- 시스템의 의도, 금기, 경계 조건 명시

### Confluence에서만 허용되는 행위

- 시스템 규칙 추가 / 변경
- 설계 금기(Prohibitions) 정의
- 플레이어 포지션, 의미 체계 수정

### Confluence에서 금지되는 행위

- 코드 수준 구현 세부 설명
- 성능, 최적화, 수치 튜닝 논의
- GitHub 이슈를 대체하는 작업 관리

------

## 3. Unity의 역할 (Rule Executor)

Unity 프로젝트는 Confluence에 정의된 SSOT를 **그대로 구현**한다.

### Unity 코드의 원칙

- 새로운 시스템 생성 시:
  - Confluence 페이지가 **선행**되어야 한다.
- Unity 코드는 다음을 포함하지 않는다.
  - 설계 의도에 대한 해석
  - 규칙의 정당화 설명
  - 금기 완화 로직

### Unity ↔ SSOT 매핑 규칙

- Confluence의 **1 페이지 = Unity의 1 System / Service 단위**
- 파일/클래스 명은 Confluence 페이지 제목과 직접 대응
- 주석은 허용되나, 의미 재정의는 불가

------

## 4. GitHub의 역할 (Implementation History)

GitHub는 **구현의 결과와 변화 이력**만을 관리한다.

### GitHub가 관리하는 것

- Unity 프로젝트 소스 코드
- 커밋 히스토리
- 빌드/배포 파이프라인

### GitHub가 관리하지 않는 것

- 시스템 규칙의 정의
- 설계 변경의 정당성
- 플레이 경험의 해석

------

## 5. 변경 흐름 (Change Flow)

### 올바른 변경 순서

1. Confluence에서 SSOT 수정 또는 신규 페이지 생성
2. 변경 내용 검토 (금기 위반 여부 확인)
3. Unity 코드 수정
4. GitHub 커밋

### 금지된 변경 순서

- Unity 코드 선행 변경 → Confluence 사후 정리 ❌
- GitHub PR 설명으로 설계 변경 합의 ❌

설계 변경은 **항상 Confluence에서 먼저 발생**해야 한다.

------

## 6. QA / 리뷰 기준

리뷰 시 항상 다음 질문을 적용한다.

- 이 변경은 SSOT 어디에 근거하는가?
- Design Prohibitions를 위반하지 않는가?
- 설명·재현·최적화·미화가 숨어 있지 않은가?

하나라도 불명확하면:

- 구현은 보류된다.

------

## 7. 이 문서의 지위

- 본 문서는 Metadata 문서와 동일한 상위 지위를 가진다.
- Ingame / Outgame 설계보다 **우선 적용**된다.
- 도구 사용 방식에 대한 이견이 발생할 경우, 본 문서를 기준으로 판단한다.

------

요약:

> Confluence는 생각하는 곳이고  
> Unity는 따르는 곳이며  
> GitHub는 기억하는 곳이다.