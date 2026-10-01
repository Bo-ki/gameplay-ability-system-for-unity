[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunId,
    [Parameter(Mandatory = $true)][string]$ContractPath,
    [Parameter(Mandatory = $true)][string]$FixtureTemplateRoot,
    [Parameter(Mandatory = $true)][string]$OwnedFixtureRoot,
    [Parameter(Mandatory = $true)][string]$EvidenceRoot,
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [Parameter(Mandatory = $true)][string]$AnalyzerPath,
    [Parameter(Mandatory = $true)][string]$SelectorAPath,
    [Parameter(Mandatory = $true)][string]$SelectorBPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [int]$TimeoutSeconds = 600
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 返回文件完整 bytes/length/SHA/Base64 快照；缺失也是显式状态。
function Get-D1E1FileSnapshot
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [IO.File]::Exists($Path))
    {
        if ([IO.Directory]::Exists($Path)) { throw "Expected file path is a directory: $Path" }
        return [pscustomobject][ordered]@{
            State = 'Missing'; Length = 0; Sha256 = $null; BytesBase64 = $null
        }
    }
    $bytes = [IO.File]::ReadAllBytes($Path)
    return [pscustomobject][ordered]@{
        State = 'Present'; Length = [long]$bytes.LongLength
        Sha256 = [Convert]::ToHexString(
            [Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        BytesBase64 = [Convert]::ToBase64String($bytes)
    }
}

# 从托管 PE 独立读取 bytes SHA 与 Module MVID，不加载被测程序集。
function Get-D1E1ManagedPeIdentity
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [IO.File]::Exists($Path))
    {
        return [pscustomobject]@{ State = 'Missing'; Sha256 = $null; Mvid = $null }
    }
    $snapshot = Get-D1E1FileSnapshot $Path
    $stream = [IO.File]::OpenRead($Path)
    $reader = [Reflection.PortableExecutable.PEReader]::new($stream)
    try
    {
        if (-not $reader.HasMetadata) { throw "Managed PE has no metadata: $Path" }
        $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($reader)
        $module = $metadata.GetModuleDefinition()
        $mvid = $metadata.GetGuid($module.Mvid).ToString('D')
        return [pscustomobject]@{
            State = 'Present'; Sha256 = [string]$snapshot.Sha256; Mvid = $mvid
        }
    }
    finally
    {
        $reader.Dispose()
        $stream.Dispose()
    }
}

# 以 UTF-8 无 BOM、LF、CreateNew 和 Flush(true) 写本 runner 的 JSON 证据。
function Write-D1E1FreshJson
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$Value
    )

    $parent = [IO.Path]::GetDirectoryName($Path)
    if ([string]::IsNullOrWhiteSpace($parent) -or [IO.File]::Exists($Path) -or
        [IO.Directory]::Exists($Path))
    {
        throw "Evidence path must be fresh: $Path"
    }
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $json = ($Value | ConvertTo-Json -Depth 64).Replace("`r`n", "`n") + "`n"
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
    $stream = [IO.FileStream]::new(
        $Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try
    {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally
    {
        $stream.Dispose()
    }
}

# 逐项复制 fixture template 到 fresh project，避免与既有 Library/Bee 合并。
function Copy-D1E1FixtureTemplate
{
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if ([IO.Directory]::Exists($Destination) -or [IO.File]::Exists($Destination))
    {
        throw "Unity fixture must be fresh: $Destination"
    }
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($entry in @(Get-ChildItem -LiteralPath $Source -Force))
    {
        Copy-Item -LiteralPath $entry.FullName -Destination $Destination -Recurse
    }
}

# 将 analyzer 与 selector frozen bytes 安装到 disposable production relative paths。
function Install-D1E1Inputs
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)]$Context,
        [Parameter(Mandatory = $true)][byte[]]$SelectorBytes
    )

    $analyzerTarget = Join-Path $ProjectRoot $Context.Contract.AnalyzerRelativePath
    $selectorTarget = Join-Path $ProjectRoot $Context.Contract.SelectorRelativePath
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($analyzerTarget)) | Out-Null
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($selectorTarget)) | Out-Null
    Copy-Item -LiteralPath $Context.AnalyzerPath -Destination $analyzerTarget
    [IO.File]::WriteAllBytes($selectorTarget, $SelectorBytes)
}

