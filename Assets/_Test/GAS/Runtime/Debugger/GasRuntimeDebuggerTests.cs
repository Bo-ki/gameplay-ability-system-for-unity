using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Tests.Debugger
{
    public sealed class GasRuntimeDebuggerTests
    {
        private World _world;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _world = new World("GasRuntimeDebuggerTests");
            _em = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void CollectAndRecordRuntimeCoreCountersSamplesRuntimeStreamsAndCursorLag()
        {
            var debugger = GasRuntimeDebugger.CreateSingleton(_em);
            var eventBus = CreateEventBus();
            var replaySink = CreateReplaySink();

            var timer = _em.CreateEntity();
            _em.AddComponentData(timer, new GlobalTimer { Frame = 7 });

            CreateApplyRequest();
            CreateAbilityCommandRequest();
            CreateActiveEffect();
            CreateEffectDestroyMarker();
            CreateAscDestroyRequest();
            CreatePresentationOutbox(eventBus);
            CreateActiveEffectStoreOwner();
            CreateActiveEffectGlobalIndexOwner();

            using var queries = GasRuntimeCoreCounterQueries.CreateOwned(_em);
            GasRuntimeDebugger.CollectAndRecordRuntimeCoreCounters(
                _em,
                debugger,
                7,
                eventBus,
                replaySink,
                queries);

            var snapshot = GasRuntimeDebugger.CreateSnapshot(_em, debugger);
            Assert.That(snapshot.CoreCounters.RequestCount, Is.EqualTo(4));
            Assert.That(snapshot.CoreCounters.SpecCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.DeltaCount, Is.EqualTo(4));
            Assert.That(snapshot.CoreCounters.FactCount, Is.EqualTo(8));
            Assert.That(snapshot.CoreCounters.CueCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.PresentationCount, Is.EqualTo(2));
            Assert.That(snapshot.CoreCounters.EntityCreateCount, Is.EqualTo(4));
            Assert.That(snapshot.CoreCounters.EntityDestroyCount, Is.EqualTo(3));
            Assert.That(snapshot.CoreCounters.PeakActiveEffectEntityCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.PeakApplyRequestEntityCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.PeakEventBusBufferLength, Is.EqualTo(8));
            Assert.That(snapshot.CoreCounters.PeakPresentationCursorLag, Is.EqualTo(5));
            Assert.That(snapshot.CoreCounters.PeakReplayCursorLag, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectStoreOwnerCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectSlotCount, Is.EqualTo(4));
            Assert.That(snapshot.CoreCounters.ActiveEffectSlotCapacity, Is.GreaterThan(ActiveEffectStore.InlineSlotCapacity));
            Assert.That(snapshot.CoreCounters.ActiveEffectSlotPendingApplyCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectSlotActiveCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectSlotInhibitedCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectSlotPendingRemoveCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectSlotLegacyBackedCount, Is.EqualTo(3));
            Assert.That(snapshot.CoreCounters.ActiveEffectSlotExternalizedOwnerCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectSlotGrantedTagCount, Is.EqualTo(2));
            Assert.That(snapshot.CoreCounters.ActiveEffectSlotGrantedAbilityCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectChunkSkipMatchedSlotCount, Is.EqualTo(4));
            Assert.That(snapshot.CoreCounters.ActiveEffectChunkSkipSkippedSlotCount, Is.EqualTo(3));
            Assert.That(snapshot.CoreCounters.ActiveEffectChunkSkipDuePeriodSlotCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectChunkSkipNoopSlotCount, Is.EqualTo(3));
            Assert.That(snapshot.CoreCounters.ActiveEffectChunkSkipOwnerCount, Is.EqualTo(0));
            Assert.That(snapshot.CoreCounters.ActiveEffectGlobalIndexOwnerCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectGlobalIndexCount, Is.EqualTo(4));
            Assert.That(snapshot.CoreCounters.ActiveEffectGlobalIndexActiveCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectGlobalIndexInhibitedCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectGlobalIndexPendingRemoveCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectGlobalIndexPeriodDueCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectGlobalIndexDurationDueCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectGlobalIndexStaleCount, Is.EqualTo(1));
            Assert.That(snapshot.CoreCounters.ActiveEffectGlobalIndexStableRowCount, Is.EqualTo(3));
            Assert.That(snapshot.CoreCounters.ActiveEffectGlobalIndexStaleStableRowCount, Is.EqualTo(0));
            Assert.That(
                snapshot.CoreCounters.ActiveEffectGlobalIndexBucketOwnerCount,
                Is.EqualTo(ActiveEffectStore.GlobalIndexBucketCount));
            Assert.That(snapshot.CoreCounters.ActiveEffectGlobalIndexBucketIndexCount, Is.EqualTo(4));
            Assert.That(snapshot.CoreCounters.ActiveEffectGlobalIndexMaxBucketLength, Is.GreaterThanOrEqualTo(1));
            var frameBudget = GASRuntimeFrameBudgetPlanner.CreateCurrent();
            Assert.That(snapshot.CoreCounters.QueryBudget, Is.EqualTo(frameBudget.TotalQueryBudget));
            Assert.That(snapshot.CoreCounters.LookupUpdateBudget, Is.EqualTo(frameBudget.TotalLookupUpdateBudget));
            Assert.That(snapshot.CoreCounters.HelperTempQueryRiskCount, Is.EqualTo(frameBudget.HelperTempQueryRiskCount));
            Assert.That(snapshot.CoreCounters.DependencyWaitRiskCount, Is.EqualTo(frameBudget.DependencyWaitRiskCount));
            Assert.That(
                snapshot.CoreCounters.WorldUpdateAllocatorOwnerCount,
                Is.EqualTo(frameBudget.WorldUpdateAllocatorOwnerCount));

            var text = GasRuntimeDebugger.ExportToText(snapshot);
            Assert.That(text, Does.Contain("runtimeCoreCounters|requests=4"));
            Assert.That(text, Does.Contain("eventBusBufferLength=8"));
            Assert.That(text, Does.Contain("presentationCursorLag=5"));
            Assert.That(text, Does.Contain("replayCursorLag=1"));
            Assert.That(text, Does.Contain("runtimeCoreActiveEffectStore|owners=1|slots=4"));
            Assert.That(text, Does.Contain("legacyBacked=3"));
            Assert.That(text, Does.Contain("activeEffectSlotExternalizedOwners=1"));
            Assert.That(text, Does.Contain("grantedTags=2"));
            Assert.That(text, Does.Contain("grantedAbilities=1"));
            Assert.That(text, Does.Contain("chunkSkipMatched=4"));
            Assert.That(text, Does.Contain("chunkSkipSkipped=3"));
            Assert.That(text, Does.Contain("chunkSkipDuePeriod=1"));
            Assert.That(text, Does.Contain("globalIndexOwners=1"));
            Assert.That(text, Does.Contain("globalIndexCount=4"));
            Assert.That(text, Does.Contain("globalIndexPeriodDue=1"));
            Assert.That(text, Does.Contain("globalIndexDurationDue=1"));
            Assert.That(text, Does.Contain("globalIndexStale=1"));
            Assert.That(text, Does.Contain("globalIndexStableRows=3"));
            Assert.That(text, Does.Contain("globalIndexStaleStableRows=0"));
            Assert.That(text, Does.Contain("globalIndexBucketOwners=16"));
            Assert.That(text, Does.Contain("globalIndexBucketIndexCount=4"));
            Assert.That(text, Does.Contain("globalIndexMaxBucketLength="));
            Assert.That(text, Does.Contain("activeEffectSlotGrantedTags=2"));
            Assert.That(text, Does.Contain("activeEffectSlotGrantedAbilities=1"));
            Assert.That(text, Does.Contain("activeEffectChunkSkipDuePeriodSlots=1"));
            Assert.That(text, Does.Contain("activeEffectGlobalIndexOwners=1"));
            Assert.That(text, Does.Contain("activeEffectGlobalIndexCount=4"));
            Assert.That(text, Does.Contain("activeEffectGlobalIndexStableRows=3"));
            Assert.That(text, Does.Contain("activeEffectGlobalIndexStaleStableRows=0"));
            Assert.That(text, Does.Contain("activeEffectGlobalIndexBucketOwners=16"));
            Assert.That(text, Does.Contain("activeEffectGlobalIndexBucketIndexCount=4"));
            Assert.That(text, Does.Contain("activeEffectGlobalIndexMaxBucketLength="));
            Assert.That(text, Does.Contain("runtimeCoreFrameBudget|queryBudget="));
            Assert.That(text, Does.Contain("lookupUpdateBudget="));
            Assert.That(text, Does.Contain("worldUpdateAllocatorOwners=1"));
        }

        [Test]
        public void CollectRuntimeCoreCountersUsesCachedPresentationFallbackQuery()
        {
            var asc = _em.CreateEntity();
            var outbox = _em.AddBuffer<BPresentationEvent>(asc);
            outbox.Add(new BPresentationEvent());
            outbox.Add(new BPresentationEvent());
            outbox.Add(new BPresentationEvent());

            using var queries = GasRuntimeCoreCounterQueries.CreateOwned(_em);
            var counters = GasRuntimeDebugger.CollectRuntimeCoreCounters(
                _em,
                Entity.Null,
                Entity.Null,
                queries);

            Assert.That(counters.PresentationCount, Is.EqualTo(3));
        }

        [Test]
        public void RecordRuntimeCoreEcbPlaybackAccumulatesStructuralEvents()
        {
            var debugger = GasRuntimeDebugger.CreateSingleton(_em);

            GasRuntimeDebugger.RecordRuntimeCoreEcbPlayback(
                _em,
                debugger,
                11,
                EGasRuntimeDiagnosticModule.Effect);
            GasRuntimeDebugger.RecordRuntimeCoreEcbPlayback(
                _em,
                debugger,
                11,
                EGasRuntimeDiagnosticModule.Ability);

            var snapshot = GasRuntimeDebugger.CreateSnapshot(_em, debugger);
            Assert.That(snapshot.CoreCounters.EcbPlaybackCount, Is.EqualTo(2));
            Assert.That(snapshot.Events.Length, Is.EqualTo(2));
            Assert.That(snapshot.Events[0].Kind, Is.EqualTo(EGasRuntimeDiagnosticKind.StructuralChange));
            Assert.That(snapshot.Events[1].Kind, Is.EqualTo(EGasRuntimeDiagnosticKind.StructuralChange));

            var text = GasRuntimeDebugger.ExportToText(snapshot);
            Assert.That(text, Does.Contain("runtimeCoreCounters|requests=0"));
            Assert.That(text, Does.Contain("ecbPlaybacks=2"));
            Assert.That(text, Does.Contain("runtimeDiagnostic|seq=0|frame=11|kind=StructuralChange"));
        }

        [Test]
        public void RecordRuntimeCoreStructuralPlaybackGateTagsGateAndCommandCounts()
        {
            var debugger = GasRuntimeDebugger.CreateSingleton(_em);

            GasRuntimeDebugger.RecordRuntimeCoreStructuralPlaybackGate(
                _em,
                debugger,
                12,
                EGasRuntimeDiagnosticModule.Runtime,
                playbackCount: 1,
                ecbCommandCount: 7,
                bulkQueryCount: 2);

            var snapshot = GasRuntimeDebugger.CreateSnapshot(_em, debugger);
            Assert.That(snapshot.CoreCounters.EcbPlaybackCount, Is.EqualTo(1));
            Assert.That(snapshot.Events.Length, Is.EqualTo(1));
            Assert.That(snapshot.Events[0].Kind, Is.EqualTo(EGasRuntimeDiagnosticKind.StructuralChange));
            Assert.That(snapshot.Events[0].GroupName.ToString(), Is.EqualTo(GASRuntimeStructuralPlaybackGateNames.DebuggerGroupName));
            Assert.That(snapshot.Events[0].EcbPlaybackCount, Is.EqualTo(1));
            Assert.That(snapshot.Events[0].ValueA, Is.EqualTo(7));
            Assert.That(snapshot.Events[0].ValueB, Is.EqualTo(2));
            Assert.That(snapshot.FrameBackboneCounters.RecordedStructuralPlaybackCount, Is.EqualTo(1));
            Assert.That(snapshot.FrameBackboneCounters.EcbCommandCount, Is.EqualTo(7));
            Assert.That(snapshot.FrameBackboneCounters.BulkQueryCount, Is.EqualTo(2));

            var text = GasRuntimeDebugger.ExportToText(snapshot);
            Assert.That(text, Does.Contain("|group=StructuralPlayback"));
            Assert.That(text, Does.Contain("|ecbPlaybacks=1"));
            Assert.That(text, Does.Contain("|ecbCommands=7"));
            Assert.That(text, Does.Contain("|bulkQueries=2"));
            Assert.That(text, Does.Contain("recordedStructuralPlaybacks=1|ecbCommands=7|bulkQueries=2"));
        }

        [Test]
        public void RecordCurrentRuntimeCoreFrameBackboneEvidenceExportsDebuggerGateCounters()
        {
            var debugger = GasRuntimeDebugger.CreateSingleton(_em);
            var plan = GASRuntimeDebuggerEvidenceGatePlanner.CreateCurrent();

            GasRuntimeDebugger.RecordCurrentRuntimeCoreFrameBackboneEvidence(_em, debugger, 13);

            var snapshot = GasRuntimeDebugger.CreateSnapshot(_em, debugger);
            Assert.That(snapshot.FrameBackboneCounters.PhaseCount, Is.EqualTo(plan.PhaseCount));
            Assert.That(
                snapshot.FrameBackboneCounters.ContractOnlyPhaseCount,
                Is.EqualTo(plan.ContractOnlyPhaseCount));
            Assert.That(snapshot.FrameBackboneCounters.QueryBudget, Is.EqualTo(plan.QueryBudget));
            Assert.That(snapshot.FrameBackboneCounters.LookupUpdateBudget, Is.EqualTo(plan.LookupUpdateBudget));
            Assert.That(snapshot.FrameBackboneCounters.StreamCount, Is.EqualTo(plan.StreamCount));
            Assert.That(
                snapshot.FrameBackboneCounters.NativeStreamCandidateCount,
                Is.EqualTo(plan.NativeStreamCandidateCount));
            Assert.That(
                snapshot.FrameBackboneCounters.DeterministicMergePolicyCount,
                Is.EqualTo(plan.DeterministicMergePolicyCount));
            Assert.That(
                snapshot.FrameBackboneCounters.RequiredStructuralPlaybackCount,
                Is.EqualTo(plan.RequiredStructuralPlaybackCount));
            Assert.That(snapshot.FrameBackboneCounters.ProfilerMarkerCount, Is.EqualTo(4));
            Assert.That(snapshot.FrameBackboneCounters.JournalingMarkerCount, Is.EqualTo(1));
            Assert.That(snapshot.FrameBackboneCounters.CoreCostGroupCount, Is.EqualTo(1));
            Assert.That(snapshot.FrameBackboneCounters.PhysicsCostGroupCount, Is.EqualTo(1));
            Assert.That(snapshot.FrameBackboneCounters.RenderCostGroupCount, Is.EqualTo(1));
            Assert.That(snapshot.FrameBackboneCounters.RunnerCostGroupCount, Is.EqualTo(1));
            Assert.That(
                snapshot.FrameBackboneCounters.DebuggerOverheadBudgetMicroseconds,
                Is.EqualTo(GASRuntimeDebuggerEvidenceGatePlanner.DefaultOverheadBudgetMicroseconds));
            Assert.That(snapshot.FrameBackboneCounters.SamplingInterval, Is.EqualTo(1));
            Assert.That(snapshot.FrameBackboneCounters.DisablePolicyCount, Is.EqualTo(1));
            Assert.That(snapshot.FrameBackboneCounters.HotPathManagedStringCount, Is.EqualTo(0));
            Assert.That(snapshot.Events.Length, Is.EqualTo(1));
            Assert.That(snapshot.Events[0].Kind, Is.EqualTo(EGasRuntimeDiagnosticKind.RuntimeCoreFrameBackbone));
            Assert.That(snapshot.Events[0].GroupName.ToString(), Is.EqualTo(GASRuntimeDebuggerEvidenceGateNames.DebuggerGroupName));

            var text = GasRuntimeDebugger.ExportToText(snapshot);
            Assert.That(text, Does.Contain("runtimeCoreFrameBackbone|phases="));
            Assert.That(text, Does.Contain("|streams=6"));
            Assert.That(text, Does.Contain("|nativeStreamCandidates=3"));
            Assert.That(text, Does.Contain("|deterministicMergePolicies=6"));
            Assert.That(text, Does.Contain("|mergeCostUs=0|mergeCostMeasured=0"));
            Assert.That(text, Does.Contain("runtimeCoreFrameBackboneEvidence|profilerMarkers=4|journalingMarkers=1"));
            Assert.That(text, Does.Contain("runtimeCoreCostSplit|core=1|physics=1|render=1|runner=1"));
            Assert.That(text, Does.Contain("runtimeCoreDebuggerOverhead|samplingInterval=1|overheadBudgetUs=50"));
            Assert.That(text, Does.Contain("|disablePolicy=1|hotPathManagedStrings=0"));
        }

        private Entity CreateEventBus()
        {
            var eventBus = _em.CreateEntity();
            _em.AddComponentData(eventBus, new CGameplayEventBus());
            _em.AddComponentData(eventBus, new CPresentationOutboxProjectionState
            {
                LastProjectedFrame = 7,
                ProcessedGameplayEventCount = 1,
                ProcessedAttributeEventCount = 1,
                ProcessedCueRequestCount = 0,
                ProcessedTagEventCount = 1,
                ProcessedDamageEventCount = 0,
            });

            var gameplayEvents = _em.AddBuffer<BGameplayEvent>(eventBus);
            gameplayEvents.Add(new BGameplayEvent { Type = EGameplayEventType.GameplayEffectRequested });
            gameplayEvents.Add(new BGameplayEvent { Type = EGameplayEventType.GameplayEffectInstanced });
            gameplayEvents.Add(new BGameplayEvent { Type = EGameplayEventType.GameplayEffectRemoved });

            var attributeEvents = _em.AddBuffer<BAttributeChangeEvent>(eventBus);
            attributeEvents.Add(new BAttributeChangeEvent());
            attributeEvents.Add(new BAttributeChangeEvent());

            _em.AddBuffer<BCueRequest>(eventBus).Add(new BCueRequest());
            _em.AddBuffer<BTagChangeEvent>(eventBus).Add(new BTagChangeEvent());
            _em.AddBuffer<BDamageEvent>(eventBus).Add(new BDamageEvent());
            _em.AddBuffer<BPresentationOutboxOwner>(eventBus);
            return eventBus;
        }

        private Entity CreateReplaySink()
        {
            var replaySink = _em.CreateEntity();
            _em.AddComponentData(replaySink, new CGameplayEventLogSink
            {
                LastProjectedFrame = 7,
                ProcessedGameplayEventCount = 2,
                ProcessedAttributeEventCount = 2,
                ProcessedCueRequestCount = 1,
                ProcessedTagEventCount = 1,
                ProcessedDamageEventCount = 1,
            });
            _em.AddBuffer<BDebugReplayEvent>(replaySink);
            return replaySink;
        }

        private void CreateApplyRequest()
        {
            var request = _em.CreateEntity();
            _em.AddComponentData(request, new CApplyGameplayEffectRequest());
        }

        private void CreateAbilityCommandRequest()
        {
            var request = _em.CreateEntity();
            _em.AddComponentData(request, new CAbilityCommandRequest());
        }

        private void CreateActiveEffect()
        {
            var effect = _em.CreateEntity();
            _em.AddComponentData(effect, new CEffectSpecData());
            _em.AddComponentData(effect, new CEffectLifecycle
            {
                State = EGameplayEffectLifecycleState.Active,
                PreviousState = EGameplayEffectLifecycleState.Active,
                StateStartFrame = 7,
            });
        }

        private void CreateEffectDestroyMarker()
        {
            var effect = _em.CreateEntity();
            _em.AddComponent<CEffectDestroy>(effect);
        }

        private void CreateAscDestroyRequest()
        {
            var request = _em.CreateEntity();
            _em.AddComponentData(request, new CAscDestroyRequest());
        }

        private void CreatePresentationOutbox(Entity eventBus)
        {
            var asc = _em.CreateEntity();
            var presentationEvents = _em.AddBuffer<BPresentationEvent>(asc);
            presentationEvents.Add(new BPresentationEvent());
            presentationEvents.Add(new BPresentationEvent());

            var owners = _em.GetBuffer<BPresentationOutboxOwner>(eventBus);
            owners.Add(new BPresentationOutboxOwner { ASC = asc });
        }

        private void CreateActiveEffectStoreOwner()
        {
            var asc = _em.CreateEntity();
            _em.AddComponentData(asc, ActiveEffectStore.CreateDefault());
            var slots = _em.AddBuffer<BActiveEffectSlot>(asc);
            slots.EnsureCapacity(ActiveEffectStore.InlineSlotCapacity + 1);
            slots.Add(new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.PendingApply,
                Flags = (int)EActiveEffectSlotFlags.LegacyEntityBacked,
            });
            slots.Add(new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.Active,
                Flags = (int)EActiveEffectSlotFlags.LegacyEntityBacked,
                PeriodFrame = 5,
                LastPeriodFrame = 1,
                ActiveGrantedTagCount = 2,
            });
            slots.Add(new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.Inhibited,
                ActiveGrantedAbilityCount = 1,
            });
            slots.Add(new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.PendingRemove,
                Flags = (int)EActiveEffectSlotFlags.LegacyEntityBacked,
            });
        }

        private void CreateActiveEffectGlobalIndexOwner()
        {
            ActiveEffectStore.EnsureGlobalIndexStore(_em);
            var owner = _em.CreateEntity();
            var activeEffect = _em.CreateEntity();
            var inhibitedEffect = _em.CreateEntity();
            var pendingRemoveEffect = _em.CreateEntity();
            var staleEffect = _em.CreateEntity();
            AddStableRow(activeEffect);
            AddStableRow(inhibitedEffect);
            AddStableRow(pendingRemoveEffect);
            var entries = new NativeList<BGlobalActiveEffectIndex>(Allocator.Temp);
            try
            {
                entries.Add(new BGlobalActiveEffectIndex
                {
                    ActiveEffectEntity = activeEffect,
                    OwnerAsc = owner,
                    State = EActiveEffectSlotState.Active,
                    PeriodDueFrame = 7,
                    IndexFlags = (int)EActiveEffectGlobalIndexFlags.PeriodDue,
                });
                entries.Add(new BGlobalActiveEffectIndex
                {
                    ActiveEffectEntity = inhibitedEffect,
                    OwnerAsc = owner,
                    State = EActiveEffectSlotState.Inhibited,
                    DurationDueFrame = 7,
                    SlotFlags = (int)EActiveEffectSlotFlags.TicksWhenInactive,
                    IndexFlags = (int)(EActiveEffectGlobalIndexFlags.Inhibited | EActiveEffectGlobalIndexFlags.DurationDue),
                });
                entries.Add(new BGlobalActiveEffectIndex
                {
                    ActiveEffectEntity = pendingRemoveEffect,
                    OwnerAsc = owner,
                    State = EActiveEffectSlotState.PendingRemove,
                    IndexFlags = (int)EActiveEffectGlobalIndexFlags.PendingRemove,
                });
                entries.Add(new BGlobalActiveEffectIndex
                {
                    ActiveEffectEntity = staleEffect,
                    OwnerAsc = owner,
                    State = EActiveEffectSlotState.PendingApply,
                });

                Assert.That(ActiveEffectStore.TryMergeGlobalIndexEntries(_em, entries, currentFrame: 7), Is.True);
            }
            finally
            {
                entries.Dispose();
            }

            _em.DestroyEntity(staleEffect);
        }

        private void AddStableRow(Entity effect)
        {
            _em.AddComponentData(effect, ActiveEffectStore.CreateGlobalIndexStableRowDefault(effect));
        }
    }
}
