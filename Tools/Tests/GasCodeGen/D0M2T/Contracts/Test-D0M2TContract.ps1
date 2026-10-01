[CmdletBinding(DefaultParameterSetName = "Evidence")]
param(
    [Parameter(Mandatory = $true, ParameterSetName = "Evidence")][string]$Path,
    [Parameter(Mandatory = $true, ParameterSetName = "Evidence")][string]$EvidenceRoot,
    [Parameter(Mandatory = $true, ParameterSetName = "Evidence")]
    [ValidatePattern("^D0M2T-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$")][string]$ExpectedRunId,
    [Parameter(Mandatory = $true, ParameterSetName = "Static")][switch]$StaticOnly,
    [Parameter(ParameterSetName = "Static")][switch]$HistoricalReplay
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$contractPath = Join-Path $PSScriptRoot "D0M2T.contract.json"
$schemaPath = Join-Path $PSScriptRoot "D0M2T.aggregate.schema.json"
$oraclePath = Join-Path $PSScriptRoot "D0M2TProtectedPathOracle.ps1"
$syntheticPath = Join-Path $PSScriptRoot "Test-D0M2TContractSynthetic.ps1"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../../../.."))
$archiveIndexPath = Join-Path $repositoryRoot "Tools/Tests/GasCodeGen/D0M2F/Tarball/Archives~/ArchiveIndex.json"
$tarballRunnerPath = Join-Path $repositoryRoot "Tools/Tests/GasCodeGen/D0M2T/Tarball/Run-D0M2T-TarballFaultExperiment.ps1"
$script:ValidatedArchiveIndex = $null

# 读取 JSON object，并将缺失、语法或顶层类型错误转为显式失败。
function Read-D0M2TJsonObject
{
    param([Parameter(Mandatory = $true)][string]$FilePath)

    if (-not [IO.File]::Exists($FilePath)) { throw "JSON file is missing: $FilePath" }
    $value = [IO.File]::ReadAllText($FilePath, [Text.Encoding]::UTF8) | ConvertFrom-Json -Depth 100
    if ($null -eq $value -or $value -isnot [pscustomobject]) { throw "JSON root must be an object: $FilePath" }
    return $value
}

# 读取大小写精确的必需属性，避免宽松属性访问掩盖拼写漂移。
function Get-D0M2TRequiredProperty
{
    param(
        [Parameter(Mandatory = $true)]$Object,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $matches = @($Object.PSObject.Properties | Where-Object { $_.Name -ceq $Name })
    if ($matches.Count -ne 1) { throw "$Context must contain exactly one '$Name' property." }
    return ,$matches[0].Value
}

# 断言 JSON 值是非 null object。
function Assert-D0M2TJsonObject
{
    param($Value, [Parameter(Mandatory = $true)][string]$Context)

    if ($null -eq $Value -or $Value -isnot [pscustomobject]) { throw "$Context must be a JSON object." }
}

# 断言 JSON 值保留数组形状。
function Assert-D0M2TJsonArray
{
    param($Value, [Parameter(Mandatory = $true)][string]$Context)

    if ($null -eq $Value -or $Value -isnot [Array]) { throw "$Context must be a JSON array." }
}

# 断言两个字符串集合大小写精确、无重复且完全相等。
function Assert-D0M2TExactSet
{
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Actual,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Expected,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $actualSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($item in $Actual)
    {
        if (-not $actualSet.Add($item)) { throw "$Context contains duplicate value '$item'." }
    }
    $expectedSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($item in $Expected) { [void]$expectedSet.Add($item) }
    if (-not $actualSet.SetEquals($expectedSet))
    {
        throw "$Context mismatch. Expected=[$([string]::Join(',', $Expected))] Actual=[$([string]::Join(',', $Actual))]"
    }
}

# 断言两个字符串序列大小写精确且顺序一致。
function Assert-D0M2TExactSequence
{
    param([string[]]$Actual, [string[]]$Expected, [Parameter(Mandatory = $true)][string]$Context)

    if ($Actual.Count -ne $Expected.Count) { throw "$Context count mismatch." }
    for ($index = 0; $index -lt $Expected.Count; $index++)
    {
        if ($Actual[$index] -cne $Expected[$index]) { throw "$Context order/value mismatch at index $index." }
    }
}

# 计算文件原始 bytes 的 SHA-256。
function Get-D0M2TFileSha256
{
    param([Parameter(Mandatory = $true)][string]$FilePath)

    return (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256).Hash.ToLowerInvariant()
}

# 计算内存 raw bytes 的 SHA-256，供已删除 fixture 的现场快照复核。
function Get-D0M2TBytesSha256
{
    param([Parameter(Mandatory = $true)][byte[]]$Bytes)

    return ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes))).ToLowerInvariant()
}

# 构造 runner 使用的稳定 LF 结尾 manifest 文本。
function Get-D0M2TManifestText
{
    param([Parameter(Mandatory = $true)][string]$FileName)

    return "{`n  `"dependencies`": {`n    `"com.exhard.exgas.d0m2f-tarball`": `"file:../../Authority/$FileName`"`n  }`n}`n"
}

# 计算 runner 构造的稳定 UTF-8 manifest bytes SHA-256。
function Get-D0M2TManifestSha256
{
    param([Parameter(Mandatory = $true)][string]$FileName)

    return Get-D0M2TBytesSha256 (([Text.UTF8Encoding]::new($false)).GetBytes((Get-D0M2TManifestText $FileName)))
}

# 返回 selector worker 的唯一 source 模板，供独立复算 harness bytes。
function Get-D0M2TSelectorWorkerText
{
    return @'
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
}

# 打开 checked-in archive index，并复算 index 与两份 tgz 的现场 raw identity。
function Get-D0M2TLiveArchiveIndex
{
    param([Parameter(Mandatory = $true)]$Contract)

    if (-not [IO.File]::Exists($archiveIndexPath)) { throw "InputIdentityDrift: ArchiveIndex.json is missing." }
    $indexSha = Get-D0M2TFileSha256 -FilePath $archiveIndexPath
    if ($indexSha -cne [string]$Contract.ArchiveIndexSha256) { throw "InputIdentityDrift: ArchiveIndex.json raw SHA drift." }
    $index = Read-D0M2TJsonObject -FilePath $archiveIndexPath
    Assert-D0M2TEvidenceCondition -Condition ([string]$index.Schema -ceq "D0M2F-TarballArchiveIndex-v1" -and [string]$index.PackageName -ceq "com.exhard.exgas.d0m2f-tarball") -Message "archive index identity drift."
    Assert-D0M2TJsonArray -Value $index.Archives -Context "ArchiveIndex.Archives"
    $archives = @($index.Archives)
    Assert-D0M2TExactSet -Actual @($archives | ForEach-Object { [string]$_.GenerationId }) -Expected @("A", "B") -Context "ArchiveIndex generations"
    foreach ($archive in $archives)
    {
        foreach ($field in @("GenerationToken", "PackageVersion", "FileName", "Sha256", "Length")) { [void](Get-D0M2TRequiredProperty $archive $field "ArchiveIndex archive") }
        $archivePath = Join-Path ([IO.Path]::GetDirectoryName($archiveIndexPath)) ([string]$archive.FileName)
        Assert-D0M2TEvidenceCondition -Condition ([string]$archive.FileName -ceq "$([string]$archive.Sha256).tgz" -and [IO.File]::Exists($archivePath)) -Message "archive index filename/file drift."
        Assert-D0M2TEvidenceCondition -Condition ((Get-D0M2TFileSha256 $archivePath) -ceq [string]$archive.Sha256 -and [long](Get-Item -LiteralPath $archivePath).Length -eq [long]$archive.Length) -Message "checked-in archive bytes drift."
    }
    return [pscustomobject]@{ Index = $index; Sha256 = $indexSha }
}

# 解析 PowerShell 文件 AST，拒绝任何语法错误。
function Assert-D0M2TPowerShellAst
{
    param([Parameter(Mandatory = $true)][string]$FilePath)

    $tokens = $null
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($FilePath, [ref]$tokens, [ref]$errors)
    if (@($errors).Count -ne 0)
    {
        throw "PowerShell AST validation failed: $([string]::Join(' | ', @($errors | ForEach-Object Message)))"
    }
}

# 将 raw evidence 的安全相对路径解析到 EvidenceRoot 内，并拒绝重解析点逃逸。
function Resolve-D0M2TEvidencePath
{
    param(
        [Parameter(Mandatory = $true)][string]$RootPath,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath)) { throw "Evidence Path must not be empty." }
    if ($RelativePath.Contains("\")) { throw "Evidence Path must use '/' separators: $RelativePath" }
    if ([IO.Path]::IsPathRooted($RelativePath) -or $RelativePath -match "^[A-Za-z]:") { throw "Evidence Path must be relative: $RelativePath" }
    $segments = @($RelativePath.Split('/'))
    if ($segments.Count -eq 0 -or @($segments | Where-Object { $_ -ceq "" -or $_ -ceq "." -or $_ -ceq ".." }).Count -ne 0)
    {
        throw "Evidence Path contains an unsafe segment: $RelativePath"
    }
    $rootFullPath = [IO.Path]::GetFullPath($RootPath).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if (-not [IO.Directory]::Exists($rootFullPath)) { throw "EvidenceRoot is missing: $rootFullPath" }
    $rootAttributes = [IO.File]::GetAttributes($rootFullPath)
    if (($rootAttributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "EvidenceRoot must not be a reparse point." }
    $candidatePath = [IO.Path]::GetFullPath((Join-Path $rootFullPath ($RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))))
    $rootPrefix = $rootFullPath + [IO.Path]::DirectorySeparatorChar
    if (-not $candidatePath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Evidence Path escapes EvidenceRoot." }
    $currentPath = $rootFullPath
    foreach ($segment in $segments)
    {
        $currentPath = Join-Path $currentPath $segment
        if ([IO.File]::Exists($currentPath) -or [IO.Directory]::Exists($currentPath))
        {
            if (([IO.File]::GetAttributes($currentPath) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Evidence Path crosses a reparse point: $RelativePath" }
        }
    }
    if (-not [IO.File]::Exists($candidatePath)) { throw "Evidence file is missing: $RelativePath" }
    return $candidatePath
}

# 校验冻结输入与冻结记录的合同声明，并按门禁要求复核六个现场文件。
function Test-D0M2TFrozenInputs
{
    param(
        [Parameter(Mandatory = $true)]$Contract,
        [Parameter(Mandatory = $true)]$Inputs,
        [switch]$ValidateLiveInputs
    )

    Assert-D0M2TJsonObject -Value $Inputs -Context "Inputs"
    $actualInputs = Get-D0M2TRequiredProperty -Object $Inputs -Name "FrozenInputs" -Context "Inputs"
    Assert-D0M2TJsonArray -Value $actualInputs -Context "Inputs.FrozenInputs"
    $expectedBindings = @($Contract.FrozenInputs | ForEach-Object { "$([string]$_.Path)|$([string]$_.Sha256)" })
    $actualBindings = @($actualInputs | ForEach-Object {
        "$([string](Get-D0M2TRequiredProperty -Object $_ -Name 'Path' -Context 'Frozen input'))|$([string](Get-D0M2TRequiredProperty -Object $_ -Name 'Sha256' -Context 'Frozen input'))"
    })
    Assert-D0M2TExactSet -Actual $actualBindings -Expected $expectedBindings -Context "Aggregate frozen inputs"
    $recordSha = [string](Get-D0M2TRequiredProperty -Object $Inputs -Name "FrozenRecordSha256" -Context "Inputs")
    if ($recordSha -cne [string]$Contract.FrozenRecordSha256) { throw "Frozen record SHA mismatch." }
    if ($ValidateLiveInputs)
    {
        foreach ($input in @($Contract.FrozenInputs))
        {
            $sourcePath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot ([string]$input.Path)))
            if (-not [IO.File]::Exists($sourcePath)) { throw "InputIdentityDrift: Frozen input is missing: $($input.Path)" }
            if ((Get-D0M2TFileSha256 -FilePath $sourcePath) -cne [string]$input.Sha256) { throw "InputIdentityDrift: Frozen input raw SHA drift: $($input.Path)" }
        }
    }
    $recordPath = Join-Path $repositoryRoot "docs/reviews/RuntimeV1.1-D0-M2F-SpecADR规范冻结结果.md"
    if (-not [IO.File]::Exists($recordPath)) { throw "InputIdentityDrift: Frozen record is missing." }
    if ((Get-D0M2TFileSha256 -FilePath $recordPath) -cne [string]$Contract.FrozenRecordSha256) { throw "Frozen record raw SHA drift." }
}

# 校验合同、schema 与硬编码冻结值的静态单一事实源一致性。
function Test-D0M2TStaticContract
{
    param([switch]$ValidateLiveFrozenInputs)

    $contract = Read-D0M2TJsonObject -FilePath $contractPath
    $schema = Read-D0M2TJsonObject -FilePath $schemaPath
    if ([string]$contract.Schema -cne "D0M2T-Contract-v1") { throw "Contract schema identity mismatch." }
    if ([string]$schema.properties.Schema.const -cne [string]$contract.AggregateSchema) { throw "Aggregate schema identity drift." }
    Assert-D0M2TExactSet -Actual @($schema.required | ForEach-Object { [string]$_ }) -Expected @($contract.RequiredTopLevelFields | ForEach-Object { [string]$_ }) -Context "Required top-level fields"
    Assert-D0M2TExactSet -Actual @($schema.properties.Status.enum | ForEach-Object { [string]$_ }) -Expected @($contract.AggregateStatuses | ForEach-Object { [string]$_ }) -Context "Aggregate statuses"
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.case.properties.Status.enum | ForEach-Object { [string]$_ }) -Expected @($contract.CaseStatuses | ForEach-Object { [string]$_ }) -Context "Case statuses"
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.case.properties.Reason.enum | ForEach-Object { [string]$_ }) -Expected @($contract.CaseReasons | ForEach-Object { [string]$_ }) -Context "Case reasons"
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.evidenceFile.required | ForEach-Object { [string]$_ }) -Expected @($contract.EvidenceFileFields | ForEach-Object { [string]$_ }) -Context "Evidence fields"
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.evidenceFile.properties.Kind.enum | ForEach-Object { [string]$_ }) -Expected @($contract.EvidenceKinds | ForEach-Object { [string]$_ }) -Context "Evidence kinds"
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.roleObservation.properties.Role.enum | ForEach-Object { [string]$_ }) -Expected @($contract.Roles | ForEach-Object { [string]$_ }) -Context "Roles"
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.roleObservation.required | ForEach-Object { [string]$_ }) -Expected @($contract.PersistentRoleObservationFields | ForEach-Object { [string]$_ }) -Context "Persistent role fields"
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.transientArtifactObservation.required | ForEach-Object { [string]$_ }) -Expected @($contract.TransientArtifactObservationFields | ForEach-Object { [string]$_ }) -Context "Transient artifact fields"
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.harnessControlObservation.required | ForEach-Object { [string]$_ }) -Expected @($contract.HarnessControlObservationFields | ForEach-Object { [string]$_ }) -Context "Harness control fields"
    $harnessKinds = @($contract.HarnessControlInventory | ForEach-Object { [string]$_.Kind } | Select-Object -Unique)
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.harnessControlObservation.properties.Kind.enum | ForEach-Object { [string]$_ }) -Expected $harnessKinds -Context "Harness control kinds"
    if (@($contract.TransientArtifactMatrix).Count -ne 4 -or (@($contract.HarnessControlInventory | Measure-Object Count -Sum).Sum) -ne 23) { throw "Experiment object inventory count drift." }
    if ([int]$schema.properties.EvidenceFiles.minItems -ne 0 -or [int]$schema.properties.RoleObservations.minItems -ne 0 -or [int]$schema.properties.TransientArtifactObservations.minItems -ne 0 -or [int]$schema.properties.HarnessControlObservations.minItems -ne 0) { throw "Partial aggregate schema must allow empty evidence and role domains." }
    if ([int]$schema.allOf[0].then.properties.EvidenceFiles.minItems -ne 8 -or [int]$schema.allOf[1].then.properties.EvidenceFiles.minItems -ne 8) { throw "Conclusive aggregate evidence minima drift." }
    foreach ($index in @(0, 1))
    {
        $properties = $schema.allOf[$index].then.properties
        if ([int]$properties.RoleObservations.minItems -ne 7 -or [int]$properties.RoleObservations.maxItems -ne 7 -or [int]$properties.TransientArtifactObservations.minItems -ne 4 -or [int]$properties.TransientArtifactObservations.maxItems -ne 4 -or [int]$properties.HarnessControlObservations.minItems -ne 23 -or [int]$properties.HarnessControlObservations.maxItems -ne 23) { throw "Conclusive role-domain cardinality drift." }
    }
    if ([string]$schema.allOf[0].then.properties.Cases.items.properties.Status.const -cne "Passed" -or [string]$schema.allOf[1].then.properties.Cases.contains.properties.Status.const -cne "Failed") { throw "Conclusive aggregate case-status schema drift." }
    Assert-D0M2TExactSet -Actual @($schema.allOf[1].then.properties.Cases.items.properties.Status.enum | ForEach-Object { [string]$_ }) -Expected @("Passed", "Failed") -Context "RouteRejected case statuses"
    Assert-D0M2TExactSet -Actual @($schema.allOf[2].then.properties.Cases.contains.properties.Status.enum | ForEach-Object { [string]$_ }) -Expected @("Inconclusive", "NotRun") -Context "Inconclusive aggregate case statuses"
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.protected.required | ForEach-Object { [string]$_ }) -Expected @("Unchanged") -Context "Partial protected fields"
    if ([int]$schema.'$defs'.case.allOf[0].then.properties.EvidenceIds.minItems -ne 1 -or [int]$schema.'$defs'.case.allOf[1].then.properties.EvidenceIds.maxItems -ne 0) { throw "Status-aware case evidence schema drift." }
    $allReasons = @($contract.Reasons.Passed) + @($contract.Reasons.RouteRejected) + @($contract.Reasons.Inconclusive)
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.reason.enum | ForEach-Object { [string]$_ }) -Expected @($allReasons | ForEach-Object { [string]$_ }) -Context "Aggregate reasons"
    Assert-D0M2TExactSet -Actual @($schema.allOf[1].then.properties.Reason.enum | ForEach-Object { [string]$_ }) -Expected @($contract.Reasons.RouteRejected | ForEach-Object { [string]$_ }) -Context "RouteRejected reasons"
    Assert-D0M2TExactSet -Actual @($schema.allOf[2].then.properties.Reason.enum | ForEach-Object { [string]$_ }) -Expected @($contract.Reasons.Inconclusive | ForEach-Object { [string]$_ }) -Context "Inconclusive reasons"
    if ([string]$schema.allOf[0].then.properties.Reason.const -cne "None") { throw "Passed reason rule drift." }
    $expectedIds = @(1..8 | ForEach-Object { "TT-{0:D2}" -f $_ })
    $expectedNames = @("BaselineAuthority", "SelectorAtomicKill", "ResolveKillRecovery", "MissingCorruptAuthority", "ManifestLockDrift", "StaleCacheBeeRsp", "BoundaryCleanup", "EvidenceClosure")
    Assert-D0M2TExactSequence -Actual @($contract.CaseSet | ForEach-Object { [string]$_.CaseId }) -Expected $expectedIds -Context "Fixed case IDs"
    Assert-D0M2TExactSequence -Actual @($contract.CaseSet | ForEach-Object { [string]$_.Name }) -Expected $expectedNames -Context "Fixed case names"
    Assert-D0M2TExactSequence -Actual @($schema.properties.Cases.prefixItems | ForEach-Object { [string]$_.allOf[1].properties.CaseId.const }) -Expected $expectedIds -Context "Schema fixed case IDs"
    Assert-D0M2TExactSequence -Actual @($schema.properties.Cases.prefixItems | ForEach-Object { [string]$_.allOf[1].properties.Name.const }) -Expected $expectedNames -Context "Schema fixed case names"
    if ($schema.properties.Cases.items -ne $false -or [string]$schema.'$defs'.case.allOf[2].then.properties.Reason.const -cne "None" -or [string]$schema.'$defs'.case.allOf[5].then.properties.Reason.const -cne "NotRun") { throw "Schema case identity/reason closure drift." }
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.case.allOf[3].then.properties.Reason.enum | ForEach-Object { [string]$_ }) -Expected @($contract.Reasons.RouteRejected | ForEach-Object { [string]$_ }) -Context "Schema Failed reasons"
    Assert-D0M2TExactSet -Actual @($schema.'$defs'.case.allOf[4].then.properties.Reason.enum | ForEach-Object { [string]$_ }) -Expected @($contract.Reasons.Inconclusive | ForEach-Object { [string]$_ }) -Context "Schema Inconclusive reasons"
    $expectedCaseKinds = @{
        "TT-01" = @("AuthoritySnapshot", "AuthorityWatcherLog")
        "TT-02" = @("ManifestSnapshot", "ProcessLog")
        "TT-03" = @("UnityObservation", "ProcessLog")
        "TT-04" = @("FaultInjectionRecord", "UnityObservation")
        "TT-05" = @("ManifestSnapshot", "PackageLockSnapshot", "AuditRecordSnapshot")
        "TT-06" = @("CacheSnapshot", "BeeRspSnapshot", "UnityObservation")
        "TT-07" = @("FilesystemBoundaryRecord", "CleanupRecord", "ProtectedSnapshot")
        "TT-08" = @("EvidenceClosure")
    }
    foreach ($case in @($contract.CaseSet))
    {
        Assert-D0M2TExactSet -Actual @($case.RequiredEvidenceKinds | ForEach-Object { [string]$_ }) -Expected @($expectedCaseKinds[[string]$case.CaseId]) -Context "$($case.CaseId) evidence kinds"
    }
    $frozen = $contract.FrozenTerminalFields
    if ([string]$frozen.SelectedRoute -cne "ImmutableTarball" -or [string]$frozen.SoleUnityConsumedSelector -cne "Packages/manifest.json" -or $frozen.D1Authorized -ne $false -or [string]$frozen.ProductionInstallAdmission -cne "NotEvaluated" -or $frozen.DeclaredFullSemanticEligibility -ne $false -or [string]$frozen.NextGate -cne "D0-M2R") { throw "Frozen terminal fields drift." }
    foreach ($name in @("SelectedRoute", "SoleUnityConsumedSelector", "D1Authorized", "ProductionInstallAdmission", "DeclaredFullSemanticEligibility", "NextGate"))
    {
        if ($schema.properties.$name.const -cne $frozen.$name) { throw "Schema frozen field '$name' drift." }
    }
    Test-D0M2TStaticBindings -Contract $contract
    [void](Get-D0M2TLiveArchiveIndex -Contract $contract)
    $staticInputs = [pscustomobject]@{ FrozenInputs = @($contract.FrozenInputs); FrozenRecordSha256 = [string]$contract.FrozenRecordSha256 }
    Test-D0M2TFrozenInputs -Contract $contract -Inputs $staticInputs -ValidateLiveInputs:$ValidateLiveFrozenInputs
    Assert-D0M2TPowerShellAst -FilePath $oraclePath
    Assert-D0M2TPowerShellAst -FilePath $syntheticPath
    Assert-D0M2TPowerShellAst -FilePath $PSCommandPath
    return [pscustomobject]@{ Contract = $contract; ContractSha256 = Get-D0M2TFileSha256 -FilePath $contractPath; AggregateSchemaSha256 = Get-D0M2TFileSha256 -FilePath $schemaPath; OracleSha256 = Get-D0M2TFileSha256 -FilePath $oraclePath }
}

