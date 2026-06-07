using Unity.Entities;

namespace GAS.Runtime
{
    public static class GASRuntimeEntityArchetypes
    {
        private const int AscCoreComponentTypeCount = 35;

        private static bool _hasCachedWorld;
        private static EntityManager _cachedEntityManager;

        private static EntityArchetype _asc;
        private static EntityArchetype _ability;
        private static EntityArchetype _grantedAbility;
        private static EntityArchetype _effectCommandStream;
        private static EntityArchetype _gameplayEventBus;
        private static EntityArchetype _gameplayEventLogSink;
        private static EntityArchetype _runtimeDebugger;
        private static EntityArchetype _globalTimer;
        private static EntityArchetype _activeEffectGlobalIndex;
        private static EntityArchetype _activeEffectGlobalIndexBucket;
        private static EntityArchetype _gasRunningTag;
        private static EntityArchetype _gameplayEffectRuntime;
        private static EntityArchetype _gameplayEffectPrototype;
        private static EntityArchetype _cueRuntime;

        public static void ResetCache()
        {
            _hasCachedWorld = false;
            _cachedEntityManager = default;
            ResetCachedArchetypes();
        }

        public static EntityArchetype ASC(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_asc.Valid)
                _asc = em.CreateArchetype(CreateASCComponentTypes());

            return _asc;
        }

        public static EntityArchetype ASC(EntityManager em, params ComponentType[] extensionComponents)
        {
            ResetIfWorldChanged(em);
            if (extensionComponents == null || extensionComponents.Length == 0)
                return ASC(em);

            return em.CreateArchetype(CreateASCComponentTypes(extensionComponents));
        }

        public static EntityArchetype Ability(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_ability.Valid)
            {
                _ability = em.CreateArchetype(
                    ComponentType.ReadWrite<AbilityStateComponent>(),
                    ComponentType.ReadWrite<AbilityMainTargetComponent>(),
                    ComponentType.ReadWrite<AbilityActivationPendingComponent>(),
                    ComponentType.ReadWrite<AbilityCommitRequestComponent>(),
                    ComponentType.ReadWrite<AbilityCancelRequestComponent>(),
                    ComponentType.ReadWrite<AbilityEndRequestComponent>(),
                    ComponentType.ReadWrite<AbilityDestroyOnCleanupComponent>(),
                    ComponentType.ReadWrite<AbilityAssetTagsComponent>(),
                    ComponentType.ReadWrite<AbilityActivationRequiredTagsComponent>(),
                    ComponentType.ReadWrite<AbilityActivationBlockedTagsComponent>(),
                    ComponentType.ReadWrite<AbilityActivationOwnedTagsComponent>(),
                    ComponentType.ReadWrite<AbilityBlockWithTagsComponent>(),
                    ComponentType.ReadWrite<AbilityCancelWithTagsComponent>(),
                    ComponentType.ReadWrite<AbilityCooldownComponent>(),
                    ComponentType.ReadWrite<AbilityCostComponent>(),
                    ComponentType.ReadWrite<AbilityAutoEndOnCommitComponent>(),
                    ComponentType.ReadWrite<AbilityMoveInputComponent>(),
                    ComponentType.ReadWrite<AbilityOwnerEffectOnActivateBuffer>(),
                    ComponentType.ReadWrite<AbilityTargetEffectOnActivateBuffer>());
            }

