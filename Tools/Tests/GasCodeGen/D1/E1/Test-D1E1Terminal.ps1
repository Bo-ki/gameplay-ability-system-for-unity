[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$TerminalPath,
    [string]$ContractPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 以异常阻断 terminal/index/sidecar 的任何结构或字节不一致。
function Assert-D1E1Terminal
{
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) { throw $Message }
}

# 返回普通文件的 bytes length 与小写 SHA-256。
function Get-D1E1TerminalFileIdentity
{
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-D1E1Terminal ([IO.File]::Exists($Path)) "Terminal evidence file is missing: $Path"
    $bytes = [IO.File]::ReadAllBytes($Path)
    return [pscustomobject]@{
        Length = [long]$bytes.LongLength
        Sha256 = [Convert]::ToHexString(
            [Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    }
}

# 验证 exact 33 case identity、EvidencePath、index entry 与实际文件 identity。
function Test-D1E1TerminalCases
{
    param(
        [Parameter(Mandatory = $true)]$Terminal,
        [Parameter(Mandatory = $true)]$Contract,
        [Parameter(Mandatory = $true)]$Index,
        [Parameter(Mandatory = $true)][string]$RunRoot
    )

    $expected = @($Contract.TransactionCases) + @($Contract.UnityCases)
    $actual = @($Terminal.Cases)
    Assert-D1E1Terminal ($expected.Count -eq 33 -and $actual.Count -eq 33) `
        'Typed terminal must contain exactly 33 cases.'
    foreach ($spec in $expected)
    {
        $matches = @($actual | Where-Object { [string]$_.CaseId -ceq [string]$spec.CaseId })
        Assert-D1E1Terminal ($matches.Count -eq 1 -and
            [string]$matches[0].Name -ceq [string]$spec.Name) `
            "Typed terminal case identity mismatch: $($spec.CaseId)"
        $relative = [string]$matches[0].EvidencePath
        $entries = @($Index.Entries | Where-Object { [string]$_.Path -ceq $relative })
        Assert-D1E1Terminal ($entries.Count -eq 1) "Case evidence is not exact in index: $relative"
        $absolute = [IO.Path]::GetFullPath((Join-Path $RunRoot $relative))
        $prefix = [IO.Path]::GetFullPath((Join-Path $RunRoot 'evidence')) +
            [IO.Path]::DirectorySeparatorChar
        Assert-D1E1Terminal ($absolute.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) `
            "Case evidence escaped evidence root: $relative"
        $identity = Get-D1E1TerminalFileIdentity $absolute
        Assert-D1E1Terminal ([long]$entries[0].Length -eq $identity.Length -and
            [string]$entries[0].Sha256 -ceq $identity.Sha256) `
            "Case evidence identity mismatch: $relative"
        $summary = Get-Content -LiteralPath $absolute -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert-D1E1Terminal ([string]$summary.CaseId -ceq [string]$spec.CaseId -and
            [string]$summary.RunId -ceq [string]$Terminal.RunId) `
            "Case evidence identity fields mismatch: $relative"
        if ([string]$spec.CaseId -cne 'EV-01')
        {
            Assert-D1E1Terminal ($null -ne $summary.PSObject.Properties['Passed'] -and
                ([string]$matches[0].Status -cne 'Passed' -or [bool]$summary.Passed)) `
                "Case evidence status mismatch: $relative"
        }
    }
}

$resolvedTerminal = [IO.Path]::GetFullPath($TerminalPath)
$resolvedContract = if ([string]::IsNullOrWhiteSpace($ContractPath)) {
    Join-Path $PSScriptRoot 'D1E1.contract.json'
} else { [IO.Path]::GetFullPath($ContractPath) }
$runRoot = [IO.Path]::GetDirectoryName($resolvedTerminal)
$contract = Get-Content -LiteralPath $resolvedContract -Raw -Encoding UTF8 | ConvertFrom-Json
$terminalIdentity = Get-D1E1TerminalFileIdentity $resolvedTerminal
$terminalText = [Text.UTF8Encoding]::new($false, $true).GetString(
    [IO.File]::ReadAllBytes($resolvedTerminal))
