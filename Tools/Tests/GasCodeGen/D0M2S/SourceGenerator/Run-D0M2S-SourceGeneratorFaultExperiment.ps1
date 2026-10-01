#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^D0M2S-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$')][string]$RunId,
    [Parameter(Mandatory = $true)][string]$EvidenceRoot,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [switch]$KeepFixture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:Schema = "D0M2S-SourceGeneratorFaultExperiment-v1"
$script:SuitePrefix = "gas-codegen-d0-m2s-sourcegen-"
$script:ExternalPrefix = "gas-codegen-d0-m2s-sourcegen-external-"
$script:OwnerSentinel = ".d0m2s-sourcegen-owner"
$script:ExternalOwnerSentinel = ".d0m2s-sourcegen-external-owner"
$script:SelectorRelativePath = "Assets/Selector/Generation.D0M2SCanaryGenerator.additionalfile"
$script:GeneratorRelativePath = "Assets/Analyzers/D0M2SCanaryGenerator.dll"
$script:GeneratorAssemblyName = "D0M2SCanaryGenerator"
$script:BaseFixtureRoot = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "../../D0M2F/SourceGenerator/Fixture~"))
$script:OverlayRoot = Join-Path $PSScriptRoot "FixtureOverlay~"
$script:TargetAssemblies = @(
    "com.exhard.exgas.generated.runtime",
    "com.exhard.exgas.generated.editor",
    "com.exhard.exgas.autochessdemo"
)
$script:DeadlineUtc = [DateTime]::UtcNow.AddMinutes(45)
$script:OwnedProcessIds = [Collections.Generic.List[int]]::new()
$script:UnityInvocationIds = [Collections.Generic.List[string]]::new()
$script:Utf8NoBom = [Text.UTF8Encoding]::new($false)

if (-not ("D0M2SNativeFileInfo" -as [type]))
{
    Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

/// <summary>为 D0-M2S fixture 查询 hardlink 并创建边界回归 alias。</summary>
public static class D0M2SNativeFileInfo
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(
        string newFileName,
        string existingFileName,
        IntPtr securityAttributes);

    /// <summary>返回已打开普通文件的链接计数，查询失败时显式抛错。</summary>
    public static uint GetLinkCount(SafeFileHandle file)
    {
        ByHandleFileInformation information;
        if (!GetFileInformationByHandle(file, out information))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return information.NumberOfLinks;
    }

    /// <summary>创建测试专用 hardlink alias，失败时显式抛错。</summary>
    public static void CreateHardLink(string aliasPath, string targetPath)
    {
        if (!CreateHardLinkW(aliasPath, targetPath, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
"@
}

# 规范化路径并移除尾部分隔符。
function Resolve-NormalizedPath
{
    param([Parameter(Mandatory = $true)][string]$Path)

    return [IO.Path]::GetFullPath($Path).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
}

# 将相对输入路径按当前工作目录解析为绝对路径。
function Resolve-InputPath
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([IO.Path]::IsPathRooted($Path))
    {
        return Resolve-NormalizedPath $Path
    }

    return Resolve-NormalizedPath (Join-Path (Get-Location) $Path)
}

# 计算普通文件 raw bytes 的 SHA-256。
function Get-FileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while ($true)
    {
        try { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
        catch [IO.IOException]
        {
            if ([DateTime]::UtcNow -ge $deadline) { throw }
            Get-RemainingMilliseconds | Out-Null
            Start-Sleep -Milliseconds 50
        }
    }
}

# 等待 CreateNew 信号完成写入并成为可解析 JSON，避免把文件可见误当成证据完整。
function Read-FreshJsonWhenComplete
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][Diagnostics.Process]$OwnerProcess,
        [Parameter(Mandatory = $true)][DateTime]$Deadline
    )

    while ([DateTime]::UtcNow -lt $Deadline)
    {
        if ([IO.File]::Exists($Path))
        {
            try
            {
                $content = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
                if (-not [string]::IsNullOrWhiteSpace($content))
                {
                    return $content | ConvertFrom-Json -Depth 16
                }
            }
            catch [IO.IOException] { }
            catch [Management.Automation.RuntimeException] { }
        }
        if ($OwnerProcess.HasExited) { return $null }
        Get-RemainingMilliseconds | Out-Null
        Start-Sleep -Milliseconds 50
    }
    return $null
}

# 在有界时间内确认已登记的 descendant PID 真实退出。
function Wait-OwnedProcessIdExit
{
    param(
        [Parameter(Mandatory = $true)][int]$ProcessId,
        [int]$TimeoutMilliseconds = 30000
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    while ($null -ne (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue))
    {
        if ([DateTime]::UtcNow -ge $deadline) { return $false }
        Get-RemainingMilliseconds | Out-Null
        Start-Sleep -Milliseconds 50
    }
    return $true
}

# 检查动态 JSON 对象是否拥有后续读取所需的全部字段。
function Test-RequiredProperties
{
    param(
        $Value,
        [Parameter(Mandatory = $true)][string[]]$Names
    )

    if ($null -eq $Value) { return $false }
    foreach ($name in $Names)
    {
        if ($null -eq $Value.PSObject.Properties[$name]) { return $false }
    }
    return $true
}

# 计算内存 bytes 的 SHA-256。
function Get-BytesSha256
{
    param([Parameter(Mandatory = $true)][byte[]]$Bytes)

    return ([Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($Bytes))).ToLowerInvariant()
}

# 返回目录内普通文件的稳定相对路径、长度和 SHA 清单。
function Get-TreeInventory
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $resolved = Resolve-NormalizedPath $Root
    return @([IO.Directory]::EnumerateFiles($resolved, "*", [IO.SearchOption]::AllDirectories) |
        ForEach-Object {
            [pscustomobject][ordered]@{
                Path = [IO.Path]::GetRelativePath($resolved, $_).Replace("\", "/")
                Length = [long]([IO.FileInfo]::new($_)).Length
                Sha256 = Get-FileSha256 $_
            }
        } | Sort-Object Path)
}

# 计算目录清单 canonical 文本的聚合 SHA-256。
function Get-TreeDigest
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $lines = @(Get-TreeInventory $Root | ForEach-Object {
        "$($_.Path)`t$($_.Length)`t$($_.Sha256)"
    })
    return Get-BytesSha256 $script:Utf8NoBom.GetBytes(($lines -join "`n") + "`n")
}

# 以 UTF-8 无 BOM、LF 和 CreateNew 语义写出文本。
function Write-FreshText
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content
    )

    $parent = [IO.Path]::GetDirectoryName($Path)
    if ([string]::IsNullOrWhiteSpace($parent)) { throw "Evidence path has no parent: $Path" }
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $normalized = $Content.Replace("`r`n", "`n").Replace("`r", "`n")
    $bytes = $script:Utf8NoBom.GetBytes($normalized)
    $stream = [IO.FileStream]::new(
        $Path,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
}

# 以 fresh-only JSON 写出机器证据。
function Write-FreshJson
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$Value
    )

    $json = ($Value | ConvertTo-Json -Depth 100).Replace("`r`n", "`n") + "`n"
    Write-FreshText -Path $Path -Content $json
}

# 用精确 bytes 覆盖 fixture 内已拥有的普通文件并强制刷盘。
function Set-ExactFileBytes
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][byte[]]$Bytes
    )

    $stream = [IO.FileStream]::new(
        $Path,
        [IO.FileMode]::Create,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None)
    try { $stream.Write($Bytes, 0, $Bytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
}

# 返回 suite 剩余硬时间盒毫秒数。
function Get-RemainingMilliseconds
{
    $remaining = [int64]($script:DeadlineUtc - [DateTime]::UtcNow).TotalMilliseconds
    if ($remaining -le 0) { throw [TimeoutException]::new("D0-M2S time-box exceeded.") }
    return [int][Math]::Min($remaining, [int]::MaxValue)
}

# 创建 EvidenceRoot 下 fresh case 目录。
function New-EvidenceDirectory
{
    param([Parameter(Mandatory = $true)][string]$Name)

    $path = Join-Path $script:ResolvedEvidenceRoot $Name
    if ([IO.File]::Exists($path) -or [IO.Directory]::Exists($path))
    {
        throw "Evidence path must be fresh: $path"
    }
    [IO.Directory]::CreateDirectory($path) | Out-Null
    return $path
}

# 验证目录树不含 reparse point，普通文件均只有一个 hardlink。
function Assert-OrdinaryTree
{
    param([Parameter(Mandatory = $true)][string]$Root)

    foreach ($entry in @(Get-ChildItem -LiteralPath $Root -Force -Recurse))
    {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "FixtureBoundaryViolation: reparse point $($entry.FullName)"
        }
        if ($entry.PSIsContainer) { continue }
        $stream = [IO.File]::Open(
            $entry.FullName,
            [IO.FileMode]::Open,
            [IO.FileAccess]::Read,
            [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
        try
        {
            if ([D0M2SNativeFileInfo]::GetLinkCount($stream.SafeFileHandle) -ne 1)
            {
                throw "FixtureBoundaryViolation: hardlink $($entry.FullName)"
            }
        }
        finally { $stream.Dispose() }
    }
}

# 深拷贝普通目录树，拒绝继承任何 hardlink 或 reparse point。
function Copy-OrdinaryTree
{
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    Assert-OrdinaryTree $Source
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($directory in [IO.Directory]::EnumerateDirectories(
        $Source, "*", [IO.SearchOption]::AllDirectories))
    {
        $relative = [IO.Path]::GetRelativePath($Source, $directory)
        [IO.Directory]::CreateDirectory((Join-Path $Destination $relative)) | Out-Null
    }
    foreach ($file in [IO.Directory]::EnumerateFiles(
        $Source, "*", [IO.SearchOption]::AllDirectories))
    {
        $relative = [IO.Path]::GetRelativePath($Source, $file)
        [IO.File]::Copy($file, (Join-Path $Destination $relative), $false)
    }
    Assert-OrdinaryTree $Destination
}

# 把 D0M2S overlay 的新普通文件叠加到已复制的历史 fixture 骨架。
function Copy-OverlayTree
{
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    Assert-OrdinaryTree $Source
    foreach ($directory in [IO.Directory]::EnumerateDirectories(
        $Source, "*", [IO.SearchOption]::AllDirectories))
    {
        $relative = [IO.Path]::GetRelativePath($Source, $directory)
        [IO.Directory]::CreateDirectory((Join-Path $Destination $relative)) | Out-Null
    }
    foreach ($file in [IO.Directory]::EnumerateFiles(
        $Source, "*", [IO.SearchOption]::AllDirectories))
    {
        $target = Join-Path $Destination ([IO.Path]::GetRelativePath($Source, $file))
        if ([IO.File]::Exists($target)) { throw "Overlay collision: $target" }
        [IO.File]::Copy($file, $target, $false)
    }
}

# 创建带唯一 owner sentinel 的 OS 临时根。
function New-OwnedRoot
{
    param(
        [Parameter(Mandatory = $true)][string]$Prefix,
        [Parameter(Mandatory = $true)][string]$SentinelName
    )

    $root = Join-Path ([IO.Path]::GetTempPath()) ($Prefix + [Guid]::NewGuid().ToString("N"))
    if ([IO.File]::Exists($root) -or [IO.Directory]::Exists($root))
    {
        throw "FixtureBoundaryViolation: owned root already exists."
    }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    Write-FreshText -Path (Join-Path $root $SentinelName) -Content ($RunId + "`n")
    return Resolve-NormalizedPath $root
}

# 复核待清理根仍是本次 run 精确拥有的普通临时目录。
function Assert-OwnedRoot
{
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Prefix,
        [Parameter(Mandatory = $true)][string]$SentinelName
    )

    $normalized = Resolve-NormalizedPath $Root
    $temp = Resolve-NormalizedPath ([IO.Path]::GetTempPath())
    $parent = Resolve-NormalizedPath ([IO.Path]::GetDirectoryName($normalized))
    if ($parent -cne $temp) { throw "FixtureBoundaryViolation: root escaped temp." }
    $escaped = [Regex]::Escape($Prefix)
    if ([IO.Path]::GetFileName($normalized) -cnotmatch "^$escaped[0-9a-f]{32}$")
    {
        throw "FixtureBoundaryViolation: root name invalid."
    }
    $sentinel = Join-Path $normalized $SentinelName
    if (-not [IO.File]::Exists($sentinel) -or
        [IO.File]::ReadAllText($sentinel) -cne ($RunId + "`n"))
    {
        throw "FixtureBoundaryViolation: owner sentinel invalid."
    }
    Assert-OrdinaryTree $normalized
}

# 仅在完整边界复核后递归删除本次 run 拥有的精确根。
function Remove-OwnedRoot
{
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Prefix,
        [Parameter(Mandatory = $true)][string]$SentinelName
    )

    Assert-OwnedRoot -Root $Root -Prefix $Prefix -SentinelName $SentinelName
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -Force -Recurse -File))
    {
        [IO.File]::Delete($file.FullName)
    }
    $directories = @(Get-ChildItem -LiteralPath $Root -Force -Recurse -Directory |
        Sort-Object { $_.FullName.Length } -Descending)
    foreach ($directory in $directories) { [IO.Directory]::Delete($directory.FullName, $false) }
    [IO.Directory]::Delete($Root, $false)
}

