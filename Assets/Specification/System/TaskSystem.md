# Task System

## Document Control

- Version: 7.1
- Status: Approved for implementation
- Action: Update
- SSOT Change: Yes
- Rationale: Retire the player Proposal system, expand the field team to twelve workers, grade all
  six competencies from F through S, and add dissatisfaction as a workload/fit pressure distinct
  from physical fatigue. Add press-and-hold assignment previews, desktop progress reset, and fix
  Gantt today navigation. Continue to focus the core prototype on interpreting ambiguous stakeholder request mail,
  decomposing each accepted request into weighted Tasks, previewing worker fit, and
  earning a stakeholder-fit score from the delivered result. Clarification calls and meetings trade
  schedule slack for better request understanding. Existing operations systems remain supporting
  status dashboards rather than the primary player decision loop.
- Idea references: `IDEA.md` items 1, 2, 4, and 8

## Scope

This document defines:

- stakeholder requests delivered through mail
- request clarity, clarification calls and meetings, and the schedule tradeoff they create
- weighted Task decomposition and worker assignment slots
- stakeholder-fit scoring for completed request deliverables
- parent work items (`Work`)
- automatically dispatched execution units (`Task`)
- role depth charts, crew operating modes, fatigue limits, and operations styles
- work and task prerequisites
- soft and hard deadlines
- one-primary-task-per-worker daily capacity
- small parallel tasks paid for with additional fatigue
- interruption, resumption, and handover cost
- extension points for task outcomes, perks, and command acceptance
- external gameplay data ownership

Run-level inheritance, fate assets, and cross-run narrative rules are outside this version.

## Terms

### Work

A Work is a player-facing objective that groups one or more Tasks.

- A Work owns a soft deadline and a hard deadline.
- A Work can require other Works to be complete before it becomes available.
- Required Tasks determine Work completion.
- Work definitions and their generated prerequisite relations come from external gameplay data.

### Task

A Task is the smallest execution unit owned by one worker. Normal ownership is selected by the
staffing dispatcher from the player's organization plan; direct player assignment is an exception
intervention rather than the default daily interaction.

- A Task belongs to exactly one Work.
- A Task has a duration measured in half-day increments.
- A Task may depend on another Task. Before that predecessor completes, the successor can be
  assigned but cannot progress beyond 30% of its effective workload.
- An unassigned Task does not progress.
- Optional Tasks do not block Work completion.

### Stakeholder Request

A Stakeholder Request is an ambiguous inbound objective delivered through mail. Opening it shows a
request-only view containing the request, constraints, and deadline. `계획하기` explicitly enters the
separate planning board and makes its Work visible; neither action assigns or starts a Task by itself.

- A request owns a stakeholder, a brief, a hidden intent profile, an initial clarity value, and a
  delivery deadline measured in campaign days.
- The player converts the request into weighted Tasks and fills each Task's worker slot.
- The prototype default is three required Tasks with contribution weights `60 / 30 / 10`. Authored
  requests may name those Tasks differently, but their contribution weights must total `100`.
- A request's planned duration is expressed as `N` weeks for presentation and converted to campaign
  days for simulation. Clarification consumes days inside that same delivery window; it never moves
  the deadline automatically.
- The request is delivered when every required child Task is complete. Delivery resolves a
  stakeholder-fit score and preserves the request, Task plan, ownership, and score in history.

## Core Prototype Loop

The primary player loop is:

1. Receive an ambiguous Stakeholder Request by mail.
2. Inspect only the brief, revealed constraints, and deadline in the request view.
3. Either spend days clarifying the request or select `계획하기` with the current interpretation.
4. In the separate planning board, confirm the weighted Task plan and assign one available worker to every required Task
   slot. The initial prototype uses the authored `60 / 30 / 10` plan rather than free-form weights.
5. Advance days while assigned workers execute their Tasks.
6. When all required Tasks finish, deliver the combined result and receive a stakeholder-fit score.

The player's essential adjustment is how an ambiguous request is interpreted and staffed. Fatigue,
health, schedules, competencies, deadlines, mail, Gantt projections, and other existing systems
provide realistic decision context. They must not replace the request-planning decision with a
second competing core loop.

### Clarification

- Before confirming the Task plan, the player may request a stakeholder call or meeting when the
  request is not clear enough to staff confidently.
- A clarification action has an authored lead time in days and an authored clarity gain. The action
  is scheduled for `current day + lead time` and is shown on the request plan and calendar.
- Only one clarification may be pending for a request. The player may continue planning while it is
  pending, but may not confirm the plan before the scheduled clarification resolves.
- When the scheduled day begins, the interaction resolves, increases request clarity up to `100`,
  and reveals additional stakeholder-intent guidance from external data.
- Calls use a shorter lead time and smaller clarity gain. Meetings use a longer lead time and larger
  clarity gain. Exact values are request data, not hard-coded runtime constants.
- Clarification does not extend soft or hard deadlines. Every clarification therefore increases
  information while reducing the remaining execution window.
- Clarification is optional. Proceeding with low clarity is valid and may preserve schedule slack,
  but leaves more of the hidden intent unrevealed.

### Task Plan and Eligible Assignment Slots

- Every request Task owns a contribution weight, required role, one to three required competencies,
  and intent tags describing the interpretation embodied by its deliverable.
- A required Task slot must be filled before the request plan can start. Reassignment after work has
  started follows the existing interruption and handover rules.
- A worker is assignable when present and not blocked by injury or scheduled rest. Role or competency
  mismatch never prevents assignment; it lowers output and adds dissatisfaction while work is performed.
- Every worker remains visible in the assignment picker. Pressing briefly assigns them; pressing and
  holding previews required grades, expected efficiency, dissatisfaction, and parallel-work pressure.
- Stakeholder-request Tasks are never filled by organization automatic assignment, learned
  intervention rules, urgent preemption, or implicit fallback. Their staffing is the player's core
  decision and requires explicit confirmation.
- Once every required slot is valid, the player confirms the plan. Confirmed Tasks execute through
  the existing daily progress, fatigue, condition, interruption, and completion rules.

### Stakeholder-Fit Score

- A completed request is scored from `0` through `100` when its last required Task completes.
- Each Task receives a `0..100` fit result by comparing its authored intent tags with the request's
  hidden intent profile. Guidance revealed by clarification helps the player choose compatible Task
  interpretations but does not directly add score.
- The request's base fit is the weighted sum of Task fit results:

```text
base fit = sum(Task fit × Task contribution weight) / 100
```

- On-time delivery keeps the full base fit. Delivery after the soft deadline applies the existing
  externally-authored late multiplier. Hard-deadline failure produces no fit score.
- Ordinary daily Failure, Success, and Great Success outcomes affect completion speed and condition,
  not the hidden intent match. Worker eligibility and execution determine whether the plan can be
  delivered; interpretation determines how well it fits the stakeholder request.
- The resolved score is immutable history. The UI shows the total, the `60 / 30 / 10` contribution
  breakdown, the matched and missed intent guidance, delivery timing, and assigned workers.
- Campaign score is the sum of resolved stakeholder-fit scores. Resource-life day count remains an
  endurance statistic and is no longer the only score presented by the prototype.
- Restoring a campaign created before a Stakeholder Request was authored merges the missing request
  entry mail from current external data by stable mail ID. Existing mail and already-created request
  state are preserved, and the merge never duplicates a request mail.

## State Rules

