[CmdletBinding()]
param(
    [string]$UnityPath = '',
    [string]$RepositoryRoot = '',
    [switch]$RequireProductionScaffold
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Get-D1RouteScaffold.ps1')

# 断言条件成立，并以异常阻断任何静态假绿。
function Assert-D1Scaffold
{
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) { throw $Message }
}

# 断言两个字符串集合在 ordinal 语义下完全相同。
function Assert-D1ExactSet
{
    param(
        [Parameter(Mandatory = $true)][string[]]$Actual,
        [Parameter(Mandatory = $true)][string[]]$Expected,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $actualSorted = [string[]]$Actual.Clone()
    $expectedSorted = [string[]]$Expected.Clone()
    [Array]::Sort($actualSorted, [StringComparer]::Ordinal)
    [Array]::Sort($expectedSorted, [StringComparer]::Ordinal)
    Assert-D1Scaffold ($actualSorted.Count -eq $expectedSorted.Count) "$Context count mismatch."
    for ($index = 0; $index -lt $actualSorted.Count; $index++)
    {
        Assert-D1Scaffold ($actualSorted[$index] -ceq $expectedSorted[$index]) `
            "$Context mismatch at $index."
    }
}

# 逐字节比较 staged fixture 与 production；pre-J 固定 6 present + 8 missing，post-J 固定 14 exact。
function Test-D1ProductionScaffoldState
{
    param(
        [Parameter(Mandatory = $true)][string]$ResolvedRepositoryRoot,
        [Parameter(Mandatory = $true)][string]$FixtureRoot,
        [Parameter(Mandatory = $true)]$Contract,
        [Parameter(Mandatory = $true)][bool]$RequireFull
    )

    $rows = [Collections.Generic.List[object]]::new()
    foreach ($relative in @($Contract.Files | ForEach-Object { [string]$_ }))
    {
        $staged = Resolve-D1ExactRelativeFile $FixtureRoot $relative
        $candidate = Join-Path $ResolvedRepositoryRoot $relative
        if (-not [IO.File]::Exists($candidate) -and -not [IO.Directory]::Exists($candidate))
        {
            $rows.Add([pscustomobject][ordered]@{
                Path = $relative; State = 'Missing'; StagedSha256 = Get-D1BytesSha256 ([IO.File]::ReadAllBytes($staged))
                ProductionSha256 = $null
            })
            continue
        }
        $production = Resolve-D1ExactRelativeFile $ResolvedRepositoryRoot $relative
        if ((Get-D1FileLinkCount $production) -ne 1) { throw "Production scaffold file is hard linked: $relative" }
        $stagedBytes = [IO.File]::ReadAllBytes($staged)
        $productionBytes = [IO.File]::ReadAllBytes($production)
        $stagedSha = Get-D1BytesSha256 $stagedBytes
        $productionSha = Get-D1BytesSha256 $productionBytes
        if ($stagedBytes.LongLength -ne $productionBytes.LongLength -or $stagedSha -cne $productionSha)
        {
            throw "Production scaffold bytes drifted from staged fixture: $relative"
        }
        $rows.Add([pscustomobject][ordered]@{
            Path = $relative; State = 'Exact'; StagedSha256 = $stagedSha
            ProductionSha256 = $productionSha
        })
    }

    $existing = @($Contract.ProductionExistingFiles | ForEach-Object { [string]$_ })
    $stagedNew = @($Contract.StagedNewFiles | ForEach-Object { [string]$_ })
    foreach ($row in $rows)
    {
        $expectedState = if ($RequireFull -or $row.Path -cin $existing) { 'Exact' } else { 'Missing' }
        if ([string]$row.State -cne $expectedState)
        {
            throw "Production scaffold phase state mismatch for $($row.Path): expected $expectedState."
        }
    }
    return [pscustomobject][ordered]@{
        Phase = $(if ($RequireFull) { 'PostMigration' } else { 'PreMigration' })
        ExistingExact = @($rows | Where-Object { $_.Path -cin $existing -and $_.State -ceq 'Exact' }).Count
        ProductionExact = @($rows | Where-Object State -ceq 'Exact').Count
        ProductionMissing = @($rows | Where-Object State -ceq 'Missing').Count
        StagedNew = $stagedNew.Count; FullExact = @($rows | Where-Object State -cne 'Exact').Count -eq 0
        Entries = @($rows)
    }
}

# 解析 PowerShell AST，拒绝 runner/module 中的语法错误。
function Test-D1PowerShellAst
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $tokens = $null
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile(
        $Path, [ref]$tokens, [ref]$errors)
    if (@($errors).Count -ne 0)
    {
        throw "PowerShell AST failed for $Path`: $($errors -join ' | ')"
    }
}

# 从显式 Unity.exe 或项目锁定版本注册表解析 Editor/Data，只做离线引用编译。
function Resolve-D1UnityDataPath
{
    param([string]$RequestedUnityPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedUnityPath))
    {
        $resolved = [IO.Path]::GetFullPath($RequestedUnityPath)
        if (-not [IO.File]::Exists($resolved)) { throw "Unity executable is missing: $resolved" }
        return Join-Path ([IO.Path]::GetDirectoryName($resolved)) 'Data'
    }

    $registry = 'HKLM:\SOFTWARE\Unity Technologies\Installer\Unity 6000.3.14f1'
    if (-not (Test-Path -LiteralPath $registry)) { return $null }
    $location = (Get-ItemProperty -LiteralPath $registry -Name 'Location x64').'Location x64'
    return Join-Path $location 'Editor/Data'
}

