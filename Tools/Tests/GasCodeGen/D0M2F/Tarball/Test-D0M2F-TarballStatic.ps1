[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 解析 PowerShell 文件并在任意 AST 错误时失败。
function Assert-PowerShellAst
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $tokens = $null
    $errors = $null
    [Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count -gt 0)
    {
        throw "PowerShell AST errors in $Path`n$($errors -join "`n")"
    }
}

# 核对 archive 文件名、长度与 SHA-256 全部绑定索引。
function Assert-ArchiveIndex
{
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][object]$Index
    )

    if ($Index.Schema -ne 'D0M2F-TarballArchiveIndex-v1') { throw 'Archive index schema mismatch.' }
    $archives = @($Index.Archives)
    if ($archives.Count -ne 2 -or (@($archives.GenerationId | Sort-Object) -join ',') -ne 'A,B')
    {
        throw 'Archive index case set mismatch.'
    }
    foreach ($archive in $archives)
    {
        $path = Join-Path $Root ([string]$archive.FileName)
        $sha = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($sha -ne [string]$archive.Sha256 -or [IO.Path]::GetFileNameWithoutExtension($path) -ne $sha)
        {
            throw "Archive content address mismatch: $path"
        }
        if ((Get-Item -LiteralPath $path).Length -ne [long]$archive.Length)
        {
            throw "Archive length mismatch: $path"
        }
    }
}

# 检查每个 Unity C#/assembly/package 资产都带必要 .meta。
function Assert-RequiredMetaFiles
{
    param([Parameter(Mandatory = $true)][string[]]$Roots)

    foreach ($root in $Roots)
    {
        foreach ($path in [IO.Directory]::EnumerateFiles($root, '*', [IO.SearchOption]::AllDirectories))
        {
            if ($path.EndsWith('.meta', [StringComparison]::OrdinalIgnoreCase)) { continue }
            $extension = [IO.Path]::GetExtension($path)
            if ($extension -in @('.cs', '.asmdef', '.asmref') -or [IO.Path]::GetFileName($path) -eq 'package.json')
            {
                if (-not [IO.File]::Exists($path + '.meta')) { throw "Required meta file is missing: $path.meta" }
            }
        }
    }
}

# 读取 tar entry 并核对 A/B marker 与 package 版本没有混代。
function Assert-ArchivePayload
{
    param(
        [Parameter(Mandatory = $true)][string]$ArchivePath,
        [Parameter(Mandatory = $true)][object]$Archive
    )

    $entries = @(& tar.exe -tzf $ArchivePath)
    if ($LASTEXITCODE -ne 0) { throw "Cannot list archive: $ArchivePath" }
    foreach ($entry in @('package/package.json', 'package/Runtime/GenerationMarker.cs', 'package/Editor/GenerationMarker.cs', 'package/AutoChessGenerated/GenerationMarker.cs'))
    {
        if ($entry -notin $entries) { throw "Archive entry is missing: $entry" }
    }
    $manifest = (& tar.exe -xOf $ArchivePath 'package/package.json') -join "`n"
    $runtime = (& tar.exe -xOf $ArchivePath 'package/Runtime/GenerationMarker.cs') -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Cannot read archive payload: $ArchivePath" }
    if ($manifest -notmatch [regex]::Escape('"version": "' + [string]$Archive.PackageVersion + '"')) { throw "Archive version mismatch: $ArchivePath" }
    if ($runtime -notmatch [regex]::Escape('GenerationId = "' + [string]$Archive.GenerationId + '"')) { throw "Archive generation mismatch: $ArchivePath" }
    if ($runtime -notmatch [regex]::Escape([string]$Archive.GenerationToken)) { throw "Archive token mismatch: $ArchivePath" }
}

# 用 R3 checker 校验不启动 Unity的最小 T/X 合同样本。
function Assert-ContractSamples
{
    param(
        [Parameter(Mandatory = $true)][string]$CheckerPath,
        [Parameter(Mandatory = $true)][string]$RunId
    )

    foreach ($probeId in @('T', 'X'))
    {
        $caseIds = if ($probeId -eq 'T') { @('T-01', 'T-02', 'T-03', 'T-04') } else { @('X-01', 'X-02', 'X-03', 'X-04') }
        $sample = [ordered]@{
            Schema = 'D0M2F-ProbeResult-v1'; RunId = $RunId; ProbeId = $probeId; Status = 'Passed'; Reason = 'None'
            Cases = @($caseIds | ForEach-Object { [ordered]@{ CaseId = $_; Status = 'Passed'; Reason = 'None'; Evidence = [ordered]@{ Static = $true } } })
            Inputs = [ordered]@{ Static = $true }; Protected = [ordered]@{ Responsibility = 'RootJ0J1' }
            Fixture = [ordered]@{ Static = $true }; Unity = [ordered]@{ Static = $true }
            RoleObservations = @([ordered]@{ Path = 'static://archive'; Role = 'Authority' })
        }
        $path = Join-Path ([IO.Path]::GetTempPath()) ('d0m2f-tarball-contract-' + [Guid]::NewGuid().ToString('N') + '.json')
        try
        {
            [IO.File]::WriteAllText($path, ($sample | ConvertTo-Json -Depth 16) + "`n", [Text.UTF8Encoding]::new($false))
            & $CheckerPath -Path $path -ExpectedProbeId $probeId -ExpectedRunId $RunId | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "R3 contract checker rejected $probeId sample." }
        }
        finally
        {
            if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
        }
    }
}

$root = [IO.Path]::GetFullPath($PSScriptRoot)
$runner = Join-Path $root 'Run-D0M2F-TarballProbe.ps1'
$archiveRoot = Join-Path $root 'Archives~'
$index = Get-Content -LiteralPath (Join-Path $archiveRoot 'ArchiveIndex.json') -Raw | ConvertFrom-Json
Assert-PowerShellAst -Path $runner
Assert-PowerShellAst -Path $PSCommandPath
Assert-ArchiveIndex -Root $archiveRoot -Index $index
Assert-RequiredMetaFiles -Roots @((Join-Path $root 'Fixture~'), (Join-Path $root 'ArchiveSources~/A/package'), (Join-Path $root 'ArchiveSources~/B/package'))
foreach ($archive in @($index.Archives))
{
    Assert-ArchivePayload -ArchivePath (Join-Path $archiveRoot ([string]$archive.FileName)) -Archive $archive
}
$checker = [IO.Path]::GetFullPath((Join-Path $root '../Contracts/Test-D0M2FContract.ps1'))
if ([IO.File]::Exists($checker))
{
    Assert-ContractSamples -CheckerPath $checker -RunId 'D0M2F-20260830T093828Z-b2bb1730b701'
}

[pscustomobject]@{
    Passed = $true
    PowerShellAst = 'Passed'
    ArchiveIdentity = 'Passed'
    ArchivePayload = 'Passed'
    RequiredMeta = 'Passed'
    ContractSamples = if ([IO.File]::Exists($checker)) { 'Passed' } else { 'NotAvailable' }
} | ConvertTo-Json -Depth 4
