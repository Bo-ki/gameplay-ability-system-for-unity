using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC Entity 的身份与基础运行参数。Level 是当前 Runtime Core 的过渡期等级入口。
    /// </summary>
    public struct ASCIdentityComponent : IComponentData
    {
        public int Level;
    }
}
