using System;
using Unity.Entities;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    public static class HeadlessAutoChessRuntimeSystemBootstrap
    {
        private static World _registeredWorld;

        public static void RegisterSystems(World world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            if (_registeredWorld == world)
                return;

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

            groups.CommandResolve.AddSystemToUpdateList(world.CreateSystem(typeof(AutoBattleCommandDriveSystem)));
            groups.ExecutionCalculationExtension.AddSystemToUpdateList(
                world.CreateSystem(typeof(AutoBattleExecuteDamageCalculationSystem)));
            GASSystemScheduleContract.SortSystems(fixedStepSimulation, groups);
            _registeredWorld = world;
        }

        public static void Reset()
        {
            _registeredWorld = null;
        }
    }
}
