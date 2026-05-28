using System;
using Unity.Entities;

namespace GAS.Runtime
{
    public static class HeadlessAutoChessRuntimeSystemBootstrap
    {
        public static void RegisterSystems(World world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            var groups = new GASSystemGroups(
                world.GetExistingSystemManaged<GASFramePrepareSystemGroup>(),
                world.GetExistingSystemManaged<GASCommandResolveSystemGroup>(),
                world.GetExistingSystemManaged<GASCoreSimulationSystemGroup>(),
                world.GetExistingSystemManaged<GEExecutionCalculationExtensionSystemGroup>(),
                world.GetExistingSystemManaged<GASStructuralCommitSystemGroup>(),
                world.GetExistingSystemManaged<BeginGASStructuralCommitECBSystem>(),
                world.GetExistingSystemManaged<EndGASStructuralCommitECBSystem>(),
                world.GetExistingSystemManaged<GASBoundaryProjectionSystemGroup>());
            var fixedStepSimulation = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();

            GASSystemScheduleContract.SortSystems(fixedStepSimulation, groups);
        }
    }
}
