using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    public enum GASDefinitionValidationSeverity
    {
        Warning = 1,
        Error = 2,
    }

    public enum GASDefinitionValidationCode
    {
        InvalidDefinitionCode = 1,
        DuplicateDefinitionCode = 2,
    }

    public readonly struct GASDefinitionValidationDiagnostic
    {
        public readonly GASDefinitionValidationSeverity Severity;
        public readonly GASDefinitionValidationCode Code;
        public readonly GASDefinitionKind DefinitionKind;
        public readonly int DefinitionCode;
        public readonly GASDefinitionKind OwnerKind;
        public readonly int OwnerCode;
        public readonly int OccurrenceCount;
        public readonly string Message;

        public GASDefinitionValidationDiagnostic(
            GASDefinitionValidationSeverity severity,
            GASDefinitionValidationCode code,
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASDefinitionKind ownerKind,
            int ownerCode,
            int occurrenceCount,
            string message)
        {
            Severity = severity;
            Code = code;
            DefinitionKind = definitionKind;
            DefinitionCode = definitionCode;
            OwnerKind = ownerKind;
            OwnerCode = ownerCode;
            OccurrenceCount = occurrenceCount;
            Message = message;
        }
    }

    public readonly struct GASGeneratedDefinitionSource
    {
        private readonly int[] _abilityCodes;
        private readonly int[] _gameplayEffectCodes;
        private readonly AttrSetConfig[] _attributeSetConfigs;
        private readonly GameplayTag[] _gameplayTags;
        private readonly int[] _gameplayCueCodes;

        public GASGeneratedDefinitionSource(
            IEnumerable<int> abilityCodes,
            IEnumerable<int> gameplayEffectCodes,
            IEnumerable<AttrSetConfig> attributeSetConfigs,
            IEnumerable<GameplayTag> gameplayTags,
            IEnumerable<int> gameplayCueCodes,
            IEnumerable<int> timelineIds)
            : this(
                abilityCodes,
                gameplayEffectCodes,
                attributeSetConfigs,
                gameplayTags,
                gameplayCueCodes)
        {
        }

        public GASGeneratedDefinitionSource(
            IEnumerable<int> abilityCodes,
            IEnumerable<int> gameplayEffectCodes,
            IEnumerable<AttrSetConfig> attributeSetConfigs,
            IEnumerable<GameplayTag> gameplayTags,
            IEnumerable<int> gameplayCueCodes)
        {
            _abilityCodes = Materialize(abilityCodes);
            _gameplayEffectCodes = Materialize(gameplayEffectCodes);
            _attributeSetConfigs = Materialize(attributeSetConfigs);
            _gameplayTags = Materialize(gameplayTags);
            _gameplayCueCodes = Materialize(gameplayCueCodes);
        }

        public IReadOnlyList<int> AbilityCodes => _abilityCodes ?? Array.Empty<int>();
        public IReadOnlyList<int> GameplayEffectCodes => _gameplayEffectCodes ?? Array.Empty<int>();
        public IReadOnlyList<AttrSetConfig> AttributeSetConfigs => _attributeSetConfigs ?? Array.Empty<AttrSetConfig>();
        public IReadOnlyList<GameplayTag> GameplayTags => _gameplayTags ?? Array.Empty<GameplayTag>();
        public IReadOnlyList<int> GameplayCueCodes => _gameplayCueCodes ?? Array.Empty<int>();

        public int TotalInputCount =>
            AbilityCodes.Count
            + GameplayEffectCodes.Count
            + AttributeSetConfigs.Count
            + GameplayTags.Count
            + GameplayCueCodes.Count;

        private static int[] Materialize(IEnumerable<int> values)
        {
            if (values == null)
                return Array.Empty<int>();

            return values is int[] array ? (int[])array.Clone() : new List<int>(values).ToArray();
        }

        private static T[] Materialize<T>(IEnumerable<T> values)
        {
            if (values == null)
                return Array.Empty<T>();

            return values is T[] array ? (T[])array.Clone() : new List<T>(values).ToArray();
        }
    }

    public readonly struct GASGeneratedDefinitionBuildResult
    {
        public readonly GASDefinitionTable DefinitionTable;
        public readonly ConfigRegistryGraphWarmupResult WarmupResult;
        public readonly ConfigRegistryDiagnostic[] RegistryDiagnostics;
        public readonly GASDefinitionValidationDiagnostic[] ValidationDiagnostics;

        public GASGeneratedDefinitionBuildResult(
            GASDefinitionTable definitionTable,
            ConfigRegistryGraphWarmupResult warmupResult,
            ConfigRegistryDiagnostic[] registryDiagnostics,
            GASDefinitionValidationDiagnostic[] validationDiagnostics)
        {
            DefinitionTable = definitionTable;
            WarmupResult = warmupResult;
            RegistryDiagnostics = registryDiagnostics ?? Array.Empty<ConfigRegistryDiagnostic>();
            ValidationDiagnostics = validationDiagnostics ?? Array.Empty<GASDefinitionValidationDiagnostic>();
        }

        public static GASGeneratedDefinitionBuildResult Empty =>
            new(
                GASDefinitionTable.Empty,
                ConfigRegistryGraphWarmupResult.Empty,
                Array.Empty<ConfigRegistryDiagnostic>(),
                Array.Empty<GASDefinitionValidationDiagnostic>());

        public int RegistryDiagnosticCount => RegistryDiagnostics.Length;
        public int ValidationDiagnosticCount => ValidationDiagnostics.Length;
        public int TotalDiagnosticCount => RegistryDiagnosticCount + ValidationDiagnosticCount;

        public bool HasErrors
        {
            get
            {
                for (var i = 0; i < RegistryDiagnostics.Length; i++)
                    if (RegistryDiagnostics[i].Severity == ConfigRegistryDiagnosticSeverity.Error)
                        return true;

                for (var i = 0; i < ValidationDiagnostics.Length; i++)
                    if (ValidationDiagnostics[i].Severity == GASDefinitionValidationSeverity.Error)
                        return true;

                return false;
            }
        }
    }

    public static class GASDefinitionGeneratedAdapter
    {
        public static GASGeneratedDefinitionBuildResult Build(
            GASGeneratedDefinitionSource source,
            bool clearDiagnostics = true)
        {
            if (clearDiagnostics)
                ConfigRegistryDiagnostics.Clear();

            var validationDiagnostics = ValidateSource(source);
            var warmupResult = ConfigRegistryGraphValidator.Warmup(
                source.AbilityCodes,
                source.GameplayEffectCodes,
                clearDiagnostics: false);
            var table = GASDefinitionSummaryBuilder.BuildFromRegistries(
                source.AbilityCodes,
                source.GameplayEffectCodes,
                source.AttributeSetConfigs,
                source.GameplayTags,
                source.GameplayCueCodes);

            return new GASGeneratedDefinitionBuildResult(
                table,
                warmupResult,
                ConfigRegistryDiagnostics.Snapshot(),
                validationDiagnostics);
        }

        public static GASDefinitionValidationDiagnostic[] ValidateSource(
            GASGeneratedDefinitionSource source)
        {
            var diagnostics = new List<GASDefinitionValidationDiagnostic>();

            ValidateCodeList(source.AbilityCodes, GASDefinitionKind.Ability, diagnostics);
            ValidateCodeList(source.GameplayEffectCodes, GASDefinitionKind.GameplayEffect, diagnostics);
            ValidateCodeList(source.GameplayCueCodes, GASDefinitionKind.GameplayCue, diagnostics);
            ValidateAttributeSets(source.AttributeSetConfigs, diagnostics);
            ValidateGameplayTags(source.GameplayTags, diagnostics);

            return diagnostics.ToArray();
        }

        private static void ValidateCodeList(
            IEnumerable<int> codes,
            GASDefinitionKind kind,
            List<GASDefinitionValidationDiagnostic> diagnostics)
        {
            var counts = new Dictionary<int, int>();
            foreach (var code in codes)
            {
                if (code <= 0)
                {
                    AddInvalidCodeDiagnostic(kind, code, GASDefinitionKind.None, 0, diagnostics);
                    continue;
                }

                counts.TryGetValue(code, out var count);
                counts[code] = count + 1;
            }

            AddDuplicateCodeDiagnostics(kind, counts, GASDefinitionKind.None, 0, diagnostics);
        }

        private static void ValidateAttributeSets(
            IEnumerable<AttrSetConfig> attributeSetConfigs,
            List<GASDefinitionValidationDiagnostic> diagnostics)
        {
            var attrSetCounts = new Dictionary<int, int>();
            foreach (var config in attributeSetConfigs)
            {
                if (config.Code <= 0)
                {
                    AddInvalidCodeDiagnostic(GASDefinitionKind.AttributeSet, config.Code, GASDefinitionKind.None, 0, diagnostics);
                    continue;
                }

                attrSetCounts.TryGetValue(config.Code, out var count);
                attrSetCounts[config.Code] = count + 1;
                ValidateAttributeCodes(config, diagnostics);
            }

            AddDuplicateCodeDiagnostics(
                GASDefinitionKind.AttributeSet,
                attrSetCounts,
                GASDefinitionKind.None,
                0,
                diagnostics);
        }

        private static void ValidateAttributeCodes(
            AttrSetConfig config,
            List<GASDefinitionValidationDiagnostic> diagnostics)
        {
            var settings = config.Settings ?? Array.Empty<AttributeBaseSetting>();
            var attributeCounts = new Dictionary<int, int>();
            for (var i = 0; i < settings.Length; i++)
            {
                var attributeCode = settings[i].Code;
                if (attributeCode <= 0)
                {
                    AddInvalidCodeDiagnostic(
                        GASDefinitionKind.Attribute,
                        attributeCode,
                        GASDefinitionKind.AttributeSet,
                        config.Code,
                        diagnostics);
                    continue;
                }

                attributeCounts.TryGetValue(attributeCode, out var count);
                attributeCounts[attributeCode] = count + 1;
            }

            AddDuplicateCodeDiagnostics(
                GASDefinitionKind.Attribute,
                attributeCounts,
                GASDefinitionKind.AttributeSet,
                config.Code,
                diagnostics);
        }

        private static void ValidateGameplayTags(
            IEnumerable<GameplayTag> gameplayTags,
            List<GASDefinitionValidationDiagnostic> diagnostics)
        {
            var tagCounts = new Dictionary<int, int>();
            foreach (var tag in gameplayTags)
            {
                if (tag.Code <= 0)
                {
                    AddInvalidCodeDiagnostic(GASDefinitionKind.GameplayTag, tag.Code, GASDefinitionKind.None, 0, diagnostics);
                    continue;
                }

                tagCounts.TryGetValue(tag.Code, out var count);
                tagCounts[tag.Code] = count + 1;
            }

            AddDuplicateCodeDiagnostics(
                GASDefinitionKind.GameplayTag,
                tagCounts,
                GASDefinitionKind.None,
                0,
                diagnostics);
        }

        private static void AddInvalidCodeDiagnostic(
            GASDefinitionKind kind,
            int code,
            GASDefinitionKind ownerKind,
            int ownerCode,
            List<GASDefinitionValidationDiagnostic> diagnostics)
        {
            diagnostics.Add(new GASDefinitionValidationDiagnostic(
                GASDefinitionValidationSeverity.Error,
                GASDefinitionValidationCode.InvalidDefinitionCode,
                kind,
                code,
                ownerKind,
                ownerCode,
                1,
                CreateInvalidCodeMessage(kind, code, ownerKind, ownerCode)));
        }

        private static void AddDuplicateCodeDiagnostics(
            GASDefinitionKind kind,
            Dictionary<int, int> counts,
            GASDefinitionKind ownerKind,
            int ownerCode,
            List<GASDefinitionValidationDiagnostic> diagnostics)
        {
            foreach (var pair in counts)
            {
                if (pair.Value <= 1)
                    continue;

                diagnostics.Add(new GASDefinitionValidationDiagnostic(
                    GASDefinitionValidationSeverity.Error,
                    GASDefinitionValidationCode.DuplicateDefinitionCode,
                    kind,
                    pair.Key,
                    ownerKind,
                    ownerCode,
                    pair.Value,
                    CreateDuplicateCodeMessage(kind, pair.Key, ownerKind, ownerCode, pair.Value)));
            }
        }

        private static string CreateInvalidCodeMessage(
            GASDefinitionKind kind,
            int code,
            GASDefinitionKind ownerKind,
            int ownerCode)
        {
            if (ownerKind != GASDefinitionKind.None && ownerCode > 0)
                return $"Invalid {kind} definition code {code}; owned by {ownerKind} definition {ownerCode}.";

            return $"Invalid {kind} definition code {code}.";
        }

        private static string CreateDuplicateCodeMessage(
            GASDefinitionKind kind,
            int code,
            GASDefinitionKind ownerKind,
            int ownerCode,
            int occurrenceCount)
        {
            if (ownerKind != GASDefinitionKind.None && ownerCode > 0)
            {
                return
                    $"Duplicate {kind} definition code {code}; owned by {ownerKind} definition {ownerCode}; occurrences={occurrenceCount}.";
            }

            return $"Duplicate {kind} definition code {code}; occurrences={occurrenceCount}.";
        }
    }
}
