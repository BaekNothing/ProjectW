# ProjectW HybridCLR development patch workflow

This PoC keeps the Unity/Android integration in the base APK and loads gameplay code from a
HybridCLR assembly. Devices check for an update only when the app starts.

## Runtime layout

- `ProjectW.Bootstrap`: fixed in the APK; downloads, verifies, promotes, and rolls back patches.
- `ProjectW.Contracts`: fixed in the APK; the minimal contract between the bootstrap and gameplay.
- `ProjectW.HotUpdate`: downloaded gameplay assembly.
- `PatchChannels/dev.json`: stable public pointer to the active GitHub Release manifest.

The device stores patch slots below `Application.persistentDataPath/patches`:

- `current`: active patch.
- `previous`: rollback patch.
- `staging`: incomplete download that is never executed.
- `boot-pending`: removed only after hot-update startup reports success.

If the previous app run stopped before reporting success, the next launch restores `previous`.
If the network or verification fails, the last valid patch or the APK-embedded assembly starts.

## One-time/base APK workflow

Use Unity `6000.3.8f1` with Android Build Support and its SDK/NDK installed.

From the Unity menu:

1. Run `ProjectW > Hot Update > 1. Configure HybridCLR`.
2. Run `ProjectW > Hot Update > 2. Install HybridCLR Runtime` after a fresh clone or package update.
3. Run `ProjectW > Hot Update > 4. Build Base APK`.
4. Install `APK/ProjectW-HybridCLR.apk` on the remote device.

The base build runs HybridCLR `Generate/All`, embeds a fallback hot-update DLL and AOT metadata,
and produces a development APK. Rebuild/reinstall it when Bootstrap, Contracts, Unity packages,
native plugins, Android settings, Unity, or HybridCLR change.

## Routine code patch workflow

Keep gameplay changes inside the `ProjectW.HotUpdate` assembly. The local credential wrapper
`tools/Publish-DevPatch.local.ps1` is ignored by Git. Put the fine-grained token in its
`$githubToken` value once, then the LLM can build and publish the next patch with:

```powershell
.\tools\Publish-DevPatch.local.ps1
```

The wrapper derives the next date/sequence version from `PatchChannels/dev.json`. An explicit
version remains available with `-Version 20260721-002`. The token needs `Contents: write` only for
`BaekNothing/ProjectW`.
It stays on the development PC and is never stored in the APK or repository.

The script:

1. Compiles the Android hot-update assembly.
2. Creates `PatchBuild/dev-YYYYMMDD-NNN` with DLL, available AOT metadata, hashes, and manifest.
3. Creates a public prerelease such as `dev-20260721-001` and uploads its assets.
4. Updates the local `PatchChannels/dev.json` pointer.

Review, commit, and push `PatchChannels/dev.json` to activate the release:

```powershell
git add PatchChannels/dev.json
git commit -m "Activate development patch dev-20260721-001"
git push
```

On its next launch, the device fetches the channel pointer from the public `ai-integration` branch,
downloads the immutable Release assets into `staging`, verifies size and SHA-256, and promotes the
patch. This development workflow does not require merging into `main`.

## Rollback

To roll all devices back, edit `PatchChannels/dev.json` so `manifestUrl` references an older valid
release, then commit and push that single file. Patch versions are monotonic on-device, so a normal
lower version is ignored. For an intentional rollback, publish the older known-good contents again
under a new, higher `dev-YYYYMMDD-NNN` version and point the channel to it.

Do not overwrite assets in an existing release. Every manifest uses tag-specific immutable URLs so
a device cannot combine files from two releases.

## Current PoC boundary

Code and arbitrary files listed in the manifest can be patched now. Addressables remote catalogs are
not part of this first PoC; add them after the code path is proven on the remote device. Until then,
large prefab, scene, texture, and audio changes still require a base APK.

## AOT compatibility warning

The downloaded assembly can replace managed gameplay logic, but it cannot assume that arbitrary
Unity, package, platform, native, or generic AOT implementations exist in the installed APK.
Supplemental AOT metadata does not add missing native implementations.

Before publishing a patch, audit every newly referenced external member and exact overload against
the installed base APK. Editor compilation and tests are not sufficient evidence. Follow
`Assets/Specification/Operation/HotUpdateAotSafety.md`.

If the normal implementation needs an unproven AOT member, do not hide it behind reflection or a
distorted workaround. Tell the user, reclassify the change as base-APK-affecting, preserve the
required surface explicitly, and build a new APK.
