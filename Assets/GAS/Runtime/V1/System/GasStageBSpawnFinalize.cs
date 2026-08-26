using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 定义 Stage-B bootstrap 与 SpawnFinalize 的稳定 fail-closed 原因码，写入 SessionFaultLatch.ReasonCode。
    /// </summary>
    public enum GasStageBSpawnFaultReason : int
    {
        None = 0,
        SessionCardinality = 1,
        SessionLayout = 2,
        SessionState = 3,
        EpochMismatch = 4,
        CatalogInvalid = 5,
        ProfileInvalid = 6,
        CapacityExceeded = 7,
        BattleInvalid = 8,
        RegistryInvalid = 9,
        AscLayoutInvalid = 10,
        AscIdentityInvalid = 11,
        SpawnBatchMismatch = 12,
        FixedBufferLengthMismatch = 13,
        PendingInitializationInvalid = 14,
        ConfigOrdinalInvalid = 15,
        DefinitionInvalid = 16,
        AuthorityWrittenBeforeFinalize = 17,
        TickOverflow = 18,
    }

    /// <summary>
    /// 保存 SpawnFinalize 全批验证期间冻结的 Session 只读快照，避免发布阶段重新解释 lifecycle 契约。
    /// </summary>
    internal struct GasStageBSessionSnapshot
    {
        public GasSessionIdentity Identity;
        public GasSessionConfig Config;
        public GasDefinitionRegistry Definitions;
        public GasCatalogRegistry Catalog;
        public GasScaleProfile Profile;
        public SimulationTickState Tick;
        public GasSessionLifecycle Lifecycle;
        public GasSpawnBatchManifest Manifest;
    }

    /// <summary>
    /// 集中定义 Stage-B bootstrap 请求与 ScaleProfile 的纯值验证，recorder 与 Kernel 共享同一失败语义。
    /// </summary>
    internal static class GasStageBSpawnContract
    {
        /// <summary>
        /// 在任何 ECB 命令前验证 Catalog、容量、Battle/ASC ranges 与全部初始化引用。
        /// </summary>
        internal static GasStageBSpawnFaultReason ValidateBootstrapRequest(
            in GasStageBSessionBootstrapRequest session,
            NativeArray<GasStageBBattleBootstrapRequest> battles,
            NativeArray<GasStageBAscBootstrapRequest> ascs,
            NativeArray<PendingAttributeInitialization> attributes,
            NativeArray<PendingTagInitialization> tags,
            NativeArray<PendingGrantedAbilityInitialization> abilities)
        {
            var failure = ValidateSessionRequest(in session);
            if (failure != GasStageBSpawnFaultReason.None)
                return failure;
            if (!battles.IsCreated || !ascs.IsCreated || !attributes.IsCreated ||
                !tags.IsCreated || !abilities.IsCreated)
                return GasStageBSpawnFaultReason.PendingInitializationInvalid;
            if (battles.Length > session.ScaleProfile.MaxBattleInstanceCount)
                return GasStageBSpawnFaultReason.CapacityExceeded;

            failure = ValidateBattleRequests(session.SimulationEpoch, battles, ascs);
            if (failure != GasStageBSpawnFaultReason.None)
                return failure;
            failure = ValidateAscRequests(in session.ScaleProfile, ascs, attributes.Length, tags.Length, abilities.Length);
            if (failure != GasStageBSpawnFaultReason.None)
                return failure;

            ref var catalog = ref session.Catalog.Value;
            failure = ValidateInitializationValues(ref catalog, ascs, attributes, tags, abilities);
            return failure == GasStageBSpawnFaultReason.None && HasUniqueConfigOrdinals(attributes, tags, abilities)
                ? GasStageBSpawnFaultReason.None
                : failure == GasStageBSpawnFaultReason.None
                    ? GasStageBSpawnFaultReason.ConfigOrdinalInvalid
                    : failure;
        }

        /// <summary>
        /// 验证 Session identity、Catalog expectation、Tick 配置与全部 profile 字段。
        /// </summary>
        private static GasStageBSpawnFaultReason ValidateSessionRequest(
            in GasStageBSessionBootstrapRequest session)
        {
            if (session.SimulationEpoch == 0 || session.SpawnBatchId == 0 || session.Config.TickRate <= 0)
                return GasStageBSpawnFaultReason.SessionState;
            if (!session.Catalog.IsCreated)
                return GasStageBSpawnFaultReason.CatalogInvalid;

            ref var catalog = ref session.Catalog.Value;
            var result = GasDefinitionCatalogValidator.Validate(ref catalog, in session.CatalogExpectation);
            if (!result.Succeeded)
                return GasStageBSpawnFaultReason.CatalogInvalid;
            return HasValidProfile(in session.ScaleProfile)
                ? GasStageBSpawnFaultReason.None
                : GasStageBSpawnFaultReason.ProfileInvalid;
        }

        /// <summary>
        /// 验证 Battle handles 唯一、Epoch 一致且成员 ranges 连续覆盖完整 ASC batch。
        /// </summary>
        private static GasStageBSpawnFaultReason ValidateBattleRequests(
            ulong epoch,
            NativeArray<GasStageBBattleBootstrapRequest> battles,
            NativeArray<GasStageBAscBootstrapRequest> ascs)
        {
            var nextMemberStart = 0;
            for (var index = 0; index < battles.Length; index++)
            {
                var battle = battles[index];
                if (!battle.BattleInstance.IsValid || battle.BattleInstance.SimulationEpoch != epoch ||
                    battle.MemberStart != nextMemberStart || battle.MemberCount < 0 ||
                    battle.MemberCount > ascs.Length - battle.MemberStart || battle.MembershipOrdinalRoot < 0 ||
                    battle.MemberCount > 0 &&
                    battle.MembershipOrdinalRoot > int.MaxValue - (battle.MemberCount - 1))
                    return GasStageBSpawnFaultReason.BattleInvalid;
                for (var prior = 0; prior < index; prior++)
                {
                    if (battles[prior].BattleInstance == battle.BattleInstance)
                        return GasStageBSpawnFaultReason.BattleInvalid;
                }
                for (var offset = 0; offset < battle.MemberCount; offset++)
                {
                    var asc = ascs[battle.MemberStart + offset];
                    if (asc.BattleInstance != battle.BattleInstance ||
                        asc.MembershipOrdinal != battle.MembershipOrdinalRoot + offset)
                        return GasStageBSpawnFaultReason.BattleInvalid;
                }
                nextMemberStart += battle.MemberCount;
            }

            return nextMemberStart == ascs.Length
                ? GasStageBSpawnFaultReason.None
                : GasStageBSpawnFaultReason.BattleInvalid;
        }

        /// <summary>
        /// 验证 ASC registry 顺序、owner 唯一性、初始化 ranges 连续覆盖及 per-ASC profile 上限。
        /// </summary>
        private static GasStageBSpawnFaultReason ValidateAscRequests(
            in GasScaleProfile profile,
            NativeArray<GasStageBAscBootstrapRequest> ascs,
            int attributeCount,
            int tagCount,
            int abilityCount)
        {
            if (ascs.Length > profile.MaxSpawnBatchSize || ascs.Length > profile.MaxAscRegistryCount)
                return GasStageBSpawnFaultReason.CapacityExceeded;
            var nextAttribute = 0;
            var nextTag = 0;
            var nextAbility = 0;
            for (var index = 0; index < ascs.Length; index++)
            {
                var asc = ascs[index];
                if (!asc.OwnerAsc.IsValid || !asc.BattleInstance.IsValid || asc.RegistryOrdinal != index ||
                    asc.ActorBindingGeneration == 0 || HasDuplicateOwner(ascs, index))
                    return GasStageBSpawnFaultReason.AscIdentityInvalid;
                if (!HasExpectedRange(asc.AttributeInitializationStart, asc.AttributeInitializationCount,
                        nextAttribute, attributeCount, profile.MaxPendingAttributeInitializationCount) ||
                    !HasExpectedRange(asc.TagInitializationStart, asc.TagInitializationCount,
                        nextTag, tagCount, profile.MaxPendingTagInitializationCount) ||
                    !HasExpectedRange(asc.GrantedAbilityInitializationStart, asc.GrantedAbilityInitializationCount,
                        nextAbility, abilityCount, profile.MaxPendingGrantedAbilityInitializationCount) ||
                    asc.GrantedAbilityInitializationCount > profile.MaxGrantedAbilityCount)
                    return GasStageBSpawnFaultReason.CapacityExceeded;
                nextAttribute += asc.AttributeInitializationCount;
                nextTag += asc.TagInitializationCount;
                nextAbility += asc.GrantedAbilityInitializationCount;
            }

            return nextAttribute == attributeCount && nextTag == tagCount && nextAbility == abilityCount
                ? GasStageBSpawnFaultReason.None
                : GasStageBSpawnFaultReason.PendingInitializationInvalid;
        }

        /// <summary>
        /// 验证三类 Pending 元素的 dense index、Definition 及每个 ASC 内严格递增的 ConfigOrdinal。
        /// </summary>
        private static GasStageBSpawnFaultReason ValidateInitializationValues(
            ref GasDefinitionCatalogBlob catalog,
            NativeArray<GasStageBAscBootstrapRequest> ascs,
            NativeArray<PendingAttributeInitialization> attributes,
            NativeArray<PendingTagInitialization> tags,
            NativeArray<PendingGrantedAbilityInitialization> abilities)
        {
            for (var index = 0; index < ascs.Length; index++)
            {
                var asc = ascs[index];
                if (!ValidateAttributeRange(ref catalog, attributes,
                        asc.AttributeInitializationStart, asc.AttributeInitializationCount) ||
                    !ValidateTagRange(ref catalog, tags,
                        asc.TagInitializationStart, asc.TagInitializationCount) ||
                    !ValidateAbilityRange(ref catalog, abilities,
                        asc.GrantedAbilityInitializationStart, asc.GrantedAbilityInitializationCount))
                    return GasStageBSpawnFaultReason.PendingInitializationInvalid;
            }
            return GasStageBSpawnFaultReason.None;
        }

        /// <summary>
        /// 验证 Attribute 初始化 range 使用合法 dense index 且按 ConfigOrdinal 严格递增。
        /// </summary>
        private static bool ValidateAttributeRange(
            ref GasDefinitionCatalogBlob catalog,
            NativeArray<PendingAttributeInitialization> values,
            int start,
            int count)
        {
            var previousOrdinal = -1;
            for (var offset = 0; offset < count; offset++)
            {
                var value = values[start + offset];
                if (value.LayoutIndex < 0 || value.LayoutIndex >= catalog.AttributeLayout.Entries.Length ||
                    catalog.AttributeLayout.Entries[value.LayoutIndex].LayoutIndex != value.LayoutIndex ||
                    value.ConfigOrdinal <= previousOrdinal)
                    return false;
                previousOrdinal = value.ConfigOrdinal;
            }
            return true;
        }

        /// <summary>
        /// 验证 Tag 初始化 range 使用合法 dense index 且按 ConfigOrdinal 严格递增。
        /// </summary>
        private static bool ValidateTagRange(
            ref GasDefinitionCatalogBlob catalog,
            NativeArray<PendingTagInitialization> values,
            int start,
            int count)
        {
            var previousOrdinal = -1;
            for (var offset = 0; offset < count; offset++)
            {
                var value = values[start + offset];
                if (value.LayoutIndex < 0 || value.LayoutIndex >= catalog.TagCatalog.Entries.Length ||
                    catalog.TagCatalog.Entries[value.LayoutIndex].TagIndex != value.LayoutIndex ||
                    value.ConfigOrdinal <= previousOrdinal)
                    return false;
                previousOrdinal = value.ConfigOrdinal;
            }
            return true;
        }

        /// <summary>
        /// 验证初始 Grant 直接索引合法 Ability Definition 且按 ConfigOrdinal 严格递增。
        /// </summary>
        private static bool ValidateAbilityRange(
            ref GasDefinitionCatalogBlob catalog,
            NativeArray<PendingGrantedAbilityInitialization> values,
            int start,
            int count)
        {
            var previousOrdinal = -1;
            for (var offset = 0; offset < count; offset++)
            {
                var value = values[start + offset];
                if (value.LayoutIndex < 0 || value.LayoutIndex >= catalog.Abilities.Length ||
                    catalog.Abilities[value.LayoutIndex].DefinitionId <= 0 || value.ConfigOrdinal <= previousOrdinal)
                    return false;
                previousOrdinal = value.ConfigOrdinal;
            }
            return true;
        }

        /// <summary>
        /// 验证 ConfigOrdinal 在整个 SpawnBatch 的三类初始化记录间全局唯一。
        /// </summary>
        private static bool HasUniqueConfigOrdinals(
            NativeArray<PendingAttributeInitialization> attributes,
            NativeArray<PendingTagInitialization> tags,
            NativeArray<PendingGrantedAbilityInitialization> abilities)
        {
            for (var index = 0; index < attributes.Length; index++)
            {
                for (var other = index + 1; other < attributes.Length; other++)
                    if (attributes[index].ConfigOrdinal == attributes[other].ConfigOrdinal)
                        return false;
                for (var other = 0; other < tags.Length; other++)
                    if (attributes[index].ConfigOrdinal == tags[other].ConfigOrdinal)
                        return false;
                for (var other = 0; other < abilities.Length; other++)
                    if (attributes[index].ConfigOrdinal == abilities[other].ConfigOrdinal)
                        return false;
            }
            for (var index = 0; index < tags.Length; index++)
            {
                for (var other = index + 1; other < tags.Length; other++)
                    if (tags[index].ConfigOrdinal == tags[other].ConfigOrdinal)
                        return false;
                for (var other = 0; other < abilities.Length; other++)
                    if (tags[index].ConfigOrdinal == abilities[other].ConfigOrdinal)
                        return false;
            }
            for (var index = 0; index < abilities.Length; index++)
                for (var other = index + 1; other < abilities.Length; other++)
                    if (abilities[index].ConfigOrdinal == abilities[other].ConfigOrdinal)
                        return false;
            return true;
        }

        /// <summary>
        /// 判断 ASC owner 是否已在更早 registry ordinal 出现。
        /// </summary>
        private static bool HasDuplicateOwner(NativeArray<GasStageBAscBootstrapRequest> ascs, int index)
        {
            for (var prior = 0; prior < index; prior++)
                if (ascs[prior].OwnerAsc == ascs[index].OwnerAsc)
                    return true;
            return false;
        }

        /// <summary>
        /// 验证初始化 range 从期望位置开始、边界合法且不超过对应 per-ASC profile 容量。
        /// </summary>
        private static bool HasExpectedRange(
            int start,
            int count,
            int expectedStart,
            int totalLength,
            int capacity)
        {
            return start == expectedStart && count >= 0 && count <= capacity &&
                   start >= 0 && count <= totalLength && start <= totalLength - count;
        }

        /// <summary>
        /// 验证 Session、batch、registry、inbox 与 boundary 相关 profile 字段均为非负逻辑容量。
        /// </summary>
        internal static bool HasValidProfile(in GasScaleProfile profile)
        {
            return profile.ProfileId > 0 && profile.ProfileVersion > 0 && profile.ProfileHash != 0 &&
                   profile.MaxFixedTicksPerBatch > 0 &&
                   profile.MaximumDeltaTimeTicks > 0 &&
                   profile.MaximumDeltaTimeTicks <= profile.MaxFixedTicksPerBatch &&
                   profile.MaxSpawnBatchSize >= 0 && profile.MaxBattleInstanceCount >= 0 &&
                   profile.MaxAscRegistryCount >= 0 && profile.MaxBoundaryCommandCount >= 0 &&
                   profile.MaxBoundaryCommandPayloadCount >= 0 && profile.MaxSessionBoundaryFactCount >= 0 &&
                   profile.MaxAscBoundaryFactCount >= 0 && HasValidTickProfile(in profile) &&
                   HasValidAbilityProfile(in profile) &&
                   HasValidEffectProfile(in profile);
        }

        /// <summary>
        /// 验证完整 Tick DAG 的 scratch、reservation、fact 与结构 intent 上限均非负。
        /// </summary>
        private static bool HasValidTickProfile(in GasScaleProfile profile)
        {
            return profile.MaxOwnerPlanCount >= 0 && profile.MaxResolvedTargetCount >= 0 &&
                   profile.MaxEffectOperationCount >= 0 && profile.MaxOwnerReservationCount >= 0 &&
                   profile.MaxTargetReservationCount >= 0 && profile.MaxCoreFactCount >= 0 &&
                   profile.MaxNextTickRouteCount >= 0 && profile.MaxStructuralIntentCount >= 0;
        }

        /// <summary>
        /// 验证 Pending 初始化、Ability slabs 与 ledger profile 字段均为非负逻辑容量。
        /// </summary>
        private static bool HasValidAbilityProfile(in GasScaleProfile profile)
        {
            return profile.MaxPendingAttributeInitializationCount >= 0 &&
                   profile.MaxPendingTagInitializationCount >= 0 &&
                   profile.MaxPendingGrantedAbilityInitializationCount >= 0 &&
                   profile.MaxGrantedAbilityCount >= 0 && profile.MaxAbilityActivationCount >= 0 &&
                   profile.MaxAbilityContinuationCount >= 0 && profile.MaxAbilitySubscriptionCount >= 0 &&
                   profile.MaxCooldownGateCount >= 0 && profile.MaxActivationOwnedContributionCount >= 0 &&
                   profile.MaxEmittedApplicationRefCount >= 0;
        }

        /// <summary>
        /// 验证 Effect、payload/capture、依赖与 PendingCommand profile 字段均为非负逻辑容量。
        /// </summary>
        private static bool HasValidEffectProfile(in GasScaleProfile profile)
        {
            return profile.MaxActiveEffectCount >= 0 && profile.MaxPayloadRangeRecordCount >= 0 &&
                   profile.MaxPayloadValueCount >= 0 &&
                   profile.MaxAttributeAggregatorCount >= 0 &&
                   profile.MaxLiveDependencyCount >= 0 && profile.MaxLiveDependencyRouteCount >= 0 &&
                   profile.MaxPendingCommandCount >= 0;
        }
    }

    /// <summary>
    /// 在 Kernel SpawnPending maintenance lane 中执行全批验证、原子 Ready 发布或 fail-closed teardown 记录。
    /// </summary>
}
