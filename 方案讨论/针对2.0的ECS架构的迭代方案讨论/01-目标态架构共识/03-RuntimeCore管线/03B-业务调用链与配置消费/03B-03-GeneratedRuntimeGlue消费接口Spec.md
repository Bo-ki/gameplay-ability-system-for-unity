# Generated Runtime Glue 消费接口 Spec

## 结论

Generated Runtime Glue 是无状态、无生命周期、Burst-compatible 的静态函数面。它把预解析 index 和冻结输入转成确定性结果，不接触 World/query/ECB/allocator/container owner。

## 合法接口形态

```csharp
/// <summary>
/// 为 Runtime 提供已生成的不可变 Definition 查找与纯计算入口。
/// </summary>
internal static class GasGeneratedDefinitionGlue
{
    /// <summary>
    /// 通过预解析索引读取 Ability Definition；调用方负责 Catalog 生命周期。
    /// </summary>
    internal static ref readonly AbilityDefinitionBlob ResolveAbility(
        in GasDefinitionCatalogBlob catalog,
        int definitionIndex)
    {
        return ref catalog.Abilities[definitionIndex];
    }

    /// <summary>
    /// 使用已声明 capture view 和冻结参数计算 magnitude，不读取 Runtime 全局状态。
    /// </summary>
    internal static float EvaluateMagnitude(
        in MagnitudeProgramBlob program,
        in MagnitudeInput input)
    {
        return 0f;
    }
}
```

实际实现必须兼容项目 C# 9.0。上例只表达 owner：Catalog 由 Session 持有，输入由 Kernel 准备，生成函数不缓存 buffer/entity/lookup。

## 输入边界

允许：DefinitionIndex、Attribute/Tag index、冻结 Context/SetByCaller、Capture accessor、稳定 source/target snapshot、纯数学参数。

禁止：Entity、EntityManager、World、SystemState、EntityQuery、BufferLookup、ECB、NativeContainer allocator、managed service、反射或任意 capture accessor。

## 输出边界

允许返回标量、bool/reason、固定 record、静态 range/index 和有界展开结果。不得返回持有 Blob 外临时指针的长期对象，不得直接写 ASC、创建 Entity、append 全局 buffer 或发布 Boundary fact。

## 验收

- Runtime-visible generated files 静态扫描零 `ISystem/OnUpdate/EntityManager/EntityQuery/ECB/NativeContainer allocation`。
- pure vector tests 在相同输入/content hash 下结果一致。
- 未声明 capture view、dynamic definition selection 与 prediction API 在生成期失败。
- 删除生成 glue 后，复杂度只回到 Definition/evaluator 实现，不扩散为调度 owner。
