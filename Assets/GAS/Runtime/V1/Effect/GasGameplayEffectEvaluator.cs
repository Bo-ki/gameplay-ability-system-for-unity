using Unity.Collections;

namespace GAS.Runtime
{
    /// <summary>
    /// 解释 Catalog 生成的 postfix evaluator；它只读取冻结输入，不访问 ECS 或 managed 状态。
    /// </summary>
    internal static class GasGameplayEffectEvaluator
    {
        /// <summary>
        /// 在调用方提供的定长栈上解释一段 evaluator，并返回唯一有限结果。
        /// </summary>
        internal static GasEvaluatorFailure TryEvaluate(
            ref GasDefinitionCatalogBlob catalog,
            in GasCatalogRange range,
            NativeArray<float> captures,
            NativeArray<float> valueViews,
            int stackCount,
            NativeArray<float> stack,
            out float result)
        {
            result = 0f;
            if (!IsRangeValid(in range, catalog.EvaluatorInstructions.Length) ||
                range.Count == 0 || stackCount < 0 || stack.Length < range.Count)
                return GasEvaluatorFailure.InvalidRange;

            var depth = 0;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var instruction = catalog.EvaluatorInstructions[range.Start + offset];
                var failure = ExecuteInstruction(
                    in instruction,
                    captures,
                    valueViews,
                    stackCount,
                    stack,
                    ref depth);
                if (failure != GasEvaluatorFailure.None)
                    return failure;
            }

            if (depth != 1 || !IsFinite(stack[0]))
                return GasEvaluatorFailure.InvalidStack;
            result = stack[0];
            return GasEvaluatorFailure.None;
        }

        /// <summary>
        /// 执行一条指令并维护 postfix 栈深度，所有算术错误均显式返回。
        /// </summary>
        private static GasEvaluatorFailure ExecuteInstruction(
            in GasEvaluatorInstructionBlob instruction,
            NativeArray<float> captures,
            NativeArray<float> valueViews,
            int stackCount,
            NativeArray<float> stack,
            ref int depth)
        {
            switch (instruction.Opcode)
            {
                case GasEvaluatorOpcode.PushConstant:
                    return Push(instruction.ConstantValue, stack, ref depth);
                case GasEvaluatorOpcode.PushCapture:
                    if (instruction.OperandIndex < 0 || instruction.OperandIndex >= captures.Length)
                        return GasEvaluatorFailure.OperandOutOfRange;
                    return Push(captures[instruction.OperandIndex], stack, ref depth);
                case GasEvaluatorOpcode.PushValueView:
                    if (instruction.OperandIndex < 0 || instruction.OperandIndex >= valueViews.Length)
                        return GasEvaluatorFailure.OperandOutOfRange;
                    return Push(valueViews[instruction.OperandIndex], stack, ref depth);
                case GasEvaluatorOpcode.PushStackCount:
                    return Push(stackCount, stack, ref depth);
                case GasEvaluatorOpcode.Negate:
                    return Unary(stack, ref depth, negate: true);
                case GasEvaluatorOpcode.Add:
                    return Binary(stack, ref depth, BinaryOperation.Add);
                case GasEvaluatorOpcode.Subtract:
                    return Binary(stack, ref depth, BinaryOperation.Subtract);
                case GasEvaluatorOpcode.Multiply:
                    return Binary(stack, ref depth, BinaryOperation.Multiply);
                case GasEvaluatorOpcode.Divide:
                    return Binary(stack, ref depth, BinaryOperation.Divide);
                case GasEvaluatorOpcode.Maximum:
                    return Binary(stack, ref depth, BinaryOperation.Maximum);
                case GasEvaluatorOpcode.Minimum:
                    return Binary(stack, ref depth, BinaryOperation.Minimum);
                case GasEvaluatorOpcode.CompareLess:
                    return Binary(stack, ref depth, BinaryOperation.CompareLess);
                case GasEvaluatorOpcode.CompareLessOrEqual:
                    return Binary(stack, ref depth, BinaryOperation.CompareLessOrEqual);
                case GasEvaluatorOpcode.CompareEqual:
                    return Binary(stack, ref depth, BinaryOperation.CompareEqual);
                case GasEvaluatorOpcode.Clamp:
                    return Clamp(stack, ref depth);
                case GasEvaluatorOpcode.Select:
                    return Select(stack, ref depth);
                default:
                    return GasEvaluatorFailure.UnsupportedOpcode;
            }
        }

        /// <summary>
        /// 将有限值压入调用方栈并拒绝越界写入。
        /// </summary>
        private static GasEvaluatorFailure Push(
            float value,
            NativeArray<float> stack,
            ref int depth)
        {
            if (!IsFinite(value))
                return GasEvaluatorFailure.NonFinite;
            if (depth < 0 || depth >= stack.Length)
                return GasEvaluatorFailure.StackOverflow;
            stack[depth++] = value;
            return GasEvaluatorFailure.None;
        }

