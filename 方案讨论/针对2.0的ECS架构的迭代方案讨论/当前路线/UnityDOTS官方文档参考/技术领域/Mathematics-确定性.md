# Mathematics 确定性

## 职责

本领域维护 Unity.Mathematics 在 EX-GAS Runtime Core 中的确定性规则：随机数状态管理、角度单位规范、矩阵与四元数运算约定。确定性计算是 AutoChess 无头验收（battle hash 验证）的基础前提。不覆盖非确定性场景（如纯视觉效果随机、Editor-only 工具）。

## 核心概念

### Unity.Mathematics.Random vs UnityEngine.Random

```csharp
// Unity.Mathematics.Random —— 显式 state，可序列化，Burst 兼容，确定性
var random = new Unity.Mathematics.Random((uint)seed);
float value = random.NextFloat();       // [0, 1] 闭合区间
int roll = random.NextInt(1, 101);      // 1 到 100

// UnityEngine.Random —— 全局静态 state，不可序列化，非确定性
float value = UnityEngine.Random.value; // 禁止在 Runtime Core 中使用
```

**核心规则：Runtime Core 全部使用 `Unity.Mathematics.Random`，零 `UnityEngine.Random` 使用。**

`Unity.Mathematics.Random` 是无状态纯函数：给定相同 seed 和相同消耗顺序，产出完全相同序列。`UnityEngine.Random` 是全局非确定性状态，任何位置的调用都会影响全局序列。

### Random State 的显式归属

Random state 必须有明确 owner（battle / entity / frame / system），不能使用全局或隐式状态。

```csharp
// 正确：每个 battle 独立 seed，随机消耗从同一个 stream 派生
public struct CBattleRandomState : IComponentData
{
    public Unity.Mathematics.Random State;
    public int ConsumptionCount;  // Debugger 可追踪
}

// 使用
var state = SystemAPI.GetSingletonRW<CBattleRandomState>();
float critRoll = state.ValueRW.State.NextFloat();
state.ValueRW.ConsumptionCount++;
```

### 角度单位：Radians 必须显式

- Unity.Mathematics 期望 radians
- UnityEngine.Mathf 使用 degrees
- 混用是常见 bug 来源

```csharp
// 正确：显式转换
float angleRad = math.radians(45f);      // degrees -> radians
float angleDeg = math.degrees(angleRad); // radians -> degrees

// 错误：隐式假设角度单位
float sinVal = math.sin(45f);  // 如果意图是 degrees 但传入 radians，结果错误
```

### 矩阵与四元数乘法

```csharp
// 使用 math.mul，不依赖 C# 的 * 操作符（* 不保证矩阵乘法语义）
float4x4 result = math.mul(matrixA, matrixB);
quaternion combined = math.mul(quatA, quatB);
float3 rotated = math.mul(quaternion, vector);
```

## 编写规范

### MAT-01: Runtime Core 禁用 UnityEngine.Random

**声明：** Runtime Core（包括 Ability、Effect、Attribute、Targeting 相关所有 system）禁止使用 `UnityEngine.Random`。所有随机数需求必须通过 `Unity.Mathematics.Random` 满足。

- **来源：** `compatibility.md` — UnityEngine 与 Mathematics 在 Random 上存在差异
- **为什么：** `UnityEngine.Random` 是全局静态 state，无法控制 seed 和消耗顺序，破坏 battle hash 确定性
- **EX-GAS 诊断：** Debugger 应检测 Runtime Core system 目录中对 `UnityEngine.Random` 的任何引用，输出违规 system 列表
- **检查方法：** Grep 搜索 `UnityEngine.Random` 在 `Assets/GAS/Runtime/` 目录下；CI 中禁止新引入

### MAT-02: Random State 必须显式归属

**声明：** 每个 `Unity.Mathematics.Random` 的 state 字段必须通过 IComponentData 明确归属 owner（battle singleton / entity / frame-scoped system），使用全局变量或静态字段存储 Random state 是违规。

- **来源：** `random-numbers.md` — Mathematics.Random 是显式 state；`15-数据流-系统生命周期规范.md` P2-03
- **为什么：** 确定性要求 seed 和消耗序列完全可控。隐式/全局 state 无法追踪消耗计数，破坏 replay 验证
- **EX-GAS 诊断：** 每个 `CBattleRandomState` 的 `ConsumptionCount` 在 Debugger 中可视化，支持 per-battle 比对
- **检查方法：** 搜索 `Random` 类型字段声明，确认均在 IComponentData 中而非 static 变量

### MAT-03: 角度单位使用 math.radians / math.degrees 显式转换

**声明：** 所有角度相关计算必须显式标注单位。向 `math.sin` / `math.cos` / `math.tan` 等三角函数传入 degrees 值时必须先调用 `math.radians`。从 radians 返回 degrees 时必须调用 `math.degrees`。

- **来源：** `compatibility.md` — UnityEngine 与 Mathematics 在角度上存在差异；`quaternion-multiplication.md` — 使用 radians
- **为什么：** Unity.Mathematics 全部使用 radians。与 `UnityEngine.Mathf`（degrees）混用是确定性 bug 的常见来源，在 scale 时难以定位
- **EX-GAS 诊断：** Code Review 中标注所有三角函数调用，确认输入值单位
- **检查方法：** 搜索 `math.sin` / `math.cos` / `math.tan` / `math.atan2` 等三角函数，检查前一行或参数来源是否显式标注单位

