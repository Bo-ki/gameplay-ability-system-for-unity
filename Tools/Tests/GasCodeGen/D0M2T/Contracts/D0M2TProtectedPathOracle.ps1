Set-StrictMode -Version Latest

$script:D0M2TContractPath = Join-Path $PSScriptRoot "D0M2T.contract.json"

# 读取 D0-M2T 机器合同，并拒绝缺失合同。
function Get-D0M2TContract
{
    if (-not [IO.File]::Exists($script:D0M2TContractPath)) { throw "D0-M2T contract is missing: $script:D0M2TContractPath" }
    return [IO.File]::ReadAllText($script:D0M2TContractPath, [Text.Encoding]::UTF8) | ConvertFrom-Json -Depth 100
}

# 计算原始字节的 SHA-256 小写十六进制身份。
function Get-D0M2TBytesSha256
{
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][byte[]]$Bytes)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($Bytes))).Replace("-", "").ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

# 计算普通文件原始字节 SHA-256，并在并发写入或锁冲突时 fail closed。
function Get-D0M2TProtectedFileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace("-", "").ToLowerInvariant() }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}

# 将合同相对路径解析到仓库内，并拒绝绝对路径、反斜杠或目录越界。
function Resolve-D0M2TProtectedPath
{
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot, [Parameter(Mandatory = $true)][string]$RelativePath)

    if ([IO.Path]::IsPathRooted($RelativePath) -or $RelativePath.Contains("\") -or $RelativePath -match "(^|/)\.\.?(?:/|$)")
    {
        throw "Protected path must be a canonical forward-slash relative path: $RelativePath"
    }
    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $resolved = [IO.Path]::GetFullPath((Join-Path $root $RelativePath))
    if (-not $resolved.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Protected path escaped the repository root: $RelativePath"
    }
    return $resolved
}

# 检查仓库根到目标的全部现存路径段，禁止 reparse point。
function Assert-D0M2TNoReparsePoint
{
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot, [Parameter(Mandatory = $true)][string]$TargetPath)

    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $segments = @([IO.Path]::GetRelativePath($root, [IO.Path]::GetFullPath($TargetPath)).Split(
            [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar),
            [StringSplitOptions]::RemoveEmptyEntries))
    $current = $root
    foreach ($segment in $segments)
    {
        $current = Join-Path $current $segment
        if (-not [IO.File]::Exists($current) -and -not [IO.Directory]::Exists($current)) { break }
        if (([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "Protected path contains a reparse point: $current"
        }
    }
}

# 枚举受保护目录的规范记录，并在读取内容前拒绝每个 reparse 子项。
function Get-D0M2TProtectedTreeRecords
{
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$TreePath,
        [Parameter(Mandatory = $true)][string]$RelativeTreePath
    )

    $records = [Collections.Generic.List[string]]::new()
    $records.Add("D`t$RelativeTreePath`ttrue")
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($TreePath)
    while ($pending.Count -gt 0)
    {
        $current = $pending.Pop()
        foreach ($entry in [IO.Directory]::EnumerateFileSystemEntries($current, "*", [IO.SearchOption]::TopDirectoryOnly))
        {
            $attributes = [IO.File]::GetAttributes($entry)
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Protected tree contains a reparse point: $entry" }
            $relative = [IO.Path]::GetRelativePath($RepositoryRoot, $entry).Replace("\", "/")
            if (($attributes -band [IO.FileAttributes]::Directory) -ne 0)
            {
                $records.Add("D`t$relative`ttrue")
                $pending.Push($entry)
            }
            else
            {
                $records.Add("F`t$relative`ttrue`t$([IO.FileInfo]::new($entry).Length)`t$(Get-D0M2TProtectedFileSha256 -Path $entry)")
            }
        }
    }
    return $records.ToArray()
}

# 获取合同精确保护集的只读、规范化 before/after 快照。
function Get-D0M2TProtectedSnapshot
{
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not [IO.Directory]::Exists($root)) { throw "Repository root does not exist: $root" }
    Assert-D0M2TNoReparsePoint -RepositoryRoot $root -TargetPath $root
    $contract = Get-D0M2TContract
    $records = [Collections.Generic.List[string]]::new()
    $specLines = [Collections.Generic.List[string]]::new()
    foreach ($spec in @($contract.ProtectedPathSpecs))
    {
        $kind = [string]$spec.Kind
        $relative = [string]$spec.Path
        $specLines.Add("$kind`t$relative")
        $path = Resolve-D0M2TProtectedPath -RepositoryRoot $root -RelativePath $relative
        Assert-D0M2TNoReparsePoint -RepositoryRoot $root -TargetPath $path
        if ($kind -ceq "File")
        {
            if ([IO.Directory]::Exists($path)) { throw "Protected file is a directory: $relative" }
            if (-not [IO.File]::Exists($path)) { $records.Add("F`t$relative`tfalse"); continue }
            $records.Add("F`t$relative`ttrue`t$([IO.FileInfo]::new($path).Length)`t$(Get-D0M2TProtectedFileSha256 -Path $path)")
            continue
        }
        if ($kind -cne "Tree") { throw "Unsupported protected path kind: $kind" }
        if ([IO.File]::Exists($path)) { throw "Protected tree is a file: $relative" }
        if (-not [IO.Directory]::Exists($path)) { $records.Add("D`t$relative`tfalse"); continue }
        foreach ($record in @(Get-D0M2TProtectedTreeRecords -RepositoryRoot $root -TreePath $path -RelativeTreePath $relative)) { $records.Add($record) }
    }
    $orderedRecords = $records.ToArray()
    [Array]::Sort($orderedRecords, [StringComparer]::Ordinal)
    $orderedSpecs = $specLines.ToArray()
    [Array]::Sort($orderedSpecs, [StringComparer]::Ordinal)
    $encoding = [Text.UTF8Encoding]::new($false)
    return [pscustomobject]@{
        Schema = "D0M2T-ProtectedSnapshot-v1"
        RepositoryRoot = $root.Replace("\", "/")
        ProtectedSpecSha256 = Get-D0M2TBytesSha256 -Bytes $encoding.GetBytes([string]::Join("`n", $orderedSpecs))
        AggregateSha256 = Get-D0M2TBytesSha256 -Bytes $encoding.GetBytes([string]::Join("`n", $orderedRecords))
        RecordCount = $orderedRecords.Count
        Records = $orderedRecords
    }
}

# 比较两个只读快照并返回新增、删除与总体 unchanged 裁决。
function Compare-D0M2TProtectedSnapshots
{
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)]$Before, [Parameter(Mandatory = $true)]$After)

    foreach ($snapshot in @($Before, $After))
    {
        if ([string]$snapshot.Schema -cne "D0M2T-ProtectedSnapshot-v1") { throw "Protected snapshot schema mismatch." }
    }
    $beforeSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $afterSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($record in @($Before.Records)) { [void]$beforeSet.Add([string]$record) }
    foreach ($record in @($After.Records)) { [void]$afterSet.Add([string]$record) }
    $removed = @($beforeSet | Where-Object { -not $afterSet.Contains($_) } | Sort-Object)
    $added = @($afterSet | Where-Object { -not $beforeSet.Contains($_) } | Sort-Object)
    $unchanged = [string]$Before.RepositoryRoot -ceq [string]$After.RepositoryRoot -and
        [string]$Before.ProtectedSpecSha256 -ceq [string]$After.ProtectedSpecSha256 -and
        [string]$Before.AggregateSha256 -ceq [string]$After.AggregateSha256 -and $removed.Count -eq 0 -and $added.Count -eq 0
    return [pscustomobject]@{
        Schema = "D0M2T-ProtectedComparison-v1"
        Unchanged = $unchanged
        BeforeAggregateSha256 = [string]$Before.AggregateSha256
        AfterAggregateSha256 = [string]$After.AggregateSha256
        Removed = $removed
        Added = $added
    }
}
