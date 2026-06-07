param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

$runtimePath = Join-Path $ProjectPath "Assets\GAS\Runtime"
if (-not (Test-Path -LiteralPath $runtimePath)) {
    throw "GAS Runtime path not found: $runtimePath"
}

$pattern = "AutoChess"
$hits = @()
$rg = Get-Command rg -ErrorAction SilentlyContinue
if ($rg) {
    $rgOutput = & rg -n $pattern $runtimePath -g "*.cs" 2>$null
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) {
        $hits = @($rgOutput)
    } elseif ($exitCode -ne 1) {
        throw "rg failed while scanning GAS Runtime boundary. ExitCode=$exitCode"
    }
} else {
    $hits = Get-ChildItem -LiteralPath $runtimePath -Recurse -Filter "*.cs" |
        Select-String -Pattern $pattern |
        ForEach-Object {
            "{0}:{1}:{2}" -f $_.Path, $_.LineNumber, $_.Line.Trim()
        }
}

if ($hits.Count -gt 0) {
    Write-Error (
        "GAS Runtime Core boundary violation: Assets/GAS/Runtime must not reference AutoChess business names.`n" +
        ($hits -join "`n"))
    exit 1
}

function Assert-FileContains {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required file not found: $Path"
    }

    $content = Get-Content -LiteralPath $Path -Raw
    if ($content -notmatch $Pattern) {
        throw $Message
    }
}

function Assert-FileNotContains {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required file not found: $Path"
    }

    $content = Get-Content -LiteralPath $Path -Raw
    if ($content -match $Pattern) {
        throw $Message
    }
}

$deltaApplyPath = Join-Path $runtimePath "System\Attribute\GASAttributeModifierDeltaApplySystem.cs"
$ascArchetypePath = Join-Path $runtimePath "System\SystemGroup\GASRuntimeEntityArchetypes.cs"
$streamPath = Join-Path $runtimePath "Effect\Component\Dynamic\GEEffectCommandSpecStream.cs"
$streamPhasePath = Join-Path $runtimePath "System\Effect\GEEffectCommandSpecStreamPhases.cs"
$gameplayEffectRequestWriterPath = Join-Path $runtimePath "System\Effect\GameplayEffectRequestWriter.cs"
$abilityRuntimeActionsPath = Join-Path $runtimePath "Ability\AbilityRuntimeActions.cs"
$executionCalculationRuntimeActionsPath = Join-Path $runtimePath "Effect\ExecutionCalculationRuntimeActions.cs"
$effectMagnitudeResolverPath = Join-Path $runtimePath "System\Effect\EffectMagnitudeResolver.cs"
$effectRuntimeUtilityPath = Join-Path $runtimePath "System\Effect\EffectRuntimeUtility.cs"
$executionCalculationSystemPath = Join-Path $runtimePath "System\Effect\GEExecutionCalculationSystem.cs"
$diagnosticsSnapshotSystemPath = Join-Path $runtimePath "System\Event\DiagnosticsSnapshotSystem.cs"
$scheduleContractPath = Join-Path $runtimePath "System\SystemGroup\GASSystemScheduleContract.cs"
$streamOwnerContractPath = Join-Path $runtimePath "System\SystemGroup\GASRuntimeStreamOwnerContract.cs"
$globalTimerPath = Join-Path $runtimePath "System\Core\GASGlobalTimerSystem.cs"
$activeEffectStorePath = Join-Path $runtimePath "Effect\Component\Dynamic\ActiveEffectStore.cs"
$gasManagerPath = Join-Path $runtimePath "General\GASManager.cs"
$debuggerPath = Join-Path $runtimePath "Debugger\GasRuntimeDebugger.cs"
$generatedActiveEffectPath = Join-Path $ProjectPath "Assets\GAS\Generated\CodeGen\Runtime\RuntimeActiveEffect.gen.cs"
$activeEffectLifecycleOwnerPath = Join-Path $ProjectPath "Assets\GAS\Generated\CodeGen\Runtime\ActiveEffectLifecycleOwnerSystems.cs"
$generatedInstantEffectPath = Join-Path $ProjectPath "Assets\GAS\Generated\CodeGen\Runtime\RuntimeEffectInstant.gen.cs"
$codeGenTemplatePath = Join-Path $ProjectPath "Assets\GAS\Editor\CodeGen\Phases\GasGlueCodeGenPhases.cs"
$codeGenManifestPath = Join-Path $ProjectPath "Assets\GAS\Generated\CodeGen\GasCodeGen.manifest.json"
$codeGenReportPath = Join-Path $ProjectPath "Assets\GAS\Generated\CodeGen\GasCodeGenValidationReport.md"
$autoChessDamagePath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\Ecs\AutoChessExecuteDamageCalculationSystem.cs"
$autoChessSessionPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\AutoChessBattleSession.cs"
$autoChessResultBuilderPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\AutoChessBattleResultBuilder.cs"
$autoChessCoreBridgePath = Join-Path $ProjectPath "Assets\AutoChessDemo\Integration\GasCore\AutoChessGasCoreBridge.cs"
$autoChessLifecyclePath = Join-Path $ProjectPath "Assets\AutoChessDemo\Integration\GasCore\AutoChessGasBattleEntityLifecycle.cs"
$autoChessUnitSnapshotProjectorPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Integration\GasCore\AutoChessGasBattleUnitSnapshotProjector.cs"
$autoChessValidationReportPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\Validation\AutoChessBattleValidationReport.cs"
$autoChessValidationRunPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\Validation\AutoChessBattleValidationRun.cs"

