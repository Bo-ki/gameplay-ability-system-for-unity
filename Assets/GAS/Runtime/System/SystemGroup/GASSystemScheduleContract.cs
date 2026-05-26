using System;
using System.Collections.Generic;
using Unity.Entities;

namespace GAS.Runtime
{
    public enum EGasRuntimeCoreFramePhase : byte
    {
        FramePrepare = 0,
        CommandIngest = 1,
        SpecEvaluation = 2,
        ActiveEffectLifecycle = 3,
        DeltaApply = 4,
        TypedFactProjection = 5,
        StructuralPlayback = 6,
        ObservationProjection = 7,
    }

    public enum EGasRuntimeCoreStructuralPermission : byte
    {
        None = 0,
        RecordOnly = 1,
        PlaybackOnly = 2,
    }

    [Flags]
    public enum EGasRuntimeCorePhaseAccess : int
    {
        None = 0,
        FrameState = 1 << 0,
        CommandStream = 1 << 1,
        SpecStream = 1 << 2,
        ActiveEffectStore = 1 << 3,
        AttributeState = 1 << 4,
        DeltaStream = 1 << 5,
        FactStream = 1 << 6,
        StructuralMutation = 1 << 7,
        Observation = 1 << 8,
    }

    public readonly struct GASRuntimeCoreFramePhaseContract
    {
        public GASRuntimeCoreFramePhaseContract(
            EGasRuntimeCoreFramePhase phase,
            Type currentGroupType,
            EGasRuntimeCorePhaseAccess reads,
            EGasRuntimeCorePhaseAccess writes,
            EGasRuntimeCoreStructuralPermission structuralPermission,
            bool observationBoundary = false,
            bool contractOnly = true)
        {
            Phase = phase;
            CurrentGroupType = currentGroupType;
            Reads = reads;
            Writes = writes;
            StructuralPermission = structuralPermission;
            ObservationBoundary = observationBoundary;
            ContractOnly = contractOnly;
        }

        public EGasRuntimeCoreFramePhase Phase { get; }

        public Type CurrentGroupType { get; }

        public EGasRuntimeCorePhaseAccess Reads { get; }

        public EGasRuntimeCorePhaseAccess Writes { get; }

        public EGasRuntimeCoreStructuralPermission StructuralPermission { get; }

        public bool ObservationBoundary { get; }

        public bool ContractOnly { get; }
    }

    public readonly struct GASRuntimeCoreFramePhaseSystemContract
    {
        public GASRuntimeCoreFramePhaseSystemContract(
            Type systemType,
            EGasRuntimeCoreFramePhase phase,
            Type currentGroupType,
            bool contractOnly = true)
        {
            SystemType = systemType;
            Phase = phase;
            CurrentGroupType = currentGroupType;
            ContractOnly = contractOnly;
        }

        public Type SystemType { get; }

        public EGasRuntimeCoreFramePhase Phase { get; }

        public Type CurrentGroupType { get; }

        public bool ContractOnly { get; }
    }

