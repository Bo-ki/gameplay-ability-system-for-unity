using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.Runtime.Tests
{
    public sealed class MagnitudeResolverTests
    {
        private readonly List<Entity> _entities = new();
        private EntityManager _em;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize();
        }

        [SetUp]
        public void SetUp()
        {
            _em = GASManager.EntityManager;
            ClearGameplayEvents();
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _entities.Count - 1; i >= 0; i--)
            {
                var entity = _entities[i];
                if (_em.Exists(entity))
                    _em.DestroyEntity(entity);
            }

            _entities.Clear();
        }

        [Test]
        public void ResolveModifiersKeepsConstantMagnitudeWithoutDefinition()
        {
            var ge = CreateEffectWithModifier(10f);
            var context = CreateContext();
            var spec = CreateSpec();

            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, spec);

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(10f));
        }

        [Test]
        public void ResolveModifiersUsesSetByCallerMagnitude()
        {
            const int setByCallerKey = 8001;
            var ge = CreateEffectWithModifier(10f);
            _em.AddBuffer<BSetByCallerValue>(ge).Add(new BSetByCallerValue
            {
                Key = setByCallerKey,
                Value = 25f,
            });
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.SetByCaller,
                Key = setByCallerKey,
            });

            var context = CreateContext();
            var spec = CreateSpec();

            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, spec);

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(25f));
        }

        [Test]
        public void ResolveModifiersUsesFallbackWhenSetByCallerIsMissing()
        {
            var ge = CreateEffectWithModifier(10f);
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.SetByCaller,
                Key = 9001,
                FallbackMagnitude = 7f,
            });

            var context = CreateContext();
            var spec = CreateSpec();

            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, spec);

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(7f));
        }

        [Test]
        public void ResolveModifiersAppliesMagnitudeDefinitionMath()
        {
            const int setByCallerKey = 8002;
            var ge = CreateEffectWithModifier(10f);
            _em.AddBuffer<BSetByCallerValue>(ge).Add(new BSetByCallerValue
            {
                Key = setByCallerKey,
                Value = 5f,
            });
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.SetByCaller,
                Key = setByCallerKey,
                PreAdd = 1f,
                Coefficient = 3f,
                PostAdd = 2f,
            });

            var context = CreateContext();
            var spec = CreateSpec();

            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, spec);

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(20f));
        }

        [Test]
        public void ResolveModifiersUsesStackCountMagnitude()
        {
            var ge = CreateEffectWithModifier(10f);
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.StackCount,
                Coefficient = 5f,
                PostAdd = 1f,
            });

            var context = CreateContext();
            var spec = CreateSpec(stackCount: 3);

            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, spec);

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(16f));
        }

        [Test]
        public void ResolveModifiersNormalizesMissingStackCountToOne()
        {
            var ge = CreateEffectWithModifier(10f);
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.StackCount,
                Coefficient = 5f,
            });

            var context = CreateContext();
            var spec = CreateSpec(stackCount: 0);

            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, spec);

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(5f));
        }

        [Test]
        public void ResolveModifiersUsesExecutionCalculationOutput()
        {
            const int executionKey = 8101;
            var ge = CreateEffectWithModifier(1f);
            _em.AddBuffer<BExecutionCalculationValue>(ge).Add(new BExecutionCalculationValue
            {
                Key = executionKey,
                Value = 9f,
            });
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.ExecutionCalculation,
                Key = executionKey,
                PreAdd = 1f,
                Coefficient = 2f,
                PostAdd = 3f,
            });

            var context = CreateContext();
            var spec = CreateSpec();

            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, spec);

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(23f));
        }

        [Test]
        public void ResolveModifiersUsesFallbackWhenExecutionCalculationOutputIsMissing()
        {
            var ge = CreateEffectWithModifier(1f);
            const int outputKey = 8102;
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.ExecutionCalculation,
                Key = outputKey,
                FallbackMagnitude = 4f,
                Coefficient = 3f,
            });

            var context = CreateContext();
            var spec = CreateSpec();

            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, spec);

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(12f));

            var evt = FindGameplayEvent(EGameplayEventType.ExecutionCalculationOutputMissing, ge, outputKey);
            Assert.That(evt.Value, Is.EqualTo(4f));
        }

        [Test]
        public void ExecutionCalculationSystemProducesOutputAndReresolvesModifier()
        {
            const int inputKey = 8201;
            const int outputKey = 8202;
            const int calculationCode = 8203;
            var ge = CreateEffectWithModifier(1f);
            _em.AddComponentData(ge, CreateContext());
            _em.AddComponentData(ge, CreateSpec());
            _em.AddBuffer<BSetByCallerValue>(ge).Add(new BSetByCallerValue
            {
                Key = inputKey,
                Value = 6f,
            });
            _em.AddBuffer<BExecutionCalculationDefinition>(ge).Add(new BExecutionCalculationDefinition
            {
                CalculationCode = calculationCode,
                OutputKey = outputKey,
                Source = EExecutionCalculationInputSource.SetByCaller,
                Key = inputKey,
                PreAdd = 1f,
                Coefficient = 3f,
                PostAdd = 2f,
            });
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.ExecutionCalculation,
                Key = outputKey,
            });

            RunCommandGroup();

            var outputs = _em.GetBuffer<BExecutionCalculationValue>(ge);
            Assert.That(outputs.Length, Is.EqualTo(1));
            Assert.That(outputs[0].Key, Is.EqualTo(outputKey));
            Assert.That(outputs[0].Value, Is.EqualTo(23f));

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(23f));

            var evt = FindGameplayEvent(EGameplayEventType.ExecutionCalculationOutputUpdated, ge, calculationCode);
            Assert.That(evt.Value, Is.EqualTo(23f));
        }

        [Test]
        public void ExecutionCalculationSystemUsesFallbackAndEmitsMissingInputFact()
        {
            const int outputKey = 8301;
            const int calculationCode = 8302;
            var ge = CreateEffectWithModifier(1f);
            _em.AddComponentData(ge, CreateContext());
            _em.AddComponentData(ge, CreateSpec());
            _em.AddBuffer<BExecutionCalculationDefinition>(ge).Add(new BExecutionCalculationDefinition
            {
                CalculationCode = calculationCode,
                OutputKey = outputKey,
                Source = EExecutionCalculationInputSource.SetByCaller,
                Key = 8303,
                FallbackValue = 4f,
                Coefficient = 2f,
            });
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.ExecutionCalculation,
                Key = outputKey,
            });

            RunCommandGroup();

            var outputs = _em.GetBuffer<BExecutionCalculationValue>(ge);
            Assert.That(outputs.Length, Is.EqualTo(1));
            Assert.That(outputs[0].Value, Is.EqualTo(8f));

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(8f));

            var missing = FindGameplayEvent(EGameplayEventType.ExecutionCalculationInputMissing, ge, calculationCode);
            Assert.That(missing.Value, Is.EqualTo(4f));

            var updated = FindGameplayEvent(EGameplayEventType.ExecutionCalculationOutputUpdated, ge, calculationCode);
            Assert.That(updated.Value, Is.EqualTo(8f));
        }

        [Test]
        public void ExecutionCalculationSystemAggregatesMultipleInputs()
        {
            const int attrSetCode = 31;
            const int attributeCode = 41;
            const int inputKey = 8401;
            const int outputKey = 8402;
            const int calculationCode = 8403;
            var source = CreateAscWithAttribute(attrSetCode, attributeCode, baseValue: 1f, currentValue: 5f);
            var target = CreateAscWithAttribute(attrSetCode, attributeCode, baseValue: 1f, currentValue: 9f);
            var ge = CreateEffectWithModifier(1f);
            _em.AddComponentData(ge, CreateContext(source, target));
            _em.AddComponentData(ge, CreateSpec(stackCount: 2));
            _em.AddBuffer<BSetByCallerValue>(ge).Add(new BSetByCallerValue
            {
                Key = inputKey,
                Value = 3f,
            });
            _em.AddBuffer<BExecutionCalculationDefinition>(ge).Add(new BExecutionCalculationDefinition
            {
                CalculationCode = calculationCode,
                OutputKey = outputKey,
                PreAdd = 2f,
                Coefficient = 2f,
                PostAdd = 1f,
            });
            var inputs = _em.AddBuffer<BExecutionCalculationInputDefinition>(ge);
            inputs.Add(new BExecutionCalculationInputDefinition
            {
                CalculationCode = calculationCode,
                InputIndex = 1,
                Source = EExecutionCalculationInputSource.SetByCaller,
                Key = inputKey,
                Coefficient = 2f,
            });
            inputs.Add(new BExecutionCalculationInputDefinition
            {
                CalculationCode = calculationCode,
                InputIndex = 2,
                Source = EExecutionCalculationInputSource.SourceAttribute,
                AttributeSetCode = attrSetCode,
                AttributeCode = attributeCode,
                PreAdd = 1f,
            });
            inputs.Add(new BExecutionCalculationInputDefinition
            {
                CalculationCode = calculationCode,
                InputIndex = 3,
                Source = EExecutionCalculationInputSource.StackCount,
                PostAdd = 4f,
            });
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.ExecutionCalculation,
                Key = outputKey,
            });

            RunCommandGroup();

            var outputs = _em.GetBuffer<BExecutionCalculationValue>(ge);
            Assert.That(outputs.Length, Is.EqualTo(1));
            Assert.That(outputs[0].Key, Is.EqualTo(outputKey));
            Assert.That(outputs[0].Value, Is.EqualTo(41f));

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(41f));

            var updated = FindGameplayEvent(EGameplayEventType.ExecutionCalculationOutputUpdated, ge, calculationCode);
            Assert.That(updated.Value, Is.EqualTo(41f));
        }

        [Test]
        public void ExecutionCalculationSystemUsesInputFallbackInsideAggregate()
        {
            const int outputKey = 8501;
            const int calculationCode = 8502;
            var ge = CreateEffectWithModifier(1f);
            _em.AddComponentData(ge, CreateContext());
            _em.AddComponentData(ge, CreateSpec());
            _em.AddBuffer<BExecutionCalculationDefinition>(ge).Add(new BExecutionCalculationDefinition
            {
                CalculationCode = calculationCode,
                OutputKey = outputKey,
            });
            var inputs = _em.AddBuffer<BExecutionCalculationInputDefinition>(ge);
            inputs.Add(new BExecutionCalculationInputDefinition
            {
                CalculationCode = calculationCode,
                InputIndex = 1,
                Source = EExecutionCalculationInputSource.Constant,
                ConstantValue = 3f,
            });
            inputs.Add(new BExecutionCalculationInputDefinition
            {
                CalculationCode = calculationCode,
                InputIndex = 2,
                Source = EExecutionCalculationInputSource.SetByCaller,
                Key = 8503,
                FallbackValue = 4f,
                Coefficient = 2f,
            });
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.ExecutionCalculation,
                Key = outputKey,
            });

            RunCommandGroup();

            var outputs = _em.GetBuffer<BExecutionCalculationValue>(ge);
            Assert.That(outputs.Length, Is.EqualTo(1));
            Assert.That(outputs[0].Value, Is.EqualTo(11f));

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(11f));

            var missing = FindGameplayEvent(EGameplayEventType.ExecutionCalculationInputMissing, ge, calculationCode);
            Assert.That(missing.Value, Is.EqualTo(4f));

            var updated = FindGameplayEvent(EGameplayEventType.ExecutionCalculationOutputUpdated, ge, calculationCode);
            Assert.That(updated.Value, Is.EqualTo(11f));
        }

        [Test]
        public void ExecutionCalculationSystemAppendsOutputModifierWithoutStaticModifierConfig()
        {
            const int outputKey = 8601;
            const int calculationCode = 8602;
            const int attrSetCode = 86;
            const int attributeCode = 87;
            var ge = CreateEntity();
            _em.AddComponentData(ge, CreateContext());
            _em.AddComponentData(ge, CreateSpec());
            _em.AddBuffer<BExecutionCalculationDefinition>(ge).Add(new BExecutionCalculationDefinition
            {
                CalculationCode = calculationCode,
                OutputKey = outputKey,
                Source = EExecutionCalculationInputSource.Constant,
                ConstantValue = 6f,
            });
            _em.AddBuffer<BExecutionCalculationOutputModifierDefinition>(ge).Add(new BExecutionCalculationOutputModifierDefinition
            {
                CalculationCode = calculationCode,
                OutputKey = outputKey,
                AttrSetCode = attrSetCode,
                AttributeCode = attributeCode,
                Op = EModifierOp.Subtract,
                PreAdd = 1f,
                Coefficient = 2f,
                PostAdd = 3f,
            });

            RunCommandGroup();

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].AttrSetCode, Is.EqualTo(attrSetCode));
            Assert.That(resolved[0].AttributeCode, Is.EqualTo(attributeCode));
            Assert.That(resolved[0].Op, Is.EqualTo(EModifierOp.Subtract));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(17f));
            Assert.That(resolved[0].SourceEffect, Is.EqualTo(ge));

            var updated = FindGameplayEvent(EGameplayEventType.ExecutionCalculationOutputUpdated, ge, calculationCode);
            Assert.That(updated.Value, Is.EqualTo(6f));
        }

        [Test]
        public void ExecutionCalculationOutputModifierSystemConsumesExternalOutputValue()
        {
            const int outputKey = 8651;
            const int calculationCode = 8652;
            const int attrSetCode = 86;
            const int attributeCode = 89;
            var ge = CreateEntity();
            _em.AddComponentData(ge, CreateContext());
            _em.AddComponentData(ge, CreateSpec());
            _em.AddBuffer<BExecutionCalculationValue>(ge).Add(new BExecutionCalculationValue
            {
                Key = outputKey,
                Value = 7f,
            });
            _em.AddBuffer<BExecutionCalculationOutputModifierDefinition>(ge).Add(new BExecutionCalculationOutputModifierDefinition
            {
                CalculationCode = calculationCode,
                OutputKey = outputKey,
                AttrSetCode = attrSetCode,
                AttributeCode = attributeCode,
                Op = EModifierOp.Add,
                PreAdd = 2f,
                Coefficient = 3f,
                PostAdd = 1f,
            });

            RunCommandGroup();

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].AttrSetCode, Is.EqualTo(attrSetCode));
            Assert.That(resolved[0].AttributeCode, Is.EqualTo(attributeCode));
            Assert.That(resolved[0].Op, Is.EqualTo(EModifierOp.Add));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(28f));
            Assert.That(resolved[0].SourceEffect, Is.EqualTo(ge));
        }

        [Test]
        public void ExecutionCalculationOutputModifierSystemUsesFallbackWhenOutputIsMissing()
        {
            const int outputKey = 8661;
            const int calculationCode = 8662;
            const int attrSetCode = 86;
            const int attributeCode = 90;
            var ge = CreateEntity();
            _em.AddComponentData(ge, CreateContext());
            _em.AddComponentData(ge, CreateSpec());
            _em.AddBuffer<BExecutionCalculationOutputModifierDefinition>(ge).Add(new BExecutionCalculationOutputModifierDefinition
            {
                CalculationCode = calculationCode,
                OutputKey = outputKey,
                AttrSetCode = attrSetCode,
                AttributeCode = attributeCode,
                Op = EModifierOp.Subtract,
                FallbackMagnitude = 5f,
                Coefficient = 2f,
            });

            RunCommandGroup();

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].AttrSetCode, Is.EqualTo(attrSetCode));
            Assert.That(resolved[0].AttributeCode, Is.EqualTo(attributeCode));
            Assert.That(resolved[0].Op, Is.EqualTo(EModifierOp.Subtract));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(10f));

            var missing = FindGameplayEvent(EGameplayEventType.ExecutionCalculationOutputMissing, ge, calculationCode);
            Assert.That(missing.Value, Is.EqualTo(5f));
        }

        [Test]
        public void ExecutionCalculationSystemSyncsOutputModifierToActiveEffect()
        {
            const int outputKey = 8701;
            const int calculationCode = 8702;
            const int attrSetCode = 87;
            const int attributeCode = 88;
            var target = CreateAscWithAttribute(attrSetCode, attributeCode, baseValue: 100f, currentValue: 100f);
            _em.AddBuffer<BActiveModifier>(target);
            var ge = CreateEntity();
            _em.AddComponentData(ge, CreateContext(default, target));
            _em.AddComponentData(ge, CreateSpec());
            _em.AddComponentData(ge, new CEffectLifecycle
            {
                State = EGameplayEffectLifecycleState.Active,
                PreviousState = EGameplayEffectLifecycleState.Active,
            });
            _em.AddBuffer<BExecutionCalculationDefinition>(ge).Add(new BExecutionCalculationDefinition
            {
                CalculationCode = calculationCode,
                OutputKey = outputKey,
                Source = EExecutionCalculationInputSource.Constant,
                ConstantValue = 12f,
            });
            _em.AddBuffer<BExecutionCalculationOutputModifierDefinition>(ge).Add(new BExecutionCalculationOutputModifierDefinition
            {
                CalculationCode = calculationCode,
                OutputKey = outputKey,
                AttrSetCode = attrSetCode,
                AttributeCode = attributeCode,
                Op = EModifierOp.Add,
            });

            RunCommandGroup();

            var activeModifiers = _em.GetBuffer<BActiveModifier>(target);
            Assert.That(activeModifiers.Length, Is.EqualTo(1));
            Assert.That(activeModifiers[0].AttrSetCode, Is.EqualTo(attrSetCode));
            Assert.That(activeModifiers[0].AttributeCode, Is.EqualTo(attributeCode));
            Assert.That(activeModifiers[0].SourceEntity, Is.EqualTo(ge));
            Assert.That(activeModifiers[0].Magnitude, Is.EqualTo(12f));

            var attributes = _em.GetBuffer<BAttribute>(target);
            Assert.That(attributes[0].Dirty, Is.True);
        }

        [Test]
        public void ResolveModifiersUsesSourceAttributeSnapshotMagnitude()
        {
            const int attrSetCode = 11;
            const int attributeCode = 22;
            var source = CreateAscWithAttribute(attrSetCode, attributeCode, baseValue: 10f, currentValue: 42f);
            var target = CreateAscWithAttribute(attrSetCode, attributeCode, baseValue: 20f, currentValue: 7f);
            var ge = CreateEffectWithModifier(1f);
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.SourceAttribute,
                AttributeSetCode = attrSetCode,
                AttributeCode = attributeCode,
            });

            var context = CreateContext(source, target);
            var spec = CreateSpec();

            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, spec);

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(42f));
        }

        [Test]
        public void ResolveModifiersKeepsSourceAttributeSnapshotAcrossResolverRuns()
        {
            const int attrSetCode = 13;
            const int attributeCode = 24;
            var source = CreateAscWithAttribute(attrSetCode, attributeCode, baseValue: 10f, currentValue: 42f);
            var target = CreateAscWithAttribute(attrSetCode, attributeCode, baseValue: 20f, currentValue: 7f);
            var ge = CreateEffectWithModifier(1f);
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.SourceAttribute,
                AttributeSetCode = attrSetCode,
                AttributeCode = attributeCode,
            });

            var context = CreateContext(source, target);
            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, CreateSpec(stackCount: 1));

            SetAttributeCurrentValue(source, attrSetCode, attributeCode, 100f);
            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, CreateSpec(stackCount: 2));

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(42f));

            var captures = _em.GetBuffer<BAttributeCaptureValue>(ge);
            Assert.That(captures.Length, Is.EqualTo(1));
            Assert.That(captures[0].Value, Is.EqualTo(42f));
        }

        [Test]
        public void ResolveModifiersCanReadCurrentAttributeWithoutSnapshotCapture()
        {
            const int attrSetCode = 14;
            const int attributeCode = 25;
            var source = CreateAscWithAttribute(attrSetCode, attributeCode, baseValue: 10f, currentValue: 1f);
            var target = CreateAscWithAttribute(attrSetCode, attributeCode, baseValue: 20f, currentValue: 11f);
            var ge = CreateEffectWithModifier(1f);
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.TargetAttribute,
                AttributeSetCode = attrSetCode,
                AttributeCode = attributeCode,
                CaptureTiming = EAttributeCaptureTiming.CurrentValue,
            });

            var context = CreateContext(source, target);
            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, CreateSpec());

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(11f));
            Assert.That(_em.HasBuffer<BAttributeCaptureValue>(ge), Is.False);

            SetAttributeCurrentValue(target, attrSetCode, attributeCode, 17f);
            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, CreateSpec());

            resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(17f));
            Assert.That(_em.HasBuffer<BAttributeCaptureValue>(ge), Is.False);
        }

        [Test]
        public void ResolveModifiersUsesTargetAttributeSnapshotMagnitude()
        {
            const int attrSetCode = 12;
            const int attributeCode = 23;
            var source = CreateAscWithAttribute(attrSetCode, attributeCode, baseValue: 10f, currentValue: 5f);
            var target = CreateAscWithAttribute(attrSetCode, attributeCode, baseValue: 20f, currentValue: 33f);
            var ge = CreateEffectWithModifier(1f);
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.TargetAttribute,
                AttributeSetCode = attrSetCode,
                AttributeCode = attributeCode,
                PreAdd = 2f,
                Coefficient = 2f,
                PostAdd = 1f,
            });

            var context = CreateContext(source, target);
            var spec = CreateSpec();

            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, spec);

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(71f));
        }

        [Test]
        public void ResolveModifiersUsesFallbackWhenAttributeCaptureIsMissing()
        {
            var source = CreateEntity();
            var target = CreateEntity();
            var ge = CreateEffectWithModifier(1f);
            _em.AddBuffer<BMagnitudeDefinition>(ge).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.SourceAttribute,
                AttributeSetCode = 99,
                AttributeCode = 100,
                FallbackMagnitude = 6f,
            });

            var context = CreateContext(source, target);
            var spec = CreateSpec();

            EffectMagnitudeResolver.ResolveModifiers(_em, ge, context, spec);

            var resolved = _em.GetBuffer<BResolvedModifier>(ge);
            Assert.That(resolved.Length, Is.EqualTo(1));
            Assert.That(resolved[0].Magnitude, Is.EqualTo(6f));
        }

        private Entity CreateEffectWithModifier(float magnitude)
        {
            var ge = CreateEntity();
            _em.AddBuffer<BModifierConfig>(ge).Add(new BModifierConfig
            {
                AttrSetCode = 1,
                AttributeCode = 2,
                Op = EModifierOp.Add,
                Magnitude = magnitude,
            });
            return ge;
        }

        private Entity CreateEntity()
        {
            var entity = _em.CreateEntity();
            _entities.Add(entity);
            return entity;
        }

        private Entity CreateAscWithAttribute(
            int attrSetCode,
            int attributeCode,
            float baseValue,
            float currentValue)
        {
            var asc = CreateEntity();
            _em.AddBuffer<BAttribute>(asc).Add(new BAttribute
            {
                AttrSetCode = attrSetCode,
                Code = attributeCode,
                BaseValue = baseValue,
                CurrentValue = currentValue,
            });
            return asc;
        }

        private void SetAttributeCurrentValue(
            Entity asc,
            int attrSetCode,
            int attributeCode,
            float currentValue)
        {
            var attributes = _em.GetBuffer<BAttribute>(asc);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode != attrSetCode || attribute.Code != attributeCode)
                    continue;

                attribute.CurrentValue = currentValue;
                attributes[i] = attribute;
                return;
            }

            Assert.Fail($"Attribute {attrSetCode}:{attributeCode} was not found.");
        }

        private void RunCommandGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>().Update();
        }

        private void ClearGameplayEvents()
        {
            if (_em.Exists(GASManager.EntityEventBus)
                && _em.HasBuffer<BGameplayEvent>(GASManager.EntityEventBus))
            {
                _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus).Clear();
            }
        }

        private BGameplayEvent FindGameplayEvent(EGameplayEventType type, Entity ge, int eventCode)
        {
            var events = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Type == type
                    && evt.GameplayEffect == ge
                    && evt.EventCode == eventCode)
                {
                    return evt;
                }
            }

            Assert.Fail($"Gameplay event {type} for {eventCode} was not found.");
            return default;
        }

        private static CEffectContext CreateContext(Entity sourceAsc = default, Entity targetAsc = default)
        {
            return new CEffectContext
            {
                SourceAsc = sourceAsc,
                TargetAsc = targetAsc,
                ContextId = 1,
                TargetDataKind = ETargetDataKind.Self,
            };
        }

        private static CEffectSpecData CreateSpec(int stackCount = 1)
        {
            return new CEffectSpecData
            {
                GameplayEffectCode = 10001,
                Level = 1,
                StackCount = stackCount,
            };
        }
    }
}
