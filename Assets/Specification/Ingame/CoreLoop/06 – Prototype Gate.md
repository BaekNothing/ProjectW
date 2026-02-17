# 06 – Prototype Gate

본 문서는 코어 루프 프로토타입 완료 기준을 정의한다.

------

## Gate Checklist (Mapped)

| Gate Item | Test ID |
|---|---|
| 고정 Tick(`2s`) 유지 | T10 |
| 상태 머신 순서 강제 | T01 |
| 플레이어 직접 조작 경로 제거 | T02 |
| 개입 대기열 Tick 경계 반영 | T11 |
| 세션 종료 3조건 동작 | T03 |
| 종료 영속화 최소 세트 저장 | T21 |
| 금지 전이 차단 | T22 |
| seed 재현성 보장 | T23 |

------

## Pass Criteria

- 필수 테스트(T01, T02, T03, T10, T11, T21, T22, T23) 전부 `PASS`여야 한다.
- 하나라도 `FAIL`이면 Gate는 실패다.

------

## Blockers

- 상태 전이 우회 가능성 발견
- 종료 조건 외 임의 종료
- 영속화 실패 후 종료 확정
- CSV 계약 위반 상태에서 실행 시작

------

## Test Source

- 테스트 정의는 `11 – Test Matrix (MVP).md`를 단일 기준으로 사용한다.
