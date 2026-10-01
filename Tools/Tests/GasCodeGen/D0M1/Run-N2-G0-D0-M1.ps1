[CmdletBinding()]
param(
    [string]$UnityPath = "",
    [string]$OutputPath = "TestResults/GasCodeGen/N2-G0-D0-M1.aggregate.json",
    [switch]$KeepFixtures,
    [string]$WorkerMode = "",
    [string]$WorkerRoot = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:PackageName = "com.exhard.exgas.d0m1-slot"
$script:GenerationA = "generation-a"
$script:GenerationB = "generation-b"
$script:OwnerSentinelName = ".d0m1-owner"
$script:SuitePrefix = "gas-codegen-d0-m1-"
$script:MaxAggregateBytes = 64 * 1024 * 1024

if (-not ("D0M1NativeFileInfo" -as [type]))
{
    Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

/// <summary>读取已打开 Windows 文件句柄的稳定链接计数。</summary>
public static class D0M1NativeFileInfo
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    /// <summary>返回句柄指向文件的 hardlink 数量，并在查询失败时显式抛错。</summary>
    public static uint GetLinkCount(SafeFileHandle file)
    {
        ByHandleFileInformation information;
        if (!GetFileInformationByHandle(file, out information))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return information.NumberOfLinks;
    }
}
"@
}

function Resolve-NormalizedPath
{
    param([Parameter(Mandatory = $true)][string]$Path)

    return [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
}

function Write-Utf8NoBomLf
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $normalized = $Content.Replace("`r`n", "`n").Replace("`r", "`n")
    [IO.File]::WriteAllText($Path, $normalized, [Text.UTF8Encoding]::new($false))
}

function Write-Utf8NoBomLfCreateNew
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    if ([IO.Directory]::Exists($Path)) { throw "CreateNew target is a directory: $Path" }
    if ([IO.File]::Exists($Path)) { [IO.File]::Delete($Path) }
    if ([IO.File]::Exists($Path) -or [IO.Directory]::Exists($Path)) { throw "CreateNew target survived exact unlink: $Path" }
    $normalized = $Content.Replace("`r`n", "`n").Replace("`r", "`n")
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($normalized)
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
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

function Get-BytesSha256
{
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][byte[]]$Bytes)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try
    {
        return ([BitConverter]::ToString($algorithm.ComputeHash($Bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally
    {
        $algorithm.Dispose()
    }
}

function Get-FileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    return Get-BytesSha256 -Bytes ([IO.File]::ReadAllBytes($Path))
}

function Get-SharedFileSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    $share = [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, $share)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try
    {
        return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace("-", "").ToLowerInvariant()
    }
    finally
    {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

function Get-TreeSha256
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not [IO.Directory]::Exists($Path))
    {
        return Get-BytesSha256 -Bytes ([Text.Encoding]::UTF8.GetBytes("<missing>"))
    }

    $root = Resolve-NormalizedPath -Path $Path
    $entries = [Collections.Generic.List[string]]::new()
    $entries.Add("D`t.`t" + [int][IO.File]::GetAttributes($root))
    foreach ($directory in [IO.Directory]::EnumerateDirectories($root, "*", [IO.SearchOption]::AllDirectories))
    {
        $relative = [IO.Path]::GetRelativePath($root, $directory).Replace("\", "/")
        $attributes = [int][IO.File]::GetAttributes($directory)
        $entries.Add("D`t" + $relative + "`t" + $attributes)
    }
    foreach ($file in [IO.Directory]::EnumerateFiles($root, "*", [IO.SearchOption]::AllDirectories))
    {
        $relative = [IO.Path]::GetRelativePath($root, $file).Replace("\", "/")
        $length = [IO.FileInfo]::new($file).Length
        $attributes = [int][IO.File]::GetAttributes($file)
        $entries.Add("F`t" + $relative + "`t" + $attributes + "`t" + $length + "`t" + (Get-FileSha256 -Path $file))
    }

    $ordered = $entries.ToArray()
    [Array]::Sort($ordered, [StringComparer]::Ordinal)
    return Get-BytesSha256 -Bytes ([Text.Encoding]::UTF8.GetBytes([string]::Join("`n", $ordered)))
}

function Get-ProtocolSnapshot
{
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)

    $relativePaths = @(
        "Assets/GAS/Editor/CodeGen/Core",
        "Assets/GAS/Editor/CodeGen/Semantics",
        "Assets/GAS/Editor/CodeGen/Proofs",
        "Assets/GAS/Editor/CodeGen/GasCodeGenPipeline.cs",
        "Assets/GAS/Editor/CodeGen/Phases/AutoChessDemoCodeGenPhase.cs",
        "Tools/GasCodeGenCli",
        "Tools/CodeGen",
        "ProjectSettings/GasCodeGen",
        "Assets/GAS/Generated/CodeGen",
        "Assets/GAS/Generated/CodeGen.meta",
        "Assets/AutoChessDemo/Generated",
        "Assets/AutoChessDemo/Generated.meta",
        "Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef",
        "Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef.meta",
        "Assets/GAS/Runtime/V1/Install",
        "Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleDefinitionCatalogBuilder.cs",
        "Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleDefinitionCatalogBuilder.cs.meta",
        "Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeAccess.cs",
        "Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeAccess.cs.meta",
        "Packages/manifest.json",
        "Packages/packages-lock.json"
    )
    $entries = [Collections.Generic.List[object]]::new()
    foreach ($relativePath in $relativePaths)
    {
        $fullPath = Join-Path $ProjectRoot $relativePath
        $sha256 = if ([IO.File]::Exists($fullPath)) { Get-FileSha256 -Path $fullPath } else { Get-TreeSha256 -Path $fullPath }
        $entries.Add([pscustomobject]@{ Path = $relativePath; Sha256 = $sha256 })
    }

    $lines = $entries | ForEach-Object { $_.Path + "`t" + $_.Sha256 }
    return [pscustomobject]@{
        AggregateSha256 = Get-BytesSha256 -Bytes ([Text.Encoding]::UTF8.GetBytes([string]::Join("`n", $lines)))
        Entries = $entries.ToArray()
    }
}

function Copy-DirectoryTree
{
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($directory in [IO.Directory]::EnumerateDirectories($Source, "*", [IO.SearchOption]::AllDirectories))
    {
        $relative = [IO.Path]::GetRelativePath($Source, $directory)
        [IO.Directory]::CreateDirectory((Join-Path $Destination $relative)) | Out-Null
    }
    foreach ($file in [IO.Directory]::EnumerateFiles($Source, "*", [IO.SearchOption]::AllDirectories))
    {
        $relative = [IO.Path]::GetRelativePath($Source, $file)
        [IO.File]::Copy($file, (Join-Path $Destination $relative), $true)
    }
}

function New-IsolatedSuiteRoot
{
    $name = $script:SuitePrefix + [Guid]::NewGuid().ToString("N")
    $root = Join-Path ([IO.Path]::GetTempPath()) $name
    if ([IO.Directory]::Exists($root) -or [IO.File]::Exists($root)) { throw "D0-M1 fixture root unexpectedly exists: $root" }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    try
    {
        Write-Utf8NoBomLfCreateNew -Path (Join-Path $root $script:OwnerSentinelName) -Content ($name + "`n")
        return Resolve-NormalizedPath -Path $root
    }
    catch
    {
        $resolvedRoot = Resolve-NormalizedPath -Path $root
        $tempRoot = Resolve-NormalizedPath -Path ([IO.Path]::GetTempPath())
        $ordinaryRoot = [IO.Directory]::Exists($resolvedRoot) -and ([IO.File]::GetAttributes($resolvedRoot) -band [IO.FileAttributes]::ReparsePoint) -eq 0
        if ($ordinaryRoot -and (Resolve-NormalizedPath -Path ([IO.Path]::GetDirectoryName($resolvedRoot))).Equals($tempRoot, [StringComparison]::OrdinalIgnoreCase))
        {
            $entries = @([IO.Directory]::EnumerateFileSystemEntries($resolvedRoot))
            $sentinel = Join-Path $resolvedRoot $script:OwnerSentinelName
            if ($entries.Count -eq 1 -and $entries[0] -eq $sentinel -and ([IO.File]::GetAttributes($sentinel) -band [IO.FileAttributes]::ReparsePoint) -eq 0) { [IO.File]::Delete($sentinel) }
            if (@([IO.Directory]::EnumerateFileSystemEntries($resolvedRoot)).Count -eq 0) { [IO.Directory]::Delete($resolvedRoot, $false) }
        }
        throw
    }
}

function Assert-SafeSuiteRoot
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $normalized = Resolve-NormalizedPath -Path $Root
    $temp = Resolve-NormalizedPath -Path ([IO.Path]::GetTempPath())
    if (-not (Resolve-NormalizedPath -Path ([IO.Path]::GetDirectoryName($normalized))).Equals($temp, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "D0-M1 fixture root escaped the exact temp boundary: $normalized"
    }
    if ([IO.Path]::GetFileName($normalized) -notmatch '^gas-codegen-d0-m1-[0-9a-f]{32}$')
    {
        throw "D0-M1 fixture root name is not owned by this harness: $normalized"
    }
    if (-not [IO.Directory]::Exists($normalized) -or ([IO.File]::GetAttributes($normalized) -band [IO.FileAttributes]::ReparsePoint) -ne 0)
    {
        throw "D0-M1 fixture root must be an existing ordinary directory: $normalized"
    }
    $sentinel = Join-Path $normalized $script:OwnerSentinelName
    $expected = [IO.Path]::GetFileName($normalized) + "`n"
    if (-not [IO.File]::Exists($sentinel) -or [IO.File]::ReadAllText($sentinel) -ne $expected)
    {
        throw "D0-M1 fixture owner sentinel is missing or invalid: $sentinel"
    }
}

function Assert-OwnedPathHasNoReparsePoint
{
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    Assert-SafeSuiteRoot -Root $Root
    $normalizedRoot = Resolve-NormalizedPath -Path $Root
    $normalizedPath = Resolve-NormalizedPath -Path $Path
    $prefix = $normalizedRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $normalizedPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "D0-M1 owned path escaped the fixture root: $normalizedPath"
    }

    $relative = $normalizedPath.Substring($prefix.Length)
    $segments = @($relative.Split(@('\', '/'), [StringSplitOptions]::RemoveEmptyEntries))
    $current = $normalizedRoot
    for ($index = 0; $index -lt $segments.Count; $index++)
    {
        $current = Join-Path $current $segments[$index]
        if (-not [IO.File]::Exists($current) -and -not [IO.Directory]::Exists($current)) { break }
        $attributes = [IO.File]::GetAttributes($current)
        if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "D0-M1 owned path contains a reparse point: $current" }
        if ($index -lt $segments.Count - 1 -and ($attributes -band [IO.FileAttributes]::Directory) -eq 0) { throw "D0-M1 owned parent path is a file: $current" }
    }
}

function Remove-IsolatedSuiteRoot
{
    param([Parameter(Mandatory = $true)][string]$Root)

    Assert-SafeSuiteRoot -Root $Root
    $entries = @(Get-ChildItem -LiteralPath $Root -Force -Recurse)
    foreach ($entry in $entries)
    {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "D0-M1 cleanup refuses a reparse point: $($entry.FullName)"
        }
    }

    $sentinel = Join-Path $Root $script:OwnerSentinelName
    foreach ($file in @($entries | Where-Object { -not $_.PSIsContainer -and $_.FullName -ne $sentinel }))
    {
        Invoke-DeleteWithRetry -Path $file.FullName -IsDirectory $false
    }
    foreach ($directory in @($entries | Where-Object { $_.PSIsContainer } | Sort-Object { $_.FullName.Length } -Descending))
    {
        Invoke-DeleteWithRetry -Path $directory.FullName -IsDirectory $true
    }
    [IO.File]::Delete($sentinel)
    [IO.Directory]::Delete($Root, $false)
}

function Invoke-DeleteWithRetry
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][bool]$IsDirectory
    )

    for ($attempt = 0; $attempt -lt 40; $attempt++)
    {
        try
        {
            if ($IsDirectory) { [IO.Directory]::Delete($Path, $false) } else { [IO.File]::Delete($Path) }
            return
        }
        catch [IO.IOException]
        {
            if ($attempt -eq 39) { throw }
            Start-Sleep -Milliseconds 250
        }
    }
}

