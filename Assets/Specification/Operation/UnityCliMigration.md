# Unity CLI migration experiment

## Document Control

- Date: 2026-09-12
- Target: `Assets/Specification/Operation/UnityCliMigration.md`
- Action: Create
- Scope: Development automation on `ai-cli-itgration`, based on `ai-integration`.
- Impact: Unity package resolution, local Editor automation, agent instructions, validation tooling.
- SSOT Change: Yes (operation workflow only).
- Rationale: User requested evaluation and migration to the new official Unity CLI.

## Boundaries

- Keep Unity at `6000.3.8f1` and retain gameplay, data, Bootstrap, Contracts, and HybridCLR behavior.
- Keep the device channel on `ai-integration`. This experiment does not publish a patch or APK.
- Package changes are base-APK-affecting under repository policy. Before deployment, build and
  verify a new base APK; Editor checks alone do not prove device compatibility.
- Existing System specifications remain authoritative; no gameplay rules change.

## Migration and acceptance

1. Install the official Unity CLI and record the tested version.
2. Install Unity Pipeline using the official CLI into this explicitly selected project.
3. Verify package resolution, compilation, project identity, live command discovery, and existing
   EditMode tests before declaring the live workflow usable.
4. Prefer direct CLI commands for shell-capable agents. Always select this project explicitly;
   never rely on whichever Editor is discovered first.
5. Retain Coplay MCP until live CLI validation succeeds. Its removal is conditional on that gate.
6. Record actual results, blockers, exact commands, and rollback instructions in
   `Docs/UnityCliMigration.md`. Do not present an attempted check as passed.

## TODO before deployment

- Validate a base APK build and on-device smoke test if these package changes are deployed.
- Review command coverage beyond the initial compilation and EditMode smoke checks.

## References

- https://docs.unity.com/en-us/unity-cli/use-unity-cli
- https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package
- https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli
