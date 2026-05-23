using System;
using System.Collections.Generic;
using Unity.Entities;

namespace GAS.Runtime
{
    public static class GASSystemScheduleContract
    {
        private static readonly Type[] FixedStepGroupTypes =
        {
            typeof(GASCommandGroup),
            typeof(GASResetDirtyGroup),
            typeof(GASTagGroup),
            typeof(GASEffectGroup),
            typeof(GASAttributeGroup),
            typeof(GASAbilityGroup),
            typeof(GASCueGroup),
        };

        private static readonly Type[] CommandSystemTypes =
        {
            typeof(SEventBusClear),
            typeof(SGlobalTimer),
            typeof(SASCCreate),
            typeof(SAscInitializeRequest),
            typeof(SAscCommandRequest),
            typeof(SHeadlessAutoBattleDriver),
            typeof(SHeadlessAutoChessDriver),
            typeof(SAbilityCommandRequest),
            typeof(STryActivateAbility),
            typeof(SAbilityCommit),
            typeof(SAbilityTimelineAction),
            typeof(SAbilityTimelineLifecycleRequest),
            typeof(SRemoveGameplayEffectRequest),
            typeof(SAscDestroyRequest),
            typeof(SApplyGameplayEffectRequest),
            typeof(SExecutionCalculation),
            typeof(GASExecutionCalculationExtensionGroup),
            typeof(SExecutionCalculationOutputModifier),
        };

        private static readonly Type[] ExecutionCalculationExtensionSystemTypes =
        {
            typeof(SHeadlessAutoBattleExecuteCalculation),
            typeof(SHeadlessAutoChessShieldDamageCalculation),
        };

        private static readonly Type[] ResetDirtySystemTypes =
        {
        };

        private static readonly Type[] TagSystemTypes =
        {
            typeof(STagChangeProcess),
        };

        private static readonly Type[] EffectSystemTypes =
        {
            typeof(SEffectApply),
            typeof(SOngoingTagRequirements),
            typeof(SEffectRemove),
            typeof(SEffectTick),
        };

        private static readonly Type[] AttributeSystemTypes =
        {
            typeof(SAttributeRecalculate),
            typeof(SAttributeChangeEventProjection),
            typeof(SHeadlessAutoChessBattleFactProjection),
        };

        private static readonly Type[] AbilitySystemTypes =
        {
            typeof(SAbilityTick),
            typeof(SAttributeThresholdAbilityLifecycleRequest),
            typeof(SAbilityLifecycleRequest),
            typeof(SAbilityStateCleanup),
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
            typeof(SPresentationOutboxProjection),
            typeof(SDebugReplayLogProjection),
            typeof(SCueRequestBridge),
            typeof(SCueStart),
            typeof(SCueTick),
            typeof(SCueEnd),
            typeof(SCueDestroy),
            typeof(SAscDestroyFinalize),
        };

        public static IReadOnlyList<Type> FixedStepGroups => FixedStepGroupTypes;

        public static IReadOnlyList<Type> CommandSystems => CommandSystemTypes;

        public static IReadOnlyList<Type> ExecutionCalculationExtensionSystems => ExecutionCalculationExtensionSystemTypes;

        public static IReadOnlyList<Type> ResetDirtySystems => ResetDirtySystemTypes;

        public static IReadOnlyList<Type> TagSystems => TagSystemTypes;

        public static IReadOnlyList<Type> EffectSystems => EffectSystemTypes;

        public static IReadOnlyList<Type> AttributeSystems => AttributeSystemTypes;

        public static IReadOnlyList<Type> AbilitySystems => AbilitySystemTypes;

        public static IReadOnlyList<Type> CueSystems => CueSystemTypes;

