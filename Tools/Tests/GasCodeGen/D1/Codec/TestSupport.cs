using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Gas.CodeGen.SourceGenerator.Tests
{
    /// <summary>
    /// 表示一个具名、无外部 test framework 依赖的串行测试动作。
    /// </summary>
    internal sealed class TestCase
    {
        /// <summary>
        /// 创建固定名称与执行委托的测试项。
        /// </summary>
        internal TestCase(string name, Action execute)
        {
            Name = name;
            Execute = execute;
        }

        internal string Name { get; private set; }
        internal Action Execute { get; private set; }
    }

    /// <summary>
    /// 提供最小断言集合，让 contract tests 不引入额外测试包或运行器。
    /// </summary>
    internal static class TestAssert
    {
        /// <summary>
        /// 断言条件为真。
        /// </summary>
        internal static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// 断言两个值按默认 equality 相等。
        /// </summary>
        internal static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " Expected=" + expected + " Actual=" + actual);
            }
        }

        /// <summary>
        /// 断言两个 byte snapshots 完全相等。
        /// </summary>
        internal static void BytesEqual(byte[] expected, byte[] actual, string message)
        {
            if (!GasSourceBundleCodec.BytesEqual(expected, actual))
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// 断言动作抛出任意异常，验证 fail-closed 路径不可静默成功。
        /// </summary>
        internal static void Throws(Action action, string message)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 建立带 production 精确相对路径的独立临时 fixture，并只删除自身 GUID 目录。
    /// </summary>
    internal sealed class D1CodecFixture : IDisposable
    {
        private const string OwnedPrefix = "gas-codegen-d1-codec-";

        /// <summary>
        /// 创建 analyzer、14 项 scaffold 与 canonical selector 所需的独立工程布局。
        /// </summary>
        internal D1CodecFixture()
        {
            ProjectRoot = Path.Combine(Path.GetTempPath(), OwnedPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ProjectRoot);
            CreateRouteFiles();
        }

        internal string ProjectRoot { get; private set; }
        internal string SelectorPath
        {
            get { return GetProjectPath(GasCodeGenContract.SelectorRelativePath); }
        }

        /// <summary>
        /// 创建携带当前 analyzer/scaffold identity 与 exact 五项 source 的合法 bundle。
        /// </summary>
        internal GasSourceBundle CreateBundle(
            IReadOnlyList<GasSourceEntry> entries = null,
            byte[] artifactManifestHash = null)
        {
            return new GasSourceBundle(
                Digest("source-input"),
                Digest("schema"),
                Digest("content"),
                Digest("layout"),
                artifactManifestHash ?? Digest("full-artifact-manifest"),
                GasRouteScaffoldIdentity.ComputeAnalyzerSha256(ProjectRoot),
                GasRouteScaffoldIdentity.ComputeRouteScaffoldSha256(ProjectRoot),
                entries ?? CreateRequiredEntries());
        }

        /// <summary>
        /// 编码并写入 canonical selector，返回与磁盘一致的 raw byte snapshot。
        /// </summary>
        internal byte[] WriteSelector(GasSourceBundle bundle = null)
        {
            byte[] bytes = GasSourceBundleCodec.EncodeSelector(bundle ?? CreateBundle());
            string directory = Path.GetDirectoryName(SelectorPath);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(SelectorPath, bytes);
            return bytes;
        }

        /// <summary>
        /// 返回 v2 contract 的五项 source entry，可指定某个 hint 的 source bytes 替换函数。
        /// </summary>
        internal IReadOnlyList<GasSourceEntry> CreateRequiredEntries(
            Func<RequiredArtifactDefinition, byte[]> sourceFactory = null)
        {
            List<GasSourceEntry> result = new List<GasSourceEntry>();
            foreach (RequiredArtifactDefinition item in GasCodeGenContract.GetRequiredArtifacts())
            {
                if (!item.IsSourceArtifact)
                {
                    continue;
                }

                byte[] source = sourceFactory == null ? CreateSource(item.HintName) : sourceFactory(item);
                result.Add(new GasSourceEntry(item.TargetAssembly, item.HintName, item.Category, source));
            }

            return result.OrderBy(item => item.TargetAssembly, StringComparer.Ordinal)
                .ThenBy(item => item.HintName, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 将 project-relative route path 转换为本 fixture 的绝对路径。
        /// </summary>
        internal string GetProjectPath(string relativePath)
        {
            return Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// 计算稳定测试输入的 SHA-256。
        /// </summary>
        internal static byte[] Digest(string value)
        {
            return GasHashing.ComputeSha256(Encoding.UTF8.GetBytes(value));
        }

        /// <summary>
        /// 创建满足 no-BOM、LF-only 与 XML 类注释约束的最小 C# source。
        /// </summary>
        internal static byte[] CreateSource(string hintName, string extra = "")
        {
            string typeName = new string(hintName.Where(char.IsLetterOrDigit).ToArray());
            string source = "namespace D1.Generated\n"
                + "{\n"
                + "    /// <summary>为 D1 codec 测试提供唯一、可编译的生成类型。</summary>\n"
                + "    internal static class " + typeName + "\n"
                + "    {\n"
                + "        internal const string Value = \"" + extra + "\";\n"
                + "    }\n"
                + "}\n";
            return new UTF8Encoding(false).GetBytes(source);
        }

        /// <summary>
        /// 安全删除本 fixture 创建的精确 GUID temp root。
        /// </summary>
        public void Dispose()
        {
            string fullRoot = Path.GetFullPath(ProjectRoot);
            string tempRoot = Path.GetFullPath(Path.GetTempPath());
            if (!fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(fullRoot).StartsWith(OwnedPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing to delete a non-owned fixture path.");
            }

            if (Directory.Exists(fullRoot))
            {
                Directory.Delete(fullRoot, true);
            }
        }

        /// <summary>
        /// 创建 analyzer DLL 副本与全部 frozen route scaffold 文件，内容保持稳定且各自非 hardlink。
        /// </summary>
        private void CreateRouteFiles()
        {
            string analyzerPath = GetProjectPath(GasCodeGenContract.AnalyzerRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(analyzerPath));
            File.Copy(typeof(GasCodeGenSourceGenerator).Assembly.Location, analyzerPath);
            foreach (string relativePath in GasCodeGenContract.GetRouteScaffoldPaths())
            {
                string path = GetProjectPath(relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes("route:" + relativePath + "\n"));
            }
        }
    }

    /// <summary>
    /// 提供 selector Base64 payload 的确定性拆包、重包和 little-endian mutation 辅助。
    /// </summary>
    internal static class WireMutation
    {
        /// <summary>
        /// 从合法 selector 提取 binary payload。
        /// </summary>
        internal static byte[] ExtractBinary(byte[] selectorBytes)
        {
            string text = Encoding.ASCII.GetString(selectorBytes);
            string base64 = text.Substring(
                GasCodeGenContract.SelectorMagic.Length,
                text.Length - GasCodeGenContract.SelectorMagic.Length - 1);
            return Convert.FromBase64String(base64);
        }

        /// <summary>
        /// 将任意 binary 包装为 canonical 两行 selector，供 decoder 负例定位 binary 层失败。
        /// </summary>
        internal static byte[] WrapBinary(byte[] binary)
        {
            return Encoding.ASCII.GetBytes(
                GasCodeGenContract.SelectorMagic + Convert.ToBase64String(binary) + "\n");
        }

        /// <summary>
        /// 返回 required contract hash 在 binary header 中的精确 offset。
        /// </summary>
        internal static int GetRequiredContractOffset(byte[] binary)
        {
            int offset = 4;
            SkipString(binary, ref offset);
            offset += 32 * 6;
            SkipString(binary, ref offset);
            return offset;
        }

        /// <summary>
        /// 返回 SourceArtifactInventoryHash 在 binary header 中的精确 offset。
        /// </summary>
        internal static int GetInventoryHashOffset(byte[] binary)
        {
            int offset = 4;
            SkipString(binary, ref offset);
            return offset + (32 * 5);
        }

        /// <summary>
        /// 返回 eligibility byte 在 binary header 中的精确 offset。
        /// </summary>
        internal static int GetEligibilityOffset(byte[] binary)
        {
            return GetRequiredContractOffset(binary) + (32 * 3);
        }

        /// <summary>
        /// 返回 source count uint32 在 binary header 中的精确 offset。
        /// </summary>
        internal static int GetCountOffset(byte[] binary)
        {
            return GetEligibilityOffset(binary) + 1;
        }

        /// <summary>
        /// 返回首个 entry target string bytes 的 offset。
        /// </summary>
        internal static int GetFirstTargetBytesOffset(byte[] binary)
        {
            return GetCountOffset(binary) + 8;
        }

        /// <summary>
        /// 返回首个 entry hint string bytes 的 offset。
        /// </summary>
        internal static int GetFirstHintBytesOffset(byte[] binary)
        {
            int targetBytes = GetFirstTargetBytesOffset(binary);
            uint targetLength = ReadUInt32(binary, targetBytes - 4);
            return checked(targetBytes + (int)targetLength + 4);
        }

        /// <summary>
        /// 返回首个 entry category byte 的 offset。
        /// </summary>
        internal static int GetFirstCategoryOffset(byte[] binary)
        {
            int hintBytes = GetFirstHintBytesOffset(binary);
            uint hintLength = ReadUInt32(binary, hintBytes - 4);
            return checked(hintBytes + (int)hintLength);
        }

        /// <summary>
        /// 返回首个 entry source bytes 的精确 offset。
        /// </summary>
        internal static int GetFirstSourceBytesOffset(byte[] binary)
        {
            int offset = GetCountOffset(binary) + 4;
            SkipString(binary, ref offset);
            SkipString(binary, ref offset);
            offset += 1;
            uint sourceLength = ReadUInt32(binary, offset);
            if (sourceLength == 0)
            {
                throw new InvalidDataException("Expected non-empty first source.");
            }

            return offset + 4 + 32;
        }

        /// <summary>
        /// 解析 canonical binary 的五个完整 entry span，供重复、换序和长度边界负例做 byte-level mutation。
        /// </summary>
        internal static IReadOnlyList<WireEntrySpan> GetEntrySpans(byte[] binary)
        {
            int offset = GetCountOffset(binary);
            uint count = ReadUInt32(binary, offset);
            offset += 4;
            List<WireEntrySpan> spans = new List<WireEntrySpan>((int)count);
            for (uint index = 0; index < count; index++)
            {
                int entryStart = offset;
                SkipString(binary, ref offset);
                SkipString(binary, ref offset);
                offset += 1;
                int sourceLengthOffset = offset;
                uint sourceLength = ReadUInt32(binary, offset);
                offset = checked(offset + 4 + 32 + (int)sourceLength);
                spans.Add(new WireEntrySpan(
                    entryStart,
                    offset - entryStart,
                    sourceLengthOffset,
                    sourceLength));
            }

            TestAssert.Equal(binary.Length, offset, "Fixture binary entry parser did not reach exact EOF.");
            return spans;
        }

        /// <summary>
        /// 保留原 header/count，按索引序列重组五个完整 entry bytes。
        /// </summary>
        internal static byte[] ReorderEntries(
            byte[] binary,
            IReadOnlyList<WireEntrySpan> spans,
            IReadOnlyList<int> order)
        {
            if (spans.Count == 0 || order.Count != spans.Count)
            {
                throw new InvalidDataException("Entry reorder requires one index for every wire entry.");
            }

            using (MemoryStream stream = new MemoryStream())
            {
                stream.Write(binary, 0, spans[0].Offset);
                foreach (int index in order)
                {
                    WireEntrySpan span = spans[index];
                    stream.Write(binary, span.Offset, span.Length);
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// 覆写 uint32 little-endian 测试字段。
        /// </summary>
        internal static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        /// <summary>
        /// 读取 uint32 little-endian 测试字段。
        /// </summary>
        internal static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24));
        }

        /// <summary>
        /// 跳过一个 length-prefixed binary string。
        /// </summary>
        private static void SkipString(byte[] bytes, ref int offset)
        {
            uint length = ReadUInt32(bytes, offset);
            offset = checked(offset + 4 + (int)length);
        }
    }

    /// <summary>
    /// 记录测试 binary 中单个完整 source entry 及其 SourceByteLength 字段位置。
    /// </summary>
    internal sealed class WireEntrySpan
    {
        /// <summary>
        /// 创建不可变 wire entry span。
        /// </summary>
        internal WireEntrySpan(int offset, int length, int sourceLengthOffset, uint sourceLength)
        {
            Offset = offset;
            Length = length;
            SourceLengthOffset = sourceLengthOffset;
            SourceLength = sourceLength;
        }

        internal int Offset { get; private set; }
        internal int Length { get; private set; }
        internal int SourceLengthOffset { get; private set; }
        internal uint SourceLength { get; private set; }
    }
}
