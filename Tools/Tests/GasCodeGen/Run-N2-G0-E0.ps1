[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$CliPath,
    [string]$OutputPath,
    [switch]$KeepFixtures,
    [switch]$OutputAliasProbeOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue)
{
    $PSNativeCommandUseErrorActionPreference = $false
}

$script:Utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$script:TreeHashDomain = 'EX-GAS-CodeGen-Tree-v1'
$script:RecordHashDomain = 'EX-GAS-CodeGen-GenerationRecord-v1'
$script:ActiveRefHashDomain = 'EX-GAS-CodeGen-ActiveGenerationRef-v1'
$script:PublishIntentHashDomain = 'EX-GAS-CodeGen-PublishIntent-v1'
$script:PreviousPromotionId = '11111111111111111111111111111111'
$script:TargetPromotionId = '22222222222222222222222222222222'
$script:ExpectedCaseCount = 10
$script:SuiteRootPattern = '^gas-codegen-e0-[0-9a-f]{32}$'
$script:SuiteSentinelFileName = '.n2-g0-e0.sentinel'
$script:OutputProbeRootPattern = '^gas-codegen-e0-hardlink-probe-[0-9a-f]{32}$'
$script:OutputProbeSentinelFileName = '.n2-g0-e0-hardlink-probe.owner'

<# 解析绝对路径，并统一去除目录末尾分隔符。 #>
function Resolve-NormalizedPath
{
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
}

<# 把 SHA-256 原始 bytes 转成固定的小写十六进制文本。 #>
function ConvertTo-LowerHex
{
    param([Parameter(Mandatory = $true)][byte[]]$Bytes)

    return [System.BitConverter]::ToString($Bytes).Replace('-', '').ToLowerInvariant()
}

<# 计算内存 bytes 的 SHA-256。 #>
function Get-BytesSha256
{
    param([Parameter(Mandatory = $true)][byte[]]$Bytes)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try
    {
        return ConvertTo-LowerHex -Bytes $sha.ComputeHash($Bytes)
    }
    finally
    {
        $sha.Dispose()
    }
}

<# 计算普通文件全部原始 bytes 的 SHA-256。 #>
function Get-FileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try
    {
        return ConvertTo-LowerHex -Bytes $sha.ComputeHash($stream)
    }
    finally
    {
        $sha.Dispose()
        $stream.Dispose()
    }
}

<# 向 canonical 流写入固定 little-endian 的 Int64。 #>
function Write-CanonicalInt64
{
    param(
        [Parameter(Mandatory = $true)][System.IO.Stream]$Stream,
        [Parameter(Mandatory = $true)][long]$Value
    )

    $bytes = [System.BitConverter]::GetBytes($Value)
    if (-not [System.BitConverter]::IsLittleEndian)
    {
        [System.Array]::Reverse($bytes)
    }
    $Stream.Write($bytes, 0, $bytes.Length)
}

<# 向 canonical 流写入 8-byte 长度前缀与 UTF-8 bytes。 #>
function Write-CanonicalString
{
    param(
        [Parameter(Mandatory = $true)][System.IO.Stream]$Stream,
        [AllowEmptyString()][string]$Value
    )

    $resolvedValue = if ($null -eq $Value) { '' } else { $Value }
    $bytes = $script:Utf8NoBom.GetBytes($resolvedValue)
    Write-CanonicalInt64 -Stream $Stream -Value $bytes.LongLength
    $Stream.Write($bytes, 0, $bytes.Length)
}

<# 按生产协议的 length-prefixed 字段序列计算 canonical SHA-256。 #>
function Get-CanonicalSha256
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]]$Fields
    )

    $stream = [System.IO.MemoryStream]::new()
    try
    {
        foreach ($field in $Fields)
        {
            Write-CanonicalString -Stream $stream -Value $field
        }
        return Get-BytesSha256 -Bytes $stream.ToArray()
    }
    finally
    {
        $stream.Dispose()
    }
}

<# 按生产协议纳入目录、相对路径、文件长度和原始 bytes，计算 tree SHA-256。 #>
function Get-ProtocolTreeSha256
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $resolvedRoot = Resolve-NormalizedPath -Path $Root
    if (-not [System.IO.Directory]::Exists($resolvedRoot))
    {
        throw "Tree root does not exist: $resolvedRoot"
    }

    $entries = @(
        Get-ChildItem -LiteralPath $resolvedRoot -Recurse -Force |
            ForEach-Object {
                $relativePath = $_.FullName.Substring($resolvedRoot.Length).TrimStart('\', '/')
                [pscustomobject]@{
                    IsDirectory = $_.PSIsContainer
                    RelativePath = $relativePath.Replace('\', '/')
                    FullPath = $_.FullName
                }
            }
    )
    $ordinalComparison = [System.Comparison[object]] {
        param($left, $right)
        return [string]::CompareOrdinal($left.RelativePath, $right.RelativePath)
    }
    [System.Array]::Sort($entries, $ordinalComparison)

    $stream = [System.IO.MemoryStream]::new()
    try
    {
        Write-CanonicalString -Stream $stream -Value $script:TreeHashDomain
        foreach ($entry in $entries)
        {
            Write-CanonicalString -Stream $stream -Value $(if ($entry.IsDirectory) { 'D' } else { 'F' })
            Write-CanonicalString -Stream $stream -Value $entry.RelativePath
            if ($entry.IsDirectory)
            {
                continue
            }

            $bytes = [System.IO.File]::ReadAllBytes($entry.FullPath)
            Write-CanonicalInt64 -Stream $stream -Value $bytes.LongLength
            $stream.Write($bytes, 0, $bytes.Length)
        }
        return Get-BytesSha256 -Bytes $stream.ToArray()
    }
    finally
    {
        $stream.Dispose()
    }
}

<# 以 UTF-8 no-BOM 和 LF 写入测试协议 JSON。 #>
function Write-ProtocolJson
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )

    $json = ($Value | ConvertTo-Json -Depth 12).Replace("`r`n", "`n") + "`n"
    [System.IO.File]::WriteAllText($Path, $json, $script:Utf8NoBom)
}

<# 创建严格受控目录，并返回规范化路径。 #>
function New-ControlledDirectory
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolvedPath = Resolve-NormalizedPath -Path $Path
    [System.IO.Directory]::CreateDirectory($resolvedPath) | Out-Null
    return $resolvedPath
}

<# 断言候选路径严格位于给定可信根下。 #>
function Assert-StrictChildPath
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$TrustedRoot
    )

    $resolvedPath = Resolve-NormalizedPath -Path $Path
    $resolvedRoot = Resolve-NormalizedPath -Path $TrustedRoot
    $prefix = $resolvedRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "Path escapes trusted root: $resolvedPath"
    }
}

