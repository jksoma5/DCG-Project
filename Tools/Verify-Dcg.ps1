param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe',
    [switch]$Regenerate,
    [switch]$Build,
    [switch]$BuildVendetta,
    [switch]$BuildRifle,
    [switch]$BuildSniper,
    [switch]$BuildPaul
)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$logPath = Join-Path $projectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity editor not found: $UnityPath" }
function Invoke-DcgUnity([string]$Label, [string]$Extra) {
    $logFile = Join-Path $logPath ("dcg-" + $Label + ".log")
    $arguments = '-batchmode -nographics -projectPath "{0}" -logFile "{1}" {2}' -f $projectPath, $logFile, $Extra
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "$Label failed ($($process.ExitCode)). See $logFile" }
}
if ($Regenerate) {
    # Replaces only the generated DCG lab scenes/prefab; existing sample assets are preserved.
    Invoke-DcgUnity 'setup' '-quit -executeMethod DCG.Editor.ProjectSetup.Generate'
    Invoke-DcgUnity 'vendetta-setup' '-quit -executeMethod DCG.Editor.VendettaLabSetup.Generate'
    Invoke-DcgUnity 'rifle-setup' '-quit -executeMethod DCG.Editor.RifleLabSetup.Generate'
    Invoke-DcgUnity 'sniper-setup' '-quit -executeMethod DCG.Editor.SniperLabSetup.Generate'
    Invoke-DcgUnity 'paul-setup' '-quit -executeMethod DCG.Editor.PaulLabSetup.Generate'
}
foreach ($mode in @('EditMode', 'PlayMode')) {
    $report = Join-Path $logPath ("dcg-" + $mode.ToLowerInvariant() + ".xml")
    Invoke-DcgUnity $mode.ToLowerInvariant() ('-runTests -testPlatform {0} -testResults "{1}"' -f $mode, $report)
    [xml]$results = Get-Content -LiteralPath $report -Raw
    if ($results.'test-run'.result -ne 'Passed' -or [int]$results.'test-run'.total -eq 0) {
        throw "$mode returned no successful test suite. See $report"
    }
    Write-Host "$mode passed: $($results.'test-run'.passed)"
}
if ($Build) { Invoke-DcgUnity 'build' '-quit -executeMethod DCG.Editor.ProjectSetup.Build' }

if ($BuildVendetta) { Invoke-DcgUnity 'vendetta-build' '-quit -executeMethod DCG.Editor.VendettaLabSetup.Build' }

if ($BuildRifle) { Invoke-DcgUnity 'rifle-build' '-quit -executeMethod DCG.Editor.RifleLabSetup.Build' }

if ($BuildSniper) { Invoke-DcgUnity 'sniper-build' '-quit -executeMethod DCG.Editor.SniperLabSetup.Build' }

if ($BuildPaul) { Invoke-DcgUnity 'paul-build' '-quit -executeMethod DCG.Editor.PaulLabSetup.Build' }