# 只终止本 harness 启动的进程树，并要求它在 30 秒内真实退出。
function Stop-OwnedProcess
{
    param(
        [Parameter(Mandatory = $true)][Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][string]$FailureMessage,
        [switch]$RequireRunning
    )

    if ($Process.HasExited)
    {
        $Process.WaitForExit()
        if ($RequireRunning) { throw $FailureMessage }
        return
    }
    try { $Process.Kill($true) }
    catch
    {
        if ($Process.HasExited)
        {
            $Process.WaitForExit()
            if ($RequireRunning) { throw $FailureMessage }
            return
        }
        throw [InvalidOperationException]::new($FailureMessage, $_.Exception)
    }
    if (-not $Process.WaitForExit(30000) -or -not $Process.HasExited)
    {
        throw $FailureMessage
    }
    $Process.WaitForExit()
}

# 启动受跟踪子进程，保留 stdout/stderr/exit/timeout raw 并闭合 owned tree。
function Invoke-TrackedProcess
{
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$EvidencePath,
        [string]$WorkingDirectory = "",
        [hashtable]$Environment = @{},
        [int]$TimeoutMilliseconds = 300000
    )

    $start = [Diagnostics.ProcessStartInfo]::new($FilePath)
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.CreateNoWindow = $true
    if ($WorkingDirectory) { $start.WorkingDirectory = $WorkingDirectory }
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    foreach ($name in $Environment.Keys) { $start.Environment[$name] = [string]$Environment[$name] }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $started = $false; $timedOut = $false; $stdout = $null; $stderr = $null; $processId = 0
    try
    {
        $started = $process.Start()
        if (-not $started) { throw "HarnessFailure: process failed to start: $FilePath" }
        $processId = $process.Id; $script:OwnedProcessIds.Add($processId)
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $bounded = [Math]::Min($TimeoutMilliseconds, (Get-RemainingMilliseconds))
        if (-not $process.WaitForExit($bounded))
        {
            $timedOut = $true
            Stop-OwnedProcess $process "UnityProcessResidue: timed-out owned process survived cleanup."
        }
        $process.WaitForExit()
        $result = [pscustomobject][ordered]@{
            Schema = "D0M2S-Process-v1"; FilePath = $FilePath; Arguments = @($Arguments)
            ProcessId = $processId; ExitCode = [int]$process.ExitCode; TimedOut = $timedOut
            DurationMilliseconds = [long]$stopwatch.ElapsedMilliseconds
            Stdout = $stdout.GetAwaiter().GetResult(); Stderr = $stderr.GetAwaiter().GetResult()
        }
        Write-FreshJson -Path $EvidencePath -Value $result
        return $result
    }
    finally
    {
        try
        {
            if ($started -and -not $process.HasExited)
            {
                Stop-OwnedProcess $process "UnityProcessResidue: exceptional owned process survived cleanup."
            }
            if ($null -ne $stdout -and $started) { $stdout.GetAwaiter().GetResult() | Out-Null }
            if ($null -ne $stderr -and $started) { $stderr.GetAwaiter().GetResult() | Out-Null }
        }
        finally { $stopwatch.Stop(); $process.Dispose() }
    }
}

# 编译 netstandard2.0 canary generator 并复制到隔离 fixture analyzer 路径。
function Install-CanaryGenerator
{
    param(
        [Parameter(Mandatory = $true)][string]$SuiteRoot,
        [Parameter(Mandatory = $true)][string]$ProjectRoot
    )

    $source = Join-Path $PSScriptRoot "Generator~"
    $buildRoot = Join-Path $SuiteRoot "GeneratorBuild"
    $outputRoot = Join-Path $SuiteRoot "GeneratorOutput"
    Copy-OrdinaryTree -Source $source -Destination $buildRoot
    [IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    $project = Join-Path $buildRoot "D0M2SCanaryGenerator.csproj"
    [IO.File]::Move((Join-Path $buildRoot "D0M2SCanaryGenerator.csproj.template"), $project)
    $evidence = New-EvidenceDirectory "inputs-generator-build"
    $build = Invoke-TrackedProcess -FilePath "dotnet" -WorkingDirectory $buildRoot `
        -EvidencePath (Join-Path $evidence "process.json") -TimeoutMilliseconds 180000 `
        -Arguments @(
            "build", $project, "--configuration", "Release", "--output", $outputRoot,
            "--nologo", "/p:RestoreIgnoreFailedSources=true"
        )
    if ($build.TimedOut -or $build.ExitCode -ne 0)
    {
        throw "HarnessFailure: generator build failed."
    }
    $dll = Join-Path $outputRoot ($script:GeneratorAssemblyName + ".dll")
    if (-not [IO.File]::Exists($dll) -or
        [Reflection.AssemblyName]::GetAssemblyName($dll).Name -cne $script:GeneratorAssemblyName)
    {
        throw "HarnessFailure: generator assembly identity mismatch."
    }
    $target = Join-Path $ProjectRoot $script:GeneratorRelativePath
    [IO.File]::Copy($dll, $target, $false)
    return [pscustomobject][ordered]@{
        SourceSha256 = Get-FileSha256 (Join-Path $source "D0M2SCanaryGenerator.cs")
        ProjectSha256 = Get-FileSha256 (Join-Path $source "D0M2SCanaryGenerator.csproj.template")
        AssemblySha256 = Get-FileSha256 $target
        AssemblyLength = [long]([IO.FileInfo]::new($target)).Length
        TargetPath = $target
    }
}

# 创建 fresh Unity fixture，并只复用历史 fixture 的无 Library 骨架。
function New-Fixture
{
    param([Parameter(Mandatory = $true)][string]$SuiteRoot)

    if (-not [IO.Directory]::Exists($script:BaseFixtureRoot))
    {
        throw "InputIdentityDrift: D0M2F fixture skeleton is missing."
    }
    $project = Join-Path $SuiteRoot "Project"
    Copy-OrdinaryTree -Source $script:BaseFixtureRoot -Destination $project
    foreach ($relative in @(
        "Assets/Editor/D0M2FSourceGeneratorUnityProbe.cs",
        "Assets/Editor/D0M2FSourceGeneratorUnityProbe.cs.meta",
        "Assets/Selector/Generation.D0M2FCanaryGenerator.additionalfile",
        "Assets/Selector/Generation.D0M2FCanaryGenerator.additionalfile.meta",
        "Assets/Analyzers/D0M2FCanaryGenerator.dll.meta"
    ))
    {
        $old = Join-Path $project $relative
        if ([IO.File]::Exists($old)) { [IO.File]::Delete($old) }
    }
    Copy-OverlayTree -Source $script:OverlayRoot -Destination $project
    [IO.Directory]::CreateDirectory((Join-Path $project "Packages")) | Out-Null
    Write-FreshText -Path (Join-Path $project "Packages/manifest.json") `
        -Content "{`n  `"dependencies`": {}`n}`n"
    if ([IO.Directory]::Exists((Join-Path $project "Library")))
    {
        throw "FixtureBoundaryViolation: fixture inherited Library."
    }
    $generator = Install-CanaryGenerator -SuiteRoot $SuiteRoot -ProjectRoot $project
    Assert-OrdinaryTree $project
    return [pscustomobject][ordered]@{
        ProjectRoot = $project
        Generator = $generator
        BaseFixtureDigest = Get-TreeDigest $script:BaseFixtureRoot
        OverlayDigest = Get-TreeDigest $script:OverlayRoot
    }
}

# 返回 generation 对应的唯一 canonical selector bytes。
function Get-CanonicalSelectorBytes
{
    param([Parameter(Mandatory = $true)][ValidateSet("generation-a", "generation-b")][string]$Generation)

    return $script:Utf8NoBom.GetBytes($Generation + "`n")
}

# 以 File.Replace 原子更新 selector，并返回 before/after raw identity。
function Set-SemanticSelectorExact
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][ValidateSet("generation-a", "generation-b")][string]$Generation
    )

    $target = Join-Path $ProjectRoot $script:SelectorRelativePath
    $next = $target + ".next"; $backup = $target + ".backup"
    if (-not [IO.File]::Exists($target) -or [IO.File]::Exists($next) -or [IO.File]::Exists($backup))
    {
        throw "HarnessFailure: selector transaction precondition failed."
    }
    $before = Get-FileSha256 $target
    Write-FreshText -Path $next -Content ($Generation + "`n")
    [IO.File]::Replace($next, $target, $backup, $true)
    [IO.File]::Delete($backup)
    return [pscustomobject][ordered]@{
        BeforeSha256 = $before; AfterSha256 = Get-FileSha256 $target; Generation = $Generation
    }
}

# 断言 selector raw bytes 精确等于 canonical generation。
function Assert-SelectorIdentity
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][ValidateSet("generation-a", "generation-b")][string]$Generation
    )

    $path = Join-Path $ProjectRoot $script:SelectorRelativePath
    $expected = Get-CanonicalSelectorBytes $Generation
    $actual = [IO.File]::ReadAllBytes($path)
    if ([Convert]::ToBase64String([byte[]]$actual) -cne [Convert]::ToBase64String([byte[]]$expected))
    {
        throw "InputIdentityDrift: selector bytes are not canonical $Generation."
    }
    return Get-FileSha256 $path
}

# 返回 SG-02 独立 selector worker 的固定脚本源码。
function Get-AtomicSelectorWorkerSource
{
    return @'
#requires -Version 7.0
param(
    [Parameter(Mandatory = $true)][string]$Target,
    [Parameter(Mandatory = $true)][string]$Next,
    [Parameter(Mandatory = $true)][string]$Backup,
    [Parameter(Mandatory = $true)][string]$Checkpoint,
    [Parameter(Mandatory = $true)][ValidateSet("BeforeReplace", "AfterReplace")][string]$Phase
)
$ErrorActionPreference = "Stop"
if ($Phase -eq "BeforeReplace")
{
    [IO.File]::WriteAllText($Checkpoint, "BeforeReplace`n", [Text.UTF8Encoding]::new($false))
    Start-Sleep -Seconds 300
    exit 0
}
[IO.File]::Replace($Next, $Target, $Backup, $true)
[IO.File]::WriteAllText($Checkpoint, "AfterReplace`n", [Text.UTF8Encoding]::new($false))
Start-Sleep -Seconds 300
'@
}

