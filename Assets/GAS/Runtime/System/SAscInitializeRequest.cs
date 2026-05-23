using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SASCCreate))]
    [UpdateBefore(typeof(SAscCommandRequest))]
    public partial struct SAscInitializeRequest : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CAscInitializeRequest>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var requests = _query.ToEntityArray(Allocator.Temp);
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                var requestEntity = requests[i];
                if (!em.Exists(requestEntity) || !em.HasComponent<CAscInitializeRequest>(requestEntity))
                    continue;

                var request = em.GetComponentData<CAscInitializeRequest>(requestEntity);
                if (request.ASC != Entity.Null
                    && em.Exists(request.ASC)
                    && !em.HasComponent<CAscDestroying>(request.ASC))
                {
                    CreateAscCommand(ecb, request.ASC, new CAscCommandRequest
                    {
                        CommandType = EAscCommandType.SetLevel,
                        Level = request.Level,
                    });

                    CreateFixedTagCommands(em, ecb, requestEntity, request.ASC);
                    CreateAttributeCommands(em, ecb, requestEntity, request.ASC);
                    CreateAbilityGrantCommands(em, ecb, requestEntity, request.ASC);
                }

                ecb.DestroyEntity(requestEntity);
            }

            ecb.Playback(em);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void CreateFixedTagCommands(EntityManager em, EntityCommandBuffer ecb, Entity requestEntity, Entity asc)
        {
            if (!em.HasBuffer<BAscInitFixedTag>(requestEntity))
                return;

            var tags = em.GetBuffer<BAscInitFixedTag>(requestEntity);
            for (var i = 0; i < tags.Length; i++)
            {
                CreateAscCommand(ecb, asc, new CAscCommandRequest
                {
                    CommandType = EAscCommandType.AddFixedTag,
                    TagCode = tags[i].TagCode,
                });
            }
        }

        private static void CreateAttributeCommands(EntityManager em, EntityCommandBuffer ecb, Entity requestEntity, Entity asc)
        {
            if (!em.HasBuffer<BAscInitAttribute>(requestEntity))
                return;

            var attributes = em.GetBuffer<BAscInitAttribute>(requestEntity);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                CreateAscCommand(ecb, asc, new CAscCommandRequest
                {
                    CommandType = EAscCommandType.AddAttribute,
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
            if (!em.HasBuffer<BAscInitAbility>(requestEntity))
                return;

            var abilities = em.GetBuffer<BAscInitAbility>(requestEntity);
            for (var i = 0; i < abilities.Length; i++)
            {
                var abilityCode = abilities[i].AbilityCode;
                if (abilityCode <= 0)
                    continue;

                var command = ecb.CreateEntity();
                ecb.SetName(command, $"AbilityGrantRequest_{abilityCode}");
                ecb.AddComponent(command, new CAbilityCommandRequest
                {
                    Owner = asc,
                    AbilityCode = abilityCode,
                    CommandType = EAbilityCommandType.Grant,
                });
            }
        }

        private static void CreateAscCommand(EntityCommandBuffer ecb, Entity asc, CAscCommandRequest command)
        {
            var commandEntity = ecb.CreateEntity();
            command.ASC = asc;
            ecb.SetName(commandEntity, $"Asc{command.CommandType}Request_{asc.Index}");
            ecb.AddComponent(commandEntity, command);
        }
    }
}
