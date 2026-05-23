using Unity.Entities;

namespace GAS.Runtime
{
    public struct CDurationDefinition : IComponentData
    {
        /// <summary>
        /// 持续时间。 小于等于0 表示无限
        /// </summary>
        public int Duration;
        
        /// <summary>
        /// 计时单位
        /// GAS的所有实际计时单位只有Frame（逻辑帧）和Turn（回合）两种。【如果开发人员手动控制Turn的更新速度和帧率一致，则实际Turn和Frame效果相同】
        /// 编辑器可能显示单位秒，实际存储时会换算为Frame逻辑帧
        /// </summary>
        public TimeUnit TimeUnit;

        /// <summary>
        /// 是否在激活时，刷新计时起始时间
        /// </summary>
        public bool ResetStartTimeWhenActivated;
        
        /// <summary>
        /// 是否在失活时，停止计时
        /// </summary>
        public bool StopTickWhenDeactivated;
    }

    public struct CDurationRuntime : IComponentData
    {
        /// <summary>
        /// 本次 GE instance 解析后的持续时间。小于等于0 表示无限。
        /// </summary>
        public int ResolvedDuration;

        /// <summary>
        /// 本次 GE instance 解析后的计时单位。
        /// </summary>
        public TimeUnit ResolvedTimeUnit;

        /// <summary>
        /// 开始计时的时间点
        /// </summary>
        public int ActiveTime;
        
        /// <summary>
        /// 是否激活生效中。只有Durational GameplayEffect存在激活和失活的概念
        /// </summary>
        public bool Active;

        /// <summary>
        /// StopTickWhenDeactivated=true时，该字段生效
        /// 上一次开始计时时间
        /// </summary>
        public int LastActiveTime;
        
        /// <summary>
        /// StopTickWhenDeactivated=true时，该字段生效
        /// 剩余持续时间
        /// </summary>
        public int RemainingTime;
    }
    
    public sealed class ConfDuration:GameplayEffectComponentConfig
    {
        public int duration;
        public TimeUnit timeUnit;
        public bool ResetStartTimeWhenActivated;
        public bool StopTickWhenDeactivated;

        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var em = GASManager.EntityManager;
            em.AddComponentData(ge, new CDurationDefinition
            {
                Duration = duration,
                TimeUnit = timeUnit,
                ResetStartTimeWhenActivated = ResetStartTimeWhenActivated,
                StopTickWhenDeactivated = StopTickWhenDeactivated,
            });
        }
    }
}
