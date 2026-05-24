using System;
using System.Collections.Generic;
using Unity.Entities;

namespace GAS.Runtime
{
    public enum ConfigRegistryConfigKind
    {
        None = 0,
        GameplayEffect = 1,
        Ability = 2,
        GameplayCue = 3,
        TimelineAbility = 4,
        AttributeSet = 5,
        Attribute = 6,
        GameplayTag = 7,
    }

    public enum ConfigRegistryReferenceKind
    {
        Direct = 0,
        ApplyGameplayEffectRequest = 1,
        AbilityCommandGrant = 2,
        AbilityCost = 3,
        AbilityCooldown = 4,
        GameplayEffectGrantedAbility = 5,
        AbilityTimeline = 6,
        AbilityCuePreset = 7,
        AbilityActivationEffect = 8,
        GameplayEffectPeriodEffect = 9,
        GameplayEffectOverflowEffect = 10,
        TimelineApplyEffect = 11,
        TimelineCuePreset = 12,
    }

    public enum ConfigRegistryDiagnosticSeverity
    {
        Warning = 1,
        Error = 2,
    }

    public enum ConfigRegistryDiagnosticCode
    {
        MissingConfig = 1,
    }

    public readonly struct ConfigRegistryReferenceContext
    {
        public readonly ConfigRegistryConfigKind SourceKind;
        public readonly int SourceCode;
        public readonly ConfigRegistryReferenceKind ReferenceKind;

        public ConfigRegistryReferenceContext(
            ConfigRegistryConfigKind sourceKind,
            int sourceCode,
            ConfigRegistryReferenceKind referenceKind)
        {
            SourceKind = sourceKind;
            SourceCode = sourceCode;
            ReferenceKind = referenceKind;
        }
    }

    public readonly struct ConfigRegistryDiagnostic
    {
        public readonly ConfigRegistryDiagnosticSeverity Severity;
        public readonly ConfigRegistryDiagnosticCode Code;
        public readonly ConfigRegistryConfigKind MissingConfigKind;
        public readonly int MissingConfigCode;
        public readonly ConfigRegistryConfigKind SourceConfigKind;
        public readonly int SourceConfigCode;
        public readonly ConfigRegistryReferenceKind ReferenceKind;
        public readonly string Message;

        public ConfigRegistryDiagnostic(
            ConfigRegistryDiagnosticSeverity severity,
            ConfigRegistryDiagnosticCode code,
            ConfigRegistryConfigKind missingConfigKind,
            int missingConfigCode,
            ConfigRegistryReferenceContext context,
            string message)
        {
            Severity = severity;
            Code = code;
            MissingConfigKind = missingConfigKind;
            MissingConfigCode = missingConfigCode;
            SourceConfigKind = context.SourceKind;
            SourceConfigCode = context.SourceCode;
            ReferenceKind = context.ReferenceKind;
            Message = message;
        }
    }

    public readonly struct ConfigRegistryGraphWarmupResult
    {
        public readonly int AbilityConfigCount;
        public readonly int GameplayEffectConfigCount;
        public readonly int TimelineConfigCount;
        public readonly int AbilityDiagnosticCount;
        public readonly int GameplayEffectDiagnosticCount;
        public readonly int TimelineDiagnosticCount;
        public readonly int NewDiagnosticCount;
        public readonly ConfigRegistryDiagnostic[] Diagnostics;

        public ConfigRegistryGraphWarmupResult(
            int abilityConfigCount,
            int gameplayEffectConfigCount,
            int timelineConfigCount,
            int abilityDiagnosticCount,
            int gameplayEffectDiagnosticCount,
            int timelineDiagnosticCount,
            int newDiagnosticCount,
            ConfigRegistryDiagnostic[] diagnostics)
        {
            AbilityConfigCount = abilityConfigCount;
            GameplayEffectConfigCount = gameplayEffectConfigCount;
            TimelineConfigCount = timelineConfigCount;
            AbilityDiagnosticCount = abilityDiagnosticCount;
            GameplayEffectDiagnosticCount = gameplayEffectDiagnosticCount;
            TimelineDiagnosticCount = timelineDiagnosticCount;
            NewDiagnosticCount = newDiagnosticCount;
            Diagnostics = diagnostics ?? Array.Empty<ConfigRegistryDiagnostic>();
        }

        public static ConfigRegistryGraphWarmupResult Empty =>
            new(0, 0, 0, 0, 0, 0, 0, Array.Empty<ConfigRegistryDiagnostic>());

        public int TotalConfigCount => AbilityConfigCount + GameplayEffectConfigCount + TimelineConfigCount;
    }

