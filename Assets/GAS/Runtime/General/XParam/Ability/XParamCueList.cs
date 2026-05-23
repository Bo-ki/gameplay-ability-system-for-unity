using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    public class XParamCueList : XParam
    {
        [BeanField(nameof(SetIDs))]
        public int[] IDs { get; private set; }

        public void SetIDs(int[] value)
        {
            IDs = value;
        }
        
        public XParamCueList(int[] value)
        {
            IDs = value;
        }
        
        public XParamCueList()
        {
            IDs = Array.Empty<int>();
        }
#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            if (paramData == null || paramData.Count == 0)
            {
                IDs = Array.Empty<int>();
                return;
            }

            var strData = paramData[0] as string;
            if (string.IsNullOrEmpty(strData))
            {
                IDs = Array.Empty<int>();
                return;
            }

            var ids = strData.Split(';');
            IDs = new int[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                var strID = ids[i];
                IDs[i] = int.TryParse(strID, out var id) ? id : 0;
            }
        }

        public List<object> EncodeExcelData()
        {
            var strIDs = "";
            for (var i = 0; i < IDs.Length; i++)
            {
                strIDs += IDs[i].ToString();
                if (i < IDs.Length - 1) strIDs += ";";
            }
            var result = new List<object> { strIDs };
            return result;
        }
#endif
    }
}
