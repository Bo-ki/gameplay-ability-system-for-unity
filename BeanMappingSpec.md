# EX-GAS 2.0 Bean / Luban 映射规范

更新时间：2026-05-23

本文是当前 `BeanUpdater`、`CodeGeneratorLubanPart`、Luban `__beans__.xlsx` 与 Runtime 参数类型之间的映射规范。旧版围绕 `AbilityLogicBase`、`AbilityTaskBase` 的说明已经过时；当前 Ability 扩展入口是数据化 `AbilityExecutionBase` 和 `TimelineActionParameterBase`。

## 当前配置链

```text
C# 参数 / schema
  -> BeanUpdater 更新 __beans__.xlsx
  -> Luban 导表
  -> CodeGeneratorLubanPart 生成 XLuban 扩展
  -> ConfigRegistry / GASDefinitionTable
  -> ECS runtime definition / request / component
```

生成产物不手改。需要改变字段、类型或映射逻辑时，修改 C# 参数类型、schema 或 generator，然后重新导表和生成。

## BeanUpdater 当前收集范围

| 类别 | Bean 抽象基类 | 收集来源 | 用途 |
| --- | --- | --- | --- |
| 通用参数 | `XParam` | 所有非抽象 `XParam` 类型 | Cue、TargetCatcher、AbilityExecution、Timeline action 参数 |
| Cue 逻辑 | `GameplayCueBase` | 非抽象 `GameplayCueBase<TParam>` | 表现层 Cue 配置 |
| Ability 执行配置 | `AbilityExecutionBase` | `EditorAbilityHelper.GetAbilityExecutionSchemas()` | Ability 表中的数据化执行类型 |
| Timeline action 参数 | `TimelineActionParameterBase` | `EditorAbilityHelper.GetTimelineActionParameterSchemas()` | Timeline action clip 参数 |
| 目标捕获 | `TargetCatcherBase` | 非抽象 `TargetCatcherBase<TParam>` | Ability / Timeline 中的目标选择参数 |

不再收集或维护 `AbilityLogicBase` / `AbilityTaskBase` 作为当前 Runtime 扩展入口。

## `[BeanField]`

`[BeanField]` 标记一个字段或属性会进入 Luban Bean，并绑定生成代码中的 setter。

```csharp
[BeanField(nameof(SetValue), Comment = "数值")]
public float Value { get; private set; }

public void SetValue(float value)
{
    Value = value;
}
```

规则：

- `Setter` 必填，推荐 `nameof(SetXxx)`。
- `Name` 可覆盖写入 `__beans__.xlsx` 的字段名。
- `LubanType` 可覆盖类型映射，例如 `LayerMask` 使用 `int`。
- `Order` 默认来自 `[CallerLineNumber]`，也可显式指定。
- 未标注 `[BeanField]` 或 `[BeanPolymorphicField]` 的成员会被忽略。

基础类型映射：

| C# 类型 | Luban 类型 |
| --- | --- |
| `int` | `int` |
| `long` | `long` |
| `float` | `float` |
| `double` | `double` |
| `bool` | `bool` |
| `string` | `string` |
| `Vector2` | `vector2` |
| `Vector3` | `vector3` |
| `Vector4` | `vector4` |
| `T[]` / `List<T>` | `(array#sep=;),T` |
| 自定义类型 | 类型名 |

## `[BeanPolymorphicField]`

当 Luban 侧用一个多态 Bean 字段表达配置，而 Runtime 侧需要拆成 `TypeName + XParam` 时使用 `[BeanPolymorphicField]`。

```csharp
[BeanPolymorphicField(
    beanFieldName: "CueLogic",
    lubanPolymorphicType: nameof(GameplayCueBase),
    typeSetter: nameof(SetCueType),
    paramSetter: nameof(SetParam),
    paramTypeResolver: nameof(ResolveCueParamType),
    helperCategory: "Cue")]
public string CueType { get; private set; }

public XParam Param { get; private set; }
```

规则：

- `beanFieldName` 是写入 `__beans__.xlsx` 的字段名。
- `lubanPolymorphicType` 是 Luban 抽象 Bean 类型，例如 `GameplayCueBase` 或 `TargetCatcherBase`。
- `typeSetter` 写入 Runtime 类型名。
- `paramSetter` 写入 Runtime 参数实例。
- `paramTypeResolver` 根据类型名返回 Runtime 参数类型。
- `helperCategory` 当前支持 `Cue` 和 `TargetCatcher`。
- 关联的 `Param` 字段本身不需要 `[BeanField]`。

## 当前标准 Bean 形状

### XParam

`XParam` 子类只通过 `[BeanField]` / `[BeanPolymorphicField]` 暴露配置字段。示例：

| Runtime 参数 | Luban Bean 字段 |
| --- | --- |
| `XParamInt` | `Value: int` |
| `XParamFloat` | `Value: float` |
| `XParamTimelineID` | `ID: int` |
| `XParamEffectIDs` | `IDs: (array#sep=;),int` |
| `XParamCue` | `RequiredTags`、`ImmunityTags`、`CueLogic: GameplayCueBase` |
| `XParamApplyEffects` | `IDs`、`TargetCatcher: TargetCatcherBase` |

### GameplayCueBase

`GameplayCueBase<TParam>` 会生成一个 Cue Bean：

```text
GameplayCueBase          abstract
CueLogging               parent=GameplayCueBase, field Param=XParamLogging
CuePlaySound             parent=GameplayCueBase, field Param=XParamPlaySound
```

Cue 是表现层扩展，不能写 gameplay state。