# 仅在 fresh fixture 内构造固定物理负例，不触碰 production checkout。
function Set-D1E1UnityMutation
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$Mutation,
        [Parameter(Mandatory = $true)]$Context
    )

    $selector = Join-Path $ProjectRoot $Context.Contract.SelectorRelativePath
    $analyzer = Join-Path $ProjectRoot $Context.Contract.AnalyzerRelativePath
    switch ($Mutation)
    {
        'ScaffoldMissing' {
            Remove-Item -LiteralPath (Join-Path $ProjectRoot `
                'Assets/GAS/Generated/CodeGen/Runtime/GasCodeGenSourceGeneratorRequired.cs') -Force
        }
        'ScaffoldByteDrift' {
            $path = Join-Path $ProjectRoot `
                'Assets/GAS/Generated/CodeGen/Runtime/GasCodeGenSourceGeneratorRequired.cs'
            [byte[]]$bytes = [IO.File]::ReadAllBytes($path) +
                [Text.Encoding]::UTF8.GetBytes("// drift`n")
            [IO.File]::WriteAllBytes($path, $bytes)
        }
        'SelectorMissing' { Remove-Item -LiteralPath $selector -Force }
        'SelectorCorrupt' {
            [IO.File]::WriteAllText(
                $selector, "not-a-canonical-selector`n", [Text.UTF8Encoding]::new($false))
        }
        'AnalyzerMissing' { Remove-Item -LiteralPath $analyzer -Force }
        'AnalyzerCorrupt' {
            [IO.File]::WriteAllBytes(
                $analyzer, [Text.Encoding]::ASCII.GetBytes('not-a-managed-analyzer'))
        }
        'DuplicateSelector' {
            $duplicateRoot = Join-Path $ProjectRoot 'Assets/D1E1Harness/Duplicate'
            [IO.Directory]::CreateDirectory($duplicateRoot) | Out-Null
            $duplicate = Join-Path $duplicateRoot `
                'Generation.GasCodeGenSourceGenerator.additionalfile'
            [IO.File]::WriteAllBytes($duplicate, [IO.File]::ReadAllBytes($selector))
            [IO.File]::WriteAllText(
                $duplicate + '.meta',
                "fileFormatVersion: 2`nguid: d1e10000000000000000000000000008`nDefaultImporter:`n  externalObjects: {}`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n",
                [Text.UTF8Encoding]::new($false))
        }
        'LegacyActiveGenCs' {
            $legacy = Join-Path $ProjectRoot $Context.Contract.LegacyActiveSourceRelativePath
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($legacy)) | Out-Null
            [IO.File]::WriteAllText(
                $legacy,
                "namespace GAS.Generated.CodeGen`n{`n    internal static class D1E1LegacyActiveAuthority { }`n}`n",
                [Text.UTF8Encoding]::new($false))
        }
        default { throw "Unknown Unity mutation: $Mutation" }
    }
}

# 以 ArgumentList 直接启动 Unity，捕获 exit/timeout/stdout/stderr 并只强杀 owned process tree。
function Invoke-D1E1UnityProcess
{
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][int]$TimeoutMilliseconds
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) { [void]$startInfo.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $startedUtc = [DateTime]::UtcNow.ToString('O')
    try
    {
        if (-not $process.Start()) { throw 'Unity process did not start.' }
        $processId = $process.Id
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutMilliseconds)
        if ($timedOut)
        {
            $process.Kill($true)
            [void]$process.WaitForExit(30000)
        }
        else
        {
            # 完成同步退出收尾，避免重定向流和 Unity 日志句柄仍处于释放窗口。
            $process.WaitForExit()
        }
        return [pscustomobject][ordered]@{
            StartedUtc = $startedUtc; CompletedUtc = [DateTime]::UtcNow.ToString('O')
            ProcessId = $processId; TimedOut = $timedOut
            ExitCode = $(if ($timedOut) { $null } else { $process.ExitCode })
            StdOut = $stdout.GetAwaiter().GetResult(); StdErr = $stderr.GetAwaiter().GetResult()
        }
    }
    finally
    {
        $process.Dispose()
    }
}

# 读取可选 raw JSON，并保留原始 bytes 身份与解析状态。
function Read-D1E1OptionalRaw
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [IO.File]::Exists($Path))
    {
        return [pscustomobject][ordered]@{ Exists = $false; Parseable = $false; Value = $null }
    }
    $snapshot = Get-D1E1FileSnapshot $Path
    try
    {
        $value = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
        return [pscustomobject][ordered]@{
            Exists = $true; Parseable = $true; Value = $value
            Length = $snapshot.Length; Sha256 = $snapshot.Sha256
            BytesBase64 = $snapshot.BytesBase64
        }
    }
    catch
    {
        return [pscustomobject][ordered]@{
            Exists = $true; Parseable = $false; Value = $null
            Length = $snapshot.Length; Sha256 = $snapshot.Sha256
            BytesBase64 = $snapshot.BytesBase64
        }
    }
}

# 在 Unity 异常退出后的短暂句柄释放窗口内重试读取完整日志。
function Read-D1E1UnityLog
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [int]$TimeoutMilliseconds = 5000
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    while ($true)
    {
        try
        {
            $stream = [IO.FileStream]::new(
                $Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
            try
            {
                $reader = [IO.StreamReader]::new(
                    $stream, [Text.Encoding]::UTF8, $true, 4096, $true)
                try { return $reader.ReadToEnd() }
                finally { $reader.Dispose() }
            }
            finally { $stream.Dispose() }
        }
        catch [IO.IOException]
        {
            if ([DateTime]::UtcNow -ge $deadline) { throw }
            Start-Sleep -Milliseconds 50
        }
    }
}

# 解析 fresh signal 的四行 exact wire 并绑定 run/case/invocation。
function Test-D1E1Signal
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedRunId,
        [Parameter(Mandatory = $true)][string]$ExpectedCaseId,
        [Parameter(Mandatory = $true)][string]$ExpectedInvocationId
    )

    if (-not [IO.File]::Exists($Path)) { return $false }
    $text = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8).Replace("`r`n", "`n")
    $expected = "EX-GAS-D1-E1-Signal-v1`n$ExpectedRunId`n$ExpectedCaseId`n$ExpectedInvocationId`n"
    return $text -ceq $expected
}

# 解析一个 RSP /switch:path 行并规范化为 fixture 内绝对路径。
function Resolve-D1E1RspPathArgument
{
    param(
        [Parameter(Mandatory = $true)][string]$Line,
        [Parameter(Mandatory = $true)][string]$SwitchName,
        [Parameter(Mandatory = $true)][string]$ProjectRoot
    )

    $prefixA = '-' + $SwitchName + ':'
    $prefixB = '/' + $SwitchName + ':'
    if (-not $Line.StartsWith($prefixA, [StringComparison]::OrdinalIgnoreCase) -and
        -not $Line.StartsWith($prefixB, [StringComparison]::OrdinalIgnoreCase))
    {
        return $null
    }
    $value = $Line.Substring($Line.IndexOf(':') + 1).Trim().Trim('"')
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }
    return [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($value)) {
        $value
    } else { Join-Path $ProjectRoot $value }))
}

