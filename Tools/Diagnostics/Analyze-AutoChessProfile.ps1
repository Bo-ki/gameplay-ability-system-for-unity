[CmdletBinding()]
param(
    [string]$SummaryPath = "",
    [string]$OutputDirectory = "",
    [switch]$PrintMarkdown
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ProjectPath = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")
$ProjectPath = $ProjectPath.Path

if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    $SummaryPath = Join-Path $ProjectPath "TestResults\AutoChess\T6-CHESS-AF-SceneRuntime\AutoChessPlayModeProfileSummary.txt"
}
elseif (-not [System.IO.Path]::IsPathRooted($SummaryPath)) {
    $SummaryPath = Join-Path $ProjectPath $SummaryPath
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectPath "TestResults\AutoChess\Analysis"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectPath $OutputDirectory
}

if (-not (Test-Path -LiteralPath $SummaryPath)) {
    throw "Summary file not found: $SummaryPath"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Convert-ProfileScalar {
    param([string]$Value)

    $trimmed = $Value.Trim()
    if ($trimmed -match '^(?i:true|false)$') {
        return [bool]::Parse($trimmed)
    }

    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    $integer = [long]0
    if ([long]::TryParse(
            $trimmed,
            [System.Globalization.NumberStyles]::Integer,
            $culture,
            [ref]$integer)) {
        return $integer
    }

    $floating = [double]0
    if ([double]::TryParse(
            $trimmed,
            [System.Globalization.NumberStyles]::Float,
            $culture,
            [ref]$floating)) {
        return $floating
    }

    return $trimmed
}

function ConvertTo-KeyValueMap {
    param([string]$Body)

    $map = [ordered]@{}
    if ([string]::IsNullOrWhiteSpace($Body)) {
        return $map
    }

    foreach ($part in ($Body -split ',\s*')) {
        if ($part -match '^\s*([^=]+?)=(.*)$') {
            $key = $Matches[1].Trim()
            $value = $Matches[2].Trim()
            if ($key -match '\s+([A-Za-z0-9_]+)$') {
                $key = $Matches[1]
            }

            $map[$key] = Convert-ProfileScalar $value
        }
    }

    return $map
}

function ConvertTo-PipeKeyValueMap {
    param([string]$Body)

    $map = [ordered]@{}
    if ([string]::IsNullOrWhiteSpace($Body)) {
        return $map
    }

    foreach ($part in ($Body -split '\|')) {
        if ($part -match '^\s*([^=]+?)=(.*)$') {
            $key = $Matches[1].Trim()
            $value = $Matches[2].Trim()
            $map[$key] = Convert-ProfileScalar $value
        }
    }

    return $map
}

function ConvertTo-TimingSnapshot {
    param([string]$Body)

    $snapshot = [ordered]@{
        ecsRuntimeTickOnly = $null
        systems = [ordered]@{}
    }

    if ([string]::IsNullOrWhiteSpace($Body)) {
        return $snapshot
    }

    foreach ($segment in ($Body -split '\s*\|\s*')) {
        $trimmed = $segment.Trim()
        if ($trimmed -match '^ecsRuntimeTickOnly=(.*)$') {
            $snapshot.ecsRuntimeTickOnly = Convert-ProfileScalar $Matches[1]
            continue
        }

        if ($trimmed -match '^([A-Za-z0-9_]+)\(samples=(\d+),\s*avgMs=([0-9.]+),\s*maxMs=([0-9.]+)\)$') {
            $snapshot.systems[$Matches[1]] = [ordered]@{
                samples = [long]$Matches[2]
                avgMs = [double]::Parse($Matches[3], [System.Globalization.CultureInfo]::InvariantCulture)
                maxMs = [double]::Parse($Matches[4], [System.Globalization.CultureInfo]::InvariantCulture)
            }
        }
    }

    return $snapshot
}

function ConvertTo-TopNEntries {
    param(
        [object]$Value,
        [ValidateSet("Record", "System", "Component")]
        [string]$Kind
    )

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return @()
    }

    $entries = @()
    foreach ($part in ([string]$Value -split ';')) {
        $trimmed = $part.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        if ($Kind -eq "System" -and $trimmed -match '^([^@=]+)@([^=]+)=(\d+)$') {
            $entries += [pscustomobject]@{
                kind = $Kind
                operation = $Matches[1]
                target = $Matches[2]
                count = [long]$Matches[3]
            }
            continue
        }

        if ($Kind -eq "Component" -and $trimmed -match '^([^:=]+):([^=]+)=(\d+)$') {
            $entries += [pscustomobject]@{
                kind = $Kind
                operation = $Matches[1]
                target = $Matches[2]
                count = [long]$Matches[3]
            }
            continue
        }

        if ($Kind -eq "Record" -and $trimmed -match '^([^=]+)=(\d+)$') {
            $entries += [pscustomobject]@{
                kind = $Kind
                operation = $Matches[1]
                target = $Matches[1]
                count = [long]$Matches[2]
            }
        }
    }

    return @($entries | Sort-Object -Property count -Descending)
}

