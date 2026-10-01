#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [Parameter(Mandatory = $true)][ValidatePattern('^D0M2F-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$')][string]$RunId,
    [ValidateSet("Feasibility", "SelectorConflict")][string]$Mode = "Feasibility",
    [switch]$KeepFixture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:Schema = "D0M2F-ProbeResult-v1"
$script:ProbeId = if ($Mode -eq "SelectorConflict") { "X" } else { "S" }
$script:SuitePrefix = "gas-codegen-d0-m2f-sourcegen-"
$script:OwnerSentinel = ".d0m2f-sourcegen-owner"
$script:SelectorRelativePath = "Assets/Selector/Generation.D0M2FCanaryGenerator.additionalfile"
$script:GeneratorAssemblyName = "D0M2FCanaryGenerator"
$script:TargetAssemblies = @(
    "com.exhard.exgas.generated.runtime",
    "com.exhard.exgas.generated.editor",
    "com.exhard.exgas.autochessdemo"
)
$script:DeadlineUtc = [DateTime]::UtcNow.AddMinutes($(if ($Mode -eq "SelectorConflict") { 60 } else { 120 }))

if (-not ("D0M2FNativeFileInfo" -as [type]))
{
    Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

/// <summary>读取 D0-M2F fixture 文件的 hardlink 数量。</summary>
public static class D0M2FNativeFileInfo
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    /// <summary>返回已打开普通文件的链接计数，查询失败时显式抛错。</summary>
    public static uint GetLinkCount(SafeFileHandle file)
    {
        ByHandleFileInformation information;
        if (!GetFileInformationByHandle(file, out information))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return information.NumberOfLinks;
    }
}
"@
}

# 规范化路径并移除尾部分隔符。
function Resolve-NormalizedPath
{
    param([Parameter(Mandatory = $true)][string]$Path)
    return [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
}

# 计算普通文件的 SHA-256。
function Get-FileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# 等待 Unity 退出尾声释放日志锁，并在有限次数内返回精确原始 SHA-256。
function Get-UnityOutputSha256
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [ValidateRange(1, 50)][int]$RetryCount = 50,
        [ValidateRange(1, 1000)][int]$RetryDelayMilliseconds = 100
    )

    for ($attempt = 1; $attempt -le $RetryCount; $attempt++)
    {
        try
        {
            return Get-FileSha256 -Path $Path
        }
        catch
        {
            $cause = $_.Exception
            while ($null -ne $cause.InnerException) { $cause = $cause.InnerException }
            $retryable = $cause -is [IO.IOException] -or $cause -is [UnauthorizedAccessException]
            if (-not $retryable) { throw }
            if ($attempt -eq $RetryCount)
            {
                throw [IO.IOException]::new("HarnessFailure: Unity log did not settle: $Path", $cause)
            }
            Get-RemainingMilliseconds | Out-Null
            Start-Sleep -Milliseconds $RetryDelayMilliseconds
        }
    }
}

# 以 UTF-8 无 BOM、LF 和 CreateNew 语义写出文件。
function Write-FreshText
{
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][string]$Content)
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Content.Replace("`r`n", "`n").Replace("`r", "`n"))
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}

# 返回 suite 剩余硬时间盒毫秒数。
function Get-RemainingMilliseconds
{
    $remaining = [int64]($script:DeadlineUtc - [DateTime]::UtcNow).TotalMilliseconds
    if ($remaining -le 0) { throw [TimeoutException]::new("D0-M2F SourceGenerator time-box exceeded.") }
    return [int][Math]::Min($remaining, [int]::MaxValue)
}

# 验证目录树不含 reparse point，普通文件均只有一个 hardlink。
function Assert-OrdinaryTree
{
    param([Parameter(Mandatory = $true)][string]$Root)
    foreach ($entry in @(Get-ChildItem -LiteralPath $Root -Force -Recurse))
    {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "FixtureBoundaryViolation: reparse point $($entry.FullName)" }
        if (-not $entry.PSIsContainer)
        {
            $stream = [IO.File]::Open($entry.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
            try { if ([D0M2FNativeFileInfo]::GetLinkCount($stream.SafeFileHandle) -ne 1) { throw "FixtureBoundaryViolation: hardlink $($entry.FullName)" } } finally { $stream.Dispose() }
        }
    }
}

# 深拷贝普通目录树，拒绝任何 link/reparse 继承。
function Copy-OrdinaryTree
{
    param([Parameter(Mandatory = $true)][string]$Source, [Parameter(Mandatory = $true)][string]$Destination)
    Assert-OrdinaryTree -Root $Source
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($directory in [IO.Directory]::EnumerateDirectories($Source, "*", [IO.SearchOption]::AllDirectories))
    {
        [IO.Directory]::CreateDirectory((Join-Path $Destination ([IO.Path]::GetRelativePath($Source, $directory)))) | Out-Null
    }
    foreach ($file in [IO.Directory]::EnumerateFiles($Source, "*", [IO.SearchOption]::AllDirectories))
    {
        [IO.File]::Copy($file, (Join-Path $Destination ([IO.Path]::GetRelativePath($Source, $file))), $false)
    }
    Assert-OrdinaryTree -Root $Destination
}

