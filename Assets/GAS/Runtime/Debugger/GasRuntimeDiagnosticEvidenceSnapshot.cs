namespace GAS.Runtime
{
    /// <summary>
    /// 保存 Runtime v1 Boundary read model 的工作量证据；不读取 ECS buffer，也不拥有 gameplay 状态。
    /// </summary>
    public readonly struct GasRuntimeWorkloadEvidenceSnapshot
    {
        public readonly int FactCount;
        public readonly int PresentationCount;

        /// <summary>
        /// 创建工作量证据快照。
        /// </summary>
        public GasRuntimeWorkloadEvidenceSnapshot(int factCount, int presentationCount)
        {
            FactCount = factCount;
            PresentationCount = presentationCount;
        }
    }

    /// <summary>
    /// 保存 Runtime v1 帧骨架证据；渲染禁用原因只能来自 managed 诊断输入。
    /// </summary>
    public readonly struct GasRuntimeFrameBackboneEvidenceSnapshot
    {
        public readonly int RenderDisabledReasonCount;

        /// <summary>
        /// 创建帧骨架证据快照。
        /// </summary>
        public GasRuntimeFrameBackboneEvidenceSnapshot(int renderDisabledReasonCount)
        {
            RenderDisabledReasonCount = renderDisabledReasonCount;
        }
    }

    /// <summary>
    /// 汇总 Runtime v1 的纯值诊断证据，供报告与表现层只读消费。
    /// </summary>
    public readonly struct GasRuntimeV1DiagnosticEvidenceSnapshot
    {
        public readonly GasRuntimeWorkloadEvidenceSnapshot Workload;
        public readonly GasRuntimeFrameBackboneEvidenceSnapshot FrameBackbone;

        /// <summary>
        /// 创建纯值诊断证据。
        /// </summary>
        public GasRuntimeV1DiagnosticEvidenceSnapshot(
            in GasRuntimeWorkloadEvidenceSnapshot workload,
            in GasRuntimeFrameBackboneEvidenceSnapshot frameBackbone)
        {
            Workload = workload;
            FrameBackbone = frameBackbone;
        }

        /// <summary>
        /// 返回没有任何诊断事实的空证据。
        /// </summary>
        public static GasRuntimeV1DiagnosticEvidenceSnapshot Empty =>
            new GasRuntimeV1DiagnosticEvidenceSnapshot(
                new GasRuntimeWorkloadEvidenceSnapshot(0, 0),
                new GasRuntimeFrameBackboneEvidenceSnapshot(0));
    }

    /// <summary>
    /// Runtime v1 诊断只读快照；它是 Boundary staging 的派生值，不是 ECS 权威状态。
    /// </summary>
    public readonly struct GasRuntimeV1DiagnosticSnapshot
    {
        public readonly GasRuntimeMetricFamilySnapshot MetricFamilies;
        public readonly GasRuntimeV1DiagnosticEvidenceSnapshot Evidence;

        /// <summary>
        /// 创建诊断快照。
        /// </summary>
        public GasRuntimeV1DiagnosticSnapshot(
            in GasRuntimeMetricFamilySnapshot metricFamilies,
            in GasRuntimeV1DiagnosticEvidenceSnapshot evidence)
        {
            MetricFamilies = metricFamilies;
            Evidence = evidence;
        }

        /// <summary>
        /// 返回没有任何旧 Debugger/ECS 依赖的空 v1 诊断快照。
        /// </summary>
        public static GasRuntimeV1DiagnosticSnapshot Empty =>
            new GasRuntimeV1DiagnosticSnapshot(
                GasRuntimeMetricFamilySnapshot.Empty,
                GasRuntimeV1DiagnosticEvidenceSnapshot.Empty);
    }
}
