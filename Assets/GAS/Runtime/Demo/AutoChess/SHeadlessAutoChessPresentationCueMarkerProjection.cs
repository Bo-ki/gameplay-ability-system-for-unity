using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCueGroup), OrderFirst = true)]
    [UpdateBefore(typeof(SPresentationOutboxProjection))]
    [UpdateBefore(typeof(SDebugReplayLogProjection))]
    public partial struct SHeadlessAutoChessPresentationCueMarkerProjection : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessPresentationCueMarkerFacts>()
                .Build();
            _unitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit>()
                .Build();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity)
                || !em.HasBuffer<BGameplayEvent>(eventBusEntity)
                || !em.HasBuffer<BAttributeChangeEvent>(eventBusEntity)
                || !em.HasBuffer<BCueRequest>(eventBusEntity)
                || !em.HasBuffer<BTagChangeEvent>(eventBusEntity)
                || !em.HasBuffer<BDamageEvent>(eventBusEntity))
            {
                return;
            }

            using var drivers = _driverQuery.ToEntityArray(Allocator.Temp);
            if (drivers.Length == 0)
                return;

            var driverEntity = drivers[0];
            var driver = em.GetComponentData<CHeadlessAutoChessDriver>(driverEntity);
            var facts = em.GetComponentData<CHeadlessAutoChessPresentationCueMarkerFacts>(driverEntity);
            var currentFrame = ResolveCurrentFrame(em);
            ResetProcessedCountsIfFrameChanged(ref facts, currentFrame);

            if (!facts.BattleStarted)
            {
                using var units = _unitQuery.ToEntityArray(Allocator.Temp);
                EmitBattleStartMarkers(em, eventBusEntity, driverEntity, driver, units);
                facts.BattleStarted = true;
                facts.LastRound = driver.Round;
            }
            else if (driver.Round > facts.LastRound)
            {
                EmitUiMarker(
                    em,
                    eventBusEntity,
                    HeadlessAutoChessPresentationMarkerCode.UiRoundStarted,
                    driver.Round,
                    driver.Round,
                    Entity.Null,
                    driverEntity);
                facts.LastRound = driver.Round;
            }

            var gameplayEvents = em.GetBuffer<BGameplayEvent>(eventBusEntity);
            var gameplayStart = ClampProcessedCount(facts.ProcessedGameplayEventCount, gameplayEvents.Length);
            var gameplayEnd = gameplayEvents.Length;
            using var gameplaySnapshot = EventBusHelper.CopyBufferRange(
                gameplayEvents,
                gameplayStart,
                gameplayEnd,
                Allocator.Temp);

            var attributeEvents = em.GetBuffer<BAttributeChangeEvent>(eventBusEntity);
            var attributeStart = ClampProcessedCount(facts.ProcessedAttributeEventCount, attributeEvents.Length);
            var attributeEnd = attributeEvents.Length;
            using var attributeSnapshot = EventBusHelper.CopyBufferRange(
                attributeEvents,
                attributeStart,
                attributeEnd,
                Allocator.Temp);

            var cueRequests = em.GetBuffer<BCueRequest>(eventBusEntity);
            var cueStart = ClampProcessedCount(facts.ProcessedCueRequestCount, cueRequests.Length);
            var cueEnd = cueRequests.Length;
            using var cueSnapshot = EventBusHelper.CopyBufferRange(
                cueRequests,
                cueStart,
                cueEnd,
                Allocator.Temp);

            var tagEvents = em.GetBuffer<BTagChangeEvent>(eventBusEntity);
            var tagStart = ClampProcessedCount(facts.ProcessedTagEventCount, tagEvents.Length);
            var tagEnd = tagEvents.Length;
            using var tagSnapshot = EventBusHelper.CopyBufferRange(
                tagEvents,
                tagStart,
                tagEnd,
                Allocator.Temp);

            var damageEvents = em.GetBuffer<BDamageEvent>(eventBusEntity);
            var damageStart = ClampProcessedCount(facts.ProcessedDamageEventCount, damageEvents.Length);
            var damageEnd = damageEvents.Length;
            using var damageSnapshot = EventBusHelper.CopyBufferRange(
                damageEvents,
                damageStart,
                damageEnd,
                Allocator.Temp);

            ProjectGameplayEvents(em, eventBusEntity, gameplaySnapshot, ref facts);
            ProjectAttributeEvents(em, eventBusEntity, attributeSnapshot);
            ProjectCueRequests(em, eventBusEntity, cueSnapshot);
            ProjectTagEvents(em, eventBusEntity, tagSnapshot);
            ProjectDamageEvents(em, eventBusEntity, damageSnapshot);

            facts.ProcessedGameplayEventCount = em.GetBuffer<BGameplayEvent>(eventBusEntity).Length;
            facts.ProcessedAttributeEventCount = attributeEnd;
            facts.ProcessedCueRequestCount = cueEnd;
            facts.ProcessedTagEventCount = tagEnd;
            facts.ProcessedDamageEventCount = damageEnd;
            em.SetComponentData(driverEntity, facts);
        }

        private static void ResetProcessedCountsIfFrameChanged(
            ref CHeadlessAutoChessPresentationCueMarkerFacts facts,
            int currentFrame)
        {
            if (facts.LastProjectionFrame == currentFrame)
                return;

            facts.LastProjectionFrame = currentFrame;
            facts.ProcessedGameplayEventCount = 0;
            facts.ProcessedAttributeEventCount = 0;
            facts.ProcessedCueRequestCount = 0;
            facts.ProcessedTagEventCount = 0;
            facts.ProcessedDamageEventCount = 0;
        }

        private static void EmitBattleStartMarkers(
            EntityManager em,
            Entity eventBusEntity,
            Entity driverEntity,
            in CHeadlessAutoChessDriver driver,
            NativeArray<Entity> units)
        {
            EmitUiMarker(
                em,
                eventBusEntity,
                HeadlessAutoChessPresentationMarkerCode.UiBattleStarted,
                driver.Round,
                units.Length,
                Entity.Null,
                driverEntity);
            EmitUiMarker(
                em,
                eventBusEntity,
                HeadlessAutoChessPresentationMarkerCode.UiRoundStarted,
                driver.Round,
                driver.Round,
                Entity.Null,
                driverEntity);

            for (var i = 0; i < units.Length; i++)
            {
                var unitEntity = units[i];
                if (!em.Exists(unitEntity) || !em.HasComponent<CHeadlessAutoChessUnit>(unitEntity))
                    continue;

                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(unitEntity);
                var health = GetAttribute(em, unitEntity, unit.HealthAttrSetCode, unit.HealthAttrCode);
                EmitUiMarker(
                    em,
                    eventBusEntity,
                    HeadlessAutoChessPresentationMarkerCode.UiUnitSpawned,
                    (int)unit.Team,
                    unit.Slot,
                    Entity.Null,
                    unitEntity);
                EmitUiMarker(
                    em,
                    eventBusEntity,
                    HeadlessAutoChessPresentationMarkerCode.UiHealthBarAttached,
                    unit.HealthAttrCode,
                    health,
                    Entity.Null,
                    unitEntity);
            }
        }

        private static void ProjectGameplayEvents(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<BGameplayEvent> events,
            ref CHeadlessAutoChessPresentationCueMarkerFacts facts)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (IsPresentationMarker(evt.Type))
                    continue;

                switch (evt.Type)
                {
                    case EGameplayEventType.AbilityCommitSucceeded:
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueAbilityCast,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxAbilityWindup,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxAbilityCast,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.GameplayEffectApplied:
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueEffectApplied,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxEffectApplied,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.GameplayEffectRemoved:
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueEffectExpired,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxEffectExpired,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessUnitDefeated:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiUnitDefeated,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxUnitDefeated,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxUnitDefeated,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessBattleResolved:
                        if (!facts.BattleEnded)
                        {
                            EmitUiMarker(
                                em,
                                eventBusEntity,
                                HeadlessAutoChessPresentationMarkerCode.UiBattleEnded,
                                evt.EventCode,
                                evt.Value,
                                evt.SourceAsc,
                                evt.TargetAsc);
                            EmitSettlementMarker(
                                em,
                                eventBusEntity,
                                HeadlessAutoChessPresentationMarkerCode.SettlementScoreboard,
                                evt.EventCode,
                                evt.Value,
                                evt.SourceAsc,
                                evt.TargetAsc);
                            EmitSettlementMarker(
                                em,
                                eventBusEntity,
                                HeadlessAutoChessPresentationMarkerCode.SettlementPanel,
                                evt.EventCode,
                                evt.Value,
                                evt.SourceAsc,
                                evt.TargetAsc);
                            facts.BattleEnded = true;
                        }
                        break;
                    case EGameplayEventType.AutoChessSynergyActivated:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiSynergyBadgeChanged,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxSynergyAura,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxSynergy,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessSynergyExpired:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiSynergyBadgeChanged,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxEffectExpired,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessReviveRequested:
                    case EGameplayEventType.AutoChessReviveApplied:
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueRevive,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxEffectApplied,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessControlTurnSkipped:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiCrowdControlSkipped,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxCrowdControl,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxCrowdControl,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextControl,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueCrowdControl,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessShieldApplied:
                    case EGameplayEventType.AutoChessShieldAbsorbed:
                    case EGameplayEventType.AutoChessShieldBroken:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiShieldChanged,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxShield,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxShield,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextShield,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueShield,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessSummonRequested:
                    case EGameplayEventType.AutoChessSummonSpawned:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiSummonSpawned,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxSummon,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxSummon,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextSummon,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueSummon,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessSummonExpired:
                    case EGameplayEventType.AutoChessSummonDespawned:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiSummonExpired,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxSummonExpired,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxSummonExpired,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextSummonExpired,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueSummonExpired,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessDamageResisted:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiResistanceChanged,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxResistance,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxResistance,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextResistance,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueResistance,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessEquipmentApplied:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiEquipmentChanged,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueCounter,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessCounterTriggered:
                    case EGameplayEventType.AutoChessCounterDamageApplied:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiCounterTriggered,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxCounter,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxCounter,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextCounter,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueCounter,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessCleanseRequested:
                    case EGameplayEventType.AutoChessCleanseApplied:
                    case EGameplayEventType.AutoChessCleanseEffectRemoved:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiCleanseTriggered,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxCleanse,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxCleanse,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextCleanse,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueCleanse,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessCleanseRallyRequested:
                    case EGameplayEventType.AutoChessCleanseRallyApplied:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiCleanseRallyTriggered,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxCleanseRally,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxCleanseRally,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextCleanseRally,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueCleanseRally,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessRallyComboTriggered:
                    case EGameplayEventType.AutoChessRallyComboDamageApplied:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiRallyComboTriggered,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxRallyCombo,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxRallyCombo,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextRallyCombo,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueRallyCombo,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessLifeStealTriggered:
                    case EGameplayEventType.AutoChessLifeStealHealed:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiLifeStealTriggered,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxLifeSteal,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxLifeSteal,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextLifeSteal,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueLifeSteal,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessPoisonStackRequested:
                    case EGameplayEventType.AutoChessPoisonStackChanged:
                    case EGameplayEventType.AutoChessPoisonOverflowTriggered:
                    case EGameplayEventType.AutoChessPoisonOverflowDamageApplied:
                    case EGameplayEventType.AutoChessPoisonPeriodDamageApplied:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiPoisonStacked,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxPoison,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxPoison,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextPoison,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CuePoison,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessExecuteTriggered:
                    case EGameplayEventType.AutoChessExecuteDamageApplied:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiExecuteTriggered,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxExecute,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxExecute,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextExecute,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueExecute,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessDeathBurstTriggered:
                    case EGameplayEventType.AutoChessDeathBurstDamageApplied:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiDeathBurstTriggered,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxDeathBurst,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxDeathBurst,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextDeathBurst,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueDeathBurst,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                    case EGameplayEventType.AutoChessEnrageTriggered:
                    case EGameplayEventType.AutoChessEnrageApplied:
                        EmitUiMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.UiEnrageTriggered,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc);
                        EmitVfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.VfxEnrage,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitSfxMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.SfxEnrage,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextEnrage,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        EmitCueMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.CueEnrage,
                            evt.EventCode,
                            evt.Value,
                            evt.SourceAsc,
                            evt.TargetAsc,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                        break;
                }
            }
        }

        private static void ProjectAttributeEvents(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<BAttributeChangeEvent> events)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.AttrSetCode != HeadlessAutoChessScenario.AttributeSetCombat)
                    continue;

                if (evt.AttributeCode == HeadlessAutoChessScenario.AttributeHealth)
                {
                    EmitUiMarker(
                        em,
                        eventBusEntity,
                        HeadlessAutoChessPresentationMarkerCode.UiHealthChanged,
                        evt.AttributeCode,
                        evt.NewValue,
                        evt.SourceAsc,
                        evt.ASC);

                    if (evt.NewValue > evt.OldValue)
                    {
                        EmitFloatingTextMarker(
                            em,
                            eventBusEntity,
                            HeadlessAutoChessPresentationMarkerCode.FloatingTextHeal,
                            evt.AttributeCode,
                            evt.NewValue - evt.OldValue,
                            evt.SourceAsc,
                            evt.ASC,
                            evt.SourceAbility,
                            evt.GameplayEffect,
                            evt.ContextId);
                    }
                }
                else if (evt.AttributeCode == HeadlessAutoChessScenario.AttributeMana)
                {
                    EmitUiMarker(
                        em,
                        eventBusEntity,
                        HeadlessAutoChessPresentationMarkerCode.UiStatusIconChanged,
                        evt.AttributeCode,
                        evt.NewValue,
                        evt.SourceAsc,
                        evt.ASC);
                }
                else if (evt.AttributeCode == HeadlessAutoChessScenario.AttributeShield)
                {
                    EmitUiMarker(
                        em,
                        eventBusEntity,
                        HeadlessAutoChessPresentationMarkerCode.UiShieldChanged,
                        evt.AttributeCode,
                        evt.NewValue,
                        evt.SourceAsc,
                        evt.ASC);
                }
                else if (evt.AttributeCode == HeadlessAutoChessScenario.AttributeCounterDamage)
                {
                    EmitUiMarker(
                        em,
                        eventBusEntity,
                        HeadlessAutoChessPresentationMarkerCode.UiEquipmentChanged,
                        evt.AttributeCode,
                        evt.NewValue,
                        evt.SourceAsc,
                        evt.ASC);
                }
                else if (evt.AttributeCode == HeadlessAutoChessScenario.AttributeLifeStealRatio)
                {
                    EmitUiMarker(
                        em,
                        eventBusEntity,
                        HeadlessAutoChessPresentationMarkerCode.UiLifeStealTriggered,
                        evt.AttributeCode,
                        evt.NewValue,
                        evt.SourceAsc,
                        evt.ASC);
                }
            }
        }

        private static void ProjectCueRequests(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<BCueRequest> requests)
        {
            for (var i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                EmitCueMarker(
                    em,
                    eventBusEntity,
                    HeadlessAutoChessPresentationMarkerCode.CueRequest,
                    (int)request.CueEvent,
                    0f,
                    request.SourceAsc,
                    request.TargetAsc,
                    request.SourceAbility,
                    request.GameplayEffect,
                    request.ContextId);
            }
        }

        private static void ProjectTagEvents(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<BTagChangeEvent> events)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                EmitUiMarker(
                    em,
                    eventBusEntity,
                    HeadlessAutoChessPresentationMarkerCode.UiStatusIconChanged,
                    evt.TagIndex,
                    evt.Added ? 1f : 0f,
                    Entity.Null,
                    evt.ASC);

                if ((evt.TagIndex == HeadlessAutoChessScenario.TagArcaneTeamBuff
                     || evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessStunned
                     || evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessShielded
                     || evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessSummoned
                     || evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessCounterReady
                     || evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessCleanseRallied
                     || evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessLifeStealReady
                     || evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessPoisoned
                     || evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessExecutionReady
                     || evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessExecuted
                     || evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessDeathBurstReady
                     || evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessEnraged)
                    && evt.Added)
                {
                    EmitFloatingTextMarker(
                        em,
                        eventBusEntity,
                        evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessStunned
                            ? HeadlessAutoChessPresentationMarkerCode.FloatingTextControl
                            : evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessShielded
                                ? HeadlessAutoChessPresentationMarkerCode.FloatingTextShield
                                : evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessSummoned
                                    ? HeadlessAutoChessPresentationMarkerCode.FloatingTextSummon
                                    : evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessCounterReady
                                        ? HeadlessAutoChessPresentationMarkerCode.FloatingTextCounter
                                        : evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessCleanseRallied
                                            ? HeadlessAutoChessPresentationMarkerCode.FloatingTextCleanseRally
                                            : evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessLifeStealReady
                                                ? HeadlessAutoChessPresentationMarkerCode.FloatingTextLifeSteal
                                                : evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessPoisoned
                                                    ? HeadlessAutoChessPresentationMarkerCode.FloatingTextPoison
                                                    : evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessExecutionReady
                                                      || evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessExecuted
                                                        ? HeadlessAutoChessPresentationMarkerCode.FloatingTextExecute
                                                        : evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessDeathBurstReady
                                                            ? HeadlessAutoChessPresentationMarkerCode.FloatingTextDeathBurst
                                                            : evt.TagIndex == HeadlessAutoChessScenario.TagAutoChessEnraged
                                                                ? HeadlessAutoChessPresentationMarkerCode.FloatingTextEnrage
                                                        : HeadlessAutoChessPresentationMarkerCode.FloatingTextBuff,
                        evt.TagIndex,
                        1f,
                        Entity.Null,
                        evt.ASC,
                        Entity.Null,
                        Entity.Null,
                        0);
                }
            }
        }

        private static void ProjectDamageEvents(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<BDamageEvent> events)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                EmitFloatingTextMarker(
                    em,
                    eventBusEntity,
                    HeadlessAutoChessPresentationMarkerCode.FloatingTextDamage,
                    HeadlessAutoChessScenario.AttributeHealth,
                    evt.Amount,
                    evt.Source,
                    evt.Target,
                    Entity.Null,
                    Entity.Null,
                    0);
                EmitVfxMarker(
                    em,
                    eventBusEntity,
                    HeadlessAutoChessPresentationMarkerCode.VfxAbilityImpact,
                    HeadlessAutoChessScenario.AttributeHealth,
                    evt.Amount,
                    evt.Source,
                    evt.Target,
                    Entity.Null,
                    Entity.Null,
                    0);
                EmitSfxMarker(
                    em,
                    eventBusEntity,
                    HeadlessAutoChessPresentationMarkerCode.SfxImpact,
                    HeadlessAutoChessScenario.AttributeHealth,
                    evt.Amount,
                    evt.Source,
                    evt.Target,
                    Entity.Null,
                    Entity.Null,
                    0);
            }
        }

        private static bool IsPresentationMarker(EGameplayEventType type)
        {
            return type == EGameplayEventType.AutoChessPresentationUiMarker
                   || type == EGameplayEventType.AutoChessPresentationVfxMarker
                   || type == EGameplayEventType.AutoChessPresentationSfxMarker
                   || type == EGameplayEventType.AutoChessPresentationFloatingTextMarker
                   || type == EGameplayEventType.AutoChessPresentationCueMarker
                   || type == EGameplayEventType.AutoChessPresentationSettlementMarker;
        }

        private static void EmitUiMarker(
            EntityManager em,
            Entity eventBusEntity,
            HeadlessAutoChessPresentationMarkerCode markerCode,
            int reasonCode,
            float value,
            Entity sourceAsc,
            Entity targetAsc)
        {
            EmitMarker(
                em,
                eventBusEntity,
                EGameplayEventType.AutoChessPresentationUiMarker,
                markerCode,
                reasonCode,
                value,
                sourceAsc,
                targetAsc,
                Entity.Null,
                Entity.Null,
                0);
        }

        private static void EmitVfxMarker(
            EntityManager em,
            Entity eventBusEntity,
            HeadlessAutoChessPresentationMarkerCode markerCode,
            int reasonCode,
            float value,
            Entity sourceAsc,
            Entity targetAsc,
            Entity sourceAbility,
            Entity gameplayEffect,
            int contextId)
        {
            EmitMarker(
                em,
                eventBusEntity,
                EGameplayEventType.AutoChessPresentationVfxMarker,
                markerCode,
                reasonCode,
                value,
                sourceAsc,
                targetAsc,
                sourceAbility,
                gameplayEffect,
                contextId);
        }

        private static void EmitSfxMarker(
            EntityManager em,
            Entity eventBusEntity,
            HeadlessAutoChessPresentationMarkerCode markerCode,
            int reasonCode,
            float value,
            Entity sourceAsc,
            Entity targetAsc,
            Entity sourceAbility,
            Entity gameplayEffect,
            int contextId)
        {
            EmitMarker(
                em,
                eventBusEntity,
                EGameplayEventType.AutoChessPresentationSfxMarker,
                markerCode,
                reasonCode,
                value,
                sourceAsc,
                targetAsc,
                sourceAbility,
                gameplayEffect,
                contextId);
        }

        private static void EmitFloatingTextMarker(
            EntityManager em,
            Entity eventBusEntity,
            HeadlessAutoChessPresentationMarkerCode markerCode,
            int reasonCode,
            float value,
            Entity sourceAsc,
            Entity targetAsc,
            Entity sourceAbility,
            Entity gameplayEffect,
            int contextId)
        {
            EmitMarker(
                em,
                eventBusEntity,
                EGameplayEventType.AutoChessPresentationFloatingTextMarker,
                markerCode,
                reasonCode,
                value,
                sourceAsc,
                targetAsc,
                sourceAbility,
                gameplayEffect,
                contextId);
        }

        private static void EmitCueMarker(
            EntityManager em,
            Entity eventBusEntity,
            HeadlessAutoChessPresentationMarkerCode markerCode,
            int reasonCode,
            float value,
            Entity sourceAsc,
            Entity targetAsc,
            Entity sourceAbility,
            Entity gameplayEffect,
            int contextId)
        {
            EmitMarker(
                em,
                eventBusEntity,
                EGameplayEventType.AutoChessPresentationCueMarker,
                markerCode,
                reasonCode,
                value,
                sourceAsc,
                targetAsc,
                sourceAbility,
                gameplayEffect,
                contextId);
        }

        private static void EmitSettlementMarker(
            EntityManager em,
            Entity eventBusEntity,
            HeadlessAutoChessPresentationMarkerCode markerCode,
            int reasonCode,
            float value,
            Entity sourceAsc,
            Entity targetAsc)
        {
            EmitMarker(
                em,
                eventBusEntity,
                EGameplayEventType.AutoChessPresentationSettlementMarker,
                markerCode,
                reasonCode,
                value,
                sourceAsc,
                targetAsc,
                Entity.Null,
                Entity.Null,
                0);
        }

        private static void EmitMarker(
            EntityManager em,
            Entity eventBusEntity,
            EGameplayEventType type,
            HeadlessAutoChessPresentationMarkerCode markerCode,
            int reasonCode,
            float value,
            Entity sourceAsc,
            Entity targetAsc,
            Entity sourceAbility,
            Entity gameplayEffect,
            int contextId)
        {
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = type,
                SourceAsc = sourceAsc,
                TargetAsc = targetAsc,
                SourceAbility = sourceAbility,
                GameplayEffect = gameplayEffect,
                ContextId = contextId,
                EventCode = (int)markerCode,
                ReasonCode = reasonCode,
                Value = value,
            });
        }

        private static int ClampProcessedCount(int processedCount, int currentLength)
        {
            return processedCount > currentLength ? 0 : processedCount;
        }

        private static float GetAttribute(
            EntityManager em,
            Entity asc,
            int attrSetCode,
            int attributeCode)
        {
            if (asc == Entity.Null || !em.Exists(asc) || !em.HasBuffer<BAttribute>(asc))
                return 0f;

            var attributes = em.GetBuffer<BAttribute>(asc);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode == attrSetCode && attribute.Code == attributeCode)
                    return attribute.CurrentValue;
            }

            return 0f;
        }

        private static int ResolveCurrentFrame(EntityManager em)
        {
            var globalTimer = GASManager.EntityGlobalTimer;
            if (globalTimer != Entity.Null
                && em.Exists(globalTimer)
                && em.HasComponent<GlobalTimer>(globalTimer))
            {
                return em.GetComponentData<GlobalTimer>(globalTimer).Frame;
            }

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GlobalTimer>());
            return query.IsEmptyIgnoreFilter ? 0 : query.GetSingleton<GlobalTimer>().Frame;
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
