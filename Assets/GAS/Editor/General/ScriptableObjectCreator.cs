#if UNITY_EDITOR
namespace GAS.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    public static class ScriptableObjectCreator
    {
        public static void ShowDialog<T>(string defaultDestinationPath, Action<T> onScritpableObjectCreated = null)
            where T : ScriptableObject
        {
            var scriptableObjectTypes = TypeCache.GetTypesDerivedFrom<T>()
                .Where(type => type.IsClass && !type.IsAbstract)
                .OrderBy(type => type.Name)
                .ToArray();

            if (scriptableObjectTypes.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Create ScriptableObject",
                    $"没有找到继承自 {typeof(T).Name} 的可创建类型。",
                    "确定");
                return;
            }

            if (scriptableObjectTypes.Length == 1)
            {
                CreateAsset(scriptableObjectTypes[0], defaultDestinationPath, onScritpableObjectCreated);
                return;
            }

            ScriptableObjectCreatorWindow.Show(
                typeof(T).Name,
                scriptableObjectTypes,
                type => CreateAsset(type, defaultDestinationPath, onScritpableObjectCreated));
        }

        private static void CreateAsset<T>(
            Type type,
            string defaultDestinationPath,
            Action<T> onScritpableObjectCreated)
            where T : ScriptableObject
        {
            var destinationPath = string.IsNullOrWhiteSpace(defaultDestinationPath)
                ? "Assets"
                : defaultDestinationPath.TrimEnd('/', '\\');

            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
                AssetDatabase.Refresh();
            }

            var assetPath = EditorUtility.SaveFilePanelInProject(
                "Save object as",
                $"New {type.Name}.asset",
                "asset",
                "选择 ScriptableObject 保存路径。",
                destinationPath);

            if (string.IsNullOrEmpty(assetPath))
                return;

            var obj = ScriptableObject.CreateInstance(type) as T;
            if (obj == null)
            {
                EditorUtility.DisplayDialog(
                    "Create ScriptableObject",
                    $"无法创建 {type.FullName}。",
                    "确定");
                return;
            }

            AssetDatabase.CreateAsset(obj, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = obj;
            onScritpableObjectCreated?.Invoke(obj);
        }
    }

    internal sealed class ScriptableObjectCreatorWindow : EditorWindow
    {
        private readonly List<Type> _allTypes = new();
        private readonly List<Type> _filteredTypes = new();

        private Action<Type> _onSelected;
        private ListView _listView;
        private Label _description;
        private Type _selectedType;

        public static void Show(string baseTypeName, IReadOnlyList<Type> types, Action<Type> onSelected)
        {
            var window = CreateInstance<ScriptableObjectCreatorWindow>();
            window.titleContent = new GUIContent($"Create {baseTypeName}");
            window.minSize = new Vector2(360, 420);
            window._allTypes.AddRange(types);
            window._filteredTypes.AddRange(types);
            window._onSelected = onSelected;
            window.ShowUtility();
        }

        public void CreateGUI()
        {
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            var search = new ToolbarSearchField();
            search.style.marginBottom = 6;
            search.RegisterValueChangedCallback(evt => ApplyFilter(evt.newValue));
            rootVisualElement.Add(search);

            _listView = new ListView
            {
                itemsSource = _filteredTypes,
                fixedItemHeight = 24,
                selectionType = SelectionType.Single,
                makeItem = () => new Label(),
                bindItem = (element, index) =>
                {
                    ((Label)element).text = _filteredTypes[index].Name;
                }
            };
            _listView.style.flexGrow = 1;
            _listView.selectionChanged += OnSelectionChanged;
            rootVisualElement.Add(_listView);

            _description = new Label("选择一个 ScriptableObject 类型。");
            _description.style.whiteSpace = WhiteSpace.Normal;
            _description.style.marginTop = 8;
            _description.style.marginBottom = 8;
            rootVisualElement.Add(_description);

            var row = new Toolbar();
            row.Add(new ToolbarButton(Close) { text = "取消" });

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            row.Add(spacer);

            row.Add(new ToolbarButton(CreateSelected) { text = "创建" });
            rootVisualElement.Add(row);
        }

        private void ApplyFilter(string keyword)
        {
            _filteredTypes.Clear();
            var normalized = keyword?.Trim();
            foreach (var type in _allTypes)
            {
                if (string.IsNullOrEmpty(normalized) ||
                    type.Name.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (type.FullName?.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                {
                    _filteredTypes.Add(type);
                }
            }

            _selectedType = null;
            _description.text = _filteredTypes.Count == 0
                ? "没有匹配的类型。"
                : "选择一个 ScriptableObject 类型。";
            _listView.Rebuild();
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            _selectedType = selection.OfType<Type>().FirstOrDefault();
            _description.text = _selectedType == null
                ? "选择一个 ScriptableObject 类型。"
                : _selectedType.FullName;
        }

        private void CreateSelected()
        {
            if (_selectedType == null)
                return;

            var selected = _selectedType;
            Close();
            _onSelected?.Invoke(selected);
        }
    }
}
#endif
