using System.IO;
using GAS.General;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace GAS.Editor
{
    [SingletonFilePath(GASConstDefine.GAS_BASE_SETTING_PATH)]
    public class GASSettingAsset : ScriptableSingleton<GASSettingAsset>
    {
        private static GASSettingAsset _setting;
        private const string DEFAULT_TABLE_OUTPUT_PATH = "Assets/DataGenerated/Luban/Json/GAS";
        private const string DEFAULT_TABLE_CODE_OUTPUT_PATH = "Assets/DataGenerated/Luban/CSharp";
        private const string DEFAULT_CONFIG_PROJECT_PATH = "EX_GAS_Config/ProjectConfigTable/exgas_config";

        public string CodeGeneratePath = "Assets/GAS/Generated/CodeGen";
        public string CodeGenerateRootNamespace = "GAS.Runtime.Generated";
        public string CodeGenerateRowTypePrefixesToStrip = "";
        public string TableOutpuPath = DEFAULT_TABLE_OUTPUT_PATH;
        public string TableClassCodeOutpuPath = DEFAULT_TABLE_CODE_OUTPUT_PATH;

        [FormerlySerializedAs("TableExportToolPath")]
        public string ConfigProjectPath = DEFAULT_CONFIG_PROJECT_PATH;

        #region 生成文件路径一览

        public string PathOfJsonTag => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_TAG}.json";
        public string PathOfExcelTag => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_TAG}.xlsx";
        public string PathOfCodeTag => $"{CodeGeneratePath}/{GASConstDefine.CODE_FILE_NAME_OF_TAG}.cs";

        public string PathOfJsonAttr => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_ATTR}.json";
        public string PathOfExcelAttr => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_ATTR}.xlsx";
        public string PathOfCodeAttr => $"{CodeGeneratePath}/{GASConstDefine.CODE_FILE_NAME_OF_ATTR}.cs";

        public string PathOfJsonAttrSet => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_ATTR_SET}.json";
        public string PathOfExcelAttrSet => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_ATTR_SET}.xlsx";
        public string PathOfCodeAttrSet => $"{CodeGeneratePath}/{GASConstDefine.CODE_FILE_NAME_OF_ATTR_SET}.cs";

        public string PathOfJsonEffect => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_EFFECT}.json";
        public string PathOfExcelEffect => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_EFFECT}.xlsx";

        public string PathOfJsonAbility => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_ABILITY}.json";
        public string PathOfExcelAbility => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_ABILITY}.xlsx";
        public string PathOfCodeAbility => $"{CodeGeneratePath}/{GASConstDefine.CODE_FILE_NAME_OF_ABILITY}.cs";

        public string PathOfJsonCue => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_CUE}.json";
        public string PathOfExcelCue => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_CUE}.xlsx";
        public string PathOfCodeCue => $"{CodeGeneratePath}/{GASConstDefine.CODE_FILE_NAME_OF_CUE}.cs";

        public string PathOfJsonAsc => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_ASC}.json";
        public string PathOfExcelAsc => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_ASC}.xlsx";

        public string PathOfCodeLubanExtesion => $"{CodeGeneratePath}/{GASConstDefine.CODE_FILE_NAME_OF_LUBAN}.cs";
        public string PathOfCodeLauncher => $"{CodeGeneratePath}/{GASConstDefine.CODE_FILE_NAME_OF_LAUNCHER}.cs";

        public string PathOfJsonTimelineAbility => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_TIMELINE_ABILITY}.json";
        public string PathOfExcelTimelineAbility => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_TIMELINE_ABILITY}.xlsx";

        public string ShowGenPaths
        {
            get
            {
                var content =
                    $"Launcher脚本路径: {PathOfCodeLauncher}\n\n" +
                    $"Tag配置Json路径: {PathOfJsonTag}\n" +
                    $"Tag配置Excel路径: {PathOfExcelTag}\n" +
                    $"Tag脚本路径: {PathOfCodeTag}\n\n" +
                    $"属性配置Json路径: {PathOfJsonAttr}\n" +
                    $"属性配置Excel路径: {PathOfExcelAttr}\n" +
                    $"属性脚本路径: {PathOfCodeAttr}\n\n" +
                    $"属性集配置Json路径: {PathOfJsonAttrSet}\n" +
                    $"属性集配置Excel路径: {PathOfExcelAttrSet}\n" +
                    $"属性集脚本路径: {PathOfCodeAttrSet}\n\n" +
                    $"Effect配置Json路径: {PathOfJsonEffect}\n" +
                    $"Effect配置Excel路径: {PathOfExcelEffect}\n\n" +
                    $"Ability配置Json路径: {PathOfJsonAbility}\n" +
                    $"Ability配置Excel路径: {PathOfExcelAbility}\n" +
                    $"Ability脚本路径: {PathOfCodeAbility}\n\n" +
                    $"Cue配置Json路径: {PathOfJsonCue}\n" +
                    $"Cue配置Excel路径: {PathOfExcelCue}\n" +
                    $"Cue脚本路径: {PathOfCodeCue}\n\n";
                return $"<color=white>{content}</color>";
            }
        }

        #endregion

        private static GASSettingAsset Setting
        {
            get
            {
                if (_setting == null) _setting = LoadOrCreate();
                return _setting;
            }
        }

        public static string Version =>
            $"<size=15><b><color=white>EX-GAS Version: {GASConstDefine.GAS_VERSION}</color></b></size>";

        public static string CodeGenPath => Setting.CodeGeneratePath;

        public string FullGenBatPath()
        {
            var projectRootPath = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            var fullBatPath = Path.Combine(projectRootPath, $"{Instance.ConfigProjectPath}/{GASConstDefine.LUBAN_GEN_BAT_TILE_NAME}");
            return fullBatPath;
        }

        public void OutputJsonTables() => CodeGenerator.GenerateGasConfigTables();

        public bool IsShowOutputButton()
        {
            var projectRootPath = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            var fullOutputPath = Path.Combine(projectRootPath, Instance.TableOutpuPath);
            return !IsGenBatNotExist() && Directory.Exists(fullOutputPath);
        }

        public bool IsGenBatNotExist()
        {
            var fullBatPath = FullGenBatPath();
            return !File.Exists(fullBatPath);
        }

        private void SaveAsset()
        {
            if (Instance == this) return;
            UpdateAsset(this);
            Save();
        }

        public const string EX_GAS_ENABLE_HOT_KEYS = "EX_GAS_ENABLE_HOT_KEYS";

#if EX_GAS_ENABLE_HOT_KEYS
        public const bool EnableHotKeys = true;
#else
        public const bool EnableHotKeys = false;
#endif

        public static void ToggleScriptDefineSymbol_EX_GAS_ENABLE_HOT_KEYS()
        {
            if (EditorUtility.DisplayDialog("Ex-GAS",
                    "切换快捷键状态\n将在你的项目中切换\"EX_GAS_ENABLE_HOT_KEYS\"宏定义\n\n这会重新编译你的代码, 之后你可能需要手动保存你的项目(请留意ProjectSettings.asset的变化).",
                    "确定", "取消"))
            {
#pragma warning disable 162
                if (EnableHotKeys)
                    ScriptingDefineSymbolsHelper.Remove(EX_GAS_ENABLE_HOT_KEYS);
                else
                    ScriptingDefineSymbolsHelper.Add(EX_GAS_ENABLE_HOT_KEYS);
#pragma warning restore 162
            }
        }
    }
}
