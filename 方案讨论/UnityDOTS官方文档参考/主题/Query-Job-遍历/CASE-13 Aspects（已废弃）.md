# CASE-13: Aspects（已废弃）

**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — PRF-17 节；`aspects-intro.md`
**关联规则**: PRF-17

## 使用场景
（已废弃场景）Aspects 在 Entities 1.4.6 中被官方标记为 deprecated，将在未来版本中移除。禁止新代码使用。已有 Aspect 用法需登记迁移计划。

## 模式描述
Aspects 曾用于将多个 component 访问封装为一个 `readonly partial struct`，以简化重复的 component 访问模式。但由于生成的源代码依赖特定代码生成器，跨版本兼容性不可靠，官方已将其废弃。

```csharp
// 已废弃 —— 禁止在新代码中使用
readonly partial struct MyAspect : IAspect
{
    public readonly RefRW<BAttribute> Attribute;
    public readonly RefRO<CTagMask> TagMask;
}
```

## 注意事项
- 官方推荐替代方案：直接 component 访问和 query 方法
- 在 Entity 1.4.6+ 版本中使用 IAspect 会产生 deprecation warning
- 未来版本中 IAspect 将被完全移除，代码将无法编译
- 已有 Aspect 使用必须登记迁移计划

## EX-GAS 适用点
- 不适用——禁止使用。已有 Aspect 代码需要迁移到直接 component 访问
