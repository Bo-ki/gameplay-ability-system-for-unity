# 10B-04：核心 System 的 Command、技能激活与 Effect Fan-In

> Owner：`01-目标态架构共识/10B-AutoChess完整业务案例` | 状态：目标态子 Spec | 拆分来源：`../10B-AutoChess完整业务案例设计Spec.md` | 最近拆分：2026-06-07

本文件只描述 AutoChess 完整业务案例的目标态设计。禁止写入当前代码事实、执行流水、验证数字或下一步任务；现实证据必须回到 `../../00-当前架构事实/`，任务拆分必须回到 `../../02-主线任务树/`。

## 与 Runtime Core 通用 Spec 的合并裁决

本文件是 AutoChess 业务案例投影，不是通用 Runtime Core 规则的第二正文。出现规则重复或冲突时，按下表回到唯一 owner 修改，10B 只同步业务字段和案例代码。

| 主题 | 唯一正文 owner | 本文件只保留 |
|---|---|---|
| Shell intent / Boundary command envelope | [16-02 Boundary Command 与 Core Command Resolve](../16-纯ECS内核与边界重划分/16-02-BoundaryCommand与CoreCommandResolveSpec.md) | AutoChess 输入如何映射为 ability / effect command |
| Ability command normalization、Target Resolve、TargetDataBuffer / NativeStream 选型 | [03D Command Resolve 与 Target Resolve](../03-RuntimeCore管线/03D-CommandResolve与TargetResolveSpec.md) | 棋子 mana、cooldown、stun/freeze、ability level 等业务校验字段 |
| Effect Fan-In、deterministic merge、target grouped range | [03E-01 Effect Fan-In](../03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-Fact/03E-01-EffectFanInSpec.md) | 普攻、技能和 duration GE 在 AutoChess 场景中的 producer / range 样例 |
| Frame-local stream / range / capacity 基础设施 | [10B-03B Runtime 基础设施](10B-03B-Runtime基础设施Spec.md) 与 [13-03 Buffer 容量与 Phase 映射](../13-EntityComponent物理布局/13-03-Buffer容量与Phase映射Spec.md) | AutoChess 所需 lane 名称、字段映射和验收样例 |

## 八、核心 System 实现

> **代码读取方式：** 本章代码表达目标态 AutoChess 业务链路的投影：frame command / spec / fact / mutation 默认通过 `NativeStream` producer、deterministic merge 和 target grouped range 流转；结构变化只输出 structural intent，并统一在 `GASStructuralCommitSystemGroup` 对应 ECB phase 播放。通用 Runtime Core 规则以上表 owner 为准，本文只展示 AutoChess 字段如何套入这些规则。

### 8.1 普攻 System（GASCoreSimulationSystemGroup / Effect Fan-In lane）

