using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    public readonly struct AbilitySystemFacade
    {
        public readonly Entity Entity;

        private static EntityManager EntityManager => GASManager.EntityManager;

        public AbilitySystemFacade(Entity entity)
        {
            Entity = entity;
        }

        public bool IsValid => Entity != Entity.Null
                               && EntityManager.Exists(Entity);

        public GameObject GameObject => EntityHelper.GetGameObjectFromEntity(Entity);

        public AbilitySystemObservation Observation => new AbilitySystemObservation(Entity);

        #region Factory

        public static AbilitySystemFacade Create()
        {
            return new AbilitySystemFacade(AbilitySystemEntityFactory.Create(EntityManager));
        }

        public Entity Dispose()
        {
            if (GASManager.ExWorld == null || !GASManager.ExWorld.IsCreated || !EntityManager.Exists(Entity))
                return Entity.Null;

            if (EntityManager.HasComponent<CAscDestroying>(Entity))
                return Entity.Null;

            var request = EntityManager.CreateEntity();
            EntityManager.SetName(request, $"AscDestroyRequest_{Entity.Index}_{request.Index}");
            EntityManager.AddComponentData(request, new CAscDestroyRequest
            {
                ASC = Entity,
            });
            return request;
        }

        public Entity Init(AbilitySystemConfig config)
        {
            return Init(config.BaseTags, config.AttrSets, config.BaseAbilityCodes, config.Level);
        }

        public Entity Init(IEnumerable<int> baseTags, IEnumerable<AttrSetConfig> attrSets,
            IEnumerable<int> baseAbilityCodes, int level = 1)
        {
            if (!IsValid || EntityManager.HasComponent<CAscDestroying>(Entity))
                return Entity.Null;

            var request = EntityManager.CreateEntity();
            EntityManager.SetName(request, $"AscInitializeRequest_{Entity.Index}_{request.Index}");
            EntityManager.AddComponentData(request, new CAscInitializeRequest
            {
                ASC = Entity,
                Level = level,
            });

            FillInitTags(request, baseTags);
            FillInitAttributes(request, attrSets);
            FillInitAbilities(request, baseAbilityCodes);

            return request;
        }

        #endregion

        #region GameplayEffect

        public Entity RequestGameplayEffectTo(int gameplayEffectCode, AbilitySystemFacade target, int level = 0)
        {
            return ApplyGameplayEffectToTarget(gameplayEffectCode, target.Entity, level, null);
        }

        public Entity RequestGameplayEffectTo(
            int gameplayEffectCode,
            AbilitySystemFacade target,
            IReadOnlyList<BSetByCallerValue> setByCallerValues,
            int level = 0)
        {
            return ApplyGameplayEffectToTarget(gameplayEffectCode, target.Entity, level, setByCallerValues);
        }

        public Entity RequestGameplayEffectToSelf(int gameplayEffectCode, int level = 0)
        {
            return ApplyGameplayEffectToTarget(gameplayEffectCode, Entity, level, null);
        }

        public Entity RequestGameplayEffectToSelf(
            int gameplayEffectCode,
            IReadOnlyList<BSetByCallerValue> setByCallerValues,
            int level = 0)
        {
            return ApplyGameplayEffectToTarget(gameplayEffectCode, Entity, level, setByCallerValues);
        }

        public Entity RemoveGameplayEffect(Entity gameplayEffect)
        {
            return Entity.Null;
        }

        public void ClearGameplayEffects()
        {
        }

        #endregion

        private Entity ApplyGameplayEffectToTarget(
            int gameplayEffectCode,
            Entity target,
            int level,
            IReadOnlyList<BSetByCallerValue> setByCallerValues)
        {
            if (!IsValid || EntityManager.HasComponent<CAscDestroying>(Entity))
                return Entity.Null;

            GameplayEffectRequestWriter.TryAppendSimpleInstantCommand(
                EntityManager,
                new CApplyGameplayEffectRequest
                {
                    SourceAsc = Entity,
                    SourceAbility = Entity.Null,
                    Instigator = Entity,
                    Causer = Entity,
                    GameplayEffectCode = gameplayEffectCode,
                    Level = level,
                },
                target,
                target == Entity ? ETargetDataKind.Self : ETargetDataKind.Entity,
                setByCallerValues);
            return Entity.Null;
        }

        #region BasicData

        public Entity SetLevel(int level)
        {
            return CreateAscCommandRequest(new CAscCommandRequest
            {
                CommandType = EAscCommandType.SetLevel,
                Level = level,
            });
        }

        public int GetLevel()
        {
            return Observation.GetLevel();
        }

        public bool TryGetLevel(out int level)
        {
            return Observation.TryGetLevel(out level);
        }

        #endregion

        #region Tag

        public bool HasTag(int tag)
        {
            return Observation.HasTag(tag);
        }

        public bool TryGetTagMask(out CTagMask mask)
        {
            return Observation.TryGetTagMask(out mask);
        }

        #endregion

        #region Attribute

        public float GetAttributeValue(int attrSetCode, int attrCode)
        {
            return Observation.GetAttributeValue(attrSetCode, attrCode);
        }

        public bool TryGetAttributeValue(int attrSetCode, int attrCode, out float value)
        {
            return Observation.TryGetAttributeValue(attrSetCode, attrCode, out value);
        }

        #endregion

        #region Ability

        public Entity TryActivateAbility(int abilityCode)
        {
            return CreateAscCommandRequest(new CAscCommandRequest
            {
                CommandType = EAscCommandType.TryActivateAbility,
                AbilityCode = abilityCode,
            });
        }

        public Entity CancelAbility(int abilityCode)
        {
            return CreateAscCommandRequest(new CAscCommandRequest
            {
                CommandType = EAscCommandType.CancelAbility,
                AbilityCode = abilityCode,
            });
        }

        #endregion

        #region Internal

        private Entity CreateAscCommandRequest(in CAscCommandRequest command)
        {
            if (!IsValid || EntityManager.HasComponent<CAscDestroying>(Entity))
                return Entity.Null;

            var request = EntityManager.CreateEntity();
            EntityManager.SetName(request, $"AscCommand_{command.CommandType}_{request.Index}");
            command.ASC = Entity;
            EntityManager.AddComponentData(request, command);
            return request;
        }

        private void FillInitTags(Entity request, IEnumerable<int> baseTags)
        {
            if (baseTags == null)
                return;

            foreach (var tag in baseTags)
            {
                var buffer = EntityManager.HasBuffer<BInitTag>(request)
                    ? EntityManager.GetBuffer<BInitTag>(request)
                    : EntityManager.AddBuffer<BInitTag>(request);
                buffer.Add(new BInitTag { TagIndex = tag });
            }
        }

        private void FillInitAttributes(Entity request, IEnumerable<AttrSetConfig> attrSets)
        {
            if (attrSets == null)
                return;

            foreach (var attrSet in attrSets)
            {
                if (attrSet?.Attributes == null) continue;
                foreach (var attr in attrSet.Attributes)
                {
                    var buffer = EntityManager.HasBuffer<BInitAttribute>(request)
                        ? EntityManager.GetBuffer<BInitAttribute>(request)
                        : EntityManager.AddBuffer<BInitAttribute>(request);
                    buffer.Add(new BInitAttribute
                    {
                        AttrSetCode = attrSet.SetCode,
                        Code = attr.Code,
                        BaseValue = attr.BaseValue,
                        MaxValue = attr.MaxValue,
                        MinValue = attr.MinValue,
                    });
                }
            }
        }

        private void FillInitAbilities(Entity request, IEnumerable<int> baseAbilityCodes)
        {
            if (baseAbilityCodes == null)
                return;

            foreach (var abilityCode in baseAbilityCodes)
            {
                var buffer = EntityManager.HasBuffer<BInitAbility>(request)
                    ? EntityManager.GetBuffer<BInitAbility>(request)
                    : EntityManager.AddBuffer<BInitAbility>(request);
                buffer.Add(new BInitAbility { AbilityCode = abilityCode });
            }
        }

        #endregion
    }
}
