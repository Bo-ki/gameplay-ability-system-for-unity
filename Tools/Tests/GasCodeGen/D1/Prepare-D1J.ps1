#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RepositoryRoot,
    [string]$RunId = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:Utf8NoBom = [Text.UTF8Encoding]::new($false)

# 将目录的普通内容复制到 fresh 目标，避免 wildcard 合并既有目录。
function Copy-D1JTree
{
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if (-not [IO.Directory]::Exists($Source) -or [IO.Directory]::Exists($Destination) -or
        [IO.File]::Exists($Destination))
    {
        throw "D1-J tree copy requires an existing source and fresh destination: $Destination"
    }
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($entry in @(Get-ChildItem -LiteralPath $Source -Force))
    {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "D1-J refuses a reparse-point input: $($entry.FullName)"
        }
        Copy-Item -LiteralPath $entry.FullName -Destination $Destination -Recurse
    }
}

# 调用 adapter 并将 stdout/stderr 固化到输入证据；非零立即阻断。
function Invoke-D1JAdapter
{
    param(
        [Parameter(Mandatory = $true)][string]$AdapterPath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][string]$Label
    )

    & $AdapterPath @Arguments 2>&1 | Tee-Object -LiteralPath $LogPath
    if ($LASTEXITCODE -ne 0) { throw "$Label failed with exit code $LASTEXITCODE." }
}

# 将 staged analyzer、14 项 scaffold 与实际 production selector 冻结为迁移唯一输入。
function Copy-D1JProductionPayload
{
    param(
        [Parameter(Mandatory = $true)][string]$StageRoot,
        [Parameter(Mandatory = $true)][string]$PayloadRoot,
        [Parameter(Mandatory = $true)]$ScaffoldContract,
        [Parameter(Mandatory = $true)][string]$ProductionSelectorPath
    )

    foreach ($relativePath in @($ScaffoldContract.Files | ForEach-Object { [string]$_ }))
    {
        $source = Join-Path $StageRoot $relativePath
        $destination = Join-Path $PayloadRoot $relativePath
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }
    foreach ($relativePath in @(
        'Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll',
        'Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile'))
    {
        $source = if ($relativePath.EndsWith('.additionalfile', [StringComparison]::Ordinal)) {
            $ProductionSelectorPath
        } else { Join-Path $StageRoot $relativePath }
        $destination = Join-Path $PayloadRoot $relativePath
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }
}

# 只删除本脚本创建且 sentinel/run id/父目录/名称全部匹配的 OS 临时根。
function Remove-D1JOwnedRoot
{
    param(
        [Parameter(Mandatory = $true)][string]$StageRoot,
        [Parameter(Mandatory = $true)][string]$ExpectedRunId
    )

    $resolved = [IO.Path]::GetFullPath($StageRoot)
    $temp = ([IO.Path]::GetFullPath([IO.Path]::GetTempPath())).TrimEnd(
        [IO.Path]::DirectorySeparatorChar)
    $sentinelPath = Join-Path $resolved '.d1j-owner.json'
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd([IO.Path]::DirectorySeparatorChar) -cne $temp -or
        [IO.Path]::GetFileName($resolved) -cnotmatch '^gas-codegen-d1-j-[0-9a-f]{12}$' -or
        -not [IO.File]::Exists($sentinelPath))
    {
        throw "Unsafe D1-J cleanup target: $resolved"
    }
    $sentinel = [IO.File]::ReadAllText($sentinelPath, $script:Utf8NoBom) | ConvertFrom-Json
    if ([string]$sentinel.Schema -cne 'EX-GAS-D1-J-OwnedRoot-v1' -or
        [string]$sentinel.RunId -cne $ExpectedRunId)
    {
        throw "D1-J cleanup sentinel mismatch: $resolved"
    }
    [IO.Directory]::Delete($resolved, $true)
}

