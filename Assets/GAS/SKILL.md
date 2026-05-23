# EX-GAS 2.0 ECS 开发辅助 Skill

## Skill 概述

你是 EX-GAS 2.0（Unity DOTS / ECS 版 Gameplay Ability System）的开发辅助 AI。
当前版本以 ECS 数据组件、System 和生成器输出为主线；新增 Ability 行为时，优先扩展配置 Bean、`AbilityComponentConfig` 和 ECS System，不再创建托管 Ability 执行类。

## 可扩展类型

| 类型 | 入口 | 用途 |
| --- | --- | --- |
| Ability 执行配置 | `AbilityComponentConfig` / ECS component | 把配置表中的 Ability 行为落到 Entity 组件 |
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

Ability 表中的行为类型应由 `CodeGeneratorLubanPart` 映射为明确的 ECS 配置组件，例如：

```csharp
configs.Add(new ConfAbilityEffectsOnActivate { EffectCodes = aData.Param.IDs });
configs.Add(new ConfAbilityTimelineRef { TimelineId = aData.Param.ID });
configs.Add(new ConfAbilityMoveInput { RotationOffset = aData.Param.RotationOffset });
```

如果需要新增 Ability 行为，应同时完成：

1. 新增 ECS 组件或 buffer。
2. 新增对应 `AbilityComponentConfig`。
3. 更新 `EditorAbilityHelper.GetAbilityExecutionSchemas()`。
4. 更新 `BeanUpdater` 产出的 Bean 定义。
5. 更新 `CodeGeneratorLubanPart` 的 Ability 行为映射。
6. 让运行时通过 request / fact / ECS system 消费该行为，不在托管对象中直接改状态。
7. 如果新增 system，加入 `GASSystemScheduleContract` 并确认顺序。
8. 重新生成 Luban / Ability 映射代码。

### Request / Fact / Observation 边界

外部输入写入 request entity，例如 `CAbilityCommandRequest`、`CApplyGameplayEffectRequest`、`CAscCommandRequest`、`CRemoveGameplayEffectRequest`。
玩法结果必须由 ECS system 消费 request 后落到 runtime component、buffer 或 event fact。

`CGameplayEventBus`、`BPresentationEvent`、`BDebugReplayEvent` 属于观察、表现、回放和日志边界。它们可以让 UI、Cue、调试窗口和测试读取结果，但不应反向成为 gameplay 权威状态。

### System 调度必须显式登记

新增 runtime system 时，必须检查 `GASSystemScheduleContract`。系统顺序应表达真实依赖，例如 command request 先标准化，Ability commit 再产出 GE request，GE request 再创建 runtime GE entity，最后投影 observation / cue / log。

### 生成代码不手改

`Gen` 路径和 `.gen.cs` 文件是生成产物。需要清理旧产物时，应修改生成逻辑或配置源后重新生成；如果旧生成产物阻塞编译，先定位对应生成入口，再决定是否删除过期产物。

### Timeline 任务仅作为配置数据

时间轴编辑器当前只保留任务片段参数编辑能力。运行时不应通过托管任务对象执行逻辑；后续预览和运行逻辑应接入 ECS 事件流或专门的 ECS Timeline System。

### GameplayCue 是表现边界

Cue 仍可通过 `GameplayCueBase<T>` 扩展，但 Cue 不应反向持有 Ability 执行状态，也不应直接写 Attribute、Tag、Ability 或 GE runtime 状态。Cue 的触发、生命周期和目标上下文应来自 ECS 事件、presentation outbox 或 GE / Ability 组件状态。

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
