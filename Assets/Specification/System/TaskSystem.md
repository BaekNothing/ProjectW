# Task System

## Document Control

- Version: 3.0
- Status: Approved for implementation
- Action: Create
- SSOT Change: Yes
- Rationale: Define resources as the run's life, remove victory and fixed-duration endings, and
  support endurance balance simulation.
- Idea references: `IDEA.md` items 1, 2, 4, and 8

## Scope

This document defines:

- parent work items (`Work`)
- executable assignment units (`Task`)
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

A Task is the smallest unit to which the player assigns a worker.

- A Task belongs to exactly one Work.
- A Task has a duration measured in half-day increments.
- A Task may depend on another Task. Before that predecessor completes, the successor can be
  assigned but cannot progress beyond 30% of its effective workload.
- An unassigned Task does not progress.
- Optional Tasks do not block Work completion.

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
- One month is always 30 days. Day 90 is a content/planning baseline, not a run limit.
- Payroll is charged every 30 days. Each crew member starts with a data-defined monthly base salary,
  and every configured block of accumulated Experience increases that salary by a configured amount.
- Experience is earned through daily Task execution. Regeneration resets Experience to zero, which
  also returns that crew member to the initial base salary. Payroll never reduces resources below zero.
- Regeneration always pays a base resource cost and offers independent ability and perk inheritance
  options for additional data-driven costs. Ability inheritance preserves specialty, skill,
  competencies, and daily output; perk inheritance preserves the current perk array. Any unselected
  layer returns to that roster slot's initial external-data definition.
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
- A worker can hold at most one primary Task for that day.
- Completing a Task preserves its assignee as historical ownership. The completed assignment does
  not consume current worker capacity and is not cleared when that worker starts another Task.
- Each worker owns a daily output value. A primary assignment contributes that output toward the
  Task workload before outcome modifiers.
- Every Task requires one to three of the six colony competencies: base engineering, science
  exploration, resource operations, environmental adaptation, life support, and command/diplomacy.
  Requirements are stored as competency indices in external gameplay data and shown on Task detail.
- Competency `4` is the standard threshold. For a worker and Task, average the worker's scores only
  across that Task's required competencies. The competency output multiplier is
  `required competency average / 4`, within the natural score range of `0.5` to `1.75`.
- If every required score is below `4`, override that calculation to `0.5`. Otherwise scores above
  `4` offset below-standard required scores through the average. Thus an excellent `7` may cover
  another required competency's shortfall, and an average of `4` produces the standard `1.0` work
  per day before condition and other existing modifiers.
- Daily output probability is derived from the worker's fatigue at the start of execution.
  The three outcomes are `Failure`, `Success`, and `GreatSuccess`. At fatigue 0 their chances are
  5% / 60% / 35%. At fatigue 50 they are
  20% / 60% / 20%, and at fatigue 100 they are 100% / 0% / 0%. Chances interpolate linearly
  between those anchor points. Their output multipliers are data-driven and default to
  0.5 / 1.0 / 1.5 for failure / success / great success.
- Final daily progress is `worker daily output × primary or parallel output × competency multiplier
  × condition outcome multiplier`, then the existing prerequisite progress cap is applied.
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
- A worker may additionally hold at most one parallel Task when that Task has no more than one day of effective work remaining.
- Parallel work costs additional fatigue defined by gameplay balance data.
- Parallel assignment does not interrupt the worker's primary Task.
- Rest, injury, or reassignment removes assignments according to the interruption rules.
- An assigned, progressable Task records its start day when the next daily cycle first produces
  work.
- While the assignee remains unchanged, every following daily cycle continues to add that worker's
  output.
- When accumulated output reaches effective required work, the Task records that cycle's day as
  its completion day, enters `Complete`, and releases its assignee.

## Learned Assignment Rules

- The first assignment for a work situation is always made manually by the player.
- A confirmed manual primary assignment records the player's choice as an assignment rule.
- A work situation is identified by the Task's kind, required role, difficulty, risk, and
  importance. Task and Work IDs are not part of the key, so the decision can apply to later work
  with the same nature.
- A rule maps one situation to one crew member. A later manual primary assignment for the same
  situation replaces the recorded crew member and increments the rule's update count.
- At the beginning of each daily cycle, an unassigned, unscheduled, available Task matching a
  learned rule is assigned to that rule's crew member when that member is available and has no
  other primary Task.
- Automatic assignment never chooses a substitute worker, interrupts current work, consumes a
  future reservation, or creates/updates another rule. If the recorded worker cannot take the Task,
  the Task remains unassigned.
- Rules and their update counts are part of the campaign save snapshot.
- `My Info` lists the current rules in readable form so the player can inspect how their judgments
  are being learned.
- The optimization goal is that repeated situations eventually continue correctly through daily
  advancement alone, while novel situations still require player judgment.

## Competency Automatic Assignment

- The Gantt header owns a campaign-level `자동배정` checkbox. It is off by default and its value is
  preserved in the campaign save.
