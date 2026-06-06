using System;
using GAS.Runtime;

namespace GAS.Editor
{
    [Serializable]
    public class GEEditDurarion
    {
        public int time;
        
        public TimeUnit Unit;
        
        public bool ResetStartTimeWhenActivated;
    }
}
