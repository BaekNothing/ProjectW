# Content Data Structure and Assistance Guide

## Document Control

- Version: 1.0
- Status: Authoring specification
- Runtime schema: `ProjectW.MilestonePrototype.TaskSystemData`, schema version 1
- Runtime artifact: `Assets/MilestonePrototype/Resources/task-system.json`
- Authoring template: `Docs/DataAuthoring/ProjectW-CoreAuthoringTemplate.md`
- Purpose: Separate human-authored intent from derived data and define where Codex may safely assist.

## Ownership Model

ProjectW uses three conceptual layers.

1. **Core authoring:** The designer owns premise, intent, player meaning, key wording, and exceptional
   behavior.
2. **Assisted expansion:** Codex may propose structured variants, fill repetitive fields, calculate
   derived values, and identify missing coverage. The designer approves the result.
3. **Compiled runtime data:** A future deterministic compiler converts approved authoring rows into
   the exact `TaskSystemData` JSON structure. Until that compiler exists, `task-system.json` remains
   the production SSOT and Markdown rows are authoring drafts.

The Markdown template must not be presented as an active runtime source until an importer/compiler,
validation tests, and an explicit SSOT change are committed.

## Authoring Principles

- Stable IDs are permanent. Rename display text instead of recycling an ID.
- One row represents one identity. Nested runtime arrays are flattened into linked sheets.
- References use IDs, never display names or row numbers.
- Blank means unspecified; it must not silently mean zero, false, or an empty array.
- Enumerations use their exact C# names.
- Runtime state fields such as progress, assignment, completion flags, and read state are not core
  authoring inputs unless an explicit starting-state feature requires them.
- Formula-derived or assistant-generated cells remain distinguishable from designer-owned cells.
- All generated proposals retain their source row and generation note.

## Responsibility Classes

| Class | Owner | Examples | Acceptance |
|---|---|---|---|
| `CORE` | Designer | premise, Work name, narrative purpose, key choice, intended consequence | Explicit designer approval |
| `ASSIST` | Designer with Codex | Task breakdown, mail draft, event variants, tags, competency suggestions | Review and edit before approval |
| `DERIVED` | Tool | IDs from approved prefixes, deadline checks, estimated duration, JSON nesting | Recomputed, never hand-maintained |
| `RUNTIME` | Game | progress, state, assignee, history, generated IDs | Excluded from authoring template |

## Markdown Table Contract

The authoring template is deliberately plain text so Git and review tools can show meaningful diffs.
Its grammar is:

- `# SheetName` starts one logical table;
- `## StableRowIdentity` starts one row;
- `FieldName : Value` stores a scalar cell and is split only on the first exact ` : ` token;
- `FieldName : [...]` stores an array as a JSON array;
- `FieldName : |` starts a multiline value whose following lines are indented by two spaces;
- `null` means an absent value and `""` means an intentional empty string.

Section order and row order are deterministic. A parser must reject duplicate sheet names, duplicate
row identities within a sheet, duplicate field names within a row, malformed arrays, and unindented
multiline content.

### `README`

Explains color ownership, edit order, enum values, competency indices, and the export boundary. This
sheet is instructional and is not compiled.

### `CoreWorks`

One row per designer-authored Work.

| Column | Type | Owner | Runtime mapping / meaning |
|---|---|---|---|
| `AuthoringStatus` | enum | CORE | `Idea`, `Draft`, `Review`, `Approved`, `Hold` |
| `WorkId` | stable ID | CORE | `Works[].Id` |
| `Name` | text | CORE | `Works[].Name` |
| `NarrativePurpose` | text | CORE | Design intent; not exported |
| `PlayerDecision` | text | CORE | Intended decision pressure; not exported |
| `Required` | boolean | CORE | `Works[].Required` |
| `RevealDay` | integer | CORE | `Works[].RevealDay` |
| `SoftDeadline` | integer | CORE | `Works[].SoftDeadline` |
| `HardDeadline` | integer | CORE | `Works[].HardDeadline` |
| `RewardCredits` | integer | ASSIST | `Works[].RewardCredits` |
| `SoftPenaltyCredits` | integer | ASSIST | `Works[].SoftPenaltyCredits` |
| `HardPenaltyCredits` | integer | ASSIST | `Works[].HardPenaltyCredits` |
| `PredecessorWorkIds` | ID list | CORE | `Works[].PredecessorIds` |
| `ThemeTags` | tag list | CORE | Assistance metadata; not exported |
| `GenerationBrief` | text | CORE | What Codex may expand |
| `DoNotGenerate` | text | CORE | Constraints and forbidden directions |
| `EstimatedRequiredDays` | formula | DERIVED | Sum of required child Task work |
| `ScheduleSlack` | formula | DERIVED | Soft deadline window minus estimated days |
| `Validation` | parser/tool | DERIVED | Row-level authoring warnings |

