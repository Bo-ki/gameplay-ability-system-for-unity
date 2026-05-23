using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Tests.Event
{
    public sealed class CommandEventBridgeContractTests
    {
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
            ClearTransientEventBuffers();
            ClearDebugReplayLog();
        }

        [TearDown]
        public void TearDown()
        {
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(_ => null);
            ClearTransientEventBuffers();
            ClearDebugReplayLog();
        }

        [Test]
        public void CommandGroupClearsPreviousTickEventsBeforeProcessingCurrentRequests()
        {
            const int effectCode = 98001;
            var source = _em.CreateEntity();
            var target = _em.CreateEntity();
            var request = Entity.Null;

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
                    ? new GameplayEffectConfig(Array.Empty<GameplayEffectComponentConfig>())
                    : null);

            try
            {
                var gameplayEvents = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
                gameplayEvents.Add(new BGameplayEvent
                {
                    Type = EGameplayEventType.GameplayEffectRemoved,
                    EventCode = -effectCode,
                });

                request = GameplayEffectRequestWriter.Create(
                    _em,
                    new CApplyGameplayEffectRequest
                    {
                        SourceAsc = source,
                        Instigator = source,
                        Causer = source,
                        GameplayEffectCode = effectCode,
                        Level = 1,
                    },
                    new CTargetDataHeader
                    {
                        SourceAsc = source,
                        Kind = ETargetDataKind.Entity,
                    });
                GameplayEffectRequestWriter.AddTarget(_em, request, target);

                UpdateCommandGroup();

                gameplayEvents = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
                Assert.That(ContainsEventCode(gameplayEvents, -effectCode), Is.False);
                Assert.That(ContainsEffectInstancedEvent(gameplayEvents, source, target, effectCode), Is.True);
            }
            finally
            {
                DestroyEffectsByCode(effectCode);
                DestroyIfExists(request);
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void EventBusClearKeepsSequenceAndClearsAllTransientStreams()
        {
            var eventBus = GASManager.EntityEventBus;
            var previousState = _em.GetComponentData<CGameplayEventBus>(eventBus);
            var asc = AbilitySystemFacade.Create().Entity;

            try
            {
                _em.SetComponentData(eventBus, new CGameplayEventBus
                {
                    NextSequence = 42,
                });

                _em.GetBuffer<BDamageEvent>(eventBus).Add(new BDamageEvent
                {
                    Amount = 10f,
                });
                _em.GetBuffer<BTagChangeEvent>(eventBus).Add(new BTagChangeEvent
                {
                    TagIndex = 3,
                    Added = true,
                });
                _em.GetBuffer<BGameplayEvent>(eventBus).Add(new BGameplayEvent
                {
                    Type = EGameplayEventType.GameplayEffectApplied,
                    EventCode = 98002,
                });
                _em.GetBuffer<BAttributeChangeEvent>(eventBus).Add(new BAttributeChangeEvent
                {
                    AttributeCode = 7,
                    OldValue = 1f,
                    NewValue = 2f,
                });
                _em.GetBuffer<BCueRequest>(eventBus).Add(new BCueRequest
                {
                    CueEvent = EGameplayCueEvent.Play,
                });
                EventBusHelper.AppendPresentationEvent(_em, eventBus, asc, new BPresentationEvent
                {
                    Kind = EPresentationEventKind.GameplayEvent,
                    EventCode = 98002,
                });

                UpdateCommandGroup();

                Assert.That(_em.GetBuffer<BDamageEvent>(eventBus).Length, Is.EqualTo(0));
                Assert.That(_em.GetBuffer<BTagChangeEvent>(eventBus).Length, Is.EqualTo(0));
                Assert.That(_em.GetBuffer<BGameplayEvent>(eventBus).Length, Is.EqualTo(0));
                Assert.That(_em.GetBuffer<BAttributeChangeEvent>(eventBus).Length, Is.EqualTo(0));
                Assert.That(_em.GetBuffer<BCueRequest>(eventBus).Length, Is.EqualTo(0));
                Assert.That(_em.GetBuffer<BPresentationEvent>(asc).Length, Is.EqualTo(0));
                Assert.That(_em.GetComponentData<CGameplayEventBus>(eventBus).NextSequence, Is.EqualTo(42));
            }
            finally
            {
                _em.SetComponentData(eventBus, previousState);
                DestroyIfExists(asc);
            }
        }

        [Test]
        public void GameplayEventBatchAppendsAfterStructuralChangeWithoutReusingInvalidatedBuffers()
        {
            var eventBus = GASManager.EntityEventBus;
            var previousState = _em.GetComponentData<CGameplayEventBus>(eventBus);
            var structuralEntity = Entity.Null;

            try
            {
                _em.SetComponentData(eventBus, new CGameplayEventBus
                {
                    NextSequence = 10,
                });

                using (EventBusHelper.BeginGameplayEventBatch(_em, eventBus))
                {
                    EventBusHelper.EnqueueGameplayEvent(_em, eventBus, new BGameplayEvent
                    {
                        Type = EGameplayEventType.GameplayEffectApplied,
                        EventCode = 98021,
                    });

                    structuralEntity = _em.CreateEntity();
                    _em.AddComponentData(structuralEntity, new CApplyGameplayEffectRequest
                    {
                        GameplayEffectCode = 98022,
                    });

                    EventBusHelper.EnqueueAttributeChangeEvent(_em, eventBus, new BAttributeChangeEvent
                    {
                        AttrSetCode = 1,
                        AttributeCode = 2,
                        OldValue = 3f,
                        NewValue = 4f,
                    });
                    EventBusHelper.EnqueueCueRequest(_em, eventBus, new BCueRequest
                    {
                        CueEvent = EGameplayCueEvent.OnApply,
                    });
                    EventBusHelper.EnqueueTagChangeEvent(_em, eventBus, new BTagChangeEvent
                    {
                        TagIndex = 5,
                        Added = true,
                    });
                    EventBusHelper.EnqueueDamageEvent(_em, eventBus, new BDamageEvent
                    {
                        Amount = 6f,
                    });
                    EventBusHelper.EnqueueGameplayEvent(_em, eventBus, new BGameplayEvent
                    {
                        Type = EGameplayEventType.GameplayEffectRemoved,
                        EventCode = 98023,
                    });
                }

                var gameplayEvents = _em.GetBuffer<BGameplayEvent>(eventBus);
                Assert.That(gameplayEvents.Length, Is.EqualTo(2));
                Assert.That(gameplayEvents[0].Sequence, Is.EqualTo(10));
                Assert.That(gameplayEvents[1].Sequence, Is.EqualTo(11));
                Assert.That(gameplayEvents[0].EventCode, Is.EqualTo(98021));
                Assert.That(gameplayEvents[1].EventCode, Is.EqualTo(98023));

                Assert.That(_em.GetComponentData<CGameplayEventBus>(eventBus).NextSequence, Is.EqualTo(12));
                Assert.That(_em.GetBuffer<BAttributeChangeEvent>(eventBus).Length, Is.EqualTo(1));
                Assert.That(_em.GetBuffer<BCueRequest>(eventBus).Length, Is.EqualTo(1));
                Assert.That(_em.GetBuffer<BTagChangeEvent>(eventBus).Length, Is.EqualTo(1));
                Assert.That(_em.GetBuffer<BDamageEvent>(eventBus).Length, Is.EqualTo(1));
            }
            finally
            {
                _em.SetComponentData(eventBus, previousState);
                DestroyIfExists(structuralEntity);
            }
        }

        [Test]
        public void GameplayFactClassifierSeparatesFailureDiagnosticRequestAndStateFacts()
        {
            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AbilityCommitFailed),
                EGameplayFactDomain.Ability,
                EGameplayFactCategory.Failure,
                EGameplayFactSeverity.Warning);
            Assert.That(
                GameplayFactClassifier.Classify(EGameplayEventType.AbilityCommitFailed).IsFailure,
                Is.True);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AbilityActivationBlockedByAbility),
                EGameplayFactDomain.Ability,
                EGameplayFactCategory.Failure,
                EGameplayFactSeverity.Warning);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AbilityEndRequested),
                EGameplayFactDomain.Ability,
                EGameplayFactCategory.Request,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AbilityCancelRequested),
                EGameplayFactDomain.Ability,
                EGameplayFactCategory.Request,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.ExecutionCalculationInputMissing),
                EGameplayFactDomain.ExecutionCalculation,
                EGameplayFactCategory.Diagnostic,
                EGameplayFactSeverity.Warning);
            Assert.That(
                GameplayFactClassifier.Classify(EGameplayEventType.ExecutionCalculationInputMissing).IsDiagnostic,
                Is.True);
            Assert.That(
                GameplayFactClassifier.Classify(EGameplayEventType.ExecutionCalculationInputMissing).IsFailure,
                Is.False);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.ExecutionCalculationOutputUpdated),
                EGameplayFactDomain.ExecutionCalculation,
                EGameplayFactCategory.StateChange,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.StackOverflowDenied),
                EGameplayFactDomain.GameplayEffect,
                EGameplayFactCategory.Failure,
                EGameplayFactSeverity.Warning);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.GameplayEffectApplicationRejected),
                EGameplayFactDomain.GameplayEffect,
                EGameplayFactCategory.Failure,
                EGameplayFactSeverity.Warning);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.GameplayEffectApplied),
                EGameplayFactDomain.GameplayEffect,
                EGameplayFactCategory.StateTransition,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessUnitDefeated),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.StateTransition,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessBattleResolved),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.StateTransition,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessPassiveTriggered),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.StateTransition,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessKillManaGranted),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.StateChange,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessReviveRequested),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.Request,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessReviveApplied),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.StateTransition,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessSynergyActivated),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.StateTransition,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessSynergyExpired),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.StateTransition,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessSynergyAllyBuffRequested),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.Request,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessSynergyEnemyDebuffRequested),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.Request,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessSynergyPeriodTicked),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.StateChange,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessPoisonPeriodDamageApplied),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.StateChange,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessExecuteTriggered),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.Request,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(EGameplayEventType.AutoChessExecuteDamageApplied),
                EGameplayFactDomain.RuntimeBoundary,
                EGameplayFactCategory.StateChange,
                EGameplayFactSeverity.Info);

            AssertClassification(
                GameplayFactClassifier.Classify(new BDebugReplayEvent
                {
                    Kind = EDebugReplayEventKind.GameplayEvent,
                    GameplayEventType = EGameplayEventType.AbilityCommitFailed,
                }),
                EGameplayFactDomain.Ability,
                EGameplayFactCategory.Failure,
                EGameplayFactSeverity.Warning);

            AssertClassification(
                GameplayFactClassifier.Classify(new BDebugReplayEvent
                {
                    Kind = EDebugReplayEventKind.TagChange,
                }),
                EGameplayFactDomain.Tag,
                EGameplayFactCategory.StateChange,
                EGameplayFactSeverity.Info);
        }

        [Test]
        public void StructuredLogViewProjectsReplayAndFactsWithoutCreatingSimulationInput()
        {
            var failure = GasStructuredLogView.FromReplay(new BDebugReplayEvent
            {
                LogIndex = 7,
                Frame = 11,
                Sequence = 12,
                Kind = EDebugReplayEventKind.GameplayEvent,
                GameplayEventType = EGameplayEventType.AbilityCommitFailed,
                EventCode = 1001,
                ReasonCode = (int)AbilityActivationResult.FailCost,
                Value = 3f,
            });

            Assert.That(failure.IsReplayBacked, Is.True);
            Assert.That(failure.IsFailure, Is.True);
            Assert.That(failure.Level, Is.EqualTo(EGasStructuredLogLevel.Warning));
            Assert.That(failure.Module, Is.EqualTo(EGasStructuredLogModule.Ability));
            Assert.That(failure.FactCategory, Is.EqualTo(EGameplayFactCategory.Failure));
            Assert.That(failure.EventCode, Is.EqualTo(1001));
            Assert.That(failure.ReasonCode, Is.EqualTo((int)AbilityActivationResult.FailCost));

            var diagnostic = GasStructuredLogView.FromReplay(new BDebugReplayEvent
            {
                LogIndex = 8,
                Kind = EDebugReplayEventKind.GameplayEvent,
                GameplayEventType = EGameplayEventType.ExecutionCalculationInputMissing,
                EventCode = 2002,
            });

            Assert.That(diagnostic.IsDiagnostic, Is.True);
            Assert.That(diagnostic.Level, Is.EqualTo(EGasStructuredLogLevel.Warning));
            Assert.That(diagnostic.Module, Is.EqualTo(EGasStructuredLogModule.ExecutionCalculation));

            var request = GasStructuredLogView.FromGameplayEvent(new BGameplayEvent
            {
                Type = EGameplayEventType.AbilityEndRequested,
                Frame = 13,
                Sequence = 14,
                EventCode = 3003,
            });

            Assert.That(request.IsReplayBacked, Is.False);
            Assert.That(request.LogIndex, Is.EqualTo(GasStructuredLogView.NoReplayLogIndex));
            Assert.That(request.Level, Is.EqualTo(EGasStructuredLogLevel.Info));
            Assert.That(request.Module, Is.EqualTo(EGasStructuredLogModule.Ability));
            Assert.That(request.FactCategory, Is.EqualTo(EGameplayFactCategory.Request));
            Assert.That(request.Frame, Is.EqualTo(13));
            Assert.That(request.Sequence, Is.EqualTo(14));

            var cueRequest = GasStructuredLogView.FromCueRequest(new BCueRequest
            {
                CueEvent = EGameplayCueEvent.OnApply,
                ContextId = 77,
            });

            Assert.That(cueRequest.Module, Is.EqualTo(EGasStructuredLogModule.GameplayCue));
            Assert.That(cueRequest.FactCategory, Is.EqualTo(EGameplayFactCategory.Request));
            Assert.That(cueRequest.ContextId, Is.EqualTo(77));
            Assert.That(cueRequest.EventCode, Is.EqualTo((int)EGameplayCueEvent.OnApply));

            var tagChange = GasStructuredLogView.FromTagChange(new BTagChangeEvent
            {
                TagIndex = 44,
                Added = true,
            });

            Assert.That(tagChange.Module, Is.EqualTo(EGasStructuredLogModule.GameplayTag));
            Assert.That(tagChange.FactCategory, Is.EqualTo(EGameplayFactCategory.StateChange));
            Assert.That(tagChange.TagIndex, Is.EqualTo(44));
            Assert.That(tagChange.Flag, Is.EqualTo(1));

            Assert.That(
                typeof(IComponentData).IsAssignableFrom(typeof(GasStructuredLogEntry)),
                Is.False);
            Assert.That(
                typeof(IBufferElementData).IsAssignableFrom(typeof(GasStructuredLogEntry)),
                Is.False);
        }

        [Test]
        public void AbilityLifecycleRequestComponentsAndFactsAreWrittenOnlyByRuntimeActions()
        {
            var projectRoot = FindProjectRoot();
            var runtimeRoot = Path.Combine(projectRoot, "Assets", "GAS", "Runtime");
            var allowedWriter = Path.GetFullPath(Path.Combine(
                runtimeRoot,
                "Ability",
                "AbilityRuntimeActions.cs"));
            var allowedFactContract = Path.GetFullPath(Path.Combine(
                runtimeRoot,
                "Event",
                "CGameplayEventBus.cs"));
            var offenders = new List<string>();

            foreach (var file in Directory.EnumerateFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                var fullPath = Path.GetFullPath(file);
                if (PathsEqual(fullPath, allowedWriter))
                    continue;

                var text = File.ReadAllText(fullPath);
                var hasComponentWrite = ContainsLifecycleRequestComponentWrite(text);
                var hasFactWrite = !PathsEqual(fullPath, allowedFactContract)
                    && ContainsLifecycleRequestFactWrite(text);
                if (hasComponentWrite || hasFactWrite)
                    offenders.Add(MakeRelativePath(projectRoot, fullPath));
            }

            Assert.That(
                offenders,
                Is.Empty,
                "Lifecycle request producers must use AbilityRuntimeActions.RequestAbilityEnd/Cancel for request components and request facts.");
        }

        [Test]
        public void CueGroupProjectsSimulationFactsToPerAscPresentationOutbox()
        {
            var eventBus = GASManager.EntityEventBus;
            var source = AbilitySystemFacade.Create().Entity;
            var target = AbilitySystemFacade.Create().Entity;
            var ability = _em.CreateEntity();
            var effect = _em.CreateEntity();
            var cue = _em.CreateEntity();

            try
            {
                _em.GetBuffer<BGameplayEvent>(eventBus).Add(new BGameplayEvent
                {
                    Frame = 211,
                    Sequence = 7,
                    Type = EGameplayEventType.GameplayEffectApplied,
                    SourceAsc = source,
                    TargetAsc = target,
                    SourceAbility = ability,
                    GameplayEffect = effect,
                    ContextId = 99,
                    EventCode = 98003,
                    Value = 12f,
                });
                _em.GetBuffer<BAttributeChangeEvent>(eventBus).Add(new BAttributeChangeEvent
                {
                    ASC = target,
                    SourceAsc = source,
                    SourceAbility = ability,
                    GameplayEffect = effect,
                    ContextId = 100,
                    AttrSetCode = 2,
                    AttributeCode = 3,
                    OldValue = 4f,
                    NewValue = 5f,
                    IsBaseValue = false,
                });
                _em.GetBuffer<BCueRequest>(eventBus).Add(new BCueRequest
                {
                    TargetAsc = target,
                    SourceAsc = source,
                    SourceAbility = ability,
                    GameplayEffect = effect,
                    CueEntity = cue,
                    ContextId = 101,
                    CueEvent = EGameplayCueEvent.OnApply,
                });

                UpdateCueGroup();

                var targetOutbox = _em.GetBuffer<BPresentationEvent>(target);
                Assert.That(ContainsPresentationGameplayEvent(targetOutbox, 98003, 7, effect), Is.True);
                Assert.That(ContainsPresentationAttributeEvent(targetOutbox, 2, 3, 4f, 5f), Is.True);
                Assert.That(ContainsPresentationCueEvent(targetOutbox, EGameplayCueEvent.OnApply, cue), Is.True);

                var sourceOutbox = _em.GetBuffer<BPresentationEvent>(source);
                Assert.That(ContainsPresentationGameplayEvent(sourceOutbox, 98003, 7, effect), Is.True);
                Assert.That(ContainsPresentationCueEvent(sourceOutbox, EGameplayCueEvent.OnApply, cue), Is.True);

                Assert.That(_em.GetBuffer<BGameplayEvent>(eventBus).Length, Is.EqualTo(1));
                Assert.That(_em.GetBuffer<BAttributeChangeEvent>(eventBus).Length, Is.EqualTo(1));
                Assert.That(_em.GetBuffer<BCueRequest>(eventBus).Length, Is.EqualTo(1));
            }
            finally
            {
                DestroyIfExists(cue);
                DestroyIfExists(effect);
                DestroyIfExists(ability);
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void PresentationOutboxDoesNotDuplicateSameTickFactsWhenCueGroupUpdatesAgain()
        {
            var eventBus = GASManager.EntityEventBus;
            var source = AbilitySystemFacade.Create().Entity;
            var target = AbilitySystemFacade.Create().Entity;
            var ability = _em.CreateEntity();
            var effect = _em.CreateEntity();
            var cue = _em.CreateEntity();

            try
            {
                EnqueueAllFactKinds(eventBus, source, target, ability, effect, cue);

                UpdateCueGroup();
                var targetOutbox = _em.GetBuffer<BPresentationEvent>(target);
                Assert.That(targetOutbox.Length, Is.EqualTo(5));

                UpdateCueGroup();
                targetOutbox = _em.GetBuffer<BPresentationEvent>(target);
                Assert.That(targetOutbox.Length, Is.EqualTo(5));

                _em.GetBuffer<BGameplayEvent>(eventBus).Add(new BGameplayEvent
                {
                    Frame = 221,
                    Sequence = 12,
                    Type = EGameplayEventType.GameplayEffectRemoved,
                    TargetAsc = target,
                    EventCode = 98007,
                });

                UpdateCueGroup();
                targetOutbox = _em.GetBuffer<BPresentationEvent>(target);
                Assert.That(targetOutbox.Length, Is.EqualTo(6));
                Assert.That(CountPresentationEventsByCode(targetOutbox, 98004), Is.EqualTo(1));
                Assert.That(CountPresentationEventsByCode(targetOutbox, 98007), Is.EqualTo(1));
            }
            finally
            {
                DestroyIfExists(cue);
                DestroyIfExists(effect);
                DestroyIfExists(ability);
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void CueGroupAppendsSimulationFactsToPersistentDebugReplaySink()
        {
            var eventBus = GASManager.EntityEventBus;
            var logSink = GASManager.EntityEventLogSink;
            var source = AbilitySystemFacade.Create().Entity;
            var target = AbilitySystemFacade.Create().Entity;
            var ability = _em.CreateEntity();
            var effect = _em.CreateEntity();
            var cue = _em.CreateEntity();

            try
            {
                EnqueueAllFactKinds(eventBus, source, target, ability, effect, cue);

                UpdateCueGroup();

                var log = _em.GetBuffer<BDebugReplayEvent>(logSink);
                Assert.That(log.Length, Is.EqualTo(5));
                Assert.That(ContainsDebugReplayGameplayEvent(log, 98004, 11, effect), Is.True);
                Assert.That(ContainsDebugReplayAttributeEvent(log, 4, 5, 6f, 7f), Is.True);
                Assert.That(ContainsDebugReplayCueEvent(log, EGameplayCueEvent.OnApply, cue), Is.True);
                Assert.That(ContainsDebugReplayTagEvent(log, 33, added: true), Is.True);
                Assert.That(ContainsDebugReplayDamageEvent(log, source, target, 25f), Is.True);

                Assert.That(_em.GetBuffer<BGameplayEvent>(eventBus).Length, Is.EqualTo(1));
                Assert.That(_em.GetBuffer<BAttributeChangeEvent>(eventBus).Length, Is.EqualTo(1));
                Assert.That(_em.GetBuffer<BCueRequest>(eventBus).Length, Is.EqualTo(1));

                UpdateCommandGroup();

                Assert.That(_em.GetBuffer<BGameplayEvent>(eventBus).Length, Is.EqualTo(0));
                Assert.That(_em.GetBuffer<BDebugReplayEvent>(logSink).Length, Is.EqualTo(5));
            }
            finally
            {
                DestroyIfExists(cue);
                DestroyIfExists(effect);
                DestroyIfExists(ability);
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void DebugReplaySinkDoesNotDuplicateSameTickFactsWhenCueGroupUpdatesAgain()
        {
            var eventBus = GASManager.EntityEventBus;
            var logSink = GASManager.EntityEventLogSink;
            var target = AbilitySystemFacade.Create().Entity;

            try
            {
                _em.GetBuffer<BGameplayEvent>(eventBus).Add(new BGameplayEvent
                {
                    Frame = 301,
                    Sequence = 13,
                    Type = EGameplayEventType.GameplayEffectApplied,
                    TargetAsc = target,
                    EventCode = 98005,
                });

                UpdateCueGroup();
                UpdateCueGroup();

                var log = _em.GetBuffer<BDebugReplayEvent>(logSink);
                Assert.That(CountDebugReplayEventsByCode(log, 98005), Is.EqualTo(1));

                _em.GetBuffer<BGameplayEvent>(eventBus).Add(new BGameplayEvent
                {
                    Frame = 301,
                    Sequence = 14,
                    Type = EGameplayEventType.GameplayEffectRemoved,
                    TargetAsc = target,
                    EventCode = 98006,
                });

                UpdateCueGroup();

                log = _em.GetBuffer<BDebugReplayEvent>(logSink);
                Assert.That(CountDebugReplayEventsByCode(log, 98005), Is.EqualTo(1));
                Assert.That(CountDebugReplayEventsByCode(log, 98006), Is.EqualTo(1));
            }
            finally
            {
                DestroyIfExists(target);
            }
        }

        [Test]
        public void DebugReplayRetentionKeepsLatestEventsWithoutRenumberingLogIndex()
        {
            var eventBus = GASManager.EntityEventBus;
            var logSink = GASManager.EntityEventLogSink;
            var target = AbilitySystemFacade.Create().Entity;

            try
            {
                var sinkState = _em.GetComponentData<CGameplayEventLogSink>(logSink);
                sinkState.MaxRetainedEvents = 3;
                _em.SetComponentData(logSink, sinkState);

                var gameplayEvents = _em.GetBuffer<BGameplayEvent>(eventBus);
                for (var i = 0; i < 5; i++)
                {
                    gameplayEvents.Add(new BGameplayEvent
                    {
                        Frame = 401,
                        Sequence = i + 1,
                        Type = EGameplayEventType.GameplayEffectApplied,
                        TargetAsc = target,
                        EventCode = 99000 + i,
                    });
                }

                UpdateCueGroup();

                var log = _em.GetBuffer<BDebugReplayEvent>(logSink);
                Assert.That(log.Length, Is.EqualTo(3));
                Assert.That(log[0].LogIndex, Is.EqualTo(2));
                Assert.That(log[0].EventCode, Is.EqualTo(99002));
                Assert.That(log[2].LogIndex, Is.EqualTo(4));
                Assert.That(log[2].EventCode, Is.EqualTo(99004));

                sinkState = _em.GetComponentData<CGameplayEventLogSink>(logSink);
                Assert.That(sinkState.NextLogIndex, Is.EqualTo(5));
                Assert.That(sinkState.FirstRetainedLogIndex, Is.EqualTo(2));
                Assert.That(sinkState.DroppedEventCount, Is.EqualTo(2));

                var stats = GasReplaySinkPolicy.GetStats(sinkState, log);
                Assert.That(stats.RetainedEventCount, Is.EqualTo(3));
                Assert.That(stats.FirstRetainedLogIndex, Is.EqualTo(2));
                Assert.That(stats.LastRetainedLogIndex, Is.EqualTo(4));
                Assert.That(stats.HasDroppedEvents, Is.True);
                Assert.That(
                    GasReplaySinkPolicy.IsCursorExpired(new GasReplayCursor(0), stats),
                    Is.True);
                Assert.That(
                    GasReplaySinkPolicy.IsCursorExpired(new GasReplayCursor(2), stats),
                    Is.False);
                Assert.That(
                    GasReplaySinkPolicy.IsCursorAtEnd(new GasReplayCursor(5), stats),
                    Is.True);
            }
            finally
            {
                DestroyIfExists(target);
            }
        }

        [Test]
        public void DebugReplayFilterIsReadSideOnlyAndMatchesReplayFields()
        {
            var source = _em.CreateEntity();
            var target = _em.CreateEntity();

            try
            {
                var evt = new BDebugReplayEvent
                {
                    LogIndex = 11,
                    Frame = 22,
                    Kind = EDebugReplayEventKind.GameplayEvent,
                    GameplayEventType = EGameplayEventType.AbilityCommitFailed,
                    SourceAsc = source,
                    TargetAsc = target,
                    ContextId = 33,
                    EventCode = 44,
                };

                var filter = new GasReplayEventFilter
                {
                    HasMinLogIndex = true,
                    MinLogIndex = 10,
                    HasMaxLogIndex = true,
                    MaxLogIndex = 12,
                    HasMinFrame = true,
                    MinFrame = 20,
                    HasMaxFrame = true,
                    MaxFrame = 30,
                    HasKind = true,
                    Kind = EDebugReplayEventKind.GameplayEvent,
                    HasGameplayEventType = true,
                    GameplayEventType = EGameplayEventType.AbilityCommitFailed,
                    HasSourceAsc = true,
                    SourceAsc = source,
                    HasTargetAsc = true,
                    TargetAsc = target,
                    HasContextId = true,
                    ContextId = 33,
                    HasEventCode = true,
                    EventCode = 44,
                };

                Assert.That(filter.Matches(evt), Is.True);

                filter.EventCode = 45;
                Assert.That(filter.Matches(evt), Is.False);

                filter.EventCode = 44;
                filter.Kind = EDebugReplayEventKind.TagChange;
                Assert.That(filter.Matches(evt), Is.False);

                Assert.That(GasReplayEventFilter.All.Matches(evt), Is.True);
            }
            finally
            {
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void StructuredLogExporterFormatsFiltersAndWritesRetainedReplaySnapshot()
        {
            var eventBus = GASManager.EntityEventBus;
            var logSink = GASManager.EntityEventLogSink;
            var source = AbilitySystemFacade.Create().Entity;
            var target = AbilitySystemFacade.Create().Entity;
            var exportPath = Path.Combine(
                Path.GetTempPath(),
                "exgas-structured-log-" + Guid.NewGuid().ToString("N") + ".log");

            try
            {
                var sinkState = _em.GetComponentData<CGameplayEventLogSink>(logSink);
                sinkState.MaxRetainedEvents = 3;
                _em.SetComponentData(logSink, sinkState);

                var gameplayEvents = _em.GetBuffer<BGameplayEvent>(eventBus);
                gameplayEvents.Add(new BGameplayEvent
                {
                    Frame = 501,
                    Sequence = 1,
                    Type = EGameplayEventType.GameplayEffectApplied,
                    SourceAsc = source,
                    TargetAsc = target,
                    ContextId = 99100,
                    EventCode = 99100,
                });
                gameplayEvents.Add(new BGameplayEvent
                {
                    Frame = 501,
                    Sequence = 2,
                    Type = EGameplayEventType.AbilityCommitFailed,
                    SourceAsc = source,
                    TargetAsc = target,
                    ContextId = 99101,
                    EventCode = 99101,
                    ReasonCode = (int)AbilityActivationResult.FailCost,
                    Value = 12.5f,
                });
                gameplayEvents.Add(new BGameplayEvent
                {
                    Frame = 502,
                    Sequence = 3,
                    Type = EGameplayEventType.ExecutionCalculationInputMissing,
                    SourceAsc = source,
                    TargetAsc = target,
                    ContextId = 99102,
                    EventCode = 99102,
                });
                gameplayEvents.Add(new BGameplayEvent
                {
                    Frame = 503,
                    Sequence = 4,
                    Type = EGameplayEventType.GameplayEffectRemoved,
                    SourceAsc = source,
                    TargetAsc = target,
                    ContextId = 99103,
                    EventCode = 99103,
                });

                UpdateCueGroup();

                var log = _em.GetBuffer<BDebugReplayEvent>(logSink);
                sinkState = _em.GetComponentData<CGameplayEventLogSink>(logSink);
                var snapshot = GasStructuredLogExporter.CreateSnapshot(
                    log,
                    sinkState,
                    new GasReplayCursor(0),
                    new GasReplayEventFilter
                    {
                        HasMinLogIndex = true,
                        MinLogIndex = 1,
                    },
                    new GasStructuredLogFilter
                    {
                        HasMinLevel = true,
                        MinLevel = EGasStructuredLogLevel.Warning,
                        HasModule = true,
                        Module = EGasStructuredLogModule.Ability,
                    });

                Assert.That(snapshot.CursorExpired, Is.True);
                Assert.That(snapshot.ReplayStats.FirstRetainedLogIndex, Is.EqualTo(1));
                Assert.That(snapshot.ReplayStats.DroppedEventCount, Is.EqualTo(1));
                Assert.That(snapshot.EntryCount, Is.EqualTo(1));
                Assert.That(snapshot.Entries[0].LogIndex, Is.EqualTo(1));
                Assert.That(snapshot.Entries[0].Level, Is.EqualTo(EGasStructuredLogLevel.Warning));
                Assert.That(snapshot.Entries[0].Module, Is.EqualTo(EGasStructuredLogModule.Ability));
                Assert.That(snapshot.Entries[0].GameplayEventType, Is.EqualTo(EGameplayEventType.AbilityCommitFailed));

                var assertionText = GasStructuredLogExporter.ExportToText(
                    snapshot,
                    GasStructuredLogFormatOptions.AssertionText);
                Assert.That(assertionText, Does.Contain("stats|entries=1"));
                Assert.That(assertionText, Does.Contain("|cursorExpired=True"));
                Assert.That(assertionText, Does.Contain("|level=Warning|module=Ability"));
                Assert.That(assertionText, Does.Contain("|type=AbilityCommitFailed"));
                Assert.That(assertionText, Does.Contain("|event=99101"));
                Assert.That(assertionText, Does.Contain("|value=12.5"));

                var humanText = GasStructuredLogExporter.ExportToText(
                    snapshot,
                    GasStructuredLogFormatOptions.HumanReadable);
                Assert.That(humanText, Does.Contain("Warning Ability Failure GameplayEvent/AbilityCommitFailed"));
                Assert.That(humanText, Does.Contain("event=99101"));

                var emptyText = GasStructuredLogExporter.ExportToText(
                    default(GasStructuredLogExportSnapshot),
                    GasStructuredLogFormatOptions.AssertionText);
                Assert.That(emptyText, Does.Contain("stats|entries=0"));

                var result = GasStructuredLogExporter.WriteTextFile(
                    exportPath,
                    snapshot,
                    GasStructuredLogFormatOptions.AssertionText);
                Assert.That(result.EntryCount, Is.EqualTo(1));
                Assert.That(result.ByteCount, Is.GreaterThan(0));
                Assert.That(File.Exists(exportPath), Is.True);
                Assert.That(File.ReadAllText(exportPath), Does.Contain("|type=AbilityCommitFailed"));

                Assert.That(
                    typeof(IComponentData).IsAssignableFrom(typeof(GasStructuredLogExportSnapshot)),
                    Is.False);
                Assert.That(
                    typeof(IBufferElementData).IsAssignableFrom(typeof(GasStructuredLogExportSnapshot)),
                    Is.False);
            }
            finally
            {
                if (File.Exists(exportPath))
                    File.Delete(exportPath);

                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        private static void UpdateCommandGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>().Update();
        }

        private static void UpdateCueGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCueGroup>().Update();
        }

        private void ClearTransientEventBuffers()
        {
            if (!GASManager.IsInitialized
                || !_em.Exists(GASManager.EntityEventBus))
            {
                return;
            }

            _em.GetBuffer<BDamageEvent>(GASManager.EntityEventBus).Clear();
            _em.GetBuffer<BTagChangeEvent>(GASManager.EntityEventBus).Clear();
            _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus).Clear();
            _em.GetBuffer<BAttributeChangeEvent>(GASManager.EntityEventBus).Clear();
            _em.GetBuffer<BCueRequest>(GASManager.EntityEventBus).Clear();

            if (_em.HasComponent<CPresentationOutboxProjectionState>(GASManager.EntityEventBus))
                _em.SetComponentData(GASManager.EntityEventBus, new CPresentationOutboxProjectionState());

            using var query = _em.CreateEntityQuery(ComponentType.ReadWrite<BPresentationEvent>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (_em.Exists(entities[i]) && _em.HasBuffer<BPresentationEvent>(entities[i]))
                    _em.GetBuffer<BPresentationEvent>(entities[i]).Clear();
            }
        }

        private void ClearDebugReplayLog()
        {
            if (!GASManager.IsInitialized
                || !_em.Exists(GASManager.EntityEventLogSink))
            {
                return;
            }

            if (_em.HasBuffer<BDebugReplayEvent>(GASManager.EntityEventLogSink))
                _em.GetBuffer<BDebugReplayEvent>(GASManager.EntityEventLogSink).Clear();

            if (_em.HasComponent<CGameplayEventLogSink>(GASManager.EntityEventLogSink))
                _em.SetComponentData(GASManager.EntityEventLogSink, new CGameplayEventLogSink());
        }

        private void EnqueueAllFactKinds(
            Entity eventBus,
            Entity source,
            Entity target,
            Entity ability,
            Entity effect,
            Entity cue)
        {
            _em.GetBuffer<BGameplayEvent>(eventBus).Add(new BGameplayEvent
            {
                Frame = 221,
                Sequence = 11,
                Type = EGameplayEventType.GameplayEffectApplied,
                SourceAsc = source,
                TargetAsc = target,
                SourceAbility = ability,
                GameplayEffect = effect,
                ContextId = 201,
                EventCode = 98004,
                Value = 13f,
            });
            _em.GetBuffer<BAttributeChangeEvent>(eventBus).Add(new BAttributeChangeEvent
            {
                ASC = target,
                SourceAsc = source,
                SourceAbility = ability,
                GameplayEffect = effect,
                ContextId = 202,
                AttrSetCode = 4,
                AttributeCode = 5,
                OldValue = 6f,
                NewValue = 7f,
            });
            _em.GetBuffer<BCueRequest>(eventBus).Add(new BCueRequest
            {
                TargetAsc = target,
                SourceAsc = source,
                SourceAbility = ability,
                GameplayEffect = effect,
                CueEntity = cue,
                ContextId = 203,
                CueEvent = EGameplayCueEvent.OnApply,
            });
            _em.GetBuffer<BTagChangeEvent>(eventBus).Add(new BTagChangeEvent
            {
                ASC = target,
                TagIndex = 33,
                Added = true,
            });
            _em.GetBuffer<BDamageEvent>(eventBus).Add(new BDamageEvent
            {
                Source = source,
                Target = target,
                Amount = 25f,
            });
        }

        private void DestroyEffectsByCode(int effectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CEffectSpecData>());
            using var effects = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (!_em.Exists(effect))
                    continue;

                var spec = _em.GetComponentData<CEffectSpecData>(effect);
                if (spec.GameplayEffectCode == effectCode)
                    _em.DestroyEntity(effect);
            }
        }

        private void DestroyIfExists(Entity entity)
        {
            if (entity != Entity.Null && _em.Exists(entity))
                _em.DestroyEntity(entity);
        }

        private static bool ContainsEventCode(DynamicBuffer<BGameplayEvent> events, int eventCode)
        {
            for (var i = 0; i < events.Length; i++)
            {
                if (events[i].EventCode == eventCode)
                    return true;
            }

            return false;
        }

        private static bool ContainsEffectInstancedEvent(
            DynamicBuffer<BGameplayEvent> events,
            Entity source,
            Entity target,
            int effectCode)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Type == EGameplayEventType.GameplayEffectInstanced
                    && evt.SourceAsc == source
                    && evt.TargetAsc == target
                    && evt.EventCode == effectCode
                    && evt.GameplayEffect != Entity.Null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPresentationGameplayEvent(
            DynamicBuffer<BPresentationEvent> events,
            int eventCode,
            int sequence,
            Entity effect)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind == EPresentationEventKind.GameplayEvent
                    && evt.GameplayEventType == EGameplayEventType.GameplayEffectApplied
                    && evt.EventCode == eventCode
                    && evt.Sequence == sequence
                    && evt.GameplayEffect == effect)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPresentationAttributeEvent(
            DynamicBuffer<BPresentationEvent> events,
            int attrSetCode,
            int attributeCode,
            float oldValue,
            float newValue)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind == EPresentationEventKind.AttributeChange
                    && evt.AttrSetCode == attrSetCode
                    && evt.AttributeCode == attributeCode
                    && evt.OldValue == oldValue
                    && evt.NewValue == newValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPresentationCueEvent(
            DynamicBuffer<BPresentationEvent> events,
            EGameplayCueEvent cueEvent,
            Entity cueEntity)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind == EPresentationEventKind.CueRequest
                    && evt.CueEvent == cueEvent
                    && evt.CueEntity == cueEntity)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertClassification(
            GameplayFactClassification classification,
            EGameplayFactDomain domain,
            EGameplayFactCategory category,
            EGameplayFactSeverity severity)
        {
            Assert.That(classification.Domain, Is.EqualTo(domain));
            Assert.That(classification.Category, Is.EqualTo(category));
            Assert.That(classification.Severity, Is.EqualTo(severity));
        }

        private static string FindProjectRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                if (IsProjectRoot(directory.FullName))
                    return directory.FullName;

                directory = directory.Parent;
            }

            directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (IsProjectRoot(directory.FullName))
                    return directory.FullName;

                directory = directory.Parent;
            }

            Assert.Fail("Could not locate project root for architecture contract scan.");
            return string.Empty;
        }

        private static bool IsProjectRoot(string path)
        {
            return Directory.Exists(Path.Combine(path, "Assets", "GAS", "Runtime"))
                && File.Exists(Path.Combine(path, "com.exhard.exgas.runtime.csproj"));
        }

        private static bool ContainsLifecycleRequestComponentWrite(string text)
        {
            return text.Contains("new CAbilityInTryEnd", StringComparison.Ordinal)
                || text.Contains("new CAbilityInTryCancel", StringComparison.Ordinal)
                || text.Contains("AddComponent<CAbilityInTryEnd", StringComparison.Ordinal)
                || text.Contains("AddComponent<CAbilityInTryCancel", StringComparison.Ordinal)
                || text.Contains("AddComponentData<CAbilityInTryEnd", StringComparison.Ordinal)
                || text.Contains("AddComponentData<CAbilityInTryCancel", StringComparison.Ordinal)
                || text.Contains("SetComponentData<CAbilityInTryEnd", StringComparison.Ordinal)
                || text.Contains("SetComponentData<CAbilityInTryCancel", StringComparison.Ordinal);
        }

        private static bool ContainsLifecycleRequestFactWrite(string text)
        {
            return text.Contains("AbilityEndRequested", StringComparison.Ordinal)
                || text.Contains("AbilityCancelRequested", StringComparison.Ordinal);
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string MakeRelativePath(string root, string path)
        {
            var rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            var pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static int CountPresentationEventsByCode(
            DynamicBuffer<BPresentationEvent> events,
            int eventCode)
        {
            var count = 0;
            for (var i = 0; i < events.Length; i++)
            {
                if (events[i].Kind == EPresentationEventKind.GameplayEvent
                    && events[i].EventCode == eventCode)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ContainsDebugReplayGameplayEvent(
            DynamicBuffer<BDebugReplayEvent> events,
            int eventCode,
            int sequence,
            Entity effect)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind == EDebugReplayEventKind.GameplayEvent
                    && evt.GameplayEventType == EGameplayEventType.GameplayEffectApplied
                    && evt.EventCode == eventCode
                    && evt.Sequence == sequence
                    && evt.GameplayEffect == effect)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDebugReplayAttributeEvent(
            DynamicBuffer<BDebugReplayEvent> events,
            int attrSetCode,
            int attributeCode,
            float oldValue,
            float newValue)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind == EDebugReplayEventKind.AttributeChange
                    && evt.AttrSetCode == attrSetCode
                    && evt.AttributeCode == attributeCode
                    && evt.OldValue == oldValue
                    && evt.NewValue == newValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDebugReplayCueEvent(
            DynamicBuffer<BDebugReplayEvent> events,
            EGameplayCueEvent cueEvent,
            Entity cueEntity)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind == EDebugReplayEventKind.CueRequest
                    && evt.CueEvent == cueEvent
                    && evt.CueEntity == cueEntity)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDebugReplayTagEvent(
            DynamicBuffer<BDebugReplayEvent> events,
            int tagIndex,
            bool added)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind == EDebugReplayEventKind.TagChange
                    && evt.TagIndex == tagIndex
                    && evt.Flag == (added ? (byte)1 : (byte)0))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDebugReplayDamageEvent(
            DynamicBuffer<BDebugReplayEvent> events,
            Entity source,
            Entity target,
            float amount)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind == EDebugReplayEventKind.Damage
                    && evt.SourceAsc == source
                    && evt.TargetAsc == target
                    && evt.DamageAmount == amount)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountDebugReplayEventsByCode(
            DynamicBuffer<BDebugReplayEvent> events,
            int eventCode)
        {
            var count = 0;
            for (var i = 0; i < events.Length; i++)
            {
                if (events[i].Kind == EDebugReplayEventKind.GameplayEvent
                    && events[i].EventCode == eventCode)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
