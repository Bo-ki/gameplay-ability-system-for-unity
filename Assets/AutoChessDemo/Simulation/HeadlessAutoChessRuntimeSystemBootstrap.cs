using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public static class HeadlessAutoChessRuntimeSystemBootstrap
    {
        private static readonly Type[] CommandSystemTypes =
        {
            typeof(SHeadlessAutoChessDriver),
        };

        private static readonly Type[] ExecutionCalculationExtensionSystemTypes =
        {
            typeof(SHeadlessAutoChessShieldDamageCalculation),
        };

        private static readonly Type[] AttributeSystemTypes =
        {
            typeof(SHeadlessAutoChessBattleFactProjection),
        };

        private static readonly Type[] AbilitySystemTypes =
        {
            typeof(SHeadlessAutoChessGameplayEffectFactProjection),
            typeof(SHeadlessAutoChessSummonLifecycle),
            typeof(SHeadlessAutoChessPassiveReaction),
            typeof(SHeadlessAutoChessEnrageReaction),
            typeof(SHeadlessAutoChessCounterReaction),
            typeof(SHeadlessAutoChessCleanseReaction),
            typeof(SHeadlessAutoChessRallyComboReaction),
            typeof(SHeadlessAutoChessLifeStealReaction),
            typeof(SHeadlessAutoChessPoisonReaction),
            typeof(SHeadlessAutoChessExecuteReaction),
            typeof(SHeadlessAutoChessDeathBurstReaction),
            typeof(SHeadlessAutoChessSynergyProjection),
        };

        private static readonly Type[] CueSystemTypes =
        {
            typeof(SHeadlessAutoChessPresentationCueMarkerProjection),
        };

        public static IReadOnlyList<Type> CommandSystems => CommandSystemTypes;

        public static IReadOnlyList<Type> ExecutionCalculationExtensionSystems =>
            ExecutionCalculationExtensionSystemTypes;

        public static IReadOnlyList<Type> AttributeSystems => AttributeSystemTypes;

        public static IReadOnlyList<Type> AbilitySystems => AbilitySystemTypes;

        public static IReadOnlyList<Type> CueSystems => CueSystemTypes;

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

            AddSystems(world, groups.Command, CommandSystemTypes);
            AddSystems(world, groups.ExecutionCalculationExtension, ExecutionCalculationExtensionSystemTypes);
            AddSystems(world, groups.Attribute, AttributeSystemTypes);
            AddSystems(world, groups.Ability, AbilitySystemTypes);
            AddSystems(world, groups.Cue, CueSystemTypes);

            GASSystemScheduleContract.SortSystems(fixedStepSimulation, groups);
        }

        private static void AddSystems(
            World world,
            ComponentSystemGroup group,
            IReadOnlyList<Type> systemTypes)
        {
            for (var i = 0; i < systemTypes.Count; i++)
            {
                var systemType = systemTypes[i];
                if (ContainsSystem(world, group, systemType))
                    continue;

                group.AddSystemToUpdateList(world.CreateSystem(systemType));
            }
        }

        private static bool ContainsSystem(
            World world,
            ComponentSystemGroup group,
            Type systemType)
        {
            var targetIndex = TypeManager.GetSystemTypeIndex(systemType).Index;
            using var systems = group.GetAllSystems(Allocator.Temp);
            for (var i = 0; i < systems.Length; i++)
            {
                if (world.Unmanaged.GetSystemTypeIndex(systems[i]).Index == targetIndex)
                    return true;
            }

            return false;
        }
    }
}
