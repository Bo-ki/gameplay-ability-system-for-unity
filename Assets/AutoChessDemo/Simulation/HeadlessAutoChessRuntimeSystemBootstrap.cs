using System;
using System.Collections.Generic;
using Unity.Collections;
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
                world.GetExistingSystemManaged<GASCommandGroup>(),
                world.GetExistingSystemManaged<GASExecutionCalculationExtensionGroup>(),
                world.GetExistingSystemManaged<GASResetDirtyGroup>(),
                world.GetExistingSystemManaged<GASTagGroup>(),
                world.GetExistingSystemManaged<GASEffectGroup>(),
                world.GetExistingSystemManaged<GASAttributeGroup>(),
                world.GetExistingSystemManaged<GASAbilityGroup>(),
                world.GetExistingSystemManaged<GASCueGroup>());
            var fixedStepSimulation = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();

            GASSystemScheduleContract.SortSystems(fixedStepSimulation, groups);
        }
    }
}
