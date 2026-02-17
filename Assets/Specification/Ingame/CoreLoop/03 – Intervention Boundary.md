# 03 – Intervention Boundary

본 문서는 플레이어 개입 경계 규칙을 정의한다.

------

## Allowed Intervention

- 정책(지령) 변경
- 목표 우선순위 조정
- `NightDream` 단계 루틴 재정렬

## Forbidden Intervention

- 캐릭터 직접 이동/직접 행동 명령
- Tick 경계 밖 강제 상태 전환
- 종료 조건 우회 명령

## Queue Rule

- 개입 명령은 즉시 적용하지 않는다.
- 명령은 대기열에 저장 후 다음 Tick에 반영한다.
- 동일 Tick에 상충 명령이 존재하면 최신 타임스탬프 명령을 우선한다.

## Visibility Rule

- 플레이어에게는 현재 적용 대기 명령 수와 마지막 적용 Tick을 표시해야 한다.
