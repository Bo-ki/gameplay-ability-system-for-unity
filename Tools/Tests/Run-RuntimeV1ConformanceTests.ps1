[CmdletBinding()]
param(
    [string]$UnityExe = "",
    [string]$ManifestPath = "Tools/Tests/RuntimeV1TierBManifest.json",
    [string]$TestResultsPath = "TestResults/RuntimeV1Conformance.xml",
    [string]$LogFile = "Logs/RuntimeV1Conformance.log",
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
. (Join-Path $PSScriptRoot "TestRunProvenance.ps1")

# 解析 Unity 路径，优先使用项目记录版本对应的本机 Editor。
function Resolve-UnityExecutable {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if ($RequestedPath -eq "Unity.exe") {
            return $RequestedPath
        }

        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    $versionPath = Join-Path $projectPath "ProjectSettings\ProjectVersion.txt"
    $versionLine = Select-String -LiteralPath $versionPath -Pattern "^m_EditorVersion:" |
        Select-Object -First 1
    if ($null -eq $versionLine) {
        throw "无法从 $versionPath 读取 Unity 版本。"
    }

    $version = $versionLine.Line.Split(":", 2)[1].Trim()
    $knownPaths = @(
        "C:\Soft\Unity\Editor\$version\Editor\Unity.exe",
        "E:\Unity\UnityEditor\$version\Editor\Unity.exe"
    )
    foreach ($knownPath in $knownPaths) {
        if (Test-Path -LiteralPath $knownPath) {
            return $knownPath
        }
    }

    return "Unity.exe"
}

# 将相对输入解析到项目根目录，绝对路径保持不变。
function Resolve-ProjectPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $projectPath $Path))
}

# 为 EditMode、PlayMode 派生互不覆盖的结果或日志路径。
function Get-PlatformArtifactPath {
    param(
        [Parameter(Mandatory = $true)][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$Platform
    )

    $directory = Split-Path -Parent $BasePath
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($BasePath)
    $extension = [System.IO.Path]::GetExtension($BasePath)
    return Join-Path $directory ($fileName + "." + $Platform + $extension)
}

# 验证 required 字符串字段，避免空白元数据进入治理门。
function Assert-ManifestText {
    param(
        [Parameter(Mandatory = $true)][string]$FieldName,
        [AllowNull()][object]$Value,
        [Parameter(Mandatory = $true)][string]$VectorId
    )

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        throw "$VectorId 缺少 manifest 字段：$FieldName。"
    }
}

# 验证测试证据元数据、项目内路径与源码 SHA-256。
function Assert-TestEvidence {
    param(
        [Parameter(Mandatory = $true)][object]$Test,
        [Parameter(Mandatory = $true)][string]$VectorId
    )

    Assert-ManifestText "TestAssembly" $Test.TestAssembly $VectorId
    Assert-ManifestText "TestPlatform" $Test.TestPlatform $VectorId
    Assert-ManifestText "TestSourcePath" $Test.TestSourcePath $VectorId
    Assert-ManifestText "TestSourceHash" $Test.TestSourceHash $VectorId
    Assert-ManifestText "TestId" $Test.TestId $VectorId
    Assert-ManifestText "ExpectedResult" $Test.ExpectedResult $VectorId
    if (@("EditMode", "PlayMode") -notcontains [string]$Test.TestPlatform) {
        throw "$VectorId 的 TestPlatform 非法：$($Test.TestPlatform)。"
    }
    if (@("Passed", "Failed") -notcontains [string]$Test.ExpectedResult) {
        throw "$VectorId 的 ExpectedResult 非法：$($Test.ExpectedResult)。"
    }
    if ([int]$Test.ExpectedCaseCount -le 0) {
        throw "$VectorId 的 ExpectedCaseCount 必须大于 0：$($Test.TestId)。"
    }

    $sourcePath = Resolve-ProjectPath ([string]$Test.TestSourcePath)
    $projectPrefix = $projectPath.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $sourcePath.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$VectorId 的 TestSourcePath 逃逸项目目录：$($Test.TestSourcePath)。"
    }
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "$VectorId 的 TestSourcePath 不存在：$($Test.TestSourcePath)。"
    }

    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash.ToLowerInvariant()
    if ($actualHash -ne ([string]$Test.TestSourceHash).ToLowerInvariant()) {
        throw "$VectorId 的 TestSourceHash 已漂移：$($Test.TestSourcePath)。"
    }
}

