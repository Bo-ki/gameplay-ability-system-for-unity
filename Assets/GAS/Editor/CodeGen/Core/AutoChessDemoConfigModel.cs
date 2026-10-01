using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GAS.Editor
{
    /// <summary>
    /// 汇总 AutoChess 的 Luban 权威玩法数据与独立场景数据，供单次代码生成使用。
    /// </summary>
    internal sealed class AutoChessDemoConfigModel
    {
        private AutoChessDemoConfigModel()
        {
        }

        public int AttributeSetCombat { get; private set; }
        public int AttributeHealth { get; private set; }
        public int AttributeEnergy { get; private set; }
        public int AttributeAttack { get; private set; }
        public int AbilityPlayerAttack { get; private set; }
        public int AbilityEnemyAttack { get; private set; }
        public int AbilityPlayerExecute { get; private set; }
        public int AbilityPlayerPoison { get; private set; }
        public int GameplayEffectPlayerAttackDamage { get; private set; }
        public int GameplayEffectEnemyAttackDamage { get; private set; }
        public int GameplayEffectPlayerExecute { get; private set; }
        public int GameplayEffectPlayerPoison { get; private set; }
        public AutoChessDemoScenarioModel ValidationScenario { get; private set; }
        public IReadOnlyList<AutoChessDemoUnitModel> BaseUnits { get; private set; }
        public AutoChessRuntimeV1CatalogModel RuntimeCatalog { get; private set; }

        /// <summary>
        /// 从最新 Luban JSON 构造模型，并在生成前拒绝 Runtime v1 无法唯一表达的配置。
        /// </summary>
        public static AutoChessDemoConfigModel FromLubanJson(GasCodeGenContext context)
        {
#if UNITY_EDITOR
            var rawValidation = RuntimeV1RawAuthoringSupportProfileAdapter.Validate(context);
            if (!rawValidation.Succeeded)
            {
                throw new InvalidDataException(
                    $"[{rawValidation.RuleId}] Raw authoring rejected: {rawValidation.Error} "
                    + $"({rawValidation.DefinitionId}:{rawValidation.ElementIndex}).");
            }
#endif
            var luban = new AutoChessLubanJsonSource(context);
            var scenario = AutoChessSourceGenConfig.Load(context);
            var abilities = luban.CreateAbilityRows();
            var effects = luban.CreateGameplayEffectRows();
            var attributes = luban.CreateAttributeRows();
            var playerAttack = RequiredEffect(effects, "AutoChessPlayerAttackDamage");
            var enemyAttack = RequiredEffect(effects, "AutoChessEnemyAttackDamage");
            var playerExecute = RequiredEffect(effects, "AutoChessPlayerExecute");
            var playerPoison = RequiredEffect(effects, "AutoChessPlayerPoison");
            var healthModifier = playerAttack.Modifiers.FirstOrDefault();
            if (healthModifier.AttributeSetCode <= 0 || healthModifier.AttributeCode <= 0)
                throw new InvalidDataException("[AutoChessDemoConfig] Player attack must target a Luban attribute.");

            var combatAttributes = attributes
                .Where(row => row.AttributeSetCode == healthModifier.AttributeSetCode)
                .OrderBy(row => row.AttributeCode)
                .ToArray();
            var health = combatAttributes.SingleOrDefault(row => row.DomainRole == 1);
            if (health.AttributeCode != healthModifier.AttributeCode)
                throw new InvalidDataException("[AutoChessDemoConfig] Player attack must target the unique Health domain attribute.");

            var attack = combatAttributes.SingleOrDefault(
                row => row.AttributeCode == playerPoison.Evaluator.AttributeCode);
            if (attack.AttributeCode <= 0 || attack.AttributeCode == health.AttributeCode)
                throw new InvalidDataException("[AutoChessDemoConfig] AutoChess Attack attribute is missing.");

            var resources = combatAttributes
                .Where(row => row.AttributeCode != health.AttributeCode && row.AttributeCode != attack.AttributeCode)
                .ToArray();
            if (resources.Length != 1)
                throw new InvalidDataException("[AutoChessDemoConfig] AutoChess combat resource attribute must be unique.");
            var energy = resources[0];

            var selectedAbilities = new[]
            {
                RequiredAbility(abilities, playerAttack.GameplayEffectCode),
                RequiredAbility(abilities, enemyAttack.GameplayEffectCode),
                RequiredAbility(abilities, playerExecute.GameplayEffectCode),
                RequiredAbility(abilities, playerPoison.GameplayEffectCode),
            };
            var rootEffectCodes = new[]
            {
                playerAttack.GameplayEffectCode,
                enemyAttack.GameplayEffectCode,
                playerExecute.GameplayEffectCode,
                playerPoison.GameplayEffectCode,
            };
            var model = new AutoChessDemoConfigModel
            {
                AttributeSetCombat = healthModifier.AttributeSetCode,
                AttributeHealth = health.AttributeCode,
                AttributeEnergy = energy.AttributeCode,
                AttributeAttack = attack.AttributeCode,
                AbilityPlayerAttack = selectedAbilities[0].AbilityCode,
                AbilityEnemyAttack = selectedAbilities[1].AbilityCode,
                AbilityPlayerExecute = selectedAbilities[2].AbilityCode,
                AbilityPlayerPoison = selectedAbilities[3].AbilityCode,
                GameplayEffectPlayerAttackDamage = playerAttack.GameplayEffectCode,
                GameplayEffectEnemyAttackDamage = enemyAttack.GameplayEffectCode,
                GameplayEffectPlayerExecute = playerExecute.GameplayEffectCode,
                GameplayEffectPlayerPoison = playerPoison.GameplayEffectCode,
                RuntimeCatalog = AutoChessRuntimeV1CatalogModel.Create(
                    combatAttributes,
                    selectedAbilities,
                    effects,
                    rootEffectCodes),
            };
            model.ValidationScenario = scenario.CreateScenario(
                model.RuntimeCatalog.Effects.Any(effect => effect.HasGameplayCue));
            model.BaseUnits = scenario.CreateUnits(model);
            return model;
        }

        /// <summary>
        /// 按稳定名称读取必需 GameplayEffect，缺失时立即中止生成。
        /// </summary>
        private static AutoChessGameplayEffectRow RequiredEffect(
            IReadOnlyList<AutoChessGameplayEffectRow> rows,
            string name)
        {
            var row = rows.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
            if (row.GameplayEffectCode <= 0)
                throw new InvalidDataException($"[AutoChessDemoConfig] Missing Luban gameplay effect row: {name}");
            return row;
        }

        /// <summary>
        /// 按唯一主效果解析 Ability，避免同一效果存在多个隐式入口。
        /// </summary>
        private static AutoChessAbilityRow RequiredAbility(
            IReadOnlyList<AutoChessAbilityRow> rows,
            int primaryGameplayEffectCode)
        {
            var matches = rows.Where(item => item.PrimaryGameplayEffectCode == primaryGameplayEffectCode).ToArray();
            if (matches.Length != 1)
                throw new InvalidDataException($"[AutoChessDemoConfig] GE {primaryGameplayEffectCode} must map to exactly one ability.");
            return matches[0];
        }
    }

    /// <summary>
    /// 保存 AutoChess 验证场景阈值；该数据不参与 Runtime Catalog 玩法语义。
    /// </summary>
    internal sealed class AutoChessDemoScenarioModel
    {
        public int Scale;
        public int MaxTicks;
        public float HealthMultiplier;
        public string ExpectedWinner;
        public int MinAcceptedCommands;
        public int MinAttributeChanges;
        public int MinExecutionOutputs;
        public int MinCueRequests;
        public int MinActiveEffectSlots;
        public int MinPeriodTickDamageFacts;
        public int MinActiveMutationCommands;
        public int MinActiveMutationOwnerGroups;
        public int MaxActiveMutationEstimatedRandomLookups;
        public int MaxActiveMutationOwnerResourceLookups;
        public int MaxActiveMutationMigrationCarriers;
    }

    /// <summary>
    /// 保存一个由场景配置引用的 AutoChess 单位模板。
    /// </summary>
    internal readonly struct AutoChessDemoUnitModel
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string ArchetypeName;
        public readonly string Team;
        public readonly int Slot;
        public readonly float Health;
        public readonly float Energy;
        public readonly float Attack;
        public readonly int PrimaryAbilityCode;
        public readonly int FinisherAbilityCode;
        public readonly int ActiveAbilityCode;
        public readonly int ActiveCastInterval;
        public readonly int ActiveCastFrameOffset;
        public readonly float FinisherHealthThreshold;
        public readonly string PrimaryTargetPolicy;
        public readonly string FinisherTargetPolicy;

        /// <summary>
        /// 创建一个不参与 Catalog 编译的场景单位模板。
        /// </summary>
        public AutoChessDemoUnitModel(
            string id,
            string displayName,
            string archetypeName,
            string team,
            int slot,
            float health,
            float energy,
            float attack,
            int primaryAbilityCode,
            int finisherAbilityCode,
            int activeAbilityCode,
            int activeCastInterval,
            int activeCastFrameOffset,
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
            Attack = attack;
            PrimaryAbilityCode = primaryAbilityCode;
            FinisherAbilityCode = finisherAbilityCode;
            ActiveAbilityCode = activeAbilityCode;
            ActiveCastInterval = activeCastInterval;
            ActiveCastFrameOffset = activeCastFrameOffset;
            FinisherHealthThreshold = finisherHealthThreshold;
            PrimaryTargetPolicy = primaryTargetPolicy;
            FinisherTargetPolicy = finisherTargetPolicy;
        }
    }

    /// <summary>
    /// 保存 Luban Ability 中 Runtime v1 Catalog 实际消费的字段。
    /// </summary>
    internal readonly struct AutoChessAbilityRow
    {
        public readonly int AbilityCode;
        public readonly int PrimaryGameplayEffectCode;
        public readonly int CostCode;
        public readonly int CooldownEffectCode;
        public readonly int CooldownTicks;
        public readonly AutoChessTargetPolicyRow TargetPolicy;
        public readonly int UnsupportedSemanticCount;

        /// <summary>
        /// 创建一条 Ability 权威输入记录。
        /// </summary>
        public AutoChessAbilityRow(
            int abilityCode,
            int primaryGameplayEffectCode,
            int costCode,
            int cooldownEffectCode,
            int cooldownTicks,
            AutoChessTargetPolicyRow targetPolicy,
            int unsupportedSemanticCount)
        {
            AbilityCode = abilityCode;
            PrimaryGameplayEffectCode = primaryGameplayEffectCode;
            CostCode = costCode;
            CooldownEffectCode = cooldownEffectCode;
            CooldownTicks = cooldownTicks;
            TargetPolicy = targetPolicy;
            UnsupportedSemanticCount = unsupportedSemanticCount;
        }
    }

    /// <summary>
    /// 保存 Luban Modifier 的属性、幅值与运算类型。
    /// </summary>
    internal readonly struct AutoChessModifierRow
    {
        public readonly int AttributeSetCode;
        public readonly int AttributeCode;
        public readonly float Magnitude;
        public readonly int Operation;

        /// <summary>
        /// 创建一条 Modifier 权威输入记录。
        /// </summary>
        public AutoChessModifierRow(int attributeSetCode, int attributeCode, float magnitude, int operation)
        {
            AttributeSetCode = attributeSetCode;
            AttributeCode = attributeCode;
            Magnitude = magnitude;
            Operation = operation;
        }
    }

    /// <summary>
    /// 保存 Runtime v1 四维目标策略的 Luban 数值。
    /// </summary>
    internal readonly struct AutoChessTargetPolicyRow
    {
        public readonly int LogicalTarget;
        public readonly int Avatar;
        public readonly int Spatial;
        public readonly int Life;

        public bool IsDefined => LogicalTarget > 0 && Avatar > 0 && Spatial > 0 && Life > 0;

        /// <summary>
        /// 创建一条四维目标策略记录。
        /// </summary>
        public AutoChessTargetPolicyRow(int logicalTarget, int avatar, int spatial, int life)
        {
            LogicalTarget = logicalTarget;
            Avatar = avatar;
            Spatial = spatial;
            Life = life;
        }
    }

    /// <summary>
    /// 保存 Luban GameplayEffect 的 duration 字段。
    /// </summary>
    internal readonly struct AutoChessDurationRow
    {
        public readonly bool IsDefined;
        public readonly int TimeUnit;
        public readonly int Time;
        public readonly bool ResetStartTimeWhenActivated;

        /// <summary>
        /// 创建一条 duration 输入记录。
        /// </summary>
        public AutoChessDurationRow(bool isDefined, int timeUnit, int time, bool resetStartTimeWhenActivated)
        {
            IsDefined = isDefined;
            TimeUnit = timeUnit;
            Time = time;
            ResetStartTimeWhenActivated = resetStartTimeWhenActivated;
        }
    }

    /// <summary>
    /// 保存 Luban GameplayEffect 的 period 与子效果引用。
    /// </summary>
    internal readonly struct AutoChessPeriodRow
    {
        public readonly bool IsDefined;
        public readonly int Time;
        public readonly int[] EffectCodes;
        public readonly bool FirstTrigger;

        /// <summary>
        /// 创建一条 period 输入记录。
        /// </summary>
        public AutoChessPeriodRow(bool isDefined, int time, int[] effectCodes, bool firstTrigger)
        {
            IsDefined = isDefined;
            Time = time;
            EffectCodes = effectCodes ?? Array.Empty<int>();
            FirstTrigger = firstTrigger;
        }
    }

    /// <summary>
    /// 保存 Luban GameplayEffect 的 stacking 策略。
    /// </summary>
    internal readonly struct AutoChessStackingRow
    {
        public readonly bool IsDefined;
        public readonly int StackingType;
        public readonly int StackCode;
        public readonly int LimitCount;
        public readonly int DurationRefreshPolicy;
        public readonly int PeriodResetPolicy;
        public readonly int ExpirationPolicy;
        public readonly bool DenyOverflowApplication;
        public readonly bool ClearStackOnOverflow;
        public readonly int OverflowEffectCount;

        /// <summary>
        /// 创建一条 stacking 输入记录。
        /// </summary>
        public AutoChessStackingRow(
            bool isDefined,
            int stackingType,
            int stackCode,
            int limitCount,
            int durationRefreshPolicy,
            int periodResetPolicy,
            int expirationPolicy,
            bool denyOverflowApplication,
            bool clearStackOnOverflow,
            int overflowEffectCount)
        {
            IsDefined = isDefined;
            StackingType = stackingType;
            StackCode = stackCode;
            LimitCount = limitCount;
            DurationRefreshPolicy = durationRefreshPolicy;
            PeriodResetPolicy = periodResetPolicy;
            ExpirationPolicy = expirationPolicy;
            DenyOverflowApplication = denyOverflowApplication;
            ClearStackOnOverflow = clearStackOnOverflow;
            OverflowEffectCount = overflowEffectCount;
        }
    }

    /// <summary>
    /// 保存 Luban Runtime v1 evaluator 的闭集公式参数。
    /// </summary>
    internal readonly struct AutoChessEvaluatorRow
    {
        public readonly int Kind;
        public readonly int AttributeSetCode;
        public readonly int AttributeCode;
        public readonly float BaseValue;
        public readonly float Coefficient;
        public readonly float Minimum;
        public readonly float Maximum;

        public bool IsDefined => Kind > 0;

        /// <summary>
        /// 创建一条 evaluator 输入记录。
        /// </summary>
        public AutoChessEvaluatorRow(
            int kind,
            int attributeSetCode,
            int attributeCode,
            float baseValue,
            float coefficient,
            float minimum,
            float maximum)
        {
            Kind = kind;
            AttributeSetCode = attributeSetCode;
            AttributeCode = attributeCode;
            BaseValue = baseValue;
            Coefficient = coefficient;
            Minimum = minimum;
            Maximum = maximum;
        }
    }

    /// <summary>
    /// 保存 Luban GameplayEffect 中 Runtime v1 Catalog 所需的完整输入。
    /// </summary>
    internal readonly struct AutoChessGameplayEffectRow
    {
        public readonly int GameplayEffectCode;
        public readonly string Name;
        public readonly AutoChessModifierRow[] Modifiers;
        public readonly int[] CueOnApply;
        public readonly int[] CueOnTick;
        public readonly int[] CueOnAdd;
        public readonly int[] CueOnRemove;
        public readonly int[] CueOnActivate;
        public readonly int[] CueOnDeactivate;
        public readonly AutoChessDurationRow Duration;
        public readonly AutoChessPeriodRow Period;
        public readonly AutoChessStackingRow Stacking;
        public readonly AutoChessTargetPolicyRow TargetPolicy;
        public readonly AutoChessEvaluatorRow Evaluator;
        public readonly int UnsupportedSemanticCount;

        public bool HasGameplayCue => CueOnApply.Length + CueOnTick.Length + CueOnAdd.Length
            + CueOnRemove.Length + CueOnActivate.Length + CueOnDeactivate.Length > 0;

        /// <summary>
        /// 创建一条 GameplayEffect 权威输入记录。
        /// </summary>
        public AutoChessGameplayEffectRow(
            int gameplayEffectCode,
            string name,
            AutoChessModifierRow[] modifiers,
            int[] cueOnApply,
            int[] cueOnTick,
            int[] cueOnAdd,
            int[] cueOnRemove,
            int[] cueOnActivate,
            int[] cueOnDeactivate,
            AutoChessDurationRow duration,
            AutoChessPeriodRow period,
            AutoChessStackingRow stacking,
            AutoChessTargetPolicyRow targetPolicy,
            AutoChessEvaluatorRow evaluator,
            int unsupportedSemanticCount)
        {
            GameplayEffectCode = gameplayEffectCode;
            Name = name ?? string.Empty;
            Modifiers = modifiers ?? Array.Empty<AutoChessModifierRow>();
            CueOnApply = cueOnApply ?? Array.Empty<int>();
            CueOnTick = cueOnTick ?? Array.Empty<int>();
            CueOnAdd = cueOnAdd ?? Array.Empty<int>();
            CueOnRemove = cueOnRemove ?? Array.Empty<int>();
            CueOnActivate = cueOnActivate ?? Array.Empty<int>();
            CueOnDeactivate = cueOnDeactivate ?? Array.Empty<int>();
            Duration = duration;
            Period = period;
            Stacking = stacking;
            TargetPolicy = targetPolicy;
            Evaluator = evaluator;
            UnsupportedSemanticCount = unsupportedSemanticCount;
        }
    }

    /// <summary>
    /// 保存 Luban AttributeSet 中一个 Runtime v1 dense layout 条目。
    /// </summary>
    internal readonly struct AutoChessAttributeRow
    {
        public readonly int AttributeSetCode;
        public readonly int AttributeCode;
        public readonly float InitialValue;
        public readonly float MinimumValue;
        public readonly float MaximumValue;
        public readonly bool ClampMinimum;
        public readonly bool ClampMaximum;
        public readonly int DomainRole;

        /// <summary>
        /// 创建一条 Attribute layout 权威输入记录。
        /// </summary>
        public AutoChessAttributeRow(
            int attributeSetCode,
            int attributeCode,
            float initialValue,
            float minimumValue,
            float maximumValue,
            bool clampMinimum,
            bool clampMaximum,
            int domainRole)
        {
            AttributeSetCode = attributeSetCode;
            AttributeCode = attributeCode;
            InitialValue = initialValue;
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
            ClampMinimum = clampMinimum;
            ClampMaximum = clampMaximum;
            DomainRole = domainRole;
        }
    }

    /// <summary>
    /// 保存经过闭世界校验且按稳定 ID 排序的 AutoChess Runtime v1 Catalog 输入。
    /// </summary>
    internal sealed class AutoChessRuntimeV1CatalogModel
    {
        private AutoChessRuntimeV1CatalogModel(
            AutoChessAttributeRow[] attributes,
            AutoChessAbilityRow[] abilities,
            AutoChessGameplayEffectRow[] effects)
        {
            Attributes = attributes;
            Abilities = abilities;
            Effects = effects;
        }

        public AutoChessAttributeRow[] Attributes { get; }
        public AutoChessAbilityRow[] Abilities { get; }
        public AutoChessGameplayEffectRow[] Effects { get; }

        /// <summary>
        /// 解析 root Effect 及 period 依赖闭包，并执行 bake-fail 语义校验。
        /// </summary>
        public static AutoChessRuntimeV1CatalogModel Create(
            AutoChessAttributeRow[] attributes,
            AutoChessAbilityRow[] abilities,
            AutoChessGameplayEffectRow[] allEffects,
            int[] rootEffectCodes)
        {
            var orderedAttributes = (attributes ?? Array.Empty<AutoChessAttributeRow>())
                .OrderBy(row => row.AttributeCode)
                .ToArray();
            var orderedAbilities = (abilities ?? Array.Empty<AutoChessAbilityRow>())
                .OrderBy(row => row.AbilityCode)
                .ToArray();
            var effectsById = (allEffects ?? Array.Empty<AutoChessGameplayEffectRow>())
                .ToDictionary(row => row.GameplayEffectCode);
            var requiredIds = ResolveEffectClosure(effectsById, rootEffectCodes);
            var orderedEffects = requiredIds
                .Select(id => effectsById[id])
                .OrderBy(row => row.GameplayEffectCode)
                .ToArray();
            ValidateAttributes(orderedAttributes);
            ValidateAbilities(orderedAbilities, rootEffectCodes);
            ValidateEffects(orderedEffects, orderedAttributes);
            return new AutoChessRuntimeV1CatalogModel(
                orderedAttributes,
                orderedAbilities,
                orderedEffects);
        }

        /// <summary>
        /// 展开所有 period child 引用，确保 Catalog 不依赖运行时外部查询。
        /// </summary>
        private static HashSet<int> ResolveEffectClosure(
            IReadOnlyDictionary<int, AutoChessGameplayEffectRow> effectsById,
            IEnumerable<int> rootEffectCodes)
        {
            var required = new HashSet<int>(rootEffectCodes ?? Array.Empty<int>());
            var pending = new Queue<int>(required.OrderBy(value => value));
            while (pending.Count > 0)
            {
                var id = pending.Dequeue();
                if (!effectsById.TryGetValue(id, out var effect))
                    throw new InvalidDataException($"[AutoChessCatalog] Missing GameplayEffect dependency: {id}");
                foreach (var childId in effect.Period.EffectCodes)
                {
                    if (childId <= 0 || !required.Add(childId))
                        continue;
                    pending.Enqueue(childId);
                }
            }
            return required;
        }

        /// <summary>
        /// 校验 dense Attribute layout、clamp 区间与唯一 Health 领域角色。
        /// </summary>
        private static void ValidateAttributes(IReadOnlyList<AutoChessAttributeRow> attributes)
        {
            if (attributes.Count == 0 || attributes.Select(row => row.AttributeCode).Distinct().Count() != attributes.Count)
                throw new InvalidDataException("[AutoChessCatalog] Attribute IDs must form a non-empty unique layout.");
            if (attributes.Count(row => row.DomainRole == 1) != 1)
                throw new InvalidDataException("[AutoChessCatalog] Exactly one Health domain attribute is required.");
            foreach (var attribute in attributes)
            {
                if (attribute.AttributeCode <= 0 || attribute.DomainRole < 0 || attribute.DomainRole > 1)
                    throw new InvalidDataException($"[AutoChessCatalog] Attribute {attribute.AttributeCode} metadata is invalid.");
                if (attribute.ClampMinimum && attribute.ClampMaximum && attribute.MaximumValue < attribute.MinimumValue)
                    throw new InvalidDataException($"[AutoChessCatalog] Attribute {attribute.AttributeCode} clamp range is inverted.");
            }
        }

        /// <summary>
        /// 校验 Ability 只包含 Runtime v1 当前可无损表达的直接效果与目标策略。
        /// </summary>
        private static void ValidateAbilities(
            IReadOnlyList<AutoChessAbilityRow> abilities,
            IReadOnlyCollection<int> rootEffectCodes)
        {
            if (abilities.Count == 0 || abilities.Select(row => row.AbilityCode).Distinct().Count() != abilities.Count)
                throw new InvalidDataException("[AutoChessCatalog] Ability IDs must be non-empty and unique.");
            foreach (var ability in abilities)
            {
                if (ability.AbilityCode <= 0 || !rootEffectCodes.Contains(ability.PrimaryGameplayEffectCode))
                    throw new InvalidDataException($"[AutoChessCatalog] Ability {ability.AbilityCode} primary effect is invalid.");
                if (ability.CostCode != 0 || ability.CooldownEffectCode != 0 || ability.CooldownTicks != 0)
                    throw new InvalidDataException($"[AutoChessCatalog] Ability {ability.AbilityCode} cost/cooldown cannot be losslessly compiled.");
                if (ability.UnsupportedSemanticCount != 0 || !IsValidTargetPolicy(in ability.TargetPolicy))
                    throw new InvalidDataException($"[AutoChessCatalog] Ability {ability.AbilityCode} contains unsupported semantics.");
            }
        }

        /// <summary>
        /// 校验 Effect 生命周期、stack、modifier、evaluator 与引用闭包均可唯一映射。
        /// </summary>
        private static void ValidateEffects(
            IReadOnlyList<AutoChessGameplayEffectRow> effects,
            IReadOnlyList<AutoChessAttributeRow> attributes)
        {
            var attributeIds = new HashSet<int>(attributes.Select(row => row.AttributeCode));
            var attributeSetCode = attributes[0].AttributeSetCode;
            var healthAttributeId = attributes.Single(row => row.DomainRole == 1).AttributeCode;
            foreach (var effect in effects)
            {
                if (effect.UnsupportedSemanticCount != 0 || !IsValidTargetPolicy(in effect.TargetPolicy))
                    throw new InvalidDataException($"[AutoChessCatalog] Effect {effect.GameplayEffectCode} contains unsupported semantics.");
                ValidateDurationAndStack(in effect);
                ValidateModifiers(in effect, attributeSetCode, attributeIds);
                ValidateEvaluator(in effect, attributeSetCode, healthAttributeId, attributeIds);
            }
        }

        /// <summary>
        /// 校验 duration/period/stacking 组合，拒绝隐式单位换算和溢出副作用。
        /// </summary>
        private static void ValidateDurationAndStack(in AutoChessGameplayEffectRow effect)
        {
            if (effect.Duration.IsDefined && (effect.Duration.TimeUnit != 0 || effect.Duration.Time <= 0))
                throw new InvalidDataException($"[AutoChessCatalog] Effect {effect.GameplayEffectCode} duration must use positive ticks.");
            if (effect.Period.IsDefined
                && (!effect.Duration.IsDefined || effect.Period.Time <= 0
                    || (effect.Evaluator.Kind == 2
                        ? effect.Period.EffectCodes.Length != 0
                        : effect.Period.EffectCodes.Length == 0)))
            {
                throw new InvalidDataException($"[AutoChessCatalog] Effect {effect.GameplayEffectCode} period contract is incomplete.");
            }
            if (!effect.Stacking.IsDefined)
                return;
            var stacking = effect.Stacking;
            if (!effect.Duration.IsDefined || stacking.StackingType < 0 || stacking.StackingType > 1
                || stacking.StackCode != 0 || stacking.LimitCount <= 0
                || stacking.DurationRefreshPolicy < 0 || stacking.DurationRefreshPolicy > 1
                || stacking.PeriodResetPolicy < 0 || stacking.PeriodResetPolicy > 1
                || stacking.ExpirationPolicy < 0 || stacking.ExpirationPolicy > 1
                || stacking.ClearStackOnOverflow || stacking.OverflowEffectCount != 0)
            {
                throw new InvalidDataException($"[AutoChessCatalog] Effect {effect.GameplayEffectCode} stacking cannot be uniquely compiled.");
            }
        }

        /// <summary>
        /// 校验所有 modifier 均引用当前 AttributeSet，并使用闭集运算。
        /// </summary>
        private static void ValidateModifiers(
            in AutoChessGameplayEffectRow effect,
            int attributeSetCode,
            HashSet<int> attributeIds)
        {
            foreach (var modifier in effect.Modifiers)
            {
                if (modifier.AttributeSetCode != attributeSetCode
                    || !attributeIds.Contains(modifier.AttributeCode)
                    || modifier.Operation < 0
                    || modifier.Operation > 4)
                {
                    throw new InvalidDataException($"[AutoChessCatalog] Effect {effect.GameplayEffectCode} modifier is unsupported.");
                }
            }
        }

        /// <summary>
        /// 校验 evaluator 落在 TargetMissingHealth 或 SourceAttackStackCount 闭集内。
        /// </summary>
        private static void ValidateEvaluator(
            in AutoChessGameplayEffectRow effect,
            int attributeSetCode,
            int healthAttributeId,
            HashSet<int> attributeIds)
        {
            if (!effect.Evaluator.IsDefined)
                return;
            var evaluator = effect.Evaluator;
            if (evaluator.AttributeSetCode != attributeSetCode
                || !attributeIds.Contains(evaluator.AttributeCode)
                || !IsFinite(evaluator.BaseValue)
                || !IsFinite(evaluator.Coefficient)
                || !IsFinite(evaluator.Minimum)
                || !IsFinite(evaluator.Maximum))
            {
                throw new InvalidDataException($"[AutoChessCatalog] Effect {effect.GameplayEffectCode} evaluator is invalid.");
            }

            if (evaluator.Kind == 1)
            {
                if (effect.Modifiers.Length != 0 || effect.Duration.IsDefined || effect.Period.IsDefined
                    || evaluator.BaseValue < 0f || evaluator.Coefficient < 0f
                    || evaluator.Minimum < 0f || evaluator.Maximum < evaluator.Minimum)
                {
                    throw new InvalidDataException($"[AutoChessCatalog] Effect {effect.GameplayEffectCode} evaluator is invalid.");
                }
                return;
            }

            if (evaluator.Kind != 2 || effect.Modifiers.Length != 1
                || effect.Modifiers[0].AttributeCode != healthAttributeId
                || evaluator.AttributeCode == healthAttributeId
                || effect.Modifiers[0].Operation != 3 || effect.Modifiers[0].Magnitude != 0f
                || !effect.Duration.IsDefined || !effect.Period.IsDefined || !effect.Stacking.IsDefined
                || evaluator.BaseValue != 0f || evaluator.Coefficient <= 0f
                || evaluator.Minimum != 0f || evaluator.Maximum != 0f)
            {
                throw new InvalidDataException($"[AutoChessCatalog] Effect {effect.GameplayEffectCode} evaluator is invalid.");
            }
        }

        /// <summary>
        /// 判断浮点 authoring 值能否安全进入 generated literal。
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// 校验四维目标策略值均落在 Runtime v1 enum 闭集内。
        /// </summary>
        private static bool IsValidTargetPolicy(in AutoChessTargetPolicyRow policy)
        {
            return policy.IsDefined
                && policy.LogicalTarget <= 2
                && policy.Avatar <= 2
                && policy.Spatial <= 2
                && policy.Life <= 3;
        }
    }

    /// <summary>
    /// 直接读取 Luban JSON 表，不经过 normalized C# 或 AppDomain 扫描中间态。
    /// </summary>
    internal sealed class AutoChessLubanJsonSource
    {
        private const string AbilityJson = "exgas_tbability.json";
        private const string GameplayEffectJson = "exgas_tbgameplayeffect.json";
        private const string AttributeSetJson = "exgas_tbattributeset.json";
        private readonly string m_root;

        /// <summary>
        /// 绑定本轮代码生成候选目录中的 Luban JSON 根路径。
        /// </summary>
        public AutoChessLubanJsonSource(GasCodeGenContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            m_root = Path.Combine(context.ProjectRoot, context.Settings.LubanDataOutputPath);
        }

        /// <summary>
        /// 读取全部 Ability 权威行。
        /// </summary>
        public AutoChessAbilityRow[] CreateAbilityRows()
        {
            return ReadArray(AbilityJson)
                .Select(row => new AutoChessAbilityRow(
                    Int(row["ID"]),
                    FirstPositive(row["AbilityExecution"]?["Param"]?["IDs"]),
                    Int(row["Cost"]),
                    Int(row["CdEffect"]),
                    Int(row["Cd"]),
                    TargetPolicy(row["RuntimeV1TargetPolicy"]),
                    Count(row["AssetTags"]) + Count(row["ActivationOwnedTags"])))
                .Where(row => row.AbilityCode > 0)
                .ToArray();
        }

        /// <summary>
        /// 读取全部 GameplayEffect 权威行。
        /// </summary>
        public AutoChessGameplayEffectRow[] CreateGameplayEffectRows()
        {
            return ReadArray(GameplayEffectJson)
                .Select(BuildGameplayEffectRow)
                .Where(row => row.GameplayEffectCode > 0)
                .ToArray();
        }

        /// <summary>
        /// 读取 AttributeSet 内全部 Attribute layout 行。
        /// </summary>
        public AutoChessAttributeRow[] CreateAttributeRows()
        {
            var rows = new List<AutoChessAttributeRow>();
            foreach (var attrSet in ReadArray(AttributeSetJson))
            {
                var attrSetCode = Int(attrSet["ID"]);
                foreach (var attribute in Array(attribute: attrSet["Attribute"]))
                {
                    rows.Add(new AutoChessAttributeRow(
                        attrSetCode,
                        Int(attribute["ID"]),
                        Float(attribute["InitValue"]),
                        Float(attribute["MinValue"]),
                        Float(attribute["MaxValue"]),
                        Bool(attribute["UseMinValue"]),
                        Bool(attribute["UseMaxValue"]),
                        Int(attribute["DomainRole"])));
                }
            }
            return rows.Where(row => row.AttributeSetCode > 0 && row.AttributeCode > 0).ToArray();
        }

        /// <summary>
        /// 将一个 Luban JSON 对象映射成完整 GameplayEffect 输入行。
        /// </summary>
        private static AutoChessGameplayEffectRow BuildGameplayEffectRow(JToken row)
        {
            var duration = row["Duration"];
            var period = row["Period"];
            var stacking = row["Stacking"];
            var evaluator = row["RuntimeV1Evaluator"];
            return new AutoChessGameplayEffectRow(
                Int(row["ID"]),
                String(row["Name"]),
                Array(row["Modifiers"]).Select(Modifier).ToArray(),
                IntArray(row["CueOnApply"]),
                IntArray(row["CueOnTick"]),
                IntArray(row["CueOnAdd"]),
                IntArray(row["CueOnRemove"]),
                IntArray(row["CueOnActivate"]),
                IntArray(row["CueOnDeactivate"]),
                new AutoChessDurationRow(
                    IsObject(duration),
                    Int(duration?["TimeUnit"]),
                    Int(duration?["Time"]),
                    Bool(duration?["ResetStartTimeWhenActivated"])),
                new AutoChessPeriodRow(
                    IsObject(period),
                    Int(period?["Time"]),
                    IntArray(period?["Effects"]),
                    Bool(period?["FirstTrigger"])),
                Stacking(stacking),
                TargetPolicy(row["RuntimeV1TargetPolicy"]),
                new AutoChessEvaluatorRow(
                    Int(evaluator?["Kind"]),
                    Int(evaluator?["AttributeSet"]),
                    Int(evaluator?["Attribute"]),
                    Float(evaluator?["BaseValue"]),
                    Float(evaluator?["Coefficient"]),
                    Float(evaluator?["Minimum"]),
                    Float(evaluator?["Maximum"])),
                Count(row["AssetTags"]) + Count(row["GrantedTags"]) + Count(row["GrantedAbility"]));
        }

        /// <summary>
        /// 读取一个 Modifier JSON 对象。
        /// </summary>
        private static AutoChessModifierRow Modifier(JToken token)
        {
            return new AutoChessModifierRow(
                Int(token?["AttrSet"]),
                Int(token?["Attribute"]),
                Float(token?["Magnitude"]),
                Int(token?["Operation"]));
        }

        /// <summary>
        /// 读取一个 Stacking JSON 对象；缺失时返回未定义记录。
        /// </summary>
        private static AutoChessStackingRow Stacking(JToken token)
        {
            return new AutoChessStackingRow(
                IsObject(token),
                Int(token?["StackingType"]),
                Int(token?["StackCode"]),
                Int(token?["LimitCount"]),
                Int(token?["DurationRefreshPolicy"]),
                Int(token?["PeriodResetPolicy"]),
                Int(token?["ExpirationPolicy"]),
                Bool(token?["DenyOverflowApplication"]),
                Bool(token?["ClearStackOnOverflow"]),
                Count(token?["OverflowEffects"]));
        }

        /// <summary>
        /// 读取四维 Runtime v1 target policy。
        /// </summary>
        private static AutoChessTargetPolicyRow TargetPolicy(JToken token)
        {
            return new AutoChessTargetPolicyRow(
                Int(token?["LogicalTarget"]),
                Int(token?["Avatar"]),
                Int(token?["Spatial"]),
                Int(token?["Life"]));
        }

        /// <summary>
        /// 读取并校验指定 Luban JSON 文件的数组根。
        /// </summary>
        private JArray ReadArray(string fileName)
        {
            var path = Path.Combine(m_root, fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException($"[AutoChessDemoConfig] Luban JSON table is missing: {path}", path);
            if (JToken.Parse(File.ReadAllText(path)) is JArray array)
                return array;
            throw new InvalidDataException($"[AutoChessDemoConfig] Luban JSON root must be an array: {path}");
        }

        /// <summary>
        /// 将 token 规范化为数组。
        /// </summary>
        private static JArray Array(JToken attribute)
        {
            return attribute as JArray ?? new JArray();
        }

        /// <summary>
        /// 返回数组中的正整数值。
        /// </summary>
        private static int[] IntArray(JToken token)
        {
            return Array(token).Select(Int).Where(value => value > 0).ToArray();
        }

        /// <summary>
        /// 返回数组中的首个正整数。
        /// </summary>
        private static int FirstPositive(JToken token)
        {
            return IntArray(token).FirstOrDefault();
        }

        /// <summary>
        /// 返回数组元素数量。
        /// </summary>
        private static int Count(JToken token)
        {
            return Array(token).Count;
        }

        /// <summary>
        /// 判断 token 是否为有效对象。
        /// </summary>
        private static bool IsObject(JToken token)
        {
            return token is JObject;
        }

        /// <summary>
        /// 读取可空整数。
        /// </summary>
        private static int Int(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? 0 : token.Value<int>();
        }

        /// <summary>
        /// 读取可空浮点数。
        /// </summary>
        private static float Float(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? 0f : token.Value<float>();
        }

        /// <summary>
        /// 读取可空布尔值。
        /// </summary>
        private static bool Bool(JToken token)
        {
            return token != null && token.Type != JTokenType.Null && token.Value<bool>();
        }

        /// <summary>
        /// 读取可空字符串。
        /// </summary>
        private static string String(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? string.Empty : token.Value<string>() ?? string.Empty;
        }
    }

    /// <summary>
    /// 读取 AutoChess 场景与单位数据；此 sidecar 不再允许声明任何 GameplayEffect、公式或 Tag 语义。
    /// </summary>
    internal sealed class AutoChessSourceGenConfig
    {
        private const string SourceRelativePath = "Datas/AutoChessDemo/autochess.sourcegen.json";
        private const string AutoCueRequests = "Auto";
        private readonly JObject m_root;

        private AutoChessSourceGenConfig(JObject root)
        {
            m_root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>
        /// 加载仅包含场景与单位数据的 AutoChess sidecar。
        /// </summary>
        public static AutoChessSourceGenConfig Load(GasCodeGenContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var path = Path.Combine(context.ProjectRoot, context.Settings.ConfigProjectPath, SourceRelativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException($"[AutoChessSourceGenConfig] source config is missing: {path}", path);
            if (JToken.Parse(File.ReadAllText(path)) is JObject root)
                return new AutoChessSourceGenConfig(root);
            throw new InvalidDataException($"[AutoChessSourceGenConfig] source config root must be an object: {path}");
        }

        /// <summary>
        /// 创建独立于 Catalog 的验证场景阈值。
        /// </summary>
        public AutoChessDemoScenarioModel CreateScenario(bool hasGameplayCue)
        {
            var scenario = RequiredObject("validationScenario");
            return new AutoChessDemoScenarioModel
            {
                Scale = RequiredInt(scenario, "scale"),
                MaxTicks = RequiredInt(scenario, "maxTicks"),
                HealthMultiplier = RequiredFloat(scenario, "healthMultiplier"),
                ExpectedWinner = RequiredString(scenario, "expectedWinner"),
                MinAcceptedCommands = RequiredInt(scenario, "minAcceptedCommands"),
                MinAttributeChanges = RequiredInt(scenario, "minAttributeChanges"),
                MinExecutionOutputs = RequiredInt(scenario, "minExecutionOutputs"),
                MinCueRequests = ReadCueRequestExpectation(scenario, hasGameplayCue),
                MinActiveEffectSlots = RequiredInt(scenario, "minActiveEffectSlots"),
                MinPeriodTickDamageFacts = RequiredInt(scenario, "minPeriodTickDamageFacts"),
                MinActiveMutationCommands = RequiredInt(scenario, "minActiveMutationCommands"),
                MinActiveMutationOwnerGroups = RequiredInt(scenario, "minActiveMutationOwnerGroups"),
                MaxActiveMutationEstimatedRandomLookups = RequiredInt(scenario, "maxActiveMutationEstimatedRandomLookups"),
                MaxActiveMutationOwnerResourceLookups = RequiredInt(scenario, "maxActiveMutationOwnerResourceLookups"),
                MaxActiveMutationMigrationCarriers = RequiredInt(scenario, "maxActiveMutationMigrationCarriers"),
            };
        }

        /// <summary>
        /// 创建场景单位模板并解析其对已生成 Ability ID 的引用。
        /// </summary>
        public AutoChessDemoUnitModel[] CreateUnits(AutoChessDemoConfigModel model)
        {
            var units = RequiredArray("units");
            if (units.Count == 0)
                throw new InvalidDataException("[AutoChessSourceGenConfig] units must contain at least one row.");
            return units.Cast<JObject>().Select(row => new AutoChessDemoUnitModel(
                RequiredString(row, "id"),
                RequiredString(row, "displayName"),
                RequiredString(row, "archetypeName"),
                RequiredString(row, "team"),
                RequiredInt(row, "slot"),
                RequiredFloat(row, "health"),
                RequiredFloat(row, "energy"),
                RequiredFloat(row, "attack"),
                ResolveAbility(model, RequiredString(row, "primaryAbility")),
                ResolveAbility(model, RequiredString(row, "finisherAbility")),
                ResolveAbility(model, RequiredString(row, "activeAbility")),
                RequiredInt(row, "activeCastInterval"),
                RequiredInt(row, "activeCastFrameOffset"),
                RequiredFloat(row, "finisherHealthThreshold"),
                RequiredString(row, "primaryTargetPolicy"),
                RequiredString(row, "finisherTargetPolicy"))).ToArray();
        }

        /// <summary>
        /// 将场景中的稳定别名解析为 Luban 生成的 Ability ID。
        /// </summary>
        private static int ResolveAbility(AutoChessDemoConfigModel model, string key)
        {
            return key switch
            {
                "None" => 0,
                "AbilityPlayerAttack" => model.AbilityPlayerAttack,
                "AbilityEnemyAttack" => model.AbilityEnemyAttack,
                "AbilityPlayerExecute" => model.AbilityPlayerExecute,
                "AbilityPlayerPoison" => model.AbilityPlayerPoison,
                _ => throw new InvalidDataException($"[AutoChessSourceGenConfig] unknown ability reference: {key}"),
            };
        }

        /// <summary>
        /// 根据 Luban Cue 数据解析 Auto 场景阈值。
        /// </summary>
        private static int ReadCueRequestExpectation(JObject scenario, bool hasGameplayCue)
        {
            var token = RequiredToken(scenario, "minCueRequests");
            if (token.Type == JTokenType.String
                && string.Equals(token.Value<string>(), AutoCueRequests, StringComparison.Ordinal))
            {
                return hasGameplayCue ? 1 : 0;
            }
            return token.Value<int>();
        }

        /// <summary>
        /// 读取 sidecar 中的必需对象。
        /// </summary>
        private JObject RequiredObject(string path)
        {
            if (RequiredToken(path) is JObject obj)
                return obj;
            throw new InvalidDataException($"[AutoChessSourceGenConfig] required object is invalid: {path}");
        }

        /// <summary>
        /// 读取 sidecar 中的必需数组。
        /// </summary>
        private JArray RequiredArray(string path)
        {
            if (RequiredToken(path) is JArray array)
                return array;
            throw new InvalidDataException($"[AutoChessSourceGenConfig] required array is invalid: {path}");
        }

        /// <summary>
        /// 按路径读取 sidecar 中的必需 token。
        /// </summary>
        private JToken RequiredToken(string path)
        {
            var token = m_root.SelectToken(path);
            if (token == null || token.Type == JTokenType.Null)
                throw new InvalidDataException($"[AutoChessSourceGenConfig] required value is missing: {path}");
            return token;
        }

        /// <summary>
        /// 读取对象中的必需整数。
        /// </summary>
        private static int RequiredInt(JObject obj, string key)
        {
            return RequiredToken(obj, key).Value<int>();
        }

        /// <summary>
        /// 读取对象中的必需浮点数。
        /// </summary>
        private static float RequiredFloat(JObject obj, string key)
        {
            return RequiredToken(obj, key).Value<float>();
        }

        /// <summary>
        /// 读取对象中的非空必需字符串。
        /// </summary>
        private static string RequiredString(JObject obj, string key)
        {
            var value = RequiredToken(obj, key).Value<string>();
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException($"[AutoChessSourceGenConfig] required string is empty: {key}");
            return value;
        }

        /// <summary>
        /// 读取对象中的必需 token。
        /// </summary>
        private static JToken RequiredToken(JObject obj, string key)
        {
            var token = obj[key];
            if (token == null || token.Type == JTokenType.Null)
                throw new InvalidDataException($"[AutoChessSourceGenConfig] required value is missing: {key}");
            return token;
        }
    }
}
