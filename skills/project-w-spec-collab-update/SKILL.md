---
name: project-w-spec-collab-update
description: Collect structured user input and directly update Project_W Specification files using local SSOT rules, including immediate replacement of legacy Confluence/Jira-oriented documents. Use when requests involve Create/Update/Review actions on Assets/Specification and require change summary, conflict checks, impact analysis, and spec-referenced commit guidance.
---

# Project_W Spec Collaborative Update

Execute specification update operations from user input. Keep design authority in local SSOT docs.

## Enforce Input Contract

Require all fields before any update:
- `Target` (file path)
- `Action` (`Create` | `Update` | `Review`)
- `Scope` (change boundary)
- `Impact` (affected systems or areas)
- `SSOT Change` (`Yes` | `No`)

Reject incomplete requests. Ask only for missing fields, then resume.

## Enforce Authority Order

Interpret documents in this fixed order:
1. `/Specification/System`
2. `/Specification/Ingame`
3. `/Specification/Outgame`
4. `/Specification/Metadata`
5. `/Specification/Operation`
6. Implementation code
7. Git history

Treat Confluence, Jira, and other external tools as non-authoritative references.

## Run Update Workflow

Apply this sequence every time:
1. Validate the 5-field request
2. Parse target and scope
3. Detect conflicts across SSOT documents
4. Update target document directly
5. Re-check cross-document consistency
6. Return standardized output

Block updates when critical conflicts remain unresolved.

## Apply Legacy Immediate Replacement Rules

When the target is a legacy document, replace legacy process language immediately with local SSOT language.

Legacy priority targets:
- `Assets/Specification/SSOT – Workflow Confluence × Unity × GitHub.md`
- `Assets/Specification/SSOT – Jira Operation.md`
- `Assets/Specification/Jira Epic Template – AI Optimized.md`

Replacement rules:
1. Remove statements that make Jira/Confluence authoritative.
2. Rewrite process logic around local `/Specification` SSOT ownership.
3. Keep tool mentions only as optional operational references.
4. Align navigation and references with `Assets/Specification/Project_W – System Index (AI Entry Point).md`.

Assume immediate replacement mode by default. Do not archive legacy versions unless explicitly requested.

## Enforce Direct-Edit Policy

Update existing specification files in place.

Maintain clear traceability in the updated content:
- State rationale for important changes.
- Preserve structure so before/after comparison is possible.
- Keep unresolved items explicit as TODOs in specification text.

Do not update implementation code in this skill.

## Run Cross-Document Conflict Checks

Check consistency across:
- Ingame
- Outgame
- Metadata
- Operation

If conflicts are found:
1. Stop automatic merge.
2. Report exact conflict points.
3. Ask for additional user input.

## Enforce Standard Output Contract

Return results in this exact section order:
1. `Change Summary`
2. `Conflict Check`
3. `Impact Matrix` (Ingame, Outgame, Metadata, Operation)
4. `Applied Sections` (before/after comparable section list)
5. `Spec Ref Commit Message` drafts

Commit message pattern examples:
- `[Spec] Update <Doc or Rule> <Version>`
- `[Impl] Apply <Rule or Constraint>` (only if implementation follow-up is requested)

After task completion, finish version-control flow unless the user explicitly says not to:
1. `git add` updated specification files
2. `git commit` with `Spec Ref` linkage
3. `git push` current branch

If push fails, report the failure reason and required user action.

## Handle Failure and Reconfirmation

Stop and request reconfirmation when any of these occurs:
- Missing required input fields
- Target outside `/Assets/Specification`
- Spec conflicts that require policy decisions
- Requests to prioritize code changes before specification changes

Keep responses in English by default.
