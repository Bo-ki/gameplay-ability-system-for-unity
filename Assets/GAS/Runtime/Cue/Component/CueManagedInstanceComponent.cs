using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    public class CueManagedInstanceComponent : IComponentData
    {
        public GameplayCueBase Cue;

        public CueManagedInstanceComponent()
        {
        }

        public CueManagedInstanceComponent(EntityManager entityManager, Entity cueEntity, GameplayCueBase cue)
        {
            Cue = cue;
            Cue?.SetRuntime(entityManager, cueEntity);
        }
    }

    [Serializable]
    public struct CueSetting
    {
        [SerializeField]
        public List<int> requiredTags;

        [SerializeField]
        public List<int> immunityTags;

    }
}