# 创建带唯一 owner sentinel 的 OS 临时 suite 根。
function New-SuiteRoot
{
    $name = $script:SuitePrefix + [Guid]::NewGuid().ToString("N")
    $root = Join-Path ([IO.Path]::GetTempPath()) $name
    if ([IO.File]::Exists($root) -or [IO.Directory]::Exists($root)) { throw "FixtureBoundaryViolation: fixture root already exists." }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    Write-FreshText -Path (Join-Path $root $script:OwnerSentinel) -Content ($RunId + "`n")
    return Resolve-NormalizedPath -Path $root
}

# 验证待清理根仍是本次 suite 精确拥有的普通临时目录。
function Assert-OwnedSuiteRoot
{
    param([Parameter(Mandatory = $true)][string]$Root)
    $normalized = Resolve-NormalizedPath -Path $Root
    $temp = Resolve-NormalizedPath -Path ([IO.Path]::GetTempPath())
    if ((Resolve-NormalizedPath -Path ([IO.Path]::GetDirectoryName($normalized))) -ne $temp) { throw "FixtureBoundaryViolation: root escaped temp." }
    if ([IO.Path]::GetFileName($normalized) -notmatch '^gas-codegen-d0-m2f-sourcegen-[0-9a-f]{32}$') { throw "FixtureBoundaryViolation: root name invalid." }
    $sentinel = Join-Path $normalized $script:OwnerSentinel
    if (-not [IO.File]::Exists($sentinel) -or [IO.File]::ReadAllText($sentinel) -ne ($RunId + "`n")) { throw "FixtureBoundaryViolation: owner sentinel invalid." }
    Assert-OrdinaryTree -Root $normalized
}

# 仅在完整边界复核后递归删除本次 suite。
function Remove-SuiteRoot
{
    param([Parameter(Mandatory = $true)][string]$Root)
    Assert-OwnedSuiteRoot -Root $Root
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -Force -Recurse -File)) { [IO.File]::Delete($file.FullName) }
    foreach ($directory in @(Get-ChildItem -LiteralPath $Root -Force -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending)) { [IO.Directory]::Delete($directory.FullName, $false) }
    [IO.Directory]::Delete($Root, $false)
}

# 只终止本 harness 启动的进程树，并要求其在 30 秒内真实退出。
function Stop-OwnedProcess
{
    param(
        [Parameter(Mandatory = $true)][Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][string]$FailureMessage
    )

    if ($Process.HasExited) { $Process.WaitForExit(); return }
    try
    {
        $Process.Kill($true)
    }
    catch
    {
        throw [InvalidOperationException]::new($FailureMessage, $_.Exception)
    }
    if (-not $Process.WaitForExit(30000) -or -not $Process.HasExited)
    {
        throw $FailureMessage
    }
    $Process.WaitForExit()
}

# 以 ArgumentList 和受控超时执行子进程，并只终止本 harness 启动的进程树。
function Invoke-BoundedProcess
{
    param([Parameter(Mandatory = $true)][string]$FilePath, [Parameter(Mandatory = $true)][string[]]$Arguments, [string]$WorkingDirectory = "")
    $start = [Diagnostics.ProcessStartInfo]::new($FilePath)
    $start.UseShellExecute = $false; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true; $start.CreateNoWindow = $true
    if ($WorkingDirectory) { $start.WorkingDirectory = $WorkingDirectory }
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
    $started = $false; $stdout = $null; $stderr = $null
    try
    {
        $started = $process.Start()
        if (-not $started) { throw "HarnessFailure: process failed to start: $FilePath" }
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit((Get-RemainingMilliseconds)))
        {
            Stop-OwnedProcess -Process $process -FailureMessage "CleanupFailure: owned child process tree survived timeout cleanup."
            throw [TimeoutException]::new("D0-M2F child process exceeded the suite time-box.")
        }
        $process.WaitForExit()
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Stdout = $stdout.GetAwaiter().GetResult(); Stderr = $stderr.GetAwaiter().GetResult() }
    }
    finally
    {
        try
        {
            if ($started -and -not $process.HasExited)
            {
                Stop-OwnedProcess -Process $process -FailureMessage "CleanupFailure: owned child process tree survived exception cleanup."
            }
            if ($started -and $process.HasExited)
            {
                if ($null -ne $stdout) { $stdout.GetAwaiter().GetResult() | Out-Null }
                if ($null -ne $stderr) { $stderr.GetAwaiter().GetResult() | Out-Null }
            }
        }
        finally
        {
            $process.Dispose()
        }
    }
}