# 将唯一 RSP /out PE 与当前加载 PE 绑定，拒绝残留 stale RSP。
function Get-D1E1RspOutputBinding
{
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$OutputPaths,
        [Parameter(Mandatory = $true)][string]$BeePrefix,
        [Parameter(Mandatory = $true)][string]$AssemblyName,
        $RawAssembly
    )

    $rsp = if ($OutputPaths.Count -eq 1) { Get-D1E1ManagedPeIdentity $OutputPaths[0] }
        else { [pscustomobject]@{ State = 'Missing'; Sha256 = $null; Mvid = $null } }
    $loaded = if ($null -ne $RawAssembly) {
        Get-D1E1ManagedPeIdentity ([string]$RawAssembly.LoadedLocation)
    } else { [pscustomobject]@{ State = 'Missing'; Sha256 = $null; Mvid = $null } }
    $matches = $OutputPaths.Count -eq 1 -and
        $OutputPaths[0].StartsWith($BeePrefix, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($OutputPaths[0]) -ceq ($AssemblyName + '.dll') -and
        [string]$rsp.State -ceq 'Present' -and [string]$loaded.State -ceq 'Present' -and
        [string]$rsp.Sha256 -ceq [string]$loaded.Sha256 -and
        [string]$rsp.Mvid -ceq [string]$loaded.Mvid
    return [pscustomobject]@{ Matches = $matches; Rsp = $rsp; Loaded = $loaded }
}

