using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Dispatches Timeline ApplyEffects clips into ECS GameplayEffect requests.
    /// </summary>
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SAbilityCommit))]
    [UpdateBefore(typeof(SApplyGameplayEffectRequest))]
    public partial struct SAbilityTimelineAction : ISystem
    {
        private const string ApplyEffectsActionType = "ApplyEffects";
        private const string ApplyCostActionType = "ApplyCost";
        private const string ApplyCooldownActionType = "ApplyCooldown";
        private const string PlayCueActionType = "PlayCue";
        private const string PlayCuePresetActionType = "PlayCuePreset";

        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CAbilityActive, CAbilityBaseInfo, CAbilityRuntimeState, CAbilityTimelineRef>()
                .Build();

            state.RequireForUpdate(_query);
            state.RequireForUpdate<GlobalTimer>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var currentFrame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            var abilities = _query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i];
                if (!em.Exists(ability))
                    continue;

                var runtime = em.GetComponentData<CAbilityRuntimeState>(ability);
                if (runtime.Phase != EAbilityPhase.Active && runtime.Phase != EAbilityPhase.Activating)
                    continue;

                var timelineRef = em.GetComponentData<CAbilityTimelineRef>(ability);
                if (timelineRef.TimelineId <= 0)
                    continue;

                var timeline = TimelineAbilityConfigRegistry.GetConfigByID(
                    timelineRef.TimelineId,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.Ability,
                        GetAbilityCode(em, ability),
                        ConfigRegistryReferenceKind.AbilityTimeline));
                if (timeline == null)
                    continue;

                var baseInfo = em.GetComponentData<CAbilityBaseInfo>(ability);
                var timelineState = GetOrInitializeTimelineState(em, ability, timelineRef.TimelineId, currentFrame);
                var timelineFrame = currentFrame - timelineState.StartFrame;
                if (timelineFrame < 0)
                    timelineFrame = 0;

                var mainTarget = ResolveMainTarget(em, ability, baseInfo.Owner);

                DispatchTimelineActions(
                    em,
                    ability,
                    mainTarget,
                    timeline,
                    timelineState.LastDispatchedElapsedFrame,
                    timelineFrame);

                timelineState.LastDispatchedElapsedFrame = timelineFrame;
                em.SetComponentData(ability, timelineState);

            }

            abilities.Dispose();
        }

        private static CAbilityTimelineRuntime GetOrInitializeTimelineState(
            EntityManager em,
            Entity ability,
            int timelineId,
            int currentFrame)
        {
            if (em.HasComponent<CAbilityTimelineRuntime>(ability))
            {
                var state = em.GetComponentData<CAbilityTimelineRuntime>(ability);
                if (state.Initialized != 0 && state.TimelineId == timelineId)
                    return state;
            }

            var created = new CAbilityTimelineRuntime
            {
                TimelineId = timelineId,
                StartFrame = currentFrame,
                LastDispatchedElapsedFrame = -1,
                Initialized = 1,
            };

            if (em.HasComponent<CAbilityTimelineRuntime>(ability))
                em.SetComponentData(ability, created);
            else
                em.AddComponentData(ability, created);

            return created;
        }

        private static Entity ResolveMainTarget(EntityManager em, Entity ability, Entity fallbackTarget)
        {
            if (em.HasComponent<CAbilityMainTarget>(ability))
            {
                var context = em.GetComponentData<CAbilityMainTarget>(ability);
                if (IsAvailableAsc(em, context.TargetAsc))
                    return context.TargetAsc;
            }

            return fallbackTarget;
        }

        private static bool IsAvailableAsc(EntityManager em, Entity asc)
        {
            return asc != Entity.Null
                   && em.Exists(asc)
                   && !em.HasComponent<CAscDestroying>(asc);
        }

        private static void DispatchTimelineActions(
            EntityManager em,
            Entity ability,
            Entity mainTarget,
            XParamTimeline timeline,
            int fromFrameExclusive,
            int toFrameInclusive)
        {
            if (timeline.Tracks == null || timeline.Tracks.Count == 0)
                return;

            for (var trackIndex = 0; trackIndex < timeline.Tracks.Count; trackIndex++)
            {
                var track = timeline.Tracks[trackIndex];
                if (track?.ActionClips == null || track.ActionClips.Count == 0)
                    continue;

                for (var clipIndex = 0; clipIndex < track.ActionClips.Count; clipIndex++)
                {
                    var clip = track.ActionClips[clipIndex];
                    if (!ShouldDispatchClip(clip, fromFrameExclusive, toFrameInclusive))
                        continue;

                    DispatchTimelineAction(em, ability, mainTarget, clip);
                }
            }
        }

        private static void DispatchTimelineAction(
            EntityManager em,
            Entity ability,
            Entity mainTarget,
            TimelineActionClipData clip)
        {
            switch (clip.ActionType)
            {
                case ApplyEffectsActionType when clip.Parameter is XParamApplyEffects applyEffects:
                    TimelineApplyEffectsProducer.RequestApplyEffects(em, ability, mainTarget, applyEffects);
                    break;
                case ApplyCostActionType:
                    AbilityRuntimeActions.RequestCostGameplayEffect(ability, em);
                    break;
                case ApplyCooldownActionType:
                    AbilityRuntimeActions.RequestCooldownGameplayEffect(ability, em);
                    break;
                case PlayCueActionType when clip.Parameter is XParamCue cue:
                    RequestCue(em, ability, mainTarget, cue.GetCueConfig());
                    break;
                case PlayCuePresetActionType when clip.Parameter is XParamCueList cueList:
                    RequestCuePreset(em, ability, mainTarget, cueList);
                    break;
            }
        }

        private static void RequestCuePreset(
            EntityManager em,
            Entity ability,
            Entity target,
            XParamCueList cueList)
        {
            if (cueList?.IDs == null || cueList.IDs.Length == 0)
                return;

            for (var i = 0; i < cueList.IDs.Length; i++)
            {
                var cueId = cueList.IDs[i];
                if (cueId <= 0)
                    continue;

                RequestCue(
                    em,
                    ability,
                    target,
                    GameplayCueConfigRegistry.GetConfigByID(
                        cueId,
                        new ConfigRegistryReferenceContext(
                            ConfigRegistryConfigKind.Ability,
                            GetAbilityCode(em, ability),
                            ConfigRegistryReferenceKind.AbilityCuePreset)));
            }
        }

        private static Entity RequestCue(
            EntityManager em,
            Entity ability,
            Entity target,
            GameplayCueConfig config)
        {
            if (!TryGetBaseInfo(em, ability, out var baseInfo)
                || !IsAvailableAsc(em, baseInfo.Owner)
                || !IsAvailableAsc(em, target))
                return Entity.Null;

            var cueEntity = CreateCueEntity(em, config, ability, CueSourceType.GameplayAbility);
            if (cueEntity == Entity.Null)
                return Entity.Null;

            EventBusHelper.EnqueueCueRequest(em, GASManager.EntityEventBus, new BCueRequest
            {
                TargetAsc = target,
                SourceAsc = baseInfo.Owner,
                SourceAbility = ability,
                SourceEntity = ability,
                SourceType = CueSourceType.GameplayAbility,
                CueEntity = cueEntity,
                CueEvent = EGameplayCueEvent.Play,
            });

            EventBusHelper.EnqueueGameplayEvent(em, GASManager.EntityEventBus, new BGameplayEvent
            {
                Type = EGameplayEventType.CueRequested,
                SourceAsc = baseInfo.Owner,
                TargetAsc = target,
                SourceAbility = ability,
                EventCode = (int)EGameplayCueEvent.Play,
            });

            return cueEntity;
        }

        private static Entity CreateCueEntity(
            EntityManager em,
            GameplayCueConfig config,
            Entity sourceEntity,
            CueSourceType sourceType)
        {
            if (config?.CueType == null)
                return Entity.Null;

            var cue = config.CreateCue();
            if (cue == null)
                return Entity.Null;

            var cueEntity = em.CreateEntity();
            em.SetName(cueEntity, $"Cue_{config.CueType.Name}_V{cueEntity.Version}_{cueEntity.Index}");

            cue.SetCueEntity(cueEntity);
            cue.SetSourceEntity(sourceEntity, sourceType);

            em.AddComponent<ECCuePlayable>(cueEntity);
            em.SetComponentEnabled<ECCuePlayable>(cueEntity, false);
            em.AddComponent<ECCuePlaying>(cueEntity);
            em.SetComponentEnabled<ECCuePlaying>(cueEntity, false);
            em.AddComponent<ECKillCue>(cueEntity);
            em.SetComponentEnabled<ECKillCue>(cueEntity, false);
            em.AddComponentData(cueEntity, new MCCue(cue));

            AddCueTagRequirements(em, cueEntity, config);
            return cueEntity;
        }

        private static void AddCueTagRequirements(
            EntityManager em,
            Entity cueEntity,
            GameplayCueConfig config)
        {
            if (HasTags(config.RequiredAllTags)
                || HasTags(config.RequiredAnyTags)
                || HasTags(config.RequiredNoneTags))
            {
                em.AddComponentData(cueEntity, new CPlayRequiredTags
                {
                    requirement = TagHelper.BuildRequirementMask(
                        config.RequiredAllTags,
                        config.RequiredAnyTags,
                        config.RequiredNoneTags),
                });
            }

            if (HasTags(config.ImmunityAllTags)
                || HasTags(config.ImmunityAnyTags)
                || HasTags(config.ImmunityNoneTags))
            {
                em.AddComponentData(cueEntity, new CPlayImmunitedTags
                {
                    requirement = TagHelper.BuildRequirementMask(
                        config.ImmunityAllTags,
                        config.ImmunityAnyTags,
                        config.ImmunityNoneTags),
                });
            }
        }

        private static bool HasTags(int[] tags)
        {
            return tags != null && tags.Length > 0;
        }

        private static bool TryGetBaseInfo(EntityManager em, Entity ability, out CAbilityBaseInfo baseInfo)
        {
            baseInfo = default;
            if (!em.Exists(ability) || !em.HasComponent<CAbilityBaseInfo>(ability))
                return false;

            baseInfo = em.GetComponentData<CAbilityBaseInfo>(ability);
            return true;
        }

        private static int GetAbilityCode(EntityManager em, Entity ability)
        {
            return em.Exists(ability) && em.HasComponent<CAbilityBaseInfo>(ability)
                ? em.GetComponentData<CAbilityBaseInfo>(ability).Code
                : 0;
        }

        private static bool ShouldDispatchClip(
            TimelineActionClipData clip,
            int fromFrameExclusive,
            int toFrameInclusive)
        {
            if (clip == null
                || clip.StartTime <= fromFrameExclusive
                || clip.StartTime > toFrameInclusive)
                return false;

            return clip.ActionType == ApplyEffectsActionType
                   || clip.ActionType == ApplyCostActionType
                   || clip.ActionType == ApplyCooldownActionType
                   || clip.ActionType == PlayCueActionType
                   || clip.ActionType == PlayCuePresetActionType;
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
