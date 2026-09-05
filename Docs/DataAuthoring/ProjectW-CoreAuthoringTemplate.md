<!--
ProjectW core authoring interchange format
- # Heading: sheet/table name
- ## Heading: stable row identity
- Field : Value: scalar cell; split only on the first exact " : " token
- Field : [...]: JSON array cell
- Field : | followed by two-space-indented lines: multiline text cell
- null: absent reference; "": intentional empty string
- Enum values use exact C# names; a compiler maps them to the runtime JSON representation.
- Runtime state fields are retained where present in the current source, but authoring guidance in ContentDataStructure.md decides what may be edited.
- This file is a non-runtime authoring seed, not an exhaustive mirror of production JSON. As of
  2026-08-31 production contains 20 Works, 39 Tasks, 20 authored mail entries, 7 critical-event
  chains, and 32 Codex entries. Use task-system.json when exact runtime completeness matters.
-->

# ProjectConfig

## project-w

SchemaVersion : 1
CampaignEndDay : 90
MidpointReviewDay : 45
StartingResources : 12

# Balance

## default

PrimaryProgressDays : 1
ParallelProgressDays : 1
ParallelMaximumRemainingDays : 1
InterruptionCostDays : 0.5
ResumptionCostDays : 0.5
MatchingFatigue : 9
MismatchedFatigue : 15
ParallelFatigue : 12
SoftDeadlineFatigue : 4
RestRecovery : 18
WeekendFatigueRecovery : 12
WeekendMentalRecovery : 8
WeekendInjuryRecoveryChance : 35
UnscheduledCheckupResourceCost : 1
RegenerationResourceCost : 3
RegenerationAbilityInheritanceCost : 5
RegenerationPerkInheritanceCost : 4
RegenerationPersonalityRetentionWeight : 60
PayrollIntervalDays : 30
BaseSalary : 1
ExperiencePerSalaryIncrease : 20
SalaryIncrease : 1
HighFatigueAccidentChance : 28
MediumFatigueAccidentChance : 10
MismatchAccidentChance : 6
SideMissionLimit : 3
BaseSideMissionChance : 30
RandomWorkLimit : 3
RandomWorkMinSoftDays : 3
RandomWorkMaxSoftDays : 6
RandomWorkHardDeadlineDays : 3
RandomWorkMinReward : 6
RandomWorkMaxReward : 10
RandomWorkSoftPenalty : 1
RandomWorkHardPenalty : 4
RandomWorkDependencyChance : 15
RandomWorkChanceScalePercent : 80
RandomWorkMinRequiredDays : 1
RandomWorkMaxRequiredDays : 2
PrerequisiteProgressLimit : 0.3
LowOutputChance : 20
HighOutputChance : 20
FreshLowOutputChance : 5
FreshHighOutputChance : 35
ExhaustedLowOutputChance : 100
ExhaustedHighOutputChance : 0
LowOutputMultiplier : 0.5
HighOutputMultiplier : 1.5

# Works

## foundation

Id : foundation
Name : 정착 기반
SoftDeadline : 45
HardDeadline : 60
Required : true
RewardCredits : 5
SoftPenaltyCredits : 2
HardPenaltyCredits : 7
PredecessorIds : []

## launch

Id : launch
Name : 최종 가동
SoftDeadline : 81
HardDeadline : 90
RevealDay : 60
Required : true
RewardCredits : 8
SoftPenaltyCredits : 3
HardPenaltyCredits : 10
PredecessorIds : ["foundation"]

## incident

Id : incident
Name : 돌발 대응
SoftDeadline : 90
HardDeadline : 90
Required : false
RewardCredits : 2
SoftPenaltyCredits : 1
HardPenaltyCredits : 4
PredecessorIds : []

# Tasks

## survey

Id : survey
Name : 착륙 지점 조사
Kind : Milestone
RequiredRole : Analysis
RequiredCompetencies : [1,3]
RequiredWork : 3
Required : true
PrerequisiteId : ""
AssignedCharacter : -1
GroupId : foundation
Risk : Medium
Importance : High
Difficulty : 2

## power

Id : power
Name : 발전 설비 설치
Kind : Milestone
RequiredRole : Tech
RequiredCompetencies : [0,2]
RequiredWork : 4
Required : true
PrerequisiteId : survey
AssignedCharacter : -1
GroupId : foundation
Risk : High
Importance : High
Difficulty : 3

## habitat

Id : habitat
Name : 거주 구역 건설
Kind : Milestone
RequiredRole : Tech
RequiredCompetencies : [0,4,2]
RequiredWork : 4
Required : true
PrerequisiteId : survey
AssignedCharacter : -1
GroupId : foundation
Risk : Medium
Importance : High
Difficulty : 2

## safety

Id : safety
Name : 안전 검증
Kind : Milestone
RequiredRole : Analysis
RequiredCompetencies : [0,4,1]
RequiredWork : 1
Required : false
PrerequisiteId : power
AssignedCharacter : -1
GroupId : foundation
Risk : Low
Importance : Medium
Difficulty : 1

## launch

Id : launch
Name : 최종 가동 시험
Kind : Milestone
RequiredRole : Adaptation
RequiredCompetencies : [0,3,5]
RequiredWork : 3
Required : true
PrerequisiteId : ""
AssignedCharacter : -1
GroupId : launch
Risk : High
Importance : High
Difficulty : 3

# Crew

## 한기술관

Name : 한기술관
PortraitLabel : |
  한
  기술
PortraitAddress : portraits/crew/crew-han-tech
Personality : 원칙적
Memo : 기지 핵심 설비를 맡는 선임 기술관. 위험한 상황에서도 절차를 우선한다.
Perks : ["정밀 정비","안전 우선"]
Specialty : Tech
Skill : 4
Competencies : [7,4,5,4,3,3]
Trust : 55
Pride : 45
Authority : 35
DailyOutput : 1

