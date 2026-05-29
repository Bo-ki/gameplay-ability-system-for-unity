using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Enableable owner-local gate for attributes that only need change-event projection.
    /// </summary>
    public struct AttributeChangeEventPendingComponent : IComponentData, IEnableableComponent
    {
    }
}
