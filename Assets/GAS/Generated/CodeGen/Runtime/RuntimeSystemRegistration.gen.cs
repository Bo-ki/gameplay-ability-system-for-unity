///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;
using Unity.Entities;

namespace GAS.Runtime.Generated
{
    public static class GASGeneratedRuntimeSystemRegistration
    {
        public static void Register(World world, GASSystemGroups groups)
        {
            groups.CommandResolve.AddSystemToUpdateList(world.CreateSystem(typeof(AbilityCatalogCommitSystem)));
            groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(typeof(GEEffectCommandCatalogNormalizeSystem)));
            groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(typeof(GEEffectSpecBuildSystem)));
            groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(typeof(GASActiveEffectMutationApplySystem)));
            groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(typeof(GASAttributeSetReduceApplySystem)));
            groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(typeof(GASActiveEffectPreTickSystem)));
            groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(typeof(GASActiveEffectRemoveSystem)));
        }
    }
}