function Resolve-UnityExecutable
{
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath))
    {
        $resolved = Resolve-NormalizedPath -Path $RequestedPath
        if (-not [IO.File]::Exists($resolved)) { throw "Unity executable does not exist: $resolved" }
        return $resolved
    }

    $registryPath = "HKLM:\SOFTWARE\Unity Technologies\Installer\Unity 6000.3.14f1"
    $location = (Get-ItemProperty -LiteralPath $registryPath -Name "Location x64")."Location x64"
    $resolved = Resolve-NormalizedPath -Path (Join-Path $location "Editor\Unity.exe")
    if (-not [IO.File]::Exists($resolved)) { throw "Unity 6000.3.14f1 was not found: $resolved" }
    return $resolved
}

function New-ProcessInfo
{
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $FilePath
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    return $info
}

function Invoke-ProcessAndWait
{
    param(
        [Parameter(Mandatory = $true)][Diagnostics.ProcessStartInfo]$StartInfo,
        [int]$TimeoutMilliseconds = 600000
    )

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $StartInfo
    $startedAt = [DateTime]::UtcNow
    $started = $false
    try
    {
        $started = $process.Start()
        if (-not $started) { throw "Failed to start process: $($StartInfo.FileName)" }
        $completed = $process.WaitForExit($TimeoutMilliseconds)
        if (-not $completed)
        {
            $process.Kill($true)
            if (-not $process.WaitForExit(30000))
            {
                throw "Process did not exit within 30 seconds after timeout kill: $($StartInfo.FileName)"
            }
        }
        return [pscustomobject]@{
            ExitCode = if ($completed) { $process.ExitCode } else { -1 }
            TimedOut = -not $completed
            DurationMilliseconds = [int]([DateTime]::UtcNow - $startedAt).TotalMilliseconds
        }
    }
    finally
    {
        try
        {
            if ($started -and -not $process.HasExited)
            {
                $process.Kill($true)
                if (-not $process.WaitForExit(30000)) { throw "Process cleanup did not complete: $($StartInfo.FileName)" }
            }
        }
        finally
        {
            $process.Dispose()
        }
    }
}

function Invoke-WorkerProcess
{
    param(
        [Parameter(Mandatory = $true)][string]$ScriptPath,
        [Parameter(Mandatory = $true)][string]$Mode,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $pwshPath = (Get-Process -Id $PID).Path
    $arguments = @("-NoProfile", "-File", $ScriptPath, "-WorkerMode", $Mode, "-WorkerRoot", $Root)
    return Invoke-ProcessAndWait -StartInfo (New-ProcessInfo -FilePath $pwshPath -Arguments $arguments) -TimeoutMilliseconds 30000
}

function Stop-WorkerAbruptly
{
    Stop-Process -Id $PID -Force
    throw "Worker termination unexpectedly returned."
}

function Invoke-InternalWorker
{
    param(
        [Parameter(Mandatory = $true)][string]$Mode,
        [Parameter(Mandatory = $true)][string]$Root
    )

    Assert-SafeSuiteRoot -Root $Root
    Assert-OwnedPathHasNoReparsePoint -Root $Root -Path (Join-Path $Root "UnityProject")
    Assert-OwnedPathHasNoReparsePoint -Root $Root -Path (Join-Path $Root "UnityProject\Packages")
    Assert-OwnedPathHasNoReparsePoint -Root $Root -Path (Join-Path $Root "Materializer")
    Assert-OwnedPathHasNoReparsePoint -Root $Root -Path (Join-Path $Root "Control")
    if ($Mode -eq "materializer-after-old-move")
    {
        $source = Join-Path $Root "UnityProject\Assets\GAS\Generated\CodeGen"
        $backup = Join-Path $Root "Materializer\Backup"
        Assert-OwnedPathHasNoReparsePoint -Root $Root -Path $source
        Assert-OwnedPathHasNoReparsePoint -Root $Root -Path $backup
        [IO.Directory]::Move($source, $backup)
        Stop-WorkerAbruptly
    }
    if ($Mode -eq "selector-before-replace")
    {
        Stop-WorkerAbruptly
    }
    if ($Mode -eq "selector-after-replace")
    {
        $packages = Join-Path $Root "UnityProject\Packages"
        $next = Join-Path $packages "manifest.json.next"
        $manifest = Join-Path $packages "manifest.json"
        $backup = Join-Path $packages "manifest.json.backup"
        Assert-OwnedPathHasNoReparsePoint -Root $Root -Path $next
        Assert-OwnedPathHasNoReparsePoint -Root $Root -Path $manifest
        Assert-OwnedPathHasNoReparsePoint -Root $Root -Path $backup
        [IO.File]::Replace($next, $manifest, $backup, $true)
        Stop-WorkerAbruptly
    }
    if ($Mode -eq "selector-reader")
    {
        Invoke-SelectorReaderWorker -Root $Root
        return
    }
    throw "Unsupported D0-M1 worker mode: $Mode"
}

function Invoke-SelectorReaderWorker
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $manifest = Join-Path $Root "UnityProject\Packages\manifest.json"
    $control = Join-Path $Root "Control"
    Assert-OwnedPathHasNoReparsePoint -Root $Root -Path $manifest
    Assert-OwnedPathHasNoReparsePoint -Root $Root -Path $control
    $readyPath = Join-Path $control "reader-ready"
    $resultPath = Join-Path $control "reader-result.json"
    Assert-OwnedPathHasNoReparsePoint -Root $Root -Path $readyPath
    Assert-OwnedPathHasNoReparsePoint -Root $Root -Path $resultPath
    $hashA = [IO.File]::ReadAllText((Join-Path $control "manifest-a.sha256"))
    $hashB = [IO.File]::ReadAllText((Join-Path $control "manifest-b.sha256"))
    $observations = 0
    $generationAObservations = 0
    $generationBObservations = 0
    $invalid = 0
    $transientReadRetries = 0
    $exhaustedReads = 0
    $maxConsecutiveRetries = 0
    Write-Utf8NoBomLfCreateNew -Path $readyPath -Content "ready`n"
    while (-not [IO.File]::Exists((Join-Path $control "stop-reader")))
    {
        $readCompleted = $false
        $consecutiveRetries = 0
        for ($attempt = 0; $attempt -lt 64; $attempt++)
        {
            try
            {
                $hash = Get-SharedFileSha256 -Path $manifest
                $observations++
                if ($hash -eq $hashA) { $generationAObservations++ }
                elseif ($hash -eq $hashB) { $generationBObservations++ }
                else { $invalid++ }
                $readCompleted = $true
                break
            }
            catch [IO.IOException]
            {
                $transientReadRetries++
                $consecutiveRetries++
                if ($consecutiveRetries -gt $maxConsecutiveRetries) { $maxConsecutiveRetries = $consecutiveRetries }
                Start-Sleep -Milliseconds 1
            }
        }
        if (-not $readCompleted) { $exhaustedReads++ }
    }
    $result = [pscustomobject]@{
        Observations = $observations
        GenerationAObservations = $generationAObservations
        GenerationBObservations = $generationBObservations
        InvalidHashes = $invalid
        TransientReadRetries = $transientReadRetries
        ExhaustedReads = $exhaustedReads
        MaxConsecutiveRetries = $maxConsecutiveRetries
        RetryLimit = 64
    }
    Write-Utf8NoBomLfCreateNew -Path $resultPath -Content (($result | ConvertTo-Json -Depth 4) + "`n")
}

