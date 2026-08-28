using System;
using Unity.Entities;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    public static class AutoChessRuntimeSystemBootstrap
    {
        /// <summary>
        /// 验证 Runtime v1 owner 已安装唯一 FixedTick lane；系统注册由 owner 统一完成。
        /// </summary>
        public static bool RegisterSystems(World world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            return world.GetExistingSystemManaged<GasFixedTickSystemGroup>() != null &&
                   world.GetExistingSystem<GasTickKernelSystem>() != default;
        }
    }
}
