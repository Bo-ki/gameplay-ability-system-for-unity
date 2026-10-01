using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GAS.Runtime;
using Newtonsoft.Json.Linq;

namespace GAS.Editor
{
    /// <summary>
    /// 在 Luban normalization 前扫描 AutoChess raw JSON 与 sidecar，防止被取首项或默认化的语义混入。
    /// </summary>
    public static class RuntimeV1RawAuthoringSupportProfileAdapter
    {
        private static readonly string[] AbilityNames =
        {
            "AutoChessPlayerAttack",
            "AutoChessEnemyAttack",
            "AutoChessPlayerExecute",
            "AutoChessPlayerPoison",
        };

        private static readonly string[] EffectNames =
        {
            "AutoChessPlayerAttackDamage",
            "AutoChessEnemyAttackDamage",
            "AutoChessPlayerExecute",
            "AutoChessPlayerPoison",
        };

        /// <summary>
        /// 直接校验四个 raw 文件，供 EditMode mutation test 无反射调用。
        /// </summary>
        public static GasRuntimeV1SupportProfileResult ValidateFiles(
            string abilityJsonPath,
            string effectJsonPath,
            string attributeJsonPath,
            string sidecarPath)
        {
            try
            {
                var abilities = ReadArray(abilityJsonPath);
                var effects = ReadArray(effectJsonPath);
                var attributes = ReadArray(attributeJsonPath);
                var sidecar = ReadObject(sidecarPath);
                var abilityResult = ValidateAbilities(abilities);
                if (!abilityResult.Succeeded)
                    return abilityResult;
                var effectResult = ValidateEffects(effects, out var combatSetId, out var healthId, out var attackId);
                if (!effectResult.Succeeded)
                    return effectResult;
                var attributeResult = ValidateAttributes(attributes, combatSetId, healthId, attackId);
                return attributeResult.Succeeded ? ValidateSidecar(sidecar) : attributeResult;
            }
            catch (Exception exception) when (exception is IOException
                || exception is UnauthorizedAccessException
                || exception is Newtonsoft.Json.JsonException
                || exception is ArgumentException
                || exception is InvalidCastException
                || exception is InvalidOperationException
                || exception is FormatException
                || exception is OverflowException)
            {
                return Failure(GasRuntimeV1SupportProfileError.RawDocumentInvalid);
            }
        }

        /// <summary>
        /// 从当前官方 Unity generation context 解析 raw 路径并在模型 normalization 前执行校验。
        /// </summary>
        internal static GasRuntimeV1SupportProfileResult Validate(GasCodeGenContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var lubanRoot = Path.Combine(context.ProjectRoot, context.Settings.LubanDataOutputPath);
            var sidecar = Path.Combine(
                context.ProjectRoot,
                context.Settings.ConfigProjectPath,
                "Datas/AutoChessDemo/autochess.sourcegen.json");
            return ValidateFiles(
                Path.Combine(lubanRoot, "exgas_tbability.json"),
                Path.Combine(lubanRoot, "exgas_tbgameplayeffect.json"),
                Path.Combine(lubanRoot, "exgas_tbattributeset.json"),
                sidecar);
        }

        /// <summary>
        /// 校验四个 one-shot Ability，尤其拒绝被 FirstPositive 静默压平的 ID 数组。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateAbilities(JArray rows)
        {
            var selected = SelectAutoChessRows(rows, AbilityNames, GasRuntimeV1SupportProfileError.RawAbilityUnsupported);
            if (selected.Result != null)
                return selected.Result.Value;
            foreach (var row in selected.Rows)
            {
                var definitionId = Int(row["ID"]);
                var name = String(row["Name"]);
                if (!HasOnlyProperties(row, "ID", "Name", "Desc", "Cost", "CdEffect", "Cd",
                        "AssetTags", "ActivationOwnedTags", "AbilityExecution", "RuntimeV1TargetPolicy")
                    || definitionId != ExpectedAbilityId(name)
                    || Int(row["Cost"]) != 0 || Int(row["CdEffect"]) != 0 || Int(row["Cd"]) != 0
                    || !IsEmptyArray(row["AssetTags"]) || !IsEmptyArray(row["ActivationOwnedTags"])
                    || !IsTargetPolicy(row["RuntimeV1TargetPolicy"] as JObject)
                    || !IsDirectAbilityExecution(row["AbilityExecution"] as JObject, ExpectedAbilityEffectId(name)))
                {
                    return Failure(GasRuntimeV1SupportProfileError.RawAbilityUnsupported, definitionId);
                }
            }
            return Success();
        }