## 윤분석관

Name : 윤분석관
PortraitLabel : |
  윤
  분석
PortraitAddress : portraits/crew/crew-yoon-analysis
Personality : 분석적
Memo : 불확실한 정보를 빠르게 정리하는 분석관. 현장 조사와 위험 판정에 강하다.
Perks : ["현장 분석","위험 감지"]
Specialty : Analysis
Skill : 4
Competencies : [3,7,4,5,3,4]
Trust : 50
Pride : 55
Authority : 30
DailyOutput : 1

## 미관리자

Name : 미관리자
PortraitLabel : |
  미
  관리
PortraitAddress : portraits/crew/crew-mi-management
Personality : 다정함
Memo : 작업 순서와 자원 배분을 조율하는 관리자. 대원들의 신뢰가 높다.
Perks : ["일정 조율","신뢰 형성"]
Specialty : Management
Skill : 4
Competencies : [4,4,7,3,4,6]
Trust : 60
Pride : 40
Authority : 50
DailyOutput : 1

## 강적응관

Name : 강적응관
PortraitLabel : |
  강
  적응
PortraitAddress : portraits/crew/crew-kang-adaptation
Personality : 대담함
Memo : 예상 밖의 환경 변화에 대응하는 적응관. 고위험 임무에서도 판단이 빠르다.
Perks : ["환경 적응","위기 대응"]
Specialty : Adaptation
Skill : 4
Competencies : [4,5,3,7,5,4]
Trust : 45
Pride : 60
Authority : 35
DailyOutput : 1

# Mail

## mail-1

Id : mail-1
ArrivalDay : 1
From : 개척 본부
Subject : 착륙 지점 조사 우선 요청
Body : 후속 설비 작업을 위해 조사 일정을 앞당겨 주십시오.
Instruction : 정착 기반의 마감이 하루 앞당겨지고 중요도가 상승합니다.
TargetWorkId : foundation
DeadlineDelta : -1
Risk : Medium

## mail-2

Id : mail-2
ArrivalDay : 4
From : 보급 통제실
Subject : 추가 보급 승인
Body : 초기 운영 보고가 승인되었습니다.
Instruction : 자원 2를 수령합니다.
ResourceDelta : 2
Risk : Low

## mail-3

Id : mail-3
ArrivalDay : 10
From : 안전 위원회
Subject : 발전 설비 안전 검토
Body : 일정 압박으로 사고 위험이 증가했습니다.
Instruction : 정착 기반 마감이 하루 앞당겨집니다.
TargetWorkId : foundation
DeadlineDelta : -1
Risk : High

# Codex

## guide-campaign-goal

Id : guide-campaign-goal
Category : 시작과 목표
Name : 캠페인의 목표
Description : |
  플레이어는 개척 운영 담당자로서 4명의 대원을 운용합니다. 이 게임에는 승리 세션과 고정 종료일이 없습니다. 초기 필수 일을 모두 끝내는 것은 장기 운영 중 하나의 이정표일 뿐입니다.

  자원은 목숨입니다. 일 완료 보상으로 보충되지만 급여, 마감 패널티, 재생 시술로 장기적으로 누수됩니다. 자원이 0이 되면 생존 기록이 종료되고, 그때까지 도달한 DAY가 점수입니다. 필수 일 실패, DAY 90 경과, 전원 부상은 그 자체로 세션을 끝내지 않습니다.

## guide-campaign-calendar

Id : guide-campaign-calendar
Category : 시작과 목표
Name : 달력과 주요 시점
Description : |
  다음날로 버튼을 누르면 하루의 작업과 사건이 한 번에 처리됩니다. 30일마다 급여를 지급하며 DAY 45에는 완료·지연·실패한 일의 수를 요약하는 중간평가가 발생합니다. 필수 최종 일인 '최종 가동'은 DAY 60부터 공개됩니다.

  중간평가는 현재 정보를 요약할 뿐 별도 합격·불합격 효과는 없습니다. DAY 90은 초기 콘텐츠와 간트의 기준일이며 종료일이 아닙니다. 간트와 예약 범위는 현재 날짜보다 최소 30일 앞까지 계속 확장됩니다.

## guide-day-order

Id : guide-day-order
Category : 시작과 목표
Name : 하루 처리 순서
Description : |
  하루는 다음 순서로 처리됩니다.
  1. 오늘 시작하기로 예약한 작업 배정
  2. 플레이어의 수동 배정에서 학습한 규칙 적용
  3. 적성 자동 배정 적용
  4. 부상·휴식 여부 확정과 회복 처리
  5. 주 작업을 먼저, 병행 작업을 나중에 실행
  6. 급여일이면 전 대원의 기본급 차감
  7. 날짜 증가
  8. 잠금과 완료 상태 갱신
  9. 소프트·하드 마감 판정
  10. 중간평가와 무작위 사이드 미션 생성

  따라서 작업 결과로 오른 경력은 같은 날의 급여 계산에 반영되며, 마감 실패 판정은 날짜가 넘어간 뒤 발생합니다.

## guide-work-task

Id : guide-work-task
Category : 일과 작업
Name : 일과 작업의 차이
Description : |
  일(Work)은 플레이어가 달성해야 하는 상위 목표이고, 작업(Task)은 대원에게 실제로 배정하는 실행 단위입니다. 하나의 일은 여러 작업을 가질 수 있습니다.

  일이 소유하는 데이터
  • 소프트·하드 마감
  • 필수 여부와 보상·패널티
  • 선행 일 목록
  • 공개일 또는 메일 수락 대기 상태

  작업이 소유하는 데이터
  • 기본 작업량과 현재 진행량
  • 필수·선택 여부
  • 선행 작업, 담당자, 예약일
  • 요구 역할과 1~3개 요구 역량
  • 위험도, 중요도, 난이도, 결과 기록

  일의 모든 필수 작업이 완료되어야 그 일이 완료됩니다. 선택 작업은 일 완료를 막지 않습니다.

