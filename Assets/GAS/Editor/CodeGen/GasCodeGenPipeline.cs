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
            if (context.RowTypes.Count == 0)
            {
                Debug.LogWarning("[GasCodeGenPipeline] 未找到任何 *DefinitionRow 类型。请先生成 Luban DefinitionRow。");
                return;
            }

            var manifest = new GasCodeGenManifest(context.ProjectRoot, context.OutputDir, context.InputHash);
            var errors = new List<string>();
            var orphanCleanupRan = false;

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
                    Debug.Log($"[GasCodeGenPipeline] {phase.PhaseName} 完成: {string.Join(", ", phase.OutputFileNames)}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{phase.PhaseName}: {ex.Message}");
                    Debug.LogException(ex);
                }
            }

            if (!orphanCleanupRan)
                context.OrphansDeleted = manifest.DeleteOrphanedFiles();

            manifest.Save();
            AssetDatabase.Refresh();

            if (errors.Count > 0)
                Debug.LogError($"[GasCodeGenPipeline] {errors.Count} 个 Phase 失败:\n{string.Join("\n", errors)}");
            else
                Debug.Log($"[GasCodeGenPipeline] 全部完成。Rows={context.Rows.Count}, Phases={phaseList.Count}, OrphansDeleted={context.OrphansDeleted}");
        }
    }
}