function Get-MapNumber {
    param(
        [System.Collections.IDictionary]$Map,
        [string]$Key,
        [double]$Default = 0
    )

    if ($null -eq $Map -or -not $Map.Contains($Key) -or $null -eq $Map[$Key]) {
        return $Default
    }

    return [double]$Map[$Key]
}

function Get-TimingAvg {
    param(
        [System.Collections.IDictionary]$Timing,
        [string]$SystemName
    )

    if ($null -eq $Timing -or -not $Timing.systems.Contains($SystemName)) {
        return 0
    }

    return [double]$Timing.systems[$SystemName].avgMs
}

function Format-Percent {
    param([double]$Value)

    return ("{0:n1}%" -f $Value)
}

function New-Finding {
    param(
        [string]$Id,
        [string]$Severity,
        [string]$Evidence,
        [string]$Inference,
        [string]$DesignMistake,
        [string]$NextProbe
    )

    return [pscustomobject]@{
        id = $Id
        severity = $Severity
        evidence = $Evidence
        inference = $Inference
        designMistake = $DesignMistake
        nextProbe = $NextProbe
    }
}

$lines = Get-Content -LiteralPath $SummaryPath
$sections = [ordered]@{}
foreach ($line in $lines) {
    if ($line -match '^(runtimeDataOrientedScorecard)\|') {
        $sections[$Matches[1]] = $line
        continue
    }

    if ($line -match '^([^:]+):\s*(.*)$') {
        $sections[$Matches[1]] = $Matches[2]
    }
}

function Get-Section {
    param([string[]]$Names)

    foreach ($name in $Names) {
        if ($sections.Contains($name) -and -not [string]::IsNullOrWhiteSpace([string]$sections[$name])) {
            return [string]$sections[$name]
        }
    }

    return ""
}

$performance = ConvertTo-KeyValueMap (Get-Section @(
        "HeadlessAutoChessPlayModeRunnerPerformance",
        "AutoChessDemoHeadlessRunnerPerformance"))
$timing = ConvertTo-TimingSnapshot (Get-Section @(
        "HeadlessAutoChessPlayModeTiming",
        "AutoChessDemoHeadlessRuntimeTiming"))
$debugger = ConvertTo-KeyValueMap (Get-Section @(
        "HeadlessAutoChessPlayModeDebugger",
        "AutoChessDemoHeadlessRuntimeDebugger"))
$official = ConvertTo-KeyValueMap (Get-Section @(
        "HeadlessAutoChessPlayModeOfficialToolDiff",
        "AutoChessDemoHeadlessOfficialToolDiff"))
$profiler = ConvertTo-KeyValueMap (Get-Section @(
        "HeadlessAutoChessPlayModeProfiler"))
if ($profiler.Count -eq 0) {
    $profiler = ConvertTo-KeyValueMap (Get-Section @(
            "AutoChessDemoHeadlessOfficialToolDiff"))
}

$diagnostic = ConvertTo-KeyValueMap (Get-Section @(
        "HeadlessAutoChessPlayModeDiagnosticRunner",
        "AutoChessDemoHeadlessRuntimeDebugger"))
$logicBudget = ConvertTo-KeyValueMap (Get-Section @(
        "AutoChessDemoHeadlessLogicBudget",
        "HeadlessAutoChessPlayModeLogicBudget"))
