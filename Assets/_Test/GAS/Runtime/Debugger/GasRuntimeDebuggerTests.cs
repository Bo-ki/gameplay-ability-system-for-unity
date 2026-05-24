using NUnit.Framework;
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

            GasRuntimeDebugger.CollectAndRecordRuntimeCoreCounters(
                _em,
                debugger,
                7,
                eventBus,
                replaySink);

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

            var text = GasRuntimeDebugger.ExportToText(snapshot);
            Assert.That(text, Does.Contain("runtimeCoreCounters|requests=4"));
            Assert.That(text, Does.Contain("eventBusBufferLength=8"));
            Assert.That(text, Does.Contain("presentationCursorLag=5"));
            Assert.That(text, Does.Contain("replayCursorLag=1"));
            Assert.That(text, Does.Contain("runtimeCoreActiveEffectStore|owners=1|slots=4"));
            Assert.That(text, Does.Contain("legacyBacked=3"));
            Assert.That(text, Does.Contain("activeEffectSlotExternalizedOwners=1"));
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
            });
            slots.Add(new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.Inhibited,
            });
            slots.Add(new BActiveEffectSlot
            {
                State = EActiveEffectSlotState.PendingRemove,
                Flags = (int)EActiveEffectSlotFlags.LegacyEntityBacked,
            });
        }
    }
}
