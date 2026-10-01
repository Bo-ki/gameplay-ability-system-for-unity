#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^D0M2F-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$')]
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
$script:RunRoot = Join-Path $script:ProjectRoot ('TestResults/GasCodeGen/N2-G0-D0-M2F/' + $RunId)
$script:ContractRoot = Join-Path $PSScriptRoot 'D0M2F/Contracts'
$script:ContractChecker = Join-Path $script:ContractRoot 'Test-D0M2FContract.ps1'
$script:Oracle = Join-Path $script:ContractRoot 'D0M2FProtectedPathOracle.ps1'
$script:TarballRunner = Join-Path $PSScriptRoot 'D0M2F/Tarball/Run-D0M2F-TarballProbe.ps1'
$script:SourceGeneratorRunner = Join-Path $PSScriptRoot 'D0M2F/SourceGenerator/Run-D0M2F-SourceGeneratorProbe.ps1'
$script:AggregatePath = Join-Path $script:RunRoot 'D0M2F.aggregate.json'
$script:Utf8NoBom = [Text.UTF8Encoding]::new($false)

# 计算普通文件的原始 SHA-256 身份。
function Get-D0M2FFileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# 计算规范文本的 SHA-256，供工具树聚合身份使用。
function Get-D0M2FTextSha256
{
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text)

    $bytes = $script:Utf8NoBom.GetBytes($Text)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try
    {
        return [Convert]::ToHexString($algorithm.ComputeHash($bytes)).ToLowerInvariant()
    }
    finally
    {
        $algorithm.Dispose()
    }
}

# 以 fresh-only、UTF-8 无 BOM 和 flush-to-disk 语义写出证据。
function Write-D0M2FFreshText
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
function Write-D0M2FFreshJson
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$Value
    )

    $json = ($Value | ConvertTo-Json -Depth 100) + "`n"
    Write-D0M2FFreshText -Path $Path -Content $json
}

# 读取并要求顶层为 JSON object。
function Read-D0M2FJsonObject
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [IO.File]::Exists($Path)) { throw "JSON evidence is missing: $Path" }
    $value = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8) | ConvertFrom-Json -Depth 100
    if ($null -eq $value -or $value -isnot [pscustomobject]) { throw "JSON root must be an object: $Path" }
    return $value
}

# 冻结中央入口、合同与两条候选 harness 的逐文件原始身份。
function Get-D0M2FToolSnapshot
{
    $roots = @(
        $PSCommandPath,
        (Join-Path $PSScriptRoot 'D0M2F/Contracts'),
        (Join-Path $PSScriptRoot 'D0M2F/Tarball'),
        (Join-Path $PSScriptRoot 'D0M2F/SourceGenerator')
    )
    $files = [Collections.Generic.List[IO.FileInfo]]::new()
    foreach ($root in $roots)
    {
        if ([IO.File]::Exists($root))
        {
            $files.Add((Get-Item -LiteralPath $root))
            continue
        }
        if (-not [IO.Directory]::Exists($root)) { throw "Tool identity root is missing: $root" }
        $entries = @(Get-ChildItem -LiteralPath $root -Force -Recurse)
        foreach ($entry in $entries)
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
                Sha256 = Get-D0M2FFileSha256 $_.FullName
            }
        })
    $canonical = [string]::Join("`n", @($records | ForEach-Object { $_.Path + "`t" + $_.Length + "`t" + $_.Sha256 }))
    return [pscustomobject]@{
        Schema = 'D0M2F-ToolSnapshot-v1'
        FileCount = $records.Count
        AggregateSha256 = Get-D0M2FTextSha256 $canonical
        Records = $records
    }
}

# 比较 J1/J2 工具树身份，拒绝执行期间的脚本或 fixture 漂移。
function Compare-D0M2FToolSnapshots
{
    param(
        [Parameter(Mandatory = $true)]$Before,
        [Parameter(Mandatory = $true)]$After
    )

    $unchanged = [int]$Before.FileCount -eq [int]$After.FileCount -and
        [string]$Before.AggregateSha256 -ceq [string]$After.AggregateSha256
    return [pscustomobject]@{
        Schema = 'D0M2F-ToolComparison-v1'
        Unchanged = $unchanged
        BeforeFileCount = [int]$Before.FileCount
        AfterFileCount = [int]$After.FileCount
        BeforeAggregateSha256 = [string]$Before.AggregateSha256
        AfterAggregateSha256 = [string]$After.AggregateSha256
    }
}

# 解析 PowerShell AST 并拒绝语法错误。
function Assert-D0M2FPowerShellAst
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