Work states:

1. `Locked`: at least one predecessor Work is incomplete.
2. `Available`: predecessors are complete and no Task has begun.
3. `InProgress`: at least one Task has progress or an assignment.
4. `Completed`: all required Tasks are complete.
5. `Failed`: the hard deadline has passed before completion.

A Work may additionally own a reveal day or wait for mail acceptance. Before either gate opens, the
Work and all of its Tasks remain locked and are hidden from the Gantt. These gates do not replace
Work or Task predecessor checks.

Task states:

1. `Locked`: its Work or Task prerequisite is locked.
2. `Available`: it may receive an assignment. A newly assigned Task remains in this state until a
   daily cycle actually produces work.
3. `Active`: it has produced work on or after its recorded start day and still has an assignee.
4. `Complete`: its base duration and accumulated context cost are paid.
5. `Failed`: its parent Work failed.

Completion immediately refreshes dependent states. A newly unlocked Task can be assigned before the next day is advanced.

## Day and Assignment Rules

- One turn advances the calendar by one day.
- Day 1 is Monday. The seven-day week repeats Monday through Sunday.
- The game calendar starts on October 1. A year is always 365 days, February always has 28 days,
  and leap years do not exist. Months otherwise use their standard lengths.
- Calendar quarters follow the standard three-month groups: Q1 is January–March, Q2 is
  April–June, Q3 is July–September, and Q4 is October–December. Day 90 is a content/planning
  baseline, not a run limit.
- Payroll is charged every 30 days. Each crew member starts with a data-defined monthly base salary,
  and every configured block of accumulated Experience increases that salary by a configured amount.
- Experience is earned through daily Task execution. Regeneration resets Experience to zero, which
  also returns that crew member to the initial base salary. Payroll never reduces resources below zero.
- Regeneration always pays a base resource cost and offers independent ability and perk inheritance
  options for additional data-driven costs. Ability inheritance preserves specialty, skill,
  competencies, and daily output; perk inheritance preserves the current perk array. Any unselected
  layer returns to that roster slot's initial external-data definition.
- Perks are resolved through external `PerkDefinitions`. Each definition may independently multiply
  incident probability at the 0%, 50%, and 100% Task checkpoints, fatigue paid for work, ordinary
  rest recovery, and weekend recovery. All applicable perk values multiply together; they are not
  added or reduced to a single best/worst perk.
- Regeneration rerolls personality. The previous personality has a data-driven retention weight;
  when it is not retained, one of the other roster-defined personalities is selected uniformly.
- Assignment and Task context survive regeneration. Fatigue, injury, scheduled rest, Experience,
  and salary progression never survive, regardless of inheritance choices.
- The midpoint review occurs on day 45. It summarizes current Work completion and deadline state
  without ending the campaign or introducing an unstated pass/fail condition.
- The required final operation is revealed at the start of day 60. It is absent from the Gantt and
  cannot be assigned, reserved, or automatically assigned before that day.
- The operations desktop exposes an always-visible `다음날로` button at the bottom-right above the
  taskbar. It is disabled only after resources reach zero.
- Beside it, `다음 이벤트까지` is an optional checkbox-style control. When clear, `다음날로`
  advances exactly one day. When selected, the action advances one day at a time until the nearest
  known completion, clarification response, mail, reveal, review, or deadline day.
- Event skipping must stop early when an unpredicted completion, failure, newly arrived mail,
  blocking critical choice, or immediate boss decision occurs. It never bypasses a required player
  decision, and its button previews both the destination day and the event category.
- A worker can hold at most one primary Task for that day.
- The player normally manages the organization plan and Work priorities, then advances the day.
  Available Tasks are staffed without requiring a Task-detail visit.
- Direct primary assignment, parallel assignment, and start reservation remain available as
  explicit intervention tools. They may override the organization plan and retain their existing
  interruption and handover costs.
- Completing a Task preserves its assignee as historical ownership. The completed assignment does
  not consume current worker capacity and is not cleared when that worker starts another Task.

## Weekly Work, Rest, and Medical Checkups

- Saturday and Sunday are non-working days by default. Assignments and reservations remain attached,
  but workers produce no Task output on those days.
- Normal matched work adds `4` fatigue per day. Each weekend or explicitly scheduled rest day
  removes `9`, so five workdays followed by two rest days net about `+2` fatigue, while a third
  rest day moves the same worker to about `-7` fatigue relative to the cycle start.
- A weekend rest cycle reduces fatigue and improves mental condition using external balance data.
  An injured worker also receives one data-driven recovery roll per resting weekend day. Successful
  recovery clears the injury for the following day; failure leaves the normal injury countdown intact.
- Regular medical checkups occur every eight weeks, beginning on day 61; each occurrence remains on
  Friday. Every worker not assigned to a `총력!` Work is examined for no resource cost and performs
  half normal Task output while accumulating half the normal Task fatigue that day. The Gantt
  calendar marks each scheduled checkup Friday with a distinct background and `검` label.
- `총력!` is a per-Work crunch flag. Only workers currently assigned to that Work skip Friday's
  regular checkup and Saturday/Sunday rest and continue the normal work cycle. There is no global
  Crunch option. `Check all` remains an explicit medical action and does not change this flag.
- The player may send an individual worker for a checkup on any day. A checkup outside a scheduled
  eight-week Friday period is unscheduled: that worker produces no Task output for the entire day and consumes
  the external-data unscheduled-checkup resource cost. The request fails without changing state when
  the campaign cannot pay that cost.
- A checkup request does not detach or interrupt the worker's assignments and does not add context cost.
- Checkups assess health, fatigue, mental condition, and manager trust. The measured values are held as
  a pending medical result rather than immediately changing the worker file.
- Results from checkups performed since the previous result delivery arrive by mail at the start of the
  next Monday. The result mail exposes a `Download medical file` action.
- Downloading the medical file applies every attached result to the corresponding worker file. Until
  download, the visible file retains its prior values and update day. Each worker file records the last
  downloaded update day and weekday.
- Health, fatigue, mental condition, and manager trust show both their numeric value and a readable
  `Good`, `Caution`, or `Risk` grade. Thresholds and all checkup costs and recovery amounts are external
  balance data.
- Each worker owns a daily output value. A primary assignment contributes that output toward the
  Task workload before outcome modifiers.
- Every Task requires one to three of the six colony competencies: base engineering, science
  exploration, resource operations, environmental adaptation, life support, and command/diplomacy.
  Requirements are stored as competency indices in external gameplay data and shown on Task detail.
- Competency values map to grades as `2=F`, `3=D`, `4=C`, `5=B`, `6=A`, and `7=S`.
- When a Task supplies per-stat required grades, average `worker grade value / required grade value`
  across its required competencies and clamp the multiplier to `0.4..1.4`. A shortfall therefore
  reduces output but never blocks assignment. Legacy Tasks without explicit required grades retain
  the C-grade (`4`) baseline calculation.
- Specialty mismatch multiplies output by `0.8`. Dissatisfaction then applies a separate linear
  output penalty up to the externally configured maximum at dissatisfaction `100`.
- Daily output probability is derived from the worker's fatigue at the start of execution.
  The three outcomes are `Failure`, `Success`, and `GreatSuccess`. At fatigue 0 their chances are
  5% / 60% / 35%. At fatigue 50 they are
  20% / 60% / 20%, and at fatigue 100 they are 100% / 0% / 0%. Chances interpolate linearly
  between those anchor points. Their output multipliers are data-driven and default to
  0.5 / 1.0 / 1.5 for failure / success / great success.
