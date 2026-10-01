using GAS.Runtime;

namespace GAS.Editor.CodeGen.Proofs
{
    /// <summary>
    /// 将 CapacityProof 的 bounds、推导、consumer、ABI、内存与 Red 编码为单一 canonical ProofHash。
    /// </summary>
    internal static class GasRuntimeV1CapacityProofHasher
    {
        /// <summary>
        /// 固定数组、排序失败、计算 ABI hash 并生成最终 proof hash。
        /// </summary>
        public static void Finalize(GasCapacityProofBuildState state)
        {
            state.Failures.Sort(GasProofCanonicalRules.CompareFailures);
            state.Payload.DefinitionIdentities = state.DefinitionIdentities;
            state.Payload.Bounds = state.Bounds.ToArray();
            state.Payload.Derivations = state.Derivations.ToArray();
            state.Payload.RuntimeLimits = state.RuntimeLimits.ToArray();
            state.Payload.ConsumerMap = state.Consumers.ToArray();
            state.Payload.MemoryEvidence = state.Memory.ToArray();
            state.Payload.Failures = state.Failures.ToArray();
            if (!GasRuntimeV1CapacityProofBuilder.TryFinalizePayloadEncodingBudget(state))
            {
                state.Payload.LayoutAbiHash = string.Empty;
                state.Payload.ProofHash = string.Empty;
                return;
            }
            state.Payload.LayoutAbiHash = ComputeLayoutAbiHash(state.Payload.MemoryEvidence);
            var summary = state.Payload.MemorySummary;
            summary.LayoutAbiHash = state.Payload.LayoutAbiHash;
            state.Payload.MemorySummary = summary;
            if (!GasRuntimeV1CapacityProofBuilder.TryFinalizePayloadEncodingBudget(state))
            {
                state.Payload.ProofHash = string.Empty;
                return;
            }
            state.Payload.ProofHash =
                GasProofCanonicalHashWriter.IsSha256Hex(state.Payload.LayoutAbiHash)
                    ? ComputeProofHash(state.Payload)
                    : string.Empty;
        }

        /// <summary>
        /// 计算全部 Runtime element type 的 size/alignment ABI hash，忽略目标规模 count。
        /// </summary>
        internal static string ComputeLayoutAbiHash(GasCapacityMemoryEvidence[] entries)
        {
            using (var writer = new GasProofCanonicalHashWriter())
            {
                writer.WriteString("gas-runtime-v1-capacity-abi/1");
                var count = 0;
                foreach (var entry in entries)
                {
                    if (entry.MemoryKind != GasCapacityMemoryKind.Summary)
                        count++;
                }
                writer.WriteInt32(count);
                foreach (var entry in entries)
                {
                    if (entry.MemoryKind == GasCapacityMemoryKind.Summary)
                        continue;
                    writer.WriteString(entry.EvidenceId);
                    writer.WriteString(entry.ElementTypeName);
                    writer.WriteInt32(entry.ElementSizeBytes);
                    writer.WriteInt32(entry.AlignmentBytes);
                    writer.WriteString(entry.AbiFingerprint);
                }
                return writer.ComputeHash();
            }
        }

        /// <summary>
        /// 计算绑定 graph、contract、layout、profile、所有证据与 Red 的最终 SHA-256。
        /// </summary>
        internal static string ComputeProofHash(GasRuntimeV1CapacityProofPayload payload)
        {
            using (var writer = new GasProofCanonicalHashWriter())
            {
                WriteHeader(writer, payload);
                WriteProfile(writer, in payload.Profile);
                writer.WriteProvenance(in payload.ScaleProfileProvenance);
                writer.WriteProvenance(in payload.MemoryBudgetProvenance);
                WriteDefinitionIdentities(writer, payload.DefinitionIdentities);
                WriteBounds(writer, payload.Bounds);
                WriteDerivations(writer, payload.Derivations);
                WriteRuntimeLimits(writer, payload.RuntimeLimits);
                WriteConsumers(writer, payload.ConsumerMap);
                WriteMemory(writer, payload.MemoryEvidence, in payload.MemorySummary);
                writer.WriteInt32(payload.Failures.Length);
                foreach (var failure in payload.Failures)
                    writer.WriteFailure(in failure);
                return writer.ComputeHash();
            }
        }

