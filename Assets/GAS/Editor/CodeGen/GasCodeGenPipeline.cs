using System;
using System.Collections.Generic;
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
            new BakerGluePhase(),
            new ComponentTypeSetPhase(),
            new QueryLayoutPhase(),
            new AutoChessDemoConfigPhase(),
            new ValidationReportPhase(),
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
            return Run(s_corePhases, refreshAssetDatabase);
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
    }
}
