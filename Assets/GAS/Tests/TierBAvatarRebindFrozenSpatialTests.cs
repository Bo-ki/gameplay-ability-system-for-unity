using GAS.Runtime;
using NUnit.Framework;

namespace GAS.Tests
{
    /// <summary>
    /// 验证 Runtime v1 目标引用的 Avatar 身份与冻结空间值；不替代完整 V12 运行时链路验收。
    /// </summary>
    public sealed class TierBAvatarRebindFrozenSpatialTests
    {
        /// <summary>
        /// 验证同一 ASC 的 Avatar 编号或绑定代次变化会形成不同目标身份。
        /// </summary>
        [TestCase(502UL, 7U)]
        [TestCase(501UL, 8U)]
        public void BoundaryTargetRef_换绑保留Asc但改变目标身份(
            ulong avatarStableId,
            uint bindingGeneration)
        {
            var battle = new BattleInstanceHandle(1, 10, 1);
            var asc = new OwnerAscHandle(101, 1);
            var original = BoundaryTargetRef.ForAsc(battle, asc, 501, 7);
            var duplicate = BoundaryTargetRef.ForAsc(battle, asc, 501, 7);
            var rebound = BoundaryTargetRef.ForAsc(battle, asc, avatarStableId, bindingGeneration);

            Assert.That(original.HasAvatarBinding, Is.True);
            Assert.That(rebound.HasAvatarBinding, Is.True);
            Assert.That(rebound.TargetAsc, Is.EqualTo(original.TargetAsc));
            Assert.That(rebound.BattleInstance, Is.EqualTo(original.BattleInstance));
            Assert.That(original.TargetAvatarStableId, Is.EqualTo(501UL));
            Assert.That(original.TargetAvatarBindingGeneration, Is.EqualTo(7U));
            Assert.That(original.Equals(duplicate), Is.True);
            Assert.That(original.GetHashCode(), Is.EqualTo(duplicate.GetHashCode()));
            Assert.That(original.Equals(rebound), Is.False);
        }

        /// <summary>
        /// 验证输入重新采样或 Avatar 换绑不会改写已构造目标内保存的空间快照。
        /// </summary>
        [Test]
        public void FrozenSpatial_重采样与换绑不改写既有目标()
        {
            var battle = new BattleInstanceHandle(1, 10, 1);
            var asc = new OwnerAscHandle(101, 1);
            var sampledSpatial = CreatePointSnapshot(2f, 3f);
            var original = BoundaryTargetRef.ForAsc(battle, asc, 501, 7, sampledSpatial);

            sampledSpatial = CreatePointSnapshot(20f, 30f);
            var resampled = BoundaryTargetRef.ForAsc(battle, asc, 501, 7, sampledSpatial);
            var rebound = BoundaryTargetRef.ForAsc(battle, asc, 502, 8, sampledSpatial);
            var expectedSpatial = CreatePointSnapshot(2f, 3f);

            Assert.That(original.HasFrozenSpatial, Is.True);
            Assert.That(original.SpatialSnapshot.Equals(expectedSpatial), Is.True);
            Assert.That(original.SpatialSnapshot.GetHashCode(), Is.EqualTo(expectedSpatial.GetHashCode()));
            Assert.That(original.SpatialSnapshot.Scalar0, Is.EqualTo(2f));
            Assert.That(original.SpatialSnapshot.Scalar2, Is.EqualTo(3f));
            Assert.That(original.SpatialSnapshot.Equals(sampledSpatial), Is.False);
            Assert.That(original.Equals(resampled), Is.False);
            Assert.That(rebound.TargetAsc, Is.EqualTo(original.TargetAsc));
            Assert.That(rebound.SpatialSnapshot.Equals(sampledSpatial), Is.True);
            Assert.That(original.TargetAvatarStableId, Is.EqualTo(501UL));
            Assert.That(original.TargetAvatarBindingGeneration, Is.EqualTo(7U));
        }

        /// <summary>
        /// 创建固定样本身份的空间点，隔离测试中的位置变化。
        /// </summary>
        private static GasBoundarySpatialSnapshot CreatePointSnapshot(float x, float z)
        {
            return new GasBoundarySpatialSnapshot(
                GasTargetDataVariant.FrozenSpatialPoint,
                71,
                0,
                x,
                0f,
                z,
                0f,
                0f,
                0f,
                0f,
                0f);
        }
    }
}
