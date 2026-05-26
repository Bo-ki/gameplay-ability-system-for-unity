# JOB-04: IJobEntity 的 Execute 参数明确 ref/in 语义

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — IJobEntity Execute 参数语义节 + JOB-04 节 + 常见陷阱

## 规则声明
IJobEntity 的 `Execute` 参数必须精确使用 `ref`（读写）、`in`（只读）修饰符，不能将只读参数错误标记为 `ref`。每次修改 query 或 Execute 签名后必须交叉验证参数与 query 属性的一致性。

## 为什么
- `ref` 参数即使未实际修改数据也触发 chunk 写标记，导致响应式系统误触发，造成无意义的级联计算。
- `in` 只读参数不触发写标记，支持 ECS 安全系统保证不被写。
- `IJobEntity` 不验证 `Execute` 方法的参数是否与关联的 `EntityQuery` 匹配——参数不匹配不会产生编译错误或运行时警告，重构时产生静默错误。

**参数语义表：**

| 修饰符 | 读写 | Query 条件 | Job 依赖 | 说明 |
|--------|------|------------|----------|------|
| `ref` | 读写 | 必需存在（WithAll） | 自动建立写依赖 | 修改影响其他 system |
| `in` | 只读 | 必需存在（WithAll） | 只读依赖 | ECS 安全系统保证不被写 |
| 值类型（无修饰符） | 值拷贝 | 不构成 query 条件 | 无依赖 | Entity/chunkIndex 等元数据 |
| `EnabledRefRW<T>` | 读写 enable 状态 | 必需存在 | 写依赖 | 只操作 enable 位 |
| `EnabledRefRO<T>` | 只读 enable 状态 | 必需存在 | 只读依赖 | 查询 enable 位 |

## EX-GAS 诊断
每个 IJobEntity 旁应注释列出对应的 EntityQuery 条件和参数语义说明。Code review 时检查 `ref`/`in` 使用是否正确，特别是避免只读字段使用 `ref`。

## 检查方法
- Code review 检查 IJobEntity Execute 参数中 ref/in 使用是否正确
- 交叉验证 query 属性（`[WithAll]`/`[WithNone]`）和 Execute 参数
- 新增/删除 component 参数时同步检查 query 声明
