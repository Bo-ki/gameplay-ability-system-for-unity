using System;

namespace GAS.Runtime
{
    public static class TimelineAbilityConfigRegistry
    {
        private static Func<int, XParamTimeline> _getConfigByID;

        public static void RegisterGetConfigByIDFunc(Func<int, XParamTimeline> func)
        {
            ConfigRegistryDiagnostics.ClearForConfigKind(ConfigRegistryConfigKind.TimelineAbility);
            _getConfigByID = func;
        }

        public static XParamTimeline GetConfigByID(
            int id,
            ConfigRegistryReferenceContext context = default)
        {
            var config = _getConfigByID?.Invoke(id);
            if (config == null)
                ConfigRegistryDiagnostics.ReportMissingConfig(
                    ConfigRegistryConfigKind.TimelineAbility,
                    id,
                    context);
            return config;
        }
    }
}
