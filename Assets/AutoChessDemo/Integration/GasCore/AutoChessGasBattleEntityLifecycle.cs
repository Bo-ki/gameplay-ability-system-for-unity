using System;
using System.Collections.Generic;
using GAS.Runtime;
using Unity.Collections;

namespace GAS.AutoChessDemo
{
    /// <summary>
    /// 持有 AutoChess 业务句柄到 Runtime v1 OwnerAscHandle 的唯一映射；不创建旧 ASC 实体或旧 CommandPort。
    /// </summary>
    internal static class AutoChessGasBattleEntityLifecycle
    {
        private static int _nextBattleUnitKey;
        private static ulong _nextAscStableId;
        private static ulong _nextCommandOrdinal;
        private static readonly Dictionary<AutoChessBattleUnitKey, OwnerAscHandle> BattleUnitRegistry =
            new Dictionary<AutoChessBattleUnitKey, OwnerAscHandle>();
        private static readonly Dictionary<AutoChessBattleUnitKey, AutoChessUnitDefinition> BattleUnitDefinitions =
            new Dictionary<AutoChessBattleUnitKey, AutoChessUnitDefinition>();

        internal static GasStageBSpawnFaultReason LastBootstrapFailure { get; private set; }

        /// <summary>
        /// 为业务单位分配单调稳定 OwnerAscHandle；Stage-B SpawnBatch 由战局宿主统一提交。
        /// </summary>
        public static AutoChessGasBattleUnitHandle CreateBattleUnit(AutoChessUnitDefinition definition)
        {
            if (AutoChessGasRuntimeHost.RuntimeOwner == null)
                return default;

            var key = AutoChessBattleUnitKey.Create(++_nextBattleUnitKey);
            var owner = new OwnerAscHandle(++_nextAscStableId, 1);
            BattleUnitRegistry[key] = owner;
            BattleUnitDefinitions[key] = definition;
            return new AutoChessGasBattleUnitHandle(key);
        }

        /// <summary>
        /// 将 Runtime v1 结构化日志投影为 AutoChess 报告事实。
        /// </summary>
        public static AutoChessBattleReportFact[] CreateReportFacts(
            in GasStructuredLogExportSnapshot structuredLog,
            AutoChessGasBattleUnitHandle[] handles)
        {
            return AutoChessGasBattleReportFactProjector.Project(
                structuredLog,
                CreateRuntimeUnitResolver(handles));
        }

        /// <summary>
        /// 创建业务句柄到稳定 OwnerAsc 身份的报告解析器。
        /// </summary>
        private static AutoChessRuntimeUnitResolver CreateRuntimeUnitResolver(
            AutoChessGasBattleUnitHandle[] handles)
        {
            if (handles == null || handles.Length == 0)
                return new AutoChessRuntimeUnitResolver(Array.Empty<AutoChessRuntimeUnitLink>());

            var links = new AutoChessRuntimeUnitLink[handles.Length];
            for (var i = 0; i < handles.Length; i++)
            {
                links[i] = new AutoChessRuntimeUnitLink(i, handles[i].Key.ReportKey);
            }

            return new AutoChessRuntimeUnitResolver(links);
        }

        /// <summary>
        /// 删除业务句柄对应的 OwnerAsc 映射；实体销毁由 Runtime v1 BoundaryStructuralOwner 负责。
        /// </summary>
        public static void DestroyBattleUnit(AutoChessGasBattleUnitHandle handle)
        {
            if (handle.IsValid)
            {
                BattleUnitRegistry.Remove(handle.Key);
                BattleUnitDefinitions.Remove(handle.Key);
            }
        }

