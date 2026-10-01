using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Gas.CodeGen.SourceGenerator
{
    /// <summary>
    /// 对 GasSourceBundle-v1 执行 required source、UTF-8、排序与无自哈希闭环验证，所有偏差均 fail closed。
    /// </summary>
    public static class GasSourceBundleValidator
    {
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// 验证 bundle 必须与 v2 required set 的五项 C# subset 形成精确、无重复的 source 集合。
        /// </summary>
        public static void ValidateRequiredSources(IReadOnlyList<GasSourceEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            RequiredArtifactDefinition[] expected = GasCodeGenContract.GetRequiredArtifacts()
                .Where(item => item.IsSourceArtifact)
                .OrderBy(item => item.TargetAssembly, StringComparer.Ordinal)
                .ThenBy(item => item.HintName, StringComparer.Ordinal)
                .ToArray();
            if (entries.Count != GasCodeGenContract.RequiredSourceCount || entries.Count != expected.Length)
            {
                throw new InvalidDataException("Bundle must contain exactly the five v2 required C# sources.");
            }

            for (int index = 0; index < entries.Count; index++)
            {
                GasSourceEntry actual = entries[index];
                RequiredArtifactDefinition required = expected[index];
                ValidateEntryShape(actual);
                if (!string.Equals(actual.TargetAssembly, required.TargetAssembly, StringComparison.Ordinal)
                    || !string.Equals(actual.HintName, required.HintName, StringComparison.Ordinal)
                    || actual.ArtifactCategory != required.Category)
                {
                    throw new InvalidDataException("Bundle source set does not match the frozen v2 target/hint/category contract.");
                }
            }
        }

        /// <summary>
        /// 验证 entry 在 wire 中已按 target/hint ordinal 严格升序，禁止重复 tuple 或 reader 暗中重排。
        /// </summary>
        public static void ValidateWireOrder(IReadOnlyList<GasSourceEntry> entries)
        {
            for (int index = 1; index < entries.Count; index++)
            {
                GasSourceEntry previous = entries[index - 1];
                GasSourceEntry current = entries[index];
                int targetOrder = string.Compare(previous.TargetAssembly, current.TargetAssembly, StringComparison.Ordinal);
                int hintOrder = targetOrder == 0
                    ? string.Compare(previous.HintName, current.HintName, StringComparison.Ordinal)
                    : 0;
                if (targetOrder > 0 || (targetOrder == 0 && hintOrder >= 0))
                {
                    throw new InvalidDataException("Bundle entries must be unique and ordinal-sorted by target assembly and hint name.");
                }
            }
        }

        /// <summary>
        /// 验证 source bytes 严格 UTF-8、无 BOM、LF-only、无 NUL，并满足单项与总量上限。
        /// </summary>
        public static void ValidateSourceBytes(IReadOnlyList<GasSourceEntry> entries)
        {
            long totalLength = 0;
            foreach (GasSourceEntry entry in entries)
            {
                byte[] sourceBytes = entry.SourceBytes;
                if (sourceBytes.Length < 1 || sourceBytes.Length > GasCodeGenContract.MaximumSourceByteLength)
                {
                    throw new InvalidDataException("Source byte length is outside the frozen range.");
                }

                totalLength += sourceBytes.Length;
                if (totalLength > GasCodeGenContract.MaximumTotalSourceByteLength)
                {
                    throw new InvalidDataException("Total source byte length exceeds the frozen limit.");
                }

                ValidateCanonicalSourceText(sourceBytes);
            }
        }

        /// <summary>
        /// 验证 C# subset 不嵌入完整 manifest、selector、inventory 或自身 source digest 的 raw/hex 表示。
        /// </summary>
        public static void ValidateHashReferences(GasSourceBundle bundle, byte[] selectorSha256)
        {
            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            ValidateDigest(selectorSha256, nameof(selectorSha256));
            byte[] artifactManifestHash = bundle.ArtifactManifestHash;
            byte[] inventoryHash = bundle.SourceArtifactInventoryHash;
            foreach (GasSourceEntry entry in bundle.Entries)
            {
                byte[] sourceBytes = entry.SourceBytes;
                RejectDigestReference(sourceBytes, artifactManifestHash, "ArtifactManifestHash");
                RejectDigestReference(sourceBytes, selectorSha256, "SelectorSha256");
                RejectDigestReference(sourceBytes, inventoryHash, "SourceArtifactInventoryHash");
                RejectDigestReference(sourceBytes, entry.SourceSha256, "SourceSha256");
            }
        }

        /// <summary>
        /// 在 selector 尚未形成时验证所有不依赖 selector SHA 的自哈希禁令。
        /// </summary>
        internal static void ValidatePreSelectorHashReferences(GasSourceBundle bundle)
        {
            byte[] artifactManifestHash = bundle.ArtifactManifestHash;
            byte[] inventoryHash = bundle.SourceArtifactInventoryHash;
            foreach (GasSourceEntry entry in bundle.Entries)
            {
                byte[] sourceBytes = entry.SourceBytes;
                RejectDigestReference(sourceBytes, artifactManifestHash, "ArtifactManifestHash");
                RejectDigestReference(sourceBytes, inventoryHash, "SourceArtifactInventoryHash");
                RejectDigestReference(sourceBytes, entry.SourceSha256, "SourceSha256");
            }
        }

        /// <summary>
        /// 验证单个 source entry 的目标、hint、category 和保留名约束。
        /// </summary>
        private static void ValidateEntryShape(GasSourceEntry entry)
        {
            if (entry == null)
            {
                throw new InvalidDataException("Bundle source entry cannot be null.");
            }

            int targetLength = StrictUtf8.GetByteCount(entry.TargetAssembly);
            int hintLength = StrictUtf8.GetByteCount(entry.HintName);
            if (targetLength < 1 || targetLength > 128 || !GasCodeGenContract.IsTargetAssembly(entry.TargetAssembly))
            {
                throw new InvalidDataException("Target assembly is outside the frozen allowlist.");
            }

            if (hintLength < 1 || hintLength > 255 || !IsCanonicalHintName(entry.HintName))
            {
                throw new InvalidDataException("Hint name is not canonical.");
            }

            if (string.Equals(entry.HintName, GasCodeGenContract.MarkerHintName, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Bundle entry uses the generator-reserved marker hint name.");
            }

            if (entry.ArtifactCategory != GasCodeGenContract.GetExpectedCategory(entry.TargetAssembly))
            {
                throw new InvalidDataException("Artifact category does not match the target assembly.");
            }
        }

        /// <summary>
        /// 验证 hint 符合冻结 ASCII 正则且不含任何路径分隔符。
        /// </summary>
        private static bool IsCanonicalHintName(string value)
        {
            if (value.Length < 1 || !IsAsciiLetterOrDigit(value[0]))
            {
                return false;
            }

            for (int index = 1; index < value.Length; index++)
            {
                char item = value[index];
                if (!IsAsciiLetterOrDigit(item) && item != '_' && item != '.' && item != '-')
                {
                    return false;
                }
            }

            return value.IndexOf('/') < 0 && value.IndexOf('\\') < 0;
        }

        /// <summary>
        /// 判断字符是否属于 hint 允许的 ASCII 字母或数字。
        /// </summary>
        private static bool IsAsciiLetterOrDigit(char value)
        {
            return (value >= 'A' && value <= 'Z')
                || (value >= 'a' && value <= 'z')
                || (value >= '0' && value <= '9');
        }

        /// <summary>
        /// 验证 source 是 no-BOM、LF-only、无 NUL 的严格 UTF-8 文本。
        /// </summary>
        private static void ValidateCanonicalSourceText(byte[] sourceBytes)
        {
            if (sourceBytes.Length >= 3 && sourceBytes[0] == 0xef && sourceBytes[1] == 0xbb && sourceBytes[2] == 0xbf)
            {
                throw new InvalidDataException("Source bytes must not contain a UTF-8 BOM.");
            }

            for (int index = 0; index < sourceBytes.Length; index++)
            {
                if (sourceBytes[index] == 0 || sourceBytes[index] == (byte)'\r')
                {
                    throw new InvalidDataException("Source bytes must be NUL-free and LF-only.");
                }
            }

            try
            {
                StrictUtf8.GetString(sourceBytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("Source bytes are not strict UTF-8.", exception);
            }
        }

        /// <summary>
        /// 拒绝 source 中出现指定 digest 的 raw、lowercase hex 或 uppercase hex bytes。
        /// </summary>
        internal static void RejectDigestReference(byte[] sourceBytes, byte[] digest, string fieldName)
        {
            ValidateDigest(digest, fieldName);
            byte[] lowerHex = Encoding.ASCII.GetBytes(GasHashing.ToLowerHex(digest));
            if (ContainsSequence(sourceBytes, digest)
                || ContainsHexSequenceIgnoreCase(sourceBytes, lowerHex))
            {
                throw new InvalidDataException("Source bytes contain a forbidden " + fieldName + " representation.");
            }
        }

        /// <summary>
        /// 验证 digest 长度恰为 32 bytes。
        /// </summary>
        private static void ValidateDigest(byte[] digest, string fieldName)
        {
            if (digest == null || digest.Length != 32)
            {
                throw new ArgumentException("Digest must contain exactly 32 bytes.", fieldName);
            }
        }

        /// <summary>
        /// 以逐 byte ordinal 匹配判断 source 是否包含指定身份表示。
        /// </summary>
        private static bool ContainsSequence(byte[] source, byte[] value)
        {
            if (value.Length == 0 || source.Length < value.Length)
            {
                return false;
            }

            for (int start = 0; start <= source.Length - value.Length; start++)
            {
                int offset = 0;
                while (offset < value.Length && source[start + offset] == value[offset])
                {
                    offset++;
                }

                if (offset == value.Length)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 以 ASCII 大小写不敏感方式匹配 64-character hex，拒绝 mixed-case digest 表示。
        /// </summary>
        private static bool ContainsHexSequenceIgnoreCase(byte[] source, byte[] lowercaseHex)
        {
            if (source.Length < lowercaseHex.Length)
            {
                return false;
            }

            for (int start = 0; start <= source.Length - lowercaseHex.Length; start++)
            {
                int offset = 0;
                while (offset < lowercaseHex.Length
                    && ToLowerAscii(source[start + offset]) == lowercaseHex[offset])
                {
                    offset++;
                }

                if (offset == lowercaseHex.Length)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 仅将 ASCII A-F 转换为小写，其他 byte 保持原值以避免 culture 参与。
        /// </summary>
        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'F'
                ? (byte)(value + ((byte)'a' - (byte)'A'))
                : value;
        }
    }
}
