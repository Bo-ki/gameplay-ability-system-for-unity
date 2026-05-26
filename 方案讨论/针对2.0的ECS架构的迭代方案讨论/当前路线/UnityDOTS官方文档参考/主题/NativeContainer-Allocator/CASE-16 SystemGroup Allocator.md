# CASE-16: SystemGroup Allocator

**Primary Owner**: NativeContainer-Allocator
**来源**: NativeContainer-Allocator.md / DocCodeSamples
**关联规则**: NAT-01, NAT-04

## 使用场景
为一个 SystemGroup 内的多个 system 提供共享的 per-frame scratch allocator，避免每个 system 各自分配临时内存。

## 模式描述
通过 `SetRateManagerCreateAllocator` + `DoubleRewindableAllocators` + `SetGroupAllocator` 为 SystemGroup 建立 per-group scratch allocator。

```csharp
// 为 SystemGroup 创建共享 allocator
var group = world.GetOrCreateSystem<GasRuntimeFramePrepareSystemGroup>();
group.SetRateManagerCreateAllocator(
    new RateUtils.RateManagerCreateAllocator(allocator => new DoubleRewindableAllocators(allocator))
);
```

## 注意事项
- Allocator 在 group 开始前 rewind，group 结束后所有分配失效
- 不同 group 的 allocator 相互独立
- 适合帧内临时的 scratch 分配

## EX-GAS 适用点
- `GasRuntimeFramePrepareSystemGroup` 的 Query 准备阶段
- Spec Evaluation 阶段的临时 buffer
