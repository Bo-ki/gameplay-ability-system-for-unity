# 16-03：Fan-in、Debugger Evidence 与 SourceGenerator Pure Glue

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分` | 状态：目标态 Spec 子页 | 最近拆分：2026-06-07

本文件只描述 Effect fan-in、Debugger evidence projection 和 SourceGenerator pure glue 的目标代码骨架。

## Evidence Tier 与 MigrationProofOnly 退出门

目标态必须把 evidence 分为五类，禁止用低等级证据替代高等级证据：

| Evidence tier | 含义 | 可证明 | 不可证明 |
|---|---|---|---|
| Contract / Plan | 目标约束、phase contract、stream owner plan、validation gate 定义 | 设计期约束存在 | runtime 已按约束执行 |
| Runtime Counter | System / lane / carrier / lookup / buffer / structural counter | 当前运行中有可读计数 | 官方 Profiler / Journaling 已捕获 |
| Official Capture | Entities Journaling / Profiler / PackageCache 官方规则对照 | 结构变化、record type、系统热点或 disabled reason | GAS 业务语义正确 |
| Validation Evidence | AutoChess / scenario / scale gate 的结构化验收字段 | 业务链路和门槛是否满足 | 所有 Runtime Core 任务都完成 |
| Derived Export | 字符串日志、Mermaid 图、中文 summary、HTML/Markdown 报告 | 人类阅读和复盘 | 机器验收源或性能完成证明 |

`MigrationProofOnly` 的退出门必须同时满足：

1. carrier / lifecycle / query / ECB / NativeContainer owner 已迁入手写 Runtime Core owner 或纯 generated glue。
2. Runtime Counter 能解释 owner、phase、capacity、reselect trigger、random lookup、merge cost 和 dependency chain。
3. Official Capture 能说明 captured / disabled / unsupported reason，不能只输出空字符串或图表。
4. Validation Evidence 能复现业务场景、scale profile、battle hash / summary hash、blocking error 和 owner 分类。
5. Derived Export 只能从结构化 evidence 派生，不能成为唯一 source。

缺任一项时，目标态 Spec 只能把该链路标为 proof / migration，不得把它写成 scale-ready。

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
