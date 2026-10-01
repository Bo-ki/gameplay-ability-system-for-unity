using GAS.AutoChessDemo;
using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 直接消费 production generated Blob，冻结本轮 AutoChess closed-world 的 ID、数值与 9203 capture 语义。
    /// </summary>
    [TestFixture]
    [Category("RuntimeV1Runnable")]
    public class RuntimeV1RunnableCatalogTests
    {
        /// <summary>
        /// 验证 production Catalog 唯一包含三属性、四 Ability 与四 Effect，且 9203 不再依赖 9204。
        /// </summary>
        [Test]
        public void ProductionCatalog_MatchesClosedWorld()
        {
            using var catalog = AutoChessRuntimeV1CatalogAccess.BuildProductionCatalog(Allocator.Temp, out var expectation);
            ref var root = ref catalog.Value;

            Assert.That(GasDefinitionCatalogValidator.Validate(ref root, in expectation).Succeeded, Is.True);
            Assert.That(GasRuntimeV1SupportProfile.ValidateBlob(ref root).Succeeded, Is.True);
            AssertAttributeAndAbilityIds(ref root);
            AssertFixedDamage(ref root, 9201, -12f);
            AssertFixedDamage(ref root, 9202, -8f);
            AssertPoison(ref root);
            AssertFinisher(ref root);
            Assert.That(ContainsEffect(ref root, 9204), Is.False);
        }

        /// <summary>
        /// 校验 Attribute identity/layout 与四个 Ability 到 root Effect 的稳定映射。
        /// </summary>
        private static void AssertAttributeAndAbilityIds(ref GasDefinitionCatalogBlob root)
        {
            Assert.That(root.AttributeLayout.Entries.Length, Is.EqualTo(3));
            Assert.That(root.AttributeLayout.Entries[0].AttributeId, Is.EqualTo(1));
            Assert.That(root.AttributeLayout.Entries[0].DomainRole, Is.EqualTo(GasAttributeDomainRole.Health));
            Assert.That(root.AttributeLayout.Entries[1].AttributeId, Is.EqualTo(2));
            Assert.That(root.AttributeLayout.Entries[2].AttributeId, Is.EqualTo(4));
            Assert.That(root.AttributeLayout.Entries[2].DefaultValue, Is.Zero);

            var expectedAbilities = new[] { 9101, 9102, 9103, 9104 };
            var expectedEffects = new[] { 9201, 9202, 9207, 9203 };
            Assert.That(root.Abilities.Length, Is.EqualTo(expectedAbilities.Length));
            for (var index = 0; index < expectedAbilities.Length; index++)
            {
                Assert.That(root.Abilities[index].DefinitionId, Is.EqualTo(expectedAbilities[index]));
                var node = root.DirectEffectProgramNodes[root.Abilities[index].DirectEffectProgramRange.Start];
                Assert.That(node.EffectDefinitionId, Is.EqualTo(expectedEffects[index]));
            }
        }

        /// <summary>
        /// 校验 9201/9202 authoring Subtract 已唯一编译为 Health Add 加负常量。
        /// </summary>
        private static void AssertFixedDamage(
            ref GasDefinitionCatalogBlob root,
            int definitionId,
            float expectedDelta)
        {
            var effect = Effect(ref root, definitionId);
            Assert.That(effect.Lifetime, Is.EqualTo(GasEffectLifetimePolicy.Instant));
            var modifier = root.Modifiers[effect.ModifierRange.Start];
            Assert.That(modifier.AttributeLayoutIndex, Is.Zero);
            Assert.That(modifier.Operation, Is.EqualTo(GasModifierOperation.Add));
            var instruction = root.EvaluatorInstructions[effect.EvaluatorProgramRange.Start];
            Assert.That(instruction.Opcode, Is.EqualTo(GasEvaluatorOpcode.PushConstant));
            Assert.That(instruction.ConstantValue, Is.EqualTo(expectedDelta));
            AssertCue(ref root, in effect, GasCuePhaseFlags.Executed);
        }

        /// <summary>
        /// 校验 9203 的 Source Attack Base Snapshot、六指令负伤害公式及 stack/period/expiry 策略。
        /// </summary>
        private static void AssertPoison(ref GasDefinitionCatalogBlob root)
        {
            var effect = Effect(ref root, 9203);
            Assert.That(effect.Lifetime, Is.EqualTo(GasEffectLifetimePolicy.Duration));
            Assert.That(effect.DurationTicks, Is.EqualTo(8));
            Assert.That(effect.PeriodTicks, Is.EqualTo(2));
            Assert.That(effect.StackLimit, Is.EqualTo(3));
            Assert.That(effect.DurationRefreshPolicy, Is.EqualTo(GasDurationRefreshPolicy.Never));
            Assert.That(effect.PeriodResetPolicy, Is.EqualTo(GasPeriodResetPolicy.OnSuccessfulApplication));
            Assert.That(effect.ExpiryPolicy, Is.EqualTo(GasExpiryPolicy.RemoveOneStackAndRefreshDuration));
            Assert.That(effect.ExpiryPeriodPolicy, Is.EqualTo(GasExpiryPeriodPolicy.Reset));
            Assert.That(effect.ExpirySameTickPolicy, Is.EqualTo(GasExpirySameTickPolicy.PeriodDueBeforeExpiry));
            Assert.That(effect.ExecuteOnApplication, Is.Zero);

            var capture = root.CaptureDescriptors[effect.CaptureRange.Start];
            Assert.That(capture.Owner, Is.EqualTo(GasCaptureOwner.Source));
            Assert.That(capture.Binding, Is.EqualTo(GasCaptureBinding.Snapshot));
            Assert.That(capture.Phase, Is.EqualTo(GasCapturePhase.SourceSpecProjection));
            Assert.That(capture.ValueView, Is.EqualTo(GasAttributeValueView.Base));
            Assert.That(capture.AttributeLayoutIndex, Is.EqualTo(2));

            var modifier = root.Modifiers[effect.ModifierRange.Start];
            Assert.That(modifier.AttributeLayoutIndex, Is.Zero);
            Assert.That(modifier.Operation, Is.EqualTo(GasModifierOperation.Add));
            var start = effect.EvaluatorProgramRange.Start;
            AssertInstruction(ref root, start, GasEvaluatorOpcode.PushCapture);
            AssertConstant(ref root, start + 1, 0.3f);
            AssertInstruction(ref root, start + 2, GasEvaluatorOpcode.Multiply);
            AssertInstruction(ref root, start + 3, GasEvaluatorOpcode.PushStackCount);
            AssertInstruction(ref root, start + 4, GasEvaluatorOpcode.Multiply);
            AssertInstruction(ref root, start + 5, GasEvaluatorOpcode.Negate);
            AssertCue(ref root, in effect, GasCuePhaseFlags.OnActive);
        }

        /// <summary>
        /// 校验 9207 的 production missing-health 数值与最终 Negate。
        /// </summary>
        private static void AssertFinisher(ref GasDefinitionCatalogBlob root)
        {
            var effect = Effect(ref root, 9207);
            var start = effect.EvaluatorProgramRange.Start;
            Assert.That(effect.Lifetime, Is.EqualTo(GasEffectLifetimePolicy.InstantExecution));
            Assert.That(effect.ValueViewRange.Count, Is.EqualTo(3));
            AssertConstant(ref root, start + 3, 0f);
            AssertConstant(ref root, start + 5, 0.5f);
            AssertConstant(ref root, start + 7, 16f);
            AssertConstant(ref root, start + 9, 12f);
            AssertConstant(ref root, start + 10, 42f);
            AssertInstruction(ref root, start + 12, GasEvaluatorOpcode.Negate);
            AssertCue(ref root, in effect, GasCuePhaseFlags.Executed);
        }

        /// <summary>
        /// 按稳定 ID 返回 production Effect definition。
        /// </summary>
        private static GasGameplayEffectDefinitionBlob Effect(ref GasDefinitionCatalogBlob root, int definitionId)
        {
            for (var index = 0; index < root.GameplayEffects.Length; index++)
                if (root.GameplayEffects[index].DefinitionId == definitionId)
                    return root.GameplayEffects[index];
            Assert.Fail($"GameplayEffect {definitionId} is missing.");
            return default;
        }

        /// <summary>
        /// 判断 production Catalog 是否仍包含指定 legacy Effect。
        /// </summary>
        private static bool ContainsEffect(ref GasDefinitionCatalogBlob root, int definitionId)
        {
            for (var index = 0; index < root.GameplayEffects.Length; index++)
                if (root.GameplayEffects[index].DefinitionId == definitionId)
                    return true;
            return false;
        }

        /// <summary>
        /// 校验一条无常量 evaluator instruction。
        /// </summary>
        private static void AssertInstruction(
            ref GasDefinitionCatalogBlob root,
            int index,
            GasEvaluatorOpcode opcode)
        {
            var instruction = root.EvaluatorInstructions[index];
            Assert.That(instruction.Opcode, Is.EqualTo(opcode));
            Assert.That(instruction.OperandIndex, Is.Zero);
            Assert.That(instruction.ConstantValue, Is.Zero);
        }

        /// <summary>
        /// 校验一条精确 PushConstant instruction。
        /// </summary>
        private static void AssertConstant(ref GasDefinitionCatalogBlob root, int index, float value)
        {
            var instruction = root.EvaluatorInstructions[index];
            Assert.That(instruction.Opcode, Is.EqualTo(GasEvaluatorOpcode.PushConstant));
            Assert.That(instruction.ConstantValue, Is.EqualTo(value));
        }

        /// <summary>
        /// 校验一个 Effect 的唯一 9301 Cue phase。
        /// </summary>
        private static void AssertCue(
            ref GasDefinitionCatalogBlob root,
            in GasGameplayEffectDefinitionBlob effect,
            GasCuePhaseFlags phases)
        {
            Assert.That(effect.CueRange.Count, Is.EqualTo(1));
            var cue = root.CueReferences[effect.CueRange.Start];
            Assert.That(cue.CueDefinitionId, Is.EqualTo(9301));
            Assert.That(cue.Phases, Is.EqualTo(phases));
        }
    }
}
