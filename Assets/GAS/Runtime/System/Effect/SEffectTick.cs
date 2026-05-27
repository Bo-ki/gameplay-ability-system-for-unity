using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASEffectGroup))]
    public partial struct SEffectTick : ISystem
    {
        private EntityQuery _ownerQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerQuery = SystemAPI.QueryBuilder()
                .WithAll<CActiveEffectStore, BActiveEffectSlot, BGameplayEffect, BTempTagSource, CTagMask>()
                .Build();
            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate(_ownerQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            var commandWriter = EffectCommandSpecStream.BeginCommandWriter(em, frame);
            var eventBusEntity = SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var eventWriter = eventBusEntity != Entity.Null
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;

            try
            {
                using var owners = _ownerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                for (var i = 0; i < owners.Length; i++)
                    TickOwnerActiveEffects(em, owners[i], frame, ref commandWriter, ref eventWriter);

                commandWriter.Flush();
            }
            finally
            {
                eventWriter.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state) { }

        private static void TickOwnerActiveEffects(
            EntityManager em,
            Entity owner,
            int frame,
            ref EffectCommandSpecStream.CommandWriter commandWriter,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (!em.Exists(owner) || !em.HasBuffer<BActiveEffectSlot>(owner))
                return;

            var slots = em.GetBuffer<BActiveEffectSlot>(owner);
            for (var i = slots.Length - 1; i >= 0; i--)
            {
                var slot = slots[i];
                var actionFlags = ActiveEffectStore.CreateTickActionFlags(in slot, frame);
                if (actionFlags == (int)EActiveEffectTickActionFlags.None)
                    continue;

                var effect = slot.ActiveEffectEntity;
                if (effect == Entity.Null || !em.Exists(effect))
                {
                    slots.RemoveAt(i);
                    continue;
                }

                if ((actionFlags & (int)EActiveEffectTickActionFlags.Period) != 0)
                {
                    EmitPeriodCommands(em, owner, in slot, frame, ref commandWriter);
                    if (EffectRuntimeUtility.TryGetContext(em, effect, out var context))
                        ActiveEffectStore.TryRefreshPeriodFrame(em, effect, in context, frame);
                    if (em.Exists(effect) && em.HasComponent<CPeriodRuntime>(effect))
                        em.SetComponentData(effect, new CPeriodRuntime { StartTime = frame });
                }

                if ((actionFlags & (int)EActiveEffectTickActionFlags.DurationExpire) != 0)
                    EffectRuntimeUtility.CleanupActiveEffect(em, effect, ref eventWriter);
            }
        }

        private static void EmitPeriodCommands(
            EntityManager em,
            Entity owner,
            in BActiveEffectSlot slot,
            int frame,
            ref EffectCommandSpecStream.CommandWriter commandWriter)
        {
            var effect = slot.ActiveEffectEntity;
            if (effect == Entity.Null || !em.Exists(effect))
                return;

            if (em.HasBuffer<BPeriodGEConfig>(effect))
            {
                var periodEffects = em.GetBuffer<BPeriodGEConfig>(effect);
                for (var i = 0; i < periodEffects.Length; i++)
                    AppendPeriodCommand(em, owner, in slot, effect, periodEffects[i].GameplayEffectCode, ref commandWriter);
                return;
            }

            if (!EffectRuntimeUtility.TryGetStaticDefinitionBlob(em, effect, out var blob))
                return;

            ref var definition = ref blob.Value;
            for (var i = 0; i < definition.PeriodEffectCodes.Length; i++)
                AppendPeriodCommand(em, owner, in slot, effect, definition.PeriodEffectCodes[i], ref commandWriter);
        }

        private static void AppendPeriodCommand(
            EntityManager em,
            Entity owner,
            in BActiveEffectSlot slot,
            Entity effect,
            int gameplayEffectCode,
            ref EffectCommandSpecStream.CommandWriter commandWriter)
        {
            if (gameplayEffectCode <= 0)
                return;

            var request = new CApplyGameplayEffectRequest
            {
                SourceAsc = slot.SourceAsc,
                SourceAbility = slot.SourceAbility,
                SourceEffect = effect,
                Instigator = slot.Instigator != Entity.Null ? slot.Instigator : slot.SourceAsc,
                Causer = effect,
                GameplayEffectCode = gameplayEffectCode,
                Level = slot.Level,
                ParentContextId = slot.ContextId,
            };

            if (!GameplayEffectRequestWriter.TryPrepareAppendableCommand(
                    em,
                    in request,
                    owner,
                    ResolvePeriodTargetDataKind(request.SourceAsc, owner),
                    EEffectCommandSource.Period,
                    out var command))
            {
                return;
            }

            if (em.HasBuffer<BSetByCallerValue>(effect))
                commandWriter.AppendCommand(command, em.GetBuffer<BSetByCallerValue>(effect));
            else
                commandWriter.AppendCommand(command);
        }

        private static ETargetDataKind ResolvePeriodTargetDataKind(Entity sourceAsc, Entity targetAsc)
        {
            return sourceAsc == targetAsc
                ? ETargetDataKind.Self
                : ETargetDataKind.Entity;
        }
    }
}
