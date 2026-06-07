using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    public abstract class GameplayCueBase
    {
        protected Entity _cueEntity;
        protected Entity _sourceEntity;
        protected CueSourceType _sourceType;
        protected Entity _targetAscEntity;
        protected EntityManager EntityManager { get; private set; }
        protected GameObject TargetGameObject => PresentationEntityBindingRegistry.GetGameObjectFromEntity(EntityManager, _targetAscEntity);

        public abstract void InitParameters(XParam xParam);

        public virtual void Reset()
        {
        }

        public void SetRuntime(EntityManager entityManager, Entity cueEntity)
        {
            EntityManager = entityManager;
            _cueEntity = cueEntity;
        }

        public void SetSourceEntity(Entity e, CueSourceType sourceType)
        {
            _sourceEntity = e;
            _sourceType = sourceType;
        }

        /// <summary>
        /// 添加Cue到目标ASC
        /// </summary>
        /// <param name="e"></param>
        internal void ApplyAddToTargetAsc(Entity e, float time)
        {
            if (e != Entity.Null)
            {
                _targetAscEntity = e;
                OnAdd(time);
            }
        }

        /// <summary>
        /// cue从目标ASC移除
        /// </summary>
        internal void ApplyRemoveFromTargetAsc(float time)
        {
            OnRemove(time);
            _targetAscEntity = Entity.Null;
        }

        /// <summary>
        /// 自定义能否播放cue逻辑
        /// </summary>
        /// <returns></returns>
        protected virtual bool CanPlay()
        {
            return true;
        }

        /// <summary>
        /// 播放Cue
        /// </summary>
        /// <param name="replay"> 是否从头播放 </param>
        public void Play(bool replay = false)
        {
            if (!CanPlay())
                return;

            if (replay && TryGetPresentationRequest(out var request))
            {
                request.ResetRequested = 1;
                SetPresentationRequest(request);
                SetCueState<CuePlayingTag>(false);
            }

            SetCueState<CuePlayableTag>(true);
        }

        /// <summary>
        /// 停止Cue
        /// </summary>
        /// <param name="immediate"> 是否立即停止 </param>
        public void Stop(bool immediate = false)
        {
            SetCueState<CuePlayableTag>(false);
        }

        public void StopImmediate() => Stop(true);

        public void KillSelf()
        {
            SetCueState<CueKillRequestTag>(true);
        }

        public void RemoveSelf()
        {
            StopImmediate();
            if (!TryGetPresentationRequest(out var request))
                return;

            request.RemoveTargetRequested = 1;
            SetPresentationRequest(request);
        }

        internal bool CanPlayRuntime() => CanPlay();

        public Entity GetSourceEffectEntity()
        {
            if (_sourceType != CueSourceType.GameplayEffect) return Entity.Null;
            if (_sourceEntity == Entity.Null || !HasRuntimeContext() || !EntityManager.Exists(_sourceEntity)) return Entity.Null;
            return _sourceEntity;
        }

        public Entity GetSourceAbilityEntity()
        {
            if (_sourceType != CueSourceType.GameplayAbility) return Entity.Null;
            if (_sourceEntity == Entity.Null || !HasRuntimeContext() || !EntityManager.Exists(_sourceEntity)) return Entity.Null;
            return _sourceEntity;
        }

        private void SetCueState<T>(bool enabled)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            if (!HasRuntimeContext())
                return;

            if (_cueEntity != Entity.Null
                && EntityManager.Exists(_cueEntity)
                && EntityManager.HasComponent<CueRuntimeActiveTag>(_cueEntity)
                && EntityManager.IsComponentEnabled<CueRuntimeActiveTag>(_cueEntity)
                && EntityManager.HasComponent<T>(_cueEntity))
            {
                EntityManager.SetComponentEnabled<T>(_cueEntity, enabled);
            }
        }

        private bool TryGetPresentationRequest(out CuePresentationRequestComponent request)
        {
            request = default;
            if (!HasRuntimeContext()
                || _cueEntity == Entity.Null
                || !EntityManager.Exists(_cueEntity)
                || !EntityManager.HasComponent<CuePresentationRequestComponent>(_cueEntity))
            {
                return false;
            }

            request = EntityManager.GetComponentData<CuePresentationRequestComponent>(_cueEntity);
            return true;
        }

        private void SetPresentationRequest(in CuePresentationRequestComponent request)
        {
            if (!HasRuntimeContext()
                || _cueEntity == Entity.Null
                || !EntityManager.Exists(_cueEntity)
                || !EntityManager.HasComponent<CuePresentationRequestComponent>(_cueEntity))
            {
                return;
            }

            EntityManager.SetComponentData(_cueEntity, request);
        }

        private bool HasRuntimeContext()
        {
            return EntityManager.World != null && EntityManager.World.IsCreated;
        }

        #region system function

        public virtual void OnAdd(float time)
        {
        }

        public virtual void OnRemove(float time)
        {
        }

        public virtual void OnActivate(float time)
        {
        }

        public virtual void OnDeactivate(float time)
        {
        }

        public virtual void OnTick(float time)
        {
        }

        public virtual void OnDestroy(float time)
        {
        }

#if UNITY_EDITOR
        /// <summary>
        ///     编辑器预览Cue效果
        ///     注意：该方法只在编辑器下有效，运行时无效。
        ///     请使用 UNITY_EDITOR 宏来包裹该方法，否则在运行时会导致编译错误。
        /// </summary>
        /// <param name="target"></param>
        /// <param name="frame"></param>
        /// <param name="startFrame"></param>
        /// <param name="endFrame"></param>
        public virtual void OnPreview(GameObject target, int frame, int startFrame, int endFrame)
        {

        }
#endif

        #endregion
    }

    public abstract class GameplayCueBase<T> : GameplayCueBase where T : XParam
    {
        public T Parameter { get; private set; }

        public override void InitParameters(XParam xParam)
        {
            if (xParam is T t)
                Parameter = t;
#if UNITY_EDITOR
            else
                Debug.LogError($"Parameter type mismatch: expected {typeof(T)}, but got {xParam.GetType()}");
#endif
        }
    }
}