        /// <summary>
        /// 通过唯一 WorldOwner GasCommandPort 提交 typed ApplyEffect 请求，不暴露任何 ECS Entity。
        /// </summary>
        internal static GasCommandAcceptResult RequestApplyEffect(
            AutoChessGasBattleUnitHandle sourceHandle,
            AutoChessGasBattleUnitHandle targetHandle,
            int effectDefinitionId,
            ulong availableTick)
        {
            if (!TryResolveOwnerAsc(sourceHandle, out var sourceAsc)
                || !TryResolveOwnerAsc(targetHandle, out var targetAsc))
                return default;

            var owner = AutoChessGasRuntimeHost.RuntimeOwner;
            if (owner == null)
                return default;

            var battle = new BattleInstanceHandle(1, 1, 1);
            var commandOrdinal = ++_nextCommandOrdinal;
            var context = new GasBoundaryCommandContext(
                battle.SimulationEpoch,
                ((ulong)sourceHandle.Key.ReportKey << 32) | commandOrdinal,
                commandOrdinal,
                availableTick,
                true,
                in battle,
                in sourceAsc,
                BoundaryTargetRef.ForAsc(
                    in battle,
                    in targetAsc,
                    (ulong)targetHandle.Key.ReportKey,
                    1));
            return owner.Port.RequestApplyEffect(
                in context,
                effectDefinitionId,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
        }

        /// <summary>
        /// 清空本次 Runtime host 的业务句柄缓存，避免跨 World 复用旧代际。
        /// </summary>
        public static void ResetRuntimeCache()
        {
            BattleUnitRegistry.Clear();
            BattleUnitDefinitions.Clear();
            _nextBattleUnitKey = 0;
            _nextAscStableId = 0;
            _nextCommandOrdinal = 0;
            LastBootstrapFailure = GasStageBSpawnFaultReason.None;
        }

        /// <summary>
        /// 解析业务句柄对应的 Runtime v1 OwnerAscHandle。
        /// </summary>
        internal static bool TryResolveOwnerAsc(
            AutoChessGasBattleUnitHandle handle,
            out OwnerAscHandle ownerAsc)
        {
            ownerAsc = default;
            return handle.IsValid && BattleUnitRegistry.TryGetValue(handle.Key, out ownerAsc);
        }

        /// <summary>
        /// 将当前战局所有稳定 ASC 一次性录入 Stage-B SpawnBatch，禁止逐单位绕过 owner 门。
        /// </summary>
        internal static bool TryBootstrapSession(AutoChessGasBattleUnitHandle[] handles)
        {
            LastBootstrapFailure = GasStageBSpawnFaultReason.None;
            var owner = AutoChessGasRuntimeHost.RuntimeOwner;
            if (owner == null || handles == null || handles.Length == 0)
            {
                LastBootstrapFailure = GasStageBSpawnFaultReason.SessionLayout;
                return false;
            }

            var battle = new BattleInstanceHandle(1, 1, 1);
            var battles = new NativeArray<GasStageBBattleBootstrapRequest>(1, Allocator.Temp);
            var ascs = new NativeArray<GasStageBAscBootstrapRequest>(handles.Length, Allocator.Temp);
            var attributeCount = handles.Length * 3;
            var abilityCount = CountInitialAbilities(handles);
            var attributes = new NativeArray<PendingAttributeInitialization>(attributeCount, Allocator.Temp);
            var tags = new NativeArray<PendingTagInitialization>(0, Allocator.Temp);
            var abilities = new NativeArray<PendingGrantedAbilityInitialization>(abilityCount, Allocator.Temp);
            var initialEffects = new NativeArray<PendingInitialGameplayEffect>(0, Allocator.Temp);
            var nextAttribute = 0;
            var nextAbility = 0;
            var configOrdinal = 0;
            battles[0] = new GasStageBBattleBootstrapRequest
            {
                BattleInstance = battle,
                MemberStart = 0,
                MemberCount = handles.Length,
                MembershipOrdinalRoot = 1,
            };
            for (var index = 0; index < handles.Length; index++)
            {
                if (!TryResolveOwnerAsc(handles[index], out var ownerAsc))
                {
                    LastBootstrapFailure = GasStageBSpawnFaultReason.AscIdentityInvalid;
                    battles.Dispose();
                    ascs.Dispose();
                    attributes.Dispose();
                    tags.Dispose();
                    abilities.Dispose();
                    initialEffects.Dispose();
                    return false;
                }
                if (!BattleUnitDefinitions.TryGetValue(handles[index].Key, out var definition))
                {
                    LastBootstrapFailure = GasStageBSpawnFaultReason.SessionLayout;
                    battles.Dispose();
                    ascs.Dispose();
                    attributes.Dispose();
                    tags.Dispose();
                    abilities.Dispose();
                    initialEffects.Dispose();
                    return false;
                }
                var unitAttributeStart = nextAttribute;
                attributes[nextAttribute++] = CreateAttributeInitialization(0, definition.Health, ref configOrdinal);
                attributes[nextAttribute++] = CreateAttributeInitialization(1, definition.Energy, ref configOrdinal);
                attributes[nextAttribute++] = CreateAttributeInitialization(2, definition.Attack, ref configOrdinal);
                var unitAbilities = definition.CreateAbilityCodes();
                var unitAbilityStart = nextAbility;
                for (var abilityIndex = 0; abilityIndex < unitAbilities.Length; abilityIndex++)
                {
                    abilities[nextAbility++] = new PendingGrantedAbilityInitialization
                    {
                        LayoutIndex = ResolveAbilityLayoutIndex(unitAbilities[abilityIndex]),
                        ConfigOrdinal = configOrdinal++,
                    };
                }
                ascs[index] = new GasStageBAscBootstrapRequest
                {
                    OwnerAsc = ownerAsc,
                    BattleInstance = battle,
                    RegistryOrdinal = index,
                    ScenarioUnitId = (ulong)(index + 1),
                    SideId = (int)definition.Team,
                    TeamId = (int)definition.Team,
                    MembershipOrdinal = index + 1,
                    OwnerActorStableId = (ulong)(index + 1),
                    AvatarActorStableId = (ulong)(index + 1),
                    ActorBindingGeneration = 1,
                    RandomState0 = (ulong)(index + 1),
                    RandomState1 = (ulong)(index + 1),
                    AttributeInitializationStart = unitAttributeStart,
                    AttributeInitializationCount = 3,
                    GrantedAbilityInitializationStart = unitAbilityStart,
                    GrantedAbilityInitializationCount = unitAbilities.Length,
                };
            }

            var request = new GasStageBSessionBootstrapRequest
            {
                SimulationEpoch = 1,
                SpawnBatchId = 1,
                Config = new GasSessionConfig { TickRate = 20, RuleVersion = 1, BoundaryPolicyVersion = 1 },
                ScaleProfile = CreateScaleProfile(handles.Length),
                Catalog = AutoChessGasCatalogSession.Catalog,
                CatalogExpectation = AutoChessGasCatalogSession.Expectation,
            };
            var failure = owner.RecordSpawnBatch(
                in request,
                battles,
                ascs,
                attributes,
                tags,
                abilities,
                initialEffects);
            battles.Dispose();
            ascs.Dispose();
            attributes.Dispose();
            tags.Dispose();
            abilities.Dispose();
            initialEffects.Dispose();
            LastBootstrapFailure = failure;
            return failure == GasStageBSpawnFaultReason.None;
        }

        /// <summary>
        /// 创建满足 AutoChess 多单位 SpawnBatch 的固定容量档位。
        /// </summary>
        private static GasScaleProfile CreateScaleProfile(int unitCount)
        {
            var capacity = Math.Max(unitCount, 1);
            return new GasScaleProfile
            {
                ProfileId = 1,
                ProfileVersion = 1,
                ProfileHash = 1,
                MaxFixedTicksPerBatch = 1,
                MaximumDeltaTimeTicks = 1,
                MaxSpawnBatchSize = capacity,
                MaxBattleInstanceCount = 1,
                MaxAscRegistryCount = capacity,
                MaxBoundaryCommandCount = capacity * 4,
                MaxBoundaryCommandPayloadCount = capacity * 8,
                MaxOwnerPlanCount = capacity * 4,
                MaxResolvedTargetCount = capacity * 4,
                MaxEffectOperationCount = capacity * 4,
                MaxOwnerReservationCount = capacity * 4,
                MaxTargetReservationCount = capacity * 4,
                MaxCoreFactCount = capacity * 16,
                MaxNextTickRouteCount = capacity * 4,
                MaxStructuralIntentCount = capacity * 4,
                MaxPendingAttributeInitializationCount = capacity * 3,
                MaxPendingTagInitializationCount = 0,
                MaxPendingGrantedAbilityInitializationCount = capacity * 3,
                MaxGrantedAbilityCount = capacity * 3,
                MaxAbilityActivationCount = capacity * 2,
                MaxAbilityContinuationCount = capacity * 2,
                MaxAbilitySubscriptionCount = capacity * 2,
                MaxCooldownGateCount = capacity * 2,
                MaxActivationOwnedContributionCount = capacity * 2,
                MaxEmittedApplicationRefCount = capacity * 2,
                MaxActiveEffectCount = capacity * 4,
                MaxPayloadRangeRecordCount = capacity * 4,
                MaxPayloadValueCount = capacity * 4,
                MaxAttributeAggregatorCount = capacity * 2,
                MaxLiveDependencyCount = capacity * 2,
                MaxLiveDependencyRouteCount = capacity * 2,
                MaxPendingCommandCount = capacity * 4,
                MaxSessionBoundaryFactCount = capacity * 16,
                MaxAscBoundaryFactCount = capacity * 16,
            };
        }

        /// <summary>
        /// 计算本批单位生成期默认 Ability grant 总数，供固定容量 preflight 使用。
        /// </summary>
        private static int CountInitialAbilities(AutoChessGasBattleUnitHandle[] handles)
        {
            var count = 0;
            for (var index = 0; index < handles.Length; index++)
            {
                if (BattleUnitDefinitions.TryGetValue(handles[index].Key, out var definition))
                    count += definition.CreateAbilityCodes().Length;
            }
            return count;
        }

        /// <summary>
        /// 创建带显式 Base/Current 初值的 Spawn Attribute 初始化记录。
        /// </summary>
        private static PendingAttributeInitialization CreateAttributeInitialization(
            int layoutIndex,
            float value,
            ref int configOrdinal)
        {
            return new PendingAttributeInitialization
            {
                LayoutIndex = layoutIndex,
                ConfigOrdinal = configOrdinal++,
                HasExplicitValue = 1,
                BaseValue = value,
                CurrentValue = value,
            };
        }

        /// <summary>
        /// 将生成 Ability stable id 映射到 Catalog 中的稳定 dense Definition index。
        /// </summary>
        private static int ResolveAbilityLayoutIndex(int abilityCode)
        {
            switch (abilityCode)
            {
                case AutoChessBattleRules.AbilityPlayerAttack:
                    return 0;
                case AutoChessBattleRules.AbilityEnemyAttack:
                    return 1;
                case AutoChessBattleRules.AbilityPlayerExecute:
                    return 2;
                case AutoChessBattleRules.AbilityPlayerPoison:
                    return 3;
                default:
                    return -1;
            }
        }
    }

