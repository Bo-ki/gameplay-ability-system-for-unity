using System.Collections.Generic;
using System.IO;
using GAS.General;
using OfficeOpenXml;

#if EX_GAS_ENABLE_ODIN_LEGACY_EDITOR
using Sirenix.OdinInspector;
#endif

namespace GAS.Editor
{
    public static class GasXlsxChoice
    {
        private static List<GasChoiceItem> _cues;
        private static List<GasChoiceItem> _effects;
        private static List<GasChoiceItem> _abilities;
        private static List<GasChoiceItem> _ascs;
        private static List<GasChoiceItem> _tags;
        private static List<GasChoiceItem> _attrSets;
        private static Dictionary<int,List<GasChoiceItem>> _attrs;
        
        public static void LoadChoices()
        {
            var setting = GASSettingAsset.LoadOrCreate();
            _cues = LoadChoiceListFromExcel(setting.PathOfExcelCue, "Cue", true);
            _tags = LoadChoiceListFromExcel(setting.PathOfExcelTag, "Tag", false);
            _effects = LoadChoiceListFromExcel(setting.PathOfExcelEffect, "Effect", true);
            _abilities = LoadChoiceListFromExcel(setting.PathOfExcelAbility, "Ability", true);
            _ascs = LoadChoiceListFromExcel(setting.PathOfExcelAsc, "ASC", true);
            
            // _attrs = GASXlsxReader.AttrChoices();
            
            // 属性集，属性需要特殊处理，读Json
            GasJsonReader.ReadAllAndCache();
            _attrSets = new List<GasChoiceItem>();
            _attrs = new Dictionary<int, List<GasChoiceItem>>();
            var attrSetMap = GasJsonReader.AttrSetMap();
            foreach (var kv in attrSetMap)
            {
                _attrSets.Add(new GasChoiceItem(kv.Value.name,kv.Key));
                _attrs.Add(kv.Key,new List<GasChoiceItem>());
                var attributes = kv.Value.attribute ?? System.Array.Empty<AttrInSetInEditor>();
                foreach (var attr in attributes)
                {
                    _attrs[kv.Key].Add(new GasChoiceItem(attr.GetAttrName(),attr.id));
                }
            }
        }

        private static List<GasChoiceItem> LoadChoiceListFromExcel(string path, string label, bool showIdInName)
        {
            var result = new List<GasChoiceItem>();
            if (string.IsNullOrWhiteSpace(path))
            {
                UnityEngine.Debug.LogWarning($"[EX-GAS] {label} Excel 路径为空。");
                return result;
            }

            if (!File.Exists(path))
            {
                UnityEngine.Debug.LogWarning($"[EX-GAS] {label} Excel 文件不存在: {path}");
                return result;
            }

            try
            {
                using var package = new ExcelPackage(new FileInfo(path));
                var worksheet = package.Workbook.Worksheets.Count > 0 ? package.Workbook.Worksheets[1] : null;
                if (worksheet == null)
                {
                    UnityEngine.Debug.LogWarning($"[EX-GAS] {label} Excel 没有可读取工作表: {path}");
                    return result;
                }

                var safeCnt = 99999;
                var row = 4;
                while (safeCnt > 0 && worksheet.Cells[row, 2].Value != null)
                {
                    safeCnt--;
                    var rawId = worksheet.Cells[row, 2].Value.ToString();
                    if (!int.TryParse(rawId, out var id))
                    {
                        UnityEngine.Debug.LogWarning($"[EX-GAS] {label} 第 {row} 行ID不是整数: {rawId}");
                        row++;
                        continue;
                    }

                    var name = worksheet.Cells[row, 3].Value?.ToString() ?? string.Empty;
                    var showName = showIdInName ? $"[{id}]{name}" : name;
                    result.Add(new GasChoiceItem(showName, id));
                    row++;
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[EX-GAS] 读取 {label} Excel 失败: {path}\n{ex.GetType().Name}: {ex.Message}");
            }

            return result;
        }

        public static List<GasChoiceItem> Cues()
        {
            if (_cues == null)
                LoadChoices();
            return _cues;
        }
        
        public static List<GasChoiceItem> Effects()
        {
            if (_effects == null)
                LoadChoices();
            return _effects;
        }
        
        public static List<GasChoiceItem> Abilities()
        {
            if (_abilities == null)
                LoadChoices();
            return _abilities;
        }
        
        public static List<GasChoiceItem> Ascs()
        {
            if (_ascs == null)
                LoadChoices();
            return _ascs;
        }
        
        public static List<GasChoiceItem> Tags()
        {
            if (_tags == null)
                LoadChoices();
            return _tags;
        }
        
        public static List<GasChoiceItem> AttrSets()
        {
            if (_attrSets == null)
                LoadChoices();
            return _attrSets;
        }
        
        public static List<GasChoiceItem> Attributes(int attrSetId)
        {
            if (_attrs == null)
                LoadChoices();
            if (_attrs != null && _attrs.TryGetValue(attrSetId, out var choices))
                return choices;
            return new List<GasChoiceItem>();
        }
    }

#if EX_GAS_ENABLE_ODIN_LEGACY_EDITOR
    public static class GasOdinChoice
    {
        public static List<ValueDropdownItem> Cues()
        {
            return ToDropdownItems(GasXlsxChoice.Cues());
        }

        public static List<ValueDropdownItem> Effects()
        {
            return ToDropdownItems(GasXlsxChoice.Effects());
        }

        public static List<ValueDropdownItem> Abilities()
        {
            return ToDropdownItems(GasXlsxChoice.Abilities());
        }

        public static List<ValueDropdownItem> Ascs()
        {
            return ToDropdownItems(GasXlsxChoice.Ascs());
        }

        public static List<ValueDropdownItem> Tags()
        {
            return ToDropdownItems(GasXlsxChoice.Tags());
        }

        public static List<ValueDropdownItem> AttrSets()
        {
            return ToDropdownItems(GasXlsxChoice.AttrSets());
        }

        public static List<ValueDropdownItem> Attributes(int attrSetId)
        {
            return ToDropdownItems(GasXlsxChoice.Attributes(attrSetId));
        }

        public static ValueDropdownItem[] JsonTags()
        {
            var choices = GasJsonReader.TagChoices();
            var result = new ValueDropdownItem[choices?.Length ?? 0];
            if (choices == null)
                return result;

            for (var i = 0; i < choices.Length; i++)
                result[i] = new ValueDropdownItem(choices[i].Text, choices[i].Value);

            return result;
        }

        private static List<ValueDropdownItem> ToDropdownItems(IReadOnlyList<GasChoiceItem> choices)
        {
            var result = new List<ValueDropdownItem>(choices?.Count ?? 0);
            if (choices == null)
                return result;

            for (var i = 0; i < choices.Count; i++)
                result.Add(new ValueDropdownItem(choices[i].Text, choices[i].Value));

            return result;
        }
    }
#endif
}