    public static class GASSystemScheduleContract
    {
        private static readonly GASRuntimeCoreFramePhaseContract[] RuntimeCoreFramePhaseContracts =
        {
            new GASRuntimeCoreFramePhaseContract(
                EGasRuntimeCoreFramePhase.FramePrepare,
                typeof(GASCommandGroup),
                EGasRuntimeCorePhaseAccess.FrameState,
                EGasRuntimeCorePhaseAccess.FrameState,
                EGasRuntimeCoreStructuralPermission.RecordOnly),
            new GASRuntimeCoreFramePhaseContract(
                EGasRuntimeCoreFramePhase.CommandIngest,
                typeof(GASCommandGroup),
                EGasRuntimeCorePhaseAccess.FrameState,
                EGasRuntimeCorePhaseAccess.CommandStream,
                EGasRuntimeCoreStructuralPermission.None),
            new GASRuntimeCoreFramePhaseContract(
                EGasRuntimeCoreFramePhase.SpecEvaluation,
                typeof(GASCommandGroup),
                EGasRuntimeCorePhaseAccess.CommandStream,
                EGasRuntimeCorePhaseAccess.SpecStream,
                EGasRuntimeCoreStructuralPermission.None),
            new GASRuntimeCoreFramePhaseContract(
                EGasRuntimeCoreFramePhase.ActiveEffectLifecycle,
                typeof(GASEffectGroup),
                EGasRuntimeCorePhaseAccess.ActiveEffectStore | EGasRuntimeCorePhaseAccess.SpecStream,
                EGasRuntimeCorePhaseAccess.ActiveEffectStore | EGasRuntimeCorePhaseAccess.StructuralMutation,
                EGasRuntimeCoreStructuralPermission.RecordOnly),
            new GASRuntimeCoreFramePhaseContract(
                EGasRuntimeCoreFramePhase.DeltaApply,
                typeof(GASAttributeGroup),
                EGasRuntimeCorePhaseAccess.SpecStream | EGasRuntimeCorePhaseAccess.ActiveEffectStore,
                EGasRuntimeCorePhaseAccess.AttributeState | EGasRuntimeCorePhaseAccess.DeltaStream,
                EGasRuntimeCoreStructuralPermission.None),
            new GASRuntimeCoreFramePhaseContract(
                EGasRuntimeCoreFramePhase.TypedFactProjection,
                typeof(GASAttributeGroup),
                EGasRuntimeCorePhaseAccess.DeltaStream | EGasRuntimeCorePhaseAccess.AttributeState,
                EGasRuntimeCorePhaseAccess.FactStream,
                EGasRuntimeCoreStructuralPermission.None),
            new GASRuntimeCoreFramePhaseContract(
                EGasRuntimeCoreFramePhase.StructuralPlayback,
                typeof(GasStructuralPlaybackSystemGroup),
                EGasRuntimeCorePhaseAccess.StructuralMutation,
                EGasRuntimeCorePhaseAccess.FrameState,
                EGasRuntimeCoreStructuralPermission.PlaybackOnly),
            new GASRuntimeCoreFramePhaseContract(
                EGasRuntimeCoreFramePhase.ObservationProjection,
                typeof(GASCueGroup),
                EGasRuntimeCorePhaseAccess.FactStream | EGasRuntimeCorePhaseAccess.AttributeState,
                EGasRuntimeCorePhaseAccess.Observation,
                EGasRuntimeCoreStructuralPermission.None,
                observationBoundary: true),
        };

        private static readonly GASRuntimeCoreFramePhaseSystemContract[] RuntimeCoreFramePhaseSystemContracts =
        {
            new GASRuntimeCoreFramePhaseSystemContract(
                typeof(SEffectCommandIngest),
                EGasRuntimeCoreFramePhase.CommandIngest,
                typeof(GASCommandGroup)),
            new GASRuntimeCoreFramePhaseSystemContract(
                typeof(SInstantEffectSpecBuild),
                EGasRuntimeCoreFramePhase.SpecEvaluation,
                typeof(GASCommandGroup)),
            new GASRuntimeCoreFramePhaseSystemContract(
                typeof(SActiveEffectMutationApply),
                EGasRuntimeCoreFramePhase.ActiveEffectLifecycle,
                typeof(GASCommandGroup)),
            new GASRuntimeCoreFramePhaseSystemContract(
                typeof(SAttributeDeltaApply),
                EGasRuntimeCoreFramePhase.DeltaApply,
                typeof(GASCommandGroup)),
            new GASRuntimeCoreFramePhaseSystemContract(
                typeof(STypedSimulationFactProjection),
                EGasRuntimeCoreFramePhase.TypedFactProjection,
                typeof(GASCommandGroup)),
            new GASRuntimeCoreFramePhaseSystemContract(
                typeof(STypedSimulationFactEventBridge),
                EGasRuntimeCoreFramePhase.TypedFactProjection,
                typeof(GASCommandGroup)),
            new GASRuntimeCoreFramePhaseSystemContract(
                typeof(SInstantEffectCueRequestProjection),
                EGasRuntimeCoreFramePhase.TypedFactProjection,
                typeof(GASCommandGroup)),
            new GASRuntimeCoreFramePhaseSystemContract(
                typeof(SRuntimeCoreDebuggerCounters),
                EGasRuntimeCoreFramePhase.ObservationProjection,
                typeof(GASCueGroup)),
        };

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
            typeof(SAbilityCommandRequest),
            typeof(STryActivateAbility),
            typeof(SAbilityCommit),
            typeof(SAbilityTimelineAction),
            typeof(SAbilityTimelineLifecycleRequest),
            typeof(SEffectCommandIngest),
            typeof(SInstantEffectSpecBuild),
            typeof(SActiveEffectMutationApply),
            typeof(SAttributeDeltaApply),
            typeof(STypedSimulationFactProjection),
            typeof(STypedSimulationFactEventBridge),
            typeof(SInstantEffectCueRequestProjection),
            typeof(SAscDestroyRequest),
            typeof(SExecutionCalculation),
            typeof(GASExecutionCalculationExtensionGroup),
            typeof(SExecutionCalculationOutputModifier),
        };

