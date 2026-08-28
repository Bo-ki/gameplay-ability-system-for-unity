using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Physics.Systems;
using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// 提供 Runtime v1 Session 生命周期与故障锁存的纯值观察结果；不暴露 ECS Entity 或可变 buffer。
    /// </summary>
    public readonly struct GasRuntimeSessionObservation
    {
        public readonly bool Exists;
        public readonly GasSessionLifecycleState SessionState;
        public readonly int FaultReasonCode;
        public readonly ulong FaultId;
        public readonly int BattleCount;
        public readonly int ReadyBattleCount;
        public readonly GasBattleInstanceState FirstBattleState;
        public readonly bool FirstBattleIngressOpen;
        public readonly int AscCount;
        public readonly int ReadyAscCount;
        public readonly ulong CurrentTick;
        public readonly GasTickDiagnostics TickDiagnostics;
        public readonly int InboxCount;
        public readonly int PendingInboxCount;
        public readonly int ConsumedInboxCount;
        public readonly OwnerAscHandle FirstAsc;
        public readonly ulong FirstAscScenarioUnitId;
        public readonly GasAscLifecycleState FirstAscLifecycle;
        public readonly float FirstAscHealth;
        public readonly float FirstAscEnergy;

        /// <summary>
        /// 创建一个不可变 Session 观察结果。
        /// </summary>
        public GasRuntimeSessionObservation(
            bool exists,
            GasSessionLifecycleState sessionState,
            int faultReasonCode,
            ulong faultId,
            int battleCount,
            int readyBattleCount,
            GasBattleInstanceState firstBattleState,
            bool firstBattleIngressOpen,
            int ascCount,
            int readyAscCount,
            ulong currentTick,
            in GasTickDiagnostics tickDiagnostics,
            int inboxCount,
            int pendingInboxCount,
            int consumedInboxCount,
            in OwnerAscHandle firstAsc,
            ulong firstAscScenarioUnitId,
            GasAscLifecycleState firstAscLifecycle,
            float firstAscHealth,
            float firstAscEnergy)
        {
            Exists = exists;
            SessionState = sessionState;
            FaultReasonCode = faultReasonCode;
            FaultId = faultId;
            BattleCount = battleCount;
            ReadyBattleCount = readyBattleCount;
            FirstBattleState = firstBattleState;
            FirstBattleIngressOpen = firstBattleIngressOpen;
            AscCount = ascCount;
            ReadyAscCount = readyAscCount;
            CurrentTick = currentTick;
            TickDiagnostics = tickDiagnostics;
            InboxCount = inboxCount;
            PendingInboxCount = pendingInboxCount;
            ConsumedInboxCount = consumedInboxCount;
            FirstAsc = firstAsc;
            FirstAscScenarioUnitId = firstAscScenarioUnitId;
            FirstAscLifecycle = firstAscLifecycle;
            FirstAscHealth = firstAscHealth;
            FirstAscEnergy = firstAscEnergy;
        }
    }

    /// <summary>
    /// 唯一拥有一个 World 的 Runtime v1 系统拓扑、CommandPort 与完整 FixedStep 批次边界。
    /// </summary>
    public sealed class GasRuntimeWorldOwner : IDisposable
    {
        private const string RegistrationEntityName = "GAS_RuntimeV1_WorldOwner";

        private readonly World _world;
        private readonly SimulationSystemGroup _simulation;
        private readonly FixedStepSimulationSystemGroup _fixedStep;
        private readonly GasCommandIngressSystem _ingressSystem;
        private readonly SessionIngressGate _ingressGate;
        private readonly GasBoundaryDrainCoordinator _boundaryDrain;
        private Entity _registrationEntity;
        private GasStageBBootstrapRecordGate _bootstrapRecordGate;
        private bool _attachedToPlayerLoop;
        private bool _disposed;

        public GasCommandPort Port { get; }

        /// <summary>
        /// 记录一次完整 Stage-B SpawnBatch；调用方只能提交 Pending，Ready 发布仍由下一次 Kernel maintenance 完成。
        /// </summary>
        public GasStageBSpawnFaultReason RecordSpawnBatch(
            in GasStageBSessionBootstrapRequest sessionRequest,
            Unity.Collections.NativeArray<GasStageBBattleBootstrapRequest> battleRequests,
            Unity.Collections.NativeArray<GasStageBAscBootstrapRequest> ascRequests,
            Unity.Collections.NativeArray<PendingAttributeInitialization> attributeInitializations,
            Unity.Collections.NativeArray<PendingTagInitialization> tagInitializations,
            Unity.Collections.NativeArray<PendingGrantedAbilityInitialization> grantedAbilityInitializations)
        {
            EnsureUsable();
            using var query = _world.EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>(),
                },
                Options = EntityQueryOptions.IncludeSystems,
            });
            if (query.CalculateEntityCount() != 1)
                return GasStageBSpawnFaultReason.SessionCardinality;

            var endFixed = query.GetSingleton<
                EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            return GasStageBBootstrapRecorder.Record(
                _world.EntityManager,
                endFixed,
                _world.Unmanaged,
                ref _bootstrapRecordGate,
                in sessionRequest,
                battleRequests,
                ascRequests,
                attributeInitializations,
                tagInitializations,
                grantedAbilityInitializations,
                out _);
        }

        /// <summary>
        /// 暴露唯一 managed Drain 的 immutable ring；注入外部 stager 时该属性为 null。
        /// </summary>
        public GasBoundaryDrainRing BoundaryDrainRing { get; }

        /// <summary>
        /// 返回最近一次完成 fence 的 Boundary Drain 失败原因，成功时为 None。
        /// </summary>
        public GasBoundaryDrainFailure LastBoundaryDrainFailure { get; private set; }

        /// <summary>
        /// 保存已经显式安装并排序的 world-local owner 状态。
        /// </summary>
        private GasRuntimeWorldOwner(
            World world,
            in GasRuntimeSystemTopology topology,
            SessionIngressGate ingressGate,
            GasCommandPort port,
            GasBoundaryDrainCoordinator boundaryDrain,
            GasBoundaryDrainRing boundaryDrainRing)
        {
            _world = world;
            _simulation = topology.Simulation;
            _fixedStep = topology.FixedStep;
            _ingressSystem = topology.Ingress;
            _ingressGate = ingressGate;
            Port = port;
            _boundaryDrain = boundaryDrain;
            BoundaryDrainRing = boundaryDrainRing;
        }

        /// <summary>
        /// 在指定 World 幂等安装唯一 Runtime v1 父链与 Boundary capability。
        /// </summary>
        public static GasRuntimeWorldOwner Install(
            World world,
            bool attachToPlayerLoop = false)
        {
            return Install(world, null, attachToPlayerLoop);
        }

        /// <summary>
        /// 安装唯一 Runtime v1 owner，并允许 headless/managed 宿主注入唯一 staging sink。
        /// </summary>
        public static GasRuntimeWorldOwner Install(
            World world,
            IGasBoundaryDrainStager boundaryStager,
            bool attachToPlayerLoop = false)
        {
            ValidateWorld(world);
            if (TryGetInstalled(world, out var installed))
            {
                if (attachToPlayerLoop)
                    installed.AttachToPlayerLoop();
                return installed;
            }

            var ingressGate = new SessionIngressGate();
            var port = new GasCommandPort(ingressGate);
            var topology = InstallSystemTopology(world, ingressGate);
            var ring = boundaryStager == null ? new GasBoundaryDrainRing() : null;
            var stager = boundaryStager ?? (IGasBoundaryDrainStager)ring;
            var boundaryDrain = new GasBoundaryDrainCoordinator(stager);
            var owner = new GasRuntimeWorldOwner(
                world,
                in topology,
                ingressGate,
                port,
                boundaryDrain,
                ring);
            owner.RegisterInWorld();
            if (attachToPlayerLoop)
                owner.AttachToPlayerLoop();
            return owner;
        }

        /// <summary>
        /// 推进完整 FixedStep 父组，并只在父组完成后执行 Boundary 批次握手。
        /// </summary>
        public bool TickBatch()
        {
            EnsureUsable();
            _simulation.Update();
            return true;
        }

        /// <summary>
        /// 读取当前唯一 Session 的生命周期、Battle/ASC Ready 计数与 fail-closed 原因。
        /// </summary>
        public bool TryReadSessionObservation(out GasRuntimeSessionObservation observation)
        {
            EnsureUsable();
            using var query = _world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasSessionIdentity>(),
                ComponentType.ReadOnly<GasSessionLifecycle>(),
                ComponentType.ReadOnly<SessionFaultLatch>(),
                ComponentType.ReadOnly<SimulationTickState>(),
                ComponentType.ReadOnly<BattleInstanceSlot>(),
                ComponentType.ReadOnly<AscRegistrySlot>());
            if (query.CalculateEntityCount() != 1)
            {
                observation = new GasRuntimeSessionObservation(
                    false,
                    default,
                    0,
                    0,
                    0,
                    0,
                    default,
                    false,
                    0,
                    0,
                    0,
                    default,
                    0,
                    0,
                    0,
                    default,
                    0,
                    default,
                    0f,
                    0f);
                return false;
            }

            var session = query.GetSingletonEntity();
            var lifecycle = _world.EntityManager.GetComponentData<GasSessionLifecycle>(session);
            var fault = _world.EntityManager.GetComponentData<SessionFaultLatch>(session);
            var tick = _world.EntityManager.GetComponentData<SimulationTickState>(session);
            var diagnostics = _world.EntityManager.GetComponentData<GasTickDiagnostics>(session);
            var inbox = _world.EntityManager.GetBuffer<BoundaryCommandInbox>(session, true);
            var pendingInboxCount = 0;
            var consumedInboxCount = 0;
            for (var index = 0; index < inbox.Length; index++)
            {
                var state = inbox[index].State;
                if (state == GasBoundaryCommandState.Pending ||
                    state == GasBoundaryCommandState.Sealed)
                    pendingInboxCount++;
                else if (state == GasBoundaryCommandState.Consumed)
                    consumedInboxCount++;
            }
            var battles = _world.EntityManager.GetBuffer<BattleInstanceSlot>(session, true);
            var ascs = _world.EntityManager.GetBuffer<AscRegistrySlot>(session, true);
            var readyBattles = 0;
            var firstBattleState = default(GasBattleInstanceState);
            var firstBattleIngressOpen = false;
            for (var index = 0; index < battles.Length; index++)
            {
                var battle = battles[index];
                if (index == 0)
                {
                    firstBattleState = battle.State;
                    firstBattleIngressOpen = battle.IngressClosed == 0;
                }
                if (battle.State == GasBattleInstanceState.Ready ||
                    battle.State == GasBattleInstanceState.Running)
                    readyBattles++;
            }

            var readyAscs = 0;
            for (var index = 0; index < ascs.Length; index++)
                if (ascs[index].State == GasAscRegistryState.Ready)
                    readyAscs++;

            var firstAsc = default(OwnerAscHandle);
            var firstAscScenarioUnitId = 0UL;
            var firstAscLifecycle = default(GasAscLifecycleState);
            var firstAscHealth = 0f;
            var firstAscEnergy = 0f;
            if (ascs.Length > 0)
            {
                var firstSlot = ascs[0];
                firstAsc = firstSlot.OwnerAsc;
                var firstEntity = firstSlot.ResolveRuntimeEntity();
                if (_world.EntityManager.Exists(firstEntity) &&
                    _world.EntityManager.HasComponent<AscLifecycle>(firstEntity))
                {
                    if (_world.EntityManager.HasComponent<AscBattleMembership>(firstEntity))
                        firstAscScenarioUnitId = _world.EntityManager.GetComponentData<AscBattleMembership>(firstEntity).ScenarioUnitId;
                    firstAscLifecycle = _world.EntityManager.GetComponentData<AscLifecycle>(firstEntity).State;
                    if (_world.EntityManager.HasBuffer<AttributeValueSlot>(firstEntity))
                    {
                        var values = _world.EntityManager.GetBuffer<AttributeValueSlot>(firstEntity, true);
                        if (values.Length > 0)
                            firstAscHealth = values[0].Current;
                        if (values.Length > 1)
                            firstAscEnergy = values[1].Current;
                    }
                }
            }

            observation = new GasRuntimeSessionObservation(
                true,
                lifecycle.State,
                fault.ReasonCode,
                fault.FaultId,
                battles.Length,
                readyBattles,
                firstBattleState,
                firstBattleIngressOpen,
                ascs.Length,
                readyAscs,
                tick.CurrentTick,
                in diagnostics,
                inbox.Length,
                pendingInboxCount,
                consumedInboxCount,
                in firstAsc,
                firstAscScenarioUnitId,
                firstAscLifecycle,
                firstAscHealth,
                firstAscEnergy);
            return true;
        }

        /// <summary>
        /// 在 outer batch 开始前把版本化 TickRate 与 MaximumDeltaTime 投影到真实 FixedRateCatchUpManager。
        /// </summary>
        internal void PrepareBatch()
        {
            if (_disposed || !_world.IsCreated)
                return;

            if (!TryReadBatchTiming(out var timestep, out var maximumDeltaTime))
                return;

            _fixedStep.Timestep = timestep;
            _world.MaximumDeltaTime = maximumDeltaTime;
        }

        /// <summary>
        /// 从 PlayerLoop 移除当前 World 并释放 owner registration；World 生命周期仍由调用方拥有。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            // 先关闭 ingress，阻止 teardown 期间再产生无法纳入 final drain 的新工作。
            _ingressGate.CloseForOwnerDisposal();
            if (_attachedToPlayerLoop && _world.IsCreated)
            {
                // FinalDrain 失败时也停止自动 gameplay 更新，保留 registration 供显式重试。
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(_world);
                _attachedToPlayerLoop = false;
            }
            if (_world.IsCreated)
            {
                _world.EntityManager.CompleteAllTrackedJobs();
                if (!_boundaryDrain.TryFinalDrain(
                        _world.EntityManager,
                        out var drainResult))
                {
                    LastBoundaryDrainFailure = drainResult.Failure;
                    throw CreateBoundaryDrainException("final drain", drainResult.Failure);
                }
                LastBoundaryDrainFailure = drainResult.Failure;
            }

            if (_world.IsCreated &&
                _registrationEntity != Entity.Null &&
                _world.EntityManager.Exists(_registrationEntity))
                _world.EntityManager.DestroyEntity(_registrationEntity);

            _attachedToPlayerLoop = false;
            _registrationEntity = Entity.Null;
            _disposed = true;
        }

        /// <summary>
        /// 在既有 FixedStep completion fence 后刷新 gate、确认消费并完成 fault close。
        /// </summary>
        internal void CompleteBatchFence()
        {
            if (_disposed || !_world.IsCreated)
                return;

            _world.EntityManager.CompleteAllTrackedJobs();
            if (!_boundaryDrain.TryDrain(
                    _world.EntityManager,
                    out var drainResult))
            {
                LastBoundaryDrainFailure = drainResult.Failure;
                throw CreateBoundaryDrainException("batch drain", drainResult.Failure);
            }
            LastBoundaryDrainFailure = drainResult.Failure;
            _ingressSystem.AcknowledgeConsumedAfterBatch(_world.EntityManager);
            if (!CloseDetectedFault())
                RefreshAuthoritySnapshot();
        }

        /// <summary>
        /// 构造带有确定性失败原因的 Boundary Drain 异常，源 outbox 保持可重试状态。
        /// </summary>
        private static InvalidOperationException CreateBoundaryDrainException(
            string phase,
            GasBoundaryDrainFailure failure)
        {
            return new InvalidOperationException(
                "Runtime v1 Boundary " + phase + " failed: " + failure);
        }

        /// <summary>
        /// 创建标准根组、FixedStep 子组与唯一 GAS Ingress/Kernel，并按属性完成排序。
        /// </summary>
        private static GasRuntimeSystemTopology InstallSystemTopology(
            World world,
            SessionIngressGate ingressGate)
        {
            world.GetOrCreateSystemManaged<InitializationSystemGroup>();
            var simulation = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            var beginSimulation = world.GetOrCreateSystemManaged<
                BeginSimulationEntityCommandBufferSystem>();
            world.GetOrCreateSystemManaged<PresentationSystemGroup>();
            var fixedStep = world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
            if (fixedStep.Timestep > 0f)
                fixedStep.Timestep = Time.fixedDeltaTime;

            var beginFixed = world.GetOrCreateSystemManaged<
                BeginFixedStepSimulationEntityCommandBufferSystem>();
            var physics = world.GetOrCreateSystemManaged<PhysicsSystemGroup>();
            var gas = world.GetOrCreateSystemManaged<GasFixedTickSystemGroup>();
            var endFixed = world.GetOrCreateSystemManaged<
                EndFixedStepSimulationEntityCommandBufferSystem>();
            var ingress = world.GetOrCreateSystemManaged<GasCommandIngressSystem>();
            var kernel = world.GetOrCreateSystem<GasTickKernelSystem>();
            var batchStart = world.GetOrCreateSystemManaged<GasRuntimeBatchStartSystem>();
            var batchFence = world.GetOrCreateSystemManaged<GasRuntimeBatchFenceSystem>();
            ingress.Bind(ingressGate);

            gas.AddSystemToUpdateList(ingress);
            gas.AddSystemToUpdateList(kernel);
            fixedStep.AddSystemToUpdateList(beginFixed);
            fixedStep.AddSystemToUpdateList(physics);
            fixedStep.AddSystemToUpdateList(gas);
            fixedStep.AddSystemToUpdateList(endFixed);
            // FixedStep 的 Unity 内置 UpdateAfter(BeginSimulation) 约束必须在同一 Simulation 列表中解析。
            simulation.AddSystemToUpdateList(beginSimulation);
            simulation.AddSystemToUpdateList(batchStart);
            simulation.AddSystemToUpdateList(fixedStep);
            simulation.AddSystemToUpdateList(batchFence);
            gas.SortSystems();
            fixedStep.SortSystems();
            simulation.SortSystems();
            return new GasRuntimeSystemTopology(simulation, fixedStep, ingress);
        }

        /// <summary>
        /// 从唯一 active Session 读取冻结批次时序，并拒绝把不合法 profile 投影到 World 隐藏默认值。
        /// </summary>
        private bool TryReadBatchTiming(out float timestep, out float maximumDeltaTime)
        {
            using var query = _world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasActiveSessionAuthority>(),
                ComponentType.ReadOnly<GasSessionConfig>(),
                ComponentType.ReadOnly<GasScaleProfile>());
            var count = query.CalculateEntityCount();
            if (count == 0)
            {
                timestep = 0;
                maximumDeltaTime = 0;
                return false;
            }
            if (count != 1)
                throw new InvalidOperationException("无法从多个 active Session 投影唯一 fixed-rate 批次时序。");

            var session = query.GetSingletonEntity();
            var config = _world.EntityManager.GetComponentData<GasSessionConfig>(session);
            var profile = _world.EntityManager.GetComponentData<GasScaleProfile>(session);
            if (config.TickRate <= 0 || profile.MaximumDeltaTimeTicks <= 0 ||
                profile.MaximumDeltaTimeTicks > profile.MaxFixedTicksPerBatch)
            {
                timestep = 0;
                maximumDeltaTime = 0;
                return false;
            }

            timestep = 1f / config.TickRate;
            maximumDeltaTime = timestep * profile.MaximumDeltaTimeTicks;
            return true;
        }

        /// <summary>
        /// 检查目标 World 存活且可承载系统。
        /// </summary>
        private static void ValidateWorld(World world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            if (!world.IsCreated)
                throw new ObjectDisposedException(nameof(world));
        }

        /// <summary>
        /// 从 World 内 managed registration 查找既有 owner，禁止使用静态 World 字典。
        /// </summary>
        private static bool TryGetInstalled(
            World world,
            out GasRuntimeWorldOwner owner)
        {
            using var query = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasRuntimeWorldOwnerRegistration>());
            var count = query.CalculateEntityCount();
            if (count == 0)
            {
                owner = null;
                return false;
            }

            if (count != 1)
                throw new InvalidOperationException("同一 World 检测到多个 Runtime v1 owner registration。");

            var registration = world.EntityManager.GetComponentObject<
                GasRuntimeWorldOwnerRegistration>(query.GetSingletonEntity());
            owner = registration.Owner;
            if (owner == null || owner._disposed)
                throw new InvalidOperationException("Runtime v1 owner registration 已损坏。");
            return true;
        }

        /// <summary>
        /// 把 owner 与 gate 注册为当前 World 的唯一 managed 实例，供批次 fence 定位。
        /// </summary>
        private void RegisterInWorld()
        {
            _registrationEntity = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentObject(
                _registrationEntity,
                new GasRuntimeWorldOwnerRegistration(this, _ingressGate));
            _world.EntityManager.SetName(_registrationEntity, RegistrationEntityName);
        }

        /// <summary>
        /// 幂等把当前 World 根组挂入 Unity PlayerLoop。
        /// </summary>
        private void AttachToPlayerLoop()
        {
            EnsureUsable();
            if (_attachedToPlayerLoop)
                return;

            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(_world);
            _attachedToPlayerLoop = true;
        }

        /// <summary>
        /// 从唯一 active Session registry 构造纯值 authority snapshot 并原子替换 gate 视图。
        /// </summary>
        private void RefreshAuthoritySnapshot()
        {
            if (!TryBuildAuthoritySnapshot(out var snapshot))
                return;

            if (!_ingressGate.ReplaceAuthoritySnapshot(in snapshot))
                throw new InvalidOperationException("Ingress gate 拒绝跨 Epoch 或不一致的 authority snapshot。");
        }

        /// <summary>
        /// 查询唯一 active Session 并复制 Battle/ASC stable authority，不向 Boundary 暴露 Entity。
        /// </summary>
        private bool TryBuildAuthoritySnapshot(out GasIngressAuthoritySnapshot snapshot)
        {
            using var query = _world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasActiveSessionAuthority>(),
                ComponentType.ReadOnly<GasSessionIdentity>(),
                ComponentType.ReadOnly<GasScaleProfile>(),
                ComponentType.ReadOnly<BattleInstanceSlot>(),
                ComponentType.ReadOnly<AscRegistrySlot>());
            var count = query.CalculateEntityCount();
            if (count == 0)
            {
                snapshot = default;
                return false;
            }

            if (count != 1)
                throw new InvalidOperationException("无法从多个 active Session 构造唯一 ingress authority snapshot。");

            var session = query.GetSingletonEntity();
            var identity = _world.EntityManager.GetComponentData<GasSessionIdentity>(session);
            var profile = _world.EntityManager.GetComponentData<GasScaleProfile>(session);
            var battles = CopyBattleAuthorities(session);
            var ascs = CopyAscAuthorities(session);
            snapshot = new GasIngressAuthoritySnapshot(
                identity.SimulationEpoch,
                profile.MaxBoundaryCommandCount,
                profile.MaxBoundaryCommandPayloadCount,
                battles,
                ascs);
            return true;
        }

        /// <summary>
        /// 按 Battle slab 索引复制全部 live authority 与 ingress 状态。
        /// </summary>
        private GasIngressBattleAuthority[] CopyBattleAuthorities(Entity session)
        {
            var slots = _world.EntityManager.GetBuffer<BattleInstanceSlot>(session, true);
            var values = new List<GasIngressBattleAuthority>(slots.Length);
            for (var index = 0; index < slots.Length; index++)
            {
                var slot = slots[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live)
                    continue;

                values.Add(new GasIngressBattleAuthority(
                    slot.Handle,
                    slot.State,
                    slot.IngressClosed == 0));
            }

            return values.ToArray();
        }

        /// <summary>
        /// 按 ASC registry 索引复制全部 live stable membership，不复制内部 Entity 映射。
        /// </summary>
        private GasIngressAscAuthority[] CopyAscAuthorities(Entity session)
        {
            var slots = _world.EntityManager.GetBuffer<AscRegistrySlot>(session, true);
            var values = new List<GasIngressAscAuthority>(slots.Length);
            for (var index = 0; index < slots.Length; index++)
            {
                var slot = slots[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live)
                    continue;

                values.Add(new GasIngressAscAuthority(
                    slot.OwnerAsc,
                    slot.BattleInstance,
                    slot.State));
            }

            return values.ToArray();
        }

        /// <summary>
        /// 在 batch fence 发现 Detected fault 时执行与 CommandPort 共锁的 close 并回写固定证据。
        /// </summary>
        private bool CloseDetectedFault()
        {
            using var query = _world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasActiveSessionAuthority>(),
                ComponentType.ReadWrite<SessionFaultLatch>(),
                ComponentType.ReadWrite<BoundaryCommandInbox>(),
                ComponentType.ReadWrite<BattleInstanceSlot>(),
                ComponentType.ReadOnly<AscRegistrySlot>());
            using var sessions = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var faultId = FindDetectedFaultId(sessions);
            if (faultId == 0)
                return false;

            var receipt = _ingressGate.CloseForFault(faultId);
            for (var index = 0; index < sessions.Length; index++)
                FinalizeSessionFault(sessions[index], in receipt);
            _ingressSystem.ForgetFaultTerminatedDeliveries();
            return true;
        }

        /// <summary>
        /// 从全部 active authority 中选择最小非零 detected FaultId，避免依赖 query 物理顺序。
        /// </summary>
        private ulong FindDetectedFaultId(Unity.Collections.NativeArray<Entity> sessions)
        {
            var faultId = 0UL;
            for (var index = 0; index < sessions.Length; index++)
            {
                var latch = _world.EntityManager.GetComponentData<SessionFaultLatch>(sessions[index]);
                if (latch.Detected == 0 || latch.FaultId == 0)
                    continue;
                if (faultId == 0 || latch.FaultId < faultId)
                    faultId = latch.FaultId;
            }
            return faultId;
        }

        /// <summary>
        /// 把唯一 Gate close receipt 回写 fixed latch，并终结所有 ECS outstanding carrier。
        /// </summary>
        private void FinalizeSessionFault(
            Entity session,
            in GasIngressFaultCloseReceipt receipt)
        {
            var latch = _world.EntityManager.GetComponentData<SessionFaultLatch>(session);
            if (latch.Detected == 0)
                return;
            latch.FaultId = receipt.FaultId;
            latch.IngressClosed = 1;
            latch.OutstandingFirstRequestSequence = receipt.FirstRequestSequence;
            latch.OutstandingLastRequestSequence = receipt.LastRequestSequence;
            latch.OutstandingRequestCount = receipt.OutstandingCount;
            latch.OutstandingRequestHash = receipt.OutstandingFnv1A64Hash;
            _world.EntityManager.SetComponentData(session, latch);
            TerminateInbox(session);
            CloseBattleIngress(session);
            CloseAscIngress(session);
        }

        /// <summary>
        /// 将尚未成功消费的 ECS inbox 条目标记为 FaultTerminated并保留其审计内容。
        /// </summary>
        private void TerminateInbox(Entity session)
        {
            var inbox = _world.EntityManager.GetBuffer<BoundaryCommandInbox>(session);
            for (var index = 0; index < inbox.Length; index++)
            {
                var command = inbox[index];
                if (command.State != GasBoundaryCommandState.Pending &&
                    command.State != GasBoundaryCommandState.Sealed)
                    continue;
                command.State = GasBoundaryCommandState.FaultTerminated;
                inbox[index] = command;
            }
        }

        /// <summary>
        /// 关闭全部 live BattleInstance 的显式 ingress 控制位。
        /// </summary>
        private void CloseBattleIngress(Entity session)
        {
            var battles = _world.EntityManager.GetBuffer<BattleInstanceSlot>(session);
            for (var index = 0; index < battles.Length; index++)
            {
                var battle = battles[index];
                if (battle.Header.StorageState != GasSlabSlotState.Live)
                    continue;
                battle.IngressClosed = 1;
                battles[index] = battle;
            }
        }

        /// <summary>
        /// 关闭全部 live ASC lifecycle 的显式 ingress 控制位，不改变其业务生命状态。
        /// </summary>
        private void CloseAscIngress(Entity session)
        {
            var ascs = _world.EntityManager.GetBuffer<AscRegistrySlot>(session, true);
            for (var index = 0; index < ascs.Length; index++)
            {
                var slot = ascs[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live)
                    continue;
                var asc = slot.ResolveRuntimeEntity();
                if (!_world.EntityManager.Exists(asc) ||
                    !_world.EntityManager.HasComponent<AscLifecycle>(asc))
                    continue;
                var lifecycle = _world.EntityManager.GetComponentData<AscLifecycle>(asc);
                lifecycle.IngressClosed = 1;
                _world.EntityManager.SetComponentData(asc, lifecycle);
            }
        }

        /// <summary>
        /// 拒绝在 owner 释放或 World 销毁后继续推进 TickBatch。
        /// </summary>
        private void EnsureUsable()
        {
            if (_disposed || !_world.IsCreated)
                throw new ObjectDisposedException(nameof(GasRuntimeWorldOwner));
        }
    }

    /// <summary>
    /// 保存 World 内唯一 owner 与 gate 引用；该 managed component 只供 Runtime 内部系统定位。
    /// </summary>
    internal sealed class GasRuntimeWorldOwnerRegistration : IComponentData
    {
        internal readonly GasRuntimeWorldOwner Owner;
        internal readonly SessionIngressGate IngressGate;

        /// <summary>
        /// 仅供 Entities managed component 类型注册器构造默认实例；生产安装始终使用完整构造函数。
        /// </summary>
        public GasRuntimeWorldOwnerRegistration()
        {
        }

        /// <summary>
        /// 创建不可替换的 world-local owner registration。
        /// </summary>
        internal GasRuntimeWorldOwnerRegistration(
            GasRuntimeWorldOwner owner,
            SessionIngressGate ingressGate)
        {
            Owner = owner;
            IngressGate = ingressGate;
        }
    }

    /// <summary>
    /// 保存 owner 运行时需要的完整 FixedStep 父组与唯一 Ingress system 实例。
    /// </summary>
    internal readonly struct GasRuntimeSystemTopology
    {
        internal readonly SimulationSystemGroup Simulation;
        internal readonly FixedStepSimulationSystemGroup FixedStep;
        internal readonly GasCommandIngressSystem Ingress;

        /// <summary>
        /// 创建已完成注册与排序的最小 Runtime v1 拓扑引用。
        /// </summary>
        internal GasRuntimeSystemTopology(
            SimulationSystemGroup simulation,
            FixedStepSimulationSystemGroup fixedStep,
            GasCommandIngressSystem ingress)
        {
            Simulation = simulation;
            FixedStep = fixedStep;
            Ingress = ingress;
        }
    }

    /// <summary>
    /// 在真实 Simulation outer batch 进入 FixedStep 前应用 Session 冻结时序预算。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    internal partial class GasRuntimeBatchStartSystem : SystemBase
    {
        private EntityQuery _ownerQuery;

        /// <summary>
        /// 缓存当前 World 的唯一 owner registration 查询。
        /// </summary>
        protected override void OnCreate()
        {
            _ownerQuery = GetEntityQuery(
                ComponentType.ReadOnly<GasRuntimeWorldOwnerRegistration>());
            RequireForUpdate(_ownerQuery);
        }

        /// <summary>
        /// 在 FixedRateCatchUpManager 决定本批次数量前投影 TickRate 与最大累计 Tick 数。
        /// </summary>
        protected override void OnUpdate()
        {
            var count = _ownerQuery.CalculateEntityCount();
            if (count != 1)
                throw new InvalidOperationException("Runtime v1 batch start 需要唯一 World owner。");

            var registration = EntityManager.GetComponentObject<
                GasRuntimeWorldOwnerRegistration>(_ownerQuery.GetSingletonEntity());
            registration.Owner.PrepareBatch();
        }
    }

    /// <summary>
    /// 在 PlayerLoop 的完整 FixedStep catch-up 后复用 owner 的唯一 batch fence 后处理。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    internal partial class GasRuntimeBatchFenceSystem : SystemBase
    {
        private EntityQuery _ownerQuery;

        /// <summary>
        /// 缓存当前 World 的 managed owner registration query。
        /// </summary>
        protected override void OnCreate()
        {
            _ownerQuery = GetEntityQuery(
                ComponentType.ReadOnly<GasRuntimeWorldOwnerRegistration>());
            RequireForUpdate(_ownerQuery);
        }

        /// <summary>
        /// 在 Simulation 父链尾部完成与手工 TickBatch 相同的 Boundary batch fence。
        /// </summary>
        protected override void OnUpdate()
        {
            var count = _ownerQuery.CalculateEntityCount();
            if (count != 1)
                throw new InvalidOperationException("Runtime v1 batch fence 需要唯一 World owner。");

            var registration = EntityManager.GetComponentObject<
                GasRuntimeWorldOwnerRegistration>(_ownerQuery.GetSingletonEntity());
            registration.Owner.CompleteBatchFence();
        }
    }

}