$hotspots = ConvertTo-KeyValueMap (Get-Section @(
        "AutoChessDemoHeadlessRuntimeHotspots",
        "HeadlessAutoChessPlayModeHotspots"))
$scorecard = ConvertTo-PipeKeyValueMap (Get-Section @(
        "runtimeDataOrientedScorecard"))

$recordTopN = ConvertTo-TopNEntries $official["journalingRecordTopN"] "Record"
$systemTopN = ConvertTo-TopNEntries $official["journalingSystemTopN"] "System"
$componentTopN = ConvertTo-TopNEntries $official["journalingComponentTopN"] "Component"

$measuredTicks = [math]::Max(1, (Get-MapNumber $performance "measuredTicks"))
$diagnosticTicks = [math]::Max(1, (Get-MapNumber $diagnostic "measuredTicks" $measuredTicks))
$totalAvg = Get-TimingAvg $timing "GASTickTotal"
$commandAvg = Get-TimingAvg $timing "GASCommandResolveSystemGroup"
$coreAvg = Get-TimingAvg $timing "GASCoreSimulationSystemGroup"
$structuralAvg = Get-TimingAvg $timing "GASStructuralCommitSystemGroup"
$boundaryAvg = Get-TimingAvg $timing "GASBoundaryProjectionSystemGroup"
$prepareAvg = Get-TimingAvg $timing "GASFramePrepareSystemGroup"
$debuggerOwnerAvg = Get-TimingAvg $timing "DebuggerOwner"
$runnerAvg = Get-TimingAvg $timing "RunnerOwner"

$getComponentRw = Get-MapNumber $official "journalingGetComponentDataRW"
$getBufferRw = Get-MapNumber $official "journalingGetBufferRW"
$enableComponents = Get-MapNumber $official "journalingEnableComponents"
$disableComponents = Get-MapNumber $official "journalingDisableComponents"
$worldRecords = Get-MapNumber $official "journalingWorldRecords"
$eventCount = Get-MapNumber $debugger "events"
$droppedEvents = Get-MapNumber $debugger "dropped"
$profilerFirstFrame = Get-MapNumber $profiler "firstFrameIndex" -1
$profilerLastFrame = Get-MapNumber $profiler "lastFrameIndex" -1
$executionSpecScans = Get-MapNumber $performance "executionSpecScans" (Get-MapNumber $hotspots "executionSpecScans")
$executionMatchedEffectSpecs = Get-MapNumber $performance "executionMatchedEffectSpecs" (Get-MapNumber $hotspots "executionMatchedEffectSpecs")
$ownerLocalFactFlushes = Get-MapNumber $debugger "ownerLocalFactFlushes" (Get-MapNumber $performance "ownerLocalFactFlushes")
$diagnosticObservationQueries = Get-MapNumber $debugger "observationMaterializedQueries"
$diagnosticObservationEntities = Get-MapNumber $debugger "observationMaterializedEntities"
$diagnosticObservationMicroseconds = Get-MapNumber $debugger "observationMaterializationUs"
$scorecardCoreFacts = Get-MapNumber $scorecard "coreFacts"
$scorecardActiveMutationCommands = Get-MapNumber $scorecard "activeMutationCommands"
$scorecardOwnerLocalFactMaxOwnerRange = Get-MapNumber $scorecard "ownerLocalFactMaxOwnerRange" (
    Get-MapNumber $debugger "ownerLocalFactMaxOwnerRange")
$scorecardMetricFamilyMask = if ($scorecard.Contains("metricFamilyMask")) { $scorecard["metricFamilyMask"] } else { "" }
$metricFamilySource = if ($logicBudget.Contains("metricFamilySource")) { $logicBudget["metricFamilySource"] } else { "" }
$performanceExcellentPassed = $null
if ($logicBudget.Contains("performanceExcellentPassed")) {
    $performanceExcellentPassed = $logicBudget["performanceExcellentPassed"]
}

$profilerEvidencePassed = $null
if ($logicBudget.Contains("profilerEvidencePassed")) {
    $profilerEvidencePassed = $logicBudget["profilerEvidencePassed"]
}
elseif ($scorecard.Contains("profilerEvidencePassed")) {
    $profilerEvidencePassed = $scorecard["profilerEvidencePassed"]
}

