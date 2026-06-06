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
    internal sealed class GASCenterEffectPage : IGASCenterPage
    {
        private readonly List<int> _ids = new();
        private readonly Dictionary<EffectEditComponent, Toggle> _componentToggles = new();
        private readonly Dictionary<EffectEditComponent, VisualElement> _componentSections = new();
        private readonly Dictionary<EffectEditComponent, TextField> _listFields = new();
        private readonly Dictionary<EffectEditComponent, TagRequirementFields> _requirementFields = new();

        private GASCenterContext _context;
        private GASCenterExcelTable _table;
        private PopupField<int> _idField;
        private TextField _newIdField;
        private TextField _nameField;
        private TextField _descriptionField;
        private TextField _modifiersField;
        private TextField _grantedAbilityField;
        private TextField _periodEffectsField;
        private TextField _stackingOverflowEffectsField;
        private IntegerField _durationTimeField;
        private EnumField _durationUnitField;
        private Toggle _durationResetField;
        private IntegerField _periodTimeField;
        private Toggle _periodFirstTriggerField;
        private IntegerField _stackingCodeField;
        private EnumField _stackingTypeField;
        private IntegerField _stackingLimitField;
        private EnumField _stackingDurationRefreshField;
        private EnumField _stackingPeriodResetField;
        private EnumField _stackingExpirationField;
        private Toggle _stackingDenyOverflowField;
        private Toggle _stackingClearOnOverflowField;
        private Label _statusLabel;
        private int _selectedId;

        public string Id => "effect";
        public string Title => "GameplayEffect 效果";
        public string Description => "编辑 GameplayEffect Excel 配置，覆盖旧版 Effect 页面的核心增删、保存、导出链路。";

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
            toolbar.Add(new ToolbarButton(() => _context.RevealPath(_context.SettingAsset.PathOfExcelEffect, "Effect Excel 文件")) { text = "打开 Excel" });
            toolbar.Add(new ToolbarButton(() => _context.RevealPath(_context.SettingAsset.PathOfJsonEffect, "Effect Json 文件")) { text = "打开 Json" });
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
            _idField = new PopupField<int>("当前 Effect", _ids, 0);
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
            idRow.Add(new Button(AddNewEffect) { text = "添加" });
            idRow.Add(new Button(DeleteSelection) { text = "删除" });
            root.Add(idRow);

            _nameField = new TextField("名字");
            _descriptionField = new TextField("描述") { multiline = true };
            root.Add(_nameField);
            root.Add(_descriptionField);

            root.Add(new Label("组件配置") { name = "effect-component-title" });
            root.Add(CreateChoiceHint("可选 Tag", GasXlsxChoice.Tags()));
            root.Add(CreateChoiceHint("可选 Cue", GasXlsxChoice.Cues()));
            root.Add(CreateChoiceHint("可选 Ability", GasXlsxChoice.Abilities()));
            root.Add(CreateChoiceHint("可选 AttributeSet", GasXlsxChoice.AttrSets()));

            AddIdListComponent(root, EffectEditComponent.AssetTags, "AssetTags 描述 Tag", "AssetTags");
            AddIdListComponent(root, EffectEditComponent.GrantedTags, "GrantedTags 授予 Tag", "GrantedTags");
            AddRequirementComponent(root, EffectEditComponent.ApplicationRequiredTags, "ApplicationRequiredTags");
            AddRequirementComponent(root, EffectEditComponent.OngoingRequiredTags, "OngoingRequiredTags");
            AddRequirementComponent(root, EffectEditComponent.RemoveGameplayEffectsWithTags, "RemoveGameplayEffectsWithTags");
            AddRequirementComponent(root, EffectEditComponent.ImmunityTags, "ImmunityTags");
            AddDurationComponent(root);
            AddPeriodComponent(root);
            AddTextProtocolComponent(root, EffectEditComponent.Modifiers, "Modifiers", "格式: AttrSet;Attribute;Magnitude;Operation | ...", field => _modifiersField = field);
            AddIdListComponent(root, EffectEditComponent.CueOnApply, "CueOnApply", "CueOnApply");
            AddIdListComponent(root, EffectEditComponent.CueOnTick, "CueOnTick", "CueOnTick");
            AddIdListComponent(root, EffectEditComponent.CueOnAdd, "CueOnAdd", "CueOnAdd");
            AddIdListComponent(root, EffectEditComponent.CueOnRemove, "CueOnRemove", "CueOnRemove");
            AddIdListComponent(root, EffectEditComponent.CueOnActivate, "CueOnActivate", "CueOnActivate");
            AddIdListComponent(root, EffectEditComponent.CueOnDeactivate, "CueOnDeactivate", "CueOnDeactivate");
            AddTextProtocolComponent(root, EffectEditComponent.GrantedAbility, "GrantedAbility", "格式: AbilityID;Level;ActivationPolicy;DeactivationPolicy;RemovePolicy | ...", field => _grantedAbilityField = field);
            AddStackingComponent(root);

            _statusLabel = new Label();
            _statusLabel.AddToClassList("gas-status-label");
            root.Add(_statusLabel);
            return root;
        }

        private void AddIdListComponent(VisualElement root, EffectEditComponent component, string title, string fieldLabel)
        {
            AddComponent(root, component, title, section =>
            {
                var field = new TextField(fieldLabel);
                field.tooltip = "使用 ; 分隔多个 ID。";
                _listFields[component] = field;
                section.Add(field);
            });
        }

        private void AddRequirementComponent(VisualElement root, EffectEditComponent component, string title)
        {
            AddComponent(root, component, title, section =>
            {
                var fields = new TagRequirementFields
                {
                    All = new TextField("All"),
                    Any = new TextField("Any"),
                    None = new TextField("None")
                };
                fields.All.tooltip = "使用 ; 分隔多个 Tag ID。";
                fields.Any.tooltip = fields.All.tooltip;
                fields.None.tooltip = fields.All.tooltip;
                _requirementFields[component] = fields;
                section.Add(fields.All);
                section.Add(fields.Any);
                section.Add(fields.None);
            });
        }

        private void AddDurationComponent(VisualElement root)
        {
            AddComponent(root, EffectEditComponent.Duration, "Duration 持续时间", section =>
            {
                _durationUnitField = new EnumField("Unit", GAS.Runtime.TimeUnit.Frame);
                _durationTimeField = new IntegerField("Time");
                _durationResetField = new Toggle("ResetStartTimeWhenActivated");
                section.Add(_durationUnitField);
                section.Add(_durationTimeField);
                section.Add(_durationResetField);
            });
        }

        private void AddPeriodComponent(VisualElement root)
        {
            AddComponent(root, EffectEditComponent.Period, "Period 周期", section =>
            {
                _periodTimeField = new IntegerField("Time");
                _periodEffectsField = new TextField("Effects");
                _periodEffectsField.tooltip = "使用 ; 分隔多个 Effect ID。";
                _periodFirstTriggerField = new Toggle("FirstTrigger");
                section.Add(_periodTimeField);
                section.Add(_periodEffectsField);
                section.Add(_periodFirstTriggerField);
            });
        }

        private void AddTextProtocolComponent(
            VisualElement root,
            EffectEditComponent component,
            string title,
            string tooltip,
            System.Action<TextField> assign)
        {
            AddComponent(root, component, title, section =>
            {
                var field = new TextField(title) { multiline = true };
                field.tooltip = tooltip;
                assign(field);
                section.Add(field);
            });
        }

        private void AddStackingComponent(VisualElement root)
        {
            AddComponent(root, EffectEditComponent.Stacking, "Stacking 堆叠", section =>
            {
                _stackingCodeField = new IntegerField("Code");
                _stackingTypeField = new EnumField("StackingType", EffectStackType.AggregateBySource);
                _stackingLimitField = new IntegerField("LimitCount");
                _stackingDurationRefreshField = new EnumField("DurationRefreshPolicy", EffectDurationRefreshPolicy.NeverRefresh);
                _stackingPeriodResetField = new EnumField("PeriodResetPolicy", EffectPeriodResetPolicy.NeverRefresh);
                _stackingExpirationField = new EnumField("ExpirationPolicy", EffectExpirationPolicy.ClearEntireStack);
                _stackingDenyOverflowField = new Toggle("DenyOverflowApplication");
                _stackingClearOnOverflowField = new Toggle("ClearStackOnOverflow");
                _stackingOverflowEffectsField = new TextField("OverflowEffects");
                _stackingOverflowEffectsField.tooltip = "使用 ; 分隔多个 Effect ID。";
                section.Add(_stackingCodeField);
                section.Add(_stackingTypeField);
                section.Add(_stackingLimitField);
                section.Add(_stackingDurationRefreshField);
                section.Add(_stackingPeriodResetField);
                section.Add(_stackingExpirationField);
                section.Add(_stackingDenyOverflowField);
                section.Add(_stackingClearOnOverflowField);
                section.Add(_stackingOverflowEffectsField);
            });
        }

        private void AddComponent(VisualElement root, EffectEditComponent component, string label, System.Action<VisualElement> build)
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
            var excelPath = _context.ResolveProjectPath(_context.SettingAsset.PathOfExcelEffect);
            if (!File.Exists(excelPath))
            {
                SetStatus($"Effect Excel 文件未找到: {excelPath}");
                _table = null;
                _ids.Clear();
                return;
            }

            _table = new GASCenterExcelTable(excelPath);
            _table.Load();
            _ids.Clear();
            _ids.AddRange(_table.Ids.OrderBy(id => id));
            SetStatus($"已加载 Effect: {_ids.Count} 条");
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

            LoadListField(EffectEditComponent.AssetTags, row, "AssetTags");
            LoadListField(EffectEditComponent.GrantedTags, row, "GrantedTags");
            LoadRequirement(row, EffectEditComponent.ApplicationRequiredTags, "ApplicationRequiredTags");
            LoadRequirement(row, EffectEditComponent.OngoingRequiredTags, "OngoingRequiredTags");
            LoadRequirement(row, EffectEditComponent.RemoveGameplayEffectsWithTags, "RemoveGameplayEffectsWithTags");
            LoadRequirement(row, EffectEditComponent.ImmunityTags, "ImmunityTags");
            LoadListField(EffectEditComponent.CueOnApply, row, "CueOnApply");
            LoadListField(EffectEditComponent.CueOnTick, row, "CueOnTick");
            LoadListField(EffectEditComponent.CueOnAdd, row, "CueOnAdd");
            LoadListField(EffectEditComponent.CueOnRemove, row, "CueOnRemove");
            LoadListField(EffectEditComponent.CueOnActivate, row, "CueOnActivate");
            LoadListField(EffectEditComponent.CueOnDeactivate, row, "CueOnDeactivate");

            _durationUnitField.SetValueWithoutNotify((GAS.Runtime.TimeUnit)ReadInt(row, "Duration"));
            _durationTimeField.value = ReadOffsetInt(row, "Duration", 1);
            _durationResetField.value = ReadOffsetBool(row, "Duration", 2);
            SetComponentEnabled(EffectEditComponent.Duration, _durationTimeField.value != 0 || ReadString(row, "Duration").Length > 0);

            _periodTimeField.value = ReadInt(row, "Period");
            _periodEffectsField.value = FormatIdList(ReadOffsetString(row, "Period", 1));
            _periodFirstTriggerField.value = ReadOffsetBool(row, "Period", 2);
            SetComponentEnabled(EffectEditComponent.Period, _periodTimeField.value != 0 || !string.IsNullOrWhiteSpace(_periodEffectsField.value));

            _modifiersField.value = ReadString(row, "Modifiers");
            SetComponentEnabled(EffectEditComponent.Modifiers, !string.IsNullOrWhiteSpace(_modifiersField.value));
            _grantedAbilityField.value = ReadString(row, "GrantedAbility");
            SetComponentEnabled(EffectEditComponent.GrantedAbility, !string.IsNullOrWhiteSpace(_grantedAbilityField.value));

            _stackingCodeField.value = ReadInt(row, "Stacking");
            _stackingTypeField.SetValueWithoutNotify((EffectStackType)ReadOffsetInt(row, "Stacking", 1));
            _stackingLimitField.value = ReadOffsetInt(row, "Stacking", 2);
            _stackingDurationRefreshField.SetValueWithoutNotify((EffectDurationRefreshPolicy)ReadOffsetInt(row, "Stacking", 3));
            _stackingPeriodResetField.SetValueWithoutNotify((EffectPeriodResetPolicy)ReadOffsetInt(row, "Stacking", 4));
            _stackingExpirationField.SetValueWithoutNotify((EffectExpirationPolicy)ReadOffsetInt(row, "Stacking", 5));
            _stackingDenyOverflowField.value = ReadOffsetBool(row, "Stacking", 6);
            _stackingClearOnOverflowField.value = ReadOffsetBool(row, "Stacking", 7);
            _stackingOverflowEffectsField.value = FormatIdList(ReadOffsetString(row, "Stacking", 8));
            SetComponentEnabled(EffectEditComponent.Stacking, _stackingCodeField.value != 0);
        }

        private void SaveSelection()
        {
            if (_table == null || _selectedId <= 0)
            {
                SetStatus("当前没有可保存的 Effect。");
                return;
            }

            if (!TryReadAllListFields(out var listValues))
            {
                return;
            }

            var values = new Dictionary<string, object>
            {
                ["ID"] = _selectedId,
                ["Name"] = _nameField.value,
                ["Desc"] = _descriptionField.value,
                ["AssetTags"] = EnabledList(EffectEditComponent.AssetTags, listValues),
                ["GrantedTags"] = EnabledList(EffectEditComponent.GrantedTags, listValues),
                ["CueOnApply"] = EnabledList(EffectEditComponent.CueOnApply, listValues),
                ["CueOnTick"] = EnabledList(EffectEditComponent.CueOnTick, listValues),
                ["CueOnAdd"] = EnabledList(EffectEditComponent.CueOnAdd, listValues),
                ["CueOnRemove"] = EnabledList(EffectEditComponent.CueOnRemove, listValues),
                ["CueOnActivate"] = EnabledList(EffectEditComponent.CueOnActivate, listValues),
                ["CueOnDeactivate"] = EnabledList(EffectEditComponent.CueOnDeactivate, listValues),
                ["Modifiers"] = IsComponentEnabled(EffectEditComponent.Modifiers) ? _modifiersField.value : string.Empty,
                ["GrantedAbility"] = IsComponentEnabled(EffectEditComponent.GrantedAbility) ? _grantedAbilityField.value : string.Empty,
                ["Duration"] = IsComponentEnabled(EffectEditComponent.Duration) ? (int)(GAS.Runtime.TimeUnit)_durationUnitField.value : null,
                ["Period"] = IsComponentEnabled(EffectEditComponent.Period) ? _periodTimeField.value : null,
                ["Stacking"] = IsComponentEnabled(EffectEditComponent.Stacking) ? _stackingCodeField.value : null
            };

            foreach (var field in EditorEffectHelper.TagRequirementProtocolFields)
            {
                values[field.ExcelHeader] = IsComponentEnabled(field.Component)
                    ? EncodeRequirement(field.Component, listValues)
                    : string.Empty;
            }

            var rawValues = BuildRawColumns(listValues);
            _table.SaveRowWithRawColumns(_selectedId, values, rawValues);
            GasXlsxChoice.LoadChoices();
            Refresh();
            SetStatus($"已保存 Effect ID: {_selectedId}");
        }

        private void AddNewEffect()
        {
            if (_table == null)
            {
                SetStatus("Effect Excel 未加载，无法添加。");
                return;
            }

            if (!int.TryParse(_newIdField.value, out var id) || id <= 0)
            {
                SetStatus("新 Effect ID 必须是正整数。");
                return;
            }

            if (_table.Ids.Contains(id))
            {
                SetStatus($"Effect ID 已存在: {id}");
                return;
            }

            _selectedId = id;
            _table.SaveRow(id, new Dictionary<string, object>
            {
                ["ID"] = id,
                ["Name"] = string.Empty,
                ["Desc"] = string.Empty
            });
            _newIdField.value = string.Empty;
            Refresh();
            SetStatus($"已添加 Effect ID: {id}");
        }

        private void DeleteSelection()
        {
            if (_table == null || !_table.Ids.Contains(_selectedId))
            {
                SetStatus("当前 Effect 不存在，无法删除。");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认删除", $"你确定要删除 Effect ID: {_selectedId} 吗？", "是", "否"))
            {
                return;
            }

            var deletedId = _selectedId;
            if (!_table.DeleteRow(_selectedId))
            {
                SetStatus($"Effect ID 删除失败: {deletedId}");
                return;
            }

            Refresh();
            SetStatus($"已删除 Effect ID: {deletedId}");
        }

        private void ExportJson()
        {
            if (!CodeGenerator.TryGenerateGasConfigTables())
            {
                SetStatus("Effect Json 导出失败，请查看 Console 中的 Luban 日志。");
                return;
            }

            _context.WarmCaches();
            Refresh();
            SetStatus("Effect Json 已导出并刷新。");
        }

        private void LoadListField(EffectEditComponent component, IReadOnlyDictionary<string, object> row, string key)
        {
            var text = FormatIdList(ReadString(row, key));
            _listFields[component].value = text;
            SetComponentEnabled(component, !string.IsNullOrWhiteSpace(text));
        }

        private void LoadRequirement(IReadOnlyDictionary<string, object> row, EffectEditComponent component, string key)
        {
            var requirement = EditorEffectHelper.ParseTagRequirementCell(ReadString(row, key));
            var fields = _requirementFields[component];
            fields.All.value = string.Join(";", requirement.All);
            fields.Any.value = string.Join(";", requirement.Any);
            fields.None.value = string.Join(";", requirement.None);
            SetComponentEnabled(component, requirement.HasAnyValue());
        }

        private bool TryReadAllListFields(out Dictionary<string, string> values)
        {
            values = new Dictionary<string, string>();
            foreach (var pair in _listFields)
            {
                if (!TryNormalizeIdList(pair.Value.value, pair.Key.ToString(), out var normalized))
                {
                    return false;
                }

                values[pair.Key.ToString()] = normalized;
            }

            foreach (var pair in _requirementFields)
            {
                if (!TryNormalizeIdList(pair.Value.All.value, $"{pair.Key}.All", out var all) ||
                    !TryNormalizeIdList(pair.Value.Any.value, $"{pair.Key}.Any", out var any) ||
                    !TryNormalizeIdList(pair.Value.None.value, $"{pair.Key}.None", out var none))
                {
                    return false;
                }

                values[$"{pair.Key}.All"] = all;
                values[$"{pair.Key}.Any"] = any;
                values[$"{pair.Key}.None"] = none;
            }

            if (!TryNormalizeIdList(_periodEffectsField.value, "Period.Effects", out var periodEffects) ||
                !TryNormalizeIdList(_stackingOverflowEffectsField.value, "Stacking.OverflowEffects", out var overflowEffects))
            {
                return false;
            }

            values["Period.Effects"] = periodEffects;
            values["Stacking.OverflowEffects"] = overflowEffects;
            return true;
        }

        private string EnabledList(EffectEditComponent component, IReadOnlyDictionary<string, string> values)
        {
            return IsComponentEnabled(component) && values.TryGetValue(component.ToString(), out var value)
                ? value
                : string.Empty;
        }

        private string EncodeRequirement(EffectEditComponent component, IReadOnlyDictionary<string, string> values)
        {
            return $"{EncodeRequirementPart(values[$"{component}.All"])};{EncodeRequirementPart(values[$"{component}.Any"])};{EncodeRequirementPart(values[$"{component}.None"])}";
        }

        private static string EncodeRequirementPart(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "0" : value.Replace(';', ',');
        }

        private Dictionary<int, object> BuildRawColumns(IReadOnlyDictionary<string, string> listValues)
        {
            var rawValues = new Dictionary<int, object>();
            AddRaw("Duration", 1, IsComponentEnabled(EffectEditComponent.Duration) ? _durationTimeField.value : null);
            AddRaw("Duration", 2, IsComponentEnabled(EffectEditComponent.Duration) ? _durationResetField.value : null);
            AddRaw("Period", 1, IsComponentEnabled(EffectEditComponent.Period) ? listValues["Period.Effects"] : null);
            AddRaw("Period", 2, IsComponentEnabled(EffectEditComponent.Period) ? _periodFirstTriggerField.value.ToString() : null);
            AddRaw("Stacking", 1, IsComponentEnabled(EffectEditComponent.Stacking) ? (int)(EffectStackType)_stackingTypeField.value : null);
            AddRaw("Stacking", 2, IsComponentEnabled(EffectEditComponent.Stacking) ? _stackingLimitField.value : null);
            AddRaw("Stacking", 3, IsComponentEnabled(EffectEditComponent.Stacking) ? (int)(EffectDurationRefreshPolicy)_stackingDurationRefreshField.value : null);
            AddRaw("Stacking", 4, IsComponentEnabled(EffectEditComponent.Stacking) ? (int)(EffectPeriodResetPolicy)_stackingPeriodResetField.value : null);
            AddRaw("Stacking", 5, IsComponentEnabled(EffectEditComponent.Stacking) ? (int)(EffectExpirationPolicy)_stackingExpirationField.value : null);
            AddRaw("Stacking", 6, IsComponentEnabled(EffectEditComponent.Stacking) ? _stackingDenyOverflowField.value.ToString() : null);
            AddRaw("Stacking", 7, IsComponentEnabled(EffectEditComponent.Stacking) ? _stackingClearOnOverflowField.value.ToString() : null);
            AddRaw("Stacking", 8, IsComponentEnabled(EffectEditComponent.Stacking) ? listValues["Stacking.OverflowEffects"] : null);
            return rawValues;

            void AddRaw(string header, int offset, object value)
            {
                if (_table != null && _table.HeaderMap.TryGetValue(header, out var baseColumn))
                {
                    rawValues[baseColumn + offset] = value;
                }
            }
        }

        private void ClearEditor()
        {
            _nameField.value = string.Empty;
            _descriptionField.value = string.Empty;
            foreach (var field in _listFields.Values)
            {
                field.value = string.Empty;
            }

            foreach (var fields in _requirementFields.Values)
            {
                fields.All.value = string.Empty;
                fields.Any.value = string.Empty;
                fields.None.value = string.Empty;
            }

            _modifiersField.value = string.Empty;
            _grantedAbilityField.value = string.Empty;
            _periodEffectsField.value = string.Empty;
            _stackingOverflowEffectsField.value = string.Empty;
            _durationTimeField.value = 0;
            _periodTimeField.value = 0;
            _stackingCodeField.value = 0;
            foreach (var component in _componentToggles.Keys.ToList())
            {
                SetComponentEnabled(component, false);
            }
        }

        private void SetComponentEnabled(EffectEditComponent component, bool enabled)
        {
            if (_componentToggles.TryGetValue(component, out var toggle))
            {
                toggle.SetValueWithoutNotify(enabled);
            }

            SetComponentSectionEnabled(component, enabled);
        }

        private void SetComponentSectionEnabled(EffectEditComponent component, bool enabled)
        {
            if (_componentSections.TryGetValue(component, out var section))
            {
                section.SetEnabled(enabled);
                section.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private bool IsComponentEnabled(EffectEditComponent component)
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

        private string ReadOffsetString(IReadOnlyDictionary<string, object> row, string header, int offset)
        {
            if (_table == null || !_table.HeaderMap.TryGetValue(header, out var baseColumn))
            {
                return string.Empty;
            }

            return row.TryGetValue($"__column_{baseColumn + offset}", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        }

        private int ReadOffsetInt(IReadOnlyDictionary<string, object> row, string header, int offset)
        {
            return int.TryParse(ReadOffsetString(row, header, offset), out var value) ? value : 0;
        }

        private bool ReadOffsetBool(IReadOnlyDictionary<string, object> row, string header, int offset)
        {
            return bool.TryParse(ReadOffsetString(row, header, offset), out var value) && value;
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

        private sealed class TagRequirementFields
        {
            public TextField All;
            public TextField Any;
            public TextField None;
        }
    }
}
