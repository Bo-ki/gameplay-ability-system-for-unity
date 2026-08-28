using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// 只描述生成期的目标规则参数；Runtime v1 在 TargetResolve 阶段生成稳定 planned token，禁止从这里访问 World、Entity 或物理结果。
    /// </summary>
    public abstract class TargetCatcherBase
    {
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
                Debug.LogError($"Parameter type mismatch: expected {typeof(T)}, but got {parameter?.GetType() ?? typeof(void)}");
#endif
        }
    }
}
