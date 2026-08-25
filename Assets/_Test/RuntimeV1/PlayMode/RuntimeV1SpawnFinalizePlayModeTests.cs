using System;
using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;

namespace GAS.RuntimeV1.Tests.PlayMode
{
    /// <summary>
    /// 在完整 FixedStep 父链中验证 Stage-B SpawnBatch 的 Pending、原子 Ready 与失败 teardown。
    /// </summary>
    [TestFixture]
    public class RuntimeV1SpawnFinalizePlayModeTests
    {
        /// <summary>
        /// 验证首轮标准 EndFixed 只创建 Pending，下一轮同 Kernel 才整批发布 Ready且不递增 gameplay tick。
        /// </summary>
        [Test]
        public void SpawnBatch_跨两个完整FixedStep整批发布Ready()
        {
            using var fixture = new RuntimeV1SpawnTestWorld();
            fixture.AssertFormalFixedStepOrder();
            Assert.That(fixture.RecordTwoAscBatch(), Is.EqualTo(GasStageBSpawnFaultReason.None));

            fixture.UpdateFixedStep();
            fixture.AssertPendingBatch();

            fixture.UpdateFixedStep();
            fixture.AssertReadyBatch();
        }

        /// <summary>
        /// 验证任一 Pending 引用在 playback 后损坏时无成员 Ready，Session Faulted并于同轮 EndFixed转 cleanup shell。
        /// </summary>
        [Test]
        public void SpawnBatch_任一成员失败时ZeroReady并整批Teardown()
        {
            using var fixture = new RuntimeV1SpawnTestWorld();
            Assert.That(fixture.RecordTwoAscBatch(), Is.EqualTo(GasStageBSpawnFaultReason.None));
            fixture.UpdateFixedStep();
            fixture.CorruptFirstPendingTag();

            fixture.UpdateFixedStep();
            fixture.AssertFaultedBatch();
        }

        /// <summary>
        /// 验证 registry 与 member 同步裁短仍被原始 manifest 拒绝，遗漏 ASC 由 batch marker 兜底清理。
        /// </summary>
        [Test]
        public void SpawnBatch_自洽子集截断时Manifest拒绝并清理遗漏成员()
        {
            using var fixture = new RuntimeV1SpawnTestWorld();
            Assert.That(fixture.RecordTwoAscBatch(), Is.EqualTo(GasStageBSpawnFaultReason.None));
            fixture.UpdateFixedStep();
            var omittedAsc = fixture.TruncateRegistryAndManifestToOneMember();

            fixture.UpdateFixedStep();
            fixture.AssertManifestFaultedBatch(omittedAsc);
        }

        /// <summary>
        /// 验证预先污染为 DestroyPending 不能冒充“已记录 ECB”，失败路径仍为该成员登记 Destroy。
        /// </summary>
        [Test]
        public void SpawnBatch_预置DestroyPending仍由本轮显式Teardown证明清理()
        {
            using var fixture = new RuntimeV1SpawnTestWorld();
            Assert.That(fixture.RecordTwoAscBatch(), Is.EqualTo(GasStageBSpawnFaultReason.None));
            fixture.UpdateFixedStep();
            fixture.CorruptFirstLifecycleToDestroyPending();

            fixture.UpdateFixedStep();
            fixture.AssertFaultedBatch(GasStageBSpawnFaultReason.AscIdentityInvalid);
        }

        /// <summary>
        /// 验证 playback 后替换仍然合法的 Session 规则，也会因原始 transaction hash 不匹配整批失败。
        /// </summary>
        [Test]
        public void SpawnBatch_安装规则被替换时Manifest拒绝重新解释()
        {
            using var fixture = new RuntimeV1SpawnTestWorld();
            Assert.That(fixture.RecordTwoAscBatch(), Is.EqualTo(GasStageBSpawnFaultReason.None));
            fixture.UpdateFixedStep();
            fixture.CorruptSessionRuleVersion();

            fixture.UpdateFixedStep();
            fixture.AssertFaultedBatch();
        }

        /// <summary>
        /// 验证保留 GasSessionIdentity 但没有 active authority 的诊断实体既不阻止录入也不参与 Kernel 基数。
        /// </summary>
        [Test]
        public void SessionAuthority_诊断Session不计入Active基数()
        {
            using var fixture = new RuntimeV1SpawnTestWorld();
            var diagnostic = fixture.CreateDiagnosticSessionWithoutAuthority();
            Assert.That(fixture.RecordTwoAscBatch(), Is.EqualTo(GasStageBSpawnFaultReason.None));

            fixture.UpdateFixedStep();
            fixture.AssertPendingBatch();
            fixture.UpdateFixedStep();
            fixture.AssertReadyBatch();
            fixture.AssertDiagnosticSessionRetained(diagnostic);
        }

