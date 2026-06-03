# AI Build, Test, and Git Push Guide

본 문서는 Project_W에서 다른 AI/LLM 세션이 Unity 테스트, Android APK 빌드, Git commit/push를 반복할 때 따라야 하는 운영 가이드다.

상위 판단 순서는 `Project_W – System Index (AI Entry Point).md`를 따른다.

------

## 1. Before You Start

작업 전 현재 브랜치와 작업트리를 확인한다.

```powershell
git status --short --branch
git branch --show-current
```

기본 통합 브랜치는 `ai-integration`이다.
사용자가 다른 브랜치를 명시하지 않았다면 `ai-integration`에 커밋하고 `origin/ai-integration`으로 push한다.

`.git` 쓰기 권한이 제한된 세션에서는 `git add`, `git commit`, `git push`가 실패할 수 있다.
이 경우 우회하지 말고 승인/escalation을 요청해서 실행한다.

------

## 2. Unity EditMode Test Runner

Unity 6 batch test runner에서는 `-runTests`와 `-quit`을 같이 쓰면 테스트 완료 전에 프로세스가 종료되거나 결과 XML이 남지 않는 사례가 있었다.

권장 명령:

```powershell
& 'D:\UnityEditors\6000.3.8f1\Editor\Unity.exe' `
  -batchmode `
  -nographics `
  -runTests `
  -testPlatform EditMode `
  -testFilter ProjectW.Tests.EditMode.CaseReviewCoreTests `
  -testResults 'D:\work\unity\ProjectW\Temp\EditModeResults.xml' `
  -projectPath 'D:\work\unity\ProjectW' `
  -logFile 'D:\work\unity\ProjectW\Temp\EditModeTest.log'
```

주의:

- `-runTests` 실행에는 `-quit`을 붙이지 않는다.
- broad run보다 `-testFilter`로 좁혀서 먼저 진입 여부를 확인한다.
- 두 번째 Unity batch 작업을 바로 실행하지 말고 이전 Unity 프로세스가 완전히 종료됐는지 확인한다.
- 성공 로그는 `Test run completed. Exiting with code 0 (Ok). Run completed.`를 포함한다.

결과 확인:

```powershell
Select-String -Path Temp\EditModeTest.log -Pattern "Saving results|Test run completed|No tests|could not be found|Assembly|Failed" -CaseSensitive:$false
Get-Content -Path Temp\EditModeResults.xml -TotalCount 20
```

Unity가 같은 결과를 아래 경로에도 저장하는 경우가 있다.

```powershell
Get-Content -Path "$env:USERPROFILE\AppData\LocalLow\Baeknothing\ProjectW\TestResults.xml" -TotalCount 20
```

------

## 3. Known Test Pitfalls

테스트 결과 XML이 없을 때:

- 먼저 로그에서 `Saving results to:`가 있는지 확인한다.
- `No tests`, `could not be found`, `Assembly` 키워드를 검색한다.
- `ProjectW.Tests.EditMode.asmdef`에 EditMode 테스트 플랫폼이 맞는지 확인한다.
- `Packages/manifest.json` 또는 `packages-lock.json`에 `com.unity.test-framework`가 있는지 확인한다.
- `-quit`을 붙였다면 제거하고 재실행한다.

Unity 프로세스가 남아 있을 때:

```powershell
Get-Process Unity -ErrorAction SilentlyContinue | Select-Object Id,ProcessName,StartTime,CPU
```

이전 batch Unity가 아직 종료 중이면 기다린 뒤 다음 테스트나 빌드를 실행한다.

라이선싱/Package Manager 예외:

- `Unity.Licensing.Client.exe` 예외나 Package Manager IPC 실패가 있어도 일회성일 수 있다.
- 에디터/Unity batch 프로세스를 완전히 종료한 뒤 같은 명령을 한 번 재시도한다.
- 반복되면 로그의 앞부분과 실패 키워드를 먼저 공유한다.

------

## 4. Build Android APK

현재 APK 빌더는 `Assets/Editor/ProjectWApkBuilder.cs`의 `ProjectW.Editor.ProjectWApkBuilder.BuildApk`이다.

권장 배치 빌드 명령:

```powershell
& 'D:\UnityEditors\6000.3.8f1\Editor\Unity.exe' `
  -batchmode `
  -nographics `
  -projectPath 'D:\work\unity\ProjectW' `
  -executeMethod ProjectW.Editor.ProjectWApkBuilder.BuildApk `
  -logFile 'D:\work\unity\ProjectW\Temp\ProjectWApkBuild.log' `
  -quit
```

