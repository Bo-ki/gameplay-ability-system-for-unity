using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public static class AbilityEntityFactory
    {
        public static Entity CreateAbilityEntity(AbilityConfig config)
        {
            return CreateAbilityEntity(config?.ComponentConfigs ?? System.Array.Empty<AbilityComponentConfig>());
        }

        public static Entity CreateAbilityEntity(AbilityComponentConfig[] configs)
        {
            var entity = GASManager.EntityManager.CreateEntity();
            GASManager.EntityManager.SetName(entity, $"Ability_{entity.ToString()}");
            foreach (var config in configs)
                config.LoadToGameplayAbilityEntity(entity);

            var em = GASManager.EntityManager;
            if (!em.HasComponent<CAbilityRuntimeState>(entity))
                em.AddComponent<CAbilityRuntimeState>(entity);
            var runtimeConfig = new CAbilityConfig
            {
                Config = BuildAbilityConfigBlob(entity, em),
            };
            if (em.HasComponent<CAbilityConfig>(entity))
                em.SetComponentData(entity, runtimeConfig);
            else
                em.AddComponentData(entity, runtimeConfig);

            return entity;
        }

        private static BlobAssetReference<BlobAbilityConfig> BuildAbilityConfigBlob(Entity ability, EntityManager em)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BlobAbilityConfig>();

            if (em.HasComponent<CAbilityBaseInfo>(ability))
            {
                var info = em.GetComponentData<CAbilityBaseInfo>(ability);
                root.Code = info.Code;
                root.MaxLevel = info.Level;
            }

            root.AssetTags = GetAbilityAssetTags(ability, em);
            root.ActivationRequiredTags = GetRequiredTags(ability, em);
            root.ActivationBlockedTags = GetBlockedTags(ability, em);
            root.ActivationOwnedTags = GetActivationOwnedTags(ability, em);
            root.CancelAbilityTags = GetCancelAbilityTags(ability, em);
            root.BlockAbilityTags = GetBlockAbilityTags(ability, em);
            root.CooldownTags = GetCooldownTags(ability, em);
            CopyCostModifiers(builder, ref root.CostModifiers, GetCostModifiers(ability, em));

            if (em.HasComponent<CAbilityCooldown>(ability))
            {
                var cooldown = em.GetComponentData<CAbilityCooldown>(ability);
                root.Cooldown = cooldown.Cooldown;
            }

            var blob = builder.CreateBlobAssetReference<BlobAbilityConfig>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        private static CTagMask GetAbilityAssetTags(Entity ability, EntityManager em)
        {
            return em.HasComponent<CAbilityAssetTags>(ability)
                ? em.GetComponentData<CAbilityAssetTags>(ability).Tags
                : default;
        }

        private static TagRequirementMask GetRequiredTags(Entity ability, EntityManager em)
        {
            return em.HasComponent<CAbilityActivationRequiredTags>(ability)
                ? em.GetComponentData<CAbilityActivationRequiredTags>(ability).requirement
                : default;
        }

        private static TagRequirementMask GetBlockedTags(Entity ability, EntityManager em)
        {
            return em.HasComponent<CAbilityActivationBlockedTags>(ability)
                ? em.GetComponentData<CAbilityActivationBlockedTags>(ability).requirement
                : default;
        }

        private static CTagMask GetActivationOwnedTags(Entity ability, EntityManager em)
        {
            return em.HasComponent<CAbilityActivationOwnedTags>(ability)
                ? em.GetComponentData<CAbilityActivationOwnedTags>(ability).Tags
                : default;
        }

        private static CTagMask GetCancelAbilityTags(Entity ability, EntityManager em)
        {
            return em.HasComponent<CCancelAbilityWithTags>(ability)
                ? em.GetComponentData<CCancelAbilityWithTags>(ability).Tags
                : default;
        }

        private static CTagMask GetBlockAbilityTags(Entity ability, EntityManager em)
        {
            return em.HasComponent<CBlockAbilityWithTags>(ability)
                ? em.GetComponentData<CBlockAbilityWithTags>(ability).Tags
                : default;
        }

        private static CTagMask GetCooldownTags(Entity ability, EntityManager em)
        {
            if (!em.HasComponent<CAbilityCooldown>(ability))
                return default;

            var cooldown = em.GetComponentData<CAbilityCooldown>(ability);
            if (cooldown.GameplayEffectCode <= 0)
                return default;

            return GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(
                em,
                cooldown.GameplayEffectCode,
                out var blob,
                new ConfigRegistryReferenceContext(
                    ConfigRegistryConfigKind.Ability,
                    GetAbilityCode(ability, em),
                    ConfigRegistryReferenceKind.AbilityCooldown))
                ? blob.Value.GrantedTags
                : default;
        }

        private static BlobAbilityCostModifier[] GetCostModifiers(Entity ability, EntityManager em)
        {
            if (!em.HasComponent<CAbilityCost>(ability)) return System.Array.Empty<BlobAbilityCostModifier>();

            var cost = em.GetComponentData<CAbilityCost>(ability);
            if (cost.GameplayEffectCode <= 0)
                return System.Array.Empty<BlobAbilityCostModifier>();

            if (!GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(
                    em,
                    cost.GameplayEffectCode,
                    out var blob,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.Ability,
                        GetAbilityCode(ability, em),
                        ConfigRegistryReferenceKind.AbilityCost)))
            {
                return System.Array.Empty<BlobAbilityCostModifier>();
            }

            var modifiers = new List<BlobAbilityCostModifier>();
            ref var definition = ref blob.Value;
            for (var i = 0; i < definition.Modifiers.Length; i++)
            {
                var modifier = definition.Modifiers[i];
                if (modifier.Operation != EModifierOp.Add && modifier.Operation != EModifierOp.Subtract)
                    continue;

                modifiers.Add(new BlobAbilityCostModifier
                {
                    AttrSetCode = modifier.AttrSetCode,
                    AttributeCode = modifier.AttributeCode,
                    Magnitude = modifier.Magnitude,
                    Op = modifier.Operation,
                });
            }

            return modifiers.ToArray();
        }

        private static int GetAbilityCode(Entity ability, EntityManager em)
        {
            return em.HasComponent<CAbilityBaseInfo>(ability)
                ? em.GetComponentData<CAbilityBaseInfo>(ability).Code
                : 0;
        }

        private static void CopyCostModifiers(BlobBuilder builder, ref BlobArray<BlobAbilityCostModifier> target,
            BlobAbilityCostModifier[] source)
        {
            source ??= System.Array.Empty<BlobAbilityCostModifier>();
            var array = builder.Allocate(ref target, source.Length);
            for (var i = 0; i < source.Length; i++)
                array[i] = source[i];
        }

    }
}
