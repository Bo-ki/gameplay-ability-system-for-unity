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

function ConvertTo-TimingSnapshot {
    param([string]$Body)

    $snapshot = [ordered]@{
        ecsRuntimeTickOnly = $null
        systems = [ordered]@{}
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
    if ($line -match '^([^:]+):\s*(.*)$') {
        $sections[$Matches[1]] = $Matches[2]
    }
}

$performance = ConvertTo-KeyValueMap $sections["HeadlessAutoChessPlayModeRunnerPerformance"]
$timing = ConvertTo-TimingSnapshot $sections["HeadlessAutoChessPlayModeTiming"]
$debugger = ConvertTo-KeyValueMap $sections["HeadlessAutoChessPlayModeDebugger"]
$official = ConvertTo-KeyValueMap $sections["HeadlessAutoChessPlayModeOfficialToolDiff"]
$profiler = ConvertTo-KeyValueMap $sections["HeadlessAutoChessPlayModeProfiler"]
$diagnostic = ConvertTo-KeyValueMap $sections["HeadlessAutoChessPlayModeDiagnosticRunner"]

$recordTopN = ConvertTo-TopNEntries $official["journalingRecordTopN"] "Record"
$systemTopN = ConvertTo-TopNEntries $official["journalingSystemTopN"] "System"
$componentTopN = ConvertTo-TopNEntries $official["journalingComponentTopN"] "Component"

$measuredTicks = [math]::Max(1, (Get-MapNumber $performance "measuredTicks"))
$diagnosticTicks = [math]::Max(1, (Get-MapNumber $diagnostic "measuredTicks"))
$totalAvg = Get-TimingAvg $timing "GASTickTotal"
$commandAvg = Get-TimingAvg $timing "GASCommandResolveSystemGroup"
$coreAvg = Get-TimingAvg $timing "GASCoreSimulationSystemGroup"
$structuralAvg = Get-TimingAvg $timing "GASStructuralCommitSystemGroup"
$boundaryAvg = Get-TimingAvg $timing "GASBoundaryProjectionSystemGroup"
$prepareAvg = Get-TimingAvg $timing "GASFramePrepareSystemGroup"

$getComponentRw = Get-MapNumber $official "journalingGetComponentDataRW"
$getBufferRw = Get-MapNumber $official "journalingGetBufferRW"
$enableComponents = Get-MapNumber $official "journalingEnableComponents"
$disableComponents = Get-MapNumber $official "journalingDisableComponents"
$worldRecords = Get-MapNumber $official "journalingWorldRecords"
$eventCount = Get-MapNumber $debugger "events"
$droppedEvents = Get-MapNumber $debugger "dropped"
$profilerFirstFrame = Get-MapNumber $profiler "firstFrameIndex" -1
$profilerLastFrame = Get-MapNumber $profiler "lastFrameIndex" -1

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
    diagnosticRequestsPerTick = (Get-MapNumber $debugger "requests") / $diagnosticTicks
    diagnosticFactsPerTick = (Get-MapNumber $debugger "facts") / $diagnosticTicks
    diagnosticPresentationPerTick = (Get-MapNumber $debugger "presentation") / $diagnosticTicks
    debuggerDropRatePct = if (($eventCount + $droppedEvents) -gt 0) { 100.0 * $droppedEvents / ($eventCount + $droppedEvents) } else { 0 }
    profilerDriverFrameCount = if ($profilerLastFrame -ge $profilerFirstFrame -and $profilerFirstFrame -ge 0) { $profilerLastFrame - $profilerFirstFrame + 1 } else { 0 }
    journalingReachedDefaultCap = ($worldRecords -ge 524288)
}

$findings = @()

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

if ($componentTopN | Where-Object { $_.target -eq "GAS.Runtime.AttributeValueBuffer" -and $_.count -gt 50000 }) {
    $findings += New-Finding `
        "GAS-ARCH-04" `
        "High" `
        "AttributeValueBuffer is the largest component TopN RW target." `
        "Attribute recalculation still scans or rewrites broad owner buffers instead of being driven by a narrow dirty attribute set." `
        "Attribute store lacks a deep owner-local delta/recalculate module with a small external interface." `
        "Emit dirty owner count, dirty attribute count, and unchanged-buffer skip count; then move recalculation to dirty chunks only."
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
[void]$builder.AppendLine()
[void]$builder.AppendLine("## Journaling Rates")
[void]$builder.AppendLine()
[void]$builder.AppendLine(("- GetComponentDataRW/tick: {0:n1}" -f $derived.getComponentDataRwPerTick))
[void]$builder.AppendLine(("- GetBufferRW/tick: {0:n1}" -f $derived.getBufferRwPerTick))
[void]$builder.AppendLine(("- Enableable toggles/tick: {0:n1}" -f $derived.enableableTogglePerTick))
[void]$builder.AppendLine(("- Journaling cap reached: {0}" -f $derived.journalingReachedDefaultCap))
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
