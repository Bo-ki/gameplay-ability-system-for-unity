using System;
using System.Collections.Generic;
using System.Linq;

namespace GAS.Runtime
{
    public class XParamCue : XParam
    {
        [BeanField(nameof(SetRequiredTags),Order = 1)]
        public List<int> RequiredTags;

        [BeanField(nameof(SetImmunityTags),Order = 2)]
        public List<int> ImmunityTags;
        
        
        [BeanPolymorphicField(  
            beanFieldName: "CueLogic",  
            lubanPolymorphicType: nameof(GameplayCueBase),  
            typeSetter: nameof(SetCueType),  
            paramSetter: nameof(SetParam),  
            ParamTypeResolver = "CueHelper.GetCueLogicParamType",  
            HelperCategory = "Cue",
            Order = 3)] 
        public string CueType { get; private set; }

        public XParam Param { get; set; }
        
        public void SetCueType(string cueType)
        {
            CueType = cueType;
        }

        public void SetParam(XParam param)
        {
            Param = param;
        }
        
        public void SetRequiredTags(int[] requiredTags)
        {
            RequiredTags = requiredTags?.ToList() ?? new List<int>();
        }

        public void SetImmunityTags(int[] immunityTags)
        {
            ImmunityTags = immunityTags?.ToList() ?? new List<int>();
        }

        public XParamCue()
        {
            CueType = "";
            Param = null;
            RequiredTags = new List<int>();
            ImmunityTags = new List<int>();
        }

        public XParamCue(string cueType, XParam param = null, int[] requiredTags = null,
            int[] immunityTags = null)
        {
            CueType = cueType;
            Param = param;
            RequiredTags = requiredTags!=null ? requiredTags.ToList(): new List<int>();
            ImmunityTags = immunityTags!=null ? immunityTags.ToList(): new List<int>();
        }

        public GameplayCueConfig GetCueConfig()
        {
            var cueType = CueHelper.GetCueType(CueType);
            return new GameplayCueConfig(
                cueType,
                Param,
                (RequiredTags ?? new List<int>()).ToArray(),
                (ImmunityTags ?? new List<int>()).ToArray());
        }
        
#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            if (paramData == null)
                return;

            // RequiredTags
            RequiredTags = new List<int>();
            if (paramData.Count > 0)
            {
                var strTags = paramData[0]?.ToString() ?? string.Empty;
                if (strTags != "0")
                {
                    var tags = strTags.Split(';');
                    foreach (var tag in tags)
                        if (int.TryParse(tag, out var tagInt))
                            RequiredTags.Add(tagInt);
                }
            }
            
            // ImmunityTags
            ImmunityTags = new List<int>();
            if (paramData.Count > 1)
            {
                var strTags = paramData[1]?.ToString() ?? string.Empty;
                if (strTags != "0")
                {
                    var tags = strTags.Split(';');
                    foreach (var tag in tags)
                        if (int.TryParse(tag, out var tagInt))
                            ImmunityTags.Add(tagInt);
                }
            }
            
            // CueType
            if (paramData.Count > 2) 
                CueType = paramData[2]?.ToString() ?? string.Empty;
            
            // Param
            if (paramData.Count > 3)
            {
                List<object> paramDataForCue = new List<object>();
                for (int i = 3; i < paramData.Count; i++)
                {
                    paramDataForCue.Add(paramData[i]);
                }

                var cueParamType = CueHelper.GetCueLogicParamType(CueType);
                if (cueParamType == null)
                {
                    Param = null;
                    return;
                }

                Param = Activator.CreateInstance(cueParamType) as XParam;
                Param?.DecodeExcelData(paramDataForCue);
            }
        }

        public List<object> EncodeExcelData()
        {
            var result = new List<object>();
            // RequiredTags
            var strRequiredTags = "";
            var requiredTags = RequiredTags ?? new List<int>();
            if (requiredTags.Count == 0)
            {
                strRequiredTags = "0";
            }
            else
            {
                for (var i = 0; i < requiredTags.Count; i++)
                {
                    strRequiredTags += requiredTags[i].ToString();
                    if (i < requiredTags.Count - 1) strRequiredTags += ";";
                }
            }

            result.Add(strRequiredTags);
            
            // ImmunityTags
            var strImmunityTags = "";
            var immunityTags = ImmunityTags ?? new List<int>();
            if (immunityTags.Count == 0)
            {
                strImmunityTags = "0";
            }
            else
            {
                for (var i = 0; i < immunityTags.Count; i++)
                {
                    strImmunityTags += immunityTags[i].ToString();
                    if (i < immunityTags.Count - 1) strImmunityTags += ";";
                }
            }

            result.Add(strImmunityTags);
            // CueType
            result.Add(CueType);
            // Param
            if (Param != null)
                result.AddRange(Param.EncodeExcelData());
           
            return result;
        }


#endif
    }
}