# 按 fixture Bee 根、目标程序集文件名与 /out: 绑定每个目标程序集的当前 RSP。
function Get-D1E1BeeRspEvidence
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        $RawValue,
        [Parameter(Mandatory = $true)]$Contract
    )

    $beeRoot = Join-Path $ProjectRoot 'Library/Bee'
    $beePrefix = [IO.Path]::GetFullPath($beeRoot) + [IO.Path]::DirectorySeparatorChar
    $expectedAnalyzer = [IO.Path]::GetFullPath((Join-Path $ProjectRoot $Contract.AnalyzerRelativePath))
    $expectedSelector = [IO.Path]::GetFullPath((Join-Path $ProjectRoot $Contract.SelectorRelativePath))
    $groups = [Collections.Generic.List[object]]::new()
    foreach ($assemblyName in @($Contract.TargetAssemblies | ForEach-Object { [string]$_ }))
    {
        $rawAssembly = if ($null -eq $RawValue) { $null } else {
            @($RawValue.Assemblies | Where-Object { [string]$_.Name -ceq $assemblyName }) |
                Select-Object -First 1
        }
        $outputPath = if ($null -eq $rawAssembly) { '' } else {
            [string]$rawAssembly.CompilationOutputPath
        }
        $rows = [Collections.Generic.List[object]]::new()
        $candidates = if ([IO.Directory]::Exists($beeRoot)) {
            @(Get-ChildItem -LiteralPath $beeRoot -Recurse -Filter ($assemblyName + '.rsp') `
                -File -ErrorAction SilentlyContinue | Sort-Object FullName)
        } else { @() }
        foreach ($candidate in $candidates)
        {
            $bytes = [IO.File]::ReadAllBytes($candidate.FullName)
            $content = [Text.Encoding]::UTF8.GetString($bytes)
            $lines = @($content -split "`r?`n")
            $analyzers = @($lines | Where-Object {
                $_ -match '^[/-]analyzer:' -and $_ -match 'GasCodeGenSourceGenerator\.dll'
            })
            $selectors = @($lines | Where-Object {
                $_ -match '^[/-]additionalfile:' -and
                $_ -match 'Generation\.GasCodeGenSourceGenerator\.additionalfile'
            })
            $outputs = @($lines | Where-Object { $_ -match '^[/-]out:' })
            $analyzerPaths = @($analyzers | ForEach-Object {
                Resolve-D1E1RspPathArgument $_ 'analyzer' $ProjectRoot
            })
            $selectorPaths = @($selectors | ForEach-Object {
                Resolve-D1E1RspPathArgument $_ 'additionalfile' $ProjectRoot
            })
            $outputPaths = @($outputs | ForEach-Object {
                Resolve-D1E1RspPathArgument $_ 'out' $ProjectRoot
            })
            $outputBinding = Get-D1E1RspOutputBinding `
                $outputPaths $beePrefix $assemblyName $rawAssembly
            $analyzerExact = $analyzerPaths.Count -eq 1 -and $analyzerPaths[0] -ceq $expectedAnalyzer
            $selectorExact = $selectorPaths.Count -eq 1 -and $selectorPaths[0] -ceq $expectedSelector
            $rows.Add([pscustomobject][ordered]@{
                Path = [IO.Path]::GetRelativePath($ProjectRoot, $candidate.FullName).Replace('\', '/')
                Length = [long]$bytes.LongLength
                Sha256 = [Convert]::ToHexString(
                    [Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
                AnalyzerArguments = $analyzers; SelectorArguments = $selectors
                AnalyzerPaths = $analyzerPaths; SelectorPaths = $selectorPaths
                OutputArguments = $outputs; OutputPaths = $outputPaths
                RspOutputIdentity = $outputBinding.Rsp
                LoadedOutputIdentity = $outputBinding.Loaded
                MatchesOutput = [bool]$outputBinding.Matches
                AnalyzerExact = [bool]$analyzerExact; SelectorExact = [bool]$selectorExact
            })
        }
        $current = @($rows | Where-Object {
            $_.MatchesOutput -and $_.AnalyzerExact -and $_.SelectorExact
        })
        $groups.Add([pscustomobject][ordered]@{
            Assembly = $assemblyName; OutputPath = $outputPath
            AnalyzerIdentity = Get-D1E1FileSnapshot $expectedAnalyzer
            SelectorIdentity = Get-D1E1FileSnapshot $expectedSelector
            Candidates = @($rows); CurrentCandidates = $current
        })
    }
    return @($groups)
}

# 扫描 source-like Bee 文件与目标输出 DLL，判断 negative 是否出现 marker projection。
function Get-D1E1AddSourceObservation
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)]$Contract
    )

    $hits = [Collections.Generic.List[string]]::new()
    $beeRoot = Join-Path $ProjectRoot 'Library/Bee'
    if ([IO.Directory]::Exists($beeRoot))
    {
        foreach ($file in @(Get-ChildItem -LiteralPath $beeRoot -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ceq [string]$Contract.MarkerHintName -or
                $_.Extension -in @('.cs', '.txt') }))
        {
            $content = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
            if ($file.Name -ceq [string]$Contract.MarkerHintName -or
                $content.Contains('class GasCodeGenSourceGeneratorMarker', [StringComparison]::Ordinal))
            {
                $hits.Add([IO.Path]::GetRelativePath($ProjectRoot, $file.FullName).Replace('\', '/'))
            }
        }
    }
    foreach ($assembly in @($Contract.TargetAssemblies | ForEach-Object { [string]$_ }))
    {
        $output = Join-Path $ProjectRoot ('Library/ScriptAssemblies/' + $assembly + '.dll')
        if (-not [IO.File]::Exists($output)) { continue }
        $binaryText = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($output))
        if ($binaryText.Contains('GasCodeGenSourceGeneratorMarker', [StringComparison]::Ordinal))
        {
            $hits.Add([IO.Path]::GetRelativePath($ProjectRoot, $output).Replace('\', '/'))
        }
    }
    return [pscustomobject][ordered]@{ Observed = $hits.Count -gt 0; Paths = @($hits) }
}

# 观察 direct-Unity fixture 中是否出现 Store intent/audit/archive，任何存在都视为 promotion 旁路。
function Get-D1E1PromotionObservation
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)]$Contract
    )

    $paths = @(
        [string]$Contract.IntentRelativePath,
        [string]$Contract.AuditRelativePath,
        [string]$Contract.ArchiveRelativePath
    )
    $observed = @($paths | Where-Object {
        [IO.File]::Exists((Join-Path $ProjectRoot $_)) -or
        [IO.Directory]::Exists((Join-Path $ProjectRoot $_))
    })
    return [pscustomobject][ordered]@{ Observed = $observed.Count -gt 0; Paths = $observed }
}

