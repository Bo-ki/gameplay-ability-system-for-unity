using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    public class MCCue : IComponentData
    {
        public GameplayCueBase cue;
        
        public MCCue()
        {
        }
        
        public MCCue(GameplayCueBase cue)
        {
            this.cue = cue;
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