    public static class ConfigRegistryDiagnostics
    {
        private static readonly List<ConfigRegistryDiagnostic> Diagnostics = new();
        private static readonly HashSet<string> DiagnosticKeys = new();

        public static ConfigRegistryDiagnosticSeverity MissingConfigSeverityPolicy { get; private set; } =
            ConfigRegistryDiagnosticSeverity.Warning;

        public static int Count => Diagnostics.Count;

        public static IReadOnlyList<ConfigRegistryDiagnostic> Entries => Diagnostics;

        public static ConfigRegistryDiagnostic[] Snapshot()
        {
            return Diagnostics.ToArray();
        }

        public static void Clear()
        {
            Diagnostics.Clear();
            DiagnosticKeys.Clear();
        }

        public static void ClearForConfigKind(ConfigRegistryConfigKind configKind)
        {
            if (configKind == ConfigRegistryConfigKind.None)
            {
                Clear();
                return;
            }

            Diagnostics.RemoveAll(diagnostic =>
                diagnostic.MissingConfigKind == configKind
                || diagnostic.SourceConfigKind == configKind);
            RebuildDiagnosticKeys();
        }

        public static void SetMissingConfigSeverityPolicy(ConfigRegistryDiagnosticSeverity severity)
        {
            MissingConfigSeverityPolicy = severity == default
                ? ConfigRegistryDiagnosticSeverity.Warning
                : severity;
        }

        public static void ResetSeverityPolicy()
        {
            MissingConfigSeverityPolicy = ConfigRegistryDiagnosticSeverity.Warning;
        }

        internal static void ReportMissingConfig(
            ConfigRegistryConfigKind missingKind,
            int missingCode,
            ConfigRegistryReferenceContext context = default)
        {
            if (missingCode <= 0)
                return;

            var key =
                $"{ConfigRegistryDiagnosticCode.MissingConfig}|{missingKind}|{missingCode}|{context.SourceKind}|{context.SourceCode}|{context.ReferenceKind}";
            if (!DiagnosticKeys.Add(key))
                return;

            Diagnostics.Add(new ConfigRegistryDiagnostic(
                MissingConfigSeverityPolicy,
                ConfigRegistryDiagnosticCode.MissingConfig,
                missingKind,
                missingCode,
                context,
                CreateMissingConfigMessage(missingKind, missingCode, context)));
        }

        private static string CreateMissingConfigMessage(
            ConfigRegistryConfigKind missingKind,
            int missingCode,
            ConfigRegistryReferenceContext context)
        {
            if (context.SourceKind != ConfigRegistryConfigKind.None && context.SourceCode > 0)
            {
                return
                    $"Missing {missingKind} config {missingCode}; referenced by {context.SourceKind} config {context.SourceCode} via {context.ReferenceKind}.";
            }

            return $"Missing {missingKind} config {missingCode}; reference kind {context.ReferenceKind}.";
        }

        private static void RebuildDiagnosticKeys()
        {
            DiagnosticKeys.Clear();
            for (var i = 0; i < Diagnostics.Count; i++)
                DiagnosticKeys.Add(CreateDiagnosticKey(Diagnostics[i]));
        }

        private static string CreateDiagnosticKey(ConfigRegistryDiagnostic diagnostic)
        {
            return
                $"{diagnostic.Code}|{diagnostic.MissingConfigKind}|{diagnostic.MissingConfigCode}|{diagnostic.SourceConfigKind}|{diagnostic.SourceConfigCode}|{diagnostic.ReferenceKind}";
        }
    }

