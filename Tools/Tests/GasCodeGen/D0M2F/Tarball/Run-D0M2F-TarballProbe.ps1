[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [Parameter(Mandatory = $true)][ValidatePattern('^D0M2F-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$')][string]$RunId,
    [ValidateSet('Feasibility', 'SelectorConflict')][string]$Mode = 'Feasibility',
    [switch]$KeepFixture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Schema = 'D0M2F-ProbeResult-v1'
$script:PackageName = 'com.exhard.exgas.d0m2f-tarball'
$script:ExpectedUnityVersion = '6000.3.14f1'
$script:OwnerSentinelName = '.d0m2f-tarball-owner'
$script:FixturePrefix = 'gas-codegen-d0-m2f-tarball-'
$script:ProbeTimeoutMilliseconds = 1200000
$script:Utf8NoBom = [Text.UTF8Encoding]::new($false)

if (-not ('D0M2FTarballNativeFileInfo' -as [type]))
{
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// 提供 Windows 文件 hardlink 数量查询，确保 disposable fixture 没有复用生产 inode。
/// </summary>
public static class D0M2FTarballNativeFileInfo
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
        SafeFileHandle handle,
        out ByHandleFileInformation information);

    /// <summary>
    /// 返回普通文件的 hardlink 数量，查询失败时显式抛错。
    /// </summary>
    public static uint GetLinkCount(string path)
    {
        using (FileStream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete))
        {
            ByHandleFileInformation information;
            if (!GetFileInformationByHandle(stream.SafeFileHandle, out information))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            return information.NumberOfLinks;
        }
    }
}
'@
}

# 将路径转换为不带尾分隔符的绝对路径。
function Resolve-NormalizedPath
{
    param([Parameter(Mandatory = $true)][string]$Path)

    return [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
}

# 计算文件 SHA-256 并统一为小写十六进制。
function Get-FileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [IO.File]::Exists($Path)) { throw "Required file does not exist: $Path" }
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try
    {
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try { return ([Convert]::ToHexString($algorithm.ComputeHash($stream))).ToLowerInvariant() }
        finally { $algorithm.Dispose() }
    }
    finally { $stream.Dispose() }
}

# 以 CreateNew 语义写入 UTF-8 无 BOM、LF 结尾文本。
function Write-NewTextFile
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content
    )

    $normalized = $Content.Replace("`r`n", "`n").Replace("`r", "`n")
    if (-not $normalized.EndsWith("`n", [StringComparison]::Ordinal)) { $normalized += "`n" }
    $bytes = $script:Utf8NoBom.GetBytes($normalized)
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try
    {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally { $stream.Dispose() }
}

# 拒绝任意包含 reparse point 的目录树。
function Assert-NoReparseTree
{
    param([Parameter(Mandatory = $true)][string]$Root)

    if (-not [IO.Directory]::Exists($Root)) { throw "Directory tree does not exist: $Root" }
    $items = @((Get-Item -LiteralPath $Root -Force)) + @(Get-ChildItem -LiteralPath $Root -Force -Recurse)
    foreach ($item in $items)
    {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "Fixture tree contains a reparse point: $($item.FullName)"
        }
    }
}

# 确认普通文件只有一个 hardlink。
function Assert-SingleLinkFile
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([D0M2FTarballNativeFileInfo]::GetLinkCount($Path) -ne 1)
    {
        throw "Fixture file must have exactly one hardlink: $Path"
    }
}

# 深拷贝模板并逐文件验证目标不是 hardlink。
function Copy-DirectoryTree
{
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    Assert-NoReparseTree -Root $Source
    if ([IO.Directory]::Exists($Destination) -or [IO.File]::Exists($Destination))
    {
        throw "Deep-copy destination already exists: $Destination"
    }

    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($directory in [IO.Directory]::EnumerateDirectories($Source, '*', [IO.SearchOption]::AllDirectories))
    {
        $relative = [IO.Path]::GetRelativePath($Source, $directory)
        [IO.Directory]::CreateDirectory((Join-Path $Destination $relative)) | Out-Null
    }
    foreach ($file in [IO.Directory]::EnumerateFiles($Source, '*', [IO.SearchOption]::AllDirectories))
    {
        $relative = [IO.Path]::GetRelativePath($Source, $file)
        $target = Join-Path $Destination $relative
        [IO.File]::Copy($file, $target, $false)
        Assert-SingleLinkFile -Path $target
    }
    Assert-NoReparseTree -Root $Destination
}

# 创建 OS 临时目录下唯一且带 owner sentinel 的 suite 根。
function New-OwnedFixtureRoot
{
    $name = $script:FixturePrefix + [Guid]::NewGuid().ToString('N')
    $root = Join-Path ([IO.Path]::GetTempPath()) $name
    if ([IO.Directory]::Exists($root) -or [IO.File]::Exists($root)) { throw "Fixture root already exists: $root" }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    Write-NewTextFile -Path (Join-Path $root $script:OwnerSentinelName) -Content ($name + '|' + $RunId)
    return Resolve-NormalizedPath -Path $root
}

# 复核 fixture 精确临时边界、名称与 owner sentinel。
function Assert-OwnedFixtureRoot
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $normalized = Resolve-NormalizedPath -Path $Root
    $temp = Resolve-NormalizedPath -Path ([IO.Path]::GetTempPath())
    $parent = Resolve-NormalizedPath -Path ([IO.Path]::GetDirectoryName($normalized))
    if (-not $parent.Equals($temp, [StringComparison]::OrdinalIgnoreCase)) { throw "Fixture escaped OS temp root: $normalized" }
    if ([IO.Path]::GetFileName($normalized) -notmatch '^gas-codegen-d0-m2f-tarball-[0-9a-f]{32}$') { throw "Fixture name is not owned: $normalized" }
    if (-not [IO.Directory]::Exists($normalized)) { throw "Fixture root is missing: $normalized" }
    Assert-NoReparseTree -Root $normalized
    $sentinel = Join-Path $normalized $script:OwnerSentinelName
    $expected = [IO.Path]::GetFileName($normalized) + '|' + $RunId + "`n"
    if (-not [IO.File]::Exists($sentinel) -or [IO.File]::ReadAllText($sentinel) -ne $expected)
    {
        throw "Fixture owner sentinel is missing or invalid: $sentinel"
    }
}

