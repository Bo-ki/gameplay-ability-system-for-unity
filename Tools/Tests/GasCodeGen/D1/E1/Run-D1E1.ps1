[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunId,
    [Parameter(Mandatory = $true)][string]$RepositoryRoot,
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [Parameter(Mandatory = $true)][string]$AnalyzerPath,
    [Parameter(Mandatory = $true)][string]$SelectorAPath,
    [Parameter(Mandatory = $true)][string]$TransactionSelectorBPath,
    [Parameter(Mandatory = $true)][string]$UnitySelectorBPath,
    [Parameter(Mandatory = $true)][string]$TransactionAdapterPath,
    [Parameter(Mandatory = $true)][string]$EvidenceRoot,
    [string]$OutputPath = '',
    [int]$TransactionTimeoutSeconds = 180,
    [int]$UnityTimeoutSeconds = 600
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'D1E1ProtectedPathOracle.ps1')

# 以 UTF-8 无 BOM、LF、CreateNew 和 Flush(true) 写 central evidence。
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
        throw "Central evidence path must be fresh: $Path"
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

# 以 UTF-8 无 BOM、CreateNew 和 Flush(true) 写 terminal hash sidecar。
function Write-D1E1FreshText
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $parent = [IO.Path]::GetDirectoryName($Path)
    if ([string]::IsNullOrWhiteSpace($parent) -or [IO.File]::Exists($Path) -or
        [IO.Directory]::Exists($Path)) { throw "Text evidence path must be fresh: $Path" }
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Value)
    $stream = [IO.FileStream]::new(
        $Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try
    {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally { $stream.Dispose() }
}

# 对必需输入解析绝对现存文件，拒绝目录、相对路径与隐式 fallback。
function Resolve-D1E1InputFile
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if (-not [IO.Path]::IsPathRooted($Path)) { throw "$Name must be an absolute path." }
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not [IO.File]::Exists($resolved)) { throw "$Name is missing: $resolved" }
    if ((Get-Item -LiteralPath $resolved -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)
    {
        throw "$Name must not be a reparse point: $resolved"
    }
    return $resolved
}

# 生成 central tool-input aggregate，绑定 runner/scaffold/analyzer/selectors/adapter/Unity bytes。
function Get-D1E1ToolIdentity
{
    param([Parameter(Mandatory = $true)]$Context)

    $paths = [ordered]@{
        E1Harness = $PSScriptRoot
        Scaffold = $Context.ScaffoldRoot
        Analyzer = $Context.AnalyzerPath
        SelectorA = $Context.SelectorAPath
        TransactionSelectorB = $Context.TransactionSelectorBPath
        UnitySelectorB = $Context.UnitySelectorBPath
        TransactionAdapter = $Context.TransactionAdapterPath
        TransactionAdapterDirectory = [IO.Path]::GetDirectoryName($Context.TransactionAdapterPath)
        Unity = $Context.UnityPath
    }
    $items = [Collections.Generic.List[object]]::new()
    foreach ($name in $paths.Keys)
    {
        $identity = Get-D1E1ExternalPathIdentity $paths[$name]
        $items.Add([pscustomobject][ordered]@{
            Name = $name; Kind = $identity.Kind; Path = $identity.Path
            FileCount = $identity.FileCount; Length = $identity.Length; Sha256 = $identity.Sha256
        })
    }
    $lines = @($items | ForEach-Object {
        '{0}|{1}|{2}|{3}|{4}' -f $_.Name, $_.Kind, $_.FileCount, $_.Length, $_.Sha256
    })
    return [pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-ToolIdentity-v1'
        AggregateSha256 = Get-D1E1TextSha256 ([string]::Join("`n", $lines))
        Items = @($items)
    }
}

# 使用当前 pwsh 以 ArgumentList 启动 child runner，并捕获退出、超时和全部输出。
function Invoke-D1E1ChildRunner
{
    param(
        [Parameter(Mandatory = $true)][string]$ScriptPath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][int]$TimeoutMilliseconds
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Process -Id $PID).Path
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile', '-File', $ScriptPath) + $Arguments)
    {
        [void]$startInfo.ArgumentList.Add([string]$argument)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $startedUtc = [DateTime]::UtcNow.ToString('O')
    try
    {
        if (-not $process.Start()) { throw "Child runner did not start: $ScriptPath" }
        $processId = $process.Id
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutMilliseconds)
        if ($timedOut)
        {
            $process.Kill($true)
            [void]$process.WaitForExit(30000)
        }
        return [pscustomobject][ordered]@{
            ScriptPath = $ScriptPath.Replace('\', '/'); StartedUtc = $startedUtc
            CompletedUtc = [DateTime]::UtcNow.ToString('O'); ProcessId = $processId
            TimedOut = $timedOut; ExitCode = $(if ($timedOut) { $null } else { $process.ExitCode })
            StdOut = $stdout.GetAwaiter().GetResult(); StdErr = $stderr.GetAwaiter().GetResult()
        }
    }
    finally
    {
        $process.Dispose()
    }
}

