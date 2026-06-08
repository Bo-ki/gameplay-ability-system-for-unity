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

            AutoChessGasRuntimeAccess.TryRegisterRuntimeSystems();
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
