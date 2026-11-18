# Daily Rule Simulation Specification

## Document Control

- Version: 1.3
- Status: Authoring specification
- Action: Update
- SSOT Change: Yes
- Rationale: Align the deterministic day cycle with the Stakeholder Request core loop, including
  future clarification delivery and one-time weighted stakeholder-fit resolution.
- SSOT relationship: This document explains how to simulate the day cycle defined by
  `Assets/Specification/System/TaskSystem.md` and implemented by `MilestoneSimulation`.
- Runtime data source: `Assets/MilestonePrototype/Resources/task-system.json`
- Purpose: Let a designer reproduce, inspect, and balance one or many campaign days without running
  the Unity presentation layer.

## Scope

This specification covers deterministic campaign-day simulation for authored and generated Work,
Task, Crew, Mail, Critical Event, and Balance data. It defines processing order, formulas, stochastic
rolls, invariants, scenario inputs, and required output metrics.

It does not define UI rendering, patch download behavior, save-file migration, or remote Addressables.
When this document and `TaskSystem.md` disagree, `TaskSystem.md` is authoritative.

## Simulation Objectives

A conforming simulator must support three uses:

1. **Single-day trace:** explain every decision, roll, state change, and resource delta in order.
2. **Fixed-seed campaign replay:** produce identical results from identical data, scenario, and seed.
3. **Monte Carlo balance run:** execute many seeds and summarize distributions rather than one result.

The simulator should answer whether authored content is feasible, varied, economically sustainable,
and distributed across roles and competencies. It must not optimize the player policy unless that
policy is explicitly supplied as scenario input.

## Required Inputs

### Gameplay definition

- Complete `TaskSystemData` document with `SchemaVersion = 1`.
- All validation rules from `TaskSystemDataLoader.Validate` must pass before simulation.
- Array order is significant for deterministic automatic assignment and content selection.

### Scenario

| Field | Type | Required | Meaning |
|---|---|---:|---|
| `ScenarioId` | string | yes | Stable identity for result comparison |
| `Seed` | integer | yes | Random stream seed for a replay |
| `MaximumDay` | integer | yes | Stop day for the simulation, not a victory day |
| `PolicyId` | string | yes | Stable identity of the player-decision policy |
| `CompetencyAutoAssignment` | boolean | yes | Initial automatic-assignment setting |
| `InitialOverrides` | object | no | Explicit starting state overrides |
| `ScheduledActions` | array | no | Player actions performed before a named day advances |

`MaximumDay` is a test boundary only. The campaign itself ends only when resources reach zero.

### Scheduled action vocabulary

A scenario may issue these actions before `AdvanceDay`:

- assign or unassign a primary Task;
- assign a parallel Task;
- reserve, change, or cancel a Task start;
- schedule rest;
- request one or all medical checkups;
- regenerate a crew member with explicit inheritance choices;
- read or resolve mail;
- approve or ignore one item in a Monday `주간현장 현황공유` mail;
- choose a critical-event option;
- open a Stakeholder Request, select Task interpretations, fill available worker slots, confirm its
  plan, or request an authored clarification call/meeting;
- enable or disable competency automatic assignment; set Work priority, `긴급!`, and `총력!` flags.

Rejected actions must be recorded with their rejection reason. A simulator must never silently repair
an invalid policy command.

## Randomness Contract

- One pseudo-random stream is created from `Seed` for a run.
- Random calls occur only in the same order as the runtime day cycle.
- A trace records the roll purpose, range, result, threshold, and resulting branch.
- Monte Carlo runs use distinct seeds and retain the mapping between seed and result.
- Results must never be described as deterministic unless the seed and input document are fixed.

Changing array order, adding an earlier random call, or changing a policy may change all later rolls.
Comparison reports therefore identify both the gameplay-data hash and simulator/runtime revision.

## Canonical Day Processing Order

For current day `D`, a conforming simulation performs the following sequence.

1. If resources are zero, return an empty report and do not mutate state.
2. Determine weekday: Day 1 is Monday and the seven-day cycle repeats.
3. Determine whether the day is a weekend or regular Friday and which assigned workers belong to a
   `총력!` Work.
4. Apply due scheduled assignments.
5. Apply player-defined Work-priority preemption for `긴급!` and related focus rules.
6. Apply learned assignment rules.
7. Apply competency automatic assignment.
8. Snapshot each crew member's paused condition for the day.
9. Record regular Friday medical results when applicable.
10. Decrement existing injury duration.
11. Apply weekend recovery, medical leave, or scheduled-rest recovery.
12. Process assigned incomplete Tasks, primary Tasks before parallel Tasks.
13. Charge payroll when `D` is a configured payroll interval.
14. Increment the calendar from `D` to `D + 1`.
15. Resolve due Stakeholder Request calls/meetings, increase clarity, reveal newly eligible authored
    guidance, and resolve a waiting delivery score when all required Tasks were already complete.
