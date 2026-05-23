using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Tests
{
    public sealed class StackingRuntimeTests
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
            ClearApplyRequests();
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
            ClearApplyRequests();
            ClearGameplayEvents();
        }

        [Test]
        public void StackApplicationBelowLimitMergesIntoExistingStackAndSyncsActiveModifier()
        {
            var fixture = CreateStackMergeFixture();

            var merged = EffectRuntimeUtility.TryMergeStackingApplication(
                _em,
                fixture.Incoming,
                fixture.Context,
                ref fixture.IncomingDuration,
                currentFrame: 150);

            Assert.That(merged, Is.True);
            Assert.That(_em.Exists(fixture.Incoming), Is.False);
            Assert.That(_em.HasComponent<CEffectDestroy>(fixture.Existing), Is.False);
            Assert.That(_em.GetComponentData<CStackingRuntime>(fixture.Existing).StackCount, Is.EqualTo(2));
            Assert.That(_em.GetComponentData<CEffectSpecData>(fixture.Existing).StackCount, Is.EqualTo(2));

            var duration = _em.GetComponentData<CDurationRuntime>(fixture.Existing);
            Assert.That(duration.ActiveTime, Is.EqualTo(150));
            Assert.That(duration.LastActiveTime, Is.EqualTo(150));
            Assert.That(duration.RemainingTime, Is.EqualTo(30));

            var activeModifier = _em.GetBuffer<BActiveModifier>(fixture.Target)[0];
            Assert.That(activeModifier.SourceEntity, Is.EqualTo(fixture.Existing));
            Assert.That(activeModifier.AttrSetCode, Is.EqualTo(1101));
            Assert.That(activeModifier.AttributeCode, Is.EqualTo(2201));
            Assert.That(activeModifier.Op, Is.EqualTo(EModifierOp.Add));
            Assert.That(activeModifier.Magnitude, Is.EqualTo(20));
            Assert.That(_em.GetBuffer<BAttribute>(fixture.Target)[0].Dirty, Is.True);

            AssertHasGameplayEvent(EGameplayEventType.StackCountChanged, fixture.Existing, eventCode: 1, value: 2);
            AssertHasGameplayEvent(EGameplayEventType.ActiveModifierUpdated, fixture.Existing, eventCode: 2201, value: 20);
            AssertDoesNotHaveGameplayEvent(EGameplayEventType.StackOverflow);
        }

        [Test]
        public void AggregateBySourceWithDifferentSourceKeepsSeparateStack()
        {
            var fixture = CreateSourceScopeFixture(EffectStackType.AggregateBySource);

            var merged = EffectRuntimeUtility.TryMergeStackingApplication(
                _em,
                fixture.Incoming,
                fixture.Context,
                ref fixture.IncomingDuration,
                currentFrame: 150);

            Assert.That(merged, Is.False);
            Assert.That(_em.Exists(fixture.Incoming), Is.True);
            Assert.That(_em.GetComponentData<CStackingRuntime>(fixture.Existing).StackCount, Is.EqualTo(1));
            Assert.That(_em.GetComponentData<CStackingRuntime>(fixture.Incoming).StackCount, Is.EqualTo(1));
            Assert.That(_em.GetComponentData<CDurationRuntime>(fixture.Existing).ActiveTime, Is.EqualTo(10));
            AssertDoesNotHaveGameplayEvent(EGameplayEventType.StackCountChanged);
            AssertDoesNotHaveGameplayEvent(EGameplayEventType.StackOverflow);
        }

        [Test]
        public void AggregateByTargetWithDifferentSourceMergesIntoSharedStack()
        {
            var fixture = CreateSourceScopeFixture(EffectStackType.AggregateByTarget);

            var merged = EffectRuntimeUtility.TryMergeStackingApplication(
                _em,
                fixture.Incoming,
                fixture.Context,
                ref fixture.IncomingDuration,
                currentFrame: 150);

            Assert.That(merged, Is.True);
            Assert.That(_em.Exists(fixture.Incoming), Is.False);
            Assert.That(_em.GetComponentData<CStackingRuntime>(fixture.Existing).StackCount, Is.EqualTo(2));
            Assert.That(_em.GetComponentData<CDurationRuntime>(fixture.Existing).ActiveTime, Is.EqualTo(150));
            AssertHasGameplayEvent(EGameplayEventType.StackCountChanged, fixture.Existing, eventCode: 1, value: 2);
            AssertDoesNotHaveGameplayEvent(EGameplayEventType.StackOverflow);
        }

        [Test]
        public void OverflowWithClearStackMarksExistingForRemovalAndEmitsEvents()
        {
            var fixture = CreateStackingFixture(denyOverflowApplication: true, clearStackOnOverflow: true);

            var merged = EffectRuntimeUtility.TryMergeStackingApplication(
                _em,
                fixture.Incoming,
                fixture.Context,
                ref fixture.IncomingDuration,
                currentFrame: 100);

            Assert.That(merged, Is.True);
            Assert.That(_em.Exists(fixture.Incoming), Is.False);
            Assert.That(_em.HasComponent<CEffectDestroy>(fixture.Existing), Is.True);
            AssertHasStackEvent(EGameplayEventType.StackOverflow, fixture.Existing, fixture.StackingCode, 2);
            AssertHasStackEvent(EGameplayEventType.StackClearedByOverflow, fixture.Existing, fixture.StackingCode, 2);
        }

        [Test]
        public void OverflowWithClearStackDoesNotRequireDenyOverflowApplication()
        {
            var fixture = CreateStackingFixture(denyOverflowApplication: false, clearStackOnOverflow: true);

            var merged = EffectRuntimeUtility.TryMergeStackingApplication(
                _em,
                fixture.Incoming,
                fixture.Context,
                ref fixture.IncomingDuration,
                currentFrame: 100);

            Assert.That(merged, Is.True);
            Assert.That(_em.Exists(fixture.Incoming), Is.False);
            Assert.That(_em.HasComponent<CEffectDestroy>(fixture.Existing), Is.True);
            AssertHasStackEvent(EGameplayEventType.StackOverflow, fixture.Existing, fixture.StackingCode, 2);
            AssertHasStackEvent(EGameplayEventType.StackClearedByOverflow, fixture.Existing, fixture.StackingCode, 2);
            AssertDoesNotHaveStackEvent(EGameplayEventType.StackOverflowDenied);
            AssertDoesNotHaveStackEvent(EGameplayEventType.StackOverflowRefreshed);
        }

        [Test]
        public void OverflowWithDenyDoesNotRefreshExistingDurationAndEmitsDeniedEvent()
        {
            var fixture = CreateStackingFixture(denyOverflowApplication: true, clearStackOnOverflow: false);

            var merged = EffectRuntimeUtility.TryMergeStackingApplication(
                _em,
                fixture.Incoming,
                fixture.Context,
                ref fixture.IncomingDuration,
                currentFrame: 100);

            Assert.That(merged, Is.True);
            Assert.That(_em.Exists(fixture.Incoming), Is.False);
            Assert.That(_em.HasComponent<CEffectDestroy>(fixture.Existing), Is.False);
            Assert.That(_em.GetComponentData<CDurationRuntime>(fixture.Existing).ActiveTime, Is.EqualTo(10));
            AssertHasStackEvent(EGameplayEventType.StackOverflow, fixture.Existing, fixture.StackingCode, 2);
            AssertHasStackEvent(EGameplayEventType.StackOverflowDenied, fixture.Existing, fixture.StackingCode, 2);
        }

        [Test]
        public void OverflowWithoutDenyRefreshesExistingDurationAndEmitsRefreshedEvent()
        {
            var fixture = CreateStackingFixture(denyOverflowApplication: false, clearStackOnOverflow: false);

            var merged = EffectRuntimeUtility.TryMergeStackingApplication(
                _em,
                fixture.Incoming,
                fixture.Context,
                ref fixture.IncomingDuration,
                currentFrame: 100);

            Assert.That(merged, Is.True);
            Assert.That(_em.Exists(fixture.Incoming), Is.False);
            Assert.That(_em.HasComponent<CEffectDestroy>(fixture.Existing), Is.False);
            Assert.That(_em.GetComponentData<CDurationRuntime>(fixture.Existing).ActiveTime, Is.EqualTo(100));
            AssertHasStackEvent(EGameplayEventType.StackOverflow, fixture.Existing, fixture.StackingCode, 2);
            AssertHasStackEvent(EGameplayEventType.StackOverflowRefreshed, fixture.Existing, fixture.StackingCode, 2);
        }

        [Test]
        public void DurationExpirationWithRemoveSingleStackDecrementsStackAndRefreshesDuration()
        {
            var fixture = CreateDurationExpirationFixture(
                EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration,
                stackCount: 3);
            var duration = _em.GetComponentData<CDurationRuntime>(fixture.Existing);

            EffectRuntimeUtility.HandleDurationExpired(
                _em,
                fixture.Existing,
                fixture.Context,
                ref duration,
                currentFrame: 240);

            Assert.That(_em.HasComponent<CEffectDestroy>(fixture.Existing), Is.False);
            Assert.That(_em.GetComponentData<CStackingRuntime>(fixture.Existing).StackCount, Is.EqualTo(2));

            var storedDuration = _em.GetComponentData<CDurationRuntime>(fixture.Existing);
            Assert.That(storedDuration.ActiveTime, Is.EqualTo(240));
            Assert.That(storedDuration.LastActiveTime, Is.EqualTo(240));
            Assert.That(storedDuration.RemainingTime, Is.EqualTo(30));
            AssertHasGameplayEvent(EGameplayEventType.StackCountChanged, fixture.Existing, eventCode: 3, value: 2);
        }

        [Test]
        public void DurationExpirationWithRemoveSingleStackAtOneMarksForRemoval()
        {
            var fixture = CreateDurationExpirationFixture(
                EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration,
                stackCount: 1);
            var duration = _em.GetComponentData<CDurationRuntime>(fixture.Existing);

            EffectRuntimeUtility.HandleDurationExpired(
                _em,
                fixture.Existing,
                fixture.Context,
                ref duration,
                currentFrame: 240);

            Assert.That(_em.HasComponent<CEffectDestroy>(fixture.Existing), Is.True);
            Assert.That(_em.GetComponentData<CEffectLifecycle>(fixture.Existing).State,
                Is.EqualTo(EGameplayEffectLifecycleState.PendingRemove));
            AssertDoesNotHaveGameplayEvent(EGameplayEventType.StackCountChanged);
        }

        [Test]
        public void DurationExpirationWithRefreshDurationKeepsStackAndRefreshesDuration()
        {
            var fixture = CreateDurationExpirationFixture(
                EffectExpirationPolicy.RefreshDuration,
                stackCount: 2);
            var duration = _em.GetComponentData<CDurationRuntime>(fixture.Existing);

            EffectRuntimeUtility.HandleDurationExpired(
                _em,
                fixture.Existing,
                fixture.Context,
                ref duration,
                currentFrame: 240);

            Assert.That(_em.HasComponent<CEffectDestroy>(fixture.Existing), Is.False);
            Assert.That(_em.GetComponentData<CStackingRuntime>(fixture.Existing).StackCount, Is.EqualTo(2));

            var storedDuration = _em.GetComponentData<CDurationRuntime>(fixture.Existing);
            Assert.That(storedDuration.ActiveTime, Is.EqualTo(240));
            Assert.That(storedDuration.LastActiveTime, Is.EqualTo(240));
            Assert.That(storedDuration.RemainingTime, Is.EqualTo(30));
            AssertDoesNotHaveGameplayEvent(EGameplayEventType.StackCountChanged);
        }

        [Test]
        public void DurationExpirationWithClearEntireStackMarksForRemoval()
        {
            var fixture = CreateDurationExpirationFixture(
                EffectExpirationPolicy.ClearEntireStack,
                stackCount: 2);
            var duration = _em.GetComponentData<CDurationRuntime>(fixture.Existing);

            EffectRuntimeUtility.HandleDurationExpired(
                _em,
                fixture.Existing,
                fixture.Context,
                ref duration,
                currentFrame: 240);

            Assert.That(_em.HasComponent<CEffectDestroy>(fixture.Existing), Is.True);
            Assert.That(_em.GetComponentData<CEffectLifecycle>(fixture.Existing).State,
                Is.EqualTo(EGameplayEffectLifecycleState.PendingRemove));
            AssertDoesNotHaveGameplayEvent(EGameplayEventType.StackCountChanged);
        }

        [Test]
        public void StackCountMagnitudeRefreshesResolvedAndActiveModifier()
        {
            var fixture = CreateStackMergeFixture();

            var merged = EffectRuntimeUtility.TryMergeStackingApplication(
                _em,
                fixture.Incoming,
                fixture.Context,
                ref fixture.IncomingDuration,
                currentFrame: 150);

            Assert.That(merged, Is.True);

            var spec = _em.GetComponentData<CEffectSpecData>(fixture.Existing);
            Assert.That(spec.StackCount, Is.EqualTo(2));

            var resolvedModifiers = _em.GetBuffer<BResolvedModifier>(fixture.Existing);
            Assert.That(resolvedModifiers.Length, Is.EqualTo(1));
            Assert.That(resolvedModifiers[0].Magnitude, Is.EqualTo(20));

            var activeModifier = _em.GetBuffer<BActiveModifier>(fixture.Target)[0];
            Assert.That(activeModifier.SourceEntity, Is.EqualTo(fixture.Existing));
            Assert.That(activeModifier.Magnitude, Is.EqualTo(20));
        }

        [Test]
        public void PeriodEffectWithoutStaticDefinitionDoesNotCreateApplyRequest()
        {
            const int periodEffectCode = 93001;
            var previousTimer = SetFrame(100);

            try
            {
                var source = CreateEntity();
                var target = CreateEntity();
                _em.AddBuffer<BGameplayEffect>(target);

                var effect = CreateEntity();
                var context = new CEffectContext
                {
                    SourceAsc = source,
                    TargetAsc = target,
                    Instigator = source,
                    ContextId = 930,
                };

                _em.AddComponentData(effect, context);
                _em.AddComponentData(effect, new CEffectLifecycle
                {
                    State = EGameplayEffectLifecycleState.Active,
                    PreviousState = EGameplayEffectLifecycleState.Active,
                    StateStartFrame = 1,
                });
                AddDuration(effect, duration: 1000, active: true, activeTime: 0, remainingTime: 1000);
                _em.AddComponentData(effect, new CPeriodDefinition
                {
                    Period = 5,
                });
                _em.AddComponentData(effect, new CPeriodRuntime { StartTime = 90 });
                _em.AddBuffer<BPeriodGEConfig>(effect).Add(new BPeriodGEConfig
                {
                    GameplayEffectCode = periodEffectCode,
                });
                _em.AddBuffer<BSetByCallerValue>(effect).Add(new BSetByCallerValue
                {
                    Key = 7,
                    Value = 77,
                });
                _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect { GameplayEffect = effect });

                Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, effect, out _), Is.False);

                RunEffectGroup();

                Assert.That(CountApplyRequests(periodEffectCode), Is.EqualTo(0));
                var periodRuntime = _em.GetComponentData<CPeriodRuntime>(effect);
                Assert.That(periodRuntime.StartTime, Is.EqualTo(90));
            }
            finally
            {
                RestoreFrame(previousTimer);
            }
        }

        [Test]
        public void PeriodEffectReadsEffectCodesFromStaticDefinitionBlob()
        {
            const int sourceEffectCode = 93021;
            const int periodEffectCode = 93022;
            var previousTimer = SetFrame(100);

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == sourceEffectCode ? CreatePeriodDefinitionConfig(periodEffectCode) : null);

            try
            {
                var source = CreateEntity();
                var target = CreateEntity();
                _em.AddBuffer<BGameplayEffect>(target);

                var effect = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(_em, sourceEffectCode);
                _entities.Add(effect);
                Assert.That(effect, Is.Not.EqualTo(Entity.Null));
                Assert.That(_em.HasComponent<CGameplayEffectPrototype>(effect), Is.False);
                Assert.That(_em.HasComponent<CPeriodDefinition>(effect), Is.True);
                Assert.That(_em.HasComponent<CPeriodRuntime>(effect), Is.True);
                Assert.That(_em.HasBuffer<BPeriodGEConfig>(effect), Is.True);

                _em.RemoveComponent<BPeriodGEConfig>(effect);
                Assert.That(_em.HasBuffer<BPeriodGEConfig>(effect), Is.False);

                var context = new CEffectContext
                {
                    SourceAsc = source,
                    TargetAsc = target,
                    Instigator = source,
                    ContextId = 9321,
                };

                _em.AddComponentData(effect, context);
                _em.AddComponentData(effect, new CEffectSpecData
                {
                    GameplayEffectCode = sourceEffectCode,
                    Level = 4,
                    StackCount = 1,
                });
                _em.AddComponentData(effect, new CEffectLifecycle
                {
                    State = EGameplayEffectLifecycleState.Active,
                    PreviousState = EGameplayEffectLifecycleState.Active,
                    StateStartFrame = 1,
                });
                _em.SetComponentData(effect, new CDurationRuntime
                {
                    ResolvedDuration = 1000,
                    ResolvedTimeUnit = TimeUnit.Frame,
                    Active = true,
                    ActiveTime = 0,
                    RemainingTime = 1000,
                });
                _em.SetComponentData(effect, new CPeriodRuntime
                {
                    StartTime = 90,
                });
                _em.AddBuffer<BSetByCallerValue>(effect).Add(new BSetByCallerValue
                {
                    Key = 9,
                    Value = 99,
                });
                _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect { GameplayEffect = effect });

                Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, effect, out var blob), Is.True);
                Assert.That(blob.Value.PeriodEffectCodes.Length, Is.EqualTo(1));
                Assert.That(blob.Value.PeriodEffectCodes[0], Is.EqualTo(periodEffectCode));

                RunEffectGroup();

                var requestEntity = FindSingleApplyRequest(periodEffectCode);
                AssertDerivedRequest(requestEntity, context, expectedCauser: effect, periodEffectCode);

                var setByCallers = _em.GetBuffer<BSetByCallerValue>(requestEntity);
                Assert.That(setByCallers.Length, Is.EqualTo(1));
                Assert.That(setByCallers[0].Key, Is.EqualTo(9));
                Assert.That(setByCallers[0].Value, Is.EqualTo(99));

                var periodRuntime = _em.GetComponentData<CPeriodRuntime>(effect);
                Assert.That(periodRuntime.StartTime, Is.EqualTo(100));
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                RestoreFrame(previousTimer);
            }
        }

        [Test]
        public void MissingPeriodDerivedGameplayEffectConfigIsDiscardedWhenRequestIsConsumed()
        {
            const int sourceEffectCode = 93041;
            const int missingPeriodEffectCode = 93042;
            var previousTimer = SetFrame(100);

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == sourceEffectCode ? CreatePeriodDefinitionConfig(missingPeriodEffectCode) : null);

            try
            {
                var source = CreateEntity();
                var target = CreateEntity();
                _em.AddBuffer<BGameplayEffect>(target);

                var effect = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(_em, sourceEffectCode);
                _entities.Add(effect);
                Assert.That(effect, Is.Not.EqualTo(Entity.Null));

                var context = new CEffectContext
                {
                    SourceAsc = source,
                    TargetAsc = target,
                    Instigator = source,
                    ContextId = 9341,
                };

                _em.AddComponentData(effect, context);
                _em.AddComponentData(effect, new CEffectSpecData
                {
                    GameplayEffectCode = sourceEffectCode,
                    Level = 4,
                    StackCount = 1,
                });
                _em.AddComponentData(effect, new CEffectLifecycle
                {
                    State = EGameplayEffectLifecycleState.Active,
                    PreviousState = EGameplayEffectLifecycleState.Active,
                    StateStartFrame = 1,
                });
                _em.SetComponentData(effect, new CDurationRuntime
                {
                    ResolvedDuration = 1000,
                    ResolvedTimeUnit = TimeUnit.Frame,
                    Active = true,
                    ActiveTime = 0,
                    RemainingTime = 1000,
                });
                _em.SetComponentData(effect, new CPeriodRuntime
                {
                    StartTime = 90,
                });
                _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect { GameplayEffect = effect });

                RunEffectGroup();
                var requestEntity = FindSingleApplyRequest(missingPeriodEffectCode);
                AssertDerivedRequest(requestEntity, context, expectedCauser: effect, missingPeriodEffectCode);

                RunCommandGroup();

                Assert.That(CountApplyRequests(missingPeriodEffectCode), Is.EqualTo(0));
                Assert.That(CountEffectsByCode(missingPeriodEffectCode), Is.EqualTo(0));
                Assert.That(_em.Exists(effect), Is.True);
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                RestoreFrame(previousTimer);
            }
        }

        [Test]
        public void ConsumedPeriodDerivedRequestPreservesSourceEffectParentContextAndSpecLevel()
        {
            const int sourceEffectCode = 93061;
            const int periodEffectCode = 93062;
            var previousTimer = SetFrame(100);

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
            {
                if (id == sourceEffectCode)
                    return CreatePeriodDefinitionConfig(periodEffectCode);

                return id == periodEffectCode
                    ? new GameplayEffectConfig(System.Array.Empty<GameplayEffectComponentConfig>())
                    : null;
            });

            try
            {
                var source = CreateEntity();
                var target = CreateEntity();
                var ability = CreateEntity();
                _em.AddBuffer<BGameplayEffect>(target);

                var effect = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(_em, sourceEffectCode);
                _entities.Add(effect);
                Assert.That(effect, Is.Not.EqualTo(Entity.Null));

                var context = new CEffectContext
                {
                    SourceAsc = source,
                    TargetAsc = target,
                    SourceAbility = ability,
                    Instigator = source,
                    ContextId = 9361,
                };

                _em.AddComponentData(effect, context);
                _em.AddComponentData(effect, new CEffectSpecData
                {
                    GameplayEffectCode = sourceEffectCode,
                    Level = 7,
                    StackCount = 1,
                });
                _em.AddComponentData(effect, new CEffectLifecycle
                {
                    State = EGameplayEffectLifecycleState.Active,
                    PreviousState = EGameplayEffectLifecycleState.Active,
                    StateStartFrame = 1,
                });
                _em.SetComponentData(effect, new CDurationRuntime
                {
                    ResolvedDuration = 1000,
                    ResolvedTimeUnit = TimeUnit.Frame,
                    Active = true,
                    ActiveTime = 0,
                    RemainingTime = 1000,
                });
                _em.SetComponentData(effect, new CPeriodRuntime
                {
                    StartTime = 90,
                });
                _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect { GameplayEffect = effect });

                RunEffectGroup();

                var requestEntity = FindSingleApplyRequest(periodEffectCode);
                AssertDerivedRequest(requestEntity, context, expectedCauser: effect, periodEffectCode);

                RunCommandGroup();

                Assert.That(CountApplyRequests(periodEffectCode), Is.EqualTo(0));
                var periodEffect = FindSingleEffectByCode(periodEffectCode);
                var periodContext = _em.GetComponentData<CEffectContext>(periodEffect);
                var periodSpec = _em.GetComponentData<CEffectSpecData>(periodEffect);

                Assert.That(periodContext.SourceAsc, Is.EqualTo(source));
                Assert.That(periodContext.TargetAsc, Is.EqualTo(target));
                Assert.That(periodContext.SourceAbility, Is.EqualTo(ability));
                Assert.That(periodContext.SourceEffect, Is.EqualTo(effect));
                Assert.That(periodContext.Instigator, Is.EqualTo(source));
                Assert.That(periodContext.Causer, Is.EqualTo(effect));
                Assert.That(periodContext.ParentContextId, Is.EqualTo(context.ContextId));
                Assert.That(periodContext.ContextId, Is.Not.EqualTo(context.ContextId));
                Assert.That(periodSpec.GameplayEffectCode, Is.EqualTo(periodEffectCode));
                Assert.That(periodSpec.Level, Is.EqualTo(7));
                Assert.That(periodSpec.StackCount, Is.EqualTo(1));
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
                RestoreFrame(previousTimer);
            }
        }

        [Test]
        public void OverflowEffectWithoutStaticDefinitionDoesNotCreateApplyRequest()
        {
            const int overflowEffectCode = 93002;
            var fixture = CreateStackingFixture(denyOverflowApplication: false, clearStackOnOverflow: false);
            _em.AddBuffer<BOverflowGEConfig>(fixture.Incoming).Add(new BOverflowGEConfig
            {
                GameplayEffectCode = overflowEffectCode,
            });
            _em.AddBuffer<BSetByCallerValue>(fixture.Incoming).Add(new BSetByCallerValue
            {
                Key = 8,
                Value = 88,
            });

            var merged = EffectRuntimeUtility.TryMergeStackingApplication(
                _em,
                fixture.Incoming,
                fixture.Context,
                ref fixture.IncomingDuration,
                currentFrame: 100);

            Assert.That(merged, Is.True);
            Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, fixture.Incoming, out _), Is.False);
            Assert.That(CountApplyRequests(overflowEffectCode), Is.EqualTo(0));
        }

        [Test]
        public void OverflowEffectReadsEffectCodesFromStaticDefinitionBlob()
        {
            const int sourceEffectCode = 93031;
            const int overflowEffectCode = 93032;
            var fixture = CreateStackingFixture(denyOverflowApplication: false, clearStackOnOverflow: false);

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == sourceEffectCode
                    ? CreateOverflowDefinitionConfig(fixture.StackingCode, overflowEffectCode)
                    : null);

            try
            {
                var incoming = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(_em, sourceEffectCode);
                _entities.Add(incoming);
                Assert.That(incoming, Is.Not.EqualTo(Entity.Null));
                Assert.That(_em.HasComponent<CGameplayEffectPrototype>(incoming), Is.False);
                Assert.That(_em.HasBuffer<BOverflowGEConfig>(incoming), Is.True);

                _em.RemoveComponent<BOverflowGEConfig>(incoming);
                Assert.That(_em.HasBuffer<BOverflowGEConfig>(incoming), Is.False);

                _em.AddComponentData(incoming, new CEffectSpecData
                {
                    GameplayEffectCode = sourceEffectCode,
                    Level = 1,
                    StackCount = 1,
                });
                _em.AddBuffer<BSetByCallerValue>(incoming).Add(new BSetByCallerValue
                {
                    Key = 10,
                    Value = 100,
                });

                Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, incoming, out var blob), Is.True);
                Assert.That(blob.Value.OverflowEffectCodes.Length, Is.EqualTo(1));
                Assert.That(blob.Value.OverflowEffectCodes[0], Is.EqualTo(overflowEffectCode));

                var merged = EffectRuntimeUtility.TryMergeStackingApplication(
                    _em,
                    incoming,
                    fixture.Context,
                    ref fixture.IncomingDuration,
                    currentFrame: 100);

                Assert.That(merged, Is.True);

                var requestEntity = FindSingleApplyRequest(overflowEffectCode);
                AssertDerivedRequest(requestEntity, fixture.Context, expectedCauser: incoming, overflowEffectCode);

                var setByCallers = _em.GetBuffer<BSetByCallerValue>(requestEntity);
                Assert.That(setByCallers.Length, Is.EqualTo(1));
                Assert.That(setByCallers[0].Key, Is.EqualTo(10));
                Assert.That(setByCallers[0].Value, Is.EqualTo(100));
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
            }
        }

        [Test]
        public void MissingOverflowDerivedGameplayEffectConfigIsDiscardedWhenRequestIsConsumed()
        {
            const int sourceEffectCode = 93051;
            const int missingOverflowEffectCode = 93052;
            var fixture = CreateStackingFixture(denyOverflowApplication: false, clearStackOnOverflow: false);

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == sourceEffectCode
                    ? CreateOverflowDefinitionConfig(fixture.StackingCode, missingOverflowEffectCode)
                    : null);

            try
            {
                var incoming = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(_em, sourceEffectCode);
                _entities.Add(incoming);
                Assert.That(incoming, Is.Not.EqualTo(Entity.Null));
                _em.AddComponentData(incoming, new CEffectSpecData
                {
                    GameplayEffectCode = sourceEffectCode,
                    Level = 1,
                    StackCount = 1,
                });

                var merged = EffectRuntimeUtility.TryMergeStackingApplication(
                    _em,
                    incoming,
                    fixture.Context,
                    ref fixture.IncomingDuration,
                    currentFrame: 100);

                Assert.That(merged, Is.True);
                var requestEntity = FindSingleApplyRequest(missingOverflowEffectCode);
                AssertDerivedRequest(
                    requestEntity,
                    fixture.Context,
                    expectedCauser: incoming,
                    missingOverflowEffectCode);

                RunCommandGroup();

                Assert.That(CountApplyRequests(missingOverflowEffectCode), Is.EqualTo(0));
                Assert.That(CountEffectsByCode(missingOverflowEffectCode), Is.EqualTo(0));
            }
            finally
            {
                GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
            }
        }

        private StackingFixture CreateStackMergeFixture()
        {
            const int stackingCode = 90001;
            var source = CreateEntity();
            var target = CreateEntity();
            _em.AddBuffer<BGameplayEffect>(target);

            var attributes = _em.AddBuffer<BAttribute>(target);
            attributes.Add(new BAttribute
            {
                AttrSetCode = 1101,
                Code = 2201,
                BaseValue = 100,
                CurrentValue = 110,
                Dirty = false,
            });

            var activeModifiers = _em.AddBuffer<BActiveModifier>(target);

            var existing = CreateEntity();
            var context = new CEffectContext
            {
                SourceAsc = source,
                TargetAsc = target,
                ContextId = 66,
            };

            _em.AddComponentData(existing, context);
            _em.AddComponentData(existing, new CEffectLifecycle
            {
                State = EGameplayEffectLifecycleState.Active,
                PreviousState = EGameplayEffectLifecycleState.Active,
                StateStartFrame = 1,
            });
            AddStacking(existing,
                stackingCode,
                denyOverflowApplication: false,
                clearStackOnOverflow: false,
                stackCount: 1,
                limitCount: 3);
            _em.AddComponentData(existing, new CEffectSpecData
            {
                GameplayEffectCode = 7001,
                Level = 1,
                StackCount = 1,
            });
            AddDuration(existing);

            var modifierConfigs = _em.AddBuffer<BModifierConfig>(existing);
            modifierConfigs.Add(new BModifierConfig
            {
                AttrSetCode = 1101,
                AttributeCode = 2201,
                Op = EModifierOp.Add,
                Magnitude = 25,
            });
            _em.AddBuffer<BMagnitudeDefinition>(existing).Add(new BMagnitudeDefinition
            {
                ModifierIndex = 0,
                Source = EMagnitudeSource.StackCount,
                Coefficient = 10,
            });

            activeModifiers.Add(new BActiveModifier
            {
                AttrSetCode = 1101,
                AttributeCode = 2201,
                SourceEntity = existing,
                Op = EModifierOp.Add,
                Magnitude = 10,
            });

            _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect { GameplayEffect = existing });

            var incoming = CreateEntity();
            AddStacking(incoming,
                stackingCode,
                denyOverflowApplication: false,
                clearStackOnOverflow: false,
                stackCount: 1,
                limitCount: 3);

            return new StackingFixture
            {
                Source = source,
                Target = target,
                Existing = existing,
                Incoming = incoming,
                Context = context,
                IncomingDuration = CreateIncomingDuration(),
                StackingCode = stackingCode,
            };
        }

        private StackingFixture CreateSourceScopeFixture(EffectStackType stackType)
        {
            const int stackingCode = 90501;
            var sourceA = CreateEntity();
            var sourceB = CreateEntity();
            var target = CreateEntity();
            _em.AddBuffer<BGameplayEffect>(target);

            var existing = CreateEntity();
            _em.AddComponentData(existing, new CEffectContext
            {
                SourceAsc = sourceA,
                TargetAsc = target,
                ContextId = 90,
            });
            _em.AddComponentData(existing, new CEffectLifecycle
            {
                State = EGameplayEffectLifecycleState.Active,
                PreviousState = EGameplayEffectLifecycleState.Active,
                StateStartFrame = 1,
            });
            AddStacking(existing,
                stackingCode,
                denyOverflowApplication: false,
                clearStackOnOverflow: false,
                stackCount: 1,
                limitCount: 3,
                stackType: stackType);
            AddDuration(existing);
            _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect { GameplayEffect = existing });

            var incoming = CreateEntity();
            AddStacking(incoming,
                stackingCode,
                denyOverflowApplication: false,
                clearStackOnOverflow: false,
                stackCount: 0,
                limitCount: 3,
                stackType: stackType);

            return new StackingFixture
            {
                Source = sourceB,
                Target = target,
                Existing = existing,
                Incoming = incoming,
                Context = new CEffectContext
                {
                    SourceAsc = sourceB,
                    TargetAsc = target,
                    ContextId = 91,
                },
                IncomingDuration = CreateIncomingDuration(),
                StackingCode = stackingCode,
            };
        }

        private StackingFixture CreateStackingFixture(bool denyOverflowApplication, bool clearStackOnOverflow)
        {
            const int stackingCode = 91001;
            var source = CreateEntity();
            var target = CreateEntity();
            _em.AddBuffer<BGameplayEffect>(target);

            var existing = CreateEntity();
            var context = new CEffectContext
            {
                SourceAsc = source,
                TargetAsc = target,
                ContextId = 77,
            };

            _em.AddComponentData(existing, context);
            _em.AddComponentData(existing, new CEffectLifecycle
            {
                State = EGameplayEffectLifecycleState.Active,
                PreviousState = EGameplayEffectLifecycleState.Active,
                StateStartFrame = 1,
            });
            AddStacking(existing,
                stackingCode,
                denyOverflowApplication: false,
                clearStackOnOverflow: false,
                stackCount: 2);
            AddDuration(existing);
            _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect { GameplayEffect = existing });

            var incoming = CreateEntity();
            AddStacking(incoming,
                stackingCode,
                denyOverflowApplication,
                clearStackOnOverflow,
                stackCount: 1);

            return new StackingFixture
            {
                Source = source,
                Target = target,
                Existing = existing,
                Incoming = incoming,
                Context = context,
                IncomingDuration = CreateIncomingDuration(),
                StackingCode = stackingCode,
            };
        }

        private StackingFixture CreateDurationExpirationFixture(EffectExpirationPolicy expirationPolicy, int stackCount)
        {
            const int stackingCode = 92001;
            var source = CreateEntity();
            var target = CreateEntity();
            _em.AddBuffer<BGameplayEffect>(target);

            var existing = CreateEntity();
            var context = new CEffectContext
            {
                SourceAsc = source,
                TargetAsc = target,
                ContextId = 88,
            };

            _em.AddComponentData(existing, context);
            _em.AddComponentData(existing, new CEffectLifecycle
            {
                State = EGameplayEffectLifecycleState.Active,
                PreviousState = EGameplayEffectLifecycleState.Active,
                StateStartFrame = 1,
            });
            AddStacking(existing,
                stackingCode,
                denyOverflowApplication: false,
                clearStackOnOverflow: false,
                stackCount: stackCount,
                expirationPolicy: expirationPolicy,
                limitCount: 3);
            AddDuration(existing, remainingTime: 0);
            _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect { GameplayEffect = existing });

            return new StackingFixture
            {
                Source = source,
                Target = target,
                Existing = existing,
                Context = context,
                StackingCode = stackingCode,
            };
        }

        private void AddDuration(
            Entity entity,
            int duration = 30,
            bool active = true,
            int activeTime = 10,
            int remainingTime = 30,
            bool stopTickWhenDeactivated = false)
        {
            _em.AddComponentData(entity, new CDurationDefinition
            {
                Duration = duration,
                TimeUnit = TimeUnit.Frame,
                StopTickWhenDeactivated = stopTickWhenDeactivated,
            });
            _em.AddComponentData(entity, new CDurationRuntime
            {
                ResolvedDuration = duration,
                ResolvedTimeUnit = TimeUnit.Frame,
                Active = active,
                ActiveTime = activeTime,
                LastActiveTime = activeTime,
                RemainingTime = remainingTime,
            });
        }

        private static CDurationRuntime CreateIncomingDuration(int duration = 30)
        {
            return new CDurationRuntime
            {
                ResolvedDuration = duration,
                ResolvedTimeUnit = TimeUnit.Frame,
            };
        }

        private void AddStacking(
            Entity entity,
            int stackingCode,
            bool denyOverflowApplication,
            bool clearStackOnOverflow,
            int stackCount,
            EffectExpirationPolicy expirationPolicy = EffectExpirationPolicy.ClearEntireStack,
            int limitCount = 2,
            EffectStackType stackType = EffectStackType.AggregateByTarget)
        {
            _em.AddComponentData(entity, new CStackingDefinition
            {
                StackType = stackType,
                StackingCode = stackingCode,
                LimitCount = limitCount,
                EffectDurationRefreshPolicy = EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication,
                EffectPeriodResetPolicy = EffectPeriodResetPolicy.NeverRefresh,
                EffectExpirationPolicy = expirationPolicy,
                DenyOverflowApplication = denyOverflowApplication,
                ClearStackOnOverflow = clearStackOnOverflow,
            });
            _em.AddComponentData(entity, new CStackingRuntime
            {
                StackCount = stackCount,
            });
        }

        private static GameplayEffectConfig CreatePeriodDefinitionConfig(int periodEffectCode)
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfDuration
                {
                    duration = 1000,
                    timeUnit = TimeUnit.Frame,
                },
                new ConfPeriod
                {
                    Period = 5,
                    GameplayEffectCodes = new[] { periodEffectCode },
                },
            });
        }

        private static GameplayEffectConfig CreateOverflowDefinitionConfig(int stackingCode, int overflowEffectCode)
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfStacking
                {
                    StackType = EffectStackType.AggregateByTarget,
                    StackingCode = stackingCode,
                    LimitCount = 2,
                    EffectDurationRefreshPolicy = EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication,
                    EffectPeriodResetPolicy = EffectPeriodResetPolicy.NeverRefresh,
                    EffectExpirationPolicy = EffectExpirationPolicy.ClearEntireStack,
                    OverflowEffectCodes = new[] { overflowEffectCode },
                },
            });
        }

        private Entity CreateEntity()
        {
            var entity = _em.CreateEntity();
            _entities.Add(entity);
            return entity;
        }

        private void ClearGameplayEvents()
        {
            if (GASManager.EntityEventBus == Entity.Null
                || !_em.Exists(GASManager.EntityEventBus)
                || !_em.HasBuffer<BGameplayEvent>(GASManager.EntityEventBus))
            {
                return;
            }

            _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus).Clear();
        }

        private void ClearApplyRequests()
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CApplyGameplayEffectRequest>());
            using var requests = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < requests.Length; i++)
            {
                if (_em.Exists(requests[i]))
                    _em.DestroyEntity(requests[i]);
            }
        }

        private GlobalTimer SetFrame(int frame)
        {
            var previousTimer = _em.GetComponentData<GlobalTimer>(GASManager.EntityGlobalTimer);
            _em.SetComponentData(GASManager.EntityGlobalTimer, new GlobalTimer
            {
                Frame = frame,
                Turn = previousTimer.Turn,
            });
            return previousTimer;
        }

        private void RestoreFrame(GlobalTimer timer)
        {
            _em.SetComponentData(GASManager.EntityGlobalTimer, timer);
        }

        private void RunEffectGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASEffectGroup>().Update();
        }

        private void RunCommandGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>().Update();
        }

        private Entity FindSingleApplyRequest(int gameplayEffectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CApplyGameplayEffectRequest>());
            using var requests = query.ToEntityArray(Allocator.Temp);
            var match = Entity.Null;
            var count = 0;

            for (var i = 0; i < requests.Length; i++)
            {
                var request = _em.GetComponentData<CApplyGameplayEffectRequest>(requests[i]);
                if (request.GameplayEffectCode != gameplayEffectCode)
                    continue;

                match = requests[i];
                count++;
            }

            Assert.That(count, Is.EqualTo(1));
            return match;
        }

        private int CountApplyRequests(int gameplayEffectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CApplyGameplayEffectRequest>());
            using var requests = query.ToEntityArray(Allocator.Temp);
            var count = 0;

            for (var i = 0; i < requests.Length; i++)
            {
                var request = _em.GetComponentData<CApplyGameplayEffectRequest>(requests[i]);
                if (request.GameplayEffectCode == gameplayEffectCode)
                    count++;
            }

            return count;
        }

        private int CountEffectsByCode(int gameplayEffectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CEffectSpecData>());
            using var effects = query.ToEntityArray(Allocator.Temp);
            var count = 0;

            for (var i = 0; i < effects.Length; i++)
            {
                var spec = _em.GetComponentData<CEffectSpecData>(effects[i]);
                if (spec.GameplayEffectCode == gameplayEffectCode)
                    count++;
            }

            return count;
        }

        private Entity FindSingleEffectByCode(int gameplayEffectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CEffectSpecData>());
            using var effects = query.ToEntityArray(Allocator.Temp);
            var match = Entity.Null;
            var count = 0;

            for (var i = 0; i < effects.Length; i++)
            {
                var spec = _em.GetComponentData<CEffectSpecData>(effects[i]);
                if (spec.GameplayEffectCode != gameplayEffectCode)
                    continue;

                match = effects[i];
                count++;
            }

            Assert.That(count, Is.EqualTo(1));
            return match;
        }

        private void AssertDerivedRequest(
            Entity requestEntity,
            in CEffectContext sourceContext,
            Entity expectedCauser,
            int gameplayEffectCode)
        {
            var request = _em.GetComponentData<CApplyGameplayEffectRequest>(requestEntity);
            Assert.That(request.SourceAsc, Is.EqualTo(sourceContext.SourceAsc));
            Assert.That(request.SourceAbility, Is.EqualTo(sourceContext.SourceAbility));
            Assert.That(request.SourceEffect, Is.EqualTo(expectedCauser));
            Assert.That(request.Instigator, Is.EqualTo(sourceContext.Instigator));
            Assert.That(request.Causer, Is.EqualTo(expectedCauser));
            Assert.That(request.GameplayEffectCode, Is.EqualTo(gameplayEffectCode));
            Assert.That(request.ParentContextId, Is.EqualTo(sourceContext.ContextId));
            Assert.That(request.Level, Is.EqualTo(_em.GetComponentData<CEffectSpecData>(expectedCauser).Level));

            var header = _em.GetComponentData<CTargetDataHeader>(requestEntity);
            Assert.That(header.SourceAsc, Is.EqualTo(sourceContext.SourceAsc));
            Assert.That(header.SourceAbility, Is.EqualTo(sourceContext.SourceAbility));
            Assert.That(header.Kind, Is.EqualTo(ETargetDataKind.Entity));

            var targets = _em.GetBuffer<BTargetEntity>(requestEntity);
            Assert.That(targets.Length, Is.EqualTo(1));
            Assert.That(targets[0].TargetAsc, Is.EqualTo(sourceContext.TargetAsc));
        }

        private void AssertHasStackEvent(
            EGameplayEventType type,
            Entity effect,
            int stackingCode,
            float stackCount)
        {
            AssertHasGameplayEvent(type, effect, stackingCode, stackCount);
        }

        private void AssertHasGameplayEvent(
            EGameplayEventType type,
            Entity effect,
            int eventCode,
            float value)
        {
            var events = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
            for (var i = 0; i < events.Length; i++)
            {
                var gameplayEvent = events[i];
                if (gameplayEvent.Type == type
                    && gameplayEvent.GameplayEffect == effect
                    && gameplayEvent.EventCode == eventCode
                    && gameplayEvent.Value == value)
                {
                    return;
                }
            }

            Assert.Fail($"Expected gameplay event {type} for code {eventCode} and value {value}.");
        }

        private void AssertDoesNotHaveStackEvent(EGameplayEventType type)
        {
            AssertDoesNotHaveGameplayEvent(type);
        }

        private void AssertDoesNotHaveGameplayEvent(EGameplayEventType type)
        {
            var events = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
            for (var i = 0; i < events.Length; i++)
            {
                if (events[i].Type == type)
                    Assert.Fail($"Did not expect gameplay event {type}.");
            }
        }

        private struct StackingFixture
        {
            public Entity Source;
            public Entity Target;
            public Entity Existing;
            public Entity Incoming;
            public CEffectContext Context;
            public CDurationRuntime IncomingDuration;
            public int StackingCode;
        }
    }
}