# 使用 Unity 托管引用离线编译 probe；本函数绝不启动 Unity Editor。
function Build-D1UnityProbeStatic
{
    param(
        [Parameter(Mandatory = $true)][string]$OutputRoot,
        [string]$RequestedUnityPath
    )

    $data = Resolve-D1UnityDataPath $RequestedUnityPath
    if ([string]::IsNullOrWhiteSpace($data))
    {
        return [pscustomobject]@{ Evaluated = $false; Reason = 'UnityReferenceRootUnavailable' }
    }

    $compiler = Join-Path $data 'DotNetSdkRoslyn/csc.dll'
    $referenceRoot = Join-Path $data 'NetStandard/ref/2.1.0'
    if (-not [IO.File]::Exists($compiler) -or -not [IO.Directory]::Exists($referenceRoot))
    {
        throw "Unity static compiler references are incomplete: $data"
    }

    $references = @(Get-ChildItem -LiteralPath $referenceRoot -Filter '*.dll' -File |
        ForEach-Object { '/reference:' + $_.FullName })
    $references += @(
        'Managed/UnityEngine/UnityEngine.dll',
        'Managed/UnityEngine/UnityEngine.CoreModule.dll',
        'Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll',
        'Managed/UnityEditor.dll'
    ) | ForEach-Object { '/reference:' + (Join-Path $data $_) }
    $source = Join-Path $PSScriptRoot `
        'Fixture~/Assets/D1E1Harness/Editor/D1E1UnityProbe.cs'
    $output = Join-Path $OutputRoot 'D1E1UnityProbe.dll'
    $compilerOutput = @(& dotnet $compiler /nologo /noconfig /nostdlib+ /target:library `
        /langversion:9.0 "/out:$output" @references $source 2>&1)
    if ($LASTEXITCODE -ne 0 -or -not [IO.File]::Exists($output))
    {
        throw "D1 E1 Unity probe static compile failed: $($compilerOutput -join ' | ')"
    }

    return [pscustomobject]@{
        Evaluated = $true
        Sha256 = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
        Length = [long]([IO.FileInfo]::new($output)).Length
    }
}

