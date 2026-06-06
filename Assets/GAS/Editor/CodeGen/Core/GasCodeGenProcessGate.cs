namespace GAS.Editor
{
    internal static class GasCodeGenProcessGate
    {
        public static bool RunDefault()
        {
            if (!BeanUpdater.TryUpdateBeans())
            {
                GasCodeGenEnvironment.LogError("[GasCodeGenProcessGate] Bean 更新失败，已阻断 Core CodeGen。");
                return false;
            }

            if (!CodeGenerator.TryGenerateGasConfigTables())
            {
                GasCodeGenEnvironment.LogError("[GasCodeGenProcessGate] Luban 导表失败，已阻断 Core CodeGen。");
                return false;
            }

            GasCodeGenEnvironment.RefreshAssetDatabase();
            GasCodeGenEnvironment.Log("[GasCodeGenProcessGate] Bean / Luban process gate 通过。");
            return true;
        }
    }
}
