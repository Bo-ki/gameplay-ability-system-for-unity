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
$ascFactoryPath = Join-Path $runtimePath "AbilitySystem\ASCEntityFactory.cs"
$streamPath = Join-Path $runtimePath "Effect\Component\Dynamic\GEEffectCommandSpecStream.cs"
$streamPhasePath = Join-Path $runtimePath "System\Effect\GEEffectCommandSpecStreamPhases.cs"
$gameplayEffectRequestWriterPath = Join-Path $runtimePath "System\Effect\GameplayEffectRequestWriter.cs"
$abilityRuntimeActionsPath = Join-Path $runtimePath "Ability\AbilityRuntimeActions.cs"
$executionCalculationRuntimeActionsPath = Join-Path $runtimePath "Effect\ExecutionCalculationRuntimeActions.cs"
$effectMagnitudeResolverPath = Join-Path $runtimePath "System\Effect\EffectMagnitudeResolver.cs"
$effectRuntimeUtilityPath = Join-Path $runtimePath "System\Effect\EffectRuntimeUtility.cs"
$executionCalculationSystemPath = Join-Path $runtimePath "System\Effect\GEExecutionCalculationSystem.cs"
$executionCalculationOutputModifierSystemPath = Join-Path $runtimePath "System\Effect\GEExecutionCalculationOutputModifierSystem.cs"
$diagnosticsSnapshotSystemPath = Join-Path $runtimePath "System\Event\DiagnosticsSnapshotSystem.cs"
$scheduleContractPath = Join-Path $runtimePath "System\SystemGroup\GASSystemScheduleContract.cs"
$streamOwnerContractPath = Join-Path $runtimePath "System\SystemGroup\GASRuntimeStreamOwnerContract.cs"
$queryLayoutPlanPath = Join-Path $runtimePath "System\SystemGroup\GASRuntimeQueryLayoutPlan.cs"
$globalTimerPath = Join-Path $runtimePath "System\Core\GASGlobalTimerSystem.cs"
$activeEffectStorePath = Join-Path $runtimePath "Effect\Component\Dynamic\ActiveEffectStore.cs"
$gasManagerPath = Join-Path $runtimePath "General\GASManager.cs"
$debuggerPath = Join-Path $runtimePath "Debugger\GasRuntimeDebugger.cs"
$generatedActiveEffectPath = Join-Path $ProjectPath "Assets\GAS\Generated\CodeGen\Runtime\RuntimeActiveEffect.gen.cs"
$activeEffectLifecycleOwnerPath = Join-Path $ProjectPath "Assets\GAS\Generated\CodeGen\Runtime\ActiveEffectLifecycleOwnerSystems.cs"
$generatedInstantEffectPath = Join-Path $ProjectPath "Assets\GAS\Generated\CodeGen\Runtime\RuntimeEffectInstant.gen.cs"
$generatedAbilityActivationPath = Join-Path $ProjectPath "Assets\GAS\Generated\CodeGen\Runtime\RuntimeAbilityActivation.gen.cs"
$codeGenTemplatePath = Join-Path $ProjectPath "Assets\GAS\Editor\CodeGen\Phases\GasGlueCodeGenPhases.cs"
$codeGenManifestPath = Join-Path $ProjectPath "Assets\GAS\Generated\CodeGen\GasCodeGen.manifest.json"
$codeGenReportPath = Join-Path $ProjectPath "Assets\GAS\Generated\CodeGen\GasCodeGenValidationReport.md"
$autoChessDriverPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\Ecs\AutoChessBattleDriverComponents.cs"
$autoChessDamagePath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\Ecs\AutoChessExecuteDamageCalculationSystem.cs"
$autoChessSessionPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\AutoChessBattleSession.cs"
$autoChessResultBuilderPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\AutoChessBattleResultBuilder.cs"
$autoChessCoreBridgePath = Join-Path $ProjectPath "Assets\AutoChessDemo\Integration\GasCore\AutoChessGasCoreBridge.cs"
$autoChessLifecyclePath = Join-Path $ProjectPath "Assets\AutoChessDemo\Integration\GasCore\AutoChessGasBattleEntityLifecycle.cs"
$autoChessObservationGatewayPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Integration\GasCore\AutoChessGasObservationGateway.cs"
$autoChessUnitSnapshotProjectorPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Integration\GasCore\AutoChessGasBattleUnitSnapshotProjector.cs"
$autoChessValidationReportPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\Validation\AutoChessBattleValidationReport.cs"
$autoChessValidationRunPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\Validation\AutoChessBattleValidationRun.cs"
$autoChessRuntimeHostPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Integration\GasCore\AutoChessGasRuntimeHost.cs"
$autoChessCatalogSessionPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Integration\GasCore\AutoChessGasCatalogSession.cs"
$autoChessRuntimeAccessPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Integration\GasCore\AutoChessGasRuntimeAccess.cs"
$autoChessDefinitionCatalogBuilderPath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\Ecs\AutoChessBattleDefinitionCatalogBuilder.cs"

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
Assert-FileNotContains `
    -Path $ascArchetypePath `
    -Pattern "EffectCommandStream\(EntityManager em\)[\s\S]*?ComponentType\.ReadWrite<AttributeModifierBuffer>\(\)[\s\S]*?return _effectCommandStream;" `
    -Message "EffectCommandStream archetype must not carry AttributeModifierBuffer after AttributeDelta moves owner-local."
Assert-FileNotContains `
    -Path $streamPath `
    -Pattern "HasRequiredBuffers[\s\S]*AttributeModifierBuffer" `
    -Message "EffectCommandSpecStream required stream buffers must not include AttributeModifierBuffer."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "OwnerLocalGameplayFactBuffer" `
    -Message "Runtime Core must define an owner-local gameplay fact buffer for ASC-local fact projection."
Assert-FileContains `
    -Path $ascArchetypePath `
    -Pattern "ComponentType\.ReadWrite<OwnerLocalGameplayFactBuffer>\(\)" `
    -Message "ASC runtime archetype must own an owner-local gameplay fact buffer."
Assert-FileContains `
    -Path $deltaApplyPath `
    -Pattern "BufferTypeHandle<OwnerLocalGameplayFactBuffer>" `
    -Message "Pending AttributeDelta apply must write attribute facts through an owner-local fact buffer."
Assert-FileContains `
    -Path $deltaApplyPath `
    -Pattern "AppendAttributeChangeFact\(ownerFacts" `
    -Message "Pending AttributeDelta apply must append attribute facts to the ASC owner-local fact buffer before export."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "GameplayOwnerLocalFactFramePrepareSystem" `
    -Message "Runtime Core must clear owner-local gameplay facts during FramePrepare."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "GameplayOwnerLocalFactFlushSystem" `
    -Message "Runtime Core must flush owner-local gameplay facts through a dedicated projection system."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "OwnerLocalGameplayFactRecordComparer" `
    -Message "Owner-local gameplay fact flush must define deterministic owner/sequence ordering."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "UpdateBefore\(typeof\(GameplayFactProjectionSystem\)\)[\s\S]*GameplayOwnerLocalFactFlushSystem" `
    -Message "Owner-local gameplay fact flush must run before stream fact projection to preserve fact sequence order."
