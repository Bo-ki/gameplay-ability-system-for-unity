using System.Collections.Generic;
using Unity.Entities;

namespace GAS.Runtime
{
    internal static class GameplayEffectRequestWriter
    {
        public static Entity AppendSimpleInstantCommandToStream(
            EntityManager em,
            in GEApplyRequestComponent request,
            Entity targetAsc,
            ETargetDataKind targetDataKind)
        {
            return TryAppendSimpleInstantCommand(em, request, targetAsc, targetDataKind, null)
                ? ResolveAcceptedOwner(in request, targetAsc)
                : Entity.Null;
        }

        public static Entity AppendSimpleInstantCommandToStream(
            EntityManager em,
            in GEApplyRequestComponent request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            in GESetByCallerRequestValueBuffer setByCallerValue)
        {
            var canUseSetByCaller = setByCallerValue.Key > 0;
            var setByCallerValues = canUseSetByCaller
                ? new[] { setByCallerValue }
                : null;

            return TryAppendSimpleInstantCommand(em, request, targetAsc, targetDataKind, setByCallerValues)
                ? ResolveAcceptedOwner(in request, targetAsc)
                : Entity.Null;
        }

        public static Entity AppendSimpleInstantCommandsToStream(
            EntityManager em,
            in GEApplyRequestComponent request,
            IReadOnlyList<Entity> targetAscs,
            ETargetDataKind targetDataKind)
        {
            return TryAppendSimpleInstantCommands(em, request, targetAscs, targetDataKind, null)
                ? ResolveAcceptedOwner(in request, targetAscs[0])
                : Entity.Null;
        }

        public static bool TryAppendSimpleInstantCommands(
            EntityManager em,
            in GEApplyRequestComponent request,
            IReadOnlyList<Entity> targetAscs,
            ETargetDataKind targetDataKind,
            IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues)
        {
            return TryAppendSimpleInstantCommands(
                em,
                request,
                targetAscs,
                targetDataKind,
                setByCallerValues,
                ResolveCommandSource(in request));
        }

        public static bool TryAppendSimpleInstantCommands(
            EntityManager em,
            in GEApplyRequestComponent request,
            IReadOnlyList<Entity> targetAscs,
            ETargetDataKind targetDataKind,
            IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues,
            GEEffectCommandSource source)
        {
            if (targetAscs == null || targetAscs.Count == 0)
                return false;

            if (!TryResolveStreamWriteContext(em, out var streamEntity, out var currentFrame))
                return false;

            for (var i = 0; i < targetAscs.Count; i++)
            {
                if (!CanAppendEffectCommand(em, request, targetAscs[i], targetDataKind, source))
                    return false;
            }

            var writer = EffectCommandSpecStream.BeginCommandWriter(em, streamEntity, currentFrame);
            if (!writer.IsCreated)
                return false;

            for (var i = 0; i < targetAscs.Count; i++)
            {
                PrepareAppendableCommand(
                    em,
                    request,
                    targetAscs[i],
                    targetDataKind,
                    source,
                    out var command);
                var resolved = AppendPreparedCommand(em, ref writer, in command, setByCallerValues);
                if (resolved.Sequence <= 0)
                    return false;
            }

            writer.Flush();
            return true;
        }

        public static bool TryAppendSimpleInstantCommand(
            EntityManager em,
            in GEApplyRequestComponent request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues)
        {
            return TryAppendSimpleInstantCommand(
                em,
                request,
                targetAsc,
                targetDataKind,
                setByCallerValues,
                ResolveCommandSource(in request));
        }

        public static bool TryAppendSimpleInstantCommand(
            EntityManager em,
            in GEApplyRequestComponent request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues,
            GEEffectCommandSource source)
        {
            if (!PrepareAppendableCommand(em, request, targetAsc, targetDataKind, source, out var command))
                return false;

            if (!TryResolveStreamWriteContext(em, out var streamEntity, out var currentFrame))
                return false;

            var writer = EffectCommandSpecStream.BeginCommandWriter(em, streamEntity, currentFrame);
            if (!writer.IsCreated)
                return false;

            var resolved = AppendPreparedCommand(em, ref writer, in command, setByCallerValues);
            writer.Flush();
            return resolved.Sequence > 0;
        }

