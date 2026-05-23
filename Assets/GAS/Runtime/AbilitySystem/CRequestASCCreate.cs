using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC 创建请求。添加到任意 Entity 上后，SASCCreate 在下一帧创建 ASC Entity
    /// 并将创建结果写回 ResultEntity 字段。
    /// </summary>
    public struct CRequestASCCreate : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// 创建完成后，ASC Entity 写入此字段。
        /// </summary>
        public Entity ResultEntity;
    }
}
