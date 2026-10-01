[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RepositoryRoot,
    [string]$UnityPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 执行 post-J 14/14 production exact gate；本脚本只读且绝不启动 Unity。
$raw = & (Join-Path $PSScriptRoot 'Test-D1-ScaffoldStatic.ps1') `
    -RepositoryRoot $RepositoryRoot -UnityPath $UnityPath -RequireProductionScaffold | Out-String
$result = $raw | ConvertFrom-Json
if (-not [bool]$result.Passed -or [bool]$result.UnityStarted -or
    [string]$result.ProductionState.Phase -cne 'PostMigration' -or
    [int]$result.ProductionState.ProductionExact -ne 14 -or
    [int]$result.ProductionState.ProductionMissing -ne 0 -or
    -not [bool]$result.ProductionState.FullExact -or
    [string]$result.ProductionRouteScaffoldSha256 -cne
        [string]$result.StagedRouteScaffoldSha256)
{
    throw 'Post-J production scaffold is not exact 14/14 staged bytes.'
}

[pscustomobject][ordered]@{
    Schema = 'EX-GAS-D1-PostMigrationScaffold-v1'; Passed = $true
    ProductionExact = 14; ProductionMissing = 0; StagedNew = 8
    StagedRouteScaffoldSha256 = [string]$result.StagedRouteScaffoldSha256
    ProductionRouteScaffoldSha256 = [string]$result.ProductionRouteScaffoldSha256
    UnityStarted = $false
} | ConvertTo-Json -Depth 4
