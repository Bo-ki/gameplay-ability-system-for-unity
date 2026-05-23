using GAS.Runtime;
using UnityEngine;

#if UNITY_EDITOR
namespace GAS.Editor
{
    using UnityEngine.UIElements;
    
    public class TimelineActionClip
    {
        private VisualElement _ve;
        public VisualElement Ve => _ve;
        
        public TimelineActionClipData ActionClipData { get;private set; }

        public TrackClipVisualElement ClipVe => _ve as TrackClipVisualElement;
        public float FrameUnitWidth { get; protected set; }
        public int StartFrameIndex => ActionClipData.StartTime;
        public int EndFrameIndex => ActionClipData.EndTime;
        public int DurationFrame => EndFrameIndex - StartFrameIndex;

        public Label ItemLabel => ClipVe.ItemLabel;
        
        public Object DataInspector => TimelineActionClipEditor.Create(this);

        private AbilityTimelineTrack _track;

        public void InitTrackClip(
            AbilityTimelineTrack track,
            VisualElement parent,
            float frameUnitWidth,
            TimelineActionClipData actionClipData)
        {
            _track = track;
            
            FrameUnitWidth = frameUnitWidth;
            ActionClipData = actionClipData;

            _ve = new TrackClipVisualElement();
            ClipVe.InitClipInfo(this);
            parent.Add(_ve);
            if (AbilityTimelineEditorWindow.Instance.CurrentInspectorObject is TimelineActionClip clipBase &&
                actionClipData == clipBase.ActionClipData)
                ClipVe.OnSelect();
            else
                ClipVe.OnUnSelect();
  
            RefreshShow(FrameUnitWidth);
        }

        public void Delete()
        {
            var success = _track.TrackData.ActionClips.Remove(ActionClipData);
            if (!success) return;
            _track.RemoveTrackItem(this);
            AbilityTimelineEditorWindow.Instance.SetInspector();
        }

        public void RefreshShow(float newFrameUnitWidth)
        {
            FrameUnitWidth = newFrameUnitWidth;
            // clip位置，宽度
            var mainPos = _ve.transform.position;
            mainPos.x = StartFrameIndex * FrameUnitWidth;
            _ve.transform.position = mainPos;
            _ve.style.width = DurationFrame * FrameUnitWidth;
            
            ClipVe.UpdateState(ActionClipData.StartTime == ActionClipData.EndTime);
            ItemLabel.text = ActionClipData.Name;
        }

        public void UpdateClipDataStartFrame(int newStartFrame)
        {
            ActionClipData.StartTime = newStartFrame;
        }

        public void UpdateClipDataEndFrame(int endFrame)
        {
            ActionClipData.EndTime = endFrame;
        }

        public void OnTickView(int frameIndex, int startFrame, int endFrame)
        {
            if (frameIndex < startFrame || frameIndex > endFrame) return;
            // 托管时间轴执行链已移除；时间轴数据仍可编辑，预览后续应接 ECS 事件流。
        }
    }
}
#endif
