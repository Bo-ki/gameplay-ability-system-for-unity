using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    public abstract class TargetCatcherBase
    {
        public Entity Owner;

        public virtual void Init(Entity owner)
        {
            Owner = owner;
        }

        [Obsolete("请使用CatchTargetsNonAlloc方法来避免产生垃圾收集（GC）。")]
        public List<Entity> CatchTargets(Entity mainTarget)
        {
            var result = new List<Entity>();
            CatchTargetsNonAlloc(mainTarget, result);
            return result;
        }

        public void CatchTargetsNonAllocSafe(Entity mainTarget, ref List<Entity> results)
        {
            results.Clear();
            CatchTargetsNonAlloc(mainTarget, results);
        }

        protected abstract void CatchTargetsNonAlloc(Entity mainTarget, List<Entity> results);

        public virtual void InitParameters(XParam parameter) { }

        public virtual void OnEditorPreview(GameObject obj) { }
    }

    public abstract class TargetCatcherBase<T> : TargetCatcherBase where T : XParam
    {
        public T Parameter { get; private set; }

        public override void InitParameters(XParam parameter)
        {
            if (parameter is T t)
                Parameter = t;
#if UNITY_EDITOR
            else
                Debug.LogError($"Parameter type mismatch: expected {typeof(T)}, but got {parameter.GetType()}");
#endif
        }
    }
}
