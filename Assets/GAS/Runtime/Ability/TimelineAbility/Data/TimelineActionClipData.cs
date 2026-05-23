using System;

namespace GAS.Runtime
{
    [Serializable]
    public class TimelineActionClipData
    {
        public string Name;
        public int StartTime;
        public int EndTime;
        public string ActionType;
        public XParam Parameter;

        public int Duration => EndTime - StartTime;
        
        public void SetParameter(XParam parameter)
        {
            Parameter = parameter;
        }
    }
}
