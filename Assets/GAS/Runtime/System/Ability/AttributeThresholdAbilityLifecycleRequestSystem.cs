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
            var ruleChunkCount = _query.CalculateChunkCount();
            if (ruleChunkCount <= 0)
                return;

            var em = state.EntityManager;
            var ruleRecordStream = new NativeStream(ruleChunkCount, Allocator.TempJob);

            try
            {
                var scanJob = new AttributeThresholdAbilityLifecycleRequestScanJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    RuleTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityAttributeThresholdLifecycleRuleComponent>(isReadOnly: true),
                    RuleRecordWriter = ruleRecordStream.AsWriter(),
                };
                state.Dependency = scanJob.ScheduleParallel(_query, state.Dependency);
                state.Dependency.Complete();

                var ruleRecordReader = ruleRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < ruleRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = ruleRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                    {
                        var ruleRecord = ruleRecordReader.Read<AttributeThresholdAbilityLifecycleRequestRecord>();
                        EvaluateRule(em, in ruleRecord);
                    }
                    ruleRecordReader.EndForEachIndex();
                }
            }
            finally
            {
                ruleRecordStream.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private struct AttributeThresholdAbilityLifecycleRequestRecord
        {
            public Entity RuleEntity;
            public AbilityAttributeThresholdLifecycleRuleComponent Rule;
        }

        private struct AttributeThresholdAbilityLifecycleRequestScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<AbilityAttributeThresholdLifecycleRuleComponent> RuleTypeHandle;
            public NativeStream.Writer RuleRecordWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                RuleRecordWriter.BeginForEachIndex(unfilteredChunkIndex);
                var ruleEntities = chunk.GetNativeArray(EntityTypeHandle);
                var rules = chunk.GetNativeArray(ref RuleTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    RuleRecordWriter.Write(new AttributeThresholdAbilityLifecycleRequestRecord
                    {
                        RuleEntity = ruleEntities[entityIndex],
                        Rule = rules[entityIndex],
                    });
                }
                RuleRecordWriter.EndForEachIndex();
            }
        }

        private static void EvaluateRule(EntityManager em, in AttributeThresholdAbilityLifecycleRequestRecord ruleRecord)
        {
            var rule = ruleRecord.Rule;
            var owner = rule.OwnerAsc != Entity.Null ? rule.OwnerAsc : ruleRecord.RuleEntity;
            if (!CanEvaluateRule(em, owner, rule))
                return;

            if (!TryGetAttributeValue(em.GetBuffer<AttributeValueBuffer>(owner), rule.AttrSetCode, rule.AttrCode, out var value)
                || value > rule.Threshold)
            {
                return;
            }

            var ability = FindGrantedAbility(em, em.GetBuffer<AbilitySlotBuffer>(owner), rule.AbilityCode);
            if (ability == Entity.Null || !ShouldRequestLifecycle(em, ability))
                return;

            var baseInfo = em.GetComponentData<AbilityStateComponent>(ability);
            var reason = rule.Reason == EAbilityLifecycleReason.Unknown
                ? EAbilityLifecycleReason.AttributeThreshold
                : rule.Reason;

            if (rule.RequestType == EAttributeThresholdAbilityLifecycleRequestType.Cancel)
            {
                AbilityRuntimeActions.RequestAbilityCancel(
                    ability,
                    em,
                    reason,
                    sourceAbility: ability,
                    sourceAbilityCode: baseInfo.Code);
            }
            else
            {
                AbilityRuntimeActions.RequestAbilityEnd(
                    ability,
                    em,
                    reason,
                    sourceAbility: ability,
                    sourceAbilityCode: baseInfo.Code);
            }
        }

        private static bool CanEvaluateRule(
            EntityManager em,
            Entity owner,
            in AbilityAttributeThresholdLifecycleRuleComponent rule)
        {
            return owner != Entity.Null
                && rule.AbilityCode > 0
                && em.Exists(owner)
                && em.HasBuffer<AttributeValueBuffer>(owner)
                && em.HasBuffer<AbilitySlotBuffer>(owner);
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

        private static Entity FindGrantedAbility(
            EntityManager em,
            DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
            int abilityCode)
        {
            for (var i = 0; i < grantedAbilities.Length; i++)
            {
                var ability = grantedAbilities[i].AbilityEntity;
                if (ability == Entity.Null
                    || !em.Exists(ability)
                    || !em.HasComponent<AbilityStateComponent>(ability))
                {
                    continue;
                }

                var baseInfo = em.GetComponentData<AbilityStateComponent>(ability);
                if (baseInfo.Code == abilityCode)
                    return ability;
            }

            return Entity.Null;
        }

        private static bool ShouldRequestLifecycle(EntityManager em, Entity ability)
        {
            if (!em.Exists(ability)
                || !em.HasComponent<AbilityStateComponent>(ability)
                || AbilityRuntimeActions.IsEndRequested(ability, em)
                || AbilityRuntimeActions.IsCancelRequested(ability, em))
            {
                return false;
            }

            var phase = em.GetComponentData<AbilityStateComponent>(ability).Phase;
            return phase == EAbilityPhase.Activating || phase == EAbilityPhase.Active;
        }
    }
}
