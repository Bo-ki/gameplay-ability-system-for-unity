using System;
using System.IO;

namespace GAS.Editor
{
    /// <summary>
    /// 为一次 SourceGenerator generation 创建空白、隔离且不可直接晋升的候选工作区。
    /// </summary>
    public sealed class GasCodeGenCandidateWorkspace : IDisposable
    {
        private const string CandidateParentRelativePath = "Temp/GasCodeGenCandidates";
        private const string CorePublishedRelativePath = "Assets/GAS/Generated/CodeGen";
        private const string AutoChessPublishedRelativePath = "Assets/AutoChessDemo/Generated";
        private const string CoreCandidateDirectoryName = "Core";
        private const string AutoChessCandidateDirectoryName = "AutoChess";
        private const string ControlDirectoryName = "Control";
        private bool _cleanedUp;
        private bool _authorityIssued;

        /// <summary>
        /// 保存已通过项目边界校验的路径；候选根始终从空目录开始，禁止复制 active source。
        /// </summary>
        private GasCodeGenCandidateWorkspace(
            string projectRoot,
            string workspaceRoot,
            string coreCandidateRoot,
            string autoChessCandidateRoot,
            string controlRoot)
        {
            ProjectRoot = projectRoot;
            WorkspaceRoot = workspaceRoot;
            CoreCandidateRoot = coreCandidateRoot;
            AutoChessCandidateRoot = autoChessCandidateRoot;
            ControlRoot = controlRoot;
            CorePublishedRoot = Normalize(Path.Combine(projectRoot, CorePublishedRelativePath));
            AutoChessPublishedRoot = Normalize(Path.Combine(projectRoot, AutoChessPublishedRelativePath));
            WorkspaceNonce = Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 返回当前 workspace 所属项目的规范化绝对根路径。
        /// </summary>
        public string ProjectRoot { get; }

        /// <summary>
        /// 返回本次候选 workspace 的规范化绝对路径。
        /// </summary>
        public string WorkspaceRoot { get; }

        /// <summary>
        /// 返回 Core emitter 的隔离输出根。
        /// </summary>
        public string CoreCandidateRoot { get; }

        /// <summary>
        /// 返回 AutoChess emitter 的隔离输出根。
        /// </summary>
        public string AutoChessCandidateRoot { get; }

        /// <summary>
        /// 返回 descriptor、envelope、selector candidate 与 compile plan 的隔离控制目录。
        /// </summary>
        public string ControlRoot { get; }

        /// <summary>
        /// 返回 Core artifact 在 manifest 中使用的稳定发布根；该路径不会被 workspace 写入。
        /// </summary>
        public string CorePublishedRoot { get; }

        /// <summary>
        /// 返回 AutoChess artifact 在 manifest 中使用的稳定发布根；该路径不会被 workspace 写入。
        /// </summary>
        public string AutoChessPublishedRoot { get; }

        /// <summary>
        /// 返回兼容现有调用命名的 Core 稳定发布根，但不提供任何目录 promotion 能力。
        /// </summary>
        public string CoreActiveRoot => CorePublishedRoot;

        /// <summary>
        /// 返回兼容现有调用命名的 AutoChess 稳定发布根，但不提供任何目录 promotion 能力。
        /// </summary>
        public string AutoChessActiveRoot => AutoChessPublishedRoot;

        internal string WorkspaceNonce { get; }

        /// <summary>
        /// 在项目 Temp 下创建唯一空白 workspace，避免 legacy active `.gen.cs` 成为候选输入旁路。
        /// </summary>
        public static GasCodeGenCandidateWorkspace Create(string projectRoot)
        {
            var normalizedProjectRoot = Normalize(projectRoot);
            EnsureProjectRoot(normalizedProjectRoot);
            var candidateParent = Normalize(Path.Combine(
                normalizedProjectRoot,
                CandidateParentRelativePath));
            EnsureUnderRoot(candidateParent, normalizedProjectRoot, nameof(candidateParent));
            EnsureExistingPathSegmentsHaveNoReparsePoints(normalizedProjectRoot, candidateParent);
            Directory.CreateDirectory(candidateParent);
            EnsureExistingPathSegmentsHaveNoReparsePoints(normalizedProjectRoot, candidateParent);

            var workspaceRoot = CreateUniqueWorkspaceRoot(candidateParent);
            var workspace = new GasCodeGenCandidateWorkspace(
                normalizedProjectRoot,
                workspaceRoot,
                Normalize(Path.Combine(workspaceRoot, CoreCandidateDirectoryName)),
                Normalize(Path.Combine(workspaceRoot, AutoChessCandidateDirectoryName)),
                Normalize(Path.Combine(workspaceRoot, ControlDirectoryName)));
            try
            {
                workspace.InitializeEmptyRoots();
                return workspace;
            }
            catch
            {
                workspace.DeleteWorkspaceBestEffort();
                throw;
            }
        }

        /// <summary>
        /// 在候选 bytes 全部生成后签发一次性进程内 authority，阻止公开路径字符串获得 Stage 权限。
        /// </summary>
        internal CandidateAuthority SealAuthority()
        {
            EnsureUsable();
            if (_authorityIssued)
                throw new InvalidOperationException("Candidate workspace authority has already been issued.");

            EnsureDirectoryTreeHasNoReparsePoints(WorkspaceRoot);
            _authorityIssued = true;
            return new CandidateAuthority(this, WorkspaceNonce);
        }

        /// <summary>
        /// 验证 authority 仍属于当前存活 workspace 且未跨进程或跨 workspace 复用。
        /// </summary>
        internal void EnsureAuthority(CandidateAuthority authority)
        {
            EnsureUsable();
            if (authority == null
                || !ReferenceEquals(authority.Owner, this)
                || !string.Equals(authority.Nonce, WorkspaceNonce, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Candidate workspace authority is invalid.");
            }
        }

        /// <summary>
        /// 清理本次 workspace；只删除协议固定根下且不含 reparse point 的精确目录。
        /// </summary>
        public bool Cleanup()
        {
            if (_cleanedUp)
                return true;
            if (!Directory.Exists(WorkspaceRoot))
            {
                _cleanedUp = true;
                return true;
            }

            try
            {
                var candidateParent = Normalize(Path.Combine(ProjectRoot, CandidateParentRelativePath));
                EnsureUnderRoot(WorkspaceRoot, candidateParent, nameof(WorkspaceRoot));
                if (string.Equals(WorkspaceRoot, candidateParent, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Workspace cleanup cannot target the candidate parent.");
                EnsureDirectoryTreeHasNoReparsePoints(WorkspaceRoot);
                Directory.Delete(WorkspaceRoot, recursive: true);
                _cleanedUp = true;
                return true;
            }
            catch (Exception cleanupFailure)
            {
                GasCodeGenEnvironment.LogError(
                    "[GasCodeGenCandidateWorkspace] CleanupDebt path=" + WorkspaceRoot
                    + ", error=" + cleanupFailure.Message);
                return false;
            }
        }

        /// <summary>
        /// 按显式 Cleanup 的安全边界释放候选 workspace。
        /// </summary>
        public void Dispose()
        {
            Cleanup();
        }

        /// <summary>
        /// 创建三个空白候选目录，任何 active 文件都不会被隐式复制进来。
        /// </summary>
        private void InitializeEmptyRoots()
        {
            EnsureUnderRoot(CoreCandidateRoot, WorkspaceRoot, nameof(CoreCandidateRoot));
            EnsureUnderRoot(AutoChessCandidateRoot, WorkspaceRoot, nameof(AutoChessCandidateRoot));
            EnsureUnderRoot(ControlRoot, WorkspaceRoot, nameof(ControlRoot));
            Directory.CreateDirectory(CoreCandidateRoot);
            Directory.CreateDirectory(AutoChessCandidateRoot);
            Directory.CreateDirectory(ControlRoot);
        }

        /// <summary>
        /// 在候选父目录内建立不可复用的 workspace root。
        /// </summary>
        private static string CreateUniqueWorkspaceRoot(string candidateParent)
        {
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var path = Normalize(Path.Combine(candidateParent, "candidate-" + Guid.NewGuid().ToString("N")));
                try
                {
                    Directory.CreateDirectory(path);
                    return path;
                }
                catch (IOException)
                {
                    // GUID 碰撞只重试当前协议拥有的精确候选名。
                }
            }

            throw new IOException("Unable to create a unique GasCodeGen candidate workspace.");
        }

        /// <summary>
        /// 在初始化失败时仅尝试清理当前精确 workspace，不覆盖原始异常。
        /// </summary>
        private void DeleteWorkspaceBestEffort()
        {
            try
            {
                if (Directory.Exists(WorkspaceRoot))
                    Directory.Delete(WorkspaceRoot, recursive: true);
            }
            catch
            {
                // 初始化异常仍是主错误；残留由 Temp scavenger 显式报告。
            }
        }

        /// <summary>
        /// 拒绝已清理 workspace 被继续用于 descriptor、gate 或 Store。
        /// </summary>
        private void EnsureUsable()
        {
            if (_cleanedUp || !Directory.Exists(WorkspaceRoot))
                throw new ObjectDisposedException(nameof(GasCodeGenCandidateWorkspace));
        }

        /// <summary>
        /// 校验项目根存在且不是文件系统根。
        /// </summary>
        private static void EnsureProjectRoot(string projectRoot)
        {
            if (!Directory.Exists(projectRoot))
                throw new DirectoryNotFoundException("Project root does not exist: " + projectRoot);
            if (string.Equals(projectRoot, Path.GetPathRoot(projectRoot), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Project root cannot be a file-system root.");
        }

        /// <summary>
        /// 检查从可信根到目标现有层级的每个目录，阻止 junction/symlink 越界。
        /// </summary>
        private static void EnsureExistingPathSegmentsHaveNoReparsePoints(string trustedRoot, string targetPath)
        {
            EnsureUnderRoot(targetPath, trustedRoot, nameof(targetPath));
            var current = targetPath;
            while (!string.Equals(current, trustedRoot, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(current))
                    EnsureNotReparsePoint(current);
                current = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(current))
                    throw new InvalidOperationException("Path traversal escaped the trusted root.");
            }

            EnsureNotReparsePoint(trustedRoot);
        }

        /// <summary>
        /// 递归拒绝 workspace 内任意 reparse point，确保清理目标不会跳出边界。
        /// </summary>
        private static void EnsureDirectoryTreeHasNoReparsePoints(string directoryRoot)
        {
            EnsureNotReparsePoint(directoryRoot);
            var entries = Directory.GetFileSystemEntries(directoryRoot, "*", SearchOption.AllDirectories);
            for (var index = 0; index < entries.Length; index++)
                EnsureNotReparsePoint(entries[index]);
        }

        /// <summary>
        /// 拒绝单个文件系统对象携带 reparse-point 属性。
        /// </summary>
        private static void EnsureNotReparsePoint(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Reparse point is not allowed: " + path);
        }

        /// <summary>
        /// 要求目标位于可信根边界内。
        /// </summary>
        private static void EnsureUnderRoot(string path, string root, string parameterName)
        {
            if (!IsUnderRoot(path, root))
                throw new ArgumentOutOfRangeException(parameterName, "Path escapes trusted root: " + path);
        }

        /// <summary>
        /// 判断规范化路径是否位于给定根本身或真实子目录中。
        /// </summary>
        private static bool IsUnderRoot(string path, string root)
        {
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将路径转换为无多余尾分隔符的绝对路径。
        /// </summary>
        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", nameof(path));
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// 携带 workspace 对象身份和进程内 nonce，作为 descriptor/store 唯一可接受的候选权限。
        /// </summary>
        internal sealed class CandidateAuthority
        {
            /// <summary>
            /// 仅允许所属 workspace 创建 authority。
            /// </summary>
            internal CandidateAuthority(GasCodeGenCandidateWorkspace owner, string nonce)
            {
                Owner = owner;
                Nonce = nonce;
            }

            internal GasCodeGenCandidateWorkspace Owner { get; }
            internal string Nonce { get; }
        }
    }
}
