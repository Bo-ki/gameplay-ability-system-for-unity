Set-StrictMode -Version Latest

# 将字节编码为小写 SHA-256，作为 scaffold 单文件和聚合身份的唯一散列实现。
function Get-D1BytesSha256
{
    param([Parameter(Mandatory = $true)][byte[]]$Bytes)

    $sha = [Security.Cryptography.SHA256]::Create()
    try
    {
        return [Convert]::ToHexString($sha.ComputeHash($Bytes)).ToLowerInvariant()
    }
    finally
    {
        $sha.Dispose()
    }
}

# 逐段按 ordinal 大小写解析 project-relative 文件，拒绝大小写漂移、逃逸与 reparse point。
function Resolve-D1ExactRelativeFile
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if ([IO.Path]::IsPathRooted($RelativePath) -or
        $RelativePath.Contains("\\", [StringComparison]::Ordinal) -or
        $RelativePath.Split('/').Count -lt 2 -or
        @($RelativePath.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -ne 0)
    {
        throw "Scaffold path must be a canonical forward-slash relative path: $RelativePath"
    }

    $root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not [IO.Directory]::Exists($root)) { throw "Project root does not exist: $root" }
    $current = $root
    foreach ($segment in $RelativePath.Split('/'))
    {
        $matches = @(Get-ChildItem -LiteralPath $current -Force -ErrorAction Stop |
            Where-Object { $_.Name -ceq $segment })
        if ($matches.Count -ne 1) { throw "Scaffold path is missing or has casing drift: $RelativePath" }
        $current = $matches[0].FullName
        if (($matches[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        {
            throw "Scaffold path contains a reparse point: $RelativePath"
        }
    }

    if (-not [IO.File]::Exists($current)) { throw "Scaffold entry is not a file: $RelativePath" }
    $resolved = [IO.Path]::GetFullPath($current)
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Scaffold path escaped the project root: $RelativePath"
    }

    return $resolved
}

# 通过 Win32 文件身份读取硬链接计数，避免同一物理文件伪装成多个 scaffold item。
function Get-D1FileLinkCount
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not ('D1E1.NativeFileIdentity' -as [type]))
    {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace D1E1
{
    /// <summary>只读查询 Windows 文件身份和硬链接计数。</summary>
    public static class NativeFileIdentity
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string path, uint access, uint share, IntPtr security, uint creation,
            uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle handle, out BY_HANDLE_FILE_INFORMATION information);

        /// <summary>返回现存普通文件的 NTFS 硬链接计数。</summary>
        public static uint GetLinkCount(string path)
        {
            using (SafeFileHandle handle = CreateFileW(
                path, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                BY_HANDLE_FILE_INFORMATION information;
                if (!GetFileInformationByHandle(handle, out information))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                return information.NumberOfLinks;
            }
        }
    }
}
'@
    }

    return [uint32][D1E1.NativeFileIdentity]::GetLinkCount($Path)
}

# 按 ADR-0001 冻结算法计算 14 项 immutable route scaffold 身份。
function Get-D1RouteScaffoldSnapshot
{
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$ContractPath
    )

    $contract = Get-Content -LiteralPath $ContractPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $paths = @($contract.Files | ForEach-Object { [string]$_ })
    if ($paths.Count -ne 14 -or @($paths | Sort-Object -Unique).Count -ne 14)
    {
        throw 'Route scaffold contract must contain exactly 14 unique files.'
    }

    $sorted = [string[]]$paths.Clone()
    [Array]::Sort($sorted, [StringComparer]::Ordinal)
    $entries = [Collections.Generic.List[object]]::new()
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream, [Text.Encoding]::UTF8, $true)
    try
    {
        $writer.Write([Text.Encoding]::ASCII.GetBytes("EX-GAS-RouteScaffold-v1`0"))
        $writer.Write([uint32]$sorted.Count)
        foreach ($relativePath in $sorted)
        {
            $absolutePath = Resolve-D1ExactRelativeFile $ProjectRoot $relativePath
            $linkCount = Get-D1FileLinkCount $absolutePath
            if ($linkCount -ne 1) { throw "Scaffold file must have exactly one hard link: $relativePath" }
            $bytes = [IO.File]::ReadAllBytes($absolutePath)
            $pathBytes = [Text.Encoding]::UTF8.GetBytes($relativePath)
            $fileHashBytes = [Security.Cryptography.SHA256]::HashData($bytes)
            $writer.Write([uint32]$pathBytes.Length)
            $writer.Write($pathBytes)
            $writer.Write([uint64]$bytes.LongLength)
            $writer.Write($fileHashBytes)
            $entries.Add([pscustomobject][ordered]@{
                Path = $relativePath
                Length = [long]$bytes.LongLength
                Sha256 = [Convert]::ToHexString($fileHashBytes).ToLowerInvariant()
                LinkCount = $linkCount
            })
        }
        $writer.Flush()
        $aggregate = Get-D1BytesSha256 $stream.ToArray()
    }
    finally
    {
        $writer.Dispose()
        $stream.Dispose()
    }

    return [pscustomobject][ordered]@{
        Schema = 'EX-GAS-D1-RouteScaffoldSnapshot-v1'
        RouteScaffoldSha256 = $aggregate
        EntryCount = $entries.Count
        Entries = @($entries)
    }
}
