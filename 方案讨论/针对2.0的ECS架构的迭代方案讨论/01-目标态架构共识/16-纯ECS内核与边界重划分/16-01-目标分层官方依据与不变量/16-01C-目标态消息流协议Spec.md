# 16-01C：目标态消息流协议

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分/16-01` | 状态：目标态 Spec 子页 | 拆分来源：`../16-01-目标分层官方依据与不变量Spec.md` | 最近拆分：2026-06-08

本文件只描述目标态 Shell / Boundary / Core / Debugger / SourceGenerator 的消息流协议。完整端到端代码骨架由 [`../16-06-端到端消息流代码骨架/16-06A-完整端到端消息流代码骨架Spec.md`](../16-06-端到端消息流代码骨架/16-06A-完整端到端消息流代码骨架Spec.md) 维护；[`16-01D`](16-01D-最小完整代码骨架Spec.md) 只维护最小完整判定门。

## 目标态消息流协议

目标态的核心不是把 OOP API 换个名字，而是让每个 Module 的 Interface 比 Implementation 更窄。以下协议用于审查所有 Shell / Boundary / Core / Debugger / SourceGenerator 代码样例：业务层只能发 intent，Boundary 只能落 command / snapshot / evidence，Core 只能处理 ECS data，生成层只能提供 immutable definition 与 pure function。

```csharp
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.TargetSpec
{
    public readonly struct GASIntent
    {
        public readonly GASRuntimeSessionId Session;
        public readonly ASCTargetRef Source;
        public readonly ASCTargetRef Target;
        public readonly int AbilityCode;
        public readonly int GameplayEffectCode;
        public readonly float SetByCallerMagnitude;
    }

    public readonly struct GASIntentReject
    {
        public readonly int ReasonCode;
        public readonly GASRequestId RequestId;
    }

    public struct GASBoundaryCommandRecord : IBufferElementData
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int AbilityCode;
        public int GameplayEffectCode;
        public float SetByCallerMagnitude;
        public int Frame;
        public int Sequence;
    }

    public struct GASTypedFactRecord
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int Domain;
        public int Category;
        public int EventCode;
        public float OldValue;
        public float NewValue;
        public int Frame;
        public int Sequence;
    }

    public struct GASBoundarySnapshotRecord
    {
        public ASCTargetRef Target;
        public GASSnapshotVersion Version;
        public float Health;
        public float Energy;
        public int TagMaskLow;
    }

    public struct GASDiagnosticsEvidenceRecord
    {
        public GASSnapshotVersion Version;
        public int CoreCommandCount;
        public int CoreSpecCount;
        public int CoreDeltaCount;
        public int CoreFactCount;
        public int StructuralPlaybackCount;
        public int ProofOnlyCarrierCount;
        public int RandomLookupCount;
        public int NativeStreamSegmentCount;
        public int MergeCostMicroseconds;
    }

    public interface IGASApplicationShell
    {
        GASRequestId Send(in GASIntent intent, out GASIntentReject reject);
        bool TryRead(in ASCTargetRef target, GASSnapshotVersion minVersion, out GASBoundarySnapshotRecord snapshot);
        GASRuntimeEvidenceSnapshot CaptureEvidence();
    }

    internal interface IGASBoundaryWriter
    {
        GASRequestId AppendCommand(in GASIntent intent);
    }

    internal interface IGASBoundaryReader
    {
        bool TryProjectSnapshot(in ASCTargetRef target, GASSnapshotVersion minVersion, out GASBoundarySnapshotRecord snapshot);
    }

    internal interface IGASCoreLaneOwner
    {
        void DeclareQuery(ref SystemState state);
        void Schedule(ref SystemState state, BlobAssetReference<GASDefinitionCatalogBlob> catalog);
    }

    internal interface IGASDerivedExport
    {
        FixedString512Bytes Format(in GASDiagnosticsEvidenceRecord evidence);
    }
}
```

协议约束：

1. `IGASApplicationShell` 不返回 `World`、`EntityManager`、raw `Entity`、`EntityQuery` 或 writable buffer。
2. `IGASBoundaryWriter` 和 `IGASBoundaryReader` 分离；写命令不能同步读 live state，读 snapshot 不能写 command。
3. `IGASCoreLaneOwner` 的 query 与 schedule 只在 Core implementation 内可见；如果 lane 需要 allocator、lookup、carrier 或 ECB，它们必须在 lane owner 内声明。
4. `GASDiagnosticsEvidenceRecord` 是 Debugger 的一等输出；`IGASDerivedExport` 只能从 evidence 派生字符串或 UI 文本。
5. SourceGenerator 只能帮助填充 `GASDefinitionCatalogBlob`、lookup、pure evaluator 和 validation metadata；不得实现 `IGASCoreLaneOwner`。