# 以无 shell 拼接的 ArgumentList 执行受控子进程。
function Invoke-D0M2FProcess
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
    $startedAt = [DateTime]::UtcNow
    try
    {
        if (-not $process.Start()) { throw "Process failed to start: $FilePath" }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutMilliseconds)
        if ($timedOut)
        {
            try { $process.Kill($true); [void]$process.WaitForExit(30000) } catch { }
            if (-not $process.HasExited) { throw "Process timed out and its owned tree survived: $FilePath" }
        }
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            TimedOut = $timedOut
            DurationMilliseconds = [int]([DateTime]::UtcNow - $startedAt).TotalMilliseconds
            Stdout = $stdoutTask.GetAwaiter().GetResult()
            Stderr = $stderrTask.GetAwaiter().GetResult()
        }
    }
    finally
    {
        $process.Dispose()
    }
}

# 确认没有会污染 fixture、PackageCache 或 Bee 的活动 Unity 进程。
function Assert-D0M2FNoUnityResidue
{
    $names = @('Unity', 'UnityPackageManager', 'bee_backend', 'UnityShaderCompiler', 'UnityCrashHandler32', 'UnityCrashHandler64')
    $processes = @(Get-Process -Name $names -ErrorAction SilentlyContinue)
    if ($processes.Count -ne 0)
    {
        $detail = $processes | ForEach-Object { $_.ProcessName + ':' + $_.Id }
        throw 'UnityProcessResidue: ' + [string]::Join(',', $detail)
    }
}

# 在 U0 前一次性要求所有计划输出叶均不存在，避免运行一半才发现旧证据。
function Assert-D0M2FFreshEvidenceLeaves
{
    $leafNames = @(
        'J1.ProtectedSnapshot.json', 'J1.ProtectedComparison.json', 'J1.ToolSnapshot.json',
        'Static.Contract.log', 'Static.Tarball.log', 'Static.SourceGenerator.log',
        'Process.T.log', 'Process.S.log', 'Process.X.log',
        'ProbeT.json', 'ProbeS.json', 'ProbeX.json',
        'J2.ProtectedSnapshot.json', 'J2.ProtectedComparison.json',
        'J2.ToolSnapshot.json', 'J2.ToolComparison.json',
        'D0M2F.aggregate.json', 'D0M2F.aggregate.sha256'
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

# 验证 run root、owner sentinel 和三份 J0 证据未被替换。
function Assert-D0M2FRunRoot
{
    $expectedRoot = [IO.Path]::GetFullPath((Join-Path $script:ProjectRoot ('TestResults/GasCodeGen/N2-G0-D0-M2F/' + $RunId)))
    if (-not [IO.Directory]::Exists($expectedRoot) -or $expectedRoot -cne [IO.Path]::GetFullPath($script:RunRoot))
    {
        throw 'Run root is missing or escaped the fixed evidence root.'
    }
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
        if (-not [IO.File]::Exists($identity.Path) -or (Get-D0M2FFileSha256 $identity.Path) -cne $identity.Expected)
        {
            throw "J0 evidence identity drift: $($identity.Path)"
        }
    }
}

# 验证 ADR、Spec08、计划和 D0-M1 证据 raw SHA 与 J0 完全一致。
function Assert-D0M2FFrozenInputs
{
    param([Parameter(Mandatory = $true)]$Dispatch)

    $paths = [ordered]@{
        Adr0001 = 'docs/adr/0001-codegen-single-install-root-and-install-envelope.md'
        Spec08 = '方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md'
        Plan = 'docs/reviews/RuntimeV1-完成后停顿审查与V1.1-D0-M2F单轮计划.md'
        D0M1Evidence = 'Tools/Tests/GasCodeGen/N2-G0-D0-M1-evidence.md'
    }
    foreach ($name in $paths.Keys)
    {
        $path = Join-Path $script:ProjectRoot $paths[$name]
        $expected = [string]$Dispatch.InputSha256.$name
        if ((Get-D0M2FFileSha256 $path) -cne $expected) { throw "InputIdentityDrift: $name" }
    }
}

# 调用公共 checker 验证一个 typed probe JSON。
function Assert-D0M2FProbeContract
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][ValidateSet('T', 'S', 'X')][string]$ProbeId
    )

    $validation = Invoke-D0M2FProcess -FilePath (Join-Path $PSHOME 'pwsh.exe') -TimeoutMilliseconds 300000 -Arguments @(
        '-NoProfile', '-File', $script:ContractChecker,
        '-Path', $Path, '-ExpectedProbeId', $ProbeId, '-ExpectedRunId', $RunId)
    if ($validation.TimedOut -or $validation.ExitCode -ne 0)
    {
        throw "SchemaViolation: $ProbeId checker failed. $($validation.Stderr)"
    }
}