Assert-FileContains `
    -Path $scheduleContractPath `
    -Pattern "GameplayOwnerLocalFactFramePrepareSystem" `
    -Message "Owner-local gameplay fact frame prepare system must be registered in the runtime schedule."
Assert-FileContains `
    -Path $scheduleContractPath `
    -Pattern "GameplayOwnerLocalFactFlushSystem" `
    -Message "Owner-local gameplay fact flush system must be registered in the runtime schedule."
Assert-FileContains `
    -Path $scheduleContractPath `
    -Pattern "GameplayOwnerLocalFactFlushSystem[\s\S]*GameplayFactProjectionSystem" `
    -Message "Owner-local gameplay fact flush must precede GameplayFactProjectionSystem in the runtime schedule contract."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "RuntimeCoreOwnerLocalFactCount" `
    -Message "Runtime Debugger must keep owner-local fact counters in the retained runtime core state."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "OwnerLocalFactCount\s*=\s*stream\.OwnerLocalFactCount" `
    -Message "Runtime Debugger must collect owner-local fact counters from the stream component."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "ownerLocalFactFlushes" `
    -Message "Runtime Debugger text summaries must expose owner-local fact flush counters."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "ownerLocalFactFlushes" `
    -Message "AutoChess validation summary must expose owner-local fact flush evidence."
Assert-FileContains `
    -Path $autoChessValidationRunPath `
    -Pattern "OwnerLocalFactFlushCount\s*>\s*0" `
    -Message "AutoChess runtime chain gate must require owner-local fact flush evidence."
Assert-FileContains `
    -Path $streamOwnerContractPath `
    -Pattern "EGasRuntimeFrameStreamId\.AttributeDelta[\s\S]*EGasRuntimeFrameStreamCarrier\.OwnerLocalDynamicBuffer,\s*[\r\n\s]*EGasRuntimeFrameStreamCarrier\.OwnerLocalDynamicBuffer" `
    -Message "AttributeDelta stream owner contract must use owner-local carriers for current and target state."
Assert-FileNotContains `
    -Path $queryLayoutPlanPath `
    -Pattern "GASRuntimeQueryLayoutEntryId\.GameplayEffectCommandSpecStream[\s\S]*GASRuntimeLayoutComponentSlot\.AttributeDeltaBuffer[\s\S]*typeof\(GameplayFactProjectionSystem\)" `
    -Message "AttributeDeltaBuffer must not remain classified with the EffectCommandSpecStream layout."
Assert-FileContains `
    -Path $queryLayoutPlanPath `
    -Pattern "GASRuntimeQueryLayoutEntryId\.ActiveEffectStore[\s\S]*GASRuntimeLayoutComponentSlot\.AttributeDeltaBuffer" `
    -Message "AttributeDeltaBuffer must be classified with the ASC owner-local layout."
Assert-FileContains `
    -Path $ascArchetypePath `
    -Pattern "ComponentType\.ReadWrite<GEEffectCommandBuffer>\(\)" `
    -Message "ASC runtime archetype must own GEEffectCommandBuffer as an owner-local instant command source."
Assert-FileContains `
    -Path $ascArchetypePath `
    -Pattern "ComponentType\.ReadWrite<GESetByCallerValueBuffer>\(\)" `
    -Message "ASC runtime archetype must own GESetByCallerValueBuffer as owner-local instant command payload."
Assert-FileContains `
    -Path $ascFactoryPath `
    -Pattern "GetBuffer<GEEffectCommandBuffer>\(asc\)\.EnsureCapacity" `
    -Message "ASC factory must initialize owner-local instant command capacity."
Assert-FileContains `
    -Path $ascFactoryPath `
    -Pattern "GetBuffer<GESetByCallerValueBuffer>\(asc\)\.EnsureCapacity" `
    -Message "ASC factory must initialize owner-local instant set-by-caller capacity."
Assert-FileContains `
    -Path $ascFactoryPath `
    -Pattern "HasBuffer<GEEffectCommandBuffer>\(asc\)" `
    -Message "ASC runtime component completeness check must require owner-local instant command buffer."
Assert-FileContains `
    -Path $ascFactoryPath `
    -Pattern "HasBuffer<GESetByCallerValueBuffer>\(asc\)" `
    -Message "ASC runtime component completeness check must require owner-local instant set-by-caller buffer."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "struct OwnerLocalInstantNextFrameCommandBuffer\s*:\s*IBufferElementData" `
    -Message "Runtime Core must define an owner-local next-frame instant command carrier for producers emitted after instant flush."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "struct OwnerLocalInstantNextFrameSetByCallerValueBuffer\s*:\s*IBufferElementData" `
    -Message "Runtime Core must define an owner-local next-frame instant set-by-caller carrier for deferred instant payloads."
Assert-FileContains `
    -Path $ascArchetypePath `
    -Pattern "ComponentType\.ReadWrite<OwnerLocalInstantNextFrameCommandBuffer>\(\)" `
    -Message "ASC runtime archetype must own OwnerLocalInstantNextFrameCommandBuffer for post-flush instant producers."
Assert-FileContains `
    -Path $ascArchetypePath `
    -Pattern "ComponentType\.ReadWrite<OwnerLocalInstantNextFrameSetByCallerValueBuffer>\(\)" `
    -Message "ASC runtime archetype must own OwnerLocalInstantNextFrameSetByCallerValueBuffer for deferred instant payloads."
Assert-FileContains `
    -Path $ascFactoryPath `
    -Pattern "GetBuffer<OwnerLocalInstantNextFrameCommandBuffer>\(asc\)\.EnsureCapacity" `
    -Message "ASC factory must initialize owner-local next-frame instant command capacity."
Assert-FileContains `
    -Path $ascFactoryPath `
    -Pattern "HasBuffer<OwnerLocalInstantNextFrameSetByCallerValueBuffer>\(asc\)" `
    -Message "ASC runtime component completeness check must require owner-local next-frame instant payload buffer."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "OwnerLocalInstantCommandFramePrepareSystem" `
    -Message "Runtime Core must clear ASC owner-local instant commands during FramePrepare."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "BufferTypeHandle<GEEffectCommandBuffer>" `
    -Message "Runtime Core must clear ASC owner-local instant commands with a chunk buffer type handle."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "BufferTypeHandle<GESetByCallerValueBuffer>" `
    -Message "Runtime Core must clear ASC owner-local instant set-by-caller payloads with a chunk buffer type handle."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "OwnerLocalInstantCommandFramePrepareSystem[\s\S]*?NextFrameCommandType[\s\S]*GetBufferTypeHandle<OwnerLocalInstantNextFrameCommandBuffer>[\s\S]*deferredCommands\.Clear\(\)" `
    -Message "FramePrepare must move next-frame instant commands into current owner-local instant commands before clearing the deferred carrier."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "OwnerLocalInstantCommandFramePrepareSystem[\s\S]*?NextFrameSetByCallerType[\s\S]*GetBufferTypeHandle<OwnerLocalInstantNextFrameSetByCallerValueBuffer>[\s\S]*deferredSetByCallerValues\.Clear\(\)" `
    -Message "FramePrepare must move next-frame instant payloads into current owner-local instant payloads before clearing the deferred carrier."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "OwnerLocalInstantCommandFlushSystem[\s\S]*?CollectOwnerLocalInstantCommandsJob[\s\S]*?FlushOwnerLocalInstantCommandsJob" `
    -Message "Runtime Core must flush ASC owner-local instant commands through a deterministic merge system."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "OwnerLocalInstantCommandRecordComparer[\s\S]*?x\.Command\.Sequence\.CompareTo\(y\.Command\.Sequence\)" `
    -Message "Owner-local instant command flush must sort by command sequence before appending to the spec stream."