# 验证三态状态契约；Pending 始终阻断，ApprovedRed 必须具备真实失败与批准记录。
function Assert-VectorStatusContract {
    param([Parameter(Mandatory = $true)][object]$Entry)

    $tests = @($Entry.Tests)
    if ($Entry.Status -eq "Green") {
        if ($tests.Count -eq 0 -or $tests.Where({ $_.ExpectedResult -ne "Passed" }).Count -gt 0) {
            throw "$($Entry.VectorId) Green 必须绑定至少一个全部期望 Passed 的真实 TestId。"
        }
        Assert-ManifestText "LastRunEvidenceId" $Entry.LastRunEvidenceId $Entry.VectorId
        return
    }

    if ($Entry.Status -eq "Pending") {
        if ($Entry.ReviewRequired -ne $true) {
            throw "$($Entry.VectorId) Pending 必须显式 ReviewRequired=true。"
        }
        if ($tests.Where({ $_.ExpectedResult -eq "Failed" }).Count -gt 0) {
            throw "$($Entry.VectorId) Pending 不得声明已批准的 expected failure。"
        }
        return
    }

    if ($tests.Count -eq 0 -or $tests.Where({ $_.ExpectedResult -eq "Failed" }).Count -eq 0) {
        throw "$($Entry.VectorId) ApprovedRed 必须绑定至少一个真实 expected failure。"
    }
    Assert-ManifestText "ApprovedBy" $Entry.Approval.ApprovedBy $Entry.VectorId
    Assert-ManifestText "ApprovedAtUtc" $Entry.Approval.ApprovedAtUtc $Entry.VectorId
    Assert-ManifestText "ApprovalRecordId" $Entry.Approval.ApprovalRecordId $Entry.VectorId
    Assert-ManifestText "ApprovalRecordHash" $Entry.Approval.ApprovalRecordHash $Entry.VectorId
    Assert-ManifestText "ExpectedFailureSignature" $Entry.Approval.ExpectedFailureSignature $Entry.VectorId
}