# 执行一个 route harness，并区分有效路线否决与 harness 故障。
function Invoke-D0M2FProbe
{
    param(
        [Parameter(Mandatory = $true)][string]$Runner,
        [Parameter(Mandatory = $true)][ValidateSet('T', 'S', 'X')][string]$ProbeId,
        [Parameter(Mandatory = $true)][ValidateSet('Feasibility', 'SelectorConflict')][string]$Mode,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][int]$TimeoutMilliseconds
    )

    if ([IO.File]::Exists($OutputPath) -or [IO.Directory]::Exists($OutputPath))
    {
        throw "Fresh evidence target already exists: $OutputPath"
    }
    Assert-D0M2FNoUnityResidue
    $arguments = @('-NoProfile', '-File', $Runner, '-UnityPath', $script:ResolvedUnity,
        '-OutputPath', $OutputPath, '-RunId', $RunId, '-Mode', $Mode)
    if ($KeepFixtures) { $arguments += '-KeepFixture' }
    $execution = Invoke-D0M2FProcess -FilePath (Join-Path $PSHOME 'pwsh.exe') -Arguments $arguments -TimeoutMilliseconds $TimeoutMilliseconds -WorkingDirectory $script:ProjectRoot
    $processLog = Join-Path $script:RunRoot ("Process.$ProbeId.log")
    $logContent = "ExitCode=$($execution.ExitCode)`nTimedOut=$($execution.TimedOut)`nDurationMilliseconds=$($execution.DurationMilliseconds)`n---STDOUT---`n$($execution.Stdout)`n---STDERR---`n$($execution.Stderr)"
    Write-D0M2FFreshText -Path $processLog -Content $logContent
    Assert-D0M2FNoUnityResidue
    if ($execution.TimedOut) { throw "Process timed out: $ProbeId" }
    if (-not [IO.File]::Exists($OutputPath)) { throw "HarnessFailure: $ProbeId output is missing." }
    Assert-D0M2FProbeContract -Path $OutputPath -ProbeId $ProbeId
    $result = Read-D0M2FJsonObject -Path $OutputPath
    if (($result.Status -ceq 'Passed') -ne ($execution.ExitCode -eq 0))
    {
        throw "HarnessFailure: $ProbeId status/exit pairing is invalid."
    }
    return [pscustomobject]@{
        Result = $result
        Execution = $execution
        Path = $OutputPath
        Sha256 = Get-D0M2FFileSha256 $OutputPath
        ProcessLogPath = $processLog
        ProcessLogSha256 = Get-D0M2FFileSha256 $processLog
    }
}