Assert-FileContains `
    -Path $deltaApplyPath `
    -Pattern "ApplyOwnerLocalPendingAttributeModifierDeltaChunkJob\s*:\s*IJobChunk" `
    -Message "GASAttributeModifierDeltaApplySystem must use chunk-local pending attribute delta apply as the primary path."
Assert-FileNotContains `
    -Path $deltaApplyPath `
    -Pattern "_ownerDeltaQuery\.ToEntityArray" `
    -Message "Owner-local pending attribute delta apply must not materialize owner entities before applying."
Assert-FileNotContains `
    -Path $deltaApplyPath `
    -Pattern "ApplyOwnerLocalPendingAttributeModifierDeltaJob\s*:\s*IJob" `
    -Message "Owner-local pending attribute delta apply must not regress to serial BufferLookup owner apply."
Assert-FileNotContains `
    -Path $deltaApplyPath `
    -Pattern "estimatedRandomLookupCount\+\+" `
    -Message "Chunk-local pending attribute delta apply must not count owner chunk writes as random lookups."
Assert-FileContains `
    -Path $deltaApplyPath `
    -Pattern "estimatedRandomLookupCount:\s*0" `
    -Message "Owner-local pending attribute delta apply must keep chunk-local random lookup pressure at zero."
Assert-FileNotContains `
    -Path $deltaApplyPath `
    -Pattern "migrationCarrierCount" `
    -Message "Pending AttributeDelta core apply must not expose a stream migration carrier path."
Assert-FileNotContains `
    -Path $deltaApplyPath `
    -Pattern "ApplyStreamMigrationPendingAttributeModifierDeltaJob" `
    -Message "GASAttributeModifierDeltaApplySystem must not reintroduce the legacy stream migration fallback."
Assert-FileNotContains `
    -Path $deltaApplyPath `
    -Pattern "PendingDeltaApplyRecord" `
    -Message "GASAttributeModifierDeltaApplySystem must not rebuild stream fallback sort records."
Assert-FileNotContains `
    -Path $deltaApplyPath `
    -Pattern "DeltaLookup\s*=\s*SystemAPI\.GetBufferLookup<AttributeModifierBuffer>" `
    -Message "Pending AttributeDelta core apply must not scan the singleton stream AttributeModifierBuffer fallback."
Assert-FileContains `
    -Path $ascArchetypePath `
    -Pattern "PendingAttributeModifierComponent" `
    -Message "ASC runtime archetype must own the pending attribute modifier marker."
Assert-FileContains `
    -Path $ascArchetypePath `
    -Pattern "ComponentType\.ReadWrite<AttributeModifierBuffer>\(\)" `
    -Message "ASC runtime archetype must own an owner-local AttributeModifierBuffer."
Assert-FileContains `
    -Path $deltaApplyPath `
    -Pattern "PendingAttributeTargetGroupCount" `
    -Message "GASAttributeModifierDeltaApplySystem must expose target group counters."
Assert-FileContains `
    -Path $deltaApplyPath `
    -Pattern "PendingAttributeEstimatedRandomLookupCount" `
    -Message "GASAttributeModifierDeltaApplySystem must expose random lookup pressure counters."
Assert-FileNotContains `
    -Path $globalTimerPath `
    -Pattern "ToEntityArray" `
    -Message "GASRuntimeFrameContext must not materialize GlobalTimer singleton fallback entities."
Assert-FileContains `
    -Path $globalTimerPath `
    -Pattern "TryResolveRegisteredGlobalTimer" `
    -Message "GASRuntimeFrameContext must prefer the registered GlobalTimer owner."
Assert-FileNotContains `
    -Path $globalTimerPath `
    -Pattern "CreateEntityQuery" `
    -Message "GASRuntimeFrameContext current-frame lookup must not create singleton fallback queries."
Assert-FileNotContains `
    -Path $globalTimerPath `
    -Pattern "TryResolveSingletonGlobalTimer" `
    -Message "GASRuntimeFrameContext must not keep singleton query fallback helpers."
Assert-FileNotContains `
    -Path $activeEffectStorePath `
    -Pattern "CreateEntityQuery" `
    -Message "ActiveEffectStore global index store lookup must not create singleton fallback queries."
Assert-FileContains `
    -Path $activeEffectStorePath `
    -Pattern "TryResolveRegisteredGlobalIndexStore" `
    -Message "ActiveEffectStore must prefer the registered global index store owner."
Assert-FileContains `
    -Path $activeEffectStorePath `
    -Pattern "RegisterKnownGlobalIndexStore" `
    -Message "ActiveEffectStore must expose a registered owner cache for the global index store."