$contractPath = Join-Path $PSScriptRoot 'D1Scaffold.contract.json'
$fixtureRoot = Join-Path $PSScriptRoot 'Fixture~'
$contract = Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8 | ConvertFrom-Json
$expectedFiles = @(
    'Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll.meta',
    'Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile.meta',
    'Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef',
    'Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef.meta',
    'Assets/GAS/Generated/CodeGen/Runtime/GasCodeGenSourceGeneratorRequired.cs',
    'Assets/GAS/Generated/CodeGen/Runtime/GasCodeGenSourceGeneratorRequired.cs.meta',
    'Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef',
    'Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef.meta',
    'Assets/GAS/Generated/CodeGen/Editor/GasCodeGenSourceGeneratorRequired.cs',
    'Assets/GAS/Generated/CodeGen/Editor/GasCodeGenSourceGeneratorRequired.cs.meta',
    'Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef',
    'Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef.meta',
    'Assets/AutoChessDemo/Generated/GasCodeGenSourceGeneratorRequired.cs',
    'Assets/AutoChessDemo/Generated/GasCodeGenSourceGeneratorRequired.cs.meta'
)
Assert-D1Scaffold ([string]$contract.Schema -ceq 'EX-GAS-D1-RouteScaffoldContract-v1') `
    'Scaffold contract schema mismatch.'
Assert-D1ExactSet @($contract.Files | ForEach-Object { [string]$_ }) $expectedFiles `
    'Scaffold exact paths'
Assert-D1ExactSet @($contract.TargetAssemblies | ForEach-Object { [string]$_ }) @(
    'com.exhard.exgas.generated.runtime',
    'com.exhard.exgas.generated.editor',
    'com.exhard.exgas.autochessdemo'
) 'Target assemblies'
Assert-D1ExactSet @($contract.ProductionExistingFiles | ForEach-Object { [string]$_ }) @(
    $expectedFiles[2], $expectedFiles[3], $expectedFiles[6], $expectedFiles[7],
    $expectedFiles[10], $expectedFiles[11]
) 'Pre-J production existing scaffold'
Assert-D1ExactSet @($contract.StagedNewFiles | ForEach-Object { [string]$_ }) @(
    $expectedFiles[0], $expectedFiles[1], $expectedFiles[4], $expectedFiles[5],
    $expectedFiles[8], $expectedFiles[9], $expectedFiles[12], $expectedFiles[13]
) 'Pre-J staged new scaffold'

$snapshot = Get-D1RouteScaffoldSnapshot $fixtureRoot $contractPath
Assert-D1Scaffold ($snapshot.EntryCount -eq 14) 'Scaffold snapshot must contain 14 entries.'
Assert-D1Scaffold (
    [string]$snapshot.RouteScaffoldSha256 -ceq [string]$contract.StagedRouteScaffoldSha256) `
    'Frozen staged RouteScaffoldSha256 drifted.'
$resolvedRepository = if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../..'))
} else { [IO.Path]::GetFullPath($RepositoryRoot) }
$productionState = Test-D1ProductionScaffoldState $resolvedRepository $fixtureRoot `
    $contract ([bool]$RequireProductionScaffold)
