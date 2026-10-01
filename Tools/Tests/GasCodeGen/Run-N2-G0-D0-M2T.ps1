#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^D0M2T-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$')]
    [string]$RunId,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedJ0DispatchSha256,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedJ0SnapshotSha256,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedUnityIdentitySha256,
    [string]$UnityPath = '',
    [switch]$KeepFixtures
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$script:RunRoot = Join-Path $script:ProjectRoot ('TestResults/GasCodeGen/N2-G0-D0-M2T/' + $RunId)
$script:ContractRoot = Join-Path $PSScriptRoot 'D0M2T/Contracts'
$script:ContractPath = Join-Path $script:ContractRoot 'D0M2T.contract.json'
$script:ContractChecker = Join-Path $script:ContractRoot 'Test-D0M2TContract.ps1'
$script:Oracle = Join-Path $script:ContractRoot 'D0M2TProtectedPathOracle.ps1'
$script:TarballRoot = Join-Path $PSScriptRoot 'D0M2T/Tarball'
$script:TarballRunner = Join-Path $script:TarballRoot 'Run-D0M2T-TarballFaultExperiment.ps1'
$script:TarballStatic = Join-Path $script:TarballRoot 'Test-D0M2T-TarballStatic.ps1'
$script:ExperimentPath = Join-Path $script:RunRoot 'TarballFault.json'
$script:EvidenceRoot = Join-Path $script:RunRoot 'Raw'
$script:TerminalPath = Join-Path $script:RunRoot 'D0M2T.terminal.json'
$script:FrozenRecordPath = Join-Path $script:ProjectRoot 'docs/reviews/RuntimeV1.1-D0-M2F-SpecADR规范冻结结果.md'
$script:FrozenRecordSha256 = 'd9ec80b7cd0229157938c2ddd87701166a7347c2f7f700673b7f17a57560ee50'
$script:ExpectedProjectVersion = '6000.3.14f1'
$script:Utf8NoBom = [Text.UTF8Encoding]::new($false)
$script:ResolvedUnity = ''
$script:TerminalCommittedByInvocation = $false
$script:CommittedTerminalIdentity = $null

# 计算普通文件的原始 SHA-256 身份。
function Get-D0M2TFileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [IO.File]::Exists($Path)) { throw "Required file does not exist: $Path" }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# 计算规范文本的 SHA-256，供有序记录聚合使用。
function Get-D0M2TTextSha256
{
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try
    {
        return [Convert]::ToHexString($algorithm.ComputeHash($script:Utf8NoBom.GetBytes($Text))).ToLowerInvariant()
    }
    finally
    {
        $algorithm.Dispose()
    }
}

# 以 fresh-only、UTF-8 无 BOM 和 flush-to-disk 语义写出证据。
function Write-D0M2TFreshText
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content
    )

    if ([IO.File]::Exists($Path) -or [IO.Directory]::Exists($Path))
    {
        throw "Fresh evidence target already exists: $Path"
    }
    $bytes = $script:Utf8NoBom.GetBytes($Content.Replace("`r`n", "`n").Replace("`r", "`n"))
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
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

# 将对象序列化为 fresh-only 机器证据。
function Write-D0M2TFreshJson
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$Value
    )

    Write-D0M2TFreshText -Path $Path -Content (ConvertTo-D0M2TJsonText -Value $Value)
}

# 生成与 fresh writer 完全一致的 UTF-8/LF JSON 文本。
function ConvertTo-D0M2TJsonText
{
    param([Parameter(Mandatory = $true)]$Value)

    return ((($Value | ConvertTo-Json -Depth 100) + "`n").Replace("`r`n", "`n").Replace("`r", "`n"))
}

# 读取并要求顶层为 JSON object。
function Read-D0M2TJsonObject
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [IO.File]::Exists($Path)) { throw "JSON evidence is missing: $Path" }
    $value = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8) | ConvertFrom-Json -Depth 100
    if ($null -eq $value -or $value -isnot [pscustomobject]) { throw "JSON root must be an object: $Path" }
    return $value
}

# 读取 fresh D0-M2T 的公共合同。
function Get-D0M2TContract
{
    return Read-D0M2TJsonObject -Path $script:ContractPath
}

# 验证 J0 dispatch 的 typed schema 与当前唯一 RunId。
function Assert-D0M2TDispatchIdentity
{
    param([Parameter(Mandatory = $true)]$Dispatch)

    if ([string]$Dispatch.Schema -cne 'D0M2T-J0-v1' -or [string]$Dispatch.RunId -cne $RunId)
    {
        throw 'InputIdentityDrift: J0 dispatch schema or RunId mismatch.'
    }
}

# 读取并严格解析当前项目声明的唯一 Unity Editor 版本。
function Get-D0M2TProjectVersion
{
    $path = Join-Path $script:ProjectRoot 'ProjectSettings/ProjectVersion.txt'
    if (-not [IO.File]::Exists($path)) { throw 'UnityIdentityMismatch: ProjectVersion.txt is missing.' }
    $matches = [regex]::Matches([IO.File]::ReadAllText($path), '(?m)^m_EditorVersion:\s*(\S+)\s*$')
    if ($matches.Count -ne 1) { throw 'UnityIdentityMismatch: m_EditorVersion is missing or duplicated.' }
    $version = [string]$matches[0].Groups[1].Value
    if ($version -cne $script:ExpectedProjectVersion) { throw "UnityIdentityMismatch: unsupported project version '$version'." }
    return $version
}

# 绑定 typed J0 Unity identity、项目版本、请求路径与现场 executable bytes。
function Resolve-D0M2TJ0UnityIdentity
{
    param(
        [Parameter(Mandatory = $true)]$UnityIdentity,
        [AllowEmptyString()][string]$RequestedUnityPath = ''
    )

    if ([string]$UnityIdentity.Schema -cne 'D0M2T-UnityIdentity-v1' -or [string]$UnityIdentity.RunId -cne $RunId)
    {
        throw 'UnityIdentityMismatch: J0 Unity identity schema or RunId mismatch.'
    }
    $projectVersion = Get-D0M2TProjectVersion
    if ([string]$UnityIdentity.ProjectVersion -cne $projectVersion) { throw 'UnityIdentityMismatch: J0 project version mismatch.' }
    $j0Path = [string]$UnityIdentity.UnityPath
    if ([string]::IsNullOrWhiteSpace($j0Path) -or -not [IO.Path]::IsPathRooted($j0Path))
    {
        throw 'UnityIdentityMismatch: J0 UnityPath must be absolute.'
    }
    if (-not [string]::IsNullOrWhiteSpace($RequestedUnityPath) -and -not [IO.Path]::IsPathRooted($RequestedUnityPath))
    {
        throw 'UnityIdentityMismatch: explicit UnityPath must be absolute.'
    }
    $normalizedJ0 = [IO.Path]::GetFullPath($j0Path)
    $selected = if ([string]::IsNullOrWhiteSpace($RequestedUnityPath)) { $normalizedJ0 } else { [IO.Path]::GetFullPath($RequestedUnityPath) }
    $comparison = if ([OperatingSystem]::IsWindows()) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if (-not $selected.Equals($normalizedJ0, $comparison)) { throw 'UnityIdentityMismatch: explicit UnityPath does not match J0 UnityPath.' }
    if ([string]$UnityIdentity.UnityFileSha256 -cnotmatch '^[0-9a-f]{64}$' -or -not [IO.File]::Exists($selected) -or
        (Get-D0M2TFileSha256 -Path $selected) -cne [string]$UnityIdentity.UnityFileSha256)
    {
        throw 'UnityIdentityMismatch: executable bytes do not match J0.'
    }
    return $selected
}