### MAT-04: 矩阵/四元数乘法使用 math.mul

**声明：** 矩阵乘法（`float4x4`）和四元数乘法（`quaternion`）必须使用 `math.mul()` 方法。禁止使用 C# 的 `*` 操作符进行矩阵或四元数乘法。

- **来源：** `4x4-matrices.md` — 矩阵乘法用 math.mul；`quaternion-multiplication.md` — 使用 math.mul
- **为什么：** C# 的 `*` 操作符不保证矩阵乘法语义，在 Burst 编译路径下行为可能与预期不同。`math.mul` 在 Burst 中正确展开
- **EX-GAS 诊断：** 搜索 `float4x4` / `quaternion` 类型的 `*` 操作符使用，报告违规位置
- **检查方法：** Grep `float4x4 *` 和 `quaternion *` 模式；确认在 Runtime Core 中零出现

### MAT-05: 确定性 fan-in 必须排序合并

**声明：** 影响 battle hash / replay 的结果（EffectCommand fan-in、TypedFact projection、AttributeDelta reduce）不得使用无序 `ParallelWriter` 或依赖未排序的 foreach chunk 顺序。并行 fan-in 后必须按确定键（如 `[ChunkIndexInQuery]` int sortKey）排序 merge。

- **来源：** `15-数据流-系统生命周期规范.md` P2-01 — 确定性输出不得依赖无序写入
- **为什么：** 并行 job 的执行顺序在不同帧和不同 CPU 架构上不可预测。依赖顺序的 fan-in 导致 battle hash 不稳定
- **EX-GAS 诊断：** Debugger 应报告每个 fan-in 点的排序策略；无序 fan-in 输出告警
- **检查方法：** 审计所有 `ParallelWriter` / `NativeStream` 使用，确认消费端在 merge 时有确定性排序；搜索 `AppendToBuffer` + `sortKey` 模式

## EX-GAS 项目解读

### AutoChess 的确定性验收

```csharp
// Battle hash 验证
uint battleHash = ComputeBattleHash(outcomes);
// 相同 seed + 相同 config -> 相同 battleHash
// Scale 放大不能改变随机结果
```

**确定性验收清单：**
- 相同输入 -> 相同输出（battle hash 稳定）
- 增加实体数量不改变已有实体的计算结果
- 并行执行不引入顺序不确定性（MAT-05）
- Unity Editor / Player 结果一致（排除浮点精度差异）

### 随机消耗追踪

Debugger 应记录：
- 初始 seed
- 每帧 random consumption count
- 每次 Ability/Effect 触发消耗的随机数（与哪个 MMC 关联）
- Replay 时按相同 seed 重放，比对 consumption sequence

### BattleRandomState 生命周期

- `CBattleRandomState` 在 battle 初始化时创建，seed 从外部配置传入
- 每帧 OnUpdate 中唯一 system 持有写访问
- Battle 结束时销毁，consumption log 持久化用于验证

## 常见陷阱

1. **`UnityEngine.Random` 全局污染**：任何地方调用都会影响全局 state，即使在使用 `Mathematics.Random` 的项目中，混合使用导致确定性丧失
2. **`Mathematics.Random.NextFloat` 包含 0 和 1**：`[0, 1]` 闭合区间，与 `UnityEngine.Random` 的 `[0, 1)` 不同，需要 `NextFloat(0f, 1f)` 获得半开区间
3. **浮点精度差异**：Editor 和 Player、不同 CPU 架构（x86 vs ARM）可能有微小差异，battle hash 比对应容忍 epsilon
4. **Degrees vs Radians 混用**：`math.sin` / `math.cos` 期望弧度，误传 degrees 不会报错但计算结果错误
5. **复用 seed 导致随机序列相同**：同一随机流的不同消费者应使用 stream offset 或派生 seed（如 `Random.CreateFromIndex(baseSeed ^ consumerId)`）

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `compatibility.md` | UnityEngine 与 Mathematics 在角度/Random 上存在差异 | MAT-01, MAT-03 |
| `random-numbers.md` | Mathematics.Random 是显式 state，可序列化，Burst 兼容 | MAT-01, MAT-02 |
| `quaternion-multiplication.md` | 使用 radians 和 math.mul | MAT-03, MAT-04 |
| `4x4-matrices.md` | 矩阵乘法用 math.mul，不依赖运算符 | MAT-04 |
| `15-数据流-系统生命周期规范.md` P2-01 | 确定性输出不得依赖无序写入 | MAT-05 |
| `15-数据流-系统生命周期规范.md` P2-03 | Mathematics.Random state 必须显式归属 | MAT-02 |

## 验收指标

1. 相同 seed、配置和输入下 battle hash 在多次运行间 100% 一致
2. Debugger 能记录 random stream owner、consumption count 和每次消耗的值
3. Runtime Core 零 `UnityEngine.Random` 引用
4. 角度单位错误能在配置验证或单元测试中暴露（通过输入已知 degrees 值比对预期 radians 值）
5. 所有 fan-in 路径有确定性排序策略且在 Debugger 中可视化
