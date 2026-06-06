using System.Collections.Generic;
using System.IO;
using System.Linq;
using GAS.General;
using GAS.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GAS.Editor
{
    internal sealed class GASCenterCuePage : IGASCenterPage
    {
        private readonly List<int> _ids = new();

        private GASCenterContext _context;
        private GASCenterExcelTable _table;
        private PopupField<int> _idField;
        private PopupField<string> _typeField;
        private TextField _newIdField;
        private TextField _nameField;
        private TextField _descriptionField;
        private TextField _requiredTagsField;
        private TextField _immunityTagsField;
        private VisualElement _paramHost;
        private Label _statusLabel;
        private int _selectedId;
        private XParam _parameter;
        private bool _parameterLoadFailed;

        public string Id => "cue";
        public string Title => "GameplayCue 演出提示";
        public string Description => "编辑 GameplayCue Excel 配置，覆盖旧版 Cue 页面的增删、保存、导出链路。";

        public VisualElement CreateView(GASCenterContext context)
        {
            _context = context;

            var root = new ScrollView();
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
            toolbar.Add(new ToolbarButton(() => _context.RevealPath(_context.SettingAsset.PathOfExcelCue, "Cue Excel 文件")) { text = "打开 Excel" });
            toolbar.Add(new ToolbarButton(() => _context.RevealPath(_context.SettingAsset.PathOfJsonCue, "Cue Json 文件")) { text = "打开 Json" });
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

            _idField = new PopupField<int>("当前 Cue", _ids, 0);
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
            idRow.Add(new Button(AddNewCue) { text = "添加" });
            idRow.Add(new Button(DeleteSelection) { text = "删除" });
            root.Add(idRow);

            _nameField = new TextField("名字");
            _descriptionField = new TextField("描述") { multiline = true };
            _requiredTagsField = new TextField("播放时需求的 Tag");
            _immunityTagsField = new TextField("播放时免疫的 Tag");
            root.Add(_nameField);
            root.Add(_descriptionField);
            root.Add(CreateChoiceHint("可选 Tag", GasXlsxChoice.Tags()));
            root.Add(_requiredTagsField);
            root.Add(_immunityTagsField);

            var cueTypeChoices = EditorCueHelper.GetCachedCueTypeNames().ToList();
            _typeField = new PopupField<string>("Cue 类型", cueTypeChoices, cueTypeChoices.Count > 0 ? cueTypeChoices[0] : string.Empty);
            _typeField.SetEnabled(cueTypeChoices.Count > 0);
            if (cueTypeChoices.Count == 0)
            {
                root.Add(new HelpBox("当前没有可用 Cue 类型。请先生成或编译 Cue 类型后再编辑 Cue 配置。", HelpBoxMessageType.Warning));
            }

            _typeField.RegisterValueChangedCallback(evt =>
            {
                CreateParameter(evt.newValue, null);
                DrawParameter();
            });
            root.Add(_typeField);

            _paramHost = new VisualElement();
            _paramHost.AddToClassList("gas-param-host");
            root.Add(_paramHost);

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
            var excelPath = _context.ResolveProjectPath(_context.SettingAsset.PathOfExcelCue);
            if (!File.Exists(excelPath))
            {
                SetStatus($"Cue Excel 文件未找到: {excelPath}");
                _table = null;
                _ids.Clear();
                return;
            }

            _table = new GASCenterExcelTable(excelPath);
            _table.Load();
            _ids.Clear();
            _ids.AddRange(_table.Ids.OrderBy(id => id));
            SetStatus($"已加载 Cue: {_ids.Count} 条");
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
            _requiredTagsField.value = FormatIdList(ReadString(row, "RequiredTag"));
            _immunityTagsField.value = FormatIdList(ReadString(row, "ImmunityTag"));

            var type = ReadString(row, "CueLogic");
            SetTypeField(type);
            CreateParameter(type, ReadParameterCells(row));
            DrawParameter();
        }

        private void SaveSelection()
        {
            if (_table == null || _selectedId <= 0)
            {
                SetStatus("当前没有可保存的 Cue。");
                return;
            }

            if (_typeField.choices.Count == 0 || string.IsNullOrWhiteSpace(_typeField.value))
            {
                SetStatus("当前没有可用 Cue 类型，保存已取消。");
                return;
            }

            if (_parameterLoadFailed)
            {
                SetStatus("Cue 参数加载失败，保存已取消，避免覆盖已有参数列。");
                return;
            }

            if (!TryNormalizeIdList(_requiredTagsField.value, "播放时需求的 Tag", out var requiredTags))
            {
                return;
            }

            if (!TryNormalizeIdList(_immunityTagsField.value, "播放时免疫的 Tag", out var immunityTags))
            {
                return;
            }

            var values = new Dictionary<string, object>
            {
                ["ID"] = _selectedId,
                ["Name"] = _nameField.value,
                ["Desc"] = _descriptionField.value,
                ["RequiredTag"] = requiredTags,
                ["ImmunityTag"] = immunityTags,
                ["CueLogic"] = _typeField.value
            };

            _table.SaveRowWithRawColumns(_selectedId, values, BuildParameterRawColumns(_parameter?.EncodeExcelData() ?? new List<object>()));
            GasXlsxChoice.LoadChoices();
            Refresh();
            SetStatus($"已保存 Cue ID: {_selectedId}");
        }

        private void AddNewCue()
        {
            if (_table == null)
            {
                SetStatus("Cue Excel 未加载，无法添加。");
                return;
            }

            if (!int.TryParse(_newIdField.value, out var id) || id <= 0)
            {
                SetStatus("新 Cue ID 必须是正整数。");
                return;
            }

            if (_table.Ids.Contains(id))
            {
                SetStatus($"Cue ID 已存在: {id}");
                return;
            }

            _selectedId = id;
            var type = _typeField.choices.Count > 0 ? _typeField.choices[0] : string.Empty;
            _table.SaveRow(id, new Dictionary<string, object>
            {
                ["ID"] = id,
                ["Name"] = string.Empty,
                ["Desc"] = string.Empty,
                ["RequiredTag"] = string.Empty,
                ["ImmunityTag"] = string.Empty,
                ["CueLogic"] = type
            });
            _newIdField.value = string.Empty;
            Refresh();
            SetStatus($"已添加 Cue ID: {id}");
        }

        private void DeleteSelection()
        {
            if (_table == null || !_table.Ids.Contains(_selectedId))
            {
                SetStatus("当前 Cue 不存在，无法删除。");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认删除", $"你确定要删除 Cue ID: {_selectedId} 吗？", "是", "否"))
            {
                return;
            }

            var deletedId = _selectedId;
            if (!_table.DeleteRow(_selectedId))
            {
                SetStatus($"Cue ID 删除失败: {deletedId}");
                return;
            }

            Refresh();
            SetStatus($"已删除 Cue ID: {deletedId}");
        }

        private void ExportJson()
        {
            if (!CodeGenerator.TryGenerateGasConfigTables())
            {
                SetStatus("Cue Json 导出失败，请查看 Console 中的 Luban 日志。");
                return;
            }

            _context.WarmCaches();
            Refresh();
            SetStatus("Cue Json 已导出并刷新。");
        }

        private void SetTypeField(string type)
        {
            if (string.IsNullOrWhiteSpace(type) || !_typeField.choices.Contains(type))
            {
                type = _typeField.choices.Count > 0 ? _typeField.choices[0] : string.Empty;
            }

            if (string.IsNullOrEmpty(type))
            {
                _typeField.SetValueWithoutNotify(default);
                return;
            }

            _typeField.SetValueWithoutNotify(type);
        }

        private void CreateParameter(string type, List<object> rawData)
        {
            try
            {
                _parameter = string.IsNullOrWhiteSpace(type) ? null : EditorCueHelper.CreateCueParameter(type, rawData);
                _parameterLoadFailed = false;
            }
            catch (System.Exception ex)
            {
                _parameter = null;
                _parameterLoadFailed = true;
                SetStatus($"Cue 参数创建失败: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void DrawParameter()
        {
            _paramHost.Clear();
            if (_parameter == null)
            {
                _paramHost.Add(new HelpBox("当前 Cue 类型没有可编辑参数。", HelpBoxMessageType.Info));
                return;
            }

            _paramHost.Add(XParamVisualElementDrawer.Create("Cue 参数", _parameter, () => { }));
        }

        private List<object> ReadParameterCells(IReadOnlyDictionary<string, object> row)
        {
            var result = new List<object>();
            if (_table == null || !_table.HeaderMap.TryGetValue("CueLogic", out var baseColumn))
            {
                return result;
            }

            for (var i = 1; i <= 50; i++)
            {
                var columnKey = $"__column_{baseColumn + i}";
                result.Add(row.TryGetValue(columnKey, out var value) ? value : null);
            }

            return result;
        }

        private Dictionary<int, object> BuildParameterRawColumns(IReadOnlyList<object> values)
        {
            var rawValues = new Dictionary<int, object>();
            if (_table == null || !_table.HeaderMap.TryGetValue("CueLogic", out var baseColumn))
            {
                return rawValues;
            }

            for (var i = 0; i < 50; i++)
            {
                rawValues[baseColumn + 1 + i] = i < values.Count ? values[i] : null;
            }

            return rawValues;
        }

        private void ClearEditor()
        {
            _nameField.value = string.Empty;
            _descriptionField.value = string.Empty;
            _requiredTagsField.value = string.Empty;
            _immunityTagsField.value = string.Empty;
            _parameter = null;
            _parameterLoadFailed = false;
            _paramHost?.Clear();
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
