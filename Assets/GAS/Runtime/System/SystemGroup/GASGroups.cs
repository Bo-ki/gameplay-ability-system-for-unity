using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Frame preparation domain. Clears previous-frame transient streams, advances frame state,
    /// and resets frame-local GAS buffers before command resolution begins.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(GASCommandResolveSystemGroup))]
    public partial class GASFramePrepareSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// Boundary command and target resolution domain. External requests are normalized into
    /// owner-local command buffers here; structural work is only recorded for StructuralCommit playback.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GASFramePrepareSystemGroup))]
    [UpdateBefore(typeof(GASCoreSimulationSystemGroup))]
    public partial class GASCommandResolveSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// Core gameplay simulation domain. Effect fan-in, active effect state, attribute reduce,
    /// tag propagation, ability state, and gameplay fact lanes run here without direct structural playback.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GASCommandResolveSystemGroup))]
    [UpdateBefore(typeof(GASStructuralCommitSystemGroup))]
    public partial class GASCoreSimulationSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// ExecutionCalculation extension slot. Project-specific ECS systems may write
    /// GEExecutionCalculationValueBuffer here before the generated output consumer runs.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEExecutionCalculationSystem))]
    [UpdateBefore(typeof(GEExecutionCalculationOutputModifierSystem))]
    public partial class GEExecutionCalculationExtensionSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// Sole GAS hot-path structural commit domain. Systems record structural work before this group;
    /// ECB playback is constrained between CoreSimulation and BoundaryProjection.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GASCoreSimulationSystemGroup))]
    [UpdateBefore(typeof(GASBoundaryProjectionSystemGroup))]
    public partial class GASStructuralCommitSystemGroup : ComponentSystemGroup
    {
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASStructuralCommitSystemGroup), OrderFirst = true)]
    public partial class BeginGASStructuralCommitECBSystem : EntityCommandBufferSystem
    {
        public unsafe struct Singleton : IComponentData, IECBSingleton
        {
            private UnsafeList<EntityCommandBuffer>* _pendingBuffers;
            private AllocatorManager.AllocatorHandle _allocator;

            public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
            {
                return EntityCommandBufferSystem.CreateCommandBuffer(ref *_pendingBuffers, _allocator, world);
            }

            public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
            {
                _pendingBuffers = (UnsafeList<EntityCommandBuffer>*)UnsafeUtility.AddressOf(ref buffers);
            }

            public void SetAllocator(Allocator allocatorIn)
            {
                _allocator = allocatorIn;
            }

            public void SetAllocator(AllocatorManager.AllocatorHandle allocatorIn)
            {
                _allocator = allocatorIn;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASStructuralCommitSystemGroup), OrderLast = true)]
    public partial class EndGASStructuralCommitECBSystem : EntityCommandBufferSystem
    {
        public unsafe struct Singleton : IComponentData, IECBSingleton
        {
            private UnsafeList<EntityCommandBuffer>* _pendingBuffers;
            private AllocatorManager.AllocatorHandle _allocator;

            public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
            {
                return EntityCommandBufferSystem.CreateCommandBuffer(ref *_pendingBuffers, _allocator, world);
            }

            public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
            {
                _pendingBuffers = (UnsafeList<EntityCommandBuffer>*)UnsafeUtility.AddressOf(ref buffers);
            }

            public void SetAllocator(Allocator allocatorIn)
            {
                _allocator = allocatorIn;
            }

            public void SetAllocator(AllocatorManager.AllocatorHandle allocatorIn)
            {
                _allocator = allocatorIn;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
        }
    }

    /// <summary>
    /// Boundary projection domain. Presentation, replay, debugger, and managed cue bridges consume
    /// committed simulation facts and must not feed state back into CoreSimulation.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GASStructuralCommitSystemGroup))]
    public partial class GASBoundaryProjectionSystemGroup : ComponentSystemGroup
    {
    }
}