## guide-work-states

Id : guide-work-states
Category : 일과 작업
Name : 상태와 잠금
Description : |
  일 상태는 잠김, 대기, 진행, 완료, 실패입니다. 작업 상태도 잠김, 대기, 진행, 완료, 실패로 나뉩니다.

  일은 선행 일이 미완료이거나 공개일 전이거나 수락 메일을 기다리면 잠깁니다. 작업은 소속 일이 잠겼을 때 잠깁니다. 작업의 선행 작업이 미완료여도 수동 배정은 가능하지만 진행률이 제한됩니다. 자동 배정은 선행 관계가 모두 끝날 때까지 기다립니다.

  작업은 실제 하루 산출을 만든 날부터 진행 상태가 됩니다. 완료 작업에는 마지막 담당자가 기록으로 남지만 그 대원의 현재 작업 슬롯을 차지하지 않습니다.

## guide-prerequisite

Id : guide-prerequisite
Category : 일과 작업
Name : 선행 관계와 30% 제한
Description : |
  작업은 하나의 선행 작업을 가질 수 있습니다. 선행 작업이 끝나지 않아도 후행 작업을 수동으로 배정할 수 있지만, 후행 작업은 유효 작업량의 30%를 넘어서 진행하지 못합니다. 이미 30%에 도달했다면 그날 산출은 0이 될 수 있습니다.

  일도 여러 선행 일을 가질 수 있으며, 이 경우 모든 선행 일이 완료되어야 잠금이 풀립니다. 자동 배정과 예약 실행은 선행 일과 선행 작업이 모두 완료된 작업만 시작합니다. 간트의 화살표는 이 관계를 표시합니다. 제한 비율 PrerequisiteProgressLimit의 기본값은 0.3입니다.

## guide-assignment

Id : guide-assignment
Category : 일과 작업
Name : 주 작업과 병행 작업
Description : |
  대원 한 명은 하루에 하나의 주 작업만 수행할 수 있습니다. 잔여 유효 작업량이 1일 이하인 작업은 병행 작업으로 배정할 수 있습니다. 병행은 별도 슬롯이지만 추가 피로를 지불합니다.

  기본값
  • 주 작업 진행 계수: ×1.0
  • 병행 작업 진행 계수: ×1.0
  • 병행 허용 잔여량: 1.0일 이하
  • 역할 일치 피로: +9
  • 역할 불일치 피로: +15
  • 병행 추가 피로: +12

  하루 처리에서는 주 작업이 먼저 실행되고 병행 작업이 나중에 실행됩니다. 부상 또는 예약 휴식 중인 대원은 둘 다 진행하지 않습니다.

## guide-progress-formula

Id : guide-progress-formula
Category : 일과 작업
Name : 진행량 계산식
Description : |
  하루 최종 진행량은 다음 곱으로 계산합니다.

  대원의 DailyOutput × 주/병행 진행 계수 × 요구 역량 배율 × 당일 결과 배율

  기본 DailyOutput은 1.0입니다. 요구 역량 배율은 해당 작업이 요구하는 역량 점수만 평균 내어 기준치 4로 나눕니다. 모든 요구 점수가 4 미만이면 배율은 0.5로 고정됩니다. 하나라도 4 이상이면 높은 점수가 낮은 점수를 평균으로 보완하며 자연 범위는 0.5~1.75입니다.

  계산된 진행량은 남은 작업량을 넘지 않으며 선행 작업이 미완료라면 30% 상한을 먼저 적용합니다.

## guide-task-outcome

Id : guide-task-outcome
Category : 일과 작업
Name : 실패·성공·대성공
Description : |
  작업을 수행할 때마다 피로에 따라 실패, 성공, 대성공 중 하나가 결정됩니다. 여기서 실패는 작업 전체 실패가 아니라 그날의 낮은 산출을 뜻합니다.

  피로 0: 실패 5% / 성공 60% / 대성공 35%
  피로 50: 실패 20% / 성공 60% / 대성공 20%
  피로 100: 실패 100% / 성공 0% / 대성공 0%

  중간 피로도는 선형 보간합니다. 결과 배율 기본값은 실패 ×0.5, 성공 ×1.0, 대성공 ×1.5입니다. 결과와 실제 산출은 작업 기록, 보고서, 메신저에 남습니다.

## guide-schedule

Id : guide-schedule
Category : 일과 작업
Name : 예약과 예상 일정
Description : |
  작업 상세에서 대원과 시작일을 골라 예약할 수 있습니다. 같은 대원에게 같은 날짜의 예약을 두 개 만들 수 없습니다. 예약일이 오면 수동 학습 및 적성 자동 배정보다 먼저 주 작업 배정을 시도합니다. 대원이 다른 주 작업을 수행 중이거나 조건을 만족하지 못하면 예약이 불발될 수 있습니다.

  예상 일정은 남은 작업량, 담당 변경 비용, 현재 피로의 기대 산출, 선행 작업, 선행 일, 대원의 기존 작업과 예약을 반영합니다. 미배정 작업은 하루 1.0 산출 기준으로 표시됩니다. 예상은 운영 가이드이며 확률 결과와 사고 때문에 실제 일정과 달라질 수 있습니다.

## guide-handover

Id : guide-handover
Category : 일과 작업
Name : 중단과 인수인계
Description : |
  진행 중인 작업의 담당자를 바꾸거나 배정을 해제하면 문맥 비용이 유효 작업량에 추가됩니다. 기본값은 중단 0.5일 + 재개 0.5일, 합계 1.0일입니다. 담당자를 여러 번 바꾸면 비용과 SplitCount가 계속 누적됩니다.

  아직 진행량이 0인 작업, 완료 또는 실패한 작업에는 새 인수인계 비용이 붙지 않습니다. 부상, 휴식, 재생은 담당자를 자동으로 떼지 않으므로 인수인계 비용도 만들지 않습니다. 작업 상세는 변경을 확정하기 전에 예상 추가 비용을 표시합니다.

