using Unity.Entities;

namespace GAS.Runtime
{
    public struct CuePresentationRequestComponent : IComponentData
    {
        public Entity TargetAsc;
        public Entity SourceEntity;
        public CueSourceType SourceType;
        public byte ResetRequested;
        public byte SourceUpdateRequested;
        public byte AddTargetRequested;
        public byte RemoveTargetRequested;

        public bool HasRequests =>
            ResetRequested != 0
            || SourceUpdateRequested != 0
            || AddTargetRequested != 0
            || RemoveTargetRequested != 0;

        public void ClearRequests()
        {
            ResetRequested = 0;
            SourceUpdateRequested = 0;
            AddTargetRequested = 0;
            RemoveTargetRequested = 0;
        }
    }
}
