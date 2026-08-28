using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 创建并验证 Runtime v1 唯一 Session/ASC 布局；每次调用均绑定传入 World，不保存跨 World archetype 缓存。
    /// </summary>
    internal static class GasRuntimeV1Archetypes
    {
        /// <summary>
        /// 在指定 EntityManager 所属 World 创建完整 Session archetype。
        /// </summary>
        public static EntityArchetype CreateSession(EntityManager entityManager)
        {
            return entityManager.CreateArchetype(CreateSessionComponentTypes());
        }

        /// <summary>
        /// 在指定 EntityManager 所属 World 创建完整 ASC archetype。
        /// </summary>
        public static EntityArchetype CreateAsc(EntityManager entityManager)
        {
            return entityManager.CreateArchetype(CreateAscComponentTypes());
        }

        /// <summary>
        /// 验证 Entity 是否完整拥有 Runtime v1 Session 固定布局。
        /// </summary>
        public static bool HasSessionLayout(EntityManager entityManager, Entity entity)
        {
            return entityManager.Exists(entity) &&
                   HasSessionComponents(entityManager, entity) &&
                   HasSessionBuffers(entityManager, entity);
        }

        /// <summary>
        /// 验证 Entity 是否完整拥有 Runtime v1 ASC 固定布局。
        /// </summary>
        public static bool HasAscLayout(EntityManager entityManager, Entity entity)
        {
            return entityManager.Exists(entity) &&
                   HasAscComponents(entityManager, entity) &&
                   HasAscFixedBuffers(entityManager, entity) &&
                   HasAscInitializationBuffers(entityManager, entity) &&
                   HasAscRuntimeBuffers(entityManager, entity);
        }

        /// <summary>
        /// 将 ASC 固定长度 Attribute/Tag buffers 清零并调整为 Catalog 冻结长度。
        /// </summary>
        public static bool TryInitializeFixedBuffers(
            EntityManager entityManager,
            Entity entity,
            in GasCatalogRegistry catalog)
        {
            if (!HasValidCatalogLengths(in catalog) || !HasAscFixedBuffers(entityManager, entity))
                return false;

            ResizeAndClear(entityManager.GetBuffer<AttributeValueSlot>(entity), catalog.AttributeCount);
            ResizeAndClear(entityManager.GetBuffer<AttributeDirtyWord>(entity), catalog.AttributeDirtyWordCount);
            ResizeAndClear(entityManager.GetBuffer<TagCountSlot>(entity), catalog.TagCount);
            ResizeAndClear(entityManager.GetBuffer<TagPresenceWord>(entity), catalog.TagPresenceWordCount);
            return true;
        }

        /// <summary>
        /// 向 ECB 记录 ASC 固定长度 Attribute/Tag buffers 的清零初始化。
        /// </summary>
        public static bool TryInitializeFixedBuffers(
            EntityCommandBuffer commandBuffer,
            Entity entity,
            in GasCatalogRegistry catalog)
        {
            if (!HasValidCatalogLengths(in catalog))
                return false;

            ResizeAndClear(commandBuffer.SetBuffer<AttributeValueSlot>(entity), catalog.AttributeCount);
            ResizeAndClear(commandBuffer.SetBuffer<AttributeDirtyWord>(entity), catalog.AttributeDirtyWordCount);
            ResizeAndClear(commandBuffer.SetBuffer<TagCountSlot>(entity), catalog.TagCount);
            ResizeAndClear(commandBuffer.SetBuffer<TagPresenceWord>(entity), catalog.TagPresenceWordCount);
            return true;
        }

        /// <summary>
        /// 验证 ASC 四类固定 buffer 的逻辑长度是否精确匹配 Catalog registry。
        /// </summary>
        public static bool HasExpectedFixedBufferLengths(
            EntityManager entityManager,
            Entity entity,
            in GasCatalogRegistry catalog)
        {
            if (!HasValidCatalogLengths(in catalog) || !HasAscFixedBuffers(entityManager, entity))
                return false;

            return entityManager.GetBuffer<AttributeValueSlot>(entity).Length == catalog.AttributeCount &&
                   entityManager.GetBuffer<AttributeDirtyWord>(entity).Length == catalog.AttributeDirtyWordCount &&
                   entityManager.GetBuffer<TagCountSlot>(entity).Length == catalog.TagCount &&
                   entityManager.GetBuffer<TagPresenceWord>(entity).Length == catalog.TagPresenceWordCount;
        }

        /// <summary>
        /// 初始化 ASC slab free-head；调用方仍需独立写入 owner-specific identity 与 drain state。
        /// </summary>
        public static bool TryInitializeAscMetadata(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.Exists(entity) || !entityManager.HasComponent<AscSlabHeads>(entity))
                return false;

            entityManager.SetComponentData(entity, AscSlabHeads.CreateEmpty());
            return true;
        }

        /// <summary>
        /// 向 ECB 记录 ASC slab free-head 初始化，不引入任何静态 World 状态。
        /// </summary>
        public static void InitializeAscMetadata(EntityCommandBuffer commandBuffer, Entity entity)
        {
            commandBuffer.SetComponent(entity, AscSlabHeads.CreateEmpty());
        }

        /// <summary>
        /// 按 ScaleProfile 为 Session 持久 buffers 预留容量，负容量或布局缺失时拒绝写入。
        /// </summary>
        public static bool TryApplySessionCapacities(
            EntityManager entityManager,
            Entity entity,
            in GasScaleProfile profile)
        {
            if (!HasValidSessionCapacities(in profile) || !HasSessionBuffers(entityManager, entity))
                return false;

            entityManager.GetBuffer<BattleInstanceSlot>(entity).EnsureCapacity(profile.MaxBattleInstanceCount);
            entityManager.GetBuffer<AscRegistrySlot>(entity).EnsureCapacity(profile.MaxAscRegistryCount);
            entityManager.GetBuffer<SpawnBatchMemberManifestSlot>(entity)
                .EnsureCapacity(profile.MaxAscRegistryCount);
            entityManager.GetBuffer<BoundaryCommandInbox>(entity).EnsureCapacity(profile.MaxBoundaryCommandCount);
            entityManager.GetBuffer<BoundaryCommandFrozenPayload>(entity)
                .EnsureCapacity(profile.MaxBoundaryCommandPayloadCount);
            entityManager.GetBuffer<BoundaryFactBuffer>(entity).EnsureCapacity(profile.MaxSessionBoundaryFactCount);
            return true;
        }

        /// <summary>
        /// 按 ScaleProfile 为 ASC 全部可变 buffers 预留容量，负容量或布局缺失时拒绝写入。
        /// </summary>
        public static bool TryApplyAscCapacities(
            EntityManager entityManager,
            Entity entity,
            in GasScaleProfile profile)
        {
            if (!HasValidAscCapacities(in profile) || !HasAscVariableBuffers(entityManager, entity))
                return false;

            ApplyInitializationCapacities(entityManager, entity, in profile);
            ApplyAbilityCapacities(entityManager, entity, in profile);
            ApplyEffectCapacities(entityManager, entity, in profile);
            entityManager.GetBuffer<PendingCommand>(entity).EnsureCapacity(profile.MaxPendingCommandCount);
            entityManager.GetBuffer<BoundaryFactBuffer>(entity).EnsureCapacity(profile.MaxAscBoundaryFactCount);
            return true;
        }

        /// <summary>
        /// 构造 Session archetype 的完整且每次新建的 ComponentType 列表。
        /// </summary>
        private static ComponentType[] CreateSessionComponentTypes()
        {
            return new[]
            {
                ComponentType.ReadWrite<GasSessionIdentity>(),
                ComponentType.ReadWrite<GasActiveSessionAuthority>(),
                ComponentType.ReadWrite<GasSessionConfig>(),
                ComponentType.ReadWrite<GasDefinitionRegistry>(),
                ComponentType.ReadWrite<GasCatalogRegistry>(),
                ComponentType.ReadWrite<GasScaleProfile>(),
                ComponentType.ReadWrite<SimulationTickState>(),
                ComponentType.ReadWrite<GasSessionLifecycle>(),
                ComponentType.ReadWrite<GasTickDiagnostics>(),
                ComponentType.ReadWrite<GasSpawnBatchManifest>(),
                ComponentType.ReadWrite<SessionFaultLatch>(),
                ComponentType.ReadWrite<BattleInstanceSlot>(),
                ComponentType.ReadWrite<AscRegistrySlot>(),
                ComponentType.ReadWrite<SpawnBatchMemberManifestSlot>(),
                ComponentType.ReadWrite<BoundaryCommandInbox>(),
                ComponentType.ReadWrite<BoundaryCommandFrozenPayload>(),
                ComponentType.ReadWrite<BoundaryDrainState>(),
                ComponentType.ReadWrite<BoundaryFactBuffer>(),
            };
        }

        /// <summary>
        /// 构造 ASC archetype 的完整且每次新建的 ComponentType 列表。
        /// </summary>
        private static ComponentType[] CreateAscComponentTypes()
        {
            return new[]
            {
                ComponentType.ReadWrite<GasAscIdentity>(),
                ComponentType.ReadWrite<GasSpawnBatchMarker>(),
                ComponentType.ReadWrite<AscBattleMembership>(),
                ComponentType.ReadWrite<AscLifecycle>(),
                ComponentType.ReadWrite<GasActorBinding>(),
                ComponentType.ReadWrite<AscRandomState>(),
                ComponentType.ReadWrite<AscSlabHeads>(),
                ComponentType.ReadWrite<GasPayloadRangeAllocatorState>(),
                ComponentType.ReadWrite<BoundaryDrainState>(),
                ComponentType.ReadWrite<AttributeValueSlot>(),
                ComponentType.ReadWrite<AttributeDirtyWord>(),
                ComponentType.ReadWrite<TagCountSlot>(),
                ComponentType.ReadWrite<TagPresenceWord>(),
                ComponentType.ReadWrite<PendingAttributeInitialization>(),
                ComponentType.ReadWrite<PendingTagInitialization>(),
                ComponentType.ReadWrite<PendingGrantedAbilityInitialization>(),
                ComponentType.ReadWrite<GrantedAbilitySlot>(),
                ComponentType.ReadWrite<AbilityActivationSlot>(),
                ComponentType.ReadWrite<AbilityContinuationSlot>(),
                ComponentType.ReadWrite<AbilitySubscriptionSlot>(),
                ComponentType.ReadWrite<CooldownGateSlot>(),
                ComponentType.ReadWrite<ActivationOwnedContributionSlot>(),
                ComponentType.ReadWrite<EmittedApplicationRefSlot>(),
                ComponentType.ReadWrite<ActiveEffectSlot>(),
                ComponentType.ReadWrite<GasPayloadRangeRecord>(),
                ComponentType.ReadWrite<GasPayloadValueSlot>(),
                ComponentType.ReadWrite<AttributeAggregatorSlot>(),
                ComponentType.ReadWrite<LiveDependencySlot>(),
                ComponentType.ReadWrite<LiveDependencyRouteSlot>(),
                ComponentType.ReadWrite<PendingCommand>(),
                ComponentType.ReadWrite<BoundaryFactBuffer>(),
            };
        }

        /// <summary>
        /// 验证 Session 的普通 Component 与 cleanup receipt 均已预挂载。
        /// </summary>
        private static bool HasSessionComponents(EntityManager entityManager, Entity entity)
        {
            return entityManager.HasComponent<GasSessionIdentity>(entity) &&
                   entityManager.HasComponent<GasActiveSessionAuthority>(entity) &&
                   entityManager.HasComponent<GasSessionConfig>(entity) &&
                   entityManager.HasComponent<GasDefinitionRegistry>(entity) &&
                   entityManager.HasComponent<GasCatalogRegistry>(entity) &&
                   entityManager.HasComponent<GasScaleProfile>(entity) &&
                   entityManager.HasComponent<SimulationTickState>(entity) &&
                   entityManager.HasComponent<GasSessionLifecycle>(entity) &&
                   entityManager.HasComponent<GasTickDiagnostics>(entity) &&
                   entityManager.HasComponent<GasSpawnBatchManifest>(entity) &&
                   entityManager.HasComponent<SessionFaultLatch>(entity) &&
                   entityManager.HasComponent<BoundaryDrainState>(entity);
        }

        /// <summary>
        /// 验证 Session 的 registry、ingress 与 cleanup outbox buffers 均已预挂载。
        /// </summary>
        private static bool HasSessionBuffers(EntityManager entityManager, Entity entity)
        {
            return entityManager.HasComponent<BattleInstanceSlot>(entity) &&
                   entityManager.HasComponent<AscRegistrySlot>(entity) &&
                   entityManager.HasComponent<SpawnBatchMemberManifestSlot>(entity) &&
                   entityManager.HasComponent<BoundaryCommandInbox>(entity) &&
                   entityManager.HasComponent<BoundaryCommandFrozenPayload>(entity) &&
                   entityManager.HasComponent<BoundaryFactBuffer>(entity);
        }

        /// <summary>
        /// 验证 ASC 的普通 Component 与 cleanup receipt 均已预挂载。
        /// </summary>
        private static bool HasAscComponents(EntityManager entityManager, Entity entity)
        {
            return entityManager.HasComponent<GasAscIdentity>(entity) &&
                   entityManager.HasComponent<GasSpawnBatchMarker>(entity) &&
                   entityManager.HasComponent<AscBattleMembership>(entity) &&
                   entityManager.HasComponent<AscLifecycle>(entity) &&
                   entityManager.HasComponent<GasActorBinding>(entity) &&
                   entityManager.HasComponent<AscRandomState>(entity) &&
                   entityManager.HasComponent<AscSlabHeads>(entity) &&
                   entityManager.HasComponent<GasPayloadRangeAllocatorState>(entity) &&
                   entityManager.HasComponent<BoundaryDrainState>(entity);
        }

        /// <summary>
        /// 验证 ASC 的四类 Catalog 固定长度 authority/derived buffers 均已预挂载。
        /// </summary>
        private static bool HasAscFixedBuffers(EntityManager entityManager, Entity entity)
        {
            return entityManager.HasComponent<AttributeValueSlot>(entity) &&
                   entityManager.HasComponent<AttributeDirtyWord>(entity) &&
                   entityManager.HasComponent<TagCountSlot>(entity) &&
                   entityManager.HasComponent<TagPresenceWord>(entity);
        }

        /// <summary>
        /// 验证 SpawnFinalize 使用的三类 Pending 初始化 buffers 均已预挂载。
        /// </summary>
        private static bool HasAscInitializationBuffers(EntityManager entityManager, Entity entity)
        {
            return entityManager.HasComponent<PendingAttributeInitialization>(entity) &&
                   entityManager.HasComponent<PendingTagInitialization>(entity) &&
                   entityManager.HasComponent<PendingGrantedAbilityInitialization>(entity);
        }

        /// <summary>
        /// 验证 ASC 的全部 non-compacting runtime buffers 与 cleanup outbox 均已预挂载。
        /// </summary>
        private static bool HasAscRuntimeBuffers(EntityManager entityManager, Entity entity)
        {
            return entityManager.HasComponent<GrantedAbilitySlot>(entity) &&
                   entityManager.HasComponent<AbilityActivationSlot>(entity) &&
                   entityManager.HasComponent<AbilityContinuationSlot>(entity) &&
                   entityManager.HasComponent<AbilitySubscriptionSlot>(entity) &&
                   entityManager.HasComponent<CooldownGateSlot>(entity) &&
                   entityManager.HasComponent<ActivationOwnedContributionSlot>(entity) &&
                   entityManager.HasComponent<EmittedApplicationRefSlot>(entity) &&
                   entityManager.HasComponent<ActiveEffectSlot>(entity) &&
                   entityManager.HasComponent<GasPayloadRangeRecord>(entity) &&
                   entityManager.HasComponent<GasPayloadValueSlot>(entity) &&
                   entityManager.HasComponent<AttributeAggregatorSlot>(entity) &&
                   entityManager.HasComponent<LiveDependencySlot>(entity) &&
                   entityManager.HasComponent<LiveDependencyRouteSlot>(entity) &&
                   entityManager.HasComponent<PendingCommand>(entity) &&
                   entityManager.HasComponent<BoundaryFactBuffer>(entity);
        }

        /// <summary>
        /// 验证 ASC 所有可变容量 buffers 已预挂载，供容量初始化拒绝部分布局。
        /// </summary>
        private static bool HasAscVariableBuffers(EntityManager entityManager, Entity entity)
        {
            return HasAscInitializationBuffers(entityManager, entity) &&
                   HasAscRuntimeBuffers(entityManager, entity);
        }

        /// <summary>
        /// 验证 Catalog 派生的四个固定长度均为非负值。
        /// </summary>
        private static bool HasValidCatalogLengths(in GasCatalogRegistry catalog)
        {
            return catalog.AttributeCount >= 0 &&
                   catalog.AttributeDirtyWordCount >= 0 &&
                   catalog.TagCount >= 0 &&
                   catalog.TagPresenceWordCount >= 0;
        }

        /// <summary>
        /// 将 buffer 清零并设置精确逻辑长度，避免复用内存残留成为 authority。
        /// </summary>
        private static void ResizeAndClear<T>(DynamicBuffer<T> buffer, int length)
            where T : unmanaged, IBufferElementData
        {
            buffer.Clear();
            buffer.ResizeUninitialized(length);
            for (var index = 0; index < length; index++)
                buffer[index] = default;
        }

        /// <summary>
        /// 验证 Session 与 batch 相关的全部 ScaleProfile 容量均非负。
        /// </summary>
        private static bool HasValidSessionCapacities(in GasScaleProfile profile)
        {
            return profile.ProfileId > 0 &&
                   profile.ProfileVersion > 0 &&
                   profile.ProfileHash != 0 &&
                   profile.MaxFixedTicksPerBatch > 0 &&
                   profile.MaximumDeltaTimeTicks > 0 &&
                   profile.MaximumDeltaTimeTicks <= profile.MaxFixedTicksPerBatch &&
                   profile.MaxSpawnBatchSize >= 0 &&
                   profile.MaxBattleInstanceCount >= 0 &&
                   profile.MaxAscRegistryCount >= 0 &&
                   profile.MaxBoundaryCommandCount >= 0 &&
                   profile.MaxBoundaryCommandPayloadCount >= 0 &&
                   HasValidTickScratchCapacities(in profile) &&
                   profile.MaxSessionBoundaryFactCount >= 0;
        }

        /// <summary>
        /// 验证完整 Tick DAG 的 scratch、reservation、fact 与结构 intent 上限均非负。
        /// </summary>
        private static bool HasValidTickScratchCapacities(in GasScaleProfile profile)
        {
            return profile.MaxOwnerPlanCount >= 0 &&
                   profile.MaxResolvedTargetCount >= 0 &&
                   profile.MaxEffectOperationCount >= 0 &&
                   profile.MaxOwnerReservationCount >= 0 &&
                   profile.MaxTargetReservationCount >= 0 &&
                   profile.MaxCoreFactCount >= 0 &&
                   profile.MaxNextTickRouteCount >= 0 &&
                   profile.MaxStructuralIntentCount >= 0;
        }

        /// <summary>
        /// 验证 ASC 初始化、Ability 与关联引用容量均非负。
        /// </summary>
        private static bool HasValidAscAbilityCapacities(in GasScaleProfile profile)
        {
            return profile.MaxPendingAttributeInitializationCount >= 0 &&
                   profile.MaxPendingTagInitializationCount >= 0 &&
                   profile.MaxPendingGrantedAbilityInitializationCount >= 0 &&
                   profile.MaxGrantedAbilityCount >= 0 &&
                   profile.MaxAbilityActivationCount >= 0 &&
                   profile.MaxAbilityContinuationCount >= 0 &&
                   profile.MaxAbilitySubscriptionCount >= 0 &&
                   profile.MaxCooldownGateCount >= 0 &&
                   profile.MaxActivationOwnedContributionCount >= 0 &&
                   profile.MaxEmittedApplicationRefCount >= 0;
        }

        /// <summary>
        /// 验证 ASC Effect、依赖、命令与 outbox 容量均非负。
        /// </summary>
        private static bool HasValidAscEffectCapacities(in GasScaleProfile profile)
        {
            return profile.MaxActiveEffectCount >= 0 &&
                   profile.MaxPayloadRangeRecordCount >= 0 &&
                   profile.MaxPayloadValueCount >= 0 &&
                   profile.MaxAttributeAggregatorCount >= 0 &&
                   profile.MaxLiveDependencyCount >= 0 &&
                   profile.MaxLiveDependencyRouteCount >= 0 &&
                   profile.MaxPendingCommandCount >= 0 &&
                   profile.MaxAscBoundaryFactCount >= 0;
        }

        /// <summary>
        /// 验证 ASC 使用的全部 ScaleProfile 逻辑容量均非负。
        /// </summary>
        private static bool HasValidAscCapacities(in GasScaleProfile profile)
        {
            return HasValidAscAbilityCapacities(in profile) &&
                   HasValidAscEffectCapacities(in profile);
        }

        /// <summary>
        /// 为 SpawnFinalize 前的三类 Pending 初始化 buffers 预留 profile 容量。
        /// </summary>
        private static void ApplyInitializationCapacities(
            EntityManager entityManager,
            Entity entity,
            in GasScaleProfile profile)
        {
            entityManager.GetBuffer<PendingAttributeInitialization>(entity)
                .EnsureCapacity(profile.MaxPendingAttributeInitializationCount);
            entityManager.GetBuffer<PendingTagInitialization>(entity)
                .EnsureCapacity(profile.MaxPendingTagInitializationCount);
            entityManager.GetBuffer<PendingGrantedAbilityInitialization>(entity)
                .EnsureCapacity(profile.MaxPendingGrantedAbilityInitializationCount);
        }

        /// <summary>
        /// 为 Ability 生命周期与其反向引用 slabs 预留 profile 容量。
        /// </summary>
        private static void ApplyAbilityCapacities(
            EntityManager entityManager,
            Entity entity,
            in GasScaleProfile profile)
        {
            entityManager.GetBuffer<GrantedAbilitySlot>(entity).EnsureCapacity(profile.MaxGrantedAbilityCount);
            entityManager.GetBuffer<AbilityActivationSlot>(entity).EnsureCapacity(profile.MaxAbilityActivationCount);
            entityManager.GetBuffer<AbilityContinuationSlot>(entity).EnsureCapacity(profile.MaxAbilityContinuationCount);
            entityManager.GetBuffer<AbilitySubscriptionSlot>(entity).EnsureCapacity(profile.MaxAbilitySubscriptionCount);
            entityManager.GetBuffer<CooldownGateSlot>(entity).EnsureCapacity(profile.MaxCooldownGateCount);
            entityManager.GetBuffer<ActivationOwnedContributionSlot>(entity)
                .EnsureCapacity(profile.MaxActivationOwnedContributionCount);
            entityManager.GetBuffer<EmittedApplicationRefSlot>(entity)
                .EnsureCapacity(profile.MaxEmittedApplicationRefCount);
        }

        /// <summary>
        /// 为 Effect、capture、aggregator 与 live dependency slabs 预留 profile 容量。
        /// </summary>
        private static void ApplyEffectCapacities(
            EntityManager entityManager,
            Entity entity,
            in GasScaleProfile profile)
        {
            entityManager.GetBuffer<ActiveEffectSlot>(entity).EnsureCapacity(profile.MaxActiveEffectCount);
            entityManager.GetBuffer<GasPayloadRangeRecord>(entity).EnsureCapacity(profile.MaxPayloadRangeRecordCount);
            entityManager.GetBuffer<GasPayloadValueSlot>(entity).EnsureCapacity(profile.MaxPayloadValueCount);
            entityManager.GetBuffer<AttributeAggregatorSlot>(entity).EnsureCapacity(profile.MaxAttributeAggregatorCount);
            entityManager.GetBuffer<LiveDependencySlot>(entity).EnsureCapacity(profile.MaxLiveDependencyCount);
            entityManager.GetBuffer<LiveDependencyRouteSlot>(entity).EnsureCapacity(profile.MaxLiveDependencyRouteCount);
        }
    }
}
