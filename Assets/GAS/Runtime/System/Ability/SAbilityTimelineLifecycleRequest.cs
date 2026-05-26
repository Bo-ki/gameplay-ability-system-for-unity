using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Produces explicit lifecycle requests from Timeline lifetime rules.
    /// Timeline clip dispatch stays in SAbilityTimelineAction.
    /// </summary>
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SAbilityTimelineAction))]
    [DisableAutoCreation]
    public partial struct SAbilityTimelineLifecycleRequest : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CAbilityActive, CAbilityBaseInfo, CAbilityRuntimeState, CAbilityTimelineRef, CAbilityTimelineRuntime>()
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

                if (em.HasComponent<CAbilityInTryEnd>(ability)
                    || em.HasComponent<CAbilityInTryCancel>(ability))
                {
                    continue;
                }

                var runtime = em.GetComponentData<CAbilityRuntimeState>(ability);
                if (runtime.Phase != EAbilityPhase.Active
                    && runtime.Phase != EAbilityPhase.Activating
                    && runtime.Phase != EAbilityPhase.Ending)
                {
                    continue;
                }

                var timelineRef = em.GetComponentData<CAbilityTimelineRef>(ability);
                if (timelineRef.TimelineId <= 0)
                    continue;

                var timelineState = em.GetComponentData<CAbilityTimelineRuntime>(ability);
                if (timelineState.Initialized == 0 || timelineState.TimelineId != timelineRef.TimelineId)
                    continue;

                var baseInfo = em.GetComponentData<CAbilityBaseInfo>(ability);
                var timeline = TimelineAbilityConfigRegistry.GetConfigByID(
                    timelineRef.TimelineId,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.Ability,
                        baseInfo.Code,
                        ConfigRegistryReferenceKind.AbilityTimeline));
                if (timeline == null || timeline.ManualEndAbility || timeline.LifeTime <= 0)
                    continue;

                var timelineFrame = currentFrame - timelineState.StartFrame;
                if (timelineFrame < timeline.LifeTime)
                    continue;

                AbilityRuntimeActions.RequestAbilityEnd(
                    ability,
                    em,
                    EAbilityLifecycleReason.TimelineCompleted,
                    sourceAbility: ability,
                    sourceAbilityCode: baseInfo.Code);
            }

            abilities.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
