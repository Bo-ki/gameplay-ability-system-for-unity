[CmdletBinding()]
param(
    [string]$UnityExe = "",
    [string]$TestFilter = "GAS.Runtime.Tests",
    [string]$TestResultsPath = "",
    [string]$LogFile = "",
    [switch]$IgnoreOpenEditorCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ProjectPath = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")
$ProjectPath = $ProjectPath.Path

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $projectVersionPath = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"
    $versionLine = Select-String -LiteralPath $projectVersionPath -Pattern "^m_EditorVersion:" | Select-Object -First 1
    if ($null -eq $versionLine) {
        throw "Could not read Unity editor version from $projectVersionPath."
    }

    $unityVersion = $versionLine.Line.Split(":", 2)[1].Trim()
    $defaultUnityExe = "C:\Soft\Unity\Editor\$unityVersion\Editor\Unity.exe"
    if (Test-Path -LiteralPath $defaultUnityExe) {
        $UnityExe = $defaultUnityExe
    }
    else {
        $UnityExe = "Unity.exe"
    }
}

if ($UnityExe -ne "Unity.exe") {
    $UnityExe = (Resolve-Path -LiteralPath $UnityExe).Path
}

if ([string]::IsNullOrWhiteSpace($TestResultsPath)) {
    $TestResultsPath = Join-Path $ProjectPath "TestResults\GASRuntimeTests.xml"
}
elseif (-not [System.IO.Path]::IsPathRooted($TestResultsPath)) {
    $TestResultsPath = Join-Path $ProjectPath $TestResultsPath
}

if ([string]::IsNullOrWhiteSpace($LogFile)) {
    $LogFile = Join-Path $ProjectPath "Logs\GASRuntimeTests.log"
}
elseif (-not [System.IO.Path]::IsPathRooted($LogFile)) {
    $LogFile = Join-Path $ProjectPath $LogFile
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $TestResultsPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogFile) | Out-Null

$lockFile = Join-Path $ProjectPath "Temp\UnityLockfile"
if (-not $IgnoreOpenEditorCheck -and (Test-Path -LiteralPath $lockFile)) {
    $unityProcesses = Get-Process -Name Unity -ErrorAction SilentlyContinue
    if ($unityProcesses) {
        $processSummary = ($unityProcesses | ForEach-Object { "PID=$($_.Id) Path=$($_.Path)" }) -join [Environment]::NewLine
        Write-Host @"
Unity appears to have this project open.
Close the Unity Editor for this project before running batchmode tests, or pass -IgnoreOpenEditorCheck if you have confirmed the lock is stale.

Project: $ProjectPath
Lock:    $lockFile
$processSummary
"@
        exit 2
    }
}

$startedAt = Get-Date

Write-Host "Unity:      $UnityExe"
Write-Host "Project:    $ProjectPath"
Write-Host "TestFilter: $TestFilter"
Write-Host "Results:    $TestResultsPath"
Write-Host "LogFile:    $LogFile"

$unityArgs = @(
    "-batchmode",
    "-quit",
    "-projectPath", $ProjectPath,
    "-runTests",
    "-testPlatform", "EditMode",
    "-testFilter", $TestFilter,
    "-testResults", $TestResultsPath,
    "-logFile", $LogFile
)

$unityProcess = Start-Process `
    -FilePath $UnityExe `
    -ArgumentList $unityArgs `
    -WindowStyle Hidden `
    -Wait `
    -PassThru

$exitCode = $unityProcess.ExitCode

if ($exitCode -ne 0) {
    Write-Error "Unity test runner exited with code $exitCode. Log file: $LogFile" -ErrorAction Continue
    if (Test-Path -LiteralPath $LogFile) {
        Write-Host "Last 80 lines from Unity log:"
        Get-Content -LiteralPath $LogFile -Tail 80
    }

    exit $exitCode
}

$resultFile = Get-Item -LiteralPath $TestResultsPath -ErrorAction SilentlyContinue
if ($null -eq $resultFile -or $resultFile.LastWriteTime -lt $startedAt) {
    Write-Error "Unity test runner finished but did not create a fresh result XML: $TestResultsPath" -ErrorAction Continue

    if (Test-Path -LiteralPath $LogFile) {
        Write-Host "Last 80 lines from Unity log:"
        Get-Content -LiteralPath $LogFile -Tail 80
    }

    exit 3
}

Write-Host "GAS runtime tests completed. Result XML: $TestResultsPath"
