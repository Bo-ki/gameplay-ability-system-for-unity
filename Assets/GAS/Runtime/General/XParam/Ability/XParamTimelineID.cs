using System.Collections.Generic;

namespace GAS.Runtime
{
    public class XParamTimelineID:XParam
    {
        [BeanField(nameof(SetID))]
        public int ID;
        
        
        public void SetID(int value)
        {
            ID = value;
        }
        
        public XParamTimelineID(int id)
        {
            ID = id;
        }
        
        public XParamTimelineID()
        {
            ID = 0;
        }
        
#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            if (paramData == null || paramData.Count == 0)
            {
                ID = 0;
                return;
            }

            switch (paramData[0])
            {
                case string strData:
                    ID = string.IsNullOrEmpty(strData) ? 0 : int.Parse(strData);
                    break;
                case int intData:
                    ID = intData;
                    break;
                case double doubleData:
                    ID = (int)doubleData;
                    break;
            }
        }

        public List<object> EncodeExcelData()
        {
            var result = new List<object> { ID };
            return result;
        }
#endif
    }
}
