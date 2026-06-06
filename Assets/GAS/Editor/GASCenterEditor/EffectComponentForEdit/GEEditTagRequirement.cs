using System;
using System.Collections.Generic;

namespace GAS.Editor
{
    [Serializable]
    public class GEEditTagRequirement
    {
        public List<int> All = new();
        public List<int> Any = new();
        public List<int> None = new();

        public bool HasAnyValue()
        {
            return (All != null && All.Count > 0)
                || (Any != null && Any.Count > 0)
                || (None != null && None.Count > 0);
        }
    }
}