# 编译 netstandard2.0 canary generator 并复制到隔离 fixture 的 analyzer 路径。
function Install-CanaryGenerator
{
    param([Parameter(Mandatory = $true)][string]$SuiteRoot, [Parameter(Mandatory = $true)][string]$ProjectRoot)
    $source = Join-Path $PSScriptRoot "Generator~"
    $buildRoot = Join-Path $SuiteRoot "GeneratorBuild"
    $outputRoot = Join-Path $SuiteRoot "GeneratorOutput"
    Copy-OrdinaryTree -Source $source -Destination $buildRoot
    [IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    $project = Join-Path $buildRoot "D0M2FCanaryGenerator.csproj"
    [IO.File]::Move((Join-Path $buildRoot "D0M2FCanaryGenerator.csproj.template"), $project)
    $build = Invoke-BoundedProcess -FilePath "dotnet" -WorkingDirectory $buildRoot -Arguments @("build", $project, "--configuration", "Release", "--output", $outputRoot, "--nologo", "/p:RestoreIgnoreFailedSources=true")
    if ($build.ExitCode -ne 0) { throw "HarnessFailure: generator build failed.`n$($build.Stdout)`n$($build.Stderr)" }
    $dll = Join-Path $outputRoot ($script:GeneratorAssemblyName + ".dll")
    if (-not [IO.File]::Exists($dll)) { throw "HarnessFailure: generator DLL missing." }
    $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($dll).Name
    if ($assemblyName -ne $script:GeneratorAssemblyName) { throw "HarnessFailure: generator assembly name mismatch." }
    $target = Join-Path $ProjectRoot "Assets/Analyzers/D0M2FCanaryGenerator.dll"
    [IO.File]::Copy($dll, $target, $false)
    return [pscustomobject]@{ SourceSha256 = Get-FileSha256 (Join-Path $source "D0M2FCanaryGenerator.cs"); AssemblySha256 = Get-FileSha256 $target }
}

# 创建独立 Unity 工程、空 manifest 与已编译 analyzer，不复制任何 Library/Bee。
function New-Fixture
{
    param([Parameter(Mandatory = $true)][string]$SuiteRoot)
    $project = Join-Path $SuiteRoot "Project"
    Copy-OrdinaryTree -Source (Join-Path $PSScriptRoot "Fixture~") -Destination $project
    [IO.Directory]::CreateDirectory((Join-Path $project "Packages")) | Out-Null
    Write-FreshText -Path (Join-Path $project "Packages/manifest.json") -Content "{`n  `"dependencies`": {}`n}`n"
    $generator = Install-CanaryGenerator -SuiteRoot $SuiteRoot -ProjectRoot $project
    if ([IO.Directory]::Exists((Join-Path $project "Library"))) { throw "FixtureBoundaryViolation: fixture inherited a Library." }
    Assert-OrdinaryTree -Root $project
    return [pscustomobject]@{ ProjectRoot = $project; Generator = $generator }
}

# 原子替换 fixture 内唯一 semantic selector 的 generation 值。
function Set-SemanticSelector
{
    param([Parameter(Mandatory = $true)][string]$ProjectRoot, [Parameter(Mandatory = $true)][string]$Generation)
    $target = Join-Path $ProjectRoot $script:SelectorRelativePath
    $next = $target + ".next"; $backup = $target + ".backup"
    if ([IO.File]::Exists($next) -or [IO.File]::Exists($backup)) { throw "HarnessFailure: selector transaction residue exists." }
    Write-FreshText -Path $next -Content ($Generation + "`n")
    [IO.File]::Replace($next, $target, $backup, $true); [IO.File]::Delete($backup)
    return Get-FileSha256 $target
}

# 在 fixture ProjectSettings 中构造与 additional-file 冲突的 audit ref，不接入任何启动钩子。
function New-ConflictingActiveRef
{
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    $directory = Join-Path $ProjectRoot "ProjectSettings/GasCodeGen"; [IO.Directory]::CreateDirectory($directory) | Out-Null
    $path = Join-Path $directory "ActiveGenerationRef.json"
    Write-FreshText -Path $path -Content "{`n  `"GenerationId`": `"generation-a`",`n  `"Role`": `"audit-probe`"`n}`n"
    return [pscustomobject]@{ Path = $path; Sha256 = Get-FileSha256 $path }
}

