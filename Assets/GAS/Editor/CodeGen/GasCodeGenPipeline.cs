using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GAS.Editor
{
    public static class GasCodeGenPipeline
    {
        private static readonly IGasCodeGenPhase[] s_corePhases =
        {
            new AssemblyDefinitionPhase(),
            new DefinitionIndexPhase(),
            new BlobSchemaPhase(),
            new StaticLookupPhase(),
            new DefinitionCatalogPhase(),
            new RuntimeDefinitionGluePhase(),
            new RuntimeLifecycleMigrationPhase(),
            new BakerGluePhase(),
            new ComponentTypeSetPhase(),
            new QueryLayoutPhase(),
            new ValidationReportPhase(),
        };

        private static readonly IGasCodeGenPhase[] s_autoChessDemoPhases =
        {
            new AutoChessDemoConfigPhase(),
        };

        public static void RunAll()
        {
            TryRunAll();
        }

        public static bool TryRunAll()
        {
            return TryRunAll(refreshAssetDatabase: true);
        }

        public static bool TryRunAll(bool refreshAssetDatabase)
        {
            var coreSuccess = TryRunCore(refreshAssetDatabase: false);
            var demoSuccess = coreSuccess && TryRunAutoChessDemo(refreshAssetDatabase: false);

            if (refreshAssetDatabase)
                GasCodeGenEnvironment.RefreshAssetDatabase();

            return coreSuccess && demoSuccess;
        }

        public static bool TryRunCore(bool refreshAssetDatabase)
        {
            return Run(s_corePhases, refreshAssetDatabase);
        }

        public static bool TryRunAutoChessDemo(bool refreshAssetDatabase)
        {
            return RunStandalone(
                s_autoChessDemoPhases,
                Path.Combine(GasCodeGenEnvironment.ProjectRoot, "Assets", "AutoChessDemo", "Generated"),
                refreshAssetDatabase);
        }

        internal static bool Run(IEnumerable<IGasCodeGenPhase> phases, bool refreshAssetDatabase = true)
        {
            var phaseList = phases as IReadOnlyList<IGasCodeGenPhase> ?? phases.ToArray();
            var lubanRowsPath = LubanNormalizedRowBootstrap.Generate(CreateSettings());
            var context = GasCodeGenContext.Create(true);
            var hasRows = context.RowTypes.Count > 0;
            if (!hasRows)
                throw new InvalidOperationException("[GasCodeGenPipeline] 未找到任何 Definition Row。Luban / SourceGenerator 输入 gate 失败，禁止 partial generation 输出过期 artifact。");

            var manifest = new GasCodeGenManifest(context.ProjectRoot, context.OutputDir, context.InputHash);
            manifest.AddGeneratedFile("LubanNormalizedRows", lubanRowsPath, "Editor", false);
            var errors = new List<string>();
            var orphanCleanupRan = false;
            var executedCount = 0;

            foreach (var phase in phaseList)
            {
                try
                {
                    if (!orphanCleanupRan && phase.PhaseName == "ValidationReport")
                    {
                        PreRegisterPhaseOutputs(context, manifest, phase, "Editor/CI", false);
                        context.OrphansDeleted = manifest.DeleteOrphanedFiles();
                        orphanCleanupRan = true;
                    }

                    phase.Execute(context, manifest);
                    executedCount++;
                    GasCodeGenEnvironment.Log($"[GasCodeGenPipeline] {phase.PhaseName} 完成: {string.Join(", ", phase.OutputFileNames)}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{phase.PhaseName}: {ex.Message}");
                    GasCodeGenEnvironment.LogException(ex);
                }
            }

            if (!orphanCleanupRan)
                context.OrphansDeleted = manifest.DeleteOrphanedFiles();

            manifest.Save();
            if (refreshAssetDatabase)
                GasCodeGenEnvironment.RefreshAssetDatabase();

            if (errors.Count > 0)
            {
                GasCodeGenEnvironment.LogError($"[GasCodeGenPipeline] {errors.Count} 个 Phase 失败:\n{string.Join("\n", errors)}");
                return false;
            }
            else
            {
                GasCodeGenEnvironment.Log($"[GasCodeGenPipeline] 全部完成。Rows={context.Rows.Count}, Phases={phaseList.Count}, OrphansDeleted={context.OrphansDeleted}");
                return true;
            }
        }

        private static bool RunStandalone(
            IEnumerable<IGasCodeGenPhase> phases,
            string manifestOutputRoot,
            bool refreshAssetDatabase)
        {
            var phaseList = phases as IReadOnlyList<IGasCodeGenPhase> ?? phases.ToArray();
            var context = GasCodeGenContext.Create(true);
            var manifest = new GasCodeGenManifest(context.ProjectRoot, manifestOutputRoot, context.InputHash);
            var errors = new List<string>();
            var executedCount = 0;

            foreach (var phase in phaseList)
            {
                try
                {
                    phase.Execute(context, manifest);
                    executedCount++;
                    GasCodeGenEnvironment.Log($"[GasCodeGenPipeline] {phase.PhaseName} 完成: {string.Join(", ", phase.OutputFileNames)}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{phase.PhaseName}: {ex.Message}");
                    GasCodeGenEnvironment.LogException(ex);
                }
            }

            if (errors.Count == 0)
                context.OrphansDeleted = manifest.DeleteOrphanedFiles();

            manifest.Save();
            if (refreshAssetDatabase)
                GasCodeGenEnvironment.RefreshAssetDatabase();

            if (errors.Count > 0)
            {
                GasCodeGenEnvironment.LogError($"[GasCodeGenPipeline] {errors.Count} 个独立 Phase 失败:\n{string.Join("\n", errors)}");
                return false;
            }

            GasCodeGenEnvironment.Log($"[GasCodeGenPipeline] 独立 Phase 完成。Phases={executedCount}, OrphansDeleted={context.OrphansDeleted}");
            return true;
        }

        private static GasCodeGenSettings CreateSettings()
        {
            if (GasCodeGenEnvironment.IsOffline)
                return GasCodeGenSettings.CreateDefault();

#if UNITY_EDITOR
            return GasCodeGenSettings.From(GASSettingAsset.LoadOrCreate());
#else
            return GasCodeGenSettings.CreateDefault();
#endif
        }

        private static void PreRegisterPhaseOutputs(
            GasCodeGenContext context,
            GasCodeGenManifest manifest,
            IGasCodeGenPhase phase,
            string layer,
            bool runtimeVisible)
        {
            for (var i = 0; i < phase.OutputFileNames.Count; i++)
            {
                manifest.AddGeneratedFile(
                    phase.PhaseName,
                    Path.Combine(context.OutputDir, phase.OutputFileNames[i]),
                    layer,
                    runtimeVisible);
            }
        }
    }
}
