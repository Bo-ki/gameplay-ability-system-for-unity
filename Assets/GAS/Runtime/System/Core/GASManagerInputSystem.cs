using Unity.Entities;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class GASManagerInputSystem : SystemBase
    {
        private Entity _managerEntity;

        protected override void OnCreate()
        {
            _managerEntity = EntityManager.CreateEntity(GASRuntimeEntityArchetypes.GASRunningTag(EntityManager));
            EntityManager.SetComponentEnabled<GASRunningTag>(_managerEntity, false);
        }

        protected override void OnUpdate()
        {
            EntityManager.SetComponentEnabled<GASRunningTag>(_managerEntity, GASManager.IsRunning);
        }
    }
}
