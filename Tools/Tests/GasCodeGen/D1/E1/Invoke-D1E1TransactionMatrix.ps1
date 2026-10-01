[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunId,
    [Parameter(Mandatory = $true)][string]$ContractPath,
    [Parameter(Mandatory = $true)][string]$ScaffoldContractPath,
    [Parameter(Mandatory = $true)][string]$FixtureTemplateRoot,
    [Parameter(Mandatory = $true)][string]$OwnedFixtureRoot,
    [Parameter(Mandatory = $true)][string]$EvidenceRoot,
    [Parameter(Mandatory = $true)][string]$AnalyzerPath,
    [Parameter(Mandatory = $true)][string]$SelectorAPath,
    [Parameter(Mandatory = $true)][string]$SelectorBPath,
    [Parameter(Mandatory = $true)][string]$TransactionAdapterPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [int]$TimeoutSeconds = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Scaffold/Get-D1RouteScaffold.ps1')

# 计算文件的完整 bytes/length/SHA/Base64 快照，缺失时显式返回 Missing。
function Get-D1E1SelectorSnapshot
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [IO.File]::Exists($Path))
    {
        if ([IO.Directory]::Exists($Path)) { throw "Selector path is a directory: $Path" }
        return [pscustomobject][ordered]@{
            State = 'Missing'; Length = 0; Sha256 = $null; BytesBase64 = $null
        }
    }
    $bytes = [IO.File]::ReadAllBytes($Path)
    return [pscustomobject][ordered]@{
        State = 'Present'
        Length = [long]$bytes.LongLength
        Sha256 = [Convert]::ToHexString(
            [Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        BytesBase64 = [Convert]::ToBase64String($bytes)
    }
}

# 按 byte 逐项验证 wire 前缀，避免 PowerShell 对 LINQ generic overload 的不稳定绑定。
function Test-D1E1BytePrefix
{
    param(
        [Parameter(Mandatory = $true)][byte[]]$Bytes,
        [Parameter(Mandatory = $true)][byte[]]$Prefix
    )

    if ($Bytes.Length -lt $Prefix.Length) { return $false }
    for ($index = 0; $index -lt $Prefix.Length; $index++)
    {
        if ($Bytes[$index] -ne $Prefix[$index]) { return $false }
    }
    return $true
}

# 用 production 唯一 codec 解码并重编码 selector，拒绝仅有 magic 前缀的伪 candidate。
function Get-D1E1ProductionSelectorIdentity
{
    param(
        [Parameter(Mandatory = $true)][string]$ResolvedAnalyzerPath,
        [Parameter(Mandatory = $true)][string]$ResolvedSelectorPath
    )

    $assembly = [Reflection.Assembly]::LoadFrom($ResolvedAnalyzerPath)
    $codec = $assembly.GetType('Gas.CodeGen.SourceGenerator.GasSourceBundleCodec', $true)
    $decode = $codec.GetMethod('DecodeSelector', [type[]]@([byte[]]))
    $encode = $codec.GetMethod('EncodeSelector', [Reflection.BindingFlags]'Public,Static')
    if ($null -eq $decode -or $null -eq $encode) { throw 'Production selector codec ABI is missing.' }
    $bytes = [IO.File]::ReadAllBytes($ResolvedSelectorPath)
    try { $bundle = $decode.Invoke($null, (,[byte[]]$bytes)) }
    catch { throw "Production selector decode failed: $ResolvedSelectorPath`n$($_.Exception)" }
    $roundTrip = [byte[]]$encode.Invoke($null, (,[object]$bundle))
    $sha256 = Get-D1BytesSha256 $bytes
    if ($roundTrip.LongLength -ne $bytes.LongLength -or
        (Get-D1BytesSha256 $roundTrip) -cne $sha256 -or
        @($bundle.Entries).Count -ne 5 -or [bool]$bundle.FullSemanticEligibility)
    {
        throw "Production selector round-trip/entry/eligibility invariant failed: $ResolvedSelectorPath"
    }
    return [pscustomobject][ordered]@{
        Path = $ResolvedSelectorPath.Replace('\', '/'); Length = [long]$bytes.LongLength
        Sha256 = $sha256; EntryCount = @($bundle.Entries).Count
        AnalyzerSha256 = [Convert]::ToHexString([byte[]]$bundle.AnalyzerSha256).ToLowerInvariant()
        RouteScaffoldSha256 = [Convert]::ToHexString([byte[]]$bundle.RouteScaffoldSha256).ToLowerInvariant()
        FullSemanticEligibility = [bool]$bundle.FullSemanticEligibility
    }
}

# 以 UTF-8 无 BOM、LF、CreateNew 和 Flush(true) 写 JSON 证据。
function Write-D1E1FreshJson
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$Value
    )

    $parent = [IO.Path]::GetDirectoryName($Path)
    if ([string]::IsNullOrWhiteSpace($parent) -or [IO.File]::Exists($Path) -or
        [IO.Directory]::Exists($Path))
    {
        throw "Evidence path must be a fresh file with a parent: $Path"
    }
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $json = ($Value | ConvertTo-Json -Depth 64).Replace("`r`n", "`n") + "`n"
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
    $stream = [IO.FileStream]::new(
        $Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try
    {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally
    {
        $stream.Dispose()
    }
}

# 将 fixture template 逐项复制到 fresh case root，避免 wildcard 和既有目标合并。
function Copy-D1E1FixtureTemplate
{
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if ([IO.Directory]::Exists($Destination) -or [IO.File]::Exists($Destination))
    {
        throw "Transaction fixture must be fresh: $Destination"
    }
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($entry in @(Get-ChildItem -LiteralPath $Source -Force))
    {
        Copy-Item -LiteralPath $entry.FullName -Destination $Destination -Recurse
    }
}

# 依据 case 的 InitialSelector 精确布置 Missing/A/B/Corrupt 初态。
function Set-D1E1InitialSelector
{
    param(
        [Parameter(Mandatory = $true)][string]$SelectorPath,
        [Parameter(Mandatory = $true)][string]$InitialSelector,
        [Parameter(Mandatory = $true)][byte[]]$SelectorA,
        [Parameter(Mandatory = $true)][byte[]]$SelectorB,
        [Parameter(Mandatory = $true)][byte[]]$Corrupt
    )

    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($SelectorPath)) | Out-Null
    switch ($InitialSelector)
    {
        'Missing' { return }
        'A' { [IO.File]::WriteAllBytes($SelectorPath, $SelectorA); return }
        'B' { [IO.File]::WriteAllBytes($SelectorPath, $SelectorB); return }
        'Corrupt' { [IO.File]::WriteAllBytes($SelectorPath, $Corrupt); return }
        default { throw "Unknown InitialSelector: $InitialSelector" }
    }
}

# 根据 adapter 文件类型构造无 shell 参与的可执行文件与参数前缀。
function Get-D1E1AdapterCommand
{
    param([Parameter(Mandatory = $true)][string]$AdapterPath)

    $resolved = [IO.Path]::GetFullPath($AdapterPath)
    if (-not [IO.File]::Exists($resolved)) { throw "Transaction adapter is missing: $resolved" }
    switch ([IO.Path]::GetExtension($resolved).ToLowerInvariant())
    {
        '.ps1' {
            return [pscustomobject]@{
                Executable = (Get-Process -Id $PID).Path
                Prefix = @('-NoProfile', '-File', $resolved)
            }
        }
        '.dll' { return [pscustomobject]@{ Executable = 'dotnet'; Prefix = @($resolved) } }
        '.exe' { return [pscustomobject]@{ Executable = $resolved; Prefix = @() } }
        default { throw "Unsupported transaction adapter type: $resolved" }
    }
}

# 以 ArgumentList 启动 adapter，绑定 stdout/stderr/exit/timeout 并强杀自己的进程树。
function Invoke-D1E1AdapterProcess
{
    param(
        [Parameter(Mandatory = $true)]$Command,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][int]$TimeoutMilliseconds
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Command.Executable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @($Command.Prefix) + $Arguments)
    {
        [void]$startInfo.ArgumentList.Add([string]$argument)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $startedUtc = [DateTime]::UtcNow.ToString('O')
    try
    {
        if (-not $process.Start()) { throw 'Transaction adapter process did not start.' }
        $processId = $process.Id
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutMilliseconds)
        if ($timedOut)
        {
            $process.Kill($true)
            [void]$process.WaitForExit(30000)
        }
        return [pscustomobject][ordered]@{
            StartedUtc = $startedUtc; CompletedUtc = [DateTime]::UtcNow.ToString('O')
            ProcessId = $processId; TimedOut = $timedOut
            ExitCode = $(if ($timedOut) { $null } else { $process.ExitCode })
            StdOut = $stdoutTask.GetAwaiter().GetResult()
            StdErr = $stderrTask.GetAwaiter().GetResult()
        }
    }
    finally
    {
        $process.Dispose()
    }
}

