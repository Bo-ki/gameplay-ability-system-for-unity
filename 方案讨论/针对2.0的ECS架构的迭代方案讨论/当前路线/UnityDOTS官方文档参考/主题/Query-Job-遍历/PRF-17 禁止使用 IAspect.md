# PRF-17: 禁止使用 IAspect

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — PRF-17 节；`aspects-intro.md`

## 规则声明
`IAspect` 在 Entities 1.4.6 中被官方标记为 deprecated，将在未来版本中移除。禁止在任何新代码中使用 `IAspect` 或 `readonly partial struct ... : IAspect`。

## 为什么
废弃 API 将在未来版本移除，代码将无法编译。IAspect 生成的源代码依赖特定代码生成器，跨版本兼容性不可靠。直接 component 访问是官方推荐的替代方案。官方文档明确说明："Aspects are deprecated and will be removed in a future release. Use component access and query methods directly instead."

## EX-GAS 诊断
Grep 搜索 `IAspect`、`: IAspect`、`partial struct.*Aspect`。新增代码 PR 必须零 `IAspect` 出现。已有 Aspect 用法在 CASE-13 中登记为待迁移。

## 检查方法
- 全局搜索 `IAspect` 关键字
- Code review 拦截任何新 Aspect 代码
- 已有 Aspect 必须登记迁移计划
