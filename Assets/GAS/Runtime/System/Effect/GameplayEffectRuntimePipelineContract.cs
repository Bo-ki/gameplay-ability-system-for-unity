using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    public enum GameplayEffectRuntimePipelineKind
    {
        None = 0,
        LegacyInstantEntityLifecycle = 1,
        EffectCommandSpecStream = 2,
        ActiveEffectStore = 3,
    }

    public enum GameplayEffectRuntimePipelineStatus
    {
        None = 0,
        LegacyFrozen = 1,
        TargetContract = 2,
        Planned = 3,
    }

    [Flags]
    public enum GameplayEffectRuntimePipelineRestriction
    {
        None = 0,
        MigrationOnly = 1 << 0,
        NoNewFeatureExpansion = 1 << 1,
        NotDefaultNewBusinessPath = 1 << 2,
        NotHighFrequencyReactionInput = 1 << 3,
        AvoidsRuntimeGameplayEffectEntity = 1 << 4,
        OwnsActiveEffectState = 1 << 5,
    }

    public readonly struct GameplayEffectRuntimePipelineContractEntry
    {
        public readonly GameplayEffectRuntimePipelineKind Kind;
        public readonly GameplayEffectRuntimePipelineStatus Status;
        public readonly GameplayEffectRuntimePipelineRestriction Restrictions;
        public readonly string WriteEntryName;
        public readonly string EvaluationName;
        public readonly string OutputName;
        public readonly Type WriteEntryType;
        public readonly Type EvaluationType;
        public readonly Type OutputType;
        public readonly Type FactType;

        public GameplayEffectRuntimePipelineContractEntry(
            GameplayEffectRuntimePipelineKind kind,
            GameplayEffectRuntimePipelineStatus status,
            GameplayEffectRuntimePipelineRestriction restrictions,
            string writeEntryName,
            string evaluationName,
            string outputName,
            Type writeEntryType = null,
            Type evaluationType = null,
            Type outputType = null,
            Type factType = null)
        {
            Kind = kind;
            Status = status;
            Restrictions = restrictions;
            WriteEntryName = writeEntryName ?? string.Empty;
            EvaluationName = evaluationName ?? string.Empty;
            OutputName = outputName ?? string.Empty;
            WriteEntryType = writeEntryType;
            EvaluationType = evaluationType;
            OutputType = outputType;
            FactType = factType;
        }

        public bool HasRestriction(GameplayEffectRuntimePipelineRestriction restriction)
        {
            return restriction != GameplayEffectRuntimePipelineRestriction.None
                   && (Restrictions & restriction) == restriction;
        }
    }

    public static class GameplayEffectRuntimePipelineContract
    {
        public const GameplayEffectRuntimePipelineKind DefaultNewRuntimeWritePath =
            GameplayEffectRuntimePipelineKind.EffectCommandSpecStream;

        private static readonly GameplayEffectRuntimePipelineContractEntry[] Entries =
        {
            new(
                GameplayEffectRuntimePipelineKind.EffectCommandSpecStream,
                GameplayEffectRuntimePipelineStatus.TargetContract,
                GameplayEffectRuntimePipelineRestriction.AvoidsRuntimeGameplayEffectEntity,
                nameof(BEffectCommand),
                nameof(BInstantEffectSpec),
                nameof(BAttributeDelta) + " / " + nameof(BTypedSimulationFact),
                typeof(BEffectCommand),
                typeof(BInstantEffectSpec),
                typeof(BAttributeDelta),
                typeof(BTypedSimulationFact)),
            new(
                GameplayEffectRuntimePipelineKind.ActiveEffectStore,
                GameplayEffectRuntimePipelineStatus.Planned,
                GameplayEffectRuntimePipelineRestriction.OwnsActiveEffectState,
                "ActiveEffectCommand",
                "ActiveEffectMutation",
                "ActiveEffectStore / AttributeDelta / TypedSimulationFact"),
        };

        public static bool IsLegacyInstantEntityLifecycleFrozen => true;

        public static IReadOnlyList<GameplayEffectRuntimePipelineContractEntry> All => Entries;

        public static bool AllowsNewFeatureExpansion(GameplayEffectRuntimePipelineKind kind)
        {
            return TryFind(kind, out var entry)
                   && !entry.HasRestriction(GameplayEffectRuntimePipelineRestriction.NoNewFeatureExpansion);
        }

        public static bool TryFind(
            GameplayEffectRuntimePipelineKind kind,
            out GameplayEffectRuntimePipelineContractEntry entry)
        {
            for (var i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].Kind == kind)
                {
                    entry = Entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