- Final daily progress is `worker daily output × primary or parallel output × competency multiplier
  × specialty fit × dissatisfaction efficiency × condition outcome multiplier`, then the existing
  prerequisite progress cap is applied.
- Fatigue 100 does not make a worker unavailable, remove an assignment, or stop ongoing work.
  The worker continues at the low output multiplier. Only explicit interruption causes such as
  injury or scheduled rest stop work.
- Injury, scheduled rest, and regeneration never implicitly remove a Task assignment. They pause
  progress for the affected cycle when applicable while preserving the owner, assignment mode,
  split count, and context cost. Recovery resumes the same assignment automatically.
- A worker who begins a daily cycle injured or scheduled to rest produces no Task output that cycle.
  Injury recovery and rest recovery are then applied for the following cycle. An accident caused
  during execution keeps the worker attached to that Task while recording the injury and progress loss.
- Only explicit player reassignment/unassignment, completion, hard-deadline failure, or another
  separately specified permanent removal may detach a worker from a Task.
- A worker may hold one primary Task and multiple explicitly forced parallel Tasks.
- Every parallel Task costs additional fatigue and dissatisfaction defined by gameplay balance data.
- Parallel assignment does not interrupt the worker's primary Task.
- Rest, injury, or reassignment removes assignments according to the interruption rules.
- An assigned, progressable Task records its start day when the next daily cycle first produces
  work.
- While the assignee remains unchanged, every following daily cycle continues to add that worker's
  output.
- When accumulated output reaches effective required work, the Task records that cycle's day as
  its completion day, enters `Complete`, and releases its assignee.

## Staffing Plan and Role Rotation

- Each of the four Work roles owns a depth chart containing every current crew member exactly once.
  Earlier positions are preferred for Tasks requiring that role.
- Each crew member has an operating mode: `Starter` is a normal first-team candidate with a
  selection bonus; `Support` is a normal candidate; `Recovery` automatically rests and receives no
  new automatic assignment; and `Emergency` receives new work only for a `긴급!` Work.
- Each crew member has a fatigue limit from `40` through `95`, adjusted in increments of `5`.
  At the beginning of a working day, reaching the limit schedules automatic rest unless that worker
  is currently assigned to a `총력!` Work. Automatic rest preserves the current Task and context.
- The organization owns one operations style: `Continuity` weighs depth order most heavily;
  `Balanced` combines depth, competency, fatigue, and mode; `Deadline` weighs competency most
  heavily; and `Growth` prefers a rested, qualified lower-depth worker when expected competency
  output remains at least `0.75`.
- Changing the plan affects future automatic decisions and never detaches a current owner by itself.
- The staffing plan is campaign state. A legacy save without it migrates to `Balanced`,
  specialty-first depth charts, `Starter` modes, and fatigue limit `70`.

## Staffing Dispatcher

- The daily staffing sequence is automatic recovery, explicit reservations, urgent preemption,
  learned intervention rules, then the staffing dispatcher.
- Stakeholder-request Tasks are excluded from this sequence. Their explicitly confirmed worker
  slots remain unchanged until the player reassigns them or normal completion/failure clears them.
- The dispatcher visits unassigned, unscheduled, dependency-complete Tasks in player-defined Work
  priority and stable Task-ID order.
- Current owners continue their Tasks. The dispatcher fills vacancies and does not replace a valid
  owner merely because another worker has a higher score.
- Candidate selection uses required-role depth position, competency output, fatigue, operating mode,
  parent Work urgency, and operations style. Ties use lower fatigue and earlier roster position.
- The Gantt preview uses the same decision and identifies the source as the organization plan.
- With no eligible worker, the Task remains unassigned and the operations UI reports the gap. The
  player may change the lineup or use a direct intervention.

## Learned Intervention Rules

- A confirmed direct primary intervention records the player's choice as an exception rule.
- Stakeholder-request Task assignment does not create or consume a learned intervention rule.
- A work situation is identified by Task kind, required role, difficulty, risk, and importance.
- At the beginning of a daily cycle, an unassigned matching Task uses the remembered worker when
  available. The rule never substitutes another worker or interrupts current work.
- Rules remain campaign state and `My Info` presents them as remembered interventions.
- Novel situations require no manual teaching because organization dispatch handles them.

## Organization Automatic Assignment

- Organization dispatch supports legacy and background operations but is not the core prototype
  loop. It is enabled by default, and the existing campaign switch remains an advanced hold control
  for future automatic assignments outside Stakeholder Requests.
- Recovery policy runs first, followed by explicit reservations, urgent preemption, learned
  intervention rules, and organization dispatch.
- Dispatch considers each unassigned, unscheduled `Available` Task in Work-priority order. It never
  consumes a future reservation and requires every Work and Task predecessor to be complete.
- Automatic execution never uses the 30% early-progress allowance; that remains an explicit
  intervention option.
- The resulting primary assignment starts in the same daily cycle. Tasks unlocked by that cycle's
  completion wait until the following cycle.
- Organization assignments do not create learned intervention rules. Holding future dispatch does
  not remove current assignments.

## Work Priority and Focus Flags

- The Priority desktop application lists visible incomplete Works in effective automatic-assignment
  order. Up/down arrow controls move a Work one position and the order survives campaign save/restore.
- `긴급!` means staffing may preempt lower-priority work. Before normal automatic assignment, the
  system finds the highest-competency available worker for the urgent Task; when that worker owns a
  lower-priority primary Task, that Task is interrupted with normal context cost and the worker is
  made available to the urgent Work.
- `총력!` is not a staffing or priority modifier. Workers currently assigned to that Work skip the
  regular Friday checkup and weekend rest and continue producing Task output. Other workers still
  receive normal checkups and rest on the same day.
- `긴급!` and `총력!` are independent and may both be attached to one Work.
- Completed and failed Works leave the active priority panel. Generated and approved Works enter at
  the bottom of the current order.
- Each active priority row shows its hard-deadline D-day and both total and remaining WorkTask
  workload in workdays.

## Task Scheduling

- A player may reserve a Task start day for one worker within the rolling planning horizon. The
  horizon always extends at least 30 days beyond the current day.
- The reserved day is a start or resume day, not a one-day assignment.
- Once started, the same worker remains assigned and contributes output every daily cycle without
  repeated player input until the Task completes, fails, is held, or is reassigned.
- Rescheduling an active Task to a future day immediately holds it. The normal interruption and
  resumption context cost applies, and the selected worker resumes it on the new start day.
- Changing the reserved worker is a handover and follows the same interruption/handover rules.
- A worker cannot hold two primary reservations on the same day.
- At the beginning of the reserved day's daily cycle, the reservation attempts a normal primary
  assignment. Existing assignment, interruption, availability, and handover rules still apply.
- If the Task cannot be assigned because it has completed, failed, is locked, or the worker is
  unavailable, the reservation is consumed and the day report records that it did not start.
- A reservation can be replaced or cancelled before it is consumed.
- Reservation day and worker are part of the campaign save snapshot.

### Expected Schedule

- Selecting a worker produces an expected schedule before the reservation is confirmed.
- Expected daily output is the worker's daily output multiplied by primary-work output, the selected
  Task's competency multiplier, and the fatigue-derived weighted average of failure, success, and
  great-success output bands.
