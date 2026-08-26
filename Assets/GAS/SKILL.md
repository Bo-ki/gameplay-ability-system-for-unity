# EX-GAS 2.0 ECS 开发辅助 Skill

## Skill 概述

你是 EX-GAS 2.0 Runtime v1（Unity DOTS / ECS 版 Gameplay Ability System）的开发辅助 AI。
> 迁移提示：本 Skill 已按破坏性迁移更新；旧 ECS component、system group、EventBus、Runtime facade 和旧生成器不再是可用入口。当前实现以 `Assets/GAS/Runtime/V1` 的 owner、command port、tick kernel、transaction 与 boundary 为准。

当前版本以不可变 definition、owner-local command/fact lane 和 Runtime v1 tick kernel 为主线；新增 Ability 行为时，优先扩展配置 Bean、normalized row/catalog 与 V1 transaction，不创建旧 ECS request 组件或托管兼容层。

## 可扩展类型

| 类型 | 入口 | 用途 |
| --- | --- | --- |
| Ability 执行配置 | normalized row / V1 definition | 把配置表中的 Ability 行为落到不可变 catalog |
| GameplayCue | `GameplayCueBase<T>` / presentation outbox | 表现层效果，只消费 ECS 派生事件，不写 gameplay 状态 |
| TargetCatcher | `TargetCatcherBase<T>` | 目标捕获器与参数解析 |
| XParam | `XParam` + `[BeanField]` | 编辑器与 Luban 配置参数 |

## 核心规则

### `[BeanField]` 是参数入表标记

只有标注了 `[BeanField]` 的字段或属性会被 BeanUpdater 收集并写入 Luban 配置。

```csharp
[BeanField(nameof(SetValue), Comment = "数值")]
public float Value { get; private set; }

public void SetValue(float value)
{
    Value = value;
}
```

### Ability 行为必须数据化

Ability 表中的行为类型应由 CodeGen 映射为明确的 normalized row / V1 definition，例如：

```csharp
configs.Add(new ConfAbilityEffectsOnActivate { EffectCodes = aData.Param.IDs });
configs.Add(new ConfAbilityTimelineRef { TimelineId = aData.Param.ID });
configs.Add(new ConfAbilityMoveInput { RotationOffset = aData.Param.RotationOffset });
```

如果需要新增 Ability 行为，应同时完成：

1. 新增配置 Bean 或 normalized row 字段。
2. 新增对应 V1 definition / transaction 入口。
3. 更新编辑器 schema 与配置校验。
4. 更新 `BeanUpdater` 产出的 Bean 定义。
5. 更新 CodeGen 的 normalized row 映射。
6. 让 Runtime v1 通过 command / transaction / fact 消费该行为。
7. 如果新增 kernel 阶段，更新 `GasTickDag` 并确认顺序。
8. 重新生成 Luban normalized rows 并运行 V1 验证。

### Request / Fact / Observation 边界

外部输入通过 `GasCommandPort` 写入 owner-local command lane。
玩法结果由 `GasTickDag` 消费 command 后落到 V1 runtime state、transaction 或 Boundary fact。

Boundary facts、presentation read model、diagnostics evidence 属于观察、表现、回放和日志边界；它们可以让 UI、Cue、调试窗口和测试读取结果，但不应反向成为 gameplay 权威状态。

### Runtime v1 调度必须显式登记

新增 Runtime v1 阶段或 transaction 时，必须检查 `GasTickDag`。顺序应表达 command、simulation、transaction、fact projection 的真实依赖。

### 生成代码不手改

CodeGen 路径和 `.gen.cs` 文件是生成产物。需要清理旧产物时，应修改生成逻辑或配置源后重新生成；当前生成器只保留 normalized rows、V1 marker、Editor asmdef 与验证报告。

### Timeline 任务仅作为配置数据

时间轴编辑器当前只保留任务片段参数编辑能力。运行时不应通过托管任务对象执行逻辑；后续预览和运行逻辑应接入 ECS 事件流或专门的 ECS Timeline System。

### GameplayCue 是表现边界

Cue 仍可通过 `GameplayCueBase<T>` 扩展，但 Cue 不应反向持有 Ability 执行状态，也不应直接写 Attribute、Tag、Ability 或 GE runtime 状态。Cue 的触发、生命周期和目标上下文应来自 Boundary facts / read model 或 V1 GE / Ability transaction 结果。

## 参数编码规则

`EncodeExcelData()` 不写入 `null`。空数组写空字符串，字符串空值使用 `XParamDefault` 中的默认值。

```csharp
result.Add(IDs == null || IDs.Length == 0 ? string.Empty : string.Join(";", IDs));
result.Add(string.IsNullOrEmpty(Value) ? XParamDefault.DefaultString : Value);
```

`LayerMask` 这类 Unity 类型入表时应显式设置 Luban 类型：

```csharp
[BeanField(nameof(SetLayer), LubanType = "int")]
public LayerMask Layer { get; private set; }
```

## 验证

优先验证顺序：

1. `dotnet build com.exhard.exgas.runtime.csproj --no-restore -v:minimal`
2. `dotnet build com.exhard.exgas.editor.csproj --no-restore -v:minimal`
3. 重新生成 `Gen` 代码。
4. `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`

如果 Unity 批处理返回 0，也必须读取日志确认没有 `another Unity instance is running with this project open`。
