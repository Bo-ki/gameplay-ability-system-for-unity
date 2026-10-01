[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:CheckerPath = Join-Path $PSScriptRoot "Test-D0M2TContract.ps1"
$script:Contract = Get-Content -LiteralPath (Join-Path $PSScriptRoot "D0M2T.contract.json") -Raw | ConvertFrom-Json -Depth 100
$script:Utf8NoBom = [Text.UTF8Encoding]::new($false)
$script:RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../../../.."))
$script:ArchiveIndexPath = Join-Path $script:RepositoryRoot "Tools/Tests/GasCodeGen/D0M2F/Tarball/Archives~/ArchiveIndex.json"
$script:TarballRunnerPath = Join-Path $script:RepositoryRoot "Tools/Tests/GasCodeGen/D0M2T/Tarball/Run-D0M2T-TarballFaultExperiment.ps1"
$script:ArchiveIndex = Get-Content -LiteralPath $script:ArchiveIndexPath -Raw | ConvertFrom-Json -Depth 100

# 创建带三程序集与稳定 generation token 的 synthetic Unity raw。
function New-SyntheticUnityPayload
{
    param([string]$Generation, [string]$ArchiveSha, [string]$GraphSha, [string]$Root)

    $archive = @($script:ArchiveIndex.Archives | Where-Object GenerationId -CEQ $Generation)[0]
    $names = @("com.exhard.exgas.generated.runtime", "com.exhard.exgas.generated.editor", "com.exhard.exgas.autochessdemo")
    $assemblies = @($names | ForEach-Object {
        [pscustomobject][ordered]@{
            Name = $_; Generation = $Generation; GenerationToken = [string]$archive.GenerationToken
            OutputPath = "$Root/output/$_.dll"; SourceFiles = @("$Root/source/$_.cs")
            CompiledAssemblyReferences = @(); Defines = @(); RoslynAdditionalFilePaths = @()
        }
    })
    return [pscustomobject][ordered]@{
        Passed = $true; Detail = "synthetic"; UnityVersion = "6000.3.14f1"; ExpectedGeneration = $Generation
        ExpectedArchiveSha256 = $ArchiveSha; PackageName = "com.exhard.exgas.d0m2f-tarball"; PackageVersion = [string]$archive.PackageVersion
        PackageSource = "Tarball"; PackageId = "synthetic-$Generation"; ResolvedPackagePath = "$Root/Fixture/Project/Library/PackageCache/pkg-$Generation"
        Assemblies = $assemblies; CompileGraphSha256 = $GraphSha
    }
}

# 构造与 Tarball runner 完全同 bytes 的 synthetic manifest 文本。
function Get-SyntheticManifestText
{
    param([string]$FileName)

    return "{`n  `"dependencies`": {`n    `"com.exhard.exgas.d0m2f-tarball`": `"file:../../Authority/$FileName`"`n  }`n}`n"
}

# 计算 synthetic 内存 bytes 的 SHA-256。
function Get-SyntheticBytesSha256
{
    param([byte[]]$Bytes)

    return ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes))).ToLowerInvariant()
}

# 计算与 Tarball runner 完全同 bytes 的 synthetic manifest SHA。
function Get-SyntheticManifestSha256
{
    param([string]$FileName)

    return Get-SyntheticBytesSha256 ($script:Utf8NoBom.GetBytes((Get-SyntheticManifestText $FileName)))
}

# 返回 runner selector worker 的唯一 source 模板。
function Get-SyntheticSelectorWorkerText
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

# 返回文本观察使用的 UTF-8 length/SHA，可复现 Write-NewTextFile 的 LF 结尾。
function Get-SyntheticTextBinding
{
    param([AllowEmptyString()][string]$Text, [bool]$EnsureLf)

    $normalized = $Text.Replace("`r`n", "`n").Replace("`r", "`n")
    if ($EnsureLf -and -not $normalized.EndsWith("`n", [StringComparison]::Ordinal)) { $normalized += "`n" }
    $bytes = $script:Utf8NoBom.GetBytes($normalized)
    return [pscustomobject]@{ Length = [long]$bytes.LongLength; Sha256 = Get-SyntheticBytesSha256 $bytes }
}

# 创建 synthetic Unity 子进程 raw。
function New-SyntheticProcess
{
    param([string]$RunId, [string]$CaseId, [string]$Name, [int]$ExitCode, [bool]$Killed, [bool]$ResultAtKill)

    return [pscustomobject][ordered]@{
        Schema = "D0M2T-UnityProcess-v1"; RunId = $RunId; CaseId = $CaseId; Name = $Name
        ExitCode = $ExitCode; TimedOut = $false; KilledOnResolve = $Killed; ResultExistedAtKill = $ResultAtKill
        KillEvent = $null; DurationMilliseconds = 1; ResultPath = "$CaseId/$Name.json"; LogPath = "$CaseId/$Name.log"
    }
}

# 创建 content-addressed、single-link、readonly synthetic archive 行。
function New-SyntheticArchive
{
    param([string]$Generation, [string]$Sha, [int]$Length)

    return [pscustomobject][ordered]@{
        GenerationId = $Generation; FileName = "$Sha.tgz"; ExpectedSha256 = $Sha; ActualSha256 = $Sha
        ExpectedLength = $Length; ActualLength = $Length; LinkCount = 1; ReadOnly = $true
    }
}

