param([switch]$Impaired)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$logsPath = Join-Path $projectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logsPath | Out-Null
    $executable = Join-Path $projectPath 'Builds\Multiplayer-NGO\DCG-Multiplayer.exe'
    $label = if ($Impaired) { 'impaired' } else { 'smoke' }
    $hostLog = Join-Path $logsPath ('duel-host-' + $label + '.log')
    $guestLog = Join-Path $logsPath ('duel-guest-' + $label + '.log')
    $hostProcess = $null
    $guestProcess = $null
    $proxyProcess = $null
    $port = if ($Impaired) { 17778 } else { 17777 }
    try {
        if ($Impaired) {
            $proxyArgs = '"{0}" --seconds 120' -f (Join-Path $PSScriptRoot "Simulate-DuelNetwork.py")
            $proxyProcess = Start-Process python -ArgumentList $proxyArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logsPath "duel-proxy.log")
        }
        $hostProcess = Start-Process -FilePath $executable -ArgumentList ('-batchmode -nographics -dcgDuelSmoke host -logFile "{0}"' -f $hostLog) -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 2
        $guestProcess = Start-Process -FilePath $executable -ArgumentList ('-batchmode -nographics -dcgDuelSmoke guest -dcgDuelPort {1} -logFile "{0}"' -f $guestLog, $port) -WindowStyle Hidden -PassThru
        if (-not $hostProcess.WaitForExit(110000)) { throw 'Host integration check timed out.' }
        if (-not $guestProcess.WaitForExit(10000)) { throw 'Guest integration check timed out.' }
        foreach ($path in @($hostLog, $guestLog)) {
            $text = Get-Content -LiteralPath $path -Raw
            if ($text -notmatch 'DCG_DUEL_SMOKE_OK' -or $text -match 'DCG_DUEL_SMOKE_FAILED|Exception:') { throw "Integration check failed: $path" }
        }
        if ($hostProcess.ExitCode -ne 0 -or $guestProcess.ExitCode -ne 0) { throw 'A smoke process returned failure.' }
        $hostResults = Select-String -LiteralPath $hostLog -Pattern 'DCG_DUEL_SMOKE_RESULT round=(\d+) map=(\d+) winner=(-?\d+)'
        $guestResults = Select-String -LiteralPath $guestLog -Pattern 'DCG_DUEL_SMOKE_RESULT round=(\d+) map=(\d+) winner=(-?\d+)'
        if ($hostResults.Count -ne 3 -or $guestResults.Count -ne 3) { throw 'Three results were not produced on both clients.' }
        for ($index = 0; $index -lt 3; $index++) {
            if ($hostResults[$index].Matches[0].Value -ne $guestResults[$index].Matches[0].Value) { throw 'Client maps or results disagree.' }
        }
        Write-Output 'Three NGO matches including quick start passed; both maps and results agree.'
    }
    finally {
        foreach ($process in @($hostProcess, $guestProcess, $proxyProcess)) {
            if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
        }
    }
