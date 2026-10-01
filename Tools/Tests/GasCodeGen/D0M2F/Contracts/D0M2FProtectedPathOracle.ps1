Set-StrictMode -Version Latest

$script:D0M2FContractPath = Join-Path $PSScriptRoot "D0M2F.contract.json"

# 读取 R3 冻结的机器合同，并拒绝缺失合同。
function Get-D0M2FContract
{
    if (-not [IO.File]::Exists($script:D0M2FContractPath))
    {
        throw "D0-M2F contract is missing: $script:D0M2FContractPath"
    }

    return [IO.File]::ReadAllText($script:D0M2FContractPath, [Text.Encoding]::UTF8) | ConvertFrom-Json -Depth 100
}

# 计算原始字节的 SHA-256 小写十六进制身份。
function Get-D0M2FBytesSha256
{
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][byte[]]$Bytes)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try
    {
        return ([BitConverter]::ToString($algorithm.ComputeHash($Bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally
    {
        $algorithm.Dispose()
    }
}

# 计算普通文件原始字节的 SHA-256，并在并发写入或锁冲突时 fail closed。
function Get-D0M2FFileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try
    {
        return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace("-", "").ToLowerInvariant()
    }
    finally
    {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

# 将合同相对路径解析到仓库内，并拒绝绝对路径或目录越界。
function Resolve-D0M2FProtectedPath
{
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if ([IO.Path]::IsPathRooted($RelativePath) -or $RelativePath.Contains("\"))
    {
        throw "Protected path must be a canonical forward-slash relative path: $RelativePath"
    }

    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $resolved = [IO.Path]::GetFullPath((Join-Path $root $RelativePath))
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Protected path escaped the repository root: $RelativePath"
    }

    return $resolved
}

# 检查仓库根到目标的全部现存路径段，禁止 reparse point。
function Assert-D0M2FNoReparsePoint
{
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$TargetPath
    )

    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $target = [IO.Path]::GetFullPath($TargetPath)
    $segments = @([IO.Path]::GetRelativePath($root, $target).Split(
            [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar),
            [StringSplitOptions]::RemoveEmptyEntries))
    $current = $root
    foreach ($segment in $segments)
    {
        $current = Join-Path $current $segment
        if (-not [IO.File]::Exists($current) -and -not [IO.Directory]::Exists($current)) { break }
        $attributes = [IO.File]::GetAttributes($current)
        if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "Protected path contains a reparse point: $current"
        }
    }
}

# 枚举受保护目录的规范记录，且在遍历前拒绝每个 reparse 子项。
function Get-D0M2FTreeRecords
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
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
            {
                throw "Protected tree contains a reparse point: $entry"
            }

            $relative = [IO.Path]::GetRelativePath($RepositoryRoot, $entry).Replace("\", "/")
            if (($attributes -band [IO.FileAttributes]::Directory) -ne 0)
            {
                $records.Add("D`t$relative`ttrue")
                $pending.Push($entry)
            }
            else
            {
                $length = [IO.FileInfo]::new($entry).Length
                $records.Add("F`t$relative`ttrue`t$length`t$(Get-D0M2FFileSha256 -Path $entry)")
            }
        }
    }

    return $records.ToArray()
}

# 获取计划精确保护集的只读、规范化 before/after 快照。
function Get-D0M2FProtectedSnapshot
{
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not [IO.Directory]::Exists($root)) { throw "Repository root does not exist: $root" }
    Assert-D0M2FNoReparsePoint -RepositoryRoot $root -TargetPath $root
    $contract = Get-D0M2FContract
    $records = [Collections.Generic.List[string]]::new()
    $specLines = [Collections.Generic.List[string]]::new()
    foreach ($spec in @($contract.ProtectedPathSpecs))
    {
        $kind = [string]$spec.Kind
        $relative = [string]$spec.Path
        $specLines.Add("$kind`t$relative")
        $path = Resolve-D0M2FProtectedPath -RepositoryRoot $root -RelativePath $relative
        Assert-D0M2FNoReparsePoint -RepositoryRoot $root -TargetPath $path
        if ($kind -ceq "File")
        {
            if ([IO.Directory]::Exists($path)) { throw "Protected file is a directory: $relative" }
            if (-not [IO.File]::Exists($path)) { $records.Add("F`t$relative`tfalse"); continue }
            $records.Add("F`t$relative`ttrue`t$([IO.FileInfo]::new($path).Length)`t$(Get-D0M2FFileSha256 -Path $path)")
            continue
        }

        if ($kind -cne "Tree") { throw "Unsupported protected path kind: $kind" }
        if ([IO.File]::Exists($path)) { throw "Protected tree is a file: $relative" }
        if (-not [IO.Directory]::Exists($path)) { $records.Add("D`t$relative`tfalse"); continue }
        foreach ($record in @(Get-D0M2FTreeRecords -RepositoryRoot $root -TreePath $path -RelativeTreePath $relative))
        {
            $records.Add($record)
        }
    }

    $orderedRecords = $records.ToArray()
    [Array]::Sort($orderedRecords, [StringComparer]::Ordinal)
    $orderedSpecs = $specLines.ToArray()
    [Array]::Sort($orderedSpecs, [StringComparer]::Ordinal)
    $encoding = [Text.UTF8Encoding]::new($false)
    return [pscustomobject]@{
        Schema = "D0M2F-ProtectedSnapshot-v1"
        RepositoryRoot = $root.Replace("\", "/")
        ProtectedSpecSha256 = Get-D0M2FBytesSha256 -Bytes $encoding.GetBytes([string]::Join("`n", $orderedSpecs))
        AggregateSha256 = Get-D0M2FBytesSha256 -Bytes $encoding.GetBytes([string]::Join("`n", $orderedRecords))
        RecordCount = $orderedRecords.Count
        Records = $orderedRecords
    }
}

# 比较两个只读快照并返回明确的新增、删除与总体 unchanged 裁决。
function Compare-D0M2FProtectedSnapshots
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]$Before,
        [Parameter(Mandatory = $true)]$After
    )

    foreach ($snapshot in @($Before, $After))
    {
        if ([string]$snapshot.Schema -cne "D0M2F-ProtectedSnapshot-v1")
        {
            throw "Protected snapshot schema mismatch."
        }
    }

    $beforeSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $afterSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($record in @($Before.Records)) { [void]$beforeSet.Add([string]$record) }
    foreach ($record in @($After.Records)) { [void]$afterSet.Add([string]$record) }
    $removed = @($beforeSet | Where-Object { -not $afterSet.Contains($_) } | Sort-Object)
    $added = @($afterSet | Where-Object { -not $beforeSet.Contains($_) } | Sort-Object)
    $unchanged = [string]$Before.RepositoryRoot -ceq [string]$After.RepositoryRoot -and
        [string]$Before.ProtectedSpecSha256 -ceq [string]$After.ProtectedSpecSha256 -and
        [string]$Before.AggregateSha256 -ceq [string]$After.AggregateSha256 -and
        $removed.Count -eq 0 -and $added.Count -eq 0
    return [pscustomobject]@{
        Schema = "D0M2F-ProtectedComparison-v1"
        Unchanged = $unchanged
        BeforeAggregateSha256 = [string]$Before.AggregateSha256
        AfterAggregateSha256 = [string]$After.AggregateSha256
        Removed = $removed
        Added = $added
    }
}
