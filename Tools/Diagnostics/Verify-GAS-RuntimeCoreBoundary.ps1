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
$scheduleContractPath = Join-Path $runtimePath "System\SystemGroup\GASSystemScheduleContract.cs"
$streamOwnerContractPath = Join-Path $runtimePath "System\SystemGroup\GASRuntimeStreamOwnerContract.cs"
$globalTimerPath = Join-Path $runtimePath "System\Core\GASGlobalTimerSystem.cs"
$debuggerPath = Join-Path $runtimePath "Debugger\GasRuntimeDebugger.cs"
$generatedActiveEffectPath = Join-Path $ProjectPath "Assets\GAS\Generated\CodeGen\Runtime\RuntimeActiveEffect.gen.cs"
$codeGenTemplatePath = Join-Path $ProjectPath "Assets\GAS\Editor\CodeGen\Phases\GasGlueCodeGenPhases.cs"
$autoChessDamagePath = Join-Path $ProjectPath "Assets\AutoChessDemo\Battle\Ecs\AutoChessExecuteDamageCalculationSystem.cs"

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
    -Message "GASRuntimeFrameContext must prefer the registered GlobalTimer owner before fallback singleton queries."
Assert-FileContains `
    -Path $streamPath `
    -Pattern "ResetFrameLocalCounters\(ref stream\)" `
    -Message "GEEffectCommandSpecStream must reset frame-local debugger counters during frame prepare."
Assert-FileContains `
    -Path $streamPhasePath `
    -Pattern "UpdateAfter\(typeof\(GASAttributeModifierDeltaApplySystem\)\)" `
    -Message "GameplayFactProjectionSystem must run after core pending attribute delta apply."
Assert-FileContains `
    -Path $scheduleContractPath `
    -Pattern "typeof\(GASAttributeModifierDeltaApplySystem\),\s*[\r\n\s]*EGasRuntimeCoreFramePhase\.DeltaApply" `
    -Message "GASSystemScheduleContract must classify GASAttributeModifierDeltaApplySystem as DeltaApply."
Assert-FileContains `
    -Path $streamOwnerContractPath `
    -Pattern "streamId == EGasRuntimeFrameStreamId\.AttributeDelta" `
    -Message "AttributeDelta stream owner plan must require owner-range/random-lookup evidence."
Assert-FileContains `
    -Path $debuggerPath `
    -Pattern "RuntimeCoreActiveMutationCommandCount \+= activeMutationCommandCount" `
    -Message "GasRuntimeDebugger snapshot counters must accumulate active mutation frame-local evidence."
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
    -Pattern "ActiveMutationCommands\.Length == 0" `
    -Message "Generated active mutation chunk apply must skip ASC chunk scans when there are no active mutation commands."
Assert-FileContains `
    -Path $codeGenTemplatePath `
    -Pattern "GEActiveEffectMutationChunkApplyJob\s*:\s*IJobChunk" `
    -Message "CodeGen template must keep active mutation apply on the chunk-local path."
Assert-FileNotContains `
    -Path $generatedActiveEffectPath `
    -Pattern "GEActiveEffectMutationApplyJob\s*:\s*IJob" `
    -Message "Generated active effect runtime must not regress to serial active mutation owner lookup apply."
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
    -Path $autoChessDamagePath `
    -Pattern "PendingOwnerLookup\.SetComponentEnabled\(targetAsc,\s*true\)" `
    -Message "AutoChess execution calculation must publish pending attribute deltas to the target ASC owner-local lane."
Assert-FileNotContains `
    -Path $autoChessDamagePath `
    -Pattern "DeltaLookup\[StreamEntity\]" `
    -Message "AutoChess execution calculation must not write pending attribute deltas to the stream migration carrier."

Write-Host "GAS Runtime Core boundary check passed: no AutoChess references under Assets/GAS/Runtime."
Write-Host "GAS Runtime Core pending attribute delta contract passed: owner-local apply, stream migration fallback retired, and debugger counters are wired."
Write-Host "GAS Runtime Core active mutation contract passed: generated gather + ASC chunk-local apply path is wired."