## guide-deadline

Id : guide-deadline
Category : 일과 작업
Name : 마감·보상·패널티
Description : |
  각 일에는 소프트 마감과 하드 마감이 있습니다. 소프트 마감을 넘기면 지정 자원 패널티가 한 번 적용되고, 이후 그 일의 작업은 하루 피로 +4를 추가로 받습니다. 하드 마감을 넘기면 미완료 작업이 모두 실패하고 배정과 예약이 해제됩니다.

  일을 완료하면 RewardCredits를 한 번 지급합니다. 필수 일과 선택 일 모두 하드 마감 실패 시 해당 일과 미완료 작업이 실패하고 자원 패널티를 적용하지만, 그 자체로 운영 기록을 종료하지 않습니다. 모든 자원 차감은 0 아래로 내려가지 않으며 자원 0만 운영 기록을 종료합니다.

## guide-side-mission

Id : guide-side-mission
Category : 일과 작업
Name : 생성형 사이드 미션
Description : |
  제안서 앱에는 시작 시, 이후 14~21일마다 무작위로 구성된 제안 후보 3~4개가 들어오며 새 후보 도착은 메일로 알립니다. 후보는 7일 뒤 소멸하고, 한 건을 골라 사장에게 제안하면 같은 묶음의 나머지는 닫힙니다. 목표와 2~4개 실행 작업을 직접 고르는 풀커스텀 작성은 '직접 제안서 작성' 안쪽에 있습니다.

  작업 난이도와 총 작업량에 따라 투자비·완료 보상·위험과 승인 후 소요 기간이 계산됩니다. 검토·질문 중에는 마감일이 없고 최종 수락 결과가 오는 날을 기준으로 실제 마감이 정해집니다. 결과는 메일로 오며 질문 답변과 안 맡음 의견은 제안서 앱에서 보냅니다. 투자비도 승인되어 작업 목록에 편입되는 날 차감됩니다.

  별도로 사장이 먼저 업무를 물어오는 경우도 있습니다. 이 요청은 메일에서 수락해야 편입되며 빈 작업 목록이라고 강제로 생성되지는 않습니다. 일반 업무는 2~3주 계획 템포를 사용하고 긴급대응은 짧은 2~3일 단위로 유지합니다.

## guide-worker-profile

Id : guide-worker-profile
Category : 작업자
Name : 대원 프로필
Description : |
  현재 현장 팀은 정확히 4명입니다. 각 대원은 이름, 텍스트 초상, 메모, 성격, 퍽, 전문 역할, 스킬, 여섯 역량, 피로, 경력, 부상, 신뢰, 자존심, 권위, 일일 산출을 가집니다. 이 초기값은 task-system.json에서 정의되고 캠페인 중 변화값은 저장 데이터에 보존됩니다.

  대원 목록은 상태, 피로, 성격, 기본급, 담당 작업, 신뢰와 최근 기록을 보여줍니다. 상세 화면은 역량 레이더, 퍽, 전체 작업 이력을 제공합니다. 현재 Skill, Pride, Authority는 데이터로 존재하지만 직접적인 결과 공식에는 아직 연결되지 않았습니다.

## guide-competencies

Id : guide-competencies
Category : 작업자
Name : 전문 역할과 여섯 역량
Description : |
  전문 역할은 기술, 분석, 관리, 적응의 네 종류이며 역할 일치 여부는 피로와 사고 확률에 영향을 줍니다. 별도로 모든 대원은 0~7 범위의 여섯 역량을 가집니다.

  역량 순서
  0 기지공학
  1 과학탐사
  2 자원운용
  3 환경적응
  4 생명유지
  5 지휘교섭

  작업은 이 중 1~3개를 요구합니다. 점수 4가 표준, 7이 탁월입니다. 전문 역할이 일치해도 요구 역량 평균이 낮으면 산출이 떨어질 수 있고, 역할이 달라도 요구 역량이 높으면 산출 자체는 높을 수 있습니다. 다만 역할 불일치 피로와 사고 가산은 그대로 적용됩니다.

## guide-fatigue-injury

Id : guide-fatigue-injury
Category : 작업자
Name : 피로·상태·사고
Description : |
  피로는 0~100입니다. 표시 상태는 0~29 정상, 30~54 피로, 55~79 과로, 80~100 소진입니다. 피로가 높을수록 낮은 산출 확률과 사고 확률이 올라갑니다.

  기본 사고 확률
  • 피로 55~79: 10%
  • 피로 80 이상: 28%
  • 전문 역할 불일치: 위 확률에 +6%

  사고가 나면 2~4일 부상을 입고 작업 진행 0.5일을 잃지만 담당자는 유지됩니다. 부상 상태로 하루를 시작하면 작업하지 않고 남은 부상일이 1 감소합니다. 네 대원이 동시에 부상이어도 그 자체로 운영 기록이 종료되지는 않습니다.

## guide-rest-regeneration

Id : guide-rest-regeneration
Category : 작업자
Name : 휴식과 재생
Description : |
  휴식 예약은 다음 하루의 작업을 쉬게 하고 피로를 18 회복합니다. 회복은 즉시가 아니라 다음날 처리에서 발생하며 현재 작업 담당은 유지됩니다. 부상 중이거나 이미 휴식이 예약된 대원에게는 다시 예약할 수 없습니다.

  재생 기본 비용은 3자원입니다. 능력 인계 +5, 퍽 인계 +4를 선택할 수 있어 모두 보존하면 12자원을 선불로 냅니다. 인계하지 않은 능력과 퍽은 해당 자리의 초기 데이터로 돌아갑니다. 피로·부상·휴식·경력은 항상 초기화되어 월급이 다시 1부터 시작합니다. 성격은 기존 성격 60% 유지, 나머지 40%는 다른 팀 성격 중 무작위입니다. 담당 작업은 유지되며 문맥 비용은 붙지 않습니다.

