# BUF-03: Buffer handle 结构变化后必须重取

**严重度**: P0
**Primary Owner**: DynamicBuffer-Chunk-Archetype
**来源**: `components-buffer-introducing.html`

## 规则声明
任何结构变化（`CreateEntity`/`DestroyEntity`/`AddComponent`/`RemoveComponent`）之后，先前获取的 `BufferHandle` / `BufferLookup` / `DynamicBuffer` 引用全部失效，必须重新获取。

## 为什么
结构变化可能移动 entity 到新 chunk，使原有 buffer 指针指向旧 chunk 的已释放内存。ECS 安全系统在检测到后抛出异常，但若在 Playback 或特殊路径下未检测则产生静默数据错误。

## EX-GAS 诊断
ISSUE-004 曾暴露 `BufferTypeHandle invalidated by structural change` 错误。`SApplyGameplayEffectRequest` 在同一系统中读 buffer 后创建/销毁 entity。

## 检查方法
搜索 GetBuffer / GetBufferLookup 后出现 CreateEntity/DestroyEntity/AddComponent/RemoveComponent 的模式。
