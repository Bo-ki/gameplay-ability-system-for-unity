using System.Collections.Generic;
using Unity.Entities;

namespace GAS.Runtime
{
    public sealed class CatchSelf : TargetCatcherBase<XParamNone>
    {
        protected override void CatchTargetsNonAlloc(Entity mainTarget, List<Entity> results)
        {
            results.Add(Owner);
        }
    }
}
