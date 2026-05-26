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
            AssertHasFields(
                typeof(CEffectCommandSpecStream),
                nameof(CEffectCommandSpecStream.EventBridgeFactCursor),
                nameof(CEffectCommandSpecStream.CueProjectionSpecCursor));
            AssertBufferOnly(typeof(BEffectCommand));
            AssertBufferOnly(typeof(BEffectCommandSetByCallerValue));
            AssertBufferOnly(typeof(BInstantEffectSpec));
            AssertBufferOnly(typeof(BAttributeDelta));
            AssertBufferOnly(typeof(BActiveEffectMutation));
            AssertBufferOnly(typeof(BTypedSimulationFact));
        }

        [Test]
        public void TryGetSingletonOnlyResolvesRegisteredStreamOwner()
        {
            var streamEntity = _em.CreateEntity();
            _em.AddComponentData(streamEntity, new CEffectCommandSpecStream
            {
                Version = EffectCommandSpecStream.CurrentVersion,
                NextContextId = 1,
                NextCommandSequence = 1,
                NextSpecSequence = 1,
                NextDeltaSequence = 1,
                NextFactSequence = 1,
            });

            Assert.That(EffectCommandSpecStream.TryGetSingleton(_em, out _), Is.False);

            EffectCommandSpecStream.RegisterKnownSingleton(_em, streamEntity);

            Assert.That(EffectCommandSpecStream.TryGetSingleton(_em, out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(streamEntity));
        }

        [Test]
        public void EnsureSingletonRegistersKnownStreamOwner()
        {
            Assert.That(EffectCommandSpecStream.TryGetSingleton(_em, out _), Is.False);

            var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);

            Assert.That(EffectCommandSpecStream.TryGetSingleton(_em, out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(streamEntity));
            Assert.That(EffectCommandSpecStream.EnsureSingleton(_em), Is.EqualTo(streamEntity));
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
                nameof(BInstantEffectSpec.CueRequestOnApplyCode),
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
                typeof(STypedSimulationFactProjection),
                typeof(STypedSimulationFactEventBridge),
                typeof(SInstantEffectCueRequestProjection),
                typeof(SAscDestroyRequest));
            AssertContainsInOrder(
                GASSystemScheduleContract.EffectCommandSpecStreamTargetSystems,
                typeof(SEffectCommandIngest),
                typeof(SInstantEffectSpecBuild),
                typeof(SActiveEffectMutationApply),
                typeof(SAttributeDeltaApply),
                typeof(STypedSimulationFactProjection));
            Assert.That(
                GASSystemScheduleContract.TryGetRuntimeCoreFramePhase(
                    typeof(STypedSimulationFactEventBridge),
                    out var bridgePhase),
                Is.True);
            Assert.That(bridgePhase, Is.EqualTo(EGasRuntimeCoreFramePhase.TypedFactProjection));
            Assert.That(
                GASSystemScheduleContract.TryGetRuntimeCoreFramePhase(
                    typeof(SInstantEffectCueRequestProjection),
                    out var cueProjectionPhase),
                Is.True);
            Assert.That(cueProjectionPhase, Is.EqualTo(EGasRuntimeCoreFramePhase.TypedFactProjection));

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
            Assert.That(
                IndexOf(entry.SystemTypes, typeof(STypedSimulationFactEventBridge), 0),
                Is.GreaterThanOrEqualTo(0));
            Assert.That(
                IndexOf(entry.SystemTypes, typeof(SInstantEffectCueRequestProjection), 0),
                Is.GreaterThanOrEqualTo(0));

            Assert.That(
                plan.TryFind(GASRuntimeQueryLayoutEntryId.ObservationReplayAndOutbox, out var observationEntry),
                Is.True);
            Assert.That(
                observationEntry.HasOptionalSlot(GASRuntimeLayoutComponentSlot.TypedSimulationFactBuffer),
                Is.True);
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

        [Test]
        public void ParallelFanInCommandMergeIsStableByTargetThenSequence()
        {
            var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
            var source = _em.CreateEntity();
            var targetA = _em.CreateEntity();
            var targetB = _em.CreateEntity();

            var plan = GASRuntimeFrameStreamOwnerPlanner.CreateCurrent();
            Assert.That(plan.TryFind(EGasRuntimeFrameStreamId.EffectCommand, out var commandStream), Is.True);
            Assert.That(commandStream.TargetCarrier, Is.EqualTo(EGasRuntimeFrameStreamCarrier.PerThreadNativeStream));
            Assert.That(commandStream.MergePolicy, Is.EqualTo(EGasRuntimeFrameStreamMergePolicy.StableSortByTargetThenSequence));
            Assert.That(commandStream.SortKey, Is.EqualTo(EGasRuntimeFrameStreamSortKey.TargetAscThenCommandSequence));

            var records = new[]
            {
                FanInRecord(1, 0, source, targetB, sequence: 10, contextId: 110),
                FanInRecord(1, 1, source, targetA, sequence: 5, contextId: 105),
                FanInRecord(0, 0, source, targetB, sequence: 1, contextId: 101),
                FanInRecord(0, 1, source, targetA, sequence: 20, contextId: 120),
                FanInRecord(0, 2, source, targetA, sequence: 5, contextId: 205),
            };

            var first = MergeAndCaptureCommandOrder(streamEntity, records);
            EffectCommandSpecStream.ClearFrameLocalData(_em, streamEntity, 2);
            var second = MergeAndCaptureCommandOrder(streamEntity, records);

            CollectionAssert.AreEqual(first, second);
            CollectionAssert.AreEqual(
                new[]
                {
                    Key(targetA, sequence: 5, contextId: 205),
                    Key(targetA, sequence: 5, contextId: 105),
                    Key(targetA, sequence: 20, contextId: 120),
                    Key(targetB, sequence: 1, contextId: 101),
                    Key(targetB, sequence: 10, contextId: 110),
                },
                first);

            var stream = _em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            Assert.That(stream.NextCommandSequence, Is.GreaterThan(20));
            Assert.That(stream.NextContextId, Is.GreaterThan(205));
        }

        private int CountEntitiesWith<T>()
            where T : unmanaged, IComponentData
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private string[] MergeAndCaptureCommandOrder(
            Entity streamEntity,
            IReadOnlyList<EffectCommandSpecStream.ParallelCommandFanInRecord> records)
        {
            var merged = EffectCommandSpecStream.MergeParallelCommandFanIn(
                _em,
                streamEntity,
                records,
                currentFrame: 77);
            Assert.That(merged, Is.EqualTo(records.Count));

            var commands = _em.GetBuffer<BEffectCommand>(streamEntity);
            Assert.That(commands.Length, Is.EqualTo(records.Count));

            var order = new string[commands.Length];
            for (var i = 0; i < commands.Length; i++)
            {
                Assert.That(commands[i].Frame, Is.EqualTo(77));
                Assert.That(commands[i].SetByCallerCount, Is.EqualTo(0));
                order[i] = Key(commands[i].TargetAsc, commands[i].Sequence, commands[i].ContextId);
            }

            return order;
        }

        private static EffectCommandSpecStream.ParallelCommandFanInRecord FanInRecord(
            int producerIndex,
            int localIndex,
            Entity source,
            Entity target,
            int sequence,
            int contextId)
        {
            return new EffectCommandSpecStream.ParallelCommandFanInRecord
            {
                ProducerIndex = producerIndex,
                LocalIndex = localIndex,
                Command = new BEffectCommand
                {
                    Kind = EEffectCommandKind.Instant,
                    Source = EEffectCommandSource.Ability,
                    SourceAsc = source,
                    TargetAsc = target,
                    GameplayEffectCode = 9100 + sequence,
                    Sequence = sequence,
                    ContextId = contextId,
                },
            };
        }

        private static string Key(Entity target, int sequence, int contextId)
        {
            return target.Index + ":" + target.Version + ":" + sequence + ":" + contextId;
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
        public void TypedFactEventBridgeProjectsAttributeFactToLegacyAttributeEventBusOnce()
        {
            const int effectCode = 991034;
            const int attrSetCode = 13;
            const int attributeCode = 23;

            var source = _em.CreateEntity();
            var sourceAbility = _em.CreateEntity();
            var sourceEffect = _em.CreateEntity();
            var target = CreateAscWithAttribute(attrSetCode, attributeCode, 90f);

            try
            {
                RegisterSimpleModifierEffect(
                    effectCode,
                    attrSetCode,
                    attributeCode,
                    EModifierOp.Add,
                    10f);

                var command = EffectCommandSpecStream.AppendCommand(_em, new BEffectCommand
                {
                    Kind = EEffectCommandKind.Instant,
                    Source = EEffectCommandSource.Ability,
                    SourceAsc = source,
                    TargetAsc = target,
                    SourceAbility = sourceAbility,
                    SourceEffect = sourceEffect,
                    GameplayEffectCode = effectCode,
                    Frame = 10,
                    ContextId = 321,
                });

                RunCommandGroup();

                var attributeEvents = _em.GetBuffer<BAttributeChangeEvent>(GASManager.EntityEventBus);
                Assert.That(attributeEvents.Length, Is.EqualTo(1));

                var evt = attributeEvents[0];
                Assert.That(evt.ASC, Is.EqualTo(target));
                Assert.That(evt.SourceAsc, Is.EqualTo(source));
                Assert.That(evt.SourceAbility, Is.EqualTo(sourceAbility));
                Assert.That(evt.GameplayEffect, Is.EqualTo(sourceEffect));
                Assert.That(evt.EventCode, Is.EqualTo(effectCode));
                Assert.That(evt.AttrSetCode, Is.EqualTo(attrSetCode));
                Assert.That(evt.AttributeCode, Is.EqualTo(attributeCode));
                Assert.That(evt.OldValue, Is.EqualTo(90f));
                Assert.That(evt.NewValue, Is.EqualTo(100f));
                Assert.That(evt.ContextId, Is.EqualTo(321));
                Assert.That(evt.IsBaseValue, Is.True);

                var facts = _em.GetBuffer<BTypedSimulationFact>(_streamEntity);
                var stream = _em.GetComponentData<CEffectCommandSpecStream>(_streamEntity);
                Assert.That(facts.Length, Is.EqualTo(1));
                Assert.That(stream.EventBridgeFactCursor, Is.EqualTo(facts.Length));
                Assert.That(facts[0].SourceCommandSequence, Is.EqualTo(command.Sequence));

                RunCommandGroup();

                Assert.That(
                    _em.GetBuffer<BAttributeChangeEvent>(GASManager.EntityEventBus).Length,
                    Is.EqualTo(0));
            }
            finally
            {
                DestroyIfExists(target);
                DestroyIfExists(sourceEffect);
                DestroyIfExists(sourceAbility);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void InstantCommandWithCueOnApplyProjectsLegacyCueRequestOnce()
        {
            const int effectCode = 991035;
            const int attrSetCode = 14;
            const int attributeCode = 24;
            const int cueCode = 3005;

            var source = _em.CreateEntity();
            var sourceAbility = _em.CreateEntity();
            var target = CreateAscWithAttribute(attrSetCode, attributeCode, 50f);

            try
            {
                RegisterSimpleModifierEffectWithCue(
                    effectCode,
                    attrSetCode,
                    attributeCode,
                    EModifierOp.Add,
                    7f,
                    cueCode);

                var command = EffectCommandSpecStream.AppendCommand(_em, new BEffectCommand
                {
                    Kind = EEffectCommandKind.Instant,
                    Source = EEffectCommandSource.Ability,
                    SourceAsc = source,
                    TargetAsc = target,
                    SourceAbility = sourceAbility,
                    GameplayEffectCode = effectCode,
                    Frame = 11,
                    ContextId = 654,
                });

                RunCommandGroup();

                var specs = _em.GetBuffer<BInstantEffectSpec>(_streamEntity);
                var facts = _em.GetBuffer<BTypedSimulationFact>(_streamEntity);
                var cueRequests = _em.GetBuffer<BCueRequest>(GASManager.EntityEventBus);
                var gameplayEvents = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
                var stream = _em.GetComponentData<CEffectCommandSpecStream>(_streamEntity);
                var attribute = _em.GetBuffer<BAttribute>(target)[0];
                var cueFactSequence = FindTypedCueFactSequence(facts, cueCode, 654);

                Assert.That(specs.Length, Is.EqualTo(1));
                Assert.That(specs[0].CueRequestOnApplyCode, Is.EqualTo(cueCode));
                Assert.That(attribute.BaseValue, Is.EqualTo(57f));
                Assert.That(cueFactSequence, Is.GreaterThan(0));
                Assert.That(ContainsTypedCueFact(facts, command.Sequence, specs[0].Sequence, cueCode, 654), Is.True);
                Assert.That(cueRequests.Length, Is.EqualTo(1));
                Assert.That(cueRequests[0].TargetAsc, Is.EqualTo(target));
                Assert.That(cueRequests[0].SourceAsc, Is.EqualTo(source));
                Assert.That(cueRequests[0].SourceAbility, Is.EqualTo(sourceAbility));
                Assert.That(cueRequests[0].GameplayEffect, Is.EqualTo(Entity.Null));
                Assert.That(cueRequests[0].SourceType, Is.EqualTo(CueSourceType.GameplayEffect));
                Assert.That(cueRequests[0].CueEntity, Is.EqualTo(Entity.Null));
                Assert.That(cueRequests[0].ContextId, Is.EqualTo(654));
                Assert.That(cueRequests[0].CueEvent, Is.EqualTo(EGameplayCueEvent.OnApply));
                Assert.That(cueRequests[0].ReasonCode, Is.EqualTo(cueCode));
                Assert.That(cueRequests[0].SourceFactSequence, Is.EqualTo(cueFactSequence));
                Assert.That(
                    ContainsGameplayEvent(
                        gameplayEvents,
                        EGameplayEventType.CueRequested,
                        (int)EGameplayCueEvent.OnApply,
                        cueCode,
                        654,
                        cueFactSequence),
                    Is.True);
                Assert.That(stream.CueProjectionSpecCursor, Is.EqualTo(specs.Length));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));
                Assert.That(CountRuntimeEffectSpecsByCode(effectCode), Is.EqualTo(0));

                RunCommandGroup();

                Assert.That(_em.GetBuffer<BCueRequest>(GASManager.EntityEventBus).Length, Is.EqualTo(0));
                Assert.That(_em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus).Length, Is.EqualTo(0));
            }
            finally
            {
                DestroyIfExists(target);
                DestroyIfExists(sourceAbility);
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

        [Test]
        public void CommandWriterReusesResolvedFrameAndFlushesStreamCounters()
        {
            const int effectCode = 991036;
            const int attrSetCode = 15;
            const int attributeCode = 25;
            const int magnitudeKey = 6002;

            var source = _em.CreateEntity();
            var targetA = CreateAscWithAttribute(attrSetCode, attributeCode, 10f);
            var targetB = CreateAscWithAttribute(attrSetCode, attributeCode, 20f);

            try
            {
                var writer = EffectCommandSpecStream.BeginCommandWriter(_em, _streamEntity, 42);
                var commandA = writer.AppendCommand(
                    new BEffectCommand
                    {
                        Kind = EEffectCommandKind.Instant,
                        Source = EEffectCommandSource.Ability,
                        SourceAsc = source,
                        TargetAsc = targetA,
                        GameplayEffectCode = effectCode,
                    },
                    new[]
                    {
                        new BSetByCallerValue
                        {
                            Key = magnitudeKey,
                            Value = 5f,
                        },
                    });
                var commandB = writer.AppendCommand(new BEffectCommand
                {
                    Kind = EEffectCommandKind.Instant,
                    Source = EEffectCommandSource.Ability,
                    SourceAsc = source,
                    TargetAsc = targetB,
                    GameplayEffectCode = effectCode,
                });
                writer.Flush();

                var stream = _em.GetComponentData<CEffectCommandSpecStream>(_streamEntity);
                var commands = _em.GetBuffer<BEffectCommand>(_streamEntity);
                var setByCallers = _em.GetBuffer<BEffectCommandSetByCallerValue>(_streamEntity);

                Assert.That(writer.IsCreated, Is.True);
                Assert.That(writer.StreamEntity, Is.EqualTo(_streamEntity));
                Assert.That(writer.CurrentFrame, Is.EqualTo(42));
                Assert.That(commands.Length, Is.EqualTo(2));
                Assert.That(commands[0].Sequence, Is.EqualTo(commandA.Sequence));
                Assert.That(commands[1].Sequence, Is.EqualTo(commandB.Sequence));
                Assert.That(commands[0].Frame, Is.EqualTo(42));
                Assert.That(commands[1].Frame, Is.EqualTo(42));
                Assert.That(commandB.Sequence, Is.EqualTo(commandA.Sequence + 1));
                Assert.That(commandB.ContextId, Is.EqualTo(commandA.ContextId + 1));
                Assert.That(commandA.SetByCallerStart, Is.EqualTo(0));
                Assert.That(commandA.SetByCallerCount, Is.EqualTo(1));
                Assert.That(commandB.SetByCallerStart, Is.EqualTo(1));
                Assert.That(commandB.SetByCallerCount, Is.EqualTo(0));
                Assert.That(setByCallers.Length, Is.EqualTo(1));
                Assert.That(setByCallers[0].CommandSequence, Is.EqualTo(commandA.Sequence));
                Assert.That(setByCallers[0].Key, Is.EqualTo(magnitudeKey));
                Assert.That(setByCallers[0].Value, Is.EqualTo(5f));
                Assert.That(stream.NextCommandSequence, Is.EqualTo(commandB.Sequence + 1));
                Assert.That(stream.NextContextId, Is.EqualTo(commandB.ContextId + 1));
            }
            finally
            {
                DestroyIfExists(targetB);
                DestroyIfExists(targetA);
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

        private static void RegisterSimpleModifierEffectWithCue(
            int effectCode,
            int attrSetCode,
            int attributeCode,
            EModifierOp operation,
            float magnitude,
            int cueCode)
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
                        new ConfGameplayEffectCueRequestOnApply
                        {
                            CueCode = cueCode,
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

        private static bool ContainsGameplayEvent(
            DynamicBuffer<BGameplayEvent> events,
            EGameplayEventType type,
            int eventCode,
            int reasonCode,
            int contextId,
            int sourceFactSequence = 0)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Type == type
                    && evt.EventCode == eventCode
                    && evt.ReasonCode == reasonCode
                    && evt.ContextId == contextId
                    && (sourceFactSequence <= 0 || evt.SourceFactSequence == sourceFactSequence))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsTypedCueFact(
            DynamicBuffer<BTypedSimulationFact> facts,
            int commandSequence,
            int specSequence,
            int cueCode,
            int contextId)
        {
            return FindTypedCueFactSequence(
                facts,
                cueCode,
                contextId,
                commandSequence,
                specSequence) > 0;
        }

        private static int FindTypedCueFactSequence(
            DynamicBuffer<BTypedSimulationFact> facts,
            int cueCode,
            int contextId,
            int commandSequence = 0,
            int specSequence = 0)
        {
            for (var i = 0; i < facts.Length; i++)
            {
                var fact = facts[i];
                if (fact.Domain == EGameplayFactDomain.Cue
                    && fact.EventType == EGameplayEventType.CueRequested
                    && fact.EventCode == (int)EGameplayCueEvent.OnApply
                    && fact.ReasonCode == cueCode
                    && fact.ContextId == contextId
                    && (commandSequence <= 0 || fact.SourceCommandSequence == commandSequence)
                    && (specSequence <= 0 || fact.SourceSpecSequence == specSequence))
                {
                    return fact.Sequence;
                }
            }

            return 0;
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