$derived = [ordered]@{
    measuredTicks = $measuredTicks
    totalElapsedSeconds = (Get-MapNumber $performance "totalElapsedMs") / 1000.0
    ticksPerSecond = $measuredTicks / [math]::Max(0.001, ((Get-MapNumber $performance "totalElapsedMs") / 1000.0))
    commandsPerTick = (Get-MapNumber $performance "commands") / $measuredTicks
    attributeChangesPerTick = (Get-MapNumber $performance "attributeChanges") / $measuredTicks
    executionOutputsPerTick = (Get-MapNumber $performance "executionOutputs") / $measuredTicks
    cueRequestsPerTick = (Get-MapNumber $performance "cueRequests") / $measuredTicks
    getComponentDataRwPerTick = $getComponentRw / $measuredTicks
    getBufferRwPerTick = $getBufferRw / $measuredTicks
    totalRwPerTick = ($getComponentRw + $getBufferRw) / $measuredTicks
    enableableTogglePerTick = ($enableComponents + $disableComponents) / $measuredTicks
    enablePerTick = $enableComponents / $measuredTicks
    disablePerTick = $disableComponents / $measuredTicks
    commandResolveSharePct = if ($totalAvg -gt 0) { 100.0 * $commandAvg / $totalAvg } else { 0 }
    coreSimulationSharePct = if ($totalAvg -gt 0) { 100.0 * $coreAvg / $totalAvg } else { 0 }
    structuralCommitSharePct = if ($totalAvg -gt 0) { 100.0 * $structuralAvg / $totalAvg } else { 0 }
    boundaryProjectionSharePct = if ($totalAvg -gt 0) { 100.0 * $boundaryAvg / $totalAvg } else { 0 }
    framePrepareSharePct = if ($totalAvg -gt 0) { 100.0 * $prepareAvg / $totalAvg } else { 0 }
    runnerSharePct = if ($totalAvg -gt 0) { 100.0 * $runnerAvg / $totalAvg } else { 0 }
    debuggerOwnerAvgMs = $debuggerOwnerAvg
    debuggerOwnerVsGasTick = if ($totalAvg -gt 0) { $debuggerOwnerAvg / $totalAvg } else { 0 }
    diagnosticRequestsPerTick = (Get-MapNumber $debugger "requests") / $diagnosticTicks
    diagnosticFactsPerTick = (Get-MapNumber $debugger "facts") / $diagnosticTicks
    diagnosticPresentationPerTick = (Get-MapNumber $debugger "presentation") / $diagnosticTicks
    diagnosticObservationQueries = $diagnosticObservationQueries
    diagnosticObservationEntities = $diagnosticObservationEntities
    diagnosticObservationEntitiesPerTick = $diagnosticObservationEntities / $diagnosticTicks
    diagnosticObservationMicroseconds = $diagnosticObservationMicroseconds
    scorecardCoreFacts = $scorecardCoreFacts
    scorecardActiveMutationCommands = $scorecardActiveMutationCommands
    scorecardOwnerLocalFactMaxOwnerRange = $scorecardOwnerLocalFactMaxOwnerRange
    scorecardMetricFamilyMask = $scorecardMetricFamilyMask
    metricFamilySource = $metricFamilySource
    scorecardConceptEvidenceMissing = ($scorecardCoreFacts -le 0 -and (Get-MapNumber $debugger "facts") -gt 0)
    debuggerDropRatePct = if (($eventCount + $droppedEvents) -gt 0) { 100.0 * $droppedEvents / ($eventCount + $droppedEvents) } else { 0 }
    profilerDriverFrameCount = if ($profilerLastFrame -ge $profilerFirstFrame -and $profilerFirstFrame -ge 0) { $profilerLastFrame - $profilerFirstFrame + 1 } else { 0 }
    journalingReachedDefaultCap = ($worldRecords -ge 524288)
    executionSpecScansPerMatchedEffect = if ($executionMatchedEffectSpecs -gt 0) { $executionSpecScans / $executionMatchedEffectSpecs } else { 0 }
    executionSpecScansPerTick = $executionSpecScans / $measuredTicks
    ownerLocalFactFlushesPerTick = $ownerLocalFactFlushes / $measuredTicks
    performanceExcellentPassed = $performanceExcellentPassed
    profilerEvidencePassed = $profilerEvidencePassed
    dominantRisk = if ($scorecard.Contains("dominantRisk")) { $scorecard["dominantRisk"] } else { "" }
}