function New-UnityProject
{
    param(
        [Parameter(Mandatory = $true)][string]$FixtureTemplate,
        [Parameter(Mandatory = $true)][string]$ProjectPath
    )

    Copy-DirectoryTree -Source $FixtureTemplate -Destination $ProjectPath
    [IO.Directory]::CreateDirectory((Join-Path $ProjectPath "Packages")) | Out-Null
    Write-Utf8NoBomLf -Path (Join-Path $ProjectPath "Packages\manifest.json") -Content "{`n  `"dependencies`": {}`n}`n"
}

function New-ImmutableSlot
{
    param(
        [Parameter(Mandatory = $true)][string]$TemplatePath,
        [Parameter(Mandatory = $true)][string]$SlotsRoot,
        [Parameter(Mandatory = $true)][string]$GenerationId
    )

    $slot = Join-Path $SlotsRoot $GenerationId
    Copy-DirectoryTree -Source $TemplatePath -Destination $slot
    foreach ($file in [IO.Directory]::EnumerateFiles($slot, "*.cs", [IO.SearchOption]::AllDirectories))
    {
        $content = [IO.File]::ReadAllText($file).Replace("__GENERATION__", $GenerationId)
        Write-Utf8NoBomLf -Path $file -Content $content
    }
    return $slot
}

function Get-ManifestContent
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$SlotPath
    )

    $packagesPath = Join-Path $ProjectPath "Packages"
    $uriPath = [IO.Path]::GetRelativePath($packagesPath, (Resolve-NormalizedPath -Path $SlotPath)).Replace("\", "/")
    return "{`n  `"dependencies`": {`n    `"$($script:PackageName)`": `"file:$uriPath`"`n  }`n}`n"
}

function Set-NextManifest
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $next = Join-Path $ProjectPath "Packages\manifest.json.next"
    Write-Utf8NoBomLf -Path $next -Content $Content
    return $next
}

function Replace-Manifest
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $packages = Join-Path $ProjectPath "Packages"
    $next = Set-NextManifest -ProjectPath $ProjectPath -Content $Content
    $backup = Join-Path $packages "manifest.json.swap-backup"
    if ([IO.File]::Exists($backup)) { [IO.File]::Delete($backup) }
    [IO.File]::Replace($next, (Join-Path $packages "manifest.json"), $backup, $true)
    [IO.File]::Delete($backup)
}

function Invoke-SelectorConcurrencyProbe
{
    param(
        [Parameter(Mandatory = $true)][string]$ScriptPath,
        [Parameter(Mandatory = $true)][string]$SuiteRoot,
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$ManifestA,
        [Parameter(Mandatory = $true)][string]$ManifestB
    )

    $control = Join-Path $SuiteRoot "Control"
    $stopPath = Join-Path $control "stop-reader"
    $readyPath = Join-Path $control "reader-ready"
    $resultPath = Join-Path $control "reader-result.json"
    if ([IO.File]::Exists($stopPath)) { [IO.File]::Delete($stopPath) }
    if ([IO.File]::Exists($readyPath)) { [IO.File]::Delete($readyPath) }
    if ([IO.File]::Exists($resultPath)) { [IO.File]::Delete($resultPath) }
    $arguments = @("-NoProfile", "-File", $ScriptPath, "-WorkerMode", "selector-reader", "-WorkerRoot", $SuiteRoot)
    $reader = [Diagnostics.Process]::new()
    $reader.StartInfo = New-ProcessInfo -FilePath (Get-Process -Id $PID).Path -Arguments $arguments
    $started = $false
    try
    {
        $started = $reader.Start()
        if (-not $started) { throw "Failed to start selector reader worker." }
        $readyDeadline = [DateTime]::UtcNow.AddSeconds(10)
        while (-not [IO.File]::Exists($readyPath) -and [DateTime]::UtcNow -lt $readyDeadline)
        {
            if ($reader.HasExited) { throw "Selector reader exited before becoming ready." }
            Start-Sleep -Milliseconds 10
        }
        if (-not [IO.File]::Exists($readyPath)) { throw "Selector reader did not become ready within 10 seconds." }
        for ($index = 0; $index -lt 200; $index++)
        {
            $content = if (($index % 2) -eq 0) { $ManifestA } else { $ManifestB }
            Replace-Manifest -ProjectPath $ProjectPath -Content $content
            Start-Sleep -Milliseconds 1
        }
        Write-Utf8NoBomLf -Path $stopPath -Content "stop`n"
        if (-not $reader.WaitForExit(30000)) { throw "Selector reader worker timed out." }
        if (-not [IO.File]::Exists($resultPath)) { throw "Selector reader result is missing." }
        return Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    }
    finally
    {
        $readerCleanupFailure = $null
        try
        {
            if (-not [IO.File]::Exists($stopPath)) { Write-Utf8NoBomLf -Path $stopPath -Content "stop`n" }
        }
        catch { $readerCleanupFailure = $_.Exception }
        try
        {
            if ($started -and -not $reader.HasExited)
            {
                $reader.Kill($true)
                if (-not $reader.WaitForExit(30000)) { throw "Selector reader did not exit after kill." }
            }
        }
        catch { if ($null -eq $readerCleanupFailure) { $readerCleanupFailure = $_.Exception } }
        finally { $reader.Dispose() }
        if ($null -ne $readerCleanupFailure) { throw $readerCleanupFailure }
    }
}

function Invoke-UnityProbe
{
    param(
        [Parameter(Mandatory = $true)][string]$UnityExecutable,
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$Operation,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [string]$ExpectedGeneration = "",
        [string]$NextManifestPath = "",
        [string]$StatePath = "",
        [switch]$NoUpm
    )

    if ([IO.File]::Exists($OutputPath)) { [IO.File]::Delete($OutputPath) }
    $arguments = @(
        "-batchmode", "-nographics", "-projectPath", $ProjectPath,
        "-executeMethod", "GAS.Tests.D0M1.D0M1UnityProbe.Run",
        "-d0m1Operation", $Operation, "-d0m1Output", $OutputPath,
        "-logFile", $LogPath
    )
    if (-not [string]::IsNullOrWhiteSpace($ExpectedGeneration))
    {
        $arguments += @("-d0m1ExpectedGeneration", $ExpectedGeneration)
    }
    if (-not [string]::IsNullOrWhiteSpace($NextManifestPath))
    {
        $arguments += @("-d0m1NextManifest", $NextManifestPath)
    }
    if (-not [string]::IsNullOrWhiteSpace($StatePath))
    {
        $arguments += @("-d0m1State", $StatePath)
    }
    if ($NoUpm)
    {
        $arguments = @("-noUpm") + $arguments
    }
    $process = Invoke-ProcessAndWait -StartInfo (New-ProcessInfo -FilePath $UnityExecutable -Arguments $arguments)
    $payload = if ([IO.File]::Exists($OutputPath)) { Get-Content -LiteralPath $OutputPath -Raw | ConvertFrom-Json } else { $null }
    return [pscustomobject]@{ Process = $process; Payload = $payload; LogPath = $LogPath }
}

function Test-ExactPath
{
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Actual,
        [Parameter(Mandatory = $true)][string]$Expected
    )

    if ([string]::IsNullOrWhiteSpace($Actual)) { return $false }
    return (Resolve-NormalizedPath -Path $Actual).Equals((Resolve-NormalizedPath -Path $Expected), [StringComparison]::OrdinalIgnoreCase)
}

function Test-ExactSourceFileSet
{
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$SourceFiles,
        [Parameter(Mandatory = $true)][string]$ExpectedPath
    )

    if ($SourceFiles.Count -ne 1 -or [string]::IsNullOrWhiteSpace([string]$SourceFiles[0])) { return $false }
    return Test-ExactPath -Actual ([string]$SourceFiles[0]) -Expected $ExpectedPath
}

function Test-UnitySlotProbe
{
    param(
        [Parameter(Mandatory = $true)][object]$Probe,
        [Parameter(Mandatory = $true)][string]$ExpectedOperation,
        [Parameter(Mandatory = $true)][string]$ExpectedGeneration,
        [Parameter(Mandatory = $true)][string]$ExpectedSlot
    )

    if ($Probe.Process.TimedOut -or $Probe.Process.ExitCode -ne 0 -or $null -eq $Probe.Payload)
    {
        return [pscustomobject]@{ Passed = $false; Detail = "Unity probe did not complete successfully." }
    }
    $payload = $Probe.Payload
    $contractMatches = $payload.Operation -eq $ExpectedOperation -and $payload.ExpectedGeneration -eq $ExpectedGeneration
    $markersMatch = $payload.Passed -and $payload.RuntimeGeneration -eq $ExpectedGeneration -and $payload.EditorGeneration -eq $ExpectedGeneration -and $payload.AutoChessGeneration -eq $ExpectedGeneration
    $resolvedPathMatches = Test-ExactPath -Actual ([string]$payload.ResolvedPackagePath) -Expected $ExpectedSlot
    $runtimeSourceMatches = Test-ExactSourceFileSet -SourceFiles @($payload.RuntimeSourceFiles) -ExpectedPath (Join-Path $ExpectedSlot "Runtime\GenerationMarker.cs")
    $editorSourceMatches = Test-ExactSourceFileSet -SourceFiles @($payload.EditorSourceFiles) -ExpectedPath (Join-Path $ExpectedSlot "Editor\GenerationMarker.cs")
    $autoChessSourceMatches = Test-ExactSourceFileSet -SourceFiles @($payload.AutoChessSourceFiles) -ExpectedPath (Join-Path $ExpectedSlot "AutoChessGenerated\GenerationMarker.cs")
    $passed = $contractMatches -and $markersMatch -and $resolvedPathMatches -and $runtimeSourceMatches -and $editorSourceMatches -and $autoChessSourceMatches
    $detail = "contract=$contractMatches; markers=$markersMatch; resolvedPath=$resolvedPathMatches; runtimeSource=$runtimeSourceMatches; editorSource=$editorSourceMatches; autoChessSource=$autoChessSourceMatches"
    return [pscustomobject]@{ Passed = $passed; Detail = $detail }
}

function Test-LiveSwitchTimeline
{
    param(
        [Parameter(Mandatory = $true)][object]$Probe,
        [Parameter(Mandatory = $true)][string]$InitialGeneration,
        [Parameter(Mandatory = $true)][string]$InitialSlot,
        [Parameter(Mandatory = $true)][string]$FinalGeneration,
        [Parameter(Mandatory = $true)][string]$FinalSlot
    )

    if ($null -eq $Probe.Payload) { return [pscustomobject]@{ Passed = $false; Detail = "Timeline payload is missing." } }
    $entries = @($Probe.Payload.Timeline)
    if ($entries.Count -lt 5) { return [pscustomobject]@{ Passed = $false; Detail = "Timeline has fewer than five phase observations." } }
    $phaseNames = @($entries | ForEach-Object { [string]$_.Phase })
    $requiredPhases = @("BeforeSelectorReplace", "SelectorReplaced", "ResolveRequested", "ScriptsReloaded", "Verified")
    $previousPhaseIndex = -1
    foreach ($requiredPhase in $requiredPhases)
    {
        $phaseIndex = [Array]::IndexOf($phaseNames, $requiredPhase)
        if ($phaseIndex -le $previousPhaseIndex) { return [pscustomobject]@{ Passed = $false; Detail = "Timeline phase is missing or out of order: $requiredPhase" } }
        $previousPhaseIndex = $phaseIndex
    }

    foreach ($entry in $entries)
    {
        $markers = @([string]$entry.RuntimeGeneration, [string]$entry.EditorGeneration, [string]$entry.AutoChessGeneration)
        $nonEmptyMarkers = @($markers | Where-Object { -not [string]::IsNullOrEmpty($_) })
        if ($nonEmptyMarkers.Count -ne 0 -and ($nonEmptyMarkers.Count -ne 3 -or $markers[0] -ne $markers[1] -or $markers[0] -ne $markers[2]))
        {
            return [pscustomobject]@{ Passed = $false; Detail = "Timeline observed a partial or mixed assembly generation at phase $($entry.Phase)." }
        }
        if ($nonEmptyMarkers.Count -eq 3 -and $markers[0] -ne $InitialGeneration -and $markers[0] -ne $FinalGeneration)
        {
            return [pscustomobject]@{ Passed = $false; Detail = "Timeline observed an unknown generation at phase $($entry.Phase)." }
        }
    }

    $first = $entries[0]
    $last = $entries[$entries.Count - 1]
    $initialMatches = $first.Phase -eq "BeforeSelectorReplace" -and $first.RuntimeGeneration -eq $InitialGeneration -and $first.EditorGeneration -eq $InitialGeneration -and $first.AutoChessGeneration -eq $InitialGeneration -and (Test-ExactPath -Actual ([string]$first.ResolvedPackagePath) -Expected $InitialSlot)
    $finalMatches = $last.Phase -eq "Verified" -and $last.RuntimeGeneration -eq $FinalGeneration -and $last.EditorGeneration -eq $FinalGeneration -and $last.AutoChessGeneration -eq $FinalGeneration -and (Test-ExactPath -Actual ([string]$last.ResolvedPackagePath) -Expected $FinalSlot)
    return [pscustomobject]@{ Passed = $initialMatches -and $finalMatches; Detail = "initial=$initialMatches; final=$finalMatches; observations=$($entries.Count)" }
}