16. On Monday, collect due ordinary schedule-changing mini incidents into one
    `주간현장 현황공유` mail. Do not apply their schedule deltas before item approval.
17. Deliver due medical results.
18. Refresh Work and Task states.
19. Apply soft- and hard-deadline results against the new current day.
20. Issue the midpoint review when due.
21. Refresh states again.
22. Generate structured inbound boss requests when eligible.
23. Deliver scheduled critical-event mail.
24. Trigger an eligible critical event.
25. Persist the report and derived state.

The order is observable. In particular, daily output and payroll occur before the day increments,
while deadline checks and newly generated side missions occur after it increments.

## Calendar and Capacity Rules

- Day 1 is Monday.
- Saturday and Sunday produce no Task output except for workers assigned to a `총력!` Work.
- On Friday, regular checkups are free and multiply Task output by `0.5`, except for workers assigned
  to a `총력!` Work; those workers skip the checkup and produce normal output.
- A worker can hold at most one active primary Task and any number of explicitly forced parallel Tasks.
- Parallel Tasks always add their configured fatigue and dissatisfaction costs.
- Completed assignments remain historical ownership but consume no current capacity.
- The planning horizon is at least 30 days beyond the current day.

## Task Progress Formula

For an unpaused assigned Task, calculate:

```text
roleProgress = ParallelProgressDays when parallel, otherwise PrimaryProgressDays
competencyMultiplier = required-grade rule below
specialtyMultiplier = 1.0 when matched, otherwise 0.8
dissatisfactionMultiplier = linear configured penalty from the worker's dissatisfaction
conditionMultiplier = Failure, Success, or GreatSuccess multiplier
workdayMultiplier = 0.5 on regular Friday checkup, otherwise 1.0

dailyProgress = Crew.DailyOutput
              * roleProgress
              * competencyMultiplier
              * specialtyMultiplier
              * dissatisfactionMultiplier
              * conditionMultiplier
              * workdayMultiplier
```

The result is capped at the remaining effective work. Effective work is:

```text
EffectiveRequiredWork = RequiredWork + ContextCostDays
```

If the direct predecessor Task is incomplete, total progress cannot exceed:

```text
EffectiveRequiredWork * Balance.PrerequisiteProgressLimit
```

Automatic assignment may not exploit this early-progress allowance. It requires every Work and Task
predecessor to be complete.

## Competency Grades and Multiplier

- Every one of the twelve workers owns all six competency values.
- Runtime values `2 / 3 / 4 / 5 / 6 / 7` display as `F / D / C / B / A / S`.
- A Task requires one to three unique competency indices from `0` through `5` and may provide one
  required grade for each index.
- Average `worker value / required value` and clamp the result to `0.4..1.4`. A shortfall lowers
  efficiency but does not reject assignment.
- Legacy Tasks without explicit required grades use value `4` (C) as their baseline.

The six competency indices are:

| Index | Meaning |
|---:|---|
| 0 | Base engineering |
| 1 | Science exploration |
| 2 | Resource operations |
| 3 | Environmental adaptation |
| 4 | Life support |
| 5 | Command and diplomacy |

## Daily Outcome Distribution

The runtime derives Failure and Great Success probabilities from fatigue at the start of Task
execution. Success is the remainder.

| Fatigue | Failure | Success | Great Success |
|---:|---:|---:|---:|
| 0 | `FreshLowOutputChance` | remainder | `FreshHighOutputChance` |
| 50 | `LowOutputChance` | remainder | `HighOutputChance` |
| 100 | `ExhaustedLowOutputChance` | remainder | `ExhaustedHighOutputChance` |

Interpolate linearly from 0 to 50 and separately from 50 to 100. Apply accumulated critical-event
success-chance modification to Failure versus Success without changing Great Success. Default output
multipliers are `LowOutputMultiplier`, `1.0`, and `HighOutputMultiplier`.

## Fatigue, Experience, and Accident Resolution

After progress is calculated:

```text
fatigueGain = MatchingFatigue when crew specialty matches Task.RequiredRole
            = MismatchedFatigue otherwise
fatigueGain += ParallelFatigue when parallel
fatigueGain += SoftDeadlineFatigue when parent Work missed its soft deadline

dissatisfactionGain = SpecialtyMismatchDissatisfaction when specialty mismatches
                    + each missing grade step * CompetencyGapDissatisfaction
                    + ParallelDissatisfaction when parallel
```

- Clamp fatigue to `0..100`.
- Clamp dissatisfaction to `0..100`. It is independent from fatigue; configured rest and weekend
  recovery reduce it separately.
- Add one Experience for every processed Task. A primary and parallel Task can each add one on the
  same day.
- Base accident chance is `MediumFatigueAccidentChance` at fatigue 55–79 and
  `HighFatigueAccidentChance` at fatigue 80–100.
- Add `MismatchAccidentChance` when specialty does not match.
- An accident assigns 2–4 injury days, preserves assignment, and removes `0.5` Task progress down to
  a minimum of zero.
