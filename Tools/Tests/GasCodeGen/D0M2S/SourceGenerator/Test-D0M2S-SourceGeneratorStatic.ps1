#requires -Version 7.0
[CmdletBinding()]
param([string]$UnityPath = "")

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:Prefix = "gas-codegen-d0-m2s-sourcegen-static-"
$script:Sentinel = ".d0m2s-sourcegen-static-owner"
$script:Utf8NoBom = [Text.UTF8Encoding]::new($false)

# 创建只供本静态门使用的 OS 临时根。
function New-StaticRoot
{
    $root = Join-Path ([IO.Path]::GetTempPath()) ($script:Prefix + [Guid]::NewGuid().ToString("N"))
    [IO.Directory]::CreateDirectory($root) | Out-Null
    [IO.File]::WriteAllText((Join-Path $root $script:Sentinel), "owned`n", $script:Utf8NoBom)
    return [IO.Path]::GetFullPath($root)
}

# 在精确 temp/prefix/sentinel 复核后删除静态门临时根。
function Remove-StaticRoot
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $resolved = [IO.Path]::GetFullPath($Root)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd([IO.Path]::DirectorySeparatorChar) -cne $temp -or
        [IO.Path]::GetFileName($resolved) -cnotmatch '^gas-codegen-d0-m2s-sourcegen-static-[0-9a-f]{32}$' -or
        [IO.File]::ReadAllText((Join-Path $resolved $script:Sentinel)) -cne "owned`n")
    {
        throw "Unsafe static cleanup root: $resolved"
    }
    [IO.Directory]::Delete($resolved, $true)
}

# 解析 PowerShell AST 并返回顶层脚本对象。
function Get-PowerShellAst
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors)
    if ($errors.Count -ne 0)
    {
        throw "PowerShell AST failed: $Path :: $($errors[0].Message)"
    }
    return $ast
}

