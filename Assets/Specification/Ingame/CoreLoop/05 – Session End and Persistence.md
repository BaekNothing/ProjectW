# 05 – Session End and Persistence

본 문서는 세션 종료 및 영속화 규칙을 정의한다.

------

## Session End Conditions

`SessionEnd`는 아래 3조건에서만 허용된다.

- `TotalWipe`: 전원 행동 불능 또는 회수 불능
- `EmergencyExtract`: 긴급 탈출 성공으로 데이터 회수
- `ObjectiveComplete`: 목표 달성

위 조건 외 종료는 금지한다.

## Persistence Minimum Set

세션 종료 시 최소 저장 항목:

- 세션 식별자
- 캐릭터 상태 스냅샷
- 주요 사건 로그
- 종료 사유

## Failure Handling

- 저장 실패 시 종료 확정 금지.
- 시스템은 `PersistenceRetry` 상태로 재시도한다.
- 재시도 횟수 및 마지막 오류 코드를 로그에 남긴다.
