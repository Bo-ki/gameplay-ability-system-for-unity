using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GAS.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
namespace GAS.Editor
{
    public class TimelineActionClipEditor : ScriptableObject
    {
        public string Name;
        public int StartTime;
        public int EndTime;
        public string ActionType;
        public XParam Parameter;

        private TimelineActionClip _clip;

        public static TimelineActionClipEditor Create(TimelineActionClip clip)
        {
            var editor = CreateInstance<TimelineActionClipEditor>();
            editor.hideFlags = HideFlags.HideAndDontSave;
            editor._clip = clip;
            editor.Refresh();
            return editor;
        }

        public void Refresh()
        {
            if (_clip == null)
                return;

            Name = _clip.ActionClipData.Name;
            StartTime = _clip.ActionClipData.StartTime;
            EndTime = _clip.ActionClipData.EndTime;
            ActionType = _clip.ActionClipData.ActionType;
            Parameter = _clip.ActionClipData.Parameter;
        }

        public void ApplyName()
        {
            if (_clip == null)
                return;

            _clip.ActionClipData.Name = Name;
            _clip.RefreshShow(_clip.FrameUnitWidth);
        }

        public void ApplyStartFrame()
        {
            if (_clip == null)
                return;

            StartTime = Mathf.Clamp(StartTime, 0, EndTime);
            _clip.UpdateClipDataStartFrame(StartTime);
            _clip.RefreshShow(_clip.FrameUnitWidth);
        }

        public void ApplyEndFrame()
        {
            if (_clip == null)
                return;

            var abilityConfig = AbilityTimelineEditorWindow.Instance.AbilityConfig;
            var lifeTime = abilityConfig != null ? abilityConfig.LifeTime : EndTime;
            EndTime = Mathf.Clamp(EndTime, StartTime, lifeTime);
            _clip.UpdateClipDataEndFrame(EndTime);
            _clip.RefreshShow(_clip.FrameUnitWidth);
        }

        public void ApplyActionType()
        {
            if (_clip == null)
                return;

            _clip.ActionClipData.ActionType = ActionType;
            Parameter = EditorAbilityHelper.CreateTimelineActionParameter(ActionType);
            _clip.ActionClipData.Parameter = Parameter;
        }

        public void ApplyParameter()
        {
            if (_clip == null)
                return;

            _clip.ActionClipData.Parameter = Parameter;
        }

        public void Delete()
        {
            _clip?.Delete();
        }
    }

    [CustomEditor(typeof(TimelineActionClipEditor))]
    public class TimelineActionClipInspector : UnityEditor.Editor
    {
        private static readonly List<string> TimelineActionTypes =
            EditorAbilityHelper.GetTimelineActionTypeNames().ToList();

        public override VisualElement CreateInspectorGUI()
        {
            var clipEditor = (TimelineActionClipEditor)target;
            clipEditor.Refresh();

            var root = new VisualElement();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            root.Add(Header("Timeline Action"));

            var nameField = new TextField("Action名[展示用]") { value = clipEditor.Name };
            nameField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(clipEditor, "Change Timeline Action Name");
                clipEditor.Name = evt.newValue;
                clipEditor.ApplyName();
                EditorUtility.SetDirty(clipEditor);
            });
            root.Add(nameField);

