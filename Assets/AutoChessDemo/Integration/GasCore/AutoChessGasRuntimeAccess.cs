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

            AutoChessRuntimeSystemBootstrap.RegisterSystems(world);
            return true;
        }

        internal static bool TryCreateRuntimeTickGroups(out AutoChessGasRuntimeTickGroups groups)
        {
            groups = default;
            if (!TryResolveSessionWorld(out var world))
                return false;

            var framePrepare = world.GetExistingSystemManaged<GASFramePrepareSystemGroup>();
            var commandResolve = world.GetExistingSystemManaged<GASCommandResolveSystemGroup>();
            var coreSimulation = world.GetExistingSystemManaged<GASCoreSimulationSystemGroup>();
            var structuralCommit = world.GetExistingSystemManaged<GASStructuralCommitSystemGroup>();
            var boundaryProjection = world.GetExistingSystemManaged<GASBoundaryProjectionSystemGroup>();
            if (framePrepare == null
                || commandResolve == null
                || coreSimulation == null
                || structuralCommit == null
                || boundaryProjection == null)
            {
                return false;
            }

            groups = new AutoChessGasRuntimeTickGroups(
                framePrepare,
                commandResolve,
                coreSimulation,
                structuralCommit,
                boundaryProjection);
            return true;
        }

        internal static bool TryInstallDefinitionCatalogSession()
        {
            if (!TryResolveRuntimeEntityManager(out var entityManager))
                return false;

            AutoChessBattleDefinitionCatalogBuilder.Install(entityManager);
            AutoChessBattleDriverRuntimeStore.Ensure(entityManager);
            return true;
        }

        internal static void UninstallDefinitionCatalogSession()
        {
            if (!TryResolveRuntimeEntityManager(out var entityManager))
                return;

            AutoChessBattleDriverRuntimeStore.Uninstall(entityManager);
            AutoChessBattleDefinitionCatalogBuilder.Uninstall(entityManager);
        }

        internal static bool TryCreateBattleDriver(out AutoChessGasBattleDriverHandle driverHandle)
        {
            driverHandle = default;
            if (!TryResolveRuntimeEntityManager(out var entityManager))
                return false;

            driverHandle = AutoChessBattleDriverRuntimeStore.ResetAndEnable(entityManager);
            return driverHandle.IsValid;
        }

        internal static AutoChessBattleDriverComponent ReadBattleDriver(
            AutoChessGasBattleDriverHandle driverHandle)
        {
            return TryResolveRuntimeEntityManager(out var entityManager)
                ? AutoChessBattleDriverRuntimeStore.Read(entityManager, driverHandle)
                : default;
        }

        internal static AutoChessBattleDriverOwnerSnapshot CreateBattleDriverOwnerSnapshot(
            AutoChessGasBattleDriverHandle driverHandle)
        {
            return TryResolveRuntimeEntityManager(out var entityManager)
                ? AutoChessBattleDriverRuntimeStore.CreateOwnerSnapshot(entityManager, driverHandle)
                : default;
        }

        internal static void DisableBattleDriver(AutoChessGasBattleDriverHandle driverHandle)
        {
            if (!TryResolveRuntimeEntityManager(out var entityManager))
                return;

            AutoChessBattleDriverRuntimeStore.Disable(entityManager, driverHandle);
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

        internal static bool TryResolveDiagnosticsGlobalTimer(
            out EntityManager entityManager,
            out Entity globalTimer)
        {
            return GASRuntimeShell.TryResolveGlobalTimer(out entityManager, out globalTimer);
        }

        internal static bool TryResolveDiagnosticsEventBus(
            out EntityManager entityManager,
            out Entity eventBus)
        {
            return GASRuntimeShell.TryResolveEventBus(out entityManager, out eventBus);
        }

        internal static bool TryResolveDiagnosticsEventLogSink(
            out EntityManager entityManager,
            out Entity eventLogSink)
        {
            return GASRuntimeShell.TryResolveEventLogSink(out entityManager, out eventLogSink);
        }

        internal static bool TryResolveDiagnosticsRuntimeDebugger(
            out EntityManager entityManager,
            out Entity runtimeDebugger)
        {
            return GASRuntimeShell.TryResolveRuntimeDebugger(out entityManager, out runtimeDebugger);
        }

        internal static bool TryCreateBattleUnitCommandPort(
            ComponentType additionalComponent,
            out ASCCommandPort commandPort)
        {
            return GASRuntimeShell.TryCreateASCCommandPort(additionalComponent, out commandPort);
        }

        internal static bool TryCreateBattleUnitCommandPort(
            ASCHandle handle,
            out ASCCommandPort commandPort)
        {
            return GASRuntimeShell.TryCreateASCCommandPort(handle, out commandPort);
        }

        internal static bool TryDrainRunnerJobs()
        {
            return GASRuntimeShell.TryDrainRuntimeJobs();
        }

        private static bool TryResolveSessionWorld(out World world)
        {
            return GASRuntimeShell.TryResolveRuntimeWorld(out world);
        }

        private static bool TryResolveRuntimeEntityManager(out EntityManager entityManager)
        {
            return GASRuntimeShell.TryResolveRuntimeEntityManager(out entityManager);
        }
    }

    internal enum AutoChessGasRuntimeAccessCapability
    {
        RuntimeSession = 0,
        DefinitionCatalogLifetime = 1,
        CommandPort = 2,
        DiagnosticsSink = 3,
        RunnerSync = 4,
        DriverLifecycle = 5,
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
            Entry(nameof(AutoChessGasRuntimeAccess.TryCreateRuntimeTickGroups),
                AutoChessGasRuntimeAccessCapability.RuntimeSession,
                "bootstrap",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: false,
                "physicalGroupAvailability",
                "RuntimeSession",
                "R1/R4"),
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
            Entry(nameof(AutoChessGasRuntimeAccess.TryCreateBattleDriver),
                AutoChessGasRuntimeAccessCapability.DriverLifecycle,
                "battle-bootstrap",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: true,
                "driverOwnerSnapshot",
                "BoundaryStructuralOwner",
                "R6"),
            Entry(nameof(AutoChessGasRuntimeAccess.ReadBattleDriver),
                AutoChessGasRuntimeAccessCapability.DriverLifecycle,
                "snapshot-read",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: false,
                "driverOwnerSnapshot",
                "SnapshotReadModel",
                "R6"),
            Entry(nameof(AutoChessGasRuntimeAccess.CreateBattleDriverOwnerSnapshot),
                AutoChessGasRuntimeAccessCapability.DriverLifecycle,
                "diagnostic-snapshot",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: false,
                "driverOwnerSnapshot",
                "DiagnosticsSink",
                "R6"),
            Entry(nameof(AutoChessGasRuntimeAccess.DisableBattleDriver),
                AutoChessGasRuntimeAccessCapability.DriverLifecycle,
                "shutdown",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: true,
                "driverDisableRequest",
                "BoundaryStructuralOwner",
                "R6"),
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
            Entry(nameof(AutoChessGasRuntimeAccess.TryResolveDiagnosticsGlobalTimer),
                AutoChessGasRuntimeAccessCapability.DiagnosticsSink,
                "diagnostic-reset",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: false,
                "diagnosticResetOwner",
                "DiagnosticsSink",
                "R4/R6"),
            Entry(nameof(AutoChessGasRuntimeAccess.TryResolveDiagnosticsEventBus),
                AutoChessGasRuntimeAccessCapability.DiagnosticsSink,
                "diagnostic-reset",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: false,
                "observationCarrierReset",
                "DiagnosticsSink",
                "R4/R6"),
            Entry(nameof(AutoChessGasRuntimeAccess.TryResolveDiagnosticsEventLogSink),
                AutoChessGasRuntimeAccessCapability.DiagnosticsSink,
                "diagnostic-export",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: false,
                "structuredLogSnapshot",
                "DiagnosticsSink",
                "R4/R6"),
            Entry(nameof(AutoChessGasRuntimeAccess.TryResolveDiagnosticsRuntimeDebugger),
                AutoChessGasRuntimeAccessCapability.DiagnosticsSink,
                "diagnostic-export",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: false,
                "runtimeDiagnosticsSnapshot",
                "DiagnosticsSink",
                "R4/R6"),
            Entry(nameof(AutoChessGasRuntimeAccess.TryCreateBattleUnitCommandPort),
                AutoChessGasRuntimeAccessCapability.CommandPort,
                "boundary-command",
                proxiesEcsHandle: true,
                manualSync: false,
                performancePassAllowed: false,
                affectsBattleHash: true,
                "ownerLocalCommandBuffer",
                "CommandPort",
                "R1/R6"),
            Entry(nameof(AutoChessGasRuntimeAccess.TryDrainRunnerJobs),
                AutoChessGasRuntimeAccessCapability.RunnerSync,
                "runner-sync",
                proxiesEcsHandle: true,
                manualSync: true,
                performancePassAllowed: true,
                affectsBattleHash: false,
                "dependencyDrainTiming",
                "RunnerSync",
                "R4/R6"),
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
