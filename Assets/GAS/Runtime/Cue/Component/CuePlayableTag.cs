using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Cue can enter the playing state this frame.
    /// </summary>
    public struct CuePlayableTag : IComponentData, IEnableableComponent
    {
    }
}