Semicolon-delimited ID and tag lists are an authoring convenience. The compiler must trim, reject
duplicates, and emit arrays.

### `CoreTasks`

One row per authored Task. Tasks are the smallest assignable units.

| Column | Type | Owner | Runtime mapping / meaning |
|---|---|---|---|
| `AuthoringStatus` | enum | CORE | Workflow state |
| `TaskId` | stable ID | CORE | `Tasks[].Id` |
| `WorkId` | Work ID | CORE | `Tasks[].GroupId` |
| `Sequence` | integer | CORE | Authoring order; not exported |
| `Name` | text | CORE | `Tasks[].Name` |
| `TaskIntent` | text | CORE | What changes when performed; not exported |
| `Kind` | enum | CORE | `Milestone`, `SideMission`, `Recovery`, `Regeneration` |
| `Required` | boolean | CORE | `Tasks[].Required` |
| `RequiredRole` | enum | ASSIST | `Tech`, `Analysis`, `Management`, `Adaptation` |
| `Competency1..3` | integer | ASSIST | Unique indices; emit compact array |
| `RequiredWork` | half-day number | ASSIST | `Tasks[].RequiredWork` |
| `PrerequisiteTaskId` | Task ID | CORE | `Tasks[].PrerequisiteId` |
| `Risk` | enum | CORE | `Low`, `Medium`, `High` |
| `Importance` | enum | CORE | `Low`, `Medium`, `High` |
| `Difficulty` | integer | ASSIST | Authored difficulty tier; current source uses `1..3` and the runtime loader does not impose an explicit range |
| `GenerationBrief` | text | CORE | Expansion request |
| `DoNotGenerate` | text | CORE | Prohibited semantics |
| `ExpectedOutputAtStandard` | formula | DERIVED | Baseline estimate |
| `EstimatedDays` | formula | DERIVED | Work divided by baseline output |
| `Validation` | parser/tool | DERIVED | Reference, range, and duplicate checks |

Do not author `Progress`, `AssignedCharacter`, `State`, `DelayDays`, `ContextCostDays`, `SplitCount`,
`LastWorker`, result fields, scheduling fields, generated word IDs, or records. They are runtime state.

### `CoreMail`

One row per authored mail item.

| Column | Owner | Runtime mapping / meaning |
|---|---|---|
| `AuthoringStatus` | CORE | Workflow state |
| `MailId` | CORE | `Mail[].Id` |
| `ArrivalDay` | CORE | `Mail[].ArrivalDay` |
| `From` | CORE | `Mail[].From` |
| `Subject` | CORE | `Mail[].Subject` |
| `BodyBrief` | CORE | Required facts and voice; not exported |
| `Body` | CORE/ASSIST | `Mail[].Body`; designer approves final text |
| `Instruction` | CORE | `Mail[].Instruction` |
| `TargetWorkId` | CORE | `Mail[].TargetWorkId` |
| `DeadlineDelta` | CORE | `Mail[].DeadlineDelta` |
| `ResourceDelta` | CORE | `Mail[].ResourceDelta` |
| `Risk` | CORE | `Mail[].Risk` |
| `GenerationBrief` / `DoNotGenerate` | CORE | Assistance boundary |
| `Validation` | DERIVED | Arrival, target, and required-field checks |

`Read`, `Resolved`, generated medical results, and runtime critical linkage are not normal authored
mail inputs.

### `CoreEvents`

One row per Critical Event chain.