<# 校验允许根下的每个输出路径名，拒绝 ADS、保留设备名和 Windows 非法文件名。 #>
function Assert-SafeOutputPathNames
{
    param(
        [Parameter(Mandatory = $true)][string]$TrustedRoot,
        [Parameter(Mandatory = $true)][string]$TargetPath
    )

    $resolvedRoot = Resolve-NormalizedPath -Path $TrustedRoot
    $resolvedTarget = Resolve-NormalizedPath -Path $TargetPath
    Assert-StrictChildPath -Path $resolvedTarget -TrustedRoot $resolvedRoot
    $relative = $resolvedTarget.Substring($resolvedRoot.Length).TrimStart('\', '/')
    $segments = @($relative.Split(@('\', '/'), [System.StringSplitOptions]::RemoveEmptyEntries))
    if ($segments.Count -eq 0)
    {
        throw "OutputPath must name a file below the allowed root: $resolvedTarget"
    }

    $invalidCharacters = [System.IO.Path]::GetInvalidFileNameChars()
    foreach ($segment in $segments)
    {
        if ($segment -eq '.' -or $segment -eq '..' -or
            $segment.Contains(':') -or
            $segment.IndexOfAny($invalidCharacters) -ge 0 -or
            $segment.EndsWith('.') -or $segment.EndsWith(' ') -or
            $segment -match '^(?i:con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)')
        {
            throw "Unsafe OutputPath segment: $segment"
        }
    }
}

<# 检查可信根到目标的全部现存路径段，拒绝 reparse point 与中间普通文件。 #>
function Assert-ExistingPathSegmentsHaveNoReparsePoint
{
    param(
        [Parameter(Mandatory = $true)][string]$TrustedRoot,
        [Parameter(Mandatory = $true)][string]$TargetPath
    )

    $resolvedRoot = Resolve-NormalizedPath -Path $TrustedRoot
    $resolvedTarget = Resolve-NormalizedPath -Path $TargetPath
    if ($resolvedTarget -ne $resolvedRoot)
    {
        Assert-StrictChildPath -Path $resolvedTarget -TrustedRoot $resolvedRoot
    }
    $relative = $resolvedTarget.Substring($resolvedRoot.Length).TrimStart('\', '/')
    $segments = @($relative.Split(@('\', '/'), [System.StringSplitOptions]::RemoveEmptyEntries))
    $current = $resolvedRoot
    $paths = @($resolvedRoot) + @($segments | ForEach-Object {
        $current = Join-Path $current $_
        $current
    })
    for ($index = 0; $index -lt $paths.Count; $index++)
    {
        $path = $paths[$index]
        try
        {
            $attributes = [System.IO.File]::GetAttributes($path)
        }
        catch
        {
            $cause = if ($null -ne $_.Exception.InnerException) {
                $_.Exception.InnerException
            } else { $_.Exception }
            if ($cause -is [System.IO.FileNotFoundException] -or
                $cause -is [System.IO.DirectoryNotFoundException])
            {
                break
            }
            throw
        }
        if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "Reparse point is forbidden in trusted path: $path"
        }
        if ($index -lt $paths.Count - 1 -and
            ($attributes -band [System.IO.FileAttributes]::Directory) -eq 0)
        {
            throw "Existing parent path is a file: $path"
        }
    }
}

<# 递归检查目录树全部现存后代，拒绝在删除时跟随任何 reparse point。 #>
function Assert-DirectoryTreeHasNoReparsePoint
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $resolvedRoot = Resolve-NormalizedPath -Path $Root
    foreach ($path in @($resolvedRoot) + @(
            Get-ChildItem -LiteralPath $resolvedRoot -Recurse -Force |
                ForEach-Object { $_.FullName }))
    {
        $attributes = [System.IO.File]::GetAttributes($path)
        if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "Reparse point is forbidden in suite tree: $path"
        }
    }
}

<# 创建精确命名的本轮 suite 根，并写入绑定目录名的 sentinel。 #>
function New-IsolatedSuiteRoot
{
    param([Parameter(Mandatory = $true)][string]$TempRoot)

    $leafName = 'gas-codegen-e0-' + [System.Guid]::NewGuid().ToString('N')
    if ($leafName -notmatch $script:SuiteRootPattern)
    {
        throw "Generated suite root name is invalid: $leafName"
    }
    $root = New-ControlledDirectory -Path (Join-Path $TempRoot $leafName)
    $sentinelPath = Join-Path $root $script:SuiteSentinelFileName
    $sentinelContent = "N2-G0-E0-SUITE-v1`n$leafName`n"
    try
    {
        [System.IO.File]::WriteAllText($sentinelPath, $sentinelContent, $script:Utf8NoBom)
    }
    catch
    {
        Assert-StrictChildPath -Path $root -TrustedRoot $TempRoot
        Assert-DirectoryTreeHasNoReparsePoint -Root $root
        [System.IO.Directory]::Delete($root, $true)
        throw
    }
    return [pscustomobject]@{
        Root = $root
        SentinelPath = $sentinelPath
        SentinelContent = $sentinelContent
    }
}

<# 只删除本轮精确 GUID 根，验证 sentinel、边界和全部后代非 reparse。 #>
function Remove-IsolatedSuiteRoot
{
    param(
        [Parameter(Mandatory = $true)][object]$Suite,
        [Parameter(Mandatory = $true)][string]$TempRoot
    )

    $SuiteRoot = $Suite.Root
    if (-not [System.IO.Directory]::Exists($SuiteRoot))
    {
        return
    }
    Assert-StrictChildPath -Path $SuiteRoot -TrustedRoot $TempRoot
    if ([System.IO.Path]::GetFileName($SuiteRoot) -notmatch $script:SuiteRootPattern)
    {
        throw "Refusing to delete unexpected suite root: $SuiteRoot"
    }
    if (-not [System.IO.File]::Exists($Suite.SentinelPath))
    {
        throw "Refusing to delete suite root without sentinel: $SuiteRoot"
    }
    $actualSentinel = [System.IO.File]::ReadAllText($Suite.SentinelPath, $script:Utf8NoBom)
    Assert-Equal -Actual $actualSentinel -Expected $Suite.SentinelContent -Label 'suite sentinel'
    Assert-DirectoryTreeHasNoReparsePoint -Root $SuiteRoot
    [System.IO.Directory]::Delete($SuiteRoot, $true)
}

<# 创建一个由最小 Core/AutoChess tree、descriptor 和 record 组成的合法 generation。 #>
function New-TestGeneration
{
    param(
        [Parameter(Mandatory = $true)][string]$FixtureRoot,
        [Parameter(Mandatory = $true)][string]$Label,
        [string]$DeclaredCoreTreeSha256,
        [string]$DeclaredAutoChessTreeSha256
    )

    $seedRoot = New-ControlledDirectory -Path (Join-Path $FixtureRoot "Seed/$Label")
    $coreRoot = New-ControlledDirectory -Path (Join-Path $seedRoot 'Core')
    $autoChessRoot = New-ControlledDirectory -Path (Join-Path $seedRoot 'AutoChess')
    [System.IO.File]::WriteAllText((Join-Path $coreRoot 'Payload.txt'), "core-$Label`n", $script:Utf8NoBom)
    [System.IO.File]::WriteAllText((Join-Path $autoChessRoot 'Payload.txt'), "autochess-$Label`n", $script:Utf8NoBom)

    $actualCoreTreeSha256 = Get-ProtocolTreeSha256 -Root $coreRoot
    $actualAutoChessTreeSha256 = Get-ProtocolTreeSha256 -Root $autoChessRoot
    $coreTreeSha256 = if ([string]::IsNullOrEmpty($DeclaredCoreTreeSha256)) {
        $actualCoreTreeSha256
    } else { $DeclaredCoreTreeSha256 }
    $autoChessTreeSha256 = if ([string]::IsNullOrEmpty($DeclaredAutoChessTreeSha256)) {
        $actualAutoChessTreeSha256
    } else { $DeclaredAutoChessTreeSha256 }
    $descriptor = [ordered]@{
        Version = 2
        CoreTreeSha256 = $coreTreeSha256
        AutoChessTreeSha256 = $autoChessTreeSha256
    }
    $descriptorJson = ($descriptor | ConvertTo-Json -Depth 4).Replace("`r`n", "`n") + "`n"
    $descriptorBytes = $script:Utf8NoBom.GetBytes($descriptorJson)
    $generationId = Get-BytesSha256 -Bytes $descriptorBytes
    $generationRoot = New-ControlledDirectory -Path (
        Join-Path $FixtureRoot "ProjectSettings/GasCodeGen/Generations/$generationId")
    Copy-Item -LiteralPath $coreRoot -Destination (Join-Path $generationRoot 'Core') -Recurse
    Copy-Item -LiteralPath $autoChessRoot -Destination (Join-Path $generationRoot 'AutoChess') -Recurse
    [System.IO.File]::WriteAllBytes(
        (Join-Path $generationRoot 'GasPackageDescriptor.json'),
        $descriptorBytes)

    $recordSha256 = Get-CanonicalSha256 -Fields @(
        $script:RecordHashDomain,
        '1',
        $generationId,
        $generationId,
        $coreTreeSha256,
        $autoChessTreeSha256)
    $record = [ordered]@{
        FormatVersion = 1
        GenerationId = $generationId
        DescriptorSha256 = $generationId
        CoreTreeSha256 = $coreTreeSha256
        AutoChessTreeSha256 = $autoChessTreeSha256
        RecordSha256 = $recordSha256
    }
    Write-ProtocolJson -Path (Join-Path $generationRoot 'GenerationRecord.json') -Value $record

    return [pscustomobject]@{
        GenerationId = $generationId
        DescriptorSha256 = $generationId
        CoreTreeSha256 = $coreTreeSha256
        AutoChessTreeSha256 = $autoChessTreeSha256
        ActualCoreTreeSha256 = $actualCoreTreeSha256
        ActualAutoChessTreeSha256 = $actualAutoChessTreeSha256
        GenerationRoot = $generationRoot
    }
}

<# 构造并独立计算 ActiveGenerationRef 的协议内 RefSha256。 #>
function New-TestActiveRef
{
    param(
        [Parameter(Mandatory = $true)][object]$Generation,
        [Parameter(Mandatory = $true)][string]$PromotionId
    )

    $refSha256 = Get-CanonicalSha256 -Fields @(
        $script:ActiveRefHashDomain,
        '1',
        $Generation.GenerationId,
        $Generation.DescriptorSha256,
        $Generation.CoreTreeSha256,
        $Generation.AutoChessTreeSha256,
        $PromotionId)
    return [ordered]@{
        FormatVersion = 1
        GenerationId = $Generation.GenerationId
        DescriptorSha256 = $Generation.DescriptorSha256
        CoreTreeSha256 = $Generation.CoreTreeSha256
        AutoChessTreeSha256 = $Generation.AutoChessTreeSha256
        PromotionId = $PromotionId
        RefSha256 = $refSha256
    }
}

<# 构造并独立计算 PublishIntent 的协议内 IntentSha256，可注入 stale tree hash。 #>
function New-TestPublishIntent
{
    param(
        [AllowNull()][object]$PreviousRef,
        [Parameter(Mandatory = $true)][object]$TargetGeneration,
        [Parameter(Mandatory = $true)][string]$PromotionId,
        [string]$CoreTreeSha256 = $TargetGeneration.CoreTreeSha256,
        [string]$AutoChessTreeSha256 = $TargetGeneration.AutoChessTreeSha256
    )

    $previousGenerationId = if ($null -eq $PreviousRef) { '' } else { $PreviousRef.GenerationId }
    $previousRefSha256 = if ($null -eq $PreviousRef) { '' } else { $PreviousRef.RefSha256 }
    $intentSha256 = Get-CanonicalSha256 -Fields @(
        $script:PublishIntentHashDomain,
        '1',
        $previousGenerationId,
        $previousRefSha256,
        $TargetGeneration.GenerationId,
        $TargetGeneration.DescriptorSha256,
        $CoreTreeSha256,
        $AutoChessTreeSha256,
        $PromotionId)
    return [ordered]@{
        FormatVersion = 1
        PreviousGenerationId = $previousGenerationId
        PreviousRefSha256 = $previousRefSha256
        TargetGenerationId = $TargetGeneration.GenerationId
        DescriptorSha256 = $TargetGeneration.DescriptorSha256
        CoreTreeSha256 = $CoreTreeSha256
        AutoChessTreeSha256 = $AutoChessTreeSha256
        PromotionId = $PromotionId
        IntentSha256 = $intentSha256
    }
}

<# 以 generation bytes 安装临时 active 双根，并写入相应 ref 与初始化标记。 #>
function Set-TestActiveGeneration
{
    param(
        [Parameter(Mandatory = $true)][object]$Fixture,
        [Parameter(Mandatory = $true)][object]$Generation,
        [Parameter(Mandatory = $true)][string]$PromotionId
    )

    foreach ($path in @($Fixture.CoreActiveRoot, $Fixture.AutoChessActiveRoot))
    {
        if ([System.IO.Directory]::Exists($path))
        {
            Assert-StrictChildPath -Path $path -TrustedRoot $Fixture.Root
            [System.IO.Directory]::Delete($path, $true)
        }
    }
    New-ControlledDirectory -Path ([System.IO.Path]::GetDirectoryName($Fixture.CoreActiveRoot)) | Out-Null
    New-ControlledDirectory -Path ([System.IO.Path]::GetDirectoryName($Fixture.AutoChessActiveRoot)) | Out-Null
    Copy-Item -LiteralPath (Join-Path $Generation.GenerationRoot 'Core') `
        -Destination $Fixture.CoreActiveRoot -Recurse
    Copy-Item -LiteralPath (Join-Path $Generation.GenerationRoot 'AutoChess') `
        -Destination $Fixture.AutoChessActiveRoot -Recurse
    $activeRef = New-TestActiveRef -Generation $Generation -PromotionId $PromotionId
    Write-ProtocolJson -Path $Fixture.RefPath -Value $activeRef
    [System.IO.File]::WriteAllText($Fixture.MarkerPath, "EX-GAS-CodeGen-Initialized-v1`n", $script:Utf8NoBom)
    return $activeRef
}

<# 创建一个包含 previous/target 两代且 tree hash 不同的最小临时 Unity 项目。 #>
function New-ProtocolFixture
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $fixtureRoot = New-ControlledDirectory -Path $Root
    New-ControlledDirectory -Path (Join-Path $fixtureRoot 'Assets') | Out-Null
    New-ControlledDirectory -Path (Join-Path $fixtureRoot 'ProjectSettings/GasCodeGen/Generations') | Out-Null
    $previous = New-TestGeneration -FixtureRoot $fixtureRoot -Label 'previous'
    $target = New-TestGeneration -FixtureRoot $fixtureRoot -Label 'target'
    if ($previous.CoreTreeSha256 -eq $target.CoreTreeSha256 -or
        $previous.AutoChessTreeSha256 -eq $target.AutoChessTreeSha256)
    {
        throw 'Synthetic previous and target generations must have distinct tree hashes.'
    }

    return [pscustomobject]@{
        Root = $fixtureRoot
        ControlRoot = Join-Path $fixtureRoot 'ProjectSettings/GasCodeGen'
        RefPath = Join-Path $fixtureRoot 'ProjectSettings/GasCodeGen/ActiveGenerationRef.json'
        IntentPath = Join-Path $fixtureRoot 'ProjectSettings/GasCodeGen/PublishIntent.json'
        MarkerPath = Join-Path $fixtureRoot 'ProjectSettings/GasCodeGen/Initialized.marker'
        CoreActiveRoot = Join-Path $fixtureRoot 'Assets/GAS/Generated/CodeGen'
        AutoChessActiveRoot = Join-Path $fixtureRoot 'Assets/AutoChessDemo/Generated'
        Previous = $previous
        Target = $target
    }
}

<# 按 fault id 把临时项目推进到指定的崩溃或破坏状态。 #>
function Set-FaultState
{
    param(
        [Parameter(Mandatory = $true)][object]$Fixture,
        [Parameter(Mandatory = $true)][string]$FaultId
    )

    $previousRef = New-TestActiveRef `
        -Generation $Fixture.Previous `
        -PromotionId $script:PreviousPromotionId
    $targetRef = New-TestActiveRef `
        -Generation $Fixture.Target `
        -PromotionId $script:TargetPromotionId
    $forgedGeneration = $null

    switch ($FaultId)
    {
        'first-publish' {
            $intent = New-TestPublishIntent -PreviousRef $null `
                -TargetGeneration $Fixture.Target -PromotionId $script:TargetPromotionId
            Write-ProtocolJson -Path $Fixture.IntentPath -Value $intent
        }
        'non-first-rollback' {
            Set-TestActiveGeneration -Fixture $Fixture -Generation $Fixture.Previous `
                -PromotionId $script:PreviousPromotionId | Out-Null
            $intent = New-TestPublishIntent -PreviousRef $previousRef `
                -TargetGeneration $Fixture.Target -PromotionId $script:TargetPromotionId
            Write-ProtocolJson -Path $Fixture.IntentPath -Value $intent
            Copy-TestGenerationToActive -Fixture $Fixture -Generation $Fixture.Target
        }
        'non-first-rollforward' {
            Set-TestActiveGeneration -Fixture $Fixture -Generation $Fixture.Target `
                -PromotionId $script:TargetPromotionId | Out-Null
            Write-ProtocolJson -Path ($Fixture.RefPath + '.bak') -Value $previousRef
            $intent = New-TestPublishIntent -PreviousRef $previousRef `
                -TargetGeneration $Fixture.Target -PromotionId $script:TargetPromotionId
            Write-ProtocolJson -Path $Fixture.IntentPath -Value $intent
        }
        'missing-ref' {
            Set-TestActiveGeneration -Fixture $Fixture -Generation $Fixture.Previous `
                -PromotionId $script:PreviousPromotionId | Out-Null
            [System.IO.File]::Delete($Fixture.RefPath)
        }
        'active-drift' {
            Set-TestActiveGeneration -Fixture $Fixture -Generation $Fixture.Previous `
                -PromotionId $script:PreviousPromotionId | Out-Null
            [System.IO.File]::WriteAllText(
                (Join-Path $Fixture.CoreActiveRoot 'fault-active-drift.txt'),
                "unaccounted-active-byte`n",
                $script:Utf8NoBom)
        }
        'descriptor-post-injection' {
            Set-TestActiveGeneration -Fixture $Fixture -Generation $Fixture.Target `
                -PromotionId $script:TargetPromotionId | Out-Null
            [System.IO.File]::WriteAllText(
                (Join-Path $Fixture.Target.GenerationRoot 'Core/fault-post-descriptor.txt'),
                "post-descriptor-byte`n",
                $script:Utf8NoBom)
        }
        'stale-tree-hash' {
            Set-TestActiveGeneration -Fixture $Fixture -Generation $Fixture.Previous `
                -PromotionId $script:PreviousPromotionId | Out-Null
            $intent = New-TestPublishIntent -PreviousRef $previousRef `
                -TargetGeneration $Fixture.Target -PromotionId $script:TargetPromotionId `
                -CoreTreeSha256 $Fixture.Previous.CoreTreeSha256 `
                -AutoChessTreeSha256 $Fixture.Previous.AutoChessTreeSha256
            Write-ProtocolJson -Path $Fixture.IntentPath -Value $intent
        }
        'idempotent-noop' {
            Set-TestActiveGeneration -Fixture $Fixture -Generation $Fixture.Target `
                -PromotionId $script:TargetPromotionId | Out-Null
        }
        'first-publish-ref-before-marker' {
            Set-TestActiveGeneration -Fixture $Fixture -Generation $Fixture.Target `
                -PromotionId $script:TargetPromotionId | Out-Null
            [System.IO.File]::Delete($Fixture.MarkerPath)
            $intent = New-TestPublishIntent -PreviousRef $null `
                -TargetGeneration $Fixture.Target -PromotionId $script:TargetPromotionId
            Write-ProtocolJson -Path $Fixture.IntentPath -Value $intent
        }
        'generation-stale-tree-binding' {
            Set-TestActiveGeneration -Fixture $Fixture -Generation $Fixture.Previous `
                -PromotionId $script:PreviousPromotionId | Out-Null
            $forgedGeneration = New-TestGeneration -FixtureRoot $Fixture.Root `
                -Label 'forged-target' `
                -DeclaredCoreTreeSha256 $Fixture.Previous.CoreTreeSha256 `
                -DeclaredAutoChessTreeSha256 $Fixture.Previous.AutoChessTreeSha256
            if ($forgedGeneration.ActualCoreTreeSha256 -eq $forgedGeneration.CoreTreeSha256 -or
                $forgedGeneration.ActualAutoChessTreeSha256 -eq $forgedGeneration.AutoChessTreeSha256)
            {
                throw 'Forged generation must declare stale hashes over different actual bytes.'
            }
            $intent = New-TestPublishIntent -PreviousRef $previousRef `
                -TargetGeneration $forgedGeneration -PromotionId $script:TargetPromotionId
            Write-ProtocolJson -Path $Fixture.IntentPath -Value $intent
        }
        default { throw "Unknown fault id: $FaultId" }
    }

    return [pscustomobject]@{
        PreviousRef = $previousRef
        TargetRef = $targetRef
        ForgedGeneration = $forgedGeneration
    }
}

<# 只替换临时 active 双根，构造 intent 已写但 ref 未提交的 synthetic 边界状态。 #>
function Copy-TestGenerationToActive
{
    param(
        [Parameter(Mandatory = $true)][object]$Fixture,
        [Parameter(Mandatory = $true)][object]$Generation
    )

    foreach ($path in @($Fixture.CoreActiveRoot, $Fixture.AutoChessActiveRoot))
    {
        Assert-StrictChildPath -Path $path -TrustedRoot $Fixture.Root
        if ([System.IO.Directory]::Exists($path))
        {
            [System.IO.Directory]::Delete($path, $true)
        }
    }
    Copy-Item -LiteralPath (Join-Path $Generation.GenerationRoot 'Core') `
        -Destination $Fixture.CoreActiveRoot -Recurse
    Copy-Item -LiteralPath (Join-Path $Generation.GenerationRoot 'AutoChess') `
        -Destination $Fixture.AutoChessActiveRoot -Recurse
}

<# 读取全部 generation 的 descriptor、record 与实际双树身份，并形成稳定聚合。 #>
function Get-GenerationStoreState
{
    param([Parameter(Mandatory = $true)][object]$Fixture)

    $generationsRoot = Join-Path $Fixture.ControlRoot 'Generations'
    $directories = @(Get-ChildItem -LiteralPath $generationsRoot -Directory -Force)
    $ordinalComparison = [System.Comparison[object]] {
        param($left, $right)
        return [string]::CompareOrdinal($left.Name, $right.Name)
    }
    [System.Array]::Sort($directories, $ordinalComparison)
    $items = [System.Collections.Generic.List[object]]::new()
    $aggregateFields = [System.Collections.Generic.List[string]]::new()
    $aggregateFields.Add('N2-G0-E0-GenerationStoreState-v1')
    foreach ($directory in $directories)
    {
        $descriptorPath = Join-Path $directory.FullName 'GasPackageDescriptor.json'
        $recordPath = Join-Path $directory.FullName 'GenerationRecord.json'
        $descriptorValue = Get-Content -Raw -LiteralPath $descriptorPath | ConvertFrom-Json
        $recordValue = Get-Content -Raw -LiteralPath $recordPath | ConvertFrom-Json
        $descriptor = [ordered]@{
            Version = $descriptorValue.Version
            CoreTreeSha256 = $descriptorValue.CoreTreeSha256
            AutoChessTreeSha256 = $descriptorValue.AutoChessTreeSha256
            FileSha256 = Get-FileSha256 -Path $descriptorPath
        }
        $record = [ordered]@{
            FormatVersion = $recordValue.FormatVersion
            GenerationId = $recordValue.GenerationId
            DescriptorSha256 = $recordValue.DescriptorSha256
            CoreTreeSha256 = $recordValue.CoreTreeSha256
            AutoChessTreeSha256 = $recordValue.AutoChessTreeSha256
            RecordSha256 = $recordValue.RecordSha256
            FileSha256 = Get-FileSha256 -Path $recordPath
        }
        $coreTreeSha256 = Get-ProtocolTreeSha256 -Root (Join-Path $directory.FullName 'Core')
        $autoChessTreeSha256 = Get-ProtocolTreeSha256 -Root (Join-Path $directory.FullName 'AutoChess')
        $generationAggregateSha256 = Get-CanonicalSha256 -Fields @(
            'N2-G0-E0-GenerationState-v1',
            $directory.Name,
            $descriptor.FileSha256,
            $record.FileSha256,
            $coreTreeSha256,
            $autoChessTreeSha256)
        $items.Add([ordered]@{
            DirectoryGenerationId = $directory.Name
            Descriptor = $descriptor
            Record = $record
            CoreTreeSha256 = $coreTreeSha256
            AutoChessTreeSha256 = $autoChessTreeSha256
            AggregateSha256 = $generationAggregateSha256
        })
        $aggregateFields.Add($directory.Name)
        $aggregateFields.Add($generationAggregateSha256)
    }

    return [ordered]@{
        Count = $items.Count
        AggregateSha256 = Get-CanonicalSha256 -Fields $aggregateFields.ToArray()
        Items = $items.ToArray()
    }
}

<# 读取 ActiveGenerationRef 或其 File.Replace backup 的全部协议字段与原始文件 hash。 #>
function Get-ActiveRefFileState
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [System.IO.File]::Exists($Path))
    {
        return [ordered]@{ Exists = $false }
    }

    $value = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    return [ordered]@{
        Exists = $true
        FormatVersion = $value.FormatVersion
        GenerationId = $value.GenerationId
        DescriptorSha256 = $value.DescriptorSha256
        CoreTreeSha256 = $value.CoreTreeSha256
        AutoChessTreeSha256 = $value.AutoChessTreeSha256
        PromotionId = $value.PromotionId
        RefSha256 = $value.RefSha256
        RefFileSha256 = Get-FileSha256 -Path $Path
    }
}

