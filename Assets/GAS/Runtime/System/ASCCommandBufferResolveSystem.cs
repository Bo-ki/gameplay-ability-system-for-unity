using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateBefore(typeof(AbilityTryActivateSystem))]
    public partial struct ASCCommandBufferResolveSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<ASCCommandPendingComponent>(),
                    ComponentType.ReadWrite<ASCIdentityComponent>(),
                    ComponentType.ReadWrite<TagMaskComponent>(),
                    ComponentType.ReadWrite<TagFixedMaskComponent>(),
                    ComponentType.ReadWrite<ASCCommandBuffer>(),
                    ComponentType.ReadWrite<AbilityCommandBuffer>(),
                    ComponentType.ReadWrite<ASCDestroyCommandBuffer>(),
                    ComponentType.ReadWrite<AttributeValueBuffer>(),
                    ComponentType.ReadWrite<AbilitySlotBuffer>(),
                    ComponentType.ReadWrite<TagFixedSourceBuffer>(),
                    ComponentType.ReadOnly<TagTemporarySourceBuffer>(),
                },
            });
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var frame = SystemAPI.TryGetSingleton<GlobalTimer>(out var timer)
                ? timer.Frame
                : 0;
            var catalog = SystemAPI.TryGetSingleton<GASDefinitionCatalogComponent>(out var catalogComponent)
                ? catalogComponent.Catalog
                : default;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new ASCCommandBufferResolveJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                IdentityTypeHandle = SystemAPI.GetComponentTypeHandle<ASCIdentityComponent>(),
                TagMaskTypeHandle = SystemAPI.GetComponentTypeHandle<TagMaskComponent>(),
                FixedTagMaskTypeHandle = SystemAPI.GetComponentTypeHandle<TagFixedMaskComponent>(),
                AscCommandBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ASCCommandBuffer>(),
                AbilityCommandBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AbilityCommandBuffer>(),
                DestroyCommandBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ASCDestroyCommandBuffer>(),
                AttributeValueBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(),
                AbilitySlotBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AbilitySlotBuffer>(),
                FixedTagSourceBufferTypeHandle = SystemAPI.GetBufferTypeHandle<TagFixedSourceBuffer>(),
                TemporaryTagSourceBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<TagTemporarySourceBuffer>(isReadOnly: true),
                PendingLookup = SystemAPI.GetComponentLookup<ASCCommandPendingComponent>(),
                DestroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(),
                AttributeDirtyLookup = SystemAPI.GetComponentLookup<AttributeDirtyComponent>(),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(),
                AbilityMainTargetLookup = SystemAPI.GetComponentLookup<AbilityMainTargetComponent>(),
                AbilityActivationPendingLookup = SystemAPI.GetComponentLookup<AbilityActivationPendingComponent>(),
                AbilityCommitRequestLookup = SystemAPI.GetComponentLookup<AbilityCommitRequestComponent>(),
                AbilityCancelRequestLookup = SystemAPI.GetComponentLookup<AbilityCancelRequestComponent>(),
                AbilityEndRequestLookup = SystemAPI.GetComponentLookup<AbilityEndRequestComponent>(),
                AbilityDestroyOnCleanupLookup = SystemAPI.GetComponentLookup<AbilityDestroyOnCleanupComponent>(),
                EventBusLookup = SystemAPI.GetComponentLookup<GameplayEventBusComponent>(),
                GameplayEventLookup = SystemAPI.GetBufferLookup<GameplayEventBusEventBuffer>(),
                AttributeChangeEventLookup = SystemAPI.GetBufferLookup<AttributeChangeEventBuffer>(),
                TagChangeEventLookup = SystemAPI.GetBufferLookup<TagChangeEventBuffer>(),
                StructuralEcb = structuralEcb,
                AbilityArchetype = GASRuntimeEntityArchetypes.Ability(em),
                Catalog = catalog,
                EventBusEntity = eventBusEntity,
                Frame = frame,
            }.Schedule(_query, state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private struct ASCCommandBufferResolveJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<ASCIdentityComponent> IdentityTypeHandle;
            public ComponentTypeHandle<TagMaskComponent> TagMaskTypeHandle;
            public ComponentTypeHandle<TagFixedMaskComponent> FixedTagMaskTypeHandle;
            public BufferTypeHandle<ASCCommandBuffer> AscCommandBufferTypeHandle;
            public BufferTypeHandle<AbilityCommandBuffer> AbilityCommandBufferTypeHandle;
            public BufferTypeHandle<ASCDestroyCommandBuffer> DestroyCommandBufferTypeHandle;
            public BufferTypeHandle<AttributeValueBuffer> AttributeValueBufferTypeHandle;
            public BufferTypeHandle<AbilitySlotBuffer> AbilitySlotBufferTypeHandle;
            public BufferTypeHandle<TagFixedSourceBuffer> FixedTagSourceBufferTypeHandle;
            [ReadOnly] public BufferTypeHandle<TagTemporarySourceBuffer> TemporaryTagSourceBufferTypeHandle;
            public ComponentLookup<ASCCommandPendingComponent> PendingLookup;
            public ComponentLookup<ASCDestroyingComponent> DestroyingLookup;
            public ComponentLookup<AttributeDirtyComponent> AttributeDirtyLookup;
            public ComponentLookup<AbilityStateComponent> AbilityStateLookup;
            public ComponentLookup<AbilityMainTargetComponent> AbilityMainTargetLookup;
            public ComponentLookup<AbilityActivationPendingComponent> AbilityActivationPendingLookup;
            public ComponentLookup<AbilityCommitRequestComponent> AbilityCommitRequestLookup;
            public ComponentLookup<AbilityCancelRequestComponent> AbilityCancelRequestLookup;
            public ComponentLookup<AbilityEndRequestComponent> AbilityEndRequestLookup;
            public ComponentLookup<AbilityDestroyOnCleanupComponent> AbilityDestroyOnCleanupLookup;
            public ComponentLookup<GameplayEventBusComponent> EventBusLookup;
            public BufferLookup<GameplayEventBusEventBuffer> GameplayEventLookup;
            public BufferLookup<AttributeChangeEventBuffer> AttributeChangeEventLookup;
            public BufferLookup<TagChangeEventBuffer> TagChangeEventLookup;
            public EntityCommandBuffer StructuralEcb;
            public EntityArchetype AbilityArchetype;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public Entity EventBusEntity;
            public int Frame;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var ascs = chunk.GetNativeArray(EntityTypeHandle);
                var identities = chunk.GetNativeArray(ref IdentityTypeHandle);
                var tagMasks = chunk.GetNativeArray(ref TagMaskTypeHandle);
                var fixedTagMasks = chunk.GetNativeArray(ref FixedTagMaskTypeHandle);
                var ascCommandBuffers = chunk.GetBufferAccessor(ref AscCommandBufferTypeHandle);
                var abilityCommandBuffers = chunk.GetBufferAccessor(ref AbilityCommandBufferTypeHandle);
                var destroyCommandBuffers = chunk.GetBufferAccessor(ref DestroyCommandBufferTypeHandle);
                var attributeBuffers = chunk.GetBufferAccessor(ref AttributeValueBufferTypeHandle);
                var abilitySlotBuffers = chunk.GetBufferAccessor(ref AbilitySlotBufferTypeHandle);
                var fixedTagSourceBuffers = chunk.GetBufferAccessor(ref FixedTagSourceBufferTypeHandle);
                var temporaryTagSourceBuffers = chunk.GetBufferAccessorRO(ref TemporaryTagSourceBufferTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var asc = ascs[entityIndex];
                    var ascCommands = ascCommandBuffers[entityIndex];
                    var abilityCommands = abilityCommandBuffers[entityIndex];
                    var destroyCommands = destroyCommandBuffers[entityIndex];
                    var attributes = attributeBuffers[entityIndex];
                    var abilitySlots = abilitySlotBuffers[entityIndex];
                    var fixedTagSources = fixedTagSourceBuffers[entityIndex];
                    var temporaryTagSources = temporaryTagSourceBuffers[entityIndex];
                    var identity = identities[entityIndex];
                    var tagMask = tagMasks[entityIndex];
                    var fixedTagMask = fixedTagMasks[entityIndex];

                    if (destroyCommands.Length > 0)
                    {
                        ProcessDestroyCommand(asc, abilitySlots);
                    }
                    else if (!IsDestroying(asc))
                    {
                        ProcessAscCommands(
                            asc,
                            ascCommands,
                            attributes,
                            fixedTagSources,
                            temporaryTagSources,
                            ref identity,
                            ref tagMask,
                            ref fixedTagMask);
                        ProcessAbilityCommands(asc, abilityCommands, abilitySlots);
                    }

                    ClearOwnerLocalCommands(ascCommands, abilityCommands, destroyCommands);
                    PendingLookup.SetComponentEnabled(asc, false);
                    identities[entityIndex] = identity;
                    tagMasks[entityIndex] = tagMask;
                    fixedTagMasks[entityIndex] = fixedTagMask;
                }
            }

            private void ProcessAscCommands(
                Entity asc,
                DynamicBuffer<ASCCommandBuffer> commands,
                DynamicBuffer<AttributeValueBuffer> attributes,
                DynamicBuffer<TagFixedSourceBuffer> fixedTagSources,
                DynamicBuffer<TagTemporarySourceBuffer> temporaryTagSources,
                ref ASCIdentityComponent identity,
                ref TagMaskComponent tagMask,
                ref TagFixedMaskComponent fixedTagMask)
            {
                for (var i = 0; i < commands.Length; i++)
                {
                    var command = commands[i].Command;
                    switch (command.CommandType)
                    {
                        case ASCCommandType.SetLevel:
                            identity.Level = command.Level;
                            break;
                        case ASCCommandType.AddFixedTag:
                            AddFixedTag(
                                asc,
                                in command,
                                fixedTagSources,
                                temporaryTagSources,
                                ref tagMask,
                                ref fixedTagMask);
                            break;
                        case ASCCommandType.RemoveFixedTag:
                            RemoveFixedTag(
                                asc,
                                in command,
                                fixedTagSources,
                                temporaryTagSources,
                                ref tagMask,
                                ref fixedTagMask);
                            break;
                        case ASCCommandType.SetAttributeBaseValue:
                            SetAttributeBaseValue(asc, attributes, in command);
                            break;
                        case ASCCommandType.AddAttribute:
                            AddAttribute(asc, attributes, in command);
                            break;
                    }
                }
            }

            private void AddFixedTag(
                Entity asc,
                in ASCCommand command,
                DynamicBuffer<TagFixedSourceBuffer> fixedTagSources,
                DynamicBuffer<TagTemporarySourceBuffer> temporaryTagSources,
                ref TagMaskComponent tagMask,
                ref TagFixedMaskComponent fixedTagMask)
            {
                if (command.TagMask.IsEmpty)
                    return;

                var changed = false;
                for (var tagIndex = 0; tagIndex < TagMaskComponent.Capacity; tagIndex++)
                {
                    if (!command.TagMask.HasTag(tagIndex)
                        || HasFixedTagSource(fixedTagSources, command.TagSourceIndex, tagIndex))
                    {
                        continue;
                    }

                    fixedTagSources.Add(new TagFixedSourceBuffer
                    {
                        SourceTagIndex = command.TagSourceIndex,
                        TagIndex = tagIndex,
                    });
                    changed = true;
                }

                if (changed)
                    RebuildTagMasks(asc, fixedTagSources, temporaryTagSources, ref tagMask, ref fixedTagMask);
            }

            private void RemoveFixedTag(
                Entity asc,
                in ASCCommand command,
                DynamicBuffer<TagFixedSourceBuffer> fixedTagSources,
                DynamicBuffer<TagTemporarySourceBuffer> temporaryTagSources,
                ref TagMaskComponent tagMask,
                ref TagFixedMaskComponent fixedTagMask)
            {
                if (command.TagMask.IsEmpty)
                    return;

                var removed = false;
                for (var i = fixedTagSources.Length - 1; i >= 0; i--)
                {
                    if (fixedTagSources[i].SourceTagIndex != command.TagSourceIndex)
                        continue;

                    fixedTagSources.RemoveAt(i);
                    removed = true;
                }

                if (removed)
                    RebuildTagMasks(asc, fixedTagSources, temporaryTagSources, ref tagMask, ref fixedTagMask);
            }

            private static bool HasFixedTagSource(
                DynamicBuffer<TagFixedSourceBuffer> sources,
                int sourceTagIndex,
                int tagIndex)
            {
                for (var i = 0; i < sources.Length; i++)
                {
                    var source = sources[i];
                    if (source.SourceTagIndex == sourceTagIndex && source.TagIndex == tagIndex)
                        return true;
                }

                return false;
            }

            private void RebuildTagMasks(
                Entity asc,
                DynamicBuffer<TagFixedSourceBuffer> fixedTagSources,
                DynamicBuffer<TagTemporarySourceBuffer> temporaryTagSources,
                ref TagMaskComponent tagMask,
                ref TagFixedMaskComponent fixedTagMask)
            {
                var oldMask = tagMask;
                var nextFixedMask = new TagFixedMaskComponent();
                for (var i = 0; i < fixedTagSources.Length; i++)
                    nextFixedMask.Mask.AddTag(fixedTagSources[i].TagIndex);

                var nextMask = nextFixedMask.Mask;
                for (var i = 0; i < temporaryTagSources.Length; i++)
                    nextMask.AddTag(temporaryTagSources[i].TagIndex);

                fixedTagMask = nextFixedMask;
                tagMask = nextMask;
                EnqueueTagDiffEvents(asc, oldMask, nextMask);
            }

            private void EnqueueTagDiffEvents(Entity asc, in TagMaskComponent oldMask, in TagMaskComponent newMask)
            {
                for (var tagIndex = 0; tagIndex < TagMaskComponent.Capacity; tagIndex++)
                {
                    var wasActive = oldMask.HasTag(tagIndex);
                    var isActive = newMask.HasTag(tagIndex);
                    if (wasActive == isActive)
                        continue;

                    EnqueueTagChange(new TagChangeEventBuffer
                    {
                        ASC = asc,
                        TagIndex = tagIndex,
                        Added = isActive,
                    });
                }
            }

            private void SetAttributeBaseValue(
                Entity asc,
                DynamicBuffer<AttributeValueBuffer> attributes,
                in ASCCommand command)
            {
                for (var i = 0; i < attributes.Length; i++)
                {
                    if (attributes[i].AttrSetCode != command.AttrSetCode
                        || attributes[i].Code != command.AttributeCode)
                    {
                        continue;
                    }

                    var attr = attributes[i];
                    var oldValue = attr.BaseValue;
                    var oldCurrentValue = attr.CurrentValue;
                    attr.BaseValue = command.AttributeValue;
                    attr.CurrentValue = attr.BaseValue;
                    attr.Dirty = true;
                    if (oldCurrentValue != attr.CurrentValue)
                    {
                        attr.PreviousCurrentValue = oldCurrentValue;
                        attr.CurrentValueChangePending = true;
                    }
                    attributes[i] = attr;

                    MarkOwnerDirty(asc);
                    EnqueueAttributeChange(new AttributeChangeEventBuffer
                    {
                        ASC = asc,
                        SourceAsc = asc,
                        AttrSetCode = command.AttrSetCode,
                        AttributeCode = command.AttributeCode,
                        OldValue = oldValue,
                        NewValue = attr.BaseValue,
                        IsBaseValue = true,
                    });
                    return;
                }
            }

            private void AddAttribute(
                Entity asc,
                DynamicBuffer<AttributeValueBuffer> attributes,
                in ASCCommand command)
            {
                attributes.Add(new AttributeValueBuffer
                {
                    AttrSetCode = command.AttrSetCode,
                    Code = command.AttributeCode,
                    BaseValue = command.AttributeValue,
                    CurrentValue = command.AttributeValue,
                    PreviousCurrentValue = command.AttributeValue,
                    IsClampMin = command.IsClampMin,
                    IsClampMax = command.IsClampMax,
                    MinValue = command.MinValue,
                    MaxValue = command.MaxValue,
                    Dirty = true,
                });
                MarkOwnerDirty(asc);
            }

            private void ProcessAbilityCommands(
                Entity owner,
                DynamicBuffer<AbilityCommandBuffer> commands,
                DynamicBuffer<AbilitySlotBuffer> grantedAbilities)
            {
                for (var i = 0; i < commands.Length; i++)
                {
                    var command = ResolveOwner(owner, commands[i].Command);
                    if (command.Owner != owner || command.CommandType != EAbilityCommandType.Grant)
                        continue;

                    ProcessGrantCommand(owner, grantedAbilities, in command);
                }

                for (var i = 0; i < commands.Length; i++)
                {
                    var command = ResolveOwner(owner, commands[i].Command);
                    if (command.Owner != owner || command.CommandType == EAbilityCommandType.Grant)
                        continue;

                    ProcessRuntimeAbilityCommand(owner, grantedAbilities, in command);
                }
            }

            private static AbilityCommand ResolveOwner(Entity owner, AbilityCommand command)
            {
                if (command.Owner == Entity.Null)
                    command.Owner = owner;
                return command;
            }

            private void ProcessGrantCommand(
                Entity owner,
                DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
                in AbilityCommand command)
            {
                if (IsDestroying(owner))
                    return;

                if (command.AbilityCode > 0
                    && HasGrantedAbility(grantedAbilities, Entity.Null, command.AbilityCode))
                {
                    return;
                }

                var abilityEntity = command.AbilityEntity;
                if ((abilityEntity == Entity.Null
                        || (!IsDeferredEntity(abilityEntity) && !AbilityStateLookup.HasComponent(abilityEntity)))
                    && command.AbilityCode > 0)
                {
                    abilityEntity = CreateAbilityEntity(owner, command.AbilityCode);
                }

                if (abilityEntity == Entity.Null)
                    return;

                if (!IsDeferredEntity(abilityEntity) && AbilityStateLookup.HasComponent(abilityEntity))
                {
                    var state = AbilityStateLookup[abilityEntity];
                    state.Owner = owner;
                    AbilityStateLookup[abilityEntity] = state;
                }

                if (HasGrantedAbility(grantedAbilities, abilityEntity, command.AbilityCode))
                    return;

                StructuralEcb.AppendToBuffer(owner, new AbilitySlotBuffer
                {
                    AbilityEntity = abilityEntity,
                });
            }

            private Entity CreateAbilityEntity(Entity owner, int abilityCode)
            {
                if (!Catalog.IsCreated)
                    return Entity.Null;

                ref var catalog = ref Catalog.Value;
                if (!TryGetAbilityDefinition(ref catalog, abilityCode, out var abilityDefinition))
                    return Entity.Null;

                var ability = StructuralEcb.CreateEntity(AbilityArchetype);
                GASRuntimeEntityArchetypes.InitializeAbilityEntity(StructuralEcb, ability);
                StructuralEcb.SetComponent(
                    ability,
                    AbilityStateComponent.Create(
                        abilityDefinition.AbilityCode,
                        abilityDefinition.Level,
                        owner));
                StructuralEcb.SetComponent(ability, new AbilityMainTargetComponent { TargetAsc = Entity.Null });
                if (ShouldAutoEndOnCatalogCommit(in abilityDefinition))
                    StructuralEcb.SetComponentEnabled<AbilityAutoEndOnCommitComponent>(ability, true);

                return ability;
            }

            private static bool ShouldAutoEndOnCatalogCommit(in GASCatalogAbilityDefinitionBlob abilityDefinition)
            {
                return abilityDefinition.PrimaryGameplayEffectCode > 0
                       || abilityDefinition.SecondaryGameplayEffectCode > 0
                       || abilityDefinition.CostGameplayEffectCode > 0
                       || abilityDefinition.CooldownGameplayEffectCode > 0;
            }

            private static bool TryGetAbilityDefinition(
                ref GASDefinitionCatalogBlob catalog,
                int abilityCode,
                out GASCatalogAbilityDefinitionBlob abilityDefinition)
            {
                for (var i = 0; i < catalog.Abilities.Length; i++)
                {
                    var candidate = catalog.Abilities[i];
                    if (candidate.AbilityCode != abilityCode)
                        continue;

                    abilityDefinition = candidate;
                    return true;
                }

                abilityDefinition = default;
                return false;
            }

            private void ProcessRuntimeAbilityCommand(
                Entity owner,
                DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
                in AbilityCommand command)
            {
                if (command.CommandType == EAbilityCommandType.Activate
                    && TryProcessResolvedActivation(in command))
                {
                    return;
                }

                if (IsDestroying(owner))
                    return;

                var ability = ResolveRequestedAbility(grantedAbilities, in command);
                if (ability == Entity.Null)
                    return;

                switch (command.CommandType)
                {
                    case EAbilityCommandType.Activate:
                        SetMainTarget(ability, command.TargetAsc);
                        EnableMarker(ref AbilityActivationPendingLookup, ability);
                        break;
                    case EAbilityCommandType.End:
                        RequestAbilityEnd(
                            ability,
                            EAbilityLifecycleReason.ExplicitEnd);
                        break;
                    case EAbilityCommandType.Cancel:
                        RequestAbilityCancel(
                            ability,
                            EAbilityLifecycleReason.ExplicitCancel);
                        break;
                    case EAbilityCommandType.Remove:
                        RemoveAbility(owner, grantedAbilities, ability);
                        break;
                }
            }

            private bool TryProcessResolvedActivation(in AbilityCommand command)
            {
                if (!IsUsableRequestedAbility(command.AbilityEntity, command.Owner, command.AbilityCode))
                    return false;

                if (AbilityCommitRequestLookup.HasComponent(command.AbilityEntity))
                {
                    AbilityCommitRequestLookup[command.AbilityEntity] = new AbilityCommitRequestComponent
                    {
                        TargetAsc = command.TargetAsc,
                    };
                    if (!AbilityCommitRequestLookup.IsComponentEnabled(command.AbilityEntity))
                        AbilityCommitRequestLookup.SetComponentEnabled(command.AbilityEntity, true);
                    return true;
                }

                SetMainTarget(command.AbilityEntity, command.TargetAsc);
                EnableMarker(ref AbilityActivationPendingLookup, command.AbilityEntity);
                return true;
            }

            private Entity ResolveRequestedAbility(
                DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
                in AbilityCommand command)
            {
                if (IsUsableRequestedAbility(command.AbilityEntity, command.Owner, command.AbilityCode))
                    return command.AbilityEntity;

                return FindAbility(grantedAbilities, command.AbilityCode);
            }

            private Entity FindAbility(DynamicBuffer<AbilitySlotBuffer> grantedAbilities, int abilityCode)
            {
                if (abilityCode <= 0)
                    return Entity.Null;

                for (var i = 0; i < grantedAbilities.Length; i++)
                {
                    var ability = grantedAbilities[i].AbilityEntity;
                    if (!AbilityStateLookup.HasComponent(ability))
                        continue;

                    var state = AbilityStateLookup[ability];
                    if (state.Code == abilityCode)
                        return ability;
                }

                return Entity.Null;
            }

            private bool IsUsableRequestedAbility(
                Entity ability,
                Entity expectedOwner,
                int expectedAbilityCode)
            {
                if (ability == Entity.Null
                    || !AbilityStateLookup.HasComponent(ability)
                    || !AbilityMainTargetLookup.HasComponent(ability)
                    || !AbilityActivationPendingLookup.HasComponent(ability))
                {
                    return false;
                }

                var state = AbilityStateLookup[ability];
                if (expectedAbilityCode > 0 && state.Code != expectedAbilityCode)
                    return false;

                return expectedOwner == Entity.Null || state.Owner == expectedOwner;
            }

            private bool HasGrantedAbility(
                DynamicBuffer<AbilitySlotBuffer> abilities,
                Entity abilityEntity,
                int abilityCode)
            {
                for (var i = 0; i < abilities.Length; i++)
                {
                    var granted = abilities[i].AbilityEntity;
                    if (granted == abilityEntity)
                        return true;
                    if (abilityCode <= 0
                        || IsDeferredEntity(granted)
                        || !AbilityStateLookup.HasComponent(granted))
                    {
                        continue;
                    }

                    if (AbilityStateLookup[granted].Code == abilityCode)
                        return true;
                }

                return false;
            }

            private void SetMainTarget(Entity ability, Entity targetAsc)
            {
                if (!AbilityMainTargetLookup.HasComponent(ability))
                    return;

                AbilityMainTargetLookup[ability] = new AbilityMainTargetComponent
                {
                    TargetAsc = IsAvailableAsc(targetAsc) ? targetAsc : Entity.Null,
                };
            }

            private void RemoveAbility(
                Entity owner,
                DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
                Entity ability)
            {
                RemoveAbilityFromOwner(grantedAbilities, ability);

                if (IsAbilityRunning(ability))
                {
                    RequestAbilityCancel(
                        ability,
                        EAbilityLifecycleReason.RemoveAbility);
                    EnableDestroyOnCleanup(ability);
                    return;
                }

                DestroyAbilityEntity(ability);
            }

            private void ProcessDestroyCommand(Entity asc, DynamicBuffer<AbilitySlotBuffer> grantedAbilities)
            {
                if (DestroyingLookup.HasComponent(asc))
                    DestroyingLookup.SetComponentEnabled(asc, true);

                DestroyOwnedAbilities(asc, grantedAbilities);
            }

            private void DestroyOwnedAbilities(Entity asc, DynamicBuffer<AbilitySlotBuffer> grantedAbilities)
            {
                for (var i = grantedAbilities.Length - 1; i >= 0; i--)
                {
                    var ability = grantedAbilities[i].AbilityEntity;
                    if (!AbilityStateLookup.HasComponent(ability))
                        continue;

                    var state = AbilityStateLookup[ability];
                    if (state.Owner != asc)
                        continue;

                    grantedAbilities.RemoveAt(i);
                    if (state.Phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending)
                    {
                        RequestAbilityCancel(
                            ability,
                            EAbilityLifecycleReason.AscDestroy);
                        EnableDestroyOnCleanup(ability);
                    }
                    else
                    {
                        DestroyAbilityEntity(ability);
                    }
                }
            }

            private bool IsAbilityRunning(Entity ability)
            {
                if (!AbilityStateLookup.HasComponent(ability))
                    return false;

                var state = AbilityStateLookup[ability];
                return state.Phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;
            }

            private static void RemoveAbilityFromOwner(
                DynamicBuffer<AbilitySlotBuffer> abilities,
                Entity ability)
            {
                for (var i = abilities.Length - 1; i >= 0; i--)
                {
                    if (abilities[i].AbilityEntity == ability)
                        abilities.RemoveAt(i);
                }
            }

            private void DestroyAbilityEntity(Entity ability)
            {
                if (AbilityStateLookup.HasComponent(ability))
                    StructuralEcb.DestroyEntity(ability);
            }

            private void RequestAbilityEnd(
                Entity ability,
                EAbilityLifecycleReason reason,
                Entity sourceAbility = default,
                Entity sourceEffect = default,
                int sourceAbilityCode = 0)
            {
                if (!AbilityEndRequestLookup.HasComponent(ability)
                    || AbilityEndRequestLookup.IsComponentEnabled(ability))
                {
                    return;
                }

                var resolvedSourceAbilityCode = ResolveSourceAbilityCode(sourceAbility, sourceAbilityCode);
                AbilityEndRequestLookup[ability] = new AbilityEndRequestComponent
                {
                    Reason = reason,
                    SourceAbility = sourceAbility,
                    SourceEffect = sourceEffect,
                    SourceAbilityCode = resolvedSourceAbilityCode,
                };
                AbilityEndRequestLookup.SetComponentEnabled(ability, true);
                EnqueueAbilityLifecycleRequestFact(
                    ability,
                    EGameplayEventType.AbilityEndRequested,
                    reason,
                    sourceAbility,
                    sourceEffect,
                    resolvedSourceAbilityCode);
            }

            private void RequestAbilityCancel(
                Entity ability,
                EAbilityLifecycleReason reason,
                Entity sourceAbility = default,
                Entity sourceEffect = default,
                int sourceAbilityCode = 0)
            {
                if (!AbilityCancelRequestLookup.HasComponent(ability)
                    || AbilityCancelRequestLookup.IsComponentEnabled(ability))
                {
                    return;
                }

                var resolvedSourceAbilityCode = ResolveSourceAbilityCode(sourceAbility, sourceAbilityCode);
                AbilityCancelRequestLookup[ability] = new AbilityCancelRequestComponent
                {
                    Reason = reason,
                    SourceAbility = sourceAbility,
                    SourceEffect = sourceEffect,
                    SourceAbilityCode = resolvedSourceAbilityCode,
                };
                AbilityCancelRequestLookup.SetComponentEnabled(ability, true);
                EnqueueAbilityLifecycleRequestFact(
                    ability,
                    EGameplayEventType.AbilityCancelRequested,
                    reason,
                    sourceAbility,
                    sourceEffect,
                    resolvedSourceAbilityCode);
            }

            private void EnableDestroyOnCleanup(Entity ability)
            {
                if (AbilityDestroyOnCleanupLookup.HasComponent(ability)
                    && !AbilityDestroyOnCleanupLookup.IsComponentEnabled(ability))
                {
                    AbilityDestroyOnCleanupLookup.SetComponentEnabled(ability, true);
                }
            }

            private int ResolveSourceAbilityCode(Entity sourceAbility, int sourceAbilityCode)
            {
                if (sourceAbilityCode != 0
                    || sourceAbility == Entity.Null
                    || !AbilityStateLookup.HasComponent(sourceAbility))
                {
                    return sourceAbilityCode;
                }

                return AbilityStateLookup[sourceAbility].Code;
            }

            private void EnqueueAbilityLifecycleRequestFact(
                Entity ability,
                EGameplayEventType type,
                EAbilityLifecycleReason reason,
                Entity sourceAbility,
                Entity sourceEffect,
                int sourceAbilityCode)
            {
                var state = AbilityStateLookup.HasComponent(ability)
                    ? AbilityStateLookup[ability]
                    : default;
                EnqueueGameplayEvent(new GameplayEventBusEventBuffer
                {
                    Type = type,
                    SourceAsc = state.Owner,
                    TargetAsc = state.Owner,
                    SourceAbility = ability,
                    GameplayEffect = sourceEffect,
                    RelatedAbility = sourceAbility,
                    EventCode = state.Code,
                    ReasonCode = (int)reason,
                    RelatedAbilityCode = sourceAbilityCode,
                    Value = sourceAbilityCode,
                });
            }

            private void MarkOwnerDirty(Entity asc)
            {
                if (asc != Entity.Null
                    && AttributeDirtyLookup.HasComponent(asc)
                    && !AttributeDirtyLookup.IsComponentEnabled(asc))
                {
                    AttributeDirtyLookup.SetComponentEnabled(asc, true);
                }
            }

            private void EnqueueGameplayEvent(GameplayEventBusEventBuffer evt)
            {
                if (EventBusEntity == Entity.Null || !GameplayEventLookup.HasBuffer(EventBusEntity))
                    return;

                evt.Frame = Frame;
                if (EventBusLookup.HasComponent(EventBusEntity))
                {
                    var eventBus = EventBusLookup[EventBusEntity];
                    evt.Sequence = eventBus.NextSequence;
                    eventBus.NextSequence++;
                    EventBusLookup[EventBusEntity] = eventBus;
                }
                else
                {
                    evt.Sequence = 0;
                }

                GameplayEventLookup[EventBusEntity].Add(evt);
            }

            private void EnqueueAttributeChange(AttributeChangeEventBuffer evt)
            {
                if (EventBusEntity != Entity.Null && AttributeChangeEventLookup.HasBuffer(EventBusEntity))
                    AttributeChangeEventLookup[EventBusEntity].Add(evt);
            }

            private void EnqueueTagChange(TagChangeEventBuffer evt)
            {
                if (EventBusEntity != Entity.Null && TagChangeEventLookup.HasBuffer(EventBusEntity))
                    TagChangeEventLookup[EventBusEntity].Add(evt);
            }

            private bool IsAvailableAsc(Entity asc)
            {
                return asc != Entity.Null
                       && DestroyingLookup.HasComponent(asc)
                       && !DestroyingLookup.IsComponentEnabled(asc);
            }

            private bool IsDestroying(Entity asc)
            {
                return asc != Entity.Null
                       && DestroyingLookup.HasComponent(asc)
                       && DestroyingLookup.IsComponentEnabled(asc);
            }

            private static bool IsDeferredEntity(Entity entity)
            {
                return entity.Index < 0;
            }

            private static void EnableMarker<T>(ref ComponentLookup<T> lookup, Entity entity)
                where T : unmanaged, IComponentData, IEnableableComponent
            {
                if (lookup.HasComponent(entity) && !lookup.IsComponentEnabled(entity))
                    lookup.SetComponentEnabled(entity, true);
            }

            private static void ClearOwnerLocalCommands(
                DynamicBuffer<ASCCommandBuffer> ascCommands,
                DynamicBuffer<AbilityCommandBuffer> abilityCommands,
                DynamicBuffer<ASCDestroyCommandBuffer> destroyCommands)
            {
                ascCommands.Clear();
                abilityCommands.Clear();
                destroyCommands.Clear();
            }
        }
    }
}
