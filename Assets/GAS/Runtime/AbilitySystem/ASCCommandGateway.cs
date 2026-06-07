using System.Collections.Generic;
using Unity.Entities;

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

        public bool AppendDestroy()
        {
            if (!IsValid || !TryGetBuffer(out DynamicBuffer<ASCDestroyCommandBuffer> commands))
                return false;

            commands.Add(new ASCDestroyCommandBuffer
            {
                Requested = 1,
            });
            MarkAscDestroying();
            MarkAscCommandPending();
            return true;
        }

        public bool AppendInit(
            IEnumerable<int> baseTags,
            IEnumerable<AttrSetConfig> attrSets,
            IEnumerable<int> baseAbilityCodes,
            int level)
        {
            if (!IsValid
                || !TryGetBuffer(out DynamicBuffer<ASCCommandBuffer> ascCommands)
                || !TryGetBuffer(out DynamicBuffer<AbilityCommandBuffer> abilityCommands))
            {
                return false;
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
            AppendInitAbilities(abilityCommands, baseAbilityCodes, _ownerAsc);
            MarkAscCommandPending();
            return true;
        }

        public bool AppendGameplayEffectApply(
            int gameplayEffectCode,
            Entity targetAsc,
            int level,
            IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues)
        {
            if (!IsValid || !IsValidAsc(targetAsc))
                return false;

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

            return accepted;
        }

        public bool AppendGameplayEffectRemove(int gameplayEffectCode)
        {
            if (!IsValid || !TryGetBuffer(out DynamicBuffer<GERemoveCommandBuffer> commands))
                return false;

            commands.Add(new GERemoveCommandBuffer
            {
                GameplayEffectCode = gameplayEffectCode,
            });
            MarkGameplayEffectRemovePending();
            return true;
        }

        public bool AppendAscCommand(in ASCCommand command)
        {
            if (!IsValid || !TryGetBuffer(out DynamicBuffer<ASCCommandBuffer> commands))
                return false;

            commands.Add(new ASCCommandBuffer
            {
                Command = command,
            });
            MarkAscCommandPending();
            return true;
        }

        public bool AppendAbilityCommand(
            int abilityCode,
            Entity targetAsc,
            EAbilityCommandType commandType)
        {
            if (!IsValid
                || targetAsc != Entity.Null && !IsValidAsc(targetAsc)
                || !TryGetBuffer(out DynamicBuffer<AbilityCommandBuffer> commands))
            {
                return false;
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
            return true;
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

        private void MarkAscDestroying()
        {
            if (_entityManager.HasComponent<ASCDestroyingComponent>(_ownerAsc))
                _entityManager.SetComponentEnabled<ASCDestroyingComponent>(_ownerAsc, true);
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

        private static void AppendInitAbilities(
            DynamicBuffer<AbilityCommandBuffer> commands,
            IEnumerable<int> baseAbilityCodes,
            Entity ownerAsc)
        {
            if (baseAbilityCodes == null)
                return;

            foreach (var abilityCode in baseAbilityCodes)
            {
                commands.Add(new AbilityCommandBuffer
                {
                    Command = new AbilityCommand
                    {
                        Owner = ownerAsc,
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

    public readonly struct ASCCommandPort
    {
        private readonly EntityManager _entityManager;
        private readonly Entity _entity;

        internal ASCCommandPort(EntityManager entityManager, Entity entity)
        {
            _entityManager = entityManager;
            _entity = entity;
        }

        public bool IsValid => IsRuntimeReady(_entityManager)
                               && _entity != Entity.Null
                               && _entityManager.Exists(_entity);

        public ASCHandle Handle => new ASCHandle(_entity);

        internal Entity RuntimeEntity => _entity;

        private ASCBoundaryCommandWriter CommandWriter => new ASCBoundaryCommandWriter(_entityManager, _entity);

        #region Factory

        public static ASCCommandPort Create(EntityManager entityManager)
        {
            if (!IsRuntimeReady(entityManager))
                return default;

            return new ASCCommandPort(entityManager, ASCEntityFactory.Create(entityManager));
        }

        public static ASCCommandPort Create(EntityManager entityManager, EntityArchetype ascArchetype)
        {
            if (!IsRuntimeReady(entityManager) || !ascArchetype.Valid)
                return default;

            var asc = ASCEntityFactory.Create(entityManager, ascArchetype);
            return asc == Entity.Null
                ? default
                : new ASCCommandPort(entityManager, asc);
        }

        public bool RequestDestroy()
        {
            return IsValid && CommandWriter.AppendDestroy();
        }

        public bool RequestInitialize(AbilitySystemConfig config)
        {
            return RequestInitialize(config.BaseTags, config.AttrSets, config.BaseAbilityCodes, config.Level);
        }

        public bool RequestInitialize(IEnumerable<int> baseTags, IEnumerable<AttrSetConfig> attrSets,
            IEnumerable<int> baseAbilityCodes, int level = 1)
        {
            return IsValid && CommandWriter.AppendInit(baseTags, attrSets, baseAbilityCodes, level);
        }

        #endregion

        #region GameplayEffect

        public bool RequestGameplayEffectTo(int gameplayEffectCode, ASCHandle targetAsc, int level = 0)
        {
            return ApplyGameplayEffectToTarget(gameplayEffectCode, targetAsc.RuntimeEntity, level, null);
        }

        public bool RequestGameplayEffectTo(
            int gameplayEffectCode,
            ASCHandle targetAsc,
            IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues,
            int level = 0)
        {
            return ApplyGameplayEffectToTarget(gameplayEffectCode, targetAsc.RuntimeEntity, level, setByCallerValues);
        }

        public bool RequestRemoveGameplayEffects(int gameplayEffectCode)
        {
            return AppendGameplayEffectRemoveCommand(gameplayEffectCode);
        }

        public bool RequestClearGameplayEffects()
        {
            return AppendGameplayEffectRemoveCommand(0);
        }

        #endregion

        private bool ApplyGameplayEffectToTarget(
            int gameplayEffectCode,
            Entity target,
            int level,
            IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues)
        {
            return IsValid && CommandWriter.AppendGameplayEffectApply(
                gameplayEffectCode,
                target,
                level,
                setByCallerValues);
        }

        private bool AppendGameplayEffectRemoveCommand(int gameplayEffectCode)
        {
            return IsValid && CommandWriter.AppendGameplayEffectRemove(gameplayEffectCode);
        }

        #region BasicData

        public bool RequestSetLevel(int level)
        {
            return AppendAscCommand(new ASCCommand
            {
                CommandType = ASCCommandType.SetLevel,
                Level = level,
            });
        }

        #endregion

        #region Ability

        public bool RequestActivateAbility(int abilityCode, ASCHandle target)
        {
            return AppendAbilityCommand(abilityCode, target.RuntimeEntity, EAbilityCommandType.Activate);
        }

        public bool RequestCancelAbility(int abilityCode)
        {
            return AppendAbilityCommand(abilityCode, Entity.Null, EAbilityCommandType.Cancel);
        }

        private bool AppendAbilityCommand(
            int abilityCode,
            Entity target,
            EAbilityCommandType commandType)
        {
            return IsValid && CommandWriter.AppendAbilityCommand(abilityCode, target, commandType);
        }

        #endregion

        #region Internal

        private bool AppendAscCommand(ASCCommand command)
        {
            return IsValid && CommandWriter.AppendAscCommand(in command);
        }

        #endregion

        public bool RequestEndAbility(int abilityCode)
        {
            return AppendAbilityCommand(abilityCode, Entity.Null, EAbilityCommandType.End);
        }

        public bool RequestRemoveAbility(int abilityCode)
        {
            return AppendAbilityCommand(abilityCode, Entity.Null, EAbilityCommandType.Remove);
        }

        internal bool TrySetComponentData<T>(T component)
            where T : unmanaged, IComponentData
        {
            if (!IsValid || !_entityManager.HasComponent<T>(_entity))
                return false;

            _entityManager.SetComponentData(_entity, component);
            return true;
        }

        private static bool IsRuntimeReady(EntityManager entityManager)
        {
            return entityManager.World != null && entityManager.World.IsCreated;
        }

    }
}