# 在完整边界复核后自底向上删除 fixture，拒绝链接或未知根。
function Remove-OwnedFixtureRoot
{
    param([Parameter(Mandatory = $true)][string]$Root)

    Assert-OwnedFixtureRoot -Root $Root
    $sentinel = Join-Path $Root $script:OwnerSentinelName
    $entries = @(Get-ChildItem -LiteralPath $Root -Force -Recurse)
    foreach ($file in @($entries | Where-Object { -not $_.PSIsContainer -and $_.FullName -ne $sentinel }))
    {
        [IO.File]::SetAttributes($file.FullName, [IO.FileAttributes]::Normal)
        [IO.File]::Delete($file.FullName)
    }
    foreach ($directory in @($entries | Where-Object { $_.PSIsContainer } | Sort-Object { $_.FullName.Length } -Descending))
    {
        [IO.Directory]::Delete($directory.FullName, $false)
    }
    [IO.File]::Delete($sentinel)
    [IO.Directory]::Delete($Root, $false)
}

# 读取并严格验证 checked-in A/B archive 索引。
function Read-ArchiveIndex
{
    param([Parameter(Mandatory = $true)][string]$IndexPath)

    $index = Get-Content -LiteralPath $IndexPath -Raw | ConvertFrom-Json
    if ($index.Schema -ne 'D0M2F-TarballArchiveIndex-v1' -or $index.PackageName -ne $script:PackageName)
    {
        throw 'Tarball archive index schema or package name is invalid.'
    }
    $archives = @($index.Archives)
    if ($archives.Count -ne 2 -or @($archives.GenerationId | Sort-Object) -join ',' -ne 'A,B')
    {
        throw 'Tarball archive index must contain exactly generation A and B.'
    }
    return $index
}

# 根据固定 fixture 布局构造只指向一个 content-addressed archive 的 manifest。
function Get-ManifestContent
{
    param([Parameter(Mandatory = $true)][object]$Archive)

    $dependency = 'file:../../Authority/' + [string]$Archive.FileName
    return [pscustomobject]@{
        Content = "{`n  `"dependencies`": {`n    `"$($script:PackageName)`": `"$dependency`"`n  }`n}`n"
        Dependency = $dependency
    }
}

# 深拷贝 Unity project 和 A/B archive，并写入初始 manifest 或 selector 冲突。
function Initialize-Fixture
{
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$ScriptRoot,
        [Parameter(Mandatory = $true)][string]$ProbeMode
    )

    $project = Join-Path $Root 'UnityProject'
    $authority = Join-Path $Root 'Authority'
    $control = Join-Path $Root 'Control'
    $evidence = Join-Path $Root 'Evidence'
    Copy-DirectoryTree -Source (Join-Path $ScriptRoot 'Fixture~') -Destination $project
    foreach ($path in @($authority, $control, $evidence, (Join-Path $project 'Packages'), (Join-Path $project 'Library')))
    {
        [IO.Directory]::CreateDirectory($path) | Out-Null
    }

    $indexPath = Join-Path $ScriptRoot 'Archives~/ArchiveIndex.json'
    $index = Read-ArchiveIndex -IndexPath $indexPath
    foreach ($archive in @($index.Archives))
    {
        $source = Join-Path (Join-Path $ScriptRoot 'Archives~') ([string]$archive.FileName)
        if ((Get-FileSha256 -Path $source) -ne [string]$archive.Sha256) { throw "Archive input hash drift: $source" }
        if ((Get-Item -LiteralPath $source).Length -ne [long]$archive.Length) { throw "Archive input length drift: $source" }
        $target = Join-Path $authority ([string]$archive.FileName)
        [IO.File]::Copy($source, $target, $false)
        Assert-SingleLinkFile -Path $target
        [IO.File]::SetAttributes($target, [IO.FileAttributes]::ReadOnly)
    }

    $archiveA = @($index.Archives | Where-Object { $_.GenerationId -eq 'A' })[0]
    $archiveB = @($index.Archives | Where-Object { $_.GenerationId -eq 'B' })[0]
    $manifestA = Get-ManifestContent -Archive $archiveA
    $manifestB = Get-ManifestContent -Archive $archiveB
    Write-NewTextFile -Path (Join-Path $control 'manifest-A.json') -Content $manifestA.Content
    Write-NewTextFile -Path (Join-Path $control 'manifest-B.json') -Content $manifestB.Content
    $initial = if ($ProbeMode -eq 'SelectorConflict') { $manifestB.Content } else { $manifestA.Content }
    Write-NewTextFile -Path (Join-Path $project 'Packages/manifest.json') -Content $initial
    if ($ProbeMode -eq 'SelectorConflict')
    {
        $settings = Join-Path $project 'ProjectSettings/GasCodeGen'
        [IO.Directory]::CreateDirectory($settings) | Out-Null
        $activeRef = [ordered]@{ GenerationId = 'A'; ArchiveSha256 = [string]$archiveA.Sha256; Role = 'AuditOnly' }
        Write-NewTextFile -Path (Join-Path $settings 'ActiveGenerationRef.json') -Content ($activeRef | ConvertTo-Json -Depth 4)
    }

    Assert-OwnedFixtureRoot -Root $Root
    return [pscustomobject]@{
        Project = $project; Authority = $authority; Control = $control; Evidence = $evidence
        IndexPath = $indexPath; Index = $index; ArchiveA = $archiveA; ArchiveB = $archiveB
        ManifestA = $manifestA; ManifestB = $manifestB
    }
}

