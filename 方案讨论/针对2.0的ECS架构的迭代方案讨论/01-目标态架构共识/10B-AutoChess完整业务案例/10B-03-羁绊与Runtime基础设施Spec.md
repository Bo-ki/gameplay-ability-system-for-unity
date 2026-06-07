# 10B-03：羁绊与 Runtime 基础设施目标设计

> Owner：`01-目标态架构共识/10B-AutoChess完整业务案例` | 状态：目标态子 Spec | 拆分来源：`../10B-AutoChess完整业务案例设计Spec.md` | 最近拆分：2026-06-07

本文件只描述 AutoChess 完整业务案例的目标态设计。禁止写入当前代码事实、执行流水、验证数字或下一步任务；现实证据必须回到 `../../00-当前架构事实/`，任务拆分必须回到 `../../02-主线任务树/`。
## 七、羁绊系统设计

### 7.1 羁绊 Luban 配置（`autochess.synergy.xlsx`）

| SynergyId | Name | Race/Class | Threshold | EffectGE | Description |
|-----------|------|------------|-----------|----------|-------------|
| 1 | 冰系羁绊 | Race=Frost | 3 | 4002 | 3+ 冰系单位在场时，所有攻击附加 ASPD×0.7 减速 |
| 2 | 牧师羁绊 | Class=Priest | 2 | — | 2+ 牧师在场时，所有治疗量 ×1.2 |

### 7.2 羁绊 System 设计

羁绊检测通过 typed fact reaction 驱动，不通过全局 EventBus 扫描。每帧在 `GASCoreSimulationSystemGroup` 中检测羁绊条件变化，产出 typed fact；目标态 fact reaction 默认写 next-frame command seed，避免同帧无界连锁。

```csharp
// ============================================================
// [Layer 3: GAS Runtime Core — GASCoreSimulationSystemGroup]
// 羁绊检测 System：统计场上各种族/职业数量
// 当阈值跨越时产出 SynergyActivated / SynergyDeactivated fact
//
// CASE-02 (IJobEntity) 用于并行统计 — 拒绝 CASE-01 (SystemAPI.Query)
// 原因: 羁绊检测每帧遍历所有存活单位，hot path 主线程 foreach 不可接受
// ============================================================

[UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
[BurstCompile]
public partial struct GameplayEventSynergyDetectSystem : ISystem
{
    private EntityQuery _aliveUnitsQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _aliveUnitsQuery = state.GetEntityQuery(
            ComponentType.ReadOnly<ASCIdentityComponent>(),
            ComponentType.ReadOnly<TagMaskComponent>());
        state.RequireForUpdate(_aliveUnitsQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var tagMaskType = SystemAPI.GetComponentTypeHandle<TagMaskComponent>(isReadOnly: true);
        var frame = GASFrameClock.ResolveFrame(ref state);
        var chunkCount = _aliveUnitsQuery.CalculateChunkCount();
        var synergyFacts = new NativeStream(chunkCount, Allocator.TempJob);

        state.Dependency = new SynergyCountAndFactJob
        {
            TagMaskType = tagMaskType,
            Facts = synergyFacts.AsWriter(),
            Frame = frame,
        }.ScheduleParallel(_aliveUnitsQuery, state.Dependency);

        state.Dependency = new DeterministicSynergyFactMergeJob
        {
            Facts = synergyFacts.AsReader(),
            Sink = GASFrameFactSink.Resolve(ref state),
        }.Schedule(state.Dependency);

        state.Dependency = synergyFacts.Dispose(state.Dependency);
    }
}

[BurstCompile]
public struct SynergyCountAndFactJob : IJobChunk
{
    [ReadOnly] public ComponentTypeHandle<TagMaskComponent> TagMaskType;
    public NativeStream.Writer Facts;
    public int Frame;

    public void Execute(
        in ArchetypeChunk chunk,
        int unfilteredChunkIndex,
        bool useEnabledMask,
        in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
    {
        var tagMasks = chunk.GetNativeArray(ref TagMaskType);
        var frostCount = 0;
        var priestCount = 0;

        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out var i))
        {
            var tagMask = tagMasks[i].Value;
            if (TagCheck.IsDead(tagMask))
                continue;

            if ((tagMask & XTagBit.SynFrost) != 0)
                frostCount++;

            if ((tagMask & XTagBit.SynPriest) != 0)
                priestCount++;
        }

        Facts.BeginForEachIndex(unfilteredChunkIndex);
        Facts.Write(new GameplayFactRecord
        {
            FactCode = frostCount >= 3 ? FactCode.SynergyFrostActivated : FactCode.SynergyFrostDeactivated,
            Frame = Frame,
            Value = frostCount,
            SortKey = unfilteredChunkIndex,
        });
        Facts.Write(new GameplayFactRecord
        {
            FactCode = priestCount >= 2 ? FactCode.SynergyPriestActivated : FactCode.SynergyPriestDeactivated,
            Frame = Frame,
            Value = priestCount,
            SortKey = unfilteredChunkIndex,
        });
        Facts.EndForEachIndex();
    }
}

[BurstCompile]
public struct DeterministicSynergyFactMergeJob : IJob
{
    [ReadOnly] public NativeStream.Reader Facts;
    public GASFrameFactSink Sink;

    public void Execute()
    {
        for (var i = 0; i < Facts.ForEachCount; i++)
        {
            Facts.BeginForEachIndex(i);
            while (Facts.RemainingItemCount > 0)
                Sink.Write(Facts.Read<GameplayFactRecord>());
            Facts.EndForEachIndex();
        }
    }
}

public struct GameplayFactRecord
{
    public int FactCode;
    public int Frame;
    public int Value;
    public int SortKey;
}

// Fact 类型常量（SourceGenerator 生成）
public static class FactCode
{
    public const int SynergyFrostActivated   = 10001;
    public const int SynergyFrostDeactivated = 10002;
    public const int SynergyPriestActivated  = 10003;
    public const int SynergyPriestDeactivated = 10004;
    public const int AttributeChanged        = 20001;
    public const int DamageResolved          = 20002;
    public const int HealResolved            = 20003;
    public const int DeathOccurred           = 20004;
    public const int EffectApplied           = 20005;
    public const int EffectExpired           = 20006;
    public const int StackOverflow           = 20007;
    public const int CueMarker               = 30001;
}
```