        /// <summary>
        /// 写入 CapacityProof 的版本、外部 identity 与稳定 coverage gaps。
        /// </summary>
        private static void WriteHeader(
            GasProofCanonicalHashWriter writer,
            GasRuntimeV1CapacityProofPayload payload)
        {
            writer.WriteString("gas-runtime-v1-capacity-proof-payload/2");
            writer.WriteInt32(payload.ProofSchemaVersion);
            writer.WriteString(payload.AlgorithmVersion);
            writer.WriteInt32(payload.GraphSchemaVersion);
            writer.WriteString(payload.GeneratorVersion);
            writer.WriteString(payload.CanonicalGraphHash);
            writer.WriteString(payload.ContractMatrixHash);
            writer.WriteString(payload.TargetScaleId);
            writer.WriteString(payload.LayoutHash);
            writer.WriteString(payload.LayoutAbiHash);
            writer.WriteUInt32((uint)payload.CoverageGaps);
        }

        /// <summary>
        /// 写入 definition-level canonical identity 与 provenance，不依赖 bound/failure 间接绑定。
        /// </summary>
        private static void WriteDefinitionIdentities(
            GasProofCanonicalHashWriter writer,
            GasCapacityDefinitionIdentityProofEntry[] entries)
        {
            writer.WriteInt32(entries.Length);
            foreach (var entry in entries)
            {
                writer.WriteString(entry.DefinitionKey);
                writer.WriteInt32(entry.DefinitionId);
                writer.WriteInt32(entry.DefinitionOrdinal);
                writer.WriteProvenance(in entry.Provenance);
            }
        }

        /// <summary>
        /// 以显式字段顺序写入完整 GasScaleProfile，不依赖反射或 struct padding。
        /// </summary>
        private static void WriteProfile(
            GasProofCanonicalHashWriter writer,
            in GasScaleProfile profile)
        {
            writer.WriteInt32(profile.ProfileId);
            writer.WriteInt32(profile.ProfileVersion);
            writer.WriteUInt64(profile.ProfileHash);
            writer.WriteInt32(profile.MaxFixedTicksPerBatch);
            writer.WriteInt32(profile.MaximumDeltaTimeTicks);
            writer.WriteInt32(profile.MaxSpawnBatchSize);
            writer.WriteInt32(profile.MaxBattleInstanceCount);
            writer.WriteInt32(profile.MaxAscRegistryCount);
            writer.WriteInt32(profile.MaxBoundaryCommandCount);
            writer.WriteInt32(profile.MaxBoundaryCommandPayloadCount);
            writer.WriteInt32(profile.MaxOwnerPlanCount);
            writer.WriteInt32(profile.MaxResolvedTargetCount);
            writer.WriteInt32(profile.MaxEffectOperationCount);
            writer.WriteInt32(profile.MaxOwnerReservationCount);
            writer.WriteInt32(profile.MaxTargetReservationCount);
            writer.WriteInt32(profile.MaxCoreFactCount);
            writer.WriteInt32(profile.MaxNextTickRouteCount);
            writer.WriteInt32(profile.MaxStructuralIntentCount);
            WriteProfileTail(writer, in profile);
        }

        /// <summary>
        /// 写入 GasScaleProfile 的 outbox、初始化、lifecycle、effect 与 command 容量尾部。
        /// </summary>
        private static void WriteProfileTail(
            GasProofCanonicalHashWriter writer,
            in GasScaleProfile profile)
        {
            writer.WriteInt32(profile.MaxSessionBoundaryFactCount);
            writer.WriteInt32(profile.MaxAscBoundaryFactCount);
            writer.WriteInt32(profile.MaxPendingAttributeInitializationCount);
            writer.WriteInt32(profile.MaxPendingTagInitializationCount);
            writer.WriteInt32(profile.MaxPendingGrantedAbilityInitializationCount);
            writer.WriteInt32(profile.MaxGrantedAbilityCount);
            writer.WriteInt32(profile.MaxAbilityActivationCount);
            writer.WriteInt32(profile.MaxAbilityContinuationCount);
            writer.WriteInt32(profile.MaxAbilitySubscriptionCount);
            writer.WriteInt32(profile.MaxCooldownGateCount);
            writer.WriteInt32(profile.MaxActivationOwnedContributionCount);
            writer.WriteInt32(profile.MaxEmittedApplicationRefCount);
            writer.WriteInt32(profile.MaxActiveEffectCount);
            writer.WriteInt32(profile.MaxPayloadRangeRecordCount);
            writer.WriteInt32(profile.MaxPayloadValueCount);
            writer.WriteInt32(profile.MaxAttributeAggregatorCount);
            writer.WriteInt32(profile.MaxLiveDependencyCount);
            writer.WriteInt32(profile.MaxLiveDependencyRouteCount);
            writer.WriteInt32(profile.MaxPendingCommandCount);
        }

