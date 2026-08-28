using GAS.Runtime;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasRuntimeHost
    {
        private static World _world;
        private static GasRuntimeWorldOwner _owner;
        private static bool _ownsWorld;

        public static GasRuntimeWorldOwner RuntimeOwner => _owner;

        public static bool EnsureRuntimeInitialized()
        {
            if (_owner != null)
                return true;

            // AutoChess 使用独立 headless World，阻断默认 World 中无关拓扑的隐式混入。
            _world = new World("EX-GAS Runtime v1 AutoChess");
            _ownsWorld = true;

            _owner = GasRuntimeWorldOwner.Install(_world, attachToPlayerLoop: false);
            if (!AutoChessGasCatalogSession.TryInstall())
            {
                ShutdownRuntime();
                return false;
            }

            return true;
        }

        public static void ShutdownRuntime()
        {
            if (_owner == null)
                return;

            AutoChessGasCatalogSession.Uninstall();
            AutoChessGasBattleEntityLifecycle.ResetRuntimeCache();
            _owner.Dispose();
            if (_ownsWorld && _world != null && _world.IsCreated)
                _world.Dispose();
            _owner = null;
            _world = null;
            _ownsWorld = false;
        }

        internal static bool TryResolveWorld(out World world)
        {
            world = _world;
            return world != null && world.IsCreated && _owner != null;
        }

        internal static bool TryResolveEntityManager(out EntityManager entityManager)
        {
            if (!TryResolveWorld(out var world))
            {
                entityManager = default;
                return false;
            }

            entityManager = world.EntityManager;
            return true;
        }
    }
}
