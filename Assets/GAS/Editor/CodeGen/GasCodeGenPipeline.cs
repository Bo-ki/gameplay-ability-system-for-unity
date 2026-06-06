using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Debug = UnityEngine.Debug;

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
            new ValidationReportPhase(),
        };

        public static void RunAll()
        {
            Run(s_corePhases);
        }

        internal static void Run(IEnumerable<IGasCodeGenPhase> phases)
        {
            var phaseList = phases as IReadOnlyList<IGasCodeGenPhase> ?? phases.ToArray();
            var context = GasCodeGenContext.Create(true);
            var hasRows = context.RowTypes.Count > 0;
            if (!hasRows)
                Debug.LogWarning("[GasCodeGenPipeline] 未找到任何 *DefinitionRow 类型。将执行不依赖 RowMetadata 的独立 Phase，并跳过 Row 驱动 Phase。");

            var manifest = new GasCodeGenManifest(context.ProjectRoot, context.OutputDir, context.InputHash);
            var errors = new List<string>();
            var orphanCleanupRan = false;
            var executedCount = 0;
            var skippedCount = 0;

            foreach (var phase in phaseList)
            {
                if (!hasRows && phase.RequiresRows)
                {
                    skippedCount++;
                    Debug.LogWarning($"[GasCodeGenPipeline] 跳过 {phase.PhaseName}: 缺少 RowMetadata。");
                    continue;
                }

                try
                {
                    if (hasRows && !orphanCleanupRan && phase.PhaseName == "ValidationReport")
                    {
                        context.OrphansDeleted = manifest.DeleteOrphanedFiles();
                        orphanCleanupRan = true;
                    }

                    phase.Execute(context, manifest);
                    executedCount++;
                    Debug.Log($"[GasCodeGenPipeline] {phase.PhaseName} 完成: {string.Join(", ", phase.OutputFileNames)}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{phase.PhaseName}: {ex.Message}");
                    Debug.LogException(ex);
                }
            }

            if (hasRows && !orphanCleanupRan)
                context.OrphansDeleted = manifest.DeleteOrphanedFiles();

            if (hasRows)
                manifest.Save();
            AssetDatabase.Refresh();

            if (errors.Count > 0)
                Debug.LogError($"[GasCodeGenPipeline] {errors.Count} 个 Phase 失败:\n{string.Join("\n", errors)}");
            else if (!hasRows)
                Debug.Log($"[GasCodeGenPipeline] Partial generation 完成。Rows=0, Executed={executedCount}, Skipped={skippedCount}, ManifestSaved=False, OrphanCleanup=False");
            else
                Debug.Log($"[GasCodeGenPipeline] 全部完成。Rows={context.Rows.Count}, Phases={phaseList.Count}, OrphansDeleted={context.OrphansDeleted}");
        }
    }
}