---

### 7.3 Runtime Core 基础设施 Component 和 Buffer 定义

以下类型是 Runtime Core 管线的通用基础设施，由 GAS Runtime Core 层定义，业务 System 直接引用。目标态不再声明全局 `GEStreamOwnerComponent` 作为 command / spec / fact / mutation 的宿主；frame-local fan-in 使用 `NativeStream`、deterministic merge 和 owner-local compact range，少量系统级状态只作为 lane metadata 存在，不承载 gameplay 数据。

> **目标态校准：** `GEEffectCommandBuffer`、`GEEffectSpecBuffer`、`AttributeModifierBuffer`、`ActiveEffectMutationBuffer`、`GameplayEventBuffer` 在本节表达的是 owner-local range 或 frame-local scratch 的记录形态，不代表全局 singleton DynamicBuffer。若实现中仍保留 singleton stream owner，必须标记为 `MigrationProofOnly` 并回到 R2/R3/R4/R7 任务收权。

```csharp
// ============================================================
// [Layer 3: GAS Runtime Core — lane metadata / owner-local range 定义]
// 每个类型明确标注: Data / Buffer / Enableable / Chunk / Cleanup
// 符合不变量 34: 禁止"ECS component"笼统称呼
// 符合不变量 33: 每个 DynamicBuffer 有 InternalBufferCapacity 声明
// ============================================================

// --- Effect Fan-In lane metadata ---
// 类型: Data (IComponentData, system-associated entity data)
// 只记录 lane 预算和本帧 sequence，不承载 gameplay command/spec/fact 数据。
public struct EffectFanInLaneStateComponent : IComponentData
{
    public int Frame;
    public int LastCommandSequence;
    public int MaxCommandRangeLength;
    public int MaxFactRangeLength;
}

// --- Frame-local command range header ---
// 类型: Data record。实际 payload 由 NativeStream 或 owner-local DynamicBuffer 承载。
public struct EffectCommandRangeHeader
{
    public Entity OwnerAsc;
    public int Frame;
    public int Start;
    public int Length;
    public int SortKey;
}

// --- Frame-local fact range header ---
// 类型: Data record。Observation / Debugger 只消费 merge 后的 stable range。
public struct GameplayFactRangeHeader
{
    public Entity OwnerAsc;
    public int Frame;
    public int Start;
    public int Length;
    public int SortKey;
}

// --- Effect command sink ---
// 类型: Runtime helper。实际 storage 由 Frame Arena / NativeStream registry 拥有，
// System 只拿 parallel writer，不持有 singleton buffer entity。
public struct GASEffectCommandSink
{
    private GASFrameAppendWriter<GEEffectCommandBuffer> _writer;

    public static GASEffectCommandSink Resolve(ref SystemState state)
    {
        return GASFrameNativeStreamRegistry.ResolveEffectCommandSink(ref state);
    }

    public ParallelWriter AsParallelWriter()
    {
        return new ParallelWriter
        {
            Writer = _writer,
        };
    }

    public struct ParallelWriter
    {
        public GASFrameAppendWriter<GEEffectCommandBuffer> Writer;

        public void Write(int sortKey, in GEEffectCommandBuffer command)
        {
            Writer.Append(sortKey, command);
        }
    }
}

// --- Effect command reader ---
// 类型: Runtime helper。EffectFanIn lane 只读取本帧 command stream，
// 不知道也不持有 stream owner entity。
public struct GASEffectCommandReader
{
    private NativeStream.Reader _reader;

    public int ForEachCount;
    public int CommandCount;

    public static GASEffectCommandReader Resolve(ref SystemState state)
    {
        return GASFrameNativeStreamRegistry.ResolveEffectCommandReader(ref state);
    }

    public NativeStream.Reader AsNativeStreamReader()
    {
        return _reader;
    }
}

// --- Effect spec sink ---
// 类型: Runtime helper。Fan-In 并行 job 写 frame-local spec stream，
// 后续 deterministic merge 生成 target grouped range。
public struct GASEffectSpecSink
{
    private NativeStream.Writer _writer;
    private NativeStream.Reader _reader;

    public int ForEachCount;

    public static GASEffectSpecSink Resolve(ref SystemState state)
    {
        return GASFrameNativeStreamRegistry.ResolveEffectSpecSink(ref state);
    }

    public NativeStream.Writer AsNativeStreamWriter()
    {
        return _writer;
    }

    public NativeStream.Reader AsNativeStreamReader()
    {
        return _reader;
    }
}

// --- Active effect mutation sink ---
// 类型: Runtime helper。Fan-In 并行 job 写 frame-local mutation stream，
// State lane 只消费 deterministic merge 后的 owner-local range。
public struct GASActiveEffectMutationSink
{
    private NativeStream.Writer _writer;
    private NativeStream.Reader _reader;

    public int ForEachCount;

    public static GASActiveEffectMutationSink Resolve(ref SystemState state)
    {
        return GASFrameNativeStreamRegistry.ResolveActiveEffectMutationSink(ref state);
    }

    public NativeStream.Writer AsNativeStreamWriter()
    {
        return _writer;
    }

    public NativeStream.Reader AsNativeStreamReader()
    {
        return _reader;
    }
}

// --- Fan-In merge sink ---
// 类型: Runtime helper。由 Frame Arena 提供 scratch payload 和 range header，
// merge job 负责按 TargetAsc + SortKey 写成稳定 owner-local compact range。
public struct GASEffectFanInRangeSink
{
    public NativeList<EffectFanInSpecRecord> SpecScratch;
    public NativeList<EffectFanInMutationRecord> MutationScratch;
    public NativeList<EffectCommandRangeHeader> SpecRanges;
    public NativeList<GEEffectSpecBuffer> SpecPayload;
    public NativeList<EffectCommandRangeHeader> MutationRanges;
    public NativeList<ActiveEffectMutationBuffer> MutationPayload;

    public static GASEffectFanInRangeSink Resolve(ref SystemState state)
    {
        return GASFrameNativeStreamRegistry.ResolveEffectFanInRangeSink(ref state);
    }
}

// --- Generic frame-local NativeStream writer ---
// 类型: Runtime helper。统一表达 fact / presentation / structural intent /
// period command 等 frame-local 输出，不把 ECB 或 singleton buffer 当事件总线。
public struct GASNativeStreamWriter<T>
    where T : unmanaged
{
    private GASFrameAppendWriter<T> _writer;

    public void Write(int sortKey, in T value)
    {
        _writer.Append(sortKey, value);
    }
}

public struct GASFrameAppendWriter<T>
    where T : unmanaged
{
    // 实际实现由 Frame Arena / NativeStream registry 拥有 NativeStream.Writer，
    // 并保证同一个 foreachIndex 只 Begin/End 一次。
    // 调用方只表达 append 语义，避免把 NativeStream segment 生命周期散落到业务 System。
    public void Append(int sortKey, in T value)
    {
        GASFrameNativeStreamRegistry.Append(sortKey, value);
    }
}

// --- Target grouped range reader ---
// 类型: Runtime helper。deterministic merge 后的 owner-local compact range 只读视图。
public struct GASTargetGroupedRangeReader<T>
    where T : unmanaged
{
    [ReadOnly] public NativeArray<GASTargetRangeHeader> Ranges;
    [ReadOnly] public NativeArray<T> Payload;

    public bool TryGetRange(Entity ownerAsc, out GASTargetRange range)
    {
        for (var i = 0; i < Ranges.Length; i++)
        {
            var header = Ranges[i];
            if (header.OwnerAsc.Index == ownerAsc.Index &&
                header.OwnerAsc.Version == ownerAsc.Version)
            {
                range = new GASTargetRange
                {
                    Start = header.Start,
                    End = header.Start + header.Length,
                };
                return true;
            }
        }

        range = default;
        return false;
    }

    public T this[int index] => Payload[index];
}

public struct GASTargetRangeHeader
{
    public Entity OwnerAsc;
    public int Start;
    public int Length;
}

public struct GASTargetRange
{
    public int Start;
    public int End;
}

// --- Structural intent ---
// 类型: Data record。Core hot path 只写意图；StructuralCommit phase 统一播放 ECB。
public enum GASStructuralIntentKind : byte
{
    None = 0,
    RequestAscDestroy = 1,
    EnsureActiveEffectStore = 2,
}

public struct GASStructuralIntentRecord
{
    public GASStructuralIntentKind Kind;
    public Entity TargetAsc;
    public int ReasonCode;
    public int SortKey;
}

// --- Frame helper: Active mutation apply ---
public struct GASActiveEffectMutationFrame
{
    public GASTargetGroupedRangeReader<ActiveEffectMutationBuffer> TargetGroupedMutationRanges;
    public GAStaticLookup GameplayEffectLookup;
    public GASNativeStreamWriter<GameplayEventBuffer> FactWriter;
    public GASNativeStreamWriter<GASStructuralIntentRecord> StructuralIntentWriter;

    public static GASActiveEffectMutationFrame Resolve(ref SystemState state)
    {
        return GASFrameNativeStreamRegistry.ResolveActiveEffectMutationFrame(ref state);
    }
}

// --- Frame helper: Active effect tick ---
public struct GASActiveEffectTickFrame
{
    public GAStaticLookup GameplayEffectLookup;
    public GASNativeStreamWriter<GEEffectCommandBuffer> PeriodEffectCommandWriter;
    public GASNativeStreamWriter<GameplayEventBuffer> FactWriter;

    public static GASActiveEffectTickFrame Resolve(ref SystemState state)
    {
        return GASFrameNativeStreamRegistry.ResolveActiveEffectTickFrame(ref state);
    }
}

// --- Frame helper: Gameplay fact / presentation / structural intent ---
public struct GASGameplayFactFrame
{
    public GASNativeStreamWriter<GameplayEventBuffer> FactWriter;
    public GASNativeStreamWriter<PresentationEventBuffer> PresentationFactWriter;
    public GASNativeStreamWriter<GASStructuralIntentRecord> StructuralIntentWriter;

    public static GASGameplayFactFrame Resolve(ref SystemState state)
    {
        return GASFrameNativeStreamRegistry.ResolveGameplayFactFrame(ref state);
    }
}

// --- Ability Activation Request / Command (Boundary → Core) ---
// 类型: Data (IComponentData) — request entity 上的一次激活上下文
public enum AbilityCommandStatus : byte
{
    Pending = 0,
    Valid = 1,
    Rejected = 2,
    Consumed = 3
}

public struct AbilityActivationRequestComponent : IComponentData
{
    public Entity SourceAsc;       // 释放者 ASC entity
    public Entity AbilityEntity;   // granted ability entity
    public Entity ExplicitTargetAsc;
    public int InputSequence;
    public int RequestFrame;
    public int TargetGroupSortKey;
    public byte TargetMode;        // 0=explicit, 1=nearest enemies, etc.
}

public struct AbilityCommandComponent : IComponentData
{
    public Entity SourceAsc;
    public Entity AbilityEntity;
    public Entity ExplicitTargetAsc;
    public int PrimaryGameplayEffectCode;
    public int SecondaryGameplayEffectCode;
    public short Level;
    public int InputSequence;
    public int RequestFrame;
    public int TargetGroupSortKey;
    public byte TargetMode;
    public AbilityCommandStatus Status;
}

[InternalBufferCapacity(4)]
public struct TargetDataBuffer : IBufferElementData
{
    public Entity TargetAsc;
    public int TargetSortKey;
}

public struct AbilityActivationCommandRecord
{
    public int Sequence;
    public int Frame;
    public Entity SourceAsc;
    public Entity AbilityEntity;
    public Entity ExplicitTargetAsc;
    public int PrimaryGameplayEffectCode;
    public short Level;
    public int TargetGroupSortKey;
    public byte TargetMode;
}

public struct AbilityTargetRecord
{
    public int Sequence;
    public int TargetIndex;
    public int TargetSortKey;
    public Entity SourceAsc;
    public Entity SourceAbility;
    public Entity TargetAsc;
    public int GameplayEffectCode;
    public short Level;
}

// --- ASC Owner (来源标记) ---
// 类型: Data (IComponentData) — 挂载在 ASC entity 上，标记所属 player/AI
public struct ASCIdentityComponent : IComponentData
{
    public int PlayerId;           // 0=己方, 1=敌方
    public int CampId;
}

// --- Active Effect Store 标记 ---
// 类型: Data (IComponentData) — 标记此 entity 持有 ActiveGameplayEffectBuffer buffer
// 用于 Query 过滤: WithAll<ASCActiveEffectsComponent> 只匹配持有 active effect 的 entity
public struct ASCActiveEffectsComponent : IComponentData
{
    // 无字段 — query marker，配合 Chunk Component 实现 chunk 级跳过
}

// ============================================================
// GAStaticLookup: ID → BlobAsset 查找表 Singleton
// 类型: Data (IComponentData, singleton)
// 由 SourceGenerator 在 bake 时写入 singleton entity
// ============================================================
public struct GAStaticLookup : IComponentData
{
    // 三种 BlobAsset 查找表的 BlobAssetReference
    // 实际实现使用 BlobMultiHashMap / BlobArray + binary search
    // 此处简化为三字段描述
    public BlobAssetReference<UnitLookupBlob>   UnitLookup;
    public BlobAssetReference<AbilityLookupBlob> AbilityLookup;
    public BlobAssetReference<GELookupBlob>     GELookup;

    // Convenience: 按 GE ID 查找 GEStaticBlob
    public BlobAssetReference<GEStaticBlob> Find(int geId)
    {
        if (GELookup.IsCreated)
            return GELookup.Value.Find(geId);
        return default;
    }
}

// BlobAsset 查找表结构（SourceGenerator 生成）
public struct GELookupBlob
{
    // 按 GE ID 二分查找 BlobArray<(int GeId, int Offset)>
    // 此处为概念示意，实际使用 BlobMultiHashMap 或排序 BlobArray
    public BlobArray<GEStaticBlob> Entries;

    public BlobAssetReference<GEStaticBlob> Find(int geId)
    {
        for (int i = 0; i < Entries.Length; i++)
            if (Entries[i].GeId == geId)
                return default; // 实际返回 index，此处简化
        return default;
    }
}

public struct AbilityLookupBlob
{
    public BlobArray<AbilityDefBlob> Entries;

    public BlobAssetReference<AbilityDefBlob> Find(int abilityId)
    {
        for (int i = 0; i < Entries.Length; i++)
            if (Entries[i].AbilityId == abilityId)
                return default;
        return default;
    }
}

public struct UnitLookupBlob
{
    public BlobArray<UnitConfigBlob> Entries;
}

// ============================================================
// DynamicBuffer 定义（按不变量 33: 必须声明 InternalBufferCapacity）
// ============================================================

// GEEffectCommandBuffer: BoundaryCommandIngest 产出 → EffectFanIn 消费
// 每帧清空，最大容量受 AoE + 多 ability 并发限制
// 容量: x1 精链路 ≤ 32, InternalBufferCapacity = 64
[InternalBufferCapacity(64)]
public struct GEEffectCommandBuffer : IBufferElementData
{
    public int EffectCode;
    public Entity SourceAsc;
    public Entity TargetAsc;
    public int ContextId;         // AbilityId 或 0(=普攻)
}

// GEEffectSpecBuffer: EffectFanIn 产出 → AttributeReduceApply 消费
// 每帧清空，数量 ≤ GEEffectCommandBuffer.Length
// 容量: x1 ≤ 32, InternalBufferCapacity = 64
[InternalBufferCapacity(64)]
public struct GEEffectSpecBuffer : IBufferElementData
{
    public int EffectCode;
    public int ContextId;
    public Entity SourceAsc;
    public Entity TargetAsc;
}

// ActiveEffectMutationBuffer: EffectFanIn 产出 → StateEvaluate 消费
// 每帧清空，数量 ≤ GEEffectCommandBuffer 中 Duration 类型数量
// 容量: x1 ≤ 16, InternalBufferCapacity = 32
[InternalBufferCapacity(32)]
public struct ActiveEffectMutationBuffer : IBufferElementData
{
    public int EffectCode;
    public Entity SourceAsc;
    public Entity TargetAsc;
    public float DurationFrames;
    public float PeriodFrames;
    public byte StackLimit;
    public GEStackPolicy StackPolicy;
    public int ContextId;
}

// ActiveGameplayEffectBuffer: StateEvaluate RW, 挂载在目标 ASC entity
// 持续存在直到 GE 到期, x1 规模 ≤ 8 slots/entity
// 容量: 每 entity 最多约 8-16 个 active slot
// InternalBufferCapacity = 8 (slot 数少，优先 inline 存储)
[InternalBufferCapacity(8)]
public struct ActiveGameplayEffectBuffer : IBufferElementData
{
    public int EffectCode;
    public byte StackCount;       // 最大 255
    public float RemainingDuration;
    public float PeriodAccumulator;
    public int SourceEffectCode;  // 若 stack 合并则指向第一个 GE
    public int ContextId;
    public Entity SourceAsc;
    public Entity TargetAsc;
    public byte Flags;            // EffectSlotFlags bitmask
}

// AttributeModifierBuffer: AttributeReduceApply / Period 产出 → GameplayFact 消费 + 属性重算
// 每帧清空，数量受 active slot period tick + instant spec 限制
// 容量: x1 ≤ 64, InternalBufferCapacity = 64
[InternalBufferCapacity(64)]
public struct AttributeModifierBuffer : IBufferElementData
{
    public int AttributeCode;     // XAttr constant
    public float Magnitude;       // 正=增加, 负=减少
    public Entity SourceAsc;
    public Entity TargetAsc;
    public int SourceEffectCode;
}

// GameplayEventBuffer: 所有 System 产出 → Observation 消费
// 同时写入 replay buffer，不参与 gameplay 决策
// 容量: 大缓冲区, InternalBufferCapacity = 128
[InternalBufferCapacity(128)]
public struct GameplayEventBuffer : IBufferElementData
{
    public int Sequence;
    public int EventCode;
    public int Frame;
    public Entity SourceAsc;
    public Entity TargetAsc;
    public int SourceEffectCode;
    public float Value;
}

// PresentationEventBuffer: Death/CE 产出 → Boundary outbox
// Cue 不决定 gameplay (不变量 6)
// 容量: InternalBufferCapacity = 32
[InternalBufferCapacity(32)]
public struct PresentationEventBuffer : IBufferElementData
{
    public int CueCode;
    public int Frame;
    public Entity SourceAsc;
    public byte PositionCol;
    public byte PositionRow;
}

// ============================================================
// Cue 代码常量 (SourceGenerator 生成到 AutoChessIds.g.cs)
// ============================================================
public static class CueCode
{
    public const int DeathVFX       = 1001;
    public const int ShieldBashVFX  = 1002;
    public const int IceNovaVFX     = 1003;
    public const int PoisonSlashVFX = 1004;
    public const int HealBeamVFX    = 1005;
    public const int AttackVFX      = 1006;
}

// ============================================================
// EndGASStructuralCommitECBSystem: 结构变化唯一播放点
// 符合不变量 21: hot path 禁止直接结构变化
// 符合 ECB-01: ECB 集中延迟到指定 SystemGroup 播放
// ============================================================
[UpdateInGroup(typeof(GASStructuralCommitSystemGroup), OrderLast = true)]
public partial class EndGASStructuralCommitECBSystem : EntityCommandBufferSystem { }

// Chunk Component: chunk 级跳过优化（不变量 37 的前置条件）
// 当 chunk 内所有 entity 都没有 active effect slot 时标记
public struct NoActiveEffectsChunkComponent : IComponentData
{
    // 无字段 — chunk component marker
}
```

> **注**: 以上类型是 Runtime Core 基础设施，不属于 AutoChess 业务特有。它们满足不变量 33（每个 buffer 有 InternalBufferCapacity）、不变量 34（明确 Data/Buffer/Chunk 分类）、不变量 22（buffer 有容量、生命周期、清空策略）。

---
