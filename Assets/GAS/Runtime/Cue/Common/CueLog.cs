using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// 将 Cue 激活事实写入 Unity 日志的纯表现适配器，不参与 Runtime v1 状态变更。
    /// </summary>
    public class CueLog : GameplayCueBase<XParamString>
    {
        public override void OnActivate(float time)
        {
            base.OnActivate(time);
            Debug.Log(
                $"[{time}]SourceType:{SourceType}, StableId:{SourceStableId}, Msg:{Parameter.Value}");

            StopImmediate();
            RemoveSelf();
        }

        public void SetMessage(string message)
        {
            Parameter.SetValue(message);
        }

        public override void Reset()
        {
            base.Reset();
        }
#if UNITY_EDITOR
        public override void OnPreview(GameObject target, int frame, int startFrame, int endFrame)
        {
            base.OnPreview(target, frame, startFrame, endFrame);
            Debug.Log($"[Preview Frame {frame}]Msg:{Parameter.Value}");
        }
#endif
    }
}
