# SC-02: 结构变化后必须重取所有 Handle

**严重度**: P0
**Primary Owner**: 结构变化-ECB
**来源**: `components-buffer-introducing.html`

## 规则声明

在结构变化发生后，所有此前获取的 TypeHandle、ComponentLookup、BufferLookup、DynamicBuffer 引用全部失效。必须重新获取后才能继续使用。

## 为什么

ECS 安全系统在结构变化后使所有 handle 失效以防止悬垂指针。使用失效 handle 触发安全系统异常，运行时崩溃。

## EX-GAS 诊断

曾出现 `DynamicBuffer invalidated by structural change` 安全异常，源于 EffectApplication 系统中在 buffer 操作前后存在隐式结构变化。

## 检查方法

Code review 时检查结构变化 API 调用后是否继续使用此前获取的 buffer/handle。优先将结构变化推迟到 playback phase 以避免此类问题。
