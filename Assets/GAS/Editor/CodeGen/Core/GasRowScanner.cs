using System;
using System.Collections.Generic;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GAS.Editor
{
    public static class GasRowScanner
    {
        private static IReadOnlyList<Type> s_cachedRows;

        public static IReadOnlyList<Type> Scan(bool forceRefresh = false)
        {
            if (s_cachedRows != null && !forceRefresh)
                return s_cachedRows;

            var result = new List<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic)
                    continue;

                try
                {
                    foreach (var type in asm.GetExportedTypes())
                    {
                        if (!type.IsValueType)
                            continue;

                        if (type.Name.EndsWith("DefinitionRow", StringComparison.Ordinal))
                            AddUnique(result, type);
                    }
                }
                catch (NotSupportedException)
                {
                }
                catch (ReflectionTypeLoadException)
                {
                }
            }

            result.Sort((left, right) => string.CompareOrdinal(left.FullName, right.FullName));
            s_cachedRows = result;
            return s_cachedRows;
        }

        private static void AddUnique(List<Type> result, Type type)
        {
            if (type == null || result.Contains(type))
                return;

            result.Add(type);
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterAssemblyReloadCallback()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
            AssemblyReloadEvents.afterAssemblyReload += ClearCache;
        }
#endif

        private static void ClearCache()
        {
            s_cachedRows = null;
        }
    }
}
