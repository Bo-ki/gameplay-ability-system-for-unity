using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// 表示由 Boundary fact 驱动的表现层 Cue；只保存托管表现状态，绝不持有 Entity 或 EntityManager。
    /// </summary>
    public abstract class GameplayCueBase
    {
        private GameObject _targetGameObject;
        private ulong _sourceStableId;
        private bool _playRequested;
        private bool _stopRequested;
        private bool _removeRequested;
        private bool _killRequested;

        protected CueSourceType _sourceType;

        /// <summary>
        /// 返回 Boundary 为该 Cue 绑定的表现目标对象。
        /// </summary>
        protected GameObject TargetGameObject => _targetGameObject;

        /// <summary>
        /// 返回当前 Cue 的稳定来源身份，供日志和表现诊断使用。
        /// </summary>
        protected ulong SourceStableId => _sourceStableId;

        /// <summary>
        /// 返回 Cue 的来源域。
        /// </summary>
        protected CueSourceType SourceType => _sourceType;

        /// <summary>
        /// 返回 Boundary 是否请求播放该 Cue。
        /// </summary>
        public bool PlayRequested => _playRequested;

        /// <summary>
        /// 返回 Boundary 是否请求停止该 Cue。
        /// </summary>
        public bool StopRequested => _stopRequested;

        /// <summary>
        /// 返回 Boundary 是否请求移除该 Cue。
        /// </summary>
        public bool RemoveRequested => _removeRequested;

        /// <summary>
        /// 返回 Boundary 是否请求销毁该 Cue 实例。
        /// </summary>
        public bool KillRequested => _killRequested;

        /// <summary>
        /// 将不可变参数注入表现对象。
        /// </summary>
        public abstract void InitParameters(XParam xParam);

        /// <summary>
        /// 清除表现请求与短生命周期状态，使实例可被新的 Cue fact 复用。
        /// </summary>
        public virtual void Reset()
        {
            _playRequested = false;
            _stopRequested = false;
            _removeRequested = false;
            _killRequested = false;
            _targetGameObject = null;
            _sourceStableId = 0;
            _sourceType = CueSourceType.None;
        }

        /// <summary>
        /// 设置 Boundary 提供的表现上下文；该方法是唯一的目标对象与来源身份入口。
        /// </summary>
        public void SetPresentationContext(GameObject target, CueSourceType sourceType, ulong sourceStableId)
        {
            _targetGameObject = target;
            _sourceType = sourceType;
            _sourceStableId = sourceStableId;
        }

        /// <summary>
        /// 请求播放 Cue；实际播放由表现层宿主消费请求完成。
        /// </summary>
        public void Play(bool replay = false)
        {
            if (!CanPlay())
                return;

            _playRequested = true;
            _stopRequested = false;
            if (replay)
                _removeRequested = false;
        }

        /// <summary>
        /// 请求停止 Cue；不直接触碰 Runtime Core 状态。
        /// </summary>
        public void Stop(bool immediate = false)
        {
            _playRequested = false;
            _stopRequested = true;
        }

        /// <summary>
        /// 请求立即停止 Cue。
        /// </summary>
        public void StopImmediate()
        {
            Stop(true);
        }

        /// <summary>
        /// 请求销毁 Cue 实例。
        /// </summary>
        public void KillSelf()
        {
            _killRequested = true;
        }

        /// <summary>
        /// 请求从表现层移除 Cue。
        /// </summary>
        public void RemoveSelf()
        {
            _removeRequested = true;
            _stopRequested = true;
            _playRequested = false;
        }

        /// <summary>
        /// 判断当前表现上下文是否允许播放 Cue。
        /// </summary>
        protected virtual bool CanPlay()
        {
            return true;
        }

        /// <summary>
        /// 处理 Cue 被加入目标表现对象的生命周期回调。
        /// </summary>
        public virtual void OnAdd(float time)
        {
        }

        /// <summary>
        /// 处理 Cue 从目标表现对象移除的生命周期回调。
        /// </summary>
        public virtual void OnRemove(float time)
        {
        }

        /// <summary>
        /// 处理 Cue 激活回调。
        /// </summary>
        public virtual void OnActivate(float time)
        {
        }

        /// <summary>
        /// 处理 Cue 停用回调。
        /// </summary>
        public virtual void OnDeactivate(float time)
        {
        }

        /// <summary>
        /// 处理 Cue 周期更新回调。
        /// </summary>
        public virtual void OnTick(float time)
        {
        }

        /// <summary>
        /// 处理 Cue 销毁回调。
        /// </summary>
        public virtual void OnDestroy(float time)
        {
        }

#if UNITY_EDITOR
        /// <summary>
        /// 在编辑器中预览 Cue 表现；运行时不会调用该入口。
        /// </summary>
        public virtual void OnPreview(GameObject target, int frame, int startFrame, int endFrame)
        {
        }
#endif
    }

    /// <summary>
    /// 为 Cue 绑定强类型参数的表现基类，保持参数解析与表现生命周期解耦。
    /// </summary>
    public abstract class GameplayCueBase<T> : GameplayCueBase where T : XParam
    {
        /// <summary>
        /// 返回当前 Cue 的强类型参数实例。
        /// </summary>
        public T Parameter { get; private set; }

        /// <summary>
        /// 校验并保存强类型 Cue 参数。
        /// </summary>
        public override void InitParameters(XParam xParam)
        {
            if (xParam is T typedParameter)
            {
                Parameter = typedParameter;
                return;
            }

#if UNITY_EDITOR
            Debug.LogError($"Parameter type mismatch: expected {typeof(T)}, but got {xParam?.GetType()}");
#endif
        }
    }
}