$productionHash = $null
if ([bool]$RequireProductionScaffold)
{
    $productionSnapshot = Get-D1RouteScaffoldSnapshot $resolvedRepository $contractPath
    Assert-D1Scaffold (
        [string]$productionSnapshot.RouteScaffoldSha256 -ceq [string]$snapshot.RouteScaffoldSha256) `
        'Post-J production route scaffold aggregate differs from staged bytes.'
    $productionHash = [string]$productionSnapshot.RouteScaffoldSha256
}

$analyzerMeta = Get-Content -LiteralPath (Join-Path $fixtureRoot $expectedFiles[0]) -Raw
Assert-D1Scaffold ($analyzerMeta.Contains("labels:`n- RoslynAnalyzer", [StringComparison]::Ordinal)) `
    'Analyzer meta must carry the RoslynAnalyzer label.'
$selectorMeta = Get-Content -LiteralPath (Join-Path $fixtureRoot $expectedFiles[1]) -Raw
Assert-D1Scaffold ($selectorMeta.Contains('DefaultImporter:', [StringComparison]::Ordinal)) `
    'Selector meta must use DefaultImporter.'

$runtimeAsmdef = Get-Content -LiteralPath (Join-Path $fixtureRoot $expectedFiles[2]) -Raw |
    ConvertFrom-Json
$editorAsmdef = Get-Content -LiteralPath (Join-Path $fixtureRoot $expectedFiles[6]) -Raw |
    ConvertFrom-Json
$autoChessAsmdef = Get-Content -LiteralPath (Join-Path $fixtureRoot $expectedFiles[10]) -Raw |
    ConvertFrom-Json
Assert-D1Scaffold ([string]$runtimeAsmdef.name -ceq 'com.exhard.exgas.generated.runtime') `
    'Runtime asmdef name drifted.'
Assert-D1Scaffold ([string]$editorAsmdef.name -ceq 'com.exhard.exgas.generated.editor') `
    'Editor asmdef name drifted.'
Assert-D1Scaffold ([string]$autoChessAsmdef.name -ceq 'com.exhard.exgas.autochessdemo') `
    'AutoChess owner assembly drifted.'
Assert-D1Scaffold (@($editorAsmdef.includePlatforms).Count -eq 1 -and
    [string]$editorAsmdef.includePlatforms[0] -ceq 'Editor') 'Editor platform constraint drifted.'

foreach ($anchorRelative in @($expectedFiles | Where-Object { $_.EndsWith('.cs') }))
{
    $anchor = Get-Content -LiteralPath (Join-Path $fixtureRoot $anchorRelative) -Raw
    foreach ($constant in @(
        'TargetAssembly', 'SelectorSha256', 'ArtifactManifestHash',
        'SourceArtifactInventoryHash'))
    {
        Assert-D1Scaffold ($anchor.Contains(
            "GasCodeGenSourceGeneratorMarker.$constant", [StringComparison]::Ordinal)) `
            "Hard-guard anchor does not bind $constant`: $anchorRelative"
    }
}

$probePath = Join-Path $fixtureRoot 'Assets/D1E1Harness/Editor/D1E1UnityProbe.cs'
$probeText = Get-Content -LiteralPath $probePath -Raw
foreach ($token in @(
    'FileMode.CreateNew', 'stream.Flush(true)', 'RunId', 'CaseId', 'InvocationId',
    'LoadedLocation', 'LoadedMvid', 'RoslynAdditionalFilePaths',
    'GasCodeGenSourceGeneratorMarker'))
{
    Assert-D1Scaffold ($probeText.Contains($token, [StringComparison]::Ordinal)) `
        "Unity probe is missing required token: $token"
}

Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' -File |
    ForEach-Object { Test-D1PowerShellAst $_.FullName }

$staticRoot = Join-Path ([IO.Path]::GetTempPath()) ('D1E1-static-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($staticRoot) | Out-Null
try
{
    $probeBuild = Build-D1UnityProbeStatic $staticRoot $UnityPath
}
finally
{
    $resolvedStatic = [IO.Path]::GetFullPath($staticRoot)
    $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolvedStatic.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([IO.Path]::GetFileName($resolvedStatic)).StartsWith('D1E1-static-', [StringComparison]::Ordinal))
    {
        throw "Unsafe static cleanup root: $resolvedStatic"
    }
    if ([IO.Directory]::Exists($resolvedStatic))
    {
        Remove-Item -LiteralPath $resolvedStatic -Recurse -Force
    }
}

[pscustomobject][ordered]@{
    Schema = 'EX-GAS-D1-ScaffoldStatic-v1'
    Passed = $true
    StagedRouteScaffoldSha256 = $snapshot.RouteScaffoldSha256
    ProductionRouteScaffoldSha256 = $productionHash
    ProductionState = $productionState
    EntryCount = $snapshot.EntryCount
    PowerShellAstPassed = $true
    UnityProbeStaticBuild = $probeBuild
    UnityStarted = $false
} | ConvertTo-Json -Depth 8
