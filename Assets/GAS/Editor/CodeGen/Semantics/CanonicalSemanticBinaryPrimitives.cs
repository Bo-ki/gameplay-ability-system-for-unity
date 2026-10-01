using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 提供版本化 domain + canonical bytes 的完整 SHA-256，所有 hash 均输出小写十六进制。
    /// </summary>
    public static class CanonicalSemanticHash
    {
        /// <summary>
        /// 对 UTF-8 byte-length domain 与 payload 计算无歧义 SHA-256。
        /// </summary>
        public static string Compute(string domain, byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Hash domain is required.", nameof(domain));
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            CanonicalSemanticResourceBudget.RequireString(domain, "hash domain");
            CanonicalSemanticResourceBudget.RequireWireLength(payload.Length, "hash payload");

            var writer = new CanonicalBinaryWriter();
            writer.WriteString(domain);
            writer.WriteBytes(payload);
            using (var sha = SHA256.Create())
                return ToLowerHex(sha.ComputeHash(writer.ToArray()));
        }

        /// <summary>
        /// 将 byte 数组转换为固定小写十六进制文本。
        /// </summary>
        private static string ToLowerHex(byte[] bytes)
        {
            var result = new StringBuilder(bytes.Length * 2);
            for (var index = 0; index < bytes.Length; index++)
                result.Append(bytes[index].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }

    /// <summary>
    /// 以显式 little-endian、严格 UTF-8 和固定宽度基础类型写 canonical bytes。
    /// </summary>
    internal sealed class CanonicalBinaryWriter
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly List<byte> _bytes;
        private readonly CanonicalSemanticBudgetMeter _meter = new CanonicalSemanticBudgetMeter();
        private readonly int _maxWireBytes;
        private int _length;

        /// <summary>
        /// 创建受全局 wire 上界约束的实体 writer。
        /// </summary>
        public CanonicalBinaryWriter()
            : this(CanonicalSemanticResourceBudget.MaxWireBytes, false)
        {
        }

        /// <summary>
        /// 创建受调用方局部上界约束的实体 writer。
        /// </summary>
        public CanonicalBinaryWriter(int maxWireBytes)
            : this(maxWireBytes, false)
        {
        }

        /// <summary>
        /// 创建只计数、不分配聚合 payload 的 writer。
        /// </summary>
        private CanonicalBinaryWriter(int maxWireBytes, bool countOnly)
        {
            if (maxWireBytes < 0 || maxWireBytes > CanonicalSemanticResourceBudget.MaxWireBytes)
                throw new ArgumentOutOfRangeException(nameof(maxWireBytes));
            _maxWireBytes = maxWireBytes;
            _bytes = countOnly ? null : new List<byte>();
        }

        /// <summary>
        /// 创建在指定局部上界内执行协议精确计数的 writer。
        /// </summary>
        public static CanonicalBinaryWriter CreateCounting(int maxWireBytes)
        {
            return new CanonicalBinaryWriter(maxWireBytes, true);
        }

        /// <summary>
        /// 返回已写入或已计量的协议 byte 数。
        /// </summary>
        public int Length => _length;

        /// <summary>
        /// 返回当前缓冲区的独立 byte 副本。
        /// </summary>
        public byte[] ToArray()
        {
            if (_bytes == null)
                throw new InvalidOperationException("A counting canonical writer has no materialized payload.");
            EnsureWireCapacity(0);
            return _bytes.ToArray();
        }

        /// <summary>
        /// 充值一个 semantic/program/schema node，必须在写节点字段前调用。
        /// </summary>
        public void AccountNode(int depth = 0)
        {
            _meter.ChargeNode(depth);
        }

        /// <summary>
        /// 充值一条 program/dependency/schema edge，必须在写边字段前调用。
        /// </summary>
        public void AccountEdge()
        {
            _meter.ChargeEdge("canonical writer edge");
        }

        /// <summary>
        /// 写入一个原始 byte。
        /// </summary>
        public void WriteByte(byte value)
        {
            EnsureWireCapacity(1);
            if (_bytes != null)
                _bytes.Add(value);
            _length++;
        }

        /// <summary>
        /// 写入只允许 0/1 的规范布尔值。
        /// </summary>
        public void WriteBoolean(bool value)
        {
            WriteByte(value ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// 按 little-endian 写入 UInt16。
        /// </summary>
        public void WriteUInt16(ushort value)
        {
            EnsureWireCapacity(2);
            if (_bytes != null)
            {
                _bytes.Add((byte)value);
                _bytes.Add((byte)(value >> 8));
            }
            _length += 2;
        }

        /// <summary>
        /// 按 little-endian 写入 Int16 原始位。
        /// </summary>
        public void WriteInt16(short value)
        {
            WriteUInt16(unchecked((ushort)value));
        }

        /// <summary>
        /// 按 little-endian 写入 UInt32。
        /// </summary>
        public void WriteUInt32(uint value)
        {
            EnsureWireCapacity(4);
            if (_bytes != null)
            {
                _bytes.Add((byte)value);
                _bytes.Add((byte)(value >> 8));
                _bytes.Add((byte)(value >> 16));
                _bytes.Add((byte)(value >> 24));
            }
            _length += 4;
        }

        /// <summary>
        /// 按 little-endian 写入 Int32 原始位。
        /// </summary>
        public void WriteInt32(int value)
        {
            WriteUInt32(unchecked((uint)value));
        }

        /// <summary>
        /// 按 little-endian 写入 UInt64。
        /// </summary>
        public void WriteUInt64(ulong value)
        {
            WriteUInt32((uint)value);
            WriteUInt32((uint)(value >> 32));
        }

        /// <summary>
        /// 按 little-endian 写入 Int64 原始位。
        /// </summary>
        public void WriteInt64(long value)
        {
            WriteUInt64(unchecked((ulong)value));
        }

        /// <summary>
        /// 写入 UInt32 count，拒绝负数和超出协议上限的集合。
        /// </summary>
        public void WriteCount(int count)
        {
            if (count < 0 || count > CanonicalSemanticResourceBudget.MaxSingleCollectionCount)
                throw new CanonicalSemanticValidationException(
                    CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                    "Canonical collection count is out of range.");
            WriteUInt32((uint)count);
        }

        /// <summary>
        /// 以 UInt32 UTF-8 byte count 加原始 UTF-8 bytes 写入字符串。
        /// </summary>
        public void WriteString(string value)
        {
            if (value == null)
                throw new CanonicalSemanticValidationException(
                    CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                    "Canonical string cannot be null.");
            int encodedLength;
            try
            {
                _meter.ChargeString(value, "canonical writer string");
                encodedLength = StrictUtf8.GetByteCount(value);
            }
            catch (EncoderFallbackException exception)
            {
                throw new CanonicalSemanticValidationException(
                    CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                    "Canonical string contains invalid UTF-16: " + exception.Message);
            }
            WriteUInt32((uint)encodedLength);
            if (_bytes == null)
            {
                Advance(encodedLength);
                return;
            }
            WriteRaw(StrictUtf8.GetBytes(value));
        }

        /// <summary>
        /// 以 UInt32 byte count 加原始 payload 写入 bytes。
        /// </summary>
        public void WriteBytes(byte[] value)
        {
            if (value == null)
                throw new CanonicalSemanticValidationException(
                    CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                    "Canonical bytes cannot be null.");
            _meter.ChargePayload(value.Length, "canonical writer payload");
            WriteUInt32((uint)value.Length);
            WriteRaw(value);
        }

        /// <summary>
        /// 直接追加固定协议 magic 或已带长度的 bytes。
        /// </summary>
        public void WriteRaw(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            Advance(value.Length);
            if (_bytes != null)
                _bytes.AddRange(value);
        }

        /// <summary>
        /// 在任何实体扩容前推进长度，counting writer 仅执行该路径。
        /// </summary>
        private void Advance(int additionalBytes)
        {
            EnsureWireCapacity(additionalBytes);
            _length += additionalBytes;
        }

        /// <summary>
        /// 在 List 扩容/AddRange 前验证总 wire bytes 上界。
        /// </summary>
        private void EnsureWireCapacity(int additionalBytes)
        {
            if (additionalBytes < 0
                || _length > _maxWireBytes - additionalBytes)
            {
                throw new CanonicalSemanticValidationException(
                    CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                    CanonicalSemanticVersions.ResourceBudgetId + ": canonical writer wire bytes exceeded.");
            }
        }
    }

    /// <summary>
    /// 从只读 byte 快照按显式 little-endian 与严格 UTF-8 读取 canonical 数据并拒绝尾随/截断输入。
    /// </summary>
    internal sealed class CanonicalBinaryReader
    {
        public const int MaxCollectionCount = CanonicalSemanticResourceBudget.MaxSingleCollectionCount;
        public const int MaxByteLength = CanonicalSemanticResourceBudget.MaxWireBytes;
        public const int MaxValueDepth = CanonicalSemanticResourceBudget.MaxDepth;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly byte[] _bytes;
        private readonly CanonicalSemanticBudgetMeter _meter = new CanonicalSemanticBudgetMeter();
        private int _offset;

        /// <summary>
        /// 复制输入 bytes，防止调用方在 decode 期间修改协议内容。
        /// </summary>
        public CanonicalBinaryReader(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length > MaxByteLength)
                Fail("Canonical payload exceeds the codec limit.");
            _bytes = (byte[])bytes.Clone();
        }

        /// <summary>
        /// 读取一个 byte，并在输入截断时 fail closed。
        /// </summary>
        public byte ReadByte()
        {
            RequireAvailable(1);
            return _bytes[_offset++];
        }

        /// <summary>
        /// 读取只允许 0/1 的规范布尔值。
        /// </summary>
        public bool ReadBoolean()
        {
            var value = ReadByte();
            if (value > 1)
                Fail("Canonical boolean byte must be zero or one.");
            return value == 1;
        }

        /// <summary>
        /// 按 little-endian 读取 UInt16。
        /// </summary>
        public ushort ReadUInt16()
        {
            RequireAvailable(2);
            var value = (ushort)(_bytes[_offset] | (_bytes[_offset + 1] << 8));
            _offset += 2;
            return value;
        }

        /// <summary>
        /// 按 little-endian 读取 Int16 原始位。
        /// </summary>
        public short ReadInt16()
        {
            return unchecked((short)ReadUInt16());
        }

        /// <summary>
        /// 按 little-endian 读取 UInt32。
        /// </summary>
        public uint ReadUInt32()
        {
            RequireAvailable(4);
            var value = (uint)(_bytes[_offset]
                               | (_bytes[_offset + 1] << 8)
                               | (_bytes[_offset + 2] << 16)
                               | (_bytes[_offset + 3] << 24));
            _offset += 4;
            return value;
        }

        /// <summary>
        /// 按 little-endian 读取 Int32 原始位。
        /// </summary>
        public int ReadInt32()
        {
            return unchecked((int)ReadUInt32());
        }

        /// <summary>
        /// 按 little-endian 读取 UInt64。
        /// </summary>
        public ulong ReadUInt64()
        {
            var low = ReadUInt32();
            var high = ReadUInt32();
            return low | ((ulong)high << 32);
        }

        /// <summary>
        /// 按 little-endian 读取 Int64 原始位。
        /// </summary>
        public long ReadInt64()
        {
            return unchecked((long)ReadUInt64());
        }

        /// <summary>
        /// 读取受协议上限保护的 UInt32 collection count。
        /// </summary>
        public int ReadCount()
        {
            var value = ReadUInt32();
            if (value > MaxCollectionCount)
                Fail("Canonical collection count exceeds the codec limit.");
            _meter.ChargeCollection((int)value, "canonical reader collection");
            return (int)value;
        }

        /// <summary>
        /// 读取 UInt32 byte count 与严格 UTF-8 字符串。
        /// </summary>
        public string ReadString()
        {
            var length = ReadLength(
                CanonicalSemanticResourceBudget.MaxSingleStringBytes,
                "Canonical string byte length exceeds the codec limit.");
            _meter.ChargeStringBytes(length, "canonical reader string");
            try
            {
                StrictUtf8.GetCharCount(_bytes, _offset, length);
                var result = StrictUtf8.GetString(_bytes, _offset, length);
                _offset += length;
                return result;
            }
            catch (DecoderFallbackException exception)
            {
                Fail("Canonical string contains invalid UTF-8: " + exception.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// 读取 UInt32 byte count 与原始 payload。
        /// </summary>
        public byte[] ReadBytes()
        {
            var length = ReadLength(
                CanonicalSemanticResourceBudget.MaxSinglePayloadBytes,
                "Canonical byte length exceeds the codec limit.");
            _meter.ChargePayload(length, "canonical reader payload");
            var result = new byte[length];
            Buffer.BlockCopy(_bytes, _offset, result, 0, result.Length);
            _offset += result.Length;
            return result;
        }

        /// <summary>
        /// 充值一个已读 semantic/program/schema node。
        /// </summary>
        public void AccountNode(int depth = 0)
        {
            _meter.ChargeNode(depth);
        }

        /// <summary>
        /// 充值一条已读 program/dependency/schema edge。
        /// </summary>
        public void AccountEdge()
        {
            _meter.ChargeEdge("canonical reader edge");
        }

        /// <summary>
        /// 在 string/blob 分配前读取并校验 length 与剩余 bytes。
        /// </summary>
        private int ReadLength(int maximum, string message)
        {
            var length = ReadUInt32();
            if (length > maximum)
                Fail(message);
            RequireAvailable((int)length);
            return (int)length;
        }

        /// <summary>
        /// 验证并消费固定 magic bytes。
        /// </summary>
        public void ExpectRaw(byte[] expected)
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));
            RequireAvailable(expected.Length);
            for (var index = 0; index < expected.Length; index++)
            {
                if (_bytes[_offset + index] != expected[index])
                    Fail("Canonical payload magic mismatch.");
            }

            _offset += expected.Length;
        }

        /// <summary>
        /// 确保 payload 已精确消费，拒绝尾随 bytes 与拼接攻击。
        /// </summary>
        public void EnsureComplete()
        {
            if (_offset != _bytes.Length)
                Fail("Canonical payload contains trailing bytes.");
        }

        /// <summary>
        /// 验证剩余输入足以完成本次读取。
        /// </summary>
        private void RequireAvailable(int count)
        {
            if (count < 0 || _offset > _bytes.Length - count)
                Fail("Canonical payload is truncated.");
        }

        /// <summary>
        /// 抛出稳定 CFG1501 codec 异常。
        /// </summary>
        private static void Fail(string message)
        {
            throw new CanonicalSemanticValidationException(
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                message);
        }
    }
}
