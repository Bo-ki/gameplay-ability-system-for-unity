using System.Collections.Generic;
using Unity.Entities;

namespace GAS.Runtime
{
    internal static class GameplayEffectRequestWriter
    {
        public static Entity AppendSimpleInstantCommandOrCreateSingleTargetRequest(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            string namePrefix = "ApplyGERequest")
        {
            TryAppendSimpleInstantCommand(em, request, targetAsc, targetDataKind, null);
            return Entity.Null;
        }

        public static Entity AppendSimpleInstantCommandOrCreateSingleTargetRequest(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            in BSetByCallerValue setByCallerValue,
            string namePrefix = "ApplyGERequest")
        {
            var canUseSetByCaller = setByCallerValue.Key > 0;
            var setByCallerValues = canUseSetByCaller
                ? new[] { setByCallerValue }
                : null;

            TryAppendSimpleInstantCommand(em, request, targetAsc, targetDataKind, setByCallerValues);
            return Entity.Null;
        }

        public static Entity AppendSimpleInstantCommandsOrCreateTargetListRequest(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            IReadOnlyList<Entity> targetAscs,
            ETargetDataKind targetDataKind,
            string namePrefix = "ApplyGERequest")
        {
            TryAppendSimpleInstantCommands(em, request, targetAscs, targetDataKind, null);
            return Entity.Null;
        }

        public static bool TryAppendSimpleInstantCommands(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            IReadOnlyList<Entity> targetAscs,
            ETargetDataKind targetDataKind,
            IReadOnlyList<BSetByCallerValue> setByCallerValues)
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
            in CApplyGameplayEffectRequest request,
            IReadOnlyList<Entity> targetAscs,
            ETargetDataKind targetDataKind,
            IReadOnlyList<BSetByCallerValue> setByCallerValues,
            EEffectCommandSource source)
        {
            if (targetAscs == null || targetAscs.Count == 0)
                return false;

            for (var i = 0; i < targetAscs.Count; i++)
            {
                if (!CanAppendSimpleInstantCommand(em, request, targetAscs[i], targetDataKind, source))
                    return false;
            }

            var writer = EffectCommandSpecStream.BeginCommandWriter(em);
            for (var i = 0; i < targetAscs.Count; i++)
            {
                var command = EffectCommandSpecStream.ToCommand(
                    request,
                    targetAscs[i],
                    targetDataKind,
                    source);
                writer.AppendCommand(command, setByCallerValues);
            }

            writer.Flush();
            return true;
        }

        public static bool TryAppendSimpleInstantCommand(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            IReadOnlyList<BSetByCallerValue> setByCallerValues)
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
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            IReadOnlyList<BSetByCallerValue> setByCallerValues,
            EEffectCommandSource source)
        {
            if (!CanAppendSimpleInstantCommand(em, request, targetAsc, targetDataKind, source))
                return false;

            var command = EffectCommandSpecStream.ToCommand(
                request,
                targetAsc,
                targetDataKind,
                source);

            EffectCommandSpecStream.AppendCommand(em, command, setByCallerValues);
            return true;
        }

        public static bool TryAppendSimpleInstantCommand(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            DynamicBuffer<BSetByCallerValue> setByCallerValues,
            EEffectCommandSource source)
        {
            if (!CanAppendSimpleInstantCommand(em, request, targetAsc, targetDataKind, source))
                return false;

            var command = EffectCommandSpecStream.ToCommand(
                request,
                targetAsc,
                targetDataKind,
                source);

            EffectCommandSpecStream.AppendCommand(em, command, setByCallerValues);
            return true;
        }

        private static bool CanAppendSimpleInstantCommand(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            EEffectCommandSource source)
        {
            if (targetAsc == Entity.Null)
                return false;

            var command = EffectCommandSpecStream.ToCommand(
                request,
                targetAsc,
                targetDataKind,
                source);

            if (command.Kind != EEffectCommandKind.Instant
                || !EffectCommandSpecStreamPhaseUtility.CanBuildSimpleInstantSpec(em, in command))
            {
                return false;
            }

            return true;
        }

        private static EEffectCommandSource ResolveCommandSource(in CApplyGameplayEffectRequest request)
        {
            return request.SourceAbility != Entity.Null
                ? EEffectCommandSource.Ability
                : EEffectCommandSource.RuntimeBoundary;
        }
    }
}