        /// <summary>
        /// 执行一元 Negate，并保持结果有限。
        /// </summary>
        private static GasEvaluatorFailure Unary(
            NativeArray<float> stack,
            ref int depth,
            bool negate)
        {
            if (depth < 1)
                return GasEvaluatorFailure.StackUnderflow;
            var value = negate ? -stack[depth - 1] : stack[depth - 1];
            if (!IsFinite(value))
                return GasEvaluatorFailure.NonFinite;
            stack[depth - 1] = value;
            return GasEvaluatorFailure.None;
        }

        /// <summary>
        /// 弹出两个操作数并按固定左值/右值顺序写回结果。
        /// </summary>
        private static GasEvaluatorFailure Binary(
            NativeArray<float> stack,
            ref int depth,
            BinaryOperation operation)
        {
            if (depth < 2)
                return GasEvaluatorFailure.StackUnderflow;
            var right = stack[--depth];
            var left = stack[depth - 1];
            if (operation == BinaryOperation.Divide && right == 0f)
                return GasEvaluatorFailure.DivideByZero;
            var value = operation switch
            {
                BinaryOperation.Add => left + right,
                BinaryOperation.Subtract => left - right,
                BinaryOperation.Multiply => left * right,
                BinaryOperation.Divide => left / right,
                BinaryOperation.Maximum => left > right ? left : right,
                BinaryOperation.Minimum => left < right ? left : right,
                BinaryOperation.CompareLess => left < right ? 1f : 0f,
                BinaryOperation.CompareLessOrEqual => left <= right ? 1f : 0f,
                BinaryOperation.CompareEqual => left == right ? 1f : 0f,
                _ => float.NaN,
            };
            if (!IsFinite(value))
                return GasEvaluatorFailure.NonFinite;
            stack[depth - 1] = value;
            return GasEvaluatorFailure.None;
        }

        /// <summary>
        /// 按 value、minimum、maximum 三元组执行闭区间 clamp。
        /// </summary>
        private static GasEvaluatorFailure Clamp(
            NativeArray<float> stack,
            ref int depth)
        {
            if (depth < 3)
                return GasEvaluatorFailure.StackUnderflow;
            var maximum = stack[--depth];
            var minimum = stack[--depth];
            var value = stack[depth - 1];
            if (minimum > maximum)
                return GasEvaluatorFailure.InvalidBounds;
            value = value < minimum ? minimum : value > maximum ? maximum : value;
            if (!IsFinite(value))
                return GasEvaluatorFailure.NonFinite;
            stack[depth - 1] = value;
            return GasEvaluatorFailure.None;
        }

        /// <summary>
        /// 按 condition、whenTrue、whenFalse 三元组选择一个值。
        /// </summary>
        private static GasEvaluatorFailure Select(
            NativeArray<float> stack,
            ref int depth)
        {
            if (depth < 3)
                return GasEvaluatorFailure.StackUnderflow;
            var whenFalse = stack[--depth];
            var whenTrue = stack[--depth];
            var condition = stack[depth - 1];
            var value = condition != 0f ? whenTrue : whenFalse;
            if (!IsFinite(value))
                return GasEvaluatorFailure.NonFinite;
            stack[depth - 1] = value;
            return GasEvaluatorFailure.None;
        }

        /// <summary>
        /// 验证 evaluator range 使用减法避免 Start+Count 整数回绕。
        /// </summary>
        private static bool IsRangeValid(
            in GasCatalogRange range,
            int length)
        {
            return range.Start >= 0 && range.Count > 0 && range.Start <= length &&
                   range.Count <= length - range.Start;
        }

        /// <summary>
        /// 在不依赖高版本 runtime API 的前提下拒绝 NaN 与无穷值。
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// 表示 evaluator 解释期间遇到的确定性失败原因。
        /// </summary>
        private enum BinaryOperation : byte
        {
            Add,
            Subtract,
            Multiply,
            Divide,
            Maximum,
            Minimum,
            CompareLess,
            CompareLessOrEqual,
            CompareEqual,
        }
    }

    /// <summary>
    /// 标识 evaluator 的机器可读失败，不与业务 requirement 拒绝混用。
    /// </summary>
    internal enum GasEvaluatorFailure : byte
    {
        None,
        InvalidRange,
        InvalidStack,
        StackOverflow,
        StackUnderflow,
        OperandOutOfRange,
        DivideByZero,
        InvalidBounds,
        NonFinite,
        UnsupportedOpcode,
    }
}