        /// <summary>
        /// 验证两个 active Session 均锁存 cardinality fault，且共享 Registry Entity 只登记一次 Destroy。
        /// </summary>
        [Test]
        public void SessionAuthority_双ActiveSession整域Fault且共享Asc去重Teardown()
        {
            using var fixture = new RuntimeV1SpawnTestWorld();
            Assert.That(fixture.RecordTwoAscBatch(), Is.EqualTo(GasStageBSpawnFaultReason.None));
            fixture.UpdateFixedStep();
            var conflict = fixture.CreateConflictingActiveSessionSharingRegistry(out var original);

            fixture.UpdateFixedStep();
            fixture.AssertCardinalityFaulted(original, conflict);
        }

        /// <summary>
        /// 验证仅携带 authority marker 的畸形 Session 仍会阻止第二个 Session 录入。
        /// </summary>
        [Test]
        public void SessionAuthority_缺Identity与Lifecycle仍阻止重复录入()
        {
            using var fixture = new RuntimeV1SpawnTestWorld();
            var malformed = fixture.CreateAuthorityOnlySession();

            Assert.That(fixture.RecordTwoAscBatch(),
                Is.EqualTo(GasStageBSpawnFaultReason.SessionCardinality));
            fixture.UpdateFixedStep();
            fixture.AssertEntityDestroyed(malformed);
        }

        /// <summary>
        /// 验证 Pending batch 遇到 authority-only 冲突时整批 zero-ready，畸形 authority 同轮销毁。
        /// </summary>
        [Test]
        public void SessionAuthority_Pending与畸形Authority冲突时FailClosed()
        {
            using var fixture = new RuntimeV1SpawnTestWorld();
            Assert.That(fixture.RecordTwoAscBatch(), Is.EqualTo(GasStageBSpawnFaultReason.None));
            fixture.UpdateFixedStep();
            var malformed = fixture.CreateAuthorityOnlySession();

            fixture.UpdateFixedStep();
            fixture.AssertFaultedBatch(GasStageBSpawnFaultReason.SessionCardinality);
            fixture.AssertEntityDestroyed(malformed);
        }

        /// <summary>
        /// 验证 membership ordinal 连续区间超出 int 上限时由 recorder 显式拒绝而不允许回绕。
        /// </summary>
        [Test]
        public void SpawnBatch_MembershipOrdinal范围回绕时Recorder拒绝()
        {
            using var fixture = new RuntimeV1SpawnTestWorld();

            Assert.That(fixture.RecordMembershipOrdinalOverflowBatch(),
                Is.EqualTo(GasStageBSpawnFaultReason.BattleInvalid));
        }
    }

    /// <summary>
    /// 构造测试专用完整 FixedStep 链、immutable Catalog 与两成员 SpawnBatch，并集中释放 World/Blob owner。
    /// </summary>
    internal sealed class RuntimeV1SpawnTestWorld : IDisposable
    {
        private const ulong Epoch = 41;
        private const ulong SpawnBatchId = 501;
        private const ulong SchemaHash = 1101;
        private const ulong ContentHash = 1102;
        private const ulong AttributeHash = 1103;
        private const ulong TagHash = 1104;

        private readonly World _world;
        private readonly BlobAssetReference<GasDefinitionCatalogBlob> _catalog;
        private readonly FixedStepSimulationSystemGroup _fixedStep;
        private readonly PhysicsSystemGroup _physics;
        private readonly GasFixedTickSystemGroup _gas;
        private readonly EndFixedStepSimulationEntityCommandBufferSystem _endFixed;
        private GasStageBBootstrapRecordGate _recordGate;

        /// <summary>
        /// 创建显式安装的新 Runtime group/kernel 与标准 Physics、EndFixed 的测试 World。
        /// </summary>
        internal RuntimeV1SpawnTestWorld()
        {
            _catalog = CreateCatalog();
            _world = new World("Runtime v1 SpawnFinalize PlayMode test");
            _fixedStep = _world.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            _fixedStep.RateManager = null;
            _physics = _world.CreateSystemManaged<PhysicsSystemGroup>();
            _gas = _world.CreateSystemManaged<GasFixedTickSystemGroup>();
            _endFixed = _world.CreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
            var kernel = _world.CreateSystem<GasTickKernelSystem>();
            _gas.AddSystemToUpdateList(kernel);
            _fixedStep.AddSystemToUpdateList(_physics);
            _fixedStep.AddSystemToUpdateList(_gas);
            _fixedStep.AddSystemToUpdateList(_endFixed);
            _gas.SortSystems();
            _fixedStep.SortSystems();
        }

        /// <summary>
        /// 释放持有 ECS 引用的 World 后再释放测试 Catalog Blob。
        /// </summary>
        public void Dispose()
        {
            _world.Dispose();
            if (_catalog.IsCreated)
                _catalog.Dispose();
        }

