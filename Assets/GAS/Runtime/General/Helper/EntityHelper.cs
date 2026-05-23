using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// Presentation binding helper. Runtime simulation code should use EntityManager / ECB directly.
    /// </summary>
    public static class EntityHelper
    {
        private static EntityManager EntityManager => GASManager.EntityManager;
        private static readonly Dictionary<Entity, GameObject> BindingGameObjects = new();

        public static void ClearGameObjectBinding()
        {
            BindingGameObjects.Clear();
        }

        public static void BindGameObjectToEntity(Entity entity, GameObject gameObject)
        {
            if (!GASManager.IsInitialized || !EntityManager.Exists(entity) || gameObject == null)
                return;

            BindingGameObjects[entity] = gameObject;
        }

        public static void UnbindGameObjectToEntity(Entity entity)
        {
            if (GASManager.ExWorld == null || !GASManager.ExWorld.IsCreated)
                return;

            BindingGameObjects.Remove(entity);
        }

        public static GameObject GetGameObjectFromEntity(Entity entity)
        {
            return BindingGameObjects.GetValueOrDefault(entity);
        }

        public static string GetEntityName(Entity entity)
        {
            if (!GASManager.IsInitialized || !EntityManager.Exists(entity))
                return string.Empty;

            var name = EntityManager.GetName(entity);
            return string.IsNullOrEmpty(name) ? entity.ToString() : name;
        }
    }
}
