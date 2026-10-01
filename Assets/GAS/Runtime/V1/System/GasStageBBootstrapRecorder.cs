using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 保存 Stage-B Session 安装所需的 Catalog、规则、容量与 SpawnBatch。
    /// </summary>
    public struct GasStageBSessionBootstrapRequest
    {
        public ulong SimulationEpoch;
        public ulong SpawnBatchId;
        public GasSessionConfig Config;
        public GasScaleProfile ScaleProfile;
        public BlobAssetReference<GasDefinitionCatalogBlob> Catalog;
        public GasCatalogValidationExpectation CatalogExpectation;
    }

    /// <summary>
    /// 由 World owner 持有的一次性录入门，阻止同一 EndFixed playback 前重复记录 deferred Session。
    /// </summary>
    public struct GasStageBBootstrapRecordGate
    {
        private byte _closed;

        /// <summary>
        /// 判断 World owner 是否已在当前一次性安装流程记录过 Session。
        /// </summary>
        public readonly bool IsClosed()
        {
            return _closed != 0;
        }

        /// <summary>
        /// 在成功记录完整 batch 后永久关闭本次 Stage-B 安装门。
        /// </summary>
        public void Close()
        {
            _closed = 1;
        }
    }

    /// <summary>
    /// 保存一个初始 BattleInstance 及其在规范 ASC registry 中的连续成员 range。
    /// </summary>
    public struct GasStageBBattleBootstrapRequest
    {
        public BattleInstanceHandle BattleInstance;
        public int MemberStart;
        public int MemberCount;
        public int MembershipOrdinalRoot;
    }

    /// <summary>
    /// 保存一个 Pending ASC 的稳定身份、战局成员关系及三类初始化 range；raw Entity 仅限此内部协议。
    /// </summary>
    public struct GasStageBAscBootstrapRequest
    {
        public OwnerAscHandle OwnerAsc;
        public BattleInstanceHandle BattleInstance;
        public int RegistryOrdinal;
        public ulong ScenarioUnitId;
        public int SideId;
        public int TeamId;
        public int MembershipOrdinal;
        public ulong OwnerActorStableId;
        public ulong AvatarActorStableId;
        public uint ActorBindingGeneration;
        public Entity AvatarEntity;
        public ulong RandomState0;
        public ulong RandomState1;
        public int AttributeInitializationStart;
        public int AttributeInitializationCount;
        public int TagInitializationStart;
        public int TagInitializationCount;
        public int GrantedAbilityInitializationStart;
        public int GrantedAbilityInitializationCount;
        public int InitialGameplayEffectStart;
        public int InitialGameplayEffectCount;
    }

    /// <summary>
    /// 向标准 EndFixed ECB 一次记录唯一 Pending Session、多个 BattleInstance 与完整 ASC batch，绝不直接 playback。
    /// </summary>
    public static class GasStageBBootstrapRecorder
    {
        private const ulong FirstStableSequence = 1;
        private const uint SessionOwnerGeneration = 1;

        /// <summary>
        /// 全批 preflight 成功后记录 Pending 拓扑与 typed initial-effect，首个 gameplay Tick 才注入效果命令。
        /// </summary>
        public static GasStageBSpawnFaultReason Record(
            EntityManager entityManager,
            EndFixedStepSimulationEntityCommandBufferSystem.Singleton endFixed,
            WorldUnmanaged world,
            ref GasStageBBootstrapRecordGate recordGate,
            in GasStageBSessionBootstrapRequest sessionRequest,
            NativeArray<GasStageBBattleBootstrapRequest> battleRequests,
            NativeArray<GasStageBAscBootstrapRequest> ascRequests,
            NativeArray<PendingAttributeInitialization> attributeInitializations,
            NativeArray<PendingTagInitialization> tagInitializations,
            NativeArray<PendingGrantedAbilityInitialization> grantedAbilityInitializations,
            NativeArray<PendingInitialGameplayEffect> initialGameplayEffects,
            out Entity deferredSession)
        {
            deferredSession = Entity.Null;
            if (recordGate.IsClosed() || HasActiveSession(entityManager))
                return GasStageBSpawnFaultReason.SessionCardinality;

            var failure = GasStageBSpawnContract.ValidateBootstrapRequest(
                in sessionRequest,
                battleRequests,
                ascRequests,
                attributeInitializations,
                tagInitializations,
                grantedAbilityInitializations,
                initialGameplayEffects);
            if (failure != GasStageBSpawnFaultReason.None)
                return failure;

            ref var profileCatalog = ref sessionRequest.Catalog.Value;
            if (!GasRuntimeV1SupportProfile.ValidateBlob(ref profileCatalog).Succeeded)
                return GasStageBSpawnFaultReason.ProfileInvalid;

            var commandBuffer = endFixed.CreateCommandBuffer(world);
            deferredSession = RecordBatch(
                entityManager,
                commandBuffer,
                in sessionRequest,
                battleRequests,
                ascRequests,
                attributeInitializations,
                tagInitializations,
                grantedAbilityInitializations,
                initialGameplayEffects);
            recordGate.Close();
            return GasStageBSpawnFaultReason.None;
        }

        /// <summary>
        /// 判断当前 World 是否仍有非 Disposed Session authority，避免同一 World 录入第二个 active Session。
        /// </summary>
        private static bool HasActiveSession(EntityManager entityManager)
        {
            var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasActiveSessionAuthority>());
            var hasActive = !query.IsEmptyIgnoreFilter;
            query.Dispose();
            return hasActive;
        }

        /// <summary>
        /// 创建 deferred Entities 并把完整 Pending batch 记录到同一个标准 EndFixed ECB。
        /// </summary>
        private static Entity RecordBatch(
            EntityManager entityManager,
            EntityCommandBuffer commandBuffer,
            in GasStageBSessionBootstrapRequest sessionRequest,
            NativeArray<GasStageBBattleBootstrapRequest> battleRequests,
            NativeArray<GasStageBAscBootstrapRequest> ascRequests,
            NativeArray<PendingAttributeInitialization> attributeInitializations,
            NativeArray<PendingTagInitialization> tagInitializations,
            NativeArray<PendingGrantedAbilityInitialization> grantedAbilityInitializations,
            NativeArray<PendingInitialGameplayEffect> initialGameplayEffects)
        {
            var manifest = CreateManifest(
                in sessionRequest,
                battleRequests,
                ascRequests,
                attributeInitializations,
                tagInitializations,
                grantedAbilityInitializations,
                initialGameplayEffects);
            var ascArchetype = GasRuntimeV1Archetypes.CreateAsc(entityManager);
            var sessionArchetype = GasRuntimeV1Archetypes.CreateSession(entityManager);
            var deferredAscs = new NativeArray<Entity>(ascRequests.Length, Allocator.Temp);
            for (var index = 0; index < deferredAscs.Length; index++)
                deferredAscs[index] = commandBuffer.CreateEntity(ascArchetype);

            var deferredSession = commandBuffer.CreateEntity(sessionArchetype);
            RecordSession(
                commandBuffer,
                deferredSession,
                in sessionRequest,
                in manifest,
                battleRequests,
                ascRequests,
                deferredAscs);
            for (var index = 0; index < deferredAscs.Length; index++)
            {
                var ascRequest = ascRequests[index];
                RecordAsc(
                    commandBuffer,
                    deferredAscs[index],
                    in sessionRequest,
                    in manifest,
                    in ascRequest,
                    attributeInitializations,
                    tagInitializations,
                    grantedAbilityInitializations,
                    initialGameplayEffects);
            }

            deferredAscs.Dispose();
            return deferredSession;
        }

        /// <summary>
        /// 记录 Session components、Pending registries、空 inbox 与 cleanup outbox。
        /// </summary>
        private static void RecordSession(
            EntityCommandBuffer commandBuffer,
            Entity session,
            in GasStageBSessionBootstrapRequest request,
            in GasSpawnBatchManifest manifest,
            NativeArray<GasStageBBattleBootstrapRequest> battles,
            NativeArray<GasStageBAscBootstrapRequest> ascs,
            NativeArray<Entity> deferredAscs)
        {
            ref var catalog = ref request.Catalog.Value;
            commandBuffer.SetComponent(session, new GasSessionIdentity { SimulationEpoch = request.SimulationEpoch });
            commandBuffer.SetComponent(session, request.Config);
            commandBuffer.SetComponent(session, GasDefinitionRegistry.Create(
                request.Catalog,
                in request.CatalogExpectation));
            commandBuffer.SetComponent(session, GasCatalogRegistry.Create(
                ref catalog,
                in request.CatalogExpectation));
            commandBuffer.SetComponent(session, request.ScaleProfile);
            commandBuffer.SetComponent(session, new SimulationTickState { NextStableSequence = FirstStableSequence });
            commandBuffer.SetComponent(session, new GasSessionLifecycle
            {
                State = GasSessionLifecycleState.SpawnPending,
                ActiveSpawnBatchId = request.SpawnBatchId,
            });
            commandBuffer.SetComponent(session, manifest);
            commandBuffer.SetComponent(session, default(SessionFaultLatch));
            commandBuffer.SetComponent(session, BoundaryDrainState.Create(
                request.SimulationEpoch,
                GasBoundaryOwnerKind.Session,
                request.SimulationEpoch,
                SessionOwnerGeneration,
                FirstStableSequence));
            RecordSessionBuffers(commandBuffer, session, in request, battles, ascs, deferredAscs);
        }

        /// <summary>
        /// 记录 Session 持久 buffers，并让 Battle/Registry 在首轮 playback 后仍全部处于 Pending。
        /// </summary>
        private static void RecordSessionBuffers(
            EntityCommandBuffer commandBuffer,
            Entity session,
            in GasStageBSessionBootstrapRequest request,
            NativeArray<GasStageBBattleBootstrapRequest> battles,
            NativeArray<GasStageBAscBootstrapRequest> ascs,
            NativeArray<Entity> deferredAscs)
        {
            var battleBuffer = PrepareBuffer<BattleInstanceSlot>(
                commandBuffer, session, request.ScaleProfile.MaxBattleInstanceCount);
            for (var index = 0; index < battles.Length; index++)
            {
                var battleRequest = battles[index];
                battleBuffer.Add(CreatePendingBattle(in battleRequest));
            }

            var registry = PrepareBuffer<AscRegistrySlot>(
                commandBuffer, session, request.ScaleProfile.MaxAscRegistryCount);
            for (var index = 0; index < ascs.Length; index++)
            {
                var ascRequest = ascs[index];
                registry.Add(CreatePendingRegistry(in ascRequest, deferredAscs[index], request.SpawnBatchId));
            }

            var members = PrepareBuffer<SpawnBatchMemberManifestSlot>(
                commandBuffer, session, request.ScaleProfile.MaxAscRegistryCount);
            for (var index = 0; index < ascs.Length; index++)
            {
                var ascRequest = ascs[index];
                members.Add(CreateManifestMember(in ascRequest, deferredAscs[index]));
            }

            PrepareBuffer<BoundaryCommandInbox>(
                commandBuffer, session, request.ScaleProfile.MaxBoundaryCommandCount);
            PrepareBuffer<BoundaryCommandFrozenPayload>(
                commandBuffer, session, request.ScaleProfile.MaxBoundaryCommandPayloadCount);
            PrepareBuffer<GasRequestTerminalIntent>(
                commandBuffer, session, request.ScaleProfile.MaxBoundaryCommandCount);
            PrepareBuffer<BoundaryFactBuffer>(
                commandBuffer, session, request.ScaleProfile.MaxSessionBoundaryFactCount);
        }

        /// <summary>
        /// 创建一个物理 live 但业务 SpawnPending 的 BattleInstance slot。
        /// </summary>
        private static BattleInstanceSlot CreatePendingBattle(in GasStageBBattleBootstrapRequest request)
        {
            return new BattleInstanceSlot
            {
                Header = GasSlabSlotHeader.CreateLive(request.BattleInstance.BattleGeneration),
                Handle = request.BattleInstance,
                BattleInstanceId = request.BattleInstance.BattleStableId,
                MemberStart = request.MemberStart,
                MemberCount = request.MemberCount,
                MembershipOrdinalRoot = request.MembershipOrdinalRoot,
                State = GasBattleInstanceState.SpawnPending,
            };
        }

        /// <summary>
        /// 创建一个只供 SpawnFinalize 解析、尚不可 gameplay lookup 的 Pending registry slot。
        /// </summary>
        private static AscRegistrySlot CreatePendingRegistry(
            in GasStageBAscBootstrapRequest request,
            Entity deferredAsc,
            ulong spawnBatchId)
        {
            var slot = new AscRegistrySlot
            {
                Header = GasSlabSlotHeader.CreateLive(request.OwnerAsc.AscGeneration),
                OwnerAsc = request.OwnerAsc,
                BattleInstance = request.BattleInstance,
                RegistryOrdinal = request.RegistryOrdinal,
                SpawnBatchId = spawnBatchId,
                State = GasAscRegistryState.Pending,
            };
            slot.SetRuntimeEntity(deferredAsc);
            return slot;
        }

        /// <summary>
        /// 从已通过 preflight 的原始 arrays 构造不可变 SpawnBatch 规模与内容哈希。
        /// </summary>
        private static GasSpawnBatchManifest CreateManifest(
            in GasStageBSessionBootstrapRequest request,
            NativeArray<GasStageBBattleBootstrapRequest> battles,
            NativeArray<GasStageBAscBootstrapRequest> ascs,
            NativeArray<PendingAttributeInitialization> attributes,
            NativeArray<PendingTagInitialization> tags,
            NativeArray<PendingGrantedAbilityInitialization> abilities,
            NativeArray<PendingInitialGameplayEffect> initialEffects)
        {
            var manifest = new GasSpawnBatchManifest
            {
                SimulationEpoch = request.SimulationEpoch,
                SpawnBatchId = request.SpawnBatchId,
                ExpectedBattleCount = battles.Length,
                ExpectedAscCount = ascs.Length,
                ExpectedAttributeInitializationCount = attributes.Length,
                ExpectedTagInitializationCount = tags.Length,
                ExpectedGrantedAbilityInitializationCount = abilities.Length,
                ExpectedInitialGameplayEffectCount = initialEffects.Length,
                Pending = 1,
            };
            var hasher = GasSpawnBatchManifestHasher.Create(in manifest);
            var definitions = GasDefinitionRegistry.Create(
                request.Catalog,
                in request.CatalogExpectation);
            ref var catalog = ref request.Catalog.Value;
            var catalogRegistry = GasCatalogRegistry.Create(
                ref catalog,
                in request.CatalogExpectation);
            hasher.AddSessionAuthority(
                in request.Config,
                in request.ScaleProfile,
                in definitions,
                in catalogRegistry);
            AddBattlesToHash(ref hasher, battles);
            AddMembersToHash(ref hasher, ascs);
            for (var index = 0; index < attributes.Length; index++)
                hasher.Add(attributes[index]);
            for (var index = 0; index < tags.Length; index++)
                hasher.Add(tags[index]);
            for (var index = 0; index < abilities.Length; index++)
                hasher.Add(abilities[index]);
            for (var index = 0; index < initialEffects.Length; index++)
                hasher.Add(initialEffects[index]);
            manifest.ContentHash = hasher.Finish();
            return manifest;
        }

        /// <summary>
        /// 将规范 Battle 请求序列加入 manifest hash。
        /// </summary>
        private static void AddBattlesToHash(
            ref GasSpawnBatchManifestHasher hasher,
            NativeArray<GasStageBBattleBootstrapRequest> battles)
        {
            for (var index = 0; index < battles.Length; index++)
            {
                var battle = battles[index];
                hasher.AddBattle(
                    battle.BattleInstance,
                    battle.MemberStart,
                    battle.MemberCount,
                    battle.MembershipOrdinalRoot);
            }
        }

        /// <summary>
        /// 将规范 ASC member 请求序列加入 manifest hash。
        /// </summary>
        private static void AddMembersToHash(
            ref GasSpawnBatchManifestHasher hasher,
            NativeArray<GasStageBAscBootstrapRequest> ascs)
        {
            for (var index = 0; index < ascs.Length; index++)
            {
                var asc = ascs[index];
                var member = CreateManifestMember(in asc, Entity.Null);
                hasher.AddMember(in member);
            }
        }

        /// <summary>
        /// 将一个 ASC bootstrap 请求冻结为 Pending batch member manifest。
        /// </summary>
        private static SpawnBatchMemberManifestSlot CreateManifestMember(
            in GasStageBAscBootstrapRequest request,
            Entity deferredAsc)
        {
            var member = new SpawnBatchMemberManifestSlot
            {
                OwnerAsc = request.OwnerAsc,
                BattleInstance = request.BattleInstance,
                RegistryOrdinal = request.RegistryOrdinal,
                ScenarioUnitId = request.ScenarioUnitId,
                SideId = request.SideId,
                TeamId = request.TeamId,
                MembershipOrdinal = request.MembershipOrdinal,
                OwnerActorStableId = request.OwnerActorStableId,
                AvatarActorStableId = request.AvatarActorStableId,
                ActorBindingGeneration = request.ActorBindingGeneration,
                RandomState0 = request.RandomState0,
                RandomState1 = request.RandomState1,
                AttributeInitializationStart = request.AttributeInitializationStart,
                AttributeInitializationCount = request.AttributeInitializationCount,
                TagInitializationStart = request.TagInitializationStart,
                TagInitializationCount = request.TagInitializationCount,
                GrantedAbilityInitializationStart = request.GrantedAbilityInitializationStart,
                GrantedAbilityInitializationCount = request.GrantedAbilityInitializationCount,
                InitialGameplayEffectStart = request.InitialGameplayEffectStart,
                InitialGameplayEffectCount = request.InitialGameplayEffectCount,
            };
            member.SetRuntimeEntity(deferredAsc);
            return member;
        }

        /// <summary>
        /// 记录一个完整 ASC archetype 的 Pending components、固定 buffers、初始化引用与容量预热。
        /// </summary>
        private static void RecordAsc(
            EntityCommandBuffer commandBuffer,
            Entity asc,
            in GasStageBSessionBootstrapRequest sessionRequest,
            in GasSpawnBatchManifest manifest,
            in GasStageBAscBootstrapRequest request,
            NativeArray<PendingAttributeInitialization> attributeInitializations,
            NativeArray<PendingTagInitialization> tagInitializations,
            NativeArray<PendingGrantedAbilityInitialization> grantedAbilityInitializations,
            NativeArray<PendingInitialGameplayEffect> initialGameplayEffects)
        {
            RecordAscComponents(commandBuffer, asc, in sessionRequest, in manifest, in request);
            ref var catalog = ref sessionRequest.Catalog.Value;
            var catalogRegistry = GasCatalogRegistry.Create(
                ref catalog,
                in sessionRequest.CatalogExpectation);
            GasRuntimeV1Archetypes.TryInitializeFixedBuffers(commandBuffer, asc, in catalogRegistry);
            RecordInitializationBuffers(
                commandBuffer,
                asc,
                in sessionRequest.ScaleProfile,
                in request,
                attributeInitializations,
                tagInitializations,
                grantedAbilityInitializations,
                initialGameplayEffects);
            RecordAbilityBuffers(commandBuffer, asc, in sessionRequest.ScaleProfile);
            RecordEffectBuffers(commandBuffer, asc, in sessionRequest.ScaleProfile);
        }

        /// <summary>
        /// 记录 ASC 的稳定身份、Pending lifecycle、Actor binding、RNG、slab heads 与 cleanup receipt。
        /// </summary>
        private static void RecordAscComponents(
            EntityCommandBuffer commandBuffer,
            Entity asc,
            in GasStageBSessionBootstrapRequest sessionRequest,
            in GasSpawnBatchManifest manifest,
            in GasStageBAscBootstrapRequest request)
        {
            commandBuffer.SetComponent(asc, new GasAscIdentity
            {
                SimulationEpoch = sessionRequest.SimulationEpoch,
                OwnerAsc = request.OwnerAsc,
            });
            commandBuffer.SetComponent(asc, new GasSpawnBatchMarker
            {
                SimulationEpoch = manifest.SimulationEpoch,
                SpawnBatchId = manifest.SpawnBatchId,
                ManifestHash = manifest.ContentHash,
                RegistryOrdinal = request.RegistryOrdinal,
            });
            commandBuffer.SetComponent(asc, new AscBattleMembership
            {
                BattleInstance = request.BattleInstance,
                ScenarioUnitId = request.ScenarioUnitId,
                SideId = request.SideId,
                TeamId = request.TeamId,
                MembershipOrdinal = request.MembershipOrdinal,
            });
            commandBuffer.SetComponent(asc, new AscLifecycle { State = GasAscLifecycleState.Pending });
            var binding = new GasActorBinding
            {
                OwnerActorStableId = request.OwnerActorStableId,
                AvatarActorStableId = request.AvatarActorStableId,
                BindingGeneration = request.ActorBindingGeneration,
            };
            binding.SetAvatarEntity(request.AvatarEntity);
            commandBuffer.SetComponent(asc, binding);
            commandBuffer.SetComponent(asc, new AscRandomState
            {
                State0 = request.RandomState0,
                State1 = request.RandomState1,
            });
            GasRuntimeV1Archetypes.InitializeAscMetadata(commandBuffer, asc);
            commandBuffer.SetComponent(asc, GasPayloadRangeAllocatorState.Create(
                sessionRequest.SimulationEpoch,
                in request.OwnerAsc));
            commandBuffer.SetComponent(asc, BoundaryDrainState.Create(
                sessionRequest.SimulationEpoch,
                GasBoundaryOwnerKind.Asc,
                request.OwnerAsc.AscStableId,
                request.OwnerAsc.AscGeneration,
                FirstStableSequence));
        }

        /// <summary>
        /// 记录三类只在 Pending 生命周期存在数据的初始化 buffers，保持 ConfigOrdinal 原规范顺序。
        /// </summary>
        private static void RecordInitializationBuffers(
            EntityCommandBuffer commandBuffer,
            Entity asc,
            in GasScaleProfile profile,
            in GasStageBAscBootstrapRequest request,
            NativeArray<PendingAttributeInitialization> attributes,
            NativeArray<PendingTagInitialization> tags,
            NativeArray<PendingGrantedAbilityInitialization> abilities,
            NativeArray<PendingInitialGameplayEffect> initialEffects)
        {
            var attributeBuffer = PrepareBuffer<PendingAttributeInitialization>(
                commandBuffer, asc, profile.MaxPendingAttributeInitializationCount);
            CopyRange(attributes, request.AttributeInitializationStart,
                request.AttributeInitializationCount, attributeBuffer);
            var tagBuffer = PrepareBuffer<PendingTagInitialization>(
                commandBuffer, asc, profile.MaxPendingTagInitializationCount);
            CopyRange(tags, request.TagInitializationStart,
                request.TagInitializationCount, tagBuffer);
            var abilityBuffer = PrepareBuffer<PendingGrantedAbilityInitialization>(
                commandBuffer, asc, profile.MaxPendingGrantedAbilityInitializationCount);
            CopyRange(abilities, request.GrantedAbilityInitializationStart,
                request.GrantedAbilityInitializationCount, abilityBuffer);
            var initialEffectBuffer = PrepareBuffer<PendingInitialGameplayEffect>(
                commandBuffer, asc, profile.MaxEffectOperationCount);
            CopyRange(initialEffects, request.InitialGameplayEffectStart,
                request.InitialGameplayEffectCount, initialEffectBuffer);
        }

        /// <summary>
        /// 预挂并按 ScaleProfile 预热 Ability、ledger、PendingCommand 与 ASC cleanup outbox buffers。
        /// </summary>
        private static void RecordAbilityBuffers(
            EntityCommandBuffer commandBuffer,
            Entity asc,
            in GasScaleProfile profile)
        {
            PrepareBuffer<GrantedAbilitySlot>(commandBuffer, asc, profile.MaxGrantedAbilityCount);
            PrepareBuffer<AbilityActivationSlot>(commandBuffer, asc, profile.MaxAbilityActivationCount);
            PrepareBuffer<AbilityContinuationSlot>(commandBuffer, asc, profile.MaxAbilityContinuationCount);
            PrepareBuffer<AbilitySubscriptionSlot>(commandBuffer, asc, profile.MaxAbilitySubscriptionCount);
            PrepareBuffer<CooldownGateSlot>(commandBuffer, asc, profile.MaxCooldownGateCount);
            PrepareBuffer<ActivationOwnedContributionSlot>(
                commandBuffer, asc, profile.MaxActivationOwnedContributionCount);
            PrepareBuffer<EmittedApplicationRefSlot>(
                commandBuffer, asc, profile.MaxEmittedApplicationRefCount);
            PrepareBuffer<PendingCommand>(commandBuffer, asc, profile.MaxPendingCommandCount);
            PrepareBuffer<BoundaryFactBuffer>(commandBuffer, asc, profile.MaxAscBoundaryFactCount);
        }

        /// <summary>
        /// 预挂并按 ScaleProfile 预热 Effect、payload/capture、aggregator 与 live dependency buffers。
        /// </summary>
        private static void RecordEffectBuffers(
            EntityCommandBuffer commandBuffer,
            Entity asc,
            in GasScaleProfile profile)
        {
            PrepareBuffer<ActiveEffectSlot>(commandBuffer, asc, profile.MaxActiveEffectCount);
            PrepareBuffer<GasPayloadRangeRecord>(commandBuffer, asc, profile.MaxPayloadRangeRecordCount);
            PrepareBuffer<GasPayloadValueSlot>(commandBuffer, asc, profile.MaxPayloadValueCount);
            PrepareBuffer<AttributeAggregatorSlot>(commandBuffer, asc, profile.MaxAttributeAggregatorCount);
            PrepareBuffer<LiveDependencySlot>(commandBuffer, asc, profile.MaxLiveDependencyCount);
            PrepareBuffer<LiveDependencyRouteSlot>(commandBuffer, asc, profile.MaxLiveDependencyRouteCount);
        }

        /// <summary>
        /// 在同一个 ECB command 中替换 buffer 内容并按具名 profile 字段预留容量。
        /// </summary>
        private static DynamicBuffer<T> PrepareBuffer<T>(
            EntityCommandBuffer commandBuffer,
            Entity entity,
            int capacity)
            where T : unmanaged, IBufferElementData
        {
            var buffer = commandBuffer.SetBuffer<T>(entity);
            buffer.EnsureCapacity(capacity);
            return buffer;
        }

        /// <summary>
        /// 将已全批校验的连续初始化 range 原序复制进 deferred ASC buffer。
        /// </summary>
        private static void CopyRange<T>(
            NativeArray<T> source,
            int start,
            int count,
            DynamicBuffer<T> destination)
            where T : unmanaged, IBufferElementData
        {
            for (var offset = 0; offset < count; offset++)
                destination.Add(source[start + offset]);
        }
    }
}