Assert-FileContains `
    -Path $scheduleContractPath `
    -Pattern "OwnerLocalInstantCommandFramePrepareSystem" `
    -Message "Owner-local instant command frame prepare system must be registered in the runtime schedule."
Assert-FileContains `
    -Path $scheduleContractPath `
    -Pattern "OwnerLocalInstantCommandFlushSystem" `
    -Message "Owner-local instant command flush system must be registered in the runtime schedule."
Assert-FileContains `
    -Path $queryLayoutPlanPath `
    -Pattern "GASRuntimeQueryLayoutEntryId\.ActiveEffectStore[\s\S]*GASRuntimeLayoutComponentSlot\.EffectCommandBuffer[\s\S]*GASRuntimeLayoutComponentSlot\.EffectCommandSetByCallerBuffer" `
    -Message "Owner-local instant command buffers must be classified with the ASC owner-local layout."
Assert-FileContains `
    -Path $queryLayoutPlanPath `
    -Pattern "GASRuntimeQueryLayoutEntryId\.ActiveEffectStore[\s\S]*GASRuntimeLayoutComponentSlot\.OwnerLocalInstantNextFrameCommandBuffer" `
    -Message "OwnerLocalInstantNextFrameCommandBuffer must be classified with the ASC ActiveEffectStore layout."
Assert-FileContains `
    -Path $streamOwnerContractPath `
    -Pattern "EGasRuntimeFrameStreamId\.OwnerLocalInstantNextFrame[\s\S]*EGasRuntimeFrameStreamCarrier\.OwnerLocalDynamicBuffer,\s*[\r\n\s]*EGasRuntimeFrameStreamCarrier\.OwnerLocalDynamicBuffer" `
    -Message "OwnerLocalInstantNextFrame stream owner contract must use owner-local carriers across the frame boundary."
Assert-FileContains `
    -Path $generatedInstantEffectPath `
    -Pattern "UpdateAfter\(typeof\(OwnerLocalInstantCommandFlushSystem\)\)" `
    -Message "Generated instant spec build must run after owner-local instant command flush."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern 'writer\.WriteLine\("\[UpdateAfter\(typeof\(OwnerLocalInstantCommandFlushSystem\)\)\]"' `
    -Message "CodeGen template must regenerate the instant spec build order after owner-local instant command flush."
Assert-FileNotContains `
    -Path $generatedInstantEffectPath `
    -Pattern "DeltaLookup\s*=\s*SystemAPI\.GetBufferLookup<AttributeModifierBuffer>" `
    -Message "Generated instant AttributeDelta must not write facts through the singleton stream delta buffer."
Assert-FileContains `
    -Path $generatedInstantEffectPath `
    -Pattern "OwnerFactLookup\s*=\s*SystemAPI\.GetBufferLookup<OwnerLocalGameplayFactBuffer>" `
    -Message "Generated instant AttributeDelta must write directly to ASC owner-local fact buffers."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern 'writer\.WriteLine\("DeltaLookup = SystemAPI\.GetBufferLookup<AttributeModifierBuffer>' `
    -Message "CodeGen must not regenerate singleton stream DeltaLookup for instant AttributeDelta facts."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern 'writer\.WriteLine\("OwnerFactLookup = SystemAPI\.GetBufferLookup<OwnerLocalGameplayFactBuffer>' `
    -Message "CodeGen must regenerate instant AttributeDelta owner-local fact output."
Assert-FileNotContains `
    -Path $executionCalculationOutputModifierSystemPath `
    -Pattern "DeltaBufferLookup" `
    -Message "Execution output AttributeDelta facts must not use the singleton stream delta buffer."
Assert-FileContains `
    -Path $executionCalculationOutputModifierSystemPath `
    -Pattern "OwnerFactLookup\s*=\s*SystemAPI\.GetBufferLookup<OwnerLocalGameplayFactBuffer>" `
    -Message "Execution output AttributeDelta facts must write directly to ASC owner-local fact buffers."
Assert-FileNotContains `
    -Path $ascArchetypePath `
    -Pattern "ComponentType\.ReadWrite<ActiveEffectMutationBuffer>\(\),\s*[\r\n\s]*ComponentType\.ReadWrite<GameplayEventBuffer>\(\)" `
    -Message "EffectCommandStream archetype must not keep ActiveEffectMutationBuffer beside the singleton GameplayEventBuffer stream."
Assert-FileNotContains `
    -Path $streamPath `
    -Pattern "HasRequiredBuffers[\s\S]*ActiveEffectMutationBuffer" `
    -Message "EffectCommandSpecStream required stream buffers must not include ActiveEffectMutationBuffer."
Assert-FileContains `
    -Path $ascArchetypePath `
    -Pattern "ComponentType\.ReadWrite<ActiveEffectMutationBuffer>\(\)" `
    -Message "ASC runtime archetype must own ActiveEffectMutationBuffer as an owner-local carrier."
Assert-FileContains `
    -Path $ascArchetypePath `
    -Pattern "ComponentType\.ReadWrite<ActiveEffectMutationCommandBuffer>\(\)" `
    -Message "ASC runtime archetype must own ActiveEffectMutationCommandBuffer as an owner-local command source."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "struct ActiveEffectMutationSetByCallerValueBuffer\s*:\s*IBufferElementData" `
    -Message "Runtime Core must define an owner-local set-by-caller carrier for active mutation commands."
Assert-FileContains `
    -Path $ascArchetypePath `
    -Pattern "ComponentType\.ReadWrite<ActiveEffectMutationSetByCallerValueBuffer>\(\)" `
    -Message "ASC runtime archetype must own ActiveEffectMutationSetByCallerValueBuffer as owner-local command payload."
Assert-FileContains `
    -Path $ascFactoryPath `
    -Pattern "GetBuffer<ActiveEffectMutationSetByCallerValueBuffer>\(asc\)\.EnsureCapacity" `
    -Message "ASC factory must initialize owner-local active mutation set-by-caller capacity."
Assert-FileContains `
    -Path $ascFactoryPath `
    -Pattern "HasBuffer<ActiveEffectMutationSetByCallerValueBuffer>\(asc\)" `
    -Message "ASC runtime component completeness check must require owner-local active mutation set-by-caller buffer."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "ActiveEffectOwnerLocalMutationFramePrepareSystem" `
    -Message "Runtime Core must clear ASC owner-local active effect mutations during FramePrepare."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "BufferTypeHandle<ActiveEffectMutationCommandBuffer>" `
    -Message "Runtime Core must clear ASC owner-local active mutation commands during FramePrepare."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "BufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer>" `
    -Message "Runtime Core must clear ASC owner-local active mutation set-by-caller payloads during FramePrepare."
