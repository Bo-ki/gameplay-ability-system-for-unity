# PRF-23: 禁止从 Job 内部启动新 Job

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — PRF-13 节 + PRF-23 节；`common-errors.md`

## 规则声明
禁止在任意 job 的执行上下文（包括 `IJobEntity.Execute`、`IJobChunk.Execute`、`IJobParallelFor.Execute`、`Entities.ForEach` lambda）中启动新的 job（`.Schedule()` 或 `.Run()`）。此约束适用于所有 job 类型和所有执行频率。

## 为什么
嵌套 job 的 safety handle 未正确建立 → 竞态条件无法被检测。编译器不报错，运行时可能偶然正确 → 隐蔽竞态 bug。官方文档明确指出："Launching jobs from jobs is not currently supported, and the resulting safety handles won't be set up correctly."

## EX-GAS 诊断
搜索 `.Schedule(` 或 `.Run(` 出现在 `void Execute(` 或 lambda `() =>` 内部的模式。工具和 Debugger 代码中的 job 嵌套调用同样需要检查。

## 检查方法
- Grep 搜索嵌套 `.Schedule()`/`.Run()` 模式
- 全局搜索 `\.Schedule\(` 和 `\.Run\(` 出现在 job 上下文内的情况
- 所有 job 类型和执行频率均适用