# 执行单次 Unity 并输出 raw/signal/process/RSP/output identity 的绑定记录。
function Invoke-D1E1UnityTracked
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][string]$ExpectedSelectorSha256,
        [Parameter(Mandatory = $true)]$Context
    )

    $invocationId = $CaseId + '-' + $Label + '-' + [Guid]::NewGuid().ToString('N')
    $evidence = Join-Path $Context.EvidenceRoot ($CaseId + '/' + $Label)
    [IO.Directory]::CreateDirectory($evidence) | Out-Null
    $rawPath = Join-Path $evidence 'unity.raw.json'
    $signalPath = Join-Path $evidence 'unity.signal'
    $logPath = Join-Path $evidence 'unity.log'
    foreach ($fresh in @($rawPath, $signalPath, $logPath))
    {
        if ([IO.File]::Exists($fresh) -or [IO.Directory]::Exists($fresh))
        {
            throw "Unity invocation path must be fresh: $fresh"
        }
    }
    $arguments = @(
        '-batchmode', '-nographics', '-projectPath', $ProjectRoot,
        '-executeMethod', 'GAS.Tests.D1.E1.D1E1UnityProbe.Run',
        '-d1e1Output', $rawPath, '-d1e1Signal', $signalPath,
        '-d1e1RunId', $Context.RunId, '-d1e1CaseId', $CaseId,
        '-d1e1InvocationId', $invocationId,
        '-d1e1ExpectedSelectorSha256', $ExpectedSelectorSha256,
        '-logFile', $logPath)
    $process = Invoke-D1E1UnityProcess $Context.UnityPath $arguments `
        ($Context.TimeoutSeconds * 1000)
    Write-D1E1FreshJson (Join-Path $evidence 'process.json') $process
    $raw = Read-D1E1OptionalRaw $rawPath
    $signalValid = Test-D1E1Signal $signalPath $Context.RunId $CaseId $invocationId
    $rsp = Get-D1E1BeeRspEvidence $ProjectRoot $raw.Value $Context.Contract
    Write-D1E1FreshJson (Join-Path $evidence 'bee-rsp.json') ([pscustomobject]@{
        Schema = 'EX-GAS-D1-E1-BeeRsp-v1'; RunId = $Context.RunId
        CaseId = $CaseId; InvocationId = $invocationId; Groups = $rsp
    })
    $outputs = [Collections.Generic.List[object]]::new()
    if ($raw.Parseable -and $null -ne $raw.Value.Assemblies)
    {
        foreach ($assembly in @($raw.Value.Assemblies))
        {
            $outputs.Add([pscustomobject][ordered]@{
                Name = [string]$assembly.Name
                Loaded = Get-D1E1FileSnapshot ([string]$assembly.LoadedLocation)
                Compilation = Get-D1E1FileSnapshot ([string]$assembly.CompilationOutputPath)
                LoadedPe = Get-D1E1ManagedPeIdentity ([string]$assembly.LoadedLocation)
                CompilationPe = Get-D1E1ManagedPeIdentity ([string]$assembly.CompilationOutputPath)
                Mvid = [string]$assembly.LoadedMvid
            })
        }
    }
    $result = [pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-UnityInvocation-v1'; RunId = $Context.RunId
        CaseId = $CaseId; Label = $Label; InvocationId = $invocationId
        ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot).Replace('\', '/')
        ExpectedSelectorSha256 = $ExpectedSelectorSha256; Process = $process
        Raw = $raw; SignalValid = $signalValid; BeeRsp = $rsp; Outputs = @($outputs)
        LogExists = [IO.File]::Exists($logPath); LogPath = $logPath.Replace('\', '/')
    }
    Write-D1E1FreshJson (Join-Path $evidence 'invocation.json') $result
    return $result
}

# 校验 positive raw、fresh signal、三程序集共同 marker、loaded identity 与唯一当前 RSP。
function Test-D1E1PositiveInvocation
{
    param(
        [Parameter(Mandatory = $true)]$Invocation,
        [Parameter(Mandatory = $true)]$Context
    )

    if ($Invocation.Process.TimedOut -or [int]$Invocation.Process.ExitCode -ne 0 -or
        -not $Invocation.Raw.Parseable -or -not $Invocation.SignalValid) { return $false }
    $raw = $Invocation.Raw.Value
    if ([string]$raw.Schema -cne 'EX-GAS-D1-E1-UnityRaw-v1' -or
        [string]$raw.RunId -cne $Context.RunId -or
        [string]$raw.CaseId -cne [string]$Invocation.CaseId -or
        [string]$raw.InvocationId -cne [string]$Invocation.InvocationId -or
        [int]$raw.ProcessId -ne [int]$Invocation.Process.ProcessId -or
        [string]$raw.ProjectRoot -cne [string]$Invocation.ProjectRoot -or
        [string]$raw.ExpectedSelectorSha256 -cne [string]$Invocation.ExpectedSelectorSha256 -or
        -not [bool]$raw.Passed -or @($raw.Assemblies).Count -ne 3) { return $false }
    $names = @($raw.Assemblies | ForEach-Object { [string]$_.Name } | Sort-Object)
    $expectedNames = @($Context.Contract.TargetAssemblies | ForEach-Object { [string]$_ } | Sort-Object)
    if ([string]::Join('|', $names) -cne [string]::Join('|', $expectedNames)) { return $false }
    $expectedSelectorPath = [IO.Path]::GetFullPath((Join-Path `
        $Invocation.ProjectRoot $Context.Contract.SelectorRelativePath)).Replace('\', '/')
    $manifestHash = [string]$raw.Assemblies[0].ArtifactManifestHash
    $inventoryHash = [string]$raw.Assemblies[0].SourceArtifactInventoryHash
    foreach ($assembly in @($raw.Assemblies))
    {
        $parsedMvid = [Guid]::Empty
        $output = @($Invocation.Outputs | Where-Object { [string]$_.Name -ceq [string]$assembly.Name })
        $additionalFiles = @($assembly.RoslynAdditionalFilePaths)
        if ([string]$assembly.TargetAssembly -cne [string]$assembly.Name -or
            [string]$assembly.SelectorSha256 -cne [string]$Invocation.ExpectedSelectorSha256 -or
            [string]$assembly.MarkerAssemblyName -cne [string]$assembly.Name -or
            [string]$assembly.ArtifactManifestHash -cne $manifestHash -or
            [string]$assembly.SourceArtifactInventoryHash -cne $inventoryHash -or
            $manifestHash -cnotmatch '^[0-9a-f]{64}$' -or
            $inventoryHash -cnotmatch '^[0-9a-f]{64}$' -or
            $additionalFiles.Count -ne 1 -or
            [string]$additionalFiles[0] -cne $expectedSelectorPath -or
            [string]$assembly.ExactSelectorPath -cne $expectedSelectorPath -or
            -not [IO.File]::Exists([string]$assembly.LoadedLocation) -or
            -not [IO.File]::Exists([string]$assembly.CompilationOutputPath) -or
            -not ([Guid]::TryParse([string]$assembly.LoadedMvid, [ref]$parsedMvid)) -or
            $output.Count -ne 1 -or [string]$output[0].LoadedPe.State -cne 'Present' -or
            [string]$output[0].CompilationPe.State -cne 'Present' -or
            [string]$output[0].LoadedPe.Sha256 -cne [string]$output[0].CompilationPe.Sha256 -or
            [string]$output[0].LoadedPe.Mvid -cne [string]$assembly.LoadedMvid -or
            [string]$output[0].CompilationPe.Mvid -cne [string]$assembly.LoadedMvid)
        {
            return $false
        }
    }
    foreach ($group in @($Invocation.BeeRsp))
    {
        if (@($group.CurrentCandidates).Count -ne 1 -or
            [string]$group.AnalyzerIdentity.State -cne 'Present' -or
            [string]$group.AnalyzerIdentity.Sha256 -cne [string]$Context.AnalyzerSha256 -or
            [string]$group.SelectorIdentity.State -cne 'Present' -or
            [string]$group.SelectorIdentity.Sha256 -cne [string]$Invocation.ExpectedSelectorSha256)
        {
            return $false
        }
    }
    return $true
}