# 以同目录 File.Replace 原子切换 disposable project 的 manifest selector。
function Set-ManifestSelector
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $manifest = Join-Path $ProjectPath 'Packages/manifest.json'
    $next = $manifest + '.next'
    $backup = $manifest + '.backup'
    Write-NewTextFile -Path $next -Content $Content
    if ([IO.File]::Exists($backup)) { throw "Selector backup unexpectedly exists: $backup" }
    [IO.File]::Replace($next, $manifest, $backup, $true)
    [IO.File]::Delete($backup)
}

# 启动覆盖整个 fixture 的单 watcher，避免不同角色 watcher 窗口不一致。
function Start-FixtureWatcher
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $prefix = 'D0M2FTarballWatcher-' + [Guid]::NewGuid().ToString('N')
    $watcher = [IO.FileSystemWatcher]::new($Root)
    $watcher.IncludeSubdirectories = $true
    $watcher.InternalBufferSize = 65536
    $watcher.NotifyFilter = [IO.NotifyFilters]'FileName, DirectoryName, LastWrite, Size, Attributes'
    $sourceIds = [Collections.Generic.List[string]]::new()
    foreach ($eventName in @('Created', 'Changed', 'Deleted', 'Renamed', 'Error'))
    {
        $sourceId = $prefix + '-' + $eventName
        Register-ObjectEvent -InputObject $watcher -EventName $eventName -SourceIdentifier $sourceId | Out-Null
        $sourceIds.Add($sourceId)
    }
    $watcher.EnableRaisingEvents = $true
    return [pscustomobject]@{ Watcher = $watcher; SourceIds = $sourceIds }
}

# 将已排队 watcher 事件转成稳定 JSON 记录并立即清空事件队列。
function Receive-FixtureWatcherEvents
{
    param(
        [Parameter(Mandatory = $true)][object]$Handle,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[object]]$Records
    )

    foreach ($sourceId in @($Handle.SourceIds))
    {
        foreach ($eventRecord in @(Get-Event -SourceIdentifier $sourceId -ErrorAction SilentlyContinue))
        {
            $isError = $sourceId.EndsWith('-Error', [StringComparison]::Ordinal)
            $change = if ($isError) { 'Error' } else { [string]$eventRecord.SourceEventArgs.ChangeType }
            $path = if ($isError) { [string]$eventRecord.SourceEventArgs.GetException().Message } else { [string]$eventRecord.SourceEventArgs.FullPath }
            $old = if ($change -eq 'Renamed') { [string]$eventRecord.SourceEventArgs.OldFullPath } else { '' }
            $Records.Add([pscustomobject]@{ Utc = [DateTime]::UtcNow.ToString('O'); ChangeType = $change; FullPath = $path; OldFullPath = $old })
            Remove-Event -EventIdentifier $eventRecord.EventIdentifier
        }
    }
}

# 停止并释放 watcher，同时收尽最后一批事件。
function Stop-FixtureWatcher
{
    param(
        [Parameter(Mandatory = $true)][object]$Handle,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[object]]$Records
    )

    Receive-FixtureWatcherEvents -Handle $Handle -Records $Records
    $Handle.Watcher.EnableRaisingEvents = $false
    Start-Sleep -Milliseconds 250
    Receive-FixtureWatcherEvents -Handle $Handle -Records $Records
    foreach ($sourceId in @($Handle.SourceIds))
    {
        Unregister-Event -SourceIdentifier $sourceId -ErrorAction SilentlyContinue
        Get-Event -SourceIdentifier $sourceId -ErrorAction SilentlyContinue | Remove-Event -ErrorAction SilentlyContinue
    }
    $Handle.Watcher.Dispose()
}

# 写入并删除 canary，证明同一递归 watcher 在 Unity 前已工作。
function Invoke-WatcherCanary
{
    param(
        [Parameter(Mandatory = $true)][object]$Handle,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[object]]$Records,
        [Parameter(Mandatory = $true)][string]$ControlPath
    )

    $canary = Join-Path $ControlPath ('watcher-canary-' + [Guid]::NewGuid().ToString('N'))
    [IO.File]::WriteAllText($canary, 'canary', $script:Utf8NoBom)
    [IO.File]::Delete($canary)
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do
    {
        Start-Sleep -Milliseconds 50
        Receive-FixtureWatcherEvents -Handle $Handle -Records $Records
        if (@($Records | Where-Object { $_.FullPath -eq $canary }).Count -gt 0) { return $canary }
    }
    while ([DateTime]::UtcNow -lt $deadline)
    throw 'Fixture watcher did not observe its positive-control canary.'
}

# 构造无 shell 参与、逐参数传递的子进程启动信息。
function New-ProcessStartInfo
{
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $FilePath
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    return $info
}

