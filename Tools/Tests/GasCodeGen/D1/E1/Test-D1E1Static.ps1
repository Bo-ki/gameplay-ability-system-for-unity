[CmdletBinding()]
param(
    [string]$UnityPath = '',
    [string]$AnalyzerPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'D1E1ProtectedPathOracle.ps1')

# 断言静态条件成立，任何漏项都必须阻断交付。
function Assert-D1E1Static
{
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) { throw $Message }
}

# 断言字符串集合在 ordinal 语义下完全相同且无重复。
function Assert-D1E1ExactSet
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
    Assert-D1E1Static ($actualSorted.Count -eq $expectedSorted.Count) "$Context count mismatch."
    for ($index = 0; $index -lt $actualSorted.Count; $index++)
    {
        Assert-D1E1Static ($actualSorted[$index] -ceq $expectedSorted[$index]) `
            "$Context mismatch at $index."
    }
}

# 解析 PowerShell AST，并拒绝语法错误或超过 80 行的函数。
function Test-D1E1PowerShellAst
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $tokens = $null
    $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile(
        $Path, [ref]$tokens, [ref]$errors)
    if (@($errors).Count -ne 0)
    {
        throw "PowerShell AST failed for $Path`: $($errors -join ' | ')"
    }
    $functions = $ast.FindAll({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst]
    }, $true)
    foreach ($function in $functions)
    {
        $lineCount = $function.Extent.EndLineNumber - $function.Extent.StartLineNumber + 1
        Assert-D1E1Static ($lineCount -le 80) `
            "Function exceeds 80 lines: $Path::$($function.Name) ($lineCount)"
    }
}

# 断言文本含全部 P0 token，防止后续机械改写删掉关键拒绝门。
function Assert-D1E1Tokens
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string[]]$Tokens
    )

    $text = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    foreach ($token in $Tokens)
    {
        Assert-D1E1Static ($text.Contains($token, [StringComparison]::Ordinal)) `
            "$Path is missing P0 token: $token"
    }
}

$contractPath = Join-Path $PSScriptRoot 'D1E1.contract.json'
$schemaPath = Join-Path $PSScriptRoot 'D1E1.terminal.schema.json'
$contract = Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8 | ConvertFrom-Json
$schema = Get-Content -LiteralPath $schemaPath -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-D1E1Static ([string]$contract.Schema -ceq 'EX-GAS-D1-E1-Contract-v1') `
    'E1 contract schema mismatch.'
Assert-D1E1Static (@($contract.TransactionCases).Count -eq 21) `
    'E1 must contain exactly 21 transaction cases.'
Assert-D1E1Static (@($contract.UnityCases).Count -eq 12) `
    'E1 must contain exactly 12 Unity/closure cases.'
$allCaseIds = @($contract.TransactionCases.CaseId) + @($contract.UnityCases.CaseId)
Assert-D1E1Static ($allCaseIds.Count -eq 33 -and @($allCaseIds | Sort-Object -Unique).Count -eq 33) `
    'E1 must contain exactly 33 unique case IDs.'
Assert-D1E1ExactSet @($contract.CommitAttemptStates | ForEach-Object { [string]$_ }) @(
    'NotStarted', 'Armed', 'Committed', 'CompetitionFailed', 'Indeterminate', 'NoOp'
) 'CommitAttemptStates'
Assert-D1E1ExactSet @($contract.TransactionCases | Where-Object {
    [string]$_.Scenario -ceq 'RecoverState'
} | ForEach-Object { [string]$_.SeedState }) @(
    'NotStarted', 'Armed', 'Committed', 'CompetitionFailed', 'Indeterminate', 'NoOp'
) 'Six-state recovery cases'