# 从 fixture 自己的 Bee 图读取目标程序集 RSP、analyzer 与 additional-file 参数。
function Get-BeeEvidence
{
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    $bee = Join-Path $ProjectRoot "Library/Bee"; $items = [Collections.Generic.List[object]]::new()
    foreach ($assemblyName in $script:TargetAssemblies)
    {
        $candidates = @(Get-ChildItem -LiteralPath $bee -Recurse -Filter ($assemblyName + ".rsp") -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending)
        $selected = $candidates | Where-Object { [IO.File]::ReadAllText($_.FullName).Contains("D0M2FCanaryGenerator.dll", [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
        if ($null -eq $selected) { $items.Add([pscustomobject]@{ Assembly = $assemblyName; Found = $false; RspPath = ""; RspSha256 = ""; ContainsActiveGenerationRef = $false; ActiveGenerationRefArguments = @(); AnalyzerArguments = @(); AdditionalFileArguments = @(); OutputArguments = @() }); continue }
        $content = [IO.File]::ReadAllText($selected.FullName); $lines = @($content -split "`r?`n"); $relative = [IO.Path]::GetRelativePath($ProjectRoot, $selected.FullName).Replace("\", "/")
        $items.Add([pscustomobject]@{ Assembly = $assemblyName; Found = $true; RspPath = $relative; RspSha256 = Get-FileSha256 $selected.FullName; ContainsActiveGenerationRef = $content.Contains("ActiveGenerationRef", [StringComparison]::OrdinalIgnoreCase); ActiveGenerationRefArguments = @($lines | Where-Object { $_ -match 'ActiveGenerationRef' }); AnalyzerArguments = @($lines | Where-Object { $_ -match '^[/-]analyzer:' -and $_ -match 'D0M2FCanaryGenerator' }); AdditionalFileArguments = @($lines | Where-Object { $_ -match '^[/-]additionalfile:' }); OutputArguments = @($lines | Where-Object { $_ -match '^[/-]out:' }) })
    }
    return $items.ToArray()
}

# 把 Unity raw JSON 与 fixture-local Bee 图组合为单次调用证据。
function Read-UnityInvocation
{
    param([string]$ProjectRoot, [string]$RawPath, [string]$LogPath, [string]$Generation, [int]$ExitCode, [bool]$WasKilled)
    if (-not [IO.File]::Exists($RawPath)) { throw "HarnessFailure: Unity raw result missing." }
    if (-not [IO.File]::Exists($LogPath)) { throw "HarnessFailure: Unity log missing." }
    $rawObject = [IO.File]::ReadAllText($RawPath) | ConvertFrom-Json -Depth 32
    $assemblies = foreach ($assembly in @($rawObject.Assemblies))
    {
        $outputHash = if ($assembly.OutputPath -and [IO.File]::Exists($assembly.OutputPath)) { Get-FileSha256 $assembly.OutputPath } else { "" }
        [pscustomobject]@{ Name = $assembly.Name; Generation = $assembly.Generation; DeclaredAssembly = $assembly.DeclaredAssembly; OutputPath = $assembly.OutputPath; OutputSha256 = $outputHash; SourceFiles = @($assembly.SourceFiles); CompiledAssemblyReferences = @($assembly.CompiledAssemblyReferences); Defines = @($assembly.Defines); RoslynAdditionalFilePaths = @($assembly.RoslynAdditionalFilePaths) }
    }
    return [pscustomobject]@{ Generation = $Generation; ExitCode = $ExitCode; WasKilled = $WasKilled; RawPassed = [bool]$rawObject.Passed; RawResultSha256 = Get-FileSha256 $RawPath; LogSha256 = Get-UnityOutputSha256 $LogPath; Assemblies = @($assemblies); Bee = @(Get-BeeEvidence -ProjectRoot $ProjectRoot) }
}

# 直接启动 Unity 执行一次 marker/CompilationPipeline 探针，禁止使用 -quit。
function Invoke-UnityProbe
{
    param([string]$UnityExecutable, [string]$ProjectRoot, [string]$ControlRoot, [string]$Generation, [string]$Label)
    $raw = Join-Path $ControlRoot ("unity-" + $Label + ".json"); $log = Join-Path $ControlRoot ("unity-" + $Label + ".log")
    $arguments = @("-batchmode", "-nographics", "-projectPath", $ProjectRoot, "-executeMethod", "GAS.Tests.D0M2F.SourceGenerator.D0M2FSourceGeneratorUnityProbe.Run", "-d0m2fOutput", $raw, "-d0m2fExpectedGeneration", $Generation, "-logFile", $log)
    $process = Invoke-BoundedProcess -FilePath $UnityExecutable -Arguments $arguments
    return Read-UnityInvocation -ProjectRoot $ProjectRoot -RawPath $raw -LogPath $log -Generation $Generation -ExitCode $process.ExitCode -WasKilled $false
}

# 等待 Unity 写出 raw/signal 后强杀本 harness 拥有的进程树，并保留杀点前证据。
function Invoke-UnityKillProbe
{
    param([string]$UnityExecutable, [string]$ProjectRoot, [string]$ControlRoot, [string]$Generation)
    $raw = Join-Path $ControlRoot "unity-conflict-kill.json"; $log = Join-Path $ControlRoot "unity-conflict-kill.log"; $signal = Join-Path $ControlRoot "unity-conflict-kill.signal"
    $arguments = @("-batchmode", "-nographics", "-projectPath", $ProjectRoot, "-executeMethod", "GAS.Tests.D0M2F.SourceGenerator.D0M2FSourceGeneratorUnityProbe.Run", "-d0m2fOutput", $raw, "-d0m2fExpectedGeneration", $Generation, "-d0m2fHoldSignal", $signal, "-logFile", $log)
    $start = [Diagnostics.ProcessStartInfo]::new($UnityExecutable); $start.UseShellExecute = $false; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true; $start.CreateNoWindow = $true
    foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
    $started = $false; $exitCode = -1; $stdout = $null; $stderr = $null
    try
    {
        $started = $process.Start()
        if (-not $started) { throw "HarnessFailure: kill probe Unity failed to start." }
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        while (-not [IO.File]::Exists($signal) -or -not [IO.File]::Exists($raw))
        {
            if ($process.HasExited)
            {
                $process.WaitForExit()
                throw "HarnessFailure: Unity exited before kill signal.`n$($stdout.GetAwaiter().GetResult())`n$($stderr.GetAwaiter().GetResult())"
            }
            Get-RemainingMilliseconds | Out-Null; Start-Sleep -Milliseconds 100
        }
        Stop-OwnedProcess -Process $process -FailureMessage "UnityProcessResidue: owned Unity tree survived planned kill."
        $exitCode = $process.ExitCode
        $stdout.GetAwaiter().GetResult() | Out-Null; $stderr.GetAwaiter().GetResult() | Out-Null
    }
    finally
    {
        try
        {
            if ($started -and -not $process.HasExited)
            {
                Stop-OwnedProcess -Process $process -FailureMessage "UnityProcessResidue: owned Unity tree survived exception cleanup."
            }
            if ($started -and $process.HasExited)
            {
                if ($null -ne $stdout) { $stdout.GetAwaiter().GetResult() | Out-Null }
                if ($null -ne $stderr) { $stderr.GetAwaiter().GetResult() | Out-Null }
            }
        }
        finally
        {
            $process.Dispose()
        }
    }
    return Read-UnityInvocation -ProjectRoot $ProjectRoot -RawPath $raw -LogPath $log -Generation $Generation -ExitCode $exitCode -WasKilled $true
}

# 判断一次 Unity 调用的三程序集是否全部绑定期望 generation 且具备真实 Bee 输入。
function Test-InvocationPassed
{
    param([Parameter(Mandatory = $true)]$Invocation, [Parameter(Mandatory = $true)][string]$Expected, [switch]$AllowKilled)
    if ((-not $AllowKilled -and $Invocation.ExitCode -ne 0) -or ($AllowKilled -and -not $Invocation.WasKilled) -or -not $Invocation.RawPassed -or @($Invocation.Assemblies).Count -ne 3) { return $false }
    foreach ($assembly in @($Invocation.Assemblies)) { if ($assembly.Generation -ne $Expected -or $assembly.DeclaredAssembly -ne $assembly.Name -or -not $assembly.OutputSha256) { return $false } }
    foreach ($rsp in @($Invocation.Bee)) { if (-not $rsp.Found -or @($rsp.AnalyzerArguments).Count -ne 1 -or @($rsp.AdditionalFileArguments).Count -lt 1) { return $false } }
    return $true
}

# 显式检查完整 RSP 与 CompilationPipeline 字段均未把 ActiveGenerationRef 当作编译输入。
function Test-ActiveRefAbsentFromCompilerInputs
{
    param([Parameter(Mandatory = $true)]$Invocation)
    foreach ($rsp in @($Invocation.Bee)) { if ($rsp.ContainsActiveGenerationRef -or @($rsp.ActiveGenerationRefArguments).Count -ne 0) { return $false } }
    foreach ($assembly in @($Invocation.Assemblies))
    {
        foreach ($value in @($assembly.SourceFiles) + @($assembly.RoslynAdditionalFilePaths) + @($assembly.CompiledAssemblyReferences) + @($assembly.Defines))
        {
            if ([string]$value -match 'ActiveGenerationRef') { return $false }
        }
    }
    return $true
}

# 要求三个程序集的 CompilationPipeline 都只接收同一个精确 semantic selector 路径。
function Test-SingleSemanticSelector
{
    param([Parameter(Mandatory = $true)]$Invocation, [Parameter(Mandatory = $true)][string]$ProjectRoot)
    $expected = Resolve-NormalizedPath (Join-Path $ProjectRoot $script:SelectorRelativePath)
    foreach ($assembly in @($Invocation.Assemblies))
    {
        $matches = @($assembly.RoslynAdditionalFilePaths | Where-Object { [IO.Path]::GetFileName([string]$_) -ceq "Generation.D0M2FCanaryGenerator.additionalfile" })
        if ($matches.Count -ne 1 -or (Resolve-NormalizedPath ([string]$matches[0])) -ne $expected) { return $false }
    }
    return $true
}

# 构造符合 R3 合同的单个 case 结果。
function New-Case
{
    param([string]$CaseId, [string]$Status, [string]$Reason, [object]$Evidence)
    return [pscustomobject]@{ CaseId = $CaseId; Status = $Status; Reason = $Reason; Evidence = $Evidence }
}

# 构造尚未执行的精确 case 集，确保失败路径也不丢 case-ID。
function New-DefaultCases
{
    $ids = if ($Mode -eq "SelectorConflict") { @("X-01", "X-02", "X-03", "X-04") } else { @("S-01", "S-02", "S-03", "S-04") }
    return @($ids | ForEach-Object { New-Case -CaseId $_ -Status "NotRun" -Reason "EvidenceIncomplete" -Evidence ([pscustomobject]@{}) })
}

# 以 fresh-only 单 JSON 写出最终机器证据。
function Write-FinalResult
{
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)]$Result)
    $json = ($Result | ConvertTo-Json -Depth 64).Replace("`r`n", "`n") + "`n"
    Write-FreshText -Path $Path -Content $json
}

$resolvedOutput = if ([IO.Path]::IsPathRooted($OutputPath)) { Resolve-NormalizedPath $OutputPath } else { Resolve-NormalizedPath (Join-Path (Get-Location) $OutputPath) }
if ([IO.Path]::GetExtension($resolvedOutput) -ne ".json") { throw "OutputPath must end in .json." }
if ([IO.File]::Exists($resolvedOutput) -or [IO.Directory]::Exists($resolvedOutput)) { throw "OutputPath must be fresh: $resolvedOutput" }
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolvedOutput)) | Out-Null
$resolvedUnity = ""; $unityVersion = ""
$suiteRoot = $null; $fixture = $null; $cases = New-DefaultCases; $invocations = @(); $roles = @(); $status = "Inconclusive"; $reason = "HarnessFailure"; $cleanup = "NotStarted"; $activeRef = $null
$inputs = [ordered]@{ Mode = $Mode; GeneratorSourceSha256 = ""; GeneratorAssemblySha256 = ""; SemanticSelectorRelativePath = $script:SelectorRelativePath; InitialSemanticSha256 = ""; FinalSemanticSha256 = ""; ExpectedGenerationA = "generation-a"; ExpectedGenerationB = "generation-b"; ActiveGenerationRefSha256 = "" }
try
{
    if (-not [IO.Path]::IsPathRooted($UnityPath)) { throw "UnityIdentityMismatch: UnityPath must be absolute." }
    $resolvedUnity = Resolve-NormalizedPath $UnityPath
    if (-not [IO.File]::Exists($resolvedUnity)) { throw "UnityIdentityMismatch: Unity executable is missing." }
    $unityVersion = (Get-Item -LiteralPath $resolvedUnity).VersionInfo.ProductVersion
    if (-not $unityVersion.StartsWith("6000.3.14f1", [StringComparison]::Ordinal)) { throw "UnityIdentityMismatch: $unityVersion" }
    $suiteRoot = New-SuiteRoot; $fixture = New-Fixture -SuiteRoot $suiteRoot; $inputs.GeneratorSourceSha256 = $fixture.Generator.SourceSha256; $inputs.GeneratorAssemblySha256 = $fixture.Generator.AssemblySha256
    $control = Join-Path $suiteRoot "Control"; [IO.Directory]::CreateDirectory($control) | Out-Null
    if ($Mode -eq "SelectorConflict")
    {
        $inputs.FinalSemanticSha256 = Set-SemanticSelector -ProjectRoot $fixture.ProjectRoot -Generation "generation-b"; $activeRef = New-ConflictingActiveRef -ProjectRoot $fixture.ProjectRoot; $inputs.ActiveGenerationRefSha256 = $activeRef.Sha256
        $killInvocation = Invoke-UnityKillProbe -UnityExecutable $resolvedUnity -ProjectRoot $fixture.ProjectRoot -ControlRoot $control -Generation "generation-b"
        $restartInvocation = Invoke-UnityProbe -UnityExecutable $resolvedUnity -ProjectRoot $fixture.ProjectRoot -ControlRoot $control -Generation "generation-b" -Label "conflict-restart"; $invocations = @($killInvocation, $restartInvocation)
        $killPassed = Test-InvocationPassed -Invocation $killInvocation -Expected "generation-b" -AllowKilled; $restartPassed = Test-InvocationPassed -Invocation $restartInvocation -Expected "generation-b"
        $candidateConsumed = $killPassed -and $restartPassed -and (Test-SingleSemanticSelector $killInvocation $fixture.ProjectRoot) -and (Test-SingleSemanticSelector $restartInvocation $fixture.ProjectRoot)
        $activeRefAbsent = (Test-ActiveRefAbsentFromCompilerInputs $killInvocation) -and (Test-ActiveRefAbsentFromCompilerInputs $restartInvocation)
        $cases = @(
            (New-Case "X-01" "Passed" "None" ([pscustomobject]@{ ActiveGenerationRef = "generation-a"; SemanticSelector = "generation-b" })),
            (New-Case "X-02" $(if ($candidateConsumed) { "Passed" } else { "Inconclusive" }) $(if ($candidateConsumed) { "None" } else { "SelectorConflictObserved" }) ([pscustomobject]@{ KillInvocation = $killInvocation; ColdRestartInvocation = $restartInvocation })),
            (New-Case "X-03" $(if ($activeRefAbsent) { "Passed" } else { "Inconclusive" }) $(if ($activeRefAbsent) { "None" } else { "SelectorConflictObserved" }) ([pscustomobject]@{ CompilerInputsContainActiveRef = (-not $activeRefAbsent); KillBee = $killInvocation.Bee; RestartBee = $restartInvocation.Bee })),
            (New-Case "X-04" $(if ($candidateConsumed) { "Passed" } else { "Inconclusive" }) $(if ($candidateConsumed) { "None" } else { "SelectorConflictObserved" }) ([pscustomobject]@{ ExpectedGeneration = "generation-b"; KillAssemblies = $killInvocation.Assemblies; RestartAssemblies = $restartInvocation.Assemblies }))
        )
        $status = if ($candidateConsumed -and $activeRefAbsent) { "Passed" } else { "Inconclusive" }; $reason = if ($status -eq "Passed") { "None" } else { "SelectorConflictObserved" }
    }
    else
    {
        $inputs.InitialSemanticSha256 = Set-SemanticSelector -ProjectRoot $fixture.ProjectRoot -Generation "generation-a"; $invocationA = Invoke-UnityProbe -UnityExecutable $resolvedUnity -ProjectRoot $fixture.ProjectRoot -ControlRoot $control -Generation "generation-a" -Label "feasibility-a"
        $inputs.FinalSemanticSha256 = Set-SemanticSelector -ProjectRoot $fixture.ProjectRoot -Generation "generation-b"; $invocationB = Invoke-UnityProbe -UnityExecutable $resolvedUnity -ProjectRoot $fixture.ProjectRoot -ControlRoot $control -Generation "generation-b" -Label "feasibility-b"; $invocations = @($invocationA, $invocationB)
        $aPassed = Test-InvocationPassed $invocationA "generation-a"; $bPassed = Test-InvocationPassed $invocationB "generation-b"
        $recompiled = $aPassed -and $bPassed; foreach ($name in $script:TargetAssemblies) { $a = $invocationA.Assemblies | Where-Object Name -eq $name; $b = $invocationB.Assemblies | Where-Object Name -eq $name; $recompiled = $recompiled -and $a.OutputSha256 -and $b.OutputSha256 -and $a.OutputSha256 -ne $b.OutputSha256 }
        $noActiveGenerated = @($invocationB.Assemblies.SourceFiles | Where-Object { $_ -match '\.gen\.cs$' }).Count -eq 0
        $singleSelector = (Test-SingleSemanticSelector $invocationA $fixture.ProjectRoot) -and (Test-SingleSemanticSelector $invocationB $fixture.ProjectRoot)
        $cases = @(
            (New-Case "S-01" $(if ($aPassed) { "Passed" } else { "Failed" }) $(if ($aPassed) { "None" } else { "AssemblyInvalidationFailed" }) ([pscustomobject]@{ LibraryInherited = $false; Invocation = $invocationA })),
            (New-Case "S-02" $(if ($bPassed) { "Passed" } else { "Failed" }) $(if ($bPassed) { "None" } else { "MixedGenerationObserved" }) ([pscustomobject]@{ Invocation = $invocationB })),
            (New-Case "S-03" $(if ($recompiled) { "Passed" } else { "Failed" }) $(if ($recompiled) { "None" } else { "StaleBeeOrRspDeterminedGeneration" }) ([pscustomobject]@{ OutputChangedForAllAssemblies = $recompiled; A = $invocationA.Bee; B = $invocationB.Bee })),
            (New-Case "S-04" $(if ($noActiveGenerated -and $singleSelector) { "Passed" } else { "Failed" }) $(if (-not $noActiveGenerated) { "ActiveGeneratedSourceCoConsumptionRequired" } elseif (-not $singleSelector) { "PerAssemblySelectorRequired" } else { "None" }) ([pscustomobject]@{ ActiveGeneratedSourceConsumed = (-not $noActiveGenerated); SingleSelectorAcrossAssemblies = $singleSelector }))
        )
        $allPassed = @($cases | Where-Object Status -ne "Passed").Count -eq 0; $status = if ($allPassed) { "Passed" } else { "HardRejected" }; $reason = if ($allPassed) { "None" } else { ($cases | Where-Object Status -ne "Passed" | Select-Object -First 1).Reason }
    }
    $selectorPath = Join-Path $fixture.ProjectRoot $script:SelectorRelativePath
    $roles = @(
        [pscustomobject]@{ Path = $script:SelectorRelativePath; Role = "Authority"; Mutable = $true; UnityConsumed = $true; Evidence = "Generated markers and target Bee inputs." },
        [pscustomobject]@{ Path = "Assets/Analyzers/D0M2FCanaryGenerator.dll"; Role = "Authority"; Mutable = $false; UnityConsumed = $true; Evidence = $inputs.GeneratorAssemblySha256 },
        [pscustomobject]@{ Path = "Library/ScriptAssemblies/**"; Role = "Derived"; Mutable = $true; UnityConsumed = $true; Evidence = "Assembly output hashes." },
        [pscustomobject]@{ Path = "Library/Bee/**"; Role = "Cache"; Mutable = $true; UnityConsumed = $true; Evidence = "Fixture-local RSP snapshots." }
    )
    if ($null -ne $activeRef)
    {
        $roles += [pscustomobject]@{ Path = "ProjectSettings/GasCodeGen/ActiveGenerationRef.json"; Role = "Derived"; Mutable = $true; UnityConsumed = $false; Evidence = "Absent from full target RSP and CompilationPipeline inputs." }
    }
}
catch [TimeoutException]
{
    $status = "TimedOut"; $reason = "TimeBoxExceeded"; $cases = @($cases | ForEach-Object { if ($_.Status -eq "NotRun") { $_.Status = "TimedOut"; $_.Reason = "TimeBoxExceeded" }; $_ })
}
catch
{
    $errorMessage = $_.Exception.Message
    $status = "Inconclusive"; $reason = if ($errorMessage -match '^FixtureBoundaryViolation') { "FixtureBoundaryViolation" } elseif ($errorMessage -match '^UnityIdentityMismatch') { "UnityIdentityMismatch" } elseif ($errorMessage -match '^UnityProcessResidue') { "UnityProcessResidue" } elseif ($errorMessage -match '^CleanupFailure') { "CleanupFailure" } else { "HarnessFailure" }
    $cases = @($cases | ForEach-Object { if ($_.Status -eq "NotRun") { $_.Status = "Inconclusive"; $_.Reason = $reason; $_.Evidence = [pscustomobject]@{ Error = $errorMessage } }; $_ })
}
finally
{
    if ($null -ne $suiteRoot)
    {
        if ($KeepFixture) { $cleanup = "Kept" } else { try { Remove-SuiteRoot -Root $suiteRoot; $cleanup = "Passed" } catch { $cleanup = "Failed"; $status = "Inconclusive"; $reason = "CleanupFailure" } }
    }
}

