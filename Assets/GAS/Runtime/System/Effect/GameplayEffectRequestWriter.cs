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
                if (!CanAppendEffectCommand(em, request, targetAscs[i], targetDataKind, source))
                    return false;
            }

            var writer = EffectCommandSpecStream.BeginCommandWriter(em);
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
            if (!PrepareAppendableCommand(em, request, targetAsc, targetDataKind, source, out var command))
                return false;

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
            if (!PrepareAppendableCommand(em, request, targetAsc, targetDataKind, source, out var command))
                return false;

            EffectCommandSpecStream.AppendCommand(em, command, setByCallerValues);
            return true;
        }

        public static bool TryPrepareAppendableCommand(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            EEffectCommandSource source,
            out BEffectCommand command)
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
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            EEffectCommandSource source)
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
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            EEffectCommandSource source,
            out BEffectCommand command)
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

            if (EffectCommandSpecStreamPhaseUtility.CanBuildSimpleInstantSpec(em, in command))
                return true;

            if (!EffectCommandSpecStreamPhaseUtility.CanBuildActiveEffectMutation(em, in command))
                return false;

            command.Kind = EEffectCommandKind.ActiveMutation;
            return true;
        }

        private static EEffectCommandSource ResolveCommandSource(in CApplyGameplayEffectRequest request)
        {
            return request.SourceAbility != Entity.Null
                ? EEffectCommandSource.Ability
                : EEffectCommandSource.RuntimeBoundary;
        }

        #region Placeholder — legacy methods retained for test compilation

        public static Entity Create(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            string namePrefix = "ApplyGERequest")
        {
            return AppendSimpleInstantCommandOrCreateSingleTargetRequest(
                em, request, targetAsc, targetDataKind, namePrefix);
        }

        public static Entity Create(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            IReadOnlyList<Entity> targetAscs,
            ETargetDataKind targetDataKind,
            string namePrefix = "ApplyGERequest")
        {
            return AppendSimpleInstantCommandsOrCreateTargetListRequest(
                em, request, targetAscs, targetDataKind, namePrefix);
        }

        public static void AddTarget(
            EntityManager em,
            Entity requestEntity,
            Entity targetAsc,
            ETargetDataKind targetDataKind) { }

        public static void AddTarget(
            EntityManager em,
            Entity requestEntity,
            IReadOnlyList<Entity> targetAscs,
            ETargetDataKind targetDataKind) { }

        public static void AddSetByCallerValues(
            EntityManager em,
            Entity requestEntity,
            IReadOnlyList<BSetByCallerValue> values) { }

        public static void AddSetByCallerValues(
            EntityManager em,
            Entity requestEntity,
            in BSetByCallerValue value) { }

        public static void AddSetByCallerValues(
            EntityManager em,
            Entity requestEntity,
            DynamicBuffer<BSetByCallerValue> values) { }

        public static Entity ApplyLegacyInstantBypassOrCreateSingleTargetRequest(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            string namePrefix = "ApplyGERequest")
        {
            return AppendSimpleInstantCommandOrCreateSingleTargetRequest(
                em, request, targetAsc, targetDataKind, namePrefix);
        }

        public static Entity ApplyLegacyInstantBypassOrCreateSingleTargetRequest(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            in BSetByCallerValue setByCallerValue,
            string namePrefix = "ApplyGERequest")
        {
            return AppendSimpleInstantCommandOrCreateSingleTargetRequest(
                em, request, targetAsc, targetDataKind, setByCallerValue, namePrefix);
        }

        #endregion
    }
}
