using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace GAS.Editor
{
    /// <summary>
    /// 捕获 production analyzer/scaffold 快照并生成供 disposable Unity gate 消费的确定性编译计划。
    /// </summary>
    public static class GasCodeGenCandidateCompileGate
    {
        internal const string SelectorRelativePath =
            "Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile";
        internal const string AnalyzerRelativePath =
            "Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll";
        private const string RouteScaffoldDomain = "EX-GAS-RouteScaffold-v1\0";
        private const string CompilePlanDomain = "EX-GAS-CandidateCompilePlan-v1\0";
        private const string CompilePlanFileName = "CandidateCompilePlan.bin";
        private static readonly string[] s_routeScaffoldPaths =
        {
            "Assets/AutoChessDemo/Generated/GasCodeGenSourceGeneratorRequired.cs",
            "Assets/AutoChessDemo/Generated/GasCodeGenSourceGeneratorRequired.cs.meta",
            "Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef",
            "Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef.meta",
            "Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll.meta",
            "Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile.meta",
            "Assets/GAS/Generated/CodeGen/Editor/GasCodeGenSourceGeneratorRequired.cs",
            "Assets/GAS/Generated/CodeGen/Editor/GasCodeGenSourceGeneratorRequired.cs.meta",
            "Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef",
            "Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef.meta",
            "Assets/GAS/Generated/CodeGen/Runtime/GasCodeGenSourceGeneratorRequired.cs",
            "Assets/GAS/Generated/CodeGen/Runtime/GasCodeGenSourceGeneratorRequired.cs.meta",
            "Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef",
            "Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef.meta",
        };
        private static readonly string[] s_targetAssemblies =
        {
            "com.exhard.exgas.autochessdemo",
            "com.exhard.exgas.generated.editor",
            "com.exhard.exgas.generated.runtime",
        };

        /// <summary>
        /// 读取 analyzer 与 14 个 immutable scaffold 文件一次，返回不可变 route byte snapshot。
        /// </summary>
        internal static GasCodeGenRouteSnapshot CaptureProductionRoute(string projectRoot)
        {
            var resolvedProjectRoot = ResolveProjectRoot(projectRoot);
            var analyzerPath = ResolveProjectFile(resolvedProjectRoot, AnalyzerRelativePath);
            var analyzerBytes = ReadRequiredRegularFile(analyzerPath, AnalyzerRelativePath);
            var entries = new List<GasCodeGenRouteFileSnapshot>(s_routeScaffoldPaths.Length);
            for (var index = 0; index < s_routeScaffoldPaths.Length; index++)
            {
                var relativePath = s_routeScaffoldPaths[index];
                var bytes = ReadRequiredRegularFile(
                    ResolveProjectFile(resolvedProjectRoot, relativePath),
                    relativePath);
                entries.Add(new GasCodeGenRouteFileSnapshot(relativePath, bytes));
            }

            entries.Sort((left, right) => string.CompareOrdinal(left.RelativePath, right.RelativePath));
            var scaffoldBytes = EncodeRouteScaffold(entries);
            return new GasCodeGenRouteSnapshot(
                resolvedProjectRoot,
                analyzerBytes,
                ComputeSha256(analyzerBytes),
                entries.ToArray(),
                scaffoldBytes,
                ComputeSha256(scaffoldBytes));
        }

        /// <summary>
        /// 复核 exact selector candidate 与 route 快照，封存 deterministic compile plan 供独立 Unity E1 执行。
        /// </summary>
        internal static GasCodeGenCandidateCompilePlanSnapshot ValidateFullPackage(
            GasCodeGenCandidateWorkspace workspace,
            GasCodeGenCandidateWorkspace.CandidateAuthority authority,
            GasCodeGenSelectorCandidateSnapshot candidate,
            GasCodeGenRouteSnapshot route)
        {
            if (workspace == null)
                throw new ArgumentNullException(nameof(workspace));
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            workspace.EnsureAuthority(authority);
            route.EnsureUnchanged();
            candidate.EnsureUnchanged();
            if (!string.Equals(candidate.AnalyzerSha256, route.AnalyzerSha256, StringComparison.Ordinal)
                || !string.Equals(candidate.RouteScaffoldSha256, route.RouteScaffoldSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Selector route identity does not match the captured production route.");
            }

            EnsureCandidateContainsOnlyRequiredArtifacts(workspace, candidate);
            var planBytes = EncodeCompilePlan(candidate, route);
            var planPath = Path.Combine(workspace.ControlRoot, CompilePlanFileName);
            WriteBytesDurable(planPath, planBytes);
            return new GasCodeGenCandidateCompilePlanSnapshot(
                planPath,
                planBytes,
                ComputeSha256(planBytes),
                candidate.SelectorSha256,
                route.AnalyzerSha256,
                route.RouteScaffoldSha256);
        }

        /// <summary>
        /// 将 route scaffold 按 ADR-0001 固定 binary domain、计数、路径、长度与 raw SHA 编码。
        /// </summary>
        private static byte[] EncodeRouteScaffold(IReadOnlyList<GasCodeGenRouteFileSnapshot> entries)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Encoding.ASCII.GetBytes(RouteScaffoldDomain));
                writer.Write((uint)entries.Count);
                for (var index = 0; index < entries.Count; index++)
                {
                    WriteUtf8(writer, entries[index].RelativePath);
                    writer.Write((ulong)entries[index].Bytes.LongLength);
                    writer.Write(HexToBytes(entries[index].Sha256));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 编码不依赖 active Bee/RSP 的 compile-plan 身份，仅列出精确 production route 与 source mapping。
        /// </summary>
        private static byte[] EncodeCompilePlan(
            GasCodeGenSelectorCandidateSnapshot candidate,
            GasCodeGenRouteSnapshot route)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Encoding.ASCII.GetBytes(CompilePlanDomain));
                WriteUtf8(writer, SelectorRelativePath);
                WriteUtf8(writer, candidate.SelectorSha256);
                WriteUtf8(writer, AnalyzerRelativePath);
                WriteUtf8(writer, route.AnalyzerSha256);
                WriteUtf8(writer, route.RouteScaffoldSha256);
                writer.Write((uint)s_targetAssemblies.Length);
                for (var index = 0; index < s_targetAssemblies.Length; index++)
                    WriteUtf8(writer, s_targetAssemblies[index]);
                writer.Write((uint)candidate.SourceMappings.Count);
                for (var index = 0; index < candidate.SourceMappings.Count; index++)
                {
                    var mapping = candidate.SourceMappings[index];
                    WriteUtf8(writer, mapping.CanonicalPath);
                    WriteUtf8(writer, mapping.TargetAssembly);
                    WriteUtf8(writer, mapping.HintName);
                    writer.Write(mapping.Category);
                    writer.Write((uint)mapping.SourceByteLength);
                    writer.Write(HexToBytes(mapping.SourceSha256));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 确认 emitter workspace 只含 descriptor 已冻结的六个 artifact、managed meta 与组件 manifest。
        /// </summary>
        private static void EnsureCandidateContainsOnlyRequiredArtifacts(
            GasCodeGenCandidateWorkspace workspace,
            GasCodeGenSelectorCandidateSnapshot candidate)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < candidate.ArtifactFiles.Count; index++)
            {
                allowed.Add(Path.GetFullPath(candidate.ArtifactFiles[index]));
                allowed.Add(Path.GetFullPath(candidate.ArtifactFiles[index] + ".meta"));
            }

            allowed.Add(Path.Combine(workspace.CoreCandidateRoot, "GasCodeGen.manifest.json"));
            allowed.Add(Path.Combine(workspace.AutoChessCandidateRoot, "GasCodeGen.manifest.json"));
            var roots = new[] { workspace.CoreCandidateRoot, workspace.AutoChessCandidateRoot };
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var files = Directory.GetFiles(roots[rootIndex], "*", SearchOption.AllDirectories);
                for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    if (!allowed.Contains(Path.GetFullPath(files[fileIndex])))
                        throw new InvalidDataException("Unregistered or legacy candidate file: " + files[fileIndex]);
                }
            }
        }

        /// <summary>
        /// 使用 write-through 与 Flush(true) 写入候选控制文件，随后按 bytes 重读复核。
        /// </summary>
        private static void WriteBytesDurable(string path, byte[] bytes)
        {
            using (var stream = new FileStream(
                       path,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            var actual = File.ReadAllBytes(path);
            if (!ByteArraysEqual(actual, bytes))
                throw new IOException("Durable compile-plan write verification failed: " + path);
        }

        /// <summary>
        /// 读取普通文件并拒绝 missing、空文件、reparse point，以及 Windows 多链接身份。
        /// </summary>
        private static byte[] ReadRequiredRegularFile(string path, string displayPath)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Required route file is missing: " + displayPath, path);
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Route reparse point is forbidden: " + displayPath);
            using (var stream = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       4096,
                       FileOptions.SequentialScan))
            {
                ValidateWindowsRouteFileIdentity(stream, displayPath);
                if (stream.Length <= 0)
                    throw new InvalidDataException("Required route file is empty: " + displayPath);
                if (stream.Length > int.MaxValue)
                    throw new InvalidDataException("Required route file exceeds the supported byte bound: " + displayPath);
                var bytes = new byte[checked((int)stream.Length)];
                var offset = 0;
                while (offset < bytes.Length)
                {
                    var read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0)
                        throw new EndOfStreamException("Required route file ended while being captured: " + displayPath);
                    offset += read;
                }
                if (stream.ReadByte() >= 0)
                    throw new InvalidDataException("Required route file length changed while being captured: " + displayPath);
                ValidateWindowsRouteFileIdentity(stream, displayPath);
                return bytes;
            }
        }

        /// <summary>
        /// 在 Windows 以已打开 handle 拒绝 reparse point 与 link count 非一，避免路径检查后的 hardlink 替换。
        /// </summary>
        private static void ValidateWindowsRouteFileIdentity(FileStream stream, string displayPath)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return;
            ByHandleFileInformation information;
            if (!GetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(), out information))
            {
                throw new IOException(
                    "Cannot read Windows route file identity: " + displayPath
                    + ", Win32Error=" + Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture));
            }
            if ((((FileAttributes)information.FileAttributes) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Route reparse point is forbidden: " + displayPath);
            if (information.NumberOfLinks != 1)
                throw new InvalidDataException("Route hardlink count must be exactly one: " + displayPath);
        }

        /// <summary>
        /// 调用 Win32 handle identity API，读取文件属性与 hardlink count。
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(
            IntPtr fileHandle,
            out ByHandleFileInformation fileInformation);

        /// <summary>
        /// 映射 Win32 FILETIME 的两个 DWORD，保持 native 四字节对齐。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeFileTime
        {
            internal uint LowDateTime;
            internal uint HighDateTime;
        }

        /// <summary>
        /// 映射 BY_HANDLE_FILE_INFORMATION，仅消费 attributes 与 link count 但保留完整布局。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            internal uint FileAttributes;
            internal NativeFileTime CreationTime;
            internal NativeFileTime LastAccessTime;
            internal NativeFileTime LastWriteTime;
            internal uint VolumeSerialNumber;
            internal uint FileSizeHigh;
            internal uint FileSizeLow;
            internal uint NumberOfLinks;
            internal uint FileIndexHigh;
            internal uint FileIndexLow;
        }

        /// <summary>
        /// 将 project-relative canonical path 解析为项目内绝对路径。
        /// </summary>
        private static string ResolveProjectFile(string projectRoot, string relativePath)
        {
            var path = Path.GetFullPath(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Route path escapes project root: " + relativePath);
            return path;
        }

        /// <summary>
        /// 解析并校验非文件系统根的现有项目目录。
        /// </summary>
        private static string ResolveProjectRoot(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            var resolved = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(resolved))
                throw new DirectoryNotFoundException("Project root does not exist: " + resolved);
            if (string.Equals(resolved, Path.GetPathRoot(resolved), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Project root cannot be a file-system root.");
            return resolved;
        }

        /// <summary>
        /// 写入 uint32 byte-length 前缀的严格 UTF-8 字符串。
        /// </summary>
        private static void WriteUtf8(BinaryWriter writer, string value)
        {
            var bytes = new UTF8Encoding(false, true).GetBytes(value ?? string.Empty);
            writer.Write((uint)bytes.Length);
            writer.Write(bytes);
        }

        /// <summary>
        /// 将 canonical SHA-256 hex 转换为 32 个 raw bytes。
        /// </summary>
        private static byte[] HexToBytes(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
                throw new InvalidDataException("SHA-256 must use 64 lowercase hexadecimal characters.");
            var bytes = new byte[32];
            for (var index = 0; index < bytes.Length; index++)
                bytes[index] = byte.Parse(value.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }

        /// <summary>
        /// 计算小写十六进制 SHA-256。
        /// </summary>
        internal static string ComputeSha256(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (var index = 0; index < hash.Length; index++)
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        /// <summary>
        /// 以常量时间要求之外的普通 byte equality 复核本地不可变快照。
        /// </summary>
        internal static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 保存一个 immutable route scaffold 文件的 canonical path 与完整 byte snapshot。
    /// </summary>
    internal sealed class GasCodeGenRouteFileSnapshot
    {
        /// <summary>
        /// 冻结文件 bytes 并立即计算其 SHA-256。
        /// </summary>
        internal GasCodeGenRouteFileSnapshot(string relativePath, byte[] bytes)
        {
            RelativePath = relativePath;
            Bytes = (byte[])bytes.Clone();
            Sha256 = GasCodeGenCandidateCompileGate.ComputeSha256(Bytes);
        }

        internal string RelativePath { get; }
        internal byte[] Bytes { get; }
        internal string Sha256 { get; }
    }

    /// <summary>
    /// 保存 analyzer 与 14 文件 route scaffold 的单次 byte snapshot，并支持 TOCTOU 复核。
    /// </summary>
    internal sealed class GasCodeGenRouteSnapshot
    {
        /// <summary>
        /// 创建 route 快照；调用方必须传入已按 ordinal 排序的 scaffold entries。
        /// </summary>
        internal GasCodeGenRouteSnapshot(
            string projectRoot,
            byte[] analyzerBytes,
            string analyzerSha256,
            GasCodeGenRouteFileSnapshot[] scaffoldFiles,
            byte[] scaffoldBytes,
            string routeScaffoldSha256)
        {
            ProjectRoot = projectRoot;
            AnalyzerBytes = (byte[])analyzerBytes.Clone();
            AnalyzerSha256 = analyzerSha256;
            ScaffoldFiles = scaffoldFiles;
            ScaffoldBytes = (byte[])scaffoldBytes.Clone();
            RouteScaffoldSha256 = routeScaffoldSha256;
        }

        internal string ProjectRoot { get; }
        internal byte[] AnalyzerBytes { get; }
        internal string AnalyzerSha256 { get; }
        internal IReadOnlyList<GasCodeGenRouteFileSnapshot> ScaffoldFiles { get; }
        internal byte[] ScaffoldBytes { get; }
        internal string RouteScaffoldSha256 { get; }

        /// <summary>
        /// 重读 analyzer/scaffold bytes，任一增删或漂移即拒绝继续封存。
        /// </summary>
        internal void EnsureUnchanged()
        {
            var current = GasCodeGenCandidateCompileGate.CaptureProductionRoute(ProjectRoot);
            if (!string.Equals(current.AnalyzerSha256, AnalyzerSha256, StringComparison.Ordinal)
                || !string.Equals(current.RouteScaffoldSha256, RouteScaffoldSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Production analyzer or route scaffold changed after capture.");
            }
        }
    }

    /// <summary>
    /// 保存 disposable Unity candidate compile plan 的完整 bytes 与三项 route binding。
    /// </summary>
    internal sealed class GasCodeGenCandidateCompilePlanSnapshot
    {
        /// <summary>
        /// 创建已落盘并复核过的 compile-plan 快照。
        /// </summary>
        internal GasCodeGenCandidateCompilePlanSnapshot(
            string path,
            byte[] bytes,
            string sha256,
            string selectorSha256,
            string analyzerSha256,
            string routeScaffoldSha256)
        {
            Path = path;
            Bytes = (byte[])bytes.Clone();
            Sha256 = sha256;
            SelectorSha256 = selectorSha256;
            AnalyzerSha256 = analyzerSha256;
            RouteScaffoldSha256 = routeScaffoldSha256;
        }

        internal string Path { get; }
        internal byte[] Bytes { get; }
        internal string Sha256 { get; }
        internal string SelectorSha256 { get; }
        internal string AnalyzerSha256 { get; }
        internal string RouteScaffoldSha256 { get; }

        /// <summary>
        /// 重读 compile plan，阻止 descriptor seal 前发生磁盘漂移。
        /// </summary>
        internal void EnsureUnchanged()
        {
            var actual = File.ReadAllBytes(Path);
            if (!GasCodeGenCandidateCompileGate.ByteArraysEqual(actual, Bytes))
                throw new InvalidDataException("Candidate compile plan changed after it was sealed.");
        }
    }
}