Assert-FileContains `
    -Path $gasManagerPath `
    -Pattern "ActiveEffectStore\.ResetKnownGlobalIndexStore\(EntityManager\)" `
    -Message "GASManager shutdown must reset the ActiveEffectStore global index owner cache."
Assert-FileNotContains `
    -Path $debuggerPath `
    -Pattern "CreateEntityQuery" `
    -Message "GasRuntimeDebugger must not create singleton fallback queries."
Assert-FileNotContains `
    -Path $debuggerPath `
    -Pattern "TryResolveSingletonByQuery" `
    -Message "GasRuntimeDebugger must not keep singleton query fallback helpers."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "TryResolveRegisteredSingleton" `
    -Message "GasRuntimeDebugger must prefer the registered runtime debugger owner before failing."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "GASManager\.EntityRuntimeDebugger" `
    -Message "GasRuntimeDebugger registered lookup must consume the GASManager-owned runtime debugger entity."
Assert-FileContains `
    -Path $gasManagerPath `
    -Pattern "GasRuntimeDebugger\.ResetKnownSingleton\(EntityManager\)" `
    -Message "GASManager shutdown must reset the runtime debugger owner cache."
Assert-FileContains `
    -Path $gasManagerPath `
    -Pattern "EntityRuntimeDebugger = GasRuntimeDebugger\.CreateSingleton\(ExWorld\.EntityManager\)" `
    -Message "GASManager initialization must register the runtime debugger owner."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "GasRuntimeObservationMaterializationCounters" `
    -Message "GasRuntimeDebugger must expose observation materialization counters outside Runtime Core counters."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "ObservationMaterialization" `
    -Message "GasRuntimeDebugger must emit a dedicated observation materialization diagnostic event."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "runtimeObservationMaterialization" `
    -Message "GasRuntimeDebugger text export must expose observation materialization counters."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "observationMaterializationCounters\s*=\s*observationMaterializationCounters\.Add" `
    -Message "GasRuntimeDebugger snapshots must aggregate observation materialization events into machine-readable counters."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "PerformancePollutionRiskCount" `
    -Message "GasRuntimeDebugger must tag observation materialization as performance-pass pollution risk."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "GasRuntimeMagnitudeSourceCounters" `
    -Message "GasRuntimeDebugger must expose magnitude source counters outside Runtime Core counters."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "MagnitudeSource\s*=\s*8" `
    -Message "GasRuntimeDebugger must emit a dedicated magnitude source diagnostic event."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "runtimeMagnitudeSource" `
    -Message "GasRuntimeDebugger text export must expose magnitude source counters."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "ActiveEffectSlotSourceSnapshotGatherAttemptCount" `
    -Message "Effect command stream must expose active effect slot source snapshot lane counters."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "activeEffectSlotSourceSnapshotWriteFailures" `
    -Message "GasRuntimeDebugger text export must expose active effect slot source snapshot write failure evidence."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "activeEffectSlotSourceSnapshotCapacityPressure" `
    -Message "GasRuntimeDebugger text export must expose active effect slot source snapshot capacity pressure evidence."
Assert-FileContains `
    -Path $generatedInstantEffectPath `
    -Pattern "TagMaskLookup\s*=\s*SystemAPI\.GetComponentLookup<TagMaskComponent>\(isReadOnly:\s*true\)" `
    -Message "Generated instant GE spec build must read target tag masks for GameplayEffect tag requirement evaluation."
Assert-FileContains `
    -Path $generatedInstantEffectPath `
    -Pattern "GASGeneratedRequirementEvaluator\.EvaluateGameplayEffectRequirements\(ref catalog, in gameplayEffect, in targetTags, out _\)" `
    -Message "Generated instant GE spec build must evaluate GameplayEffect tag requirements before creating specs."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "TagMaskLookup\s*=\s*SystemAPI\.GetComponentLookup<TagMaskComponent>\(isReadOnly:\s*true\)" `
    -Message "CodeGen template must keep instant GE target tag mask lookup."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "GASGeneratedRequirementEvaluator\.EvaluateGameplayEffectRequirements\(ref catalog, in gameplayEffect, in targetTags, out _\)" `
    -Message "CodeGen template must keep instant GE tag requirement evaluation."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "runtimeCoreMagnitudeSource" `
    -Message "GasRuntimeDebugger RuntimeCore export must expose magnitude source owner counters."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "RecordMagnitudeSourceEvidence" `
    -Message "GasRuntimeDebugger must materialize magnitude source stream counters into diagnostic evidence."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "magnitudeSourceCounters\s*=\s*magnitudeSourceCounters\.Add" `
    -Message "GasRuntimeDebugger snapshots must aggregate magnitude source events into machine-readable counters."
Assert-FileContains `
    -Path $diagnosticsSnapshotSystemPath `
    -Pattern "RecordMagnitudeSourceEvidence" `
    -Message "DiagnosticsSnapshotSystem must sample magnitude source debugger evidence."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "diagnosticResult\.RuntimeDiagnostics\.ObservationMaterializationCounters" `
    -Message "AutoChess validation evidence must consume observation materialization counters from the diagnostic pass."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "performancePassObservationPollutionRisks" `
    -Message "AutoChess validation evidence must expose observation materialization pollution risk."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "observationMaterializedQueries" `
    -Message "AutoChess validation evidence must expose observation materialized query count."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "diagnosticResult\.RuntimeDiagnostics\.MagnitudeSourceCounters" `
    -Message "AutoChess validation evidence must consume magnitude source counters from the diagnostic pass."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "magnitudeSourceCaptureMissLiveLookups" `
    -Message "AutoChess validation evidence must expose magnitude source capture miss live lookup count."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "magnitudeSourceFallbackFacts" `
    -Message "AutoChess validation evidence must expose magnitude source fallback fact count."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "diagnosticResult\.RuntimeDiagnostics\.ObservationMaterializationCounters" `
    -Message "AutoChess validation evidence must read observation materialization detail from the diagnostic pass."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "performanceResult\.RuntimeDiagnostics\.ObservationMaterializationCounters" `
    -Message "AutoChess validation evidence must read performance pollution risk from the performance pass."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "performanceObservation\.PerformancePollutionRiskCount" `
    -Message "AutoChess validation evidence must not publish diagnostic observation materialization as performance pollution."
Assert-FileContains `
    -Path $autoChessValidationRunPath `
    -Pattern "var performanceResult = RunGeneratedScenario\([\s\S]*?debuggerEnabled:\s*false[\s\S]*?captureSystemTimings:\s*false[\s\S]*?captureBufferPressure:\s*false" `
    -Message "AutoChess headless performance pass must run without debugger observation materialization."