function Test-LiveSwitchArtifacts
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$NextManifestPath,
        [Parameter(Mandatory = $true)][string]$StatePath,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][string]$ExpectedManifest,
        [Parameter(Mandatory = $true)][string]$PreviousManifest,
        [Parameter(Mandatory = $true)][string]$ExpectedGeneration
    )

    try
    {
        $manifestPath = Join-Path $ProjectPath "Packages\manifest.json"
        $backupPath = $manifestPath + ".d0m1.backup"
        $stagedPath = $manifestPath + ".d0m1.next"
        if (-not [IO.File]::Exists($manifestPath) -or -not [IO.File]::Exists($NextManifestPath) -or -not [IO.File]::Exists($backupPath) -or -not [IO.File]::Exists($StatePath)) { throw "Live-switch artifact is missing." }
        $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
        $timeline = @($state.Timeline)
        $stateMatches = $state.ExpectedGeneration -eq $ExpectedGeneration -and (Test-ExactPath -Actual ([string]$state.OutputPath) -Expected $OutputPath) -and $timeline.Count -ge 5 -and $timeline[$timeline.Count - 1].Phase -eq "Verified"
        $selectorMatches = [IO.File]::ReadAllText($manifestPath) -eq $ExpectedManifest -and [IO.File]::ReadAllText($NextManifestPath) -eq $ExpectedManifest -and [IO.File]::ReadAllText($backupPath) -eq $PreviousManifest -and -not [IO.File]::Exists($stagedPath)
        return [pscustomobject]@{ Passed = $stateMatches -and $selectorMatches; Detail = "state=$stateMatches; selector=$selectorMatches" }
    }
    catch
    {
        return [pscustomobject]@{ Passed = $false; Detail = $_.Exception.Message }
    }
}

function Test-MarkerLoadBlocked
{
    param([Parameter(Mandatory = $true)][object]$Probe)

    if ($Probe.Process.TimedOut -or $Probe.Process.ExitCode -ne 1 -or $null -eq $Probe.Payload)
    {
        return $false
    }
    $payload = $Probe.Payload
    return $payload.Operation -eq "failed"
        -and -not $payload.Passed
        -and [string]::IsNullOrEmpty([string]$payload.RuntimeGeneration)
        -and [string]::IsNullOrEmpty([string]$payload.EditorGeneration)
        -and [string]::IsNullOrEmpty([string]$payload.AutoChessGeneration)
        -and ([string]$payload.Detail).StartsWith("System.InvalidOperationException: Generation marker type was not loaded:", [StringComparison]::Ordinal)
}

function Test-MissingSlotBlocked
{
    param(
        [Parameter(Mandatory = $true)][object]$Probe,
        [Parameter(Mandatory = $true)][string]$ExpectedMissingSlot
    )

    if (Test-MarkerLoadBlocked -Probe $Probe) { return $true }
    if ($Probe.Process.TimedOut -or $Probe.Process.ExitCode -ne 1 -or $null -eq $Probe.Payload)
    {
        return $false
    }
    $payload = $Probe.Payload
    $firstLine = ([string]$payload.Detail).Replace("`r`n", "`n").Split("`n")[0]
    $normalizedSlot = Resolve-NormalizedPath -Path $ExpectedMissingSlot
    $expectedRootFailure = "System.InvalidOperationException: Resolved package path does not exist: " + $normalizedSlot
    $expectedMarkerFailures = @(
        "System.InvalidOperationException: Generation marker source file does not exist: " + (Join-Path $normalizedSlot "Runtime\GenerationMarker.cs"),
        "System.InvalidOperationException: Generation marker source file does not exist: " + (Join-Path $normalizedSlot "Editor\GenerationMarker.cs"),
        "System.InvalidOperationException: Generation marker source file does not exist: " + (Join-Path $normalizedSlot "AutoChessGenerated\GenerationMarker.cs")
    )
    return $payload.Operation -eq "failed" -and -not $payload.Passed -and ($firstLine -eq $expectedRootFailure -or $firstLine -in $expectedMarkerFailures)
}

function Test-PackageLock
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$ExpectedSlotPath
    )

    $lockPath = Join-Path $ProjectPath "Packages\packages-lock.json"
    if (-not [IO.File]::Exists($lockPath)) { return [pscustomobject]@{ Passed = $false; Detail = "packages-lock.json is missing." } }
    $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
    $property = $lock.dependencies.PSObject.Properties[$script:PackageName]
    if ($null -eq $property) { return [pscustomobject]@{ Passed = $false; Detail = "Local slot is absent from packages-lock.json." } }
    $entry = $property.Value
    $version = [string]$entry.version
    $packagesPath = Join-Path $ProjectPath "Packages"
    $expectedRelativePath = [IO.Path]::GetRelativePath($packagesPath, (Resolve-NormalizedPath -Path $ExpectedSlotPath)).Replace("\", "/")
    $expectedVersion = "file:" + $expectedRelativePath
    $passed = [string]$entry.source -eq "local" -and $version -eq $expectedVersion
    return [pscustomobject]@{ Passed = $passed; Detail = "source=$($entry.source); version=$version; expected=$expectedVersion" }
}

function New-CaseResult
{
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][bool]$Passed,
        [Parameter(Mandatory = $true)][string]$Detail,
        [object]$Evidence = $null
    )

    return [pscustomobject]@{ Id = $Id; Passed = $Passed; Detail = $Detail; Evidence = $Evidence }
}

function Start-SlotMutationWatcher
{
    param([Parameter(Mandatory = $true)][string]$SlotsRoot)

    $prefix = "D0M1SlotWatcher-" + [Guid]::NewGuid().ToString("N")
    $watcher = [IO.FileSystemWatcher]::new($SlotsRoot)
    $watcher.IncludeSubdirectories = $true
    $watcher.NotifyFilter = [IO.NotifyFilters]::FileName -bor [IO.NotifyFilters]::DirectoryName -bor [IO.NotifyFilters]::LastWrite -bor [IO.NotifyFilters]::Size -bor [IO.NotifyFilters]::Attributes
    $sourceIds = @()
    try
    {
        foreach ($eventName in @("Created", "Changed", "Deleted", "Renamed", "Error"))
        {
            $sourceId = $prefix + "-" + $eventName
            Register-ObjectEvent -InputObject $watcher -EventName $eventName -SourceIdentifier $sourceId | Out-Null
            $sourceIds += $sourceId
        }
        $watcher.EnableRaisingEvents = $true
        return [pscustomobject]@{ Watcher = $watcher; SourceIds = $sourceIds }
    }
    catch
    {
        foreach ($sourceId in $sourceIds)
        {
            Unregister-Event -SourceIdentifier $sourceId -ErrorAction SilentlyContinue
            foreach ($job in @(Get-Job | Where-Object { $_.Name -eq $sourceId })) { Remove-Job -Job $job -Force -ErrorAction SilentlyContinue }
        }
        $watcher.Dispose()
        throw
    }
}

function Wait-SlotWatcherCanary
{
    param(
        [Parameter(Mandatory = $true)][object]$Handle,
        [Parameter(Mandatory = $true)][string]$CanaryPath,
        [int]$TimeoutMilliseconds = 5000
    )

    $records = [Collections.Generic.List[object]]::new()
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    while ([DateTime]::UtcNow -lt $deadline)
    {
        foreach ($record in @(Receive-SlotMutationEvents -Handle $Handle)) { $records.Add($record) }
        if (@($records | Where-Object { $_.ChangeType -eq "Error" }).Count -gt 0) { throw "Slot mutation watcher reported an error during canary." }
        $created = @($records | Where-Object { $_.ChangeType -eq "Created" -and $_.FullPath -eq $CanaryPath }).Count -gt 0
        $deleted = @($records | Where-Object { $_.ChangeType -eq "Deleted" -and $_.FullPath -eq $CanaryPath }).Count -gt 0
        if ($created -and $deleted) { return $records.ToArray() }
        Start-Sleep -Milliseconds 25
    }
    throw "Slot mutation watcher did not observe the bounded canary barrier: $CanaryPath"
}

function Stop-SlotMutationWatcher
{
    param([Parameter(Mandatory = $true)][object]$Handle)

    $records = @()
    try
    {
        Start-Sleep -Milliseconds 100
        $records = @(Receive-SlotMutationEvents -Handle $Handle)
        $Handle.Watcher.EnableRaisingEvents = $false
        Start-Sleep -Milliseconds 100
        $records += @(Receive-SlotMutationEvents -Handle $Handle)
    }
    finally
    {
        foreach ($sourceId in $Handle.SourceIds)
        {
            Unregister-Event -SourceIdentifier $sourceId -ErrorAction SilentlyContinue
            foreach ($job in @(Get-Job | Where-Object { $_.Name -eq $sourceId })) { Remove-Job -Job $job -Force -ErrorAction SilentlyContinue }
        }
        $Handle.Watcher.Dispose()
    }
    return $records
}

function Receive-SlotMutationEvents
{
    param([Parameter(Mandatory = $true)][object]$Handle)

    $records = [Collections.Generic.List[object]]::new()
    foreach ($sourceId in $Handle.SourceIds)
    {
        foreach ($eventRecord in @(Get-Event -SourceIdentifier $sourceId -ErrorAction SilentlyContinue))
        {
            $isWatcherError = $sourceId.EndsWith("-Error", [StringComparison]::Ordinal)
            $changeType = if ($isWatcherError) { "Error" } else { [string]$eventRecord.SourceEventArgs.ChangeType }
            $fullPath = if ($isWatcherError) { [string]$eventRecord.SourceEventArgs.GetException().Message } else { [string]$eventRecord.SourceEventArgs.FullPath }
            $oldFullPath = if ($changeType -eq "Renamed") { [string]$eventRecord.SourceEventArgs.OldFullPath } else { "" }
            $records.Add([pscustomobject]@{ ChangeType = $changeType; FullPath = $fullPath; OldFullPath = $oldFullPath })
            Remove-Event -EventIdentifier $eventRecord.EventIdentifier
        }
    }
    return $records.ToArray()
}