        /// <summary>
        /// 验证实际 FixedStep managed update list 按 Physics、GAS、EndFixed 排序。
        /// </summary>
        internal void AssertFormalFixedStepOrder()
        {
            var physicsIndex = FindManagedSystemIndex(_physics);
            var gasIndex = FindManagedSystemIndex(_gas);
            var endFixedIndex = FindManagedSystemIndex(_endFixed);

            Assert.That(physicsIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(gasIndex, Is.GreaterThan(physicsIndex));
            Assert.That(endFixedIndex, Is.GreaterThan(gasIndex));
        }

        /// <summary>
        /// 通过标准 EndFixed singleton 记录一个 Battle、两个 ASC 的最小 Stage-B batch。
        /// </summary>
        internal GasStageBSpawnFaultReason RecordTwoAscBatch()
        {
            using var battles = CreateBattleRequests();
            using var ascs = CreateAscRequests();
            using var attributes = CreateAttributeInitializations();
            using var tags = CreateTagInitializations();
            using var abilities = CreateAbilityInitializations();
            var request = CreateSessionRequest();
            var endFixed = GetEndFixedSingleton();

            return GasStageBBootstrapRecorder.Record(
                _world.EntityManager,
                endFixed,
                _world.Unmanaged,
                ref _recordGate,
                in request,
                battles,
                ascs,
                attributes,
                tags,
                abilities,
                out _);
        }

        /// <summary>
        /// 记录一个会令第二个 membership ordinal 回绕的非法请求，验证入口的有界加法契约。
        /// </summary>
        internal GasStageBSpawnFaultReason RecordMembershipOrdinalOverflowBatch()
        {
            using var battles = CreateBattleRequests(int.MaxValue);
            using var ascs = CreateAscRequests(int.MaxValue, int.MinValue);
            using var attributes = CreateAttributeInitializations();
            using var tags = CreateTagInitializations();
            using var abilities = CreateAbilityInitializations();
            var request = CreateSessionRequest();

            return GasStageBBootstrapRecorder.Record(
                _world.EntityManager,
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
        }

        /// <summary>
        /// 更新完整 FixedStep 父组，使 Physics、GAS Job 和标准 EndFixed 按正式顺序运行。
        /// </summary>
        internal void UpdateFixedStep()
        {
            _fixedStep.Update();
        }

        /// <summary>
        /// 创建只有稳定身份、没有 gameplay authority marker 的诊断保留 Session。
        /// </summary>
        internal Entity CreateDiagnosticSessionWithoutAuthority()
        {
            var diagnostic = _world.EntityManager.CreateEntity(ComponentType.ReadWrite<GasSessionIdentity>());
            _world.EntityManager.SetComponentData(diagnostic, new GasSessionIdentity
            {
                SimulationEpoch = Epoch + 1000,
            });
            return diagnostic;
        }

        /// <summary>
        /// 创建只有 active authority marker 的畸形 Session，用于验证基数查询不依赖其余布局。
        /// </summary>
        internal Entity CreateAuthorityOnlySession()
        {
            return _world.EntityManager.CreateEntity(
                ComponentType.ReadWrite<GasActiveSessionAuthority>());
        }

        /// <summary>
        /// 验证标准 EndFixed 已彻底移除不含 cleanup 数据的畸形 Entity。
        /// </summary>
        internal void AssertEntityDestroyed(Entity entity)
        {
            Assert.That(_world.EntityManager.Exists(entity), Is.False);
        }

        /// <summary>
        /// 验证诊断 Session 未被 active Kernel 修改或清理。
        /// </summary>
        internal void AssertDiagnosticSessionRetained(Entity diagnostic)
        {
            Assert.That(_world.EntityManager.Exists(diagnostic), Is.True);
            Assert.That(_world.EntityManager.HasComponent<GasSessionIdentity>(diagnostic), Is.True);
            Assert.That(_world.EntityManager.HasComponent<GasActiveSessionAuthority>(diagnostic), Is.False);
        }

        /// <summary>
        /// 构造第二个 active Session，并故意复制原 Session registry 以覆盖跨 Session Destroy 去重。
        /// </summary>
        internal Entity CreateConflictingActiveSessionSharingRegistry(out Entity original)
        {
            original = GetSession();
            var conflict = _world.EntityManager.CreateEntity(
                ComponentType.ReadWrite<GasSessionIdentity>(),
                ComponentType.ReadWrite<GasActiveSessionAuthority>(),
                ComponentType.ReadWrite<GasSessionLifecycle>(),
                ComponentType.ReadWrite<SessionFaultLatch>(),
                ComponentType.ReadWrite<BattleInstanceSlot>(),
                ComponentType.ReadWrite<AscRegistrySlot>());
            _world.EntityManager.SetComponentData(conflict,
                _world.EntityManager.GetComponentData<GasSessionIdentity>(original));
            _world.EntityManager.SetComponentData(conflict,
                _world.EntityManager.GetComponentData<GasSessionLifecycle>(original));
            CopyBuffer(_world.EntityManager.GetBuffer<BattleInstanceSlot>(original),
                _world.EntityManager.GetBuffer<BattleInstanceSlot>(conflict));
            CopyBuffer(_world.EntityManager.GetBuffer<AscRegistrySlot>(original),
                _world.EntityManager.GetBuffer<AscRegistrySlot>(conflict));
            return conflict;
        }

        /// <summary>
        /// 验证两个冲突 Session 的 fault 证据与共享 ASC cleanup shell 均完整。
        /// </summary>
        internal void AssertCardinalityFaulted(Entity original, Entity conflict)
        {
            AssertCardinalityFault(original);
            AssertCardinalityFault(conflict);
            var registry = _world.EntityManager.GetBuffer<AscRegistrySlot>(original);
            for (var index = 0; index < registry.Length; index++)
                AssertCleanupShell(registry[index].ResolveRuntimeEntity());
            using var pendingMarkers = _world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasSpawnBatchMarker>());
            Assert.That(pendingMarkers.CalculateEntityCount(), Is.Zero);
        }

        /// <summary>
        /// 验证首轮 playback 后只有 Pending 物理结果，gameplay authority 与 tick仍为零。
        /// </summary>
        internal void AssertPendingBatch()
        {
            var session = GetSession();
            var lifecycle = _world.EntityManager.GetComponentData<GasSessionLifecycle>(session);
            var tick = _world.EntityManager.GetComponentData<SimulationTickState>(session);
            var registry = _world.EntityManager.GetBuffer<AscRegistrySlot>(session);
            var manifest = _world.EntityManager.GetComponentData<GasSpawnBatchManifest>(session);
            var members = _world.EntityManager.GetBuffer<SpawnBatchMemberManifestSlot>(session);

            Assert.That(lifecycle.State, Is.EqualTo(GasSessionLifecycleState.SpawnPending));
            Assert.That(lifecycle.ActiveSpawnBatchId, Is.EqualTo(SpawnBatchId));
            Assert.That(tick.CurrentTick, Is.Zero);
            Assert.That(registry.Length, Is.EqualTo(2));
            Assert.That(manifest.Pending, Is.EqualTo(1));
            Assert.That(manifest.ExpectedAscCount, Is.EqualTo(2));
            Assert.That(manifest.ContentHash, Is.Not.Zero);
            Assert.That(members.Length, Is.EqualTo(2));
            for (var index = 0; index < registry.Length; index++)
            {
                Assert.That(registry[index].State, Is.EqualTo(GasAscRegistryState.Pending));
                var asc = registry[index].ResolveRuntimeEntity();
                Assert.That(_world.EntityManager.GetComponentData<AscLifecycle>(asc).State,
                    Is.EqualTo(GasAscLifecycleState.Pending));
                Assert.That(_world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0].Current, Is.Zero);
                Assert.That(_world.EntityManager.GetBuffer<GrantedAbilitySlot>(asc).Length, Is.Zero);
                Assert.That(_world.EntityManager.IsComponentEnabled<GasSpawnBatchMarker>(asc), Is.True);
            }
        }

