# Task System

## Document Control

- Version: 0.4
- Status: Approved for implementation
- Action: Create
- SSOT Change: Yes
- Rationale: Replace the prototype's code-defined milestone data with a schedulable work system whose scarce resource is each worker's day.
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

Task states:

1. `Locked`: its Work or Task prerequisite is locked.
2. `Available`: it may receive an assignment.
3. `Active`: it has a primary or parallel assignment.
4. `Complete`: its base duration and accumulated context cost are paid.
5. `Failed`: its parent Work failed.

Completion immediately refreshes dependent states. A newly unlocked Task can be assigned before the next day is advanced.

## Day and Assignment Rules

- One turn advances the calendar by one day.
- The operations desktop exposes an always-visible `다음날로` button at the bottom-right above the
  taskbar. It is disabled after campaign victory or loss.
- A worker can hold at most one primary Task for that day.
- Each worker owns a daily output value. A primary assignment contributes that output toward the
  Task workload before outcome modifiers.
- Daily output lands in a low band 20% of the time, the expected band 60% of the time, and a high
  band 20% of the time. Version 0.3 multipliers are data-driven and default to 0.7 / 1.0 / 1.3.
- A worker may additionally hold at most one parallel Task when that Task has no more than one day of effective work remaining.
- Parallel work costs additional fatigue defined by gameplay balance data.
- Parallel assignment does not interrupt the worker's primary Task.
- Rest, injury, or reassignment removes assignments according to the interruption rules.

## Task Scheduling

- A player may reserve a Task for one worker on a calendar day from the current day through the
  campaign end day.
- A worker cannot hold two primary reservations on the same day.
- At the beginning of the reserved day's daily cycle, the reservation attempts a normal primary
  assignment. Existing assignment, interruption, availability, and handover rules still apply.
- If the Task cannot be assigned because it has completed, failed, is locked, or the worker is
  unavailable, the reservation is consumed and the day report records that it did not start.
- A reservation can be replaced or cancelled before it is consumed.
- Reservation day and worker are part of the campaign save snapshot.

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

## Random Work Generation

- The daily cycle may generate optional Works up to the configured active limit.
- Every generated Work receives a soft deadline, later hard deadline, credit reward, soft penalty,
  and larger hard penalty.
- Generated Tasks receive a workload and required role.
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

## External Data

Gameplay content and balance values must not be authored as constructor literals or hard-coded creation methods.

External data includes:

- campaign duration and starting resources
- Work definitions and predecessor IDs
- Task definitions, durations, roles, difficulty, and prerequisite IDs
- worker definitions and initial stats
- assignment, interruption, parallel-work, fatigue, outcome, and perk balance values
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

## Gantt and Task Detail UI

### Gantt

The Task application must present schedule information as a time-based Gantt view rather than only a flat progress list.

- The horizontal axis represents campaign days.
- Today, each Work soft deadline, and each Work hard deadline are visually distinct.
- Each Task row shows its projected segment from the current day through its effective remaining duration.
- Completed progress and projected remaining work use distinguishable portions of the same bar.
- Locked Tasks remain visible and state why they cannot be entered.
- The Task application is Gantt-only; it does not combine a Task detail panel with the timeline.
- The left Work/Task column stays horizontally fixed while only the timeline pans.
- Work predecessors and Task prerequisites are shown as arrowed dependency connectors.
- Work sections are divided with `#999999` separator lines on the white background.
- Work rows expose Work state, completion, and deadline status.

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

Every scrollable window region keeps its visible scrollbar and also supports direct-content dragging.

- The complete operations UI uses a `1.8x` accessibility magnification over the responsive screen
  scale. Font size, padding, controls, panels, windows, taskbar, and touch targets share the same
  magnification so text does not outgrow its layout.
- When magnification reduces the logical desktop below the compact breakpoint, application windows
  use the existing near-full-screen compact layout and retain scrollable content.

- Mouse drag and single-finger touch drag pan the content in the opposite direction of pointer movement.
- Both horizontal and vertical scroll axes are supported when the content exceeds the viewport.
- A press remains a normal button press until movement exceeds the drag threshold.
- Crossing the drag threshold cancels click intent for that gesture and begins scrolling.
- Scroll offsets remain clamped by the scroll view.
- Window-title dragging continues to move the window and must not scroll its content.
- The window-title drag hit area is twice the visible title-bar height.
- Minimize and close keep their visual size but use hit areas twice their width and height.
- Escape closes the active non-minimized window, then the next active window on another press.
- Nested scroll regions use the region where the gesture began and do not transfer the gesture mid-drag.

## Crew Detail UI

- Selecting a crew member opens a separate crew-detail window.
- The detail shows a portrait area, name, memo, perks, current assignment, and work history.
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
