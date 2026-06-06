param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot

try {
    $defaultChainPaths = @(
        "Assets/GAS/Editor/GASCenterUIToolkit",
        "Assets/GAS/Editor/GameplayAbilitySystem/GASSettingAsset.cs",
        "Assets/GAS/Editor/GameplayAbilitySystem/GASWatcher.cs",
        "Assets/GAS/Editor/General/ScriptableObjectCreator.cs",
        "Assets/GAS/Editor/Ability/AbilityTimelineEditor/Track/TimelineActionClipTrack"
    )

    $forbiddenDefaultPattern = "OnGUI|OnInspectorGUI|EditorGUILayout|EditorGUI\.|GUILayout|GenericMenu|IMGUIContainer|using Sirenix|Sirenix\.|Odin|ValueDropdownItem|SdfIconType"
    $legacyPattern = "using Sirenix|Sirenix\.|OdinEditorWindow|OdinMenuEditorWindow|OdinSelector|OdinEditor|OdinMenuTree|ValueDropdownItem|SdfIconType"

    Write-Host "[EX-GAS] Checking default UI Toolkit editor chain..."
    $defaultHits = & rg -n $forbiddenDefaultPattern @defaultChainPaths -g "*.cs"
    if ($LASTEXITCODE -eq 0) {
        Write-Error "Default editor chain still references Odin/IMGUI:`n$defaultHits"
    } elseif ($LASTEXITCODE -ne 1) {
        throw "rg failed while scanning default editor chain."
    }

    Write-Host "[EX-GAS] Checking legacy Odin gate..."
    $legacyHits = & rg -n $legacyPattern "Assets/GAS/Editor/GASCenterEditor" -g "*.cs"
    if ($LASTEXITCODE -eq 0) {
        $legacyFiles = $legacyHits |
            ForEach-Object { ($_ -split ":", 2)[0] } |
            Sort-Object -Unique

        foreach ($file in $legacyFiles) {
            $content = Get-Content -Raw -LiteralPath $file
            if ($content -notmatch "#if\s+EX_GAS_ENABLE_ODIN_LEGACY_EDITOR") {
                Write-Error "Odin legacy hit is not guarded by EX_GAS_ENABLE_ODIN_LEGACY_EDITOR: $file"
            }
        }
    } elseif ($LASTEXITCODE -ne 1) {
        throw "rg failed while scanning legacy editor chain."
    }

    Write-Host "[EX-GAS] Checking UI Toolkit editable GAS pages..."
    $windowPath = "Assets/GAS/Editor/GASCenterUIToolkit/GASCenterUIToolkitWindow.cs"
    $windowContent = Get-Content -Raw -LiteralPath $windowPath
    $requiredEditablePages = @(
        "GASCenterAbilityPage",
        "GASCenterEffectPage",
        "GASCenterAscPage",
        "GASCenterCuePage"
    )

    foreach ($pageType in $requiredEditablePages) {
        if ($windowContent -notmatch "new\s+$pageType\s*\(") {
            Write-Error "Default GAS Center does not register editable UI Toolkit page: $pageType"
        }
    }

    if ($windowContent -match 'new\s+GASCenterJsonTablePage\s*\(\s*"ability"') {
        Write-Error "Ability default page regressed to read-only JSON browser."
    }

    if ($windowContent -match 'new\s+GASCenterJsonTablePage\s*\(\s*"effect"') {
        Write-Error "Effect default page regressed to read-only JSON browser."
    }

    if (-not $SkipBuild) {
        Write-Host "[EX-GAS] Building com.exhard.exgas.editor.csproj..."
        dotnet build com.exhard.exgas.editor.csproj --no-restore /p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE."
        }
    }

    Write-Host "[EX-GAS] Odin exit verification passed."
}
finally {
    Pop-Location
}
