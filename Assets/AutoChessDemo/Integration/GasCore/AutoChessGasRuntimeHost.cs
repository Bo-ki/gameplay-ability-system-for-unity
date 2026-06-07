using GAS.Runtime;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasRuntimeHost
    {
        public static void EnsureRuntimeInitialized()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize(attachToPlayerLoop: false);

            if (GASRuntimeShell.TryResolveRuntimeWorld(out var world))
                AutoChessRuntimeSystemBootstrap.RegisterSystems(world);

            AutoChessGasCatalogSession.TryInstall();
        }

        public static void ShutdownRuntime()
        {
            if (!GASManager.IsInitialized)
                return;

            AutoChessGasCatalogSession.Uninstall();

            AutoChessRuntimeSystemBootstrap.Reset();
            AutoChessGasBattleEntityLifecycle.ResetRuntimeCache();
            GASManager.Shutdown();
        }

        public static bool TryGetRuntimeTickGroups(out AutoChessGasRuntimeTickGroups groups)
        {
            groups = default;
            if (!GASRuntimeShell.TryResolveRuntimeWorld(out var world))
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

        public static bool TryCompleteRuntimeJobs()
        {
            return GASRuntimeShell.TryDrainRuntimeJobs();
        }
    }

    internal readonly struct AutoChessGasRuntimeTickGroups
    {
        public AutoChessGasRuntimeTickGroups(
            ComponentSystemGroup framePrepare,
            ComponentSystemGroup commandResolve,
            ComponentSystemGroup coreSimulation,
            ComponentSystemGroup structuralCommit,
            ComponentSystemGroup boundaryProjection)
        {
            FramePrepare = framePrepare;
            CommandResolve = commandResolve;
            CoreSimulation = coreSimulation;
            StructuralCommit = structuralCommit;
            BoundaryProjection = boundaryProjection;
        }

        public ComponentSystemGroup FramePrepare { get; }

        public ComponentSystemGroup CommandResolve { get; }

        public ComponentSystemGroup CoreSimulation { get; }

        public ComponentSystemGroup StructuralCommit { get; }

        public ComponentSystemGroup BoundaryProjection { get; }
    }
}