# 读取唯一 Tier-B 执行子清单，并验证 scope、编号、状态、owner 与证据来源。
function Read-TierBManifest {
    param([Parameter(Mandatory = $true)][string]$Path)

    $manifest = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([int]$manifest.SchemaVersion -ne 2 -or [string]$manifest.ManifestId -ne "runtime-v1-tier-b") {
        throw "Runtime v1 Tier B manifest schema 或 ManifestId 不受支持。"
    }
    if ([string]$manifest.ManifestScope -ne "TierBExecutionSubManifest" -or
        $manifest.CanAuthorizeV0Exit -ne $false) {
        throw "Runtime v1 Tier B manifest 必须声明为不能单独授权 V0 退出的执行子清单。"
    }
    Assert-ManifestText "SemanticOwner" $manifest.SemanticOwner "manifest"
    $requiredMasterScopes = @(
        "TierA-LegacyCharacterization",
        "TierB-TargetSemanticConformance",
        "R3-ThirdRoundFamilies")
    $actualMasterScopes = @($manifest.RequiredMasterScopes)
    if ($actualMasterScopes.Count -ne $requiredMasterScopes.Count -or
        @(Compare-Object $requiredMasterScopes $actualMasterScopes).Count -ne 0) {
        throw "Runtime v1 Tier B manifest 未声明完整 master manifest scope。"
    }

    $vectors = @($manifest.Vectors)
    if ($vectors.Count -ne 17) {
        throw "Tier B manifest 向量数量为 $($vectors.Count)，期望 17。"
    }

    $observedVectorIds = @{}
    for ($index = 0; $index -lt $vectors.Count; $index++) {
        $entry = $vectors[$index]
        $expectedVectorId = "TB-{0:D2}" -f ($index + 1)
        if ([string]$entry.VectorId -ne $expectedVectorId -or $observedVectorIds.ContainsKey($expectedVectorId)) {
            throw "Tier B manifest VectorId 缺失、重复或乱序：期望 $expectedVectorId。"
        }
        $observedVectorIds[$expectedVectorId] = $true
        if (@("Green", "ApprovedRed", "Pending") -notcontains [string]$entry.Status) {
            throw "$expectedVectorId 使用非法状态：$($entry.Status)。"
        }
        if ($entry.RequiredPass -ne $true -or [int]$entry.Version -le 0) {
            throw "$expectedVectorId 必须 RequiredPass=true 且 Version>0。"
        }
        Assert-ManifestText "Description" $entry.Description $expectedVectorId
        Assert-ManifestText "OwnerTask" $entry.OwnerTask $expectedVectorId
        Assert-ManifestText "NextOwnerTask" $entry.NextOwnerTask $expectedVectorId
        Assert-ManifestText "StatusReason" $entry.StatusReason $expectedVectorId
        if ($null -eq $entry.Approval) {
            throw "$expectedVectorId 缺少 Approval 对象。"
        }

        $observedTestIds = @{}
        foreach ($test in @($entry.Tests)) {
            Assert-TestEvidence $test $expectedVectorId
            $testKey = [string]$test.TestPlatform + "|" + [string]$test.TestId
            if ($observedTestIds.ContainsKey($testKey)) {
                throw "$expectedVectorId 重复登记 TestId：$testKey。"
            }
            $observedTestIds[$testKey] = $true
        }
        Assert-VectorStatusContract $entry
    }

    return $manifest
}

# 去重跨向量共享的真实测试，并拒绝同一 TestId 的元数据冲突。
function Get-UniqueManifestTests {
    param([Parameter(Mandatory = $true)][object]$Manifest)

    $unique = @{}
    $ordered = @()
    foreach ($entry in @($Manifest.Vectors)) {
        foreach ($test in @($entry.Tests)) {
            $key = [string]$test.TestPlatform + "|" + [string]$test.TestId
            if (-not $unique.ContainsKey($key)) {
                $unique[$key] = $test
                $ordered += $test
                continue
            }

            $existing = $unique[$key]
            $fields = @("TestAssembly", "TestSourcePath", "TestSourceHash", "ExpectedCaseCount", "ExpectedResult")
            foreach ($field in $fields) {
                if ([string]$existing.$field -ne [string]$test.$field) {
                    throw "共享 TestId 的 manifest 元数据冲突：$key，字段=$field。"
                }
            }
        }
    }

    return $ordered
}

# 返回 ApprovedRed 对指定真实失败测试登记的唯一失败签名。
function Get-ExpectedFailureSignatures {
    param(
        [Parameter(Mandatory = $true)][object]$Manifest,
        [Parameter(Mandatory = $true)][string]$Platform,
        [Parameter(Mandatory = $true)][string]$TestId
    )

    $signatures = @()
    foreach ($entry in @($Manifest.Vectors)) {
        if ($entry.Status -ne "ApprovedRed") {
            continue
        }
        foreach ($test in @($entry.Tests)) {
            if ($test.TestPlatform -eq $Platform -and $test.TestId -eq $TestId -and
                $test.ExpectedResult -eq "Failed") {
                $signatures += [string]$entry.Approval.ExpectedFailureSignature
            }
        }
    }

    return @($signatures | Select-Object -Unique)
}

# 将 NUnit 参数化 case 归一为 manifest 使用的 Class.Method TestId。
function Get-NUnitCaseTestId {
    param([Parameter(Mandatory = $true)][System.Xml.XmlElement]$Case)

    return [string]$Case.classname + "." + [string]$Case.methodname
}

