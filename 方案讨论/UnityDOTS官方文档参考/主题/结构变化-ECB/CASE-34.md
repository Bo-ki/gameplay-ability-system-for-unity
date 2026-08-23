# CASE-34: EntityQueryCaptureMode.AtPlayback

**Primary Owner**: 结构变化-ECB
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `optimize-structural-changes.md`
**关联规则**: SC-03

## 使用场景

当 ECB 需要批量对大量 entity 执行相同的结构变化，且 entity 集合在录制到 playback 之间可能变化时。

## 模式描述

ECB 录制时存储 query 引用，playback 时重新评估 query 结果，利用批量 chunk 级操作优势。

```csharp
// 录制时传入 query，指定 AtPlayback
ecb.AddComponent(query, new CEffectTag(), EntityQueryCaptureMode.AtPlayback);

// playback 时重新评估 query，批量操作届时匹配的 chunk
```

无 AtPlayback 时需要 batch 的替代方案：
```csharp
// 手动遍历 + ECB 录制（逐个 entity）
foreach (var entity in query.ToEntityArray(Allocator.Temp))
    ecb.AddComponent(entity, new CEffectTag());
```

## 注意事项

- 默认是 `EntityQueryCaptureMode.AtRecord`——录制时评估 query 结果并固定 entity 集合
- 使用 `AtPlayback` 必须显式传递参数
- playback 执行这些结构变化，仍需遵守结构变化的同步和直接引用失效规则
- 适合批量 chunk 级操作，不适合少量 entity 的精确控制
- 性能收益取决于匹配量、archetype 分布和平台；不得把单次样本数字当成通用倍率

## EX-GAS 适用点

- 批量对大量 ASC entity 添加/移除标记组件
- 批量销毁过期的 effect entity
