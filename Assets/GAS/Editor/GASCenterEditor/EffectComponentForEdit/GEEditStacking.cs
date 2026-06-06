using System;
using System.Collections.Generic;
using GAS.Runtime;
using UnityEngine.Serialization;

namespace GAS.Editor
{
    [Serializable]
    public class GEEditStacking
    {
        public int code;
        public EffectStackType stackingType;
        public int limitCount;
        public EffectDurationRefreshPolicy durationRefreshPolicy;
        public EffectPeriodResetPolicy periodResetPolicy;

        [FormerlySerializedAs("expirationPolicy")]
        public EffectExpirationPolicy stackingExpirationPolicy;

        public bool DenyOverflowApplication;
        public bool clearStackOnOverflow;
        public List<int> overflowEffects;
    }
}