    public static class ConfigRegistryGraphValidator
    {
        public static ConfigRegistryGraphWarmupResult Warmup(
            IEnumerable<int> abilityCodes,
            IEnumerable<int> gameplayEffectCodes,
            IEnumerable<int> timelineIds,
            bool clearDiagnostics = true)
        {
            if (clearDiagnostics)
                ConfigRegistryDiagnostics.Clear();

            var before = ConfigRegistryDiagnostics.Count;
            var abilityConfigCount = 0;
            var gameplayEffectConfigCount = 0;
            var timelineConfigCount = 0;
            var abilityDiagnosticCount = 0;
            var gameplayEffectDiagnosticCount = 0;
            var timelineDiagnosticCount = 0;

            foreach (var abilityCode in EnumerateUniquePositiveCodes(abilityCodes))
            {
                abilityConfigCount++;
                abilityDiagnosticCount += ValidateAbilityConfigByID(abilityCode);
            }

            foreach (var gameplayEffectCode in EnumerateUniquePositiveCodes(gameplayEffectCodes))
            {
                gameplayEffectConfigCount++;
                gameplayEffectDiagnosticCount += ValidateGameplayEffectConfigByID(gameplayEffectCode);
            }

            foreach (var timelineId in EnumerateUniquePositiveCodes(timelineIds))
            {
                timelineConfigCount++;
                timelineDiagnosticCount += ValidateTimelineConfigByID(timelineId);
            }

            return new ConfigRegistryGraphWarmupResult(
                abilityConfigCount,
                gameplayEffectConfigCount,
                timelineConfigCount,
                abilityDiagnosticCount,
                gameplayEffectDiagnosticCount,
                timelineDiagnosticCount,
                ConfigRegistryDiagnostics.Count - before,
                ConfigRegistryDiagnostics.Snapshot());
        }

        public static int ValidateAbilityConfigByID(int abilityCode)
        {
            var before = ConfigRegistryDiagnostics.Count;
            var config = AbilityConfigRegistry.GetConfigByID(abilityCode);
            ValidateAbilityConfig(abilityCode, config);
            return ConfigRegistryDiagnostics.Count - before;
        }

        public static int ValidateGameplayEffectConfigByID(int gameplayEffectCode)
        {
            var before = ConfigRegistryDiagnostics.Count;
            var config = GameplayEffectConfigRegistry.GetConfigByID(gameplayEffectCode);
            ValidateGameplayEffectConfig(gameplayEffectCode, config);
            return ConfigRegistryDiagnostics.Count - before;
        }

        public static int ValidateTimelineConfigByID(int timelineId)
        {
            var before = ConfigRegistryDiagnostics.Count;
            var timeline = TimelineAbilityConfigRegistry.GetConfigByID(timelineId);
            ValidateTimelineConfig(timelineId, timeline);
            return ConfigRegistryDiagnostics.Count - before;
        }

        public static int ValidateAbilityConfig(int abilityCode, AbilityConfig config)
        {
            var before = ConfigRegistryDiagnostics.Count;
            var configs = config?.ComponentConfigs ?? Array.Empty<AbilityComponentConfig>();

            for (var i = 0; i < configs.Length; i++)
            {
                switch (configs[i])
                {
                    case ConfAbilityCost cost:
                        ValidateGameplayEffectReference(
                            cost.GameplayEffectCode,
                            ConfigRegistryConfigKind.Ability,
                            abilityCode,
                            ConfigRegistryReferenceKind.AbilityCost);
                        break;
                    case ConfAbilityCooldown cooldown:
                        ValidateGameplayEffectReference(
                            cooldown.GameplayEffectCode,
                            ConfigRegistryConfigKind.Ability,
                            abilityCode,
                            ConfigRegistryReferenceKind.AbilityCooldown);
                        break;
                    case ConfAbilityEffectsOnActivate activationEffects:
                        ValidateGameplayEffectReferences(
                            activationEffects.EffectCodes,
                            ConfigRegistryConfigKind.Ability,
                            abilityCode,
                            ConfigRegistryReferenceKind.AbilityActivationEffect);
                        break;
                    case ConfAbilityTimelineRef timelineRef:
                        ValidateTimelineReference(abilityCode, timelineRef.TimelineId);
                        break;
                }
            }

            return ConfigRegistryDiagnostics.Count - before;
        }