```csharp
// ============================================================
// [Layer 3: GAS Runtime Core — GASCoreSimulationSystemGroup]
// 普攻 System：冷却计时 → 选敌 → 发射 Instant GE command
// ============================================================

[UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
[BurstCompile]
public partial struct GEAutoAttackSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CChessCombat>();
        state.RequireForUpdate<EffectFanInLaneStateComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 1. 法力自然回复
        var manaRegenJob = new ManaRegenTickJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = manaRegenJob.ScheduleParallel(state.Dependency);

        // 2. 普攻冷却 tick + 攻击检测 → 写入 EffectCommand NativeStream lane
        var attackJob = new AutoAttackTickJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Commands = GASEffectCommandSink.Resolve(ref state).AsParallelWriter(),
        };
        state.Dependency = attackJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct ManaRegenTickJob : IJobEntity
{
    public float DeltaTime;

    public void Execute(
        ref AutoChessResourceAttributeCurrentSetComponent resource,
        in AutoChessResourceAttributeBaseSetComponent resourceBase,
        ref AutoChessAttributeDirtyMaskComponent dirty,
        in TagMaskComponent tagMask)
    {
        if (TagCheck.IsDead(tagMask.Value)) return;
        var oldMana = resource.Mana;
        resource.Mana = math.min(resource.Mana + resourceBase.ManaRegen * DeltaTime, resourceBase.MaxMana);
        if (resource.Mana != oldMana)
            dirty.ResourceWord |= 1ul << 0;
    }
}

[BurstCompile]
public partial struct AutoAttackTickJob : IJobEntity
{
    public float DeltaTime;
    public GASEffectCommandSink.ParallelWriter Commands;

    public void Execute(
        [EntityIndexInChunk] int chunkIndex,
        Entity attacker,
        ref CChessCombat combat,
        in CChessUnit unit,
        in TagMaskComponent tagMask,
        in AutoChessCombatAttributeCurrentSetComponent combatAttributes)
    {
        if (!unit.IsAlive) return;
        if (TagCheck.IsStunnedOrFrozen(tagMask.Value)) return;

        combat.AttackCooldownRemaining -= DeltaTime;

        if (combat.AttackCooldownRemaining <= 0f && combat.CurrentTarget != Entity.Null)
        {
            // 重置普攻冷却: interval = 1.0 / (ASPD/100)
            combat.AttackCooldownRemaining = 1.0f / (combatAttributes.AttackSpeed / 100f);

            // 发射普攻 EffectCommand (GE 4001 = 普攻伤害)
            // SourceAsc = 攻击者自身(entity), TargetAsc = 当前目标
            Commands.Write(chunkIndex, new GEEffectCommandBuffer
            {
                EffectCode = 4001,
                SourceAsc = attacker,
                TargetAsc = combat.CurrentTarget,
                ContextId = 0,
            });
        }
    }
}
```

### 8.2 技能激活 System（AutoChess 对 03D Command Resolve 的业务投影）