# 返回 test-case 所属 Unity 测试程序集，确保同名 TestId 没有从错误 assembly 冒充发现。
function Get-NUnitCaseAssemblyName {
    param([Parameter(Mandatory = $true)][System.Xml.XmlElement]$Case)

    $ancestor = $Case.ParentNode
    while ($null -ne $ancestor) {
        if ($ancestor.LocalName -eq "test-suite" -and [string]$ancestor.type -eq "Assembly") {
            return [System.IO.Path]::GetFileNameWithoutExtension([string]$ancestor.name)
        }
        $ancestor = $ancestor.ParentNode
    }

    throw "NUnit test-case 缺少 Assembly ancestor：$($Case.fullname)。"
}

# 精确校验一个平台的发现数量、结果及 ApprovedRed 失败签名。
function Assert-PlatformResults {
    param(
        [Parameter(Mandatory = $true)][xml]$Results,
        [Parameter(Mandatory = $true)][object[]]$Tests,
        [Parameter(Mandatory = $true)][object]$Manifest,
        [Parameter(Mandatory = $true)][string]$Platform
    )

    $cases = @($Results.SelectNodes("//*[local-name()='test-case']"))
    $expectedCaseCount = ($Tests | Measure-Object -Property ExpectedCaseCount -Sum).Sum
    if ($cases.Count -eq 0 -or $cases.Count -ne [int]$expectedCaseCount) {
        throw "$Platform 真实测试发现数为 $($cases.Count)，期望 $expectedCaseCount。"
    }

    $summaries = @()
    foreach ($test in $Tests) {
        $matches = @($cases | Where-Object {
                (Get-NUnitCaseTestId $_) -eq $test.TestId -and
                (Get-NUnitCaseAssemblyName $_) -eq $test.TestAssembly
            })
        if ($matches.Count -ne [int]$test.ExpectedCaseCount) {
            throw "$Platform Assembly=$($test.TestAssembly) TestId=$($test.TestId) 发现 $($matches.Count) case，期望 $($test.ExpectedCaseCount)。"
        }

        $unexpected = @($matches | Where-Object { [string]$_.result -ne [string]$test.ExpectedResult })
        if ($unexpected.Count -gt 0) {
            throw "$Platform TestId=$($test.TestId) 实际结果与 manifest ExpectedResult=$($test.ExpectedResult) 不一致。"
        }

        $failureMessages = @($matches | ForEach-Object {
                $message = $_.SelectSingleNode("./*[local-name()='failure']/*[local-name()='message']")
                if ($null -ne $message) { [string]$message.InnerText }
            })
        if ($test.ExpectedResult -eq "Failed") {
            $signatures = Get-ExpectedFailureSignatures $Manifest $Platform $test.TestId
            if ($signatures.Count -eq 0) {
                throw "$Platform TestId=$($test.TestId) 失败但没有 ApprovedRed failure signature。"
            }
            if ($failureMessages.Count -ne $matches.Count) {
                throw "$Platform TestId=$($test.TestId) 失败但缺少可校验的 failure message。"
            }
            foreach ($message in $failureMessages) {
                $matched = @($signatures | Where-Object {
                        $message.IndexOf($_, [System.StringComparison]::Ordinal) -ge 0
                    }).Count -gt 0
                if (-not $matched) {
                    throw "$Platform TestId=$($test.TestId) 失败信息未命中 ApprovedRed signature。"
                }
            }
        }

        $summaries += [pscustomobject][ordered]@{
            TestAssembly = [string]$test.TestAssembly
            TestId = [string]$test.TestId
            ExpectedResult = [string]$test.ExpectedResult
            ActualResult = [string]$matches[0].result
            CaseCount = $matches.Count
            FailureMessages = $failureMessages
        }
    }

    return $summaries
}