        public static int ValidateGameplayEffectConfig(int gameplayEffectCode, GameplayEffectConfig config)
        {
            var before = ConfigRegistryDiagnostics.Count;
            var configs = config?.ComponentConfigs ?? Array.Empty<GameplayEffectComponentConfig>();

            for (var i = 0; i < configs.Length; i++)
            {
                switch (configs[i])
                {
                    case ConfGrantedAbilityConfig grantedAbilityConfig:
                        ValidateGrantedAbilities(gameplayEffectCode, grantedAbilityConfig.GrantedAbilities);
                        break;
                    case ConfPeriod period:
                        ValidateGameplayEffectReferences(
                            period.GameplayEffectCodes,
                            ConfigRegistryConfigKind.GameplayEffect,
                            gameplayEffectCode,
                            ConfigRegistryReferenceKind.GameplayEffectPeriodEffect);
                        break;
                    case ConfStacking stacking:
                        ValidateGameplayEffectReferences(
                            stacking.OverflowEffectCodes,
                            ConfigRegistryConfigKind.GameplayEffect,
                            gameplayEffectCode,
                            ConfigRegistryReferenceKind.GameplayEffectOverflowEffect);
                        break;
                }
            }

            return ConfigRegistryDiagnostics.Count - before;
        }

        public static int ValidateTimelineConfig(int timelineId, XParamTimeline timeline)
        {
            var before = ConfigRegistryDiagnostics.Count;
            if (timeline?.Tracks == null || timeline.Tracks.Count == 0)
                return 0;

            var sourceCode = timeline.ID > 0 ? timeline.ID : timelineId;
            for (var trackIndex = 0; trackIndex < timeline.Tracks.Count; trackIndex++)
            {
                var track = timeline.Tracks[trackIndex];
                if (track?.ActionClips == null || track.ActionClips.Count == 0)
                    continue;

                for (var clipIndex = 0; clipIndex < track.ActionClips.Count; clipIndex++)
                    ValidateTimelineClip(sourceCode, track.ActionClips[clipIndex]);
            }

            return ConfigRegistryDiagnostics.Count - before;
        }

        private static void ValidateTimelineReference(int abilityCode, int timelineId)
        {
            if (timelineId <= 0)
                return;

            var timeline = TimelineAbilityConfigRegistry.GetConfigByID(
                timelineId,
                new ConfigRegistryReferenceContext(
                    ConfigRegistryConfigKind.Ability,
                    abilityCode,
                    ConfigRegistryReferenceKind.AbilityTimeline));
            if (timeline != null)
                ValidateTimelineConfig(timelineId, timeline);
        }

        private static void ValidateTimelineClip(int timelineId, TimelineActionClipData clip)
        {
            if (clip == null)
                return;

            switch (clip.Parameter)
            {
                case XParamApplyEffects applyEffects:
                    ValidateGameplayEffectReferences(
                        applyEffects.IDs,
                        ConfigRegistryConfigKind.TimelineAbility,
                        timelineId,
                        ConfigRegistryReferenceKind.TimelineApplyEffect);
                    break;
                case XParamEffectIDs effectIds:
                    ValidateGameplayEffectReferences(
                        effectIds.IDs,
                        ConfigRegistryConfigKind.TimelineAbility,
                        timelineId,
                        ConfigRegistryReferenceKind.TimelineApplyEffect);
                    break;
                case XParamCueList cueList:
                    ValidateGameplayCueReferences(
                        cueList.IDs,
                        ConfigRegistryConfigKind.TimelineAbility,
                        timelineId,
                        ConfigRegistryReferenceKind.TimelineCuePreset);
                    break;
                case XParamCueIDs cueIds:
                    ValidateGameplayCueReferences(
                        cueIds.IDs,
                        ConfigRegistryConfigKind.TimelineAbility,
                        timelineId,
                        ConfigRegistryReferenceKind.TimelineCuePreset);
                    break;
            }
        }

        private static void ValidateGrantedAbilities(
            int gameplayEffectCode,
            GrantedAbilityConfigSetting[] settings)
        {
            if (settings == null || settings.Length == 0)
                return;

            for (var i = 0; i < settings.Length; i++)
            {
                var abilityCode = settings[i].AbilityCode;
                if (abilityCode <= 0)
                    continue;

                AbilityConfigRegistry.GetConfigByID(
                    abilityCode,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.GameplayEffect,
                        gameplayEffectCode,
                        ConfigRegistryReferenceKind.GameplayEffectGrantedAbility));
            }
        }

