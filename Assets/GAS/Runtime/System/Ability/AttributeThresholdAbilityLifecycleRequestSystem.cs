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
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AbilityAttributeThresholdLifecycleRuleComponent>(),
                },
            });
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var streamEntity = SystemAPI.TryGetSingletonEntity<GEEffectCommandStreamComponent>(out var resolvedStream)
                ? resolvedStream
                : Entity.Null;
            var frame = SystemAPI.TryGetSingleton<GlobalTimer>(out var timer)
                ? timer.Frame
                : 0;
            var job = new AttributeThresholdAbilityLifecycleRequestJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                RuleTypeHandle =
                    SystemAPI.GetComponentTypeHandle<AbilityAttributeThresholdLifecycleRuleComponent>(isReadOnly: true),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: true),
                AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: true),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: true),
                AbilityLifecycleRequestLookup = SystemAPI.GetBufferLookup<AbilityLifecycleRequestBuffer>(),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                OwnerFactLookup = SystemAPI.GetBufferLookup<OwnerLocalGameplayFactBuffer>(),
                OwnerLocalGameplayFactDirtyOwnerLookup =
                    SystemAPI.GetBufferLookup<OwnerLocalGameplayFactDirtyOwnerBuffer>(),
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
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
            public BufferLookup<AbilityLifecycleRequestBuffer> AbilityLifecycleRequestLookup;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<OwnerLocalGameplayFactBuffer> OwnerFactLookup;
            public BufferLookup<OwnerLocalGameplayFactDirtyOwnerBuffer> OwnerLocalGameplayFactDirtyOwnerLookup;
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;

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
                    AppendLifecycleRequest(
                        ability,
                        EAbilityLifecycleRequestKind.Cancel,
                        reason,
                        baseInfo.Code);
                    AppendLifecycleFact(
                        ability,
                        baseInfo,
                        EGameplayEventType.AbilityCancelRequested,
                        reason);
                }
                else
                {
                    AppendLifecycleRequest(
                        ability,
                        EAbilityLifecycleRequestKind.End,
                        reason,
                        baseInfo.Code);
                    AppendLifecycleFact(
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
                        || !AbilityStateLookup.HasComponent(ability))
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
                if (!AbilityStateLookup.HasComponent(ability))
                {
                    return false;
                }

                var phase = AbilityStateLookup[ability].Phase;
                return phase == EAbilityPhase.Activating || phase == EAbilityPhase.Active;
            }

            private void AppendLifecycleRequest(
                Entity ability,
                EAbilityLifecycleRequestKind requestKind,
                EAbilityLifecycleReason reason,
                int sourceAbilityCode)
            {
                if (EventBusEntity == Entity.Null
                    || !AbilityLifecycleRequestLookup.HasBuffer(EventBusEntity))
                {
                    return;
                }

                var requests = AbilityLifecycleRequestLookup[EventBusEntity];
                requests.Add(new AbilityLifecycleRequestBuffer
                {
                    Sequence = requests.Length,
                    RequestKind = requestKind,
                    Reason = reason,
                    Ability = ability,
                    SourceAbility = ability,
                    SourceEffect = Entity.Null,
                    SourceAbilityCode = sourceAbilityCode,
                    DestroyOnCleanup = 0,
                });
            }

            private void AppendLifecycleFact(
                Entity ability,
                in AbilityStateComponent baseInfo,
                EGameplayEventType type,
                EAbilityLifecycleReason reason)
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || baseInfo.Owner == Entity.Null
                    || !OwnerFactLookup.HasBuffer(baseInfo.Owner))
                    return;

                var fact = new GameplayEventBuffer
                {
                    Frame = Frame,
                    EventType = type,
                    Domain = EGameplayFactDomain.Ability,
                    Category = EGameplayFactCategory.Request,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = baseInfo.Owner,
                    TargetAsc = baseInfo.Owner,
                    SourceAbility = ability,
                    EventCode = baseInfo.Code,
                    ReasonCode = (int)reason,
                    Value = baseInfo.Code,
                };
                var stream = StreamLookup[StreamEntity];
                fact.Sequence = GASRuntimeSequenceAllocator.AllocateFactSequence(ref stream);
                StreamLookup[StreamEntity] = stream;
                OwnerFactLookup[baseInfo.Owner].Add(new OwnerLocalGameplayFactBuffer
                {
                    Fact = fact,
                });
                EffectCommandSpecStream.MarkOwnerLocalGameplayFactDirty(
                    OwnerLocalGameplayFactDirtyOwnerLookup,
                    StreamEntity,
                    baseInfo.Owner);
            }

        }
    }
}