# 执行一个平台的 manifest 精确测试集，并生成与 XML 同名的 provenance。
function Invoke-PlatformEvidenceRun {
    param(
        [Parameter(Mandatory = $true)][string]$Unity,
        [Parameter(Mandatory = $true)][string]$Platform,
        [Parameter(Mandatory = $true)][object[]]$Tests,
        [Parameter(Mandatory = $true)][object]$Manifest,
        [Parameter(Mandatory = $true)][string]$ManifestHash,
        [Parameter(Mandatory = $true)][string]$ResultsPath,
        [Parameter(Mandatory = $true)][string]$LogPath
    )

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ResultsPath) | Out-Null
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogPath) | Out-Null
    $filter = ($Tests | ForEach-Object { [string]$_.TestId }) -join ";"
    $provenancePath = [System.IO.Path]::ChangeExtension($ResultsPath, ".provenance.json")
    $provenance = Get-TestRunProvenance -RepositoryPath $projectPath -UnityExecutable $Unity `
        -TestPlatform $Platform -TestFilter $filter -ResultPath $ResultsPath -LogPath $LogPath
    $provenance.ManifestSha256 = $ManifestHash
    $provenance.ManifestPath = $resolvedManifestPath
    Write-TestRunProvenance $provenance $provenancePath

    Write-Host "Runtime v1 Tier B evidence: platform=$Platform, tests=$($Tests.Count)"
    $startedAt = Get-Date
    $arguments = @(
        "-batchmode",
        "-projectPath", $projectPath,
        "-runTests",
        "-testPlatform", $Platform,
        "-testFilter", $filter,
        "-testResults", $ResultsPath,
        "-logFile", $LogPath
    )
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    $provenance.UnityExitCode = $process.ExitCode
    $provenance.CompletedAtUtc = [DateTime]::UtcNow.ToString("o")

    $resultFile = Get-Item -LiteralPath $ResultsPath -ErrorAction SilentlyContinue
    if ($null -eq $resultFile -or $resultFile.LastWriteTime -lt $startedAt) {
        Write-TestRunProvenance $provenance $provenancePath
        throw "$Platform 未生成新的结果 XML：$ResultsPath。"
    }
    $provenance.ResultGenerated = $true
    Assert-UnityTestLogHealthy -LogPath $LogPath -StartedAt $startedAt
    $provenance.LogValidated = $true

    [xml]$resultXml = Get-Content -LiteralPath $ResultsPath -Raw -Encoding UTF8
    $testSummaries = @(Assert-PlatformResults $resultXml $Tests $Manifest $Platform)
    $after = Get-TestRunProvenance -RepositoryPath $projectPath -UnityExecutable $Unity `
        -TestPlatform $Platform -TestFilter $filter -ResultPath $ResultsPath -LogPath $LogPath
    if ($after.Head -ne $provenance.Head -or
        $after.DirtyFingerprintSha256 -ne $provenance.DirtyFingerprintSha256) {
        throw "$Platform 测试运行期间 Git 工作区 fingerprint 发生变化，证据作废。"
    }

    $provenance.ResultSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $ResultsPath).Hash.ToLowerInvariant()
    $provenance.LogSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $LogPath).Hash.ToLowerInvariant()
    $provenance.Classification = "ManifestExpectedResultsMatched"
    Write-TestRunProvenance $provenance $provenancePath

    return [pscustomobject][ordered]@{
        Platform = $Platform
        UnityExitCode = $process.ExitCode
        Head = $provenance.Head
        DirtyFingerprintSha256 = $provenance.DirtyFingerprintSha256
        ResultPath = $ResultsPath
        ResultSha256 = $provenance.ResultSha256
        LogPath = $LogPath
        LogSha256 = $provenance.LogSha256
        ProvenancePath = $provenancePath
        Tests = $testSummaries
    }
}

$resolvedManifestPath = Resolve-ProjectPath $ManifestPath
$resolvedResultsPath = Resolve-ProjectPath $TestResultsPath
$resolvedLogPath = Resolve-ProjectPath $LogFile
$manifest = Read-TierBManifest $resolvedManifestPath
$manifestHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolvedManifestPath).Hash.ToLowerInvariant()
$unity = Resolve-UnityExecutable $UnityExe
$allTests = @(Get-UniqueManifestTests $manifest)
if ($ValidateOnly) {
    Write-Host "Runtime v1 Tier B manifest validation passed: vectors=$(@($manifest.Vectors).Count), uniqueTests=$($allTests.Count), sha256=$manifestHash"
    return
}

