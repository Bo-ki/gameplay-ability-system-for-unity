using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Gas.CodeGen.SourceGenerator
{
    /// <summary>
    /// 集中定义 D0-M2R 冻结的 selector、bundle、required-set 与 route scaffold 常量，避免 writer、reader 和 generator 出现第二事实源。
    /// </summary>
    public static class GasCodeGenContract
    {
        public const uint BundleVersion = 1;
        public const string SelectorMagic = "EX-GAS-SourceSelector-v1\n";
        public const string BundleEncodingDomain = "EX-GAS-SourceBundle-v1";
        public const string RequiredArtifactSetId = "EX-GAS-RuntimeV1-RequiredArtifacts-v2";
        public const string RequiredArtifactSetContractDomain = "EX-GAS-RequiredArtifactSetContract-v1\0";
        public const string SourceArtifactInventoryDomain = "EX-GAS-SourceArtifactInventory-v1\0";
        public const string ArtifactManifestHashDomain = "EX-GAS-ArtifactManifest-v3";
        public const string RouteScaffoldDomain = "EX-GAS-RouteScaffold-v1\0";
        public const string SelectorRelativePath = "Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile";
        public const string SelectorFileName = "Generation.GasCodeGenSourceGenerator.additionalfile";
        public const string AnalyzerRelativePath = "Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll";
        public const string MarkerHintName = "GasCodeGenSourceGenerator.Marker.g.cs";
        public const string RuntimeAssembly = "com.exhard.exgas.generated.runtime";
        public const string EditorAssembly = "com.exhard.exgas.generated.editor";
        public const string AutoChessAssembly = "com.exhard.exgas.autochessdemo";
        public const int RequiredSourceCount = 5;
        public const int MaximumSourceCount = 4096;
        public const int MaximumSourceByteLength = 16777216;
        public const long MaximumTotalSourceByteLength = 67108864;
        public const string RequiredArtifactSetContractHashHex = "63f69551708194359d76c3772fb68983a3632d8994d818d7ec3e8c931ac74565";

        private static readonly string[] RouteScaffoldPathValues =
        {
            "Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll.meta",
            "Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile.meta",
            "Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef",
            "Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef.meta",
            "Assets/GAS/Generated/CodeGen/Runtime/GasCodeGenSourceGeneratorRequired.cs",
            "Assets/GAS/Generated/CodeGen/Runtime/GasCodeGenSourceGeneratorRequired.cs.meta",
            "Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef",
            "Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef.meta",
            "Assets/GAS/Generated/CodeGen/Editor/GasCodeGenSourceGeneratorRequired.cs",
            "Assets/GAS/Generated/CodeGen/Editor/GasCodeGenSourceGeneratorRequired.cs.meta",
            "Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef",
            "Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef.meta",
            "Assets/AutoChessDemo/Generated/GasCodeGenSourceGeneratorRequired.cs",
            "Assets/AutoChessDemo/Generated/GasCodeGenSourceGeneratorRequired.cs.meta",
        };

        private static readonly RequiredArtifactDefinition[] RequiredArtifactValues =
        {
            new RequiredArtifactDefinition(
                "Assets/AutoChessDemo/Generated/AutoChessGeneratedConfig.gen.cs",
                "RuntimeDemoConfig", "AutoChessDemo", true, "AutoChess",
                AutoChessAssembly, "AutoChessGeneratedConfig.gen.cs", 3),
            new RequiredArtifactDefinition(
                "Assets/GAS/Generated/CodeGen/Editor/LubanNormalizedRows.gen.cs",
                "NormalizedDefinitionRow", "DefinitionCodeGen", false, "Core",
                EditorAssembly, "LubanNormalizedRows.gen.cs", 2),
            new RequiredArtifactDefinition(
                "Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md",
                "ValidationArtifact", "EditorCi", false, "Core", string.Empty, string.Empty, 0),
            new RequiredArtifactDefinition(
                "Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs",
                "RuntimePureGlue", "DefinitionCodeGen", true, "Core",
                RuntimeAssembly, "RuntimeAbilityActivation.gen.cs", 1),
            new RequiredArtifactDefinition(
                "Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs",
                "RuntimePureGlue", "DefinitionCodeGen", true, "Core",
                RuntimeAssembly, "RuntimeActiveEffect.gen.cs", 1),
            new RequiredArtifactDefinition(
                "Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs",
                "RuntimePureGlue", "DefinitionCodeGen", true, "Core",
                RuntimeAssembly, "RuntimeEffectInstant.gen.cs", 1),
        };

        /// <summary>
        /// 返回按 canonical path ordinal 排序的 14 项 immutable route scaffold 相对路径副本。
        /// </summary>
        public static IReadOnlyList<string> GetRouteScaffoldPaths()
        {
            string[] paths = (string[])RouteScaffoldPathValues.Clone();
            Array.Sort(paths, StringComparer.Ordinal);
            return Array.AsReadOnly(paths);
        }

        /// <summary>
        /// 返回按 canonical path ordinal 排序的 6 项 required artifact 冻结定义副本。
        /// </summary>
        public static IReadOnlyList<RequiredArtifactDefinition> GetRequiredArtifacts()
        {
            return Array.AsReadOnly((RequiredArtifactDefinition[])RequiredArtifactValues.Clone());
        }

        /// <summary>
        /// 依冻结 wire 重新计算 required-set contract hash，供启动和测试拒绝常量漂移。
        /// </summary>
        public static byte[] ComputeRequiredArtifactSetContractHash()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                GasBinaryEncoding.WriteAscii(stream, RequiredArtifactSetContractDomain);
                GasBinaryEncoding.WriteUInt32(stream, (uint)RequiredArtifactValues.Length);
                foreach (RequiredArtifactDefinition item in RequiredArtifactValues)
                {
                    GasBinaryEncoding.WriteString(stream, item.CanonicalPath);
                    GasBinaryEncoding.WriteString(stream, item.Kind);
                    GasBinaryEncoding.WriteString(stream, item.Owner);
                    stream.WriteByte(item.RuntimeVisible ? (byte)1 : (byte)0);
                    GasBinaryEncoding.WriteString(stream, item.Component);
                    GasBinaryEncoding.WriteString(stream, item.TargetAssembly);
                    GasBinaryEncoding.WriteString(stream, item.HintName);
                    stream.WriteByte(item.Category);
                    stream.WriteByte(1);
                }

                return GasHashing.ComputeSha256(stream.ToArray());
            }
        }

        /// <summary>
        /// 验证冻结 required-set 常量与运行时重算结果一致，防止实现和文档协议分叉。
        /// </summary>
        public static void ValidateFrozenContractHash()
        {
            string actual = GasHashing.ToLowerHex(ComputeRequiredArtifactSetContractHash());
            if (!string.Equals(actual, RequiredArtifactSetContractHashHex, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Required artifact set contract hash does not match the frozen v2 value.");
            }
        }

        /// <summary>
        /// 判断程序集是否属于 SourceGenerator 唯一允许的三个目标 owner。
        /// </summary>
        public static bool IsTargetAssembly(string assemblyName)
        {
            return string.Equals(assemblyName, RuntimeAssembly, StringComparison.Ordinal)
                || string.Equals(assemblyName, EditorAssembly, StringComparison.Ordinal)
                || string.Equals(assemblyName, AutoChessAssembly, StringComparison.Ordinal);
        }

        /// <summary>
        /// 返回目标程序集对应的唯一 artifact category，非目标程序集返回零。
        /// </summary>
        public static byte GetExpectedCategory(string assemblyName)
        {
            if (string.Equals(assemblyName, RuntimeAssembly, StringComparison.Ordinal))
            {
                return 1;
            }

            if (string.Equals(assemblyName, EditorAssembly, StringComparison.Ordinal))
            {
                return 2;
            }

            return string.Equals(assemblyName, AutoChessAssembly, StringComparison.Ordinal) ? (byte)3 : (byte)0;
        }
    }

    /// <summary>
    /// 描述 `EX-GAS-RuntimeV1-RequiredArtifacts-v2` 中单个精确 manifest item 及其 source routing 约束。
    /// </summary>
    public sealed class RequiredArtifactDefinition
    {
        /// <summary>
        /// 创建不可变 required artifact 定义；managed meta 在 v2 中始终为必需。
        /// </summary>
        public RequiredArtifactDefinition(
            string canonicalPath,
            string kind,
            string owner,
            bool runtimeVisible,
            string component,
            string targetAssembly,
            string hintName,
            byte category)
        {
            CanonicalPath = canonicalPath;
            Kind = kind;
            Owner = owner;
            RuntimeVisible = runtimeVisible;
            Component = component;
            TargetAssembly = targetAssembly;
            HintName = hintName;
            Category = category;
        }

        public string CanonicalPath { get; private set; }
        public string Kind { get; private set; }
        public string Owner { get; private set; }
        public bool RuntimeVisible { get; private set; }
        public string Component { get; private set; }
        public string TargetAssembly { get; private set; }
        public string HintName { get; private set; }
        public byte Category { get; private set; }
        public bool ManagedMetaRequired { get { return true; } }
        public bool IsSourceArtifact { get { return TargetAssembly.Length > 0; } }
    }

    /// <summary>
    /// 提供协议使用的固定 SHA-256 与小写十六进制转换，禁止依赖 culture 或默认格式化。
    /// </summary>
    internal static class GasHashing
    {
        /// <summary>
        /// 计算完整 byte snapshot 的 SHA-256。
        /// </summary>
        internal static byte[] ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(bytes);
            }
        }

        /// <summary>
        /// 将 32-byte digest 转换为 canonical 小写十六进制。
        /// </summary>
        internal static string ToLowerHex(byte[] digest)
        {
            if (digest == null || digest.Length != 32)
            {
                throw new ArgumentException("SHA-256 digest must contain exactly 32 bytes.", nameof(digest));
            }

            StringBuilder builder = new StringBuilder(64);
            foreach (byte value in digest)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }

    }

    /// <summary>
    /// 实现冻结协议的 little-endian 与 length-prefixed UTF-8 基础编码，不暴露可变格式选项。
    /// </summary>
    internal static class GasBinaryEncoding
    {
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// 写入固定 ASCII domain bytes。
        /// </summary>
        internal static void WriteAscii(Stream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// 写入 uint32 little-endian。
        /// </summary>
        internal static void WriteUInt32(Stream stream, uint value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        /// <summary>
        /// 写入 uint64 little-endian。
        /// </summary>
        internal static void WriteUInt64(Stream stream, ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                stream.WriteByte((byte)(value >> shift));
            }
        }

        /// <summary>
        /// 写入 uint32 byte length 加严格 UTF-8 bytes。
        /// </summary>
        internal static void WriteString(Stream stream, string value)
        {
            byte[] bytes = StrictUtf8.GetBytes(value ?? string.Empty);
            WriteUInt32(stream, checked((uint)bytes.Length));
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
