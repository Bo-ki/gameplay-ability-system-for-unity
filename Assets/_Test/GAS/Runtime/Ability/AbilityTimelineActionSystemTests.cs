using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Tests.Ability
{
    public sealed class AbilityTimelineActionSystemTests
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
            TargetCatcherHelper.RegisterTargetCatcher(nameof(CatchSelf), typeof(CatchSelf), typeof(XParamNone));
            TargetCatcherHelper.RegisterTargetCatcher(nameof(CatchTarget), typeof(CatchTarget), typeof(XParamNone));
            CueHelper.RegisterCue(nameof(RecordingCue), typeof(RecordingCue), typeof(XParamNone));
            ClearEffectCommandStream();
        }

        [TearDown]
        public void TearDown()
        {
            TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(null);
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(null);
            GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(null);
            ClearEffectCommandStream();
        }

        [Test]
        public void TimelineApplyEffectsAtStartFrameIsConsumedByCommandGroup()
        {
            const int timelineId = 71001;
            const int effectCode = 61001;

            var owner = _em.CreateEntity();
            var ability = CreateActiveTimelineAbility(owner, timelineId, level: 4);
            var effect = Entity.Null;

            try
            {
                RegisterTimeline(CreateTimeline(timelineId, lifeTime: 30, manualEnd: false,
                    CreateApplyEffectsClip(startFrame: 0, effectCode, nameof(CatchSelf))));
                RegisterEffect(effectCode);

                UpdateCommandGroup();

                effect = FindEffectByCode(effectCode);
                Assert.That(effect, Is.Not.EqualTo(Entity.Null));

                var context = _em.GetComponentData<CEffectContext>(effect);
                Assert.That(context.SourceAsc, Is.EqualTo(owner));
                Assert.That(context.TargetAsc, Is.EqualTo(owner));
                Assert.That(context.SourceAbility, Is.EqualTo(ability));
                Assert.That(context.Instigator, Is.EqualTo(owner));
                Assert.That(context.Causer, Is.EqualTo(ability));
                Assert.That(context.TargetDataKind, Is.EqualTo(ETargetDataKind.Self));
                Assert.That(_em.GetComponentData<CEffectSpecData>(effect).Level, Is.EqualTo(4));

                var timelineRuntime = _em.GetComponentData<CAbilityTimelineRuntime>(ability);
                Assert.That(timelineRuntime.TimelineId, Is.EqualTo(timelineId));
                Assert.That(timelineRuntime.LastDispatchedElapsedFrame, Is.EqualTo(0));
            }
            finally
            {
                DestroyIfExists(effect);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TimelineApplyEffectsAppliesSimpleInstantThroughEffectCommandStream()
        {
            const int timelineId = 71101;
            const int effectCode = 61101;
            const int attrSetCode = 20;
            const int attributeCode = 30;

            var owner = _em.CreateEntity();
            var target = CreateAscWithAttribute(attrSetCode, attributeCode, 100f);
            var ability = CreateActiveTimelineAbility(owner, timelineId, level: 4);

            try
            {
                _em.AddComponentData(ability, new CAbilityMainTarget { TargetAsc = target });
                RegisterTimeline(CreateTimeline(timelineId, lifeTime: 30, manualEnd: false,
                    CreateApplyEffectsClip(startFrame: 0, effectCode, nameof(CatchTarget))));
                RegisterEffect(effectCode, new ConfModifierConfig
                {
                    ModifierSettings = new[]
                    {
                        new ModifierDefinitionSetting
                        {
                            AttrSetCode = attrSetCode,
                            AttrCode = attributeCode,
                            Operation = EModifierOp.Subtract,
                            Magnitude = 18f,
                        },
                    },
                });

                UpdateCommandGroup();

                Assert.That(FindEffectByCode(effectCode), Is.EqualTo(Entity.Null));
                Assert.That(CountApplyRequestsByCode(effectCode), Is.EqualTo(0));

                var streamEntity = EffectCommandSpecStream.EnsureSingleton(_em);
                var commands = _em.GetBuffer<BEffectCommand>(streamEntity);
                var specs = _em.GetBuffer<BInstantEffectSpec>(streamEntity);
                var deltas = _em.GetBuffer<BAttributeDelta>(streamEntity);
                var facts = _em.GetBuffer<BTypedSimulationFact>(streamEntity);
                var attribute = _em.GetBuffer<BAttribute>(target)[0];

                Assert.That(commands.Length, Is.EqualTo(1));
                Assert.That(specs.Length, Is.EqualTo(1));
                Assert.That(deltas.Length, Is.EqualTo(1));
                Assert.That(facts.Length, Is.EqualTo(1));
                Assert.That(attribute.BaseValue, Is.EqualTo(82f));
                Assert.That(attribute.CurrentValue, Is.EqualTo(82f));

                Assert.That(commands[0].SourceAsc, Is.EqualTo(owner));
                Assert.That(commands[0].TargetAsc, Is.EqualTo(target));
                Assert.That(commands[0].SourceAbility, Is.EqualTo(ability));
                Assert.That(commands[0].GameplayEffectCode, Is.EqualTo(effectCode));
                Assert.That(commands[0].Level, Is.EqualTo(4));
                Assert.That(commands[0].TargetDataKind, Is.EqualTo(ETargetDataKind.Entity));
                Assert.That(specs[0].SourceCommandSequence, Is.EqualTo(commands[0].Sequence));
                Assert.That(deltas[0].SourceSpecSequence, Is.EqualTo(specs[0].Sequence));
                Assert.That(deltas[0].TargetAsc, Is.EqualTo(target));
                Assert.That(deltas[0].SourceAbility, Is.EqualTo(ability));
                Assert.That(deltas[0].OldValue, Is.EqualTo(100f));
                Assert.That(deltas[0].NewValue, Is.EqualTo(82f));
                Assert.That(facts[0].SourceDeltaSequence, Is.EqualTo(deltas[0].Sequence));
                Assert.That(facts[0].EventType, Is.EqualTo(EGameplayEventType.AttributeBaseValueChanged));

                var timelineRuntime = _em.GetComponentData<CAbilityTimelineRuntime>(ability);
                Assert.That(timelineRuntime.TimelineId, Is.EqualTo(timelineId));
                Assert.That(timelineRuntime.LastDispatchedElapsedFrame, Is.EqualTo(0));
            }
            finally
            {
                DestroyIfExists(ability);
                DestroyIfExists(target);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TimelineApplyEffectsDispatchesOnlyWhenStartFrameIsReached()
        {
            const int timelineId = 71002;
            const int effectCode = 61002;

            var owner = _em.CreateEntity();
            var ability = CreateActiveTimelineAbility(owner, timelineId, level: 1);
            var effect = Entity.Null;

            try
            {
                RegisterTimeline(CreateTimeline(timelineId, lifeTime: 30, manualEnd: false,
                    CreateApplyEffectsClip(startFrame: 2, effectCode, nameof(CatchSelf))));
                RegisterEffect(effectCode);

                UpdateCommandGroup();
                Assert.That(FindEffectByCode(effectCode), Is.EqualTo(Entity.Null));

                UpdateCommandGroup();
                Assert.That(FindEffectByCode(effectCode), Is.EqualTo(Entity.Null));

                UpdateCommandGroup();
                effect = FindEffectByCode(effectCode);
                Assert.That(effect, Is.Not.EqualTo(Entity.Null));
                Assert.That(_em.GetComponentData<CAbilityTimelineRuntime>(ability).LastDispatchedElapsedFrame, Is.EqualTo(2));
            }
            finally
            {
                DestroyIfExists(effect);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TimelineApplyEffectsUsesOptionalMainTargetForCatchTarget()
        {
            const int timelineId = 71003;
            const int effectCode = 61003;

            var owner = _em.CreateEntity();
            var target = _em.CreateEntity();
            var ability = CreateActiveTimelineAbility(owner, timelineId, level: 2);
            var effect = Entity.Null;

            try
            {
                _em.AddComponentData(ability, new CAbilityMainTarget { TargetAsc = target });
                RegisterTimeline(CreateTimeline(timelineId, lifeTime: 30, manualEnd: false,
                    CreateApplyEffectsClip(startFrame: 0, effectCode, nameof(CatchTarget))));
                RegisterEffect(effectCode);

                UpdateCommandGroup();

                effect = FindEffectByCode(effectCode);
                Assert.That(effect, Is.Not.EqualTo(Entity.Null));

                var context = _em.GetComponentData<CEffectContext>(effect);
                Assert.That(context.TargetAsc, Is.EqualTo(target));
                Assert.That(context.TargetDataKind, Is.EqualTo(ETargetDataKind.Entity));
            }
            finally
            {
                DestroyIfExists(effect);
                DestroyIfExists(ability);
                DestroyIfExists(target);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TimelineApplyCostRequestsCostGameplayEffect()
        {
            const int timelineId = 71006;
            const int effectCode = 61006;

            var owner = _em.CreateEntity();
            var ability = CreateActiveTimelineAbility(owner, timelineId, level: 5);
            var effect = Entity.Null;

            try
            {
                _em.AddComponentData(ability, new CAbilityCost
                {
                    GameplayEffectCode = effectCode,
                });
                RegisterTimeline(CreateTimeline(timelineId, lifeTime: 30, manualEnd: false,
                    CreateActionClip(startFrame: 0, "ApplyCost")));
                RegisterEffect(effectCode);

                UpdateCommandGroup();

                effect = FindEffectByCode(effectCode);
                Assert.That(effect, Is.Not.EqualTo(Entity.Null));

                var context = _em.GetComponentData<CEffectContext>(effect);
                Assert.That(context.SourceAsc, Is.EqualTo(owner));
                Assert.That(context.TargetAsc, Is.EqualTo(owner));
                Assert.That(context.SourceAbility, Is.EqualTo(ability));
                Assert.That(context.Instigator, Is.EqualTo(owner));
                Assert.That(context.Causer, Is.EqualTo(ability));
                Assert.That(context.TargetDataKind, Is.EqualTo(ETargetDataKind.Self));
                Assert.That(_em.GetComponentData<CEffectSpecData>(effect).Level, Is.EqualTo(5));
            }
            finally
            {
                DestroyIfExists(effect);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TimelineApplyCooldownRequestsCooldownGameplayEffectWithDurationOverride()
        {
            const int timelineId = 71007;
            const int effectCode = 61007;
            const int cooldownFrames = 17;

            var owner = _em.CreateEntity();
            var ability = CreateActiveTimelineAbility(owner, timelineId, level: 2);
            var effect = Entity.Null;

            try
            {
                _em.AddComponentData(ability, new CAbilityCooldown
                {
                    GameplayEffectCode = effectCode,
                    Cooldown = cooldownFrames,
                });
                RegisterTimeline(CreateTimeline(timelineId, lifeTime: 30, manualEnd: false,
                    CreateActionClip(startFrame: 0, "ApplyCooldown")));
                RegisterEffect(effectCode, new ConfDuration
                {
                    duration = 1,
                    timeUnit = TimeUnit.Frame,
                });

                UpdateCommandGroup();

                effect = FindEffectByCode(effectCode);
                Assert.That(effect, Is.Not.EqualTo(Entity.Null));

                var context = _em.GetComponentData<CEffectContext>(effect);
                Assert.That(context.SourceAsc, Is.EqualTo(owner));
                Assert.That(context.TargetAsc, Is.EqualTo(owner));
                Assert.That(context.SourceAbility, Is.EqualTo(ability));
                Assert.That(context.TargetDataKind, Is.EqualTo(ETargetDataKind.Self));

                var definition = _em.GetComponentData<CDurationDefinition>(effect);
                Assert.That(definition.Duration, Is.EqualTo(1));
                Assert.That(definition.TimeUnit, Is.EqualTo(TimeUnit.Frame));

                var runtime = _em.GetComponentData<CDurationRuntime>(effect);
                Assert.That(runtime.ResolvedDuration, Is.EqualTo(cooldownFrames));
                Assert.That(runtime.ResolvedTimeUnit, Is.EqualTo(TimeUnit.Frame));
            }
            finally
            {
                DestroyIfExists(effect);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TimelinePlayCueWritesCueRequestAndBridgePlaysWithAbilitySource()
        {
            const int timelineId = 71008;

            var owner = _em.CreateEntity();
            var ability = CreateActiveTimelineAbility(owner, timelineId, level: 1);
            var cueEntity = Entity.Null;

            try
            {
                RegisterTimeline(CreateTimeline(timelineId, lifeTime: 30, manualEnd: false,
                    CreatePlayCueClip(startFrame: 0)));

                UpdateCommandGroup();

                var request = FindCueRequest(EGameplayCueEvent.Play);
                Assert.That(request.CueEntity, Is.Not.EqualTo(Entity.Null));
                Assert.That(request.TargetAsc, Is.EqualTo(owner));
                Assert.That(request.SourceAsc, Is.EqualTo(owner));
                Assert.That(request.SourceAbility, Is.EqualTo(ability));
                Assert.That(request.SourceEntity, Is.EqualTo(ability));
                Assert.That(request.SourceType, Is.EqualTo(CueSourceType.GameplayAbility));
                Assert.That(request.GameplayEffect, Is.EqualTo(Entity.Null));

                cueEntity = request.CueEntity;
                var cue = (RecordingCue)_em.GetComponentData<MCCue>(cueEntity).cue;
                Assert.That(cue.GetSourceAbilityEntity(), Is.EqualTo(ability));
                Assert.That(cue.AddCount, Is.EqualTo(0));

                UpdateCueGroup();

                Assert.That(cue.GetSourceAbilityEntity(), Is.EqualTo(ability));
                Assert.That(cue.AddCount, Is.EqualTo(1));
                Assert.That(cue.ActivateCount, Is.EqualTo(1));
            }
            finally
            {
                DestroyIfExists(cueEntity);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TimelinePlayCuePresetWritesCueRequestFromCueRegistry()
        {
            const int timelineId = 71009;
            const int cueId = 81009;

            var owner = _em.CreateEntity();
            var ability = CreateActiveTimelineAbility(owner, timelineId, level: 1);
            var cueEntity = Entity.Null;

            try
            {
                GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(id =>
                    id == cueId
                        ? new GameplayCueConfig(typeof(RecordingCue), new XParamNone())
                        : null);
                RegisterTimeline(CreateTimeline(timelineId, lifeTime: 30, manualEnd: false,
                    CreatePlayCuePresetClip(startFrame: 0, cueId)));

                UpdateCommandGroup();

                var request = FindCueRequest(EGameplayCueEvent.Play);
                Assert.That(request.CueEntity, Is.Not.EqualTo(Entity.Null));
                Assert.That(request.SourceAbility, Is.EqualTo(ability));
                Assert.That(request.SourceType, Is.EqualTo(CueSourceType.GameplayAbility));

                cueEntity = request.CueEntity;
                var cue = (RecordingCue)_em.GetComponentData<MCCue>(cueEntity).cue;

                UpdateCueGroup();

                Assert.That(cue.AddCount, Is.EqualTo(1));
                Assert.That(cue.ActivateCount, Is.EqualTo(1));
            }
            finally
            {
                DestroyIfExists(cueEntity);
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void TimelineAutoEndsAbilityWhenLifetimeIsReached()
        {
            const int timelineId = 71004;

            var owner = _em.CreateEntity();
            var ability = CreateActiveTimelineAbility(owner, timelineId, level: 1);

            try
            {
                RegisterTimeline(CreateTimeline(timelineId, lifeTime: 1, manualEnd: false));

                UpdateCommandGroup();
                Assert.That(_em.HasComponent<CAbilityInTryEnd>(ability), Is.False);

                UpdateCommandGroup();
                Assert.That(_em.HasComponent<CAbilityInTryEnd>(ability), Is.True);
                var endRequest = _em.GetComponentData<CAbilityInTryEnd>(ability);
                Assert.That(endRequest.Reason, Is.EqualTo(EAbilityLifecycleReason.TimelineCompleted));
                Assert.That(endRequest.SourceAbility, Is.EqualTo(ability));
                Assert.That(endRequest.SourceAbilityCode, Is.EqualTo(10001));
            }
            finally
            {
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        [Test]
        public void ManualEndTimelineDoesNotAutoEndWhenLifetimeIsReached()
        {
            const int timelineId = 71005;

            var owner = _em.CreateEntity();
            var ability = CreateActiveTimelineAbility(owner, timelineId, level: 1);

            try
            {
                RegisterTimeline(CreateTimeline(timelineId, lifeTime: 1, manualEnd: true));

                UpdateCommandGroup();
                UpdateCommandGroup();

                Assert.That(_em.HasComponent<CAbilityInTryEnd>(ability), Is.False);
            }
            finally
            {
                DestroyIfExists(ability);
                DestroyIfExists(owner);
            }
        }

        private Entity CreateActiveTimelineAbility(Entity owner, int timelineId, int level)
        {
            var ability = _em.CreateEntity();
            _em.AddComponentData(ability, new CAbilityBaseInfo
            {
                Code = 10001,
                Level = level,
                Owner = owner,
            });
            _em.AddComponentData(ability, new CAbilityRuntimeState
            {
                Phase = EAbilityPhase.Active,
            });
            _em.AddComponentData(ability, new CAbilityTimelineRef
            {
                TimelineId = timelineId,
            });
            _em.AddComponent<CAbilityActive>(ability);
            return ability;
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
            });
            return asc;
        }

        private static XParamTimeline CreateTimeline(
            int timelineId,
            int lifeTime,
            bool manualEnd,
            params TimelineActionClipData[] clips)
        {
            var track = new Track
            {
                Name = "GE Track",
            };
            track.ActionClips.AddRange(clips);

            return new XParamTimeline(
                timelineId,
                $"Timeline_{timelineId}",
                lifeTime,
                manualEnd,
                new List<Track> { track });
        }

        private static TimelineActionClipData CreateApplyEffectsClip(
            int startFrame,
            int effectCode,
            string catcherType)
        {
            var parameter = new XParamApplyEffects(new[] { effectCode });
            parameter.SetCatcherType(catcherType);
            parameter.SetParam(new XParamNone());

            return new TimelineActionClipData
            {
                Name = $"ApplyEffects_{effectCode}",
                StartTime = startFrame,
                EndTime = startFrame,
                ActionType = "ApplyEffects",
                Parameter = parameter,
            };
        }

        private static TimelineActionClipData CreateActionClip(int startFrame, string actionType)
        {
            return new TimelineActionClipData
            {
                Name = $"{actionType}_{startFrame}",
                StartTime = startFrame,
                EndTime = startFrame,
                ActionType = actionType,
            };
        }

        private static TimelineActionClipData CreatePlayCueClip(int startFrame)
        {
            return new TimelineActionClipData
            {
                Name = $"PlayCue_{startFrame}",
                StartTime = startFrame,
                EndTime = startFrame,
                ActionType = "PlayCue",
                Parameter = new XParamCue(nameof(RecordingCue), new XParamNone()),
            };
        }

        private static TimelineActionClipData CreatePlayCuePresetClip(int startFrame, int cueId)
        {
            return new TimelineActionClipData
            {
                Name = $"PlayCuePreset_{cueId}",
                StartTime = startFrame,
                EndTime = startFrame,
                ActionType = "PlayCuePreset",
                Parameter = new XParamCueList(new[] { cueId }),
            };
        }

        private static void RegisterTimeline(XParamTimeline timeline)
        {
            TimelineAbilityConfigRegistry.RegisterGetConfigByIDFunc(id => id == timeline.ID ? timeline : null);
        }

        private static void RegisterEffect(
            int effectCode,
            params GameplayEffectComponentConfig[] componentConfigs)
        {
            GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(id =>
                id == effectCode
                    ? new GameplayEffectConfig(componentConfigs ?? Array.Empty<GameplayEffectComponentConfig>())
                    : null);
        }

        private void UpdateCommandGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCommandGroup>().Update();
        }

        private void UpdateCueGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCueGroup>().Update();
        }

        private Entity FindEffectByCode(int gameplayEffectCode)
        {
            using var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CEffectSpecData>(),
                ComponentType.ReadOnly<CEffectContext>());
            using var effects = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < effects.Length; i++)
            {
                var spec = _em.GetComponentData<CEffectSpecData>(effects[i]);
                if (spec.GameplayEffectCode == gameplayEffectCode)
                    return effects[i];
            }

            return Entity.Null;
        }

        private int CountApplyRequestsByCode(int gameplayEffectCode)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CApplyGameplayEffectRequest>());
            using var requests = query.ToEntityArray(Allocator.Temp);
            var count = 0;
            for (var i = 0; i < requests.Length; i++)
            {
                if (_em.GetComponentData<CApplyGameplayEffectRequest>(requests[i]).GameplayEffectCode == gameplayEffectCode)
                    count++;
            }

            return count;
        }

        private BCueRequest FindCueRequest(EGameplayCueEvent cueEvent)
        {
            var requests = _em.GetBuffer<BCueRequest>(GASManager.EntityEventBus);
            for (var i = 0; i < requests.Length; i++)
            {
                if (requests[i].CueEvent == cueEvent)
                    return requests[i];
            }

            return default;
        }

        private void DestroyIfExists(Entity entity)
        {
            if (entity != Entity.Null && _em.Exists(entity))
                _em.DestroyEntity(entity);
        }

        private void ClearEffectCommandStream()
        {
            if (_em == default)
                return;

            if (EffectCommandSpecStream.TryGetSingleton(_em, out var streamEntity))
                EffectCommandSpecStream.ClearFrameLocalData(_em, streamEntity, 0);
        }

        private sealed class RecordingCue : GameplayCueBase
        {
            public int AddCount;
            public int ActivateCount;

            public override void InitParameters(XParam xParam)
            {
            }

            public override void OnAdd(float time)
            {
                AddCount++;
            }

            public override void OnActivate(float time)
            {
                ActivateCount++;
            }
        }
    }
}