        /// <summary>
        /// 写入 definition-major、dimension-minor 的全部 semantic bounds 与 provenance。
        /// </summary>
        private static void WriteBounds(
            GasProofCanonicalHashWriter writer,
            GasCapacityBoundProofEntry[] entries)
        {
            writer.WriteInt32(entries.Length);
            foreach (var entry in entries)
            {
                writer.WriteString(entry.DefinitionKey);
                writer.WriteInt32(entry.DefinitionId);
                writer.WriteInt32(entry.DefinitionOrdinal);
                writer.WriteByte((byte)entry.Dimension);
                writer.WriteInt64(entry.Maximum);
                writer.WriteProvenance(in entry.Provenance);
            }
        }

        /// <summary>
        /// 写入所有 checked derivation 的表达式、操作数、结果与 provenance。
        /// </summary>
        private static void WriteDerivations(
            GasProofCanonicalHashWriter writer,
            GasProofDerivationEntry[] entries)
        {
            writer.WriteInt32(entries.Length);
            foreach (var entry in entries)
            {
                writer.WriteString(entry.DerivationId);
                writer.WriteString(entry.DimensionId);
                writer.WriteString(entry.Expression);
                writer.WriteString(entry.Operator);
                writer.WriteString(entry.LeftOperandName);
                writer.WriteInt64(entry.LeftOperandValue);
                writer.WriteString(entry.RightOperandName);
                writer.WriteInt64(entry.RightOperandValue);
                writer.WriteInt64(entry.Result);
                writer.WriteString(entry.Unit);
                writer.WriteInt32(entry.DefinitionId);
                writer.WriteString(entry.DefinitionKey);
                writer.WriteInt32(entry.ProfileId);
                writer.WriteInt32(entry.CanonicalOrdinal);
                writer.WriteProvenance(in entry.Provenance);
            }
        }

        /// <summary>
        /// 写入 Runtime/Profile capacity 对账表。
        /// </summary>
        private static void WriteRuntimeLimits(
            GasProofCanonicalHashWriter writer,
            GasRuntimeCapacityLimitEntry[] entries)
        {
            writer.WriteInt32(entries.Length);
            foreach (var entry in entries)
            {
                writer.WriteString(entry.LimitId);
                writer.WriteString(entry.ProfileField);
                writer.WriteString(entry.Formula);
                writer.WriteInt64(entry.Required);
                writer.WriteInt64(entry.Available);
                writer.WriteString(entry.Unit);
                writer.WriteByte((byte)entry.ConsumerStatus);
                writer.WriteString(entry.FirstConsumer);
            }
        }

        /// <summary>
        /// 写入固定顺序的 Runtime v1 consumer map。
        /// </summary>
        private static void WriteConsumers(
            GasProofCanonicalHashWriter writer,
            GasProofConsumerEntry[] entries)
        {
            writer.WriteInt32(entries.Length);
            foreach (var entry in entries)
            {
                writer.WriteString(entry.DimensionId);
                writer.WriteString(entry.ProfileField);
                writer.WriteString(entry.FirstConsumer);
                writer.WriteString(entry.ConsumptionMoment);
                writer.WriteByte((byte)entry.Status);
            }
        }

        /// <summary>
        /// 写入逐 array/buffer 内存证据及汇总预算对账。
        /// </summary>
        private static void WriteMemory(
            GasProofCanonicalHashWriter writer,
            GasCapacityMemoryEvidence[] entries,
            in GasCapacityMemorySummary summary)
        {
            writer.WriteInt32(entries.Length);
            foreach (var entry in entries)
            {
                writer.WriteString(entry.EvidenceId);
                writer.WriteByte((byte)entry.MemoryKind);
                writer.WriteString(entry.Owner);
                writer.WriteString(entry.ElementTypeName);
                writer.WriteString(entry.Formula);
                writer.WriteInt64(entry.ElementCount);
                writer.WriteInt32(entry.ElementSizeBytes);
                writer.WriteInt32(entry.AlignmentBytes);
                writer.WriteInt64(entry.CheckedByteCount);
                writer.WriteString(entry.AbiFingerprint);
            }
            writer.WriteInt64(summary.OneTickScratchBytes);
            writer.WriteInt64(summary.EffectiveTickBatchScratchBytes);
            writer.WriteInt64(summary.DeclaredHardCapTickBatchScratchBytes);
            writer.WriteInt64(summary.DurableSessionBytes);
            writer.WriteInt64(summary.DurableBytesPerAsc);
            writer.WriteInt64(summary.DurableAllAscBytes);
            writer.WriteInt64(summary.EffectiveScopedPayloadBytes);
            writer.WriteInt64(summary.DeclaredHardCapScopedPayloadBytes);
            writer.WriteInt64(summary.DeclaredMemoryBudgetBytes);
            writer.WriteString(summary.LayoutAbiHash);
        }
    }
}
