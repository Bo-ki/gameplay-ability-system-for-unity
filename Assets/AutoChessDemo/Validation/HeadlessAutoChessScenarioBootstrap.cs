namespace GAS.Runtime
{
    public static partial class HeadlessAutoChessScenario
    {
        private static void EnsureRuntimeInitialized()
        {
            if (!GASManager.IsInitialized)
                GASManager.Initialize();

            HeadlessAutoChessRuntimeSystemBootstrap.RegisterSystems(GASManager.ExWorld);
        }

        private static void RegisterTargetCatcher()
        {
            TargetCatcherHelper.RegisterTargetCatcher(
                HeadlessAutoChessDefinitionSource.TargetCatcherName,
                typeof(CatchTarget),
                typeof(XParamNone));
        }

        private static void RegisterConfigs()
        {
            HeadlessAutoChessDefinitionSource.RegisterRuntimeProviders();
        }

        private static void ClearConfigProviders()
        {
            HeadlessAutoChessDefinitionSource.ClearRuntimeProviders();
        }
    }
}