Assert-FileContains `
    -Path $scheduleContractPath `
    -Pattern "ActiveEffectOwnerLocalMutationFramePrepareSystem" `
    -Message "ActiveEffect owner-local mutation frame prepare system must be registered in the runtime schedule."
Assert-FileContains `
    -Path $streamOwnerContractPath `
    -Pattern "EGasRuntimeFrameStreamId\.ActiveEffectMutation[\s\S]*EGasRuntimeFrameStreamCarrier\.OwnerLocalDynamicBuffer,\s*[\r\n\s]*EGasRuntimeFrameStreamCarrier\.OwnerLocalDynamicBuffer" `
    -Message "ActiveEffectMutation stream owner contract must use owner-local carriers for current and target state."
Assert-FileContains `
    -Path $queryLayoutPlanPath `
    -Pattern "GASRuntimeQueryLayoutEntryId\.ActiveEffectStore[\s\S]*GASRuntimeLayoutComponentSlot\.ActiveEffectMutationBuffer" `
    -Message "ActiveEffectMutationBuffer must be classified with the ASC ActiveEffectStore layout, not the command stream layout."
Assert-FileContains `
    -Path $queryLayoutPlanPath `
    -Pattern "GASRuntimeQueryLayoutEntryId\.ActiveEffectStore[\s\S]*GASRuntimeLayoutComponentSlot\.ActiveEffectMutationCommandBuffer" `
    -Message "ActiveEffectMutationCommandBuffer must be classified with the ASC ActiveEffectStore layout, not the command stream layout."
Assert-FileContains `
    -Path $queryLayoutPlanPath `
    -Pattern "GASRuntimeQueryLayoutEntryId\.ActiveEffectStore[\s\S]*GASRuntimeLayoutComponentSlot\.ActiveEffectMutationSetByCallerBuffer" `
    -Message "ActiveEffectMutationSetByCallerBuffer must be classified with the ASC ActiveEffectStore layout, not the command stream layout."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "struct ActiveEffectNextFrameMutationCommandBuffer\s*:\s*IBufferElementData" `
    -Message "Runtime Core must define an owner-local next-frame command carrier for active mutation commands emitted after collect."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "struct ActiveEffectNextFrameMutationSetByCallerValueBuffer\s*:\s*IBufferElementData" `
    -Message "Runtime Core must define an owner-local next-frame set-by-caller carrier for deferred active mutation commands."
Assert-FileContains `
    -Path $ascArchetypePath `
    -Pattern "ComponentType\.ReadWrite<ActiveEffectNextFrameMutationCommandBuffer>\(\)" `
    -Message "ASC runtime archetype must own ActiveEffectNextFrameMutationCommandBuffer for post-collect active mutation producers."
Assert-FileContains `
    -Path $ascArchetypePath `
    -Pattern "ComponentType\.ReadWrite<ActiveEffectNextFrameMutationSetByCallerValueBuffer>\(\)" `
    -Message "ASC runtime archetype must own ActiveEffectNextFrameMutationSetByCallerValueBuffer for deferred active mutation payloads."
Assert-FileContains `
    -Path $ascFactoryPath `
    -Pattern "GetBuffer<ActiveEffectNextFrameMutationCommandBuffer>\(asc\)\.EnsureCapacity" `
    -Message "ASC factory must initialize owner-local next-frame active mutation command capacity."
Assert-FileContains `
    -Path $ascFactoryPath `
    -Pattern "HasBuffer<ActiveEffectNextFrameMutationSetByCallerValueBuffer>\(asc\)" `
    -Message "ASC runtime component completeness check must require owner-local next-frame active mutation payload buffer."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "NextFrameCommandType[\s\S]*GetBufferTypeHandle<ActiveEffectNextFrameMutationCommandBuffer>[\s\S]*deferredCommands\.Clear\(\)" `
    -Message "FramePrepare must move next-frame active mutation commands into current owner-local commands before clearing the deferred carrier."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "NextFrameSetByCallerType[\s\S]*GetBufferTypeHandle<ActiveEffectNextFrameMutationSetByCallerValueBuffer>[\s\S]*deferredSetByCallerValues\.Clear\(\)" `
    -Message "FramePrepare must move next-frame active mutation payloads into current owner-local payloads before clearing the deferred carrier."
Assert-FileContains `
    -Path $queryLayoutPlanPath `
    -Pattern "GASRuntimeQueryLayoutEntryId\.ActiveEffectStore[\s\S]*GASRuntimeLayoutComponentSlot\.ActiveEffectNextFrameMutationCommandBuffer" `
    -Message "ActiveEffectNextFrameMutationCommandBuffer must be classified with the ASC ActiveEffectStore layout."
Assert-FileContains `
    -Path $streamOwnerContractPath `
    -Pattern "EGasRuntimeFrameStreamId\.ActiveEffectNextFrameMutation[\s\S]*EGasRuntimeFrameStreamCarrier\.OwnerLocalDynamicBuffer,\s*[\r\n\s]*EGasRuntimeFrameStreamCarrier\.OwnerLocalDynamicBuffer" `
    -Message "ActiveEffectNextFrameMutation stream owner contract must use owner-local carriers across the frame boundary."
Assert-FileNotContains `
    -Path $debuggerPath `
    -Pattern "RecordFrameStreamBufferPressure<ActiveEffectMutationBuffer>" `
    -Message "Debugger stream pressure sampling must not treat owner-local ActiveEffectMutationBuffer as an EffectCommandSpecStream buffer."
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
Assert-FileContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "AppendPreparedCommand\([\s\S]*?if\s*\(command\.Kind == GEEffectCommandKind\.Instant\)[\s\S]*?writer\.AppendOwnerLocalInstantCommand" `
    -Message "GameplayEffectRequestWriter must route instant runtime requests through owner-local ASC command buffers."
Assert-FileContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "AppendPreparedCommand\([\s\S]*?TryGetActiveMutationOwnerPayload[\s\S]*?writer\.AppendOwnerLocalActiveMutationCommand" `
    -Message "GameplayEffectRequestWriter must route active mutation runtime requests through owner-local ASC command buffers."
Assert-FileContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "TryGetInstantCommandOwnerPayload\([\s\S]*?em\.GetBuffer<GESetByCallerValueBuffer>\(targetAsc\)" `
    -Message "GameplayEffectRequestWriter must resolve owner-local instant set-by-caller payload buffers."
Assert-FileContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "TryGetInstantCommandOwnerPayload\([\s\S]*?em\.GetBuffer<GEEffectCommandBuffer>\(targetAsc\)" `
    -Message "GameplayEffectRequestWriter must resolve target ASC owner-local instant command buffers."