$requiredTransactionNames = @(
    'MissingSuccess', 'MissingInterruptedBeforeCommit', 'PresentReplaceSuccess',
    'PresentInterruptedBeforeCommit', 'NotStartedIntentFlushedBeforePostClaimReread',
    'PostClaimCasPreviousDrift', 'CommittedReceiptAuditFailure', 'SameTargetNoOp',
    'PresentSelectorMissingDuringRecovery', 'CorruptUnknownRecovery',
    'SameTargetCompetingCreate', 'DifferentTargetCompetingCreate',
    'CompetitionTargetEqualCannotClaimPromotion',
    'IndeterminateTargetEqualCannotClaimPromotion', 'RepeatedRecoveryIsStable',
    'RecoveryStateNotStarted', 'RecoveryStateArmed', 'RecoveryStateCommitted',
    'RecoveryStateCompetitionFailed', 'RecoveryStateIndeterminate', 'RecoveryStateNoOp'
)
Assert-D1E1ExactSet @($contract.TransactionCases.Name | ForEach-Object { [string]$_ }) `
    $requiredTransactionNames 'Transaction fixed cases'
$tr06 = @($contract.TransactionCases | Where-Object CaseId -ceq 'TR-06')[0]
Assert-D1E1Static ([string]$tr06.Scenario -ceq 'PostClaimPreviousDrift' -and
    [string]$tr06.FaultPoint -ceq 'AfterNotStartedIntent' -and
    [string]$tr06.ExpectedState -ceq 'Indeterminate' -and
    [string]$tr06.ExpectedSelectorAfter -ceq 'Competition') `
    'TR-06 must inject real post-claim CAS drift after the durable NotStarted claim.'
$tr11 = @($contract.TransactionCases | Where-Object CaseId -ceq 'TR-11')[0]
$tr12 = @($contract.TransactionCases | Where-Object CaseId -ceq 'TR-12')[0]
Assert-D1E1Static ([string]$tr11.Scenario -ceq 'CompetingCreate' -and
    [string]$tr11.CompetitionTarget -ceq 'B' -and
    $null -eq $tr11.PSObject.Properties['FaultPoint'] -and
    [string]$tr12.Scenario -ceq 'CompetingCreate' -and
    [string]$tr12.CompetitionTarget -ceq 'A' -and
    $null -eq $tr12.PSObject.Properties['FaultPoint']) `
    'TR-11/TR-12 must use real same/different create races, not injected competition state.'
$requiredUnityNames = @(
    'ThreeAssembliesCommonSelectorMarker', 'ScaffoldMissing', 'ScaffoldByteDrift',
    'SelectorMissing', 'SelectorCorrupt', 'AnalyzerMissing',
    'AnalyzerCorruptHardFail', 'DuplicateSelector', 'LegacyActiveGenCs',
    'StaleABeeRspCannotSelect', 'OwnedFixtureCleanup', 'EvidenceClosure'
)
Assert-D1E1ExactSet @($contract.UnityCases.Name | ForEach-Object { [string]$_ }) `
    $requiredUnityNames 'Unity fixed cases'

Assert-D1E1Static (-not [bool]$contract.NegativeUnityInvariants.AddSourceObserved -and
    -not [bool]$contract.NegativeUnityInvariants.PromotionObserved -and
    -not [bool]$contract.NegativeUnityInvariants.SelectorChanged -and
    [string]$contract.NegativeUnityInvariants.InvocationExit -ceq 'NonZero') `
    'Negative Unity invariants must be nonzero/zero-AddSource/zero-promotion/selector-stable.'
$corruptAnalyzer = @($contract.UnityCases | Where-Object CaseId -ceq 'DU-07')
Assert-D1E1Static ($corruptAnalyzer.Count -eq 1 -and
    $null -eq $corruptAnalyzer[0].PSObject.Properties['RequireCs8034'] -and
    [bool]$corruptAnalyzer[0].RequireHardGuard) `
    'DU-07 must require the ordinary compile hard guard without brittle diagnostic text.'
$legacy = @($contract.UnityCases | Where-Object CaseId -ceq 'DU-09')
Assert-D1E1Static ($legacy.Count -eq 1 -and
    [string]$legacy[0].ExpectedDiagnostic -ceq 'GASGEN007' -and
    [string]$contract.LegacyActiveSourceRelativePath -ceq
        'Assets/AutoChessDemo/Generated/AutoChessGeneratedConfig.gen.cs') `
    'DU-09 legacy active source contract drifted.'

Assert-D1E1Static ([string]$schema.'$id' -ceq 'EX-GAS-D1-E1-Terminal-v1') `
    'Typed terminal schema id mismatch.'