<# 读取 ref/intent 全字段、marker、active 与 generation store 的完整黑盒状态。 #>
function Get-ProtocolState
{
    param([Parameter(Mandatory = $true)][object]$Fixture)

    $ref = Get-ActiveRefFileState -Path $Fixture.RefPath
    $refBackup = Get-ActiveRefFileState -Path ($Fixture.RefPath + '.bak')
    $intent = if ([System.IO.File]::Exists($Fixture.IntentPath)) {
        $value = Get-Content -Raw -LiteralPath $Fixture.IntentPath | ConvertFrom-Json
        [ordered]@{
            Exists = $true
            FormatVersion = $value.FormatVersion
            PreviousGenerationId = $value.PreviousGenerationId
            PreviousRefSha256 = $value.PreviousRefSha256
            TargetGenerationId = $value.TargetGenerationId
            DescriptorSha256 = $value.DescriptorSha256
            CoreTreeSha256 = $value.CoreTreeSha256
            AutoChessTreeSha256 = $value.AutoChessTreeSha256
            PromotionId = $value.PromotionId
            IntentSha256 = $value.IntentSha256
            IntentFileSha256 = Get-FileSha256 -Path $Fixture.IntentPath
        }
    } else { [ordered]@{ Exists = $false } }

    return [ordered]@{
        Ref = $ref
        Intent = $intent
        MarkerExists = [System.IO.File]::Exists($Fixture.MarkerPath)
        MarkerFileSha256 = if ([System.IO.File]::Exists($Fixture.MarkerPath)) {
            Get-FileSha256 -Path $Fixture.MarkerPath
        } else { '' }
        RefBackup = $refBackup
        CoreActiveFileExists = [System.IO.File]::Exists($Fixture.CoreActiveRoot)
        CoreActiveDirectoryExists = [System.IO.Directory]::Exists($Fixture.CoreActiveRoot)
        CoreActiveTreeSha256 = if ([System.IO.Directory]::Exists($Fixture.CoreActiveRoot)) {
            Get-ProtocolTreeSha256 -Root $Fixture.CoreActiveRoot
        } else { '' }
        AutoChessActiveFileExists = [System.IO.File]::Exists($Fixture.AutoChessActiveRoot)
        AutoChessActiveDirectoryExists = [System.IO.Directory]::Exists($Fixture.AutoChessActiveRoot)
        AutoChessActiveTreeSha256 = if ([System.IO.Directory]::Exists($Fixture.AutoChessActiveRoot)) {
            Get-ProtocolTreeSha256 -Root $Fixture.AutoChessActiveRoot
        } else { '' }
        ActiveDriftEvidenceExists = [System.IO.File]::Exists(
            (Join-Path $Fixture.CoreActiveRoot 'fault-active-drift.txt'))
        DescriptorInjectionEvidenceExists = [System.IO.File]::Exists(
            (Join-Path $Fixture.Target.GenerationRoot 'Core/fault-post-descriptor.txt'))
        GenerationStore = Get-GenerationStoreState -Fixture $Fixture
        ControlTreeSha256 = Get-ProtocolTreeSha256 -Root $Fixture.ControlRoot
    }
}