# 安全读取可选 JSON 控制文件并携带原始 bytes 身份。
function Get-D1E1OptionalJsonFile
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [IO.File]::Exists($Path))
    {
        return [pscustomobject][ordered]@{ Exists = $false }
    }
    $bytes = [IO.File]::ReadAllBytes($Path)
    $parsed = $null
    $parseable = $true
    try { $parsed = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json }
    catch { $parseable = $false }
    return [pscustomobject][ordered]@{
        Exists = $true; Parseable = $parseable; Length = [long]$bytes.LongLength
        Sha256 = [Convert]::ToHexString(
            [Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        BytesBase64 = [Convert]::ToBase64String($bytes); Parsed = $parsed
    }
}

# 枚举单 case generation archive 下的 Tx intent，保留完整 raw bytes 并拒绝链接文件。
function Get-D1E1ArchivedIntents
{
    param(
        [Parameter(Mandatory = $true)][string]$FixtureRoot,
        [Parameter(Mandatory = $true)][string]$ArchiveRelativePath
    )

    $root = Join-Path $FixtureRoot $ArchiveRelativePath
    if (-not [IO.Directory]::Exists($root)) { return @() }
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($file in @(Get-ChildItem -LiteralPath $root -Recurse -Filter '*.json' -File |
        Where-Object { $_.Directory.Name -ceq 'Tx' } | Sort-Object FullName))
    {
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            (Get-D1FileLinkCount $file.FullName) -ne 1)
        {
            throw "Archived intent is linked: $($file.FullName)"
        }
        $raw = Get-D1E1OptionalJsonFile $file.FullName
        if (-not $raw.Parseable) { throw "Archived intent is invalid JSON: $($file.FullName)" }
        [void](Get-D1E1VerifiedIntent $raw.Parsed 'Archived intent')
        $rows.Add([pscustomobject][ordered]@{
            Path = [IO.Path]::GetRelativePath($FixtureRoot, $file.FullName).Replace('\', '/')
            Length = $raw.Length; Sha256 = $raw.Sha256; BytesBase64 = $raw.BytesBase64
            Parsed = $raw.Parsed
        })
    }
    return @($rows)
}

# 返回必需 response 属性，禁止 schema 漏字段被 PowerShell 空值语义吞掉。
function Get-D1E1RequiredProperty
{
    param(
        [Parameter(Mandatory = $true)]$Object,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { throw "$Context is missing property '$Name'." }
    return $property.Value
}

# 要求 JSON object 恰好包含冻结属性集，拒绝 adapter 通过隐藏字段控制或扩展证据语义。
function Assert-D1E1ExactProperties
{
    param(
        [Parameter(Mandatory = $true)]$Object,
        [Parameter(Mandatory = $true)][string[]]$Expected,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $actual = @($Object.PSObject.Properties | ForEach-Object { [string]$_.Name })
    if ($actual.Count -ne $Expected.Count -or
        @($actual | Sort-Object -Unique).Count -ne $actual.Count)
    {
        throw "$Context property count/uniqueness mismatch."
    }
    foreach ($name in $Expected)
    {
        if (@($actual | Where-Object { $_ -ceq $name }).Count -ne 1)
        {
            throw "$Context is missing exact property '$name'."
        }
    }
}

# 验证 response raw file snapshot 的 Missing/Present exact wire，并返回重算后的 bytes。
function Get-D1E1VerifiedRawSnapshot
{
    param(
        [Parameter(Mandatory = $true)]$Snapshot,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $names = @('State', 'Length', 'Sha256', 'BytesBase64')
    Assert-D1E1ExactProperties $Snapshot $names $Context
    foreach ($name in $names)
    {
        [void](Get-D1E1RequiredProperty $Snapshot $name $Context)
    }
    if ([string]$Snapshot.State -ceq 'Missing')
    {
        if ([long]$Snapshot.Length -ne 0 -or -not [string]::IsNullOrEmpty([string]$Snapshot.Sha256) -or
            -not [string]::IsNullOrEmpty([string]$Snapshot.BytesBase64))
        {
            throw "$Context Missing snapshot carries bytes identity."
        }
        return [pscustomobject]@{ State = 'Missing'; Bytes = [byte[]]@() }
    }
    if ([string]$Snapshot.State -cne 'Present' -or [long]$Snapshot.Length -le 0 -or
        [string]$Snapshot.Sha256 -cnotmatch '^[0-9a-f]{64}$')
    {
        throw "$Context snapshot state/length/SHA is invalid."
    }
    try { $bytes = [Convert]::FromBase64String([string]$Snapshot.BytesBase64) }
    catch { throw "$Context snapshot Base64 is invalid." }
    if ([Convert]::ToBase64String($bytes) -cne [string]$Snapshot.BytesBase64 -or
        [long]$bytes.LongLength -ne [long]$Snapshot.Length -or
        (Get-D1BytesSha256 $bytes) -cne [string]$Snapshot.Sha256)
    {
        throw "$Context snapshot bytes identity mismatch."
    }
    return [pscustomobject]@{ State = 'Present'; Bytes = [byte[]]$bytes }
}

# 比较两个 raw snapshot 的完整字段，禁止同 SHA 之外的状态或 Base64 形状漂移。
function Test-D1E1RawSnapshotEqual
{
    param(
        [Parameter(Mandatory = $true)]$Left,
        [Parameter(Mandatory = $true)]$Right
    )

    return [string]$Left.State -ceq [string]$Right.State -and
        [long]$Left.Length -eq [long]$Right.Length -and
        [string]$Left.Sha256 -ceq [string]$Right.Sha256 -and
        [string]$Left.BytesBase64 -ceq [string]$Right.BytesBase64
}

# 按 production Store 的 U32LE length-prefixed UTF-8 算法独立计算自哈希。
function Get-D1E1CanonicalHash
{
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][AllowEmptyString()][string[]]$Fields)

    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream, [Text.UTF8Encoding]::new($false, $true), $true)
    try
    {
        foreach ($field in $Fields)
        {
            $bytes = [Text.UTF8Encoding]::new($false, $true).GetBytes($(if ($null -eq $field) { '' } else { $field }))
            $writer.Write([uint32]$bytes.Length)
            $writer.Write($bytes)
        }
        $writer.Flush()
        return Get-D1BytesSha256 $stream.ToArray()
    }
    finally
    {
        $writer.Dispose()
        $stream.Dispose()
    }
}

# 解码 canonical Base64 并验证其声明 SHA；空值是否允许由调用方显式决定。
function Get-D1E1VerifiedBase64Bytes
{
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Base64,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Sha256,
        [Parameter(Mandatory = $true)][bool]$AllowEmpty,
        [Parameter(Mandatory = $true)][string]$Context
    )

    if ($AllowEmpty -and [string]::IsNullOrEmpty($Base64) -and [string]::IsNullOrEmpty($Sha256))
    {
        return [byte[]]@()
    }
    if ($Sha256 -cnotmatch '^[0-9a-f]{64}$') { throw "$Context SHA is invalid." }
    try { $bytes = [Convert]::FromBase64String($Base64) }
    catch { throw "$Context Base64 is invalid." }
    if ($bytes.Length -eq 0 -or [Convert]::ToBase64String($bytes) -cne $Base64 -or
        (Get-D1BytesSha256 $bytes) -cne $Sha256) { throw "$Context bytes identity mismatch." }
    return [byte[]]$bytes
}

