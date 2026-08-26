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
            JobHandle dependency)
        {
            var recordedDestroys = new NativeList<Entity>(
                maximumDestroyCount < 0 ? 0 : maximumDestroyCount,
                Allocator.TempJob);
            var job = template;
            job.EndFixed = endFixed;
            job.Session = session;
            job.BatchMarkedAscs = batchMarkedAscs.AsDeferredJobArray();
            job.RecordedDestroyEntities = recordedDestroys;
            var combinedDependency = JobHandle.CombineDependencies(dependency, gatherDependency);
            var handle = job.Schedule(combinedDependency);
            handle = recordedDestroys.Dispose(handle);
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
                job.ForceCardinalityFault = 1;
                handle = job.Schedule(handle);
            }
            handle = sessions.Dispose(handle);
            handle = recordedDestroys.Dispose(handle);
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
        [ReadOnly] public ComponentLookup<BoundaryDrainState> Drains;
        public BufferLookup<BattleInstanceSlot> Battles;
        public BufferLookup<AscRegistrySlot> Registries;
        public BufferLookup<SpawnBatchMemberManifestSlot> SpawnManifestMembers;
        [ReadOnly] public BufferLookup<BoundaryCommandInbox> Inboxes;
        [ReadOnly] public BufferLookup<BoundaryCommandFrozenPayload> FrozenPayloads;
        [ReadOnly] public BufferLookup<BoundaryFactBuffer> BoundaryFacts;

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
        public BufferLookup<GrantedAbilitySlot> GrantedAbilities;
        [ReadOnly] public BufferLookup<AbilityActivationSlot> Activations;
        [ReadOnly] public BufferLookup<AbilityContinuationSlot> Continuations;
        [ReadOnly] public BufferLookup<AbilitySubscriptionSlot> Subscriptions;
        [ReadOnly] public BufferLookup<CooldownGateSlot> Cooldowns;
        [ReadOnly] public BufferLookup<ActivationOwnedContributionSlot> OwnedContributions;
        [ReadOnly] public BufferLookup<EmittedApplicationRefSlot> EmittedApplications;
        [ReadOnly] public BufferLookup<ActiveEffectSlot> ActiveEffects;
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
            Drains = state.GetComponentLookup<BoundaryDrainState>(true);
            Battles = state.GetBufferLookup<BattleInstanceSlot>();
            Registries = state.GetBufferLookup<AscRegistrySlot>();
            SpawnManifestMembers = state.GetBufferLookup<SpawnBatchMemberManifestSlot>();
            Inboxes = state.GetBufferLookup<BoundaryCommandInbox>(true);
            FrozenPayloads = state.GetBufferLookup<BoundaryCommandFrozenPayload>(true);
            BoundaryFacts = state.GetBufferLookup<BoundaryFactBuffer>(true);
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
            GrantedAbilities = state.GetBufferLookup<GrantedAbilitySlot>();
            Activations = state.GetBufferLookup<AbilityActivationSlot>(true);
            Continuations = state.GetBufferLookup<AbilityContinuationSlot>(true);
            Subscriptions = state.GetBufferLookup<AbilitySubscriptionSlot>(true);
            Cooldowns = state.GetBufferLookup<CooldownGateSlot>(true);
            OwnedContributions = state.GetBufferLookup<ActivationOwnedContributionSlot>(true);
            EmittedApplications = state.GetBufferLookup<EmittedApplicationRefSlot>(true);
            ActiveEffects = state.GetBufferLookup<ActiveEffectSlot>(true);
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
                   manifest.ExpectedGrantedAbilityInitializationCount >= 0;
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
            if (battles.Length > snapshot.Profile.MaxBattleInstanceCount ||
                registry.Length > snapshot.Profile.MaxAscRegistryCount ||
                registry.Length > snapshot.Profile.MaxSpawnBatchSize)
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
                    out var abilityCount))
                return GasStageBSpawnFaultReason.SpawnBatchMismatch;
            AddPendingValuesToManifestHash(registry, ref hasher);
            return attributeCount == manifest.ExpectedAttributeInitializationCount &&
                   tagCount == manifest.ExpectedTagInitializationCount &&
                   abilityCount == manifest.ExpectedGrantedAbilityInitializationCount &&
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
            out int abilityCount)
        {
            attributeCount = 0;
            tagCount = 0;
            abilityCount = 0;
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
                        in manifest))
                    return false;
                hasher.AddMember(in member);
                attributeCount += member.AttributeInitializationCount;
                tagCount += member.TagInitializationCount;
                abilityCount += member.GrantedAbilityInitializationCount;
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
                       expectedAbilityStart);
        }

        /// <summary>
        /// 判断 manifest 校验所需的 ASC components 与 Pending buffers 是否仍完整存在。
        /// </summary>
        private bool HasManifestMemberLayout(Entity asc)
        {
            return AscIdentities.HasComponent(asc) && Memberships.HasComponent(asc) &&
                   ActorBindings.HasComponent(asc) && RandomStates.HasComponent(asc) &&
                   SpawnMarkers.HasComponent(asc) && PendingAttributes.HasBuffer(asc) &&
                   PendingTags.HasBuffer(asc) && PendingAbilities.HasBuffer(asc);
        }

        /// <summary>
        /// 验证 manifest ranges 连续覆盖当前每个 ASC 的 Pending buffers。
        /// </summary>
        private bool HasExpectedManifestRanges(
            Entity asc,
            in SpawnBatchMemberManifestSlot member,
            int expectedAttributeStart,
            int expectedTagStart,
            int expectedAbilityStart)
        {
            return member.AttributeInitializationStart == expectedAttributeStart &&
                   member.AttributeInitializationCount == PendingAttributes[asc].Length &&
                   member.TagInitializationStart == expectedTagStart &&
                   member.TagInitializationCount == PendingTags[asc].Length &&
                   member.GrantedAbilityInitializationStart == expectedAbilityStart &&
                   member.GrantedAbilityInitializationCount == PendingAbilities[asc].Length;
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
            var lifecycle = AscLifecycles[asc];
            var drain = Drains[asc];
            var payloadRanges = PayloadRangeStates[asc];
            if (identity.SimulationEpoch != snapshot.Identity.SimulationEpoch || identity.OwnerAsc != slot.OwnerAsc ||
                membership.BattleInstance != slot.BattleInstance || lifecycle.State != GasAscLifecycleState.Pending ||
                lifecycle.ReadyTick != 0 || !HasExpectedMembership(battles, ordinal, membership.MembershipOrdinal) ||
                ActorBindings[asc].BindingGeneration == 0 ||
                drain.SimulationEpoch != snapshot.Identity.SimulationEpoch ||
                drain.OwnerKind != GasBoundaryOwnerKind.Asc ||
                drain.OwnerStableId != slot.OwnerAsc.AscStableId ||
                drain.OwnerGeneration != slot.OwnerAsc.AscGeneration ||
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
            return ValidatePending(asc, ref catalog);
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
                   PendingAbilities.HasBuffer(asc) && GrantedAbilities.HasBuffer(asc) &&
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
        private GasStageBSpawnFaultReason ValidatePending(Entity asc, ref GasDefinitionCatalogBlob catalog)
        {
            if (!ValidAttributes(PendingAttributes[asc], ref catalog) ||
                !ValidTags(PendingTags[asc], ref catalog))
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
            }
            return true;
        }

        /// <summary>
        /// 统计一个 ConfigOrdinal 在 batch 三类 Pending buffers 的总次数。
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
                ApplyAsc(slot.ResolveRuntimeEntity(), ref catalog, in snapshot, readyTick);
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
        /// 应用 Stage-B 无 initial-effect 最小契约并发布一个 ASC Ready。
        /// </summary>
        private void ApplyAsc(
            Entity asc,
            ref GasDefinitionCatalogBlob catalog,
            in GasStageBSessionSnapshot snapshot,
            ulong readyTick)
        {
            ApplyAllAttributeDefaults(asc, ref catalog);
            ApplyTags(asc, ref catalog);
            ApplyAbilities(asc, ref catalog, in snapshot);
            PendingAttributes[asc].Clear();
            PendingTags[asc].Clear();
            PendingAbilities[asc].Clear();
            SpawnMarkers.SetComponentEnabled(asc, false);
            AscLifecycles[asc] = new AscLifecycle
            {
                State = GasAscLifecycleState.Ready,
                ReadyTick = readyTick,
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
