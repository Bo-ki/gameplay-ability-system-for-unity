using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    /// <summary>
    /// 标识一个 World 内唯一 active GAS Session，并以 Epoch 阻断跨 World 旧引用。
    /// </summary>
    public struct GasSessionIdentity : IComponentData
    {
        public ulong SimulationEpoch;
    }

    /// <summary>
    /// 标记仍拥有本 World gameplay authority 的唯一 Session；诊断保留实体不得携带该标记。
    /// </summary>
    public struct GasActiveSessionAuthority : IComponentData
    {
    }

    /// <summary>
    /// 保存 Session 安装后不可热改的 tick 与规则配置。
    /// </summary>
    public struct GasSessionConfig : IComponentData
    {
        public int TickRate;
        public int RuleVersion;
        public int BoundaryPolicyVersion;
    }

    /// <summary>
    /// 保存 Session 唯一 Definition Catalog Blob 与其版本化哈希。
    /// </summary>
    public struct GasDefinitionRegistry : IComponentData
    {
        public BlobAssetReference<GasDefinitionCatalogBlob> Catalog;
        public int SchemaVersion;
        public ulong SchemaHash;
        public ulong ContentHash;

        /// <summary>
        /// 从已创建的 immutable Catalog 构造 Session registry 快照。
        /// </summary>
        public static GasDefinitionRegistry Create(BlobAssetReference<GasDefinitionCatalogBlob> catalog)
        {
            if (!catalog.IsCreated)
                return default;

            ref var root = ref catalog.Value;
            return new GasDefinitionRegistry
            {
                Catalog = catalog,
                SchemaVersion = root.SchemaVersion,
                SchemaHash = root.SchemaHash,
                ContentHash = root.ContentHash,
            };
        }
    }

    /// <summary>
    /// 冻结 AttributeLayout 与 TagCatalog 的固定长度和独立哈希，供 ASC layout 验证使用。
    /// </summary>
    public struct GasCatalogRegistry : IComponentData
    {
        public int AttributeCount;
        public int AttributeDirtyWordCount;
        public int TagCount;
        public int TagPresenceWordCount;
        public ulong AttributeLayoutHash;
        public ulong TagCatalogHash;

        /// <summary>
        /// 从 Definition Catalog 根投影固定逻辑长度与 layout/catalog 哈希。
        /// </summary>
        public static GasCatalogRegistry Create(ref GasDefinitionCatalogBlob catalog)
        {
            var attributeCount = catalog.AttributeLayout.Entries.Length;
            var tagCount = catalog.TagCatalog.Entries.Length;
            return new GasCatalogRegistry
            {
                AttributeCount = attributeCount,
                AttributeDirtyWordCount = CalculateWordCount(attributeCount),
                TagCount = tagCount,
                TagPresenceWordCount = CalculateWordCount(tagCount),
                AttributeLayoutHash = catalog.AttributeLayout.LayoutHash,
                TagCatalogHash = catalog.TagCatalog.CatalogHash,
            };
        }

        /// <summary>
        /// 将 dense 元素数量转换为 64 位派生位图的固定逻辑长度。
        /// </summary>
        private static int CalculateWordCount(int elementCount)
        {
            return elementCount <= 0 ? 0 : ((elementCount - 1) / 64) + 1;
        }
    }

    /// <summary>
    /// 保存当前 gameplay SimulationTick 与确定性序列根。
    /// </summary>
    public struct SimulationTickState : IComponentData
    {
        public ulong CurrentTick;
        public ulong NextStableSequence;
    }

    /// <summary>
    /// 保存 Session 生命周期及当前唯一 in-flight SpawnBatch 身份。
    /// </summary>
    public struct GasSessionLifecycle : IComponentData
    {
        public GasSessionLifecycleState State;
        public ulong ActiveSpawnBatchId;
    }

    /// <summary>
    /// 冻结 SpawnBatch 原始规模与内容哈希，使被同步裁短的自洽子集也必须整批失败。
    /// </summary>
    public struct GasSpawnBatchManifest : IComponentData
    {
        public ulong SimulationEpoch;
        public ulong SpawnBatchId;
        public int ExpectedBattleCount;
        public int ExpectedAscCount;
        public int ExpectedAttributeInitializationCount;
        public int ExpectedTagInitializationCount;
        public int ExpectedGrantedAbilityInitializationCount;
        public ulong ContentHash;
        public byte Pending;
    }

    /// <summary>
    /// 持久锁存 Session fault 与 ingress close 证据，fault 后不得恢复 gameplay 写入。
    /// </summary>
    public struct SessionFaultLatch : IComponentData
    {
        public ulong FaultId;
        public ulong FaultEpoch;
        public ulong FaultTick;
        public int ReasonCode;
        public byte Detected;
        public byte IngressClosed;
        public ulong SealedFirstRequestSequence;
        public ulong SealedLastRequestSequence;
        public int SealedRequestCount;
        public ulong SealedRequestHash;
        public ulong OutstandingFirstRequestSequence;
        public ulong OutstandingLastRequestSequence;
        public int OutstandingRequestCount;
        public ulong OutstandingRequestHash;
    }

    /// <summary>
    /// 保存 Session-local 非压缩 BattleInstance registry 槽及其独立终局状态。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct BattleInstanceSlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public BattleInstanceHandle Handle;
        public ulong BattleInstanceId;
        public ulong BattleLocalTick;
        public int MemberStart;
        public int MemberCount;
        public int ReadyMemberCount;
        public int MembershipOrdinalRoot;
        public int OutcomeCode;
        public GasBattleInstanceState State;
        public byte IngressClosed;
    }

    /// <summary>
    /// 保存 ASC stable handle 到内部 Entity 的唯一 Session registry 映射。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AscRegistrySlot : IBufferElementData
    {
        public GasSlabSlotHeader Header;
        public OwnerAscHandle OwnerAsc;
        internal Entity RuntimeEntity;
        public BattleInstanceHandle BattleInstance;
        public int RegistryOrdinal;
        public ulong SpawnBatchId;
        public ulong ReadyTick;
        public GasAscRegistryState State;

        /// <summary>
        /// 仅向 Runtime Core 暴露 registry 内部 Entity 映射，Boundary/public protocol 不得调用。
        /// </summary>
        internal readonly Entity ResolveRuntimeEntity()
        {
            return RuntimeEntity;
        }

        /// <summary>
        /// 仅由 Session registry owner 设置内部 Entity 映射。
        /// </summary>
        internal void SetRuntimeEntity(Entity entity)
        {
            RuntimeEntity = entity;
        }
    }

    /// <summary>
    /// 保存 Pending batch 的不可变成员与初始化 ranges；Ready 后清空，ASC registry 仍是唯一长期 Entity 映射。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SpawnBatchMemberManifestSlot : IBufferElementData
    {
        public OwnerAscHandle OwnerAsc;
        public BattleInstanceHandle BattleInstance;
        public int RegistryOrdinal;
        public ulong ScenarioUnitId;
        public int SideId;
        public int TeamId;
        public int MembershipOrdinal;
        public ulong OwnerActorStableId;
        public ulong AvatarActorStableId;
        public uint ActorBindingGeneration;
        public ulong RandomState0;
        public ulong RandomState1;
        public int AttributeInitializationStart;
        public int AttributeInitializationCount;
        public int TagInitializationStart;
        public int TagInitializationCount;
        public int GrantedAbilityInitializationStart;
        public int GrantedAbilityInitializationCount;
        internal Entity RuntimeEntity;

        /// <summary>
        /// 仅供 SpawnFinalize 与失败清理解析 Pending batch 的临时 Entity 映射。
        /// </summary>
        internal readonly Entity ResolveRuntimeEntity()
        {
            return RuntimeEntity;
        }

        /// <summary>
        /// 仅由 bootstrap recorder 写入 Pending batch 的临时 Entity 映射。
        /// </summary>
        internal void SetRuntimeEntity(Entity entity)
        {
            RuntimeEntity = entity;
        }
    }

    /// <summary>
    /// 以固定字段顺序生成 SpawnBatch 的平台无关 FNV-1a 内容哈希。
    /// </summary>
    internal struct GasSpawnBatchManifestHasher
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        private ulong _value;

        /// <summary>
        /// 以 manifest header 初始化内容哈希，确保长度变化不能与空尾部等价。
        /// </summary>
        internal static GasSpawnBatchManifestHasher Create(in GasSpawnBatchManifest manifest)
        {
            var hasher = new GasSpawnBatchManifestHasher { _value = OffsetBasis };
            hasher.Add(manifest.SimulationEpoch);
            hasher.Add(manifest.SpawnBatchId);
            hasher.Add(manifest.ExpectedBattleCount);
            hasher.Add(manifest.ExpectedAscCount);
            hasher.Add(manifest.ExpectedAttributeInitializationCount);
            hasher.Add(manifest.ExpectedTagInitializationCount);
            hasher.Add(manifest.ExpectedGrantedAbilityInitializationCount);
            return hasher;
        }

        /// <summary>
        /// 加入 Session 规则、版本化容量档位与 Catalog 身份，禁止 finalize 重新解释另一套安装输入。
        /// </summary>
        internal void AddSessionAuthority(
            in GasSessionConfig config,
            in GasScaleProfile profile,
            in GasDefinitionRegistry definitions,
            in GasCatalogRegistry catalog)
        {
            Add(config.TickRate);
            Add(config.RuleVersion);
            Add(config.BoundaryPolicyVersion);
            Add(profile.ProfileId);
            Add(profile.ProfileVersion);
            Add(profile.ProfileHash);
            AddProfileCapacities(in profile);
            Add(definitions.SchemaVersion);
            Add(definitions.SchemaHash);
            Add(definitions.ContentHash);
            Add(catalog.AttributeCount);
            Add(catalog.AttributeDirtyWordCount);
            Add(catalog.TagCount);
            Add(catalog.TagPresenceWordCount);
            Add(catalog.AttributeLayoutHash);
            Add(catalog.TagCatalogHash);
        }

        /// <summary>
        /// 按冻结字段顺序加入一个 Battle manifest 记录。
        /// </summary>
        internal void AddBattle(
            BattleInstanceHandle handle,
            int memberStart,
            int memberCount,
            int membershipOrdinalRoot)
        {
            Add(handle.SimulationEpoch);
            Add(handle.BattleStableId);
            Add(handle.BattleGeneration);
            Add(memberStart);
            Add(memberCount);
            Add(membershipOrdinalRoot);
        }

        /// <summary>
        /// 按冻结字段顺序加入一个完整 ASC member manifest 记录。
        /// </summary>
        internal void AddMember(in SpawnBatchMemberManifestSlot member)
        {
            Add(member.OwnerAsc.AscStableId);
            Add(member.OwnerAsc.AscGeneration);
            Add(member.BattleInstance.SimulationEpoch);
            Add(member.BattleInstance.BattleStableId);
            Add(member.BattleInstance.BattleGeneration);
            Add(member.RegistryOrdinal);
            Add(member.ScenarioUnitId);
            Add(member.SideId);
            Add(member.TeamId);
            Add(member.MembershipOrdinal);
            Add(member.OwnerActorStableId);
            Add(member.AvatarActorStableId);
            Add(member.ActorBindingGeneration);
            Add(member.RandomState0);
            Add(member.RandomState1);
            AddMemberRanges(in member);
        }

        /// <summary>
        /// 加入一个 Pending Attribute 初始化记录。
        /// </summary>
        internal void Add(PendingAttributeInitialization value)
        {
            Add(value.LayoutIndex);
            Add(value.ConfigOrdinal);
            Add(value.HasExplicitValue);
            Add(value.BaseValue);
            Add(value.CurrentValue);
        }

        /// <summary>
        /// 加入一个 Pending Tag 初始化记录。
        /// </summary>
        internal void Add(PendingTagInitialization value)
        {
            Add(value.LayoutIndex);
            Add(value.ConfigOrdinal);
        }

        /// <summary>
        /// 加入一个 Pending Ability grant 初始化记录。
        /// </summary>
        internal void Add(PendingGrantedAbilityInitialization value)
        {
            Add(value.LayoutIndex);
            Add(value.ConfigOrdinal);
        }

        /// <summary>
        /// 返回当前冻结内容哈希；零值被保留给未初始化 manifest。
        /// </summary>
        internal readonly ulong Finish()
        {
            return _value == 0 ? 1UL : _value;
        }

        /// <summary>
        /// 将 member 的三类连续 range 加入内容哈希。
        /// </summary>
        private void AddMemberRanges(in SpawnBatchMemberManifestSlot member)
        {
            Add(member.AttributeInitializationStart);
            Add(member.AttributeInitializationCount);
            Add(member.TagInitializationStart);
            Add(member.TagInitializationCount);
            Add(member.GrantedAbilityInitializationStart);
            Add(member.GrantedAbilityInitializationCount);
        }

        /// <summary>
        /// 按字段声明顺序加入 ScaleProfile 的全部逻辑容量。
        /// </summary>
        private void AddProfileCapacities(in GasScaleProfile profile)
        {
            Add(profile.MaxFixedTicksPerBatch);
            Add(profile.MaximumDeltaTimeTicks);
            Add(profile.MaxSpawnBatchSize);
            Add(profile.MaxBattleInstanceCount);
            Add(profile.MaxAscRegistryCount);
            Add(profile.MaxBoundaryCommandCount);
            Add(profile.MaxBoundaryCommandPayloadCount);
            Add(profile.MaxOwnerPlanCount);
            Add(profile.MaxResolvedTargetCount);
            Add(profile.MaxEffectOperationCount);
            Add(profile.MaxOwnerReservationCount);
            Add(profile.MaxTargetReservationCount);
            Add(profile.MaxCoreFactCount);
            Add(profile.MaxNextTickRouteCount);
            Add(profile.MaxStructuralIntentCount);
            Add(profile.MaxSessionBoundaryFactCount);
            Add(profile.MaxAscBoundaryFactCount);
            Add(profile.MaxPendingAttributeInitializationCount);
            Add(profile.MaxPendingTagInitializationCount);
            Add(profile.MaxPendingGrantedAbilityInitializationCount);
            Add(profile.MaxGrantedAbilityCount);
            Add(profile.MaxAbilityActivationCount);
            Add(profile.MaxAbilityContinuationCount);
            Add(profile.MaxAbilitySubscriptionCount);
            Add(profile.MaxCooldownGateCount);
            Add(profile.MaxActivationOwnedContributionCount);
            Add(profile.MaxEmittedApplicationRefCount);
            Add(profile.MaxActiveEffectCount);
            Add(profile.MaxPayloadRangeRecordCount);
            Add(profile.MaxPayloadValueCount);
            Add(profile.MaxAttributeAggregatorCount);
            Add(profile.MaxLiveDependencyCount);
            Add(profile.MaxLiveDependencyRouteCount);
            Add(profile.MaxPendingCommandCount);
        }

        /// <summary>
        /// 以固定小端字节序加入一个有符号整数。
        /// </summary>
        private void Add(int value)
        {
            Add(unchecked((uint)value));
        }

        /// <summary>
        /// 以固定小端字节序加入一个无符号整数。
        /// </summary>
        private void Add(uint value)
        {
            for (var shift = 0; shift < 32; shift += 8)
                AddByte((byte)(value >> shift));
        }

        /// <summary>
        /// 以固定小端字节序加入一个无符号长整数。
        /// </summary>
        private void Add(ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
                AddByte((byte)(value >> shift));
        }

        /// <summary>
        /// 以 IEEE-754 原始位模式加入浮点初值，避免文本格式化造成跨平台哈希差异。
        /// </summary>
        private void Add(float value)
        {
            Add(math.asuint(value));
        }

        /// <summary>
        /// 执行单字节 FNV-1a 更新。
        /// </summary>
        private void AddByte(byte value)
        {
            _value ^= value;
            _value = unchecked(_value * Prime);
        }
    }

    /// <summary>
    /// 保存跨 render/fixed tick 的唯一 Boundary command journal 条目及冻结 payload range。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct BoundaryCommandInbox : IBufferElementData
    {
        public ulong SimulationEpoch;
        public ulong RequestId;
        public ulong RequestSequence;
        public ulong SourceSequence;
        public byte HasSource;
        public OwnerAscHandle SourceAsc;
        public BattleInstanceHandle BattleInstance;
        public BoundaryTargetRef Target;
        public StableHandleDiagnosticCarrier SubjectHandle;
        public int DefinitionId;
        public ulong AvailableTick;
        public GasBoundaryCommandKind CommandKind;
        public ushort SemanticPhaseOrdinal;
        public ushort WorkClassOrdinal;
        public ushort PayloadSchemaVersion;
        public GasBoundaryCommandPayloadKind PayloadKind;
        public int FrozenPayloadOffset;
        public int FrozenPayloadLength;
        public uint FrozenPayloadGeneration;
        public ulong PayloadHash;
        public ulong SemanticHash;
        public ulong CommandHash;
        public GasBoundaryCommandState State;
        public ulong SealedTick;
    }

    /// <summary>
    /// 承载 Boundary command 的冻结 unmanaged payload 字节，range 只由 inbox 条目解释。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct BoundaryCommandFrozenPayload : IBufferElementData
    {
        public byte Value;
    }
}
