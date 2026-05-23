using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace GAS.Runtime
{
    public readonly struct HeadlessAutoChessGeneratedRegistrySnapshot
    {
        private readonly HeadlessAutoChessAbilityDefinitionRow[] _abilityRows;
        private readonly HeadlessAutoChessGameplayEffectDefinitionRow[] _gameplayEffectRows;
        private readonly HeadlessAutoChessAttributeSetDefinitionRow[] _attributeSetRows;
        private readonly HeadlessAutoChessAttributeDefinitionRow[] _attributeRows;
        private readonly HeadlessAutoChessGameplayTagDefinitionRow[] _gameplayTagRows;
        private readonly HeadlessAutoChessGameplayCueDefinitionRow[] _gameplayCueRows;
        private readonly HeadlessAutoChessTimelineDefinitionRow[] _timelineRows;
        private readonly HeadlessAutoChessSummonDefinitionRow[] _summonRows;

        public HeadlessAutoChessGeneratedRegistrySnapshot(
            IEnumerable<HeadlessAutoChessAbilityDefinitionRow> abilityRows,
            IEnumerable<HeadlessAutoChessGameplayEffectDefinitionRow> gameplayEffectRows,
            IEnumerable<HeadlessAutoChessAttributeSetDefinitionRow> attributeSetRows,
            IEnumerable<HeadlessAutoChessAttributeDefinitionRow> attributeRows,
            IEnumerable<HeadlessAutoChessGameplayTagDefinitionRow> gameplayTagRows,
            IEnumerable<HeadlessAutoChessGameplayCueDefinitionRow> gameplayCueRows,
            IEnumerable<HeadlessAutoChessTimelineDefinitionRow> timelineRows,
            IEnumerable<HeadlessAutoChessSummonDefinitionRow> summonRows = null)
        {
            _abilityRows = Materialize(abilityRows);
            _gameplayEffectRows = Materialize(gameplayEffectRows);
            _attributeSetRows = Materialize(attributeSetRows);
            _attributeRows = Materialize(attributeRows);
            _gameplayTagRows = Materialize(gameplayTagRows);
            _gameplayCueRows = Materialize(gameplayCueRows);
            _timelineRows = Materialize(timelineRows);
            _summonRows = Materialize(summonRows);
        }

        public IReadOnlyList<HeadlessAutoChessAbilityDefinitionRow> AbilityRows =>
            _abilityRows ?? Array.Empty<HeadlessAutoChessAbilityDefinitionRow>();

        public IReadOnlyList<HeadlessAutoChessGameplayEffectDefinitionRow> GameplayEffectRows =>
            _gameplayEffectRows ?? Array.Empty<HeadlessAutoChessGameplayEffectDefinitionRow>();

        public IReadOnlyList<HeadlessAutoChessAttributeSetDefinitionRow> AttributeSetRows =>
            _attributeSetRows ?? Array.Empty<HeadlessAutoChessAttributeSetDefinitionRow>();

        public IReadOnlyList<HeadlessAutoChessAttributeDefinitionRow> AttributeRows =>
            _attributeRows ?? Array.Empty<HeadlessAutoChessAttributeDefinitionRow>();

        public IReadOnlyList<HeadlessAutoChessGameplayTagDefinitionRow> GameplayTagRows =>
            _gameplayTagRows ?? Array.Empty<HeadlessAutoChessGameplayTagDefinitionRow>();

        public IReadOnlyList<HeadlessAutoChessGameplayCueDefinitionRow> GameplayCueRows =>
            _gameplayCueRows ?? Array.Empty<HeadlessAutoChessGameplayCueDefinitionRow>();

        public IReadOnlyList<HeadlessAutoChessTimelineDefinitionRow> TimelineRows =>
            _timelineRows ?? Array.Empty<HeadlessAutoChessTimelineDefinitionRow>();

        public IReadOnlyList<HeadlessAutoChessSummonDefinitionRow> SummonRows =>
            _summonRows ?? Array.Empty<HeadlessAutoChessSummonDefinitionRow>();

        public int AbilityRowCount => AbilityRows.Count;
        public int GameplayEffectRowCount => GameplayEffectRows.Count;
        public int AttributeSetRowCount => AttributeSetRows.Count;
        public int AttributeRowCount => AttributeRows.Count;
        public int GameplayTagRowCount => GameplayTagRows.Count;
        public int GameplayCueRowCount => GameplayCueRows.Count;
        public int TimelineRowCount => TimelineRows.Count;
        public int SummonRowCount => SummonRows.Count;

        public GASGeneratedDefinitionSource CreateDefinitionSource()
        {
            return new GASGeneratedDefinitionSource(
                CreateAbilityCodes(),
                CreateGameplayEffectCodes(),
                CreateAttributeSetConfigs(),
                CreateGameplayTags(),
                CreateGameplayCueCodes(),
                CreateTimelineIds());
        }

        public int[] CreateAbilityCodes()
        {
            var rows = AbilityRows;
            var codes = new int[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                codes[i] = rows[i].AbilityCode;
            return codes;
        }

        public int[] CreateGameplayEffectCodes()
        {
            var rows = GameplayEffectRows;
            var codes = new int[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                codes[i] = rows[i].GameplayEffectCode;
            return codes;
        }

        public AttrSetConfig[] CreateAttributeSetConfigs()
        {
            var attributeSetRows = AttributeSetRows;
            var attributeRows = AttributeRows;
            var configs = new AttrSetConfig[attributeSetRows.Count];

            for (var i = 0; i < attributeSetRows.Count; i++)
            {
                var attributeSetCode = attributeSetRows[i].AttributeSetCode;
                var settings = new List<AttributeBaseSetting>();
                for (var j = 0; j < attributeRows.Count; j++)
                {
                    var attributeRow = attributeRows[j];
                    if (attributeRow.AttributeSetCode == attributeSetCode)
                        settings.Add(attributeRow.ToAttributeSetting());
                }

                configs[i] = new AttrSetConfig(attributeSetCode, settings.ToArray());
            }

            return configs;
        }

        public GameplayTag[] CreateGameplayTags()
        {
            var rows = GameplayTagRows;
            var tags = new GameplayTag[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                tags[i] = rows[i].ToGameplayTag();
            return tags;
        }

        public int[] CreateGameplayCueCodes()
        {
            var rows = GameplayCueRows;
            var codes = new int[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                codes[i] = rows[i].GameplayCueCode;
            return codes;
        }

        public int[] CreateTimelineIds()
        {
            var rows = TimelineRows;
            var ids = new int[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                ids[i] = rows[i].TimelineId;
            return ids;
        }

        public bool TryFindAbilityRow(int abilityCode, out HeadlessAutoChessAbilityDefinitionRow row)
        {
            var rows = AbilityRows;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].AbilityCode != abilityCode)
                    continue;

                row = rows[i];
                return true;
            }

            row = default;
            return false;
        }

        public bool TryFindGameplayEffectRow(
            int gameplayEffectCode,
            out HeadlessAutoChessGameplayEffectDefinitionRow row)
        {
            var rows = GameplayEffectRows;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].GameplayEffectCode != gameplayEffectCode)
                    continue;

                row = rows[i];
                return true;
            }

            row = default;
            return false;
        }

        public bool TryFindGameplayCueRow(int gameplayCueCode, out HeadlessAutoChessGameplayCueDefinitionRow row)
        {
            var rows = GameplayCueRows;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].GameplayCueCode != gameplayCueCode)
                    continue;

                row = rows[i];
                return true;
            }

            row = default;
            return false;
        }

        public bool TryFindTimelineRow(int timelineId, out HeadlessAutoChessTimelineDefinitionRow row)
        {
            var rows = TimelineRows;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].TimelineId != timelineId)
                    continue;

                row = rows[i];
                return true;
            }

            row = default;
            return false;
        }

        public bool TryFindSummonRow(
            int summonGameplayEffectCode,
            out HeadlessAutoChessSummonDefinitionRow row)
        {
            var rows = SummonRows;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].SummonGameplayEffectCode != summonGameplayEffectCode)
                    continue;

                row = rows[i];
                return true;
            }

            row = default;
            return false;
        }

        private static T[] Materialize<T>(IEnumerable<T> values)
        {
            if (values == null)
                return Array.Empty<T>();

            return values is T[] array ? (T[])array.Clone() : new List<T>(values).ToArray();
        }
    }

    public enum HeadlessAutoChessGeneratedDefinitionSourceKind
    {
        Unknown = 0,
        LubanSourceGenerator = 1,
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionManifest
    {
        public readonly HeadlessAutoChessGeneratedDefinitionSourceKind SourceKind;
        public readonly string PackageName;
        public readonly string SchemaVersion;
        public readonly string GeneratorName;
        public readonly string SourceRevision;
        public readonly int AbilityRowCount;
        public readonly int GameplayEffectRowCount;
        public readonly int AttributeSetRowCount;
        public readonly int AttributeRowCount;
        public readonly int GameplayTagRowCount;
        public readonly int GameplayCueRowCount;
        public readonly int TimelineRowCount;
        public readonly int SummonRowCount;
        public readonly int ContentHash;

        public HeadlessAutoChessGeneratedDefinitionManifest(
            HeadlessAutoChessGeneratedDefinitionSourceKind sourceKind,
            string packageName,
            string schemaVersion,
            string generatorName,
            string sourceRevision,
            int abilityRowCount,
            int gameplayEffectRowCount,
            int attributeSetRowCount,
            int attributeRowCount,
            int gameplayTagRowCount,
            int gameplayCueRowCount,
            int timelineRowCount,
            int summonRowCount,
            int contentHash)
        {
            SourceKind = sourceKind;
            PackageName = packageName ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            GeneratorName = generatorName ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            AbilityRowCount = abilityRowCount;
            GameplayEffectRowCount = gameplayEffectRowCount;
            AttributeSetRowCount = attributeSetRowCount;
            AttributeRowCount = attributeRowCount;
            GameplayTagRowCount = gameplayTagRowCount;
            GameplayCueRowCount = gameplayCueRowCount;
            TimelineRowCount = timelineRowCount;
            SummonRowCount = summonRowCount;
            ContentHash = contentHash;
        }

        public int TotalRowCount =>
            AbilityRowCount
            + GameplayEffectRowCount
            + AttributeSetRowCount
            + AttributeRowCount
            + GameplayTagRowCount
            + GameplayCueRowCount
            + TimelineRowCount
            + SummonRowCount;

        public bool HasGeneratorIdentity =>
            SourceKind != HeadlessAutoChessGeneratedDefinitionSourceKind.Unknown
            && !string.IsNullOrEmpty(PackageName)
            && !string.IsNullOrEmpty(SchemaVersion)
            && !string.IsNullOrEmpty(GeneratorName);

        public bool MatchesRowCounts(in HeadlessAutoChessGeneratedRegistrySnapshot snapshot)
        {
            return AbilityRowCount == snapshot.AbilityRowCount
                   && GameplayEffectRowCount == snapshot.GameplayEffectRowCount
                   && AttributeSetRowCount == snapshot.AttributeSetRowCount
                   && AttributeRowCount == snapshot.AttributeRowCount
                   && GameplayTagRowCount == snapshot.GameplayTagRowCount
                   && GameplayCueRowCount == snapshot.GameplayCueRowCount
                   && TimelineRowCount == snapshot.TimelineRowCount
                   && SummonRowCount == snapshot.SummonRowCount;
        }
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionPackageValidation
    {
        public readonly bool HasGeneratorIdentity;
        public readonly bool RowCountsMatch;
        public readonly bool ContentHashMatches;
        public readonly int ManifestTotalRowCount;
        public readonly int SnapshotTotalRowCount;
        public readonly int ExpectedContentHash;
        public readonly int ActualContentHash;
        public readonly string FailureReason;

        public HeadlessAutoChessGeneratedDefinitionPackageValidation(
            bool hasGeneratorIdentity,
            bool rowCountsMatch,
            bool contentHashMatches,
            int manifestTotalRowCount,
            int snapshotTotalRowCount,
            int expectedContentHash,
            int actualContentHash,
            string failureReason)
        {
            HasGeneratorIdentity = hasGeneratorIdentity;
            RowCountsMatch = rowCountsMatch;
            ContentHashMatches = contentHashMatches;
            ManifestTotalRowCount = manifestTotalRowCount;
            SnapshotTotalRowCount = snapshotTotalRowCount;
            ExpectedContentHash = expectedContentHash;
            ActualContentHash = actualContentHash;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Passed => HasGeneratorIdentity && RowCountsMatch && ContentHashMatches;
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionPackage
    {
        public readonly HeadlessAutoChessGeneratedDefinitionManifest Manifest;
        public readonly HeadlessAutoChessGeneratedRegistrySnapshot Snapshot;

        public HeadlessAutoChessGeneratedDefinitionPackage(
            HeadlessAutoChessGeneratedDefinitionManifest manifest,
            HeadlessAutoChessGeneratedRegistrySnapshot snapshot)
        {
            Manifest = manifest;
            Snapshot = snapshot;
        }

        public static HeadlessAutoChessGeneratedDefinitionPackage Create(
            HeadlessAutoChessGeneratedDefinitionSourceKind sourceKind,
            string packageName,
            string schemaVersion,
            string generatorName,
            string sourceRevision,
            HeadlessAutoChessGeneratedRegistrySnapshot snapshot)
        {
            var manifest = new HeadlessAutoChessGeneratedDefinitionManifest(
                sourceKind,
                packageName,
                schemaVersion,
                generatorName,
                sourceRevision,
                snapshot.AbilityRowCount,
                snapshot.GameplayEffectRowCount,
                snapshot.AttributeSetRowCount,
                snapshot.AttributeRowCount,
                snapshot.GameplayTagRowCount,
                snapshot.GameplayCueRowCount,
                snapshot.TimelineRowCount,
                snapshot.SummonRowCount,
                CalculateContentHash(snapshot));

            return new HeadlessAutoChessGeneratedDefinitionPackage(manifest, snapshot);
        }

        public GASGeneratedDefinitionSource CreateDefinitionSource()
        {
            return Snapshot.CreateDefinitionSource();
        }

        public HeadlessAutoChessGeneratedDefinitionPackageValidation Validate()
        {
            var hasGeneratorIdentity = Manifest.HasGeneratorIdentity;
            var rowCountsMatch = Manifest.MatchesRowCounts(Snapshot);
            var actualContentHash = CalculateContentHash(Snapshot);
            var contentHashMatches = Manifest.ContentHash == actualContentHash;
            var reason = string.Empty;

            if (!hasGeneratorIdentity)
                reason = "Generated definition package is missing source generator identity.";
            else if (!rowCountsMatch)
                reason = "Generated definition package row counts do not match its snapshot.";
            else if (!contentHashMatches)
                reason = "Generated definition package content hash does not match its snapshot.";

            return new HeadlessAutoChessGeneratedDefinitionPackageValidation(
                hasGeneratorIdentity,
                rowCountsMatch,
                contentHashMatches,
                Manifest.TotalRowCount,
                Snapshot.AbilityRowCount
                + Snapshot.GameplayEffectRowCount
                + Snapshot.AttributeSetRowCount
                + Snapshot.AttributeRowCount
                + Snapshot.GameplayTagRowCount
                + Snapshot.GameplayCueRowCount
                + Snapshot.TimelineRowCount
                + Snapshot.SummonRowCount,
                Manifest.ContentHash,
                actualContentHash,
                reason);
        }

        public static int CalculateContentHash(in HeadlessAutoChessGeneratedRegistrySnapshot snapshot)
        {
            unchecked
            {
                var hash = 2166136261u;
                AppendHash(ref hash, snapshot.AbilityRowCount);
                AppendHash(ref hash, snapshot.GameplayEffectRowCount);
                AppendHash(ref hash, snapshot.AttributeSetRowCount);
                AppendHash(ref hash, snapshot.AttributeRowCount);
                AppendHash(ref hash, snapshot.GameplayTagRowCount);
                AppendHash(ref hash, snapshot.GameplayCueRowCount);
                AppendHash(ref hash, snapshot.TimelineRowCount);
                AppendHash(ref hash, snapshot.SummonRowCount);

                var abilityRows = snapshot.AbilityRows;
                for (var i = 0; i < abilityRows.Count; i++)
                    AppendAbilityRow(ref hash, abilityRows[i]);

                var gameplayEffectRows = snapshot.GameplayEffectRows;
                for (var i = 0; i < gameplayEffectRows.Count; i++)
                    AppendGameplayEffectRow(ref hash, gameplayEffectRows[i]);

                var attributeSetRows = snapshot.AttributeSetRows;
                for (var i = 0; i < attributeSetRows.Count; i++)
                    AppendHash(ref hash, attributeSetRows[i].AttributeSetCode);

                var attributeRows = snapshot.AttributeRows;
                for (var i = 0; i < attributeRows.Count; i++)
                    AppendAttributeRow(ref hash, attributeRows[i]);

                var gameplayTagRows = snapshot.GameplayTagRows;
                for (var i = 0; i < gameplayTagRows.Count; i++)
                    AppendGameplayTagRow(ref hash, gameplayTagRows[i]);

                var gameplayCueRows = snapshot.GameplayCueRows;
                for (var i = 0; i < gameplayCueRows.Count; i++)
                    AppendGameplayCueRow(ref hash, gameplayCueRows[i]);

                var timelineRows = snapshot.TimelineRows;
                for (var i = 0; i < timelineRows.Count; i++)
                    AppendTimelineRow(ref hash, timelineRows[i]);

                var summonRows = snapshot.SummonRows;
                for (var i = 0; i < summonRows.Count; i++)
                    AppendSummonRow(ref hash, summonRows[i]);

                return (int)hash;
            }
        }

        private static void AppendAbilityRow(
            ref uint hash,
            in HeadlessAutoChessAbilityDefinitionRow row)
        {
            AppendHash(ref hash, row.AbilityCode);
            AppendHash(ref hash, row.Level);
            AppendHash(ref hash, row.TimelineId);
            AppendHash(ref hash, row.ActivationOwnedTagCode);
            AppendHash(ref hash, row.CostGameplayEffectCode);
            AppendHash(ref hash, row.CooldownGameplayEffectCode);
            AppendHash(ref hash, row.CooldownFrames);
        }

        private static void AppendGameplayEffectRow(
            ref uint hash,
            in HeadlessAutoChessGameplayEffectDefinitionRow row)
        {
            AppendHash(ref hash, row.GameplayEffectCode);
            AppendHash(ref hash, row.Name);
            AppendHash(ref hash, row.ModifierAttributeSetCode);
            AppendHash(ref hash, row.ModifierAttributeCode);
            AppendHash(ref hash, (int)row.ModifierOperation);
            AppendHash(ref hash, row.ModifierMagnitude);
            AppendHash(ref hash, (int)row.ModifierMagnitudeSource);
            AppendHash(ref hash, row.ModifierMagnitudeKey);
            AppendHash(ref hash, row.DurationFrames);
            AppendHash(ref hash, row.PeriodFrames);
            AppendHash(ref hash, row.PeriodGameplayEffectCode);
            AppendHash(ref hash, row.GrantedTagCode);
            AppendHash(ref hash, row.GameplayCueCode);
            AppendHash(ref hash, row.DamageTypeCode);
            AppendHash(ref hash, row.ResistanceAttributeSetCode);
            AppendHash(ref hash, row.ResistanceAttributeCode);
            AppendHash(ref hash, row.ResistanceCap);
            AppendHash(ref hash, row.RemoveGameplayEffectTagCode);
            AppendHash(ref hash, row.StackingCode);
            AppendHash(ref hash, row.StackLimitCount);
            AppendHash(ref hash, (int)row.StackType);
            AppendHash(ref hash, (int)row.EffectDurationRefreshPolicy);
            AppendHash(ref hash, (int)row.EffectPeriodResetPolicy);
            AppendHash(ref hash, (int)row.EffectExpirationPolicy);
            AppendHash(ref hash, row.DenyOverflowApplication);
            AppendHash(ref hash, row.ClearStackOnOverflow);
            AppendHash(ref hash, row.OverflowGameplayEffectCode);
        }

        private static void AppendAttributeRow(
            ref uint hash,
            in HeadlessAutoChessAttributeDefinitionRow row)
        {
            AppendHash(ref hash, row.AttributeSetCode);
            AppendHash(ref hash, row.AttributeCode);
            AppendHash(ref hash, row.InitialValue);
            AppendHash(ref hash, row.IsClampMin);
            AppendHash(ref hash, row.IsClampMax);
            AppendHash(ref hash, row.MinValue);
            AppendHash(ref hash, row.MaxValue);
        }

        private static void AppendGameplayTagRow(
            ref uint hash,
            in HeadlessAutoChessGameplayTagDefinitionRow row)
        {
            AppendHash(ref hash, row.GameplayTagCode);
            AppendCodes(ref hash, row.ParentCodes);
            AppendCodes(ref hash, row.ChildCodes);
        }

        private static void AppendGameplayCueRow(
            ref uint hash,
            in HeadlessAutoChessGameplayCueDefinitionRow row)
        {
            AppendHash(ref hash, row.GameplayCueCode);
            AppendHash(ref hash, row.PresentationKey);
        }

        private static void AppendTimelineRow(
            ref uint hash,
            in HeadlessAutoChessTimelineDefinitionRow row)
        {
            AppendHash(ref hash, row.TimelineId);
            AppendHash(ref hash, row.Name);
            AppendHash(ref hash, row.GameplayEffectCode);
            AppendHash(ref hash, row.SecondaryGameplayEffectCode);
            AppendHash(ref hash, row.TargetCatcherName);
        }

        private static void AppendSummonRow(
            ref uint hash,
            in HeadlessAutoChessSummonDefinitionRow row)
        {
            AppendHash(ref hash, row.SummonGameplayEffectCode);
            AppendHash(ref hash, row.SummonedUnitCode);
            AppendHash(ref hash, row.FixedTagCode);
            AppendHash(ref hash, row.PrimaryAbilityCode);
            AppendHash(ref hash, row.LifetimeTurns);
            AppendHash(ref hash, row.SlotOffset);
            AppendHash(ref hash, row.BoardXOffset);
            AppendHash(ref hash, row.BoardYOffset);
            AppendHash(ref hash, row.TurnOrderOffset);
            AppendHash(ref hash, row.Health);
            AppendHash(ref hash, row.Mana);
            AppendHash(ref hash, row.Shield);
            AppendHash(ref hash, row.MaxHealth);
            AppendHash(ref hash, row.MaxMana);
            AppendHash(ref hash, row.MaxShield);
            AppendHash(ref hash, (int)row.PrimaryTargetPolicy);
        }

        private static void AppendCodes(ref uint hash, IReadOnlyList<int> codes)
        {
            AppendHash(ref hash, codes.Count);
            for (var i = 0; i < codes.Count; i++)
                AppendHash(ref hash, codes[i]);
        }

        private static void AppendHash(ref uint hash, string value)
        {
            value ??= string.Empty;
            AppendHash(ref hash, value.Length);
            for (var i = 0; i < value.Length; i++)
                AppendHash(ref hash, value[i]);
        }

        private static void AppendHash(ref uint hash, bool value)
        {
            AppendHash(ref hash, value ? 1 : 0);
        }

        private static void AppendHash(ref uint hash, float value)
        {
            AppendHash(ref hash, value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendHash(ref uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 16777619u;
            }
        }
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutputValidation
    {
        public readonly bool PackageValidationPassed;
        public readonly bool HasOutputIdentity;
        public readonly bool SourceTextHashMatches;
        public readonly bool ManifestTextHashMatches;
        public readonly bool SourceTextContainsManifestFingerprint;
        public readonly bool WritesDefinitionPlaneOnly;
        public readonly int ExpectedSourceTextHash;
        public readonly int ActualSourceTextHash;
        public readonly int ExpectedManifestTextHash;
        public readonly int ActualManifestTextHash;
        public readonly string FailureReason;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutputValidation(
            bool packageValidationPassed,
            bool hasOutputIdentity,
            bool sourceTextHashMatches,
            bool manifestTextHashMatches,
            bool sourceTextContainsManifestFingerprint,
            bool writesDefinitionPlaneOnly,
            int expectedSourceTextHash,
            int actualSourceTextHash,
            int expectedManifestTextHash,
            int actualManifestTextHash,
            string failureReason)
        {
            PackageValidationPassed = packageValidationPassed;
            HasOutputIdentity = hasOutputIdentity;
            SourceTextHashMatches = sourceTextHashMatches;
            ManifestTextHashMatches = manifestTextHashMatches;
            SourceTextContainsManifestFingerprint = sourceTextContainsManifestFingerprint;
            WritesDefinitionPlaneOnly = writesDefinitionPlaneOnly;
            ExpectedSourceTextHash = expectedSourceTextHash;
            ActualSourceTextHash = actualSourceTextHash;
            ExpectedManifestTextHash = expectedManifestTextHash;
            ActualManifestTextHash = actualManifestTextHash;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Passed =>
            PackageValidationPassed
            && HasOutputIdentity
            && SourceTextHashMatches
            && ManifestTextHashMatches
            && SourceTextContainsManifestFingerprint
            && WritesDefinitionPlaneOnly;
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput
    {
        public readonly HeadlessAutoChessGeneratedDefinitionPackage Package;
        public readonly string RuntimeSourceRelativePath;
        public readonly string ManifestRelativePath;
        public readonly string GeneratedNamespace;
        public readonly string GeneratedTypeName;
        public readonly string SourceText;
        public readonly string ManifestText;
        public readonly int SourceTextHash;
        public readonly int ManifestTextHash;
        public readonly bool EmitsDefinitionPackage;
        public readonly bool EmitsRuntimeLifecycle;
        public readonly bool EmitsPresentationBehavior;
        public readonly bool EmitsEditorTypes;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput(
            HeadlessAutoChessGeneratedDefinitionPackage package,
            string runtimeSourceRelativePath,
            string manifestRelativePath,
            string generatedNamespace,
            string generatedTypeName,
            string sourceText,
            string manifestText,
            bool emitsDefinitionPackage,
            bool emitsRuntimeLifecycle,
            bool emitsPresentationBehavior,
            bool emitsEditorTypes)
        {
            Package = package;
            RuntimeSourceRelativePath = runtimeSourceRelativePath ?? string.Empty;
            ManifestRelativePath = manifestRelativePath ?? string.Empty;
            GeneratedNamespace = generatedNamespace ?? string.Empty;
            GeneratedTypeName = generatedTypeName ?? string.Empty;
            SourceText = sourceText ?? string.Empty;
            ManifestText = manifestText ?? string.Empty;
            SourceTextHash = CalculateTextHash(SourceText);
            ManifestTextHash = CalculateTextHash(ManifestText);
            EmitsDefinitionPackage = emitsDefinitionPackage;
            EmitsRuntimeLifecycle = emitsRuntimeLifecycle;
            EmitsPresentationBehavior = emitsPresentationBehavior;
            EmitsEditorTypes = emitsEditorTypes;
        }

        public bool WritesDefinitionPlaneOnly =>
            EmitsDefinitionPackage
            && !EmitsRuntimeLifecycle
            && !EmitsPresentationBehavior
            && !EmitsEditorTypes;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutputValidation Validate()
        {
            var packageValidation = Package.Validate();
            var hasOutputIdentity =
                !string.IsNullOrEmpty(RuntimeSourceRelativePath)
                && !string.IsNullOrEmpty(ManifestRelativePath)
                && !string.IsNullOrEmpty(GeneratedNamespace)
                && !string.IsNullOrEmpty(GeneratedTypeName);
            var actualSourceTextHash = CalculateTextHash(SourceText);
            var actualManifestTextHash = CalculateTextHash(ManifestText);
            var sourceTextHashMatches = SourceTextHash == actualSourceTextHash;
            var manifestTextHashMatches = ManifestTextHash == actualManifestTextHash;
            var containsFingerprint = SourceTextContainsManifestFingerprint();
            var writesDefinitionPlaneOnly = WritesDefinitionPlaneOnly;
            var reason = string.Empty;

            if (!packageValidation.Passed)
                reason = packageValidation.FailureReason;
            else if (!hasOutputIdentity)
                reason = "Generated source output is missing file or type identity.";
            else if (!sourceTextHashMatches)
                reason = "Generated source output source text hash does not match.";
            else if (!manifestTextHashMatches)
                reason = "Generated source output manifest text hash does not match.";
            else if (!containsFingerprint)
                reason = "Generated source output source text does not contain manifest fingerprint.";
            else if (!writesDefinitionPlaneOnly)
                reason = "Generated source output must only emit Definition Plane package data.";

            return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutputValidation(
                packageValidation.Passed,
                hasOutputIdentity,
                sourceTextHashMatches,
                manifestTextHashMatches,
                containsFingerprint,
                writesDefinitionPlaneOnly,
                SourceTextHash,
                actualSourceTextHash,
                ManifestTextHash,
                actualManifestTextHash,
                reason);
        }

        public static HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput Create(
            HeadlessAutoChessGeneratedDefinitionPackage package,
            string runtimeSourceRelativePath,
            string manifestRelativePath,
            string generatedNamespace,
            string generatedTypeName)
        {
            var manifestText = BuildManifestText(
                package,
                runtimeSourceRelativePath,
                manifestRelativePath,
                generatedNamespace,
                generatedTypeName);
            var sourceText = BuildSourceText(
                package,
                generatedNamespace,
                generatedTypeName,
                CalculateTextHash(manifestText));

            return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput(
                package,
                runtimeSourceRelativePath,
                manifestRelativePath,
                generatedNamespace,
                generatedTypeName,
                sourceText,
                manifestText,
                emitsDefinitionPackage: true,
                emitsRuntimeLifecycle: false,
                emitsPresentationBehavior: false,
                emitsEditorTypes: false);
        }

        public static int CalculateTextHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                value ??= string.Empty;
                for (var i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }

        private bool SourceTextContainsManifestFingerprint()
        {
            var manifest = Package.Manifest;
            return SourceText.Contains(manifest.PackageName)
                   && SourceText.Contains(manifest.SchemaVersion)
                   && SourceText.Contains(manifest.GeneratorName)
                   && SourceText.Contains(manifest.SourceRevision)
                   && SourceText.Contains(manifest.TotalRowCount.ToString(CultureInfo.InvariantCulture))
                   && SourceText.Contains(manifest.ContentHash.ToString(CultureInfo.InvariantCulture))
                   && SourceText.Contains(ManifestTextHash.ToString(CultureInfo.InvariantCulture));
        }

        private static string BuildManifestText(
            HeadlessAutoChessGeneratedDefinitionPackage package,
            string runtimeSourceRelativePath,
            string manifestRelativePath,
            string generatedNamespace,
            string generatedTypeName)
        {
            var manifest = package.Manifest;
            var builder = new StringBuilder(1024);
            builder.AppendLine("{");
            AppendJsonStringProperty(builder, "packageName", manifest.PackageName, trailingComma: true);
            AppendJsonStringProperty(builder, "schemaVersion", manifest.SchemaVersion, trailingComma: true);
            AppendJsonStringProperty(builder, "generatorName", manifest.GeneratorName, trailingComma: true);
            AppendJsonStringProperty(builder, "sourceRevision", manifest.SourceRevision, trailingComma: true);
            AppendJsonStringProperty(builder, "sourceKind", manifest.SourceKind.ToString(), trailingComma: true);
            AppendJsonStringProperty(builder, "runtimeSourceRelativePath", runtimeSourceRelativePath, trailingComma: true);
            AppendJsonStringProperty(builder, "manifestRelativePath", manifestRelativePath, trailingComma: true);
            AppendJsonStringProperty(builder, "generatedNamespace", generatedNamespace, trailingComma: true);
            AppendJsonStringProperty(builder, "generatedTypeName", generatedTypeName, trailingComma: true);
            AppendJsonIntProperty(builder, "abilityRows", manifest.AbilityRowCount, trailingComma: true);
            AppendJsonIntProperty(builder, "gameplayEffectRows", manifest.GameplayEffectRowCount, trailingComma: true);
            AppendJsonIntProperty(builder, "attributeSetRows", manifest.AttributeSetRowCount, trailingComma: true);
            AppendJsonIntProperty(builder, "attributeRows", manifest.AttributeRowCount, trailingComma: true);
            AppendJsonIntProperty(builder, "gameplayTagRows", manifest.GameplayTagRowCount, trailingComma: true);
            AppendJsonIntProperty(builder, "gameplayCueRows", manifest.GameplayCueRowCount, trailingComma: true);
            AppendJsonIntProperty(builder, "timelineRows", manifest.TimelineRowCount, trailingComma: true);
            AppendJsonIntProperty(builder, "summonRows", manifest.SummonRowCount, trailingComma: true);
            AppendJsonIntProperty(builder, "totalRows", manifest.TotalRowCount, trailingComma: true);
            AppendJsonIntProperty(builder, "contentHash", manifest.ContentHash, trailingComma: true);
            AppendJsonBoolProperty(builder, "emitsDefinitionPackage", true, trailingComma: true);
            AppendJsonBoolProperty(builder, "emitsRuntimeLifecycle", false, trailingComma: true);
            AppendJsonBoolProperty(builder, "emitsPresentationBehavior", false, trailingComma: true);
            AppendJsonBoolProperty(builder, "emitsEditorTypes", false, trailingComma: false);
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string BuildSourceText(
            HeadlessAutoChessGeneratedDefinitionPackage package,
            string generatedNamespace,
            string generatedTypeName,
            int manifestTextHash)
        {
            var manifest = package.Manifest;
            var builder = new StringBuilder(2048);
            builder.AppendLine("// <auto-generated />");
            builder.AppendLine("// EX-GAS generated definition package manifest. Do not edit by hand.");
            builder.Append("namespace ");
            builder.AppendLine(generatedNamespace);
            builder.AppendLine("{");
            builder.Append("    public static class ");
            builder.AppendLine(generatedTypeName);
            builder.AppendLine("    {");
            AppendCSharpStringConstant(builder, "PackageName", manifest.PackageName);
            AppendCSharpStringConstant(builder, "SchemaVersion", manifest.SchemaVersion);
            AppendCSharpStringConstant(builder, "GeneratorName", manifest.GeneratorName);
            AppendCSharpStringConstant(builder, "SourceRevision", manifest.SourceRevision);
            AppendCSharpIntConstant(builder, "AbilityRowCount", manifest.AbilityRowCount);
            AppendCSharpIntConstant(builder, "GameplayEffectRowCount", manifest.GameplayEffectRowCount);
            AppendCSharpIntConstant(builder, "AttributeSetRowCount", manifest.AttributeSetRowCount);
            AppendCSharpIntConstant(builder, "AttributeRowCount", manifest.AttributeRowCount);
            AppendCSharpIntConstant(builder, "GameplayTagRowCount", manifest.GameplayTagRowCount);
            AppendCSharpIntConstant(builder, "GameplayCueRowCount", manifest.GameplayCueRowCount);
            AppendCSharpIntConstant(builder, "TimelineRowCount", manifest.TimelineRowCount);
            AppendCSharpIntConstant(builder, "SummonRowCount", manifest.SummonRowCount);
            AppendCSharpIntConstant(builder, "TotalRowCount", manifest.TotalRowCount);
            AppendCSharpIntConstant(builder, "ContentHash", manifest.ContentHash);
            AppendCSharpIntConstant(builder, "ManifestTextHash", manifestTextHash);
            builder.AppendLine();
            builder.AppendLine("        public static GAS.Runtime.HeadlessAutoChessGeneratedDefinitionPackage CreatePackage()");
            builder.AppendLine("        {");
            builder.AppendLine("            return GAS.Runtime.HeadlessAutoChessGeneratedDefinitionRows.CreateLubanSourceGeneratorPackage();");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendJsonStringProperty(
            StringBuilder builder,
            string name,
            string value,
            bool trailingComma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": ");
            AppendJsonString(builder, value);
            if (trailingComma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonIntProperty(
            StringBuilder builder,
            string name,
            int value,
            bool trailingComma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            if (trailingComma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonBoolProperty(
            StringBuilder builder,
            string name,
            bool value,
            bool trailingComma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value ? "true" : "false");
            if (trailingComma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            value ??= string.Empty;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character == '\\' || character == '"')
                    builder.Append('\\');
                builder.Append(character);
            }
            builder.Append('"');
        }

        private static void AppendCSharpStringConstant(StringBuilder builder, string name, string value)
        {
            builder.Append("        public const string ");
            builder.Append(name);
            builder.Append(" = ");
            AppendCSharpStringLiteral(builder, value);
            builder.AppendLine(";");
        }

        private static void AppendCSharpIntConstant(StringBuilder builder, string name, int value)
        {
            builder.Append("        public const int ");
            builder.Append(name);
            builder.Append(" = ");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(";");
        }

        private static void AppendCSharpStringLiteral(StringBuilder builder, string value)
        {
            builder.Append('"');
            value ??= string.Empty;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character == '\\' || character == '"')
                    builder.Append('\\');
                builder.Append(character);
            }
            builder.Append('"');
        }
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlanValidation
    {
        public readonly bool OutputValidationPassed;
        public readonly bool HasProjectRoot;
        public readonly bool HasResolvedPaths;
        public readonly bool PathsStayUnderProjectRoot;
        public readonly bool SourceAndManifestAreDistinct;
        public readonly bool WritesDefinitionPlaneOnly;
        public readonly string FailureReason;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlanValidation(
            bool outputValidationPassed,
            bool hasProjectRoot,
            bool hasResolvedPaths,
            bool pathsStayUnderProjectRoot,
            bool sourceAndManifestAreDistinct,
            bool writesDefinitionPlaneOnly,
            string failureReason)
        {
            OutputValidationPassed = outputValidationPassed;
            HasProjectRoot = hasProjectRoot;
            HasResolvedPaths = hasResolvedPaths;
            PathsStayUnderProjectRoot = pathsStayUnderProjectRoot;
            SourceAndManifestAreDistinct = sourceAndManifestAreDistinct;
            WritesDefinitionPlaneOnly = writesDefinitionPlaneOnly;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Passed =>
            OutputValidationPassed
            && HasProjectRoot
            && HasResolvedPaths
            && PathsStayUnderProjectRoot
            && SourceAndManifestAreDistinct
            && WritesDefinitionPlaneOnly;
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportFileResult
    {
        public readonly string RelativePath;
        public readonly string AbsolutePath;
        public readonly int ExpectedTextHash;
        public readonly int ActualTextHash;
        public readonly long ByteCount;
        public readonly bool Exists;
        public readonly bool HashMatches;
        public readonly bool WroteUnderProjectRoot;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportFileResult(
            string relativePath,
            string absolutePath,
            int expectedTextHash,
            int actualTextHash,
            long byteCount,
            bool exists,
            bool hashMatches,
            bool wroteUnderProjectRoot)
        {
            RelativePath = relativePath ?? string.Empty;
            AbsolutePath = absolutePath ?? string.Empty;
            ExpectedTextHash = expectedTextHash;
            ActualTextHash = actualTextHash;
            ByteCount = byteCount;
            Exists = exists;
            HashMatches = hashMatches;
            WroteUnderProjectRoot = wroteUnderProjectRoot;
        }

        public bool Passed => Exists && HashMatches && WroteUnderProjectRoot && ByteCount > 0;
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportResult
    {
        public readonly bool PlanValidationPassed;
        public readonly int PlannedFileCount;
        public readonly int WrittenFileCount;
        public readonly HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportFileResult RuntimeSourceFile;
        public readonly HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportFileResult ManifestFile;
        public readonly string FailureReason;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportResult(
            bool planValidationPassed,
            int plannedFileCount,
            int writtenFileCount,
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportFileResult runtimeSourceFile,
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportFileResult manifestFile,
            string failureReason)
        {
            PlanValidationPassed = planValidationPassed;
            PlannedFileCount = plannedFileCount;
            WrittenFileCount = writtenFileCount;
            RuntimeSourceFile = runtimeSourceFile;
            ManifestFile = manifestFile;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool WroteExpectedFileCount => WrittenFileCount == PlannedFileCount;

        public bool Passed =>
            PlanValidationPassed
            && WroteExpectedFileCount
            && RuntimeSourceFile.Passed
            && ManifestFile.Passed;
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlan
    {
        public readonly HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput Output;
        public readonly string ProjectRoot;
        public readonly string RuntimeSourceAbsolutePath;
        public readonly string ManifestAbsolutePath;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlan(
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput output,
            string projectRoot,
            string runtimeSourceAbsolutePath,
            string manifestAbsolutePath)
        {
            Output = output;
            ProjectRoot = NormalizeProjectRoot(projectRoot);
            RuntimeSourceAbsolutePath = runtimeSourceAbsolutePath ?? string.Empty;
            ManifestAbsolutePath = manifestAbsolutePath ?? string.Empty;
        }

        public int PlannedFileCount => 2;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlanValidation Validate()
        {
            var outputValidation = HasOutputText(Output)
                ? Output.Validate()
                : new HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutputValidation(
                    packageValidationPassed: false,
                    hasOutputIdentity: false,
                    sourceTextHashMatches: false,
                    manifestTextHashMatches: false,
                    sourceTextContainsManifestFingerprint: false,
                    writesDefinitionPlaneOnly: false,
                    expectedSourceTextHash: 0,
                    actualSourceTextHash: 0,
                    expectedManifestTextHash: 0,
                    actualManifestTextHash: 0,
                    failureReason: "Generated source output has no source or manifest text.");
            var hasProjectRoot = Directory.Exists(ProjectRoot);
            var hasResolvedPaths =
                !string.IsNullOrEmpty(RuntimeSourceAbsolutePath)
                && !string.IsNullOrEmpty(ManifestAbsolutePath);
            var pathsStayUnderProjectRoot =
                IsPathUnderProjectRoot(ProjectRoot, RuntimeSourceAbsolutePath)
                && IsPathUnderProjectRoot(ProjectRoot, ManifestAbsolutePath);
            var sourceAndManifestAreDistinct = !string.Equals(
                RuntimeSourceAbsolutePath,
                ManifestAbsolutePath,
                StringComparison.OrdinalIgnoreCase);
            var writesDefinitionPlaneOnly = Output.WritesDefinitionPlaneOnly;
            var reason = string.Empty;

            if (!outputValidation.Passed)
                reason = outputValidation.FailureReason;
            else if (!hasProjectRoot)
                reason = "Generated source export plan is missing project root.";
            else if (!hasResolvedPaths)
                reason = "Generated source export plan could not resolve output paths.";
            else if (!pathsStayUnderProjectRoot)
                reason = "Generated source export plan paths must stay under project root.";
            else if (!sourceAndManifestAreDistinct)
                reason = "Generated source export plan source and manifest paths must be distinct.";
            else if (!writesDefinitionPlaneOnly)
                reason = "Generated source export plan must only write Definition Plane artifacts.";

            return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlanValidation(
                outputValidation.Passed,
                hasProjectRoot,
                hasResolvedPaths,
                pathsStayUnderProjectRoot,
                sourceAndManifestAreDistinct,
                writesDefinitionPlaneOnly,
                reason);
        }

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportResult WriteFiles()
        {
            var validation = Validate();
            if (!validation.Passed)
            {
                return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportResult(
                    planValidationPassed: false,
                    plannedFileCount: PlannedFileCount,
                    writtenFileCount: 0,
                    runtimeSourceFile: default,
                    manifestFile: default,
                    failureReason: validation.FailureReason);
            }

            var runtimeSourceFile = WriteFile(
                ProjectRoot,
                Output.RuntimeSourceRelativePath,
                RuntimeSourceAbsolutePath,
                Output.SourceText,
                Output.SourceTextHash);
            var manifestFile = WriteFile(
                ProjectRoot,
                Output.ManifestRelativePath,
                ManifestAbsolutePath,
                Output.ManifestText,
                Output.ManifestTextHash);
            var writtenFileCount = 0;
            if (runtimeSourceFile.Passed)
                writtenFileCount++;
            if (manifestFile.Passed)
                writtenFileCount++;
            var reason = string.Empty;

            if (writtenFileCount != PlannedFileCount)
                reason = "Generated source export plan did not write all expected files.";
            else if (!runtimeSourceFile.Passed)
                reason = "Generated source export plan failed to write runtime source.";
            else if (!manifestFile.Passed)
                reason = "Generated source export plan failed to write manifest.";

            return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportResult(
                planValidationPassed: true,
                plannedFileCount: PlannedFileCount,
                writtenFileCount: writtenFileCount,
                runtimeSourceFile: runtimeSourceFile,
                manifestFile: manifestFile,
                failureReason: reason);
        }

        public static HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlan Create(
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput output,
            string projectRoot)
        {
            var normalizedProjectRoot = NormalizeProjectRoot(projectRoot);
            return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlan(
                output,
                normalizedProjectRoot,
                ResolveProjectRelativePath(normalizedProjectRoot, output.RuntimeSourceRelativePath),
                ResolveProjectRelativePath(normalizedProjectRoot, output.ManifestRelativePath));
        }

        private static bool HasOutputText(in HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput output)
        {
            return !string.IsNullOrEmpty(output.SourceText)
                   && !string.IsNullOrEmpty(output.ManifestText);
        }

        private static HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportFileResult WriteFile(
            string projectRoot,
            string relativePath,
            string absolutePath,
            string text,
            int expectedTextHash)
        {
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(absolutePath, text ?? string.Empty, Encoding.UTF8);
            var exists = File.Exists(absolutePath);
            var actualText = exists ? File.ReadAllText(absolutePath, Encoding.UTF8) : string.Empty;
            var actualTextHash =
                HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput.CalculateTextHash(actualText);
            var byteCount = exists ? new FileInfo(absolutePath).Length : 0L;
            var hashMatches = expectedTextHash == actualTextHash;
            var wroteUnderProjectRoot = IsPathUnderProjectRoot(projectRoot, absolutePath);

            return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportFileResult(
                relativePath,
                absolutePath,
                expectedTextHash,
                actualTextHash,
                byteCount,
                exists,
                hashMatches,
                wroteUnderProjectRoot);
        }

        private static string NormalizeProjectRoot(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot))
                return string.Empty;

            return Path.GetFullPath(projectRoot);
        }

        private static string ResolveProjectRelativePath(string projectRoot, string relativePath)
        {
            if (string.IsNullOrEmpty(projectRoot)
                || string.IsNullOrEmpty(relativePath)
                || Path.IsPathRooted(relativePath))
                return string.Empty;

            var normalizedRelativePath = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, normalizedRelativePath));
        }

        private static bool IsPathUnderProjectRoot(string projectRoot, string absolutePath)
        {
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(absolutePath))
                return false;

            var root = Path.GetFullPath(projectRoot);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                && !root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;

            var path = Path.GetFullPath(absolutePath);
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlanValidation
    {
        public readonly bool UsesLubanSourceGeneratorPackage;
        public readonly bool HasToolchainIdentity;
        public readonly bool HasConfigSourceRoot;
        public readonly bool ConfigSourceStaysUnderProjectRoot;
        public readonly bool OutputValidationPassed;
        public readonly bool ExportPlanValidationPassed;
        public readonly bool WritesDefinitionPlaneOnly;
        public readonly string FailureReason;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlanValidation(
            bool usesLubanSourceGeneratorPackage,
            bool hasToolchainIdentity,
            bool hasConfigSourceRoot,
            bool configSourceStaysUnderProjectRoot,
            bool outputValidationPassed,
            bool exportPlanValidationPassed,
            bool writesDefinitionPlaneOnly,
            string failureReason)
        {
            UsesLubanSourceGeneratorPackage = usesLubanSourceGeneratorPackage;
            HasToolchainIdentity = hasToolchainIdentity;
            HasConfigSourceRoot = hasConfigSourceRoot;
            ConfigSourceStaysUnderProjectRoot = configSourceStaysUnderProjectRoot;
            OutputValidationPassed = outputValidationPassed;
            ExportPlanValidationPassed = exportPlanValidationPassed;
            WritesDefinitionPlaneOnly = writesDefinitionPlaneOnly;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Passed =>
            UsesLubanSourceGeneratorPackage
            && HasToolchainIdentity
            && HasConfigSourceRoot
            && ConfigSourceStaysUnderProjectRoot
            && OutputValidationPassed
            && ExportPlanValidationPassed
            && WritesDefinitionPlaneOnly;
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainResult
    {
        public readonly bool PlanValidationPassed;
        public readonly bool PackageValidationPassed;
        public readonly bool OutputValidationPassed;
        public readonly bool ExportResultPassed;
        public readonly int PlannedStepCount;
        public readonly int ExecutedStepCount;
        public readonly int PackageContentHash;
        public readonly int OutputSourceTextHash;
        public readonly int OutputManifestTextHash;
        public readonly HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportResult ExportResult;
        public readonly string FailureReason;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainResult(
            bool planValidationPassed,
            bool packageValidationPassed,
            bool outputValidationPassed,
            bool exportResultPassed,
            int plannedStepCount,
            int executedStepCount,
            int packageContentHash,
            int outputSourceTextHash,
            int outputManifestTextHash,
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportResult exportResult,
            string failureReason)
        {
            PlanValidationPassed = planValidationPassed;
            PackageValidationPassed = packageValidationPassed;
            OutputValidationPassed = outputValidationPassed;
            ExportResultPassed = exportResultPassed;
            PlannedStepCount = plannedStepCount;
            ExecutedStepCount = executedStepCount;
            PackageContentHash = packageContentHash;
            OutputSourceTextHash = outputSourceTextHash;
            OutputManifestTextHash = outputManifestTextHash;
            ExportResult = exportResult;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool ExecutedExpectedStepCount => ExecutedStepCount == PlannedStepCount;

        public bool Passed =>
            PlanValidationPassed
            && PackageValidationPassed
            && OutputValidationPassed
            && ExportResultPassed
            && ExecutedExpectedStepCount;
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan
    {
        public readonly string ToolchainName;
        public readonly string ConfigSourceRelativePath;
        public readonly string ConfigSourceAbsolutePath;
        public readonly HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput Output;
        public readonly HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlan ExportPlan;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan(
            string toolchainName,
            string configSourceRelativePath,
            string configSourceAbsolutePath,
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput output,
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlan exportPlan)
        {
            ToolchainName = toolchainName ?? string.Empty;
            ConfigSourceRelativePath = NormalizeRelativePath(configSourceRelativePath);
            ConfigSourceAbsolutePath = configSourceAbsolutePath ?? string.Empty;
            Output = output;
            ExportPlan = exportPlan;
        }

        public int PlannedStepCount => 3;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlanValidation Validate()
        {
            var packageValidation = Output.Package.Validate();
            var outputValidation = Output.Validate();
            var exportPlanValidation = ExportPlan.Validate();
            var usesLubanSourceGeneratorPackage =
                Output.Package.Manifest.SourceKind == HeadlessAutoChessGeneratedDefinitionSourceKind.LubanSourceGenerator;
            var hasToolchainIdentity =
                !string.IsNullOrEmpty(ToolchainName)
                && !string.IsNullOrEmpty(ConfigSourceRelativePath);
            var hasConfigSourceRoot = Directory.Exists(ConfigSourceAbsolutePath);
            var configSourceStaysUnderProjectRoot =
                IsPathUnderProjectRoot(ExportPlan.ProjectRoot, ConfigSourceAbsolutePath);
            var writesDefinitionPlaneOnly = Output.WritesDefinitionPlaneOnly;
            var reason = string.Empty;

            if (!usesLubanSourceGeneratorPackage)
                reason = "Source generator toolchain plan must use a Luban source generator package.";
            else if (!packageValidation.Passed)
                reason = packageValidation.FailureReason;
            else if (!hasToolchainIdentity)
                reason = "Source generator toolchain plan is missing toolchain identity.";
            else if (!hasConfigSourceRoot)
                reason = "Source generator toolchain plan is missing config source root.";
            else if (!configSourceStaysUnderProjectRoot)
                reason = "Source generator toolchain plan config source root must stay under project root.";
            else if (!outputValidation.Passed)
                reason = outputValidation.FailureReason;
            else if (!exportPlanValidation.Passed)
                reason = exportPlanValidation.FailureReason;
            else if (!writesDefinitionPlaneOnly)
                reason = "Source generator toolchain plan must only produce Definition Plane artifacts.";

            return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlanValidation(
                usesLubanSourceGeneratorPackage,
                hasToolchainIdentity,
                hasConfigSourceRoot,
                configSourceStaysUnderProjectRoot,
                outputValidation.Passed,
                exportPlanValidation.Passed,
                writesDefinitionPlaneOnly,
                reason);
        }

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainResult Execute()
        {
            var validation = Validate();
            if (!validation.Passed)
            {
                return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainResult(
                    planValidationPassed: false,
                    packageValidationPassed: false,
                    outputValidationPassed: false,
                    exportResultPassed: false,
                    plannedStepCount: PlannedStepCount,
                    executedStepCount: 0,
                    packageContentHash: 0,
                    outputSourceTextHash: 0,
                    outputManifestTextHash: 0,
                    exportResult: default,
                    failureReason: validation.FailureReason);
            }

            var packageValidation = Output.Package.Validate();
            var outputValidation = Output.Validate();
            var exportResult = ExportPlan.WriteFiles();
            var executedStepCount = 0;
            if (packageValidation.Passed)
                executedStepCount++;
            if (outputValidation.Passed)
                executedStepCount++;
            if (exportResult.Passed)
                executedStepCount++;
            var reason = string.Empty;

            if (!packageValidation.Passed)
                reason = packageValidation.FailureReason;
            else if (!outputValidation.Passed)
                reason = outputValidation.FailureReason;
            else if (!exportResult.Passed)
                reason = exportResult.FailureReason;
            else if (executedStepCount != PlannedStepCount)
                reason = "Source generator toolchain plan did not execute all expected steps.";

            return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainResult(
                planValidationPassed: true,
                packageValidationPassed: packageValidation.Passed,
                outputValidationPassed: outputValidation.Passed,
                exportResultPassed: exportResult.Passed,
                plannedStepCount: PlannedStepCount,
                executedStepCount: executedStepCount,
                packageContentHash: Output.Package.Manifest.ContentHash,
                outputSourceTextHash: Output.SourceTextHash,
                outputManifestTextHash: Output.ManifestTextHash,
                exportResult: exportResult,
                failureReason: reason);
        }

        public static HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan Create(
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput output,
            string projectRoot,
            string configSourceRelativePath)
        {
            var exportPlan = HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportPlan.Create(
                output,
                projectRoot);
            return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan(
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedToolchainName,
                configSourceRelativePath,
                ResolveProjectRelativePath(exportPlan.ProjectRoot, configSourceRelativePath),
                output,
                exportPlan);
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            return (relativePath ?? string.Empty)
                .Replace('\\', '/')
                .Trim('/');
        }

        private static string ResolveProjectRelativePath(string projectRoot, string relativePath)
        {
            if (string.IsNullOrEmpty(projectRoot)
                || string.IsNullOrEmpty(relativePath)
                || Path.IsPathRooted(relativePath))
                return string.Empty;

            var normalizedRelativePath = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, normalizedRelativePath));
        }

        private static bool IsPathUnderProjectRoot(string projectRoot, string absolutePath)
        {
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(absolutePath))
                return false;

            var root = Path.GetFullPath(projectRoot);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                && !root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;

            var path = Path.GetFullPath(absolutePath);
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionLubanProcessPlanValidation
    {
        public readonly bool HasProjectRoot;
        public readonly bool HasDotNetExecutable;
        public readonly bool HasLubanDll;
        public readonly bool HasConfigSourceRoot;
        public readonly bool HasLubanConf;
        public readonly bool InputPathsStayUnderProjectRoot;
        public readonly bool HasOutputRoots;
        public readonly bool OutputPathsStayUnderProjectRoot;
        public readonly bool OutputPathsAreDistinct;
        public readonly bool UsesDefinitionGenerationTargets;
        public readonly string FailureReason;

        public HeadlessAutoChessGeneratedDefinitionLubanProcessPlanValidation(
            bool hasProjectRoot,
            bool hasDotNetExecutable,
            bool hasLubanDll,
            bool hasConfigSourceRoot,
            bool hasLubanConf,
            bool inputPathsStayUnderProjectRoot,
            bool hasOutputRoots,
            bool outputPathsStayUnderProjectRoot,
            bool outputPathsAreDistinct,
            bool usesDefinitionGenerationTargets,
            string failureReason)
        {
            HasProjectRoot = hasProjectRoot;
            HasDotNetExecutable = hasDotNetExecutable;
            HasLubanDll = hasLubanDll;
            HasConfigSourceRoot = hasConfigSourceRoot;
            HasLubanConf = hasLubanConf;
            InputPathsStayUnderProjectRoot = inputPathsStayUnderProjectRoot;
            HasOutputRoots = hasOutputRoots;
            OutputPathsStayUnderProjectRoot = outputPathsStayUnderProjectRoot;
            OutputPathsAreDistinct = outputPathsAreDistinct;
            UsesDefinitionGenerationTargets = usesDefinitionGenerationTargets;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Passed =>
            HasProjectRoot
            && HasDotNetExecutable
            && HasLubanDll
            && HasConfigSourceRoot
            && HasLubanConf
            && InputPathsStayUnderProjectRoot
            && HasOutputRoots
            && OutputPathsStayUnderProjectRoot
            && OutputPathsAreDistinct
            && UsesDefinitionGenerationTargets;
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionLubanProcessResult
    {
        public readonly bool PlanValidationPassed;
        public readonly bool ProcessStarted;
        public readonly bool ProcessExited;
        public readonly int ExitCode;
        public readonly int GeneratedCodeFileCount;
        public readonly int GeneratedDataFileCount;
        public readonly long GeneratedByteCount;
        public readonly int GeneratedOutputHash;
        public readonly int StandardOutputHash;
        public readonly int StandardErrorHash;
        public readonly string FailureReason;

        public HeadlessAutoChessGeneratedDefinitionLubanProcessResult(
            bool planValidationPassed,
            bool processStarted,
            bool processExited,
            int exitCode,
            int generatedCodeFileCount,
            int generatedDataFileCount,
            long generatedByteCount,
            int generatedOutputHash,
            int standardOutputHash,
            int standardErrorHash,
            string failureReason)
        {
            PlanValidationPassed = planValidationPassed;
            ProcessStarted = processStarted;
            ProcessExited = processExited;
            ExitCode = exitCode;
            GeneratedCodeFileCount = generatedCodeFileCount;
            GeneratedDataFileCount = generatedDataFileCount;
            GeneratedByteCount = generatedByteCount;
            GeneratedOutputHash = generatedOutputHash;
            StandardOutputHash = standardOutputHash;
            StandardErrorHash = standardErrorHash;
            FailureReason = failureReason ?? string.Empty;
        }

        public int GeneratedFileCount => GeneratedCodeFileCount + GeneratedDataFileCount;

        public bool HasGeneratedFiles =>
            GeneratedCodeFileCount > 0
            && GeneratedDataFileCount > 0
            && GeneratedByteCount > 0;

        public bool Passed =>
            PlanValidationPassed
            && ProcessStarted
            && ProcessExited
            && ExitCode == 0
            && HasGeneratedFiles
            && GeneratedOutputHash != 0;
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionLubanProcessPlan
    {
        public readonly string ProjectRoot;
        public readonly string DotNetExecutablePath;
        public readonly string LubanDllRelativePath;
        public readonly string LubanDllAbsolutePath;
        public readonly string ConfigSourceRelativePath;
        public readonly string ConfigSourceAbsolutePath;
        public readonly string LubanConfAbsolutePath;
        public readonly string OutputCodeRelativePath;
        public readonly string OutputCodeAbsolutePath;
        public readonly string OutputDataRelativePath;
        public readonly string OutputDataAbsolutePath;
        public readonly string TargetName;
        public readonly string CodeGenerationTarget;
        public readonly string DataGenerationTarget;

        public HeadlessAutoChessGeneratedDefinitionLubanProcessPlan(
            string projectRoot,
            string dotNetExecutablePath,
            string lubanDllRelativePath,
            string lubanDllAbsolutePath,
            string configSourceRelativePath,
            string configSourceAbsolutePath,
            string lubanConfAbsolutePath,
            string outputCodeRelativePath,
            string outputCodeAbsolutePath,
            string outputDataRelativePath,
            string outputDataAbsolutePath,
            string targetName,
            string codeGenerationTarget,
            string dataGenerationTarget)
        {
            ProjectRoot = NormalizeProjectRoot(projectRoot);
            DotNetExecutablePath = dotNetExecutablePath ?? string.Empty;
            LubanDllRelativePath = NormalizeRelativePath(lubanDllRelativePath);
            LubanDllAbsolutePath = lubanDllAbsolutePath ?? string.Empty;
            ConfigSourceRelativePath = NormalizeRelativePath(configSourceRelativePath);
            ConfigSourceAbsolutePath = configSourceAbsolutePath ?? string.Empty;
            LubanConfAbsolutePath = lubanConfAbsolutePath ?? string.Empty;
            OutputCodeRelativePath = NormalizeRelativePath(outputCodeRelativePath);
            OutputCodeAbsolutePath = outputCodeAbsolutePath ?? string.Empty;
            OutputDataRelativePath = NormalizeRelativePath(outputDataRelativePath);
            OutputDataAbsolutePath = outputDataAbsolutePath ?? string.Empty;
            TargetName = targetName ?? string.Empty;
            CodeGenerationTarget = codeGenerationTarget ?? string.Empty;
            DataGenerationTarget = dataGenerationTarget ?? string.Empty;
        }

        public HeadlessAutoChessGeneratedDefinitionLubanProcessPlanValidation Validate()
        {
            var hasProjectRoot = Directory.Exists(ProjectRoot);
            var hasDotNetExecutable = !string.IsNullOrEmpty(DotNetExecutablePath);
            var hasLubanDll = File.Exists(LubanDllAbsolutePath);
            var hasConfigSourceRoot = Directory.Exists(ConfigSourceAbsolutePath);
            var hasLubanConf = File.Exists(LubanConfAbsolutePath);
            var inputPathsStayUnderProjectRoot =
                IsPathUnderProjectRoot(ProjectRoot, LubanDllAbsolutePath)
                && IsPathUnderProjectRoot(ProjectRoot, ConfigSourceAbsolutePath)
                && IsPathUnderProjectRoot(ProjectRoot, LubanConfAbsolutePath);
            var hasOutputRoots =
                !string.IsNullOrEmpty(OutputCodeAbsolutePath)
                && !string.IsNullOrEmpty(OutputDataAbsolutePath);
            var outputPathsStayUnderProjectRoot =
                IsPathUnderProjectRoot(ProjectRoot, OutputCodeAbsolutePath)
                && IsPathUnderProjectRoot(ProjectRoot, OutputDataAbsolutePath);
            var outputPathsAreDistinct = !string.Equals(
                OutputCodeAbsolutePath,
                OutputDataAbsolutePath,
                StringComparison.OrdinalIgnoreCase);
            var usesDefinitionGenerationTargets =
                string.Equals(TargetName, "client", StringComparison.Ordinal)
                && string.Equals(CodeGenerationTarget, "cs-simple-json", StringComparison.Ordinal)
                && string.Equals(DataGenerationTarget, "json", StringComparison.Ordinal);
            var reason = string.Empty;

            if (!hasProjectRoot)
                reason = "Luban process plan is missing project root.";
            else if (!hasDotNetExecutable)
                reason = "Luban process plan is missing dotnet executable.";
            else if (!hasLubanDll)
                reason = "Luban process plan is missing Luban dll.";
            else if (!hasConfigSourceRoot)
                reason = "Luban process plan is missing config source root.";
            else if (!hasLubanConf)
                reason = "Luban process plan is missing luban.conf.";
            else if (!inputPathsStayUnderProjectRoot)
                reason = "Luban process plan input paths must stay under project root.";
            else if (!hasOutputRoots)
                reason = "Luban process plan is missing output roots.";
            else if (!outputPathsStayUnderProjectRoot)
                reason = "Luban process plan output paths must stay under project root.";
            else if (!outputPathsAreDistinct)
                reason = "Luban process plan code and data output roots must be distinct.";
            else if (!usesDefinitionGenerationTargets)
                reason = "Luban process plan must use the client cs-simple-json/json definition targets.";

            return new HeadlessAutoChessGeneratedDefinitionLubanProcessPlanValidation(
                hasProjectRoot,
                hasDotNetExecutable,
                hasLubanDll,
                hasConfigSourceRoot,
                hasLubanConf,
                inputPathsStayUnderProjectRoot,
                hasOutputRoots,
                outputPathsStayUnderProjectRoot,
                outputPathsAreDistinct,
                usesDefinitionGenerationTargets,
                reason);
        }

        public string BuildArgumentString()
        {
            var builder = new StringBuilder(512);
            AppendProcessArgument(builder, LubanDllAbsolutePath);
            AppendProcessArgument(builder, "-t");
            AppendProcessArgument(builder, TargetName);
            AppendProcessArgument(builder, "-c");
            AppendProcessArgument(builder, CodeGenerationTarget);
            AppendProcessArgument(builder, "-d");
            AppendProcessArgument(builder, DataGenerationTarget);
            AppendProcessArgument(builder, "--conf");
            AppendProcessArgument(builder, LubanConfAbsolutePath);
            AppendProcessArgument(builder, "-x");
            AppendProcessArgument(builder, "outputCodeDir=" + OutputCodeAbsolutePath);
            AppendProcessArgument(builder, "-x");
            AppendProcessArgument(builder, "outputDataDir=" + OutputDataAbsolutePath);
            return builder.ToString();
        }

        public HeadlessAutoChessGeneratedDefinitionLubanProcessResult Execute(int timeoutMilliseconds = 60000)
        {
            var validation = Validate();
            if (!validation.Passed)
            {
                return new HeadlessAutoChessGeneratedDefinitionLubanProcessResult(
                    planValidationPassed: false,
                    processStarted: false,
                    processExited: false,
                    exitCode: -1,
                    generatedCodeFileCount: 0,
                    generatedDataFileCount: 0,
                    generatedByteCount: 0,
                    generatedOutputHash: 0,
                    standardOutputHash: 0,
                    standardErrorHash: 0,
                    failureReason: validation.FailureReason);
            }

            try
            {
                Directory.CreateDirectory(OutputCodeAbsolutePath);
                Directory.CreateDirectory(OutputDataAbsolutePath);

                var standardOutput = new StringBuilder(2048);
                var standardError = new StringBuilder(2048);
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = DotNetExecutablePath,
                    Arguments = BuildArgumentString(),
                    WorkingDirectory = ConfigSourceAbsolutePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                process.OutputDataReceived += (_, args) =>
                {
                    if (args.Data != null)
                        standardOutput.AppendLine(args.Data);
                };
                process.ErrorDataReceived += (_, args) =>
                {
                    if (args.Data != null)
                        standardError.AppendLine(args.Data);
                };

                var started = process.Start();
                if (!started)
                {
                    return new HeadlessAutoChessGeneratedDefinitionLubanProcessResult(
                        planValidationPassed: true,
                        processStarted: false,
                        processExited: false,
                        exitCode: -1,
                        generatedCodeFileCount: 0,
                        generatedDataFileCount: 0,
                        generatedByteCount: 0,
                        generatedOutputHash: 0,
                        standardOutputHash: 0,
                        standardErrorHash: 0,
                        failureReason: "Luban process did not start.");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var exited = process.WaitForExit(timeoutMilliseconds);
                if (!exited)
                {
                    process.Kill();
                    return new HeadlessAutoChessGeneratedDefinitionLubanProcessResult(
                        planValidationPassed: true,
                        processStarted: true,
                        processExited: false,
                        exitCode: -1,
                        generatedCodeFileCount: CountFiles(OutputCodeAbsolutePath),
                        generatedDataFileCount: CountFiles(OutputDataAbsolutePath),
                        generatedByteCount: CountBytes(OutputCodeAbsolutePath) + CountBytes(OutputDataAbsolutePath),
                        generatedOutputHash: CalculateCombinedDirectoryHash(OutputCodeAbsolutePath, OutputDataAbsolutePath),
                        standardOutputHash: HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput.CalculateTextHash(standardOutput.ToString()),
                        standardErrorHash: HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput.CalculateTextHash(standardError.ToString()),
                        failureReason: "Luban process timed out.");
                }

                process.WaitForExit();
                var codeFileCount = CountFiles(OutputCodeAbsolutePath);
                var dataFileCount = CountFiles(OutputDataAbsolutePath);
                var generatedByteCount = CountBytes(OutputCodeAbsolutePath) + CountBytes(OutputDataAbsolutePath);
                var generatedOutputHash =
                    CalculateCombinedDirectoryHash(OutputCodeAbsolutePath, OutputDataAbsolutePath);
                var outputHash =
                    HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput.CalculateTextHash(
                        standardOutput.ToString());
                var errorHash =
                    HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput.CalculateTextHash(
                        standardError.ToString());
                var reason = string.Empty;

                if (process.ExitCode != 0)
                    reason = "Luban process exited with non-zero code.";
                else if (codeFileCount <= 0)
                    reason = "Luban process did not generate code files.";
                else if (dataFileCount <= 0)
                    reason = "Luban process did not generate data files.";
                else if (generatedByteCount <= 0)
                    reason = "Luban process generated empty output.";
                else if (generatedOutputHash == 0)
                    reason = "Luban process generated an empty output hash.";

                return new HeadlessAutoChessGeneratedDefinitionLubanProcessResult(
                    planValidationPassed: true,
                    processStarted: true,
                    processExited: true,
                    exitCode: process.ExitCode,
                    generatedCodeFileCount: codeFileCount,
                    generatedDataFileCount: dataFileCount,
                    generatedByteCount: generatedByteCount,
                    generatedOutputHash: generatedOutputHash,
                    standardOutputHash: outputHash,
                    standardErrorHash: errorHash,
                    failureReason: reason);
            }
            catch (Exception exception)
            {
                return new HeadlessAutoChessGeneratedDefinitionLubanProcessResult(
                    planValidationPassed: true,
                    processStarted: false,
                    processExited: false,
                    exitCode: -1,
                    generatedCodeFileCount: CountFiles(OutputCodeAbsolutePath),
                    generatedDataFileCount: CountFiles(OutputDataAbsolutePath),
                    generatedByteCount: CountBytes(OutputCodeAbsolutePath) + CountBytes(OutputDataAbsolutePath),
                    generatedOutputHash: CalculateCombinedDirectoryHash(OutputCodeAbsolutePath, OutputDataAbsolutePath),
                    standardOutputHash: 0,
                    standardErrorHash: HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput.CalculateTextHash(
                        exception.GetType().FullName + ":" + exception.Message),
                    failureReason: "Luban process failed to start or execute.");
            }
        }

        public static HeadlessAutoChessGeneratedDefinitionLubanProcessPlan Create(
            string projectRoot,
            string configSourceRelativePath,
            string outputCodeRelativePath,
            string outputDataRelativePath)
        {
            return Create(
                projectRoot,
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedDotNetExecutablePath,
                HeadlessAutoChessGeneratedDefinitionRows.GeneratedLubanDllRelativePath,
                configSourceRelativePath,
                outputCodeRelativePath,
                outputDataRelativePath);
        }

        public static HeadlessAutoChessGeneratedDefinitionLubanProcessPlan Create(
            string projectRoot,
            string dotNetExecutablePath,
            string lubanDllRelativePath,
            string configSourceRelativePath,
            string outputCodeRelativePath,
            string outputDataRelativePath)
        {
            var normalizedProjectRoot = NormalizeProjectRoot(projectRoot);
            var normalizedConfigSourceRelativePath = NormalizeRelativePath(configSourceRelativePath);
            var configSourceAbsolutePath =
                ResolveProjectRelativePath(normalizedProjectRoot, normalizedConfigSourceRelativePath);
            return new HeadlessAutoChessGeneratedDefinitionLubanProcessPlan(
                normalizedProjectRoot,
                dotNetExecutablePath,
                lubanDllRelativePath,
                ResolveProjectRelativePath(normalizedProjectRoot, lubanDllRelativePath),
                normalizedConfigSourceRelativePath,
                configSourceAbsolutePath,
                string.IsNullOrEmpty(configSourceAbsolutePath)
                    ? string.Empty
                    : Path.GetFullPath(Path.Combine(configSourceAbsolutePath, "luban.conf")),
                outputCodeRelativePath,
                ResolveProjectRelativePath(normalizedProjectRoot, outputCodeRelativePath),
                outputDataRelativePath,
                ResolveProjectRelativePath(normalizedProjectRoot, outputDataRelativePath),
                targetName: "client",
                codeGenerationTarget: "cs-simple-json",
                dataGenerationTarget: "json");
        }

        private static void AppendProcessArgument(StringBuilder builder, string argument)
        {
            if (builder.Length > 0)
                builder.Append(' ');

            argument ??= string.Empty;
            var needsQuotes = argument.Length == 0;
            for (var i = 0; i < argument.Length && !needsQuotes; i++)
            {
                var character = argument[i];
                needsQuotes = char.IsWhiteSpace(character) || character == '"';
            }

            if (!needsQuotes)
            {
                builder.Append(argument);
                return;
            }

            builder.Append('"');
            for (var i = 0; i < argument.Length; i++)
            {
                var character = argument[i];
                if (character == '"')
                    builder.Append('\\');
                builder.Append(character);
            }

            builder.Append('"');
        }

        private static int CountFiles(string directory)
        {
            return Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length
                : 0;
        }

        private static long CountBytes(string directory)
        {
            if (!Directory.Exists(directory))
                return 0L;

            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            long total = 0;
            for (var i = 0; i < files.Length; i++)
                total += new FileInfo(files[i]).Length;

            return total;
        }

        private static int CalculateCombinedDirectoryHash(string firstDirectory, string secondDirectory)
        {
            unchecked
            {
                var hash = 2166136261u;
                AppendDirectoryHash(ref hash, firstDirectory);
                AppendDirectoryHash(ref hash, secondDirectory);
                return (int)hash;
            }
        }

        private static void AppendDirectoryHash(ref uint hash, string directory)
        {
            if (!Directory.Exists(directory))
                return;

            var root = Path.GetFullPath(directory);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                && !root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;

            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < files.Length; i++)
            {
                var file = Path.GetFullPath(files[i]);
                var relativePath = file.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                    ? file.Substring(root.Length)
                    : file;
                AppendHash(ref hash, relativePath.Replace('\\', '/'));
                var bytes = File.ReadAllBytes(file);
                for (var byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
                {
                    hash ^= bytes[byteIndex];
                    hash *= 16777619u;
                }
            }
        }

        private static void AppendHash(ref uint hash, string value)
        {
            value ??= string.Empty;
            for (var i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
        }

        private static string NormalizeProjectRoot(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot))
                return string.Empty;

            return Path.GetFullPath(projectRoot);
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            return (relativePath ?? string.Empty)
                .Replace('\\', '/')
                .Trim('/');
        }

        private static string ResolveProjectRelativePath(string projectRoot, string relativePath)
        {
            if (string.IsNullOrEmpty(projectRoot)
                || string.IsNullOrEmpty(relativePath)
                || Path.IsPathRooted(relativePath))
                return string.Empty;

            var normalizedRelativePath = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, normalizedRelativePath));
        }

        private static bool IsPathUnderProjectRoot(string projectRoot, string absolutePath)
        {
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(absolutePath))
                return false;

            var root = Path.GetFullPath(projectRoot);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                && !root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;

            var path = Path.GetFullPath(absolutePath);
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainResult
    {
        public readonly bool LubanProcessResultPassed;
        public readonly HeadlessAutoChessGeneratedDefinitionLubanProcessResult LubanProcessResult;
        public readonly HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainResult ToolchainResult;
        public readonly HeadlessAutoChessGeneratedDefinitionAuthoringSnapshot AuthoringSnapshot;
        public readonly string FailureReason;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainResult(
            bool lubanProcessResultPassed,
            HeadlessAutoChessGeneratedDefinitionLubanProcessResult lubanProcessResult,
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainResult toolchainResult,
            HeadlessAutoChessGeneratedDefinitionAuthoringSnapshot authoringSnapshot,
            string failureReason)
        {
            LubanProcessResultPassed = lubanProcessResultPassed;
            LubanProcessResult = lubanProcessResult;
            ToolchainResult = toolchainResult;
            AuthoringSnapshot = authoringSnapshot;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Passed =>
            LubanProcessResultPassed
            && ToolchainResult.Passed
            && AuthoringSnapshot.ReadyForAuthoring;
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainPlan
    {
        public readonly HeadlessAutoChessGeneratedDefinitionLubanProcessPlan LubanProcessPlan;
        public readonly HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan ToolchainPlan;

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainPlan(
            HeadlessAutoChessGeneratedDefinitionLubanProcessPlan lubanProcessPlan,
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan toolchainPlan)
        {
            LubanProcessPlan = lubanProcessPlan;
            ToolchainPlan = toolchainPlan;
        }

        public HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainResult Execute(
            int lubanTimeoutMilliseconds = 60000)
        {
            var lubanResult = LubanProcessPlan.Execute(lubanTimeoutMilliseconds);
            if (!lubanResult.Passed)
            {
                return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainResult(
                    lubanProcessResultPassed: false,
                    lubanProcessResult: lubanResult,
                    toolchainResult: default,
                    authoringSnapshot: HeadlessAutoChessGeneratedDefinitionAuthoringSnapshot.Create(
                        ToolchainPlan,
                        default),
                    failureReason: lubanResult.FailureReason);
            }

            var toolchainResult = ToolchainPlan.Execute();
            var authoringSnapshot =
                HeadlessAutoChessGeneratedDefinitionAuthoringSnapshot.Create(ToolchainPlan, toolchainResult);
            var reason = string.Empty;
            if (!toolchainResult.Passed)
                reason = toolchainResult.FailureReason;
            else if (!authoringSnapshot.ReadyForAuthoring)
                reason = authoringSnapshot.FailureReason;

            return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainResult(
                lubanProcessResultPassed: true,
                lubanProcessResult: lubanResult,
                toolchainResult: toolchainResult,
                authoringSnapshot: authoringSnapshot,
                failureReason: reason);
        }

        public static HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainPlan Create(
            HeadlessAutoChessGeneratedDefinitionLubanProcessPlan lubanProcessPlan,
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan toolchainPlan)
        {
            return new HeadlessAutoChessGeneratedDefinitionSourceGeneratorProcessToolchainPlan(
                lubanProcessPlan,
                toolchainPlan);
        }
    }

    public enum HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus
    {
        Unknown = 0,
        Ready = 1,
        PendingExecution = 2,
        BlockedByValidation = 3,
        ArtifactIncomplete = 4,
    }

    public readonly struct HeadlessAutoChessGeneratedDefinitionAuthoringSnapshot
    {
        public readonly HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus Status;
        public readonly HeadlessAutoChessGeneratedDefinitionSourceKind SourceKind;
        public readonly string ToolchainName;
        public readonly string PackageName;
        public readonly string SchemaVersion;
        public readonly string GeneratorName;
        public readonly string SourceRevision;
        public readonly string ConfigSourceRelativePath;
        public readonly string ConfigSourceAbsolutePath;
        public readonly string ProjectRoot;
        public readonly string RuntimeSourceRelativePath;
        public readonly string ManifestRelativePath;
        public readonly string RuntimeSourceAbsolutePath;
        public readonly string ManifestAbsolutePath;
        public readonly string GeneratedNamespace;
        public readonly string GeneratedTypeName;
        public readonly int AbilityRowCount;
        public readonly int GameplayEffectRowCount;
        public readonly int AttributeSetRowCount;
        public readonly int AttributeRowCount;
        public readonly int GameplayTagRowCount;
        public readonly int GameplayCueRowCount;
        public readonly int TimelineRowCount;
        public readonly int SummonRowCount;
        public readonly int PackageContentHash;
        public readonly int SourceTextHash;
        public readonly int ManifestTextHash;
        public readonly int PlannedStepCount;
        public readonly int ExecutedStepCount;
        public readonly int PlannedFileCount;
        public readonly int WrittenFileCount;
        public readonly long RuntimeSourceByteCount;
        public readonly long ManifestByteCount;
        public readonly bool PlanValidationPassed;
        public readonly bool PackageValidationPassed;
        public readonly bool OutputValidationPassed;
        public readonly bool ExportPlanValidationPassed;
        public readonly bool ToolchainResultPassed;
        public readonly bool ExportResultPassed;
        public readonly bool ExecutedExpectedStepCount;
        public readonly bool ConfigSourceRootExists;
        public readonly bool ConfigSourceStaysUnderProjectRoot;
        public readonly bool WritesDefinitionPlaneOnly;
        public readonly bool RuntimeSourceFileExists;
        public readonly bool ManifestFileExists;
        public readonly bool RuntimeSourceFileHashMatches;
        public readonly bool ManifestFileHashMatches;
        public readonly string FailureReason;

        public HeadlessAutoChessGeneratedDefinitionAuthoringSnapshot(
            HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus status,
            HeadlessAutoChessGeneratedDefinitionSourceKind sourceKind,
            string toolchainName,
            string packageName,
            string schemaVersion,
            string generatorName,
            string sourceRevision,
            string configSourceRelativePath,
            string configSourceAbsolutePath,
            string projectRoot,
            string runtimeSourceRelativePath,
            string manifestRelativePath,
            string runtimeSourceAbsolutePath,
            string manifestAbsolutePath,
            string generatedNamespace,
            string generatedTypeName,
            int abilityRowCount,
            int gameplayEffectRowCount,
            int attributeSetRowCount,
            int attributeRowCount,
            int gameplayTagRowCount,
            int gameplayCueRowCount,
            int timelineRowCount,
            int summonRowCount,
            int packageContentHash,
            int sourceTextHash,
            int manifestTextHash,
            int plannedStepCount,
            int executedStepCount,
            int plannedFileCount,
            int writtenFileCount,
            long runtimeSourceByteCount,
            long manifestByteCount,
            bool planValidationPassed,
            bool packageValidationPassed,
            bool outputValidationPassed,
            bool exportPlanValidationPassed,
            bool toolchainResultPassed,
            bool exportResultPassed,
            bool executedExpectedStepCount,
            bool configSourceRootExists,
            bool configSourceStaysUnderProjectRoot,
            bool writesDefinitionPlaneOnly,
            bool runtimeSourceFileExists,
            bool manifestFileExists,
            bool runtimeSourceFileHashMatches,
            bool manifestFileHashMatches,
            string failureReason)
        {
            Status = status;
            SourceKind = sourceKind;
            ToolchainName = toolchainName ?? string.Empty;
            PackageName = packageName ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            GeneratorName = generatorName ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            ConfigSourceRelativePath = configSourceRelativePath ?? string.Empty;
            ConfigSourceAbsolutePath = configSourceAbsolutePath ?? string.Empty;
            ProjectRoot = projectRoot ?? string.Empty;
            RuntimeSourceRelativePath = runtimeSourceRelativePath ?? string.Empty;
            ManifestRelativePath = manifestRelativePath ?? string.Empty;
            RuntimeSourceAbsolutePath = runtimeSourceAbsolutePath ?? string.Empty;
            ManifestAbsolutePath = manifestAbsolutePath ?? string.Empty;
            GeneratedNamespace = generatedNamespace ?? string.Empty;
            GeneratedTypeName = generatedTypeName ?? string.Empty;
            AbilityRowCount = abilityRowCount;
            GameplayEffectRowCount = gameplayEffectRowCount;
            AttributeSetRowCount = attributeSetRowCount;
            AttributeRowCount = attributeRowCount;
            GameplayTagRowCount = gameplayTagRowCount;
            GameplayCueRowCount = gameplayCueRowCount;
            TimelineRowCount = timelineRowCount;
            SummonRowCount = summonRowCount;
            PackageContentHash = packageContentHash;
            SourceTextHash = sourceTextHash;
            ManifestTextHash = manifestTextHash;
            PlannedStepCount = plannedStepCount;
            ExecutedStepCount = executedStepCount;
            PlannedFileCount = plannedFileCount;
            WrittenFileCount = writtenFileCount;
            RuntimeSourceByteCount = runtimeSourceByteCount;
            ManifestByteCount = manifestByteCount;
            PlanValidationPassed = planValidationPassed;
            PackageValidationPassed = packageValidationPassed;
            OutputValidationPassed = outputValidationPassed;
            ExportPlanValidationPassed = exportPlanValidationPassed;
            ToolchainResultPassed = toolchainResultPassed;
            ExportResultPassed = exportResultPassed;
            ExecutedExpectedStepCount = executedExpectedStepCount;
            ConfigSourceRootExists = configSourceRootExists;
            ConfigSourceStaysUnderProjectRoot = configSourceStaysUnderProjectRoot;
            WritesDefinitionPlaneOnly = writesDefinitionPlaneOnly;
            RuntimeSourceFileExists = runtimeSourceFileExists;
            ManifestFileExists = manifestFileExists;
            RuntimeSourceFileHashMatches = runtimeSourceFileHashMatches;
            ManifestFileHashMatches = manifestFileHashMatches;
            FailureReason = failureReason ?? string.Empty;
        }

        public int TotalRowCount =>
            AbilityRowCount
            + GameplayEffectRowCount
            + AttributeSetRowCount
            + AttributeRowCount
            + GameplayTagRowCount
            + GameplayCueRowCount
            + TimelineRowCount
            + SummonRowCount;

        public bool ValidationPassed =>
            PlanValidationPassed
            && PackageValidationPassed
            && OutputValidationPassed
            && ExportPlanValidationPassed
            && ConfigSourceRootExists
            && ConfigSourceStaysUnderProjectRoot
            && WritesDefinitionPlaneOnly;

        public bool ArtifactFilesReady =>
            WrittenFileCount == PlannedFileCount
            && RuntimeSourceFileExists
            && ManifestFileExists;

        public bool ArtifactHashesMatch =>
            RuntimeSourceFileHashMatches
            && ManifestFileHashMatches;

        public bool ReadyForAuthoring =>
            Status == HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus.Ready
            && ValidationPassed
            && ToolchainResultPassed
            && ExportResultPassed
            && ExecutedExpectedStepCount
            && ArtifactFilesReady
            && ArtifactHashesMatch;

        public string FormatSummary()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "status={0}; ready={1}; package={2}; schema={3}; generator={4}; rows={5}; contentHash={6}; sourceHash={7}; manifestHash={8}; steps={9}/{10}; files={11}/{12}; runtimeSource={13}; manifest={14}; failure={15}",
                Status,
                ReadyForAuthoring,
                PackageName,
                SchemaVersion,
                GeneratorName,
                TotalRowCount,
                PackageContentHash,
                SourceTextHash,
                ManifestTextHash,
                ExecutedStepCount,
                PlannedStepCount,
                WrittenFileCount,
                PlannedFileCount,
                RuntimeSourceRelativePath,
                ManifestRelativePath,
                FailureReason);
        }

        public static HeadlessAutoChessGeneratedDefinitionAuthoringSnapshot Create(
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlan plan,
            HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainResult result)
        {
            var manifest = plan.Output.Package.Manifest;
            var planValidation = plan.Validate();
            var packageValidation = plan.Output.Package.Validate();
            var outputValidation = plan.Output.Validate();
            var exportPlanValidation = plan.ExportPlan.Validate();
            var exportResult = result.ExportResult;
            var runtimeSourceFile = exportResult.RuntimeSourceFile;
            var manifestFile = exportResult.ManifestFile;
            var status = ResolveStatus(planValidation, result, exportResult);
            var failureReason = ResolveFailureReason(planValidation, result, exportResult);
            var executedExpectedStepCount =
                result.PlannedStepCount == plan.PlannedStepCount
                && result.ExecutedExpectedStepCount;

            return new HeadlessAutoChessGeneratedDefinitionAuthoringSnapshot(
                status,
                manifest.SourceKind,
                plan.ToolchainName,
                manifest.PackageName,
                manifest.SchemaVersion,
                manifest.GeneratorName,
                manifest.SourceRevision,
                plan.ConfigSourceRelativePath,
                plan.ConfigSourceAbsolutePath,
                plan.ExportPlan.ProjectRoot,
                plan.Output.RuntimeSourceRelativePath,
                plan.Output.ManifestRelativePath,
                plan.ExportPlan.RuntimeSourceAbsolutePath,
                plan.ExportPlan.ManifestAbsolutePath,
                plan.Output.GeneratedNamespace,
                plan.Output.GeneratedTypeName,
                manifest.AbilityRowCount,
                manifest.GameplayEffectRowCount,
                manifest.AttributeSetRowCount,
                manifest.AttributeRowCount,
                manifest.GameplayTagRowCount,
                manifest.GameplayCueRowCount,
                manifest.TimelineRowCount,
                manifest.SummonRowCount,
                manifest.ContentHash,
                plan.Output.SourceTextHash,
                plan.Output.ManifestTextHash,
                plan.PlannedStepCount,
                result.ExecutedStepCount,
                plan.ExportPlan.PlannedFileCount,
                exportResult.WrittenFileCount,
                runtimeSourceFile.ByteCount,
                manifestFile.ByteCount,
                planValidation.Passed,
                packageValidation.Passed,
                outputValidation.Passed,
                exportPlanValidation.Passed,
                result.Passed,
                result.ExportResultPassed && exportResult.Passed,
                executedExpectedStepCount,
                planValidation.HasConfigSourceRoot,
                planValidation.ConfigSourceStaysUnderProjectRoot,
                planValidation.WritesDefinitionPlaneOnly && outputValidation.WritesDefinitionPlaneOnly,
                runtimeSourceFile.Exists,
                manifestFile.Exists,
                runtimeSourceFile.HashMatches,
                manifestFile.HashMatches,
                failureReason);
        }

        private static HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus ResolveStatus(
            in HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlanValidation planValidation,
            in HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainResult result,
            in HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportResult exportResult)
        {
            if (!planValidation.Passed)
                return HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus.BlockedByValidation;

            if (IsPendingExecution(result))
                return HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus.PendingExecution;

            if (result.Passed
                && exportResult.Passed
                && exportResult.WroteExpectedFileCount
                && exportResult.RuntimeSourceFile.Exists
                && exportResult.ManifestFile.Exists
                && exportResult.RuntimeSourceFile.HashMatches
                && exportResult.ManifestFile.HashMatches)
                return HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus.Ready;

            return HeadlessAutoChessGeneratedDefinitionAuthoringSnapshotStatus.ArtifactIncomplete;
        }

        private static bool IsPendingExecution(
            in HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainResult result)
        {
            return !result.PlanValidationPassed
                   && result.PlannedStepCount == 0
                   && result.ExecutedStepCount == 0
                   && string.IsNullOrEmpty(result.FailureReason);
        }

        private static string ResolveFailureReason(
            in HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainPlanValidation planValidation,
            in HeadlessAutoChessGeneratedDefinitionSourceGeneratorToolchainResult result,
            in HeadlessAutoChessGeneratedDefinitionSourceGeneratorExportResult exportResult)
        {
            if (!planValidation.Passed)
                return planValidation.FailureReason;

            if (!string.IsNullOrEmpty(result.FailureReason))
                return result.FailureReason;

            if (!string.IsNullOrEmpty(exportResult.FailureReason))
                return exportResult.FailureReason;

            return string.Empty;
        }
    }

    public readonly struct HeadlessAutoChessAbilityDefinitionRow
    {
        public readonly int AbilityCode;
        public readonly int Level;
        public readonly int TimelineId;
        public readonly int ActivationOwnedTagCode;
        public readonly int CostGameplayEffectCode;
        public readonly int CooldownGameplayEffectCode;
        public readonly int CooldownFrames;

        public HeadlessAutoChessAbilityDefinitionRow(
            int abilityCode,
            int level,
            int timelineId,
            int activationOwnedTagCode,
            int costGameplayEffectCode,
            int cooldownGameplayEffectCode,
            int cooldownFrames)
        {
            AbilityCode = abilityCode;
            Level = level;
            TimelineId = timelineId;
            ActivationOwnedTagCode = activationOwnedTagCode;
            CostGameplayEffectCode = costGameplayEffectCode;
            CooldownGameplayEffectCode = cooldownGameplayEffectCode;
            CooldownFrames = cooldownFrames;
        }

        public bool HasTimeline => TimelineId > 0;
        public bool HasActivationOwnedTag => ActivationOwnedTagCode > 0;
        public bool HasCost => CostGameplayEffectCode > 0;
        public bool HasCooldown => CooldownGameplayEffectCode > 0 || CooldownFrames > 0;
    }

    public readonly struct HeadlessAutoChessGameplayEffectDefinitionRow
    {
        public readonly int GameplayEffectCode;
        public readonly string Name;
        public readonly int ModifierAttributeSetCode;
        public readonly int ModifierAttributeCode;
        public readonly EModifierOp ModifierOperation;
        public readonly float ModifierMagnitude;
        public readonly EMagnitudeSource ModifierMagnitudeSource;
        public readonly int ModifierMagnitudeKey;
        public readonly int DurationFrames;
        public readonly int PeriodFrames;
        public readonly int PeriodGameplayEffectCode;
        public readonly int GrantedTagCode;
        public readonly int GameplayCueCode;
        public readonly int DamageTypeCode;
        public readonly int ResistanceAttributeSetCode;
        public readonly int ResistanceAttributeCode;
        public readonly float ResistanceCap;
        public readonly int RemoveGameplayEffectTagCode;
        public readonly int StackingCode;
        public readonly int StackLimitCount;
        public readonly EffectStackType StackType;
        public readonly EffectDurationRefreshPolicy EffectDurationRefreshPolicy;
        public readonly EffectPeriodResetPolicy EffectPeriodResetPolicy;
        public readonly EffectExpirationPolicy EffectExpirationPolicy;
        public readonly bool DenyOverflowApplication;
        public readonly bool ClearStackOnOverflow;
        public readonly int OverflowGameplayEffectCode;

        public HeadlessAutoChessGameplayEffectDefinitionRow(
            int gameplayEffectCode,
            string name,
            int modifierAttributeSetCode,
            int modifierAttributeCode,
            EModifierOp modifierOperation,
            float modifierMagnitude,
            int durationFrames,
            int periodFrames,
            int periodGameplayEffectCode,
            int grantedTagCode,
            int gameplayCueCode,
            int damageTypeCode = 0,
            int resistanceAttributeSetCode = 0,
            int resistanceAttributeCode = 0,
            float resistanceCap = 0f,
            int removeGameplayEffectTagCode = 0,
            EMagnitudeSource modifierMagnitudeSource = EMagnitudeSource.Constant,
            int modifierMagnitudeKey = 0,
            int stackingCode = 0,
            int stackLimitCount = 0,
            EffectStackType stackType = EffectStackType.AggregateByTarget,
            EffectDurationRefreshPolicy effectDurationRefreshPolicy =
                EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication,
            EffectPeriodResetPolicy effectPeriodResetPolicy = EffectPeriodResetPolicy.ResetOnSuccessfulApplication,
            EffectExpirationPolicy effectExpirationPolicy = EffectExpirationPolicy.ClearEntireStack,
            bool denyOverflowApplication = false,
            bool clearStackOnOverflow = false,
            int overflowGameplayEffectCode = 0)
        {
            GameplayEffectCode = gameplayEffectCode;
            Name = name ?? string.Empty;
            ModifierAttributeSetCode = modifierAttributeSetCode;
            ModifierAttributeCode = modifierAttributeCode;
            ModifierOperation = modifierOperation;
            ModifierMagnitude = modifierMagnitude;
            ModifierMagnitudeSource = modifierMagnitudeSource;
            ModifierMagnitudeKey = modifierMagnitudeKey;
            DurationFrames = durationFrames;
            PeriodFrames = periodFrames;
            PeriodGameplayEffectCode = periodGameplayEffectCode;
            GrantedTagCode = grantedTagCode;
            GameplayCueCode = gameplayCueCode;
            DamageTypeCode = damageTypeCode;
            ResistanceAttributeSetCode = resistanceAttributeSetCode;
            ResistanceAttributeCode = resistanceAttributeCode;
            ResistanceCap = resistanceCap;
            RemoveGameplayEffectTagCode = removeGameplayEffectTagCode;
            StackingCode = stackingCode;
            StackLimitCount = stackLimitCount;
            StackType = stackType;
            EffectDurationRefreshPolicy = effectDurationRefreshPolicy;
            EffectPeriodResetPolicy = effectPeriodResetPolicy;
            EffectExpirationPolicy = effectExpirationPolicy;
            DenyOverflowApplication = denyOverflowApplication;
            ClearStackOnOverflow = clearStackOnOverflow;
            OverflowGameplayEffectCode = overflowGameplayEffectCode;
        }

        public bool HasModifier => ModifierAttributeSetCode > 0 && ModifierAttributeCode > 0;
        public bool HasSetByCallerMagnitude =>
            HasModifier
            && ModifierMagnitudeSource == EMagnitudeSource.SetByCaller
            && ModifierMagnitudeKey > 0;
        public bool HasDuration => DurationFrames > 0;
        public bool HasPeriod => PeriodFrames > 0;
        public bool HasGrantedTag => GrantedTagCode > 0;
        public bool HasGameplayCue => GameplayCueCode > 0;
        public bool HasDamageType => DamageTypeCode > 0;
        public bool HasResistanceCapture => ResistanceAttributeSetCode > 0 && ResistanceAttributeCode > 0;
        public bool HasRemoveGameplayEffectsWithTags => RemoveGameplayEffectTagCode > 0;
        public bool HasStacking => StackingCode > 0 && StackLimitCount > 0;
        public bool HasOverflowGameplayEffect => HasStacking && OverflowGameplayEffectCode > 0;
    }

    public readonly struct HeadlessAutoChessAttributeSetDefinitionRow
    {
        public readonly int AttributeSetCode;

        public HeadlessAutoChessAttributeSetDefinitionRow(int attributeSetCode)
        {
            AttributeSetCode = attributeSetCode;
        }
    }

    public readonly struct HeadlessAutoChessAttributeDefinitionRow
    {
        public readonly int AttributeSetCode;
        public readonly int AttributeCode;
        public readonly float InitialValue;
        public readonly bool IsClampMin;
        public readonly bool IsClampMax;
        public readonly float MinValue;
        public readonly float MaxValue;

        public HeadlessAutoChessAttributeDefinitionRow(
            int attributeSetCode,
            int attributeCode,
            float initialValue,
            bool isClampMin,
            bool isClampMax,
            float minValue,
            float maxValue)
        {
            AttributeSetCode = attributeSetCode;
            AttributeCode = attributeCode;
            InitialValue = initialValue;
            IsClampMin = isClampMin;
            IsClampMax = isClampMax;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public AttributeBaseSetting ToAttributeSetting()
        {
            return new AttributeBaseSetting(
                AttributeCode,
                InitialValue,
                IsClampMin,
                IsClampMax,
                MinValue,
                MaxValue);
        }
    }

    public readonly struct HeadlessAutoChessGameplayTagDefinitionRow
    {
        private readonly int[] _parentCodes;
        private readonly int[] _childCodes;

        public readonly int GameplayTagCode;

        public HeadlessAutoChessGameplayTagDefinitionRow(
            int gameplayTagCode,
            IEnumerable<int> parentCodes,
            IEnumerable<int> childCodes)
        {
            GameplayTagCode = gameplayTagCode;
            _parentCodes = Materialize(parentCodes);
            _childCodes = Materialize(childCodes);
        }

        public IReadOnlyList<int> ParentCodes => _parentCodes ?? Array.Empty<int>();
        public IReadOnlyList<int> ChildCodes => _childCodes ?? Array.Empty<int>();

        public GameplayTag ToGameplayTag()
        {
            return new GameplayTag(GameplayTagCode, CopyCodes(ParentCodes), CopyCodes(ChildCodes));
        }

        private static int[] CopyCodes(IReadOnlyList<int> codes)
        {
            var result = new int[codes.Count];
            for (var i = 0; i < codes.Count; i++)
                result[i] = codes[i];
            return result;
        }

        private static int[] Materialize(IEnumerable<int> values)
        {
            if (values == null)
                return Array.Empty<int>();

            return values is int[] array ? (int[])array.Clone() : new List<int>(values).ToArray();
        }
    }

    public readonly struct HeadlessAutoChessGameplayCueDefinitionRow
    {
        public readonly int GameplayCueCode;
        public readonly string PresentationKey;

        public HeadlessAutoChessGameplayCueDefinitionRow(int gameplayCueCode, string presentationKey)
        {
            GameplayCueCode = gameplayCueCode;
            PresentationKey = presentationKey ?? string.Empty;
        }
    }

    public readonly struct HeadlessAutoChessTimelineDefinitionRow
    {
        public readonly int TimelineId;
        public readonly string Name;
        public readonly int GameplayEffectCode;
        public readonly int SecondaryGameplayEffectCode;
        public readonly string TargetCatcherName;

        public HeadlessAutoChessTimelineDefinitionRow(
            int timelineId,
            string name,
            int gameplayEffectCode,
            int secondaryGameplayEffectCode,
            string targetCatcherName)
        {
            TimelineId = timelineId;
            Name = name ?? string.Empty;
            GameplayEffectCode = gameplayEffectCode;
            SecondaryGameplayEffectCode = secondaryGameplayEffectCode;
            TargetCatcherName = targetCatcherName ?? string.Empty;
        }

        public int[] CreateGameplayEffectCodes()
        {
            return SecondaryGameplayEffectCode > 0
                ? new[] { GameplayEffectCode, SecondaryGameplayEffectCode }
                : new[] { GameplayEffectCode };
        }
    }

    public readonly struct HeadlessAutoChessSummonDefinitionRow
    {
        public readonly int SummonGameplayEffectCode;
        public readonly int SummonedUnitCode;
        public readonly int FixedTagCode;
        public readonly int PrimaryAbilityCode;
        public readonly int LifetimeTurns;
        public readonly int SlotOffset;
        public readonly int BoardXOffset;
        public readonly int BoardYOffset;
        public readonly int TurnOrderOffset;
        public readonly float Health;
        public readonly float Mana;
        public readonly float Shield;
        public readonly float MaxHealth;
        public readonly float MaxMana;
        public readonly float MaxShield;
        public readonly HeadlessAutoChessTargetPolicy PrimaryTargetPolicy;

        public HeadlessAutoChessSummonDefinitionRow(
            int summonGameplayEffectCode,
            int summonedUnitCode,
            int fixedTagCode,
            int primaryAbilityCode,
            int lifetimeTurns,
            int slotOffset,
            int boardXOffset,
            int boardYOffset,
            int turnOrderOffset,
            float health,
            float mana,
            float shield,
            float maxHealth,
            float maxMana,
            float maxShield,
            HeadlessAutoChessTargetPolicy primaryTargetPolicy)
        {
            SummonGameplayEffectCode = summonGameplayEffectCode;
            SummonedUnitCode = summonedUnitCode;
            FixedTagCode = fixedTagCode;
            PrimaryAbilityCode = primaryAbilityCode;
            LifetimeTurns = lifetimeTurns;
            SlotOffset = slotOffset;
            BoardXOffset = boardXOffset;
            BoardYOffset = boardYOffset;
            TurnOrderOffset = turnOrderOffset;
            Health = health;
            Mana = mana;
            Shield = shield;
            MaxHealth = maxHealth;
            MaxMana = maxMana;
            MaxShield = maxShield;
            PrimaryTargetPolicy = primaryTargetPolicy;
        }
    }

    public static class HeadlessAutoChessGeneratedDefinitionRows
    {
        public const string GeneratedPackageName = "HeadlessAutoChess";
        public const string GeneratedSchemaVersion = "HeadlessAutoChess.GeneratedDefinition.v1";
        public const string GeneratedSourceGeneratorName = "EX-GAS.LubanSourceGenerator";
        public const string GeneratedToolchainName = "EX-GAS.LubanSourceGenerator.Toolchain";
        public const string GeneratedSourceRevision = "autochess-fixture-v1";
        public const string GeneratedDotNetExecutablePath = "dotnet";
        public const string GeneratedLubanDllRelativePath =
            "EX_GAS_Config/ProjectConfigTable/Tools/Luban/Luban.dll";
        public const string GeneratedConfigSourceRelativePath =
            "EX_GAS_Config/ProjectConfigTable/exgas_config";
        public const string GeneratedOutputRoot = "Assets/DataGenerated/Luban/HeadlessAutoChess";
        public const string GeneratedRuntimeSourceRelativePath =
            GeneratedOutputRoot + "/HeadlessAutoChessGeneratedDefinitionPackage.g.cs";
        public const string GeneratedManifestRelativePath =
            GeneratedOutputRoot + "/HeadlessAutoChessGeneratedDefinitionPackage.manifest.json";
        public const string GeneratedRuntimeNamespace = "GAS.Runtime.Generated.AutoChess";
        public const string GeneratedRuntimeTypeName = "HeadlessAutoChessGeneratedDefinitionPackageOutput";
        public const string NoopCuePresentationKey = "HeadlessAutoChess.NoopCue";

        public static HeadlessAutoChessGeneratedDefinitionPackage CreateLubanSourceGeneratorPackage()
        {
            return HeadlessAutoChessGeneratedDefinitionPackage.Create(
                HeadlessAutoChessGeneratedDefinitionSourceKind.LubanSourceGenerator,
                GeneratedPackageName,
                GeneratedSchemaVersion,
                GeneratedSourceGeneratorName,
                GeneratedSourceRevision,
                CreateDefaultSnapshot());
        }

        public static HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput CreateLubanSourceGeneratorOutput()
        {
            return HeadlessAutoChessGeneratedDefinitionSourceGeneratorOutput.Create(
                CreateLubanSourceGeneratorPackage(),
                GeneratedRuntimeSourceRelativePath,
                GeneratedManifestRelativePath,
                GeneratedRuntimeNamespace,
                GeneratedRuntimeTypeName);
        }

        public static HeadlessAutoChessGeneratedRegistrySnapshot CreateDefaultSnapshot()
        {
            return new HeadlessAutoChessGeneratedRegistrySnapshot(
                CreateAbilityRows(),
                CreateGameplayEffectRows(),
                CreateAttributeSetRows(),
                CreateAttributeRows(
                    health: 60f,
                    mana: 0f,
                    maxHealth: 100f,
                    maxMana: 10f,
                    counterDamage: 0f,
                    maxCounterDamage: HeadlessAutoChessScenario.MaxCounterDamageAmount,
                    lifeStealRatio: 0f,
                    maxLifeStealRatio: HeadlessAutoChessScenario.MaxLifeStealRatio),
                CreateGameplayTagRows(),
                CreateGameplayCueRows(),
                CreateTimelineRows(),
                CreateSummonRows());
        }

        public static HeadlessAutoChessAbilityDefinitionRow[] CreateAbilityRows()
        {
            return new[]
            {
                new HeadlessAutoChessAbilityDefinitionRow(
                    HeadlessAutoChessScenario.AbilityPlayerStrike,
                    1,
                    HeadlessAutoChessScenario.TimelinePlayerStrike,
                    HeadlessAutoChessScenario.TagAbilityActing,
                    0,
                    0,
                    0),
                new HeadlessAutoChessAbilityDefinitionRow(
                    HeadlessAutoChessScenario.AbilityEnemyStrike,
                    1,
                    HeadlessAutoChessScenario.TimelineEnemyStrike,
                    HeadlessAutoChessScenario.TagAbilityActing,
                    0,
                    0,
                    0),
                new HeadlessAutoChessAbilityDefinitionRow(
                    HeadlessAutoChessScenario.AbilityPlayerManaBurst,
                    1,
                    HeadlessAutoChessScenario.TimelinePlayerManaBurst,
                    HeadlessAutoChessScenario.TagAbilityActing,
                    HeadlessAutoChessScenario.GameplayEffectManaBurstCost,
                    HeadlessAutoChessScenario.GameplayEffectManaBurstCooldown,
                    2),
                new HeadlessAutoChessAbilityDefinitionRow(
                    HeadlessAutoChessScenario.AbilityPlayerControlStun,
                    1,
                    HeadlessAutoChessScenario.TimelinePlayerControlStun,
                    HeadlessAutoChessScenario.TagAbilityActing,
                    0,
                    HeadlessAutoChessScenario.GameplayEffectPlayerStunCooldown,
                    HeadlessAutoChessScenario.PlayerStunCooldownFrames),
                new HeadlessAutoChessAbilityDefinitionRow(
                    HeadlessAutoChessScenario.AbilityPlayerBarrier,
                    1,
                    HeadlessAutoChessScenario.TimelinePlayerBarrier,
                    HeadlessAutoChessScenario.TagAbilityActing,
                    0,
                    HeadlessAutoChessScenario.GameplayEffectPlayerBarrierCooldown,
                    HeadlessAutoChessScenario.PlayerBarrierCooldownFrames),
                new HeadlessAutoChessAbilityDefinitionRow(
                    HeadlessAutoChessScenario.AbilityPlayerSummon,
                    1,
                    HeadlessAutoChessScenario.TimelinePlayerSummon,
                    HeadlessAutoChessScenario.TagAbilityActing,
                    0,
                    HeadlessAutoChessScenario.GameplayEffectPlayerSummonCooldown,
                    HeadlessAutoChessScenario.PlayerSummonCooldownFrames),
                new HeadlessAutoChessAbilityDefinitionRow(
                    HeadlessAutoChessScenario.AbilityPlayerCleanse,
                    1,
                    HeadlessAutoChessScenario.TimelinePlayerCleanse,
                    HeadlessAutoChessScenario.TagAbilityActing,
                    0,
                    HeadlessAutoChessScenario.GameplayEffectPlayerCleanseCooldown,
                    HeadlessAutoChessScenario.PlayerCleanseCooldownFrames),
            };
        }

        public static HeadlessAutoChessGameplayEffectDefinitionRow[] CreateGameplayEffectRows()
        {
            return new[]
            {
                CreateModifierEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerStrikeDamage,
                    "PlayerStrikeDamage",
                    HeadlessAutoChessScenario.AttributeHealth,
                    EModifierOp.Subtract,
                    9f,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    HeadlessAutoChessScenario.DamageTypePhysical),
                CreateModifierEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectEnemyStrikeDamage,
                    "EnemyStrikeDamage",
                    HeadlessAutoChessScenario.AttributeHealth,
                    EModifierOp.Subtract,
                    5f,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    HeadlessAutoChessScenario.DamageTypePhysical),
                CreateModifierEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerManaBurstDamage,
                    "PlayerManaBurstDamage",
                    HeadlessAutoChessScenario.AttributeHealth,
                    EModifierOp.Subtract,
                    24f,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    HeadlessAutoChessScenario.DamageTypeArcane,
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeArcaneResistance,
                    HeadlessAutoChessScenario.MaxArcaneResistance),
                CreateModifierEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectManaBurstCost,
                    "ManaBurstCost",
                    HeadlessAutoChessScenario.AttributeMana,
                    EModifierOp.Subtract,
                    3f,
                    0),
                CreateDurationEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectManaBurstCooldown,
                    "ManaBurstCooldown",
                    2,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagManaBurstCooldown,
                    0),
                CreateModifierEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectKillManaGain,
                    "KillManaGain",
                    HeadlessAutoChessScenario.AttributeMana,
                    EModifierOp.Add,
                    HeadlessAutoChessScenario.KillManaGainAmount,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                CreateModifierEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectSelfRevive,
                    "SelfRevive",
                    HeadlessAutoChessScenario.AttributeHealth,
                    EModifierOp.Add,
                    HeadlessAutoChessScenario.ReviveHealthAmount,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                new HeadlessAutoChessGameplayEffectDefinitionRow(
                    HeadlessAutoChessScenario.GameplayEffectArcaneTeamBuff,
                    "ArcaneTeamBuff",
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeMana,
                    EModifierOp.Add,
                    HeadlessAutoChessScenario.ArcaneTeamBuffManaAmount,
                    10,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagArcaneTeamBuff,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                CreateDurationEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectArcaneStormDebuff,
                    "ArcaneStormDebuff",
                    10,
                    2,
                    HeadlessAutoChessScenario.GameplayEffectArcaneStormPeriodDamage,
                    HeadlessAutoChessScenario.TagArcaneStormDebuff,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                CreateModifierEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectArcaneStormPeriodDamage,
                    "ArcaneStormPeriodDamage",
                    HeadlessAutoChessScenario.AttributeHealth,
                    EModifierOp.Subtract,
                    HeadlessAutoChessScenario.ArcaneStormTickDamage,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    HeadlessAutoChessScenario.DamageTypeArcane,
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeArcaneResistance,
                    HeadlessAutoChessScenario.MaxArcaneResistance),
                CreateDurationEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerStun,
                    "PlayerStun",
                    HeadlessAutoChessScenario.PlayerStunDurationFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagAutoChessStunned,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                CreateDurationEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerStunCooldown,
                    "PlayerStunCooldown",
                    HeadlessAutoChessScenario.PlayerStunCooldownFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagPlayerStunCooldown,
                    0),
                CreateModifierEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerBarrierShield,
                    "PlayerBarrierShield",
                    HeadlessAutoChessScenario.AttributeShield,
                    EModifierOp.Add,
                    HeadlessAutoChessScenario.PlayerBarrierShieldAmount,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                CreateDurationEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerBarrierStatus,
                    "PlayerBarrierStatus",
                    HeadlessAutoChessScenario.PlayerBarrierStatusDurationFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagAutoChessShielded,
                    0),
                CreateDurationEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerBarrierCooldown,
                    "PlayerBarrierCooldown",
                    HeadlessAutoChessScenario.PlayerBarrierCooldownFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagPlayerBarrierCooldown,
                    0),
                CreateDurationEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerSummonRequest,
                    "PlayerSummonRequest",
                    1,
                    0,
                    0,
                    0,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                CreateDurationEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerSummonCooldown,
                    "PlayerSummonCooldown",
                    HeadlessAutoChessScenario.PlayerSummonCooldownFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagPlayerSummonCooldown,
                    0),
                new HeadlessAutoChessGameplayEffectDefinitionRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerCounterGear,
                    "PlayerCounterGear",
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeCounterDamage,
                    EModifierOp.Add,
                    HeadlessAutoChessScenario.PlayerCounterDamageAmount,
                    HeadlessAutoChessScenario.PlayerCounterGearDurationFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagAutoChessCounterReady,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                new HeadlessAutoChessGameplayEffectDefinitionRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerCounterDamage,
                    "PlayerCounterDamage",
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeHealth,
                    EModifierOp.Subtract,
                    HeadlessAutoChessScenario.PlayerCounterDamageAmount,
                    0,
                    0,
                    0,
                    0,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    HeadlessAutoChessScenario.DamageTypePhysical,
                    modifierMagnitudeSource: EMagnitudeSource.SetByCaller,
                    modifierMagnitudeKey: HeadlessAutoChessScenario.SetByCallerCounterDamageAmount),
                new HeadlessAutoChessGameplayEffectDefinitionRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerCleanse,
                    "PlayerCleanse",
                    0,
                    0,
                    EModifierOp.Add,
                    0f,
                    0,
                    0,
                    0,
                    0,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    removeGameplayEffectTagCode: HeadlessAutoChessScenario.TagAutoChessStunned),
                new HeadlessAutoChessGameplayEffectDefinitionRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerCleanseRally,
                    "PlayerCleanseRally",
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeMana,
                    EModifierOp.Add,
                    HeadlessAutoChessScenario.PlayerCleanseRallyManaAmount,
                    HeadlessAutoChessScenario.PlayerCleanseRallyDurationFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagAutoChessCleanseRallied,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                CreateModifierEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerRallyComboDamage,
                    "PlayerRallyComboDamage",
                    HeadlessAutoChessScenario.AttributeHealth,
                    EModifierOp.Subtract,
                    HeadlessAutoChessScenario.PlayerRallyComboDamageAmount,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    HeadlessAutoChessScenario.DamageTypePhysical),
                new HeadlessAutoChessGameplayEffectDefinitionRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerLifeStealGear,
                    "PlayerLifeStealGear",
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeLifeStealRatio,
                    EModifierOp.Add,
                    HeadlessAutoChessScenario.PlayerLifeStealRatio,
                    HeadlessAutoChessScenario.PlayerLifeStealGearDurationFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagAutoChessLifeStealReady,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                new HeadlessAutoChessGameplayEffectDefinitionRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerLifeStealHeal,
                    "PlayerLifeStealHeal",
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeHealth,
                    EModifierOp.Add,
                    0f,
                    0,
                    0,
                    0,
                    0,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    modifierMagnitudeSource: EMagnitudeSource.SetByCaller,
                    modifierMagnitudeKey: HeadlessAutoChessScenario.SetByCallerLifeStealHealAmount),
                new HeadlessAutoChessGameplayEffectDefinitionRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerPoisonStack,
                    "PlayerPoisonStack",
                    0,
                    0,
                    EModifierOp.Add,
                    0f,
                    HeadlessAutoChessScenario.PlayerPoisonStackDurationFrames,
                    HeadlessAutoChessScenario.PlayerPoisonPeriodFrames,
                    HeadlessAutoChessScenario.GameplayEffectPlayerPoisonPeriodDamage,
                    HeadlessAutoChessScenario.TagAutoChessPoisoned,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    stackingCode: HeadlessAutoChessScenario.PoisonStackingCode,
                    stackLimitCount: HeadlessAutoChessScenario.PlayerPoisonStackLimit,
                    stackType: EffectStackType.AggregateByTarget,
                    effectDurationRefreshPolicy: EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication,
                    effectPeriodResetPolicy: EffectPeriodResetPolicy.ResetOnSuccessfulApplication,
                    effectExpirationPolicy: EffectExpirationPolicy.ClearEntireStack,
                    overflowGameplayEffectCode: HeadlessAutoChessScenario.GameplayEffectPlayerPoisonOverflowDamage),
                CreateModifierEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerPoisonOverflowDamage,
                    "PlayerPoisonOverflowDamage",
                    HeadlessAutoChessScenario.AttributeHealth,
                    EModifierOp.Subtract,
                    HeadlessAutoChessScenario.PlayerPoisonOverflowDamageAmount,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    HeadlessAutoChessScenario.DamageTypePoison),
                CreateModifierEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerPoisonPeriodDamage,
                    "PlayerPoisonPeriodDamage",
                    HeadlessAutoChessScenario.AttributeHealth,
                    EModifierOp.Subtract,
                    HeadlessAutoChessScenario.PlayerPoisonPeriodDamageAmount,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    HeadlessAutoChessScenario.DamageTypePoison),
                CreateDurationEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerExecuteGear,
                    "PlayerExecuteGear",
                    HeadlessAutoChessScenario.PlayerExecuteGearDurationFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagAutoChessExecutionReady,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                new HeadlessAutoChessGameplayEffectDefinitionRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerExecuteDamage,
                    "PlayerExecuteDamage",
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeHealth,
                    EModifierOp.Subtract,
                    HeadlessAutoChessScenario.PlayerExecuteDamageAmount,
                    HeadlessAutoChessScenario.PlayerExecutedMarkDurationFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagAutoChessExecuted,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    damageTypeCode: HeadlessAutoChessScenario.DamageTypeExecute),
                CreateDurationEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerDeathBurstGear,
                    "PlayerDeathBurstGear",
                    HeadlessAutoChessScenario.PlayerDeathBurstGearDurationFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagAutoChessDeathBurstReady,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                CreateModifierEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerDeathBurstDamage,
                    "PlayerDeathBurstDamage",
                    HeadlessAutoChessScenario.AttributeHealth,
                    EModifierOp.Subtract,
                    HeadlessAutoChessScenario.PlayerDeathBurstDamageAmount,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    HeadlessAutoChessScenario.DamageTypeDeathBurst),
                new HeadlessAutoChessGameplayEffectDefinitionRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerEnrage,
                    "PlayerEnrage",
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeCounterDamage,
                    EModifierOp.Add,
                    HeadlessAutoChessScenario.PlayerEnrageCounterDamageBonus,
                    HeadlessAutoChessScenario.PlayerEnrageDurationFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagAutoChessEnraged,
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply),
                CreateDurationEffectRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerCleanseCooldown,
                    "PlayerCleanseCooldown",
                    HeadlessAutoChessScenario.PlayerCleanseCooldownFrames,
                    0,
                    0,
                    HeadlessAutoChessScenario.TagPlayerCleanseCooldown,
                    0),
            };
        }

        public static HeadlessAutoChessAttributeSetDefinitionRow[] CreateAttributeSetRows()
        {
            return new[]
            {
                new HeadlessAutoChessAttributeSetDefinitionRow(HeadlessAutoChessScenario.AttributeSetCombat),
            };
        }

        public static HeadlessAutoChessAttributeDefinitionRow[] CreateAttributeRows(
            float health,
            float mana,
            float maxHealth,
            float maxMana,
            float shield = 0f,
            float maxShield = HeadlessAutoChessScenario.MaxShieldAmount,
            float arcaneResistance = 0f,
            float maxArcaneResistance = HeadlessAutoChessScenario.MaxArcaneResistance,
            float counterDamage = 0f,
            float maxCounterDamage = HeadlessAutoChessScenario.MaxCounterDamageAmount,
            float lifeStealRatio = 0f,
            float maxLifeStealRatio = HeadlessAutoChessScenario.MaxLifeStealRatio)
        {
            return new[]
            {
                new HeadlessAutoChessAttributeDefinitionRow(
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeHealth,
                    health,
                    true,
                    true,
                    0f,
                    maxHealth),
                new HeadlessAutoChessAttributeDefinitionRow(
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeMana,
                    mana,
                    true,
                    true,
                    0f,
                    maxMana),
                new HeadlessAutoChessAttributeDefinitionRow(
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeShield,
                    shield,
                    true,
                    true,
                    0f,
                    maxShield),
                new HeadlessAutoChessAttributeDefinitionRow(
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeArcaneResistance,
                    arcaneResistance,
                    true,
                    true,
                    0f,
                    maxArcaneResistance),
                new HeadlessAutoChessAttributeDefinitionRow(
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeCounterDamage,
                    counterDamage,
                    true,
                    true,
                    0f,
                    maxCounterDamage),
                new HeadlessAutoChessAttributeDefinitionRow(
                    HeadlessAutoChessScenario.AttributeSetCombat,
                    HeadlessAutoChessScenario.AttributeLifeStealRatio,
                    lifeStealRatio,
                    true,
                    true,
                    0f,
                    maxLifeStealRatio),
            };
        }

        public static HeadlessAutoChessGameplayTagDefinitionRow[] CreateGameplayTagRows()
        {
            return new[]
            {
                CreateRootTagRow(HeadlessAutoChessScenario.TagAbilityActing),
                CreateRootTagRow(HeadlessAutoChessScenario.TagManaBurstCooldown),
                CreateRootTagRow(HeadlessAutoChessScenario.TagArcaneTeamBuff),
                CreateRootTagRow(HeadlessAutoChessScenario.TagArcaneStormDebuff),
                CreateRootTagRow(HeadlessAutoChessScenario.TagAutoChessStunned),
                CreateRootTagRow(HeadlessAutoChessScenario.TagPlayerStunCooldown),
                CreateRootTagRow(HeadlessAutoChessScenario.TagAutoChessShielded),
                CreateRootTagRow(HeadlessAutoChessScenario.TagPlayerBarrierCooldown),
                CreateRootTagRow(HeadlessAutoChessScenario.TagAutoChessSummoned),
                CreateRootTagRow(HeadlessAutoChessScenario.TagPlayerSummonCooldown),
                CreateRootTagRow(HeadlessAutoChessScenario.TagAutoChessCounterReady),
                CreateRootTagRow(HeadlessAutoChessScenario.TagPlayerCleanseCooldown),
                CreateRootTagRow(HeadlessAutoChessScenario.TagAutoChessCleanseRallied),
                CreateRootTagRow(HeadlessAutoChessScenario.TagAutoChessLifeStealReady),
                CreateRootTagRow(HeadlessAutoChessScenario.TagAutoChessPoisoned),
                CreateRootTagRow(HeadlessAutoChessScenario.TagAutoChessExecutionReady),
                CreateRootTagRow(HeadlessAutoChessScenario.TagAutoChessExecuted),
                CreateRootTagRow(HeadlessAutoChessScenario.TagAutoChessDeathBurstReady),
                CreateRootTagRow(HeadlessAutoChessScenario.TagAutoChessEnraged),
            };
        }

        public static HeadlessAutoChessGameplayCueDefinitionRow[] CreateGameplayCueRows()
        {
            return new[]
            {
                new HeadlessAutoChessGameplayCueDefinitionRow(
                    HeadlessAutoChessDefinitionSource.GameplayCueNoopOnApply,
                    NoopCuePresentationKey),
            };
        }

        public static HeadlessAutoChessTimelineDefinitionRow[] CreateTimelineRows()
        {
            return new[]
            {
                new HeadlessAutoChessTimelineDefinitionRow(
                    HeadlessAutoChessScenario.TimelinePlayerStrike,
                    "PlayerStrike",
                    HeadlessAutoChessScenario.GameplayEffectPlayerStrikeDamage,
                    0,
                    HeadlessAutoChessDefinitionSource.TargetCatcherName),
                new HeadlessAutoChessTimelineDefinitionRow(
                    HeadlessAutoChessScenario.TimelineEnemyStrike,
                    "EnemyStrike",
                    HeadlessAutoChessScenario.GameplayEffectEnemyStrikeDamage,
                    0,
                    HeadlessAutoChessDefinitionSource.TargetCatcherName),
                new HeadlessAutoChessTimelineDefinitionRow(
                    HeadlessAutoChessScenario.TimelinePlayerManaBurst,
                    "PlayerManaBurst",
                    HeadlessAutoChessScenario.GameplayEffectPlayerManaBurstDamage,
                    0,
                    HeadlessAutoChessDefinitionSource.TargetCatcherName),
                new HeadlessAutoChessTimelineDefinitionRow(
                    HeadlessAutoChessScenario.TimelinePlayerControlStun,
                    "PlayerControlStun",
                    HeadlessAutoChessScenario.GameplayEffectPlayerStun,
                    0,
                    HeadlessAutoChessDefinitionSource.TargetCatcherName),
                new HeadlessAutoChessTimelineDefinitionRow(
                    HeadlessAutoChessScenario.TimelinePlayerBarrier,
                    "PlayerBarrier",
                    HeadlessAutoChessScenario.GameplayEffectPlayerBarrierShield,
                    HeadlessAutoChessScenario.GameplayEffectPlayerBarrierStatus,
                    HeadlessAutoChessDefinitionSource.TargetCatcherName),
                new HeadlessAutoChessTimelineDefinitionRow(
                    HeadlessAutoChessScenario.TimelinePlayerSummon,
                    "PlayerSummon",
                    HeadlessAutoChessScenario.GameplayEffectPlayerSummonRequest,
                    0,
                    HeadlessAutoChessDefinitionSource.TargetCatcherName),
                new HeadlessAutoChessTimelineDefinitionRow(
                    HeadlessAutoChessScenario.TimelinePlayerCleanse,
                    "PlayerCleanse",
                    HeadlessAutoChessScenario.GameplayEffectPlayerCleanse,
                    0,
                    HeadlessAutoChessDefinitionSource.TargetCatcherName),
            };
        }

        public static HeadlessAutoChessSummonDefinitionRow[] CreateSummonRows()
        {
            return new[]
            {
                new HeadlessAutoChessSummonDefinitionRow(
                    HeadlessAutoChessScenario.GameplayEffectPlayerSummonRequest,
                    HeadlessAutoChessScenario.SummonedUnitArcaneWisp,
                    HeadlessAutoChessScenario.TagAutoChessSummoned,
                    HeadlessAutoChessScenario.AbilityPlayerStrike,
                    HeadlessAutoChessScenario.PlayerSummonLifetimeTurns,
                    HeadlessAutoChessScenario.SummonedUnitSlotOffset,
                    1,
                    0,
                    HeadlessAutoChessScenario.SummonedUnitTurnOrderOffset,
                    HeadlessAutoChessScenario.PlayerSummonHealth,
                    0f,
                    0f,
                    HeadlessAutoChessScenario.PlayerSummonHealth,
                    10f,
                    HeadlessAutoChessScenario.MaxShieldAmount,
                    HeadlessAutoChessTargetPolicy.LowestHealth),
            };
        }

        private static HeadlessAutoChessGameplayEffectDefinitionRow CreateModifierEffectRow(
            int gameplayEffectCode,
            string name,
            int attributeCode,
            EModifierOp operation,
            float magnitude,
            int gameplayCueCode,
            int damageTypeCode = 0,
            int resistanceAttributeSetCode = 0,
            int resistanceAttributeCode = 0,
            float resistanceCap = 0f)
        {
            return new HeadlessAutoChessGameplayEffectDefinitionRow(
                gameplayEffectCode,
                name,
                HeadlessAutoChessScenario.AttributeSetCombat,
                attributeCode,
                operation,
                magnitude,
                0,
                0,
                0,
                0,
                gameplayCueCode,
                damageTypeCode,
                resistanceAttributeSetCode,
                resistanceAttributeCode,
                resistanceCap);
        }

        private static HeadlessAutoChessGameplayEffectDefinitionRow CreateDurationEffectRow(
            int gameplayEffectCode,
            string name,
            int durationFrames,
            int periodFrames,
            int periodGameplayEffectCode,
            int grantedTagCode,
            int gameplayCueCode)
        {
            return new HeadlessAutoChessGameplayEffectDefinitionRow(
                gameplayEffectCode,
                name,
                0,
                0,
                EModifierOp.Add,
                0f,
                durationFrames,
                periodFrames,
                periodGameplayEffectCode,
                grantedTagCode,
                gameplayCueCode);
        }

        private static HeadlessAutoChessGameplayTagDefinitionRow CreateRootTagRow(int gameplayTagCode)
        {
            return new HeadlessAutoChessGameplayTagDefinitionRow(
                gameplayTagCode,
                Array.Empty<int>(),
                Array.Empty<int>());
        }
    }
}