# 固定 Spec/ADR 冻结阶段发布的六个 raw SHA，拒绝 J0 静默采用漂移后的输入。
function Get-D0M2TFrozenInputSpecs
{
    return @(
        [pscustomobject]@{ Name = 'Spec08'; Path = '方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md'; Sha256 = '184df26da7a9080dcafa9af3b50c42655e8a69f41115617337c09621b3bfc6b0' },
        [pscustomobject]@{ Name = 'Adr0001'; Path = 'docs/adr/0001-codegen-single-install-root-and-install-envelope.md'; Sha256 = '38be527ddf587c66655685153ada138efb6a1e92d0815b02378b0bb40b373f47' },
        [pscustomobject]@{ Name = 'Spec20'; Path = '方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/20-策划配置能力交叉审查Spec.md'; Sha256 = 'e3141bdfb299f742b523a5e01166a3326aa01fed7d337a1881cecf48a89e1012' },
        [pscustomobject]@{ Name = 'Glossary91'; Path = '方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/91-术语表.md'; Sha256 = '8f6440d32dfe8a8a84c0f5bafe54af15c04b6ddf2ad382c69a76f7ed2860a7a8' },
        [pscustomobject]@{ Name = 'D0M2FDecision'; Path = 'docs/reviews/RuntimeV1.1-D0-M2F-选路裁决.md'; Sha256 = 'a2d568f60a834ba8a18ed750948b10e1894d1c3e85a98dfd4744356105326629' },
        [pscustomobject]@{ Name = 'GasCodeGenReadme'; Path = 'Tools/Tests/GasCodeGen/README.md'; Sha256 = 'e074d6d6290a244ade97547b8d0fa722d24bc6da953e46d6821c34e671e01586' }
    )
}

# 同时核对冻结常量、J0 声明与现场文件 bytes。
function Assert-D0M2TFrozenInputs
{
    param([Parameter(Mandatory = $true)]$Dispatch)

    $inputProperty = $Dispatch.PSObject.Properties['InputSha256']
    if ($null -eq $inputProperty -or $null -eq $inputProperty.Value) { throw 'InputIdentityDrift: J0 InputSha256 is missing.' }
    foreach ($spec in @(Get-D0M2TFrozenInputSpecs))
    {
        $declaredProperty = $inputProperty.Value.PSObject.Properties[[string]$spec.Name]
        if ($null -eq $declaredProperty -or [string]$declaredProperty.Value -cne [string]$spec.Sha256)
        {
            throw "InputIdentityDrift: J0 frozen identity mismatch for $($spec.Name)."
        }
        $path = Join-Path $script:ProjectRoot ([string]$spec.Path)
        if ((Get-D0M2TFileSha256 -Path $path) -cne [string]$spec.Sha256)
        {
            throw "InputIdentityDrift: frozen file drift for $($spec.Name)."
        }
    }
    $recordProperty = $Dispatch.PSObject.Properties['FrozenRecordSha256']
    if ($null -eq $recordProperty -or [string]$recordProperty.Value -cne $script:FrozenRecordSha256)
    {
        throw 'InputIdentityDrift: J0 FrozenRecordSha256 is missing or drifted.'
    }
    if ((Get-D0M2TFileSha256 -Path $script:FrozenRecordPath) -cne $script:FrozenRecordSha256)
    {
        throw 'InputIdentityDrift: frozen record raw SHA drifted.'
    }
}

# 冻结中央入口、fresh 工具以及实际复用的旧 archive/fixture 原始身份。
function Get-D0M2TToolSnapshot
{
    $roots = @(
        $PSCommandPath,
        $script:ContractRoot,
        $script:TarballRoot,
        (Join-Path $PSScriptRoot 'D0M2F/Tarball/Archives~'),
        (Join-Path $PSScriptRoot 'D0M2F/Tarball/Fixture~')
    )
    $files = [Collections.Generic.List[IO.FileInfo]]::new()
    foreach ($root in $roots)
    {
        if ([IO.File]::Exists($root))
        {
            $item = Get-Item -LiteralPath $root -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Tool identity file is a reparse point: $root" }
            $files.Add($item)
            continue
        }
        if (-not [IO.Directory]::Exists($root)) { throw "Tool identity root is missing: $root" }
        $rootItem = Get-Item -LiteralPath $root -Force
        if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Tool identity root is a reparse point: $root" }
        foreach ($entry in @(Get-ChildItem -LiteralPath $root -Force -Recurse))
        {
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
            {
                throw "Tool identity root contains a reparse point: $($entry.FullName)"
            }
            if (-not $entry.PSIsContainer) { $files.Add($entry) }
        }
    }
    $records = @($files | Sort-Object FullName -Unique | ForEach-Object {
            [pscustomobject]@{
                Path = [IO.Path]::GetRelativePath($script:ProjectRoot, $_.FullName).Replace('\', '/')
                Length = $_.Length
                Sha256 = Get-D0M2TFileSha256 -Path $_.FullName
            }
        })
    $canonical = [string]::Join("`n", @($records | ForEach-Object { $_.Path + "`t" + $_.Length + "`t" + $_.Sha256 }))
    return [pscustomobject]@{
        Schema = 'D0M2T-ToolSnapshot-v1'
        FileCount = $records.Count
        AggregateSha256 = Get-D0M2TTextSha256 -Text $canonical
        Records = $records
    }
}

# 比较 J1/J2 工具树身份。
function Compare-D0M2TToolSnapshots
{
    param(
        [Parameter(Mandatory = $true)]$Before,
        [Parameter(Mandatory = $true)]$After
    )

    return [pscustomobject]@{
        Schema = 'D0M2T-ToolComparison-v1'
        Unchanged = [int]$Before.FileCount -eq [int]$After.FileCount -and [string]$Before.AggregateSha256 -ceq [string]$After.AggregateSha256
        BeforeFileCount = [int]$Before.FileCount
        AfterFileCount = [int]$After.FileCount
        BeforeAggregateSha256 = [string]$Before.AggregateSha256
        AfterAggregateSha256 = [string]$After.AggregateSha256
    }
}

# 解析 PowerShell AST 并拒绝语法错误。
function Assert-D0M2TPowerShellAst
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $tokens = $null
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors)
    if (@($errors).Count -ne 0)
    {
        throw "PowerShell AST failed for '$Path': $([string]::Join(' | ', @($errors | ForEach-Object Message)))"
    }
}

# 以 ArgumentList 启动 owned process，并在超时时终止其完整进程树。
function Invoke-D0M2TProcess
{
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][int]$TimeoutMilliseconds,
        [string]$WorkingDirectory = ''
    )

    $start = [Diagnostics.ProcessStartInfo]::new($FilePath)
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.CreateNoWindow = $true
    if (-not [string]::IsNullOrWhiteSpace($WorkingDirectory)) { $start.WorkingDirectory = $WorkingDirectory }
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $started = $false
    $startedAt = [DateTime]::UtcNow
    try
    {
        $started = $process.Start()
        if (-not $started) { throw "Process failed to start: $FilePath" }
        $processId = $process.Id
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutMilliseconds)
        if ($timedOut)
        {
            try { $process.Kill($true); [void]$process.WaitForExit(30000) } catch { }
            if (-not $process.HasExited) { throw "Process timed out and its owned tree survived: $FilePath" }
        }
        return [pscustomobject]@{
            ProcessId = $processId
            ExitCode = $process.ExitCode
            TimedOut = $timedOut
            DurationMilliseconds = [long]([DateTime]::UtcNow - $startedAt).TotalMilliseconds
            Stdout = $stdoutTask.GetAwaiter().GetResult()
            Stderr = $stderrTask.GetAwaiter().GetResult()
        }
    }
    finally
    {
        if ($started -and -not $process.HasExited)
        {
            try { $process.Kill($true); [void]$process.WaitForExit(30000) } catch { }
        }
        $process.Dispose()
    }
}

# 确认没有会污染 fixture、PackageCache 或 Bee 的活动 Unity 进程。
function Assert-D0M2TNoUnityResidue
{
    $names = @('Unity', 'UnityPackageManager', 'bee_backend', 'UnityShaderCompiler', 'UnityCrashHandler32', 'UnityCrashHandler64')
    $processes = @(Get-Process -Name $names -ErrorAction SilentlyContinue)
    if ($processes.Count -ne 0)
    {
        $detail = @($processes | ForEach-Object { $_.ProcessName + ':' + $_.Id })
        throw 'UnityProcessResidue: ' + [string]::Join(',', $detail)
    }
}

# 拒绝 ProjectRoot 到目标根之间任一现存 reparse path segment。
function Assert-D0M2TPathChain
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $root = [IO.Path]::GetFullPath($script:ProjectRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $target = [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not $target.Equals($root, [StringComparison]::OrdinalIgnoreCase) -and
        -not $target.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Path escaped ProjectRoot: $target"
    }
    $cursor = $root
    foreach ($segment in [IO.Path]::GetRelativePath($root, $target).Split([IO.Path]::DirectorySeparatorChar, [StringSplitOptions]::RemoveEmptyEntries))
    {
        $cursor = Join-Path $cursor $segment
        if ([IO.File]::Exists($cursor) -or [IO.Directory]::Exists($cursor))
        {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Path chain contains a reparse point: $cursor" }
        }
    }
}

