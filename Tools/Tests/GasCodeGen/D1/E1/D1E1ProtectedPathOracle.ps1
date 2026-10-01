Set-StrictMode -Version Latest

. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Scaffold/Get-D1RouteScaffold.ps1')

# 将字符串按 UTF-8 编码后计算小写 SHA-256，供只读 inventory 聚合。
function Get-D1E1TextSha256
{
    param([Parameter(Mandatory = $true)][string]$Text)

    return Get-D1BytesSha256 ([Text.Encoding]::UTF8.GetBytes($Text))
}

# 规范化仓库根并拒绝根自身为 reparse point。
function Resolve-D1E1RepositoryRoot
{
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not [IO.Directory]::Exists($root)) { throw "Repository root does not exist: $root" }
    $item = Get-Item -LiteralPath $root -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
    {
        throw "Repository root must not be a reparse point: $root"
    }
    return $root
}

# 将受保护相对路径解析到仓库内；目标允许缺失，但现存父链不得 reparse 或大小写漂移。
function Resolve-D1E1ProtectedPath
{
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if ([IO.Path]::IsPathRooted($RelativePath) -or
        $RelativePath.Contains("\\", [StringComparison]::Ordinal) -or
        @($RelativePath.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -ne 0)
    {
        throw "Protected path must be canonical: $RelativePath"
    }

    $root = Resolve-D1E1RepositoryRoot $RepositoryRoot
    $current = $root
    foreach ($segment in $RelativePath.Split('/'))
    {
        if (-not [IO.Directory]::Exists($current)) { break }
        $match = @(Get-ChildItem -LiteralPath $current -Force |
            Where-Object { $_.Name -ceq $segment })
        if ($match.Count -eq 0)
        {
            $current = Join-Path $current $segment
            continue
        }
        if ($match.Count -ne 1) { throw "Protected path is ambiguous: $RelativePath" }
        if (($match[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "Protected path contains a reparse point: $RelativePath"
        }
        $current = $match[0].FullName
    }

    $resolved = [IO.Path]::GetFullPath($current)
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Protected path escaped the repository root: $RelativePath"
    }
    return $resolved
}

# 返回单文件的 bytes/length/link 身份；缺失状态由调用方显式编码。
function Get-D1E1FileSnapshot
{
    param(
        [Parameter(Mandatory = $true)][string]$AbsolutePath,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    $item = Get-Item -LiteralPath $AbsolutePath -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
    {
        throw "Protected file is a reparse point: $RelativePath"
    }
    $linkCount = Get-D1FileLinkCount $AbsolutePath
    if ($linkCount -ne 1) { throw "Protected file is hard linked: $RelativePath" }
    $bytes = [IO.File]::ReadAllBytes($AbsolutePath)
    return [pscustomobject][ordered]@{
        Path = $RelativePath
        Length = [long]$bytes.LongLength
        Sha256 = Get-D1BytesSha256 $bytes
        LinkCount = $linkCount
    }
}

# 枚举一棵现存受保护树并按 ordinal 路径生成闭合 inventory。
function Get-D1E1TreeInventory
{
    param(
        [Parameter(Mandatory = $true)][string]$TreeRoot,
        [Parameter(Mandatory = $true)][string]$SpecPath
    )

    $rootItem = Get-Item -LiteralPath $TreeRoot -Force
    if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
    {
        throw "Protected tree root is a reparse point: $SpecPath"
    }
    $entries = [Collections.Generic.List[object]]::new()
    foreach ($entry in @(Get-ChildItem -LiteralPath $TreeRoot -Recurse -Force | Sort-Object FullName))
    {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "Protected tree contains a reparse point: $($entry.FullName)"
        }
        if ($entry.PSIsContainer) { continue }
        $relative = [IO.Path]::GetRelativePath($TreeRoot, $entry.FullName).Replace('\', '/')
        $entries.Add((Get-D1E1FileSnapshot $entry.FullName $relative))
    }

    $lines = @($entries | ForEach-Object {
        '{0}|{1}|{2}|{3}' -f $_.Path, $_.Length, $_.Sha256, $_.LinkCount
    })
    return [pscustomobject][ordered]@{
        FileCount = $entries.Count
        InventorySha256 = Get-D1E1TextSha256 ([string]::Join("`n", $lines))
        Files = @($entries)
    }
}

# 按 contract 的 file/tree specs 生成 production 只读快照，缺失也是受保护状态。
function Get-D1E1ProtectedSnapshot
{
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$ContractPath
    )

    $root = Resolve-D1E1RepositoryRoot $RepositoryRoot
    $contract = Get-Content -LiteralPath $ContractPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $snapshots = [Collections.Generic.List[object]]::new()
    foreach ($spec in @($contract.ProtectedPathSpecs))
    {
        $relative = [string]$spec.Path
        $kind = [string]$spec.Kind
        if ($kind -cnotin @('File', 'Tree')) { throw "Unknown protected path kind: $kind" }
        $absolute = Resolve-D1E1ProtectedPath $root $relative
        $fileExists = [IO.File]::Exists($absolute)
        $directoryExists = [IO.Directory]::Exists($absolute)
        if (-not $fileExists -and -not $directoryExists)
        {
            $snapshots.Add([pscustomobject][ordered]@{
                Path = $relative; Kind = $kind; State = 'Missing'
                FileCount = 0; InventorySha256 = Get-D1E1TextSha256 'Missing'
            })
            continue
        }

        if ($kind -ceq 'File')
        {
            if (-not $fileExists) { throw "Protected file became a directory: $relative" }
            $file = Get-D1E1FileSnapshot $absolute $relative
            $snapshots.Add([pscustomobject][ordered]@{
                Path = $relative; Kind = $kind; State = 'Present'
                FileCount = 1
                InventorySha256 = Get-D1E1TextSha256 (
                    '{0}|{1}|{2}' -f $file.Length, $file.Sha256, $file.LinkCount)
            })
            continue
        }

        if (-not $directoryExists) { throw "Protected tree became a file: $relative" }
        $tree = Get-D1E1TreeInventory $absolute $relative
        $snapshots.Add([pscustomobject][ordered]@{
            Path = $relative; Kind = $kind; State = 'Present'
            FileCount = $tree.FileCount; InventorySha256 = $tree.InventorySha256
        })
    }

    $lines = @($snapshots | ForEach-Object {
        '{0}|{1}|{2}|{3}|{4}' -f $_.Path, $_.Kind, $_.State,
            $_.FileCount, $_.InventorySha256
    })
    return [pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-ProtectedSnapshot-v1'
        RepositoryRoot = $root.Replace('\', '/')
        SpecCount = $snapshots.Count
        SnapshotSha256 = Get-D1E1TextSha256 ([string]::Join("`n", $lines))
        Paths = @($snapshots)
    }
}

# 比较 before/after 快照；只有 spec 和聚合身份都相同才允许通过。
function Compare-D1E1ProtectedSnapshots
{
    param(
        [Parameter(Mandatory = $true)]$Before,
        [Parameter(Mandatory = $true)]$After
    )

    $unchanged = [string]$Before.Schema -ceq 'EX-GAS-D1-E1-ProtectedSnapshot-v1' -and
        [string]$After.Schema -ceq 'EX-GAS-D1-E1-ProtectedSnapshot-v1' -and
        [int]$Before.SpecCount -eq [int]$After.SpecCount -and
        [string]$Before.SnapshotSha256 -ceq [string]$After.SnapshotSha256
    return [pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-ProtectedComparison-v1'
        Unchanged = $unchanged
        BeforeSha256 = [string]$Before.SnapshotSha256
        AfterSha256 = [string]$After.SnapshotSha256
    }
}

# 为单个文件或目录生成 tool input identity，供 runner 前后比较。
function Get-D1E1ExternalPathIdentity
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolved = [IO.Path]::GetFullPath($Path)
    if ([IO.File]::Exists($resolved))
    {
        $bytes = [IO.File]::ReadAllBytes($resolved)
        return [pscustomobject][ordered]@{
            Path = $resolved.Replace('\', '/')
            Kind = 'File'; FileCount = 1; Length = [long]$bytes.LongLength
            Sha256 = Get-D1BytesSha256 $bytes
        }
    }
    if ([IO.Directory]::Exists($resolved))
    {
        $tree = Get-D1E1TreeInventory $resolved ([IO.Path]::GetFileName($resolved))
        return [pscustomobject][ordered]@{
            Path = $resolved.Replace('\', '/')
            Kind = 'Tree'; FileCount = $tree.FileCount; Length = $null
            Sha256 = $tree.InventorySha256
        }
    }
    throw "Identity path does not exist: $resolved"
}
