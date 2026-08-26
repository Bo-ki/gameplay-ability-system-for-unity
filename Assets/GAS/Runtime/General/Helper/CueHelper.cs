using System;
using System.Collections.Generic;
using System.Linq;

namespace GAS.Runtime
{
    /// <summary>
    /// 提供 Cue 类型与参数的 authoring 映射；它只创建托管表现对象，不拥有 ECS 或 gameplay 状态。
    /// </summary>
    public static class CueHelper
    {
        private static readonly Dictionary<string, Type> CueTypeMap = new Dictionary<string, Type>();
        private static readonly Dictionary<string, Type> CueParamTypeMap = new Dictionary<string, Type>();
        private static readonly Dictionary<string, string> CueTypeToParamTypeMap = new Dictionary<string, string>();

        /// <summary>
        /// 根据 Cue 配置创建表现对象。
        /// </summary>
        public static GameplayCueBase TryCreateCue(GameplayCueConfig config)
        {
            return config == null ? null : TryCreateCue(config.CueType, config.Param);
        }

        /// <summary>
        /// 根据 Cue 类型名创建表现对象。
        /// </summary>
        public static GameplayCueBase TryCreateCue(string cueType, XParam parameter)
        {
            return CueTypeMap.TryGetValue(cueType ?? string.Empty, out var type)
                ? TryCreateCue(type, parameter)
                : null;
        }

        /// <summary>
        /// 通过反射创建 Cue，并注入其强类型参数。
        /// </summary>
        public static GameplayCueBase TryCreateCue(Type cueType, XParam parameter)
        {
            if (cueType == null || !typeof(GameplayCueBase).IsAssignableFrom(cueType))
                return null;

            var cue = Activator.CreateInstance(cueType) as GameplayCueBase;
            cue?.InitParameters(parameter);
            return cue;
        }

        /// <summary>
        /// 创建 Cue 参数对象，供配置编辑和序列化使用。
        /// </summary>
        public static XParam CreateCueParameter(string cueType, IList<object> parameterData = null)
        {
            var parameterType = GetCueLogicParamType(cueType);
            if (parameterType == null)
                return null;

            var parameter = Activator.CreateInstance(parameterType) as XParam;
#if UNITY_EDITOR
            if (parameterData != null)
                parameter?.DecodeExcelData(parameterData.ToList());
#endif
            return parameter;
        }

        /// <summary>
        /// 返回 Cue 类型对应的参数类型。
        /// </summary>
        public static Type GetCueLogicParamType(string cueType)
        {
            if (!CueTypeToParamTypeMap.TryGetValue(cueType ?? string.Empty, out var parameterName))
                return null;

            return CueParamTypeMap.TryGetValue(parameterName, out var parameterType)
                ? parameterType
                : null;
        }

        /// <summary>
        /// 返回 Cue 类型对应的参数类型。
        /// </summary>
        public static Type GetCueLogicParamType(Type cueType)
        {
            return cueType == null ? null : GetCueLogicParamType(cueType.Name);
        }

        /// <summary>
        /// 注册一个 Cue 类型及其参数类型，供 authoring 反射和配置解码使用。
        /// </summary>
        public static void RegisterCue(string cueType, Type logicType, Type cueParamType)
        {
            if (string.IsNullOrWhiteSpace(cueType) || logicType == null || cueParamType == null)
                return;

            CueTypeMap[cueType] = logicType;
            CueParamTypeMap[cueParamType.Name] = cueParamType;
            CueTypeToParamTypeMap[cueType] = cueParamType.Name;
        }

        /// <summary>
        /// 注册一个泛型 Cue 类型及其参数类型。
        /// </summary>
        public static void RegisterCue<T>(string cueType, Type cueParamType)
            where T : GameplayCueBase
        {
            RegisterCue(cueType, typeof(T), cueParamType);
        }

        /// <summary>
        /// 返回已注册 Cue 类型名的稳定快照。
        /// </summary>
        public static List<string> GetCueTypeNames()
        {
            return CueTypeMap.Keys.OrderBy(value => value, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// 返回已注册 Cue 类型。
        /// </summary>
        public static Type GetCueType(string cueType)
        {
            return CueTypeMap.TryGetValue(cueType ?? string.Empty, out var type) ? type : null;
        }
    }
}