Assert-FileContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "TryGetActiveMutationOwnerPayload\([\s\S]*?em\.GetBuffer<ActiveEffectMutationSetByCallerValueBuffer>\(targetAsc\)" `
    -Message "GameplayEffectRequestWriter must resolve owner-local active mutation set-by-caller payload buffers."
Assert-FileContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "TryGetActiveMutationOwnerPayload\([\s\S]*?em\.GetBuffer<ActiveEffectMutationCommandBuffer>\(targetAsc\)" `
    -Message "GameplayEffectRequestWriter must resolve target ASC owner-local command buffers."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "AppendOwnerLocalActiveMutationCommand[\s\S]*?ownerCommands\.Add\(new ActiveEffectMutationCommandBuffer" `
    -Message "EffectCommandSpecStream writer must support owner-local active mutation command append without adding to the singleton command buffer."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "AppendOwnerLocalActiveMutationCommand[\s\S]*?CopyRequestSetByCallerValues\(ownerSetByCallerValues" `
    -Message "EffectCommandSpecStream writer must copy runtime request set-by-caller payloads to owner-local active mutation payload buffers."
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
    -Pattern "GEActiveEffectMutationOwnerCommandCollectJob\s*:\s*IJobChunk" `
    -Message "Generated active effect runtime must collect active mutation commands from owner-local buffers before chunk-local apply."
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
    -Pattern "GEActiveEffectMutationOwnerCommandFinalizeJob\s*:\s*IJob[\s\S]*?\[ReadOnly\] public BufferLookup<AttributeValueBuffer> AttributeLookup" `
    -Message "Generated active mutation SourceAttribute capture must happen in the read-only owner-local finalize/snapshot lane."
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
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "ActiveMutationCommandLookup\s*=\s*SystemAPI\.GetBufferLookup<ActiveEffectMutationCommandBuffer>" `
    -Message "Active mutation commands must be projected into ASC owner-local command buffers."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "ActiveMutationSetByCallerLookup\s*=\s*SystemAPI\.GetBufferLookup<ActiveEffectMutationSetByCallerValueBuffer>" `
    -Message "Active mutation set-by-caller payloads must be projected into ASC owner-local buffers."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "CopySetByCallerValuesToOwner" `
    -Message "Active mutation normalize must copy stream set-by-caller ranges into owner-local command payloads."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "SetByCallerBufferTypeHandle\s*=[\s\S]*?SystemAPI\.GetBufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer>" `
    -Message "Active mutation owner system must pass owner-local set-by-caller buffers into the collect job."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectMutationOwnerCommandCollectJob\s*:\s*IJobChunk" `
    -Message "Generated active mutation command source must collect from owner-local command buffers."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "BufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer>\s+SetByCallerBufferTypeHandle" `
    -Message "Generated active mutation command source must collect owner-local set-by-caller payloads."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "ActiveMutationSetByCallerValues\.Add\(new GESetByCallerValueBuffer" `
    -Message "Generated active mutation collect must flatten owner-local set-by-caller payloads into a frame-local list."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "\[ReadOnly\] public NativeList<GESetByCallerValueBuffer> ActiveMutationSetByCallerValues" `
    -Message "Generated active mutation apply must consume frame-local set-by-caller payloads."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "TryApplyActiveMutationToOwner[\s\S]*?NativeList<GESetByCallerValueBuffer> setByCallerValues" `
    -Message "Generated active mutation apply must pass owner-local set-by-caller payloads through the active mutation path."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "TryFindSetByCallerValue\([\s\S]*?NativeList<GESetByCallerValueBuffer> setByCallerValues" `
    -Message "Generated active mutation magnitude resolution must read set-by-caller payloads from the owner-local frame list."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectMutationOwnerCommandFinalizeJob\s*:\s*IJob" `
    -Message "Generated active mutation finalize must keep owner-local commands separate from singleton stream gather."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "GEActiveEffectMutationOwnerCommandCollectJob\s*:\s*IJobChunk" `
    -Message "CodeGen template must keep owner-local active mutation command collection."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "SetByCallerBufferTypeHandle\s*=[\s\S]*?SystemAPI\.GetBufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer>" `
    -Message "CodeGen template must keep owner-local active mutation set-by-caller collection."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "BufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer>\s+SetByCallerBufferTypeHandle" `
    -Message "CodeGen template must keep active mutation collect job set-by-caller buffer field."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "ActiveMutationSetByCallerValues\.Add\(new GESetByCallerValueBuffer" `
    -Message "CodeGen template must keep frame-local set-by-caller flattening for active mutation."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "\[ReadOnly\] public NativeList<GESetByCallerValueBuffer> ActiveMutationSetByCallerValues" `
    -Message "CodeGen template must keep active mutation apply on owner-local set-by-caller payloads."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "TryFindSetByCallerValue\([\s\S]*?NativeList<GESetByCallerValueBuffer> setByCallerValues" `
    -Message "CodeGen template must not regenerate stream-backed set-by-caller magnitude resolution for active mutation."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "GEActiveEffectMutationOwnerCommandFinalizeJob\s*:\s*IJob" `
    -Message "CodeGen template must keep owner-local active mutation command finalization."
Assert-FileContains `
    -Path $generatedAbilityActivationPath `
    -Pattern "ActiveMutationCommandLookup\s*=\s*SystemAPI\.GetBufferLookup<ActiveEffectMutationCommandBuffer>\(isReadOnly:\s*false\)" `
    -Message "Generated ability commit must acquire ASC owner-local active mutation command buffers."
Assert-FileContains `
    -Path $generatedAbilityActivationPath `
    -Pattern "ActiveMutationSetByCallerLookup\s*=\s*SystemAPI\.GetBufferLookup<ActiveEffectMutationSetByCallerValueBuffer>\(isReadOnly:\s*false\)" `
    -Message "Generated ability commit must acquire ASC owner-local active mutation payload buffers."
Assert-FileContains `
    -Path $generatedAbilityActivationPath `
    -Pattern "CommandLookup\s*=\s*SystemAPI\.GetBufferLookup<GEEffectCommandBuffer>\(\)" `
    -Message "Generated ability commit must acquire ASC owner-local instant command buffers."
Assert-FileContains `
    -Path $generatedAbilityActivationPath `
    -Pattern "if\s*\(resolved\.Kind == GEEffectCommandKind\.ActiveMutation\)[\s\S]*?AppendActiveMutationCommand\(in resolved\);[\s\S]*?else[\s\S]*?AppendInstantCommand\(in resolved\);" `
    -Message "Generated ability commit must route active and instant commands through owner-local ASC command buffers."
Assert-FileContains `
    -Path $generatedAbilityActivationPath `
    -Pattern "ActiveMutationCommandLookup\[targetAsc\]\.Add\(new ActiveEffectMutationCommandBuffer" `
    -Message "Generated ability commit must append active mutation commands to the target ASC owner-local command buffer."
Assert-FileContains `
    -Path $generatedAbilityActivationPath `
    -Pattern "AppendInstantCommand[\s\S]*?CommandLookup\[targetAsc\]\.Add\(ownerCommand\)" `
    -Message "Generated ability commit must append instant commands to the target ASC owner-local command buffer."
Assert-FileNotContains `
    -Path $generatedAbilityActivationPath `
    -Pattern "CommandLookup\[StreamEntity\]\.Add\(resolved\)" `
    -Message "Generated ability commit must not append instant commands directly to the singleton command stream."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "ActiveMutationCommandLookup\s*=\s*SystemAPI\.GetBufferLookup<ActiveEffectMutationCommandBuffer>\(isReadOnly:\s*false\)" `
    -Message "CodeGen template must acquire ASC owner-local active mutation command buffers for ability commit."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "if\s*\(resolved\.Kind == GEEffectCommandKind\.ActiveMutation\)[\s\S]*?AppendActiveMutationCommand\(in resolved\);[\s\S]*?else[\s\S]*?AppendInstantCommand\(in resolved\);" `
    -Message "CodeGen template must keep ability commit active and instant commands off the singleton command stream."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "AppendInstantCommand[\s\S]*?CommandLookup\[targetAsc\]\.Add\(ownerCommand\)" `
    -Message "CodeGen template must regenerate ability commit instant owner-local command append."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "private void AppendEffectCommand[\s\S]{0,1800}CommandLookup\[StreamEntity\]\.Add\(resolved\)" `
    -Message "CodeGen template must not regenerate ability commit instant singleton command append."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "AppendOwnerLocalActiveMutationCommand\([\s\S]*?DynamicBuffer<ActiveEffectMutationCommandBuffer> ownerCommands[\s\S]*?DynamicBuffer<ActiveEffectMutationSetByCallerValueBuffer> ownerSetByCallerValues" `
    -Message "EffectCommandSpecStream.CommandWriter must expose owner-local active mutation append while retaining stream-owned sequence allocation."
Assert-FileContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "AppendPreparedCommand\([\s\S]*?writer\.AppendOwnerLocalActiveMutationCommand" `
    -Message "GameplayEffectRequestWriter must route runtime/simple active mutation commands to ASC owner-local command buffers."
