using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
namespace GAS.Editor
{
    public class AbilityTimelineTrackEditor : ScriptableObject
    {
        public string trackName;
        public string trackInfo;

        private AbilityTimelineTrack _track;

        public static AbilityTimelineTrackEditor Create(AbilityTimelineTrack track)
        {
            var editor = CreateInstance<AbilityTimelineTrackEditor>();
            editor.hideFlags = HideFlags.HideAndDontSave;
            editor._track = track;
            editor.trackName = track.TrackData.Name;
            editor.UpdateTrackInfo();
            return editor;
        }

        public void Refresh()
        {
            if (_track == null)
                return;

            trackName = _track.TrackData.Name;
            UpdateTrackInfo();
        }

        public void ApplyTrackName()
        {
            if (_track == null)
                return;

            _track.TrackData.Name = trackName;
            _track.RefreshShow();
            UpdateTrackInfo();
        }

        private void UpdateTrackInfo()
        {
            if (_track == null)
            {
                trackInfo = string.Empty;
                return;
            }

            var builder = new StringBuilder();
            foreach (var clip in _track.TrackData.ActionClips)
            {
                builder.Append('[')
                    .Append(clip.ActionType)
                    .Append(':')
                    .Append(clip.Name)
                    .AppendLine("]");
                builder.Append("   Run(f):")
                    .Append(clip.StartTime)
                    .Append(" -> ")
                    .Append(clip.EndTime)
                    .AppendLine();
            }

            trackInfo = builder.ToString();
        }
    }

    [CustomEditor(typeof(AbilityTimelineTrackEditor))]
    public class TimelineActionTrackInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var trackEditor = (AbilityTimelineTrackEditor)target;
            trackEditor.Refresh();

            var root = new VisualElement();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            var title = new Label("Timeline Track");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;
            root.Add(title);

            var trackName = new TextField("轨道名[展示用]") { value = trackEditor.trackName };
            trackName.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(trackEditor, "Change Timeline Track Name");
                trackEditor.trackName = evt.newValue;
                trackEditor.ApplyTrackName();
                EditorUtility.SetDirty(trackEditor);
            });
            root.Add(trackName);

            var infoTitle = new Label("轨道片段");
            infoTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            infoTitle.style.marginTop = 8;
            root.Add(infoTitle);

            var info = new TextField
            {
                multiline = true,
                value = trackEditor.trackInfo
            };
            info.SetEnabled(false);
            info.style.minHeight = 90;
            root.Add(info);

            return root;
        }
    }
}
#endif
