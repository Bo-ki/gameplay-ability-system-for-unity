using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GAS.Runtime;
using Sirenix.OdinInspector;

namespace GAS.Editor
{
    public enum AbilityEditComponent
    {
        [LabelText("消耗[GE]")]
        Cost,

        [LabelText("冷却[GE]")]
        Cooldown,

        [LabelText("描述标签")]
        AssetTags,

        [LabelText("拥有【任意】Tag的Ability会被取消")]
        CancelAbilityWithTags,

        [LabelText("拥有【任意】Tag的Ability会被阻止")]
        BlockAbilityWithTags,

        [LabelText("激活后获得的Tag")]
        ActivationOwnedTags,

        [LabelText("激活需要的Tag")]
        ActivationRequiredTags,

        [LabelText("阻止激活的Tag")]
        ActivationBlockedTags,
    }

    public readonly struct AbilityExecutionSchema
    {
        public readonly string Name;
        public readonly Type ParamType;

        public AbilityExecutionSchema(string name, Type paramType)
        {
            Name = name;
            ParamType = paramType;
        }
    }

    public readonly struct TimelineActionParameterSchema
    {
        public readonly string Name;
        public readonly Type ParamType;

        public TimelineActionParameterSchema(string name, Type paramType)
        {
            Name = name;
            ParamType = paramType;
        }
    }

    public static class EditorAbilityHelper
    {
        private static IReadOnlyList<AbilityExecutionSchema> _abilityExecutionSchemas;
        private static IReadOnlyList<TimelineActionParameterSchema> _timelineActionParameterSchemas;

        public static IEnumerable<AbilityEditComponent> ComponentTypes()
        {
            return new[]
            {
                AbilityEditComponent.Cost,
                AbilityEditComponent.Cooldown,
                AbilityEditComponent.AssetTags,
                AbilityEditComponent.CancelAbilityWithTags,
                AbilityEditComponent.BlockAbilityWithTags,
                AbilityEditComponent.ActivationOwnedTags,
                AbilityEditComponent.ActivationRequiredTags,
                AbilityEditComponent.ActivationBlockedTags,
            };
        }

        public static IReadOnlyList<AbilityExecutionSchema> GetAbilityExecutionSchemas()
        {
            return _abilityExecutionSchemas ??= new[]
            {
                new AbilityExecutionSchema("ApplyEffectsOnActivate", typeof(XParamEffectIDs)),
                new AbilityExecutionSchema("TimelineRef", typeof(XParamTimelineID)),
                new AbilityExecutionSchema("MoveInput", ResolveXParamType("MoveInput", "DemoForESC._Script.Gas.Ability.XParamMove", "XParamMove")),
            };
        }

        public static IEnumerable<string> GetAbilityExecutionTypeNames()
        {
            return GetAbilityExecutionSchemas().Select(schema => schema.Name);
        }

        public static XParam CreateAbilityParameter(string type, List<object> paramData = null)
        {
            var schema = GetAbilityExecutionSchemas().FirstOrDefault(item => item.Name == type);
            if (schema.ParamType == null)
                throw new KeyNotFoundException($"未找到 Ability 执行配置 {type} 对应的 XParam 类型。");

            var abilityParamEditor = (XParam)Activator.CreateInstance(schema.ParamType);
            if (paramData != null) abilityParamEditor.DecodeExcelData(paramData);
            return abilityParamEditor;
        }

        public static IReadOnlyList<TimelineActionParameterSchema> GetTimelineActionParameterSchemas()
        {
            return _timelineActionParameterSchemas ??= new[]
            {
                new TimelineActionParameterSchema("NoOp", typeof(XParamNone)),
                new TimelineActionParameterSchema("DebugLog", typeof(XParamString)),
                new TimelineActionParameterSchema("ApplyCost", typeof(XParamNone)),
                new TimelineActionParameterSchema("ApplyCooldown", typeof(XParamNone)),
                new TimelineActionParameterSchema("PlayCue", typeof(XParamCue)),
                new TimelineActionParameterSchema("ApplyEffects", typeof(XParamApplyEffects)),
                new TimelineActionParameterSchema("DodgeMove", ResolveXParamType("DodgeMove", "DemoForESC._Script.Gas.Ability.XParamDodgeMove", "XParamDodgeMove")),
                new TimelineActionParameterSchema("PlayCuePreset", typeof(XParamCueList)),
            };
        }

        public static IEnumerable<string> GetTimelineActionTypeNames()
        {
            return GetTimelineActionParameterSchemas().Select(schema => schema.Name);
        }

        public static XParam CreateTimelineActionParameter(string type, List<object> paramData = null)
        {
            var schema = GetTimelineActionParameterSchemas().FirstOrDefault(item => item.Name == type);
            if (schema.ParamType == null)
                throw new KeyNotFoundException($"未找到 Timeline Action 配置 {type} 对应的 XParam 类型。");

            var abilityParamEditor = (XParam)Activator.CreateInstance(schema.ParamType);
            if (paramData != null) abilityParamEditor.DecodeExcelData(paramData);
            return abilityParamEditor;
        }

        private static Type ResolveXParamType(string ownerName, params string[] typeNames)
        {
            foreach (var typeName in typeNames)
            {
                var resolved = ResolveType(typeName);
                if (resolved != null && typeof(XParam).IsAssignableFrom(resolved))
                    return resolved;
            }

            throw new InvalidOperationException($"未找到 {ownerName} 需要的 XParam 类型：{string.Join(", ", typeNames)}");
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            var direct = Type.GetType(typeName);
            if (direct != null)
                return direct;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }

                var resolved = types.FirstOrDefault(type =>
                    type.FullName == typeName || type.Name == typeName);
                if (resolved != null)
                    return resolved;
            }

            return null;
        }
    }
}