```csharp
// ============================================================
// [Layer 3: GAS Runtime Core — GASCommandResolveSystemGroup]
// 技能激活检查：法力足够 + 冷却完毕 + 未眩晕/冰冻 → 发射 EffectCommand
//
// CASE-02 (IJobEntity) — 拒绝 CASE-01 (SystemAPI.Query)
// 原因: 若本案例选择低频 request-owned 物化路径，也必须保持 chunk/job 化；
//       默认 Shell intent 入口和高频 command 仍以 16-02 / 03D 的 owner-local / NativeStream 路径为准。
// ECB: 使用 GASStructuralCommitSystemGroup ECB (EndGASStructuralCommitECB) 统一播放
// ============================================================

[UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
[BurstCompile]
public partial struct AbilityActivationSystem : ISystem
{
    private EntityQuery _requestQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _requestQuery = state.GetEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<AbilityActivationRequestComponent>(),
                ComponentType.ReadWrite<AbilityCommandComponent>(),
                ComponentType.ReadWrite<TargetDataBuffer>()
            }
        });
        state.RequireForUpdate(_requestQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var abilityLookup = SystemAPI.GetSingleton<GAStaticLookup>(); // Ability lookup 单例（BlobAsset 表）

        var ingestJob = new AbilityActivationIngestJob
        {
            AbilityLookup = abilityLookup,
            AbilityStates = state.GetComponentLookup<AbilityStateComponent>(true),
            SourceTags = state.GetComponentLookup<TagMaskComponent>(true),
            SourceResources = state.GetComponentLookup<AutoChessResourceAttributeCurrentSetComponent>(true),
            RequestType = state.GetComponentTypeHandle<AbilityActivationRequestComponent>(true),
            CommandType = state.GetComponentTypeHandle<AbilityCommandComponent>(false),
            Frame = (int)(SystemAPI.Time.ElapsedTime * 60)
        };
        state.Dependency = ingestJob.ScheduleParallel(_requestQuery, state.Dependency);
    }
}

[BurstCompile]
public struct AbilityActivationIngestJob : IJobChunk
{
    [ReadOnly] public GAStaticLookup AbilityLookup;
    [ReadOnly] public ComponentLookup<AbilityStateComponent> AbilityStates;
    [ReadOnly] public ComponentLookup<TagMaskComponent> SourceTags;
    [ReadOnly] public ComponentLookup<AutoChessResourceAttributeCurrentSetComponent> SourceResources;
    [ReadOnly] public ComponentTypeHandle<AbilityActivationRequestComponent> RequestType;
    public ComponentTypeHandle<AbilityCommandComponent> CommandType;
    public int Frame;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
    {
        Unity.Assertions.Assert.IsFalse(useEnabledMask);
        var requests = chunk.GetNativeArray(ref RequestType);
        var commands = chunk.GetNativeArray(ref CommandType);

        for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
        {
            var request = requests[entityIndex];
            var command = new AbilityCommandComponent
            {
                SourceAsc = request.SourceAsc,
                AbilityEntity = request.AbilityEntity,
                ExplicitTargetAsc = request.ExplicitTargetAsc,
                InputSequence = request.InputSequence,
                RequestFrame = request.RequestFrame,
                TargetGroupSortKey = request.TargetGroupSortKey,
                TargetMode = request.TargetMode,
                Status = AbilityCommandStatus.Rejected
            };

            if (!AbilityStates.HasComponent(request.AbilityEntity) ||
                !SourceTags.HasComponent(request.SourceAsc) ||
                !SourceResources.HasComponent(request.SourceAsc))
            {
                commands[entityIndex] = command;
                continue;
            }

            var ability = AbilityStates[request.AbilityEntity];
            var tagMask = SourceTags[request.SourceAsc];
            var resource = SourceResources[request.SourceAsc];
            var abilityDef = AbilityLookup.FindAbility(ability.AbilityId);

            if (!abilityDef.IsCreated ||
                ability.OwnerAsc != request.SourceAsc ||
                ability.State != AbilityRuntimeState.Ready ||
                ability.CooldownEndFrame > Frame ||
                TagCheck.IsStunnedOrFrozen(tagMask.Value))
            {
                commands[entityIndex] = command;
                continue;
            }

            ref var def = ref abilityDef.Value;
            if (resource.Mana < def.ManaCost)
            {
                commands[entityIndex] = command;
                continue;
            }

            command.PrimaryGameplayEffectCode = def.EffectIdPrimary;
            command.SecondaryGameplayEffectCode = def.EffectIdSecondary;
            command.Level = ability.Level;
            command.Status = AbilityCommandStatus.Valid;
            commands[entityIndex] = command;
        }
    }
}
```

### 8.3 Effect Fan-In System（GASCoreSimulationSystemGroup / Effect Fan-In lane）

