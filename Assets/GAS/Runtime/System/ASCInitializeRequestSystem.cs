using Unity.Entities;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateAfter(typeof(ASCEntityCreateSystem))]
    [UpdateBefore(typeof(ASCCommandRequestSystem))]
    public partial struct ASCInitializeRequestSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<ASCInitializeRequestComponent>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_query.CalculateEntityCount() <= 0)
                return;

            var em = state.EntityManager;
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (requestRef, requestEntity) in SystemAPI
                         .Query<RefRO<ASCInitializeRequestComponent>>()
                         .WithEntityAccess())
            {
                var request = requestRef.ValueRO;
                if (request.ASC != Entity.Null
                    && em.Exists(request.ASC)
                    && !ASCEntityFactory.IsDestroying(em, request.ASC))
                {
                    CreateAscCommand(em, ecb, request.ASC, new ASCCommandRequestComponent
                    {
                        CommandType = ASCCommandType.SetLevel,
                        Level = request.Level,
                    });

                    CreateFixedTagCommands(em, ecb, requestEntity, request.ASC);
                    CreateAttributeCommands(em, ecb, requestEntity, request.ASC);
                    CreateAbilityGrantCommands(em, ecb, requestEntity, request.ASC);
                }

                ecb.DestroyEntity(requestEntity);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void CreateFixedTagCommands(EntityManager em, EntityCommandBuffer ecb, Entity requestEntity, Entity asc)
        {
            if (!em.HasBuffer<ASCInitializeFixedTagBuffer>(requestEntity))
                return;

            var tags = em.GetBuffer<ASCInitializeFixedTagBuffer>(requestEntity);
            for (var i = 0; i < tags.Length; i++)
            {
                CreateAscCommand(em, ecb, asc, new ASCCommandRequestComponent
                {
                    CommandType = ASCCommandType.AddFixedTag,
                    TagCode = tags[i].TagCode,
                });
            }
        }

        private static void CreateAttributeCommands(EntityManager em, EntityCommandBuffer ecb, Entity requestEntity, Entity asc)
        {
            if (!em.HasBuffer<ASCInitializeAttributeBuffer>(requestEntity))
                return;

            var attributes = em.GetBuffer<ASCInitializeAttributeBuffer>(requestEntity);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                CreateAscCommand(em, ecb, asc, new ASCCommandRequestComponent
                {
                    CommandType = ASCCommandType.AddAttribute,
                    AttrSetCode = attribute.AttrSetCode,
                    AttributeCode = attribute.AttributeCode,
                    AttributeValue = attribute.BaseValue,
                    IsClampMin = attribute.IsClampMin,
                    IsClampMax = attribute.IsClampMax,
                    MinValue = attribute.MinValue,
                    MaxValue = attribute.MaxValue,
                });
            }
        }

        private static void CreateAbilityGrantCommands(EntityManager em, EntityCommandBuffer ecb, Entity requestEntity, Entity asc)
        {
            if (!em.HasBuffer<ASCInitializeAbilityBuffer>(requestEntity))
                return;

            var abilities = em.GetBuffer<ASCInitializeAbilityBuffer>(requestEntity);
            for (var i = 0; i < abilities.Length; i++)
            {
                var abilityCode = abilities[i].AbilityCode;
                if (abilityCode <= 0)
                    continue;

                var command = ecb.CreateEntity(GASRuntimeEntityArchetypes.AbilityCommandRequest(em));
                ecb.SetName(command, $"AbilityGrantRequest_{abilityCode}");
                ecb.SetComponent(command, new AbilityCommandRequestComponent
                {
                    Owner = asc,
                    AbilityCode = abilityCode,
                    CommandType = EAbilityCommandType.Grant,
                });
            }
        }

        private static void CreateAscCommand(EntityManager em, EntityCommandBuffer ecb, Entity asc, ASCCommandRequestComponent command)
        {
            var commandEntity = ecb.CreateEntity(GASRuntimeEntityArchetypes.ASCCommandRequest(em));
            command.ASC = asc;
            ecb.SetName(commandEntity, $"Asc{command.CommandType}Request_{asc.Index}");
            ecb.SetComponent(commandEntity, command);
        }
    }
}
