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
                    $"Runtime v1 代码输出路径: {_context.SettingAsset.CodeGeneratePath}\n" +
                    $"规范化 Row 输出: {_context.SettingAsset.CodeGeneratePath}/Editor/LubanNormalizedRows.gen.cs\n" +
                    $"生成清单: {_context.SettingAsset.CodeGeneratePath}/GasCodeGen.manifest.json\n" +
                    $"验证报告: {_context.SettingAsset.CodeGeneratePath}/GasCodeGenValidationReport.md\n\n" +
                    $"表 Json 输出路径: {_context.SettingAsset.TableOutpuPath}\n" +
                    $"表 C# 输出路径: {_context.SettingAsset.TableClassCodeOutpuPath}"
            };
            summary.AddToClassList("gas-path-summary");
            _root.Add(summary);
        }

        private void AddGenerationButtons()
        {
            AddSectionTitle("Runtime v1 生成");

            var row = new Toolbar();
            row.AddToClassList("gas-button-row");
            row.Add(new ToolbarButton(SaveSettings) { text = "保存设置" });
            row.Add(new ToolbarButton(ExportJsonTables) { text = "导出 Json 表" });
            row.Add(new ToolbarButton(CodeGenerator.GenerateAllCode) { text = "生成 Runtime v1" });
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
