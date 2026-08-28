using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// 提供 TargetCatcher 的 authoring 类型映射；Runtime v1 不从这里解析 World、Entity 或物理命中。
    /// </summary>
    public static class TargetCatcherHelper
    {
        private static readonly Dictionary<string, Type> CatcherTypeMap = new Dictionary<string, Type>();
        private static readonly Dictionary<string, Type> CatcherParamTypeMap = new Dictionary<string, Type>();
        private static readonly Dictionary<string, string> CatcherType2ParamTypeMap = new Dictionary<string, string>();

        /// <summary>
        /// 注册一个目标规则类型及其参数类型。
        /// </summary>
        public static void RegisterTargetCatcher(string catcherName, Type catcherType, Type catcherParamType)
        {
            if (string.IsNullOrWhiteSpace(catcherName)
                || catcherType == null
                || catcherParamType == null
                || !typeof(TargetCatcherBase).IsAssignableFrom(catcherType)
                || !typeof(XParam).IsAssignableFrom(catcherParamType))
                return;

            CatcherTypeMap[catcherName] = catcherType;
            CatcherParamTypeMap[catcherParamType.Name] = catcherParamType;
            CatcherType2ParamTypeMap[catcherName] = catcherParamType.Name;
        }

        /// <summary>
        /// 创建一个仅用于 authoring 的目标规则对象。
        /// </summary>
        public static TargetCatcherBase TryCreateTargetCatcher(string catcherType)
        {
            if (CatcherTypeMap.TryGetValue(catcherType ?? string.Empty, out var type))
                return Activator.CreateInstance(type) as TargetCatcherBase;
#if UNITY_EDITOR
            Debug.LogError($"[EX] 创建TargetCatcher失败:Can't find TargetCatcher for catcherType [{catcherType}]. " +
                           "TargetCatcher的Type映射脚本错误，请重新生成。");
#endif
            return null;
        }

        /// <summary>
        /// 返回目标规则对应的参数类型；未知名称返回 null。
        /// </summary>
        public static Type GetCatcherParamType(string catcherTypeName)
        {
            if (!CatcherType2ParamTypeMap.TryGetValue(catcherTypeName ?? string.Empty, out var paramName))
                return null;

            return CatcherParamTypeMap.TryGetValue(paramName, out var paramType) ? paramType : null;
        }

        /// <summary>
        /// 返回已注册目标规则名称的稳定快照。
        /// </summary>
        public static IEnumerable<string> GetCatcherTypeNames()
        {
            return CatcherTypeMap.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// 创建目标规则的默认 authoring 参数实例。
        /// </summary>
        public static XParam CreateCatcherParameter(string catcherType)
        {
            var paramType = GetCatcherParamType(catcherType);
            return paramType == null ? null : Activator.CreateInstance(paramType) as XParam;
        }
    }
}
