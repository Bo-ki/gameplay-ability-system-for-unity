# 16-03：Fan-in、Debugger Evidence 与 SourceGenerator Pure Glue

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分` | 状态：目标态 Spec 子页 | 最近拆分：2026-06-07

本文件只描述 Effect fan-in、Debugger evidence projection 和 SourceGenerator pure glue 的目标代码骨架。

## 目标代码骨架（续）

### 3. Fan-in 默认 NativeStream + deterministic merge

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    public struct GASEffectCommandSortKey : System.IComparable<GASEffectCommandSortKey>
    {
        public int TargetSortKey;
        public int Sequence;
        public int ProducerIndex;
        public int LocalIndex;

        public int CompareTo(GASEffectCommandSortKey other)
        {
            var target = TargetSortKey.CompareTo(other.TargetSortKey);
            if (target != 0) return target;
            var sequence = Sequence.CompareTo(other.Sequence);
            if (sequence != 0) return sequence;
            var producer = ProducerIndex.CompareTo(other.ProducerIndex);
            if (producer != 0) return producer;
            return LocalIndex.CompareTo(other.LocalIndex);
        }
    }

    public struct GASSortedEffectCommand
    {
        public GASEffectCommandSortKey SortKey;
        public GASEffectCommandRecord Command;
    }

    [BurstCompile]
    public struct GASEffectCommandMergeJob : IJob
    {
        public NativeStream.Reader Reader;
        public NativeList<GASSortedEffectCommand> SortedCommands;

        public void Execute()
        {
            SortedCommands.Clear();

            for (var streamIndex = 0; streamIndex < Reader.ForEachCount; streamIndex++)
            {
                Reader.BeginForEachIndex(streamIndex);
                var localIndex = 0;
                while (Reader.RemainingItemCount > 0)
                {
                    var command = Reader.Read<GASEffectCommandRecord>();
                    SortedCommands.Add(new GASSortedEffectCommand
                    {
                        SortKey = new GASEffectCommandSortKey
                        {
                            TargetSortKey = command.TargetAsc.Index,
                            Sequence = command.Sequence,
                            ProducerIndex = command.ProducerIndex,
                            LocalIndex = localIndex++,
                        },
                        Command = command,
                    });
                }
                Reader.EndForEachIndex();
            }

            SortedCommands.Sort(new SortComparer());
        }

        private struct SortComparer : IComparer<GASSortedEffectCommand>
        {
            public int Compare(GASSortedEffectCommand x, GASSortedEffectCommand y)
            {
                return x.SortKey.CompareTo(y.SortKey);
            }
        }
    }
}
```

解释：影响 battle hash 的 fan-in 不能依赖 worker 写入顺序。`NativeStream` 只解决并行写入，deterministic merge 才解决 GAS 时序和 replay。singleton DynamicBuffer 可以作为 proof，但目标态必须给出 sort key、merge cost、allocator owner 和 reselect trigger。

### 4. Debugger 只消费 counters / facts

```csharp
using Unity.Entities;

namespace GAS.Runtime
{
    public struct GASRuntimeEvidenceCounters : IComponentData
    {
        public int CommandCount;
        public int SpecCount;
        public int DeltaCount;
        public int FactCount;
        public int StructuralPlaybackCount;
        public int RandomLookupCount;
        public int GlobalBufferPressure;
        public int ProofOnlyApiMask;
        public int ReselectTriggerMask;
    }

    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    public partial struct GASRuntimeEvidenceProjectionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GASRuntimeEvidenceCounters>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // 只读 Core counters / facts，写 Boundary snapshot。
            // 不修改 Attribute / Ability / ActiveEffect / Command buffer。
        }
    }
}
```

解释：Debugger 位于 BoundaryProjection。它的输出是 evidence，不是 runtime 控制输入。日志字符串、Mermaid 图和战报都应从 snapshot 派生，而不是热路径直接拼接。

### 5. SourceGenerator 只生成 pure glue

```csharp
using Unity.Entities;

namespace GAS.Runtime.Generated
{
    public readonly struct GASGeneratedAbilityPlan
    {
        public readonly int AbilityIndex;
        public readonly int CostEffectCode;
        public readonly int CooldownEffectCode;
        public readonly int TargetRuleCode;

        public GASGeneratedAbilityPlan(
            int abilityIndex,
            int costEffectCode,
            int cooldownEffectCode,
            int targetRuleCode)
        {
            AbilityIndex = abilityIndex;
            CostEffectCode = costEffectCode;
            CooldownEffectCode = cooldownEffectCode;
            TargetRuleCode = targetRuleCode;
        }
    }

    public static class GASGeneratedDefinitionGlue
    {
        public static bool TryResolveAbilityPlan(
            ref GASDefinitionCatalogBlob catalog,
            int abilityCode,
            out GASGeneratedAbilityPlan plan)
        {
            plan = default;
            if (!GASGeneratedDefinitionCatalogLookup.TryGetAbilityIndex(
                    ref catalog,
                    abilityCode,
                    out var abilityIndex))
            {
                return false;
            }

            ref readonly var ability = ref GASGeneratedDefinitionCatalogLookup.GetAbility(
                ref catalog,
                abilityIndex);

            plan = new GASGeneratedAbilityPlan(
                abilityIndex,
                ability.CostGameplayEffectCode,
                ability.CooldownGameplayEffectCode,
                ability.TargetRuleCode);
            return true;
        }
    }
}
```