- Completion is checked only when the accident branch does not occur.

## Rest, Injury, and Medical Rules

- A worker injured or scheduled to rest at the start of a day produces no output that cycle.
- Scheduled rest restores `RestRecovery`, clears the rest flag, and preserves assignments.
- Weekend rest restores `WeekendFatigueRecovery` and `WeekendMentalRecovery` for workers not assigned
  to a `총력!` Work.
- Each injured crew member receives one weekend recovery roll using
  `WeekendInjuryRecoveryChance`.
- An unscheduled checkup costs `UnscheduledCheckupResourceCost` and pauses the entire day.
- A regular Friday checkup is free and makes that Friday a half workday.
- Results arrive on the next Monday and do not update the visible medical file until downloaded.

## Work Completion, Rewards, and Deadlines

- A Work completes when every required child Task is complete.
- Completion grants `RewardCredits` once.
- When current day becomes greater than `SoftDeadline`, apply `SoftPenaltyCredits` once and mark the
  Work late.
- When current day becomes greater than `HardDeadline`, fail the Work, apply `HardPenaltyCredits`
  once, fail all incomplete child Tasks, and clear their assignments and reservations.
- Resource deductions clamp at zero.
- A Stakeholder Request waits for any pending clarification even when all three required Tasks are
  complete. Otherwise its last required Task completion resolves the weighted `60 / 30 / 10` fit
  score once. A due clarification may resolve that waiting score at step 15. Hard-deadline failure
  resolves no fit score.
- Required and optional Work failures are reported as Work outcomes and resource pressure. Neither
  is itself a run-ending condition; only resource depletion ends the run.

## Payroll and Run End

On every day divisible by `PayrollIntervalDays`:

```text
memberSalary = BaseSalary
             + floor(Experience / ExperiencePerSalaryIncrease) * SalaryIncrease
teamPayroll = sum(memberSalary)
```

Deduct team payroll after Task processing and clamp resources at zero. There is no victory state and
no fixed-duration campaign ending. A simulation stops when resources are zero or its scenario stop
condition is reached.

## Automatic Assignment Order

1. Due reservations.
2. Learned rules keyed by Task kind, role, difficulty, risk, and importance.
3. Competency automatic assignment, when enabled.

Competency automatic assignment processes Tasks in player-defined Work-priority order. It selects the eligible crew
member with the highest competency multiplier, then lowest fatigue, then earliest roster position.
It never interrupts work or consumes a future reservation.

## Minimum Trace Output

Each simulated day must record:

- input day, weekday, resources, and settings;
- player-policy actions and rejected actions;
- automatic assignments and their source;
- crew pre-state and post-state;
- every Task's base output, multipliers, probability thresholds, roll, progress, fatigue, accident,
  and completion result;
- payroll, rewards, penalties, mail, events, and resource deltas;
- Work and Task state transitions;
- generated content identities;
- resulting day and stop condition.

## Monte Carlo Report

A batch report should include at least:

- seed count and gameplay-data hash;
- median and percentile run length;
- resources over time, including bankruptcy probability by day;
- Work completion and failure rates;
- Task start delay and completion-duration distributions;
- soft- and hard-deadline miss rates;
- crew utilization, fatigue, injury, and regeneration rates;
- role and competency demand share;
- Failure, Success, Great Success, and accident rates;
- payroll, reward, and penalty totals;
- generated side-mission count, completion rate, and word usage;
- unreachable, never-selected, or extremely rare authored content.

Reports must show distributions and sample sizes. Averages alone are insufficient for highly random
or failure-prone content.

## Authoring Quality Gates

Recommended gates for a content batch are configurable rather than hard-coded, but must cover:

- no invalid references or dependency cycles;
- no required Work that is impossible under a declared baseline policy;
- deliberate role and competency coverage;
- acceptable deadline miss bands by difficulty tier;
- no single authored reward or penalty dominating the economy;
- no content whose arrival or reveal day occurs after its useful deadline;
- no critical-event node that is unreachable or has no terminating path;
- outcome weights greater than zero and a documented probability interpretation;
- enough compatible random words to avoid rapid repetition.

## Conformance Tests

A simulator is conforming when it passes fixed scenarios for:

1. weekday, Friday, weekend, and per-Work `총력!` processing;
2. primary and parallel capacity;
3. competency multiplier edge cases;
4. fatigue probability interpolation at 0, 50, and 100;
5. prerequisite 30% cap;
6. interruption and resumption context cost;
7. accident progress loss and preserved ownership;
8. soft deadline, hard deadline, reward, and payroll order;
9. reservation, learned, and competency assignment priority;
10. medical result delivery;
11. critical-event blocking and follow-up delivery;
12. fixed-seed replay equality;
13. resources reaching zero without becoming negative.
14. Monday field-status aggregation, per-item approval/ignore, and separate mission mail delivery.
