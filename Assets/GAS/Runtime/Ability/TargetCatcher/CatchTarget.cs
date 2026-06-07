using System.Collections.Generic;
using Unity.Entities;

namespace GAS.Runtime
{
    public sealed class CatchTarget : TargetCatcherBase<XParamNone>
    {
        protected override void CollectTargetsNonAllocCore(Entity mainTarget, List<Entity> results)
        {
            results.Add(mainTarget);
        }
    }
}
