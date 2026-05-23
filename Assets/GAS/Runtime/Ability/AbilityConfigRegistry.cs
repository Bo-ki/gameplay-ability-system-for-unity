using System;

namespace GAS.Runtime
{
    public static class AbilityConfigRegistry
    {
        private static Func<int, AbilityConfig> _getConfigByID;

        public static void RegisterGetConfigByIDFunc(Func<int, AbilityConfig> func)
        {
            ConfigRegistryDiagnostics.ClearForConfigKind(ConfigRegistryConfigKind.Ability);
            _getConfigByID = func;
        }

        public static AbilityConfig GetConfigByID(
            int id,
            ConfigRegistryReferenceContext context = default)
        {
            var config = _getConfigByID?.Invoke(id);
            if (config == null)
                ConfigRegistryDiagnostics.ReportMissingConfig(
                    ConfigRegistryConfigKind.Ability,
                    id,
                    context);
            return config;
        }
    }
}
