using GAS.Runtime;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasRuntimeAccess
    {
        internal static bool TryResolveSessionWorld(out World world)
        {
            return GASRuntimeShell.TryResolveRuntimeWorld(out world);
        }

        internal static bool TryResolveDefinitionEntityManager(out EntityManager entityManager)
        {
            return GASRuntimeShell.TryResolveRuntimeEntityManager(out entityManager);
        }

        internal static bool TryResolveBattleLifecycleEntityManager(out EntityManager entityManager)
        {
            return GASRuntimeShell.TryResolveRuntimeEntityManager(out entityManager);
        }

        internal static bool TryResolveDiagnosticsWorld(out World world)
        {
            return GASRuntimeShell.TryResolveRuntimeWorld(out world);
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
    }
}
