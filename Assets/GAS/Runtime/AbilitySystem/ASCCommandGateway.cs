using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    internal readonly struct ASCBoundaryCommandWriter
    {
        private readonly EntityManager _entityManager;
        private readonly Entity _ownerAsc;

        public ASCBoundaryCommandWriter(EntityManager entityManager, Entity ownerAsc)
        {
            _entityManager = entityManager;
            _ownerAsc = ownerAsc;
        }

        public bool IsValid => IsValidAsc(_ownerAsc) && !ASCEntityFactory.IsDestroying(_entityManager, _ownerAsc);

        public Entity AppendDestroy()
        {
            if (!IsValid || !TryGetBuffer(out DynamicBuffer<ASCDestroyCommandBuffer> commands))
                return Entity.Null;

            commands.Add(new ASCDestroyCommandBuffer
            {
                Requested = 1,
            });
            MarkAscCommandPending();
            return _ownerAsc;
        }

        public Entity AppendInit(
            IEnumerable<int> baseTags,
            IEnumerable<AttrSetConfig> attrSets,
            IEnumerable<int> baseAbilityCodes,
            int level)
        {
            if (!IsValid
                || !TryGetBuffer(out DynamicBuffer<ASCCommandBuffer> ascCommands)
                || !TryGetBuffer(out DynamicBuffer<AbilityCommandBuffer> abilityCommands))
            {
                return Entity.Null;
            }

            ascCommands.Add(new ASCCommandBuffer
            {
                Command = new ASCCommand
                {
                    CommandType = ASCCommandType.SetLevel,
                    Level = level,
                },
            });

            AppendInitTags(ascCommands, baseTags);
            AppendInitAttributes(ascCommands, attrSets);
            AppendInitAbilities(abilityCommands, baseAbilityCodes);
            MarkAscCommandPending();
            return _ownerAsc;
        }

        public Entity AppendGameplayEffectApply(
            int gameplayEffectCode,
            Entity targetAsc,
            int level,
            IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues)
        {
            if (!IsValid || !IsValidAsc(targetAsc))
                return Entity.Null;

            var accepted = GameplayEffectRequestWriter.TryAppendSimpleInstantCommand(
                _entityManager,
                new GEApplyRequestComponent
                {
                    SourceAsc = _ownerAsc,
                    SourceAbility = Entity.Null,
                    Instigator = _ownerAsc,
                    Causer = _ownerAsc,
                    GameplayEffectCode = gameplayEffectCode,
                    Level = level,
                },
                targetAsc,
                targetAsc == _ownerAsc ? ETargetDataKind.Self : ETargetDataKind.Entity,
                setByCallerValues,
                GEEffectCommandSource.RuntimeBoundary);

            return accepted && EffectCommandSpecStream.TryGetSingleton(_entityManager, out var streamEntity)
                ? streamEntity
                : Entity.Null;
        }

        public Entity AppendGameplayEffectRemove(int gameplayEffectCode)
        {
            if (!IsValid || !TryGetBuffer(out DynamicBuffer<GERemoveCommandBuffer> commands))
                return Entity.Null;

            commands.Add(new GERemoveCommandBuffer
            {
                GameplayEffectCode = gameplayEffectCode,
            });
            MarkGameplayEffectRemovePending();
            return _ownerAsc;
        }

        public Entity AppendAscCommand(in ASCCommand command)
        {
            if (!IsValid || !TryGetBuffer(out DynamicBuffer<ASCCommandBuffer> commands))
                return Entity.Null;

            commands.Add(new ASCCommandBuffer
            {
                Command = command,
            });
            MarkAscCommandPending();
            return _ownerAsc;
        }

        public Entity AppendAbilityCommand(
            int abilityCode,
            Entity targetAsc,
            EAbilityCommandType commandType)
        {
            if (!IsValid
                || targetAsc != Entity.Null && !IsValidAsc(targetAsc)
                || !TryGetBuffer(out DynamicBuffer<AbilityCommandBuffer> commands))
            {
                return Entity.Null;
            }

            commands.Add(new AbilityCommandBuffer
            {
                Command = new AbilityCommand
                {
                    Owner = _ownerAsc,
                    AbilityCode = abilityCode,
                    TargetAsc = targetAsc,
                    CommandType = commandType,
                },
            });
            MarkAscCommandPending();
            return _ownerAsc;
        }

        public int PresentationEventCount
        {
            get
            {
                if (!IsValidAsc(_ownerAsc) || !_entityManager.HasBuffer<PresentationEventBuffer>(_ownerAsc))
                    return 0;

                return _entityManager.GetBuffer<PresentationEventBuffer>(_ownerAsc).Length;
            }
        }

        public int CopyPresentationEvents(GasPresentationEventView[] output)
        {
            if (output == null
                || output.Length == 0
                || !IsValidAsc(_ownerAsc)
                || !_entityManager.HasBuffer<PresentationEventBuffer>(_ownerAsc))
            {
                return 0;
            }

            var events = _entityManager.GetBuffer<PresentationEventBuffer>(_ownerAsc);
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

        private bool TryGetBuffer<T>(out DynamicBuffer<T> buffer)
            where T : unmanaged, IBufferElementData
        {
            if (!_entityManager.HasBuffer<T>(_ownerAsc))
            {
                buffer = default;
                return false;
            }

            buffer = _entityManager.GetBuffer<T>(_ownerAsc);
            return true;
        }

        private bool IsValidAsc(Entity asc)
        {
            return asc != Entity.Null && _entityManager.Exists(asc);
        }

        private void MarkAscCommandPending()
        {
            if (_entityManager.HasComponent<ASCCommandPendingComponent>(_ownerAsc))
                _entityManager.SetComponentEnabled<ASCCommandPendingComponent>(_ownerAsc, true);
        }

        private void MarkGameplayEffectRemovePending()
        {
            if (_entityManager.HasComponent<GERemoveCommandPendingComponent>(_ownerAsc))
                _entityManager.SetComponentEnabled<GERemoveCommandPendingComponent>(_ownerAsc, true);
        }

        private static void AppendInitTags(DynamicBuffer<ASCCommandBuffer> commands, IEnumerable<int> baseTags)
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

        private static void AppendInitAttributes(
            DynamicBuffer<ASCCommandBuffer> commands,
            IEnumerable<AttrSetConfig> attrSets)
        {
            if (attrSets == null)
                return;

            foreach (var attrSet in attrSets)
            {
                if (attrSet.Settings == null)
                    continue;

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

        private void AppendInitAbilities(
            DynamicBuffer<AbilityCommandBuffer> commands,
            IEnumerable<int> baseAbilityCodes)
        {
            if (baseAbilityCodes == null)
                return;

            foreach (var abilityCode in baseAbilityCodes)
            {
                commands.Add(new AbilityCommandBuffer
                {
                    Command = new AbilityCommand
                    {
                        Owner = _ownerAsc,
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
    }

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

        private ASCBoundaryCommandWriter CommandWriter => new ASCBoundaryCommandWriter(EntityManager, Entity);

        #region Factory

        public static ASCCommandGateway Create()
        {
            return new ASCCommandGateway(ASCEntityFactory.Create(EntityManager));
        }

        public Entity Dispose()
        {
            return IsValid ? CommandWriter.AppendDestroy() : Entity.Null;
        }

        public Entity Init(AbilitySystemConfig config)
        {
            return Init(config.BaseTags, config.AttrSets, config.BaseAbilityCodes, config.Level);
        }

        public Entity Init(IEnumerable<int> baseTags, IEnumerable<AttrSetConfig> attrSets,
            IEnumerable<int> baseAbilityCodes, int level = 1)
        {
            return IsValid
                ? CommandWriter.AppendInit(baseTags, attrSets, baseAbilityCodes, level)
                : Entity.Null;
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
            return IsValid
                ? CommandWriter.AppendGameplayEffectApply(gameplayEffectCode, target, level, setByCallerValues)
                : Entity.Null;
        }

        private Entity RequestRemoveGameplayEffects(int gameplayEffectCode)
        {
            return IsValid ? CommandWriter.AppendGameplayEffectRemove(gameplayEffectCode) : Entity.Null;
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
            return IsValid
                ? CommandWriter.AppendAbilityCommand(abilityCode, target, commandType)
                : Entity.Null;
        }

        #endregion

        #region Internal

        private Entity AppendAscCommand(ASCCommand command)
        {
            return IsValid ? CommandWriter.AppendAscCommand(in command) : Entity.Null;
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
                return IsValid ? CommandWriter.PresentationEventCount : 0;
            }
        }

        public int PeekPresentationEvents(GasPresentationEventView[] output)
        {
            if (output == null
                || output.Length == 0
                || !IsValid)
            {
                return 0;
            }

            return CommandWriter.CopyPresentationEvents(output);
        }

    }
}
