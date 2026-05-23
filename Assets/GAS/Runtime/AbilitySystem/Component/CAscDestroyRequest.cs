using Unity.Entities;

namespace GAS.Runtime
{
    public struct CAscDestroyRequest : IComponentData
    {
        public Entity ASC;
    }

    public struct CAscDestroying : IComponentData
    {
    }
}
