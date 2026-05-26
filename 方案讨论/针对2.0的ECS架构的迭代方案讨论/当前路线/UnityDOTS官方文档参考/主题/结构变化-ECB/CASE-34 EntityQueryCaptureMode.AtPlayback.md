# CASE-34: EntityQueryCaptureMode.AtPlayback

**Primary Owner**: 结构变化-ECB
**来源**: `optimize-structural-changes.md`
**关联规则**: SC-03

## 使用场景

当 ECB 需要批量对大量 entity 执行相同的结构变化，且 entity 集合在录制到 playback 之间可能变化时。

## 模式描述

ECB 录制时存储 query 引用，playback 时重新评估 query 结果，利用批量 chunk 级操作优势。

```csharp
// 录制时传入 query，指定 AtPlayback
ecb.AddComponent(query, new CEffectTag(), EntityQueryCaptureMode.AtPlayback);

// playback 时重新评估 query，批量操作所有匹配 chunk
// 1M entity 场景下：3.5ms（AtPlayback）vs 170ms（逐个 entity）
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
- `AtPlayback` 在 playback 时仍会触发 sync point（因为执行了结构变化）
- 适合批量 chunk 级操作，不适合少量 entity 的精确控制

## EX-GAS 适用点

- 批量对大量 ASC entity 添加/移除标记组件
- 批量销毁过期的 effect entity
