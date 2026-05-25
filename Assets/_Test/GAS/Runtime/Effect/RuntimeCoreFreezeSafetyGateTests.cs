using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace GAS.Runtime.Tests.Effect
{
    public sealed class RuntimeCoreFreezeSafetyGateTests
    {
        [Test]
        public void LegacyInstantEntityLifecycleIsFrozenMigrationOnly()
        {
            Assert.That(GameplayEffectRuntimePipelineContract.IsLegacyInstantEntityLifecycleFrozen, Is.True);
            Assert.That(
                GameplayEffectRuntimePipelineContract.AllowsNewFeatureExpansion(
                    GameplayEffectRuntimePipelineKind.LegacyInstantEntityLifecycle),
                Is.False);
            Assert.That(
                GameplayEffectRuntimePipelineContract.DefaultNewRuntimeWritePath,
                Is.EqualTo(GameplayEffectRuntimePipelineKind.EffectCommandSpecStream));

            Assert.That(
                GameplayEffectRuntimePipelineContract.TryFind(
                    GameplayEffectRuntimePipelineKind.LegacyInstantEntityLifecycle,
                    out var legacy),
                Is.True);
            Assert.That(legacy.Status, Is.EqualTo(GameplayEffectRuntimePipelineStatus.LegacyFrozen));
            Assert.That(legacy.HasRestriction(GameplayEffectRuntimePipelineRestriction.MigrationOnly), Is.True);
            Assert.That(legacy.HasRestriction(GameplayEffectRuntimePipelineRestriction.NoNewFeatureExpansion), Is.True);
            Assert.That(legacy.HasRestriction(GameplayEffectRuntimePipelineRestriction.NotDefaultNewBusinessPath), Is.True);
            Assert.That(legacy.HasRestriction(GameplayEffectRuntimePipelineRestriction.NotHighFrequencyReactionInput), Is.True);
        }

        [Test]
        public void TargetPipelineContractKeepsEffectCommandSpecDeltaTerms()
        {
            Assert.That(
                GameplayEffectRuntimePipelineContract.TryFind(
                    GameplayEffectRuntimePipelineKind.EffectCommandSpecStream,
                    out var target),
                Is.True);

            Assert.That(target.Status, Is.EqualTo(GameplayEffectRuntimePipelineStatus.TargetContract));
            Assert.That(target.WriteEntryName, Does.Contain("EffectCommand"));
            Assert.That(target.EvaluationName, Does.Contain("InstantEffectSpec"));
            Assert.That(target.OutputName, Does.Contain("AttributeDelta"));
            Assert.That(target.OutputName, Does.Contain("TypedSimulationFact"));
            Assert.That(
                target.HasRestriction(GameplayEffectRuntimePipelineRestriction.AvoidsRuntimeGameplayEffectEntity),
                Is.True);
        }

        [Test]
        public void RequestWriterExposesTargetCommandEntryAndLegacyBypassEntryPoints()
        {
            var methodNames = GetDeclaredStaticMethodNames(typeof(GameplayEffectRequestWriter));

            Assert.That(methodNames, Does.Not.Contain("TryApplyFastInstantModifier"));
            Assert.That(methodNames, Does.Not.Contain("ApplyFastOrCreateSingleTargetRequest"));
            Assert.That(methodNames, Does.Contain("TryAppendSimpleInstantCommand"));
            Assert.That(methodNames, Does.Contain("TryAppendSimpleInstantCommands"));
            Assert.That(methodNames, Does.Contain("AppendSimpleInstantCommandOrCreateSingleTargetRequest"));
            Assert.That(methodNames, Does.Contain("AppendSimpleInstantCommandsOrCreateTargetListRequest"));
            Assert.That(methodNames, Does.Contain("TryApplyLegacyInstantModifierBypass"));
            Assert.That(methodNames, Does.Contain("ApplyLegacyInstantBypassOrCreateSingleTargetRequest"));
        }

        [Test]
        public void PublicLegacyBridgeDoesNotExposeFastPathNaming()
        {
            var methodNames = GetDeclaredStaticMethodNames(typeof(GameplayEffectLegacyBridge));

            Assert.That(methodNames, Does.Not.Contain("TryApplyFastInstantModifier"));
            Assert.That(methodNames, Does.Not.Contain("ApplyFastOrCreateSingleTargetRequest"));
            Assert.That(methodNames, Does.Contain("ApplyLegacyInstantBypassOrCreateSingleTargetRequest"));
        }

        private static IReadOnlyList<string> GetDeclaredStaticMethodNames(Type type)
        {
            var methods = type.GetMethods(
                BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
            var names = new List<string>(methods.Length);
            for (var i = 0; i < methods.Length; i++)
                names.Add(methods[i].Name);

            return names;
        }
    }
}
