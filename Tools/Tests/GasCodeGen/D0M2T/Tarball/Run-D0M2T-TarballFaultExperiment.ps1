#requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [Parameter(Mandatory = $true)][string]$EvidenceRoot,
    [Parameter(Mandatory = $true)][ValidatePattern('^D0M2T-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$')][string]$RunId,
    [switch]$KeepFixtures
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Schema = 'D0M2T-Aggregate-v1'
$script:PackageName = 'com.exhard.exgas.d0m2f-tarball'
$script:ExpectedUnityVersion = '6000.3.14f1'
$script:OwnerSentinelName = '.d0m2t-tarball-owner'
$script:FixturePrefix = 'gas-codegen-d0-m2t-tarball-'
$script:ProbeTimeoutMilliseconds = 600000
$script:Utf8NoBom = [Text.UTF8Encoding]::new($false)
$script:EvidenceFiles = [Collections.Generic.List[object]]::new()
$script:OwnedProcessIds = [Collections.Generic.List[int]]::new()
$script:CreatedFixtureRoots = [Collections.Generic.List[string]]::new()
$script:RemovedFixtureRoots = [Collections.Generic.List[string]]::new()
$script:TransientArtifactObservations = [Collections.Generic.List[object]]::new()
$script:HarnessControlObservations = [Collections.Generic.List[object]]::new()

if (-not ('D0M2TTarballNativeFileInfo' -as [type]))
{
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// 提供 Windows 文件 hardlink 数量查询，确保 disposable fixture 没有复用生产 inode。
/// </summary>
public static class D0M2TTarballNativeFileInfo
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

# 确认目标位于指定根内，禁止输出或证据路径逃逸到生产目录。
function Assert-DescendantPath
{
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $rootPath = Resolve-NormalizedPath -Path $Root
    $targetPath = Resolve-NormalizedPath -Path $Path
    $prefix = $rootPath + [IO.Path]::DirectorySeparatorChar
    if (-not $targetPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "$Label escaped its allowed root: $targetPath"
    }
}

# 将 EvidenceRoot 相对路径解析为安全绝对路径并拒绝绝对、点段或越界。
function Resolve-EvidencePath
{
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    if ([IO.Path]::IsPathRooted($RelativePath)) { throw "Evidence path must be relative: $RelativePath" }
    $segments = $RelativePath.Replace('\', '/').Split('/')
    if ($segments.Count -eq 0 -or @($segments | Where-Object { $_ -eq '' -or $_ -eq '.' -or $_ -eq '..' }).Count -ne 0)
    {
        throw "Evidence path contains an unsafe segment: $RelativePath"
    }
    $absolute = Resolve-NormalizedPath -Path (Join-Path $script:ResolvedEvidenceRoot $RelativePath)
    Assert-DescendantPath -Root $script:ResolvedEvidenceRoot -Path $absolute -Label 'Evidence file'
    return $absolute
}

# 写出 fresh typed raw JSON，所有证据先于 aggregate 持久化。
function Write-EvidenceJson
{
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][object]$Value
    )

    $path = Resolve-EvidencePath -RelativePath $RelativePath
    $parent = [IO.Path]::GetDirectoryName($path)
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    Write-NewTextFile -Path $path -Content ($Value | ConvertTo-Json -Depth 64)
    return $path
}

