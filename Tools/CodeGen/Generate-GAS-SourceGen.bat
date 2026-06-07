@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..\..") do set "PROJECT_ROOT=%%~fI"
set "JSON_OUT=%PROJECT_ROOT%\Assets\DataGenerated\Luban\Json\GAS"
set "CODE_OUT=%PROJECT_ROOT%\Assets\DataGenerated\Luban\CSharp"
set "CONFIG_ROOT=%PROJECT_ROOT%\EX_GAS_Config\ProjectConfigTable\exgas_config"
set "LUBAN_DLL=%PROJECT_ROOT%\EX_GAS_Config\ProjectConfigTable\Tools\Luban\Luban.dll"

if not exist "%LUBAN_DLL%" (
    echo Luban.dll not found: %LUBAN_DLL% 1>&2
    exit /b 2
)

if not exist "%JSON_OUT%" mkdir "%JSON_OUT%"
if not exist "%CODE_OUT%" mkdir "%CODE_OUT%"

echo Project: %PROJECT_ROOT%
echo Luban JSON: %JSON_OUT%
echo Luban C#:   %CODE_OUT%

pushd "%CONFIG_ROOT%" || exit /b 2
dotnet "%LUBAN_DLL%" ^
    -t client ^
    -c cs-simple-json ^
    -d json ^
    --conf "%CONFIG_ROOT%\luban.conf" ^
    -x outputCodeDir="%CODE_OUT%" ^
    -x outputDataDir="%JSON_OUT%"
set "LUBAN_EXIT=%ERRORLEVEL%"
popd

if not "%LUBAN_EXIT%"=="0" (
    echo Luban generation failed: %LUBAN_EXIT% 1>&2
    exit /b %LUBAN_EXIT%
)

dotnet run --project "%PROJECT_ROOT%\Tools\GasCodeGenCli\GasCodeGenCli.csproj" -- --projectRoot "%PROJECT_ROOT%" --mode sourcegen-all
exit /b %ERRORLEVEL%