## guide-career-payroll

Id : guide-career-payroll
Category : 작업자
Name : 경력과 기본급
Description : |
  대원은 실제 작업 처리를 한 번 할 때마다 경력 1을 얻습니다. 주 작업과 병행 작업을 모두 수행하면 같은 날 두 번 오를 수 있습니다. 기본 월급은 대원당 1자원이며 경력 20마다 +1자원 상승합니다.

  계산식
  개인 기본급 = 1 + floor(경력 / 20) × 1
  팀 급여 = 네 대원의 개인 기본급 합계

  DAY 30·60·90 작업이 끝난 뒤 팀 급여를 자원에서 차감합니다. 대기, 휴식, 부상 중인 대원도 고용 상태이므로 급여 대상입니다. 재생으로 경력을 0으로 초기화하면 다음 급여도 낮아집니다.

## guide-trust-personality

Id : guide-trust-personality
Category : 작업자
Name : 신뢰와 성격
Description : |
  신뢰는 대원이 플레이어인 운영 담당자를 어떻게 보는지 나타내는 0~100 값입니다. 현재는 대원 목록, 상세, 메신저에서 수치와 관계 설명을 표시하고 저장하지만 명령 수용이나 능력치에는 아직 영향을 주지 않습니다.

  성격은 외부 데이터로 정의됩니다. 현재 원칙적, 분석적, 다정함, 대담함 네 성격이 있으며 동일한 상태·작업 정보도 메신저 문장 표현이 달라집니다. 현재 성격 효과는 말투 차이에 한정됩니다. 향후 선택 성향, 갈등, 자율 행동에 연결할 수 있도록 분리된 데이터입니다.

## guide-perks

Id : guide-perks
Category : 작업자
Name : 퍽과 미구현 효과
Description : |
  각 대원은 초기 퍽 목록을 가지고 상세 화면에 표시됩니다. 현재 퍽은 프로필 데이터이며 작업 산출, 사고, 피로에 실제 보정을 주지 않습니다. 작업 중 새 퍽을 얻거나 장점·단점이 발생하는 시스템도 아직 구현되지 않았습니다.

  개발 방향은 작업 결과에 따라 관련 퍽을 확률적으로 획득하고, 변화가 처음에는 ?로 숨겨진 뒤 교류를 통해 발견되도록 하는 것입니다. 이 항목은 현재 동작과 계획을 구분하기 위한 명세이며, 표시된 퍽 이름만 보고 수치 효과가 있다고 가정해서는 안 됩니다.

## guide-learned-assignment

Id : guide-learned-assignment
Category : 자동화
Name : 학습 자동 배정
Description : |
  플레이어가 작업자를 수동으로 배정하면 게임은 작업 종류, 요구 역할, 난이도, 위험도, 중요도의 조합과 선택한 대원을 규칙으로 저장합니다. 같은 조건의 새 작업이 대기 상태가 되면 해당 대원이 사용 가능할 때 자동으로 주 작업에 배정됩니다. 같은 조건을 다시 수동 배정하면 규칙의 담당자와 갱신 횟수가 업데이트됩니다.

  학습 규칙은 캠페인 저장에 포함되며 내정보에서 확인할 수 있습니다. 예약이 있는 작업, 선행 관계가 끝나지 않은 작업, 이미 주 작업 중인 대원은 자동 배정 대상에서 제외됩니다.

## guide-competency-auto

Id : guide-competency-auto
Category : 자동화
Name : 적성 자동 배정
Description : |
  적성 자동 배정은 옵션을 켰을 때 아직 담당자와 예약이 없는 대기 작업에 가장 높은 요구 역량 배율의 대원을 선택합니다. 배율이 같으면 피로가 낮은 대원을 우선합니다. 부상·휴식 중이거나 이미 주 작업을 가진 대원은 제외됩니다.

  하루 우선순위는 예약 배정 → 학습 자동 배정 → 적성 자동 배정입니다. 따라서 플레이어의 명시적 예약과 학습된 습관이 일반 적성 추천보다 우선합니다. 자동화는 선행 작업과 선행 일이 완료되지 않은 후행 작업을 미리 잡지 않습니다. 설정은 캠페인 저장에 보존됩니다.

## guide-resources

Id : guide-resources
Category : 자원과 운영
Name : 자원의 수입과 지출
Description : |
  캠페인은 자원 12로 시작합니다. 현재 자원은 하나의 통합 정수이며 일 완료 보상과 메일로 얻고, 마감 패널티·재생 시술·월 급여로 소비합니다.

  주요 기본값
  • 생성형 사이드 미션 보상: 6~10
  • 재생 시술: 3
  • 개인 초기 월 기본급: 1
  • 소프트·하드 패널티: 각 일 데이터 또는 생성 밸런스 사용

  자원은 어떤 차감에서도 0 아래로 내려가지 않습니다. 자원 0은 즉시 운영 기록을 종료하며 현재 부채나 미지급 급여 상태는 없습니다. 경제 밸런스와 추가 사용처는 개발 중입니다.

## guide-critical-events

Id : guide-critical-events
Category : 상호작용
Name : [!중요!] 선택 이벤트
Description : |
  중요 선택 이벤트는 메일 제목에 [!중요!]로 표시됩니다. 도착일을 포함한 7일 응답 창 동안은 날짜를 진행할 수 있지만, 마지막 응답일에는 선택하기 전까지 다음날로 진행할 수 없습니다. 선택을 마치면 후속 상황은 2~3일 뒤에 도착하며 같은 고리가 끝나기 전에는 다른 중요 이벤트가 끼어들지 않습니다.

  각 선택지는 표시된 가능성에 따라 결과를 굴립니다. 결과는 자원, 특정 대원 또는 전원의 피로, 이후 작업의 실패·성공 확률에 영향을 줄 수 있습니다. 작업 성공률 보정은 대성공 확률을 바꾸지 않고 실패 확률과 일반 성공 확률 사이에서 이동하며 한 회차 동안 누적됩니다.

