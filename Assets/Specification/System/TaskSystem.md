# Task System

## Document Control

- Version: 0.1
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
- A Task may depend on other Tasks.
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
- A worker can hold at most one primary Task for that day.
- A primary assignment contributes one day of Task progress before outcome modifiers.
- A worker may additionally hold at most one parallel Task when that Task has no more than one day of effective work remaining.
- Parallel work costs additional fatigue defined by gameplay balance data.
- Parallel assignment does not interrupt the worker's primary Task.
- Rest, injury, or reassignment removes assignments according to the interruption rules.

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

- Soft deadline: work may continue, but the Work becomes overdue and its configured soft-deadline consequence is applied.
- Hard deadline: if required Tasks are not complete when the deadline passes, the Work and its unfinished Tasks fail.
- Deadline ownership belongs to Work by default.
- A Task-specific deadline is allowed only when explicitly supplied by external data.

Version 0.1 uses increased fatigue after a soft deadline. Further consequences may be added through data without redefining Task ownership.

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
