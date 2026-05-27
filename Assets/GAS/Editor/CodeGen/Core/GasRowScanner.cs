using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

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
                            result.Add(type);
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

        [InitializeOnLoadMethod]
        private static void RegisterAssemblyReloadCallback()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
            AssemblyReloadEvents.afterAssemblyReload += ClearCache;
        }

        private static void ClearCache()
        {
            s_cachedRows = null;
        }
    }
}
