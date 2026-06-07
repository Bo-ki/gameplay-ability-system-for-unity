using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// Presentation binding helper. Runtime simulation code should use EntityManager / ECB directly.
    /// </summary>
    public static class PresentationEntityBindingRegistry
    {
        private static readonly Dictionary<EntityBindingKey, GameObject> BindingGameObjects = new();

        public static void ClearGameObjectBinding()
        {
            BindingGameObjects.Clear();
        }

        public static void BindGameObjectToEntity(EntityManager entityManager, Entity entity, GameObject gameObject)
        {
            if (!IsUsable(entityManager) || !entityManager.Exists(entity) || gameObject == null)
                return;

            BindingGameObjects[new EntityBindingKey(entityManager, entity)] = gameObject;
        }

        public static void UnbindGameObjectToEntity(EntityManager entityManager, Entity entity)
        {
            if (!IsUsable(entityManager))
                return;

            BindingGameObjects.Remove(new EntityBindingKey(entityManager, entity));
        }

        public static GameObject GetGameObjectFromEntity(EntityManager entityManager, Entity entity)
        {
            if (!IsUsable(entityManager) || !entityManager.Exists(entity))
                return null;

            return BindingGameObjects.GetValueOrDefault(new EntityBindingKey(entityManager, entity));
        }

        public static string GetEntityName(EntityManager entityManager, Entity entity)
        {
            if (!IsUsable(entityManager) || !entityManager.Exists(entity))
                return string.Empty;

            var name = entityManager.GetName(entity);
            return string.IsNullOrEmpty(name) ? entity.ToString() : name;
        }

        private static bool IsUsable(EntityManager entityManager)
        {
            return entityManager.World != null && entityManager.World.IsCreated;
        }

        private readonly struct EntityBindingKey : IEquatable<EntityBindingKey>
        {
            private readonly ulong _worldSequenceNumber;
            private readonly Entity _entity;

            public EntityBindingKey(EntityManager entityManager, Entity entity)
            {
                _worldSequenceNumber = entityManager.World.SequenceNumber;
                _entity = entity;
            }

            public bool Equals(EntityBindingKey other)
            {
                return _worldSequenceNumber == other._worldSequenceNumber && _entity == other._entity;
            }

            public override bool Equals(object obj)
            {
                return obj is EntityBindingKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_worldSequenceNumber.GetHashCode() * 397) ^ _entity.GetHashCode();
                }
            }
        }
    }
}
