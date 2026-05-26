# CASE-08: EntityPrefab Reference 加载

**Primary Owner**: Prefab-Content管理
**来源**: 官方案例模式
**关联规则**: CONTENT-01

## 使用场景
baked prefab 包含完整 archetype，通过 `EntityPrefabReference` 在运行时加载和实例化。适用于角色模型实体、特效实体、UI 实体等真实资源模板。

## 模式描述
```csharp
// Authoring 端
public struct CEffectVfxPrefab : IComponentData
{
    public EntityPrefabReference VfxPrefab;
}

// Runtime 端：加载 prefab
// 通过 PrefabLoadResult 获取加载结果
```

## 注意事项
- 适用于"实体原型"（角色、特效、UI），不适用于"配置定义"
- 无头 Demo 中可用 log marker 占位
- Burst-compatible

## EX-GAS 适用点
- VFX prefab
- SFX prefab
- 角色 rendering entity
