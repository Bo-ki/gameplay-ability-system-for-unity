using System;
using System.Collections.Generic;

namespace GAS.Editor.CodeGen.Semantics
{
    /// <summary>
    /// 保存一条仅供静态环证明使用的 dependency 边，不暴露可注入的生产 DTO。
    /// </summary>
    internal sealed class CanonicalDependencyCycleArc
    {
        /// <summary>
        /// 创建含 source/target、kind/sign、work/cleanup 与硬上界的环证明边。
        /// </summary>
        public CanonicalDependencyCycleArc(
            CanonicalDefinitionKey source,
            CanonicalSemanticDependency dependency)
            : this(
                source,
                dependency.TargetDefinition,
                dependency.DependencyKind,
                dependency.Sign,
                dependency.WorkKind,
                dependency.CleanupPolicy,
                dependency.MaxExpansion,
                dependency.Provenance)
        {
        }

        /// <summary>
        /// 从 Typed Red 原型或定向测试创建同一封闭环证明边。
        /// </summary>
        public CanonicalDependencyCycleArc(
            CanonicalDefinitionKey source,
            CanonicalDefinitionKey target,
            CanonicalDependencyKind dependencyKind,
            CanonicalDependencySign sign,
            CanonicalDependencyWorkKind workKind,
            CanonicalCleanupPolicy cleanupPolicy,
            int maxExpansion,
            CanonicalSemanticProvenance provenance)
        {
            Source = source;
            Target = target;
            DependencyKind = dependencyKind;
            Sign = sign;
            WorkKind = workKind;
            CleanupPolicy = cleanupPolicy;
            MaxExpansion = maxExpansion;
            Provenance = provenance;
        }

        public CanonicalDefinitionKey Source { get; }

        public CanonicalDefinitionKey Target { get; }

        public CanonicalDependencyKind DependencyKind { get; }

        public CanonicalDependencySign Sign { get; }

        public CanonicalDependencyWorkKind WorkKind { get; }

        public CanonicalCleanupPolicy CleanupPolicy { get; }

        public int MaxExpansion { get; }

        public CanonicalSemanticProvenance Provenance { get; }
    }

    /// <summary>
    /// 以确定性索引和迭代 Kosaraju SCC 证明负向或受上界约束的 work/cleanup 边不在环中。
    /// </summary>
    internal static class CanonicalSemanticDependencyCycleValidator
    {
        /// <summary>
        /// 拒绝包含 proof-relevant 边的环；仅接受调用方刚构建且不再共享的封闭数组。
        /// </summary>
        public static void Validate(CanonicalDependencyCycleArc[] arcs)
        {
            if (arcs == null)
                FailBudget("Dependency cycle arcs cannot be null.");
            ValidateArcBudget(arcs);
            var graph = BuildIndexedGraph(arcs);
            var componentIds = ComputeComponents(graph, out var componentSizes);
            for (var index = 0; index < arcs.Length; index++)
            {
                var arc = arcs[index];
                if (!RequiresCycleProof(arc))
                    continue;
                if (arc.MaxExpansion <= 0
                    || IsCyclic(graph.Endpoints[index], componentIds, componentSizes))
                    Fail(arc.Provenance);
            }
        }

        /// <summary>
        /// 在任何 SCC 数组分配前限制 edge、key parts 与唯一节点总量的上界。
        /// </summary>
        private static void ValidateArcBudget(CanonicalDependencyCycleArc[] arcs)
        {
            var meter = new CanonicalSemanticBudgetMeter();
            for (var index = 0; index < arcs.Length; index++)
            {
                var arc = arcs[index];
                if (arc == null || arc.Source == null || arc.Target == null)
                    FailBudget("Dependency cycle arc endpoints cannot be null.");
                meter.ChargeEdge("dependency cycle arc");
                CanonicalSemanticResourceBudget.ChargeKey(meter, arc.Source);
                CanonicalSemanticResourceBudget.ChargeKey(meter, arc.Target);
            }
        }

        /// <summary>
        /// 按 dependency kind/sign 与有界 cleanup 元数据判定该边是否参与环证明。
        /// </summary>
        private static bool RequiresCycleProof(CanonicalDependencyCycleArc arc)
        {
            if (arc.DependencyKind == CanonicalDependencyKind.Negative
                || arc.Sign == CanonicalDependencySign.Negative)
                return true;
            if (arc.DependencyKind == CanonicalDependencyKind.Program
                || arc.DependencyKind == CanonicalDependencyKind.Cleanup
                || arc.WorkKind == CanonicalDependencyWorkKind.Cleanup)
                return true;
            return arc.CleanupPolicy != CanonicalCleanupPolicy.None
                   && arc.CleanupPolicy != CanonicalCleanupPolicy.Unsupported;
        }

