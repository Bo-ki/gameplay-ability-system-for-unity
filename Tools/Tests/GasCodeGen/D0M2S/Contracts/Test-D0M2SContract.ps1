#requires -Version 7.0
[CmdletBinding(DefaultParameterSetName = "Evidence")]
param(
    [Parameter(Mandatory = $true, ParameterSetName = "Evidence")][string]$Path,
    [Parameter(Mandatory = $true, ParameterSetName = "Evidence")][string]$EvidenceRoot,
    [Parameter(Mandatory = $true, ParameterSetName = "Evidence")]
    [ValidatePattern("^D0M2S-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$")][string]$ExpectedRunId,
    [Parameter(Mandatory = $true, ParameterSetName = "Static")][switch]$StaticOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:ContractPath = Join-Path $PSScriptRoot "D0M2S.contract.json"
$script:OraclePath = Join-Path $PSScriptRoot "D0M2SProtectedPathOracle.ps1"
$script:RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../../../.."))
$script:CentralRunnerPath = Join-Path $script:RepositoryRoot "Tools/Tests/GasCodeGen/Run-N2-G0-D0-M2S.ps1"
$script:ChildRunnerPath = Join-Path $script:RepositoryRoot "Tools/Tests/GasCodeGen/D0M2S/SourceGenerator/Run-D0M2S-SourceGeneratorFaultExperiment.ps1"
$script:ChildStaticPath = Join-Path $script:RepositoryRoot "Tools/Tests/GasCodeGen/D0M2S/SourceGenerator/Test-D0M2S-SourceGeneratorStatic.ps1"
$script:TerminalFileName = "D0M2S.terminal.json"
$script:SidecarFileName = "D0M2S.terminal.sha256"

# 读取 JSON 对象并拒绝数组、null 或损坏内容。
function Read-D0M2SJsonObject
{
    param([Parameter(Mandatory = $true)][string]$FilePath)

    if (-not [IO.File]::Exists($FilePath)) { throw "EvidenceFileMissing: $FilePath" }
    $value = [IO.File]::ReadAllText($FilePath, [Text.Encoding]::UTF8) | ConvertFrom-Json -Depth 100
    if ($null -eq $value -or $value -is [Array] -or $value -isnot [pscustomobject])
    {
        throw "SchemaViolation: JSON root must be an object: $FilePath"
    }

    return $value
}

# 计算普通文件原始字节的 SHA-256 小写身份。
function Get-D0M2SValidationSha256
{
    param([Parameter(Mandatory = $true)][string]$FilePath)

    return (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256).Hash.ToLowerInvariant()
}

# 计算 UTF-8 canonical 文本的 SHA-256。
function Get-D0M2SValidationTextSha256
{
    param([Parameter(Mandatory = $true)][string]$Text)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try
    {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Text)
        return ([BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally { $algorithm.Dispose() }
}

# 要求对象拥有精确字段集合，阻止宽松 schema 接受拼接事实。
function Assert-D0M2SExactProperties
{
    param(
        [Parameter(Mandatory = $true)]$Object,
        [Parameter(Mandatory = $true)][string[]]$Expected,
        [Parameter(Mandatory = $true)][string]$Context
    )

    if ($null -eq $Object -or $Object -isnot [pscustomobject]) { throw "SchemaViolation: $Context must be an object." }
    $actual = @($Object.PSObject.Properties | ForEach-Object Name | Sort-Object -CaseSensitive)
    $wanted = @($Expected | Sort-Object -CaseSensitive)
    if ([string]::Join("`n", $actual) -cne [string]::Join("`n", $wanted))
    {
        throw "SchemaViolation: $Context property set mismatch."
    }
}

# 要求两个字符串集合大小写敏感且无重复地相等。
function Assert-D0M2SExactSet
{
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Actual,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Expected,
        [Parameter(Mandatory = $true)][string]$Context
    )

    if (@($Actual | Select-Object -Unique).Count -ne $Actual.Count -or
        @($Expected | Select-Object -Unique).Count -ne $Expected.Count)
    {
        throw "SchemaViolation: $Context contains duplicates."
    }

    $actualOrdered = @($Actual | Sort-Object -CaseSensitive)
    $expectedOrdered = @($Expected | Sort-Object -CaseSensitive)
    if ([string]::Join("`n", $actualOrdered) -cne [string]::Join("`n", $expectedOrdered))
    {
        throw "SchemaViolation: $Context set mismatch."
    }
}

# 解析 PowerShell 文件并拒绝任何 AST 错误。
function Assert-D0M2SPowerShellAst
{
    param([Parameter(Mandatory = $true)][string]$FilePath)

    if (-not [IO.File]::Exists($FilePath)) { throw "InputIdentityDrift: tool file is missing: $FilePath" }
    $tokens = $null
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($FilePath, [ref]$tokens, [ref]$errors)
    if ($errors.Count -ne 0) { throw "SchemaViolation: PowerShell AST failed: $FilePath :: $($errors[0].Message)" }
}

# 校验不可变 D0-M2R 决策、固定 case 与工具入口的静态身份。
function Test-D0M2SStaticContract
{
    $contract = Read-D0M2SJsonObject -FilePath $script:ContractPath
    if ([string]$contract.Schema -cne "D0M2S-Contract-v1" -or
        [string]$contract.TerminalSchema -cne "D0M2S-Terminal-v1")
    {
        throw "SchemaViolation: contract schema identity drift."
    }

    $expectedIds = @(1..8 | ForEach-Object { "SG-{0:D2}" -f $_ })
    $expectedNames = @(
        "ColdAIdentity", "AtomicSelectorSwitch", "GeneratorKillRestart", "MissingCorruptInputs",
        "SourceGeneratorSpecificXPositive", "CompetingAuthorityFailClosed", "BoundaryCleanup", "EvidenceClosure")
    $actualIds = @($contract.CaseSet | ForEach-Object { [string]$_.CaseId })
    $actualNames = @($contract.CaseSet | ForEach-Object { [string]$_.Name })
    if ([string]::Join("`n", $actualIds) -cne [string]::Join("`n", $expectedIds) -or
        [string]::Join("`n", $actualNames) -cne [string]::Join("`n", $expectedNames))
    {
        throw "SchemaViolation: fixed case identity drift."
    }

    $decisionPath = Join-Path $script:RepositoryRoot ([string]$contract.DecisionInput.Path)
    if (-not [IO.File]::Exists($decisionPath) -or
        (Get-D0M2SValidationSha256 -FilePath $decisionPath) -cne [string]$contract.DecisionInput.Sha256)
    {
        throw "InputIdentityDrift: D0-M2R decision input drift."
    }

    foreach ($file in @($script:CentralRunnerPath, $script:ChildRunnerPath, $script:ChildStaticPath, $script:OraclePath, $PSCommandPath))
    {
        Assert-D0M2SPowerShellAst -FilePath $file
    }

    return [pscustomobject]@{
        Contract = $contract
        ContractSha256 = Get-D0M2SValidationSha256 -FilePath $script:ContractPath
        DecisionSha256 = Get-D0M2SValidationSha256 -FilePath $decisionPath
    }
}

# 校验终态冻结字段仍保持门后确认前的未授权状态。
function Test-D0M2SFrozenTerminalFields
{
    param([Parameter(Mandatory = $true)]$Contract, [Parameter(Mandatory = $true)]$Terminal)

    if ([string]$Terminal.RouteUnderTest -cne [string]$Contract.RouteUnderTest)
    {
        throw "SchemaViolation: RouteUnderTest drift."
    }

    foreach ($name in @(
            "AcceptedProductionRoute", "AcceptedSoleUnityConsumedSelector", "D1Authorized",
            "ProductionInstallAdmission", "DeclaredFullSemanticEligibility",
            "ProductionMigrationAuthorized", "NextGate"))
    {
        $actualProperty = $Terminal.PSObject.Properties[$name]
        $expectedProperty = $Contract.FrozenTerminalFields.PSObject.Properties[$name]
        if ($null -eq $actualProperty -or $null -eq $expectedProperty -or
            $actualProperty.Value.GetType() -ne $expectedProperty.Value.GetType() -or
            $actualProperty.Value -cne $expectedProperty.Value)
        {
            throw "SchemaViolation: frozen terminal field '$name' drift."
        }
    }
}

# 将固定 case 状态重算为 Passed、RouteRejected 或 Inconclusive。
function Get-D0M2SGateStatus
{
    param([Parameter(Mandatory = $true)][object[]]$Cases)

    if (@($Cases | Where-Object { [string]$_.Status -ceq "Failed" }).Count -gt 0)
    {
        return "RouteRejected"
    }

    if (@($Cases | Where-Object { [string]$_.Status -in @("Inconclusive", "NotRun") }).Count -gt 0)
    {
        return "Inconclusive"
    }

    return "Passed"
}

# 校验固定 case 集、状态原因与 full-fault/X 派生终态。
function Test-D0M2SCases
{
    param([Parameter(Mandatory = $true)]$Contract, [Parameter(Mandatory = $true)]$Terminal)

    if ($Terminal.Cases -isnot [Array] -or @($Terminal.Cases).Count -ne 8)
    {
        throw "SchemaViolation: terminal must contain exactly eight cases."
    }

    $cases = @($Terminal.Cases)
    for ($index = 0; $index -lt $cases.Count; $index++)
    {
        $case = $cases[$index]
        Assert-D0M2SExactProperties -Object $case -Expected @("CaseId", "Name", "Status", "Reason", "EvidencePaths", "Evidence") -Context "Cases[$index]"
        if ([string]$case.CaseId -cne [string]$Contract.CaseSet[$index].CaseId -or
            [string]$case.Name -cne [string]$Contract.CaseSet[$index].Name)
        {
            throw "SchemaViolation: fixed case order/identity drift."
        }

        if ([string]$case.Status -cnotin @($Contract.CaseStatuses)) { throw "SchemaViolation: invalid case status." }
        if ($case.EvidencePaths -isnot [Array]) { throw "SchemaViolation: case EvidencePaths must be an array." }
        $reason = [string]$case.Reason
        switch ([string]$case.Status)
        {
            "Passed" { if ($reason -cne "None") { throw "SchemaViolation: passed case reason must be None." } }
            "Failed" { if ($reason -cnotin @($Contract.Reasons.RouteRejected)) { throw "SchemaViolation: failed case reason is not technical." } }
            "Inconclusive" { if ($reason -cnotin @($Contract.Reasons.Inconclusive)) { throw "SchemaViolation: inconclusive case reason drift." } }
            "NotRun" { if ($reason -cne "EvidenceIncomplete") { throw "SchemaViolation: NotRun reason must be EvidenceIncomplete." } }
        }
    }

    $fullFault = Get-D0M2SGateStatus -Cases @($cases[0..3])
    $specificX = Get-D0M2SGateStatus -Cases @($cases[4..5])
    if ([string]$Terminal.FullFaultStatus -cne $fullFault -or
        [string]$Terminal.SourceGeneratorSpecificXStatus -cne $specificX)
    {
        throw "SchemaViolation: derived full-fault/X status drift."
    }

    $derivedTerminal = Get-D0M2SGateStatus -Cases $cases
    if (-not [bool]$Terminal.Protected.Unchanged -or -not [bool]$Terminal.ToolIdentity.Unchanged -or
        -not [bool]$Terminal.EvidenceClosure.Verified -or [string]$Terminal.Fixture.CleanupStatus -cne "Passed")
    {
        $derivedTerminal = "Inconclusive"
    }

    if ([string]$Terminal.Status -cne $derivedTerminal) { throw "SchemaViolation: aggregate status contradicts cases/global gates." }
    if ($derivedTerminal -ceq "Passed" -and [string]$Terminal.Reason -cne "None")
    {
        throw "SchemaViolation: Passed terminal reason must be None."
    }
    if ($derivedTerminal -ceq "RouteRejected" -and [string]$Terminal.Reason -cnotin @($Contract.Reasons.RouteRejected))
    {
        throw "SchemaViolation: RouteRejected terminal reason drift."
    }
    if ($derivedTerminal -ceq "Inconclusive" -and [string]$Terminal.Reason -cnotin @($Contract.Reasons.Inconclusive))
    {
        throw "SchemaViolation: Inconclusive terminal reason drift."
    }

    if ($derivedTerminal -ceq "Passed")
    {
        if ([string]::IsNullOrWhiteSpace([string]$Terminal.ObservedFixtureSelector) -or
            [string]$Terminal.MeasuredCommonSelectorAcrossAssemblies -cne [string]$Terminal.ObservedFixtureSelector -or
            [bool]$Terminal.Fixture.OwnedProcessResidue -or [string]$Terminal.Fixture.Root -cne "<deleted>")
        {
            throw "EvidenceIncomplete: passed terminal lacks common selector or clean fixture."
        }
    }
}

# 将证据相对路径解析在 run root 内并拒绝 reparse/越界。
function Resolve-D0M2SEvidencePath
{
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath) -or
        $RelativePath.Contains("\") -or $RelativePath -match "(^|/)\.\.?(?:/|$)")
    {
        throw "SchemaViolation: non-canonical evidence path: $RelativePath"
    }

    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $resolved = [IO.Path]::GetFullPath((Join-Path $rootPath $RelativePath))
    if (-not $resolved.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "SchemaViolation: evidence path escaped root."
    }

    return $resolved
}

# 复算全部 evidence leaf 的路径、长度、SHA、物理集合和 canonical aggregate。
function Test-D0M2SEvidenceClosure
{
    param([Parameter(Mandatory = $true)]$Terminal, [Parameter(Mandatory = $true)][string]$Root)

    if ($Terminal.EvidenceFiles -isnot [Array]) { throw "SchemaViolation: EvidenceFiles must be an array." }
    $declaredPaths = [Collections.Generic.List[string]]::new()
    $canonical = [Collections.Generic.List[string]]::new()
    foreach ($leaf in @($Terminal.EvidenceFiles))
    {
        Assert-D0M2SExactProperties -Object $leaf -Expected @("Path", "Length", "Sha256", "Kind") -Context "EvidenceFiles leaf"
        $relative = [string]$leaf.Path
        if ($relative -cin @($script:TerminalFileName, $script:SidecarFileName))
        {
            throw "SchemaViolation: terminal/sidecar cannot self-enter evidence closure."
        }
        $declaredPaths.Add($relative)
        $resolved = Resolve-D0M2SEvidencePath -Root $Root -RelativePath $relative
        if (-not [IO.File]::Exists($resolved)) { throw "EvidenceFileMissing: $relative" }
        if ([IO.FileInfo]::new($resolved).Length -ne [long]$leaf.Length) { throw "EvidenceHashMismatch: length mismatch: $relative" }
        if ((Get-D0M2SValidationSha256 -FilePath $resolved) -cne [string]$leaf.Sha256)
        {
            throw "EvidenceHashMismatch: SHA-256 mismatch: $relative"
        }
        $canonical.Add("$relative|$([long]$leaf.Length)|$([string]$leaf.Sha256)")
    }

    $physicalPaths = @([IO.Directory]::EnumerateFiles([IO.Path]::GetFullPath($Root), "*", [IO.SearchOption]::AllDirectories) |
            ForEach-Object { [IO.Path]::GetRelativePath($Root, $_).Replace("\", "/") } |
            Where-Object { $_ -cnotin @($script:TerminalFileName, $script:SidecarFileName) })
    Assert-D0M2SExactSet -Actual $declaredPaths.ToArray() -Expected $physicalPaths -Context "physical evidence closure"

    $casePaths = @($Terminal.Cases | ForEach-Object { @($_.EvidencePaths) } | ForEach-Object { [string]$_ })
    foreach ($casePath in $casePaths)
    {
        if ($casePath -cnotin $declaredPaths) { throw "EvidenceIncomplete: case references undeclared evidence: $casePath" }
    }

    $ordered = $canonical.ToArray()
    [Array]::Sort($ordered, [StringComparer]::Ordinal)
    $aggregate = Get-D0M2SValidationTextSha256 -Text ([string]::Join("`n", $ordered))
    $expectedPartial = [string]$Terminal.Status -ceq "Inconclusive"
    if (-not [bool]$Terminal.EvidenceClosure.Verified -or
        [bool]$Terminal.EvidenceClosure.Partial -ne $expectedPartial -or
        [int]$Terminal.EvidenceClosure.DeclaredFileCount -ne $declaredPaths.Count -or
        [int]$Terminal.EvidenceClosure.PhysicalFileCount -ne $physicalPaths.Count -or
        [string]$Terminal.EvidenceClosure.AggregateSha256 -cne $aggregate)
    {
        throw "EvidenceIncomplete: evidence closure summary drift."
    }
}

# 校验 terminal sidecar 精确绑定终态原始字节。
function Test-D0M2STerminalSidecar
{
    param([Parameter(Mandatory = $true)][string]$TerminalPath, [Parameter(Mandatory = $true)][string]$Root)

    $sidecar = Join-Path $Root $script:SidecarFileName
    if (-not [IO.File]::Exists($sidecar)) { throw "EvidenceFileMissing: terminal sidecar is missing." }
    $content = [IO.File]::ReadAllText($sidecar, [Text.Encoding]::ASCII)
    $expected = (Get-D0M2SValidationSha256 -FilePath $TerminalPath) + "`n"
    if ($content -cne $expected) { throw "EvidenceHashMismatch: terminal sidecar mismatch." }
}

# 校验正式 terminal 的字段、输入身份、case 语义与证据闭包。
function Test-D0M2STerminal
{
    param(
        [Parameter(Mandatory = $true)]$Static,
        [Parameter(Mandatory = $true)]$Terminal,
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$RunId,
        [Parameter(Mandatory = $true)][string]$TerminalPath
    )

    $expectedTop = @(
        "Schema", "RunId", "Status", "Reason", "RouteUnderTest",
        "AcceptedProductionRoute", "AcceptedSoleUnityConsumedSelector", "D1Authorized",
        "ProductionInstallAdmission", "DeclaredFullSemanticEligibility", "ProductionMigrationAuthorized", "NextGate",
        "Inputs", "Cases", "FullFaultStatus", "SourceGeneratorSpecificXStatus",
        "ObservedFixtureSelector", "MeasuredCommonSelectorAcrossAssemblies", "RoleObservations",
        "Fixture", "Unity", "Protected", "ToolIdentity", "EvidenceClosure", "EvidenceFiles")
    Assert-D0M2SExactProperties -Object $Terminal -Expected $expectedTop -Context "terminal"
    if ([string]$Terminal.Schema -cne [string]$Static.Contract.TerminalSchema -or
        [string]$Terminal.RunId -cne $RunId -or [string]$Terminal.RunId -cnotmatch [string]$Static.Contract.RunIdPattern)
    {
        throw "SchemaViolation: terminal schema or RunId mismatch."
    }

    if ([string]$Terminal.Status -cnotin @($Static.Contract.AggregateStatuses))
    {
        throw "SchemaViolation: terminal Status drift."
    }
    Test-D0M2SFrozenTerminalFields -Contract $Static.Contract -Terminal $Terminal
    Assert-D0M2SExactProperties -Object $Terminal.Inputs -Expected @(
        "DecisionPath", "DecisionSha256", "ContractSha256", "RunnerSha256", "ChildRunnerSha256",
        "UnityPath", "UnitySha256", "UnityVersion") -Context "Inputs"
    if ([string]$Terminal.Inputs.DecisionPath -cne [string]$Static.Contract.DecisionInput.Path -or
        [string]$Terminal.Inputs.DecisionSha256 -cne [string]$Static.DecisionSha256 -or
        [string]$Terminal.Inputs.ContractSha256 -cne [string]$Static.ContractSha256 -or
        [string]$Terminal.Inputs.RunnerSha256 -cne (Get-D0M2SValidationSha256 -FilePath $script:CentralRunnerPath) -or
        [string]$Terminal.Inputs.ChildRunnerSha256 -cne (Get-D0M2SValidationSha256 -FilePath $script:ChildRunnerPath))
    {
        throw "InputIdentityDrift: terminal input/tool binding drift."
    }

    $unityPath = [string]$Terminal.Inputs.UnityPath
    if (-not [IO.Path]::IsPathRooted($unityPath) -or -not [IO.File]::Exists($unityPath) -or
        [string]$Terminal.Inputs.UnitySha256 -cne [string]$Static.Contract.UnityIdentity.Sha256 -or
        [string]$Terminal.Inputs.UnitySha256 -cne (Get-D0M2SValidationSha256 -FilePath $unityPath) -or
        -not ([string]$Terminal.Inputs.UnityVersion).StartsWith([string]$Static.Contract.UnityIdentity.VersionPrefix, [StringComparison]::Ordinal))
    {
        throw "UnityIdentityMismatch: terminal Unity identity drift."
    }

    Test-D0M2SEvidenceClosure -Terminal $Terminal -Root $Root
    Test-D0M2SCases -Contract $Static.Contract -Terminal $Terminal
    Test-D0M2STerminalSidecar -TerminalPath $TerminalPath -Root $Root
}

try
{
    $static = Test-D0M2SStaticContract
    if ($PSCmdlet.ParameterSetName -ceq "Static")
    {
        [pscustomobject]@{
            Schema = "D0M2S-ContractStaticValidation-v1"
            Passed = $true
            ContractSha256 = $static.ContractSha256
            DecisionSha256 = $static.DecisionSha256
        } | ConvertTo-Json -Depth 10
        exit 0
    }

    $root = [IO.Path]::GetFullPath($EvidenceRoot)
    $terminalPath = [IO.Path]::GetFullPath($Path)
    if (-not [IO.Directory]::Exists($root) -or
        [IO.Path]::GetDirectoryName($terminalPath).TrimEnd([IO.Path]::DirectorySeparatorChar) -cne
            $root.TrimEnd([IO.Path]::DirectorySeparatorChar))
    {
        throw "SchemaViolation: terminal must be a direct child of EvidenceRoot."
    }

    $terminal = Read-D0M2SJsonObject -FilePath $terminalPath
    Test-D0M2STerminal -Static $static -Terminal $terminal -Root $root -RunId $ExpectedRunId -TerminalPath $terminalPath
    [pscustomobject]@{
        Schema = "D0M2S-ContractValidation-v1"
        Passed = $true
        Status = [string]$terminal.Status
        Reason = [string]$terminal.Reason
        AggregateSha256 = Get-D0M2SValidationSha256 -FilePath $terminalPath
        ExpectedRunId = $ExpectedRunId
    } | ConvertTo-Json -Depth 10
    exit 0
}
catch
{
    $message = $_.Exception.Message
    $reason = "SchemaViolation"
    foreach ($candidate in @(
            "EvidenceFileMissing", "EvidenceHashMismatch", "EvidenceIncomplete", "InputIdentityDrift",
            "UnityIdentityMismatch", "ToolIdentityDrift", "ProtectedPathDrift", "SchemaViolation"))
    {
        if ($message.StartsWith($candidate + ":", [StringComparison]::Ordinal)) { $reason = $candidate; break }
    }
    [pscustomobject]@{
        Schema = "D0M2S-ContractValidation-v1"
        Passed = $false
        FailureReason = $reason
        Detail = $message
    } | ConvertTo-Json -Depth 10
    exit 1
}
