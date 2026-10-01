using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    /// <summary>
    /// 为 Kernel 创建仅持有本次 World-local lookups 的 Stage-B SpawnFinalize 调度节点。
    /// </summary>
    internal static class GasStageBSpawnFinalize
    {
        /// <summary>
        /// 调度唯一 Session 的全批 finalize，并把写入依赖接回 Kernel DAG。
        /// </summary>
        internal static JobHandle Run(
            ref GasStageBSpawnFinalizeJob template,
            Entity session,
            NativeList<Entity> batchMarkedAscs,
            int maximumDestroyCount,
            JobHandle gatherDependency,
            EntityCommandBuffer endFixed,
            JobHandle dependency,
            GasTickScratch bootstrapScratch = default)
        {
            var bootstrapEvaluatorStack = bootstrapScratch.EvaluatorStack;
            var bootstrapAttributeMutations = bootstrapScratch.AttributeMutations;
            var ownsBootstrapEvaluatorStack = false;
            var ownsBootstrapAttributeMutations = false;
            if (!bootstrapEvaluatorStack.IsCreated)
            {
                bootstrapEvaluatorStack = new NativeArray<float>(0, Allocator.TempJob);
                ownsBootstrapEvaluatorStack = true;
            }
            if (!bootstrapAttributeMutations.IsCreated)
            {
                bootstrapAttributeMutations = new NativeArray<GasAttributeMutationRecord>(
                    0, Allocator.TempJob);
                ownsBootstrapAttributeMutations = true;
            }
            var recordedDestroys = new NativeList<Entity>(
                maximumDestroyCount < 0 ? 0 : maximumDestroyCount,
                Allocator.TempJob);
            var job = template;
            job.EndFixed = endFixed;
            job.Session = session;
            job.BatchMarkedAscs = batchMarkedAscs.AsDeferredJobArray();
            job.RecordedDestroyEntities = recordedDestroys;
            job.BootstrapEvaluatorStack = bootstrapEvaluatorStack;
            job.BootstrapAttributeMutations = bootstrapAttributeMutations;
            var combinedDependency = JobHandle.CombineDependencies(dependency, gatherDependency);
            var handle = job.Schedule(combinedDependency);
            handle = recordedDestroys.Dispose(handle);
            if (ownsBootstrapEvaluatorStack)
                handle = bootstrapEvaluatorStack.Dispose(handle);
            if (ownsBootstrapAttributeMutations)
                handle = bootstrapAttributeMutations.Dispose(handle);
            return batchMarkedAscs.Dispose(handle);
        }

        /// <summary>
        /// 调度非单 Session 基数的 fail-closed 处理，并在 job 后释放临时实体数组。
        /// </summary>
        internal static JobHandle FailSessionCardinality(
            ref GasStageBSpawnFinalizeJob template,
            NativeArray<Entity> sessions,
            NativeArray<Entity> batchMarkedAscs,
            EntityCommandBuffer endFixed,
            JobHandle dependency)
        {
            var handle = dependency;
            var bootstrapEvaluatorStack = new NativeArray<float>(0, Allocator.TempJob);
            var bootstrapAttributeMutations = new NativeArray<GasAttributeMutationRecord>(
                0, Allocator.TempJob);
            var initialCapacity = batchMarkedAscs.Length > sessions.Length
                ? batchMarkedAscs.Length
                : sessions.Length;
            var recordedDestroys = new NativeList<Entity>(initialCapacity, Allocator.TempJob);
            for (var index = 0; index < sessions.Length; index++)
            {
                var job = template;
                job.EndFixed = endFixed;
                job.Session = sessions[index];
                job.BatchMarkedAscs = batchMarkedAscs;
                job.RecordedDestroyEntities = recordedDestroys;
                job.BootstrapEvaluatorStack = bootstrapEvaluatorStack;
                job.BootstrapAttributeMutations = bootstrapAttributeMutations;
                job.ForceCardinalityFault = 1;
                handle = job.Schedule(handle);
            }
            handle = sessions.Dispose(handle);
            handle = recordedDestroys.Dispose(handle);
            handle = bootstrapEvaluatorStack.Dispose(handle);
            handle = bootstrapAttributeMutations.Dispose(handle);
            return batchMarkedAscs.Dispose(handle);
        }
    }

    /// <summary>
    /// 在单一 job 内执行全批先验证后发布；失败只写 Fault/zero-Ready 并记录标准 EndFixed teardown。
    /// </summary>
    internal struct GasStageBSpawnFinalizeJob : IJob
    {
        private const uint InitialGeneration = 1;
        private const int BitsPerWord = 64;

        public Entity Session;
        [ReadOnly] public NativeArray<Entity> BatchMarkedAscs;
        public NativeList<Entity> RecordedDestroyEntities;
        public byte ForceCardinalityFault;
        public EntityCommandBuffer EndFixed;
        [ReadOnly] public EntityStorageInfoLookup EntityStorage;
        // evaluator 会在定长 scratch 栈上写入 postfix 中间值，不能声明为只读。
        public NativeArray<float> BootstrapEvaluatorStack;
        public NativeArray<GasAttributeMutationRecord> BootstrapAttributeMutations;

        [ReadOnly] public ComponentLookup<GasSessionIdentity> SessionIdentities;
        [ReadOnly] public ComponentLookup<GasActiveSessionAuthority> ActiveSessionAuthorities;
        [ReadOnly] public ComponentLookup<GasSessionConfig> SessionConfigs;
        [ReadOnly] public ComponentLookup<GasDefinitionRegistry> Definitions;
        [ReadOnly] public ComponentLookup<GasCatalogRegistry> Catalogs;
        [ReadOnly] public ComponentLookup<GasScaleProfile> Profiles;
        [ReadOnly] public ComponentLookup<SimulationTickState> Ticks;
        public ComponentLookup<GasSessionLifecycle> SessionLifecycles;
        public ComponentLookup<GasSpawnBatchManifest> SpawnManifests;
        public ComponentLookup<SessionFaultLatch> FaultLatches;
        public ComponentLookup<BoundaryDrainState> Drains;
        public BufferLookup<BattleInstanceSlot> Battles;
        public BufferLookup<AscRegistrySlot> Registries;
        public BufferLookup<SpawnBatchMemberManifestSlot> SpawnManifestMembers;
        [ReadOnly] public BufferLookup<BoundaryCommandInbox> Inboxes;
        [ReadOnly] public BufferLookup<BoundaryCommandFrozenPayload> FrozenPayloads;
        [ReadOnly] public BufferLookup<GasRequestTerminalIntent> RequestTerminalIntents;
        public BufferLookup<BoundaryFactBuffer> BoundaryFacts;

        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        public ComponentLookup<GasSpawnBatchMarker> SpawnMarkers;
        [ReadOnly] public ComponentLookup<AscBattleMembership> Memberships;
        public ComponentLookup<AscLifecycle> AscLifecycles;
        [ReadOnly] public ComponentLookup<GasActorBinding> ActorBindings;
        [ReadOnly] public ComponentLookup<AscRandomState> RandomStates;
        public ComponentLookup<AscSlabHeads> SlabHeads;
        [ReadOnly] public ComponentLookup<GasPayloadRangeAllocatorState> PayloadRangeStates;
        public BufferLookup<AttributeValueSlot> AttributeValues;
        public BufferLookup<AttributeDirtyWord> AttributeDirtyWords;
        public BufferLookup<TagCountSlot> TagCounts;
        public BufferLookup<TagPresenceWord> TagPresenceWords;
        public BufferLookup<PendingAttributeInitialization> PendingAttributes;
        public BufferLookup<PendingTagInitialization> PendingTags;
        public BufferLookup<PendingGrantedAbilityInitialization> PendingAbilities;
        public BufferLookup<PendingInitialGameplayEffect> PendingInitialEffects;
        public BufferLookup<GrantedAbilitySlot> GrantedAbilities;
        [ReadOnly] public BufferLookup<AbilityActivationSlot> Activations;
        [ReadOnly] public BufferLookup<AbilityContinuationSlot> Continuations;
        [ReadOnly] public BufferLookup<AbilitySubscriptionSlot> Subscriptions;
        [ReadOnly] public BufferLookup<CooldownGateSlot> Cooldowns;
        [ReadOnly] public BufferLookup<ActivationOwnedContributionSlot> OwnedContributions;
        [ReadOnly] public BufferLookup<EmittedApplicationRefSlot> EmittedApplications;
        // initial effect 在 Ready 发布前直接写入 target-owned ActiveEffect slab。
        public BufferLookup<ActiveEffectSlot> ActiveEffects;
        [ReadOnly] public BufferLookup<GasPayloadRangeRecord> PayloadRangeRecords;
        [ReadOnly] public BufferLookup<GasPayloadValueSlot> PayloadValues;
        [ReadOnly] public BufferLookup<AttributeAggregatorSlot> Aggregators;
        [ReadOnly] public BufferLookup<LiveDependencySlot> Dependencies;
        [ReadOnly] public BufferLookup<LiveDependencyRouteSlot> Routes;
        [ReadOnly] public BufferLookup<PendingCommand> PendingCommands;

        /// <summary>
        /// 在 Kernel OnCreate 一次建立全部 World-local Lookup，避免热路径重复构造安全句柄。
        /// </summary>
        internal void InitializeLookups(ref SystemState state)
        {
            EntityStorage = state.GetEntityStorageInfoLookup();
            SessionIdentities = state.GetComponentLookup<GasSessionIdentity>(true);
            ActiveSessionAuthorities = state.GetComponentLookup<GasActiveSessionAuthority>(true);
            SessionConfigs = state.GetComponentLookup<GasSessionConfig>(true);
            Definitions = state.GetComponentLookup<GasDefinitionRegistry>(true);
            Catalogs = state.GetComponentLookup<GasCatalogRegistry>(true);
            Profiles = state.GetComponentLookup<GasScaleProfile>(true);
            Ticks = state.GetComponentLookup<SimulationTickState>(true);
            SessionLifecycles = state.GetComponentLookup<GasSessionLifecycle>();
            SpawnManifests = state.GetComponentLookup<GasSpawnBatchManifest>();
            FaultLatches = state.GetComponentLookup<SessionFaultLatch>();
            Drains = state.GetComponentLookup<BoundaryDrainState>();
            Battles = state.GetBufferLookup<BattleInstanceSlot>();
            Registries = state.GetBufferLookup<AscRegistrySlot>();
            SpawnManifestMembers = state.GetBufferLookup<SpawnBatchMemberManifestSlot>();
            Inboxes = state.GetBufferLookup<BoundaryCommandInbox>(true);
            FrozenPayloads = state.GetBufferLookup<BoundaryCommandFrozenPayload>(true);
            RequestTerminalIntents = state.GetBufferLookup<GasRequestTerminalIntent>(true);
            BoundaryFacts = state.GetBufferLookup<BoundaryFactBuffer>();
            InitializeAscLookups(ref state);
        }

        /// <summary>
        /// 初始化 ASC authority、slab、payload 与 outbox Lookup 子集。
        /// </summary>
        private void InitializeAscLookups(ref SystemState state)
        {
            AscIdentities = state.GetComponentLookup<GasAscIdentity>(true);
            SpawnMarkers = state.GetComponentLookup<GasSpawnBatchMarker>();
            Memberships = state.GetComponentLookup<AscBattleMembership>(true);
            AscLifecycles = state.GetComponentLookup<AscLifecycle>();
            ActorBindings = state.GetComponentLookup<GasActorBinding>(true);
            RandomStates = state.GetComponentLookup<AscRandomState>(true);
            SlabHeads = state.GetComponentLookup<AscSlabHeads>();
            PayloadRangeStates = state.GetComponentLookup<GasPayloadRangeAllocatorState>(true);
            AttributeValues = state.GetBufferLookup<AttributeValueSlot>();
            AttributeDirtyWords = state.GetBufferLookup<AttributeDirtyWord>();
            TagCounts = state.GetBufferLookup<TagCountSlot>();
            TagPresenceWords = state.GetBufferLookup<TagPresenceWord>();
            PendingAttributes = state.GetBufferLookup<PendingAttributeInitialization>();
            PendingTags = state.GetBufferLookup<PendingTagInitialization>();
            PendingAbilities = state.GetBufferLookup<PendingGrantedAbilityInitialization>();
            PendingInitialEffects = state.GetBufferLookup<PendingInitialGameplayEffect>();
            GrantedAbilities = state.GetBufferLookup<GrantedAbilitySlot>();
            Activations = state.GetBufferLookup<AbilityActivationSlot>(true);
            Continuations = state.GetBufferLookup<AbilityContinuationSlot>(true);
            Subscriptions = state.GetBufferLookup<AbilitySubscriptionSlot>(true);
            Cooldowns = state.GetBufferLookup<CooldownGateSlot>(true);
            OwnedContributions = state.GetBufferLookup<ActivationOwnedContributionSlot>(true);
            EmittedApplications = state.GetBufferLookup<EmittedApplicationRefSlot>(true);
            ActiveEffects = state.GetBufferLookup<ActiveEffectSlot>();
            PayloadRangeRecords = state.GetBufferLookup<GasPayloadRangeRecord>(true);
            PayloadValues = state.GetBufferLookup<GasPayloadValueSlot>(true);
            Aggregators = state.GetBufferLookup<AttributeAggregatorSlot>(true);
            Dependencies = state.GetBufferLookup<LiveDependencySlot>(true);
            Routes = state.GetBufferLookup<LiveDependencyRouteSlot>(true);
            PendingCommands = state.GetBufferLookup<PendingCommand>(true);
        }

        /// <summary>
        /// 在每次 Kernel 调度前刷新全部 Lookup 版本并让依赖追踪绑定当前 World 状态。
        /// </summary>
        internal void UpdateLookups(ref SystemState state)
        {
            EntityStorage.Update(ref state);
            SessionIdentities.Update(ref state);
            ActiveSessionAuthorities.Update(ref state);
            SessionConfigs.Update(ref state);
            Definitions.Update(ref state);
            Catalogs.Update(ref state);
            Profiles.Update(ref state);
            Ticks.Update(ref state);
            SessionLifecycles.Update(ref state);
            SpawnManifests.Update(ref state);
            FaultLatches.Update(ref state);
            Drains.Update(ref state);
            Battles.Update(ref state);
            Registries.Update(ref state);
            SpawnManifestMembers.Update(ref state);
            Inboxes.Update(ref state);
            FrozenPayloads.Update(ref state);
            RequestTerminalIntents.Update(ref state);
            BoundaryFacts.Update(ref state);
            UpdateAscLookups(ref state);
        }

        /// <summary>
        /// 刷新 ASC authority、slab、payload 与 outbox Lookup 子集。
        /// </summary>
        private void UpdateAscLookups(ref SystemState state)
        {
            AscIdentities.Update(ref state);
            SpawnMarkers.Update(ref state);
            Memberships.Update(ref state);
            AscLifecycles.Update(ref state);
            ActorBindings.Update(ref state);
            RandomStates.Update(ref state);
            SlabHeads.Update(ref state);
            PayloadRangeStates.Update(ref state);
            AttributeValues.Update(ref state);
            AttributeDirtyWords.Update(ref state);
            TagCounts.Update(ref state);
            TagPresenceWords.Update(ref state);
            PendingAttributes.Update(ref state);
            PendingTags.Update(ref state);
            PendingAbilities.Update(ref state);
            PendingInitialEffects.Update(ref state);
            GrantedAbilities.Update(ref state);
            Activations.Update(ref state);
            Continuations.Update(ref state);
            Subscriptions.Update(ref state);
            Cooldowns.Update(ref state);
            OwnedContributions.Update(ref state);
            EmittedApplications.Update(ref state);
            ActiveEffects.Update(ref state);
            PayloadRangeRecords.Update(ref state);
            PayloadValues.Update(ref state);
            Aggregators.Update(ref state);
            Dependencies.Update(ref state);
            Routes.Update(ref state);
            PendingCommands.Update(ref state);
        }

        /// <summary>
        /// 执行 cardinality fault 或唯一 Session finalize，两种维护模式互斥。
        /// </summary>
        public void Execute()
        {
            if (ForceCardinalityFault != 0)
            {
                FaultAndRecordTeardown(Session, GasStageBSpawnFaultReason.SessionCardinality);
                return;
            }
            if (!SessionLifecycles.HasComponent(Session))
            {
                FaultAndRecordTeardown(Session, GasStageBSpawnFaultReason.SessionLayout);
                return;
            }
            if (SessionLifecycles[Session].State != GasSessionLifecycleState.SpawnPending)
                return;

            var failure = ValidateSession(Session, out var snapshot);
            if (failure == GasStageBSpawnFaultReason.None)
                failure = ValidatePendingBatch(Session, in snapshot);
            if (failure == GasStageBSpawnFaultReason.None)
                PublishBatch(Session, in snapshot);
            else
                FaultAndRecordTeardown(Session, failure);
        }

        /// <summary>
        /// 验证唯一 Session 布局、状态、Catalog、profile、容量与 ReadyTick 边界。
        /// </summary>
        private GasStageBSpawnFaultReason ValidateSession(
            Entity session,
            out GasStageBSessionSnapshot snapshot)
        {
            snapshot = default;
            if (!HasSessionLayout(session))
                return GasStageBSpawnFaultReason.SessionLayout;

            snapshot.Identity = SessionIdentities[session];
            snapshot.Config = SessionConfigs[session];
            snapshot.Definitions = Definitions[session];
            snapshot.Catalog = Catalogs[session];
            snapshot.Profile = Profiles[session];
            snapshot.Tick = Ticks[session];
            snapshot.Lifecycle = SessionLifecycles[session];
            snapshot.Manifest = SpawnManifests[session];
            if (snapshot.Identity.SimulationEpoch == 0 || snapshot.Lifecycle.ActiveSpawnBatchId == 0 ||
                snapshot.Lifecycle.State != GasSessionLifecycleState.SpawnPending ||
                snapshot.Config.TickRate <= 0)
                return GasStageBSpawnFaultReason.SessionState;
            if (!HasValidManifestHeader(in snapshot.Manifest, in snapshot))
                return GasStageBSpawnFaultReason.SpawnBatchMismatch;
            var drain = Drains[session];
            if (drain.SimulationEpoch != snapshot.Identity.SimulationEpoch ||
                drain.OwnerKind != GasBoundaryOwnerKind.Session ||
                drain.OwnerStableId != snapshot.Identity.SimulationEpoch ||
                drain.OwnerGeneration == 0)
                return GasStageBSpawnFaultReason.EpochMismatch;
            if (snapshot.Tick.CurrentTick == ulong.MaxValue)
                return GasStageBSpawnFaultReason.TickOverflow;
            if (!GasStageBSpawnContract.HasValidProfile(in snapshot.Profile))
                return GasStageBSpawnFaultReason.ProfileInvalid;

            var failure = ValidateCatalog(in snapshot);
            if (failure != GasStageBSpawnFaultReason.None)
                return failure;
            return HasSessionCapacities(session, in snapshot.Profile)
                ? GasStageBSpawnFaultReason.None
                : GasStageBSpawnFaultReason.CapacityExceeded;
        }

        /// <summary>
        /// 判断 Session 是否具有 Runtime v1 完整固定布局。
        /// </summary>
        private bool HasSessionLayout(Entity session)
        {
            return SessionIdentities.HasComponent(session) && SessionConfigs.HasComponent(session) &&
                   ActiveSessionAuthorities.HasComponent(session) &&
                   Definitions.HasComponent(session) && Catalogs.HasComponent(session) &&
                   Profiles.HasComponent(session) && Ticks.HasComponent(session) &&
                   SessionLifecycles.HasComponent(session) && SpawnManifests.HasComponent(session) &&
                   FaultLatches.HasComponent(session) &&
                   Drains.HasComponent(session) && Battles.HasBuffer(session) && Registries.HasBuffer(session) &&
                   SpawnManifestMembers.HasBuffer(session) &&
                   Inboxes.HasBuffer(session) && FrozenPayloads.HasBuffer(session) &&
                   RequestTerminalIntents.HasBuffer(session) &&
                   BoundaryFacts.HasBuffer(session);
        }

        /// <summary>
        /// 验证不可变 SpawnBatch header 与当前 Session、lifecycle 及容量档位一致。
        /// </summary>
        private static bool HasValidManifestHeader(
            in GasSpawnBatchManifest manifest,
            in GasStageBSessionSnapshot snapshot)
        {
            return manifest.Pending == 1 && manifest.ContentHash != 0 &&
                   manifest.SimulationEpoch == snapshot.Identity.SimulationEpoch &&
                   manifest.SpawnBatchId == snapshot.Lifecycle.ActiveSpawnBatchId &&
                   manifest.ExpectedBattleCount >= 0 &&
                   manifest.ExpectedBattleCount <= snapshot.Profile.MaxBattleInstanceCount &&
                   manifest.ExpectedAscCount >= 0 &&
                   manifest.ExpectedAscCount <= snapshot.Profile.MaxAscRegistryCount &&
                   manifest.ExpectedAscCount <= snapshot.Profile.MaxSpawnBatchSize &&
                   manifest.ExpectedAttributeInitializationCount >= 0 &&
                   manifest.ExpectedTagInitializationCount >= 0 &&
                   manifest.ExpectedGrantedAbilityInitializationCount >= 0 &&
                   manifest.ExpectedInitialGameplayEffectCount >= 0;
        }

        /// <summary>
        /// 验证 Blob header、固定长度和 layout/tag hash 与安装快照一致。
        /// </summary>
        private static GasStageBSpawnFaultReason ValidateCatalog(in GasStageBSessionSnapshot snapshot)
        {
            if (!snapshot.Definitions.Catalog.IsCreated)
                return GasStageBSpawnFaultReason.CatalogInvalid;

            ref var catalog = ref snapshot.Definitions.Catalog.Value;
            var expectation = new GasCatalogValidationExpectation(
                snapshot.Definitions.SchemaVersion,
                snapshot.Definitions.SchemaHash,
                snapshot.Definitions.ContentHash,
                snapshot.Catalog.AttributeLayoutHash,
                snapshot.Catalog.TagCatalogHash);
            var result = GasDefinitionCatalogValidator.Validate(ref catalog, in expectation);
            if (!result.Succeeded || snapshot.Catalog.AttributeCount != catalog.AttributeLayout.Entries.Length ||
                snapshot.Catalog.TagCount != catalog.TagCatalog.Entries.Length)
                return GasStageBSpawnFaultReason.CatalogInvalid;

            return snapshot.Catalog.AttributeDirtyWordCount == WordCount(snapshot.Catalog.AttributeCount) &&
                   snapshot.Catalog.TagPresenceWordCount == WordCount(snapshot.Catalog.TagCount)
                ? GasStageBSpawnFaultReason.None
                : GasStageBSpawnFaultReason.CatalogInvalid;
        }

        /// <summary>
        /// 验证 Session 全部持久 buffers 已按具名 ScaleProfile 预热。
        /// </summary>
        private bool HasSessionCapacities(Entity session, in GasScaleProfile profile)
        {
            return Battles[session].Capacity >= profile.MaxBattleInstanceCount &&
                   Registries[session].Capacity >= profile.MaxAscRegistryCount &&
                   SpawnManifestMembers[session].Capacity >= profile.MaxAscRegistryCount &&
                   Inboxes[session].Capacity >= profile.MaxBoundaryCommandCount &&
                   FrozenPayloads[session].Capacity >= profile.MaxBoundaryCommandPayloadCount &&
                   RequestTerminalIntents[session].Capacity >= profile.MaxBoundaryCommandCount &&
                   BoundaryFacts[session].Capacity >= profile.MaxSessionBoundaryFactCount;
        }

        /// <summary>
        /// 验证 Battle、registry↔ASC、固定长度、容量、Pending 引用与全局 ConfigOrdinal。
        /// </summary>
        private GasStageBSpawnFaultReason ValidatePendingBatch(
            Entity session,
            in GasStageBSessionSnapshot snapshot)
        {
            var battles = Battles[session];
            var registry = Registries[session];
            if (RequestTerminalIntents[session].Length != 0)
                return GasStageBSpawnFaultReason.AuthorityWrittenBeforeFinalize;
            if (battles.Length > snapshot.Profile.MaxBattleInstanceCount ||
                registry.Length > snapshot.Profile.MaxAscRegistryCount ||
                registry.Length > snapshot.Profile.MaxSpawnBatchSize ||
                Inboxes[session].Length > snapshot.Profile.MaxBoundaryCommandCount)
                return GasStageBSpawnFaultReason.CapacityExceeded;

            var failure = ValidateManifest(session, battles, registry, in snapshot);
            if (failure != GasStageBSpawnFaultReason.None)
                return failure;
            failure = ValidateBattles(battles, registry, in snapshot);
            if (failure != GasStageBSpawnFaultReason.None)
                return failure;
            for (var index = 0; index < registry.Length; index++)
            {
                if (HasDuplicateOwner(registry, index))
                    return GasStageBSpawnFaultReason.RegistryInvalid;
                failure = ValidateAsc(registry[index], index, battles, in snapshot);
                if (failure != GasStageBSpawnFaultReason.None)
                    return failure;
            }
            return HasUniqueConfigOrdinals(registry)
                ? GasStageBSpawnFaultReason.None
                : GasStageBSpawnFaultReason.ConfigOrdinalInvalid;
        }

        /// <summary>
        /// 将当前 Pending 物理内容与录入时冻结的成员、规模与内容哈希做全量比对。
        /// </summary>
        private GasStageBSpawnFaultReason ValidateManifest(
            Entity session,
            DynamicBuffer<BattleInstanceSlot> battles,
            DynamicBuffer<AscRegistrySlot> registry,
            in GasStageBSessionSnapshot snapshot)
        {
            var manifest = snapshot.Manifest;
            var members = SpawnManifestMembers[session];
            if (battles.Length != manifest.ExpectedBattleCount ||
                registry.Length != manifest.ExpectedAscCount ||
                members.Length != manifest.ExpectedAscCount)
                return GasStageBSpawnFaultReason.SpawnBatchMismatch;

            var hasher = GasSpawnBatchManifestHasher.Create(in manifest);
            hasher.AddSessionAuthority(
                in snapshot.Config,
                in snapshot.Profile,
                in snapshot.Definitions,
                in snapshot.Catalog);
            AddBattlesToManifestHash(battles, ref hasher);
            if (!ValidateManifestMembers(
                    registry,
                    members,
                    in manifest,
                    ref hasher,
                    out var attributeCount,
                    out var tagCount,
                    out var abilityCount,
                    out var initialEffectCount))
                return GasStageBSpawnFaultReason.SpawnBatchMismatch;
            AddPendingValuesToManifestHash(registry, ref hasher);
            return attributeCount == manifest.ExpectedAttributeInitializationCount &&
                   tagCount == manifest.ExpectedTagInitializationCount &&
                   abilityCount == manifest.ExpectedGrantedAbilityInitializationCount &&
                   initialEffectCount == manifest.ExpectedInitialGameplayEffectCount &&
                   hasher.Finish() == manifest.ContentHash
                ? GasStageBSpawnFaultReason.None
                : GasStageBSpawnFaultReason.SpawnBatchMismatch;
        }

        /// <summary>
        /// 将当前 Battle 序列按录入时相同字段顺序加入 manifest hash。
        /// </summary>
        private static void AddBattlesToManifestHash(
            DynamicBuffer<BattleInstanceSlot> battles,
            ref GasSpawnBatchManifestHasher hasher)
        {
            for (var index = 0; index < battles.Length; index++)
            {
                var battle = battles[index];
                hasher.AddBattle(
                    battle.Handle,
                    battle.MemberStart,
                    battle.MemberCount,
                    battle.MembershipOrdinalRoot);
            }
        }

        /// <summary>
        /// 验证 manifest 成员与 registry/runtime 快照逐项一致并累计三类初始化总数。
        /// </summary>
        private bool ValidateManifestMembers(
            DynamicBuffer<AscRegistrySlot> registry,
            DynamicBuffer<SpawnBatchMemberManifestSlot> members,
            in GasSpawnBatchManifest manifest,
            ref GasSpawnBatchManifestHasher hasher,
            out int attributeCount,
            out int tagCount,
            out int abilityCount,
            out int initialEffectCount)
        {
            attributeCount = 0;
            tagCount = 0;
            abilityCount = 0;
            initialEffectCount = 0;
            for (var index = 0; index < members.Length; index++)
            {
                var member = members[index];
                var slot = registry[index];
                if (!ManifestMemberMatchesRuntime(
                        in member,
                        in slot,
                        index,
                        attributeCount,
                        tagCount,
                        abilityCount,
                        initialEffectCount,
                        in manifest))
                    return false;
                hasher.AddMember(in member);
                attributeCount += member.AttributeInitializationCount;
                tagCount += member.TagInitializationCount;
                abilityCount += member.GrantedAbilityInitializationCount;
                initialEffectCount += member.InitialGameplayEffectCount;
            }
            return true;
        }

        /// <summary>
        /// 验证一个冻结 member 与当前 ASC 身份、成员关系、binding、RNG、marker 和 ranges 一致。
        /// </summary>
        private bool ManifestMemberMatchesRuntime(
            in SpawnBatchMemberManifestSlot member,
            in AscRegistrySlot slot,
            int ordinal,
            int expectedAttributeStart,
            int expectedTagStart,
            int expectedAbilityStart,
            int expectedInitialEffectStart,
            in GasSpawnBatchManifest manifest)
        {
            var asc = member.ResolveRuntimeEntity();
            if (asc != slot.ResolveRuntimeEntity() || member.RegistryOrdinal != ordinal ||
                member.OwnerAsc != slot.OwnerAsc || member.BattleInstance != slot.BattleInstance ||
                !HasManifestMemberLayout(asc))
                return false;
            var identity = AscIdentities[asc];
            var membership = Memberships[asc];
            var binding = ActorBindings[asc];
            var random = RandomStates[asc];
            var marker = SpawnMarkers[asc];
            return identity.OwnerAsc == member.OwnerAsc &&
                   membership.BattleInstance == member.BattleInstance &&
                   membership.ScenarioUnitId == member.ScenarioUnitId &&
                   membership.SideId == member.SideId && membership.TeamId == member.TeamId &&
                   membership.MembershipOrdinal == member.MembershipOrdinal &&
                   binding.OwnerActorStableId == member.OwnerActorStableId &&
                   binding.AvatarActorStableId == member.AvatarActorStableId &&
                   binding.BindingGeneration == member.ActorBindingGeneration &&
                   random.State0 == member.RandomState0 && random.State1 == member.RandomState1 &&
                   SpawnMarkers.IsComponentEnabled(asc) &&
                   marker.SpawnBatchId == manifest.SpawnBatchId &&
                   marker.SimulationEpoch == manifest.SimulationEpoch &&
                   marker.ManifestHash == manifest.ContentHash && marker.RegistryOrdinal == ordinal &&
                   HasExpectedManifestRanges(
                       asc,
                       in member,
                       expectedAttributeStart,
                       expectedTagStart,
                       expectedAbilityStart,
                       expectedInitialEffectStart);
        }

        /// <summary>
        /// 判断 manifest 校验所需的 ASC components 与 Pending buffers 是否仍完整存在。
        /// </summary>
        private bool HasManifestMemberLayout(Entity asc)
        {
            return AscIdentities.HasComponent(asc) && Memberships.HasComponent(asc) &&
                   ActorBindings.HasComponent(asc) && RandomStates.HasComponent(asc) &&
                   SpawnMarkers.HasComponent(asc) && PendingAttributes.HasBuffer(asc) &&
                   PendingTags.HasBuffer(asc) && PendingAbilities.HasBuffer(asc) &&
                   PendingInitialEffects.HasBuffer(asc);
        }

        /// <summary>
        /// 验证 manifest ranges 连续覆盖当前每个 ASC 的 Pending buffers。
        /// </summary>
        private bool HasExpectedManifestRanges(
            Entity asc,
            in SpawnBatchMemberManifestSlot member,
            int expectedAttributeStart,
            int expectedTagStart,
            int expectedAbilityStart,
            int expectedInitialEffectStart)
        {
            return member.AttributeInitializationStart == expectedAttributeStart &&
                   member.AttributeInitializationCount == PendingAttributes[asc].Length &&
                   member.TagInitializationStart == expectedTagStart &&
                   member.TagInitializationCount == PendingTags[asc].Length &&
                   member.GrantedAbilityInitializationStart == expectedAbilityStart &&
                   member.GrantedAbilityInitializationCount == PendingAbilities[asc].Length &&
                   member.InitialGameplayEffectStart == expectedInitialEffectStart &&
                   member.InitialGameplayEffectCount == PendingInitialEffects[asc].Length;
        }

        /// <summary>
        /// 按 Attribute、Tag、Ability 三个全局规范序列加入当前 Pending 初始化内容。
        /// </summary>
        private void AddPendingValuesToManifestHash(
            DynamicBuffer<AscRegistrySlot> registry,
            ref GasSpawnBatchManifestHasher hasher)
        {
            for (var ownerIndex = 0; ownerIndex < registry.Length; ownerIndex++)
            {
                var values = PendingAttributes[registry[ownerIndex].ResolveRuntimeEntity()];
                for (var index = 0; index < values.Length; index++)
                    hasher.Add(values[index]);
            }
            for (var ownerIndex = 0; ownerIndex < registry.Length; ownerIndex++)
            {
                var values = PendingTags[registry[ownerIndex].ResolveRuntimeEntity()];
                for (var index = 0; index < values.Length; index++)
                    hasher.Add(values[index]);
            }
            for (var ownerIndex = 0; ownerIndex < registry.Length; ownerIndex++)
            {
                var values = PendingAbilities[registry[ownerIndex].ResolveRuntimeEntity()];
                for (var index = 0; index < values.Length; index++)
                    hasher.Add(values[index]);
            }
            for (var ownerIndex = 0; ownerIndex < registry.Length; ownerIndex++)
            {
                var values = PendingInitialEffects[registry[ownerIndex].ResolveRuntimeEntity()];
                for (var index = 0; index < values.Length; index++)
                    hasher.Add(values[index]);
            }
        }

        /// <summary>
        /// 验证 Battle slots 为 live SpawnPending 且成员 ranges 连续覆盖 registry。
        /// </summary>
        private static GasStageBSpawnFaultReason ValidateBattles(
            DynamicBuffer<BattleInstanceSlot> battles,
            DynamicBuffer<AscRegistrySlot> registry,
            in GasStageBSessionSnapshot snapshot)
        {
            var nextStart = 0;
            for (var index = 0; index < battles.Length; index++)
            {
                var battle = battles[index];
                if (battle.Header.StorageState != GasSlabSlotState.Live ||
                    battle.Header.Generation != battle.Handle.BattleGeneration || !battle.Handle.IsValid ||
                    battle.Header.NextFreeIndex != -1 || battle.MembershipOrdinalRoot < 0 ||
                    battle.Handle.SimulationEpoch != snapshot.Identity.SimulationEpoch ||
                    battle.BattleInstanceId != battle.Handle.BattleStableId ||
                    battle.State != GasBattleInstanceState.SpawnPending || battle.ReadyMemberCount != 0 ||
                    battle.MemberStart != nextStart || battle.MemberCount < 0 ||
                    battle.MemberCount > registry.Length - battle.MemberStart ||
                    battle.MemberCount > 0 &&
                    battle.MembershipOrdinalRoot > int.MaxValue - (battle.MemberCount - 1))
                    return GasStageBSpawnFaultReason.BattleInvalid;
                for (var prior = 0; prior < index; prior++)
                    if (battles[prior].Handle == battle.Handle)
                        return GasStageBSpawnFaultReason.BattleInvalid;
                for (var offset = 0; offset < battle.MemberCount; offset++)
                    if (registry[battle.MemberStart + offset].BattleInstance != battle.Handle)
                        return GasStageBSpawnFaultReason.BattleInvalid;
                nextStart += battle.MemberCount;
            }
            return nextStart == registry.Length
                ? GasStageBSpawnFaultReason.None
                : GasStageBSpawnFaultReason.BattleInvalid;
        }

        /// <summary>
        /// 验证一个 registry slot 与 ASC identity、batch、membership、布局和 Pending authority。
        /// </summary>
        private GasStageBSpawnFaultReason ValidateAsc(
            AscRegistrySlot slot,
            int ordinal,
            DynamicBuffer<BattleInstanceSlot> battles,
            in GasStageBSessionSnapshot snapshot)
        {
            if (slot.Header.StorageState != GasSlabSlotState.Live ||
                slot.Header.Generation != slot.OwnerAsc.AscGeneration || !slot.OwnerAsc.IsValid ||
                slot.Header.NextFreeIndex != -1 ||
                slot.RegistryOrdinal != ordinal || slot.SpawnBatchId != snapshot.Lifecycle.ActiveSpawnBatchId ||
                slot.ReadyTick != 0 || slot.State != GasAscRegistryState.Pending ||
                slot.BattleInstance.SimulationEpoch != snapshot.Identity.SimulationEpoch)
                return GasStageBSpawnFaultReason.RegistryInvalid;

            var asc = slot.ResolveRuntimeEntity();
            if (!HasAscLayout(asc))
                return GasStageBSpawnFaultReason.AscLayoutInvalid;
            var identity = AscIdentities[asc];
            var membership = Memberships[asc];
            var binding = ActorBindings[asc];
            var lifecycle = AscLifecycles[asc];
            var drain = Drains[asc];
            var payloadRanges = PayloadRangeStates[asc];
            if (identity.SimulationEpoch != snapshot.Identity.SimulationEpoch || identity.OwnerAsc != slot.OwnerAsc ||
                membership.BattleInstance != slot.BattleInstance || lifecycle.State != GasAscLifecycleState.Pending ||
                lifecycle.ReadyTick != 0 || !HasExpectedMembership(battles, ordinal, membership.MembershipOrdinal) ||
                 binding.BindingGeneration == 0 ||
                drain.SimulationEpoch != snapshot.Identity.SimulationEpoch ||
                drain.OwnerKind != GasBoundaryOwnerKind.Asc ||
                drain.OwnerStableId != slot.OwnerAsc.AscStableId ||
                drain.OwnerGeneration != slot.OwnerAsc.AscGeneration ||
                drain.Phase != GasBoundaryDrainPhase.Idle ||
                drain.NextOwnerSequence != 1 ||
                drain.InFlightWatermark != 0 ||
                BoundaryFacts[asc].Length != 0 ||
                payloadRanges.SimulationEpoch != snapshot.Identity.SimulationEpoch ||
                payloadRanges.OwnerAsc != slot.OwnerAsc || !IsEmpty(payloadRanges.Records) ||
                payloadRanges.ValueHighWater != 0 ||
                !HasEmptySlabHeads(SlabHeads[asc]))
                return GasStageBSpawnFaultReason.AscIdentityInvalid;
            if (!HasFixedLengths(asc, in snapshot.Catalog))
                return GasStageBSpawnFaultReason.FixedBufferLengthMismatch;
            if (!HasAscCapacities(asc, in snapshot.Profile))
                return GasStageBSpawnFaultReason.CapacityExceeded;
            if (!HasZeroAuthority(asc))
                return GasStageBSpawnFaultReason.AuthorityWrittenBeforeFinalize;

            ref var catalog = ref snapshot.Definitions.Catalog.Value;
            return ValidatePending(
                asc,
                in identity,
                in membership,
                in binding,
                ref catalog,
                snapshot.Identity.SimulationEpoch);
        }

        /// <summary>
        /// 判断 ASC 固定组件及全部预挂载 buffers 是否齐全。
        /// </summary>
        private bool HasAscLayout(Entity asc)
        {
            return AscIdentities.HasComponent(asc) && Memberships.HasComponent(asc) &&
                   SpawnMarkers.HasComponent(asc) &&
                   AscLifecycles.HasComponent(asc) && ActorBindings.HasComponent(asc) &&
                   RandomStates.HasComponent(asc) && SlabHeads.HasComponent(asc) &&
                   PayloadRangeStates.HasComponent(asc) && Drains.HasComponent(asc) &&
                   AttributeValues.HasBuffer(asc) && AttributeDirtyWords.HasBuffer(asc) &&
                   TagCounts.HasBuffer(asc) && TagPresenceWords.HasBuffer(asc) &&
                   PendingAttributes.HasBuffer(asc) && PendingTags.HasBuffer(asc) &&
                   PendingAbilities.HasBuffer(asc) && PendingInitialEffects.HasBuffer(asc) &&
                   GrantedAbilities.HasBuffer(asc) &&
                   Activations.HasBuffer(asc) && Continuations.HasBuffer(asc) &&
                   Subscriptions.HasBuffer(asc) && Cooldowns.HasBuffer(asc) &&
                   OwnedContributions.HasBuffer(asc) && EmittedApplications.HasBuffer(asc) &&
                   ActiveEffects.HasBuffer(asc) && PayloadRangeRecords.HasBuffer(asc) &&
                   PayloadValues.HasBuffer(asc) && Aggregators.HasBuffer(asc) &&
                   Dependencies.HasBuffer(asc) && Routes.HasBuffer(asc) &&
                   PendingCommands.HasBuffer(asc) && BoundaryFacts.HasBuffer(asc);
        }

        /// <summary>
        /// 验证 registry ordinal 对应 Battle range 的规范 membership ordinal。
        /// </summary>
        private static bool HasExpectedMembership(
            DynamicBuffer<BattleInstanceSlot> battles,
            int registryOrdinal,
            int membershipOrdinal)
        {
            for (var index = 0; index < battles.Length; index++)
            {
                var battle = battles[index];
                if (registryOrdinal < battle.MemberStart)
                    continue;
                var offset = registryOrdinal - battle.MemberStart;
                if (offset >= battle.MemberCount)
                    continue;
                return battle.MembershipOrdinalRoot >= 0 &&
                       battle.MembershipOrdinalRoot <= int.MaxValue - offset &&
                       membershipOrdinal == battle.MembershipOrdinalRoot + offset;
            }
            return false;
        }

        /// <summary>
        /// 判断 owner handle 是否已在更早 registry ordinal 出现。
        /// </summary>
        private static bool HasDuplicateOwner(DynamicBuffer<AscRegistrySlot> registry, int index)
        {
            for (var prior = 0; prior < index; prior++)
                if (registry[prior].OwnerAsc == registry[index].OwnerAsc)
                    return true;
            return false;
        }

        /// <summary>
        /// 验证所有 non-compacting slab heads 仍处于未分配初始状态。
        /// </summary>
        private static bool HasEmptySlabHeads(AscSlabHeads heads)
        {
            return IsEmpty(heads.GrantedAbility) && IsEmpty(heads.AbilityActivation) &&
                   IsEmpty(heads.AbilityContinuation) && IsEmpty(heads.AbilitySubscription) &&
                   IsEmpty(heads.CooldownGate) && IsEmpty(heads.ActivationOwnedContribution) &&
                   IsEmpty(heads.EmittedApplicationRef) && IsEmpty(heads.ActiveEffect) &&
                   IsEmpty(heads.AttributeAggregator) && IsEmpty(heads.LiveDependency) &&
                   IsEmpty(heads.LiveDependencyRoute) && IsEmpty(heads.PendingCommand);
        }

        /// <summary>
        /// 判断单个 slab head 是否没有 free-list 节点与已分配 high-water。
        /// </summary>
        private static bool IsEmpty(GasSlabHead head)
        {
            return head.FreeHeadIndex == -1 && head.HighWater == 0 && head.FreeCount == 0;
        }

        /// <summary>
        /// 验证 Catalog 固定 buffers 的长度与 registry 快照精确相等。
        /// </summary>
        private bool HasFixedLengths(Entity asc, in GasCatalogRegistry catalog)
        {
            return AttributeValues[asc].Length == catalog.AttributeCount &&
                   AttributeDirtyWords[asc].Length == catalog.AttributeDirtyWordCount &&
                   TagCounts[asc].Length == catalog.TagCount &&
                   TagPresenceWords[asc].Length == catalog.TagPresenceWordCount;
        }

        /// <summary>
        /// 验证三类 Pending 的 index/definition 与每类非负递增 ConfigOrdinal。
        /// </summary>
        private GasStageBSpawnFaultReason ValidatePending(
            Entity asc,
            in GasAscIdentity identity,
            in AscBattleMembership membership,
            in GasActorBinding binding,
            ref GasDefinitionCatalogBlob catalog,
            ulong simulationEpoch)
        {
            if (!ValidAttributes(PendingAttributes[asc], ref catalog) ||
                !ValidTags(PendingTags[asc], ref catalog) ||
                !ValidInitialEffects(
                    PendingInitialEffects[asc],
                    in identity,
                    in membership,
                    in binding,
                    ref catalog,
                    simulationEpoch))
                return GasStageBSpawnFaultReason.PendingInitializationInvalid;
            return ValidAbilities(PendingAbilities[asc], ref catalog)
                ? GasStageBSpawnFaultReason.None
                : GasStageBSpawnFaultReason.DefinitionInvalid;
        }

        /// <summary>
        /// 验证 Pending Attribute dense index；Catalog 默认值仍会对全部属性完整写入。
        /// </summary>
        private static bool ValidAttributes(
            DynamicBuffer<PendingAttributeInitialization> values,
            ref GasDefinitionCatalogBlob catalog)
        {
            var previous = -1;
            for (var index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (value.LayoutIndex < 0 || value.LayoutIndex >= catalog.AttributeLayout.Entries.Length ||
                    catalog.AttributeLayout.Entries[value.LayoutIndex].LayoutIndex != value.LayoutIndex ||
                    value.ConfigOrdinal <= previous ||
                    value.HasExplicitValue > 1 ||
                    value.HasExplicitValue != 0 &&
                    (float.IsNaN(value.BaseValue) || float.IsInfinity(value.BaseValue) ||
                     float.IsNaN(value.CurrentValue) || float.IsInfinity(value.CurrentValue)))
                    return false;
                previous = value.ConfigOrdinal;
            }
            return true;
        }

        /// <summary>
        /// 验证 Pending Tag dense index 与 ConfigOrdinal 规范顺序。
        /// </summary>
        private static bool ValidTags(
            DynamicBuffer<PendingTagInitialization> values,
            ref GasDefinitionCatalogBlob catalog)
        {
            var previous = -1;
            for (var index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (value.LayoutIndex < 0 || value.LayoutIndex >= catalog.TagCatalog.Entries.Length ||
                    catalog.TagCatalog.Entries[value.LayoutIndex].TagIndex != value.LayoutIndex ||
                    value.ConfigOrdinal <= previous)
                    return false;
                previous = value.ConfigOrdinal;
            }
            return true;
        }

        /// <summary>
        /// 验证 Pending Ability definition index 与 ConfigOrdinal 规范顺序。
        /// </summary>
        private static bool ValidAbilities(
            DynamicBuffer<PendingGrantedAbilityInitialization> values,
            ref GasDefinitionCatalogBlob catalog)
        {
            var previous = -1;
            for (var index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (value.LayoutIndex < 0 || value.LayoutIndex >= catalog.Abilities.Length ||
                    catalog.Abilities[value.LayoutIndex].DefinitionId <= 0 ||
                    value.ConfigOrdinal <= previous)
                    return false;
                previous = value.ConfigOrdinal;
            }
            return true;
        }

        /// <summary>
        /// 验证 ASC Pending 初始 GameplayEffect 的闭世界定义、目标和因果身份。
        /// </summary>
        private static bool ValidInitialEffects(
            DynamicBuffer<PendingInitialGameplayEffect> values,
            in GasAscIdentity identity,
            in AscBattleMembership membership,
            in GasActorBinding binding,
            ref GasDefinitionCatalogBlob catalog,
            ulong simulationEpoch)
        {
            var previous = -1;
            for (var index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (!GasDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                        ref catalog,
                        value.DefinitionId,
                        out var definitionIndex))
                    return false;
                var definition = catalog.GameplayEffects[definitionIndex];
                if (value.DefinitionId <= 0 ||
                    definition.TargetPolicy.LogicalTarget != GasLogicalTargetPolicy.Self ||
                    definition.TargetPolicy.Spatial != GasSpatialTargetPolicy.None ||
                    definition.TargetPolicy.Life == GasTargetLifePolicy.RequireDead ||
                    definition.ApplicationRequirementRange.Count != 0 ||
                    definition.OngoingRequirementRange.Count != 0 ||
                    definition.ImmunityRequirementRange.Count != 0 ||
                    definition.CaptureRange.Count != 0 ||
                    definition.ValueViewRange.Count != 0 ||
                    definition.RequiredValueViews != GasAttributeValueViewMask.None ||
                    definition.SetByCallerRange.Count != 0 ||
                    definition.TargetDataRange.Count != 0 ||
                    definition.EffectContextFieldRange.Count != 0 ||
                    definition.DirectEffectProgramRange.Count != 0 ||
                    definition.RemovalRequirementRange.Count != 0 ||
                    value.ConfigOrdinal <= previous || value.CausalityId == 0 ||
                    value.Target.Kind != GasBoundaryTargetKind.Asc ||
                    !value.Target.TargetAsc.IsValid ||
                    !value.Target.TargetAsc.Equals(identity.OwnerAsc) ||
                    !value.Target.BattleInstance.Equals(membership.BattleInstance) ||
                    value.Target.ResolutionPolicy != GasBoundaryTargetResolutionPolicy.ResolveAtConsume ||
                    value.Target.SimulationEpoch != simulationEpoch ||
                    value.Target.BattleInstance.SimulationEpoch != simulationEpoch ||
                    !ValidateInitialAvatar(in value.Target, in binding, definition.TargetPolicy.Avatar))
                    return false;
                previous = value.ConfigOrdinal;
            }
            return true;
        }

        /// <summary>
        /// 验证初始 self effect 的 Avatar policy 不会把未冻结 binding 静默降级。
        /// </summary>
        private static bool ValidateInitialAvatar(
            in BoundaryTargetRef target,
            in GasActorBinding binding,
            GasAvatarTargetPolicy policy)
        {
            var hasAvatar = target.TargetAvatarStableId != 0 ||
                            target.TargetAvatarBindingGeneration != 0;
            if (policy == GasAvatarTargetPolicy.FollowAsc)
                return !hasAvatar;
            if (policy != GasAvatarTargetPolicy.RequireSameAvatar || !hasAvatar)
                return false;
            return binding.AvatarActorStableId != 0 && binding.BindingGeneration != 0 &&
                   target.TargetAvatarStableId == binding.AvatarActorStableId &&
                   target.TargetAvatarBindingGeneration == binding.BindingGeneration;
        }

        /// <summary>
        /// 验证全批三类 Pending 共享的 ConfigOrdinal 不重复。
        /// </summary>
        private bool HasUniqueConfigOrdinals(DynamicBuffer<AscRegistrySlot> registry)
        {
            for (var ownerIndex = 0; ownerIndex < registry.Length; ownerIndex++)
            {
                var asc = registry[ownerIndex].ResolveRuntimeEntity();
                var attributes = PendingAttributes[asc];
                for (var index = 0; index < attributes.Length; index++)
                    if (CountOrdinal(registry, attributes[index].ConfigOrdinal) != 1)
                        return false;
                var tags = PendingTags[asc];
                for (var index = 0; index < tags.Length; index++)
                    if (CountOrdinal(registry, tags[index].ConfigOrdinal) != 1)
                        return false;
                var abilities = PendingAbilities[asc];
                for (var index = 0; index < abilities.Length; index++)
                    if (CountOrdinal(registry, abilities[index].ConfigOrdinal) != 1)
                        return false;
                var initialEffects = PendingInitialEffects[asc];
                for (var index = 0; index < initialEffects.Length; index++)
                    if (CountOrdinal(registry, initialEffects[index].ConfigOrdinal) != 1)
                        return false;
            }
            return true;
        }

        /// <summary>
        /// 统计一个 ConfigOrdinal 在 batch 全部 Pending buffers 的总次数。
        /// </summary>
        private int CountOrdinal(DynamicBuffer<AscRegistrySlot> registry, int ordinal)
        {
            var count = 0;
            for (var ownerIndex = 0; ownerIndex < registry.Length; ownerIndex++)
            {
                var asc = registry[ownerIndex].ResolveRuntimeEntity();
                var attributes = PendingAttributes[asc];
                for (var index = 0; index < attributes.Length; index++)
                    if (attributes[index].ConfigOrdinal == ordinal)
                        count++;
                var tags = PendingTags[asc];
                for (var index = 0; index < tags.Length; index++)
                    if (tags[index].ConfigOrdinal == ordinal)
                        count++;
                var abilities = PendingAbilities[asc];
                for (var index = 0; index < abilities.Length; index++)
                    if (abilities[index].ConfigOrdinal == ordinal)
                        count++;
                var initialEffects = PendingInitialEffects[asc];
                for (var index = 0; index < initialEffects.Length; index++)
                    if (initialEffects[index].ConfigOrdinal == ordinal)
                        count++;
            }
            return count;
        }

        /// <summary>
        /// 验证 finalize 前核心 authority、派生位图与运行时 slabs 保持零写入。
        /// </summary>
        private bool HasZeroAuthority(Entity asc)
        {
            var attributes = AttributeValues[asc];
            for (var index = 0; index < attributes.Length; index++)
                if (attributes[index].Base != 0f || attributes[index].Current != 0f ||
                    attributes[index].Revision != 0)
                    return false;
            var dirty = AttributeDirtyWords[asc];
            for (var index = 0; index < dirty.Length; index++)
                if (dirty[index].Value != 0)
                    return false;
            var tags = TagCounts[asc];
            for (var index = 0; index < tags.Length; index++)
                if (tags[index].ExactCount != 0 || tags[index].InclusiveCount != 0)
                    return false;
            var presence = TagPresenceWords[asc];
            for (var index = 0; index < presence.Length; index++)
                if (presence[index].Value != 0)
                    return false;
            return HasEmptyRuntimeBuffers(asc);
        }

        /// <summary>
        /// 验证 gameplay slabs 与 PendingCommand 在 finalize 前长度均为零。
        /// </summary>
        private bool HasEmptyRuntimeBuffers(Entity asc)
        {
            return GrantedAbilities[asc].Length == 0 && Activations[asc].Length == 0 &&
                   Continuations[asc].Length == 0 && Subscriptions[asc].Length == 0 &&
                   Cooldowns[asc].Length == 0 && OwnedContributions[asc].Length == 0 &&
                   EmittedApplications[asc].Length == 0 && ActiveEffects[asc].Length == 0 &&
                   PayloadRangeRecords[asc].Length == 0 && PayloadValues[asc].Length == 0 &&
                   Aggregators[asc].Length == 0 && Dependencies[asc].Length == 0 &&
                   Routes[asc].Length == 0 && PendingCommands[asc].Length == 0;
        }

        /// <summary>
        /// 验证 ASC 所有 buffers 的实际 capacity 满足具名 ScaleProfile。
        /// </summary>
        private bool HasAscCapacities(Entity asc, in GasScaleProfile profile)
        {
            var attributes = PendingAttributes[asc];
            var tags = PendingTags[asc];
            var abilities = PendingAbilities[asc];
            return attributes.Capacity >= profile.MaxPendingAttributeInitializationCount &&
                   attributes.Length <= profile.MaxPendingAttributeInitializationCount &&
                   tags.Capacity >= profile.MaxPendingTagInitializationCount &&
                   tags.Length <= profile.MaxPendingTagInitializationCount &&
                   abilities.Capacity >= profile.MaxPendingGrantedAbilityInitializationCount &&
                   abilities.Length <= profile.MaxPendingGrantedAbilityInitializationCount &&
                   PendingInitialEffects[asc].Capacity >= profile.MaxEffectOperationCount &&
                   PendingInitialEffects[asc].Length <= profile.MaxEffectOperationCount &&
                   abilities.Length <= profile.MaxGrantedAbilityCount &&
                   HasAbilityCapacities(asc, in profile) && HasEffectCapacities(asc, in profile) &&
                   PendingCommands[asc].Capacity >= profile.MaxPendingCommandCount &&
                   BoundaryFacts[asc].Capacity >= profile.MaxAscBoundaryFactCount;
        }

        /// <summary>
        /// 验证 Ability、ledger 与 emission buffers 容量。
        /// </summary>
        private bool HasAbilityCapacities(Entity asc, in GasScaleProfile profile)
        {
            return GrantedAbilities[asc].Capacity >= profile.MaxGrantedAbilityCount &&
                   Activations[asc].Capacity >= profile.MaxAbilityActivationCount &&
                   Continuations[asc].Capacity >= profile.MaxAbilityContinuationCount &&
                   Subscriptions[asc].Capacity >= profile.MaxAbilitySubscriptionCount &&
                   Cooldowns[asc].Capacity >= profile.MaxCooldownGateCount &&
                   OwnedContributions[asc].Capacity >= profile.MaxActivationOwnedContributionCount &&
                   EmittedApplications[asc].Capacity >= profile.MaxEmittedApplicationRefCount;
        }

        /// <summary>
        /// 验证 Effect、payload/capture、aggregator 与 live dependency buffers 容量。
        /// </summary>
        private bool HasEffectCapacities(Entity asc, in GasScaleProfile profile)
        {
            return ActiveEffects[asc].Capacity >= profile.MaxActiveEffectCount &&
                   PayloadRangeRecords[asc].Capacity >= profile.MaxPayloadRangeRecordCount &&
                   PayloadValues[asc].Capacity >= profile.MaxPayloadValueCount &&
                   Aggregators[asc].Capacity >= profile.MaxAttributeAggregatorCount &&
                   Dependencies[asc].Capacity >= profile.MaxLiveDependencyCount &&
                   Routes[asc].Capacity >= profile.MaxLiveDependencyRouteCount;
        }

        /// <summary>
        /// 将已部分执行的 SpawnBatch 恢复为 Pending canonical state，保证失败路径零 Ready、零 authority。
        /// </summary>
        private void ResetBatchAuthority(DynamicBuffer<AscRegistrySlot> registry)
        {
            for (var index = 0; index < registry.Length; index++)
                ResetAscAuthority(registry[index].ResolveRuntimeEntity());
        }

        /// <summary>
        /// 清除单个 ASC 在 ApplyAsc 期间可能写入的全部 gameplay authority 与 Boundary outbox。
        /// </summary>
        private void ResetAscAuthority(Entity asc)
        {
            ResetAttributeAuthority(asc);
            ResetTagAuthority(asc);
            ClearRuntimeBuffers(asc);
            SlabHeads[asc] = AscSlabHeads.CreateEmpty();
            BoundaryFacts[asc].Clear();
            var drain = Drains[asc];
            drain.NextOwnerSequence = 1;
            drain.Phase = GasBoundaryDrainPhase.Idle;
            drain.BatchId = 0;
            drain.InFlightWatermark = 0;
            Drains[asc] = drain;
            if (AscLifecycles.HasComponent(asc))
                AscLifecycles[asc] = new AscLifecycle { State = GasAscLifecycleState.Pending };
            if (SpawnMarkers.HasComponent(asc))
                SpawnMarkers.SetComponentEnabled(asc, true);
        }

        /// <summary>
        /// 将 Attribute 值与 dirty 位图恢复为 SpawnPending 的全零基线。
        /// </summary>
        private void ResetAttributeAuthority(Entity asc)
        {
            var attributes = AttributeValues[asc];
            for (var index = 0; index < attributes.Length; index++)
                attributes[index] = default;
            var dirty = AttributeDirtyWords[asc];
            for (var index = 0; index < dirty.Length; index++)
                dirty[index] = default;
        }

        /// <summary>
        /// 将 Tag exact/inclusive 计数与 presence 位图恢复为零基线。
        /// </summary>
        private void ResetTagAuthority(Entity asc)
        {
            var tags = TagCounts[asc];
            for (var index = 0; index < tags.Length; index++)
                tags[index] = default;
            var presence = TagPresenceWords[asc];
            for (var index = 0; index < presence.Length; index++)
                presence[index] = default;
        }

        /// <summary>
        /// 清空所有可变 runtime buffers，释放本轮尚未对外可见的临时槽位。
        /// </summary>
        private void ClearRuntimeBuffers(Entity asc)
        {
            GrantedAbilities[asc].Clear();
            ActiveEffects[asc].Clear();
            PendingAttributes[asc].Clear();
            PendingTags[asc].Clear();
            PendingAbilities[asc].Clear();
            PendingInitialEffects[asc].Clear();
        }

        /// <summary>
        /// 全批验证成功后写最小 bootstrap authority，再统一发布所有 Ready 状态。
        /// </summary>
        private void PublishBatch(Entity session, in GasStageBSessionSnapshot snapshot)
        {
            var readyTick = snapshot.Tick.CurrentTick + 1;
            var registry = Registries[session];
            ref var catalog = ref snapshot.Definitions.Catalog.Value;
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (!ApplyAsc(
                        slot.ResolveRuntimeEntity(),
                        ref catalog,
                        in snapshot,
                        readyTick))
                {
                    ResetBatchAuthority(registry);
                    FaultAndRecordTeardown(session, GasStageBSpawnFaultReason.PendingInitializationInvalid);
                    return;
                }
            }

            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                var asc = slot.ResolveRuntimeEntity();
                CommitReadyAsc(asc, readyTick);
                slot.ReadyTick = readyTick;
                slot.State = GasAscRegistryState.Ready;
                registry[index] = slot;
            }

            var battles = Battles[session];
            for (var index = 0; index < battles.Length; index++)
            {
                var battle = battles[index];
                battle.ReadyMemberCount = battle.MemberCount;
                battle.State = GasBattleInstanceState.Ready;
                battles[index] = battle;
            }
            SpawnManifestMembers[session].Clear();
            var manifest = SpawnManifests[session];
            manifest.Pending = 0;
            SpawnManifests[session] = manifest;
            SessionLifecycles[session] = new GasSessionLifecycle
            {
                State = GasSessionLifecycleState.Ready,
                ActiveSpawnBatchId = 0,
            };
        }

        /// <summary>
        /// 在 Ready 可见性发布前完成一个 ASC 的完整 bootstrap transaction。
        /// </summary>
        private bool ApplyAsc(
            Entity asc,
            ref GasDefinitionCatalogBlob catalog,
            in GasStageBSessionSnapshot snapshot,
            ulong readyTick)
        {
            ApplyAllAttributeDefaults(asc, ref catalog);
            ApplyTags(asc, ref catalog);
            ApplyAbilities(asc, ref catalog, in snapshot);
            if (!TryApplyInitialEffects(asc, ref catalog, in snapshot, readyTick))
                return false;
            return true;
        }

        /// <summary>
        /// 在全批 Apply 成功后提交单个 ASC 的 Ready lifecycle，并清除本次 bootstrap 输入。
        /// </summary>
        private void CommitReadyAsc(Entity asc, ulong readyTick)
        {
            PendingAttributes[asc].Clear();
            PendingTags[asc].Clear();
            PendingAbilities[asc].Clear();
            PendingInitialEffects[asc].Clear();
            SpawnMarkers.SetComponentEnabled(asc, false);
            AscLifecycles[asc] = new AscLifecycle
            {
                State = GasAscLifecycleState.Ready,
                ReadyTick = readyTick,
            };
        }

        /// <summary>
        /// 在 SpawnFinalize shadow 验证完成后直接提交 self initial effects，不经过 Boundary ingress。
        /// </summary>
        private bool TryApplyInitialEffects(
            Entity sourceAsc,
            ref GasDefinitionCatalogBlob catalog,
            in GasStageBSessionSnapshot snapshot,
            ulong readyTick)
        {
            if (!AscIdentities.HasComponent(sourceAsc) || !Memberships.HasComponent(sourceAsc) ||
                !Drains.HasComponent(sourceAsc) || !BoundaryFacts.HasBuffer(sourceAsc))
                return false;
            var owner = AscIdentities[sourceAsc].OwnerAsc;
            var battle = Memberships[sourceAsc].BattleInstance;
            var pending = PendingInitialEffects[sourceAsc];
            if (!CanReserveInitialFacts(
                    sourceAsc,
                    pending,
                    in snapshot.Profile,
                    ref catalog))
                return false;
            var mutationStart = 0;
            for (var index = 0; index < pending.Length; index++)
            {
                var effect = pending[index];
                if (!GasDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                        ref catalog, effect.DefinitionId, out var definitionIndex))
                    return false;
                var request = new GasGameplayEffectApplicationRequest
                {
                    SimulationEpoch = snapshot.Identity.SimulationEpoch,
                    SourceAsc = owner,
                    TargetAsc = owner,
                    DefinitionIndex = definitionIndex,
                    ApplicationId = BuildInitialApplicationId(
                        snapshot.Identity.SimulationEpoch,
                        snapshot.Manifest.SpawnBatchId,
                        owner,
                        effect.ConfigOrdinal,
                        effect.DefinitionId),
                    StartTick = readyTick,
                    CausalityId = effect.CausalityId,
                    TargetIsAlive = 1,
                    CaptureValueCount = 0,
                    ValueViewCount = 0,
                    TargetAvatarStableId = effect.Target.TargetAvatarStableId,
                    TargetAvatarBindingGeneration = effect.Target.TargetAvatarBindingGeneration,
                    SpatialSnapshot = GasBoundarySpatialSnapshot.None,
                };
                var heads = SlabHeads[sourceAsc];
                var applied = GasGameplayEffectTransaction.TryApply(
                    ref catalog,
                    in request,
                    ActiveEffects[sourceAsc],
                    AttributeValues[sourceAsc],
                    AttributeDirtyWords[sourceAsc],
                    TagCounts[sourceAsc],
                    TagPresenceWords[sourceAsc],
                    ref heads.ActiveEffect,
                    snapshot.Profile.MaxActiveEffectCount,
                    default,
                    default,
                    BootstrapEvaluatorStack,
                    BootstrapAttributeMutations,
                    mutationStart,
                    out var result);
                if (!applied || result.Outcome == GasGameplayEffectApplicationOutcome.None)
                    return false;
                if (!AppendInitialEffectFacts(
                        sourceAsc,
                        in battle,
                        in effect,
                        in request,
                        in result,
                        ref catalog,
                        readyTick,
                        ref mutationStart))
                    return false;
                SlabHeads[sourceAsc] = heads;
            }
            return true;
        }

        /// <summary>
        /// 预留初始 effect 的 ActiveEffect、mutation 与 ASC outbox 容量，禁止提交中途扩容。
        /// </summary>
        private bool CanReserveInitialFacts(
            Entity asc,
            DynamicBuffer<PendingInitialGameplayEffect> pending,
            in GasScaleProfile profile,
            ref GasDefinitionCatalogBlob catalog)
        {
            var demand = 0;
            var activeEffectDemand = 0;
            var mutationDemand = 0;
            var evaluatorDemand = 0;
            for (var index = 0; index < pending.Length; index++)
            {
                if (!GasDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                        ref catalog, pending[index].DefinitionId, out var definitionIndex))
                    return false;
                var definition = catalog.GameplayEffects[definitionIndex];
                if (definition.Lifetime == GasEffectLifetimePolicy.Duration ||
                    definition.Lifetime == GasEffectLifetimePolicy.Infinite)
                {
                    if (activeEffectDemand == int.MaxValue)
                        return false;
                    activeEffectDemand++;
                }
                if (definition.ModifierRange.Count < 0 ||
                    definition.ModifierRange.Count > int.MaxValue - 1)
                    return false;
                // Duration/Infinite 且关闭 ExecuteOnApplication 时只建立 ActiveEffect，
                // 预检不得为本次不会发生的 modifier mutation/fact 预留 outbox。
                var appliesModifiersOnApplication =
                    definition.Lifetime == GasEffectLifetimePolicy.Instant ||
                    definition.Lifetime == GasEffectLifetimePolicy.InstantExecution ||
                    definition.ExecuteOnApplication != 0;
                var modifierFactDemand = appliesModifiersOnApplication
                    ? definition.ModifierRange.Count
                    : 0;
                var effectDemand = 1 + modifierFactDemand;
                var cueDemand = CountInitialCueFacts(in definition, ref catalog);
                if (cueDemand < 0 || cueDemand > int.MaxValue - effectDemand)
                    return false;
                effectDemand += cueDemand;
                if (definition.Lifetime == GasEffectLifetimePolicy.InstantExecution)
                {
                    if (effectDemand == int.MaxValue)
                        return false;
                    effectDemand++;
                }
                if (effectDemand < 0 || demand > int.MaxValue - effectDemand)
                    return false;
                demand += effectDemand;
                if (mutationDemand > int.MaxValue - modifierFactDemand)
                    return false;
                mutationDemand += modifierFactDemand;
                for (var modifier = 0; modifier < definition.ModifierRange.Count; modifier++)
                {
                    var value = catalog.Modifiers[definition.ModifierRange.Start + modifier];
                    if (value.EvaluatorProgramRange.Count > evaluatorDemand)
                        evaluatorDemand = value.EvaluatorProgramRange.Count;
                }
            }

            var state = Drains[asc];
            var outbox = BoundaryFacts[asc];
            if (state.SimulationEpoch == 0 || state.OwnerKind != GasBoundaryOwnerKind.Asc ||
                state.OwnerStableId != AscIdentities[asc].OwnerAsc.AscStableId ||
                state.OwnerGeneration != AscIdentities[asc].OwnerAsc.AscGeneration ||
                state.Phase != GasBoundaryDrainPhase.Idle || state.NextOwnerSequence == 0 ||
                state.NextOwnerSequence == ulong.MaxValue || state.InFlightWatermark != 0 ||
                activeEffectDemand > profile.MaxActiveEffectCount ||
                ActiveEffects[asc].Length > profile.MaxActiveEffectCount - activeEffectDemand ||
                mutationDemand > 0 &&
                (!BootstrapAttributeMutations.IsCreated ||
                 mutationDemand > BootstrapAttributeMutations.Length) ||
                evaluatorDemand > 0 &&
                (!BootstrapEvaluatorStack.IsCreated || evaluatorDemand > BootstrapEvaluatorStack.Length))
                return false;
            if (demand < 0 || demand > profile.MaxAscBoundaryFactCount ||
                outbox.Length > profile.MaxAscBoundaryFactCount - demand ||
                demand > int.MaxValue - outbox.Length ||
                outbox.Capacity < outbox.Length + demand ||
                (ulong)demand > ulong.MaxValue - state.NextOwnerSequence)
                return false;
            return true;
        }

        /// <summary>
        /// 统计 AppendInitialDefinitionFacts 实际会写出的 Cue 数量，严格复用 phase filter 与 instant fallback。
        /// </summary>
        private static int CountInitialCueFacts(
            in GasGameplayEffectDefinitionBlob definition,
            ref GasDefinitionCatalogBlob catalog)
        {
            if (definition.CueRange.Count < 0 || definition.CueRange.Start < 0 ||
                definition.CueRange.Start > catalog.CueReferences.Length - definition.CueRange.Count)
                return -1;
            var phases = definition.Lifetime == GasEffectLifetimePolicy.InstantExecution
                ? GasCuePhaseFlags.OnActive | GasCuePhaseFlags.Executed
                : GasCuePhaseFlags.OnActive;
            var count = 0;
            for (var cue = 0; cue < definition.CueRange.Count; cue++)
            {
                var reference = catalog.CueReferences[definition.CueRange.Start + cue];
                if ((reference.Phases & phases) == GasCuePhaseFlags.None)
                    continue;
                if (count == int.MaxValue)
                    return -1;
                count++;
            }
            return definition.Lifetime == GasEffectLifetimePolicy.InstantExecution && count == 0
                ? 1
                : count;
        }

        /// <summary>
        /// 为 SpawnBatch 中一条初始 effect 生成不会与普通 application 混淆的稳定 application identity。
        /// </summary>
        private static ulong BuildInitialApplicationId(
            ulong simulationEpoch,
            ulong spawnBatchId,
            in OwnerAscHandle owner,
            int configOrdinal,
            int definitionId)
        {
            var hash = GasBoundaryFnv1A64.Create();
            hash.AddUInt64(simulationEpoch);
            hash.AddUInt64(spawnBatchId);
            hash.AddUInt64(owner.AscStableId);
            hash.AddUInt32(owner.AscGeneration);
            hash.AddInt32(configOrdinal);
            hash.AddInt32(definitionId);
            return hash.Value == 0 ? 1UL : hash.Value;
        }

        /// <summary>
        /// 按正式 TargetWave 的事实语义发布 initial effect lifecycle、mutation、execution 与 Cue facts。
        /// </summary>
        private bool AppendInitialEffectFacts(
            Entity asc,
            in BattleInstanceHandle battle,
            in PendingInitialGameplayEffect pending,
            in GasGameplayEffectApplicationRequest request,
            in GasGameplayEffectApplicationResult result,
            ref GasDefinitionCatalogBlob catalog,
            ulong readyTick,
            ref int mutationStart)
        {
            var definition = catalog.GameplayEffects[request.DefinitionIndex];
            var membership = Memberships[asc];
            var state = Drains[asc];
            var outbox = BoundaryFacts[asc];
            var operationOrdinal = pending.ConfigOrdinal;
            if (!AppendInitialFact(
                    ref state,
                    outbox,
                    CreateInitialEffectFact(
                        in membership,
                        in battle,
                        in request,
                        in result,
                        definition.DefinitionId,
                        readyTick,
                        operationOrdinal)))
                return false;

            for (var modifier = 0; modifier < result.MutationCount; modifier++)
            {
                if (!BootstrapAttributeMutations.IsCreated ||
                    mutationStart + modifier < 0 ||
                    mutationStart + modifier >= BootstrapAttributeMutations.Length)
                    return false;
                var mutation = BootstrapAttributeMutations[mutationStart + modifier];
                if (!AppendInitialFact(
                        ref state,
                        outbox,
                        CreateInitialMutationFact(
                            in membership,
                            in battle,
                            in request,
                            in mutation,
                            definition.DefinitionId,
                            readyTick,
                            operationOrdinal,
                            modifier,
                            ref catalog)))
                    return false;
            }
            mutationStart += result.MutationCount;
            if (!AppendInitialDefinitionFacts(
                    ref state,
                    outbox,
                    in membership,
                    in battle,
                    in request,
                    in definition,
                    ref catalog,
                    readyTick,
                    operationOrdinal))
                return false;
            Drains[asc] = state;
            return true;
        }

        /// <summary>
        /// 追加一条自包含 initial fact，并让 DrainProtocol 分配唯一 OwnerSequence。
        /// </summary>
        private static bool AppendInitialFact(
            ref BoundaryDrainState state,
            DynamicBuffer<BoundaryFactBuffer> outbox,
            in BoundaryFactBuffer fact)
        {
            return GasBoundaryDrainProtocol.TryAppendFactWithSequence(
                ref state,
                outbox,
                in fact,
                out _,
                out _);
        }

        /// <summary>
        /// 创建 initial effect lifecycle fact，冻结 definition、outcome、application 与 causality identity。
        /// </summary>
        private static BoundaryFactBuffer CreateInitialEffectFact(
            in AscBattleMembership membership,
            in BattleInstanceHandle battle,
            in GasGameplayEffectApplicationRequest request,
            in GasGameplayEffectApplicationResult result,
            int definitionId,
            ulong readyTick,
            int operationOrdinal)
        {
            return CreateInitialFactHeader(
                in membership,
                in battle,
                in request,
                readyTick,
                GasBoundaryFactKind.EffectLifecycle,
                2,
                operationOrdinal,
                int.MaxValue,
                new BoundaryFactPayload
                {
                    SchemaVersion = 1,
                    Kind = GasBoundaryPayloadKind.IntegerPair,
                    Integer0 = (long)result.Outcome,
                    Integer1 = ((long)(byte)result.Failure << 32) |
                               (uint)result.AppliedModifierCount,
                    Integer2 = definitionId,
                    StableId0 = request.ApplicationId,
                    StableId1 = 0,
                    StableId2 = result.ActiveEffect.IsValid
                        ? result.ActiveEffect.OwnerAsc.AscStableId
                        : request.TargetAsc.AscStableId,
                    Generation0 = result.ActiveEffect.IsValid
                        ? result.ActiveEffect.SlotGeneration
                        : 0,
                    Generation1 = request.TargetAsc.AscGeneration,
                    Generation2 = request.TargetAsc.AscGeneration,
                });
        }

        /// <summary>
        /// 创建 initial Attribute mutation fact，复用正式 mutation payload 字段布局。
        /// </summary>
        private static BoundaryFactBuffer CreateInitialMutationFact(
            in AscBattleMembership membership,
            in BattleInstanceHandle battle,
            in GasGameplayEffectApplicationRequest request,
            in GasAttributeMutationRecord mutation,
            int definitionId,
            ulong readyTick,
            int operationOrdinal,
            int modifierOrdinal,
            ref GasDefinitionCatalogBlob catalog)
        {
            var attributeId = mutation.AttributeLayoutIndex >= 0 &&
                              mutation.AttributeLayoutIndex < catalog.AttributeLayout.Entries.Length
                ? catalog.AttributeLayout.Entries[mutation.AttributeLayoutIndex].AttributeId
                : 0;
            return CreateInitialFactHeader(
                in membership,
                in battle,
                in request,
                readyTick,
                GasBoundaryFactKind.AttributeChanged,
                1,
                operationOrdinal,
                modifierOrdinal,
                new BoundaryFactPayload
                {
                    SchemaVersion = 1,
                    Kind = GasBoundaryPayloadKind.AttributeDelta,
                    Integer0 = attributeId,
                    Integer1 = ((long)(uint)mutation.AttributeLayoutIndex << 32) |
                               mutation.Revision,
                    Integer2 = definitionId,
                    Scalar0 = mutation.RequestedBaseDelta,
                    Scalar1 = mutation.RequestedCurrentDelta,
                    Scalar2 = mutation.PreviousBase,
                    Scalar3 = mutation.PreviousCurrent,
                    Scalar4 = mutation.UnclampedBase,
                    Scalar5 = mutation.UnclampedCurrent,
                    Scalar6 = mutation.AppliedBase,
                    Scalar7 = mutation.AppliedCurrent,
                    Scalar8 = mutation.AppliedBase - mutation.PreviousBase,
                    Scalar9 = mutation.AppliedCurrent - mutation.PreviousCurrent,
                    StableId0 = request.ApplicationId,
                    StableId1 = 0,
                    StableId2 = ComposeInitialContributorId(
                        request.ApplicationId,
                        modifierOrdinal),
                    Generation0 = 0,
                    Generation1 = mutation.PreviousRevision,
                });
        }

        /// <summary>
        /// 创建拥有稳定 phase/ordinal 的 initial execution 与 Cue facts。
        /// </summary>
        private static bool AppendInitialDefinitionFacts(
            ref BoundaryDrainState state,
            DynamicBuffer<BoundaryFactBuffer> outbox,
            in AscBattleMembership membership,
            in BattleInstanceHandle battle,
            in GasGameplayEffectApplicationRequest request,
            in GasGameplayEffectDefinitionBlob definition,
            ref GasDefinitionCatalogBlob catalog,
            ulong readyTick,
            int operationOrdinal)
        {
            if (definition.Lifetime == GasEffectLifetimePolicy.InstantExecution &&
                !AppendInitialFact(
                    ref state,
                    outbox,
                    CreateInitialFactHeader(
                        in membership,
                        in battle,
                        in request,
                        readyTick,
                        GasBoundaryFactKind.ExecutionCalculation,
                        4,
                        operationOrdinal,
                        0,
                        CreateDefinitionPayload(
                            in request,
                            definition.DefinitionId,
                            GasBoundaryFactKind.ExecutionCalculation))))
                return false;

            if (definition.Lifetime == GasEffectLifetimePolicy.InstantExecution &&
                definition.CueRange.Count == 0)
                return AppendInitialFact(
                    ref state,
                    outbox,
                    CreateInitialFactHeader(
                        in membership,
                        in battle,
                        in request,
                        readyTick,
                        GasBoundaryFactKind.Cue,
                        5,
                        operationOrdinal,
                        0,
                        CreateDefinitionPayload(
                            in request,
                            definition.DefinitionId,
                            GasBoundaryFactKind.Cue)));

            var emittedCue = false;
            for (var cue = 0; cue < definition.CueRange.Count; cue++)
            {
                var reference = catalog.CueReferences[definition.CueRange.Start + cue];
                var phases = definition.Lifetime == GasEffectLifetimePolicy.InstantExecution
                    ? GasCuePhaseFlags.OnActive | GasCuePhaseFlags.Executed
                    : GasCuePhaseFlags.OnActive;
                if ((reference.Phases & phases) == GasCuePhaseFlags.None)
                    continue;
                if (!AppendInitialFact(
                        ref state,
                        outbox,
                        CreateInitialFactHeader(
                            in membership,
                            in battle,
                            in request,
                            readyTick,
                            GasBoundaryFactKind.Cue,
                            definition.Lifetime == GasEffectLifetimePolicy.InstantExecution ? (ushort)5 : (ushort)4,
                            operationOrdinal,
                            reference.CueDefinitionOrdinal,
                            CreateDefinitionPayload(
                                in request,
                                definition.DefinitionId,
                                GasBoundaryFactKind.Cue))))
                    return false;
                emittedCue = true;
            }
            if (definition.Lifetime == GasEffectLifetimePolicy.InstantExecution && !emittedCue)
                return AppendInitialFact(
                    ref state,
                    outbox,
                    CreateInitialFactHeader(
                        in membership,
                        in battle,
                        in request,
                        readyTick,
                        GasBoundaryFactKind.Cue,
                        5,
                        operationOrdinal,
                        0,
                        CreateDefinitionPayload(
                            in request,
                            definition.DefinitionId,
                            GasBoundaryFactKind.Cue)));
            return true;
        }

        /// <summary>
        /// 创建 execution/Cue fact 的稳定 payload。
        /// </summary>
        private static BoundaryFactPayload CreateDefinitionPayload(
            in GasGameplayEffectApplicationRequest request,
            int definitionId,
            GasBoundaryFactKind kind)
        {
            return new BoundaryFactPayload
            {
                SchemaVersion = 1,
                Kind = GasBoundaryPayloadKind.IntegerPair,
                Integer0 = (long)kind,
                Integer1 = request.ApplicationId > long.MaxValue
                    ? long.MaxValue
                    : (long)request.ApplicationId,
                Integer2 = definitionId,
                StableId0 = request.ApplicationId,
                StableId1 = request.CausalityId,
            };
        }

        /// <summary>
        /// 为 bootstrap modifier 生成与普通 application 相同的稳定 contributor 身份。
        /// </summary>
        private static ulong ComposeInitialContributorId(
            ulong applicationId,
            int modifierOrdinal)
        {
            var value = applicationId ^
                        (0x9E3779B97F4A7C15UL * (ulong)(modifierOrdinal + 1));
            return value == 0 ? 1UL : value;
        }

        /// <summary>
        /// 创建 ASC-scoped fact 的稳定身份、phase、scope 与 payload 头部。
        /// </summary>
        private static BoundaryFactBuffer CreateInitialFactHeader(
            in AscBattleMembership membership,
            in BattleInstanceHandle battle,
            in GasGameplayEffectApplicationRequest request,
            ulong readyTick,
            GasBoundaryFactKind kind,
            ushort phase,
            int operationOrdinal,
            int factOrdinal,
            in BoundaryFactPayload payload)
        {
            return new BoundaryFactBuffer
            {
                Scope = GasBoundaryFactScope.Asc,
                Plane = GasBoundaryFactPlane.Gameplay,
                ScopeStableId = request.TargetAsc.AscStableId,
                ScopeGeneration = request.TargetAsc.AscGeneration,
                BattleInstanceId = battle.BattleStableId,
                BattleInstanceGeneration = battle.BattleGeneration,
                OwnerScenarioUnitId = membership.ScenarioUnitId,
                SourceAsc = request.SourceAsc,
                TargetAsc = request.TargetAsc,
                SimulationTick = readyTick,
                SemanticPhaseOrdinal = phase,
                WorkClassOrdinal = 1,
                ParentCausalityId = request.CausalityId,
                SemanticId = request.ApplicationId,
                Kind = kind,
                Payload = payload,
            };
        }

        /// <summary>
        /// 对所有 Catalog Attribute 无条件写 DefaultValue，未列 Pending 项也不得保留隐式零。
        /// </summary>
        private void ApplyAllAttributeDefaults(Entity asc, ref GasDefinitionCatalogBlob catalog)
        {
            var values = AttributeValues[asc];
            var dirty = AttributeDirtyWords[asc];
            for (var index = 0; index < catalog.AttributeLayout.Entries.Length; index++)
            {
                var defaultValue = catalog.AttributeLayout.Entries[index].DefaultValue;
                values[index] = new AttributeValueSlot
                {
                    Base = defaultValue,
                    Current = defaultValue,
                    Revision = InitialGeneration,
                };
                SetBit(dirty, index);
            }

            var pending = PendingAttributes[asc];
            for (var index = 0; index < pending.Length; index++)
            {
                var initialization = pending[index];
                if (initialization.HasExplicitValue == 0)
                    continue;

                var entry = catalog.AttributeLayout.Entries[initialization.LayoutIndex];
                var baseValue = ClampInitialValue(initialization.BaseValue, in entry);
                var currentValue = ClampInitialValue(initialization.CurrentValue, in entry);
                values[initialization.LayoutIndex] = new AttributeValueSlot
                {
                    Base = baseValue,
                    Current = currentValue,
                    Revision = InitialGeneration,
                };
                SetBit(dirty, initialization.LayoutIndex);
            }
        }

        /// <summary>
        /// 按 Catalog clamp 元数据规范化 Spawn 初始值，确保发布前后使用同一数值边界。
        /// </summary>
        private static float ClampInitialValue(
            float value,
            in GasAttributeLayoutEntryBlob entry)
        {
            if (entry.ClampMinimum != 0 && value < entry.MinimumValue)
                value = entry.MinimumValue;
            if (entry.ClampMaximum != 0 && value > entry.MaximumValue)
                value = entry.MaximumValue;
            return value;
        }

        /// <summary>
        /// 将每条 Pending Tag 解释为一次 exact grant，并传播自身和 ancestors 的 inclusive count。
        /// </summary>
        private void ApplyTags(Entity asc, ref GasDefinitionCatalogBlob catalog)
        {
            var pending = PendingTags[asc];
            var counts = TagCounts[asc];
            for (var index = 0; index < pending.Length; index++)
            {
                var tagIndex = pending[index].LayoutIndex;
                IncrementExact(counts, tagIndex);
                var range = catalog.TagCatalog.Entries[tagIndex].AncestorIndexRange;
                for (var offset = 0; offset < range.Count; offset++)
                {
                    var ancestor = catalog.TagCatalog.AncestorIndices[range.Start + offset];
                    if (ancestor != tagIndex)
                        IncrementInclusive(counts, ancestor);
                }
            }
            RebuildPresence(TagPresenceWords[asc], counts);
        }

        /// <summary>
        /// 依 Pending 规范顺序创建初始 Ability non-compacting live slots。
        /// </summary>
        private void ApplyAbilities(
            Entity asc,
            ref GasDefinitionCatalogBlob catalog,
            in GasStageBSessionSnapshot snapshot)
        {
            var pending = PendingAbilities[asc];
            var granted = GrantedAbilities[asc];
            var owner = AscIdentities[asc].OwnerAsc;
            for (var index = 0; index < pending.Length; index++)
            {
                var definitionIndex = pending[index].LayoutIndex;
                var slotIndex = granted.Length;
                granted.Add(new GrantedAbilitySlot
                {
                    Header = GasSlabSlotHeader.CreateLive(InitialGeneration),
                    Handle = new GrantedAbilityHandle(
                        snapshot.Identity.SimulationEpoch,
                        owner,
                        slotIndex,
                        InitialGeneration),
                    DefinitionId = catalog.Abilities[definitionIndex].DefinitionId,
                    DefinitionIndex = definitionIndex,
                    DefinitionVersion = snapshot.Definitions.SchemaVersion,
                    DefinitionHash = snapshot.Definitions.ContentHash,
                    Level = catalog.Abilities[definitionIndex].Level,
                    GrantOrdinal = pending[index].ConfigOrdinal,
                    GrantSourceKind = GasAbilityGrantSourceKind.Bootstrap,
                    RemovalPolicy = GasGrantedAbilityRemovalPolicy.LeaveGranted,
                });
            }
            var heads = SlabHeads[asc];
            heads.GrantedAbility.HighWater = granted.Length;
            SlabHeads[asc] = heads;
        }

        /// <summary>
        /// 锁存 Fault、清零 Ready 可见性并向标准 EndFixed 记录整批去重 teardown。
        /// </summary>
        private void FaultAndRecordTeardown(Entity session, GasStageBSpawnFaultReason reason)
        {
            MarkSessionFault(session, reason);
            var registry = default(DynamicBuffer<AscRegistrySlot>);
            if (Registries.HasBuffer(session))
            {
                registry = Registries[session];
                for (var index = 0; index < registry.Length; index++)
                {
                    var slot = registry[index];
                    var asc = slot.ResolveRuntimeEntity();
                    slot.ReadyTick = 0;
                    slot.State = GasAscRegistryState.Tombstone;
                    slot.Header.MarkTombstone();
                    registry[index] = slot;
                    DisableSpawnMarker(asc);
                    if (!TryRecordDestroy(asc))
                        continue;
                    if (AscLifecycles.HasComponent(asc))
                        MarkAscDestroyPending(asc);
                    EndFixed.DestroyEntity(asc);
                }
            }
            RecordMarkerFallbackTeardown(session, registry);
            InvalidateBattles(session);
            DestroyMalformedSessionWithoutLifecycle(session);
        }

        /// <summary>
        /// 对无法承载 Faulted 状态的畸形 authority 记录标准 EndFixed 销毁，避免永久污染基数。
        /// </summary>
        private void DestroyMalformedSessionWithoutLifecycle(Entity session)
        {
            if (SessionLifecycles.HasComponent(session) || !TryRecordDestroy(session))
                return;
            EndFixed.DestroyEntity(session);
        }

        /// <summary>
        /// 通过 ASC batch marker 清理被损坏 registry/manifest 遗漏的原始 batch 成员。
        /// </summary>
        private void RecordMarkerFallbackTeardown(
            Entity session,
            DynamicBuffer<AscRegistrySlot> registry)
        {
            var batchId = SessionLifecycles.HasComponent(session)
                ? SessionLifecycles[session].ActiveSpawnBatchId
                : 0;
            var epoch = SessionIdentities.HasComponent(session)
                ? SessionIdentities[session].SimulationEpoch
                : 0;
            for (var index = 0; index < BatchMarkedAscs.Length; index++)
            {
                var asc = BatchMarkedAscs[index];
                if (!SpawnMarkers.HasComponent(asc) || ContainsEntity(registry, asc))
                    continue;
                var marker = SpawnMarkers[asc];
                if (!SpawnMarkers.IsComponentEnabled(asc) || marker.SpawnBatchId != batchId ||
                    marker.SimulationEpoch != epoch || !TryRecordDestroy(asc))
                    continue;
                SpawnMarkers.SetComponentEnabled(asc, false);
                if (AscLifecycles.HasComponent(asc))
                    MarkAscDestroyPending(asc);
                EndFixed.DestroyEntity(asc);
            }
        }

        /// <summary>
        /// 禁用仍存在的 SpawnBatch 派生 work marker，同时保留其不可变诊断数据。
        /// </summary>
        private void DisableSpawnMarker(Entity asc)
        {
            if (SpawnMarkers.HasComponent(asc) && SpawnMarkers.IsComponentEnabled(asc))
                SpawnMarkers.SetComponentEnabled(asc, false);
        }

        /// <summary>
        /// 仅在 Entity 仍存在且本轮尚未记录 Destroy 时登记一次显式 teardown 证明。
        /// </summary>
        private bool TryRecordDestroy(Entity entity)
        {
            if (entity == Entity.Null || !EntityStorage.Exists(entity))
                return false;
            for (var index = 0; index < RecordedDestroyEntities.Length; index++)
                if (RecordedDestroyEntities[index] == entity)
                    return false;
            RecordedDestroyEntities.Add(entity);
            return true;
        }

        /// <summary>
        /// 判断当前可能为空的 registry 是否已记录指定 ASC Entity。
        /// </summary>
        private static bool ContainsEntity(DynamicBuffer<AscRegistrySlot> registry, Entity asc)
        {
            if (!registry.IsCreated)
                return false;
            for (var index = 0; index < registry.Length; index++)
                if (registry[index].ResolveRuntimeEntity() == asc)
                    return true;
            return false;
        }

        /// <summary>
        /// 将 Session 转 Faulted，并写 batch/原因、epoch、tick 到 FaultLatch。
        /// </summary>
        private void MarkSessionFault(Entity session, GasStageBSpawnFaultReason reason)
        {
            if (!SessionLifecycles.HasComponent(session))
                return;

            var lifecycle = SessionLifecycles[session];
            lifecycle.State = GasSessionLifecycleState.Faulted;
            SessionLifecycles[session] = lifecycle;
            if (!FaultLatches.HasComponent(session))
                return;

            var latch = FaultLatches[session];
            latch.FaultId = lifecycle.ActiveSpawnBatchId != 0
                ? lifecycle.ActiveSpawnBatchId
                : (ulong)reason;
            latch.FaultEpoch = SessionIdentities.HasComponent(session)
                ? SessionIdentities[session].SimulationEpoch
                : 0;
            latch.FaultTick = Ticks.HasComponent(session) ? Ticks[session].CurrentTick : 0;
            latch.ReasonCode = (int)reason;
            latch.Detected = 1;
            latch.IngressClosed = 0;
            FaultLatches[session] = latch;
        }

        /// <summary>
        /// 清零 Battle Ready 成员、关闭 ingress 并 tombstone。
        /// </summary>
        private void InvalidateBattles(Entity session)
        {
            if (!Battles.HasBuffer(session))
                return;
            var battles = Battles[session];
            for (var index = 0; index < battles.Length; index++)
            {
                var battle = battles[index];
                battle.ReadyMemberCount = 0;
                battle.IngressClosed = 1;
                battle.State = GasBattleInstanceState.Tombstone;
                battle.Header.MarkTombstone();
                battles[index] = battle;
            }
        }

        /// <summary>
        /// 在 EndFixed playback 前将 ASC 关闭 ingress 并转 DestroyPending。
        /// </summary>
        private void MarkAscDestroyPending(Entity asc)
        {
            var lifecycle = AscLifecycles[asc];
            lifecycle.State = GasAscLifecycleState.DestroyPending;
            lifecycle.ReadyTick = 0;
            lifecycle.IngressClosed = 1;
            AscLifecycles[asc] = lifecycle;
        }

        /// <summary>
        /// 将 dense 元素数转换为 64 位派生位图精确长度。
        /// </summary>
        private static int WordCount(int count)
        {
            return count <= 0 ? 0 : ((count - 1) / BitsPerWord) + 1;
        }

        /// <summary>
        /// 设置指定 dense Attribute 的 dirty bit。
        /// </summary>
        private static void SetBit(DynamicBuffer<AttributeDirtyWord> words, int index)
        {
            var wordIndex = index / BitsPerWord;
            var word = words[wordIndex];
            word.Value |= 1UL << (index % BitsPerWord);
            words[wordIndex] = word;
        }

        /// <summary>
        /// 递增 directly granted Tag 的 exact 与 inclusive count。
        /// </summary>
        private static void IncrementExact(DynamicBuffer<TagCountSlot> counts, int index)
        {
            var value = counts[index];
            value.ExactCount++;
            value.InclusiveCount++;
            counts[index] = value;
        }

        /// <summary>
        /// 仅递增 ancestor Tag 的 inclusive count。
        /// </summary>
        private static void IncrementInclusive(DynamicBuffer<TagCountSlot> counts, int index)
        {
            var value = counts[index];
            value.InclusiveCount++;
            counts[index] = value;
        }

        /// <summary>
        /// 从最终 inclusive count 重建固定长度 Tag presence 位图。
        /// </summary>
        private static void RebuildPresence(
            DynamicBuffer<TagPresenceWord> words,
            DynamicBuffer<TagCountSlot> counts)
        {
            for (var index = 0; index < words.Length; index++)
                words[index] = default;
            for (var index = 0; index < counts.Length; index++)
            {
                if (counts[index].InclusiveCount <= 0)
                    continue;
                var wordIndex = index / BitsPerWord;
                var word = words[wordIndex];
                word.Value |= 1UL << (index % BitsPerWord);
                words[wordIndex] = word;
            }
        }
    }
}
