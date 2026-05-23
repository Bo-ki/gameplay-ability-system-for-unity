using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC 轻量门面，仅持有 Entity 引用。非 ECS 代码通过它进入 2.0 ECS 数据主链。
    /// ECS 内部代码应直接使用 ISystem / SystemAPI 操作。
    /// </summary>
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
            if (!target.IsValid || gameplayEffectCode <= 0) return Entity.Null;
            return CreateApplyGameplayEffectRequest(gameplayEffectCode, target.Entity, level, null);
        }

        public Entity RequestGameplayEffectTo(
            int gameplayEffectCode,
            AbilitySystemFacade target,
            IReadOnlyList<BSetByCallerValue> setByCallerValues,
            int level = 0)
        {
            if (!target.IsValid || gameplayEffectCode <= 0) return Entity.Null;
            return CreateApplyGameplayEffectRequest(gameplayEffectCode, target.Entity, level, setByCallerValues);
        }

        public Entity RequestGameplayEffectToSelf(int gameplayEffectCode, int level = 0)
        {
            if (gameplayEffectCode <= 0) return Entity.Null;
            return CreateApplyGameplayEffectRequest(gameplayEffectCode, Entity, level, null);
        }

        public Entity RequestGameplayEffectToSelf(
            int gameplayEffectCode,
            IReadOnlyList<BSetByCallerValue> setByCallerValues,
            int level = 0)
        {
            if (gameplayEffectCode <= 0) return Entity.Null;
            return CreateApplyGameplayEffectRequest(gameplayEffectCode, Entity, level, setByCallerValues);
        }

        public Entity RemoveGameplayEffect(Entity gameplayEffect)
        {
            return CreateRemoveGameplayEffectRequest(gameplayEffect);
        }

        public void ClearGameplayEffects()
        {
            if (!EntityManager.HasBuffer<BGameplayEffect>(Entity)) return;

            var effects = EntityManager.GetBuffer<BGameplayEffect>(Entity);
            for (var i = effects.Length - 1; i >= 0; i--)
                CreateRemoveGameplayEffectRequest(effects[i].GameplayEffect);
        }

        #endregion

        private Entity CreateApplyGameplayEffectRequest(
            int gameplayEffectCode,
            Entity target,
            int level,
            IReadOnlyList<BSetByCallerValue> setByCallerValues)
        {
            if (!IsValid || EntityManager.HasComponent<CAscDestroying>(Entity))
                return Entity.Null;

            var request = GameplayEffectRequestWriter.Create(
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
                new CTargetDataHeader
                {
                    SourceAsc = Entity,
                    SourceAbility = Entity.Null,
                    Kind = target == Entity ? ETargetDataKind.Self : ETargetDataKind.Entity,
                });
            GameplayEffectRequestWriter.AddTarget(EntityManager, request, target);
            GameplayEffectRequestWriter.AddSetByCallerValues(EntityManager, request, setByCallerValues);
            return request;
        }

        private Entity CreateRemoveGameplayEffectRequest(Entity gameplayEffect)
        {
            if (gameplayEffect == Entity.Null || !EntityManager.Exists(gameplayEffect))
                return Entity.Null;

            var request = EntityManager.CreateEntity();
            EntityManager.SetName(request, $"RemoveGERequest_{gameplayEffect.Index}_{request.Index}");
            EntityManager.AddComponentData(request, new CRemoveGameplayEffectRequest
            {
                GameplayEffect = gameplayEffect,
            });
            return request;
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

        public bool HasAllTags(IEnumerable<int> tags)
        {
            return Observation.HasAllTags(tags);
        }

        public bool HasAnyTags(IEnumerable<int> tags)
        {
            return Observation.HasAnyTags(tags);
        }

        public void AddFixedTags(IEnumerable<int> tags)
        {
            if (tags == null) return;
            foreach (var tag in tags)
                AddFixedTag(tag);
        }

        public Entity AddFixedTag(int tag)
        {
            return CreateAscCommandRequest(new CAscCommandRequest
            {
                CommandType = EAscCommandType.AddFixedTag,
                TagCode = tag,
            });
        }

        public Entity KillFixedTag(int tag)
        {
            return CreateAscCommandRequest(new CAscCommandRequest
            {
                CommandType = EAscCommandType.RemoveFixedTag,
                TagCode = tag,
            });
        }

        public void KillFixedTags(IEnumerable<int> tags)
        {
            if (tags == null) return;
            foreach (var tag in tags)
                KillFixedTag(tag);
        }

        #endregion

        #region Attribute

        public float GetAttrCurrentValue(int attrSetCode, int attributeCode)
        {
            return Observation.GetAttributeCurrentValue(attrSetCode, attributeCode);
        }

        public bool TryGetAttributeCurrentValue(int attrSetCode, int attributeCode, out float value)
        {
            return Observation.TryGetAttributeCurrentValue(attrSetCode, attributeCode, out value);
        }

        public float GetAttrBaseValue(int attrSetCode, int attributeCode)
        {
            return Observation.GetAttributeBaseValue(attrSetCode, attributeCode);
        }

        public bool TryGetAttributeBaseValue(int attrSetCode, int attributeCode, out float value)
        {
            return Observation.TryGetAttributeBaseValue(attrSetCode, attributeCode, out value);
        }

        public Entity SetAttrBaseValue(int attrSetCode, int attributeCode, float value)
        {
            return CreateAscCommandRequest(new CAscCommandRequest
            {
                CommandType = EAscCommandType.SetAttributeBaseValue,
                AttrSetCode = attrSetCode,
                AttributeCode = attributeCode,
                AttributeValue = value,
            });
        }

        public float GetAttrCurrentValue(int attrCode)
        {
            return GetAttrCurrentValue(0, attrCode);
        }

        public bool TryGetAttributeCurrentValue(int attrCode, out float value)
        {
            return TryGetAttributeCurrentValue(0, attrCode, out value);
        }

        public Entity SetAttrBaseValue(int attrCode, float value)
        {
            return SetAttrBaseValue(0, attrCode, value);
        }

        public Entity AddAttribute(int code, float baseValue)
        {
            return AddAttribute(0, code, baseValue);
        }

        public Entity AddAttribute(int attrSetCode, int code, float baseValue)
        {
            return CreateAscCommandRequest(new CAscCommandRequest
            {
                CommandType = EAscCommandType.AddAttribute,
                AttrSetCode = attrSetCode,
                AttributeCode = code,
                AttributeValue = baseValue,
            });
        }

        private Entity CreateAscCommandRequest(CAscCommandRequest request)
        {
            if (!IsValid || EntityManager.HasComponent<CAscDestroying>(Entity))
                return Entity.Null;

            var requestEntity = EntityManager.CreateEntity();
            request.ASC = Entity;
            EntityManager.SetName(requestEntity, $"Asc{request.CommandType}Request_{Entity.Index}_{requestEntity.Index}");
            EntityManager.AddComponentData(requestEntity, request);
            return requestEntity;
        }

        #endregion

        #region Ability

        public Entity GrantAbility(int abilityCode)
        {
            return CreateAbilityCommandRequest(EAbilityCommandType.Grant, abilityCode, Entity.Null);
        }

        public Entity TryActivateAbility(int abilityCode)
        {
            return CreateAbilityCommandRequest(EAbilityCommandType.Activate, abilityCode, Entity.Null);
        }

        public Entity TryActivateAbility(int abilityCode, AbilitySystemFacade target)
        {
            return TryActivateAbility(abilityCode, target.Entity);
        }

        public Entity TryActivateAbility(int abilityCode, Entity targetAsc)
        {
            return CreateAbilityCommandRequest(EAbilityCommandType.Activate, abilityCode, targetAsc);
        }

        public Entity TryEndAbility(int abilityCode)
        {
            return CreateAbilityCommandRequest(EAbilityCommandType.End, abilityCode, Entity.Null);
        }

        public Entity TryCancelAbility(int abilityCode)
        {
            return CreateAbilityCommandRequest(EAbilityCommandType.Cancel, abilityCode, Entity.Null);
        }

        public bool IsAbilityActive(int abilityCode)
        {
            return Observation.IsAbilityActive(abilityCode);
        }

        public Entity RemoveAbility(int abilityCode)
        {
            return CreateAbilityCommandRequest(EAbilityCommandType.Remove, abilityCode, Entity.Null);
        }

        private Entity CreateAbilityCommandRequest(EAbilityCommandType commandType, int abilityCode, Entity targetAsc)
        {
            if (!IsValid || EntityManager.HasComponent<CAscDestroying>(Entity) || abilityCode <= 0)
                return Entity.Null;

            var request = EntityManager.CreateEntity();
            EntityManager.SetName(request, $"Ability{commandType}Request_{abilityCode}_{request.Index}");
            EntityManager.AddComponentData(request, new CAbilityCommandRequest
            {
                Owner = Entity,
                TargetAsc = targetAsc,
                AbilityCode = abilityCode,
                CommandType = commandType,
            });
            return request;
        }

        private void FillInitTags(Entity request, IEnumerable<int> baseTags)
        {
            if (baseTags == null) return;

            var tags = EntityManager.AddBuffer<BAscInitFixedTag>(request);
            foreach (var tag in baseTags)
                if (tag > 0)
                    tags.Add(new BAscInitFixedTag { TagCode = tag });
        }

        private void FillInitAttributes(Entity request, IEnumerable<AttrSetConfig> attrSets)
        {
            if (attrSets == null) return;

            var attributes = EntityManager.AddBuffer<BAscInitAttribute>(request);
            foreach (var attrSet in attrSets)
            {
                if (attrSet.Settings == null)
                    continue;

                foreach (var setting in attrSet.Settings)
                {
                    attributes.Add(new BAscInitAttribute
                    {
                        AttrSetCode = attrSet.Code,
                        AttributeCode = setting.Code,
                        BaseValue = setting.InitValue,
                        IsClampMin = setting.IsClampMin,
                        IsClampMax = setting.IsClampMax,
                        MinValue = setting.Min,
                        MaxValue = setting.Max,
                    });
                }
            }
        }

        private void FillInitAbilities(Entity request, IEnumerable<int> baseAbilityCodes)
        {
            if (baseAbilityCodes == null) return;

            var abilities = EntityManager.AddBuffer<BAscInitAbility>(request);
            foreach (var abilityCode in baseAbilityCodes)
                if (abilityCode > 0)
                    abilities.Add(new BAscInitAbility { AbilityCode = abilityCode });
        }

        #endregion

        #region Observation

        public int PresentationEventCount => Observation.PresentationEventCount;

        public int PeekPresentationEvents(GasPresentationEventView[] output)
        {
            return Observation.PeekPresentationEvents(output);
        }

        #endregion
    }

    /// <summary>
    /// Facade read side. It reads ECS authority as an observation view and never
    /// caches gameplay state or drains presentation outboxes.
    /// </summary>
    public readonly struct AbilitySystemObservation
    {
        private readonly Entity _entity;

        private static EntityManager EntityManager => GASManager.EntityManager;

        internal AbilitySystemObservation(Entity entity)
        {
            _entity = entity;
        }

        public bool IsValid => _entity != Entity.Null && EntityManager.Exists(_entity);

        public int GetLevel()
        {
            return TryGetLevel(out var level) ? level : 0;
        }

        public bool TryGetLevel(out int level)
        {
            level = 0;
            if (!IsValid || !EntityManager.HasComponent<CAscBasicData>(_entity))
                return false;

            level = EntityManager.GetComponentData<CAscBasicData>(_entity).Level;
            return true;
        }

        public bool HasTag(int tag)
        {
            if (!IsValid || !EntityManager.HasComponent<CTagMask>(_entity))
                return false;

            return TagHelper.TryGetDenseIndex(tag, out var denseIndex)
                   && EntityManager.GetComponentData<CTagMask>(_entity).HasTag(denseIndex);
        }

        public bool HasAllTags(IEnumerable<int> tags)
        {
            if (tags == null || !IsValid || !EntityManager.HasComponent<CTagMask>(_entity))
                return false;

            var mask = EntityManager.GetComponentData<CTagMask>(_entity);
            foreach (var tag in tags)
            {
                if (!TagHelper.TryGetDenseIndex(tag, out var denseIndex) || !mask.HasTag(denseIndex))
                    return false;
            }
            return true;
        }

        public bool HasAnyTags(IEnumerable<int> tags)
        {
            if (tags == null || !IsValid || !EntityManager.HasComponent<CTagMask>(_entity))
                return false;

            var mask = EntityManager.GetComponentData<CTagMask>(_entity);
            foreach (var tag in tags)
            {
                if (TagHelper.TryGetDenseIndex(tag, out var denseIndex) && mask.HasTag(denseIndex))
                    return true;
            }
            return false;
        }

        public float GetAttributeCurrentValue(int attrSetCode, int attributeCode)
        {
            return TryGetAttributeCurrentValue(attrSetCode, attributeCode, out var value) ? value : 0f;
        }

        public float GetAttributeBaseValue(int attrSetCode, int attributeCode)
        {
            return TryGetAttributeBaseValue(attrSetCode, attributeCode, out var value) ? value : 0f;
        }

        public bool TryGetAttributeCurrentValue(int attrSetCode, int attributeCode, out float value)
        {
            return TryGetAttributeValue(attrSetCode, attributeCode, current: true, out value);
        }

        public bool TryGetAttributeBaseValue(int attrSetCode, int attributeCode, out float value)
        {
            return TryGetAttributeValue(attrSetCode, attributeCode, current: false, out value);
        }

        public bool IsAbilityActive(int abilityCode)
        {
            var ability = FindAbility(abilityCode);
            if (ability == Entity.Null || !EntityManager.HasComponent<CAbilityRuntimeState>(ability))
                return false;

            var runtime = EntityManager.GetComponentData<CAbilityRuntimeState>(ability);
            return runtime.Phase is EAbilityPhase.Activating or EAbilityPhase.Active;
        }

        public int PresentationEventCount
        {
            get
            {
                return IsValid && EntityManager.HasBuffer<BPresentationEvent>(_entity)
                    ? EntityManager.GetBuffer<BPresentationEvent>(_entity).Length
                    : 0;
            }
        }

        public int PeekPresentationEvents(GasPresentationEventView[] output)
        {
            if (output == null
                || output.Length == 0
                || !IsValid
                || !EntityManager.HasBuffer<BPresentationEvent>(_entity))
            {
                return 0;
            }

            var events = EntityManager.GetBuffer<BPresentationEvent>(_entity);
            var count = output.Length < events.Length ? output.Length : events.Length;
            for (var i = 0; i < count; i++)
                output[i] = GasPresentationEventView.From(events[i]);

            return count;
        }

        private bool TryGetAttributeValue(int attrSetCode, int attributeCode, bool current, out float value)
        {
            value = 0f;
            if (!IsValid || !EntityManager.HasBuffer<BAttribute>(_entity))
                return false;

            var attributes = EntityManager.GetBuffer<BAttribute>(_entity);
            for (var i = 0; i < attributes.Length; i++)
            {
                if (!IsAttribute(attributes[i], attrSetCode, attributeCode))
                    continue;

                value = current ? attributes[i].CurrentValue : attributes[i].BaseValue;
                return true;
            }
            return false;
        }

        private Entity FindAbility(int abilityCode)
        {
            if (!IsValid || !EntityManager.HasBuffer<BGrantedAbility>(_entity))
                return Entity.Null;

            var abilities = EntityManager.GetBuffer<BGrantedAbility>(_entity);
            for (var i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i].AbilityEntity;
                if (!EntityManager.Exists(ability) || !EntityManager.HasComponent<CAbilityConfig>(ability))
                    continue;

                var config = EntityManager.GetComponentData<CAbilityConfig>(ability).Config;
                if (config.IsCreated && config.Value.Code == abilityCode)
                    return ability;
            }
            return Entity.Null;
        }

        private static bool IsAttribute(BAttribute attribute, int attrSetCode, int attributeCode)
        {
            return attribute.AttrSetCode == attrSetCode && attribute.Code == attributeCode;
        }
    }

    public struct GasPresentationEventView
    {
        public int Frame;
        public int Sequence;
        public EPresentationEventKind Kind;
        public EGameplayEventType GameplayEventType;
        public EGameplayCueEvent CueEvent;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public Entity GameplayEffect;
        public Entity SourceEntity;
        public Entity CueEntity;
        public CueSourceType CueSourceType;
        public int ContextId;
        public int EventCode;
        public int AttrSetCode;
        public int AttributeCode;
        public int TagIndex;
        public float Value;
        public float OldValue;
        public float NewValue;
        public float DamageAmount;
        public byte Flag;

        public static GasPresentationEventView From(BPresentationEvent evt)
        {
            return new GasPresentationEventView
            {
                Frame = evt.Frame,
                Sequence = evt.Sequence,
                Kind = evt.Kind,
                GameplayEventType = evt.GameplayEventType,
                CueEvent = evt.CueEvent,
                SourceAsc = evt.SourceAsc,
                TargetAsc = evt.TargetAsc,
                SourceAbility = evt.SourceAbility,
                GameplayEffect = evt.GameplayEffect,
                SourceEntity = evt.SourceEntity,
                CueEntity = evt.CueEntity,
                CueSourceType = evt.CueSourceType,
                ContextId = evt.ContextId,
                EventCode = evt.EventCode,
                AttrSetCode = evt.AttrSetCode,
                AttributeCode = evt.AttributeCode,
                TagIndex = evt.TagIndex,
                Value = evt.Value,
                OldValue = evt.OldValue,
                NewValue = evt.NewValue,
                DamageAmount = evt.DamageAmount,
                Flag = evt.Flag,
            };
        }
    }
}
