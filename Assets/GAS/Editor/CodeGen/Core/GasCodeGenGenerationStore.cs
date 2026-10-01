using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GAS.Editor
{
    /// <summary>
    /// 以 fixed intent 独占 claim 和单 canonical selector 原子提交实现 generation 发布、恢复与零选择权审计。
    /// </summary>
    public sealed class GasCodeGenGenerationStore : IDisposable
    {
        private const int RecordVersion = 3;
        private const int IntentVersion = 2;
        private const int AuditVersion = 2;
        private const string StoreRelativePath = "ProjectSettings/GasCodeGen";
        private const string GenerationsDirectoryName = "Generations";
        private const string WorkRelativePath = "Temp/GasCodeGenGenerationStore";
        private const string IntentFileName = "PublishIntent.json";
        private const string AuditFileName = "ActiveGenerationRef.json";
        private const string LockFileName = "store.lock";
        private const string RecordFileName = "GenerationRecord.json";
        private const string ArchiveSelectorFileName = "Generation.additionalfile";
        private const string ArchiveDescriptorFileName = "GasPackageDescriptor.json";
        private const string ArchiveEnvelopeFileName = "GasInstallEnvelope.bin";
        private const string ArchiveCompilePlanFileName = "CandidateCompilePlan.bin";
        private const string ArchiveAnalyzerFileName = "GasCodeGenSourceGenerator.dll";
        private const string ArchiveScaffoldFileName = "RouteScaffold.bin";
        private const string TransactionDirectoryName = "Tx";
        private const string GenerationDomain = "EX-GAS-GenerationRecord-v3";
        private const string AuditDomain = "EX-GAS-ActiveGenerationAudit-v2";
        private readonly string _projectRoot;
        private readonly string _storeRoot;
        private readonly string _generationsRoot;
        private readonly string _workRoot;
        private readonly FileStream _lockStream;
        private readonly GasCodeGenGenerationStoreFixture _fixture;
        private bool _disposed;

        /// <summary>
        /// 保存经过边界验证的 Store 路径和进程独占 lock handle。
        /// </summary>
        private GasCodeGenGenerationStore(
            string projectRoot,
            string storeRoot,
            string generationsRoot,
            string workRoot,
            FileStream lockStream,
            GasCodeGenGenerationStoreFixture fixture)
        {
            _projectRoot = projectRoot;
            _storeRoot = storeRoot;
            _generationsRoot = generationsRoot;
            _workRoot = workRoot;
            _lockStream = lockStream;
            _fixture = fixture;
        }

        /// <summary>
        /// 返回 fixed transaction intent 路径；该对象对 Unity generation 选择权为零。
        /// </summary>
        public string PublishIntentPath => Path.Combine(_storeRoot, IntentFileName);

        /// <summary>
        /// 返回 post-commit DerivedAudit 路径；其缺失或漂移不得改变 selector。
        /// </summary>
        public string ActiveRefPath => Path.Combine(_storeRoot, AuditFileName);

        /// <summary>
        /// 返回唯一 Unity-consumed canonical selector 绝对路径。
        /// </summary>
        public string SelectorPath => ResolveProjectPath(GasCodeGenCandidateCompileGate.SelectorRelativePath);

        /// <summary>
        /// 打开普通发布 Store；若存在未关闭 intent 则 fail closed，要求显式 recovery。
        /// </summary>
        public static GasCodeGenGenerationStore Open(string projectRoot)
        {
            var store = OpenCore(projectRoot, null);
            if (File.Exists(store.PublishIntentPath))
            {
                store.Dispose();
                throw new InvalidOperationException(
                    "A publish intent already exists; run explicit generation recovery before publishing.");
            }
            return store;
        }

        /// <summary>
        /// 打开显式 recovery Store；不会按时间、lease、audit 或 cache 自动猜测 generation。
        /// </summary>
        public static GasCodeGenGenerationStore OpenForRecovery(string projectRoot)
        {
            return OpenCore(projectRoot, null);
        }

        /// <summary>
        /// 仅为同程序集事务 fixture 打开带精确 interleave seam 的 Store；production 入口永不传入 fixture。
        /// </summary>
        internal static GasCodeGenGenerationStore OpenForFixture(
            string projectRoot,
            GasCodeGenGenerationStoreFixture fixture)
        {
            if (fixture == null)
                throw new ArgumentNullException(nameof(fixture));
            var store = OpenCore(projectRoot, fixture);
            if (File.Exists(store.PublishIntentPath))
            {
                store.Dispose();
                throw new InvalidOperationException(
                    "A publish intent already exists; fixture must use a fresh transaction root.");
            }
            return store;
        }

        /// <summary>
        /// 创建固定 Store/Work 根并以 FileShare.None 获取进程级串行 lease。
        /// </summary>
        private static GasCodeGenGenerationStore OpenCore(
            string projectRoot,
            GasCodeGenGenerationStoreFixture fixture)
        {
            var resolvedProjectRoot = ResolveProjectRoot(projectRoot);
            var storeRoot = ResolveUnderProject(resolvedProjectRoot, StoreRelativePath);
            var generationsRoot = Path.Combine(storeRoot, GenerationsDirectoryName);
            var workRoot = ResolveUnderProject(resolvedProjectRoot, WorkRelativePath);
            Directory.CreateDirectory(storeRoot);
            Directory.CreateDirectory(generationsRoot);
            Directory.CreateDirectory(workRoot);
            EnsureNoReparsePoint(storeRoot);
            EnsureNoReparsePoint(generationsRoot);
            EnsureNoReparsePoint(workRoot);
            var lockPath = Path.Combine(workRoot, LockFileName);
            var lockStream = new FileStream(
                lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                1, FileOptions.WriteThrough);
            return new GasCodeGenGenerationStore(
                resolvedProjectRoot, storeRoot, generationsRoot, workRoot, lockStream, fixture);
        }

        /// <summary>
        /// 仅接受 sealed descriptor snapshot，把 route/control bytes 复制到零选择权 generation archive。
        /// </summary>
        internal GenerationRecord StageGeneration(GasCodeGenPackageDescriptorSnapshot snapshot)
        {
            EnsureUsable();
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            snapshot.EnsureUnchanged();
            var record = CreateGenerationRecord(snapshot);
            var finalRoot = GetGenerationRoot(record.GenerationId);
            if (Directory.Exists(finalRoot))
            {
                var existing = VerifyGeneration(record.GenerationId);
                EnsureRecordsEqual(existing, record);
                return existing;
            }

            var stageRoot = CreateUniqueWorkDirectory("stage-");
            try
            {
                WriteGenerationFiles(stageRoot, snapshot, record);
                VerifyGenerationDirectory(stageRoot, record);
                Directory.Move(stageRoot, finalRoot);
                return VerifyGeneration(record.GenerationId);
            }
            catch
            {
                DeleteOwnedWorkDirectoryBestEffort(stageRoot);
                throw;
            }
        }

        /// <summary>
        /// 仅为事务 fixture 用外部冻结 selector bytes 建立 archive，后续 publish/recovery 完整走 production 路径。
        /// </summary>
        internal GenerationRecord StageGenerationForFixture(
            GasCodeGenPackageDescriptorSnapshot snapshot,
            byte[] selectorBytes)
        {
            EnsureFixtureEnabled();
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (selectorBytes == null)
                throw new ArgumentNullException(nameof(selectorBytes));
            snapshot.EnsureUnchanged();
            GasCodeGenPackageDescriptor.ValidateSelectorBytes(selectorBytes);
            var record = CreateGenerationRecord(snapshot, selectorBytes);
            var finalRoot = GetGenerationRoot(record.GenerationId);
            if (Directory.Exists(finalRoot))
            {
                var existing = VerifyGeneration(record.GenerationId);
                EnsureRecordsEqual(existing, record);
                return existing;
            }
            var stageRoot = CreateUniqueWorkDirectory("fixture-stage-");
            try
            {
                WriteGenerationFiles(stageRoot, snapshot, record, selectorBytes);
                VerifyGenerationDirectory(stageRoot, record);
                Directory.Move(stageRoot, finalRoot);
                return VerifyGeneration(record.GenerationId);
            }
            catch
            {
                DeleteOwnedWorkDirectoryBestEffort(stageRoot);
                throw;
            }
        }

        /// <summary>
        /// 使用无故障 production 路径发布 staged generation，并返回 selector/audit 结果。
        /// </summary>
        internal PublishResult Publish(GenerationRecord record)
        {
            return Publish(record, GasCodeGenPublishFaultPoint.None);
        }

        /// <summary>
        /// 执行 NotStarted claim、post-claim CAS、Armed、单文件 commit、Committed receipt 与 post-commit audit。
        /// </summary>
        internal PublishResult Publish(GenerationRecord record, GasCodeGenPublishFaultPoint faultPoint)
        {
            EnsureUsable();
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            var verified = VerifyGeneration(record.GenerationId);
            EnsureRecordsEqual(verified, record);
            if (File.Exists(PublishIntentPath))
                throw new InvalidOperationException("A publish intent already owns the Store mutation claim.");

            var previous = ReadSelectorSnapshot();
            var targetBytes = ReadGenerationFile(record.GenerationId, ArchiveSelectorFileName);
            var intent = CreateNotStartedIntent(record, previous, targetBytes);
            WriteIntentCreateNew(intent);
            ApplyFixtureAfterIntentClaim();
            ThrowInjected(faultPoint, GasCodeGenPublishFaultPoint.AfterNotStartedIntent);
            EnsurePostClaimCasOrMarkIndeterminate(intent, faultPoint);
            if (previous.IsPresent && ByteArraysEqual(previous.Bytes, targetBytes))
                return CloseNoOp(intent, previous);

            ArmIntent(intent);
            ThrowInjected(faultPoint, GasCodeGenPublishFaultPoint.AfterArmedIntent);
            CommitSelector(intent, faultPoint);
            ThrowInjected(faultPoint, GasCodeGenPublishFaultPoint.AfterSelectorCommit);
            WriteCommittedReceipt(intent);
            ThrowInjected(faultPoint, GasCodeGenPublishFaultPoint.AfterCommittedReceipt);
            var audit = WriteAuditFromCommittedIntent(intent);
            CloseIntent(intent, "CommittedAndAudited");
            return new PublishResult(intent.CommitAttemptState, intent.PromotionId, audit, false);
        }

        /// <summary>
        /// 仅为六态 recovery fixture 通过 production intent writer/state transitions 留下指定 durable state。
        /// </summary>
        internal PublishIntent SeedRecoveryIntentForFixture(
            GenerationRecord record,
            GasCodeGenCommitAttemptState state,
            bool useMissingPreviousSnapshot)
        {
            EnsureFixtureEnabled();
            var verified = VerifyGeneration(record.GenerationId);
            EnsureRecordsEqual(verified, record);
            if (File.Exists(PublishIntentPath))
                throw new InvalidOperationException("Fixture recovery seed requires no active intent.");
            var previous = useMissingPreviousSnapshot
                ? SelectorSnapshot.Missing()
                : ReadSelectorSnapshot();
            var targetBytes = ReadGenerationFile(record.GenerationId, ArchiveSelectorFileName);
            var intent = CreateNotStartedIntent(record, previous, targetBytes);
            WriteIntentCreateNew(intent);
            switch (state)
            {
                case GasCodeGenCommitAttemptState.NotStarted:
                    return intent;
                case GasCodeGenCommitAttemptState.Armed:
                    ArmIntent(intent);
                    return intent;
                case GasCodeGenCommitAttemptState.Committed:
                    ArmIntent(intent);
                    CommitSelector(intent, GasCodeGenPublishFaultPoint.None);
                    WriteCommittedReceipt(intent);
                    return intent;
                case GasCodeGenCommitAttemptState.CompetitionFailed:
                case GasCodeGenCommitAttemptState.Indeterminate:
                case GasCodeGenCommitAttemptState.NoOp:
                    MarkIntent(intent, state, "FixtureSeed" + state);
                    return intent;
                default:
                    throw new InvalidDataException("Unknown fixture recovery state: " + state);
            }
        }

        /// <summary>
        /// 将 operator 指定历史 archive 作为新 candidate 走同一 selector transaction，并分配新 PromotionId。
        /// </summary>
        internal PublishResult PublishRollback(string generationId)
        {
            return Publish(VerifyGeneration(generationId));
        }

        /// <summary>
        /// 依据六态矩阵恢复唯一 fixed intent；无法证明线性化归属时保留证据并 fail closed。
        /// </summary>
        public RecoveryResult Recover()
        {
            EnsureUsable();
            var intent = ReadAndVerifyPublishIntent();
            var record = VerifyGeneration(intent.GenerationId);
            EnsureIntentMatchesRecord(intent, record);
            var selector = ReadSelectorSnapshot();
            switch (intent.CommitAttemptState)
            {
                case GasCodeGenCommitAttemptState.NotStarted:
                case GasCodeGenCommitAttemptState.Armed:
                    return RecoverUncommitted(intent, selector);
                case GasCodeGenCommitAttemptState.Committed:
                    return RecoverCommitted(intent, selector);
                case GasCodeGenCommitAttemptState.NoOp:
                    return RecoverNoOp(intent, selector);
                case GasCodeGenCommitAttemptState.CompetitionFailed:
                case GasCodeGenCommitAttemptState.Indeterminate:
                    throw new InvalidOperationException(
                        "Recovery is fail-closed for state " + intent.CommitAttemptState + ".");
                default:
                    throw new InvalidDataException("Unknown publish intent state: " + intent.CommitAttemptState);
            }
        }

        /// <summary>
        /// 验证 fixed intent 的 schema、自哈希、snapshot Base64 与 generation archive binding。
        /// </summary>
        public PublishIntent VerifyPublishIntent()
        {
            EnsureUsable();
            return ReadAndVerifyPublishIntent();
        }

        /// <summary>
        /// 验证 DerivedAudit 与 canonical selector/record 一致；该方法不修复 selector。
        /// </summary>
        public ActiveGenerationRef VerifyActive()
        {
            EnsureUsable();
            if (!File.Exists(ActiveRefPath))
                throw new FileNotFoundException("Active generation audit is missing.", ActiveRefPath);
            var audit = ReadJson<ActiveGenerationRef>(ActiveRefPath);
            ValidateAudit(audit);
            var record = VerifyGeneration(audit.GenerationId);
            EnsureAuditMatchesRecord(audit, record);
            var selector = ReadSelectorSnapshot();
            if (!selector.IsPresent
                || !string.Equals(selector.Sha256, audit.SelectorSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Derived audit does not match the canonical selector.");
            return audit;
        }

        /// <summary>
        /// 验证 sealed generation archive 的 record 与六个 snapshot 文件完整闭合。
        /// </summary>
        public GenerationRecord VerifyGeneration(string generationId)
        {
            EnsureUsable();
            ValidateIdentifier(generationId, nameof(generationId));
            var root = GetGenerationRoot(generationId);
            var recordPath = Path.Combine(root, RecordFileName);
            if (!File.Exists(recordPath))
                throw new FileNotFoundException("Generation record is missing.", recordPath);
            var record = ReadJson<GenerationRecord>(recordPath);
            ValidateRecord(record, generationId);
            VerifyGenerationDirectory(root, record);
            return record;
        }

        /// <summary>
        /// 释放进程级 Store lock；不会删除 intent、selector、audit 或 generation archive。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _lockStream.Dispose();
        }

        /// <summary>
        /// 从 sealed package snapshot 创建 deterministic generation record 与 GenerationId。
        /// </summary>
        private static GenerationRecord CreateGenerationRecord(GasCodeGenPackageDescriptorSnapshot snapshot)
        {
            return CreateGenerationRecord(snapshot, snapshot.Candidate.SelectorBytes);
        }

        /// <summary>
        /// 从 sealed package 与指定 selector snapshot 创建 record；指定入口仅供 internal fixture 复用。
        /// </summary>
        private static GenerationRecord CreateGenerationRecord(
            GasCodeGenPackageDescriptorSnapshot snapshot,
            byte[] selectorBytes)
        {
            var candidate = snapshot.Candidate;
            var selectorSha256 = ComputeSha256(selectorBytes);
            var generationId = ComputeCanonicalHash(
                GenerationDomain,
                snapshot.DescriptorSha256,
                snapshot.InstallEnvelopeSha256,
                selectorSha256,
                candidate.AnalyzerSha256,
                candidate.RouteScaffoldSha256,
                snapshot.CompilePlan.Sha256);
            var record = new GenerationRecord
            {
                Version = RecordVersion,
                GenerationId = generationId,
                DescriptorSha256 = snapshot.DescriptorSha256,
                InstallEnvelopeSha256 = snapshot.InstallEnvelopeSha256,
                SelectorSha256 = selectorSha256,
                AnalyzerSha256 = candidate.AnalyzerSha256,
                RouteScaffoldSha256 = candidate.RouteScaffoldSha256,
                CandidateCompilePlanSha256 = snapshot.CompilePlan.Sha256,
                RequiredArtifactSetId = GasCodeGenPackageDescriptor.RequiredArtifactSetId,
                RequiredArtifactSetContractHash = GasCodeGenPackageDescriptor.RequiredArtifactSetContractHash,
                SourceInputHash = candidate.SourceInputHash,
                SchemaHash = candidate.SchemaHash,
                ContentHash = candidate.ContentHash,
                LayoutHash = candidate.LayoutHash,
                ArtifactManifestHash = candidate.ArtifactManifestHash,
                SourceArtifactInventoryHash = candidate.SourceArtifactInventoryHash,
                CandidateArtifactManifestSha256 = candidate.CandidateArtifactManifestSha256,
                FullSemanticEligibility = false,
            };
            record.RecordSha256 = ComputeRecordHash(record);
            return record;
        }

        /// <summary>
        /// 把六个 route/control snapshots 和 record durable 写入 staging generation root。
        /// </summary>
        private static void WriteGenerationFiles(
            string stageRoot,
            GasCodeGenPackageDescriptorSnapshot snapshot,
            GenerationRecord record)
        {
            WriteGenerationFiles(stageRoot, snapshot, record, snapshot.Candidate.SelectorBytes);
        }

        /// <summary>
        /// 将指定 selector 与其余 sealed package snapshots 写入 generation staging root。
        /// </summary>
        private static void WriteGenerationFiles(
            string stageRoot,
            GasCodeGenPackageDescriptorSnapshot snapshot,
            GenerationRecord record,
            byte[] selectorBytes)
        {
            WriteBytesDurable(Path.Combine(stageRoot, ArchiveSelectorFileName), selectorBytes, false);
            WriteBytesDurable(Path.Combine(stageRoot, ArchiveDescriptorFileName), snapshot.DescriptorBytes, false);
            WriteBytesDurable(Path.Combine(stageRoot, ArchiveEnvelopeFileName), snapshot.InstallEnvelopeBytes, false);
            WriteBytesDurable(Path.Combine(stageRoot, ArchiveCompilePlanFileName), snapshot.CompilePlan.Bytes, false);
            WriteBytesDurable(Path.Combine(stageRoot, ArchiveAnalyzerFileName), snapshot.Route.AnalyzerBytes, false);
            WriteBytesDurable(Path.Combine(stageRoot, ArchiveScaffoldFileName), snapshot.Route.ScaffoldBytes, false);
            WriteJsonDurable(Path.Combine(stageRoot, RecordFileName), record, false);
        }

        /// <summary>
        /// 核对 generation 目录只包含固定文件并逐一匹配 record SHA。
        /// </summary>
        private static void VerifyGenerationDirectory(string root, GenerationRecord record)
        {
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException("Generation archive is missing: " + root);
            EnsureNoReparsePoint(root);
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                RecordFileName,
                ArchiveSelectorFileName,
                ArchiveDescriptorFileName,
                ArchiveEnvelopeFileName,
                ArchiveCompilePlanFileName,
                ArchiveAnalyzerFileName,
                ArchiveScaffoldFileName,
                TransactionDirectoryName,
            };
            var entries = Directory.GetFileSystemEntries(root, "*", SearchOption.TopDirectoryOnly);
            for (var index = 0; index < entries.Length; index++)
            {
                EnsureNoReparsePoint(entries[index]);
                if (!allowed.Contains(Path.GetFileName(entries[index])))
                    throw new InvalidDataException("Unexpected generation archive entry: " + entries[index]);
            }
            EnsureFileHash(root, ArchiveSelectorFileName, record.SelectorSha256);
            EnsureFileHash(root, ArchiveDescriptorFileName, record.DescriptorSha256);
            EnsureFileHash(root, ArchiveEnvelopeFileName, record.InstallEnvelopeSha256);
            EnsureFileHash(root, ArchiveCompilePlanFileName, record.CandidateCompilePlanSha256);
            EnsureFileHash(root, ArchiveAnalyzerFileName, record.AnalyzerSha256);
            EnsureFileHash(root, ArchiveScaffoldFileName, record.RouteScaffoldSha256);
        }

        /// <summary>
        /// 创建包含完整 previous/target snapshot binding 的 NotStarted intent 并保持 PromotionId 为空。
        /// </summary>
        private static PublishIntent CreateNotStartedIntent(
            GenerationRecord record,
            SelectorSnapshot previous,
            byte[] targetBytes)
        {
            var intent = new PublishIntent
            {
                Version = IntentVersion,
                ExclusiveClaimId = Guid.NewGuid().ToString("N"),
                OwnerSentinel = CreateOwnerSentinel(),
                CommitAttemptState = GasCodeGenCommitAttemptState.NotStarted,
                ClosureOutcome = string.Empty,
                GenerationId = record.GenerationId,
                PromotionId = string.Empty,
                PreviousSelectorState = previous.IsPresent ? "Present" : "Missing",
                PreviousSelectorBytesBase64 = previous.IsPresent ? Convert.ToBase64String(previous.Bytes) : string.Empty,
                PreviousSelectorSha256 = previous.IsPresent ? previous.Sha256 : string.Empty,
                TargetSelectorBytesBase64 = Convert.ToBase64String(targetBytes),
                TargetSelectorSha256 = ComputeSha256(targetBytes),
                DescriptorSha256 = record.DescriptorSha256,
                InstallEnvelopeSha256 = record.InstallEnvelopeSha256,
                RequiredArtifactSetContractHash = record.RequiredArtifactSetContractHash,
                ArtifactManifestHash = record.ArtifactManifestHash,
                SourceArtifactInventoryHash = record.SourceArtifactInventoryHash,
                AnalyzerSha256 = record.AnalyzerSha256,
                RouteScaffoldSha256 = record.RouteScaffoldSha256,
                CandidateCompilePlanSha256 = record.CandidateCompilePlanSha256,
                ReceiptExclusiveClaimId = string.Empty,
                ReceiptPromotionId = string.Empty,
                ReceiptTargetSelectorSha256 = string.Empty,
            };
            intent.IntentSha256 = ComputeIntentHash(intent);
            return intent;
        }

        /// <summary>
        /// 取得 claim 后重读 selector 做 previous CAS；漂移或故障注入均 durable 标记 Indeterminate。
        /// </summary>
        private void EnsurePostClaimCasOrMarkIndeterminate(
            PublishIntent intent,
            GasCodeGenPublishFaultPoint faultPoint)
        {
            if (faultPoint == GasCodeGenPublishFaultPoint.ForceIndeterminate)
            {
                MarkIntent(intent, GasCodeGenCommitAttemptState.Indeterminate, "InjectedIndeterminate");
                throw new GasCodeGenInjectedFaultException(faultPoint);
            }
            SelectorSnapshot current;
            try
            {
                current = ReadSelectorSnapshot();
            }
            catch
            {
                MarkIntent(intent, GasCodeGenCommitAttemptState.Indeterminate, "PostClaimPreviousUnreadable");
                throw;
            }
            if (SelectorMatchesPrevious(current, intent))
                return;
            MarkIntent(intent, GasCodeGenCommitAttemptState.Indeterminate, "PostClaimPreviousDrift");
            throw new InvalidOperationException("Canonical selector changed after the exclusive claim was acquired.");
        }

        /// <summary>
        /// 在 exclusive claim 内关闭 same-target NoOp，不分配 PromotionId 且不改 selector/audit。
        /// </summary>
        private PublishResult CloseNoOp(PublishIntent intent, SelectorSnapshot previous)
        {
            if (!previous.IsPresent
                || !string.Equals(previous.Sha256, intent.TargetSelectorSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("NoOp requires Present previous bytes equal to target bytes.");
            MarkIntent(intent, GasCodeGenCommitAttemptState.NoOp, "SameTargetNoOp");
            CloseIntent(intent, "SameTargetNoOp");
            return new PublishResult(intent.CommitAttemptState, string.Empty, null, true);
        }

        /// <summary>
        /// 分配 mutation-time PromotionId 并 durable 写 Armed 状态。
        /// </summary>
        private void ArmIntent(PublishIntent intent)
        {
            intent.PromotionId = Guid.NewGuid().ToString("N");
            intent.CommitAttemptState = GasCodeGenCommitAttemptState.Armed;
            intent.ClosureOutcome = string.Empty;
            RewriteIntent(intent);
        }

        /// <summary>
        /// 按 previous Present/Missing 分别执行 File.Replace 或 no-overwrite File.Move 提交。
        /// </summary>
        private void CommitSelector(PublishIntent intent, GasCodeGenPublishFaultPoint faultPoint)
        {
            if (faultPoint == GasCodeGenPublishFaultPoint.ForceCompetitionFailed)
            {
                MarkIntent(intent, GasCodeGenCommitAttemptState.CompetitionFailed, "InjectedCompetition");
                throw new GasCodeGenInjectedFaultException(faultPoint);
            }
            var targetBytes = DecodeCanonicalBase64(intent.TargetSelectorBytesBase64, "TargetSelectorBytesBase64");
            var selectorDirectory = Path.GetDirectoryName(SelectorPath);
            if (string.IsNullOrEmpty(selectorDirectory) || !Directory.Exists(selectorDirectory))
                throw new DirectoryNotFoundException("Canonical selector directory is missing: " + selectorDirectory);
            var tempPath = Path.Combine(selectorDirectory, ".gas-codegen-" + intent.ExclusiveClaimId + ".tmp");
            WriteBytesDurable(tempPath, targetBytes, false);
            try
            {
                ApplyFixtureBeforeSelectorCommit();
                CommitSelectorCore(intent, tempPath);
            }
            catch (IOException competition)
            {
                MarkIntent(intent, GasCodeGenCommitAttemptState.CompetitionFailed, competition.GetType().Name);
                throw;
            }
            catch
            {
                MarkIntent(intent, GasCodeGenCommitAttemptState.Indeterminate, "SelectorCommitUnknown");
                throw;
            }
            finally
            {
                DeleteExactFileBestEffort(tempPath);
            }
            var current = ReadSelectorSnapshot();
            if (!current.IsPresent
                || !string.Equals(current.Sha256, intent.TargetSelectorSha256, StringComparison.Ordinal))
            {
                MarkIntent(intent, GasCodeGenCommitAttemptState.Indeterminate, "PostCommitTargetMismatch");
                throw new InvalidOperationException("Selector commit result cannot be proven.");
            }
        }

        /// <summary>
        /// 在 mutation 前最后一次复核 previous，再调用唯一允许的原子文件 API。
        /// </summary>
        private void CommitSelectorCore(PublishIntent intent, string tempPath)
        {
            var current = ReadSelectorSnapshot();
            if (!SelectorMatchesPrevious(current, intent))
                throw new IOException("Selector previous snapshot changed before atomic commit.");
            if (string.Equals(intent.PreviousSelectorState, "Present", StringComparison.Ordinal))
            {
                File.Replace(tempPath, SelectorPath, null);
                return;
            }
            if (File.Exists(SelectorPath))
                throw new IOException("First-publish selector target was created by a competitor.");
            File.Move(tempPath, SelectorPath);
        }

        /// <summary>
        /// 在 selector target bytes 已复核后写入绑定 claim/promotion/target 的 durable Committed receipt。
        /// </summary>
        private void WriteCommittedReceipt(PublishIntent intent)
        {
            intent.CommitAttemptState = GasCodeGenCommitAttemptState.Committed;
            intent.ReceiptExclusiveClaimId = intent.ExclusiveClaimId;
            intent.ReceiptPromotionId = intent.PromotionId;
            intent.ReceiptTargetSelectorSha256 = intent.TargetSelectorSha256;
            intent.ClosureOutcome = string.Empty;
            RewriteIntent(intent);
        }

        /// <summary>
        /// 仅从有效 Committed receipt 写 post-commit DerivedAudit。
        /// </summary>
        private ActiveGenerationRef WriteAuditFromCommittedIntent(PublishIntent intent)
        {
            ValidateCommittedReceipt(intent);
            var selector = ReadSelectorSnapshot();
            if (!selector.IsPresent
                || !string.Equals(selector.Sha256, intent.TargetSelectorSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Committed receipt target no longer matches selector.");
            var record = VerifyGeneration(intent.GenerationId);
            var audit = CreateAudit(record, intent.PromotionId, intent.ExclusiveClaimId);
            WriteJsonAtomic(ActiveRefPath, audit);
            return audit;
        }

        /// <summary>
        /// 恢复 NotStarted/Armed：只允许 selector 仍 Missing 或仍等于 previous，绝不前滚 target。
        /// </summary>
        private RecoveryResult RecoverUncommitted(PublishIntent intent, SelectorSnapshot selector)
        {
            var unlinearized = string.Equals(intent.PreviousSelectorState, "Missing", StringComparison.Ordinal)
                ? !selector.IsPresent
                : SelectorMatchesPrevious(selector, intent);
            if (!unlinearized)
                throw new InvalidOperationException("Uncommitted recovery cannot prove the selector stayed at previous.");
            CloseIntent(intent, "AbortedBeforeLinearization");
            return new RecoveryResult(
                intent.CommitAttemptState, "AbortedBeforeLinearization", false, null);
        }

        /// <summary>
        /// 恢复 Committed：receipt、exclusive claim 与 selector target 全部有效时只补 audit。
        /// </summary>
        private RecoveryResult RecoverCommitted(PublishIntent intent, SelectorSnapshot selector)
        {
            ValidateCommittedReceipt(intent);
            if (!selector.IsPresent
                || !string.Equals(selector.Sha256, intent.TargetSelectorSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("Committed recovery selector does not equal target.");
            var audit = WriteAuditFromCommittedIntent(intent);
            CloseIntent(intent, "RecoveredCommittedAudit");
            return new RecoveryResult(intent.CommitAttemptState, "RecoveredCommittedAudit", true, audit);
        }

        /// <summary>
        /// 恢复 NoOp：要求 Present previous、target 与 current 三者完整相等且不补 audit。
        /// </summary>
        private RecoveryResult RecoverNoOp(PublishIntent intent, SelectorSnapshot selector)
        {
            if (!selector.IsPresent
                || !string.Equals(intent.PreviousSelectorState, "Present", StringComparison.Ordinal)
                || !string.Equals(selector.Sha256, intent.PreviousSelectorSha256, StringComparison.Ordinal)
                || !string.Equals(selector.Sha256, intent.TargetSelectorSha256, StringComparison.Ordinal)
                || !string.IsNullOrEmpty(intent.PromotionId))
                throw new InvalidOperationException("NoOp recovery identity mismatch.");
            CloseIntent(intent, "RecoveredNoOp");
            return new RecoveryResult(intent.CommitAttemptState, "RecoveredNoOp", false, null);
        }

        /// <summary>
        /// 将 closed intent 复制到 generation transaction archive 后删除 fixed claim 文件。
        /// </summary>
        private void CloseIntent(PublishIntent intent, string closureOutcome)
        {
            intent.ClosureOutcome = closureOutcome;
            RewriteIntent(intent);
            var transactionRoot = Path.Combine(GetGenerationRoot(intent.GenerationId), TransactionDirectoryName);
            Directory.CreateDirectory(transactionRoot);
            var archivePath = Path.Combine(
                transactionRoot,
                intent.ExclusiveClaimId.Substring(0, 16) + ".json");
            WriteJsonDurable(archivePath, intent, false);
            File.Delete(PublishIntentPath);
        }

        /// <summary>
        /// 更新 intent 状态与 closure 原因并 durable 原子替换 fixed claim 文件。
        /// </summary>
        private void MarkIntent(
            PublishIntent intent,
            GasCodeGenCommitAttemptState state,
            string outcome)
        {
            if (state == GasCodeGenCommitAttemptState.CompetitionFailed
                || state == GasCodeGenCommitAttemptState.Indeterminate)
            {
                // 未形成 Committed receipt 的失败状态不能携带可被误认作已生效的 PromotionId。
                intent.PromotionId = string.Empty;
                intent.ReceiptExclusiveClaimId = string.Empty;
                intent.ReceiptPromotionId = string.Empty;
                intent.ReceiptTargetSelectorSha256 = string.Empty;
            }
            intent.CommitAttemptState = state;
            intent.ClosureOutcome = outcome;
            RewriteIntent(intent);
        }

        /// <summary>
        /// 使用 FileMode.CreateNew 一次性创建完整 NotStarted intent，作为单 active mutation claim。
        /// </summary>
        private void WriteIntentCreateNew(PublishIntent intent)
        {
            ValidateIntent(intent);
            var receipt = WriteJsonDurable(PublishIntentPath, intent, false);
            if (_fixture != null)
            {
                _fixture.RecordIntentClaimWrite(
                    receipt.FileMode,
                    receipt.FileOptions,
                    receipt.FlushToDisk,
                    receipt.BytesVerified);
            }
        }

        /// <summary>
        /// 重算 IntentSha256 并以同目录 temp + File.Replace durable 更新 claim 状态。
        /// </summary>
        private void RewriteIntent(PublishIntent intent)
        {
            intent.IntentSha256 = ComputeIntentHash(intent);
            ValidateIntent(intent);
            WriteJsonAtomic(PublishIntentPath, intent);
        }

        /// <summary>
        /// 读取 fixed intent 并验证 schema、canonical Base64、自哈希和 snapshot fields。
        /// </summary>
        private PublishIntent ReadAndVerifyPublishIntent()
        {
            if (!File.Exists(PublishIntentPath))
                throw new FileNotFoundException("Publish intent is missing.", PublishIntentPath);
            var intent = ReadJson<PublishIntent>(PublishIntentPath);
            ValidateIntent(intent);
            return intent;
        }

        /// <summary>
        /// 要求 intent 中全部 generation/route snapshot hash 与 sealed record 一致。
        /// </summary>
        private static void EnsureIntentMatchesRecord(PublishIntent intent, GenerationRecord record)
        {
            if (!string.Equals(intent.GenerationId, record.GenerationId, StringComparison.Ordinal)
                || !string.Equals(intent.TargetSelectorSha256, record.SelectorSha256, StringComparison.Ordinal)
                || !string.Equals(intent.DescriptorSha256, record.DescriptorSha256, StringComparison.Ordinal)
                || !string.Equals(intent.InstallEnvelopeSha256, record.InstallEnvelopeSha256, StringComparison.Ordinal)
                || !string.Equals(intent.RequiredArtifactSetContractHash, record.RequiredArtifactSetContractHash, StringComparison.Ordinal)
                || !string.Equals(intent.ArtifactManifestHash, record.ArtifactManifestHash, StringComparison.Ordinal)
                || !string.Equals(intent.SourceArtifactInventoryHash, record.SourceArtifactInventoryHash, StringComparison.Ordinal)
                || !string.Equals(intent.AnalyzerSha256, record.AnalyzerSha256, StringComparison.Ordinal)
                || !string.Equals(intent.RouteScaffoldSha256, record.RouteScaffoldSha256, StringComparison.Ordinal)
                || !string.Equals(intent.CandidateCompilePlanSha256, record.CandidateCompilePlanSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Publish intent route snapshot does not match generation record.");
        }

        /// <summary>
        /// 校验 intent 六态、previous/target Base64、PromotionId/receipt 约束与 IntentSha256。
        /// </summary>
        private static void ValidateIntent(PublishIntent intent)
        {
            if (intent == null || intent.Version != IntentVersion)
                throw new InvalidDataException("Only PublishIntent version 2 is accepted.");
            ValidateIdentifier(intent.ExclusiveClaimId, nameof(intent.ExclusiveClaimId));
            ValidateHash(intent.OwnerSentinel, nameof(intent.OwnerSentinel));
            ValidateIdentifier(intent.GenerationId, nameof(intent.GenerationId));
            ValidateHash(intent.TargetSelectorSha256, nameof(intent.TargetSelectorSha256));
            ValidateIntentHashes(intent);
            var target = DecodeCanonicalBase64(intent.TargetSelectorBytesBase64, "TargetSelectorBytesBase64");
            if (!string.Equals(ComputeSha256(target), intent.TargetSelectorSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Target selector bytes/hash mismatch.");
            ValidatePreviousSnapshot(intent);
            ValidateIntentStateFields(intent);
            var expectedIntentHash = ComputeIntentHash(intent);
            if (!string.Equals(expectedIntentHash, intent.IntentSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Publish intent self-hash mismatch.");
        }

        /// <summary>
        /// 校验 intent 所有 route/control identity 均为 canonical SHA-256。
        /// </summary>
        private static void ValidateIntentHashes(PublishIntent intent)
        {
            ValidateHash(intent.DescriptorSha256, nameof(intent.DescriptorSha256));
            ValidateHash(intent.InstallEnvelopeSha256, nameof(intent.InstallEnvelopeSha256));
            ValidateHash(intent.RequiredArtifactSetContractHash, nameof(intent.RequiredArtifactSetContractHash));
            ValidateHash(intent.ArtifactManifestHash, nameof(intent.ArtifactManifestHash));
            ValidateHash(intent.SourceArtifactInventoryHash, nameof(intent.SourceArtifactInventoryHash));
            ValidateHash(intent.AnalyzerSha256, nameof(intent.AnalyzerSha256));
            ValidateHash(intent.RouteScaffoldSha256, nameof(intent.RouteScaffoldSha256));
            ValidateHash(intent.CandidateCompilePlanSha256, nameof(intent.CandidateCompilePlanSha256));
        }

        /// <summary>
        /// 校验 Missing/Present previous snapshot 的 bytes、SHA 与空值规范。
        /// </summary>
        private static void ValidatePreviousSnapshot(PublishIntent intent)
        {
            if (string.Equals(intent.PreviousSelectorState, "Missing", StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(intent.PreviousSelectorBytesBase64)
                    || !string.IsNullOrEmpty(intent.PreviousSelectorSha256))
                    throw new InvalidDataException("Missing previous selector must not carry bytes/hash.");
                return;
            }
            if (!string.Equals(intent.PreviousSelectorState, "Present", StringComparison.Ordinal))
                throw new InvalidDataException("PreviousSelectorState must be Missing or Present.");
            ValidateHash(intent.PreviousSelectorSha256, nameof(intent.PreviousSelectorSha256));
            var previous = DecodeCanonicalBase64(intent.PreviousSelectorBytesBase64, "PreviousSelectorBytesBase64");
            if (!string.Equals(ComputeSha256(previous), intent.PreviousSelectorSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Previous selector bytes/hash mismatch.");
        }

        /// <summary>
        /// 校验六态下 PromotionId、receipt 与 NoOp 的互斥约束。
        /// </summary>
        private static void ValidateIntentStateFields(PublishIntent intent)
        {
            if (!Enum.IsDefined(typeof(GasCodeGenCommitAttemptState), intent.CommitAttemptState))
                throw new InvalidDataException("Unknown CommitAttemptState.");
            if (intent.CommitAttemptState == GasCodeGenCommitAttemptState.NotStarted
                || intent.CommitAttemptState == GasCodeGenCommitAttemptState.CompetitionFailed
                || intent.CommitAttemptState == GasCodeGenCommitAttemptState.Indeterminate
                || intent.CommitAttemptState == GasCodeGenCommitAttemptState.NoOp)
            {
                if (!string.IsNullOrEmpty(intent.PromotionId))
                    throw new InvalidDataException(intent.CommitAttemptState + " cannot carry PromotionId.");
            }
            else
            {
                ValidateIdentifier(intent.PromotionId, nameof(intent.PromotionId));
            }
            if (intent.CommitAttemptState == GasCodeGenCommitAttemptState.Committed)
                ValidateCommittedReceipt(intent);
            else if (!string.IsNullOrEmpty(intent.ReceiptExclusiveClaimId)
                     || !string.IsNullOrEmpty(intent.ReceiptPromotionId)
                     || !string.IsNullOrEmpty(intent.ReceiptTargetSelectorSha256))
                throw new InvalidDataException("Only Committed intent may carry a receipt.");
        }

        /// <summary>
        /// 要求 Committed receipt 精确回绑 claim、PromotionId 与 target selector SHA。
        /// </summary>
        private static void ValidateCommittedReceipt(PublishIntent intent)
        {
            if (intent.CommitAttemptState != GasCodeGenCommitAttemptState.Committed
                || !string.Equals(intent.ReceiptExclusiveClaimId, intent.ExclusiveClaimId, StringComparison.Ordinal)
                || !string.Equals(intent.ReceiptPromotionId, intent.PromotionId, StringComparison.Ordinal)
                || !string.Equals(intent.ReceiptTargetSelectorSha256, intent.TargetSelectorSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Committed receipt binding is invalid.");
        }

        /// <summary>
        /// 创建只携带零选择权 provenance 的 post-commit audit record。
        /// </summary>
        private static ActiveGenerationRef CreateAudit(
            GenerationRecord record,
            string promotionId,
            string exclusiveClaimId)
        {
            var audit = new ActiveGenerationRef
            {
                Version = AuditVersion,
                UnityConsumerAuthority = 0,
                GenerationId = record.GenerationId,
                PromotionId = promotionId,
                ExclusiveClaimId = exclusiveClaimId,
                SelectorSha256 = record.SelectorSha256,
                DescriptorSha256 = record.DescriptorSha256,
                InstallEnvelopeSha256 = record.InstallEnvelopeSha256,
                RequiredArtifactSetContractHash = record.RequiredArtifactSetContractHash,
                ArtifactManifestHash = record.ArtifactManifestHash,
                SourceArtifactInventoryHash = record.SourceArtifactInventoryHash,
                AnalyzerSha256 = record.AnalyzerSha256,
                RouteScaffoldSha256 = record.RouteScaffoldSha256,
                CandidateCompilePlanSha256 = record.CandidateCompilePlanSha256,
            };
            audit.AuditSha256 = ComputeAuditHash(audit);
            return audit;
        }

        /// <summary>
        /// 校验 audit v2、自哈希、零选择权字段与全部 route hashes。
        /// </summary>
        private static void ValidateAudit(ActiveGenerationRef audit)
        {
            if (audit == null || audit.Version != AuditVersion || audit.UnityConsumerAuthority != 0)
                throw new InvalidDataException("Only zero-authority ActiveGenerationRef v2 is accepted.");
            ValidateIdentifier(audit.GenerationId, nameof(audit.GenerationId));
            ValidateIdentifier(audit.PromotionId, nameof(audit.PromotionId));
            ValidateIdentifier(audit.ExclusiveClaimId, nameof(audit.ExclusiveClaimId));
            ValidateHash(audit.SelectorSha256, nameof(audit.SelectorSha256));
            ValidateHash(audit.DescriptorSha256, nameof(audit.DescriptorSha256));
            ValidateHash(audit.InstallEnvelopeSha256, nameof(audit.InstallEnvelopeSha256));
            ValidateHash(audit.RequiredArtifactSetContractHash, nameof(audit.RequiredArtifactSetContractHash));
            ValidateHash(audit.ArtifactManifestHash, nameof(audit.ArtifactManifestHash));
            ValidateHash(audit.SourceArtifactInventoryHash, nameof(audit.SourceArtifactInventoryHash));
            ValidateHash(audit.AnalyzerSha256, nameof(audit.AnalyzerSha256));
            ValidateHash(audit.RouteScaffoldSha256, nameof(audit.RouteScaffoldSha256));
            ValidateHash(audit.CandidateCompilePlanSha256, nameof(audit.CandidateCompilePlanSha256));
            if (!string.Equals(audit.AuditSha256, ComputeAuditHash(audit), StringComparison.Ordinal))
                throw new InvalidDataException("Active generation audit self-hash mismatch.");
        }

        /// <summary>
        /// 要求 audit route/control identity 全部等于 sealed generation record。
        /// </summary>
        private static void EnsureAuditMatchesRecord(ActiveGenerationRef audit, GenerationRecord record)
        {
            if (!string.Equals(audit.GenerationId, record.GenerationId, StringComparison.Ordinal)
                || !string.Equals(audit.SelectorSha256, record.SelectorSha256, StringComparison.Ordinal)
                || !string.Equals(audit.DescriptorSha256, record.DescriptorSha256, StringComparison.Ordinal)
                || !string.Equals(audit.InstallEnvelopeSha256, record.InstallEnvelopeSha256, StringComparison.Ordinal)
                || !string.Equals(audit.RequiredArtifactSetContractHash, record.RequiredArtifactSetContractHash, StringComparison.Ordinal)
                || !string.Equals(audit.ArtifactManifestHash, record.ArtifactManifestHash, StringComparison.Ordinal)
                || !string.Equals(audit.SourceArtifactInventoryHash, record.SourceArtifactInventoryHash, StringComparison.Ordinal)
                || !string.Equals(audit.AnalyzerSha256, record.AnalyzerSha256, StringComparison.Ordinal)
                || !string.Equals(audit.RouteScaffoldSha256, record.RouteScaffoldSha256, StringComparison.Ordinal)
                || !string.Equals(audit.CandidateCompilePlanSha256, record.CandidateCompilePlanSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Active audit does not match generation record.");
        }

        /// <summary>
        /// 校验 generation record 版本、标识、required set、eligibility 与自哈希。
        /// </summary>
        private static void ValidateRecord(GenerationRecord record, string expectedGenerationId)
        {
            if (record == null || record.Version != RecordVersion)
                throw new InvalidDataException("Only GenerationRecord version 3 is accepted.");
            if (!string.Equals(record.GenerationId, expectedGenerationId, StringComparison.Ordinal))
                throw new InvalidDataException("GenerationId/path mismatch.");
            ValidateIdentifier(record.GenerationId, nameof(record.GenerationId));
            ValidateRecordHashes(record);
            if (!string.Equals(record.RequiredArtifactSetId, GasCodeGenPackageDescriptor.RequiredArtifactSetId, StringComparison.Ordinal)
                || !string.Equals(record.RequiredArtifactSetContractHash, GasCodeGenPackageDescriptor.RequiredArtifactSetContractHash, StringComparison.Ordinal)
                || record.FullSemanticEligibility)
                throw new InvalidDataException("Generation required-set or eligibility contract mismatch.");
            if (!string.Equals(record.RecordSha256, ComputeRecordHash(record), StringComparison.Ordinal))
                throw new InvalidDataException("Generation record self-hash mismatch.");
            var expectedId = ComputeCanonicalHash(
                GenerationDomain, record.DescriptorSha256, record.InstallEnvelopeSha256,
                record.SelectorSha256, record.AnalyzerSha256,
                record.RouteScaffoldSha256, record.CandidateCompilePlanSha256);
            if (!string.Equals(expectedId, record.GenerationId, StringComparison.Ordinal))
                throw new InvalidDataException("GenerationId does not match frozen snapshots.");
        }

        /// <summary>
        /// 校验 record 内全部必需 SHA 字段。
        /// </summary>
        private static void ValidateRecordHashes(GenerationRecord record)
        {
            ValidateHash(record.DescriptorSha256, nameof(record.DescriptorSha256));
            ValidateHash(record.InstallEnvelopeSha256, nameof(record.InstallEnvelopeSha256));
            ValidateHash(record.SelectorSha256, nameof(record.SelectorSha256));
            ValidateHash(record.AnalyzerSha256, nameof(record.AnalyzerSha256));
            ValidateHash(record.RouteScaffoldSha256, nameof(record.RouteScaffoldSha256));
            ValidateHash(record.CandidateCompilePlanSha256, nameof(record.CandidateCompilePlanSha256));
            ValidateHash(record.RequiredArtifactSetContractHash, nameof(record.RequiredArtifactSetContractHash));
            ValidateHash(record.SourceInputHash, nameof(record.SourceInputHash));
            ValidateHash(record.SchemaHash, nameof(record.SchemaHash));
            ValidateHash(record.ContentHash, nameof(record.ContentHash));
            ValidateHash(record.LayoutHash, nameof(record.LayoutHash));
            ValidateHash(record.ArtifactManifestHash, nameof(record.ArtifactManifestHash));
            ValidateHash(record.SourceArtifactInventoryHash, nameof(record.SourceArtifactInventoryHash));
            ValidateHash(record.CandidateArtifactManifestSha256, nameof(record.CandidateArtifactManifestSha256));
        }

        /// <summary>
        /// 要求两个 record 的 deterministic self hash 相同。
        /// </summary>
        private static void EnsureRecordsEqual(GenerationRecord left, GenerationRecord right)
        {
            if (!string.Equals(left.RecordSha256, right.RecordSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Generation archive identity collision.");
        }

        /// <summary>
        /// 从磁盘读取 selector 的 Missing/Present 完整 byte snapshot。
        /// </summary>
        private SelectorSnapshot ReadSelectorSnapshot()
        {
            if (!File.Exists(SelectorPath))
            {
                EnsureSelectorPathAbsent();
                return SelectorSnapshot.Missing();
            }
            EnsureNoReparsePoint(SelectorPath);
            var bytes = File.ReadAllBytes(SelectorPath);
            GasCodeGenPackageDescriptor.ValidateSelectorBytes(bytes);
            return SelectorSnapshot.Present(bytes, ComputeSha256(bytes));
        }

        /// <summary>
        /// 证明 selector namespace path 确实不存在；目录或任何可探测非 regular 对象均属于 unknown。
        /// </summary>
        private void EnsureSelectorPathAbsent()
        {
            if (Directory.Exists(SelectorPath))
                throw new InvalidDataException("Selector path is occupied by a directory.");
            try
            {
                var attributes = File.GetAttributes(SelectorPath);
                throw new InvalidDataException(
                    "Selector path is occupied by a non-regular object: " + attributes + ".");
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        /// <summary>
        /// 判断当前 selector snapshot 与 intent 中 frozen previous state/bytes 完整相等。
        /// </summary>
        private static bool SelectorMatchesPrevious(SelectorSnapshot current, PublishIntent intent)
        {
            if (string.Equals(intent.PreviousSelectorState, "Missing", StringComparison.Ordinal))
                return !current.IsPresent;
            if (!current.IsPresent
                || !string.Equals(current.Sha256, intent.PreviousSelectorSha256, StringComparison.Ordinal))
                return false;
            var previous = DecodeCanonicalBase64(intent.PreviousSelectorBytesBase64, "PreviousSelectorBytesBase64");
            return ByteArraysEqual(current.Bytes, previous);
        }

        /// <summary>
        /// 读取 generation archive 中一个固定 snapshot 文件。
        /// </summary>
        private byte[] ReadGenerationFile(string generationId, string fileName)
        {
            return File.ReadAllBytes(Path.Combine(GetGenerationRoot(generationId), fileName));
        }

        /// <summary>
        /// 校验指定 archive 文件的 SHA 与 record 一致。
        /// </summary>
        private static void EnsureFileHash(string root, string fileName, string expectedHash)
        {
            var path = Path.Combine(root, fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException("Generation snapshot is missing: " + fileName, path);
            EnsureNoReparsePoint(path);
            var actual = ComputeSha256(File.ReadAllBytes(path));
            if (!string.Equals(actual, expectedHash, StringComparison.Ordinal))
                throw new InvalidDataException("Generation snapshot hash mismatch: " + fileName);
        }

        /// <summary>
        /// 以 strict member contract 读取 JSON，并要求原始 bytes 等于 production canonical 序列化结果。
        /// </summary>
        private static T ReadJson<T>(string path) where T : class
        {
            EnsureNoReparsePoint(path);
            var bytes = File.ReadAllBytes(path);
            var text = new UTF8Encoding(false, true).GetString(bytes);
            T value;
            try
            {
                value = JsonConvert.DeserializeObject<T>(
                    text,
                    new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Error,
                    });
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("JSON does not match the strict protocol contract: " + path, exception);
            }
            if (value == null)
                throw new InvalidDataException("JSON object is empty: " + path);
            if (!ByteArraysEqual(bytes, SerializeJson(value)))
                throw new InvalidDataException("JSON bytes are not canonical: " + path);
            return value;
        }

        /// <summary>
        /// 以 FileMode.CreateNew 或 Create 写 JSON 并 Flush(true)，随后重读 bytes 复核。
        /// </summary>
        private static DurableWriteReceipt WriteJsonDurable(string path, object value, bool overwrite)
        {
            var bytes = SerializeJson(value);
            return WriteBytesDurable(path, bytes, overwrite);
        }

        /// <summary>
        /// 以同目录 temp + File.Replace 原子更新既有 JSON 文件。
        /// </summary>
        private static void WriteJsonAtomic(string path, object value)
        {
            var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                WriteBytesDurable(tempPath, SerializeJson(value), false);
                if (File.Exists(path))
                    File.Replace(tempPath, path, null);
                else
                    File.Move(tempPath, path);
            }
            finally
            {
                DeleteExactFileBestEffort(tempPath);
            }
        }

        /// <summary>
        /// 以固定 JSON property 顺序生成 UTF-8 no-BOM、LF 且单终止 LF bytes。
        /// </summary>
        private static byte[] SerializeJson(object value)
        {
            var json = JsonConvert.SerializeObject(value, Formatting.Indented)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n') + "\n";
            return new UTF8Encoding(false, true).GetBytes(json);
        }

        /// <summary>
        /// 以 WriteThrough 和 Flush(true) durable 写 bytes，并执行落盘重读复核。
        /// </summary>
        private static DurableWriteReceipt WriteBytesDurable(string path, byte[] bytes, bool overwrite)
        {
            var parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                throw new DirectoryNotFoundException("Durable write parent is missing: " + parent);
            using (var stream = new FileStream(
                       path,
                       overwrite ? FileMode.Create : FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }
            if (!ByteArraysEqual(File.ReadAllBytes(path), bytes))
                throw new IOException("Durable write verification failed: " + path);
            return new DurableWriteReceipt(
                overwrite ? FileMode.Create : FileMode.CreateNew,
                FileOptions.WriteThrough,
                true,
                true);
        }

        /// <summary>
        /// 计算 record 自哈希；RecordSha256 字段本身不进入输入。
        /// </summary>
        private static string ComputeRecordHash(GenerationRecord record)
        {
            return ComputeCanonicalHash(
                record.Version.ToString(CultureInfo.InvariantCulture), record.GenerationId,
                record.DescriptorSha256, record.InstallEnvelopeSha256, record.SelectorSha256,
                record.AnalyzerSha256, record.RouteScaffoldSha256, record.CandidateCompilePlanSha256,
                record.RequiredArtifactSetId, record.RequiredArtifactSetContractHash,
                record.SourceInputHash, record.SchemaHash, record.ContentHash, record.LayoutHash,
                record.ArtifactManifestHash, record.SourceArtifactInventoryHash,
                record.CandidateArtifactManifestSha256, record.FullSemanticEligibility ? "1" : "0");
        }

        /// <summary>
        /// 计算 intent 自哈希；IntentSha256 字段本身不进入输入。
        /// </summary>
        private static string ComputeIntentHash(PublishIntent intent)
        {
            return ComputeCanonicalHash(
                intent.Version.ToString(CultureInfo.InvariantCulture), intent.ExclusiveClaimId,
                intent.OwnerSentinel, intent.CommitAttemptState.ToString(), intent.ClosureOutcome,
                intent.GenerationId, intent.PromotionId, intent.PreviousSelectorState,
                intent.PreviousSelectorBytesBase64, intent.PreviousSelectorSha256,
                intent.TargetSelectorBytesBase64, intent.TargetSelectorSha256,
                intent.DescriptorSha256, intent.InstallEnvelopeSha256,
                intent.RequiredArtifactSetContractHash, intent.ArtifactManifestHash,
                intent.SourceArtifactInventoryHash, intent.AnalyzerSha256,
                intent.RouteScaffoldSha256, intent.CandidateCompilePlanSha256,
                intent.ReceiptExclusiveClaimId, intent.ReceiptPromotionId,
                intent.ReceiptTargetSelectorSha256);
        }

        /// <summary>
        /// 计算 DerivedAudit 自哈希；AuditSha256 字段本身不进入输入。
        /// </summary>
        private static string ComputeAuditHash(ActiveGenerationRef audit)
        {
            return ComputeCanonicalHash(
                AuditDomain, audit.Version.ToString(CultureInfo.InvariantCulture),
                audit.UnityConsumerAuthority.ToString(CultureInfo.InvariantCulture),
                audit.GenerationId, audit.PromotionId, audit.ExclusiveClaimId,
                audit.SelectorSha256, audit.DescriptorSha256, audit.InstallEnvelopeSha256,
                audit.RequiredArtifactSetContractHash, audit.ArtifactManifestHash,
                audit.SourceArtifactInventoryHash, audit.AnalyzerSha256,
                audit.RouteScaffoldSha256, audit.CandidateCompilePlanSha256);
        }

        /// <summary>
        /// 以 UTF-8 byte-length 前缀字段计算 deterministic SHA-256。
        /// </summary>
        private static string ComputeCanonicalHash(params string[] fields)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                for (var index = 0; index < fields.Length; index++)
                {
                    var bytes = new UTF8Encoding(false, true).GetBytes(fields[index] ?? string.Empty);
                    writer.Write((uint)bytes.Length);
                    writer.Write(bytes);
                }
                writer.Flush();
                return ComputeSha256(stream.ToArray());
            }
        }

        /// <summary>
        /// 计算 bytes 的 lowercase SHA-256。
        /// </summary>
        private static string ComputeSha256(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (var index = 0; index < hash.Length; index++)
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        /// <summary>
        /// 创建无法从时间/路径推断的 owner sentinel hash。
        /// </summary>
        private static string CreateOwnerSentinel()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return ToHex(bytes);
        }

        /// <summary>
        /// 将 bytes 转成 lowercase hexadecimal 文本。
        /// </summary>
        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (var index = 0; index < bytes.Length; index++)
                builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        /// <summary>
        /// 解码 canonical RFC4648 Base64，并以重新编码严格比对拒绝非 canonical 文本。
        /// </summary>
        private static byte[] DecodeCanonicalBase64(string value, string fieldName)
        {
            if (string.IsNullOrEmpty(value))
                throw new InvalidDataException(fieldName + " is required.");
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(value);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException(fieldName + " is not valid Base64.", exception);
            }
            if (!string.Equals(Convert.ToBase64String(bytes), value, StringComparison.Ordinal))
                throw new InvalidDataException(fieldName + " is not canonical Base64.");
            return bytes;
        }

        /// <summary>
        /// 比较两个完整 byte arrays。
        /// </summary>
        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 校验 SHA-256 为 64 个 lowercase hexadecimal 字符。
        /// </summary>
        private static void ValidateHash(string value, string fieldName)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
                throw new InvalidDataException(fieldName + " must be a 64-character SHA-256.");
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!(character >= '0' && character <= '9')
                    && !(character >= 'a' && character <= 'f'))
                    throw new InvalidDataException(fieldName + " must be lowercase hexadecimal.");
            }
        }

        /// <summary>
        /// 校验 claim/generation/promotion identifier 为 32 个 lowercase hexadecimal 字符。
        /// </summary>
        private static void ValidateIdentifier(string value, string fieldName)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32 && value.Length != 64)
                throw new InvalidDataException(fieldName + " must be a 32/64-character hexadecimal identifier.");
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!(character >= '0' && character <= '9')
                    && !(character >= 'a' && character <= 'f'))
                    throw new InvalidDataException(fieldName + " must be lowercase hexadecimal.");
            }
        }

        /// <summary>
        /// 在测试指定故障点抛出显式异常；production 默认 None 不经过任何分支。
        /// </summary>
        private static void ThrowInjected(
            GasCodeGenPublishFaultPoint actual,
            GasCodeGenPublishFaultPoint expected)
        {
            if (actual == expected)
                throw new GasCodeGenInjectedFaultException(actual);
        }

        /// <summary>
        /// 在 NotStarted intent 已完成 production durable write 后应用精确 fixture interleave。
        /// </summary>
        private void ApplyFixtureAfterIntentClaim()
        {
            if (_fixture == null)
                return;
            if (_fixture.AfterClaimMutation == GasCodeGenFixtureSelectorMutation.Replace)
            {
                WriteBytesDurable(SelectorPath, _fixture.AfterClaimSelectorBytes, true);
                return;
            }
            if (_fixture.AfterClaimMutation == GasCodeGenFixtureSelectorMutation.Delete
                && File.Exists(SelectorPath))
            {
                File.Delete(SelectorPath);
            }
        }

        /// <summary>
        /// 在 production temp durable write 后、原子 commit 前模拟真实 no-overwrite competing create。
        /// </summary>
        private void ApplyFixtureBeforeSelectorCommit()
        {
            if (_fixture == null || _fixture.CompetingCreateSelectorBytes == null)
                return;
            WriteBytesDurable(SelectorPath, _fixture.CompetingCreateSelectorBytes, false);
        }

        /// <summary>
        /// 返回 generation archive 的固定 project-relative 子目录。
        /// </summary>
        private string GetGenerationRoot(string generationId)
        {
            ValidateIdentifier(generationId, nameof(generationId));
            return Path.Combine(_generationsRoot, generationId);
        }

        /// <summary>
        /// 在 fixed work root 下创建协议拥有的唯一 staging 目录。
        /// </summary>
        private string CreateUniqueWorkDirectory(string prefix)
        {
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var path = Path.Combine(_workRoot, prefix + Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(path);
                    return path;
                }
                catch (IOException)
                {
                    // 仅重试当前协议命名空间内的 GUID collision。
                }
            }
            throw new IOException("Unable to create generation staging directory.");
        }

        /// <summary>
        /// 最佳努力删除本次明确拥有且不含 reparse point 的 staging 目录。
        /// </summary>
        private static void DeleteOwnedWorkDirectoryBestEffort(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    return;
                var entries = Directory.GetFileSystemEntries(path, "*", SearchOption.AllDirectories);
                for (var index = 0; index < entries.Length; index++)
                    EnsureNoReparsePoint(entries[index]);
                Directory.Delete(path, recursive: true);
            }
            catch
            {
                // 原始 staging 失败仍是主错误；残留由显式 cleanup oracle 报告。
            }
        }

        /// <summary>
        /// 最佳努力删除同一事务创建的精确临时普通文件。
        /// </summary>
        private static void DeleteExactFileBestEffort(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // selector 事务结果优先；temp residue 由 cleanup oracle 独立报告。
            }
        }

        /// <summary>
        /// 拒绝 symlink、junction 或其他 reparse-point 文件系统对象。
        /// </summary>
        private static void EnsureNoReparsePoint(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Reparse point is forbidden: " + path);
        }

        /// <summary>
        /// 将固定 project-relative path 解析为项目内绝对路径。
        /// </summary>
        private string ResolveProjectPath(string relativePath)
        {
            return ResolveUnderProject(_projectRoot, relativePath);
        }

        /// <summary>
        /// 解析 project-relative path 并拒绝越过项目目录边界。
        /// </summary>
        private static string ResolveUnderProject(string projectRoot, string relativePath)
        {
            var path = Path.GetFullPath(Path.Combine(
                projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Store path escapes project root: " + relativePath);
            return path;
        }

        /// <summary>
        /// 校验项目根存在且不是文件系统根。
        /// </summary>
        private static string ResolveProjectRoot(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            var resolved = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(resolved))
                throw new DirectoryNotFoundException("Project root does not exist: " + resolved);
            if (string.Equals(resolved, Path.GetPathRoot(resolved), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Project root cannot be a file-system root.");
            return resolved;
        }

        /// <summary>
        /// 拒绝 disposed Store 被继续用于任何验证或 mutation。
        /// </summary>
        private void EnsureUsable()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GasCodeGenGenerationStore));
        }

        /// <summary>
        /// 拒绝 production Store 调用任何 internal-only fixture seam。
        /// </summary>
        private void EnsureFixtureEnabled()
        {
            EnsureUsable();
            if (_fixture == null)
                throw new InvalidOperationException("Generation Store fixture seam is not enabled.");
        }

        /// <summary>
        /// 记录 production durable writer 实际使用的 mode/options/flush/verification 结果。
        /// </summary>
        private sealed class DurableWriteReceipt
        {
            /// <summary>
            /// 创建一次成功 durable write 的不可变回执。
            /// </summary>
            internal DurableWriteReceipt(
                FileMode fileMode,
                FileOptions fileOptions,
                bool flushToDisk,
                bool bytesVerified)
            {
                FileMode = fileMode;
                FileOptions = fileOptions;
                FlushToDisk = flushToDisk;
                BytesVerified = bytesVerified;
            }

            internal FileMode FileMode { get; }
            internal FileOptions FileOptions { get; }
            internal bool FlushToDisk { get; }
            internal bool BytesVerified { get; }
        }

        /// <summary>
        /// 描述 sealed generation archive 的 descriptor/envelope/selector/route/identity 闭包。
        /// </summary>
        [JsonObject(MemberSerialization.OptIn)]
        public sealed class GenerationRecord
        {
            [JsonProperty(Order = 1)] public int Version { get; set; }
            [JsonProperty(Order = 2)] public string GenerationId { get; set; }
            [JsonProperty(Order = 3)] public string DescriptorSha256 { get; set; }
            [JsonProperty(Order = 4)] public string InstallEnvelopeSha256 { get; set; }
            [JsonProperty(Order = 5)] public string SelectorSha256 { get; set; }
            [JsonProperty(Order = 6)] public string AnalyzerSha256 { get; set; }
            [JsonProperty(Order = 7)] public string RouteScaffoldSha256 { get; set; }
            [JsonProperty(Order = 8)] public string CandidateCompilePlanSha256 { get; set; }
            [JsonProperty(Order = 9)] public string RequiredArtifactSetId { get; set; }
            [JsonProperty(Order = 10)] public string RequiredArtifactSetContractHash { get; set; }
            [JsonProperty(Order = 11)] public string SourceInputHash { get; set; }
            [JsonProperty(Order = 12)] public string SchemaHash { get; set; }
            [JsonProperty(Order = 13)] public string ContentHash { get; set; }
            [JsonProperty(Order = 14)] public string LayoutHash { get; set; }
            [JsonProperty(Order = 15)] public string ArtifactManifestHash { get; set; }
            [JsonProperty(Order = 16)] public string SourceArtifactInventoryHash { get; set; }
            [JsonProperty(Order = 17)] public string CandidateArtifactManifestSha256 { get; set; }
            [JsonProperty(Order = 18)] public bool FullSemanticEligibility { get; set; }
            [JsonProperty(Order = 19)] public string RecordSha256 { get; set; }
        }

        /// <summary>
        /// 描述 fixed exclusive claim、完整 previous/target snapshot、六态 commit 与 durable receipt。
        /// </summary>
        [JsonObject(MemberSerialization.OptIn)]
        public sealed class PublishIntent
        {
            [JsonProperty(Order = 1)] public int Version { get; set; }
            [JsonProperty(Order = 2)] public string ExclusiveClaimId { get; set; }
            [JsonProperty(Order = 3)] public string OwnerSentinel { get; set; }
            [JsonProperty(Order = 4)]
            [JsonConverter(typeof(StringEnumConverter))]
            public GasCodeGenCommitAttemptState CommitAttemptState { get; set; }
            [JsonProperty(Order = 5)] public string ClosureOutcome { get; set; }
            [JsonProperty(Order = 6)] public string GenerationId { get; set; }
            [JsonProperty(Order = 7)] public string PromotionId { get; set; }
            [JsonProperty(Order = 8)] public string PreviousSelectorState { get; set; }
            [JsonProperty(Order = 9)] public string PreviousSelectorBytesBase64 { get; set; }
            [JsonProperty(Order = 10)] public string PreviousSelectorSha256 { get; set; }
            [JsonProperty(Order = 11)] public string TargetSelectorBytesBase64 { get; set; }
            [JsonProperty(Order = 12)] public string TargetSelectorSha256 { get; set; }
            [JsonProperty(Order = 13)] public string DescriptorSha256 { get; set; }
            [JsonProperty(Order = 14)] public string InstallEnvelopeSha256 { get; set; }
            [JsonProperty(Order = 15)] public string RequiredArtifactSetContractHash { get; set; }
            [JsonProperty(Order = 16)] public string ArtifactManifestHash { get; set; }
            [JsonProperty(Order = 17)] public string SourceArtifactInventoryHash { get; set; }
            [JsonProperty(Order = 18)] public string AnalyzerSha256 { get; set; }
            [JsonProperty(Order = 19)] public string RouteScaffoldSha256 { get; set; }
            [JsonProperty(Order = 20)] public string CandidateCompilePlanSha256 { get; set; }
            [JsonProperty(Order = 21)] public string ReceiptExclusiveClaimId { get; set; }
            [JsonProperty(Order = 22)] public string ReceiptPromotionId { get; set; }
            [JsonProperty(Order = 23)] public string ReceiptTargetSelectorSha256 { get; set; }
            [JsonProperty(Order = 24)] public string IntentSha256 { get; set; }
        }

        /// <summary>
        /// 描述 selector commit 后的零选择权 audit provenance。
        /// </summary>
        [JsonObject(MemberSerialization.OptIn)]
        public sealed class ActiveGenerationRef
        {
            [JsonProperty(Order = 1)] public int Version { get; set; }
            [JsonProperty(Order = 2)] public int UnityConsumerAuthority { get; set; }
            [JsonProperty(Order = 3)] public string GenerationId { get; set; }
            [JsonProperty(Order = 4)] public string PromotionId { get; set; }
            [JsonProperty(Order = 5)] public string ExclusiveClaimId { get; set; }
            [JsonProperty(Order = 6)] public string SelectorSha256 { get; set; }
            [JsonProperty(Order = 7)] public string DescriptorSha256 { get; set; }
            [JsonProperty(Order = 8)] public string InstallEnvelopeSha256 { get; set; }
            [JsonProperty(Order = 9)] public string RequiredArtifactSetContractHash { get; set; }
            [JsonProperty(Order = 10)] public string ArtifactManifestHash { get; set; }
            [JsonProperty(Order = 11)] public string SourceArtifactInventoryHash { get; set; }
            [JsonProperty(Order = 12)] public string AnalyzerSha256 { get; set; }
            [JsonProperty(Order = 13)] public string RouteScaffoldSha256 { get; set; }
            [JsonProperty(Order = 14)] public string CandidateCompilePlanSha256 { get; set; }
            [JsonProperty(Order = 15)] public string AuditSha256 { get; set; }
        }

        /// <summary>
        /// 表示 selector 的 Missing 或 Present 完整 byte snapshot。
        /// </summary>
        private sealed class SelectorSnapshot
        {
            /// <summary>
            /// 创建 selector snapshot。
            /// </summary>
            private SelectorSnapshot(bool isPresent, byte[] bytes, string sha256)
            {
                IsPresent = isPresent;
                Bytes = bytes;
                Sha256 = sha256;
            }

            internal bool IsPresent { get; }
            internal byte[] Bytes { get; }
            internal string Sha256 { get; }

            /// <summary>
            /// 返回不携带 bytes/hash 的 Missing snapshot。
            /// </summary>
            internal static SelectorSnapshot Missing()
            {
                return new SelectorSnapshot(false, Array.Empty<byte>(), string.Empty);
            }

            /// <summary>
            /// 返回携带完整 bytes/hash 的 Present snapshot。
            /// </summary>
            internal static SelectorSnapshot Present(byte[] bytes, string sha256)
            {
                return new SelectorSnapshot(true, bytes, sha256);
            }
        }
    }

    /// <summary>
    /// 仅供同程序集 E1 事务 adapter 注入精确文件系统 interleave，并记录 production durable claim 回执。
    /// </summary>
    internal sealed class GasCodeGenGenerationStoreFixture
    {
        /// <summary>
        /// 创建不改变 selector、仅观察 production claim writer 的 fixture。
        /// </summary>
        internal static GasCodeGenGenerationStoreFixture Observe()
        {
            return new GasCodeGenGenerationStoreFixture(
                GasCodeGenFixtureSelectorMutation.None,
                null,
                null);
        }

        /// <summary>
        /// 创建在 durable claim 后、post-claim CAS 前替换 selector 的 fixture。
        /// </summary>
        internal static GasCodeGenGenerationStoreFixture ReplaceAfterClaim(byte[] selectorBytes)
        {
            return new GasCodeGenGenerationStoreFixture(
                GasCodeGenFixtureSelectorMutation.Replace,
                selectorBytes,
                null);
        }

        /// <summary>
        /// 创建在 durable claim 后、post-claim CAS 前删除 Present selector 的 fixture。
        /// </summary>
        internal static GasCodeGenGenerationStoreFixture DeleteAfterClaim()
        {
            return new GasCodeGenGenerationStoreFixture(
                GasCodeGenFixtureSelectorMutation.Delete,
                null,
                null);
        }

        /// <summary>
        /// 创建在 Missing commit 前通过 CreateNew 写入竞争 selector 的 fixture。
        /// </summary>
        internal static GasCodeGenGenerationStoreFixture CompetingCreate(byte[] selectorBytes)
        {
            return new GasCodeGenGenerationStoreFixture(
                GasCodeGenFixtureSelectorMutation.None,
                null,
                selectorBytes);
        }

        /// <summary>
        /// 冻结本次 fixture 的唯一 mutation 与完整 selector bytes。
        /// </summary>
        private GasCodeGenGenerationStoreFixture(
            GasCodeGenFixtureSelectorMutation afterClaimMutation,
            byte[] afterClaimSelectorBytes,
            byte[] competingCreateSelectorBytes)
        {
            AfterClaimMutation = afterClaimMutation;
            AfterClaimSelectorBytes = CloneOrNull(afterClaimSelectorBytes);
            CompetingCreateSelectorBytes = CloneOrNull(competingCreateSelectorBytes);
        }

        internal GasCodeGenFixtureSelectorMutation AfterClaimMutation { get; }
        internal byte[] AfterClaimSelectorBytes { get; }
        internal byte[] CompetingCreateSelectorBytes { get; }
        internal FileMode IntentClaimFileMode { get; private set; }
        internal FileOptions IntentClaimFileOptions { get; private set; }
        internal bool IntentClaimFlushToDisk { get; private set; }
        internal bool IntentClaimBytesVerified { get; private set; }
        internal bool IntentClaimWriteObserved { get; private set; }

        /// <summary>
        /// 由 Store production writer 回填实际 claim persistence 回执，不接受 adapter 自行赋值。
        /// </summary>
        internal void RecordIntentClaimWrite(
            FileMode fileMode,
            FileOptions fileOptions,
            bool flushToDisk,
            bool bytesVerified)
        {
            IntentClaimFileMode = fileMode;
            IntentClaimFileOptions = fileOptions;
            IntentClaimFlushToDisk = flushToDisk;
            IntentClaimBytesVerified = bytesVerified;
            IntentClaimWriteObserved = true;
        }

        /// <summary>
        /// 克隆可选 selector bytes，防止 adapter 在事务中途修改 fixture 输入。
        /// </summary>
        private static byte[] CloneOrNull(byte[] bytes)
        {
            return bytes == null ? null : (byte[])bytes.Clone();
        }
    }

    /// <summary>
    /// 限定 fixture 在 durable claim 后只能不变、替换或删除 canonical selector。
    /// </summary>
    internal enum GasCodeGenFixtureSelectorMutation
    {
        None = 0,
        Replace = 1,
        Delete = 2,
    }

    /// <summary>
    /// 冻结 selector transaction 的六个 durable commit attempt state。
    /// </summary>
    public enum GasCodeGenCommitAttemptState
    {
        NotStarted = 0,
        Armed = 1,
        Committed = 2,
        CompetitionFailed = 3,
        Indeterminate = 4,
        NoOp = 5,
    }

    /// <summary>
    /// 为非 Unity E1 提供确定性故障边界；production 调用必须使用 None。
    /// </summary>
    internal enum GasCodeGenPublishFaultPoint
    {
        None = 0,
        AfterNotStartedIntent = 1,
        AfterArmedIntent = 2,
        AfterSelectorCommit = 3,
        AfterCommittedReceipt = 4,
        ForceCompetitionFailed = 5,
        ForceIndeterminate = 6,
    }

    /// <summary>
    /// 表示 production fault injection 在精确事务边界主动中止，而非业务校验失败。
    /// </summary>
    internal sealed class GasCodeGenInjectedFaultException : Exception
    {
        /// <summary>
        /// 创建携带 exact fault point 的测试异常。
        /// </summary>
        internal GasCodeGenInjectedFaultException(GasCodeGenPublishFaultPoint faultPoint)
            : base("Injected GasCodeGen publish fault: " + faultPoint)
        {
            FaultPoint = faultPoint;
        }

        internal GasCodeGenPublishFaultPoint FaultPoint { get; }
    }

    /// <summary>
    /// 返回 ordinary publish 的 durable state、PromotionId、audit 与 NoOp 判定。
    /// </summary>
    internal sealed class PublishResult
    {
        /// <summary>
        /// 创建 publish 结果；NoOp 时 audit 可为空且 PromotionId 必须为空。
        /// </summary>
        internal PublishResult(
            GasCodeGenCommitAttemptState state,
            string promotionId,
            GasCodeGenGenerationStore.ActiveGenerationRef audit,
            bool isNoOp)
        {
            State = state;
            PromotionId = promotionId;
            Audit = audit;
            IsNoOp = isNoOp;
        }

        internal GasCodeGenCommitAttemptState State { get; }
        internal string PromotionId { get; }
        internal GasCodeGenGenerationStore.ActiveGenerationRef Audit { get; }
        internal bool IsNoOp { get; }
    }

    /// <summary>
    /// 返回 recovery 的原状态、关闭原因、是否补写 audit 与最终 audit。
    /// </summary>
    public sealed class RecoveryResult
    {
        /// <summary>
        /// 创建一次显式 recovery 的确定性结果。
        /// </summary>
        internal RecoveryResult(
            GasCodeGenCommitAttemptState originalState,
            string outcome,
            bool auditWritten,
            GasCodeGenGenerationStore.ActiveGenerationRef audit)
        {
            OriginalState = originalState;
            Outcome = outcome;
            AuditWritten = auditWritten;
            Audit = audit;
        }

        public GasCodeGenCommitAttemptState OriginalState { get; }
        public string Outcome { get; }
        public bool AuditWritten { get; }
        public GasCodeGenGenerationStore.ActiveGenerationRef Audit { get; }
    }
}
