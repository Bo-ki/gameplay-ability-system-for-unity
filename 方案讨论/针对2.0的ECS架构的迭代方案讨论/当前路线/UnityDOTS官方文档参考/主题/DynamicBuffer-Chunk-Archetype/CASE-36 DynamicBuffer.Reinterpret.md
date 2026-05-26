# CASE-36: DynamicBuffer.Reinterpret

**Primary Owner**: DynamicBuffer-Chunk-Archetype
**来源**: DynamicBuffer-Chunk-Archetype.md / `components-buffer-reinterpret.md`
**关联规则**: BUF-01

## 使用场景
需要将同一块 buffer 内存以不同类型访问，且原始类型和目标类型具有相同的大小。

## 模式描述
使用 `DynamicBuffer.Reinterpret<T>()` 在同一内存上以不同类型读写，共享安全句柄。

```csharp
// 原始 buffer 类型
DynamicBuffer<MyIntWrapper> myBuffer = ...;

// 重解释为 int
DynamicBuffer<int> intBuffer = myBuffer.Reinterpret<int>();  // 当 sizeof(MyIntWrapper) == sizeof(int) 时
```

修改 `intBuffer[i]` 等价于修改 `myBuffer[i]` 的对应字节。跨大小类型编译失败。

## 注意事项
- 仅当 `sizeof(T) == sizeof(U)` 时可用
- 修改 reinterpret 后的 buffer 等价于修改原始 buffer
- 共享同一安全句柄，ECS 安全系统统一管理

## EX-GAS 适用点
- 将 byte buffer 重解释为特定 struct 类型
- 位压缩数据的读写
- 类型擦除场景中的 buffer 访问