        /// <summary>
        /// 将 endpoint 按 DefinitionKey 排序建索引，再以精确 degree 分配正反邻接表。
        /// </summary>
        private static IndexedDependencyGraph BuildIndexedGraph(
            CanonicalDependencyCycleArc[] arcs)
        {
            var keys = new CanonicalDefinitionKey[checked(arcs.Length * 2)];
            for (var index = 0; index < arcs.Length; index++)
            {
                keys[index * 2] = arcs[index].Source;
                keys[(index * 2) + 1] = arcs[index].Target;
            }
            Array.Sort(keys, CanonicalDefinitionKeyComparer.Instance);
            var nodeCount = DeduplicateKeys(keys);
            if (nodeCount > CanonicalSemanticResourceBudget.MaxTotalNodes)
                FailBudget("Dependency cycle nodes exceed resource budget.");
            var endpoints = IndexEndpoints(arcs, keys, nodeCount);
            return BuildAdjacency(endpoints, nodeCount);
        }

        /// <summary>
        /// 原地压缩已排序 key 数组并返回唯一节点数量。
        /// </summary>
        private static int DeduplicateKeys(CanonicalDefinitionKey[] keys)
        {
            var count = 0;
            for (var index = 0; index < keys.Length; index++)
            {
                if (count > 0 && keys[count - 1].CompareTo(keys[index]) == 0)
                    continue;
                keys[count++] = keys[index];
            }
            return count;
        }

        /// <summary>
        /// 以二分查找把每条 key endpoint 映射为稳定整数索引。
        /// </summary>
        private static ArcEndpoints[] IndexEndpoints(
            CanonicalDependencyCycleArc[] arcs,
            CanonicalDefinitionKey[] keys,
            int nodeCount)
        {
            var result = new ArcEndpoints[arcs.Length];
            for (var index = 0; index < arcs.Length; index++)
            {
                result[index] = new ArcEndpoints(
                    FindKeyIndex(keys, nodeCount, arcs[index].Source),
                    FindKeyIndex(keys, nodeCount, arcs[index].Target));
            }
            return result;
        }

