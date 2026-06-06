using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Produces ability lifecycle requests from attribute thresholds without using OOP callbacks.
    /// </summary>
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(AbilityStateTickSystem))]
    [UpdateBefore(typeof(AbilityLifecycleRequestSystem))]
    [DisableAutoCreation]
    public partial struct AttributeThresholdAbilityLifecycleRequestSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AbilityAttributeThresholdLifecycleRuleComponent>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var factEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();
            var job = new AttributeThresholdAbilityLifecycleRequestJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                RuleTypeHandle =
                    SystemAPI.GetComponentTypeHandle<AbilityAttributeThresholdLifecycleRuleComponent>(isReadOnly: true),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: true),
                AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: true),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: true),
                CancelRequestLookup = SystemAPI.GetComponentLookup<AbilityCancelRequestComponent>(),
                EndRequestLookup = SystemAPI.GetComponentLookup<AbilityEndRequestComponent>(),
                EventBusEntity = eventBusEntity,
                FactEcb = factEcb,
            };
            state.Dependency = job.Schedule(_query, state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private struct AttributeThresholdAbilityLifecycleRequestJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<AbilityAttributeThresholdLifecycleRuleComponent> RuleTypeHandle;
            [ReadOnly] public BufferLookup<AttributeValueBuffer> AttributeLookup;
            [ReadOnly] public BufferLookup<AbilitySlotBuffer> AbilitySlotLookup;
            [ReadOnly] public ComponentLookup<AbilityStateComponent> AbilityStateLookup;
            public ComponentLookup<AbilityCancelRequestComponent> CancelRequestLookup;
            public ComponentLookup<AbilityEndRequestComponent> EndRequestLookup;
            public Entity EventBusEntity;
            public EntityCommandBuffer.ParallelWriter FactEcb;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var ruleEntities = chunk.GetNativeArray(EntityTypeHandle);
                var rules = chunk.GetNativeArray(ref RuleTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    EvaluateRule(unfilteredChunkIndex, ruleEntities[entityIndex], rules[entityIndex]);
                }
            }

            private void EvaluateRule(
                int sortKey,
                Entity ruleEntity,
                in AbilityAttributeThresholdLifecycleRuleComponent rule)
            {
                var owner = rule.OwnerAsc != Entity.Null ? rule.OwnerAsc : ruleEntity;
                if (owner == Entity.Null
                    || rule.AbilityCode <= 0
                    || !AttributeLookup.HasBuffer(owner)
                    || !AbilitySlotLookup.HasBuffer(owner))
                {
                    return;
                }

                if (!TryGetAttributeValue(
                        AttributeLookup[owner],
                        rule.AttrSetCode,
                        rule.AttrCode,
                        out var value)
                    || value > rule.Threshold)
                {
                    return;
                }

                var ability = FindGrantedAbility(AbilitySlotLookup[owner], rule.AbilityCode);
                if (ability == Entity.Null || !ShouldRequestLifecycle(ability))
                    return;

                var baseInfo = AbilityStateLookup[ability];
                var reason = rule.Reason == EAbilityLifecycleReason.Unknown
                    ? EAbilityLifecycleReason.AttributeThreshold
                    : rule.Reason;
                if (rule.RequestType == EAttributeThresholdAbilityLifecycleRequestType.Cancel)
                {
                    CancelRequestLookup[ability] = new AbilityCancelRequestComponent
                    {
                        Reason = reason,
                        SourceAbility = ability,
                        SourceEffect = Entity.Null,
                        SourceAbilityCode = baseInfo.Code,
                    };
                    CancelRequestLookup.SetComponentEnabled(ability, true);
                    AppendLifecycleFact(
                        sortKey,
                        ability,
                        baseInfo,
                        EGameplayEventType.AbilityCancelRequested,
                        reason);
                }
                else
                {
                    EndRequestLookup[ability] = new AbilityEndRequestComponent
                    {
                        Reason = reason,
                        SourceAbility = ability,
                        SourceEffect = Entity.Null,
                        SourceAbilityCode = baseInfo.Code,
                    };
                    EndRequestLookup.SetComponentEnabled(ability, true);
                    AppendLifecycleFact(
                        sortKey,
                        ability,
                        baseInfo,
                        EGameplayEventType.AbilityEndRequested,
                        reason);
                }
            }

            private static bool TryGetAttributeValue(
                DynamicBuffer<AttributeValueBuffer> attributes,
                int attrSetCode,
                int attrCode,
                out float value)
            {
                for (var i = 0; i < attributes.Length; i++)
                {
                    var attribute = attributes[i];
                    if (attribute.AttrSetCode != attrSetCode || attribute.Code != attrCode)
                        continue;

                    value = attribute.CurrentValue;
                    return true;
                }

                value = 0f;
                return false;
            }

            private Entity FindGrantedAbility(
                DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
                int abilityCode)
            {
                for (var i = 0; i < grantedAbilities.Length; i++)
                {
                    var ability = grantedAbilities[i].AbilityEntity;
                    if (ability == Entity.Null
                        || !AbilityStateLookup.HasComponent(ability)
                        || !CancelRequestLookup.HasComponent(ability)
                        || !EndRequestLookup.HasComponent(ability))
                    {
                        continue;
                    }

                    if (AbilityStateLookup[ability].Code == abilityCode)
                        return ability;
                }

                return Entity.Null;
            }

            private bool ShouldRequestLifecycle(Entity ability)
            {
                if (!AbilityStateLookup.HasComponent(ability)
                    || CancelRequestLookup.IsComponentEnabled(ability)
                    || EndRequestLookup.IsComponentEnabled(ability))
                {
                    return false;
                }

                var phase = AbilityStateLookup[ability].Phase;
                return phase == EAbilityPhase.Activating || phase == EAbilityPhase.Active;
            }

            private void AppendLifecycleFact(
                int sortKey,
                Entity ability,
                in AbilityStateComponent baseInfo,
                EGameplayEventType type,
                EAbilityLifecycleReason reason)
            {
                if (EventBusEntity == Entity.Null)
                    return;

                FactEcb.AppendToBuffer(sortKey, EventBusEntity, new GameplayEventBusEventBuffer
                {
                    Type = type,
                    SourceAsc = baseInfo.Owner,
                    TargetAsc = baseInfo.Owner,
                    SourceAbility = ability,
                    GameplayEffect = Entity.Null,
                    RelatedAbility = ability,
                    EventCode = baseInfo.Code,
                    ReasonCode = (int)reason,
                    RelatedAbilityCode = baseInfo.Code,
                    Value = baseInfo.Code,
                });
            }
        }
    }
}