        private static void ValidateGameplayEffectReference(
            int gameplayEffectCode,
            ConfigRegistryConfigKind sourceKind,
            int sourceCode,
            ConfigRegistryReferenceKind referenceKind)
        {
            if (gameplayEffectCode <= 0)
                return;

            GameplayEffectConfigRegistry.GetConfigByID(
                gameplayEffectCode,
                new ConfigRegistryReferenceContext(sourceKind, sourceCode, referenceKind));
        }

        private static void ValidateGameplayEffectReferences(
            int[] gameplayEffectCodes,
            ConfigRegistryConfigKind sourceKind,
            int sourceCode,
            ConfigRegistryReferenceKind referenceKind)
        {
            if (gameplayEffectCodes == null || gameplayEffectCodes.Length == 0)
                return;

            for (var i = 0; i < gameplayEffectCodes.Length; i++)
                ValidateGameplayEffectReference(
                    gameplayEffectCodes[i],
                    sourceKind,
                    sourceCode,
                    referenceKind);
        }

        private static void ValidateGameplayCueReferences(
            int[] gameplayCueCodes,
            ConfigRegistryConfigKind sourceKind,
            int sourceCode,
            ConfigRegistryReferenceKind referenceKind)
        {
            if (gameplayCueCodes == null || gameplayCueCodes.Length == 0)
                return;

            for (var i = 0; i < gameplayCueCodes.Length; i++)
            {
                var gameplayCueCode = gameplayCueCodes[i];
                if (gameplayCueCode <= 0)
                    continue;

                GameplayCueConfigRegistry.GetConfigByID(
                    gameplayCueCode,
                    new ConfigRegistryReferenceContext(sourceKind, sourceCode, referenceKind));
            }
        }

