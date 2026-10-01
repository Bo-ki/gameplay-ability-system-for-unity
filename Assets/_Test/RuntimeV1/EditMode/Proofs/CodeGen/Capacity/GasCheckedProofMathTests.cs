using GAS.Runtime;
using NUnit.Framework;

namespace GAS.Editor.Tests.CodeGen.Proofs
{
    /// <summary>
    /// 验证 proof 专用 checked 算术不会继承 Runtime scratch 的溢出归零语义。
    /// </summary>
    [TestFixture]
    public sealed class GasCheckedProofMathTests
    {
        /// <summary>
        /// 验证 long 加法与乘法在边界值成功、N+1 溢出时显式失败。
        /// </summary>
        [Test]
        public void Long边界_N加一与乘法溢出应显式失败()
        {
            Assert.That(GasCheckedProofMath.TryAdd(long.MaxValue - 1, 1, out var maximum), Is.True);
            Assert.That(maximum, Is.EqualTo(long.MaxValue));
            Assert.That(GasCheckedProofMath.TryAdd(long.MaxValue, 1, out _), Is.False);
            Assert.That(GasCheckedProofMath.TryMultiply(long.MaxValue / 2, 2, out _), Is.True);
            Assert.That(GasCheckedProofMath.TryMultiply(long.MaxValue, 2, out _), Is.False);
        }

        /// <summary>
        /// 验证 word-count 在 255/256/257 边界分别产生 4/4/5。
        /// </summary>
        [Test]
        public void WordCount边界_二百五十七应进入第五个Word()
        {
            Assert.That(GasCheckedProofMath.TryCalculateWordCount(255, out var words255), Is.True);
            Assert.That(GasCheckedProofMath.TryCalculateWordCount(256, out var words256), Is.True);
            Assert.That(GasCheckedProofMath.TryCalculateWordCount(257, out var words257), Is.True);
            Assert.That(words255, Is.EqualTo(4));
            Assert.That(words256, Is.EqualTo(4));
            Assert.That(words257, Is.EqualTo(5));
        }

        /// <summary>
        /// 验证 NativeArray int length 的上界与 N+1 均被精确区分。
        /// </summary>
        [Test]
        public void ArrayLength边界_IntMax通过而N加一失败()
        {
            Assert.That(GasCheckedProofMath.TryToArrayLength(int.MaxValue, out var maximum), Is.True);
            Assert.That(maximum, Is.EqualTo(int.MaxValue));
            Assert.That(GasCheckedProofMath.TryToArrayLength((long)int.MaxValue + 1, out _), Is.False);
        }
    }
}