### AbilityExecutionBase

Ability 执行配置由 `EditorAbilityHelper.GetAbilityExecutionSchemas()` 提供，当前生成形状：

```text
AbilityExecutionBase     abstract
ApplyEffectsOnActivate   parent=AbilityExecutionBase, field Param=XParamEffectIDs
TimelineRef              parent=AbilityExecutionBase, field Param=XParamTimelineID
MoveInput                parent=AbilityExecutionBase, field Param=<对应参数>
```

`CodeGeneratorLubanPart.GetAbilityConfig(...)` 将这些 Bean 映射为 `AbilityComponentConfig`，例如：

```csharp
configs.Add(new ConfAbilityEffectsOnActivate { EffectCodes = aData.Param.IDs });
configs.Add(new ConfAbilityTimelineRef { TimelineId = aData.Param.ID });
configs.Add(new ConfAbilityMoveInput { RotationOffset = aData.Param.RotationOffset });
```

新增 Ability 行为时，必须同时补：

1. Runtime ECS component / buffer 或 config carrier。
2. `AbilityComponentConfig`。
3. `EditorAbilityHelper.GetAbilityExecutionSchemas()`。
4. `CodeGeneratorLubanPart.GetAbilityConfig(...)` 的映射。
5. 对应 system 和 `GASSystemScheduleContract` 注册。
6. 测试覆盖 request / runtime state / fact / presentation 需要的链路。

### TimelineActionParameterBase

Timeline action 参数由 `EditorAbilityHelper.GetTimelineActionParameterSchemas()` 提供。Runtime 不再通过托管 `AbilityTaskBase` 自由执行逻辑；Timeline action 应被转换为数据化 action clip，并由 `SAbilityTimelineAction` 或专用 ECS system 分发 request / fact。

### TargetCatcherBase

`TargetCatcherBase<TParam>` 用于目标选择参数。当前仍作为参数解析和 editor preview 边界保留，但 gameplay 判定应落到 ECS request / target data：

```text
TargetCatcherBase        abstract
CatchSelf                parent=TargetCatcherBase, field Param=XParamNone
CatchTarget              parent=TargetCatcherBase, field Param=XParamNone
CatchAreaBox3D           parent=TargetCatcherBase, field Param=XParamCatchAreaBox3D
```

## 多态生成逻辑

`CodeGeneratorLubanPart.WritePolymorphicFieldAssignment(...)` 的生成过程：

1. 从 Luban 数据读取多态 Bean。
2. 调用 `typeSetter` 写入 `polyBean.GetType().Name`。
3. 通过 `paramTypeResolver` 找到 Runtime 参数类型。
4. 创建 `XParam` 实例。
5. 对每个多态子类生成 `switch case`，逐字段调用 `[BeanField]` 的 setter。
6. 调用 `paramSetter` 写回参数实例。

如果新增多态类别，需要扩展 `GetPolymorphicHelperInfo(...)`，当前只支持 `Cue` 和 `TargetCatcher`。

## Luban 导表流程

推荐流程：

1. 修改或新增 Runtime 参数类型 / schema。
2. 菜单执行 `EXTool/EX-GAS/生成脚本/更新Bean定义`。
3. 运行 Luban 导表：

```powershell
EX_GAS_Config\ProjectConfigTable\exgas_config\gen.bat
```

4. 菜单执行 `EXTool/EX-GAS/生成脚本/GAS表配置`。
5. 编译 Runtime / Editor。
6. 运行相关配置、definition、runtime contract tests。

## 常见问题

### 配置字段没有进入 `__beans__.xlsx`

检查：

1. 字段或属性是否标注 `[BeanField]`。
2. 字段是否属于 `XParam`、Cue 参数、TargetCatcher 参数或已注册 schema 参数。
3. `BeanUpdater` 是否能加载对应程序集。
4. 是否执行了“更新Bean定义”。

### 多态字段解析失败

检查：

1. `[BeanPolymorphicField]` 的 `lubanPolymorphicType` 是否与 Bean 抽象基类一致。
2. `helperCategory` 是否为当前支持的类别。
3. Runtime 类型和 Luban Bean 类型名是否一致。
4. `paramTypeResolver` 是否能返回正确 `XParam` 类型。

### 新 Ability 行为应该继承哪个类

当前不继承托管 Ability runtime 类。新增行为应通过 `AbilityExecutionBase` schema 映射为 `AbilityComponentConfig` 和 ECS system。

### Cue 能不能修改属性

不能。Cue 是表现边界，最多读取上下文并播放表现。属性、Tag、GE、Ability lifecycle 必须由 simulation system 通过 request / runtime component 修改。

## 相关文件

| 文件 | 作用 |
| --- | --- |
| `Assets/GAS/Runtime/General/XParam/BeanFieldAttribute.cs` | `[BeanField]` 定义 |
| `Assets/GAS/Runtime/General/XParam/BeanPolymorphicFieldAttribute.cs` | `[BeanPolymorphicField]` 定义 |
| `Assets/GAS/Editor/CodeGen/BeanUpdater.cs` | 更新 `__beans__.xlsx` |
| `Assets/GAS/Editor/CodeGen/CodeGeneratorLubanPart.cs` | 生成 `XLuban` 扩展与配置映射 |
| `Assets/GAS/Editor/Helper/EXEditorHelper.cs` | 反射读取 Bean 字段 |
| `Assets/GAS/Editor/Helper/EditorAbilityHelper.cs` | AbilityExecution / Timeline action schema |
| `EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/__beans__.xlsx` | Luban Bean 定义源 |
