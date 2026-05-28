using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GAS.Runtime;

namespace GAS.Editor
{
    public static class RowMetadataFactory
    {
        public static IReadOnlyList<RowMetadata> BuildAll(
            IReadOnlyList<Type> rowTypes,
            GasCodeGenSettings settings)
        {
            return rowTypes.Select(rowType => Build(rowType, settings)).ToList();
        }

        public static RowMetadata Build(Type rowType, GasCodeGenSettings settings)
        {
            var domainName = InferDomainName(rowType, settings);
            var definitionName = $"{domainName}Definition";
            var blobSchemaName = $"{definitionName}Blob";
            var codeFieldName = InferCodeFieldName(rowType, domainName);
            var rowFactory = FindRowFactory(rowType);

            return new RowMetadata
            {
                RowType = rowType,
                DomainName = domainName,
                CodeFieldName = codeFieldName,
                DefinitionKind = InferDefinitionKind(domainName),
                BlobSchemaName = blobSchemaName,
                LookupName = $"{definitionName}Lookup",
                BakerMethodName = $"Build{blobSchemaName}",
                ComponentSetName = $"{definitionName}ComponentTypes",
                QueryDescName = $"{definitionName}Query",
                CodeComponentType = InferCodeComponentType(domainName),
                BlobComponentType = InferBlobComponentType(domainName),
                RowFactoryTypeName = rowFactory.TypeName,
                RowFactoryMethodName = rowFactory.MethodName,
                BlobMembers = BuildBlobMembers(rowType),
                RowValues = BuildRowValues(rowType, codeFieldName, rowFactory),
            };
        }

        private static string InferDomainName(Type rowType, GasCodeGenSettings settings)
        {
            var name = rowType.Name;
            var prefixes = settings?.RowTypePrefixesToStrip ?? Array.Empty<string>();
            for (var i = 0; i < prefixes.Count; i++)
            {
                var prefix = prefixes[i];
                if (string.IsNullOrWhiteSpace(prefix))
                    continue;

                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    name = name.Substring(prefix.Length);
                    break;
                }
            }

            if (name.EndsWith("DefinitionRow", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "DefinitionRow".Length);

            return name;
        }

        private static GASDefinitionKind InferDefinitionKind(string domainName)
        {
            if (domainName == "Ability")
                return GASDefinitionKind.Ability;
            if (domainName == "GameplayEffect")
                return GASDefinitionKind.GameplayEffect;
            if (domainName == "AttributeSet")
                return GASDefinitionKind.AttributeSet;
            if (domainName == "Attribute")
                return GASDefinitionKind.Attribute;
            if (domainName == "GameplayTag")
                return GASDefinitionKind.GameplayTag;
            if (domainName == "GameplayCue")
                return GASDefinitionKind.GameplayCue;

            return GASDefinitionKind.None;
        }

        private static string InferCodeFieldName(Type rowType, string domainName)
        {
            var fields = rowType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var fieldNames = new HashSet<string>(fields.Select(f => f.Name));
            var candidates = GetCodeFieldCandidates(domainName);

            foreach (var candidate in candidates)
                if (fieldNames.Contains(candidate))
                    return candidate;

            var codeField = fields.FirstOrDefault(f => f.Name.EndsWith("Code", StringComparison.Ordinal));
            if (codeField != null)
                return codeField.Name;

            var idField = fields.FirstOrDefault(f => f.Name.EndsWith("Id", StringComparison.Ordinal));
            if (idField != null)
                return idField.Name;

            return fields.Length > 0 ? fields[0].Name : "UnknownCode";
        }

        private static IReadOnlyList<string> GetCodeFieldCandidates(string domainName)
        {
            switch (domainName)
            {
                case "Ability":
                    return new[] { "AbilityCode" };
                case "GameplayEffect":
                    return new[] { "GameplayEffectCode" };
                case "AttributeSet":
                    return new[] { "AttributeSetCode" };
                case "Attribute":
                    return new[] { "AttributeCode", "AttributeSetCode" };
                case "GameplayTag":
                    return new[] { "GameplayTagCode" };
                case "GameplayCue":
                    return new[] { "GameplayCueCode" };
                case "Timeline":
                    return new[] { "TimelineId" };
                case "Summon":
                    return new[] { "SummonGameplayEffectCode" };
                default:
                    return new[]
                    {
                        "AbilityCode",
                        "GameplayEffectCode",
                        "AttributeCode",
                        "AttributeSetCode",
                        "GameplayTagCode",
                        "GameplayCueCode",
                        "TimelineId",
                        "SummonGameplayEffectCode",
                    };
            }
        }

