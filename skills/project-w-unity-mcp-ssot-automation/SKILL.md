---
name: project-w-unity-mcp-ssot-automation
description: Create and update Unity scripts with Unity MCP while handling Unity Editor actions in the same workflow, always grounded in Project_W SSOT documents under Assets/Specification. Use when requests involve script generation, editor manipulation, or implementation updates that must be traceable to specification and must always include paired Unity Test Framework tests.
---

# Project_W Unity MCP SSOT Automation

Use this skill to execute Unity MCP implementation work with strict SSOT traceability and mandatory test pairing.

## Enforce SSOT First

Before any script or editor change:
1. Read relevant docs under `Assets/Specification`.
2. Resolve authority in this order:
   - `Assets/Specification/System`
   - `Assets/Specification/Ingame`
   - `Assets/Specification/Outgame`
   - `Assets/Specification/Metadata`
   - `Assets/Specification/Operation`
3. Extract a concise `Spec Ref` list (file path and section title) to drive implementation.

If specification is missing or ambiguous, stop implementation and request clarification.

## Execute Script + Editor Operations Together

When the request needs both code and editor state updates, perform both in the same run:
- Use Unity MCP script tools for script creation and edits.
- Use Unity MCP editor/gameobject/component tools for scene and editor handling.
- Prefer batched Unity MCP operations when multiple actions are needed.

Keep script intent and editor state aligned to the same `Spec Ref`.

## Enforce Mandatory Paired Tests

Every newly created runtime script must include a paired Unity test script in the same task.

Pairing rules:
1. Runtime script path: `Assets/Scripts/.../<Name>.cs`
2. Test script path: `Assets/Tests/EditMode/<Name>Tests.cs` (default)
3. Test framework: Unity Test Framework with NUnit (`[Test]`, optional `[UnityTest]` when frame-based behavior is needed)
4. Minimum test coverage:
   - One behavior test for expected result
   - One edge or failure-mode test derived from spec constraints

Do not finish the task with runtime code only.

## Keep Tests Runnable

Ensure tests are runnable in Unity Test Framework:
1. Ensure EditMode test assembly setup is valid.
2. Avoid dependencies on scene-only runtime state unless explicitly required.
3. Run tests through Unity MCP test tools when available.
4. Report pass/fail and failing test names in the final result.

If tests cannot run, explain the exact blocker and required fix.

## Apply Changes Immediately

Apply approved code and editor updates directly without draft-only mode.

After applying changes:
1. Summarize updated files.
2. Show how each change maps to `Spec Ref`.
3. Include test file mapping for each runtime script.

## Enforce Version Control Discipline

Treat version control as strict and explicit:
1. Keep changes traceable by including `Spec Ref` in summaries.
2. Recommend commit message format with spec linkage.
3. Keep implementation and paired tests in the same logical change set.

Commit message pattern:
- `[Impl] <Feature or Rule> (Spec: <Doc or Section>)`
- Optional follow-up: `[Test] <Coverage Scope> (Spec: <Doc or Section>)`

## Standard Output Format

Return results in this order:
1. `Spec Ref`
2. `Implementation Changes`
3. `Editor Actions`
4. `Test Pairing`
5. `Test Execution Result`
6. `Commit Message Drafts`
7. `Open Issues` (if any)

## Stop Conditions

Stop and request reconfirmation when:
- Spec and requested behavior conflict
- User asks to skip paired tests
- User asks to implement without SSOT evidence
- Required Unity MCP capability is unavailable
