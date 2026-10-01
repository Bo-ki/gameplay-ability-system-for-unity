[CmdletBinding(DefaultParameterSetName = "Evidence")]
param(
    [Parameter(Mandatory = $true, ParameterSetName = "Evidence")][string]$Path,
    [Parameter(Mandatory = $true, ParameterSetName = "Evidence")][ValidateSet("T", "S", "X")][string]$ExpectedProbeId,
    [Parameter(Mandatory = $true, ParameterSetName = "Evidence")]
    [ValidatePattern("^D0M2F-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$")][string]$ExpectedRunId,
    [Parameter(Mandatory = $true, ParameterSetName = "Static")][switch]$StaticOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$contractPath = Join-Path $PSScriptRoot "D0M2F.contract.json"
$schemaPath = Join-Path $PSScriptRoot "D0M2F.aggregate.schema.json"
$roleTemplatePath = Join-Path $PSScriptRoot "AuthorityDerivedCache.template.json"
$threatPath = Join-Path $PSScriptRoot "D0M2F.threats.json"
$oraclePath = Join-Path $PSScriptRoot "D0M2FProtectedPathOracle.ps1"

# 计算只读合同或证据文件的 SHA-256。
function Get-D0M2FCheckerFileSha256
{
    param([Parameter(Mandatory = $true)][string]$FilePath)

    return (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256).Hash.ToLowerInvariant()
}

# 读取 JSON object，并将语法或顶层类型错误转为显式失败。
function Read-D0M2FJsonObject
{
    param([Parameter(Mandatory = $true)][string]$FilePath)

    if (-not [IO.File]::Exists($FilePath)) { throw "JSON file is missing: $FilePath" }
    $value = [IO.File]::ReadAllText($FilePath, [Text.Encoding]::UTF8) | ConvertFrom-Json -Depth 100
    if ($null -eq $value -or $value -isnot [pscustomobject]) { throw "JSON root must be an object: $FilePath" }
    return $value
}

# 读取大小写精确的必需属性，避免 ConvertFrom-Json 的宽松属性访问掩盖拼写漂移。
function Get-D0M2FRequiredProperty
{
    param(
        [Parameter(Mandatory = $true)]$Object,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $matches = @($Object.PSObject.Properties | Where-Object { $_.Name -ceq $Name })
    if ($matches.Count -ne 1) { throw "$Context must contain exactly one '$Name' property." }
    return $matches[0].Value
}

# 断言 JSON 值是非 null object。
function Assert-D0M2FJsonObject
{
    param($Value, [Parameter(Mandatory = $true)][string]$Context)

    if ($null -eq $Value -or $Value -isnot [pscustomobject]) { throw "$Context must be a JSON object." }
}

# 断言 JSON 值保留数组形状，避免单对象被宽松提升为单元素集合。
function Assert-D0M2FJsonArray
{
    param($Value, [Parameter(Mandatory = $true)][string]$Context)

    if ($null -eq $Value -or $Value -isnot [Array]) { throw "$Context must be a JSON array." }
}

# 断言两个字符串集合大小写精确、无重复且完全相等。
function Assert-D0M2FExactSet
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

# 解析 PowerShell 文件 AST，拒绝任何语法错误。
function Assert-D0M2FPowerShellAst
{
    param([Parameter(Mandatory = $true)][string]$FilePath)

    $tokens = $null
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($FilePath, [ref]$tokens, [ref]$errors)
    if (@($errors).Count -ne 0)
    {
        throw "PowerShell AST validation failed for '$FilePath': $([string]::Join(' | ', @($errors | ForEach-Object Message)))"
    }
}

# 校验机器合同、JSON Schema、角色模板、威胁表与脚本 AST 的静态一致性。
function Test-D0M2FStaticContract
{
    $contract = Read-D0M2FJsonObject -FilePath $contractPath
    $schema = Read-D0M2FJsonObject -FilePath $schemaPath
    $roleTemplate = Read-D0M2FJsonObject -FilePath $roleTemplatePath
    $threats = Read-D0M2FJsonObject -FilePath $threatPath
    if ([string]$contract.Schema -cne "D0M2F-Contract-v1") { throw "Contract schema identity mismatch." }
    if ([string]$schema.properties.Schema.const -cne [string]$contract.ProbeResultSchema) { throw "Probe result schema identity drift." }
    Assert-D0M2FExactSet -Actual @($schema.required | ForEach-Object { [string]$_ }) -Expected @($contract.RequiredTopLevelFields | ForEach-Object { [string]$_ }) -Context "Required top-level fields"
    Assert-D0M2FExactSet -Actual @($schema.properties.ProbeId.enum | ForEach-Object { [string]$_ }) -Expected @($contract.ProbeIds | ForEach-Object { [string]$_ }) -Context "Probe IDs"
    Assert-D0M2FExactSet -Actual @($schema.properties.Status.enum | ForEach-Object { [string]$_ }) -Expected @($contract.ProbeStatuses | ForEach-Object { [string]$_ }) -Context "Probe statuses"
    Assert-D0M2FExactSet -Actual @($contract.ProbeAllowedStatuses.T | ForEach-Object { [string]$_ }) -Expected @("Passed", "HardRejected", "TimedOut", "Inconclusive") -Context "Probe T allowed statuses"
    Assert-D0M2FExactSet -Actual @($contract.ProbeAllowedStatuses.S | ForEach-Object { [string]$_ }) -Expected @("Passed", "HardRejected", "TimedOut", "Inconclusive") -Context "Probe S allowed statuses"
    Assert-D0M2FExactSet -Actual @($contract.ProbeAllowedStatuses.X | ForEach-Object { [string]$_ }) -Expected @("Passed", "TimedOut", "Inconclusive") -Context "Probe X allowed statuses"
    Assert-D0M2FExactSet -Actual @($schema.'$defs'.case.properties.Status.enum | ForEach-Object { [string]$_ }) -Expected @($contract.CaseStatuses | ForEach-Object { [string]$_ }) -Context "Case statuses"
    Assert-D0M2FExactSet -Actual @($schema.'$defs'.roleObservation.properties.Role.enum | ForEach-Object { [string]$_ }) -Expected @($contract.Roles | ForEach-Object { [string]$_ }) -Context "Roles"

    $allReasons = @($contract.Reasons.Passed) + @($contract.Reasons.HardRejected) + @($contract.Reasons.TimedOut) + @($contract.Reasons.Inconclusive)
    Assert-D0M2FExactSet -Actual @($schema.properties.Reason.enum | ForEach-Object { [string]$_ }) -Expected @($allReasons | ForEach-Object { [string]$_ }) -Context "Reasons"
    Assert-D0M2FExactSet -Actual @($schema.'$defs'.reason.enum | ForEach-Object { [string]$_ }) -Expected @($allReasons | ForEach-Object { [string]$_ }) -Context "Case reasons"
    if ([string]$schema.allOf[0].then.properties.Reason.const -cne "None") { throw "Passed reason rule drift." }
    Assert-D0M2FExactSet -Actual @($schema.allOf[1].then.properties.Reason.enum | ForEach-Object { [string]$_ }) -Expected @($contract.Reasons.HardRejected | ForEach-Object { [string]$_ }) -Context "Hard-reject schema reasons"
    if ([string]$schema.allOf[2].then.properties.Reason.const -cne "TimeBoxExceeded") { throw "Timeout reason rule drift." }
    Assert-D0M2FExactSet -Actual @($schema.allOf[3].then.properties.Reason.enum | ForEach-Object { [string]$_ }) -Expected @($contract.Reasons.Inconclusive | ForEach-Object { [string]$_ }) -Context "Inconclusive schema reasons"
    $allCaseIds = [Collections.Generic.List[string]]::new()
    foreach ($probeId in @($contract.ProbeIds))
    {
        $cases = @($contract.CaseSets.$probeId)
        if ($cases.Count -ne 4) { throw "Probe '$probeId' must define exactly four cases." }
        foreach ($case in $cases)
        {
            $caseId = [string]$case.CaseId
            if ($caseId -cnotmatch "^$probeId-0[1-4]$") { throw "Invalid fixed case ID '$caseId'." }
            $allCaseIds.Add($caseId)
        }
    }
    Assert-D0M2FExactSet -Actual $allCaseIds.ToArray() -Expected @("T-01", "T-02", "T-03", "T-04", "S-01", "S-02", "S-03", "S-04", "X-01", "X-02", "X-03", "X-04") -Context "All fixed case IDs"

    Assert-D0M2FExactSet -Actual @($roleTemplate.Observations | ForEach-Object { [string]$_.Role }) -Expected @($contract.Roles | ForEach-Object { [string]$_ }) -Context "Role template"
    if ([string]$roleTemplate.Route -cne "Unselected") { throw "Role template must not select a route." }
    if (@($roleTemplate.Assertions.PSObject.Properties | Where-Object { $_.Value -ne $false }).Count -ne 0) { throw "Role template assertions must start false." }
    $expectedPaths = @("Packages/manifest.json", "Packages/packages-lock.json", "ProjectSettings/ProjectVersion.txt", "ProjectSettings/GasCodeGen", "Assets/GAS/Runtime/V1", "Assets/GAS/Editor/CodeGen", "Assets/GAS/Generated/CodeGen", "Assets/AutoChessDemo/Generated", "Tools/GasCodeGenCli", "Library/Bee", "docs/adr", "方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md", "docs/reviews/RuntimeV1-完成后停顿审查与V1.1-D0-M2F单轮计划.md", "Tools/Tests/GasCodeGen/N2-G0-D0-M1-evidence.md", "TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68")
    Assert-D0M2FExactSet -Actual @($contract.ProtectedPathSpecs | ForEach-Object { [string]$_.Path }) -Expected $expectedPaths -Context "Protected path specs"
    $expectedFiles = @("Packages/manifest.json", "Packages/packages-lock.json", "ProjectSettings/ProjectVersion.txt", "方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md", "docs/reviews/RuntimeV1-完成后停顿审查与V1.1-D0-M2F单轮计划.md", "Tools/Tests/GasCodeGen/N2-G0-D0-M1-evidence.md")
    Assert-D0M2FExactSet -Actual @($contract.ProtectedPathSpecs | Where-Object Kind -ceq "File" | ForEach-Object { [string]$_.Path }) -Expected $expectedFiles -Context "Protected file specs"
    Assert-D0M2FExactSet -Actual @($contract.ProtectedPathSpecs | Where-Object Kind -ceq "Tree" | ForEach-Object { [string]$_.Path }) -Expected @($expectedPaths | Where-Object { $_ -cnotin $expectedFiles }) -Context "Protected tree specs"
    if ($contract.FrozenDeclarations.D1Authorized -ne $false -or [string]$contract.FrozenDeclarations.ProductionInstallAdmission -cne "NotEvaluated" -or $contract.FrozenDeclarations.DeclaredFullSemanticEligibility -ne $false) { throw "Frozen declarations drift." }
    Assert-D0M2FExactSet -Actual @($threats.Threats | ForEach-Object { [string]$_.ThreatId }) -Expected @(1..15 | ForEach-Object { "TH-{0:D2}" -f $_ }) -Context "Threat IDs"
    foreach ($threat in @($threats.Threats))
    {
        $allowed = @($contract.Reasons.([string]$threat.Disposition))
        if ([string]$threat.Reason -cnotin $allowed) { throw "Threat '$($threat.ThreatId)' reason does not match its disposition." }
    }
    Assert-D0M2FPowerShellAst -FilePath $oraclePath
    Assert-D0M2FPowerShellAst -FilePath $PSCommandPath
    foreach ($probeId in @($contract.ProbeIds))
    {
        $sampleCases = @($contract.CaseSets.$probeId | ForEach-Object { [pscustomobject]@{ CaseId = [string]$_.CaseId; Status = "Passed"; Reason = "None"; Evidence = [pscustomobject]@{} } })
        $sample = [pscustomobject]@{ Schema = $contract.ProbeResultSchema; RunId = "D0M2F-19700101T000000Z-000000000000"; ProbeId = $probeId; Status = "Passed"; Reason = "None"; Cases = $sampleCases; Inputs = [pscustomobject]@{}; Protected = [pscustomobject]@{}; Fixture = [pscustomobject]@{}; Unity = [pscustomobject]@{}; RoleObservations = @([pscustomobject]@{ Path = "fixture/selector"; Role = "Authority" }) }
        Test-D0M2FEvidence -Contract $contract -Evidence $sample -ProbeId $probeId -RunId $sample.RunId
    }
    return [pscustomobject]@{
        Schema = "D0M2F-ContractStaticValidation-v1"
        Passed = $true
        ContractSha256 = Get-D0M2FCheckerFileSha256 -FilePath $contractPath
        AggregateSchemaSha256 = Get-D0M2FCheckerFileSha256 -FilePath $schemaPath
        OracleSha256 = Get-D0M2FCheckerFileSha256 -FilePath $oraclePath
    }
}

# 校验单个 R1/R2/root probe JSON 的公共字段、固定 case 集与 typed 状态。
function Test-D0M2FEvidence
{
    param(
        [Parameter(Mandatory = $true)]$Contract,
        [Parameter(Mandatory = $true)]$Evidence,
        [Parameter(Mandatory = $true)][string]$ProbeId,
        [Parameter(Mandatory = $true)][string]$RunId
    )

    foreach ($field in @($Contract.RequiredTopLevelFields))
    {
        [void](Get-D0M2FRequiredProperty -Object $Evidence -Name ([string]$field) -Context "Probe result")
    }
    if ([string]$Evidence.Schema -cne [string]$Contract.ProbeResultSchema) { throw "Evidence Schema mismatch." }
    if ([string]$Evidence.RunId -cne $RunId) { throw "Evidence RunId mismatch." }
    if ([string]$Evidence.ProbeId -cne $ProbeId) { throw "Evidence ProbeId mismatch." }
    $status = [string]$Evidence.Status
    $reason = [string]$Evidence.Reason
    if ($status -cnotin @($Contract.ProbeAllowedStatuses.$ProbeId)) { throw "Status '$status' is not allowed for probe '$ProbeId'." }
    if ($reason -cnotin @($Contract.Reasons.$status)) { throw "Reason '$reason' does not match status '$status'." }
    foreach ($name in @("Inputs", "Protected", "Fixture", "Unity"))
    {
        Assert-D0M2FJsonObject -Value (Get-D0M2FRequiredProperty -Object $Evidence -Name $name -Context "Probe result") -Context $name
    }

    Assert-D0M2FJsonArray -Value $Evidence.Cases -Context "Cases"
    $cases = @($Evidence.Cases)
    $expectedCaseIds = @($Contract.CaseSets.$ProbeId | ForEach-Object { [string]$_.CaseId })
    Assert-D0M2FExactSet -Actual @($cases | ForEach-Object { [string]$_.CaseId }) -Expected $expectedCaseIds -Context "$ProbeId case IDs"
    foreach ($case in $cases)
    {
        foreach ($field in @("CaseId", "Status", "Reason", "Evidence"))
        {
            [void](Get-D0M2FRequiredProperty -Object $case -Name $field -Context "Case '$($case.CaseId)'")
        }
        if ([string]$case.Status -cnotin @($Contract.CaseStatuses)) { throw "Case '$($case.CaseId)' has invalid status." }
        $allReasons = @($Contract.Reasons.Passed) + @($Contract.Reasons.HardRejected) + @($Contract.Reasons.TimedOut) + @($Contract.Reasons.Inconclusive)
        if ([string]$case.Reason -cnotin $allReasons) { throw "Case '$($case.CaseId)' has invalid reason." }
        Assert-D0M2FJsonObject -Value $case.Evidence -Context "Case '$($case.CaseId)' Evidence"
    }
    if ($status -ceq "Passed" -and @($cases | Where-Object { [string]$_.Status -cne "Passed" }).Count -ne 0) { throw "Passed probe must have four passed cases." }
    if ($status -ceq "HardRejected" -and @($cases | Where-Object { [string]$_.Status -ceq "Failed" }).Count -eq 0) { throw "HardRejected probe must have a failed case." }
    if ($status -ceq "TimedOut" -and @($cases | Where-Object { [string]$_.Status -ceq "TimedOut" }).Count -eq 0) { throw "TimedOut probe must have a timed-out case." }

    Assert-D0M2FJsonArray -Value $Evidence.RoleObservations -Context "RoleObservations"
    $roles = @($Evidence.RoleObservations)
    if ($status -ceq "Passed" -and $roles.Count -eq 0) { throw "Passed probe must include role observations." }
    $roleByPath = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($observation in $roles)
    {
        $rolePath = [string](Get-D0M2FRequiredProperty -Object $observation -Name "Path" -Context "Role observation")
        $role = [string](Get-D0M2FRequiredProperty -Object $observation -Name "Role" -Context "Role observation")
        if ([string]::IsNullOrWhiteSpace($rolePath)) { throw "Role observation Path must not be empty." }
        if ($role -cnotin @($Contract.Roles)) { throw "Role observation '$rolePath' has invalid role '$role'." }
        if ($roleByPath.ContainsKey($rolePath) -and $roleByPath[$rolePath] -cne $role) { throw "Role observation '$rolePath' overlaps roles." }
        $roleByPath[$rolePath] = $role
    }
}

try
{
    $staticResult = Test-D0M2FStaticContract
    if ($PSCmdlet.ParameterSetName -ceq "Static")
    {
        $staticResult | ConvertTo-Json -Depth 10
        exit 0
    }

    $contract = Read-D0M2FJsonObject -FilePath $contractPath
    $evidencePath = [IO.Path]::GetFullPath($Path)
    $evidence = Read-D0M2FJsonObject -FilePath $evidencePath
    Test-D0M2FEvidence -Contract $contract -Evidence $evidence -ProbeId $ExpectedProbeId -RunId $ExpectedRunId
    [pscustomobject]@{
        Schema = "D0M2F-ContractValidation-v1"
        Passed = $true
        Path = $evidencePath.Replace("\", "/")
        EvidenceSha256 = Get-D0M2FCheckerFileSha256 -FilePath $evidencePath
        ExpectedProbeId = $ExpectedProbeId
        ExpectedRunId = $ExpectedRunId
        ContractSha256 = $staticResult.ContractSha256
    } | ConvertTo-Json -Depth 10
    exit 0
}
catch
{
    [Console]::Error.WriteLine("D0-M2F contract validation failed: $($_.Exception.Message)")
    exit 1
}