Assert-FileContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "AppendPreparedCommand\([\s\S]*?writer\.AppendOwnerLocalInstantCommand" `
    -Message "GameplayEffectRequestWriter must route runtime/simple instant commands to ASC owner-local command buffers."
Assert-FileContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "CanAppendInstantCommand\([\s\S]*?HasBuffer<GEEffectCommandBuffer>[\s\S]*?HasBuffer<GESetByCallerValueBuffer>" `
    -Message "GameplayEffectRequestWriter must preflight owner-local instant command payload buffers."
Assert-FileContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "CanAppendActiveMutationCommand\([\s\S]*?HasBuffer<ActiveEffectMutationCommandBuffer>[\s\S]*?HasBuffer<ActiveEffectMutationSetByCallerValueBuffer>" `
    -Message "GameplayEffectRequestWriter must preflight owner-local active mutation payload buffers."
Assert-FileNotContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "var resolved\s*=\s*writer\.AppendCommand\(command,\s*setByCallerValues\);" `
    -Message "GameplayEffectRequestWriter must not directly append prepared commands to the singleton stream without lane selection."
Assert-FileNotContains `
    -Path $gameplayEffectRequestWriterPath `
    -Pattern "PrepareOwnerLocalCommand|Allocate\(ref stream\.NextCommandSequence\)" `
    -Message "GameplayEffectRequestWriter must not duplicate EffectCommandSpecStream command sequence allocation."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "MutationBufferTypeHandle\s*=\s*SystemAPI\.GetBufferTypeHandle<ActiveEffectMutationBuffer>\(isReadOnly:\s*false\)" `
    -Message "Generated active effect pre-tick/remove must pass ASC owner-local mutation buffers by chunk type handle."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "public BufferTypeHandle<ActiveEffectMutationBuffer>\s+MutationBufferTypeHandle" `
    -Message "Generated active effect pre-tick/remove job must own ActiveEffectMutationBuffer as an owner-local chunk buffer."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "var mutationBuffers\s*=\s*chunk\.GetBufferAccessor\(ref MutationBufferTypeHandle\)[\s\S]*?var mutations\s*=\s*mutationBuffers\[entityIndex\]" `
    -Message "Generated active effect pre-tick/remove must write mutations to the current ASC owner-local buffer."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "var mutationBuffers\s*=\s*chunk\.GetBufferAccessor\(ref MutationBufferTypeHandle\)[\s\S]*?var mutations\s*=\s*mutationBuffers\[entityIndex\]" `
    -Message "CodeGen template must keep active effect pre-tick/remove mutation output owner-local."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "MutationLookup\.HasBuffer\(StreamEntity\)|MutationLookup\[StreamEntity\]|public BufferLookup<ActiveEffectMutationBuffer>\s+MutationLookup" `
    -Message "Generated active effect pre-tick/remove must not read ActiveEffectMutationBuffer from the singleton stream owner."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "MutationLookup\.HasBuffer\(StreamEntity\)|MutationLookup\[StreamEntity\]|public BufferLookup<ActiveEffectMutationBuffer>\s+MutationLookup" `
    -Message "CodeGen template must not regenerate singleton stream ActiveEffectMutationBuffer access for pre-tick/remove."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "ActiveMutationCommandLookup\s*=\s*SystemAPI\.GetBufferLookup<ActiveEffectMutationCommandBuffer>\(isReadOnly:\s*false\)" `
    -Message "Generated active effect pre-tick must acquire ASC owner-local active mutation command buffers for period commands."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "if\s*\(kind == GEEffectCommandKind\.ActiveMutation\)[\s\S]*?ActiveMutationCommandLookup\[ownerResources\.Owner\]\.Add\(new ActiveEffectMutationCommandBuffer" `
    -Message "Generated period active mutation commands must be routed to owner-local ASC buffers."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "var ownerCommand = PrepareCommand\([\s\S]*?ownerSetByCallerValues\.Length[\s\S]*?Command = ownerCommand" `
    -Message "Generated period active mutation route must remap set-by-caller payloads into owner-local command payload ranges."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "if\s*\(kind == GEEffectCommandKind\.ActiveMutation\)[\s\S]*?ActiveMutationCommandLookup\[ownerResources\.Owner\]\.Add\(new ActiveEffectMutationCommandBuffer" `
    -Message "CodeGen template must keep period active mutation commands off the singleton command stream."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "var ownerCommand = PrepareCommand\([\s\S]*?ownerSetByCallerValues\.Length[\s\S]*?Command = ownerCommand" `
    -Message "CodeGen template must keep owner-local period set-by-caller payload remapping."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "var ownerInstantSetByCallerValues\s*=\s*CommandSetByCallerLookup\[ownerResources\.Owner\][\s\S]*?CommandLookup\[ownerResources\.Owner\]\.Add\(resolved\)" `
    -Message "Generated period instant commands must be routed through ASC owner-local instant buffers."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "var ownerInstantSetByCallerValues\s*=\s*CommandSetByCallerLookup\[ownerResources\.Owner\][\s\S]*?CommandLookup\[ownerResources\.Owner\]\.Add\(resolved\)" `
    -Message "CodeGen template must keep period instant commands on ASC owner-local instant buffers."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "EmitPeriodCommand[\s\S]*?CommandSetByCallerLookup\[StreamEntity\][\s\S]*?CommandLookup\[StreamEntity\]\.Add\(resolved\)" `
    -Message "Generated period instant commands must not append to singleton stream command buffers."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "EmitPeriodCommand[\s\S]*?CommandSetByCallerLookup\[StreamEntity\][\s\S]*?CommandLookup\[StreamEntity\]\.Add\(resolved\)" `
    -Message "CodeGen template must not regenerate singleton stream appends for period instant commands."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "NextFrameInstantCommandLookup\s*=\s*[\s\S]*?SystemAPI\.GetBufferLookup<OwnerLocalInstantNextFrameCommandBuffer>\(isReadOnly:\s*false\)" `
    -Message "Generated active mutation apply must acquire owner-local next-frame instant command buffers for post-flush instant producers."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "NextFrameInstantSetByCallerLookup\s*=\s*[\s\S]*?SystemAPI\.GetBufferLookup<OwnerLocalInstantNextFrameSetByCallerValueBuffer>\(isReadOnly:\s*false\)" `
    -Message "Generated active mutation apply must acquire owner-local next-frame instant payload buffers for post-flush instant producers."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "NextFrameActiveMutationCommandLookup\s*=\s*[\s\S]*?SystemAPI\.GetBufferLookup<ActiveEffectNextFrameMutationCommandBuffer>\(isReadOnly:\s*false\)" `
    -Message "Generated active mutation apply must acquire owner-local next-frame command buffers for post-collect active mutation producers."
Assert-FileContains `
    -Path $activeEffectLifecycleOwnerPath `
    -Pattern "NextFrameActiveMutationSetByCallerLookup\s*=\s*[\s\S]*?SystemAPI\.GetBufferLookup<ActiveEffectNextFrameMutationSetByCallerValueBuffer>\(isReadOnly:\s*false\)" `
    -Message "Generated active mutation apply must acquire owner-local next-frame payload buffers for post-collect active mutation producers."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "EmitOverflowCommand[\s\S]*?if\s*\(kind == GEEffectCommandKind\.ActiveMutation\)[\s\S]*?NextFrameActiveMutationCommandLookup\[targetAsc\]\.Add\(new ActiveEffectNextFrameMutationCommandBuffer" `
    -Message "Generated overflow active mutation commands must use next-frame owner-local active mutation buffers."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "EmitOverflowCommand[\s\S]*?if\s*\(kind == GEEffectCommandKind\.ActiveMutation\)[\s\S]*?NextFrameActiveMutationCommandLookup\[targetAsc\]\.Add\(new ActiveEffectNextFrameMutationCommandBuffer" `
    -Message "CodeGen template must keep overflow active mutation commands on next-frame owner-local active mutation buffers."
