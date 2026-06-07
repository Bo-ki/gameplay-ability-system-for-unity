using Unity.Entities;
using Unity.Collections;
using System.Collections.Generic;
namespace GAS.Editor
{
    public static class EntityQueryHelper
    {
        /// <summary>
        /// 获取所有包含指定组件的实体列表
        /// </summary>
        public static List<Entity> GetAllEntitiesWithComponent<T>(
            World world,
            Allocator allocator = Allocator.TempJob
        ) where T : IComponentData
        {
            var result = new List<Entity>();
            if (world == null || !world.IsCreated)
                return result;

            using var query = new EntityQueryBuilder(allocator)
                .WithAll<T>()
                .Build(world.EntityManager);

            using (var entities = query.ToEntityArray(allocator))
            {
                result.AddRange(entities);
            }

            return result;
        }

        /// <summary>
        /// 获取实体及其组件数据
        /// </summary>
        public static List<(Entity, T)> GetAllComponentData<T>(World world) where T : unmanaged, IComponentData
        {
            var result = new List<(Entity, T)>();
            if (world == null || !world.IsCreated)
                return result;

            var entityManager = world.EntityManager;
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<T>()
                .Build(entityManager);

            var entities = query.ToEntityArray(Allocator.TempJob);
            var components = query.ToComponentDataArray<T>(Allocator.TempJob);

            for (int i = 0; i < entities.Length; i++)
            {
                result.Add((entities[i], components[i]));
            }

            entities.Dispose();
            components.Dispose();
            query.Dispose();

            return result;
        }
    }
}
