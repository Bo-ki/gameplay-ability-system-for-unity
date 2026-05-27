using UnityEditor;
using Debug = UnityEngine.Debug;

namespace GAS.Editor
{
    internal static class GasCodeGenProcessGate
    {
        public static bool RunDefault()
        {
            if (!BeanUpdater.TryUpdateBeans())
            {
                Debug.LogError("[GasCodeGenProcessGate] Bean 更新失败，已阻断 Core CodeGen。");
                return false;
            }

            if (!CodeGenerator.TryGenerateGasConfigTables())
            {
                Debug.LogError("[GasCodeGenProcessGate] Luban 导表失败，已阻断 Core CodeGen。");
                return false;
            }

            AssetDatabase.Refresh();
            Debug.Log("[GasCodeGenProcessGate] Bean / Luban process gate 通过。");
            return true;
        }
    }
}
