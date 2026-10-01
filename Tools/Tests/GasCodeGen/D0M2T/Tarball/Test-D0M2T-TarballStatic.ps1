#requires -Version 7.0

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 解析 PowerShell 源文件并返回 AST，任意语法错误均 fail closed。
function Get-CheckedPowerShellAst
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors)
    if (@($errors).Count -ne 0) { throw "PowerShell AST errors in $Path`n$($errors -join "`n")" }
    return $ast
}

# 计算文件 SHA-256 并统一为小写十六进制。
function Get-StaticFileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [IO.File]::Exists($Path)) { throw "Required file is missing: $Path" }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# 核对 runner 的冻结参数、八案、终端字段与 raw schema 字符串均位于活动 AST。
function Assert-RunnerContractLiterals
{
    param([Parameter(Mandatory = $true)][Management.Automation.Language.ScriptBlockAst]$Ast)

    $parameters = @($Ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
    foreach ($name in @('UnityPath', 'OutputPath', 'EvidenceRoot', 'RunId', 'KeepFixtures'))
    {
        if ($name -cnotin $parameters) { throw "Runner parameter is missing: $name" }
    }
    $strings = @($Ast.FindAll({ param($node) $node -is [Management.Automation.Language.StringConstantExpressionAst] }, $true) | ForEach-Object Value)
    foreach ($literal in @(
        'D0M2T-Aggregate-v1', 'ImmutableTarball', 'Packages/manifest.json', 'D0-M2R',
        'BaselineAuthority', 'SelectorAtomicKill', 'ResolveKillRecovery', 'MissingCorruptAuthority',
        'ManifestLockDrift', 'StaleCacheBeeRsp', 'BoundaryCleanup', 'EvidenceClosure',
        'D0M2T-BaselineAuthority-v1', 'D0M2T-SelectorAtomicKill-v1', 'D0M2T-ResolveKillRecovery-v1',
        'D0M2T-MissingCorruptAuthority-v1', 'D0M2T-ManifestGate-v1', 'D0M2T-StaleBeeRsp-v1',
        'D0M2T-BoundaryCleanup-v1', 'D0M2T-EvidenceClosure-v1'))
    {
        if ($literal -cnotin $strings) { throw "Active runner AST literal is missing: $literal" }
    }
    foreach ($caseId in 1..8 | ForEach-Object { 'TT-{0:D2}' -f $_ })
    {
        if ($caseId -cnotin $strings) { throw "Active runner case id is missing: $caseId" }
    }
}

# 核对缺失 payload、stale RSP 与异常清理路径均以 fail-closed 方式收口。
function Assert-RunnerFailurePathGuards
{
    param([Parameter(Mandatory = $true)][string]$Source)

    foreach ($snippet in @(
        'AssemblySetMatches = $false; AllGeneration = $false; CacheMatches = $false',
        '$timedOut = $warm.TimedOut -or $missing.TimedOut -or $corrupt.TimedOut',
        '$ready = -not $timedOut -and $null -ne $warm.Payload -and $warmValidation.IdentityMatches -and $warmValidation.Passed',
        'Path = $auditPath; Exists = $auditExists; Length = $auditLength; Sha256 = $auditSha',
        'RawBytesBase64 = $auditRawBytesBase64',
        '$tt05Ready = $null -ne $restartB.Payload -and $restartValidation.IdentityMatches -and $auditSnapshot.Passed',
        '$script:CreatedFixtureRoots | Select-Object -Unique',
        'Remove-OwnedFixtureRoot -Root $normalized',
        '$overallReason = ''CleanupFailure''; $exitCode = 24',
        'New-PartialAggregate -Reason $overallReason'))
    {
        if (-not $Source.Contains($snippet)) { throw "Runner failure-path guard is missing: $snippet" }
    }
    $existsIndex = $Source.IndexOf('$staleRspStillExists = [IO.File]::Exists($staleRspPath)', [StringComparison]::Ordinal)
    $guardIndex = $Source.IndexOf('if ($staleRspStillExists)', $existsIndex, [StringComparison]::Ordinal)
    $readIndex = $Source.IndexOf('[IO.File]::ReadAllText($staleRspPath)', $guardIndex, [StringComparison]::Ordinal)
    if ($existsIndex -lt 0 -or $guardIndex -le $existsIndex -or $readIndex -le $guardIndex) { throw 'Stale RSP read is not guarded by Exists.' }
    $restartIndex = $Source.IndexOf("-ObservationName 'restart-b'", [StringComparison]::Ordinal)
    $auditReadIndex = $Source.IndexOf('[IO.File]::ReadAllBytes($auditPath)', $restartIndex, [StringComparison]::Ordinal)
    $auditEvidenceIndex = $Source.IndexOf("-EvidenceId 'TT-05-audit-zero'", $auditReadIndex, [StringComparison]::Ordinal)
    if ($restartIndex -lt 0 -or $auditReadIndex -le $restartIndex -or $auditEvidenceIndex -le $auditReadIndex) { throw 'TT-05 audit evidence is not reread after restart B.' }
}

# 核对 TT06 将 stale A 的保留与受控清理都视为 never-selector 的有效闭包。
function Assert-RunnerTT06Disposition
{
    param([Parameter(Mandatory = $true)][string]$Source)

    foreach ($snippet in @(
        '$staleARemainedUnselected = $staleAStillExists -and $staleAInAfterSnapshot -and $finalUsesDifferentCache',
        '$staleACleanedBeforeFinal = -not $staleAStillExists -and -not $staleAInAfterSnapshot -and $finalUsesDifferentCache',
        'StaleADisposition = $staleADisposition; StaleAResolvedWithoutSelection = $staleAResolvedWithoutSelection',
        '$tt06Ready = $null -ne $restartB.Payload -and $restartValidation.IdentityMatches -and $cacheRecord.StaleAPreseeded -and $cacheRecord.StaleAPresenceConsistent',
        '$tt06Passed = $tt06Ready -and $cacheRecord.StaleAResolvedWithoutSelection',
        'elseif (-not $cacheRecord.FinalUsesDifferentCache) { ''CacheFallbackSelectorObserved'' }'))
    {
        if (-not $Source.Contains($snippet)) { throw "TT06 disposition guard is missing: $snippet" }
    }
    $readyLine = [regex]::Match($Source, '(?m)^\s*\$tt06Ready\s*=.*$').Value
    if ($readyLine.Contains('$cacheRecord.StaleAStillExists') -or $readyLine.Contains('$cacheRecord.StaleAInAfterSnapshot')) { throw 'TT06 Ready still requires stale A physical survival.' }
}

# 核对 persistent、transient 与 harness control 三域均在 raw/aggregate 使用单一冻结 shape。
function Assert-RunnerRoleLifecycleClosure
{
    param([Parameter(Mandatory = $true)][string]$Source)

    foreach ($snippet in @(
        'UnityConsumerAuthority = 1; NeverFallbackSelector = $false',
        'Role = ''Derived''; ConsumerAuthority = ''None''; UnityConsumerAuthority = 0; NeverFallbackSelector = $true',
        'ExistedAfterKill = $exists; ObservedLength = if ($exists)',
        'TransactionArtifacts = @($transactionRows)',
        '$tt02TransactionReady = $tt02TransactionRows.Count -eq 4',
        'HarnessControlObservations = $harnessControlObservations',
        '$harnessControlsClosed = $harnessControlObservations.Count -eq 23',
        'TransientArtifactObservations = @($script:TransientArtifactObservations)',
        'Register-HarnessControlPath -CaseId $CaseId -Path (Join-Path $normalized $script:OwnerSentinelName) -Kind ''OwnerSentinel'''))
    {
        if (-not $Source.Contains($snippet)) { throw "Runner role/lifecycle closure is missing: $snippet" }
    }
    foreach ($kind in @('ManifestAControl', 'ManifestBControl', 'WatcherCanary', 'SelectorWorker', 'SelectorWorkerCheckpoint', 'MissingAuthorityHolding', 'StaleCacheCanary', 'StaleRspCanary', 'ExternalTargetCanary', 'HardlinkAttack', 'ReparseTargetControl', 'ReparseAttack', 'ForgedSentinelAttack'))
    {
        if (-not $Source.Contains("-Kind '$kind'")) { throw "Harness control kind is missing: $kind" }
    }
    if ([regex]::Matches($Source, 'New-OwnedFixtureRoot\s+-CaseId\s+''TT-0[1-8]''').Count -ne 6) { throw 'Every created fixture root must bind one owner-sentinel case.' }
}

# 核对 A/B archive 文件名、长度和 SHA 精确绑定 checked-in index。
function Assert-ArchiveIndex
{
    param([Parameter(Mandatory = $true)][string]$ArchiveRoot, [Parameter(Mandatory = $true)][object]$Index)

    if ($Index.Schema -cne 'D0M2F-TarballArchiveIndex-v1' -or $Index.PackageName -cne 'com.exhard.exgas.d0m2f-tarball') { throw 'Archive index identity mismatch.' }
    $archives = @($Index.Archives)
    if ($archives.Count -ne 2 -or (@($archives.GenerationId | Sort-Object) -join ',') -cne 'A,B') { throw 'Archive index must contain exact A/B.' }
    foreach ($archive in $archives)
    {
        $path = Join-Path $ArchiveRoot ([string]$archive.FileName); $sha = Get-StaticFileSha256 -Path $path
        if ($sha -cne [string]$archive.Sha256 -or [IO.Path]::GetFileNameWithoutExtension($path) -cne $sha) { throw "Archive content address mismatch: $path" }
        if ([long](Get-Item -LiteralPath $path).Length -ne [long]$archive.Length) { throw "Archive length mismatch: $path" }
    }
}

# 拒绝 archive 中的绝对/点段/大小写冲突路径以及 symlink/hardlink entry。
function Assert-ArchiveEntrySafety
{
    param([Parameter(Mandatory = $true)][string]$ArchivePath)

    $entries = @(& tar.exe -tzf $ArchivePath); if ($LASTEXITCODE -ne 0) { throw "Cannot list archive: $ArchivePath" }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $entries)
    {
        $normalized = ([string]$entry).Replace('\', '/').TrimEnd('/')
        if ([string]::IsNullOrWhiteSpace($normalized)) { continue }
        if ($normalized -match '^(?:/|[A-Za-z]:)' -or $normalized -match '(^|/)\.\.(/|$)') { throw "Unsafe archive entry: $entry" }
        if (-not $seen.Add($normalized)) { throw "Duplicate or case-colliding archive entry: $entry" }
    }
    $verbose = @(& tar.exe -tvzf $ArchivePath); if ($LASTEXITCODE -ne 0) { throw "Cannot inspect archive types: $ArchivePath" }
    if (@($verbose | Where-Object { ([string]$_).Length -gt 0 -and ([string]$_)[0] -in @('l', 'h') }).Count -ne 0) { throw "Archive contains a link entry: $ArchivePath" }
}

# 核对 package、三程序集 marker/定义和 A/B token 没有混代。
function Assert-ArchivePayload
{
    param([Parameter(Mandatory = $true)][string]$ArchivePath, [Parameter(Mandatory = $true)][object]$Archive)

    $entries = @(& tar.exe -tzf $ArchivePath)
    foreach ($entry in @(
        'package/package.json', 'package/Runtime/com.exhard.exgas.generated.runtime.asmdef',
        'package/Editor/com.exhard.exgas.generated.editor.asmdef',
        'package/AutoChessGenerated/com.exhard.exgas.autochessdemo.generated.asmref',
        'package/Runtime/GenerationMarker.cs', 'package/Editor/GenerationMarker.cs',
        'package/AutoChessGenerated/GenerationMarker.cs'))
    {
        if ($entry -cnotin $entries) { throw "Archive entry is missing: $entry" }
    }
    $packageJson = (& tar.exe -xOzf $ArchivePath 'package/package.json') -join "`n"
    if ($packageJson -notmatch [regex]::Escape('"version": "' + [string]$Archive.PackageVersion + '"')) { throw "Archive package version mismatch: $ArchivePath" }
    foreach ($markerPath in @('package/Runtime/GenerationMarker.cs', 'package/Editor/GenerationMarker.cs', 'package/AutoChessGenerated/GenerationMarker.cs'))
    {
        $marker = (& tar.exe -xOzf $ArchivePath $markerPath) -join "`n"
        if ($marker -notmatch [regex]::Escape('GenerationId = "' + [string]$Archive.GenerationId + '"') -or $marker -notmatch [regex]::Escape([string]$Archive.GenerationToken)) { throw "Archive marker drift: $markerPath" }
    }
}

