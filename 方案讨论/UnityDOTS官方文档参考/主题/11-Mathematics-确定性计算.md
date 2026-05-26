# 11 Mathematics 与确定性计算

## 职责

本主题维护 Unity.Mathematics 在随机、角度、矩阵、四元数和可复现战斗中的规则。确定性计算是 AutoChess 无头验收的基础前提。

## 核心概念详解

### Unity.Mathematics.Random vs UnityEngine.Random

```csharp
// Unity.Mathematics.Random —— 显式 state，可序列化，确定性
var random = new Unity.Mathematics.Random((uint)seed);
float value = random.NextFloat();       // 0-1
int roll = random.NextInt(1, 101);      // 1-100

// UnityEngine.Random —— 全局静态 state，不可确定性控制
float value = UnityEngine.Random.value; // 禁止在 Runtime Core 中使用
```

**核心规则：Runtime Core 不使用 `UnityEngine.Random`。**

### Random State 的归属

```csharp
// 每个 battle 独立 seed
// 每个 Ability/Effect 随机消耗从同一个 stream 派生
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

```csharp
// Mathematics 使用 radians
// UnityEngine.Mathf 使用 degrees
// 混用是常见 bug 来源

// 正确
float angleRad = math.radians(45f);     // degree → radian
float angleDeg = math.degrees(angleRad); // radian → degree
```

### 矩阵与四元数乘法

```csharp
// 使用 math.mul，不依赖操作符（C# 的 * 不保证矩阵乘法语义）
float4x4 result = math.mul(matrixA, matrixB);
quaternion combined = math.mul(quatA, quatB);
float3 rotated = math.mul(quaternion, vector);
```

## 官方证据

| 证据 | 结论 |
|---|---|
| `compatibility.md` | UnityEngine 与 Mathematics 在角度/Random 上存在差异 |
| `random-numbers.md` | Mathematics.Random 是显式 state |
| `quaternion-multiplication.md` | 使用 radians 和 math.mul |
| `4x4-matrices.md` | 矩阵乘法用 math.mul |

## 使用模式与反模式

**正确模式：**
- Runtime Core 全部使用 `Unity.Mathematics.Random`
- Random state 明确归属（battle / frame / per-entity）
- 角度转换使用 `math.radians()` / `math.degrees()`
- 矩阵/四元数乘法使用 `math.mul()`

**反模式：**
- `UnityEngine.Random` 用于 gameplay 决策
- 角度单位隐式假设
- `*` 操作符做矩阵乘法

## EX-GAS 项目解读

### AutoChess 的确定性验收

```csharp
// Battle hash 验证
uint battleHash = ComputeBattleHash(outcomes);
// 相同 seed + 相同 config → 相同 battleHash
// Scale 放大不能改变随机结果
```

**确定性验收清单：**
- 相同输入 → 相同输出
- 增加实体数量不改变已有实体的计算结果
- 并行执行不引入顺序不确定性
- Unity Editor / Player 结果一致（排除浮点精度差异）

### 随机消耗追踪

Debugger 应记录：
- 初始 seed
- 每帧 random consumption count
- 每次 Ability/Effect 触发消耗的随机数
- Replay 时按相同 seed 重放

## 常见陷阱

1. **`UnityEngine.Random` 全局污染**：任何地方调用都会影响全局 state
2. **Mathematics.Random 的 `NextFloat` 包含 0 和 1**：`[0, 1]` 闭合区间，与 Unity 不同
3. **浮点精度差异**：Editor 和 Player、不同 CPU 架构可能有微小差异
4. **Degrees vs Radians 混用**：`Trigonometric` 函数期望弧度

## 验收指标

1. 相同 seed、配置和输入下 battle hash 稳定
2. Debugger 能记录 random stream owner 和消耗计数
3. 数学单位错误能在配置验证或测试中暴露
