using Unity.Burst;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Tag 查询工具方法。TagMaskComponent 内部存储 DenseIndex，外部 API 传入 TagCode。
    /// </summary>
    [BurstCompile]
    public static class GameplayTagQuery
    {
        public static bool HasTag(in TagMaskComponent mask, int tagCode)
        {
            return TagHelper.TryGetDenseIndex(tagCode, out var denseIndex) && mask.HasTag(denseIndex);
        }

        /// <summary>
        /// 非 Burst 版本，接受托管数组。用于编辑器或非 ECS 上下文。
        /// </summary>
        public static bool HasAnyTag(in TagMaskComponent mask, int[] tags)
        {
            foreach (var tag in tags)
                if (HasTag(mask, tag))
                    return true;
            return false;
        }

        public static bool HasAllTags(in TagMaskComponent mask, int[] tags)
        {
            foreach (var tag in tags)
                if (!HasTag(mask, tag))
                    return false;
            return true;
        }

        [BurstCompile]
        public static bool HasAnyTagInMask(in TagMaskComponent maskA, in TagMaskComponent maskB)
        {
            return maskA.HasAnyTag(maskB);
        }

        [BurstCompile]
        public static bool HasAllTagsInMask(in TagMaskComponent maskA, in TagMaskComponent maskB)
        {
            return maskA.HasAllTags(maskB);
        }
    }

    /// <summary>
    /// Tag 变更处理 System。在 GASCoreSimulationSystemGroup 中运行。
    /// </summary>
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    public partial struct GameplayTagChangeProcessSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TagMaskComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
