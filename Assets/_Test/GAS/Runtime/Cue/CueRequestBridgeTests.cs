using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Tests.Cue
{
    public sealed class CueRequestBridgeTests
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
            ClearEventBus();
        }

        [Test]
        public void CueRequestBridgePlaysStopsAndKillsCueEntities()
        {
            var target = _em.CreateEntity();
            var source = _em.CreateEntity();
            var effect = _em.CreateEntity();
            var cue = new RecordingCue();
            var cueEntity = CreateCueEntity(cue);

            try
            {
                EnqueueCueRequest(target, source, effect, cueEntity, EGameplayCueEvent.OnApply);
                UpdateCueGroup();

                Assert.That(cue.ResetCount, Is.EqualTo(1));
                Assert.That(cue.AddCount, Is.EqualTo(1));
                Assert.That(cue.ActivateCount, Is.EqualTo(1));
                Assert.That(_em.IsComponentEnabled<ECCuePlaying>(cueEntity), Is.True);

                ClearEventBus();
                EnqueueCueRequest(target, source, effect, cueEntity, EGameplayCueEvent.StopTick);
                UpdateCueGroup();

                Assert.That(cue.DeactivateCount, Is.EqualTo(1));
                Assert.That(_em.IsComponentEnabled<ECCuePlaying>(cueEntity), Is.False);

                ClearEventBus();
                EnqueueCueRequest(target, source, effect, cueEntity, EGameplayCueEvent.Kill);
                UpdateCueGroup();

                Assert.That(cue.DestroyCount, Is.EqualTo(1));
                Assert.That(_em.Exists(cueEntity), Is.False);
            }
            finally
            {
                DestroyIfExists(cueEntity);
                DestroyIfExists(effect);
                DestroyIfExists(source);
                DestroyIfExists(target);
            }
        }

        [Test]
        public void EffectRuntimeUtilityQueuesCueRequestInsteadOfPlayingImmediately()
        {
            var source = _em.CreateEntity();
            var target = _em.CreateEntity();
            var ability = _em.CreateEntity();
            var effect = _em.CreateEntity();
            var cue = new RecordingCue();
            var cueEntity = CreateCueEntity(cue);
            var cues = new NativeArray<Entity>(new[] { cueEntity }, Allocator.Persistent);

            try
            {
                var context = new CEffectContext
                {
                    SourceAsc = source,
                    TargetAsc = target,
                    SourceAbility = ability,
                    ContextId = 77,
                };

                _em.AddComponentData(effect, context);
                _em.AddComponentData(effect, new CCueOnApply { cues = cues });

                EffectRuntimeUtility.ApplyInstantEffect(_em, effect, context);

                Assert.That(cue.AddCount, Is.EqualTo(0));
                Assert.That(cue.ActivateCount, Is.EqualTo(0));

                var requests = _em.GetBuffer<BCueRequest>(GASManager.EntityEventBus);
                Assert.That(requests.Length, Is.EqualTo(1));
                Assert.That(requests[0].CueEntity, Is.EqualTo(cueEntity));
                Assert.That(requests[0].TargetAsc, Is.EqualTo(target));
                Assert.That(requests[0].SourceAsc, Is.EqualTo(source));
                Assert.That(requests[0].SourceAbility, Is.EqualTo(ability));
                Assert.That(requests[0].GameplayEffect, Is.EqualTo(effect));
                Assert.That(requests[0].ContextId, Is.EqualTo(77));
                Assert.That(requests[0].CueEvent, Is.EqualTo(EGameplayCueEvent.OnApply));

                var gameplayEvents = _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus);
                Assert.That(ContainsCueRequestedEvent(gameplayEvents, effect, target, 77), Is.True);

                UpdateCueGroup();

                Assert.That(cue.AddCount, Is.EqualTo(1));
                Assert.That(cue.ActivateCount, Is.EqualTo(1));
            }
            finally
            {
                if (cues.IsCreated)
                    cues.Dispose();

                DestroyIfExists(cueEntity);
                DestroyIfExists(effect);
                DestroyIfExists(ability);
                DestroyIfExists(target);
                DestroyIfExists(source);
            }
        }

        [Test]
        public void CueRequestBridgeUsesUnifiedTagRequirementEvaluation()
        {
            var target = _em.CreateEntity();
            var source = _em.CreateEntity();
            var effect = _em.CreateEntity();
            var cue = new RecordingCue();
            var cueEntity = CreateCueEntity(cue);

            _em.AddComponentData(target, new CTagMask());
            _em.AddComponentData(cueEntity, new CPlayRequiredTags
            {
                requirement = new TagRequirementMask { All = CreateMask(1) },
            });

            try
            {
                EnqueueCueRequest(target, source, effect, cueEntity, EGameplayCueEvent.OnApply);
                UpdateCueGroup();
                Assert.That(cue.AddCount, Is.EqualTo(0));
                Assert.That(cue.ActivateCount, Is.EqualTo(0));

                var tags = _em.GetComponentData<CTagMask>(target);
                tags.AddTag(1);
                _em.SetComponentData(target, tags);

                ClearEventBus();
                EnqueueCueRequest(target, source, effect, cueEntity, EGameplayCueEvent.OnApply);
                UpdateCueGroup();
                Assert.That(cue.AddCount, Is.EqualTo(1));
                Assert.That(cue.ActivateCount, Is.EqualTo(1));

                _em.AddComponentData(cueEntity, new CPlayImmunitedTags
                {
                    requirement = new TagRequirementMask { All = CreateMask(2) },
                });

                ClearEventBus();
                EnqueueCueRequest(target, source, effect, cueEntity, EGameplayCueEvent.OnApply);
                UpdateCueGroup();
                Assert.That(cue.AddCount, Is.EqualTo(2));
                Assert.That(cue.ActivateCount, Is.EqualTo(2));

                tags = _em.GetComponentData<CTagMask>(target);
                tags.AddTag(2);
                _em.SetComponentData(target, tags);

                ClearEventBus();
                EnqueueCueRequest(target, source, effect, cueEntity, EGameplayCueEvent.OnApply);
                UpdateCueGroup();
                Assert.That(cue.AddCount, Is.EqualTo(2));
                Assert.That(cue.ActivateCount, Is.EqualTo(2));
            }
            finally
            {
                DestroyIfExists(cueEntity);
                DestroyIfExists(effect);
                DestroyIfExists(source);
                DestroyIfExists(target);
            }
        }

        private Entity CreateCueEntity(RecordingCue cue)
        {
            var cueEntity = _em.CreateEntity();
            cue.SetCueEntity(cueEntity);

            _em.AddComponent<ECCuePlayable>(cueEntity);
            _em.SetComponentEnabled<ECCuePlayable>(cueEntity, false);
            _em.AddComponent<ECCuePlaying>(cueEntity);
            _em.SetComponentEnabled<ECCuePlaying>(cueEntity, false);
            _em.AddComponent<ECKillCue>(cueEntity);
            _em.SetComponentEnabled<ECKillCue>(cueEntity, false);
            _em.AddComponentData(cueEntity, new MCCue(cue));

            return cueEntity;
        }

        private void EnqueueCueRequest(
            Entity target,
            Entity source,
            Entity effect,
            Entity cueEntity,
            EGameplayCueEvent cueEvent)
        {
            _em.GetBuffer<BCueRequest>(GASManager.EntityEventBus).Add(new BCueRequest
            {
                TargetAsc = target,
                SourceAsc = source,
                GameplayEffect = effect,
                CueEntity = cueEntity,
                CueEvent = cueEvent,
            });
        }

        private static bool ContainsCueRequestedEvent(
            DynamicBuffer<BGameplayEvent> events,
            Entity effect,
            Entity target,
            int contextId)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Type == EGameplayEventType.CueRequested
                    && evt.GameplayEffect == effect
                    && evt.TargetAsc == target
                    && evt.ContextId == contextId
                    && evt.EventCode == (int)EGameplayCueEvent.OnApply)
                {
                    return true;
                }
            }

            return false;
        }

        private static CTagMask CreateMask(params int[] tagIndices)
        {
            var mask = new CTagMask();
            for (var i = 0; i < tagIndices.Length; i++)
                mask.AddTag(tagIndices[i]);

            return mask;
        }

        private void UpdateCueGroup()
        {
            GASManager.ExWorld.GetExistingSystemManaged<GASCueGroup>().Update();
        }

        private void ClearEventBus()
        {
            _em.GetBuffer<BCueRequest>(GASManager.EntityEventBus).Clear();
            _em.GetBuffer<BGameplayEvent>(GASManager.EntityEventBus).Clear();
        }

        private void DestroyIfExists(Entity entity)
        {
            if (entity != Entity.Null && _em.Exists(entity))
                _em.DestroyEntity(entity);
        }

        private sealed class RecordingCue : GameplayCueBase
        {
            public int ResetCount;
            public int AddCount;
            public int ActivateCount;
            public int DeactivateCount;
            public int DestroyCount;

            public override void InitParameters(XParam xParam)
            {
            }

            public override void Reset()
            {
                ResetCount++;
            }

            public override void OnAdd(float time)
            {
                AddCount++;
            }

            public override void OnActivate(float time)
            {
                ActivateCount++;
            }

            public override void OnDeactivate(float time)
            {
                DeactivateCount++;
            }

            public override void OnDestroy(float time)
            {
                DestroyCount++;
            }
        }
    }
}