        /// <summary>
        /// 验证第二轮 maintenance 全批发布默认 Attribute、Tag、Grant 与 Ready，可见 tick仍未前进。
        /// </summary>
        internal void AssertReadyBatch()
        {
            var session = GetSession();
            var lifecycle = _world.EntityManager.GetComponentData<GasSessionLifecycle>(session);
            var tick = _world.EntityManager.GetComponentData<SimulationTickState>(session);
            var battles = _world.EntityManager.GetBuffer<BattleInstanceSlot>(session);
            var registry = _world.EntityManager.GetBuffer<AscRegistrySlot>(session);
            var manifest = _world.EntityManager.GetComponentData<GasSpawnBatchManifest>(session);
            var members = _world.EntityManager.GetBuffer<SpawnBatchMemberManifestSlot>(session);

            Assert.That(lifecycle.State, Is.EqualTo(GasSessionLifecycleState.Ready));
            Assert.That(lifecycle.ActiveSpawnBatchId, Is.Zero);
            Assert.That(tick.CurrentTick, Is.Zero);
            Assert.That(battles[0].State, Is.EqualTo(GasBattleInstanceState.Ready));
            Assert.That(battles[0].ReadyMemberCount, Is.EqualTo(2));
            Assert.That(manifest.Pending, Is.Zero);
            Assert.That(members.Length, Is.Zero);
            for (var index = 0; index < registry.Length; index++)
                AssertReadyAsc(registry[index]);
            using var pendingMarkers = _world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasSpawnBatchMarker>());
            Assert.That(pendingMarkers.CalculateEntityCount(), Is.Zero);
        }

