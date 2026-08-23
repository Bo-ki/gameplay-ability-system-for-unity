# CASE-21: ECB PlaybackPolicy.MultiPlayback + Deferred Entity Remapping

**Primary Owner**: 结构变化-ECB
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `systems-entity-command-buffer-playback.md`、`systems-entity-command-buffer-automatic-playback.md`
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

- 手工创建的 ECB 必须由调用方 `Dispose()`；这与是否 `MultiPlayback` 无关
- placeholder entity 只可用于创建它的同一 ECB 内的后续命令；每次 playback 都会创建一组新的真实实体并完成本次映射
- MultiPlayback 本身不会启用并行排序；并行录制必须先取得 `var writer = ecb.AsParallelWriter()`，再调用 `ParallelWriter` 的带 `sortKey` 重载
- `ChunkIndexInQuery` 可作为当前 query 的调度无关 key；若 key 重叠或需要跨 query/job/World 的业务全序，仍需稳定业务 total key、tie-breaker，必要时使用独立 ECB

## EX-GAS 适用点

- 多 pass 效果应用（同一命令集在不同阶段执行）
- 将结构变化命令 playback 到不同 World（如 Core World 和 Debugger World）