$findings = @()

if ($derived.scorecardConceptEvidenceMissing) {
    $findings += New-Finding `
        "GAS-MEASURE-03" `
        "High" `
        ("Scorecard coreFacts is {0:n0}, but debugger facts is {1:n0}; metricFamilySource={2}." -f $derived.scorecardCoreFacts, (Get-MapNumber $debugger "facts"), $derived.metricFamilySource) `
        "The budget read model is losing GAS concept/data-shape evidence while the debugger captured it." `
        "Performance timing and diagnostic metric families were coupled to one pass instead of being treated as a deliberate validation read model." `
        "Build the scorecard from performance timing plus diagnostic GAS/data/API families, while keeping performance-pass overhead as the pollution gate."
}

if ($derived.totalRwPerTick -gt 50) {
    $findings += New-Finding `
        "GAS-ARCH-01" `
        "High" `
        ("Journaling RW reads are {0:n1}/tick: GetComponentDataRW={1:n1}/tick, GetBufferRW={2:n1}/tick." -f $derived.totalRwPerTick, $derived.getComponentDataRwPerTick, $derived.getBufferRwPerTick) `
        "The hot path is still shaped around random entity/buffer lookup instead of chunk-local or owner-local batches." `
        "Runtime Core modules are too shallow around ability commit, cleanup, and attribute recalculation; callers still pay ECS lookup details every tick." `
        "Split TopN by system and add per-system chunk/entity match counters; prove which paths can move to IJobChunk or owner-local dirty sets."
}

if ($derived.enableableTogglePerTick -gt 10) {
    $findings += New-Finding `
        "GAS-ARCH-02" `
        "High" `
        ("Enableable toggles are {0:n1}/tick: Enable={1:n1}/tick, Disable={2:n1}/tick." -f $derived.enableableTogglePerTick, $derived.enablePerTick, $derived.disablePerTick) `
        "The data is not suffering from structural changes; it is suffering from a per-command enableable-state protocol." `
        "Ability lifecycle is encoded as too many request/commit/end marker flips instead of a compact per-owner command state or chunk batch." `
        "Measure toggle count by component and phase; replace repeated commit/end toggles with a compact command queue state where semantics allow it."
}

if ($derived.coreSimulationSharePct + $derived.commandResolveSharePct -gt 80 -and $derived.structuralCommitSharePct -lt 5) {
    $findings += New-Finding `
        "GAS-ARCH-03" `
        "High" `
        ("CommandResolve + CoreSimulation consume {0}; StructuralCommit is only {1}." -f (Format-Percent ($derived.coreSimulationSharePct + $derived.commandResolveSharePct)), (Format-Percent $derived.structuralCommitSharePct)) `
        "The old architecture priority over-weighted structural-change avoidance; current x50 bottleneck is data access and lifecycle resolution." `
        "Frame backbone has phase names, but the deeper module seams still expose low-level command/spec/delta/fact plumbing to too many systems." `
        "Stop treating ECB count as the primary success metric; gate on RW lookup budget, dirty-set size, command fan-in cost, and system count."
}

$hotComponent = $componentTopN | Where-Object { ($_.count / $measuredTicks) -gt 500 } | Select-Object -First 1
if ($null -ne $hotComponent) {
    $findings += New-Finding `
        "GAS-ARCH-04" `
        "High" `
        ("{0} is a hot component RW target: {1:n0} records, {2:n1}/tick." -f $hotComponent.target, $hotComponent.count, ($hotComponent.count / $measuredTicks)) `
        "The runtime still pays high buffer RW volume even when structural changes are near zero." `
        "Owner-local stores exist, but the execution/attribute/fact path still exposes broad buffer surfaces to too many systems." `
        "Add per-owner dirty counters and prove which TopN component paths can become chunk-local or fixed-width metric streams."
}

