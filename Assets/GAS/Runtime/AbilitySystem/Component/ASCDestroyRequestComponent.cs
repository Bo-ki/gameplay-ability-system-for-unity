using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC 销毁请求。属于低频边界意图，消费后进入 ASCDestroyingComponent 标记阶段。
    /// </summary>
    public struct ASCDestroyRequestComponent : IComponentData
    {
        public Entity ASC;
    }

    /// <summary>
    /// ASC 正在销毁的持续状态。低频多帧生命周期状态使用普通 Component，而不是 enableable toggle。
    /// </summary>
    public struct ASCDestroyingComponent : IComponentData, IEnableableComponent
    {
    }
}
