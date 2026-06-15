# 16-02：Boundary Command 与 Core Command Resolve

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分` | 状态：目标态 Spec 子页 | 最近拆分：2026-06-07

本文件只描述 Boundary command record 和 Core command resolve 的目标代码骨架，用于约束 Shell intent 如何进入纯 ECS Runtime Core。

## 相邻 Spec Owner 裁决

`16-02` 的唯一职责是定义 Shell intent 穿过 Runtime Boundary 后的 owner-local command envelope，以及 Core 如何以 Job/query 形态消费这个 envelope。它不维护完整 Ability command normalization、Target Resolve、TargetDataBuffer 或 NativeStream fan-out 规则；这些通用规则归入 [03D Command Resolve 与 Target Resolve](../03-RuntimeCore管线/03D-CommandResolve与TargetResolveSpec.md)。

裁决：

1. Shell / UI / 外部业务默认只能看到业务 command port，不能看到 ECS handle、request entity 或 `TargetDataBuffer`。
2. Boundary implementation 可以把 Shell intent 压成 owner-local command buffer；如果需要低频 request-owned 物化路径，必须引用 `03D` 的选型和退出门。
3. `16-02` 的代码只保留边界 envelope 和 Core 消费骨架；目标解析、AoE fan-out、ability validation、deterministic merge 的完整规则不得在本文件再次展开。

## 目标代码骨架

以下代码是目标态结构骨架，用于说明接口深度、owner、读写方向和 DOTS API 选型。本节只定义可作为框架设计参考的代码形态。

### 1. Boundary 只写 command record

```csharp
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    internal struct GASBoundaryCommandPending : IComponentData, IEnableableComponent
    {
    }

    internal struct GASBoundaryCommand : IBufferElementData
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int AbilityCode;
        public int GameplayEffectCode;
        public int Level;
        public GASBoundaryCommandKind Kind;
        public int Sequence;
    }

    internal enum GASBoundaryCommandKind : byte
    {
        ActivateAbility = 1,
        ApplyGameplayEffect = 2,
        RemoveGameplayEffect = 3,
        DestroyAsc = 4,
    }

    public readonly struct ASCTargetRef
    {
        internal readonly Entity RuntimeEntity;

        internal ASCTargetRef(Entity runtimeEntity)
        {
            RuntimeEntity = runtimeEntity;
        }
    }

    internal readonly struct GASBoundaryCommandWriter
    {
        private readonly EntityManager _entityManager;
        private readonly Entity _ownerAsc;

        internal GASBoundaryCommandWriter(EntityManager entityManager, Entity ownerAsc)
        {
            _entityManager = entityManager;
            _ownerAsc = ownerAsc;
        }

        public bool RequestAbility(int abilityCode, ASCTargetRef target, int sequence)
        {
            if (!IsValid() || !_entityManager.HasBuffer<GASBoundaryCommand>(_ownerAsc))
                return false;

            var commands = _entityManager.GetBuffer<GASBoundaryCommand>(_ownerAsc);
            commands.Add(new GASBoundaryCommand
            {
                SourceAsc = _ownerAsc,
                TargetAsc = target.RuntimeEntity,
                AbilityCode = abilityCode,
                Kind = GASBoundaryCommandKind.ActivateAbility,
                Sequence = sequence,
            });

            if (_entityManager.HasComponent<GASBoundaryCommandPending>(_ownerAsc))
                _entityManager.SetComponentEnabled<GASBoundaryCommandPending>(_ownerAsc, true);

            return true;
        }

        private bool IsValid()
        {
            return _ownerAsc != Entity.Null && _entityManager.Exists(_ownerAsc);
        }
    }
}
```

解释：这段代码把 `EntityManager` 限定在 Runtime Boundary implementation 内。它的职责是把 Shell 意图压成 owner-local command record，不直接执行 GameplayEffect、不写 Attribute、不读 Debugger、不创建 transient request entity。Shell 侧只能看到业务请求接口，不能看到 ECS 写句柄。

### 2. Core 以 IJobChunk 消费 command

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct GASEffectCommandRecord
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int GameplayEffectCode;
        public int Level;
        public int Sequence;
        public int ProducerIndex;
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    public partial struct GASBoundaryCommandResolveSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<GASBoundaryCommandPending>(),
                    ComponentType.ReadWrite<GASBoundaryCommand>(),
                },
            });
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandType = SystemAPI.GetBufferTypeHandle<GASBoundaryCommand>(isReadOnly: false);
            var pendingType = SystemAPI.GetComponentTypeHandle<GASBoundaryCommandPending>(isReadOnly: false);
            var entityType = SystemAPI.GetEntityTypeHandle();

            state.Dependency = new ResolveBoundaryCommandsJob
            {
                EntityType = entityType,
                CommandType = commandType,
                PendingType = pendingType,
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct ResolveBoundaryCommandsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            public BufferTypeHandle<GASBoundaryCommand> CommandType;
            public ComponentTypeHandle<GASBoundaryCommandPending> PendingType;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var commands = chunk.GetBufferAccessor(ref CommandType);
                var pendingMask = chunk.GetEnabledMask(ref PendingType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var i))
                {
                    var ownerCommands = commands[i];
                    for (var commandIndex = 0; commandIndex < ownerCommands.Length; commandIndex++)
                    {
                        var command = ownerCommands[commandIndex];
                        // Target resolve / ability validation / GE seed 只写 frame-local record。
                        // 不在这里创建 runtime GE entity，不写 Attribute，不写 Presentation。
                    }

                    ownerCommands.Clear();
                    pendingMask[i] = false;
                }
            }
        }
    }
}
```

解释：Core 的接口是 command buffer 和 job query，不是 OOP 方法调用。`SystemState.GetEntityQuery` 让 query owner 可追踪；`ChunkEntityEnumerator` 让 enableable 语义显式；job 内不执行结构变化。