<# 调用生产 CLI，并把临时/真实绝对路径归一化后生成稳定输出哈希。 #>
function Invoke-CodeGenCliProbe
{
    param(
        [Parameter(Mandatory = $true)][string]$ExecutablePath,
        [Parameter(Mandatory = $true)][object]$Fixture,
        [Parameter(Mandatory = $true)][string]$Mode,
        [Parameter(Mandatory = $true)][string]$RealProjectRoot
    )

    $rawLines = @(& $ExecutablePath --projectRoot $Fixture.Root --mode $Mode 2>&1 |
        ForEach-Object { $_.ToString() })
    $exitCode = $LASTEXITCODE
    $normalizedOutput = ($rawLines -join "`n").Replace("`r`n", "`n")
    $normalizedOutput = $normalizedOutput.Replace($Fixture.Root, '<fixture>')
    $normalizedOutput = $normalizedOutput.Replace($RealProjectRoot, '<project>')
    $outputBytes = $script:Utf8NoBom.GetBytes($normalizedOutput)
    return [ordered]@{
        Mode = $Mode
        ExitCode = $exitCode
        NormalizedOutputSha256 = Get-BytesSha256 -Bytes $outputBytes
        NormalizedOutput = $normalizedOutput
    }
}

<# 在调用前后记录完整协议身份，并校验退出码与稳定错误契约。 #>
function Invoke-Probe
{
    param(
        [Parameter(Mandatory = $true)][string]$ExecutablePath,
        [Parameter(Mandatory = $true)][object]$Fixture,
        [Parameter(Mandatory = $true)][string]$Mode,
        [Parameter(Mandatory = $true)][int]$ExpectedExitCode,
        [AllowEmptyString()][string]$ExpectedOutputPattern,
        [Parameter(Mandatory = $true)][string]$RealProjectRoot
    )

    $before = Get-ProtocolState -Fixture $Fixture
    $invocation = Invoke-CodeGenCliProbe -ExecutablePath $ExecutablePath `
        -Fixture $Fixture -Mode $Mode -RealProjectRoot $RealProjectRoot
    if ($invocation.ExitCode -ne $ExpectedExitCode)
    {
        throw "$Mode expected exit $ExpectedExitCode but got $($invocation.ExitCode)."
    }
    if (-not [string]::IsNullOrEmpty($ExpectedOutputPattern) -and
        $invocation.NormalizedOutput -notmatch [regex]::Escape($ExpectedOutputPattern))
    {
        throw "$Mode output did not contain expected contract: $ExpectedOutputPattern"
    }

    return [ordered]@{
        ExpectedExitCode = $ExpectedExitCode
        ExpectedOutputContract = $ExpectedOutputPattern
        ActualExitCode = $invocation.ExitCode
        NormalizedOutputSha256 = $invocation.NormalizedOutputSha256
        Before = $before
        After = Get-ProtocolState -Fixture $Fixture
    }
}

<# 断言 ref、active 双树、marker 和 intent 已收敛到给定 generation。 #>
function Assert-RecoveredGeneration
{
    param(
        [Parameter(Mandatory = $true)][object]$State,
        [Parameter(Mandatory = $true)][object]$Generation,
        [Parameter(Mandatory = $true)][object]$ExpectedRef
    )

    Assert-Equal -Actual $State.Ref.Exists -Expected $true -Label 'ref exists'
    Assert-Equal -Actual $State.Ref.GenerationId -Expected $Generation.GenerationId -Label 'generation id'
    Assert-Equal -Actual $State.Ref.PromotionId -Expected $ExpectedRef.PromotionId -Label 'promotion id'
    Assert-Equal -Actual $State.Ref.RefSha256 -Expected $ExpectedRef.RefSha256 -Label 'RefSha256'
    Assert-NotEqual -Actual $State.Ref.RefSha256 -Expected $State.Ref.RefFileSha256 `
        -Label 'RefSha256 versus RefFileSha256'
    Assert-Equal -Actual $State.CoreActiveTreeSha256 -Expected $Generation.CoreTreeSha256 `
        -Label 'Core active tree'
    Assert-Equal -Actual $State.AutoChessActiveTreeSha256 -Expected $Generation.AutoChessTreeSha256 `
        -Label 'AutoChess active tree'
    Assert-Equal -Actual $State.MarkerExists -Expected $true -Label 'initialized marker'
    Assert-Equal -Actual $State.Intent.Exists -Expected $false -Label 'intent cleanup'
}

<# 按 outcome token 校验每个 fault point 的最终黑盒状态。 #>
function Assert-ProbeOutcome
{
    param(
        [Parameter(Mandatory = $true)][string]$Outcome,
        [Parameter(Mandatory = $true)][object]$Probe,
        [Parameter(Mandatory = $true)][object]$Fixture,
        [Parameter(Mandatory = $true)][object]$FaultRefs
    )

    $state = $Probe.After
    switch ($Outcome)
    {
        'FirstPublishPreserved' {
            Assert-Equal $state.Ref.Exists $false 'first publish ref absence'
            Assert-Equal $state.Intent.Exists $true 'first publish intent retention'
            Assert-Equal $state.MarkerExists $false 'first publish marker absence'
            Assert-Equal $state.CoreActiveTreeSha256 '' 'first publish Core absence'
            Assert-Equal $state.AutoChessActiveTreeSha256 '' 'first publish AutoChess absence'
        }
        'PreviousRecovered' {
            Assert-RecoveredGeneration -State $state -Generation $Fixture.Previous `
                -ExpectedRef $FaultRefs.PreviousRef
        }
        'TargetRecovered' {
            Assert-RecoveredGeneration -State $state -Generation $Fixture.Target `
                -ExpectedRef $FaultRefs.TargetRef
        }
        'TargetRecoveredWithBackupPreserved' {
            Assert-RecoveredGeneration -State $state -Generation $Fixture.Target `
                -ExpectedRef $FaultRefs.TargetRef
            Assert-Equal $Probe.Before.RefBackup.Exists $true 'rollforward backup precondition'
            Assert-Equal $Probe.Before.RefBackup.RefSha256 $FaultRefs.PreviousRef.RefSha256 `
                'rollforward backup previous ref identity'
            Assert-Equal $Probe.Before.RefBackup.PromotionId $FaultRefs.PreviousRef.PromotionId `
                'rollforward backup previous PromotionId'
            Assert-StateEqual -Actual $state.RefBackup -Expected $Probe.Before.RefBackup `
                -Label 'rollforward backup preservation'
        }
        'MissingRefPreserved' {
            Assert-Equal $state.Ref.Exists $false 'missing ref remains absent'
            Assert-Equal $state.MarkerExists $true 'missing ref marker retention'
            Assert-Equal $state.Intent.Exists $false 'missing ref intent absence'
            Assert-Equal $state.CoreActiveTreeSha256 $Fixture.Previous.CoreTreeSha256 `
                'missing ref Core preservation'
            Assert-Equal $state.AutoChessActiveTreeSha256 $Fixture.Previous.AutoChessTreeSha256 `
                'missing ref AutoChess preservation'
        }
        'ActiveDriftPreserved' {
            Assert-Equal $state.Ref.RefSha256 $FaultRefs.PreviousRef.RefSha256 'active drift ref preservation'
            Assert-NotEqual $state.CoreActiveTreeSha256 $Fixture.Previous.CoreTreeSha256 `
                'active drift Core mismatch'
            Assert-Equal $state.ActiveDriftEvidenceExists $true 'active drift evidence retention'
        }
        'DescriptorInjectionPreserved' {
            Assert-Equal $state.Ref.RefSha256 $FaultRefs.TargetRef.RefSha256 `
                'descriptor injection ref preservation'
            Assert-Equal $state.DescriptorInjectionEvidenceExists $true `
                'descriptor injection evidence retention'
            Assert-Equal $state.Intent.Exists $false 'descriptor injection intent absence'
        }
        'StaleIntentPreserved' {
            Assert-Equal $state.Ref.Exists $true 'stale tree ref existence'
            Assert-Equal $state.Ref.GenerationId $Fixture.Previous.GenerationId `
                'stale tree generation preservation'
            Assert-Equal $state.Ref.RefSha256 $FaultRefs.PreviousRef.RefSha256 `
                'stale tree ref preservation'
            Assert-NotEqual $state.Ref.RefSha256 $state.Ref.RefFileSha256 `
                'stale tree RefSha256 versus RefFileSha256'
            Assert-Equal $state.CoreActiveTreeSha256 $Fixture.Previous.CoreTreeSha256 `
                'stale tree Core preservation'
            Assert-Equal $state.AutoChessActiveTreeSha256 $Fixture.Previous.AutoChessTreeSha256 `
                'stale tree AutoChess preservation'
            Assert-Equal $state.MarkerExists $true 'stale tree marker retention'
            Assert-Equal $state.Intent.Exists $true 'stale tree intent retention'
        }
        'TargetRecoveredRefUnchanged' {
            Assert-RecoveredGeneration -State $state -Generation $Fixture.Target `
                -ExpectedRef $FaultRefs.TargetRef
            Assert-Equal $Probe.Before.MarkerExists $false 'ref-before-marker initial marker absence'
            Assert-Equal $Probe.Before.Intent.Exists $true 'ref-before-marker initial intent'
            Assert-Equal $state.Ref.RefFileSha256 $Probe.Before.Ref.RefFileSha256 `
                'ref-before-marker ref bytes'
            Assert-Equal $state.Ref.PromotionId $Probe.Before.Ref.PromotionId `
                'ref-before-marker PromotionId'
        }
        'ForgedGenerationPreserved' {
            Assert-Equal $state.Ref.RefSha256 $FaultRefs.PreviousRef.RefSha256 `
                'forged generation previous ref preservation'
            Assert-Equal $state.Intent.Exists $true 'forged generation intent retention'
            $forged = @($state.GenerationStore.Items | Where-Object {
                $_.DirectoryGenerationId -eq $FaultRefs.ForgedGeneration.GenerationId
            })
            Assert-Equal $forged.Count 1 'forged generation state count'
            Assert-Equal $forged[0].Descriptor.CoreTreeSha256 $Fixture.Previous.CoreTreeSha256 `
                'forged descriptor stale Core declaration'
            Assert-Equal $forged[0].Record.CoreTreeSha256 $Fixture.Previous.CoreTreeSha256 `
                'forged record stale Core declaration'
            Assert-NotEqual $forged[0].CoreTreeSha256 $Fixture.Previous.CoreTreeSha256 `
                'forged generation actual Core bytes'
            Assert-NotEqual $forged[0].AutoChessTreeSha256 $Fixture.Previous.AutoChessTreeSha256 `
                'forged generation actual AutoChess bytes'
        }
        default { throw "Unknown outcome: $Outcome" }
    }
}