# 检查 probe 的 meta、命令行入口、C#9 边界与项目要求的职责注释。
function Assert-UnityProbeSource
{
    param([Parameter(Mandatory = $true)][string]$SourcePath)

    $metaPath = $SourcePath + '.meta'; if (-not [IO.File]::Exists($metaPath)) { throw "Probe meta is missing: $metaPath" }
    $meta = [IO.File]::ReadAllText($metaPath); if ($meta -notmatch '(?m)^guid: [0-9a-f]{32}$') { throw 'Probe meta GUID is invalid.' }
    $source = [IO.File]::ReadAllText($SourcePath)
    foreach ($literal in @('D0M2TTarballUnityProbe', '-d0m2tOutput', '-d0m2tExpectedGeneration', '-d0m2tExpectedArchiveSha', 'CompileGraphSha256'))
    {
        if (-not $source.Contains($literal)) { throw "Probe source literal is missing: $literal" }
    }
    if ($source -cmatch '(?m)^\s*namespace\s+[A-Za-z0-9_.]+\s*;' -or $source -cmatch '\brecord\s+(?:class|struct)?\s*' -or $source -cmatch '\brequired\s+' -or $source -cmatch '(?m)^\s*global\s+using\s+') { throw 'Probe uses syntax beyond C# 9.' }
    $typeCount = ([regex]::Matches($source, '(?m)^\s*(?:public|internal|private)?\s*(?:static\s+|sealed\s+)*class\s+')).Count
    $methodCount = ([regex]::Matches($source, '(?m)^\s*(?:public|private|internal)\s+(?:static\s+)?[A-Za-z0-9_<>,\[\]]+\s+[A-Za-z0-9_]+\s*\(')).Count
    $summaryCount = ([regex]::Matches($source, '///\s*<summary>')).Count
    if ($summaryCount -lt ($typeCount + $methodCount)) { throw "Probe responsibilities are under-documented: summaries=$summaryCount types=$typeCount methods=$methodCount" }
}