        private static IEnumerable<int> EnumerateUniquePositiveCodes(IEnumerable<int> codes)
        {
            if (codes == null)
                yield break;

            var visited = new HashSet<int>();
            foreach (var code in codes)
            {
                if (code <= 0 || !visited.Add(code))
                    continue;

                yield return code;
            }
        }
    }

    public enum GameplayEffectDefinitionLifecycleOwnerKind
    {
        GameplayEffectConfigRegistry = 1,
    }

    public readonly struct GameplayEffectDefinitionCacheState
    {
        public readonly GameplayEffectDefinitionLifecycleOwnerKind OwnerKind;
        public readonly bool HasConfigProvider;
        public readonly int Generation;
        public readonly int CachedPrototypeCount;
        public readonly int CachedStaticDefinitionBlobCount;

        public GameplayEffectDefinitionCacheState(
            GameplayEffectDefinitionLifecycleOwnerKind ownerKind,
            bool hasConfigProvider,
            int generation,
            int cachedPrototypeCount,
            int cachedStaticDefinitionBlobCount)
        {
            OwnerKind = ownerKind;
            HasConfigProvider = hasConfigProvider;
            Generation = generation;
            CachedPrototypeCount = cachedPrototypeCount;
            CachedStaticDefinitionBlobCount = cachedStaticDefinitionBlobCount;
        }
    }

    public readonly struct GameplayEffectDefinitionCacheReloadResult
    {
        public readonly GameplayEffectDefinitionCacheState Before;
        public readonly GameplayEffectDefinitionCacheState After;
        public readonly bool UsedEntityManagerForPrototypeDisposal;
        public readonly int RemovedPrototypeEntryCount;
        public readonly int DestroyedPrototypeEntityCount;
        public readonly int RemovedStaticDefinitionBlobEntryCount;
        public readonly int DisposedStaticDefinitionBlobCount;
        public readonly int ClearedGameplayEffectDiagnosticCount;

        public GameplayEffectDefinitionCacheReloadResult(
            GameplayEffectDefinitionCacheState before,
            GameplayEffectDefinitionCacheState after,
            bool usedEntityManagerForPrototypeDisposal,
            int removedPrototypeEntryCount,
            int destroyedPrototypeEntityCount,
            int removedStaticDefinitionBlobEntryCount,
            int disposedStaticDefinitionBlobCount,
            int clearedGameplayEffectDiagnosticCount)
        {
            Before = before;
            After = after;
            UsedEntityManagerForPrototypeDisposal = usedEntityManagerForPrototypeDisposal;
            RemovedPrototypeEntryCount = removedPrototypeEntryCount;
            DestroyedPrototypeEntityCount = destroyedPrototypeEntityCount;
            RemovedStaticDefinitionBlobEntryCount = removedStaticDefinitionBlobEntryCount;
            DisposedStaticDefinitionBlobCount = disposedStaticDefinitionBlobCount;
            ClearedGameplayEffectDiagnosticCount = clearedGameplayEffectDiagnosticCount;
        }
    }

    public static class GameplayEffectConfigRegistry
    {
        private static Func<int, GameplayEffectConfig> _getConfigByID;
        private static int _definitionCacheGeneration;
        private static readonly Dictionary<int, Entity> PrototypeByCode = new();
        private static readonly Dictionary<int, BlobAssetReference<GEStaticDefinitionBlob>> StaticDefinitionBlobByCode = new();

        public static GameplayEffectDefinitionCacheReloadResult RegisterGetConfigByIDFunc(
            Func<int, GameplayEffectConfig> func)
        {
            var before = CreateDefinitionCacheState();
            var diagnosticCountBefore = ConfigRegistryDiagnostics.Count;
            ConfigRegistryDiagnostics.ClearForConfigKind(ConfigRegistryConfigKind.GameplayEffect);
            var clearedDiagnosticsCount = diagnosticCountBefore - ConfigRegistryDiagnostics.Count;
            _getConfigByID = func;
            return ClearDefinitionCaches(before, clearedDiagnosticsCount);
        }

        public static GameplayEffectConfig GetConfigByID(
            int id,
            ConfigRegistryReferenceContext context = default)
        {
            var config = _getConfigByID?.Invoke(id);
            if (config == null)
                ConfigRegistryDiagnostics.ReportMissingConfig(
                    ConfigRegistryConfigKind.GameplayEffect,
                    id,
                    context);
            return config;
        }

        public static GameplayEffectDefinitionCacheState GetDefinitionCacheState()
        {
            return CreateDefinitionCacheState();
        }

        public static GameplayEffectDefinitionCacheReloadResult ReloadDefinitionCaches(
            bool clearGameplayEffectDiagnostics = false)
        {
            var before = CreateDefinitionCacheState();
            var clearedDiagnosticsCount = 0;

            if (clearGameplayEffectDiagnostics)
            {
                var diagnosticCountBefore = ConfigRegistryDiagnostics.Count;
                ConfigRegistryDiagnostics.ClearForConfigKind(ConfigRegistryConfigKind.GameplayEffect);
                clearedDiagnosticsCount = diagnosticCountBefore - ConfigRegistryDiagnostics.Count;
            }

            return ClearDefinitionCaches(before, clearedDiagnosticsCount);
        }

        internal static Entity CreateRuntimeEffectInstance(
            EntityManager entityManager,
            int gameplayEffectCode,
            ConfigRegistryReferenceContext context = default)
        {
            if (TryGetLivePrototype(entityManager, gameplayEffectCode, out var prototype))
                return GameplayEffectEntityFactory.InstantiateFromPrototype(entityManager, prototype);

            var config = GetConfigByID(gameplayEffectCode, context);
            if (config == null)
                return Entity.Null;

            if (!GameplayEffectEntityFactory.CanCreatePrototypeFromConfig(config.ComponentConfigs))
            {
                EnsureStaticDefinitionBlobFromConfig(
                    entityManager,
                    gameplayEffectCode,
                    config.ComponentConfigs);
                return GameplayEffectEntityFactory.CreateFromConfig(entityManager, config.ComponentConfigs);
            }

            prototype = GameplayEffectEntityFactory.CreatePrototypeFromConfig(
                entityManager,
                gameplayEffectCode,
                config.ComponentConfigs);
            PrototypeByCode[gameplayEffectCode] = prototype;
            CacheStaticDefinitionBlob(entityManager, gameplayEffectCode, prototype);
            return GameplayEffectEntityFactory.InstantiateFromPrototype(entityManager, prototype);
        }

        public static bool TryWarmupRuntimePrototype(
            EntityManager entityManager,
            int gameplayEffectCode,
            ConfigRegistryReferenceContext context = default)
        {
            if (TryGetLivePrototype(entityManager, gameplayEffectCode, out _))
                return true;

            var config = GetConfigByID(gameplayEffectCode, context);
            if (config == null)
                return false;

            if (!GameplayEffectEntityFactory.CanCreatePrototypeFromConfig(config.ComponentConfigs))
            {
                EnsureStaticDefinitionBlobFromConfig(
                    entityManager,
                    gameplayEffectCode,
                    config.ComponentConfigs);
                return false;
            }

            var prototype = GameplayEffectEntityFactory.CreatePrototypeFromConfig(
                entityManager,
                gameplayEffectCode,
                config.ComponentConfigs);
            PrototypeByCode[gameplayEffectCode] = prototype;
            CacheStaticDefinitionBlob(entityManager, gameplayEffectCode, prototype);
            return true;
        }

        internal static bool TryGetCachedPrototype(int gameplayEffectCode, out Entity prototype)
        {
            if (!PrototypeByCode.TryGetValue(gameplayEffectCode, out prototype))
                return false;

            if (!GASManager.IsInitialized)
                return prototype != Entity.Null;

            if (prototype != Entity.Null && GASManager.EntityManager.Exists(prototype))
                return true;

            PrototypeByCode.Remove(gameplayEffectCode);
            RemoveCachedStaticDefinitionBlob(gameplayEffectCode);
            prototype = Entity.Null;
            return false;
        }

        internal static int CachedPrototypeCount => PrototypeByCode.Count;

        internal static bool TryGetCachedStaticDefinitionBlob(
            int gameplayEffectCode,
            out BlobAssetReference<GEStaticDefinitionBlob> blob)
        {
            if (StaticDefinitionBlobByCode.TryGetValue(gameplayEffectCode, out blob)
                && blob.IsCreated)
            {
                return true;
            }

            if (StaticDefinitionBlobByCode.ContainsKey(gameplayEffectCode))
                StaticDefinitionBlobByCode.Remove(gameplayEffectCode);

            blob = default;
            return false;
        }

        internal static int CachedStaticDefinitionBlobCount => StaticDefinitionBlobByCode.Count;

        internal static bool TryGetOrCreateStaticDefinitionBlob(
            EntityManager entityManager,
            int gameplayEffectCode,
            out BlobAssetReference<GEStaticDefinitionBlob> blob,
            ConfigRegistryReferenceContext context = default)
        {
            if (TryGetCachedStaticDefinitionBlob(gameplayEffectCode, out blob))
                return true;

            if (TryGetLivePrototype(entityManager, gameplayEffectCode, out var prototype))
            {
                CacheStaticDefinitionBlob(entityManager, gameplayEffectCode, prototype);
                return TryGetCachedStaticDefinitionBlob(gameplayEffectCode, out blob);
            }

            var config = GetConfigByID(gameplayEffectCode, context);
            if (config == null)
            {
                blob = default;
                return false;
            }

            EnsureStaticDefinitionBlobFromConfig(
                entityManager,
                gameplayEffectCode,
                config.ComponentConfigs);

            return TryGetCachedStaticDefinitionBlob(gameplayEffectCode, out blob);
        }

        private static void EnsureStaticDefinitionBlobFromConfig(
            EntityManager entityManager,
            int gameplayEffectCode,
            GameplayEffectComponentConfig[] componentConfigs)
        {
            if (TryGetCachedStaticDefinitionBlob(gameplayEffectCode, out _))
                return;

            var staticConfigs = FilterStaticDefinitionConfigs(componentConfigs);
            var temporaryPrototype = GameplayEffectEntityFactory.CreatePrototypeFromConfig(
                entityManager,
                gameplayEffectCode,
                staticConfigs);

            CacheStaticDefinitionBlob(entityManager, gameplayEffectCode, temporaryPrototype);

            if (temporaryPrototype != Entity.Null && entityManager.Exists(temporaryPrototype))
                entityManager.DestroyEntity(temporaryPrototype);
        }

        internal static GameplayEffectDefinitionCacheReloadResult ClearPrototypeCache()
        {
            return ReloadDefinitionCaches();
        }

        private static GameplayEffectDefinitionCacheReloadResult ClearDefinitionCaches(
            GameplayEffectDefinitionCacheState before,
            int clearedGameplayEffectDiagnosticCount)
        {
            var removedStaticDefinitionBlobEntryCount = StaticDefinitionBlobByCode.Count;
            var disposedStaticDefinitionBlobCount = DisposeCachedStaticDefinitionBlobs();
            var removedPrototypeEntryCount = PrototypeByCode.Count;
            var destroyedPrototypeEntityCount = DestroyCachedPrototypeEntities();

            PrototypeByCode.Clear();
            _definitionCacheGeneration++;

            return new GameplayEffectDefinitionCacheReloadResult(
                before,
                CreateDefinitionCacheState(),
                GASManager.IsInitialized,
                removedPrototypeEntryCount,
                destroyedPrototypeEntityCount,
                removedStaticDefinitionBlobEntryCount,
                disposedStaticDefinitionBlobCount,
                clearedGameplayEffectDiagnosticCount);
        }

        private static int DestroyCachedPrototypeEntities()
        {
            if (!GASManager.IsInitialized)
                return 0;

            var destroyedPrototypeEntityCount = 0;
            var entityManager = GASManager.EntityManager;
            foreach (var prototype in PrototypeByCode.Values)
            {
                if (prototype != Entity.Null && entityManager.Exists(prototype))
                {
                    entityManager.DestroyEntity(prototype);
                    destroyedPrototypeEntityCount++;
                }
            }

            return destroyedPrototypeEntityCount;
        }

        private static void CacheStaticDefinitionBlob(
            EntityManager entityManager,
            int gameplayEffectCode,
            Entity prototype)
        {
            RemoveCachedStaticDefinitionBlob(gameplayEffectCode);

            var blob = GEStaticDefinitionBlobBuilder.BuildFromPrototype(
                entityManager,
                prototype);
            if (blob.IsCreated)
                StaticDefinitionBlobByCode[gameplayEffectCode] = blob;
        }

        private static int DisposeCachedStaticDefinitionBlobs()
        {
            var disposedStaticDefinitionBlobCount = 0;
            foreach (var blob in StaticDefinitionBlobByCode.Values)
            {
                if (blob.IsCreated)
                {
                    blob.Dispose();
                    disposedStaticDefinitionBlobCount++;
                }
            }

            StaticDefinitionBlobByCode.Clear();
            return disposedStaticDefinitionBlobCount;
        }

        private static int RemoveCachedStaticDefinitionBlob(int gameplayEffectCode)
        {
            if (!StaticDefinitionBlobByCode.TryGetValue(gameplayEffectCode, out var blob))
                return 0;

            var disposedStaticDefinitionBlobCount = 0;
            if (blob.IsCreated)
            {
                blob.Dispose();
                disposedStaticDefinitionBlobCount = 1;
            }

            StaticDefinitionBlobByCode.Remove(gameplayEffectCode);
            return disposedStaticDefinitionBlobCount;
        }

        private static GameplayEffectComponentConfig[] FilterStaticDefinitionConfigs(
            GameplayEffectComponentConfig[] componentConfigs)
        {
            if (componentConfigs == null || componentConfigs.Length == 0)
                return Array.Empty<GameplayEffectComponentConfig>();

            var staticConfigs = new List<GameplayEffectComponentConfig>(componentConfigs.Length);
            foreach (var componentConfig in componentConfigs)
            {
                if (componentConfig == null || !componentConfig.SupportsStaticDefinitionBlob)
                    continue;

                staticConfigs.Add(componentConfig);
            }

            return staticConfigs.ToArray();
        }

        private static bool TryGetLivePrototype(
            EntityManager entityManager,
            int gameplayEffectCode,
            out Entity prototype)
        {
            if (!PrototypeByCode.TryGetValue(gameplayEffectCode, out prototype))
                return false;

            if (prototype != Entity.Null && entityManager.Exists(prototype))
                return true;

            PrototypeByCode.Remove(gameplayEffectCode);
            RemoveCachedStaticDefinitionBlob(gameplayEffectCode);
            prototype = Entity.Null;
            return false;
        }

        private static GameplayEffectDefinitionCacheState CreateDefinitionCacheState()
        {
            return new GameplayEffectDefinitionCacheState(
                GameplayEffectDefinitionLifecycleOwnerKind.GameplayEffectConfigRegistry,
                _getConfigByID != null,
                _definitionCacheGeneration,
                PrototypeByCode.Count,
                StaticDefinitionBlobByCode.Count);
        }
    }
}
