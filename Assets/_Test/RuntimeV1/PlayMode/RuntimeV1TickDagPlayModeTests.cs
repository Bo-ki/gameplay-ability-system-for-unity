using System;
using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Physics.Systems;

namespace GAS.RuntimeV1.Tests.PlayMode
{
    /// <summary>
    /// 在完整 WorldOwner 批次边界中验证 Stage-C Ingress、单 Kernel DAG 与 WholeTick fault close。
    /// </summary>
    [TestFixture]
    public class RuntimeV1TickDagPlayModeTests
    {
        /// <summary>
        /// 验证 WorldOwner 幂等安装唯一父链、唯一 Ingress/Kernel，且不创建旧五组实例。
        /// </summary>
        [Test]
        public void WorldOwner_幂等安装唯一正式拓扑且不注册旧五组()
        {
            using var world = new World("Runtime v1 topology PlayMode test");
            using var owner = GasRuntimeWorldOwner.Install(world);
            var repeated = GasRuntimeWorldOwner.Install(world);

            Assert.That(repeated, Is.SameAs(owner));
            AssertFormalParentOrder(world);
            AssertIngressBeforeKernel(world);
            AssertLegacyGroupsAbsent(world);
        }

        /// <summary>
        /// 验证 owner 释放与 Port accept 在同一 Gate 锁上线性化，缓存 Port 不会留下无人消费的 journal。
        /// </summary>
        [Test]
        public void WorldOwner_Dispose后缓存Port同步拒绝新请求()
        {
            using var fixture = new RuntimeV1TickDagTestWorld();
            fixture.DisposeOwner();

            var rejected = fixture.SubmitApplyEffect(601, fixture.CurrentTick);

            Assert.That(rejected.Status, Is.EqualTo(GasCommandAcceptStatus.FaultClosed));
            Assert.That(rejected.RequestSequence, Is.Zero);
        }

