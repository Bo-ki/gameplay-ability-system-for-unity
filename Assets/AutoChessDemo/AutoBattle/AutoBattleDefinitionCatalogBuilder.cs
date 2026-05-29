using Unity.Collections;
using Unity.Entities;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    public static class AutoBattleDefinitionCatalogBuilder
    {
        private const int SchemaVersion = 1;
        private const int CueAutoBattleHit = 9301;

        private static BlobAssetReference<GASDefinitionCatalogBlob> _installedCatalog;

        public static void Install(EntityManager entityManager)
        {
            if (_installedCatalog.IsCreated)
                _installedCatalog.Dispose();

            _installedCatalog = Build(Allocator.Persistent);
            var catalogEntity = ResolveCatalogEntity(entityManager);
            entityManager.SetName(catalogEntity, "AutoBattleDefinitionCatalog");
            entityManager.SetComponentData(catalogEntity, new GASDefinitionCatalogComponent
            {
                Catalog = _installedCatalog,
                Revision = SchemaVersion,
            });
        }

        public static void Uninstall(EntityManager entityManager)
        {
            if (entityManager.World == null || !entityManager.World.IsCreated)
            {
                DisposeInstalledCatalog();
                return;
            }

            var catalogEntity = ResolveExistingCatalogEntity(entityManager);
            if (catalogEntity != Entity.Null && entityManager.Exists(catalogEntity))
            {
                entityManager.SetComponentData(catalogEntity, new GASDefinitionCatalogComponent
                {
                    Catalog = default,
                    Revision = 0,
                });
            }

            DisposeInstalledCatalog();
        }

        private static void DisposeInstalledCatalog()
        {
            if (!_installedCatalog.IsCreated)
                return;

            _installedCatalog.Dispose();
            _installedCatalog = default;
        }

        private static BlobAssetReference<GASDefinitionCatalogBlob> Build(Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GASDefinitionCatalogBlob>();
                root.SchemaVersion = SchemaVersion;

                var abilityCodes = builder.Allocate(ref root.AbilityCodes, 3);
                abilityCodes[0] = HeadlessAutoBattleScenario.AbilityPlayerAttack;
                abilityCodes[1] = HeadlessAutoBattleScenario.AbilityEnemyAttack;
                abilityCodes[2] = HeadlessAutoBattleScenario.AbilityPlayerExecute;

                var abilities = builder.Allocate(ref root.Abilities, 3);
                abilities[0] = CreateAbility(
                    HeadlessAutoBattleScenario.AbilityPlayerAttack,
                    HeadlessAutoBattleScenario.GameplayEffectPlayerAttackDamage);
                abilities[1] = CreateAbility(
                    HeadlessAutoBattleScenario.AbilityEnemyAttack,
                    HeadlessAutoBattleScenario.GameplayEffectEnemyAttackDamage);
                abilities[2] = CreateAbility(
                    HeadlessAutoBattleScenario.AbilityPlayerExecute,
                    HeadlessAutoBattleScenario.GameplayEffectPlayerExecute);

                var gameplayEffectCodes = builder.Allocate(ref root.GameplayEffectCodes, 3);
                gameplayEffectCodes[0] = HeadlessAutoBattleScenario.GameplayEffectPlayerAttackDamage;
                gameplayEffectCodes[1] = HeadlessAutoBattleScenario.GameplayEffectEnemyAttackDamage;
                gameplayEffectCodes[2] = HeadlessAutoBattleScenario.GameplayEffectPlayerExecute;

                var gameplayEffects = builder.Allocate(ref root.GameplayEffects, 3);
                gameplayEffects[0] = CreateInstantModifierEffect(
                    HeadlessAutoBattleScenario.GameplayEffectPlayerAttackDamage,
                    modifierStart: 0);
                gameplayEffects[1] = CreateInstantModifierEffect(
                    HeadlessAutoBattleScenario.GameplayEffectEnemyAttackDamage,
                    modifierStart: 1);
                gameplayEffects[2] = CreateExecuteEffect();

                var modifiers = builder.Allocate(ref root.Modifiers, 2);
                modifiers[0] = CreateHealthDamageModifier(
                    HeadlessAutoBattleScenario.GameplayEffectPlayerAttackDamage,
                    modifierIndex: 0,
                    magnitude: 12f);
                modifiers[1] = CreateHealthDamageModifier(
                    HeadlessAutoBattleScenario.GameplayEffectEnemyAttackDamage,
                    modifierIndex: 1,
                    magnitude: 8f);

                builder.Allocate(ref root.Requirements, 0);
                builder.Allocate(ref root.TagMasks, 0);
                builder.Allocate(ref root.TagMaskCodes, 0);
                builder.Allocate(ref root.GrantedAbilities, 0);

                return builder.CreateBlobAssetReference<GASDefinitionCatalogBlob>(allocator);
            }
            finally
            {
                builder.Dispose();
            }
        }

        private static Entity ResolveCatalogEntity(EntityManager entityManager)
        {
            var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<GASDefinitionCatalogComponent>());
            var entities = query.ToEntityArray(Allocator.Temp);
            try
            {
                if (entities.Length > 0)
                    return entities[0];
            }
            finally
            {
                entities.Dispose();
                query.Dispose();
            }

            return entityManager.CreateEntity(ComponentType.ReadWrite<GASDefinitionCatalogComponent>());
        }

        private static Entity ResolveExistingCatalogEntity(EntityManager entityManager)
        {
            var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<GASDefinitionCatalogComponent>());
            var entities = query.ToEntityArray(Allocator.Temp);
            try
            {
                return entities.Length > 0 ? entities[0] : Entity.Null;
            }
            finally
            {
                entities.Dispose();
                query.Dispose();
            }
        }

        private static GASCatalogAbilityDefinitionBlob CreateAbility(int abilityCode, int primaryGameplayEffectCode)
        {
            return new GASCatalogAbilityDefinitionBlob
            {
                AbilityCode = abilityCode,
                Level = 1,
                PrimaryGameplayEffectCode = primaryGameplayEffectCode,
                ActivationOwnedTagMaskIndex = -1,
                TargetRuleCode = 0,
            };
        }

        private static GASCatalogGameplayEffectDefinitionBlob CreateInstantModifierEffect(
            int gameplayEffectCode,
            int modifierStart)
        {
            return new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = gameplayEffectCode,
                GameplayCueCode = CueAutoBattleHit,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                ModifierStart = modifierStart,
                ModifierCount = 1,
            };
        }

        private static GASCatalogGameplayEffectDefinitionBlob CreateExecuteEffect()
        {
            return new GASCatalogGameplayEffectDefinitionBlob
            {
                GameplayEffectCode = HeadlessAutoBattleScenario.GameplayEffectPlayerExecute,
                GameplayCueCode = CueAutoBattleHit,
                GrantedTagMaskIndex = -1,
                RemoveGameplayEffectTagMaskIndex = -1,
                ModifierStart = 2,
                ModifierCount = 0,
            };
        }

        private static GASCatalogModifierDefinitionBlob CreateHealthDamageModifier(
            int gameplayEffectCode,
            int modifierIndex,
            float magnitude)
        {
            return new GASCatalogModifierDefinitionBlob
            {
                GameplayEffectCode = gameplayEffectCode,
                ModifierIndex = modifierIndex,
                AttributeSetCode = HeadlessAutoBattleScenario.AttributeSetCombat,
                AttributeCode = HeadlessAutoBattleScenario.AttributeHealth,
                Operation = EModifierOp.Subtract,
                BaseMagnitude = magnitude,
                MagnitudeSource = EMagnitudeSource.Constant,
                FallbackMagnitude = magnitude,
                Coefficient = 1f,
            };
        }
    }
}
