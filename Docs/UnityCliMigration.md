# Official Unity CLI migration

Spec Ref: `Assets/Specification/Operation/UnityCliMigration.md`

This experiment starts from `ai-integration` commit
`e8afe0fa775218d827e74f6533231b3f33943084` on branch `ai-cli-itgration`.
The independent clone is `C:\Users\king0\Desktop\works\ProjectW-ai-cli-itgration`.
The existing `works\ProjectW` checkout is separate.

## Versions and setup

- Unity Editor: `6000.3.8f1` (unchanged).
- Official Unity CLI: `1.0.0-beta.9`.
- Unity Pipeline: `0.7.0-exp.1`, installed with `unity pipeline install`.
- Previous transport: `com.coplaydev.unity-mcp` (Coplay, a third-party package).

Install the CLI using the [official installation guide](https://docs.unity.com/en-us/unity-cli/use-unity-cli).
This machine uses `%LOCALAPPDATA%\Unity\bin\unity.exe`; restart the terminal for PATH discovery,
or invoke that executable directly. The official PowerShell installer verifies its SHA-256.
Check `unity --version` and use installed `--help` as the authority for CLI syntax.

From this repository in PowerShell:

```powershell
$projectPath = (Get-Location).Path
$unityCli = Join-Path $env:LOCALAPPDATA 'Unity\bin\unity.exe'
& $unityCli pipeline install --project-path $projectPath --package-version 0.7.0-exp.1
& $unityCli open $projectPath
# Wait for initial import and compilation.
& $unityCli pipeline list --json
& $unityCli command --project-path $projectPath --json
```

`pipeline list` describes installation; a manifest entry alone does not prove a reachable server.
Use the live `command` response to discover names and parameter schemas before issuing operations.
Always pass `--project-path` to commands that control a live Editor. A second checkout can be open
at the same time, so automatic selection is not an acceptable project identity check.

During a cold import, CLI beta.9 `status` can report `ready` while a main-thread command still
returns `503 Server Busy` from Pipeline. Treat the actual command response as authoritative;
allow import to finish and retry. `recompile_status` remains available during this interval.

The live test invocation discovered from Pipeline 0.7 is:

```powershell
& $unityCli command --project-path $projectPath run_tests --mode editor --filter ProjectW.MilestonePrototype.Tests --filter_type assembly --async_tests true --json
& $unityCli command --project-path $projectPath test_status --json
```

Poll `test_status` until completion and inspect the returned test counts and failures, including
the inner JSON `result` when the CLI wraps it as a string. Successful request submission alone
does not mean tests passed.

For background validation, the tested launch form is:

```powershell
& $unityCli open $projectPath --args '-batchmode -nographics -logFile Logs/UnityCliEditor.log' --non-interactive
```

For a headless EditMode suite with the project closed:

```powershell
& $unityCli test $projectPath --mode EditMode --output Logs/cli-editmode.xml --timeout 600 --non-interactive -- -nographics
```

Do not start a second batch Editor against an already-open project. Use the discovered live test
command instead, or save and close the Editor first. `unity close` does not save work automatically.

## Validation record

On 2026-09-12:

- Official CLI installation and checksum verification completed.
- UPM resolved Pipeline `0.7.0-exp.1`. The lock diff adds Pipeline and changes Mono.Cecil's
  dependency depth from 2 to 1; it does not upgrade existing dependency versions.
- Live command discovery connected to this clone at `127.0.0.1:7800`.
- `editor_status` returned this exact project path, Unity `6000.3.8f1`, `ready`,
  `compiling: false`, and `playMode: stopped` after initial import.
- `recompile_status` returned `compilationFailed: false`.
- Live `run_tests` / `test_status`: **179 total, 151 passed, 28 failed**, no skipped or
  inconclusive tests. Actual results are stored locally in `Logs/cli-editmode-live.json`.
- Failures: 24 `NullReferenceException` and 4 assertion failures in `MilestoneSimulationTests`.
  For example, `UseShortPlanningFixture` dereferences `power`, `habitat`, `safety`, and `launch`
  tasks that are absent from the current `Resources/task-system.json`. This is evidence of
  stale test fixtures; it is not permission to change gameplay to satisfy old tests.
- Baseline comparison: after closing this experiment's Editor, restored the original manifest
  and lock and ran `unity test` without Pipeline. Result: **180 total, 152 passed, 28 failed**.
  The extra passing test is Addressables' `AddressableAssets.DocExampleCode.TestStub.RequiredTest`,
  excluded by the live project-assembly filter. **All 28 failing project test names match exactly**.
  See [failure inventory](UnityCliTestFailures.md); local NUnit output is
  `Logs/cli-editmode-baseline.xml`. Pipeline manifest and lock were restored after comparison.
- `unity close --timeout 20` reported a timeout, but the owned batch Editor subsequently exited
  cleanly. Process exit was verified before baseline launch; no forced termination was needed.

The CLI transport works, but the full-suite acceptance gate is not green. Coplay MCP is retained
as the previous transport. Removing it and deploying package changes remain deferred.
No new runtime script was added, so there is no new runtime/test pairing in this experiment.

## Deployment boundary and rollback

This is a development-tool migration experiment. No APK, GitHub Release, remote content, or
`PatchChannels/dev.json` update is part of it. Under repository rules package changes require a
new verified base APK before deployment. EditMode tests do not prove Android/AOT compatibility.

To abandon the experiment, use the separate original checkout or a fresh `ai-integration` clone.
To roll back this branch, close its Editor after saving and revert the migration implementation
commit, including both manifest and package lock. Reopen and allow UPM to resolve packages.
No global MCP client configuration is changed by this experiment.

## Official references

- [Unity CLI setup](https://docs.unity.com/en-us/unity-cli/use-unity-cli)
- [CLI reference](https://docs.unity.com/en-us/unity-cli/unity-cli-reference)
- [Pipeline setup](https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package)
- [MCP migration scope](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli)

Unity's in-editor AI Assistant MCP deprecation does not apply to Coplay's third-party package.
This branch changes transports because the user requested a CLI evaluation.