        private static string InferCodeComponentType(string domainName)
        {
            return "GASDefinitionCodeComponent";
        }

        private static string InferBlobComponentType(string domainName)
        {
            return string.Empty;
        }

        private static RowFactoryInfo FindRowFactory(Type rowType)
        {
            var candidates = new List<MethodInfo>();
            foreach (var type in rowType.Assembly.GetExportedTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.GetParameters().Length != 0)
                        continue;

                    if (!ReturnsRowArray(method, rowType))
                        continue;

                    candidates.Add(method);
                }
            }

            var selected = candidates
                .OrderByDescending(method => method.Name.StartsWith("Create", StringComparison.Ordinal)
                                             && method.Name.EndsWith("Rows", StringComparison.Ordinal))
                .ThenBy(method => method.DeclaringType?.FullName, StringComparer.Ordinal)
                .ThenBy(method => method.Name, StringComparer.Ordinal)
                .FirstOrDefault();

            if (selected == null)
                return RowFactoryInfo.None;

            return new RowFactoryInfo(
                (selected.DeclaringType?.FullName ?? selected.DeclaringType?.Name ?? string.Empty).Replace('+', '.'),
                selected.Name,
                selected);
        }

        private static bool ReturnsRowArray(MethodInfo method, Type rowType)
        {
            var returnType = method.ReturnType;
            return returnType.IsArray && returnType.GetElementType() == rowType;
        }

        private static IReadOnlyList<RowValueSnapshot> BuildRowValues(
            Type rowType,
            string codeFieldName,
            RowFactoryInfo rowFactory)
        {
            var rows = rowFactory.Method != null
                ? InvokeRows(rowFactory.Method, rowType)
                : FindRowsFromSnapshot(rowType);

            if (rows.Count == 0)
                return Array.Empty<RowValueSnapshot>();

            var result = new List<RowValueSnapshot>(rows.Count);
            foreach (var row in rows)
            {
                result.Add(new RowValueSnapshot
                {
                    Row = row,
                    Code = Convert.ToInt32(GetRowMemberValue(rowType, row, codeFieldName) ?? 0),
                });
            }

            return result;
        }

        private static IReadOnlyList<object> InvokeRows(MethodInfo method, Type rowType)
        {
            if (method == null)
                return Array.Empty<object>();

            var value = method.Invoke(null, null);
            return MaterializeRows(value, rowType);
        }

        private static IReadOnlyList<object> FindRowsFromSnapshot(Type rowType)
        {
            foreach (var type in rowType.Assembly.GetExportedTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.GetParameters().Length != 0)
                        continue;

                    if (!method.Name.StartsWith("Create", StringComparison.Ordinal))
                        continue;

                    if (method.DeclaringType == null
                        || method.DeclaringType.Name.IndexOf("GeneratedDefinition", StringComparison.Ordinal) < 0)
                        continue;

                    if (method.ReturnType == typeof(void) || method.ReturnType.IsPrimitive || method.ReturnType == typeof(string))
                        continue;

                    var snapshot = method.Invoke(null, null);
                    if (snapshot == null)
                        continue;

                    var rows = FindRowsOnObject(snapshot, rowType);
                    if (rows.Count > 0)
                        return rows;
                }
            }

            return Array.Empty<object>();
        }

        private static IReadOnlyList<object> FindRowsOnObject(object owner, Type rowType)
        {
            var ownerType = owner.GetType();
            foreach (var property in ownerType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead)
                    continue;

                if (!IsEnumerableOfRowType(property.PropertyType, rowType))
                    continue;

                var rows = MaterializeRows(property.GetValue(owner), rowType);
                if (rows.Count > 0)
                    return rows;
            }

            foreach (var field in ownerType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!IsEnumerableOfRowType(field.FieldType, rowType))
                    continue;

                var rows = MaterializeRows(field.GetValue(owner), rowType);
                if (rows.Count > 0)
                    return rows;
            }

            return Array.Empty<object>();
        }

        private static bool IsEnumerableOfRowType(Type type, Type rowType)
        {
            if (type.IsArray)
                return type.GetElementType() == rowType;

            if (!type.IsGenericType)
                return false;

            var generic = type.GetGenericTypeDefinition();
            return (generic == typeof(IReadOnlyList<>)
                    || generic == typeof(IReadOnlyCollection<>)
                    || generic == typeof(IEnumerable<>)
                    || generic == typeof(List<>))
                   && type.GetGenericArguments()[0] == rowType;
        }

        private static IReadOnlyList<object> MaterializeRows(object rows, Type rowType)
        {
            if (rows == null)
                return Array.Empty<object>();

            if (rows is IEnumerable enumerable)
            {
                var result = new List<object>();
                foreach (var row in enumerable)
                {
                    if (row != null && row.GetType() == rowType)
                        result.Add(row);
                }

                return result;
            }

            return Array.Empty<object>();
        }

        private static object GetRowMemberValue(Type rowType, object row, string memberName)
        {
            var field = rowType.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(row);

            var property = rowType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.CanRead ? property.GetValue(row) : null;
        }

        private static IReadOnlyList<BlobMemberInfo> BuildBlobMembers(Type rowType)
        {
            var result = new List<BlobMemberInfo>();

            foreach (var field in rowType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var member = MapMember(field.FieldType, field.Name, field.Name);
                if (member != null)
                    result.Add(member);
            }

            foreach (var property in rowType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetMethod == null)
                    continue;

                if (!property.PropertyType.IsGenericType)
                    continue;

                if (property.PropertyType.GetGenericTypeDefinition() != typeof(IReadOnlyList<>))
                    continue;

                var member = MapMember(property.PropertyType, property.Name, property.Name);
                if (member != null)
                    result.Add(member);
            }

            return result;
        }

        private static BlobMemberInfo MapMember(Type memberType, string name, string rowAccessor)
        {
            var info = new BlobMemberInfo
            {
                Name = name,
                RowAccessor = rowAccessor,
            };

            if (memberType == typeof(int))
                info.BlobTypeName = "int";
            else if (memberType == typeof(float))
                info.BlobTypeName = "float";
            else if (memberType == typeof(bool))
                info.BlobTypeName = "bool";
            else if (memberType == typeof(string))
            {
                info.BlobTypeName = "BlobString";
                info.IsBlobString = true;
            }
            else if (memberType.IsEnum)
            {
                info.BlobTypeName = "int";
                info.RequiresCast = true;
            }
            else if (memberType.IsArray)
            {
                info.BlobTypeName = $"BlobArray<{BlobElementTypeName(memberType.GetElementType())}>";
                info.IsArray = true;
                info.RequiresAllocate = true;
            }
            else if (memberType.IsGenericType)
            {
                var genericType = memberType.GetGenericTypeDefinition();
                if (genericType != typeof(IReadOnlyList<>) && genericType != typeof(List<>))
                    return null;

                info.BlobTypeName = $"BlobArray<{BlobElementTypeName(memberType.GetGenericArguments()[0])}>";
                info.RequiresAllocate = true;
            }
            else
            {
                return null;
            }

            return info;
        }

        private static string BlobElementTypeName(Type type)
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(bool)) return "bool";
            if (type != null && type.IsEnum) return "int";
            return "int";
        }

        private readonly struct RowFactoryInfo
        {
            public static readonly RowFactoryInfo None = new RowFactoryInfo(string.Empty, string.Empty);

            public readonly string TypeName;
            public readonly string MethodName;
            public readonly MethodInfo Method;

            public RowFactoryInfo(string typeName, string methodName, MethodInfo method = null)
            {
                TypeName = typeName;
                MethodName = methodName;
                Method = method;
            }
        }
    }
}
