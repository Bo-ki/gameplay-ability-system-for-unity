# Fan-In、Debugger、SourceGenerator 权限 Spec

## 结论

Fan-In 是 Kernel 内算法；Debugger 是只读证据消费；SourceGenerator 是不可变定义与纯 evaluator 生产者。三者互不代管 lifecycle，也不能因为“方便生成/观察”突破 Core owner。

## Fan-In

- producer 写 Kernel tick scratch segment。
- merge 按 target stable id + canonical key 形成 owner-local range。
- TargetOwned Apply 只写当前 ASC。
- 输出 counters：segments、commands、target groups、sort/merge cost、scratch high-water、spill/fault、hash。

具体 `NativeStream`、list、prefix sum 或 radix sort 由 ScaleProfile 选择，不成为 public interface。禁止 singleton global buffer、managed list 和跨 owner random write 作为默认路线。

## Runtime Debugger

Debugger 只接收：

- Kernel/Drain 导出的结构化 counters snapshot；
- immutable Boundary batch；
- ReadModel snapshot；
- Profiler/Journaling capture 的引用与运行参数。

Debugger 不拥有 gameplay query、type handle、ECB、allocator、Core buffer cursor 或 mutation。日志/图表是 evidence 的派生格式，不是 evidence source。

最低证据族：Core/Drain/managed consumer timing split、job/sync、scratch/slot/buffer pressure、structural playback、stale/reject/fault、stabilization iterations、reaction latency、deterministic hashes 与 generated artifact scan。

## SourceGenerator

允许生成：ID、Blob schema、lookup、pre-resolved ranges、pure requirement/magnitude/target evaluators、CaptureProjectionContract、DirectEffectProgram、dependency graph、Editor metadata、validation 和规模报告。

禁止生成：

- `ISystem`、`OnUpdate`、system registration；
- query、lookup refresh owner、ECB、EntityManager write；
- NativeContainer allocation/lifetime；
- Ability/Effect/Tag/Attribute/Cue lifecycle；
- definition backend selector、fallback Runtime；
- 未声明的动态 Capture/Execution 逃生 API。

## 交叉验收

- Generated artifact 扫描零 lifecycle 权限。
- 关闭 Debugger/Presentation 后 Core 结果和 hash 不变。
- 更换内部 fan-in 算法但保持 contract 时，语义 trace/hash 不变。
- 所有 scale/overflow 结论可由机器可读 evidence 重现，而不是日志文本推断。