        public static bool TryAppendSimpleInstantCommand(
            EntityManager em,
            in GEApplyRequestComponent request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            DynamicBuffer<GESetByCallerRequestValueBuffer> setByCallerValues,
            GEEffectCommandSource source)
        {
            if (!PrepareAppendableCommand(em, request, targetAsc, targetDataKind, source, out var command))
                return false;

            if (!TryResolveStreamWriteContext(em, out var streamEntity, out var currentFrame))
                return false;

            var writer = EffectCommandSpecStream.BeginCommandWriter(em, streamEntity, currentFrame);
            if (!writer.IsCreated)
                return false;

            var resolved = AppendPreparedCommand(em, ref writer, in command, setByCallerValues);
            writer.Flush();
            return resolved.Sequence > 0;
        }

        public static bool TryPrepareAppendableCommand(
            EntityManager em,
            in GEApplyRequestComponent request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            GEEffectCommandSource source,
            out GEEffectCommandBuffer command)
        {
            return PrepareAppendableCommand(
                em,
                request,
                targetAsc,
                targetDataKind,
                source,
                out command);
        }

        private static bool CanAppendActiveMutationCommand(
            EntityManager em,
            in GEEffectCommandBuffer command)
        {
            var targetAsc = ResolveTargetAsc(in command);
            return targetAsc != Entity.Null
                && em.Exists(targetAsc)
                && em.HasBuffer<ActiveEffectMutationCommandBuffer>(targetAsc)
                && em.HasBuffer<ActiveEffectMutationSetByCallerValueBuffer>(targetAsc);
        }

        private static bool CanAppendInstantCommand(
            EntityManager em,
            in GEEffectCommandBuffer command)
        {
            var targetAsc = ResolveTargetAsc(in command);
            return targetAsc != Entity.Null
                && em.Exists(targetAsc)
                && em.HasBuffer<GEEffectCommandBuffer>(targetAsc)
                && em.HasBuffer<GESetByCallerValueBuffer>(targetAsc);
        }

        private static GEEffectCommandBuffer AppendPreparedCommand(
            EntityManager em,
            ref EffectCommandSpecStream.CommandWriter writer,
            in GEEffectCommandBuffer command,
            IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues)
        {
            if (command.Kind == GEEffectCommandKind.Instant)
            {
                var resolved = TryGetInstantCommandOwnerPayload(
                        em,
                        in command,
                        out var instantCommands,
                        out var instantSetByCallerValues)
                    ? writer.AppendOwnerLocalInstantCommand(
                        command,
                        instantCommands,
                        instantSetByCallerValues,
                        setByCallerValues)
                    : default;
                return resolved;
            }

            var activeMutationResolved = TryGetActiveMutationOwnerPayload(
                    em,
                    in command,
                    out var activeMutationCommands,
                    out var activeMutationSetByCallerValues)
                ? writer.AppendOwnerLocalActiveMutationCommand(
                    command,
                    activeMutationCommands,
                    activeMutationSetByCallerValues,
                    setByCallerValues)
                : default;
            return activeMutationResolved;
        }

        private static GEEffectCommandBuffer AppendPreparedCommand(
            EntityManager em,
            ref EffectCommandSpecStream.CommandWriter writer,
            in GEEffectCommandBuffer command,
            DynamicBuffer<GESetByCallerRequestValueBuffer> setByCallerValues)
        {
            if (command.Kind == GEEffectCommandKind.Instant)
            {
                var resolved = TryGetInstantCommandOwnerPayload(
                        em,
                        in command,
                        out var instantCommands,
                        out var instantSetByCallerValues)
                    ? writer.AppendOwnerLocalInstantCommand(
                        command,
                        instantCommands,
                        instantSetByCallerValues,
                        setByCallerValues)
                    : default;
                return resolved;
            }

            var activeMutationResolved = TryGetActiveMutationOwnerPayload(
                    em,
                    in command,
                    out var activeMutationCommands,
                    out var activeMutationSetByCallerValues)
                ? writer.AppendOwnerLocalActiveMutationCommand(
                    command,
                    activeMutationCommands,
                    activeMutationSetByCallerValues,
                    setByCallerValues)
                : default;
            return activeMutationResolved;
        }