# 从 raw snapshot 以严格 UTF-8 解析 JSON；Missing 显式返回 null。
function Get-D1E1VerifiedRawJson
{
    param(
        [Parameter(Mandatory = $true)]$Snapshot,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $verified = Get-D1E1VerifiedRawSnapshot $Snapshot $Context
    if ([string]$verified.State -ceq 'Missing') { return $null }
    try
    {
        $text = [Text.UTF8Encoding]::new($false, $true).GetString([byte[]]$verified.Bytes)
        return $text | ConvertFrom-Json
    }
    catch { throw "$Context is not strict UTF-8 JSON." }
}

# 独立验证 Store intent 全字段、previous/target bytes 与 IntentSha256 自哈希。
function Get-D1E1VerifiedIntent
{
    param(
        [Parameter(Mandatory = $true)]$Intent,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $names = @(
        'Version', 'ExclusiveClaimId', 'OwnerSentinel', 'CommitAttemptState', 'ClosureOutcome',
        'GenerationId', 'PromotionId', 'PreviousSelectorState', 'PreviousSelectorBytesBase64',
        'PreviousSelectorSha256', 'TargetSelectorBytesBase64', 'TargetSelectorSha256',
        'DescriptorSha256', 'InstallEnvelopeSha256', 'RequiredArtifactSetContractHash',
        'ArtifactManifestHash', 'SourceArtifactInventoryHash', 'AnalyzerSha256',
        'RouteScaffoldSha256', 'CandidateCompilePlanSha256', 'ReceiptExclusiveClaimId',
        'ReceiptPromotionId', 'ReceiptTargetSelectorSha256', 'IntentSha256')
    Assert-D1E1ExactProperties $Intent $names $Context
    foreach ($name in $names) { [void](Get-D1E1RequiredProperty $Intent $name $Context) }
    $state = [string]$Intent.CommitAttemptState
    if ($state -cnotin @('NotStarted', 'Armed', 'Committed', 'CompetitionFailed', 'Indeterminate', 'NoOp'))
    {
        throw "$Context has unknown commit state."
    }
    foreach ($name in @(
        'DescriptorSha256', 'InstallEnvelopeSha256', 'RequiredArtifactSetContractHash',
        'ArtifactManifestHash', 'SourceArtifactInventoryHash', 'AnalyzerSha256',
        'RouteScaffoldSha256', 'CandidateCompilePlanSha256', 'TargetSelectorSha256'))
    {
        if ([string]$Intent.$name -cnotmatch '^[0-9a-f]{64}$') { throw "$Context $name is invalid." }
    }
    [void](Get-D1E1VerifiedBase64Bytes ([string]$Intent.TargetSelectorBytesBase64) `
        ([string]$Intent.TargetSelectorSha256) $false "$Context target")
    if ([string]$Intent.PreviousSelectorState -ceq 'Missing')
    {
        [void](Get-D1E1VerifiedBase64Bytes ([string]$Intent.PreviousSelectorBytesBase64) `
            ([string]$Intent.PreviousSelectorSha256) $true "$Context previous")
    }
    elseif ([string]$Intent.PreviousSelectorState -ceq 'Present')
    {
        [void](Get-D1E1VerifiedBase64Bytes ([string]$Intent.PreviousSelectorBytesBase64) `
            ([string]$Intent.PreviousSelectorSha256) $false "$Context previous")
    }
    else { throw "$Context previous state is invalid." }
    $fields = @($names[0..22] | ForEach-Object { [string]$Intent.$_ })
    if ([string]$Intent.IntentSha256 -cne (Get-D1E1CanonicalHash -Fields $fields))
    {
        throw "$Context self hash mismatch."
    }
    if ($state -ceq 'Committed')
    {
        if ([string]$Intent.ReceiptExclusiveClaimId -cne [string]$Intent.ExclusiveClaimId -or
            [string]$Intent.ReceiptPromotionId -cne [string]$Intent.PromotionId -or
            [string]$Intent.ReceiptTargetSelectorSha256 -cne [string]$Intent.TargetSelectorSha256)
        {
            throw "$Context committed receipt is invalid."
        }
    }
    elseif (-not [string]::IsNullOrEmpty([string]$Intent.ReceiptExclusiveClaimId) -or
        -not [string]::IsNullOrEmpty([string]$Intent.ReceiptPromotionId) -or
        -not [string]::IsNullOrEmpty([string]$Intent.ReceiptTargetSelectorSha256))
    {
        throw "$Context non-committed state carries receipt."
    }
    return $Intent
}

# 独立验证 DerivedAudit 的零选择权字段与 AuditSha256 自哈希。
function Get-D1E1VerifiedAudit
{
    param(
        [Parameter(Mandatory = $true)]$Audit,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $names = @(
        'Version', 'UnityConsumerAuthority', 'GenerationId', 'PromotionId', 'ExclusiveClaimId',
        'SelectorSha256', 'DescriptorSha256', 'InstallEnvelopeSha256',
        'RequiredArtifactSetContractHash', 'ArtifactManifestHash', 'SourceArtifactInventoryHash',
        'AnalyzerSha256', 'RouteScaffoldSha256', 'CandidateCompilePlanSha256', 'AuditSha256')
    Assert-D1E1ExactProperties $Audit $names $Context
    foreach ($name in $names) { [void](Get-D1E1RequiredProperty $Audit $name $Context) }
    if ([int]$Audit.UnityConsumerAuthority -ne 0) { throw "$Context has Unity selection authority." }
    foreach ($name in @(
        'SelectorSha256', 'DescriptorSha256', 'InstallEnvelopeSha256',
        'RequiredArtifactSetContractHash', 'ArtifactManifestHash', 'SourceArtifactInventoryHash',
        'AnalyzerSha256', 'RouteScaffoldSha256', 'CandidateCompilePlanSha256'))
    {
        if ([string]$Audit.$name -cnotmatch '^[0-9a-f]{64}$') { throw "$Context $name is invalid." }
    }
    $fields = @('EX-GAS-ActiveGenerationAudit-v2') + @($names[0..13] | ForEach-Object { [string]$Audit.$_ })
    if ([string]$Audit.AuditSha256 -cne (Get-D1E1CanonicalHash -Fields $fields))
    {
        throw "$Context self hash mismatch."
    }
    return $Audit
}

# 验证 response 中一个 transaction phase 的 raw files，并独立解析 intent/audit。
function Get-D1E1VerifiedTransactionSnapshot
{
    param(
        [Parameter(Mandatory = $true)]$Snapshot,
        [Parameter(Mandatory = $true)][string]$ExpectedPhase,
        [Parameter(Mandatory = $true)][int]$ExpectedRecoveryIndex,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $names = @('Phase', 'RecoveryIndex', 'Selector', 'Intent', 'Audit', 'ObservedState', 'PromotionId')
    Assert-D1E1ExactProperties $Snapshot $names $Context
    foreach ($name in $names)
    {
        [void](Get-D1E1RequiredProperty $Snapshot $name $Context)
    }
    if ([string]$Snapshot.Phase -cne $ExpectedPhase -or
        [int]$Snapshot.RecoveryIndex -ne $ExpectedRecoveryIndex)
    {
        throw "$Context phase/index mismatch."
    }
    [void](Get-D1E1VerifiedRawSnapshot $Snapshot.Selector "$Context selector")
    $intentJson = Get-D1E1VerifiedRawJson $Snapshot.Intent "$Context intent"
    $auditJson = Get-D1E1VerifiedRawJson $Snapshot.Audit "$Context audit"
    $intent = if ($null -eq $intentJson) { $null } else {
        Get-D1E1VerifiedIntent $intentJson "$Context intent"
    }
    $audit = if ($null -eq $auditJson) { $null } else {
        Get-D1E1VerifiedAudit $auditJson "$Context audit"
    }
    if ($null -ne $intent -and [string]$Snapshot.ObservedState -cne [string]$intent.CommitAttemptState)
    {
        throw "$Context derived intent state mismatch."
    }
    if ($null -ne $audit -and $null -eq $intent -and
        [string]$Snapshot.ObservedState -cne 'Committed')
    {
        throw "$Context derived audit state mismatch."
    }
    $promotion = if ($null -ne $intent) { [string]$intent.PromotionId }
        elseif ($null -ne $audit) { [string]$audit.PromotionId } else { '' }
    if ([string]$Snapshot.PromotionId -cne $promotion) { throw "$Context PromotionId mismatch." }
    return [pscustomobject]@{ Raw = $Snapshot; Intent = $intent; Audit = $audit }
}

# 将 matrix 进程外部读取的 file snapshot 转为 response raw wire 形状。
function ConvertTo-D1E1ObservedRawSnapshot
{
    param([Parameter(Mandatory = $true)]$Observed)

    $isPresent = if ($null -ne $Observed.PSObject.Properties['State']) {
        [string]$Observed.State -ceq 'Present'
    } else { [bool]$Observed.Exists }
    if (-not $isPresent)
    {
        return [pscustomobject]@{ State = 'Missing'; Length = 0; Sha256 = ''; BytesBase64 = '' }
    }
    return [pscustomobject]@{
        State = 'Present'; Length = [long]$Observed.Length
        Sha256 = [string]$Observed.Sha256; BytesBase64 = [string]$Observed.BytesBase64
    }
}

# 要求 response final 三文件等于 adapter 退出后由 matrix 独立读取的实际磁盘 bytes。
function Test-D1E1FinalSnapshotBinding
{
    param(
        [Parameter(Mandatory = $true)]$Final,
        [Parameter(Mandatory = $true)]$SelectorAfter,
        [Parameter(Mandatory = $true)]$IntentAfter,
        [Parameter(Mandatory = $true)]$AuditAfter
    )

    return (Test-D1E1RawSnapshotEqual $Final.Selector (ConvertTo-D1E1ObservedRawSnapshot $SelectorAfter)) -and
        (Test-D1E1RawSnapshotEqual $Final.Intent (ConvertTo-D1E1ObservedRawSnapshot $IntentAfter)) -and
        (Test-D1E1RawSnapshotEqual $Final.Audit (ConvertTo-D1E1ObservedRawSnapshot $AuditAfter))
}

# 验证 intent previous/target 与 case 输入完全对应，Corrupt 场景仅要求 fail-closed 自洽。
function Test-D1E1IntentSelectorBinding
{
    param(
        [Parameter(Mandatory = $true)]$Intent,
        [Parameter(Mandatory = $true)]$Spec,
        [Parameter(Mandatory = $true)]$Known
    )

    $target = if ([string]$Spec.Target -ceq 'A') { $Known.A } else { $Known.B }
    if ([string]$Intent.TargetSelectorSha256 -cne [string]$target.Sha256 -or
        [string]$Intent.TargetSelectorBytesBase64 -cne [string]$target.BytesBase64)
    {
        return $false
    }
    if ([string]$Spec.InitialSelector -ceq 'Corrupt') { return $true }
    if ([string]$Spec.InitialSelector -ceq 'Missing')
    {
        return [string]$Intent.PreviousSelectorState -ceq 'Missing' -and
            [string]::IsNullOrEmpty([string]$Intent.PreviousSelectorSha256) -and
            [string]::IsNullOrEmpty([string]$Intent.PreviousSelectorBytesBase64)
    }
    $previous = if ([string]$Spec.InitialSelector -ceq 'A') { $Known.A } else { $Known.B }
    return [string]$Intent.PreviousSelectorState -ceq 'Present' -and
        [string]$Intent.PreviousSelectorSha256 -ceq [string]$previous.Sha256 -and
        [string]$Intent.PreviousSelectorBytesBase64 -ceq [string]$previous.BytesBase64
}

# 验证 committed audit 与 intent 的 identity/provenance 全字段相等且绑定最终 selector。
function Test-D1E1AuditIntentBinding
{
    param(
        [Parameter(Mandatory = $true)]$Audit,
        [Parameter(Mandatory = $true)]$Intent
    )

    if ([string]$Intent.CommitAttemptState -cne 'Committed' -or
        [string]$Audit.GenerationId -cne [string]$Intent.GenerationId -or
        [string]$Audit.PromotionId -cne [string]$Intent.PromotionId -or
        [string]$Audit.ExclusiveClaimId -cne [string]$Intent.ExclusiveClaimId -or
        [string]$Audit.SelectorSha256 -cne [string]$Intent.TargetSelectorSha256)
    {
        return $false
    }
    foreach ($name in @(
        'DescriptorSha256', 'InstallEnvelopeSha256', 'RequiredArtifactSetContractHash',
        'ArtifactManifestHash', 'SourceArtifactInventoryHash', 'AnalyzerSha256',
        'RouteScaffoldSha256', 'CandidateCompilePlanSha256'))
    {
        if ([string]$Audit.$name -cne [string]$Intent.$name) { return $false }
    }
    return $true
}

# 仅以外部磁盘 active/archive/audit raw bytes 判定最终六态与 promotion，不信任 response 布尔值。
function Test-D1E1DurableOutcome
{
    param(
        [Parameter(Mandatory = $true)]$Spec,
        [Parameter(Mandatory = $true)]$IntentAfter,
        [Parameter(Mandatory = $true)]$AuditAfter,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$ArchivedIntents,
        [Parameter(Mandatory = $true)]$Known
    )

    $activeExpected = [string]$Spec.ExpectedState -cin @('CompetitionFailed', 'Indeterminate')
    if ($activeExpected)
    {
        if (-not [bool]$IntentAfter.Exists -or $ArchivedIntents.Count -ne 0 -or
            -not [bool]$IntentAfter.Parseable) { return $false }
        try { $intent = Get-D1E1VerifiedIntent $IntentAfter.Parsed 'Active intent' }
        catch { return $false }
    }
    else
    {
        if ([bool]$IntentAfter.Exists -or $ArchivedIntents.Count -ne 1) { return $false }
        $intent = $ArchivedIntents[0].Parsed
    }
    if ([string]$intent.CommitAttemptState -cne [string]$Spec.ExpectedState -or
        -not (Test-D1E1IntentSelectorBinding $intent $Spec $Known)) { return $false }
    $auditExpected = [string]$Spec.ExpectedPromotion -cin @('Present', 'PresentAfterRecovery', 'Stable')
    if (-not $auditExpected) { return -not [bool]$AuditAfter.Exists }
    if (-not [bool]$AuditAfter.Exists -or -not [bool]$AuditAfter.Parseable) { return $false }
    try { $audit = Get-D1E1VerifiedAudit $AuditAfter.Parsed 'Final audit' }
    catch { return $false }
    return Test-D1E1AuditIntentBinding $audit $intent
}

# 比较两个 transaction snapshot 的三份 raw file identity。
function Test-D1E1TransactionSnapshotEqual
{
    param(
        [Parameter(Mandatory = $true)]$Left,
        [Parameter(Mandatory = $true)]$Right
    )

    return (Test-D1E1RawSnapshotEqual $Left.Selector $Right.Selector) -and
        (Test-D1E1RawSnapshotEqual $Left.Intent $Right.Intent) -and
        (Test-D1E1RawSnapshotEqual $Left.Audit $Right.Audit) -and
        [string]$Left.PromotionId -ceq [string]$Right.PromotionId
}

# 验证 before/recovery/final typed snapshots 的顺序、raw identity 与 fail-closed 稳定性。
function Test-D1E1RecoveryEvidence
{
    param(
        [Parameter(Mandatory = $true)]$Response,
        [Parameter(Mandatory = $true)]$Spec,
        [Parameter(Mandatory = $true)]$SelectorAfter,
        [Parameter(Mandatory = $true)]$IntentAfter,
        [Parameter(Mandatory = $true)]$AuditAfter
    )

    $before = Get-D1E1VerifiedTransactionSnapshot $Response.BeforeRecovery `
        'BeforeRecovery' -1 'BeforeRecovery'
    [void](Get-D1E1VerifiedRawSnapshot $Response.IntentBeforeRecovery 'IntentBeforeRecovery')
    if (-not (Test-D1E1RawSnapshotEqual $Response.IntentBeforeRecovery $Response.BeforeRecovery.Intent))
    {
        return $false
    }
    $recovery = @($Response.RecoverySnapshots)
    $exitCodes = @($Response.RecoveryExitCodes)
    if ($recovery.Count -ne [int]$Spec.RecoveryCount -or $exitCodes.Count -ne $recovery.Count)
    {
        return $false
    }
    $verified = [Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt $recovery.Count; $index++)
    {
        $verified.Add((Get-D1E1VerifiedTransactionSnapshot $recovery[$index] `
            'Recovery' ($index + 1) ('Recovery ' + ($index + 1))))
    }
    $final = Get-D1E1VerifiedTransactionSnapshot $Response.Final `
        'Final' ([int]$Spec.RecoveryCount) 'Final'
    $last = if ($verified.Count -eq 0) { $before.Raw } else { $verified[$verified.Count - 1].Raw }
    if (-not (Test-D1E1TransactionSnapshotEqual $Response.Final $last) -or
        -not (Test-D1E1FinalSnapshotBinding $Response.Final $SelectorAfter $IntentAfter $AuditAfter))
    {
        return $false
    }
    $failClosed = [string]$Spec.ExpectedState -cin @('CompetitionFailed', 'Indeterminate')
    if (@($exitCodes | Where-Object { $failClosed -xor ([int]$_ -ne 0) }).Count -ne 0)
    {
        return $false
    }
    if (($Spec.RecoveryCount -gt 0 -or $failClosed) -and
        ($null -eq $before.Intent -or
            [string]$before.Intent.CommitAttemptState -cne [string]$Spec.ExpectedState))
    {
        return $false
    }
    if ($failClosed -and @($verified | Where-Object {
        $null -eq $_.Intent -or [string]$_.Intent.CommitAttemptState -cne [string]$Spec.ExpectedState -or
        -not (Test-D1E1TransactionSnapshotEqual $_.Raw $before.Raw)
    }).Count -ne 0) { return $false }
    if ([string]$Spec.ExpectedPromotion -ceq 'Stable' -and $verified.Count -gt 1 -and
        @($verified | Select-Object -Skip 1 | Where-Object {
            -not (Test-D1E1TransactionSnapshotEqual $_.Raw $verified[0].Raw)
        }).Count -ne 0) { return $false }
    return $true
}

# 验证 Store production claim writer trace；仅 contract 明确要求时作为正门。
function Test-D1E1ProductionClaimWrite
{
    param(
        [Parameter(Mandatory = $true)]$Response,
        [Parameter(Mandatory = $true)]$Spec
    )

    $names = @('Observed', 'FileMode', 'FileOptions', 'FlushToDisk', 'BytesVerified')
    Assert-D1E1ExactProperties $Response.ProductionClaimWrite $names 'ProductionClaimWrite'
    foreach ($name in $names)
    {
        [void](Get-D1E1RequiredProperty $Response.ProductionClaimWrite $name 'ProductionClaimWrite')
    }
    if (-not ($Spec.PSObject.Properties['RequireCreateNewFlush'] -and
        [bool]$Spec.RequireCreateNewFlush)) { return $true }
    return [bool]$Response.ProductionClaimWrite.Observed -and
        [string]$Response.ProductionClaimWrite.FileMode -ceq 'CreateNew' -and
        [string]$Response.ProductionClaimWrite.FileOptions -match 'WriteThrough' -and
        [bool]$Response.ProductionClaimWrite.FlushToDisk -and
        [bool]$Response.ProductionClaimWrite.BytesVerified
}

# 按 contract 独立判断最终 selector bytes，而不是信任 adapter 自报状态。
function Test-D1E1ExpectedSelector
{
    param(
        [Parameter(Mandatory = $true)][string]$Expected,
        [Parameter(Mandatory = $true)]$Actual,
        [Parameter(Mandatory = $true)]$Known
    )

    if ($Expected -ceq 'Missing') { return [string]$Actual.State -ceq 'Missing' }
    if ([string]$Actual.State -cne 'Present') { return $false }
    switch ($Expected)
    {
        'A' { return [string]$Actual.Sha256 -ceq [string]$Known.A.Sha256 }
        'B' { return [string]$Actual.Sha256 -ceq [string]$Known.B.Sha256 }
        'Corrupt' { return [string]$Actual.Sha256 -ceq [string]$Known.Corrupt.Sha256 }
        'Competition' {
            return [string]$Actual.Sha256 -ceq [string]$Known.Competition.Sha256
        }
        default { throw "Unknown ExpectedSelectorAfter: $Expected" }
    }
}

# 验证 adapter response raw evidence；Passed/ObservedState 等自报字段不作为正证据。
function Test-D1E1TransactionResponse
{
    param(
        [Parameter(Mandatory = $true)]$Response,
        [Parameter(Mandatory = $true)]$Spec,
        [Parameter(Mandatory = $true)][string]$ExpectedRunId,
        [Parameter(Mandatory = $true)][string]$ExpectedInvocationId,
        [Parameter(Mandatory = $true)]$ActualProcessExit,
        [Parameter(Mandatory = $true)]$SelectorAfter,
        [Parameter(Mandatory = $true)]$IntentAfter,
        [Parameter(Mandatory = $true)]$AuditAfter,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$ArchivedIntents,
        [Parameter(Mandatory = $true)]$Known
    )

    $names = @(
        'Schema', 'RunId', 'CaseId', 'InvocationId', 'Passed', 'ObservedState',
        'AdapterExitCode', 'FaultInvocationExitCode', 'RecoveryExitCodes', 'PromotionClaimed',
        'IntentFullPreviousSnapshot', 'IntentCreatedWithCreateNewAndFlush',
        'RecoveryStable', 'Detail', 'TargetSelectorSha256', 'TargetSelectorBytesBase64',
        'IntentBeforeRecovery', 'BeforeRecovery', 'RecoverySnapshots', 'Final',
        'ProductionClaimWrite')
    Assert-D1E1ExactProperties $Response $names 'Transaction response'
    foreach ($name in $names)
    {
        [void](Get-D1E1RequiredProperty $Response $name 'Transaction response')
    }
    if ([string]$Response.Schema -cne 'EX-GAS-D1-E1-TransactionResponse-v1' -or
        [string]$Response.RunId -cne $ExpectedRunId -or
        [string]$Response.CaseId -cne [string]$Spec.CaseId -or
        [string]$Response.InvocationId -cne $ExpectedInvocationId -or
        [int]$Response.AdapterExitCode -ne [int]$ActualProcessExit -or
        [string]$Response.ObservedState -cne [string]$Spec.ExpectedState)
    {
        return $false
    }
    if ($Spec.PSObject.Properties['ExpectedFaultExit'] -and
        [string]$Spec.ExpectedFaultExit -ceq 'NonZero' -and
        ([int]$Response.FaultInvocationExitCode -eq 0)) { return $false }
    $target = if ([string]$Spec.Target -ceq 'A') { $Known.A } else { $Known.B }
    try
    {
        [void](Get-D1E1VerifiedBase64Bytes ([string]$Response.TargetSelectorBytesBase64) `
            ([string]$Response.TargetSelectorSha256) $false 'Response target')
        if ([string]$Response.TargetSelectorSha256 -cne [string]$target.Sha256 -or
            [string]$Response.TargetSelectorBytesBase64 -cne [string]$target.BytesBase64)
        {
            return $false
        }
        if (-not (Test-D1E1RecoveryEvidence $Response $Spec $SelectorAfter $IntentAfter $AuditAfter) -or
            -not (Test-D1E1ProductionClaimWrite $Response $Spec) -or
            -not (Test-D1E1DurableOutcome $Spec $IntentAfter $AuditAfter $ArchivedIntents $Known))
        {
            return $false
        }
    }
    catch { return $false }
    return $true
}

# 仅从 contract spec 构造显式 request；CaseId 只作为 identity，不参与场景选择。
function New-D1E1TransactionRequest
{
    param(
        [Parameter(Mandatory = $true)]$Spec,
        [Parameter(Mandatory = $true)]$Context,
        [Parameter(Mandatory = $true)][string]$Fixture,
        [Parameter(Mandatory = $true)][string]$InvocationId
    )

    return [pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-TransactionRequest-v1'
        RunId = $Context.RunId; CaseId = [string]$Spec.CaseId; InvocationId = $InvocationId
        Scenario = [string]$Spec.Scenario; ProjectRoot = $Fixture.Replace('\', '/')
        SelectorRelativePath = [string]$Context.Contract.SelectorRelativePath
        IntentRelativePath = [string]$Context.Contract.IntentRelativePath
        AuditRelativePath = [string]$Context.Contract.AuditRelativePath
        InitialSelector = [string]$Spec.InitialSelector; Target = [string]$Spec.Target
        SelectorABytesBase64 = [Convert]::ToBase64String($Context.SelectorABytes)
        SelectorBBytesBase64 = [Convert]::ToBase64String($Context.SelectorBBytes)
        CompetitionSelectorBytesBase64 = [Convert]::ToBase64String($Context.CompetitionBytes)
        FaultPoint = $(if ($Spec.PSObject.Properties['FaultPoint']) { [string]$Spec.FaultPoint } else { $null })
        SeedState = $(if ($Spec.PSObject.Properties['SeedState']) { [string]$Spec.SeedState } else { $null })
        CompetitionTarget = $(if ($Spec.PSObject.Properties['CompetitionTarget']) { [string]$Spec.CompetitionTarget } else { $null })
        RecoveryCount = [int]$Spec.RecoveryCount
    }
}

# 执行一个 fresh transaction case 并同时保存 adapter 与独立磁盘观察。
function Invoke-D1E1TransactionCase
{
    param(
        [Parameter(Mandatory = $true)]$Spec,
        [Parameter(Mandatory = $true)]$Context
    )

    $caseId = [string]$Spec.CaseId
    $invocationId = $caseId + '-' + [Guid]::NewGuid().ToString('N')
    $fixture = Join-Path $Context.OwnedFixtureRoot ('transaction-' + $caseId)
    $evidence = Join-Path $Context.EvidenceRoot $caseId
    Copy-D1E1FixtureTemplate $Context.FixtureTemplateRoot $fixture
    [IO.Directory]::CreateDirectory($evidence) | Out-Null
    $analyzerTarget = Join-Path $fixture $Context.Contract.AnalyzerRelativePath
    Copy-Item -LiteralPath $Context.AnalyzerPath -Destination $analyzerTarget
    $routeBefore = Get-D1RouteScaffoldSnapshot $fixture $Context.ScaffoldContractPath
    $analyzerBefore = Get-D1E1SelectorSnapshot $analyzerTarget
    $selectorPath = Join-Path $fixture $Context.Contract.SelectorRelativePath
    Set-D1E1InitialSelector $selectorPath ([string]$Spec.InitialSelector) `
        $Context.SelectorABytes $Context.SelectorBBytes $Context.CorruptBytes
    $before = Get-D1E1SelectorSnapshot $selectorPath
    $requestPath = Join-Path $evidence 'request.json'
    $responsePath = Join-Path $evidence 'response.json'
    $request = New-D1E1TransactionRequest $Spec $Context $fixture $invocationId
    Write-D1E1FreshJson $requestPath $request
    $arguments = @(
        '--mode', 'd1b-self-test', '--projectRoot', $fixture,
        '--request', $requestPath, '--output', $responsePath)
    $process = Invoke-D1E1AdapterProcess $Context.AdapterCommand $arguments `
        ($Context.TimeoutSeconds * 1000)
    Write-D1E1FreshJson (Join-Path $evidence 'process.json') $process
    $after = Get-D1E1SelectorSnapshot $selectorPath
    $routeAfter = Get-D1RouteScaffoldSnapshot $fixture $Context.ScaffoldContractPath
    $analyzerAfter = Get-D1E1SelectorSnapshot $analyzerTarget
    $intent = Get-D1E1OptionalJsonFile (Join-Path $fixture $Context.Contract.IntentRelativePath)
    $audit = Get-D1E1OptionalJsonFile (Join-Path $fixture $Context.Contract.AuditRelativePath)
    $archives = @(Get-D1E1ArchivedIntents $fixture $Context.Contract.ArchiveRelativePath)
    $response = $null
    $responseValid = $false
    if ([IO.File]::Exists($responsePath))
    {
        try
        {
            $response = Get-Content -LiteralPath $responsePath -Raw -Encoding UTF8 |
                ConvertFrom-Json
            $responseValid = Test-D1E1TransactionResponse $response $Spec `
                $Context.RunId $invocationId $process.ExitCode $after $intent $audit `
                $archives $Context.KnownSelectors
        }
        catch { $responseValid = $false }
    }
    $exitExpected = if ([string]$Spec.ExpectedAdapterExit -ceq 'NonZero') {
        $null -ne $process.ExitCode -and [int]$process.ExitCode -ne 0
    } else { [int]$process.ExitCode -eq [int]$Spec.ExpectedAdapterExit }
    $selectorExpected = Test-D1E1ExpectedSelector ([string]$Spec.ExpectedSelectorAfter) `
        $after $Context.KnownSelectors
    $toolInvariant = [string]$routeBefore.RouteScaffoldSha256 -ceq [string]$routeAfter.RouteScaffoldSha256 -and
        [string]$analyzerBefore.Sha256 -ceq [string]$analyzerAfter.Sha256 -and
        [string]$analyzerBefore.BytesBase64 -ceq [string]$analyzerAfter.BytesBase64
    $passed = -not $process.TimedOut -and $exitExpected -and $responseValid -and
        $selectorExpected -and $toolInvariant
    $summary = [pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-TransactionCase-v1'; RunId = $Context.RunId
        CaseId = $caseId; Name = [string]$Spec.Name; InvocationId = $invocationId
        Passed = $passed; Process = $process; SelectorBefore = $before; SelectorAfter = $after
        RouteBefore = $routeBefore; RouteAfter = $routeAfter
        AnalyzerBefore = $analyzerBefore; AnalyzerAfter = $analyzerAfter
        ToolInvariant = $toolInvariant
        Intent = $intent; Audit = $audit; ArchivedIntents = $archives
        ResponseValid = $responseValid
        ExpectedState = [string]$Spec.ExpectedState
        Detail = $(if ($passed) { 'Transaction case satisfied all independent invariants.' } else { 'Transaction adapter or independent disk invariant failed.' })
    }
    Write-D1E1FreshJson (Join-Path $evidence 'summary.json') $summary
    return $summary
}

$contract = Get-Content -LiteralPath $ContractPath -Raw -Encoding UTF8 | ConvertFrom-Json
$scaffoldContract = Get-Content -LiteralPath $ScaffoldContractPath -Raw -Encoding UTF8 | ConvertFrom-Json
$resolvedAnalyzerPath = [IO.Path]::GetFullPath($AnalyzerPath)
$resolvedAdapterPath = [IO.Path]::GetFullPath($TransactionAdapterPath)
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../..'))
$expectedAdapterPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot `
    ([string]$contract.TransactionAdapterRelativePath)))
if ($resolvedAdapterPath -cne $expectedAdapterPath -or
    [Reflection.AssemblyName]::GetAssemblyName($resolvedAdapterPath).Name -cne
        [string]$contract.TransactionAdapterAssemblyName)
{
    throw 'Transaction adapter must be the exact production GasCodeGenCli build output.'
}
$resolvedSelectorAPath = [IO.Path]::GetFullPath($SelectorAPath)
$resolvedSelectorBPath = [IO.Path]::GetFullPath($SelectorBPath)
$selectorABytes = [IO.File]::ReadAllBytes([IO.Path]::GetFullPath($SelectorAPath))
$selectorBBytes = [IO.File]::ReadAllBytes([IO.Path]::GetFullPath($SelectorBPath))
if ([Convert]::ToBase64String($selectorABytes) -ceq [Convert]::ToBase64String($selectorBBytes))
{
    throw 'Selector A and B must have different bytes.'
}
$selectorAIdentity = Get-D1E1ProductionSelectorIdentity $resolvedAnalyzerPath $resolvedSelectorAPath
$selectorBIdentity = Get-D1E1ProductionSelectorIdentity $resolvedAnalyzerPath $resolvedSelectorBPath
$analyzerSha256 = (Get-FileHash -LiteralPath $resolvedAnalyzerPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ([string]$selectorAIdentity.AnalyzerSha256 -cne $analyzerSha256 -or
    [string]$selectorBIdentity.AnalyzerSha256 -cne $analyzerSha256 -or
    [string]$selectorAIdentity.RouteScaffoldSha256 -cne [string]$scaffoldContract.StagedRouteScaffoldSha256 -or
    [string]$selectorBIdentity.RouteScaffoldSha256 -cne [string]$scaffoldContract.StagedRouteScaffoldSha256)
{
    throw 'Selector inputs are not bound to the exact analyzer/staged route scaffold.'
}
$magic = [Text.Encoding]::UTF8.GetBytes("EX-GAS-SourceSelector-v1`n")
foreach ($selector in @($selectorABytes, $selectorBBytes))
{
    if ($selector.Length -le $magic.Length -or
        -not (Test-D1E1BytePrefix $selector $magic))
    {
        throw 'Selector input does not use EX-GAS-SourceSelector-v1 wire format.'
    }
}

[IO.Directory]::CreateDirectory($OwnedFixtureRoot) | Out-Null
[IO.Directory]::CreateDirectory($EvidenceRoot) | Out-Null
$corruptBytes = [Text.UTF8Encoding]::new($false).GetBytes("not-a-canonical-selector`n")
$competitionBytes = [Text.UTF8Encoding]::new($false).GetBytes("competing-selector-bytes`n")
$known = [pscustomobject]@{
    A = Get-D1E1SelectorSnapshot ([IO.Path]::GetFullPath($SelectorAPath))
    B = Get-D1E1SelectorSnapshot ([IO.Path]::GetFullPath($SelectorBPath))
    Corrupt = [pscustomobject]@{ Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($corruptBytes)).ToLowerInvariant() }
    Competition = [pscustomobject]@{ Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($competitionBytes)).ToLowerInvariant() }
}
$context = [pscustomobject]@{
    RunId = $RunId; Contract = $contract
    ScaffoldContractPath = [IO.Path]::GetFullPath($ScaffoldContractPath)
    FixtureTemplateRoot = [IO.Path]::GetFullPath($FixtureTemplateRoot)
    OwnedFixtureRoot = [IO.Path]::GetFullPath($OwnedFixtureRoot)
    EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
    AnalyzerPath = $resolvedAnalyzerPath
    SelectorABytes = $selectorABytes; SelectorBBytes = $selectorBBytes
    CorruptBytes = $corruptBytes; CompetitionBytes = $competitionBytes
    KnownSelectors = $known; TimeoutSeconds = $TimeoutSeconds
    AdapterCommand = Get-D1E1AdapterCommand $TransactionAdapterPath
}
$results = [Collections.Generic.List[object]]::new()
foreach ($spec in @($contract.TransactionCases))
{
    $results.Add((Invoke-D1E1TransactionCase $spec $context))
}
$matrix = [pscustomobject][ordered]@{
    Schema = 'EX-GAS-D1-E1-TransactionMatrix-v1'; RunId = $RunId
    Inputs = [pscustomobject][ordered]@{
        AnalyzerSha256 = $analyzerSha256; SelectorA = $selectorAIdentity
        SelectorB = $selectorBIdentity
        StagedRouteScaffoldSha256 = [string]$scaffoldContract.StagedRouteScaffoldSha256
        TransactionAdapterSha256 = (Get-FileHash -LiteralPath $resolvedAdapterPath `
            -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    Passed = @($results | Where-Object { -not $_.Passed }).Count -eq 0
    CaseCount = $results.Count; Cases = @($results)
}
Write-D1E1FreshJson $OutputPath $matrix
if (-not $matrix.Passed) { exit 41 }