```csharp
// ============================================================
// [Layer 3: GAS Runtime Core — GASCoreSimulationSystemGroup]
// Effect Fan-In: 读取 EffectCommand NativeStream → 查 GE BlobAsset → 分别产出:
//   Instant GE → InstantSpec NativeStream / target grouped range
//   Duration GE → ActiveEffectMutation NativeStream / target grouped range
//
// CASE-02 (IJobEntity) — 拒绝 CASE-01 (SystemAPI.Query foreach)
// 原因: GEEffectCommand 数量在 AoE / period / passive 场景可达数十到数百。
//       目标态不把 frame command/fact 写入 singleton DynamicBuffer，也不把 ECB 当 event bus。
// 实现: producer lane 写 NativeStream；Fan-In lane 只读 frame stream，
//       输出 instant spec 和 active mutation 的 frame-local stream，
//       后续 deterministic merge 生成 target grouped range。
// ============================================================

[UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
[BurstCompile]
public partial struct GEEffectFanInSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EffectFanInLaneStateComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var laneState = SystemAPI.GetSingleton<EffectFanInLaneStateComponent>();
        var commandReader = GASEffectCommandReader.Resolve(ref state);
        if (commandReader.CommandCount == 0)
            return;

        var specSink = GASEffectSpecSink.Resolve(ref state);
        var mutationSink = GASActiveEffectMutationSink.Resolve(ref state);

        var buildHandle = new EffectFanInBuildJob
        {
            CommandReader = commandReader.AsNativeStreamReader(),
            GeLookup = SystemAPI.GetSingleton<GAStaticLookup>(),
            SpecWriter = specSink.AsNativeStreamWriter(),
            MutationWriter = mutationSink.AsNativeStreamWriter(),
        }.Schedule(commandReader.ForEachCount, 1, state.Dependency);

        state.Dependency = new EffectFanInDeterministicMergeJob
        {
            SpecReader = specSink.AsNativeStreamReader(),
            MutationReader = mutationSink.AsNativeStreamReader(),
            ForEachCount = commandReader.ForEachCount,
            Frame = laneState.Frame,
            RangeSink = GASEffectFanInRangeSink.Resolve(ref state),
        }.Schedule(buildHandle);
    }
}

[BurstCompile]
public struct EffectFanInBuildJob : IJobFor
{
    [ReadOnly] public NativeStream.Reader CommandReader;
    [ReadOnly] public GAStaticLookup GeLookup;
    public NativeStream.Writer SpecWriter;
    public NativeStream.Writer MutationWriter;

    public void Execute(int commandIndex)
    {
        CommandReader.BeginForEachIndex(commandIndex);
        SpecWriter.BeginForEachIndex(commandIndex);
        MutationWriter.BeginForEachIndex(commandIndex);

        int localIndex = 0;
        while (CommandReader.RemainingItemCount > 0)
        {
            var cmd = CommandReader.Read<GEEffectCommandBuffer>();
            var geBlob = GeLookup.Find(cmd.EffectCode);
            if (!geBlob.IsCreated)
                continue;

            ref var ge = ref geBlob.Value;
            int sortKey = commandIndex * 1024 + localIndex++;

            if (ge.GeType == GEType.Instant)
            {
                SpecWriter.Write(new EffectFanInSpecRecord
                {
                    TargetAsc = cmd.TargetAsc,
                    SortKey = sortKey,
                    Spec = new GEEffectSpecBuffer
                    {
                        EffectCode = ge.GeId,
                        ContextId = cmd.ContextId,
                        SourceAsc = cmd.SourceAsc,
                        TargetAsc = cmd.TargetAsc,
                    },
                });
                continue;
            }

            if (ge.GeType == GEType.Duration)
            {
                MutationWriter.Write(new EffectFanInMutationRecord
                {
                    TargetAsc = cmd.TargetAsc,
                    SortKey = sortKey,
                    Mutation = new ActiveEffectMutationBuffer
                    {
                        EffectCode = ge.GeId,
                        SourceAsc = cmd.SourceAsc,
                        TargetAsc = cmd.TargetAsc,
                        DurationFrames = ge.DurationFrames,
                        PeriodFrames = ge.PeriodFrames,
                        StackLimit = ge.StackLimit,
                        StackPolicy = ge.StackPolicy,
                        ContextId = cmd.ContextId,
                    },
                });
            }
        }

        MutationWriter.EndForEachIndex();
        SpecWriter.EndForEachIndex();
        CommandReader.EndForEachIndex();
    }
}

public struct EffectFanInSpecRecord
{
    public Entity TargetAsc;
    public int SortKey;
    public GEEffectSpecBuffer Spec;
}

public struct EffectFanInMutationRecord
{
    public Entity TargetAsc;
    public int SortKey;
    public ActiveEffectMutationBuffer Mutation;
}

[BurstCompile]
public struct EffectFanInDeterministicMergeJob : IJob
{
    [ReadOnly] public NativeStream.Reader SpecReader;
    [ReadOnly] public NativeStream.Reader MutationReader;
    public int ForEachCount;
    public int Frame;
    public GASEffectFanInRangeSink RangeSink;

    public void Execute()
    {
        RangeSink.SpecScratch.Clear();
        RangeSink.MutationScratch.Clear();
        RangeSink.SpecRanges.Clear();
        RangeSink.SpecPayload.Clear();
        RangeSink.MutationRanges.Clear();
        RangeSink.MutationPayload.Clear();

        for (int i = 0; i < ForEachCount; i++)
        {
            SpecReader.BeginForEachIndex(i);
            while (SpecReader.RemainingItemCount > 0)
                RangeSink.SpecScratch.Add(SpecReader.Read<EffectFanInSpecRecord>());
            SpecReader.EndForEachIndex();

            MutationReader.BeginForEachIndex(i);
            while (MutationReader.RemainingItemCount > 0)
                RangeSink.MutationScratch.Add(MutationReader.Read<EffectFanInMutationRecord>());
            MutationReader.EndForEachIndex();
        }

        RangeSink.SpecScratch.Sort(new SpecRecordComparer());
        RangeSink.MutationScratch.Sort(new MutationRecordComparer());

        WriteSpecRanges(RangeSink.SpecScratch, Frame, ref RangeSink);
        WriteMutationRanges(RangeSink.MutationScratch, Frame, ref RangeSink);
    }

    private static void WriteSpecRanges(
        NativeList<EffectFanInSpecRecord> records,
        int frame,
        ref GASEffectFanInRangeSink sink)
    {
        int index = 0;
        while (index < records.Length)
        {
            var owner = records[index].TargetAsc;
            int start = sink.SpecPayload.Length;
            int firstSortKey = records[index].SortKey;

            do
            {
                sink.SpecPayload.Add(records[index].Spec);
                index++;
            }
            while (index < records.Length && SameEntity(records[index].TargetAsc, owner));

            sink.SpecRanges.Add(new EffectCommandRangeHeader
            {
                OwnerAsc = owner,
                Frame = frame,
                Start = start,
                Length = sink.SpecPayload.Length - start,
                SortKey = firstSortKey,
            });
        }
    }

    private static void WriteMutationRanges(
        NativeList<EffectFanInMutationRecord> records,
        int frame,
        ref GASEffectFanInRangeSink sink)
    {
        int index = 0;
        while (index < records.Length)
        {
            var owner = records[index].TargetAsc;
            int start = sink.MutationPayload.Length;
            int firstSortKey = records[index].SortKey;

            do
            {
                sink.MutationPayload.Add(records[index].Mutation);
                index++;
            }
            while (index < records.Length && SameEntity(records[index].TargetAsc, owner));

            sink.MutationRanges.Add(new EffectCommandRangeHeader
            {
                OwnerAsc = owner,
                Frame = frame,
                Start = start,
                Length = sink.MutationPayload.Length - start,
                SortKey = firstSortKey,
            });
        }
    }

    private static bool SameEntity(Entity left, Entity right)
    {
        return left.Index == right.Index && left.Version == right.Version;
    }
}

public struct SpecRecordComparer : IComparer<EffectFanInSpecRecord>
{
    public int Compare(EffectFanInSpecRecord x, EffectFanInSpecRecord y)
    {
        int owner = EffectFanInRecordOrder.CompareEntity(x.TargetAsc, y.TargetAsc);
        return owner != 0 ? owner : x.SortKey.CompareTo(y.SortKey);
    }
}

public struct MutationRecordComparer : IComparer<EffectFanInMutationRecord>
{
    public int Compare(EffectFanInMutationRecord x, EffectFanInMutationRecord y)
    {
        int owner = EffectFanInRecordOrder.CompareEntity(x.TargetAsc, y.TargetAsc);
        return owner != 0 ? owner : x.SortKey.CompareTo(y.SortKey);
    }
}

public static class EffectFanInRecordOrder
{
    public static int CompareEntity(Entity left, Entity right)
    {
        int index = left.Index.CompareTo(right.Index);
        return index != 0 ? index : left.Version.CompareTo(right.Version);
    }
}
```
