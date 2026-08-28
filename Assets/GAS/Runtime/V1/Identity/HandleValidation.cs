namespace GAS.Runtime
{
    /// <summary>
    /// 表示稳定槽句柄按冻结校验顺序遇到的首个失败阶段。
    /// </summary>
    public enum HandleValidationFailure : byte
    {
        None = 0,
        EpochMismatch = 1,
        KindMismatch = 2,
        OwnerMismatch = 3,
        SlotIndexOutOfRange = 4,
        AllocationNotLive = 5,
        GenerationMismatch = 6,
    }

    /// <summary>
    /// 为诊断、序列化和统一校验承载无类型槽身份；业务权威不得保存该类型代替领域句柄。
    /// </summary>
    public readonly struct StableHandleDiagnosticCarrier
    {
        public readonly ulong SimulationEpoch;
        public readonly OwnerAscHandle OwnerAsc;
        public readonly int SlotIndex;
        public readonly uint SlotGeneration;
        public readonly HandleKind Kind;

        /// <summary>
        /// 使用完整槽身份与显式种类创建诊断载体。
        /// </summary>
        public StableHandleDiagnosticCarrier(
            ulong simulationEpoch,
            OwnerAscHandle ownerAsc,
            int slotIndex,
            uint slotGeneration,
            HandleKind kind)
        {
            SimulationEpoch = simulationEpoch;
            OwnerAsc = ownerAsc;
            SlotIndex = slotIndex;
            SlotGeneration = slotGeneration;
            Kind = kind;
        }
    }

    /// <summary>
    /// 以纯值输入按 Epoch、Kind、Owner、Index、Live、Generation 的固定顺序校验稳定槽句柄。
    /// </summary>
    public static class StableHandleValidator
    {
        /// <summary>
        /// 返回稳定槽句柄的首个失败阶段，不查询 Entity 或任何全局状态。
        /// </summary>
        public static HandleValidationFailure Validate(
            in StableHandleDiagnosticCarrier handle,
            ulong expectedSimulationEpoch,
            in OwnerAscHandle expectedOwnerAsc,
            int slotCount,
            bool allocationIsLive,
            uint allocationGeneration,
            HandleKind expectedKind)
        {
            if (handle.SimulationEpoch == 0 || expectedSimulationEpoch == 0 ||
                handle.SimulationEpoch != expectedSimulationEpoch)
                return HandleValidationFailure.EpochMismatch;

            if (!IsValidKind(handle.Kind) || !IsValidKind(expectedKind) || handle.Kind != expectedKind)
                return HandleValidationFailure.KindMismatch;

            if (!handle.OwnerAsc.IsValid || !expectedOwnerAsc.IsValid ||
                !handle.OwnerAsc.Equals(expectedOwnerAsc))
                return HandleValidationFailure.OwnerMismatch;

            if (handle.SlotIndex < 0 || handle.SlotIndex >= slotCount)
                return HandleValidationFailure.SlotIndexOutOfRange;

            if (!allocationIsLive)
                return HandleValidationFailure.AllocationNotLive;

            if (handle.SlotGeneration == 0 || allocationGeneration == 0 ||
                handle.SlotGeneration != allocationGeneration)
                return HandleValidationFailure.GenerationMismatch;

            return HandleValidationFailure.None;
        }

        /// <summary>
        /// 仅接受 Runtime v1 已冻结的具名稳定槽种类，拒绝 None 与越界枚举值。
        /// </summary>
        private static bool IsValidKind(HandleKind kind)
        {
            var value = (byte)kind;
            return value >= (byte)HandleKind.GrantedAbility && value <= (byte)HandleKind.ActiveEffect;
        }
    }

    /// <summary>
    /// 表示 BattleInstance 句柄按 Epoch、StableId、Generation 校验的首个失败阶段。
    /// </summary>
    public enum BattleHandleValidationFailure : byte
    {
        None = 0,
        EpochMismatch = 1,
        StableIdMismatch = 2,
        GenerationMismatch = 3,
    }

    /// <summary>
    /// 仅依据稳定值校验 BattleInstance 生命周期身份。
    /// </summary>
    public static class BattleInstanceHandleValidator
    {
        /// <summary>
        /// 返回 BattleInstance 句柄的首个身份失败阶段。
        /// </summary>
        public static BattleHandleValidationFailure Validate(
            in BattleInstanceHandle handle,
            ulong expectedSimulationEpoch,
            ulong expectedBattleStableId,
            uint expectedBattleGeneration)
        {
            if (handle.SimulationEpoch == 0 || expectedSimulationEpoch == 0 ||
                handle.SimulationEpoch != expectedSimulationEpoch)
                return BattleHandleValidationFailure.EpochMismatch;

            if (handle.BattleStableId == 0 || expectedBattleStableId == 0 ||
                handle.BattleStableId != expectedBattleStableId)
                return BattleHandleValidationFailure.StableIdMismatch;

            if (handle.BattleGeneration == 0 || expectedBattleGeneration == 0 ||
                handle.BattleGeneration != expectedBattleGeneration)
                return BattleHandleValidationFailure.GenerationMismatch;

            return BattleHandleValidationFailure.None;
        }
    }
}