## guide-mail

Id : guide-mail
Category : 상호작용
Name : 통신 메일
Description : |
  메일은 ArrivalDay가 된 날부터 도착합니다. 읽지 않은 메일은 [NEW]로 표시되고 읽었거나 처리한 메일보다 위에 정렬됩니다. 같은 읽음 상태에서는 최신 도착일이 먼저입니다.

  일반 자원 메일은 개별 처리합니다. 일정을 증감하는 일반 미니 사건은 개별 메일로 표시하지 않고 매주 월요일 `주간현장 현황공유` 한 통에 모읍니다. 각 안건을 승인하면 그때 일정에 반영되고 무시하면 일정 변화 없이 닫힙니다.

  [!중요!] 미션, 사장실 업무 문의, 미니작업을 부여하는 미션, 제안 결과, 검진 결과는 각각 별도 메일을 유지합니다. 같은 효과는 한 번만 적용되며 미래 메일은 미리 처리할 수 없습니다.

## guide-messenger

Id : guide-messenger
Category : 상호작용
Name : 메신저와 대원 보고
Description : |
  메신저에서는 대원별 현재 상태와 작업 현황을 질문할 수 있습니다. 질문과 답변은 하나의 대화 항목으로 저장됩니다. 작업 산출, 완료, 사고 같은 TaskRecord도 같은 대원의 날짜순 대화 흐름에 합쳐 표시됩니다. 새 기록 수는 바탕화면 배지에 반영되고 메신저를 열면 확인 처리됩니다.

  상태 답변은 피로, 부상, 휴식 예정, 신뢰 관계를 설명합니다. 작업 답변은 주 작업과 병행 작업의 이름과 진행률을 보고합니다. 성격은 같은 사실의 표현을 바꾸지만 현재 선택지나 관계 수치를 변화시키지는 않습니다.

## guide-reports

Id : guide-reports
Category : 상호작용
Name : 보고서와 마일스톤
Description : |
  보고서는 완료·진행·대기·잠김 작업 수, 지연, 고위험 작업, 과로 또는 부상 대원 수와 최근 결과를 요약합니다. 마일스톤 앱은 시작되었거나 끝난 일을 최신 활동 순으로 누적하는 프로젝트 히스토리입니다. 각 일의 기간과 소요 일수, 참여자, 완료 작업과 누적 산출, 획득 자원, 성과·기록·사건 로그를 보여줍니다. 아직 시작하지 않은 계획은 간트에서 확인합니다. 간트는 일과 작업의 시간 배치, 오늘, 마감선, 실제 진행, 예상 잔여, 담당자, 선행 화살표를 함께 보여줍니다.

  완료 막대는 실제 시작일과 완료일에 고정됩니다. 미완료 예상 막대는 현재 상태가 바뀔 때 다시 계산되므로 확정 예약이 아닙니다. 세 화면은 같은 시뮬레이션 상태를 서로 다른 의사결정 관점으로 표현합니다.

## guide-codex

Id : guide-codex
Category : 상호작용
Name : 도감의 역할
Description : |
  도감은 플레이어용 가이드이자 현재 구현 명세입니다. 카테고리 헤더를 눌러 항목을 접거나 펼칠 수 있습니다. 기본 가이드 항목은 처음부터 공개되며, 생성형 사이드 미션을 완료하면 그 임무에 사용된 형용사·대상·행동 단어가 별도 카테고리에 해금됩니다.

  가이드의 수치는 task-system.json의 현재 기본값을 설명합니다. 밸런스 데이터가 바뀌면 이 문서도 같은 변경에서 갱신해야 합니다. '현재', '아직', '향후'라는 표현은 구현된 효과와 계획된 효과를 구분하기 위한 개발 명세 표기입니다.

## guide-desktop

Id : guide-desktop
Category : 시스템과 데이터
Name : 운영 데스크와 창
Description : |
  바탕화면 아이콘으로 통신, 간트, 마일스톤, 대원, 보고서, 도감, 메신저, 내정보, 옵션을 엽니다. 이미 열린 앱의 바탕화면 아이콘을 누르면 최소화가 해제되고 창이 가장 위로 올라옵니다. 창은 포커스 순서, 이동, 최소화, 닫기, 우하단 크기 조절을 지원합니다. 두 손가락 핀치는 선택한 창을 중심 기준으로 확대·축소하고 중심 이동으로 창도 함께 옮깁니다.

  화면 배율은 1.0×, 1.4×, 1.8×, 2.2×이며 기본은 1.8×입니다. 배율은 글자뿐 아니라 버튼, 여백, 패널과 터치 영역에 함께 적용됩니다. Escape는 가장 앞의 열린 창부터 닫습니다. 창 배치와 배율은 캠페인과 별도의 데스크 설정으로 저장됩니다.

## guide-save

Id : guide-save
Category : 시스템과 데이터
Name : 저장 데이터
Description : |
  캠페인 저장에는 날짜, 자원, 일과 작업 상태, 대원, 메일과 주간 안건 결정, 시스템 로그, 학습 배정 규칙, 발견한 임무 단어·대원 특성, 중간평가 여부, 적성 자동 배정, 제안 후보, 검진 결과, 중요 사건 상태가 포함됩니다. 앱 일시정지와 종료, 주요 조작 뒤에 PlayerPrefs 기반 JSON으로 저장합니다. 현재 캠페인 schema는 2이고, 데스크 schema 1은 화면 배율, 메신저 확인 수, 열린 창의 위치·크기·최소화·순서를 별도로 보존합니다.

  로드 시 구버전 생성 임무의 계층, 누락된 역량·초상·메모·성격을 현재 외부 데이터로 보정합니다. 손상되거나 지원하지 않는 schema의 저장은 불러오지 않습니다. 다중 저장 슬롯과 클라우드 저장은 현재 없습니다.

