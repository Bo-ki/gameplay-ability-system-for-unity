using System;
using System.Collections.Generic;

namespace Gas.CodeGen.SourceGenerator.Tests
{
    /// <summary>
    /// 串行执行 D1-A 非 Unity contract 与 generator tests，并以进程退出码形成最小验收门。
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// 运行全部固定测试；任何一个失败都立即返回非零并打印精确 case。
        /// </summary>
        private static int Main()
        {
            List<TestCase> cases = new List<TestCase>();
            cases.AddRange(CodecTests.GetCases());
            cases.AddRange(GeneratorTests.GetCases());
            int passed = 0;
            foreach (TestCase testCase in cases)
            {
                try
                {
                    testCase.Execute();
                    passed++;
                    Console.WriteLine("PASS " + testCase.Name);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("FAIL " + testCase.Name);
                    Console.Error.WriteLine(exception);
                    return 1;
                }
            }

            Console.WriteLine("PASSED " + passed + "/" + cases.Count);
            return 0;
        }
    }
}
