using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 表示长期槽位独立于物理 Free/Live/Tombstone 的业务阶段。
    /// </summary>
    public enum GasSlotBusinessState : byte
    {
        Inactive,
        Pending,
        Active,
        Terminal,
    }

    /// <summary>
    /// 保存单类 non-compacting slab 的 free-list 入口与只增 high-water。
    /// </summary>
    public struct GasSlabHead
    {
        public int FreeHeadIndex;
        public int HighWater;
        public int FreeCount;

        /// <summary>
        /// 创建尚未分配槽位且没有 free-list 节点的 slab 头。
        /// </summary>
        public static GasSlabHead CreateEmpty()
        {
            return new GasSlabHead
            {
                FreeHeadIndex = -1,
                HighWater = 0,
                FreeCount = 0,
            };
        }
    }

    /// <summary>
    /// 保存 ASC 的稳定身份；Epoch 与 ASC generation 共同拒绝跨 Session 旧引用。
    /// </summary>
    public struct GasAscIdentity : IComponentData
    {
        public ulong SimulationEpoch;
        public OwnerAscHandle OwnerAsc;
    }

    /// <summary>
    /// 标记 ASC 所属 Pending SpawnBatch，供 manifest 校验与损坏 registry 后的整批兜底清理使用。
    /// </summary>
    public struct GasSpawnBatchMarker : IComponentData, IEnableableComponent
    {
        public ulong SimulationEpoch;
        public ulong SpawnBatchId;
        public ulong ManifestHash;
        public int RegistryOrdinal;
    }

    /// <summary>
    /// 冻结 Ready ASC 所属战局、业务单位与确定性成员顺序，禁止退回 Entity 或 chunk 顺序分组。
    /// </summary>
    public struct AscBattleMembership : IComponentData
    {
        public BattleInstanceHandle BattleInstance;
        public ulong ScenarioUnitId;
        public int SideId;
        public int TeamId;
        public int MembershipOrdinal;
    }

    /// <summary>
    /// 保存 ASC 的业务生命周期与 ingress 终止状态，Entity 存在性不代表 gameplay liveness。
    /// </summary>
    public struct AscLifecycle : IComponentData
    {
        public GasAscLifecycleState State;
        public ulong ReadyTick;
        public ulong TerminalTick;
        public ulong DeathTick;
        public ulong DeathTransitionId;
        public ulong DeathApplicationId;
        public OwnerAscHandle DeathSourceAsc;
        public float DeathOverkill;
        public byte IngressClosed;
    }

    /// <summary>
    /// 保存 ASC 与表现 Actor 的稳定绑定及内部 Entity 快捷映射，稳定身份始终是 gameplay 真值。
    /// </summary>
    public struct GasActorBinding : IComponentData
    {
        public ulong OwnerActorStableId;
        public ulong AvatarActorStableId;
        public uint BindingGeneration;
        internal Entity AvatarEntity;

        /// <summary>
        /// 仅向 Runtime Core 暴露当前 Avatar Entity 快捷映射。
        /// </summary>
        internal readonly Entity ResolveAvatarEntity()
        {
            return AvatarEntity;
        }

        /// <summary>
        /// 仅由 Actor binding owner 更新当前 Avatar Entity 快捷映射。
        /// </summary>
        internal void SetAvatarEntity(Entity entity)
        {
            AvatarEntity = entity;
        }
    }

    /// <summary>
    /// 保存 ASC-local 确定性随机流状态与消费计数，禁止依赖全局随机源。
    /// </summary>
    public struct AscRandomState : IComponentData
    {
        public ulong State0;
        public ulong State1;
        public ulong DrawCount;
    }

    /// <summary>
    /// 汇总 ASC 内所有 non-compacting slab 的独立 free-list 与 high-water 元数据。
    /// </summary>
    public struct AscSlabHeads : IComponentData
    {
        public GasSlabHead GrantedAbility;
        public GasSlabHead AbilityActivation;
        public GasSlabHead AbilityContinuation;
        public GasSlabHead AbilitySubscription;
        public GasSlabHead CooldownGate;
        public GasSlabHead ActivationOwnedContribution;
        public GasSlabHead EmittedApplicationRef;
        public GasSlabHead ActiveEffect;
        public GasSlabHead AttributeAggregator;
        public GasSlabHead LiveDependency;
        public GasSlabHead LiveDependencyRoute;
        public GasSlabHead PendingCommand;

        /// <summary>
        /// 创建所有 free-list 为空且 high-water 为零的 ASC slab 元数据。
        /// </summary>
        public static AscSlabHeads CreateEmpty()
        {
            return new AscSlabHeads
            {
                GrantedAbility = GasSlabHead.CreateEmpty(),
                AbilityActivation = GasSlabHead.CreateEmpty(),
                AbilityContinuation = GasSlabHead.CreateEmpty(),
                AbilitySubscription = GasSlabHead.CreateEmpty(),
                CooldownGate = GasSlabHead.CreateEmpty(),
                ActivationOwnedContribution = GasSlabHead.CreateEmpty(),
                EmittedApplicationRef = GasSlabHead.CreateEmpty(),
                ActiveEffect = GasSlabHead.CreateEmpty(),
                AttributeAggregator = GasSlabHead.CreateEmpty(),
                LiveDependency = GasSlabHead.CreateEmpty(),
                LiveDependencyRoute = GasSlabHead.CreateEmpty(),
                PendingCommand = GasSlabHead.CreateEmpty(),
            };
        }
    }

    /// <summary>
    /// 保存 dense AttributeLayout 对应的 Base、Current 与权威 revision。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AttributeValueSlot : IBufferElementData
    {
        public float Base;
        public float Current;
        public uint Revision;
    }

    /// <summary>
    /// 保存固定长度 Attribute dirty 位图，其长度由 GasCatalogRegistry 唯一确定。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AttributeDirtyWord : IBufferElementData
    {
        public ulong Value;
    }

    /// <summary>
    /// 保存 dense TagCatalog 对应的 exact 与含 ancestor 聚合计数。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TagCountSlot : IBufferElementData
    {
        public int ExactCount;
        public int InclusiveCount;
    }

    /// <summary>
    /// 保存固定长度 Tag presence 派生位图，其长度由 GasCatalogRegistry 唯一确定。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TagPresenceWord : IBufferElementData
    {
        public ulong Value;
    }

    /// <summary>
    /// 暂存 SpawnBatch 中待验证的 Attribute 初值引用，Finalize 前不得写 gameplay authority。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct PendingAttributeInitialization : IBufferElementData
    {
        public int LayoutIndex;
        public int ConfigOrdinal;
        /// <summary>
        /// 标记该初始化记录是否携带显式 Base/Current 初值；未标记时沿用 Catalog 默认值。
        /// </summary>
        public byte HasExplicitValue;
        public float BaseValue;
        public float CurrentValue;
    }

    /// <summary>
    /// 暂存 SpawnBatch 中待验证的 Tag 初值引用，Finalize 前不得写 gameplay authority。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct PendingTagInitialization : IBufferElementData
    {
        public int LayoutIndex;
        public int ConfigOrdinal;
    }

    /// <summary>
    /// 暂存 SpawnBatch 中待验证的初始 Ability grant 引用，Finalize 前不得写 gameplay authority。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct PendingGrantedAbilityInitialization : IBufferElementData
    {
        public int LayoutIndex;
        public int ConfigOrdinal;
    }

    /// <summary>
    /// 保存 SpawnBatch 成功发布后注入首个 gameplay Tick 的初始 GameplayEffect；只允许无 payload 的闭世界定义。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct PendingInitialGameplayEffect : IBufferElementData
    {
        public int DefinitionId;
        public int ConfigOrdinal;
        public ulong CausalityId;
        public BoundaryTargetRef Target;
    }

    /// <summary>
    /// 保存不会因撤销或重授而移动的 Ability grant 长期槽。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct GrantedAbilitySlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public GrantedAbilityHandle Handle;
        public int DefinitionId;
        public int DefinitionIndex;
        public int DefinitionVersion;
        public ulong DefinitionHash;
        public int Level;
        public int InputBindingId;
        public int GrantOrdinal;
        public GasAbilityGrantSourceKind GrantSourceKind;
        public ulong GrantSourceStableId;
        public ActiveEffectHandle GrantingActiveEffect;
        public ulong GrantApplicationId;
        public ulong GrantContextId;
        public int ChildActivationCount;
        public GasGrantedAbilityRemovalPolicy RemovalPolicy;
        public GasGrantedAbilityRemovalState RemovalState;
        public byte ProvenanceDetached;
    }

    /// <summary>
    /// 保存 Ability 激活生命周期及其稳定句柄的 non-compacting 长期槽。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AbilityActivationSlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public AbilityActivationHandle Handle;
        public GrantedAbilityHandle GrantedAbility;
        public ulong CausalityId;
        public ulong ActivationSequence;
        public ulong StartTick;
        public ulong CommitSequence;
        public ulong EventDataId;
        public ulong TargetDataId;
        public ulong EffectContextId;
        public uint SourceAvatarBindingGeneration;
        public int ContinuationCount;
        public int OwnedContributionStart;
        public int OwnedContributionCount;
        public int EmittedApplicationStart;
        public int EmittedApplicationCount;
        public GasAbilityActivationPhase Phase;
        public GasAbilityEndReason EndReason;
        public GasAbilityCommandResult LastCommandResult;
        public byte WasCancelled;
    }

    /// <summary>
    /// 保存 Ability continuation 的恢复时点与冻结 payload range，槽索引在生命周期内不移动。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AbilityContinuationSlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public AbilityContinuationHandle Handle;
        public AbilityActivationHandle Activation;
        public OwnerAscHandle ObservedAsc;
        public GasAbilityObservedHandle ObservedHandle;
        public AbilitySubscriptionHandle Subscription;
        public PayloadRangeHandle PayloadRange;
        public ulong TargetDataId;
        public ulong DueTick;
        public ulong ResumeTick;
        public ulong WakeOrdinal;
        public ulong ObservedBaseline;
        public int ProgramCounter;
        public int InstanceNameId;
        public int QueryKey;
        public uint RegistrationGeneration;
        public GasAbilityWaitSemantic WaitSemantic;
        public GasAbilityWaitPolicy WaitPolicy;
        public GasAbilityWaitState WaitState;
        public GasAbilityWaitCompletionReason CompletionReason;
    }

    /// <summary>
    /// 保存 Ability 对确定性事件类别的长期订阅，不以结构变化表达启停。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AbilitySubscriptionSlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public AbilitySubscriptionHandle Handle;
        public OwnerAscHandle ObservedAsc;
        public OwnerAscHandle SubscriberAsc;
        public AbilityActivationHandle Activation;
        public AbilityContinuationHandle Continuation;
        public GasAbilityObservedHandle ObservedHandle;
        public ulong ObservedRevisionAtRegister;
        public ulong RegistrationSequence;
        public ulong WakeOrdinal;
        public int QueryKey;
        public uint RegistrationGeneration;
        public GasAbilityWaitSemantic WaitSemantic;
        public GasAbilityWaitPolicy WaitPolicy;
        public GasAbilitySubscriptionState State;
    }

    /// <summary>
    /// 保存 owner ASC 的长期冷却门，Activation 结束或取消不会隐式释放该槽。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CooldownGateSlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public CooldownGateHandle Handle;
        public GrantedAbilityHandle GrantedAbility;
        public AbilityActivationHandle SourceCommitActivation;
        public int GateKey;
        public int AbilityDefinitionId;
        public int OwnedTagIndex;
        public ulong StartTick;
        public ulong EndTick;
        public GasSlotBusinessState State;
    }

    /// <summary>
    /// 保存 Activation 拥有的长期贡献引用，供终止路径按稳定槽回收而不扫描压缩数组。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ActivationOwnedContributionSlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public AbilityActivationHandle Activation;
        public ActiveEffectHandle ActiveEffect;
        public ulong ApplicationId;
        public int AttributeLayoutIndex;
        public int TagIndex;
        public float Magnitude;
        public GasAbilityContributionKind Kind;
        public GasSlotBusinessState State;
    }

    /// <summary>
    /// 保存 Activation 已发出 application 的稳定反向引用，便于取消与 teardown 有界遍历。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct EmittedApplicationRefSlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public AbilityActivationHandle Activation;
        public OwnerAscHandle TargetAsc;
        public ActiveEffectHandle ActiveEffect;
        public ulong EffectSpecId;
        public ulong ApplicationId;
        public GasEmittedApplicationCleanupPolicy CleanupPolicy;
        public GasSlotBusinessState State;
    }

    /// <summary>
    /// 保存 Target ASC 上长期 GameplayEffect 的时序、stack 与冻结 payload/capture ranges。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ActiveEffectSlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public ActiveEffectHandle Handle;
        public OwnerAscHandle SourceAsc;
        public int DefinitionIndex;
        public ulong StartTick;
        public ulong EndTick;
        public ulong NextPeriodTick;
        /// <summary>
        /// 保存创建或最近一次成功 stack application 的正式 EffectApplicationId，供 period provenance 复用。
        /// </summary>
        public ulong ApplicationId;
        public int StackCount;
        public uint ActiveCycleOrdinal;
        /// <summary>
        /// 保存该 ActiveEffect 已成功 claim 的 period execution ordinal，重复扫描不得复用。
        /// </summary>
        public uint PeriodExecutionOrdinal;
        public PayloadRangeHandle PayloadRange;
        public PayloadRangeHandle CaptureRange;
        /// <summary>
        /// 保存 RequireSameAvatar application 成功时的冻结 Avatar 身份，period 不重新采样。
        /// </summary>
        public ulong TargetAvatarStableId;
        public uint TargetAvatarBindingGeneration;
        /// <summary>
        /// 保存 FrozenSpatial application 的不可变 TargetData，直到 ActiveEffect 终止。
        /// </summary>
        public GasBoundarySpatialSnapshot SpatialSnapshot;
        public GasSlotBusinessState State;
        public byte Inhibited;
    }

    /// <summary>
    /// 保存单属性 aggregator 的长期稳定节点及最后提交的派生值。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AttributeAggregatorSlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public int AttributeLayoutIndex;
        public float AggregatedValue;
        public uint Revision;
        public GasSlotBusinessState State;
    }

    /// <summary>
    /// 保存 live capture producer 到 consumer 的稳定依赖记录，不以 raw Entity 表达 owner。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct LiveDependencySlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public OwnerAscHandle ProducerAsc;
        public int ProducerAttributeLayoutIndex;
        public ActiveEffectHandle ConsumerEffect;
        public int ConsumerFieldOrdinal;
        public uint ObservedRevision;
        public GasSlotBusinessState State;
    }

    /// <summary>
    /// 保存跨 ASC live dependency 的确定性传播路由及目标 dependency generation。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct LiveDependencyRouteSlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public OwnerAscHandle SourceAsc;
        public OwnerAscHandle TargetAsc;
        public int DependencySlotIndex;
        public uint DependencySlotGeneration;
        public GasSlotBusinessState State;
    }

    /// <summary>
    /// 保存跨 tick 延迟执行的内部命令；它与 Boundary ingress journal 保持独立来源和生命周期。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct PendingCommand : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public BattleInstanceHandle BattleInstance;
        public OwnerAscHandle SourceAsc;
        public OwnerAscHandle TargetAsc;
        public GrantedAbilityHandle GrantedAbility;
        public AbilityActivationHandle Activation;
        public AbilityContinuationHandle Continuation;
        public AbilitySubscriptionHandle Subscription;
        public GasAbilityObservedHandle ObservedHandle;
        public PayloadRangeHandle PayloadRange;
        public ulong AvailableTick;
        public ulong CommandSequence;
        public ulong RegistrationSequence;
        public ulong DueTick;
        public ulong CompletionTick;
        public ulong ResumeTick;
        public ulong WakeOrdinal;
        public ulong ObservedRevision;
        public int QueryKey;
        public int MatchedTagDepth;
        public uint RegistrationGeneration;
        public ushort RecipientKindPriority;
        public int CommandKind;
        public GasGrantedAbilityRemovalPolicy GrantedRemovalPolicy;
        public GasAbilityGrantSourceKind GrantedRemovalSourceKind;
        public ulong GrantedRemovalSourceStableId;
        public ActiveEffectHandle GrantedRemovalActiveEffect;
        public ulong GrantedRemovalApplicationId;
        public ulong GrantedRemovalContextId;
        public GasAbilityWaitSemantic WaitSemantic;
        public GasAbilityWaitPolicy WaitPolicy;
        public GasAbilityWaitSignalKind WaitSignalKind;
        public GasAbilityWaitCompletionReason CompletionReason;
        public GasSlotBusinessState State;
    }
}
