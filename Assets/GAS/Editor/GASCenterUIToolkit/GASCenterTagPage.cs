using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GAS.Editor
{
    internal sealed class GASCenterTagPage : IGASCenterPage
    {
        private readonly List<TagInEditor> _allTags = new();
        private readonly List<TagInEditor> _visibleTags = new();

        private GASCenterContext _context;
        private ListView _tagList;
        private TextField _searchField;
        private TextField _idField;
        private TextField _nameField;
        private TextField _descriptionField;
        private Label _countLabel;
        private HelpBox _loadErrorBox;
        private TagInEditor _selectedTag;

        public string Id => "tag";
        public string Title => "GameplayTag 标签";
        public string Description => "浏览 Tag JSON，按名称搜索并查看 Tag 基本信息。";

        public VisualElement CreateView(GASCenterContext context)
        {
            _context = context;

            var root = new VisualElement();
            root.AddToClassList("gas-page");

            root.Add(BuildToolbar());

            _loadErrorBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            _loadErrorBox.style.display = DisplayStyle.None;
            root.Add(_loadErrorBox);

            var split = new TwoPaneSplitView(0, 340, TwoPaneSplitViewOrientation.Horizontal);
            split.AddToClassList("gas-tag-split");
            split.Add(BuildTagList());
            split.Add(BuildDetailPanel());
            root.Add(split);

            return root;
        }

        public void Refresh()
        {
            LoadTags();
            ApplyFilter();
            SelectFirstVisibleTag();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("gas-page-toolbar");

            toolbar.Add(new ToolbarButton(() => _context.RevealPath(_context.SettingAsset.PathOfExcelTag, "Tag Excel 文件")) { text = "打开 Excel" });
            toolbar.Add(new ToolbarButton(() => _context.RevealPath(_context.SettingAsset.PathOfJsonTag, "Tag Json 文件")) { text = "打开 Json" });
            toolbar.Add(new ToolbarButton(ExportJson) { text = "导出更新 Json 表" });
            toolbar.Add(new ToolbarButton(Refresh) { text = "刷新" });

            _searchField = new TextField();
            _searchField.AddToClassList("gas-search-field");
            _searchField.RegisterValueChangedCallback(_ => ApplyFilter());
            _searchField.label = "搜索";
            toolbar.Add(_searchField);

            _countLabel = new Label();
            _countLabel.AddToClassList("gas-count-label");
            toolbar.Add(_countLabel);

            return toolbar;
        }

        private VisualElement BuildTagList()
        {
            _tagList = new ListView
            {
                itemsSource = _visibleTags,
                fixedItemHeight = 24,
                selectionType = SelectionType.Single,
                makeItem = () =>
                {
                    var label = new Label();
                    label.AddToClassList("gas-tag-list-item");
                    return label;
                },
                bindItem = (element, index) =>
                {
                    var tag = _visibleTags[index];
                    var label = (Label)element;
                    label.text = $"{tag.id}  {tag.name}";
                    label.style.marginLeft = Mathf.Clamp(GetTagDepth(tag.name), 0, 8) * 12;
                }
            };

            _tagList.selectionChanged += selection =>
            {
                _selectedTag = selection.OfType<TagInEditor>().FirstOrDefault();
                UpdateDetail();
            };
            _tagList.AddToClassList("gas-tag-list");
            return _tagList;
        }

        private VisualElement BuildDetailPanel()
        {
            var detail = new VisualElement();
            detail.AddToClassList("gas-detail-panel");

            detail.Add(new Label("Tag 详情") { name = "tag-detail-title" });
            _idField = CreateReadOnlyField("ID");
            _nameField = CreateReadOnlyField("标签名");
            _descriptionField = CreateReadOnlyField("Tag 描述", true);

            detail.Add(_idField);
            detail.Add(_nameField);
            detail.Add(_descriptionField);
            return detail;
        }

        private static TextField CreateReadOnlyField(string label, bool multiline = false)
        {
            var field = new TextField(label)
            {
                isReadOnly = true,
                multiline = multiline
            };
            field.AddToClassList("gas-readonly-field");
            return field;
        }

        private void LoadTags()
        {
            _allTags.Clear();
            _selectedTag = null;

            var tagJsonPath = _context.ResolveProjectPath(_context.SettingAsset.PathOfJsonTag);
            if (!File.Exists(tagJsonPath))
            {
                ShowLoadError($"Tag JSON 文件未找到: {tagJsonPath}");
                return;
            }

            try
            {
                var json = File.ReadAllText(tagJsonPath);
                var tags = GasJsonReader.ReadTags(json) ?? System.Array.Empty<TagInEditor>();
                _allTags.AddRange(tags.Where(tag => tag != null).OrderBy(tag => tag.name));
                HideLoadError();
            }
            catch (System.Exception ex)
            {
                ShowLoadError($"Tag JSON 读取失败: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void ApplyFilter()
        {
            _visibleTags.Clear();

            var keyword = _searchField?.value;
            foreach (var tag in _allTags)
            {
                if (string.IsNullOrWhiteSpace(keyword) ||
                    tag.name != null && tag.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    tag.desc != null && tag.desc.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    tag.id.ToString().Contains(keyword))
                {
                    _visibleTags.Add(tag);
                }
            }

            _tagList?.Rebuild();
            _countLabel.text = $"{_visibleTags.Count}/{_allTags.Count}";
        }

        private void SelectFirstVisibleTag()
        {
            if (_tagList == null)
            {
                return;
            }

            if (_visibleTags.Count == 0)
            {
                _tagList.ClearSelection();
                _selectedTag = null;
                UpdateDetail();
                return;
            }

            _tagList.SetSelection(0);
        }

        private void UpdateDetail()
        {
            _idField.value = _selectedTag?.id.ToString() ?? string.Empty;
            _nameField.value = _selectedTag?.name ?? string.Empty;
            _descriptionField.value = _selectedTag?.desc ?? string.Empty;
        }

        private void ExportJson()
        {
            if (!CodeGenerator.TryGenerateGasConfigTables())
            {
                ShowLoadError("Tag JSON 导出失败，请查看 Console 中的 Luban 日志。");
                return;
            }

            Refresh();
        }

        private void ShowLoadError(string message)
        {
            _loadErrorBox.text = message;
            _loadErrorBox.style.display = DisplayStyle.Flex;
            Debug.LogWarning($"[EX-GAS] {message}");
        }

        private void HideLoadError()
        {
            _loadErrorBox.text = string.Empty;
            _loadErrorBox.style.display = DisplayStyle.None;
        }

        private static int GetTagDepth(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
            {
                return 0;
            }

            var depth = 0;
            foreach (var ch in tagName)
            {
                if (ch == '.')
                {
                    depth++;
                }
            }

            return depth;
        }
    }
}