$schemaPath = Join-Path $PSScriptRoot 'D1E1.terminal.schema.json'
Assert-D1E1Terminal ($terminalText | Test-Json -SchemaFile $schemaPath -ErrorAction SilentlyContinue) `
    'Terminal does not satisfy the frozen typed JSON schema.'
$terminal = $terminalText | ConvertFrom-Json
$sidecar = [IO.File]::ReadAllText($resolvedTerminal + '.sha256', [Text.UTF8Encoding]::new($false, $true))
$expectedSidecar = $terminalIdentity.Sha256 + '  ' + [IO.Path]::GetFileName($resolvedTerminal) + "`n"
Assert-D1E1Terminal ($sidecar -ceq $expectedSidecar) 'Terminal SHA sidecar mismatch.'

$required = @(
    'Schema', 'RunId', 'ProbeId', 'Status', 'Reason', 'StartedUtc', 'CompletedUtc',
    'Inputs', 'ToolIdentity', 'ChildProcesses', 'Protected', 'Cases', 'Cleanup',
    'EvidenceClosure', 'FailureDetail', 'ProductionInstallAdmission',
    'DeclaredFullSemanticEligibility')
$actualNames = @($terminal.PSObject.Properties.Name)
Assert-D1E1Terminal ($actualNames.Count -eq $required.Count -and
    @($required | Where-Object { $_ -cnotin $actualNames }).Count -eq 0) `
    'Terminal top-level exact properties mismatch.'
Assert-D1E1Terminal ([string]$terminal.Schema -ceq 'EX-GAS-D1-E1-Terminal-v1' -and
    [string]$terminal.RunId -cmatch '^D1E1-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}$' -and
    [string]$terminal.ProductionInstallAdmission -ceq 'NotEvaluated' -and
    -not [bool]$terminal.DeclaredFullSemanticEligibility) 'Terminal frozen identity drifted.'

$indexPath = Join-Path $runRoot ([string]$terminal.EvidenceClosure.IndexPath)
$indexIdentity = Get-D1E1TerminalFileIdentity $indexPath
$index = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-D1E1Terminal ([string]$index.Schema -ceq 'EX-GAS-D1-E1-EvidenceIndex-v1' -and
    [string]$index.RunId -ceq [string]$terminal.RunId -and
    [string]$index.Scope -ceq 'evidence/**' -and -not [bool]$index.Partial -and
    [string]$terminal.EvidenceClosure.IndexSha256 -ceq $indexIdentity.Sha256 -and
    [int]$index.DeclaredCount -eq @($index.Entries).Count -and
    [int]$terminal.EvidenceClosure.DeclaredCount -eq [int]$index.DeclaredCount -and
    [int]$terminal.EvidenceClosure.PhysicalCount -eq [int]$index.DeclaredCount) `
    'Evidence index/closure identity mismatch.'
Test-D1E1TerminalCases $terminal $contract $index $runRoot

if ([string]$terminal.Status -ceq 'Passed')
{
    Assert-D1E1Terminal ([string]$terminal.Reason -ceq 'None' -and
        [bool]$terminal.ToolIdentity.Unchanged -and [bool]$terminal.Protected.Unchanged -and
        [bool]$terminal.ChildProcesses.Passed -and
        [int]$terminal.ChildProcesses.Transaction.ExitCode -eq 0 -and
        -not [bool]$terminal.ChildProcesses.Transaction.TimedOut -and
        [int]$terminal.ChildProcesses.Unity.ExitCode -eq 0 -and
        -not [bool]$terminal.ChildProcesses.Unity.TimedOut -and
        [bool]$terminal.Cleanup.Passed -and [bool]$terminal.Cleanup.RootRemoved -and
        [bool]$terminal.EvidenceClosure.Verified -and
        [bool]$terminal.EvidenceClosure.CaseEvidenceVerified -and
        @($terminal.Cases | Where-Object { [string]$_.Status -cne 'Passed' -or
            [string]$_.Reason -cne 'None' }).Count -eq 0) `
        'Passed terminal does not imply every child/protected/cleanup/closure/case invariant.'
}

[pscustomobject][ordered]@{
    Schema = 'EX-GAS-D1-E1-TerminalVerification-v1'; Passed = $true
    RunId = [string]$terminal.RunId; Status = [string]$terminal.Status
    TerminalSha256 = $terminalIdentity.Sha256; IndexSha256 = $indexIdentity.Sha256
    CaseCount = @($terminal.Cases).Count
} | ConvertTo-Json -Depth 4
