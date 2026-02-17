# Project_W Skills Quick Guide

This is a short usage guide for the skills created in this project, with Claude/Cursor + Unity MCP assumptions.

## Environment Assumptions

- AI client: Claude/Cursor style agent
- MCP: `unityMCP` enabled
  - config: `c:\Users\king0\.cursor\mcp.json`
  - endpoint: `http://127.0.0.1:8080/mcp`
- SSOT root: `Assets/Specification`

## How to Trigger a Skill

Use explicit skill names in your prompt.

- `$project-w-spec-governance`
- `$project-w-spec-collab-update`
- `$project-w-unity-mcp-ssot-automation`

Recommended prompt structure:
1. Declare the skill name first.
2. Provide goal + target file path.
3. Provide required input fields in one message.

## Skill 1: `project-w-spec-governance`

Use when:
- You need SSOT authority checks.
- You need spec conflict detection.
- You need to validate spec-first order before implementation.

Example:
```text
$project-w-spec-governance
Validate this request.
Target: Assets/Specification/Ingame/...
Action: Update
Scope: Intervention delay rule
Impact: Ingame loop, Metadata timing
SSOT Change: Yes
```

## Skill 2: `project-w-spec-collab-update`

Use when:
- You want direct in-place updates to `Assets/Specification`.
- You want legacy Confluence/Jira-oriented docs replaced with local SSOT wording.

Required fields:
- `Target`
- `Action` (`Create` | `Update` | `Review`)
- `Scope`
- `Impact`
- `SSOT Change` (`Yes` | `No`)

Example:
```text
$project-w-spec-collab-update
Target: Assets/Specification/SSOT - Jira Operation.md
Action: Update
Scope: Remove Jira-authoritative flow, replace with local SSOT process
Impact: Operation + System Index references
SSOT Change: Yes
```

## Skill 3: `project-w-unity-mcp-ssot-automation`

Use when:
- You need Unity MCP script changes and editor handling in one workflow.
- You need implementation traceable to SSOT references.
- You need mandatory paired Unity tests for new runtime scripts.

Key enforcement:
- No implementation without `Spec Ref`.
- New runtime script must include paired Unity Test Framework test.
- Test execution result must be reported.

Example:
```text
$project-w-unity-mcp-ssot-automation
Spec Ref: Assets/Specification/Ingame/CoreLoop/03 - Intervention Boundary.md
Create runtime script + paired EditMode test.
Apply required Unity editor/gameobject setup through Unity MCP.
```

## Recommended Combined Flow

1. Run `project-w-spec-governance` for request and conflict validation.
2. Run `project-w-spec-collab-update` if specification text must be updated.
3. Run `project-w-unity-mcp-ssot-automation` for implementation + editor + test pairing.

Default lifecycle:
- SSOT validation -> Spec update -> Implementation -> Test execution