- At the beginning of a daily cycle, start reservations are applied first, learned assignment rules
  second, and competency automatic assignment third.
- When enabled, competency automatic assignment considers each unassigned, unscheduled `Available`
  Task in external data order. It never interrupts an existing assignment or consumes a future
  reservation.
- `Available` alone is not sufficient for automatic assignment because manual planning may preassign
  a successor under the 30% prerequisite cap. Learned and competency automatic assignment both
  require every predecessor Work and the Task's direct predecessor Task to be `Complete`.
- A blocked successor remains unassigned during automatic operation and is reconsidered at the next
  daily cycle after its final predecessor completes. Automatic execution never uses the 30% early
  progress allowance; that allowance remains available only through explicit player assignment.
- For each considered Task, choose among available workers without another primary Task. The worker
  with the highest competency output multiplier for that Task is selected. Equal multipliers prefer
  lower fatigue, then the earlier field-team position for deterministic behavior.
- The resulting primary assignment starts and produces output in the same daily cycle. A Task that
  becomes available only after that cycle's completion/state refresh waits until the next daily cycle.
- Competency automatic assignments do not create or update learned assignment rules.
- Turning the checkbox off stops future competency assignments but does not remove existing assignments.

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

Task rows in the Gantt and milestone views open this detail page.

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

- The prototype operates one field team with exactly four crew members.
- Each crew member owns a `Trust` value from 0 to 100 representing how that person regards the
  player-character responsible for the team.
- Trust and a readable interpretation must appear in the crew list, crew detail, and messenger.
- Messenger status replies include the worker's current view of the responsible officer.
- Each crew member has a data-defined personality. Crew detail and messenger expose it, and
  messenger replies express the same underlying situation differently according to personality.
- Trust changes and command acceptance effects remain extension points until their outcome rules are
  specified. This version exposes and preserves the relationship value without changing it implicitly.
- When an older campaign save contains more than four crew members, only the first four remain on
  the active field team. Assignments and reservations owned by removed positions are cleared safely.

## External Data

Gameplay content and balance values must not be authored as constructor literals or hard-coded creation methods.

External data includes:

- initial planning baseline and starting resources
- midpoint review day
- Work definitions and predecessor IDs
- Task definitions, durations, roles, one-to-three competency requirements, difficulty, and prerequisite IDs
- worker definitions and initial stats
- the four-person field-team roster and each member's initial trust toward the responsible officer
- each crew member's personality and initial voice
- assignment, interruption, parallel-work, fatigue, outcome, and perk balance values
- random Task adjective, target, and action pools with their role, risk, and difficulty metadata
- learned assignment situation fields and player-created rule state

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
- mail and codex content

The authoritative runtime data file for this prototype is:

`Assets/MilestonePrototype/Resources/task-system.json`

Patch builds must include this file in the patch manifest. Hot-update runtime loads it from the patch data directory. Editor and APK-embedded fallback execution load the same source JSON as a Unity `Resources` asset. A missing or invalid required data file is an explicit startup error; gameplay code must not silently replace it with hard-coded content.

## Impact Matrix

| Area | Impact |
|---|---|
| Ingame | Work availability, daily assignment, progress, fatigue, deadline failure |
| Outgame | None in version 0.1 |
| Metadata | External JSON schema and campaign save schema |
| Operation | Patch builder includes and verifies gameplay JSON |

## Acceptance Criteria

- Work prerequisites prevent early Task assignment.
- One worker cannot have two primary assignments.
- A Task with at most one day remaining can be assigned in parallel at an additional fatigue cost.
- Interrupting and later resuming a four-day Task after one day raises its effective total to five days.
- Replacing a worker charges one split, not two.
- Soft and hard deadlines are evaluated at Work level.
- No initial Work, Task, worker, mail, codex, or balance values are created in runtime code.
- The gameplay JSON is emitted as a manifest-listed patch file.
- EditMode tests cover assignment capacity, prerequisites, parallel work, interruption cost, handover cost, and deadlines.
- EditMode tests cover below-standard output, competency averaging, excellent-score coverage, and
  fatigue-driven failure/success/great-success output.
- The run continues beyond day 90, has no victory state, and ends only at zero resources.
- Day 45 produces one midpoint review and the planning horizon remains at least 30 days ahead.
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
- A generated side mission is automatically added to the visible task roster when its next-morning
  notification mail arrives and follows normal Work/Task state rules without an acceptance gate.
- Generated side-mission hard-deadline failure deducts resources without setting campaign loss.
- Task completion retains the completing assignee while leaving that worker free for new work.
- Zero remaining side missions generate one to three next-morning structured offers; urgent
  missions do not prevent that refill.
- Each generated offer has one Work row, two to four chained Task rows, and one notification mail.
- Side-mission failure can trigger same-morning replacement mail, unread mail sorts first, and a
  messenger question/reply renders as one item in the unified report/conversation stream.

## Morning Side-Mission Offers

