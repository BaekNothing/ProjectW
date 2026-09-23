[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$unityCli = Join-Path $env:LOCALAPPDATA 'Unity\bin\unity.exe'
if (-not (Test-Path -LiteralPath $unityCli)) {
    $unityCli = (Get-Command unity -ErrorAction Stop).Source
}
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'Logs') -Force | Out-Null
& $unityCli run $projectRoot --non-interactive --timeout 3600 -- -buildTarget WebGL -executeMethod ProjectW.MilestonePrototype.Editor.WebPreviewBuilder.Build -logFile (Join-Path $projectRoot 'Logs\WebPreviewBuild.log')
if ($LASTEXITCODE -ne 0) { throw "WebGL build failed ($LASTEXITCODE). See Logs/WebPreviewBuild.log." }
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'Builds\WebGL\index.html'))) {
    throw 'Build did not produce index.html.'
}
$indexPath = Join-Path $projectRoot 'Builds\WebGL\index.html'
$html = [IO.File]::ReadAllText($indexPath)
$responsiveStyle = @'
<style id="projectw-responsive">
html,body { margin:0; width:100%; height:100%; overflow:hidden; background:#191f29; }
#unity-container.unity-desktop { position:absolute; inset:0; width:100%; height:100%; transform:none; }
#unity-canvas { width:100% !important; height:calc(100% - 38px) !important; display:block; touch-action:none; }
#unity-footer { width:100%; height:38px; background:#f5f5f5; }
#unity-canvas:fullscreen { height:100% !important; }
</style>
'@
if (-not $html.Contains('id="projectw-responsive"')) {
    [IO.File]::WriteAllText($indexPath, $html.Replace('</head>', $responsiveStyle + '</head>'))
}
Write-Host 'Web build ready in Builds/WebGL. Upload with butler push, then confirm with butler status.'