# 在 worker checkpoint 强杀独立 pwsh，并保留真实进程 raw。
function Invoke-AtomicSelectorWorker
{
    param(
        [Parameter(Mandatory = $true)][string]$WorkerPath,
        [Parameter(Mandatory = $true)][string]$Target,
        [Parameter(Mandatory = $true)][string]$Next,
        [Parameter(Mandatory = $true)][string]$Backup,
        [Parameter(Mandatory = $true)][string]$Checkpoint,
        [Parameter(Mandatory = $true)][string]$Phase,
        [Parameter(Mandatory = $true)][string]$ProcessEvidencePath
    )

    $arguments = @(
        "-NoProfile", "-File", $WorkerPath, "-Target", $Target, "-Next", $Next,
        "-Backup", $Backup, "-Checkpoint", $Checkpoint, "-Phase", $Phase
    )
    $start = [Diagnostics.ProcessStartInfo]::new("pwsh")
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
    $stdout = $null; $stderr = $null; $checkpointObserved = $false; $processId = 0
    $started = $false
    try
    {
        $started = $process.Start()
        if (-not $started) { throw "HarnessFailure: selector worker failed to start." }
        $processId = $process.Id; $script:OwnedProcessIds.Add($processId)
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        while (-not [IO.File]::Exists($Checkpoint))
        {
            if ($process.HasExited -or [DateTime]::UtcNow -ge $deadline) { break }
            Get-RemainingMilliseconds | Out-Null; Start-Sleep -Milliseconds 50
        }
        $checkpointObserved = [IO.File]::Exists($Checkpoint)
        Stop-OwnedProcess $process "UnityProcessResidue: selector worker was not running at checkpoint." -RequireRunning
        $result = [pscustomobject][ordered]@{
            Schema = "D0M2S-SelectorWorkerProcess-v1"; Phase = $Phase; ProcessId = $processId
            CheckpointObserved = $checkpointObserved; KillIssued = $true; ExitCode = [int]$process.ExitCode
            Stdout = $stdout.GetAwaiter().GetResult(); Stderr = $stderr.GetAwaiter().GetResult()
        }
        Write-FreshJson $ProcessEvidencePath $result
        return $result
    }
    finally
    {
        if ($started -and -not $process.HasExited)
        {
            Stop-OwnedProcess $process "UnityProcessResidue: selector worker survived exception cleanup."
        }
        if ($null -ne $stdout) { $stdout.GetAwaiter().GetResult() | Out-Null }
        if ($null -ne $stderr) { $stderr.GetAwaiter().GetResult() | Out-Null }
        $process.Dispose()
    }
}

# 执行 SG-02 前后 Replace 两个强杀点，并让 fixture 最终只保留 canonical B。
function Invoke-AtomicSelectorSwitch
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$SuiteRoot
    )

    $evidence = New-EvidenceDirectory "sg-02-atomic-selector-switch"
    $worker = Join-Path $SuiteRoot "Control/selector-worker.ps1"
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($worker)) | Out-Null
    Write-FreshText $worker (Get-AtomicSelectorWorkerSource)
    $target = Join-Path $ProjectRoot $script:SelectorRelativePath
    $next = $target + ".next"; $backup = $target + ".backup"
    $shaA = Assert-SelectorIdentity $ProjectRoot "generation-a"
    $bytesB = Get-CanonicalSelectorBytes "generation-b"; $shaB = Get-BytesSha256 $bytesB
    Write-FreshText $next "generation-b`n"
    $beforeCheckpoint = Join-Path $evidence "before.checkpoint"
    $before = Invoke-AtomicSelectorWorker $worker $target $next $backup $beforeCheckpoint `
        "BeforeReplace" (Join-Path $evidence "before.process.json")
    $beforeValid = $before.CheckpointObserved -and (Get-FileSha256 $target) -ceq $shaA -and
        (Get-FileSha256 $next) -ceq $shaB -and -not [IO.File]::Exists($backup)
    [IO.File]::Delete($next)
    Write-FreshText $next "generation-b`n"
    $afterCheckpoint = Join-Path $evidence "after.checkpoint"
    $after = Invoke-AtomicSelectorWorker $worker $target $next $backup $afterCheckpoint `
        "AfterReplace" (Join-Path $evidence "after.process.json")
    $afterValid = $after.CheckpointObserved -and (Get-FileSha256 $target) -ceq $shaB -and
        -not [IO.File]::Exists($next) -and (Get-FileSha256 $backup) -ceq $shaA
    [IO.File]::Delete($backup)
    $residue = @(@($next, $backup) | Where-Object { [IO.File]::Exists($_) })
    $result = [pscustomobject][ordered]@{
        Schema = "D0M2S-AtomicSelectorSwitch-v1"; RunId = $RunId
        CanonicalASha256 = $shaA; CanonicalBSha256 = $shaB
        BeforeReplace = $before; AfterReplace = $after
        BeforeValid = $beforeValid; AfterValid = $afterValid
        TransactionResidue = @($residue); FinalSelectorSha256 = Get-FileSha256 $target
        Passed = $beforeValid -and $afterValid -and $residue.Count -eq 0
    }
    Write-FreshJson (Join-Path $evidence "result.json") $result
    return $result
}

# 返回 EvidenceRoot 内路径的规范化相对表示。
function Get-EvidenceRelativePath
{
    param([Parameter(Mandatory = $true)][string]$Path)

    return [IO.Path]::GetRelativePath($script:ResolvedEvidenceRoot, $Path).Replace("\", "/")
}

# 为一次 Unity 调用创建不可复用的 invocation 与 phase 身份。
function New-InvocationIdentity
{
    param(
        [Parameter(Mandatory = $true)][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $invocationId = [Guid]::NewGuid().ToString("N")
    return [pscustomobject][ordered]@{
        CaseId = $CaseId; Label = $Label; InvocationId = $invocationId
        PhaseNonce = "phase-$invocationId"
    }
}

# 验证 tracked control 信号只指向当前 invocation 的 fresh evidence 目录并返回 canonical Base64。
function Get-TrackedSignalPathBase64
{
    param(
        [Parameter(Mandatory = $true)][ValidateSet("None", "BlockBeforeAddSource")][string]$ControlMode,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$ControlSignalPath,
        [Parameter(Mandatory = $true)][string]$EvidenceDirectory
    )

    if ($ControlMode -ceq "None")
    {
        if ($ControlSignalPath.Length -ne 0) { throw "HarnessFailure: inactive tracked control has a signal path." }
        return ""
    }
    if (-not [IO.Path]::IsPathRooted($ControlSignalPath))
    {
        throw "HarnessFailure: tracked control signal path must be absolute."
    }
    $signalPath = Resolve-NormalizedPath $ControlSignalPath
    $evidencePath = Resolve-NormalizedPath $EvidenceDirectory
    if ($signalPath -cne $ControlSignalPath -or
        (Resolve-NormalizedPath ([IO.Path]::GetDirectoryName($signalPath))) -cne $evidencePath -or
        [IO.File]::Exists($signalPath) -or [IO.Directory]::Exists($signalPath))
    {
        throw "HarnessFailure: tracked control signal path is not canonical and fresh."
    }
    return [Convert]::ToBase64String($script:Utf8NoBom.GetBytes($signalPath))
}

# 改写三个目标程序集各自的 phase stamp，并持久化可重放的 raw 编译输入证据。
function Set-CompilationPhaseStamp
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)]$Identity,
        [Parameter(Mandatory = $true)][string]$GeneratorSha256,
        [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
        [ValidateSet("None", "BlockBeforeAddSource")][string]$ControlMode = "None",
        [AllowEmptyString()][string]$ControlSignalPath = ""
    )

    $signalPathBase64 = Get-TrackedSignalPathBase64 $ControlMode $ControlSignalPath $EvidenceDirectory
    $trackedIdentity = "// D0M2S_TRACKED_PHASE_STAMP|RunId=$RunId" +
        "|CaseId=$($Identity.CaseId)|InvocationId=$($Identity.InvocationId)" +
        "|GeneratorSha256=$GeneratorSha256|PhaseNonce=$($Identity.PhaseNonce)" +
        "|ControlMode=$ControlMode|SignalPathBase64=$signalPathBase64"
    if ($trackedIdentity -cnotmatch '^// D0M2S_TRACKED_PHASE_STAMP\|RunId=D0M2S-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}\|CaseId=SG-0[1-8]\|InvocationId=[0-9a-f]{32}\|GeneratorSha256=[0-9a-f]{64}\|PhaseNonce=phase-[0-9a-f]{32}\|ControlMode=(None|BlockBeforeAddSource)\|SignalPathBase64=[A-Za-z0-9+/]*={0,2}$' -or
        [string]$Identity.PhaseNonce -cne ("phase-" + [string]$Identity.InvocationId))
    {
        throw "HarnessFailure: tracked phase-stamp identity is not canonical."
    }
    $tuple = [pscustomobject][ordered]@{
        RunId = $RunId; CaseId = [string]$Identity.CaseId
        InvocationId = [string]$Identity.InvocationId; GeneratorSha256 = $GeneratorSha256
        PhaseNonce = [string]$Identity.PhaseNonce; ControlMode = $ControlMode
        SignalPathBase64 = $signalPathBase64; SignalPath = $ControlSignalPath
    }
    $specs = @(
        @("Assets/RuntimeGenerated/D0M2SRuntimePhaseStamp.cs", "D0M2S.RuntimeHarness"),
        @("Assets/EditorGenerated/D0M2SEditorPhaseStamp.cs", "D0M2S.EditorHarness"),
        @("Assets/AutoChessHost/D0M2SAutoChessPhaseStamp.cs", "D0M2S.AutoChessHarness")
    )
    $stamps = [Collections.Generic.List[object]]::new()
    foreach ($spec in $specs)
    {
        $path = Join-Path $ProjectRoot $spec[0]
        $text = $trackedIdentity + "`nnamespace $($spec[1])`n{`n" +
            "    /// <summary>绑定本次 D0-M2S warm 编译 invocation。</summary>`n" +
            "    internal static class PhaseStamp`n    {`n" +
            "        internal const string Value = `"$($Identity.PhaseNonce)`";`n    }`n}`n"
        $bytes = $script:Utf8NoBom.GetBytes($text)
        Set-ExactFileBytes -Path $path -Bytes $bytes
        $stamps.Add([pscustomobject][ordered]@{
            RelativePath = ([IO.Path]::GetRelativePath($ProjectRoot, $path)).Replace("\", "/")
            Length = [long]$bytes.Length; Sha256 = Get-BytesSha256 $bytes
            RawBytesBase64 = [Convert]::ToBase64String($bytes); ParsedTuple = $tuple
        })
    }
    $evidencePath = Join-Path $EvidenceDirectory "phase-stamps.json"
    $snapshot = [pscustomobject][ordered]@{
        Schema = "D0M2S-TrackedPhaseStamps-v1"; CanonicalLine = $trackedIdentity
        StampCount = $stamps.Count; Stamps = $stamps.ToArray()
    }
    Write-FreshJson $evidencePath $snapshot
    return [pscustomobject]@{
        AbsolutePath = $evidencePath
        RelativePath = Get-EvidenceRelativePath $evidencePath
        ParsedTuple = $tuple
        Stamps = $snapshot.Stamps
    }
}

# 构造不含 -quit 的 Unity batchmode 探针参数。
function Get-UnityArguments
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$RawPath,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][string]$Generation,
        [Parameter(Mandatory = $true)][string]$SelectorSha256,
        [Parameter(Mandatory = $true)][string]$GeneratorSha256,
        [Parameter(Mandatory = $true)]$Identity
    )

    return @(
        "-batchmode", "-nographics", "-projectPath", $ProjectRoot,
        "-executeMethod", "GAS.Tests.D0M2S.SourceGenerator.D0M2SSourceGeneratorUnityProbe.Run",
        "-d0m2sOutput", $RawPath,
        "-d0m2sExpectedGeneration", $Generation,
        "-d0m2sExpectedSelectorSha256", $SelectorSha256,
        "-d0m2sRunId", $RunId,
        "-d0m2sCaseId", [string]$Identity.CaseId,
        "-d0m2sInvocationId", [string]$Identity.InvocationId,
        "-d0m2sExpectedGeneratorSha256", $GeneratorSha256,
        "-d0m2sExpectedPhaseNonce", [string]$Identity.PhaseNonce,
        "-logFile", $LogPath
    )
}