- Generated side missions are optional Works with the same Work/Task deadline, assignment,
  competency, progress, reward, and penalty rules as authored missions.
- A generated side mission is automatically added to the Gantt and task roster on the morning after
  generation. It can receive an assignment immediately, subject to normal predecessors.
- The mail application receives one notification for the generated mission. Resolving the mail only
  acknowledges the notification and is not an acceptance gate.
- Generated offers use materially larger credit rewards than the previous incident baseline. Their
  reward and resource penalties remain externally balanced in `task-system.json`.
- A generated side mission is never required for campaign victory. Missing its hard deadline fails
  only that Work, applies its configured resource penalty, and cannot directly produce game over.
- Until the day-60 final-operation reveal, incident handling and accepted generated side missions
  are the primary additional work stream around the authored foundation Work.
- Generated missions count toward the random-Work limit so unread mail cannot create an unbounded queue.
- Remaining side missions are available or active Tasks whose `TaskKind` is `SideMission`
  and whose parent Work is neither complete nor failed. Urgent/incident missions are not side
  missions and never satisfy this inventory check.
- At the end of a day with zero remaining side missions, the system generates a random batch of one
  to three structured side-mission Works, bounded by the random-Work limit. Each generated Work uses
  the existing adjective/target/action structure and arrives with its own next-morning notification mail.
- Every generated side-mission Work contains two to four required child Tasks. The Work is the
  mission/table-of-contents row, while its Tasks are separately assignable structured work items.
- Generated child Tasks use the existing adjective/target/action word pool independently and form a
  prerequisite chain inside the Work. Completing one child unlocks the next; completing all children
  completes the mission Work.
- A generated mission sends exactly one notification mail, regardless of its child Task count. The
  mail names the mission and summarizes the number of child work items and total reward.
- Urgent/incident generation remains a separate concept. It must not use the side-mission batch,
  side-mission mail, or `TaskKind.SideMission` merely to represent a small random action.
- The generated Work deadline includes additional schedule allowance for its child count so the
  hierarchy is not balanced as though it were a single Task.
- On campaign restore, an incomplete legacy generated side-mission Work with only one child Task is
  migrated to the hierarchical form by appending structured dependent Tasks and extending its
  deadlines. Completed and failed historical Works are not rewritten.
- The zero-inventory batch is guaranteed; ordinary chance-based generation is used only while at
  least one side mission remains.
- A failed side-mission Work leaves the remaining inventory in the same way as a completed Work.
  Hard-deadline failure is evaluated before the morning refill check, so a replacement offer can
  arrive immediately on the newly entered day without an extra empty day.

## Mail and Messenger Ordering

- Arrived unread mail marked `[NEW]` is listed above every read or resolved message. Within each
  read state, newer arrival days appear first while equal-day insertion order remains stable.
- A messenger question and its worker reply are stored and rendered as one conversation item, not
  two detached bubbles.
- Worker reports and question/reply items share one day-ordered messenger stream. Task records remain
  available in Task detail but are not rendered as a separate report section below conversation.

## Gantt and Task Detail UI

### Gantt

The Task application must present schedule information as a time-based Gantt view rather than only a flat progress list.

- The horizontal axis represents campaign days.
- Today, each Work soft deadline, and each Work hard deadline are visually distinct.
- Each Task row anchors completed history to its recorded start and completion days. Completed bars
  never move forward with the current day.
- Unstarted Tasks show their reservation or projected start; active Tasks project only their
  remaining duration from today.
- Completed progress and projected remaining work use distinguishable portions of the same bar.
- Locked Tasks remain visible and state why they cannot be entered.
- The Task application is Gantt-only; it does not combine a Task detail panel with the timeline.
- The left Work/Task column stays horizontally fixed while only the timeline pans.
- Work predecessors and Task prerequisites are shown as arrowed dependency connectors.
- Work sections are divided with `#999999` separator lines on the white background.
- Work rows expose Work state, completion, and deadline status.
- Every Task row shows its current primary or parallel assignee. Unassigned rows show `미배정`, and
  future reservations show the reserved worker and start day.
- A completed Task row continues to show the worker who completed it. Historical ownership does not
  imply that the worker is currently occupied by that Task.
- The Task name is immediately followed by Task state and the assigned worker's current condition.
- On each assigned Task row, the current-day column anchors a worker activity slot showing the
  worker, current Task, and condition. The slot reserves separate portrait, status-icon, and border
  regions implemented as code-drawn placeholders until a remote content pipeline supplies assets.

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

## Scroll and Touch Interaction

Scrollable window regions do not show a right-side scrollbar. Movement uses direct-content dragging
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
- Scroll offsets remain clamped by the scroll view.
- Window-title dragging continues to move the window and must not scroll its content.
- The window-title drag hit area is twice the visible title-bar height.
- Pressing a desktop icon for an already-open application restores it when minimized
  and raises its window above every other application window.
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
- Until remote image content delivery exists, the portrait area uses the data-defined text portrait;
  adding bitmap portraits remains a base APK or future Addressables content change.

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
