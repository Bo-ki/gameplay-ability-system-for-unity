using System.Collections.Generic;
using System.IO;
using System.Linq;
using GAS.General;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GAS.Editor
{
    internal sealed class GASCenterAscPage : IGASCenterPage
    {
        private readonly List<int> _ids = new();

        private GASCenterContext _context;
        private GASCenterExcelTable _table;
        private PopupField<int> _idField;
        private TextField _newIdField;
        private TextField _nameField;
        private TextField _descriptionField;
        private IntegerField _levelField;
        private TextField _tagsField;
        private TextField _attrSetsField;
        private TextField _abilitiesField;
        private Label _statusLabel;
        private int _selectedId;

        public string Id => "asc";
        public string Title => "ASC 预设";
        public string Description => "编辑 ASC Excel 配置，覆盖旧版 ASC 页面的增删、保存、导出链路。";

        public VisualElement CreateView(GASCenterContext context)
        {
            _context = context;

            var root = new VisualElement();
            root.AddToClassList("gas-page");
            root.Add(BuildToolbar());
            root.Add(BuildEditor());
            return root;
        }

        public void Refresh()
        {
            LoadTable();
            RebuildIdChoices();
            SelectFirstIfNeeded();
            LoadSelection();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("gas-page-toolbar");
            toolbar.Add(new ToolbarButton(() => _context.RevealPath(_context.SettingAsset.PathOfExcelAsc, "ASC Excel 文件")) { text = "打开 Excel" });
            toolbar.Add(new ToolbarButton(() => _context.RevealPath(_context.SettingAsset.PathOfJsonAsc, "ASC Json 文件")) { text = "打开 Json" });
            toolbar.Add(new ToolbarButton(ExportJson) { text = "导出更新 Json 表" });
            toolbar.Add(new ToolbarButton(Refresh) { text = "刷新" });
            toolbar.Add(new ToolbarButton(SaveSelection) { text = "保存" });
            return toolbar;
        }

        private VisualElement BuildEditor()
        {
            var root = new VisualElement();
            root.AddToClassList("gas-form");

            var idRow = new VisualElement();
            idRow.AddToClassList("gas-inline-row");

            _idField = new PopupField<int>("当前 ASC", _ids, 0);
            _idField.AddToClassList("gas-id-popup");
            _idField.RegisterValueChangedCallback(evt =>
            {
                _selectedId = evt.newValue;
                LoadSelection();
            });
            idRow.Add(_idField);

            _newIdField = new TextField("新 ID");
            _newIdField.AddToClassList("gas-new-id-field");
            idRow.Add(_newIdField);
            idRow.Add(new Button(AddNewAsc) { text = "添加" });
            idRow.Add(new Button(DeleteSelection) { text = "删除" });
            root.Add(idRow);

            _nameField = new TextField("名字");
            _descriptionField = new TextField("描述") { multiline = true };
            _levelField = new IntegerField("等级");
            _tagsField = new TextField("标签 ID 列表");
            _attrSetsField = new TextField("属性集 ID 列表");
            _abilitiesField = new TextField("技能 ID 列表");

            root.Add(_nameField);
            root.Add(_descriptionField);
            root.Add(_levelField);
            root.Add(CreateChoiceHint("可选 Tag", GasXlsxChoice.Tags()));
            root.Add(_tagsField);
            root.Add(CreateChoiceHint("可选 AttributeSet", GasXlsxChoice.AttrSets()));
            root.Add(_attrSetsField);
            root.Add(CreateChoiceHint("可选 Ability", GasXlsxChoice.Abilities()));
            root.Add(_abilitiesField);

            _statusLabel = new Label();
            _statusLabel.AddToClassList("gas-status-label");
            root.Add(_statusLabel);

            return root;
        }

        private static HelpBox CreateChoiceHint(string title, IReadOnlyList<GasChoiceItem> choices)
        {
            var text = choices == null || choices.Count == 0
                ? $"{title}: 当前无可用选项。"
                : $"{title}: {string.Join("  ", choices.Take(12).Select(choice => choice.ToString()))}";
            if (choices != null && choices.Count > 12)
            {
                text += $"  ... 共 {choices.Count} 项";
            }

            return new HelpBox(text, HelpBoxMessageType.Info);
        }

        private void LoadTable()
        {
            var excelPath = _context.ResolveProjectPath(_context.SettingAsset.PathOfExcelAsc);
            if (!File.Exists(excelPath))
            {
                SetStatus($"ASC Excel 文件未找到: {excelPath}");
                _table = null;
                _ids.Clear();
                return;
            }

            _table = new GASCenterExcelTable(excelPath);
            _table.Load();
            _ids.Clear();
            _ids.AddRange(_table.Ids.OrderBy(id => id));
            SetStatus($"已加载 ASC: {_ids.Count} 条");
        }

        private void RebuildIdChoices()
        {
            if (_idField == null)
            {
                return;
            }

            _idField.choices = _ids;
            _idField.SetEnabled(_ids.Count > 0);
            if (_ids.Count > 0 && !_ids.Contains(_selectedId))
            {
                _selectedId = _ids[0];
            }

            if (_ids.Count > 0)
            {
                _idField.SetValueWithoutNotify(_selectedId);
            }
        }

        private void SelectFirstIfNeeded()
        {
            if (_ids.Count > 0 && !_ids.Contains(_selectedId))
            {
                _selectedId = _ids[0];
            }
        }

        private void LoadSelection()
        {
            if (_table == null || _ids.Count == 0)
            {
                ClearEditor();
                return;
            }

            var row = _table.GetRowOrEmpty(_selectedId);
            _nameField.value = ReadString(row, "Name");
            _descriptionField.value = ReadString(row, "Desc");
            _levelField.value = ReadInt(row, "Level");
            _tagsField.value = FormatIdList(ReadString(row, "Tag"));
            _attrSetsField.value = FormatIdList(ReadString(row, "AttrSet"));
            _abilitiesField.value = FormatIdList(ReadString(row, "Ability"));
        }

        private void SaveSelection()
        {
            if (_table == null || _selectedId <= 0)
            {
                SetStatus("当前没有可保存的 ASC。");
                return;
            }

            if (!TryNormalizeIdList(_tagsField.value, "标签 ID 列表", out var tags))
            {
                return;
            }

            if (!TryNormalizeIdList(_attrSetsField.value, "属性集 ID 列表", out var attrSets))
            {
                return;
            }

            if (!TryNormalizeIdList(_abilitiesField.value, "技能 ID 列表", out var abilities))
            {
                return;
            }

            _table.SaveRow(_selectedId, new Dictionary<string, object>
            {
                ["ID"] = _selectedId,
                ["Name"] = _nameField.value,
                ["Desc"] = _descriptionField.value,
                ["Level"] = _levelField.value,
                ["Tag"] = tags,
                ["AttrSet"] = attrSets,
                ["Ability"] = abilities
            });
            GasXlsxChoice.LoadChoices();
            Refresh();
            SetStatus($"已保存 ASC ID: {_selectedId}");
        }

        private void AddNewAsc()
        {
            if (_table == null)
            {
                SetStatus("ASC Excel 未加载，无法添加。");
                return;
            }

            if (!int.TryParse(_newIdField.value, out var id) || id <= 0)
            {
                SetStatus("新 ASC ID 必须是正整数。");
                return;
            }

            if (_table.Ids.Contains(id))
            {
                SetStatus($"ASC ID 已存在: {id}");
                return;
            }

            _selectedId = id;
            _table.SaveRow(id, new Dictionary<string, object>
            {
                ["ID"] = id,
                ["Name"] = string.Empty,
                ["Desc"] = string.Empty,
                ["Level"] = 0,
                ["Tag"] = string.Empty,
                ["AttrSet"] = string.Empty,
                ["Ability"] = string.Empty
            });
            _newIdField.value = string.Empty;
            Refresh();
            SetStatus($"已添加 ASC ID: {id}");
        }

        private void DeleteSelection()
        {
            if (_table == null || !_table.Ids.Contains(_selectedId))
            {
                SetStatus("当前 ASC 不存在，无法删除。");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认删除", $"你确定要删除 ASC ID: {_selectedId} 吗？", "是", "否"))
            {
                return;
            }

            var deletedId = _selectedId;
            if (!_table.DeleteRow(_selectedId))
            {
                SetStatus($"ASC ID 删除失败: {deletedId}");
                return;
            }

            Refresh();
            SetStatus($"已删除 ASC ID: {deletedId}");
        }

        private void ExportJson()
        {
            if (!CodeGenerator.TryGenerateGasConfigTables())
            {
                SetStatus("ASC Json 导出失败，请查看 Console 中的 Luban 日志。");
                return;
            }

            _context.WarmCaches();
            Refresh();
            SetStatus("ASC Json 已导出并刷新。");
        }

        private void ClearEditor()
        {
            _nameField.value = string.Empty;
            _descriptionField.value = string.Empty;
            _levelField.value = 0;
            _tagsField.value = string.Empty;
            _attrSetsField.value = string.Empty;
            _abilitiesField.value = string.Empty;
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = message;
            }

            _context.Notify(message);
        }

        private static string ReadString(IReadOnlyDictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        }

        private static int ReadInt(IReadOnlyDictionary<string, object> row, string key)
        {
            return int.TryParse(ReadString(row, key), out var value) ? value : 0;
        }

        private static string FormatIdList(string raw)
        {
            return string.Join(";", GASCenterParseHelper.ParseIntListLoose(raw));
        }

        private static string NormalizeIdList(string raw)
        {
            return string.Join(";", GASCenterParseHelper.ParseIntListLoose(raw));
        }

        private bool TryNormalizeIdList(string raw, string label, out string normalized)
        {
            if (GASCenterParseHelper.TryNormalizeIntList(raw, out normalized, out var error))
            {
                return true;
            }

            SetStatus($"{label} 格式错误: {error}");
            return false;
        }
    }
}