Assert-D1E1ExactSet @($schema.required | ForEach-Object { [string]$_ }) @(
    'Schema', 'RunId', 'ProbeId', 'Status', 'Reason', 'StartedUtc', 'CompletedUtc',
    'Inputs', 'ToolIdentity', 'ChildProcesses', 'Protected', 'Cases', 'Cleanup', 'EvidenceClosure',
    'FailureDetail', 'ProductionInstallAdmission', 'DeclaredFullSemanticEligibility'
) 'Typed terminal required fields'
Assert-D1E1Static ([string]$schema.properties.ProductionInstallAdmission.const -ceq 'NotEvaluated' -and
    -not [bool]$schema.properties.DeclaredFullSemanticEligibility.const -and
    [int]$schema.properties.Cases.minItems -eq 33 -and
    [int]$schema.properties.Cases.maxItems -eq 33) `
    'D1 E1 must not grant production admission or full semantic eligibility.'

$e1Scripts = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' -File)
$scaffoldRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'Scaffold'
$scaffoldScripts = @(Get-ChildItem -LiteralPath $scaffoldRoot -Filter '*.ps1' -File)
$badReplaceToken = ".Replace('" + ('\' * 2) + "', '/')"
foreach ($script in @($e1Scripts) + @($scaffoldScripts))
{
    Test-D1E1PowerShellAst $script.FullName
    $scriptText = Get-Content -LiteralPath $script.FullName -Raw -Encoding UTF8
    Assert-D1E1Static (-not $scriptText.Contains($badReplaceToken, [StringComparison]::Ordinal)) `
        "PowerShell path normalization uses a two-character backslash token: $($script.FullName)"
}

Assert-D1E1Tokens (Join-Path $PSScriptRoot 'Run-D1E1.ps1') @(
    'Get-D1E1ProtectedSnapshot', 'Get-D1E1ToolIdentity',
    'Remove-D1E1OwnedFixtureRoot', 'New-D1E1EvidenceClosure',
    'Test-D1E1CaseEvidenceClosure', 'transactionChildPassed', 'unityChildPassed',
    'Test-D1E1Terminal.ps1', 'CaseEvidenceVerified',
    'ProductionInstallAdmission', 'DeclaredFullSemanticEligibility',
    'TransactionSelectorBPath', 'UnitySelectorBPath', 'MAX_PATH',
    'evidence/**', 'FileMode]::CreateNew', 'Flush($true)'
)
Assert-D1E1Tokens (Join-Path $PSScriptRoot 'Invoke-D1E1TransactionMatrix.ps1') @(
    'EX-GAS-D1-E1-TransactionRequest-v1', 'EX-GAS-D1-E1-TransactionResponse-v1',
    '--mode', 'd1b-self-test', '--request', '--output',
    'Scenario =', 'SeedState =', 'CompetitionTarget =', 'Target =',
    'Get-D1E1VerifiedRawSnapshot', 'Get-D1E1VerifiedIntent',
    'Get-D1E1VerifiedAudit', 'Get-D1E1ArchivedIntents',
    'Test-D1E1DurableOutcome', 'Get-D1E1ProductionSelectorIdentity',
    'IntentBeforeRecovery', 'RecoverySnapshots', 'ExpectedSelectorAfter',
    'TransactionAdapterRelativePath'
)
Assert-D1E1Tokens (Join-Path $PSScriptRoot 'Invoke-D1E1UnityMatrix.ps1') @(
    '-d1e1Output', '-d1e1Signal', '-d1e1RunId', '-d1e1CaseId',
    '-d1e1InvocationId', 'LoadedMvid', 'LoadedLocation',
    'CurrentCandidates', 'GasCodeGenSourceGeneratorMarker',
    'Get-D1E1AddSourceObservation', 'Get-D1E1PromotionObservation',
    'Resolve-D1E1RspPathArgument', 'Get-D1E1ManagedPeIdentity',
    'Get-D1E1RspOutputBinding', 'RspOutputIdentity', 'LoadedOutputIdentity',
    'AnalyzerExact', 'SelectorExact', 'AnalyzerIdentity', 'SelectorIdentity'
)
Assert-D1E1Tokens (Join-Path $scaffoldRoot 'Test-D1-PostMigrationScaffold.ps1') @(
    'RequireProductionScaffold', 'ProductionExact', 'ProductionMissing',
    'StagedRouteScaffoldSha256', 'ProductionRouteScaffoldSha256'
)
Assert-D1E1Tokens (Join-Path $PSScriptRoot 'Test-D1E1Terminal.ps1') @(
    'Test-Json', 'D1E1.terminal.schema.json', 'CaseEvidenceVerified',
    'summary.Passed', 'ChildProcesses.Transaction.ExitCode',
    'ChildProcesses.Unity.ExitCode'
)
$unityRunnerText = Get-Content -LiteralPath `
    (Join-Path $PSScriptRoot 'Invoke-D1E1UnityMatrix.ps1') -Raw
Assert-D1E1Static (-not $unityRunnerText.Contains("'-quit'", [StringComparison]::Ordinal)) `
    'Direct Unity runner must not use -quit with its execute-method lifecycle.'

