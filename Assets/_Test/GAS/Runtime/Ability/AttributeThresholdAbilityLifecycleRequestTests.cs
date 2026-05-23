using NUnit.Framework;
using Unity.Entities;

namespace GAS.Runtime.Tests.Ability
{
    public sealed class AttributeThresholdAbilityLifecycleRequestTests
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
            ClearTransientEventBuffers();
            ClearDebugReplayLog();
        }

        [Test]
        public void AttributeAboveThresholdDoesNotRequestLifecycle()
        {
            const int attrSetCode = 10;
            const int attrCode = 20;
            const int abilityCode = 100401;

            var asc = CreateStandardAsc();
            var ability = CreateRuntimeAbility(asc, abilityCode, EAbilityPhase.Active);

            try
            {
                AddAttribute(asc, attrSetCode, attrCode, currentValue: 5f);
                AddGrantedAbility(asc, ability);
                AddRule(
                    asc,
                    attrSetCode,
                    attrCode,
                    threshold: 0f,
                    abilityCode,
                    EAttributeThresholdAbilityLifecycleRequestType.End);

                RunAbilityGroup();
                RunCueGroup();

                Assert.That(_em.HasComponent<CAbilityInTryEnd>(ability), Is.False);
                Assert.That(_em.HasComponent<CAbilityActive>(ability), Is.True);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(ability).Phase, Is.EqualTo(EAbilityPhase.Active));
                AssertNoGameplayEvent(EGameplayEventType.AbilityEndRequested, abilityCode);
                AssertNoReplayEvent(EGameplayEventType.AbilityEndRequested, abilityCode);
            }
            finally
            {
                DestroyIfExists(ability);
                DestroyIfExists(asc);
            }
        }

        [Test]
        public void AttributeAtThresholdRequestsEndAndCleanupEmitsFacts()
        {
            const int attrSetCode = 11;
            const int attrCode = 21;
            const int abilityCode = 100402;

            var asc = CreateStandardAsc();
            var ability = CreateRuntimeAbility(asc, abilityCode, EAbilityPhase.Active);

            try
            {
                AddAttribute(asc, attrSetCode, attrCode, currentValue: 0f);
                AddGrantedAbility(asc, ability);
                AddRule(
                    asc,
                    attrSetCode,
                    attrCode,
                    threshold: 0f,
                    abilityCode,
                    EAttributeThresholdAbilityLifecycleRequestType.End);

                RunAbilityGroup();
                RunCueGroup();

                Assert.That(_em.HasComponent<CAbilityInTryEnd>(ability), Is.False);
                Assert.That(_em.HasComponent<CAbilityActive>(ability), Is.False);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(ability).Phase, Is.EqualTo(EAbilityPhase.Ready));

                var requestEvent = AssertGameplayEvent(EGameplayEventType.AbilityEndRequested, ability, abilityCode);
                Assert.That(requestEvent.ReasonCode, Is.EqualTo((int)EAbilityLifecycleReason.AttributeThreshold));
                Assert.That(requestEvent.RelatedAbility, Is.EqualTo(ability));
                Assert.That(requestEvent.RelatedAbilityCode, Is.EqualTo(abilityCode));

                var endedEvent = AssertGameplayEvent(EGameplayEventType.AbilityEnded, ability, abilityCode);
                Assert.That(endedEvent.ReasonCode, Is.EqualTo((int)EAbilityLifecycleReason.AttributeThreshold));
                Assert.That(endedEvent.RelatedAbility, Is.EqualTo(ability));
                Assert.That(endedEvent.RelatedAbilityCode, Is.EqualTo(abilityCode));

                AssertReplayEvent(EGameplayEventType.AbilityEndRequested, abilityCode);
                AssertReplayEvent(EGameplayEventType.AbilityEnded, abilityCode);
            }
            finally
            {
                DestroyIfExists(ability);
                DestroyIfExists(asc);
            }
        }

        [Test]
        public void AttributeBelowThresholdRequestsCancelAndCleanupEmitsFacts()
        {
            const int attrSetCode = 12;
            const int attrCode = 22;
            const int abilityCode = 100403;

            var asc = CreateStandardAsc();
            var ability = CreateRuntimeAbility(asc, abilityCode, EAbilityPhase.Activating);

            try
            {
                AddAttribute(asc, attrSetCode, attrCode, currentValue: -1f);
                AddGrantedAbility(asc, ability);
                AddRule(
                    asc,
                    attrSetCode,
                    attrCode,
                    threshold: 0f,
                    abilityCode,
                    EAttributeThresholdAbilityLifecycleRequestType.Cancel);

                RunAbilityGroup();
                RunCueGroup();

                Assert.That(_em.HasComponent<CAbilityInTryCancel>(ability), Is.False);
                Assert.That(_em.HasComponent<CAbilityActive>(ability), Is.False);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(ability).Phase, Is.EqualTo(EAbilityPhase.Ready));

                var requestEvent = AssertGameplayEvent(EGameplayEventType.AbilityCancelRequested, ability, abilityCode);
                Assert.That(requestEvent.ReasonCode, Is.EqualTo((int)EAbilityLifecycleReason.AttributeThreshold));
                Assert.That(requestEvent.RelatedAbility, Is.EqualTo(ability));
                Assert.That(requestEvent.RelatedAbilityCode, Is.EqualTo(abilityCode));

                var canceledEvent = AssertGameplayEvent(EGameplayEventType.AbilityCanceled, ability, abilityCode);
                Assert.That(canceledEvent.ReasonCode, Is.EqualTo((int)EAbilityLifecycleReason.AttributeThreshold));
                Assert.That(canceledEvent.RelatedAbility, Is.EqualTo(ability));
                Assert.That(canceledEvent.RelatedAbilityCode, Is.EqualTo(abilityCode));

                AssertReplayEvent(EGameplayEventType.AbilityCancelRequested, abilityCode);
                AssertReplayEvent(EGameplayEventType.AbilityCanceled, abilityCode);
            }
            finally
            {
                DestroyIfExists(ability);
                DestroyIfExists(asc);
            }
        }

        [Test]
        public void ReadyAbilityIsIgnoredEvenWhenAttributeIsBelowThreshold()
        {
            const int attrSetCode = 13;
            const int attrCode = 23;
            const int abilityCode = 100404;

            var asc = CreateStandardAsc();
            var ability = CreateRuntimeAbility(asc, abilityCode, EAbilityPhase.Ready, addActiveComponent: false);

            try
            {
                AddAttribute(asc, attrSetCode, attrCode, currentValue: 0f);
                AddGrantedAbility(asc, ability);
                AddRule(
                    asc,
                    attrSetCode,
                    attrCode,
                    threshold: 1f,
                    abilityCode,
                    EAttributeThresholdAbilityLifecycleRequestType.End);

                RunAbilityGroup();
                RunCueGroup();

                Assert.That(_em.HasComponent<CAbilityInTryEnd>(ability), Is.False);
                Assert.That(_em.GetComponentData<CAbilityRuntimeState>(ability).Phase, Is.EqualTo(EAbilityPhase.Ready));
                AssertNoGameplayEvent(EGameplayEventType.AbilityEndRequested, abilityCode);
                AssertNoReplayEvent(EGameplayEventType.AbilityEndRequested, abilityCode);
            }
            finally
            {
                DestroyIfExists(ability);
                DestroyIfExists(asc);
            }
        }

        private Entity CreateStandardAsc()
        {
            var asc = _em.CreateEntity();
            _em.AddComponentData(asc, new CTagMask());
            _em.AddComponentData(asc, new CFixedTagMask());
            _em.AddBuffer<BAttribute>(asc);
            _em.AddBuffer<BActiveModifier>(asc);
            _em.AddBuffer<BGrantedAbility>(asc);
            _em.AddBuffer<BFixedTagSource>(asc);
            _em.AddBuffer<BTempTagSource>(asc);
            _em.AddBuffer<BGameplayEffect>(asc);
            _em.AddBuffer<BPresentationEvent>(asc);
            return asc;
        }

        private Entity CreateRuntimeAbility(
            Entity owner,
            int abilityCode,
            EAbilityPhase phase,
            bool addActiveComponent = true)
        {
            var ability = _em.CreateEntity();
            _em.AddComponentData(ability, new CAbilityBaseInfo
            {
                Code = abilityCode,
                Level = 1,
                Owner = owner,
            });
            _em.AddComponentData(ability, new CAbilityRuntimeState
            {
                Phase = phase,
            });

            if (addActiveComponent)
                _em.AddComponent<CAbilityActive>(ability);

            return ability;
        }

        private void AddAttribute(Entity asc, int attrSetCode, int attrCode, float currentValue)
        {
            _em.GetBuffer<BAttribute>(asc).Add(new BAttribute
            {
                AttrSetCode = attrSetCode,
                Code = attrCode,
                BaseValue = currentValue,
                CurrentValue = currentValue,
                PreviousCurrentValue = currentValue,
            });
        }

        private void AddGrantedAbility(Entity asc, Entity ability)
        {
            _em.GetBuffer<BGrantedAbility>(asc).Add(new BGrantedAbility
            {
                AbilityEntity = ability,
            });
        }

        private void AddRule(
            Entity asc,
            int attrSetCode,
            int attrCode,
            float threshold,
            int abilityCode,
            EAttributeThresholdAbilityLifecycleRequestType requestType)
        {
            _em.AddComponentData(asc, new CAttributeThresholdAbilityLifecycleRule
            {
                AttrSetCode = attrSetCode,
                AttrCode = attrCode,
                Threshold = threshold,
                AbilityCode = abilityCode,
                RequestType = requestType,
                Reason = EAbilityLifecycleReason.AttributeThreshold,
            });
        }

        private BGameplayEvent AssertGameplayEvent(EGameplayEventType type, Entity ability, int abilityCode)
        {
            var events = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Type == type
                    && evt.SourceAbility == ability
                    && evt.EventCode == abilityCode)
                {
                    return evt;
                }
            }

            Assert.Fail($"Gameplay event {type} for ability {abilityCode} was not found.");
            return default;
        }

        private void AssertNoGameplayEvent(EGameplayEventType type, int abilityCode)
        {
            var events = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Type == type && evt.EventCode == abilityCode)
                    Assert.Fail($"Unexpected gameplay event {type} for ability {abilityCode}.");
            }
        }

        private void AssertReplayEvent(EGameplayEventType type, int abilityCode)
        {
            var events = _em.GetBuffer<BDebugReplayEvent>(GASManager.EntityEventLogSink);
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind == EDebugReplayEventKind.GameplayEvent
                    && evt.GameplayEventType == type
                    && evt.EventCode == abilityCode
                    && evt.ReasonCode == (int)EAbilityLifecycleReason.AttributeThreshold)
                {
                    return;
                }
            }

            Assert.Fail($"Replay event {type} for ability {abilityCode} was not found.");
        }

        private void AssertNoReplayEvent(EGameplayEventType type, int abilityCode)
        {
            var events = _em.GetBuffer<BDebugReplayEvent>(GASManager.EntityEventLogSink);
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind == EDebugReplayEventKind.GameplayEvent
                    && evt.GameplayEventType == type
                    && evt.EventCode == abilityCode)
                {
                    Assert.Fail($"Unexpected replay event {type} for ability {abilityCode}.");
                }
            }
        }

        private void ClearTransientEventBuffers()
        {
            if (!GASManager.IsInitialized || !_em.Exists(GASManager.EntityEventBus))
                return;

            _em.GetBuffer<BDamageEvent>(GASManager.EntityEventBus).Clear();
            _em.GetBuffer<BTagChangeEvent>(GASManager.EntityEventBus).Clear();
            _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus).Clear();
            _em.GetBuffer<BAttributeChangeEvent>(GASManager.EntityEventBus).Clear();
            _em.GetBuffer<BCueRequest>(GASManager.EntityEventBus).Clear();

            if (_em.HasComponent<CPresentationOutboxProjectionState>(GASManager.EntityEventBus))
                _em.SetComponentData(GASManager.EntityEventBus, new CPresentationOutboxProjectionState());
        }

        private void ClearDebugReplayLog()
        {
            if (!GASManager.IsInitialized || !_em.Exists(GASManager.EntityEventLogSink))
                return;

            _em.GetBuffer<BDebugReplayEvent>(GASManager.EntityEventLogSink).Clear();
            _em.SetComponentData(GASManager.EntityEventLogSink, new CGameplayEventLogSink());
        }

        private static void RunAbilityGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASAbilityGroup>().Update();
        }

        private static void RunCueGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCueGroup>().Update();
        }

        private void DestroyIfExists(Entity entity)
        {
            if (entity != Entity.Null && _em.Exists(entity))
                _em.DestroyEntity(entity);
        }
    }
}