        private static readonly Type[] EffectCommandSpecStreamTargetSystemTypes =
        {
            typeof(SEffectCommandIngest),
            typeof(SInstantEffectSpecBuild),
            typeof(SActiveEffectMutationApply),
            typeof(SAttributeDeltaApply),
            typeof(STypedSimulationFactProjection),
        };

        private static readonly Type[] ExecutionCalculationExtensionSystemTypes =
        {
            typeof(SHeadlessAutoBattleExecuteCalculation),
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
            typeof(SEffectRemove),
            typeof(SEffectFinalDestroy),
            typeof(SEffectTick),
        };

        private static readonly Type[] AttributeSystemTypes =
        {
            typeof(SAttributeRecalculate),
            typeof(SAttributeChangeEventProjection),
        };

        private static readonly Type[] AbilitySystemTypes =
        {
            typeof(SAbilityTick),
            typeof(SAttributeThresholdAbilityLifecycleRequest),
            typeof(SAbilityLifecycleRequest),
            typeof(SAbilityStateCleanup),
        };

        private static readonly Type[] CueSystemTypes =
        {
            typeof(SPresentationOutboxProjection),
            typeof(SDebugReplayLogProjection),
            typeof(SRuntimeCoreDebuggerCounters),
            typeof(SCueRequestBridge),
            typeof(SCueStart),
            typeof(SCueTick),
            typeof(SCueEnd),
            typeof(SCueDestroy),
            typeof(SAscDestroyFinalize),
        };

        public static IReadOnlyList<Type> FixedStepGroups => FixedStepGroupTypes;

        public static IReadOnlyList<GASRuntimeCoreFramePhaseContract> RuntimeCoreFramePhases =>
            RuntimeCoreFramePhaseContracts;

        public static IReadOnlyList<GASRuntimeCoreFramePhaseSystemContract> RuntimeCoreFramePhaseSystems =>
            RuntimeCoreFramePhaseSystemContracts;

        public static IReadOnlyList<Type> CommandSystems => CommandSystemTypes;

        public static IReadOnlyList<Type> EffectCommandSpecStreamTargetSystems =>
            EffectCommandSpecStreamTargetSystemTypes;

        public static IReadOnlyList<Type> ExecutionCalculationExtensionSystems => ExecutionCalculationExtensionSystemTypes;

        public static IReadOnlyList<Type> ResetDirtySystems => ResetDirtySystemTypes;

        public static IReadOnlyList<Type> TagSystems => TagSystemTypes;

        public static IReadOnlyList<Type> EffectSystems => EffectSystemTypes;

        public static IReadOnlyList<Type> AttributeSystems => AttributeSystemTypes;

        public static IReadOnlyList<Type> AbilitySystems => AbilitySystemTypes;

        public static IReadOnlyList<Type> CueSystems => CueSystemTypes;

        public static bool TryGetRuntimeCoreFramePhase(
            Type systemType,
            out EGasRuntimeCoreFramePhase phase)
        {
            for (var i = 0; i < RuntimeCoreFramePhaseSystemContracts.Length; i++)
            {
                var contract = RuntimeCoreFramePhaseSystemContracts[i];
                if (contract.SystemType != systemType)
                    continue;

                phase = contract.Phase;
                return true;
            }

            phase = default;
            return false;
        }

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