$result = [ordered]@{
    Schema = $script:Schema; RunId = $RunId; ProbeId = $script:ProbeId; Status = $status; Reason = $reason; Cases = @($cases); Inputs = [pscustomobject]$inputs
    Protected = [pscustomobject]@{ ProductionWriteAttempted = $false; FixtureEscaped = $false }
    Fixture = [pscustomobject]@{ Root = if ($KeepFixture -and $suiteRoot) { $suiteRoot } else { "<deleted>" }; Kept = [bool]$KeepFixture; OwnerSentinelValidated = $cleanup -in @("Passed", "Kept"); NoReparsePoints = $cleanup -ne "Failed"; NoHardlinks = $cleanup -ne "Failed"; CleanupStatus = $cleanup }
    Unity = [pscustomobject]@{ ExecutablePath = $resolvedUnity; ExecutableSha256 = if ($resolvedUnity -and [IO.File]::Exists($resolvedUnity)) { Get-FileSha256 $resolvedUnity } else { "" }; Version = $unityVersion; Invocations = @($invocations); TargetAssemblies = $script:TargetAssemblies }
    RoleObservations = @($roles)
}
Write-FinalResult -Path $resolvedOutput -Result ([pscustomobject]$result)
if ($status -eq "Passed") { exit 0 }
exit 1
