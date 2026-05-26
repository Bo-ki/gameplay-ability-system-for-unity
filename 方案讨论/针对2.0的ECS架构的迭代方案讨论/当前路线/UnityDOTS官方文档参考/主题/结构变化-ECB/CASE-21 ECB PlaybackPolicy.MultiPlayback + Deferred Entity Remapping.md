# CASE-21: ECB PlaybackPolicy.MultiPlayback + Deferred Entity Remapping

**Primary Owner**: 结构变化-ECB
**来源**: `systems-entity-command-buffer-playback.md`
**关联规则**: ECB-04, CASE-35, PRF-13

## 使用场景

当需要多次 playback 同一批结构变化命令，或将同一批命令 playback 到不同 World state 时（如多 pass 效果应用、网络同步预测与回滚）。

## 模式描述

通过 `new EntityCommandBuffer(Allocator.TempJob, PlaybackPolicy.MultiPlayback)` 创建可多次 playback 的 ECB。同一 ECB 内先 `CreateEntity()` 后通过 placeholder entity 引用该实体，deferred entity remapping 自动完成映射。

```csharp
// 创建可多次 playback 的 ECB
var ecb = new EntityCommandBuffer(Allocator.TempJob, PlaybackPolicy.MultiPlayback);

// 录制命令
var placeholder = ecb.CreateEntity();
ecb.AddComponent(placeholder, new CEffectResult { ... });

// 第一次 playback
ecb.Playback(state.EntityManager);

// 然后在另一个 World 或另一个 state 再次 playback
ecb.Playback(otherWorld.EntityManager);

// 手动释放 allocator（非自动 rewind）
ecb.Dispose();
```

## 注意事项

- 必须手动 `Dispose()`，因为 `MultiPlayback` 模式不会自动 rewind
- Deferred entity remapping 只在同一 ECB 内有效——如果在第一次 playback 后创建了新 entity，第二次 playback 时不会映射到新 entity
- 配合 `[ChunkIndexInQuery] int sortKey` 实现确定性 playback 顺序

## EX-GAS 适用点

- 多 pass 效果应用（同一命令集在不同阶段执行）
- 将结构变化命令 playback 到不同 World（如 Core World 和 Debugger World）