<# 断言两个值完全相同，并给出可定位标签。 #>
function Assert-Equal
{
    param($Actual, $Expected, [Parameter(Mandatory = $true)][string]$Label)

    if ($Actual -ne $Expected)
    {
        throw "$Label mismatch. Expected=[$Expected] Actual=[$Actual]"
    }
}

<# 断言两个值不同，用于固定协议 hash 与文件 hash 的语义边界。 #>
function Assert-NotEqual
{
    param($Actual, $Expected, [Parameter(Mandatory = $true)][string]$Label)

    if ($Actual -eq $Expected)
    {
        throw "$Label unexpectedly matched: [$Actual]"
    }
}

<# 运行同一 fault 的普通 verify 与显式 recover 两个独立夹具。 #>
function Invoke-MatrixCase
{
    param(
        [Parameter(Mandatory = $true)][object]$Spec,
        [Parameter(Mandatory = $true)][string]$SuiteRoot,
        [Parameter(Mandatory = $true)][string]$ExecutablePath,
        [Parameter(Mandatory = $true)][string]$RealProjectRoot
    )

    try
    {
        $verifyFixture = New-ProtocolFixture -Root (Join-Path $SuiteRoot "$($Spec.Id)/verify")
        $verifyRefs = Set-FaultState -Fixture $verifyFixture -FaultId $Spec.FaultId
        $verifyProbe = Invoke-Probe -ExecutablePath $ExecutablePath -Fixture $verifyFixture `
            -Mode 'generation-verify' -ExpectedExitCode $Spec.VerifyExit `
            -ExpectedOutputPattern $Spec.VerifyOutput -RealProjectRoot $RealProjectRoot
        Assert-ProbeOutcome -Outcome $Spec.VerifyOutcome -Probe $verifyProbe `
            -Fixture $verifyFixture -FaultRefs $verifyRefs
        if ($Spec.VerifyExit -ne 0)
        {
            Assert-StateEqual -Actual $verifyProbe.After -Expected $verifyProbe.Before `
                -Label 'expected verify failure atomicity'
        }

        $recoverFixture = New-ProtocolFixture -Root (Join-Path $SuiteRoot "$($Spec.Id)/recover")
        $recoverRefs = Set-FaultState -Fixture $recoverFixture -FaultId $Spec.FaultId
        $recoverProbe = Invoke-Probe -ExecutablePath $ExecutablePath -Fixture $recoverFixture `
            -Mode 'generation-recover' -ExpectedExitCode $Spec.RecoverExit `
            -ExpectedOutputPattern $Spec.RecoverOutput -RealProjectRoot $RealProjectRoot
        Assert-ProbeOutcome -Outcome $Spec.RecoverOutcome -Probe $recoverProbe `
            -Fixture $recoverFixture -FaultRefs $recoverRefs
        if ($Spec.RecoverExit -ne 0)
        {
            Assert-StateEqual -Actual $recoverProbe.After -Expected $recoverProbe.Before `
                -Label 'expected recover failure atomicity'
        }
        if ($Spec.RecoverMustBeNoOp)
        {
            Assert-StateEqual -Actual $recoverProbe.After -Expected $recoverProbe.Before `
                -Label 'first recover/no-op state'
        }

        $repeatProbe = $null
        if ($Spec.RepeatRecover)
        {
            $repeatProbe = Invoke-Probe -ExecutablePath $ExecutablePath -Fixture $recoverFixture `
                -Mode 'generation-recover' -ExpectedExitCode 0 -ExpectedOutputPattern '' `
                -RealProjectRoot $RealProjectRoot
            Assert-ProbeOutcome -Outcome $Spec.RecoverOutcome -Probe $repeatProbe `
                -Fixture $recoverFixture -FaultRefs $recoverRefs
            Assert-StateEqual -Actual $repeatProbe.After -Expected $recoverProbe.After `
                -Label 'repeated recover/no-op state'
        }

        return [ordered]@{
            Id = $Spec.Id
            FaultPoint = $Spec.FaultId
            Status = 'Passed'
            Verify = $verifyProbe
            Recover = $recoverProbe
            RepeatRecover = $repeatProbe
            Failure = ''
        }
    }
    catch
    {
        return [ordered]@{
            Id = $Spec.Id
            FaultPoint = $Spec.FaultId
            Status = 'Failed'
            Verify = $null
            Recover = $null
            RepeatRecover = $null
            Failure = $_.Exception.ToString()
        }
    }
}

<# 通过稳定 JSON 比较两个协议状态快照。 #>
function Assert-StateEqual
{
    param(
        [Parameter(Mandatory = $true)][object]$Actual,
        [Parameter(Mandatory = $true)][object]$Expected,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $actualJson = $Actual | ConvertTo-Json -Depth 12 -Compress
    $expectedJson = $Expected | ConvertTo-Json -Depth 12 -Compress
    Assert-Equal -Actual $actualJson -Expected $expectedJson -Label $Label
}

<# 对真实工程的控制面源码、store 与 active bytes 形成聚合只读快照。 #>
function Get-RealProjectProtocolSnapshot
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $relativePaths = @(
        'Assets/GAS/Editor/CodeGen/Core',
        'Assets/GAS/Editor/CodeGen/GasCodeGenPipeline.cs',
        'Assets/GAS/Editor/CodeGen/Phases/AutoChessDemoCodeGenPhase.cs',
        'Tools/GasCodeGenCli',
        'ProjectSettings/GasCodeGen',
        'Assets/GAS/Generated/CodeGen',
        'Assets/AutoChessDemo/Generated')
    $entries = [System.Collections.Generic.List[object]]::new()
    $aggregateFields = [System.Collections.Generic.List[string]]::new()
    $aggregateFields.Add('N2-G0-E0-RealProjectProtocolSnapshot-v1')
    foreach ($relativePath in $relativePaths)
    {
        $fullPath = Join-Path $Root $relativePath
        $hash = if ([System.IO.Directory]::Exists($fullPath)) {
            Get-ProtocolTreeSha256 -Root $fullPath
        } elseif ([System.IO.File]::Exists($fullPath)) {
            Get-FileSha256 -Path $fullPath
        } else { '<missing>' }
        $entries.Add([ordered]@{ Path = $relativePath.Replace('\', '/'); Sha256 = $hash })
        $aggregateFields.Add($relativePath.Replace('\', '/'))
        $aggregateFields.Add($hash)
    }

    return [ordered]@{
        AggregateSha256 = Get-CanonicalSha256 -Fields $aggregateFields.ToArray()
        Entries = $entries.ToArray()
    }
}

<# 使用真实 active/ref 校准独立 tree 与 RefSha256 oracle，但绝不写真实工程。 #>
function Get-OracleCalibration
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $refPath = Join-Path $Root 'ProjectSettings/GasCodeGen/ActiveGenerationRef.json'
    if (-not [System.IO.File]::Exists($refPath))
    {
        throw "Real project ActiveGenerationRef is required for oracle calibration: $refPath"
    }
    $ref = Get-Content -Raw -LiteralPath $refPath | ConvertFrom-Json
    $computedRefSha256 = Get-CanonicalSha256 -Fields @(
        $script:ActiveRefHashDomain,
        $ref.FormatVersion.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        $ref.GenerationId,
        $ref.DescriptorSha256,
        $ref.CoreTreeSha256,
        $ref.AutoChessTreeSha256,
        $ref.PromotionId)
    $computedCoreTree = Get-ProtocolTreeSha256 -Root (
        Join-Path $Root 'Assets/GAS/Generated/CodeGen')
    $computedAutoChessTree = Get-ProtocolTreeSha256 -Root (
        Join-Path $Root 'Assets/AutoChessDemo/Generated')
    Assert-Equal $computedRefSha256 $ref.RefSha256 'real RefSha256 oracle calibration'
    Assert-Equal $computedCoreTree $ref.CoreTreeSha256 'real Core tree oracle calibration'
    Assert-Equal $computedAutoChessTree $ref.AutoChessTreeSha256 `
        'real AutoChess tree oracle calibration'
    $refFileSha256 = Get-FileSha256 -Path $refPath
    Assert-NotEqual $ref.RefSha256 $refFileSha256 'real RefSha256 versus RefFileSha256'

    return [ordered]@{
        Passed = $true
        RefSha256 = $ref.RefSha256
        RefFileSha256 = $refFileSha256
        CoreTreeSha256 = $computedCoreTree
        AutoChessTreeSha256 = $computedAutoChessTree
    }
}

<# 把相对 OutputPath 固定解析到 ProjectRoot，并限制在 TestResults/GasCodeGen 严格子路径。 #>
function Resolve-SafeOutputPath
{
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$RequestedPath
    )

    $allowedRoot = Resolve-NormalizedPath -Path (Join-Path $Root 'TestResults/GasCodeGen')
    $candidate = Resolve-NormalizedPath -Path $(if ([System.IO.Path]::IsPathRooted($RequestedPath)) {
        $RequestedPath
    } else {
        Join-Path $Root $RequestedPath
    })
    Assert-StrictChildPath -Path $candidate -TrustedRoot $allowedRoot
    Assert-SafeOutputPathNames -TrustedRoot $allowedRoot -TargetPath $candidate
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $Root -TargetPath $candidate
    if ([System.IO.Directory]::Exists($candidate))
    {
        throw "OutputPath must be a file, not a directory: $candidate"
    }
    return [pscustomobject]@{
        AllowedRoot = $allowedRoot
        Path = $candidate
    }
}