| Column | Owner | Runtime mapping / meaning |
|---|---|---|
| `AuthoringStatus` | CORE | Workflow state |
| `EventId` | CORE | `CriticalEvents[].Id` |
| `StartDay` | CORE | `CriticalEvents[].StartDay` |
| `FirstNodeId` | CORE | `CriticalEvents[].FirstNodeId` |
| `Premise` | CORE | Human-owned event thesis; not exported |
| `DecisionQuestion` | CORE | Central player decision; not exported |
| `DesiredAftermath` | CORE | Intended consequence range; not exported |
| `GenerationBrief` / `DoNotGenerate` | CORE | Assistance boundary |
| `Validation` | DERIVED | First-node and identity checks |

### `EventNodes`

| Column | Owner | Runtime mapping / meaning |
|---|---|---|
| `EventId`, `NodeId` | CORE | Parent and `Nodes[].Id` |
| `From`, `Subject` | CORE | Sender and subject |
| `BodyBrief` | CORE | Required narrative facts |
| `Body` | CORE/ASSIST | Final approved `Nodes[].Body` |
| `Risk` | CORE | `Nodes[].Risk` |
| `TerminalIntent` | CORE | Design note; not exported |

### `EventChoices`

| Column | Owner | Runtime mapping / meaning |
|---|---|---|
| `EventId`, `NodeId`, `ChoiceId` | CORE | Parent keys and `Choices[].Id` |
| `Text` | CORE | Player-facing choice |
| `Forecast` | CORE | Player-facing uncertainty |
| `ChoiceIntent` | CORE | Why this option exists; not exported |

### `EventOutcomes`

| Column | Owner | Runtime mapping / meaning |
|---|---|---|
| parent keys + `OutcomeIndex` | CORE | Stable authoring identity and order |
| `Weight` | ASSIST | `Outcomes[].Weight`, positive integer |
| `Text` | CORE/ASSIST | Result text |
| `ResourceDelta` | CORE | Result economy effect |
| `CrewIndex` | CORE | `-1` or valid roster position |
| `FatigueDelta` | CORE | Result condition effect |
| `SuccessChanceDelta` | CORE | Run-level Task outcome adjustment |
| `NextNodeId` | CORE | Empty means terminal outcome |
| `ConsequenceIntent` | CORE | Design note; not exported |

Outcome weights are relative, not required to sum to 100. An authoring report may display normalized
probability as `Weight / choice total weight` for review.

### `WordPools`

Use one sheet with a `WordType` discriminator or three separate sheets. Exact runtime mappings are:

- adjective: `Id`, `Text`, `Risk`, `Difficulty`;
- target: `Id`, `Text`, `Role`, `RequiredCompetencies`, `Difficulty`;
- action: `Id`, `Text`, `Role`, `RequiredCompetencies`, `Difficulty`.

Authoring-only fields should include semantic tags, compatible tags, forbidden tags, tone, example
combination, and approval status. Codex may expand word candidates but must not approve semantic
compatibility itself.

### `Crew`

Exactly four approved rows are currently required. The designer owns name, portrait label, memo,
personality, perks, specialty, and character identity. Codex may review competency coverage and
suggest numeric alternatives. Exported fields are the current `CrewMember` initial-definition fields;
runtime condition, assignment, history, and medical fields are excluded.

### `Codex`

The designer owns category, name, factual intent, and statements about what is or is not implemented.
Codex may draft prose and synchronize numeric references, but every approved balance or behavior
change must update affected Codex rows in the same change. Never convert planned behavior into a
claim of current behavior.

### `Balance`

One row per `TaskSystemBalance` field with value, unit, design intent, safe experiment range, and
affected systems. Field names must match C# exactly. Codex may run sensitivity analysis and propose
values; the designer approves all economy and difficulty targets.

### `EnumsAndTags`

Provides validation lists and definitions. Enum spelling must be exact:

- Work roles: `Tech`, `Analysis`, `Management`, `Adaptation`;
- Task kinds: `Milestone`, `SideMission`, `Recovery`, `Regeneration`;
- risk and importance: `Low`, `Medium`, `High`;
- authoring status: `Idea`, `Draft`, `Review`, `Approved`, `Hold`.

