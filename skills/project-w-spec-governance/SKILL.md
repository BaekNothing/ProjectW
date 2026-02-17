---
name: project-w-spec-governance
description: Enforce Project_W local specification governance and SSOT workflow. Use when requests involve creating, updating, reviewing, or implementing features tied to /Specification, /Implementation, /Log, loop-based MVP rules, or spec-to-implementation traceability in Project_W.
---

# Project_W Spec Governance

Follow this skill for all Project_W work that touches design intent, implementation planning, or code changes derived from specification.

## Enforce Authority Model

Treat this authority order as fixed:
1. `/Specification/System`
2. Related SSOT docs under `/Specification` in this order:
   Ingame, Outgame, Metadata, Operation
3. Implementation code under `/Implementation` (Unity project files)
4. Git history

Treat external tools (Confluence, Jira, etc.) as non-authoritative.

Use `/Specification` as SSOT:
- Keep all design intent in `/Specification`.
- Read specification before implementation.
- Never let implementation redefine specification.
- Treat docs as governing source and code as output artifact.

## Restrict AI Role

Perform only these roles:
- Interpret specification docs
- Detect specification conflicts
- Propose specification updates
- Generate implementation support
- Analyze change impact

Never do the following:
- Invent design without document basis
- Add implicit rules not written in specification
- Change specification priority order

If a request pushes toward forbidden behavior, stop and ask for clarification.

## Enforce Human Responsibilities

Assume humans provide:
- Change intent
- Direction and priority
- Git approval (commit/merge)

Reject workflows that:
- Force implementation without Specification basis
- Introduce oral/implicit rules without docs
- Leave spec/implementation mismatch unresolved

## Validate Change Request First

Before specification change work, require all fields:
- `Target` (file path)
- `Action` (`Create` | `Update` | `Review`)
- `Scope` (change boundary)
- `Impact` (affected systems/areas)
- `SSOT Change` (`Yes` | `No`)

If any field is missing, treat request as invalid and ask for the missing fields.

## Run Spec Change Process

Apply this sequence strictly:
1. Validate request format
2. Analyze impact
3. Update specification first
4. Start implementation only after spec update

For impact analysis, evaluate:
- Conflicts with existing docs
- Ingame/Outgame effects
- Metadata effects
- Loop/system propagation

For spec updates:
- Record rationale
- Keep before/after structure comparable

For implementation start:
- Use updated spec as source
- Block implementation for unresolved spec items
- Write TODO in docs first, then implement

## Enforce Implementation Principles

Treat Unity project as execution output.

Apply these rules:
- Use code as interpretation of spec
- Fix code when behavior violates spec
- Never modify spec authority from code status

## Enforce Project_W Loop MVP Rules

Treat Project_W as automatic loop-based system:
- Tick-based execution
- External CSV injection allowed
- Death/reset/state-transition definitions required
- Loop cadence fixed at `1 Tick / 2 seconds`
- All systems must comply with loop rule

## Block Prohibited AI Behaviors

Do not:
- Generate code before spec validation
- Change rules based on current implementation state
- Promote single experiment results to structural rules
- Redefine human intent without explicit interpretation step

When any prohibition is triggered, stop and request reconfirmation.

## Follow Git Policy

Apply commit ordering:
1. Spec change commit
2. Implementation commit after spec commit

Require commit messages to include spec reference, e.g.:
- `[Spec] Update Ingame Loop Definition v0.2`
- `[Impl] Apply Loop State Transition Rule`

## Operational Summary

Always enforce:
- Design exists only in `/Specification`
- Implementation follows specification
- AI decisions come from docs, not assumptions
- Humans decide direction
- Git records traceable history
