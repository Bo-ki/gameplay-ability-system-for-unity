namespace GAS.Runtime
{
    /// <summary>
    /// 固定 Ability 内部命令的 schema 语义优先级与 owner-local canonical 比较，禁止依赖 enum 声明顺序。
    /// </summary>
    internal static class GasAbilityPendingCommandOrder
    {
        /// <summary>
        /// 返回版本化 wait protocol phase；未知 kind 排在闭世界命令之后并由调用方拒绝。
        /// </summary>
        internal static int GetSemanticPriority(int commandKind)
        {
            switch ((GasAbilityPendingCommandKind)commandKind)
            {
                case GasAbilityPendingCommandKind.WaitUnsubscribe:
                    return 1;
                case GasAbilityPendingCommandKind.WaitRegistration:
                    return 2;
                case GasAbilityPendingCommandKind.WaitSignal:
                    return 3;
                case GasAbilityPendingCommandKind.WaitRegistrationAck:
                    return 4;
                case GasAbilityPendingCommandKind.WaitCompletion:
                    return 5;
                case GasAbilityPendingCommandKind.WaitUnsubscribeAck:
                    return 6;
                default:
                    return int.MaxValue;
            }
        }

        /// <summary>
        /// 比较同一 destination 的 deliver tick、正式序列、recipient key 与完整 continuation identity。
        /// </summary>
        internal static int Compare(in PendingCommand left, in PendingCommand right)
        {
            var comparison = left.AvailableTick.CompareTo(right.AvailableTick);
            if (comparison != 0)
                return comparison;
            comparison = left.CommandSequence.CompareTo(right.CommandSequence);
            if (comparison != 0)
                return comparison;
            comparison = GetSemanticPriority(left.CommandKind).CompareTo(
                GetSemanticPriority(right.CommandKind));
            if (comparison != 0)
                return comparison;
            comparison = left.RecipientKindPriority.CompareTo(right.RecipientKindPriority);
            if (comparison != 0)
                return comparison;
            comparison = right.MatchedTagDepth.CompareTo(left.MatchedTagDepth);
            if (comparison != 0)
                return comparison;
            comparison = CompareOwner(in left.SourceAsc, in right.SourceAsc);
            if (comparison != 0)
                return comparison;
            comparison = left.RegistrationSequence.CompareTo(right.RegistrationSequence);
            if (comparison != 0)
                return comparison;
            comparison = CompareContinuation(in left.Continuation, in right.Continuation);
            if (comparison != 0)
                return comparison;
            comparison = left.WakeOrdinal.CompareTo(right.WakeOrdinal);
            return comparison != 0
                ? comparison
                : left.RegistrationGeneration.CompareTo(right.RegistrationGeneration);
        }

        /// <summary>
        /// 比较完整 ASC 稳定身份。
        /// </summary>
        private static int CompareOwner(in OwnerAscHandle left, in OwnerAscHandle right)
        {
            var comparison = left.AscStableId.CompareTo(right.AscStableId);
            return comparison != 0 ? comparison : left.AscGeneration.CompareTo(right.AscGeneration);
        }

        /// <summary>
        /// 比较 Continuation 的 owner、slot 与 generation，不使用 Entity 或遍历位置。
        /// </summary>
        private static int CompareContinuation(
            in AbilityContinuationHandle left,
            in AbilityContinuationHandle right)
        {
            var comparison = CompareOwner(in left.OwnerAsc, in right.OwnerAsc);
            if (comparison != 0)
                return comparison;
            comparison = left.SlotIndex.CompareTo(right.SlotIndex);
            return comparison != 0
                ? comparison
                : left.SlotGeneration.CompareTo(right.SlotGeneration);
        }
    }
}
