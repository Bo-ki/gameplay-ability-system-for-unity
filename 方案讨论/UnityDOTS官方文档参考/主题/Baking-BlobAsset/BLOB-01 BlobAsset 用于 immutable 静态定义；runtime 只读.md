# BLOB-01: BlobAsset 用于 immutable 静态定义；runtime 只读

**严重度**: P1
**Primary Owner**: Baking-BlobAsset
**来源**: `blob-assets-create.md`、BlobAsset API 文档

## 规则声明
BlobAsset 存放 Baking 期或初始化期确定的不可变数据；runtime hot path 只消费 `BlobAssetReference<T>`，不修改。Runtime 可变状态必须使用 `DynamicBuffer` 或普通 component。

## 为什么
BlobAsset 的设计目标是 immutable、unmanaged、Burst-friendly 的共享只读数据。创建后不可修改，适合存放 Ability 定义、GE 定义、Tag 查找表等静态配置。生命周期按来源区分：运行时创建的 Blob 由创建者手动 `Dispose()`；Baker / Entity Scene 路径通过 BlobAssetStore 和引用计数释放。

## EX-GAS 诊断
目标态 Runtime Core hot path 只消费 `BlobAssetReference<T>`、`GASDefinitionCatalogBlob` 或 generated static table，无 managed config lookup。`AbilityConfigRegistry`／`GameplayEffectConfigRegistry` 应逐步被 Blob Catalog 替代。

## 检查方法
审查 BlobAsset 数据类型的写入路径，确认只在 Baking System 或 `OnCreate` 中写入，runtime `OnUpdate` 中只读不写。搜索 `BlobAssetReference` 的 `.Value` 赋值出现位置。