# 校验冻结输入、保护路径与 TT-08 闭包字段的不可漂移常量。
function Test-D0M2TStaticBindings
{
    param([Parameter(Mandatory = $true)]$Contract)

    $expectedFrozen = @(
        "方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md|184df26da7a9080dcafa9af3b50c42655e8a69f41115617337c09621b3bfc6b0",
        "docs/adr/0001-codegen-single-install-root-and-install-envelope.md|38be527ddf587c66655685153ada138efb6a1e92d0815b02378b0bb40b373f47",
        "方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/20-策划配置能力交叉审查Spec.md|e3141bdfb299f742b523a5e01166a3326aa01fed7d337a1881cecf48a89e1012",
        "方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/91-术语表.md|8f6440d32dfe8a8a84c0f5bafe54af15c04b6ddf2ad382c69a76f7ed2860a7a8",
        "docs/reviews/RuntimeV1.1-D0-M2F-选路裁决.md|a2d568f60a834ba8a18ed750948b10e1894d1c3e85a98dfd4744356105326629",
        "Tools/Tests/GasCodeGen/README.md|e074d6d6290a244ade97547b8d0fa722d24bc6da953e46d6821c34e671e01586"
    )
    Assert-D0M2TExactSet -Actual @($Contract.FrozenInputs | ForEach-Object { "$([string]$_.Path)|$([string]$_.Sha256)" }) -Expected $expectedFrozen -Context "Contract frozen inputs"
    if ([string]$Contract.FrozenRecordSha256 -cne "d9ec80b7cd0229157938c2ddd87701166a7347c2f7f700673b7f17a57560ee50") { throw "FrozenRecordSha256 drift." }
    if ([string]$Contract.ArchiveIndexSha256 -cne "696365b83636079f9099c80d9cdd5d9a81460c940c6c737782a299882c9de1cc") { throw "ArchiveIndexSha256 drift." }
    $expectedProtected = @(
        "File|Packages/manifest.json", "File|Packages/packages-lock.json", "Tree|ProjectSettings/GasCodeGen", "Tree|Assets/GAS/Editor/CodeGen", "Tree|Assets/GAS/Generated/CodeGen", "Tree|Assets/AutoChessDemo/Generated", "Tree|Tools/GasCodeGenCli", "Tree|Library/Bee", "Tree|docs/adr",
        "File|方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md", "File|方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/20-策划配置能力交叉审查Spec.md", "File|方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/91-术语表.md",
        "File|docs/reviews/RuntimeV1.1-D0-M2F-选路裁决.md", "File|docs/reviews/RuntimeV1.1-D0-M2F-SpecADR规范冻结结果.md", "File|Tools/Tests/GasCodeGen/README.md", "Tree|Tools/Tests/GasCodeGen/D0M2F", "Tree|TestResults/GasCodeGen/N2-G0-D0-M2F/D0M2F-20260830T124005Z-c661f64e4c81"
    )
    Assert-D0M2TExactSet -Actual @($Contract.ProtectedPathSpecs | ForEach-Object { "$([string]$_.Kind)|$([string]$_.Path)" }) -Expected $expectedProtected -Context "Protected path specs"
    Assert-D0M2TExactSet -Actual @($Contract.EvidenceClosureRequiredFields | ForEach-Object { [string]$_ }) -Expected @("Schema", "RunId", "ClosedAtUtc", "EvidenceRoot", "PriorEvidenceFiles", "Checks", "FixtureCleanup", "OwnedProcessIds", "LiveOwnedProcessIds", "Passed", "Reason") -Context "Evidence closure fields"
    Assert-D0M2TExactSet -Actual @($Contract.EvidenceClosureCheckFields | ForEach-Object { [string]$_ }) -Expected @("AllPathsRelative", "AllFilesOpenable", "LengthsMatch", "Sha256Match", "DuplicateEvidenceIds", "OrphanEvidenceIds") -Context "Evidence closure checks"
    Assert-D0M2TExactSet -Actual @($Contract.EvidenceClosureCleanupFields | ForEach-Object { [string]$_ }) -Expected @("Requested", "CreatedRoots", "RemovedRoots", "ResidualRoots") -Context "Evidence closure cleanup fields"
    Assert-D0M2TExactSet -Actual @($Contract.CaseEvidencePolicy.PSObject.Properties | ForEach-Object { $_.Name + "=" + [string]$_.Value }) -Expected @("Passed=RequiredKinds", "Failed=RequiredKinds", "Inconclusive=PartialAllowed", "NotRun=MustBeEmpty") -Context "Case evidence policy"
    if ([string]$Contract.PassedSemanticEvidenceAdmission -cne "TypedRawV1") { throw "Passed semantic evidence admission drift." }
}

# 校验 aggregate 固定头、状态/原因和冻结输入。
function Test-D0M2THeader
{
    param([Parameter(Mandatory = $true)]$Contract, [Parameter(Mandatory = $true)]$Aggregate, [Parameter(Mandatory = $true)][string]$RunId)

    Assert-D0M2TExactSet -Actual @($Aggregate.PSObject.Properties | ForEach-Object Name) -Expected @($Contract.RequiredTopLevelFields | ForEach-Object { [string]$_ }) -Context "Aggregate top-level fields"
    if ([string]$Aggregate.Schema -cne [string]$Contract.AggregateSchema) { throw "Aggregate Schema mismatch." }
    if ([string]$Aggregate.RunId -cne $RunId -or [string]$Aggregate.RunId -cnotmatch [string]$Contract.RunIdPattern) { throw "Aggregate RunId mismatch." }
    $status = [string]$Aggregate.Status
    if ($status -cnotin @($Contract.AggregateStatuses)) { throw "Invalid aggregate Status '$status'." }
    if ([string]$Aggregate.Reason -cnotin @($Contract.Reasons.$status)) { throw "Aggregate Reason does not match Status '$status'." }
    foreach ($name in @("SelectedRoute", "SoleUnityConsumedSelector", "D1Authorized", "ProductionInstallAdmission", "DeclaredFullSemanticEligibility", "NextGate"))
    {
        $actual = Get-D0M2TRequiredProperty -Object $Aggregate -Name $name -Context "Aggregate"
        $expected = $Contract.FrozenTerminalFields.$name
        if ($null -eq $actual -or $actual.GetType() -ne $expected.GetType() -or $actual -cne $expected) { throw "Frozen aggregate field '$name' type/value drift." }
    }
    foreach ($name in @("Inputs", "Authority", "Selector", "Unity", "Fixture", "Protected"))
    {
        Assert-D0M2TJsonObject -Value (Get-D0M2TRequiredProperty -Object $Aggregate -Name $name -Context "Aggregate") -Context $name
    }
    Assert-D0M2TExactSet -Actual @($Aggregate.Inputs.PSObject.Properties | ForEach-Object Name) -Expected @("UnityPath", "UnitySha256", "HarnessSha256", "ArchiveIndexSha256", "FrozenInputs", "FrozenRecordSha256") -Context "Aggregate Inputs fields"
    $liveIndex = Get-D0M2TLiveArchiveIndex -Contract $Contract
    $script:ValidatedArchiveIndex = $liveIndex.Index
    if ([string]$Aggregate.Inputs.ArchiveIndexSha256 -cne [string]$liveIndex.Sha256) { throw "InputIdentityDrift: aggregate archive index SHA drift." }
    if (-not [IO.File]::Exists($tarballRunnerPath) -or [string]$Aggregate.Inputs.HarnessSha256 -cne (Get-D0M2TFileSha256 $tarballRunnerPath)) { throw "ToolIdentityDrift: aggregate tarball harness SHA drift." }
    $unityPath = [string]$Aggregate.Inputs.UnityPath
    if (-not [IO.Path]::IsPathRooted($unityPath) -or -not [IO.File]::Exists($unityPath) -or [string]$Aggregate.Inputs.UnitySha256 -cne (Get-D0M2TFileSha256 $unityPath)) { throw "UnityIdentityMismatch: aggregate Unity path/SHA drift." }
    Test-D0M2TFrozenInputs -Contract $Contract -Inputs $Aggregate.Inputs
}

# 校验固定 TT-01..TT-08、名称、状态与聚合终态关系。
function Test-D0M2TCases
{
    param([Parameter(Mandatory = $true)]$Contract, [Parameter(Mandatory = $true)]$Aggregate)

    Assert-D0M2TJsonArray -Value $Aggregate.Cases -Context "Cases"
    $cases = @($Aggregate.Cases)
    if ($cases.Count -ne 8) { throw "Cases must contain exactly eight entries." }
    Assert-D0M2TExactSequence -Actual @($cases | ForEach-Object { [string]$_.CaseId }) -Expected @($Contract.CaseSet | ForEach-Object { [string]$_.CaseId }) -Context "Aggregate case IDs"
    Assert-D0M2TExactSequence -Actual @($cases | ForEach-Object { [string]$_.Name }) -Expected @($Contract.CaseSet | ForEach-Object { [string]$_.Name }) -Context "Aggregate case names"
    foreach ($case in $cases)
    {
        foreach ($field in @("CaseId", "Name", "Status", "Reason", "EvidenceIds")) { [void](Get-D0M2TRequiredProperty -Object $case -Name $field -Context "Case") }
        $caseStatus = [string]$case.Status
        $caseReason = [string]$case.Reason
        if ($caseStatus -cnotin @($Contract.CaseStatuses) -or $caseReason -cnotin @($Contract.CaseReasons)) { throw "Case '$($case.CaseId)' has invalid status or reason." }
        if ($caseStatus -ceq "Passed" -and $caseReason -cne "None") { throw "Passed case '$($case.CaseId)' must use Reason=None." }
        if ($caseStatus -ceq "Failed" -and $caseReason -cnotin @($Contract.Reasons.RouteRejected)) { throw "Failed case '$($case.CaseId)' must use a route rejection reason." }
        if ($caseStatus -ceq "Inconclusive" -and $caseReason -cnotin @($Contract.Reasons.Inconclusive)) { throw "Inconclusive case '$($case.CaseId)' has invalid reason." }
        if ($caseStatus -ceq "NotRun" -and $caseReason -cne "NotRun") { throw "NotRun case '$($case.CaseId)' must use Reason=NotRun." }
        Assert-D0M2TJsonArray -Value $case.EvidenceIds -Context "Case '$($case.CaseId)' EvidenceIds"
        $evidenceCount = @($case.EvidenceIds).Count
        if ($caseStatus -cin @("Passed", "Failed") -and $evidenceCount -eq 0) { throw "Conclusive case '$($case.CaseId)' must reference raw evidence." }
        if ($caseStatus -ceq "NotRun" -and $evidenceCount -ne 0) { throw "NotRun case '$($case.CaseId)' must not claim raw evidence." }
    }
    if ([string]$Aggregate.Status -ceq "Passed" -and @($cases | Where-Object { [string]$_.Status -cne "Passed" }).Count -ne 0) { throw "Passed aggregate requires eight passed cases." }
    if ([string]$Aggregate.Status -ceq "RouteRejected" -and @($cases | Where-Object { [string]$_.Status -ceq "Failed" }).Count -eq 0) { throw "RouteRejected aggregate requires a failed case." }
    if ([string]$Aggregate.Status -ceq "Inconclusive" -and @($cases | Where-Object { [string]$_.Status -in @("Inconclusive", "NotRun") }).Count -eq 0) { throw "Inconclusive aggregate requires an inconclusive or not-run case." }
    return $cases
}

# 打开每个 raw evidence，并复算严格的长度与 SHA-256 绑定。
function Test-D0M2TEvidenceFiles
{
    param([Parameter(Mandatory = $true)]$Contract, [Parameter(Mandatory = $true)]$Aggregate, [Parameter(Mandatory = $true)][string]$RootPath)

    Assert-D0M2TJsonArray -Value $Aggregate.EvidenceFiles -Context "EvidenceFiles"
    $byId = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $pathById = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($evidence in @($Aggregate.EvidenceFiles))
    {
        Assert-D0M2TExactSet -Actual @($evidence.PSObject.Properties | ForEach-Object Name) -Expected @($Contract.EvidenceFileFields | ForEach-Object { [string]$_ }) -Context "Evidence file fields"
        $evidenceId = [string]$evidence.EvidenceId
        if ($evidenceId -cnotmatch "^TT-0[1-8]-[a-z0-9][a-z0-9._-]*$" -or -not $byId.TryAdd($evidenceId, $evidence)) { throw "Invalid or duplicate EvidenceId '$evidenceId'." }
        if ([string]$evidence.CaseId -cnotmatch "^TT-0[1-8]$" -or $evidenceId -cnotmatch "^$([regex]::Escape([string]$evidence.CaseId))-" ) { throw "Evidence '$evidenceId' CaseId mismatch." }
        if ([string]$evidence.Kind -cnotin @($Contract.EvidenceKinds)) { throw "Evidence '$evidenceId' has invalid Kind." }
        if ($evidence.Length -isnot [int] -and $evidence.Length -isnot [long]) { throw "Evidence '$evidenceId' Length must be an integer." }
        $rawPath = Resolve-D0M2TEvidencePath -RootPath $RootPath -RelativePath ([string]$evidence.Path)
        $rawLength = ([IO.FileInfo]::new($rawPath)).Length
        if ([long]$evidence.Length -ne $rawLength) { throw "Evidence '$evidenceId' Length mismatch." }
        if ([string]$evidence.Sha256 -cnotmatch "^[0-9a-f]{64}$" -or (Get-D0M2TFileSha256 -FilePath $rawPath) -cne [string]$evidence.Sha256) { throw "Evidence '$evidenceId' SHA-256 mismatch." }
        $pathById.Add($evidenceId, $rawPath)
    }
    return [pscustomobject]@{ ById = $byId; PathById = $pathById }
}

# 校验 case 到 raw evidence 的双向闭包及每个 case 的最低证据种类。
function Test-D0M2TEvidenceReferences
{
    param([Parameter(Mandatory = $true)]$Contract, [Parameter(Mandatory = $true)][object[]]$Cases, [Parameter(Mandatory = $true)]$EvidenceIndex)

    $referenced = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($case in $Cases)
    {
        $ids = @($case.EvidenceIds | ForEach-Object { [string]$_ })
        Assert-D0M2TExactSet -Actual $ids -Expected $ids -Context "Case '$($case.CaseId)' EvidenceIds"
        foreach ($evidenceId in $ids)
        {
            if (-not $EvidenceIndex.ById.ContainsKey($evidenceId)) { throw "Case '$($case.CaseId)' references missing evidence '$evidenceId'." }
            if ([string]$EvidenceIndex.ById[$evidenceId].CaseId -cne [string]$case.CaseId) { throw "Evidence '$evidenceId' belongs to another case." }
            [void]$referenced.Add($evidenceId)
        }
        $requiredKinds = @($Contract.CaseSet | Where-Object { [string]$_.CaseId -ceq [string]$case.CaseId } | ForEach-Object { $_.RequiredEvidenceKinds })
        $actualKinds = @($ids | ForEach-Object { [string]$EvidenceIndex.ById[$_].Kind } | Select-Object -Unique)
        if ([string]$case.Status -cin @("Passed", "Failed"))
        {
            foreach ($kind in $requiredKinds) { if ([string]$kind -cnotin $actualKinds) { throw "Case '$($case.CaseId)' lacks required evidence Kind '$kind'." } }
        }
    }
    Assert-D0M2TExactSet -Actual @($referenced | ForEach-Object { [string]$_ }) -Expected @($EvidenceIndex.ById.Keys | ForEach-Object { [string]$_ }) -Context "Referenced evidence closure"
}

