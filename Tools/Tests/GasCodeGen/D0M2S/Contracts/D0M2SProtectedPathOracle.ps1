#requires -Version 7.0
Set-StrictMode -Version Latest

$script:D0M2SContractPath = Join-Path $PSScriptRoot "D0M2S.contract.json"

# 读取 D0-M2S 机器合同并拒绝缺失输入。
function Get-D0M2SContract
{
    if (-not [IO.File]::Exists($script:D0M2SContractPath))
    {
        throw "D0-M2S contract is missing: $script:D0M2SContractPath"
    }

    return [IO.File]::ReadAllText($script:D0M2SContractPath, [Text.Encoding]::UTF8) |
        ConvertFrom-Json -Depth 100
}

# 计算字节序列的 SHA-256 小写十六进制身份。
function Get-D0M2SBytesSha256
{
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][byte[]]$Bytes)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($Bytes))).Replace("-", "").ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

# 以只读共享读取普通文件的原始 SHA-256。
function Get-D0M2SFileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace("-", "").ToLowerInvariant() }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}

# 将合同相对路径约束在仓库根内并拒绝非规范路径。
function Resolve-D0M2SRepositoryPath
{
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if ([IO.Path]::IsPathRooted($RelativePath) -or $RelativePath.Contains("\") -or
        $RelativePath -match "(^|/)\.\.?(?:/|$)")
    {
        throw "Snapshot path must be a canonical repository-relative path: $RelativePath"
    }

    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $resolved = [IO.Path]::GetFullPath((Join-Path $root $RelativePath))
    if (-not $resolved.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Snapshot path escaped the repository root: $RelativePath"
    }

    return $resolved
}

# 检查仓库根至目标的全部现存路径段，禁止 reparse point。
function Assert-D0M2SNoReparsePoint
{
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$TargetPath
    )

    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $relative = [IO.Path]::GetRelativePath($root, [IO.Path]::GetFullPath($TargetPath))
    $segments = @($relative.Split(
            [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar),
            [StringSplitOptions]::RemoveEmptyEntries))
    $current = $root
    foreach ($segment in $segments)
    {
        $current = Join-Path $current $segment
        if (-not [IO.File]::Exists($current) -and -not [IO.Directory]::Exists($current)) { break }
        if (([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "Snapshot path contains a reparse point: $current"
        }
    }
}

# 枚举普通目录树的规范记录，读取前拒绝任意 reparse 子项。
function Get-D0M2STreeRecords
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
                throw "Snapshot tree contains a reparse point: $entry"
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
                $records.Add("F`t$relative`ttrue`t$length`t$(Get-D0M2SFileSha256 -Path $entry)")
            }
        }
    }

    return $records.ToArray()
}

# 根据合同路径集合创建不可变、可重算的仓库快照。
function Get-D0M2SSnapshot
{
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][object[]]$Specs,
        [Parameter(Mandatory = $true)][string]$Schema
    )

    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not [IO.Directory]::Exists($root)) { throw "Repository root does not exist: $root" }
    Assert-D0M2SNoReparsePoint -RepositoryRoot $root -TargetPath $root
    $records = [Collections.Generic.List[string]]::new()
    $specLines = [Collections.Generic.List[string]]::new()
    foreach ($spec in $Specs)
    {
        $kind = [string]$spec.Kind
        $relative = [string]$spec.Path
        $specLines.Add("$kind`t$relative")
        $path = Resolve-D0M2SRepositoryPath -RepositoryRoot $root -RelativePath $relative
        Assert-D0M2SNoReparsePoint -RepositoryRoot $root -TargetPath $path
        if ($kind -ceq "File")
        {
            if ([IO.Directory]::Exists($path)) { throw "Snapshot file is a directory: $relative" }
            if (-not [IO.File]::Exists($path)) { $records.Add("F`t$relative`tfalse"); continue }
            $length = [IO.FileInfo]::new($path).Length
            $records.Add("F`t$relative`ttrue`t$length`t$(Get-D0M2SFileSha256 -Path $path)")
            continue
        }

        if ($kind -cne "Tree") { throw "Unsupported snapshot path kind: $kind" }
        if ([IO.File]::Exists($path)) { throw "Snapshot tree is a file: $relative" }
        if (-not [IO.Directory]::Exists($path)) { $records.Add("D`t$relative`tfalse"); continue }
        foreach ($record in @(Get-D0M2STreeRecords -RepositoryRoot $root -TreePath $path -RelativeTreePath $relative))
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
        Schema = $Schema
        RepositoryRoot = $root.Replace("\", "/")
        SpecSha256 = Get-D0M2SBytesSha256 -Bytes $encoding.GetBytes([string]::Join("`n", $orderedSpecs))
        AggregateSha256 = Get-D0M2SBytesSha256 -Bytes $encoding.GetBytes([string]::Join("`n", $orderedRecords))
        RecordCount = $orderedRecords.Count
        Records = $orderedRecords
    }
}

# 创建 D0-M2S production 保护集快照。
function Get-D0M2SProtectedSnapshot
{
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $contract = Get-D0M2SContract
    return Get-D0M2SSnapshot -RepositoryRoot $RepositoryRoot -Specs @($contract.ProtectedPathSpecs) -Schema "D0M2S-ProtectedSnapshot-v1"
}

# 创建 D0-M2S 工具与复用 fixture 的身份快照。
function Get-D0M2SToolSnapshot
{
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $contract = Get-D0M2SContract
    return Get-D0M2SSnapshot -RepositoryRoot $RepositoryRoot -Specs @($contract.ToolPathSpecs) -Schema "D0M2S-ToolSnapshot-v1"
}

# 比较同类快照并返回精确 added/removed 记录。
function Compare-D0M2SSnapshots
{
    param(
        [Parameter(Mandatory = $true)]$Before,
        [Parameter(Mandatory = $true)]$After,
        [Parameter(Mandatory = $true)][string]$Schema
    )

    if ([string]$Before.Schema -cne [string]$After.Schema) { throw "Snapshot schema mismatch." }
    $beforeSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $afterSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($record in @($Before.Records)) { [void]$beforeSet.Add([string]$record) }
    foreach ($record in @($After.Records)) { [void]$afterSet.Add([string]$record) }
    $removed = @($beforeSet | Where-Object { -not $afterSet.Contains($_) } | Sort-Object)
    $added = @($afterSet | Where-Object { -not $beforeSet.Contains($_) } | Sort-Object)
    $unchanged = [string]$Before.RepositoryRoot -ceq [string]$After.RepositoryRoot -and
        [string]$Before.SpecSha256 -ceq [string]$After.SpecSha256 -and
        [string]$Before.AggregateSha256 -ceq [string]$After.AggregateSha256 -and
        $removed.Count -eq 0 -and $added.Count -eq 0
    return [pscustomobject]@{
        Schema = $Schema
        Unchanged = $unchanged
        BeforeAggregateSha256 = [string]$Before.AggregateSha256
        AfterAggregateSha256 = [string]$After.AggregateSha256
        Removed = $removed
        Added = $added
    }
}
