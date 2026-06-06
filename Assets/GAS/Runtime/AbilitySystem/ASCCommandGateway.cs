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

            if (!EntityManager.HasBuffer<ASCDestroyCommandBuffer>(Entity))
                return Entity.Null;

            EntityManager.GetBuffer<ASCDestroyCommandBuffer>(Entity).Add(new ASCDestroyCommandBuffer
            {
                Requested = 1,
            });
            MarkAscCommandPending();
            return Entity;
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

            if (!EntityManager.HasBuffer<ASCCommandBuffer>(Entity)
                || !EntityManager.HasBuffer<AbilityCommandBuffer>(Entity))
            {
                return Entity.Null;
            }

            var ascCommands = EntityManager.GetBuffer<ASCCommandBuffer>(Entity);
            ascCommands.Add(new ASCCommandBuffer
            {
                Command = new ASCCommand
                {
                    CommandType = ASCCommandType.SetLevel,
                    Level = level,
                },
            });

            FillInitTags(ascCommands, baseTags);
            FillInitAttributes(ascCommands, attrSets);
            FillInitAbilities(EntityManager.GetBuffer<AbilityCommandBuffer>(Entity), baseAbilityCodes);
            MarkAscCommandPending();

            return Entity;
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

            if (!EntityManager.HasBuffer<GERemoveCommandBuffer>(Entity))
                return Entity.Null;

            EntityManager.GetBuffer<GERemoveCommandBuffer>(Entity).Add(new GERemoveCommandBuffer
            {
                GameplayEffectCode = gameplayEffectCode,
            });
            MarkGameplayEffectRemovePending();
            return Entity;
        }

        #region BasicData

        public Entity SetLevel(int level)
        {
            return AppendAscCommand(new ASCCommand
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
            return AppendAbilityCommand(abilityCode, target, EAbilityCommandType.Activate);
        }

        public Entity CancelAbility(int abilityCode)
        {
            return AppendAbilityCommand(abilityCode, Entity.Null, EAbilityCommandType.Cancel);
        }

        private Entity AppendAbilityCommand(
            int abilityCode,
            Entity target,
            EAbilityCommandType commandType)
        {
            if (!IsValid || ASCEntityFactory.IsDestroying(EntityManager, Entity))
                return Entity.Null;
            if (target != Entity.Null && !EntityManager.Exists(target))
                return Entity.Null;
            if (!EntityManager.HasBuffer<AbilityCommandBuffer>(Entity))
                return Entity.Null;

            EntityManager.GetBuffer<AbilityCommandBuffer>(Entity).Add(new AbilityCommandBuffer
            {
                Command = new AbilityCommand
                {
                    Owner = Entity,
                    AbilityCode = abilityCode,
                    TargetAsc = target,
                    CommandType = commandType,
                },
            });
            MarkAscCommandPending();
            return Entity;
        }

        #endregion

        #region Internal

        private Entity AppendAscCommand(ASCCommand command)
        {
            if (!IsValid || ASCEntityFactory.IsDestroying(EntityManager, Entity))
                return Entity.Null;
            if (!EntityManager.HasBuffer<ASCCommandBuffer>(Entity))
                return Entity.Null;

            EntityManager.GetBuffer<ASCCommandBuffer>(Entity).Add(new ASCCommandBuffer
            {
                Command = command,
            });
            MarkAscCommandPending();
            return Entity;
        }

        private void FillInitTags(DynamicBuffer<ASCCommandBuffer> commands, IEnumerable<int> baseTags)
        {
            if (baseTags == null)
                return;

            foreach (var tag in baseTags)
            {
                if (!TryCreateFixedTagCommand(tag, ASCCommandType.AddFixedTag, out var command))
                    continue;

                commands.Add(new ASCCommandBuffer
                {
                    Command = command,
                });
            }
        }

        private void FillInitAttributes(DynamicBuffer<ASCCommandBuffer> commands, IEnumerable<AttrSetConfig> attrSets)
        {
            if (attrSets == null)
                return;

            foreach (var attrSet in attrSets)
            {
                if (attrSet.Settings == null) continue;
                foreach (var setting in attrSet.Settings)
                {
                    commands.Add(new ASCCommandBuffer
                    {
                        Command = new ASCCommand
                        {
                            CommandType = ASCCommandType.AddAttribute,
                            AttrSetCode = attrSet.Code,
                            AttributeCode = setting.Code,
                            AttributeValue = setting.InitValue,
                            MaxValue = setting.Max,
                            MinValue = setting.Min,
                            IsClampMin = setting.IsClampMin,
                            IsClampMax = setting.IsClampMax,
                        },
                    });
                }
            }
        }

        private void FillInitAbilities(DynamicBuffer<AbilityCommandBuffer> commands, IEnumerable<int> baseAbilityCodes)
        {
            if (baseAbilityCodes == null)
                return;

            foreach (var abilityCode in baseAbilityCodes)
            {
                commands.Add(new AbilityCommandBuffer
                {
                    Command = new AbilityCommand
                    {
                        Owner = Entity,
                        AbilityCode = abilityCode,
                        CommandType = EAbilityCommandType.Grant,
                    },
                });
            }
        }

        private static bool TryCreateFixedTagCommand(int tagCode, ASCCommandType commandType, out ASCCommand command)
        {
            command = default;
            if (!TagHelper.TryGetDenseIndex(tagCode, out var sourceTagIndex))
                return false;

            var tagMask = new TagMaskComponent();
            if (!TagHelper.TryAddTagToMask(ref tagMask, tagCode, includeParents: true) || tagMask.IsEmpty)
                return false;

            command = new ASCCommand
            {
                CommandType = commandType,
                TagSourceIndex = sourceTagIndex,
                TagMask = tagMask,
            };
            return true;
        }

        private void MarkAscCommandPending()
        {
            if (EntityManager.HasComponent<ASCCommandPendingComponent>(Entity))
                EntityManager.SetComponentEnabled<ASCCommandPendingComponent>(Entity, true);
        }

        private void MarkGameplayEffectRemovePending()
        {
            if (EntityManager.HasComponent<GERemoveCommandPendingComponent>(Entity))
                EntityManager.SetComponentEnabled<GERemoveCommandPendingComponent>(Entity, true);
        }

        #endregion

        public Entity TryEndAbility(int abilityCode)
        {
            return AppendAbilityCommand(abilityCode, Entity.Null, EAbilityCommandType.End);
        }

        public Entity TryCancelAbility(int abilityCode)
        {
            return CancelAbility(abilityCode);
        }

        public Entity RemoveAbility(int abilityCode)
        {
            return AppendAbilityCommand(abilityCode, Entity.Null, EAbilityCommandType.Remove);
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