    /// <summary>
    /// 把 Runtime v1 稳定报告键映射回 AutoChess 单位数组索引。
    /// </summary>
    internal readonly struct AutoChessRuntimeUnitResolver
    {
        private readonly AutoChessRuntimeUnitLink[] _links;

        public AutoChessRuntimeUnitResolver(AutoChessRuntimeUnitLink[] links)
        {
            _links = links ?? Array.Empty<AutoChessRuntimeUnitLink>();
        }

        /// <summary>
        /// 按报告键查找业务单位索引，未知键显式返回 -1。
        /// </summary>
        public int ResolveUnitIndex(int reportKey)
        {
            if (reportKey <= 0 || _links == null)
                return -1;

            for (var i = 0; i < _links.Length; i++)
            {
                if (_links[i].Matches(reportKey))
                    return _links[i].UnitIndex;
            }

            return -1;
        }
    }

    /// <summary>
    /// 保存一个稳定报告键到业务数组索引的不可变映射。
    /// </summary>
    internal readonly struct AutoChessRuntimeUnitLink
    {
        private readonly int _reportKey;

        public readonly int UnitIndex;

        public AutoChessRuntimeUnitLink(int unitIndex, int reportKey)
        {
            UnitIndex = unitIndex;
            _reportKey = reportKey;
        }

        /// <summary>
        /// 判断报告键是否属于该业务单位。
        /// </summary>
        public bool Matches(int reportKey)
        {
            return _reportKey > 0 && _reportKey == reportKey;
        }
    }
}
