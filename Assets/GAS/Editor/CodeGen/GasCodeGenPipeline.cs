using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GAS.Editor
{
    /// <summary>
    /// 在独立 candidate 中生成精确 source bytes，并仅通过单 canonical selector 发布完整 generation。
    /// </summary>
    public static class GasCodeGenPipeline
    {
        private static readonly IGasCodeGenPhase[] s_corePhases =
        {
            new RuntimeLifecycleMigrationPhase(),
            new ValidationReportPhase(),
        };

        private static readonly IGasCodeGenPhase[] s_autoChessDemoPhases =
        {
            new AutoChessDemoConfigPhase(),
        };

        /// <summary>
        /// 执行 Core 与 AutoChess 的同代候选生成；失败信息由 TryRunAll 统一记录。
        /// </summary>
        public static void RunAll()
        {
            TryRunAll();
        }

        /// <summary>
        /// 生成并发布完整配置包，同时刷新 Unity AssetDatabase。
        /// </summary>
        public static bool TryRunAll()
        {
            return TryRunAll(refreshAssetDatabase: true);
        }

        /// <summary>
        /// 生成 Core 与 AutoChess 的同一输入快照，并在两者全绿后组合晋升。
        /// </summary>
        public static bool TryRunAll(bool refreshAssetDatabase)
        {
            GasCodeGenCandidateWorkspace workspace = null;
            GasCodeGenGenerationStore generationStore = null;
            try
            {
                generationStore = GasCodeGenGenerationStore.Open(GasCodeGenEnvironment.ProjectRoot);
                workspace = GasCodeGenCandidateWorkspace.Create(GasCodeGenEnvironment.ProjectRoot);
                var coreContext = GasCodeGenContext.Create(true, workspace.CoreCandidateRoot);
                if (!GenerateCoreCandidate(
                        s_corePhases,
                        coreContext,
                        workspace.CoreActiveRoot,
                        out var coreManifest))
                    return false;

                var demoContext = coreContext.WithOutputDir(workspace.AutoChessCandidateRoot);
                if (!GenerateStandaloneCandidate(
                        s_autoChessDemoPhases,
                        demoContext,
                        workspace.AutoChessActiveRoot,
                        out var autoChessManifest))
                    return false;

                var semanticIdentity = GasCodeGenSemanticIdentity.Create(coreContext.Rows);
                var route = GasCodeGenCandidateCompileGate.CaptureProductionRoute(workspace.ProjectRoot);
                var authority = workspace.SealAuthority();
                var selectorCandidate = GasCodeGenPackageDescriptor.CreateSelectorCandidate(
                    workspace,
                    authority,
                    coreContext.InputHash,
                    semanticIdentity.SchemaHash,
                    semanticIdentity.ContentHash,
                    semanticIdentity.LayoutHash,
                    coreManifest,
                    autoChessManifest,
                    route);
                EnsureInputsUnchanged(coreContext);
                var compilePlan = GasCodeGenCandidateCompileGate.ValidateFullPackage(
                    workspace,
                    authority,
                    selectorCandidate,
                    route);
                var descriptor = GasCodeGenPackageDescriptor.Save(
                    workspace,
                    authority,
                    selectorCandidate,
                    compilePlan,
                    route);
                coreManifest.EnsureFrozenArtifactIdentitiesUnchanged();
                autoChessManifest.EnsureFrozenArtifactIdentitiesUnchanged();
                EnsureInputsUnchanged(coreContext);
                descriptor.EnsureUnchanged();
                var generation = generationStore.StageGeneration(descriptor);
                EnsureInputsUnchanged(coreContext);
                var publishResult = generationStore.Publish(generation);
                if (!publishResult.IsNoOp)
                    generationStore.VerifyActive();
                RefreshAfterCommit(refreshAssetDatabase);
                GasCodeGenEnvironment.Log(
                    "[GasCodeGenPipeline] Core/AutoChess 已封装并通过唯一 selector 发布。"
                    + $" GenerationId={generation.GenerationId}, "
                    + $"SelectorSha256={selectorCandidate.SelectorSha256}, "
                    + $"ArtifactManifestHash={descriptor.ArtifactManifestHash}, "
                    + $"CommitState={publishResult.State}");
                return true;
            }
            catch (Exception ex)
            {
                GasCodeGenEnvironment.LogException(ex);
                return false;
            }
            finally
            {
                CleanupWorkspace(workspace);
                if (generationStore != null)
                    generationStore.Dispose();
            }
        }

        /// <summary>
        /// 仅生成 Core candidate；该入口用于开发诊断，不代表完整 package 同代发布。
        /// </summary>
        public static bool TryRunCore(bool refreshAssetDatabase)
        {
            return RunCoreDiagnostic(s_corePhases);
        }

        /// <summary>
        /// 仅生成 AutoChess candidate；该入口用于开发诊断，不代表完整 package 同代发布。
        /// </summary>
        public static bool TryRunAutoChessDemo(bool refreshAssetDatabase)
        {
            return RunDemoDiagnostic(s_autoChessDemoPhases);
        }

        /// <summary>
        /// 让自定义 Core phase 复用与生产入口一致的 candidate 隔离和晋升边界。
        /// </summary>
        internal static bool Run(IEnumerable<IGasCodeGenPhase> phases, bool refreshAssetDatabase = true)
        {
            var phaseList = phases as IReadOnlyList<IGasCodeGenPhase> ?? phases.ToArray();
            return RunCoreDiagnostic(phaseList);
        }

        /// <summary>
        /// 在单根 Core candidate 中执行只读诊断；partial 入口永不改写 active package。
        /// </summary>
        private static bool RunCoreDiagnostic(IReadOnlyList<IGasCodeGenPhase> phases)
        {
            GasCodeGenCandidateWorkspace workspace = null;
            try
            {
                workspace = GasCodeGenCandidateWorkspace.Create(GasCodeGenEnvironment.ProjectRoot);
                var context = GasCodeGenContext.Create(true, workspace.CoreCandidateRoot);
                if (!GenerateCoreCandidate(
                        phases,
                        context,
                        workspace.CoreActiveRoot,
                        out _))
                    return false;

                EnsureInputsUnchanged(context);
                GasCodeGenEnvironment.Log(
                    "[GasCodeGenPipeline] Core candidate 诊断通过；partial 入口未发布 active package。");
                return true;
            }
            catch (Exception ex)
            {
                GasCodeGenEnvironment.LogException(ex);
                return false;
            }
            finally
            {
                CleanupWorkspace(workspace);
            }
        }

        /// <summary>
        /// 在单根 AutoChess candidate 中执行只读诊断；partial 入口永不改写 active package。
        /// </summary>
        private static bool RunDemoDiagnostic(IReadOnlyList<IGasCodeGenPhase> phases)
        {
            GasCodeGenCandidateWorkspace workspace = null;
            try
            {
                workspace = GasCodeGenCandidateWorkspace.Create(GasCodeGenEnvironment.ProjectRoot);
                var context = GasCodeGenContext.Create(true, workspace.AutoChessCandidateRoot);
                if (!GenerateStandaloneCandidate(
                        phases,
                        context,
                        workspace.AutoChessActiveRoot,
                        out _))
                    return false;

                EnsureInputsUnchanged(context);
                GasCodeGenEnvironment.Log(
                    "[GasCodeGenPipeline] AutoChess candidate 诊断通过；partial 入口未发布 active package。");
                return true;
            }
            catch (Exception ex)
            {
                GasCodeGenEnvironment.LogException(ex);
                return false;
            }
            finally
            {
                CleanupWorkspace(workspace);
            }
        }

        /// <summary>
        /// 生成 Core candidate、执行合约 gate、清理候选 orphan 并冻结 manifest bytes。
        /// </summary>
        private static bool GenerateCoreCandidate(
            IReadOnlyList<IGasCodeGenPhase> phases,
            GasCodeGenContext context,
            string publishedOutputRoot,
            out GasCodeGenManifest frozenManifest)
        {
            frozenManifest = null;
            EnsureRequiredCandidateMetas(context, autoChess: false);
            var lubanRowsPath = LubanNormalizedRowBootstrap.Generate(
                context.Settings,
                context.OutputDir);
            if (context.RowTypes.Count == 0)
            {
                throw new InvalidOperationException(
                    "[GasCodeGenPipeline] 未找到任何 Definition Row。禁止 partial generation 输出过期 artifact。");
            }

            var manifest = new GasCodeGenManifest(
                context.ProjectRoot,
                context.OutputDir,
                context.InputHash,
                publishedOutputRoot);
            manifest.AddGeneratedFile(
                "LubanNormalizedRows",
                lubanRowsPath,
                "Editor",
                false,
                "NormalizedDefinitionRow",
                "DefinitionCodeGen");
            var errors = new List<string>();
            var orphanCleanupRan = false;
            for (var index = 0; index < phases.Count; index++)
                ExecuteCorePhase(phases[index], context, manifest, errors, ref orphanCleanupRan);

            AppendManifestContractError(manifest, errors);
            if (!orphanCleanupRan && errors.Count == 0)
                context.OrphansDeleted = manifest.DeleteOrphanedFiles();
            if (!ReportErrors(errors, "Core"))
                return false;

            NormalizeRequiredArtifactText(context, autoChess: false);
            manifest.Save();
            frozenManifest = manifest;
            GasCodeGenEnvironment.Log(
                $"[GasCodeGenPipeline] Core candidate 完成。Rows={context.Rows.Count}, "
                + $"Phases={phases.Count}, OrphansDeleted={context.OrphansDeleted}");
            return true;
        }

        /// <summary>
        /// 执行一个 Core phase，并在 ValidationReport 前完成候选 orphan 与合约检查。
        /// </summary>
        private static void ExecuteCorePhase(
            IGasCodeGenPhase phase,
            GasCodeGenContext context,
            GasCodeGenManifest manifest,
            ICollection<string> errors,
            ref bool orphanCleanupRan)
        {
            try
            {
                if (!orphanCleanupRan
                    && errors.Count == 0
                    && phase.PhaseName == "ValidationReport")
                {
                    PreRegisterPhaseOutputs(context, manifest, phase, "Editor/CI", false);
                    manifest.EnsureContractValid();
                    context.OrphansDeleted = manifest.DeleteOrphanedFiles();
                    orphanCleanupRan = true;
                }

                phase.Execute(context, manifest);
                GasCodeGenEnvironment.Log(
                    $"[GasCodeGenPipeline] {phase.PhaseName} 完成: {string.Join(", ", phase.OutputFileNames)}");
            }
            catch (Exception ex)
            {
                errors.Add($"{phase.PhaseName}: {ex.Message}");
                GasCodeGenEnvironment.LogException(ex);
            }
        }

        /// <summary>
        /// 生成一个独立候选根，并在所有 phase 与 manifest byte gate 成功后返回。
        /// </summary>
        private static bool GenerateStandaloneCandidate(
            IReadOnlyList<IGasCodeGenPhase> phases,
            GasCodeGenContext context,
            string publishedOutputRoot,
            out GasCodeGenManifest frozenManifest)
        {
            frozenManifest = null;
            EnsureRequiredCandidateMetas(context, autoChess: true);
            var manifest = new GasCodeGenManifest(
                context.ProjectRoot,
                context.OutputDir,
                context.InputHash,
                publishedOutputRoot);
            var errors = new List<string>();
            for (var index = 0; index < phases.Count; index++)
            {
                try
                {
                    phases[index].Execute(context, manifest);
                    GasCodeGenEnvironment.Log(
                        $"[GasCodeGenPipeline] {phases[index].PhaseName} 完成: "
                        + string.Join(", ", phases[index].OutputFileNames));
                }
                catch (Exception ex)
                {
                    errors.Add($"{phases[index].PhaseName}: {ex.Message}");
                    GasCodeGenEnvironment.LogException(ex);
                }
            }

            AppendManifestContractError(manifest, errors);
            if (errors.Count == 0)
                context.OrphansDeleted = manifest.DeleteOrphanedFiles();
            if (!ReportErrors(errors, "AutoChess"))
                return false;

            NormalizeRequiredArtifactText(context, autoChess: true);
            manifest.Save();
            frozenManifest = manifest;
            GasCodeGenEnvironment.Log(
                $"[GasCodeGenPipeline] AutoChess candidate 完成。Phases={phases.Count}, "
                + $"OrphansDeleted={context.OrphansDeleted}");
            return true;
        }

        /// <summary>
        /// 在晋升前重算全部 artifact-producing 输入，拒绝生成期间发生的 TOCTOU 漂移。
        /// </summary>
        private static void EnsureInputsUnchanged(GasCodeGenContext originalContext)
        {
            var currentContext = GasCodeGenContext.Create(false, originalContext.OutputDir);
            if (!string.Equals(
                    originalContext.InputHash,
                    currentContext.InputHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "[GasCodeGenPipeline] 输入在 candidate 生成期间发生变化，已拒绝晋升。");
            }
        }

        /// <summary>
        /// 在提交点之后刷新 Unity；刷新失败作为明确 post-commit 债务记录，不反判生成事务。
        /// </summary>
        private static void RefreshAfterCommit(bool refreshAssetDatabase)
        {
            if (!refreshAssetDatabase)
                return;

            try
            {
                GasCodeGenEnvironment.RefreshAssetDatabase();
            }
            catch (Exception ex)
            {
                GasCodeGenEnvironment.LogError(
                    "[GasCodeGenPipeline] Candidate 已提交，但 AssetDatabase 刷新失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 清理本次 workspace；实现会记录 cleanup debt，且不改变已确定的事务结果。
        /// </summary>
        private static void CleanupWorkspace(GasCodeGenCandidateWorkspace workspace)
        {
            if (workspace != null)
                workspace.Cleanup();
        }

        /// <summary>
        /// 将 phase 的声明输出预登记到 manifest，使验证报告可在写入自身前完成合约检查。
        /// </summary>
        private static void PreRegisterPhaseOutputs(
            GasCodeGenContext context,
            GasCodeGenManifest manifest,
            IGasCodeGenPhase phase,
            string layer,
            bool runtimeVisible)
        {
            for (var index = 0; index < phase.OutputFileNames.Count; index++)
            {
                manifest.AddGeneratedFile(
                    phase.PhaseName,
                    Path.Combine(context.OutputDir, phase.OutputFileNames[index]),
                    layer,
                    runtimeVisible,
                    "ValidationArtifact",
                    "EditorCi");
            }
        }

        /// <summary>
        /// 聚合 manifest 合约错误，避免自定义 phase 绕过统一 gate。
        /// </summary>
        private static void AppendManifestContractError(
            GasCodeGenManifest manifest,
            ICollection<string> errors)
        {
            var contractErrors = manifest.CollectContractErrors();
            for (var index = 0; index < contractErrors.Count; index++)
                errors.Add("ManifestContract: " + contractErrors[index]);
        }

        /// <summary>
        /// 统一记录候选错误并返回是否允许继续冻结 manifest。
        /// </summary>
        private static bool ReportErrors(IReadOnlyCollection<string> errors, string scope)
        {
            if (errors.Count == 0)
                return true;

            GasCodeGenEnvironment.LogError(
                $"[GasCodeGenPipeline] {scope} candidate 有 {errors.Count} 个错误:\n"
                + string.Join("\n", errors));
            return false;
        }

        /// <summary>
        /// 在 emitter 执行前为六个 required artifact 写入按 canonical path 派生的确定性 managed meta。
        /// </summary>
        private static void EnsureRequiredCandidateMetas(GasCodeGenContext context, bool autoChess)
        {
            var relativePaths = autoChess
                ? new[] { "AutoChessGeneratedConfig.gen.cs" }
                : new[]
                {
                    "Editor/LubanNormalizedRows.gen.cs",
                    "GasCodeGenValidationReport.md",
                    "Runtime/RuntimeAbilityActivation.gen.cs",
                    "Runtime/RuntimeActiveEffect.gen.cs",
                    "Runtime/RuntimeEffectInstant.gen.cs",
                };
            for (var index = 0; index < relativePaths.Length; index++)
            {
                var artifactPath = Path.GetFullPath(Path.Combine(context.OutputDir, relativePaths[index]));
                var directory = Path.GetDirectoryName(artifactPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var publishedPath = autoChess
                    ? "Assets/AutoChessDemo/Generated/" + relativePaths[index]
                    : "Assets/GAS/Generated/CodeGen/" + relativePaths[index];
                WriteDeterministicMetaIfMissing(artifactPath + ".meta", publishedPath);
            }
        }

        /// <summary>
        /// 以稳定 canonical path hash 派生 Unity GUID，避免空白 candidate 每轮产生随机 meta identity。
        /// </summary>
        private static void WriteDeterministicMetaIfMissing(string metaPath, string publishedPath)
        {
            if (File.Exists(metaPath))
                return;
            var identityBytes = Encoding.UTF8.GetBytes(
                "EX-GAS-ManagedMeta-v1\0" + publishedPath.Replace('\\', '/'));
            byte[] hash;
            using (var sha256 = SHA256.Create())
                hash = sha256.ComputeHash(identityBytes);
            var guid = new StringBuilder(32);
            for (var index = 0; index < 16; index++)
                guid.Append(hash[index].ToString("x2"));
            File.WriteAllText(
                metaPath,
                "fileFormatVersion: 2\n" + "guid: " + guid + "\n",
                new UTF8Encoding(false));
        }

        /// <summary>
        /// 将 emitter 产物统一为 UTF-8 no-BOM、LF-only，满足 selector source wire 的逐 byte 合约。
        /// </summary>
        private static void NormalizeRequiredArtifactText(GasCodeGenContext context, bool autoChess)
        {
            var relativePaths = autoChess
                ? new[] { "AutoChessGeneratedConfig.gen.cs" }
                : new[]
                {
                    "Editor/LubanNormalizedRows.gen.cs",
                    "GasCodeGenValidationReport.md",
                    "Runtime/RuntimeAbilityActivation.gen.cs",
                    "Runtime/RuntimeActiveEffect.gen.cs",
                    "Runtime/RuntimeEffectInstant.gen.cs",
                };
            for (var index = 0; index < relativePaths.Length; index++)
            {
                var path = Path.GetFullPath(Path.Combine(context.OutputDir, relativePaths[index]));
                var text = File.ReadAllText(path, new UTF8Encoding(false, true))
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n');
                File.WriteAllText(path, text, new UTF8Encoding(false, true));
            }
        }
    }
}
