using System.Collections.Generic;  
  
namespace GAS.Runtime  
{  
    public class XParamPlaySound : XParam  
    {  
        [BeanField(nameof(SetAudioClipPath), Comment = "音效资源路径")]
        public string AudioClipPath;  
  
        [BeanField(nameof(SetVolume), Comment = "音量")]
        public float Volume;  
  
        [BeanField(nameof(SetSpeed), Comment = "播放速度")]
        public float Speed;  
  
        [BeanField(nameof(SetLoop), Comment = "是否循环")]
        public bool Loop;  
  
        [BeanField(nameof(SetAudioSourceNodePath), Comment = "AudioSource节点路径")]
        public string AudioSourceNodePath;  
  
        public XParamPlaySound()  
        {  
            AudioClipPath = string.Empty;  
            Volume = 1f;  
            Speed = 1f;  
            Loop = false;  
            AudioSourceNodePath = string.Empty;  
        }  
  
        public void SetAudioClipPath(string path) => AudioClipPath = path;  
        public void SetVolume(float volume) => Volume = volume;  
        public void SetSpeed(float speed) => Speed = speed;  
        public void SetLoop(bool loop) => Loop = loop;  
        public void SetAudioSourceNodePath(string path) => AudioSourceNodePath = path;  
  
#if UNITY_EDITOR  
        public void DecodeExcelData(List<object> paramData)  
        {  
            if (paramData == null || paramData.Count == 0)  
            {  
                AudioClipPath = string.Empty;  
                Volume = 1f;  
                Speed = 1f;  
                Loop = false;  
                AudioSourceNodePath = string.Empty;  
                return;  
            }  
  
            if (paramData.Count > 0 && paramData[0] is string s0)  
                AudioClipPath = s0 == XParamDefault.DefaultString ? string.Empty : s0;  
  
            if (paramData.Count > 1)  
                float.TryParse(paramData[1]?.ToString(), out Volume);  
            else  
                Volume = 1f;  
  
            if (paramData.Count > 2)  
                float.TryParse(paramData[2]?.ToString(), out Speed);  
            else  
                Speed = 1f;  
  
            if (paramData.Count > 3)  
                bool.TryParse(paramData[3]?.ToString(), out Loop);  
            else  
                Loop = false;  
  
            if (paramData.Count > 4 && paramData[4] is string s4)  
                AudioSourceNodePath = s4 == XParamDefault.DefaultString ? string.Empty : s4;  
            else  
                AudioSourceNodePath = string.Empty;  
        }  
  
        public List<object> EncodeExcelData()  
        {  
            var paramData = new List<object>  
            {  
                string.IsNullOrEmpty(AudioClipPath) ? XParamDefault.DefaultString : AudioClipPath,  
                Volume,  
                Speed,  
                Loop,  
                string.IsNullOrEmpty(AudioSourceNodePath) ? XParamDefault.DefaultString : AudioSourceNodePath,  
            };  
            return paramData;  
        }  
#endif  
    }  
}
