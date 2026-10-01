using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GAS.AutoChessDemo
{
    /// <summary>
    /// 统一 Runtime V1 可运行验证的命令行参数、证据读取和原子结果写入，避免 Player 与 Editor 形成两套身份协议。
    /// </summary>
    public static class RuntimeV1RunnableEvidence
    {
        public const string RunArgument = "-gasRuntimeV1Runnable";
        public const string RunIdArgument = "-gasRuntimeV1RunnableRunId";
        public const string ManifestArgument = "-gasRuntimeV1RunnableManifest";
        public const string ManifestSha256Argument = "-gasRuntimeV1RunnableManifestSha256";
        public const string ManifestSchema = "RuntimeV1RunnableRunManifest-v1";
        public const string PlayerResultSchema = "RuntimeV1RunnablePlayerResult-v1";
        public const string PlayerResultFileName = "PlayerResult.json";
        public const string BuildScenePath =
            "Assets/AutoChessDemo/Presentation/Scenes/AutoChessLogDemo.unity";

        /// <summary>
        /// 判断当前进程是否显式启用了 Runtime V1 可运行验证。
        /// </summary>
        public static bool IsRequested()
        {
            return HasArgument(Environment.GetCommandLineArgs(), RunArgument);
        }

        /// <summary>
        /// 从当前命令行读取指定参数值，缺失时返回空字符串。
        /// </summary>
        public static string GetCommandLineValue(string argument)
        {
            return GetArgumentValue(Environment.GetCommandLineArgs(), argument);
        }

        /// <summary>
        /// 读取并校验由 launcher 指定的 RunManifest 投影和文件哈希。
        /// </summary>
        public static RuntimeV1RunnableRunManifest ReadValidatedManifest(
            string manifestPath,
            string expectedSha256)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
                throw new InvalidOperationException("Runtime V1 RunManifest path is missing.");
            if (!IsSha256(expectedSha256))
                throw new InvalidOperationException("Runtime V1 RunManifest SHA-256 is invalid.");

            var fullPath = Path.GetFullPath(manifestPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Runtime V1 RunManifest was not found.", fullPath);

            var actualSha256 = ComputeFileSha256(fullPath);
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Runtime V1 RunManifest SHA-256 mismatch.");

            var manifest = JsonUtility.FromJson<RuntimeV1RunnableRunManifest>(
                File.ReadAllText(fullPath, Encoding.UTF8));
            ValidateManifest(manifest);
            manifest.RunManifestSha256 = actualSha256;
            manifest.ManifestPath = fullPath;
            return manifest;
        }

        /// <summary>
        /// 计算文件完整 SHA-256，并返回 64 个 lowercase hex 字符。
        /// </summary>
        public static string ComputeFileSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha256 = SHA256.Create())
                return ToLowerHex(sha256.ComputeHash(stream));
        }

        /// <summary>
        /// 将 PlayerResult 以同目录临时文件加原子移动的方式写入，禁止覆盖旧证据。
        /// </summary>
        public static void WritePlayerResultAtomic(
            string manifestPath,
            RuntimeV1RunnablePlayerResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                throw new DirectoryNotFoundException("Runtime V1 run root does not exist.");

            var resultPath = Path.Combine(directory, PlayerResultFileName);
            if (File.Exists(resultPath))
                throw new IOException("Runtime V1 PlayerResult already exists: " + resultPath);

            var tempPath = resultPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(
                    tempPath,
                    JsonUtility.ToJson(result, true),
                    new UTF8Encoding(false));
                File.Move(tempPath, resultPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        /// <summary>
        /// 校验 RunManifest 中 Player 必须消费的身份字段。
        /// </summary>
        private static void ValidateManifest(RuntimeV1RunnableRunManifest manifest)
        {
            if (manifest == null || !string.Equals(
                    manifest.Schema,
                    ManifestSchema,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Runtime V1 RunManifest schema is invalid.");
            }

            if (string.IsNullOrWhiteSpace(manifest.RunId)
                || !IsSha256(manifest.ProducerFingerprint)
                || !IsSha256(manifest.GeneratedArtifactIdentity)
                || !IsSha256(manifest.FinalSourceFingerprint)
                || !IsSha256(manifest.BuildHash)
                || string.IsNullOrWhiteSpace(manifest.PlayerOutputPath)
                || manifest.BuildScenes == null
                || manifest.BuildScenes.Length != 1
                || !string.Equals(
                    manifest.BuildScenes[0],
                    BuildScenePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Runtime V1 RunManifest identity is incomplete.");
            }
        }

        /// <summary>
        /// 判断命令行是否包含一个独立开关参数。
        /// </summary>
        private static bool HasArgument(string[] arguments, string argument)
        {
            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], argument, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 读取 `-name value` 或 `-name=value` 形式的命令行参数。
        /// </summary>
        private static string GetArgumentValue(string[] arguments, string argument)
        {
            var prefix = argument + "=";
            for (var index = 0; index < arguments.Length; index++)
            {
                var value = arguments[index];
                if (string.Equals(value, argument, StringComparison.OrdinalIgnoreCase))
                    return index + 1 < arguments.Length ? arguments[index + 1] : string.Empty;
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return value.Substring(prefix.Length);
            }

            return string.Empty;
        }

        /// <summary>
        /// 判断字符串是否为未截断的 SHA-256 十六进制值。
        /// </summary>
        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
                return false;

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9')
                      || (character >= 'a' && character <= 'f')
                      || (character >= 'A' && character <= 'F')))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 将哈希字节编码为 lowercase hex，避免依赖高版本 BCL API。
        /// </summary>
        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (var index = 0; index < bytes.Length; index++)
                builder.Append(bytes[index].ToString("x2"));
            return builder.ToString();
        }
    }

    /// <summary>
    /// 保存 Player 只读消费的 Runtime V1 RunManifest 身份投影，完整 inventory 仍由 Editor evidence owner 持有。
    /// </summary>
    [Serializable]
    public sealed class RuntimeV1RunnableRunManifest
    {
        public string Schema;
        public string RunId;
        public string ProducerFingerprint;
        public string GeneratedArtifactIdentity;
        public string FinalSourceFingerprint;
        public string BuildHash;
        public string PlayerOutputPath;
        public string[] BuildScenes;

        [NonSerialized] public string RunManifestSha256;
        [NonSerialized] public string ManifestPath;
    }

    /// <summary>
    /// 保存 Development Player 三个最小向量和全部身份绑定，作为本轮机器可读最终结果。
    /// </summary>
    [Serializable]
    public sealed class RuntimeV1RunnablePlayerResult
    {
        public string Schema = RuntimeV1RunnableEvidence.PlayerResultSchema;
        public string RunId;
        public string ProducerFingerprint;
        public string GeneratedArtifactIdentity;
        public string FinalSourceFingerprint;
        public string BuildHash;
        public string RunManifestSha256;
        public string SupportProfileAdmission;
        public string ProductionInstallAdmission;
        public bool DeclaredFullSemanticEligibility;
        public int Scale;
        public int AscCount;
        public RuntimeV1RunnableVectorResult Ability;
        public RuntimeV1RunnableVectorResult GameplayEffect9203;
        public RuntimeV1RunnableVectorResult AutoChess;
        public string SemanticHash;
        public string StateHash;
        public bool Passed;
        public string Failure;
    }

    /// <summary>
    /// 保存一个 Runtime V1 Player 验收向量的结果与精确状态摘要。
    /// </summary>
    [Serializable]
    public sealed class RuntimeV1RunnableVectorResult
    {
        public string Name;
        public bool Passed;
        public string Summary;
        public string SemanticHash;
        public string StateHash;
    }
}