# 验证唯一运行根、owner sentinel 与三份 J0 原始身份。
function Assert-D0M2TRunRoot
{
    param([switch]$RequireInitialLayout)

    $expectedRoot = [IO.Path]::GetFullPath((Join-Path $script:ProjectRoot ('TestResults/GasCodeGen/N2-G0-D0-M2T/' + $RunId)))
    if (-not [IO.Directory]::Exists($expectedRoot) -or $expectedRoot -cne [IO.Path]::GetFullPath($script:RunRoot))
    {
        throw 'Run root is missing or escaped the fixed evidence root.'
    }
    Assert-D0M2TPathChain -Path $expectedRoot
    if ([IO.File]::Exists((Join-Path $script:RunRoot 'J0.Aborted.json')))
    {
        throw 'J0Aborted: this run root is permanently non-reusable.'
    }
    $sentinel = Join-Path $script:RunRoot 'RunOwner.sentinel'
    if (-not [IO.File]::Exists($sentinel) -or [IO.File]::ReadAllText($sentinel).Trim() -cne $RunId)
    {
        throw 'Run owner sentinel is missing or invalid.'
    }
    $identities = @(
        @{ Path = (Join-Path $script:RunRoot 'J0.Dispatch.json'); Expected = $ExpectedJ0DispatchSha256 },
        @{ Path = (Join-Path $script:RunRoot 'J0.ProtectedSnapshot.json'); Expected = $ExpectedJ0SnapshotSha256 },
        @{ Path = (Join-Path $script:RunRoot 'J0.UnityIdentity.json'); Expected = $ExpectedUnityIdentitySha256 }
    )
    foreach ($identity in $identities)
    {
        if (-not [IO.File]::Exists($identity.Path)) { throw "J0 evidence is missing: $($identity.Path)" }
        $item = Get-Item -LiteralPath $identity.Path -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or (Get-D0M2TFileSha256 -Path $identity.Path) -cne $identity.Expected)
        {
            throw "J0 evidence identity drift: $($identity.Path)"
        }
    }
    if ($RequireInitialLayout)
    {
        $allowed = @('J0.Dispatch.json', 'J0.ProtectedSnapshot.json', 'J0.UnityIdentity.json', 'RunOwner.sentinel')
        $actual = @(Get-ChildItem -LiteralPath $script:RunRoot -Force | ForEach-Object Name | Sort-Object)
        if ([string]::Join("`n", $actual) -cne [string]::Join("`n", @($allowed | Sort-Object)))
        {
            throw 'Fresh evidence root contains unexpected initial entries.'
        }
    }
}

# 在 U0 前要求所有计划输出叶均不存在。
function Assert-D0M2TFreshEvidenceLeaves
{
    $leafNames = @(
        'J1.ProtectedSnapshot.json', 'J1.ProtectedComparison.json', 'J1.ToolSnapshot.json',
        'Static.Contract.log', 'Static.Tarball.log', 'Process.Tarball.log',
        'TarballFault.json', 'Raw',
        'J2.ProtectedSnapshot.json', 'J2.ProtectedComparison.json',
        'J2.ToolSnapshot.json', 'J2.ToolComparison.json',
        'D0M2T.terminal.json', 'D0M2T.terminal.sha256'
    )
    foreach ($leafName in $leafNames)
    {
        $path = Join-Path $script:RunRoot $leafName
        if ([IO.File]::Exists($path) -or [IO.Directory]::Exists($path))
        {
            throw "Fresh evidence target already exists: $path"
        }
    }
}

# 验证合同身份、八个固定 case 与冻结终态字段。
function Assert-D0M2TContractIdentity
{
    param([Parameter(Mandatory = $true)]$Contract)

    if ([string]$Contract.Schema -cne 'D0M2T-Contract-v1' -or [string]$Contract.AggregateSchema -cne 'D0M2T-Aggregate-v1')
    {
        throw 'SchemaViolation: D0-M2T contract identity mismatch.'
    }
    if ([string]$Contract.FrozenRecordSha256 -cne $script:FrozenRecordSha256)
    {
        throw 'SchemaViolation: contract FrozenRecordSha256 drifted.'
    }
    $expectedIds = 1..8 | ForEach-Object { 'TT-' + $_.ToString('00') }
    $actualIds = @($Contract.CaseSet | ForEach-Object { [string]$_.CaseId })
    if ([string]::Join(',', $actualIds) -cne [string]::Join(',', $expectedIds)) { throw 'CaseIdSetMismatch: contract case order drifted.' }
    $frozen = $Contract.FrozenTerminalFields
    if ([string]$frozen.SelectedRoute -cne 'ImmutableTarball' -or
        [string]$frozen.SoleUnityConsumedSelector -cne 'Packages/manifest.json' -or
        [bool]$frozen.D1Authorized -or
        [string]$frozen.ProductionInstallAdmission -cne 'NotEvaluated' -or
        [bool]$frozen.DeclaredFullSemanticEligibility -or
        [string]$frozen.NextGate -cne 'D0-M2R')
    {
        throw 'SchemaViolation: frozen terminal fields drifted.'
    }
}

# 从 checker 的机器 JSON、前缀或旧文本中恢复 typed failure reason。
function Get-D0M2TCheckerFailureReason
{
    param([Parameter(Mandatory = $true)]$Execution)

    $knownReasons = @(
        'EvidenceFileMissing', 'EvidenceHashMismatch', 'CaseIdSetMismatch', 'EvidenceIncomplete',
        'ProtectedPathDrift', 'InputIdentityDrift', 'ToolIdentityDrift', 'UnityIdentityMismatch',
        'UnityProcessResidue', 'WatcherFailure', 'CleanupFailure', 'TimeBoxExceeded',
        'HarnessFailure', 'SchemaViolation'
    )
    $combined = ([string]$Execution.Stdout + "`n" + [string]$Execution.Stderr).Trim()
    $jsonCandidates = @([string]$Execution.Stdout, [string]$Execution.Stderr) +
        @($combined -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_.StartsWith('{') -and $_.EndsWith('}') })
    foreach ($candidate in @($jsonCandidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }))
    {
        try
        {
            $machine = $candidate.Trim() | ConvertFrom-Json -Depth 20
            foreach ($name in @('FailureReason', 'Reason'))
            {
                $property = $machine.PSObject.Properties[$name]
                if ($null -ne $property -and [string]$property.Value -cin $knownReasons) { return [string]$property.Value }
            }
        }
        catch { }
    }
    foreach ($reason in $knownReasons)
    {
        if ($combined -match ('(?i)(FailureReason|Reason)\s*[=:]\s*["'']?' + [regex]::Escape($reason))) { return $reason }
        if ($combined.StartsWith($reason + ':', [StringComparison]::OrdinalIgnoreCase)) { return $reason }
    }
    if ($combined -match '(?i)evidence file is missing|cannot find.*evidence') { return 'EvidenceFileMissing' }
    if ($combined -match '(?i)evidence.*(length|sha-?256).*mismatch') { return 'EvidenceHashMismatch' }
    if ($combined -match '(?i)case.*(set|sequence|identity|id).*mismatch') { return 'CaseIdSetMismatch' }
    return 'SchemaViolation'
}

# 调用公共 checker 验证一次完整故障实验 JSON。
function Assert-D0M2TExperimentContract
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $validation = Invoke-D0M2TProcess -FilePath (Join-Path $PSHOME 'pwsh.exe') -TimeoutMilliseconds 300000 -Arguments @(
        '-NoProfile', '-File', $script:ContractChecker, '-Path', $Path,
        '-EvidenceRoot', $script:EvidenceRoot, '-ExpectedRunId', $RunId)
    if ($validation.TimedOut) { throw 'TimeBoxExceeded: experiment checker timed out.' }
    if ($validation.ExitCode -ne 0)
    {
        $reason = Get-D0M2TCheckerFailureReason -Execution $validation
        throw "$reason`: experiment checker failed. $($validation.Stderr)"
    }
}