        public static GASSystemGroups CreateFixedStepGroups(
            World world,
            FixedStepSimulationSystemGroup fixedStepSimulation)
        {
            var groups = new GASSystemGroups(
                world.CreateSystemManaged<GASCommandGroup>(),
                world.CreateSystemManaged<GASExecutionCalculationExtensionGroup>(),
                world.CreateSystemManaged<GASResetDirtyGroup>(),
                world.CreateSystemManaged<GASTagGroup>(),
                world.CreateSystemManaged<GASEffectGroup>(),
                world.CreateSystemManaged<GASAttributeGroup>(),
                world.CreateSystemManaged<GASAbilityGroup>(),
                world.CreateSystemManaged<GASCueGroup>());

            fixedStepSimulation.AddSystemToUpdateList(groups.Command);
            fixedStepSimulation.AddSystemToUpdateList(groups.ResetDirty);
            fixedStepSimulation.AddSystemToUpdateList(groups.Tag);
            fixedStepSimulation.AddSystemToUpdateList(groups.Effect);
            fixedStepSimulation.AddSystemToUpdateList(groups.Attribute);
            fixedStepSimulation.AddSystemToUpdateList(groups.Ability);
            fixedStepSimulation.AddSystemToUpdateList(groups.Cue);

            return groups;
        }

        public static void RegisterSystems(World world, GASSystemGroups groups)
        {
            AddCommandSystems(world, groups);
            AddSystems(world, groups.ExecutionCalculationExtension, ExecutionCalculationExtensionSystemTypes);
            AddSystems(world, groups.ResetDirty, ResetDirtySystemTypes);
            AddSystems(world, groups.Tag, TagSystemTypes);
            AddSystems(world, groups.Effect, EffectSystemTypes);
            AddSystems(world, groups.Attribute, AttributeSystemTypes);
            AddSystems(world, groups.Ability, AbilitySystemTypes);
            AddSystems(world, groups.Cue, CueSystemTypes);
        }

        public static void SortSystems(
            FixedStepSimulationSystemGroup fixedStepSimulation,
            GASSystemGroups groups)
        {
            groups.ExecutionCalculationExtension.SortSystems();
            groups.Command.SortSystems();
            groups.ResetDirty.SortSystems();
            groups.Tag.SortSystems();
            groups.Effect.SortSystems();
            groups.Attribute.SortSystems();
            groups.Ability.SortSystems();
            groups.Cue.SortSystems();
            fixedStepSimulation.SortSystems();
        }

        private static void AddSystems(
            World world,
            ComponentSystemGroup group,
            IReadOnlyList<Type> systemTypes)
        {
            for (var i = 0; i < systemTypes.Count; i++)
                group.AddSystemToUpdateList(world.CreateSystem(systemTypes[i]));
        }

        private static void AddCommandSystems(World world, GASSystemGroups groups)
        {
            for (var i = 0; i < CommandSystemTypes.Length; i++)
            {
                var systemType = CommandSystemTypes[i];
                if (systemType == typeof(GASExecutionCalculationExtensionGroup))
                {
                    groups.Command.AddSystemToUpdateList(groups.ExecutionCalculationExtension);
                    continue;
                }

                groups.Command.AddSystemToUpdateList(world.CreateSystem(systemType));
            }
        }
    }

    public readonly struct GASSystemGroups
    {
        public GASSystemGroups(
            GASCommandGroup command,
            GASExecutionCalculationExtensionGroup executionCalculationExtension,
            GASResetDirtyGroup resetDirty,
            GASTagGroup tag,
            GASEffectGroup effect,
            GASAttributeGroup attribute,
            GASAbilityGroup ability,
            GASCueGroup cue)
        {
            Command = command;
            ExecutionCalculationExtension = executionCalculationExtension;
            ResetDirty = resetDirty;
            Tag = tag;
            Effect = effect;
            Attribute = attribute;
            Ability = ability;
            Cue = cue;
        }

        public GASCommandGroup Command { get; }

        public GASExecutionCalculationExtensionGroup ExecutionCalculationExtension { get; }

        public GASResetDirtyGroup ResetDirty { get; }

        public GASTagGroup Tag { get; }

        public GASEffectGroup Effect { get; }

        public GASAttributeGroup Attribute { get; }

        public GASAbilityGroup Ability { get; }

        public GASCueGroup Cue { get; }
    }
}