        /// <summary>
        /// 校验四个 Effect raw row，并返回由 Luban 定义的 combat/Health/Attack identity。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateEffects(
            JArray rows,
            out int combatSetId,
            out int healthId,
            out int attackId)
        {
            combatSetId = 0;
            healthId = 0;
            attackId = 0;
            var selected = SelectAutoChessRows(rows, EffectNames, GasRuntimeV1SupportProfileError.RawEffectUnsupported);
            if (selected.Result != null)
                return selected.Result.Value;
            foreach (var row in selected.Rows)
            {
                var result = ValidateEffectRow(row, ref combatSetId, ref healthId, ref attackId);
                if (!result.Succeeded)
                    return result;
            }
            return combatSetId == 9001 && healthId == 1 && attackId == 4
                ? Success()
                : Failure(GasRuntimeV1SupportProfileError.RawEffectUnsupported);
        }

        /// <summary>
        /// 按稳定名称选择一个 Effect，并验证 common envelope 后分派精确语义校验。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateEffectRow(
            JObject row,
            ref int combatSetId,
            ref int healthId,
            ref int attackId)
        {
            var definitionId = Int(row["ID"]);
            var name = String(row["Name"]);
            if (!HasOnlyProperties(row, "ID", "Name", "Desc", "AssetTags", "GrantedTags", "Duration", "Period",
                    "Modifiers", "CueOnApply", "CueOnTick", "CueOnAdd", "CueOnRemove", "CueOnActivate",
                    "CueOnDeactivate", "GrantedAbility", "Stacking", "RuntimeV1TargetPolicy", "RuntimeV1Evaluator")
                || definitionId != ExpectedEffectId(name)
                || !IsEmptyArray(row["AssetTags"]) || !IsEmptyArray(row["GrantedTags"])
                || !IsEmptyArray(row["GrantedAbility"]) || !IsTargetPolicy(row["RuntimeV1TargetPolicy"] as JObject)
                || !IsOnlyApplyCue(row))
            {
                return Failure(GasRuntimeV1SupportProfileError.RawEffectUnsupported, definitionId);
            }

            if (name == "AutoChessPlayerAttackDamage")
                return ValidateFixedEffect(row, 12f, ref combatSetId, ref healthId);
            if (name == "AutoChessEnemyAttackDamage")
                return ValidateFixedEffect(row, 8f, ref combatSetId, ref healthId);
            if (name == "AutoChessPlayerExecute")
                return ValidateFinisher(row, ref combatSetId, ref healthId);
            if (name == "AutoChessPlayerPoison")
                return ValidatePoison(row, ref combatSetId, ref healthId, ref attackId);
            return Failure(GasRuntimeV1SupportProfileError.RawEffectUnsupported, definitionId);
        }

        /// <summary>
        /// 校验 9201/9202 的唯一 Health Subtract modifier 与无生命周期 payload。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateFixedEffect(
            JObject row,
            float magnitude,
            ref int combatSetId,
            ref int healthId)
        {
            if (row["Duration"] != null || row["Period"] != null || row["Stacking"] != null
                || row["RuntimeV1Evaluator"] != null
                || !TrySingleModifier(row["Modifiers"], magnitude, 3, out var setId, out var attributeId)
                || !MergeIdentity(ref combatSetId, setId) || !MergeIdentity(ref healthId, attributeId))
            {
                return Failure(GasRuntimeV1SupportProfileError.RawEffectUnsupported, Int(row["ID"]));
            }
            return Success();
        }

        /// <summary>
        /// 校验 9207 missing-health evaluator 的精确 raw 公式。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateFinisher(
            JObject row,
            ref int combatSetId,
            ref int healthId)
        {
            var evaluator = row["RuntimeV1Evaluator"] as JObject;
            if (row["Duration"] != null || row["Period"] != null || row["Stacking"] != null
                || !IsEmptyArray(row["Modifiers"]) || !IsEvaluator(evaluator, 1, 16f, 0.5f, 12f, 42f)
                || !MergeIdentity(ref combatSetId, Int(evaluator?["AttributeSet"]))
                || !MergeIdentity(ref healthId, Int(evaluator?["Attribute"])))
            {
                return Failure(GasRuntimeV1SupportProfileError.RawEffectUnsupported, Int(row["ID"]));
            }
            return Success();
        }

        /// <summary>
        /// 校验 9203 inline Health Subtract、Source Attack evaluator 与完整 stack/period/expiry authoring。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidatePoison(
            JObject row,
            ref int combatSetId,
            ref int healthId,
            ref int attackId)
        {
            var duration = row["Duration"] as JObject;
            var period = row["Period"] as JObject;
            var stacking = row["Stacking"] as JObject;
            var evaluator = row["RuntimeV1Evaluator"] as JObject;
            if (!IsDuration(duration) || !IsPeriod(period) || !IsStacking(stacking)
                || !TrySingleModifier(row["Modifiers"], 0f, 3, out var setId, out var outputId)
                || !IsEvaluator(evaluator, 2, 0f, 0.3f, 0f, 0f)
                || !MergeIdentity(ref combatSetId, setId)
                || !MergeIdentity(ref combatSetId, Int(evaluator?["AttributeSet"]))
                || !MergeIdentity(ref healthId, outputId)
                || !MergeIdentity(ref attackId, Int(evaluator?["Attribute"]))
                || healthId == attackId)
            {
                return Failure(GasRuntimeV1SupportProfileError.RawEffectUnsupported, Int(row["ID"]));
            }
            return Success();
        }

        /// <summary>
        /// 校验 AutoChessCombat 只有 Health、Energy、Attack 三属性，并保持 Attack default=0。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateAttributes(
            JArray rows,
            int combatSetId,
            int healthId,
            int attackId)
        {
            var autoChess = rows.OfType<JObject>()
                .Where(row => String(row["Name"]).StartsWith("AutoChess", StringComparison.Ordinal))
                .ToArray();
            if (autoChess.Length != 1 || !HasOnlyProperties(autoChess[0], "ID", "Name", "Desc", "Attribute")
                || Int(autoChess[0]["ID"]) != combatSetId || String(autoChess[0]["Name"]) != "AutoChessCombat"
                || !(autoChess[0]["Attribute"] is JArray attributes) || attributes.Count != 3)
            {
                return Failure(GasRuntimeV1SupportProfileError.RawAttributeUnsupported, combatSetId);
            }

            var ids = new HashSet<int>();
            foreach (var attribute in attributes.OfType<JObject>())
            {
                var id = Int(attribute["ID"]);
                var isHealth = id == healthId;
                if (!HasOnlyProperties(attribute, "ID", "InitValue", "MinValue", "MaxValue",
                        "UseMinValue", "UseMaxValue", "DomainRole")
                    || id <= 0 || !ids.Add(id) || Float(attribute["InitValue"]) != (isHealth ? 100f : 0f)
                    || Float(attribute["MinValue"]) != 0f || Float(attribute["MaxValue"]) != 99999f
                    || !Bool(attribute["UseMinValue"]) || !Bool(attribute["UseMaxValue"])
                    || Int(attribute["DomainRole"]) != (isHealth ? 1 : 0))
                {
                    return Failure(GasRuntimeV1SupportProfileError.RawAttributeUnsupported, combatSetId, id);
                }
            }
            return combatSetId == 9001 && healthId == 1 && attackId == 4
                && ids.SetEquals(new[] { 1, 2, 4 })
                ? Success()
                : Failure(GasRuntimeV1SupportProfileError.RawAttributeUnsupported, combatSetId);
        }

        /// <summary>
        /// 校验 sidecar 只含场景阈值与四个 unit，Attack 为每单位唯一显式非默认值源。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult ValidateSidecar(JObject root)
        {
            if (!HasExactProperties(root, "validationScenario", "units")
                || !(root["validationScenario"] is JObject scenario)
                || !IsValidationScenario(scenario)
                || !(root["units"] is JArray units) || units.Count != 4)
            {
                return Failure(GasRuntimeV1SupportProfileError.RawScenarioUnsupported);
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unit in units.OfType<JObject>())
            {
                if (!IsUnit(unit, ids))
                    return Failure(GasRuntimeV1SupportProfileError.RawScenarioUnsupported);
            }
            return ids.Count == 4 ? Success() : Failure(GasRuntimeV1SupportProfileError.RawScenarioUnsupported);
        }

        /// <summary>
        /// 校验单个 sidecar unit 的字段闭集与显式正 Attack。
        /// </summary>
        private static bool IsUnit(JObject unit, ISet<string> ids)
        {
            var id = String(unit["id"]);
            if (!HasExactProperties(unit, "id", "displayName", "archetypeName", "team", "slot", "health",
                    "energy", "attack", "primaryAbility", "finisherAbility", "activeAbility",
                    "activeCastInterval", "activeCastFrameOffset", "finisherHealthThreshold",
                    "primaryTargetPolicy", "finisherTargetPolicy")
                || string.IsNullOrWhiteSpace(id) || !ids.Add(id)
                || string.IsNullOrWhiteSpace(String(unit["displayName"]))
                || string.IsNullOrWhiteSpace(String(unit["archetypeName"])))
            {
                return false;
            }

            return id switch
            {
                "player-vanguard" => IsUnitValues(unit, "AutoChessTeam.Player", 0, 72f, 8f, 16f,
                    "AbilityPlayerAttack", "AbilityPlayerExecute", "AbilityPlayerPoison", 3, 0, 44f,
                    "AutoChessTargetPolicy.Frontline", "AutoChessTargetPolicy.LowestHealth"),
                "player-ranger" => IsUnitValues(unit, "AutoChessTeam.Player", 1, 54f, 8f, 14f,
                    "AbilityPlayerAttack", "AbilityPlayerExecute", "AbilityPlayerPoison", 3, 1, 44f,
                    "AutoChessTargetPolicy.LowestHealth", "AutoChessTargetPolicy.LowestHealth"),
                "enemy-brute" => IsUnitValues(unit, "AutoChessTeam.Enemy", 0, 48f, 8f, 12f,
                    "AbilityEnemyAttack", "None", "None", 0, 0, 0f,
                    "AutoChessTargetPolicy.Frontline", "AutoChessTargetPolicy.Frontline"),
                "enemy-caster" => IsUnitValues(unit, "AutoChessTeam.Enemy", 1, 42f, 8f, 10f,
                    "AbilityEnemyAttack", "None", "None", 0, 0, 0f,
                    "AutoChessTargetPolicy.Frontline", "AutoChessTargetPolicy.Frontline"),
                _ => false,
            };
        }

        /// <summary>
        /// 校验 validation scenario 的全部必填阈值，避免缺字段或未知值在模型层非 typed 失败。
        /// </summary>
        private static bool IsValidationScenario(JObject scenario)
        {
            return HasExactProperties(scenario, "scale", "maxTicks", "healthMultiplier", "expectedWinner",
                    "minAcceptedCommands", "minAttributeChanges", "minExecutionOutputs", "minCueRequests",
                    "minActiveEffectSlots", "minPeriodTickDamageFacts", "minActiveMutationCommands",
                    "minActiveMutationOwnerGroups", "maxActiveMutationEstimatedRandomLookups",
                    "maxActiveMutationOwnerResourceLookups", "maxActiveMutationMigrationCarriers")
                && Int(scenario["scale"]) == 50 && Int(scenario["maxTicks"]) == 96
                && Float(scenario["healthMultiplier"]) == 1f
                && String(scenario["expectedWinner"]) == "AutoChessTeam.Player"
                && Int(scenario["minAcceptedCommands"]) == 1
                && Int(scenario["minAttributeChanges"]) == 1
                && Int(scenario["minExecutionOutputs"]) == 1
                && String(scenario["minCueRequests"]) == "Auto"
                && Int(scenario["minActiveEffectSlots"]) == 1
                && Int(scenario["minPeriodTickDamageFacts"]) == 1
                && Int(scenario["minActiveMutationCommands"]) == 1
                && Int(scenario["minActiveMutationOwnerGroups"]) == 1
                && Int(scenario["maxActiveMutationEstimatedRandomLookups"]) == 0
                && Int(scenario["maxActiveMutationOwnerResourceLookups"]) == 0
                && Int(scenario["maxActiveMutationMigrationCarriers"]) == 0;
        }

        /// <summary>
        /// 校验一个已知 unit 的全部 runtime 语义值，显示文本只要求非空。
        /// </summary>
        private static bool IsUnitValues(
            JObject unit,
            string team,
            int slot,
            float health,
            float energy,
            float attack,
            string primaryAbility,
            string finisherAbility,
            string activeAbility,
            int activeCastInterval,
            int activeCastFrameOffset,
            float finisherHealthThreshold,
            string primaryTargetPolicy,
            string finisherTargetPolicy)
        {
            return String(unit["team"]) == team && Int(unit["slot"]) == slot
                && Float(unit["health"]) == health && Float(unit["energy"]) == energy
                && Float(unit["attack"]) == attack
                && String(unit["primaryAbility"]) == primaryAbility
                && String(unit["finisherAbility"]) == finisherAbility
                && String(unit["activeAbility"]) == activeAbility
                && Int(unit["activeCastInterval"]) == activeCastInterval
                && Int(unit["activeCastFrameOffset"]) == activeCastFrameOffset
                && Float(unit["finisherHealthThreshold"]) == finisherHealthThreshold
                && String(unit["primaryTargetPolicy"]) == primaryTargetPolicy
                && String(unit["finisherTargetPolicy"]) == finisherTargetPolicy;
        }

        /// <summary>
        /// 校验 AbilityExecution 只含单个正 Effect ID，禁止 normalization 取首项。
        /// </summary>
        private static bool IsDirectAbilityExecution(JObject execution, int expectedEffectId)
        {
            if (execution == null || !HasOnlyProperties(execution, "$type", "Param")
                || String(execution["$type"]) != "ApplyEffectsOnActivate"
                || !(execution["Param"] is JObject parameter) || !HasOnlyProperties(parameter, "IDs")
                || !(parameter["IDs"] is JArray ids) || ids.Count != 1)
            {
                return false;
            }
            return ids[0].Type == JTokenType.Integer && Int(ids[0]) == expectedEffectId;
        }

        /// <summary>
        /// 校验 Effect 只声明单个 OnApply authoring Cue，其余阶段为空。
        /// </summary>
        private static bool IsOnlyApplyCue(JObject row)
        {
            if (!(row["CueOnApply"] is JArray apply) || apply.Count != 1 || Int(apply[0]) != 9301
                || !IsEmptyArray(row["CueOnTick"]) || !IsEmptyArray(row["CueOnAdd"])
                || !IsEmptyArray(row["CueOnRemove"]) || !IsEmptyArray(row["CueOnActivate"])
                || !IsEmptyArray(row["CueOnDeactivate"]))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 返回闭世界中一个 Ability 稳定名称的精确 Definition ID。
        /// </summary>
        private static int ExpectedAbilityId(string name)
        {
            return name switch
            {
                "AutoChessPlayerAttack" => 9101,
                "AutoChessEnemyAttack" => 9102,
                "AutoChessPlayerExecute" => 9103,
                "AutoChessPlayerPoison" => 9104,
                _ => 0,
            };
        }

        /// <summary>
        /// 返回闭世界中一个 Ability 的唯一 direct Effect ID。
        /// </summary>
        private static int ExpectedAbilityEffectId(string name)
        {
            return name switch
            {
                "AutoChessPlayerAttack" => 9201,
                "AutoChessEnemyAttack" => 9202,
                "AutoChessPlayerExecute" => 9207,
                "AutoChessPlayerPoison" => 9203,
                _ => 0,
            };
        }

        /// <summary>
        /// 返回闭世界中一个 Effect 稳定名称的精确 Definition ID。
        /// </summary>
        private static int ExpectedEffectId(string name)
        {
            return name switch
            {
                "AutoChessPlayerAttackDamage" => 9201,
                "AutoChessEnemyAttackDamage" => 9202,
                "AutoChessPlayerExecute" => 9207,
                "AutoChessPlayerPoison" => 9203,
                _ => 0,
            };
        }

        /// <summary>
        /// 校验单个 raw Modifier 并返回其 Attribute identity。
        /// </summary>
        private static bool TrySingleModifier(
            JToken token,
            float magnitude,
            int operation,
            out int attributeSetId,
            out int attributeId)
        {
            attributeSetId = 0;
            attributeId = 0;
            if (!(token is JArray modifiers) || modifiers.Count != 1 || !(modifiers[0] is JObject modifier)
                || !HasOnlyProperties(modifier, "AttrSet", "Attribute", "Magnitude", "Operation"))
            {
                return false;
            }
            attributeSetId = Int(modifier["AttrSet"]);
            attributeId = Int(modifier["Attribute"]);
            return attributeSetId > 0 && attributeId > 0
                && Float(modifier["Magnitude"]) == magnitude && Int(modifier["Operation"]) == operation;
        }

        /// <summary>
        /// 校验 RuntimeV1Evaluator 对象的字段闭集与公式参数。
        /// </summary>
        private static bool IsEvaluator(
            JObject evaluator,
            int kind,
            float baseValue,
            float coefficient,
            float minimum,
            float maximum)
        {
            return evaluator != null
                && HasOnlyProperties(evaluator, "Kind", "AttributeSet", "Attribute", "BaseValue",
                    "Coefficient", "Minimum", "Maximum")
                && Int(evaluator["Kind"]) == kind && Int(evaluator["AttributeSet"]) > 0
                && Int(evaluator["Attribute"]) > 0 && Float(evaluator["BaseValue"]) == baseValue
                && Float(evaluator["Coefficient"]) == coefficient
                && Float(evaluator["Minimum"]) == minimum && Float(evaluator["Maximum"]) == maximum;
        }

        /// <summary>
        /// 校验 9203 duration 为 8 ticks 且不隐式刷新。
        /// </summary>
        private static bool IsDuration(JObject duration)
        {
            return duration != null && HasOnlyProperties(duration, "TimeUnit", "Time", "ResetStartTimeWhenActivated")
                && Int(duration["TimeUnit"]) == 0 && Int(duration["Time"]) == 8
                && !Bool(duration["ResetStartTimeWhenActivated"]);
        }

        /// <summary>
        /// 校验 9203 period 使用显式 zero sentinel 表示无动态 child，且不首帧执行。
        /// </summary>
        private static bool IsPeriod(JObject period)
        {
            return period != null && HasOnlyProperties(period, "Time", "Effects", "FirstTrigger")
                && Int(period["Time"]) == 2 && period["Effects"] is JArray effects
                && effects.Count == 1 && Int(effects[0]) == 0 && !Bool(period["FirstTrigger"]);
        }

        /// <summary>
        /// 校验 9203 stack、period reset、逐层 expiry 与无 overflow child。
        /// </summary>
        private static bool IsStacking(JObject stacking)
        {
            return stacking != null && HasOnlyProperties(stacking, "StackingType", "StackCode", "LimitCount",
                    "DurationRefreshPolicy", "PeriodResetPolicy", "ExpirationPolicy", "DenyOverflowApplication",
                    "ClearStackOnOverflow", "OverflowEffects")
                && Int(stacking["StackingType"]) == 0 && Int(stacking["StackCode"]) == 0
                && Int(stacking["LimitCount"]) == 3 && Int(stacking["DurationRefreshPolicy"]) == 0
                && Int(stacking["PeriodResetPolicy"]) == 1 && Int(stacking["ExpirationPolicy"]) == 1
                && !Bool(stacking["DenyOverflowApplication"]) && !Bool(stacking["ClearStackOnOverflow"])
                && IsEmptyArray(stacking["OverflowEffects"]);
        }

        /// <summary>
        /// 校验四维 target policy 为本轮唯一 FrozenAsc alive 组合。
        /// </summary>
        private static bool IsTargetPolicy(JObject policy)
        {
            return policy != null && HasOnlyProperties(policy, "LogicalTarget", "Avatar", "Spatial", "Life")
                && Int(policy["LogicalTarget"]) == 2 && Int(policy["Avatar"]) == 2
                && Int(policy["Spatial"]) == 1 && Int(policy["Life"]) == 1;
        }

        /// <summary>
        /// 选择全部 AutoChess row，并拒绝额外或缺失的稳定名称。
        /// </summary>
        private static SelectedRows SelectAutoChessRows(
            JArray rows,
            IReadOnlyCollection<string> expectedNames,
            GasRuntimeV1SupportProfileError error)
        {
            var selected = rows.OfType<JObject>()
                .Where(row => String(row["Name"]).StartsWith("AutoChess", StringComparison.Ordinal))
                .ToArray();
            if (selected.Length != expectedNames.Count
                || selected.Select(row => String(row["Name"])).Distinct(StringComparer.Ordinal).Count() != selected.Length
                || selected.Any(row => !expectedNames.Contains(String(row["Name"]))))
            {
                return new SelectedRows(Array.Empty<JObject>(), Failure(error));
            }
            return new SelectedRows(selected, null);
        }

        /// <summary>
        /// 校验 JObject 不含 allowlist 之外的 raw 字段。
        /// </summary>
        private static bool HasOnlyProperties(JObject value, params string[] names)
        {
            if (value == null)
                return false;
            var allowed = new HashSet<string>(names, StringComparer.Ordinal);
            return value.Properties().All(property => allowed.Contains(property.Name));
        }

        /// <summary>
        /// 校验 JObject 必须且只能包含完整字段集。
        /// </summary>
        private static bool HasExactProperties(JObject value, params string[] names)
        {
            return value != null && value.Properties().Count() == names.Length
                && HasOnlyProperties(value, names);
        }

        /// <summary>
        /// 合并同一条 authoring identity；第一次写入，后续必须精确相等。
        /// </summary>
        private static bool MergeIdentity(ref int identity, int candidate)
        {
            if (candidate <= 0)
                return false;
            if (identity == 0)
                identity = candidate;
            return identity == candidate;
        }

        /// <summary>
        /// 判断 token 为显式空数组。
        /// </summary>
        private static bool IsEmptyArray(JToken token)
        {
            return token is JArray array && array.Count == 0;
        }

        /// <summary>
        /// 读取 JSON 数组根。
        /// </summary>
        private static JArray ReadArray(string path)
        {
            return JToken.Parse(File.ReadAllText(path)) as JArray
                ?? throw new InvalidDataException($"JSON root must be an array: {path}");
        }

        /// <summary>
        /// 读取 JSON 对象根。
        /// </summary>
        private static JObject ReadObject(string path)
        {
            return JToken.Parse(File.ReadAllText(path)) as JObject
                ?? throw new InvalidDataException($"JSON root must be an object: {path}");
        }

        /// <summary>
        /// 读取整数 token，类型不符时抛出并由 public adapter 转 typed reject。
        /// </summary>
        private static int Int(JToken token)
        {
            return token?.Value<int>() ?? 0;
        }

        /// <summary>
        /// 读取浮点 token，类型不符时抛出并由 public adapter 转 typed reject。
        /// </summary>
        private static float Float(JToken token)
        {
            return token?.Value<float>() ?? 0f;
        }

        /// <summary>
        /// 读取布尔 token，类型不符时抛出并由 public adapter 转 typed reject。
        /// </summary>
        private static bool Bool(JToken token)
        {
            return token != null && token.Value<bool>();
        }

        /// <summary>
        /// 读取字符串 token。
        /// </summary>
        private static string String(JToken token)
        {
            return token?.Value<string>() ?? string.Empty;
        }

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult Success()
        {
            return new GasRuntimeV1SupportProfileResult(GasRuntimeV1SupportProfileError.None);
        }

        /// <summary>
        /// 创建 raw typed reject。
        /// </summary>
        private static GasRuntimeV1SupportProfileResult Failure(
            GasRuntimeV1SupportProfileError error,
            int definitionId = 0,
            int elementIndex = -1)
        {
            return new GasRuntimeV1SupportProfileResult(error, definitionId, elementIndex);
        }

        /// <summary>
        /// 携带稳定名称选择结果与可空 typed reject。
        /// </summary>
        private readonly struct SelectedRows
        {
            public SelectedRows(JObject[] rows, GasRuntimeV1SupportProfileResult? result)
            {
                Rows = rows;
                Result = result;
            }

            public JObject[] Rows { get; }
            public GasRuntimeV1SupportProfileResult? Result { get; }
        }
    }
}