# 安全读取可选 Unity raw；缺失或 JSON 失败均保留为 typed 事实而不伪造成功。
function Read-OptionalUnityRaw
{
    param([Parameter(Mandatory = $true)][string]$RawPath)

    if (-not [IO.File]::Exists($RawPath))
    {
        return [pscustomobject][ordered]@{
            Exists = $false; Parseable = $false; Sha256 = ""; Length = 0L
            Passed = $false; Value = $null; ParseError = ""
        }
    }
    try
    {
        $value = [IO.File]::ReadAllText($RawPath, [Text.Encoding]::UTF8) |
            ConvertFrom-Json -Depth 64
        return [pscustomobject][ordered]@{
            Exists = $true; Parseable = $true; Sha256 = Get-FileSha256 $RawPath
            Length = [long]([IO.FileInfo]::new($RawPath)).Length
            Passed = $value.Passed -is [bool] -and [bool]$value.Passed
            Value = $value; ParseError = ""
        }
    }
    catch
    {
        return [pscustomobject][ordered]@{
            Exists = $true; Parseable = $false; Sha256 = Get-FileSha256 $RawPath
            Length = [long]([IO.FileInfo]::new($RawPath)).Length
            Passed = $false; Value = $null; ParseError = $_.Exception.Message
        }
    }
}