解释：generated glue 只做 code -> index -> immutable definition -> frame-local record。它不拥有 `ISystem`、query、ECB、NativeContainer、`EntityManager` 或生命周期。

### 6. Magnitude Source Snapshot Lane

```csharp
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public enum GASMagnitudeSnapshotTiming : byte
    {
        Current = 0,
        CapturedOnApply = 1,
        CapturedOnTick = 2,
        CapturedBeforeExecution = 3,
    }

    public struct GASMagnitudeSnapshotKey : System.IEquatable<GASMagnitudeSnapshotKey>
    {
        public Entity Owner;
        public int AttributeSetCode;
        public int AttributeCode;
        public GASMagnitudeSnapshotTiming Timing;

        public bool Equals(GASMagnitudeSnapshotKey other)
        {
            return Owner == other.Owner
                && AttributeSetCode == other.AttributeSetCode
                && AttributeCode == other.AttributeCode
                && Timing == other.Timing;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Owner.GetHashCode();
                hash = (hash * 397) ^ AttributeSetCode;
                hash = (hash * 397) ^ AttributeCode;
                hash = (hash * 397) ^ (int)Timing;
                return hash;
            }
        }
    }

    public struct GASActiveEffectSlotMagnitudeSnapshotKey
        : System.IEquatable<GASActiveEffectSlotMagnitudeSnapshotKey>
    {
        public Entity TargetOwner;
        public int SlotSequence;
        public int ModifierIndex;
        public GASMagnitudeSnapshotTiming Timing;

        public bool Equals(GASActiveEffectSlotMagnitudeSnapshotKey other)
        {
            return TargetOwner == other.TargetOwner
                && SlotSequence == other.SlotSequence
                && ModifierIndex == other.ModifierIndex
                && Timing == other.Timing;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TargetOwner.GetHashCode();
                hash = (hash * 397) ^ SlotSequence;
                hash = (hash * 397) ^ ModifierIndex;
                hash = (hash * 397) ^ (int)Timing;
                return hash;
            }
        }
    }

    public struct GASMagnitudeSourceSnapshotRecord
    {
        public GASMagnitudeSnapshotKey Key;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int SourceCommandSequence;
        public int SourceSpecSequence;
        public float Value;
    }

    public struct GASMagnitudeInputRecord
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int AttributeSetCode;
        public int AttributeCode;
        public GASMagnitudeSnapshotTiming Timing;
        public float FallbackValue;
    }

    [BurstCompile]
    public struct GASMagnitudeSourceSnapshotResolveJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle EntityType;
        [ReadOnly] public BufferTypeHandle<AttributeValueBuffer> AttributeValuesType;
        public NativeParallelHashMap<GASMagnitudeSnapshotKey, GASMagnitudeSourceSnapshotRecord>.ParallelWriter Snapshots;

        public void Execute(
            in ArchetypeChunk chunk,
            int unfilteredChunkIndex,
            bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(EntityType);
            var attributesAccessor = chunk.GetBufferAccessor(ref AttributeValuesType);

            for (var i = 0; i < chunk.Count; i++)
            {
                var owner = entities[i];
                var attributes = attributesAccessor[i];
                for (var attrIndex = 0; attrIndex < attributes.Length; attrIndex++)
                {
                    var attr = attributes[attrIndex];
                    var key = new GASMagnitudeSnapshotKey
                    {
                        Owner = owner,
                        AttributeSetCode = attr.AttrSetCode,
                        AttributeCode = attr.Code,
                        Timing = GASMagnitudeSnapshotTiming.CapturedBeforeExecution,
                    };

                    Snapshots.TryAdd(key, new GASMagnitudeSourceSnapshotRecord
                    {
                        Key = key,
                        SourceAsc = owner,
                        TargetAsc = owner,
                        Value = attr.CurrentValue,
                    });
                }
            }
        }
    }
}
```

解释：SourceAttribute / TargetAttribute 不允许在 evaluator、generated lifecycle 或 active effect tick 中临时打开跨 owner lookup。目标态必须先由明确的 snapshot lane 采集需要的 source / target attribute 值，再由 magnitude evaluator 消费 snapshot record。`CurrentValue`、apply-time capture、tick-time capture 和 execution-before capture 是不同 timing，必须进入 key / counter / Debugger evidence；缺失 snapshot 只能走显式 fallback fact，不能静默读取 live owner。

active effect slot 的 sequence 是 target owner 内局部序号，不是全局唯一 id。slot tick snapshot key 必须至少包含 target owner、slot sequence、modifier index 和 timing；只用 slot sequence，或只用 slot sequence + modifier index，都会在多 owner 并行 tick 时产生碰撞风险。slot tick 的容量估算必须来自 definition catalog 中相关 modifier 上界，并输出 capacity pressure / spill / lane-specific hit-miss-fallback evidence。

SourceGenerator 在该 lane 的目标职责只包括生成 key 类型、static lookup、pure evaluator glue、catalog metadata 和 validation graph。snapshot gather / apply lifecycle、query owner、NativeContainer allocator owner、dependency owner 和 Debugger counter 写入 owner 必须属于手写 Runtime Core lane；任何 generated lifecycle 只能作为迁移证明形态，并绑定退出门。