# 执行合同与 Tarball 的纯静态门并固化日志。
function Invoke-D0M2TStaticGates
{
    $gates = @(
        @{ Name = 'Contract'; Script = $script:ContractChecker; Args = @('-StaticOnly') },
        @{ Name = 'Tarball'; Script = $script:TarballStatic; Args = @() }
    )
    $results = [Collections.Generic.List[object]]::new()
    foreach ($gate in $gates)
    {
        if (-not [IO.File]::Exists($gate.Script)) { throw "HarnessFailure: static gate is missing: $($gate.Script)" }
        Assert-D0M2TPowerShellAst -Path $gate.Script
        $execution = Invoke-D0M2TProcess -FilePath (Join-Path $PSHOME 'pwsh.exe') -Arguments (@('-NoProfile', '-File', $gate.Script) + @($gate.Args)) -TimeoutMilliseconds 600000
        $logPath = Join-Path $script:RunRoot ('Static.' + $gate.Name + '.log')
        $content = "ExitCode=$($execution.ExitCode)`nTimedOut=$($execution.TimedOut)`nDurationMilliseconds=$($execution.DurationMilliseconds)`n---STDOUT---`n$($execution.Stdout)`n---STDERR---`n$($execution.Stderr)"
        Write-D0M2TFreshText -Path $logPath -Content $content
        if ($execution.TimedOut -or $execution.ExitCode -ne 0) { throw "HarnessFailure: static gate failed: $($gate.Name)" }
        $results.Add([pscustomobject]@{ Name = $gate.Name; Passed = $true; Path = $logPath.Replace('\', '/'); Sha256 = Get-D0M2TFileSha256 -Path $logPath })
    }
    return $results.ToArray()
}

# 将 child 声明的安全相对路径解析到唯一 Raw 根。
function Resolve-D0M2TRawEvidencePath
{
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath) -or $RelativePath.Contains(':'))
    {
        throw "EvidenceIncomplete: evidence path is not a safe relative path: $RelativePath"
    }
    $normalized = $RelativePath.Replace('\', '/')
    $segments = @($normalized.Split('/', [StringSplitOptions]::RemoveEmptyEntries))
    if ($segments.Count -eq 0 -or @($segments | Where-Object { $_ -ceq '.' -or $_ -ceq '..' }).Count -ne 0)
    {
        throw "EvidenceIncomplete: evidence path contains an invalid segment: $RelativePath"
    }
    $root = [IO.Path]::GetFullPath($script:EvidenceRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $resolved = [IO.Path]::GetFullPath((Join-Path $root ([string]::Join([IO.Path]::DirectorySeparatorChar, $segments))))
    if (-not $resolved.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "EvidenceIncomplete: evidence path escaped Raw root: $RelativePath"
    }
    return [pscustomobject]@{ Relative = [string]::Join('/', $segments); FullPath = $resolved }
}

# 验证 Raw 根和全部后代均为无 reparse 的 suite-owned 普通路径。
function Get-D0M2TRawEvidenceEntries
{
    if (-not [IO.Directory]::Exists($script:EvidenceRoot)) { throw 'EvidenceFileMissing: Raw evidence root is missing.' }
    $rawRootItem = Get-Item -LiteralPath $script:EvidenceRoot -Force
    if (($rawRootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'EvidenceIncomplete: Raw evidence root is a reparse point.' }
    $entries = @(Get-ChildItem -LiteralPath $script:EvidenceRoot -Force -Recurse)
    foreach ($entry in $entries)
    {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "EvidenceIncomplete: Raw evidence contains a reparse point: $($entry.FullName)" }
    }
    return $entries
}

# 在 Inconclusive 收口时绑定所有可安全打开的 partial Raw bytes。
function Get-D0M2TPartialRawInventory
{
    if (-not [IO.Directory]::Exists($script:EvidenceRoot))
    {
        return [pscustomobject]@{
            Schema = 'D0M2T-PartialRawInventory-v1'; RootExists = $false; Captured = $true
            FileCount = 0; AggregateSha256 = ''; Records = @(); Errors = @()
        }
    }
    try { $entries = @(Get-D0M2TRawEvidenceEntries) }
    catch
    {
        return [pscustomobject]@{
            Schema = 'D0M2T-PartialRawInventory-v1'; RootExists = $true; Captured = $false
            FileCount = 0; AggregateSha256 = ''; Records = @(); Errors = @($_.Exception.Message)
        }
    }
    $records = [Collections.Generic.List[object]]::new()
    $errors = [Collections.Generic.List[string]]::new()
    foreach ($entry in @($entries | Where-Object { -not $_.PSIsContainer } | Sort-Object FullName))
    {
        try
        {
            $records.Add([pscustomobject][ordered]@{
                    Path = [IO.Path]::GetRelativePath($script:EvidenceRoot, $entry.FullName).Replace('\', '/')
                    Length = [long]$entry.Length
                    Sha256 = Get-D0M2TFileSha256 -Path $entry.FullName
                })
        }
        catch { $errors.Add($entry.FullName + ': ' + $_.Exception.Message) }
    }
    $canonical = [string]::Join("`n", @($records | ForEach-Object { $_.Path + "`t" + $_.Length + "`t" + $_.Sha256 }))
    return [pscustomobject]@{
        Schema = 'D0M2T-PartialRawInventory-v1'
        RootExists = $true
        Captured = $errors.Count -eq 0
        FileCount = $records.Count
        AggregateSha256 = Get-D0M2TTextSha256 -Text $canonical
        Records = $records.ToArray()
        Errors = $errors.ToArray()
    }
}

# 现场复算 child 声明的每个 Raw 文件并返回可信记录。
function Get-D0M2TVerifiedEvidenceRecords
{
    param(
        [Parameter(Mandatory = $true)]$Result,
        [Parameter(Mandatory = $true)]$Contract,
        [Parameter(Mandatory = $true)][string[]]$ExpectedCaseIds
    )

    $declaredEvidence = @($Result.EvidenceFiles)
    if ([string]$Result.Status -cne 'Inconclusive' -and $declaredEvidence.Count -lt 8)
    {
        throw 'EvidenceIncomplete: a conclusive result declared fewer than eight raw evidence records.'
    }
    if (@($declaredEvidence | Group-Object EvidenceId | Where-Object Count -ne 1).Count -ne 0) { throw 'EvidenceIncomplete: EvidenceId is not unique.' }
    $records = [Collections.Generic.List[object]]::new()
    foreach ($evidence in $declaredEvidence)
    {
        $caseId = [string]$evidence.CaseId
        if ($caseId -cnotin $ExpectedCaseIds -or -not ([string]$evidence.EvidenceId).StartsWith($caseId + '-', [StringComparison]::Ordinal))
        {
            throw "EvidenceIncomplete: evidence/case identity mismatch: $($evidence.EvidenceId)"
        }
        if ([string]$evidence.Kind -cnotin @($Contract.EvidenceKinds | ForEach-Object { [string]$_ }))
        {
            throw "EvidenceIncomplete: unknown evidence kind: $($evidence.Kind)"
        }
        $resolved = Resolve-D0M2TRawEvidencePath -RelativePath ([string]$evidence.Path)
        if (-not [IO.File]::Exists($resolved.FullPath)) { throw "EvidenceFileMissing: $($resolved.Relative)" }
        $item = Get-Item -LiteralPath $resolved.FullPath -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $item.PSIsContainer) { throw "EvidenceIncomplete: evidence leaf is not an ordinary file: $($resolved.Relative)" }
        $actualSha = Get-D0M2TFileSha256 -Path $resolved.FullPath
        if ($item.Length -ne [long]$evidence.Length -or $actualSha -cne [string]$evidence.Sha256)
        {
            throw "EvidenceHashMismatch: $($resolved.Relative)"
        }
        $records.Add([pscustomobject][ordered]@{
                EvidenceId = [string]$evidence.EvidenceId
                CaseId = $caseId
                Kind = [string]$evidence.Kind
                Path = [string]$resolved.Relative
                Sha256 = $actualSha
                Length = [long]$item.Length
            })
    }
    return $records.ToArray()
}

# 现场复算 Raw 文件并验证 case 引用、必需 kind 与物理文件全集闭包。
function Assert-D0M2TEvidenceClosure
{
    param(
        [Parameter(Mandatory = $true)]$Result,
        [Parameter(Mandatory = $true)]$Contract
    )

    $isPartial = [string]$Result.Status -ceq 'Inconclusive'
    if ([IO.Directory]::Exists($script:EvidenceRoot)) { $entries = @(Get-D0M2TRawEvidenceEntries) }
    elseif ($isPartial) { $entries = @() }
    else { throw 'EvidenceFileMissing: Raw evidence root is missing for a conclusive result.' }
    $expectedCases = @($Contract.CaseSet)
    $cases = @($Result.Cases)
    $actualCaseIds = @($cases | ForEach-Object { [string]$_.CaseId })
    $expectedCaseIds = @($expectedCases | ForEach-Object { [string]$_.CaseId })
    if ([string]::Join(',', $actualCaseIds) -cne [string]::Join(',', $expectedCaseIds)) { throw 'CaseIdSetMismatch: experiment case set or order mismatch.' }
    if (@($cases | Group-Object CaseId | Where-Object Count -ne 1).Count -ne 0) { throw 'CaseIdSetMismatch: experiment repeats a case.' }
    $records = @(Get-D0M2TVerifiedEvidenceRecords -Result $Result -Contract $Contract -ExpectedCaseIds $expectedCaseIds)

    $referencedIds = [Collections.Generic.List[string]]::new()
    foreach ($case in $cases)
    {
        $ids = @($case.EvidenceIds | ForEach-Object { [string]$_ })
        $caseStatus = [string]$case.Status
        $mayBeEmpty = $isPartial -and $caseStatus -cin @('Inconclusive', 'NotRun')
        if ((-not $mayBeEmpty -and $ids.Count -eq 0) -or @($ids | Group-Object | Where-Object Count -ne 1).Count -ne 0)
        {
            throw "EvidenceIncomplete: case evidence references are empty or duplicated: $($case.CaseId)"
        }
        foreach ($id in $ids)
        {
            $matches = @($records | Where-Object EvidenceId -CEQ $id)
            if ($matches.Count -ne 1 -or [string]$matches[0].CaseId -cne [string]$case.CaseId) { throw "EvidenceIncomplete: dangling or cross-case evidence reference: $id" }
            $referencedIds.Add($id)
        }
        $caseContract = @($expectedCases | Where-Object CaseId -CEQ ([string]$case.CaseId))[0]
        $caseKinds = @($records | Where-Object CaseId -CEQ ([string]$case.CaseId) | ForEach-Object Kind)
        if ($caseStatus -cin @('Passed', 'Failed'))
        {
            foreach ($requiredKind in @($caseContract.RequiredEvidenceKinds))
            {
                if ([string]$requiredKind -cnotin $caseKinds) { throw "EvidenceIncomplete: $($case.CaseId) lacks required kind $requiredKind." }
            }
        }
    }
    $declaredIds = @($records | ForEach-Object EvidenceId | Sort-Object)
    $caseIds = @($referencedIds | Sort-Object)
    if ([string]::Join(',', $declaredIds) -cne [string]::Join(',', $caseIds)) { throw 'EvidenceIncomplete: raw evidence has dangling or orphan declarations.' }

    $physicalPaths = @($entries | Where-Object { -not $_.PSIsContainer } | ForEach-Object { [IO.Path]::GetRelativePath($script:EvidenceRoot, $_.FullName).Replace('\', '/') } | Sort-Object)
    $declaredPaths = @($records | ForEach-Object Path | Sort-Object)
    if ([string]::Join("`n", $physicalPaths) -cne [string]::Join("`n", $declaredPaths))
    {
        throw 'EvidenceIncomplete: Raw root contains an undeclared file or repeats one path.'
    }
    $canonical = [string]::Join("`n", @($records | Sort-Object EvidenceId | ForEach-Object { $_.EvidenceId + "`t" + $_.CaseId + "`t" + $_.Kind + "`t" + $_.Path + "`t" + $_.Length + "`t" + $_.Sha256 }))
    return [pscustomobject]@{
        Schema = 'D0M2T-EvidenceClosure-v1'
        Verified = $true
        Partial = $isPartial
        FileCount = $records.Count
        AggregateSha256 = Get-D0M2TTextSha256 -Text $canonical
        Records = $records
    }
}

# 校验 child 的 typed status 与退出码配对。
function Assert-D0M2TExperimentExitPairing
{
    param(
        [Parameter(Mandatory = $true)]$Result,
        [Parameter(Mandatory = $true)][int]$ExitCode
    )

    $status = [string]$Result.Status
    $reason = [string]$Result.Reason
    $expected = switch ($status)
    {
        'Passed' { 0 }
        'RouteRejected' { 20 }
        'Inconclusive'
        {
            if ($reason -ceq 'TimeBoxExceeded') { 21 }
            elseif ($reason -ceq 'CleanupFailure') { 24 }
            elseif ($reason -cin @('HarnessFailure', 'WatcherFailure', 'UnityProcessResidue')) { 23 }
            else { 22 }
        }
        default { throw "SchemaViolation: unknown experiment status: $status" }
    }
    if ($ExitCode -ne $expected) { throw "HarnessFailure: experiment status/exit pairing is invalid: $status/$reason/$ExitCode" }
}

# 单次调用完整 Tarball fault matrix；八个 case 由 child 内部统一调度。
function Invoke-D0M2TExperiment
{
    param([Parameter(Mandatory = $true)]$Contract)

    if (-not [IO.File]::Exists($script:TarballRunner)) { throw "HarnessFailure: Tarball runner is missing: $($script:TarballRunner)" }
    Assert-D0M2TPowerShellAst -Path $script:TarballRunner
    Assert-D0M2TNoUnityResidue
    $arguments = @(
        '-NoProfile', '-File', $script:TarballRunner,
        '-UnityPath', $script:ResolvedUnity,
        '-OutputPath', $script:ExperimentPath,
        '-EvidenceRoot', $script:EvidenceRoot,
        '-RunId', $RunId
    )
    if ($KeepFixtures) { $arguments += '-KeepFixtures' }
    $execution = Invoke-D0M2TProcess -FilePath (Join-Path $PSHOME 'pwsh.exe') -Arguments $arguments -TimeoutMilliseconds 7200000 -WorkingDirectory $script:ProjectRoot
    $processLog = Join-Path $script:RunRoot 'Process.Tarball.log'
    $logContent = "ProcessId=$($execution.ProcessId)`nExitCode=$($execution.ExitCode)`nTimedOut=$($execution.TimedOut)`nDurationMilliseconds=$($execution.DurationMilliseconds)`n---STDOUT---`n$($execution.Stdout)`n---STDERR---`n$($execution.Stderr)"
    Write-D0M2TFreshText -Path $processLog -Content $logContent
    Assert-D0M2TNoUnityResidue
    if ($execution.TimedOut) { throw 'TimeBoxExceeded: complete Tarball experiment exceeded the central timeout.' }
    if (-not [IO.File]::Exists($script:ExperimentPath))
    {
        if ($execution.ExitCode -eq 90) { throw 'ExperimentOutputUnavailable: child could not publish its typed JSON.' }
        throw 'HarnessFailure: Tarball experiment output is missing.'
    }
    Assert-D0M2TExperimentContract -Path $script:ExperimentPath
    $result = Read-D0M2TJsonObject -Path $script:ExperimentPath
    if ([string]$result.Schema -cne [string]$Contract.AggregateSchema -or [string]$result.RunId -cne $RunId)
    {
        throw 'SchemaViolation: experiment schema or RunId mismatch.'
    }
    Assert-D0M2TExperimentExitPairing -Result $result -ExitCode $execution.ExitCode
    $evidenceClosure = Assert-D0M2TEvidenceClosure -Result $result -Contract $Contract
    return [pscustomobject]@{
        Result = $result
        Execution = $execution
        Path = $script:ExperimentPath
        Sha256 = Get-D0M2TFileSha256 -Path $script:ExperimentPath
        ProcessLogPath = $processLog
        ProcessLogSha256 = Get-D0M2TFileSha256 -Path $processLog
        EvidenceClosure = $evidenceClosure
    }
}

# 验证中央 terminal 的冻结边界与 conclusive 门。
function Assert-D0M2TTerminalObject
{
    param(
        [Parameter(Mandatory = $true)]$Value,
        [Parameter(Mandatory = $true)]$Contract
    )

    if ([string]$Value.Schema -cne 'D0M2T-Terminal-v1' -or [string]$Value.RunId -cne $RunId) { throw 'Terminal schema or RunId mismatch.' }
    if ([string]$Value.Status -cnotin @($Contract.AggregateStatuses | ForEach-Object { [string]$_ })) { throw "Terminal status is invalid: $($Value.Status)" }
    $allowedReasons = @($Contract.Reasons.Passed) + @($Contract.Reasons.RouteRejected) + @($Contract.Reasons.Inconclusive)
    if ([string]$Value.Reason -cnotin @($allowedReasons | ForEach-Object { [string]$_ })) { throw "Terminal reason is invalid: $($Value.Reason)" }
    if ([string]$Value.SelectedRoute -cne 'ImmutableTarball' -or [string]$Value.SoleUnityConsumedSelector -cne 'Packages/manifest.json' -or
        [bool]$Value.D1Authorized -or [string]$Value.ProductionInstallAdmission -cne 'NotEvaluated' -or
        [bool]$Value.DeclaredFullSemanticEligibility -or [string]$Value.NextGate -cne 'D0-M2R')
    {
        throw 'Terminal illegally changes frozen route or authorizes D1.'
    }
    $expectedIds = @($Contract.CaseSet | ForEach-Object { [string]$_.CaseId })
    $actualIds = @($Value.Cases | ForEach-Object { [string]$_.CaseId })
    if ([string]::Join(',', $actualIds) -cne [string]::Join(',', $expectedIds)) { throw 'Terminal case set mismatch.' }
    if ([string]$Value.Status -ceq 'Passed')
    {
        if ([string]$Value.Reason -cne 'None' -or @($Value.Cases | Where-Object Status -CNE 'Passed').Count -ne 0 -or
            -not [bool]$Value.EvidenceClosure.Verified -or -not [bool]$Value.Protected.Unchanged -or -not [bool]$Value.ToolIdentity.Unchanged -or
            [string]$Value.Experiment.Status -cne 'Passed' -or [int]$Value.Experiment.ExitCode -ne 0 -or
            @($Value.StaticGates | Where-Object { -not [bool]$_.Passed }).Count -ne 0)
        {
            throw 'Passed terminal invariants failed.'
        }
    }
    elseif ([string]$Value.Status -ceq 'RouteRejected')
    {
        if ([string]$Value.Reason -cnotin @($Contract.Reasons.RouteRejected | ForEach-Object { [string]$_ }) -or
            [string]$Value.Experiment.Status -cne 'RouteRejected' -or [int]$Value.Experiment.ExitCode -ne 20 -or
            @($Value.Cases | Where-Object { [string]$_.Status -cin @('Inconclusive', 'NotRun') }).Count -ne 0 -or
            @($Value.Cases | Where-Object Status -CEQ 'Failed').Count -eq 0 -or
            -not [bool]$Value.EvidenceClosure.Verified -or -not [bool]$Value.Protected.Unchanged -or -not [bool]$Value.ToolIdentity.Unchanged)
        {
            throw 'RouteRejected terminal invariants failed.'
        }
    }
}

# 只回读本 invocation 已完整提交且 raw identity 仍匹配的 terminal。
function Read-D0M2TCommittedTerminal
{
    param([Parameter(Mandatory = $true)]$Contract)

    $identity = $script:CommittedTerminalIdentity
    if (-not $script:TerminalCommittedByInvocation -or $null -eq $identity -or
        [string]$identity.Schema -cne 'D0M2T-CommittedTerminalIdentity-v1' -or [string]$identity.RunId -cne $RunId)
    {
        throw 'HarnessFailure: no terminal was committed by this invocation.'
    }
    $terminalPath = [IO.Path]::GetFullPath($script:TerminalPath)
    $sidecarPath = [IO.Path]::GetFullPath((Join-Path $script:RunRoot 'D0M2T.terminal.sha256'))
    if ([string]$identity.TerminalPath -cne $terminalPath -or [string]$identity.SidecarPath -cne $sidecarPath)
    {
        throw 'HarnessFailure: committed terminal path identity drifted.'
    }
    foreach ($path in @($terminalPath, $sidecarPath))
    {
        if (-not [IO.File]::Exists($path)) { throw "HarnessFailure: committed evidence is missing: $path" }
        if (((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "HarnessFailure: committed evidence became a reparse point: $path"
        }
    }
    if ((Get-Item -LiteralPath $terminalPath).Length -ne [long]$identity.TerminalLength -or
        (Get-D0M2TFileSha256 -Path $terminalPath) -cne [string]$identity.TerminalSha256 -or
        (Get-D0M2TFileSha256 -Path $sidecarPath) -cne [string]$identity.SidecarSha256)
    {
        throw 'HarnessFailure: committed terminal raw identity drifted.'
    }
    $expectedSidecar = [string]$identity.TerminalSha256 + "  D0M2T.terminal.json`n"
    if ([IO.File]::ReadAllText($sidecarPath, [Text.Encoding]::UTF8) -cne $expectedSidecar)
    {
        throw 'HarnessFailure: committed sidecar payload is invalid.'
    }
    $terminal = Read-D0M2TJsonObject -Path $terminalPath
    Assert-D0M2TTerminalObject -Value $terminal -Contract $Contract
    return $terminal
}

# 先发布 SHA 预承诺，最后以 fresh terminal 作为唯一权威 commit。
function Publish-D0M2TTerminal
{
    param(
        [Parameter(Mandatory = $true)]$Value,
        [Parameter(Mandatory = $true)]$Contract
    )

    if ($script:TerminalCommittedByInvocation -or $null -ne $script:CommittedTerminalIdentity)
    {
        throw 'HarnessFailure: this invocation already committed a terminal.'
    }
    Assert-D0M2TTerminalObject -Value $Value -Contract $Contract
    $terminalContent = ConvertTo-D0M2TJsonText -Value $Value
    $sha256 = Get-D0M2TTextSha256 -Text $terminalContent
    $sidecarPath = Join-Path $script:RunRoot 'D0M2T.terminal.sha256'
    $sidecarContent = $sha256 + "  D0M2T.terminal.json`n"
    Write-D0M2TFreshText -Path $sidecarPath -Content $sidecarContent
    if ([IO.File]::ReadAllText($sidecarPath, [Text.Encoding]::UTF8) -cne $sidecarContent) { throw 'Terminal SHA sidecar read-back mismatch.' }
    Write-D0M2TFreshText -Path $script:TerminalPath -Content $terminalContent
    if ((Get-D0M2TFileSha256 -Path $script:TerminalPath) -cne $sha256) { throw 'Terminal bytes do not match the committed SHA sidecar.' }
    $readBack = Read-D0M2TJsonObject -Path $script:TerminalPath
    Assert-D0M2TTerminalObject -Value $readBack -Contract $Contract
    if ([IO.File]::ReadAllText($sidecarPath, [Text.Encoding]::UTF8) -cne ($sha256 + "  D0M2T.terminal.json`n"))
    {
        throw 'Terminal SHA sidecar changed after terminal commit.'
    }
    $script:CommittedTerminalIdentity = [pscustomobject]@{
        Schema = 'D0M2T-CommittedTerminalIdentity-v1'
        RunId = $RunId
        TerminalPath = [IO.Path]::GetFullPath($script:TerminalPath)
        TerminalLength = (Get-Item -LiteralPath $script:TerminalPath).Length
        TerminalSha256 = $sha256
        SidecarPath = [IO.Path]::GetFullPath($sidecarPath)
        SidecarSha256 = Get-D0M2TFileSha256 -Path $sidecarPath
    }
    $script:TerminalCommittedByInvocation = $true
    return $sha256
}

# 完成 J2 身份复核并发布正常路径 terminal。
function Complete-D0M2TRunEvidence
{
    param(
        [Parameter(Mandatory = $true)]$Contract,
        [Parameter(Mandatory = $true)]$Dispatch,
        [Parameter(Mandatory = $true)]$UnityIdentity,
        [Parameter(Mandatory = $true)]$J0Snapshot,
        [Parameter(Mandatory = $true)]$J1ToolSnapshot,
        [Parameter(Mandatory = $true)]$StaticGates,
        [Parameter(Mandatory = $true)]$Experiment
    )

    Assert-D0M2TNoUnityResidue
    $j2Snapshot = Get-D0M2TProtectedSnapshot -RepositoryRoot $script:ProjectRoot
    $protected = Compare-D0M2TProtectedSnapshots -Before $J0Snapshot -After $j2Snapshot
    Write-D0M2TFreshJson -Path (Join-Path $script:RunRoot 'J2.ProtectedSnapshot.json') -Value $j2Snapshot
    Write-D0M2TFreshJson -Path (Join-Path $script:RunRoot 'J2.ProtectedComparison.json') -Value $protected
    $j2Tool = Get-D0M2TToolSnapshot
    $toolIdentity = Compare-D0M2TToolSnapshots -Before $J1ToolSnapshot -After $j2Tool
    Write-D0M2TFreshJson -Path (Join-Path $script:RunRoot 'J2.ToolSnapshot.json') -Value $j2Tool
    Write-D0M2TFreshJson -Path (Join-Path $script:RunRoot 'J2.ToolComparison.json') -Value $toolIdentity
    Assert-D0M2TRunRoot
    Assert-D0M2TDispatchIdentity -Dispatch $Dispatch
    Assert-D0M2TFrozenInputs -Dispatch $Dispatch
    [void](Resolve-D0M2TJ0UnityIdentity -UnityIdentity $UnityIdentity -RequestedUnityPath $script:ResolvedUnity)

    $status = [string]$Experiment.Result.Status
    $reason = [string]$Experiment.Result.Reason
    if (-not [bool]$protected.Unchanged)
    {
        $status = 'Inconclusive'
        $reason = 'ProtectedPathDrift'
    }
    elseif (-not [bool]$toolIdentity.Unchanged)
    {
        $status = 'Inconclusive'
        $reason = 'ToolIdentityDrift'
    }
    $terminal = [pscustomobject][ordered]@{
        Schema = 'D0M2T-Terminal-v1'
        RunId = $RunId
        Status = $status
        Reason = $reason
        SelectedRoute = 'ImmutableTarball'
        SoleUnityConsumedSelector = 'Packages/manifest.json'
        Cases = @($Experiment.Result.Cases)
        EvidenceFiles = @($Experiment.EvidenceClosure.Records)
        EvidenceClosure = [pscustomobject]@{
            Schema = [string]$Experiment.EvidenceClosure.Schema
            Verified = [bool]$Experiment.EvidenceClosure.Verified
            Partial = [bool]$Experiment.EvidenceClosure.Partial
            FileCount = [int]$Experiment.EvidenceClosure.FileCount
            AggregateSha256 = [string]$Experiment.EvidenceClosure.AggregateSha256
        }
        Experiment = [pscustomobject]@{
            Path = $Experiment.Path.Replace('\', '/')
            Sha256 = [string]$Experiment.Sha256
            Status = [string]$Experiment.Result.Status
            Reason = [string]$Experiment.Result.Reason
            ExitCode = [int]$Experiment.Execution.ExitCode
            ProcessId = [int]$Experiment.Execution.ProcessId
            DurationMilliseconds = [long]$Experiment.Execution.DurationMilliseconds
            ProcessLogPath = $Experiment.ProcessLogPath.Replace('\', '/')
            ProcessLogSha256 = [string]$Experiment.ProcessLogSha256
        }
        StaticGates = @($StaticGates)
        Protected = $protected
        ToolIdentity = $toolIdentity
        InputSha256 = $Dispatch.InputSha256
        UnityIdentity = $UnityIdentity
        D1Authorized = $false
        ProductionInstallAdmission = 'NotEvaluated'
        DeclaredFullSemanticEligibility = $false
        NextGate = 'D0-M2R'
    }
    [void](Publish-D0M2TTerminal -Value $terminal -Contract $Contract)
    return Read-D0M2TCommittedTerminal -Contract $Contract
}

# 将异常消息归一到合同内的 typed reason。
function Get-D0M2TFailureReason
{
    param([Parameter(Mandatory = $true)][string]$Message)

    foreach ($reason in @('CleanupFailure', 'EvidenceFileMissing', 'EvidenceHashMismatch', 'SchemaViolation',
            'CaseIdSetMismatch', 'ProtectedPathDrift', 'UnityProcessResidue', 'InputIdentityDrift',
            'ToolIdentityDrift', 'WatcherFailure', 'UnityIdentityMismatch', 'TimeBoxExceeded', 'EvidenceIncomplete'))
    {
        if ($Message.StartsWith($reason, [StringComparison]::Ordinal)) { return $reason }
    }
    if ($Message -match 'timed out') { return 'TimeBoxExceeded' }
    if ($Message -match 'identity drift|J0Aborted') { return 'InputIdentityDrift' }
    if ($Message -match 'Fresh evidence|missing|not proven') { return 'EvidenceIncomplete' }
    return 'HarnessFailure'
}

# 将 terminal reason 映射为公开退出码。
function Get-D0M2TTerminalExitCode
{
    param([Parameter(Mandatory = $true)]$Terminal)

    if ([string]$Terminal.Status -ceq 'Passed') { return 0 }
    if ([string]$Terminal.Status -ceq 'RouteRejected') { return 20 }
    if ([string]$Terminal.Reason -ceq 'TimeBoxExceeded') { return 21 }
    if ([string]$Terminal.Reason -ceq 'CleanupFailure') { return 24 }
    $identityOrEvidenceReasons = @(
        'EvidenceIncomplete', 'EvidenceFileMissing', 'EvidenceHashMismatch', 'CaseIdSetMismatch',
        'SchemaViolation', 'ProtectedPathDrift', 'InputIdentityDrift', 'ToolIdentityDrift', 'UnityIdentityMismatch'
    )
    if ([string]$Terminal.Reason -cin $identityOrEvidenceReasons) { return 22 }
    return 23
}

# 为中央异常构造固定八项 NotRun，避免失败 terminal 丢失矩阵身份。
function New-D0M2TNotRunCases
{
    param([Parameter(Mandatory = $true)]$Contract)

    return @($Contract.CaseSet | ForEach-Object {
            [pscustomobject]@{ CaseId = [string]$_.CaseId; Name = [string]$_.Name; Status = 'NotRun'; Reason = 'NotRun'; EvidenceIds = @() }
        })
}

# 异常路径尽力生成并持久化 J2 protected comparison。
function Get-D0M2TFailureProtectedState
{
    param([Parameter(Mandatory = $true)][string]$Reason)

    $comparison = [pscustomobject]@{ Schema = 'D0M2T-ProtectedComparison-v1'; Unchanged = $false; Failure = 'NotEvaluated' }
    if ($null -eq (Get-Command Get-D0M2TProtectedSnapshot -ErrorAction SilentlyContinue))
    {
        return [pscustomobject]@{ Reason = $Reason; Comparison = $comparison }
    }
    try
    {
        $j0 = Read-D0M2TJsonObject -Path (Join-Path $script:RunRoot 'J0.ProtectedSnapshot.json')
        $j2 = Get-D0M2TProtectedSnapshot -RepositoryRoot $script:ProjectRoot
        $comparison = Compare-D0M2TProtectedSnapshots -Before $j0 -After $j2
        $snapshotPath = Join-Path $script:RunRoot 'J2.ProtectedSnapshot.json'
        $comparisonPath = Join-Path $script:RunRoot 'J2.ProtectedComparison.json'
        if (-not [IO.File]::Exists($snapshotPath)) { Write-D0M2TFreshJson -Path $snapshotPath -Value $j2 }
        if (-not [IO.File]::Exists($comparisonPath)) { Write-D0M2TFreshJson -Path $comparisonPath -Value $comparison }
        if (-not [bool]$comparison.Unchanged) { $Reason = 'ProtectedPathDrift' }
    }
    catch { $Reason = 'ProtectedPathDrift' }
    return [pscustomobject]@{ Reason = $Reason; Comparison = $comparison }
}

# 异常路径尽力生成并持久化 J2 tool comparison。
function Get-D0M2TFailureToolState
{
    param([Parameter(Mandatory = $true)][string]$Reason)

    $comparison = [pscustomobject]@{ Schema = 'D0M2T-ToolComparison-v1'; Unchanged = $false; Failure = 'NotEvaluated' }
    try
    {
        $j1ToolPath = Join-Path $script:RunRoot 'J1.ToolSnapshot.json'
        if ([IO.File]::Exists($j1ToolPath))
        {
            $j2Tool = Get-D0M2TToolSnapshot
            $comparison = Compare-D0M2TToolSnapshots -Before (Read-D0M2TJsonObject -Path $j1ToolPath) -After $j2Tool
            $snapshotPath = Join-Path $script:RunRoot 'J2.ToolSnapshot.json'
            $comparisonPath = Join-Path $script:RunRoot 'J2.ToolComparison.json'
            if (-not [IO.File]::Exists($snapshotPath)) { Write-D0M2TFreshJson -Path $snapshotPath -Value $j2Tool }
            if (-not [IO.File]::Exists($comparisonPath)) { Write-D0M2TFreshJson -Path $comparisonPath -Value $comparison }
            if (-not [bool]$comparison.Unchanged) { $Reason = 'ToolIdentityDrift' }
        }
    }
    catch { $Reason = 'ToolIdentityDrift' }
    return [pscustomobject]@{ Reason = $Reason; Comparison = $comparison }
}

# 异常路径尽力完成 residue、J2 与工具身份收口并发布 Inconclusive terminal。
function Publish-D0M2TFailure
{
    param([Parameter(Mandatory = $true)]$ErrorRecord)

    $reason = Get-D0M2TFailureReason -Message $ErrorRecord.Exception.Message
    try { Assert-D0M2TNoUnityResidue } catch { $reason = 'UnityProcessResidue' }
    if ([IO.File]::Exists($script:TerminalPath) -and -not $script:TerminalCommittedByInvocation)
    {
        throw 'HarnessFailure: refusing a pre-existing or partially published terminal.'
    }
    $contract = Get-D0M2TContract
    Assert-D0M2TContractIdentity -Contract $contract
    $dispatch = $null
    $unityIdentity = $null
    try
    {
        $dispatch = Read-D0M2TJsonObject -Path (Join-Path $script:RunRoot 'J0.Dispatch.json')
        Assert-D0M2TDispatchIdentity -Dispatch $dispatch
        Assert-D0M2TFrozenInputs -Dispatch $dispatch
    }
    catch { $reason = 'InputIdentityDrift'; $dispatch = $null }
    try
    {
        $unityIdentity = Read-D0M2TJsonObject -Path (Join-Path $script:RunRoot 'J0.UnityIdentity.json')
        [void](Resolve-D0M2TJ0UnityIdentity -UnityIdentity $unityIdentity -RequestedUnityPath $UnityPath)
    }
    catch { $reason = 'UnityIdentityMismatch'; $unityIdentity = $null }

    $protectedState = Get-D0M2TFailureProtectedState -Reason $reason
    $reason = [string]$protectedState.Reason
    $toolState = Get-D0M2TFailureToolState -Reason $reason
    $reason = [string]$toolState.Reason

    if ([IO.File]::Exists($script:TerminalPath))
    {
        if (-not $script:TerminalCommittedByInvocation)
        {
            throw 'HarnessFailure: refusing a terminal not committed by this invocation.'
        }
        $committed = Read-D0M2TCommittedTerminal -Contract $contract
        return [pscustomobject]@{ Terminal = $committed; ExitCode = 23 }
    }
    $processLog = Join-Path $script:RunRoot 'Process.Tarball.log'
    $experimentExists = [IO.File]::Exists($script:ExperimentPath)
    $terminal = [pscustomobject][ordered]@{
        Schema = 'D0M2T-Terminal-v1'
        RunId = $RunId
        Status = 'Inconclusive'
        Reason = $reason
        SelectedRoute = 'ImmutableTarball'
        SoleUnityConsumedSelector = 'Packages/manifest.json'
        Cases = @(New-D0M2TNotRunCases -Contract $contract)
        EvidenceFiles = @()
        EvidenceClosure = [pscustomobject]@{ Schema = 'D0M2T-EvidenceClosure-v1'; Verified = $false; Partial = $true; FileCount = 0; AggregateSha256 = '' }
        PartialRawInventory = (Get-D0M2TPartialRawInventory)
        Experiment = [pscustomobject]@{
            Path = if ($experimentExists) { $script:ExperimentPath.Replace('\', '/') } else { '' }
            Sha256 = if ($experimentExists) { Get-D0M2TFileSha256 -Path $script:ExperimentPath } else { '' }
            Status = 'Inconclusive'; Reason = $reason; ExitCode = -1; ProcessId = -1; DurationMilliseconds = 0
            ProcessLogPath = if ([IO.File]::Exists($processLog)) { $processLog.Replace('\', '/') } else { '' }
            ProcessLogSha256 = if ([IO.File]::Exists($processLog)) { Get-D0M2TFileSha256 -Path $processLog } else { '' }
        }
        StaticGates = @()
        Protected = $protectedState.Comparison
        ToolIdentity = $toolState.Comparison
        InputSha256 = if ($null -ne $dispatch) { $dispatch.InputSha256 } else { [pscustomobject]@{} }
        UnityIdentity = if ($null -ne $unityIdentity) { $unityIdentity } else { [pscustomobject]@{} }
        D1Authorized = $false
        ProductionInstallAdmission = 'NotEvaluated'
        DeclaredFullSemanticEligibility = $false
        NextGate = 'D0-M2R'
        Failure = [pscustomobject]@{ Message = $ErrorRecord.Exception.Message; Type = $ErrorRecord.Exception.GetType().FullName }
    }
    [void](Publish-D0M2TTerminal -Value $terminal -Contract $contract)
    $committed = Read-D0M2TCommittedTerminal -Contract $contract
    $exitCode = Get-D0M2TTerminalExitCode -Terminal $committed
    return [pscustomobject]@{ Terminal = $committed; ExitCode = $exitCode }
}

# 执行完整 J1→单 child fault matrix→J2 证据链。
function Invoke-D0M2TRun
{
    Assert-D0M2TPowerShellAst -Path $PSCommandPath
    Assert-D0M2TRunRoot -RequireInitialLayout
    Assert-D0M2TFreshEvidenceLeaves
    $contract = Get-D0M2TContract
    Assert-D0M2TContractIdentity -Contract $contract
    $dispatch = Read-D0M2TJsonObject -Path (Join-Path $script:RunRoot 'J0.Dispatch.json')
    Assert-D0M2TDispatchIdentity -Dispatch $dispatch
    Assert-D0M2TFrozenInputs -Dispatch $dispatch
    $unityIdentity = Read-D0M2TJsonObject -Path (Join-Path $script:RunRoot 'J0.UnityIdentity.json')
    $script:ResolvedUnity = Resolve-D0M2TJ0UnityIdentity -UnityIdentity $unityIdentity -RequestedUnityPath $UnityPath
    Assert-D0M2TNoUnityResidue

    $j0Snapshot = Read-D0M2TJsonObject -Path (Join-Path $script:RunRoot 'J0.ProtectedSnapshot.json')
    $j1Snapshot = Get-D0M2TProtectedSnapshot -RepositoryRoot $script:ProjectRoot
    $j1Comparison = Compare-D0M2TProtectedSnapshots -Before $j0Snapshot -After $j1Snapshot
    Write-D0M2TFreshJson -Path (Join-Path $script:RunRoot 'J1.ProtectedSnapshot.json') -Value $j1Snapshot
    Write-D0M2TFreshJson -Path (Join-Path $script:RunRoot 'J1.ProtectedComparison.json') -Value $j1Comparison
    if (-not [bool]$j1Comparison.Unchanged) { throw 'ProtectedPathDrift: J0/J1 mismatch.' }
    $j1Tool = Get-D0M2TToolSnapshot
    Write-D0M2TFreshJson -Path (Join-Path $script:RunRoot 'J1.ToolSnapshot.json') -Value $j1Tool
    if ([int]$j1Tool.FileCount -ne [int]$dispatch.ToolPreflightFileCount -or
        [string]$j1Tool.AggregateSha256 -cne [string]$dispatch.ToolPreflightAggregateSha256)
    {
        throw 'ToolIdentityDrift: J0/J1 tool mismatch.'
    }

    $staticGates = Invoke-D0M2TStaticGates
    $experiment = Invoke-D0M2TExperiment -Contract $contract
    $terminal = Complete-D0M2TRunEvidence -Contract $contract -Dispatch $dispatch -UnityIdentity $unityIdentity -J0Snapshot $j0Snapshot -J1ToolSnapshot $j1Tool -StaticGates $staticGates -Experiment $experiment
    return [pscustomobject]@{ Terminal = $terminal; ExitCode = Get-D0M2TTerminalExitCode -Terminal $terminal }
}

try
{
    . $script:Oracle
    $runResult = Invoke-D0M2TRun
    Write-Output ($runResult.Terminal | ConvertTo-Json -Depth 100)
    exit $runResult.ExitCode
}
catch
{
    $rootError = $_
    try
    {
        $failure = Publish-D0M2TFailure -ErrorRecord $rootError
        Write-Output ($failure.Terminal | ConvertTo-Json -Depth 100)
        [Console]::Error.WriteLine($rootError.Exception.ToString())
        exit $failure.ExitCode
    }
    catch
    {
        [Console]::Error.WriteLine("Failure evidence publication also failed: $($_.Exception)")
        [Console]::Error.WriteLine($rootError.Exception.ToString())
        $trustedTerminalExists = $script:TerminalCommittedByInvocation -and [IO.File]::Exists($script:TerminalPath)
        exit $(if ($trustedTerminalExists) { 23 } else { 90 })
    }
}
