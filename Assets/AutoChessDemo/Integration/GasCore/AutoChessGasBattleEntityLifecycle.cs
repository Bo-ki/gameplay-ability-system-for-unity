using System;
using System.Collections.Generic;
using GAS.Runtime;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasBattleEntityLifecycle
    {
        private static int _nextBattleUnitKey;
        private static readonly Dictionary<AutoChessBattleUnitKey, ASCHandle> BattleUnitRegistry =
            new Dictionary<AutoChessBattleUnitKey, ASCHandle>();

        public static AutoChessGasBattleUnitHandle CreateBattleUnit(AutoChessUnitDefinition definition)
        {
            if (!TryCreateBattleUnitCommandPort(definition, out var commandPort))
                return default;

            commandPort.RequestInitialize(
                Array.Empty<int>(),
                new[]
                {
                    new AttrSetConfig(
                        AutoChessBattleRules.AttributeSetCombat,
                        new[]
                        {
                            new AttributeBaseSetting(
                                AutoChessBattleRules.AttributeHealth,
                                definition.Health,
                                true,
                                true,
                                0f,
                                definition.Health),
                            new AttributeBaseSetting(
                                AutoChessBattleRules.AttributeEnergy,
                                definition.Energy,
                                true,
                                true,
                                0f,
                                definition.Energy),
                        }),
                },
                definition.CreateAbilityCodes(),
                1);

            return RegisterBattleUnit(commandPort);
        }

        public static AutoChessGasBattleDriverHandle CreateBattleDriver()
        {
            if (!GASRuntimeShell.TryResolveRuntimeEntityManager(out var entityManager))
                return default;

            return AutoChessBattleDriverRuntimeStore.ResetAndEnable(entityManager);
        }

        public static AutoChessBattleReportFact[] CreateReportFacts(
            in GasStructuredLogExportSnapshot structuredLog,
            AutoChessGasBattleUnitHandle[] handles)
        {
            return AutoChessGasBattleReportFactProjector.Project(
                structuredLog,
                CreateRuntimeUnitResolver(handles));
        }

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

        public static AutoChessBattleDriverComponent GetBattleDriverStats(
            AutoChessGasBattleDriverHandle driverHandle)
        {
            if (!GASRuntimeShell.TryResolveRuntimeEntityManager(out var entityManager))
                return default;

            return AutoChessBattleDriverRuntimeStore.Read(entityManager, driverHandle);
        }

        public static AutoChessBattleDriverOwnerSnapshot GetBattleDriverOwnerSnapshot(
            AutoChessGasBattleDriverHandle driverHandle)
        {
            if (!GASRuntimeShell.TryResolveRuntimeEntityManager(out var entityManager))
                return default;

            return AutoChessBattleDriverRuntimeStore.CreateOwnerSnapshot(entityManager, driverHandle);
        }

        public static void CloseBattleDriver(AutoChessGasBattleDriverHandle driverHandle)
        {
            if (!GASRuntimeShell.TryResolveRuntimeEntityManager(out var entityManager))
                return;

            AutoChessBattleDriverRuntimeStore.Disable(entityManager, driverHandle);
        }

        public static void DestroyBattleUnit(AutoChessGasBattleUnitHandle handle)
        {
            if (TryResolveAscHandle(handle, out var ascHandle)
                && GASRuntimeShell.TryCreateASCCommandPort(ascHandle, out var commandPort))
            {
                commandPort.RequestDestroy();
            }

            UnregisterBattleUnit(handle);
        }

        public static void ResetRuntimeCache()
        {
            BattleUnitRegistry.Clear();
            _nextBattleUnitKey = 0;
            AutoChessBattleDriverRuntimeStore.ResetRuntimeCache();
        }

        private static AutoChessGasBattleUnitHandle RegisterBattleUnit(ASCCommandPort commandPort)
        {
            var ascHandle = commandPort.Handle;
            if (!ascHandle.IsValid)
                return default;

            var key = AutoChessBattleUnitKey.Create(++_nextBattleUnitKey);
            BattleUnitRegistry[key] = ascHandle;
            commandPort.TrySetComponentData(ASCBoundaryReportKeyComponent.Create(key.ReportKey));
            return new AutoChessGasBattleUnitHandle(key);
        }

        private static void UnregisterBattleUnit(AutoChessGasBattleUnitHandle handle)
        {
            if (!handle.IsValid)
                return;

            BattleUnitRegistry.Remove(handle.Key);
        }

        private static bool TryResolveAscHandle(
            AutoChessGasBattleUnitHandle handle,
            out ASCHandle ascHandle)
        {
            ascHandle = default;
            return handle.IsValid
                   && BattleUnitRegistry.TryGetValue(handle.Key, out ascHandle)
                   && ascHandle.IsValid;
        }

        private static bool TryCreateBattleUnitCommandPort(
            AutoChessUnitDefinition definition,
            out ASCCommandPort commandPort)
        {
            commandPort = default;
            if (!GASRuntimeShell.TryCreateASCCommandPort(
                    ComponentType.ReadWrite<AutoChessBattleUnitComponent>(),
                    out commandPort))
            {
                return false;
            }

            return commandPort.TrySetComponentData(CreateBattleUnitComponent(definition));
        }

        private static AutoChessBattleUnitComponent CreateBattleUnitComponent(
            AutoChessUnitDefinition definition)
        {
            return new AutoChessBattleUnitComponent
            {
                BattleGroup = definition.BattleGroup,
                Team = definition.Team,
                Slot = definition.Slot,
                PrimaryAbilityCode = definition.PrimaryAbilityCode,
                FinisherAbilityCode = definition.FinisherAbilityCode,
                ActiveAbilityCode = definition.ActiveAbilityCode,
                ActiveCastInterval = definition.ActiveCastInterval,
                ActiveCastFrameOffset = definition.ActiveCastFrameOffset,
                HealthAttrSetCode = AutoChessBattleRules.AttributeSetCombat,
                HealthAttrCode = AutoChessBattleRules.AttributeHealth,
                EnergyAttrSetCode = AutoChessBattleRules.AttributeSetCombat,
                EnergyAttrCode = AutoChessBattleRules.AttributeEnergy,
                CooldownTagIndex = AutoChessBattleRules.TagAttackCooldown,
                FinisherHealthThreshold = definition.FinisherHealthThreshold,
                PrimaryTargetPolicy = definition.PrimaryTargetPolicy,
                FinisherTargetPolicy = definition.FinisherTargetPolicy,
            };
        }

    }

    internal readonly struct AutoChessRuntimeUnitResolver
    {
        private readonly AutoChessRuntimeUnitLink[] _links;

        public AutoChessRuntimeUnitResolver(AutoChessRuntimeUnitLink[] links)
        {
            _links = links ?? Array.Empty<AutoChessRuntimeUnitLink>();
        }

        public int ResolveUnitIndex(int reportKey)
        {
            if (reportKey <= 0 || _links == null)
                return -1;

            for (var i = 0; i < _links.Length; i++)
            {
                var link = _links[i];
                if (link.Matches(reportKey))
                    return link.UnitIndex;
            }

            return -1;
        }
    }

    internal readonly struct AutoChessRuntimeUnitLink
    {
        private readonly int _reportKey;

        public readonly int UnitIndex;

        public AutoChessRuntimeUnitLink(int unitIndex, int reportKey)
        {
            UnitIndex = unitIndex;
            _reportKey = reportKey;
        }

        public bool Matches(int reportKey)
        {
            return _reportKey > 0 && _reportKey == reportKey;
        }
    }
}
