# PRF-20: IJobEntity Execute 参数不匹配的安全风险

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — PRF-12 节 + PRF-20 节；`concepts-safety.md`

## 规则声明
`IJobEntity` 不验证 `Execute` 方法的参数是否与关联的 `EntityQuery` 匹配。参数不匹配是静默错误——无编译错误、无运行时警告。每次修改 query 或 Execute 签名后必须交叉验证。使用 `SystemAPI.Query` + source generator 自动生成的 IJobEntity 也需要确认 query 语义的正确性。

## 为什么
Execute 参数比 query 多 → job 不调度任何 entity（静默跳过）。Execute 参数比 query 少 → 遗漏 component 的写入，数据不一致。重构 query 忘记同步 Execute 参数 → 几个月后才被发现。官方文档明确指出："IJobEntity does not verify that the Execute method's parameters match the EntityQuery. You must manually keep them in sync."

## EX-GAS 诊断
Runtime Core 中所有 IJobEntity 使用（参数复杂，重构风险高）。每个 IJobEntity 旁应注释列出对应的 EntityQuery 条件。Code review 清单中包含 IJobEntity query/Execute 一致性检查项。

## 检查方法
- Code review 时交叉验证 query 属性（`[WithAll]`/`[WithNone]`）和 Execute 参数
- 每个 IJobEntity 旁注释列出对应的 EntityQuery 条件
- 新增/删除 component 参数时同步检查 query 声明