# 读取全部候选 RSP，并按 raw assembly output path 与 /out: 交叉绑定当前唯一候选。
function Get-BeeEvidence
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        $RawValue
    )

    $bee = Join-Path $ProjectRoot "Library/Bee"
    $groups = [Collections.Generic.List[object]]::new()
    foreach ($assemblyName in $script:TargetAssemblies)
    {
        $rawAssembly = if (-not (Test-RequiredProperties $RawValue @("Assemblies"))) { $null } else {
            @($RawValue.Assemblies | Where-Object Name -CEQ $assemblyName) | Select-Object -First 1
        }
        $outputPath = if ($null -eq $rawAssembly) { "" } else { [string]$rawAssembly.OutputPath }
        $outputName = if ($outputPath) { [IO.Path]::GetFileName($outputPath) } else { "" }
        $rows = [Collections.Generic.List[object]]::new()
        $candidates = @(Get-ChildItem -LiteralPath $bee -Recurse -Filter ($assemblyName + ".rsp") `
            -File -ErrorAction SilentlyContinue | Sort-Object FullName)
        foreach ($candidate in $candidates)
        {
            $bytes = [IO.File]::ReadAllBytes($candidate.FullName)
            $content = [Text.Encoding]::UTF8.GetString($bytes)
            $lines = @($content -split "`r?`n")
            $analyzers = @($lines | Where-Object { $_ -match '^[/-]analyzer:' -and $_ -match 'D0M2SCanaryGenerator\.dll' })
            $selectors = @($lines | Where-Object { $_ -match '^[/-]additionalfile:' -and $_ -match 'Generation\.D0M2SCanaryGenerator\.additionalfile' })
            $outputs = @($lines | Where-Object { $_ -match '^[/-]out:' })
            $matchesOutput = $outputName -and @($outputs | Where-Object {
                $_.Contains($outputName, [StringComparison]::OrdinalIgnoreCase)
            }).Count -gt 0
            $rows.Add([pscustomobject][ordered]@{
                Path = [IO.Path]::GetRelativePath($ProjectRoot, $candidate.FullName).Replace("\", "/")
                Sha256 = Get-BytesSha256 $bytes; Length = [long]$bytes.Length
                RawBytesBase64 = [Convert]::ToBase64String($bytes)
                AnalyzerArguments = $analyzers; SelectorArguments = $selectors
                OutputArguments = $outputs; MatchesOutput = [bool]$matchesOutput
                ContainsActiveRef = $content.Contains("ActiveGenerationRef", [StringComparison]::OrdinalIgnoreCase)
                ContainsStaleCanary = $content.Contains("d0m2s-stale-a", [StringComparison]::OrdinalIgnoreCase)
                ContainsLegacyGen = $content.Contains("D0M2SLegacyGeneration.gen.cs", [StringComparison]::OrdinalIgnoreCase)
            })
        }
        $current = @($rows | Where-Object { $_.MatchesOutput -and @($_.AnalyzerArguments).Count -eq 1 })
        $groups.Add([pscustomobject][ordered]@{
            Assembly = $assemblyName; OutputPath = $outputPath
            Candidates = $rows.ToArray(); CurrentCandidates = $current
        })
    }
    return $groups.ToArray()
}

# 执行一次 Unity，保留 fresh raw/log/process/RSP 与 typed invocation summary。
function Invoke-UnityTracked
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$Generation,
        [Parameter(Mandatory = $true)][string]$SelectorSha256,
        [Parameter(Mandatory = $true)][string]$GeneratorSha256,
        [Parameter(Mandatory = $true)][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $directory = New-EvidenceDirectory $Label
    $identity = New-InvocationIdentity $CaseId $Label
    $script:UnityInvocationIds.Add($identity.InvocationId)
    $rawPath = Join-Path $directory "unity.raw.json"
    $logPath = Join-Path $directory "unity.log"
    $processPath = Join-Path $directory "process.json"
    $phaseStampEvidence = Set-CompilationPhaseStamp $ProjectRoot $identity `
        $GeneratorSha256 $directory
    $arguments = Get-UnityArguments $ProjectRoot $rawPath $logPath $Generation `
        $SelectorSha256 $GeneratorSha256 $identity
    $process = Invoke-TrackedProcess -FilePath $script:ResolvedUnity -Arguments $arguments `
        -EvidencePath $processPath -TimeoutMilliseconds 300000
    $raw = Read-OptionalUnityRaw $rawPath
    $bee = Get-BeeEvidence -ProjectRoot $ProjectRoot -RawValue $raw.Value
    Write-FreshJson (Join-Path $directory "bee-rsp.json") ([pscustomobject]@{
        Schema = "D0M2S-BeeRsp-v1"; RunId = $RunId; CaseId = $CaseId
        InvocationId = $identity.InvocationId; Groups = $bee
    })
    $evidencePaths = @($phaseStampEvidence.AbsolutePath, $processPath, $logPath, $rawPath,
        (Join-Path $directory "bee-rsp.json") |
        Where-Object { [IO.File]::Exists($_) } |
        ForEach-Object { Get-EvidenceRelativePath $_ })
    $invocation = [pscustomobject][ordered]@{
        Schema = "D0M2S-UnityInvocation-v1"; RunId = $RunId; CaseId = $CaseId
        Label = $Label; InvocationId = $identity.InvocationId; PhaseNonce = $identity.PhaseNonce
        ExpectedGeneration = $Generation; ExpectedSelectorSha256 = $SelectorSha256
        ExpectedGeneratorSha256 = $GeneratorSha256; Process = $process
        Raw = $raw; Bee = $bee; PhaseStampTuple = $phaseStampEvidence.ParsedTuple
        PhaseStamps = $phaseStampEvidence.Stamps; LogExists = [IO.File]::Exists($logPath)
        LogSha256 = if ([IO.File]::Exists($logPath)) { Get-FileSha256 $logPath } else { "" }
        EvidencePaths = $evidencePaths
    }
    Write-FreshJson (Join-Path $directory "invocation.json") $invocation
    return $invocation
}

# 计算 positive invocation 的输出 DLL SHA，并拒绝缺失文件。
function Get-InvocationOutputHashes
{
    param([Parameter(Mandatory = $true)]$Invocation)

    $hashes = [ordered]@{}
    if (-not $Invocation.Raw.Parseable -or
        -not (Test-RequiredProperties $Invocation.Raw.Value @("Assemblies")))
    {
        return [pscustomobject]$hashes
    }
    foreach ($assembly in @($Invocation.Raw.Value.Assemblies))
    {
        $path = [string]$assembly.OutputPath
        $hashes[[string]$assembly.Name] = if ([IO.File]::Exists($path)) {
            Get-FileSha256 $path
        } else { "" }
    }
    return [pscustomobject]$hashes
}

# 校验 positive invocation 的 typed raw、三程序集、唯一 selector/analyzer 与当前 phase 身份。
function Test-ExactInvocation
{
    param(
        [Parameter(Mandatory = $true)]$Invocation,
        [Parameter(Mandatory = $true)][string]$ProjectRoot
    )

    $rawShapeValid = $Invocation.Raw.Parseable -and (Test-RequiredProperties $Invocation.Raw.Value @(
        "Assemblies", "RunId", "CaseId", "InvocationId", "ProcessId",
        "ObservedSelectorSha256", "ObservedGeneratorSha256"))
    $identityMatches = -not $Invocation.Process.TimedOut -and $Invocation.Process.ExitCode -eq 0 -and
        $rawShapeValid -and $Invocation.Raw.Passed
    $assembliesMatch = $identityMatches -and @($Invocation.Raw.Value.Assemblies).Count -eq 3
    $phaseStampShapeValid = Test-RequiredProperties $Invocation @("PhaseStampTuple", "PhaseStamps")
    $phaseStampsMatch = $phaseStampShapeValid -and @($Invocation.PhaseStamps).Count -eq 3
    $selectorPath = Resolve-NormalizedPath (Join-Path $ProjectRoot $script:SelectorRelativePath)
    if ($identityMatches)
    {
        $raw = $Invocation.Raw.Value
        $identityMatches = [string]$raw.RunId -ceq [string]$Invocation.RunId -and
            [string]$raw.CaseId -ceq [string]$Invocation.CaseId -and
            [string]$raw.InvocationId -ceq [string]$Invocation.InvocationId -and
            [int]$raw.ProcessId -eq [int]$Invocation.Process.ProcessId -and
            [string]$raw.ObservedSelectorSha256 -ceq [string]$Invocation.ExpectedSelectorSha256 -and
            [string]$raw.ObservedGeneratorSha256 -ceq [string]$Invocation.ExpectedGeneratorSha256
    }
    if ($phaseStampShapeValid)
    {
        $tuple = $Invocation.PhaseStampTuple
        $phaseStampsMatch = $phaseStampsMatch -and (Test-RequiredProperties $tuple @(
            "RunId", "CaseId", "InvocationId", "GeneratorSha256", "PhaseNonce",
            "ControlMode", "SignalPathBase64", "SignalPath")) -and
            [string]$tuple.RunId -ceq $RunId -and [string]$tuple.CaseId -ceq $Invocation.CaseId -and
            [string]$tuple.InvocationId -ceq $Invocation.InvocationId -and
            [string]$tuple.GeneratorSha256 -ceq $Invocation.ExpectedGeneratorSha256 -and
            [string]$tuple.PhaseNonce -ceq $Invocation.PhaseNonce -and
            [string]$tuple.ControlMode -ceq "None" -and
            [string]$tuple.SignalPathBase64 -ceq "" -and [string]$tuple.SignalPath -ceq ""
    }
    foreach ($assemblyName in $script:TargetAssemblies)
    {
        $assembly = if ($rawShapeValid) {
            @($Invocation.Raw.Value.Assemblies | Where-Object Name -CEQ $assemblyName)
        } else { @() }
        if ($assembly.Count -ne 1)
        {
            $assembliesMatch = $false; $phaseStampsMatch = $false; continue
        }
        $item = $assembly[0]
        $itemShapeValid = Test-RequiredProperties $item @(
            "SourceFiles", "RoslynAdditionalFilePaths", "Generation", "DeclaredAssembly", "SelectorSha256",
            "RunId", "CaseId", "InvocationId", "GeneratorSha256", "PhaseNonce",
            "LoadedAssemblyLocation", "OutputPath", "LoadedAssemblyMvid")
        if (-not $itemShapeValid)
        {
            $assembliesMatch = $false; $phaseStampsMatch = $false; continue
        }
        $phaseRelativePath = if ($assemblyName -ceq "com.exhard.exgas.generated.runtime") {
            "Assets/RuntimeGenerated/D0M2SRuntimePhaseStamp.cs"
        } elseif ($assemblyName -ceq "com.exhard.exgas.generated.editor") {
            "Assets/EditorGenerated/D0M2SEditorPhaseStamp.cs"
        } else { "Assets/AutoChessHost/D0M2SAutoChessPhaseStamp.cs" }
        $phaseRows = if ($phaseStampShapeValid) {
            @($Invocation.PhaseStamps | Where-Object RelativePath -CEQ $phaseRelativePath)
        } else { @() }
        $phaseFullPath = Resolve-NormalizedPath (Join-Path $ProjectRoot $phaseRelativePath)
        $phaseSourcePaths = @($item.SourceFiles | ForEach-Object {
            Resolve-NormalizedPath ([string]$_)
        })
        $phaseRowValid = $phaseRows.Count -eq 1 -and
            (Test-RequiredProperties $phaseRows[0] @("RelativePath", "Length", "Sha256", "RawBytesBase64", "ParsedTuple"))
        $phaseStampsMatch = $phaseStampsMatch -and $phaseRowValid -and
            @($phaseSourcePaths | Where-Object { $_ -ceq $phaseFullPath }).Count -eq 1 -and
            [IO.File]::Exists($phaseFullPath) -and
            (Get-FileSha256 $phaseFullPath) -ceq [string]$phaseRows[0].Sha256
        $selectors = @($item.RoslynAdditionalFilePaths | Where-Object {
            [IO.Path]::GetFileName([string]$_) -ceq [IO.Path]::GetFileName($script:SelectorRelativePath)
        })
        $assembliesMatch = $assembliesMatch -and [string]$item.Generation -ceq $Invocation.ExpectedGeneration -and
            [string]$item.DeclaredAssembly -ceq $assemblyName -and
            [string]$item.SelectorSha256 -ceq $Invocation.ExpectedSelectorSha256 -and
            [string]$item.RunId -ceq $RunId -and [string]$item.CaseId -ceq $Invocation.CaseId -and
            [string]$item.InvocationId -ceq $Invocation.InvocationId -and
            [string]$item.GeneratorSha256 -ceq $Invocation.ExpectedGeneratorSha256 -and
            [string]$item.PhaseNonce -ceq $Invocation.PhaseNonce -and $selectors.Count -eq 1 -and
            (Resolve-NormalizedPath ([string]$selectors[0])) -ceq $selectorPath -and
            (Resolve-NormalizedPath ([string]$item.LoadedAssemblyLocation)) -ceq
                (Resolve-NormalizedPath ([string]$item.OutputPath)) -and
            [string]$item.LoadedAssemblyMvid -cmatch '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
    }
    $rspMatches = $true
    foreach ($group in @($Invocation.Bee))
    {
        $current = @($group.CurrentCandidates)
        if ($current.Count -ne 1) { $rspMatches = $false; continue }
        $rspMatches = $rspMatches -and @($current[0].AnalyzerArguments).Count -eq 1 -and
            @($current[0].SelectorArguments).Count -eq 1
    }
    $outputHashes = Get-InvocationOutputHashes $Invocation
    $outputsExist = @($outputHashes.PSObject.Properties | Where-Object {
        [string]::IsNullOrWhiteSpace([string]$_.Value)
    }).Count -eq 0 -and @($outputHashes.PSObject.Properties).Count -eq 3
    $passed = $identityMatches -and $assembliesMatch -and $phaseStampsMatch -and
        $rspMatches -and $outputsExist
    $status = if ($passed) { "Passed" } elseif ($Invocation.Raw.Passed) { "RouteRejected" } else { "Inconclusive" }
    return [pscustomobject][ordered]@{
        Status = $status; Passed = $passed; IdentityMatches = $identityMatches
        AssembliesMatch = $assembliesMatch; PhaseStampsMatch = $phaseStampsMatch
        RspMatches = $rspMatches
        OutputsExist = $outputsExist; OutputHashes = $outputHashes
    }
}

# 将 negative invocation 限定为非零、精确诊断且无 Passed raw，静默接受才技术否决。
function Invoke-NegativeUnityFault
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$GeneratorSha256,
        [Parameter(Mandatory = $true)][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][string[]]$DiagnosticPatterns
    )

    $selectorPath = Join-Path $ProjectRoot $script:SelectorRelativePath
    $selectorSha = if ([IO.File]::Exists($selectorPath)) { Get-FileSha256 $selectorPath } else { "0" * 64 }
    $invocation = Invoke-UnityTracked $ProjectRoot "generation-b" $selectorSha `
        $GeneratorSha256 $CaseId $Label
    $directory = Join-Path $script:ResolvedEvidenceRoot $Label
    $logText = if ($invocation.LogExists) {
        [IO.File]::ReadAllText((Join-Path $directory "unity.log"), [Text.Encoding]::UTF8)
    } else { "" }
    $diagnosticMatched = $true
    foreach ($pattern in $DiagnosticPatterns)
    {
        $diagnosticMatched = $diagnosticMatched -and $logText -match $pattern
    }
    $passed = -not $invocation.Process.TimedOut -and $invocation.Process.ExitCode -ne 0 -and
        $diagnosticMatched -and -not $invocation.Raw.Passed
    $status = if ($passed) { "Passed" } elseif ($invocation.Raw.Passed) { "RouteRejected" } else { "Inconclusive" }
    return [pscustomobject][ordered]@{
        Status = $status; Passed = $passed; DiagnosticMatched = $diagnosticMatched
        DiagnosticPatterns = $DiagnosticPatterns; Invocation = $invocation
        SuccessfulRawObserved = [bool]$invocation.Raw.Passed
    }
}

# 在真实 generator Execute/AddSource 前 checkpoint 强杀 owned Unity/compiler tree。
function Invoke-GeneratorCompileKill
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$SelectorSha256,
        [Parameter(Mandatory = $true)][string]$GeneratorSha256
    )

    $caseId = "SG-03"; $label = "sg-03-generator-compile-kill"
    $directory = New-EvidenceDirectory $label
    $identity = New-InvocationIdentity $caseId $label
    $script:UnityInvocationIds.Add($identity.InvocationId)
    $rawPath = Join-Path $directory "unity.raw.json"
    $logPath = Join-Path $directory "unity.log"
    $signalPath = Join-Path $directory "generator-before-addsource.signal.json"
    $processPath = Join-Path $directory "process.json"
    $phaseStampEvidence = Set-CompilationPhaseStamp $ProjectRoot $identity $GeneratorSha256 `
        $directory "BlockBeforeAddSource" $signalPath
    $arguments = Get-UnityArguments $ProjectRoot $rawPath $logPath "generation-b" `
        $SelectorSha256 $GeneratorSha256 $identity
    $start = [Diagnostics.ProcessStartInfo]::new($script:ResolvedUnity)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
    $stdout = $null; $stderr = $null; $processId = 0; $signal = $null; $started = $false
    $checkpointObserved = $false; $wasRunning = $false; $killIssued = $false; $timedOut = $false
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    try
    {
        $started = $process.Start()
        if (-not $started) { throw "HarnessFailure: compile-kill Unity failed to start." }
        $processId = $process.Id; $script:OwnedProcessIds.Add($processId)
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        $signalDeadline = [DateTime]::UtcNow.AddSeconds(120)
        $signal = Read-FreshJsonWhenComplete $signalPath $process $signalDeadline
        $checkpointObserved = $null -ne $signal
        if (-not $checkpointObserved -and [DateTime]::UtcNow -ge $signalDeadline) { $timedOut = $true }
        $wasRunning = -not $process.HasExited
        $compilerPid = 0
        $compilerPidValid = $null -ne $signal -and
            [int]::TryParse([string]$signal.CompilerProcessId, [ref]$compilerPid)
        if ($compilerPidValid) { $script:OwnedProcessIds.Add($compilerPid) }
        if ($wasRunning)
        {
            Stop-OwnedProcess $process "UnityProcessResidue: compile-kill Unity tree survived." -RequireRunning
            $killIssued = $true
        }
        else { $process.WaitForExit() }
        $compilerExited = $compilerPidValid -and
            (Wait-OwnedProcessIdExit -ProcessId $compilerPid)
        $signalMatches = $null -ne $signal -and [string]$signal.Schema -ceq "D0M2S-GeneratorBeforeAddSource-v1" -and
            [string]$signal.RunId -ceq $RunId -and [string]$signal.CaseId -ceq $caseId -and
            [string]$signal.InvocationId -ceq $identity.InvocationId -and
            [string]$signal.Assembly -ceq "com.exhard.exgas.generated.runtime" -and
            [string]$signal.SelectorSha256 -ceq $SelectorSha256 -and
            [string]$signal.GeneratorSha256 -ceq $GeneratorSha256 -and
            [string]$signal.PhaseNonce -ceq $identity.PhaseNonce
        $processEvidence = [pscustomobject][ordered]@{
            Schema = "D0M2S-CompileKillProcess-v1"; RunId = $RunId; CaseId = $caseId
            InvocationId = $identity.InvocationId; PhaseNonce = $identity.PhaseNonce
            ProcessId = $processId; ExitCode = [int]$process.ExitCode; TimedOut = $timedOut
            CheckpointObserved = $checkpointObserved; WasRunningAtCheckpoint = $wasRunning
            KillIssued = $killIssued; CompilerProcessExited = $compilerExited
            DurationMilliseconds = [long]$stopwatch.ElapsedMilliseconds
            Stdout = $stdout.GetAwaiter().GetResult(); Stderr = $stderr.GetAwaiter().GetResult()
        }
        Write-FreshJson $processPath $processEvidence
        $passed = $checkpointObserved -and $wasRunning -and $killIssued -and $signalMatches -and
            $compilerExited -and -not [IO.File]::Exists($rawPath)
        $result = [pscustomobject][ordered]@{
            Schema = "D0M2S-GeneratorCompileKill-v1"; RunId = $RunId
            CaseId = $caseId; InvocationId = $identity.InvocationId; PhaseNonce = $identity.PhaseNonce
            CheckpointObserved = $checkpointObserved; SignalMatches = $signalMatches
            SuccessfulRawAbsent = -not [IO.File]::Exists($rawPath); Process = $processEvidence
            Signal = $signal; Passed = $passed; Status = if ($passed) { "Passed" } else { "Inconclusive" }
            EvidencePaths = @(
                $phaseStampEvidence.RelativePath
                Get-EvidenceRelativePath $signalPath
                Get-EvidenceRelativePath $logPath
                Get-EvidenceRelativePath $processPath
            )
        }
        Write-FreshJson (Join-Path $directory "result.json") $result
        return $result
    }
    finally
    {
        if ($started -and -not $process.HasExited)
        {
            Stop-OwnedProcess $process "UnityProcessResidue: compile-kill Unity survived exception cleanup."
        }
        if ($null -ne $stdout) { $stdout.GetAwaiter().GetResult() | Out-Null }
        if ($null -ne $stderr) { $stderr.GetAwaiter().GetResult() | Out-Null }
        $stopwatch.Stop(); $process.Dispose()
    }
}

# 在 warm fixture 中预置 ActiveRef A、stale generated/Bee/RSP/cache canary，供 SG-05 与 B 对撞。
function New-SpecificXControls
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)]$ColdInvocation
    )

    $evidenceDirectory = New-EvidenceDirectory "sg-05-specific-x-controls"
    $activeDirectory = Join-Path $ProjectRoot "ProjectSettings/GasCodeGen"
    [IO.Directory]::CreateDirectory($activeDirectory) | Out-Null
    $activeRef = Join-Path $activeDirectory "ActiveGenerationRef.json"
    Write-FreshText $activeRef "{`n  `"GenerationId`": `"generation-a`",`n  `"Role`": `"audit-only`"`n}`n"
    $staleRoot = Join-Path $ProjectRoot "Library/Bee/D0M2SStaleA"
    [IO.Directory]::CreateDirectory($staleRoot) | Out-Null
    $generated = Join-Path $staleRoot "d0m2s-stale-a.g.cs"
    Write-FreshText $generated "// generation-a stale generated canary`n"
    foreach ($assemblyName in $script:TargetAssemblies)
    {
        $output = @($ColdInvocation.Raw.Value.Assemblies | Where-Object Name -CEQ $assemblyName)[0].OutputPath
        $rsp = Join-Path $staleRoot ($assemblyName + ".rsp")
        Write-FreshText $rsp ("/out:`"$output`"`n/additionalfile:ActiveGenerationRef.json`n" +
            "/additionalfile:d0m2s-stale-a.additionalfile`n")
    }
    $cache = Join-Path $ProjectRoot "Library/D0M2SStaleCache/generation-a/cache.txt"
    Write-FreshText $cache "generation-a stale cache canary`n"
    $result = [pscustomobject][ordered]@{
        Schema = "D0M2S-SpecificXControls-v1"; RunId = $RunId
        ActiveRefPath = $activeRef; ActiveRefSha256 = Get-FileSha256 $activeRef
        StaleGeneratedPath = $generated; StaleGeneratedSha256 = Get-FileSha256 $generated
        StaleRspRoot = $staleRoot; StaleCachePath = $cache; StaleCacheSha256 = Get-FileSha256 $cache
    }
    Write-FreshJson (Join-Path $evidenceDirectory "controls.json") $result
    return $result
}

