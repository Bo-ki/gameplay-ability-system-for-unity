using Unity.Entities;

namespace GAS.Runtime
{
    public abstract class GameplayEffectComponentConfig
    {

        /// <summary>
        /// 是否可以被加载到 GE prototype 上并通过 EntityManager.Instantiate 复制。
        /// 包含 managed runtime 对象、NativeArray 或一次性 entity 引用的配置应返回 false。
        /// </summary>
        public virtual bool SupportsPrototypeCache => true;

        /// <summary>
        /// 是否可以参与 GE static definition blob 构建。
        /// 表现层或托管运行时配置应返回 false，避免污染纯 definition 摘要。
        /// </summary>
        public virtual bool SupportsStaticDefinitionBlob => true;

        /// <summary>
        /// 添加组件到GE的实例上，这个函数是生成GE的核心。
        /// 因为采用了component结构，未来拓展GE的功能模块，会变得方便很多，实现了提前解耦。
        /// </summary>
        /// <param name="ge"></param>
        public abstract void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge);
    }
}
