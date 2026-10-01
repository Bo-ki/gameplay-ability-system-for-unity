using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Gas.CodeGen.SourceGenerator
{
    /// <summary>
    /// 编解码唯一 canonical selector 与 GasSourceBundle-v1 binary，并对所有 wire 偏差执行 fail-closed 校验。
    /// </summary>
    public static class GasSourceBundleCodec
    {
        private const int MaximumSelectorByteLength = 100663296;
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// 将已验证 bundle 编码成两行 UTF-8 no-BOM canonical selector bytes。
        /// </summary>
        public static byte[] EncodeSelector(GasSourceBundle bundle)
        {
            ValidateBundle(bundle);
            byte[] binary = EncodeBundleBinary(bundle);
            string base64 = Convert.ToBase64String(binary);
            byte[] selectorBytes = Encoding.ASCII.GetBytes(GasCodeGenContract.SelectorMagic + base64 + "\n");
            GasSourceBundleValidator.ValidateHashReferences(bundle, GasHashing.ComputeSha256(selectorBytes));
            return selectorBytes;
        }

        /// <summary>
        /// 严格解码 canonical selector，验证 EOF、required-set、inventory 与全部 source hash 禁令。
        /// </summary>
        public static GasSourceBundle DecodeSelector(byte[] selectorBytes)
        {
            byte[] binary = DecodeCanonicalSelectorText(selectorBytes);
            GasSourceBundle bundle = DecodeBundleBinary(binary);
            GasSourceBundleValidator.ValidateHashReferences(bundle, GasHashing.ComputeSha256(selectorBytes));
            return bundle;
        }

        /// <summary>
        /// 计算 selector raw byte snapshot 的 canonical lowercase SHA-256。
        /// </summary>
        public static string ComputeSelectorSha256Hex(byte[] selectorBytes)
        {
            if (selectorBytes == null)
            {
                throw new ArgumentNullException(nameof(selectorBytes));
            }

            return GasHashing.ToLowerHex(GasHashing.ComputeSha256(selectorBytes));
        }

        /// <summary>
        /// 按冻结 inventory domain 和 entry metadata 计算独立 C# subset hash，不读取完整 artifact manifest。
        /// </summary>
        public static byte[] ComputeSourceArtifactInventoryHash(IReadOnlyList<GasSourceEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            GasSourceBundleValidator.ValidateRequiredSources(entries);
            GasSourceBundleValidator.ValidateWireOrder(entries);
            using (MemoryStream stream = new MemoryStream())
            {
                GasBinaryEncoding.WriteAscii(stream, GasCodeGenContract.SourceArtifactInventoryDomain);
                GasBinaryEncoding.WriteUInt32(stream, checked((uint)entries.Count));
                foreach (GasSourceEntry entry in entries)
                {
                    GasBinaryEncoding.WriteString(stream, entry.TargetAssembly);
                    GasBinaryEncoding.WriteString(stream, entry.HintName);
                    stream.WriteByte(entry.ArtifactCategory);
                    GasBinaryEncoding.WriteUInt32(stream, entry.SourceByteLength);
                    byte[] sourceHash = entry.SourceSha256;
                    stream.Write(sourceHash, 0, sourceHash.Length);
                }

                return GasHashing.ComputeSha256(stream.ToArray());
            }
        }

        /// <summary>
        /// 将 digest 转为协议与 marker 共用的 canonical lowercase hex。
        /// </summary>
        public static string ToLowerHex(byte[] digest)
        {
            return GasHashing.ToLowerHex(digest);
        }

        /// <summary>
        /// 对 writer 输入执行 required-set、canonical source 和冻结 contract 自校验。
        /// </summary>
        private static void ValidateBundle(GasSourceBundle bundle)
        {
            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            GasCodeGenContract.ValidateFrozenContractHash();
            GasSourceBundleValidator.ValidateRequiredSources(bundle.Entries);
            GasSourceBundleValidator.ValidateWireOrder(bundle.Entries);
            GasSourceBundleValidator.ValidateSourceBytes(bundle.Entries);
            GasSourceBundleValidator.ValidatePreSelectorHashReferences(bundle);
        }

        /// <summary>
        /// 按冻结 header 与 entry 顺序编码 binary payload。
        /// </summary>
        private static byte[] EncodeBundleBinary(GasSourceBundle bundle)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                GasBinaryEncoding.WriteUInt32(stream, GasCodeGenContract.BundleVersion);
                GasBinaryEncoding.WriteString(stream, GasCodeGenContract.BundleEncodingDomain);
                WriteDigest(stream, bundle.SourceInputHash);
                WriteDigest(stream, bundle.SchemaHash);
                WriteDigest(stream, bundle.ContentHash);
                WriteDigest(stream, bundle.LayoutHash);
                WriteDigest(stream, bundle.ArtifactManifestHash);
                WriteDigest(stream, bundle.SourceArtifactInventoryHash);
                GasBinaryEncoding.WriteString(stream, GasCodeGenContract.RequiredArtifactSetId);
                WriteDigest(stream, GasCodeGenContract.ComputeRequiredArtifactSetContractHash());
                WriteDigest(stream, bundle.AnalyzerSha256);
                WriteDigest(stream, bundle.RouteScaffoldSha256);
                stream.WriteByte(0);
                GasBinaryEncoding.WriteUInt32(stream, checked((uint)bundle.Entries.Count));
                foreach (GasSourceEntry entry in bundle.Entries)
                {
                    EncodeEntry(stream, entry);
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// 编码一个已验证的 source entry。
        /// </summary>
        private static void EncodeEntry(Stream stream, GasSourceEntry entry)
        {
            GasBinaryEncoding.WriteString(stream, entry.TargetAssembly);
            GasBinaryEncoding.WriteString(stream, entry.HintName);
            stream.WriteByte(entry.ArtifactCategory);
            GasBinaryEncoding.WriteUInt32(stream, entry.SourceByteLength);
            WriteDigest(stream, entry.SourceSha256);
            byte[] sourceBytes = entry.SourceBytes;
            stream.Write(sourceBytes, 0, sourceBytes.Length);
        }

        /// <summary>
        /// 严格验证两行 selector 文本并返回 canonical Base64 解码后的 binary。
        /// </summary>
        private static byte[] DecodeCanonicalSelectorText(byte[] selectorBytes)
        {
            if (selectorBytes == null)
            {
                throw new ArgumentNullException(nameof(selectorBytes));
            }

            if (selectorBytes.Length == 0 || selectorBytes.Length > MaximumSelectorByteLength)
            {
                throw new InvalidDataException("Selector byte length is outside the supported protocol bound.");
            }

            string selectorText;
            try
            {
                selectorText = StrictUtf8.GetString(selectorBytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("Selector is not strict UTF-8.", exception);
            }

            if (!selectorText.StartsWith(GasCodeGenContract.SelectorMagic, StringComparison.Ordinal)
                || !selectorText.EndsWith("\n", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Selector magic or final LF is not canonical.");
            }

            string base64 = selectorText.Substring(
                GasCodeGenContract.SelectorMagic.Length,
                selectorText.Length - GasCodeGenContract.SelectorMagic.Length - 1);
            ValidateCanonicalBase64(base64);
            byte[] binary = Convert.FromBase64String(base64);
            if (!string.Equals(Convert.ToBase64String(binary), base64, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Selector Base64 is not canonical RFC 4648 encoding.");
            }

            byte[] expectedBytes = Encoding.ASCII.GetBytes(GasCodeGenContract.SelectorMagic + base64 + "\n");
            if (!BytesEqual(selectorBytes, expectedBytes))
            {
                throw new InvalidDataException("Selector bytes are not exact ASCII canonical text.");
            }

            return binary;
        }

        /// <summary>
        /// 验证 Base64 只含 standard alphabet、尾部必要 padding 且没有任何空白。
        /// </summary>
        private static void ValidateCanonicalBase64(string value)
        {
            if (value.Length == 0 || (value.Length & 3) != 0)
            {
                throw new InvalidDataException("Selector Base64 length is not canonical.");
            }

            int paddingStart = value.IndexOf('=');
            if (paddingStart >= 0 && (value.Length - paddingStart > 2))
            {
                throw new InvalidDataException("Selector Base64 padding is not canonical.");
            }

            for (int index = 0; index < value.Length; index++)
            {
                char item = value[index];
                bool data = (item >= 'A' && item <= 'Z')
                    || (item >= 'a' && item <= 'z')
                    || (item >= '0' && item <= '9')
                    || item == '+' || item == '/';
                bool padding = item == '=' && paddingStart >= 0 && index >= paddingStart;
                if (!data && !padding)
                {
                    throw new InvalidDataException("Selector Base64 contains non-standard characters or whitespace.");
                }
            }
        }

        /// <summary>
        /// 解码 binary header、五项 entry 和 exact EOF。
        /// </summary>
        private static GasSourceBundle DecodeBundleBinary(byte[] binary)
        {
            GasBundleReader reader = new GasBundleReader(binary);
            ValidateHeaderPrefix(reader);
            byte[] sourceInputHash = reader.ReadDigest();
            byte[] schemaHash = reader.ReadDigest();
            byte[] contentHash = reader.ReadDigest();
            byte[] layoutHash = reader.ReadDigest();
            byte[] artifactManifestHash = reader.ReadDigest();
            byte[] declaredInventoryHash = reader.ReadDigest();
            ValidateHeaderSuffix(reader);
            byte[] analyzerHash = reader.ReadDigest();
            byte[] scaffoldHash = reader.ReadDigest();
            ValidateEligibility(reader);
            List<GasSourceEntry> entries = DecodeEntries(reader);
            reader.RequireEndOfFile();
            GasSourceBundleValidator.ValidateWireOrder(entries);
            GasSourceBundle bundle = new GasSourceBundle(
                sourceInputHash, schemaHash, contentHash, layoutHash, artifactManifestHash,
                analyzerHash, scaffoldHash, entries);
            if (!BytesEqual(bundle.SourceArtifactInventoryHash, declaredInventoryHash))
            {
                throw new InvalidDataException("SourceArtifactInventoryHash does not match the C# subset metadata.");
            }

            GasSourceBundleValidator.ValidateSourceBytes(bundle.Entries);
            return bundle;
        }

        /// <summary>
        /// 验证 binary version 与 encoding domain。
        /// </summary>
        private static void ValidateHeaderPrefix(GasBundleReader reader)
        {
            if (reader.ReadUInt32() != GasCodeGenContract.BundleVersion)
            {
                throw new InvalidDataException("Unsupported bundle version.");
            }

            if (!string.Equals(reader.ReadString(64), GasCodeGenContract.BundleEncodingDomain, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Bundle encoding domain is not the frozen v1 value.");
            }
        }

        /// <summary>
        /// 验证 required-set ID 与重算后的 exact contract hash。
        /// </summary>
        private static void ValidateHeaderSuffix(GasBundleReader reader)
        {
            GasCodeGenContract.ValidateFrozenContractHash();
            if (!string.Equals(reader.ReadString(128), GasCodeGenContract.RequiredArtifactSetId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Required artifact set ID is not the frozen v2 value.");
            }

            if (!BytesEqual(reader.ReadDigest(), GasCodeGenContract.ComputeRequiredArtifactSetContractHash()))
            {
                throw new InvalidDataException("Required artifact set contract hash is invalid.");
            }
        }

        /// <summary>
        /// 验证 D1 eligibility byte 必须严格为零。
        /// </summary>
        private static void ValidateEligibility(GasBundleReader reader)
        {
            if (reader.ReadByte() != 0)
            {
                throw new InvalidDataException("D1 FullSemanticEligibility must be zero.");
            }
        }

        /// <summary>
        /// 读取并验证 exact 五项 source entry 与绝对 count 上限。
        /// </summary>
        private static List<GasSourceEntry> DecodeEntries(GasBundleReader reader)
        {
            uint count = reader.ReadUInt32();
            if (count > GasCodeGenContract.MaximumSourceCount)
            {
                throw new InvalidDataException("Source artifact count exceeds the decoder absolute limit.");
            }

            if (count != GasCodeGenContract.RequiredSourceCount)
            {
                throw new InvalidDataException("Bundle must contain exactly five required source entries.");
            }

            List<GasSourceEntry> entries = new List<GasSourceEntry>((int)count);
            for (uint index = 0; index < count; index++)
            {
                entries.Add(DecodeEntry(reader));
            }

            return entries;
        }

        /// <summary>
        /// 读取单个 entry 并在建立 model 前验证声明 SHA 与 source bytes 一致。
        /// </summary>
        private static GasSourceEntry DecodeEntry(GasBundleReader reader)
        {
            string targetAssembly = reader.ReadString(128);
            string hintName = reader.ReadString(255);
            byte category = reader.ReadByte();
            uint sourceLength = reader.ReadUInt32();
            if (sourceLength < 1 || sourceLength > GasCodeGenContract.MaximumSourceByteLength)
            {
                throw new InvalidDataException("Source byte length is outside the frozen range.");
            }

            byte[] declaredHash = reader.ReadDigest();
            byte[] sourceBytes = reader.ReadBytes((int)sourceLength);
            if (!BytesEqual(declaredHash, GasHashing.ComputeSha256(sourceBytes)))
            {
                throw new InvalidDataException("SourceSha256 does not match source bytes.");
            }

            return new GasSourceEntry(targetAssembly, hintName, category, sourceBytes);
        }

        /// <summary>
        /// 写入恰好 32-byte digest。
        /// </summary>
        private static void WriteDigest(Stream stream, byte[] digest)
        {
            if (digest == null || digest.Length != 32)
            {
                throw new InvalidDataException("Bundle digest must contain exactly 32 bytes.");
            }

            stream.Write(digest, 0, digest.Length);
        }

        /// <summary>
        /// 逐 byte 比较两个 protocol snapshot。
        /// </summary>
        internal static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;
            for (int index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }
    }

    /// <summary>
    /// 在 byte array 上执行有界 little-endian、strict UTF-8 读取，并保证无法越过已声明 payload。
    /// </summary>
    internal sealed class GasBundleReader
    {
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly byte[] bytes;
        private int position;

        /// <summary>
        /// 从不可变 binary snapshot 创建 reader。
        /// </summary>
        internal GasBundleReader(byte[] bytes)
        {
            this.bytes = bytes == null ? throw new ArgumentNullException(nameof(bytes)) : (byte[])bytes.Clone();
        }

        /// <summary>
        /// 读取单个 byte。
        /// </summary>
        internal byte ReadByte()
        {
            RequireAvailable(1);
            return bytes[position++];
        }

        /// <summary>
        /// 读取 uint32 little-endian。
        /// </summary>
        internal uint ReadUInt32()
        {
            RequireAvailable(4);
            uint value = (uint)(bytes[position]
                | (bytes[position + 1] << 8)
                | (bytes[position + 2] << 16)
                | (bytes[position + 3] << 24));
            position += 4;
            return value;
        }

        /// <summary>
        /// 读取恰好 32-byte digest。
        /// </summary>
        internal byte[] ReadDigest()
        {
            return ReadBytes(32);
        }

        /// <summary>
        /// 读取受 byte 上限约束的 length-prefixed strict UTF-8 字符串。
        /// </summary>
        internal string ReadString(int maximumByteLength)
        {
            uint length = ReadUInt32();
            if (length > maximumByteLength)
            {
                throw new InvalidDataException("Length-prefixed UTF-8 field exceeds its protocol limit.");
            }

            byte[] value = ReadBytes((int)length);
            try
            {
                return StrictUtf8.GetString(value);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("Length-prefixed field is not strict UTF-8.", exception);
            }
        }

        /// <summary>
        /// 读取精确长度的 byte snapshot。
        /// </summary>
        internal byte[] ReadBytes(int count)
        {
            RequireAvailable(count);
            byte[] result = new byte[count];
            Buffer.BlockCopy(bytes, position, result, 0, count);
            position += count;
            return result;
        }

        /// <summary>
        /// 要求最后一个 entry 后立即 EOF，拒绝 padding 和 trailing bytes。
        /// </summary>
        internal void RequireEndOfFile()
        {
            if (position != bytes.Length)
            {
                throw new InvalidDataException("Bundle contains trailing bytes after the final entry.");
            }
        }

        /// <summary>
        /// 验证下一次读取不会越过 binary snapshot。
        /// </summary>
        private void RequireAvailable(int count)
        {
            if (count < 0 || position > bytes.Length - count)
            {
                throw new EndOfStreamException("Bundle ended before the declared field was complete.");
            }
        }
    }
}
