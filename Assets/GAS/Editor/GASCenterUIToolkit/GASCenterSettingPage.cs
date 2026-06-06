using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GAS.Editor
{
    internal sealed class GASCenterSettingPage : IGASCenterPage
    {
        private GASCenterContext _context;
        private VisualElement _root;

        public string Id => "setting";
        public string Title => "Setting 基本设置";
        public string Description => "配置 GAS 代码生成、Luban 表工程和导出路径。";

        public VisualElement CreateView(GASCenterContext context)
        {
            _context = context;
            _root = new ScrollView();
            _root.AddToClassList("gas-page");
            BuildContent();

            return _root;
        }

        public void Refresh()
        {
            _context?.SettingSerializedObject.Update();
        }

        private void BuildContent()
        {
            _root.Clear();

            AddSectionTitle("生成路径");
            AddProperty("CodeGeneratePath", "代码生成路径");
            AddProperty("CodeGenerateRootNamespace", "生成代码命名空间");
            AddProperty("CodeGenerateRowTypePrefixesToStrip", "Row 类型前缀剥离");

            AddSectionTitle("Luban 表配置");
            AddProperty("TableOutpuPath", "表导出路径");
            AddProperty("TableClassCodeOutpuPath", "表 class 生成路径");
            AddProperty("ConfigProjectPath", "配置表工程路径");

            AddPathSummary();
            AddGenerationButtons();
            AddAdvanceControls();
        }

        private void AddSectionTitle(string title)
        {
            var label = new Label(title);
            label.AddToClassList("gas-section-title");
            _root.Add(label);
        }

        private void AddProperty(string propertyName, string label)
        {
            var property = _context.SettingSerializedObject.FindProperty(propertyName);
            if (property == null)
            {
                _root.Add(new HelpBox($"找不到设置字段: {propertyName}", HelpBoxMessageType.Warning));
                return;
            }

            var field = new PropertyField(property, label);
            field.Bind(_context.SettingSerializedObject);
            field.AddToClassList("gas-property-field");
            _root.Add(field);
        }

        private void AddPathSummary()
        {
            AddSectionTitle("文件路径一览");

            var summary = new TextField
            {
                multiline = true,
                isReadOnly = true,
                value =
                    $"Launcher 脚本路径: {_context.SettingAsset.PathOfCodeLauncher}\n\n" +
                    $"Tag 配置 Json 路径: {_context.SettingAsset.PathOfJsonTag}\n" +
                    $"Tag 配置 Excel 路径: {_context.SettingAsset.PathOfExcelTag}\n" +
                    $"Tag 脚本路径: {_context.SettingAsset.PathOfCodeTag}\n\n" +
                    $"属性配置 Json 路径: {_context.SettingAsset.PathOfJsonAttr}\n" +
                    $"属性配置 Excel 路径: {_context.SettingAsset.PathOfExcelAttr}\n" +
                    $"属性脚本路径: {_context.SettingAsset.PathOfCodeAttr}\n\n" +
                    $"属性集配置 Json 路径: {_context.SettingAsset.PathOfJsonAttrSet}\n" +
                    $"属性集配置 Excel 路径: {_context.SettingAsset.PathOfExcelAttrSet}\n" +
                    $"属性集脚本路径: {_context.SettingAsset.PathOfCodeAttrSet}\n\n" +
                    $"Effect 配置 Json 路径: {_context.SettingAsset.PathOfJsonEffect}\n" +
                    $"Effect 配置 Excel 路径: {_context.SettingAsset.PathOfExcelEffect}\n\n" +
                    $"Ability 配置 Json 路径: {_context.SettingAsset.PathOfJsonAbility}\n" +
                    $"Ability 配置 Excel 路径: {_context.SettingAsset.PathOfExcelAbility}\n" +
                    $"Ability 脚本路径: {_context.SettingAsset.PathOfCodeAbility}\n\n" +
                    $"Cue 配置 Json 路径: {_context.SettingAsset.PathOfJsonCue}\n" +
                    $"Cue 配置 Excel 路径: {_context.SettingAsset.PathOfExcelCue}\n" +
                    $"Cue 脚本路径: {_context.SettingAsset.PathOfCodeCue}"
            };
            summary.AddToClassList("gas-path-summary");
            _root.Add(summary);
        }

        private void AddGenerationButtons()
        {
            AddSectionTitle("生成脚本");

            var row = new Toolbar();
            row.AddToClassList("gas-button-row");
            row.Add(new ToolbarButton(SaveSettings) { text = "保存设置" });
            row.Add(new ToolbarButton(ExportJsonTables) { text = "导出 Json 表" });
            row.Add(new ToolbarButton(CodeGenerator.GenerateAllCode) { text = "一键生成所有" });
            row.Add(new ToolbarButton(CodeGenerator.GenerateTagCode) { text = "Tag 脚本" });
            row.Add(new ToolbarButton(CodeGenerator.GenerateAttrCode) { text = "属性脚本" });
            row.Add(new ToolbarButton(CodeGenerator.GenerateAttrSetCode) { text = "属性集脚本" });
            row.Add(new ToolbarButton(CodeGenerator.GenerateCueCode) { text = "Cue 脚本" });
            row.Add(new ToolbarButton(CodeGenerator.GenerateAbilityCode) { text = "Ability 脚本" });
            _root.Add(row);
        }

        private void AddAdvanceControls()
        {
            AddSectionTitle("高级");

            var status = GASSettingAsset.EnableHotKeys ? "当前快捷键状态: 启用" : "当前快捷键状态: 禁用";
            var helpBox = new HelpBox(status, HelpBoxMessageType.Info);
            _root.Add(helpBox);

            var row = new Toolbar();
            row.AddToClassList("gas-button-row");
            row.Add(new ToolbarButton(GASSettingAsset.ToggleScriptDefineSymbol_EX_GAS_ENABLE_HOT_KEYS)
            {
                text = GASSettingAsset.EnableHotKeys ? "禁用快捷键" : "开启快捷键"
            });
            _root.Add(row);
        }

        private void SaveSettings()
        {
            _context.SaveSettings();
            _context.ReloadSettings();
            BuildContent();
            Refresh();
        }

        private void ExportJsonTables()
        {
            if (CodeGenerator.TryGenerateGasConfigTables())
            {
                _context.Notify("Json 表已导出。");
                return;
            }

            _context.Notify("Json 表导出失败，请查看 Console 中的 Luban 日志。");
        }
    }
}
