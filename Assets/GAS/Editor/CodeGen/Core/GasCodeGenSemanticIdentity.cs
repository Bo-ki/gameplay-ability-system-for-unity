using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace GAS.Editor
{
    /// <summary>
    /// 从当前 typed row 快照生成可复现的三元加密身份，为外置 package descriptor 提供过渡期控制面证据。
    /// </summary>
    public sealed class GasCodeGenSemanticIdentity
    {
        public const string Algorithm = "TypedRowSnapshotSemanticIdentity-v1";
        private const string ContractAbiVersion = "RuntimeV1-ContractAbi-v1";
        private const string CapacityProofSchemaVersion = "RuntimeV1-CapacityProofSchema-v1-pending";

        private GasCodeGenSemanticIdentity(string schemaHash, string contentHash, string layoutHash)
        {
            SchemaHash = schemaHash;
            ContentHash = contentHash;
            LayoutHash = layoutHash;
        }

        public string SchemaHash { get; }

        public string ContentHash { get; }

        public string LayoutHash { get; }

        /// <summary>
        /// 对同一份已归一化 row 快照分别计算 schema、content 与 layout 身份。
        /// </summary>
        public static GasCodeGenSemanticIdentity Create(IReadOnlyList<RowMetadata> rows)
        {
            var canonicalRows = new List<RowMetadata>(rows ?? Array.Empty<RowMetadata>());
            canonicalRows.Sort(CompareRows);
            if (canonicalRows.Count == 0)
                throw new InvalidOperationException("Semantic identity requires at least one typed row schema.");

            return new GasCodeGenSemanticIdentity(
                ComputeSchemaHash(canonicalRows),
                ComputeContentHash(canonicalRows),
                ComputeLayoutHash(canonicalRows));
        }

        /// <summary>
        /// 将 row schema、字段序号与当前 typed contract ABI 纳入 SchemaHash。
        /// </summary>
        private static string ComputeSchemaHash(IReadOnlyList<RowMetadata> rows)
        {
            var builder = CreateDomainBuilder("EX-GAS-SchemaHash-v1");
            AppendField(builder, ContractAbiVersion);
            for (var index = 0; index < rows.Count; index++)
                AppendRowSchema(builder, rows[index]);
            return ComputeSha256(builder);
        }

        /// <summary>
        /// 将稳定排序后的 gameplay row 值纳入 ContentHash，不包含路径和生成环境信息。
        /// </summary>
        private static string ComputeContentHash(IReadOnlyList<RowMetadata> rows)
        {
            var builder = CreateDomainBuilder("EX-GAS-ContentHash-v1");
            for (var index = 0; index < rows.Count; index++)
            {
                AppendField(builder, GetRowTypeName(rows[index]));
                AppendRowValues(builder, rows[index]);
            }

            return ComputeSha256(builder);
        }

        /// <summary>
        /// 将 Blob 布局、Attribute/Tag 稠密布局输入和 proof schema 纳入 LayoutHash。
        /// </summary>
        private static string ComputeLayoutHash(IReadOnlyList<RowMetadata> rows)
        {
            var builder = CreateDomainBuilder("EX-GAS-LayoutHash-v1");
            AppendField(builder, CapacityProofSchemaVersion);
            for (var index = 0; index < rows.Count; index++)
            {
                AppendLayoutSchema(builder, rows[index]);
                if (IsLayoutAuthorityRow(rows[index].DefinitionKind))
                    AppendRowValues(builder, rows[index]);
            }

            return ComputeSha256(builder);
        }

        /// <summary>
        /// 写入一个 row 的类型、领域、字段序号与生成 ABI 元数据。
        /// </summary>
        private static void AppendRowSchema(StringBuilder builder, RowMetadata row)
        {
            AppendField(builder, GetRowTypeName(row));
            AppendField(builder, row.DomainName);
            AppendField(builder, row.CodeFieldName);
            AppendField(builder, ((byte)row.DefinitionKind).ToString(CultureInfo.InvariantCulture));
            AppendField(builder, row.RowFactoryTypeName);
            AppendField(builder, row.RowFactoryMethodName);
            AppendOrdinalStrings(builder, row.BakerKeyFieldNames);
            AppendBlobMembers(builder, row.BlobMembers, includeAccessors: true);
        }

        /// <summary>
        /// 写入影响 Blob、slot 与 range 形状的 schema 元数据。
        /// </summary>
        private static void AppendLayoutSchema(StringBuilder builder, RowMetadata row)
        {
            AppendField(builder, GetRowTypeName(row));
            AppendField(builder, ((byte)row.DefinitionKind).ToString(CultureInfo.InvariantCulture));
            AppendField(builder, row.BlobSchemaName);
            AppendBlobMembers(builder, row.BlobMembers, includeAccessors: false);
        }

        /// <summary>
        /// 按声明序号写入字符串字段，使字段重排能够改变 schema 身份。
        /// </summary>
        private static void AppendOrdinalStrings(StringBuilder builder, IReadOnlyList<string> values)
        {
            var resolved = values ?? Array.Empty<string>();
            AppendField(builder, resolved.Count.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < resolved.Count; index++)
            {
                AppendField(builder, index.ToString(CultureInfo.InvariantCulture));
                AppendField(builder, resolved[index]);
            }
        }

        /// <summary>
        /// 按声明序号写入 Blob 成员，保留 FieldOrdinal 与布局标志。
        /// </summary>
        private static void AppendBlobMembers(
            StringBuilder builder,
            IReadOnlyList<BlobMemberInfo> members,
            bool includeAccessors)
        {
            var resolved = members ?? Array.Empty<BlobMemberInfo>();
            AppendField(builder, resolved.Count.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < resolved.Count; index++)
            {
                var member = resolved[index];
                AppendField(builder, index.ToString(CultureInfo.InvariantCulture));
                AppendField(builder, member.Name);
                AppendField(builder, member.BlobTypeName);
                AppendField(builder, includeAccessors ? member.RowAccessor : string.Empty);
                AppendField(builder, BuildBlobFlags(member));
            }
        }

        /// <summary>
        /// 将 Blob 成员的结构标志编码为固定顺序的紧凑文本。
        /// </summary>
        private static string BuildBlobFlags(BlobMemberInfo member)
        {
            return (member.IsBlobString ? "1" : "0")
                   + (member.IsArray ? "1" : "0")
                   + (member.RequiresAllocate ? "1" : "0")
                   + (member.RequiresCast ? "1" : "0");
        }

        /// <summary>
        /// 按业务键与规范化值排序后写入一个 row domain 的全部内容。
        /// </summary>
        private static void AppendRowValues(StringBuilder builder, RowMetadata row)
        {
            var values = new List<RowValueSnapshot>(row.RowValues ?? Array.Empty<RowValueSnapshot>());
            values.Sort(CompareRowValues);
            AppendField(builder, values.Count.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < values.Count; index++)
            {
                AppendField(builder, values[index].Code.ToString(CultureInfo.InvariantCulture));
                AppendIntValues(builder, values[index].BakerKeyValues);
                AppendField(builder, BuildCanonicalValue(values[index].Row));
            }
        }

        /// <summary>
        /// 按声明顺序写入整数业务键，保留复合键的 ordinal 语义。
        /// </summary>
        private static void AppendIntValues(StringBuilder builder, IReadOnlyList<int> values)
        {
            var resolved = values ?? Array.Empty<int>();
            AppendField(builder, resolved.Count.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < resolved.Count; index++)
                AppendField(builder, resolved[index].ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 将对象的公开数据成员转为名称稳定、文化无关的规范文本。
        /// </summary>
        private static string BuildCanonicalValue(object value)
        {
            var builder = new StringBuilder();
            AppendValue(builder, value);
            return builder.ToString();
        }

        /// <summary>
        /// 递归写入标量、字典、序列或对象，避免运行时 ToString 产生环境漂移。
        /// </summary>
        private static void AppendValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                AppendField(builder, "<null>");
                return;
            }

            if (value is string text)
            {
                AppendField(builder, "string");
                AppendField(builder, text);
                return;
            }

            if (value is IDictionary dictionary)
            {
                AppendDictionary(builder, dictionary);
                return;
            }

            if (value is IEnumerable enumerable)
            {
                AppendEnumerable(builder, enumerable);
                return;
            }

            if (IsScalar(value.GetType()))
            {
                AppendScalar(builder, value);
                return;
            }

            AppendObject(builder, value);
        }

        /// <summary>
        /// 以规范键值文本排序字典，消除哈希表枚举顺序差异。
        /// </summary>
        private static void AppendDictionary(StringBuilder builder, IDictionary dictionary)
        {
            var entries = new List<string>();
            foreach (DictionaryEntry entry in dictionary)
                entries.Add(BuildCanonicalValue(entry.Key) + BuildCanonicalValue(entry.Value));
            entries.Sort(StringComparer.Ordinal);
            AppendField(builder, "dictionary");
            AppendField(builder, entries.Count.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < entries.Count; index++)
                AppendField(builder, entries[index]);
        }

        /// <summary>
        /// 保留序列元素顺序写入集合，因为 Luban array ordinal 属于 gameplay 语义。
        /// </summary>
        private static void AppendEnumerable(StringBuilder builder, IEnumerable enumerable)
        {
            var values = new List<string>();
            foreach (var item in enumerable)
                values.Add(BuildCanonicalValue(item));
            AppendField(builder, "sequence");
            AppendField(builder, values.Count.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < values.Count; index++)
                AppendField(builder, values[index]);
        }

        /// <summary>
        /// 以类型名和文化无关文本写入标量值。
        /// </summary>
        private static void AppendScalar(StringBuilder builder, object value)
        {
            AppendField(builder, value.GetType().FullName);
            var formattable = value as IFormattable;
            AppendField(builder, formattable != null
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value.ToString());
        }

        /// <summary>
        /// 按名称与成员类型排序公开字段和属性后写入复杂对象。
        /// </summary>
        private static void AppendObject(StringBuilder builder, object value)
        {
            var members = CollectReadableMembers(value.GetType());
            AppendField(builder, value.GetType().FullName);
            AppendField(builder, members.Count.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < members.Count; index++)
            {
                AppendField(builder, members[index].Name);
                AppendField(builder, members[index].MemberType.ToString());
                AppendValue(builder, ReadMemberValue(members[index], value));
            }
        }

        /// <summary>
        /// 收集可读公开实例成员并建立跨反射顺序稳定的排序。
        /// </summary>
        private static List<MemberInfo> CollectReadableMembers(Type type)
        {
            var members = new List<MemberInfo>();
            members.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.Instance));
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead && property.GetMethod != null && property.GetIndexParameters().Length == 0)
                    members.Add(property);
            }

            members.Sort(CompareMembers);
            return members;
        }

        /// <summary>
        /// 读取已验证的字段或属性成员值。
        /// </summary>
        private static object ReadMemberValue(MemberInfo member, object owner)
        {
            var field = member as FieldInfo;
            return field != null
                ? field.GetValue(owner)
                : ((PropertyInfo)member).GetValue(owner, null);
        }

        /// <summary>
        /// 判断值类型是否可直接按文化无关文本编码。
        /// </summary>
        private static bool IsScalar(Type type)
        {
            return type.IsPrimitive
                   || type.IsEnum
                   || type == typeof(decimal)
                   || type == typeof(DateTime)
                   || type == typeof(Guid);
        }

        /// <summary>
        /// 仅 Attribute/Tag 权威行的值会改变稠密布局身份。
        /// </summary>
        private static bool IsLayoutAuthorityRow(GasDefinitionKind kind)
        {
            return kind == GasDefinitionKind.AttributeSet
                   || kind == GasDefinitionKind.Attribute
                   || kind == GasDefinitionKind.GameplayTag;
        }

        /// <summary>
        /// 按 row 类型全名排序，消除程序集扫描顺序差异。
        /// </summary>
        private static int CompareRows(RowMetadata left, RowMetadata right)
        {
            return string.CompareOrdinal(GetRowTypeName(left), GetRowTypeName(right));
        }

        /// <summary>
        /// 按业务 code、复合键和完整值排序 source rows。
        /// </summary>
        private static int CompareRowValues(RowValueSnapshot left, RowValueSnapshot right)
        {
            var result = left.Code.CompareTo(right.Code);
            if (result != 0)
                return result;
            result = CompareIntValues(left.BakerKeyValues, right.BakerKeyValues);
            return result != 0
                ? result
                : string.CompareOrdinal(BuildCanonicalValue(left.Row), BuildCanonicalValue(right.Row));
        }

        /// <summary>
        /// 按长度和值比较复合整数键。
        /// </summary>
        private static int CompareIntValues(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            var resolvedLeft = left ?? Array.Empty<int>();
            var resolvedRight = right ?? Array.Empty<int>();
            var result = resolvedLeft.Count.CompareTo(resolvedRight.Count);
            for (var index = 0; result == 0 && index < resolvedLeft.Count; index++)
                result = resolvedLeft[index].CompareTo(resolvedRight[index]);
            return result;
        }

        /// <summary>
        /// 按成员名与成员种类建立稳定排序。
        /// </summary>
        private static int CompareMembers(MemberInfo left, MemberInfo right)
        {
            var result = string.CompareOrdinal(left.Name, right.Name);
            return result != 0
                ? result
                : string.CompareOrdinal(left.MemberType.ToString(), right.MemberType.ToString());
        }

        /// <summary>
        /// 返回 row 类型的稳定全名。
        /// </summary>
        private static string GetRowTypeName(RowMetadata row)
        {
            return row?.RowType?.FullName ?? row?.RowType?.Name ?? string.Empty;
        }

        /// <summary>
        /// 创建带版本化 domain separator 的规范哈希缓冲区。
        /// </summary>
        private static StringBuilder CreateDomainBuilder(string domain)
        {
            var builder = new StringBuilder();
            AppendField(builder, domain);
            AppendField(builder, Algorithm);
            return builder;
        }

        /// <summary>
        /// 以字符长度前缀写入字段，避免分隔符与拼接组合歧义。
        /// </summary>
        private static void AppendField(StringBuilder builder, string value)
        {
            var resolved = value ?? string.Empty;
            builder.Append(resolved.Length)
                .Append(':')
                .Append(resolved)
                .Append('|');
        }

        /// <summary>
        /// 对 UTF-8 规范文本计算小写十六进制 SHA-256。
        /// </summary>
        private static string ComputeSha256(StringBuilder builder)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            var result = new StringBuilder(hash.Length * 2);
            for (var index = 0; index < hash.Length; index++)
                result.Append(hash[index].ToString("x2"));
            return result.ToString();
        }
    }
}
