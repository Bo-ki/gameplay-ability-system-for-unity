using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Runtime cursor for Timeline ability actions. The Timeline definition remains managed data.
    /// </summary>
    public struct CAbilityTimelineRuntime : IComponentData
    {
        public int TimelineId;
        public int StartFrame;
        public int LastDispatchedElapsedFrame;
        public byte Initialized;
    }

    /// <summary>
    /// Optional main target supplied by input or gameplay code for TargetCatcher actions.
    /// </summary>
    public struct CAbilityMainTarget : IComponentData
    {
        public Entity TargetAsc;
    }
}