## guide-data-source

Id : guide-data-source
Category : 시스템과 데이터
Name : 외부 데이터 명세
Description : |
  게임 정의와 밸런스의 기준 파일은 Assets/MilestonePrototype/Resources/task-system.json입니다. 캠페인 길이, 시작 자원, 모든 밸런스 수치, 고정 일과 작업, 대원 초기값, 메일, 임무 단어, 기본 도감이 여기에 있습니다. 런타임은 SchemaVersion=1과 필수 배열, 대원 수 4명, 역량 범위와 참조 가능한 작업 데이터를 검증합니다.

  자주 조정하는 값은 코드 상수로 복제하지 않는 것이 원칙입니다. 필드 이름은 C# 직렬화 모델과 정확히 일치해야 합니다. 도감은 사용자 설명이면서 데이터 변경 시 함께 수정해야 하는 미니 기획서입니다.

## guide-patch

Id : guide-patch
Category : 시스템과 데이터
Name : 패치와 현재 기술 경계
Description : |
  현재 base APK는 version 9입니다. ProjectW.Bootstrap이 개발 채널을 확인해 패치 매니페스트, HotUpdate DLL, task-system.json과 AOT 메타데이터의 크기·SHA-256을 검증하고 staging에서 current 슬롯으로 승격합니다. 시작 실패 시 previous 슬롯으로 롤백하며 네트워크 실패 시 마지막 정상 패치 또는 APK 내장본을 사용합니다.

  게임 규칙과 매니페스트에 포함된 데이터는 패치할 수 있습니다. Unity·패키지·네이티브·Contracts의 새 AOT 표면은 새 APK가 필요합니다. 원격 Addressables는 아직 없어 이미지, 씬, 오디오 같은 대형 콘텐츠 패치는 현재 범위 밖입니다.

# RandomTaskAdjectives

## adjective-stable

Id : adjective-stable
Text : 안정적인
Risk : Low
Difficulty : 0

## adjective-unstable

Id : adjective-unstable
Text : 불안정한
Risk : Medium
Difficulty : 1

## adjective-dangerous

Id : adjective-dangerous
Text : 위험한
Risk : High
Difficulty : 2

## adjective-very-unstable

Id : adjective-very-unstable
Text : 매우 불안정한
Risk : High
Difficulty : 3

# RandomTaskTargets

## target-bedrock

Id : target-bedrock
Text : 암반
Role : Analysis
RequiredCompetencies : [1]
Difficulty : 1

## target-strata

Id : target-strata
Text : 지층
Role : Analysis
RequiredCompetencies : [1]
Difficulty : 1

## target-aggregate

Id : target-aggregate
Text : 골재
Role : Tech
RequiredCompetencies : [2]
Difficulty : 0

## target-equipment

Id : target-equipment
Text : 장비
Role : Tech
RequiredCompetencies : [0]
Difficulty : 1

## target-supply-schedule

Id : target-supply-schedule
Text : 보급 일정
Role : Management
RequiredCompetencies : [2]
Difficulty : 1

## target-field-crew

Id : target-field-crew
Text : 현장 인력
Role : Management
RequiredCompetencies : [5]
Difficulty : 0

## target-unknown-zone

Id : target-unknown-zone
Text : 미지 구역
Role : Adaptation
RequiredCompetencies : [3,1]
Difficulty : 2

## target-weather-change

Id : target-weather-change
Text : 기상 변화
Role : Adaptation
RequiredCompetencies : [3]
Difficulty : 1

# RandomTaskActions

## action-survey

Id : action-survey
Text : 탐사
Role : Analysis
RequiredCompetencies : [1]
Difficulty : 1

## action-analysis

Id : action-analysis
Text : 분석
Role : Analysis
RequiredCompetencies : [1]
Difficulty : 0

## action-transport

Id : action-transport
Text : 운반
Role : Tech
RequiredCompetencies : [2,3]
Difficulty : 0

## action-maintenance

Id : action-maintenance
Text : 정비
Role : Tech
RequiredCompetencies : [0]
Difficulty : 1

## action-coordinate

Id : action-coordinate
Text : 조정
Role : Management
RequiredCompetencies : [5,2]
Difficulty : 1

## action-inspection

Id : action-inspection
Text : 점검
Role : Management
RequiredCompetencies : [4]
Difficulty : 0

## action-response

Id : action-response
Text : 대응
Role : Adaptation
RequiredCompetencies : [3,4]
Difficulty : 1

## action-pioneer

Id : action-pioneer
Text : 개척
Role : Adaptation
RequiredCompetencies : [3,0]
Difficulty : 2

# CriticalEvents

## solar-storm-chain

Id : solar-storm-chain
StartDay : 12
FirstNodeId : storm-warning

# EventNodes

## solar-storm-chain/storm-warning

EventId : solar-storm-chain
Id : storm-warning
From : 궤도 기상 관제
Subject : 태양 폭풍 접근
Body : 고에너지 입자 폭풍이 18시간 안에 기지를 통과합니다. 전력망을 차폐하면 비축 부품을 소모하고, 현장 보강은 대원 부담과 실패 위험을 감수해야 합니다.
Risk : High

## solar-storm-chain/distress-call

EventId : solar-storm-chain
Id : distress-call
From : 외곽 채굴 전초기지
Subject : 폭풍 속 구조 요청
Body : 폭풍으로 고립된 전초기지가 구조 전력을 요청했습니다. 우리도 여유가 없지만, 응답하면 이후 물자 회수 기회가 생길 수 있습니다.
Risk : High

## solar-storm-chain/supply-wreck