        /// <summary>
        /// 在首轮 playback 后破坏一个 Pending Tag index，模拟 batch 内单成员验证失败。
        /// </summary>
        internal void CorruptFirstPendingTag()
        {
            var registry = _world.EntityManager.GetBuffer<AscRegistrySlot>(GetSession());
            var asc = registry[0].ResolveRuntimeEntity();
            var pending = _world.EntityManager.GetBuffer<PendingTagInitialization>(asc);
            var value = pending[0];
            value.LayoutIndex = 999;
            pending[0] = value;
        }

        /// <summary>
        /// 只污染 lifecycle 状态而不登记任何 ECB Destroy，用于验证 teardown 去重证据不可伪造。
        /// </summary>
        internal void CorruptFirstLifecycleToDestroyPending()
        {
            var registry = _world.EntityManager.GetBuffer<AscRegistrySlot>(GetSession());
            var asc = registry[0].ResolveRuntimeEntity();
            var lifecycle = _world.EntityManager.GetComponentData<AscLifecycle>(asc);
            lifecycle.State = GasAscLifecycleState.DestroyPending;
            _world.EntityManager.SetComponentData(asc, lifecycle);
        }

        /// <summary>
        /// 将规则版本改为另一合法值，验证 finalize 不允许用当前值重解释原始 SpawnBatch。
        /// </summary>
        internal void CorruptSessionRuleVersion()
        {
            var session = GetSession();
            var config = _world.EntityManager.GetComponentData<GasSessionConfig>(session);
            config.RuleVersion++;
            _world.EntityManager.SetComponentData(session, config);
        }

        /// <summary>
        /// 同步裁短 registry 与 member buffer，返回仅能由 ASC marker 找回的遗漏成员。
        /// </summary>
        internal Entity TruncateRegistryAndManifestToOneMember()
        {
            var session = GetSession();
            var registry = _world.EntityManager.GetBuffer<AscRegistrySlot>(session);
            var members = _world.EntityManager.GetBuffer<SpawnBatchMemberManifestSlot>(session);
            var omittedAsc = registry[1].ResolveRuntimeEntity();
            registry.RemoveAt(1);
            members.RemoveAt(1);
            return omittedAsc;
        }

        /// <summary>
        /// 验证失败轮未发布任何 Ready，Session 锁存原因且 ASC 仅留下 cleanup shell。
        /// </summary>
        internal void AssertFaultedBatch(
            GasStageBSpawnFaultReason expectedReason = GasStageBSpawnFaultReason.SpawnBatchMismatch)
        {
            var session = GetSession();
            var lifecycle = _world.EntityManager.GetComponentData<GasSessionLifecycle>(session);
            var latch = _world.EntityManager.GetComponentData<SessionFaultLatch>(session);
            var tick = _world.EntityManager.GetComponentData<SimulationTickState>(session);
            var battles = _world.EntityManager.GetBuffer<BattleInstanceSlot>(session);
            var registry = _world.EntityManager.GetBuffer<AscRegistrySlot>(session);

            Assert.That(lifecycle.State, Is.EqualTo(GasSessionLifecycleState.Faulted));
            Assert.That(latch.ReasonCode, Is.EqualTo((int)expectedReason));
            Assert.That(latch.IngressClosed, Is.EqualTo(1));
            Assert.That(tick.CurrentTick, Is.Zero);
            Assert.That(battles[0].ReadyMemberCount, Is.Zero);
            Assert.That(battles[0].State, Is.EqualTo(GasBattleInstanceState.Tombstone));
            for (var index = 0; index < registry.Length; index++)
            {
                Assert.That(registry[index].ReadyTick, Is.Zero);
                Assert.That(registry[index].State, Is.EqualTo(GasAscRegistryState.Tombstone));
                AssertCleanupShell(registry[index].ResolveRuntimeEntity());
            }
        }

        /// <summary>
        /// 验证 manifest mismatch 锁存且 registry 内外的两个原始成员都已转 cleanup shell。
        /// </summary>
        internal void AssertManifestFaultedBatch(Entity omittedAsc)
        {
            var session = GetSession();
            var lifecycle = _world.EntityManager.GetComponentData<GasSessionLifecycle>(session);
            var latch = _world.EntityManager.GetComponentData<SessionFaultLatch>(session);
            var registry = _world.EntityManager.GetBuffer<AscRegistrySlot>(session);

            Assert.That(lifecycle.State, Is.EqualTo(GasSessionLifecycleState.Faulted));
            Assert.That(latch.ReasonCode, Is.EqualTo((int)GasStageBSpawnFaultReason.SpawnBatchMismatch));
            Assert.That(registry.Length, Is.EqualTo(1));
            Assert.That(registry[0].State, Is.EqualTo(GasAscRegistryState.Tombstone));
            AssertCleanupShell(registry[0].ResolveRuntimeEntity());
            AssertCleanupShell(omittedAsc);
        }

