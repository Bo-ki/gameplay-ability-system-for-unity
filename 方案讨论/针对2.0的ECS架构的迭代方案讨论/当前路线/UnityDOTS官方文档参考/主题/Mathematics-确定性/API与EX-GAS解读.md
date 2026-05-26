# Mathematics 确定性: API 与 EX-GAS 解读

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

---

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

---

## 常见陷阱

1. **`UnityEngine.Random` 全局污染**：任何地方调用都会影响全局 state，即使在使用 `Mathematics.Random` 的项目中，混合使用导致确定性丧失
2. **`Mathematics.Random.NextFloat` 包含 0 和 1**：`[0, 1]` 闭合区间，与 `UnityEngine.Random` 的 `[0, 1)` 不同，需要 `NextFloat(0f, 1f)` 获得半开区间
3. **浮点精度差异**：Editor 和 Player、不同 CPU 架构（x86 vs ARM）可能有微小差异，battle hash 比应对容忍 epsilon
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
