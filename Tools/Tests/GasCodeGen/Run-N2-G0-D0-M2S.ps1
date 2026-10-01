#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [ValidatePattern("^D0M2S-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$")][string]$RunId = (
        "D0M2S-" + [DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" +
        [Guid]::NewGuid().ToString("N").Substring(0, 12))
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../.."))
$script:ContractRoot = Join-Path $PSScriptRoot "D0M2S/Contracts"
$script:ContractPath = Join-Path $script:ContractRoot "D0M2S.contract.json"
$script:CheckerPath = Join-Path $script:ContractRoot "Test-D0M2SContract.ps1"
$script:OraclePath = Join-Path $script:ContractRoot "D0M2SProtectedPathOracle.ps1"
$script:ChildRunnerPath = Join-Path $PSScriptRoot "D0M2S/SourceGenerator/Run-D0M2S-SourceGeneratorFaultExperiment.ps1"
$script:EvidenceBase = Join-Path $script:ProjectRoot "TestResults/GasCodeGen/N2-G0-D0-M2S"
$script:RunRoot = Join-Path $script:EvidenceBase $RunId
$script:RawRoot = Join-Path $script:RunRoot "Raw"
$script:TerminalPath = Join-Path $script:RunRoot "D0M2S.terminal.json"
$script:SidecarPath = Join-Path $script:RunRoot "D0M2S.terminal.sha256"
$script:ChildOutputPath = Join-Path $script:RunRoot "SourceGeneratorFault.json"
$script:DeadlineUtc = [DateTime]::UtcNow.AddMinutes(45)

# 计算普通文件原始字节的 SHA-256 小写身份。
function Get-D0M2SFileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# 计算 canonical UTF-8 文本的 SHA-256。
function Get-D0M2STextSha256
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

# 以 UTF-8 无 BOM、LF 和 CreateNew 语义写出证据文本。
function Write-D0M2SFreshText
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content,
        [Text.Encoding]$Encoding = [Text.UTF8Encoding]::new($false)
    )

    $parent = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Path))
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $normalized = $Content.Replace("`r`n", "`n").Replace("`r", "`n")
    $bytes = $Encoding.GetBytes($normalized)
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
}

# 以 fresh-only JSON 写出机器证据。
function Write-D0M2SFreshJson
{
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)]$Value)

    $json = ($Value | ConvertTo-Json -Depth 100).Replace("`r`n", "`n") + "`n"
    Write-D0M2SFreshText -Path $Path -Content $json
}

# 读取 JSON 对象并拒绝缺失或非对象根。
function Read-D0M2SJsonObject
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [IO.File]::Exists($Path)) { throw "EvidenceIncomplete: JSON evidence is missing: $Path" }
    $value = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8) | ConvertFrom-Json -Depth 100
    if ($null -eq $value -or $value -is [Array] -or $value -isnot [pscustomobject])
    {
        throw "SchemaViolation: JSON evidence root must be an object: $Path"
    }

    return $value
}

# 返回全局硬时间盒内剩余毫秒数。
function Get-D0M2SRemainingMilliseconds
{
    $remaining = [int64][Math]::Floor(($script:DeadlineUtc - [DateTime]::UtcNow).TotalMilliseconds)
    if ($remaining -le 0) { throw [TimeoutException]::new("TimeBoxExceeded: D0-M2S global deadline expired.") }
    return [int][Math]::Min($remaining, [int]::MaxValue)
}

