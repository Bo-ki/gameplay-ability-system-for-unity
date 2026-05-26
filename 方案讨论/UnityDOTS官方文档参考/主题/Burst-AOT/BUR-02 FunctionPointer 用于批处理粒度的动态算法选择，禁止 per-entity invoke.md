# BUR-02: FunctionPointer 用于批处理粒度的动态算法选择，禁止 per-entity invoke

**严重度**: P1
**Primary Owner**: Burst-AOT
**来源**: `09-Burst-编译-向量化-AOT.md` — FunctionPointer

## 规则声明
FunctionPointer 只能用于同类型 entity 的 chunk 级批处理调用。禁止在 IJobEntity 的 per-entity Execute 中逐 entity invoke FunctionPointer。

## 为什么
Per-entity invoke 造成标量调用，失去自动向量化机会，性能退化为与虚方法调用同级别。批处理 job 往往比 FunctionPointer 更简单更快。

## EX-GAS 诊断
ExecutionCalculation 的 FunctionPointer 使用必须遵循"一类计算方式一个 FunctionPointer，同类的 entity chunk 批处理"。不能每个 Ability 一个 FunctionPointer + per-entity invoke。

## 检查方法
审查 ExecutionCalculation job 中 FunctionPointer.Invoke 的调用位置（IJobChunk.Execute 内批量优于 IJobEntity.Execute 内逐个）。
