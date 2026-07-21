# ProjectW agent rules

These rules apply to every AI agent working anywhere in this repository.

## Required context

Before changing code, data, Unity settings, builds, patch tooling, or GitHub releases:

1. Read `skills/project-w-hybridclr-workflow/SKILL.md` completely.
2. Read `Docs/HotUpdatePoC.md` when the task affects builds, patches, releases, or runtime startup.
3. Follow the specification skills and `Assets/Specification` SSOT when those files exist in the current branch.

## Branch policy

- Treat `ai-integration` as the persistent development and deployment branch.
- Stay on `ai-integration` and push completed work directly to `origin/ai-integration` unless the user explicitly requests another branch.
- Do not open a PR to `main`, merge into `main`, or change the patch channel to `main` without explicit user direction.
- The runtime channel is `https://raw.githubusercontent.com/BaekNothing/ProjectW/ai-integration/PatchChannels/dev.json`.
- The Architecture Doc Sync workflow is PR-only and is not part of the direct `ai-integration` deployment path.

## Hot-update boundary

- APK-fixed code: `ProjectW.Bootstrap`, `ProjectW.Contracts`, Unity/platform integration, native plugins, packages, PlayerSettings, and HybridCLR configuration.
- Patchable code: `ProjectW.HotUpdate`, currently rooted at `Assets/MilestonePrototype/Runtime`.
- Patchable content/data must live outside the fixed Bootstrap/Contracts layer and be distributed through a patch manifest or a future Addressables remote catalog.
- Do not state that Addressables content patching exists yet. The current PoC supports the hot-update DLL and manifest-listed files; remote Addressables integration is pending.
- Keep scenes dependent on the fixed bootstrap, not directly on hot-update implementation types.
- Keep `ProjectW.Contracts` minimal and stable. A breaking Contracts change requires a new base APK.

## Build and release policy

- Rebuild/reinstall the APK only for fixed-layer, package, Unity, native, Android, HybridCLR, or incompatible contract changes.
- For gameplay logic changes confined to `ProjectW.HotUpdate`, publish a patch instead of rebuilding the APK.
- Use release tags `dev-YYYYMMDD-NNN`; reset `NNN` to `001` each date.
- Publish with `tools/Publish-DevPatch.local.ps1`; it contains the local credential and is ignored by Git.
- Never print, stage, commit, or copy the token from `tools/Publish-DevPatch.local.ps1`.
- After publishing, commit and push `PatchChannels/dev.json` to `origin/ai-integration` so devices can discover the release.
- Validate tests, build results, public download size, and SHA-256 in proportion to the change.

## Completion policy

- Inspect the worktree and stage only task-related files.
- Commit and push completed work to `origin/ai-integration` unless the user explicitly says not to.
- Do not silently include generated caches, local credentials, or unrelated user changes.