- Estimated duration is `ceil(estimated remaining work / expected daily output)`.
- Estimated remaining work includes the handover/context cost that selecting a different worker
  would add.
- An idle, available worker can start immediately when no dependency blocks entry.
- A busy worker starts the day after their current primary Task's estimated completion.
- A Task blocked by another Task or predecessor Work starts the day after the latest calculable
  blocker completion.
- Worker availability and dependency readiness combine by taking the later start.
- If a blocker has no assigned worker or no calculable completion, the estimate uses tomorrow as a
  rolling start. Recalculation on the next day uses that new tomorrow, so it never silently becomes
  today until the blocker becomes calculable.
- The Task detail shows estimated work, expected daily output, duration, start day, completion day,
  and the reason for the selected start day.
- A Task without an assigned or reserved worker still receives a planning preview using an assumed
  output of `1.0 work/day`.
- Unassigned predecessor Tasks use the same baseline preview, so their successors are placed after
  the predecessor's estimated completion instead of all appearing on the current day.
- Work-level predecessors use the latest estimated completion among their unfinished required Tasks.
- Dependency order determines sequence. Tasks with no dependency between them may remain parallel
  and start on the same day.
- The Gantt view uses this shared preview for bar start, duration, end, and dependency arrows.

## Task Detail Page

The task detail page must show:

- parent Work and Work state
- predecessor that blocks progress, if any
- successor Tasks blocked by this Task
- progress for the current Task and each displayed dependency
- current assignee, role, workload, context cost, recent output, risk, importance, and deadlines
- current reservation and controls for selecting a worker and start day
- recent Task records

Every day shown in the Task detail page (actual start/completion, deadlines, reservation, expected
schedule, selected reservation day, and recent records) includes its game-calendar month/day and
quarter while retaining the underlying `DAY n` value.

Task rows in the Gantt and milestone views open this detail page.

## Milestone Project History

- The Milestone app is a project history, not a second planning view.
- It includes visible Works that have started, completed, or failed. Untouched available and locked
  plans remain in planning surfaces until they produce history.
- Work entries are ordered by latest activity and remain visible after completion so history
  accumulates throughout the run.
- Each entry shows state, progress, first activity, terminal or current day, inclusive elapsed days,
  every historical participant, completed Task count, accumulated output, and claimed resource
  reward.
- Its activity log shows all persisted Task records newest-first with calendar date, record kind,
  Task name, actor, and text. Output records describe what was gained; note and issue records retain
  assignments, handovers, interruptions, and incidents.
- Existing Task rows remain available inside each history entry and open Task detail.

## Interruption and Handover

A continuous segment is an uninterrupted sequence of days in which the same Task remains the worker's primary assignment.

- Continuing the same primary Task on the following day has no context cost.
- Leaving a Task before it is complete creates one split.
- Returning to the Task later creates the resumption side of that split.
- The same rule applies whether the returning worker is the previous worker, another worker, or the player character.
- Each split adds `0.5 day` for interruption and `0.5 day` for resumption.
- Therefore each split adds `1.0 day` to the Task's effective remaining work.
- Changing workers without completing the Task is one split and must not be charged twice.

Example:

```text
Base duration: 4 days
Segment 1: 1 day
Interruption cost: 0.5 day
Resumption cost: 0.5 day
Segment 2: 3 days
Effective total: 5 days
```

The UI must show base progress, accumulated context cost, current owner, and expected remaining duration.

## Deadlines

- Soft deadline: work may continue, but the Work becomes overdue and its configured credit penalty
  is applied once.
- Hard deadline: if required Tasks are not complete when the deadline passes, the Work and its
  unfinished Tasks fail and the larger configured credit penalty is applied once.
- Completing all required Tasks pays the Work's configured credit reward once.
- Deadline ownership belongs to Work by default.
- A Task-specific deadline is allowed only when explicitly supplied by external data.

Overdue work also retains the increased-fatigue consequence. Further consequences may be added
through data without redefining Task ownership.

## Endless Run and Resource Life

- There is no victory state. Completing all initial required Works is a milestone inside the run.
- A failed required Work, passing day 90, and having every crew member injured do not end the run.
- Resources are the run's life. The only session-ending condition is `Resources <= 0`.
- Rewards refill resources while payroll, deadline penalties, regeneration, and future sinks drain
  them. All deductions clamp at zero.
- The score is the day reached before resource depletion. Balance evaluation must therefore compare
  survival-day distributions rather than a win rate.
- Random Work generation continues after the initial authored content so the run can continue.

## Critical Choice Event Chains

- A critical event arrives through mail with a `[!중요!]` subject prefix.
- Entering a critical event activates exactly one event chain. Each arrived node has a seven-day
  response window including its arrival day. The player may advance normally during that window,
  but must resolve the node before advancing beyond the seventh day. Unrelated mail remains visible
  and can be read from the same list while the critical choice is pending.
- After a node is resolved, its follow-up node arrives 2 or 3 days later. During this waiting period
  normal day advancement is allowed, but the same event retains the exclusive critical-event slot.
- Day advancement is locked only when an arrived critical node reaches the last day of its response
  window without a player choice.
- A chain contains one or more nodes. Every node presents explicit player choices, and every choice
  contains one or more weighted outcomes.
- An outcome applies data-defined resource, fatigue, and task-success modifiers, then names the next
  node. An empty next-node ID ends the chain.
- `CrewIndex = -1` applies fatigue to the whole team; a valid roster index targets one crew member.
- The task-success modifier moves percentage points between daily Failure and Success. It does not
  change Great Success probability and is clamped to `-50..+50` percentage points for the run.
- Only one due critical event may be active. Other due chains wait until a later daily cycle after
  the active chain ends.
- Critical-event definitions, nodes, choices, outcome weights, effects, and start days are external
  gameplay data. Active chain/node and accumulated success modifier persist in campaign saves.
- Options provides a clearly labeled debug button that immediately starts the earliest configured
  critical event, including replay after completion. It is disabled during an active chain or after
  resource depletion and opens the resulting `[!중요!]` mail on success.

## Random Work Generation

- The daily cycle may generate optional Works up to the configured active limit.
- The final generation chance applies a data-driven percentage scale after overdue and exhausted
  crew modifiers. The default scale is `80%`, reducing the previous emergency frequency to
  four-fifths without changing the relative pressure from those modifiers.
- Every generated Work receives a soft deadline, later hard deadline, credit reward, soft penalty,
  and larger hard penalty.
- Generated Tasks receive a workload of one or two days, a required role, and the union of one to
  three competency requirements supplied by their external target and action words.
- Generated Task names combine one adjective, one target noun, and one action from external data.
  The adjective determines risk and contributes difficulty. The target and action contribute
  difficulty and share a role, so combinations remain semantically coherent and point to the
  matching crew specialty (for example, `불안정한 암반 탐사` is high-risk analysis work, while
  `안정적인 골재 운반` is low-risk technical work).
- Word pools and their role, risk, and difficulty metadata are owned by `task-system.json`; runtime
  code must not contain fallback mission vocabulary.
- Completing a generated Task unlocks its adjective, target, and action in the Codex. An adjective
  entry explains its risk and difficulty role; target and action entries explain their recommended
  crew specialty and difficulty contribution. A word is unlocked only once, and discovered word
  IDs persist in the campaign snapshot.
- When loading a campaign containing generated Tasks from before word IDs were saved, the exact
  adjective-target-action name is matched against the current word pool to restore those IDs.
- A data-driven rare roll may connect a generated Task to an existing predecessor Task; the normal
  30% progress cap applies until that predecessor completes.

## Task Outcomes and People

Task execution exposes, but does not require every Task to use, these hooks:

- success chance derived from worker condition, specialty, perks, and Task difficulty
- result records for clear daily feedback
- probability of gaining a Task-related perk
- command acceptance derived from player capability, trust, authority, worker pride, and command reasonableness
- hidden worker changes represented as unknown until discovered through a later interaction system

Version 0.1 retains deterministic progress and the prototype accident outcome while adding data fields needed by these hooks. Full social interaction and hidden-information resolution are separate specifications.

## Field Team and Trust

- The prototype operates one field team with exactly twelve crew members.
- Every worker owns all six competency stats. Values `2..7` are presented as
  `F / D / C / B / A / S`; numeric storage remains deterministic runtime data.
- Every worker owns one specialty as their long-term strength. A different specialty remains
  assignable but reduces efficiency and adds dissatisfaction each worked day.
- Dissatisfaction is a separate `0..100` gauge from fatigue. Grade shortfall, specialty mismatch,
  and every forced parallel Task add dissatisfaction. Rest and weekends recover it through separate
  balance values, and high dissatisfaction reduces output without being treated as bodily fatigue.
- Each crew member owns a `Trust` value from 0 to 100 representing how that person regards the
  player-character responsible for the team.
- Trust and a readable interpretation must appear in the crew list, crew detail, and messenger.
- Messenger status replies include the worker's current view of the responsible officer.
- Each crew member has a data-defined personality. Crew detail and messenger expose it, and
  messenger replies express the same underlying situation differently according to personality.
- Trust changes and command acceptance effects remain extension points until their outcome rules are
  specified. This version exposes and preserves the relationship value without changing it implicitly.
- Restoring an older campaign with fewer than twelve crew members appends the missing current
  roster definitions without changing the saved state of existing workers.

## External Data

Gameplay content and balance values must not be authored as constructor literals or hard-coded creation methods.

External data includes:

- initial planning baseline and starting resources
- midpoint review day
- Stakeholder Request definitions: stakeholder, brief, hidden intent profile, initial clarity,
  planned weeks, deadlines, clarification options, revealed guidance thresholds, and result text
- request Task definitions: `60 / 30 / 10` contribution weights, interpretation/intent tags,
  role eligibility, competency minimums, workload, and late-score multiplier
- Work definitions and predecessor IDs
- Task definitions, durations, roles, one-to-three competency requirements, difficulty, and prerequisite IDs
- worker definitions and initial stats
- the twelve-person field-team roster and each member's initial trust toward the responsible officer
- each crew member's personality and initial voice
- assignment, interruption, parallel-work, fatigue, outcome, and perk balance values
- random Task adjective, target, and action pools with their role, risk, and difficulty metadata
- critical event chains, choices, weighted outcomes, and resource/fatigue/success effects
- learned assignment situation fields and player-created rule state

## Title and Startup Operations

- After Bootstrap has selected and loaded a valid HotUpdate assembly, the player first sees a
  dedicated title screen instead of the operations desktop.
- The title screen presents `PROJECT W`, `PRESS TO START`, and an adjacent `OPTIONS` action.
- `PRESS TO START` enters the operations desktop and restores a valid campaign save when one exists.
- Title Options provides the basic startup operations: reset campaign data, reset desktop layout,
  check for and install an update, inspect update status/version, and return to the title.
- Reset actions require a second confirmation action before deleting data. Campaign reset does not
  delete desktop accessibility settings; desktop reset does not delete campaign progress.
- The desktop Settings page also exposes `진행상황 리셋`. After confirmation it deletes only the
  campaign snapshot, immediately creates a fresh campaign, and keeps theme, patch, and data settings.
- The operations desktop Options application exposes the same update action and diagnostics.
- An update request downloads, validates, and installs the newest immutable development patch. The
  currently loaded HotUpdate assembly continues running; the newly installed patch is activated on
  the next application restart. The UI must state this restart requirement instead of claiming that
  an already-loaded assembly was replaced.
- The fixed Bootstrap scene remains the only Unity scene dependency. Title, desktop, and data editor
  are HotUpdate-owned screen states created through `IGameEntry`, so fixed scenes never reference
  HotUpdate implementation types directly.

## Gameplay Data Editor and Reload

- The operations desktop provides a developer-facing `GAME DATA` application presented as an
  Excel-like table editor. It is not exposed on the title screen.
- The editor groups data into at least `Characters`, `Balance and probabilities`, `Critical events`,
  and `Mail`. Additional external-data sections can be added without changing the editor's ownership.
- Tables use stable row identities, visible column headers, editable scalar cells, and explicit
  validation messages. Nested critical-event choices/outcomes may use a selected-row detail table.
- Editing changes a working copy only. `Save draft` validates and persists the complete gameplay-data
  document as a local override. Invalid drafts are rejected without changing the active campaign or
  the last valid override.
- `Reload game data` exists in the data editor, not in Options. Reload validates the working copy,
  persists it, creates a new campaign from it, and returns to the operations desktop. Because initial
  definitions can invalidate live object identities, reload intentionally discards current campaign
  progress after a confirmation action.
- The source precedence is: valid local editor override, active patch manifest data, then the
  APK-embedded Resources fallback. `Reset data override` removes only the local override and reloads
  from the patch or embedded source after confirmation.
- Local editor overrides are development data. They are not uploaded, added to a release, or treated
  as authoritative SSOT. Production defaults remain `task-system.json`, and publish tooling continues
  to package that file explicitly.
- Data reload never reloads or unloads the HotUpdate DLL. Code update and data reload are separate,
  clearly labeled operations.
- Each editor category tab (`Characters`, `Balance and probabilities`, `Critical events`, and
  `Mail`) provides one JSON transfer field, `Copy`, and `Apply`.
- `Copy` serializes the entire selected category into the transfer field and system clipboard in
  one action. Copy does not focus the transfer field or open the virtual keyboard.
- The player may paste replacement category JSON into the same field using the platform's normal
  paste action. `Apply` parses the field as the selected category, validates its required fields and
  the complete gameplay-data document, and replaces that entire category in the working copy.
- A category mismatch, malformed JSON, missing required content, invalid stable identity, duplicate
  identity, or cross-reference failure rejects the change and leaves the working copy untouched.
- Paste/apply changes only the working copy. `Save draft` or confirmed `Reload game data` remains
  required to persist or activate the edited document.
- The shared desktop skin uses dark text on white backgrounds for labels, buttons, windows, boxes,
  toggles, and text-entry states. Data-editor cells and the JSON transfer field must remain readable
  in normal, hover, active, and focused states.

## In-Game Codex as Living Specification

- The base `Codex` array in `task-system.json` is both an always-available player guide and a
  developer-facing mini specification of the behavior shipped in the current patch.
- It covers campaign goals, day order, Work and Task mechanics, worker systems, automation,
  resources, interactions, save data, and patch boundaries.
- Entries must distinguish implemented behavior from planned behavior with explicit wording such
  as `현재`, `아직`, and `향후`; planned effects must never be presented as active mechanics.
- Any balance or behavior change that makes a guide statement inaccurate must update the matching
  Codex entry in the same commit.
