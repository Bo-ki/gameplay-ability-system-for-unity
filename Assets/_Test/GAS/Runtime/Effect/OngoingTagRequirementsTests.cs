using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.Runtime.Tests
{
    public sealed class OngoingTagRequirementsTests
    {
        private const int RequiredTagIndex = 3;
        private const int GrantedTagIndex = 4;
        private const int AttrSetCode = 1201;
        private const int AttributeCode = 2301;

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
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
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
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
            ClearGameplayEvents();
        }

        [Test]
        public void OngoingRequirementFailureInhibitsAppliedEffectWithoutRemovingIt()
        {
            var fixture = CreateActiveOngoingFixture();
            RemoveTag(fixture.Target, RequiredTagIndex);

            UpdateEffectGroup(frame: 20);

            Assert.That(_em.Exists(fixture.Effect), Is.True);
            Assert.That(TargetContainsEffect(fixture.Target, fixture.Effect), Is.True);
            Assert.That(HasActiveModifierFrom(fixture.Target, fixture.Effect), Is.False);
            Assert.That(HasTag(fixture.Target, GrantedTagIndex), Is.False);
            Assert.That(HasTempTagSource(fixture.Target, fixture.Effect, GrantedTagIndex), Is.False);
            Assert.That(_em.GetBuffer<BAttribute>(fixture.Target)[0].Dirty, Is.True);

            var duration = _em.GetComponentData<CDurationRuntime>(fixture.Effect);
            Assert.That(duration.Active, Is.False);
            Assert.That(duration.RemainingTime, Is.EqualTo(90));
            Assert.That(duration.LastActiveTime, Is.EqualTo(20));

            var lifecycle = _em.GetComponentData<CEffectLifecycle>(fixture.Effect);
            Assert.That(lifecycle.State, Is.EqualTo(EGameplayEffectLifecycleState.Inhibited));
            Assert.That(lifecycle.PreviousState, Is.EqualTo(EGameplayEffectLifecycleState.Active));
            Assert.That(lifecycle.StateStartFrame, Is.EqualTo(20));
            AssertHasLifecycleEvent(
                EGameplayEventType.GameplayEffectInhibited,
                fixture.Target,
                fixture.Effect,
                EGameplayEffectLifecycleState.Active,
                EGameplayEffectLifecycleState.Inhibited);
        }

        [Test]
        public void OngoingRequirementRecoveryReactivatesRuntimeContributions()
        {
            var fixture = CreateActiveOngoingFixture();
            RemoveTag(fixture.Target, RequiredTagIndex);
            UpdateEffectGroup(frame: 20);

            AddTag(fixture.Target, RequiredTagIndex);
            UpdateEffectGroup(frame: 35);

            Assert.That(TargetContainsEffect(fixture.Target, fixture.Effect), Is.True);
            Assert.That(HasActiveModifierFrom(fixture.Target, fixture.Effect), Is.True);
            Assert.That(HasTag(fixture.Target, GrantedTagIndex), Is.True);
            Assert.That(HasTempTagSource(fixture.Target, fixture.Effect, GrantedTagIndex), Is.True);

            var modifier = GetActiveModifierFrom(fixture.Target, fixture.Effect);
            Assert.That(modifier.AttrSetCode, Is.EqualTo(AttrSetCode));
            Assert.That(modifier.AttributeCode, Is.EqualTo(AttributeCode));
            Assert.That(modifier.Op, Is.EqualTo(EModifierOp.Add));
            Assert.That(modifier.Magnitude, Is.EqualTo(15f));

            var duration = _em.GetComponentData<CDurationRuntime>(fixture.Effect);
            Assert.That(duration.Active, Is.True);
            Assert.That(duration.RemainingTime, Is.EqualTo(90));
            Assert.That(duration.ActiveTime, Is.EqualTo(25));

            var lifecycle = _em.GetComponentData<CEffectLifecycle>(fixture.Effect);
            Assert.That(lifecycle.State, Is.EqualTo(EGameplayEffectLifecycleState.Active));
            Assert.That(lifecycle.PreviousState, Is.EqualTo(EGameplayEffectLifecycleState.Inhibited));
            Assert.That(lifecycle.StateStartFrame, Is.EqualTo(35));
            AssertHasLifecycleEvent(
                EGameplayEventType.GameplayEffectReactivated,
                fixture.Target,
                fixture.Effect,
                EGameplayEffectLifecycleState.Inhibited,
                EGameplayEffectLifecycleState.Active);
        }

        [Test]
        public void InactiveInitialApplyEmitsInhibitedLifecycleEvent()
        {
            var source = CreateEntity();
            var target = CreateEntity();
            _em.AddBuffer<BGameplayEffect>(target);

            var effect = CreateEntity();
            var context = new CEffectContext
            {
                SourceAsc = source,
                TargetAsc = target,
                ContextId = 2,
                TargetDataKind = ETargetDataKind.Entity,
            };
            var duration = new CDurationRuntime
            {
                ResolvedDuration = 60,
                ResolvedTimeUnit = TimeUnit.Frame,
            };

            EffectRuntimeUtility.ApplyInactiveDurationEffect(_em, effect, context, ref duration, currentFrame: 12);

            Assert.That(TargetContainsEffect(target, effect), Is.True);
            Assert.That(duration.Active, Is.False);
            Assert.That(duration.ActiveTime, Is.EqualTo(12));
            Assert.That(duration.LastActiveTime, Is.EqualTo(12));
            Assert.That(duration.RemainingTime, Is.EqualTo(60));

            var lifecycle = _em.GetComponentData<CEffectLifecycle>(effect);
            Assert.That(lifecycle.State, Is.EqualTo(EGameplayEffectLifecycleState.Inhibited));
            Assert.That(lifecycle.PreviousState, Is.EqualTo(EGameplayEffectLifecycleState.Inhibited));
            Assert.That(lifecycle.StateStartFrame, Is.EqualTo(12));
            AssertHasLifecycleEvent(
                EGameplayEventType.GameplayEffectInhibited,
                target,
                effect,
                EGameplayEffectLifecycleState.PendingApply,
                EGameplayEffectLifecycleState.Inhibited);
        }

        [Test]
        public void OngoingRequirementReadsRequirementFromStaticDefinitionBlob()
        {
            const int effectCode = 94001;
            var source = CreateEntity();
            var target = CreateEntity();

            var targetTags = new CTagMask();
            targetTags.AddTag(RequiredTagIndex);
            _em.AddComponentData(target, targetTags);
            _em.AddBuffer<BGameplayEffect>(target);

            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateOngoingDefinitionConfig() : null);

            var effect = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(_em, effectCode);
            _entities.Add(effect);
            Assert.That(effect, Is.Not.EqualTo(Entity.Null));
            Assert.That(_em.HasComponent<COngoingRequiredTags>(effect), Is.True);

            _em.RemoveComponent<COngoingRequiredTags>(effect);
            Assert.That(_em.HasComponent<COngoingRequiredTags>(effect), Is.False);

            var context = new CEffectContext
            {
                SourceAsc = source,
                TargetAsc = target,
                ContextId = 3,
                TargetDataKind = ETargetDataKind.Entity,
            };
            _em.AddComponentData(effect, context);
            _em.AddComponentData(effect, new CEffectSpecData
            {
                GameplayEffectCode = effectCode,
                Level = 1,
                StackCount = 1,
            });
            _em.AddComponentData(effect, new CEffectLifecycle
            {
                State = EGameplayEffectLifecycleState.Active,
                PreviousState = EGameplayEffectLifecycleState.PendingApply,
                StateStartFrame = 10,
            });
            var duration = _em.GetComponentData<CDurationRuntime>(effect);
            duration.Active = true;
            duration.ActiveTime = 10;
            duration.LastActiveTime = 10;
            duration.RemainingTime = duration.ResolvedDuration;
            _em.SetComponentData(effect, duration);
            _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect { GameplayEffect = effect });

            Assert.That(EffectRuntimeUtility.HasOngoingRequirements(_em, effect), Is.True);
            Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, effect, out var blob), Is.True);
            Assert.That(blob.Value.HasOngoingRequiredTags, Is.True);

            RemoveTag(target, RequiredTagIndex);
            UpdateEffectGroup(frame: 25);

            duration = _em.GetComponentData<CDurationRuntime>(effect);
            Assert.That(duration.Active, Is.False);
            Assert.That(duration.RemainingTime, Is.EqualTo(85));
            Assert.That(duration.LastActiveTime, Is.EqualTo(25));

            var lifecycle = _em.GetComponentData<CEffectLifecycle>(effect);
            Assert.That(lifecycle.State, Is.EqualTo(EGameplayEffectLifecycleState.Inhibited));
            Assert.That(lifecycle.PreviousState, Is.EqualTo(EGameplayEffectLifecycleState.Active));
            Assert.That(lifecycle.StateStartFrame, Is.EqualTo(25));
        }

        [Test]
        public void DurationEffectWithoutOngoingRequirementIsIgnoredByOngoingSystem()
        {
            var source = CreateEntity();
            var target = CreateEntity();
            _em.AddBuffer<BGameplayEffect>(target);

            var effect = CreateEntity();
            _em.AddComponentData(effect, new CEffectContext
            {
                SourceAsc = source,
                TargetAsc = target,
                ContextId = 4,
                TargetDataKind = ETargetDataKind.Entity,
            });
            AddDuration(effect, duration: 60, active: false, activeTime: 10, remainingTime: 60);
            _em.AddComponentData(effect, new CEffectLifecycle
            {
                State = EGameplayEffectLifecycleState.Inhibited,
                PreviousState = EGameplayEffectLifecycleState.PendingApply,
                StateStartFrame = 10,
            });
            _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect { GameplayEffect = effect });

            UpdateEffectGroup(frame: 30);

            var duration = _em.GetComponentData<CDurationRuntime>(effect);
            Assert.That(duration.Active, Is.False);
            Assert.That(duration.ActiveTime, Is.EqualTo(10));

            var lifecycle = _em.GetComponentData<CEffectLifecycle>(effect);
            Assert.That(lifecycle.State, Is.EqualTo(EGameplayEffectLifecycleState.Inhibited));
            Assert.That(lifecycle.StateStartFrame, Is.EqualTo(10));
        }

        private OngoingFixture CreateActiveOngoingFixture()
        {
            const int effectCode = 94002;
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode ? CreateActiveOngoingDefinitionConfig() : null);

            var source = CreateEntity();
            var target = CreateEntity();

            var targetTags = new CTagMask();
            targetTags.AddTag(RequiredTagIndex);
            targetTags.AddTag(GrantedTagIndex);
            _em.AddComponentData(target, targetTags);

            _em.AddBuffer<BGameplayEffect>(target);
            _em.AddBuffer<BActiveModifier>(target);
            _em.AddBuffer<BTempTagSource>(target);

            _em.AddBuffer<BAttribute>(target).Add(new BAttribute
            {
                AttrSetCode = AttrSetCode,
                Code = AttributeCode,
                BaseValue = 100f,
                CurrentValue = 100f,
            });

            var effect = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(_em, effectCode);
            _entities.Add(effect);
            Assert.That(effect, Is.Not.EqualTo(Entity.Null));
            _em.AddComponentData(effect, new CEffectContext
            {
                SourceAsc = source,
                TargetAsc = target,
                ContextId = 1,
                TargetDataKind = ETargetDataKind.Entity,
            });
            _em.AddComponentData(effect, new CEffectSpecData
            {
                GameplayEffectCode = effectCode,
                Level = 1,
                StackCount = 1,
            });
            _em.AddComponentData(effect, new CEffectLifecycle
            {
                State = EGameplayEffectLifecycleState.Active,
                PreviousState = EGameplayEffectLifecycleState.PendingApply,
                StateStartFrame = 10,
            });
            _em.SetComponentData(effect, new CDurationRuntime
            {
                ResolvedDuration = 100,
                ResolvedTimeUnit = TimeUnit.Frame,
                Active = true,
                ActiveTime = 10,
                LastActiveTime = 10,
                RemainingTime = 100,
            });
            _em.AddBuffer<BResolvedModifier>(effect).Add(new BResolvedModifier
            {
                AttrSetCode = AttrSetCode,
                AttributeCode = AttributeCode,
                Op = EModifierOp.Add,
                Magnitude = 15f,
                SourceEffect = effect,
            });
            Assert.That(EffectRuntimeUtility.TryGetStaticDefinitionBlob(_em, effect, out var blob), Is.True);
            Assert.That(blob.Value.HasOngoingRequiredTags, Is.True);
            Assert.That(blob.Value.GrantedTags.HasTag(GrantedTagIndex), Is.True);

            _em.GetBuffer<BActiveModifier>(target).Add(new BActiveModifier
            {
                AttrSetCode = AttrSetCode,
                AttributeCode = AttributeCode,
                Op = EModifierOp.Add,
                Magnitude = 15f,
                SourceEntity = effect,
            });
            _em.GetBuffer<BGameplayEffect>(target).Add(new BGameplayEffect { GameplayEffect = effect });
            _em.GetBuffer<BTempTagSource>(target).Clear();
            _em.GetBuffer<BTempTagSource>(target).Add(new BTempTagSource
            {
                TagIndex = GrantedTagIndex,
                Source = effect,
            });

            return new OngoingFixture
            {
                Target = target,
                Effect = effect,
            };
        }

        private GameplayEffectConfig CreateActiveOngoingDefinitionConfig()
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfDuration
                {
                    duration = 100,
                    timeUnit = TimeUnit.Frame,
                    StopTickWhenDeactivated = true,
                },
                new OngoingRequirementDefinitionConfig
                {
                    Requirement = new TagRequirementMask
                    {
                        All = CreateMask(RequiredTagIndex),
                    },
                },
                new GrantedTagDefinitionConfig
                {
                    Tags = CreateMask(GrantedTagIndex),
                },
            });
        }

        private GameplayEffectConfig CreateOngoingDefinitionConfig()
        {
            return new GameplayEffectConfig(new GameplayEffectComponentConfig[]
            {
                new ConfDuration
                {
                    duration = 100,
                    timeUnit = TimeUnit.Frame,
                    StopTickWhenDeactivated = true,
                },
                new OngoingRequirementDefinitionConfig
                {
                    Requirement = new TagRequirementMask
                    {
                        All = CreateMask(RequiredTagIndex),
                    },
                },
            });
        }

        private Entity CreateEntity()
        {
            var entity = _em.CreateEntity();
            _entities.Add(entity);
            return entity;
        }

        private void AddDuration(
            Entity entity,
            int duration = 100,
            bool active = true,
            int activeTime = 10,
            int remainingTime = 100,
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

        private void UpdateEffectGroup(int frame)
        {
            _em.SetComponentData(GASManager.EntityGlobalTimer, new GlobalTimer
            {
                Frame = frame,
            });
            GASManager.ExWorld.GetExistingSystemManaged<GASEffectGroup>().Update();
        }

        private static CTagMask CreateMask(int tagIndex)
        {
            var mask = new CTagMask();
            mask.AddTag(tagIndex);
            return mask;
        }

        private void AddTag(Entity target, int tagIndex)
        {
            var mask = _em.GetComponentData<CTagMask>(target);
            mask.AddTag(tagIndex);
            _em.SetComponentData(target, mask);
        }

        private void RemoveTag(Entity target, int tagIndex)
        {
            var mask = _em.GetComponentData<CTagMask>(target);
            mask.RemoveTag(tagIndex);
            _em.SetComponentData(target, mask);
        }

        private bool HasTag(Entity target, int tagIndex)
        {
            return _em.GetComponentData<CTagMask>(target).HasTag(tagIndex);
        }

        private bool TargetContainsEffect(Entity target, Entity effect)
        {
            var effects = _em.GetBuffer<BGameplayEffect>(target);
            for (var i = 0; i < effects.Length; i++)
            {
                if (effects[i].GameplayEffect == effect)
                    return true;
            }

            return false;
        }

        private bool HasActiveModifierFrom(Entity target, Entity effect)
        {
            var modifiers = _em.GetBuffer<BActiveModifier>(target);
            for (var i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i].SourceEntity == effect)
                    return true;
            }

            return false;
        }

        private BActiveModifier GetActiveModifierFrom(Entity target, Entity effect)
        {
            var modifiers = _em.GetBuffer<BActiveModifier>(target);
            for (var i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i].SourceEntity == effect)
                    return modifiers[i];
            }

            Assert.Fail($"Active modifier from effect {effect} was not found.");
            return default;
        }

        private bool HasTempTagSource(Entity target, Entity source, int tagIndex)
        {
            var sources = _em.GetBuffer<BTempTagSource>(target);
            for (var i = 0; i < sources.Length; i++)
            {
                if (sources[i].Source == source && sources[i].TagIndex == tagIndex)
                    return true;
            }

            return false;
        }

        private void AssertHasLifecycleEvent(
            EGameplayEventType type,
            Entity target,
            Entity effect,
            EGameplayEffectLifecycleState previousState,
            EGameplayEffectLifecycleState state)
        {
            var events = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Type == type
                    && evt.TargetAsc == target
                    && evt.GameplayEffect == effect
                    && evt.EventCode == (int)previousState
                    && evt.Value == (int)state)
                {
                    return;
                }
            }

            Assert.Fail(
                $"Gameplay event {type} for effect {effect} from {previousState} to {state} was not found.");
        }

        private void ClearGameplayEvents()
        {
            if (!GASManager.IsInitialized
                || !GASManager.EntityManager.Exists(GASManager.EntityEventBus)
                || !_em.HasBuffer<BGameplayEvent>(GASManager.EntityEventBus))
            {
                return;
            }

            _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus).Clear();
        }

        private struct OngoingFixture
        {
            public Entity Target;
            public Entity Effect;
        }

        private sealed class OngoingRequirementDefinitionConfig : GameplayEffectComponentConfig
        {
            public TagRequirementMask Requirement;

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                _entityManager.AddComponentData(ge, new COngoingRequiredTags
                {
                    requirement = Requirement,
                });
            }
        }

        private sealed class GrantedTagDefinitionConfig : GameplayEffectComponentConfig
        {
            public CTagMask Tags;

            public override void LoadToGameplayEffectEntity(Entity ge)
            {
                _entityManager.AddComponentData(ge, new CEffectGrantedTags
                {
                    Tags = Tags,
                });
            }
        }
    }
}