<# 精确 unlink 旧叶后以独占 CreateNew 句柄写 aggregate，避免跟随预置 hard link。 #>
function Write-Aggregate
{
    param(
        [Parameter(Mandatory = $true)][object]$Output,
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][object]$Aggregate
    )

    Assert-StrictChildPath -Path $Output.Path -TrustedRoot $Output.AllowedRoot
    Assert-SafeOutputPathNames -TrustedRoot $Output.AllowedRoot -TargetPath $Output.Path
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $Root -TargetPath $Output.Path
    $parent = [System.IO.Path]::GetDirectoryName($Output.Path)
    [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    Assert-SafeOutputPathNames -TrustedRoot $Output.AllowedRoot -TargetPath $Output.Path
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $Root -TargetPath $Output.Path
    if ([System.IO.Directory]::Exists($Output.Path))
    {
        throw "OutputPath became a directory: $($Output.Path)"
    }

    if ([System.IO.File]::Exists($Output.Path))
    {
        [System.IO.File]::Delete($Output.Path)
    }
    if ([System.IO.Directory]::Exists($Output.Path) -or [System.IO.File]::Exists($Output.Path))
    {
        throw "OutputPath leaf still exists after exact unlink: $($Output.Path)"
    }

    Assert-SafeOutputPathNames -TrustedRoot $Output.AllowedRoot -TargetPath $Output.Path
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $Root -TargetPath $Output.Path
    $json = ($Aggregate | ConvertTo-Json -Depth 12).Replace("`r`n", "`n") + "`n"
    $bytes = $script:Utf8NoBom.GetBytes($json)
    $stream = [System.IO.FileStream]::new(
        $Output.Path,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    try
    {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally
    {
        $stream.Dispose()
    }

    Assert-SafeOutputPathNames -TrustedRoot $Output.AllowedRoot -TargetPath $Output.Path
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $Root -TargetPath $Output.Path
    if (-not [System.IO.File]::Exists($Output.Path) -or [System.IO.Directory]::Exists($Output.Path))
    {
        throw "OutputPath is not an ordinary file after write: $($Output.Path)"
    }
    Assert-Equal -Actual (Get-FileSha256 -Path $Output.Path) `
        -Expected (Get-BytesSha256 -Bytes $bytes) -Label 'aggregate bytes'
    return $Output.Path
}

<# 读取并解析刚写入的 aggregate，复核路径边界、普通文件身份与 JSON 可读性。 #>
function Read-Aggregate
{
    param(
        [Parameter(Mandatory = $true)][object]$Output,
        [Parameter(Mandatory = $true)][string]$Root
    )

    Assert-SafeOutputPathNames -TrustedRoot $Output.AllowedRoot -TargetPath $Output.Path
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $Root -TargetPath $Output.Path
    if (-not [System.IO.File]::Exists($Output.Path) -or [System.IO.Directory]::Exists($Output.Path))
    {
        throw "Aggregate is not an ordinary readable file: $($Output.Path)"
    }
    $bytes = [System.IO.File]::ReadAllBytes($Output.Path)
    $json = $script:Utf8NoBom.GetString($bytes)
    return [ordered]@{
        Value = $json | ConvertFrom-Json
        FileSha256 = Get-BytesSha256 -Bytes $bytes
        ByteLength = $bytes.LongLength
    }
}

<# 只有被测执行成功且对应写入、读取均完成时才允许发布确认位。 #>
function Test-PostFinalWriteConfirmationEligibility
{
    param(
        [Parameter(Mandatory = $true)][int]$TestedProcessExitCode,
        [Parameter(Mandatory = $true)][bool]$FinalWriteCompleted,
        [Parameter(Mandatory = $true)][bool]$FinalReadValidated
    )

    return $TestedProcessExitCode -eq 0 -and $FinalWriteCompleted -and $FinalReadValidated
}

<# 以四项最小真值表固定确认位不得出现在启动前或任一失败路径。 #>
function Test-AggregateConfirmationTruthTable
{
    $cases = @(
        [pscustomobject]@{ ExitCode = 0; Write = $true; Read = $true; Expected = $true },
        [pscustomobject]@{ ExitCode = 1; Write = $true; Read = $true; Expected = $false },
        [pscustomobject]@{ ExitCode = 0; Write = $false; Read = $true; Expected = $false },
        [pscustomobject]@{ ExitCode = 0; Write = $true; Read = $false; Expected = $false })
    foreach ($case in $cases)
    {
        $eligible = Test-PostFinalWriteConfirmationEligibility `
            -TestedProcessExitCode $case.ExitCode -FinalWriteCompleted $case.Write `
            -FinalReadValidated $case.Read
        Assert-Equal -Actual $eligible -Expected $case.Expected `
            -Label 'PostFinalWriteConfirmed truth table'
    }
    return $true
}

<# 生成并校验 hardlink probe 根与 owner sentinel 的绑定身份。 #>
function Get-OutputProbeDescriptor
{
    param(
        [Parameter(Mandatory = $true)][string]$AllowedRoot,
        [string]$LeafName
    )

    $resolvedLeafName = if ([string]::IsNullOrWhiteSpace($LeafName)) {
        'gas-codegen-e0-hardlink-probe-' + [System.Guid]::NewGuid().ToString('N')
    } else { $LeafName }
    if ($resolvedLeafName -notmatch $script:OutputProbeRootPattern)
    {
        throw "Output probe root name is invalid: $resolvedLeafName"
    }
    $root = Resolve-NormalizedPath -Path (Join-Path $AllowedRoot $resolvedLeafName)
    Assert-StrictChildPath -Path $root -TrustedRoot $AllowedRoot
    $sentinelPath = Join-Path $root $script:OutputProbeSentinelFileName
    return [pscustomobject]@{
        Root = $root
        SentinelPath = $sentinelPath
        SentinelContent = "N2-G0-E0-HARDLINK-PROBE-v1`n$resolvedLeafName`n"
    }
}

<# 创建带独立 owner sentinel 的精确 GUID hardlink probe 根。 #>
function New-IsolatedOutputProbeRoot
{
    param(
        [Parameter(Mandatory = $true)][string]$AllowedRoot,
        [string]$LeafName,
        [switch]$SimulateSentinelCreateFailure
    )

    $probe = Get-OutputProbeDescriptor -AllowedRoot $AllowedRoot -LeafName $LeafName
    New-ControlledDirectory -Path $probe.Root | Out-Null
    try
    {
        if ($SimulateSentinelCreateFailure)
        {
            throw [System.IO.IOException]::new('Simulated owner sentinel creation failure.')
        }
        [System.IO.File]::WriteAllText(
            $probe.SentinelPath, $probe.SentinelContent, $script:Utf8NoBom)
    }
    catch
    {
        throw "Output probe owner sentinel creation failed; cleanup debt retained at: $($probe.Root). " +
            $_.Exception.Message
    }
    return $probe
}

<# 仅在 GUID 边界、非 reparse 与 owner sentinel 全部匹配时删除精确 probe 根。 #>
function Remove-IsolatedOutputProbeRoot
{
    param(
        [Parameter(Mandatory = $true)][object]$Probe,
        [Parameter(Mandatory = $true)][string]$AllowedRoot
    )

    if (-not [System.IO.Directory]::Exists($Probe.Root))
    {
        return
    }
    Assert-StrictChildPath -Path $Probe.Root -TrustedRoot $AllowedRoot
    if ([System.IO.Path]::GetFileName($Probe.Root) -notmatch $script:OutputProbeRootPattern)
    {
        throw "Refusing to delete unexpected output probe root: $($Probe.Root)"
    }
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $AllowedRoot `
        -TargetPath $Probe.Root
    if (-not [System.IO.File]::Exists($Probe.SentinelPath))
    {
        throw "Refusing to delete output probe root without owner sentinel: $($Probe.Root)"
    }
    $actualSentinel = [System.IO.File]::ReadAllText($Probe.SentinelPath, $script:Utf8NoBom)
    Assert-Equal -Actual $actualSentinel -Expected $Probe.SentinelContent `
        -Label 'output probe owner sentinel'
    Assert-DirectoryTreeHasNoReparsePoint -Root $Probe.Root
    [System.IO.Directory]::Delete($Probe.Root, $true)
}

<# 断言不可信 owner sentinel 会让清理器拒绝删除并保留精确根。 #>
function Test-OutputProbeRemovalRefusal
{
    param(
        [Parameter(Mandatory = $true)][object]$Probe,
        [Parameter(Mandatory = $true)][string]$AllowedRoot,
        [Parameter(Mandatory = $true)][string]$ExpectedErrorPattern,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $errorText = ''
    try
    {
        Remove-IsolatedOutputProbeRoot -Probe $Probe -AllowedRoot $AllowedRoot
    }
    catch
    {
        $errorText = $_.Exception.ToString()
    }
    Assert-Equal -Actual ([System.IO.Directory]::Exists($Probe.Root)) -Expected $true `
        -Label "$Label root retention"
    Assert-Equal -Actual ($errorText -match $ExpectedErrorPattern) -Expected $true `
        -Label "$Label refusal error"
    return $true
}

<# 为负例测试恢复正确 sentinel，再通过同一受控清理器删除夹具。 #>
function Restore-OutputProbeSentinelAndRemove
{
    param(
        [Parameter(Mandatory = $true)][object]$Probe,
        [Parameter(Mandatory = $true)][string]$AllowedRoot
    )

    Assert-StrictChildPath -Path $Probe.Root -TrustedRoot $AllowedRoot
    Assert-DirectoryTreeHasNoReparsePoint -Root $Probe.Root
    if ([System.IO.Directory]::Exists($Probe.SentinelPath))
    {
        throw "Owner sentinel path became a directory: $($Probe.SentinelPath)"
    }
    if ([System.IO.File]::Exists($Probe.SentinelPath))
    {
        [System.IO.File]::Delete($Probe.SentinelPath)
    }
    $bytes = $script:Utf8NoBom.GetBytes($Probe.SentinelContent)
    $stream = [System.IO.FileStream]::new(
        $Probe.SentinelPath, [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try
    {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally
    {
        $stream.Dispose()
    }
    Remove-IsolatedOutputProbeRoot -Probe $Probe -AllowedRoot $AllowedRoot
    Assert-Equal -Actual ([System.IO.Directory]::Exists($Probe.Root)) -Expected $false `
        -Label 'restored output probe cleanup'
}

<# 隔离验证 sentinel 创建失败、缺失与错误内容均不会删除 probe 根。 #>
function Invoke-OutputProbeSentinelNegativeValidation
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $allowedRoot = New-ControlledDirectory -Path (Join-Path $Root 'TestResults/GasCodeGen')
    $caseNames = @('SentinelCreateFailure', 'SentinelMissing', 'SentinelWrongContent')
    $results = [System.Collections.Generic.List[object]]::new()
    foreach ($caseName in $caseNames)
    {
        $leafName = 'gas-codegen-e0-hardlink-probe-' + [System.Guid]::NewGuid().ToString('N')
        $probe = Get-OutputProbeDescriptor -AllowedRoot $allowedRoot -LeafName $leafName
        try
        {
            if ($caseName -eq 'SentinelCreateFailure')
            {
                $creationError = ''
                try
                {
                    New-IsolatedOutputProbeRoot -AllowedRoot $allowedRoot -LeafName $leafName `
                        -SimulateSentinelCreateFailure | Out-Null
                }
                catch
                {
                    $creationError = $_.Exception.ToString()
                }
                Assert-Equal -Actual ($creationError -match 'cleanup debt retained') `
                    -Expected $true -Label 'sentinel creation failure cleanup debt'
            }
            else
            {
                $probe = New-IsolatedOutputProbeRoot -AllowedRoot $allowedRoot -LeafName $leafName
                if ($caseName -eq 'SentinelMissing')
                {
                    [System.IO.File]::Delete($probe.SentinelPath)
                }
                else
                {
                    [System.IO.File]::WriteAllText(
                        $probe.SentinelPath, "wrong-owner`n", $script:Utf8NoBom)
                }
            }
            $pattern = if ($caseName -eq 'SentinelWrongContent') {
                'output probe owner sentinel mismatch'
            } else { 'without owner sentinel' }
            $refused = Test-OutputProbeRemovalRefusal -Probe $probe -AllowedRoot $allowedRoot `
                -ExpectedErrorPattern $pattern -Label $caseName
            $results.Add([ordered]@{ Case = $caseName; RootRetained = $refused })
        }
        finally
        {
            if ([System.IO.Directory]::Exists($probe.Root))
            {
                Restore-OutputProbeSentinelAndRemove -Probe $probe -AllowedRoot $allowedRoot
            }
        }
    }
    return [ordered]@{
        Status = 'Passed'
        Cases = $results.ToArray()
        ProbeRoots = '<deleted>'
    }
}

<# 在独立同卷夹具中证明预置 hard-link 叶不会让 aggregate 写入截断外部目标。 #>
function Invoke-AggregateHardLinkAliasProbe
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $actualAllowedRoot = New-ControlledDirectory -Path (Join-Path $Root 'TestResults/GasCodeGen')
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $Root -TargetPath $actualAllowedRoot
    $probe = New-IsolatedOutputProbeRoot -AllowedRoot $actualAllowedRoot
    $result = $null
    try
    {
        $fakeProjectRoot = New-ControlledDirectory -Path (Join-Path $probe.Root 'FakeProject')
        $fakeAllowedRoot = New-ControlledDirectory -Path (
            Join-Path $fakeProjectRoot 'TestResults/GasCodeGen')
        $sacrificialRoot = New-ControlledDirectory -Path (Join-Path $probe.Root 'Sacrificial')
        $sacrificialPath = Join-Path $sacrificialRoot 'external-target.bin'
        $aliasPath = Join-Path $fakeAllowedRoot 'AliasProbe.aggregate.json'
        $sacrificialBytes = $script:Utf8NoBom.GetBytes(
            "N2-G0-E0 external sacrificial hard-link target`n")
        [System.IO.File]::WriteAllBytes($sacrificialPath, $sacrificialBytes)
        $externalBeforeSha256 = Get-FileSha256 -Path $sacrificialPath
        New-Item -ItemType HardLink -Path $aliasPath -Target $sacrificialPath | Out-Null
        Assert-Equal -Actual (Get-FileSha256 -Path $aliasPath) `
            -Expected $externalBeforeSha256 -Label 'hard-link precondition'

        $output = Resolve-SafeOutputPath -Root $fakeProjectRoot `
            -RequestedPath 'TestResults/GasCodeGen/AliasProbe.aggregate.json'
        $aggregate = [ordered]@{
            SchemaVersion = 1
            Probe = 'N2-G0-E0 aggregate hard-link alias isolation'
            Expected = 'Exact unlink followed by FileMode.CreateNew and FileShare.None.'
            TestedProcessExitCode = 1
            PostFinalWriteConfirmed = $false
        }
        Write-Aggregate -Output $output -Root $fakeProjectRoot -Aggregate $aggregate | Out-Null
        $parsedAggregate = Read-Aggregate -Output $output -Root $fakeProjectRoot
        Assert-Equal -Actual $parsedAggregate.Value.PostFinalWriteConfirmed -Expected $false `
            -Label 'failure-path confirmation parse'

        $confirmationTruthTablePassed = Test-AggregateConfirmationTruthTable

        $externalAfterSha256 = Get-FileSha256 -Path $sacrificialPath
        $outputSha256 = Get-FileSha256 -Path $aliasPath
        $outputAttributes = [System.IO.File]::GetAttributes($aliasPath)
        $ordinaryOutput = [System.IO.File]::Exists($aliasPath) `
            -and -not [System.IO.Directory]::Exists($aliasPath) `
            -and ($outputAttributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0 `
            -and $outputSha256 -ne $externalAfterSha256
        Assert-Equal -Actual $externalAfterSha256 -Expected $externalBeforeSha256 `
            -Label 'external sacrificial target bytes'
        Assert-Equal -Actual $ordinaryOutput -Expected $true `
            -Label 'independent ordinary aggregate output'
        $result = [ordered]@{
            Status = 'Passed'
            Probe = 'AggregateHardLinkAliasIsolation'
            ExternalTargetBeforeSha256 = $externalBeforeSha256
            ExternalTargetAfterSha256 = $externalAfterSha256
            ExternalTargetUnchanged = $true
            OutputSha256 = $outputSha256
            OutputIndependentOrdinaryFile = $true
            CreateMode = 'CreateNew'
            FileShare = 'None'
            ParsedFailureConfirmation = $parsedAggregate.Value.PostFinalWriteConfirmed
            ConfirmationTruthTablePassed = $confirmationTruthTablePassed
            OwnerSentinelValidated = $true
            ProbeRoot = '<deleted>'
        }
    }
    finally
    {
        Remove-IsolatedOutputProbeRoot -Probe $probe -AllowedRoot $actualAllowedRoot
    }
    return $result
}

<# 组装机器可读 aggregate，并明确 synthetic、SKIP 与最终快照口径。 #>
function New-E0Aggregate
{
    param(
        [Parameter(Mandatory = $true)][object]$Context,
        [Parameter(Mandatory = $true)][object]$FinalSnapshot,
        [Parameter(Mandatory = $true)][int]$TestedProcessExitCode,
        [Parameter(Mandatory = $true)][bool]$PostFinalWriteConfirmed,
        [Parameter(Mandatory = $true)][bool]$OverallPassed
    )

    return [ordered]@{
        SchemaVersion = 4
        Suite = 'N2-G0-E0'
        ProtocolBaseline = 'Production CLI over legal synthetic generation/ref/intent/descriptor boundary states.'
        ExpectedCaseCount = $script:ExpectedCaseCount
        GuaranteeBoundary = [ordered]@{
            Verified = 'Production CLI open/recover convergence from constructed synthetic boundary states.'
            SkippedOrBlocked = @(
                [ordered]@{
                    Name = 'ActualProcessTerminationInjection'
                    Status = 'SKIP/blocked'
                    Reason = 'E0 has no production termination hook and does not kill the CLI mid-write.'
                },
                [ordered]@{
                    Name = 'MachinePowerLossDurability'
                    Status = 'SKIP/blocked'
                    Reason = 'Power-loss, directory persistence, and fsync semantics require a dedicated environment.'
                })
            DeferredToE1 = @('stale RSP', 'candidate asmdef graph', 'single-root fault points')
            DeferredToE2 = 'Final rerun after Z integration.'
        }
        HashVocabulary = [ordered]@{
            RefSha256 = 'ActiveGenerationRef canonical protocol self-hash; excludes RefSha256 itself.'
            RefFileSha256 = 'SHA-256 over every raw byte of ActiveGenerationRef.json.'
        }
        Inputs = [ordered]@{
            CliPath = $Context.CliPath
            CliFileSha256 = $Context.CliFileSha256
            HarnessSha256 = $Context.HarnessSha256
            StoreSourceSha256 = $Context.StoreSourceSha256
            ProgramSourceSha256 = $Context.ProgramSourceSha256
            OutputPath = $Context.OutputPath
        }
        OracleCalibration = $Context.Calibration
        RealProjectProtocolBytes = [ordered]@{
            Before = $Context.BeforeSnapshot
            AfterCases = $Context.AfterCasesSnapshot
            AfterAggregateWrite = $FinalSnapshot
            PostFinalWriteConfirmed = $PostFinalWriteConfirmed
            Unchanged = $Context.BeforeSnapshot.AggregateSha256 -eq $FinalSnapshot.AggregateSha256
        }
        Cases = $Context.CaseResults.ToArray()
        Cleanup = [ordered]@{
            Status = $Context.CleanupStatus
            FixtureRoot = $Context.FixtureRoot
            Failure = $Context.CleanupFailure
        }
        Summary = [ordered]@{
            Expected = $script:ExpectedCaseCount
            Total = $Context.CaseResults.Count
            Passed = $Context.PassedCount
            Failed = $Context.FailedCount
            TestedProcessExitCode = $TestedProcessExitCode
            OverallPassed = $OverallPassed
        }
    }
}

$scriptRoot = Resolve-NormalizedPath -Path $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ProjectRoot))
{
    $ProjectRoot = Resolve-NormalizedPath -Path (Join-Path $scriptRoot '../../..')
}
else
{
    $ProjectRoot = Resolve-NormalizedPath -Path $ProjectRoot
}
if ($OutputAliasProbeOnly)
{
    $probeResult = [ordered]@{
        Status = 'Passed'
        HardLinkAlias = Invoke-AggregateHardLinkAliasProbe -Root $ProjectRoot
        SentinelCleanupNegativeValidation = `
            Invoke-OutputProbeSentinelNegativeValidation -Root $ProjectRoot
    }
    $probeResult | ConvertTo-Json -Depth 8
    exit 0
}
if ([string]::IsNullOrWhiteSpace($CliPath))
{
    $CliPath = 'Tools/GasCodeGenCli/bin/Debug/net472/GasCodeGenCli.exe'
}
$CliPath = Resolve-NormalizedPath -Path $(if ([System.IO.Path]::IsPathRooted($CliPath)) {
    $CliPath
} else {
    Join-Path $ProjectRoot $CliPath
})
if (-not [System.IO.File]::Exists($CliPath))
{
    throw "Built GasCodeGen CLI not found. Build it before E0 or pass -CliPath: $CliPath"
}
if ([string]::IsNullOrWhiteSpace($OutputPath))
{
    $OutputPath = 'TestResults/GasCodeGen/N2-G0-E0.aggregate.json'
}
$output = Resolve-SafeOutputPath -Root $ProjectRoot -RequestedPath $OutputPath

$tempRoot = Resolve-NormalizedPath -Path ([System.IO.Path]::GetTempPath())
$beforeSnapshot = Get-RealProjectProtocolSnapshot -Root $ProjectRoot
$calibration = Get-OracleCalibration -Root $ProjectRoot
$caseSpecs = @(
    [pscustomobject]@{
        Id = 'E0-01-first-publish-recovery'; FaultId = 'first-publish'
        VerifyExit = 1; VerifyOutput = 'First publish was interrupted before ActiveGenerationRef commit'
        VerifyOutcome = 'FirstPublishPreserved'
        RecoverExit = 0; RecoverOutput = ''; RecoverOutcome = 'TargetRecovered'; RepeatRecover = $true
        RecoverMustBeNoOp = $false
    },
    [pscustomobject]@{
        Id = 'E0-02-non-first-rollback'; FaultId = 'non-first-rollback'
        VerifyExit = 0; VerifyOutput = ''; VerifyOutcome = 'PreviousRecovered'
        RecoverExit = 0; RecoverOutput = ''; RecoverOutcome = 'PreviousRecovered'; RepeatRecover = $true
        RecoverMustBeNoOp = $false
    },
    [pscustomobject]@{
        Id = 'E0-03-non-first-rollforward'; FaultId = 'non-first-rollforward'
        VerifyExit = 0; VerifyOutput = ''; VerifyOutcome = 'TargetRecoveredWithBackupPreserved'
        RecoverExit = 0; RecoverOutput = ''; RecoverOutcome = 'TargetRecoveredWithBackupPreserved'; RepeatRecover = $true
        RecoverMustBeNoOp = $false
    },
    [pscustomobject]@{
        Id = 'E0-04-initialized-missing-ref'; FaultId = 'missing-ref'
        VerifyExit = 1; VerifyOutput = 'ActiveGenerationRef is missing from an initialized generation store'
        VerifyOutcome = 'MissingRefPreserved'
        RecoverExit = 1; RecoverOutput = 'ActiveGenerationRef is missing from an initialized generation store'
        RecoverOutcome = 'MissingRefPreserved'; RepeatRecover = $false
        RecoverMustBeNoOp = $false
    },
    [pscustomobject]@{
        Id = 'E0-05-active-drift'; FaultId = 'active-drift'
        VerifyExit = 1; VerifyOutput = 'Active generation trees do not match the immutable generation record'
        VerifyOutcome = 'ActiveDriftPreserved'
        RecoverExit = 0; RecoverOutput = ''; RecoverOutcome = 'PreviousRecovered'; RepeatRecover = $true
        RecoverMustBeNoOp = $false
    },
    [pscustomobject]@{
        Id = 'E0-06-descriptor-post-injection'; FaultId = 'descriptor-post-injection'
        VerifyExit = 1; VerifyOutput = 'Generation Core tree hash mismatch'
        VerifyOutcome = 'DescriptorInjectionPreserved'
        RecoverExit = 1; RecoverOutput = 'Generation Core tree hash mismatch'
        RecoverOutcome = 'DescriptorInjectionPreserved'; RepeatRecover = $false
        RecoverMustBeNoOp = $false
    },
    [pscustomobject]@{
        Id = 'E0-07-intent-stale-tree-field-mismatch'; FaultId = 'stale-tree-hash'
        VerifyExit = 1; VerifyOutput = 'PublishIntent does not match its immutable target generation'
        VerifyOutcome = 'StaleIntentPreserved'
        RecoverExit = 1; RecoverOutput = 'PublishIntent does not match its immutable target generation'
        RecoverOutcome = 'StaleIntentPreserved'; RepeatRecover = $false
        RecoverMustBeNoOp = $false
    },
    [pscustomobject]@{
        Id = 'E0-08-idempotent-recover-noop'; FaultId = 'idempotent-noop'
        VerifyExit = 0; VerifyOutput = ''; VerifyOutcome = 'TargetRecovered'
        RecoverExit = 0; RecoverOutput = ''; RecoverOutcome = 'TargetRecovered'; RepeatRecover = $true
        RecoverMustBeNoOp = $true
    },
    [pscustomobject]@{
        Id = 'E0-09-first-publish-ref-before-marker'; FaultId = 'first-publish-ref-before-marker'
        VerifyExit = 0; VerifyOutput = ''; VerifyOutcome = 'TargetRecoveredRefUnchanged'
        RecoverExit = 0; RecoverOutput = ''; RecoverOutcome = 'TargetRecoveredRefUnchanged'
        RepeatRecover = $false; RecoverMustBeNoOp = $false
    },
    [pscustomobject]@{
        Id = 'E0-10-generation-stale-tree-binding'; FaultId = 'generation-stale-tree-binding'
        VerifyExit = 1; VerifyOutput = 'Generation Core tree hash mismatch'
        VerifyOutcome = 'ForgedGenerationPreserved'
        RecoverExit = 1; RecoverOutput = 'Generation Core tree hash mismatch'
        RecoverOutcome = 'ForgedGenerationPreserved'; RepeatRecover = $false
        RecoverMustBeNoOp = $false
    })
Assert-Equal -Actual $caseSpecs.Count -Expected $script:ExpectedCaseCount -Label 'expected case count'

$caseResults = [System.Collections.Generic.List[object]]::new()
$cleanupFailure = ''
$cleanupStatus = 'Pending'
$suite = New-IsolatedSuiteRoot -TempRoot $tempRoot
try
{
    foreach ($spec in $caseSpecs)
    {
        $result = Invoke-MatrixCase -Spec $spec -SuiteRoot $suite.Root `
            -ExecutablePath $CliPath -RealProjectRoot $ProjectRoot
        $caseResults.Add($result)
        Write-Host ("{0} {1}" -f $result.Status, $result.Id)
    }
}
finally
{
    if ($KeepFixtures)
    {
        $cleanupStatus = 'SkippedByRequest'
    }
    else
    {
        try
        {
            Remove-IsolatedSuiteRoot -Suite $suite -TempRoot $tempRoot
            $cleanupStatus = 'Passed'
        }
        catch
        {
            $cleanupStatus = 'Failed'
            $cleanupFailure = $_.Exception.ToString()
        }
    }
}

