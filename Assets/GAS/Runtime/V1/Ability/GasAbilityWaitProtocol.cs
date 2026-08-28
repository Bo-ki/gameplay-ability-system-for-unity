namespace GAS.Runtime
{
    /// <summary>
    /// 定义 continuation 可等待的五种冻结语义，调用方不得以通用事件替代这些分支。
    /// </summary>
    public enum GasAbilityWaitSemantic : byte
    {
        Level = 1,
        Edge = 2,
        Event = 3,
        HandleLifecycle = 4,
        Timer = 5,
    }

    /// <summary>
    /// 定义首次匹配后消费 continuation，或保留订阅并持续产生有序唤醒。
    /// </summary>
    public enum GasAbilityWaitPolicy : byte
    {
        OneShot = 1,
        Persistent = 2,
    }

    /// <summary>
    /// 定义 wait 从跨 ASC 注册到确定终态的唯一生命周期。
    /// </summary>
    public enum GasAbilityWaitState : byte
    {
        PendingRegistration = 1,
        Registered = 2,
        Completed = 3,
        Ending = 4,
        Ended = 5,
    }

    /// <summary>
    /// 定义 observed ASC 上订阅槽从注册握手到回收完成的唯一生命周期。
    /// </summary>
    public enum GasAbilitySubscriptionState : byte
    {
        PendingRegistration = 1,
        Registered = 2,
        Completed = 3,
        Ending = 4,
        Ended = 5,
    }

    /// <summary>
    /// 表示 HandleLifecycle 在 sample 时的类型化解析结果。
    /// </summary>
    public enum GasAbilityObservedHandleState : byte
    {
        Live = 1,
        Invalid = 2,
        AlreadyRemoved = 3,
    }

    /// <summary>
    /// 标识一次 wait completion 的业务原因，避免把句柄异常折叠为普通事件。
    /// </summary>
    public enum GasAbilityWaitCompletionReason : byte
    {
        None = 0,
        LevelSatisfied = 1,
        EdgeObserved = 2,
        EventObserved = 3,
        HandleInvalid = 4,
        HandleAlreadyRemoved = 5,
        HandleRemoved = 6,
        TimerDue = 7,
        ObservedOwnerGone = 8,
    }

    /// <summary>
    /// 区分注册后可驱动 wait 的类型化 signal，Event 与 Edge 永远携带单调序号。
    /// </summary>
    public enum GasAbilityWaitSignalKind : byte
    {
        LevelSatisfied = 1,
        EdgeObserved = 2,
        EventObserved = 3,
        HandleRemoved = 4,
        TimerDueEvaluation = 5,
        ObservedOwnerGone = 6,
    }

    /// <summary>
    /// 描述纯协议调用的确定结果，失败与 no-op 均不会隐式改写 record。
    /// </summary>
    public enum GasAbilityWaitProtocolStatus : byte
    {
        PendingRegistration = 1,
        Registered = 2,
        Completed = 3,
        PersistentWake = 4,
        Ending = 5,
        Ended = 6,
        NoOpNotMatched = 7,
        NoOpStale = 8,
        InvalidRequest = 9,
        ResumeTickOverflow = 10,
        WakeOrdinalOverflow = 11,
    }

    /// <summary>
    /// 保存创建 wait 所需的稳定身份与冻结策略，不包含 Entity、World 或引用类型。
    /// </summary>
    public readonly struct GasAbilityWaitRegistrationRequest
    {
        public readonly AbilityActivationHandle Activation;
        public readonly AbilityContinuationHandle Continuation;
        public readonly OwnerAscHandle ObservedAsc;
        public readonly GasAbilityWaitSemantic Semantic;
        public readonly GasAbilityWaitPolicy Mode;
        public readonly ulong DueTick;

        /// <summary>
        /// 使用完整 continuation 身份、observed ASC 与等待策略创建注册请求。
        /// </summary>
        public GasAbilityWaitRegistrationRequest(
            in AbilityActivationHandle activation,
            in AbilityContinuationHandle continuation,
            in OwnerAscHandle observedAsc,
            GasAbilityWaitSemantic semantic,
            GasAbilityWaitPolicy mode,
            ulong dueTick = 0)
        {
            Activation = activation;
            Continuation = continuation;
            ObservedAsc = observedAsc;
            Semantic = semantic;
            Mode = mode;
            DueTick = dueTick;
        }
    }

    /// <summary>
    /// 保存 sample-and-register 的瞬时观测；Edge 与 Event 只冻结当前位置而不回放历史。
    /// </summary>
    public readonly struct GasAbilityWaitRegistrationSample
    {
        public readonly GasAbilityWaitSemantic Semantic;
        public readonly bool LevelSatisfied;
        public readonly ulong ObservedOrdinal;
        public readonly GasAbilityObservedHandleState HandleState;

        /// <summary>
        /// 创建 Level 当前值样本。
        /// </summary>
        public static GasAbilityWaitRegistrationSample ForLevel(bool satisfied)
        {
            return new GasAbilityWaitRegistrationSample(GasAbilityWaitSemantic.Level, satisfied, 0, default);
        }

        /// <summary>
        /// 创建 Edge 当前 baseline 样本，后续只接受更大的 edge 序号。
        /// </summary>
        public static GasAbilityWaitRegistrationSample ForEdge(ulong baseline)
        {
            return new GasAbilityWaitRegistrationSample(GasAbilityWaitSemantic.Edge, false, baseline, default);
        }

        /// <summary>
        /// 创建 Event 当前 watermark 样本，后续只接受更大的 event 序号。
        /// </summary>
        public static GasAbilityWaitRegistrationSample ForEvent(ulong watermark)
        {
            return new GasAbilityWaitRegistrationSample(GasAbilityWaitSemantic.Event, false, watermark, default);
        }

        /// <summary>
        /// 创建 HandleLifecycle 当前解析样本。
        /// </summary>
        public static GasAbilityWaitRegistrationSample ForHandle(GasAbilityObservedHandleState state)
        {
            return new GasAbilityWaitRegistrationSample(
                GasAbilityWaitSemantic.HandleLifecycle,
                false,
                0,
                state);
        }

        /// <summary>
        /// 创建不携带额外观测值的 Timer 样本。
        /// </summary>
        public static GasAbilityWaitRegistrationSample ForTimer()
        {
            return new GasAbilityWaitRegistrationSample(GasAbilityWaitSemantic.Timer, false, 0, default);
        }

        /// <summary>
        /// 只允许各冻结工厂创建形状明确的 sample。
        /// </summary>
        private GasAbilityWaitRegistrationSample(
            GasAbilityWaitSemantic semantic,
            bool levelSatisfied,
            ulong observedOrdinal,
            GasAbilityObservedHandleState handleState)
        {
            Semantic = semantic;
            LevelSatisfied = levelSatisfied;
            ObservedOrdinal = observedOrdinal;
            HandleState = handleState;
        }
    }

    /// <summary>
    /// 携带注册、Ack、wake 与 cancel 必须共同核对的完整稳定身份。
    /// </summary>
    public readonly struct GasAbilityWaitMessageIdentity
    {
        public readonly AbilityActivationHandle Activation;
        public readonly AbilityContinuationHandle Continuation;
        public readonly AbilitySubscriptionHandle Subscription;

        /// <summary>
        /// 使用完整 activation、continuation 与可选 subscription 身份创建消息身份。
        /// </summary>
        public GasAbilityWaitMessageIdentity(
            in AbilityActivationHandle activation,
            in AbilityContinuationHandle continuation,
            in AbilitySubscriptionHandle subscription)
        {
            Activation = activation;
            Continuation = continuation;
            Subscription = subscription;
        }

        /// <summary>
        /// 从当前 record 复制完整消息身份，未分配订阅时保留默认 subscription。
        /// </summary>
        public static GasAbilityWaitMessageIdentity From(in GasAbilityWaitProtocolRecord record)
        {
            return new GasAbilityWaitMessageIdentity(
                in record.Activation,
                in record.Continuation,
                in record.Subscription);
        }
    }

    /// <summary>
    /// 表示 observed ASC 或本地 Timer 发回的一次确定性唤醒候选。
    /// </summary>
    public readonly struct GasAbilityWaitSignal
    {
        public readonly GasAbilityWaitMessageIdentity Identity;
        public readonly GasAbilityWaitSignalKind Kind;
        public readonly ulong CurrentTick;
        public readonly ulong ObservedOrdinal;

        /// <summary>
        /// 创建携带完整身份、发生 tick 与可选观测序号的 signal。
        /// </summary>
        public GasAbilityWaitSignal(
            in GasAbilityWaitMessageIdentity identity,
            GasAbilityWaitSignalKind kind,
            ulong currentTick,
            ulong observedOrdinal = 0)
        {
            Identity = identity;
            Kind = kind;
            CurrentTick = currentTick;
            ObservedOrdinal = observedOrdinal;
        }
    }

    /// <summary>
    /// 保存可直接落入 ASC slab 的 wait 协议状态；所有字段均为 unmanaged 值。
    /// </summary>
    public struct GasAbilityWaitProtocolRecord
    {
        public AbilityActivationHandle Activation;
        public AbilityContinuationHandle Continuation;
        public AbilitySubscriptionHandle Subscription;
        public OwnerAscHandle ObservedAsc;
        public GasAbilityWaitSemantic Semantic;
        public GasAbilityWaitPolicy Mode;
        public GasAbilityWaitState LifecycleState;
        public GasAbilityWaitCompletionReason CompletionReason;
        public ulong DueTick;
        public ulong ObservedOrdinal;
        public ulong WakeOrdinal;
        public ulong CompletionTick;
        public ulong ResumeTick;
    }

    /// <summary>
    /// 返回 wait 状态转移结果与本次 completion 的确定性时序信息。
    /// </summary>
    public readonly struct GasAbilityWaitProtocolResult
    {
        public readonly GasAbilityWaitProtocolStatus Status;
        public readonly GasAbilityWaitCompletionReason CompletionReason;
        public readonly ulong CompletionTick;
        public readonly ulong ResumeTick;
        public readonly ulong WakeOrdinal;

        /// <summary>
        /// 由协议内部创建单次操作结果，调用方只读取而不重构状态。
        /// </summary>
        internal GasAbilityWaitProtocolResult(
            GasAbilityWaitProtocolStatus status,
            GasAbilityWaitCompletionReason completionReason = GasAbilityWaitCompletionReason.None,
            ulong completionTick = 0,
            ulong resumeTick = 0,
            ulong wakeOrdinal = 0)
        {
            Status = status;
            CompletionReason = completionReason;
            CompletionTick = completionTick;
            ResumeTick = resumeTick;
            WakeOrdinal = wakeOrdinal;
        }
    }

    /// <summary>
    /// 实现纯值 wait 协议，统一处理 sample、跨 ASC Ack、唤醒、取消与迟到消息。
    /// </summary>
    public static class GasAbilityWaitProtocol
    {
        /// <summary>
        /// 采样并创建 wait：即时满足直接完成，需要观察者时进入 PendingRegistration，Timer 本地注册。
        /// </summary>
        public static GasAbilityWaitProtocolResult SampleAndRegister(
            in GasAbilityWaitRegistrationRequest request,
            in GasAbilityWaitRegistrationSample sample,
            ulong currentTick,
            out GasAbilityWaitProtocolRecord record)
        {
            record = default;
            if (!IsValidRequest(in request, in sample))
                return Result(GasAbilityWaitProtocolStatus.InvalidRequest);

            record = CreateRecord(in request, in sample);
            switch (request.Semantic)
            {
                case GasAbilityWaitSemantic.Level:
                    return sample.LevelSatisfied
                        ? CompleteNew(ref record, GasAbilityWaitCompletionReason.LevelSatisfied, currentTick)
                        : SetPending(ref record);
                case GasAbilityWaitSemantic.Edge:
                case GasAbilityWaitSemantic.Event:
                    return SetPending(ref record);
                case GasAbilityWaitSemantic.HandleLifecycle:
                    return RegisterHandleWait(ref record, sample.HandleState, currentTick);
                case GasAbilityWaitSemantic.Timer:
                    return currentTick >= request.DueTick
                        ? CompleteNew(ref record, GasAbilityWaitCompletionReason.TimerDue, currentTick)
                        : SetRegistered(ref record);
                default:
                    record = default;
                    return Result(GasAbilityWaitProtocolStatus.InvalidRequest);
            }
        }

        /// <summary>
        /// 接受 observed ASC 分配的订阅句柄；任何迟到、重复或代际不匹配 Ack 均保持 no-op。
        /// </summary>
        public static GasAbilityWaitProtocolResult AcknowledgeRegistration(
            ref GasAbilityWaitProtocolRecord record,
            in GasAbilityWaitMessageIdentity identity)
        {
            if (record.LifecycleState != GasAbilityWaitState.PendingRegistration ||
                !MatchesActivationAndContinuation(in record, in identity) ||
                !IsValidSubscriptionAck(in record, in identity.Subscription))
            {
                return Result(GasAbilityWaitProtocolStatus.NoOpStale);
            }

            record.Subscription = identity.Subscription;
            record.LifecycleState = GasAbilityWaitState.Registered;
            return Result(GasAbilityWaitProtocolStatus.Registered);
        }

        /// <summary>
        /// 处理注册窗口内的一次性 completion，支持 PendingRegistration 直接走向 Completed。
        /// </summary>
        public static GasAbilityWaitProtocolResult CompletePendingRegistration(
            ref GasAbilityWaitProtocolRecord record,
            in GasAbilityWaitSignal signal)
        {
            if (record.LifecycleState != GasAbilityWaitState.PendingRegistration ||
                !MatchesExactIdentityWithoutSubscription(in record, in signal.Identity))
            {
                return Result(GasAbilityWaitProtocolStatus.NoOpStale);
            }

            if (record.Mode == GasAbilityWaitPolicy.Persistent &&
                signal.Kind != GasAbilityWaitSignalKind.ObservedOwnerGone)
            {
                return Result(GasAbilityWaitProtocolStatus.InvalidRequest);
            }

            if (!TryMatch(in record, in signal, out var reason))
                return Result(GasAbilityWaitProtocolStatus.NoOpNotMatched);

            return CompleteMatched(ref record, reason, signal.CurrentTick);
        }

        /// <summary>
        /// 处理 Registered wait 的匹配候选；one-shot 首次消费，persistent 产生单调 WakeOrdinal。
        /// </summary>
        public static GasAbilityWaitProtocolResult Wake(
            ref GasAbilityWaitProtocolRecord record,
            in GasAbilityWaitSignal signal)
        {
            if (record.LifecycleState != GasAbilityWaitState.Registered ||
                !MatchesExactIdentity(in record, in signal.Identity))
            {
                return Result(GasAbilityWaitProtocolStatus.NoOpStale);
            }

            if (!TryMatch(in record, in signal, out var reason))
                return Result(GasAbilityWaitProtocolStatus.NoOpNotMatched);

            if (record.Mode == GasAbilityWaitPolicy.OneShot ||
                signal.Kind == GasAbilityWaitSignalKind.ObservedOwnerGone)
            {
                return CompleteMatched(ref record, reason, signal.CurrentTick);
            }

            return WakePersistent(ref record, reason, in signal);
        }

        /// <summary>
        /// 将尚未完成的 wait 置为 Ending；重复取消与迟到旧代际消息保持 no-op。
        /// </summary>
        public static GasAbilityWaitProtocolResult Cancel(
            ref GasAbilityWaitProtocolRecord record,
            in GasAbilityWaitMessageIdentity identity)
        {
            var cancellable = record.LifecycleState == GasAbilityWaitState.PendingRegistration ||
                              record.LifecycleState == GasAbilityWaitState.Registered;
            if (!cancellable || !MatchesExactIdentity(in record, in identity))
                return Result(GasAbilityWaitProtocolStatus.NoOpStale);

            record.LifecycleState = GasAbilityWaitState.Ending;
            return Result(GasAbilityWaitProtocolStatus.Ending);
        }

        /// <summary>
        /// 在外层完成 unsubscribe 后把 Ending wait 固定为 Ended。
        /// </summary>
        public static GasAbilityWaitProtocolResult FinishEnding(
            ref GasAbilityWaitProtocolRecord record,
            in GasAbilityWaitMessageIdentity identity)
        {
            if (record.LifecycleState != GasAbilityWaitState.Ending ||
                !MatchesExactIdentity(in record, in identity))
            {
                return Result(GasAbilityWaitProtocolStatus.NoOpStale);
            }

            record.LifecycleState = GasAbilityWaitState.Ended;
            return Result(GasAbilityWaitProtocolStatus.Ended);
        }

        /// <summary>
        /// 验证注册请求只使用同一 activation 生命周期的完整稳定句柄与合法 wait 组合。
        /// </summary>
        private static bool IsValidRequest(
            in GasAbilityWaitRegistrationRequest request,
            in GasAbilityWaitRegistrationSample sample)
        {
            if (!request.Activation.IsValid || !request.Continuation.IsValid ||
                request.Activation.SimulationEpoch != request.Continuation.SimulationEpoch ||
                request.Activation.OwnerAsc != request.Continuation.OwnerAsc ||
                request.Semantic != sample.Semantic ||
                !IsKnownSemantic(request.Semantic) || !IsKnownMode(request.Mode))
            {
                return false;
            }

            if (request.Semantic == GasAbilityWaitSemantic.Timer)
                return request.Mode == GasAbilityWaitPolicy.OneShot;

            if (!request.ObservedAsc.IsValid)
                return false;

            return request.Semantic != GasAbilityWaitSemantic.HandleLifecycle ||
                   IsKnownHandleState(sample.HandleState);
        }

        /// <summary>
        /// 判断 wait semantic 是否属于冻结闭集。
        /// </summary>
        private static bool IsKnownSemantic(GasAbilityWaitSemantic semantic)
        {
            return semantic >= GasAbilityWaitSemantic.Level && semantic <= GasAbilityWaitSemantic.Timer;
        }

        /// <summary>
        /// 判断 wait mode 是否属于冻结闭集。
        /// </summary>
        private static bool IsKnownMode(GasAbilityWaitPolicy mode)
        {
            return mode == GasAbilityWaitPolicy.OneShot || mode == GasAbilityWaitPolicy.Persistent;
        }

        /// <summary>
        /// 判断 HandleLifecycle sample 是否返回一种明确解析状态。
        /// </summary>
        private static bool IsKnownHandleState(GasAbilityObservedHandleState state)
        {
            return state >= GasAbilityObservedHandleState.Live &&
                   state <= GasAbilityObservedHandleState.AlreadyRemoved;
        }

        /// <summary>
        /// 从请求与 sample 创建尚未发生状态转移的完整值 record。
        /// </summary>
        private static GasAbilityWaitProtocolRecord CreateRecord(
            in GasAbilityWaitRegistrationRequest request,
            in GasAbilityWaitRegistrationSample sample)
        {
            return new GasAbilityWaitProtocolRecord
            {
                Activation = request.Activation,
                Continuation = request.Continuation,
                ObservedAsc = request.ObservedAsc,
                Semantic = request.Semantic,
                Mode = request.Mode,
                DueTick = request.DueTick,
                ObservedOrdinal = sample.ObservedOrdinal,
            };
        }

        /// <summary>
        /// 根据 HandleLifecycle 初始状态选择注册或类型化即时 completion。
        /// </summary>
        private static GasAbilityWaitProtocolResult RegisterHandleWait(
            ref GasAbilityWaitProtocolRecord record,
            GasAbilityObservedHandleState handleState,
            ulong currentTick)
        {
            if (handleState == GasAbilityObservedHandleState.Invalid)
                return CompleteNew(ref record, GasAbilityWaitCompletionReason.HandleInvalid, currentTick);
            if (handleState == GasAbilityObservedHandleState.AlreadyRemoved)
                return CompleteNew(ref record, GasAbilityWaitCompletionReason.HandleAlreadyRemoved, currentTick);
            return SetPending(ref record);
        }

        /// <summary>
        /// 把新 record 置为 PendingRegistration。
        /// </summary>
        private static GasAbilityWaitProtocolResult SetPending(ref GasAbilityWaitProtocolRecord record)
        {
            record.LifecycleState = GasAbilityWaitState.PendingRegistration;
            return Result(GasAbilityWaitProtocolStatus.PendingRegistration);
        }

        /// <summary>
        /// 把无需订阅的新 record 置为 Registered。
        /// </summary>
        private static GasAbilityWaitProtocolResult SetRegistered(ref GasAbilityWaitProtocolRecord record)
        {
            record.LifecycleState = GasAbilityWaitState.Registered;
            return Result(GasAbilityWaitProtocolStatus.Registered);
        }

        /// <summary>
        /// 完成刚创建的 record；ResumeTick 溢出时撤销整条新记录。
        /// </summary>
        private static GasAbilityWaitProtocolResult CompleteNew(
            ref GasAbilityWaitProtocolRecord record,
            GasAbilityWaitCompletionReason reason,
            ulong currentTick)
        {
            if (!TryResumeTick(currentTick, out var resumeTick))
            {
                record = default;
                return Result(GasAbilityWaitProtocolStatus.ResumeTickOverflow);
            }

            SetCompleted(ref record, reason, currentTick, resumeTick);
            return Result(GasAbilityWaitProtocolStatus.Completed, reason, currentTick, resumeTick);
        }

        /// <summary>
        /// 完成现有 record；任何溢出均保持调用前状态。
        /// </summary>
        private static GasAbilityWaitProtocolResult CompleteMatched(
            ref GasAbilityWaitProtocolRecord record,
            GasAbilityWaitCompletionReason reason,
            ulong currentTick)
        {
            if (!TryResumeTick(currentTick, out var resumeTick))
                return Result(GasAbilityWaitProtocolStatus.ResumeTickOverflow);

            if (record.Mode == GasAbilityWaitPolicy.Persistent)
            {
                if (record.WakeOrdinal == ulong.MaxValue)
                    return Result(GasAbilityWaitProtocolStatus.WakeOrdinalOverflow);
                record.WakeOrdinal++;
            }

            SetCompleted(ref record, reason, currentTick, resumeTick);
            return Result(
                GasAbilityWaitProtocolStatus.Completed,
                reason,
                currentTick,
                resumeTick,
                record.WakeOrdinal);
        }

        /// <summary>
        /// 应用 persistent 唤醒并推进 ordinal；tick 或 ordinal 溢出时不改写 record。
        /// </summary>
        private static GasAbilityWaitProtocolResult WakePersistent(
            ref GasAbilityWaitProtocolRecord record,
            GasAbilityWaitCompletionReason reason,
            in GasAbilityWaitSignal signal)
        {
            if (!TryResumeTick(signal.CurrentTick, out var resumeTick))
                return Result(GasAbilityWaitProtocolStatus.ResumeTickOverflow);
            if (record.WakeOrdinal == ulong.MaxValue)
                return Result(GasAbilityWaitProtocolStatus.WakeOrdinalOverflow);

            record.WakeOrdinal++;
            record.CompletionReason = reason;
            record.CompletionTick = signal.CurrentTick;
            record.ResumeTick = resumeTick;
            if (record.Semantic == GasAbilityWaitSemantic.Edge ||
                record.Semantic == GasAbilityWaitSemantic.Event)
            {
                record.ObservedOrdinal = signal.ObservedOrdinal;
            }

            return Result(
                GasAbilityWaitProtocolStatus.PersistentWake,
                reason,
                signal.CurrentTick,
                resumeTick,
                record.WakeOrdinal);
        }

        /// <summary>
        /// 将 record 固定为 Completed 并保存统一 completion 时序。
        /// </summary>
        private static void SetCompleted(
            ref GasAbilityWaitProtocolRecord record,
            GasAbilityWaitCompletionReason reason,
            ulong completionTick,
            ulong resumeTick)
        {
            record.LifecycleState = GasAbilityWaitState.Completed;
            record.CompletionReason = reason;
            record.CompletionTick = completionTick;
            record.ResumeTick = resumeTick;
        }

        /// <summary>
        /// 按 semantic 判断 signal 是否为首次新匹配，并返回类型化 completion reason。
        /// </summary>
        private static bool TryMatch(
            in GasAbilityWaitProtocolRecord record,
            in GasAbilityWaitSignal signal,
            out GasAbilityWaitCompletionReason reason)
        {
            if (signal.Kind == GasAbilityWaitSignalKind.ObservedOwnerGone)
            {
                var hasObservedOwner = record.Semantic != GasAbilityWaitSemantic.Timer &&
                                       record.ObservedAsc.IsValid;
                reason = hasObservedOwner
                    ? GasAbilityWaitCompletionReason.ObservedOwnerGone
                    : GasAbilityWaitCompletionReason.None;
                return hasObservedOwner;
            }

            reason = GetSemanticReason(record.Semantic);
            switch (record.Semantic)
            {
                case GasAbilityWaitSemantic.Level:
                    return signal.Kind == GasAbilityWaitSignalKind.LevelSatisfied;
                case GasAbilityWaitSemantic.Edge:
                    return signal.Kind == GasAbilityWaitSignalKind.EdgeObserved &&
                           signal.ObservedOrdinal > record.ObservedOrdinal;
                case GasAbilityWaitSemantic.Event:
                    return signal.Kind == GasAbilityWaitSignalKind.EventObserved &&
                           signal.ObservedOrdinal > record.ObservedOrdinal;
                case GasAbilityWaitSemantic.HandleLifecycle:
                    return signal.Kind == GasAbilityWaitSignalKind.HandleRemoved;
                case GasAbilityWaitSemantic.Timer:
                    return signal.Kind == GasAbilityWaitSignalKind.TimerDueEvaluation &&
                           signal.CurrentTick >= record.DueTick;
                default:
                    reason = GasAbilityWaitCompletionReason.None;
                    return false;
            }
        }

        /// <summary>
        /// 将 wait semantic 映射到唯一 completion reason。
        /// </summary>
        private static GasAbilityWaitCompletionReason GetSemanticReason(GasAbilityWaitSemantic semantic)
        {
            switch (semantic)
            {
                case GasAbilityWaitSemantic.Level:
                    return GasAbilityWaitCompletionReason.LevelSatisfied;
                case GasAbilityWaitSemantic.Edge:
                    return GasAbilityWaitCompletionReason.EdgeObserved;
                case GasAbilityWaitSemantic.Event:
                    return GasAbilityWaitCompletionReason.EventObserved;
                case GasAbilityWaitSemantic.HandleLifecycle:
                    return GasAbilityWaitCompletionReason.HandleRemoved;
                case GasAbilityWaitSemantic.Timer:
                    return GasAbilityWaitCompletionReason.TimerDue;
                default:
                    return GasAbilityWaitCompletionReason.None;
            }
        }

        /// <summary>
        /// 验证 Ack subscription 与 activation Epoch、observed ASC 完整匹配。
        /// </summary>
        private static bool IsValidSubscriptionAck(
            in GasAbilityWaitProtocolRecord record,
            in AbilitySubscriptionHandle subscription)
        {
            return subscription.IsValid &&
                   subscription.SimulationEpoch == record.Activation.SimulationEpoch &&
                   subscription.OwnerAsc == record.ObservedAsc;
        }

        /// <summary>
        /// 验证消息的 activation 与 continuation 包含完全相同的稳定身份和代际。
        /// </summary>
        private static bool MatchesActivationAndContinuation(
            in GasAbilityWaitProtocolRecord record,
            in GasAbilityWaitMessageIdentity identity)
        {
            return record.Activation == identity.Activation &&
                   record.Continuation == identity.Continuation;
        }

        /// <summary>
        /// 验证消息同时匹配 activation、continuation 与当前可选 subscription。
        /// </summary>
        private static bool MatchesExactIdentity(
            in GasAbilityWaitProtocolRecord record,
            in GasAbilityWaitMessageIdentity identity)
        {
            if (!MatchesActivationAndContinuation(in record, in identity))
                return false;

            return record.Subscription.IsValid
                ? record.Subscription == identity.Subscription
                : identity.Subscription == default;
        }

        /// <summary>
        /// 验证 PendingRegistration 消息不伪造尚未分配的 subscription。
        /// </summary>
        private static bool MatchesExactIdentityWithoutSubscription(
            in GasAbilityWaitProtocolRecord record,
            in GasAbilityWaitMessageIdentity identity)
        {
            return MatchesActivationAndContinuation(in record, in identity) &&
                   identity.Subscription == default;
        }

        /// <summary>
        /// 计算 completion 最早可恢复的 T+1，并显式拒绝 ulong 回绕。
        /// </summary>
        private static bool TryResumeTick(ulong currentTick, out ulong resumeTick)
        {
            if (currentTick == ulong.MaxValue)
            {
                resumeTick = 0;
                return false;
            }

            resumeTick = currentTick + 1;
            return true;
        }

        /// <summary>
        /// 创建无附加 completion 数据的协议结果。
        /// </summary>
        private static GasAbilityWaitProtocolResult Result(GasAbilityWaitProtocolStatus status)
        {
            return new GasAbilityWaitProtocolResult(status);
        }

        /// <summary>
        /// 创建携带 completion 数据的协议结果。
        /// </summary>
        private static GasAbilityWaitProtocolResult Result(
            GasAbilityWaitProtocolStatus status,
            GasAbilityWaitCompletionReason reason,
            ulong completionTick,
            ulong resumeTick,
            ulong wakeOrdinal = 0)
        {
            return new GasAbilityWaitProtocolResult(
                status,
                reason,
                completionTick,
                resumeTick,
                wakeOrdinal);
        }
    }
}
