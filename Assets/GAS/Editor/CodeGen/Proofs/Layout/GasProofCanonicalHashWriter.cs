using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GAS.Runtime;

namespace GAS.Editor.CodeGen.Proofs
{
    /// <summary>
    /// 以显式 little-endian 与 UTF-8 length-prefix 编码 proof，避免 JSON、locale 与运行环境影响身份。
    /// </summary>
    internal sealed class GasProofCanonicalHashWriter : IDisposable
    {
        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(false, true);
        private readonly MemoryStream _stream = new MemoryStream();
        private bool _encodingBudgetExceeded;
        private long _relatedDefinitionCount;

        /// <summary>
        /// 写入一个原始 byte。
        /// </summary>
        public void WriteByte(byte value)
        {
            if (!TryReserveBytes(1))
                return;
            _stream.WriteByte(value);
        }

        /// <summary>
        /// 以 little-endian 写入一个 32 位有符号整数。
        /// </summary>
        public void WriteInt32(int value)
        {
            WriteUInt32(unchecked((uint)value));
        }

        /// <summary>
        /// 以 little-endian 写入一个 32 位无符号整数。
        /// </summary>
        public void WriteUInt32(uint value)
        {
            if (!TryReserveBytes(sizeof(uint)))
                return;
            WriteUInt32Unchecked(value);
        }