Assert-FileContains `
    -Path $autoChessValidationRunPath `
    -Pattern "var diagnosticResult = RunGeneratedScenario\([\s\S]*?debuggerEnabled:\s*true[\s\S]*?captureSystemTimings:\s*true[\s\S]*?captureBufferPressure:\s*true" `
    -Message "AutoChess headless diagnostic pass must be the dedicated runtime diagnostics owner."
Assert-FileContains `
    -Path $autoChessValidationRunPath `
    -Pattern "var officialDiffResult = RunGeneratedScenario\([\s\S]*?debuggerEnabled:\s*false[\s\S]*?captureSystemTimings:\s*false[\s\S]*?captureBufferPressure:\s*false" `
    -Message "AutoChess official diff replay must stay out of performance diagnostics sampling."
Assert-FileContains `
    -Path $autoChessValidationRunPath `
    -Pattern "RunWarmupPass\(CreateGeneratedScenarioOptions\([\s\S]*?debuggerEnabled:\s*false[\s\S]*?captureSystemTimings:\s*false[\s\S]*?captureBufferPressure:\s*false" `
    -Message "AutoChess process warmup pass must not enable debugger observation materialization."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "ResetFrameLocalCounters\(ref stream\)" `
    -Message "GEEffectCommandSpecStream must reset frame-local debugger counters during frame prepare."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "AddMagnitudeSourceCounters" `
    -Message "GEEffectCommandSpecStream must expose frame-local magnitude source counter accumulation."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "MagnitudeSourceCaptureMissCount" `
    -Message "GEEffectCommandSpecStream must own magnitude source capture miss evidence."
Assert-FileContains `
    -Path $effectMagnitudeResolverPath `
    -Pattern "RecordMagnitudeSourceAttributeResolution" `
    -Message "EffectMagnitudeResolver must record magnitude source attribute resolution evidence."
Assert-FileContains `
    -Path $effectMagnitudeResolverPath `
    -Pattern "sourceAttributeLookups:\s*source == EMagnitudeSource\.SourceAttribute" `
    -Message "EffectMagnitudeResolver must classify source attribute magnitude lookups."
Assert-FileContains `
    -Path $effectMagnitudeResolverPath `
    -Pattern "targetAttributeLookups:\s*source == EMagnitudeSource\.TargetAttribute" `
    -Message "EffectMagnitudeResolver must classify target attribute magnitude lookups."
Assert-FileNotContains `
    -Path $streamPath `
    -Pattern "BeginCommandWriter\(EntityManager em\)|BeginCommandWriter\(EntityManager em, int currentFrame\)" `
    -Message "GEEffectCommandSpecStream must not expose implicit singleton command writer helpers."
Assert-FileNotContains `
    -Path $streamPath `
    -Pattern "BeginGameplayEventWriter\(EntityManager em\)" `
    -Message "GEEffectCommandSpecStream must not expose implicit singleton gameplay event writer helpers."
Assert-FileNotContains `
    -Path $streamPath `
    -Pattern "AppendCommand\(EntityManager em|AppendGameplayEvent\(EntityManager em" `
    -Message "GEEffectCommandSpecStream must not expose implicit singleton append helpers."
Assert-FileNotContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "BeginCommandWriter\(em\)|AppendCommand\(em," `
    -Message "GameplayEffectRequestWriter must resolve stream owner explicitly before writing commands."
Assert-FileNotContains `
    -Path $abilityRuntimeActionsPath `
    -Pattern "AppendGameplayEvent\(entityManager" `
    -Message "AbilityRuntimeActions must resolve stream owner explicitly before writing facts."
