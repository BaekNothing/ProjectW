# Unity CLI: APK and hot-update deployment review

Date: 2026-09-12. Review only; no build, installation, release publication, or device-channel change.
Spec Ref: `Assets/Specification/Operation/UnityCliMigration.md` (Boundaries, TODO before deployment)
and `Assets/Specification/Operation/HotUpdateAotSafety.md`.

## Verdict

Both workflows can be driven by the official Unity CLI using the existing project build methods.
This is a source-and-environment feasibility finding, not a successful Android build or device test.
The CLI replaces the Editor launcher/control layer. HybridCLR, the bootstrap, Addressables, and
GitHub Release publishing continue to supply hot updates.

| Operation | Existing implementation | CLI route |
|---|---|---|
| Install local HybridCLR toolchain | `HybridClrPocBuilder.SetupFromCommandLine` | `unity run` with `-executeMethod` |
| Base APK | `HybridClrPocBuilder.BuildBaseApkFromCommandLine` | Same route; retains GenerateAll, embedded DLL/AOT, Addressables and BuildPlayer |
| Patch artifacts | `HybridClrPocBuilder.BuildPatchFromCommandLine` | Same route with `PROJECTW_PATCH_VERSION` |
| Publish DLL/data/content | `tools/Publish-DevPatch.ps1` | Existing publisher after successful build; `-SkipBuild` avoids rebuilding |
| Activate on existing devices | `PatchChannels/dev.json` | Reviewed channel commit on `ai-integration` |

Do not replace the custom base builder with a plain `unity build` invocation that omits its
HybridCLR preparation. CLI installation alone does not change an APK. Adding the Pipeline package
to this project is a package change and remains base-APK-affecting under repository policy.

## Evidence checked

- Installed CLI `1.0.0-beta.9` `run --help` explicitly supports forwarding `-executeMethod`.
- Editor `6000.3.8f1`, Android SDK/adb, NDK, and OpenJDK are installed on this PC.
- No adb device was connected at review time.
- This fresh clone has neither `HybridCLRData` nor `tools/Publish-DevPatch.local.ps1`.
  The latter was checked for existence only; no credential file was read or copied.
- Live public channel resolves to `dev-20260905-001`, `minBaseVersion: 11`, with 9 manifest files:
  HotUpdate assembly, gameplay data, AOT metadata, Addressables catalog/hash/bundles.
- Existing `APK/ProjectW-HybridCLR.apk`: 51,427,973 bytes;
  SHA-256 `bc21f669d80476023611be7a706388faae985f5b8cc4ea6434418c9f088c1bba`.
  ZIP inspection confirms embedded `ProjectW.HotUpdate.dll.bytes` and `mscorlib`, `System`,
  `System.Core` AOT metadata. This verifies archive contents, not installed-device behavior.
- Bootstrap source has `BaseVersion = 11`, size/hash validation and interrupted-boot recovery.
  It checks updates at startup; `RequestUpdate` can download while running but requires an app
  restart to activate. Editor code explicitly declines remote update checks.
- Prior CLI test run: 151/179 passed; the identical 28 project failures occur without Pipeline.
  See `UnityCliMigration.md` and `UnityCliTestFailures.md`.

## Candidate build commands (not executed in this review)

Run from this clone with its Editor closed. Check every process exit code before the next step.
Use a local build log instead of dumping Editor output, which may include authentication data.

```powershell
$projectPath = (Get-Location).Path
$unityCli = Join-Path $env:LOCALAPPDATA 'Unity\bin\unity.exe'

# Once per fresh clone, before building:
& $unityCli run $projectPath --non-interactive --timeout 1800 -- -buildTarget Android -executeMethod ProjectW.MilestonePrototype.Editor.HybridClrPocBuilder.SetupFromCommandLine -quit -logFile Logs/HybridClrSetup.log

# Base APK; overwrites APK/ProjectW-HybridCLR.apk:
& $unityCli run $projectPath --non-interactive --timeout 3600 -- -buildTarget Android -executeMethod ProjectW.MilestonePrototype.Editor.HybridClrPocBuilder.BuildBaseApkFromCommandLine -quit -logFile Logs/CliBaseApk.log

# Patch artifacts only: choose an unused YYYYMMDD-NNN release version first.
# Set $env:PROJECTW_PATCH_VERSION to that value before this command.
& $unityCli run $projectPath --non-interactive --timeout 1800 -- -buildTarget Android -executeMethod ProjectW.MilestonePrototype.Editor.HybridClrPocBuilder.BuildPatchFromCommandLine -quit -logFile Logs/CliPatch.log
```

Passing `-buildTarget Android` at startup avoids relying on the builder's in-session target switch
and domain reload behavior in batch mode. These command lines need actual build validation.
Use a fresh version: `BuildPatch` recreates its local output directory for the selected tag.

## Gaps before actual deployment

1. **Local setup and baseline:** install HybridCLR locally. Recover/produce AOT metadata from the
   correct base build. `BuildPatch` silently omits absent AOT metadata; a successful patch build
   alone therefore does not prove that its metadata matches the installed base.
2. **Launcher portability:** the publisher defaults to `D:\UnityEditors\6000.3.8f1\Editor\Unity.exe`,
   absent from this machine's checked setup. Migrate that launch to CLI, or build via CLI first
   and use the existing publisher's `-SkipBuild` after validating the exact output/version.
3. **Publication credentials:** configure an authorized local credential mechanism on this PC.
   Git push authentication does not by itself establish that this script's `GITHUB_TOKEN` is set.
4. **Release integrity:** publisher creates a public prerelease before uploading all assets, then
   updates the local channel. Add preflight and post-upload size/hash verification before channel
   activation; check tag collision, stale `-SkipBuild` output, and partial-upload recovery.
5. **Branch routing:** publisher uses the current branch for `target_commitish`, but devices always
   read `ai-integration/PatchChannels/dev.json`. Pushing only `ai-cli-itgration` cannot activate an
   update for those devices. Keep experimental artifacts away from the active channel until reviewed.
6. **AOT compatibility:** `ValidateAotSurface` immediately returns for base >= 6 and otherwise checks
   four legacy source tokens. With base 11 it is not a general compatibility gate. Follow the SSOT
   audit; do not assume any successful patch build proves new APIs exist in the installed APK.
7. **Device and base verification:** verify signing identity/versionCode for upgrade installation,
   build the package-changed base, inspect embedded files, install it, and test offline startup,
   patch download, restart activation, and interrupted-boot recovery. ADB currently has no device.
   If the new base introduces a required AOT surface, assign a new base compatibility version so
   old v11 installs cannot accept an incompatible patch merely because both report v11.
8. **Tests:** resolve or explicitly classify the existing 28 failures before using the suite as a
   deployment gate. No gameplay or test expectations were changed during this feasibility review.

## Proposed next implementation

Add a build-only CLI wrapper that selects this repository, validates versions and exit codes,
and invokes the existing builders. Then perform local APK and patch artifact builds and verify
their contents. Connect a test device for the end-to-end check before activating a public channel.
Keep publication as a distinct step so a build experiment cannot silently update devices.

References: [official CLI reference](https://docs.unity.com/en-us/unity-cli/unity-cli-reference),
`Assets/MilestonePrototype/Editor/HybridClrPocBuilder.cs`, `tools/Publish-DevPatch.ps1`,
`Assets/Scripts/Bootstrap/PatchBootstrapper.cs`, and `Docs/HotUpdatePoC.md`.
