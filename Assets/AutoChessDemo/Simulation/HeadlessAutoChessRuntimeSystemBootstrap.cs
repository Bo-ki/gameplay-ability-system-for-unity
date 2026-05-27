using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public static class HeadlessAutoChessRuntimeSystemBootstrap
    {
        private static World _registeredWorld;

        public static void EnsureRuntimeReady()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize();

            RegisterSystems(GASManager.ExWorld);
            RegisterTargetCatcher();
            HeadlessAutoChessDefinitionSource.RegisterRuntimeProviders();
        }

        public static Entity RequestDefaultScenario(
            HeadlessAutoChessScenarioVariant variant = HeadlessAutoChessScenarioVariant.DefaultBalanced,
            int deterministicSeed = 0)
        {
            EnsureRuntimeReady();

            var em = GASManager.EntityManager;
            var request = em.CreateEntity();
            em.SetName(request, $"HeadlessAutoChessScenarioBootstrap_{HeadlessAutoChessScenario.ScenarioDefaultDuel}");
            em.AddComponentData(request, new CHeadlessAutoChessScenarioBootstrapRequest
            {
                ScenarioCode = HeadlessAutoChessScenario.ScenarioDefaultDuel,
                Variant = variant,
                DeterministicSeed = deterministicSeed,
            });
            return request;
        }

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

            if (!ReferenceEquals(_registeredWorld, world))
            {
                groups.Command.AddSystemToUpdateList(world.CreateSystem(typeof(SHeadlessAutoChessScenarioBootstrap)));
                groups.Command.AddSystemToUpdateList(world.CreateSystem(typeof(SHeadlessAutoChessAbilitySlotLink)));
                groups.Command.AddSystemToUpdateList(world.CreateSystem(typeof(SHeadlessAutoChessDriver)));
                groups.Command.AddSystemToUpdateList(world.CreateSystem(typeof(SHeadlessAutoChessSummonProjection)));
                groups.Command.AddSystemToUpdateList(world.CreateSystem(typeof(SHeadlessAutoChessFactProjection)));
                _registeredWorld = world;
            }

            GASSystemScheduleContract.SortSystems(fixedStepSimulation, groups);
        }

        private static void RegisterTargetCatcher()
        {
            TargetCatcherHelper.RegisterTargetCatcher(
                HeadlessAutoChessDefinitionSource.TargetCatcherName,
                typeof(CatchTarget),
                typeof(XParamNone));
        }
    }
}