EventId : solar-storm-chain
Id : supply-wreck
From : 개척 운영 본부
Subject : 표류 보급선 처리
Body : 폭풍이 지나간 뒤 손상된 보급선이 탐지되었습니다. 즉시 회수하면 큰 자원을 얻을 수 있지만 잔류 방사선 위험이 있습니다.
Risk : Medium

# EventChoices

## solar-storm-chain/storm-warning/spend-shielding

EventId : solar-storm-chain
NodeId : storm-warning
Id : spend-shielding
Text : 비축 부품으로 전력망을 차폐한다
Forecast : 확정 비용: 자원 -2 · 전원 피로 -5 · 작업 성공률 +5%p

## solar-storm-chain/storm-warning/field-reinforcement

EventId : solar-storm-chain
NodeId : storm-warning
Id : field-reinforcement
Text : 대원을 보내 현장에서 보강한다
Forecast : 70% 안정화 / 30% 과부하 · 자원과 피로, 성공률 변동

## solar-storm-chain/distress-call/send-rescue

EventId : solar-storm-chain
NodeId : distress-call
Id : send-rescue
Text : 구조대를 보낸다
Forecast : 60% 구조 성공 / 40% 장기 수색 · 전원 피로 증가

## solar-storm-chain/distress-call/hold-position

EventId : solar-storm-chain
NodeId : distress-call
Id : hold-position
Text : 기지를 지키며 통신 안내만 제공한다
Forecast : 확정 효과: 전원 피로 -4 · 작업 성공률 -2%p

## solar-storm-chain/supply-wreck/salvage-now

EventId : solar-storm-chain
NodeId : supply-wreck
Id : salvage-now
Text : 지금 회수한다
Forecast : 50% 자원 +5 / 50% 자원 +2·전원 피로 +12

## solar-storm-chain/supply-wreck/mark-and-withdraw

EventId : solar-storm-chain
NodeId : supply-wreck
Id : mark-and-withdraw
Text : 좌표만 기록하고 철수한다
Forecast : 확정 효과: 자원 +1 · 전원 피로 -6

# EventOutcomes

## solar-storm-chain/storm-warning/spend-shielding/0

EventId : solar-storm-chain
NodeId : storm-warning
ChoiceId : spend-shielding
OutcomeIndex : 0
Weight : 100
Text : 차폐가 안정적으로 작동했다.
ResourceDelta : -2
CrewIndex : -1
FatigueDelta : -5
SuccessChanceDelta : 5
NextNodeId : distress-call

## solar-storm-chain/storm-warning/field-reinforcement/0

EventId : solar-storm-chain
NodeId : storm-warning
ChoiceId : field-reinforcement
OutcomeIndex : 0
Weight : 70
Text : 현장 보강이 제시간에 끝났다.
ResourceDelta : -1
CrewIndex : -1
FatigueDelta : 8
SuccessChanceDelta : 3
NextNodeId : distress-call

## solar-storm-chain/storm-warning/field-reinforcement/1

EventId : solar-storm-chain
NodeId : storm-warning
ChoiceId : field-reinforcement
OutcomeIndex : 1
Weight : 30
Text : 보강 중 계통이 손상되어 복구 부담이 커졌다.
ResourceDelta : -3
CrewIndex : -1
FatigueDelta : 15
SuccessChanceDelta : -5
NextNodeId : distress-call

## solar-storm-chain/distress-call/send-rescue/0

EventId : solar-storm-chain
NodeId : distress-call
ChoiceId : send-rescue
OutcomeIndex : 0
Weight : 60
Text : 생존자를 확보하고 보급 좌표를 받았다.
ResourceDelta : 1
CrewIndex : -1
FatigueDelta : 10
SuccessChanceDelta : 4
NextNodeId : supply-wreck

## solar-storm-chain/distress-call/send-rescue/1

EventId : solar-storm-chain
NodeId : distress-call
ChoiceId : send-rescue
OutcomeIndex : 1
Weight : 40
Text : 수색이 길어져 인력과 물자를 소모했다.
ResourceDelta : -2
CrewIndex : -1
FatigueDelta : 18
SuccessChanceDelta : -3
NextNodeId : supply-wreck

## solar-storm-chain/distress-call/hold-position/0

EventId : solar-storm-chain
NodeId : distress-call
ChoiceId : hold-position
OutcomeIndex : 0
Weight : 100
Text : 기지는 안전했지만 현장 사기는 가라앉았다.
ResourceDelta : 0
CrewIndex : -1
FatigueDelta : -4
SuccessChanceDelta : -2
NextNodeId : supply-wreck

## solar-storm-chain/supply-wreck/salvage-now/0

EventId : solar-storm-chain
NodeId : supply-wreck
ChoiceId : salvage-now
OutcomeIndex : 0
Weight : 50
Text : 온전한 보급 컨테이너를 확보했다.
ResourceDelta : 5
CrewIndex : -1
FatigueDelta : 4
SuccessChanceDelta : 2
NextNodeId : ""

## solar-storm-chain/supply-wreck/salvage-now/1

EventId : solar-storm-chain
NodeId : supply-wreck
ChoiceId : salvage-now
OutcomeIndex : 1
Weight : 50
Text : 회수는 성공했지만 잔류 방사선 대응에 지쳤다.
ResourceDelta : 2
CrewIndex : -1
FatigueDelta : 12
SuccessChanceDelta : -2
NextNodeId : ""

## solar-storm-chain/supply-wreck/mark-and-withdraw/0

EventId : solar-storm-chain
NodeId : supply-wreck
ChoiceId : mark-and-withdraw
OutcomeIndex : 0
Weight : 100
Text : 안전한 부품만 회수하고 사건을 마무리했다.
ResourceDelta : 1
CrewIndex : -1
FatigueDelta : -6
SuccessChanceDelta : 0
NextNodeId : ""
