using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC 创建请求。作为 enableable request component 挂到边界实体上，由 ASCEntityCreateSystem 创建 ASC Entity。
    /// </summary>
    public struct ASCCreateRequestComponent : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// 创建完成后写回 ASC Entity，并禁用本 request component。
        /// </summary>
        public Entity ResultEntity;
    }
}
