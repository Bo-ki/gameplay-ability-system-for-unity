using System.Collections;
using System.Linq;
using GAS.Runtime;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace GAS.Editor
{
    public class TimelineActionClipEditor : OdinEditorWindow
    {
        private const string GRP_BOX = "TimelineAction";
        private const string GRP_BOX_ACTION = "TimelineAction/Action";

        private static IEnumerable _timelineActionTypes =
            EditorAbilityHelper.GetTimelineActionTypeNames().ToArray();


        [BoxGroup(GRP_BOX)] [Delayed] [LabelText("Action名[展示用]")] [OnValueChanged(nameof(OnNameChange))]
        public string Name;

        [Delayed] [BoxGroup(GRP_BOX)] [LabelText("起始帧(f)")] [OnValueChanged(nameof(OnClipStartFrameChanged))]
        public int StartTime;

        [Delayed] [BoxGroup(GRP_BOX)] [LabelText("结束帧(f)")] [OnValueChanged(nameof(OnClipEndFrameChanged))]
        public int EndTime;

        [Delayed]
        [BoxGroup(GRP_BOX_ACTION)]
        [LabelText("Action类型")]
        [ValueDropdown(nameof(_timelineActionTypes))]
        [OnValueChanged(nameof(OnActionTypeChanged))]
        public string ActionType;

        [BoxGroup(GRP_BOX_ACTION)] [HideLabel] [ShowInInspector] [HideReferenceObjectPicker]
        public XParam Parameter;

        private XParamTimeline AbilityConfig => AbilityTimelineEditorWindow.Instance.AbilityConfig;

        private TimelineActionClip _clip;
        
        public static TimelineActionClipEditor Create(TimelineActionClip clip)
        {
            var window = CreateInstance<TimelineActionClipEditor>();
            window._clip = clip;
            window.Refresh();
            return window;
        }

        [BoxGroup(GRP_BOX)]
        [Button("删除")]
        [GUIColor(0.9f, 0.2f, 0.2f)]
        private void Delete()
        {
            _clip.Delete();
        }


        private void Refresh()
        {
            Name = _clip.ActionClipData.Name;
            StartTime = _clip.ActionClipData.StartTime;
            EndTime = _clip.ActionClipData.EndTime;
            ActionType = _clip.ActionClipData.ActionType;
            Parameter = _clip.ActionClipData.Parameter;
        }

        private void OnClipStartFrameChanged()
        {
            // 钳制
            StartTime = Mathf.Clamp(StartTime, 0, EndTime);
            _clip.UpdateClipDataStartFrame(StartTime);
            _clip.RefreshShow(_clip.FrameUnitWidth);
        }
        
        private void OnClipEndFrameChanged()
        {
            // 钳制
            EndTime = Mathf.Clamp(EndTime, StartTime, AbilityConfig.LifeTime);
            _clip.UpdateClipDataEndFrame(EndTime);
            _clip.RefreshShow(_clip.FrameUnitWidth);
        }

        private void OnActionTypeChanged()
        {
            _clip.ActionClipData.ActionType = ActionType;
            Parameter = EditorAbilityHelper.CreateTimelineActionParameter(ActionType);
            _clip.ActionClipData.Parameter = Parameter;
        }

        private void OnNameChange()
        {
            _clip.ActionClipData.Name = Name;
            _clip.RefreshShow(_clip.FrameUnitWidth);
        }
    }

    [CustomEditor(typeof(TimelineActionClipEditor))]
    public class TimelineActionClipInspector : OdinEditorWithoutHeader
    {
    }
}
#endif
