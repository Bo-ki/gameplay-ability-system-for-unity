using System;
using System.Collections.Generic;

namespace GAS.Editor
{
    [Serializable]
    public class GEEditPeriod
    {
        public bool firstTrigger;
        public int time;
        public List<int> effects;
    }
}
