---
name: project-w-hybridclr-workflow
description: Enforce ProjectW's ai-integration branch policy and HybridCLR APK/code/data separation. Use for every ProjectW task that changes C# code, Unity scenes or settings, packages, gameplay data, APK builds, patch manifests, GitHub Releases, deployment scripts, or branch/release workflow.
---

# ProjectW HybridCLR Workflow

Preserve remote-device iteration: keep the base APK stable and deliver gameplay changes through
HybridCLR patches whenever the change boundary permits it.

## Start with repository policy

1. Read repository-root `AGENTS.md`.
2. Read `Docs/HotUpdatePoC.md` for detailed runtime and command reference.
3. Confirm the active branch with `git branch --show-current`.
4. Remain on `ai-integration` unless the user explicitly overrides it.
5. Never open or merge a `main` PR merely to deploy development patches.

## Classify every change

- **Patch-only:** gameplay rules, UI flow, simulations, or other code contained in
  `ProjectW.HotUpdate`; manifest-listed patch data.
- **Base APK:** `ProjectW.Bootstrap`, `ProjectW.Contracts`, scenes' bootstrap wiring, Unity/package
  upgrades, PlayerSettings, Android/native plugins, HybridCLR configuration, or incompatible AOT
  dependencies.
- **Content pipeline pending:** prefabs, scenes, textures, and audio do not yet have a remote
  Addressables pipeline. Do not claim they are patchable until that integration is implemented.

When uncertain, treat a change as base-APK-affecting and explain why before building.

## Preserve assembly boundaries

- Keep fixed startup/download/verification/rollback behavior in `ProjectW.Bootstrap`.
- Keep only stable DTOs and `IGameEntry` contracts in `ProjectW.Contracts`.
- Put frequently changing gameplay behavior in `ProjectW.HotUpdate`.
- Make the fixed scene reference `PatchBootstrapper`; instantiate hot-update behavior through
  `IGameEntry` after assembly loading.
- Put new patchable data outside Bootstrap/Contracts. Add it to the patch manifest or, after
  Addressables is implemented, to a remote Addressables group.
- Treat a breaking Contracts change as a new base version and rebuild the APK.

## Implement and validate

1. Read applicable ProjectW specification documents when present.
2. Make the smallest boundary-correct change.
3. Add or update Unity EditMode tests.
4. Run relevant tests.
5. For base changes, build `APK/ProjectW-HybridCLR.apk` and verify embedded HotUpdate/AOT files.
6. For patch-only changes, do not spend time rebuilding the APK.

## Publish a hot-update patch

1. Commit and push gameplay/data source changes to `origin/ai-integration`.
2. Run `tools/Publish-DevPatch.local.ps1` without printing or reading its token into output.
3. Let the wrapper choose `dev-YYYYMMDD-NNN`, or pass an explicit version when retrying.
4. Verify public Release assets against local size and SHA-256.
5. Commit and push the updated `PatchChannels/dev.json` to `origin/ai-integration`.
6. Confirm the public raw channel resolves to the new manifest.

Never overwrite an existing Release's assets. Publish corrected contents under a higher version.

## Publish a base APK

1. Configure/install HybridCLR only when missing or upgraded.
2. Run tests.
3. Build through `ProjectW/Hot Update/4. Build Base APK` or the command-line builder documented in
   `Docs/HotUpdatePoC.md`.
4. Verify the APK contains the embedded HotUpdate DLL and AOT metadata.
5. Commit the APK with fixed-layer changes and push `ai-integration`.
6. Tell the user that this APK must be installed once before subsequent patch-only iteration.

## Protect credentials and branch state

- Keep `tools/Publish-DevPatch.local.ps1` ignored and local.
- Never expose a GitHub token in logs, diffs, shell output, commits, or responses.
- Stage explicit files and preserve unrelated changes.
- Push the current `ai-integration` branch directly after successful validation.
- Do not create a `main` PR unless the user explicitly requests one.
- Keep the device channel at
  `https://raw.githubusercontent.com/BaekNothing/ProjectW/ai-integration/PatchChannels/dev.json`.
