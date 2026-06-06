#if UNITY_EDITOR && EX_GAS_ENABLE_ODIN_LEGACY_EDITOR
namespace GAS.Editor
{
    using Sirenix.OdinInspector.Editor;

    public class OdinEditorWithoutHeader : OdinEditor
    {
        protected override void OnHeaderGUI()
        {
        }
    }
}
#endif
