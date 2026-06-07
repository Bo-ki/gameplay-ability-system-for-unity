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

    /// <summary>
    /// Boundary-only stable key for report / snapshot projection. Runtime Core still uses Entity internally.
    /// </summary>
    public struct ASCBoundaryReportKeyComponent : IComponentData
    {
        public int Key;
        public int Version;

        public bool IsValid => Key > 0;

        public static ASCBoundaryReportKeyComponent Create(int key, int version = 1)
        {
            return new ASCBoundaryReportKeyComponent
            {
                Key = key,
                Version = version,
            };
        }
    }
}
