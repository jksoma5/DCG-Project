param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe',
    [switch]$Build,
    [switch]$Smoke,
    [switch]$Impaired
)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$logsPath = Join-Path $projectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logsPath | Out-Null
function Invoke-DuelUnity([string]$Label, [string]$Extra) {
    $logFile = Join-Path $logsPath ("duel-" + $Label + ".log")
    $arguments = '-batchmode -nographics -projectPath "{0}" -logFile "{1}" {2}' -f $projectPath, $logFile, $Extra
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "$Label failed ($($process.ExitCode)). See $logFile" }
}
Invoke-DuelUnity 'setup' '-quit -executeMethod DCG.Editor.MultiplayerSetup.Generate'
foreach ($mode in @('EditMode', 'PlayMode')) {
    $report = Join-Path $logsPath ("duel-" + $mode + ".xml")
    Invoke-DuelUnity $mode ('-runTests -testPlatform {0} -testResults "{1}"' -f $mode, $report)
    [xml]$results = Get-Content -LiteralPath $report -Raw
    if ($results.'test-run'.result -ne 'Passed' -or [int]$results.'test-run'.total -eq 0) { throw "$mode failed: $report" }
    Write-Output "$mode passed: $($results.'test-run'.passed)"
}
if ($Build -or $Smoke) { Invoke-DuelUnity 'build' '-quit -executeMethod DCG.Editor.MultiplayerSetup.Build' }
if ($Smoke) {
    & (Join-Path $PSScriptRoot "Verify-DuelNetwork.ps1") -Impaired:$Impaired
}
