using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public static partial class HeadlessAutoChessScenario
    {
        private static void ResetObservationState(in HeadlessAutoChessOptions normalizedOptions)
        {
            var em = GASManager.EntityManager;
            if (em.Exists(GASManager.EntityGlobalTimer))
                em.SetComponentData(GASManager.EntityGlobalTimer, new GlobalTimer());

            if (em.Exists(GASManager.EntityEventBus))
            {
                em.SetComponentData(GASManager.EntityEventBus, new CGameplayEventBus());
                if (em.HasComponent<CPresentationOutboxProjectionState>(GASManager.EntityEventBus))
                    em.SetComponentData(GASManager.EntityEventBus, new CPresentationOutboxProjectionState());
                if (em.HasComponent<CPresentationOutboxProjectionOptions>(GASManager.EntityEventBus))
                {
                    em.SetComponentData(GASManager.EntityEventBus, new CPresentationOutboxProjectionOptions
                    {
                        ProjectRawFacts = normalizedOptions.ProjectRawPresentationOutbox ? (byte)1 : (byte)0,
                    });
                }
                ClearBuffer<BDamageEvent>(em, GASManager.EntityEventBus);
                ClearBuffer<BTagChangeEvent>(em, GASManager.EntityEventBus);
                ClearBuffer<BGameplayEvent>(em, GASManager.EntityEventBus);
                ClearBuffer<BAttributeChangeEvent>(em, GASManager.EntityEventBus);
                ClearBuffer<BCueRequest>(em, GASManager.EntityEventBus);
                ClearBuffer<BPresentationOutboxOwner>(em, GASManager.EntityEventBus);
            }

            if (em.Exists(GASManager.EntityEventLogSink))
            {
                em.SetComponentData(GASManager.EntityEventLogSink, new CGameplayEventLogSink());
                ClearBuffer<BDebugReplayEvent>(em, GASManager.EntityEventLogSink);
            }
        }

        private static void ResetRuntimeDebugger(in HeadlessAutoChessOptions normalizedOptions)
        {
            var em = GASManager.EntityManager;
            var debugger = GASManager.EntityRuntimeDebugger;
            if (debugger == Entity.Null || !em.Exists(debugger))
                return;

            GasRuntimeDebugger.Reset(em, debugger);
            GasRuntimeDebugger.Configure(
                em,
                debugger,
                enabled: true,
                captureSystemTimings: normalizedOptions.CollectSystemTimings,
                captureBufferPressure: true);
        }

        private static void ClearBuffer<T>(EntityManager em, Entity entity)
            where T : unmanaged, IBufferElementData
        {
            if (em.Exists(entity) && em.HasBuffer<T>(entity))
                em.GetBuffer<T>(entity).Clear();
        }

        private static void CleanupUnits(ScenarioState state)
        {
            var em = GASManager.EntityManager;
            if (state.DriverEntity != Entity.Null && em.Exists(state.DriverEntity))
                em.DestroyEntity(state.DriverEntity);

            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<CHeadlessAutoChessSummonedUnit>()))
            using (var summons = query.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < summons.Length; i++)
                    DestroyAscRuntime(em, summons[i]);
            }

            for (var i = 0; i < state.Units.Length; i++)
                DestroyAscRuntime(em, state.Units[i].Facade.Entity);
        }

        private static void RestoreDefaultObservationOptions()
        {
            var em = GASManager.EntityManager;
            if (em.Exists(GASManager.EntityEventBus)
                && em.HasComponent<CPresentationOutboxProjectionOptions>(GASManager.EntityEventBus))
            {
                em.SetComponentData(GASManager.EntityEventBus, new CPresentationOutboxProjectionOptions
                {
                    ProjectRawFacts = 1,
                });
            }
        }

        private static void DestroyAscRuntime(EntityManager em, Entity asc)
        {
            if (asc == Entity.Null || !em.Exists(asc))
                return;

            DestroyGrantedAbilities(em, asc);
            DestroyActiveEffects(em, asc);
            em.DestroyEntity(asc);
        }

        private static void DestroyGrantedAbilities(EntityManager em, Entity asc)
        {
            if (!em.HasBuffer<BGrantedAbility>(asc))
                return;

            var abilities = em.GetBuffer<BGrantedAbility>(asc);
            var abilityEntities = new NativeArray<Entity>(abilities.Length, Allocator.Temp);
            for (var i = 0; i < abilities.Length; i++)
                abilityEntities[i] = abilities[i].AbilityEntity;

            try
            {
                for (var i = abilityEntities.Length - 1; i >= 0; i--)
                {
                    var ability = abilityEntities[i];
                    if (ability == Entity.Null || !em.Exists(ability))
                        continue;

                    if (em.HasComponent<CAbilityConfig>(ability))
                    {
                        var config = em.GetComponentData<CAbilityConfig>(ability).Config;
                        if (config.IsCreated)
                            config.Dispose();
                    }

                    em.DestroyEntity(ability);
                }
            }
            finally
            {
                abilityEntities.Dispose();
            }
        }

        private static void DestroyActiveEffects(EntityManager em, Entity asc)
        {
            if (!em.HasBuffer<BGameplayEffect>(asc))
                return;

            var effects = em.GetBuffer<BGameplayEffect>(asc);
            var effectEntities = new NativeArray<Entity>(effects.Length, Allocator.Temp);
            for (var i = 0; i < effects.Length; i++)
                effectEntities[i] = effects[i].GameplayEffect;

            try
            {
                for (var i = effectEntities.Length - 1; i >= 0; i--)
                {
                    var effect = effectEntities[i];
                    if (effect != Entity.Null && em.Exists(effect))
                        em.DestroyEntity(effect);
                }
            }
            finally
            {
                effectEntities.Dispose();
            }
        }
    }
}