if ($derived.scorecardOwnerLocalFactMaxOwnerRange -gt 4) {
    $findings += New-Finding `
        "GAS-ARCH-07" `
        "High" `
        ("OwnerLocalFact max owner range is {0:n0}; owner-local facts flush {1:n1}/tick." -f $derived.scorecardOwnerLocalFactMaxOwnerRange, $derived.ownerLocalFactFlushesPerTick) `
        "The fact path has owner-local storage, but the hot range is still too broad for a strict chunk-local DOTS shape." `
        "Fact fan-in is represented as mutable buffers with repeated owner flushes instead of a compact dirty-owner reduction lane." `
        "Add dirty owner/fact span counters and move OwnerLocalGameplayFactBuffer writes behind a generated fact reduce/apply lane."
}

if ($derived.executionSpecScansPerMatchedEffect -gt 2.0) {
    $findings += New-Finding `
        "GAS-ARCH-06" `
        "High" `
        ("Execution scans {0:n0} specs for {1:n0} matched effects, ratio={2:n2}:1." -f $executionSpecScans, $executionMatchedEffectSpecs, $derived.executionSpecScansPerMatchedEffect) `
        "The execution-calculation path still has fan-out scan cost that grows faster than matched work." `
        "Execution definitions are not yet indexed as a compact generated lookup matched to the active gameplay effect command stream." `
        "Generate a calculation-code to effect-spec index and compare spec scan count against matched effect count in the next run."
}

if ($debuggerOwnerAvg -gt 10 -or ($totalAvg -gt 0 -and $debuggerOwnerAvg -gt $totalAvg * 5)) {
    $findings += New-Finding `
        "GAS-DBG-01" `
        "High" `
        ("DebuggerOwner avg is {0:n3} ms for {1:n0} materialized entities and {2:n0} queries; GASTick avg is {3:n3} ms." -f $debuggerOwnerAvg, $diagnosticObservationEntities, $diagnosticObservationQueries, $totalAvg) `
        "The debugger can locate gameplay hot spots, but its diagnostic materialization/export pass is itself a large one-shot cost." `
        "Runtime diagnostics still mix validation evidence, materialization, and text-export obligations too closely." `
        "Keep performance pass counter-only, and split DiagnosticMaterializationPass/export into an explicitly budgeted post-run owner."
}

if ($null -ne $profilerEvidencePassed -and -not [bool]$profilerEvidencePassed) {
    $findings += New-Finding `
        "GAS-MEASURE-02" `
        "Medium" `
        ("Profiler evidence is missing: performanceExcellentPassed={0}, dominantRisk={1}." -f $performanceExcellentPassed, $derived.dominantRisk) `
        "Runtime self-diagnostics and Journaling are enough to find current hot spots, but the official Profiler leg is not enabled." `
        "The validation contract now distinguishes headless budget pass from DOTS-profiler-backed performance excellence, but the runner still defaults to disabled Profiler." `
        "Add an explicit profiler-enabled validation mode or record a hard reason why batchmode Profiler is unavailable on this machine."
}

if ($derived.debuggerDropRatePct -gt 25 -or $derived.journalingReachedDefaultCap) {
    $findings += New-Finding `
        "GAS-ARCH-05" `
        "Medium" `
        ("Debugger drop rate is {0}; Journaling records={1:n0}, capReached={2}." -f (Format-Percent $derived.debuggerDropRatePct), $worldRecords, $derived.journalingReachedDefaultCap) `
        "Raw event capture is too fine-grained for x50; the evidence layer must aggregate first and sample raw details second." `
        "Diagnostics were designed as event logs before they were designed as bounded counters and profiler-diff summaries." `
        "Keep hot-path diagnostics as counters; add a separate opt-in trace sampling mode for short windows only."
}

if ($derived.profilerDriverFrameCount -gt 0 -and $derived.profilerDriverFrameCount -le 512) {
    $findings += New-Finding `
        "GAS-MEASURE-01" `
        "Medium" `
        ("ProfilerDriver frame history contains {0} frames while elapsed time is {1:n1}s." -f $derived.profilerDriverFrameCount, $derived.totalElapsedSeconds) `
        "ProfilerDriver SaveProfile is only a windowed companion; binary log is the durable official capture for the 30s run." `
        "The measurement module previously conflated profiler-window metadata with full-run evidence." `
        "Prefer binary log existence/size and explicit frame window metadata in reports; do not infer full timeline coverage from first/last frame alone."
}