# 创建位于系统 temp 下且携带 durable owner sentinel 的唯一 fixture 根。
function New-D1E1OwnedFixtureRoot
{
    param([Parameter(Mandatory = $true)][string]$ExpectedRunId)

    # fixture 叶名保持短小，避免 net472 adapter 在深层 candidate 文件上触发 MAX_PATH。
    $root = Join-Path ([IO.Path]::GetTempPath()) (
        'D1E1-' + [Guid]::NewGuid().ToString('N'))
    $deepestCandidate = Join-Path $root (
        'transaction/transaction-TR-21/Temp/GasCodeGenCandidates/candidate-' +
        ('0' * 32) + '/Control/Generation.GasCodeGenSourceGenerator.additionalfile')
    if ([IO.Path]::GetFullPath($deepestCandidate).Length -gt 259)
    {
        throw 'Owned fixture root violates the net472 MAX_PATH budget.'
    }
    if ([IO.Directory]::Exists($root) -or [IO.File]::Exists($root))
    {
        throw "Owned fixture root must be fresh: $root"
    }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    Write-D1E1FreshJson (Join-Path $root '.d1e1-owner.json') ([pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-OwnedRoot-v1'; RunId = $ExpectedRunId
        Root = ([IO.Path]::GetFullPath($root)).Replace('\', '/')
    })
    return [IO.Path]::GetFullPath($root)
}

# 枚举命令行仍引用 owned root 的进程，只将 exact scoped residue 交给 cleanup。
function Get-D1E1OwnedProcessResidue
{
    param([Parameter(Mandatory = $true)][string]$OwnedRoot)

    $needle = ([IO.Path]::GetFullPath($OwnedRoot)).Replace('\', '/')
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($process in @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue))
    {
        $commandLine = [string]$process.CommandLine
        if ([string]::IsNullOrWhiteSpace($commandLine) -or
            -not $commandLine.Replace('\', '/').Contains(
                $needle, [StringComparison]::OrdinalIgnoreCase)) { continue }
        $rows.Add([pscustomobject][ordered]@{
            ProcessId = [int]$process.ProcessId; Name = [string]$process.Name
            CommandLine = $commandLine
        })
    }
    return @($rows)
}

# 校验 owner sentinel 与 temp containment 后清理 project/process residue，拒绝宽路径递归删除。
function Remove-D1E1OwnedFixtureRoot
{
    param(
        [Parameter(Mandatory = $true)][string]$OwnedRoot,
        [Parameter(Mandatory = $true)][string]$ExpectedRunId
    )

    $resolved = [IO.Path]::GetFullPath($OwnedRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $name = [IO.Path]::GetFileName($resolved)
    if (-not $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or
        $name -cnotmatch '^D1E1-[0-9a-f]{32}$')
    {
        throw "Unsafe owned cleanup root: $resolved"
    }
    $sentinelPath = Join-Path $resolved '.d1e1-owner.json'
    $sentinel = Get-Content -LiteralPath $sentinelPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$sentinel.Schema -cne 'EX-GAS-D1-E1-OwnedRoot-v1' -or
        [string]$sentinel.RunId -cne $ExpectedRunId -or
        [string]$sentinel.Root -cne $resolved.Replace('\', '/'))
    {
        throw 'Owned cleanup sentinel mismatch.'
    }
    $residue = @(Get-D1E1OwnedProcessResidue $resolved)
    foreach ($row in $residue)
    {
        try
        {
            $ownedProcess = [Diagnostics.Process]::GetProcessById([int]$row.ProcessId)
            $ownedProcess.Kill($true)
            [void]$ownedProcess.WaitForExit(30000)
            $ownedProcess.Dispose()
        }
        catch { }
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    $remaining = @(Get-D1E1OwnedProcessResidue $resolved)
    return [pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-Cleanup-v1'; RunId = $ExpectedRunId
        OwnedRoot = $resolved.Replace('\', '/'); RootRemoved = -not [IO.Directory]::Exists($resolved)
        ResidueObservedBeforeCleanup = $residue.Count -gt 0; ResidueBefore = $residue
        ResidueAfter = $remaining; Passed = -not [IO.Directory]::Exists($resolved) -and
            $residue.Count -eq 0 -and $remaining.Count -eq 0
    }
}

# 读取 child matrix；缺失或 schema/RunId 不符时返回 null 供 case 归一化失败。
function Read-D1E1Matrix
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedSchema,
        [Parameter(Mandatory = $true)][string]$ExpectedRunId
    )

    if (-not [IO.File]::Exists($Path)) { return $null }
    try
    {
        $value = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
        if ([string]$value.Schema -cne $ExpectedSchema -or
            [string]$value.RunId -cne $ExpectedRunId) { return $null }
        return $value
    }
    catch { return $null }
}

# 将 contract case 与 child result 合并为固定 typed case，禁止丢 case 假绿。
function New-D1E1TypedCases
{
    param(
        [Parameter(Mandatory = $true)]$Contract,
        $TransactionMatrix,
        $UnityMatrix,
        [Parameter(Mandatory = $true)]$Cleanup,
        [bool]$ClosurePassed
    )

    $results = [Collections.Generic.List[object]]::new()
    foreach ($spec in @($Contract.TransactionCases))
    {
        $match = @(if ($null -ne $TransactionMatrix) {
            $TransactionMatrix.Cases | Where-Object {
                [string]$_.CaseId -ceq [string]$spec.CaseId
            }
        })
        $passed = $match.Count -eq 1 -and [bool]$match[0].Passed
        $results.Add([pscustomobject][ordered]@{
            CaseId = [string]$spec.CaseId; Name = [string]$spec.Name
            Status = $(if ($passed) { 'Passed' } else { 'Failed' })
            Reason = $(if ($passed) { 'None' } else { 'TransactionStateViolation' })
            EvidencePath = ('evidence/transaction/' + [string]$spec.CaseId + '/summary.json')
        })
    }
    foreach ($spec in @($Contract.UnityCases | Where-Object { [string]$_.Mode -ne 'Closure' }))
    {
        $match = @(if ($null -ne $UnityMatrix) {
            $UnityMatrix.Cases | Where-Object {
                [string]$_.CaseId -ceq [string]$spec.CaseId
            }
        })
        $passed = $match.Count -eq 1 -and [bool]$match[0].Passed
        $results.Add([pscustomobject][ordered]@{
            CaseId = [string]$spec.CaseId; Name = [string]$spec.Name
            Status = $(if ($passed) { 'Passed' } else { 'Failed' })
            Reason = $(if ($passed) { 'None' } else { 'UnityBindingMismatch' })
            EvidencePath = ('evidence/unity/' + [string]$spec.CaseId + '/summary.json')
        })
    }
    $results.Add([pscustomobject][ordered]@{
        CaseId = 'CL-01'; Name = 'OwnedFixtureCleanup'
        Status = $(if ($Cleanup.Passed) { 'Passed' } else { 'Failed' })
        Reason = $(if ($Cleanup.Passed) { 'None' } else { 'CleanupFailure' })
        EvidencePath = 'evidence/closure/CL-01-summary.json'
    })
    $results.Add([pscustomobject][ordered]@{
        CaseId = 'EV-01'; Name = 'EvidenceClosure'
        Status = $(if ($ClosurePassed) { 'Passed' } else { 'Failed' })
        Reason = $(if ($ClosurePassed) { 'None' } else { 'EvidenceIncomplete' })
        EvidencePath = 'evidence/closure/EV-01-summary.json'
    })
    return @($results)
}

# 逐条验证 33 个 typed case 的 EvidencePath 真实存在、回绑 CaseId 且被 index 收录。
function Test-D1E1CaseEvidenceClosure
{
    param(
        [Parameter(Mandatory = $true)][string]$RunRoot,
        [Parameter(Mandatory = $true)][object[]]$Cases,
        [Parameter(Mandatory = $true)]$EvidenceClosure,
        [Parameter(Mandatory = $true)][string]$ExpectedRunId
    )

    try
    {
        if ($Cases.Count -ne 33 -or -not [bool]$EvidenceClosure.Verified) { return $false }
        $indexPath = Join-Path $RunRoot ([string]$EvidenceClosure.IndexPath)
        $index = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $declared = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($entry in @($index.Entries))
        {
            if (-not $declared.Add([string]$entry.Path)) { return $false }
        }
        $caseIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($case in $Cases)
        {
            $caseId = [string]$case.CaseId
            $relative = [string]$case.EvidencePath
            if (-not $caseIds.Add($caseId) -or
                -not $relative.StartsWith('evidence/', [StringComparison]::Ordinal) -or
                -not $declared.Contains($relative))
            {
                return $false
            }
            $absolute = [IO.Path]::GetFullPath((Join-Path $RunRoot $relative))
            $prefix = [IO.Path]::GetFullPath((Join-Path $RunRoot 'evidence')) +
                [IO.Path]::DirectorySeparatorChar
            if (-not $absolute.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
                -not [IO.File]::Exists($absolute)) { return $false }
            $summary = Get-Content -LiteralPath $absolute -Raw -Encoding UTF8 | ConvertFrom-Json
            if ([string]$summary.CaseId -cne $caseId -or
                [string]$summary.RunId -cne $ExpectedRunId) { return $false }
            if ($caseId -cne 'EV-01')
            {
                $passedProperty = $summary.PSObject.Properties['Passed']
                if ($null -eq $passedProperty -or
                    ([string]$case.Status -ceq 'Passed' -and -not [bool]$summary.Passed))
                {
                    return $false
                }
            }
        }
        return $true
    }
    catch { return $false }
}

# 为提前退出的 child 补齐固定 case 失败摘要，使 failed terminal 仍可独立闭包验证。
function Write-D1E1MissingCaseSummaries
{
    param(
        [Parameter(Mandatory = $true)]$Contract,
        [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
        [Parameter(Mandatory = $true)][string]$ExpectedRunId
    )

    foreach ($group in @(
        [pscustomobject]@{ Specs = @($Contract.TransactionCases); Directory = 'transaction'; Reason = 'TransactionUnexpectedExit' },
        [pscustomobject]@{ Specs = @($Contract.UnityCases | Where-Object { [string]$_.Mode -ne 'Closure' }); Directory = 'unity'; Reason = 'UnityUnexpectedExit' }
    ))
    {
        foreach ($spec in $group.Specs)
        {
            $path = Join-Path $EvidenceDirectory (
                $group.Directory + '/' + [string]$spec.CaseId + '/summary.json')
            if ([IO.File]::Exists($path)) { continue }
            Write-D1E1FreshJson $path ([pscustomobject][ordered]@{
                Schema = 'EX-GAS-D1-E1-FallbackCase-v1'; RunId = $ExpectedRunId
                CaseId = [string]$spec.CaseId; Name = [string]$spec.Name
                Passed = $false; Reason = [string]$group.Reason
            })
        }
    }
}

# 重读 index 并逐文件复算 path/length/SHA/schema，拒绝仅凭数量形成 closure。
function Test-D1E1EvidenceIndex
{
    param(
        [Parameter(Mandatory = $true)][string]$RunRoot,
        [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
        [Parameter(Mandatory = $true)]$Index,
        [Parameter(Mandatory = $true)][string]$ExpectedRunId
    )

    try
    {
        $declared = @($Index.Entries)
        $physical = @(Get-ChildItem -LiteralPath $EvidenceDirectory -Recurse -File)
        if ([string]$Index.Schema -cne 'EX-GAS-D1-E1-EvidenceIndex-v1' -or
            [string]$Index.RunId -cne $ExpectedRunId -or [string]$Index.Scope -cne 'evidence/**' -or
            [bool]$Index.Partial -or [int]$Index.DeclaredCount -ne $declared.Count -or
            $declared.Count -ne $physical.Count)
        {
            return $false
        }
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $evidencePrefix = [IO.Path]::GetFullPath($EvidenceDirectory) + [IO.Path]::DirectorySeparatorChar
        foreach ($entry in $declared)
        {
            $relative = [string]$entry.Path
            if (-not $seen.Add($relative) -or -not $relative.StartsWith('evidence/', [StringComparison]::Ordinal))
            {
                return $false
            }
            $absolute = [IO.Path]::GetFullPath((Join-Path $RunRoot $relative))
            if (-not $absolute.StartsWith($evidencePrefix, [StringComparison]::OrdinalIgnoreCase) -or
                -not [IO.File]::Exists($absolute) -or
                [IO.Path]::GetRelativePath($RunRoot, $absolute).Replace('\', '/') -cne $relative)
            {
                return $false
            }
            $item = Get-Item -LiteralPath $absolute -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                (Get-D1FileLinkCount $absolute) -ne 1) { return $false }
            $bytes = [IO.File]::ReadAllBytes($absolute)
            $sha256 = [Convert]::ToHexString(
                [Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
            if ([long]$entry.Length -ne [long]$bytes.LongLength -or
                [string]$entry.Sha256 -cne $sha256) { return $false }
            $schema = $null
            if ($item.Extension -ceq '.json')
            {
                try { $schema = [string](Get-Content -LiteralPath $absolute -Raw | ConvertFrom-Json).Schema }
                catch { $schema = $null }
            }
            if (($null -eq $entry.Schema) -ne ($null -eq $schema) -or
                ($null -ne $schema -and [string]$entry.Schema -cne $schema)) { return $false }
        }
        return $true
    }
    catch { return $false }
}

# 为 evidence/** 建立无循环 index，并重读复核 declared/physical exact closure。
function New-D1E1EvidenceClosure
{
    param(
        [Parameter(Mandatory = $true)][string]$RunRoot,
        [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
        [Parameter(Mandatory = $true)][string]$ExpectedRunId
    )

    $physical = @(Get-ChildItem -LiteralPath $EvidenceDirectory -Recurse -File | Sort-Object FullName)
    $entries = [Collections.Generic.List[object]]::new()
    foreach ($file in $physical)
    {
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            (Get-D1FileLinkCount $file.FullName) -ne 1)
        {
            throw "Evidence file is linked: $($file.FullName)"
        }
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $schema = $null
        if ($file.Extension -ceq '.json')
        {
            try { $schema = [string](Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json).Schema }
            catch { $schema = $null }
        }
        $entries.Add([pscustomobject][ordered]@{
            Path = [IO.Path]::GetRelativePath($RunRoot, $file.FullName).Replace('\', '/')
            Length = [long]$bytes.LongLength
            Sha256 = [Convert]::ToHexString(
                [Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
            Schema = $schema
        })
    }
    $indexPath = Join-Path $RunRoot 'evidence-index.json'
    $index = [pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-EvidenceIndex-v1'; RunId = $ExpectedRunId
        Scope = 'evidence/**'; DeclaredCount = $entries.Count; Partial = $false
        Entries = @($entries)
    }
    Write-D1E1FreshJson $indexPath $index
    $roundTrip = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $physicalAfter = @(Get-ChildItem -LiteralPath $EvidenceDirectory -Recurse -File)
    $verified = Test-D1E1EvidenceIndex $RunRoot $EvidenceDirectory $roundTrip $ExpectedRunId
    return [pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-EvidenceClosure-v1'; Verified = $verified
        Partial = $false; DeclaredCount = [int]$roundTrip.DeclaredCount
        PhysicalCount = $physicalAfter.Count
        IndexPath = 'evidence-index.json'
        IndexSha256 = (Get-FileHash -LiteralPath $indexPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$startedUtc = [DateTime]::UtcNow.ToString('O')
if ($RunId -cnotmatch '^D1E1-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$')
{
    throw 'RunId must match D1E1-YYYYMMDDThhmmssZ-12hex.'
}
$resolvedRepository = Resolve-D1E1RepositoryRoot $RepositoryRoot
$resolvedEvidence = [IO.Path]::GetFullPath($EvidenceRoot)
if (-not [IO.Path]::IsPathRooted($EvidenceRoot) -or [IO.Directory]::Exists($resolvedEvidence) -or
    [IO.File]::Exists($resolvedEvidence))
{
    throw 'EvidenceRoot must be an absolute fresh path.'
}
$resolvedOutput = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Join-Path $resolvedEvidence 'terminal.json'
} else { [IO.Path]::GetFullPath($OutputPath) }
if ([IO.File]::Exists($resolvedOutput) -or [IO.Directory]::Exists($resolvedOutput))
{
    throw "OutputPath must be fresh: $resolvedOutput"
}
if ([IO.Path]::GetFullPath([IO.Path]::GetDirectoryName($resolvedOutput)).TrimEnd(
        [IO.Path]::DirectorySeparatorChar) -cne
    $resolvedEvidence.TrimEnd([IO.Path]::DirectorySeparatorChar))
{
    throw 'OutputPath must be a direct child of EvidenceRoot.'
}

$contractPath = Join-Path $PSScriptRoot 'D1E1.contract.json'
$scaffoldRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'Scaffold'
$scaffoldContractPath = Join-Path $scaffoldRoot 'D1Scaffold.contract.json'
$fixtureTemplateRoot = Join-Path $scaffoldRoot 'Fixture~'
$context = [pscustomobject]@{
    RunId = $RunId; RepositoryRoot = $resolvedRepository; EvidenceRoot = $resolvedEvidence
    ContractPath = $contractPath; ScaffoldRoot = $scaffoldRoot
    ScaffoldContractPath = $scaffoldContractPath; FixtureTemplateRoot = $fixtureTemplateRoot
    UnityPath = Resolve-D1E1InputFile $UnityPath 'UnityPath'
    AnalyzerPath = Resolve-D1E1InputFile $AnalyzerPath 'AnalyzerPath'
    SelectorAPath = Resolve-D1E1InputFile $SelectorAPath 'SelectorAPath'
    TransactionSelectorBPath = Resolve-D1E1InputFile `
        $TransactionSelectorBPath 'TransactionSelectorBPath'
    UnitySelectorBPath = Resolve-D1E1InputFile $UnitySelectorBPath 'UnitySelectorBPath'
    TransactionAdapterPath = Resolve-D1E1InputFile $TransactionAdapterPath 'TransactionAdapterPath'
}
[IO.Directory]::CreateDirectory($resolvedEvidence) | Out-Null
$contract = Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8 | ConvertFrom-Json
$scaffold = Get-D1RouteScaffoldSnapshot $fixtureTemplateRoot $scaffoldContractPath
$scaffoldStaticOutput = & (Join-Path $scaffoldRoot 'Test-D1-ScaffoldStatic.ps1') `
    -UnityPath $context.UnityPath -RepositoryRoot $resolvedRepository | Out-String
$scaffoldStatic = $scaffoldStaticOutput | ConvertFrom-Json
if (-not [bool]$scaffoldStatic.Passed -or
    [string]$scaffoldStatic.StagedRouteScaffoldSha256 -cne [string]$scaffold.RouteScaffoldSha256)
{
    throw 'Pre-J staged/production scaffold static evidence is invalid.'
}
$protectedBefore = Get-D1E1ProtectedSnapshot $resolvedRepository $contractPath
$toolBefore = Get-D1E1ToolIdentity $context
$evidenceDirectory = Join-Path $resolvedEvidence 'evidence'
if ($resolvedOutput.StartsWith(
        [IO.Path]::GetFullPath($evidenceDirectory) + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase))
{
    throw 'OutputPath must remain outside evidence/** to preserve acyclic closure.'
}
[IO.Directory]::CreateDirectory($evidenceDirectory) | Out-Null
Write-D1E1FreshJson (Join-Path $evidenceDirectory 'protected-before.json') $protectedBefore
Write-D1E1FreshJson (Join-Path $evidenceDirectory 'tool-before.json') $toolBefore
Write-D1E1FreshJson (Join-Path $evidenceDirectory 'scaffold-static.json') $scaffoldStatic

$ownedRoot = New-D1E1OwnedFixtureRoot $RunId
$transactionProcess = $null
$unityProcess = $null
$cleanup = $null
$failureDetail = ''
try
{
    $transactionMatrixPath = Join-Path $evidenceDirectory 'transaction-matrix.json'
    $transactionArguments = @(
        '-RunId', $RunId, '-ContractPath', $contractPath,
        '-ScaffoldContractPath', $scaffoldContractPath,
        '-FixtureTemplateRoot', $fixtureTemplateRoot,
        '-OwnedFixtureRoot', (Join-Path $ownedRoot 'transaction'),
        '-EvidenceRoot', (Join-Path $evidenceDirectory 'transaction'),
        '-AnalyzerPath', $context.AnalyzerPath, '-SelectorAPath', $context.SelectorAPath,
        '-SelectorBPath', $context.TransactionSelectorBPath,
        '-TransactionAdapterPath', $context.TransactionAdapterPath,
        '-OutputPath', $transactionMatrixPath,
        '-TimeoutSeconds', [string]$TransactionTimeoutSeconds)
    $transactionProcess = Invoke-D1E1ChildRunner `
        (Join-Path $PSScriptRoot 'Invoke-D1E1TransactionMatrix.ps1') `
        $transactionArguments (($TransactionTimeoutSeconds * 21 + 120) * 1000)
    Write-D1E1FreshJson (Join-Path $evidenceDirectory 'transaction-child-process.json') `
        $transactionProcess

    $unityMatrixPath = Join-Path $evidenceDirectory 'unity-matrix.json'
    $unityArguments = @(
        '-RunId', $RunId, '-ContractPath', $contractPath,
        '-FixtureTemplateRoot', $fixtureTemplateRoot,
        '-OwnedFixtureRoot', (Join-Path $ownedRoot 'unity'),
        '-EvidenceRoot', (Join-Path $evidenceDirectory 'unity'),
        '-UnityPath', $context.UnityPath, '-AnalyzerPath', $context.AnalyzerPath,
        '-SelectorAPath', $context.SelectorAPath,
        '-SelectorBPath', $context.UnitySelectorBPath,
        '-OutputPath', $unityMatrixPath, '-TimeoutSeconds', [string]$UnityTimeoutSeconds)
    $unityProcess = Invoke-D1E1ChildRunner `
        (Join-Path $PSScriptRoot 'Invoke-D1E1UnityMatrix.ps1') `
        $unityArguments (($UnityTimeoutSeconds * 11 + 300) * 1000)
    Write-D1E1FreshJson (Join-Path $evidenceDirectory 'unity-child-process.json') $unityProcess
}
catch
{
    $failureDetail = $_.Exception.ToString()
}
finally
{
    try { $cleanup = Remove-D1E1OwnedFixtureRoot $ownedRoot $RunId }
    catch
    {
        $cleanup = [pscustomobject][ordered]@{
            Schema = 'EX-GAS-D1-E1-Cleanup-v1'; RunId = $RunId; Passed = $false
            RootRemoved = -not [IO.Directory]::Exists($ownedRoot); Detail = $_.Exception.ToString()
        }
        $failureDetail += "`nCleanup: " + $_.Exception.ToString()
    }
}

$protectedAfter = Get-D1E1ProtectedSnapshot $resolvedRepository $contractPath
$protectedComparison = Compare-D1E1ProtectedSnapshots $protectedBefore $protectedAfter
$toolAfter = Get-D1E1ToolIdentity $context
$toolUnchanged = [string]$toolBefore.AggregateSha256 -ceq [string]$toolAfter.AggregateSha256
Write-D1E1FreshJson (Join-Path $evidenceDirectory 'protected-after.json') $protectedAfter
Write-D1E1FreshJson (Join-Path $evidenceDirectory 'protected-comparison.json') $protectedComparison
Write-D1E1FreshJson (Join-Path $evidenceDirectory 'tool-after.json') $toolAfter
[IO.Directory]::CreateDirectory((Join-Path $evidenceDirectory 'closure')) | Out-Null
Write-D1E1FreshJson (Join-Path $evidenceDirectory 'closure/CL-01-summary.json') `
    ([pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-CleanupCase-v1'; RunId = $RunId
        CaseId = 'CL-01'; Passed = [bool]$cleanup.Passed; Cleanup = $cleanup
    })

$transactionMatrix = Read-D1E1Matrix (Join-Path $evidenceDirectory 'transaction-matrix.json') `
    'EX-GAS-D1-E1-TransactionMatrix-v1' $RunId
$unityMatrix = Read-D1E1Matrix (Join-Path $evidenceDirectory 'unity-matrix.json') `
    'EX-GAS-D1-E1-UnityMatrix-v1' $RunId
Write-D1E1MissingCaseSummaries $contract $evidenceDirectory $RunId
$preClosureCount = @(Get-ChildItem -LiteralPath $evidenceDirectory -Recurse -File).Count
$evPreSummary = [pscustomobject][ordered]@{
    Schema = 'EX-GAS-D1-E1-EvidenceClosureCase-v1'; RunId = $RunId
    CaseId = 'EV-01'; ExpectedDeclaredCount = $preClosureCount + 1
    Scope = 'evidence/**'; Partial = $false
}
Write-D1E1FreshJson (Join-Path $evidenceDirectory 'closure/EV-01-summary.json') $evPreSummary
$closure = New-D1E1EvidenceClosure $resolvedEvidence $evidenceDirectory $RunId
$closure.Verified = [bool]$closure.Verified -and
    [int]$closure.DeclaredCount -eq ($preClosureCount + 1)
$typedCases = @(New-D1E1TypedCases $contract $transactionMatrix $unityMatrix $cleanup $closure.Verified)
$caseEvidenceClosed = Test-D1E1CaseEvidenceClosure `
    $resolvedEvidence $typedCases $closure $RunId
$closure.Verified = [bool]$closure.Verified -and $caseEvidenceClosed
$closure | Add-Member -NotePropertyName CaseEvidenceVerified -NotePropertyValue $caseEvidenceClosed
$typedCases = @(New-D1E1TypedCases $contract $transactionMatrix $unityMatrix $cleanup $closure.Verified)
$expectedCaseIds = @($contract.TransactionCases.CaseId) + @($contract.UnityCases.CaseId)
$actualCaseIds = @($typedCases.CaseId)
$exactCases = $expectedCaseIds.Count -eq 33 -and $actualCaseIds.Count -eq 33 -and
    ((@($expectedCaseIds | Sort-Object) -join '|') -ceq
        (@($actualCaseIds | Sort-Object) -join '|'))
$allCasesPassed = $exactCases -and @($typedCases | Where-Object Status -cne 'Passed').Count -eq 0
$transactionChildPassed = $null -ne $transactionProcess -and -not $transactionProcess.TimedOut -and
    [int]$transactionProcess.ExitCode -eq 0
$unityChildPassed = $null -ne $unityProcess -and -not $unityProcess.TimedOut -and
    [int]$unityProcess.ExitCode -eq 0
$overallPassed = $allCasesPassed -and $transactionChildPassed -and $unityChildPassed -and
    $cleanup.Passed -and $closure.Verified -and
    $protectedComparison.Unchanged -and $toolUnchanged -and [string]::IsNullOrWhiteSpace($failureDetail)
$reason = if ($overallPassed) { 'None' }
elseif (-not $protectedComparison.Unchanged) { 'ProtectedPathDrift' }
elseif (-not $toolUnchanged) { 'ToolIdentityDrift' }
elseif (-not $cleanup.Passed) { 'CleanupFailure' }
elseif (-not $transactionChildPassed) { 'TransactionUnexpectedExit' }
elseif (-not $unityChildPassed) { 'UnityUnexpectedExit' }
elseif (-not $closure.Verified) { 'EvidenceIncomplete' }
elseif (-not $exactCases) { 'EvidenceIncomplete' }
elseif ($null -eq $transactionMatrix -or -not $transactionMatrix.Passed) { 'TransactionStateViolation' }
elseif ($null -eq $unityMatrix -or -not $unityMatrix.Passed) { 'UnityBindingMismatch' }
else { 'UnexpectedFailure' }
$terminal = [pscustomobject][ordered]@{
    Schema = 'EX-GAS-D1-E1-Terminal-v1'; RunId = $RunId; ProbeId = 'D1-E1'
    Status = $(if ($overallPassed) { 'Passed' } else { 'Failed' }); Reason = $reason
    StartedUtc = $startedUtc; CompletedUtc = [DateTime]::UtcNow.ToString('O')
    Inputs = [pscustomobject][ordered]@{
        UnityPath = $context.UnityPath.Replace('\', '/')
        AnalyzerSha256 = (Get-FileHash $context.AnalyzerPath -Algorithm SHA256).Hash.ToLowerInvariant()
        SelectorASha256 = (Get-FileHash $context.SelectorAPath -Algorithm SHA256).Hash.ToLowerInvariant()
        TransactionSelectorBSha256 = (Get-FileHash `
            $context.TransactionSelectorBPath -Algorithm SHA256).Hash.ToLowerInvariant()
        UnitySelectorBSha256 = (Get-FileHash `
            $context.UnitySelectorBPath -Algorithm SHA256).Hash.ToLowerInvariant()
        StagedRouteScaffoldSha256 = $scaffoldStatic.StagedRouteScaffoldSha256
        ProductionRouteScaffoldSha256 = $scaffoldStatic.ProductionRouteScaffoldSha256
        ProductionExistingExact = [int]$scaffoldStatic.ProductionState.ExistingExact
        ProductionMissing = [int]$scaffoldStatic.ProductionState.ProductionMissing
        StagedNew = [int]$scaffoldStatic.ProductionState.StagedNew
        TransactionAdapterPath = $context.TransactionAdapterPath.Replace('\', '/')
    }
    ToolIdentity = [pscustomobject][ordered]@{
        BeforeSha256 = $toolBefore.AggregateSha256; AfterSha256 = $toolAfter.AggregateSha256
        Unchanged = $toolUnchanged
    }
    ChildProcesses = [pscustomobject][ordered]@{
        Transaction = [pscustomobject][ordered]@{
            ExitCode = $(if ($null -eq $transactionProcess) { $null } else { $transactionProcess.ExitCode })
            TimedOut = $(if ($null -eq $transactionProcess) { $false } else { $transactionProcess.TimedOut })
            Passed = $transactionChildPassed
        }
        Unity = [pscustomobject][ordered]@{
            ExitCode = $(if ($null -eq $unityProcess) { $null } else { $unityProcess.ExitCode })
            TimedOut = $(if ($null -eq $unityProcess) { $false } else { $unityProcess.TimedOut })
            Passed = $unityChildPassed
        }
        Passed = $transactionChildPassed -and $unityChildPassed
    }
    Protected = $protectedComparison; Cases = $typedCases; Cleanup = $cleanup
    EvidenceClosure = $closure; FailureDetail = $failureDetail
    ProductionInstallAdmission = 'NotEvaluated'
    DeclaredFullSemanticEligibility = $false
}
Write-D1E1FreshJson $resolvedOutput $terminal
$terminalHash = (Get-FileHash -LiteralPath $resolvedOutput -Algorithm SHA256).Hash.ToLowerInvariant()
$sidecar = $resolvedOutput + '.sha256'
Write-D1E1FreshText $sidecar (
    $terminalHash + "  " + [IO.Path]::GetFileName($resolvedOutput) + "`n")
$terminalVerificationOutput = & (Join-Path $PSScriptRoot 'Test-D1E1Terminal.ps1') `
    -TerminalPath $resolvedOutput -ContractPath $contractPath | Out-String
$terminalVerification = $terminalVerificationOutput | ConvertFrom-Json
if (-not [bool]$terminalVerification.Passed) { exit 62 }
Write-D1E1FreshJson ($resolvedOutput + '.verification.json') $terminalVerification
if (-not $overallPassed) { exit 61 }