Assert-FileNotContains `
    -Path $executionCalculationRuntimeActionsPath `
    -Pattern "BeginGameplayEventWriter\(em\)" `
    -Message "ExecutionCalculationRuntimeActions must resolve stream owner explicitly before writing facts."
Assert-FileNotContains `
    -Path $effectMagnitudeResolverPath `
    -Pattern "BeginGameplayEventWriter\(em\)" `
    -Message "EffectMagnitudeResolver must resolve stream owner explicitly before writing facts."
Assert-FileNotContains `
    -Path $effectRuntimeUtilityPath `
    -Pattern "BeginGameplayEventWriter\(em\)" `
    -Message "EffectRuntimeUtility must resolve stream owner explicitly before writing facts."
Assert-FileContains `
    -Path $executionCalculationSystemPath `
    -Pattern "FactWriter\s*=\s*factStream\.AsWriter\(\)" `
    -Message "GEExecutionCalculationSystem must collect execution output facts through a NativeStream writer."
Assert-FileContains `
    -Path $executionCalculationSystemPath `
    -Pattern "GEExecutionCalculationFactMergeJob\s*:\s*IJob" `
    -Message "GEExecutionCalculationSystem must merge execution output facts through a deterministic merge job."
Assert-FileContains `
    -Path $executionCalculationSystemPath `
    -Pattern "PendingFacts\.Sort\(new PendingExecutionOutputFactRecordComparer\(\)\)" `
    -Message "GEExecutionCalculationSystem execution output facts must be stable-sorted before entering the typed fact buffer."
Assert-FileContains `
    -Path $executionCalculationSystemPath `
    -Pattern "EffectCommandSpecStreamPhaseUtility\.Allocate\(ref stream\.NextFactSequence\)" `
    -Message "GEExecutionCalculationSystem merge job must allocate deterministic fact sequences."
Assert-FileContains `
    -Path $executionCalculationSystemPath `
    -Pattern "ExecutionMagnitudeSourceChunkCounters" `
    -Message "GEExecutionCalculationSystem must keep magnitude source evidence chunk-local before deterministic merge."
Assert-FileContains `
    -Path $executionCalculationSystemPath `
    -Pattern "MagnitudeSourceChunkCounters" `
    -Message "GEExecutionCalculationSystem must merge magnitude source evidence through the execution fact merge job."
Assert-FileNotContains `
    -Path $executionCalculationSystemPath `
    -Pattern "EndGASStructuralCommitECBSystem|FactEcb|EntityCommandBuffer|AppendToBuffer" `
    -Message "GEExecutionCalculationSystem must not write execution output facts through structural ECB append to the singleton stream."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "UpdateAfter\(typeof\(GASAttributeModifierDeltaApplySystem\)\)" `
    -Message "GameplayFactProjectionSystem must run after core pending attribute delta apply."
Assert-FileContains `
    -Path $scheduleContractPath `
    -Pattern "typeof\(GASAttributeModifierDeltaApplySystem\),\s*[\r\n\s]*EGasRuntimeCoreFramePhase\.DeltaApply" `
    -Message "GASSystemScheduleContract must classify GASAttributeModifierDeltaApplySystem as DeltaApply."
Assert-FileNotContains `
    -Path $scheduleContractPath `
    -Pattern "if\s*\(systemType\s*==\s*null\)\s*[\r\n\s]*continue" `
    -Message "GASSystemScheduleContract must not silently skip missing generated runtime systems."
Assert-FileContains `
    -Path $scheduleContractPath `
    -Pattern "Generated GAS runtime system type is missing" `
    -Message "GASSystemScheduleContract must fail fast when generated runtime system types are missing."
Assert-FileContains `
    -Path $streamOwnerContractPath `
    -Pattern "streamId == EGasRuntimeFrameStreamId\.AttributeDelta" `
    -Message "AttributeDelta stream owner plan must require owner-range/random-lookup evidence."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "RuntimeCoreActiveMutationCommandCount \+= activeMutationCommandCount" `
    -Message "GasRuntimeDebugger snapshot counters must accumulate active mutation frame-local evidence."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "GEEffectCommandCatalogNormalizeSystem\s*:\s*ISystem[\s\S]*?GASActiveEffectMutationApplySystem\s*:\s*ISystem" `
    -Message "Active effect lifecycle systems must live in the dedicated owner artifact, not RuntimeActiveEffect.gen.cs."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "GEEffectCommandCatalogNormalizeSystem\s*:\s*ISystem[\s\S]*?GASActiveEffectRemoveSystem\s*:\s*ISystem" `
    -Message "Dedicated active effect lifecycle owner file must carry normalize/apply/pre-tick/remove system wrappers."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "(GEEffectCommandCatalogNormalizeSystem|GASActiveEffectMutationApplySystem|GASActiveEffectPreTickSystem|GASActiveEffectRemoveSystem)\s*:\s*ISystem" `
    -Message "RuntimeActiveEffect.gen.cs must not regenerate active-effect lifecycle ISystem wrappers."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "Runtime/ActiveEffectLifecycleOwnerSystems\.cs" `
    -Message "RuntimeLifecycleMigrationPhase must not output the handwritten active-effect lifecycle owner artifact."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "WriteRuntimeActiveEffectLifecycleOwnerSystems" `
    -Message "CodeGen must not retain a writer that regenerates the active-effect lifecycle owner artifact."
Assert-FileNotContains `
    -Path $codeGenManifestPath `
    -Pattern "ActiveEffectLifecycleOwnerSystems\.cs" `
    -Message "GasCodeGen manifest must not classify the handwritten active-effect lifecycle owner as generated output."
