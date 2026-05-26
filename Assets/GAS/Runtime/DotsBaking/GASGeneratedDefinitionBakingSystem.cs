using Unity.Entities;

namespace GAS.Runtime.DotsBaking
{
    /// <summary>
    /// Placeholder: awaits codegen regeneration after AutoChessDemo deletion.
    /// </summary>
    [RequireMatchingQueriesForUpdate]
    public partial class GASGeneratedDefinitionBakingSystem : SystemBase
    {
        private bool _hasRun;

        protected override void OnCreate()
        {
            _hasRun = false;
            Enabled = false;
        }

        protected override void OnUpdate()
        {
            if (_hasRun) return;
            _hasRun = true;
            Enabled = false;
        }
    }
}
