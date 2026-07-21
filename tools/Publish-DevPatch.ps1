[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d{8}-\d{3}$')]
    [string]$Version,

    [string]$Token = $env:GITHUB_TOKEN,

    [string]$UnityPath = "D:\UnityEditors\6000.3.8f1\Editor\Unity.exe",

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$tag = "dev-$Version"
$patchDirectory = Join-Path $projectRoot "PatchBuild\$tag"

if (-not $SkipBuild) {
    $env:PROJECTW_PATCH_VERSION = $Version.ToString()
    $logPath = Join-Path $projectRoot "Logs\PatchBuild-$tag.log"
    $arguments = @(
        "-batchmode",
        "-projectPath", $projectRoot,
        "-executeMethod", "ProjectW.MilestonePrototype.Editor.HybridClrPocBuilder.BuildPatchFromCommandLine",
        "-quit",
        "-logFile", $logPath
    )
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -ne 0) {
        Get-Content $logPath -Tail 120
        throw "Unity patch build failed with exit code $($process.ExitCode)."
    }
}

if (-not (Test-Path -LiteralPath $patchDirectory)) {
    throw "Patch directory does not exist: $patchDirectory"
}
if ([string]::IsNullOrWhiteSpace($Token)) {
    throw "Set GITHUB_TOKEN to a fine-grained token with Contents: write for BaekNothing/ProjectW."
}

$headers = @{
    Authorization = "Bearer $Token"
    Accept = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2026-03-10"
}
$releaseNotes = [System.IO.File]::ReadAllText((Join-Path $patchDirectory "release-notes.md"))
$releaseBody = @{
    tag_name = $tag
    target_commitish = (git -C $projectRoot branch --show-current).Trim()
    name = "ProjectW development patch $tag"
    body = $releaseNotes
    draft = $false
    prerelease = $true
} | ConvertTo-Json

$release = Invoke-RestMethod -Method Post `
    -Uri "https://api.github.com/repos/BaekNothing/ProjectW/releases" `
    -Headers $headers -ContentType "application/json" -Body $releaseBody

foreach ($file in Get-ChildItem -LiteralPath $patchDirectory -File | Where-Object Name -ne "release-notes.md") {
    $escapedName = [Uri]::EscapeDataString($file.Name)
    $uploadUrl = "https://uploads.github.com/repos/BaekNothing/ProjectW/releases/$($release.id)/assets?name=$escapedName"
    Invoke-RestMethod -Method Post -Uri $uploadUrl -Headers $headers `
        -ContentType "application/octet-stream" -InFile $file.FullName | Out-Null
}

$channel = @{
    schemaVersion = 1
    manifestUrl = "https://github.com/BaekNothing/ProjectW/releases/download/$tag/patch-manifest.json"
} | ConvertTo-Json
Set-Content -LiteralPath (Join-Path $projectRoot "PatchChannels\dev.json") -Value $channel -Encoding utf8

Write-Host "Published $tag. Commit and push PatchChannels/dev.json to activate it for devices."