$runs = @()
foreach ($platform in @("EditMode", "PlayMode")) {
    $platformTests = @($allTests | Where-Object { $_.TestPlatform -eq $platform })
    if ($platformTests.Count -eq 0) {
        throw "Tier B manifest 未登记任何 $platform 真实测试。"
    }
    $platformResultsPath = Get-PlatformArtifactPath $resolvedResultsPath $platform
    $platformLogPath = Get-PlatformArtifactPath $resolvedLogPath $platform
    $runs += Invoke-PlatformEvidenceRun $unity $platform $platformTests $manifest $manifestHash `
        $platformResultsPath $platformLogPath
}

if ($runs.Count -ne 2 -or $runs[0].Head -ne $runs[1].Head -or
    $runs[0].DirtyFingerprintSha256 -ne $runs[1].DirtyFingerprintSha256) {
    throw "EditMode 与 PlayMode provenance 不属于同一 HEAD + dirty fingerprint。"
}

$resultByKey = @{}
foreach ($run in $runs) {
    foreach ($testResult in @($run.Tests)) {
        $resultByKey[$run.Platform + "|" + $testResult.TestId] = $testResult
    }
}

$gateBlocks = @()
$vectorSummaries = @()
foreach ($entry in @($manifest.Vectors)) {
    $evidenceResults = @()
    foreach ($test in @($entry.Tests)) {
        $key = $test.TestPlatform + "|" + $test.TestId
        if (-not $resultByKey.ContainsKey($key)) {
            throw "$($entry.VectorId) 缺少 runner 结果：$key。"
        }
        $evidenceResults += $resultByKey[$key]
    }
    if ($entry.Status -eq "Pending") {
        $gateBlocks += "$($entry.VectorId) Pending：$($entry.StatusReason)"
    }

    $vectorSummaries += [pscustomobject][ordered]@{
        VectorId = [string]$entry.VectorId
        Version = [int]$entry.Version
        OwnerTask = [string]$entry.OwnerTask
        NextOwnerTask = [string]$entry.NextOwnerTask
        Status = [string]$entry.Status
        StatusReason = [string]$entry.StatusReason
        ReviewRequired = [bool]$entry.ReviewRequired
        LastRunEvidenceId = [string]$entry.LastRunEvidenceId
        EvidenceResults = $evidenceResults
    }
}

$statusCounts = [ordered]@{}
foreach ($status in @("Green", "ApprovedRed", "Pending")) {
    $statusCounts[$status] = @($manifest.Vectors | Where-Object { $_.Status -eq $status }).Count
}
$aggregatePath = [System.IO.Path]::ChangeExtension($resolvedResultsPath, ".evidence.json")
$aggregate = [ordered]@{
    SchemaVersion = 1
    EvidenceId = ""
    CapturedAtUtc = [DateTime]::UtcNow.ToString("o")
    ManifestPath = $resolvedManifestPath
    ManifestSha256 = $manifestHash
    ManifestVersion = [int]$manifest.ManifestVersion
    Head = $runs[0].Head
    DirtyFingerprintSha256 = $runs[0].DirtyFingerprintSha256
    GateOutcome = if ($gateBlocks.Count -eq 0) { "Passed" } else { "Blocked" }
    StatusCounts = $statusCounts
    Runs = $runs
    Vectors = $vectorSummaries
    GateBlocks = $gateBlocks
}
$aggregate.EvidenceId = Get-Sha256Hex ($aggregate | ConvertTo-Json -Depth 16 -Compress)
$aggregate | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $aggregatePath -Encoding UTF8

Write-Host "Runtime v1 Tier B aggregate evidence: $aggregatePath"
Write-Host "Status: Green=$($statusCounts.Green), ApprovedRed=$($statusCounts.ApprovedRed), Pending=$($statusCounts.Pending)"
if ($gateBlocks.Count -gt 0) {
    throw "Runtime v1 Tier B conformance gate blocked。$([Environment]::NewLine)$($gateBlocks -join [Environment]::NewLine)"
}

Write-Host "Runtime v1 Tier B conformance gate passed。"