$analysis = [ordered]@{
    source = [ordered]@{
        summaryPath = $SummaryPath
        generatedAt = (Get-Date).ToString("o")
    }
    performance = $performance
    timing = $timing
    debugger = $debugger
    diagnostic = $diagnostic
    logicBudget = $logicBudget
    hotspots = $hotspots
    scorecard = $scorecard
    officialToolDiff = $official
    profiler = $profiler
    derived = $derived
    topN = [ordered]@{
        records = $recordTopN
        systems = $systemTopN
        components = $componentTopN
    }
    findings = $findings
}

$jsonPath = Join-Path $OutputDirectory "AutoChessProfileAnalysis.json"
$markdownPath = Join-Path $OutputDirectory "AutoChessProfileAnalysis.md"

$json = $analysis | ConvertTo-Json -Depth 12
[System.IO.File]::WriteAllText($jsonPath, $json, [System.Text.UTF8Encoding]::new($false))

$builder = [System.Text.StringBuilder]::new()
[void]$builder.AppendLine("# AutoChess x50 Runtime Profile Analysis")
[void]$builder.AppendLine()
[void]$builder.AppendLine("Source: ``$SummaryPath``")
[void]$builder.AppendLine()
[void]$builder.AppendLine("## Summary")
[void]$builder.AppendLine()
[void]$builder.AppendLine(("- elapsed: {0:n3}s" -f $derived.totalElapsedSeconds))
[void]$builder.AppendLine(("- measured ticks: {0:n0}" -f $derived.measuredTicks))
[void]$builder.AppendLine(("- avg tick: {0:n3} ms" -f (Get-MapNumber $performance "avgTickMs")))
[void]$builder.AppendLine(("- commands/tick: {0:n1}" -f $derived.commandsPerTick))
[void]$builder.AppendLine(("- attributes/tick: {0:n1}" -f $derived.attributeChangesPerTick))
[void]$builder.AppendLine(("- cue requests/tick: {0:n1}" -f $derived.cueRequestsPerTick))
[void]$builder.AppendLine(("- performance excellent: {0}" -f $derived.performanceExcellentPassed))
[void]$builder.AppendLine(("- dominant risk: {0}" -f $derived.dominantRisk))
[void]$builder.AppendLine()
[void]$builder.AppendLine("## Cost Split")
[void]$builder.AppendLine()
[void]$builder.AppendLine("| Group | avg ms | share |")
[void]$builder.AppendLine("|---|---:|---:|")
[void]$builder.AppendLine(("| FramePrepare | {0:n3} | {1} |" -f $prepareAvg, (Format-Percent $derived.framePrepareSharePct)))
[void]$builder.AppendLine(("| CommandResolve | {0:n3} | {1} |" -f $commandAvg, (Format-Percent $derived.commandResolveSharePct)))
[void]$builder.AppendLine(("| CoreSimulation | {0:n3} | {1} |" -f $coreAvg, (Format-Percent $derived.coreSimulationSharePct)))
[void]$builder.AppendLine(("| StructuralCommit | {0:n3} | {1} |" -f $structuralAvg, (Format-Percent $derived.structuralCommitSharePct)))
[void]$builder.AppendLine(("| BoundaryProjection | {0:n3} | {1} |" -f $boundaryAvg, (Format-Percent $derived.boundaryProjectionSharePct)))
[void]$builder.AppendLine(("| RunnerOwner | {0:n3} | {1} |" -f $runnerAvg, (Format-Percent $derived.runnerSharePct)))
[void]$builder.AppendLine(("| DebuggerOwner | {0:n3} | outside tick |" -f $debuggerOwnerAvg))
[void]$builder.AppendLine()
[void]$builder.AppendLine("## Journaling Rates")
[void]$builder.AppendLine()
[void]$builder.AppendLine(("- GetComponentDataRW/tick: {0:n1}" -f $derived.getComponentDataRwPerTick))
[void]$builder.AppendLine(("- GetBufferRW/tick: {0:n1}" -f $derived.getBufferRwPerTick))
[void]$builder.AppendLine(("- Enableable toggles/tick: {0:n1}" -f $derived.enableableTogglePerTick))
[void]$builder.AppendLine(("- Journaling cap reached: {0}" -f $derived.journalingReachedDefaultCap))
[void]$builder.AppendLine()
[void]$builder.AppendLine("## Debugger Evidence")
[void]$builder.AppendLine()
[void]$builder.AppendLine(("- diagnostics source: {0}" -f $debugger["runtimeDiagnosticsSource"]))
[void]$builder.AppendLine(("- metric family source: {0}" -f $derived.metricFamilySource))
[void]$builder.AppendLine(("- scorecard metric mask: {0}" -f $derived.scorecardMetricFamilyMask))
[void]$builder.AppendLine(("- scorecard core facts / active mutations: {0:n0} / {1:n0}" -f $derived.scorecardCoreFacts, $derived.scorecardActiveMutationCommands))
[void]$builder.AppendLine(("- scorecard owner-local fact max range: {0:n0}" -f $derived.scorecardOwnerLocalFactMaxOwnerRange))
[void]$builder.AppendLine(("- events/warnings/errors: {0:n0}/{1:n0}/{2:n0}" -f (Get-MapNumber $debugger "events"), (Get-MapNumber $debugger "warnings"), (Get-MapNumber $debugger "errors")))
[void]$builder.AppendLine(("- observation materialization: queries={0:n0}, entities={1:n0}, us={2:n0}" -f $derived.diagnosticObservationQueries, $derived.diagnosticObservationEntities, $derived.diagnosticObservationMicroseconds))
[void]$builder.AppendLine(("- execution spec scan ratio: {0:n2}:1" -f $derived.executionSpecScansPerMatchedEffect))
[void]$builder.AppendLine(("- owner local fact flushes/tick: {0:n1}" -f $derived.ownerLocalFactFlushesPerTick))
[void]$builder.AppendLine()
[void]$builder.AppendLine("## Top Systems")
[void]$builder.AppendLine()
[void]$builder.AppendLine("| Operation | System | Count | Per measured tick |")
[void]$builder.AppendLine("|---|---|---:|---:|")
foreach ($entry in ($systemTopN | Select-Object -First 12)) {
    [void]$builder.AppendLine(("| {0} | {1} | {2:n0} | {3:n1} |" -f $entry.operation, $entry.target, $entry.count, ($entry.count / $measuredTicks)))
}

