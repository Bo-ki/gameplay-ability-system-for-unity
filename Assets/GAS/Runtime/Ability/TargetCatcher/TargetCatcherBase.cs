using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    public abstract class TargetCatcherBase
    {
        public Entity Owner;
        protected EntityManager EntityManager { get; private set; }

        public virtual void Init(EntityManager entityManager, Entity owner)
        {
            EntityManager = entityManager;
            Owner = owner;
        }

        public void CollectTargetsNonAlloc(Entity mainTarget, List<Entity> results)
        {
            results.Clear();
            CollectTargetsNonAllocCore(mainTarget, results);
        }

        protected abstract void CollectTargetsNonAllocCore(Entity mainTarget, List<Entity> results);

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
