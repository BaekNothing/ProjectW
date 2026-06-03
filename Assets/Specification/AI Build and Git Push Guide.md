# AI Build and Git Push Guide

본 문서는 Project_W에서 다른 AI/LLM 세션이 Android APK 빌드와 Git push를 반복할 때 따라야 하는 운영 가이드다.

상위 판단 순서는 `Project_W – System Index (AI Entry Point).md`를 따른다.

------

## 1. Before You Start

작업 전 항상 현재 브랜치와 작업트리를 확인한다.

```powershell
git status --short --branch
git branch --show-current
```

기본 통합 브랜치는 `ai-integration`이다.
사용자가 다른 브랜치를 명시하지 않았다면 `ai-integration`에 커밋하고 `origin/ai-integration`으로 push한다.

`.git` 쓰기 권한이 제한된 세션에서는 `git add`, `git commit`, `git push`가 실패할 수 있다.
이 경우 우회하지 말고 승인/escalation을 요청해 실행한다.

------

## 2. Sync Specification Metadata

`Assets/Specification`, `Assets/Scripts`, `Assets/Tests`, `Assets/Editor`, `Assets/Resources/CaseReviewData`를 바꿨다면 Architecture 문서 지문을 갱신한다.

```powershell
python tools\sync_architecture_doc.py
python tools\sync_architecture_doc.py --check
```

`--check`가 stale을 보고하면 다시 `sync_architecture_doc.py`를 실행하고 Architecture 문서를 커밋 대상에 포함한다.

주의:

- `tools/sync_architecture_doc.py`는 Git 인덱스 기준으로 지문을 계산한다.
- 새 파일은 `git add` 또는 `git add -N` 이후에 지문에 반영된다.
- Architecture 문서 자체는 지문 계산에서 제외되어야 한다.

------

## 3. Build Android APK

현재 APK 빌더는 `Assets/Editor/ProjectWApkBuilder.cs`의 `ProjectW.Editor.ProjectWApkBuilder.BuildApk`다.

권장 배치 빌드 명령:

```powershell
& 'D:\UnityEditors\6000.3.8f1\Editor\Unity.exe' `
  -batchmode `
  -projectPath 'D:\work\unity\ProjectW' `
  -executeMethod ProjectW.Editor.ProjectWApkBuilder.BuildApk `
  -quit `
  -logFile 'D:\work\unity\ProjectW\Temp\ProjectWApkBuild.log'
```

성공 시 APK는 `APK/ProjectW_YYYYMMDD_N.apk` 형식으로 생성된다.
빌더는 `ProjectSettings/ProjectSettings.asset`의 Android bundle version code를 다음 번호로 갱신한다.

빌드 후 확인:

```powershell
Get-ChildItem -Path APK -Force | Sort-Object LastWriteTime -Descending
git status --short
```

------

## 4. Known Build Pitfalls

Unity Package Manager IPC 실패:

- 증상: 로그에 `Could not connect to IPC stream "Upm-..."` 또는 `Could not establish a connection with the Unity Package Manager local server process`.
- 대응: 샌드박스 안 실행이면 외부 권한/escalation으로 Unity 배치 빌드를 재실행한다.

남은 Unity 임시 파일:

- 실패한 Unity 실행 뒤 `Temp/UnityLockfile` 또는 루트의 `casesensitivetest`가 남을 수 있다.
- 실행 중인 Unity 프로세스가 없는지 확인한 뒤 삭제한다.

```powershell
Get-Process | Where-Object { $_.ProcessName -like '*Unity*' }
Remove-Item -LiteralPath '.\Temp\UnityLockfile', '.\casesensitivetest' -Force -ErrorAction SilentlyContinue
```

Burst debug 폴더:

- APK 빌드 중 `APK/ProjectW_BurstDebugInformation_DoNotShip/`가 생길 수 있다.
- APK가 아니므로 커밋하지 않는다.
- 삭제 전 경로가 `APK` 아래인지 확인하고 삭제한다.

```powershell
$target = Resolve-Path -LiteralPath '.\APK\ProjectW_BurstDebugInformation_DoNotShip'
$root = Resolve-Path -LiteralPath '.\APK'
if (-not $target.Path.StartsWith($root.Path)) { throw "Refusing to remove outside APK folder: $($target.Path)" }
Remove-Item -LiteralPath $target.Path -Recurse -Force
```

------

## 5. What To Commit

커밋에 포함할 수 있는 항목:

- 변경된 SSOT/Specification 문서
- 변경된 코드, 테스트, 에셋
- `ProjectSettings/ProjectSettings.asset`의 Android bundle version code 변경
- 새 APK 파일: `APK/ProjectW_YYYYMMDD_N.apk`
- 필요한 경우 `tools/sync_architecture_doc.py` 변경

커밋하지 않는 항목:

- `Library/`
- `Temp/`
- `APK/ProjectW_BurstDebugInformation_DoNotShip/`
- 루트 `casesensitivetest`
- Unity 실패 로그와 임시 lock 파일

------

## 6. Commit and Push

스테이징 전 변경 범위를 확인한다.

```powershell
git status --short
git diff --stat
```

명시 파일만 스테이징한다.

```powershell
git add <changed-files> APK/ProjectW_YYYYMMDD_N.apk
```

커밋 로그에는 다음을 포함한다.

- SSOT/구현 변경 요약
- APK 파일명
- Android bundle version code
- 실행한 검증 또는 빌드 결과

예시:

```powershell
git commit -m "docs: define long loop and script presentation SSOT" `
  -m "Add long-form core loop and script presentation SSOT. Build and include Android APK ProjectW_20260603_3.apk with bundle version code 3."
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

------

## 7. Report Back

사용자에게는 다음을 짧게 보고한다.

- 커밋 SHA와 제목
- push 대상 브랜치
- APK 파일명과 크기
- 통과한 검증
- 실패했다면 실패 로그의 핵심 원인과 다음 조치
