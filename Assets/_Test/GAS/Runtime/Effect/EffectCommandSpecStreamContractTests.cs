using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.Runtime.Tests.Effect
{
    public sealed class EffectCommandSpecStreamContractTests
    {
        private World _world;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _world = new World("EffectCommandSpecStreamContractTests");
            _em = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void StreamDataUsesUnityEcsBufferContract()
        {
            Assert.That(typeof(IComponentData).IsAssignableFrom(typeof(CEffectCommandSpecStream)), Is.True);
            AssertBufferOnly(typeof(BEffectCommand));
            AssertBufferOnly(typeof(BEffectCommandSetByCallerValue));
            AssertBufferOnly(typeof(BInstantEffectSpec));
            AssertBufferOnly(typeof(BAttributeDelta));
            AssertBufferOnly(typeof(BActiveEffectMutation));
            AssertBufferOnly(typeof(BTypedSimulationFact));
        }

        [Test]
        public void CommandSpecDeltaAndFactCarryGasContextContinuity()
        {
            AssertHasFields(
                typeof(BEffectCommand),
                nameof(BEffectCommand.SourceAsc),
                nameof(BEffectCommand.TargetAsc),
                nameof(BEffectCommand.SourceAbility),
                nameof(BEffectCommand.SourceEffect),
                nameof(BEffectCommand.GameplayEffectCode),
                nameof(BEffectCommand.ContextId),
                nameof(BEffectCommand.ParentContextId),
                nameof(BEffectCommand.TargetDataKind),
                nameof(BEffectCommand.SetByCallerStart),
                nameof(BEffectCommand.SetByCallerCount));
            AssertHasFields(
                typeof(BInstantEffectSpec),
                nameof(BInstantEffectSpec.SourceCommandSequence),
                nameof(BInstantEffectSpec.SourceAsc),
                nameof(BInstantEffectSpec.TargetAsc),
                nameof(BInstantEffectSpec.SourceAbility),
                nameof(BInstantEffectSpec.SourceEffect),
                nameof(BInstantEffectSpec.GameplayEffectCode),
                nameof(BInstantEffectSpec.ContextId),
                nameof(BInstantEffectSpec.ParentContextId),
                nameof(BInstantEffectSpec.SetByCallerStart),
                nameof(BInstantEffectSpec.SetByCallerCount));
            AssertHasFields(
                typeof(BAttributeDelta),
                nameof(BAttributeDelta.SourceCommandSequence),
                nameof(BAttributeDelta.SourceSpecSequence),
                nameof(BAttributeDelta.SourceAsc),
                nameof(BAttributeDelta.TargetAsc),
                nameof(BAttributeDelta.SourceAbility),
                nameof(BAttributeDelta.SourceEffect),
                nameof(BAttributeDelta.GameplayEffectCode),
                nameof(BAttributeDelta.ContextId),
                nameof(BAttributeDelta.ParentContextId),
                nameof(BAttributeDelta.AttrSetCode),
                nameof(BAttributeDelta.AttributeCode));
            AssertHasFields(
                typeof(BTypedSimulationFact),
                nameof(BTypedSimulationFact.SourceCommandSequence),
                nameof(BTypedSimulationFact.SourceSpecSequence),
                nameof(BTypedSimulationFact.SourceDeltaSequence),
                nameof(BTypedSimulationFact.SourceAsc),
                nameof(BTypedSimulationFact.TargetAsc),
                nameof(BTypedSimulationFact.SourceAbility),
                nameof(BTypedSimulationFact.SourceEffect),
                nameof(BTypedSimulationFact.GameplayEffectCode),
                nameof(BTypedSimulationFact.ContextId),
                nameof(BTypedSimulationFact.ParentContextId),
                nameof(BTypedSimulationFact.Domain),
                nameof(BTypedSimulationFact.Category),
                nameof(BTypedSimulationFact.Severity));
            AssertHasFields(
                typeof(BEffectCommandSetByCallerValue),
                nameof(BEffectCommandSetByCallerValue.CommandSequence),
                nameof(BEffectCommandSetByCallerValue.SpecSequence),
                nameof(BEffectCommandSetByCallerValue.Key),
                nameof(BEffectCommandSetByCallerValue.Value));
        }

        [Test]
        public void BridgeCommandWritesStreamWithoutPerCommandEntityLifecycle()
        {
            var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);

            var sourceAsc = _em.CreateEntity();
            var targetAsc = _em.CreateEntity();
            var sourceAbility = _em.CreateEntity();

            var command = EffectCommandSpecStream.AppendLegacyRequestBridgeCommand(
                _em,
                new CApplyGameplayEffectRequest
                {
                    SourceAsc = sourceAsc,
                    SourceAbility = sourceAbility,
                    GameplayEffectCode = 9001,
                    Level = 3,
                    ParentContextId = 12,
                },
                targetAsc,
                ETargetDataKind.Entity,
                new[]
                {
                    new BSetByCallerValue
                    {
                        Key = 77,
                        Value = 12.5f,
                    },
                });

            Assert.That(CountEntitiesWith<CApplyGameplayEffectRequest>(), Is.EqualTo(0));
            Assert.That(CountEntitiesWith<CEffectSpecData>(), Is.EqualTo(0));
            Assert.That(CountEntitiesWith<CEffectLifecycle>(), Is.EqualTo(0));

            Assert.That(command.Sequence, Is.GreaterThan(0));
            Assert.That(command.ContextId, Is.GreaterThan(0));
            Assert.That(command.ParentContextId, Is.EqualTo(12));
            Assert.That(command.SetByCallerStart, Is.EqualTo(0));
            Assert.That(command.SetByCallerCount, Is.EqualTo(1));

            var commands = _em.GetBuffer<BEffectCommand>(streamEntity);
            var setByCallerValues = _em.GetBuffer<BEffectCommandSetByCallerValue>(streamEntity);
            Assert.That(commands.Length, Is.EqualTo(1));
            Assert.That(commands[0].GameplayEffectCode, Is.EqualTo(9001));
            Assert.That(commands[0].TargetAsc, Is.EqualTo(targetAsc));
            Assert.That(setByCallerValues.Length, Is.EqualTo(1));
            Assert.That(setByCallerValues[0].CommandSequence, Is.EqualTo(command.Sequence));
            Assert.That(setByCallerValues[0].Key, Is.EqualTo(77));
            Assert.That(setByCallerValues[0].Value, Is.EqualTo(12.5f));
        }

        [Test]
        public void PipelineContractReferencesConcreteAm2Types()
        {
            Assert.That(
                GameplayEffectRuntimePipelineContract.TryFind(
                    GameplayEffectRuntimePipelineKind.EffectCommandSpecStream,
                    out var target),
                Is.True);
            Assert.That(target.WriteEntryType, Is.EqualTo(typeof(BEffectCommand)));
            Assert.That(target.EvaluationType, Is.EqualTo(typeof(BInstantEffectSpec)));
            Assert.That(target.OutputType, Is.EqualTo(typeof(BAttributeDelta)));
            Assert.That(target.FactType, Is.EqualTo(typeof(BTypedSimulationFact)));
            Assert.That(
                target.HasRestriction(GameplayEffectRuntimePipelineRestriction.AvoidsRuntimeGameplayEffectEntity),
                Is.True);
        }

        [Test]
        public void ScheduleAndLayoutExposeAm2TargetPhases()
        {
            AssertContainsInOrder(
                GASSystemScheduleContract.CommandSystems,
                typeof(SEffectCommandIngest),
                typeof(SInstantEffectSpecBuild),
                typeof(SActiveEffectMutationApply),
                typeof(SAttributeDeltaApply),
                typeof(STypedSimulationFactProjection));
            AssertContainsInOrder(
                GASSystemScheduleContract.EffectCommandSpecStreamTargetSystems,
                typeof(SEffectCommandIngest),
                typeof(SInstantEffectSpecBuild),
                typeof(SActiveEffectMutationApply),
                typeof(SAttributeDeltaApply),
                typeof(STypedSimulationFactProjection));

            var plan = GASRuntimeQueryLayoutPlanner.CreateCurrent();
            Assert.That(
                plan.TryFind(GASRuntimeQueryLayoutEntryId.GameplayEffectCommandSpecStream, out var entry),
                Is.True);
            Assert.That(entry.EntityKind, Is.EqualTo(GASRuntimeEntityKind.RuntimeCoreStream));
            Assert.That(entry.Decision, Is.EqualTo(GASRuntimeLayoutDecision.TargetContract));
            Assert.That(entry.HasCapability(GASRuntimeLayoutCapability.CommandDataBacked), Is.True);
            Assert.That(entry.HasCapability(GASRuntimeLayoutCapability.NoPerHitStructuralChange), Is.True);
            Assert.That(entry.HasCapability(GASRuntimeLayoutCapability.JobCandidate), Is.True);
            Assert.That(entry.HasCapability(GASRuntimeLayoutCapability.BurstCandidate), Is.True);
            Assert.That(entry.HasBoundary(GASRuntimeLayoutBoundary.HighFrequencyCommandDataBoundary), Is.True);
            Assert.That(entry.HasBoundary(GASRuntimeLayoutBoundary.StructuralEntityManagerHotspot), Is.False);
            Assert.That(entry.HasRequiredSlot(GASRuntimeLayoutComponentSlot.EffectCommandBuffer), Is.True);
            Assert.That(entry.HasRequiredSlot(GASRuntimeLayoutComponentSlot.InstantEffectSpecBuffer), Is.True);
            Assert.That(entry.HasRequiredSlot(GASRuntimeLayoutComponentSlot.AttributeDeltaBuffer), Is.True);
            Assert.That(entry.HasRequiredSlot(GASRuntimeLayoutComponentSlot.TypedSimulationFactBuffer), Is.True);
            Assert.That(entry.HasOptionalSlot(GASRuntimeLayoutComponentSlot.EffectCommandSetByCallerBuffer), Is.True);
        }

        [Test]
        public void DebuggerCountersPreferAuthorityStreamCountsWhenPresent()
        {
            var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
            var command = EffectCommandSpecStream.AppendCommand(_em, new BEffectCommand
            {
                Source = EEffectCommandSource.Ability,
                GameplayEffectCode = 300,
            });
            _em.GetBuffer<BInstantEffectSpec>(streamEntity).Add(new BInstantEffectSpec
            {
                Sequence = 1,
                SourceCommandSequence = command.Sequence,
                ContextId = command.ContextId,
            });
            _em.GetBuffer<BAttributeDelta>(streamEntity).Add(new BAttributeDelta
            {
                Sequence = 1,
                SourceCommandSequence = command.Sequence,
                SourceSpecSequence = 1,
                ContextId = command.ContextId,
            });
            _em.GetBuffer<BTypedSimulationFact>(streamEntity).Add(new BTypedSimulationFact
            {
                Sequence = 1,
                SourceCommandSequence = command.Sequence,
                SourceSpecSequence = 1,
                SourceDeltaSequence = 1,
                ContextId = command.ContextId,
                Domain = EGameplayFactDomain.GameplayEffect,
                Category = EGameplayFactCategory.StateChange,
                Severity = EGameplayFactSeverity.Info,
            });

            var counters = GasRuntimeDebugger.CollectRuntimeCoreCounters(_em, Entity.Null, Entity.Null);

            Assert.That(counters.RequestCount, Is.EqualTo(1));
            Assert.That(counters.SpecCount, Is.EqualTo(1));
            Assert.That(counters.DeltaCount, Is.EqualTo(1));
            Assert.That(counters.FactCount, Is.EqualTo(1));
            Assert.That(counters.EntityCreateCount, Is.EqualTo(0));
            Assert.That(counters.EntityDestroyCount, Is.EqualTo(0));
        }

        private int CountEntitiesWith<T>()
            where T : unmanaged, IComponentData
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private static void AssertBufferOnly(Type type)
        {
            Assert.That(typeof(IBufferElementData).IsAssignableFrom(type), Is.True, type.Name);
            Assert.That(typeof(IComponentData).IsAssignableFrom(type), Is.False, type.Name);
        }

        private static void AssertHasFields(Type type, params string[] fieldNames)
        {
            for (var i = 0; i < fieldNames.Length; i++)
            {
                Assert.That(
                    type.GetField(fieldNames[i], BindingFlags.Instance | BindingFlags.Public),
                    Is.Not.Null,
                    type.Name + " must expose " + fieldNames[i]);
            }
        }

        private static void AssertContainsInOrder(IReadOnlyList<Type> source, params Type[] expected)
        {
            var nextStart = 0;
            for (var i = 0; i < expected.Length; i++)
            {
                var index = IndexOf(source, expected[i], nextStart);
                Assert.That(index, Is.GreaterThanOrEqualTo(0), expected[i].Name);
                nextStart = index + 1;
            }
        }

        private static int IndexOf(IReadOnlyList<Type> source, Type target, int start)
        {
            for (var i = start; i < source.Count; i++)
            {
                if (source[i] == target)
                    return i;
            }

            return -1;
        }
    }

    public sealed class EffectCommandSpecStreamInstantEvaluationTests
    {
        private EntityManager _em;
        private Entity _streamEntity;

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
            _streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
            EffectCommandSpecStream.ClearFrameLocalData(_em, _streamEntity, 0);
            ConfigRegistryDiagnostics.Clear();
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
        }

        [TearDown]
        public void TearDown()
        {
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
            if (_em.Exists(_streamEntity))
                EffectCommandSpecStream.ClearFrameLocalData(_em, _streamEntity, 0);
        }

        [Test]
        public void DirectInstantCommandBuildsSpecAppliesAttributeDeltaAndProjectsTypedFact()
        {
            const int effectCode = 991031;
            const int attrSetCode = 10;
            const int attributeCode = 20;

            var source = _em.CreateEntity();
            var target = CreateAscWithAttribute(attrSetCode, attributeCode, 100f);

            try
            {
                RegisterSimpleModifierEffect(
                    effectCode,
                    attrSetCode,
                    attributeCode,
                    EModifierOp.Subtract,
                    25f);

                var command = EffectCommandSpecStream.AppendCommand(_em, new BEffectCommand
                {
                    Kind = EEffectCommandKind.Instant,
                    Source = EEffectCommandSource.Ability,
                    SourceAsc = source,
                    TargetAsc = target,
                    GameplayEffectCode = effectCode,
                    Level = 2,
                    Frame = 7,
                    ContextId = 123,
                    ParentContextId = 45,
                });

                RunCommandGroup();

                var specs = _em.GetBuffer<BInstantEffectSpec>(_streamEntity);
                var deltas = _em.GetBuffer<BAttributeDelta>(_streamEntity);
                var facts = _em.GetBuffer<BTypedSimulationFact>(_streamEntity);
                var attributes = _em.GetBuffer<BAttribute>(target);

                Assert.That(specs.Length, Is.EqualTo(1));
                Assert.That(deltas.Length, Is.EqualTo(1));
                Assert.That(facts.Length, Is.EqualTo(1));

                Assert.That(specs[0].SourceCommandSequence, Is.EqualTo(command.Sequence));
                Assert.That(specs[0].ContextId, Is.EqualTo(123));
                Assert.That(specs[0].ParentContextId, Is.EqualTo(45));

                Assert.That(attributes[0].BaseValue, Is.EqualTo(75f));
                Assert.That(attributes[0].CurrentValue, Is.EqualTo(75f));
                Assert.That(attributes[0].PreviousCurrentValue, Is.EqualTo(100f));
                Assert.That(attributes[0].Dirty, Is.True);
                Assert.That(attributes[0].CurrentValueChangePending, Is.True);

                Assert.That(deltas[0].SourceCommandSequence, Is.EqualTo(command.Sequence));
                Assert.That(deltas[0].SourceSpecSequence, Is.EqualTo(specs[0].Sequence));
                Assert.That(deltas[0].Magnitude, Is.EqualTo(25f));
                Assert.That(deltas[0].OldValue, Is.EqualTo(100f));
                Assert.That(deltas[0].NewValue, Is.EqualTo(75f));

                Assert.That(facts[0].SourceCommandSequence, Is.EqualTo(command.Sequence));
                Assert.That(facts[0].SourceSpecSequence, Is.EqualTo(specs[0].Sequence));
                Assert.That(facts[0].SourceDeltaSequence, Is.EqualTo(deltas[0].Sequence));
                Assert.That(facts[0].EventType, Is.EqualTo(EGameplayEventType.AttributeBaseValueChanged));
                Assert.That(facts[0].Domain, Is.EqualTo(EGameplayFactDomain.Attribute));
                Assert.That(facts[0].OldValue, Is.EqualTo(100f));
                Assert.That(facts[0].NewValue, Is.EqualTo(75f));

                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                Assert.That(CountRuntimeEffectSpecsByCode(effectCode), Is.EqualTo(0));
            }
            finally
            {
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void DirectInstantCommandResolvesSetByCallerMagnitudeAndCarriesSpecSequence()
        {
            const int effectCode = 991032;
            const int attrSetCode = 11;
            const int attributeCode = 21;
            const int magnitudeKey = 6001;

            var source = _em.CreateEntity();
            var target = CreateAscWithAttribute(attrSetCode, attributeCode, 40f);

            try
            {
                RegisterSetByCallerModifierEffect(
                    effectCode,
                    attrSetCode,
                    attributeCode,
                    magnitudeKey);

                var command = EffectCommandSpecStream.AppendCommand(
                    _em,
                    new BEffectCommand
                    {
                        Kind = EEffectCommandKind.Instant,
                        Source = EEffectCommandSource.Passive,
                        SourceAsc = source,
                        TargetAsc = target,
                        GameplayEffectCode = effectCode,
                        Frame = 8,
                        ParentContextId = 77,
                    },
                    new[]
                    {
                        new BSetByCallerValue
                        {
                            Key = magnitudeKey,
                            Value = 5f,
                        },
                    });

                RunCommandGroup();

                var specs = _em.GetBuffer<BInstantEffectSpec>(_streamEntity);
                var deltas = _em.GetBuffer<BAttributeDelta>(_streamEntity);
                var facts = _em.GetBuffer<BTypedSimulationFact>(_streamEntity);
                var setByCallers = _em.GetBuffer<BEffectCommandSetByCallerValue>(_streamEntity);
                var attributes = _em.GetBuffer<BAttribute>(target);

                Assert.That(specs.Length, Is.EqualTo(1));
                Assert.That(deltas.Length, Is.EqualTo(1));
                Assert.That(facts.Length, Is.EqualTo(1));
                Assert.That(setByCallers.Length, Is.EqualTo(1));

                Assert.That(setByCallers[0].CommandSequence, Is.EqualTo(command.Sequence));
                Assert.That(setByCallers[0].SpecSequence, Is.EqualTo(specs[0].Sequence));
                Assert.That(deltas[0].Magnitude, Is.EqualTo(16f));
                Assert.That(deltas[0].ContextId, Is.EqualTo(command.ContextId));
                Assert.That(deltas[0].ParentContextId, Is.EqualTo(77));
                Assert.That(attributes[0].BaseValue, Is.EqualTo(56f));
                Assert.That(attributes[0].CurrentValue, Is.EqualTo(56f));
                Assert.That(facts[0].Value, Is.EqualTo(16f));
                Assert.That(facts[0].NewValue, Is.EqualTo(56f));

                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                Assert.That(CountRuntimeEffectSpecsByCode(effectCode), Is.EqualTo(0));
            }
            finally
            {
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        private Entity CreateAscWithAttribute(int attrSetCode, int attributeCode, float value)
        {
            var asc = _em.CreateEntity();
            _em.AddBuffer<BAttribute>(asc).Add(new BAttribute
            {
                AttrSetCode = attrSetCode,
                Code = attributeCode,
                BaseValue = value,
                CurrentValue = value,
                PreviousCurrentValue = value,
            });
            return asc;
        }

        private static void RegisterSimpleModifierEffect(
            int effectCode,
            int attrSetCode,
            int attributeCode,
            EModifierOp operation,
            float magnitude)
        {
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
                    ? new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                    {
                        new ConfModifierConfig
                        {
                            ModifierSettings = new[]
                            {
                                new ModifierDefinitionSetting
                                {
                                    AttrSetCode = attrSetCode,
                                    AttrCode = attributeCode,
                                    Operation = operation,
                                    Magnitude = magnitude,
                                },
                            },
                        },
                    })
                    : null);
        }

        private static void RegisterSetByCallerModifierEffect(
            int effectCode,
            int attrSetCode,
            int attributeCode,
            int magnitudeKey)
        {
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
                    ? new GameplayEffectConfig(new GameplayEffectComponentConfig[]
                    {
                        new ConfModifierConfig
                        {
                            ModifierSettings = new[]
                            {
                                new ModifierDefinitionSetting
                                {
                                    AttrSetCode = attrSetCode,
                                    AttrCode = attributeCode,
                                    Operation = EModifierOp.Add,
                                    Magnitude = 0f,
                                },
                            },
                        },
                        new MagnitudeDefinitionConfig
                        {
                            Definitions = new[]
                            {
                                new BMagnitudeDefinition
                                {
                                    ModifierIndex = 0,
                                    Source = EMagnitudeSource.SetByCaller,
                                    Key = magnitudeKey,
                                    FallbackMagnitude = 2f,
                                    Coefficient = 2f,
                                    PreAdd = 1f,
                                    PostAdd = 4f,
                                },
                            },
                        },
                    })
                    : null);
        }

        private static void RunCommandGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>().Update();
        }

        private int CountApplyRequestsByCode(int effectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CApplyGameplayEffectRequest>());
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var count = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                if (_em.GetComponentData<CApplyGameplayEffectRequest>(entities[i]).GameplayEffectCode == effectCode)
                    count++;
            }

            return count;
        }

        private int CountRuntimeEffectSpecsByCode(int effectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CEffectSpecData>());
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var count = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                if (_em.GetComponentData<CEffectSpecData>(entities[i]).GameplayEffectCode == effectCode)
                    count++;
            }

            return count;
        }

        private void DestroyIfExists(Entity entity)
        {
            if (entity != Entity.Null && _em.Exists(entity))
                _em.DestroyEntity(entity);
        }

        private sealed class MagnitudeDefinitionConfig : GameplayEffectComponentConfig
        {
            public BMagnitudeDefinition[] Definitions;

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                var buffer = _entityManager.HasBuffer<BMagnitudeDefinition>(ge)
                    ? _entityManager.GetBuffer<BMagnitudeDefinition>(ge)
                    : _entityManager.AddBuffer<BMagnitudeDefinition>(ge);
                buffer.Clear();

                if (Definitions == null)
                    return;

                for (var i = 0; i < Definitions.Length; i++)
                    buffer.Add(Definitions[i]);
            }
        }
    }
}