Assert-FileContains `
    -Path $generatedActiveEffectPath `
    -Pattern "EmitOverflowCommand[\s\S]*?NextFrameInstantCommandLookup\[instantTargetAsc\]\.Add\(new OwnerLocalInstantNextFrameCommandBuffer" `
    -Message "Generated overflow instant commands must use next-frame owner-local instant buffers."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "EmitOverflowCommand[\s\S]*?NextFrameInstantCommandLookup\[instantTargetAsc\]\.Add\(new OwnerLocalInstantNextFrameCommandBuffer" `
    -Message "CodeGen template must keep overflow instant commands on next-frame owner-local instant buffers."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "EmitOverflowCommand[\s\S]*?EffectCommandSpecStream\.AppendPreparedCommand" `
    -Message "Generated overflow command emission must not fall back to the singleton command/spec stream."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "EmitOverflowCommand[\s\S]*?EffectCommandSpecStream\.AppendPreparedCommand" `
    -Message "CodeGen template must not regenerate singleton command/spec stream fallback for overflow commands."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectMutationChunkApplyJob\s*:\s*IJobChunk[\s\S]{0,3000}\|\| !CommandSetByCallerLookup\.HasBuffer\(StreamEntity\)" `
    -Message "Generated active mutation apply must not require singleton stream set-by-caller buffers before processing owner-local commands."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "GEActiveEffectMutationChunkApplyJob\s*:\s*IJobChunk[\s\S]{0,3000}\|\| !CommandSetByCallerLookup\.HasBuffer\(StreamEntity\)" `
    -Message "CodeGen template must not regenerate stream set-by-caller as an active mutation apply precondition."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectMutationChunkApplyJob\s*:\s*IJobChunk[\s\S]{0,5000}var setByCallerValues\s*=\s*CommandSetByCallerLookup\[StreamEntity\]" `
    -Message "Generated active mutation apply must not read active mutation set-by-caller input from the singleton stream buffer."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "GEActiveEffectMutationChunkApplyJob\s*:\s*IJobChunk[\s\S]{0,5000}var setByCallerValues\s*=\s*CommandSetByCallerLookup\[StreamEntity\]" `
    -Message "CodeGen template must not regenerate singleton stream set-by-caller input reads for active mutation apply."
Assert-FileNotContains `
    -Path $streamPath `
    -Pattern "ActiveMutationCommandCursor" `
    -Message "EffectCommandStream must not retain the old active mutation singleton command cursor."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectMutationGatherJob\s*:\s*IJob" `
    -Message "Generated active mutation command source must not regress to singleton stream gather."
Assert-FileNotContains `
    -Path $codeGenTemplatePath `
    -Pattern "GEActiveEffectMutationGatherJob\s*:\s*IJob" `
    -Message "CodeGen template must not regenerate singleton stream active mutation gather."
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
    -Path $autoChessRuntimeAccessPath `
    -Pattern "internal static class AutoChessGasRuntimeAccess" `
    -Message "AutoChess runtime access capability owner must stay internal to the AutoChess adapter."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "TryResolveSessionWorld" `
    -Message "AutoChess runtime access must expose a session world capability."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "TryResolveDefinitionEntityManager" `
    -Message "AutoChess runtime access must expose a definition/catalog EntityManager capability."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "TryResolveBattleLifecycleEntityManager" `
    -Message "AutoChess runtime access must expose a battle lifecycle EntityManager capability."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "TryResolveDiagnosticsWorld" `
    -Message "AutoChess runtime access must expose a diagnostics world capability."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "TryResolveDiagnosticsGlobalTimer" `
    -Message "AutoChess runtime access must expose a diagnostics GlobalTimer capability."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "TryResolveDiagnosticsEventBus" `
    -Message "AutoChess runtime access must expose a diagnostics EventBus capability."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "TryResolveDiagnosticsEventLogSink" `
    -Message "AutoChess runtime access must expose a diagnostics EventLogSink capability."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "TryResolveDiagnosticsRuntimeDebugger" `
    -Message "AutoChess runtime access must expose a diagnostics RuntimeDebugger capability."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "TryCreateBattleUnitCommandPort" `
    -Message "AutoChess runtime access must expose battle unit command port capabilities."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "TryDrainRunnerJobs" `
    -Message "AutoChess runtime access must expose runner dependency drain capability."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "GASRuntimeShell\.TryResolveRuntimeWorld" `
    -Message "AutoChess runtime access must centralize RuntimeWorld resolution."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "GASRuntimeShell\.TryResolveRuntimeEntityManager" `
    -Message "AutoChess runtime access must centralize Runtime EntityManager resolution."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "GASRuntimeShell\.TryResolveGlobalTimer" `
    -Message "AutoChess runtime access must centralize GlobalTimer resolution."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "GASRuntimeShell\.TryResolveEventBus" `
    -Message "AutoChess runtime access must centralize EventBus resolution."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "GASRuntimeShell\.TryResolveEventLogSink" `
    -Message "AutoChess runtime access must centralize EventLogSink resolution."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "GASRuntimeShell\.TryResolveRuntimeDebugger" `
    -Message "AutoChess runtime access must centralize RuntimeDebugger resolution."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "GASRuntimeShell\.TryCreateASCCommandPort" `
    -Message "AutoChess runtime access must centralize ASCCommandPort creation."
