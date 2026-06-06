using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GAS.Editor
{
    internal sealed class AutoChessDemoConfigModel
    {
        private AutoChessDemoConfigModel()
        {
        }

        public int AttributeSetCombat { get; private set; }
        public int AttributeHealth { get; private set; }
        public int AttributeEnergy { get; private set; }
        public int AbilityPlayerAttack { get; private set; }
        public int AbilityEnemyAttack { get; private set; }
        public int AbilityPlayerExecute { get; private set; }
        public int GameplayEffectPlayerAttackDamage { get; private set; }
        public int GameplayEffectEnemyAttackDamage { get; private set; }
        public int GameplayEffectPlayerExecute { get; private set; }
        public int ExecutionCalculationExecuteDamage { get; private set; }
        public int ExecutionCalculationExecuteDamageOutput { get; private set; }
        public int TagAttackCooldown { get; private set; }
        public AutoChessDemoScenarioModel ValidationScenario { get; private set; }
        public IReadOnlyList<AutoChessDemoUnitModel> BaseUnits { get; private set; }

        public static AutoChessDemoConfigModel FromLubanJson(GasCodeGenContext context)
        {
            var source = new AutoChessLubanJsonSource(context);
            var autoChessSource = AutoChessSourceGenConfig.Load(context);
            var abilities = source.CreateAbilityRows();
            var gameplayEffects = source.CreateGameplayEffectRows();
            var attributes = source.CreateAttributeRows();

            var playerAttack = RequiredEffect(gameplayEffects, "AutoChessPlayerAttackDamage");
            var enemyAttack = RequiredEffect(gameplayEffects, "AutoChessEnemyAttackDamage");
            var playerExecute = RequiredEffect(gameplayEffects, "AutoChessPlayerExecute");
            var combatSet = playerAttack.ModifierAttributeSetCode;
            var health = playerAttack.ModifierAttributeCode;
            if (combatSet <= 0 || health <= 0)
                throw new InvalidOperationException("[AutoChessDemoConfig] AutoChess player attack GE must resolve combat health modifier from Luban rows.");

            var energy = attributes
                .Where(row => row.AttributeSetCode == combatSet && row.AttributeCode != health)
                .OrderBy(row => row.AttributeCode)
                .Select(row => row.AttributeCode)
                .FirstOrDefault();
            if (energy <= 0)
                throw new InvalidOperationException("[AutoChessDemoConfig] AutoChess combat energy attribute is missing from Luban rows.");

            var model = new AutoChessDemoConfigModel
            {
                AttributeSetCombat = combatSet,
                AttributeHealth = health,
                AttributeEnergy = energy,
                GameplayEffectPlayerAttackDamage = playerAttack.GameplayEffectCode,
                GameplayEffectEnemyAttackDamage = enemyAttack.GameplayEffectCode,
                GameplayEffectPlayerExecute = playerExecute.GameplayEffectCode,
                AbilityPlayerAttack = RequiredAbility(abilities, playerAttack.GameplayEffectCode),
                AbilityEnemyAttack = RequiredAbility(abilities, enemyAttack.GameplayEffectCode),
                AbilityPlayerExecute = RequiredAbility(abilities, playerExecute.GameplayEffectCode),
                ExecutionCalculationExecuteDamage = autoChessSource.ExecutionCalculationExecuteDamage,
                ExecutionCalculationExecuteDamageOutput = autoChessSource.ExecutionCalculationExecuteDamageOutput,
                TagAttackCooldown = autoChessSource.TagAttackCooldown,
            };

            model.ValidationScenario = autoChessSource.CreateScenario(
                playerAttack.GameplayCueCode > 0
                || enemyAttack.GameplayCueCode > 0
                || playerExecute.GameplayCueCode > 0);
            model.BaseUnits = autoChessSource.CreateUnits(model);
            return model;
        }

        private static AutoChessGameplayEffectRow RequiredEffect(
            IReadOnlyList<AutoChessGameplayEffectRow> rows,
            string name)
        {
            var row = rows.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
            if (row.GameplayEffectCode <= 0)
                throw new InvalidOperationException($"[AutoChessDemoConfig] Missing Luban gameplay effect row: {name}");

            return row;
        }

        private static int RequiredAbility(
            IReadOnlyList<AutoChessAbilityRow> rows,
            int primaryGameplayEffectCode)
        {
            var row = rows.FirstOrDefault(item => item.PrimaryGameplayEffectCode == primaryGameplayEffectCode);
            if (row.AbilityCode <= 0)
                throw new InvalidOperationException($"[AutoChessDemoConfig] Missing Luban ability row for GE {primaryGameplayEffectCode}.");

            return row.AbilityCode;
        }

    }

    internal sealed class AutoChessDemoScenarioModel
    {
        public int Scale;
        public int MaxTicks;
        public int PostVictoryFlushTicks;
        public int ProcessWarmupRuns;
        public float HealthMultiplier;
        public string ExpectedWinner;
        public int MinDriverIssuedCommands;
        public int MinAttributeChanges;
        public int MinExecutionOutputs;
        public int MinCueRequests;
    }

    internal readonly struct AutoChessDemoUnitModel
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string ArchetypeName;
        public readonly string Team;
        public readonly int Slot;
        public readonly float Health;
        public readonly float Energy;
        public readonly int PrimaryAbilityCode;
        public readonly int FinisherAbilityCode;
        public readonly float FinisherHealthThreshold;
        public readonly string PrimaryTargetPolicy;
        public readonly string FinisherTargetPolicy;

        public AutoChessDemoUnitModel(
            string id,
            string displayName,
            string archetypeName,
            string team,
            int slot,
            float health,
            float energy,
            int primaryAbilityCode,
            int finisherAbilityCode,
            float finisherHealthThreshold,
            string primaryTargetPolicy,
            string finisherTargetPolicy)
        {
            Id = id;
            DisplayName = displayName;
            ArchetypeName = archetypeName;
            Team = team;
            Slot = slot;
            Health = health;
            Energy = energy;
            PrimaryAbilityCode = primaryAbilityCode;
            FinisherAbilityCode = finisherAbilityCode;
            FinisherHealthThreshold = finisherHealthThreshold;
            PrimaryTargetPolicy = primaryTargetPolicy;
            FinisherTargetPolicy = finisherTargetPolicy;
        }
    }

    internal readonly struct AutoChessAbilityRow
    {
        public readonly int AbilityCode;
        public readonly int PrimaryGameplayEffectCode;

        public AutoChessAbilityRow(int abilityCode, int primaryGameplayEffectCode)
        {
            AbilityCode = abilityCode;
            PrimaryGameplayEffectCode = primaryGameplayEffectCode;
        }
    }

    internal readonly struct AutoChessGameplayEffectRow
    {
        public readonly int GameplayEffectCode;
        public readonly string Name;
        public readonly int ModifierAttributeSetCode;
        public readonly int ModifierAttributeCode;
        public readonly int GameplayCueCode;

        public AutoChessGameplayEffectRow(
            int gameplayEffectCode,
            string name,
            int modifierAttributeSetCode,
            int modifierAttributeCode,
            int gameplayCueCode)
        {
            GameplayEffectCode = gameplayEffectCode;
            Name = name ?? string.Empty;
            ModifierAttributeSetCode = modifierAttributeSetCode;
            ModifierAttributeCode = modifierAttributeCode;
            GameplayCueCode = gameplayCueCode;
        }
    }

    internal readonly struct AutoChessAttributeRow
    {
        public readonly int AttributeSetCode;
        public readonly int AttributeCode;

        public AutoChessAttributeRow(int attributeSetCode, int attributeCode)
        {
            AttributeSetCode = attributeSetCode;
            AttributeCode = attributeCode;
        }
    }

    internal sealed class AutoChessLubanJsonSource
    {
        private const string AbilityJson = "exgas_tbability.json";
        private const string GameplayEffectJson = "exgas_tbgameplayeffect.json";
        private const string AttributeSetJson = "exgas_tbattributeset.json";

        private readonly string m_root;

        public AutoChessLubanJsonSource(GasCodeGenContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            m_root = Path.Combine(context.ProjectRoot, context.Settings.LubanDataOutputPath);
        }

        public AutoChessAbilityRow[] CreateAbilityRows()
        {
            return ReadArray(AbilityJson)
                .Select(row => new AutoChessAbilityRow(
                    Int(row["ID"]),
                    FirstPositive(row["AbilityExecution"]?["Param"]?["IDs"])))
                .Where(row => row.AbilityCode > 0)
                .ToArray();
        }

        public AutoChessGameplayEffectRow[] CreateGameplayEffectRows()
        {
            return ReadArray(GameplayEffectJson)
                .Select(BuildGameplayEffectRow)
                .Where(row => row.GameplayEffectCode > 0)
                .ToArray();
        }

        public AutoChessAttributeRow[] CreateAttributeRows()
        {
            var rows = new List<AutoChessAttributeRow>();
            foreach (var attrSet in ReadArray(AttributeSetJson))
            {
                var attrSetCode = Int(attrSet["ID"]);
                foreach (var attribute in Array(attrSet["Attribute"]))
                {
                    var attrCode = Int(attribute["ID"]);
                    if (attrSetCode > 0 && attrCode > 0)
                        rows.Add(new AutoChessAttributeRow(attrSetCode, attrCode));
                }
            }

            return rows.ToArray();
        }

        private AutoChessGameplayEffectRow BuildGameplayEffectRow(JToken row)
        {
            var modifier = Array(row["Modifiers"]).FirstOrDefault();
            return new AutoChessGameplayEffectRow(
                Int(row["ID"]),
                String(row["Name"]),
                Int(modifier?["AttrSet"]),
                Int(modifier?["Attribute"]),
                FirstPositive(row["CueOnApply"]));
        }

        private JArray ReadArray(string fileName)
        {
            var path = Path.Combine(m_root, fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException($"[AutoChessDemoConfig] Luban JSON table is missing: {path}", path);

            var token = JToken.Parse(File.ReadAllText(path));
            if (token is JArray array)
                return array;

            throw new InvalidDataException($"[AutoChessDemoConfig] Luban JSON table root must be an array: {path}");
        }

        private static JArray Array(JToken token)
        {
            return token as JArray ?? new JArray();
        }

        private static int FirstPositive(JToken token)
        {
            if (token is not JArray array)
                return 0;

            foreach (var item in array)
            {
                var value = Int(item);
                if (value > 0)
                    return value;
            }

            return 0;
        }

        private static int Int(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? 0 : token.Value<int>();
        }

        private static string String(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? string.Empty : token.Value<string>() ?? string.Empty;
        }
    }

    internal sealed class AutoChessSourceGenConfig
    {
        private const string SourceRelativePath = "Datas/AutoChessDemo/autochess.sourcegen.json";
        private const string AutoCueRequests = "Auto";

        private readonly JObject m_root;

        private AutoChessSourceGenConfig(JObject root)
        {
            m_root = root ?? throw new ArgumentNullException(nameof(root));
            ExecutionCalculationExecuteDamage = RequiredInt("executionCalculations.executeDamage");
            ExecutionCalculationExecuteDamageOutput = RequiredInt("executionCalculations.executeDamageOutput");
            TagAttackCooldown = RequiredInt("tags.attackCooldown");
            if (ExecutionCalculationExecuteDamage <= 0
                || ExecutionCalculationExecuteDamageOutput <= 0
                || TagAttackCooldown <= 0)
            {
                throw new InvalidDataException("[AutoChessSourceGenConfig] execution calculation and tag codes must be positive.");
            }
        }

        public int ExecutionCalculationExecuteDamage { get; }

        public int ExecutionCalculationExecuteDamageOutput { get; }

        public int TagAttackCooldown { get; }

        public static AutoChessSourceGenConfig Load(GasCodeGenContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var path = Path.Combine(context.ProjectRoot, context.Settings.ConfigProjectPath, SourceRelativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException($"[AutoChessSourceGenConfig] source config is missing: {path}", path);

            var token = JToken.Parse(File.ReadAllText(path));
            if (token is not JObject root)
                throw new InvalidDataException($"[AutoChessSourceGenConfig] source config root must be an object: {path}");

            return new AutoChessSourceGenConfig(root);
        }

        public AutoChessDemoScenarioModel CreateScenario(bool hasGameplayCue)
        {
            var scenario = RequiredObject("validationScenario");
            return new AutoChessDemoScenarioModel
            {
                Scale = RequiredInt(scenario, "scale"),
                MaxTicks = RequiredInt(scenario, "maxTicks"),
                PostVictoryFlushTicks = RequiredInt(scenario, "postVictoryFlushTicks"),
                ProcessWarmupRuns = RequiredInt(scenario, "processWarmupRuns"),
                HealthMultiplier = RequiredFloat(scenario, "healthMultiplier"),
                ExpectedWinner = RequiredString(scenario, "expectedWinner"),
                MinDriverIssuedCommands = RequiredInt(scenario, "minDriverIssuedCommands"),
                MinAttributeChanges = RequiredInt(scenario, "minAttributeChanges"),
                MinExecutionOutputs = RequiredInt(scenario, "minExecutionOutputs"),
                MinCueRequests = ReadCueRequestExpectation(scenario, hasGameplayCue),
            };
        }

        public AutoChessDemoUnitModel[] CreateUnits(AutoChessDemoConfigModel model)
        {
            var units = RequiredArray("units");
            if (units.Count == 0)
                throw new InvalidDataException("[AutoChessSourceGenConfig] units must contain at least one unit row.");

            return units
                .Select(row => row as JObject ?? throw new InvalidDataException("[AutoChessSourceGenConfig] unit row must be an object."))
                .Select(row => new AutoChessDemoUnitModel(
                    RequiredString(row, "id"),
                    RequiredString(row, "displayName"),
                    RequiredString(row, "archetypeName"),
                    RequiredString(row, "team"),
                    RequiredInt(row, "slot"),
                    RequiredFloat(row, "health"),
                    RequiredFloat(row, "energy"),
                    ResolveAbility(model, RequiredString(row, "primaryAbility")),
                    ResolveAbility(model, RequiredString(row, "finisherAbility")),
                    RequiredFloat(row, "finisherHealthThreshold"),
                    RequiredString(row, "primaryTargetPolicy"),
                    RequiredString(row, "finisherTargetPolicy")))
                .ToArray();
        }

        private static int ResolveAbility(AutoChessDemoConfigModel model, string key)
        {
            return key switch
            {
                "None" => 0,
                "AbilityPlayerAttack" => model.AbilityPlayerAttack,
                "AbilityEnemyAttack" => model.AbilityEnemyAttack,
                "AbilityPlayerExecute" => model.AbilityPlayerExecute,
                _ => throw new InvalidDataException($"[AutoChessSourceGenConfig] unknown ability reference: {key}"),
            };
        }

        private static int ReadCueRequestExpectation(JObject scenario, bool hasGameplayCue)
        {
            var token = scenario["minCueRequests"];
            if (token == null || token.Type == JTokenType.Null)
                throw new InvalidDataException("[AutoChessSourceGenConfig] validationScenario.minCueRequests is required.");

            if (token.Type == JTokenType.String
                && string.Equals(token.Value<string>(), AutoCueRequests, StringComparison.Ordinal))
            {
                return hasGameplayCue ? 1 : 0;
            }

            return token.Value<int>();
        }

        private int RequiredInt(string path)
        {
            return RequiredToken(path).Value<int>();
        }

        private JObject RequiredObject(string path)
        {
            if (RequiredToken(path) is JObject obj)
                return obj;

            throw new InvalidDataException($"[AutoChessSourceGenConfig] required object is invalid: {path}");
        }

        private JArray RequiredArray(string path)
        {
            if (RequiredToken(path) is JArray array)
                return array;

            throw new InvalidDataException($"[AutoChessSourceGenConfig] required array is invalid: {path}");
        }

        private JToken RequiredToken(string path)
        {
            var token = m_root.SelectToken(path);
            if (token == null || token.Type == JTokenType.Null)
                throw new InvalidDataException($"[AutoChessSourceGenConfig] required value is missing: {path}");

            return token;
        }

        private static int RequiredInt(JObject obj, string key)
        {
            var token = RequiredToken(obj, key);
            return token.Value<int>();
        }

        private static float RequiredFloat(JObject obj, string key)
        {
            var token = RequiredToken(obj, key);
            return token.Value<float>();
        }

        private static string RequiredString(JObject obj, string key)
        {
            var token = RequiredToken(obj, key);
            var value = token.Value<string>();
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException($"[AutoChessSourceGenConfig] required string is empty: {key}");

            return value;
        }

        private static JToken RequiredToken(JObject obj, string key)
        {
            var token = obj[key];
            if (token == null || token.Type == JTokenType.Null)
                throw new InvalidDataException($"[AutoChessSourceGenConfig] required value is missing: {key}");

            return token;
        }
    }
}
