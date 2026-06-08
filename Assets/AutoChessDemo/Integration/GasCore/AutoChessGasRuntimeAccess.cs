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
}
