# Mathematics 确定性

## 职责边界

本领域维护 Unity.Mathematics 在 EX-GAS Runtime Core 中的确定性规则：随机数状态管理、角度单位规范、矩阵与四元数运算约定。确定性计算是 AutoChess 无头验收（battle hash 验证）的基础前提。不覆盖非确定性场景（如纯视觉效果随机、Editor-only 工具）。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解 Random 状态管理、角度单位规范、矩阵运算约定）
2. **核心规范**（按严重度）
   - `MAT-01: Runtime Core 禁用 UnityEngine.Random.md` — P0: Runtime Core 禁用 UnityEngine.Random
   - `MAT-04: 矩阵_四元数乘法使用 math.mul.md` — P1: 矩阵/四元数乘法使用 math.mul
   - `MAT-05: 确定性 fan-in 必须排序合并.md` — P1: 确定性 fan-in 必须排序合并
   - `MAT-02: Random State 必须显式归属.md` — P2: Random State 必须显式归属
   - `MAT-03: 角度单位使用 math.radians _ math.degrees 显式转换.md` — P2: 角度单位使用 math.radians / math.degrees 显式转换
   - `PRF-15: Mathematics.Random 状态显式归属；不用 UnityEngine.Random.md` — P1: Mathematics.Random 状态显式归属；不用 UnityEngine.Random
3. **模式与案例**：本主题无独立 CASE 文件（确定性规则通过规范文件直接体现）。
4. **拓展阅读**（按需）
   - Store 确定性数据分类 → `Store选型-数据承载策略/_index.md`
   - NativeContainer deterministic merge → `NativeContainer-Allocator/_index.md`
   - Burst 编译约束 → `Burst-AOT/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | Mathematics.Random、角度单位、矩阵/四元数运算 |
| `MAT-01: Runtime Core 禁用 UnityEngine.Random.md` | 规范 P0 | Runtime Core 禁用 UnityEngine.Random |
| `MAT-02: Random State 必须显式归属.md` | 规范 P2 | Random State 必须显式归属 |
| `MAT-03: 角度单位使用 math.radians _ math.degrees 显式转换.md` | 规范 P2 | 角度单位显式转换 |
| `MAT-04: 矩阵_四元数乘法使用 math.mul.md` | 规范 P1 | 矩阵/四元数乘法使用 math.mul |
| `MAT-05: 确定性 fan-in 必须排序合并.md` | 规范 P1 | 确定性 fan-in 必须排序合并 |
| `PRF-15: Mathematics.Random 状态显式归属；不用 UnityEngine.Random.md` | 规范 P1 | Mathematics.Random 状态显式归属 |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `MAT-01` | P0 | Runtime Core 禁用 UnityEngine.Random | Grep 搜索 Runtime 目录下 UnityEngine.Random |
| `MAT-04` | P1 | 矩阵/四元数乘法使用 math.mul | Grep float4x4 * 和 quaternion * 模式 |
| `MAT-05` | P1 | 确定性 fan-in 必须排序合并 | 审计 ParallelWriter/NativeStream 消费端排序 |
| `PRF-15` | P1 | Mathematics.Random 状态显式归属 | 搜索 Random 类型字段声明，确认在 IComponentData 中 |
| `MAT-02` | P2 | Random State 必须显式归属 | 确认 Random 字段在 IComponentData 中而非 static |
| `MAT-03` | P2 | 角度单位显式转换 | 搜索三角函数，检查参数来源单位标注 |

## 跨主题引用

其他主题引用了本主题的规则：
| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `MAT-05` | Store选型-数据承载策略 | 确定性 fan-in 排序策略 |
| `MAT-01` | Burst-AOT | Runtime Core Burst 编译约束与 UnityEngine.Random 冲突 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| Store 数据分类与确定性 | `Store选型-数据承载策略/_index.md` |
| NativeStream deterministic merge | `NativeContainer-Allocator/_index.md` |
| Burst 编译约束 | `Burst-AOT/_index.md` |
| AutoChess battle hash 验收 | `数据流-系统生命周期/_index.md` |

## 验收指标

1. 相同 seed、配置和输入下 battle hash 在多次运行间 100% 一致
2. Debugger 能记录 random stream owner、consumption count 和每次消耗的值
3. Runtime Core 零 `UnityEngine.Random` 引用
4. 角度单位错误能在配置验证或单元测试中暴露（通过输入已知 degrees 值比对预期 radians 值）
5. 所有 fan-in 路径有确定性排序策略且在 Debugger 中可视化
