using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GAS.Editor
{
    internal sealed class GASCenterJsonTablePage : IGASCenterPage
    {
        private readonly List<GasJsonTableRow> _allRows = new();
        private readonly List<GasJsonTableRow> _visibleRows = new();
        private readonly System.Func<GASSettingAsset, string> _jsonPathGetter;
        private readonly System.Func<GASSettingAsset, string> _excelPathGetter;
        private readonly string _emptyMessage;
        private readonly string _id;
        private readonly string _title;
        private readonly string _description;

        private GASCenterContext _context;
        private ListView _rowList;
        private TextField _searchField;
        private Label _countLabel;
        private HelpBox _loadErrorBox;
        private ScrollView _detailScroll;
        private GasJsonTableRow _selectedRow;

        public GASCenterJsonTablePage(
            string id,
            string title,
            string description,
            System.Func<GASSettingAsset, string> jsonPathGetter,
            System.Func<GASSettingAsset, string> excelPathGetter,
            string emptyMessage = null)
        {
            _id = id;
            _title = title;
            _description = description;
            _jsonPathGetter = jsonPathGetter;
            _excelPathGetter = excelPathGetter;
            _emptyMessage = emptyMessage ?? "当前 JSON 表为空。";
        }

        public string Id => _id;
        public string Title => _title;
        public string Description => _description;

        public VisualElement CreateView(GASCenterContext context)
        {
            _context = context;

            var root = new VisualElement();
            root.AddToClassList("gas-page");

            root.Add(BuildToolbar());

            _loadErrorBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            _loadErrorBox.style.display = DisplayStyle.None;
            root.Add(_loadErrorBox);

            var split = new TwoPaneSplitView(0, 360, TwoPaneSplitViewOrientation.Horizontal);
            split.AddToClassList("gas-table-split");
            split.Add(BuildRowList());
            split.Add(BuildDetailPanel());
            root.Add(split);

            return root;
        }

        public void Refresh()
        {
            LoadRows();
            ApplyFilter();
            SelectFirstVisibleRow();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("gas-page-toolbar");

            toolbar.Add(new ToolbarButton(() => _context.RevealPath(_excelPathGetter(_context.SettingAsset), $"{Title} Excel 文件")) { text = "打开 Excel" });
            toolbar.Add(new ToolbarButton(() => _context.RevealPath(_jsonPathGetter(_context.SettingAsset), $"{Title} Json 文件")) { text = "打开 Json" });
            toolbar.Add(new ToolbarButton(ExportJson) { text = "导出更新 Json 表" });
            toolbar.Add(new ToolbarButton(Refresh) { text = "刷新" });

            _searchField = new TextField
            {
                label = "搜索"
            };
            _searchField.AddToClassList("gas-search-field");
            _searchField.RegisterValueChangedCallback(_ => ApplyFilter());
            toolbar.Add(_searchField);

            _countLabel = new Label();
            _countLabel.AddToClassList("gas-count-label");
            toolbar.Add(_countLabel);

            return toolbar;
        }

        private VisualElement BuildRowList()
        {
            _rowList = new ListView
            {
                itemsSource = _visibleRows,
                fixedItemHeight = 25,
                selectionType = SelectionType.Single,
                makeItem = () =>
                {
                    var label = new Label();
                    label.AddToClassList("gas-table-list-item");
                    return label;
                },
                bindItem = (element, index) =>
                {
                    var label = (Label)element;
                    label.text = _visibleRows[index].DisplayName;
                }
            };

            _rowList.selectionChanged += selection =>
            {
                _selectedRow = selection.OfType<GasJsonTableRow>().FirstOrDefault();
                UpdateDetail();
            };
            _rowList.AddToClassList("gas-table-list");
            return _rowList;
        }

        private VisualElement BuildDetailPanel()
        {
            _detailScroll = new ScrollView();
            _detailScroll.AddToClassList("gas-detail-panel");
            return _detailScroll;
        }

        private void LoadRows()
        {
            _allRows.Clear();
            _selectedRow = null;

            var jsonPath = _context.ResolveProjectPath(_jsonPathGetter(_context.SettingAsset));
            if (!File.Exists(jsonPath))
            {
                ShowLoadError($"{Title} JSON 文件未找到: {jsonPath}");
                return;
            }

            try
            {
                var token = JToken.Parse(File.ReadAllText(jsonPath));
                var rows = ExtractRows(token);
                var index = 0;
                foreach (var row in rows)
                {
                    _allRows.Add(GasJsonTableRow.Create(index, row));
                    index++;
                }

                HideLoadError();
                if (_allRows.Count == 0)
                {
                    ShowLoadError(_emptyMessage);
                }
            }
            catch (System.Exception ex)
            {
                ShowLoadError($"{Title} JSON 读取失败: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static IEnumerable<JObject> ExtractRows(JToken token)
        {
            if (token is JArray array)
            {
                return array.OfType<JObject>();
            }

            if (token is JObject obj)
            {
                var nested = obj["items"] ?? obj["data"] ?? obj["list"];
                if (nested is JArray nestedArray)
                {
                    return nestedArray.OfType<JObject>();
                }

                return new[] { obj };
            }

            return System.Array.Empty<JObject>();
        }

        private void ApplyFilter()
        {
            _visibleRows.Clear();

            var keyword = _searchField?.value;
            foreach (var row in _allRows)
            {
                if (string.IsNullOrWhiteSpace(keyword) ||
                    row.SearchText.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _visibleRows.Add(row);
                }
            }

            _rowList?.Rebuild();
            _countLabel.text = $"{_visibleRows.Count}/{_allRows.Count}";
        }

        private void SelectFirstVisibleRow()
        {
            if (_rowList == null)
            {
                return;
            }

            if (_visibleRows.Count == 0)
            {
                _rowList.ClearSelection();
                _selectedRow = null;
                UpdateDetail();
                return;
            }

            _rowList.SetSelection(0);
        }

        private void UpdateDetail()
        {
            _detailScroll.Clear();

            if (_selectedRow == null)
            {
                _detailScroll.Add(new HelpBox("请选择左侧记录。", HelpBoxMessageType.Info));
                return;
            }

            var title = new Label(_selectedRow.DisplayName);
            title.AddToClassList("gas-detail-title");
            _detailScroll.Add(title);

            foreach (var property in _selectedRow.Token.Properties())
            {
                _detailScroll.Add(CreateValueElement(property.Name, property.Value));
            }
        }

        private static VisualElement CreateValueElement(string label, JToken value)
        {
            if (value is JObject obj)
            {
                var foldout = new Foldout
                {
                    text = label,
                    value = true
                };
                foldout.AddToClassList("gas-detail-foldout");
                foreach (var property in obj.Properties())
                {
                    foldout.Add(CreateValueElement(property.Name, property.Value));
                }

                return foldout;
            }

            if (value is JArray array)
            {
                var foldout = new Foldout
                {
                    text = $"{label} ({array.Count})",
                    value = array.Count <= 6
                };
                foldout.AddToClassList("gas-detail-foldout");

                for (var i = 0; i < array.Count; i++)
                {
                    foldout.Add(CreateValueElement($"[{i}]", array[i]));
                }

                return foldout;
            }

            var field = new TextField(label)
            {
                isReadOnly = true,
                value = FormatScalarValue(value)
            };
            field.AddToClassList("gas-readonly-field");
            return field;
        }

        private static string FormatScalarValue(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                return string.Empty;
            }

            return value.ToString();
        }

        private void ExportJson()
        {
            if (!CodeGenerator.TryGenerateGasConfigTables())
            {
                ShowLoadError($"{Title} JSON 导出失败，请查看 Console 中的 Luban 日志。");
                return;
            }

            _context.WarmCaches();
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

        private sealed class GasJsonTableRow
        {
            public JObject Token { get; private set; }
            public string DisplayName { get; private set; }
            public string SearchText { get; private set; }

            public static GasJsonTableRow Create(int index, JObject token)
            {
                var id = ReadFirst(token, "ID", "id", "Code", "code");
                var name = ReadFirst(token, "Name", "name");
                var label = ReadFirst(token, "Desc", "desc", "Description", "description", "Label", "label");
                var displayName = string.IsNullOrWhiteSpace(name) ? label : name;
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = $"Record {index + 1}";
                }

                if (!string.IsNullOrWhiteSpace(id))
                {
                    displayName = $"[{id}] {displayName}";
                }

                return new GasJsonTableRow
                {
                    Token = token,
                    DisplayName = displayName,
                    SearchText = token.ToString(Formatting.None)
                };
            }

            private static string ReadFirst(JObject token, params string[] names)
            {
                foreach (var name in names)
                {
                    var value = token[name]?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                return string.Empty;
            }
        }
    }
}