# 启动一次 direct cold Unity，并在等待期间持续抽干 watcher 队列。
function Invoke-UnityObservation
{
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$ProbeMode,
        [Parameter(Mandatory = $true)][string]$Operation,
        [Parameter(Mandatory = $true)][object]$Archive,
        [Parameter(Mandatory = $true)][string]$ObservationName,
        [Parameter(Mandatory = $true)][string]$EvidencePath,
        [Parameter(Mandatory = $true)][object]$WatcherHandle,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[object]]$WatcherRecords,
        [switch]$KillAfterEvidence
    )

    $resultPath = Join-Path $EvidencePath ($ObservationName + '.json')
    $logPath = Join-Path $EvidencePath ($ObservationName + '.log')
    $arguments = @(
        '-batchmode', '-nographics', '-projectPath', $ProjectPath,
        '-executeMethod', 'GAS.Tests.D0M2F.Tarball.D0M2FTarballUnityProbe.Run',
        '-d0m2fTarballOperation', $Operation,
        '-d0m2fTarballOutput', $resultPath,
        '-d0m2fExpectedGeneration', [string]$Archive.GenerationId,
        '-d0m2fExpectedArchiveSha', [string]$Archive.Sha256,
        '-d0m2fMode', $ProbeMode,
        '-logFile', $logPath
    )
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = New-ProcessStartInfo -FilePath $Executable -Arguments $arguments
    $startedAt = [DateTime]::UtcNow
    $started = $process.Start()
    if (-not $started) { throw 'Unity process did not start.' }
    $timedOut = $false
    $killed = $false
    try
    {
        $deadline = $startedAt.AddMilliseconds($script:ProbeTimeoutMilliseconds)
        while (-not $process.HasExited)
        {
            Receive-FixtureWatcherEvents -Handle $WatcherHandle -Records $WatcherRecords
            if ($KillAfterEvidence -and [IO.File]::Exists($resultPath))
            {
                Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json | Out-Null
                $process.Kill($true)
                $killed = $true
                if (-not $process.WaitForExit(30000)) { throw 'Unity process tree survived the planned kill.' }
                break
            }
            if ([DateTime]::UtcNow -ge $deadline)
            {
                $process.Kill($true)
                $timedOut = $true
                if (-not $process.WaitForExit(30000)) { throw 'Unity process tree survived timeout cleanup.' }
                break
            }
            Start-Sleep -Milliseconds 100
        }
        Receive-FixtureWatcherEvents -Handle $WatcherHandle -Records $WatcherRecords
        $payload = if ([IO.File]::Exists($resultPath)) { Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json } else { $null }
        return [pscustomobject]@{
            Name = $ObservationName; ExitCode = if ($process.HasExited) { $process.ExitCode } else { -1 }
            TimedOut = $timedOut; KilledAsPlanned = $killed
            DurationMilliseconds = [int]([DateTime]::UtcNow - $startedAt).TotalMilliseconds
            ResultPath = $resultPath; LogPath = $logPath; Payload = $payload
        }
    }
    finally
    {
        if (-not $process.HasExited)
        {
            $process.Kill($true)
            $process.WaitForExit(30000) | Out-Null
        }
        $process.Dispose()
    }
}

# 读取当前 packages-lock 对 tarball dependency 的 typed 角色观察。
function Get-PackageLockObservation
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][object]$Archive
    )

    $path = Join-Path $ProjectPath 'Packages/packages-lock.json'
    if (-not [IO.File]::Exists($path)) { return [pscustomobject]@{ Passed = $false; Path = $path; Detail = 'packages-lock.json is missing.' } }
    $lock = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $property = $lock.dependencies.PSObject.Properties[$script:PackageName]
    if ($null -eq $property) { return [pscustomobject]@{ Passed = $false; Path = $path; Detail = 'Tarball dependency is missing.' } }
    $entry = $property.Value
    $source = [string]$entry.source
    $version = [string]$entry.version
    $sourceMatches = $source -eq 'local-tarball' -or $source -eq 'local'
    $versionMatches = $version.Replace('\\', '/').EndsWith('/' + [string]$Archive.FileName, [StringComparison]::OrdinalIgnoreCase)
    return [pscustomobject]@{
        Passed = $sourceMatches -and $versionMatches; Path = $path; Source = $source; Version = $version
        Sha256 = Get-FileSha256 -Path $path; ExpectedArchive = [string]$Archive.FileName
        Detail = "source=$source; version=$version"
    }
}