# 创建 fresh staged project、生成实际 selector 并冻结下一门所需的所有 bytes。
function Invoke-D1JPreparation
{
    param(
        [Parameter(Mandatory = $true)][string]$ResolvedRepository,
        [Parameter(Mandatory = $true)][string]$ResolvedRunId
    )

    $suffix = $ResolvedRunId.Substring($ResolvedRunId.Length - 12)
    $runRoot = Join-Path $ResolvedRepository "TestResults/GasCodeGen/D1/$ResolvedRunId"
    if ([IO.Directory]::Exists($runRoot) -or [IO.File]::Exists($runRoot))
    {
        throw "D1-J run root must be fresh: $runRoot"
    }
    $inputs = Join-Path $runRoot 'inputs'
    [IO.Directory]::CreateDirectory($inputs) | Out-Null
    $stage = Join-Path ([IO.Path]::GetTempPath()) ("gas-codegen-d1-j-$suffix")
    if ([IO.Directory]::Exists($stage) -or [IO.File]::Exists($stage))
    {
        throw "D1-J stage root must be fresh: $stage"
    }
    $template = Join-Path $ResolvedRepository 'Tools/Tests/GasCodeGen/D1/Scaffold/Fixture~'
    Copy-D1JTree $template $stage
    $sentinel = [ordered]@{ Schema = 'EX-GAS-D1-J-OwnedRoot-v1'; RunId = $ResolvedRunId }
    [IO.File]::WriteAllText(
        (Join-Path $stage '.d1j-owner.json'),
        (($sentinel | ConvertTo-Json -Compress) + "`n"),
        $script:Utf8NoBom)
    $analyzerSource = Join-Path $ResolvedRepository `
        'Tools/GasCodeGenSourceGenerator/bin/Release/netstandard2.0/GasCodeGenSourceGenerator.dll'
    $analyzerTarget = Join-Path $stage `
        'Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll'
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($analyzerTarget)) | Out-Null
    Copy-Item -LiteralPath $analyzerSource -Destination $analyzerTarget

    Copy-D1JTree (Join-Path $ResolvedRepository 'Assets/DataGenerated/Luban/Json/GAS') `
        (Join-Path $stage 'Assets/DataGenerated/Luban/Json/GAS')
    $sidecar = 'EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/AutoChessDemo/autochess.sourcegen.json'
    $sidecarTarget = Join-Path $stage $sidecar
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($sidecarTarget)) | Out-Null
    Copy-Item -LiteralPath (Join-Path $ResolvedRepository $sidecar) -Destination $sidecarTarget

    $adapter = Join-Path $ResolvedRepository 'Tools/GasCodeGenCli/bin/Release/net472/GasCodeGenCli.exe'
    $selectorA = Join-Path $inputs 'selector-a-synthetic.additionalfile'
    $selectorSyntheticB = Join-Path $inputs 'selector-b-synthetic.additionalfile'
    Invoke-D1JAdapter $adapter @(
        '--mode', 'd1b-export-selectors', '--projectRoot', $stage,
        '--selectorAOutput', $selectorA, '--selectorBOutput', $selectorSyntheticB) `
        (Join-Path $inputs 'selector-export.log') 'Selector export'
    Invoke-D1JAdapter $adapter @('--mode', 'sourcegen-all', '--projectRoot', $stage) `
        (Join-Path $inputs 'production-candidate.log') 'Staged production candidate'

    $stageSelector = Join-Path $stage `
        'Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile'
    if (-not [IO.File]::Exists($stageSelector)) { throw 'Staged production selector is missing.' }
    $selectorB = Join-Path $inputs 'selector-b-production.additionalfile'
    Copy-Item -LiteralPath $stageSelector -Destination $selectorB
    $scaffoldContractPath = Join-Path $ResolvedRepository `
        'Tools/Tests/GasCodeGen/D1/Scaffold/D1Scaffold.contract.json'
    $scaffoldContract = Get-Content -LiteralPath $scaffoldContractPath -Raw -Encoding UTF8 |
        ConvertFrom-Json
    Copy-D1JProductionPayload $stage (Join-Path $inputs 'production-payload') `
        $scaffoldContract $selectorB
    Copy-D1JTree (Join-Path $stage 'ProjectSettings/GasCodeGen') `
        (Join-Path $inputs 'staged-control-evidence')

    . (Join-Path $ResolvedRepository 'Tools/Tests/GasCodeGen/D1/Scaffold/Get-D1RouteScaffold.ps1')
    $route = Get-D1RouteScaffoldSnapshot $stage $scaffoldContractPath
    $preparation = [ordered]@{
        Schema = 'EX-GAS-D1-J-Preparation-v1'; RunId = $ResolvedRunId; StageRemoved = $false
        AnalyzerSha256 = (Get-FileHash $analyzerSource -Algorithm SHA256).Hash.ToLowerInvariant()
        AdapterSha256 = (Get-FileHash $adapter -Algorithm SHA256).Hash.ToLowerInvariant()
        RouteScaffoldSha256 = [string]$route.RouteScaffoldSha256
        SelectorASha256 = (Get-FileHash $selectorA -Algorithm SHA256).Hash.ToLowerInvariant()
        SelectorBSyntheticSha256 = (Get-FileHash $selectorSyntheticB -Algorithm SHA256).Hash.ToLowerInvariant()
        SelectorBProductionSha256 = (Get-FileHash $selectorB -Algorithm SHA256).Hash.ToLowerInvariant()
        SelectorAByteLength = [long](Get-Item $selectorA).Length
        SelectorBProductionByteLength = [long](Get-Item $selectorB).Length
        PayloadRoot = 'inputs/production-payload'; ControlEvidenceRoot = 'inputs/staged-control-evidence'
    }
    if ($preparation.SelectorASha256 -ceq $preparation.SelectorBProductionSha256)
    {
        throw 'E1 selectors A/B must differ.'
    }
    Remove-D1JOwnedRoot $stage $ResolvedRunId
    $preparation.StageRemoved = $true
    [IO.File]::WriteAllText(
        (Join-Path $inputs 'preparation.json'),
        (($preparation | ConvertTo-Json -Depth 8) + "`n"),
        $script:Utf8NoBom)
    return $preparation
}

$repository = [IO.Path]::GetFullPath($RepositoryRoot)
if (-not [IO.Directory]::Exists($repository) -or
    [string]::Equals($repository, [IO.Path]::GetPathRoot($repository), [StringComparison]::OrdinalIgnoreCase))
{
    throw "Invalid repository root: $repository"
}
$resolvedRunId = if ([string]::IsNullOrWhiteSpace($RunId)) {
    'D1E1-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' +
        [Guid]::NewGuid().ToString('N').Substring(0, 12)
} else { $RunId }
if ($resolvedRunId -cnotmatch '^D1E1-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$')
{
    throw 'RunId must match D1E1-YYYYMMDDThhmmssZ-12hex.'
}

Invoke-D1JPreparation $repository $resolvedRunId | ConvertTo-Json -Depth 8
