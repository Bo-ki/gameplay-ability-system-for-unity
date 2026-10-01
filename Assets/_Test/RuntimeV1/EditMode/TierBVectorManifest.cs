using System;
using System.IO;
using UnityEngine;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 表示 Runtime v1 Tier B 执行子清单的根文档，只承载治理数据，不判断语义是否通过。
    /// </summary>
    [Serializable]
    internal sealed class TierBVectorManifestDocument
    {
        public int SchemaVersion;
        public string ManifestId;
        public int ManifestVersion;
        public string ManifestScope;
        public string SemanticOwner;
        public bool CanAuthorizeV0Exit;
        public string[] RequiredMasterScopes;
        public TierBVectorManifestEntry[] Vectors;
    }

    /// <summary>
    /// 表示一个 Tier B 向量的 owner、治理状态与真实测试证据引用。
    /// </summary>
    [Serializable]
    internal sealed class TierBVectorManifestEntry
    {
        public string VectorId;
        public int Version;
        public string Description;
        public string OwnerTask;
        public string NextOwnerTask;
        public bool RequiredPass;
        public string Status;
        public string StatusReason;
        public bool ReviewRequired;
        public string LastRunEvidenceId;
        public TierBVectorApproval Approval;
        public TierBVectorTestEvidence[] Tests;
    }

    /// <summary>
    /// 保存 ApprovedRed 所需的显式批准人与可追溯记录，空值不代表已经批准。
    /// </summary>
    [Serializable]
    internal sealed class TierBVectorApproval
    {
        public string ApprovedBy;
        public string ApprovedAtUtc;
        public string ApprovalRecordId;
        public string ApprovalRecordHash;
        public string ExpectedFailureSignature;
    }

    /// <summary>
    /// 描述一个必须由 Unity Test Runner 真实发现的测试方法及其源码指纹。
    /// </summary>
    [Serializable]
    internal sealed class TierBVectorTestEvidence
    {
        public string TestAssembly;
        public string TestPlatform;
        public string TestSourcePath;
        public string TestSourceHash;
        public string TestId;
        public int ExpectedCaseCount;
        public string ExpectedResult;
    }

    /// <summary>
    /// 从 Tools/Tests 下唯一 Tier B 执行子清单读取治理数据，避免 C# 与 runner 各维护一份状态。
    /// </summary>
    internal static class TierBVectorManifest
    {
        internal const int CurrentSchemaVersion = 2;
        internal const string RelativeManifestPath = "Tools/Tests/RuntimeV1TierBManifest.json";

        /// <summary>
        /// 读取并反序列化 Tier B 执行子清单；文件缺失或 JSON 无效时显式失败。
        /// </summary>
        internal static TierBVectorManifestDocument Load()
        {
            var manifestPath = GetManifestPath();
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Runtime v1 Tier B manifest 不存在。", manifestPath);

            var document = JsonUtility.FromJson<TierBVectorManifestDocument>(
                File.ReadAllText(manifestPath));
            if (document == null)
                throw new InvalidDataException("Runtime v1 Tier B manifest JSON 无法解析。");

            return document;
        }

        /// <summary>
        /// 返回机器清单的绝对路径，供契约测试定位同一数据源。
        /// </summary>
        internal static string GetManifestPath()
        {
            var projectPath = Directory.GetParent(Application.dataPath);
            if (projectPath == null)
                throw new InvalidOperationException("无法从 Application.dataPath 定位 Unity 项目根目录。");

            return Path.GetFullPath(Path.Combine(projectPath.FullName, RelativeManifestPath));
        }

        /// <summary>
        /// 将仓库相对路径解析为本机绝对路径，禁止证据引用逃逸项目目录。
        /// </summary>
        internal static string ResolveProjectPath(string relativePath)
        {
            var projectPath = Directory.GetParent(Application.dataPath);
            if (projectPath == null)
                throw new InvalidOperationException("无法从 Application.dataPath 定位 Unity 项目根目录。");

            var projectRoot = Path.GetFullPath(projectPath.FullName)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var resolvedPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
            var projectPrefix = projectRoot + Path.DirectorySeparatorChar;
            if (!resolvedPath.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Tier B test source 路径逃逸 Unity 项目：" + relativePath);

            return resolvedPath;
        }
    }
}