# 核对 Unity payload 的版本、marker、sourceFiles 与 PackageCache 物理来源。
function Test-UnityPayload
{
    param(
        [Parameter(Mandatory = $true)][object]$Observation,
        [Parameter(Mandatory = $true)][object]$Archive,
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [switch]$ExpectedKilled
    )

    if ($Observation.TimedOut -or $null -eq $Observation.Payload)
    {
        return [pscustomobject]@{ Passed = $false; TimedOut = [bool]$Observation.TimedOut; Detail = 'Unity observation is missing or timed out.' }
    }
    $payload = $Observation.Payload
    $processMatches = if ($ExpectedKilled) { $Observation.KilledAsPlanned } else { $Observation.ExitCode -eq 0 }
    $generation = [string]$Archive.GenerationId
    $token = [string]$Archive.GenerationToken
    $markersMatch = $payload.Passed -and $payload.RuntimeGeneration -eq $generation -and $payload.EditorGeneration -eq $generation -and $payload.AutoChessGeneration -eq $generation
    $tokensMatch = $payload.RuntimeToken -eq $token -and $payload.EditorToken -eq $token -and $payload.AutoChessToken -eq $token
    $identityMatches = $payload.UnityVersion -eq $script:ExpectedUnityVersion -and $payload.PackageName -eq $script:PackageName -and $payload.PackageVersion -eq [string]$Archive.PackageVersion
    $cacheRoot = Resolve-NormalizedPath -Path (Join-Path $ProjectPath 'Library/PackageCache')
    $resolved = if ([string]::IsNullOrWhiteSpace([string]$payload.ResolvedPackagePath)) { '' } else { Resolve-NormalizedPath -Path ([string]$payload.ResolvedPackagePath) }
    $cacheMatches = -not [string]::IsNullOrWhiteSpace($resolved) -and $resolved.StartsWith($cacheRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
    $passed = $processMatches -and $markersMatch -and $tokensMatch -and $identityMatches -and $cacheMatches
    return [pscustomobject]@{
        Passed = $passed; TimedOut = $false; ProcessMatches = $processMatches; MarkersMatch = $markersMatch
        TokensMatch = $tokensMatch; IdentityMatches = $identityMatches; CacheMatches = $cacheMatches
        Detail = "process=$processMatches; markers=$markersMatch; tokens=$tokensMatch; identity=$identityMatches; cache=$cacheMatches"
    }
}

# 将 Unity 校验区分为路线语义失败与证据/工具身份不完整。
function Get-UnityValidationOutcome
{
    param(
        [Parameter(Mandatory = $true)][object]$Validation,
        [Parameter(Mandatory = $true)][string]$RouteFailureReason
    )

    if ($Validation.TimedOut) { return [pscustomobject]@{ Status = 'TimedOut'; Reason = 'TimeBoxExceeded' } }
    if ($Validation.Passed) { return [pscustomobject]@{ Status = 'Passed'; Reason = 'None' } }
    $identity = $Validation.PSObject.Properties['IdentityMatches']
    $process = $Validation.PSObject.Properties['ProcessMatches']
    if ($null -eq $identity -or $null -eq $process) { return [pscustomobject]@{ Status = 'Inconclusive'; Reason = 'EvidenceIncomplete' } }
    if (-not [bool]$identity.Value) { return [pscustomobject]@{ Status = 'Inconclusive'; Reason = 'UnityIdentityMismatch' } }
    if (-not [bool]$process.Value) { return [pscustomobject]@{ Status = 'Inconclusive'; Reason = 'EvidenceIncomplete' } }
    return [pscustomobject]@{ Status = 'Failed'; Reason = $RouteFailureReason }
}

# 创建满足公共合同的固定 case 结果。
function New-CaseResult
{
    param(
        [Parameter(Mandatory = $true)][string]$CaseId,
        [string]$Status = 'NotRun',
        [string]$Reason = 'EvidenceIncomplete',
        [object]$Evidence = $null
    )

    if ($null -eq $Evidence) { $Evidence = [pscustomobject]@{ Detail = 'Not evaluated.' } }
    return [pscustomobject]@{ CaseId = $CaseId; Status = $Status; Reason = $Reason; Evidence = $Evidence }
}

# 覆盖指定 case 的状态、原因与证据。
function Set-CaseResult
{
    param(
        [Parameter(Mandatory = $true)][object[]]$Cases,
        [Parameter(Mandatory = $true)][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$Status,
        [Parameter(Mandatory = $true)][string]$Reason,
        [Parameter(Mandatory = $true)][object]$Evidence
    )

    $case = @($Cases | Where-Object { $_.CaseId -eq $CaseId })
    if ($case.Count -ne 1) { throw "Case is not unique: $CaseId" }
    $case[0].Status = $Status
    $case[0].Reason = $Reason
    $case[0].Evidence = $Evidence
}

# 汇总 archive、selector、lock 与 cache 的 Authority/Derived/Cache 角色表。
function New-RoleObservations
{
    param(
        [Parameter(Mandatory = $true)][object]$Context,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Events,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$UnityObservations
    )

    $authorityEvents = @($Events | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.FullPath) -and $_.FullPath.StartsWith($Context.Authority + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) })
    $packagesPath = Join-Path $Context.Project 'Packages'
    $packageEvents = @($Events | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.FullPath) -and $_.FullPath.StartsWith($packagesPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) })
    $cachePath = Join-Path $Context.Project 'Library/PackageCache'
    $cacheEvents = @($Events | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.FullPath) -and $_.FullPath.StartsWith($cachePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) })
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($archive in @($Context.ArchiveA, $Context.ArchiveB))
    {
        $path = Join-Path $Context.Authority ([string]$archive.FileName)
        $rows.Add([pscustomobject]@{ Path = $path; Role = 'Authority'; GenerationId = [string]$archive.GenerationId; Mutable = $false; ObservedWriteCount = @($authorityEvents | Where-Object { $_.FullPath -eq $path -or $_.OldFullPath -eq $path }).Count; ConsumerAuthority = 'Payload' })
    }
    $rows.Add([pscustomobject]@{ Path = (Join-Path $packagesPath 'manifest.json'); Role = 'Authority'; GenerationId = ''; Mutable = $true; ObservedWriteCount = @($packageEvents | Where-Object { $_.FullPath.EndsWith('manifest.json', [StringComparison]::OrdinalIgnoreCase) }).Count; ConsumerAuthority = 'SoleUnitySelector' })
    $rows.Add([pscustomobject]@{ Path = (Join-Path $packagesPath 'packages-lock.json'); Role = 'Derived'; GenerationId = ''; Mutable = $true; ObservedWriteCount = @($packageEvents | Where-Object { $_.FullPath.EndsWith('packages-lock.json', [StringComparison]::OrdinalIgnoreCase) }).Count; ConsumerAuthority = 'None' })
    $rows.Add([pscustomobject]@{ Path = $cachePath; Role = 'Cache'; GenerationId = ''; Mutable = $true; ObservedWriteCount = $cacheEvents.Count; ConsumerAuthority = 'None' })
    if ($Mode -eq 'SelectorConflict')
    {
        $rows.Add([pscustomobject]@{ Path = (Join-Path $Context.Project 'ProjectSettings/GasCodeGen/ActiveGenerationRef.json'); Role = 'Derived'; GenerationId = 'A'; Mutable = $true; ObservedWriteCount = 0; ConsumerAuthority = 'AuditOnly' })
    }
    foreach ($observation in $UnityObservations)
    {
        if ($null -ne $observation.Payload -and -not [string]::IsNullOrWhiteSpace([string]$observation.Payload.ResolvedPackagePath))
        {
            $rows.Add([pscustomobject]@{ Path = [string]$observation.Payload.ResolvedPackagePath; Role = 'Cache'; GenerationId = [string]$observation.Payload.RuntimeGeneration; Mutable = $true; ObservedWriteCount = 0; ConsumerAuthority = 'CompiledProjection' })
        }
    }
    return $rows
}

# 以 CreateNew 写入本 probe 唯一 aggregate JSON。
function Write-Aggregate
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Aggregate
    )

    $directory = [IO.Path]::GetDirectoryName($Path)
    if ([string]::IsNullOrWhiteSpace($directory)) { throw 'Aggregate output directory is missing.' }
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    Write-NewTextFile -Path $Path -Content ($Aggregate | ConvertTo-Json -Depth 32)
}