- Base guide entries are visible from campaign start. Generated adjective, target, and action
  entries remain discovery-based additions and persist through discovered word IDs.
- Every personality and perk currently present on a crew member is recorded as a discovery-based
  Codex entry under separate worker-personality and worker-perk categories. Duplicate names create
  only one entry, with the first observed crew member recorded as its discovery source.
- Crew-trait discoveries are recorded when a campaign starts, after a crew regeneration changes
  personality or perks, and after a campaign save is restored. Once discovered, an entry remains in
  the Codex even when no current crew member retains that trait.
- Discovered crew-trait IDs and their first-observed crew names are campaign save data. Restoring an
  older save backfills the current roster's traits without removing existing generated-word entries.
- mail and codex content

The authoritative runtime data file for this prototype is:

`Assets/MilestonePrototype/Resources/task-system.json`

Patch builds must include this file in the patch manifest. Hot-update runtime loads it from the patch data directory. Editor and APK-embedded fallback execution load the same source JSON as a Unity `Resources` asset. A missing or invalid required data file is an explicit startup error; gameplay code must not silently replace it with hard-coded content.

## Impact Matrix

| Area | Impact |
|---|---|
| Ingame | Mail-driven request intake, clarification timing, weighted Task planning, eligible assignment, progress, delivery, and stakeholder-fit scoring |
| Outgame | Title, startup Options, reset confirmation, and update status |
| Metadata | External request/intent/clarification data, weighted Tasks, score history, validated local override, and campaign save schema |
| Operation | Patch builder includes and verifies gameplay JSON; update activation requires restart |

## Acceptance Criteria

- An arrived Stakeholder Request mail first opens a request-only view. Task interpretations and
  worker slots appear only after the player selects `계획하기`, and no assignment starts before that.
- `다음날로` advances one day by default; the optional next-event mode previews its destination and
  stops on scheduled or newly encountered meaningful events.
- The prototype request plan contains three required Tasks whose contribution weights are
  `60 / 30 / 10` and total `100`.
- Only workers satisfying availability, capacity, role eligibility, and every Task competency
  minimum can be confirmed into a stakeholder Task slot.
- Requesting a call or meeting schedules one future interaction, increases clarity on its due day,
  reveals authored intent guidance, and never extends the delivery deadline.
- A request cannot be delivered while a clarification is pending or while a required Task is
  incomplete.
- Completing all required Tasks resolves one immutable `0..100` weighted stakeholder-fit score;
  hard-deadline failure resolves no score.
- Stakeholder-request Tasks are not filled or replaced by automatic staffing systems.
- A legacy campaign snapshot missing a newly authored Stakeholder Request mail receives that mail
  once on restore without losing existing campaign state.
- The dashboard exposes the current state needed to make request-planning decisions without adding
  a second mandatory management loop.
- Work prerequisites prevent early Task assignment.
- One worker cannot have two primary assignments.
- A Task with at most one day remaining can be assigned in parallel at an additional fatigue cost.
- Interrupting and later resuming a four-day Task after one day raises its effective total to five days.
- Replacing a worker charges one split, not two.
- Soft and hard deadlines are evaluated at Work level.
- No initial Work, Task, worker, mail, codex, or balance values are created in runtime code.
- The gameplay JSON is emitted as a manifest-listed patch file.
- The title exposes Press to Start and Options before the desktop is entered.
- Title and desktop Options can request an update and clearly report next-restart activation.
- The data editor exposes character, balance/probability, critical-event, and mail tables.
- Invalid data drafts cannot replace the active campaign or last valid override.
- Confirmed data reload starts a new campaign from the validated working copy.
- EditMode tests cover assignment capacity, prerequisites, parallel work, interruption cost, handover cost, and deadlines.
- EditMode tests cover below-standard output, competency averaging, excellent-score coverage, and
  fatigue-driven failure/success/great-success output.
- The run continues beyond day 90, has no victory state, and ends only at zero resources.
- Day 45 produces one midpoint review and the planning horizon remains at least 30 days ahead.
- An arrived `[!중요!]` mail locks day advancement until chosen; follow-up nodes arrive after 2–3 days.
- Critical choice outcomes deterministically apply their rolled resource, fatigue, and task-success effects.
- A manual primary assignment creates or updates a learned rule.
- A matching available Task is automatically assigned only when the recorded worker is eligible.
- Learned rules survive campaign save/restore and are visible in `My Info`.
- Competency automatic assignment respects reservations, learned rules, worker capacity, and
  competency/fatigue tie-breaking; its toggle survives campaign save/restore.
- Learned and competency automatic assignment leave every predecessor-blocked successor unassigned
  until its Work and Task predecessors are complete.
- Reveal-day and mail-acceptance gates also block manual, learned, scheduled, and competency
  automatic assignment.
- The final operation is invisible and unassignable through day 59, then becomes visible on day 60.
- The Proposal desktop application and every ready-made/custom proposal flow are absent.
- Generated side-mission hard-deadline failure deducts resources without setting campaign loss.
- Task completion retains the completing assignee while leaving that worker free for new work.
- Separately, the boss may initiate an inbound Work request. It opens an immediate decision popup
  and enters the Gantt only when accepted. Inbound requests are chance-driven and are never
  guaranteed by empty inventory.

## Boss Requests

- The player-authored and ready-made Proposal system is retired. No Proposal application, proposal
  candidates, review queue, result mail, investment, or proposal save state is generated.
- Generated side missions remain optional Works created only through structured inbound boss
  requests and use the same Work/Task deadline, assignment, competency, progress, reward, and
  penalty rules as authored missions.
- A generated side mission is never required for campaign victory. Missing its hard deadline fails
  only that Work, applies its configured resource penalty, and cannot directly produce game over.
- A Task checks for a structured inbound incident exactly once when work first begins at 0%, once
  when final progress first reaches or crosses 50%, and once when it reaches 100%. There is no daily
  background incident roll. Each check starts from the externally configured base probability,
  default `1%`, then multiplicatively applies every checkpoint-specific perk on the assigned worker.
- A successful check creates one boss request, bounded by the random-Work limit, and opens an
  immediate modal decision popup instead of waiting in the mail application. A consumed checkpoint
  is saved even when its roll fails or the queue is full, so it cannot be rerolled by reassigning or
  restoring the Task.
- The request mail, day report, and source Task record identify the worker, source Task, checkpoint,
  total multiplier, full multiplication expression, and final probability that caused the incident.
- The incident popup blocks day advancement and every background control until the PM chooses
  `일함 / 승인` or `거절`. It displays the current day, current resources, reward, refusal cost,
  workload, child Tasks, and soft/hard deadlines.
- The incident decision renders in its own full-screen overlay window and is brought to the front
  every frame, above desktop windows, toasts, and every other popup.
- Accepting admits the Work to the Gantt. Refusing immediately deducts the displayed hard-penalty
  resource cost, fails the hidden offer for history, and never admits it to the Gantt.
- Restoring a legacy unresolved inbound-request mail preserves its acceptance gate. Save-data
  migration must not make that Work visible in the Gantt before the player accepts it.
- A generated request's adjective or risk—including `긴급한`—never bypasses the acceptance gate.
  Only the player's explicit popup approval makes that inbound Work visible.
- Every generated side-mission Work contains two to four required child Tasks. The Work is the
  mission/table-of-contents row, while its Tasks are separately assignable structured work items.
