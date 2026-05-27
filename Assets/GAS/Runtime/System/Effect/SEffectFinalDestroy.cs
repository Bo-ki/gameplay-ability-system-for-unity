using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASEffectGroup))]
    [UpdateAfter(typeof(SEffectRemove))]
    public partial struct SEffectFinalDestroy : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CEffectFinalDestroy>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var effects = _query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < effects.Length; i++)
                EffectRuntimeUtility.FinalizeEffectDestroy(em, effects[i]);
        }

        public void OnDestroy(ref SystemState state) { }
    }
}