[void]$builder.AppendLine()
[void]$builder.AppendLine("## Top Components")
[void]$builder.AppendLine()
[void]$builder.AppendLine("| Operation | Component | Count | Per measured tick |")
[void]$builder.AppendLine("|---|---|---:|---:|")
foreach ($entry in ($componentTopN | Select-Object -First 12)) {
    [void]$builder.AppendLine(("| {0} | {1} | {2:n0} | {3:n1} |" -f $entry.operation, $entry.target, $entry.count, ($entry.count / $measuredTicks)))
}

[void]$builder.AppendLine()
[void]$builder.AppendLine("## Reverse-Inferred Architecture Mistakes")
[void]$builder.AppendLine()
foreach ($finding in $findings) {
    [void]$builder.AppendLine(("### {0} ({1})" -f $finding.id, $finding.severity))
    [void]$builder.AppendLine()
    [void]$builder.AppendLine(("- evidence: {0}" -f $finding.evidence))
    [void]$builder.AppendLine(("- inference: {0}" -f $finding.inference))
    [void]$builder.AppendLine(("- design mistake: {0}" -f $finding.designMistake))
    [void]$builder.AppendLine(("- next probe: {0}" -f $finding.nextProbe))
    [void]$builder.AppendLine()
}

$markdown = $builder.ToString()
[System.IO.File]::WriteAllText($markdownPath, $markdown, [System.Text.UTF8Encoding]::new($false))

Write-Host "AutoChess profile analysis written:"
Write-Host "JSON: $jsonPath"
Write-Host "Markdown: $markdownPath"

if ($PrintMarkdown) {
    Write-Host ""
    Write-Host $markdown
}