        /// <summary>
        /// 验证一个已发布 ASC 的固定 authority、派生位图、初始 grant与清空后的 Pending buffers。
        /// </summary>
        private void AssertReadyAsc(AscRegistrySlot registry)
        {
            var asc = registry.ResolveRuntimeEntity();
            var ascLifecycle = _world.EntityManager.GetComponentData<AscLifecycle>(asc);
            var attributes = _world.EntityManager.GetBuffer<AttributeValueSlot>(asc);
            var tags = _world.EntityManager.GetBuffer<TagCountSlot>(asc);
            var grants = _world.EntityManager.GetBuffer<GrantedAbilitySlot>(asc);

            Assert.That(registry.State, Is.EqualTo(GasAscRegistryState.Ready));
            Assert.That(registry.ReadyTick, Is.EqualTo(1));
            Assert.That(ascLifecycle.State, Is.EqualTo(GasAscLifecycleState.Ready));
            Assert.That(ascLifecycle.ReadyTick, Is.EqualTo(1));
            Assert.That(attributes[0].Current, Is.EqualTo(10f));
            Assert.That(attributes[1].Current, Is.EqualTo(20f));
            Assert.That(attributes[0].Revision, Is.EqualTo(1));
            Assert.That(tags[0].ExactCount, Is.Zero);
            Assert.That(tags[0].InclusiveCount, Is.EqualTo(1));
            Assert.That(tags[1].ExactCount, Is.EqualTo(1));
            Assert.That(tags[1].InclusiveCount, Is.EqualTo(1));
            Assert.That(grants.Length, Is.EqualTo(1));
            Assert.That(grants[0].Handle.IsValid, Is.True);
            Assert.That(_world.EntityManager.IsComponentEnabled<GasSpawnBatchMarker>(asc), Is.False);
            Assert.That(_world.EntityManager.GetBuffer<PendingTagInitialization>(asc).Length, Is.Zero);
        }

        /// <summary>
        /// 验证 teardown 后 Entity 只保留 cleanup owner数据，不再具备 gameplay ASC 身份。
        /// </summary>
        private void AssertCleanupShell(Entity asc)
        {
            Assert.That(_world.EntityManager.Exists(asc), Is.True);
            Assert.That(_world.EntityManager.HasComponent<BoundaryDrainState>(asc), Is.True);
            Assert.That(_world.EntityManager.HasBuffer<BoundaryFactBuffer>(asc), Is.True);
            Assert.That(_world.EntityManager.HasComponent<GasAscIdentity>(asc), Is.False);
            Assert.That(_world.EntityManager.HasComponent<AscLifecycle>(asc), Is.False);
        }