        /// <summary>
        /// 在唯一 key 前缀中执行无哈希碰撞退化的确定性二分查找。
        /// </summary>
        private static int FindKeyIndex(
            CanonicalDefinitionKey[] keys,
            int count,
            CanonicalDefinitionKey target)
        {
            var low = 0;
            var high = count - 1;
            while (low <= high)
            {
                var middle = low + ((high - low) / 2);
                var comparison = keys[middle].CompareTo(target);
                if (comparison == 0)
                    return middle;
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            FailBudget("Dependency cycle endpoint index is missing.");
            return -1;
        }

        /// <summary>
        /// 按 endpoint degree 一次分配并填充正反邻接数组。
        /// </summary>
        private static IndexedDependencyGraph BuildAdjacency(
            ArcEndpoints[] endpoints,
            int nodeCount)
        {
            var forwardDegrees = new int[nodeCount];
            var reverseDegrees = new int[nodeCount];
            for (var index = 0; index < endpoints.Length; index++)
            {
                forwardDegrees[endpoints[index].Source]++;
                reverseDegrees[endpoints[index].Target]++;
            }
            var forward = AllocateRows(forwardDegrees);
            var reverse = AllocateRows(reverseDegrees);
            Array.Clear(forwardDegrees, 0, forwardDegrees.Length);
            Array.Clear(reverseDegrees, 0, reverseDegrees.Length);
            for (var index = 0; index < endpoints.Length; index++)
            {
                var endpoint = endpoints[index];
                forward[endpoint.Source][forwardDegrees[endpoint.Source]++] = endpoint.Target;
                reverse[endpoint.Target][reverseDegrees[endpoint.Target]++] = endpoint.Source;
            }
            return new IndexedDependencyGraph(endpoints, forward, reverse);
        }

        /// <summary>
        /// 以每个节点的精确 degree 分配邻接行，避免 List 扩容峰值。
        /// </summary>
        private static int[][] AllocateRows(int[] degrees)
        {
            var result = new int[degrees.Length][];
            for (var index = 0; index < degrees.Length; index++)
                result[index] = new int[degrees[index]];
            return result;
        }

        /// <summary>
        /// 两次迭代 DFS 计算全部 SCC，并返回每个 component 的节点数量。
        /// </summary>
        private static int[] ComputeComponents(
            IndexedDependencyGraph graph,
            out int[] componentSizes)
        {
            var nodeCount = graph.Forward.Length;
            var finishOrder = ComputeFinishOrder(graph.Forward);
            var componentIds = new int[nodeCount];
            for (var index = 0; index < componentIds.Length; index++)
                componentIds[index] = -1;
            var sizes = new int[nodeCount];
            var stack = new int[nodeCount];
            var componentCount = 0;
            for (var index = finishOrder.Length - 1; index >= 0; index--)
            {
                var start = finishOrder[index];
                if (componentIds[start] >= 0)
                    continue;
                sizes[componentCount] = AssignComponent(
                    start, componentCount, graph.Reverse, componentIds, stack);
                componentCount++;
            }
            componentSizes = sizes;
            return componentIds;
        }

        /// <summary>
        /// 以显式 node/edge 栈计算第一遍 DFS 的完成顺序。
        /// </summary>
        private static int[] ComputeFinishOrder(int[][] adjacency)
        {
            var visited = new bool[adjacency.Length];
            var finishOrder = new int[adjacency.Length];
            var stackNodes = new int[adjacency.Length];
            var stackEdges = new int[adjacency.Length];
            var finishCount = 0;
            for (var start = 0; start < adjacency.Length; start++)
            {
                if (visited[start])
                    continue;
                finishCount = TraverseForFinish(
                    start, adjacency, visited, stackNodes, stackEdges, finishOrder, finishCount);
            }
            return finishOrder;
        }

        /// <summary>
        /// 完成一个 DFS forest root，并把节点按退出顺序写入固定数组。
        /// </summary>
        private static int TraverseForFinish(
            int start,
            int[][] adjacency,
            bool[] visited,
            int[] stackNodes,
            int[] stackEdges,
            int[] finishOrder,
            int finishCount)
        {
            var depth = 1;
            stackNodes[0] = start;
            stackEdges[0] = 0;
            visited[start] = true;
            while (depth > 0)
            {
                var frame = depth - 1;
                var node = stackNodes[frame];
                if (stackEdges[frame] < adjacency[node].Length)
                {
                    var next = adjacency[node][stackEdges[frame]++];
                    if (!visited[next])
                    {
                        visited[next] = true;
                        stackNodes[depth] = next;
                        stackEdges[depth++] = 0;
                    }
                    continue;
                }
                finishOrder[finishCount++] = node;
                depth--;
            }
            return finishCount;
        }

        /// <summary>
        /// 在反图上以显式栈标记一个 SCC 并返回其节点数量。
        /// </summary>
        private static int AssignComponent(
            int start,
            int componentId,
            int[][] reverse,
            int[] componentIds,
            int[] stack)
        {
            var pending = 1;
            var size = 0;
            stack[0] = start;
            componentIds[start] = componentId;
            while (pending > 0)
            {
                var node = stack[--pending];
                size++;
                for (var index = 0; index < reverse[node].Length; index++)
                {
                    var next = reverse[node][index];
                    if (componentIds[next] >= 0)
                        continue;
                    componentIds[next] = componentId;
                    stack[pending++] = next;
                }
            }
            return size;
        }

        /// <summary>
        /// 判断 endpoint 是否为 self-loop 或位于包含多个节点的同一 SCC。
        /// </summary>
        private static bool IsCyclic(
            ArcEndpoints endpoint,
            int[] componentIds,
            int[] componentSizes)
        {
            var component = componentIds[endpoint.Source];
            return component == componentIds[endpoint.Target]
                   && (endpoint.Source == endpoint.Target || componentSizes[component] > 1);
        }

        /// <summary>
        /// 以稳定 CFG1301 拒绝负向或 cleanup/work 有界证明环。
        /// </summary>
        private static void Fail(CanonicalSemanticProvenance provenance)
        {
            throw new CanonicalSemanticValidationException(
                CanonicalSemanticRuleCatalog.DependencyOrCleanupUnbounded,
                "Proof-relevant dependency cycle is forbidden.",
                provenance);
        }

        /// <summary>
        /// 以稳定 CFG1501 拒绝 SCC 预检预算或结构错误。
        /// </summary>
        private static void FailBudget(string message)
        {
            throw new CanonicalSemanticValidationException(
                CanonicalSemanticRuleCatalog.NonCanonicalIdentity,
                CanonicalSemanticVersions.ResourceBudgetId + ": " + message);
        }

        /// <summary>
        /// 保存一条 arc 的整数化 source/target endpoint。
        /// </summary>
        private readonly struct ArcEndpoints
        {
            /// <summary>
            /// 创建无需哈希查找的整数 endpoint。
            /// </summary>
            public ArcEndpoints(int source, int target)
            {
                Source = source;
                Target = target;
            }

            public int Source { get; }

            public int Target { get; }
        }

        /// <summary>
        /// 保存 SCC 使用的固定 endpoint 与正反邻接数组。
        /// </summary>
        private sealed class IndexedDependencyGraph
        {
            /// <summary>
            /// 创建一次性固定容量 SCC 图。
            /// </summary>
            public IndexedDependencyGraph(
                ArcEndpoints[] endpoints,
                int[][] forward,
                int[][] reverse)
            {
                Endpoints = endpoints;
                Forward = forward;
                Reverse = reverse;
            }

            public ArcEndpoints[] Endpoints { get; }

            public int[][] Forward { get; }

            public int[][] Reverse { get; }
        }

        /// <summary>
        /// 复用 CanonicalDefinitionKey 的稳定全字段顺序，避免 hash collision CPU 退化。
        /// </summary>
        private sealed class CanonicalDefinitionKeyComparer : IComparer<CanonicalDefinitionKey>
        {
            public static readonly CanonicalDefinitionKeyComparer Instance =
                new CanonicalDefinitionKeyComparer();

            /// <summary>
            /// 比较两个非空 canonical key。
            /// </summary>
            public int Compare(CanonicalDefinitionKey left, CanonicalDefinitionKey right)
            {
                return left.CompareTo(right);
            }
        }
    }
}