# 注册一个已落盘证据并固定精确六字段合同。
function Register-EvidenceFile
{
    param(
        [Parameter(Mandatory = $true)][ValidatePattern('^TT-0[1-8]-[a-z0-9][a-z0-9._-]*$')][string]$EvidenceId,
        [Parameter(Mandatory = $true)][ValidatePattern('^TT-0[1-8]$')][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$Kind,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if (@($script:EvidenceFiles | Where-Object EvidenceId -CEQ $EvidenceId).Count -ne 0)
    {
        throw "Duplicate evidence id: $EvidenceId"
    }
    $path = Resolve-EvidencePath -RelativePath $RelativePath
    if (-not [IO.File]::Exists($path)) { throw "Evidence file does not exist: $path" }
    $normalizedRelative = $RelativePath.Replace('\', '/')
    $script:EvidenceFiles.Add([pscustomobject][ordered]@{
        EvidenceId = $EvidenceId
        CaseId = $CaseId
        Kind = $Kind
        Path = $normalizedRelative
        Sha256 = Get-FileSha256 -Path $path
        Length = [long](Get-Item -LiteralPath $path).Length
    })
    return $EvidenceId
}

# 写出 JSON 后立即注册其相对路径、长度与 SHA-256。
function Add-JsonEvidence
{
    param(
        [Parameter(Mandatory = $true)][string]$EvidenceId,
        [Parameter(Mandatory = $true)][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$Kind,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][object]$Value
    )

    Write-EvidenceJson -RelativePath $RelativePath -Value $Value | Out-Null
    return Register-EvidenceFile -EvidenceId $EvidenceId -CaseId $CaseId -Kind $Kind -RelativePath $RelativePath
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

    if ([D0M2TTarballNativeFileInfo]::GetLinkCount($Path) -ne 1)
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
    param([Parameter(Mandatory = $true)][ValidatePattern('^TT-0[1-8]$')][string]$CaseId)

    $name = $script:FixturePrefix + [Guid]::NewGuid().ToString('N')
    $root = Join-Path ([IO.Path]::GetTempPath()) $name
    if ([IO.Directory]::Exists($root) -or [IO.File]::Exists($root)) { throw "Fixture root already exists: $root" }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    Write-NewTextFile -Path (Join-Path $root $script:OwnerSentinelName) -Content ($name + '|' + $RunId)
    $normalized = Resolve-NormalizedPath -Path $root
    $script:CreatedFixtureRoots.Add($normalized)
    Register-HarnessControlPath -CaseId $CaseId -Path (Join-Path $normalized $script:OwnerSentinelName) -Kind 'OwnerSentinel' | Out-Null
    return $normalized
}

# 复核 fixture 精确临时边界、名称与 owner sentinel。
function Assert-OwnedFixtureRoot
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $normalized = Resolve-NormalizedPath -Path $Root
    $temp = Resolve-NormalizedPath -Path ([IO.Path]::GetTempPath())
    $parent = Resolve-NormalizedPath -Path ([IO.Path]::GetDirectoryName($normalized))
    if (-not $parent.Equals($temp, [StringComparison]::OrdinalIgnoreCase)) { throw "Fixture escaped OS temp root: $normalized" }
    if ([IO.Path]::GetFileName($normalized) -notmatch '^gas-codegen-d0-m2t-tarball-[0-9a-f]{32}$') { throw "Fixture name is not owned: $normalized" }
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
    foreach ($file in @($entries | Where-Object { -not $_.PSIsContainer }))
    {
        Assert-SingleLinkFile -Path $file.FullName
    }
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
    $script:RemovedFixtureRoots.Add((Resolve-NormalizedPath -Path $Root))
}

# 将少量 overlay 文件深拷贝进已存在的 fixture 工程并拒绝覆盖。
function Copy-OverlayTree
{
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    Assert-NoReparseTree -Root $Source
    foreach ($directory in [IO.Directory]::EnumerateDirectories($Source, '*', [IO.SearchOption]::AllDirectories))
    {
        $relative = [IO.Path]::GetRelativePath($Source, $directory)
        [IO.Directory]::CreateDirectory((Join-Path $Destination $relative)) | Out-Null
    }
    foreach ($file in [IO.Directory]::EnumerateFiles($Source, '*', [IO.SearchOption]::AllDirectories))
    {
        $relative = [IO.Path]::GetRelativePath($Source, $file)
        $target = Join-Path $Destination $relative
        if ([IO.File]::Exists($target)) { throw "Fixture overlay would overwrite a file: $target" }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        [IO.File]::Copy($file, $target, $false)
        Assert-SingleLinkFile -Path $target
    }
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
        [Parameter(Mandatory = $true)][ValidateSet('A', 'B')][string]$InitialGeneration,
        [Parameter(Mandatory = $true)][ValidatePattern('^TT-0[1-8]$')][string]$ControlCaseId
    )

    $project = Join-Path $Root 'UnityProject'
    $authority = Join-Path $Root 'Authority'
    $control = Join-Path $Root 'Control'
    $evidence = Join-Path $Root 'Evidence'
    Copy-DirectoryTree -Source (Join-Path $script:D0M2FRoot 'Fixture~') -Destination $project
    Copy-OverlayTree -Source (Join-Path $script:ScriptRoot 'FixtureOverlay~') -Destination $project
    foreach ($path in @($authority, $control, $evidence, (Join-Path $project 'Packages'), (Join-Path $project 'Library')))
    {
        [IO.Directory]::CreateDirectory($path) | Out-Null
    }

    $indexPath = Join-Path $script:D0M2FRoot 'Archives~/ArchiveIndex.json'
    $index = Read-ArchiveIndex -IndexPath $indexPath
    foreach ($archive in @($index.Archives))
    {
        $source = Join-Path (Join-Path $script:D0M2FRoot 'Archives~') ([string]$archive.FileName)
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
    Register-HarnessControlPath -CaseId $ControlCaseId -Path (Join-Path $control 'manifest-A.json') -Kind 'ManifestAControl' | Out-Null
    Register-HarnessControlPath -CaseId $ControlCaseId -Path (Join-Path $control 'manifest-B.json') -Kind 'ManifestBControl' | Out-Null
    $initial = if ($InitialGeneration -eq 'B') { $manifestB.Content } else { $manifestA.Content }
    Write-NewTextFile -Path (Join-Path $project 'Packages/manifest.json') -Content $initial

    Assert-OwnedFixtureRoot -Root $Root
    return [pscustomobject]@{
        Project = $project; Authority = $authority; Control = $control; Evidence = $evidence
        IndexPath = $indexPath; Index = $index; ArchiveA = $archiveA; ArchiveB = $archiveB
        ManifestA = $manifestA; ManifestB = $manifestB
    }
}

# 启动覆盖整个 fixture 的单 watcher，避免不同角色 watcher 窗口不一致。
function Start-FixtureWatcher
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $prefix = 'D0M2TTarballWatcher-' + [Guid]::NewGuid().ToString('N')
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
        [Parameter(Mandatory = $true)][string]$ControlPath,
        [Parameter(Mandatory = $true)][ValidatePattern('^TT-0[1-8]$')][string]$CaseId
    )

    $canary = Join-Path $ControlPath ('watcher-canary-' + [Guid]::NewGuid().ToString('N'))
    [IO.File]::WriteAllText($canary, 'canary', $script:Utf8NoBom)
    Register-HarnessControlPath -CaseId $CaseId -Path $canary -Kind 'WatcherCanary' | Out-Null
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

# 启动一次 direct Unity，持久化 raw 日志/观察，并可在首次 B resolve 事件后强杀。
function Invoke-UnityObservation
{
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][object]$Archive,
        [Parameter(Mandatory = $true)][ValidatePattern('^TT-0[1-8]$')][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$ObservationName,
        [Parameter(Mandatory = $true)][object]$WatcherHandle,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[object]]$WatcherRecords,
        [switch]$KillOnResolve
    )

    $caseDirectory = $CaseId.ToLowerInvariant()
    $resultRelative = "$caseDirectory/$ObservationName-unity.json"
    $logRelative = "$caseDirectory/$ObservationName-unity.log"
    $processRelative = "$caseDirectory/$ObservationName-process.json"
    $resultPath = Resolve-EvidencePath -RelativePath $resultRelative
    $logPath = Resolve-EvidencePath -RelativePath $logRelative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resultPath)) | Out-Null
    $arguments = @(
        '-batchmode', '-nographics', '-projectPath', $ProjectPath,
        '-executeMethod', 'GAS.Tests.D0M2T.Tarball.D0M2TTarballUnityProbe.Run',
        '-d0m2tOutput', $resultPath,
        '-d0m2tExpectedGeneration', [string]$Archive.GenerationId,
        '-d0m2tExpectedArchiveSha', [string]$Archive.Sha256,
        '-logFile', $logPath
    )
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = New-ProcessStartInfo -FilePath $Executable -Arguments $arguments
    if ($KillOnResolve) { Receive-FixtureWatcherEvents -Handle $WatcherHandle -Records $WatcherRecords }
    $eventOffset = $WatcherRecords.Count
    $startedAt = [DateTime]::UtcNow
    $started = $process.Start()
    if (-not $started) { throw 'Unity process did not start.' }
    $script:OwnedProcessIds.Add($process.Id)
    $timedOut = $false
    $killedOnResolve = $false
    $resultExistedAtKill = $false
    $killEvent = $null
    try
    {
        $deadline = $startedAt.AddMilliseconds($script:ProbeTimeoutMilliseconds)
        while (-not $process.HasExited)
        {
            Receive-FixtureWatcherEvents -Handle $WatcherHandle -Records $WatcherRecords
            if ($KillOnResolve -and -not $killedOnResolve)
            {
                $newEvents = @($WatcherRecords | Select-Object -Skip $eventOffset)
                $killEvent = @($newEvents | Where-Object {
                    -not [string]::IsNullOrWhiteSpace([string]$_.FullPath) -and
                    $_.FullPath.IndexOf('Library\PackageCache', [StringComparison]::OrdinalIgnoreCase) -ge 0 -and
                    $_.FullPath.IndexOf($script:PackageName, [StringComparison]::OrdinalIgnoreCase) -ge 0
                } | Select-Object -First 1)
                if ($killEvent.Count -eq 1)
                {
                    $resultExistedAtKill = [IO.File]::Exists($resultPath)
                    $process.Kill($true)
                    $killedOnResolve = $true
                    if (-not $process.WaitForExit(30000)) { throw 'Unity process tree survived the resolve kill.' }
                    break
                }
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
        $record = [pscustomobject][ordered]@{
            Schema = 'D0M2T-UnityProcess-v1'; RunId = $RunId; CaseId = $CaseId; Name = $ObservationName
            ExitCode = if ($process.HasExited) { $process.ExitCode } else { -1 }
            TimedOut = $timedOut; KilledOnResolve = $killedOnResolve; ResultExistedAtKill = $resultExistedAtKill
            KillEvent = if ($null -eq $killEvent -or $killEvent.Count -eq 0) { $null } else { $killEvent[0] }
            DurationMilliseconds = [int]([DateTime]::UtcNow - $startedAt).TotalMilliseconds
            ResultPath = $resultRelative; LogPath = $logRelative
        }
        Write-EvidenceJson -RelativePath $processRelative -Value $record | Out-Null
        $ids = [Collections.Generic.List[string]]::new()
        $ids.Add((Register-EvidenceFile -EvidenceId "$CaseId-$ObservationName-process" -CaseId $CaseId -Kind 'ProcessLog' -RelativePath $processRelative))
        if ([IO.File]::Exists($logPath))
        {
            $ids.Add((Register-EvidenceFile -EvidenceId "$CaseId-$ObservationName-log" -CaseId $CaseId -Kind 'ProcessLog' -RelativePath $logRelative))
        }
        if ([IO.File]::Exists($resultPath))
        {
            $ids.Add((Register-EvidenceFile -EvidenceId "$CaseId-$ObservationName-unity" -CaseId $CaseId -Kind 'UnityObservation' -RelativePath $resultRelative))
        }
        return [pscustomobject]@{
            Name = $ObservationName; ExitCode = $record.ExitCode; TimedOut = $timedOut
            KilledOnResolve = $killedOnResolve; ResultExistedAtKill = $resultExistedAtKill
            DurationMilliseconds = $record.DurationMilliseconds; Payload = $payload; EvidenceIds = @($ids)
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

# 核对 Unity raw payload 的版本、三程序集 marker 与 PackageCache 物理来源。
function Test-UnityPayload
{
    param([Parameter(Mandatory = $true)][object]$Observation, [Parameter(Mandatory = $true)][object]$Archive, [Parameter(Mandatory = $true)][string]$ProjectPath)

    if ($Observation.TimedOut -or $null -eq $Observation.Payload)
    {
        return [pscustomobject]@{
            Passed = $false; TimedOut = [bool]$Observation.TimedOut; IdentityMatches = $false
            AssemblySetMatches = $false; AllGeneration = $false; CacheMatches = $false
            Detail = 'Unity payload is missing.'
        }
    }
    $payload = $Observation.Payload
    $assemblies = @($payload.Assemblies)
    $generation = [string]$Archive.GenerationId
    $token = [string]$Archive.GenerationToken
    $names = @($assemblies | ForEach-Object Name | Sort-Object)
    $expectedNames = @('com.exhard.exgas.autochessdemo', 'com.exhard.exgas.generated.editor', 'com.exhard.exgas.generated.runtime')
    $assemblySetMatches = ($names -join '|') -ceq ($expectedNames -join '|')
    $allGeneration = $assemblies.Count -eq 3 -and @($assemblies | Where-Object { $_.Generation -cne $generation -or $_.GenerationToken -cne $token }).Count -eq 0
    $cacheRoot = Resolve-NormalizedPath -Path (Join-Path $ProjectPath 'Library/PackageCache')
    $resolved = if ([string]::IsNullOrWhiteSpace([string]$payload.ResolvedPackagePath)) { '' } else { Resolve-NormalizedPath -Path ([string]$payload.ResolvedPackagePath) }
    $cacheMatches = $resolved.StartsWith($cacheRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
    $identity = $payload.UnityVersion -ceq $script:ExpectedUnityVersion -and $payload.PackageName -ceq $script:PackageName -and $payload.PackageVersion -ceq [string]$Archive.PackageVersion
    $passed = $Observation.ExitCode -eq 0 -and $payload.Passed -and $identity -and $assemblySetMatches -and $allGeneration -and $cacheMatches
    return [pscustomobject]@{ Passed = $passed; TimedOut = $false; IdentityMatches = $identity; AssemblySetMatches = $assemblySetMatches; AllGeneration = $allGeneration; CacheMatches = $cacheMatches; Detail = "identity=$identity; assemblies=$assemblySetMatches; generation=$allGeneration; cache=$cacheMatches" }
}

# 创建冻结名称、状态和证据 ID 的 TT case 结果。
function New-CaseResult
{
    param([Parameter(Mandatory = $true)][string]$CaseId, [Parameter(Mandatory = $true)][string]$Name)

    return [pscustomobject][ordered]@{ CaseId = $CaseId; Name = $Name; Status = 'NotRun'; Reason = 'NotRun'; EvidenceIds = @(); Evidence = [pscustomobject]@{ Detail = 'Not evaluated.' } }
}

# 覆盖一个 TT case 的终态及独立 checker 可重算的摘要。
function Set-CaseResult
{
    param([Parameter(Mandatory = $true)][object[]]$Cases, [Parameter(Mandatory = $true)][string]$CaseId, [Parameter(Mandatory = $true)][string]$Status, [Parameter(Mandatory = $true)][string]$Reason, [Parameter(Mandatory = $true)][string[]]$EvidenceIds, [Parameter(Mandatory = $true)][object]$Evidence)

    $case = @($Cases | Where-Object CaseId -CEQ $CaseId)
    if ($case.Count -ne 1) { throw "Case is not unique: $CaseId" }
    $case[0].Status = $Status; $case[0].Reason = $Reason; $case[0].EvidenceIds = @($EvidenceIds); $case[0].Evidence = $Evidence
}

# 固定 Authority、Derived 与 Cache 角色，只有 manifest 声明 Unity selector 权威。
function New-RoleObservations
{
    param([Parameter(Mandatory = $true)][object]$Context)

    return @(
        [pscustomobject]@{ Path = (Join-Path $Context.Authority ([string]$Context.ArchiveA.FileName)); Role = 'Authority'; ConsumerAuthority = 'Payload'; UnityConsumerAuthority = 0; NeverFallbackSelector = $true },
        [pscustomobject]@{ Path = (Join-Path $Context.Authority ([string]$Context.ArchiveB.FileName)); Role = 'Authority'; ConsumerAuthority = 'Payload'; UnityConsumerAuthority = 0; NeverFallbackSelector = $true },
        [pscustomobject]@{ Path = (Join-Path $Context.Project 'Packages/manifest.json'); Role = 'Authority'; ConsumerAuthority = 'SoleUnitySelector'; UnityConsumerAuthority = 1; NeverFallbackSelector = $false },
        [pscustomobject]@{ Path = (Join-Path $Context.Project 'Packages/packages-lock.json'); Role = 'Derived'; ConsumerAuthority = 'None'; UnityConsumerAuthority = 0; NeverFallbackSelector = $true },
        [pscustomobject]@{ Path = (Join-Path $Context.Project 'ProjectSettings/GasCodeGen/ActiveGenerationRef.json'); Role = 'Derived'; ConsumerAuthority = 'AuditOnly'; UnityConsumerAuthority = 0; NeverFallbackSelector = $true },
        [pscustomobject]@{ Path = (Join-Path $Context.Project 'Library/PackageCache'); Role = 'Cache'; ConsumerAuthority = 'None'; UnityConsumerAuthority = 0; NeverFallbackSelector = $true },
        [pscustomobject]@{ Path = (Join-Path $Context.Project 'Library/Bee'); Role = 'Cache'; ConsumerAuthority = 'None'; UnityConsumerAuthority = 0; NeverFallbackSelector = $true }
    )
}

# 注册一个实际创建的 harness/fault control，并绑定当时存在性与文件 bytes。
function Register-HarnessControlPath
{
    param(
        [Parameter(Mandatory = $true)][ValidatePattern('^TT-0[1-8]$')][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Kind
    )

    $normalized = Resolve-NormalizedPath -Path $Path
    $isFile = [IO.File]::Exists($normalized); $isDirectory = [IO.Directory]::Exists($normalized)
    if (-not $isFile -and -not $isDirectory) { throw "Harness control was not observed: $normalized" }
    if (@($script:HarnessControlObservations | Where-Object { $_.CaseId -ceq $CaseId -and $_.Path -ceq $normalized -and $_.Kind -ceq $Kind }).Count -ne 0)
    {
        throw "Duplicate harness control observation: $CaseId/$Kind/$normalized"
    }
    $row = [pscustomobject][ordered]@{
        CaseId = $CaseId; Path = $normalized; Kind = $Kind; ConsumerAuthority = 'None'
        UnityConsumerAuthority = 0; NeverFallbackSelector = $true; Observed = $true
        ObservedLength = if ($isFile) { [long](Get-Item -LiteralPath $normalized).Length } else { 0L }
        ObservedSha256 = if ($isFile) { Get-FileSha256 -Path $normalized } else { '' }
        RemovedAfterCleanup = $false
    }
    $script:HarnessControlObservations.Add($row)
    return $row
}

# 以当前物理状态冻结 harness control 的最终移除事实，供 TT07 raw 与 aggregate 同源引用。
function Get-FinalHarnessControlObservations
{
    return @($script:HarnessControlObservations | ForEach-Object {
        [pscustomobject][ordered]@{
            CaseId = $_.CaseId; Path = $_.Path; Kind = $_.Kind; ConsumerAuthority = $_.ConsumerAuthority
            UnityConsumerAuthority = $_.UnityConsumerAuthority; NeverFallbackSelector = $_.NeverFallbackSelector
            Observed = $_.Observed; ObservedLength = $_.ObservedLength; ObservedSha256 = $_.ObservedSha256
            RemovedAfterCleanup = -not [IO.File]::Exists([string]$_.Path) -and -not [IO.Directory]::Exists([string]$_.Path)
        }
    })
}

# 计算稳定 UTF-8 文本的 SHA-256，用于目录快照聚合。
function Get-StringSha256
{
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([Convert]::ToHexString($algorithm.ComputeHash($script:Utf8NoBom.GetBytes($Text)))).ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

# 快照 A/B authority 的精确 SHA、长度、hardlink 数和只读属性。
function Get-AuthoritySnapshot
{
    param([Parameter(Mandatory = $true)][object]$Context)

    $rows = foreach ($archive in @($Context.ArchiveA, $Context.ArchiveB))
    {
        $path = Join-Path $Context.Authority ([string]$archive.FileName)
        [pscustomobject][ordered]@{
            GenerationId = [string]$archive.GenerationId; FileName = [string]$archive.FileName
            ExpectedSha256 = [string]$archive.Sha256; ActualSha256 = Get-FileSha256 -Path $path
            ExpectedLength = [long]$archive.Length; ActualLength = [long](Get-Item -LiteralPath $path).Length
            LinkCount = [int][D0M2TTarballNativeFileInfo]::GetLinkCount($path)
            ReadOnly = ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReadOnly) -ne 0
        }
    }
    return @($rows)
}

# 对单个受保护文件或目录生成内容绑定，不采信时间戳。
function Get-ProtectedPathBinding
{
    param([Parameter(Mandatory = $true)][string]$Kind, [Parameter(Mandatory = $true)][string]$RelativePath)

    $path = Resolve-NormalizedPath -Path (Join-Path $script:ProjectRoot $RelativePath)
    if ($Kind -eq 'File')
    {
        if (-not [IO.File]::Exists($path)) { return [pscustomobject]@{ Kind = $Kind; Path = $RelativePath; Exists = $false; Digest = ''; FileCount = 0 } }
        return [pscustomobject]@{ Kind = $Kind; Path = $RelativePath; Exists = $true; Digest = Get-FileSha256 -Path $path; FileCount = 1 }
    }
    if (-not [IO.Directory]::Exists($path)) { return [pscustomobject]@{ Kind = $Kind; Path = $RelativePath; Exists = $false; Digest = ''; FileCount = 0 } }
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($file in @(Get-ChildItem -LiteralPath $path -File -Force -Recurse | Sort-Object FullName))
    {
        $relative = [IO.Path]::GetRelativePath($path, $file.FullName).Replace('\', '/')
        $lines.Add("$relative|$($file.Length)|$(Get-FileSha256 -Path $file.FullName)")
    }
    return [pscustomobject]@{ Kind = $Kind; Path = $RelativePath; Exists = $true; Digest = Get-StringSha256 -Text ($lines -join "`n"); FileCount = $lines.Count }
}

# 按冻结 contract 的精确 protected specs 生成生产工作区 before/after 快照。
function Get-ProtectedSnapshot
{
    param([Parameter(Mandatory = $true)][string]$Phase, [Parameter(Mandatory = $true)][object]$Contract)

    $bindings = foreach ($spec in @($Contract.ProtectedPathSpecs))
    {
        Get-ProtectedPathBinding -Kind ([string]$spec.Kind) -RelativePath ([string]$spec.Path)
    }
    return [pscustomobject][ordered]@{ Schema = 'D0M2T-ProtectedSnapshot-v1'; RunId = $RunId; Phase = $Phase; CapturedAtUtc = [DateTime]::UtcNow.ToString('O'); Bindings = @($bindings) }
}

# 比较 protected before/after 的规范化绑定，不依赖采集时间字段。
function Test-ProtectedUnchanged
{
    param([Parameter(Mandatory = $true)][object]$Before, [Parameter(Mandatory = $true)][object]$After)

    $beforeRows = @($Before.Bindings | ForEach-Object { "$($_.Kind)|$($_.Path)|$($_.Exists)|$($_.Digest)|$($_.FileCount)" } | Sort-Object)
    $afterRows = @($After.Bindings | ForEach-Object { "$($_.Kind)|$($_.Path)|$($_.Exists)|$($_.Digest)|$($_.FileCount)" } | Sort-Object)
    return ($beforeRows -join "`n") -ceq ($afterRows -join "`n")
}

# 快照 PackageCache 与 Bee response-file 状态，证明 stale A 存在但不是 selector。
function Get-CacheBeeSnapshot
{
    param([Parameter(Mandatory = $true)][string]$ProjectPath, [Parameter(Mandatory = $true)][string]$Phase)

    $cacheRoot = Join-Path $ProjectPath 'Library/PackageCache'
    $beeRoot = Join-Path $ProjectPath 'Library/Bee'
    $cacheEntries = if ([IO.Directory]::Exists($cacheRoot))
    {
        @(Get-ChildItem -LiteralPath $cacheRoot -Directory -Force | Where-Object Name -Like ($script:PackageName + '@*') | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Path = $_.FullName } })
    }
    else { @() }
    $rspEntries = [Collections.Generic.List[object]]::new()
    if ([IO.Directory]::Exists($beeRoot))
    {
        foreach ($file in @(Get-ChildItem -LiteralPath $beeRoot -File -Force -Recurse -Filter '*.rsp'))
        {
            $text = [IO.File]::ReadAllText($file.FullName)
            $rspEntries.Add([pscustomobject]@{
                Path = [IO.Path]::GetRelativePath($ProjectPath, $file.FullName).Replace('\', '/')
                Length = [long]$file.Length; Sha256 = Get-FileSha256 -Path $file.FullName
                ContainsGenerationA = $text.Contains('d0m2f-tarball-generation-a')
                ContainsGenerationB = $text.Contains('d0m2f-tarball-generation-b')
            })
        }
    }
    return [pscustomobject][ordered]@{ Schema = 'D0M2T-CacheBeeSnapshot-v1'; RunId = $RunId; Phase = $Phase; CacheEntries = @($cacheEntries); RspEntries = @($rspEntries) }
}

# 创建受控 selector worker 脚本，用于在 File.Replace 前后真实强杀子进程。
function Initialize-SelectorWorker
{
    param([Parameter(Mandatory = $true)][string]$ControlPath)

    $path = Join-Path $ControlPath 'selector-worker.ps1'
    if ([IO.File]::Exists($path)) { return $path }
    $source = @'
[CmdletBinding()]
param([string]$Manifest,[string]$Next,[string]$Backup,[string]$Source,[string]$Checkpoint,[ValidateSet('BeforeReplace','AfterReplace')][string]$Phase)
$ErrorActionPreference='Stop'
$content=[IO.File]::ReadAllText($Source)
$bytes=[Text.UTF8Encoding]::new($false).GetBytes($content)
$stream=[IO.FileStream]::new($Next,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try{$stream.Write($bytes,0,$bytes.Length);$stream.Flush($true)}finally{$stream.Dispose()}
if($Phase -eq 'BeforeReplace')
{
    [IO.File]::WriteAllText($Checkpoint,'BeforeReplace',[Text.UTF8Encoding]::new($false))
    Start-Sleep -Seconds 300
    exit 0
}
[IO.File]::Replace($Next,$Manifest,$Backup,$true)
[IO.File]::WriteAllText($Checkpoint,'AfterReplace',[Text.UTF8Encoding]::new($false))
Start-Sleep -Seconds 300
'@
    Write-NewTextFile -Path $path -Content $source
    Register-HarnessControlPath -CaseId 'TT-02' -Path $path -Kind 'SelectorWorker' | Out-Null
    return $path
}

# 运行一个 selector 强杀相位并返回可由 checker 重算的 manifest 完整性观察。
function Invoke-SelectorKillPhase
{
    param([Parameter(Mandatory = $true)][object]$Context, [Parameter(Mandatory = $true)][ValidateSet('BeforeReplace', 'AfterReplace')][string]$Phase)

    $manifest = Join-Path $Context.Project 'Packages/manifest.json'
    $next = $manifest + '.next'; $backup = $manifest + '.backup'; $checkpoint = Join-Path $Context.Control ("selector-$Phase.checkpoint")
    foreach ($stale in @($next, $backup, $checkpoint)) { if ([IO.File]::Exists($stale)) { [IO.File]::Delete($stale) } }
    $worker = Initialize-SelectorWorker -ControlPath $Context.Control
    $source = Join-Path $Context.Control 'manifest-B.json'
    $executable = (Get-Process -Id $PID).Path
    $arguments = @('-NoProfile', '-File', $worker, '-Manifest', $manifest, '-Next', $next, '-Backup', $backup, '-Source', $source, '-Checkpoint', $checkpoint, '-Phase', $Phase)
    $process = [Diagnostics.Process]::new(); $process.StartInfo = New-ProcessStartInfo -FilePath $executable -Arguments $arguments
    if (-not $process.Start()) { throw 'Selector worker did not start.' }
    $script:OwnedProcessIds.Add($process.Id)
    $deadline = [DateTime]::UtcNow.AddSeconds(30); $checkpointObserved = $false; $killed = $false
    $result = $null; $checkpointControl = $null; $transactionRows = [Collections.Generic.List[object]]::new()
    try
    {
        while (-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
        {
            if ([IO.File]::Exists($checkpoint))
            {
                $checkpointObserved = $true; $process.Kill($true); $killed = $process.WaitForExit(30000); break
            }
            Start-Sleep -Milliseconds 50
        }
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit(30000) | Out-Null }
        if ([IO.File]::Exists($checkpoint)) { $checkpointControl = Register-HarnessControlPath -CaseId 'TT-02' -Path $checkpoint -Kind 'SelectorWorkerCheckpoint' }
        $content = [IO.File]::ReadAllText($manifest); $sha = Get-FileSha256 -Path $manifest
        $shaA = Get-FileSha256 -Path (Join-Path $Context.Control 'manifest-A.json'); $shaB = Get-FileSha256 -Path (Join-Path $Context.Control 'manifest-B.json')
        try { $content | ConvertFrom-Json | Out-Null; $parseable = $true } catch { $parseable = $false }
        foreach ($artifact in @([pscustomobject]@{ Path = $next; Kind = 'ManifestNext' }, [pscustomobject]@{ Path = $backup; Kind = 'ManifestBackup' }))
        {
            $exists = [IO.File]::Exists([string]$artifact.Path)
            $transactionRows.Add([pscustomobject][ordered]@{
                CaseId = 'TT-02'; Phase = $Phase; Path = (Resolve-NormalizedPath -Path ([string]$artifact.Path)); Kind = [string]$artifact.Kind
                Role = 'Derived'; ConsumerAuthority = 'None'; UnityConsumerAuthority = 0; NeverFallbackSelector = $true
                ExistedAfterKill = $exists; ObservedLength = if ($exists) { [long](Get-Item -LiteralPath $artifact.Path).Length } else { 0L }
                ObservedSha256 = if ($exists) { Get-FileSha256 -Path $artifact.Path } else { '' }; RemovedAfterCleanup = $false
            })
        }
        $result = [pscustomobject][ordered]@{
            Phase = $Phase; CheckpointObserved = $checkpointObserved; CheckpointPath = (Resolve-NormalizedPath -Path $checkpoint)
            Killed = $killed; WorkerPath = $worker; ProcessId = $process.Id; Started = $true; ExitedAfterKill = $process.HasExited
            ExitCode = if ($process.HasExited) { $process.ExitCode } else { -1 }; ManifestSha256 = $sha
            IsManifestA = $sha -ceq $shaA; IsManifestB = $sha -ceq $shaB; Parseable = $parseable
            Length = [long](Get-Item -LiteralPath $manifest).Length; TransactionArtifacts = @($transactionRows)
        }
    }
    finally
    {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit(30000) | Out-Null }
        $process.Dispose()
        foreach ($transactionPath in @($next, $backup, $checkpoint)) { if ([IO.File]::Exists($transactionPath)) { [IO.File]::Delete($transactionPath) } }
        foreach ($row in @($transactionRows))
        {
            $row.RemovedAfterCleanup = -not [IO.File]::Exists([string]$row.Path)
            $script:TransientArtifactObservations.Add($row)
        }
        if ($null -ne $checkpointControl) { $checkpointControl.RemovedAfterCleanup = -not [IO.File]::Exists($checkpoint) }
    }
    return $result
}

# 对一个 manifest 文本执行 exact content-addressed tarball gate，拒绝别名、损坏与逃逸。
function Test-CanonicalManifest
{
    param([Parameter(Mandatory = $true)][string]$Content, [Parameter(Mandatory = $true)][object[]]$Archives)

    try { $manifest = $Content | ConvertFrom-Json } catch { return [pscustomobject]@{ Accepted = $false; Reason = 'InvalidJson' } }
    $dependencies = @($manifest.dependencies.PSObject.Properties)
    $property = @($dependencies | Where-Object Name -CEQ $script:PackageName)
    if ($dependencies.Count -ne 1 -or $property.Count -ne 1) { return [pscustomobject]@{ Accepted = $false; Reason = 'DependencySetMismatch' } }
    $value = [string]$property[0].Value
    if ($value -cnotmatch '^file:\.\./\.\./Authority/([0-9a-f]{64}\.tgz)$') { return [pscustomobject]@{ Accepted = $false; Reason = 'NonCanonicalUri' } }
    $fileName = $Matches[1]; $archive = @($Archives | Where-Object FileName -CEQ $fileName)
    if ($archive.Count -ne 1 -or ([string]$archive[0].Sha256 + '.tgz') -cne $fileName) { return [pscustomobject]@{ Accepted = $false; Reason = 'UnknownArchive' } }
    return [pscustomobject]@{ Accepted = $true; Reason = 'None'; GenerationId = [string]$archive[0].GenerationId; FileName = $fileName }
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

# 将异常窗口已经落盘但尚未注册的 raw 文件纳入 partial evidence 闭包。
function Register-UntrackedEvidenceFiles
{
    if (-not [IO.Directory]::Exists($script:ResolvedEvidenceRoot)) { return }
    $knownPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($evidence in @($script:EvidenceFiles)) { [void]$knownPaths.Add([string]$evidence.Path) }
    foreach ($file in @(Get-ChildItem -LiteralPath $script:ResolvedEvidenceRoot -File -Force -Recurse | Sort-Object FullName))
    {
        $relative = [IO.Path]::GetRelativePath($script:ResolvedEvidenceRoot, $file.FullName).Replace('\', '/')
        if ($knownPaths.Contains($relative)) { continue }
        $segment = @($relative.Split('/'))[0]
        if ($segment -cnotmatch '^tt-0[1-8]$') { throw "Untracked raw file has no case boundary: $relative" }
        $caseId = $segment.ToUpperInvariant()
        $kind = if ($relative.EndsWith('-unity.json', [StringComparison]::Ordinal)) { 'UnityObservation' } else { 'ProcessLog' }
        $suffix = (Get-StringSha256 -Text $relative).Substring(0, 16)
        [void](Register-EvidenceFile -EvidenceId "$caseId-recovered-$suffix" -CaseId $caseId -Kind $kind -RelativePath $relative)
        [void]$knownPaths.Add($relative)
    }
}

# 让 partial aggregate 的每个 case 精确引用自身全部已声明证据。
function Sync-PartialCaseEvidence
{
    param([Parameter(Mandatory = $true)][object[]]$Cases, [Parameter(Mandatory = $true)][string]$Reason)

    foreach ($case in $Cases)
    {
        $ids = @($script:EvidenceFiles | Where-Object CaseId -CEQ ([string]$case.CaseId) | ForEach-Object EvidenceId)
        $case.EvidenceIds = @($ids)
        if ([string]$case.Status -ceq 'NotRun' -and $ids.Count -gt 0)
        {
            $case.Status = 'Inconclusive'; $case.Reason = $Reason
        }
    }
    if (@($Cases | Where-Object { [string]$_.Status -cin @('Inconclusive', 'NotRun') }).Count -eq 0)
    {
        $tt08 = @($Cases | Where-Object CaseId -CEQ 'TT-08')[0]
        $tt08.Status = 'Inconclusive'; $tt08.Reason = $Reason
    }
}

# 为异常路径构造可被公共 checker 接受的最小 typed partial aggregate。
function New-PartialAggregate
{
    param(
        [Parameter(Mandatory = $true)][string]$Reason,
        [Parameter(Mandatory = $true)][object[]]$Cases,
        [Parameter(Mandatory = $true)][object]$Contract,
        [Parameter(Mandatory = $true)][string]$ResolvedUnity
    )

    $archiveIndexPath = Join-Path $script:D0M2FRoot 'Archives~/ArchiveIndex.json'
    $residualRoots = @($script:CreatedFixtureRoots | Where-Object { [IO.Directory]::Exists($_) })
    return [ordered]@{
        Schema = $script:Schema; RunId = $RunId; Status = 'Inconclusive'; Reason = $Reason
        SelectedRoute = 'ImmutableTarball'; SoleUnityConsumedSelector = 'Packages/manifest.json'; Cases = $Cases
        Inputs = [ordered]@{ UnityPath = $ResolvedUnity; UnitySha256 = Get-FileSha256 -Path $ResolvedUnity; HarnessSha256 = Get-FileSha256 -Path $PSCommandPath; ArchiveIndexSha256 = Get-FileSha256 -Path $archiveIndexPath; FrozenInputs = @($Contract.FrozenInputs); FrozenRecordSha256 = [string]$Contract.FrozenRecordSha256 }
        Authority = [ordered]@{}; Selector = [ordered]@{}; Unity = [ordered]@{}; RoleObservations = @()
        TransientArtifactObservations = @(); HarnessControlObservations = @()
        Fixture = [ordered]@{ Kept = [bool]$KeepFixtures; CreatedRoots = @($script:CreatedFixtureRoots); RemovedRoots = @($script:RemovedFixtureRoots); ResidualRoots = $residualRoots; CleanupPassed = -not $KeepFixtures -and $residualRoots.Count -eq 0 }
        EvidenceFiles = @($script:EvidenceFiles); Protected = [ordered]@{ Unchanged = $false }
        D1Authorized = $false; ProductionInstallAdmission = 'NotEvaluated'; DeclaredFullSemanticEligibility = $false; NextGate = 'D0-M2R'
    }
}

# 遍历全局 Created-Removed 集合，并仅通过 owner validator 清理剩余 fixture。
function Remove-RemainingOwnedFixtureRoots
{
    $removed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($root in @($script:RemovedFixtureRoots)) { [void]$removed.Add((Resolve-NormalizedPath -Path $root)) }
    $failures = [Collections.Generic.List[string]]::new()
    if (-not $KeepFixtures)
    {
        foreach ($root in @($script:CreatedFixtureRoots | Select-Object -Unique))
        {
            $normalized = Resolve-NormalizedPath -Path $root
            if ($removed.Contains($normalized) -or -not [IO.Directory]::Exists($normalized)) { continue }
            try { Remove-OwnedFixtureRoot -Root $normalized; [void]$removed.Add($normalized) }
            catch { $failures.Add("$normalized :: $($_.Exception.Message)") }
        }
    }
    $remaining = @($script:CreatedFixtureRoots | Select-Object -Unique | Where-Object { [IO.Directory]::Exists($_) })
    return [pscustomobject]@{ Failed = $failures.Count -gt 0; Failures = $failures.ToArray(); Remaining = $remaining }
}


# 执行缺失/损坏 authority 的独立 warm-cache fail-closed 实验。
function Invoke-MissingCorruptAuthorityCase
{
    param([Parameter(Mandatory = $true)][string]$Executable)

    $root = New-OwnedFixtureRoot -CaseId 'TT-04'; $context = Initialize-Fixture -Root $root -InitialGeneration 'B' -ControlCaseId 'TT-04'
    $events = [Collections.Generic.List[object]]::new(); $watcher = Start-FixtureWatcher -Root $root
    Invoke-WatcherCanary -Handle $watcher -Records $events -ControlPath $context.Control -CaseId 'TT-04' | Out-Null
    $ids = [Collections.Generic.List[string]]::new()
    try
    {
        $warm = Invoke-UnityObservation -Executable $Executable -ProjectPath $context.Project -Archive $context.ArchiveB -CaseId 'TT-04' -ObservationName 'warm-b' -WatcherHandle $watcher -WatcherRecords $events
        foreach ($id in @($warm.EvidenceIds)) { $ids.Add($id) }
        $warmValidation = Test-UnityPayload -Observation $warm -Archive $context.ArchiveB -ProjectPath $context.Project
        if ($null -eq $warm.Payload)
        {
            $ids.Add((Add-JsonEvidence -EvidenceId 'TT-04-warm-b-missing-unity' -CaseId 'TT-04' -Kind 'UnityObservation' -RelativePath 'tt-04/warm-b-missing-unity.json' -Value ([pscustomobject]@{ Schema = 'D0M2T-MissingUnityObservation-v1'; RunId = $RunId; ExpectedGeneration = 'B'; ResultPresent = $false })))
        }
        $archivePath = Join-Path $context.Authority ([string]$context.ArchiveB.FileName)
        $missingPath = Join-Path $context.Control 'selected-authority.missing'
        [IO.File]::Move($archivePath, $missingPath)
        Register-HarnessControlPath -CaseId 'TT-04' -Path $missingPath -Kind 'MissingAuthorityHolding' | Out-Null
        $missing = Invoke-UnityObservation -Executable $Executable -ProjectPath $context.Project -Archive $context.ArchiveB -CaseId 'TT-04' -ObservationName 'missing-b' -WatcherHandle $watcher -WatcherRecords $events
        foreach ($id in @($missing.EvidenceIds)) { $ids.Add($id) }
        [IO.File]::Move($missingPath, $archivePath); [IO.File]::SetAttributes($archivePath, [IO.FileAttributes]::ReadOnly)

        [IO.File]::SetAttributes($archivePath, [IO.FileAttributes]::Normal)
        $bytes = [IO.File]::ReadAllBytes($archivePath); $bytes[0] = $bytes[0] -bxor 0x01; [IO.File]::WriteAllBytes($archivePath, $bytes)
        $corruptSha = Get-FileSha256 -Path $archivePath
        $corrupt = Invoke-UnityObservation -Executable $Executable -ProjectPath $context.Project -Archive $context.ArchiveB -CaseId 'TT-04' -ObservationName 'corrupt-b' -WatcherHandle $watcher -WatcherRecords $events
        foreach ($id in @($corrupt.EvidenceIds)) { $ids.Add($id) }
        [IO.File]::Delete($archivePath)
        [IO.File]::Copy((Join-Path $script:D0M2FRoot ('Archives~/' + [string]$context.ArchiveB.FileName)), $archivePath, $false)
        [IO.File]::SetAttributes($archivePath, [IO.FileAttributes]::ReadOnly)

        $missingSuccessful = $missing.ExitCode -eq 0 -or ($null -ne $missing.Payload -and [bool]$missing.Payload.Passed)
        $corruptSuccessful = $corrupt.ExitCode -eq 0 -or ($null -ne $corrupt.Payload -and [bool]$corrupt.Payload.Passed)
        $record = [pscustomobject][ordered]@{
            Schema = 'D0M2T-MissingCorruptAuthority-v1'; RunId = $RunId
            WarmBaselineB = [bool]$warmValidation.Passed
            Missing = [pscustomobject]@{ ExitCode = $missing.ExitCode; SuccessfulObservation = $missingSuccessful; ResultPresent = $null -ne $missing.Payload }
            Corrupt = [pscustomobject]@{ InjectedSha256 = $corruptSha; ExpectedSha256 = [string]$context.ArchiveB.Sha256; ExitCode = $corrupt.ExitCode; SuccessfulObservation = $corruptSuccessful; ResultPresent = $null -ne $corrupt.Payload }
            CacheFallbackAccepted = $missingSuccessful -or $corruptSuccessful
            AuthorityRestored = (Get-FileSha256 -Path $archivePath) -ceq [string]$context.ArchiveB.Sha256
        }
        $faultId = Add-JsonEvidence -EvidenceId 'TT-04-fault-injection' -CaseId 'TT-04' -Kind 'FaultInjectionRecord' -RelativePath 'tt-04/missing-corrupt-authority.json' -Value $record
        $ids.Add($faultId)
        $passed = $warmValidation.Passed -and -not $missingSuccessful -and -not $corruptSuccessful -and $record.AuthorityRestored
        $timedOut = $warm.TimedOut -or $missing.TimedOut -or $corrupt.TimedOut
        $ready = -not $timedOut -and $null -ne $warm.Payload -and $warmValidation.IdentityMatches -and $warmValidation.Passed
        return [pscustomobject]@{ Root = $root; Context = $context; Ready = $ready; Passed = $passed; TimedOut = $timedOut; Record = $record; EvidenceIds = @($ids) }
    }
    finally { Stop-FixtureWatcher -Handle $watcher -Records $events }
}

# 对 hardlink、junction 与 forged sentinel 各执行一次拒绝后修复清理。
function Invoke-BoundaryCleanupCase
{
    $results = [Collections.Generic.List[object]]::new()

    $hardlinkRoot = New-OwnedFixtureRoot -CaseId 'TT-07'; $externalRoot = New-OwnedFixtureRoot -CaseId 'TT-07'
    $target = Join-Path $externalRoot 'external-target.bin'; $link = Join-Path $hardlinkRoot 'alias.bin'
    Write-NewTextFile -Path $target -Content 'boundary-canary'; New-Item -ItemType HardLink -Path $link -Target $target | Out-Null
    Register-HarnessControlPath -CaseId 'TT-07' -Path $target -Kind 'ExternalTargetCanary' | Out-Null
    Register-HarnessControlPath -CaseId 'TT-07' -Path $link -Kind 'HardlinkAttack' | Out-Null
    $targetBefore = Get-FileSha256 -Path $target; $refused = $false
    try { Remove-OwnedFixtureRoot -Root $hardlinkRoot } catch { $refused = $true }
    $targetAfter = Get-FileSha256 -Path $target; [IO.File]::Delete($link); Remove-OwnedFixtureRoot -Root $hardlinkRoot
    $externalUnchanged = $targetBefore -ceq $targetAfter; Remove-OwnedFixtureRoot -Root $externalRoot
    $results.Add([pscustomobject]@{ Attack = 'Hardlink'; Refused = $refused; ExternalTargetRoot = $externalRoot; ExternalShaBefore = $targetBefore; ExternalShaAfter = $targetAfter; ExternalUnchanged = $externalUnchanged; AttackRootCleaned = -not [IO.Directory]::Exists($hardlinkRoot); ExternalRootCleaned = -not [IO.Directory]::Exists($externalRoot); Cleaned = -not [IO.Directory]::Exists($hardlinkRoot) -and -not [IO.Directory]::Exists($externalRoot) })

    $junctionRoot = New-OwnedFixtureRoot -CaseId 'TT-07'; $junctionTarget = Join-Path $junctionRoot 'junction-target'; $junction = Join-Path $junctionRoot 'junction-alias'
    [IO.Directory]::CreateDirectory($junctionTarget) | Out-Null; New-Item -ItemType Junction -Path $junction -Target $junctionTarget | Out-Null
    Register-HarnessControlPath -CaseId 'TT-07' -Path $junctionTarget -Kind 'ReparseTargetControl' | Out-Null
    Register-HarnessControlPath -CaseId 'TT-07' -Path $junction -Kind 'ReparseAttack' | Out-Null
    $refused = $false; try { Remove-OwnedFixtureRoot -Root $junctionRoot } catch { $refused = $true }
    [IO.Directory]::Delete($junction, $false); Remove-OwnedFixtureRoot -Root $junctionRoot
    $results.Add([pscustomobject]@{ Attack = 'ReparsePoint'; Refused = $refused; ExternalUnchanged = $true; Cleaned = -not [IO.Directory]::Exists($junctionRoot) })

    $sentinelRoot = New-OwnedFixtureRoot -CaseId 'TT-07'; $sentinel = Join-Path $sentinelRoot $script:OwnerSentinelName
    $expectedSentinel = [IO.Path]::GetFileName($sentinelRoot) + '|' + $RunId + "`n"; [IO.File]::WriteAllText($sentinel, 'forged', $script:Utf8NoBom)
    Register-HarnessControlPath -CaseId 'TT-07' -Path $sentinel -Kind 'ForgedSentinelAttack' | Out-Null
    $refused = $false; try { Remove-OwnedFixtureRoot -Root $sentinelRoot } catch { $refused = $true }
    [IO.File]::WriteAllText($sentinel, $expectedSentinel, $script:Utf8NoBom); Remove-OwnedFixtureRoot -Root $sentinelRoot
    $results.Add([pscustomobject]@{ Attack = 'ForgedSentinel'; Refused = $refused; ExternalUnchanged = $true; Cleaned = -not [IO.Directory]::Exists($sentinelRoot) })

    $passed = @($results | Where-Object { -not $_.Refused -or -not $_.ExternalUnchanged -or -not $_.Cleaned }).Count -eq 0
    return [pscustomobject][ordered]@{ Schema = 'D0M2T-BoundaryCleanup-v1'; RunId = $RunId; Attacks = @($results); HardlinkRejected = [bool]$results[0].Refused; ReparseRejected = [bool]$results[1].Refused; SentinelRejected = [bool]$results[2].Refused; ExternalUnchanged = @($results | Where-Object { -not $_.ExternalUnchanged }).Count -eq 0; CleanupPassed = @($results | Where-Object { -not $_.Cleaned }).Count -eq 0; Passed = $passed }
}

$script:ScriptRoot = Resolve-NormalizedPath -Path $PSScriptRoot
$script:ProjectRoot = Resolve-NormalizedPath -Path (Join-Path $script:ScriptRoot '../../../../..')
$script:D0M2FRoot = Resolve-NormalizedPath -Path (Join-Path $script:ScriptRoot '../../D0M2F/Tarball')
$resolvedUnity = Resolve-NormalizedPath -Path $UnityPath
$resolvedOutput = if ([IO.Path]::IsPathRooted($OutputPath)) { Resolve-NormalizedPath -Path $OutputPath } else { Resolve-NormalizedPath -Path (Join-Path $script:ProjectRoot $OutputPath) }
$script:ResolvedEvidenceRoot = if ([IO.Path]::IsPathRooted($EvidenceRoot)) { Resolve-NormalizedPath -Path $EvidenceRoot } else { Resolve-NormalizedPath -Path (Join-Path $script:ProjectRoot $EvidenceRoot) }
$testResultsRoot = Resolve-NormalizedPath -Path (Join-Path $script:ProjectRoot 'TestResults')
$contractPath = Join-Path $script:ProjectRoot 'Tools/Tests/GasCodeGen/D0M2T/Contracts/D0M2T.contract.json'
$cases = @(
    New-CaseResult -CaseId 'TT-01' -Name 'BaselineAuthority'
    New-CaseResult -CaseId 'TT-02' -Name 'SelectorAtomicKill'
    New-CaseResult -CaseId 'TT-03' -Name 'ResolveKillRecovery'
    New-CaseResult -CaseId 'TT-04' -Name 'MissingCorruptAuthority'
    New-CaseResult -CaseId 'TT-05' -Name 'ManifestLockDrift'
    New-CaseResult -CaseId 'TT-06' -Name 'StaleCacheBeeRsp'
    New-CaseResult -CaseId 'TT-07' -Name 'BoundaryCleanup'
    New-CaseResult -CaseId 'TT-08' -Name 'EvidenceClosure'
)
$mainRoot = ''; $faultRoot = ''; $mainWatcher = $null; $mainEvents = [Collections.Generic.List[object]]::new()
$context = $null; $contract = $null; $protectedBefore = $null; $protectedAfter = $null
$protectedBeforeId = ''; $protectedAfterId = ''; $failureDetail = ''; $aggregate = $null
$overallStatus = 'Inconclusive'; $overallReason = 'HarnessFailure'; $exitCode = 23
$aggregateWriteFailed = $false
$caughtFailure = $false

try
{
    if (-not [IO.Path]::IsPathRooted($UnityPath) -or -not [IO.File]::Exists($resolvedUnity)) { throw "UnityPath must be an existing absolute file: $UnityPath" }
    Assert-DescendantPath -Root $testResultsRoot -Path $resolvedOutput -Label 'OutputPath'
    Assert-DescendantPath -Root $testResultsRoot -Path $script:ResolvedEvidenceRoot -Label 'EvidenceRoot'
    if ([IO.File]::Exists($resolvedOutput) -or [IO.Directory]::Exists($resolvedOutput)) { throw "OutputPath must be fresh: $resolvedOutput" }
    if ([IO.File]::Exists($script:ResolvedEvidenceRoot) -or [IO.Directory]::Exists($script:ResolvedEvidenceRoot)) { throw "EvidenceRoot must be fresh: $script:ResolvedEvidenceRoot" }
    [IO.Directory]::CreateDirectory($script:ResolvedEvidenceRoot) | Out-Null
    $contract = Get-Content -LiteralPath $contractPath -Raw | ConvertFrom-Json
    foreach ($input in @($contract.FrozenInputs))
    {
        $inputPath = Join-Path $script:ProjectRoot ([string]$input.Path)
        if ((Get-FileSha256 -Path $inputPath) -cne [string]$input.Sha256) { throw "Frozen input drift: $($input.Path)" }
    }
    $frozenRecordPath = Join-Path $script:ProjectRoot 'docs/reviews/RuntimeV1.1-D0-M2F-SpecADR规范冻结结果.md'
    if ((Get-FileSha256 -Path $frozenRecordPath) -cne [string]$contract.FrozenRecordSha256) { throw 'Frozen record raw SHA drift.' }

    $protectedBefore = Get-ProtectedSnapshot -Phase 'Before' -Contract $contract
    $protectedBeforeId = Add-JsonEvidence -EvidenceId 'TT-07-protected-before' -CaseId 'TT-07' -Kind 'ProtectedSnapshot' -RelativePath 'tt-07/protected-before.json' -Value $protectedBefore

    $mainRoot = New-OwnedFixtureRoot -CaseId 'TT-07'; $context = Initialize-Fixture -Root $mainRoot -InitialGeneration 'A' -ControlCaseId 'TT-02'
    $mainWatcher = Start-FixtureWatcher -Root $mainRoot
    $canary = Invoke-WatcherCanary -Handle $mainWatcher -Records $mainEvents -ControlPath $context.Control -CaseId 'TT-01'
    $authorityBefore = Get-AuthoritySnapshot -Context $context
    $coldA = Invoke-UnityObservation -Executable $resolvedUnity -ProjectPath $context.Project -Archive $context.ArchiveA -CaseId 'TT-01' -ObservationName 'baseline-a' -WatcherHandle $mainWatcher -WatcherRecords $mainEvents
    $coldAValidation = Test-UnityPayload -Observation $coldA -Archive $context.ArchiveA -ProjectPath $context.Project
    $lockA = Get-PackageLockObservation -ProjectPath $context.Project -Archive $context.ArchiveA
    $lockAContent = [IO.File]::ReadAllText((Join-Path $context.Project 'Packages/packages-lock.json'))
    $sourceACachePath = if ($null -eq $coldA.Payload) { '' } else { [string]$coldA.Payload.ResolvedPackagePath }
    $staleACachePath = Join-Path $context.Project ('Library/PackageCache/' + $script:PackageName + '@d0m2t-stale-a')
    if (-not [string]::IsNullOrWhiteSpace($sourceACachePath) -and [IO.Directory]::Exists($sourceACachePath))
    {
        Copy-DirectoryTree -Source $sourceACachePath -Destination $staleACachePath
        Register-HarnessControlPath -CaseId 'TT-06' -Path $staleACachePath -Kind 'StaleCacheCanary' | Out-Null
    }
    $staleRspPath = Join-Path $context.Project 'Library/Bee/d0m2t-stale-a.rsp'
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($staleRspPath)) | Out-Null
    Write-NewTextFile -Path $staleRspPath -Content 'd0m2f-tarball-generation-a|suite-owned-stale-rsp'
    $staleRspSha = Get-FileSha256 -Path $staleRspPath
    Register-HarnessControlPath -CaseId 'TT-06' -Path $staleRspPath -Kind 'StaleRspCanary' | Out-Null
    $staleA = Get-CacheBeeSnapshot -ProjectPath $context.Project -Phase 'AfterA'

    $beforeReplace = Invoke-SelectorKillPhase -Context $context -Phase 'BeforeReplace'
    $afterReplace = Invoke-SelectorKillPhase -Context $context -Phase 'AfterReplace'
    $tt02TransactionRows = @($beforeReplace.TransactionArtifacts) + @($afterReplace.TransactionArtifacts)
    $manifestAControlSha = Get-FileSha256 -Path (Join-Path $context.Control 'manifest-A.json')
    $manifestBControlSha = Get-FileSha256 -Path (Join-Path $context.Control 'manifest-B.json')
    $tt02TransactionReady = $tt02TransactionRows.Count -eq 4 -and @($tt02TransactionRows | Where-Object { -not $_.RemovedAfterCleanup -or $_.UnityConsumerAuthority -ne 0 -or -not $_.NeverFallbackSelector }).Count -eq 0
    $tt02TransactionPassed = $tt02TransactionReady -and @($tt02TransactionRows | Where-Object { $_.Phase -ceq 'BeforeReplace' -and $_.Kind -ceq 'ManifestNext' -and $_.ExistedAfterKill -and $_.ObservedSha256 -ceq $manifestBControlSha }).Count -eq 1 -and @($tt02TransactionRows | Where-Object { $_.Phase -ceq 'BeforeReplace' -and $_.Kind -ceq 'ManifestBackup' -and -not $_.ExistedAfterKill -and $_.ObservedLength -eq 0 -and $_.ObservedSha256 -ceq '' }).Count -eq 1 -and @($tt02TransactionRows | Where-Object { $_.Phase -ceq 'AfterReplace' -and $_.Kind -ceq 'ManifestNext' -and -not $_.ExistedAfterKill -and $_.ObservedLength -eq 0 -and $_.ObservedSha256 -ceq '' }).Count -eq 1 -and @($tt02TransactionRows | Where-Object { $_.Phase -ceq 'AfterReplace' -and $_.Kind -ceq 'ManifestBackup' -and $_.ExistedAfterKill -and $_.ObservedSha256 -ceq $manifestAControlSha }).Count -eq 1
    $selectorRecord = [pscustomobject][ordered]@{
        Schema = 'D0M2T-SelectorAtomicKill-v1'; RunId = $RunId
        ManifestABytesSha256 = $manifestAControlSha
        ManifestBBytesSha256 = $manifestBControlSha
        BeforeReplace = $beforeReplace; AfterReplace = $afterReplace
        TransactionArtifacts = $tt02TransactionRows
        CompleteBytesOnly = $beforeReplace.Parseable -and $afterReplace.Parseable -and ($beforeReplace.IsManifestA -or $beforeReplace.IsManifestB) -and ($afterReplace.IsManifestA -or $afterReplace.IsManifestB)
        BothKillsObserved = $beforeReplace.Killed -and $afterReplace.Killed
    }
    $tt02ManifestId = Add-JsonEvidence -EvidenceId 'TT-02-manifest-snapshot' -CaseId 'TT-02' -Kind 'ManifestSnapshot' -RelativePath 'tt-02/selector-atomic-kill.json' -Value $selectorRecord
    $tt02ProcessId = Add-JsonEvidence -EvidenceId 'TT-02-worker-process' -CaseId 'TT-02' -Kind 'ProcessLog' -RelativePath 'tt-02/selector-worker-process.json' -Value ([pscustomobject]@{ Schema = 'D0M2T-SelectorWorkerProcess-v1'; RunId = $RunId; BeforeReplace = $beforeReplace; AfterReplace = $afterReplace })
    $tt02Passed = $selectorRecord.CompleteBytesOnly -and $selectorRecord.BothKillsObserved -and $beforeReplace.IsManifestA -and $afterReplace.IsManifestB -and $tt02TransactionPassed
    $tt02Ready = $beforeReplace.CheckpointObserved -and $afterReplace.CheckpointObserved -and $beforeReplace.Started -and $afterReplace.Started -and $beforeReplace.ExitedAfterKill -and $afterReplace.ExitedAfterKill -and $selectorRecord.BothKillsObserved -and $tt02TransactionReady
    Set-CaseResult -Cases $cases -CaseId 'TT-02' -Status $(if (-not $tt02Ready) { 'Inconclusive' } elseif ($tt02Passed) { 'Passed' } else { 'Failed' }) -Reason $(if (-not $tt02Ready) { 'HarnessFailure' } elseif ($tt02Passed) { 'None' } else { 'SelectorAtomicityViolation' }) -EvidenceIds @($tt02ManifestId, $tt02ProcessId) -Evidence $selectorRecord

    $resolveKill = Invoke-UnityObservation -Executable $resolvedUnity -ProjectPath $context.Project -Archive $context.ArchiveB -CaseId 'TT-03' -ObservationName 'resolve-kill-b' -WatcherHandle $mainWatcher -WatcherRecords $mainEvents -KillOnResolve
    [IO.File]::WriteAllText((Join-Path $context.Project 'Packages/packages-lock.json'), $lockAContent, $script:Utf8NoBom)
    $auditDirectory = Join-Path $context.Project 'ProjectSettings/GasCodeGen'; [IO.Directory]::CreateDirectory($auditDirectory) | Out-Null
    $auditPath = Join-Path $auditDirectory 'ActiveGenerationRef.json'
    $auditRecord = [pscustomobject][ordered]@{ Schema = 'D0M2T-AuditRecord-v1'; RunId = $RunId; GenerationId = 'A'; ArchiveSha256 = [string]$context.ArchiveA.Sha256; Role = 'AuditOnly'; ConsumerAuthority = 'None' }
    Write-NewTextFile -Path $auditPath -Content ($auditRecord | ConvertTo-Json -Depth 8)
    $restartB = Invoke-UnityObservation -Executable $resolvedUnity -ProjectPath $context.Project -Archive $context.ArchiveB -CaseId 'TT-03' -ObservationName 'restart-b' -WatcherHandle $mainWatcher -WatcherRecords $mainEvents
    $restartValidation = Test-UnityPayload -Observation $restartB -Archive $context.ArchiveB -ProjectPath $context.Project
    $auditExists = [IO.File]::Exists($auditPath); $auditLength = 0L; $auditSha = ''; $auditRawBytesBase64 = ''; $auditParseable = $false; $auditActual = $null; $auditReadError = ''
    if ($auditExists)
    {
        try
        {
            $auditBytes = [IO.File]::ReadAllBytes($auditPath)
            $auditLength = [long]$auditBytes.LongLength
            $auditSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($auditBytes)).ToLowerInvariant()
            $auditRawBytesBase64 = [Convert]::ToBase64String($auditBytes)
            $auditActual = $script:Utf8NoBom.GetString($auditBytes) | ConvertFrom-Json
            $auditParseable = $null -ne $auditActual
        }
        catch { $auditReadError = $_.Exception.Message }
    }
    $auditGeneration = if ($auditParseable -and $null -ne $auditActual.PSObject.Properties['GenerationId']) { [string]$auditActual.PSObject.Properties['GenerationId'].Value } else { '' }
    $auditArchiveSha = if ($auditParseable -and $null -ne $auditActual.PSObject.Properties['ArchiveSha256']) { [string]$auditActual.PSObject.Properties['ArchiveSha256'].Value } else { '' }
    $auditRole = if ($auditParseable -and $null -ne $auditActual.PSObject.Properties['Role']) { [string]$auditActual.PSObject.Properties['Role'].Value } else { '' }
    $auditConsumerAuthority = if ($auditParseable -and $null -ne $auditActual.PSObject.Properties['ConsumerAuthority']) { [string]$auditActual.PSObject.Properties['ConsumerAuthority'].Value } else { '' }
    $auditRemainedA = $auditExists -and $auditParseable -and $auditGeneration -ceq 'A' -and $auditArchiveSha -ceq [string]$context.ArchiveA.Sha256 -and $auditRole -ceq 'AuditOnly' -and $auditConsumerAuthority -ceq 'None'
    $auditSnapshot = [pscustomobject][ordered]@{ Schema = 'D0M2T-AuditRecord-v1'; RunId = $RunId; Path = $auditPath; Exists = $auditExists; Length = $auditLength; Sha256 = $auditSha; RawBytesBase64 = $auditRawBytesBase64; Parseable = $auditParseable; ReadError = $auditReadError; GenerationId = $auditGeneration; ArchiveSha256 = $auditArchiveSha; Role = $auditRole; ConsumerAuthority = $auditConsumerAuthority; RemainedA = $auditRemainedA; UnityAllB = [bool]$restartValidation.AllGeneration; Passed = $auditRemainedA -and [bool]$restartValidation.AllGeneration }
    $tt03Summary = [pscustomobject][ordered]@{ Schema = 'D0M2T-ResolveKillRecovery-v1'; RunId = $RunId; Killed = [bool]$resolveKill.KilledOnResolve; ResultExistedAtKill = [bool]$resolveKill.ResultExistedAtKill; KillTimedOut = [bool]$resolveKill.TimedOut; RestartExitCode = $restartB.ExitCode; RestartTimedOut = [bool]$restartB.TimedOut; RestartAllB = [bool]$restartValidation.AllGeneration; RestartValidation = $restartValidation }
    $tt03SummaryId = Add-JsonEvidence -EvidenceId 'TT-03-recovery-summary' -CaseId 'TT-03' -Kind 'ProcessLog' -RelativePath 'tt-03/resolve-recovery-summary.json' -Value $tt03Summary
    $tt03Ids = @($resolveKill.EvidenceIds) + @($restartB.EvidenceIds) + @($tt03SummaryId)
    if ($resolveKill.TimedOut -or $restartB.TimedOut) { Set-CaseResult -Cases $cases -CaseId 'TT-03' -Status 'Inconclusive' -Reason 'TimeBoxExceeded' -EvidenceIds $tt03Ids -Evidence $tt03Summary }
    elseif (-not $resolveKill.KilledOnResolve -or $resolveKill.ResultExistedAtKill) { Set-CaseResult -Cases $cases -CaseId 'TT-03' -Status 'Inconclusive' -Reason 'WatcherFailure' -EvidenceIds $tt03Ids -Evidence $tt03Summary }
    elseif ($null -eq $restartB.Payload) { Set-CaseResult -Cases $cases -CaseId 'TT-03' -Status 'Inconclusive' -Reason 'EvidenceIncomplete' -EvidenceIds $tt03Ids -Evidence $tt03Summary }
    elseif (-not $restartValidation.IdentityMatches) { Set-CaseResult -Cases $cases -CaseId 'TT-03' -Status 'Inconclusive' -Reason 'UnityIdentityMismatch' -EvidenceIds $tt03Ids -Evidence $tt03Summary }
    else { Set-CaseResult -Cases $cases -CaseId 'TT-03' -Status $(if ($restartValidation.Passed) { 'Passed' } else { 'Failed' }) -Reason $(if ($restartValidation.Passed) { 'None' } else { 'MixedGenerationObserved' }) -EvidenceIds $tt03Ids -Evidence $tt03Summary }

    $lockB = Get-PackageLockObservation -ProjectPath $context.Project -Archive $context.ArchiveB
    $canonicalB = Test-CanonicalManifest -Content ([IO.File]::ReadAllText((Join-Path $context.Project 'Packages/manifest.json'))) -Archives @($context.Index.Archives)
    $invalidJson = Test-CanonicalManifest -Content '{' -Archives @($context.Index.Archives)
    $aliasManifest = $context.ManifestB.Content.Replace([string]$context.ArchiveB.FileName, 'current.tgz')
    $alias = Test-CanonicalManifest -Content $aliasManifest -Archives @($context.Index.Archives)
    $escapeManifest = $context.ManifestB.Content.Replace('../../Authority/', '../../../Authority/')
    $escape = Test-CanonicalManifest -Content $escapeManifest -Archives @($context.Index.Archives)
    $tt05Manifest = [pscustomobject][ordered]@{ Schema = 'D0M2T-ManifestGate-v1'; RunId = $RunId; Canonical = $canonicalB; InvalidJson = $invalidJson; Alias = $alias; Escape = $escape; InvalidRejected = -not $invalidJson.Accepted; AliasRejected = -not $alias.Accepted; EscapeRejected = -not $escape.Accepted; FinalAllB = [bool]$restartValidation.AllGeneration }
    $tt05Lock = [pscustomobject][ordered]@{ Schema = 'D0M2T-LockDrift-v1'; RunId = $RunId; StaleA = $lockA; FinalB = $lockB; DriftDidNotSelect = [bool]$restartValidation.AllGeneration }
    $tt05ManifestId = Add-JsonEvidence -EvidenceId 'TT-05-manifest-gates' -CaseId 'TT-05' -Kind 'ManifestSnapshot' -RelativePath 'tt-05/manifest-gates.json' -Value $tt05Manifest
    $tt05LockId = Add-JsonEvidence -EvidenceId 'TT-05-lock-drift' -CaseId 'TT-05' -Kind 'PackageLockSnapshot' -RelativePath 'tt-05/package-lock-drift.json' -Value $tt05Lock
    $tt05AuditId = Add-JsonEvidence -EvidenceId 'TT-05-audit-zero' -CaseId 'TT-05' -Kind 'AuditRecordSnapshot' -RelativePath 'tt-05/audit-authority-zero.json' -Value $auditSnapshot
    $tt05Passed = $canonicalB.Accepted -and -not $invalidJson.Accepted -and -not $alias.Accepted -and -not $escape.Accepted -and $lockA.Passed -and $lockB.Passed -and $restartValidation.Passed -and $auditSnapshot.Passed
    $tt05Ready = $null -ne $restartB.Payload -and $restartValidation.IdentityMatches -and $auditSnapshot.Passed
    Set-CaseResult -Cases $cases -CaseId 'TT-05' -Status $(if (-not $tt05Ready) { 'Inconclusive' } elseif ($tt05Passed) { 'Passed' } else { 'Failed' }) -Reason $(if (-not $tt05Ready) { 'EvidenceIncomplete' } elseif ($tt05Passed) { 'None' } else { 'ManifestLockDriftAccepted' }) -EvidenceIds @($tt05ManifestId, $tt05LockId, $tt05AuditId) -Evidence ([pscustomobject]@{ Manifest = $tt05Manifest; Lock = $tt05Lock; Audit = $auditSnapshot; AuditAuthority = 0 })

    $finalB = Get-CacheBeeSnapshot -ProjectPath $context.Project -Phase 'AfterB'
    $staleAPath = $staleACachePath
    $finalBPath = if ($null -eq $restartB.Payload) { '' } else { [string]$restartB.Payload.ResolvedPackagePath }
    $staleAPreseeded = @($staleA.CacheEntries | Where-Object Path -CEQ $staleAPath).Count -eq 1
    $staleAStillExists = [IO.Directory]::Exists($staleAPath)
    $staleAInAfterSnapshot = @($finalB.CacheEntries | Where-Object Path -CEQ $staleAPath).Count -eq 1
    $finalUsesDifferentCache = -not [string]::IsNullOrWhiteSpace($finalBPath) -and $finalBPath -cne $staleAPath
    $staleAPresenceConsistent = $staleAStillExists -eq $staleAInAfterSnapshot
    $staleARemainedUnselected = $staleAStillExists -and $staleAInAfterSnapshot -and $finalUsesDifferentCache
    $staleACleanedBeforeFinal = -not $staleAStillExists -and -not $staleAInAfterSnapshot -and $finalUsesDifferentCache
    $staleAResolvedWithoutSelection = $staleARemainedUnselected -or $staleACleanedBeforeFinal
    $staleADisposition = if ($staleARemainedUnselected) { 'RemainedUnselected' } elseif ($staleACleanedBeforeFinal) { 'CleanedBeforeFinal' } else { 'Unresolved' }
    $cacheRecord = [pscustomobject][ordered]@{ Schema = 'D0M2T-StaleCache-v1'; RunId = $RunId; Before = $staleA; After = $finalB; SourceAPath = $sourceACachePath; StaleAPath = $staleAPath; StaleAPreseeded = $staleAPreseeded; StaleAStillExists = $staleAStillExists; StaleAInAfterSnapshot = $staleAInAfterSnapshot; StaleAPresenceConsistent = $staleAPresenceConsistent; RemainedUnselected = $staleARemainedUnselected; CleanedBeforeFinal = $staleACleanedBeforeFinal; StaleADisposition = $staleADisposition; StaleAResolvedWithoutSelection = $staleAResolvedWithoutSelection; FinalBPath = $finalBPath; FinalUsesDifferentCache = $finalUsesDifferentCache }
    $graphA = if ($null -eq $coldA.Payload) { '' } else { [string]$coldA.Payload.CompileGraphSha256 }
    $graphB = if ($null -eq $restartB.Payload) { '' } else { [string]$restartB.Payload.CompileGraphSha256 }
    $staleRspStillExists = [IO.File]::Exists($staleRspPath)
    $staleRspContainsA = $false
    if ($staleRspStillExists)
    {
        try { $staleRspContainsA = [IO.File]::ReadAllText($staleRspPath).Contains('d0m2f-tarball-generation-a') }
        catch { $staleRspContainsA = $false }
    }
    $beeRecord = [pscustomobject][ordered]@{ Schema = 'D0M2T-StaleBeeRsp-v1'; RunId = $RunId; BeforeRsp = @($staleA.RspEntries); AfterRsp = @($finalB.RspEntries); CanaryPath = [IO.Path]::GetRelativePath($context.Project, $staleRspPath).Replace('\', '/'); CanarySha256 = $staleRspSha; CanaryContainsGenerationA = $staleRspContainsA; CanaryStillExists = $staleRspStillExists; StaleRspPreexisted = @($staleA.RspEntries | Where-Object Path -CEQ 'Library/Bee/d0m2t-stale-a.rsp').Count -eq 1; FinalAllB = [bool]$restartValidation.AllGeneration; GraphA = $graphA; GraphB = $graphB; GraphChanged = -not [string]::IsNullOrWhiteSpace($graphA) -and -not [string]::IsNullOrWhiteSpace($graphB) -and $graphA -cne $graphB }
    $tt06CacheId = Add-JsonEvidence -EvidenceId 'TT-06-cache-snapshot' -CaseId 'TT-06' -Kind 'CacheSnapshot' -RelativePath 'tt-06/stale-cache.json' -Value $cacheRecord
    $tt06BeeId = Add-JsonEvidence -EvidenceId 'TT-06-bee-rsp-snapshot' -CaseId 'TT-06' -Kind 'BeeRspSnapshot' -RelativePath 'tt-06/stale-bee-rsp.json' -Value $beeRecord
    $tt06UnityPayload = if ($null -eq $restartB.Payload) { [pscustomobject]@{ Schema = 'D0M2T-MissingUnityObservation-v1'; RunId = $RunId; ExpectedGeneration = 'B'; ResultPresent = $false } } else { $restartB.Payload }
    $tt06UnityId = Add-JsonEvidence -EvidenceId 'TT-06-final-unity' -CaseId 'TT-06' -Kind 'UnityObservation' -RelativePath 'tt-06/final-b-unity.json' -Value $tt06UnityPayload
    $tt06Ready = $null -ne $restartB.Payload -and $restartValidation.IdentityMatches -and $cacheRecord.StaleAPreseeded -and $cacheRecord.StaleAPresenceConsistent -and -not [string]::IsNullOrWhiteSpace($cacheRecord.FinalBPath) -and $beeRecord.StaleRspPreexisted -and $beeRecord.CanaryStillExists -and $beeRecord.CanaryContainsGenerationA -and -not [string]::IsNullOrWhiteSpace($beeRecord.GraphA) -and -not [string]::IsNullOrWhiteSpace($beeRecord.GraphB)
    $tt06Passed = $tt06Ready -and $cacheRecord.StaleAResolvedWithoutSelection -and $beeRecord.FinalAllB -and $beeRecord.GraphChanged -and $restartValidation.Passed
    $tt06Status = if (-not $tt06Ready) { 'Inconclusive' } elseif ($tt06Passed) { 'Passed' } else { 'Failed' }
    $tt06Reason = if (-not $tt06Ready) { 'EvidenceIncomplete' } elseif (-not $cacheRecord.FinalUsesDifferentCache) { 'CacheFallbackSelectorObserved' } elseif ($tt06Passed) { 'None' } else { 'StaleBeeOrRspConsumed' }
    Set-CaseResult -Cases $cases -CaseId 'TT-06' -Status $tt06Status -Reason $tt06Reason -EvidenceIds @($tt06CacheId, $tt06BeeId, $tt06UnityId) -Evidence ([pscustomobject]@{ Cache = $cacheRecord; BeeRsp = $beeRecord })

    $faultCase = Invoke-MissingCorruptAuthorityCase -Executable $resolvedUnity; $faultRoot = [string]$faultCase.Root
    $tt04Status = if ($faultCase.TimedOut -or -not $faultCase.Ready) { 'Inconclusive' } elseif ($faultCase.Passed) { 'Passed' } else { 'Failed' }
    $tt04Reason = if ($faultCase.TimedOut) { 'TimeBoxExceeded' } elseif (-not $faultCase.Ready) { 'EvidenceIncomplete' } elseif ($faultCase.Passed) { 'None' } else { 'MissingOrCorruptAuthorityAccepted' }
    Set-CaseResult -Cases $cases -CaseId 'TT-04' -Status $tt04Status -Reason $tt04Reason -EvidenceIds @($faultCase.EvidenceIds) -Evidence $faultCase.Record

    Stop-FixtureWatcher -Handle $mainWatcher -Records $mainEvents; $mainWatcher = $null
    $authorityAfter = Get-AuthoritySnapshot -Context $context
    $authorityWrites = @($mainEvents | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.FullPath) -and $_.FullPath.StartsWith($context.Authority + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) }).Count
    $watcherErrors = @($mainEvents | Where-Object ChangeType -CEQ 'Error')
    $authorityRecord = [pscustomobject][ordered]@{ Schema = 'D0M2T-BaselineAuthority-v1'; RunId = $RunId; Before = $authorityBefore; After = $authorityAfter; AuthorityWrites = $authorityWrites; BaselineA = [pscustomobject]@{ ExitCode = $coldA.ExitCode; AllA = [bool]$coldAValidation.AllGeneration; Passed = [bool]$coldAValidation.Passed; Assemblies = if ($null -eq $coldA.Payload) { @() } else { @($coldA.Payload.Assemblies) } }; BaselineB = [pscustomobject]@{ ExitCode = $restartB.ExitCode; AllB = [bool]$restartValidation.AllGeneration; Passed = [bool]$restartValidation.Passed; Assemblies = if ($null -eq $restartB.Payload) { @() } else { @($restartB.Payload.Assemblies) } }; CanaryObserved = @($mainEvents | Where-Object FullPath -CEQ $canary).Count -gt 0; WatcherErrors = $watcherErrors.Count }
    $watcherRecord = [pscustomobject][ordered]@{ Schema = 'D0M2T-AuthorityWatcher-v1'; RunId = $RunId; CanaryPath = $canary; CanaryObserved = $authorityRecord.CanaryObserved; AuthorityWrites = $authorityWrites; Errors = $watcherErrors; Events = @($mainEvents) }
    $tt01AuthorityId = Add-JsonEvidence -EvidenceId 'TT-01-authority-snapshot' -CaseId 'TT-01' -Kind 'AuthoritySnapshot' -RelativePath 'tt-01/authority-snapshot.json' -Value $authorityRecord
    $tt01WatcherId = Add-JsonEvidence -EvidenceId 'TT-01-authority-watcher' -CaseId 'TT-01' -Kind 'AuthorityWatcherLog' -RelativePath 'tt-01/authority-watcher.json' -Value $watcherRecord
    $tt01Ids = @($coldA.EvidenceIds) + @($tt01AuthorityId, $tt01WatcherId)
    $tt01Passed = $coldAValidation.Passed -and $restartValidation.Passed -and $authorityWrites -eq 0 -and $authorityRecord.CanaryObserved -and $watcherErrors.Count -eq 0 -and @(@($authorityBefore) + @($authorityAfter) | Where-Object { $_.ActualSha256 -cne $_.ExpectedSha256 -or $_.ActualLength -ne $_.ExpectedLength -or $_.LinkCount -ne 1 -or -not $_.ReadOnly }).Count -eq 0
    $tt01Reason = if ($coldA.TimedOut -or $restartB.TimedOut) { 'TimeBoxExceeded' } elseif ($null -eq $coldA.Payload -or $null -eq $restartB.Payload) { 'EvidenceIncomplete' } elseif (-not $coldAValidation.IdentityMatches -or -not $restartValidation.IdentityMatches) { 'UnityIdentityMismatch' } elseif ($watcherErrors.Count -gt 0 -or -not $authorityRecord.CanaryObserved) { 'WatcherFailure' } elseif ($authorityWrites -ne 0) { 'AuthorityMutationObserved' } elseif (-not $coldAValidation.Passed -or -not $restartValidation.Passed) { 'MixedGenerationObserved' } else { 'None' }
    Set-CaseResult -Cases $cases -CaseId 'TT-01' -Status $(if ($tt01Reason -in @('TimeBoxExceeded', 'EvidenceIncomplete', 'UnityIdentityMismatch', 'WatcherFailure')) { 'Inconclusive' } elseif ($tt01Passed) { 'Passed' } else { 'Failed' }) -Reason $tt01Reason -EvidenceIds $tt01Ids -Evidence $authorityRecord

    $boundaryRecord = Invoke-BoundaryCleanupCase
    $tt07BoundaryId = Add-JsonEvidence -EvidenceId 'TT-07-boundary-record' -CaseId 'TT-07' -Kind 'FilesystemBoundaryRecord' -RelativePath 'tt-07/boundary-record.json' -Value $boundaryRecord
    if (-not $KeepFixtures)
    {
        Remove-OwnedFixtureRoot -Root $mainRoot; $mainRoot = ''
        Remove-OwnedFixtureRoot -Root $faultRoot; $faultRoot = ''
    }
    $residualRoots = @($script:CreatedFixtureRoots | Where-Object { [IO.Directory]::Exists($_) })
    $harnessControlObservations = @(Get-FinalHarnessControlObservations)
    $ownerControlsComplete = @($script:CreatedFixtureRoots | Where-Object { $ownerPath = Join-Path $_ $script:OwnerSentinelName; @($harnessControlObservations | Where-Object { $_.Kind -ceq 'OwnerSentinel' -and $_.Path -ceq $ownerPath }).Count -ne 1 }).Count -eq 0
    $harnessControlsClosed = $harnessControlObservations.Count -eq 23 -and $ownerControlsComplete -and @($harnessControlObservations | Where-Object { -not $_.Observed -or -not $_.RemovedAfterCleanup -or $_.UnityConsumerAuthority -ne 0 -or -not $_.NeverFallbackSelector }).Count -eq 0
    $cleanupRecord = [pscustomobject][ordered]@{ Schema = 'D0M2T-Cleanup-v1'; RunId = $RunId; Requested = -not [bool]$KeepFixtures; CreatedRoots = @($script:CreatedFixtureRoots); RemovedRoots = @($script:RemovedFixtureRoots); ResidualRoots = $residualRoots; AllOwnedOnly = $ownerControlsComplete; HarnessControlObservations = $harnessControlObservations; Passed = -not $KeepFixtures -and $residualRoots.Count -eq 0 -and $harnessControlsClosed }
    $tt07CleanupId = Add-JsonEvidence -EvidenceId 'TT-07-cleanup-record' -CaseId 'TT-07' -Kind 'CleanupRecord' -RelativePath 'tt-07/cleanup-record.json' -Value $cleanupRecord
    $protectedAfter = Get-ProtectedSnapshot -Phase 'After' -Contract $contract
    $protectedAfterId = Add-JsonEvidence -EvidenceId 'TT-07-protected-after' -CaseId 'TT-07' -Kind 'ProtectedSnapshot' -RelativePath 'tt-07/protected-after.json' -Value $protectedAfter
    $protectedUnchanged = Test-ProtectedUnchanged -Before $protectedBefore -After $protectedAfter
    $protectedComplete = @(@($protectedBefore.Bindings) + @($protectedAfter.Bindings) | Where-Object { $_.Exists -and [long]$_.FileCount -lt 1 }).Count -eq 0
    $tt07Passed = $boundaryRecord.Passed -and $cleanupRecord.Passed -and $protectedUnchanged -and $protectedComplete
    $tt07Reason = if (-not $protectedComplete) { 'EvidenceIncomplete' } elseif (-not $protectedUnchanged) { 'ProtectedPathDrift' } elseif (-not $cleanupRecord.Passed) { 'CleanupFailure' } elseif (-not $boundaryRecord.Passed) { 'FilesystemBoundaryViolation' } else { 'None' }
    Set-CaseResult -Cases $cases -CaseId 'TT-07' -Status $(if ($tt07Reason -in @('EvidenceIncomplete', 'ProtectedPathDrift', 'CleanupFailure')) { 'Inconclusive' } elseif ($tt07Passed) { 'Passed' } else { 'Failed' }) -Reason $tt07Reason -EvidenceIds @($protectedBeforeId, $tt07BoundaryId, $tt07CleanupId, $protectedAfterId) -Evidence ([pscustomobject]@{ Boundary = $boundaryRecord; Cleanup = $cleanupRecord; ProtectedUnchanged = $protectedUnchanged; ProtectedComplete = $protectedComplete })

    $liveProcessIds = @($script:OwnedProcessIds | Where-Object { $null -ne (Get-Process -Id $_ -ErrorAction SilentlyContinue) } | Select-Object -Unique)
    $prior = @($script:EvidenceFiles | ForEach-Object { [pscustomobject][ordered]@{ EvidenceId = $_.EvidenceId; CaseId = $_.CaseId; Kind = $_.Kind; Path = $_.Path; Sha256 = $_.Sha256; Length = $_.Length } })
    $allOpenable = @($prior | Where-Object { -not [IO.File]::Exists((Resolve-EvidencePath -RelativePath $_.Path)) }).Count -eq 0
    $lengthsMatch = @($prior | Where-Object { [long](Get-Item -LiteralPath (Resolve-EvidencePath -RelativePath $_.Path)).Length -ne [long]$_.Length }).Count -eq 0
    $hashesMatch = @($prior | Where-Object { (Get-FileSha256 -Path (Resolve-EvidencePath -RelativePath $_.Path)) -cne [string]$_.Sha256 }).Count -eq 0
    $duplicateIds = @($prior | Group-Object EvidenceId | Where-Object Count -gt 1).Count -gt 0
    $referencedIds = @($cases | Where-Object CaseId -CNE 'TT-08' | ForEach-Object EvidenceIds)
    $orphanIds = @($prior | Where-Object EvidenceId -CNotIn $referencedIds).Count -gt 0
    $closurePassed = $allOpenable -and $lengthsMatch -and $hashesMatch -and -not $duplicateIds -and -not $orphanIds -and $residualRoots.Count -eq 0 -and $liveProcessIds.Count -eq 0
    $closure = [pscustomobject][ordered]@{
        Schema = 'D0M2T-EvidenceClosure-v1'; RunId = $RunId; ClosedAtUtc = [DateTime]::UtcNow.ToString('O'); EvidenceRoot = $script:ResolvedEvidenceRoot
        PriorEvidenceFiles = $prior
        Checks = [pscustomobject][ordered]@{ AllPathsRelative = $true; AllFilesOpenable = $allOpenable; LengthsMatch = $lengthsMatch; Sha256Match = $hashesMatch; DuplicateEvidenceIds = $duplicateIds; OrphanEvidenceIds = $orphanIds }
        FixtureCleanup = [pscustomobject][ordered]@{ Requested = -not [bool]$KeepFixtures; CreatedRoots = @($script:CreatedFixtureRoots); RemovedRoots = @($script:RemovedFixtureRoots); ResidualRoots = $residualRoots }
        OwnedProcessIds = @($script:OwnedProcessIds | Select-Object -Unique); LiveOwnedProcessIds = $liveProcessIds; Passed = $closurePassed; Reason = if ($closurePassed) { 'None' } elseif ($liveProcessIds.Count -gt 0) { 'UnityProcessResidue' } elseif ($residualRoots.Count -gt 0) { 'CleanupFailure' } else { 'EvidenceIncomplete' }
    }
    $tt08Id = Add-JsonEvidence -EvidenceId 'TT-08-evidence-closure' -CaseId 'TT-08' -Kind 'EvidenceClosure' -RelativePath 'tt-08/evidence-closure.json' -Value $closure
    Set-CaseResult -Cases $cases -CaseId 'TT-08' -Status $(if ($closurePassed) { 'Passed' } else { 'Inconclusive' }) -Reason ([string]$closure.Reason) -EvidenceIds @($tt08Id) -Evidence ([pscustomobject]@{ ClosureEvidenceId = $tt08Id; Passed = $closurePassed })

    $inconclusive = @($cases | Where-Object Status -CEQ 'Inconclusive'); $failed = @($cases | Where-Object Status -CEQ 'Failed')
    if ($inconclusive.Count -gt 0)
    {
        $overallStatus = 'Inconclusive'; $overallReason = [string]$inconclusive[0].Reason
        foreach ($priorityReason in @('TimeBoxExceeded', 'CleanupFailure', 'UnityProcessResidue', 'HarnessFailure', 'WatcherFailure'))
        {
            if (@($inconclusive | Where-Object Reason -CEQ $priorityReason).Count -gt 0) { $overallReason = $priorityReason; break }
        }
    }
    elseif ($failed.Count -gt 0) { $overallStatus = 'RouteRejected'; $overallReason = [string]$failed[0].Reason }
    else { $overallStatus = 'Passed'; $overallReason = 'None' }
    $exitCode = if ($overallStatus -eq 'Passed') { 0 } elseif ($overallStatus -eq 'RouteRejected') { 20 } elseif ($overallReason -eq 'TimeBoxExceeded') { 21 } elseif ($overallReason -eq 'CleanupFailure') { 24 } elseif ($overallReason -in @('HarnessFailure', 'WatcherFailure', 'UnityProcessResidue')) { 23 } else { 22 }

    $aggregate = [ordered]@{
        Schema = $script:Schema; RunId = $RunId; Status = $overallStatus; Reason = $overallReason
        SelectedRoute = 'ImmutableTarball'; SoleUnityConsumedSelector = 'Packages/manifest.json'
        Cases = $cases
        Inputs = [ordered]@{ UnityPath = $resolvedUnity; UnitySha256 = Get-FileSha256 -Path $resolvedUnity; HarnessSha256 = Get-FileSha256 -Path $PSCommandPath; ArchiveIndexSha256 = Get-FileSha256 -Path $context.IndexPath; FrozenInputs = @($contract.FrozenInputs); FrozenRecordSha256 = [string]$contract.FrozenRecordSha256 }
        Authority = [ordered]@{ PackageName = $script:PackageName; Archives = $authorityAfter; AuthorityWrites = $authorityWrites }
        Selector = [ordered]@{ Path = 'Packages/manifest.json'; ManifestAHash = $selectorRecord.ManifestABytesSha256; ManifestBHash = $selectorRecord.ManifestBBytesSha256; FinalGeneration = 'B'; AuditAuthority = 0 }
        Unity = [ordered]@{ ExpectedVersion = $script:ExpectedUnityVersion; BaselineA = $coldA.Payload; RestartB = $restartB.Payload }
        RoleObservations = @(New-RoleObservations -Context $context)
        TransientArtifactObservations = @($script:TransientArtifactObservations)
        HarnessControlObservations = $harnessControlObservations
        Fixture = [ordered]@{ Kept = [bool]$KeepFixtures; CreatedRoots = @($script:CreatedFixtureRoots); RemovedRoots = @($script:RemovedFixtureRoots); ResidualRoots = $residualRoots; CleanupPassed = [bool]$cleanupRecord.Passed }
        EvidenceFiles = @($script:EvidenceFiles)
        Protected = [ordered]@{ Unchanged = $protectedUnchanged; BeforeSnapshotEvidenceId = $protectedBeforeId; AfterSnapshotEvidenceId = $protectedAfterId }
        D1Authorized = $false; ProductionInstallAdmission = 'NotEvaluated'; DeclaredFullSemanticEligibility = $false; NextGate = 'D0-M2R'
    }
    try { Write-Aggregate -Path $resolvedOutput -Aggregate $aggregate }
    catch { $aggregateWriteFailed = $true; throw }
}
catch
{
    $caughtFailure = $true
    $failureDetail = $_.Exception.ToString(); [Console]::Error.WriteLine($failureDetail)
    $overallStatus = 'Inconclusive'; $overallReason = 'HarnessFailure'; $exitCode = if ($aggregateWriteFailed) { 90 } else { 23 }
}
finally
{
    if ($null -ne $mainWatcher)
    {
        try { Stop-FixtureWatcher -Handle $mainWatcher -Records $mainEvents } catch { [Console]::Error.WriteLine($_.Exception.ToString()) }
    }
    $cleanupState = $null
    try { $cleanupState = Remove-RemainingOwnedFixtureRoots }
    catch
    {
        $cleanupState = [pscustomobject]@{ Failed = $true; Failures = @($_.Exception.ToString()); Remaining = @($script:CreatedFixtureRoots | Where-Object { [IO.Directory]::Exists($_) }) }
    }
    $cleanupFailed = [bool]$cleanupState.Failed -or ($caughtFailure -and [bool]$KeepFixtures -and @($cleanupState.Remaining).Count -gt 0)
    if ($cleanupFailed)
    {
        foreach ($cleanupFailure in @($cleanupState.Failures))
        {
            [Console]::Error.WriteLine([string]$cleanupFailure)
        }
        $overallStatus = 'Inconclusive'; $overallReason = 'CleanupFailure'; $exitCode = 24
    }
    if ($caughtFailure -and -not $aggregateWriteFailed)
    {
        try
        {
            Register-UntrackedEvidenceFiles
            Sync-PartialCaseEvidence -Cases $cases -Reason $overallReason
            $aggregate = New-PartialAggregate -Reason $overallReason -Cases $cases -Contract $contract -ResolvedUnity $resolvedUnity
            Write-Aggregate -Path $resolvedOutput -Aggregate $aggregate
        }
        catch
        {
            $aggregateWriteFailed = $true; $exitCode = 90
            [Console]::Error.WriteLine($_.Exception.ToString())
        }
    }
}

exit $exitCode