- Generated child Tasks use the existing adjective/target/action word pool independently and form a
  prerequisite chain inside the Work. Completing one child unlocks the next; completing all children
  completes the mission Work.
- An inbound boss request creates one popup decision regardless of child Task count and retains the
  resolved record in mail.
- Urgent/incident generation remains a separate concept. It must not use the side-mission batch,
  side-mission mail, or `TaskKind.SideMission` merely to represent a small random action.
- Authored non-urgent Works should provide roughly 10–15 working days of chained required workload,
  with soft and hard deadlines sized for a two-to-three-week planning tempo. Urgent/incident work
  remains a short two-to-three-day response and is balanced separately.
- The generated Work deadline includes additional schedule allowance for its child count so the
  hierarchy is not balanced as though it were a single Task.
- On campaign restore, an incomplete legacy generated side-mission Work with only one child Task is
  migrated to the hierarchical form by appending structured dependent Tasks and extending its
  deadlines. Completed and failed historical Works are not rewritten.
- Empty side-mission inventory does not force a boss request.

## Mail and Messenger Ordering

- Stakeholder Request mail is the entry point for the core prototype. Its primary action opens the
  request planning board with the brief, clarity, remaining days, weighted Tasks, and assignment
  slots. Reading mail alone never accepts, staffs, or starts the request.
- A clarification request and its eventual stakeholder response are shown as one request-thread
  history. The scheduled response day remains visible until it resolves.
- Delivery creates a result mail containing the final stakeholder-fit score and weighted Task
  breakdown. This result is historical and cannot be rerolled by reopening the mail.
- Arrived unread mail marked `[NEW]` is listed above every read or resolved message. Within each
  read state, newer arrival days appear first while equal-day insertion order remains stable.
- Critical mail is listed together with all other arrived mail, including while its choice is
  pending. Resolved mail rows are visually dimmed while remaining available for reference.
- Ordinary mini incidents whose only Work effect is to increase or decrease its schedule are not
  delivered as individual mail. They are collected into one Monday mail titled
  `주간현장 현황공유`.
- The weekly field-status mail lists every collected incident separately. Each item exposes
  `승인` and `무시`; approving applies that item's schedule delta at the time of approval, while
  ignoring closes it without changing the schedule. Undecided items remain pending in that digest.
- Critical missions remain separate mail. Resolved boss-request records and every other mission that
  grants a Work or one or more mini Tasks remain separate from the weekly field-status digest.
- A messenger question and its worker reply are stored and rendered as one conversation item, not
  two detached bubbles.
- Worker reports and question/reply items share one day-ordered messenger stream. Task records remain
  available in Task detail but are not rendered as a separate report section below conversation.

## Gantt and Task Detail UI

### Request Planning Board

- Stakeholder Request mail first opens a request-intake surface, not the planning board. It shows the
  stakeholder, original brief, revealed constraints, clarity, remaining days, planned duration in
  weeks, and soft/hard delivery dates without Task breakdowns or worker slots.
- The intake surface exposes clarification calls and meetings plus one primary `계획하기` action.
  Clarification consumes its authored lead days while the original deadline continues to approach.
- Selecting `계획하기` opens the separate request planning board and makes the Work visible in
  planning surfaces. Returning to the inbox does not cancel either phase.
- It shows the currently revealed guidance separately from unrevealed intent so low clarity remains
  legible as uncertainty rather than missing UI.
- It exposes `통화 요청` and `회의 요청` with their scheduled response day and clarity gain before
  confirmation. While one is pending, both actions are disabled and the pending interaction is
  visible.
- The body presents the three weighted Task rows in `60 / 30 / 10` order. Each row shows its
  interpretation, workload, role, competency minimums, current assignee, and eligible-worker picker.
- Unavailable workers stay visible with a concise blocking reason. Mismatched workers remain
  assignable with their efficiency and dissatisfaction forecast. The confirm-plan action is enabled
  when every required Task has one assigned worker.
- After confirmation, the same board becomes a live delivery view showing progress, schedule risk,
  ownership, clarification history, and the eventual score breakdown.
- The broader operations desktop and Gantt are read-oriented status surfaces for this loop. They may
  open the request board but must not require duplicate assignment work.

### Gantt

The Task application must present schedule information as a time-based Gantt view rather than only a flat progress list.

- The horizontal axis represents campaign days.
- Today, each Work soft deadline, and each Work hard deadline are visually distinct.
- Each Task row anchors completed history to its recorded start and completion days. Completed bars
  never move forward with the current day.
- Unstarted Tasks show their reservation or projected start; active Tasks project only their
  remaining duration from today.
- Visible incomplete Works are ordered by the same player-defined priority used by automatic
  assignment. The projected segments reserve current assignees first, then place unassigned Tasks
  into future worker-availability slots in Work-priority and dependency order.
- Completed progress and projected remaining work use distinguishable portions of the same bar.
- Locked Tasks remain visible and state why they cannot be entered.
- The Task application is Gantt-only; it does not combine a Task detail panel with the timeline.
- The left Work/Task column stays horizontally fixed while only the timeline pans.
- A single `오늘로` control places the current-day column after three visible past-day columns,
  clamped at the beginning and end of the horizontal range.
- `오늘로` rebuilds the existing schedule page through normal navigation; it must never append a
  second Gantt tree below the current one.
- Work predecessors and Task prerequisites are shown as arrowed dependency connectors.
- Dependency connectors use the same priority schedule and visible Work ordering as their bars and
  rows, so each arrow remains attached when priorities, assignments, or forecasts move a Task.
- Work sections are divided with `#999999` separator lines on the white background.
- Work rows expose Work state, completion, and deadline status.
- Every Task row shows its current primary or parallel assignee. Unassigned rows show `미배정`, and
  future reservations show the reserved worker and start day.
- A completed Task row continues to show the worker who completed it. Historical ownership does not
  imply that the worker is currently occupied by that Task.
- A completed Task row uses a dimmed gray row background in both the fixed label column and the
  timeline while remaining available to open for historical detail.
- A completed Work remains in the Task panel through the first 13 elapsed days after its latest
  required Task completion. Once 14 days have elapsed, the Work and all of its Task rows are removed
  from the panel. This is presentation filtering only and does not delete completion history.
- The Task name is immediately followed by Task state and the assigned worker's current condition.
- On each assigned Task row, the current-day column anchors a worker activity slot showing the
  worker, current Task, and condition. The slot reserves separate portrait, status-icon, and border
  regions. The portrait region displays the assigned roster slot's layered remote portrait. It falls
  back to the stable complete portrait, then the data-defined text portrait, when loading fails.

The projected segment is operational guidance, not an immutable reservation. It is recalculated from current progress, context cost, prerequisites, assignments, and today whenever state changes.

### Task Detail

Task detail is not displayed inside the Gantt application. If it is reintroduced, it must use a
separate view so timeline navigation remains dedicated to the Gantt.

The selected Task detail must expose:

- parent Work and Work state
- required or optional status
- prerequisite and lock reason
- required role and current assignee
- primary or parallel assignment mode
- base duration, effective duration, completed work, remaining work
- accumulated interruption/handover cost and split count
- projected cost of changing the current assignee
- fatigue cost for primary and parallel execution
- soft and hard deadline status
- recent Task records
- primary assignment, parallel assignment, and unassignment controls when valid

Costs must be shown before the player confirms a reassignment. The UI must not require the player to infer handover cost from a result log after the action.

