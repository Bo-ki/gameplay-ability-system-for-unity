using System;
using System.Globalization;

namespace GAS.Runtime
{
    public static partial class HeadlessAutoChessScenario
    {
        public static HeadlessAutoChessScenarioVariantDefinition GetVariantDefinition(
            HeadlessAutoChessScenarioVariant variant)
        {
            var normalized = NormalizeVariant(variant);
            var units = CreateUnits(normalized);
            return new HeadlessAutoChessScenarioVariantDefinition(
                normalized,
                GetVariantName(normalized),
                GetDeterministicSeed(normalized),
                CountUnits(units, HeadlessAutoChessTeam.Player),
                CountUnits(units, HeadlessAutoChessTeam.Enemy),
                HeadlessAutoChessTeam.Player);
        }

        private static HeadlessAutoChessScenarioVariantDefinition CreateRunVariantDefinition(
            HeadlessAutoChessScenarioVariant variant,
            int unitScale)
        {
            var normalized = NormalizeVariant(variant);
            var scale = unitScale > 0 ? unitScale : 1;
            var units = CreateUnits(normalized, scale);
            var name = GetVariantName(normalized);
            if (scale > 1)
                name += "x" + scale.ToString(CultureInfo.InvariantCulture);

            return new HeadlessAutoChessScenarioVariantDefinition(
                normalized,
                name,
                GetDeterministicSeed(normalized),
                CountUnits(units, HeadlessAutoChessTeam.Player),
                CountUnits(units, HeadlessAutoChessTeam.Enemy),
                HeadlessAutoChessTeam.Player);
        }

        public static string GetVariantName(HeadlessAutoChessScenarioVariant variant)
        {
            switch (NormalizeVariant(variant))
            {
                case HeadlessAutoChessScenarioVariant.PlayerAdvantage:
                    return "PlayerAdvantage";
                case HeadlessAutoChessScenarioVariant.EnemyPressure:
                    return "EnemyPressure";
                case HeadlessAutoChessScenarioVariant.LargeBoard:
                    return "LargeBoard";
                default:
                    return "DefaultBalanced";
            }
        }

        private static HeadlessAutoChessScenarioVariant NormalizeVariant(
            HeadlessAutoChessScenarioVariant variant)
        {
            return variant == HeadlessAutoChessScenarioVariant.PlayerAdvantage
                   || variant == HeadlessAutoChessScenarioVariant.EnemyPressure
                   || variant == HeadlessAutoChessScenarioVariant.LargeBoard
                ? variant
                : HeadlessAutoChessScenarioVariant.DefaultBalanced;
        }

        private static int GetDeterministicSeed(HeadlessAutoChessScenarioVariant variant)
        {
            switch (NormalizeVariant(variant))
            {
                case HeadlessAutoChessScenarioVariant.PlayerAdvantage:
                    return 2001;
                case HeadlessAutoChessScenarioVariant.EnemyPressure:
                    return 3001;
                case HeadlessAutoChessScenarioVariant.LargeBoard:
                    return 4001;
                default:
                    return 1001;
            }
        }

        private static UnitDefinition[] CreateUnits(HeadlessAutoChessScenarioVariant variant)
        {
            switch (NormalizeVariant(variant))
            {
                case HeadlessAutoChessScenarioVariant.PlayerAdvantage:
                    return CreatePlayerAdvantageUnits();
                case HeadlessAutoChessScenarioVariant.EnemyPressure:
                    return CreateEnemyPressureUnits();
                case HeadlessAutoChessScenarioVariant.LargeBoard:
                    return CreateLargeBoardUnits();
                default:
                    return CreateDefaultUnits();
            }
        }

        private static UnitDefinition[] CreateUnits(HeadlessAutoChessScenarioVariant variant, int unitScale)
        {
            var units = CreateUnits(variant);
            return unitScale > 1 ? ScaleUnits(units, unitScale) : units;
        }

        private static UnitDefinition[] ScaleUnits(UnitDefinition[] source, int unitScale)
        {
            if (source == null || source.Length == 0 || unitScale <= 1)
                return source ?? Array.Empty<UnitDefinition>();

            var scaled = new UnitDefinition[source.Length * unitScale];
            var index = 0;
            for (var copy = 0; copy < unitScale; copy++)
            {
                for (var i = 0; i < source.Length; i++)
                {
                    scaled[index] = copy == 0
                        ? source[i]
                        : source[i].WithScaleCopy(copy, copy * source.Length);
                    index++;
                }
            }

            return scaled;
        }

        private static int CountUnits(
            UnitDefinition[] units,
            HeadlessAutoChessTeam team)
        {
            var count = 0;
            for (var i = 0; i < units.Length; i++)
            {
                if (units[i].Team == team)
                    count++;
            }

            return count;
        }
    }
}
