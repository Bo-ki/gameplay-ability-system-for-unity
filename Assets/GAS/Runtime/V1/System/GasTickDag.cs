using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    /// <summary>
    /// 集中组装 Runtime v1 唯一 Tick Job DAG，禁止通过主线程 Complete 读取中间结果决定后续调度。
    /// </summary>
    internal static class GasTickDag
    {
        /// <summary>
        /// 为所有未 Disposed Kernel update 预排 cleanup maintenance 前置依赖。
        /// </summary>
        internal static JobHandle ScheduleCleanupAcceptedPrepass(JobHandle dependency)
        {
            return new GasCleanupAcceptedPrepassJob().Schedule(dependency);
        }

        /// <summary>
        /// 按 immutable ScaleProfile 创建当前 Tick 全部定长 scratch。
        /// </summary>
        internal static GasTickScratch CreateScratch(
            ref SystemState state,
            in GasScaleProfile profile)
        {
            return GasTickScratch.Create(in profile, state.WorldUpdateAllocator);
        }

        /// <summary>
        /// 在 SpawnFinalize 前以 Job 冻结 lifecycle dispatch，避免主线程读取可变状态形成隐式 fence。
        /// </summary>
        internal static JobHandle ScheduleModeCapture(
            ref SystemState state,
            Entity session,
            in GasTickScratch scratch,
            JobHandle dependency)
        {
            return new GasCaptureKernelModeJob
            {
                Session = session,
                Lifecycles = state.GetComponentLookup<GasSessionLifecycle>(true),
                Execution = scratch.Execution,
            }.Schedule(dependency);
        }

        /// <summary>
        /// 使用已冻结 dispatch 和完整 scratch，无条件排出 Gather 到 TickFinalize 的完整依赖链。
        /// </summary>
        internal static JobHandle ScheduleGameplay(
            ref SystemState state,
            Entity session,
            in GasSessionIdentity identity,
            in GasScaleProfile profile,
            in GasDefinitionRegistry definitions,
            in GasTickScratch scratch,
            JobHandle dependency)
        {
            dependency = SchedulePlanAndAdmission(
                ref state,
                session,
                identity.SimulationEpoch,
                in profile,
                definitions.Catalog,
                in scratch,
                dependency);
            dependency = ScheduleDownstream(in scratch, dependency);
            return ScheduleFinalize(ref state, session, in scratch, dependency);
        }

        /// <summary>
        /// 按 Gather、Provision、OwnerPlan、TargetResolve、Admission、FaultLatch 顺序排出零权威规划链。
        /// </summary>
        private static JobHandle SchedulePlanAndAdmission(
            ref SystemState state,
            Entity session,
            ulong simulationEpoch,
            in GasScaleProfile profile,
            BlobAssetReference<GasDefinitionCatalogBlob> catalog,
            in GasTickScratch scratch,
            JobHandle dependency)
        {
            dependency = new GasGatherTickStartSnapshotJob
            {
                Session = session,
                Ticks = state.GetComponentLookup<SimulationTickState>(true),
                Inboxes = state.GetBufferLookup<BoundaryCommandInbox>(),
                FrozenPayloads = state.GetBufferLookup<BoundaryCommandFrozenPayload>(true),
                Execution = scratch.Execution,
                SealedCommands = scratch.SealedCommands,
            }.Schedule(dependency);
            dependency = new GasPlanExpandScratchProvisionJob
            {
                Catalog = catalog,
                Profile = profile,
                Execution = scratch.Execution,
                Envelope = scratch.Envelope,
            }.Schedule(dependency);
            dependency = new GasOwnerPlanBuildJob
            {
                SealedCommands = scratch.SealedCommands,
                Envelope = scratch.Envelope,
                OwnerPlans = scratch.OwnerPlans,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasTargetResolveExpandJob
            {
                Envelope = scratch.Envelope,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            dependency = new GasWholeTickInfrastructureAdmissionJob
            {
                Profile = profile,
                Envelope = scratch.Envelope,
                Admission = scratch.Admission,
                Execution = scratch.Execution,
            }.Schedule(dependency);
            return ScheduleFaultLatch(
                ref state,
                session,
                simulationEpoch,
                in scratch,
                dependency);
        }

        /// <summary>
        /// 在 admission 后紧接唯一 fault writer，使后续预排 lane 只读取 AdmissionResult 决定 no-op。
        /// </summary>
        private static JobHandle ScheduleFaultLatch(
            ref SystemState state,
            Entity session,
            ulong simulationEpoch,
            in GasTickScratch scratch,
            JobHandle dependency)
        {
            return new GasFaultLatchJob
            {
                Session = session,
                SimulationEpoch = simulationEpoch,
                Admission = scratch.Admission,
                Execution = scratch.Execution,
                FaultLatches = state.GetComponentLookup<SessionFaultLatch>(),
                Lifecycles = state.GetComponentLookup<GasSessionLifecycle>(),
            }.Schedule(dependency);
        }

        /// <summary>
        /// 无条件排出 admission 后九个 gameplay/投影节点，失败时各节点统一入口 no-op。
        /// </summary>
        private static JobHandle ScheduleDownstream(
            in GasTickScratch scratch,
            JobHandle dependency)
        {
            dependency = new GasAscOwnerCommandWaveJob
                { Admission = scratch.Admission, Execution = scratch.Execution }.Schedule(dependency);
            dependency = new GasSourceSpecProjectionJob
                { Admission = scratch.Admission, Execution = scratch.Execution }.Schedule(dependency);
            dependency = new GasGroupWorkByTargetJob
                { Admission = scratch.Admission, Execution = scratch.Execution }.Schedule(dependency);
            dependency = new GasAscTargetStateWaveJob
                { Admission = scratch.Admission, Execution = scratch.Execution }.Schedule(dependency);
            dependency = new GasTargetLocalStabilizationDeathJob
                { Admission = scratch.Admission, Execution = scratch.Execution }.Schedule(dependency);
            dependency = new GasStableFactMergeTerminalResolveJob
                { Admission = scratch.Admission, Execution = scratch.Execution }.Schedule(dependency);
            dependency = new GasGroupNextTickRouteByDestinationJob
                { Admission = scratch.Admission, Execution = scratch.Execution }.Schedule(dependency);
            dependency = new GasBoundaryProjectJob
                { Admission = scratch.Admission, Execution = scratch.Execution }.Schedule(dependency);
            return new GasRecordEndFixedJob
                { Admission = scratch.Admission, Execution = scratch.Execution }.Schedule(dependency);
        }

        /// <summary>
        /// 在完整 DAG 尾部仅按 AdmissionResult 推进成功 Tick，并发布 lane 诊断与 consume 控制状态。
        /// </summary>
        private static JobHandle ScheduleFinalize(
            ref SystemState state,
            Entity session,
            in GasTickScratch scratch,
            JobHandle dependency)
        {
            return new GasTickFinalizeJob
            {
                Session = session,
                Admission = scratch.Admission,
                Execution = scratch.Execution,
                Ticks = state.GetComponentLookup<SimulationTickState>(),
                Lifecycles = state.GetComponentLookup<GasSessionLifecycle>(),
                Diagnostics = state.GetComponentLookup<GasTickDiagnostics>(),
                Inboxes = state.GetBufferLookup<BoundaryCommandInbox>(),
            }.Schedule(dependency);
        }
    }
}