### Worker Assignment Preview

- A short press on a worker button performs the assignment.
- Holding the same button for at least `0.45` seconds opens a compact, non-committing preview and
  suppresses the release click.
- The preview shows the worker's required competency grades, Task required grades, specialty match,
  expected efficiency, current dissatisfaction, expected dissatisfaction per workday, active Task
  count, and whether this would be a forced parallel assignment.
- Releasing the pointer anywhere closes the preview. Any additional pointer/touch press, any
  keyboard input, Android Back, or Escape also closes it immediately. Viewing it never mutates
  assignment or campaign state.

## Scroll and Touch Interaction

Scrollable window regions do not show persistent scrollbars. Movement uses direct-content dragging
or the mouse wheel.

- The Options application lets the player select `1.0x`, `1.4x`, `1.8x`, or `2.2x` accessibility
  magnification over the responsive screen scale. `1.8x` is the default.
- Font size, padding, controls, panels, windows, taskbar, and touch targets share the selected
  magnification so text does not outgrow its layout.
- The selected magnification is saved in desktop settings and restored on the next launch. Older
  desktop saves without a value use the `1.8x` default.
- When magnification reduces the logical desktop below the compact breakpoint, application windows
  use the existing near-full-screen compact layout and retain scrollable content.

- Mouse drag and single-finger touch drag pan the content in the opposite direction of pointer movement.
- The Options application uses the same direct-content dragging and mouse-wheel scrolling as other
  scrollable windows.
- Both horizontal and vertical scroll axes are supported when the content exceeds the viewport.
- A press remains a normal button press until movement exceeds the drag threshold.
- Crossing the drag threshold cancels click intent for that gesture and begins scrolling.
- Releasing a sufficiently fast drag continues a short decelerating inertial scroll without
  overscroll bounce. Pressing the moving content stops that inertia without activating a control.
- Scroll offsets remain clamped by the scroll view.
- Window-title dragging continues to move the window and must not scroll its content.
- The window-title drag hit area is twice the visible title-bar height.
- Pressing a desktop icon for an already-open application restores it when minimized
  and raises its window above every other application window.
- Pressing or beginning a drag, resize, or pinch gesture on a visible part of a window gives that
  window focus and raises its stable GUI window ID above every other application window.
- Desktop application icons use a six-column grid, with each name tucked upward into a taller label
  area beneath its icon so the bottom of the text remains fully visible.
- Minimize and close keep their visual size but use hit areas twice their width and height.
- Escape closes the active non-minimized window, then the next active window on another press.
- Nested scroll regions use the region where the gesture began and do not transfer the gesture mid-drag.
- A two-finger pinch on a window changes that window's width and height around the gesture center.
- Moving the two-finger gesture center moves the same window while the pinch remains active.
- The window selected when the second finger begins remains the gesture target until fewer than two
  touches remain. The gesture cancels content dragging and saves the resulting window geometry.

## Crew Detail UI

- Selecting a crew member opens a separate crew-detail window.
- The detail shows a portrait area, name, memo, perks, current assignment, and work history.
- The crew list, detail, and messenger show trust toward the responsible officer on a 0–100 scale
  with a readable relationship summary.
- Crew profile metadata is owned by `task-system.json`.
- Crew portrait labels and addresses remain optional profile metadata owned by `task-system.json`.
  Rendering and presentation behavior are intentionally outside this rules specification.

Unity IMGUI touch-to-mouse synthesis is the runtime input path for this prototype. Drag calculation remains a pure tested function so a later input-system migration does not redefine the interaction.

### Base APK v2 Compatibility Hold

Direct-content dragging is temporarily disabled for patch-only delivery on base APK v2.

- Base APK v2 does not prove preservation of the required IMGUI pointer and control APIs.
- A patch must not call unproven `GUILayoutUtility`, direct `GUI.BeginScrollView`, `Event` pointer, or `GUIUtility` control members.
- The corrective patch keeps visible `GUILayout` scrollbars and Gantt day-page buttons.
- Direct-content dragging remains a required feature, but it resumes only after a base APK explicitly preserves and verifies its exact AOT surface.
- Do not simulate dragging through window movement, reflection, or another control with unrelated semantics.

### Base APK v3 Activation

Base APK v3 explicitly preserves the IMGUI layout, pointer, and control surface required by direct-content dragging.

- The full horizontal Gantt scroll view and content dragging are restored only in patches with `minBaseVersion = 3`.
- The base v3 embedded HotUpdate DLL contains the same restored behavior for offline fallback.
- Base v2 continues to use the safe paged/scrollbar UI from `dev-20260727-007`.

### Base APK v4 Activation

Base APK v4 explicitly preserves `UnityEngine.GUISkin` and `UnityEngine.GUIStyle`, including the
vertical scrollbar styles and `fixedWidth` accessors used to double the right-side scrollbar width.

- Scrollbar-width patches require `minBaseVersion = 4`.
- Base APK v4 embeds the same doubled-scrollbar and start/resume reservation behavior for offline
  fallback.

### Base APK v5 Activation

Base APK v5 explicitly preserves the legacy multi-touch surface required for two-finger window
gestures: `UnityEngine.Input` and `UnityEngine.Touch`.

- Pinch-window patches require `minBaseVersion = 5`.
- Base APK v5 embeds the same pinch resize and two-finger drag behavior for offline fallback.

### Base APK v6 Activation

Base APK v6 uses the project's active Input System package rather than the disabled legacy
`UnityEngine.Input` touch path.

- Input System pinch-window patches require `minBaseVersion = 6`.
- The gesture reads the first two actively pressed `Touchscreen` contacts and ignores inactive
  touch-control slots.
- Base APK v6 preserves the required `Unity.InputSystem` types and embeds the same behavior for
  offline fallback.

### Current Base APK v14 Activation

- The currently supported installed baseline is base APK v14. Base APK v11 added the unified remote
  Addressables path, v12 added the StudioUI UI Toolkit runtime, v13 added pointer callbacks, and v14
  adds the exact keyboard controls used to dismiss assignment previews.
- Current development patches declare `minBaseVersion = 14`. Earlier base compatibility notes in
  this document are historical constraints, not supported alternatives for the active dev channel.
- The exact approved AOT and Contracts surface remains authoritative in
  `Assets/Specification/Operation/HotUpdateAotSafety.md`; this section does not authorize additional
  Unity, package, platform, native, reflection, serialization, delegate, or generic dependencies.
- Base APK v14 retains the reviewed UI Toolkit pointer-down/up callback surface and adds only the
  Input System `Keyboard.current`, `Keyboard.escapeKey`, `Keyboard.anyKey`, and existing
  `ButtonControl.isPressed` surface used to dismiss worker previews. It must be installed once
  before consuming v14 patches.
- Saturday and Sunday columns use a distinct background treatment and weekday labels so the
  non-working weekend cadence remains identifiable across the full planning horizon.
- An unassigned Task row shows its next automatic-assignment candidate when one can currently be
  determined. Reservation preview has priority, followed by a matching learned assignment rule,
  followed by competency automatic assignment when that campaign option is enabled.
- Learned and competency previews apply the same dependency-complete, worker-availability, and
  no-existing-primary-work checks as actual daily assignment. If those checks prevent assignment,
  the row does not promise a worker. The preview states whether it comes from a reservation,
  learned rule, or competency selection and does not itself change assignment state.
