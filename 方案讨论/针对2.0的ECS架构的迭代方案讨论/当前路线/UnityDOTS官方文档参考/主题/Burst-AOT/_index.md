# Burst-AOT

## 职责边界

本主题维护 Burst 编译器在 EX-GAS Runtime Core 热路径、Player 验收、FunctionPointer、向量化加速和 AOT 配置中的使用规则。覆盖 `[BurstCompile]` 标记规范、FunctionPointer 的合理使用边界、Player 与 Editor 的 Burst 差异、AOT 编译配置、以及 `[NoAlias]` 向量化辅助。不覆盖 IL2CPP 配置或非 ECS 相关的 burst 使用。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解 Burst 编译器原理、FunctionPointer 代价、AOT 差异）
2. **核心规范**（按严重度）
   - `BUR-01: Hot path system_job 必须标注 [BurstCompile] 且无托管依赖.md` — P0: Hot path system/job 必须标注 `[BurstCompile]` 且无托管依赖
   - `BUR-02: FunctionPointer 用于批处理粒度的动态算法选择，禁止 per-entity invoke.md` — P1: FunctionPointer 用于批处理粒度的动态算法选择，禁止 per-entity invoke
   - `BUR-03: Player 性能报告必须包含 AOT_Safety_架构_warmup 上下文.md` — P1: Player 性能报告必须包含 AOT/Safety/架构/warmup 上下文
   - `BUR-04: 使用 [NoAlias] 辅助 Burst 向量化优化.md` — P1: 使用 `[NoAlias]` 辅助 Burst 向量化优化
   - `BUR-05: Editor Burst 通过不等于 Player Burst 通过，AOT 平台需单独验证.md` — P1: Editor Burst 通过不等于 Player Burst 通过，AOT 平台需单独验证
3. **模式与案例**
   - `CASE-11: Burst FunctionPointer 用于动态算法选择 + 批处理粒度.md` — Burst FunctionPointer 用于动态算法选择 + 批处理粒度
4. **拓展阅读**（按需）
   - NativeContainer allocator 约束 → `NativeContainer-Allocator/_index.md`
   - 确定性计算约束 → `Mathematics-确定性/_index.md`
   - Query-Job 性能优化 → `Query-Job-遍历/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | Burst 编译器原理、FunctionPointer、AOT 配置、向量化 |
| `BUR-01: Hot path system_job 必须标注 [BurstCompile] 且无托管依赖.md` | 规范 P0 | Hot path system/job 必须标注 [BurstCompile] |
| `BUR-02: FunctionPointer 用于批处理粒度的动态算法选择，禁止 per-entity invoke.md` | 规范 P1 | FunctionPointer 用于批处理粒度的动态算法选择 |
| `BUR-03: Player 性能报告必须包含 AOT_Safety_架构_warmup 上下文.md` | 规范 P1 | Player 性能报告包含 AOT/Safety/架构/warmup |
| `BUR-04: 使用 [NoAlias] 辅助 Burst 向量化优化.md` | 规范 P1 | 使用 [NoAlias] 辅助 Burst 向量化优化 |
| `BUR-05: Editor Burst 通过不等于 Player Burst 通过，AOT 平台需单独验证.md` | 规范 P1 | Editor Burst 通过不等于 Player Burst 通过 |
| `CASE-11: Burst FunctionPointer 用于动态算法选择 + 批处理粒度.md` | 模式 | Burst FunctionPointer 动态算法选择 |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `BUR-01` | P0 | Hot path system/job 必须标注 [BurstCompile] 且无托管依赖 | Burst Inspector 查看编译状态；Debugger 输出编译计数 |
| `BUR-02` | P1 | FunctionPointer 用于批处理粒度，禁止 per-entity invoke | 审查 FunctionPointer.Invoke 调用位置 |
| `BUR-03` | P1 | Player 性能报告必须包含 AOT 上下文 | 性能报告头部检查强制字段 |
| `BUR-04` | P1 | 使用 [NoAlias] 辅助 Burst 向量化 | Burst Inspector 检查循环是否已向量化 |
| `BUR-05` | P1 | Editor Burst 通过不等于 Player Burst 通过 | CI 配置中包含 AOT 平台的 Build 验证 |

## 跨主题引用

其他主题引用了本主题的规则：
| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `BUR-01` | System-World-SystemGroup | ISystem 的 Burst 编译要求 |
| `BUR-02` | NativeContainer-Allocator | ExecutionCalculation 的 FunctionPointer 使用 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| NativeContainer allocator 约束 | `NativeContainer-Allocator/_index.md` |
| 确定性计算约束 | `Mathematics-确定性/_index.md` |
| Query-Job 性能优化 | `Query-Job-遍历/_index.md` |
| 托管 vs unmanaged System 选择 | `System-World-SystemGroup/_index.md` |

## 验收指标

1. 热路径 system 可标注 `[BurstCompile]` 且无托管依赖
2. Player 跑完 warmup 后再采样稳定成本
3. Burst Inspector / 编译日志能作为性能交还证据
4. ExecutionCalculation 不走 OOP delegate 链
5. Player 性能报告包含完整的 Burst AOT 上下文
