using System;
using GAS.Runtime;

namespace GAS.Editor
{
    [Serializable]
    public class GEEditPeriodModifier
    {
        public int AttrSet;
        public int Attribute;
        public float Magnitude;
        public EModifierOp Operation;
    }
}
