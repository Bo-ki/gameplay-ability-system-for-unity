using System.Collections.Generic;

namespace GAS.Runtime
{
    public class Track
    {
        public string Name { get; set; }
        public List<TimelineActionClipData> ActionClips = new List<TimelineActionClipData>();
    }

}