# 将静态门输出持久化到 fresh run root。
function Invoke-D0M2FStaticGates
{
    $gates = @(
        @{ Name = 'Contract'; Script = $script:ContractChecker; Args = @('-StaticOnly') },
        @{ Name = 'Tarball'; Script = (Join-Path $PSScriptRoot 'D0M2F/Tarball/Test-D0M2F-TarballStatic.ps1'); Args = @() },
        @{ Name = 'SourceGenerator'; Script = (Join-Path $PSScriptRoot 'D0M2F/SourceGenerator/Test-D0M2F-SourceGeneratorStatic.ps1'); Args = @() }
    )
    $results = [Collections.Generic.List[object]]::new()
    foreach ($gate in $gates)
    {
        if (-not [IO.File]::Exists($gate.Script)) { throw "Static gate script is missing: $($gate.Script)" }
        Assert-D0M2FPowerShellAst -Path $gate.Script
        $arguments = @('-NoProfile', '-File', $gate.Script) + @($gate.Args)
        $execution = Invoke-D0M2FProcess -FilePath (Join-Path $PSHOME 'pwsh.exe') -TimeoutMilliseconds 600000 -Arguments $arguments
        $logPath = Join-Path $script:RunRoot ('Static.' + $gate.Name + '.log')
        Write-D0M2FFreshText -Path $logPath -Content ($execution.Stdout + "`n---STDERR---`n" + $execution.Stderr)
        if ($execution.TimedOut -or $execution.ExitCode -ne 0) { throw "Static gate failed: $($gate.Name)" }
        $results.Add([pscustomobject]@{ Name = $gate.Name; Passed = $true; LogPath = $logPath.Replace('\', '/'); LogSha256 = Get-D0M2FFileSha256 $logPath })
    }
    return $results.ToArray()
}

# 从选中路线的 X 证据提取唯一 Unity-consumed selector。
function Get-D0M2FSoleSelector
{
    param(
        [Parameter(Mandatory = $true)][string]$Route,
        [Parameter(Mandatory = $true)]$XResult
    )

    if ($Route -ceq 'ImmutableTarball')
    {
        $matches = @($XResult.RoleObservations | Where-Object {
                [string]$_.ConsumerAuthority -ceq 'SoleUnitySelector' -and
                [string]$_.Role -ceq 'Authority' -and
                [IO.Path]::GetFileName([string]$_.Path) -ceq 'manifest.json' })
        if ($matches.Count -ne 1) { throw 'EvidenceIncomplete: exactly one Unity-consumed selector was not proven.' }
        return 'Packages/manifest.json'
    }
    else
    {
        $matches = @($XResult.RoleObservations | Where-Object {
                [string]$_.Path -like '*.additionalfile' -and [string]$_.Role -ceq 'Authority' -and [bool]$_.UnityConsumed })
    }
    if ($matches.Count -ne 1) { throw 'EvidenceIncomplete: exactly one Unity-consumed selector was not proven.' }
    return [string]$matches[0].Path
}

# 在落盘前后验证中央 aggregate 的终态不变量与所引用证据身份。
function Assert-D0M2FAggregateObject
{
    param([Parameter(Mandatory = $true)]$Value)

    if ([string]$Value.Schema -cne 'D0M2F-Aggregate-v1' -or [string]$Value.RunId -cne $RunId)
    {
        throw 'Aggregate schema or RunId mismatch.'
    }
    if ([string]$Value.Status -cnotin @('CandidateSelected', 'NoViableRoute', 'Inconclusive'))
    {
        throw "Aggregate status is invalid: $($Value.Status)"
    }
    if ([bool]$Value.D1Authorized -or [string]$Value.ProductionInstallAdmission -cne 'NotEvaluated' -or
        [bool]$Value.DeclaredFullSemanticEligibility)
    {
        throw 'Aggregate illegally authorizes post-D0-M2F work.'
    }
    $probes = @($Value.Probes)
    if (@($probes | Group-Object ProbeId | Where-Object Count -gt 1).Count -ne 0) { throw 'Aggregate repeats a probe.' }
    foreach ($probe in $probes)
    {
        if ([string]$probe.ProbeId -cnotin @('T', 'S', 'X')) { throw 'Aggregate contains an unknown probe.' }
        $path = [string]$probe.Path
        if (-not [IO.File]::Exists($path) -or (Get-D0M2FFileSha256 $path) -cne [string]$probe.Sha256)
        {
            throw "Aggregate probe identity mismatch: $($probe.ProbeId)"
        }
        $processLogPath = [string]$probe.ProcessLogPath
        if (-not [string]::IsNullOrWhiteSpace($processLogPath) -and
            (-not [IO.File]::Exists($processLogPath) -or (Get-D0M2FFileSha256 $processLogPath) -cne [string]$probe.ProcessLogSha256))
        {
            throw "Aggregate process-log identity mismatch: $($probe.ProbeId)"
        }
    }
    foreach ($gate in @($Value.StaticGates))
    {
        $logPath = [string]$gate.LogPath
        if (-not [IO.File]::Exists($logPath) -or (Get-D0M2FFileSha256 $logPath) -cne [string]$gate.LogSha256)
        {
            throw "Aggregate static-gate identity mismatch: $($gate.Name)"
        }
    }
    $processLogs = @($Value.ProcessLogs)
    if (@($processLogs | Group-Object ProbeId | Where-Object Count -gt 1).Count -ne 0) { throw 'Aggregate repeats a process log.' }
    foreach ($processLog in $processLogs)
    {
        $path = [string]$processLog.Path
        if ([string]$processLog.ProbeId -cnotin @('T', 'S', 'X') -or
            -not [IO.File]::Exists($path) -or (Get-D0M2FFileSha256 $path) -cne [string]$processLog.Sha256)
        {
            throw "Aggregate process-log evidence mismatch: $($processLog.ProbeId)"
        }
    }
    if ([string]$Value.Status -ceq 'CandidateSelected')
    {
        $ids = @($probes | ForEach-Object { [string]$_.ProbeId } | Sort-Object)
        if ([string]::Join(',', $ids) -cne 'S,T,X' -or
            @($probes | Where-Object { $_.ProbeId -ceq 'X' -and $_.Status -ceq 'Passed' }).Count -ne 1 -or
            [string]::IsNullOrWhiteSpace([string]$Value.SelectedRoute) -or
            [string]::IsNullOrWhiteSpace([string]$Value.SoleUnityConsumedSelector) -or
            -not [bool]$Value.HumanConfirmationRequired -or
            -not [bool]$Value.Protected.Unchanged -or -not [bool]$Value.ToolIdentity.Unchanged)
        {
            throw 'CandidateSelected aggregate invariants failed.'
        }
    }
    elseif (-not [string]::IsNullOrWhiteSpace([string]$Value.SelectedRoute) -or
        -not [string]::IsNullOrWhiteSpace([string]$Value.SoleUnityConsumedSelector) -or
        [bool]$Value.HumanConfirmationRequired)
    {
        throw 'Non-candidate aggregate retained candidate authority.'
    }
}

# fresh 写 aggregate，读回复核并发布独立 SHA sidecar。
function Publish-D0M2FAggregate
{
    param([Parameter(Mandatory = $true)]$Value)

    Assert-D0M2FAggregateObject -Value $Value
    Write-D0M2FFreshJson -Path $script:AggregatePath -Value $Value
    $readBack = Read-D0M2FJsonObject -Path $script:AggregatePath
    Assert-D0M2FAggregateObject -Value $readBack
    $sha256 = Get-D0M2FFileSha256 $script:AggregatePath
    Write-D0M2FFreshText -Path (Join-Path $script:RunRoot 'D0M2F.aggregate.sha256') -Content ($sha256 + "  D0M2F.aggregate.json`n")
    return $sha256
}

# 从已经落盘的 probe 叶构造失败态 partial evidence 引用。
function Get-D0M2FExistingProbeRows
{
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($probeId in @('T', 'S', 'X'))
    {
        $path = Join-Path $script:RunRoot ("Probe$probeId.json")
        if (-not [IO.File]::Exists($path)) { continue }
        try
        {
            $result = Read-D0M2FJsonObject -Path $path
            $processLogPath = Join-Path $script:RunRoot ("Process.$probeId.log")
            $hasProcessLog = [IO.File]::Exists($processLogPath)
            $rows.Add([pscustomobject]@{
                    ProbeId = $probeId; Status = [string]$result.Status; Reason = [string]$result.Reason
                    Path = $path.Replace('\', '/'); Sha256 = Get-D0M2FFileSha256 $path
                    ProcessLogPath = if ($hasProcessLog) { $processLogPath.Replace('\', '/') } else { '' }
                    ProcessLogSha256 = if ($hasProcessLog) { Get-D0M2FFileSha256 $processLogPath } else { '' }
                    ExitCode = $null; DurationMilliseconds = $null
                })
        }
        catch { }
    }
    return $rows.ToArray()
}

# 枚举失败前已落盘的 static logs，并绑定其当前原始身份。
function Get-D0M2FExistingStaticRows
{
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($name in @('Contract', 'Tarball', 'SourceGenerator'))
    {
        $path = Join-Path $script:RunRoot ("Static.$name.log")
        if ([IO.File]::Exists($path))
        {
            $rows.Add([pscustomobject]@{ Name = $name; Passed = $false; LogPath = $path.Replace('\', '/'); LogSha256 = Get-D0M2FFileSha256 $path })
        }
    }
    return $rows.ToArray()
}

# 枚举所有 process logs，包括尚未来得及生成 Probe JSON 的 timeout/failure。
function Get-D0M2FExistingProcessRows
{
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($probeId in @('T', 'S', 'X'))
    {
        $path = Join-Path $script:RunRoot ("Process.$probeId.log")
        if ([IO.File]::Exists($path))
        {
            $rows.Add([pscustomobject]@{ ProbeId = $probeId; Path = $path.Replace('\', '/'); Sha256 = Get-D0M2FFileSha256 $path })
        }
    }
    return $rows.ToArray()
}

# 将顶层异常映射为冻结的 Inconclusive reason。
function Get-D0M2FFailureReason
{
    param([Parameter(Mandatory = $true)][string]$Message)

    foreach ($reason in @('ProtectedPathDrift', 'UnityProcessResidue', 'InputIdentityDrift', 'SchemaViolation',
            'CaseIdSetMismatch', 'FixtureBoundaryViolation', 'UnityIdentityMismatch', 'EvidenceIncomplete',
            'WatcherFailure', 'CleanupFailure'))
    {
        if ($Message.StartsWith($reason, [StringComparison]::Ordinal)) { return $reason }
    }
    if ($Message -match 'timed out') { return 'TimeBoxExceeded' }
    if ($Message -match 'identity drift') { return 'InputIdentityDrift' }
    if ($Message -match 'Fresh evidence|missing|not proven') { return 'EvidenceIncomplete' }
    return 'HarnessFailure'
}

# 异常路径尽力完成 residue、J2 与工具身份收口，并发布 typed Inconclusive aggregate。
function Publish-D0M2FFailure
{
    param([Parameter(Mandatory = $true)]$ErrorRecord)

    $reason = Get-D0M2FFailureReason -Message $ErrorRecord.Exception.Message
    try { Assert-D0M2FNoUnityResidue } catch { $reason = 'UnityProcessResidue' }
    $dispatch = Read-D0M2FJsonObject -Path (Join-Path $script:RunRoot 'J0.Dispatch.json')
    $unityIdentity = Read-D0M2FJsonObject -Path (Join-Path $script:RunRoot 'J0.UnityIdentity.json')
    try { Assert-D0M2FFrozenInputs -Dispatch $dispatch } catch { $reason = 'InputIdentityDrift' }

    $protected = [pscustomobject]@{ Schema = 'D0M2F-ProtectedComparison-v1'; Unchanged = $false; Failure = 'NotEvaluated' }
    if ($null -ne (Get-Command Get-D0M2FProtectedSnapshot -ErrorAction SilentlyContinue))
    {
        try
        {
            $j0 = Read-D0M2FJsonObject -Path (Join-Path $script:RunRoot 'J0.ProtectedSnapshot.json')
            $j2Path = Join-Path $script:RunRoot 'J2.ProtectedSnapshot.json'
            $comparisonPath = Join-Path $script:RunRoot 'J2.ProtectedComparison.json'
            $j2 = Get-D0M2FProtectedSnapshot -RepositoryRoot $script:ProjectRoot
            $protected = Compare-D0M2FProtectedSnapshots -Before $j0 -After $j2
            if (-not [IO.File]::Exists($j2Path)) { Write-D0M2FFreshJson -Path $j2Path -Value $j2 }
            if (-not [IO.File]::Exists($comparisonPath)) { Write-D0M2FFreshJson -Path $comparisonPath -Value $protected }
            if (-not $protected.Unchanged) { $reason = 'ProtectedPathDrift' }
        }
        catch { $reason = 'ProtectedPathDrift' }
    }

    $toolIdentity = [pscustomobject]@{ Schema = 'D0M2F-ToolComparison-v1'; Unchanged = $false; Failure = 'NotEvaluated' }
    try
    {
        $j1ToolPath = Join-Path $script:RunRoot 'J1.ToolSnapshot.json'
        if ([IO.File]::Exists($j1ToolPath))
        {
            $j1Tool = Read-D0M2FJsonObject $j1ToolPath
            $j2Tool = Get-D0M2FToolSnapshot
            $toolIdentity = Compare-D0M2FToolSnapshots -Before $j1Tool -After $j2Tool
            $j2ToolPath = Join-Path $script:RunRoot 'J2.ToolSnapshot.json'
            $toolComparisonPath = Join-Path $script:RunRoot 'J2.ToolComparison.json'
            if (-not [IO.File]::Exists($j2ToolPath)) { Write-D0M2FFreshJson -Path $j2ToolPath -Value $j2Tool }
            if (-not [IO.File]::Exists($toolComparisonPath)) { Write-D0M2FFreshJson -Path $toolComparisonPath -Value $toolIdentity }
            if (-not $toolIdentity.Unchanged) { $reason = 'InputIdentityDrift' }
        }
    }
    catch { $reason = 'InputIdentityDrift' }

    if (-not [IO.File]::Exists($script:AggregatePath))
    {
        $failure = [pscustomobject][ordered]@{
            Schema = 'D0M2F-Aggregate-v1'; RunId = $RunId; Status = 'Inconclusive'; Reason = $reason
            SelectedRoute = ''; SoleUnityConsumedSelector = ''; AuthorityDerivedCache = @()
            Probes = @(Get-D0M2FExistingProbeRows); StaticGates = @(Get-D0M2FExistingStaticRows)
            ProcessLogs = @(Get-D0M2FExistingProcessRows); Protected = $protected; ToolIdentity = $toolIdentity
            InputSha256 = $dispatch.InputSha256; Unity = $unityIdentity
            HumanConfirmationRequired = $false; D1Authorized = $false
            ProductionInstallAdmission = 'NotEvaluated'; DeclaredFullSemanticEligibility = $false
            Failure = [pscustomobject]@{ Message = $ErrorRecord.Exception.Message; Type = $ErrorRecord.Exception.GetType().FullName }
        }
        [void](Publish-D0M2FAggregate -Value $failure)
    }
}

# 完整性类 Inconclusive 必须立即停链，路线可行性未知与 time-box 才允许继续取证。
function Assert-D0M2FProbeIntegrity
{
    param([Parameter(Mandatory = $true)]$Probe)

    $fatalReasons = @(
        'HarnessFailure', 'SchemaViolation', 'CaseIdSetMismatch', 'ProtectedPathDrift',
        'UnityProcessResidue', 'FixtureBoundaryViolation', 'InputIdentityDrift',
        'EvidenceIncomplete', 'CleanupFailure', 'UnityIdentityMismatch', 'WatcherFailure'
    )
    if ([string]$Probe.Result.Status -ceq 'Inconclusive' -and [string]$Probe.Result.Reason -cin $fatalReasons)
    {
        throw "$($Probe.Result.Reason): probe $($Probe.Result.ProbeId) integrity failed."
    }
}

# 根据 T/S typed 结果决定 provisional route，并按需执行唯一 X。
function Invoke-D0M2FCandidateDecision
{
    param([Parameter(Mandatory = $true)]$T, [Parameter(Mandatory = $true)]$S)

    $route = ''
    $conclusive = [string]$T.Result.Status -cin @('Passed', 'HardRejected') -and
        [string]$S.Result.Status -cin @('Passed', 'HardRejected')
    if ($conclusive -and $T.Result.Status -ceq 'Passed') { $route = 'ImmutableTarball' }
    elseif ($conclusive -and $T.Result.Status -ceq 'HardRejected' -and $S.Result.Status -ceq 'Passed') { $route = 'StableGraphSourceGenerator' }

    $x = $null; $status = 'Inconclusive'; $reason = 'EvidenceIncomplete'; $selector = ''; $roles = @()
    if (-not [string]::IsNullOrWhiteSpace($route))
    {
        $runner = if ($route -ceq 'ImmutableTarball') { $script:TarballRunner } else { $script:SourceGeneratorRunner }
        $x = Invoke-D0M2FProbe -Runner $runner -ProbeId X -Mode SelectorConflict -OutputPath (Join-Path $script:RunRoot 'ProbeX.json') -TimeoutMilliseconds 3600000
        if ($x.Result.Status -ceq 'Passed')
        {
            $selector = Get-D0M2FSoleSelector -Route $route -XResult $x.Result
            $roles = @($x.Result.RoleObservations); $status = 'CandidateSelected'; $reason = 'None'
        }
        else { $reason = [string]$x.Result.Reason }
    }
    elseif ($conclusive -and $T.Result.Status -ceq 'HardRejected' -and $S.Result.Status -ceq 'HardRejected')
    {
        $status = 'NoViableRoute'; $reason = 'BothRoutesHardRejected'
    }
    else
    {
        $failed = @($T, $S) | Where-Object { [string]$_.Result.Status -cnotin @('Passed', 'HardRejected') } | Select-Object -First 1
        $reason = [string]$failed.Result.Reason
    }
    return [pscustomobject]@{ Route = $route; X = $x; Status = $status; Reason = $reason; Selector = $selector; Roles = @($roles) }
}

# 完成 J2 身份复核并发布正常路径的最终 aggregate。
function Complete-D0M2FRunEvidence
{
    param($Dispatch, $UnityIdentity, $J0Snapshot, $J1ToolSnapshot, $StaticGates, $T, $S, $Decision)

    $j2Snapshot = Get-D0M2FProtectedSnapshot -RepositoryRoot $script:ProjectRoot
    $protected = Compare-D0M2FProtectedSnapshots -Before $J0Snapshot -After $j2Snapshot
    Write-D0M2FFreshJson -Path (Join-Path $script:RunRoot 'J2.ProtectedSnapshot.json') -Value $j2Snapshot
    Write-D0M2FFreshJson -Path (Join-Path $script:RunRoot 'J2.ProtectedComparison.json') -Value $protected
    $j2Tool = Get-D0M2FToolSnapshot
    $toolIdentity = Compare-D0M2FToolSnapshots -Before $J1ToolSnapshot -After $j2Tool
    Write-D0M2FFreshJson -Path (Join-Path $script:RunRoot 'J2.ToolSnapshot.json') -Value $j2Tool
    Write-D0M2FFreshJson -Path (Join-Path $script:RunRoot 'J2.ToolComparison.json') -Value $toolIdentity
    Assert-D0M2FRunRoot
    Assert-D0M2FFrozenInputs -Dispatch $Dispatch
    if ((Get-D0M2FFileSha256 $script:ResolvedUnity) -cne [string]$UnityIdentity.UnityFileSha256) { throw 'UnityIdentityMismatch: executable bytes changed during U0.' }

    $status = [string]$Decision.Status; $reason = [string]$Decision.Reason
    $route = [string]$Decision.Route; $selector = [string]$Decision.Selector; $roles = @($Decision.Roles)
    if (-not $protected.Unchanged -or -not $toolIdentity.Unchanged)
    {
        $status = 'Inconclusive'; $reason = if (-not $protected.Unchanged) { 'ProtectedPathDrift' } else { 'InputIdentityDrift' }
        $route = ''; $selector = ''; $roles = @()
    }
    $probeExecutions = @($T, $S) + @($Decision.X | Where-Object { $null -ne $_ })
    $probeRows = $probeExecutions | ForEach-Object {
        [pscustomobject]@{
            ProbeId = [string]$_.Result.ProbeId; Status = [string]$_.Result.Status; Reason = [string]$_.Result.Reason
            Path = $_.Path.Replace('\', '/'); Sha256 = $_.Sha256
            ProcessLogPath = $_.ProcessLogPath.Replace('\', '/'); ProcessLogSha256 = $_.ProcessLogSha256
            ExitCode = $_.Execution.ExitCode; DurationMilliseconds = $_.Execution.DurationMilliseconds
        }
    }
    $processRows = $probeExecutions | ForEach-Object {
        [pscustomobject]@{ ProbeId = [string]$_.Result.ProbeId; Path = $_.ProcessLogPath.Replace('\', '/'); Sha256 = $_.ProcessLogSha256 }
    }
    $aggregate = [pscustomobject][ordered]@{
        Schema = 'D0M2F-Aggregate-v1'; RunId = $RunId; Status = $status; Reason = $reason
        SelectedRoute = if ($status -ceq 'CandidateSelected') { $route } else { '' }; SoleUnityConsumedSelector = $selector
        AuthorityDerivedCache = @($roles); Probes = @($probeRows); StaticGates = @($StaticGates); ProcessLogs = @($processRows)
        Protected = $protected; ToolIdentity = $toolIdentity; InputSha256 = $Dispatch.InputSha256; Unity = $UnityIdentity
        HumanConfirmationRequired = $status -ceq 'CandidateSelected'; D1Authorized = $false
        ProductionInstallAdmission = 'NotEvaluated'; DeclaredFullSemanticEligibility = $false
    }
    [void](Publish-D0M2FAggregate -Value $aggregate)
    return $aggregate
}

# 执行完整 J1→U0→J2 单次证据链。
function Invoke-D0M2FRun
{
    Assert-D0M2FPowerShellAst -Path $PSCommandPath
    Assert-D0M2FRunRoot
    $dispatch = Read-D0M2FJsonObject -Path (Join-Path $script:RunRoot 'J0.Dispatch.json')
    Assert-D0M2FFrozenInputs -Dispatch $dispatch
    $unityIdentity = Read-D0M2FJsonObject -Path (Join-Path $script:RunRoot 'J0.UnityIdentity.json')
    $script:ResolvedUnity = if ([string]::IsNullOrWhiteSpace($UnityPath)) { [IO.Path]::GetFullPath([string]$unityIdentity.UnityPath) } else { [IO.Path]::GetFullPath($UnityPath) }
    if (-not [IO.File]::Exists($script:ResolvedUnity) -or (Get-D0M2FFileSha256 $script:ResolvedUnity) -cne [string]$unityIdentity.UnityFileSha256) { throw 'UnityIdentityMismatch: executable bytes do not match J0.' }
    Assert-D0M2FNoUnityResidue
    Assert-D0M2FFreshEvidenceLeaves

    $j0Snapshot = Read-D0M2FJsonObject -Path (Join-Path $script:RunRoot 'J0.ProtectedSnapshot.json')
    $j1Snapshot = Get-D0M2FProtectedSnapshot -RepositoryRoot $script:ProjectRoot
    $j1Comparison = Compare-D0M2FProtectedSnapshots -Before $j0Snapshot -After $j1Snapshot
    Write-D0M2FFreshJson -Path (Join-Path $script:RunRoot 'J1.ProtectedSnapshot.json') -Value $j1Snapshot
    Write-D0M2FFreshJson -Path (Join-Path $script:RunRoot 'J1.ProtectedComparison.json') -Value $j1Comparison
    if (-not $j1Comparison.Unchanged) { throw 'ProtectedPathDrift: J0/J1 mismatch.' }
    $j1Tool = Get-D0M2FToolSnapshot
    Write-D0M2FFreshJson -Path (Join-Path $script:RunRoot 'J1.ToolSnapshot.json') -Value $j1Tool
    # 强制 J0 授权身份与实际执行工具一致，关闭准备与 U0 之间的漂移窗口。
    if ([int]$j1Tool.FileCount -ne [int]$dispatch.ToolPreflightFileCount -or
        [string]$j1Tool.AggregateSha256 -cne [string]$dispatch.ToolPreflightAggregateSha256)
    {
        throw 'InputIdentityDrift: J0/J1 tool mismatch.'
    }

    $staticGates = Invoke-D0M2FStaticGates
    $t = Invoke-D0M2FProbe -Runner $script:TarballRunner -ProbeId T -Mode Feasibility -OutputPath (Join-Path $script:RunRoot 'ProbeT.json') -TimeoutMilliseconds 5400000
    Assert-D0M2FProbeIntegrity -Probe $t
    $s = Invoke-D0M2FProbe -Runner $script:SourceGeneratorRunner -ProbeId S -Mode Feasibility -OutputPath (Join-Path $script:RunRoot 'ProbeS.json') -TimeoutMilliseconds 7200000
    Assert-D0M2FProbeIntegrity -Probe $s
    $decision = Invoke-D0M2FCandidateDecision -T $t -S $s
    $aggregate = Complete-D0M2FRunEvidence -Dispatch $dispatch -UnityIdentity $unityIdentity -J0Snapshot $j0Snapshot -J1ToolSnapshot $j1Tool -StaticGates $staticGates -T $t -S $s -Decision $decision
    return [pscustomobject]@{ ExitCode = $(if ($aggregate.Status -ceq 'CandidateSelected') { 0 } else { 1 }); Aggregate = $aggregate }
}

try
{
    . $script:Oracle
    $runResult = Invoke-D0M2FRun
    Write-Output ($runResult.Aggregate | ConvertTo-Json -Depth 100)
    exit $runResult.ExitCode
}
catch
{
    $rootError = $_
    try { Publish-D0M2FFailure -ErrorRecord $rootError }
    catch { [Console]::Error.WriteLine("Failure evidence publication also failed: $($_.Exception)") }
    [Console]::Error.WriteLine($rootError.Exception.ToString())
    exit 1
}
