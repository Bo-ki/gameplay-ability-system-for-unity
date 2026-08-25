using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GE 静态定义 prototype 标记。runtime instance 由 prototype clone 后会移除此组件。
    /// </summary>
    public struct GEPrototypeComponent : IComponentData, IEnableableComponent
    {
        public int GameplayEffectCode;
    }
}