$scriptRoot = Resolve-NormalizedPath -Path $PSScriptRoot
$projectRoot = Resolve-NormalizedPath -Path (Join-Path $scriptRoot '../../../../..')
$resolvedOutput = if ([IO.Path]::IsPathRooted($OutputPath)) { Resolve-NormalizedPath -Path $OutputPath } else { Resolve-NormalizedPath -Path (Join-Path $projectRoot $OutputPath) }
$resolvedUnity = Resolve-NormalizedPath -Path $UnityPath
$probeId = if ($Mode -eq 'SelectorConflict') { 'X' } else { 'T' }
$caseIds = if ($probeId -eq 'X') { @('X-01', 'X-02', 'X-03', 'X-04') } else { @('T-01', 'T-02', 'T-03', 'T-04') }
$cases = @($caseIds | ForEach-Object { New-CaseResult -CaseId $_ })
$fixtureRoot = ''
$fixtureCleanup = 'NotStarted'
$fixtureBoundaryValidated = $false
$manifestAHash = ''
$manifestBHash = ''
$archiveIndexHash = ''
$watcher = $null
$watcherRecords = [Collections.Generic.List[object]]::new()
$unityObservations = [Collections.Generic.List[object]]::new()
$context = $null
$roleObservations = @([pscustomobject]@{ Path = '<unavailable>'; Role = 'Authority'; Detail = 'Fixture was not initialized.' })
$overallStatus = 'Inconclusive'
$overallReason = 'EvidenceIncomplete'
$failureDetail = ''
$exitCode = 23

