using System;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GAS.Tests.D0M2F.SourceGenerator
{
    /// <summary>
    /// 仅为 D0-M2F 隔离工程生成三程序集同代 canary，用于验证单 additional-file 是否驱动真实编译图。
    /// </summary>
    [Generator]
    public sealed class D0M2FCanaryGenerator : ISourceGenerator
    {
        private const string SelectorFileName = "Generation.D0M2FCanaryGenerator.additionalfile";
        private const string RuntimeAssembly = "com.exhard.exgas.generated.runtime";
        private const string EditorAssembly = "com.exhard.exgas.generated.editor";
        private const string AutoChessAssembly = "com.exhard.exgas.autochessdemo";
        private static readonly DiagnosticDescriptor SelectorError = new DiagnosticDescriptor(
            "D0M2FSG001",
            "D0-M2F semantic selector invalid",
            "{0}",
            "D0M2F",
            DiagnosticSeverity.Error,
            true);

        /// <summary>
        /// 本探针无需注册语法接收器，全部输入只来自单一 additional-file。
        /// </summary>
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        /// <summary>
        /// 对三个冻结程序集读取同一 selector，并生成携带 generation 与程序集身份的 marker。
        /// </summary>
        public void Execute(GeneratorExecutionContext context)
        {
            string markerNamespace = GetMarkerNamespace(context.Compilation.AssemblyName);
            if (markerNamespace.Length == 0)
            {
                return;
            }

            AdditionalText selector = FindUniqueSelector(context);
            if (selector == null)
            {
                return;
            }

            SourceText selectorText = selector.GetText(context.CancellationToken);
            string generation = selectorText == null ? string.Empty : selectorText.ToString().Trim();
            if (generation != "generation-a" && generation != "generation-b")
            {
                ReportError(context, "Semantic selector must be exactly generation-a or generation-b.");
                return;
            }

            string assemblyName = context.Compilation.AssemblyName ?? string.Empty;
            string source = BuildMarkerSource(markerNamespace, assemblyName, generation);
            context.AddSource("D0M2F.GenerationMarker.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        /// <summary>
        /// 按冻结 assembly graph 映射 marker namespace，其他程序集不生成任何输出。
        /// </summary>
        private static string GetMarkerNamespace(string assemblyName)
        {
            if (assemblyName == RuntimeAssembly)
            {
                return "D0M2F.Generated.Runtime";
            }

            if (assemblyName == EditorAssembly)
            {
                return "D0M2F.Generated.Editor";
            }

            return assemblyName == AutoChessAssembly ? "D0M2F.Generated.AutoChess" : string.Empty;
        }

        /// <summary>
        /// 查找唯一、精确命名的 semantic selector，缺失或重复均以编译错误 fail closed。
        /// </summary>
        private static AdditionalText FindUniqueSelector(GeneratorExecutionContext context)
        {
            AdditionalText selected = null;
            foreach (AdditionalText candidate in context.AdditionalFiles)
            {
                if (!string.Equals(Path.GetFileName(candidate.Path), SelectorFileName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (selected != null)
                {
                    ReportError(context, "More than one semantic selector reached the same compilation.");
                    return null;
                }

                selected = candidate;
            }

            if (selected == null)
            {
                ReportError(context, "The semantic selector did not reach this compilation.");
            }

            return selected;
        }

        /// <summary>
        /// 构造 C# 9 兼容的不可变 marker 源码，避免生成任何运行时 selector fallback。
        /// </summary>
        private static string BuildMarkerSource(string markerNamespace, string assemblyName, string generation)
        {
            return "namespace " + markerNamespace + "\n"
                + "{\n"
                + "    /// <summary>记录本程序集由单一 D0-M2F semantic selector 生成的身份。</summary>\n"
                + "    public static class GenerationMarker\n"
                + "    {\n"
                + "        public const string GenerationId = \"" + generation + "\";\n"
                + "        public const string DeclaredAssembly = \"" + assemblyName + "\";\n"
                + "        public const string SelectorFileName = \"" + SelectorFileName + "\";\n"
                + "    }\n"
                + "}\n";
        }

        /// <summary>
        /// 把 selector 合同错误写入 Roslyn 诊断，使缺失输入不能退化为陈旧输出。
        /// </summary>
        private static void ReportError(GeneratorExecutionContext context, string message)
        {
            context.ReportDiagnostic(Diagnostic.Create(SelectorError, Location.None, message));
        }
    }
}
