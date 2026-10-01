Set-StrictMode -Version Latest

# 计算稳定的 SHA-256 文本摘要，供测试产物绑定脏工作区内容。
function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string]$Value)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        return [System.BitConverter]::ToString($sha256.ComputeHash($bytes)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

# 分离 Git stdout 与 stderr，避免 PowerShell 5.1 将成功命令的换行提示升级为终止错误。
function Invoke-ProvenanceGit {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryPath,
        [Parameter(Mandatory = $true)][string[]]$GitArguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        # 固定输出真实 UTF-8 路径，避免中文 untracked 文件被八进制转义后无法交给 hash-object。
        $rawOutput = @(& git -c core.quotepath=false -C $RepositoryPath @GitArguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    $standardOutput = @($rawOutput |
            Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] } |
            ForEach-Object { [string]$_ })
    $standardError = @($rawOutput |
            Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } |
            ForEach-Object { $_.ToString() })
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $standardOutput
        Error = $standardError
    }
}

# 采集 HEAD、已跟踪改动与未忽略 untracked 内容指纹，避免测试结果脱离实际工作区。
function Get-TestRunProvenance {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryPath,
        [Parameter(Mandatory = $true)][string]$UnityExecutable,
        [Parameter(Mandatory = $true)][string]$TestPlatform,
        [Parameter(Mandatory = $true)][string]$TestFilter,
        [Parameter(Mandatory = $true)][string]$ResultPath,
        [Parameter(Mandatory = $true)][string]$LogPath
    )

    $headResult = Invoke-ProvenanceGit $RepositoryPath @("rev-parse", "HEAD")
    $headOutput = @($headResult.Output)
    if ($headResult.ExitCode -ne 0 -or $headOutput.Count -eq 0) {
        throw "无法读取 Git HEAD：$($headResult.Error -join [Environment]::NewLine)"
    }

    $statusResult = Invoke-ProvenanceGit $RepositoryPath @(
        "status", "--porcelain=v1", "--untracked-files=all")
    $statusLines = @($statusResult.Output)
    if ($statusResult.ExitCode -ne 0) {
        throw "无法读取 Git 工作区状态：$($statusResult.Error -join [Environment]::NewLine)"
    }

    $trackedPatchResult = Invoke-ProvenanceGit $RepositoryPath @(
        "diff", "--binary", "--no-ext-diff", "HEAD", "--")
    $trackedPatchLines = @($trackedPatchResult.Output)
    if ($trackedPatchResult.ExitCode -ne 0) {
        throw "无法计算 tracked diff 指纹：$($trackedPatchResult.Error -join [Environment]::NewLine)"
    }

    $untrackedResult = Invoke-ProvenanceGit $RepositoryPath @(
        "ls-files", "--others", "--exclude-standard")
    $untrackedPaths = @($untrackedResult.Output)
    if ($untrackedResult.ExitCode -ne 0) {
        throw "无法枚举 untracked 文件：$($untrackedResult.Error -join [Environment]::NewLine)"
    }

    $untrackedRecords = foreach ($relativePath in $untrackedPaths) {
        $blobResult = Invoke-ProvenanceGit $RepositoryPath @(
            "hash-object", "--no-filters", "--", $relativePath)
        $blobOutput = @($blobResult.Output)
        if ($blobResult.ExitCode -ne 0 -or $blobOutput.Count -eq 0) {
            throw "无法计算 untracked 文件指纹：$relativePath"
        }

        "{0}`t{1}" -f $relativePath, ([string]$blobOutput[0]).Trim()
    }

    $fingerprintSource = @(
        "STATUS"
        $statusLines
        "TRACKED_PATCH"
        $trackedPatchLines
        "UNTRACKED_BLOBS"
        $untrackedRecords
    ) -join "`n"

    return [ordered]@{
        SchemaVersion = 2
        CapturedAtUtc = [DateTime]::UtcNow.ToString("o")
        Head = ([string]$headOutput[0]).Trim()
        Dirty = $statusLines.Count -gt 0
        DirtyFingerprintSha256 = Get-Sha256Hex $fingerprintSource
        UnityExecutable = $UnityExecutable
        ProjectPath = $RepositoryPath
        TestPlatform = $TestPlatform
        TestFilter = $TestFilter
        ResultPath = $ResultPath
        LogPath = $LogPath
        ResultGenerated = $false
        LogValidated = $false
        UnityExitCode = $null
        CompletedAtUtc = $null
    }
}

# 拒绝 NUnit XML 无法表达的 Job 异常与 native container 泄漏，许可证握手噪声不在匹配集合内。
function Assert-UnityTestLogHealthy {
    param(
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][DateTime]$StartedAt
    )

    $logFile = Get-Item -LiteralPath $LogPath -ErrorAction SilentlyContinue
    if ($null -eq $logFile -or $logFile.LastWriteTime -lt $StartedAt) {
        throw "Unity 未生成新的日志：$LogPath。"
    }

    $fatalPatterns = @(
        "InvalidOperationException:",
        "NullReferenceException:",
        "IndexOutOfRangeException:",
        "ArgumentOutOfRangeException:",
        "ObjectDisposedException:",
        "Unhandled Exception",
        "native container may not be deallocated",
        "A Native Collection has not been disposed",
        "has not been disposed, resulting in a memory leak",
        "JobTempAlloc",
        "Leak Detected",
        "Fatal Error"
    )
    $failures = foreach ($pattern in $fatalPatterns) {
        Select-String -LiteralPath $LogPath -SimpleMatch -Pattern $pattern | ForEach-Object {
            [pscustomobject]@{
                Pattern = $pattern
                LineNumber = $_.LineNumber
                Line = $_.Line.Trim()
            }
        }
    }

    if (@($failures).Count -eq 0) {
        return
    }

    $evidence = @($failures | Select-Object -First 20 | ForEach-Object {
            "line $($_.LineNumber) [$($_.Pattern)] $($_.Line)"
        }) -join [Environment]::NewLine
    throw "Unity 日志包含 XML 结果未必捕获的致命异常或泄漏：$LogPath$([Environment]::NewLine)$evidence"
}

# 将 provenance 作为与 XML 同名的 JSON sidecar 持久化。
function Write-TestRunProvenance {
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$Provenance,
        [Parameter(Mandatory = $true)][string]$ProvenancePath
    )

    $json = $Provenance | ConvertTo-Json -Depth 4
    Set-Content -LiteralPath $ProvenancePath -Value $json -Encoding UTF8
}
