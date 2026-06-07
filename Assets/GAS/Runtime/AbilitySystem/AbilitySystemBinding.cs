using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// GameObject 到 ASC Entity 的生命周期绑定。GAS 运行时状态只存在于 ECS Entity 中。
    /// </summary>
    public class AbilitySystemBinding : MonoBehaviour
    {
        private ASCCommandPort _commands;
        private bool _isPresentationBound;

        private void Awake()
        {
            TryInitializeRuntimePort();
        }

        private void OnDestroy()
        {
            UnbindPresentation();
            if (_commands.IsValid)
                _commands.RequestDestroy();
        }

        private void OnEnable()
        {
            if (TryInitializeRuntimePort())
                BindPresentation();
        }

        private void OnDisable()
        {
            UnbindPresentation();
        }

        public void Init(AbilitySystemConfig config)
        {
            if (!TryInitializeRuntimePort())
                return;

            BindPresentation();
            _commands.RequestInitialize(config);
        }

        public ASCCommandPort Commands => _commands;

        public ASCReadModel ReadModel => CaptureReadModel();

        public ASCReadModel CaptureReadModel()
        {
            return _commands.IsValid
                ? GASRuntimeShell.CaptureASCReadModel(_commands.Handle)
                : default;
        }

        public ASCHandle Handle => _commands.Handle;

        internal bool TryResolveRuntimeEntityForBoundary(out Entity entity)
        {
            return Handle.TryResolveRuntimeEntity(out entity);
        }

        private bool TryInitializeRuntimePort()
        {
            if (_commands.IsValid)
                return true;

            return GASRuntimeShell.TryCreateASCCommandPort(out _commands);
        }

        private void BindPresentation()
        {
            if (_isPresentationBound || !_commands.IsValid)
                return;

            _isPresentationBound = GASRuntimeShell.TryBindPresentation(_commands.Handle, gameObject);
        }

        private void UnbindPresentation()
        {
            if (!_isPresentationBound)
                return;

            GASRuntimeShell.TryUnbindPresentation(_commands.Handle);
            _isPresentationBound = false;
        }
    }
}
