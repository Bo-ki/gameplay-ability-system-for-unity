using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SAbilityStateCleanup))]
    [UpdateBefore(typeof(SHeadlessAutoChessSummonLifecycle))]
    public partial struct SHeadlessAutoChessGameplayEffectFactProjection : ISystem
    {
        private EntityQuery _driverQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessGameplayEffectFacts, BHeadlessAutoChessGameplayEffectAppliedFact>()
                .Build();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity) || !em.HasBuffer<BGameplayEvent>(eventBusEntity))
                return;

            if (_driverQuery.IsEmptyIgnoreFilter)
                return;

            var driverEntity = _driverQuery.GetSingletonEntity();
            var facts = em.GetComponentData<CHeadlessAutoChessGameplayEffectFacts>(driverEntity);
            var appliedFacts = em.GetBuffer<BHeadlessAutoChessGameplayEffectAppliedFact>(driverEntity);
            var frame = ResolveCurrentFrame(em);
            PrepareTypedFactBuffers(appliedFacts, ref facts, frame);

            using var gameplayEvents = EventBusHelper.SnapshotBufferRange<BGameplayEvent>(
                em,
                eventBusEntity,
                facts.ProcessedGameplayEventCount,
                Allocator.Temp,
                out var eventCount);

            for (var i = 0; i < gameplayEvents.Length; i++)
            {
                var evt = gameplayEvents[i];
                if (evt.Type != EGameplayEventType.GameplayEffectApplied)
                    continue;

                appliedFacts.Add(new BHeadlessAutoChessGameplayEffectAppliedFact
                {
                    Frame = evt.Frame,
                    Sequence = evt.Sequence,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = evt.TargetAsc,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    RelatedAbility = evt.RelatedAbility,
                    ContextId = evt.ContextId,
                    GameplayEffectCode = evt.EventCode,
                    ReasonCode = evt.ReasonCode,
                    RelatedAbilityCode = evt.RelatedAbilityCode,
                    Value = evt.Value,
                });
                facts.GameplayEffectAppliedFactCount++;
            }

            facts.ProcessedGameplayEventCount = eventCount;
            em.SetComponentData(driverEntity, facts);
        }

        private static void PrepareTypedFactBuffers(
            DynamicBuffer<BHeadlessAutoChessGameplayEffectAppliedFact> appliedFacts,
            ref CHeadlessAutoChessGameplayEffectFacts facts,
            int frame)
        {
            if (facts.LastProjectionFrame == frame)
                return;

            facts.LastProjectionFrame = frame;
            facts.ProcessedGameplayEventCount = 0;
            appliedFacts.Clear();
        }

        private static int ResolveCurrentFrame(EntityManager em)
        {
            var globalTimer = GASManager.EntityGlobalTimer;
            if (globalTimer != Entity.Null
                && em.Exists(globalTimer)
                && em.HasComponent<GlobalTimer>(globalTimer))
            {
                return em.GetComponentData<GlobalTimer>(globalTimer).Frame;
            }

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GlobalTimer>());
            return query.IsEmptyIgnoreFilter ? 0 : query.GetSingleton<GlobalTimer>().Frame;
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
