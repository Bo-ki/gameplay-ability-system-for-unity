using System;
using System.Text;
using GAS.Runtime;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasRuntimeAccess
    {
        internal static bool TryRegisterRuntimeSystems()
        {
            if (!TryResolveSessionWorld(out var world))
                return false;

            return AutoChessRuntimeSystemBootstrap.RegisterSystems(world);
        }

        internal static bool TryInstallDefinitionCatalogSession()
        {
            if (!TryResolveRuntimeEntityManager(out var entityManager))
                return false;

            if (!AutoChessBattleDefinitionCatalogBuilder.Install(entityManager))
                return false;

            return true;
        }

        internal static void UninstallDefinitionCatalogSession()
        {
            if (!TryResolveRuntimeEntityManager(out var entityManager))
                return;

            AutoChessBattleDefinitionCatalogBuilder.Uninstall(entityManager);
        }

        internal static bool TryBeginOfficialToolDiffCapture(
            out AutoChessGasCoreOfficialToolDiffCapture capture)
        {
            capture = default;
            if (!TryResolveSessionWorld(out var world))
                return false;

            capture = new AutoChessGasCoreOfficialToolDiffCapture(
                GasRuntimeOfficialToolDiffCapture.Begin(world));
            return true;
        }

        /// <summary>
        /// 读取 Runtime v1 Session 的生命周期与 Stage-B 故障证据，供启动失败报告使用。
        /// </summary>
        internal static bool TryReadSessionObservation(out GasRuntimeSessionObservation observation)
        {
            var owner = AutoChessGasRuntimeHost.RuntimeOwner;
            if (owner == null)
            {
                observation = default;
                return false;
            }

            return owner.TryReadSessionObservation(out observation);
        }

        /// <summary>
        /// 从唯一 Runtime v1 managed BoundaryDrainRing 取出一批自包含事实，供 AutoChess 验证层读取。
        /// </summary>
        internal static bool TryDequeueBoundaryBatch(out GasBoundaryDrainBatch batch)
        {
            var ring = AutoChessGasRuntimeHost.RuntimeOwner?.BoundaryDrainRing;
            if (ring == null)
            {
                batch = default;
                return false;
            }

            return ring.TryDequeue(out batch);
        }

        /// <summary>
        /// 读取 Runtime v1 managed ring 的当前数量与固定容量，供观测层记录 high-water evidence。
        /// </summary>
        internal static bool TryReadBoundaryRingStats(out int count, out int capacity)
        {
            var ring = AutoChessGasRuntimeHost.RuntimeOwner?.BoundaryDrainRing;
            if (ring == null)
            {
                count = 0;
                capacity = 0;
                return false;
            }

            count = ring.Count;
            capacity = ring.Capacity;
            return true;
        }

        /// <summary>
        /// 读取 owner 最近一次 Boundary drain 结果，失败时保留显式 reconcile evidence。
        /// </summary>
        internal static bool TryReadBoundaryDrainFailure(out GasBoundaryDrainFailure failure)
        {
            var owner = AutoChessGasRuntimeHost.RuntimeOwner;
            if (owner == null)
            {
                failure = GasBoundaryDrainFailure.None;
                return false;
            }

            failure = owner.LastBoundaryDrainFailure;
            return true;
        }

        private static bool TryResolveSessionWorld(out World world)
        {
            return AutoChessGasRuntimeHost.TryResolveWorld(out world);
        }

        private static bool TryResolveRuntimeEntityManager(out EntityManager entityManager)
        {
            return AutoChessGasRuntimeHost.TryResolveEntityManager(out entityManager);
        }
    }

    internal enum AutoChessGasRuntimeAccessCapability
    {
        RuntimeSession = 0,
        DefinitionCatalogLifetime = 1,
        DiagnosticsSink = 2,
    }

    internal readonly struct AutoChessGasRuntimeAccessContractEntry
    {
        public readonly string MethodName;
        public readonly AutoChessGasRuntimeAccessCapability Capability;
        public readonly string TimingDomain;
        public readonly bool ProxiesEcsHandle;
        public readonly bool ManualSync;
        public readonly bool PerformancePassAllowed;
        public readonly bool AffectsBattleHash;
        public readonly string RequiredEvidence;
        public readonly string ReplacementOwner;
        public readonly string ExitTask;

        public AutoChessGasRuntimeAccessContractEntry(
            string methodName,
            AutoChessGasRuntimeAccessCapability capability,
            string timingDomain,
            bool proxiesEcsHandle,
            bool manualSync,
            bool performancePassAllowed,
            bool affectsBattleHash,
            string requiredEvidence,
            string replacementOwner,
            string exitTask)
        {
            MethodName = methodName ?? string.Empty;
            Capability = capability;
            TimingDomain = timingDomain ?? string.Empty;
            ProxiesEcsHandle = proxiesEcsHandle;
            ManualSync = manualSync;
            PerformancePassAllowed = performancePassAllowed;
            AffectsBattleHash = affectsBattleHash;
            RequiredEvidence = requiredEvidence ?? string.Empty;
            ReplacementOwner = replacementOwner ?? string.Empty;
            ExitTask = exitTask ?? string.Empty;
        }
    }

    internal static class AutoChessGasRuntimeAccessContract
    {
        private static readonly AutoChessGasRuntimeAccessContractEntry[] s_entries =
        {
            Entry(nameof(AutoChessGasRuntimeAccess.TryRegisterRuntimeSystems),
                AutoChessGasRuntimeAccessCapability.RuntimeSession,
                "bootstrap",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: false,
                "runtimeSystemRegistration",
                "RuntimeSession",
                "R1"),
            Entry(nameof(AutoChessGasRuntimeAccess.TryInstallDefinitionCatalogSession),
                AutoChessGasRuntimeAccessCapability.DefinitionCatalogLifetime,
                "bootstrap/catalog",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: true,
                "catalogInstallOwner",
                "DefinitionCatalogLifetime",
                "R5/R6"),
            Entry(nameof(AutoChessGasRuntimeAccess.UninstallDefinitionCatalogSession),
                AutoChessGasRuntimeAccessCapability.DefinitionCatalogLifetime,
                "shutdown/catalog",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: false,
                "catalogDisposeOwner",
                "DefinitionCatalogLifetime",
                "R5/R6"),
            Entry(nameof(AutoChessGasRuntimeAccess.TryBeginOfficialToolDiffCapture),
                AutoChessGasRuntimeAccessCapability.DiagnosticsSink,
                "official-diff-pass",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: false,
                "journalingCaptureState",
                "DiagnosticsSink",
                "R4"),
        };

        public static int EntryCount => s_entries.Length;

        public static int EcsHandleProxyCount => Count(entry => entry.ProxiesEcsHandle);

        public static int ManualSyncCount => Count(entry => entry.ManualSync);

        public static int PerformancePassRiskCount => Count(
            entry => entry.ManualSync || !entry.PerformancePassAllowed);

        public static int BattleHashAffectingCount => Count(entry => entry.AffectsBattleHash);

        public static int CapabilityMask
        {
            get
            {
                var mask = 0;
                for (var i = 0; i < s_entries.Length; i++)
                    mask |= 1 << (int)s_entries[i].Capability;
                return mask;
            }
        }

        public static string CreateSummary()
        {
            var builder = new StringBuilder(1024);
            builder.Append("entries=").Append(EntryCount)
                .Append(", ecsHandleProxies=").Append(EcsHandleProxyCount)
                .Append(", manualSync=").Append(ManualSyncCount)
                .Append(", performancePassRisks=").Append(PerformancePassRiskCount)
                .Append(", battleHashAffecting=").Append(BattleHashAffectingCount)
                .Append(", capabilityMask=0x").Append(CapabilityMask.ToString("X"));

            for (var i = 0; i < s_entries.Length; i++)
            {
                var entry = s_entries[i];
                builder.Append(", access[").Append(i).Append("]=")
                    .Append(entry.MethodName).Append("/")
                    .Append(entry.Capability).Append("/")
                    .Append(entry.TimingDomain).Append("/")
                    .Append("ecs=").Append(entry.ProxiesEcsHandle ? "true" : "false").Append("/")
                    .Append("sync=").Append(entry.ManualSync ? "true" : "false").Append("/")
                    .Append("perf=").Append(entry.PerformancePassAllowed ? "true" : "false").Append("/")
                    .Append("hash=").Append(entry.AffectsBattleHash ? "true" : "false").Append("/")
                    .Append("evidence=").Append(entry.RequiredEvidence).Append("/")
                    .Append("owner=").Append(entry.ReplacementOwner).Append("/")
                    .Append("exit=").Append(entry.ExitTask);
            }

            return builder.ToString();
        }

        private static AutoChessGasRuntimeAccessContractEntry Entry(
            string methodName,
            AutoChessGasRuntimeAccessCapability capability,
            string timingDomain,
            bool proxiesEcsHandle,
            bool manualSync,
            bool performancePassAllowed,
            bool affectsBattleHash,
            string requiredEvidence,
            string replacementOwner,
            string exitTask)
        {
            return new AutoChessGasRuntimeAccessContractEntry(
                methodName,
                capability,
                timingDomain,
                proxiesEcsHandle,
                manualSync,
                performancePassAllowed,
                affectsBattleHash,
                requiredEvidence,
                replacementOwner,
                exitTask);
        }

        private static int Count(Predicate<AutoChessGasRuntimeAccessContractEntry> predicate)
        {
            var count = 0;
            for (var i = 0; i < s_entries.Length; i++)
            {
                if (predicate(s_entries[i]))
                    count++;
            }

            return count;
        }
    }
}