$afterCasesSnapshot = Get-RealProjectProtocolSnapshot -Root $ProjectRoot
$passedCount = @($caseResults | Where-Object { $_.Status -eq 'Passed' }).Count
$failedCount = $caseResults.Count - $passedCount
$context = [pscustomobject]@{
    CliPath = $CliPath
    CliFileSha256 = Get-FileSha256 -Path $CliPath
    HarnessSha256 = Get-FileSha256 -Path $PSCommandPath
    StoreSourceSha256 = Get-FileSha256 -Path (
        Join-Path $ProjectRoot 'Assets/GAS/Editor/CodeGen/Core/GasCodeGenGenerationStore.cs')
    ProgramSourceSha256 = Get-FileSha256 -Path (Join-Path $ProjectRoot 'Tools/GasCodeGenCli/Program.cs')
    OutputPath = $output.Path
    Calibration = $calibration
    BeforeSnapshot = $beforeSnapshot
    AfterCasesSnapshot = $afterCasesSnapshot
    CaseResults = $caseResults
    CleanupStatus = $cleanupStatus
    CleanupFailure = $cleanupFailure
    FixtureRoot = if ($cleanupStatus -eq 'Passed') { '<deleted>' } else { $suite.Root }
    PassedCount = $passedCount
    FailedCount = $failedCount
}

