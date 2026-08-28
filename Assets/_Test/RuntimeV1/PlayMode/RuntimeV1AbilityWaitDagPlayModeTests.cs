using System;
using GAS.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.PlayMode
{
    /// <summary>
    /// 在正式 Tick DAG 中验证跨 ASC wait registration 与 persistent wake 的 durable 路由。
    /// </summary>
    [TestFixture]
    public class RuntimeV1AbilityWaitDagPlayModeTests
    {
        /// <summary>
        /// 验证 Edge/Event 注册、两次持久唤醒及 ordinal/payload 独立性。
        /// </summary>
        [TestCase(GasAbilityWaitSemantic.Edge, GasAbilityWaitSignalKind.EdgeObserved)]
        [TestCase(GasAbilityWaitSemantic.Event, GasAbilityWaitSignalKind.EventObserved)]
        public void TickDag_Registration与PersistentWake保持独立序号和Payload(
            GasAbilityWaitSemantic semantic,
            GasAbilityWaitSignalKind signalKind)
        {
            using var fixture = new RuntimeV1AbilityWaitDagTestWorld();
            var seed = fixture.SeedPendingWait(semantic);

            fixture.AdvanceUntilRegistrationAck(in seed);
            fixture.ConsumeRegistrationAck(in seed);
            var payloads = fixture.AllocatePayloadRanges();
            fixture.SeedPersistentSignals(in seed, signalKind, in payloads);
            fixture.AdvanceUntilRunnableWakeCount(in seed, 2);
            fixture.AssertPersistentWakeResults(in seed, in payloads);
        }
    }

    /// <summary>
    /// 复用正式单 ASC bootstrap，并在测试准备期补齐一个 Ready observed ASC。
    /// </summary>
    internal sealed class RuntimeV1AbilityWaitDagTestWorld : IDisposable
    {
        private const uint InitialGeneration = 1;
        private const string ReusedWorldName = "Runtime v1 Tick DAG PlayMode test";

        private readonly RuntimeV1TickDagTestWorld _runtime;
        private readonly World _world;
        private readonly Entity _observedEntity;

        internal ulong CurrentTick => _runtime.CurrentTick;
        private EntityManager EntityManager => _world.EntityManager;
        private OwnerAscHandle ObservedAsc => new OwnerAscHandle(202, 1);

        /// <summary>
        /// 建立测试 World、扩展 Session registry 容量并发布第二个 Ready ASC。
        /// </summary>
        internal RuntimeV1AbilityWaitDagTestWorld()
        {
            _runtime = new RuntimeV1TickDagTestWorld();
            _world = ResolveReusedWorld();
            _observedEntity = CreateObservedAsc();
        }

        /// <summary>
        /// 释放正式 Tick fixture 持有的 WorldOwner、World 与 Catalog。
        /// </summary>
        public void Dispose()
        {
            _runtime.Dispose();
        }

        /// <summary>
        /// 写入 owner live grant/activation，并通过正式 slab transaction 创建 wait registration。
        /// </summary>
        internal RuntimeV1AbilityWaitSeed SeedPendingWait(GasAbilityWaitSemantic semantic)
        {
            var epoch = _runtime.Battle.SimulationEpoch;
            var grant = new GrantedAbilityHandle(epoch, _runtime.OwnerAsc, 0, InitialGeneration);
            var activation = new AbilityActivationHandle(epoch, _runtime.OwnerAsc, 0, InitialGeneration);
            SeedGrant(in grant);
            SeedActivation(in grant, in activation);

            var heads = EntityManager.GetComponentData<AscSlabHeads>(_runtime.Asc);
            var request = new GasAbilityContinuationCreateRequest
            {
                Activation = activation,
                ObservedAsc = ObservedAsc,
                Semantic = semantic,
                Policy = GasAbilityWaitPolicy.Persistent,
                CurrentTick = CurrentTick,
                DueTick = 0,
                CommandSequence = 7001,
                ProgramCounter = 3,
                InstanceNameId = 4,
                QueryKey = 5,
            };
            Assert.That(GasAbilityWaitSlabTransaction.TryBeginWait(
                in request,
                4,
                EntityManager.GetBuffer<AbilityActivationSlot>(_runtime.Asc),
                EntityManager.GetBuffer<AbilityContinuationSlot>(_runtime.Asc),
                ref heads.AbilityContinuation,
                out var continuation,
                out var registration), Is.True);
            EntityManager.SetComponentData(_runtime.Asc, heads);
            EnqueueCommand(_runtime.Asc, in registration);
            return new RuntimeV1AbilityWaitSeed(in grant, in activation, in continuation);
        }

        /// <summary>
        /// 推进 Tick 直到 observed Subscription 与 owner registration Ack 同时落盘。
        /// </summary>
        internal void AdvanceUntilRegistrationAck(in RuntimeV1AbilityWaitSeed seed)
        {
            for (var attempt = 0; attempt < 6; attempt++)
            {
                _runtime.TickBatch();
                if (TryFindSubscription(in seed, out _) &&
                    TryFindCommand(_runtime.Asc, GasAbilityPendingCommandKind.WaitRegistrationAck,
                        out _))
                    return;
            }
            Assert.Fail("registration 未在有界 Tick 内形成 Subscription 与 Ack。");
        }

        /// <summary>
        /// 消费 registration Ack，并确认 owner continuation 已进入 Registered。
        /// </summary>
        internal void ConsumeRegistrationAck(in RuntimeV1AbilityWaitSeed seed)
        {
            _runtime.TickBatch();
            var continuation = EntityManager.GetBuffer<AbilityContinuationSlot>(_runtime.Asc)[
                seed.Continuation.SlotIndex];
            Assert.That(continuation.WaitState, Is.EqualTo(GasAbilityWaitState.Registered));
            Assert.That(continuation.Subscription.IsValid, Is.True);
            Assert.That(TryFindCommand(_runtime.Asc, GasAbilityPendingCommandKind.WaitRegistrationAck,
                out _), Is.False);
        }

        /// <summary>
        /// 在 owner payload allocator 中分配两个不重叠的持久 range。
        /// </summary>
        internal RuntimeV1AbilityWaitPayloads AllocatePayloadRanges()
        {
            var profile = EntityManager.GetComponentData<GasScaleProfile>(_runtime.Session);
            var state = EntityManager.GetComponentData<GasPayloadRangeAllocatorState>(_runtime.Asc);
            var records = EntityManager.GetBuffer<GasPayloadRangeRecord>(_runtime.Asc);
            var storage = new GasPayloadRangeDynamicBufferStorage(records);
            Assert.That(GasPayloadRangeAllocator.TryAllocate(
                ref state, ref storage, PayloadKind.AbilityContinuation, 1,
                profile.MaxPayloadRangeRecordCount, profile.MaxPayloadValueCount,
                out var first), Is.EqualTo(GasPayloadRangeStorageFailure.None));
            Assert.That(GasPayloadRangeAllocator.TryAllocate(
                ref state, ref storage, PayloadKind.AbilityContinuation, 1,
                profile.MaxPayloadRangeRecordCount, profile.MaxPayloadValueCount,
                out var second), Is.EqualTo(GasPayloadRangeStorageFailure.None));
            EntityManager.SetComponentData(_runtime.Asc, state);
            return new RuntimeV1AbilityWaitPayloads(first.Handle, second.Handle);
        }

        /// <summary>
        /// 向 observed pending slab 写入同一订阅的两个 due persistent signal。
        /// </summary>
        internal void SeedPersistentSignals(
            in RuntimeV1AbilityWaitSeed seed,
            GasAbilityWaitSignalKind signalKind,
            in RuntimeV1AbilityWaitPayloads payloads)
        {
            Assert.That(TryFindSubscription(in seed, out var subscription), Is.True);
            var baseline = subscription.ObservedRevisionAtRegister;
            AddSignal(in seed, in subscription, signalKind, baseline + 1, 9101, in payloads.First);
            AddSignal(in seed, in subscription, signalKind, baseline + 2, 9102, in payloads.Second);
        }

        /// <summary>
        /// 推进 Tick 直到 owner pending slab 中出现指定数量的 Active wake completion。
        /// </summary>
        internal void AdvanceUntilRunnableWakeCount(
            in RuntimeV1AbilityWaitSeed seed,
            int expectedCount)
        {
            for (var attempt = 0; attempt < 6; attempt++)
            {
                _runtime.TickBatch();
                if (CountActiveWakeCompletions(in seed) == expectedCount)
                    return;
            }
            Assert.Fail("persistent wake 未在有界 Tick 内形成 runnable completion。");
        }

        /// <summary>
        /// 验证两条 wake 的 ordinal 连续、completion 身份一致且 payload range 不重叠。
        /// </summary>
        internal void AssertPersistentWakeResults(
            in RuntimeV1AbilityWaitSeed seed,
            in RuntimeV1AbilityWaitPayloads payloads)
        {
            var commands = EntityManager.GetBuffer<PendingCommand>(_runtime.Asc);
            var first = default(PendingCommand);
            var second = default(PendingCommand);
            var count = 0;
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Header.StorageState != GasSlabSlotState.Live ||
                    command.State != GasSlotBusinessState.Active ||
                    command.CommandKind != (int)GasAbilityPendingCommandKind.WaitCompletion ||
                    !command.Continuation.Equals(seed.Continuation))
                    continue;
                if (count++ == 0)
                    first = command;
                else
                    second = command;
            }
            Assert.That(count, Is.EqualTo(2));
            Assert.That(first.WakeOrdinal, Is.EqualTo(1));
            Assert.That(second.WakeOrdinal, Is.EqualTo(2));
            Assert.That(first.PayloadRange, Is.EqualTo(payloads.First));
            Assert.That(second.PayloadRange, Is.EqualTo(payloads.Second));
            Assert.That(first.PayloadRange.Offset + first.PayloadRange.Length,
                Is.LessThanOrEqualTo(second.PayloadRange.Offset));
        }

        /// <summary>
        /// 在 owner pending slab 追加一条已由正式 wait transaction 创建的命令。
        /// </summary>
        private void EnqueueCommand(Entity ownerEntity, in PendingCommand command)
        {
            var profile = EntityManager.GetComponentData<GasScaleProfile>(_runtime.Session);
            var heads = EntityManager.GetComponentData<AscSlabHeads>(ownerEntity);
            var commands = EntityManager.GetBuffer<PendingCommand>(ownerEntity);
            var storage = new GasPendingCommandSlabStorage { Buffer = commands };
            Assert.That(GasNonCompactingSlabAllocator.TryAllocate(
                ref heads.PendingCommand, ref storage, profile.MaxPendingCommandCount,
                out var allocation), Is.EqualTo(GasSlabStorageFailure.None));
            var value = command;
            value.Header = allocation.LiveHeader;
            commands[allocation.SlotIndex] = value;
            EntityManager.SetComponentData(ownerEntity, heads);
        }

        /// <summary>
        /// 创建完整 signal 命令并写入 observed owner 的 pending slab。
        /// </summary>
        private void AddSignal(
            in RuntimeV1AbilityWaitSeed seed,
            in AbilitySubscriptionSlot subscription,
            GasAbilityWaitSignalKind signalKind,
            ulong observedOrdinal,
            ulong commandSequence,
            in PayloadRangeHandle payload)
        {
            var signal = new PendingCommand
            {
                SourceAsc = ObservedAsc,
                TargetAsc = ObservedAsc,
                Activation = seed.Activation,
                Continuation = seed.Continuation,
                Subscription = subscription.Handle,
                PayloadRange = payload,
                AvailableTick = CurrentTick + 1,
                CommandSequence = commandSequence,
                RegistrationSequence = subscription.RegistrationSequence,
                ObservedRevision = observedOrdinal,
                QueryKey = subscription.QueryKey,
                RegistrationGeneration = subscription.RegistrationGeneration,
                CommandKind = (int)GasAbilityPendingCommandKind.WaitSignal,
                WaitSemantic = subscription.WaitSemantic,
                WaitPolicy = subscription.WaitPolicy,
                WaitSignalKind = signalKind,
                State = GasSlotBusinessState.Pending,
            };
            EnqueueCommand(_observedEntity, in signal);
        }

        /// <summary>
        /// 统计 owner slab 中指定 continuation 的 active completion 数量。
        /// </summary>
        private int CountActiveWakeCompletions(in RuntimeV1AbilityWaitSeed seed)
        {
            var commands = EntityManager.GetBuffer<PendingCommand>(_runtime.Asc);
            var count = 0;
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Header.StorageState == GasSlabSlotState.Live &&
                    command.State == GasSlotBusinessState.Active &&
                    command.CommandKind == (int)GasAbilityPendingCommandKind.WaitCompletion &&
                    command.Continuation.Equals(seed.Continuation))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 查找 owner/observed slab 中指定类型的 live pending 命令。
        /// </summary>
        private bool TryFindCommand(
            Entity entity,
            GasAbilityPendingCommandKind kind,
            out PendingCommand result)
        {
            var commands = EntityManager.GetBuffer<PendingCommand>(entity);
            result = default;
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Header.StorageState == GasSlabSlotState.Live &&
                    command.State == GasSlotBusinessState.Pending &&
                    command.CommandKind == (int)kind)
                {
                    result = command;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 查找 observed slab 中与 owner continuation 完整匹配的 live Subscription。
        /// </summary>
        private bool TryFindSubscription(
            in RuntimeV1AbilityWaitSeed seed,
            out AbilitySubscriptionSlot result)
        {
            var subscriptions = EntityManager.GetBuffer<AbilitySubscriptionSlot>(_observedEntity);
            result = default;
            for (var index = 0; index < subscriptions.Length; index++)
            {
                var subscription = subscriptions[index];
                if (subscription.Header.StorageState == GasSlabSlotState.Live &&
                    subscription.Activation.Equals(seed.Activation) &&
                    subscription.Continuation.Equals(seed.Continuation))
                {
                    result = subscription;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 写入 grant 并同步 non-compacting high-water。
        /// </summary>
        private void SeedGrant(in GrantedAbilityHandle grant)
        {
            var grants = EntityManager.GetBuffer<GrantedAbilitySlot>(_runtime.Asc);
            grants.Add(new GrantedAbilitySlot
            {
                Header = GasSlabSlotHeader.CreateLive(InitialGeneration),
                Handle = grant,
                DefinitionIndex = 0,
                ChildActivationCount = 0,
            });
            var heads = EntityManager.GetComponentData<AscSlabHeads>(_runtime.Asc);
            heads.GrantedAbility.HighWater = 1;
            EntityManager.SetComponentData(_runtime.Asc, heads);
        }

        /// <summary>
        /// 写入可运行 Activation 并同步 grant 反向 child 计数与 slab high-water。
        /// </summary>
        private void SeedActivation(
            in GrantedAbilityHandle grant,
            in AbilityActivationHandle activation)
        {
            var activations = EntityManager.GetBuffer<AbilityActivationSlot>(_runtime.Asc);
            activations.Add(new AbilityActivationSlot
            {
                Header = GasSlabSlotHeader.CreateLive(InitialGeneration),
                Handle = activation,
                GrantedAbility = grant,
                Phase = GasAbilityActivationPhase.Committed,
                LastCommandResult = GasAbilityCommandResult.Committed,
            });
            var grants = EntityManager.GetBuffer<GrantedAbilitySlot>(_runtime.Asc);
            var grantSlot = grants[grant.SlotIndex];
            grantSlot.ChildActivationCount = 1;
            grants[grant.SlotIndex] = grantSlot;
            var heads = EntityManager.GetComponentData<AscSlabHeads>(_runtime.Asc);
            heads.AbilityActivation.HighWater = 1;
            EntityManager.SetComponentData(_runtime.Asc, heads);
        }

        /// <summary>
        /// 定位 bootstrap fixture 的唯一 World，避免绕过正式 Session/ASC 建立路径。
        /// </summary>
        private World ResolveReusedWorld()
        {
            World resolved = null;
            var matches = 0;
            foreach (var candidate in World.All)
            {
                if (!candidate.IsCreated || candidate.Name != ReusedWorldName)
                    continue;
                var manager = candidate.EntityManager;
                if (!manager.Exists(_runtime.Session) || !manager.Exists(_runtime.Asc))
                    continue;
                if (manager.GetComponentData<GasSessionIdentity>(_runtime.Session).SimulationEpoch !=
                    _runtime.Battle.SimulationEpoch ||
                    !manager.GetComponentData<GasAscIdentity>(_runtime.Asc).OwnerAsc.Equals(
                        _runtime.OwnerAsc))
                    continue;
                resolved = candidate;
                matches++;
            }
            Assert.That(matches, Is.EqualTo(1));
            return resolved;
        }

        /// <summary>
        /// 创建并初始化 observed ASC 的全部固定 buffers 与 lifecycle components。
        /// </summary>
        private Entity CreateObservedAsc()
        {
            var profile = EntityManager.GetComponentData<GasScaleProfile>(_runtime.Session);
            profile.MaxAscRegistryCount = 2;
            profile.MaxSpawnBatchSize = 2;
            profile.MaxPayloadRangeRecordCount = 4;
            profile.MaxPayloadValueCount = 8;
            EntityManager.SetComponentData(_runtime.Session, profile);
            Assert.That(GasRuntimeV1Archetypes.TryApplySessionCapacities(
                EntityManager, _runtime.Session, in profile), Is.True);
            Assert.That(GasRuntimeV1Archetypes.TryApplyAscCapacities(
                EntityManager, _runtime.Asc, in profile), Is.True);
            var entity = EntityManager.CreateEntity(GasRuntimeV1Archetypes.CreateAsc(EntityManager));
            var catalog = EntityManager.GetComponentData<GasCatalogRegistry>(_runtime.Session);
            Assert.That(GasRuntimeV1Archetypes.TryInitializeAscMetadata(
                EntityManager, entity), Is.True);
            Assert.That(GasRuntimeV1Archetypes.TryApplyAscCapacities(
                EntityManager, entity, in profile), Is.True);
            Assert.That(GasRuntimeV1Archetypes.TryInitializeFixedBuffers(
                EntityManager, entity, in catalog), Is.True);
            InitializeObservedComponents(entity);
            PublishObservedRegistry(entity);
            return entity;
        }

        /// <summary>
        /// 复制 owner 的 battle/lifecycle 身份，替换 observed 稳定身份并关闭 spawn marker。
        /// </summary>
        private void InitializeObservedComponents(Entity entity)
        {
            var ownerMembership = EntityManager.GetComponentData<AscBattleMembership>(_runtime.Asc);
            var ownerLifecycle = EntityManager.GetComponentData<AscLifecycle>(_runtime.Asc);
            EntityManager.SetComponentData(entity, new GasAscIdentity
            {
                SimulationEpoch = _runtime.Battle.SimulationEpoch,
                OwnerAsc = ObservedAsc,
            });
            EntityManager.SetComponentData(entity, new AscBattleMembership
            {
                BattleInstance = _runtime.Battle,
                ScenarioUnitId = ownerMembership.ScenarioUnitId + 1,
                SideId = ownerMembership.SideId,
                TeamId = ownerMembership.TeamId,
                MembershipOrdinal = ownerMembership.MembershipOrdinal + 1,
            });
            EntityManager.SetComponentData(entity, ownerLifecycle);
            EntityManager.SetComponentData(entity, new GasActorBinding
            {
                OwnerActorStableId = 3000,
                AvatarActorStableId = 4000,
                BindingGeneration = 1,
            });
            EntityManager.SetComponentData(entity, new AscRandomState { State0 = 5000, State1 = 6000 });
            var observedAsc = ObservedAsc;
            EntityManager.SetComponentData(entity, GasPayloadRangeAllocatorState.Create(
                _runtime.Battle.SimulationEpoch, in observedAsc));
            EntityManager.SetComponentData(entity, BoundaryDrainState.Create(
                _runtime.Battle.SimulationEpoch, GasBoundaryOwnerKind.Asc,
                ObservedAsc.AscStableId, ObservedAsc.AscGeneration, 1));
            EntityManager.SetComponentEnabled<GasSpawnBatchMarker>(entity, false);
        }

        /// <summary>
        /// 将 observed identity 发布为第二个 Ready registry/battle member。
        /// </summary>
        private void PublishObservedRegistry(Entity entity)
        {
            var registry = EntityManager.GetBuffer<AscRegistrySlot>(_runtime.Session);
            Assert.That(registry.Length, Is.EqualTo(1));
            var ownerSlot = registry[0];
            var observedSlot = new AscRegistrySlot
            {
                Header = GasSlabSlotHeader.CreateLive(InitialGeneration),
                OwnerAsc = ObservedAsc,
                BattleInstance = _runtime.Battle,
                RegistryOrdinal = 1,
                SpawnBatchId = ownerSlot.SpawnBatchId,
                ReadyTick = ownerSlot.ReadyTick,
                State = GasAscRegistryState.Ready,
            };
            observedSlot.SetRuntimeEntity(entity);
            registry.Add(observedSlot);
            var battles = EntityManager.GetBuffer<BattleInstanceSlot>(_runtime.Session);
            var battle = battles[0];
            battle.MemberCount = 2;
            battle.ReadyMemberCount = 2;
            battles[0] = battle;
        }
    }

    /// <summary>
    /// 冻结一次 owner wait 的完整稳定身份。
    /// </summary>
    internal readonly struct RuntimeV1AbilityWaitSeed
    {
        internal readonly GrantedAbilityHandle Grant;
        internal readonly AbilityActivationHandle Activation;
        internal readonly AbilityContinuationHandle Continuation;

        /// <summary>
        /// 保存 grant、activation 与 continuation 的 typed handles。
        /// </summary>
        internal RuntimeV1AbilityWaitSeed(
            in GrantedAbilityHandle grant,
            in AbilityActivationHandle activation,
            in AbilityContinuationHandle continuation)
        {
            Grant = grant;
            Activation = activation;
            Continuation = continuation;
        }
    }

    /// <summary>
    /// 保存两个 signal 使用的非重叠 payload range。
    /// </summary>
    internal readonly struct RuntimeV1AbilityWaitPayloads
    {
        internal readonly PayloadRangeHandle First;
        internal readonly PayloadRangeHandle Second;

        /// <summary>
        /// 保存两个 allocator 返回的稳定 range handle。
        /// </summary>
        internal RuntimeV1AbilityWaitPayloads(
            in PayloadRangeHandle first,
            in PayloadRangeHandle second)
        {
            First = first;
            Second = second;
        }
    }
}
