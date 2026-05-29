using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Enableable owner-local gate for ASC attributes that need recalculation or event projection.
    /// </summary>
    public struct AttributeDirtyComponent : IComponentData, IEnableableComponent
    {
    }
}