# 证明 ActiveRef/stale A canary 未进入 B 的当前 CompilationPipeline 与唯一 output-bound RSP。
function Test-SpecificXPositive
{
    param(
        [Parameter(Mandatory = $true)]$RestartInvocation,
        [Parameter(Mandatory = $true)]$RestartEvaluation,
        [Parameter(Mandatory = $true)]$Controls
    )

    $forbidden = $false
    if ($RestartInvocation.Raw.Parseable)
    {
        foreach ($assembly in @($RestartInvocation.Raw.Value.Assemblies))
        {
            foreach ($value in @($assembly.SourceFiles) + @($assembly.RoslynAdditionalFilePaths) +
                @($assembly.CompiledAssemblyReferences) + @($assembly.Defines))
            {
                if ([string]$value -match 'ActiveGenerationRef|D0M2SStaleA|d0m2s-stale-a')
                {
                    $forbidden = $true
                }
            }
        }
    }
    foreach ($group in @($RestartInvocation.Bee))
    {
        foreach ($candidate in @($group.CurrentCandidates))
        {
            if ($candidate.ContainsActiveRef -or $candidate.ContainsStaleCanary) { $forbidden = $true }
        }
    }
    $activeUnchanged = [IO.File]::Exists($Controls.ActiveRefPath) -and
        (Get-FileSha256 $Controls.ActiveRefPath) -ceq $Controls.ActiveRefSha256
    $staleDispositionKnown = (-not [IO.File]::Exists($Controls.StaleGeneratedPath)) -or
        (Get-FileSha256 $Controls.StaleGeneratedPath) -ceq $Controls.StaleGeneratedSha256
    $passed = $RestartEvaluation.Passed -and -not $forbidden -and $activeUnchanged -and $staleDispositionKnown
    $status = if ($passed) { "Passed" } elseif ($RestartEvaluation.Status -eq "RouteRejected" -or $forbidden) {
        "RouteRejected"
    } else { "Inconclusive" }
    return [pscustomobject][ordered]@{
        Status = $status; Passed = $passed; ForbiddenACompilerInputObserved = $forbidden
        ActiveRefUnchanged = $activeUnchanged; StaleDispositionKnown = $staleDispositionKnown
        StaleGeneratedStillExists = [IO.File]::Exists($Controls.StaleGeneratedPath)
        StaleCacheStillExists = [IO.File]::Exists($Controls.StaleCachePath)
        Controls = $Controls
    }
}

# 在 warm B 下移除 selector，要求命中缺失 selector 稳定诊断并精确恢复原 bytes。
function Invoke-MissingSelectorFault
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$SuiteRoot,
        [Parameter(Mandatory = $true)][string]$GeneratorSha256
    )

    $label = "sg-04-selector-missing"; $path = Join-Path $ProjectRoot $script:SelectorRelativePath
    $before = [IO.File]::ReadAllBytes($path); $beforeSha = Get-BytesSha256 $before
    $backup = Join-Path $SuiteRoot "Control/selector-missing.backup"
    [IO.File]::Move($path, $backup)
    try { $fault = Invoke-NegativeUnityFault $ProjectRoot $GeneratorSha256 "SG-04" $label @("D0M2SSG001") }
    finally { if ([IO.File]::Exists($backup)) { [IO.File]::Move($backup, $path) } }
    $restored = [IO.File]::Exists($path) -and (Get-FileSha256 $path) -ceq $beforeSha
    $record = [pscustomobject][ordered]@{
        Schema = "D0M2S-NegativeFault-v1"; Fault = "SelectorMissing"
        BeforeSha256 = $beforeSha; Restored = $restored; Result = $fault
    }
    Write-FreshJson (Join-Path $script:ResolvedEvidenceRoot "$label/fault.json") $record
    if (-not $restored) { throw "InputIdentityDrift: selector missing fault restoration failed." }
    return $record
}

# 在 warm B 下写入非 canonical selector bytes，要求稳定诊断并精确恢复。
function Invoke-InvalidSelectorFault
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$GeneratorSha256
    )

    $label = "sg-04-selector-invalid"; $path = Join-Path $ProjectRoot $script:SelectorRelativePath
    $before = [IO.File]::ReadAllBytes($path); $beforeSha = Get-BytesSha256 $before
    $invalid = $script:Utf8NoBom.GetBytes("generation-b `n")
    Set-ExactFileBytes $path $invalid
    try { $fault = Invoke-NegativeUnityFault $ProjectRoot $GeneratorSha256 "SG-04" $label @("D0M2SSG003") }
    finally { Set-ExactFileBytes $path $before }
    $restored = (Get-FileSha256 $path) -ceq $beforeSha
    $record = [pscustomobject][ordered]@{
        Schema = "D0M2S-NegativeFault-v1"; Fault = "SelectorInvalidCanonicalBytes"
        BeforeSha256 = $beforeSha; FaultSha256 = Get-BytesSha256 $invalid
        FaultBytesBase64 = [Convert]::ToBase64String($invalid); Restored = $restored; Result = $fault
    }
    Write-FreshJson (Join-Path $script:ResolvedEvidenceRoot "$label/fault.json") $record
    if (-not $restored) { throw "InputIdentityDrift: invalid selector restoration failed." }
    return $record
}

# 在 warm B 下损坏 generator DLL，要求对应 loader/analyzer 诊断并精确恢复。
function Invoke-CorruptGeneratorFault
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$GeneratorSha256
    )

    $label = "sg-04-generator-corrupt"; $path = Join-Path $ProjectRoot $script:GeneratorRelativePath
    $before = [IO.File]::ReadAllBytes($path); $beforeSha = Get-BytesSha256 $before
    $corrupt = $script:Utf8NoBom.GetBytes("D0M2S-corrupt-generator-dll`n")
    Set-ExactFileBytes $path $corrupt
    try
    {
        $fault = Invoke-NegativeUnityFault $ProjectRoot $GeneratorSha256 "SG-04" $label `
            @("D0M2SCanaryGenerator", "(?i)(analyzer|assembly|image|load|invalid|corrupt|PE)")
    }
    finally { Set-ExactFileBytes $path $before }
    $restored = (Get-FileSha256 $path) -ceq $beforeSha
    $record = [pscustomobject][ordered]@{
        Schema = "D0M2S-NegativeFault-v1"; Fault = "GeneratorDllCorrupt"
        BeforeSha256 = $beforeSha; FaultSha256 = Get-BytesSha256 $corrupt
        FaultBytesBase64 = [Convert]::ToBase64String($corrupt); Restored = $restored; Result = $fault
    }
    Write-FreshJson (Join-Path $script:ResolvedEvidenceRoot "$label/fault.json") $record
    if (-not $restored) { throw "InputIdentityDrift: generator restoration failed." }
    return $record
}

# 注入第二个同名 selector A，要求 duplicate authority 以稳定诊断 fail closed。
function Invoke-DuplicateSelectorFault
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$GeneratorSha256
    )

    $label = "sg-06-duplicate-selector"; $directory = Join-Path $ProjectRoot "Assets/CompetingAuthority"
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $path = Join-Path $directory ([IO.Path]::GetFileName($script:SelectorRelativePath))
    Write-FreshText $path "generation-a`n"
    try { $fault = Invoke-NegativeUnityFault $ProjectRoot $GeneratorSha256 "SG-06" $label @("D0M2SSG002") }
    finally
    {
        if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
        if ([IO.File]::Exists($path + ".meta")) { [IO.File]::Delete($path + ".meta") }
    }
    $record = [pscustomobject][ordered]@{
        Schema = "D0M2S-NegativeFault-v1"; Fault = "DuplicateSameNameAdditionalFile"
        DuplicateSha256 = Get-BytesSha256 (Get-CanonicalSelectorBytes "generation-a")
        Removed = -not [IO.File]::Exists($path); Result = $fault
    }
    Write-FreshJson (Join-Path $script:ResolvedEvidenceRoot "$label/fault.json") $record
    return $record
}

# 注入 active legacy .gen.cs A 与 generator marker 同名类型，要求 CS0101 fail closed。
function Invoke-LegacyGeneratedSourceFault
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$GeneratorSha256
    )

    $label = "sg-06-legacy-active-gen"; $path = Join-Path $ProjectRoot `
        "Assets/RuntimeGenerated/D0M2SLegacyGeneration.gen.cs"
    $text = "namespace D0M2S.Generated.Runtime`n{`n" +
        "    /// <summary>故障注入的 generation-a legacy active marker。</summary>`n" +
        "    public static class GenerationMarker`n    {`n" +
        "        public const string GenerationId = `"generation-a`";`n    }`n}`n"
    Write-FreshText $path $text
    try
    {
        $fault = Invoke-NegativeUnityFault $ProjectRoot $GeneratorSha256 "SG-06" $label `
            @("CS0101", "GenerationMarker")
    }
    finally
    {
        if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
        if ([IO.File]::Exists($path + ".meta")) { [IO.File]::Delete($path + ".meta") }
    }
    $record = [pscustomobject][ordered]@{
        Schema = "D0M2S-NegativeFault-v1"; Fault = "LegacyActiveGeneratedSourceA"
        LegacySha256 = Get-BytesSha256 $script:Utf8NoBom.GetBytes($text)
        Removed = -not [IO.File]::Exists($path); Result = $fault
    }
    Write-FreshJson (Join-Path $script:ResolvedEvidenceRoot "$label/fault.json") $record
    return $record
}

# 实测 hardlink/reparse 拒绝与外部 canary 保持，随后恢复 suite 普通树。
function Test-BoundaryControls
{
    param([Parameter(Mandatory = $true)][string]$SuiteRoot)

    $directory = New-EvidenceDirectory "sg-07-boundary-cleanup"
    $script:ExternalRoot = New-OwnedRoot $script:ExternalPrefix $script:ExternalOwnerSentinel
    $externalFile = Join-Path $script:ExternalRoot "external-target.bin"
    Write-FreshText $externalFile "D0M2S external hardlink target`n"
    $externalSha = Get-FileSha256 $externalFile
    $externalDirectory = Join-Path $script:ExternalRoot "junction-target"
    [IO.Directory]::CreateDirectory($externalDirectory) | Out-Null
    $junctionCanary = Join-Path $externalDirectory "canary.txt"
    Write-FreshText $junctionCanary "D0M2S external reparse target`n"
    $junctionSha = Get-FileSha256 $junctionCanary
    $boundaryRoot = Join-Path $SuiteRoot "BoundaryControls"
    [IO.Directory]::CreateDirectory($boundaryRoot) | Out-Null
    $hardlinkAlias = Join-Path $boundaryRoot "hardlink-alias.bin"
    [D0M2SNativeFileInfo]::CreateHardLink($hardlinkAlias, $externalFile)
    $hardlinkRejected = $false
    try { Assert-OrdinaryTree $SuiteRoot }
    catch { $hardlinkRejected = $_.Exception.Message -match "hardlink" }
    [IO.File]::Delete($hardlinkAlias)
    $reparseAlias = Join-Path $boundaryRoot "junction-alias"
    New-Item -ItemType Junction -Path $reparseAlias -Target $externalDirectory -ErrorAction Stop | Out-Null
    $reparseRejected = $false
    try { Assert-OrdinaryTree $SuiteRoot }
    catch { $reparseRejected = $_.Exception.Message -match "reparse point" }
    [IO.Directory]::Delete($reparseAlias, $false)
    $targetsUnchanged = (Get-FileSha256 $externalFile) -ceq $externalSha -and
        (Get-FileSha256 $junctionCanary) -ceq $junctionSha
    Assert-OrdinaryTree $SuiteRoot
    $result = [pscustomobject][ordered]@{
        Schema = "D0M2S-BoundaryControls-v1"; RunId = $RunId
        HardlinkRejected = $hardlinkRejected; ReparseRejected = $reparseRejected
        ExternalTargetsUnchanged = $targetsUnchanged
        ExternalFileSha256 = $externalSha; JunctionCanarySha256 = $junctionSha
        Passed = $hardlinkRejected -and $reparseRejected -and $targetsUnchanged
    }
    Write-FreshJson (Join-Path $directory "boundary.json") $result
    return $result
}

