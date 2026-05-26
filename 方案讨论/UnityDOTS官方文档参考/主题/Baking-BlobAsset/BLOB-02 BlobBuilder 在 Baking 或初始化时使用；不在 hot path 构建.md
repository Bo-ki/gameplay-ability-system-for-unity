# BLOB-02: BlobBuilder 在 Baking 或初始化时使用；不在 hot path 构建

**严重度**: P1
**Primary Owner**: Baking-BlobAsset
**来源**: `blob-assets-create.md`

## 规则声明
`BlobBuilder` 分配临时内存并执行数据复制，成本较高，禁止在 runtime hot path 中使用。正确模式：`ISystem.OnCreate` 中一次性构建并将 `BlobAssetReference<T>` 存入 singleton component，`OnDestroy` 时 `Dispose()`。

## 为什么
`BlobBuilder.ConstructRoot` + `Allocate` + `CreateBlobAssetReference` 涉及大量内存分配和数据拷贝，不适合 per-frame 或 per-event 调用。

## EX-GAS 诊断
Definition & Generation Layer 中，Luban Excel/JSON → SourceGenerator → .gen.cs → Baker → BlobAsset 管线仅在 Baking 期执行。Runtime 无 `BlobBuilder` 调用。

## 检查方法
Grep 搜索 `new BlobBuilder` 在 Runtime Core 目录中的出现，确认仅存在于 Baking System 或初始化 System 的 `OnCreate` 中。hot path system 中出现 `BlobBuilder` 为违规。
