using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

namespace GAS.Editor
{
    public enum EffectEditComponent
    {
        [LabelText("描述标签")]
        AssetTags,
        
        [LabelText("获得标签")]
        GrantedTags,

        [LabelText("应用需求标签")]
        ApplicationRequiredTags,

        [LabelText("持续需求标签")]
        OngoingRequiredTags,
        
        [LabelText("移除持有标签的buff")]
        RemoveGameplayEffectsWithTags,
        
        [LabelText("被免疫的标签")]
        ImmunityTags,
        
        [LabelText("持续时间")]
        Duration,
        
        [LabelText("间隔执行")]
        Period,
        
        [LabelText("修改器")]
        Modifiers,
        
        [LabelText("应用时触发的Cue")]
        CueOnApply,
        
        [LabelText("帧更新的Cue")]
        CueOnTick,
        
        [LabelText("添加时触发的Cue")]
        CueOnAdd,
        
        [LabelText("移除时触发的Cue")]
        CueOnRemove,	
        
        [LabelText("激活时触发的Cue")]
        CueOnActivate,
        
        [LabelText("失活时触发的Cue")]
        CueOnDeactivate,
        
        [LabelText("获取技能")]
        GrantedAbility,
        
        [LabelText("buff堆叠")]
        Stacking
    }
    
    public static class EditorEffectHelper
    {
        public readonly struct TagRequirementProtocolField
        {
            public TagRequirementProtocolField(EffectEditComponent component, string excelHeader, string jsonKey)
            {
                Component = component;
                ExcelHeader = excelHeader;
                JsonKey = jsonKey;
            }

            public EffectEditComponent Component { get; }
            public string ExcelHeader { get; }
            public string JsonKey { get; }
        }

        public static readonly TagRequirementProtocolField[] TagRequirementProtocolFields =
        {
            new(EffectEditComponent.ApplicationRequiredTags, "ApplicationRequiredTags", "applicationRequiredTags"),
            new(EffectEditComponent.OngoingRequiredTags, "OngoingRequiredTags", "ongoingRequiredTags"),
            new(EffectEditComponent.RemoveGameplayEffectsWithTags, "RemoveGameplayEffectsWithTags", "removeEffectsWithTags"),
            new(EffectEditComponent.ImmunityTags, "ImmunityTags", "immunityTags")
        };

        public static IEnumerable<EffectEditComponent> ComponentTypes()
        {
            return new[]
            {
                EffectEditComponent.AssetTags,
                EffectEditComponent.GrantedTags,
                EffectEditComponent.ApplicationRequiredTags,
                EffectEditComponent.OngoingRequiredTags,
                EffectEditComponent.RemoveGameplayEffectsWithTags,
                EffectEditComponent.ImmunityTags,
                EffectEditComponent.Duration,
                EffectEditComponent.Period,
                EffectEditComponent.Modifiers,
                EffectEditComponent.CueOnApply,
                EffectEditComponent.CueOnTick,
                EffectEditComponent.CueOnAdd,
                EffectEditComponent.CueOnRemove,
                EffectEditComponent.CueOnActivate,
                EffectEditComponent.CueOnDeactivate,
                EffectEditComponent.GrantedAbility,
                EffectEditComponent.Stacking
            };
        }

        public static GEEditTagRequirement ParseTagRequirementCell(string raw)
        {
            var requirement = new GEEditTagRequirement();
            if (string.IsNullOrWhiteSpace(raw)) return requirement;

            var text = raw.Trim();
            var parts = text.Split(';');
            if (parts.Length != 3) return requirement;

            requirement.All = ParseTagCsv(parts[0]);
            requirement.Any = ParseTagCsv(parts[1]);
            requirement.None = ParseTagCsv(parts[2]);

            return requirement;
        }

        public static string EncodeTagRequirementCell(GEEditTagRequirement requirement)
        {
            if (requirement == null) return string.Empty;
            if (!requirement.HasAnyValue()) return string.Empty;
            return $"{EncodeTagCsv(requirement.All)};{EncodeTagCsv(requirement.Any)};{EncodeTagCsv(requirement.None)}";
        }

        private static List<int> ParseTagCsv(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "0") return new List<int>();
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x.Trim(), out var value) ? value : 0)
                .Where(x => x > 0)
                .ToList();
        }

        private static string EncodeTagCsv(List<int> tags)
        {
            return tags == null || tags.Count == 0
                ? "0"
                : string.Join(",", tags.Where(x => x > 0));
        }
    }
}