        /// <summary>
        /// 验证 Ready Session 的空 Tick 仍执行完整 DAG 并只推进一次 gameplay tick。
        /// </summary>
        [Test]
        public void TickDag_空Tick成功推进且执行完整Lane链()
        {
            using var fixture = new RuntimeV1TickDagTestWorld();
            var before = fixture.CurrentTick;

            fixture.TickBatch();

            Assert.That(fixture.CurrentTick, Is.EqualTo(before + 1));
            var diagnostics = fixture.Diagnostics;
            Assert.That(diagnostics.CandidateTick, Is.EqualTo(before + 1));
            Assert.That(diagnostics.AdmissionSucceeded, Is.EqualTo(1));
            Assert.That(diagnostics.SealedCommandCount, Is.Zero);
            Assert.That(diagnostics.ExecutedLaneMask, Is.EqualTo(GasTickLaneMask.AllGameplay));
            Assert.That(fixture.SessionLifecycle.State, Is.EqualTo(GasSessionLifecycleState.Running));
            Assert.That(fixture.FixedTimestep, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(fixture.MaximumDeltaTime, Is.EqualTo(0.05f).Within(0.0001f));
        }

        /// <summary>
        /// 验证 AvailableTick 尚未到达的命令只进入持久 inbox，不会被当前 Tick 提前 seal。
        /// </summary>
        [Test]
        public void Ingress_FutureAvailableTick不提前消费()
        {
            using var fixture = new RuntimeV1TickDagTestWorld();
            var before = fixture.CurrentTick;
            var accepted = fixture.SubmitApplyEffect(701, before + 1);

            Assert.That(accepted.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
            fixture.TickBatch();

            Assert.That(fixture.CurrentTick, Is.EqualTo(before + 1));
            var inbox = fixture.Inbox;
            Assert.That(inbox.Length, Is.EqualTo(1));
            Assert.That(inbox[0].RequestSequence, Is.EqualTo(accepted.RequestSequence));
            Assert.That(inbox[0].State, Is.EqualTo(GasBoundaryCommandState.Pending));
            Assert.That(fixture.Diagnostics.SealedCommandCount, Is.Zero);
        }

        /// <summary>
        /// 验证一条 due 命令经 Port、journal、Ingress、Kernel 恰好消费一次并在确认后压缩。
        /// </summary>
        [Test]
        public void Ingress_Due命令经完整链恰好消费一次()
        {
            using var fixture = new RuntimeV1TickDagTestWorld();
            var dueTick = fixture.CurrentTick;
            var accepted = fixture.SubmitApplyEffect(801, dueTick);

            Assert.That(accepted.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
            fixture.TickBatch();
            fixture.AssertSingleConsumed(accepted.RequestSequence);

            var duplicate = fixture.SubmitApplyEffect(801, dueTick);
            Assert.That(duplicate.Status, Is.EqualTo(GasCommandAcceptStatus.DuplicateAccepted));
            Assert.That(duplicate.RequestSequence, Is.EqualTo(accepted.RequestSequence));
            fixture.TickBatch();

            Assert.That(fixture.Inbox.Length, Is.Zero);
            Assert.That(fixture.CurrentTick, Is.EqualTo(dueTick + 2));
        }

        /// <summary>
        /// 验证 owner-plan 容量失败不推进 Tick，仍执行全部 lane，并由 outer fence 原子关闭 Gate。
        /// </summary>
        [Test]
        public void Admission_OwnerPlan容量失败零写并在OuterFence关闭Ingress()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(0);
            var before = fixture.CaptureGameplayAuthority();
            var accepted = fixture.SubmitApplyEffect(901, before.Tick.CurrentTick);

            Assert.That(accepted.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
            fixture.TickBatch();

            fixture.AssertAdmissionFault(in before, accepted.RequestSequence);
            var rejected = fixture.SubmitApplyEffect(902, before.Tick.CurrentTick);
            Assert.That(rejected.Status, Is.EqualTo(GasCommandAcceptStatus.FaultClosed));
            Assert.That(rejected.IsAccepted, Is.False);
        }

        /// <summary>
        /// 验证 FixedStep 的完整系统句柄顺序严格满足 Physics、GAS、标准 EndFixed。
        /// </summary>
        private static void AssertFormalParentOrder(World world)
        {
            var fixedStep = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            var physics = world.GetExistingSystemManaged<PhysicsSystemGroup>();
            var gas = world.GetExistingSystemManaged<GasFixedTickSystemGroup>();
            var endFixed = world.GetExistingSystemManaged<
                EndFixedStepSimulationEntityCommandBufferSystem>();
            using var systems = fixedStep.GetAllSystems();
            var physicsIndex = FindSystemIndex(systems, physics.SystemHandle);
            var gasIndex = FindSystemIndex(systems, gas.SystemHandle);
            var endFixedIndex = FindSystemIndex(systems, endFixed.SystemHandle);

            Assert.That(physicsIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(gasIndex, Is.GreaterThan(physicsIndex));
            Assert.That(endFixedIndex, Is.GreaterThan(gasIndex));
        }

        /// <summary>
        /// 验证 GAS 子组中 managed Ingress 位于唯一 unmanaged Kernel 前。
        /// </summary>
        private static void AssertIngressBeforeKernel(World world)
        {
            var gas = world.GetExistingSystemManaged<GasFixedTickSystemGroup>();
            var ingress = world.GetExistingSystemManaged<GasCommandIngressSystem>();
            var kernel = world.GetExistingSystem<GasTickKernelSystem>();
            using var systems = gas.GetAllSystems();
            var ingressIndex = FindSystemIndex(systems, ingress.SystemHandle);
            var kernelIndex = FindSystemIndex(systems, kernel);

            Assert.That(ingressIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(kernelIndex, Is.GreaterThan(ingressIndex));
        }

        /// <summary>
        /// 验证破坏性安装不再隐式创建旧五个 phase group。
        /// </summary>
        private static void AssertLegacyGroupsAbsent(World world)
        {
            Assert.That(world.GetExistingSystemManaged<GASFramePrepareSystemGroup>(), Is.Null);
            Assert.That(world.GetExistingSystemManaged<GASCommandResolveSystemGroup>(), Is.Null);
            Assert.That(world.GetExistingSystemManaged<GASCoreSimulationSystemGroup>(), Is.Null);
            Assert.That(world.GetExistingSystemManaged<GEExecutionCalculationExtensionSystemGroup>(), Is.Null);
            Assert.That(world.GetExistingSystemManaged<GASStructuralCommitSystemGroup>(), Is.Null);
            Assert.That(world.GetExistingSystemManaged<GASBoundaryProjectionSystemGroup>(), Is.Null);
        }

        /// <summary>
        /// 返回已排序系统句柄在父组中的位置，未注册时返回 -1。
        /// </summary>
        private static int FindSystemIndex(
            NativeList<SystemHandle> systems,
            SystemHandle expected)
        {
            for (var index = 0; index < systems.Length; index++)
            {
                if (systems[index].Equals(expected))
                    return index;
            }
            return -1;
        }
    }

    /// <summary>
    /// 使用正式 WorldOwner 与 Stage-B recorder 构造一战局一 ASC 的最小 Ready Session。
    /// </summary>
    internal sealed class RuntimeV1TickDagTestWorld : IDisposable
    {
        private const ulong Epoch = 61;
        private const ulong SpawnBatchId = 601;
        private const ulong SchemaHash = 6101;
        private const ulong ContentHash = 6102;
        private const ulong AttributeHash = 6103;
        private const ulong TagHash = 6104;
        private const int EffectDefinitionId = 6201;
        private const float FixedDeltaTime = 0.05f;

        private readonly BlobAssetReference<GasDefinitionCatalogBlob> _catalog;
        private readonly World _world;
        private readonly GasRuntimeWorldOwner _owner;
        private double _elapsedTime = -FixedDeltaTime;
        private GasStageBBootstrapRecordGate _recordGate;

        internal Entity Session { get; private set; }
        internal Entity Asc { get; private set; }
        internal BattleInstanceHandle Battle { get; } = new BattleInstanceHandle(Epoch, 71, 1);
        internal OwnerAscHandle OwnerAsc { get; } = new OwnerAscHandle(101, 1);

        internal ulong CurrentTick => EntityManager.GetComponentData<SimulationTickState>(Session).CurrentTick;
        internal GasTickDiagnostics Diagnostics => EntityManager.GetComponentData<GasTickDiagnostics>(Session);
        internal GasSessionLifecycle SessionLifecycle => EntityManager.GetComponentData<GasSessionLifecycle>(Session);
        internal DynamicBuffer<BoundaryCommandInbox> Inbox => EntityManager.GetBuffer<BoundaryCommandInbox>(Session);
        internal float FixedTimestep => _world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Timestep;
        internal float MaximumDeltaTime => _world.MaximumDeltaTime;

        private EntityManager EntityManager => _world.EntityManager;

        /// <summary>
        /// 安装正式拓扑、记录最小 SpawnBatch，并用两次完整批次发布 Ready。
        /// </summary>
        internal RuntimeV1TickDagTestWorld(int maxOwnerPlanCount = 4)
        {
            _catalog = CreateEmptyCatalog();
            _world = new World("Runtime v1 Tick DAG PlayMode test");
            _owner = GasRuntimeWorldOwner.Install(_world);
            _world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Timestep = FixedDeltaTime;
            RecordBootstrap(maxOwnerPlanCount);
            TickBatch();
            TickBatch();
            ResolveReadyEntities();
        }

        /// <summary>
        /// 先释放持有 gate/World 引用的 owner，再释放 World 与测试 Blob。
        /// </summary>
        public void Dispose()
        {
            _owner.Dispose();
            _world.Dispose();
            if (_catalog.IsCreated)
                _catalog.Dispose();
        }

        /// <summary>
        /// 仅释放正式 owner，保留 World 以验证缓存 Port 的同步终态。
        /// </summary>
        internal void DisposeOwner()
        {
            _owner.Dispose();
        }

        /// <summary>
        /// 推进恰好一次完整 FixedStep 并完成 outer batch fence 握手。
        /// </summary>
        internal void TickBatch()
        {
            _elapsedTime += FixedDeltaTime;
            _world.SetTime(new TimeData(_elapsedTime, FixedDeltaTime));
            _owner.TickBatch();
        }

        /// <summary>
        /// 通过唯一 typed Port 提交一个无 payload 的 ApplyEffect 命令。
        /// </summary>
        internal GasCommandAcceptResult SubmitApplyEffect(ulong requestId, ulong availableTick)
        {
            var target = BoundaryTargetRef.ForAsc(Battle, OwnerAsc);
            var context = new GasBoundaryCommandContext(
                Epoch,
                requestId,
                requestId + 1000,
                availableTick,
                false,
                Battle,
                default,
                target);
            return _owner.Port.RequestApplyEffect(
                context,
                EffectDefinitionId,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
        }

        /// <summary>
        /// 捕获 fault 前必须保持完全不变的 Tick、RNG、slab 与各类 durable 长度。
        /// </summary>
        internal RuntimeV1GameplayAuthoritySnapshot CaptureGameplayAuthority()
        {
            return new RuntimeV1GameplayAuthoritySnapshot(
                EntityManager.GetComponentData<SimulationTickState>(Session),
                EntityManager.GetComponentData<AscRandomState>(Asc),
                EntityManager.GetComponentData<AscSlabHeads>(Asc),
                EntityManager.GetBuffer<AttributeValueSlot>(Asc).Length,
                EntityManager.GetBuffer<TagCountSlot>(Asc).Length,
                EntityManager.GetBuffer<PendingCommand>(Asc).Length,
                EntityManager.GetBuffer<BoundaryFactBuffer>(Asc).Length,
                EntityManager.GetBuffer<BoundaryFactBuffer>(Session).Length);
        }

        /// <summary>
        /// 验证已成功消费的唯一 inbox 记录与当 Tick 诊断，尚未进入下一轮物理压缩。
        /// </summary>
        internal void AssertSingleConsumed(ulong requestSequence)
        {
            var inbox = Inbox;
            Assert.That(inbox.Length, Is.EqualTo(1));
            Assert.That(inbox[0].RequestSequence, Is.EqualTo(requestSequence));
            Assert.That(inbox[0].State, Is.EqualTo(GasBoundaryCommandState.Consumed));
            Assert.That(Diagnostics.SealedCommandCount, Is.EqualTo(1));
            Assert.That(Diagnostics.AdmissionSucceeded, Is.EqualTo(1));
            Assert.That(Diagnostics.ExecutedLaneMask, Is.EqualTo(GasTickLaneMask.AllGameplay));
        }

        /// <summary>
        /// 验证 WholeTick 失败证据、零 gameplay 写与同 gate lock 上完成的 fault close 回执。
        /// </summary>
        internal void AssertAdmissionFault(
            in RuntimeV1GameplayAuthoritySnapshot before,
            ulong requestSequence)
        {
            AssertGameplayAuthorityUnchanged(in before);
            AssertFaultDiagnostics(before.Tick.CurrentTick + 1);
            var latch = EntityManager.GetComponentData<SessionFaultLatch>(Session);
            AssertFaultLatch(in latch, requestSequence);
            Assert.That(SessionLifecycle.State, Is.EqualTo(GasSessionLifecycleState.Faulted));
            Assert.That(Inbox.Length, Is.EqualTo(1));
            Assert.That(Inbox[0].State, Is.EqualTo(GasBoundaryCommandState.FaultTerminated));
            Assert.That(EntityManager.GetBuffer<BattleInstanceSlot>(Session)[0].IngressClosed, Is.EqualTo(1));
            Assert.That(EntityManager.GetComponentData<AscLifecycle>(Asc).IngressClosed, Is.EqualTo(1));
        }

        /// <summary>
        /// 通过正式 EndFixed singleton 记录一个无初始化数据的最小 SpawnBatch。
        /// </summary>
        private void RecordBootstrap(int maxOwnerPlanCount)
        {
            using var battles = CreateBattleRequests();
            using var ascs = CreateAscRequests();
            using var attributes = new NativeArray<PendingAttributeInitialization>(0, Allocator.Temp);
            using var tags = new NativeArray<PendingTagInitialization>(0, Allocator.Temp);
            using var abilities = new NativeArray<PendingGrantedAbilityInitialization>(0, Allocator.Temp);
            var request = CreateSessionRequest(maxOwnerPlanCount);
            var failure = GasStageBBootstrapRecorder.Record(
                EntityManager,
                GetEndFixedSingleton(),
                _world.Unmanaged,
                ref _recordGate,
                in request,
                battles,
                ascs,
                attributes,
                tags,
                abilities,
                out _);
            Assert.That(failure, Is.EqualTo(GasStageBSpawnFaultReason.None));
        }

        /// <summary>
        /// 查询两轮后唯一 Session/ASC 并确认 Stage-B 已完整发布 Ready。
        /// </summary>
        private void ResolveReadyEntities()
        {
            using var query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasSessionIdentity>(),
                ComponentType.ReadOnly<GasActiveSessionAuthority>());
            Assert.That(query.CalculateEntityCount(), Is.EqualTo(1));
            Session = query.GetSingletonEntity();
            var registry = EntityManager.GetBuffer<AscRegistrySlot>(Session);
            Assert.That(registry.Length, Is.EqualTo(1));
            Asc = registry[0].ResolveRuntimeEntity();
            Assert.That(SessionLifecycle.State, Is.EqualTo(GasSessionLifecycleState.Ready));
            Assert.That(CurrentTick, Is.Zero);
            Assert.That(registry[0].State, Is.EqualTo(GasAscRegistryState.Ready));
        }

        /// <summary>
        /// 构造 Session、空 Catalog expectation 与完整版本化容量档位。
        /// </summary>
        private GasStageBSessionBootstrapRequest CreateSessionRequest(int maxOwnerPlanCount)
        {
            return new GasStageBSessionBootstrapRequest
            {
                SimulationEpoch = Epoch,
                SpawnBatchId = SpawnBatchId,
                Config = new GasSessionConfig
                {
                    TickRate = 20,
                    RuleVersion = 1,
                    BoundaryPolicyVersion = 1,
                },
                ScaleProfile = CreateScaleProfile(maxOwnerPlanCount),
                Catalog = _catalog,
                CatalogExpectation = new GasCatalogValidationExpectation(
                    GasDefinitionCatalogSchema.Version,
                    SchemaHash,
                    ContentHash,
                    AttributeHash,
                    TagHash),
            };
        }

        /// <summary>
        /// 构造包含唯一 Ready 成员范围的 Battle 请求。
        /// </summary>
        private NativeArray<GasStageBBattleBootstrapRequest> CreateBattleRequests()
        {
            var values = new NativeArray<GasStageBBattleBootstrapRequest>(1, Allocator.Temp);
            values[0] = new GasStageBBattleBootstrapRequest
            {
                BattleInstance = Battle,
                MemberStart = 0,
                MemberCount = 1,
                MembershipOrdinalRoot = 10,
            };
            return values;
        }

        /// <summary>
        /// 构造唯一 ASC 的稳定成员关系，三类初始化 range 均为空。
        /// </summary>
        private NativeArray<GasStageBAscBootstrapRequest> CreateAscRequests()
        {
            var values = new NativeArray<GasStageBAscBootstrapRequest>(1, Allocator.Temp);
            values[0] = new GasStageBAscBootstrapRequest
            {
                OwnerAsc = OwnerAsc,
                BattleInstance = Battle,
                RegistryOrdinal = 0,
                ScenarioUnitId = 900,
                MembershipOrdinal = 10,
                OwnerActorStableId = 1000,
                AvatarActorStableId = 2000,
                ActorBindingGeneration = 1,
                RandomState0 = 3000,
                RandomState1 = 4000,
            };
            return values;
        }

        /// <summary>
        /// 返回标准 EndFixed 系统注册的 singleton，确保 bootstrap 只通过正式 playback 边界。
        /// </summary>
        private EndFixedStepSimulationEntityCommandBufferSystem.Singleton GetEndFixedSingleton()
        {
            using var query = EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>(),
                },
                Options = EntityQueryOptions.IncludeSystems,
            });
            return query.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        }

        /// <summary>
        /// 验证 fault 后 Tick、RNG、slab 与 durable gameplay buffer 均保持原值。
        /// </summary>
        private void AssertGameplayAuthorityUnchanged(
            in RuntimeV1GameplayAuthoritySnapshot before)
        {
            Assert.That(EntityManager.GetComponentData<SimulationTickState>(Session), Is.EqualTo(before.Tick));
            Assert.That(EntityManager.GetComponentData<AscRandomState>(Asc), Is.EqualTo(before.Random));
            Assert.That(EntityManager.GetComponentData<AscSlabHeads>(Asc), Is.EqualTo(before.Slabs));
            Assert.That(EntityManager.GetBuffer<AttributeValueSlot>(Asc).Length, Is.EqualTo(before.AttributeCount));
            Assert.That(EntityManager.GetBuffer<TagCountSlot>(Asc).Length, Is.EqualTo(before.TagCount));
            Assert.That(EntityManager.GetBuffer<PendingCommand>(Asc).Length, Is.EqualTo(before.PendingCount));
            Assert.That(EntityManager.GetBuffer<BoundaryFactBuffer>(Asc).Length, Is.EqualTo(before.AscFactCount));
            Assert.That(EntityManager.GetBuffer<BoundaryFactBuffer>(Session).Length,
                Is.EqualTo(before.SessionFactCount));
        }

        /// <summary>
        /// 验证 admission 失败仍无条件进入全部预排 lane 并发布唯一原因码。
        /// </summary>
        private void AssertFaultDiagnostics(ulong candidateTick)
        {
            var diagnostics = Diagnostics;
            Assert.That(diagnostics.CandidateTick, Is.EqualTo(candidateTick));
            Assert.That(diagnostics.ExecutedLaneMask, Is.EqualTo(GasTickLaneMask.AllGameplay));
            Assert.That(diagnostics.AdmissionSucceeded, Is.Zero);
            Assert.That(diagnostics.AdmissionReasonCode,
                Is.EqualTo((int)GasTickAdmissionFailureReason.OwnerPlanLimit));
            Assert.That(diagnostics.SealedCommandCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 验证 deterministic sealed subset 与 gate outstanding close receipt 指向同一请求。
        /// </summary>
        private static void AssertFaultLatch(
            in SessionFaultLatch latch,
            ulong requestSequence)
        {
            Assert.That(latch.FaultId, Is.Not.Zero);
            Assert.That(latch.Detected, Is.EqualTo(1));
            Assert.That(latch.IngressClosed, Is.EqualTo(1));
            Assert.That(latch.SealedFirstRequestSequence, Is.EqualTo(requestSequence));
            Assert.That(latch.SealedLastRequestSequence, Is.EqualTo(requestSequence));
            Assert.That(latch.SealedRequestCount, Is.EqualTo(1));
            Assert.That(latch.OutstandingFirstRequestSequence, Is.EqualTo(requestSequence));
            Assert.That(latch.OutstandingLastRequestSequence, Is.EqualTo(requestSequence));
            Assert.That(latch.OutstandingRequestCount, Is.EqualTo(1));
            Assert.That(latch.OutstandingRequestHash, Is.Not.Zero);
        }

        /// <summary>
        /// 构造满足单命令 Stage-C DAG 与 Stage-B 空初始化的最小合法 ScaleProfile。
        /// </summary>
        private static GasScaleProfile CreateScaleProfile(int maxOwnerPlanCount)
        {
            return new GasScaleProfile
            {
                ProfileId = 1,
                ProfileVersion = 1,
                ProfileHash = 6001,
                MaxFixedTicksPerBatch = 1,
                MaximumDeltaTimeTicks = 1,
                MaxSpawnBatchSize = 1,
                MaxBattleInstanceCount = 1,
                MaxAscRegistryCount = 1,
                MaxBoundaryCommandCount = 4,
                MaxBoundaryCommandPayloadCount = 16,
                MaxOwnerPlanCount = maxOwnerPlanCount,
                MaxResolvedTargetCount = 4,
                MaxEffectOperationCount = 4,
                MaxOwnerReservationCount = 4,
                MaxTargetReservationCount = 4,
                MaxCoreFactCount = 4,
                MaxNextTickRouteCount = 4,
                MaxStructuralIntentCount = 4,
            };
        }

        /// <summary>
        /// 构造所有定义数组为空但 header/hash 完整的合法 immutable Catalog。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateEmptyCatalog()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                root.SchemaVersion = GasDefinitionCatalogSchema.Version;
                root.SchemaHash = SchemaHash;
                root.ContentHash = ContentHash;
                root.AttributeLayout.LayoutHash = AttributeHash;
                root.TagCatalog.CatalogHash = TagHash;
                AllocateEmptyCatalogArrays(ref builder, ref root);
                return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 显式分配全部空 BlobArray，避免默认 offset 被误解为另一种 Catalog 形状。
        /// </summary>
        private static void AllocateEmptyCatalogArrays(
            ref BlobBuilder builder,
            ref GasDefinitionCatalogBlob root)
        {
            builder.Allocate(ref root.AttributeLayout.Entries, 0);
            builder.Allocate(ref root.TagCatalog.Entries, 0);
            builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
            builder.Allocate(ref root.AbilityIndex, 0);
            builder.Allocate(ref root.Abilities, 0);
            builder.Allocate(ref root.GameplayEffectIndex, 0);
            builder.Allocate(ref root.GameplayEffects, 0);
            builder.Allocate(ref root.Requirements, 0);
            builder.Allocate(ref root.RequirementTagIndices, 0);
            builder.Allocate(ref root.CaptureDescriptors, 0);
            builder.Allocate(ref root.Modifiers, 0);
            builder.Allocate(ref root.DirectEffectProgramNodes, 0);
            builder.Allocate(ref root.CueReferences, 0);
            builder.Allocate(ref root.ValueViews, 0);
            builder.Allocate(ref root.EvaluatorInstructions, 0);
            builder.Allocate(ref root.SetByCallerDescriptors, 0);
            builder.Allocate(ref root.TargetDataDescriptors, 0);
            builder.Allocate(ref root.EffectContextFieldDescriptors, 0);
        }
    }

    /// <summary>
    /// 冻结 WholeTick fault 前必须保持不变的最小 gameplay authority 快照。
    /// </summary>
    internal readonly struct RuntimeV1GameplayAuthoritySnapshot
    {
        internal readonly SimulationTickState Tick;
        internal readonly AscRandomState Random;
        internal readonly AscSlabHeads Slabs;
        internal readonly int AttributeCount;
        internal readonly int TagCount;
        internal readonly int PendingCount;
        internal readonly int AscFactCount;
        internal readonly int SessionFactCount;

        /// <summary>
        /// 保存全部权威值与 durable buffer 长度，排除允许写入的 fault/inbox 控制态。
        /// </summary>
        internal RuntimeV1GameplayAuthoritySnapshot(
            SimulationTickState tick,
            AscRandomState random,
            AscSlabHeads slabs,
            int attributeCount,
            int tagCount,
            int pendingCount,
            int ascFactCount,
            int sessionFactCount)
        {
            Tick = tick;
            Random = random;
            Slabs = slabs;
            AttributeCount = attributeCount;
            TagCount = tagCount;
            PendingCount = pendingCount;
            AscFactCount = ascFactCount;
            SessionFactCount = sessionFactCount;
        }
    }
}
