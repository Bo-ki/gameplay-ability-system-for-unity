@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..\..") do set "PROJECT_ROOT=%%~fI"
set "PROJECT_VERSION_FILE=%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt"
set "LOG_FILE=%PROJECT_ROOT%\Logs\GASCodeGenBatch.log"

if "%~1"=="" (
    for /f "tokens=2 delims=:" %%V in ('findstr /b /c:"m_EditorVersion:" "%PROJECT_VERSION_FILE%"') do set "UNITY_VERSION=%%V"
    set "UNITY_VERSION=!UNITY_VERSION: =!"
    set "UNITY_EXE=E:\Unity\UnityEditor\!UNITY_VERSION!\Editor\Unity.exe"
    if not exist "!UNITY_EXE!" set "UNITY_EXE=Unity.exe"
) else (
    set "UNITY_EXE=%~1"
)

if not exist "%PROJECT_ROOT%\Logs" mkdir "%PROJECT_ROOT%\Logs"

echo Unity:  !UNITY_EXE!
echo Project: %PROJECT_ROOT%
echo Log:     %LOG_FILE%

"!UNITY_EXE!" -batchmode -quit -projectPath "%PROJECT_ROOT%" -executeMethod GAS.Editor.GasCodeGenBatchRunner.GenerateAllAndExit -logFile "%LOG_FILE%"
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
    echo GAS CodeGen failed with exit code %EXIT_CODE%.
    if exist "%LOG_FILE%" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Content -LiteralPath '%LOG_FILE%' -Tail 120"
    )
    exit /b %EXIT_CODE%
)

echo GAS CodeGen completed.
exit /b 0