Tags are authoring metadata and are not emitted until a runtime feature explicitly consumes them.

## Codex Assistance Contract

### Codex may do without inventing design intent

- normalize IDs and flag collisions;
- expand a Work brief into candidate Task breakdowns;
- draft mail and event prose from explicit facts and voice constraints;
- suggest roles, competencies, workload, rewards, and deadlines with reasons;
- generate variations constrained by tags and `DoNotGenerate`;
- find missing role, competency, risk, date, or theme coverage;
- detect broken references, cycles, unreachable nodes, and duplicated wording;
- compare a new batch with existing content;
- produce deterministic JSON from approved rows once a compiler exists;
- run fixed-seed and Monte Carlo simulations and summarize outliers.

### Codex must ask or leave a proposal when

- the premise, intended player dilemma, canon, or tone is absent;
- a new mechanic or runtime field appears necessary;
- a proposed result changes campaign-loss rules or core economy direction;
- two authored constraints conflict;
- semantic compatibility cannot be inferred from approved tags;
- a change would require HotUpdate code, Contracts, Unity, package, platform, or native work.

### Codex must not

- silently replace core prose or designer intent;
- recycle an existing stable ID for different content;
- treat generated drafts as approved content;
- fabricate runtime support for an authoring-only field;
- hide invalid data by supplying defaults;
- claim that editor compilation proves installed-device AOT compatibility;
- claim remote Addressables content patching exists;
- publish gameplay data without the repository's validation, AOT audit when relevant, and explicit
  task authorization.

## Proposed Authoring Workflow

1. Designer creates `CORE` rows with premise, decision, constraints, and approval status.
2. Codex returns candidate `ASSIST` rows linked to their source IDs.
3. Designer edits and marks accepted rows `Approved`.
4. Validation checks identities, references, graph reachability, value ranges, and coverage.
5. A compiler emits a canonical `TaskSystemData` document in stable sheet and row order.
6. `TaskSystemDataLoader.Validate` and EditMode tests validate the emitted document.
7. Fixed-seed and Monte Carlo simulations produce a balance report.
8. Designer approves the batch and updates affected Codex entries.
9. Production JSON is committed and, when requested, published through the development patch flow.

## Compile and Validation Rules

A future Markdown authoring compiler must:

- read only `Approved` rows for production output;
- preserve deterministic ordering;
- reject duplicate or blank stable IDs;
- reject missing Work, Task, node, choice, mail, and word references;
- reject Work and Task dependency cycles;
- reject unreachable event nodes and invalid next-node links;
- compact competency columns into one-to-three unique integer arrays;
- parse semicolon lists without retaining whitespace or empty members;
- distinguish blank from numeric zero and boolean false;
- exclude all authoring-only columns;
- set only intentional initial runtime values;
- emit JSON field names matching C# models exactly;
- validate the complete output using the runtime validator;
- report every error with sheet, row, column, stable ID, and remediation text;
- write no production JSON when any error remains.

## Current Runtime Validation Baseline

The current loader requires:

- `SchemaVersion = 1`;
- positive campaign and midpoint-review days;
- non-negative starting resources;
- non-null Balance, Works, Tasks, Crew, RandomTaskWords, and non-empty Codex and word-pool arrays;
- exactly four crew members;
- six `0..7` competency values for every crew member;
- one to three unique competency indices per Task, target, and action;
- valid campaign and Balance ranges explicitly checked by `TaskSystemDataLoader`;
- target/action role compatibility;
- non-empty, uniquely identified Codex entries;
- a valid first node, non-empty choices, positive total outcome weight, and valid next-node references
  for each critical event.

The current runtime validator does not yet enforce every proposed authoring rule, including all Work,
Task, Mail, dependency, identity, difficulty, deadline, and workload checks. The future compiler must
apply the stricter rules in this document before calling the runtime validator.

The compiler should mirror these rules for fast feedback, but the runtime validator remains the final
compatibility gate.

## Change Boundary

These specifications and the Markdown template are authoring assets only. They do not change runtime data,
HotUpdate code, the base APK, or the patch channel. Implementing the compiler or changing runtime
schema requires a separate reviewed task. A breaking Contracts change requires a new base APK.
