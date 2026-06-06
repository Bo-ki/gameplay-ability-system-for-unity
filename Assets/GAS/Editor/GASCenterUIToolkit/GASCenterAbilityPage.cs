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
    internal sealed class GASCenterAbilityPage : IGASCenterPage
    {
        private readonly List<int> _ids = new();
        private readonly Dictionary<AbilityEditComponent, Toggle> _componentToggles = new();
        private readonly Dictionary<AbilityEditComponent, VisualElement> _componentSections = new();

        private GASCenterContext _context;
        private GASCenterExcelTable _table;
        private PopupField<int> _idField;
        private PopupField<string> _executionTypeField;
        private TextField _newIdField;
        private TextField _nameField;
        private TextField _descriptionField;
        private IntegerField _costField;
        private IntegerField _cooldownEffectField;
        private IntegerField _cooldownField;
        private TextField _assetTagsField;
        private TextField _cancelAbilityWithTagsField;
        private TextField _blockAbilityWithTagsField;
        private TextField _activationOwnedTagsField;
        private TextField _activationRequiredTagsField;
        private TextField _activationBlockedTagsField;
        private VisualElement _paramHost;
        private Label _statusLabel;
        private int _selectedId;
        private XParam _parameter;
        private bool _parameterLoadFailed;

        public string Id => "ability";
        public string Title => "GameplayAbility 技能";
        public string Description => "编辑 GameplayAbility Excel 配置，覆盖旧版 Ability 页面的增删、保存、导出链路。";

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
            toolbar.Add(new ToolbarButton(() => _context.RevealPath(_context.SettingAsset.PathOfExcelAbility, "Ability Excel 文件")) { text = "打开 Excel" });
            toolbar.Add(new ToolbarButton(() => _context.RevealPath(_context.SettingAsset.PathOfJsonAbility, "Ability Json 文件")) { text = "打开 Json" });
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

            _idField = new PopupField<int>("当前 Ability", _ids, 0);
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
            idRow.Add(new Button(AddNewAbility) { text = "添加" });
            idRow.Add(new Button(DeleteSelection) { text = "删除" });
            root.Add(idRow);

            _nameField = new TextField("名字");
            _descriptionField = new TextField("描述") { multiline = true };
            root.Add(_nameField);
            root.Add(_descriptionField);

            root.Add(BuildExecutionSection());
            root.Add(BuildComponentSection());

            _statusLabel = new Label();
            _statusLabel.AddToClassList("gas-status-label");
            root.Add(_statusLabel);

            return root;
        }

        private VisualElement BuildExecutionSection()
        {
            var root = new VisualElement();
            root.Add(new Label("执行配置") { name = "ability-execution-title" });

            var choices = EditorAbilityHelper.GetAbilityExecutionTypeNames().ToList();
            _executionTypeField = new PopupField<string>("执行配置类型", choices, choices.Count > 0 ? choices[0] : string.Empty);
            _executionTypeField.SetEnabled(choices.Count > 0);
            _executionTypeField.RegisterValueChangedCallback(evt =>
            {
                CreateParameter(evt.newValue, null);
                DrawParameter();
            });
            root.Add(_executionTypeField);

            if (choices.Count == 0)
            {
                root.Add(new HelpBox("当前没有可用 AbilityExecution 类型。", HelpBoxMessageType.Warning));
            }

            _paramHost = new VisualElement();
            _paramHost.AddToClassList("gas-param-host");
            root.Add(_paramHost);
            return root;
        }

        private VisualElement BuildComponentSection()
        {
            var root = new VisualElement();
            root.Add(new Label("组件配置") { name = "ability-component-title" });
            root.Add(CreateChoiceHint("可选 Tag", GasXlsxChoice.Tags()));
            root.Add(CreateChoiceHint("可选 Effect", GasXlsxChoice.Effects()));

            _componentToggles.Clear();
            _componentSections.Clear();
            AddComponent(root, AbilityEditComponent.Cost, "Cost 消耗", section =>
            {
                _costField = new IntegerField("Cost");
                section.Add(_costField);
            });
            AddComponent(root, AbilityEditComponent.Cooldown, "Cooldown 冷却", section =>
            {
                _cooldownEffectField = new IntegerField("CdEffect");
                _cooldownField = new IntegerField("Cd");
                section.Add(_cooldownEffectField);
                section.Add(_cooldownField);
            });
            AddComponent(root, AbilityEditComponent.AssetTags, "AssetTags 描述 Tag", section =>
                _assetTagsField = AddIdListField(section, "AssetTags"));
            AddComponent(root, AbilityEditComponent.CancelAbilityWithTags, "CancelAbilityWithTags", section =>
                _cancelAbilityWithTagsField = AddIdListField(section, "CancelAbilityWithTags"));
            AddComponent(root, AbilityEditComponent.BlockAbilityWithTags, "BlockAbilityWithTags", section =>
                _blockAbilityWithTagsField = AddIdListField(section, "BlockAbilityWithTags"));
            AddComponent(root, AbilityEditComponent.ActivationOwnedTags, "ActivationOwnedTags", section =>
                _activationOwnedTagsField = AddIdListField(section, "ActivationOwnedTags"));
            AddComponent(root, AbilityEditComponent.ActivationRequiredTags, "ActivationRequiredTags", section =>
                _activationRequiredTagsField = AddIdListField(section, "ActivationRequiredTags"));
            AddComponent(root, AbilityEditComponent.ActivationBlockedTags, "ActivationBlockedTags", section =>
                _activationBlockedTagsField = AddIdListField(section, "ActivationBlockedTags"));

            return root;
        }

        private void AddComponent(VisualElement root, AbilityEditComponent component, string label, System.Action<VisualElement> build)
        {
            var foldout = new Foldout { text = label, value = false };
            var toggle = new Toggle("启用");
            toggle.RegisterValueChangedCallback(evt =>
            {
                foldout.value = evt.newValue;
                SetComponentSectionEnabled(component, evt.newValue);
            });
            foldout.Add(toggle);

            var section = new VisualElement();
            section.AddToClassList("gas-param-host");
            build(section);
            foldout.Add(section);

            _componentToggles[component] = toggle;
            _componentSections[component] = section;
            root.Add(foldout);
        }

        private static TextField AddIdListField(VisualElement root, string label)
        {
            var field = new TextField(label);
            field.tooltip = "使用 ; 分隔多个 ID。";
            root.Add(field);
            return field;
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
            var excelPath = _context.ResolveProjectPath(_context.SettingAsset.PathOfExcelAbility);
            if (!File.Exists(excelPath))
            {
                SetStatus($"Ability Excel 文件未找到: {excelPath}");
                _table = null;
                _ids.Clear();
                return;
            }

            _table = new GASCenterExcelTable(excelPath);
            _table.Load();
            _ids.Clear();
            _ids.AddRange(_table.Ids.OrderBy(id => id));
            SetStatus($"已加载 Ability: {_ids.Count} 条");
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

            _costField.value = ReadInt(row, "Cost");
            _cooldownEffectField.value = ReadInt(row, "CdEffect");
            _cooldownField.value = ReadInt(row, "Cd");
            _assetTagsField.value = FormatIdList(ReadString(row, "AssetTags"));
            _cancelAbilityWithTagsField.value = FormatIdList(ReadString(row, "CancelAbilityWithTags"));
            _blockAbilityWithTagsField.value = FormatIdList(ReadString(row, "BlockAbilityWithTags"));
            _activationOwnedTagsField.value = FormatIdList(ReadString(row, "ActivationOwnedTags"));
            _activationRequiredTagsField.value = FormatIdList(ReadString(row, "ActivationRequiredTags"));
            _activationBlockedTagsField.value = FormatIdList(ReadString(row, "ActivationBlockedTags"));

            SetComponentEnabled(AbilityEditComponent.Cost, _costField.value > 0);
            SetComponentEnabled(AbilityEditComponent.Cooldown, _cooldownEffectField.value > 0 || _cooldownField.value > 0);
            SetComponentEnabled(AbilityEditComponent.AssetTags, !string.IsNullOrWhiteSpace(_assetTagsField.value));
            SetComponentEnabled(AbilityEditComponent.CancelAbilityWithTags, !string.IsNullOrWhiteSpace(_cancelAbilityWithTagsField.value));
            SetComponentEnabled(AbilityEditComponent.BlockAbilityWithTags, !string.IsNullOrWhiteSpace(_blockAbilityWithTagsField.value));
            SetComponentEnabled(AbilityEditComponent.ActivationOwnedTags, !string.IsNullOrWhiteSpace(_activationOwnedTagsField.value));
            SetComponentEnabled(AbilityEditComponent.ActivationRequiredTags, !string.IsNullOrWhiteSpace(_activationRequiredTagsField.value));
            SetComponentEnabled(AbilityEditComponent.ActivationBlockedTags, !string.IsNullOrWhiteSpace(_activationBlockedTagsField.value));

            var type = ReadString(row, "AbilityExecution");
            SetExecutionTypeField(type);
            CreateParameter(type, ReadParameterCells(row));
            DrawParameter();
        }

        private void SaveSelection()
        {
            if (_table == null || _selectedId <= 0)
            {
                SetStatus("当前没有可保存的 Ability。");
                return;
            }

            if (_executionTypeField.choices.Count == 0 || string.IsNullOrWhiteSpace(_executionTypeField.value))
            {
                SetStatus("当前没有可用 AbilityExecution 类型，保存已取消。");
                return;
            }

            if (_parameterLoadFailed)
            {
                SetStatus("Ability 参数加载失败，保存已取消，避免覆盖已有参数列。");
                return;
            }

            if (!TryNormalizeIdList(_assetTagsField.value, "AssetTags", out var assetTags) ||
                !TryNormalizeIdList(_cancelAbilityWithTagsField.value, "CancelAbilityWithTags", out var cancelAbilityWithTags) ||
                !TryNormalizeIdList(_blockAbilityWithTagsField.value, "BlockAbilityWithTags", out var blockAbilityWithTags) ||
                !TryNormalizeIdList(_activationOwnedTagsField.value, "ActivationOwnedTags", out var activationOwnedTags) ||
                !TryNormalizeIdList(_activationRequiredTagsField.value, "ActivationRequiredTags", out var activationRequiredTags) ||
                !TryNormalizeIdList(_activationBlockedTagsField.value, "ActivationBlockedTags", out var activationBlockedTags))
            {
                return;
            }

            var values = new Dictionary<string, object>
            {
                ["ID"] = _selectedId,
                ["Name"] = _nameField.value,
                ["Desc"] = _descriptionField.value,
                ["Cost"] = IsComponentEnabled(AbilityEditComponent.Cost) ? _costField.value : string.Empty,
                ["CdEffect"] = IsComponentEnabled(AbilityEditComponent.Cooldown) ? _cooldownEffectField.value : string.Empty,
                ["Cd"] = IsComponentEnabled(AbilityEditComponent.Cooldown) ? _cooldownField.value : string.Empty,
                ["AssetTags"] = IsComponentEnabled(AbilityEditComponent.AssetTags) ? assetTags : string.Empty,
                ["CancelAbilityWithTags"] = IsComponentEnabled(AbilityEditComponent.CancelAbilityWithTags) ? cancelAbilityWithTags : string.Empty,
                ["BlockAbilityWithTags"] = IsComponentEnabled(AbilityEditComponent.BlockAbilityWithTags) ? blockAbilityWithTags : string.Empty,
                ["ActivationOwnedTags"] = IsComponentEnabled(AbilityEditComponent.ActivationOwnedTags) ? activationOwnedTags : string.Empty,
                ["ActivationRequiredTags"] = IsComponentEnabled(AbilityEditComponent.ActivationRequiredTags) ? activationRequiredTags : string.Empty,
                ["ActivationBlockedTags"] = IsComponentEnabled(AbilityEditComponent.ActivationBlockedTags) ? activationBlockedTags : string.Empty,
                ["AbilityExecution"] = _executionTypeField.value
            };

            _table.SaveRowWithRawColumns(_selectedId, values, BuildParameterRawColumns(_parameter?.EncodeExcelData() ?? new List<object>()));
            GasXlsxChoice.LoadChoices();
            Refresh();
            SetStatus($"已保存 Ability ID: {_selectedId}");
        }

        private void AddNewAbility()
        {
            if (_table == null)
            {
                SetStatus("Ability Excel 未加载，无法添加。");
                return;
            }

            if (!int.TryParse(_newIdField.value, out var id) || id <= 0)
            {
                SetStatus("新 Ability ID 必须是正整数。");
                return;
            }

            if (_table.Ids.Contains(id))
            {
                SetStatus($"Ability ID 已存在: {id}");
                return;
            }

            var type = _executionTypeField.choices.Count > 0 ? _executionTypeField.choices[0] : string.Empty;
            _selectedId = id;
            _table.SaveRow(id, new Dictionary<string, object>
            {
                ["ID"] = id,
                ["Name"] = string.Empty,
                ["Desc"] = string.Empty,
                ["Cost"] = string.Empty,
                ["CdEffect"] = string.Empty,
                ["Cd"] = string.Empty,
                ["AssetTags"] = string.Empty,
                ["CancelAbilityWithTags"] = string.Empty,
                ["BlockAbilityWithTags"] = string.Empty,
                ["ActivationOwnedTags"] = string.Empty,
                ["ActivationRequiredTags"] = string.Empty,
                ["ActivationBlockedTags"] = string.Empty,
                ["AbilityExecution"] = type
            });
            _newIdField.value = string.Empty;
            Refresh();
            SetStatus($"已添加 Ability ID: {id}");
        }

        private void DeleteSelection()
        {
            if (_table == null || !_table.Ids.Contains(_selectedId))
            {
                SetStatus("当前 Ability 不存在，无法删除。");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认删除", $"你确定要删除 Ability ID: {_selectedId} 吗？", "是", "否"))
            {
                return;
            }

            var deletedId = _selectedId;
            if (!_table.DeleteRow(_selectedId))
            {
                SetStatus($"Ability ID 删除失败: {deletedId}");
                return;
            }

            Refresh();
            SetStatus($"已删除 Ability ID: {deletedId}");
        }

        private void ExportJson()
        {
            if (!CodeGenerator.TryGenerateGasConfigTables())
            {
                SetStatus("Ability Json 导出失败，请查看 Console 中的 Luban 日志。");
                return;
            }

            _context.WarmCaches();
            Refresh();
            SetStatus("Ability Json 已导出并刷新。");
        }

        private void SetExecutionTypeField(string type)
        {
            if (string.IsNullOrWhiteSpace(type) || !_executionTypeField.choices.Contains(type))
            {
                type = _executionTypeField.choices.Count > 0 ? _executionTypeField.choices[0] : string.Empty;
            }

            if (string.IsNullOrEmpty(type))
            {
                _executionTypeField.SetValueWithoutNotify(default);
                return;
            }

            _executionTypeField.SetValueWithoutNotify(type);
        }

        private void CreateParameter(string type, List<object> rawData)
        {
            try
            {
                _parameter = string.IsNullOrWhiteSpace(type) ? null : EditorAbilityHelper.CreateAbilityParameter(type, rawData);
                _parameterLoadFailed = false;
            }
            catch (System.Exception ex)
            {
                _parameter = null;
                _parameterLoadFailed = true;
                SetStatus($"Ability 参数创建失败: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void DrawParameter()
        {
            _paramHost.Clear();
            if (_parameter == null)
            {
                _paramHost.Add(new HelpBox("当前 AbilityExecution 没有可编辑参数。", HelpBoxMessageType.Info));
                return;
            }

            _paramHost.Add(XParamVisualElementDrawer.Create("Ability 参数", _parameter, () => { }));
        }

        private List<object> ReadParameterCells(IReadOnlyDictionary<string, object> row)
        {
            var result = new List<object>();
            if (_table == null || !_table.HeaderMap.TryGetValue("AbilityExecution", out var baseColumn))
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
            if (_table == null || !_table.HeaderMap.TryGetValue("AbilityExecution", out var baseColumn))
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
            _costField.value = 0;
            _cooldownEffectField.value = 0;
            _cooldownField.value = 0;
            _assetTagsField.value = string.Empty;
            _cancelAbilityWithTagsField.value = string.Empty;
            _blockAbilityWithTagsField.value = string.Empty;
            _activationOwnedTagsField.value = string.Empty;
            _activationRequiredTagsField.value = string.Empty;
            _activationBlockedTagsField.value = string.Empty;
            foreach (var component in _componentToggles.Keys.ToList())
            {
                SetComponentEnabled(component, false);
            }

            _parameter = null;
            _parameterLoadFailed = false;
            _paramHost?.Clear();
        }

        private void SetComponentEnabled(AbilityEditComponent component, bool enabled)
        {
            if (_componentToggles.TryGetValue(component, out var toggle))
            {
                toggle.SetValueWithoutNotify(enabled);
            }

            SetComponentSectionEnabled(component, enabled);
        }

        private void SetComponentSectionEnabled(AbilityEditComponent component, bool enabled)
        {
            if (_componentSections.TryGetValue(component, out var section))
            {
                section.SetEnabled(enabled);
                section.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private bool IsComponentEnabled(AbilityEditComponent component)
        {
            return _componentToggles.TryGetValue(component, out var toggle) && toggle.value;
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
