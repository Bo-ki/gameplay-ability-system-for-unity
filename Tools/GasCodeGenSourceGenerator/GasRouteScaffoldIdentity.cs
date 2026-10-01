using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Gas.CodeGen.SourceGenerator
{
    /// <summary>
    /// 从 canonical selector 路径反算工程根，并重算 immutable analyzer 与 14 项 route scaffold 身份。
    /// </summary>
    public static class GasRouteScaffoldIdentity
    {
        /// <summary>
        /// 要求 selector 位于冻结 project-relative suffix，并返回其绝对工程根。
        /// </summary>
        public static string ResolveProjectRoot(string selectorPath)
        {
            if (string.IsNullOrEmpty(selectorPath) || !Path.IsPathRooted(selectorPath))
            {
                throw new InvalidDataException("Selector path must be an absolute canonical production path.");
            }

            string absolutePath = Path.GetFullPath(selectorPath);
            string normalized = absolutePath.Replace('\\', '/');
            string suffix = "/" + GasCodeGenContract.SelectorRelativePath;
            if (!normalized.EndsWith(suffix, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Selector path does not end with the frozen production suffix.");
            }

            string root = normalized.Substring(0, normalized.Length - suffix.Length);
            if (root.Length == 0)
            {
                throw new InvalidDataException("Selector path does not contain a project root.");
            }

            return Path.GetFullPath(root.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// 读取冻结 analyzer DLL snapshot 并计算独立 SHA-256。
        /// </summary>
        public static byte[] ComputeAnalyzerSha256(string projectRoot)
        {
            string analyzerPath = ResolveExactProjectFile(projectRoot, GasCodeGenContract.AnalyzerRelativePath);
            return GasHashing.ComputeSha256(File.ReadAllBytes(analyzerPath));
        }

        /// <summary>
        /// 验证 mutable selector 本身位于精确 canonical path，且是非 reparse、非 hardlink 的普通文件。
        /// </summary>
        public static void ValidateSelectorFile(string selectorPath)
        {
            string projectRoot = ResolveProjectRoot(selectorPath);
            string exactPath = ResolveExactProjectFile(projectRoot, GasCodeGenContract.SelectorRelativePath);
            if (!string.Equals(Path.GetFullPath(selectorPath), exactPath, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Selector path is not the exact canonical filesystem entry.");
            }
        }

        /// <summary>
        /// 按 domain、ordinal path、U64 length 与 file SHA 重算 14 项 RouteScaffoldSha256。
        /// </summary>
        public static byte[] ComputeRouteScaffoldSha256(string projectRoot)
        {
            IReadOnlyList<string> paths = GasCodeGenContract.GetRouteScaffoldPaths();
            using (MemoryStream stream = new MemoryStream())
            {
                GasBinaryEncoding.WriteAscii(stream, GasCodeGenContract.RouteScaffoldDomain);
                GasBinaryEncoding.WriteUInt32(stream, checked((uint)paths.Count));
                foreach (string relativePath in paths)
                {
                    string fullPath = ResolveExactProjectFile(projectRoot, relativePath);
                    byte[] fileBytes = File.ReadAllBytes(fullPath);
                    GasBinaryEncoding.WriteString(stream, relativePath);
                    GasBinaryEncoding.WriteUInt64(stream, checked((ulong)fileBytes.LongLength));
                    byte[] fileHash = GasHashing.ComputeSha256(fileBytes);
                    stream.Write(fileHash, 0, fileHash.Length);
                }

                return GasHashing.ComputeSha256(stream.ToArray());
            }
        }

        /// <summary>
        /// 验证 bundle 声明的 analyzer/scaffold 身份与 selector 所在工程的精确 bytes 一致。
        /// </summary>
        public static void ValidateProjectIdentity(string projectRoot, GasSourceBundle bundle)
        {
            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            if (!GasSourceBundleCodec.BytesEqual(ComputeAnalyzerSha256(projectRoot), bundle.AnalyzerSha256))
            {
                throw new InvalidDataException("AnalyzerSha256 does not match the immutable production analyzer bytes.");
            }

            if (!GasSourceBundleCodec.BytesEqual(ComputeRouteScaffoldSha256(projectRoot), bundle.RouteScaffoldSha256))
            {
                throw new InvalidDataException("RouteScaffoldSha256 does not match the frozen 14-file scaffold.");
            }
        }

        /// <summary>
        /// 逐路径段解析精确大小写的普通文件，并拒绝 reparse point 与 Windows hardlink。
        /// </summary>
        private static string ResolveExactProjectFile(string projectRoot, string relativePath)
        {
            if (string.IsNullOrEmpty(projectRoot) || !Path.IsPathRooted(projectRoot))
            {
                throw new InvalidDataException("Project root must be absolute.");
            }

            string current = Path.GetFullPath(projectRoot);
            RejectReparsePoint(current);
            string[] segments = relativePath.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                current = ResolveExactChild(current, segments[index]);
                RejectReparsePoint(current);
            }

            if (!File.Exists(current))
            {
                throw new FileNotFoundException("Required route identity file is missing.", current);
            }

            RejectWindowsHardlink(current);
            return current;
        }

        /// <summary>
        /// 在父目录中按 ordinal 名称解析唯一 child，拒绝仅大小写匹配或 case collision。
        /// </summary>
        private static string ResolveExactChild(string parent, string expectedName)
        {
            if (!Directory.Exists(parent))
            {
                throw new DirectoryNotFoundException("Route scaffold parent directory is missing: " + parent);
            }

            string exact = null;
            int insensitiveMatches = 0;
            foreach (string child in Directory.EnumerateFileSystemEntries(parent))
            {
                string name = Path.GetFileName(child);
                if (string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    insensitiveMatches++;
                    if (string.Equals(name, expectedName, StringComparison.Ordinal))
                    {
                        exact = child;
                    }
                }
            }

            if (insensitiveMatches != 1 || exact == null)
            {
                throw new InvalidDataException("Route scaffold path is missing, duplicated, or has non-canonical casing: " + expectedName);
            }

            return exact;
        }

        /// <summary>
        /// 拒绝路径本身是 junction、symbolic link 或其他 reparse point。
        /// </summary>
        private static void RejectReparsePoint(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Route identity paths must not be reparse points: " + path);
            }
        }

        /// <summary>
        /// 在 Windows 正式运行环境拒绝 link count 不为一的 scaffold 文件。
        /// </summary>
        private static void RejectWindowsHardlink(string path)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                ByHandleFileInformation information;
                if (!GetFileInformationByHandle(stream.SafeFileHandle, out information))
                {
                    throw new IOException("Could not inspect route scaffold hardlink identity: " + path);
                }

                if (information.NumberOfLinks != 1)
                {
                    throw new InvalidDataException("Route scaffold files must not be hardlinked: " + path);
                }
            }
        }

        /// <summary>
        /// 读取 Windows 文件身份和 link count。
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle fileHandle,
            out ByHandleFileInformation fileInformation);

        /// <summary>
        /// 映射 Windows BY_HANDLE_FILE_INFORMATION，仅使用 NumberOfLinks 字段拒绝 hardlink。
        /// </summary>
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
    }
}