Assert-FileNotContains `
    -Path $codeGenReportPath `
    -Pattern "ActiveEffectLifecycleOwnerSystems\.cs" `
    -Message "GasCodeGen validation report must not count the handwritten active-effect lifecycle owner as generated runtime boundary debt."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectMutationGatherJob" `
    -Message "Generated active effect runtime must gather active mutation commands before chunk-local apply."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectMutationChunkApplyJob\s*:\s*IJobChunk" `
    -Message "Generated active effect runtime must apply active mutations through ASC chunk-local IJobChunk."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "BuildActiveMutationSourceAttributeSnapshots" `
    -Message "Generated active mutation must build a frame-local SourceAttribute snapshot before chunk-local apply."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectMutationGatherJob\s*:\s*IJob[\s\S]*?\[ReadOnly\] public BufferLookup<AttributeValueBuffer> AttributeLookup" `
    -Message "Generated active mutation SourceAttribute capture must happen in the read-only gather/snapshot lane."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectMutationChunkApplyJob\s*:\s*IJobChunk[\s\S]*?\[ReadOnly\] public NativeParallelHashMap<long, float> ActiveMutationSourceAttributeSnapshots" `
    -Message "Generated active mutation chunk apply must consume SourceAttribute snapshots instead of live cross-owner attribute lookup."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "ActiveMutationCommands\.Length == 0" `
    -Message "Generated active mutation chunk apply must skip ASC chunk scans when there are no active mutation commands."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "GEActiveEffectMutationChunkApplyJob\s*:\s*IJobChunk" `
    -Message "CodeGen template must keep active mutation apply on the chunk-local path."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "BuildActiveMutationSourceAttributeSnapshots" `
    -Message "CodeGen template must keep active mutation SourceAttribute capture in the frame-local snapshot lane."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectPreTickSourceAttributeSnapshotGatherJob" `
    -Message "Generated active effect pre-tick must gather SourceAttribute snapshots before slot rebuild."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "ActiveEffectSlotSourceAttributeSnapshots\s*=\s*activeEffectSlotSourceAttributeSnapshots\.AsParallelWriter" `
    -Message "Dedicated active effect pre-tick owner must write SourceAttribute snapshots through a parallel writer."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "ActiveEffectSlotSourceAttributeSnapshotKey\s*:\s*IEquatable<ActiveEffectSlotSourceAttributeSnapshotKey>" `
    -Message "Generated active effect pre-tick snapshot key must include owner identity, not only owner-local slot sequence."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "EstimateActiveEffectSlotSourceAttributeSnapshotCapacity[\s\S]*?maxSourceAttributeModifierCount" `
    -Message "Generated active effect pre-tick snapshot capacity must be driven by catalog SourceAttribute modifier upper bound."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectPreTickJob\s*:\s*IJobChunk[\s\S]*?\[ReadOnly\] public NativeParallelHashMap<ActiveEffectSlotSourceAttributeSnapshotKey, float> ActiveEffectSlotSourceAttributeSnapshots" `
    -Message "Generated active effect pre-tick apply must consume SourceAttribute snapshots instead of live cross-owner attribute lookup."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "MakeActiveEffectSlotSourceAttributeSnapshotKey\(owner,\s*slot\.Sequence,\s*modifierIndex\)" `
    -Message "Generated active effect pre-tick gather must key SourceAttribute snapshots by target owner and owner-local slot sequence."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "ActiveEffectSlotSourceAttributeSnapshots\.TryAdd\(snapshotKey,\s*sourceValue\)" `
    -Message "Generated active effect pre-tick snapshot gather must use bounded snapshot writes; misses are exposed by magnitude-source fallback counters."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "ActiveEffectSlotSourceSnapshotLaneCounters" `
    -Message "Generated active effect pre-tick must expose lane-specific SourceAttribute snapshot counters."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "SnapshotLaneCounters\s*=\s*activeEffectSlotSourceSnapshotLaneCounters" `
    -Message "Dedicated active effect pre-tick owner must pass SourceAttribute snapshot lane counters through gather and apply jobs."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "RecordSnapshotWrite\(\s*ActiveEffectSlotSourceAttributeSnapshots\.TryAdd\(snapshotKey,\s*sourceValue\)\)" `
    -Message "Generated active effect pre-tick gather must record snapshot write success/failure evidence."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "snapshotLaneCounters\.ApplyHitCount\+\+" `
    -Message "Generated active effect pre-tick apply must record source snapshot hit evidence."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "snapshotLaneCounters\.ApplyMissCount\+\+" `
    -Message "Generated active effect pre-tick apply must record source snapshot miss evidence."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "snapshotLaneCounters\.FallbackValueCount\+\+" `
    -Message "Generated active effect pre-tick apply must record source snapshot fallback evidence."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "SetActiveEffectSlotSourceSnapshotCapacity" `
    -Message "Dedicated active effect pre-tick owner must record SourceAttribute snapshot capacity evidence."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "MakeActiveEffectSlotSourceAttributeSnapshotKey\(ownerResources\.Owner,\s*slot\.Sequence,\s*modifierIndex\)" `
    -Message "Generated active effect pre-tick apply must read SourceAttribute snapshots by the same target owner key."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "GEActiveEffectPreTickSourceAttributeSnapshotGatherJob" `
    -Message "CodeGen template must keep active effect pre-tick SourceAttribute snapshot gather."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "ActiveEffectSlotSourceAttributeSnapshots\s*=\s*activeEffectSlotSourceAttributeSnapshots\.AsParallelWriter" `
    -Message "CodeGen template must keep active effect pre-tick SourceAttribute snapshots on a parallel writer."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "ActiveEffectSlotSourceAttributeSnapshotKey\s*:\s*IEquatable<ActiveEffectSlotSourceAttributeSnapshotKey>" `
    -Message "CodeGen template must keep owner-aware active effect pre-tick snapshot keys."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "EstimateActiveEffectSlotSourceAttributeSnapshotCapacity[\s\S]*?maxSourceAttributeModifierCount" `
    -Message "CodeGen template must keep catalog-driven active effect pre-tick snapshot capacity."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "MakeActiveEffectSlotSourceAttributeSnapshotKey\(owner,\s*slot\.Sequence,\s*modifierIndex\)" `
    -Message "CodeGen template must keep pre-tick snapshot gather keyed by target owner."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "ActiveEffectSlotSourceAttributeSnapshots\.TryAdd\(snapshotKey,\s*sourceValue\)" `
    -Message "CodeGen template must keep bounded active effect pre-tick snapshot writes with fallback evidence."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "ActiveEffectSlotSourceSnapshotLaneCounters" `
    -Message "CodeGen template must keep active effect SourceAttribute snapshot lane counters."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "SnapshotLaneCounters\s*=\s*activeEffectSlotSourceSnapshotLaneCounters" `
    -Message "CodeGen template must pass active effect SourceAttribute snapshot lane counters through gather and apply jobs."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "RecordSnapshotWrite\(\s*ActiveEffectSlotSourceAttributeSnapshots\.TryAdd\(snapshotKey,\s*sourceValue\)\)" `
    -Message "CodeGen template must preserve snapshot write success/failure evidence."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "snapshotLaneCounters\.ApplyHitCount\+\+" `
    -Message "CodeGen template must keep source snapshot hit evidence."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "snapshotLaneCounters\.ApplyMissCount\+\+" `
    -Message "CodeGen template must keep source snapshot miss evidence."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "snapshotLaneCounters\.FallbackValueCount\+\+" `
    -Message "CodeGen template must keep source snapshot fallback evidence."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "SetActiveEffectSlotSourceSnapshotCapacity" `
    -Message "CodeGen template must preserve SourceAttribute snapshot capacity evidence."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "ActiveEffectMagnitudeSourceCounters[\s\S]*?CaptureMissCount[\s\S]*?FallbackValueCount" `
    -Message "Generated active effect magnitude snapshot lane must expose capture misses and fallback values."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "ActiveEffectMagnitudeSourceCounters[\s\S]*?CaptureMissCount[\s\S]*?FallbackValueCount" `
    -Message "CodeGen template must keep active effect magnitude snapshot miss and fallback evidence."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "MakeActiveEffectSlotSourceAttributeSnapshotKey\(ownerResources\.Owner,\s*slot\.Sequence,\s*modifierIndex\)" `
    -Message "CodeGen template must keep pre-tick snapshot apply keyed by target owner."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "activeEffectSlotSourceAttributeSnapshotCapacity\s*=\s*ownerCapacity\s*\*" `
    -Message "Generated active effect pre-tick snapshot capacity must not regress to a raw owner-count estimate."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "activeEffectSlotSourceAttributeSnapshotCapacity\s*=\s*ownerCapacity\s*\*" `
    -Message "CodeGen template must not regenerate raw owner-count snapshot capacity."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "MakeActiveEffectSlotSourceAttributeSnapshotKey\(slot\.Sequence,\s*modifierIndex\)" `
    -Message "Generated active effect pre-tick snapshot key must not regress to owner-local slot sequence only."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "MakeActiveEffectSlotSourceAttributeSnapshotKey\(slot\.Sequence,\s*modifierIndex\)" `
    -Message "CodeGen template must not regenerate owner-local-only pre-tick snapshot keys."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectMutationApplyJob\s*:\s*IJob" `
    -Message "Generated active effect runtime must not regress to serial active mutation owner lookup apply."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "TryReadAttributeValue\(ref ownerResources,\s*command\.SourceAsc" `
    -Message "Generated active mutation apply must not read cross-owner SourceAttribute through owner resources directly."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "TryReadAttributeValue\(ref ownerResources,\s*command\.SourceAsc" `
    -Message "CodeGen template must not regenerate direct SourceAttribute owner-resource reads."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "BuildMagnitudeContextFromSlot[\s\S]*?TryReadAttributeValue\(ref ownerResources,\s*slot\.SourceAsc" `
    -Message "Generated active effect pre-tick must not read slot SourceAttribute through live owner-resource lookup."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "BuildMagnitudeContextFromSlot[\s\S]*?TryReadAttributeValue\(ref ownerResources,\s*slot\.SourceAsc" `
    -Message "CodeGen template must not regenerate slot SourceAttribute live owner-resource lookup."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "ActiveMutationOwnerResourceLookupCount \+= ownerGroupCount" `
    -Message "Active mutation owner groups must not be counted as random owner resource lookups on the chunk-local path."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "EstimateActiveMutationRandomLookupCount" `
    -Message "Active mutation chunk-local path must not use the old estimated random lookup budget."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectMutationChunkApplyJob\s*:\s*IJobChunk[\s\S]*?BufferLookup<AttributeValueBuffer> AttributeLookup[\s\S]*?\[ReadOnly\] public NativeList<GEEffectCommandBuffer> ActiveMutationCommands" `
    -Message "Active mutation chunk-local apply must not hold AttributeValueBuffer lookup aliases beside writable chunk buffers."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "RuntimeCorePendingAttributeAppliedDeltaCount \+= pendingAttributeAppliedDeltaCount" `
    -Message "GasRuntimeDebugger snapshot counters must accumulate pending attribute frame-local evidence."
Assert-FileContains `
    -Path $diagnosticsSnapshotSystemPath `
    -Pattern "RecordEffectCommandSpecStreamPressure" `
    -Message "DiagnosticsSnapshotSystem must sample proof-only stream carrier pressure."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "EffectCommandSpecStream" `
    -Message "GasRuntimeDebugger must tag EffectCommandSpecStream carrier pressure as structured evidence."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "EGasRuntimeDiagnosticModule\.Effect" `
    -Message "GasRuntimeDebugger must classify stream carrier pressure under the Effect module, not EventBus."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "streamCarrierPressureWarnings" `
    -Message "AutoChess validation evidence must export stream carrier pressure warnings for R3 scale gates."
Assert-FileContains `
    -Path $autoChessDamagePath `
    -Pattern "PendingOwnerLookup\.SetComponentEnabled\(targetAsc,\s*true\)" `
    -Message "AutoChess execution calculation must publish pending attribute deltas to the target ASC owner-local lane."