# 校验 negative 的非零退出、期望 diagnostic/hard guard、零 AddSource/promotion 与 selector 不变。
function Test-D1E1NegativeInvocation
{
    param(
        [Parameter(Mandatory = $true)]$Spec,
        [Parameter(Mandatory = $true)]$Invocation,
        [Parameter(Mandatory = $true)]$SelectorBefore,
        [Parameter(Mandatory = $true)]$SelectorAfter,
        [Parameter(Mandatory = $true)]$AddSource,
        [Parameter(Mandatory = $true)]$Promotion
    )

    if ($Invocation.Process.TimedOut -or $null -eq $Invocation.Process.ExitCode -or
        [int]$Invocation.Process.ExitCode -eq 0 -or $AddSource.Observed -or
        $Promotion.Observed) { return $false }
    if ([string]$SelectorBefore.State -cne [string]$SelectorAfter.State -or
        [string]$SelectorBefore.Sha256 -cne [string]$SelectorAfter.Sha256 -or
        [string]$SelectorBefore.BytesBase64 -cne [string]$SelectorAfter.BytesBase64)
    {
        return $false
    }
    if ($Invocation.Raw.Exists -and
        (-not $Invocation.Raw.Parseable -or [bool]$Invocation.Raw.Value.Passed)) { return $false }
    $log = if ($Invocation.LogExists) {
        Read-D1E1UnityLog ([string]$Invocation.LogPath)
    } else { '' }
    if ($Spec.PSObject.Properties['ExpectedDiagnostic'] -and
        -not $log.Contains([string]$Spec.ExpectedDiagnostic, [StringComparison]::Ordinal))
    {
        return $false
    }
    if ($Spec.PSObject.Properties['RequireHardGuard'] -and [bool]$Spec.RequireHardGuard)
    {
        $hasMarker = $log.Contains('GasCodeGenSourceGeneratorMarker', [StringComparison]::Ordinal)
        $hasCompilerError = $log -match 'error\s+CS(?:0103|0246|0234)'
        if (-not $hasMarker -or -not $hasCompilerError) { return $false }
    }
    return $true
}