            var startField = new IntegerField("起始帧(f)") { value = clipEditor.StartTime };
            startField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(clipEditor, "Change Timeline Action Start");
                clipEditor.StartTime = evt.newValue;
                clipEditor.ApplyStartFrame();
                startField.SetValueWithoutNotify(clipEditor.StartTime);
                EditorUtility.SetDirty(clipEditor);
            });
            root.Add(startField);

            var endField = new IntegerField("结束帧(f)") { value = clipEditor.EndTime };
            endField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(clipEditor, "Change Timeline Action End");
                clipEditor.EndTime = evt.newValue;
                clipEditor.ApplyEndFrame();
                endField.SetValueWithoutNotify(clipEditor.EndTime);
                EditorUtility.SetDirty(clipEditor);
            });
            root.Add(endField);

            root.Add(Header("Action"));

            if (TimelineActionTypes.Count == 0)
            {
                root.Add(new HelpBox("没有可用的 Timeline Action 类型。", HelpBoxMessageType.Warning));
            }
            else
            {
                var current = TimelineActionTypes.Contains(clipEditor.ActionType)
                    ? clipEditor.ActionType
                    : TimelineActionTypes[0];
                var actionTypeField = new PopupField<string>("Action类型", TimelineActionTypes, current);
                actionTypeField.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(clipEditor, "Change Timeline Action Type");
                    clipEditor.ActionType = evt.newValue;
                    clipEditor.ApplyActionType();
                    RebuildParameter(root, clipEditor);
                    EditorUtility.SetDirty(clipEditor);
                });
                root.Add(actionTypeField);
            }

            AddParameter(root, clipEditor);

            var deleteButton = new Button(clipEditor.Delete) { text = "删除" };
            deleteButton.style.marginTop = 10;
            deleteButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(deleteButton);

            return root;
        }

        private static Label Header(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 8;
            label.style.marginBottom = 4;
            return label;
        }

        private static void RebuildParameter(VisualElement root, TimelineActionClipEditor clipEditor)
        {
            var old = root.Q<VisualElement>("timeline-action-parameter");
            old?.RemoveFromHierarchy();
            AddParameter(root, clipEditor);
        }

        private static void AddParameter(VisualElement root, TimelineActionClipEditor clipEditor)
        {
            var container = new VisualElement { name = "timeline-action-parameter" };
            if (clipEditor.Parameter == null)
            {
                container.Add(new HelpBox("当前 Action 没有参数对象。", HelpBoxMessageType.Info));
            }
            else
            {
                container.Add(XParamVisualElementDrawer.Create("参数", clipEditor.Parameter, () =>
                {
                    Undo.RecordObject(clipEditor, "Change Timeline Action Parameter");
                    clipEditor.ApplyParameter();
                    EditorUtility.SetDirty(clipEditor);
                }));
            }

            root.Insert(Mathf.Max(0, root.childCount - 1), container);
        }
    }

    public static class XParamVisualElementDrawer
    {
        private const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static VisualElement Create(string title, XParam parameter, Action onChanged)
        {
            var foldout = new Foldout
            {
                text = $"{title} ({parameter.GetType().Name})",
                value = true
            };
            foldout.style.marginTop = 8;
            DrawObject(foldout, parameter, onChanged);
            return foldout;
        }

        private static void DrawObject(VisualElement root, object target, Action onChanged)
        {
            var members = target.GetType()
                .GetMembers(MemberFlags)
                .Where(IsDrawableMember)
                .OrderBy(member => member.MetadataToken);

            foreach (var member in members)
            {
                var field = CreateField(member.Name, GetMemberType(member), GetValue(member, target), newValue =>
                {
                    SetValue(member, target, newValue);
                    onChanged?.Invoke();
                });
                root.Add(field);
            }
        }

        private static bool IsDrawableMember(MemberInfo member)
        {
            if (member is FieldInfo field)
                return !field.IsStatic && !field.IsInitOnly && !field.Name.Contains("BackingField");

            if (member is PropertyInfo property)
                return property.GetIndexParameters().Length == 0 && property.GetMethod != null;

            return false;
        }

        private static Type GetMemberType(MemberInfo member)
        {
            return member switch
            {
                FieldInfo field => field.FieldType,
                PropertyInfo property => property.PropertyType,
                _ => typeof(object)
            };
        }

        private static object GetValue(MemberInfo member, object target)
        {
            return member switch
            {
                FieldInfo field => field.GetValue(target),
                PropertyInfo property => property.GetValue(target),
                _ => null
            };
        }

        private static void SetValue(MemberInfo member, object target, object value)
        {
            switch (member)
            {
                case FieldInfo field:
                    field.SetValue(target, value);
                    break;
                case PropertyInfo property when property.GetSetMethod(true) != null:
                    property.SetValue(target, value);
                    break;
            }
        }

        private static VisualElement CreateField(string label, Type type, object value, Action<object> onChanged)
        {
            if (type == typeof(int))
            {
                var field = new IntegerField(label) { value = value is int intValue ? intValue : 0 };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            if (type == typeof(float))
            {
                var field = new FloatField(label) { value = value is float floatValue ? floatValue : 0f };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            if (type == typeof(string))
            {
                var field = new TextField(label) { value = value as string ?? string.Empty };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            if (type == typeof(bool))
            {
                var field = new Toggle(label) { value = value is bool boolValue && boolValue };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            if (type == typeof(Vector2))
            {
                var field = new Vector2Field(label) { value = value is Vector2 vector ? vector : Vector2.zero };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            if (type == typeof(Vector3))
            {
                var field = new Vector3Field(label) { value = value is Vector3 vector ? vector : Vector3.zero };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            if (type == typeof(int[]))
                return CreateDelimitedField(label, string.Join(";", value as int[] ?? Array.Empty<int>()),
                    text => ParseArray<int>(text, int.TryParse), onChanged);

            if (type == typeof(float[]))
                return CreateDelimitedField(label, string.Join(";", value as float[] ?? Array.Empty<float>()),
                    text => ParseArray<float>(text, float.TryParse), onChanged);

            if (type == typeof(string[]))
                return CreateDelimitedField(label, string.Join(";", value as string[] ?? Array.Empty<string>()),
                    text => string.IsNullOrWhiteSpace(text) ? Array.Empty<string>() : text.Split(';'), onChanged);

            if (typeof(IList<int>).IsAssignableFrom(type))
            {
                var values = value as IList<int> ?? new List<int>();
                return CreateDelimitedField(label, string.Join(";", values),
                    text => ParseArray<int>(text, int.TryParse).ToList(), onChanged);
            }

            if (typeof(XParam).IsAssignableFrom(type))
            {
                var foldout = new Foldout
                {
                    text = $"{label}: {value?.GetType().Name ?? "null"}",
                    value = true
                };
                if (value is XParam nested)
                    DrawObject(foldout, nested, () => onChanged(value));
                return foldout;
            }

            var readOnly = new TextField(label) { value = value?.ToString() ?? "null" };
            readOnly.SetEnabled(false);
            return readOnly;
        }

        private static TextField CreateDelimitedField(
            string label,
            string value,
            Func<string, object> parser,
            Action<object> onChanged)
        {
            var field = new TextField(label) { value = value };
            field.tooltip = "使用 ; 分隔多个值。";
            field.RegisterValueChangedCallback(evt =>
            {
                try
                {
                    onChanged(parser(evt.newValue));
                    field.tooltip = "使用 ; 分隔多个值。";
                    field.style.borderLeftWidth = 0;
                }
                catch (FormatException ex)
                {
                    field.tooltip = ex.Message;
                    field.style.borderLeftColor = Color.red;
                    field.style.borderLeftWidth = 2;
                }
            });
            return field;
        }

        private delegate bool TryParse<T>(string text, out T value);

        private static T[] ParseArray<T>(string text, TryParse<T> tryParse)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<T>();

            return text.Split(';')
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .Select(part =>
                {
                    if (tryParse(part, out var value))
                    {
                        return value;
                    }

                    throw new FormatException($"无法解析数组项: {part}");
                })
                .ToArray();
        }
    }
}
#endif
