namespace GAS.General
{
    public static class GASConstDefine
    {
        public const string GAS_VERSION = "2.0";
        public const string TITLE_SETTING = "设置";

        #region GASSettingAsset

        public const string GAS_BASE_SETTING_PATH = "ProjectSettings/GASSettingAsset.asset";
        public const string JSON_FILE_NAME_OF_TAG = "exgas_tbgameplaytags";
        public const string EXCEL_FILE_NAME_OF_TAG = "#exgas.gameplayTags";

        public const string JSON_FILE_NAME_OF_ATTR = "exgas_tbattribute";
        public const string EXCEL_FILE_NAME_OF_ATTR = "#exgas.attribute";

        public const string JSON_FILE_NAME_OF_ATTR_SET = "exgas_tbattributeset";
        public const string EXCEL_FILE_NAME_OF_ATTR_SET = "#exgas.attributeSet";

        public const string JSON_FILE_NAME_OF_EFFECT = "exgas_tbgameplayeffect";
        public const string EXCEL_FILE_NAME_OF_EFFECT = "#exgas.gameplayEffect";

        public const string JSON_FILE_NAME_OF_ABILITY = "exgas_tbability";
        public const string EXCEL_FILE_NAME_OF_ABILITY = "#exgas.ability";

        public const string JSON_FILE_NAME_OF_CUE = "exgas_tbgameplaycue";
        public const string EXCEL_FILE_NAME_OF_CUE = "#exgas.gameplayCue";

        public const string JSON_FILE_NAME_OF_ASC = "exgas_tbasc";
        public const string EXCEL_FILE_NAME_OF_ASC = "#exgas.asc";

        public const string JSON_FILE_NAME_OF_TIMELINE_ABILITY = "exgas_tbtimelineability";
        public const string EXCEL_FILE_NAME_OF_TIMELINE_ABILITY = "#exgas.timelineAbility";

        #endregion

        #region AttributeSet

        public const string ERROR_DuplicatedAttributeSet = "<size=16><b><color=orange>存在重复AttributeSet!\n" +
                                                           "<color=white> ->  {0}</color></color></b></size>";

        #endregion
    }
}