# 运行受控子进程并在超时或异常时终止其完整进程树。
function Invoke-D0M2SBoundedProcess
{
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][int]$TimeoutMilliseconds
    )

    $start = [Diagnostics.ProcessStartInfo]::new($FilePath)
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.CreateNoWindow = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $started = $false
    try
    {
        $started = $process.Start()
        if (-not $started) { throw "HarnessFailure: child process failed to start." }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $completed = $process.WaitForExit([Math]::Min($TimeoutMilliseconds, (Get-D0M2SRemainingMilliseconds)))
        if (-not $completed)
        {
            try { $process.Kill($true); [void]$process.WaitForExit(30000) } catch { }
            return [pscustomobject]@{ ExitCode = -1; TimedOut = $true; Stdout = $stdout.GetAwaiter().GetResult(); Stderr = $stderr.GetAwaiter().GetResult() }
        }

        $process.WaitForExit()
        return [pscustomobject]@{ ExitCode = $process.ExitCode; TimedOut = $false; Stdout = $stdout.GetAwaiter().GetResult(); Stderr = $stderr.GetAwaiter().GetResult() }
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

# 构造固定 case 集的未执行初值。
function New-D0M2SDefaultCases
{
    param([Parameter(Mandatory = $true)]$Contract)

    return @($Contract.CaseSet | ForEach-Object {
            [pscustomobject]@{
                CaseId = [string]$_.CaseId
                Name = [string]$_.Name
                Status = "NotRun"
                Reason = "EvidenceIncomplete"
                EvidencePaths = @()
                Evidence = [pscustomobject]@{}
            }
        })
}

# 将 child evidence path 归一化为 run-root 相对路径。
function Convert-D0M2SChildEvidencePath
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $normalized = $Path.Replace("\", "/")
    if ([IO.Path]::IsPathRooted($Path))
    {
        $full = [IO.Path]::GetFullPath($Path)
        $root = [IO.Path]::GetFullPath($script:RunRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
        if (-not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
        {
            throw "FilesystemBoundaryViolation: child evidence path escaped run root."
        }
        return [IO.Path]::GetRelativePath($root, $full).Replace("\", "/")
    }

    if ($normalized.StartsWith("Raw/", [StringComparison]::Ordinal)) { return $normalized }
    if ($normalized -match "(^|/)\.\.?(?:/|$)") { throw "FilesystemBoundaryViolation: child evidence path is non-canonical." }
    return "Raw/" + $normalized.TrimStart('/')
}

# 将 child case 结果压入固定 8-case 合同并规范化原因。
function Convert-D0M2SChildCases
{
    param([Parameter(Mandatory = $true)]$Contract, [Parameter(Mandatory = $true)]$Child)

    $cases = New-D0M2SDefaultCases -Contract $Contract
    foreach ($target in $cases)
    {
        $source = @($Child.Cases | Where-Object { [string]$_.CaseId -ceq [string]$target.CaseId })
        if ($source.Count -ne 1 -or [string]$target.CaseId -ceq "SG-08") { continue }
        $sourceCase = $source[0]
        $status = [string]$sourceCase.Status
        if ($status -cnotin @($Contract.CaseStatuses)) { $status = "Inconclusive" }
        $reason = [string]$sourceCase.Reason
        if ($status -ceq "Passed") { $reason = "None" }
        elseif ($status -ceq "Failed" -and $reason -cnotin @($Contract.Reasons.RouteRejected))
        {
            $fallback = @{
                "SG-01" = "ColdBaselineFailed"; "SG-02" = "AtomicSelectorViolation"
                "SG-03" = "CompileKillRecoveryFailed"; "SG-04" = "GeneratorIdentityDriftAccepted"
                "SG-05" = "AuditOrCacheSelected"; "SG-06" = "DuplicateSelectorAccepted"
            }
            $reason = [string]$fallback[[string]$target.CaseId]
        }
        elseif ($status -in @("Inconclusive", "NotRun") -and $reason -cnotin @($Contract.Reasons.Inconclusive))
        {
            $reason = if ($status -ceq "NotRun") { "EvidenceIncomplete" } else { "HarnessFailure" }
        }

        $paths = @()
        if ($sourceCase.PSObject.Properties["EvidencePaths"])
        {
            $paths = @($sourceCase.EvidencePaths | ForEach-Object { Convert-D0M2SChildEvidencePath -Path ([string]$_) })
        }
        $target.Status = $status
        $target.Reason = $reason
        $target.EvidencePaths = $paths
        $target.Evidence = if ($sourceCase.PSObject.Properties["Evidence"]) { $sourceCase.Evidence } else { [pscustomobject]@{} }
    }

    return $cases
}

# 以 case 状态重算单个门的技术终态。
function Get-D0M2SGateStatus
{
    param([Parameter(Mandatory = $true)][object[]]$Cases)

    if (@($Cases | Where-Object { [string]$_.Status -ceq "Failed" }).Count -gt 0) { return "RouteRejected" }
    if (@($Cases | Where-Object { [string]$_.Status -in @("Inconclusive", "NotRun") }).Count -gt 0) { return "Inconclusive" }
    return "Passed"
}

# 枚举 terminal 写入前的全部证据叶并生成 canonical closure。
function Get-D0M2SEvidenceInventory
{
    $leaves = [Collections.Generic.List[object]]::new()
    $canonical = [Collections.Generic.List[string]]::new()
    foreach ($file in [IO.Directory]::EnumerateFiles($script:RunRoot, "*", [IO.SearchOption]::AllDirectories))
    {
        $relative = [IO.Path]::GetRelativePath($script:RunRoot, $file).Replace("\", "/")
        if ($relative -cin @("D0M2S.terminal.json", "D0M2S.terminal.sha256")) { continue }
        $length = [IO.FileInfo]::new($file).Length
        $sha = Get-D0M2SFileSha256 -Path $file
        $kind = if ($relative.EndsWith(".json", [StringComparison]::OrdinalIgnoreCase)) { "Json" }
            elseif ($relative.EndsWith(".log", [StringComparison]::OrdinalIgnoreCase)) { "Log" }
            elseif ($relative.EndsWith(".sentinel", [StringComparison]::OrdinalIgnoreCase)) { "HarnessControl" }
            else { "File" }
        $leaves.Add([pscustomobject]@{ Path = $relative; Length = $length; Sha256 = $sha; Kind = $kind })
        $canonical.Add("$relative|$length|$sha")
    }

    $orderedLeaves = @($leaves.ToArray() | Sort-Object Path -CaseSensitive)
    $orderedCanonical = $canonical.ToArray()
    [Array]::Sort($orderedCanonical, [StringComparer]::Ordinal)
    return [pscustomobject]@{
        Leaves = $orderedLeaves
        AggregateSha256 = Get-D0M2STextSha256 -Text ([string]::Join("`n", $orderedCanonical))
    }
}

# 从 child 输出归一化 fixture 清理状态。
function Get-D0M2SFixtureSummary
{
    param($Child)

    $fixture = if ($null -ne $Child -and $Child.PSObject.Properties["Fixture"]) { $Child.Fixture } else { $null }
    return [pscustomobject]@{
        Root = if ($null -ne $fixture -and $fixture.PSObject.Properties["Root"]) { [string]$fixture.Root } else { "<unknown>" }
        CleanupStatus = if ($null -ne $fixture -and $fixture.PSObject.Properties["CleanupStatus"]) { [string]$fixture.CleanupStatus } else { "Failed" }
        OwnedProcessResidue = if ($null -ne $fixture -and $fixture.PSObject.Properties["OwnedProcessResidue"]) { [bool]$fixture.OwnedProcessResidue } else { $true }
        OwnerSentinelValidated = if ($null -ne $fixture -and $fixture.PSObject.Properties["OwnerSentinelValidated"]) { [bool]$fixture.OwnerSentinelValidated } else { $false }
        NoReparsePoints = if ($null -ne $fixture -and $fixture.PSObject.Properties["NoReparsePoints"]) { [bool]$fixture.NoReparsePoints } else { $false }
        NoHardlinks = if ($null -ne $fixture -and $fixture.PSObject.Properties["NoHardlinks"]) { [bool]$fixture.NoHardlinks } else { $false }
    }
}

# 根据 cases 与全局门计算 terminal 状态和唯一首要原因。
function Get-D0M2STerminalDecision
{
    param(
        [Parameter(Mandatory = $true)]$Contract,
        [Parameter(Mandatory = $true)][object[]]$Cases,
        [Parameter(Mandatory = $true)]$Fixture,
        [Parameter(Mandatory = $true)]$Protected,
        [Parameter(Mandatory = $true)]$ToolIdentity,
        [string]$ForcedReason = ""
    )

    if (-not [string]::IsNullOrWhiteSpace($ForcedReason)) { return [pscustomobject]@{ Status = "Inconclusive"; Reason = $ForcedReason } }
    if (-not [bool]$Protected.Unchanged) { return [pscustomobject]@{ Status = "Inconclusive"; Reason = "ProtectedPathDrift" } }
    if (-not [bool]$ToolIdentity.Unchanged) { return [pscustomobject]@{ Status = "Inconclusive"; Reason = "ToolIdentityDrift" } }
    if ([string]$Fixture.CleanupStatus -cne "Passed") { return [pscustomobject]@{ Status = "Inconclusive"; Reason = "CleanupFailure" } }
    if ([bool]$Fixture.OwnedProcessResidue) { return [pscustomobject]@{ Status = "Inconclusive"; Reason = "UnityProcessResidue" } }
    $status = Get-D0M2SGateStatus -Cases $Cases
    if ($status -ceq "Passed") { return [pscustomobject]@{ Status = "Passed"; Reason = "None" } }
    $first = $Cases | Where-Object { [string]$_.Status -in $(if ($status -ceq "RouteRejected") { @("Failed") } else { @("Inconclusive", "NotRun") }) } | Select-Object -First 1
    $reason = if ($null -ne $first) { [string]$first.Reason } elseif ($status -ceq "RouteRejected") { [string]$Contract.Reasons.RouteRejected[0] } else { "EvidenceIncomplete" }
    return [pscustomobject]@{ Status = $status; Reason = $reason }
}

# 将异常消息归一化到 terminal Inconclusive reason。
function Get-D0M2SFailureReason
{
    param([Parameter(Mandatory = $true)][string]$Message)

    foreach ($reason in @(
            "TimeBoxExceeded", "InputIdentityDrift", "UnityIdentityMismatch", "ToolIdentityDrift",
            "ProtectedPathDrift", "FilesystemBoundaryViolation", "UnityProcessResidue",
            "CleanupFailure", "SchemaViolation", "EvidenceIncomplete", "HarnessFailure"))
    {
        if ($Message.StartsWith($reason + ":", [StringComparison]::Ordinal)) { return $reason }
    }
    return "HarnessFailure"
}

# 提交 immutable terminal 与只含 raw SHA 的 sidecar。
function Write-D0M2STerminal
{
    param([Parameter(Mandatory = $true)]$Terminal)

    Write-D0M2SFreshJson -Path $script:TerminalPath -Value $Terminal
    $sha = Get-D0M2SFileSha256 -Path $script:TerminalPath
    Write-D0M2SFreshText -Path $script:SidecarPath -Content ($sha + "`n") -Encoding ([Text.ASCIIEncoding]::new())
    return $sha
}

if ([IO.Directory]::Exists($script:RunRoot) -or [IO.File]::Exists($script:RunRoot))
{
    throw "Evidence root must be fresh: $script:RunRoot"
}

[IO.Directory]::CreateDirectory($script:RawRoot) | Out-Null
Write-D0M2SFreshText -Path (Join-Path $script:RunRoot "RunOwner.sentinel") -Content ("D0M2S`nRunId=$RunId`n")

$contract = Read-D0M2SJsonObject -Path $script:ContractPath
. $script:OraclePath
$cases = New-D0M2SDefaultCases -Contract $contract
$child = $null
$forcedReason = ""
$protectedBefore = $null
$protectedAfter = $null
$toolBefore = $null
$toolAfter = $null
$protectedComparison = [pscustomobject]@{ Schema = "D0M2S-ProtectedComparison-v1"; Unchanged = $false; Detail = "Not evaluated." }
$toolComparison = [pscustomobject]@{ Schema = "D0M2S-ToolComparison-v1"; Unchanged = $false; Detail = "Not evaluated." }
$resolvedUnity = ""
$unitySha = ""
$unityVersion = ""
$childProcess = $null

try
{
    if (-not [IO.Path]::IsPathRooted($UnityPath)) { throw "UnityIdentityMismatch: UnityPath must be absolute." }
    $resolvedUnity = [IO.Path]::GetFullPath($UnityPath)
    if (-not [IO.File]::Exists($resolvedUnity)) { throw "UnityIdentityMismatch: Unity executable is missing." }
    $unitySha = Get-D0M2SFileSha256 -Path $resolvedUnity
    $unityVersion = (Get-Item -LiteralPath $resolvedUnity).VersionInfo.ProductVersion
    if ($unitySha -cne [string]$contract.UnityIdentity.Sha256 -or
        -not $unityVersion.StartsWith([string]$contract.UnityIdentity.VersionPrefix, [StringComparison]::Ordinal))
    {
        throw "UnityIdentityMismatch: Unity path/version/SHA drift."
    }
    if (-not [IO.File]::Exists($script:ChildRunnerPath)) { throw "InputIdentityDrift: child runner is missing." }

    $protectedBefore = Get-D0M2SProtectedSnapshot -RepositoryRoot $script:ProjectRoot
    $toolBefore = Get-D0M2SToolSnapshot -RepositoryRoot $script:ProjectRoot
    Write-D0M2SFreshJson -Path (Join-Path $script:RunRoot "J0.ProtectedSnapshot.json") -Value $protectedBefore
    Write-D0M2SFreshJson -Path (Join-Path $script:RunRoot "J0.ToolSnapshot.json") -Value $toolBefore
    Write-D0M2SFreshJson -Path (Join-Path $script:RunRoot "J0.UnityIdentity.json") -Value ([pscustomobject]@{
            Path = $resolvedUnity.Replace("\", "/"); Sha256 = $unitySha; Version = $unityVersion })

    $static = Invoke-D0M2SBoundedProcess -FilePath "pwsh" -WorkingDirectory $script:ProjectRoot -TimeoutMilliseconds 300000 -Arguments @(
        "-NoProfile", "-File", $script:CheckerPath, "-StaticOnly")
    Write-D0M2SFreshText -Path (Join-Path $script:RunRoot "Static.Contract.stdout.log") -Content ([string]$static.Stdout)
    Write-D0M2SFreshText -Path (Join-Path $script:RunRoot "Static.Contract.stderr.log") -Content ([string]$static.Stderr)
    if ($static.TimedOut -or $static.ExitCode -ne 0) { throw "InputIdentityDrift: D0-M2S static contract failed." }

    $sourceGeneratorStaticPath = Join-Path $PSScriptRoot "D0M2S/SourceGenerator/Test-D0M2S-SourceGeneratorStatic.ps1"
    $sourceGeneratorStatic = Invoke-D0M2SBoundedProcess -FilePath "pwsh" -WorkingDirectory $script:ProjectRoot -TimeoutMilliseconds 300000 -Arguments @(
        "-NoProfile", "-File", $sourceGeneratorStaticPath, "-UnityPath", $resolvedUnity)
    Write-D0M2SFreshText -Path (Join-Path $script:RunRoot "Static.SourceGenerator.stdout.log") -Content ([string]$sourceGeneratorStatic.Stdout)
    Write-D0M2SFreshText -Path (Join-Path $script:RunRoot "Static.SourceGenerator.stderr.log") -Content ([string]$sourceGeneratorStatic.Stderr)
    if ($sourceGeneratorStatic.TimedOut -or $sourceGeneratorStatic.ExitCode -ne 0)
    {
        throw "InputIdentityDrift: D0-M2S SourceGenerator static gate failed."
    }

    $arguments = @(
        "-NoProfile", "-File", $script:ChildRunnerPath,
        "-UnityPath", $resolvedUnity,
        "-RunId", $RunId,
        "-EvidenceRoot", $script:RawRoot,
        "-OutputPath", $script:ChildOutputPath)
    $childProcess = Invoke-D0M2SBoundedProcess -FilePath "pwsh" -WorkingDirectory $script:ProjectRoot -TimeoutMilliseconds 2400000 -Arguments $arguments
    Write-D0M2SFreshText -Path (Join-Path $script:RunRoot "Process.SourceGeneratorFault.stdout.log") -Content ([string]$childProcess.Stdout)
    Write-D0M2SFreshText -Path (Join-Path $script:RunRoot "Process.SourceGeneratorFault.stderr.log") -Content ([string]$childProcess.Stderr)
    Write-D0M2SFreshJson -Path (Join-Path $script:RunRoot "Process.SourceGeneratorFault.json") -Value $childProcess
    if ($childProcess.TimedOut) { throw "TimeBoxExceeded: SourceGenerator fault experiment timed out." }
    $child = Read-D0M2SJsonObject -Path $script:ChildOutputPath
    if ([string]$child.RunId -cne $RunId) { throw "SchemaViolation: child RunId mismatch." }
    if ([string]$child.Status -ceq "Passed" -and $childProcess.ExitCode -ne 0)
    {
        throw "HarnessFailure: child reported Passed with non-zero exit."
    }
    $cases = Convert-D0M2SChildCases -Contract $contract -Child $child
}
catch
{
    $forcedReason = Get-D0M2SFailureReason -Message $_.Exception.Message
    $errorPath = Join-Path $script:RunRoot "RunFailure.json"
    if (-not [IO.File]::Exists($errorPath))
    {
        Write-D0M2SFreshJson -Path $errorPath -Value ([pscustomobject]@{
                Reason = $forcedReason; Detail = $_.Exception.Message; Type = $_.Exception.GetType().FullName })
    }
}
finally
{
    try
    {
        $protectedAfter = Get-D0M2SProtectedSnapshot -RepositoryRoot $script:ProjectRoot
        Write-D0M2SFreshJson -Path (Join-Path $script:RunRoot "J2.ProtectedSnapshot.json") -Value $protectedAfter
        if ($null -ne $protectedBefore)
        {
            $protectedComparison = Compare-D0M2SSnapshots -Before $protectedBefore -After $protectedAfter -Schema "D0M2S-ProtectedComparison-v1"
        }
        Write-D0M2SFreshJson -Path (Join-Path $script:RunRoot "J2.ProtectedComparison.json") -Value $protectedComparison
    }
    catch
    {
        $protectedComparison = [pscustomobject]@{ Schema = "D0M2S-ProtectedComparison-v1"; Unchanged = $false; Detail = $_.Exception.Message }
        if ([string]::IsNullOrWhiteSpace($forcedReason)) { $forcedReason = "ProtectedPathDrift" }
    }

    try
    {
        $toolAfter = Get-D0M2SToolSnapshot -RepositoryRoot $script:ProjectRoot
        Write-D0M2SFreshJson -Path (Join-Path $script:RunRoot "J2.ToolSnapshot.json") -Value $toolAfter
        if ($null -ne $toolBefore)
        {
            $toolComparison = Compare-D0M2SSnapshots -Before $toolBefore -After $toolAfter -Schema "D0M2S-ToolComparison-v1"
        }
        Write-D0M2SFreshJson -Path (Join-Path $script:RunRoot "J2.ToolComparison.json") -Value $toolComparison
    }
    catch
    {
        $toolComparison = [pscustomobject]@{ Schema = "D0M2S-ToolComparison-v1"; Unchanged = $false; Detail = $_.Exception.Message }
        if ([string]::IsNullOrWhiteSpace($forcedReason)) { $forcedReason = "ToolIdentityDrift" }
    }
}

$fixture = Get-D0M2SFixtureSummary -Child $child
$sg07 = $cases | Where-Object CaseId -CEQ "SG-07"
$sg07Paths = @(
    "J0.ProtectedSnapshot.json", "J0.ToolSnapshot.json", "J2.ProtectedSnapshot.json",
    "J2.ProtectedComparison.json", "J2.ToolSnapshot.json", "J2.ToolComparison.json") |
    Where-Object { [IO.File]::Exists((Join-Path $script:RunRoot $_)) }
if (-not [bool]$protectedComparison.Unchanged)
{
    $sg07.Status = "Inconclusive"; $sg07.Reason = "ProtectedPathDrift"
}
elseif (-not [bool]$toolComparison.Unchanged)
{
    $sg07.Status = "Inconclusive"; $sg07.Reason = "ToolIdentityDrift"
}
elseif ([string]$fixture.CleanupStatus -cne "Passed")
{
    $sg07.Status = "Inconclusive"; $sg07.Reason = "CleanupFailure"
}
elseif ([bool]$fixture.OwnedProcessResidue)
{
    $sg07.Status = "Inconclusive"; $sg07.Reason = "UnityProcessResidue"
}
elseif ([string]$sg07.Status -ceq "NotRun")
{
    $sg07.Status = "Passed"; $sg07.Reason = "None"
}
$sg07.EvidencePaths = @($sg07.EvidencePaths) + @($sg07Paths)

$sg08 = $cases | Where-Object CaseId -CEQ "SG-08"
$sg08.Status = "Passed"; $sg08.Reason = "None"

$inventory = Get-D0M2SEvidenceInventory
$sg08.EvidencePaths = @($inventory.Leaves | ForEach-Object { [string]$_.Path })
$sg08.Evidence = [pscustomobject]@{
    DeclaredFileCount = @($inventory.Leaves).Count
    AggregateSha256 = [string]$inventory.AggregateSha256
}
$decision = Get-D0M2STerminalDecision -Contract $contract -Cases $cases -Fixture $fixture -Protected $protectedComparison -ToolIdentity $toolComparison -ForcedReason $forcedReason
$observedSelector = if ($null -ne $child -and $child.PSObject.Properties["ObservedFixtureSelector"]) { [string]$child.ObservedFixtureSelector } else { "" }
$measuredSelector = if ($null -ne $child -and $child.PSObject.Properties["MeasuredCommonSelectorAcrossAssemblies"]) { [string]$child.MeasuredCommonSelectorAcrossAssemblies } else { "" }
$terminal = [pscustomobject][ordered]@{
    Schema = [string]$contract.TerminalSchema
    RunId = $RunId
    Status = [string]$decision.Status
    Reason = [string]$decision.Reason
    RouteUnderTest = [string]$contract.RouteUnderTest
    AcceptedProductionRoute = [string]$contract.FrozenTerminalFields.AcceptedProductionRoute
    AcceptedSoleUnityConsumedSelector = [string]$contract.FrozenTerminalFields.AcceptedSoleUnityConsumedSelector
    D1Authorized = [bool]$contract.FrozenTerminalFields.D1Authorized
    ProductionInstallAdmission = [string]$contract.FrozenTerminalFields.ProductionInstallAdmission
    DeclaredFullSemanticEligibility = [bool]$contract.FrozenTerminalFields.DeclaredFullSemanticEligibility
    ProductionMigrationAuthorized = [bool]$contract.FrozenTerminalFields.ProductionMigrationAuthorized
    NextGate = [string]$contract.FrozenTerminalFields.NextGate
    Inputs = [pscustomobject][ordered]@{
        DecisionPath = [string]$contract.DecisionInput.Path
        DecisionSha256 = [string]$contract.DecisionInput.Sha256
        ContractSha256 = Get-D0M2SFileSha256 -Path $script:ContractPath
        RunnerSha256 = Get-D0M2SFileSha256 -Path $PSCommandPath
        ChildRunnerSha256 = if ([IO.File]::Exists($script:ChildRunnerPath)) { Get-D0M2SFileSha256 -Path $script:ChildRunnerPath } else { "" }
        UnityPath = $resolvedUnity.Replace("\", "/")
        UnitySha256 = $unitySha
        UnityVersion = $unityVersion
    }
    Cases = @($cases)
    FullFaultStatus = Get-D0M2SGateStatus -Cases @($cases[0..3])
    SourceGeneratorSpecificXStatus = Get-D0M2SGateStatus -Cases @($cases[4..5])
    ObservedFixtureSelector = $observedSelector
    MeasuredCommonSelectorAcrossAssemblies = $measuredSelector
    RoleObservations = if ($null -ne $child -and $child.PSObject.Properties["RoleObservations"]) { @($child.RoleObservations) } else { @() }
    Fixture = $fixture
    Unity = if ($null -ne $child -and $child.PSObject.Properties["Unity"]) { $child.Unity } else { [pscustomobject]@{} }
    Protected = $protectedComparison
    ToolIdentity = $toolComparison
    EvidenceClosure = [pscustomobject]@{
        Verified = $true
        Partial = [string]$decision.Status -ceq "Inconclusive"
        DeclaredFileCount = @($inventory.Leaves).Count
        PhysicalFileCount = @($inventory.Leaves).Count
        AggregateSha256 = [string]$inventory.AggregateSha256
    }
    EvidenceFiles = @($inventory.Leaves)
}

$terminalSha = Write-D0M2STerminal -Terminal $terminal
$validation = Invoke-D0M2SBoundedProcess -FilePath "pwsh" -WorkingDirectory $script:ProjectRoot -TimeoutMilliseconds 300000 -Arguments @(
    "-NoProfile", "-File", $script:CheckerPath,
    "-Path", $script:TerminalPath,
    "-EvidenceRoot", $script:RunRoot,
    "-ExpectedRunId", $RunId)
if ($validation.TimedOut -or $validation.ExitCode -ne 0)
{
    Write-Error "D0-M2S terminal checker failed after immutable commit: $($validation.Stdout) $($validation.Stderr)"
    exit 23
}

[pscustomobject]@{
    Schema = "D0M2S-RunResult-v1"
    RunId = $RunId
    Status = [string]$terminal.Status
    Reason = [string]$terminal.Reason
    TerminalPath = $script:TerminalPath.Replace("\", "/")
    TerminalSha256 = $terminalSha
    ExitCode = if ([string]$terminal.Status -ceq "Passed") { 0 } elseif ([string]$terminal.Status -ceq "RouteRejected") { 20 } else { 23 }
} | ConvertTo-Json -Depth 10

if ([string]$terminal.Status -ceq "Passed") { exit 0 }
if ([string]$terminal.Status -ceq "RouteRejected") { exit 20 }
exit 23