# 返回仍存活的本 harness 已登记 PID 集合。
function Get-LiveOwnedProcessIds
{
    return @($script:OwnedProcessIds | Sort-Object -Unique | Where-Object {
        $null -ne (Get-Process -Id $_ -ErrorAction SilentlyContinue)
    })
}

# 创建固定 SG-01..SG-08 case 集，确保异常路径也不丢 case identity。
function New-DefaultCases
{
    $specs = @(
        @("SG-01", "ColdAIdentity"),
        @("SG-02", "AtomicSelectorSwitch"),
        @("SG-03", "GeneratorKillRestart"),
        @("SG-04", "MissingCorruptInputs"),
        @("SG-05", "SourceGeneratorSpecificXPositive"),
        @("SG-06", "CompetingAuthorityFailClosed"),
        @("SG-07", "BoundaryCleanup"),
        @("SG-08", "EvidenceClosure")
    )
    return @($specs | ForEach-Object {
        [pscustomobject][ordered]@{
            CaseId = $_[0]; Name = $_[1]; Status = "NotRun"; Reason = "EvidenceIncomplete"
            EvidencePaths = @(); Evidence = [pscustomobject]@{}
        }
    })
}

# 更新唯一 case 的 typed status/reason/evidence，拒绝未知或重复 CaseId。
function Set-CaseResult
{
    param(
        [Parameter(Mandatory = $true)][string]$CaseId,
        [Parameter(Mandatory = $true)][ValidateSet("Passed", "Failed", "RouteRejected", "Inconclusive", "NotRun")][string]$Status,
        [Parameter(Mandatory = $true)][string]$Reason,
        [Parameter(Mandatory = $true)][string[]]$EvidencePaths,
        [Parameter(Mandatory = $true)]$Evidence
    )

    $matches = @($script:Cases | Where-Object CaseId -CEQ $CaseId)
    if ($matches.Count -ne 1) { throw "HarnessFailure: case identity is not unique: $CaseId" }
    $matches[0].Status = if ($Status -ceq "RouteRejected") { "Failed" } else { $Status }
    $matches[0].Reason = $Reason
    $matches[0].EvidencePaths = @($EvidencePaths | Sort-Object -Unique)
    $matches[0].Evidence = $Evidence
}

# 收集指定 case 前缀下已存在的全部相对证据路径。
function Get-CaseEvidencePaths
{
    param([Parameter(Mandatory = $true)][string]$CasePrefix)

    return @([IO.Directory]::EnumerateFiles(
        $script:ResolvedEvidenceRoot, "*", [IO.SearchOption]::AllDirectories) |
        ForEach-Object { Get-EvidenceRelativePath $_ } |
        Where-Object { $_.StartsWith($CasePrefix, [StringComparison]::Ordinal) } |
        Sort-Object)
}

# 合并多分支结果，技术否决优先于 Inconclusive，全部通过才 Passed。
function Get-CompositeStatus
{
    param([Parameter(Mandatory = $true)][object[]]$Results)

    if (@($Results | Where-Object { $_.Status -ceq "RouteRejected" }).Count -gt 0)
    {
        return "RouteRejected"
    }
    if (@($Results | Where-Object { $_.Status -cne "Passed" }).Count -gt 0)
    {
        return "Inconclusive"
    }
    return "Passed"
}

# 写出 child prior-inventory，供 central SG-08 最终 raw closure 复算。
function Complete-ChildEvidence
{
    $directory = New-EvidenceDirectory "sg-08-child-inventory"
    $prior = @(Get-TreeInventory $script:ResolvedEvidenceRoot)
    $closure = [pscustomobject][ordered]@{
        Schema = "D0M2S-ChildEvidenceInventory-v1"; RunId = $RunId
        EvidenceRoot = $script:ResolvedEvidenceRoot.Replace("\", "/")
        PriorFiles = $prior; PriorFileCount = $prior.Count
        CentralClosureRequired = $true; Passed = $true
    }
    $path = Join-Path $directory "inventory.json"
    Write-FreshJson $path $closure
    return [pscustomobject][ordered]@{
        Path = Get-EvidenceRelativePath $path
        Prior = $closure
        AllFiles = @(Get-TreeInventory $script:ResolvedEvidenceRoot)
    }
}

# 计算 SG-01..SG-07 child 总状态，SG-08 留给 central 不参与 child 技术判定。
function Get-ChildStatus
{
    $technical = @($script:Cases | Where-Object CaseId -CNE "SG-08")
    if (@($technical | Where-Object Status -CEQ "Failed").Count -gt 0)
    {
        $case = $technical | Where-Object Status -CEQ "Failed" | Select-Object -First 1
        return [pscustomobject]@{ Status = "RouteRejected"; Reason = [string]$case.Reason }
    }
    if (@($technical | Where-Object Status -CNE "Passed").Count -gt 0)
    {
        $case = $technical | Where-Object Status -CNE "Passed" | Select-Object -First 1
        return [pscustomobject]@{ Status = "Inconclusive"; Reason = [string]$case.Reason }
    }
    return [pscustomobject]@{ Status = "Passed"; Reason = "None" }
}

$script:ResolvedEvidenceRoot = Resolve-InputPath $EvidenceRoot
$resolvedOutput = Resolve-InputPath $OutputPath
if ([IO.File]::Exists($resolvedOutput) -or [IO.Directory]::Exists($resolvedOutput))
{
    throw "OutputPath must be fresh: $resolvedOutput"
}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolvedOutput)) | Out-Null
if ([IO.File]::Exists($script:ResolvedEvidenceRoot)) { throw "EvidenceRoot must be a directory." }
if (-not [IO.Directory]::Exists($script:ResolvedEvidenceRoot))
{
    [IO.Directory]::CreateDirectory($script:ResolvedEvidenceRoot) | Out-Null
}
if (([IO.File]::GetAttributes($script:ResolvedEvidenceRoot) -band [IO.FileAttributes]::ReparsePoint) -ne 0)
{
    throw "FixtureBoundaryViolation: EvidenceRoot is a reparse point."
}

$script:Cases = New-DefaultCases
$script:ExternalRoot = $null
$suiteRoot = $null; $fixture = $null; $boundary = $null
$cleanupErrors = [Collections.Generic.List[string]]::new()
$cleanupStatus = "NotStarted"; $caughtError = ""
$resolvedUnity = ""; $unityVersion = ""; $unitySha = ""
$measuredCommonSelector = ""
$inputs = [ordered]@{
    UnityPath = ""; UnitySha256 = ""; UnityVersion = ""
    GeneratorSourceSha256 = ""; GeneratorProjectSha256 = ""; GeneratorAssemblySha256 = ""
    GeneratorAssemblyLength = 0L; BaseFixtureDigest = ""; OverlayDigest = ""
    SelectorRelativePath = $script:SelectorRelativePath
    CanonicalASha256 = Get-BytesSha256 (Get-CanonicalSelectorBytes "generation-a")
    CanonicalBSha256 = Get-BytesSha256 (Get-CanonicalSelectorBytes "generation-b")
}

