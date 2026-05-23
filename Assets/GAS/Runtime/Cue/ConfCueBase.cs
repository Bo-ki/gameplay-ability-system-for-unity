using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public abstract class ConfCueBase: GameplayEffectComponentConfig
    {
        public GameplayCueConfig[] cues;

        public override bool SupportsPrototypeCache => false;
        public override bool SupportsStaticDefinitionBlob => false;

        public NativeArray<Entity> CreateCueEntityArray(Entity ge)
        {
            bool HasTags(int[] tags) => tags != null && tags.Length > 0;

            var em = GASManager.EntityManager;
            var entities = new Entity[cues.Length];
            for (var i = 0; i < cues.Length; i++)
            {
                entities[i] = em.CreateEntity();
                var c = cues[i];
                em.SetName(entities[i], $"Cue_{c.CueType.Name}_V{entities[i].Version}_{entities[i].Index}");
                // Cue是否可播放组件
                em.AddComponent<ECCuePlayable>(entities[i]);
                em.SetComponentEnabled<ECCuePlayable>(entities[i], false);

                // Cue是否播放中组件
                em.AddComponent<ECCuePlaying>(entities[i]);
                em.SetComponentEnabled<ECCuePlaying>(entities[i], false);

                // Cue是否死亡组件
                em.AddComponent<ECKillCue>(entities[i]);
                em.SetComponentEnabled<ECKillCue>(entities[i], false);

                // Cue逻辑
                var instantCue = CueHelper.InitInstantCueFromGameplayEffect(new MCCue(c.CreateCue()),entities[i],ge);
                em.AddComponentData(entities[i], instantCue);

                // cue播放免疫tag
                if (HasTags(c.ImmunityAllTags) || HasTags(c.ImmunityAnyTags) || HasTags(c.ImmunityNoneTags))
                {
                    em.AddComponentData(entities[i], new CPlayImmunitedTags
                    {
                        requirement = TagHelper.BuildRequirementMask(c.ImmunityAllTags, c.ImmunityAnyTags, c.ImmunityNoneTags)
                    });
                }

                // cue播放需求tag
                if (HasTags(c.RequiredAllTags) || HasTags(c.RequiredAnyTags) || HasTags(c.RequiredNoneTags))
                {
                    em.AddComponentData(entities[i], new CPlayRequiredTags
                    {
                        requirement = TagHelper.BuildRequirementMask(c.RequiredAllTags, c.RequiredAnyTags, c.RequiredNoneTags)
                    });
                }
            }

            return new NativeArray<Entity>(entities, Allocator.Persistent);
        }
    }
}
