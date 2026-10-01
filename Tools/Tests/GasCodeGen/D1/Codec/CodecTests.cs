using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Gas.CodeGen.SourceGenerator.Tests
{
    /// <summary>
    /// 覆盖 GasSourceBundle-v1 canonical wire、strict decoder、v2 source contract 与自哈希禁令。
    /// </summary>
    internal static class CodecTests
    {
        /// <summary>
        /// 返回 D1-A codec 固定 case 集。
        /// </summary>
        internal static IReadOnlyList<TestCase> GetCases()
        {
            return new[]
            {
                new TestCase("CODEC-01 canonical roundtrip and deterministic bytes", CanonicalRoundtrip),
                new TestCase("CODEC-02 exact required contract hash", RequiredContractHash),
                new TestCase("CODEC-03 reject selector BOM CRLF whitespace and invalid UTF8", RejectSelectorTextDrift),
                new TestCase("CODEC-04 reject binary trailing bytes", RejectTrailingBytes),
                new TestCase("CODEC-05 reject binary strict UTF8 drift", RejectBinaryUtf8Drift),
                new TestCase("CODEC-06 reject required contract hash drift", RejectContractHashDrift),
                new TestCase("CODEC-07 reject eligibility true", RejectEligibilityTrue),
                new TestCase("CODEC-08 reject required source missing and extra", RejectMissingAndExtraSources),
                new TestCase("CODEC-09 reject target hint and category drift", RejectRoutingDrift),
                new TestCase("CODEC-10 reject reserved marker hint", RejectReservedHint),
                new TestCase("CODEC-11 reject noncanonical source bytes", RejectNonCanonicalSources),
                new TestCase("CODEC-12 reject artifact selector inventory and source hash references", RejectHashReferences),
                new TestCase("CODEC-13 keep full manifest and source inventory identities distinct", PreserveDistinctIdentities),
                new TestCase("CODEC-14 reject inventory and source SHA wire drift", RejectInventoryAndSourceHashDrift),
                new TestCase("CODEC-15 reject duplicate length bounds payload mismatch and entry reorder", RejectStructuralEntryDrift),
            };
        }

        /// <summary>
        /// 证明同一 model 两次编码逐 byte 相同，解码后 header 与五项 entry 不丢失。
        /// </summary>
        private static void CanonicalRoundtrip()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                GasSourceBundle bundle = fixture.CreateBundle();
                byte[] first = GasSourceBundleCodec.EncodeSelector(bundle);
                byte[] second = GasSourceBundleCodec.EncodeSelector(bundle);
                TestAssert.BytesEqual(first, second, "Selector encoding must be deterministic.");
                GasSourceBundle decoded = GasSourceBundleCodec.DecodeSelector(first);
                TestAssert.Equal(5, decoded.Entries.Count, "Decoded source count changed.");
                TestAssert.BytesEqual(bundle.ArtifactManifestHash, decoded.ArtifactManifestHash,
                    "ArtifactManifestHash changed during roundtrip.");
                TestAssert.BytesEqual(bundle.SourceArtifactInventoryHash, decoded.SourceArtifactInventoryHash,
                    "SourceArtifactInventoryHash changed during roundtrip.");
            }
        }

        /// <summary>
        /// 独立重算 frozen required-set contract hash 并匹配文档常量。
        /// </summary>
        private static void RequiredContractHash()
        {
            string actual = GasSourceBundleCodec.ToLowerHex(GasCodeGenContract.ComputeRequiredArtifactSetContractHash());
            TestAssert.Equal(GasCodeGenContract.RequiredArtifactSetContractHashHex, actual,
                "Required artifact set contract hash drifted.");
        }

        /// <summary>
        /// 验证 selector 层拒绝 BOM、CRLF、Base64 空白与非法 UTF-8。
        /// </summary>
        private static void RejectSelectorTextDrift()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                byte[] canonical = GasSourceBundleCodec.EncodeSelector(fixture.CreateBundle());
                TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(PrependBom(canonical)), "BOM was accepted.");
                TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(ReplaceLfWithCrLf(canonical)), "CRLF was accepted.");
                TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(InsertBase64Whitespace(canonical)), "Whitespace was accepted.");
                byte[] invalidUtf8 = (byte[])canonical.Clone();
                invalidUtf8[0] = 0xff;
                TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(invalidUtf8), "Invalid selector UTF-8 was accepted.");
            }
        }

        /// <summary>
        /// 验证最后一个 entry 后的任意 padding byte 都被 exact EOF 拒绝。
        /// </summary>
        private static void RejectTrailingBytes()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                byte[] binary = WireMutation.ExtractBinary(GasSourceBundleCodec.EncodeSelector(fixture.CreateBundle()));
                Array.Resize(ref binary, binary.Length + 1);
                TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(WireMutation.WrapBinary(binary)),
                    "Trailing binary byte was accepted.");
            }
        }

        /// <summary>
        /// 验证 length-prefixed header string 使用严格 UTF-8 decoder。
        /// </summary>
        private static void RejectBinaryUtf8Drift()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                byte[] binary = WireMutation.ExtractBinary(GasSourceBundleCodec.EncodeSelector(fixture.CreateBundle()));
                binary[8] = 0xff;
                TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(WireMutation.WrapBinary(binary)),
                    "Invalid bundle UTF-8 was accepted.");
            }
        }

        /// <summary>
        /// 验证 required-set 声明 hash 不能替换为任意 digest。
        /// </summary>
        private static void RejectContractHashDrift()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                byte[] binary = WireMutation.ExtractBinary(GasSourceBundleCodec.EncodeSelector(fixture.CreateBundle()));
                binary[WireMutation.GetRequiredContractOffset(binary)] ^= 0x01;
                TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(WireMutation.WrapBinary(binary)),
                    "Required contract hash drift was accepted.");
            }
        }

        /// <summary>
        /// 验证 D1 bundle 的 FullSemanticEligibility 只能为零。
        /// </summary>
        private static void RejectEligibilityTrue()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                byte[] binary = WireMutation.ExtractBinary(GasSourceBundleCodec.EncodeSelector(fixture.CreateBundle()));
                binary[WireMutation.GetEligibilityOffset(binary)] = 1;
                TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(WireMutation.WrapBinary(binary)),
                    "Eligibility=true was accepted.");
            }
        }

        /// <summary>
        /// 验证 count=4、count=6 以及 model 层缺项/多项均不能建立合法 bundle。
        /// </summary>
        private static void RejectMissingAndExtraSources()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                IReadOnlyList<GasSourceEntry> required = fixture.CreateRequiredEntries();
                TestAssert.Throws(() => fixture.CreateBundle(required.Take(4).ToArray()), "Missing source was accepted.");
                TestAssert.Throws(() => fixture.CreateBundle(required.Concat(new[] { required[0] }).ToArray()),
                    "Extra source was accepted.");
                byte[] binary = WireMutation.ExtractBinary(GasSourceBundleCodec.EncodeSelector(fixture.CreateBundle()));
                WireMutation.WriteUInt32(binary, WireMutation.GetCountOffset(binary), 4);
                TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(WireMutation.WrapBinary(binary)),
                    "Wire count=4 was accepted.");
                WireMutation.WriteUInt32(binary, WireMutation.GetCountOffset(binary), 6);
                TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(WireMutation.WrapBinary(binary)),
                    "Wire count=6 was accepted.");
            }
        }

        /// <summary>
        /// 验证 target、hint、category 任一 byte 漂移都破坏 exact source routing contract。
        /// </summary>
        private static void RejectRoutingDrift()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                byte[] selector = GasSourceBundleCodec.EncodeSelector(fixture.CreateBundle());
                AssertBinaryMutationRejected(selector, WireMutation.GetFirstTargetBytesOffset, (bytes, offset) => bytes[offset] = (byte)'x');
                AssertBinaryMutationRejected(selector, WireMutation.GetFirstHintBytesOffset, (bytes, offset) => bytes[offset] = (byte)'x');
                AssertBinaryMutationRejected(selector, WireMutation.GetFirstCategoryOffset, (bytes, offset) => bytes[offset] = 1);
            }
        }

        /// <summary>
        /// 验证 bundle entry 不得占用 generator-owned marker hint。
        /// </summary>
        private static void RejectReservedHint()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                List<GasSourceEntry> entries = fixture.CreateRequiredEntries().ToList();
                entries[0] = new GasSourceEntry(
                    GasCodeGenContract.AutoChessAssembly,
                    GasCodeGenContract.MarkerHintName,
                    3,
                    D1CodecFixture.CreateSource("ReservedMarker"));
                TestAssert.Throws(() => fixture.CreateBundle(entries), "Reserved marker hint was accepted.");
            }
        }

        /// <summary>
        /// 验证 source 层拒绝 BOM、CR、NUL 和非法 UTF-8 bytes。
        /// </summary>
        private static void RejectNonCanonicalSources()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                AssertSourceRejected(fixture, new byte[] { 0xef, 0xbb, 0xbf, (byte)'x' });
                AssertSourceRejected(fixture, Encoding.UTF8.GetBytes("x\r\n"));
                AssertSourceRejected(fixture, new byte[] { (byte)'x', 0, (byte)'y' });
                AssertSourceRejected(fixture, new byte[] { 0xff });
            }
        }

        /// <summary>
        /// 验证四种禁止身份的 raw/hex representation 检查均真实执行。
        /// </summary>
        private static void RejectHashReferences()
        {
            byte[] digest = D1CodecFixture.Digest("forbidden-identity");
            byte[] source = Encoding.ASCII.GetBytes("prefix" + GasHashing.ToLowerHex(digest) + "suffix");
            TestAssert.Throws(() => GasSourceBundleValidator.RejectDigestReference(source, digest, "ArtifactManifestHash"),
                "Artifact manifest hash reference was accepted.");
            TestAssert.Throws(() => GasSourceBundleValidator.RejectDigestReference(source, digest, "SelectorSha256"),
                "Selector hash reference was accepted.");
            TestAssert.Throws(() => GasSourceBundleValidator.RejectDigestReference(source, digest, "SourceArtifactInventoryHash"),
                "Inventory hash reference was accepted.");
            TestAssert.Throws(() => GasSourceBundleValidator.RejectDigestReference(source, digest, "SourceSha256"),
                "Source self-hash reference was accepted.");
            byte[] mixedCase = Encoding.ASCII.GetBytes(ToMixedCaseHex(GasHashing.ToLowerHex(digest)));
            TestAssert.Throws(() => GasSourceBundleValidator.RejectDigestReference(mixedCase, digest, "SourceSha256"),
                "Mixed-case source hash reference was accepted.");
            AssertWriterRejectsManifestHashReference(digest);
        }

        /// <summary>
        /// 证明完整 ArtifactManifestHash 与 C# subset inventory 分字段保存且 roundtrip 后仍不混淆。
        /// </summary>
        private static void PreserveDistinctIdentities()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                GasSourceBundle bundle = fixture.CreateBundle();
                TestAssert.True(!GasSourceBundleCodec.BytesEqual(
                    bundle.ArtifactManifestHash, bundle.SourceArtifactInventoryHash),
                    "Fixture identities unexpectedly collided.");
                GasSourceBundle decoded = GasSourceBundleCodec.DecodeSelector(GasSourceBundleCodec.EncodeSelector(bundle));
                TestAssert.BytesEqual(bundle.ArtifactManifestHash, decoded.ArtifactManifestHash,
                    "Full manifest identity was replaced by source inventory.");
            }
        }

        /// <summary>
        /// 验证 declared inventory 与 entry source bytes 任一漂移均被对应重算门拒绝。
        /// </summary>
        private static void RejectInventoryAndSourceHashDrift()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                byte[] selector = GasSourceBundleCodec.EncodeSelector(fixture.CreateBundle());
                byte[] inventoryDrift = WireMutation.ExtractBinary(selector);
                inventoryDrift[WireMutation.GetInventoryHashOffset(inventoryDrift)] ^= 0x01;
                TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(WireMutation.WrapBinary(inventoryDrift)),
                    "Inventory hash drift was accepted.");
                byte[] sourceDrift = WireMutation.ExtractBinary(selector);
                sourceDrift[WireMutation.GetFirstSourceBytesOffset(sourceDrift)] ^= 0x01;
                TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(WireMutation.WrapBinary(sourceDrift)),
                    "Source bytes drift was accepted without SourceSha256 update.");
            }
        }

        /// <summary>
        /// 真实构造 exact-count duplicate、长度零/越界/缺 payload 与完整 entry 换序，验证 decoder 全部 fail closed。
        /// </summary>
        private static void RejectStructuralEntryDrift()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                byte[] binary = WireMutation.ExtractBinary(GasSourceBundleCodec.EncodeSelector(fixture.CreateBundle()));
                IReadOnlyList<WireEntrySpan> spans = WireMutation.GetEntrySpans(binary);
                AssertReorderedEntriesRejected(binary, spans, new[] { 0, 0, 2, 3, 4 },
                    "Exact-count duplicate target/hint tuple was accepted.");
                AssertSourceLengthRejected(binary, spans[0].SourceLengthOffset, 0,
                    "SourceByteLength=0 was accepted.");
                AssertSourceLengthRejected(binary, spans[0].SourceLengthOffset, 16777217,
                    "SourceByteLength above 16777216 was accepted.");
                AssertSourceLengthRejected(binary, spans[4].SourceLengthOffset, spans[4].SourceLength + 1,
                    "Declared source length without matching payload was accepted.");
                AssertReorderedEntriesRejected(binary, spans, new[] { 1, 0, 2, 3, 4 },
                    "Non-ordinal complete entry order was accepted.");
            }
        }

        /// <summary>
        /// 重组完整 entry bytes 并断言 strict decoder 拒绝 tuple 重复或 wire 乱序。
        /// </summary>
        private static void AssertReorderedEntriesRejected(
            byte[] binary,
            IReadOnlyList<WireEntrySpan> spans,
            IReadOnlyList<int> order,
            string message)
        {
            byte[] mutated = WireMutation.ReorderEntries(binary, spans, order);
            TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(WireMutation.WrapBinary(mutated)), message);
        }

        /// <summary>
        /// 仅覆写 SourceByteLength 字段并断言边界或 payload mismatch 被拒绝。
        /// </summary>
        private static void AssertSourceLengthRejected(
            byte[] binary,
            int sourceLengthOffset,
            uint sourceLength,
            string message)
        {
            byte[] mutated = (byte[])binary.Clone();
            WireMutation.WriteUInt32(mutated, sourceLengthOffset, sourceLength);
            TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(WireMutation.WrapBinary(mutated)), message);
        }

        /// <summary>
        /// 构造真实 writer 输入，证明 C# source 嵌入完整 manifest hex 时无法生成 selector。
        /// </summary>
        private static void AssertWriterRejectsManifestHashReference(byte[] manifestHash)
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                string forbidden = GasHashing.ToLowerHex(manifestHash);
                IReadOnlyList<GasSourceEntry> entries = fixture.CreateRequiredEntries(item =>
                    D1CodecFixture.CreateSource(item.HintName,
                        item.HintName == "AutoChessGeneratedConfig.gen.cs" ? forbidden : string.Empty));
                GasSourceBundle bundle = fixture.CreateBundle(entries, manifestHash);
                TestAssert.Throws(() => GasSourceBundleCodec.EncodeSelector(bundle),
                    "Writer accepted a source embedding ArtifactManifestHash.");
            }
        }

        /// <summary>
        /// 对一个定位后的 binary byte mutation 断言 strict decoder 拒绝。
        /// </summary>
        private static void AssertBinaryMutationRejected(
            byte[] selector,
            Func<byte[], int> locate,
            Action<byte[], int> mutate)
        {
            byte[] binary = WireMutation.ExtractBinary(selector);
            int offset = locate(binary);
            mutate(binary, offset);
            TestAssert.Throws(() => GasSourceBundleCodec.DecodeSelector(WireMutation.WrapBinary(binary)),
                "Routing mutation was accepted.");
        }

        /// <summary>
        /// 用指定非法 bytes 替换首项 source，断言 writer 在产生 selector 前失败。
        /// </summary>
        private static void AssertSourceRejected(D1CodecFixture fixture, byte[] invalidSource)
        {
            IReadOnlyList<GasSourceEntry> entries = fixture.CreateRequiredEntries(item =>
                item.HintName == "AutoChessGeneratedConfig.gen.cs"
                    ? invalidSource
                    : D1CodecFixture.CreateSource(item.HintName));
            TestAssert.Throws(() => GasSourceBundleCodec.EncodeSelector(fixture.CreateBundle(entries)),
                "Noncanonical source bytes were accepted.");
        }

        /// <summary>
        /// 在 selector 前添加 UTF-8 BOM。
        /// </summary>
        private static byte[] PrependBom(byte[] canonical)
        {
            return new byte[] { 0xef, 0xbb, 0xbf }.Concat(canonical).ToArray();
        }

        /// <summary>
        /// 将所有 LF 改为 CRLF，制造非 canonical selector。
        /// </summary>
        private static byte[] ReplaceLfWithCrLf(byte[] canonical)
        {
            return Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(canonical).Replace("\n", "\r\n"));
        }

        /// <summary>
        /// 在 Base64 行插入单个空格。
        /// </summary>
        private static byte[] InsertBase64Whitespace(byte[] canonical)
        {
            string text = Encoding.ASCII.GetString(canonical);
            int insertAt = GasCodeGenContract.SelectorMagic.Length + 4;
            return Encoding.ASCII.GetBytes(text.Insert(insertAt, " "));
        }

        /// <summary>
        /// 将 lowercase digest 交替改为大写，构造 mixed-case hex 负例。
        /// </summary>
        private static string ToMixedCaseHex(string lowercaseHex)
        {
            char[] characters = lowercaseHex.ToCharArray();
            for (int index = 0; index < characters.Length; index += 2)
            {
                characters[index] = char.ToUpperInvariant(characters[index]);
            }

            return new string(characters);
        }
    }
}
