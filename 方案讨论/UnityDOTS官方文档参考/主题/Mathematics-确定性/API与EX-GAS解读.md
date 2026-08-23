# Mathematics 确定性：API 与 EX-GAS 解读

**适用版本**：Unity `6000.3.14f1`；Mathematics `1.3.3`；Burst `1.8.29`

## 结论

- `Unity.Mathematics.Random` 是**有可变状态的 struct**，不是无状态纯函数。相同非零 seed、相同 Mathematics 版本和相同调用序列会产生相同伪随机序列。
- `Random.NextFloat()` 返回 `[0, 1)`，上界不包含；当前旧文档中的 `[0, 1]` 表述相反。
- `UnityEngine.Random` 可以用 `InitState` 或 `state` 控制/保存状态，但它是进程级共享静态状态，调用顺序容易被无关消费者污染，也不适合 Burst job。EX-GAS 只在确定性 Runtime Core 中禁用它，不把“无法设 seed”当作理由。
- 角度单位由具体 API 决定。`math.sin/cos/tan` 与 `Mathf.Sin/Cos/Tan` 都接收弧度；常见差异是 `UnityEngine.Quaternion.Euler` 接收角度，而 `Unity.Mathematics.quaternion.Euler/AxisAngle` 接收弧度。
- 排序只有在使用**稳定、唯一的全序键**时才建立确定性。只按 target 或 `ChunkIndexInQuery` 排序不能处理同键项，也不能保证跨 World 重建/回放稳定。

## Random 的状态与边界

```csharp
uint seed = 1; // Unity.Mathematics.Random 的构造 seed 必须非零。
var random = new Unity.Mathematics.Random(seed);

float unit = random.NextFloat();  // [0, 1)
int roll = random.NextInt(1, 101); // [1, 101)，即 1..100
```

每次 `Next*` 调用都会推进实例内部状态。调用者必须把更新后的 struct 写回其 owner；从组件或容器按值取出后只修改副本、但不写回，会重复生成同一段序列。

并行场景不得让多个 worker 同时修改同一个 Random 实例。常见方案是按 battle/entity/逻辑分区保存独立 state，或用稳定业务 ID 从基础 seed 派生独立 stream。派生键不能使用线程索引、chunk 顺序或本次调度的 worker 数。

## EX-GAS 的 Random owner

Random state 可以归属于：

- battle singleton/component（单 writer 串行消费）；
- per-entity component（每个实体独立序列）；
- system-owned NativeContainer（每个稳定逻辑分区一个 state）；
- 明确传入单次 job 的局部 state，并由结果写回其 owner。

不要求所有 Random 都必须存进 `IComponentData`，但禁止隐藏在静态字段或无法追踪的共享可变状态中。Debugger 的 seed、owner、逻辑 stream ID 和消费计数属于 EX-GAS 诊断策略，不是 Mathematics API 的强制字段。

## 角度单位

```csharp
float angleRadians = math.radians(45f);
float sine = math.sin(angleRadians);
quaternion rotation = quaternion.AxisAngle(math.up(), angleRadians);
float angleDegrees = math.degrees(angleRadians);
```

变量、字段或类型命名应携带 `Degrees`/`Radians` 语义。边界处转换一次，不要笼统写“UnityEngine.Mathf 使用 degrees”。

## 矩阵与四元数乘法

`float4x4 * float4x4` 在 Unity.Mathematics 中是逐分量乘法；矩阵乘法应使用 `math.mul`。四元数复合/旋转同样使用 `math.mul`，并按 API 定义确认左右操作数顺序。

```csharp
float4x4 matrixProduct = math.mul(matrixA, matrixB);
quaternion combined = math.mul(rotationA, rotationB);
float3 rotated = math.mul(combined, direction);
```

这与 Burst/非 Burst 路径无关，是 Unity.Mathematics API 本身的运算语义。

## Battle 确定性

影响 battle hash 或 replay 的 fan-in 应满足以下之一：

1. 操作在数学和表示层面可交换、可结合，结果与执行顺序无关；或
2. 消费前按稳定全序键排序，例如 `(Phase, TargetStableId, SourceStableId, CommandKind, ProducerSequence)`，并明确所有 tie-breaker。

`Entity.Index`、chunk index、线程索引和 NativeStream buffer index 只在特定 World/调度映射内有意义，不应直接充当跨运行 replay 的业务稳定 ID。浮点加法不满足结合律；若 hash 要跨 CPU/编译配置逐 bit 一致，还必须定义 Burst 浮点模式、NaN/负零规范化和必要的定点/量化策略。“epsilon 比较”适合近似验证，不等于确定性 hash。

## 官方证据

| 官方文档（Mathematics 1.3.3） | 可裁决结论 |
|---|---|
| `random-numbers.md` | Random state 由调用者创建和管理；`NextFloat()` 为 `[0,1)` |
| `compatibility.md` | Mathematics.Random 是实例状态、上界排除；矩阵 `*` 是逐分量乘法 |
| `quaternion-multiplication.md` | quaternion 角度使用 radians；复合使用 `math.mul` |
| `4x4-matrices.md` | 矩阵乘法使用 `math.mul` |

排序键、battle hash、跨平台位级确定性和 Runtime Core 禁用 `UnityEngine.Random` 均为 EX-GAS 项目规范，不是 Mathematics 官方保证。
