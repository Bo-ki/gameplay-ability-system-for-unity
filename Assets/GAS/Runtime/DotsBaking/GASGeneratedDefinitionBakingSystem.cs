using GAS.Runtime.Generated;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.DotsBaking
{
    /// <summary>
    /// 冷启动 Definition Baking System。
    /// 在 Luban 配置表加载完毕后运行一次，将 Row 数据转换为 DOTS Entity + BlobAsset。
    ///
    /// 执行顺序：
    /// 1. 从 RegistrySnapshot 获取所有 Row
    /// 2. Phase 1: 调用 BlobDefinitionBuilder.Build* 创建 BlobAssetReference
    /// 3. Phase 2: 构建 StaticLookup 二分查找表
    /// 4. Phase 3: 调用 GasGeneratedBakers.Bake* 创建 Definition Entity
    /// 5. ECB playback — 所有结构变化批量执行
    /// 6. 自身禁用（Enabled = false）
    ///
    /// CASE-39/40/41 合规：Baker 无状态、仅 ECB 写入；Baking System 管理依赖和生命周期。
    /// </summary>
    [RequireMatchingQueriesForUpdate]
    public partial class GASGeneratedDefinitionBakingSystem : SystemBase
    {
        private bool _hasRun;

        /// <summary>
        /// 由外部冷启动流程调用，传入 Luban 生成的 RegistrySnapshot。
        /// 调用后本 System 在下一帧 OnUpdate 中执行 baking。
        /// </summary>
        public HeadlessAutoChessGeneratedRegistrySnapshot Snapshot { get; set; }

        protected override void OnCreate()
        {
            _hasRun = false;
            Enabled = false; // 默认不运行，等待外部激活
        }

        protected override void OnUpdate()
        {
            if (_hasRun) return;

            var snapshot = Snapshot;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            BakeAbilities(ref ecb, snapshot);
            BakeGameplayEffects(ref ecb, snapshot);
            BakeAttributeSets(ref ecb, snapshot);
            BakeAttributes(ref ecb, snapshot);
            BakeGameplayTags(ref ecb, snapshot);
            BakeGameplayCues(ref ecb, snapshot);
            BakeTimelines(ref ecb, snapshot);
            BakeSummons(ref ecb, snapshot);

            ecb.Playback(EntityManager);
            ecb.Dispose();

            _hasRun = true;
            Enabled = false;
        }

        private void BakeAbilities(
            ref EntityCommandBuffer ecb,
            HeadlessAutoChessGeneratedRegistrySnapshot snapshot)
        {
            var rows = snapshot.AbilityRows;
            if (rows == null || rows.Count == 0) return;

            // Build StaticLookup
            var lookup = new BlobAbilityDefinitionLookup();
            lookup.BuildFromRows(rows, Allocator.Persistent);
            // TODO: Register lookup in global static registry for runtime access

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var blob = BlobDefinitionBuilder.BuildBlobAbilityDefinition(row, Allocator.Persistent);
                GasGeneratedBakers.BakeBlobAbilityDefinition(ref ecb, row, blob);
            }
        }

        private void BakeGameplayEffects(
            ref EntityCommandBuffer ecb,
            HeadlessAutoChessGeneratedRegistrySnapshot snapshot)
        {
            var rows = snapshot.GameplayEffectRows;
            if (rows == null || rows.Count == 0) return;

            var lookup = new BlobGameplayEffectDefinitionLookup();
            lookup.BuildFromRows(rows, Allocator.Persistent);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var blob = BlobDefinitionBuilder.BuildBlobGameplayEffectDefinition(row, Allocator.Persistent);
                GasGeneratedBakers.BakeBlobGameplayEffectDefinition(ref ecb, row, blob);
            }
        }

        private void BakeAttributeSets(
            ref EntityCommandBuffer ecb,
            HeadlessAutoChessGeneratedRegistrySnapshot snapshot)
        {
            var rows = snapshot.AttributeSetRows;
            if (rows == null || rows.Count == 0) return;

            var lookup = new BlobAttributeSetDefinitionLookup();
            lookup.BuildFromRows(rows, Allocator.Persistent);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var blob = BlobDefinitionBuilder.BuildBlobAttributeSetDefinition(row, Allocator.Persistent);
                GasGeneratedBakers.BakeBlobAttributeSetDefinition(ref ecb, row, blob);
            }
        }

        private void BakeAttributes(
            ref EntityCommandBuffer ecb,
            HeadlessAutoChessGeneratedRegistrySnapshot snapshot)
        {
            var rows = snapshot.AttributeRows;
            if (rows == null || rows.Count == 0) return;

            var lookup = new BlobAttributeDefinitionLookup();
            lookup.BuildFromRows(rows, Allocator.Persistent);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var blob = BlobDefinitionBuilder.BuildBlobAttributeDefinition(row, Allocator.Persistent);
                GasGeneratedBakers.BakeBlobAttributeDefinition(ref ecb, row, blob);
            }
        }

        private void BakeGameplayTags(
            ref EntityCommandBuffer ecb,
            HeadlessAutoChessGeneratedRegistrySnapshot snapshot)
        {
            var rows = snapshot.GameplayTagRows;
            if (rows == null || rows.Count == 0) return;

            var lookup = new BlobGameplayTagDefinitionLookup();
            lookup.BuildFromRows(rows, Allocator.Persistent);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var blob = BlobDefinitionBuilder.BuildBlobGameplayTagDefinition(row, Allocator.Persistent);
                GasGeneratedBakers.BakeBlobGameplayTagDefinition(ref ecb, row, blob);
            }
        }

        private void BakeGameplayCues(
            ref EntityCommandBuffer ecb,
            HeadlessAutoChessGeneratedRegistrySnapshot snapshot)
        {
            var rows = snapshot.GameplayCueRows;
            if (rows == null || rows.Count == 0) return;

            var lookup = new BlobGameplayCueDefinitionLookup();
            lookup.BuildFromRows(rows, Allocator.Persistent);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var blob = BlobDefinitionBuilder.BuildBlobGameplayCueDefinition(row, Allocator.Persistent);
                GasGeneratedBakers.BakeBlobGameplayCueDefinition(ref ecb, row, blob);
            }
        }

        private void BakeTimelines(
            ref EntityCommandBuffer ecb,
            HeadlessAutoChessGeneratedRegistrySnapshot snapshot)
        {
            var rows = snapshot.TimelineRows;
            if (rows == null || rows.Count == 0) return;

            var lookup = new BlobTimelineDefinitionLookup();
            lookup.BuildFromRows(rows, Allocator.Persistent);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var blob = BlobDefinitionBuilder.BuildBlobTimelineDefinition(row, Allocator.Persistent);
                GasGeneratedBakers.BakeBlobTimelineDefinition(ref ecb, row, blob);
            }
        }

        private void BakeSummons(
            ref EntityCommandBuffer ecb,
            HeadlessAutoChessGeneratedRegistrySnapshot snapshot)
        {
            var rows = snapshot.SummonRows;
            if (rows == null || rows.Count == 0) return;

            var lookup = new BlobSummonDefinitionLookup();
            lookup.BuildFromRows(rows, Allocator.Persistent);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var blob = BlobDefinitionBuilder.BuildBlobSummonDefinition(row, Allocator.Persistent);
                GasGeneratedBakers.BakeBlobSummonDefinition(ref ecb, row, blob);
            }
        }
    }
}