성공 시 APK는 `APK/ProjectW_YYYYMMDD_N.apk` 형식으로 생성된다.
빌더는 `ProjectSettings/ProjectSettings.asset`의 Android bundle version code를 다음 번호로 갱신한다.

확인:

```powershell
Get-ChildItem -Path APK -Force | Sort-Object LastWriteTime -Descending
Select-String -Path Temp\ProjectWApkBuild.log -Pattern "Build Finished|Result: Success|Copy|Error|Exception" -CaseSensitive:$false
git status --short
```

APK는 사용자가 명시적으로 승인했거나 요청한 경우 정상 commit/push 대상이다.
현재 저장 규칙은 프로젝트명, 날짜, 빌드번호이며 최신 APK 3개만 관리한다.

------

## 5. Known Build Pitfalls

Unity Package Manager IPC 실패:

- 증상: `Could not connect to IPC stream "Upm-..."` 또는 `Could not establish a connection with the Unity Package Manager local server process`.
- 대응: Unity 프로세스가 완전히 종료됐는지 확인하고 같은 batch 명령을 승인/escalation으로 재시도한다.

임시 Unity 파일:

- 실패 후 `Temp/UnityLockfile` 또는 루트 `casesensitivetest`가 남을 수 있다.
- 실행 중인 Unity 프로세스가 없는지 확인한 뒤 제거한다.

```powershell
Get-Process Unity -ErrorAction SilentlyContinue
Remove-Item -LiteralPath '.\Temp\UnityLockfile', '.\casesensitivetest' -Force -ErrorAction SilentlyContinue
```

Burst debug 폴더:

- APK 빌드 중 `APK/ProjectW_BurstDebugInformation_DoNotShip/`가 생길 수 있다.
- APK가 아니므로 commit하지 않는다.
- 삭제 전 경로가 `APK` 아래인지 확인한다.

```powershell
$target = Resolve-Path -LiteralPath '.\APK\ProjectW_BurstDebugInformation_DoNotShip'
$root = Resolve-Path -LiteralPath '.\APK'
if (-not $target.Path.StartsWith($root.Path)) { throw "Refusing to remove outside APK folder: $($target.Path)" }
Remove-Item -LiteralPath $target.Path -Recurse -Force
```

------

## 6. Specification Metadata Sync

`Assets/Specification`, `Assets/Scripts`, `Assets/Tests`, `Assets/Editor`, `Assets/Resources/CaseReviewData`를 바꾸면 Architecture 문서 지문을 갱신한다.

```powershell
python tools\sync_architecture_doc.py
python tools\sync_architecture_doc.py --check
```

`--check`가 stale을 보고하면 다시 `sync_architecture_doc.py`를 실행하고 Architecture 문서를 커밋 대상에 포함한다.

------

## 7. What To Commit

커밋에 포함할 수 있는 항목:

- 변경된 SSOT/Specification 문서
- 변경된 코드, 테스트, 에셋
- `ProjectSettings/ProjectSettings.asset`의 Android bundle version code 변경
- 사용자가 승인한 APK 파일: `APK/ProjectW_YYYYMMDD_N.apk`
- 필요한 경우 `tools/sync_architecture_doc.py` 변경

커밋하지 않는 항목:

- `Library/`
- `Temp/`
- `APK/ProjectW_BurstDebugInformation_DoNotShip/`
- 루트 `casesensitivetest`
- Unity 실패 로그, 임시 lock 파일

------

## 8. Commit and Push

스테이징 전 변경 범위를 확인한다.

```powershell
git status --short
git diff --stat
```

명시 파일만 스테이징한다.

```powershell
git add <changed-files>
```

커밋 메시지는 변경 목적을 짧게 쓴다.
APK를 포함하는 경우 본문에 APK 파일명과 bundle version code를 적는다.

```powershell
git commit -m "Add character mutation interface"
```

push:

```powershell
git push origin ai-integration
```

push 후 확인:

```powershell
git status --short --branch
git log -1 --oneline
```

최종 상태는 `ai-integration...origin/ai-integration`이고 작업트리가 깨끗해야 한다.

금지:

- 사용자의 명시 승인 없는 `main` 병합
- force push
- 히스토리 재작성
- 광범위한 파일 삭제 후 squash/force merge로 정리하는 방식

------

## 9. Report Back

사용자에게는 다음을 짧게 보고한다.

- 커밋 SHA와 제목
- push 대상 브랜치
- 테스트 결과 또는 빌드 결과
- APK를 만들었다면 파일명과 크기
- 실패했다면 핵심 로그와 다음 조치