        /// <summary>
        /// 返回标准 EndFixed 系统安装的 singleton 以创建正式 playback ECB。
        /// </summary>
        private EndFixedStepSimulationEntityCommandBufferSystem.Singleton GetEndFixedSingleton()
        {
            using var query = _world.EntityManager.CreateEntityQuery(new EntityQueryDesc
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
        /// 返回当前唯一 Session Entity，基数错误直接让测试失败。
        /// </summary>
        private Entity GetSession()
        {
            using var query = _world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasSessionIdentity>(),
                ComponentType.ReadOnly<GasActiveSessionAuthority>());
            Assert.That(query.CalculateEntityCount(), Is.EqualTo(1));
            return query.GetSingletonEntity();
        }

        /// <summary>
        /// 验证一个 active Session 已锁存唯一 cardinality fault，并清零所有 Registry Ready 可见性。
        /// </summary>
        private void AssertCardinalityFault(Entity session)
        {
            var lifecycle = _world.EntityManager.GetComponentData<GasSessionLifecycle>(session);
            var latch = _world.EntityManager.GetComponentData<SessionFaultLatch>(session);
            var registry = _world.EntityManager.GetBuffer<AscRegistrySlot>(session);
            Assert.That(lifecycle.State, Is.EqualTo(GasSessionLifecycleState.Faulted));
            Assert.That(latch.ReasonCode, Is.EqualTo((int)GasStageBSpawnFaultReason.SessionCardinality));
            Assert.That(latch.IngressClosed, Is.EqualTo(1));
            for (var index = 0; index < registry.Length; index++)
                Assert.That(registry[index].State, Is.EqualTo(GasAscRegistryState.Tombstone));
        }

        /// <summary>
        /// 按原顺序复制测试用 DynamicBuffer，保持共享 Entity 引用不变。
        /// </summary>
        private static void CopyBuffer<T>(DynamicBuffer<T> source, DynamicBuffer<T> destination)
            where T : unmanaged, IBufferElementData
        {
            for (var index = 0; index < source.Length; index++)
                destination.Add(source[index]);
        }

        /// <summary>
        /// 查找指定 managed System 在已排序 FixedStep update list 中的位置。
        /// </summary>
        private int FindManagedSystemIndex(ComponentSystemBase expected)
        {
            var systems = _fixedStep.ManagedSystems;
            for (var index = 0; index < systems.Count; index++)
                if (ReferenceEquals(systems[index], expected))
                    return index;
            return -1;
        }

        /// <summary>
        /// 构造 Session、Catalog expectation 与所有具名容量上限。
        /// </summary>
        private GasStageBSessionBootstrapRequest CreateSessionRequest()
        {
            return new GasStageBSessionBootstrapRequest
            {
                SimulationEpoch = Epoch,
                SpawnBatchId = SpawnBatchId,
                Config = new GasSessionConfig { TickRate = 20, RuleVersion = 1, BoundaryPolicyVersion = 1 },
                ScaleProfile = CreateScaleProfile(),
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
        /// 构造覆盖一个 Battle 的连续两成员请求。
        /// </summary>
        private static NativeArray<GasStageBBattleBootstrapRequest> CreateBattleRequests(
            int membershipOrdinalRoot = 10)
        {
            var battles = new NativeArray<GasStageBBattleBootstrapRequest>(1, Allocator.Temp);
            battles[0] = new GasStageBBattleBootstrapRequest
            {
                BattleInstance = new BattleInstanceHandle(Epoch, 71, 1),
                MemberStart = 0,
                MemberCount = 2,
                MembershipOrdinalRoot = membershipOrdinalRoot,
            };
            return battles;
        }

        /// <summary>
        /// 构造两个稳定 ASC、连续 registry/ranges与不同 RNG 的请求。
        /// </summary>
        private static NativeArray<GasStageBAscBootstrapRequest> CreateAscRequests(
            int firstMembershipOrdinal = 10,
            int secondMembershipOrdinal = 11)
        {
            var battle = new BattleInstanceHandle(Epoch, 71, 1);
            var ascs = new NativeArray<GasStageBAscBootstrapRequest>(2, Allocator.Temp);
            ascs[0] = CreateAscRequest(
                new OwnerAscHandle(101, 1), battle, 0, firstMembershipOrdinal, 0, 0, 0);
            ascs[1] = CreateAscRequest(
                new OwnerAscHandle(102, 1), battle, 1, secondMembershipOrdinal, 2, 1, 1);
            return ascs;
        }

        /// <summary>
        /// 构造一个 ASC 的稳定成员关系与三类初始化 range。
        /// </summary>
        private static GasStageBAscBootstrapRequest CreateAscRequest(
            OwnerAscHandle owner,
            BattleInstanceHandle battle,
            int registryOrdinal,
            int membershipOrdinal,
            int attributeStart,
            int tagStart,
            int abilityStart)
        {
            return new GasStageBAscBootstrapRequest
            {
                OwnerAsc = owner,
                BattleInstance = battle,
                RegistryOrdinal = registryOrdinal,
                ScenarioUnitId = (ulong)(900 + registryOrdinal),
                SideId = registryOrdinal,
                TeamId = registryOrdinal,
                MembershipOrdinal = membershipOrdinal,
                OwnerActorStableId = (ulong)(1000 + registryOrdinal),
                AvatarActorStableId = (ulong)(2000 + registryOrdinal),
                ActorBindingGeneration = 1,
                RandomState0 = (ulong)(3000 + registryOrdinal),
                RandomState1 = (ulong)(4000 + registryOrdinal),
                AttributeInitializationStart = attributeStart,
                AttributeInitializationCount = 2,
                TagInitializationStart = tagStart,
                TagInitializationCount = 1,
                GrantedAbilityInitializationStart = abilityStart,
                GrantedAbilityInitializationCount = 1,
            };
        }

        /// <summary>
        /// 构造全局唯一且每 ASC 内递增的 Attribute 初始化规范顺序。
        /// </summary>
        private static NativeArray<PendingAttributeInitialization> CreateAttributeInitializations()
        {
            var values = new NativeArray<PendingAttributeInitialization>(4, Allocator.Temp);
            values[0] = new PendingAttributeInitialization { LayoutIndex = 0, ConfigOrdinal = 0 };
            values[1] = new PendingAttributeInitialization { LayoutIndex = 1, ConfigOrdinal = 1 };
            values[2] = new PendingAttributeInitialization { LayoutIndex = 0, ConfigOrdinal = 4 };
            values[3] = new PendingAttributeInitialization { LayoutIndex = 1, ConfigOrdinal = 5 };
            return values;
        }

        /// <summary>
        /// 构造两个 child Tag exact grants并保持全局 ConfigOrdinal 唯一。
        /// </summary>
        private static NativeArray<PendingTagInitialization> CreateTagInitializations()
        {
            var values = new NativeArray<PendingTagInitialization>(2, Allocator.Temp);
            values[0] = new PendingTagInitialization { LayoutIndex = 1, ConfigOrdinal = 2 };
            values[1] = new PendingTagInitialization { LayoutIndex = 1, ConfigOrdinal = 6 };
            return values;
        }

        /// <summary>
        /// 构造两个相同 Ability definition 的独立初始 grants。
        /// </summary>
        private static NativeArray<PendingGrantedAbilityInitialization> CreateAbilityInitializations()
        {
            var values = new NativeArray<PendingGrantedAbilityInitialization>(2, Allocator.Temp);
            values[0] = new PendingGrantedAbilityInitialization { LayoutIndex = 0, ConfigOrdinal = 3 };
            values[1] = new PendingGrantedAbilityInitialization { LayoutIndex = 0, ConfigOrdinal = 7 };
            return values;
        }

        /// <summary>
        /// 构造满足 Session、Pending、slab 与 cleanup outbox 的版本化测试容量档位。
        /// </summary>
        private static GasScaleProfile CreateScaleProfile()
        {
            return new GasScaleProfile
            {
                ProfileId = 1,
                ProfileVersion = 1,
                ProfileHash = 1001,
                MaxFixedTicksPerBatch = 1,
                MaximumDeltaTimeTicks = 1,
                MaxSpawnBatchSize = 2,
                MaxBattleInstanceCount = 1,
                MaxAscRegistryCount = 2,
                MaxBoundaryCommandCount = 2,
                MaxBoundaryCommandPayloadCount = 8,
                MaxSessionBoundaryFactCount = 4,
                MaxAscBoundaryFactCount = 4,
                MaxPendingAttributeInitializationCount = 2,
                MaxPendingTagInitializationCount = 1,
                MaxPendingGrantedAbilityInitializationCount = 1,
                MaxGrantedAbilityCount = 1,
                MaxPayloadRangeRecordCount = 2,
                MaxPayloadValueCount = 8,
            };
        }

        /// <summary>
        /// 构造包含两属性、父子 Tag 与一个 Ability 的最小合法 immutable Catalog。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateCatalog()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                root.SchemaVersion = GasDefinitionCatalogSchema.Version;
                root.SchemaHash = SchemaHash;
                root.ContentHash = ContentHash;
                PopulateAttributes(ref builder, ref root);
                PopulateTags(ref builder, ref root);
                PopulateAbility(ref builder, ref root);
                return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 写入 dense AttributeLayout 及默认值。
        /// </summary>
        private static void PopulateAttributes(ref BlobBuilder builder, ref GasDefinitionCatalogBlob root)
        {
            root.AttributeLayout.LayoutHash = AttributeHash;
            var entries = builder.Allocate(ref root.AttributeLayout.Entries, 2);
            entries[0] = CreateAttribute(201, 0, 10f);
            entries[1] = CreateAttribute(202, 1, 20f);
        }

        /// <summary>
        /// 创建一个具备显式 clamp range 的 AttributeLayout entry。
        /// </summary>
        private static GasAttributeLayoutEntryBlob CreateAttribute(int id, int index, float defaultValue)
        {
            return new GasAttributeLayoutEntryBlob
            {
                AttributeId = id,
                LayoutIndex = index,
                DefaultValue = defaultValue,
                MinimumValue = 0f,
                MaximumValue = 100f,
                ClampMinimum = 1,
                ClampMaximum = 1,
            };
        }

        /// <summary>
        /// 写入父子 TagCatalog，child 的 ancestor range只引用 parent。
        /// </summary>
        private static void PopulateTags(ref BlobBuilder builder, ref GasDefinitionCatalogBlob root)
        {
            root.TagCatalog.CatalogHash = TagHash;
            var entries = builder.Allocate(ref root.TagCatalog.Entries, 2);
            var ancestors = builder.Allocate(ref root.TagCatalog.AncestorIndices, 3);
            ancestors[0] = 0;
            ancestors[1] = 0;
            ancestors[2] = 1;
            entries[0] = new GasTagCatalogEntryBlob
            {
                TagId = 301,
                TagIndex = 0,
                AncestorIndexRange = new GasCatalogRange { Start = 0, Count = 1 },
            };
            entries[1] = new GasTagCatalogEntryBlob
            {
                TagId = 302,
                TagIndex = 1,
                AncestorIndexRange = new GasCatalogRange { Start = 1, Count = 2 },
            };
        }

        /// <summary>
        /// 写入排序 Ability index 与一个显式 Self target definition。
        /// </summary>
        private static void PopulateAbility(ref BlobBuilder builder, ref GasDefinitionCatalogBlob root)
        {
            var index = builder.Allocate(ref root.AbilityIndex, 1);
            var definitions = builder.Allocate(ref root.Abilities, 1);
            index[0] = new GasDefinitionIndexEntry { DefinitionId = 401, DefinitionIndex = 0 };
            definitions[0] = new GasAbilityDefinitionBlob
            {
                DefinitionId = 401,
                Level = 1,
                TargetPolicy = new GasTargetPolicyBlob
                {
                    LogicalTarget = GasLogicalTargetPolicy.Self,
                    Avatar = GasAvatarTargetPolicy.FollowAsc,
                    Spatial = GasSpatialTargetPolicy.None,
                    Life = GasTargetLifePolicy.AnyLifeState,
                },
            };
        }
    }
}