$scaffoldStaticOutput = & (Join-Path $scaffoldRoot 'Test-D1-ScaffoldStatic.ps1') `
    -UnityPath $UnityPath | Out-String
if ($LASTEXITCODE -ne 0) { throw 'Scaffold static validation failed.' }
$scaffoldStatic = $scaffoldStaticOutput | ConvertFrom-Json
Assert-D1E1Static ([bool]$scaffoldStatic.Passed -and -not [bool]$scaffoldStatic.UnityStarted) `
    'Scaffold static validation did not pass.'
Assert-D1E1Static ([int]$scaffoldStatic.ProductionState.ExistingExact -eq 6 -and
    [int]$scaffoldStatic.ProductionState.ProductionExact -eq 6 -and
    [int]$scaffoldStatic.ProductionState.ProductionMissing -eq 8 -and
    [int]$scaffoldStatic.ProductionState.StagedNew -eq 8 -and
    -not [bool]$scaffoldStatic.ProductionState.FullExact) `
    'Pre-J scaffold state must be exactly existing 6/6 plus staged-new 8/8.'

$resolvedRepository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../..'))
$protectedBefore = Get-D1E1ProtectedSnapshot $resolvedRepository $contractPath
$protectedAfter = Get-D1E1ProtectedSnapshot $resolvedRepository $contractPath
$protectedComparison = Compare-D1E1ProtectedSnapshots $protectedBefore $protectedAfter
Assert-D1E1Static ([bool]$protectedComparison.Unchanged) `
    'Read-only protected snapshot must be stable during static validation.'

$resolvedAnalyzer = if (-not [string]::IsNullOrWhiteSpace($AnalyzerPath)) {
    [IO.Path]::GetFullPath($AnalyzerPath)
} else {
    Join-Path $resolvedRepository `
        'Tools/GasCodeGenSourceGenerator/bin/Release/netstandard2.0/GasCodeGenSourceGenerator.dll'
}
$analyzerIdentity = $null
if ([IO.File]::Exists($resolvedAnalyzer))
{
    $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($resolvedAnalyzer)
    Assert-D1E1Static ($assemblyName.Name -ceq 'GasCodeGenSourceGenerator') `
        'Analyzer assembly identity mismatch.'
    $analyzerIdentity = [pscustomobject]@{
        Path = $resolvedAnalyzer.Replace('\', '/')
        Sha256 = (Get-FileHash $resolvedAnalyzer -Algorithm SHA256).Hash.ToLowerInvariant()
        AssemblyName = $assemblyName.Name
    }
}

[pscustomobject][ordered]@{
    Schema = 'EX-GAS-D1-E1-Static-v1'; Passed = $true
    TransactionCaseCount = @($contract.TransactionCases).Count
    UnityAndClosureCaseCount = @($contract.UnityCases).Count
    TotalCaseCount = $allCaseIds.Count; PowerShellAstPassed = $true
    StagedRouteScaffoldSha256 = [string]$scaffoldStatic.StagedRouteScaffoldSha256
    ProductionRouteScaffoldSha256 = $scaffoldStatic.ProductionRouteScaffoldSha256
    ProductionScaffoldState = $scaffoldStatic.ProductionState
    UnityProbeStaticBuild = $scaffoldStatic.UnityProbeStaticBuild
    ProtectedSnapshotSha256 = $protectedBefore.SnapshotSha256
    AnalyzerIdentity = $analyzerIdentity; UnityStarted = $false
} | ConvertTo-Json -Depth 12
