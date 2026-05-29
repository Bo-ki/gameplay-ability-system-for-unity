using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Enableable owner-local gate for ASCs that currently have active attribute modifiers.
    /// </summary>
    public struct AttributeActiveModifierPresentComponent : IComponentData, IEnableableComponent
    {
    }
}