Assert-FileContains `
    -Path $autoChessRuntimeAccessPath `
    -Pattern "GASRuntimeShell\.TryDrainRuntimeJobs" `
    -Message "AutoChess runtime access must centralize runner dependency drain."
foreach ($autoChessRuntimeAccessConsumerPath in @(
        $autoChessRuntimeHostPath,
        $autoChessCatalogSessionPath,
        $autoChessLifecyclePath,
        $autoChessObservationGatewayPath)) {
    Assert-FileNotContains `
        -Path $autoChessRuntimeAccessConsumerPath `
        -Pattern "GASRuntimeShell\.Try(Resolve|Get|Create|Drain|Complete)" `
        -Message "AutoChess runtime access consumers must not call raw GASRuntimeShell Try* seams directly: $autoChessRuntimeAccessConsumerPath"
}
Assert-FileNotContains `
    -Path $autoChessRuntimeHostPath `
    -Pattern "TryResolveRuntimeEntityManager" `
    -Message "AutoChess runtime host must not resolve EntityManager for definition catalog lifetime; AutoChessGasCatalogSession owns that capability."
Assert-FileContains `
    -Path $autoChessRuntimeHostPath `
    -Pattern "AutoChessGasCatalogSession\.TryInstall\(\)" `
    -Message "AutoChess runtime host must install catalog lifetime through the catalog session capability."
Assert-FileContains `
    -Path $autoChessRuntimeHostPath `
    -Pattern "AutoChessGasCatalogSession\.Uninstall\(\)" `
    -Message "AutoChess runtime host must uninstall catalog lifetime through the catalog session capability."
Assert-FileContains `
    -Path $autoChessCatalogSessionPath `
    -Pattern "TryInstall\(\)[\s\S]*?AutoChessGasRuntimeAccess\.TryResolveDefinitionEntityManager" `
    -Message "AutoChess catalog session must resolve definition/catalog lifetime through AutoChessGasRuntimeAccess."
Assert-FileNotContains `
    -Path $autoChessCatalogSessionPath `
    -Pattern "public\s+static\s+\w+\s+\w+\s*\(\s*EntityManager" `
    -Message "AutoChess catalog session public API must not expose EntityManager parameters."
Assert-FileContains `
    -Path $autoChessDefinitionCatalogBuilderPath `
    -Pattern "internal static class AutoChessBattleDefinitionCatalogBuilder" `
    -Message "AutoChess generated catalog installer must stay an internal implementation detail."
Assert-FileNotContains `
    -Path $autoChessDefinitionCatalogBuilderPath `
    -Pattern "public static class AutoChessBattleDefinitionCatalogBuilder" `
    -Message "AutoChess generated catalog installer must not be a public API seam."
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
Assert-FileContains `
    -Path $autoChessDriverPath `
    -Pattern "AutoChessBattleDriverOwnerSnapshot" `
    -Message "AutoChess driver runtime store must expose a raw-Entity-free owner snapshot."
Assert-FileContains `
    -Path $autoChessDriverPath `
    -Pattern "CreateOwnerSnapshot[\s\S]*?AutoChessGasBattleDriverHandle handle" `
    -Message "AutoChess driver runtime store must publish owner evidence through the opaque driver handle."
Assert-FileNotContains `
    -Path $autoChessDriverPath `
    -Pattern "TryGetEntityForAdapter|public\s+static\s+Entity\s+|internal\s+static\s+Entity\s+" `
    -Message "AutoChess driver runtime store must not expose raw driver Entity through public or adapter APIs."
Assert-FileContains `
    -Path $autoChessSessionPath `
    -Pattern "GetDriverOwnerSnapshot" `
    -Message "AutoChess battle session must capture driver owner evidence before closing the driver."
Assert-FileContains `
    -Path $autoChessResultBuilderPath `
    -Pattern "driverOwnerSnapshot" `
    -Message "AutoChess battle result builder must carry driver owner evidence into the validation result."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "driverOwnerHandleMatched" `
    -Message "AutoChess validation evidence must report driver owner handle matching."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "driverStructuralCreates" `
    -Message "AutoChess validation evidence must report driver structural owner creation count."
Assert-FileContains `
    -Path $autoChessValidationReportPath `
    -Pattern "driverAdapterRawEntity=false" `
    -Message "AutoChess validation evidence must keep raw driver Entity hidden from adapter APIs."
Assert-FileContains `
    -Path $autoChessObservationGatewayPath `
    -Pattern 'RecordSystemTimingAggregate[\s\S]*?"OwnerSplit"[\s\S]*?"CoreRuntimeOwner"' `
    -Message "AutoChess Runtime Debugger timing evidence must publish core runtime owner split, not only physical group timing."
Assert-FileContains `
    -Path $autoChessObservationGatewayPath `
    -Pattern 'RecordSystemTimingAggregate[\s\S]*?"OwnerSplit"[\s\S]*?"BoundaryOwner"' `
    -Message "AutoChess Runtime Debugger timing evidence must publish boundary owner split."
Assert-FileContains `
    -Path $autoChessObservationGatewayPath `
    -Pattern 'RecordSystemTimingAggregate[\s\S]*?"OwnerSplit"[\s\S]*?"RunnerOwner"' `
    -Message "AutoChess Runtime Debugger timing evidence must publish runner/dependency-drain owner split."

Write-Host "GAS Runtime Core boundary check passed: no AutoChess references under Assets/GAS/Runtime."
Write-Host "GAS Runtime Core pending attribute delta contract passed: owner-local apply, stream migration fallback retired, and debugger counters are wired."
Write-Host "GAS Runtime Core global timer contract passed: registered/cache owner lookup is wired and singleton fallback queries are blocked."
Write-Host "GAS Runtime Core active effect global index contract passed: registered/cache owner lookup is wired and singleton fallback queries are blocked."
Write-Host "GAS Runtime Core debugger singleton contract passed: registered/cache owner lookup is wired and singleton fallback queries are blocked."
Write-Host "GAS Runtime Core stream writer contract passed: runtime helpers resolve stream owners explicitly before writing commands or facts."
Write-Host "GAS Runtime Core execution output fact contract passed: NativeStream collection and deterministic merge replaced structural ECB singleton append."
Write-Host "GAS Runtime Core active mutation contract passed: owner-local command collect + ASC chunk-local apply path is wired."
Write-Host "GAS Runtime Core active mutation set-by-caller contract passed: owner-local payload projection and frame-local apply inputs are wired."
Write-Host "GAS Runtime Core active mutation SourceAttribute contract passed: read-only snapshot lane feeds chunk-local apply."
Write-Host "GAS Runtime Core owner-local fact lane contract passed: Attribute facts use ASC-local carrier and debugger-visible flush counters."
Write-Host "GAS Runtime Core AttributeDelta owner-local fact projection contract passed: generated instant and execution output no longer write stream deltas."
Write-Host "AutoChess R1/R6 snapshot contract passed: unit result snapshots are projected from structured boundary evidence, not live ASCReadModel."
Write-Host "AutoChess R1/R6 runtime access capability contract passed: raw GASRuntimeShell ECS seams are centralized behind AutoChessGasRuntimeAccess."
Write-Host "AutoChess R6 driver owner contract passed: driver owner snapshot is exposed without raw Entity adapter APIs."
Write-Host "AutoChess R6/R8 timing owner split contract passed: Runtime Debugger publishes core, boundary, and runner timing owners."