# 校验 transient/harness 实例的字段、权限、bytes 摘要与域内唯一性。
function Test-D0M2TExperimentObjectRows
{
    param($Contract, $Rows, [ValidateSet("Transient", "Harness")][string]$Domain)

    Assert-D0M2TJsonArray -Value $Rows -Context "$Domain observations"
    $expectedFields = if ($Domain -ceq "Transient") { @($Contract.TransientArtifactObservationFields) } else { @($Contract.HarnessControlObservationFields) }
    $keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $paths = [Collections.Generic.List[string]]::new()
    foreach ($row in @($Rows))
    {
        Assert-D0M2TExactSet -Actual @($row.PSObject.Properties | ForEach-Object Name) -Expected $expectedFields -Context "$Domain observation fields"
        $path = [string]$row.Path
        $unityAuthorityIsInteger = $row.UnityConsumerAuthority -is [int] -or $row.UnityConsumerAuthority -is [long]
        Assert-D0M2TEvidenceCondition -Condition ([IO.Path]::IsPathRooted($path) -and [string]$row.ConsumerAuthority -ceq "None" -and $unityAuthorityIsInteger -and [int]$row.UnityConsumerAuthority -eq 0 -and $row.NeverFallbackSelector -is [bool] -and $row.NeverFallbackSelector -eq $true) -Message "$Domain observation path/selector authority drift."
        $normalized = $path.Replace('\', '/') ; $paths.Add($normalized)
        $exists = if ($Domain -ceq "Transient") { $row.ExistedAfterKill } else { $row.Observed }
        Assert-D0M2TEvidenceCondition -Condition ($exists -is [bool] -and $row.RemovedAfterCleanup -is [bool] -and ($row.ObservedLength -is [int] -or $row.ObservedLength -is [long])) -Message "$Domain lifecycle fields have invalid types."
        $emptyBytes = [long]$row.ObservedLength -eq 0 -and [string]::IsNullOrEmpty([string]$row.ObservedSha256)
        $fileBytes = [long]$row.ObservedLength -gt 0 -and [string]$row.ObservedSha256 -cmatch "^[0-9a-f]{64}$"
        Assert-D0M2TEvidenceCondition -Condition ($emptyBytes -or $fileBytes) -Message "$Domain observed bytes binding is invalid."
        if ($Domain -ceq "Transient")
        {
            Assert-D0M2TEvidenceCondition -Condition ([string]$row.CaseId -ceq "TT-02" -and [string]$row.Phase -cin @("BeforeReplace", "AfterReplace") -and [string]$row.Kind -cin @("ManifestNext", "ManifestBackup") -and [string]$row.Role -ceq "Derived") -Message "Transient identity/role drift."
            $key = "$($row.CaseId)|$($row.Phase)|$normalized|$($row.Kind)"
        }
        else
        {
            Assert-D0M2TEvidenceCondition -Condition ([string]$row.CaseId -cmatch "^TT-0[1-8]$" -and -not [string]::IsNullOrWhiteSpace([string]$row.Kind)) -Message "Harness control identity drift."
            $key = "$($row.CaseId)|$normalized|$($row.Kind)"
        }
        Assert-D0M2TEvidenceCondition -Condition $keys.Add($key) -Message "$Domain observation is duplicated: $key"
    }
    return @($paths)
}

# 校验唯一 selector、三域角色互斥与 Protected before/after 证据绑定。
function Test-D0M2TRolesAndProtected
{
    param([Parameter(Mandatory = $true)]$Contract, [Parameter(Mandatory = $true)]$Aggregate, [Parameter(Mandatory = $true)]$EvidenceIndex)

    Assert-D0M2TJsonArray -Value $Aggregate.RoleObservations -Context "RoleObservations"
    $selectors = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $roleByPath = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($observation in @($Aggregate.RoleObservations))
    {
        Assert-D0M2TExactSet -Actual @($observation.PSObject.Properties | ForEach-Object Name) -Expected @($Contract.PersistentRoleObservationFields) -Context "Persistent role fields"
        $rolePath = [string](Get-D0M2TRequiredProperty -Object $observation -Name "Path" -Context "Role observation")
        $role = [string](Get-D0M2TRequiredProperty -Object $observation -Name "Role" -Context "Role observation")
        $consumer = [string](Get-D0M2TRequiredProperty -Object $observation -Name "ConsumerAuthority" -Context "Role observation")
        $unityAuthority = Get-D0M2TRequiredProperty $observation "UnityConsumerAuthority" "Role observation"
        $neverFallback = Get-D0M2TRequiredProperty $observation "NeverFallbackSelector" "Role observation"
        $unityAuthorityIsInteger = $unityAuthority -is [int] -or $unityAuthority -is [long]
        if ([string]::IsNullOrWhiteSpace($rolePath) -or $role -cnotin @($Contract.Roles) -or [string]::IsNullOrWhiteSpace($consumer) -or -not $unityAuthorityIsInteger -or $neverFallback -isnot [bool]) { throw "Invalid role observation." }
        $normalizedRolePath = $rolePath.Replace('\', '/')
        if ($roleByPath.ContainsKey($normalizedRolePath) -and $roleByPath[$normalizedRolePath] -cne "$role|$consumer") { throw "Role observation '$rolePath' conflicts." }
        $roleByPath[$normalizedRolePath] = "$role|$consumer"
        if ($consumer -ceq "SoleUnitySelector")
        {
            if ([int]$unityAuthority -ne 1 -or $neverFallback -ne $false) { throw "Sole selector flags drift." }
            if ($normalizedRolePath -ceq "Packages/manifest.json" -or $normalizedRolePath.EndsWith('/Packages/manifest.json', [StringComparison]::OrdinalIgnoreCase)) { [void]$selectors.Add("Packages/manifest.json") }
            else { [void]$selectors.Add($normalizedRolePath) }
        }
        elseif ([int]$unityAuthority -ne 0 -or $neverFallback -ne $true) { throw "Non-selector persistent role gained fallback authority." }
        if ($role -ceq "Cache" -and $consumer -ceq "SoleUnitySelector") { throw "Cache cannot be a Unity selector." }
    }
    $transientPaths = @(Test-D0M2TExperimentObjectRows $Contract $Aggregate.TransientArtifactObservations "Transient")
    $harnessPaths = @(Test-D0M2TExperimentObjectRows $Contract $Aggregate.HarnessControlObservations "Harness")
    $persistentPaths = @($roleByPath.Keys | ForEach-Object { [string]$_ })
    Assert-D0M2TEvidenceCondition -Condition (@($transientPaths | Where-Object { $_ -cin $persistentPaths -or $_ -cin $harnessPaths }).Count -eq 0 -and @($harnessPaths | Where-Object { $_ -cin $persistentPaths }).Count -eq 0) -Message "Persistent/transient/harness domains overlap."
    $isConclusive = [string]$Aggregate.Status -cin @("Passed", "RouteRejected")
    if ($isConclusive)
    {
        Assert-D0M2TExactSet -Actual @($selectors | ForEach-Object { [string]$_ }) -Expected @("Packages/manifest.json") -Context "Sole Unity selector"
        Assert-D0M2TEvidenceCondition -Condition ($transientPaths.Count -gt 0 -and $harnessPaths.Count -gt 0) -Message "Conclusive experiment object closure is empty."
    }
    elseif ($selectors.Count -gt 0)
    {
        Assert-D0M2TExactSet -Actual @($selectors | ForEach-Object { [string]$_ }) -Expected @("Packages/manifest.json") -Context "Partial Sole Unity selector"
    }
    $unchanged = Get-D0M2TRequiredProperty -Object $Aggregate.Protected -Name "Unchanged" -Context "Protected"
    if ($unchanged -isnot [bool]) { throw "Protected.Unchanged must be boolean." }
    $beforeProperty = @($Aggregate.Protected.PSObject.Properties | Where-Object Name -CEQ "BeforeSnapshotEvidenceId")
    $afterProperty = @($Aggregate.Protected.PSObject.Properties | Where-Object Name -CEQ "AfterSnapshotEvidenceId")
    if ($beforeProperty.Count -ne $afterProperty.Count -or ($isConclusive -and $beforeProperty.Count -ne 1)) { throw "Protected before/after evidence binding is incomplete." }
    if ($beforeProperty.Count -eq 1)
    {
        $beforeId = [string]$beforeProperty[0].Value
        $afterId = [string]$afterProperty[0].Value
        if ($beforeId -ceq $afterId) { throw "Protected before/after evidence IDs must be distinct." }
        foreach ($evidenceId in @($beforeId, $afterId))
        {
            if (-not $EvidenceIndex.ById.ContainsKey($evidenceId)) { throw "Protected snapshot evidence is missing: $evidenceId" }
            $item = $EvidenceIndex.ById[$evidenceId]
            if ([string]$item.CaseId -cne "TT-07" -or [string]$item.Kind -cne "ProtectedSnapshot") { throw "Protected snapshot '$evidenceId' has an invalid binding." }
        }
    }
    if ([string]$Aggregate.Status -ceq "Passed" -and $unchanged -ne $true) { throw "Passed aggregate requires Protected.Unchanged=true." }
}

# 校验 TT-08 独立 raw closure 完整绑定此前证据且自身不参与自哈希。
function Test-D0M2TEvidenceClosure
{
    param([Parameter(Mandatory = $true)]$Contract, [Parameter(Mandatory = $true)]$Aggregate, [Parameter(Mandatory = $true)]$EvidenceIndex)

    $closureItems = @($Aggregate.EvidenceFiles | Where-Object { [string]$_.Kind -ceq "EvidenceClosure" })
    $tt08 = @($Aggregate.Cases | Where-Object CaseId -CEQ "TT-08")[0]
    $requiresClosure = [string]$Aggregate.Status -cin @("Passed", "RouteRejected") -or [string]$tt08.Status -cin @("Passed", "Failed")
    if ($closureItems.Count -eq 0)
    {
        if ($requiresClosure) { throw "Conclusive evidence requires one TT-08 EvidenceClosure." }
        return
    }
    if ($closureItems.Count -ne 1 -or [string]$closureItems[0].CaseId -cne "TT-08" -or [string]$tt08.Status -ceq "NotRun") { throw "Invalid TT-08 EvidenceClosure cardinality or status." }
    $closureId = [string]$closureItems[0].EvidenceId
    $closure = Read-D0M2TJsonObject -FilePath $EvidenceIndex.PathById[$closureId]
    Assert-D0M2TExactSet -Actual @($closure.PSObject.Properties | ForEach-Object Name) -Expected @($Contract.EvidenceClosureRequiredFields | ForEach-Object { [string]$_ }) -Context "Evidence closure fields"
    if ([string]$closure.Schema -cne [string]$Contract.EvidenceClosureSchema -or [string]$closure.RunId -cne [string]$Aggregate.RunId) { throw "Evidence closure identity mismatch." }
    Assert-D0M2TJsonArray -Value $closure.PriorEvidenceFiles -Context "Evidence closure PriorEvidenceFiles"
    $priorItems = @($Aggregate.EvidenceFiles | Where-Object { [string]$_.EvidenceId -cne $closureId })
    $expectedBindings = @($priorItems | ForEach-Object { "$($_.EvidenceId)|$($_.CaseId)|$($_.Kind)|$($_.Path)|$($_.Sha256)|$($_.Length)" })
    $actualBindings = @($closure.PriorEvidenceFiles | ForEach-Object {
        Assert-D0M2TExactSet -Actual @($_.PSObject.Properties | ForEach-Object Name) -Expected @($Contract.EvidenceFileFields | ForEach-Object { [string]$_ }) -Context "Closure prior evidence fields"
        "$($_.EvidenceId)|$($_.CaseId)|$($_.Kind)|$($_.Path)|$($_.Sha256)|$($_.Length)"
    })
    Assert-D0M2TExactSet -Actual $actualBindings -Expected $expectedBindings -Context "Evidence closure prior bindings"
    Assert-D0M2TJsonObject -Value $closure.Checks -Context "Evidence closure Checks"
    Assert-D0M2TExactSet -Actual @($closure.Checks.PSObject.Properties | ForEach-Object Name) -Expected @($Contract.EvidenceClosureCheckFields | ForEach-Object { [string]$_ }) -Context "Evidence closure check fields"
    foreach ($name in @("AllPathsRelative", "AllFilesOpenable", "LengthsMatch", "Sha256Match")) { if ($closure.Checks.$name -isnot [bool] -or $closure.Checks.$name -ne $true) { throw "Evidence closure check '$name' must be boolean true." } }
    foreach ($name in @("DuplicateEvidenceIds", "OrphanEvidenceIds")) { if ($closure.Checks.$name -isnot [bool] -or $closure.Checks.$name -ne $false) { throw "Evidence closure check '$name' must be boolean false." } }
    Assert-D0M2TJsonObject -Value $closure.FixtureCleanup -Context "Evidence closure FixtureCleanup"
    Assert-D0M2TExactSet -Actual @($closure.FixtureCleanup.PSObject.Properties | ForEach-Object Name) -Expected @($Contract.EvidenceClosureCleanupFields | ForEach-Object { [string]$_ }) -Context "Fixture cleanup fields"
    if ($closure.FixtureCleanup.Requested -isnot [bool] -or $closure.Passed -isnot [bool]) { throw "Evidence closure cleanup/admission flags must be boolean." }
    foreach ($name in @("CreatedRoots", "RemovedRoots", "ResidualRoots")) { Assert-D0M2TJsonArray -Value $closure.FixtureCleanup.$name -Context "Evidence closure FixtureCleanup.$name" }
    foreach ($name in @("OwnedProcessIds", "LiveOwnedProcessIds")) { Assert-D0M2TJsonArray -Value $closure.$name -Context "Evidence closure $name" }
    if ($closure.Passed -eq $true -and [string]$closure.Reason -cne "None") { throw "Passed evidence closure must use Reason=None." }
    if ($closure.Passed -eq $false -and [string]$closure.Reason -cnotin @($Contract.Reasons.Inconclusive)) { throw "Failed evidence closure must use an inconclusive reason." }
    if ([string]$Aggregate.Status -ceq "Passed" -and ($closure.Passed -ne $true -or [string]$closure.Reason -cne "None" -or @($closure.FixtureCleanup.ResidualRoots).Count -ne 0 -or @($closure.LiveOwnedProcessIds).Count -ne 0)) { throw "Passed aggregate requires a clean TT-08 closure." }
}

# 将 typed raw 语义失败统一标成 EvidenceIncomplete，供中央 runner 保留稳定原因。
function Assert-D0M2TEvidenceCondition
{
    param([Parameter(Mandatory = $true)][bool]$Condition, [Parameter(Mandatory = $true)][string]$Message)

    if (-not $Condition) { throw "EvidenceIncomplete: $Message" }
}

# 对指定 raw 属性同时断言 JSON boolean 类型与预期值，拒绝字符串真假值。
function Assert-D0M2TBooleanProperties
{
    param($Object, [string[]]$Names, [bool]$Expected, [string]$Context)

    foreach ($name in $Names)
    {
        $value = Get-D0M2TRequiredProperty -Object $Object -Name $name -Context $Context
        Assert-D0M2TEvidenceCondition -Condition ($value -is [bool] -and $value -eq $Expected) -Message "$Context.$name must be boolean $Expected."
    }
}

# 按固定 ID、case、kind 与 schema 打开唯一 raw JSON，避免从自报摘要猜证据类型。
function Get-D0M2TTypedEvidence
{
    param(
        [Parameter(Mandatory = $true)]$EvidenceIndex,
        [Parameter(Mandatory = $true)][string]$EvidenceId,
        [Parameter(Mandatory = $true)][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$Kind,
        [AllowEmptyString()][string]$Schema = "",
        [AllowEmptyString()][string]$RunId = ""
    )

    Assert-D0M2TEvidenceCondition -Condition $EvidenceIndex.ById.ContainsKey($EvidenceId) -Message "missing typed raw '$EvidenceId'."
    $declaration = $EvidenceIndex.ById[$EvidenceId]
    Assert-D0M2TEvidenceCondition -Condition ([string]$declaration.CaseId -ceq $CaseId -and [string]$declaration.Kind -ceq $Kind) -Message "typed raw '$EvidenceId' declaration drift."
    $value = Read-D0M2TJsonObject -FilePath $EvidenceIndex.PathById[$EvidenceId]
    if (-not [string]::IsNullOrWhiteSpace($Schema))
    {
        $actualSchema = [string](Get-D0M2TRequiredProperty -Object $value -Name "Schema" -Context $EvidenceId)
        Assert-D0M2TEvidenceCondition -Condition ($actualSchema -ceq $Schema) -Message "typed raw '$EvidenceId' schema drift."
    }
    if (-not [string]::IsNullOrWhiteSpace($RunId))
    {
        $actualRunId = [string](Get-D0M2TRequiredProperty -Object $value -Name "RunId" -Context $EvidenceId)
        Assert-D0M2TEvidenceCondition -Condition ($actualRunId -ceq $RunId) -Message "typed raw '$EvidenceId' RunId drift."
    }
    return $value
}

# 比较由同一 raw 对象复制到 aggregate 的 JSON 内容，阻断顶层与原始证据分叉。
function Assert-D0M2TJsonEquivalent
{
    param($Actual, $Expected, [Parameter(Mandatory = $true)][string]$Context)

    $actualJson = $Actual | ConvertTo-Json -Compress -Depth 100
    $expectedJson = $Expected | ConvertTo-Json -Compress -Depth 100
    Assert-D0M2TEvidenceCondition -Condition ($actualJson -ceq $expectedJson) -Message "$Context is not bound to raw evidence."
}

# 验证 archive 声明锚定的 generation、content-address filename、期望 SHA/Length。
function Get-D0M2TArchiveExpectedBindings
{
    param($Rows, [Parameter(Mandatory = $true)][string]$Context)

    Assert-D0M2TJsonArray -Value $Rows -Context $Context
    $items = @($Rows)
    Assert-D0M2TEvidenceCondition -Condition ($items.Count -eq 2) -Message "$Context must contain archive A and B."
    Assert-D0M2TExactSet -Actual @($items | ForEach-Object { [string]$_.GenerationId }) -Expected @("A", "B") -Context "$Context generations"
    $bindings = foreach ($item in $items)
    {
        foreach ($field in @("FileName", "ExpectedSha256", "ExpectedLength"))
        {
            [void](Get-D0M2TRequiredProperty -Object $item -Name $field -Context $Context)
        }
        $expectedSha = [string]$item.ExpectedSha256
        Assert-D0M2TEvidenceCondition -Condition ($expectedSha -cmatch "^[0-9a-f]{64}$" -and [string]$item.FileName -ceq "$expectedSha.tgz") -Message "$Context has a non-content-addressed archive."
        Assert-D0M2TEvidenceCondition -Condition ([long]$item.ExpectedLength -gt 0) -Message "$Context expected archive length is invalid."
        "$([string]$item.GenerationId)|$([string]$item.FileName)|$expectedSha|$([long]$item.ExpectedLength)"
    }
    return @($bindings)
}

# 验证一组 immutable archive 的现场 bytes、single-link、readonly 并返回期望绑定。
function Test-D0M2TArchiveRows
{
    param($Rows, [Parameter(Mandatory = $true)][string]$Context)

    $bindings = @(Get-D0M2TArchiveExpectedBindings -Rows $Rows -Context $Context)
    foreach ($item in @($Rows))
    {
        foreach ($field in @("ActualSha256", "ActualLength", "LinkCount", "ReadOnly")) { [void](Get-D0M2TRequiredProperty $item $field $Context) }
        Assert-D0M2TEvidenceCondition -Condition ([string]$item.ActualSha256 -ceq [string]$item.ExpectedSha256 -and [long]$item.ActualLength -eq [long]$item.ExpectedLength) -Message "$Context archive bytes drift."
        Assert-D0M2TEvidenceCondition -Condition ([int]$item.LinkCount -eq 1 -and $item.ReadOnly -is [bool] -and $item.ReadOnly -eq $true) -Message "$Context archive mutability/link invariant failed."
    }
    return @($bindings)
}

# 验证 Unity probe 存在且版本、package、archive 与 PackageCache 物理身份可信。
function Test-D0M2TUnityPayloadIdentity
{
    param($Payload, [Parameter(Mandatory = $true)][string]$Generation, [Parameter(Mandatory = $true)][string]$ArchiveSha, [Parameter(Mandatory = $true)][string]$Context)

    Assert-D0M2TJsonObject -Value $Payload -Context $Context
    foreach ($field in @("Passed", "UnityVersion", "ExpectedGeneration", "ExpectedArchiveSha256", "PackageName", "PackageVersion", "ResolvedPackagePath"))
    {
        [void](Get-D0M2TRequiredProperty -Object $Payload -Name $field -Context $Context)
    }
    Assert-D0M2TEvidenceCondition -Condition ($Payload.Passed -is [bool]) -Message "$Context Passed must be boolean."
    Assert-D0M2TEvidenceCondition -Condition ([string]$Payload.UnityVersion -ceq "6000.3.14f1" -and [string]$Payload.ExpectedGeneration -ceq $Generation) -Message "$Context Unity/generation identity drift."
    Assert-D0M2TEvidenceCondition -Condition ([string]$Payload.ExpectedArchiveSha256 -ceq $ArchiveSha -and $ArchiveSha -cmatch "^[0-9a-f]{64}$") -Message "$Context archive SHA drift."
    Assert-D0M2TEvidenceCondition -Condition ([string]$Payload.PackageName -ceq "com.exhard.exgas.d0m2f-tarball" -and -not [string]::IsNullOrWhiteSpace([string]$Payload.PackageVersion)) -Message "$Context package identity drift."
    $indexedArchive = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ $Generation)
    Assert-D0M2TEvidenceCondition -Condition ($indexedArchive.Count -eq 1 -and [string]$Payload.PackageVersion -ceq [string]$indexedArchive[0].PackageVersion) -Message "$Context package version is not bound to ArchiveIndex."
    $resolvedPath = ([string]$Payload.ResolvedPackagePath).Replace('\', '/')
    Assert-D0M2TEvidenceCondition -Condition ($resolvedPath.IndexOf('/Library/PackageCache/', [StringComparison]::OrdinalIgnoreCase) -ge 0) -Message "$Context did not resolve from PackageCache."
}

# 从 Unity raw 的程序集与路径重算 runner validation，禁止负链只信 RestartValidation 摘要。
function Get-D0M2TUnityPayloadFacts
{
    param($Payload, [string]$Generation, [string]$ArchiveSha, [string]$Context)

    Test-D0M2TUnityPayloadIdentity $Payload $Generation $ArchiveSha $Context
    foreach ($field in @("PackageSource", "PackageId", "Assemblies", "CompileGraphSha256")) { [void](Get-D0M2TRequiredProperty $Payload $field $Context) }
    Assert-D0M2TEvidenceCondition -Condition ([string]$Payload.CompileGraphSha256 -cmatch "^[0-9a-f]{64}$") -Message "$Context compile graph hash is invalid."
    Assert-D0M2TJsonArray -Value $Payload.Assemblies -Context "$Context Assemblies"
    $assemblies = @($Payload.Assemblies)
    foreach ($assembly in $assemblies)
    {
        foreach ($field in @("Name", "Generation", "GenerationToken", "SourceFiles")) { [void](Get-D0M2TRequiredProperty $assembly $field "$Context assembly") }
        Assert-D0M2TJsonArray -Value $assembly.SourceFiles -Context "$Context SourceFiles"
        Assert-D0M2TEvidenceCondition -Condition (@($assembly.SourceFiles).Count -gt 0) -Message "$Context assembly lacks source evidence."
    }
    $archive = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ $Generation)[0]
    $expectedNames = @("com.exhard.exgas.autochessdemo", "com.exhard.exgas.generated.editor", "com.exhard.exgas.generated.runtime")
    $actualNames = @($assemblies | ForEach-Object { [string]$_.Name } | Sort-Object)
    $assemblySetMatches = $assemblies.Count -eq 3 -and ($actualNames -join "|") -ceq ($expectedNames -join "|")
    $allGeneration = $assemblies.Count -eq 3 -and @($assemblies | Where-Object { [string]$_.Generation -cne $Generation -or [string]$_.GenerationToken -cne [string]$archive.GenerationToken }).Count -eq 0
    return [pscustomobject]@{ IdentityMatches = $true; AssemblySetMatches = $assemblySetMatches; AllGeneration = $allGeneration; CacheMatches = $true; Passed = $Payload.Passed -eq $true -and $assemblySetMatches -and $allGeneration }
}

# 验证 Unity probe 对三程序集、generation 与 compile graph 的 Passed typed 观察。
function Test-D0M2TUnityPayload
{
    param($Payload, [Parameter(Mandatory = $true)][string]$Generation, [Parameter(Mandatory = $true)][string]$ArchiveSha, [Parameter(Mandatory = $true)][string]$Context)

    Test-D0M2TUnityPayloadIdentity -Payload $Payload -Generation $Generation -ArchiveSha $ArchiveSha -Context $Context
    $indexedArchive = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ $Generation)
    foreach ($field in @("PackageSource", "PackageId", "Assemblies", "CompileGraphSha256")) { [void](Get-D0M2TRequiredProperty $Payload $field $Context) }
    Assert-D0M2TEvidenceCondition -Condition ($Payload.Passed -eq $true) -Message "$Context did not pass."
    Assert-D0M2TEvidenceCondition -Condition ([string]$Payload.CompileGraphSha256 -cmatch "^[0-9a-f]{64}$") -Message "$Context compile graph hash is invalid."
    Assert-D0M2TJsonArray -Value $Payload.Assemblies -Context "$Context Assemblies"
    $assemblies = @($Payload.Assemblies)
    $expectedNames = @("com.exhard.exgas.autochessdemo", "com.exhard.exgas.generated.editor", "com.exhard.exgas.generated.runtime")
    Assert-D0M2TExactSet -Actual @($assemblies | ForEach-Object { [string]$_.Name }) -Expected $expectedNames -Context "$Context assembly names"
    foreach ($assembly in $assemblies)
    {
        Assert-D0M2TEvidenceCondition -Condition ([string]$assembly.Generation -ceq $Generation -and [string]$assembly.GenerationToken -ceq [string]$indexedArchive[0].GenerationToken) -Message "$Context assembly generation/token split."
        $sourceFiles = Get-D0M2TRequiredProperty -Object $assembly -Name "SourceFiles" -Context "$Context assembly"
        Assert-D0M2TJsonArray -Value $sourceFiles -Context "$Context SourceFiles"
        Assert-D0M2TEvidenceCondition -Condition (@($sourceFiles).Count -gt 0) -Message "$Context assembly lacks source evidence."
    }
    Assert-D0M2TEvidenceCondition -Condition (@($assemblies | ForEach-Object { [string]$_.GenerationToken } | Select-Object -Unique).Count -eq 1) -Message "$Context generation tokens split."
}

# 将 restart process、Unity raw 与 RestartValidation 每个派生字段双向绑定并返回真实 Passed。
function Test-D0M2TRestartValidationBinding
{
    param($Summary, $Process, $PayloadFacts, [string]$Context)

    $validation = Get-D0M2TRequiredProperty $Summary "RestartValidation" $Context
    $expectedPassed = [int]$Process.ExitCode -eq 0 -and $Process.TimedOut -eq $false -and $PayloadFacts.Passed
    $expected = [ordered]@{
        Passed = $expectedPassed; TimedOut = [bool]$Process.TimedOut; IdentityMatches = $true
        AssemblySetMatches = [bool]$PayloadFacts.AssemblySetMatches; AllGeneration = [bool]$PayloadFacts.AllGeneration; CacheMatches = [bool]$PayloadFacts.CacheMatches
    }
    foreach ($name in $expected.Keys)
    {
        $value = Get-D0M2TRequiredProperty $validation $name "$Context RestartValidation"
        Assert-D0M2TEvidenceCondition -Condition ($value -is [bool] -and $value -eq $expected[$name]) -Message "$Context RestartValidation.$name contradicts raw evidence."
    }
    Assert-D0M2TEvidenceCondition -Condition ([int]$Summary.RestartExitCode -eq [int]$Process.ExitCode -and $Summary.RestartTimedOut -eq $Process.TimedOut -and $Summary.RestartAllB -eq $PayloadFacts.AllGeneration) -Message "$Context restart summary contradicts process/Unity raw."
    return $expectedPassed
}

# 校验 Unity 子进程 raw 的 run/case/name 身份并返回记录。
function Get-D0M2TUnityProcess
{
    param($EvidenceIndex, [string]$EvidenceId, [string]$CaseId, [string]$Name, [string]$RunId)

    $record = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId $EvidenceId -CaseId $CaseId -Kind "ProcessLog" -Schema "D0M2T-UnityProcess-v1" -RunId $RunId
    Assert-D0M2TEvidenceCondition -Condition ([string]$record.CaseId -ceq $CaseId -and [string]$record.Name -ceq $Name) -Message "$EvidenceId process identity drift."
    foreach ($field in @("ExitCode", "TimedOut", "KilledOnResolve", "ResultExistedAtKill")) { [void](Get-D0M2TRequiredProperty -Object $record -Name $field -Context $EvidenceId) }
    foreach ($field in @("TimedOut", "KilledOnResolve", "ResultExistedAtKill"))
    {
        Assert-D0M2TEvidenceCondition -Condition ($record.$field -is [bool]) -Message "$EvidenceId.$field must be boolean."
    }
    return $record
}

# 由 process exit 与可选 Unity result raw 重算 missing/corrupt 的 SuccessfulObservation。
function Get-D0M2TObservationSuccess
{
    param($EvidenceIndex, $Observation, $Process, [string]$EvidenceId, [string]$Context)

    $resultPresent = Get-D0M2TRequiredProperty $Observation "ResultPresent" $Context
    $reportedSuccess = Get-D0M2TRequiredProperty $Observation "SuccessfulObservation" $Context
    Assert-D0M2TEvidenceCondition -Condition ($resultPresent -is [bool] -and $reportedSuccess -is [bool]) -Message "$Context result flags must be boolean."
    $hasPayload = $EvidenceIndex.ById.ContainsKey($EvidenceId)
    Assert-D0M2TEvidenceCondition -Condition ($hasPayload -eq $resultPresent) -Message "$Context ResultPresent contradicts Unity raw declaration."
    $payloadPassed = $false
    if ($hasPayload)
    {
        $payload = Get-D0M2TTypedEvidence $EvidenceIndex $EvidenceId "TT-04" "UnityObservation" "" ""
        $passed = Get-D0M2TRequiredProperty $payload "Passed" "$Context Unity result"
        Assert-D0M2TEvidenceCondition -Condition ($passed -is [bool]) -Message "$Context Unity result Passed must be boolean."
        $payloadPassed = $passed -eq $true
    }
    $computed = [int]$Process.ExitCode -eq 0 -or $payloadPassed
    Assert-D0M2TEvidenceCondition -Condition ($reportedSuccess -eq $computed) -Message "$Context SuccessfulObservation contradicts process/result raw."
    return $computed
}

# 从 watcher.Events 重算 canary、authority writes 与 error 数，不采信 watcher 摘要布尔值。
function Test-D0M2TWatcherEvents
{
    param($Aggregate, $AuthorityRecord, $WatcherRecord, [string]$Context)

    Assert-D0M2TJsonArray -Value $WatcherRecord.Events -Context "$Context Events"
    $archiveRoles = @($Aggregate.RoleObservations | Where-Object { [string]$_.Role -ceq "Authority" -and [string]$_.ConsumerAuthority -ceq "Payload" })
    Assert-D0M2TEvidenceCondition -Condition ($archiveRoles.Count -eq 2) -Message "$Context archive roles are incomplete."
    $authorityRoots = @($archiveRoles | ForEach-Object { ([IO.Path]::GetDirectoryName([string]$_.Path)).Replace('\', '/') } | Select-Object -Unique)
    Assert-D0M2TEvidenceCondition -Condition ($authorityRoots.Count -eq 1) -Message "$Context authority root is not unique."
    $authorityPrefix = $authorityRoots[0].TrimEnd('/') + '/'
    $canaryPath = ([string]$WatcherRecord.CanaryPath).Replace('\', '/')
    $canaryCount = @($WatcherRecord.Events | Where-Object { ([string]$_.FullPath).Replace('\', '/') -ceq $canaryPath }).Count
    $errorCount = @($WatcherRecord.Events | Where-Object ChangeType -CEQ "Error").Count
    $writeCount = @($WatcherRecord.Events | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$_.FullPath) -and ([string]$_.FullPath).Replace('\', '/').StartsWith($authorityPrefix, [StringComparison]::OrdinalIgnoreCase)
    }).Count
    Assert-D0M2TEvidenceCondition -Condition ($canaryCount -gt 0 -and $WatcherRecord.CanaryObserved -eq $true -and $AuthorityRecord.CanaryObserved -eq $true) -Message "$Context canary event is absent."
    Assert-D0M2TEvidenceCondition -Condition ($errorCount -eq [int]$WatcherRecord.Errors.Count -and $errorCount -eq [int]$AuthorityRecord.WatcherErrors) -Message "$Context watcher error summary drift."
    Assert-D0M2TEvidenceCondition -Condition ($writeCount -eq [int]$WatcherRecord.AuthorityWrites -and $writeCount -eq [int]$AuthorityRecord.AuthorityWrites) -Message "$Context authority-write summary drift."
}

# 将 fixture 绝对路径规范化为固定七角色集合，并绑定每个角色的 selector 权限。
function Test-D0M2TFullRoleClosure
{
    param([Parameter(Mandatory = $true)]$Aggregate)

    $observations = @($Aggregate.RoleObservations)
    Assert-D0M2TEvidenceCondition -Condition ($observations.Count -eq 7) -Message "RoleObservations must contain exactly seven roles."
    foreach ($item in $observations)
    {
        Assert-D0M2TExactSet -Actual @($item.PSObject.Properties | ForEach-Object Name) -Expected @("Path", "Role", "ConsumerAuthority", "UnityConsumerAuthority", "NeverFallbackSelector") -Context "Role observation fields"
    }
    $manifestItems = @($observations | Where-Object { ([string]$_.Path).Replace('\', '/').EndsWith('/Packages/manifest.json', [StringComparison]::OrdinalIgnoreCase) })
    Assert-D0M2TEvidenceCondition -Condition ($manifestItems.Count -eq 1) -Message "manifest selector role is not unique."
    $manifestPath = ([string]$manifestItems[0].Path).Replace('\', '/')
    Assert-D0M2TEvidenceCondition -Condition ([IO.Path]::IsPathRooted([string]$manifestItems[0].Path)) -Message "role paths must be absolute fixture paths."
    $projectPath = $manifestPath.Substring(0, $manifestPath.Length - "/Packages/manifest.json".Length)
    $separator = $projectPath.LastIndexOf('/')
    Assert-D0M2TEvidenceCondition -Condition ($separator -gt 0) -Message "fixture project role path is invalid."
    $fixtureRoot = $projectPath.Substring(0, $separator)
    $archives = @($Aggregate.Authority.Archives)
    $expected = @(
        "$projectPath/Packages/manifest.json|Authority|SoleUnitySelector|1|False",
        "$projectPath/Packages/packages-lock.json|Derived|None|0|True",
        "$projectPath/ProjectSettings/GasCodeGen/ActiveGenerationRef.json|Derived|AuditOnly|0|True",
        "$projectPath/Library/PackageCache|Cache|None|0|True",
        "$projectPath/Library/Bee|Cache|None|0|True"
    ) + @($archives | ForEach-Object { "$fixtureRoot/Authority/$([string]$_.FileName)|Authority|Payload|0|True" })
    $actual = @($observations | ForEach-Object { "$(([string]$_.Path).Replace('\', '/'))|$([string]$_.Role)|$([string]$_.ConsumerAuthority)|$([int]$_.UnityConsumerAuthority)|$([bool]$_.NeverFallbackSelector)" })
    Assert-D0M2TExactSet -Actual $actual -Expected $expected -Context "Typed role closure"
}

# 计算已观察文本的 UTF-8 bytes 绑定，可选择复现 Write-NewTextFile 的 LF 结尾。
function Get-D0M2TTextByteBinding
{
    param([AllowEmptyString()][string]$Text, [bool]$EnsureLf)

    $normalized = $Text.Replace("`r`n", "`n").Replace("`r", "`n")
    if ($EnsureLf -and -not $normalized.EndsWith("`n", [StringComparison]::Ordinal)) { $normalized += "`n" }
    $bytes = ([Text.UTF8Encoding]::new($false)).GetBytes($normalized)
    return [pscustomobject]@{ Length = [long]$bytes.LongLength; Sha256 = Get-D0M2TBytesSha256 $bytes }
}

# 将观察行的长度与 SHA 绑定到独立重建的 bytes。
function Assert-D0M2TObservedBytes
{
    param($Row, [long]$ExpectedLength, [AllowEmptyString()][string]$ExpectedSha256, [string]$Context)

    Assert-D0M2TEvidenceCondition -Condition ([long]$Row.ObservedLength -eq $ExpectedLength -and [string]$Row.ObservedSha256 -ceq $ExpectedSha256) -Message "$Context observed bytes drift."
}

# 判断绝对实例路径是否位于任一 fixture owner root 内。
function Test-D0M2TPathWithinRoots
{
    param([string]$Path, $Roots)

    $candidate = [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    foreach ($root in @($Roots))
    {
        $normalizedRoot = [IO.Path]::GetFullPath([string]$root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
        $prefix = $normalizedRoot + [IO.Path]::DirectorySeparatorChar
        if ($candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

# 复核 TT-02 每个强杀进程、checkpoint 与 transaction phase 的生命周期交叉绑定。
function Test-D0M2TSelectorLifecycle
{
    param($Aggregate, $Record)

    $phaseFields = @("Phase", "CheckpointObserved", "CheckpointPath", "Killed", "WorkerPath", "ProcessId", "Started", "ExitedAfterKill", "ExitCode", "ManifestSha256", "IsManifestA", "IsManifestB", "Parseable", "Length", "TransactionArtifacts")
    $manifestPath = ([string]@($Aggregate.RoleObservations | Where-Object ConsumerAuthority -CEQ "SoleUnitySelector")[0].Path).Replace('\', '/')
    $projectPath = $manifestPath.Substring(0, $manifestPath.Length - "/Packages/manifest.json".Length)
    $controlPath = $projectPath.Substring(0, $projectPath.LastIndexOf('/')) + "/Control"
    foreach ($phaseName in @("BeforeReplace", "AfterReplace"))
    {
        $phase = $Record.$phaseName
        Assert-D0M2TExactSet -Actual @($phase.PSObject.Properties | ForEach-Object Name) -Expected $phaseFields -Context "TT-02 $phaseName lifecycle fields"
        $phaseRows = @($Record.TransactionArtifacts | Where-Object Phase -CEQ $phaseName)
        Assert-D0M2TJsonEquivalent -Actual $phase.TransactionArtifacts -Expected $phaseRows -Context "TT-02 $phaseName transaction rows"
        $ready = $phase.CheckpointObserved -eq $true -and $phase.Killed -eq $true -and $phase.Started -eq $true -and $phase.ExitedAfterKill -eq $true
        Assert-D0M2TEvidenceCondition -Condition ($ready -and [long]$phase.ProcessId -gt 0 -and $phase.ExitCode -is [long]) -Message "TT-02 $phaseName process lifecycle is incomplete."
        $workerPath = ([string]$phase.WorkerPath).Replace('\', '/'); $checkpointPath = ([string]$phase.CheckpointPath).Replace('\', '/')
        Assert-D0M2TEvidenceCondition -Condition ($workerPath -ceq "$controlPath/selector-worker.ps1" -and $checkpointPath -ceq "$controlPath/selector-$phaseName.checkpoint") -Message "TT-02 $phaseName process path template drift."
        $workerRows = @($Aggregate.HarnessControlObservations | Where-Object { $_.CaseId -ceq "TT-02" -and $_.Kind -ceq "SelectorWorker" -and ([string]$_.Path).Replace('\', '/') -ceq ([string]$phase.WorkerPath).Replace('\', '/') })
        $checkpointRows = @($Aggregate.HarnessControlObservations | Where-Object { $_.CaseId -ceq "TT-02" -and $_.Kind -ceq "SelectorWorkerCheckpoint" -and ([string]$_.Path).Replace('\', '/') -ceq ([string]$phase.CheckpointPath).Replace('\', '/') })
        Assert-D0M2TEvidenceCondition -Condition ($workerRows.Count -eq 1 -and $checkpointRows.Count -eq 1) -Message "TT-02 $phaseName worker/checkpoint control binding is missing."
        $checkpointBytes = Get-D0M2TTextByteBinding -Text $phaseName -EnsureLf $false
        Assert-D0M2TObservedBytes $checkpointRows[0] $checkpointBytes.Length $checkpointBytes.Sha256 "TT-02 $phaseName checkpoint"
    }
}

# 重算 TT-02 四个 transient 对象的 phase、存在性、bytes 与最终移除矩阵。
function Test-D0M2TTransientArtifactClosure
{
    param($Contract, $Aggregate, $EvidenceIndex)

    $record = Get-D0M2TTypedEvidence $EvidenceIndex "TT-02-manifest-snapshot" "TT-02" "ManifestSnapshot" "D0M2T-SelectorAtomicKill-v1" ([string]$Aggregate.RunId)
    Assert-D0M2TJsonArray $record.TransactionArtifacts "TT-02 TransactionArtifacts"
    Assert-D0M2TJsonEquivalent $Aggregate.TransientArtifactObservations $record.TransactionArtifacts "Aggregate transient artifacts"
    $manifestRole = @($Aggregate.RoleObservations | Where-Object ConsumerAuthority -CEQ "SoleUnitySelector")
    Assert-D0M2TEvidenceCondition -Condition ($manifestRole.Count -eq 1) -Message "TT-02 transient closure lacks one manifest role."
    $manifestPath = ([string]$manifestRole[0].Path).Replace('\', '/')
    $archiveA = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ "A")[0]
    $archiveB = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ "B")[0]
    $bindings = @{
        A = Get-D0M2TTextByteBinding (Get-D0M2TManifestText ([string]$archiveA.FileName)) $false
        B = Get-D0M2TTextByteBinding (Get-D0M2TManifestText ([string]$archiveB.FileName)) $false
    }
    foreach ($spec in @($Contract.TransientArtifactMatrix))
    {
        $rows = @($record.TransactionArtifacts | Where-Object { $_.Phase -ceq [string]$spec.Phase -and $_.Kind -ceq [string]$spec.Kind })
        Assert-D0M2TEvidenceCondition -Condition ($rows.Count -eq 1) -Message "TT-02 transient matrix row is missing or duplicated."
        $row = $rows[0]; $suffix = if ([string]$row.Kind -ceq "ManifestNext") { ".next" } else { ".backup" }
        Assert-D0M2TEvidenceCondition -Condition (([string]$row.Path).Replace('\', '/') -ceq ($manifestPath + $suffix) -and $row.ExistedAfterKill -eq [bool]$spec.ExistedAfterKill -and $row.RemovedAfterCleanup -eq $true) -Message "TT-02 transient path/lifecycle drift."
        if ([string]$spec.ExpectedGeneration -ceq "None") { Assert-D0M2TObservedBytes $row 0 "" "TT-02 absent transient" }
        else
        {
            $binding = $bindings[[string]$spec.ExpectedGeneration]
            Assert-D0M2TObservedBytes $row $binding.Length $binding.Sha256 "TT-02 generation $($spec.ExpectedGeneration) transient"
        }
    }
    Test-D0M2TSelectorLifecycle $Aggregate $record
}

# 复核 conclusive harness inventory、owner roots、selector flags 与 top/raw 全等投影。
function Test-D0M2THarnessControlInventory
{
    param($Contract, $Aggregate, $Cleanup)

    Assert-D0M2TJsonArray $Cleanup.HarnessControlObservations "TT-07 HarnessControlObservations"
    Assert-D0M2TJsonEquivalent $Aggregate.HarnessControlObservations $Cleanup.HarnessControlObservations "Aggregate harness controls"
    $expected = @($Contract.HarnessControlInventory | ForEach-Object { "$([string]$_.CaseId)|$([string]$_.Kind)|$([int]$_.Count)" })
    $actual = @($Aggregate.HarnessControlObservations | Group-Object CaseId, Kind | ForEach-Object { "$([string]$_.Group[0].CaseId)|$([string]$_.Group[0].Kind)|$([int]$_.Count)" })
    Assert-D0M2TExactSet $actual $expected "Harness control inventory"
    $createdRoots = @($Cleanup.CreatedRoots | ForEach-Object { [IO.Path]::GetFullPath([string]$_) })
    Assert-D0M2TExactSet -Actual @($Cleanup.RemovedRoots | ForEach-Object { [IO.Path]::GetFullPath([string]$_) }) -Expected $createdRoots -Context "Harness created/removed roots"
    Assert-D0M2TEvidenceCondition -Condition ($createdRoots.Count -eq 6 -and @($Cleanup.ResidualRoots).Count -eq 0) -Message "Harness owner-root closure is not six removed roots."
    foreach ($row in @($Aggregate.HarnessControlObservations))
    {
        $closed = $row.Observed -eq $true -and $row.RemovedAfterCleanup -eq $true -and [string]$row.ConsumerAuthority -ceq "None" -and [int]$row.UnityConsumerAuthority -eq 0 -and $row.NeverFallbackSelector -eq $true
        Assert-D0M2TEvidenceCondition -Condition ($closed -and (Test-D0M2TPathWithinRoots ([string]$row.Path) $createdRoots)) -Message "Harness control is not observed, removed, selector-free, or owner-contained."
    }
    $ownerRows = @($Aggregate.HarnessControlObservations | Where-Object Kind -CEQ "OwnerSentinel")
    $ownerPaths = @($createdRoots | ForEach-Object { (Join-Path $_ ".d0m2t-tarball-owner").Replace('\', '/') })
    Assert-D0M2TExactSet -Actual @($ownerRows | ForEach-Object { ([string]$_.Path).Replace('\', '/') }) -Expected $ownerPaths -Context "Owner sentinel controls"
}

# 返回唯一 Case/Kind harness row，避免路径模板检查吞掉重复实例。
function Get-D0M2TUniqueHarnessControl
{
    param($Aggregate, [string]$CaseId, [string]$Kind)

    $rows = @($Aggregate.HarnessControlObservations | Where-Object { $_.CaseId -ceq $CaseId -and $_.Kind -ceq $Kind })
    Assert-D0M2TEvidenceCondition -Condition ($rows.Count -eq 1) -Message "Harness control $CaseId/$Kind is not unique."
    return $rows[0]
}

# 按 runner 创建模板绑定 23 项路径，并把目录 controls 交叉到 TT06/TT07 typed raw。
function Test-D0M2THarnessPathTemplates
{
    param($Aggregate, $Boundary, $Cache, $Watcher, $Bee, $Cleanup)

    $manifest = @($Aggregate.RoleObservations | Where-Object ConsumerAuthority -CEQ "SoleUnitySelector")[0]
    $manifestPath = ([string]$manifest.Path).Replace('\', '/'); $projectPath = $manifestPath.Substring(0, $manifestPath.Length - "/Packages/manifest.json".Length)
    $mainRoot = $projectPath.Substring(0, $projectPath.LastIndexOf('/')); $mainControl = "$mainRoot/Control"
    $faultOwner = Get-D0M2TUniqueHarnessControl $Aggregate "TT-04" "OwnerSentinel"
    $faultRoot = ([IO.Path]::GetDirectoryName([string]$faultOwner.Path)).Replace('\', '/'); $faultControl = "$faultRoot/Control"
    $fixed = @(
        @("TT-02", "ManifestAControl", "$mainControl/manifest-A.json"), @("TT-02", "ManifestBControl", "$mainControl/manifest-B.json"),
        @("TT-02", "SelectorWorker", "$mainControl/selector-worker.ps1"),
        @("TT-04", "ManifestAControl", "$faultControl/manifest-A.json"), @("TT-04", "ManifestBControl", "$faultControl/manifest-B.json"),
        @("TT-04", "MissingAuthorityHolding", "$faultControl/selected-authority.missing"),
        @("TT-06", "StaleCacheCanary", "$projectPath/Library/PackageCache/com.exhard.exgas.d0m2f-tarball@d0m2t-stale-a"),
        @("TT-06", "StaleRspCanary", "$projectPath/Library/Bee/d0m2t-stale-a.rsp")
    )
    foreach ($item in $fixed)
    {
        $row = Get-D0M2TUniqueHarnessControl $Aggregate $item[0] $item[1]
        Assert-D0M2TEvidenceCondition -Condition (([string]$row.Path).Replace('\', '/') -ceq [string]$item[2]) -Message "Harness path template drift: $($item[0])/$($item[1])."
    }
    foreach ($item in @(@("TT-01", $mainControl), @("TT-04", $faultControl)))
    {
        $watcherControl = Get-D0M2TUniqueHarnessControl $Aggregate $item[0] "WatcherCanary"
        $pattern = '^' + [regex]::Escape(([string]$item[1]).Replace('\', '/')) + '/watcher-canary-[0-9a-f]{32}$'
        Assert-D0M2TEvidenceCondition -Condition (([string]$watcherControl.Path).Replace('\', '/') -cmatch $pattern) -Message "Harness watcher path template drift."
    }
    $tt01Canary = Get-D0M2TUniqueHarnessControl $Aggregate "TT-01" "WatcherCanary"
    Assert-D0M2TEvidenceCondition -Condition (([string]$tt01Canary.Path).Replace('\', '/') -ceq ([string]$Watcher.CanaryPath).Replace('\', '/')) -Message "TT-01 watcher control is not bound to watcher raw."
    $hardlink = @($Boundary.Attacks | Where-Object Attack -CEQ "Hardlink")[0]
    $external = Get-D0M2TUniqueHarnessControl $Aggregate "TT-07" "ExternalTargetCanary"
    $alias = Get-D0M2TUniqueHarnessControl $Aggregate "TT-07" "HardlinkAttack"
    $reparseTarget = Get-D0M2TUniqueHarnessControl $Aggregate "TT-07" "ReparseTargetControl"; $reparse = Get-D0M2TUniqueHarnessControl $Aggregate "TT-07" "ReparseAttack"
    $forged = Get-D0M2TUniqueHarnessControl $Aggregate "TT-07" "ForgedSentinelAttack"
    $externalRoot = ([string]$hardlink.ExternalTargetRoot).Replace('\', '/'); $hardlinkRoot = ([IO.Path]::GetDirectoryName([string]$alias.Path)).Replace('\', '/')
    $junctionRoot = ([IO.Path]::GetDirectoryName([string]$reparse.Path)).Replace('\', '/'); $sentinelRoot = ([IO.Path]::GetDirectoryName([string]$forged.Path)).Replace('\', '/')
    Assert-D0M2TEvidenceCondition -Condition (([string]$external.Path).Replace('\', '/') -ceq "$externalRoot/external-target.bin" -and ([string]$alias.Path).Replace('\', '/') -ceq "$hardlinkRoot/alias.bin") -Message "Hardlink control path binding drift."
    Assert-D0M2TEvidenceCondition -Condition (([string]$reparseTarget.Path).Replace('\', '/') -ceq "$junctionRoot/junction-target" -and ([string]$reparse.Path).Replace('\', '/') -ceq "$junctionRoot/junction-alias") -Message "Reparse control path binding drift."
    $forgedOwners = @($Aggregate.HarnessControlObservations | Where-Object { $_.Kind -ceq "OwnerSentinel" -and ([string]$_.Path).Replace('\', '/') -ceq ([string]$forged.Path).Replace('\', '/') })
    Assert-D0M2TEvidenceCondition -Condition ($forgedOwners.Count -eq 1) -Message "Forged sentinel is not bound to an owner sentinel lifecycle."
    $expectedRoots = @($mainRoot, $faultRoot, $hardlinkRoot, $externalRoot, $junctionRoot, $sentinelRoot)
    Assert-D0M2TExactSet -Actual @($Cleanup.CreatedRoots | ForEach-Object { ([string]$_).Replace('\', '/') }) -Expected $expectedRoots -Context "Harness path-template owner roots"
    $stale = Get-D0M2TUniqueHarnessControl $Aggregate "TT-06" "StaleCacheCanary"
    $stalePath = ([string]$stale.Path).Replace('\', '/')
    $staleBeforeCount = @($Cache.Before.CacheEntries | Where-Object { ([string]$_.Path).Replace('\', '/') -ceq $stalePath }).Count
    $staleAfterCount = @($Cache.After.CacheEntries | Where-Object { ([string]$_.Path).Replace('\', '/') -ceq $stalePath }).Count
    Assert-D0M2TEvidenceCondition -Condition ($stalePath -ceq ([string]$Cache.StaleAPath).Replace('\', '/') -and $staleBeforeCount -eq 1 -and $staleAfterCount -le 1 -and ($staleAfterCount -eq 1) -eq $Cache.StaleAInAfterSnapshot) -Message "Stale cache directory control is not bound to TT-06 snapshots."
    $staleRsp = Get-D0M2TUniqueHarnessControl $Aggregate "TT-06" "StaleRspCanary"; $rspPath = ([string]$staleRsp.Path).Replace('\', '/')
    Assert-D0M2TEvidenceCondition -Condition ($rspPath.EndsWith('/' + ([string]$Bee.CanaryPath).Replace('\', '/'), [StringComparison]::Ordinal) -and [string]$staleRsp.ObservedSha256 -ceq [string]$Bee.CanarySha256) -Message "Stale rsp control is not bound to TT-06 Bee raw."
    $reparseAttack = @($Boundary.Attacks | Where-Object Attack -CEQ "ReparsePoint")
    Assert-D0M2TEvidenceCondition -Condition ($reparseAttack.Count -eq 1 -and $reparseAttack[0].Refused -eq $true -and $reparseAttack[0].Cleaned -eq $true) -Message "Reparse directory controls are not bound to TT-07 raw."
}

# 绑定 owner、manifest、watcher、fault、cache 与 boundary controls 的可独立重建 bytes。
function Test-D0M2THarnessKnownBytes
{
    param($Contract, $Aggregate, $Boundary)

    $rows = @($Aggregate.HarnessControlObservations)
    foreach ($row in @($rows | Where-Object Kind -CEQ "OwnerSentinel"))
    {
        $root = [IO.Path]::GetDirectoryName([string]$row.Path); $text = [IO.Path]::GetFileName($root) + "|" + [string]$Aggregate.RunId
        $binding = Get-D0M2TTextByteBinding $text $true; Assert-D0M2TObservedBytes $row $binding.Length $binding.Sha256 "Owner sentinel"
    }
    foreach ($generation in @("A", "B"))
    {
        $archive = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ $generation)[0]
        $binding = Get-D0M2TTextByteBinding (Get-D0M2TManifestText ([string]$archive.FileName)) $false
        foreach ($row in @($rows | Where-Object Kind -CEQ ("Manifest" + $generation + "Control"))) { Assert-D0M2TObservedBytes $row $binding.Length $binding.Sha256 "Manifest $generation control" }
    }
    foreach ($pair in @(@("WatcherCanary", "canary", $false), @("StaleRspCanary", "d0m2f-tarball-generation-a|suite-owned-stale-rsp", $true), @("ExternalTargetCanary", "boundary-canary", $true), @("HardlinkAttack", "boundary-canary", $true), @("ForgedSentinelAttack", "forged", $false)))
    {
        $binding = Get-D0M2TTextByteBinding ([string]$pair[1]) ([bool]$pair[2])
        foreach ($row in @($rows | Where-Object Kind -CEQ [string]$pair[0])) { Assert-D0M2TObservedBytes $row $binding.Length $binding.Sha256 ([string]$pair[0]) }
    }
    $archiveB = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ "B")[0]
    foreach ($row in @($rows | Where-Object Kind -CEQ "MissingAuthorityHolding")) { Assert-D0M2TObservedBytes $row ([long]$archiveB.Length) ([string]$archiveB.Sha256) "Missing authority holding" }
    $directoryKinds = @($Contract.HarnessDirectoryKinds | ForEach-Object { [string]$_ })
    foreach ($row in @($rows | Where-Object { [string]$_.Kind -cin $directoryKinds })) { Assert-D0M2TObservedBytes $row 0 "" "Directory control" }
    $workerBinding = Get-D0M2TTextByteBinding (Get-D0M2TSelectorWorkerText) $true
    foreach ($row in @($rows | Where-Object Kind -CEQ "SelectorWorker")) { Assert-D0M2TObservedBytes $row $workerBinding.Length $workerBinding.Sha256 "Selector worker" }
    $hardlink = @($Boundary.Attacks | Where-Object Attack -CEQ "Hardlink")[0]
    $targetRows = @($rows | Where-Object Kind -CEQ "ExternalTargetCanary"); $aliasRows = @($rows | Where-Object Kind -CEQ "HardlinkAttack")
    Assert-D0M2TEvidenceCondition -Condition ($targetRows.Count -eq 1 -and $aliasRows.Count -eq 1 -and [string]$targetRows[0].ObservedSha256 -ceq [string]$hardlink.ExternalShaBefore -and [string]$aliasRows[0].ObservedSha256 -ceq [string]$hardlink.ExternalShaBefore) -Message "Boundary control bytes are not bound to TT-07 raw."
}

# 统一闭合 transient transaction/recovery 与 harness/fault controls，不将其污染进持久七角色。
function Test-D0M2TExperimentObjectClosure
{
    param($Contract, $Aggregate, $EvidenceIndex)

    Test-D0M2TTransientArtifactClosure $Contract $Aggregate $EvidenceIndex
    $cleanup = Get-D0M2TTypedEvidence $EvidenceIndex "TT-07-cleanup-record" "TT-07" "CleanupRecord" "D0M2T-Cleanup-v1" ([string]$Aggregate.RunId)
    $boundary = Get-D0M2TTypedEvidence $EvidenceIndex "TT-07-boundary-record" "TT-07" "FilesystemBoundaryRecord" "D0M2T-BoundaryCleanup-v1" ([string]$Aggregate.RunId)
    $cache = Get-D0M2TTypedEvidence $EvidenceIndex "TT-06-cache-snapshot" "TT-06" "CacheSnapshot" "D0M2T-StaleCache-v1" ([string]$Aggregate.RunId)
    $watcher = Get-D0M2TTypedEvidence $EvidenceIndex "TT-01-authority-watcher" "TT-01" "AuthorityWatcherLog" "D0M2T-AuthorityWatcher-v1" ([string]$Aggregate.RunId)
    $bee = Get-D0M2TTypedEvidence $EvidenceIndex "TT-06-bee-rsp-snapshot" "TT-06" "BeeRspSnapshot" "D0M2T-StaleBeeRsp-v1" ([string]$Aggregate.RunId)
    Test-D0M2THarnessControlInventory $Contract $Aggregate $cleanup
    Test-D0M2THarnessPathTemplates $Aggregate $boundary $cache $watcher $bee $cleanup
    Test-D0M2THarnessKnownBytes $Contract $Aggregate $boundary
}

# 重算 TT-01 baseline、authority 不变性、watcher 与顶层 authority/Unity 绑定。
function Test-D0M2TTT01Semantic
{
    param($Aggregate, $EvidenceIndex)

    $runId = [string]$Aggregate.RunId
    $authority = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-01-authority-snapshot" -CaseId "TT-01" -Kind "AuthoritySnapshot" -Schema "D0M2T-BaselineAuthority-v1" -RunId $runId
    $watcher = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-01-authority-watcher" -CaseId "TT-01" -Kind "AuthorityWatcherLog" -Schema "D0M2T-AuthorityWatcher-v1" -RunId $runId
    $baseline = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-01-baseline-a-unity" -CaseId "TT-01" -Kind "UnityObservation"
    $process = Get-D0M2TUnityProcess -EvidenceIndex $EvidenceIndex -EvidenceId "TT-01-baseline-a-process" -CaseId "TT-01" -Name "baseline-a" -RunId $runId
    $beforeBindings = @(Test-D0M2TArchiveRows -Rows $authority.Before -Context "TT-01 authority before")
    $afterBindings = @(Test-D0M2TArchiveRows -Rows $authority.After -Context "TT-01 authority after")
    $topBindings = @(Test-D0M2TArchiveRows -Rows $Aggregate.Authority.Archives -Context "Aggregate authority archives")
    Assert-D0M2TExactSet -Actual $afterBindings -Expected $beforeBindings -Context "TT-01 immutable authority before/after"
    Assert-D0M2TExactSet -Actual $topBindings -Expected $afterBindings -Context "Aggregate authority binding"
    $archiveA = @($Aggregate.Authority.Archives | Where-Object GenerationId -CEQ "A")[0]
    Test-D0M2TUnityPayload -Payload $baseline -Generation "A" -ArchiveSha ([string]$archiveA.ExpectedSha256) -Context "TT-01 baseline A"
    Assert-D0M2TJsonEquivalent -Actual $Aggregate.Unity.BaselineA -Expected $baseline -Context "Aggregate Unity.BaselineA"
    Assert-D0M2TJsonEquivalent -Actual $authority.BaselineA.Assemblies -Expected $baseline.Assemblies -Context "TT-01 BaselineA assemblies"
    Assert-D0M2TJsonEquivalent -Actual $authority.BaselineB.Assemblies -Expected $Aggregate.Unity.RestartB.Assemblies -Context "TT-01 BaselineB assemblies"
    Assert-D0M2TBooleanProperties -Object $authority -Names @("CanaryObserved") -Expected $true -Context "TT-01 authority"
    Assert-D0M2TBooleanProperties -Object $authority.BaselineA -Names @("AllA", "Passed") -Expected $true -Context "TT-01 BaselineA"
    Assert-D0M2TBooleanProperties -Object $authority.BaselineB -Names @("AllB", "Passed") -Expected $true -Context "TT-01 BaselineB"
    Assert-D0M2TBooleanProperties -Object $watcher -Names @("CanaryObserved") -Expected $true -Context "TT-01 watcher"
    Test-D0M2TWatcherEvents -Aggregate $Aggregate -AuthorityRecord $authority -WatcherRecord $watcher -Context "TT-01"
    Assert-D0M2TEvidenceCondition -Condition ([string]$Aggregate.Authority.PackageName -ceq "com.exhard.exgas.d0m2f-tarball" -and [int]$Aggregate.Authority.AuthorityWrites -eq 0) -Message "aggregate authority identity/writes drift."
    Assert-D0M2TEvidenceCondition -Condition ([int]$authority.AuthorityWrites -eq 0 -and $authority.CanaryObserved -eq $true -and [int]$authority.WatcherErrors -eq 0) -Message "TT-01 authority summary failed."
    Assert-D0M2TEvidenceCondition -Condition ([int]$authority.BaselineA.ExitCode -eq 0 -and $authority.BaselineA.AllA -eq $true -and $authority.BaselineA.Passed -eq $true) -Message "TT-01 baseline A summary failed."
    Assert-D0M2TEvidenceCondition -Condition ([int]$authority.BaselineB.ExitCode -eq 0 -and $authority.BaselineB.AllB -eq $true -and $authority.BaselineB.Passed -eq $true) -Message "TT-01 baseline B summary failed."
    Assert-D0M2TEvidenceCondition -Condition ($watcher.CanaryObserved -eq $true -and [int]$watcher.AuthorityWrites -eq 0 -and @($watcher.Errors).Count -eq 0) -Message "TT-01 watcher failed."
    Assert-D0M2TEvidenceCondition -Condition ([int]$process.ExitCode -eq 0 -and $process.TimedOut -eq $false -and $process.KilledOnResolve -eq $false) -Message "TT-01 Unity process failed."
}

# 验证 selector kill 相位的完整 A/B bytes、强杀点与 aggregate selector 绑定。
function Test-D0M2TSelectorPhase
{
    param($Phase, [string]$ExpectedPhase, [string]$ExpectedSha, [bool]$ExpectA)

    foreach ($field in @("Phase", "CheckpointObserved", "Killed", "ManifestSha256", "IsManifestA", "IsManifestB", "Parseable", "Length"))
    {
        [void](Get-D0M2TRequiredProperty -Object $Phase -Name $field -Context "TT-02 $ExpectedPhase")
    }
    Assert-D0M2TEvidenceCondition -Condition ([string]$Phase.Phase -ceq $ExpectedPhase -and $Phase.CheckpointObserved -eq $true -and $Phase.Killed -eq $true -and $Phase.Parseable -eq $true) -Message "TT-02 $ExpectedPhase kill/checkpoint failed."
    Assert-D0M2TEvidenceCondition -Condition ([string]$Phase.ManifestSha256 -ceq $ExpectedSha -and [long]$Phase.Length -gt 0) -Message "TT-02 $ExpectedPhase manifest bytes drift."
    Assert-D0M2TBooleanProperties -Object $Phase -Names @("CheckpointObserved", "Killed", "Parseable") -Expected $true -Context "TT-02 $ExpectedPhase"
    Assert-D0M2TEvidenceCondition -Condition ($Phase.IsManifestA -is [bool] -and $Phase.IsManifestB -is [bool] -and $Phase.IsManifestA -eq $ExpectA -and $Phase.IsManifestB -eq (-not $ExpectA)) -Message "TT-02 $ExpectedPhase generation classification failed."
}

# 重算 TT-02 selector 原子替换并交叉 worker 独立进程记录。
function Test-D0M2TTT02Semantic
{
    param($Aggregate, $EvidenceIndex)

    $runId = [string]$Aggregate.RunId
    $record = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-02-manifest-snapshot" -CaseId "TT-02" -Kind "ManifestSnapshot" -Schema "D0M2T-SelectorAtomicKill-v1" -RunId $runId
    $worker = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-02-worker-process" -CaseId "TT-02" -Kind "ProcessLog" -Schema "D0M2T-SelectorWorkerProcess-v1" -RunId $runId
    $shaA = [string]$record.ManifestABytesSha256
    $shaB = [string]$record.ManifestBBytesSha256
    $indexA = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ "A")[0]
    $indexB = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ "B")[0]
    $expectedManifestA = Get-D0M2TManifestSha256 -FileName ([string]$indexA.FileName)
    $expectedManifestB = Get-D0M2TManifestSha256 -FileName ([string]$indexB.FileName)
    Assert-D0M2TEvidenceCondition -Condition ($shaA -ceq $expectedManifestA -and $shaB -ceq $expectedManifestB -and $shaA -cne $shaB) -Message "TT-02 selector hashes are not bound to ArchiveIndex manifests."
    Test-D0M2TSelectorPhase -Phase $record.BeforeReplace -ExpectedPhase "BeforeReplace" -ExpectedSha $shaA -ExpectA $true
    Test-D0M2TSelectorPhase -Phase $record.AfterReplace -ExpectedPhase "AfterReplace" -ExpectedSha $shaB -ExpectA $false
    Assert-D0M2TBooleanProperties -Object $record -Names @("CompleteBytesOnly", "BothKillsObserved") -Expected $true -Context "TT-02 selector"
    Assert-D0M2TJsonEquivalent -Actual $worker.BeforeReplace -Expected $record.BeforeReplace -Context "TT-02 worker BeforeReplace"
    Assert-D0M2TJsonEquivalent -Actual $worker.AfterReplace -Expected $record.AfterReplace -Context "TT-02 worker AfterReplace"
    Assert-D0M2TEvidenceCondition -Condition ($record.CompleteBytesOnly -is [bool] -and $record.CompleteBytesOnly -eq $true -and $record.BothKillsObserved -is [bool] -and $record.BothKillsObserved -eq $true) -Message "TT-02 atomic kill summary failed."
    Assert-D0M2TEvidenceCondition -Condition ([string]$Aggregate.Selector.Path -ceq "Packages/manifest.json" -and [string]$Aggregate.Selector.ManifestAHash -ceq $shaA -and [string]$Aggregate.Selector.ManifestBHash -ceq $shaB) -Message "aggregate selector is not bound to TT-02."
    Assert-D0M2TEvidenceCondition -Condition ([string]$Aggregate.Selector.FinalGeneration -ceq "B" -and [int]$Aggregate.Selector.AuditAuthority -eq 0) -Message "aggregate selector final/audit role drift."
}

# 重算 TT-03 resolve 强杀、无结果窗口、B 重启和三程序集恢复。
function Test-D0M2TTT03Semantic
{
    param($Aggregate, $EvidenceIndex)

    $runId = [string]$Aggregate.RunId
    $summary = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-03-recovery-summary" -CaseId "TT-03" -Kind "ProcessLog" -Schema "D0M2T-ResolveKillRecovery-v1" -RunId $runId
    $kill = Get-D0M2TUnityProcess -EvidenceIndex $EvidenceIndex -EvidenceId "TT-03-resolve-kill-b-process" -CaseId "TT-03" -Name "resolve-kill-b" -RunId $runId
    $restart = Get-D0M2TUnityProcess -EvidenceIndex $EvidenceIndex -EvidenceId "TT-03-restart-b-process" -CaseId "TT-03" -Name "restart-b" -RunId $runId
    $payload = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-03-restart-b-unity" -CaseId "TT-03" -Kind "UnityObservation"
    $archiveB = @($Aggregate.Authority.Archives | Where-Object GenerationId -CEQ "B")[0]
    Test-D0M2TUnityPayload -Payload $payload -Generation "B" -ArchiveSha ([string]$archiveB.ExpectedSha256) -Context "TT-03 restart B"
    Assert-D0M2TBooleanProperties -Object $summary -Names @("Killed", "RestartAllB") -Expected $true -Context "TT-03 summary"
    Assert-D0M2TBooleanProperties -Object $summary -Names @("ResultExistedAtKill", "KillTimedOut", "RestartTimedOut") -Expected $false -Context "TT-03 summary"
    Assert-D0M2TJsonEquivalent -Actual $Aggregate.Unity.RestartB -Expected $payload -Context "Aggregate Unity.RestartB"
    Assert-D0M2TEvidenceCondition -Condition ($kill.KilledOnResolve -eq $true -and $kill.ResultExistedAtKill -eq $false -and $kill.TimedOut -eq $false) -Message "TT-03 resolve kill window was not observed."
    Assert-D0M2TEvidenceCondition -Condition ([int]$restart.ExitCode -eq 0 -and $restart.TimedOut -eq $false -and $restart.KilledOnResolve -eq $false) -Message "TT-03 restart process failed."
    Assert-D0M2TEvidenceCondition -Condition ($summary.Killed -eq $true -and $summary.ResultExistedAtKill -eq $false -and $summary.KillTimedOut -eq $false) -Message "TT-03 recovery summary kill fields failed."
    Assert-D0M2TEvidenceCondition -Condition ([int]$summary.RestartExitCode -eq 0 -and $summary.RestartTimedOut -eq $false -and $summary.RestartAllB -eq $true) -Message "TT-03 recovery summary restart fields failed."
    foreach ($field in @("Passed", "IdentityMatches", "AssemblySetMatches", "AllGeneration", "CacheMatches"))
    {
        Assert-D0M2TEvidenceCondition -Condition ((Get-D0M2TRequiredProperty -Object $summary.RestartValidation -Name $field -Context "TT-03 RestartValidation") -eq $true) -Message "TT-03 RestartValidation.$field failed."
    }
    Assert-D0M2TEvidenceCondition -Condition ($summary.Killed -eq $kill.KilledOnResolve -and $summary.ResultExistedAtKill -eq $kill.ResultExistedAtKill -and [int]$summary.RestartExitCode -eq [int]$restart.ExitCode) -Message "TT-03 summary/process cross-binding failed."
}

# 重算 TT-04 warm-cache 下 authority 缺失/损坏均 fail-closed，且 B bytes 被恢复。
function Test-D0M2TTT04Semantic
{
    param($Aggregate, $EvidenceIndex)

    $runId = [string]$Aggregate.RunId
    $fault = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-04-fault-injection" -CaseId "TT-04" -Kind "FaultInjectionRecord" -Schema "D0M2T-MissingCorruptAuthority-v1" -RunId $runId
    $warmProcess = Get-D0M2TUnityProcess -EvidenceIndex $EvidenceIndex -EvidenceId "TT-04-warm-b-process" -CaseId "TT-04" -Name "warm-b" -RunId $runId
    $missingProcess = Get-D0M2TUnityProcess -EvidenceIndex $EvidenceIndex -EvidenceId "TT-04-missing-b-process" -CaseId "TT-04" -Name "missing-b" -RunId $runId
    $corruptProcess = Get-D0M2TUnityProcess -EvidenceIndex $EvidenceIndex -EvidenceId "TT-04-corrupt-b-process" -CaseId "TT-04" -Name "corrupt-b" -RunId $runId
    $warmPayload = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-04-warm-b-unity" -CaseId "TT-04" -Kind "UnityObservation"
    $archiveB = @($Aggregate.Authority.Archives | Where-Object GenerationId -CEQ "B")[0]
    Test-D0M2TUnityPayload -Payload $warmPayload -Generation "B" -ArchiveSha ([string]$archiveB.ExpectedSha256) -Context "TT-04 warm B"
    Assert-D0M2TBooleanProperties -Object $fault -Names @("WarmBaselineB", "AuthorityRestored") -Expected $true -Context "TT-04 fault"
    Assert-D0M2TBooleanProperties -Object $fault -Names @("CacheFallbackAccepted") -Expected $false -Context "TT-04 fault"
    Assert-D0M2TBooleanProperties -Object $fault.Missing -Names @("SuccessfulObservation") -Expected $false -Context "TT-04 Missing"
    Assert-D0M2TBooleanProperties -Object $fault.Corrupt -Names @("SuccessfulObservation") -Expected $false -Context "TT-04 Corrupt"
    Assert-D0M2TEvidenceCondition -Condition ($fault.WarmBaselineB -eq $true -and $fault.CacheFallbackAccepted -eq $false -and $fault.AuthorityRestored -eq $true) -Message "TT-04 fail-closed/restoration summary failed."
    Assert-D0M2TEvidenceCondition -Condition ($warmProcess.TimedOut -eq $false -and [int]$warmProcess.ExitCode -eq 0 -and $missingProcess.TimedOut -eq $false -and $corruptProcess.TimedOut -eq $false) -Message "TT-04 process baseline/timebox failed."
    Assert-D0M2TEvidenceCondition -Condition ([int]$fault.Missing.ExitCode -eq [int]$missingProcess.ExitCode -and [int]$fault.Missing.ExitCode -ne 0 -and $fault.Missing.SuccessfulObservation -eq $false) -Message "TT-04 missing authority was accepted."
    Assert-D0M2TEvidenceCondition -Condition ([int]$fault.Corrupt.ExitCode -eq [int]$corruptProcess.ExitCode -and [int]$fault.Corrupt.ExitCode -ne 0 -and $fault.Corrupt.SuccessfulObservation -eq $false) -Message "TT-04 corrupt authority was accepted."
    $expectedSha = [string]$fault.Corrupt.ExpectedSha256
    Assert-D0M2TEvidenceCondition -Condition ($expectedSha -ceq [string]$archiveB.ExpectedSha256 -and [string]$fault.Corrupt.InjectedSha256 -cmatch "^[0-9a-f]{64}$" -and [string]$fault.Corrupt.InjectedSha256 -cne $expectedSha) -Message "TT-04 corruption bytes are not bound to authority B."
    foreach ($pair in @(@("Missing", "TT-04-missing-b-unity"), @("Corrupt", "TT-04-corrupt-b-unity")))
    {
        $resultPresent = Get-D0M2TRequiredProperty -Object $fault.($pair[0]) -Name "ResultPresent" -Context "TT-04 $($pair[0])"
        Assert-D0M2TEvidenceCondition -Condition ($resultPresent -is [bool]) -Message "TT-04 $($pair[0]) ResultPresent type drift."
        Assert-D0M2TEvidenceCondition -Condition ($EvidenceIndex.ById.ContainsKey($pair[1]) -eq $resultPresent) -Message "TT-04 $($pair[0]) result declaration drift."
        if ($resultPresent)
        {
            $rejectedPayload = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId $pair[1] -CaseId "TT-04" -Kind "UnityObservation"
            Assert-D0M2TEvidenceCondition -Condition ($rejectedPayload.Passed -is [bool] -and $rejectedPayload.Passed -eq $false) -Message "TT-04 $($pair[0]) result claimed success."
        }
    }
}

# 验证 packages-lock 仅记录预期 immutable archive，不具备 selector 权威。
function Test-D0M2TLockObservation
{
    param($Observation, [string]$Generation, [string]$ExpectedArchive, [string]$Context)

    foreach ($field in @("Passed", "Source", "Version", "Sha256", "ExpectedArchive")) { [void](Get-D0M2TRequiredProperty -Object $Observation -Name $field -Context $Context) }
    $normalizedVersion = ([string]$Observation.Version).Replace('\', '/')
    Assert-D0M2TEvidenceCondition -Condition ($Observation.Passed -is [bool] -and $Observation.Passed -eq $true) -Message "$Context did not pass."
    Assert-D0M2TEvidenceCondition -Condition ([string]$Observation.ExpectedArchive -ceq $ExpectedArchive -and $normalizedVersion.EndsWith('/' + $ExpectedArchive, [StringComparison]::OrdinalIgnoreCase)) -Message "$Context archive binding drift."
    Assert-D0M2TEvidenceCondition -Condition ([string]$Observation.Source -cin @("local", "local-tarball") -and [string]$Observation.Sha256 -cmatch "^[0-9a-f]{64}$") -Message "$Context source/hash drift."
}

# 解码现场 ActiveGenerationRef bytes，并复算 SHA、长度、JSON 字段与 audit-only 结论。
function Test-D0M2TAuditObservation
{
    param($Aggregate, $Audit, $UnityFacts, [string]$Context)

    $fields = @("Schema", "RunId", "Path", "Exists", "Length", "Sha256", "RawBytesBase64", "Parseable", "ReadError", "GenerationId", "ArchiveSha256", "Role", "ConsumerAuthority", "RemainedA", "UnityAllB", "Passed")
    Assert-D0M2TExactSet -Actual @($Audit.PSObject.Properties | ForEach-Object Name) -Expected $fields -Context "$Context fields"
    Assert-D0M2TEvidenceCondition -Condition ([string]$Audit.Schema -ceq "D0M2T-AuditRecord-v1" -and [string]$Audit.RunId -ceq [string]$Aggregate.RunId) -Message "$Context identity drift."
    foreach ($name in @("Exists", "Parseable", "RemainedA", "UnityAllB", "Passed")) { Assert-D0M2TEvidenceCondition -Condition ($Audit.$name -is [bool]) -Message "$Context.$name must be boolean." }
    try { $bytes = [Convert]::FromBase64String([string]$Audit.RawBytesBase64) }
    catch { throw "EvidenceIncomplete: $Context RawBytesBase64 is invalid." }
    Assert-D0M2TEvidenceCondition -Condition ($Audit.Exists -eq $true -and [long]$Audit.Length -eq [long]$bytes.LongLength -and [string]$Audit.Sha256 -ceq (Get-D0M2TBytesSha256 $bytes)) -Message "$Context bytes/length/SHA contradict raw bytes."
    try
    {
        $text = ([Text.UTF8Encoding]::new($false, $true)).GetString($bytes)
        $parsed = $text | ConvertFrom-Json -ErrorAction Stop
    }
    catch { throw "EvidenceIncomplete: $Context raw bytes are not strict UTF-8 JSON." }
    Assert-D0M2TEvidenceCondition -Condition ($Audit.Parseable -eq $true -and [string]::IsNullOrEmpty([string]$Audit.ReadError)) -Message "$Context parse summary contradicts raw bytes."
    Assert-D0M2TExactSet -Actual @($parsed.PSObject.Properties | ForEach-Object Name) -Expected @("Schema", "RunId", "GenerationId", "ArchiveSha256", "Role", "ConsumerAuthority") -Context "$Context embedded audit fields"
    $archiveA = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ "A")[0]
    $embedded = @($parsed.Schema, $parsed.RunId, $parsed.GenerationId, $parsed.ArchiveSha256, $parsed.Role, $parsed.ConsumerAuthority) -join "|"
    $reported = @($Audit.Schema, $Audit.RunId, $Audit.GenerationId, $Audit.ArchiveSha256, $Audit.Role, $Audit.ConsumerAuthority) -join "|"
    Assert-D0M2TEvidenceCondition -Condition ($embedded -ceq $reported) -Message "$Context parsed fields contradict reported fields."
    $auditRoles = @($Aggregate.RoleObservations | Where-Object ConsumerAuthority -CEQ "AuditOnly")
    Assert-D0M2TEvidenceCondition -Condition ($auditRoles.Count -eq 1 -and ([string]$Audit.Path).Replace('\', '/') -ceq ([string]$auditRoles[0].Path).Replace('\', '/')) -Message "$Context path is not bound to the persistent audit role."
    $remainedA = [string]$Audit.GenerationId -ceq "A" -and [string]$Audit.ArchiveSha256 -ceq [string]$archiveA.Sha256 -and [string]$Audit.Role -ceq "AuditOnly" -and [string]$Audit.ConsumerAuthority -ceq "None"
    $passed = $remainedA -and $UnityFacts.AllGeneration
    Assert-D0M2TEvidenceCondition -Condition ($Audit.RemainedA -eq $remainedA -and $Audit.UnityAllB -eq $UnityFacts.AllGeneration -and $Audit.Passed -eq $passed) -Message "$Context conclusion contradicts audit/Unity raw."
    return $passed
}

# 重算 TT-05 canonical manifest gate、lock drift 与 audit-only 零权威。
function Test-D0M2TTT05Semantic
{
    param($Aggregate, $EvidenceIndex)

    $runId = [string]$Aggregate.RunId
    $manifest = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-05-manifest-gates" -CaseId "TT-05" -Kind "ManifestSnapshot" -Schema "D0M2T-ManifestGate-v1" -RunId $runId
    $lock = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-05-lock-drift" -CaseId "TT-05" -Kind "PackageLockSnapshot" -Schema "D0M2T-LockDrift-v1" -RunId $runId
    $audit = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-05-audit-zero" -CaseId "TT-05" -Kind "AuditRecordSnapshot" -Schema "D0M2T-AuditRecord-v1" -RunId $runId
    $archiveA = @($Aggregate.Authority.Archives | Where-Object GenerationId -CEQ "A")[0]
    $archiveB = @($Aggregate.Authority.Archives | Where-Object GenerationId -CEQ "B")[0]
    foreach ($field in @("Canonical", "InvalidJson", "Alias", "Escape", "InvalidRejected", "AliasRejected", "EscapeRejected", "FinalAllB")) { [void](Get-D0M2TRequiredProperty -Object $manifest -Name $field -Context "TT-05 manifest") }
    Assert-D0M2TBooleanProperties -Object $manifest -Names @("InvalidRejected", "AliasRejected", "EscapeRejected", "FinalAllB") -Expected $true -Context "TT-05 manifest"
    Assert-D0M2TBooleanProperties -Object $manifest.Canonical -Names @("Accepted") -Expected $true -Context "TT-05 Canonical"
    Assert-D0M2TEvidenceCondition -Condition ($manifest.Canonical.Accepted -eq $true -and [string]$manifest.Canonical.GenerationId -ceq "B" -and [string]$manifest.Canonical.FileName -ceq [string]$archiveB.FileName) -Message "TT-05 canonical B manifest was not admitted."
    foreach ($name in @("InvalidJson", "Alias", "Escape"))
    {
        Assert-D0M2TEvidenceCondition -Condition ($manifest.$name.Accepted -is [bool] -and $manifest.$name.Accepted -eq $false) -Message "TT-05 rejected manifest '$name' was accepted."
    }
    Assert-D0M2TEvidenceCondition -Condition ($manifest.InvalidRejected -eq $true -and $manifest.AliasRejected -eq $true -and $manifest.EscapeRejected -eq $true -and $manifest.FinalAllB -eq $true) -Message "TT-05 manifest gate summary failed."
    Test-D0M2TLockObservation -Observation $lock.StaleA -Generation "A" -ExpectedArchive ([string]$archiveA.FileName) -Context "TT-05 stale lock A"
    Test-D0M2TLockObservation -Observation $lock.FinalB -Generation "B" -ExpectedArchive ([string]$archiveB.FileName) -Context "TT-05 final lock B"
    Assert-D0M2TEvidenceCondition -Condition ($lock.DriftDidNotSelect -is [bool] -and $lock.DriftDidNotSelect -eq $true) -Message "TT-05 lock drift selected generation A."
    $unityFacts = Get-D0M2TUnityPayloadFacts $Aggregate.Unity.RestartB "B" ([string]$archiveB.ExpectedSha256) "TT-05 restart B"
    $auditPassed = Test-D0M2TAuditObservation $Aggregate $audit $unityFacts "TT-05 audit"
    Assert-D0M2TEvidenceCondition -Condition ($auditPassed -and [int]$Aggregate.Selector.AuditAuthority -eq 0) -Message "TT-05 audit failed or acquired selector authority."
}

# 验证 Cache/Bee snapshot 的 schema、run 和固定采集相位。
function Test-D0M2TCacheSnapshot
{
    param($Snapshot, [string]$RunId, [string]$Phase, [string]$Context)

    Assert-D0M2TJsonObject -Value $Snapshot -Context $Context
    Assert-D0M2TEvidenceCondition -Condition ([string]$Snapshot.Schema -ceq "D0M2T-CacheBeeSnapshot-v1" -and [string]$Snapshot.RunId -ceq $RunId -and [string]$Snapshot.Phase -ceq $Phase) -Message "$Context identity drift."
    Assert-D0M2TJsonArray -Value $Snapshot.CacheEntries -Context "$Context CacheEntries"
    Assert-D0M2TJsonArray -Value $Snapshot.RspEntries -Context "$Context RspEntries"
}

# 从 before/after snapshot 与现场 presence 摘要重算 TT-06 stale cache 的唯一 disposition。
function Get-D0M2TStaleCacheFacts
{
    param($Cache, [string]$RunId, [string]$Context)

    $expectedFields = @(
        "Schema", "RunId", "Before", "After", "SourceAPath", "StaleAPath", "StaleAPreseeded", "StaleAStillExists",
        "StaleAInAfterSnapshot", "StaleAPresenceConsistent", "RemainedUnselected", "CleanedBeforeFinal", "StaleADisposition",
        "StaleAResolvedWithoutSelection", "FinalBPath", "FinalUsesDifferentCache"
    )
    Assert-D0M2TExactSet -Actual @($Cache.PSObject.Properties | ForEach-Object Name) -Expected $expectedFields -Context "$Context fields"
    Test-D0M2TCacheSnapshot -Snapshot $Cache.Before -RunId $RunId -Phase "AfterA" -Context "$Context before"
    Test-D0M2TCacheSnapshot -Snapshot $Cache.After -RunId $RunId -Phase "AfterB" -Context "$Context after"
    $booleanFields = @(
        "StaleAPreseeded", "StaleAStillExists", "StaleAInAfterSnapshot", "StaleAPresenceConsistent",
        "RemainedUnselected", "CleanedBeforeFinal", "StaleAResolvedWithoutSelection", "FinalUsesDifferentCache"
    )
    foreach ($field in $booleanFields)
    {
        Assert-D0M2TEvidenceCondition -Condition ($Cache.$field -is [bool]) -Message "$Context.$field must be boolean."
    }
    $sourcePath = ([string]$Cache.SourceAPath).Replace('\', '/')
    $stalePath = ([string]$Cache.StaleAPath).Replace('\', '/')
    $finalPath = ([string]$Cache.FinalBPath).Replace('\', '/')
    $preseeded = -not [string]::IsNullOrWhiteSpace($stalePath) -and @($Cache.Before.CacheEntries | Where-Object { ([string]$_.Path).Replace('\', '/') -ceq $stalePath }).Count -eq 1
    $staleInAfter = -not [string]::IsNullOrWhiteSpace($stalePath) -and @($Cache.After.CacheEntries | Where-Object { ([string]$_.Path).Replace('\', '/') -ceq $stalePath }).Count -eq 1
    $finalInAfter = -not [string]::IsNullOrWhiteSpace($finalPath) -and @($Cache.After.CacheEntries | Where-Object { ([string]$_.Path).Replace('\', '/') -ceq $finalPath }).Count -eq 1
    $differentCache = -not [string]::IsNullOrWhiteSpace($finalPath) -and $finalPath -cne $stalePath
    $presenceConsistent = $Cache.StaleAStillExists -eq $staleInAfter
    $remainedUnselected = $Cache.StaleAStillExists -and $staleInAfter -and $differentCache
    $cleanedBeforeFinal = -not $Cache.StaleAStillExists -and -not $staleInAfter -and $differentCache
    $resolvedWithoutSelection = $remainedUnselected -or $cleanedBeforeFinal
    $disposition = if ($remainedUnselected) { "RemainedUnselected" } elseif ($cleanedBeforeFinal) { "CleanedBeforeFinal" } else { "Unresolved" }
    $summariesMatch = $Cache.StaleAPreseeded -eq $preseeded -and $Cache.StaleAInAfterSnapshot -eq $staleInAfter -and $Cache.FinalUsesDifferentCache -eq $differentCache -and
        $Cache.StaleAPresenceConsistent -eq $presenceConsistent -and $Cache.RemainedUnselected -eq $remainedUnselected -and $Cache.CleanedBeforeFinal -eq $cleanedBeforeFinal -and
        $Cache.StaleAResolvedWithoutSelection -eq $resolvedWithoutSelection -and [string]$Cache.StaleADisposition -ceq $disposition
    Assert-D0M2TEvidenceCondition -Condition $summariesMatch -Message "$Context summary contradicts snapshots/paths/presence."
    return [pscustomobject]@{
        SourcePath = $sourcePath; StalePath = $stalePath; FinalPath = $finalPath; Preseeded = $preseeded; StaleInAfter = $staleInAfter
        FinalInAfter = $finalInAfter; DifferentCache = $differentCache; PresenceConsistent = $presenceConsistent
        RemainedUnselected = $remainedUnselected; CleanedBeforeFinal = $cleanedBeforeFinal; ResolvedWithoutSelection = $resolvedWithoutSelection; Disposition = $disposition
    }
}

# 重算 TT-06 stale cache 的两种合法 disposition、rsp canary 与完整 B 编译图。
function Test-D0M2TTT06Semantic
{
    param($Aggregate, $EvidenceIndex)

    $runId = [string]$Aggregate.RunId
    $cache = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-06-cache-snapshot" -CaseId "TT-06" -Kind "CacheSnapshot" -Schema "D0M2T-StaleCache-v1" -RunId $runId
    $bee = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-06-bee-rsp-snapshot" -CaseId "TT-06" -Kind "BeeRspSnapshot" -Schema "D0M2T-StaleBeeRsp-v1" -RunId $runId
    $payload = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-06-final-unity" -CaseId "TT-06" -Kind "UnityObservation"
    $cacheFacts = Get-D0M2TStaleCacheFacts -Cache $cache -RunId $runId -Context "TT-06 cache"
    Assert-D0M2TEvidenceCondition -Condition (-not [string]::IsNullOrWhiteSpace($cacheFacts.StalePath) -and $cacheFacts.Preseeded -and $cacheFacts.PresenceConsistent -and $cacheFacts.DifferentCache -and $cacheFacts.ResolvedWithoutSelection) -Message "TT-06 stale cache disposition did not resolve without selection."
    Assert-D0M2TEvidenceCondition -Condition ($cacheFacts.Disposition -cin @("RemainedUnselected", "CleanedBeforeFinal")) -Message "TT-06 stale cache disposition is not conclusive."
    Assert-D0M2TEvidenceCondition -Condition ($cacheFacts.SourcePath -ceq ([string]$Aggregate.Unity.BaselineA.ResolvedPackagePath).Replace('\', '/') -and $cacheFacts.SourcePath -cne $cacheFacts.StalePath) -Message "TT-06 stale copy is not bound to Unity A source cache."
    Assert-D0M2TEvidenceCondition -Condition $cacheFacts.FinalInAfter -Message "TT-06 final B cache is absent from after snapshot."
    foreach ($field in @("BeforeRsp", "AfterRsp", "CanaryPath", "CanarySha256", "CanaryContainsGenerationA", "CanaryStillExists", "StaleRspPreexisted", "FinalAllB", "GraphA", "GraphB", "GraphChanged")) { [void](Get-D0M2TRequiredProperty -Object $bee -Name $field -Context "TT-06 BeeRsp") }
    Assert-D0M2TEvidenceCondition -Condition ([string]$bee.CanaryPath -ceq "Library/Bee/d0m2t-stale-a.rsp" -and [string]$bee.CanarySha256 -cmatch "^[0-9a-f]{64}$") -Message "TT-06 rsp canary identity drift."
    Assert-D0M2TBooleanProperties -Object $bee -Names @("CanaryContainsGenerationA", "CanaryStillExists", "StaleRspPreexisted", "FinalAllB", "GraphChanged") -Expected $true -Context "TT-06 BeeRsp"
    Assert-D0M2TEvidenceCondition -Condition ($bee.CanaryContainsGenerationA -eq $true -and $bee.CanaryStillExists -eq $true -and $bee.StaleRspPreexisted -eq $true -and $bee.FinalAllB -eq $true -and $bee.GraphChanged -eq $true) -Message "TT-06 rsp/final generation summary failed."
    foreach ($rows in @($bee.BeforeRsp, $bee.AfterRsp))
    {
        $canary = @($rows | Where-Object Path -CEQ "Library/Bee/d0m2t-stale-a.rsp")
        Assert-D0M2TEvidenceCondition -Condition ($canary.Count -eq 1 -and [string]$canary[0].Sha256 -ceq [string]$bee.CanarySha256 -and $canary[0].ContainsGenerationA -eq $true -and $canary[0].ContainsGenerationB -eq $false) -Message "TT-06 rsp canary snapshot drift."
    }
    $archiveB = @($Aggregate.Authority.Archives | Where-Object GenerationId -CEQ "B")[0]
    Test-D0M2TUnityPayload -Payload $payload -Generation "B" -ArchiveSha ([string]$archiveB.ExpectedSha256) -Context "TT-06 final B"
    Assert-D0M2TJsonEquivalent -Actual $payload -Expected $Aggregate.Unity.RestartB -Context "TT-06 final Unity"
    Assert-D0M2TEvidenceCondition -Condition ($cacheFacts.FinalPath -ceq ([string]$payload.ResolvedPackagePath).Replace('\', '/')) -Message "TT-06 final cache is not bound to Unity raw."
    Assert-D0M2TEvidenceCondition -Condition ([string]$bee.GraphA -ceq [string]$Aggregate.Unity.BaselineA.CompileGraphSha256 -and [string]$bee.GraphB -ceq [string]$Aggregate.Unity.RestartB.CompileGraphSha256) -Message "TT-06 compile graph cross-binding failed."
}

# 将 protected snapshot 约束为合同的 17 路径并返回可比较内容绑定。
function Test-D0M2TProtectedSnapshot
{
    param($Snapshot, $Contract, [string]$RunId, [string]$Phase, [string]$Context)

    Assert-D0M2TEvidenceCondition -Condition ([string]$Snapshot.Schema -ceq "D0M2T-ProtectedSnapshot-v1" -and [string]$Snapshot.RunId -ceq $RunId -and [string]$Snapshot.Phase -ceq $Phase) -Message "$Context identity drift."
    Assert-D0M2TJsonArray -Value $Snapshot.Bindings -Context "$Context Bindings"
    $expectedSpecs = @($Contract.ProtectedPathSpecs | ForEach-Object { "$([string]$_.Kind)|$([string]$_.Path)" })
    $actualSpecs = @($Snapshot.Bindings | ForEach-Object { "$([string]$_.Kind)|$([string]$_.Path)" })
    Assert-D0M2TExactSet -Actual $actualSpecs -Expected $expectedSpecs -Context "$Context protected specs"
    $rows = foreach ($binding in @($Snapshot.Bindings))
    {
        foreach ($field in @("Exists", "Digest", "FileCount")) { [void](Get-D0M2TRequiredProperty -Object $binding -Name $field -Context $Context) }
        Assert-D0M2TEvidenceCondition -Condition ($binding.Exists -is [bool] -and [long]$binding.FileCount -ge 0) -Message "$Context binding type/count drift."
        if ($binding.Exists)
        {
            $countIsValid = if ([string]$binding.Kind -ceq "File") { [long]$binding.FileCount -eq 1 } else { [long]$binding.FileCount -ge 0 }
            Assert-D0M2TEvidenceCondition -Condition ([string]$binding.Digest -cmatch "^[0-9a-f]{64}$" -and $countIsValid) -Message "$Context existing binding digest/count drift."
        }
        else
        {
            Assert-D0M2TEvidenceCondition -Condition ([string]$binding.Digest -ceq "" -and [long]$binding.FileCount -eq 0) -Message "$Context missing binding is not canonical."
        }
        "$([string]$binding.Kind)|$([string]$binding.Path)|$([bool]$binding.Exists)|$([string]$binding.Digest)|$([long]$binding.FileCount)"
    }
    return @($rows)
}

# 重算 TT-07 三类边界拒绝、owned-only cleanup 与 protected before/after 内容相等。
function Test-D0M2TTT07Semantic
{
    param($Contract, $Aggregate, $EvidenceIndex)

    $runId = [string]$Aggregate.RunId
    $boundary = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-07-boundary-record" -CaseId "TT-07" -Kind "FilesystemBoundaryRecord" -Schema "D0M2T-BoundaryCleanup-v1" -RunId $runId
    $cleanup = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-07-cleanup-record" -CaseId "TT-07" -Kind "CleanupRecord" -Schema "D0M2T-Cleanup-v1" -RunId $runId
    $before = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-07-protected-before" -CaseId "TT-07" -Kind "ProtectedSnapshot" -Schema "D0M2T-ProtectedSnapshot-v1" -RunId $runId
    $after = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-07-protected-after" -CaseId "TT-07" -Kind "ProtectedSnapshot" -Schema "D0M2T-ProtectedSnapshot-v1" -RunId $runId
    $closure = Get-D0M2TTypedEvidence -EvidenceIndex $EvidenceIndex -EvidenceId "TT-08-evidence-closure" -CaseId "TT-08" -Kind "EvidenceClosure" -Schema "D0M2T-EvidenceClosure-v1" -RunId $runId
    Assert-D0M2TJsonArray -Value $boundary.Attacks -Context "TT-07 Attacks"
    $attacks = @($boundary.Attacks)
    Assert-D0M2TExactSet -Actual @($attacks | ForEach-Object { [string]$_.Attack }) -Expected @("Hardlink", "ReparsePoint", "ForgedSentinel") -Context "TT-07 attacks"
    foreach ($attack in $attacks)
    {
        Assert-D0M2TBooleanProperties -Object $attack -Names @("Refused", "ExternalUnchanged", "Cleaned") -Expected $true -Context "TT-07 $($attack.Attack)"
    }
    $hardlink = @($attacks | Where-Object Attack -CEQ "Hardlink")[0]
    Assert-D0M2TEvidenceCondition -Condition ([string]$hardlink.ExternalShaBefore -cmatch "^[0-9a-f]{64}$" -and [string]$hardlink.ExternalShaAfter -ceq [string]$hardlink.ExternalShaBefore) -Message "TT-07 hardlink external target changed."
    Assert-D0M2TEvidenceCondition -Condition ($boundary.HardlinkRejected -eq $true -and $boundary.ReparseRejected -eq $true -and $boundary.SentinelRejected -eq $true -and $boundary.ExternalUnchanged -eq $true -and $boundary.CleanupPassed -eq $true -and $boundary.Passed -eq $true) -Message "TT-07 boundary summary failed."
    Assert-D0M2TBooleanProperties -Object $boundary -Names @("HardlinkRejected", "ReparseRejected", "SentinelRejected", "ExternalUnchanged", "CleanupPassed", "Passed") -Expected $true -Context "TT-07 boundary"
    foreach ($field in @("CreatedRoots", "RemovedRoots", "ResidualRoots")) { Assert-D0M2TJsonArray -Value $cleanup.$field -Context "TT-07 cleanup $field" }
    Assert-D0M2TEvidenceCondition -Condition ($cleanup.Requested -eq $true -and $cleanup.AllOwnedOnly -eq $true -and $cleanup.Passed -eq $true -and @($cleanup.ResidualRoots).Count -eq 0) -Message "TT-07 cleanup did not close."
    Assert-D0M2TBooleanProperties -Object $cleanup -Names @("Requested", "AllOwnedOnly", "Passed") -Expected $true -Context "TT-07 cleanup"
    Assert-D0M2TExactSet -Actual @($cleanup.RemovedRoots | ForEach-Object { [string]$_ }) -Expected @($cleanup.CreatedRoots | ForEach-Object { [string]$_ }) -Context "TT-07 created/removed roots"
    foreach ($removedRoot in @($cleanup.RemovedRoots)) { Assert-D0M2TEvidenceCondition -Condition (-not [IO.Directory]::Exists([string]$removedRoot)) -Message "TT-07 removed fixture still exists: $removedRoot" }
    $beforeRows = @(Test-D0M2TProtectedSnapshot -Snapshot $before -Contract $Contract -RunId $runId -Phase "Before" -Context "TT-07 protected before")
    $afterRows = @(Test-D0M2TProtectedSnapshot -Snapshot $after -Contract $Contract -RunId $runId -Phase "After" -Context "TT-07 protected after")
    Assert-D0M2TExactSet -Actual $afterRows -Expected $beforeRows -Context "TT-07 protected content"
    Assert-D0M2TEvidenceCondition -Condition ($Aggregate.Protected.Unchanged -is [bool] -and $Aggregate.Protected.Unchanged -eq $true -and [string]$Aggregate.Protected.BeforeSnapshotEvidenceId -ceq "TT-07-protected-before" -and [string]$Aggregate.Protected.AfterSnapshotEvidenceId -ceq "TT-07-protected-after") -Message "aggregate protected binding drift."
    Assert-D0M2TJsonEquivalent -Actual $Aggregate.Fixture.CreatedRoots -Expected $cleanup.CreatedRoots -Context "Aggregate Fixture.CreatedRoots"
    Assert-D0M2TJsonEquivalent -Actual $Aggregate.Fixture.RemovedRoots -Expected $cleanup.RemovedRoots -Context "Aggregate Fixture.RemovedRoots"
    Assert-D0M2TJsonEquivalent -Actual $Aggregate.Fixture.ResidualRoots -Expected $cleanup.ResidualRoots -Context "Aggregate Fixture.ResidualRoots"
    Assert-D0M2TJsonEquivalent -Actual $closure.FixtureCleanup.CreatedRoots -Expected $cleanup.CreatedRoots -Context "TT-08 FixtureCleanup.CreatedRoots"
    Assert-D0M2TJsonEquivalent -Actual $closure.FixtureCleanup.RemovedRoots -Expected $cleanup.RemovedRoots -Context "TT-08 FixtureCleanup.RemovedRoots"
    Assert-D0M2TJsonEquivalent -Actual $closure.FixtureCleanup.ResidualRoots -Expected $cleanup.ResidualRoots -Context "TT-08 FixtureCleanup.ResidualRoots"
    Assert-D0M2TEvidenceCondition -Condition ($Aggregate.Fixture.Kept -is [bool] -and $Aggregate.Fixture.Kept -eq $false -and $Aggregate.Fixture.CleanupPassed -is [bool] -and $Aggregate.Fixture.CleanupPassed -eq $true) -Message "aggregate fixture cleanup drift."
}

# 校验两类结论性 aggregate 共用的顶层 typed 字段与完整角色闭包。
function Test-D0M2TConclusiveTop
{
    param($Aggregate)

    Assert-D0M2TExactSet -Actual @($Aggregate.Authority.PSObject.Properties | ForEach-Object Name) -Expected @("PackageName", "Archives", "AuthorityWrites") -Context "Aggregate Authority fields"
    Assert-D0M2TExactSet -Actual @($Aggregate.Selector.PSObject.Properties | ForEach-Object Name) -Expected @("Path", "ManifestAHash", "ManifestBHash", "FinalGeneration", "AuditAuthority") -Context "Aggregate Selector fields"
    Assert-D0M2TExactSet -Actual @($Aggregate.Unity.PSObject.Properties | ForEach-Object Name) -Expected @("ExpectedVersion", "BaselineA", "RestartB") -Context "Aggregate Unity fields"
    Assert-D0M2TExactSet -Actual @($Aggregate.Fixture.PSObject.Properties | ForEach-Object Name) -Expected @("Kept", "CreatedRoots", "RemovedRoots", "ResidualRoots", "CleanupPassed") -Context "Aggregate Fixture fields"
    Assert-D0M2TEvidenceCondition -Condition ([string]$Aggregate.Unity.ExpectedVersion -ceq "6000.3.14f1") -Message "aggregate Unity version drift."
    $actualArchiveBindings = @(Get-D0M2TArchiveExpectedBindings -Rows $Aggregate.Authority.Archives -Context "Aggregate authority archives")
    $expectedArchiveBindings = @($script:ValidatedArchiveIndex.Archives | ForEach-Object { "$([string]$_.GenerationId)|$([string]$_.FileName)|$([string]$_.Sha256)|$([long]$_.Length)" })
    Assert-D0M2TExactSet -Actual $actualArchiveBindings -Expected $expectedArchiveBindings -Context "Aggregate ArchiveIndex binding"
    Test-D0M2TFullRoleClosure -Aggregate $Aggregate
}

# 依次执行固定 TT-01..TT-07 Passed predicates 与顶层交叉绑定。
function Test-D0M2TTypedRawSemanticEvidence
{
    param($Contract, $Aggregate, $EvidenceIndex)

    Test-D0M2TConclusiveTop -Aggregate $Aggregate
    Test-D0M2TTT01Semantic -Aggregate $Aggregate -EvidenceIndex $EvidenceIndex
    Test-D0M2TTT02Semantic -Aggregate $Aggregate -EvidenceIndex $EvidenceIndex
    Test-D0M2TTT03Semantic -Aggregate $Aggregate -EvidenceIndex $EvidenceIndex
    Test-D0M2TTT04Semantic -Aggregate $Aggregate -EvidenceIndex $EvidenceIndex
    Test-D0M2TTT05Semantic -Aggregate $Aggregate -EvidenceIndex $EvidenceIndex
    Test-D0M2TTT06Semantic -Aggregate $Aggregate -EvidenceIndex $EvidenceIndex
    Test-D0M2TTT07Semantic -Contract $Contract -Aggregate $Aggregate -EvidenceIndex $EvidenceIndex
}

# 读取固定 case 的唯一 aggregate 记录。
function Get-D0M2TAggregateCase
{
    param($Aggregate, [string]$CaseId)

    $matches = @($Aggregate.Cases | Where-Object CaseId -CEQ $CaseId)
    Assert-D0M2TEvidenceCondition -Condition ($matches.Count -eq 1) -Message "$CaseId is not unique."
    return $matches[0]
}

# 重算 TT-01 的 authority mutation 或 mixed-generation 负 predicate。
function Test-D0M2TNegativeTT01
{
    param($Aggregate, $EvidenceIndex, [string]$Reason)

    $runId = [string]$Aggregate.RunId
    $record = Get-D0M2TTypedEvidence $EvidenceIndex "TT-01-authority-snapshot" "TT-01" "AuthoritySnapshot" "D0M2T-BaselineAuthority-v1" $runId
    $watcher = Get-D0M2TTypedEvidence $EvidenceIndex "TT-01-authority-watcher" "TT-01" "AuthorityWatcherLog" "D0M2T-AuthorityWatcher-v1" $runId
    $beforeBindings = @(Test-D0M2TArchiveRows $record.Before "TT-01 rejected before")
    $afterBindings = @(Get-D0M2TArchiveExpectedBindings $record.After "TT-01 rejected after")
    Assert-D0M2TExactSet $afterBindings $beforeBindings "TT-01 rejected expected authority bindings"
    Assert-D0M2TBooleanProperties $record @("CanaryObserved") $true "TT-01 rejected authority"
    Assert-D0M2TBooleanProperties $watcher @("CanaryObserved") $true "TT-01 rejected watcher"
    Test-D0M2TWatcherEvents $Aggregate $record $watcher "TT-01 rejected"
    Assert-D0M2TEvidenceCondition -Condition (@($watcher.Errors).Count -eq 0 -and [int]$record.WatcherErrors -eq 0) -Message "TT-01 watcher failure must be Inconclusive."
    $baselineFlags = @($record.BaselineA.AllA, $record.BaselineA.Passed, $record.BaselineB.AllB, $record.BaselineB.Passed)
    foreach ($flag in $baselineFlags) { Assert-D0M2TEvidenceCondition -Condition ($flag -is [bool]) -Message "TT-01 baseline flags must be boolean." }
    if ($Reason -ceq "AuthorityMutationObserved")
    {
        Assert-D0M2TEvidenceCondition -Condition ([int]$record.AuthorityWrites -gt 0 -and [int]$watcher.AuthorityWrites -eq [int]$record.AuthorityWrites -and [int]$Aggregate.Authority.AuthorityWrites -eq [int]$record.AuthorityWrites) -Message "TT-01 mutation reason lacks authority writes."
    }
    elseif ($Reason -ceq "MixedGenerationObserved")
    {
        $baselinePassed = @($baselineFlags | Where-Object { $_ -ne $true }).Count -eq 0
        Assert-D0M2TEvidenceCondition -Condition ([int]$record.AuthorityWrites -eq 0 -and -not $baselinePassed) -Message "TT-01 mixed-generation reason lacks a failed baseline."
        [void](Test-D0M2TArchiveRows $record.After "TT-01 mixed-generation authority after")
    }
    else { throw "EvidenceIncomplete: TT-01 failed reason '$Reason' is not a TT-01 predicate." }
}

# 重算 TT-02 selector atomicity 的负 predicate。
function Test-D0M2TNegativeTT02
{
    param($Aggregate, $EvidenceIndex, [string]$Reason)

    Assert-D0M2TEvidenceCondition -Condition ($Reason -ceq "SelectorAtomicityViolation") -Message "TT-02 failed reason mismatch."
    $record = Get-D0M2TTypedEvidence $EvidenceIndex "TT-02-manifest-snapshot" "TT-02" "ManifestSnapshot" "D0M2T-SelectorAtomicKill-v1" ([string]$Aggregate.RunId)
    $worker = Get-D0M2TTypedEvidence $EvidenceIndex "TT-02-worker-process" "TT-02" "ProcessLog" "D0M2T-SelectorWorkerProcess-v1" ([string]$Aggregate.RunId)
    $indexA = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ "A")[0]
    $indexB = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ "B")[0]
    Assert-D0M2TEvidenceCondition -Condition ([string]$record.ManifestABytesSha256 -ceq (Get-D0M2TManifestSha256 ([string]$indexA.FileName)) -and [string]$record.ManifestBBytesSha256 -ceq (Get-D0M2TManifestSha256 ([string]$indexB.FileName))) -Message "TT-02 rejected manifest hashes are not index-bound."
    foreach ($name in @("CompleteBytesOnly", "BothKillsObserved")) { Assert-D0M2TEvidenceCondition -Condition ($record.$name -is [bool]) -Message "TT-02 rejected $name must be boolean." }
    foreach ($phase in @($record.BeforeReplace, $record.AfterReplace))
    {
        foreach ($name in @("CheckpointObserved", "Killed", "IsManifestA", "IsManifestB", "Parseable"))
        {
            $value = Get-D0M2TRequiredProperty $phase $name "TT-02 rejected phase"
            Assert-D0M2TEvidenceCondition -Condition ($value -is [bool]) -Message "TT-02 rejected phase.$name must be boolean."
        }
    }
    Assert-D0M2TJsonEquivalent $worker.BeforeReplace $record.BeforeReplace "TT-02 rejected worker before"
    Assert-D0M2TJsonEquivalent $worker.AfterReplace $record.AfterReplace "TT-02 rejected worker after"
    $derivedKills = $record.BeforeReplace.Killed -eq $true -and $record.AfterReplace.Killed -eq $true
    $derivedComplete = $record.BeforeReplace.Parseable -eq $true -and $record.AfterReplace.Parseable -eq $true -and ($record.BeforeReplace.IsManifestA -or $record.BeforeReplace.IsManifestB) -and ($record.AfterReplace.IsManifestA -or $record.AfterReplace.IsManifestB)
    Assert-D0M2TEvidenceCondition -Condition ($record.BothKillsObserved -eq $derivedKills -and $record.CompleteBytesOnly -eq $derivedComplete) -Message "TT-02 derived summary contradicts phase raw."
    $ready = $record.BeforeReplace.CheckpointObserved -eq $true -and $record.AfterReplace.CheckpointObserved -eq $true -and $derivedKills
    Assert-D0M2TEvidenceCondition -Condition $ready -Message "TT-02 not-ready evidence must be Inconclusive."
    $passed = $derivedComplete -and $derivedKills -and $record.BeforeReplace.IsManifestA -eq $true -and $record.AfterReplace.IsManifestB -eq $true
    Assert-D0M2TEvidenceCondition -Condition (-not $passed) -Message "TT-02 failed status still satisfies atomicity predicate."
}

# 重算 TT-03 restart mixed-generation 的负 predicate，同时保留有效 kill 前置。
function Test-D0M2TNegativeTT03
{
    param($Aggregate, $EvidenceIndex, [string]$Reason)

    Assert-D0M2TEvidenceCondition -Condition ($Reason -ceq "MixedGenerationObserved") -Message "TT-03 failed reason mismatch."
    $runId = [string]$Aggregate.RunId
    $summary = Get-D0M2TTypedEvidence $EvidenceIndex "TT-03-recovery-summary" "TT-03" "ProcessLog" "D0M2T-ResolveKillRecovery-v1" $runId
    $kill = Get-D0M2TUnityProcess $EvidenceIndex "TT-03-resolve-kill-b-process" "TT-03" "resolve-kill-b" $runId
    $restart = Get-D0M2TUnityProcess $EvidenceIndex "TT-03-restart-b-process" "TT-03" "restart-b" $runId
    $payload = Get-D0M2TTypedEvidence $EvidenceIndex "TT-03-restart-b-unity" "TT-03" "UnityObservation" "" ""
    $archiveB = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ "B")[0]
    $facts = Get-D0M2TUnityPayloadFacts $payload "B" ([string]$archiveB.Sha256) "TT-03 rejected restart B"
    $restartPassed = Test-D0M2TRestartValidationBinding $summary $restart $facts "TT-03 rejected"
    Assert-D0M2TEvidenceCondition -Condition ($kill.KilledOnResolve -eq $true -and $kill.ResultExistedAtKill -eq $false -and $kill.TimedOut -eq $false) -Message "TT-03 invalid kill must be Inconclusive."
    foreach ($name in @("Killed", "ResultExistedAtKill", "KillTimedOut", "RestartTimedOut", "RestartAllB")) { Assert-D0M2TEvidenceCondition -Condition ($summary.$name -is [bool]) -Message "TT-03 rejected $name must be boolean." }
    Assert-D0M2TEvidenceCondition -Condition ($summary.Killed -eq $kill.KilledOnResolve -and $summary.ResultExistedAtKill -eq $kill.ResultExistedAtKill -and $summary.KillTimedOut -eq $kill.TimedOut) -Message "TT-03 kill summary contradicts process raw."
    Assert-D0M2TEvidenceCondition -Condition ($restart.TimedOut -eq $false -and $summary.RestartTimedOut -eq $false) -Message "TT-03 not-ready evidence must be Inconclusive."
    Assert-D0M2TEvidenceCondition -Condition (-not $restartPassed) -Message "TT-03 failed status still satisfies recovery predicate."
}

# 重算 TT-04 missing/corrupt authority 的负 predicate与进程退出绑定。
function Test-D0M2TNegativeTT04
{
    param($Aggregate, $EvidenceIndex, [string]$Reason)

    Assert-D0M2TEvidenceCondition -Condition ($Reason -ceq "MissingOrCorruptAuthorityAccepted") -Message "TT-04 failed reason mismatch."
    $runId = [string]$Aggregate.RunId
    $fault = Get-D0M2TTypedEvidence $EvidenceIndex "TT-04-fault-injection" "TT-04" "FaultInjectionRecord" "D0M2T-MissingCorruptAuthority-v1" $runId
    $warm = Get-D0M2TUnityProcess $EvidenceIndex "TT-04-warm-b-process" "TT-04" "warm-b" $runId
    $missing = Get-D0M2TUnityProcess $EvidenceIndex "TT-04-missing-b-process" "TT-04" "missing-b" $runId
    $corrupt = Get-D0M2TUnityProcess $EvidenceIndex "TT-04-corrupt-b-process" "TT-04" "corrupt-b" $runId
    $warmPayload = Get-D0M2TTypedEvidence $EvidenceIndex "TT-04-warm-b-unity" "TT-04" "UnityObservation" "" ""
    $archiveB = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ "B")[0]
    $warmFacts = Get-D0M2TUnityPayloadFacts $warmPayload "B" ([string]$archiveB.Sha256) "TT-04 rejected warm B"
    foreach ($value in @($fault.WarmBaselineB, $fault.Missing.SuccessfulObservation, $fault.Corrupt.SuccessfulObservation, $fault.CacheFallbackAccepted, $fault.AuthorityRestored))
    {
        Assert-D0M2TEvidenceCondition -Condition ($value -is [bool]) -Message "TT-04 rejected predicate flags must be boolean."
    }
    Assert-D0M2TEvidenceCondition -Condition ([int]$fault.Missing.ExitCode -eq [int]$missing.ExitCode -and [int]$fault.Corrupt.ExitCode -eq [int]$corrupt.ExitCode) -Message "TT-04 rejected process exits drift."
    Assert-D0M2TEvidenceCondition -Condition ($warm.TimedOut -eq $false -and $missing.TimedOut -eq $false -and $corrupt.TimedOut -eq $false) -Message "TT-04 timed-out evidence must be Inconclusive."
    Assert-D0M2TEvidenceCondition -Condition ($warm.KilledOnResolve -eq $false -and $missing.KilledOnResolve -eq $false -and $corrupt.KilledOnResolve -eq $false) -Message "TT-04 fault processes did not reach ordinary terminal observations."
    $warmPassed = [int]$warm.ExitCode -eq 0 -and $warmFacts.Passed
    Assert-D0M2TEvidenceCondition -Condition $warmPassed -Message "TT-04 warm baseline must be Passed before a conclusive fault rejection."
    $missingSuccessful = Get-D0M2TObservationSuccess $EvidenceIndex $fault.Missing $missing "TT-04-missing-b-unity" "TT-04 Missing"
    $corruptSuccessful = Get-D0M2TObservationSuccess $EvidenceIndex $fault.Corrupt $corrupt "TT-04-corrupt-b-unity" "TT-04 Corrupt"
    Assert-D0M2TEvidenceCondition -Condition ($fault.WarmBaselineB -eq $warmPassed -and $fault.CacheFallbackAccepted -eq ($missingSuccessful -or $corruptSuccessful)) -Message "TT-04 derived summary contradicts process/Unity raw."
    Assert-D0M2TEvidenceCondition -Condition ([string]$fault.Corrupt.ExpectedSha256 -ceq [string]$archiveB.Sha256 -and [string]$fault.Corrupt.InjectedSha256 -cne [string]$archiveB.Sha256) -Message "TT-04 rejected corruption is not index-bound."
    $passed = $warmPassed -and -not $missingSuccessful -and -not $corruptSuccessful -and $fault.AuthorityRestored
    Assert-D0M2TEvidenceCondition -Condition (-not $passed) -Message "TT-04 failed status still satisfies fail-closed predicate."
}

# 重算 TT-05 manifest/lock/audit 的负 predicate并绑定 restart validation。
function Test-D0M2TNegativeTT05
{
    param($Aggregate, $EvidenceIndex, [string]$Reason)

    Assert-D0M2TEvidenceCondition -Condition ($Reason -ceq "ManifestLockDriftAccepted") -Message "TT-05 failed reason mismatch."
    $runId = [string]$Aggregate.RunId
    $manifest = Get-D0M2TTypedEvidence $EvidenceIndex "TT-05-manifest-gates" "TT-05" "ManifestSnapshot" "D0M2T-ManifestGate-v1" $runId
    $lock = Get-D0M2TTypedEvidence $EvidenceIndex "TT-05-lock-drift" "TT-05" "PackageLockSnapshot" "D0M2T-LockDrift-v1" $runId
    $audit = Get-D0M2TTypedEvidence $EvidenceIndex "TT-05-audit-zero" "TT-05" "AuditRecordSnapshot" "D0M2T-AuditRecord-v1" $runId
    $restart = Get-D0M2TTypedEvidence $EvidenceIndex "TT-03-recovery-summary" "TT-03" "ProcessLog" "D0M2T-ResolveKillRecovery-v1" $runId
    $restartProcess = Get-D0M2TUnityProcess $EvidenceIndex "TT-03-restart-b-process" "TT-03" "restart-b" $runId
    $payload = Get-D0M2TTypedEvidence $EvidenceIndex "TT-03-restart-b-unity" "TT-03" "UnityObservation" "" ""
    $archiveB = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ "B")[0]
    $facts = Get-D0M2TUnityPayloadFacts $payload "B" ([string]$archiveB.Sha256) "TT-05 rejected restart B"
    $restartPassed = Test-D0M2TRestartValidationBinding $restart $restartProcess $facts "TT-05 rejected"
    $auditPassed = Test-D0M2TAuditObservation $Aggregate $audit $facts "TT-05 rejected audit"
    Assert-D0M2TEvidenceCondition -Condition $auditPassed -Message "TT-05 audit must remain a valid A audit before conclusive manifest/lock rejection."
    foreach ($value in @($manifest.Canonical.Accepted, $manifest.InvalidJson.Accepted, $manifest.Alias.Accepted, $manifest.Escape.Accepted, $lock.StaleA.Passed, $lock.FinalB.Passed, $restart.RestartValidation.Passed))
    {
        Assert-D0M2TEvidenceCondition -Condition ($value -is [bool]) -Message "TT-05 rejected predicate flags must be boolean."
    }
    Assert-D0M2TEvidenceCondition -Condition ($manifest.InvalidRejected -eq (-not $manifest.InvalidJson.Accepted) -and $manifest.AliasRejected -eq (-not $manifest.Alias.Accepted) -and $manifest.EscapeRejected -eq (-not $manifest.Escape.Accepted) -and $manifest.FinalAllB -eq $facts.AllGeneration -and $lock.DriftDidNotSelect -eq $facts.AllGeneration) -Message "TT-05 derived summary contradicts gate/Unity raw."
    $passed = $manifest.Canonical.Accepted -eq $true -and $manifest.InvalidJson.Accepted -eq $false -and $manifest.Alias.Accepted -eq $false -and $manifest.Escape.Accepted -eq $false -and $lock.StaleA.Passed -eq $true -and $lock.FinalB.Passed -eq $true -and $restartPassed
    Assert-D0M2TEvidenceCondition -Condition (-not $passed) -Message "TT-05 failed status still satisfies manifest/lock predicate."
}

# 重算 TT-06 cache-selector 或 stale Bee/rsp 的分类负 predicate。
function Test-D0M2TNegativeTT06
{
    param($Aggregate, $EvidenceIndex, [string]$Reason)

    $runId = [string]$Aggregate.RunId
    $cache = Get-D0M2TTypedEvidence $EvidenceIndex "TT-06-cache-snapshot" "TT-06" "CacheSnapshot" "D0M2T-StaleCache-v1" $runId
    $bee = Get-D0M2TTypedEvidence $EvidenceIndex "TT-06-bee-rsp-snapshot" "TT-06" "BeeRspSnapshot" "D0M2T-StaleBeeRsp-v1" $runId
    $restart = Get-D0M2TTypedEvidence $EvidenceIndex "TT-03-recovery-summary" "TT-03" "ProcessLog" "D0M2T-ResolveKillRecovery-v1" $runId
    $restartProcess = Get-D0M2TUnityProcess $EvidenceIndex "TT-03-restart-b-process" "TT-03" "restart-b" $runId
    $payload = Get-D0M2TTypedEvidence $EvidenceIndex "TT-06-final-unity" "TT-06" "UnityObservation" "" ""
    $archiveB = @($script:ValidatedArchiveIndex.Archives | Where-Object GenerationId -CEQ "B")[0]
    $facts = Get-D0M2TUnityPayloadFacts $payload "B" ([string]$archiveB.Sha256) "TT-06 rejected final B"
    $restartPassed = Test-D0M2TRestartValidationBinding $restart $restartProcess $facts "TT-06 rejected"
    $cacheFacts = Get-D0M2TStaleCacheFacts -Cache $cache -RunId $runId -Context "TT-06 rejected cache"
    foreach ($value in @($bee.StaleRspPreexisted, $bee.CanaryStillExists, $bee.CanaryContainsGenerationA, $bee.FinalAllB, $bee.GraphChanged))
    {
        Assert-D0M2TEvidenceCondition -Condition ($value -is [bool]) -Message "TT-06 rejected predicate flags must be boolean."
    }
    Assert-D0M2TEvidenceCondition -Condition ($cacheFacts.FinalInAfter -and $cacheFacts.FinalPath -ceq ([string]$payload.ResolvedPackagePath).Replace('\', '/')) -Message "TT-06 final cache is not bound to Unity raw."
    $beforeCanary = @($bee.BeforeRsp | Where-Object Path -CEQ "Library/Bee/d0m2t-stale-a.rsp")
    $afterCanary = @($bee.AfterRsp | Where-Object Path -CEQ "Library/Bee/d0m2t-stale-a.rsp")
    $canaryValid = $beforeCanary.Count -eq 1 -and $afterCanary.Count -eq 1 -and [string]$beforeCanary[0].Sha256 -ceq [string]$bee.CanarySha256 -and [string]$afterCanary[0].Sha256 -ceq [string]$bee.CanarySha256 -and $afterCanary[0].ContainsGenerationA -eq $true -and $afterCanary[0].ContainsGenerationB -eq $false
    Assert-D0M2TEvidenceCondition -Condition ($bee.StaleRspPreexisted -eq ($beforeCanary.Count -eq 1) -and $bee.CanaryStillExists -eq ($afterCanary.Count -eq 1) -and $bee.CanaryContainsGenerationA -eq $canaryValid) -Message "TT-06 rsp summary contradicts snapshots."
    $graphChanged = -not [string]::IsNullOrWhiteSpace([string]$bee.GraphA) -and -not [string]::IsNullOrWhiteSpace([string]$bee.GraphB) -and [string]$bee.GraphA -cne [string]$bee.GraphB
    Assert-D0M2TEvidenceCondition -Condition ([string]$bee.GraphA -ceq [string]$Aggregate.Unity.BaselineA.CompileGraphSha256 -and [string]$bee.GraphB -ceq [string]$payload.CompileGraphSha256 -and $bee.GraphChanged -eq $graphChanged -and $bee.FinalAllB -eq $facts.AllGeneration) -Message "TT-06 graph/final summary contradicts Unity raw."
    $ready = $facts.IdentityMatches -and $cacheFacts.Preseeded -and $cacheFacts.PresenceConsistent -and -not [string]::IsNullOrWhiteSpace($cacheFacts.FinalPath) -and $beforeCanary.Count -eq 1 -and $afterCanary.Count -eq 1 -and $canaryValid -and -not [string]::IsNullOrWhiteSpace([string]$bee.GraphA) -and -not [string]::IsNullOrWhiteSpace([string]$bee.GraphB)
    Assert-D0M2TEvidenceCondition -Condition $ready -Message "TT-06 not-ready evidence must be Inconclusive."
    if ($Reason -ceq "CacheFallbackSelectorObserved")
    {
        Assert-D0M2TEvidenceCondition -Condition (-not $cacheFacts.DifferentCache) -Message "TT-06 cache reason requires the final path to reuse stale A."
    }
    elseif ($Reason -ceq "StaleBeeOrRspConsumed")
    {
        $terminalFailed = -not $facts.AllGeneration -or -not $graphChanged -or -not $restartPassed
        Assert-D0M2TEvidenceCondition -Condition ($cacheFacts.DifferentCache -and $cacheFacts.ResolvedWithoutSelection -and $terminalFailed) -Message "TT-06 Bee/rsp reason lacks its terminal predicate failure."
    }
    else { throw "EvidenceIncomplete: TT-06 failed reason '$Reason' is not a TT-06 predicate." }
}

# 重算 TT-07 boundary violation，并证明 cleanup/protected 未把结果降为 Inconclusive。
function Test-D0M2TNegativeTT07
{
    param($Contract, $Aggregate, $EvidenceIndex, [string]$Reason)

    Assert-D0M2TEvidenceCondition -Condition ($Reason -ceq "FilesystemBoundaryViolation") -Message "TT-07 failed reason mismatch."
    $runId = [string]$Aggregate.RunId
    $boundary = Get-D0M2TTypedEvidence $EvidenceIndex "TT-07-boundary-record" "TT-07" "FilesystemBoundaryRecord" "D0M2T-BoundaryCleanup-v1" $runId
    $cleanup = Get-D0M2TTypedEvidence $EvidenceIndex "TT-07-cleanup-record" "TT-07" "CleanupRecord" "D0M2T-Cleanup-v1" $runId
    $before = Get-D0M2TTypedEvidence $EvidenceIndex "TT-07-protected-before" "TT-07" "ProtectedSnapshot" "D0M2T-ProtectedSnapshot-v1" $runId
    $after = Get-D0M2TTypedEvidence $EvidenceIndex "TT-07-protected-after" "TT-07" "ProtectedSnapshot" "D0M2T-ProtectedSnapshot-v1" $runId
    $beforeRows = @(Test-D0M2TProtectedSnapshot $before $Contract $runId "Before" "TT-07 rejected protected before")
    $afterRows = @(Test-D0M2TProtectedSnapshot $after $Contract $runId "After" "TT-07 rejected protected after")
    Assert-D0M2TExactSet $afterRows $beforeRows "TT-07 rejected protected content"
    Assert-D0M2TBooleanProperties $cleanup @("Requested", "AllOwnedOnly", "Passed") $true "TT-07 rejected cleanup"
    Assert-D0M2TEvidenceCondition -Condition (@($cleanup.ResidualRoots).Count -eq 0) -Message "TT-07 cleanup failure must be Inconclusive."
    $flags = @($boundary.HardlinkRejected, $boundary.ReparseRejected, $boundary.SentinelRejected, $boundary.ExternalUnchanged, $boundary.CleanupPassed, $boundary.Passed)
    foreach ($flag in $flags) { Assert-D0M2TEvidenceCondition -Condition ($flag -is [bool]) -Message "TT-07 rejected flags must be boolean." }
    Assert-D0M2TEvidenceCondition -Condition (@($flags | Where-Object { $_ -ne $true }).Count -gt 0) -Message "TT-07 failed status still satisfies boundary predicate."
}

# 按 case 身份执行唯一对应的 Passed 正 predicate。
function Test-D0M2TPassedCaseSemantic
{
    param($Contract, $Aggregate, $EvidenceIndex, [string]$CaseId)

    switch ($CaseId)
    {
        "TT-01" { Test-D0M2TTT01Semantic $Aggregate $EvidenceIndex }
        "TT-02" { Test-D0M2TTT02Semantic $Aggregate $EvidenceIndex }
        "TT-03" { Test-D0M2TTT03Semantic $Aggregate $EvidenceIndex }
        "TT-04" { Test-D0M2TTT04Semantic $Aggregate $EvidenceIndex }
        "TT-05" { Test-D0M2TTT05Semantic $Aggregate $EvidenceIndex }
        "TT-06" { Test-D0M2TTT06Semantic $Aggregate $EvidenceIndex }
        "TT-07" { Test-D0M2TTT07Semantic $Contract $Aggregate $EvidenceIndex }
        default { throw "EvidenceIncomplete: no Passed validator for '$CaseId'." }
    }
}

# 按 case 身份执行唯一对应的 Failed 负 predicate与 reason 分类。
function Test-D0M2TFailedCaseSemantic
{
    param($Contract, $Aggregate, $EvidenceIndex, $Case)

    $reason = [string]$Case.Reason
    switch ([string]$Case.CaseId)
    {
        "TT-01" { Test-D0M2TNegativeTT01 $Aggregate $EvidenceIndex $reason }
        "TT-02" { Test-D0M2TNegativeTT02 $Aggregate $EvidenceIndex $reason }
        "TT-03" { Test-D0M2TNegativeTT03 $Aggregate $EvidenceIndex $reason }
        "TT-04" { Test-D0M2TNegativeTT04 $Aggregate $EvidenceIndex $reason }
        "TT-05" { Test-D0M2TNegativeTT05 $Aggregate $EvidenceIndex $reason }
        "TT-06" { Test-D0M2TNegativeTT06 $Aggregate $EvidenceIndex $reason }
        "TT-07" { Test-D0M2TNegativeTT07 $Contract $Aggregate $EvidenceIndex $reason }
        default { throw "EvidenceIncomplete: no Failed validator for '$($Case.CaseId)'." }
    }
}

# 对 RouteRejected 重算所有 Passed/Failed case，拒绝 partial case 混入结论性拒绝。
function Test-D0M2TRouteRejectedSemanticEvidence
{
    param($Contract, $Aggregate, $EvidenceIndex)

    Test-D0M2TConclusiveTop $Aggregate
    $matrixCases = @($Aggregate.Cases | Where-Object CaseId -CNE "TT-08")
    $partial = @($matrixCases | Where-Object Status -CNotIn @("Passed", "Failed"))
    Assert-D0M2TEvidenceCondition -Condition ($partial.Count -eq 0) -Message "RouteRejected cannot contain partial TT-01..TT-07 cases."
    $failed = @($matrixCases | Where-Object Status -CEQ "Failed")
    Assert-D0M2TEvidenceCondition -Condition ($failed.Count -gt 0 -and [string]$Aggregate.Reason -ceq [string]$failed[0].Reason) -Message "RouteRejected reason is not the first failed predicate."
    foreach ($case in $matrixCases)
    {
        if ([string]$case.Status -ceq "Passed") { Test-D0M2TPassedCaseSemantic $Contract $Aggregate $EvidenceIndex ([string]$case.CaseId) }
        else { Test-D0M2TFailedCaseSemantic $Contract $Aggregate $EvidenceIndex $case }
    }
    $tt08 = Get-D0M2TAggregateCase $Aggregate "TT-08"
    Assert-D0M2TEvidenceCondition -Condition ([string]$tt08.Status -ceq "Passed") -Message "RouteRejected requires a Passed TT-08 closure."
}

# 对 Passed/RouteRejected 统一执行 typed raw 正负 predicate，避免任一结论只靠 child 自报。
function Test-D0M2TPassedSemanticEvidence
{
    param([Parameter(Mandatory = $true)]$Contract, [Parameter(Mandatory = $true)]$Aggregate, [Parameter(Mandatory = $true)]$EvidenceIndex)

    if ([string]$Aggregate.Status -cnotin @("Passed", "RouteRejected")) { return }
    foreach ($name in @("Authority", "Selector", "Unity", "Fixture"))
    {
        if (@($Aggregate.$name.PSObject.Properties).Count -eq 0) { throw "EvidenceIncomplete: conclusive aggregate '$name' must not be empty." }
    }
    if ([string]$Contract.PassedSemanticEvidenceAdmission -cne "TypedRawV1")
    {
        throw "EvidenceIncomplete: conclusive semantic raw shape is not admitted yet."
    }
    $validator = Get-Command Test-D0M2TTypedRawSemanticEvidence -CommandType Function -ErrorAction SilentlyContinue
    if ($null -eq $validator) { throw "EvidenceIncomplete: typed raw semantic validator is missing." }
    try
    {
        Test-D0M2TExperimentObjectClosure $Contract $Aggregate $EvidenceIndex
        if ([string]$Aggregate.Status -ceq "Passed") { Test-D0M2TTypedRawSemanticEvidence $Contract $Aggregate $EvidenceIndex }
        else { Test-D0M2TRouteRejectedSemanticEvidence $Contract $Aggregate $EvidenceIndex }
    }
    catch
    {
        if ($_.Exception.Message.StartsWith("EvidenceIncomplete:", [StringComparison]::Ordinal)) { throw }
        throw "EvidenceIncomplete: typed raw validation failed. $($_.Exception.Message)"
    }
}

# 执行完整 D0-M2T aggregate 契约与 raw evidence 闭包校验。
function Test-D0M2TAggregate
{
    param([Parameter(Mandatory = $true)]$Contract, [Parameter(Mandatory = $true)]$Aggregate, [Parameter(Mandatory = $true)][string]$RootPath, [Parameter(Mandatory = $true)][string]$RunId)

    Test-D0M2THeader -Contract $Contract -Aggregate $Aggregate -RunId $RunId
    $cases = @(Test-D0M2TCases -Contract $Contract -Aggregate $Aggregate)
    if ([string]$Aggregate.Status -cin @("Passed", "RouteRejected") -and @($Aggregate.EvidenceFiles).Count -lt 8) { throw "EvidenceIncomplete: conclusive aggregate requires at least eight raw evidence files." }
    $evidenceIndex = Test-D0M2TEvidenceFiles -Contract $Contract -Aggregate $Aggregate -RootPath $RootPath
    Test-D0M2TEvidenceReferences -Contract $Contract -Cases $cases -EvidenceIndex $evidenceIndex
    Test-D0M2TRolesAndProtected -Contract $Contract -Aggregate $Aggregate -EvidenceIndex $evidenceIndex
    Test-D0M2TEvidenceClosure -Contract $Contract -Aggregate $Aggregate -EvidenceIndex $evidenceIndex
    Test-D0M2TPassedSemanticEvidence -Contract $Contract -Aggregate $Aggregate -EvidenceIndex $evidenceIndex
}

# 将 checker 异常稳定映射为中央 runner 可解析的机器 FailureReason。
function Get-D0M2TCheckerFailureReason
{
    param([Parameter(Mandatory = $true)][string]$Message)

    foreach ($reason in @("EvidenceFileMissing", "EvidenceHashMismatch", "EvidenceIncomplete", "CaseIdSetMismatch", "InputIdentityDrift", "ToolIdentityDrift", "UnityIdentityMismatch", "SchemaViolation"))
    {
        if ($Message.StartsWith($reason + ":", [StringComparison]::Ordinal)) { return $reason }
    }
    if ($Message -match "Evidence file is missing|EvidenceRoot is missing|raw evidence file is missing") { return "EvidenceFileMissing" }
    if ($Message -match "Frozen input|Frozen record|frozen inputs") { return "InputIdentityDrift" }
    if ($Message -match "SHA-256 mismatch|Length mismatch|raw SHA drift|Frozen record raw SHA drift") { return "EvidenceHashMismatch" }
    if ($Message -match "case IDs|case names|Cases must contain|CaseId") { return "CaseIdSetMismatch" }
    if ($Message -match "evidence|Evidence|selector|role observation|Protected snapshot") { return "EvidenceIncomplete" }
    return "SchemaViolation"
}

try
{
    $validateLiveFrozenInputs = $PSCmdlet.ParameterSetName -ceq "Static" -and -not $HistoricalReplay
    $staticResult = Test-D0M2TStaticContract -ValidateLiveFrozenInputs:$validateLiveFrozenInputs
    if ($PSCmdlet.ParameterSetName -ceq "Static")
    {
        $syntheticOutput = & pwsh -NoProfile -File $syntheticPath 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { throw "Synthetic contract validation failed: $syntheticOutput" }
        $syntheticResult = $syntheticOutput | ConvertFrom-Json -Depth 20
        if ($syntheticResult.Passed -isnot [bool] -or $syntheticResult.Passed -ne $true -or [string]$syntheticResult.Schema -cne "D0M2T-ContractSyntheticValidation-v1") { throw "Synthetic contract validation result drift." }
        [pscustomobject]@{
            Schema = "D0M2T-ContractStaticValidation-v1"
            Passed = $true
            ContractSha256 = $staticResult.ContractSha256
            AggregateSchemaSha256 = $staticResult.AggregateSchemaSha256
            OracleSha256 = $staticResult.OracleSha256
            SyntheticPassed = $true
            SyntheticSha256 = Get-D0M2TFileSha256 -FilePath $syntheticPath
            HistoricalReplay = [bool]$HistoricalReplay
        } | ConvertTo-Json -Depth 10
        exit 0
    }

    $aggregatePath = [IO.Path]::GetFullPath($Path)
    $aggregate = Read-D0M2TJsonObject -FilePath $aggregatePath
    Test-D0M2TAggregate -Contract $staticResult.Contract -Aggregate $aggregate -RootPath $EvidenceRoot -RunId $ExpectedRunId
    [pscustomobject]@{
        Schema = "D0M2T-ContractValidation-v1"
        Passed = $true
        Path = $aggregatePath.Replace("\", "/")
        AggregateSha256 = Get-D0M2TFileSha256 -FilePath $aggregatePath
        ExpectedRunId = $ExpectedRunId
        ContractSha256 = $staticResult.ContractSha256
    } | ConvertTo-Json -Depth 10
    exit 0
}
catch
{
    $failureReason = Get-D0M2TCheckerFailureReason -Message $_.Exception.Message
    $failure = [pscustomobject][ordered]@{
        Schema = "D0M2T-ContractValidationFailure-v1"
        Passed = $false
        FailureReason = $failureReason
        Message = $_.Exception.Message
    }
    [Console]::Error.WriteLine(($failure | ConvertTo-Json -Compress -Depth 10))
    exit 1
}
