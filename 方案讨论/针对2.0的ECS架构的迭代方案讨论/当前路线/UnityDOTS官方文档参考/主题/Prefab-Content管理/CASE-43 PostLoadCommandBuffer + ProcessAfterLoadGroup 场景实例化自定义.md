# CASE-43: PostLoadCommandBuffer + ProcessAfterLoadGroup 场景实例化自定义

**Primary Owner**: Prefab-Content管理
**来源**: `streaming-scene-instancing.md`
**关联规则**: CONTENT-01

## 使用场景
场景加载在独立的 streaming world 中进行，加载完成后 `ProcessAfterLoadGroup` 在 streaming world 中运行，允许对场景实例进行 per-instance 修改（偏移、配置注入等）。

## 模式描述
```csharp
// 1. 创建场景实例时附加 PostLoadCommandBuffer
var loadParams = new SceneLoadParameters
{
    Flags = SceneLoadFlags.NewInstance,
    PostLoadCommandBuffer = myPostLoadECB  // managed component 含 ECB
};

// 2. ProcessAfterLoadGroup 在 streaming world 中运行
// 此时所有内容已加载但尚未移入 main world
// 读取配置并应用偏移

// 伪代码逻辑：
// SceneLoadFlags.NewInstance + PostLoadCommandBuffer（创建 per-instance 配置 entity）
// + ProcessAfterLoadGroup（读取配置并应用偏移）
```

## 注意事项
- PostLoadCommandBuffer 是一种 managed component（含 ECB）
- section 加载时 streaming system 检查其存在并在 `ProcessAfterLoadGroup` 之前执行
- `ProcessAfterLoadGroup` 在 streaming world 中运行，不是在 main world
- 访问 main world 数据需要显式传递引用

## EX-GAS 适用点
- AutoChess 从同一场景文件创建多个战斗实例
- 每个实例不同初始位置、不同队伍配置
- 避免为每个实例 bake 独立场景
