using GAS.General;
using UnityEditor;
using UnityEngine.Serialization;

namespace GAS.Editor
{
    [SingletonFilePath(GASConstDefine.GAS_BASE_SETTING_PATH)]
    public class GASSettingAsset : ScriptableSingleton<GASSettingAsset>
    {
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
        public string PathOfJsonAttr => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_ATTR}.json";
        public string PathOfExcelAttr => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_ATTR}.xlsx";

        public string PathOfJsonAttrSet => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_ATTR_SET}.json";
        public string PathOfExcelAttrSet => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_ATTR_SET}.xlsx";

        public string PathOfJsonEffect => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_EFFECT}.json";
        public string PathOfExcelEffect => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_EFFECT}.xlsx";

        public string PathOfJsonAbility => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_ABILITY}.json";
        public string PathOfExcelAbility => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_ABILITY}.xlsx";

        public string PathOfJsonCue => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_CUE}.json";
        public string PathOfExcelCue => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_CUE}.xlsx";

        public string PathOfJsonAsc => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_ASC}.json";
        public string PathOfExcelAsc => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_ASC}.xlsx";

        public string PathOfJsonTimelineAbility => $"{TableOutpuPath}/{GASConstDefine.JSON_FILE_NAME_OF_TIMELINE_ABILITY}.json";
        public string PathOfExcelTimelineAbility => $"{ConfigProjectPath}/Datas/{GASConstDefine.EXCEL_FILE_NAME_OF_TIMELINE_ABILITY}.xlsx";

        #endregion

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