Assert-FileNotContains `
    -Path $autoChessDamagePath `
    -Pattern "DeltaLookup\[StreamEntity\]" `
    -Message "AutoChess execution calculation must not write pending attribute deltas to the stream migration carrier."
Assert-FileContains `
    -Path $autoChessUnitSnapshotProjectorPath `
    -Pattern "GasStructuredLogExportSnapshot" `
    -Message "AutoChess unit result snapshots must be projected from structured boundary evidence."
Assert-FileNotContains `
    -Path $autoChessSessionPath `
    -Pattern "ReadCombatAttributes|RefreshUnits|TryCaptureASCReadModel" `
    -Message "AutoChess session must not refresh unit results through live ASC read model."
Assert-FileContains `
    -Path $autoChessResultBuilderPath `
    -Pattern "session\.CreateUnitResults\(coreObservation\.StructuredLog\)" `
    -Message "AutoChess result builder must pass structured boundary evidence into unit result snapshots."
Assert-FileNotContains `
    -Path $autoChessCoreBridgePath `
    -Pattern "ReadCombatAttributes" `
    -Message "AutoChess GasCore bridge must not expose live combat attribute read APIs."
Assert-FileNotContains `
    -Path $autoChessLifecyclePath `
    -Pattern "ReadCombatAttributes|TryCaptureASCReadModel" `
    -Message "AutoChess lifecycle must not capture ASCReadModel for business unit snapshots."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "snapshotOwner=AutoChessGasBattleUnitSnapshotProjector\.StructuredLog" `
    -Message "AutoChess validation evidence must report structured-log snapshot ownership."

Write-Host "GAS Runtime Core boundary check passed: no AutoChess references under Assets/GAS/Runtime."
Write-Host "GAS Runtime Core pending attribute delta contract passed: owner-local apply, stream migration fallback retired, and debugger counters are wired."
Write-Host "GAS Runtime Core global timer contract passed: registered/cache owner lookup is wired and singleton fallback queries are blocked."
Write-Host "GAS Runtime Core active effect global index contract passed: registered/cache owner lookup is wired and singleton fallback queries are blocked."
Write-Host "GAS Runtime Core debugger singleton contract passed: registered/cache owner lookup is wired and singleton fallback queries are blocked."
Write-Host "GAS Runtime Core stream writer contract passed: runtime helpers resolve stream owners explicitly before writing commands or facts."
Write-Host "GAS Runtime Core execution output fact contract passed: NativeStream collection and deterministic merge replaced structural ECB singleton append."
Write-Host "GAS Runtime Core active mutation contract passed: generated gather + ASC chunk-local apply path is wired."
Write-Host "GAS Runtime Core active mutation SourceAttribute contract passed: read-only snapshot lane feeds chunk-local apply."
Write-Host "AutoChess R1/R6 snapshot contract passed: unit result snapshots are projected from structured boundary evidence, not live ASCReadModel."
