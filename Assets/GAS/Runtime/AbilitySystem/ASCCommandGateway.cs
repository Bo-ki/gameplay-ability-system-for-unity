using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    public readonly struct ASCCommandGateway
    {
        public readonly Entity Entity;

        private static EntityManager EntityManager => GASManager.EntityManager;
        private static bool IsRuntimeReady => GASManager.ExWorld != null
                                             && GASManager.ExWorld.IsCreated
                                             && GASManager.IsInitialized;

        public ASCCommandGateway(Entity entity)
        {
            Entity = entity;
        }

        public bool IsValid => IsRuntimeReady
                               && Entity != Entity.Null
                               && EntityManager.Exists(Entity);

        public GameObject GameObject => IsValid ? EntityHelper.GetGameObjectFromEntity(Entity) : null;

        public ASCReadModel ReadModel => new ASCReadModel(Entity);

        #region Factory

        public static ASCCommandGateway Create()
        {
            return new ASCCommandGateway(ASCEntityFactory.Create(EntityManager));
        }

        public Entity Dispose()
        {
            if (!IsValid)
                return Entity.Null;

            if (ASCEntityFactory.IsDestroying(EntityManager, Entity))
                return Entity.Null;

            var request = EntityManager.CreateEntity(GASRuntimeEntityArchetypes.ASCDestroyRequest(EntityManager));
            EntityManager.SetName(request, $"AscDestroyRequest_{Entity.Index}_{request.Index}");
            EntityManager.SetComponentData(request, new ASCDestroyRequestComponent
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
            if (!IsValid || ASCEntityFactory.IsDestroying(EntityManager, Entity))
                return Entity.Null;

            var request = EntityManager.CreateEntity(GASRuntimeEntityArchetypes.ASCInitializeRequest(EntityManager));
            EntityManager.SetName(request, $"AscInitializeRequest_{Entity.Index}_{request.Index}");
            EntityManager.SetComponentData(request, new ASCInitializeRequestComponent
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

        public Entity RequestGameplayEffectTo(int gameplayEffectCode, ASCCommandGateway target, int level = 0)
        {
            return ApplyGameplayEffectToTarget(gameplayEffectCode, target.Entity, level, null);
        }

        public Entity RequestGameplayEffectTo(
            int gameplayEffectCode,
            ASCCommandGateway target,
            IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues,
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
            IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues,
            int level = 0)
        {
            return ApplyGameplayEffectToTarget(gameplayEffectCode, Entity, level, setByCallerValues);
        }

        public Entity RemoveGameplayEffect(Entity gameplayEffect)
        {
            if (!IsValid || gameplayEffect == Entity.Null || !EntityManager.Exists(gameplayEffect))
                return Entity.Null;

            if (EntityManager.HasComponent<GEEffectSpecComponent>(gameplayEffect))
            {
                var spec = EntityManager.GetComponentData<GEEffectSpecComponent>(gameplayEffect);
                return RequestRemoveGameplayEffects(spec.GameplayEffectCode);
            }

            if (EntityManager.HasComponent<GEPrototypeComponent>(gameplayEffect))
            {
                var prototype = EntityManager.GetComponentData<GEPrototypeComponent>(gameplayEffect);
                if (prototype.GameplayEffectCode > 0)
                    return RequestRemoveGameplayEffects(prototype.GameplayEffectCode);
            }

            return Entity.Null;
        }

        public Entity RemoveGameplayEffects(int gameplayEffectCode)
        {
            return RequestRemoveGameplayEffects(gameplayEffectCode);
        }

        public Entity ClearGameplayEffects()
        {
            return RequestRemoveGameplayEffects(0);
        }

        #endregion

        private Entity ApplyGameplayEffectToTarget(
            int gameplayEffectCode,
            Entity target,
            int level,
            IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues)
        {
            if (!IsValid || ASCEntityFactory.IsDestroying(EntityManager, Entity))
                return Entity.Null;
            if (target == Entity.Null || !EntityManager.Exists(target))
                return Entity.Null;

            var accepted = GameplayEffectRequestWriter.TryAppendSimpleInstantCommand(
                EntityManager,
                new GEApplyRequestComponent
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
            return accepted && EffectCommandSpecStream.TryGetSingleton(EntityManager, out var streamEntity)
                ? streamEntity
                : Entity.Null;
        }

        private Entity RequestRemoveGameplayEffects(int gameplayEffectCode)
        {
            if (!IsValid || ASCEntityFactory.IsDestroying(EntityManager, Entity))
                return Entity.Null;

            var request = EntityManager.CreateEntity(GASRuntimeEntityArchetypes.GERemoveRequest(EntityManager));
            EntityManager.SetName(
                request,
                gameplayEffectCode > 0
                    ? $"GERemove_{gameplayEffectCode}_{request.Index}"
                    : $"GEClear_{Entity.Index}_{request.Index}");
            EntityManager.SetComponentData(request, new GERemoveRequestComponent
            {
                TargetAsc = Entity,
                GameplayEffectCode = gameplayEffectCode,
            });
            return request;
        }

        #region BasicData

        public Entity SetLevel(int level)
        {
            return CreateAscCommandRequest(new ASCCommandRequestComponent
            {
                CommandType = ASCCommandType.SetLevel,
                Level = level,
            });
        }

        public int GetLevel()
        {
            return ReadModel.GetLevel();
        }

        public bool TryGetLevel(out int level)
        {
            return ReadModel.TryGetLevel(out level);
        }

        #endregion

        #region Tag

        public bool HasTag(int tag)
        {
            return ReadModel.HasTag(tag);
        }

        public bool TryGetTagMask(out TagMaskComponent mask)
        {
            return ReadModel.TryGetTagMask(out mask);
        }

        #endregion

        #region Attribute

        public float GetAttributeValue(int attrSetCode, int attrCode)
        {
            return ReadModel.GetAttributeValue(attrSetCode, attrCode);
        }

        public bool TryGetAttributeValue(int attrSetCode, int attrCode, out float value)
        {
            return ReadModel.TryGetAttributeValue(attrSetCode, attrCode, out value);
        }

        #endregion

        #region Ability

        public Entity TryActivateAbility(int abilityCode)
        {
            return TryActivateAbility(abilityCode, Entity.Null);
        }

        public Entity TryActivateAbility(int abilityCode, ASCCommandGateway target)
        {
            return TryActivateAbility(abilityCode, target.Entity);
        }

        public Entity TryActivateAbility(int abilityCode, Entity target)
        {
            return CreateAbilityCommandRequest(abilityCode, target, EAbilityCommandType.Activate, "Activate");
        }

        public Entity CancelAbility(int abilityCode)
        {
            return CreateAbilityCommandRequest(abilityCode, Entity.Null, EAbilityCommandType.Cancel, "Cancel");
        }

        private Entity CreateAbilityCommandRequest(
            int abilityCode,
            Entity target,
            EAbilityCommandType commandType,
            string name)
        {
            if (!IsValid || ASCEntityFactory.IsDestroying(EntityManager, Entity))
                return Entity.Null;
            if (target != Entity.Null && !EntityManager.Exists(target))
                return Entity.Null;

            var request = EntityManager.CreateEntity(GASRuntimeEntityArchetypes.AbilityCommandRequest(EntityManager));
            EntityManager.SetName(request, $"AbilityCommand_{name}_{request.Index}");
            EntityManager.SetComponentData(request, new AbilityCommandRequestComponent
            {
                Owner = Entity,
                AbilityCode = abilityCode,
                TargetAsc = target,
                CommandType = commandType,
            });
            return request;
        }

        #endregion

        #region Internal

        private Entity CreateAscCommandRequest(ASCCommandRequestComponent command)
        {
            if (!IsValid || ASCEntityFactory.IsDestroying(EntityManager, Entity))
                return Entity.Null;

            var request = EntityManager.CreateEntity(GASRuntimeEntityArchetypes.ASCCommandRequest(EntityManager));
            EntityManager.SetName(request, $"AscCommand_{command.CommandType}_{request.Index}");
            command.ASC = Entity;
            EntityManager.SetComponentData(request, command);
            return request;
        }

        private void FillInitTags(Entity request, IEnumerable<int> baseTags)
        {
            if (baseTags == null)
                return;

            var buffer = EntityManager.GetBuffer<ASCInitializeFixedTagBuffer>(request);
            foreach (var tag in baseTags)
                buffer.Add(new ASCInitializeFixedTagBuffer { TagCode = tag });
        }

        private void FillInitAttributes(Entity request, IEnumerable<AttrSetConfig> attrSets)
        {
            if (attrSets == null)
                return;

            var buffer = EntityManager.GetBuffer<ASCInitializeAttributeBuffer>(request);
            foreach (var attrSet in attrSets)
            {
                if (attrSet.Settings == null) continue;
                foreach (var setting in attrSet.Settings)
                {
                    buffer.Add(new ASCInitializeAttributeBuffer
                    {
                        AttrSetCode = attrSet.Code,
                        AttributeCode = setting.Code,
                        BaseValue = setting.InitValue,
                        MaxValue = setting.Max,
                        MinValue = setting.Min,
                        IsClampMin = setting.IsClampMin,
                        IsClampMax = setting.IsClampMax,
                    });
                }
            }
        }

        private void FillInitAbilities(Entity request, IEnumerable<int> baseAbilityCodes)
        {
            if (baseAbilityCodes == null)
                return;

            var buffer = EntityManager.GetBuffer<ASCInitializeAbilityBuffer>(request);
            foreach (var abilityCode in baseAbilityCodes)
                buffer.Add(new ASCInitializeAbilityBuffer { AbilityCode = abilityCode });
        }

        #endregion

        public Entity TryEndAbility(int abilityCode)
        {
            return CreateAbilityCommandRequest(abilityCode, Entity.Null, EAbilityCommandType.End, "End");
        }

        public Entity TryCancelAbility(int abilityCode)
        {
            return CancelAbility(abilityCode);
        }

        public Entity RemoveAbility(int abilityCode)
        {
            return CreateAbilityCommandRequest(abilityCode, Entity.Null, EAbilityCommandType.Remove, "Remove");
        }

        public bool TryGetAttributeCurrentValue(int attrSetCode, int attrCode, out float value)
        {
            return ReadModel.TryGetAttributeCurrentValue(attrSetCode, attrCode, out value);
        }

        public float GetAttrCurrentValue(int attrSetCode, int attrCode)
        {
            return TryGetAttributeCurrentValue(attrSetCode, attrCode, out var value) ? value : 0f;
        }

        public bool TryGetAttributeBaseValue(int attrSetCode, int attrCode, out float value)
        {
            return ReadModel.TryGetAttributeValue(attrSetCode, attrCode, out value);
        }

        public float GetAttrBaseValue(int attrSetCode, int attrCode)
        {
            return TryGetAttributeBaseValue(attrSetCode, attrCode, out var value) ? value : 0f;
        }

        public int PresentationEventCount
        {
            get
            {
                if (!IsValid || !EntityManager.HasBuffer<PresentationEventBuffer>(Entity))
                    return 0;

                return EntityManager.GetBuffer<PresentationEventBuffer>(Entity).Length;
            }
        }

        public int PeekPresentationEvents(GasPresentationEventView[] output)
        {
            if (output == null
                || output.Length == 0
                || !IsValid
                || !EntityManager.HasBuffer<PresentationEventBuffer>(Entity))
            {
                return 0;
            }

            var events = EntityManager.GetBuffer<PresentationEventBuffer>(Entity);
            var count = events.Length < output.Length ? events.Length : output.Length;
            for (var i = 0; i < count; i++)
            {
                output[i] = new GasPresentationEventView
                {
                    Event = events[i],
                };
            }

            return count;
        }

    }
}
