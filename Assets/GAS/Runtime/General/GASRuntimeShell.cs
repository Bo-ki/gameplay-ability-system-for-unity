using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// Thin OOP shell entry for external gameplay code that needs to talk to the ECS runtime.
    /// </summary>
    public static class GASRuntimeShell
    {
        internal static bool TryGetRuntimeWorld(out World world)
        {
            return TryResolveRuntimeWorld(out world);
        }

        internal static bool TryResolveRuntimeWorld(out World world)
        {
            world = null;
            if (!GASManager.IsInitialized
                || GASManager.ExWorld == null
                || !GASManager.ExWorld.IsCreated)
            {
                return false;
            }

            world = GASManager.ExWorld;
            return true;
        }

        internal static bool TryGetRuntimeEntityManager(out EntityManager entityManager)
        {
            return TryResolveRuntimeEntityManager(out entityManager);
        }

        internal static bool TryResolveRuntimeEntityManager(out EntityManager entityManager)
        {
            entityManager = default;
            if (!GASManager.IsInitialized)
                return false;

            var runtimeEntityManager = GASManager.EntityManager;
            if (!IsRuntimeReady(runtimeEntityManager))
                return false;

            entityManager = runtimeEntityManager;
            return true;
        }

        public static bool TryCreateASCCommandPort(out ASCCommandPort commandPort)
        {
            if (!TryResolveRuntimeEntityManager(out var entityManager))
            {
                commandPort = default;
                return false;
            }

            commandPort = ASCCommandPort.Create(entityManager);
            return commandPort.IsValid;
        }

        internal static bool TryCreateASCCommandPort(
            out EntityManager entityManager,
            out ASCCommandPort commandPort)
        {
            commandPort = default;
            if (!TryResolveRuntimeEntityManager(out entityManager))
                return false;

            commandPort = ASCCommandPort.Create(entityManager);
            return commandPort.IsValid;
        }

        internal static bool TryCreateASCCommandPort(
            ComponentType additionalComponent,
            out ASCCommandPort commandPort)
        {
            commandPort = default;
            if (!TryResolveRuntimeEntityManager(out var entityManager))
                return false;

            var archetype = GASRuntimeEntityArchetypes.ASC(entityManager, additionalComponent);
            commandPort = ASCCommandPort.Create(entityManager, archetype);
            return commandPort.IsValid;
        }

        internal static bool TryCreateASCCommandPort(
            ASCHandle handle,
            out ASCCommandPort commandPort)
        {
            commandPort = default;
            if (!TryResolveASC(handle, out var entityManager, out var asc))
                return false;

            commandPort = new ASCCommandPort(entityManager, asc);
            return commandPort.IsValid;
        }

        internal static bool TryCreateASCCommandPort(
            ASCHandle handle,
            out EntityManager entityManager,
            out ASCCommandPort commandPort)
        {
            entityManager = default;
            commandPort = default;
            if (!handle.IsValid)
                return false;

            if (!TryResolveRuntimeEntityManager(out entityManager))
                return false;

            var asc = handle.RuntimeEntity;
            if (asc == Entity.Null || !entityManager.Exists(asc))
                return false;

            commandPort = new ASCCommandPort(entityManager, asc);
            return commandPort.IsValid;
        }

        public static bool TryCaptureASCReadModel(ASCHandle handle, out ASCReadModel readModel)
        {
            readModel = default;
            if (!handle.IsValid || !TryResolveRuntimeEntityManager(out var entityManager))
                return false;

            readModel = new ASCReadModel(entityManager, handle);
            return readModel.IsReadable;
        }

        public static ASCReadModel CaptureASCReadModel(ASCHandle handle)
        {
            return TryCaptureASCReadModel(handle, out var readModel)
                ? readModel
                : default;
        }

        internal static bool TryCompleteRuntimeJobs()
        {
            return TryDrainRuntimeJobs();
        }

        internal static bool TryDrainRuntimeJobs()
        {
            if (!TryResolveRuntimeEntityManager(out var entityManager))
                return false;

            entityManager.CompleteAllTrackedJobs();
            return true;
        }

        internal static bool TryBindPresentation(ASCHandle handle, GameObject gameObject)
        {
            if (!TryResolveASC(handle, out var entityManager, out var asc))
                return false;

            PresentationEntityBindingRegistry.BindGameObjectToEntity(entityManager, asc, gameObject);
            return true;
        }

        internal static bool TryUnbindPresentation(ASCHandle handle)
        {
            if (!TryResolveASC(handle, out var entityManager, out var asc))
                return false;

            PresentationEntityBindingRegistry.UnbindGameObjectToEntity(entityManager, asc);
            return true;
        }

        internal static bool TryGetGlobalTimer(
            out EntityManager entityManager,
            out Entity globalTimer)
        {
            return TryResolveGlobalTimer(out entityManager, out globalTimer);
        }

        internal static bool TryResolveGlobalTimer(
            out EntityManager entityManager,
            out Entity globalTimer)
        {
            return TryResolveRuntimeSingleton(
                GASManager.EntityGlobalTimer,
                out entityManager,
                out globalTimer);
        }

        internal static bool TryGetEventBus(
            out EntityManager entityManager,
            out Entity eventBus)
        {
            return TryResolveEventBus(out entityManager, out eventBus);
        }

        internal static bool TryResolveEventBus(
            out EntityManager entityManager,
            out Entity eventBus)
        {
            return TryResolveRuntimeSingleton(
                GASManager.EntityEventBus,
                out entityManager,
                out eventBus);
        }

        internal static bool TryGetEventLogSink(
            out EntityManager entityManager,
            out Entity eventLogSink)
        {
            return TryResolveEventLogSink(out entityManager, out eventLogSink);
        }

        internal static bool TryResolveEventLogSink(
            out EntityManager entityManager,
            out Entity eventLogSink)
        {
            return TryResolveRuntimeSingleton(
                GASManager.EntityEventLogSink,
                out entityManager,
                out eventLogSink);
        }

        internal static bool TryGetRuntimeDebugger(
            out EntityManager entityManager,
            out Entity runtimeDebugger)
        {
            return TryResolveRuntimeDebugger(out entityManager, out runtimeDebugger);
        }

        internal static bool TryResolveRuntimeDebugger(
            out EntityManager entityManager,
            out Entity runtimeDebugger)
        {
            return TryResolveRuntimeSingleton(
                GASManager.EntityRuntimeDebugger,
                out entityManager,
                out runtimeDebugger);
        }

        private static bool TryResolveRuntimeSingleton(
            Entity singleton,
            out EntityManager entityManager,
            out Entity entity)
        {
            entityManager = default;
            entity = Entity.Null;
            if (!TryResolveRuntimeEntityManager(out var runtimeEntityManager)
                || singleton == Entity.Null
                || !runtimeEntityManager.Exists(singleton))
            {
                return false;
            }

            entityManager = runtimeEntityManager;
            entity = singleton;
            return true;
        }

        internal static bool TryResolveASC(
            ASCHandle handle,
            out EntityManager entityManager,
            out Entity asc)
        {
            entityManager = default;
            asc = Entity.Null;
            if (!handle.IsValid
                || !TryResolveRuntimeEntityManager(out var runtimeEntityManager)
                || !handle.TryResolveRuntimeEntity(out var runtimeAsc)
                || runtimeAsc == Entity.Null
                || !runtimeEntityManager.Exists(runtimeAsc))
            {
                return false;
            }

            entityManager = runtimeEntityManager;
            asc = runtimeAsc;
            return true;
        }

        private static bool IsRuntimeReady(EntityManager entityManager)
        {
            return entityManager.World != null && entityManager.World.IsCreated;
        }
    }
}
