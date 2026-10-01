using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 为生成期 proof 提供显式 checked 非负整数运算，禁止复用 Runtime scratch 的溢出归零行为。
    /// </summary>
    public static class GasCheckedProofMath
    {
        /// <summary>
        /// 对两个非负长整数执行 checked 加法，负数或溢出均返回失败。
        /// </summary>
        public static bool TryAdd(long left, long right, out long result)
        {
            result = 0;
            if (left < 0 || right < 0)
                return false;

            try
            {
                result = checked(left + right);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        /// <summary>
        /// 对两个非负长整数执行 checked 乘法，负数或溢出均返回失败。
        /// </summary>
        public static bool TryMultiply(long left, long right, out long result)
        {
            result = 0;
            if (left < 0 || right < 0)
                return false;

            try
            {
                result = checked(left * right);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        /// <summary>
        /// 计算非负元素数量需要的 64 位 word 数，拒绝负值和 int 表示范围溢出。
        /// </summary>
        public static bool TryCalculateWordCount(long elementCount, out int wordCount)
        {
            wordCount = 0;
            if (elementCount < 0)
                return false;
            if (elementCount == 0)
                return true;
            if (!TryAdd(elementCount, 63, out var rounded) || rounded / 64 > int.MaxValue)
                return false;

            wordCount = (int)(rounded / 64);
            return true;
        }

        /// <summary>
        /// 将非负长整数收窄为 NativeArray 可表示的 int 长度。
        /// </summary>
        public static bool TryToArrayLength(long value, out int result)
        {
            result = 0;
            if (value < 0 || value > int.MaxValue)
                return false;

            result = (int)value;
            return true;
        }
    }
}