        /// <summary>
        /// 在调用方已预留四字节后写入 little-endian uint。
        /// </summary>
        private void WriteUInt32Unchecked(uint value)
        {
            _stream.WriteByte((byte)value);
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)(value >> 16));
            _stream.WriteByte((byte)(value >> 24));
        }

        /// <summary>
        /// 以 little-endian 写入一个 64 位有符号整数。
        /// </summary>
        public void WriteInt64(long value)
        {
            WriteUInt64(unchecked((ulong)value));
        }

        /// <summary>
        /// 以 little-endian 写入一个 64 位无符号整数。
        /// </summary>
        public void WriteUInt64(ulong value)
        {
            if (!TryReserveBytes(sizeof(ulong)))
                return;
            for (var shift = 0; shift < 64; shift += 8)
                _stream.WriteByte((byte)(value >> shift));
        }

        /// <summary>
        /// 写入 UTF-8 byte length 与内容；null 与空串统一为零长度。
        /// </summary>
        public void WriteString(string value)
        {
            if (_encodingBudgetExceeded)
                return;
            var normalized = value ?? string.Empty;
            if (!TryGetCanonicalUtf8ByteCount(normalized, out var byteCount))
            {
                _encodingBudgetExceeded = true;
                return;
            }
            if (!TryReserveBytes(sizeof(int) + (long)byteCount))
                return;
            try
            {
                var bytes = StrictUtf8.GetBytes(normalized);
                WriteUInt32Unchecked((uint)bytes.Length);
                _stream.Write(bytes, 0, bytes.Length);
            }
            catch (EncoderFallbackException)
            {
                _encodingBudgetExceeded = true;
            }
        }

        /// <summary>
        /// 写入一条完整且路径稳定的 authoring provenance。
        /// </summary>
        public void WriteProvenance(in GasProofProvenance provenance)
        {
            if (_encodingBudgetExceeded)
                return;
            var related = provenance.RelatedDefinitionKeys;
            if (related != null && !TryRegisterRelatedDefinitions(related.Length))
                return;
            WriteInt32(provenance.ProvenanceOrdinal);
            WriteString(provenance.WorkbookId);
            WriteString(provenance.TableId);
            WriteString(provenance.RowStableId);
            WriteString(provenance.FieldPath);
            WriteString(provenance.RawValue);
            WriteString(provenance.NormalizedValue);
            WriteString(provenance.RuleId);
            WriteString(provenance.RuleVersion);
            WriteString(provenance.GeneratorVersion);
            WriteInt32(related == null ? -1 : related.Length);
            if (related == null)
                return;
            for (var index = 0; index < related.Length; index++)
                WriteString(related[index]);
        }

        /// <summary>
        /// 写入一条完整失败记录，使相同 Red 输入产生相同 ProofHash。
        /// </summary>
        public void WriteFailure(in GasProofFailure failure)
        {
            WriteByte((byte)failure.ProofKind);
            WriteInt32((int)failure.FailureKind);
            WriteString(failure.RuleId);
            WriteString(failure.DimensionId);
            WriteString(failure.DerivationId);
            WriteString(failure.Operator);
            WriteString(failure.LeftOperandName);
            WriteInt64(failure.LeftOperandValue);
            WriteString(failure.RightOperandName);
            WriteInt64(failure.RightOperandValue);
            WriteInt64(failure.ExpectedMaximum);
            WriteInt64(failure.ActualValue);
            WriteString(failure.Unit);
            WriteInt32(failure.DefinitionId);
            WriteString(failure.DefinitionKey);
            WriteInt32(failure.ProfileId);
            WriteInt32(failure.CanonicalOrdinal);
            WriteString(failure.FirstConsumer);
            WriteString(failure.ExpectedText);
            WriteString(failure.ActualText);
            WriteProvenance(in failure.Provenance);
        }

        /// <summary>
        /// 计算当前 canonical bytes 的小写 SHA-256 十六进制摘要。
        /// </summary>
        public string ComputeHash()
        {
            if (_encodingBudgetExceeded)
                return string.Empty;
            using (var sha256 = SHA256.Create())
            {
                _stream.Position = 0;
                var hash = sha256.ComputeHash(_stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (var index = 0; index < hash.Length; index++)
                    builder.Append(hash[index].ToString("x2"));
                return builder.ToString();
            }
        }

        /// <summary>
        /// 释放 canonical byte buffer。
        /// </summary>
        public void Dispose()
        {
            _stream.Dispose();
        }

        /// <summary>
        /// 在任何 canonical buffer 扩容前执行 checked 总字节硬门。
        /// </summary>
        private bool TryReserveBytes(long byteCount)
        {
            if (_encodingBudgetExceeded || byteCount < 0 ||
                !GasCheckedProofMath.TryAdd(_stream.Length, byteCount, out var nextLength) ||
                nextLength > GasRuntimeV1ProofInventory.MaximumProofBuildBytes)
            {
                _encodingBudgetExceeded = true;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 在遍历 provenance 嵌套数组前校验单项 count 与全 writer 累计条目预算。
        /// </summary>
        private bool TryRegisterRelatedDefinitions(int count)
        {
            if (_encodingBudgetExceeded ||
                count > GasRuntimeV1ProofInventory.MaximumProofRelatedDefinitionCount ||
                !GasCheckedProofMath.TryAdd(_relatedDefinitionCount, count,
                    out var nextCount) ||
                nextCount > GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount)
            {
                _encodingBudgetExceeded = true;
                return false;
            }
            _relatedDefinitionCount = nextCount;
            return true;
        }

        /// <summary>
        /// 判断字符串是否为不带前缀的 64 位十六进制 SHA-256 文本。
        /// </summary>
        public static bool IsSha256Hex(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
                return false;

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f') ||
                      (character >= 'A' && character <= 'F')))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 将有效 SHA-256 文本规范化为小写，无效输入保持空串用于 Red payload。
        /// </summary>
        public static string NormalizeSha256(string value)
        {
            return IsSha256Hex(value) ? value.ToLowerInvariant() : string.Empty;
        }

        /// <summary>
        /// 在 UTF-8 分配前判断单个 canonical 字符串是否处于版本化字节硬门内。
        /// </summary>
        public static bool IsWithinStringBudget(string value)
        {
            return TryGetCanonicalUtf8ByteCount(value, out _);
        }

        /// <summary>
        /// 以 strict UTF-8 计算 canonical 字节数，非法 UTF-16 与超门输入均不抛出。
        /// </summary>
        internal static bool TryGetCanonicalUtf8ByteCount(
            string value,
            out int byteCount)
        {
            byteCount = 0;
            var normalized = value ?? string.Empty;
            if (normalized.Length >
                GasRuntimeV1ProofInventory.MaximumProofCanonicalStringBytes)
                return false;
            try
            {
                byteCount = StrictUtf8.GetByteCount(normalized);
                return byteCount <=
                       GasRuntimeV1ProofInventory.MaximumProofCanonicalStringBytes;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 累计 proof 中真实 strict UTF-8 与 related-key 工作量，供 source/build/verifier 共用同一硬门。
    /// </summary>
    internal struct GasProofEncodingBudget
    {
        private long _byteCount;
        private long _relatedDefinitionCount;
        private long _failureActual;
        private bool _failedOnRelatedDefinitions;

        public long ByteCount => _byteCount;
        public long RelatedDefinitionCount => _relatedDefinitionCount;
        public long FailureActual => _failureActual;
        public bool FailedOnRelatedDefinitions => _failedOnRelatedDefinitions;

        /// <summary>
        /// 累加一个含 length prefix 的真实 strict UTF-8 字符串，失败后不再扫描后续文本。
        /// </summary>
        public bool TryAddString(string value)
        {
            if (_failureActual > 0)
                return false;
            if (!GasProofCanonicalHashWriter.TryGetCanonicalUtf8ByteCount(
                    value, out var byteCount))
                return FailBytes(GasRuntimeV1ProofInventory.MaximumProofBuildBytes + 1);
            if (!GasCheckedProofMath.TryAdd(byteCount, sizeof(int), out var encodedBytes))
                return FailBytes(long.MaxValue);
            if (!GasCheckedProofMath.TryAdd(_byteCount, encodedBytes, out var nextBytes))
                return FailBytes(long.MaxValue);
            if (nextBytes > GasRuntimeV1ProofInventory.MaximumProofBuildBytes)
                return FailBytes(nextBytes);
            _byteCount = nextBytes;
            return true;
        }

        /// <summary>
        /// 累加完整 provenance 的真实字符串与 related-key count/bytes，任一超门即停止。
        /// </summary>
        public bool TryAddProvenance(in GasProofProvenance provenance)
        {
            if (_failureActual > 0)
                return false;
            var related = provenance.RelatedDefinitionKeys;
            if (related != null && !TryAddRelatedDefinitions(related.Length))
                return false;
            if (!TryAddString(provenance.WorkbookId) ||
                !TryAddString(provenance.TableId) ||
                !TryAddString(provenance.RowStableId) ||
                !TryAddString(provenance.FieldPath) ||
                !TryAddString(provenance.RawValue) ||
                !TryAddString(provenance.NormalizedValue) ||
                !TryAddString(provenance.RuleId) ||
                !TryAddString(provenance.RuleVersion) ||
                !TryAddString(provenance.GeneratorVersion))
                return false;
            if (related == null)
                return true;
            for (var index = 0; index < related.Length; index++)
            {
                if (!TryAddString(related[index]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 累加一条 failure 中所有 canonical 文本与 provenance。
        /// </summary>
        public bool TryAddFailure(in GasProofFailure failure)
        {
            return TryAddString(failure.RuleId) &&
                   TryAddString(failure.DimensionId) &&
                   TryAddString(failure.DerivationId) &&
                   TryAddString(failure.Operator) &&
                   TryAddString(failure.LeftOperandName) &&
                   TryAddString(failure.RightOperandName) &&
                   TryAddString(failure.Unit) &&
                   TryAddString(failure.DefinitionKey) &&
                   TryAddString(failure.FirstConsumer) &&
                   TryAddString(failure.ExpectedText) &&
                   TryAddString(failure.ActualText) &&
                   TryAddProvenance(in failure.Provenance);
        }

        /// <summary>
        /// 累加 related-key 数量，单 provenance 与全 payload 任一超门均 fail-closed。
        /// </summary>
        private bool TryAddRelatedDefinitions(int count)
        {
            if (count > GasRuntimeV1ProofInventory.MaximumProofRelatedDefinitionCount)
            {
                _failedOnRelatedDefinitions = true;
                _failureActual = count;
                return false;
            }
            if (!GasCheckedProofMath.TryAdd(_relatedDefinitionCount, count,
                    out var nextCount))
            {
                _failedOnRelatedDefinitions = true;
                _failureActual = long.MaxValue;
                return false;
            }
            if (nextCount > GasRuntimeV1ProofInventory.MaximumProofBuildEntryCount)
            {
                _failedOnRelatedDefinitions = true;
                _failureActual = nextCount;
                return false;
            }
            _relatedDefinitionCount = nextCount;
            return true;
        }

        /// <summary>
        /// 固定 byte-budget 失败状态，使后续调用首行返回且不再扫描字符串。
        /// </summary>
        private bool FailBytes(long actual)
        {
            _failureActual = actual;
            return false;
        }
    }

    /// <summary>
    /// 提供 proof provenance 与失败顺序的 canonical 校验，供 Layout/Capacity builder 共用。
    /// </summary>
    internal static class GasProofCanonicalRules
    {
        /// <summary>
        /// 验证 provenance 的身份字段与 opaque DefinitionKey 严格递增顺序。
        /// </summary>
        public static bool IsCanonicalProvenance(in GasProofProvenance provenance)
        {
            if (!provenance.IsComplete)
                return false;

            var related = provenance.RelatedDefinitionKeys;
            if (related.Length >
                GasRuntimeV1ProofInventory.MaximumProofRelatedDefinitionCount)
                return false;
            if (!GasProofCanonicalHashWriter.IsWithinStringBudget(provenance.WorkbookId) ||
                !GasProofCanonicalHashWriter.IsWithinStringBudget(provenance.TableId) ||
                !GasProofCanonicalHashWriter.IsWithinStringBudget(provenance.RowStableId) ||
                !GasProofCanonicalHashWriter.IsWithinStringBudget(provenance.FieldPath) ||
                !GasProofCanonicalHashWriter.IsWithinStringBudget(provenance.RawValue) ||
                !GasProofCanonicalHashWriter.IsWithinStringBudget(provenance.NormalizedValue) ||
                !GasProofCanonicalHashWriter.IsWithinStringBudget(provenance.RuleId) ||
                !GasProofCanonicalHashWriter.IsWithinStringBudget(provenance.RuleVersion) ||
                !GasProofCanonicalHashWriter.IsWithinStringBudget(provenance.GeneratorVersion))
                return false;
            for (var index = 1; index < related.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(related[index]) ||
                    !GasProofCanonicalHashWriter.IsWithinStringBudget(related[index]) ||
                    string.CompareOrdinal(related[index], related[index - 1]) <= 0)
                    return false;
            }
            return related.Length == 0 ||
                   (!string.IsNullOrWhiteSpace(related[0]) &&
                    GasProofCanonicalHashWriter.IsWithinStringBudget(related[0]));
        }

        /// <summary>
        /// 比较两条失败的稳定身份字段，屏蔽容器遍历顺序。
        /// </summary>
        public static int CompareFailures(GasProofFailure left, GasProofFailure right)
        {
            var comparison = string.CompareOrdinal(left.RuleId, right.RuleId);
            if (comparison != 0)
                return comparison;
            comparison = left.DefinitionId.CompareTo(right.DefinitionId);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.DimensionId, right.DimensionId);
            if (comparison != 0)
                return comparison;
            comparison = left.CanonicalOrdinal.CompareTo(right.CanonicalOrdinal);
            if (comparison != 0)
                return comparison;
            comparison = left.FailureKind.CompareTo(right.FailureKind);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.DerivationId, right.DerivationId);
            return comparison != 0 ? comparison : CompareFailurePayload(in left, in right);
        }

        /// <summary>
        /// 比较失败的算术、consumer 与 provenance 尾部，建立跨运行时稳定的完整全序。
        /// </summary>
        private static int CompareFailurePayload(
            in GasProofFailure left,
            in GasProofFailure right)
        {
            var comparison = string.CompareOrdinal(left.Operator, right.Operator);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.LeftOperandName, right.LeftOperandName);
            if (comparison != 0)
                return comparison;
            comparison = left.LeftOperandValue.CompareTo(right.LeftOperandValue);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.RightOperandName, right.RightOperandName);
            if (comparison != 0)
                return comparison;
            comparison = left.RightOperandValue.CompareTo(right.RightOperandValue);
            if (comparison != 0)
                return comparison;
            comparison = left.ExpectedMaximum.CompareTo(right.ExpectedMaximum);
            if (comparison != 0)
                return comparison;
            comparison = left.ActualValue.CompareTo(right.ActualValue);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.Unit, right.Unit);
            if (comparison != 0)
                return comparison;
            comparison = left.ProfileId.CompareTo(right.ProfileId);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.DefinitionKey, right.DefinitionKey);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.FirstConsumer, right.FirstConsumer);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.ExpectedText, right.ExpectedText);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.ActualText, right.ActualText);
            return comparison != 0 ? comparison :
                CompareProvenance(in left.Provenance, in right.Provenance);
        }

        /// <summary>
        /// 按 proof hash 编码顺序比较 provenance，并以 opaque DefinitionKey 完成全序。
        /// </summary>
        private static int CompareProvenance(
            in GasProofProvenance left,
            in GasProofProvenance right)
        {
            var comparison = left.ProvenanceOrdinal.CompareTo(right.ProvenanceOrdinal);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.WorkbookId, right.WorkbookId);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.TableId, right.TableId);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.RowStableId, right.RowStableId);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.FieldPath, right.FieldPath);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.RawValue, right.RawValue);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.NormalizedValue, right.NormalizedValue);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.RuleId, right.RuleId);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.RuleVersion, right.RuleVersion);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.GeneratorVersion, right.GeneratorVersion);
            if (comparison != 0)
                return comparison;
            var leftRelated = left.RelatedDefinitionKeys;
            var rightRelated = right.RelatedDefinitionKeys;
            if (leftRelated == null || rightRelated == null)
            {
                if (leftRelated == null && rightRelated == null)
                    return 0;
                return leftRelated == null ? -1 : 1;
            }
            comparison = leftRelated.Length.CompareTo(rightRelated.Length);
            if (comparison != 0)
                return comparison;
            for (var index = 0; index < leftRelated.Length; index++)
            {
                comparison = string.CompareOrdinal(leftRelated[index], rightRelated[index]);
                if (comparison != 0)
                    return comparison;
            }
            return 0;
        }
    }
}
