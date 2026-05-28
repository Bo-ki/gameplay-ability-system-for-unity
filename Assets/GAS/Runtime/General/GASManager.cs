using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    public static class GASManager
    {
        private const int GameplayEventCapacity = 1024;
        private const int AttributeChangeEventCapacity = 512;
        private const int CueRequestCapacity = 512;
        private const int DamageEventCapacity = 256;
        private const int TagChangeEventCapacity = 256;
        private const int DebugReplayEventCapacity = 8192;
        private const int PresentationOutboxOwnerCapacity = 512;

        public static World ExWorld { get; private set; }
        public static EntityManager EntityManager { get; private set; }

        public static TurnController TurnController { get; private set; }

        public static bool IsRunning { get; private set; }

        public static bool IsInitialized { get; private set; }

        public static Entity EntityGlobalTimer { get; private set; }

        public static Entity EntityEffectCommandSpecStream { get; private set; }

        public static Entity EntityActiveEffectGlobalIndex { get; private set; }

        public static GlobalTimer GetGlobalTimer()
        {
            return EntityManager.GetComponentData<GlobalTimer>(EntityGlobalTimer);
        }

        public static int CurrentFrame => EntityManager.GetComponentData<GlobalTimer>(EntityGlobalTimer).Frame;

        public static int CurrentTurn => EntityManager.GetComponentData<GlobalTimer>(EntityGlobalTimer).Turn;

        public static void Initialize()
        {
            if (IsInitialized)
            {
#if UNITY_EDITOR
                Debug.LogWarning("EX-GAS has been initialized.Don't reinitialize.");
#endif
                return;
            }

            TurnController ??= new TurnController();
            ExWorld = new World("EX_GAS_World");
            EntityManager = ExWorld.EntityManager;
            CreateSystems();
            EntityGlobalTimer = ExWorld.EntityManager.CreateEntity(GASRuntimeEntityArchetypes.GlobalTimer(EntityManager));
            ExWorld.EntityManager.SetName(EntityGlobalTimer, "GAS_GlobalTimer");
            EntityEffectCommandSpecStream = EffectCommandSpecStream.EnsureSingleton(EntityManager);
            EntityActiveEffectGlobalIndex = ActiveEffectStore.EnsureGlobalIndexStore(EntityManager);
            CreateEventBusSingleton();
            CreateEventLogSinkSingleton();
            CreateRuntimeDebuggerSingleton();
            IsInitialized = true;
        }

        public static void Run()
        {
            IsRunning = true;
        }

        public static void Stop()
        {
            IsRunning = false;
        }

        private static void CreateSystems()
        {
            // 基础系统组
            ExWorld.CreateSystemManaged<InitializationSystemGroup>();
            var sgSimulation = ExWorld.CreateSystemManaged<SimulationSystemGroup>();
            ExWorld.CreateSystemManaged<PresentationSystemGroup>();
            var sgFixedStepSimulation = ExWorld.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            sgFixedStepSimulation.RateManager = new RateUtils.FixedRateSimpleManager(Time.fixedDeltaTime);
            sgSimulation.AddSystemToUpdateList(sgFixedStepSimulation);

            var gasGroups = GASSystemScheduleContract.CreateFixedStepGroups(ExWorld, sgFixedStepSimulation);
            GASSystemScheduleContract.RegisterSystems(ExWorld, gasGroups);
            GASSystemScheduleContract.SortSystems(sgFixedStepSimulation, gasGroups);

            // 同步到 PlayerLoop
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(ExWorld);
        }

        public static Entity EntityEventBus { get; private set; }

        public static Entity EntityEventLogSink { get; private set; }

        public static Entity EntityRuntimeDebugger { get; private set; }

        private static void CreateEventBusSingleton()
        {
            EntityEventBus = ExWorld.EntityManager.CreateEntity(GASRuntimeEntityArchetypes.GameplayEventBus(EntityManager));
            ExWorld.EntityManager.SetComponentData(EntityEventBus, new PresentationOutboxProjectionOptionsComponent
            {
                ProjectRawFacts = 1,
            });
            ExWorld.EntityManager.GetBuffer<DamageEventBuffer>(EntityEventBus).EnsureCapacity(DamageEventCapacity);
            ExWorld.EntityManager.GetBuffer<TagChangeEventBuffer>(EntityEventBus).EnsureCapacity(TagChangeEventCapacity);
            ExWorld.EntityManager.GetBuffer<GameplayEventBusEventBuffer>(EntityEventBus).EnsureCapacity(GameplayEventCapacity);
            ExWorld.EntityManager.GetBuffer<AttributeChangeEventBuffer>(EntityEventBus).EnsureCapacity(AttributeChangeEventCapacity);
            ExWorld.EntityManager.GetBuffer<CueRequestBuffer>(EntityEventBus).EnsureCapacity(CueRequestCapacity);
            ExWorld.EntityManager.GetBuffer<PresentationOutboxOwnerBuffer>(EntityEventBus).EnsureCapacity(PresentationOutboxOwnerCapacity);
            ExWorld.EntityManager.SetName(EntityEventBus, "EventBus");
        }

        private static void CreateEventLogSinkSingleton()
        {
            EntityEventLogSink = ExWorld.EntityManager.CreateEntity(GASRuntimeEntityArchetypes.GameplayEventLogSink(EntityManager));
            ExWorld.EntityManager.GetBuffer<ReplayLogEventBuffer>(EntityEventLogSink).EnsureCapacity(DebugReplayEventCapacity);
            ExWorld.EntityManager.SetName(EntityEventLogSink, "DebugReplayEventLog");
        }

        private static void CreateRuntimeDebuggerSingleton()
        {
            EntityRuntimeDebugger = GasRuntimeDebugger.CreateSingleton(ExWorld.EntityManager);
        }

    }
}