# 读取 runner 的唯一函数定义，供结构门检查关键实现。
function Get-FunctionDefinition
{
    param(
        [Parameter(Mandatory = $true)]$Ast,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $matches = @($Ast.FindAll({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
            $node.Name -ceq $Name
    }, $true))
    if ($matches.Count -ne 1) { throw "Function definition is not unique: $Name" }
    return $matches[0]
}

# 校验 child 参数、固定函数、8-case 与禁止的 post-raw/mtime/-quit 假绿模式。
function Test-RunnerContract
{
    param([Parameter(Mandatory = $true)][string]$RunnerPath)

    $ast = Get-PowerShellAst $RunnerPath
    $parameterNames = @($ast.ParamBlock.Parameters | ForEach-Object {
        $_.Name.VariablePath.UserPath
    })
    foreach ($name in @("UnityPath", "RunId", "EvidenceRoot", "OutputPath"))
    {
        if ($name -cnotin $parameterNames) { throw "Runner parameter is missing: $name" }
    }
    foreach ($name in @(
        "Invoke-AtomicSelectorSwitch", "Invoke-GeneratorCompileKill", "Invoke-NegativeUnityFault",
        "Get-BeeEvidence", "Test-ExactInvocation", "Test-BoundaryControls", "Complete-ChildEvidence",
        "Get-TrackedSignalPathBase64", "Set-CompilationPhaseStamp"))
    {
        [void](Get-FunctionDefinition $ast $name)
    }
    $quit = @($ast.FindAll({
        param($node)
        $node -is [Management.Automation.Language.StringConstantExpressionAst] -and
            $node.Value -ceq "-quit"
    }, $true))
    if ($quit.Count -ne 0) { throw "Runner must not pass -quit to Unity." }
    $text = [IO.File]::ReadAllText($RunnerPath)
    foreach ($caseId in 1..8 | ForEach-Object { "SG-{0:D2}" -f $_ })
    {
        if (-not $text.Contains($caseId, [StringComparison]::Ordinal))
        {
            throw "Runner fixed case is missing: $caseId"
        }
    }
    if ($text.Contains("LastWriteTimeUtc", [StringComparison]::Ordinal) -or
        $text.Contains("Sort-Object LastWriteTime", [StringComparison]::Ordinal) -or
        $text.Contains("D0M2FSourceGeneratorUnityProbe.Run", [StringComparison]::Ordinal) -or
        $text.Contains("D0M2F/SourceGenerator/Generator~", [StringComparison]::Ordinal))
    {
        throw "Runner contains a forbidden historical/mtime selection path."
    }
    $bee = (Get-FunctionDefinition $ast "Get-BeeEvidence").Extent.Text
    if (-not $bee.Contains("MatchesOutput", [StringComparison]::Ordinal) -or
        -not $bee.Contains("RawBytesBase64", [StringComparison]::Ordinal))
    {
        throw "RSP evidence is not output-bound and replayable."
    }
    $phaseStamp = (Get-FunctionDefinition $ast "Set-CompilationPhaseStamp").Extent.Text
    foreach ($token in @(
        "D0M2S_TRACKED_PHASE_STAMP", "RunId=", "CaseId=", "InvocationId=",
        "GeneratorSha256=", "PhaseNonce=", "ControlMode=", "SignalPathBase64=",
        "D0M2S-TrackedPhaseStamps-v1", "RawBytesBase64", "ParsedTuple", "Stamps"))
    {
        if (-not $phaseStamp.Contains($token, [StringComparison]::Ordinal))
        {
            throw "Runner tracked phase-stamp token is missing: $token"
        }
    }
    if ($phaseStamp.Contains("Generation=", [StringComparison]::Ordinal) -or
        $phaseStamp.Contains("SelectorSha256=", [StringComparison]::Ordinal) -or
        $text.Contains("D0M2S_GENERATOR_", [StringComparison]::Ordinal) -or
        -not $text.Contains('$phaseStampEvidence.RelativePath', [StringComparison]::Ordinal) -or
        -not $text.Contains("PhaseStampsMatch", [StringComparison]::Ordinal) -or
        -not $text.Contains('"SourceFiles"', [StringComparison]::Ordinal) -or
        -not $text.Contains("-ItemType Junction", [StringComparison]::Ordinal) -or
        $text.Contains("CreateSymbolicLink", [StringComparison]::Ordinal))
    {
        throw "Runner phase-stamp authority or evidence path is invalid."
    }
}

# 校验 generator 的 selector 语义、tracked SyntaxTree identity/control 与 AddSource 前阻塞合同。
function Test-GeneratorContract
{
    param([Parameter(Mandatory = $true)][string]$GeneratorPath)

    $text = [IO.File]::ReadAllText($GeneratorPath)
    foreach ($token in @(
        "generation-a\n", "generation-b\n", "D0M2SSG001", "D0M2SSG002", "D0M2SSG003",
        "D0M2SSG004", "D0M2S_TRACKED_PHASE_STAMP", "Compilation.SyntaxTrees", "syntaxTree.FilePath",
        "D0M2SRuntimePhaseStamp.cs", "D0M2SEditorPhaseStamp.cs", "D0M2SAutoChessPhaseStamp.cs",
        "selector.GetText", "selectorSnapshot.ToString", "SignalPathBase64",
        "Convert.FromBase64String", "Directory.GetParent", "Path.GetFullPath",
        "D0M2S-GeneratorBeforeAddSource-v1", "CompilerProcessId", "SelectorSha256"))
    {
        if (-not $text.Contains($token, [StringComparison]::Ordinal))
        {
            throw "Generator contract token is missing: $token"
        }
    }
    if ($text.Contains(".Trim(", [StringComparison]::Ordinal))
    {
        throw "Generator must not Trim selector bytes."
    }
    if ($text.Contains("Environment.GetEnvironmentVariable", [StringComparison]::Ordinal) -or
        $text.Contains('syntaxPath.EndsWith', [StringComparison]::Ordinal) -or
        $text.Contains("D0M2S_GENERATOR_", [StringComparison]::Ordinal))
    {
        throw "Generator identity/control must not be decided by process environment."
    }
    $patternStart = $text.IndexOf("TrackedPhaseStampPattern =", [StringComparison]::Ordinal)
    $patternEnd = $text.IndexOf("RegexOptions.CultureInvariant", $patternStart, [StringComparison]::Ordinal)
    if ($patternStart -lt 0 -or $patternEnd -le $patternStart)
    {
        throw "Generator tracked phase-stamp regex is missing."
    }
    $patternText = $text.Substring($patternStart, $patternEnd - $patternStart)
    if ($patternText.Contains("Generation", [StringComparison]::Ordinal) -or
        $patternText.Contains("Selector", [StringComparison]::Ordinal))
    {
        throw "Tracked phase-stamp must not carry generation or selector authority."
    }
    $controlIndex = $text.IndexOf("TryEnterCompileWindowControl(", [StringComparison]::Ordinal)
    $addSourceIndex = $text.IndexOf("context.AddSource(", [StringComparison]::Ordinal)
    if ($controlIndex -lt 0 -or $addSourceIndex -le $controlIndex)
    {
        throw "Compile-window checkpoint is not before AddSource."
    }
}

# 校验 overlay 的 canonical selector、Unity meta 与 analyzer label。
function Test-OverlayContract
{
    param([Parameter(Mandatory = $true)][string]$OverlayRoot)

    $selector = Join-Path $OverlayRoot "Assets/Selector/Generation.D0M2SCanaryGenerator.additionalfile"
    $bytes = [IO.File]::ReadAllBytes($selector)
    $expected = $script:Utf8NoBom.GetBytes("generation-a`n")
    if ([Convert]::ToBase64String([byte[]]$bytes) -cne
        [Convert]::ToBase64String([byte[]]$expected))
    {
        throw "Overlay selector is not canonical generation-a bytes."
    }
    foreach ($asset in @(
        $selector,
        (Join-Path $OverlayRoot "Assets/Editor/D0M2SSourceGeneratorUnityProbe.cs")))
    {
        if (-not [IO.File]::Exists($asset + ".meta")) { throw "Overlay asset meta missing: $asset" }
    }
    $analyzerMeta = Join-Path $OverlayRoot "Assets/Analyzers/D0M2SCanaryGenerator.dll.meta"
    if (-not [IO.File]::ReadAllText($analyzerMeta).Contains("- RoslynAnalyzer", [StringComparison]::Ordinal))
    {
        throw "D0M2S analyzer meta label is missing."
    }
}

# 以 C# 9/netstandard2.0 编译新 generator，并返回 DLL identity。
function Build-GeneratorStatic
{
    param([Parameter(Mandatory = $true)][string]$StaticRoot)

    $sourceRoot = Join-Path $PSScriptRoot "Generator~"
    $project = Join-Path $StaticRoot "D0M2SCanaryGenerator.csproj"
    [IO.File]::Copy((Join-Path $sourceRoot "D0M2SCanaryGenerator.csproj.template"), $project, $false)
    [IO.File]::Copy((Join-Path $sourceRoot "D0M2SCanaryGenerator.cs"),
        (Join-Path $StaticRoot "D0M2SCanaryGenerator.cs"), $false)
    $output = Join-Path $StaticRoot "generator-out"
    $intermediate = Join-Path $StaticRoot "generator-obj/"
    $buildOutput = @(& dotnet build $project --configuration Release --output $output --nologo `
        "/p:BaseIntermediateOutputPath=$intermediate" "/p:RestoreIgnoreFailedSources=true" 2>&1)
    $buildExitCode = $LASTEXITCODE
    if ($buildExitCode -ne 0)
    {
        throw "D0M2S generator static build failed (exit=$buildExitCode): $($buildOutput -join ' | ')"
    }
    $dll = Join-Path $output "D0M2SCanaryGenerator.dll"
    if (-not [IO.File]::Exists($dll) -or
        [Reflection.AssemblyName]::GetAssemblyName($dll).Name -cne "D0M2SCanaryGenerator")
    {
        throw "D0M2S generator assembly identity mismatch."
    }
    return [pscustomobject]@{
        Path = $dll; Sha256 = (Get-FileHash $dll -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

# 解析 Unity Editor/Data 路径，仅用于离线托管引用编译，不启动 Unity。
function Resolve-UnityDataPath
{
    param([string]$RequestedUnityPath)

    if ($RequestedUnityPath)
    {
        $executable = [IO.Path]::GetFullPath($RequestedUnityPath)
        if (-not [IO.File]::Exists($executable)) { throw "Unity executable is missing: $executable" }
        return Join-Path ([IO.Path]::GetDirectoryName($executable)) "Data"
    }
    $registry = "HKLM:\SOFTWARE\Unity Technologies\Installer\Unity 6000.3.14f1"
    $location = (Get-ItemProperty -LiteralPath $registry -Name "Location x64")."Location x64"
    return Join-Path $location "Editor/Data"
}

# 使用 Unity 6000.3.14f1 托管引用离线编译新 Editor probe。
function Build-UnityProbeStatic
{
    param(
        [Parameter(Mandatory = $true)][string]$StaticRoot,
        [string]$RequestedUnityPath
    )

    $data = Resolve-UnityDataPath $RequestedUnityPath
    $csc = Join-Path $data "DotNetSdkRoslyn/csc.dll"
    $referenceRoot = Join-Path $data "NetStandard/ref/2.1.0"
    $references = @(Get-ChildItem -LiteralPath $referenceRoot -Filter "*.dll" -File |
        ForEach-Object { "/reference:" + $_.FullName })
    $references += @(
        "Managed/UnityEngine/UnityEngine.dll",
        "Managed/UnityEngine/UnityEngine.CoreModule.dll",
        "Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll",
        "Managed/UnityEditor.dll"
    ) | ForEach-Object { "/reference:" + (Join-Path $data $_) }
    $output = Join-Path $StaticRoot "D0M2SSourceGeneratorUnityProbe.dll"
    $source = Join-Path $PSScriptRoot "FixtureOverlay~/Assets/Editor/D0M2SSourceGeneratorUnityProbe.cs"
    $compilerOutput = @(& dotnet $csc /nologo /noconfig /nostdlib+ /target:library /langversion:9.0 `
        "/out:$output" @references $source 2>&1)
    $compilerExitCode = $LASTEXITCODE
    if ($compilerExitCode -ne 0 -or -not [IO.File]::Exists($output))
    {
        throw "D0M2S Unity probe static compile failed (exit=$compilerExitCode): $($compilerOutput -join ' | ')"
    }
    return $output
}

$staticRoot = New-StaticRoot
try
{
    $runner = Join-Path $PSScriptRoot "Run-D0M2S-SourceGeneratorFaultExperiment.ps1"
    $generator = Join-Path $PSScriptRoot "Generator~/D0M2SCanaryGenerator.cs"
    $overlay = Join-Path $PSScriptRoot "FixtureOverlay~"
    Test-RunnerContract $runner
    Test-GeneratorContract $generator
    Test-OverlayContract $overlay
    $generatorBuild = Build-GeneratorStatic $staticRoot
    $probeBuild = Build-UnityProbeStatic $staticRoot $UnityPath
    [pscustomobject][ordered]@{
        Schema = "D0M2S-SourceGeneratorStatic-v1"; Passed = $true
        RunnerAstPassed = $true; GeneratorContractPassed = $true; OverlayContractPassed = $true
        GeneratorAssemblySha256 = $generatorBuild.Sha256
        UnityProbeLength = [long]([IO.FileInfo]::new($probeBuild)).Length
        UnityStarted = $false
    } | ConvertTo-Json -Depth 10
}
finally
{
    Remove-StaticRoot $staticRoot
}