function Test-WatcherRecordTouchesAnyPath
{
    param(
        [Parameter(Mandatory = $true)][object]$Record,
        [Parameter(Mandatory = $true)][string[]]$Paths
    )

    if ($Record.ChangeType -eq "Error") { return $false }
    $eventPaths = @([string]$Record.FullPath, [string]$Record.OldFullPath)
    foreach ($eventPath in $eventPaths)
    {
        if ([string]::IsNullOrEmpty($eventPath)) { continue }
        $normalizedEvent = Resolve-NormalizedPath -Path $eventPath
        foreach ($path in $Paths)
        {
            $normalizedPath = Resolve-NormalizedPath -Path $path
            if ($normalizedEvent.Equals($normalizedPath, [StringComparison]::OrdinalIgnoreCase) -or
                $normalizedEvent.StartsWith($normalizedPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { return $true }
        }
    }
    return $false
}

function Assert-StrictChildPath
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$TrustedRoot
    )

    $normalizedPath = Resolve-NormalizedPath -Path $Path
    $normalizedRoot = Resolve-NormalizedPath -Path $TrustedRoot
    if (-not $normalizedPath.StartsWith($normalizedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Path escaped the trusted root: $normalizedPath"
    }
}

function Assert-SafeOutputPathNames
{
    param(
        [Parameter(Mandatory = $true)][string]$TrustedRoot,
        [Parameter(Mandatory = $true)][string]$TargetPath
    )

    Assert-StrictChildPath -Path $TargetPath -TrustedRoot $TrustedRoot
    $relative = (Resolve-NormalizedPath -Path $TargetPath).Substring((Resolve-NormalizedPath -Path $TrustedRoot).Length).TrimStart('\', '/')
    $segments = @($relative.Split(@('\', '/'), [StringSplitOptions]::RemoveEmptyEntries))
    if ($segments.Count -eq 0) { throw "D0-M1 output must name a file below the allowed root." }
    $invalidCharacters = [IO.Path]::GetInvalidFileNameChars()
    foreach ($segment in $segments)
    {
        if ($segment -eq "." -or $segment -eq ".." -or $segment.Contains(":") -or $segment.IndexOfAny($invalidCharacters) -ge 0 -or $segment.EndsWith(".") -or $segment.EndsWith(" ") -or $segment -match '^(?i:con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)')
        {
            throw "Unsafe D0-M1 output path segment: $segment"
        }
    }
}

function Assert-ExistingPathSegmentsHaveNoReparsePoint
{
    param(
        [Parameter(Mandatory = $true)][string]$TrustedRoot,
        [Parameter(Mandatory = $true)][string]$TargetPath
    )

    $normalizedRoot = Resolve-NormalizedPath -Path $TrustedRoot
    $normalizedTarget = Resolve-NormalizedPath -Path $TargetPath
    Assert-StrictChildPath -Path $normalizedTarget -TrustedRoot $normalizedRoot
    $relative = $normalizedTarget.Substring($normalizedRoot.Length).TrimStart('\', '/')
    $segments = @($relative.Split(@('\', '/'), [StringSplitOptions]::RemoveEmptyEntries))
    $current = $normalizedRoot
    $paths = @($normalizedRoot) + @($segments | ForEach-Object { $current = Join-Path $current $_; $current })
    for ($index = 0; $index -lt $paths.Count; $index++)
    {
        $path = $paths[$index]
        if (-not [IO.File]::Exists($path) -and -not [IO.Directory]::Exists($path)) { break }
        $attributes = [IO.File]::GetAttributes($path)
        if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point is forbidden in D0-M1 output path: $path" }
        if ($index -lt $paths.Count - 1 -and ($attributes -band [IO.FileAttributes]::Directory) -eq 0) { throw "Existing D0-M1 output parent is a file: $path" }
    }
}

function Resolve-SafeOutputPath
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$RequestedPath
    )

    $full = if ([IO.Path]::IsPathRooted($RequestedPath)) { Resolve-NormalizedPath -Path $RequestedPath } else { Resolve-NormalizedPath -Path (Join-Path $ProjectRoot $RequestedPath) }
    $allowed = Resolve-NormalizedPath -Path (Join-Path $ProjectRoot "TestResults\GasCodeGen")
    Assert-StrictChildPath -Path $full -TrustedRoot $allowed
    Assert-SafeOutputPathNames -TrustedRoot $allowed -TargetPath $full
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $ProjectRoot -TargetPath $full
    if ([IO.Directory]::Exists($full)) { throw "D0-M1 output must be a file: $full" }
    return [pscustomobject]@{ AllowedRoot = $allowed; Path = $full }
}

function Write-SafeAggregate
{
    param(
        [Parameter(Mandatory = $true)][object]$Output,
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][object]$Aggregate
    )

    Assert-SafeOutputPathNames -TrustedRoot $Output.AllowedRoot -TargetPath $Output.Path
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $ProjectRoot -TargetPath $Output.Path
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Output.Path)) | Out-Null
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $ProjectRoot -TargetPath $Output.Path
    if ([IO.Directory]::Exists($Output.Path)) { throw "D0-M1 output became a directory: $($Output.Path)" }
    if ([IO.File]::Exists($Output.Path)) { [IO.File]::Delete($Output.Path) }
    if ([IO.File]::Exists($Output.Path) -or [IO.Directory]::Exists($Output.Path)) { throw "D0-M1 output leaf survived exact unlink: $($Output.Path)" }
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $ProjectRoot -TargetPath $Output.Path
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes((($Aggregate | ConvertTo-Json -Depth 20).Replace("`r`n", "`n")) + "`n")
    $expectedHash = Get-BytesSha256 -Bytes $bytes
    $stream = [IO.FileStream]::new($Output.Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try
    {
        if ([D0M1NativeFileInfo]::GetLinkCount($stream.SafeFileHandle) -ne 1) { throw "D0-M1 aggregate output has multiple links before write." }
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
        if ([D0M1NativeFileInfo]::GetLinkCount($stream.SafeFileHandle) -ne 1) { throw "D0-M1 aggregate output gained a hardlink during write." }
    }
    finally { $stream.Dispose() }
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $ProjectRoot -TargetPath $Output.Path
    if ((Get-FileSha256 -Path $Output.Path) -ne $expectedHash) { throw "D0-M1 aggregate bytes changed after write." }
    return [pscustomobject]@{ ByteLength = $bytes.LongLength; Sha256 = $expectedHash }
}

function Read-SafeAggregate
{
    param(
        [Parameter(Mandatory = $true)][object]$Output,
        [Parameter(Mandatory = $true)][string]$ProjectRoot
    )

    Assert-SafeOutputPathNames -TrustedRoot $Output.AllowedRoot -TargetPath $Output.Path
    Assert-ExistingPathSegmentsHaveNoReparsePoint -TrustedRoot $ProjectRoot -TargetPath $Output.Path
    if (-not [IO.File]::Exists($Output.Path) -or [IO.Directory]::Exists($Output.Path)) { throw "D0-M1 aggregate is not an ordinary file." }
    $stream = [IO.FileStream]::new($Output.Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try
    {
        $linksBefore = [D0M1NativeFileInfo]::GetLinkCount($stream.SafeFileHandle)
        $length = $stream.Length
        if ($linksBefore -ne 1 -or $length -le 0 -or $length -gt $script:MaxAggregateBytes) { throw "D0-M1 aggregate handle failed link-count or length bounds." }
        $bytes = [byte[]]::new([int]$length)
        $offset = 0
        while ($offset -lt $bytes.Length)
        {
            $read = $stream.Read($bytes, $offset, $bytes.Length - $offset)
            if ($read -le 0) { throw "D0-M1 aggregate read ended before its opened-handle length." }
            $offset += $read
        }
        if ($stream.Length -ne $length -or [D0M1NativeFileInfo]::GetLinkCount($stream.SafeFileHandle) -ne 1) { throw "D0-M1 aggregate changed links or length during read." }
    }
    finally
    {
        $stream.Dispose()
    }
    return [pscustomobject]@{ Value = ([Text.UTF8Encoding]::new($false).GetString($bytes) | ConvertFrom-Json); ByteLength = $bytes.LongLength; Sha256 = Get-BytesSha256 -Bytes $bytes; LinkCount = $linksBefore }
}

function Set-D0M1AggregateFinalState
{
    param(
        [Parameter(Mandatory = $true)][object]$Aggregate,
        [Parameter(Mandatory = $true)][object]$FinalProtocol,
        [Parameter(Mandatory = $true)][bool]$ProtocolUnchanged,
        [Parameter(Mandatory = $true)][bool]$PostFinalWriteConfirmed,
        [Parameter(Mandatory = $true)][bool]$EvidenceCollectionCompleted,
        [Parameter(Mandatory = $true)][bool]$CandidatePassed,
        [Parameter(Mandatory = $true)][string]$SelectorDecision,
        [Parameter(Mandatory = $true)][string]$RemainingGate
    )

    $overallPassed = $CandidatePassed -and $EvidenceCollectionCompleted -and $PostFinalWriteConfirmed
    $Aggregate.Decision.ImmutableUpmSelectorRoute = $SelectorDecision
    $Aggregate.Decision.RemainingGate = $RemainingGate
    $Aggregate.RealProjectProtocolBytes.After = $FinalProtocol
    $Aggregate.RealProjectProtocolBytes.Unchanged = $ProtocolUnchanged
    $Aggregate.RealProjectProtocolBytes.PostFinalWriteConfirmed = $PostFinalWriteConfirmed
    $Aggregate.Summary.CandidateRequirementsPassed = $CandidatePassed
    $Aggregate.Summary.EvidenceCollectionCompleted = $EvidenceCollectionCompleted
    $Aggregate.Summary.TestedProcessExitCode = if ($overallPassed) { 0 } else { 1 }
    $Aggregate.Summary.OverallPassed = $overallPassed
    $fingerprintLines = [Collections.Generic.List[string]]::new()
    $fingerprintLines.Add("EX-GAS-N2-G0-D0-M1-Evidence-v1")
    $fingerprintLines.Add("Unity=" + $Aggregate.Inputs.UnityFileSha256)
    $fingerprintLines.Add("Harness=" + $Aggregate.Inputs.HarnessSha256)
    $fingerprintLines.Add("Fixture=" + $Aggregate.Inputs.FixtureTemplateSha256)
    $fingerprintLines.Add("PackageTemplate=" + $Aggregate.Inputs.PackageTemplateSha256)
    foreach ($case in $Aggregate.Cases) { $fingerprintLines.Add($case.Id + "=" + ([bool]$case.Passed).ToString().ToLowerInvariant()) }
    $fingerprintLines.Add("MaterializerRoute=" + $Aggregate.Decision.MaterializerRoute)
    $fingerprintLines.Add("ImmutableUpmSelectorRoute=" + $SelectorDecision)
    $fingerprintLines.Add("ProtocolUnchanged=" + $ProtocolUnchanged.ToString().ToLowerInvariant())
    $fingerprintLines.Add("Cleanup=" + $Aggregate.Cleanup.Status)
    $fingerprintLines.Add("CandidatePassed=" + $CandidatePassed.ToString().ToLowerInvariant())
    $fingerprintLines.Add("EvidenceCollectionCompleted=" + $EvidenceCollectionCompleted.ToString().ToLowerInvariant())
    $fingerprintLines.Add("PostFinalWriteConfirmed=" + $PostFinalWriteConfirmed.ToString().ToLowerInvariant())
    $fingerprintLines.Add("OverallPassed=" + $overallPassed.ToString().ToLowerInvariant())
    $fingerprintPayload = [string]::Join("`n", $fingerprintLines)
    $Aggregate.DeterministicEvidence.Payload = $fingerprintPayload
    $Aggregate.DeterministicEvidence.Sha256 = Get-BytesSha256 -Bytes ([Text.Encoding]::UTF8.GetBytes($fingerprintPayload))
    return $overallPassed
}

if (-not [string]::IsNullOrWhiteSpace($WorkerMode))
{
    Invoke-InternalWorker -Mode $WorkerMode -Root (Resolve-NormalizedPath -Path $WorkerRoot)
    exit 0
}

$scriptPath = Resolve-NormalizedPath -Path $PSCommandPath
$projectRoot = Resolve-NormalizedPath -Path (Join-Path $PSScriptRoot "..\..\..\..")
$fixtureTemplate = Join-Path $PSScriptRoot "Fixture~"
$packageTemplate = Join-Path $PSScriptRoot "PackageTemplate~"
$resolvedOutput = Resolve-SafeOutputPath -ProjectRoot $projectRoot -RequestedPath $OutputPath
$resolvedUnity = Resolve-UnityExecutable -RequestedPath $UnityPath
$unityFileSha256 = Get-FileSha256 -Path $resolvedUnity
$harnessSha256 = Get-FileSha256 -Path $scriptPath
$fixtureTemplateSha256 = Get-TreeSha256 -Path $fixtureTemplate
$packageTemplateSha256 = Get-TreeSha256 -Path $packageTemplate
$beforeProtocol = Get-ProtocolSnapshot -ProjectRoot $projectRoot
$suiteRoot = New-IsolatedSuiteRoot
$cases = [Collections.Generic.List[object]]::new()
$cleanupStatus = "Pending"
$cleanupFailure = ""
$executionFailure = ""
$slotWatcher = $null
$packageWatcher = $null
$slotMutationRecords = @()
$watcherCanaryRecords = @()

try
{
    $unityProject = Join-Path $suiteRoot "UnityProject"
    $slotsRoot = Join-Path $suiteRoot "Slots"
    $controlRoot = Join-Path $suiteRoot "Control"
    [IO.Directory]::CreateDirectory($slotsRoot) | Out-Null
    [IO.Directory]::CreateDirectory($controlRoot) | Out-Null
    New-UnityProject -FixtureTemplate $fixtureTemplate -ProjectPath $unityProject

    $materializerRoot = Join-Path $suiteRoot "Materializer"
    [IO.Directory]::CreateDirectory((Join-Path $materializerRoot "Target")) | Out-Null
    $installRoot = Join-Path $unityProject "Assets\GAS\Generated\CodeGen"
    [IO.Directory]::CreateDirectory($installRoot) | Out-Null
    Write-Utf8NoBomLf -Path (Join-Path $installRoot "generation.txt") -Content ($script:GenerationA + "`n")
    Write-Utf8NoBomLf -Path (Join-Path $materializerRoot "Target\generation.txt") -Content ($script:GenerationB + "`n")
    $materializerFault = Invoke-WorkerProcess -ScriptPath $scriptPath -Mode "materializer-after-old-move" -Root $suiteRoot
    $activeMissing = -not [IO.Directory]::Exists($installRoot)
    $backupGeneration = [IO.File]::ReadAllText((Join-Path $materializerRoot "Backup\generation.txt")).Trim()
    $materializerPassed = -not $materializerFault.TimedOut -and $materializerFault.ExitCode -ne 0 -and $activeMissing -and $backupGeneration -eq $script:GenerationA
    $cases.Add((New-CaseResult -Id "M-01" -Passed $materializerPassed -Detail "同一隔离 Unity 工程的安装根在 old move 后强杀会暴露 Missing。" -Evidence $materializerFault))

    $directProbe = Invoke-UnityProbe -UnityExecutable $resolvedUnity -ProjectPath $unityProject -Operation "observe-root" -OutputPath (Join-Path $controlRoot "direct-unity.json") -LogPath (Join-Path $controlRoot "direct-unity.log")
    $directPassed = -not $directProbe.Process.TimedOut -and $directProbe.Process.ExitCode -eq 0 -and $null -ne $directProbe.Payload -and $directProbe.Payload.Passed -and -not $directProbe.Payload.InstallRootExists
    $cases.Add((New-CaseResult -Id "M-02" -Passed $directPassed -Detail "直接 Unity.exe 打开 M-01 的同一工程并观察到该 crash residue 的 Missing 根。" -Evidence $directProbe))

    $slotA = New-ImmutableSlot -TemplatePath $packageTemplate -SlotsRoot $slotsRoot -GenerationId $script:GenerationA
    $slotB = New-ImmutableSlot -TemplatePath $packageTemplate -SlotsRoot $slotsRoot -GenerationId $script:GenerationB
    $watcherControlRoot = Join-Path $slotsRoot ".watcher-control"
    [IO.Directory]::CreateDirectory($watcherControlRoot) | Out-Null
    $slotWatcher = Start-SlotMutationWatcher -SlotsRoot $slotsRoot
    $startCanaryPath = Join-Path $watcherControlRoot "start-canary"
    Write-Utf8NoBomLfCreateNew -Path $startCanaryPath -Content "start`n"
    [IO.File]::Delete($startCanaryPath)
    $startCanaryRecords = @(Wait-SlotWatcherCanary -Handle $slotWatcher -CanaryPath $startCanaryPath)
    $slotABefore = Get-TreeSha256 -Path $slotA
    $slotBBefore = Get-TreeSha256 -Path $slotB
    $manifestA = Get-ManifestContent -ProjectPath $unityProject -SlotPath $slotA
    $manifestB = Get-ManifestContent -ProjectPath $unityProject -SlotPath $slotB
    $manifestPath = Join-Path $unityProject "Packages\manifest.json"
    Write-Utf8NoBomLf -Path $manifestPath -Content $manifestA
    Write-Utf8NoBomLf -Path (Join-Path $controlRoot "manifest-a.sha256") -Content (Get-FileSha256 -Path $manifestPath)
    Write-Utf8NoBomLf -Path (Join-Path $controlRoot "manifest-b.json") -Content $manifestB
    Write-Utf8NoBomLf -Path (Join-Path $controlRoot "manifest-b.sha256") -Content (Get-FileSha256 -Path (Join-Path $controlRoot "manifest-b.json"))

    $unityA = Invoke-UnityProbe -UnityExecutable $resolvedUnity -ProjectPath $unityProject -Operation "verify-slot" -ExpectedGeneration $script:GenerationA -OutputPath (Join-Path $controlRoot "unity-a.json") -LogPath (Join-Path $controlRoot "unity-a.log")
    $lockA = Test-PackageLock -ProjectPath $unityProject -ExpectedSlotPath $slotA
    $unityAValidation = Test-UnitySlotProbe -Probe $unityA -ExpectedOperation "verify-slot" -ExpectedGeneration $script:GenerationA -ExpectedSlot $slotA
    $unityAPassed = $unityAValidation.Passed -and $lockA.Passed
    $cases.Add((New-CaseResult -Id "U-01" -Passed $unityAPassed -Detail "Unity/UPM 解析 generation-a，三程序集 marker、sourceFiles、resolvedPath 与 packages-lock 同代。" -Evidence ([pscustomobject]@{ Unity = $unityA; Validation = $unityAValidation; PackageLock = $lockA })))

    Set-NextManifest -ProjectPath $unityProject -Content $manifestB | Out-Null
    $beforeFault = Invoke-WorkerProcess -ScriptPath $scriptPath -Mode "selector-before-replace" -Root $suiteRoot
    $beforeFaultPassed = -not $beforeFault.TimedOut -and $beforeFault.ExitCode -ne 0 -and [IO.File]::ReadAllText($manifestPath) -eq $manifestA
    $cases.Add((New-CaseResult -Id "U-02" -Passed $beforeFaultPassed -Detail "单文件 selector 在 replace 前强杀时保持 generation-a 原字节。" -Evidence $beforeFault))

    if ([IO.File]::Exists((Join-Path $unityProject "Packages\manifest.json.next"))) { [IO.File]::Delete((Join-Path $unityProject "Packages\manifest.json.next")) }
    Set-NextManifest -ProjectPath $unityProject -Content $manifestB | Out-Null
    $afterFault = Invoke-WorkerProcess -ScriptPath $scriptPath -Mode "selector-after-replace" -Root $suiteRoot
    $backupPath = Join-Path $unityProject "Packages\manifest.json.backup"
    $afterFaultPassed = -not $afterFault.TimedOut -and $afterFault.ExitCode -ne 0 -and [IO.File]::ReadAllText($manifestPath) -eq $manifestB -and [IO.File]::ReadAllText($backupPath) -eq $manifestA
    $cases.Add((New-CaseResult -Id "U-03" -Passed $afterFaultPassed -Detail "单文件 selector 在 replace 后强杀时提交 generation-b，并保留 generation-a backup。" -Evidence $afterFault))

    $readerResult = Invoke-SelectorConcurrencyProbe -ScriptPath $scriptPath -SuiteRoot $suiteRoot -ProjectPath $unityProject -ManifestA $manifestA -ManifestB $manifestB
    $readerPassed = $readerResult.GenerationAObservations -gt 0 -and $readerResult.GenerationBObservations -gt 0 -and $readerResult.InvalidHashes -eq 0 -and $readerResult.ExhaustedReads -eq 0 -and $readerResult.MaxConsecutiveRetries -lt $readerResult.RetryLimit
    $cases.Add((New-CaseResult -Id "U-04" -Passed $readerPassed -Detail "并发 selector gate 同时观察到完整 A/B；底层共享冲突只允许在 64 次有界重试内恢复，禁止无界重试、耗尽或无效字节。" -Evidence $readerResult))

    if ([IO.File]::ReadAllText($manifestPath) -ne $manifestB) { Replace-Manifest -ProjectPath $unityProject -Content $manifestB }
    $noUpm = Invoke-UnityProbe -UnityExecutable $resolvedUnity -ProjectPath $unityProject -Operation "verify-slot" -ExpectedGeneration $script:GenerationB -OutputPath (Join-Path $controlRoot "unity-b-no-upm.json") -LogPath (Join-Path $controlRoot "unity-b-no-upm.log") -NoUpm
    $noUpmPassed = Test-MarkerLoadBlocked -Probe $noUpm
    $cases.Add((New-CaseResult -Id "U-05" -Passed $noUpmPassed -Detail "selector=B 且 Library/lock 仍为 A 时，-noUpm 必须在进入 Play/Build 前 fail closed。" -Evidence $noUpm))

    $unityB = Invoke-UnityProbe -UnityExecutable $resolvedUnity -ProjectPath $unityProject -Operation "verify-slot" -ExpectedGeneration $script:GenerationB -OutputPath (Join-Path $controlRoot "unity-b.json") -LogPath (Join-Path $controlRoot "unity-b.log")
    $lockB = Test-PackageLock -ProjectPath $unityProject -ExpectedSlotPath $slotB
    $unityBValidation = Test-UnitySlotProbe -Probe $unityB -ExpectedOperation "verify-slot" -ExpectedGeneration $script:GenerationB -ExpectedSlot $slotB
    $unityBPassed = $unityBValidation.Passed -and $lockB.Passed
    $cases.Add((New-CaseResult -Id "U-06" -Passed $unityBPassed -Detail "Unity/UPM 解析 generation-b，三程序集 marker、sourceFiles、resolvedPath 与 packages-lock 同代。" -Evidence ([pscustomobject]@{ Unity = $unityB; Validation = $unityBValidation; PackageLock = $lockB })))

    $nextManifestA = Set-NextManifest -ProjectPath $unityProject -Content $manifestA
    $liveStatePath = Join-Path $controlRoot "live-switch-state.json"
    $liveOutputPath = Join-Path $controlRoot "unity-live-a.json"
    $liveA = Invoke-UnityProbe -UnityExecutable $resolvedUnity -ProjectPath $unityProject -Operation "live-switch" -ExpectedGeneration $script:GenerationA -NextManifestPath $nextManifestA -StatePath $liveStatePath -OutputPath $liveOutputPath -LogPath (Join-Path $controlRoot "unity-live-a.log")
    $liveAValidation = Test-UnitySlotProbe -Probe $liveA -ExpectedOperation "live-switch" -ExpectedGeneration $script:GenerationA -ExpectedSlot $slotA
    $liveTimelineValidation = Test-LiveSwitchTimeline -Probe $liveA -InitialGeneration $script:GenerationB -InitialSlot $slotB -FinalGeneration $script:GenerationA -FinalSlot $slotA
    $liveArtifactValidation = Test-LiveSwitchArtifacts -ProjectPath $unityProject -NextManifestPath $nextManifestA -StatePath $liveStatePath -OutputPath $liveOutputPath -ExpectedManifest $manifestA -PreviousManifest $manifestB -ExpectedGeneration $script:GenerationA
    $liveLockA = Test-PackageLock -ProjectPath $unityProject -ExpectedSlotPath $slotA
    $domainReloadObserved = $null -ne $liveA.Payload -and $liveA.Payload.DomainReloadObserved -and ([int]$liveA.Payload.ReloadEpochAfter -gt [int]$liveA.Payload.ReloadEpochBefore)
    $liveAPassed = $liveAValidation.Passed -and $liveTimelineValidation.Passed -and $liveArtifactValidation.Passed -and $liveLockA.Passed -and $domainReloadObserved
    $cases.Add((New-CaseResult -Id "U-07" -Passed $liveAPassed -Detail "同一 Unity 进程从 generation-b 原子切换到 generation-a，完成 UPM Resolve、脚本域重载，并在时间线中从未观察到三程序集混代。" -Evidence ([pscustomobject]@{ Unity = $liveA; Validation = $liveAValidation; TimelineValidation = $liveTimelineValidation; ArtifactValidation = $liveArtifactValidation; PackageLock = $liveLockA; DomainReloadObserved = $domainReloadObserved })))

    $endCanaryPath = Join-Path $watcherControlRoot "end-canary"
    Write-Utf8NoBomLfCreateNew -Path $endCanaryPath -Content "end`n"
    [IO.File]::Delete($endCanaryPath)
    $endCanaryRecords = @(Wait-SlotWatcherCanary -Handle $slotWatcher -CanaryPath $endCanaryPath)
    $slotMutationRecords = @($startCanaryRecords) + @($endCanaryRecords) + @(Stop-SlotMutationWatcher -Handle $slotWatcher)
    $slotWatcher = $null
    $slotAAfter = Get-TreeSha256 -Path $slotA
    $slotBAfter = Get-TreeSha256 -Path $slotB
    $watcherCanaryRecords = @($slotMutationRecords | Where-Object { $_.FullPath -eq $startCanaryPath -or $_.FullPath -eq $endCanaryPath })
    $slotMutationRecords = @($slotMutationRecords | Where-Object { $_.FullPath -ne $startCanaryPath -and $_.FullPath -ne $endCanaryPath })
    $generationMutationRecords = @($slotMutationRecords | Where-Object { Test-WatcherRecordTouchesAnyPath -Record $_ -Paths @($slotA, $slotB) })
    $watcherErrors = @($slotMutationRecords | Where-Object { $_.ChangeType -eq "Error" })
    $slotsImmutable = $slotABefore -eq $slotAAfter -and $slotBBefore -eq $slotBAfter -and $generationMutationRecords.Count -eq 0 -and $watcherErrors.Count -eq 0
    $cases.Add((New-CaseResult -Id "U-08" -Passed $slotsImmutable -Detail "同一 watcher 经前后 canary 屏障验证；两份 immutable UPM slot 在 resolve/compile/域重载全过程无写事件，且首尾树哈希完全一致。" -Evidence ([pscustomobject]@{ WatcherPositiveControl = $watcherCanaryRecords; SlotABefore = $slotABefore; SlotAAfter = $slotAAfter; SlotBBefore = $slotBBefore; SlotBAfter = $slotBAfter; MutationEvents = $slotMutationRecords; GenerationMutationEvents = $generationMutationRecords; WatcherErrors = $watcherErrors })))

    $missingSlotA = $slotA + ".missing"
    [IO.Directory]::Move($slotA, $missingSlotA)
    $slotWatcher = Start-SlotMutationWatcher -SlotsRoot $slotsRoot
    $packagesRoot = Join-Path $unityProject "Packages"
    $packageWatcher = Start-SlotMutationWatcher -SlotsRoot $packagesRoot
    $u09StartCanaryPath = Join-Path $watcherControlRoot "u09-start-canary"
    Write-Utf8NoBomLfCreateNew -Path $u09StartCanaryPath -Content "u09-start`n"
    [IO.File]::Delete($u09StartCanaryPath)
    $u09StartCanaryRecords = @(Wait-SlotWatcherCanary -Handle $slotWatcher -CanaryPath $u09StartCanaryPath)
    $u09PackageStartCanaryPath = Join-Path $packagesRoot ".d0m1-u09-start-canary"
    Write-Utf8NoBomLfCreateNew -Path $u09PackageStartCanaryPath -Content "u09-package-start`n"
    [IO.File]::Delete($u09PackageStartCanaryPath)
    $u09PackageStartCanaryRecords = @(Wait-SlotWatcherCanary -Handle $packageWatcher -CanaryPath $u09PackageStartCanaryPath)
    $u09MissingBefore = Get-TreeSha256 -Path $missingSlotA
    $u09SlotBBefore = Get-TreeSha256 -Path $slotB
    $u09ManifestBefore = Get-FileSha256 -Path $manifestPath
    $u09LockPath = Join-Path $unityProject "Packages\packages-lock.json"
    $u09LockBefore = Get-FileSha256 -Path $u09LockPath
    $missingSlotPreconditions = [IO.File]::ReadAllText($manifestPath) -eq $manifestA -and -not [IO.Directory]::Exists($slotA) -and (Get-TreeSha256 -Path $missingSlotA) -eq $slotABefore
    $missingSlotLock = Test-PackageLock -ProjectPath $unityProject -ExpectedSlotPath $slotA
    $missingSlot = Invoke-UnityProbe -UnityExecutable $resolvedUnity -ProjectPath $unityProject -Operation "verify-slot" -ExpectedGeneration $script:GenerationA -OutputPath (Join-Path $controlRoot "unity-a-missing-no-upm.json") -LogPath (Join-Path $controlRoot "unity-a-missing-no-upm.log") -NoUpm
    $u09EndCanaryPath = Join-Path $watcherControlRoot "u09-end-canary"
    Write-Utf8NoBomLfCreateNew -Path $u09EndCanaryPath -Content "u09-end`n"
    [IO.File]::Delete($u09EndCanaryPath)
    $u09EndCanaryRecords = @(Wait-SlotWatcherCanary -Handle $slotWatcher -CanaryPath $u09EndCanaryPath)
    $u09PackageEndCanaryPath = Join-Path $packagesRoot ".d0m1-u09-end-canary"
    Write-Utf8NoBomLfCreateNew -Path $u09PackageEndCanaryPath -Content "u09-package-end`n"
    [IO.File]::Delete($u09PackageEndCanaryPath)
    $u09PackageEndCanaryRecords = @(Wait-SlotWatcherCanary -Handle $packageWatcher -CanaryPath $u09PackageEndCanaryPath)
    $u09WatcherRecords = @($u09StartCanaryRecords) + @($u09EndCanaryRecords) + @(Stop-SlotMutationWatcher -Handle $slotWatcher)
    $slotWatcher = $null
    $u09PackageWatcherRecords = @($u09PackageStartCanaryRecords) + @($u09PackageEndCanaryRecords) + @(Stop-SlotMutationWatcher -Handle $packageWatcher)
    $packageWatcher = $null
    $u09CanaryRecords = @($u09WatcherRecords | Where-Object { $_.FullPath -eq $u09StartCanaryPath -or $_.FullPath -eq $u09EndCanaryPath })
    $u09MutationRecords = @($u09WatcherRecords | Where-Object { $_.FullPath -ne $u09StartCanaryPath -and $_.FullPath -ne $u09EndCanaryPath })
    $u09ProtectedMutations = @($u09MutationRecords | Where-Object { Test-WatcherRecordTouchesAnyPath -Record $_ -Paths @($slotA, $missingSlotA, $slotB) })
    $u09WatcherErrors = @($u09MutationRecords | Where-Object { $_.ChangeType -eq "Error" })
    $u09PackageCanaryRecords = @($u09PackageWatcherRecords | Where-Object { $_.FullPath -eq $u09PackageStartCanaryPath -or $_.FullPath -eq $u09PackageEndCanaryPath })
    $u09PackageMutationRecords = @($u09PackageWatcherRecords | Where-Object { $_.FullPath -ne $u09PackageStartCanaryPath -and $_.FullPath -ne $u09PackageEndCanaryPath })
    $u09PackageProtectedMutations = @($u09PackageMutationRecords | Where-Object { Test-WatcherRecordTouchesAnyPath -Record $_ -Paths @($manifestPath, $u09LockPath) })
    $u09PackageWatcherErrors = @($u09PackageMutationRecords | Where-Object { $_.ChangeType -eq "Error" })
    $u09StorageStable = -not [IO.Directory]::Exists($slotA) -and
        (Get-TreeSha256 -Path $missingSlotA) -eq $u09MissingBefore -and
        (Get-TreeSha256 -Path $slotB) -eq $u09SlotBBefore -and
        (Get-FileSha256 -Path $manifestPath) -eq $u09ManifestBefore -and
        (Get-FileSha256 -Path $u09LockPath) -eq $u09LockBefore -and
        $u09ProtectedMutations.Count -eq 0 -and $u09WatcherErrors.Count -eq 0 -and
        $u09PackageProtectedMutations.Count -eq 0 -and $u09PackageWatcherErrors.Count -eq 0
    $missingSlotBlocked = Test-MissingSlotBlocked -Probe $missingSlot -ExpectedMissingSlot $slotA
    $missingSlotPassed = $missingSlotPreconditions -and $missingSlotLock.Passed -and $missingSlotBlocked -and $u09StorageStable
    $cases.Add((New-CaseResult -Id "U-09" -Passed $missingSlotPassed -Detail "selector、lock 与旧缓存均为 generation-a 时，来源 gate 必须拒绝缺槽；Slots 与 Packages 双 watcher 的前后 canary 窗口内不得改写三条槽路径、manifest 或 lock。" -Evidence ([pscustomobject]@{ PreconditionsPassed = $missingSlotPreconditions; PackageLock = $missingSlotLock; GateBlocked = $missingSlotBlocked; StorageStable = $u09StorageStable; SlotWatcherPositiveControl = $u09CanaryRecords; SlotMutationEvents = $u09MutationRecords; ProtectedSlotMutationEvents = $u09ProtectedMutations; SlotWatcherErrors = $u09WatcherErrors; PackageWatcherPositiveControl = $u09PackageCanaryRecords; PackageMutationEvents = $u09PackageMutationRecords; ProtectedPackageMutationEvents = $u09PackageProtectedMutations; PackageWatcherErrors = $u09PackageWatcherErrors; Unity = $missingSlot })))
}
catch
{
    $executionFailure = $_.Exception.ToString()
}
finally
{
    if ($null -ne $packageWatcher)
    {
        try { Stop-SlotMutationWatcher -Handle $packageWatcher | Out-Null }
        catch { $executionFailure += "`nPackage watcher cleanup failed: " + $_.Exception.ToString() }
        finally { $packageWatcher = $null }
    }
    if ($null -ne $slotWatcher)
    {
        try { $slotMutationRecords = @(Stop-SlotMutationWatcher -Handle $slotWatcher) }
        catch { $executionFailure += "`nSlot watcher cleanup failed: " + $_.Exception.ToString() }
        finally { $slotWatcher = $null }
    }
    if ($KeepFixtures)
    {
        $cleanupStatus = "SkippedByRequest"
    }
    else
    {
        try
        {
            Remove-IsolatedSuiteRoot -Root $suiteRoot
            $cleanupStatus = "Passed"
        }
        catch
        {
            $cleanupStatus = "Failed"
            $cleanupFailure = $_.Exception.Message
        }
    }
}

$afterProtocol = Get-ProtocolSnapshot -ProjectRoot $projectRoot
$protocolUnchanged = $beforeProtocol.AggregateSha256 -eq $afterProtocol.AggregateSha256
$passedCases = @($cases | Where-Object { $_.Passed })
$failedCases = @($cases | Where-Object { -not $_.Passed })
$expectedCaseIds = @("M-01", "M-02", "U-01", "U-02", "U-03", "U-04", "U-05", "U-06", "U-07", "U-08", "U-09")
$actualCaseIds = @($cases | ForEach-Object { [string]$_.Id })
$duplicateCaseIds = @($actualCaseIds | Group-Object | Where-Object { $_.Count -ne 1 })
$caseIdSetMatches = $actualCaseIds.Count -eq $expectedCaseIds.Count -and $duplicateCaseIds.Count -eq 0
foreach ($expectedCaseId in $expectedCaseIds) { $caseIdSetMatches = $caseIdSetMatches -and $expectedCaseId -in $actualCaseIds }
$caseCollectionComplete = $caseIdSetMatches -and [string]::IsNullOrEmpty($executionFailure)
$allCasesPassed = $caseCollectionComplete -and $failedCases.Count -eq 0
$materializerDecision = if (($cases | Where-Object { $_.Id -eq "M-02" }).Passed) { "RejectedByDirectUnityBypass" } else { "Inconclusive" }
$slotCase = @($cases | Where-Object { $_.Id -eq "U-08" })
$slotMutationRejected = $slotCase.Count -eq 1 -and -not $slotCase[0].Passed -and @($slotCase[0].Evidence.GenerationMutationEvents).Count -gt 0
$selectorDecision = if ($allCasesPassed) { "EligibleForHumanReview" } elseif ($slotMutationRejected) { "RejectedByUnityDirectoryMonitorMutation" } else { "NotProven" }
$remainingGate = if ($allCasesPassed) { "ADR human confirmation" } elseif ($slotMutationRejected) { "D0-M2 immutable tarball selector or SourceGenerator physical experiment, then ADR human confirmation" } else { "D0-M1 technical evidence gaps and ADR human confirmation" }
$aggregate = [ordered]@{
    SchemaVersion = 1
    Suite = "N2-G0-D0-M1"
    Decision = [ordered]@{
        MaterializerRoute = $materializerDecision
        ImmutableUpmSelectorRoute = $selectorDecision
        D1Authorized = $false
        RemainingGate = $remainingGate
    }
    Inputs = [ordered]@{
        UnityPath = $resolvedUnity
        UnityFileSha256 = $unityFileSha256
        HarnessSha256 = $harnessSha256
        FixtureTemplateSha256 = $fixtureTemplateSha256
        PackageTemplateSha256 = $packageTemplateSha256
        ProjectVersion = "6000.3.14f1"
        OutputPath = $resolvedOutput.Path
    }
    RealProjectProtocolBytes = [ordered]@{
        Before = $beforeProtocol
        After = $afterProtocol
        Unchanged = $protocolUnchanged
        PostFinalWriteConfirmed = $false
    }
    Cases = $cases.ToArray()
    Cleanup = [ordered]@{
        Status = $cleanupStatus
        FixtureRoot = if ($cleanupStatus -eq "Passed") { "<deleted>" } else { $suiteRoot }
        Failure = $cleanupFailure
    }
    ExecutionFailure = $executionFailure
    DeterministicEvidence = [ordered]@{
        Domain = "EX-GAS-N2-G0-D0-M1-Evidence-v1"
        Payload = ""
        Sha256 = ""
    }
    Summary = [ordered]@{
        Expected = 11
        Total = $cases.Count
        Passed = $passedCases.Count
        Failed = $failedCases.Count
        ExactCaseIdSet = $caseIdSetMatches
        CandidateRequirementsPassed = $allCasesPassed
        EvidenceCollectionCompleted = $false
        TestedProcessExitCode = 1
        OverallPassed = $false
    }
}

$null = Set-D0M1AggregateFinalState -Aggregate $aggregate -FinalProtocol $afterProtocol -ProtocolUnchanged $protocolUnchanged -PostFinalWriteConfirmed $false -EvidenceCollectionCompleted $false -CandidatePassed $allCasesPassed -SelectorDecision $selectorDecision -RemainingGate $remainingGate
$draftWrite = Write-SafeAggregate -Output $resolvedOutput -ProjectRoot $projectRoot -Aggregate $aggregate
$draftRead = Read-SafeAggregate -Output $resolvedOutput -ProjectRoot $projectRoot
$afterDraftWriteProtocol = Get-ProtocolSnapshot -ProjectRoot $projectRoot
$draftProtocolUnchanged = $beforeProtocol.AggregateSha256 -eq $afterDraftWriteProtocol.AggregateSha256
$draftBytesMatch = $draftRead.LinkCount -eq 1 -and $draftRead.ByteLength -eq $draftWrite.ByteLength -and $draftRead.Sha256 -eq $draftWrite.Sha256
$evidenceCandidateComplete = $caseCollectionComplete -and $cleanupStatus -eq "Passed" -and $draftProtocolUnchanged -and $draftBytesMatch -and -not $draftRead.Value.RealProjectProtocolBytes.PostFinalWriteConfirmed -and -not $draftRead.Value.Summary.OverallPassed
if (-not $evidenceCandidateComplete)
{
    $selectorDecision = "NotProven"
    $remainingGate = "D0-M1 evidence collection/finalization gaps and ADR human confirmation"
}
$null = Set-D0M1AggregateFinalState -Aggregate $aggregate -FinalProtocol $afterDraftWriteProtocol -ProtocolUnchanged $draftProtocolUnchanged -PostFinalWriteConfirmed $false -EvidenceCollectionCompleted $evidenceCandidateComplete -CandidatePassed $allCasesPassed -SelectorDecision $selectorDecision -RemainingGate $remainingGate
$candidateWrite = Write-SafeAggregate -Output $resolvedOutput -ProjectRoot $projectRoot -Aggregate $aggregate
$candidateRead = Read-SafeAggregate -Output $resolvedOutput -ProjectRoot $projectRoot
$afterCandidateWriteProtocol = Get-ProtocolSnapshot -ProjectRoot $projectRoot
$candidateBytesMatch = $candidateRead.LinkCount -eq 1 -and $candidateRead.ByteLength -eq $candidateWrite.ByteLength -and $candidateRead.Sha256 -eq $candidateWrite.Sha256
$candidateReadValidated = $candidateBytesMatch -and $beforeProtocol.AggregateSha256 -eq $afterCandidateWriteProtocol.AggregateSha256 -and -not $candidateRead.Value.RealProjectProtocolBytes.PostFinalWriteConfirmed
$confirmationEligible = $allCasesPassed -and $evidenceCandidateComplete -and $candidateReadValidated
$null = Set-D0M1AggregateFinalState -Aggregate $aggregate -FinalProtocol $afterCandidateWriteProtocol -ProtocolUnchanged ($beforeProtocol.AggregateSha256 -eq $afterCandidateWriteProtocol.AggregateSha256) -PostFinalWriteConfirmed $confirmationEligible -EvidenceCollectionCompleted ($evidenceCandidateComplete -and $candidateReadValidated) -CandidatePassed $allCasesPassed -SelectorDecision $selectorDecision -RemainingGate $remainingGate
$finalWrite = Write-SafeAggregate -Output $resolvedOutput -ProjectRoot $projectRoot -Aggregate $aggregate
$finalRead = Read-SafeAggregate -Output $resolvedOutput -ProjectRoot $projectRoot
$postFinalWriteProtocol = Get-ProtocolSnapshot -ProjectRoot $projectRoot
$finalProtocolUnchanged = $beforeProtocol.AggregateSha256 -eq $postFinalWriteProtocol.AggregateSha256
$finalBytesMatch = $finalRead.LinkCount -eq 1 -and $finalRead.ByteLength -eq $finalWrite.ByteLength -and $finalRead.Sha256 -eq $finalWrite.Sha256
$finalReadValidated = $finalBytesMatch -and $finalProtocolUnchanged -and [bool]$finalRead.Value.RealProjectProtocolBytes.PostFinalWriteConfirmed -eq $confirmationEligible -and [bool]$finalRead.Value.Summary.OverallPassed -eq $confirmationEligible
$overallPassed = $confirmationEligible -and $finalReadValidated
if (-not $finalReadValidated)
{
    $selectorDecision = "NotProven"
    $remainingGate = "D0-M1 aggregate finalization failed; technical evidence gaps and ADR human confirmation"
    $null = Set-D0M1AggregateFinalState -Aggregate $aggregate -FinalProtocol $postFinalWriteProtocol -ProtocolUnchanged $finalProtocolUnchanged -PostFinalWriteConfirmed $false -EvidenceCollectionCompleted $false -CandidatePassed $false -SelectorDecision $selectorDecision -RemainingGate $remainingGate
    $null = Write-SafeAggregate -Output $resolvedOutput -ProjectRoot $projectRoot -Aggregate $aggregate
    Read-SafeAggregate -Output $resolvedOutput -ProjectRoot $projectRoot | Out-Null
}
if (-not $overallPassed) { exit 1 }
exit 0
