# CASE-36：DynamicBuffer.Reinterpret

**Primary Owner**：DynamicBuffer-Chunk-Archetype
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `components-buffer-reinterpret.md`

## 使用场景
两个 unmanaged 元素类型大小完全相同，并且调用者明确需要用另一类型解释相同字节。

## 模式描述
`DynamicBuffer.Reinterpret<U>()` 返回与原 buffer 别名相同内存、共享 safety handle 的 buffer。

```csharp
DynamicBuffer<MyIntWrapper> source = entityManager.GetBuffer<MyIntWrapper>(entity);
DynamicBuffer<int> values = source.Reinterpret<int>();
values[0] = 42;
```

## 注意事项
- API 只验证元素大小，不验证字段布局/业务语义。
- 大小不同在运行时检查中失败，不是编译期错误。
- reinterpret buffer 受与原 buffer 相同的结构变化失效和 safety 限制。
- `byte` 与多字节 struct 元素大小不同，不能用该 API把 byte buffer 直接变为 struct buffer。

## EX-GAS 适用点
仅用于有明确布局证明的 wrapper/基础类型别名；网络序列化、持久化和类型擦除优先使用显式编码，不用 Reinterpret 偷换格式。