try
{
    if (-not [IO.Path]::IsPathRooted($UnityPath))
    {
        throw "UnityIdentityMismatch: UnityPath must be absolute."
    }
    $resolvedUnity = Resolve-NormalizedPath $UnityPath
    if (-not [IO.File]::Exists($resolvedUnity)) { throw "UnityIdentityMismatch: Unity is missing." }
    $unitySha = Get-FileSha256 $resolvedUnity
    if ($unitySha -cne "611b5785ece71684351029e7f64857f49949f87543a8de1ea45490eff8535262")
    {
        throw "UnityIdentityMismatch: Unity executable SHA drift."
    }
    $unityVersion = (Get-Item -LiteralPath $resolvedUnity).VersionInfo.ProductVersion
    if (-not $unityVersion.StartsWith("6000.3.14f1", [StringComparison]::Ordinal))
    {
        throw "UnityIdentityMismatch: Unity version drift."
    }
    $script:ResolvedUnity = $resolvedUnity
    $inputs.UnityPath = $resolvedUnity; $inputs.UnitySha256 = $unitySha; $inputs.UnityVersion = $unityVersion

    $suiteRoot = New-OwnedRoot $script:SuitePrefix $script:OwnerSentinel
    $fixture = New-Fixture $suiteRoot
    $inputs.GeneratorSourceSha256 = $fixture.Generator.SourceSha256
    $inputs.GeneratorProjectSha256 = $fixture.Generator.ProjectSha256
    $inputs.GeneratorAssemblySha256 = $fixture.Generator.AssemblySha256
    $inputs.GeneratorAssemblyLength = $fixture.Generator.AssemblyLength
    $inputs.BaseFixtureDigest = $fixture.BaseFixtureDigest; $inputs.OverlayDigest = $fixture.OverlayDigest
    $selectorA = Assert-SelectorIdentity $fixture.ProjectRoot "generation-a"

    $cold = Invoke-UnityTracked $fixture.ProjectRoot "generation-a" $selectorA `
        $fixture.Generator.AssemblySha256 "SG-01" "sg-01-cold-a"
    $coldEvaluation = Test-ExactInvocation $cold $fixture.ProjectRoot
    $coldReason = if ($coldEvaluation.Passed) { "None" } elseif ($coldEvaluation.Status -ceq "RouteRejected") {
        "ColdBaselineFailed"
    } else { "EvidenceIncomplete" }
    Set-CaseResult "SG-01" $coldEvaluation.Status $coldReason `
        (Get-CaseEvidencePaths "sg-01-") ([pscustomobject]@{
            Evaluation = $coldEvaluation; InvocationId = $cold.InvocationId
        })
    if (-not $coldEvaluation.Passed) { throw "ExperimentPrerequisite: SG-01 did not pass." }

    $atomic = Invoke-AtomicSelectorSwitch $fixture.ProjectRoot $suiteRoot
    $atomicStatus = if ($atomic.Passed) { "Passed" } else { "Inconclusive" }
    Set-CaseResult "SG-02" $atomicStatus $(if ($atomic.Passed) { "None" } else { "EvidenceIncomplete" }) `
        (Get-CaseEvidencePaths "sg-02-") $atomic
    if (-not $atomic.Passed) { throw "ExperimentPrerequisite: SG-02 did not pass." }
    $selectorB = Assert-SelectorIdentity $fixture.ProjectRoot "generation-b"

    $specificControls = New-SpecificXControls $fixture.ProjectRoot $cold
    $kill = Invoke-GeneratorCompileKill $fixture.ProjectRoot $selectorB $fixture.Generator.AssemblySha256
    if (-not $kill.Passed)
    {
        Set-CaseResult "SG-03" "Inconclusive" "CompileWindowCheckpointIncomplete" `
            (Get-CaseEvidencePaths "sg-03-") $kill
        throw "ExperimentPrerequisite: SG-03 compile kill did not pass."
    }
    $restart = Invoke-UnityTracked $fixture.ProjectRoot "generation-b" $selectorB `
        $fixture.Generator.AssemblySha256 "SG-03" "sg-03-restart-b"
    $restartEvaluation = Test-ExactInvocation $restart $fixture.ProjectRoot
    $outputChanged = $restartEvaluation.Passed
    foreach ($assemblyName in $script:TargetAssemblies)
    {
        $aProperty = $coldEvaluation.OutputHashes.PSObject.Properties[$assemblyName]
        $bProperty = $restartEvaluation.OutputHashes.PSObject.Properties[$assemblyName]
        $aHash = if ($null -eq $aProperty) { "" } else { [string]$aProperty.Value }
        $bHash = if ($null -eq $bProperty) { "" } else { [string]$bProperty.Value }
        $outputChanged = $outputChanged -and $aHash -and $bHash -and $aHash -cne $bHash
    }
    $sg03Passed = $kill.Passed -and $restartEvaluation.Passed -and $outputChanged
    $sg03Status = if ($sg03Passed) { "Passed" } elseif ($restartEvaluation.Status -ceq "RouteRejected" -or
        ($restart.Raw.Passed -and -not $outputChanged)) { "RouteRejected" } else { "Inconclusive" }
    $sg03Reason = if ($sg03Passed) { "None" } elseif ($sg03Status -ceq "RouteRejected") {
        "CompileKillRecoveryFailed"
    } else { "EvidenceIncomplete" }
    Set-CaseResult "SG-03" $sg03Status $sg03Reason (Get-CaseEvidencePaths "sg-03-") `
        ([pscustomobject]@{
            Kill = $kill; RestartEvaluation = $restartEvaluation; OutputChangedForAllAssemblies = $outputChanged
        })
    if (-not $sg03Passed) { throw "ExperimentPrerequisite: SG-03 restart did not pass." }
    $measuredCommonSelector = $script:SelectorRelativePath

    $specific = Test-SpecificXPositive $restart $restartEvaluation $specificControls
    $specificReason = if ($specific.Passed) { "None" } elseif ($specific.Status -ceq "RouteRejected") {
        "AuditOrCacheSelected"
    } else { "EvidenceIncomplete" }
    $specificPaths = @((Get-CaseEvidencePaths "sg-03-") + (Get-CaseEvidencePaths "sg-05-"))
    Set-CaseResult "SG-05" $specific.Status $specificReason $specificPaths $specific

    $missing = Invoke-MissingSelectorFault $fixture.ProjectRoot $suiteRoot $fixture.Generator.AssemblySha256
    $invalid = Invoke-InvalidSelectorFault $fixture.ProjectRoot $fixture.Generator.AssemblySha256
    $corrupt = Invoke-CorruptGeneratorFault $fixture.ProjectRoot $fixture.Generator.AssemblySha256
    $sg04Branches = @($missing.Result, $invalid.Result, $corrupt.Result)
    $sg04Status = Get-CompositeStatus $sg04Branches
    $sg04Reason = if ($sg04Status -ceq "Passed") { "None" }
        elseif ($sg04Status -cne "RouteRejected") { "EvidenceIncomplete" }
        elseif ($missing.Result.Status -ceq "RouteRejected") { "MissingSelectorAccepted" }
        elseif ($invalid.Result.Status -ceq "RouteRejected") { "CorruptSelectorAccepted" }
        else { "GeneratorIdentityDriftAccepted" }
    Set-CaseResult "SG-04" $sg04Status $sg04Reason (Get-CaseEvidencePaths "sg-04-") `
        ([pscustomobject]@{ Missing = $missing; Invalid = $invalid; GeneratorCorrupt = $corrupt })

    $duplicate = Invoke-DuplicateSelectorFault $fixture.ProjectRoot $fixture.Generator.AssemblySha256
    $legacy = Invoke-LegacyGeneratedSourceFault $fixture.ProjectRoot $fixture.Generator.AssemblySha256
    $sg06Branches = @($duplicate.Result, $legacy.Result)
    $sg06Status = Get-CompositeStatus $sg06Branches
    $sg06Reason = if ($sg06Status -ceq "Passed") { "None" }
        elseif ($sg06Status -cne "RouteRejected") { "EvidenceIncomplete" }
        elseif ($duplicate.Result.Status -ceq "RouteRejected") { "DuplicateSelectorAccepted" }
        else { "LegacyGeneratedSourceAccepted" }
    Set-CaseResult "SG-06" $sg06Status $sg06Reason (Get-CaseEvidencePaths "sg-06-") `
        ([pscustomobject]@{ Duplicate = $duplicate; Legacy = $legacy })

    $boundary = Test-BoundaryControls $suiteRoot
}
catch
{
    $caughtError = $_.Exception.Message
    $errorPath = Join-Path $script:ResolvedEvidenceRoot "harness-error.json"
    if (-not [IO.File]::Exists($errorPath))
    {
        Write-FreshJson $errorPath ([pscustomobject][ordered]@{
            Schema = "D0M2S-HarnessError-v1"; RunId = $RunId; Message = $caughtError
            Type = $_.Exception.GetType().FullName
            PositionMessage = [string]$_.InvocationInfo.PositionMessage
            ScriptStackTrace = [string]$_.ScriptStackTrace
        })
    }
}
finally
{
    if ($KeepFixture)
    {
        $cleanupStatus = "Kept"
    }
    else
    {
        if ($null -ne $suiteRoot -and [IO.Directory]::Exists($suiteRoot))
        {
            try { Remove-OwnedRoot $suiteRoot $script:SuitePrefix $script:OwnerSentinel }
            catch { $cleanupErrors.Add($_.Exception.Message) }
        }
        if ($null -ne $script:ExternalRoot -and [IO.Directory]::Exists($script:ExternalRoot))
        {
            try { Remove-OwnedRoot $script:ExternalRoot $script:ExternalPrefix $script:ExternalOwnerSentinel }
            catch { $cleanupErrors.Add($_.Exception.Message) }
        }
        $cleanupStatus = if ($cleanupErrors.Count -eq 0) { "Passed" } else { "Failed" }
    }
    $livePids = @(Get-LiveOwnedProcessIds)
    if ($livePids.Count -gt 0)
    {
        $cleanupErrors.Add("Owned process residue: $($livePids -join ',')")
        $cleanupStatus = "Failed"
    }
    $cleanupDirectory = Join-Path $script:ResolvedEvidenceRoot "sg-07-boundary-cleanup"
    if (-not [IO.Directory]::Exists($cleanupDirectory))
    {
        [IO.Directory]::CreateDirectory($cleanupDirectory) | Out-Null
    }
    $cleanup = [pscustomobject][ordered]@{
        Schema = "D0M2S-Cleanup-v1"; RunId = $RunId; Requested = -not $KeepFixture
        Status = $cleanupStatus; SuiteRoot = if ($KeepFixture) { $suiteRoot } else { "<deleted>" }
        ExternalRoot = if ($KeepFixture) { $script:ExternalRoot } else { "<deleted>" }
        OwnedProcessIds = @($script:OwnedProcessIds | Sort-Object -Unique)
        LiveOwnedProcessIds = $livePids; Errors = $cleanupErrors.ToArray()
        ResidualSuiteRoot = $null -ne $suiteRoot -and [IO.Directory]::Exists($suiteRoot)
        ResidualExternalRoot = $null -ne $script:ExternalRoot -and [IO.Directory]::Exists($script:ExternalRoot)
    }
    $cleanupPath = Join-Path $cleanupDirectory "cleanup.json"
    if (-not [IO.File]::Exists($cleanupPath)) { Write-FreshJson $cleanupPath $cleanup }
    $boundaryPassed = $null -ne $boundary -and $boundary.Passed -and $cleanupStatus -ceq "Passed" -and
        -not $cleanup.ResidualSuiteRoot -and -not $cleanup.ResidualExternalRoot -and $livePids.Count -eq 0
    Set-CaseResult "SG-07" $(if ($boundaryPassed) { "Passed" } else { "Inconclusive" }) `
        $(if ($boundaryPassed) { "None" } elseif ($KeepFixture) { "CleanupSkipped" } else { "BoundaryOrCleanupIncomplete" }) `
        (Get-CaseEvidencePaths "sg-07-") ([pscustomobject]@{ Boundary = $boundary; Cleanup = $cleanup })
}

$childClosure = Complete-ChildEvidence
Set-CaseResult "SG-08" "NotRun" "CentralEvidenceClosurePending" @($childClosure.Path) `
    ([pscustomobject]@{
        PriorFileCount = $childClosure.Prior.PriorFileCount; CentralClosureRequired = $true
    })
$childStatus = Get-ChildStatus
if ($cleanupStatus -cne "Passed" -and -not $KeepFixture)
{
    $childStatus = [pscustomobject]@{ Status = "Inconclusive"; Reason = "CleanupFailure" }
}

$roles = @(
    [pscustomobject]@{ Path = $script:SelectorRelativePath; Role = "Authority"; UnityConsumed = $true; Mutable = $true },
    [pscustomobject]@{ Path = $script:GeneratorRelativePath; Role = "Authority"; UnityConsumed = $true; Mutable = $false },
    [pscustomobject]@{ Path = "ProjectSettings/GasCodeGen/ActiveGenerationRef.json"; Role = "DerivedAudit"; UnityConsumed = $false; Mutable = $true },
    [pscustomobject]@{ Path = "Library/ScriptAssemblies/**"; Role = "Derived"; UnityConsumed = $true; Mutable = $true },
    [pscustomobject]@{ Path = "Library/Bee/**"; Role = "Cache"; UnityConsumed = $true; Mutable = $true },
    [pscustomobject]@{ Path = "Assets/**/D0M2S*PhaseStamp.cs"; Role = "HarnessControl"; UnityConsumed = $true; Mutable = $true },
    [pscustomobject]@{ Path = "Assets/CompetingAuthority/Generation.D0M2SCanaryGenerator.additionalfile"; Role = "FaultOnlyCompetingAuthority"; UnityConsumed = $true; Mutable = $true },
    [pscustomobject]@{ Path = "Assets/RuntimeGenerated/D0M2SLegacyGeneration.gen.cs"; Role = "FaultOnlyCompetingAuthority"; UnityConsumed = $true; Mutable = $true }
)
$result = [pscustomobject][ordered]@{
    Schema = $script:Schema; RunId = $RunId; Status = $childStatus.Status; Reason = $childStatus.Reason
    RouteUnderTest = "StableGraphSourceGenerator"
    ObservedFixtureSelector = $script:SelectorRelativePath
    MeasuredCommonSelectorAcrossAssemblies = $measuredCommonSelector
    Cases = @($script:Cases); Inputs = [pscustomobject]$inputs; RoleObservations = $roles
    Cleanup = $cleanup
    Fixture = [pscustomobject][ordered]@{
        Root = if ($cleanupStatus -ceq "Passed") { "<deleted>" } else { $suiteRoot }
        CleanupStatus = $cleanupStatus; OwnedProcessResidue = @($cleanup.LiveOwnedProcessIds).Count -gt 0
        OwnerSentinelValidated = $cleanupStatus -ceq "Passed"
        NoReparsePoints = $null -ne $boundary -and [bool]$boundary.ReparseRejected
        NoHardlinks = $null -ne $boundary -and [bool]$boundary.HardlinkRejected
    }
    EvidenceFiles = @($childClosure.AllFiles)
    Unity = [pscustomobject][ordered]@{
        ExecutablePath = $resolvedUnity; ExecutableSha256 = $unitySha; Version = $unityVersion
        InvocationCount = $script:UnityInvocationIds.Count
        InvocationIds = @($script:UnityInvocationIds)
        TargetAssemblies = $script:TargetAssemblies
    }
    ChildEvidenceClosurePending = $true
    HarnessError = $caughtError
}
Write-FreshJson $resolvedOutput $result
if ($childStatus.Status -ceq "Passed") { exit 0 }
if ($childStatus.Status -ceq "RouteRejected") { exit 20 }
exit 22