$draftAggregate = New-E0Aggregate -Context $context -FinalSnapshot $afterCasesSnapshot `
    -TestedProcessExitCode 1 -PostFinalWriteConfirmed $false -OverallPassed $false
$writtenOutput = Write-Aggregate -Output $output -Root $ProjectRoot -Aggregate $draftAggregate
$afterAggregateWrite = Get-RealProjectProtocolSnapshot -Root $ProjectRoot
$basePassed = $failedCount -eq 0 `
    -and $caseResults.Count -eq $script:ExpectedCaseCount `
    -and $cleanupStatus -ne 'Failed' `
    -and $beforeSnapshot.AggregateSha256 -eq $afterAggregateWrite.AggregateSha256
$testedProcessExitCode = if ($basePassed) { 0 } else { 1 }

# 先持久化 confirmation=false 的 final candidate，只有其写入和解析复核完成后才计算确认位。
$unconfirmedFinal = New-E0Aggregate -Context $context -FinalSnapshot $afterAggregateWrite `
    -TestedProcessExitCode $testedProcessExitCode `
    -PostFinalWriteConfirmed $false -OverallPassed $false
Write-Aggregate -Output $output -Root $ProjectRoot -Aggregate $unconfirmedFinal | Out-Null
$unconfirmedRead = Read-Aggregate -Output $output -Root $ProjectRoot
Assert-Equal -Actual $unconfirmedRead.Value.RealProjectProtocolBytes.PostFinalWriteConfirmed `
    -Expected $false -Label 'unconfirmed final aggregate confirmation'
Assert-Equal -Actual $unconfirmedRead.Value.Summary.TestedProcessExitCode `
    -Expected $testedProcessExitCode -Label 'unconfirmed final tested process exit code'
$postCandidateSnapshot = Get-RealProjectProtocolSnapshot -Root $ProjectRoot
$candidateReadValidated = $unconfirmedRead.ByteLength -gt 0 `
    -and $beforeSnapshot.AggregateSha256 -eq $postCandidateSnapshot.AggregateSha256
$confirmationEligible = Test-PostFinalWriteConfirmationEligibility `
    -TestedProcessExitCode $testedProcessExitCode -FinalWriteCompleted $true `
    -FinalReadValidated $candidateReadValidated

$confirmedFinal = New-E0Aggregate -Context $context -FinalSnapshot $postCandidateSnapshot `
    -TestedProcessExitCode $testedProcessExitCode `
    -PostFinalWriteConfirmed $confirmationEligible -OverallPassed $confirmationEligible
$finalizationFailure = ''
$finalReadValidated = $false
$postFinalWriteSnapshot = $postCandidateSnapshot
try
{
    Write-Aggregate -Output $output -Root $ProjectRoot -Aggregate $confirmedFinal | Out-Null
    $confirmedRead = Read-Aggregate -Output $output -Root $ProjectRoot
    Assert-Equal -Actual $confirmedRead.Value.RealProjectProtocolBytes.PostFinalWriteConfirmed `
        -Expected $confirmationEligible -Label 'confirmed final aggregate confirmation'
    Assert-Equal -Actual $confirmedRead.Value.Summary.TestedProcessExitCode `
        -Expected $testedProcessExitCode -Label 'confirmed final tested process exit code'
    $postFinalWriteSnapshot = Get-RealProjectProtocolSnapshot -Root $ProjectRoot
    $finalReadValidated = $confirmedRead.ByteLength -gt 0 `
        -and $beforeSnapshot.AggregateSha256 -eq $postFinalWriteSnapshot.AggregateSha256
}
catch
{
    $finalizationFailure = $_.Exception.ToString()
}
$overallPassed = $confirmationEligible -and $finalReadValidated `
    -and [string]::IsNullOrEmpty($finalizationFailure)
if (-not $overallPassed)
{
    $failureAggregate = New-E0Aggregate -Context $context -FinalSnapshot $postFinalWriteSnapshot `
        -TestedProcessExitCode 1 -PostFinalWriteConfirmed $false -OverallPassed $false
    Write-Aggregate -Output $output -Root $ProjectRoot -Aggregate $failureAggregate | Out-Null
    $failureRead = Read-Aggregate -Output $output -Root $ProjectRoot
    Assert-Equal -Actual $failureRead.Value.RealProjectProtocolBytes.PostFinalWriteConfirmed `
        -Expected $false -Label 'failure aggregate confirmation'
    Assert-Equal -Actual $failureRead.Value.Summary.TestedProcessExitCode `
        -Expected 1 -Label 'failure aggregate tested process exit code'
    $postFinalWriteSnapshot = Get-RealProjectProtocolSnapshot -Root $ProjectRoot
}

Write-Host "Aggregate: $writtenOutput"
Write-Host "Cases: $passedCount/$($caseResults.Count) passed (expected $script:ExpectedCaseCount)"
Write-Host "Final real project protocol fingerprint: $($postFinalWriteSnapshot.AggregateSha256)"
Write-Host "Real project protocol bytes unchanged: $(
    $beforeSnapshot.AggregateSha256 -eq $postFinalWriteSnapshot.AggregateSha256)"
if (-not $overallPassed)
{
    exit 1
}
exit 0