# 复核 contract 当前 frozen inputs 与冻结记录的现场 raw SHA。
function Assert-FrozenInputs
{
    param([Parameter(Mandatory = $true)][string]$ProjectRoot, [Parameter(Mandatory = $true)][object]$Contract)

    foreach ($input in @($Contract.FrozenInputs))
    {
        if ((Get-StaticFileSha256 -Path (Join-Path $ProjectRoot ([string]$input.Path))) -cne [string]$input.Sha256) { throw "Frozen input drift: $($input.Path)" }
    }
    $record = Join-Path $ProjectRoot 'docs/reviews/RuntimeV1.1-D0-M2F-SpecADR规范冻结结果.md'
    if ((Get-StaticFileSha256 -Path $record) -cne [string]$Contract.FrozenRecordSha256) { throw 'Frozen record raw SHA drift.' }
}

$root = [IO.Path]::GetFullPath($PSScriptRoot)
$projectRoot = [IO.Path]::GetFullPath((Join-Path $root '../../../../..'))
$runner = Join-Path $root 'Run-D0M2T-TarballFaultExperiment.ps1'
$probe = Join-Path $root 'FixtureOverlay~/Assets/Editor/D0M2TTarballUnityProbe.cs'
$archiveRoot = [IO.Path]::GetFullPath((Join-Path $root '../../D0M2F/Tarball/Archives~'))
$contractPath = [IO.Path]::GetFullPath((Join-Path $root '../Contracts/D0M2T.contract.json'))
$runnerAst = Get-CheckedPowerShellAst -Path $runner
Get-CheckedPowerShellAst -Path $PSCommandPath | Out-Null
Assert-RunnerContractLiterals -Ast $runnerAst
$runnerSource = [IO.File]::ReadAllText($runner)
Assert-RunnerFailurePathGuards -Source $runnerSource
Assert-RunnerTT06Disposition -Source $runnerSource
Assert-RunnerRoleLifecycleClosure -Source $runnerSource
$index = Get-Content -LiteralPath (Join-Path $archiveRoot 'ArchiveIndex.json') -Raw | ConvertFrom-Json
Assert-ArchiveIndex -ArchiveRoot $archiveRoot -Index $index
foreach ($archive in @($index.Archives))
{
    $archivePath = Join-Path $archiveRoot ([string]$archive.FileName)
    Assert-ArchiveEntrySafety -ArchivePath $archivePath
    Assert-ArchivePayload -ArchivePath $archivePath -Archive $archive
}
Assert-UnityProbeSource -SourcePath $probe
$contract = Get-Content -LiteralPath $contractPath -Raw | ConvertFrom-Json
Assert-FrozenInputs -ProjectRoot $projectRoot -Contract $contract

[pscustomobject][ordered]@{
    Passed = $true
    PowerShellAst = 'Passed'
    RunnerContractLiterals = 'Passed'
    RunnerFailurePathGuards = 'Passed'
    RunnerTT06Disposition = 'Passed'
    RunnerRoleLifecycleClosure = 'Passed'
    ArchiveIdentity = 'Passed'
    ArchiveEntrySafety = 'Passed'
    ArchivePayload = 'Passed'
    UnityProbeCSharp9AndMeta = 'Passed'
    FrozenInputs = 'Passed'
    UnityStarted = $false
} | ConvertTo-Json -Depth 4
