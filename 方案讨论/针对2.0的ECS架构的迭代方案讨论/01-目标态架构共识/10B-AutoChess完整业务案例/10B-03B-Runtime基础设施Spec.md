# 10B-03B：Runtime 基础设施 Spec

> Owner：`01-目标态架构共识/10B-AutoChess完整业务案例` | 状态：目标态子 Spec | 来源入口：[10B-03](10B-03-羁绊与Runtime基础设施Spec.md)

## 目的

定义 AutoChess 完整业务案例需要消费的 Runtime Core 通用基础设施形态。这里描述 frame-local stream/range、lane metadata、target grouped range、structural intent、command/request、ASC identity、static lookup 和 buffer capacity 目标，不承载具体羁绊规则。

## 非目标

1. 不把 singleton DynamicBuffer 设计成 command/spec/fact/mutation 总线。
2. 不让业务 System 拥有 NativeStream 生命周期。
3. 不让 SourceGenerator 生成 lifecycle 权威或 System 调度权威。
4. 不在本文件记录当前实现命中、验证数字或迁移 proof。

## 基础设施分层

| 层 | 目标职责 | 示例 |
|---|---|---|
| Lane metadata | 记录 lane frame、sequence、range 预算和 diagnostic key | `EffectFanInLaneStateComponent` |
| Frame-local stream | 承载 command/spec/mutation/fact 的并行写入 | `NativeStream` writer / reader |
| Deterministic merge | 将并行输出合并为稳定顺序和 owner-local range | `TargetAsc + SortKey` merge |
| Target grouped range | 提供 owner-local compact range 只读视图 | `GASTargetGroupedRangeReader<T>` |
| Structural intent | 表达结构变化请求，交给 StructuralCommit | `GASStructuralIntentRecord` |
| Definition lookup | 提供 generated static lookup / blob 查询 | `GAStaticLookup` |

## 核心约束

1. gameplay command、effect spec、active mutation、attribute delta 和 gameplay fact 默认是 frame-local record，不是长期 entity。
2. 需要跨 phase 消费的数据先 merge 成 owner-local compact range，再由 owner chunk applicator 消费。
3. 少量 singleton 只能保存 frame arena / catalog / lane metadata，不承载每帧 gameplay payload。
4. `DynamicBuffer` 只用于明确 owner 的小容量状态或边界 request target data；每个 buffer 必须声明容量策略。
5. `EntityQuery`、lookup 和 singleton fallback 必须有 owner 和预算；不能成为业务 API 的默认承载。

## 目标代码骨架

```csharp
public struct EffectFanInLaneStateComponent : IComponentData
{
    public int Frame;
    public int LastCommandSequence;
    public int MaxCommandRangeLength;
    public int MaxFactRangeLength;
}

public struct EffectCommandRangeHeader
{
    public Entity OwnerAsc;
    public int Frame;
    public int Start;
    public int Length;
    public int SortKey;
}

public struct GASEffectCommandSink
{
    private GASFrameAppendWriter<GEEffectCommandRecord> _writer;

    public ParallelWriter AsParallelWriter()
    {
        return new ParallelWriter { Writer = _writer };
    }

    public struct ParallelWriter
    {
        public GASFrameAppendWriter<GEEffectCommandRecord> Writer;

        public void Write(int sortKey, in GEEffectCommandRecord command)
        {
            Writer.Append(sortKey, command);
        }
    }
}

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
            if (header.OwnerAsc == ownerAsc)
            {
                range = new GASTargetRange(header.Start, header.Length);
                return true;
            }
        }

        range = default;
        return false;
    }
}

public struct GASStructuralIntentRecord
{
    public GASStructuralIntentKind Kind;
    public Entity TargetAsc;
    public int ReasonCode;
    public int SortKey;
}
```

## Command / Request 形态

| 数据 | 目标 owner | 生命周期 | 约束 |
|---|---|---|---|
| `AbilityActivationRequestComponent` | request entity | Boundary 输入到 Core resolve | 低频边界命令；不进入 hot payload 总线 |
| `TargetDataBuffer` | request entity | request 生命周期 | `InternalBufferCapacity` 必填 |
| `AbilityActivationCommandRecord` | frame-local command stream | 当帧 | Core command seed，不直接拥有 entity |
| `AbilityTargetRecord` | frame-local target stream | 当帧 | TargetResolve 输出，按 source / target sort key 稳定 |
| `GEEffectCommandRecord` | frame-local effect command stream | 当帧 | EffectFanIn 输入 |

## Static Lookup 形态

`GAStaticLookup` 是 generated definition 的只读 Runtime 入口。它只能提供 blob / static lookup，不拥有 lifecycle、query、ECB、NativeContainer owner 或 System registration。

```csharp
public struct GAStaticLookup : IComponentData
{
    public BlobAssetReference<UnitLookupBlob> UnitLookup;
    public BlobAssetReference<AbilityLookupBlob> AbilityLookup;
    public BlobAssetReference<GELookupBlob> GELookup;
}
```

## Buffer 容量策略

| Buffer | Owner | Internal capacity 口径 | 目标用途 |
|---|---|---:|---|
| `TargetDataBuffer` | Request entity | 4 | 显式目标 / small AoE target list |
| `ActiveGameplayEffectBuffer` | ASC entity | 8 | owner-local active slots |
| `AttributeSetBuffer` | ASC entity | 16-32 | owner-local attribute state |
| `GrantedAbilityBuffer` | ASC entity | 8 | owner-local granted ability handles |
| `PresentationEventBuffer` | boundary outbox owner | 16-32 | 表现层只读派生 |

每个 buffer 的容量来自业务上界和 scale profile，不允许因为迁移方便设置为无界容器。

## 禁止方向

1. 禁止把 `GEEffectCommandBuffer`、`GEEffectSpecBuffer`、`AttributeModifierBuffer`、`ActiveEffectMutationBuffer` 或 `GameplayEventBuffer` 写成全局 singleton payload owner。
2. 禁止业务 System 直接持有 `NativeStream.Writer.BeginForEachIndex` 生命周期；必须通过 frame arena helper 或明确 owner。
3. 禁止用 ECB 充当事件总线；ECB 只消费 structural intent。
4. 禁止 Debugger、Mermaid export 或中文日志反写 gameplay truth。
5. 禁止 generated glue 生成 Runtime lifecycle 权威；generated glue 只服务 static lookup、validation graph、pure resolver 和 contract。

## 验收门槛

1. 任一 frame-local payload 都能说明 owner、生命周期、merge 顺序和 disposal owner。
2. 任一 owner-local range 都能被 chunk applicator 消费，不依赖随机 lookup 遍历目标。
3. 任一 buffer 都有 capacity 口径和越界处理策略。
4. structural intent 能被 StructuralCommit 统一播放，并能在 Debugger evidence 中对账。
5. 业务案例新增机制时，只引用本文件的基础设施接口，不复制 Runtime 基础设施正文。