            return _ability;
        }

        public static EntityArchetype GrantedAbility(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_grantedAbility.Valid)
            {
                _grantedAbility = em.CreateArchetype(
                    ComponentType.ReadWrite<AbilityStateComponent>(),
                    ComponentType.ReadWrite<AbilityMainTargetComponent>(),
                    ComponentType.ReadWrite<AbilityGrantedByEffectComponent>(),
                    ComponentType.ReadWrite<AbilityActivationPendingComponent>(),
                    ComponentType.ReadWrite<AbilityCommitRequestComponent>(),
                    ComponentType.ReadWrite<AbilityCancelRequestComponent>(),
                    ComponentType.ReadWrite<AbilityEndRequestComponent>(),
                    ComponentType.ReadWrite<AbilityDestroyOnCleanupComponent>(),
                    ComponentType.ReadWrite<AbilityAssetTagsComponent>(),
                    ComponentType.ReadWrite<AbilityActivationRequiredTagsComponent>(),
                    ComponentType.ReadWrite<AbilityActivationBlockedTagsComponent>(),
                    ComponentType.ReadWrite<AbilityActivationOwnedTagsComponent>(),
                    ComponentType.ReadWrite<AbilityBlockWithTagsComponent>(),
                    ComponentType.ReadWrite<AbilityCancelWithTagsComponent>(),
                    ComponentType.ReadWrite<AbilityCooldownComponent>(),
                    ComponentType.ReadWrite<AbilityCostComponent>(),
                    ComponentType.ReadWrite<AbilityAutoEndOnCommitComponent>(),
                    ComponentType.ReadWrite<AbilityMoveInputComponent>(),
                    ComponentType.ReadWrite<AbilityOwnerEffectOnActivateBuffer>(),
                    ComponentType.ReadWrite<AbilityTargetEffectOnActivateBuffer>());
            }

            return _grantedAbility;
        }

        public static EntityArchetype EffectCommandStream(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_effectCommandStream.Valid)
            {
                _effectCommandStream = em.CreateArchetype(
                    ComponentType.ReadWrite<GEEffectCommandStreamComponent>(),
                    ComponentType.ReadWrite<GEEffectCommandBuffer>(),
                    ComponentType.ReadWrite<GESetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<GEEffectSpecBuffer>(),
                    ComponentType.ReadWrite<GameplayEventBuffer>());
            }

            return _effectCommandStream;
        }

        public static EntityArchetype GameplayEventBus(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_gameplayEventBus.Valid)
            {
                _gameplayEventBus = em.CreateArchetype(
                    ComponentType.ReadWrite<GameplayEventBusComponent>(),
                    ComponentType.ReadWrite<PresentationOutboxProjectionStateComponent>(),
                    ComponentType.ReadWrite<PresentationOutboxProjectionOptionsComponent>(),
                    ComponentType.ReadWrite<TagChangeEventBuffer>(),
                    ComponentType.ReadWrite<AbilityLifecycleRequestBuffer>(),
                    ComponentType.ReadWrite<AttributeOwnerMarkerRequestBuffer>(),
                    ComponentType.ReadWrite<AttributeChangeEventBuffer>(),
                    ComponentType.ReadWrite<CueRequestBuffer>(),
                    ComponentType.ReadWrite<PresentationOutboxOwnerBuffer>());
            }

            return _gameplayEventBus;
        }

        public static EntityArchetype GameplayEventLogSink(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_gameplayEventLogSink.Valid)
            {
                _gameplayEventLogSink = em.CreateArchetype(
                    ComponentType.ReadWrite<GameplayEventLogSinkComponent>(),
                    ComponentType.ReadWrite<ReplayLogEventBuffer>());
            }

            return _gameplayEventLogSink;
        }

        public static EntityArchetype RuntimeDebugger(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_runtimeDebugger.Valid)
            {
                _runtimeDebugger = em.CreateArchetype(
                    ComponentType.ReadWrite<GASRuntimeDebuggerComponent>(),
                    ComponentType.ReadWrite<GASRuntimeDiagnosticEventBuffer>());
            }

            return _runtimeDebugger;
        }

        public static EntityArchetype GlobalTimer(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_globalTimer.Valid)
                _globalTimer = em.CreateArchetype(ComponentType.ReadWrite<GlobalTimer>());
            return _globalTimer;
        }

        public static EntityArchetype ActiveEffectGlobalIndex(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_activeEffectGlobalIndex.Valid)
            {
                _activeEffectGlobalIndex = em.CreateArchetype(
                    ComponentType.ReadWrite<ActiveGameplayEffectGlobalIndexComponent>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectGlobalIndexBuffer>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectGlobalIndexBucketOwnerBuffer>());
            }

            return _activeEffectGlobalIndex;
        }

        public static EntityArchetype ActiveEffectGlobalIndexBucket(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_activeEffectGlobalIndexBucket.Valid)
            {
                _activeEffectGlobalIndexBucket = em.CreateArchetype(
                    ComponentType.ReadWrite<ActiveGameplayEffectGlobalIndexBucketComponent>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectGlobalIndexBuffer>());
            }

            return _activeEffectGlobalIndexBucket;
        }

        public static EntityArchetype GASRunningTag(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_gasRunningTag.Valid)
                _gasRunningTag = em.CreateArchetype(ComponentType.ReadWrite<GASRunningTag>());
            return _gasRunningTag;
        }

        public static EntityArchetype GameplayEffectRuntime(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_gameplayEffectRuntime.Valid)
            {
                _gameplayEffectRuntime = em.CreateArchetype(
                    ComponentType.ReadWrite<GEPrototypeComponent>(),
                    ComponentType.ReadWrite<GEBasicInfoComponent>(),
                    ComponentType.ReadWrite<GEAssetTagsComponent>(),
                    ComponentType.ReadWrite<GEGrantedTagsComponent>(),
                    ComponentType.ReadWrite<GEApplicationRequiredTagsComponent>(),
                    ComponentType.ReadWrite<GEOngoingRequiredTagsComponent>(),
                    ComponentType.ReadWrite<GERemoveEffectWithTagsComponent>(),
                    ComponentType.ReadWrite<GEImmunityTagsComponent>(),
                    ComponentType.ReadWrite<GECueRequestOnApplyComponent>(),
                    ComponentType.ReadWrite<GECueOnActivateComponent>(),
                    ComponentType.ReadWrite<GECueOnAddComponent>(),
                    ComponentType.ReadWrite<GECueOnApplyComponent>(),
                    ComponentType.ReadWrite<GECueOnDeactivateComponent>(),
                    ComponentType.ReadWrite<GECueOnRemoveComponent>(),
                    ComponentType.ReadWrite<GECueOnTickComponent>(),
                    ComponentType.ReadWrite<GEDurationDefinitionComponent>(),
                    ComponentType.ReadWrite<GEDurationRuntimeComponent>(),
                    ComponentType.ReadWrite<GEPeriodDefinitionComponent>(),
                    ComponentType.ReadWrite<GEPeriodRuntimeComponent>(),
                    ComponentType.ReadWrite<GEStackingDefinitionComponent>(),
                    ComponentType.ReadWrite<GEStackingRuntimeComponent>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectGlobalIndexStableRowComponent>(),
                    ComponentType.ReadWrite<GEEffectLifecycleComponent>(),
                    ComponentType.ReadWrite<GEEffectDestroyComponent>(),
                    ComponentType.ReadWrite<GEEffectFinalDestroyComponent>(),
                    ComponentType.ReadWrite<GEExecutionCalculationOutputModifierAppliedComponent>(),
                    ComponentType.ReadWrite<GEModifierConfigBuffer>(),
                    ComponentType.ReadWrite<GEMagnitudeDefinitionBuffer>(),
                    ComponentType.ReadWrite<GEExecutionCalculationDefinitionBuffer>(),
                    ComponentType.ReadWrite<GEExecutionCalculationInputDefinitionBuffer>(),
                    ComponentType.ReadWrite<GEExecutionCalculationOutputModifierDefinitionBuffer>(),
                    ComponentType.ReadWrite<GEGrantedAbilityConfigBuffer>(),
                    ComponentType.ReadWrite<GEGrantedTagConfigBuffer>(),
                    ComponentType.ReadWrite<GEPeriodConfigBuffer>(),
                    ComponentType.ReadWrite<GEOverflowConfigBuffer>(),
                    ComponentType.ReadWrite<GEResolvedModifierBuffer>(),
                    ComponentType.ReadWrite<GEAttributeCaptureValueBuffer>(),
                    ComponentType.ReadWrite<GEExecutionCalculationValueBuffer>(),
                    ComponentType.ReadWrite<GESetByCallerRequestValueBuffer>(),
                    ComponentType.ReadWrite<GEGrantedAbilityRuntimeBuffer>());
            }

            return _gameplayEffectRuntime;
        }

        public static EntityArchetype GameplayEffectPrototype(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_gameplayEffectPrototype.Valid)
            {
                _gameplayEffectPrototype = em.CreateArchetype(
                    ComponentType.ReadWrite<GEPrototypeComponent>(),
                    ComponentType.ReadWrite<GEBasicInfoComponent>(),
                    ComponentType.ReadWrite<GEAssetTagsComponent>(),
                    ComponentType.ReadWrite<GEGrantedTagsComponent>(),
                    ComponentType.ReadWrite<GEApplicationRequiredTagsComponent>(),
                    ComponentType.ReadWrite<GEOngoingRequiredTagsComponent>(),
                    ComponentType.ReadWrite<GERemoveEffectWithTagsComponent>(),
                    ComponentType.ReadWrite<GEImmunityTagsComponent>(),
                    ComponentType.ReadWrite<GECueRequestOnApplyComponent>(),
                    ComponentType.ReadWrite<GECueOnActivateComponent>(),
                    ComponentType.ReadWrite<GECueOnAddComponent>(),
                    ComponentType.ReadWrite<GECueOnApplyComponent>(),
                    ComponentType.ReadWrite<GECueOnDeactivateComponent>(),
                    ComponentType.ReadWrite<GECueOnRemoveComponent>(),
                    ComponentType.ReadWrite<GECueOnTickComponent>(),
                    ComponentType.ReadWrite<GEDurationDefinitionComponent>(),
                    ComponentType.ReadWrite<GEDurationRuntimeComponent>(),
                    ComponentType.ReadWrite<GEPeriodDefinitionComponent>(),
                    ComponentType.ReadWrite<GEPeriodRuntimeComponent>(),
                    ComponentType.ReadWrite<GEStackingDefinitionComponent>(),
                    ComponentType.ReadWrite<GEStackingRuntimeComponent>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectGlobalIndexStableRowComponent>(),
                    ComponentType.ReadWrite<GEEffectLifecycleComponent>(),
                    ComponentType.ReadWrite<GEEffectDestroyComponent>(),
                    ComponentType.ReadWrite<GEEffectFinalDestroyComponent>(),
                    ComponentType.ReadWrite<GEExecutionCalculationOutputModifierAppliedComponent>(),
                    ComponentType.ReadWrite<GEModifierConfigBuffer>(),
                    ComponentType.ReadWrite<GEMagnitudeDefinitionBuffer>(),
                    ComponentType.ReadWrite<GEExecutionCalculationDefinitionBuffer>(),
                    ComponentType.ReadWrite<GEExecutionCalculationInputDefinitionBuffer>(),
                    ComponentType.ReadWrite<GEExecutionCalculationOutputModifierDefinitionBuffer>(),
                    ComponentType.ReadWrite<GEGrantedAbilityConfigBuffer>(),
                    ComponentType.ReadWrite<GEGrantedTagConfigBuffer>(),
                    ComponentType.ReadWrite<GEPeriodConfigBuffer>(),
                    ComponentType.ReadWrite<GEOverflowConfigBuffer>(),
                    ComponentType.ReadWrite<GEResolvedModifierBuffer>(),
                    ComponentType.ReadWrite<GEAttributeCaptureValueBuffer>(),
                    ComponentType.ReadWrite<GEExecutionCalculationValueBuffer>(),
                    ComponentType.ReadWrite<GESetByCallerRequestValueBuffer>(),
                    ComponentType.ReadWrite<GEGrantedAbilityRuntimeBuffer>());
            }

            return _gameplayEffectPrototype;
        }

        public static EntityArchetype CueRuntime(EntityManager em)
        {
            ResetIfWorldChanged(em);
            if (!_cueRuntime.Valid)
            {
                _cueRuntime = em.CreateArchetype(
                    ComponentType.ReadWrite<CueManagedInstanceComponent>(),
                    ComponentType.ReadWrite<CuePresentationRequestComponent>(),
                    ComponentType.ReadWrite<CueRuntimeActiveTag>(),
                    ComponentType.ReadWrite<CuePlayableTag>(),
                    ComponentType.ReadWrite<CuePlayingTag>(),
                    ComponentType.ReadWrite<CueKillRequestTag>(),
                    ComponentType.ReadWrite<CueImmunityTagsComponent>(),
                    ComponentType.ReadWrite<CueRequiredTagsComponent>());
            }

            return _cueRuntime;
        }

        public static void InitializeAbilityEntity(EntityCommandBuffer ecb, Entity ability)
        {
            ecb.SetComponentEnabled<AbilityActivationPendingComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityCommitRequestComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityCancelRequestComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityEndRequestComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityDestroyOnCleanupComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityAssetTagsComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityActivationRequiredTagsComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityActivationBlockedTagsComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityActivationOwnedTagsComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityBlockWithTagsComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityCancelWithTagsComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityCooldownComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityCostComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityAutoEndOnCommitComponent>(ability, false);
            ecb.SetComponentEnabled<AbilityMoveInputComponent>(ability, false);
        }

        public static void InitializeGameplayEffectEntity(EntityManager em, Entity entity, bool prototype)
        {
            em.SetComponentEnabled<GEPrototypeComponent>(entity, prototype);
            em.SetComponentEnabled<GEBasicInfoComponent>(entity, false);
            em.SetComponentEnabled<GEAssetTagsComponent>(entity, false);
            em.SetComponentEnabled<GEGrantedTagsComponent>(entity, false);
            em.SetComponentEnabled<GEApplicationRequiredTagsComponent>(entity, false);
            em.SetComponentEnabled<GEOngoingRequiredTagsComponent>(entity, false);
            em.SetComponentEnabled<GERemoveEffectWithTagsComponent>(entity, false);
            em.SetComponentEnabled<GEImmunityTagsComponent>(entity, false);
            em.SetComponentEnabled<GECueRequestOnApplyComponent>(entity, false);
            em.SetComponentEnabled<GECueOnActivateComponent>(entity, false);
            em.SetComponentEnabled<GECueOnAddComponent>(entity, false);
            em.SetComponentEnabled<GECueOnApplyComponent>(entity, false);
            em.SetComponentEnabled<GECueOnDeactivateComponent>(entity, false);
            em.SetComponentEnabled<GECueOnRemoveComponent>(entity, false);
            em.SetComponentEnabled<GECueOnTickComponent>(entity, false);
            em.SetComponentEnabled<GEDurationDefinitionComponent>(entity, false);
            em.SetComponentEnabled<GEPeriodDefinitionComponent>(entity, false);
            em.SetComponentEnabled<GEStackingDefinitionComponent>(entity, false);
            ResetGameplayEffectRuntimeState(em, entity);
            em.SetComponentEnabled<GEModifierConfigBuffer>(entity, false);
            em.SetComponentEnabled<GEMagnitudeDefinitionBuffer>(entity, false);
            em.SetComponentEnabled<GEExecutionCalculationDefinitionBuffer>(entity, false);
            em.SetComponentEnabled<GEExecutionCalculationInputDefinitionBuffer>(entity, false);
            em.SetComponentEnabled<GEExecutionCalculationOutputModifierDefinitionBuffer>(entity, false);
            em.SetComponentEnabled<GEGrantedAbilityConfigBuffer>(entity, false);
            em.SetComponentEnabled<GEGrantedTagConfigBuffer>(entity, false);
            em.SetComponentEnabled<GEPeriodConfigBuffer>(entity, false);
            em.SetComponentEnabled<GEOverflowConfigBuffer>(entity, false);
        }

        public static void InitializeGameplayEffectRuntimeInstance(EntityManager em, Entity entity)
        {
            em.SetComponentEnabled<GEPrototypeComponent>(entity, false);
            ResetGameplayEffectRuntimeState(em, entity);
        }

        public static void InitializeCueEntity(EntityManager em, Entity cue)
        {
            em.SetComponentEnabled<CueRuntimeActiveTag>(cue, true);
            em.SetComponentEnabled<CuePlayableTag>(cue, false);
            em.SetComponentEnabled<CuePlayingTag>(cue, false);
            em.SetComponentEnabled<CueKillRequestTag>(cue, false);
            em.SetComponentEnabled<CueImmunityTagsComponent>(cue, false);
            em.SetComponentEnabled<CueRequiredTagsComponent>(cue, false);
            em.SetComponentData(cue, default(CuePresentationRequestComponent));
        }

        public static void DeactivateCueEntity(EntityManager em, Entity cue)
        {
            if (cue == Entity.Null || !em.Exists(cue))
                return;

            em.SetComponentEnabled<CueRuntimeActiveTag>(cue, false);
            em.SetComponentEnabled<CuePlayableTag>(cue, false);
            em.SetComponentEnabled<CuePlayingTag>(cue, false);
            em.SetComponentEnabled<CueKillRequestTag>(cue, false);
            em.SetComponentEnabled<CueImmunityTagsComponent>(cue, false);
            em.SetComponentEnabled<CueRequiredTagsComponent>(cue, false);
            em.SetComponentData(cue, default(CuePresentationRequestComponent));
        }

        private static void ResetGameplayEffectRuntimeState(EntityManager em, Entity entity)
        {
            em.SetComponentData(entity, new GEEffectLifecycleComponent());
            em.SetComponentEnabled<GEEffectDestroyComponent>(entity, false);
            em.SetComponentEnabled<GEEffectFinalDestroyComponent>(entity, false);
            em.SetComponentEnabled<GEExecutionCalculationOutputModifierAppliedComponent>(entity, false);
            em.SetComponentEnabled<GEDurationRuntimeComponent>(entity, false);
            em.SetComponentEnabled<GEPeriodRuntimeComponent>(entity, false);
            em.SetComponentEnabled<GEStackingRuntimeComponent>(entity, false);
            em.GetBuffer<GEAttributeCaptureValueBuffer>(entity).Clear();
            em.GetBuffer<GEExecutionCalculationValueBuffer>(entity).Clear();
            em.GetBuffer<GEGrantedAbilityRuntimeBuffer>(entity).Clear();
        }

        private static void ResetIfWorldChanged(EntityManager em)
        {
            if (_hasCachedWorld && _cachedEntityManager.Equals(em))
                return;

            _hasCachedWorld = true;
            _cachedEntityManager = em;
            ResetCachedArchetypes();
        }

        private static void ResetCachedArchetypes()
        {
            _asc = default;
            _ability = default;
            _grantedAbility = default;
            _effectCommandStream = default;
            _gameplayEventBus = default;
            _gameplayEventLogSink = default;
            _runtimeDebugger = default;
            _globalTimer = default;
            _activeEffectGlobalIndex = default;
            _activeEffectGlobalIndexBucket = default;
            _gasRunningTag = default;
            _gameplayEffectRuntime = default;
            _gameplayEffectPrototype = default;
            _cueRuntime = default;
        }

        private static ComponentType[] CreateASCComponentTypes(params ComponentType[] extensionComponents)
        {
            var extensionCount = extensionComponents?.Length ?? 0;
            var componentTypes = new ComponentType[AscCoreComponentTypeCount + extensionCount];
            WriteASCComponentTypes(componentTypes);
            for (var i = 0; i < extensionCount; i++)
                componentTypes[AscCoreComponentTypeCount + i] = extensionComponents[i];
            return componentTypes;
        }

        private static void WriteASCComponentTypes(ComponentType[] componentTypes)
        {
            var index = 0;
            componentTypes[index++] = ComponentType.ReadWrite<ASCIdentityComponent>();
            componentTypes[index++] = ComponentType.ReadWrite<ASCBoundaryReportKeyComponent>();
            componentTypes[index++] = ComponentType.ReadWrite<ASCCommandPendingComponent>();
            componentTypes[index++] = ComponentType.ReadWrite<GERemoveCommandPendingComponent>();
            componentTypes[index++] = ComponentType.ReadWrite<ASCDestroyingComponent>();
            componentTypes[index++] = ComponentType.ReadWrite<TagMaskComponent>();
            componentTypes[index++] = ComponentType.ReadWrite<TagFixedMaskComponent>();
            componentTypes[index++] = ComponentType.ReadWrite<AttributeDirtyComponent>();
            componentTypes[index++] = ComponentType.ReadWrite<AttributeChangeEventPendingComponent>();
            componentTypes[index++] = ComponentType.ReadWrite<AttributeActiveModifierPresentComponent>();
            componentTypes[index++] = ComponentType.ReadWrite<PendingAttributeModifierComponent>();
            componentTypes[index++] = ComponentType.ReadWrite<AttributeValueBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<AttributeModifierBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<AttributeActiveModifierBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<ASCCommandBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<AbilityCommandBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<ASCDestroyCommandBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<GERemoveCommandBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<AbilitySlotBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<TagFixedSourceBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<TagTemporarySourceBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<LegacyGameplayEffectEntityBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<ASCActiveEffectsComponent>();
            componentTypes[index++] = ComponentType.ReadWrite<ActiveGameplayEffectBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<ActiveGameplayEffectSetByCallerValueBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<ActiveGameplayEffectCleanupRecordBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<GEEffectCommandBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<GESetByCallerValueBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<ActiveEffectMutationCommandBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<ActiveEffectMutationSetByCallerValueBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<ActiveEffectNextFrameMutationCommandBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<ActiveEffectNextFrameMutationSetByCallerValueBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<ActiveEffectMutationBuffer>();
            componentTypes[index++] = ComponentType.ReadWrite<OwnerLocalGameplayFactBuffer>();
            componentTypes[index] = ComponentType.ReadWrite<PresentationEventBuffer>();
        }
    }
}
