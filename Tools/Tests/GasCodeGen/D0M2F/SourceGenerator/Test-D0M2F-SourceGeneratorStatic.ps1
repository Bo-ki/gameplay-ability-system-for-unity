#requires -Version 7.0
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:StaticPrefix = "gas-codegen-d0m2f-sourcegen-static-"
$script:StaticSentinel = ".d0m2f-sourcegen-static-owner"

# 以 UTF-8 无 BOM、CreateNew 语义写入静态检查 sentinel。
function Write-StaticFreshText
{
    param([string]$Path, [string]$Content)
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Content)
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}

# 创建由本静态门精确拥有的 OS 临时根。
function New-StaticRoot
{
    $name = $script:StaticPrefix + [Guid]::NewGuid().ToString("N")
    $root = Join-Path ([IO.Path]::GetTempPath()) $name
    [IO.Directory]::CreateDirectory($root) | Out-Null
    Write-StaticFreshText -Path (Join-Path $root $script:StaticSentinel) -Content ($name + "`n")
    return [IO.Path]::GetFullPath($root).TrimEnd('\')
}

# 复核临时根边界与 reparse 状态后逐叶清理，不对任意外部路径递归删除。
function Remove-StaticRoot
{
    param([string]$Root)
    $resolved = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    $name = [IO.Path]::GetFileName($resolved)
    if ([IO.Path]::GetDirectoryName($resolved) -ne $temp -or $name -notmatch '^gas-codegen-d0m2f-sourcegen-static-[0-9a-f]{32}$') { throw "Unsafe static root: $resolved" }
    $sentinel = Join-Path $resolved $script:StaticSentinel
    if (-not [IO.File]::Exists($sentinel) -or [IO.File]::ReadAllText($sentinel) -ne ($name + "`n")) { throw "Static owner sentinel mismatch." }
    $entries = @(Get-ChildItem -LiteralPath $resolved -Force -Recurse)
    foreach ($entry in $entries) { if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Static root contains reparse point: $($entry.FullName)" } }
    foreach ($file in @($entries | Where-Object { -not $_.PSIsContainer })) { [IO.File]::Delete($file.FullName) }
    foreach ($directory in @($entries | Where-Object { $_.PSIsContainer } | Sort-Object { $_.FullName.Length } -Descending)) { [IO.Directory]::Delete($directory.FullName, $false) }
    [IO.Directory]::Delete($resolved, $false)
}

# 要求 PowerShell 入口通过 PS7 AST，且参数和禁止 -quit 合同保持冻结。
function Test-MainScriptAst
{
    param([string]$MainScript)
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($MainScript, [ref]$tokens, [ref]$errors)
    if ($errors.Count -ne 0) { throw "Main PowerShell AST has $($errors.Count) errors." }
    $parameterNames = @($ast.ParamBlock.Parameters.Name.VariablePath.UserPath)
    foreach ($required in @("UnityPath", "OutputPath", "RunId", "Mode", "KeepFixture")) { if ($required -notin $parameterNames) { throw "Main parameter missing: $required" } }
    $quitLiteral = $ast.FindAll({ param($node) $node -is [Management.Automation.Language.StringConstantExpressionAst] -and $node.Value -eq "-quit" }, $true)
    if (@($quitLiteral).Count -ne 0) { throw "Main Unity arguments must not contain -quit." }
    foreach ($functionName in @("Invoke-BoundedProcess", "Invoke-UnityKillProbe"))
    {
        $definition = Get-MainFunctionDefinition -MainScript $MainScript -FunctionName $functionName
        if (-not $definition.Contains('$process.Dispose()', [StringComparison]::Ordinal) -or
            -not $definition.Contains('$process.WaitForExit()', [StringComparison]::Ordinal) -or
            -not $definition.Contains('Stop-OwnedProcess', [StringComparison]::Ordinal))
        {
            throw "Unity process lifecycle is not deterministically closed: $functionName"
        }
    }
    $stopOwned = Get-MainFunctionDefinition -MainScript $MainScript -FunctionName "Stop-OwnedProcess"
    if (-not $stopOwned.Contains('WaitForExit(30000)', [StringComparison]::Ordinal) -or
        -not $stopOwned.Contains('HasExited', [StringComparison]::Ordinal))
    {
        throw "Owned process termination does not prove bounded exit."
    }
    $reader = Get-MainFunctionDefinition -MainScript $MainScript -FunctionName "Read-UnityInvocation"
    if (-not $reader.Contains("Get-UnityOutputSha256", [StringComparison]::Ordinal)) { throw "Unity log SHA does not use the settlement helper." }
    $errorCatch = @($ast.FindAll({
                param($node)
                $node -is [Management.Automation.Language.CatchClauseAst] -and $node.Extent.Text.Contains('$errorMessage', [StringComparison]::Ordinal)
            }, $true))
    if ($errorCatch.Count -ne 1 -or -not $errorCatch[0].Extent.Text.Contains('^CleanupFailure', [StringComparison]::Ordinal))
    {
        throw "CleanupFailure is not preserved by the typed catch mapping."
    }
}

# 从主入口 AST 提取唯一函数定义，供静态门执行真实实现。
function Get-MainFunctionDefinition
{
    param([string]$MainScript, [string]$FunctionName)
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($MainScript, [ref]$tokens, [ref]$errors)
    if ($errors.Count -ne 0) { throw "Main PowerShell AST has $($errors.Count) errors." }
    $definitions = @($ast.FindAll({
                param($node)
                $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $FunctionName
            }, $true))
    if ($definitions.Count -ne 1) { throw "Main function definition is not unique: $FunctionName" }
    return $definitions[0].Extent.Text
}

# 启动仅由静态门拥有的 pwsh 子进程，以 FileShare.None 模拟 Unity 日志短锁。
function Start-StaticExclusiveLock
{
    param([string]$LockScript, [string]$Path, [string]$ReadyPath, [int]$HoldMilliseconds)
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $PSHOME "pwsh.exe"))
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    foreach ($argument in @("-NoProfile", "-File", $LockScript, "-Path", $Path, "-ReadyPath", $ReadyPath, "-HoldMilliseconds", [string]$HoldMilliseconds))
    {
        $start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
    $started = $false
    try
    {
        $started = $process.Start()
        if (-not $started) { throw "Static lock process failed to start." }
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        while (-not [IO.File]::Exists($ReadyPath))
        {
            if ($process.HasExited) { throw "Static lock process exited before ready." }
            if ([DateTime]::UtcNow -ge $deadline) { throw "Static lock process ready timeout." }
            Start-Sleep -Milliseconds 20
        }
        return $process
    }
    catch
    {
        if ($started -and -not $process.HasExited) { try { $process.Kill($true); $process.WaitForExit(30000) | Out-Null } catch { } }
        $process.Dispose()
        throw
    }
}

# 只终止并释放静态门自己启动的锁进程。
function Stop-StaticExclusiveLock
{
    param([AllowNull()][Diagnostics.Process]$Process)
    if ($null -eq $Process) { return }
    try
    {
        if (-not $Process.HasExited) { $Process.Kill($true); $Process.WaitForExit(30000) | Out-Null }
    }
    finally
    {
        $Process.Dispose()
    }
}

# 真实执行日志 settlement helper，验证短锁成功与持续锁有限失败。
function Test-UnityOutputShaRegression
{
    param([string]$MainScript, [string]$StaticRoot)
    $hashDefinition = Get-MainFunctionDefinition -MainScript $MainScript -FunctionName "Get-FileSha256"
    $settlementDefinition = Get-MainFunctionDefinition -MainScript $MainScript -FunctionName "Get-UnityOutputSha256"
    $lockScript = Join-Path $StaticRoot "Hold-ExclusiveLock.ps1"
    Write-StaticFreshText -Path $lockScript -Content @'
param([string]$Path, [string]$ReadyPath, [int]$HoldMilliseconds)
$stream = [IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try
{
    [IO.File]::WriteAllText($ReadyPath, "ready", [Text.UTF8Encoding]::new($false))
    Start-Sleep -Milliseconds $HoldMilliseconds
}
finally
{
    $stream.Dispose()
}
'@
    $payloadPath = Join-Path $StaticRoot "locked-unity-output.log"
    Write-StaticFreshText -Path $payloadPath -Content "known-unity-log-bytes`n"
    $expectedSha256 = (Get-FileHash -LiteralPath $payloadPath -Algorithm SHA256).Hash.ToLowerInvariant()

    $shortProcess = $null; $shortReady = Join-Path $StaticRoot "short-lock.ready"
    try
    {
        $shortProcess = Start-StaticExclusiveLock -LockScript $lockScript -Path $payloadPath -ReadyPath $shortReady -HoldMilliseconds 500
        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        $actualSha256 = & {
            param($HashDefinition, $SettlementDefinition, $Path)
            function Get-RemainingMilliseconds { return 60000 }
            . ([scriptblock]::Create($HashDefinition)); . ([scriptblock]::Create($SettlementDefinition))
            Get-UnityOutputSha256 -Path $Path -RetryCount 50 -RetryDelayMilliseconds 100
        } $hashDefinition $settlementDefinition $payloadPath
        $stopwatch.Stop()
        if ($actualSha256 -cne $expectedSha256 -or $stopwatch.ElapsedMilliseconds -lt 200 -or $stopwatch.ElapsedMilliseconds -gt 5000)
        {
            throw "Unity output SHA short-lock regression failed."
        }
    }
    finally { Stop-StaticExclusiveLock -Process $shortProcess }

    $longProcess = $null; $longReady = Join-Path $StaticRoot "long-lock.ready"; $failedClosed = $false
    try
    {
        $longProcess = Start-StaticExclusiveLock -LockScript $lockScript -Path $payloadPath -ReadyPath $longReady -HoldMilliseconds 3000
        try
        {
            & {
                param($HashDefinition, $SettlementDefinition, $Path)
                function Get-RemainingMilliseconds { return 60000 }
                . ([scriptblock]::Create($HashDefinition)); . ([scriptblock]::Create($SettlementDefinition))
                Get-UnityOutputSha256 -Path $Path -RetryCount 3 -RetryDelayMilliseconds 100
            } $hashDefinition $settlementDefinition $payloadPath | Out-Null
        }
        catch
        {
            if ($_.Exception.Message -notmatch 'Unity log did not settle') { throw }
            $failedClosed = $true
        }
        if (-not $failedClosed) { throw "Unity output SHA persistent-lock regression did not fail closed." }
    }
    finally { Stop-StaticExclusiveLock -Process $longProcess }
}

# 生成目录树逐文件的相对路径与原始 SHA-256 清单。
function Get-StaticTreeInventory
{
    param([string]$Root)
    return @([IO.Directory]::EnumerateFiles($Root, "*", [IO.SearchOption]::AllDirectories) | ForEach-Object {
            $relative = [IO.Path]::GetRelativePath($Root, $_).Replace("\", "/")
            $sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
            $relative + "`t" + $sha256
        } | Sort-Object)
}

# 从主入口 AST 提取并真实执行 Copy-OrdinaryTree，防止参数括号再次漂移。
function Test-CopyOrdinaryTreeRegression
{
    param([string]$MainScript, [string]$StaticRoot)
    $definition = Get-MainFunctionDefinition -MainScript $MainScript -FunctionName "Copy-OrdinaryTree"
    $source = Join-Path $PSScriptRoot "Fixture~"
    $destination = Join-Path $StaticRoot "CopyRegression"
    & {
        param($Definition, $Source, $Destination)
        # 本回归仅验证真实复制体；fixture 边界由主入口和其它静态检查独立覆盖。
        function Assert-OrdinaryTree { param([string]$Root) }
        . ([scriptblock]::Create($Definition))
        Copy-OrdinaryTree -Source $Source -Destination $Destination
    } $definition $source $destination
    $badSegments = @(Get-ChildItem -LiteralPath $destination -Force -Recurse | Where-Object {
            @([IO.Path]::GetRelativePath($destination, $_.FullName).Split([char[]]@('\', '/'), [StringSplitOptions]::RemoveEmptyEntries) | Where-Object { $_ -in @("False", "True") }).Count -ne 0
        })
    if ($badSegments.Count -ne 0) { throw "Copy-OrdinaryTree emitted a boolean path segment." }
    $sourceInventory = Get-StaticTreeInventory -Root $source
    $destinationInventory = Get-StaticTreeInventory -Root $destination
    if ([string]::Join("`n", $sourceInventory) -cne [string]::Join("`n", $destinationInventory)) { throw "Copy-OrdinaryTree inventory mismatch." }
}

# 要求 fixture 中每个 Unity 资产和子目录具备对应 .meta，且 analyzer 标签精确存在。
function Test-FixtureMetadata
{
    param([string]$FixtureRoot)
    $assets = Join-Path $FixtureRoot "Assets"
    foreach ($directory in [IO.Directory]::EnumerateDirectories($assets, "*", [IO.SearchOption]::AllDirectories))
    {
        if (-not [IO.File]::Exists($directory + ".meta")) { throw "Fixture directory meta missing: $directory" }
    }
    foreach ($file in [IO.Directory]::EnumerateFiles($assets, "*", [IO.SearchOption]::AllDirectories))
    {
        if (-not $file.EndsWith(".meta", [StringComparison]::OrdinalIgnoreCase) -and -not [IO.File]::Exists($file + ".meta")) { throw "Fixture asset meta missing: $file" }
    }
    $analyzerMeta = [IO.File]::ReadAllText((Join-Path $assets "Analyzers/D0M2FCanaryGenerator.dll.meta"))
    if (-not $analyzerMeta.Contains("- RoslynAnalyzer", [StringComparison]::Ordinal)) { throw "RoslynAnalyzer label missing." }
}

# 以 C# 9/netstandard2.0 离线编译 canary generator，并返回 DLL 路径。
function Build-GeneratorStatic
{
    param([string]$StaticRoot)
    $project = Join-Path $StaticRoot "D0M2FCanaryGenerator.csproj"
    [IO.File]::Copy((Join-Path $PSScriptRoot "Generator~/D0M2FCanaryGenerator.csproj.template"), $project, $false)
    [IO.File]::Copy((Join-Path $PSScriptRoot "Generator~/D0M2FCanaryGenerator.cs"), (Join-Path $StaticRoot "D0M2FCanaryGenerator.cs"), $false)
    $output = Join-Path $StaticRoot "generator-out"; $intermediate = Join-Path $StaticRoot "generator-obj/"
    & dotnet build $project --configuration Release --output $output --nologo "/p:BaseIntermediateOutputPath=$intermediate" "/p:RestoreIgnoreFailedSources=true"
    if ($LASTEXITCODE -ne 0) { throw "Canary generator static build failed." }
    $dll = Join-Path $output "D0M2FCanaryGenerator.dll"
    if (-not [IO.File]::Exists($dll) -or [Reflection.AssemblyName]::GetAssemblyName($dll).Name -ne "D0M2FCanaryGenerator") { throw "Canary generator assembly identity mismatch." }
    return $dll
}

# 使用 Unity 6000.3.14f1 托管引用离线编译 Editor probe，不启动 Unity 进程。
function Build-UnityProbeStatic
{
    param([string]$StaticRoot)
    $registry = "HKLM:\SOFTWARE\Unity Technologies\Installer\Unity 6000.3.14f1"
    $location = (Get-ItemProperty -LiteralPath $registry -Name "Location x64")."Location x64"
    $data = Join-Path $location "Editor/Data"; $csc = Join-Path $data "DotNetSdkRoslyn/csc.dll"
    $references = @(Get-ChildItem -LiteralPath (Join-Path $data "NetStandard/ref/2.1.0") -Filter "*.dll" -File | ForEach-Object { "/reference:" + $_.FullName })
    $references += @("Managed/UnityEngine/UnityEngine.dll", "Managed/UnityEngine/UnityEngine.CoreModule.dll", "Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll", "Managed/UnityEngine/UnityEditor.CoreModule.dll") | ForEach-Object { "/reference:" + (Join-Path $data $_) }
    $output = Join-Path $StaticRoot "D0M2FSourceGeneratorUnityProbe.dll"
    $source = Join-Path $PSScriptRoot "Fixture~/Assets/Editor/D0M2FSourceGeneratorUnityProbe.cs"
    & dotnet $csc /nologo /noconfig /nostdlib+ /target:library /langversion:9.0 "/out:$output" @references $source
    if ($LASTEXITCODE -ne 0 -or -not [IO.File]::Exists($output)) { throw "Unity probe static compile failed." }
}

$staticRoot = New-StaticRoot
try
{
    $mainScript = Join-Path $PSScriptRoot "Run-D0M2F-SourceGeneratorProbe.ps1"
    Test-MainScriptAst -MainScript $mainScript
    Test-FixtureMetadata -FixtureRoot (Join-Path $PSScriptRoot "Fixture~")
    Build-GeneratorStatic -StaticRoot $staticRoot | Out-Null
    Build-UnityProbeStatic -StaticRoot $staticRoot
    Test-CopyOrdinaryTreeRegression -MainScript $mainScript -StaticRoot $staticRoot
    Test-UnityOutputShaRegression -MainScript $mainScript -StaticRoot $staticRoot
    Write-Output "D0-M2F SourceGenerator static validation passed."
}
finally
{
    Remove-StaticRoot -Root $staticRoot
}
