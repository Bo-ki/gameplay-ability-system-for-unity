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

            for (var i = 0; i < targetAscs.Count; i++)
            {
                if (!CanAppendEffectCommand(em, request, targetAscs[i], targetDataKind, source))
                    return false;
            }

            if (!TryResolveStreamWriteContext(em, out var streamEntity, out var currentFrame))
                return false;

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
                writer.AppendCommand(command, setByCallerValues);
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

            var resolved = writer.AppendCommand(command, setByCallerValues);
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

            var resolved = writer.AppendCommand(command, setByCallerValues);
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
                out _);
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

            currentFrame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            return true;
        }

        private static Entity ResolveAcceptedOwner(in GEApplyRequestComponent request, Entity fallbackTarget)
        {
            return request.SourceAsc != Entity.Null ? request.SourceAsc : fallbackTarget;
        }

    }
}
