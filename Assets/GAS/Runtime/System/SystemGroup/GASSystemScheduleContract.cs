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
        private const string GeneratedRuntimeAssemblyName = "com.exhard.exgas.generated.runtime";

        private static readonly GASRuntimeCoreFramePhaseContract[] RuntimeCoreFramePhaseContracts =
        {
            new(
                EGasRuntimeCoreFramePhase.FramePrepare,
                typeof(GASFramePrepareSystemGroup),
                EGasRuntimeCorePhaseAccess.FrameState,
                EGasRuntimeCorePhaseAccess.FrameState,
                EGasRuntimeCoreStructuralPermission.None),
            new(
                EGasRuntimeCoreFramePhase.CommandIngest,
                typeof(GASCommandResolveSystemGroup),
                EGasRuntimeCorePhaseAccess.FrameState,
                EGasRuntimeCorePhaseAccess.CommandStream | EGasRuntimeCorePhaseAccess.StructuralMutation,
                EGasRuntimeCoreStructuralPermission.RecordOnly),
            new(
                EGasRuntimeCoreFramePhase.SpecEvaluation,
                typeof(GASCoreSimulationSystemGroup),
                EGasRuntimeCorePhaseAccess.CommandStream,
                EGasRuntimeCorePhaseAccess.SpecStream,
                EGasRuntimeCoreStructuralPermission.None),
            new(
                EGasRuntimeCoreFramePhase.ActiveEffectLifecycle,
                typeof(GASCoreSimulationSystemGroup),
                EGasRuntimeCorePhaseAccess.ActiveEffectStore | EGasRuntimeCorePhaseAccess.SpecStream,
                EGasRuntimeCorePhaseAccess.ActiveEffectStore | EGasRuntimeCorePhaseAccess.StructuralMutation,
                EGasRuntimeCoreStructuralPermission.RecordOnly),
            new(
                EGasRuntimeCoreFramePhase.DeltaApply,
                typeof(GASCoreSimulationSystemGroup),
                EGasRuntimeCorePhaseAccess.SpecStream | EGasRuntimeCorePhaseAccess.ActiveEffectStore,
                EGasRuntimeCorePhaseAccess.AttributeState | EGasRuntimeCorePhaseAccess.DeltaStream,
                EGasRuntimeCoreStructuralPermission.None),
            new(
                EGasRuntimeCoreFramePhase.TypedFactProjection,
                typeof(GASCoreSimulationSystemGroup),
                EGasRuntimeCorePhaseAccess.DeltaStream | EGasRuntimeCorePhaseAccess.AttributeState,
                EGasRuntimeCorePhaseAccess.FactStream,
                EGasRuntimeCoreStructuralPermission.None),
            new(
                EGasRuntimeCoreFramePhase.StructuralPlayback,
                typeof(GASStructuralCommitSystemGroup),
                EGasRuntimeCorePhaseAccess.StructuralMutation,
                EGasRuntimeCorePhaseAccess.FrameState,
                EGasRuntimeCoreStructuralPermission.PlaybackOnly),
            new(
                EGasRuntimeCoreFramePhase.ObservationProjection,
                typeof(GASBoundaryProjectionSystemGroup),
                EGasRuntimeCorePhaseAccess.FactStream | EGasRuntimeCorePhaseAccess.AttributeState,
                EGasRuntimeCorePhaseAccess.Observation,
                EGasRuntimeCoreStructuralPermission.None,
                observationBoundary: true),
        };

        private static readonly GASRuntimeCoreFramePhaseSystemContract[] RuntimeCoreFramePhaseSystemContracts =
        {
            new(
                typeof(GameplayEventBusClearSystem),
                EGasRuntimeCoreFramePhase.FramePrepare,
                typeof(GASFramePrepareSystemGroup)),
            new(
                typeof(GASGlobalTimerSystem),
                EGasRuntimeCoreFramePhase.FramePrepare,
                typeof(GASFramePrepareSystemGroup)),
            new(
                typeof(GEEffectCommandSpecStreamFramePrepareSystem),
                EGasRuntimeCoreFramePhase.FramePrepare,
                typeof(GASFramePrepareSystemGroup)),
            new(
                typeof(ActiveEffectOwnerLocalMutationFramePrepareSystem),
                EGasRuntimeCoreFramePhase.FramePrepare,
                typeof(GASFramePrepareSystemGroup)),
            new(
                typeof(GameplayOwnerLocalFactFramePrepareSystem),
                EGasRuntimeCoreFramePhase.FramePrepare,
                typeof(GASFramePrepareSystemGroup)),
            new(
                typeof(GASAttributeModifierDeltaApplySystem),
                EGasRuntimeCoreFramePhase.DeltaApply,
                typeof(GASCoreSimulationSystemGroup)),
            new(
                typeof(GameplayOwnerLocalFactFlushSystem),
                EGasRuntimeCoreFramePhase.TypedFactProjection,
                typeof(GASCoreSimulationSystemGroup)),
            new(
                typeof(GameplayFactProjectionSystem),
                EGasRuntimeCoreFramePhase.TypedFactProjection,
                typeof(GASCoreSimulationSystemGroup)),
            new(
                typeof(GameplayFactBoundaryProjectionSystem),
                EGasRuntimeCoreFramePhase.ObservationProjection,
                typeof(GASBoundaryProjectionSystemGroup)),
            new(
                typeof(DiagnosticsSnapshotSystem),
                EGasRuntimeCoreFramePhase.ObservationProjection,
                typeof(GASBoundaryProjectionSystemGroup)),
        };

        private static readonly Type[] FixedStepGroupTypes =
        {
            typeof(GASFramePrepareSystemGroup),
            typeof(GASCommandResolveSystemGroup),
            typeof(GASCoreSimulationSystemGroup),
            typeof(GASStructuralCommitSystemGroup),
            typeof(GASBoundaryProjectionSystemGroup),
        };

        private static readonly Type[] FramePrepareSystemTypes =
        {
            typeof(GameplayEventBusClearSystem),
            typeof(GASGlobalTimerSystem),
            typeof(GEEffectCommandSpecStreamFramePrepareSystem),
            typeof(ActiveEffectOwnerLocalMutationFramePrepareSystem),
            typeof(GameplayOwnerLocalFactFramePrepareSystem),
        };

        private static readonly Type[] CommandResolveSystemTypes =
        {
            typeof(ASCCommandBufferResolveSystem),
            typeof(AbilityTryActivateSystem),
            typeof(AbilityCommitSystem),
        };

        private static readonly string[] GeneratedCommandResolveSystemTypeNames =
        {
            "GAS.Runtime.Generated.AbilityCatalogCommitSystem, " + GeneratedRuntimeAssemblyName,
        };

        private static readonly Type[] CoreSimulationSystemTypes =
        {
            typeof(GEExecutionCalculationSystem),
            typeof(GEExecutionCalculationExtensionSystemGroup),
            typeof(GEExecutionCalculationOutputModifierSystem),
            typeof(GASAttributeModifierDeltaApplySystem),
            typeof(AttributeOwnerMarkerRequestSystem),
            typeof(AttributeRecalculateSystem),
            typeof(GameplayTagChangeProcessSystem),
            typeof(AbilityStateTickSystem),
            typeof(AttributeThresholdAbilityLifecycleRequestSystem),
            typeof(AbilityLifecycleRequestSystem),
            typeof(AbilityStateCleanupSystem),
            typeof(GameplayOwnerLocalFactFlushSystem),
            typeof(GameplayFactProjectionSystem),
        };

        private static readonly string[] GeneratedCoreSimulationSystemTypeNames =
        {
            "GAS.Runtime.Generated.GEEffectCommandCatalogNormalizeSystem, " + GeneratedRuntimeAssemblyName,
            "GAS.Runtime.Generated.GEEffectSpecBuildSystem, " + GeneratedRuntimeAssemblyName,
            "GAS.Runtime.Generated.GASActiveEffectMutationApplySystem, " + GeneratedRuntimeAssemblyName,
            "GAS.Runtime.Generated.GASAttributeSetReduceApplySystem, " + GeneratedRuntimeAssemblyName,
            "GAS.Runtime.Generated.GASActiveEffectPreTickSystem, " + GeneratedRuntimeAssemblyName,
            "GAS.Runtime.Generated.GASActiveEffectRemoveSystem, " + GeneratedRuntimeAssemblyName,
        };

        private static readonly Type[] StructuralCommitSystemTypes =
        {
            typeof(BeginGASStructuralCommitECBSystem),
            typeof(EndGASStructuralCommitECBSystem),
        };

        private static readonly Type[] BoundaryProjectionSystemTypes =
        {
            typeof(GameplayFactBoundaryProjectionSystem),
            typeof(PresentationOutboxProjectionSystem),
            typeof(ReplayLogSystem),
            typeof(DiagnosticsSnapshotSystem),
            typeof(ASCDestroyFinalizeSystem),
        };

        private static readonly Type[] EffectCommandSpecStreamTargetSystemTypes =
        {
            typeof(GEEffectCommandSpecStreamFramePrepareSystem),
            typeof(GameplayOwnerLocalFactFlushSystem),
            typeof(GameplayFactProjectionSystem),
            typeof(GameplayFactBoundaryProjectionSystem),
        };

        public static IReadOnlyList<Type> FixedStepGroups => FixedStepGroupTypes;

        public static IReadOnlyList<GASRuntimeCoreFramePhaseContract> RuntimeCoreFramePhases =>
            RuntimeCoreFramePhaseContracts;

        public static IReadOnlyList<GASRuntimeCoreFramePhaseSystemContract> RuntimeCoreFramePhaseSystems =>
            RuntimeCoreFramePhaseSystemContracts;

        public static IReadOnlyList<Type> FramePrepareSystems => FramePrepareSystemTypes;

        public static IReadOnlyList<Type> CommandResolveSystems => CommandResolveSystemTypes;

        public static IReadOnlyList<Type> CoreSimulationSystems => CoreSimulationSystemTypes;

        public static IReadOnlyList<Type> StructuralCommitSystems => StructuralCommitSystemTypes;

        public static IReadOnlyList<Type> BoundaryProjectionSystems => BoundaryProjectionSystemTypes;

        public static IReadOnlyList<Type> EffectCommandSpecStreamTargetSystems =>
            EffectCommandSpecStreamTargetSystemTypes;

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
                world.CreateSystemManaged<GASFramePrepareSystemGroup>(),
                world.CreateSystemManaged<GASCommandResolveSystemGroup>(),
                world.CreateSystemManaged<GASCoreSimulationSystemGroup>(),
                world.CreateSystemManaged<GEExecutionCalculationExtensionSystemGroup>(),
                world.CreateSystemManaged<GASStructuralCommitSystemGroup>(),
                world.CreateSystemManaged<BeginGASStructuralCommitECBSystem>(),
                world.CreateSystemManaged<EndGASStructuralCommitECBSystem>(),
                world.CreateSystemManaged<GASBoundaryProjectionSystemGroup>());

            fixedStepSimulation.AddSystemToUpdateList(groups.FramePrepare);
            fixedStepSimulation.AddSystemToUpdateList(groups.CommandResolve);
            fixedStepSimulation.AddSystemToUpdateList(groups.CoreSimulation);
            fixedStepSimulation.AddSystemToUpdateList(groups.StructuralCommit);
            fixedStepSimulation.AddSystemToUpdateList(groups.BoundaryProjection);

            return groups;
        }

        public static void RegisterSystems(World world, GASSystemGroups groups)
        {
            AddSystems(world, groups.FramePrepare, FramePrepareSystemTypes);
            AddSystems(world, groups.CommandResolve, CommandResolveSystemTypes);
            AddCoreSimulationSystems(world, groups);
            AddGeneratedRuntimeSystems(world, groups);
            groups.StructuralCommit.AddSystemToUpdateList(groups.BeginStructuralCommitECB);
            groups.StructuralCommit.AddSystemToUpdateList(groups.EndStructuralCommitECB);
            AddSystems(world, groups.BoundaryProjection, BoundaryProjectionSystemTypes);
        }

        public static void SortSystems(
            FixedStepSimulationSystemGroup fixedStepSimulation,
            GASSystemGroups groups)
        {
            groups.FramePrepare.SortSystems();
            groups.CommandResolve.SortSystems();
            groups.ExecutionCalculationExtension.SortSystems();
            groups.CoreSimulation.SortSystems();
            groups.StructuralCommit.SortSystems();
            groups.BoundaryProjection.SortSystems();
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

        private static void AddCoreSimulationSystems(World world, GASSystemGroups groups)
        {
            for (var i = 0; i < CoreSimulationSystemTypes.Length; i++)
            {
                var systemType = CoreSimulationSystemTypes[i];
                if (systemType == typeof(GEExecutionCalculationExtensionSystemGroup))
                {
                    groups.CoreSimulation.AddSystemToUpdateList(groups.ExecutionCalculationExtension);
                    continue;
                }

                groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(systemType));
            }
        }

        private static void AddGeneratedRuntimeSystems(World world, GASSystemGroups groups)
        {
            AddSystemsByTypeName(world, groups.CommandResolve, GeneratedCommandResolveSystemTypeNames);
            AddSystemsByTypeName(world, groups.CoreSimulation, GeneratedCoreSimulationSystemTypeNames);
        }

        private static void AddSystemsByTypeName(
            World world,
            ComponentSystemGroup group,
            IReadOnlyList<string> systemTypeNames)
        {
            for (var i = 0; i < systemTypeNames.Count; i++)
            {
                var systemTypeName = systemTypeNames[i];
                var systemType = Type.GetType(systemTypeName);
                if (systemType == null)
                    throw new InvalidOperationException(
                        $"Generated GAS runtime system type is missing: {systemTypeName}");

                group.AddSystemToUpdateList(world.CreateSystem(systemType));
            }
        }
    }

    public readonly struct GASSystemGroups
    {
        public GASSystemGroups(
            GASFramePrepareSystemGroup framePrepare,
            GASCommandResolveSystemGroup commandResolve,
            GASCoreSimulationSystemGroup coreSimulation,
            GEExecutionCalculationExtensionSystemGroup executionCalculationExtension,
            GASStructuralCommitSystemGroup structuralCommit,
            BeginGASStructuralCommitECBSystem beginStructuralCommitECB,
            EndGASStructuralCommitECBSystem endStructuralCommitECB,
            GASBoundaryProjectionSystemGroup boundaryProjection)
        {
            FramePrepare = framePrepare;
            CommandResolve = commandResolve;
            CoreSimulation = coreSimulation;
            ExecutionCalculationExtension = executionCalculationExtension;
            StructuralCommit = structuralCommit;
            BeginStructuralCommitECB = beginStructuralCommitECB;
            EndStructuralCommitECB = endStructuralCommitECB;
            BoundaryProjection = boundaryProjection;
        }

        public GASFramePrepareSystemGroup FramePrepare { get; }

        public GASCommandResolveSystemGroup CommandResolve { get; }

        public GASCoreSimulationSystemGroup CoreSimulation { get; }

        public GEExecutionCalculationExtensionSystemGroup ExecutionCalculationExtension { get; }

        public GASStructuralCommitSystemGroup StructuralCommit { get; }

        public BeginGASStructuralCommitECBSystem BeginStructuralCommitECB { get; }

        public EndGASStructuralCommitECBSystem EndStructuralCommitECB { get; }

        public GASBoundaryProjectionSystemGroup BoundaryProjection { get; }
    }
}