try
{
    if (-not [IO.Path]::IsPathRooted($UnityPath) -or -not [IO.File]::Exists($resolvedUnity)) { throw "UnityPath must be an existing absolute file: $UnityPath" }
    if ([IO.File]::Exists($resolvedOutput) -or [IO.Directory]::Exists($resolvedOutput)) { throw "OutputPath must be fresh: $resolvedOutput" }
    $fixtureRoot = New-OwnedFixtureRoot
    $context = Initialize-Fixture -Root $fixtureRoot -ScriptRoot $scriptRoot -ProbeMode $Mode
    $fixtureBoundaryValidated = $true
    $manifestAHash = Get-FileSha256 -Path (Join-Path $context.Control 'manifest-A.json')
    $manifestBHash = Get-FileSha256 -Path (Join-Path $context.Control 'manifest-B.json')
    $archiveIndexHash = Get-FileSha256 -Path $context.IndexPath
    $archiveAPath = Join-Path $context.Authority ([string]$context.ArchiveA.FileName)
    $archiveBPath = Join-Path $context.Authority ([string]$context.ArchiveB.FileName)
    $archiveABefore = Get-FileSha256 -Path $archiveAPath
    $archiveBBefore = Get-FileSha256 -Path $archiveBPath
    $watcher = Start-FixtureWatcher -Root $fixtureRoot
    $canaryPath = Invoke-WatcherCanary -Handle $watcher -Records $watcherRecords -ControlPath $context.Control

    if ($Mode -eq 'Feasibility')
    {
        $coldA = Invoke-UnityObservation -Executable $resolvedUnity -ProjectPath $context.Project -ProbeMode $Mode -Operation 'Verify' -Archive $context.ArchiveA -ObservationName 'cold-a' -EvidencePath $context.Evidence -WatcherHandle $watcher -WatcherRecords $watcherRecords
        $unityObservations.Add($coldA)
        $coldAValidation = Test-UnityPayload -Observation $coldA -Archive $context.ArchiveA -ProjectPath $context.Project
        $lockA = Get-PackageLockObservation -ProjectPath $context.Project -Archive $context.ArchiveA
        $t01Outcome = Get-UnityValidationOutcome -Validation $coldAValidation -RouteFailureReason 'MixedGenerationObserved'
        $t01Status = if ($t01Outcome.Status -eq 'Passed' -and -not $lockA.Passed) { 'Failed' } else { $t01Outcome.Status }
        $t01Reason = if ($t01Outcome.Status -eq 'Passed' -and -not $lockA.Passed) { 'CacheFallbackSelectorObserved' } else { $t01Outcome.Reason }
        Set-CaseResult -Cases $cases -CaseId 'T-01' -Status $t01Status -Reason $t01Reason -Evidence ([pscustomobject]@{ Validation = $coldAValidation; Lock = $lockA; Observation = $coldA })

        Set-ManifestSelector -ProjectPath $context.Project -Content $context.ManifestB.Content
        $preKillB = Invoke-UnityObservation -Executable $resolvedUnity -ProjectPath $context.Project -ProbeMode $Mode -Operation 'SignalAndWaitForKill' -Archive $context.ArchiveB -ObservationName 'switch-b-pre-kill' -EvidencePath $context.Evidence -WatcherHandle $watcher -WatcherRecords $watcherRecords -KillAfterEvidence
        $unityObservations.Add($preKillB)
        $preKillValidation = Test-UnityPayload -Observation $preKillB -Archive $context.ArchiveB -ProjectPath $context.Project -ExpectedKilled
        $t02Outcome = Get-UnityValidationOutcome -Validation $preKillValidation -RouteFailureReason 'MixedGenerationObserved'
        $t02Status = $t02Outcome.Status
        $t02Reason = $t02Outcome.Reason
        Set-CaseResult -Cases $cases -CaseId 'T-02' -Status $t02Status -Reason $t02Reason -Evidence ([pscustomobject]@{ Validation = $preKillValidation; Observation = $preKillB; ManifestHash = Get-FileSha256 -Path (Join-Path $context.Project 'Packages/manifest.json') })

        $restartB = Invoke-UnityObservation -Executable $resolvedUnity -ProjectPath $context.Project -ProbeMode $Mode -Operation 'Verify' -Archive $context.ArchiveB -ObservationName 'restart-b' -EvidencePath $context.Evidence -WatcherHandle $watcher -WatcherRecords $watcherRecords
        $unityObservations.Add($restartB)
        $restartValidation = Test-UnityPayload -Observation $restartB -Archive $context.ArchiveB -ProjectPath $context.Project
        $lockB = Get-PackageLockObservation -ProjectPath $context.Project -Archive $context.ArchiveB
        $t03Passed = $preKillValidation.Passed -and $preKillB.KilledAsPlanned -and $restartValidation.Passed -and $lockB.Passed
        $restartOutcome = Get-UnityValidationOutcome -Validation $restartValidation -RouteFailureReason 'CacheFallbackSelectorObserved'
        $t03Status = if ($preKillValidation.TimedOut -or $restartValidation.TimedOut) { 'TimedOut' } elseif ($t03Passed) { 'Passed' } elseif (-not $preKillB.KilledAsPlanned) { 'Inconclusive' } elseif ($restartOutcome.Status -eq 'Inconclusive') { 'Inconclusive' } else { 'Failed' }
        $t03Reason = if ($t03Status -eq 'TimedOut') { 'TimeBoxExceeded' } elseif ($t03Status -eq 'Passed') { 'None' } elseif (-not $preKillB.KilledAsPlanned) { 'UnityProcessResidue' } elseif ($restartOutcome.Status -eq 'Inconclusive') { $restartOutcome.Reason } else { 'CacheFallbackSelectorObserved' }
        Set-CaseResult -Cases $cases -CaseId 'T-03' -Status $t03Status -Reason $t03Reason -Evidence ([pscustomobject]@{ PreKill = $preKillB; PreKillValidation = $preKillValidation; Restart = $restartB; RestartValidation = $restartValidation; Lock = $lockB })
    }
    else
    {
        $activeRefPath = Join-Path $context.Project 'ProjectSettings/GasCodeGen/ActiveGenerationRef.json'
        $activeRef = Get-Content -LiteralPath $activeRefPath -Raw | ConvertFrom-Json
        $manifestHash = Get-FileSha256 -Path (Join-Path $context.Project 'Packages/manifest.json')
        $conflictConstructed = $activeRef.GenerationId -eq 'A' -and $manifestHash -eq (Get-FileSha256 -Path (Join-Path $context.Control 'manifest-B.json'))
        Set-CaseResult -Cases $cases -CaseId 'X-01' -Status $(if ($conflictConstructed) { 'Passed' } else { 'Inconclusive' }) -Reason $(if ($conflictConstructed) { 'None' } else { 'SelectorConflictObserved' }) -Evidence ([pscustomobject]@{ ActiveGenerationRef = $activeRef; ManifestHash = $manifestHash; ManifestGeneration = 'B' })

        $coldB = Invoke-UnityObservation -Executable $resolvedUnity -ProjectPath $context.Project -ProbeMode $Mode -Operation 'Verify' -Archive $context.ArchiveB -ObservationName 'conflict-cold-b' -EvidencePath $context.Evidence -WatcherHandle $watcher -WatcherRecords $watcherRecords
        $unityObservations.Add($coldB)
        $coldBValidation = Test-UnityPayload -Observation $coldB -Archive $context.ArchiveB -ProjectPath $context.Project
        $preKillB = Invoke-UnityObservation -Executable $resolvedUnity -ProjectPath $context.Project -ProbeMode $Mode -Operation 'SignalAndWaitForKill' -Archive $context.ArchiveB -ObservationName 'conflict-pre-kill-b' -EvidencePath $context.Evidence -WatcherHandle $watcher -WatcherRecords $watcherRecords -KillAfterEvidence
        $unityObservations.Add($preKillB)
        $preKillValidation = Test-UnityPayload -Observation $preKillB -Archive $context.ArchiveB -ProjectPath $context.Project -ExpectedKilled
        $restartB = Invoke-UnityObservation -Executable $resolvedUnity -ProjectPath $context.Project -ProbeMode $Mode -Operation 'Verify' -Archive $context.ArchiveB -ObservationName 'conflict-restart-b' -EvidencePath $context.Evidence -WatcherHandle $watcher -WatcherRecords $watcherRecords
        $unityObservations.Add($restartB)
        $restartValidation = Test-UnityPayload -Observation $restartB -Archive $context.ArchiveB -ProjectPath $context.Project
        $allConsumedB = $coldBValidation.Passed -and $preKillValidation.Passed -and $restartValidation.Passed
        $x02Status = if ($coldBValidation.TimedOut -or $preKillValidation.TimedOut -or $restartValidation.TimedOut) { 'TimedOut' } elseif ($allConsumedB) { 'Passed' } else { 'Inconclusive' }
        $x02Reason = if ($x02Status -eq 'TimedOut') { 'TimeBoxExceeded' } elseif ($x02Status -eq 'Passed') { 'None' } else { 'SelectorConflictObserved' }
        Set-CaseResult -Cases $cases -CaseId 'X-02' -Status $x02Status -Reason $x02Reason -Evidence ([pscustomobject]@{ Cold = $coldB; PreKill = $preKillB; Restart = $restartB })

        $activeRefZero = $allConsumedB -and @($unityObservations | Where-Object { $_.Payload.ActiveGenerationRefGeneration -ne 'A' -or $_.Payload.RuntimeGeneration -ne 'B' }).Count -eq 0
        Set-CaseResult -Cases $cases -CaseId 'X-03' -Status $(if ($activeRefZero) { 'Passed' } else { 'Inconclusive' }) -Reason $(if ($activeRefZero) { 'None' } else { 'SelectorConflictObserved' }) -Evidence ([pscustomobject]@{ ActiveGenerationRefGeneration = 'A'; ManifestGeneration = 'B'; ActualGenerations = @($unityObservations | ForEach-Object { $_.Payload.RuntimeGeneration }); ConsumerAuthority = 'Manifest' })

        $sameGeneration = $allConsumedB -and @($unityObservations | Where-Object { $_.Payload.RuntimeGeneration -ne $_.Payload.EditorGeneration -or $_.Payload.EditorGeneration -ne $_.Payload.AutoChessGeneration }).Count -eq 0
        Set-CaseResult -Cases $cases -CaseId 'X-04' -Status $(if ($sameGeneration) { 'Passed' } else { 'Inconclusive' }) -Reason $(if ($sameGeneration) { 'None' } else { 'SelectorConflictObserved' }) -Evidence ([pscustomobject]@{ ExpectedGeneration = 'B'; Observations = @($unityObservations) })
    }

    Stop-FixtureWatcher -Handle $watcher -Records $watcherRecords
    $watcher = $null
    $watcherErrors = @($watcherRecords | Where-Object { $_.ChangeType -eq 'Error' })
    $authorityEvents = @($watcherRecords | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.FullPath) -and $_.FullPath.StartsWith($context.Authority + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) })
    $archiveStable = (Get-FileSha256 -Path $archiveAPath) -eq $archiveABefore -and (Get-FileSha256 -Path $archiveBPath) -eq $archiveBBefore -and $authorityEvents.Count -eq 0
    $canaryObserved = @($watcherRecords | Where-Object { $_.FullPath -eq $canaryPath }).Count -gt 0
    $roleObservations = @(New-RoleObservations -Context $context -Events @($watcherRecords) -UnityObservations @($unityObservations))

    if ($Mode -eq 'Feasibility')
    {
        $t04Status = if ($watcherErrors.Count -gt 0 -or -not $canaryObserved) { 'Inconclusive' } elseif (-not $archiveStable) { 'Failed' } else { 'Passed' }
        $t04Reason = if ($watcherErrors.Count -gt 0 -or -not $canaryObserved) { 'WatcherFailure' } elseif (-not $archiveStable) { 'AuthorityMutationObserved' } else { 'None' }
        Set-CaseResult -Cases $cases -CaseId 'T-04' -Status $t04Status -Reason $t04Reason -Evidence ([pscustomobject]@{ ArchiveStable = $archiveStable; AuthorityEvents = $authorityEvents; WatcherErrors = $watcherErrors; CanaryObserved = $canaryObserved; Roles = $roleObservations })
    }
    elseif ($watcherErrors.Count -gt 0 -or -not $archiveStable -or -not $canaryObserved)
    {
        Set-CaseResult -Cases $cases -CaseId 'X-03' -Status 'Inconclusive' -Reason $(if ($watcherErrors.Count -gt 0 -or -not $canaryObserved) { 'WatcherFailure' } else { 'SelectorConflictObserved' }) -Evidence ([pscustomobject]@{ ArchiveStable = $archiveStable; WatcherErrors = $watcherErrors; CanaryObserved = $canaryObserved; Roles = $roleObservations })
    }

    $timedOutCases = @($cases | Where-Object { $_.Status -eq 'TimedOut' })
    $inconclusiveCases = @($cases | Where-Object { $_.Status -eq 'Inconclusive' -or $_.Status -eq 'NotRun' })
    $failedCases = @($cases | Where-Object { $_.Status -eq 'Failed' })
    if ($timedOutCases.Count -gt 0) { $overallStatus = 'TimedOut'; $overallReason = 'TimeBoxExceeded'; $exitCode = 21 }
    elseif ($inconclusiveCases.Count -gt 0) { $overallStatus = 'Inconclusive'; $overallReason = [string]$inconclusiveCases[0].Reason; $exitCode = 22 }
    elseif ($failedCases.Count -gt 0 -and $Mode -eq 'Feasibility') { $overallStatus = 'HardRejected'; $overallReason = [string]$failedCases[0].Reason; $exitCode = 20 }
    elseif ($failedCases.Count -gt 0) { $overallStatus = 'Inconclusive'; $overallReason = 'SelectorConflictObserved'; $exitCode = 22 }
    else { $overallStatus = 'Passed'; $overallReason = 'None'; $exitCode = 0 }
}
catch
{
    $failureDetail = $_.Exception.ToString()
    if ($overallStatus -ne 'TimedOut') { $overallStatus = 'Inconclusive'; $overallReason = 'HarnessFailure'; $exitCode = 23 }
}
finally
{
    if ($null -ne $watcher)
    {
        try { Stop-FixtureWatcher -Handle $watcher -Records $watcherRecords }
        catch { $failureDetail += "`nWatcher cleanup: " + $_.Exception.ToString(); $overallStatus = 'Inconclusive'; $overallReason = 'WatcherFailure'; $exitCode = 23 }
    }
    if (-not [string]::IsNullOrWhiteSpace($fixtureRoot))
    {
        if ($KeepFixture) { $fixtureCleanup = 'Kept' }
        else
        {
            try { Remove-OwnedFixtureRoot -Root $fixtureRoot; $fixtureCleanup = 'Passed' }
            catch { $fixtureCleanup = 'Failed'; $failureDetail += "`nFixture cleanup: " + $_.Exception.ToString(); $overallStatus = 'Inconclusive'; $overallReason = 'CleanupFailure'; $exitCode = 24 }
        }
    }

    $inputs = [ordered]@{
        Mode = $Mode; UnityPath = $resolvedUnity
        UnitySha256 = if ([IO.File]::Exists($resolvedUnity)) { Get-FileSha256 -Path $resolvedUnity } else { '' }
        HarnessSha256 = Get-FileSha256 -Path $PSCommandPath
        ArchiveIndexSha256 = $archiveIndexHash
        ArchiveA = if ($null -ne $context) { $context.ArchiveA } else { [pscustomobject]@{} }
        ArchiveB = if ($null -ne $context) { $context.ArchiveB } else { [pscustomobject]@{} }
        ManifestAHash = $manifestAHash
        ManifestBHash = $manifestBHash
        ActiveGenerationRefGeneration = if ($Mode -eq 'SelectorConflict') { 'A' } else { '' }
    }
    $aggregate = [ordered]@{
        Schema = $script:Schema; RunId = $RunId; ProbeId = $probeId; Status = $overallStatus; Reason = $overallReason
        Cases = $cases; Inputs = $inputs
        Protected = [ordered]@{ Responsibility = 'RootJ0J1'; Evaluated = $false; DriftDetected = $null }
        Fixture = [ordered]@{ Root = $fixtureRoot; Kept = [bool]$KeepFixture; BoundaryValidated = $fixtureBoundaryValidated; NoReparsePoints = $fixtureBoundaryValidated; SingleLinkFiles = $fixtureBoundaryValidated; CleanupStatus = $fixtureCleanup }
        Unity = [ordered]@{ ExpectedVersion = $script:ExpectedUnityVersion; Observations = @($unityObservations) }
        RoleObservations = @($roleObservations)
        FailureDetail = $failureDetail
    }
    try { Write-Aggregate -Path $resolvedOutput -Aggregate $aggregate }
    catch { [Console]::Error.WriteLine($_.Exception.ToString()); exit 90 }
}

exit $exitCode