# 将 synthetic JSON 写入 EvidenceRoot 并登记现场 SHA/Length。
function Add-SyntheticEvidence
{
    param($Context, [string]$EvidenceId, [string]$CaseId, [string]$Kind, [string]$RelativePath, $Value)

    $path = Join-Path $Context.Root $RelativePath
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
    [IO.File]::WriteAllText($path, ($Value | ConvertTo-Json -Depth 100), $script:Utf8NoBom)
    $declaration = [pscustomobject][ordered]@{
        EvidenceId = $EvidenceId; CaseId = $CaseId; Kind = $Kind; Path = $RelativePath.Replace('\', '/')
        Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant(); Length = [long](Get-Item -LiteralPath $path).Length
    }
    $Context.EvidenceFiles.Add($declaration)
    $Context.CaseEvidence[$CaseId].Add($EvidenceId)
    return $EvidenceId
}

# 写出 TT-01 authority、watcher、baseline Unity 与 process raw。
function Add-SyntheticTT01
{
    param($Context)

    $record = [pscustomobject][ordered]@{
        Schema = "D0M2T-BaselineAuthority-v1"; RunId = $Context.RunId; Before = $Context.Archives; After = $Context.Archives
        AuthorityWrites = 0
        BaselineA = [pscustomobject]@{ ExitCode = 0; AllA = $true; Passed = $true; Assemblies = $Context.PayloadA.Assemblies }
        BaselineB = [pscustomobject]@{ ExitCode = 0; AllB = $true; Passed = $true; Assemblies = $Context.PayloadB.Assemblies }
        CanaryObserved = $true; WatcherErrors = 0
    }
    if ($Context.Mutation -ceq "TT01") { $record.AuthorityWrites = 1 }
    $canary = Join-Path $Context.Root "Fixture/Control/watcher-canary-00000000000000000000000000000001"
    $watcherEvent = [pscustomobject]@{ Utc = "2026-08-30T00:00:00Z"; ChangeType = "Created"; FullPath = $canary; OldFullPath = "" }
    $watcher = [pscustomobject][ordered]@{ Schema = "D0M2T-AuthorityWatcher-v1"; RunId = $Context.RunId; CanaryPath = $canary; CanaryObserved = $true; AuthorityWrites = 0; Errors = @(); Events = @($watcherEvent) }
    if ($Context.Mutation -ceq "WatcherRawPathDivergence") { $watcher.CanaryPath += ".diverged" }
    Add-SyntheticEvidence $Context "TT-01-authority-snapshot" "TT-01" "AuthoritySnapshot" "tt-01/authority.json" $record | Out-Null
    Add-SyntheticEvidence $Context "TT-01-authority-watcher" "TT-01" "AuthorityWatcherLog" "tt-01/watcher.json" $watcher | Out-Null
    Add-SyntheticEvidence $Context "TT-01-baseline-a-unity" "TT-01" "UnityObservation" "tt-01/baseline-a.json" $Context.PayloadA | Out-Null
    Add-SyntheticEvidence $Context "TT-01-baseline-a-process" "TT-01" "ProcessLog" "tt-01/baseline-process.json" (New-SyntheticProcess $Context.RunId "TT-01" "baseline-a" 0 $false $false) | Out-Null
    $Context.Objects.AuthorityRecord = $record
}

# 写出 TT-02 selector 原子强杀与 worker 交叉 raw。
function Add-SyntheticTT02
{
    param($Context)

    $project = Join-Path $Context.Root "Fixture/Project"; $control = Join-Path $Context.Root "Fixture/Control"
    $manifest = Join-Path $project "Packages/manifest.json"; $workerPath = Join-Path $control "selector-worker.ps1"
    $bindingA = Get-SyntheticTextBinding (Get-SyntheticManifestText $Context.ArchiveA.FileName) $false
    $bindingB = Get-SyntheticTextBinding (Get-SyntheticManifestText $Context.ArchiveB.FileName) $false
    $beforeRows = @(
        [pscustomobject][ordered]@{ CaseId = "TT-02"; Phase = "BeforeReplace"; Path = $manifest + ".next"; Kind = "ManifestNext"; Role = "Derived"; ConsumerAuthority = "None"; UnityConsumerAuthority = 0; NeverFallbackSelector = $true; ExistedAfterKill = $true; ObservedLength = $bindingB.Length; ObservedSha256 = $bindingB.Sha256; RemovedAfterCleanup = $true },
        [pscustomobject][ordered]@{ CaseId = "TT-02"; Phase = "BeforeReplace"; Path = $manifest + ".backup"; Kind = "ManifestBackup"; Role = "Derived"; ConsumerAuthority = "None"; UnityConsumerAuthority = 0; NeverFallbackSelector = $true; ExistedAfterKill = $false; ObservedLength = 0L; ObservedSha256 = ""; RemovedAfterCleanup = $true }
    )
    $afterRows = @(
        [pscustomobject][ordered]@{ CaseId = "TT-02"; Phase = "AfterReplace"; Path = $manifest + ".next"; Kind = "ManifestNext"; Role = "Derived"; ConsumerAuthority = "None"; UnityConsumerAuthority = 0; NeverFallbackSelector = $true; ExistedAfterKill = $false; ObservedLength = 0L; ObservedSha256 = ""; RemovedAfterCleanup = $true },
        [pscustomobject][ordered]@{ CaseId = "TT-02"; Phase = "AfterReplace"; Path = $manifest + ".backup"; Kind = "ManifestBackup"; Role = "Derived"; ConsumerAuthority = "None"; UnityConsumerAuthority = 0; NeverFallbackSelector = $true; ExistedAfterKill = $true; ObservedLength = $bindingA.Length; ObservedSha256 = $bindingA.Sha256; RemovedAfterCleanup = $true }
    )
    $before = [pscustomobject][ordered]@{ Phase = "BeforeReplace"; CheckpointObserved = $true; CheckpointPath = (Join-Path $control "selector-BeforeReplace.checkpoint"); Killed = $true; WorkerPath = $workerPath; ProcessId = 1001; Started = $true; ExitedAfterKill = $true; ExitCode = -1; ManifestSha256 = $Context.ManifestShaA; IsManifestA = $true; IsManifestB = $false; Parseable = $true; Length = $bindingA.Length; TransactionArtifacts = $beforeRows }
    $after = [pscustomobject][ordered]@{ Phase = "AfterReplace"; CheckpointObserved = $true; CheckpointPath = (Join-Path $control "selector-AfterReplace.checkpoint"); Killed = $true; WorkerPath = $workerPath; ProcessId = 1002; Started = $true; ExitedAfterKill = $true; ExitCode = -1; ManifestSha256 = $Context.ManifestShaB; IsManifestA = $false; IsManifestB = $true; Parseable = $true; Length = $bindingB.Length; TransactionArtifacts = $afterRows }
    $transientRows = @($beforeRows) + @($afterRows)
    if ($Context.Mutation -ceq "TransientPathRelocation") { $transientRows[0].Path = [string]$transientRows[0].Path + ".relocated" }
    $record = [pscustomobject][ordered]@{ Schema = "D0M2T-SelectorAtomicKill-v1"; RunId = $Context.RunId; ManifestABytesSha256 = $Context.ManifestShaA; ManifestBBytesSha256 = $Context.ManifestShaB; BeforeReplace = $before; AfterReplace = $after; TransactionArtifacts = $transientRows; CompleteBytesOnly = $true; BothKillsObserved = $true }
    if ($Context.Mutation -ceq "TT02") { $record.BeforeReplace.Parseable = $false }
    if ($Context.Mutation -ceq "TT02NotReadyAsFailed") { $record.BeforeReplace.CheckpointObserved = $false }
    if ($Context.Mutation -ceq "TT02ContradictorySummaryAsFailed") { $record.CompleteBytesOnly = $false }
    if ($Context.Mutation -ceq "TransientRawOmission") { $record.TransactionArtifacts = @($record.TransactionArtifacts | Select-Object -First 3) }
    $worker = [pscustomobject][ordered]@{ Schema = "D0M2T-SelectorWorkerProcess-v1"; RunId = $Context.RunId; BeforeReplace = ($before | ConvertTo-Json | ConvertFrom-Json); AfterReplace = ($after | ConvertTo-Json | ConvertFrom-Json) }
    Add-SyntheticEvidence $Context "TT-02-manifest-snapshot" "TT-02" "ManifestSnapshot" "tt-02/selector.json" $record | Out-Null
    Add-SyntheticEvidence $Context "TT-02-worker-process" "TT-02" "ProcessLog" "tt-02/worker.json" $worker | Out-Null
    $Context.Objects.SelectorRecord = $record
    $Context.Objects.TransientRows = $transientRows
}

# 写出 TT-03 resolve kill、restart process、Unity B 与恢复摘要。
function Add-SyntheticTT03
{
    param($Context)

    $kill = New-SyntheticProcess $Context.RunId "TT-03" "resolve-kill-b" -1 $true $false
    $restart = New-SyntheticProcess $Context.RunId "TT-03" "restart-b" 0 $false $false
    $validation = [pscustomobject]@{ Passed = $true; TimedOut = $false; IdentityMatches = $true; AssemblySetMatches = $true; AllGeneration = $true; CacheMatches = $true; Detail = "synthetic" }
    $summary = [pscustomobject][ordered]@{ Schema = "D0M2T-ResolveKillRecovery-v1"; RunId = $Context.RunId; Killed = $true; ResultExistedAtKill = $false; KillTimedOut = $false; RestartExitCode = 0; RestartTimedOut = $false; RestartAllB = $true; RestartValidation = $validation }
    if ($Context.Mutation -ceq "TT03") { $summary.ResultExistedAtKill = $true }
    if ($Context.Mutation -ceq "TT03NotReadyAsFailed") { $restart.TimedOut = $true; $summary.RestartTimedOut = $true; $summary.RestartValidation.TimedOut = $true; $summary.RestartValidation.Passed = $false }
    if ($Context.Mutation -ceq "TT03ContradictorySummaryAsFailed") { $summary.RestartValidation.Passed = $false }
    if ($Context.Mutation -ceq "TT05NotReadyAsFailed") { $summary.RestartValidation.IdentityMatches = $false }
    Add-SyntheticEvidence $Context "TT-03-resolve-kill-b-process" "TT-03" "ProcessLog" "tt-03/kill.json" $kill | Out-Null
    Add-SyntheticEvidence $Context "TT-03-restart-b-process" "TT-03" "ProcessLog" "tt-03/restart.json" $restart | Out-Null
    Add-SyntheticEvidence $Context "TT-03-restart-b-unity" "TT-03" "UnityObservation" "tt-03/restart-unity.json" $Context.PayloadB | Out-Null
    Add-SyntheticEvidence $Context "TT-03-recovery-summary" "TT-03" "ProcessLog" "tt-03/summary.json" $summary | Out-Null
}

# 写出 TT-04 warm baseline 与 missing/corrupt fail-closed raw。
function Add-SyntheticTT04
{
    param($Context)

    $routeFailure = $Context.Mutation -cin @("TT04RouteRejected", "TT04WrongReason", "TT04NotReadyAsFailed", "TT04WarmPayloadNotReadyAsFailed", "TT04WarmExitNotReadyAsFailed")
    $missingExit = if ($routeFailure) { 0 } else { 1 }
    $missing = [pscustomobject]@{ ExitCode = $missingExit; SuccessfulObservation = $routeFailure; ResultPresent = $false }
    $corrupt = [pscustomobject]@{ InjectedSha256 = ("c" * 64); ExpectedSha256 = $Context.ShaB; ExitCode = 1; SuccessfulObservation = $false; ResultPresent = $false }
    $warmPayload = $Context.PayloadB | ConvertTo-Json -Depth 100 | ConvertFrom-Json
    if ($Context.Mutation -ceq "TT04WarmPayloadNotReadyAsFailed") { $warmPayload.Passed = $false }
    $warmExit = if ($Context.Mutation -ceq "TT04WarmExitNotReadyAsFailed") { 1 } else { 0 }
    $warmTimedOut = $Context.Mutation -ceq "TT04NotReadyAsFailed"
    $warmBaseline = -not $warmTimedOut -and $warmExit -eq 0 -and $warmPayload.Passed -eq $true
    $fault = [pscustomobject][ordered]@{ Schema = "D0M2T-MissingCorruptAuthority-v1"; RunId = $Context.RunId; WarmBaselineB = $warmBaseline; Missing = $missing; Corrupt = $corrupt; CacheFallbackAccepted = $routeFailure; AuthorityRestored = $true }
    if ($Context.Mutation -ceq "TT04") { $fault.Missing.SuccessfulObservation = $true }
    if ($Context.Mutation -ceq "TT04ContradictorySummaryAsFailed") { $fault.Missing.SuccessfulObservation = $true; $fault.CacheFallbackAccepted = $true }
    $warmProcess = New-SyntheticProcess $Context.RunId "TT-04" "warm-b" $warmExit $false $false
    $warmProcess.TimedOut = $warmTimedOut
    Add-SyntheticEvidence $Context "TT-04-warm-b-process" "TT-04" "ProcessLog" "tt-04/warm-process.json" $warmProcess | Out-Null
    Add-SyntheticEvidence $Context "TT-04-warm-b-unity" "TT-04" "UnityObservation" "tt-04/warm-unity.json" $warmPayload | Out-Null
    Add-SyntheticEvidence $Context "TT-04-missing-b-process" "TT-04" "ProcessLog" "tt-04/missing.json" (New-SyntheticProcess $Context.RunId "TT-04" "missing-b" $missingExit $false $false) | Out-Null
    Add-SyntheticEvidence $Context "TT-04-corrupt-b-process" "TT-04" "ProcessLog" "tt-04/corrupt.json" (New-SyntheticProcess $Context.RunId "TT-04" "corrupt-b" 1 $false $false) | Out-Null
    Add-SyntheticEvidence $Context "TT-04-fault-injection" "TT-04" "FaultInjectionRecord" "tt-04/fault.json" $fault | Out-Null
}

# 写出 TT-05 canonical manifest、lock A/B 与 audit-only raw。
function Add-SyntheticTT05
{
    param($Context)

    $reject = { param($Reason) [pscustomobject]@{ Accepted = $false; Reason = $Reason } }
    $manifest = [pscustomobject][ordered]@{
        Schema = "D0M2T-ManifestGate-v1"; RunId = $Context.RunId
        Canonical = [pscustomobject]@{ Accepted = $true; Reason = "None"; GenerationId = "B"; FileName = $Context.ArchiveB.FileName }
        InvalidJson = (& $reject "InvalidJson"); Alias = (& $reject "NonCanonicalUri"); Escape = (& $reject "NonCanonicalUri")
        InvalidRejected = $true; AliasRejected = $true; EscapeRejected = $true; FinalAllB = $true
    }
    if ($Context.Mutation -ceq "TT05") { $manifest.Alias.Accepted = $true }
    if ($Context.Mutation -ceq "TT05NotReadyAsFailed") { $manifest.Alias.Accepted = $true }
    if ($Context.Mutation -ceq "TT05ContradictorySummaryAsFailed") { $manifest.InvalidRejected = $false }
    $lockA = [pscustomobject]@{ Passed = $true; Path = "lock"; Source = "local-tarball"; Version = "file:../../Authority/$($Context.ArchiveA.FileName)"; Sha256 = ("e" * 64); ExpectedArchive = $Context.ArchiveA.FileName; Detail = "A" }
    $lockB = [pscustomobject]@{ Passed = $true; Path = "lock"; Source = "local-tarball"; Version = "file:../../Authority/$($Context.ArchiveB.FileName)"; Sha256 = ("f" * 64); ExpectedArchive = $Context.ArchiveB.FileName; Detail = "B" }
    $lock = [pscustomobject][ordered]@{ Schema = "D0M2T-LockDrift-v1"; RunId = $Context.RunId; StaleA = $lockA; FinalB = $lockB; DriftDidNotSelect = $true }
    $auditRaw = [pscustomobject][ordered]@{ Schema = "D0M2T-AuditRecord-v1"; RunId = $Context.RunId; GenerationId = "A"; ArchiveSha256 = $Context.ShaA; Role = "AuditOnly"; ConsumerAuthority = "None" }
    $auditText = (($auditRaw | ConvertTo-Json -Depth 8).Replace("`r`n", "`n").Replace("`r", "`n")) + "`n"
    $auditBytes = $script:Utf8NoBom.GetBytes($auditText)
    $audit = [pscustomobject][ordered]@{
        Schema = "D0M2T-AuditRecord-v1"; RunId = $Context.RunId; Path = (Join-Path $Context.Root "Fixture/Project/ProjectSettings/GasCodeGen/ActiveGenerationRef.json")
        Exists = $true; Length = [long]$auditBytes.LongLength; Sha256 = Get-SyntheticBytesSha256 $auditBytes; RawBytesBase64 = [Convert]::ToBase64String($auditBytes)
        Parseable = $true; ReadError = ""; GenerationId = "A"; ArchiveSha256 = $Context.ShaA; Role = "AuditOnly"; ConsumerAuthority = "None"; RemainedA = $true; UnityAllB = $true; Passed = $true
    }
    if ($Context.Mutation -ceq "TT05AuditMemoryForgery") { $audit.GenerationId = "B" }
    if ($Context.Mutation -ceq "TT05AuditBytesTamper") { $audit.RawBytesBase64 = [Convert]::ToBase64String($script:Utf8NoBom.GetBytes("{}`n")) }
    Add-SyntheticEvidence $Context "TT-05-manifest-gates" "TT-05" "ManifestSnapshot" "tt-05/manifest.json" $manifest | Out-Null
    Add-SyntheticEvidence $Context "TT-05-lock-drift" "TT-05" "PackageLockSnapshot" "tt-05/lock.json" $lock | Out-Null
    Add-SyntheticEvidence $Context "TT-05-audit-zero" "TT-05" "AuditRecordSnapshot" "tt-05/audit.json" $audit | Out-Null
}

# 写出 TT-06 两种合法 stale cache disposition、rsp canary 与 final Unity B raw。
function Add-SyntheticTT06
{
    param($Context)

    $project = Join-Path $Context.Root "Fixture/Project"
    $sourcePath = ([string]$Context.PayloadA.ResolvedPackagePath).Replace('\', '/')
    $stalePath = (Join-Path $project "Library/PackageCache/com.exhard.exgas.d0m2f-tarball@d0m2t-stale-a").Replace('\', '/')
    $finalPath = (Join-Path $project "Library/PackageCache/pkg-B").Replace('\', '/')
    $canaryBinding = Get-SyntheticTextBinding "d0m2f-tarball-generation-a|suite-owned-stale-rsp" $true
    $canary = [pscustomobject]@{ Path = "Library/Bee/d0m2t-stale-a.rsp"; Length = $canaryBinding.Length; Sha256 = $canaryBinding.Sha256; ContainsGenerationA = $true; ContainsGenerationB = $false }
    $before = [pscustomobject][ordered]@{ Schema = "D0M2T-CacheBeeSnapshot-v1"; RunId = $Context.RunId; Phase = "AfterA"; CacheEntries = @([pscustomobject]@{ Name = "pkg-A"; Path = $stalePath }); RspEntries = @($canary) }
    $after = [pscustomobject][ordered]@{ Schema = "D0M2T-CacheBeeSnapshot-v1"; RunId = $Context.RunId; Phase = "AfterB"; CacheEntries = @([pscustomobject]@{ Name = "pkg-A"; Path = $stalePath }, [pscustomobject]@{ Name = "pkg-B"; Path = $finalPath }); RspEntries = @($canary) }
    $cache = [pscustomobject][ordered]@{
        Schema = "D0M2T-StaleCache-v1"; RunId = $Context.RunId; Before = $before; After = $after; SourceAPath = $sourcePath; StaleAPath = $stalePath
        StaleAPreseeded = $true; StaleAStillExists = $true; StaleAInAfterSnapshot = $true; StaleAPresenceConsistent = $true
        RemainedUnselected = $true; CleanedBeforeFinal = $false; StaleADisposition = "RemainedUnselected"; StaleAResolvedWithoutSelection = $true
        FinalBPath = $finalPath; FinalUsesDifferentCache = $true
    }
    $bee = [pscustomobject][ordered]@{ Schema = "D0M2T-StaleBeeRsp-v1"; RunId = $Context.RunId; BeforeRsp = @($canary); AfterRsp = @($canary); CanaryPath = "Library/Bee/d0m2t-stale-a.rsp"; CanarySha256 = $canaryBinding.Sha256; CanaryContainsGenerationA = $true; CanaryStillExists = $true; StaleRspPreexisted = $true; FinalAllB = $true; GraphA = $Context.GraphA; GraphB = $Context.GraphB; GraphChanged = $true }
    if ($Context.Mutation -ceq "TT06CleanedBeforeFinal")
    {
        $after.CacheEntries = @([pscustomobject]@{ Name = "pkg-B"; Path = $finalPath })
        $cache.StaleAStillExists = $false; $cache.StaleAInAfterSnapshot = $false; $cache.RemainedUnselected = $false
        $cache.CleanedBeforeFinal = $true; $cache.StaleADisposition = "CleanedBeforeFinal"
    }
    if ($Context.Mutation -ceq "RspRawPathDivergence") { $bee.CanaryPath = "Library/Bee/diverged.rsp" }
    if ($Context.Mutation -ceq "TT06") { $cache.FinalUsesDifferentCache = $false }
    if ($Context.Mutation -ceq "TT06NotReadyAsFailed") { $before.CacheEntries = @(); $cache.StaleAPreseeded = $false }
    if ($Context.Mutation -ceq "TT06ContradictorySummaryAsFailed") { $cache.FinalUsesDifferentCache = $false }
    if ($Context.Mutation -ceq "TT06PresenceContradiction") { $cache.StaleAStillExists = $false }
    if ($Context.Mutation -ceq "TT06DispositionForgery") { $cache.StaleADisposition = "CleanedBeforeFinal" }
    if ($Context.Mutation -ceq "TT06ResolutionForgery") { $cache.StaleAResolvedWithoutSelection = $false }
    if ($Context.Mutation -ceq "TT06GraphSummaryAsFailed") { $bee.GraphChanged = $false }
    Add-SyntheticEvidence $Context "TT-06-cache-snapshot" "TT-06" "CacheSnapshot" "tt-06/cache.json" $cache | Out-Null
    Add-SyntheticEvidence $Context "TT-06-bee-rsp-snapshot" "TT-06" "BeeRspSnapshot" "tt-06/bee.json" $bee | Out-Null
    Add-SyntheticEvidence $Context "TT-06-final-unity" "TT-06" "UnityObservation" "tt-06/unity.json" $Context.PayloadB | Out-Null
}

# 创建与合同精确同集合的 protected snapshot bindings。
function New-SyntheticProtectedBindings
{
    return @($script:Contract.ProtectedPathSpecs | ForEach-Object {
        [pscustomobject][ordered]@{ Kind = [string]$_.Kind; Path = [string]$_.Path; Exists = $false; Digest = ""; FileCount = 0 }
    })
}

# 创建一个 selector-free 且已观察、已移除的 harness control 行。
function New-SyntheticHarnessRow
{
    param([string]$CaseId, [string]$Path, [string]$Kind, $Binding)

    $length = if ($null -eq $Binding) { 0L } else { [long]$Binding.Length }
    $sha = if ($null -eq $Binding) { "" } else { [string]$Binding.Sha256 }
    return [pscustomobject][ordered]@{
        CaseId = $CaseId; Path = $Path; Kind = $Kind; ConsumerAuthority = "None"; UnityConsumerAuthority = 0
        NeverFallbackSelector = $true; Observed = $true; ObservedLength = $length; ObservedSha256 = $sha; RemovedAfterCleanup = $true
    }
}

# 构造 runner 实际创建全集对应的 23 行 HarnessControl/FaultInjection inventory。
function New-SyntheticHarnessControls
{
    param($Context)

    $root = $Context.Root
    $roots = [ordered]@{
        Main = (Join-Path $root "Fixture"); Fault = (Join-Path $root "FaultFixture"); Hardlink = (Join-Path $root "HardlinkFixture")
        External = (Join-Path $root "ExternalFixture"); Junction = (Join-Path $root "JunctionFixture"); Sentinel = (Join-Path $root "SentinelFixture")
    }
    $rows = [Collections.Generic.List[object]]::new(); $ownerCases = @("TT-07", "TT-04", "TT-07", "TT-07", "TT-07", "TT-07")
    $rootValues = @($roots.Values)
    for ($index = 0; $index -lt $rootValues.Count; $index++)
    {
        $ownerBinding = Get-SyntheticTextBinding (([IO.Path]::GetFileName($rootValues[$index])) + "|" + $Context.RunId) $true
        $rows.Add((New-SyntheticHarnessRow $ownerCases[$index] (Join-Path $rootValues[$index] ".d0m2t-tarball-owner") "OwnerSentinel" $ownerBinding))
    }
    $manifestA = Get-SyntheticTextBinding (Get-SyntheticManifestText $Context.ArchiveA.FileName) $false
    $manifestB = Get-SyntheticTextBinding (Get-SyntheticManifestText $Context.ArchiveB.FileName) $false
    foreach ($pair in @(@("TT-02", $roots.Main), @("TT-04", $roots.Fault)))
    {
        $rows.Add((New-SyntheticHarnessRow $pair[0] (Join-Path $pair[1] "Control/manifest-A.json") "ManifestAControl" $manifestA))
        $rows.Add((New-SyntheticHarnessRow $pair[0] (Join-Path $pair[1] "Control/manifest-B.json") "ManifestBControl" $manifestB))
    }
    $canaryBinding = Get-SyntheticTextBinding "canary" $false
    $rows.Add((New-SyntheticHarnessRow "TT-01" (Join-Path $roots.Main "Control/watcher-canary-00000000000000000000000000000001") "WatcherCanary" $canaryBinding))
    $rows.Add((New-SyntheticHarnessRow "TT-04" (Join-Path $roots.Fault "Control/watcher-canary-00000000000000000000000000000002") "WatcherCanary" $canaryBinding))
    $workerBinding = Get-SyntheticTextBinding (Get-SyntheticSelectorWorkerText) $true
    $selector = $Context.Objects.SelectorRecord
    $rows.Add((New-SyntheticHarnessRow "TT-02" ([string]$selector.BeforeReplace.WorkerPath) "SelectorWorker" $workerBinding))
    foreach ($phase in @($selector.BeforeReplace, $selector.AfterReplace))
    {
        $checkpointBinding = Get-SyntheticTextBinding ([string]$phase.Phase) $false
        $rows.Add((New-SyntheticHarnessRow "TT-02" ([string]$phase.CheckpointPath) "SelectorWorkerCheckpoint" $checkpointBinding))
    }
    $rows.Add((New-SyntheticHarnessRow "TT-04" (Join-Path $roots.Fault "Control/selected-authority.missing") "MissingAuthorityHolding" ([pscustomobject]@{ Length = $Context.ArchiveB.ExpectedLength; Sha256 = $Context.ShaB })))
    $rows.Add((New-SyntheticHarnessRow "TT-06" (Join-Path $roots.Main "Project/Library/PackageCache/com.exhard.exgas.d0m2f-tarball@d0m2t-stale-a") "StaleCacheCanary" $null))
    $rows.Add((New-SyntheticHarnessRow "TT-06" (Join-Path $roots.Main "Project/Library/Bee/d0m2t-stale-a.rsp") "StaleRspCanary" (Get-SyntheticTextBinding "d0m2f-tarball-generation-a|suite-owned-stale-rsp" $true)))
    $boundaryBinding = Get-SyntheticTextBinding "boundary-canary" $true
    $rows.Add((New-SyntheticHarnessRow "TT-07" (Join-Path $roots.External "external-target.bin") "ExternalTargetCanary" $boundaryBinding))
    $rows.Add((New-SyntheticHarnessRow "TT-07" (Join-Path $roots.Hardlink "alias.bin") "HardlinkAttack" $boundaryBinding))
    $rows.Add((New-SyntheticHarnessRow "TT-07" (Join-Path $roots.Junction "junction-target") "ReparseTargetControl" $null))
    $rows.Add((New-SyntheticHarnessRow "TT-07" (Join-Path $roots.Junction "junction-alias") "ReparseAttack" $null))
    $rows.Add((New-SyntheticHarnessRow "TT-07" (Join-Path $roots.Sentinel ".d0m2t-tarball-owner") "ForgedSentinelAttack" (Get-SyntheticTextBinding "forged" $false)))
    if ($Context.Mutation -ceq "HarnessPathRelocation") { @($rows | Where-Object { $_.CaseId -ceq "TT-02" -and $_.Kind -ceq "ManifestAControl" })[0].Path += ".relocated" }
    if ($Context.Mutation -ceq "WorkerHashTamper") { @($rows | Where-Object Kind -CEQ "SelectorWorker")[0].ObservedSha256 = "f" * 64 }
    if ($Context.Mutation -ceq "DirectoryPathRelocation") { @($rows | Where-Object Kind -CEQ "StaleCacheCanary")[0].Path += ".relocated" }
    $Context.Objects.CreatedRoots = $rootValues; $Context.Objects.HarnessRows = @($rows)
    return @($rows)
}

# 写出 TT-07 三类边界、owned cleanup 与 protected before/after raw。
function Add-SyntheticTT07
{
    param($Context)

    $harnessRows = @(New-SyntheticHarnessControls $Context)
    $external = @($harnessRows | Where-Object Kind -CEQ "ExternalTargetCanary")[0]
    $externalRoot = [IO.Path]::GetDirectoryName([string]$external.Path)
    $hardlink = [pscustomobject]@{ Attack = "Hardlink"; Refused = $true; ExternalTargetRoot = $externalRoot; ExternalShaBefore = [string]$external.ObservedSha256; ExternalShaAfter = [string]$external.ObservedSha256; ExternalUnchanged = $true; AttackRootCleaned = $true; ExternalRootCleaned = $true; Cleaned = $true }
    $reparse = [pscustomobject]@{ Attack = "ReparsePoint"; Refused = $true; ExternalUnchanged = $true; Cleaned = $true }
    $sentinel = [pscustomobject]@{ Attack = "ForgedSentinel"; Refused = $true; ExternalUnchanged = $true; Cleaned = $true }
    $boundary = [pscustomobject][ordered]@{ Schema = "D0M2T-BoundaryCleanup-v1"; RunId = $Context.RunId; Attacks = @($hardlink, $reparse, $sentinel); HardlinkRejected = $true; ReparseRejected = $true; SentinelRejected = $true; ExternalUnchanged = $true; CleanupPassed = $true; Passed = $true }
    if ($Context.Mutation -ceq "TT07") { $boundary.Attacks[0].Refused = $false }
    $rawHarnessRows = if ($Context.Mutation -ceq "HarnessRawOmission") { @($harnessRows | Select-Object -First 22) } else { $harnessRows }
    $createdRoots = @($Context.Objects.CreatedRoots)
    $cleanup = [pscustomobject][ordered]@{ Schema = "D0M2T-Cleanup-v1"; RunId = $Context.RunId; Requested = $true; CreatedRoots = $createdRoots; RemovedRoots = $createdRoots; ResidualRoots = @(); AllOwnedOnly = $true; HarnessControlObservations = $rawHarnessRows; Passed = $true }
    $before = [pscustomobject][ordered]@{ Schema = "D0M2T-ProtectedSnapshot-v1"; RunId = $Context.RunId; Phase = "Before"; CapturedAtUtc = "2026-08-30T00:00:00Z"; Bindings = @(New-SyntheticProtectedBindings) }
    $after = [pscustomobject][ordered]@{ Schema = "D0M2T-ProtectedSnapshot-v1"; RunId = $Context.RunId; Phase = "After"; CapturedAtUtc = "2026-08-30T00:00:01Z"; Bindings = @(New-SyntheticProtectedBindings) }
    Add-SyntheticEvidence $Context "TT-07-protected-before" "TT-07" "ProtectedSnapshot" "tt-07/before.json" $before | Out-Null
    Add-SyntheticEvidence $Context "TT-07-boundary-record" "TT-07" "FilesystemBoundaryRecord" "tt-07/boundary.json" $boundary | Out-Null
    Add-SyntheticEvidence $Context "TT-07-cleanup-record" "TT-07" "CleanupRecord" "tt-07/cleanup.json" $cleanup | Out-Null
    Add-SyntheticEvidence $Context "TT-07-protected-after" "TT-07" "ProtectedSnapshot" "tt-07/after.json" $after | Out-Null
    $Context.Objects.Cleanup = $cleanup
}

# 写出不自哈希、精确绑定此前所有 raw 的 TT-08 closure。
function Add-SyntheticTT08
{
    param($Context)

    $prior = @($Context.EvidenceFiles | ForEach-Object { [pscustomobject][ordered]@{ EvidenceId = $_.EvidenceId; CaseId = $_.CaseId; Kind = $_.Kind; Path = $_.Path; Sha256 = $_.Sha256; Length = $_.Length } })
    $cleanup = $Context.Objects.Cleanup
    $closure = [pscustomobject][ordered]@{
        Schema = "D0M2T-EvidenceClosure-v1"; RunId = $Context.RunId; ClosedAtUtc = "2026-08-30T00:00:02Z"; EvidenceRoot = $Context.Root.Replace('\', '/')
        PriorEvidenceFiles = $prior
        Checks = [pscustomobject][ordered]@{ AllPathsRelative = $true; AllFilesOpenable = $true; LengthsMatch = $true; Sha256Match = $true; DuplicateEvidenceIds = $false; OrphanEvidenceIds = $false }
        FixtureCleanup = [pscustomobject][ordered]@{ Requested = $true; CreatedRoots = $cleanup.CreatedRoots; RemovedRoots = $cleanup.RemovedRoots; ResidualRoots = @() }
        OwnedProcessIds = @(); LiveOwnedProcessIds = @(); Passed = $true; Reason = "None"
    }
    Add-SyntheticEvidence $Context "TT-08-evidence-closure" "TT-08" "EvidenceClosure" "tt-08/closure.json" $closure | Out-Null
}

# 将 synthetic 场景映射到唯一伪造/真实 Failed case 与 route reason。
function Get-SyntheticFailureDescriptor
{
    param([string]$Mutation)

    switch ($Mutation)
    {
        "TT02NotReadyAsFailed" { return [pscustomobject]@{ CaseId = "TT-02"; Reason = "SelectorAtomicityViolation" } }
        "TT02ContradictorySummaryAsFailed" { return [pscustomobject]@{ CaseId = "TT-02"; Reason = "SelectorAtomicityViolation" } }
        "TT03NotReadyAsFailed" { return [pscustomobject]@{ CaseId = "TT-03"; Reason = "MixedGenerationObserved" } }
        "TT03ContradictorySummaryAsFailed" { return [pscustomobject]@{ CaseId = "TT-03"; Reason = "MixedGenerationObserved" } }
        "TT04RouteRejected" { return [pscustomobject]@{ CaseId = "TT-04"; Reason = "MissingOrCorruptAuthorityAccepted" } }
        "TT04FakeFailed" { return [pscustomobject]@{ CaseId = "TT-04"; Reason = "MissingOrCorruptAuthorityAccepted" } }
        "TT04WrongReason" { return [pscustomobject]@{ CaseId = "TT-04"; Reason = "SelectorAtomicityViolation" } }
        "TT04NotReadyAsFailed" { return [pscustomobject]@{ CaseId = "TT-04"; Reason = "MissingOrCorruptAuthorityAccepted" } }
        "TT04WarmPayloadNotReadyAsFailed" { return [pscustomobject]@{ CaseId = "TT-04"; Reason = "MissingOrCorruptAuthorityAccepted" } }
        "TT04WarmExitNotReadyAsFailed" { return [pscustomobject]@{ CaseId = "TT-04"; Reason = "MissingOrCorruptAuthorityAccepted" } }
        "TT04ContradictorySummaryAsFailed" { return [pscustomobject]@{ CaseId = "TT-04"; Reason = "MissingOrCorruptAuthorityAccepted" } }
        "TT05NotReadyAsFailed" { return [pscustomobject]@{ CaseId = "TT-05"; Reason = "ManifestLockDriftAccepted" } }
        "TT05ContradictorySummaryAsFailed" { return [pscustomobject]@{ CaseId = "TT-05"; Reason = "ManifestLockDriftAccepted" } }
        "TT06NotReadyAsFailed" { return [pscustomobject]@{ CaseId = "TT-06"; Reason = "StaleBeeOrRspConsumed" } }
        "TT06ContradictorySummaryAsFailed" { return [pscustomobject]@{ CaseId = "TT-06"; Reason = "CacheFallbackSelectorObserved" } }
        "TT06GraphSummaryAsFailed" { return [pscustomobject]@{ CaseId = "TT-06"; Reason = "StaleBeeOrRspConsumed" } }
        default { return $null }
    }
}

# 创建固定 TT-01..TT-08 Passed case 数组。
function New-SyntheticCases
{
    param($Context)

    $failure = Get-SyntheticFailureDescriptor $Context.Mutation
    return @($script:Contract.CaseSet | ForEach-Object {
        $failed = $null -ne $failure -and [string]$_.CaseId -ceq [string]$failure.CaseId
        $partial = $Context.Mutation -ceq "PartialInconclusive" -and [string]$_.CaseId -ceq "TT-06"
        $status = if ($failed) { "Failed" } elseif ($partial) { "Inconclusive" } else { "Passed" }
        $reason = if ($failed) { [string]$failure.Reason } elseif ($partial) { "EvidenceIncomplete" } else { "None" }
        [pscustomobject][ordered]@{ CaseId = [string]$_.CaseId; Name = [string]$_.Name; Status = $status; Reason = $reason; EvidenceIds = @($Context.CaseEvidence[[string]$_.CaseId]) }
    })
}

# 创建与所有 synthetic raw 交叉绑定的完整 Passed aggregate。
function New-SyntheticAggregate
{
    param($Context)

    $project = Join-Path $Context.Root "Fixture/Project"
    $authority = Join-Path $Context.Root "Fixture/Authority"
    $cleanup = $Context.Objects.Cleanup
    $failure = Get-SyntheticFailureDescriptor $Context.Mutation
    $routeRejected = $null -ne $failure
    $partial = $Context.Mutation -ceq "PartialInconclusive"
    $aggregateStatus = if ($routeRejected) { "RouteRejected" } elseif ($partial) { "Inconclusive" } else { "Passed" }
    $aggregateReason = if ($routeRejected) { [string]$failure.Reason } elseif ($partial) { "EvidenceIncomplete" } else { "None" }
    $roles = @(
        [pscustomobject]@{ Path = (Join-Path $authority $Context.ArchiveA.FileName); Role = "Authority"; ConsumerAuthority = "Payload"; UnityConsumerAuthority = 0; NeverFallbackSelector = $true },
        [pscustomobject]@{ Path = (Join-Path $authority $Context.ArchiveB.FileName); Role = "Authority"; ConsumerAuthority = "Payload"; UnityConsumerAuthority = 0; NeverFallbackSelector = $true },
        [pscustomobject]@{ Path = (Join-Path $project "Packages/manifest.json"); Role = "Authority"; ConsumerAuthority = "SoleUnitySelector"; UnityConsumerAuthority = 1; NeverFallbackSelector = $false },
        [pscustomobject]@{ Path = (Join-Path $project "Packages/packages-lock.json"); Role = "Derived"; ConsumerAuthority = "None"; UnityConsumerAuthority = 0; NeverFallbackSelector = $true },
        [pscustomobject]@{ Path = (Join-Path $project "ProjectSettings/GasCodeGen/ActiveGenerationRef.json"); Role = "Derived"; ConsumerAuthority = "AuditOnly"; UnityConsumerAuthority = 0; NeverFallbackSelector = $true },
        [pscustomobject]@{ Path = (Join-Path $project "Library/PackageCache"); Role = "Cache"; ConsumerAuthority = "None"; UnityConsumerAuthority = 0; NeverFallbackSelector = $true },
        [pscustomobject]@{ Path = (Join-Path $project "Library/Bee"); Role = "Cache"; ConsumerAuthority = "None"; UnityConsumerAuthority = 0; NeverFallbackSelector = $true }
    )
    $transientRows = @($Context.Objects.TransientRows | ConvertTo-Json -Depth 100 | ConvertFrom-Json)
    $harnessRows = @($Context.Objects.HarnessRows | ConvertTo-Json -Depth 100 | ConvertFrom-Json)
    if ($Context.Mutation -ceq "PersistentRoleOmission") { $roles = @($roles | Select-Object -First 6) }
    if ($Context.Mutation -ceq "PersistentRoleFlagTamper") { $roles[3].UnityConsumerAuthority = 1 }
    if ($Context.Mutation -ceq "TransientTopOmission") { $transientRows = @($transientRows | Select-Object -First 3) }
    if ($Context.Mutation -ceq "TransientFlagTamper") { $transientRows[0].UnityConsumerAuthority = 1 }
    if ($Context.Mutation -ceq "HarnessTopOmission") { $harnessRows = @($harnessRows | Select-Object -First 22) }
    if ($Context.Mutation -ceq "HarnessFlagTamper") { $harnessRows[0].NeverFallbackSelector = $false }
    return [pscustomobject][ordered]@{
        Schema = "D0M2T-Aggregate-v1"; RunId = $Context.RunId; Status = $aggregateStatus; Reason = $aggregateReason
        SelectedRoute = "ImmutableTarball"; SoleUnityConsumedSelector = "Packages/manifest.json"; Cases = @(New-SyntheticCases $Context)
        Inputs = [pscustomobject][ordered]@{
            UnityPath = (Join-Path $PSHOME "pwsh.exe"); UnitySha256 = (Get-FileHash -LiteralPath (Join-Path $PSHOME "pwsh.exe") -Algorithm SHA256).Hash.ToLowerInvariant()
            HarnessSha256 = (Get-FileHash -LiteralPath $script:TarballRunnerPath -Algorithm SHA256).Hash.ToLowerInvariant()
            ArchiveIndexSha256 = (Get-FileHash -LiteralPath $script:ArchiveIndexPath -Algorithm SHA256).Hash.ToLowerInvariant()
            FrozenInputs = @($script:Contract.FrozenInputs); FrozenRecordSha256 = [string]$script:Contract.FrozenRecordSha256
        }
        Authority = [pscustomobject][ordered]@{ PackageName = "com.exhard.exgas.d0m2f-tarball"; Archives = $Context.Archives; AuthorityWrites = 0 }
        Selector = [pscustomobject][ordered]@{ Path = "Packages/manifest.json"; ManifestAHash = $Context.ManifestShaA; ManifestBHash = $Context.ManifestShaB; FinalGeneration = "B"; AuditAuthority = 0 }
        Unity = [pscustomobject][ordered]@{ ExpectedVersion = "6000.3.14f1"; BaselineA = $Context.PayloadA; RestartB = $Context.PayloadB }
        RoleObservations = $roles
        TransientArtifactObservations = $transientRows
        HarnessControlObservations = $harnessRows
        Fixture = [pscustomobject][ordered]@{ Kept = $false; CreatedRoots = $cleanup.CreatedRoots; RemovedRoots = $cleanup.RemovedRoots; ResidualRoots = @(); CleanupPassed = $true }
        EvidenceFiles = @($Context.EvidenceFiles)
        Protected = [pscustomobject][ordered]@{ Unchanged = $true; BeforeSnapshotEvidenceId = "TT-07-protected-before"; AfterSnapshotEvidenceId = "TT-07-protected-after" }
        D1Authorized = $false; ProductionInstallAdmission = "NotEvaluated"; DeclaredFullSemanticEligibility = $false; NextGate = "D0-M2R"
    }
}

# 生成一个 SHA/closure 均有效的场景并运行 checker。
function Invoke-SyntheticScenario
{
    param([int]$Index, [AllowEmptyString()][string]$Mutation)

    $root = Join-Path ([IO.Path]::GetTempPath()) ("D0M2T-ContractSynthetic-" + [Guid]::NewGuid().ToString("N"))
    [IO.Directory]::CreateDirectory($root) | Out-Null
    $caseEvidence = @{}; foreach ($caseId in 1..8 | ForEach-Object { "TT-{0:D2}" -f $_ }) { $caseEvidence[$caseId] = [Collections.Generic.List[string]]::new() }
    $indexA = @($script:ArchiveIndex.Archives | Where-Object GenerationId -CEQ "A")[0]
    $indexB = @($script:ArchiveIndex.Archives | Where-Object GenerationId -CEQ "B")[0]
    $shaA = [string]$indexA.Sha256; $shaB = [string]$indexB.Sha256; $graphA = "1" * 64; $graphB = "2" * 64
    $archiveA = New-SyntheticArchive "A" $shaA ([int]$indexA.Length); $archiveB = New-SyntheticArchive "B" $shaB ([int]$indexB.Length)
    $manifestShaA = Get-SyntheticManifestSha256 ([string]$indexA.FileName); $manifestShaB = Get-SyntheticManifestSha256 ([string]$indexB.FileName)
    $context = [pscustomobject]@{
        Root = $root; RunId = "D0M2T-20260830T235959Z-$('{0:x12}' -f $Index)"; Mutation = $Mutation
        ShaA = $shaA; ShaB = $shaB; ManifestShaA = $manifestShaA; ManifestShaB = $manifestShaB; GraphA = $graphA; GraphB = $graphB; ArchiveA = $archiveA; ArchiveB = $archiveB; Archives = @($archiveA, $archiveB)
        PayloadA = New-SyntheticUnityPayload "A" $shaA $graphA $root; PayloadB = New-SyntheticUnityPayload "B" $shaB $graphB $root
        EvidenceFiles = [Collections.Generic.List[object]]::new(); CaseEvidence = $caseEvidence; Objects = @{}
    }
    try
    {
        Add-SyntheticTT01 $context; Add-SyntheticTT02 $context; Add-SyntheticTT03 $context; Add-SyntheticTT04 $context
        Add-SyntheticTT05 $context; Add-SyntheticTT06 $context; Add-SyntheticTT07 $context; Add-SyntheticTT08 $context
        $aggregate = New-SyntheticAggregate $context
        $aggregatePath = Join-Path $root "aggregate.json"
        [IO.File]::WriteAllText($aggregatePath, ($aggregate | ConvertTo-Json -Depth 100), $script:Utf8NoBom)
        $output = & pwsh -NoProfile -File $script:CheckerPath -Path $aggregatePath -EvidenceRoot $root -ExpectedRunId $context.RunId 2>&1 | Out-String
        return [pscustomobject]@{ Mutation = $Mutation; ExitCode = $LASTEXITCODE; Output = $output }
    }
    finally
    {
        $resolved = [IO.Path]::GetFullPath($root)
        if (-not $resolved.StartsWith([IO.Path]::GetTempPath(), [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -cnotlike "D0M2T-ContractSynthetic-*") { throw "Unsafe synthetic cleanup target: $resolved" }
        if ([IO.Directory]::Exists($resolved)) { [IO.Directory]::Delete($resolved, $true) }
    }
}

$baseline = Invoke-SyntheticScenario -Index 0 -Mutation ""
if ($baseline.ExitCode -ne 0) { throw "Synthetic 8-Passed baseline failed: $($baseline.Output)" }
$partialInconclusive = Invoke-SyntheticScenario -Index 1 -Mutation "PartialInconclusive"
if ($partialInconclusive.ExitCode -ne 0) { throw "Synthetic partial Inconclusive positive failed: $($partialInconclusive.Output)" }
$cleanedBeforeFinal = Invoke-SyntheticScenario -Index 2 -Mutation "TT06CleanedBeforeFinal"
if ($cleanedBeforeFinal.ExitCode -ne 0) { throw "Synthetic TT-06 CleanedBeforeFinal positive failed: $($cleanedBeforeFinal.Output)" }
$mutations = @(
    "TT01", "TT02", "TT03", "TT04", "TT05", "TT06", "TT07", "TT05AuditMemoryForgery", "TT05AuditBytesTamper",
    "PersistentRoleOmission", "PersistentRoleFlagTamper",
    "TransientTopOmission", "TransientRawOmission", "TransientFlagTamper", "TransientPathRelocation",
    "HarnessTopOmission", "HarnessRawOmission", "HarnessFlagTamper", "HarnessPathRelocation", "WorkerHashTamper", "DirectoryPathRelocation",
    "WatcherRawPathDivergence", "RspRawPathDivergence", "TT06PresenceContradiction", "TT06DispositionForgery", "TT06ResolutionForgery"
)
for ($index = 0; $index -lt $mutations.Count; $index++)
{
    $result = Invoke-SyntheticScenario -Index ($index + 1) -Mutation $mutations[$index]
    if ($result.ExitCode -eq 0 -or $result.Output -notmatch '"FailureReason":"EvidenceIncomplete"')
    {
        throw "Synthetic mutation '$($mutations[$index])' was not rejected semantically: $($result.Output)"
    }
}

$routeRejected = Invoke-SyntheticScenario -Index 8 -Mutation "TT04RouteRejected"
if ($routeRejected.ExitCode -ne 0) { throw "Synthetic TT04 RouteRejected positive failed: $($routeRejected.Output)" }
foreach ($scenario in @(@(9, "TT04FakeFailed"), @(10, "TT04WrongReason")))
{
    $result = Invoke-SyntheticScenario -Index $scenario[0] -Mutation $scenario[1]
    if ($result.ExitCode -eq 0 -or $result.Output -notmatch '"FailureReason":"EvidenceIncomplete"')
    {
        throw "Synthetic rejection scenario '$($scenario[1])' was not rejected semantically: $($result.Output)"
    }
}
$notReadyScenarios = @("TT02NotReadyAsFailed", "TT03NotReadyAsFailed", "TT04NotReadyAsFailed", "TT04WarmPayloadNotReadyAsFailed", "TT04WarmExitNotReadyAsFailed", "TT05NotReadyAsFailed", "TT06NotReadyAsFailed")
for ($index = 0; $index -lt $notReadyScenarios.Count; $index++)
{
    $result = Invoke-SyntheticScenario -Index ($index + 11) -Mutation $notReadyScenarios[$index]
    if ($result.ExitCode -eq 0 -or $result.Output -notmatch '"FailureReason":"EvidenceIncomplete"')
    {
        throw "Synthetic not-ready scenario '$($notReadyScenarios[$index])' was admitted as RouteRejected: $($result.Output)"
    }
}
$contradictoryScenarios = @(
    [pscustomobject]@{ Name = "TT02ContradictorySummaryAsFailed"; Message = "TT-02 derived summary contradicts phase raw." },
    [pscustomobject]@{ Name = "TT03ContradictorySummaryAsFailed"; Message = "TT-03 rejected RestartValidation.Passed contradicts raw evidence." },
    [pscustomobject]@{ Name = "TT04ContradictorySummaryAsFailed"; Message = "TT-04 Missing SuccessfulObservation contradicts process/result raw." },
    [pscustomobject]@{ Name = "TT05ContradictorySummaryAsFailed"; Message = "TT-05 derived summary contradicts gate/Unity raw." },
    [pscustomobject]@{ Name = "TT06ContradictorySummaryAsFailed"; Message = "TT-06 rejected cache summary contradicts snapshots/paths/presence." },
    [pscustomobject]@{ Name = "TT06GraphSummaryAsFailed"; Message = "TT-06 graph/final summary contradicts Unity raw." }
)
for ($index = 0; $index -lt $contradictoryScenarios.Count; $index++)
{
    $scenario = $contradictoryScenarios[$index]
    $result = Invoke-SyntheticScenario -Index ($index + 16) -Mutation $scenario.Name
    if ($result.ExitCode -eq 0 -or $result.Output -notmatch '"FailureReason":"EvidenceIncomplete"' -or $result.Output.IndexOf([string]$scenario.Message, [StringComparison]::Ordinal) -lt 0)
    {
        throw "Synthetic contradictory-summary scenario '$($scenario.Name)' did not hit its raw-binding guard: $($result.Output)"
    }
}

[pscustomobject][ordered]@{ Schema = "D0M2T-ContractSyntheticValidation-v1"; Passed = $true; Baseline = "8-Passed"; PartialInconclusivePositive = "TT06"; TT06DispositionPositives = @("RemainedUnselected", "CleanedBeforeFinal"); RouteRejectedPositive = "TT04"; RejectedMutations = $mutations; RejectedFalseConclusions = @("TT04FakeFailed", "TT04WrongReason"); RejectedNotReadyAsFailed = $notReadyScenarios; RejectedContradictorySummaries = @($contradictoryScenarios.Name) } | ConvertTo-Json -Depth 10