        private static bool TryGetActiveMutationOwnerPayload(
            EntityManager em,
            in GEEffectCommandBuffer command,
            out DynamicBuffer<ActiveEffectMutationCommandBuffer> ownerCommands,
            out DynamicBuffer<ActiveEffectMutationSetByCallerValueBuffer> ownerSetByCallerValues)
        {
            ownerCommands = default;
            ownerSetByCallerValues = default;

            var targetAsc = ResolveTargetAsc(in command);
            if (targetAsc == Entity.Null
                || !em.Exists(targetAsc)
                || !em.HasBuffer<ActiveEffectMutationCommandBuffer>(targetAsc)
                || !em.HasBuffer<ActiveEffectMutationSetByCallerValueBuffer>(targetAsc))
            {
                return false;
            }

            ownerCommands = em.GetBuffer<ActiveEffectMutationCommandBuffer>(targetAsc);
            ownerSetByCallerValues = em.GetBuffer<ActiveEffectMutationSetByCallerValueBuffer>(targetAsc);
            return true;
        }

        private static bool TryGetInstantCommandOwnerPayload(
            EntityManager em,
            in GEEffectCommandBuffer command,
            out DynamicBuffer<GEEffectCommandBuffer> ownerCommands,
            out DynamicBuffer<GESetByCallerValueBuffer> ownerSetByCallerValues)
        {
            ownerCommands = default;
            ownerSetByCallerValues = default;

            var targetAsc = ResolveTargetAsc(in command);
            if (targetAsc == Entity.Null
                || !em.Exists(targetAsc)
                || !em.HasBuffer<GEEffectCommandBuffer>(targetAsc)
                || !em.HasBuffer<GESetByCallerValueBuffer>(targetAsc))
            {
                return false;
            }

            ownerCommands = em.GetBuffer<GEEffectCommandBuffer>(targetAsc);
            ownerSetByCallerValues = em.GetBuffer<GESetByCallerValueBuffer>(targetAsc);
            return true;
        }

        private static bool CanAppendEffectCommand(
            EntityManager em,
            in GEApplyRequestComponent request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            GEEffectCommandSource source)
        {
            return PrepareAppendableCommand(
                em,
                request,
                targetAsc,
                targetDataKind,
                source,
                out var command)
                && (command.Kind == GEEffectCommandKind.Instant
                    ? CanAppendInstantCommand(em, in command)
                    : CanAppendActiveMutationCommand(em, in command));
        }

        private static bool PrepareAppendableCommand(
            EntityManager em,
            in GEApplyRequestComponent request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            GEEffectCommandSource source,
            out GEEffectCommandBuffer command)
        {
            if (targetAsc == Entity.Null)
            {
                command = default;
                return false;
            }

            command = EffectCommandSpecStream.ToCommand(
                request,
                targetAsc,
                targetDataKind,
                source);
            return true;
        }

        private static Entity ResolveTargetAsc(in GEEffectCommandBuffer command)
        {
            return command.TargetAsc != Entity.Null ? command.TargetAsc : command.SourceAsc;
        }

        private static GEEffectCommandSource ResolveCommandSource(in GEApplyRequestComponent request)
        {
            return request.SourceAbility != Entity.Null
                ? GEEffectCommandSource.Ability
                : GEEffectCommandSource.RuntimeBoundary;
        }

        private static bool TryResolveStreamWriteContext(
            EntityManager em,
            out Entity streamEntity,
            out int currentFrame)
        {
            currentFrame = 0;
            if (!EffectCommandSpecStream.TryGetSingleton(em, out streamEntity))
                return false;
            if (!em.HasComponent<GEEffectCommandStreamComponent>(streamEntity))
                return false;

            currentFrame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            return true;
        }

        private static Entity ResolveAcceptedOwner(in GEApplyRequestComponent request, Entity fallbackTarget)
        {
            return request.SourceAsc != Entity.Null ? request.SourceAsc : fallbackTarget;
        }

    }
}