# 执行一个 fresh positive/negative direct-Unity fixed case。
function Invoke-D1E1UnityCase
{
    param(
        [Parameter(Mandatory = $true)]$Spec,
        [Parameter(Mandatory = $true)]$Context
    )

    $caseId = [string]$Spec.CaseId
    $project = Join-Path $Context.OwnedFixtureRoot ('unity-' + $caseId)
    Copy-D1E1FixtureTemplate $Context.FixtureTemplateRoot $project
    Install-D1E1Inputs $project $Context $Context.SelectorBBytes
    if ([string]$Spec.Mode -ceq 'Negative')
    {
        Set-D1E1UnityMutation $project ([string]$Spec.Mutation) $Context
    }
    $selectorPath = Join-Path $project $Context.Contract.SelectorRelativePath
    $before = Get-D1E1FileSnapshot $selectorPath
    $expectedSha = if ([string]$before.State -ceq 'Present') {
        [string]$before.Sha256
    } else { $Context.SelectorBSha256 }
    $invocation = Invoke-D1E1UnityTracked $project $caseId 'only' $expectedSha $Context
    $after = Get-D1E1FileSnapshot $selectorPath
    $addSource = Get-D1E1AddSourceObservation $project $Context.Contract
    $promotion = Get-D1E1PromotionObservation $project $Context.Contract
    $passed = if ([string]$Spec.Mode -ceq 'Positive') {
        (Test-D1E1PositiveInvocation $invocation $Context) -and
            [string]$before.BytesBase64 -ceq [string]$after.BytesBase64 -and
            -not $promotion.Observed
    } else {
        Test-D1E1NegativeInvocation $Spec $invocation $before $after $addSource $promotion
    }
    return [pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-UnityCase-v1'; RunId = $Context.RunId
        CaseId = $caseId; Name = [string]$Spec.Name; Passed = $passed
        SelectorBefore = $before; SelectorAfter = $after; Invocation = $invocation
        AddSource = $addSource; Promotion = $promotion
        Detail = $(if ($passed) { 'Direct-Unity case satisfied all invariants.' } else { 'Direct-Unity case invariant failed.' })
    }
}

