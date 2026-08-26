param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

function Get-ProjectPath {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    return Join-Path $ProjectPath ($RelativePath -replace '/', '\')
}

function Assert-Exists {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $path = Get-ProjectPath $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Runtime v1 required path is missing: $RelativePath"
    }
}

function Assert-NotExists {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $path = Get-ProjectPath $RelativePath
    if (Test-Path -LiteralPath $path) {
        throw "Legacy runtime path still exists: $RelativePath"
    }
}

function Get-CsFiles {
    param([Parameter(Mandatory = $true)][string]$RelativeRoot)

    $root = Get-ProjectPath $RelativeRoot
    if (-not (Test-Path -LiteralPath $root)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter "*.cs")
}

function Assert-NoForbiddenSymbol {
    param(
        [Parameter(Mandatory = $true)][string]$RelativeRoot,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    $files = Get-CsFiles $RelativeRoot
    if ($files.Count -eq 0) {
        return
    }

    $hits = @($files | Select-String -Pattern $Pattern)
    if ($hits.Count -gt 0) {
        $evidence = $hits | ForEach-Object {
            "{0}:{1}:{2}" -f $_.Path, $_.LineNumber, $_.Line.Trim()
        }
        throw "Forbidden legacy symbol '$Pattern' found:`n$($evidence -join "`n")"
    }
}

if (-not (Test-Path -LiteralPath (Get-ProjectPath "Assets/GAS/Runtime/V1"))) {
    throw "Runtime v1 root not found under project: $ProjectPath"
}

$requiredPaths = @(
    "Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs",
    "Assets/GAS/Runtime/V1/System/GasTickDag.cs",
    "Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainCoordinator.cs",
    "Assets/GAS/Runtime/V1/Boundary/GasCommandPort.cs",
    "Assets/GAS/Runtime/V1/Layout/GasRuntimeV1Archetypes.cs",
    "Assets/GAS/Runtime/V1/Layout/GasBoundaryLayout.cs",
    "Assets/GAS/Runtime/V1/Identity/OwnerIdentityHandles.cs",
    "Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs",
    "Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs",
    "Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs"
)

foreach ($path in $requiredPaths) {
    Assert-Exists $path
}

$deletedPaths = @(
    "Assets/GAS/Runtime/General/GASManager.cs",
    "Assets/GAS/Runtime/General/GASRuntimeShell.cs",
    "Assets/GAS/Runtime/AbilitySystem/AbilitySystemBinding.cs",
    "Assets/GAS/Runtime/AbilitySystem/ASCEntityFactory.cs",
    "Assets/GAS/Runtime/AbilitySystem/ASCCommandGateway.cs",
    "Assets/GAS/Runtime/Effect/GameplayEffectEntityFactory.cs",
    "Assets/GAS/Runtime/Effect/GameplayEffectConfigRegistry.cs",
    "Assets/GAS/Runtime/Effect/Component/Dynamic/ActiveEffectStore.cs",
    "Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs",
    "Assets/GAS/Runtime/Definition/GASRuntimeDefinitionResolver.cs",
    "Assets/GAS/Runtime/Definition/GASDefinitionCatalogRuntimeTypes.cs",
    "Assets/GAS/Runtime/Definition/GASDefinitionCatalogLookup.cs",
    "Assets/GAS/Runtime/Event/EventBusHelper.cs",
    "Assets/GAS/Runtime/Event/GameplayEventBusComponent.cs",
    "Assets/GAS/Runtime/System/SystemGroup/GASGroups.cs",
    "Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs",
    "Assets/GAS/Runtime/System/Core/GASGlobalTimerSystem.cs",
    "Assets/GAS/Runtime/System/ASCCommandBufferResolveSystem.cs",
    "Assets/GAS/Runtime/System/Ability/AbilityCommitSystem.cs",
    "Assets/GAS/Runtime/System/Effect/GEActiveEffectLifecycleSystems.cs",
    "Assets/GAS/Runtime/System/Event/GameplayEventBusClearSystem.cs",
    "Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs"
)

foreach ($path in $deletedPaths) {
    Assert-NotExists $path
}

$forbiddenSymbols = @(
    "\bGASManager\b",
    "\bGASRuntimeShell\b",
    "\bAbilitySystemBinding\b",
    "\bGameplayEventBusComponent\b",
    "\bGameplayEventBuffer\b",
    "\bGEEffectCommandStreamComponent\b",
    "\bGEEffectCommandBuffer\b",
    "\bActiveEffectGlobalIndex\b",
    "\bGameplayEffectEntityFactory\b",
    "\bGASRuntimeDebugger\b",
    "\bGASFramePrepareSystemGroup\b",
    "\bGASCommandResolveSystemGroup\b",
    "\bGASCoreSimulationSystemGroup\b",
    "\bGASStructuralCommitSystemGroup\b",
    "\bGASBoundaryProjectionSystemGroup\b"
)

foreach ($pattern in $forbiddenSymbols) {
    Assert-NoForbiddenSymbol "Assets/GAS/Runtime" $pattern
}

Write-Output "Runtime v1 boundary verification passed."