# 在同一 fresh project 上执行 warm A -> B，拒绝 stale A Bee/RSP 决定当前 marker。
function Invoke-D1E1WarmSwitchCase
{
    param(
        [Parameter(Mandatory = $true)]$Spec,
        [Parameter(Mandatory = $true)]$Context
    )

    $caseId = [string]$Spec.CaseId
    $project = Join-Path $Context.OwnedFixtureRoot ('unity-' + $caseId)
    Copy-D1E1FixtureTemplate $Context.FixtureTemplateRoot $project
    Install-D1E1Inputs $project $Context $Context.SelectorABytes
    $selectorPath = Join-Path $project $Context.Contract.SelectorRelativePath
    $first = Invoke-D1E1UnityTracked $project $caseId 'warm-a' $Context.SelectorASha256 $Context
    [IO.File]::WriteAllBytes($selectorPath, $Context.SelectorBBytes)
    $second = Invoke-D1E1UnityTracked $project $caseId 'switch-b' $Context.SelectorBSha256 $Context
    $firstPassed = Test-D1E1PositiveInvocation $first $Context
    $secondPassed = Test-D1E1PositiveInvocation $second $Context
    $mvidChanged = $firstPassed -and $secondPassed
    if ($mvidChanged)
    {
        foreach ($name in @($Context.Contract.TargetAssemblies | ForEach-Object { [string]$_ }))
        {
            $a = @($first.Raw.Value.Assemblies | Where-Object { [string]$_.Name -ceq $name })[0]
            $b = @($second.Raw.Value.Assemblies | Where-Object { [string]$_.Name -ceq $name })[0]
            $mvidChanged = $mvidChanged -and [string]$a.LoadedMvid -cne [string]$b.LoadedMvid
        }
    }
    $selectorAfter = Get-D1E1FileSnapshot $selectorPath
    $promotion = Get-D1E1PromotionObservation $project $Context.Contract
    $passed = $firstPassed -and $secondPassed -and $mvidChanged -and
        [string]$selectorAfter.Sha256 -ceq $Context.SelectorBSha256 -and
        -not $promotion.Observed
    return [pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-E1-UnityCase-v1'; RunId = $Context.RunId
        CaseId = $caseId; Name = [string]$Spec.Name; Passed = $passed
        WarmA = $first; SwitchB = $second; OutputMvidChanged = $mvidChanged
        SelectorAfter = $selectorAfter; Promotion = $promotion
        Detail = $(if ($passed) { 'Warm A cache could not select after B.' } else { 'Warm A/B binding invariant failed.' })
    }
}

$contract = Get-Content -LiteralPath $ContractPath -Raw -Encoding UTF8 | ConvertFrom-Json
$resolvedUnity = [IO.Path]::GetFullPath($UnityPath)
if (-not [IO.File]::Exists($resolvedUnity)) { throw "Unity executable is missing: $resolvedUnity" }
$selectorABytes = [IO.File]::ReadAllBytes([IO.Path]::GetFullPath($SelectorAPath))
$selectorBBytes = [IO.File]::ReadAllBytes([IO.Path]::GetFullPath($SelectorBPath))
if ([Convert]::ToBase64String($selectorABytes) -ceq [Convert]::ToBase64String($selectorBBytes))
{
    throw 'Selector A and B must differ.'
}
[IO.Directory]::CreateDirectory($OwnedFixtureRoot) | Out-Null
[IO.Directory]::CreateDirectory($EvidenceRoot) | Out-Null
$context = [pscustomobject]@{
    RunId = $RunId; Contract = $contract
    FixtureTemplateRoot = [IO.Path]::GetFullPath($FixtureTemplateRoot)
    OwnedFixtureRoot = [IO.Path]::GetFullPath($OwnedFixtureRoot)
    EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
    UnityPath = $resolvedUnity; AnalyzerPath = [IO.Path]::GetFullPath($AnalyzerPath)
    AnalyzerSha256 = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData(
            [IO.File]::ReadAllBytes([IO.Path]::GetFullPath($AnalyzerPath)))).ToLowerInvariant()
    SelectorABytes = $selectorABytes; SelectorBBytes = $selectorBBytes
    SelectorASha256 = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($selectorABytes)).ToLowerInvariant()
    SelectorBSha256 = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($selectorBBytes)).ToLowerInvariant()
    TimeoutSeconds = $TimeoutSeconds
}
$results = [Collections.Generic.List[object]]::new()
foreach ($spec in @($contract.UnityCases | Where-Object { [string]$_.Mode -in @('Positive', 'Negative', 'WarmSwitch') }))
{
    $result = if ([string]$spec.Mode -ceq 'WarmSwitch') {
        Invoke-D1E1WarmSwitchCase $spec $context
    } else { Invoke-D1E1UnityCase $spec $context }
    $results.Add($result)
    Write-D1E1FreshJson (Join-Path $EvidenceRoot ([string]$spec.CaseId + '/summary.json')) $result
}
$matrix = [pscustomobject][ordered]@{
    Schema = 'EX-GAS-D1-E1-UnityMatrix-v1'; RunId = $RunId
    Passed = @($results | Where-Object { -not $_.Passed }).Count -eq 0
    CaseCount = $results.Count; Cases = @($results)
}
Write-D1E1FreshJson $OutputPath $matrix
if (-not $matrix.Passed) { exit 51 }
