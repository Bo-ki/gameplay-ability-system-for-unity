using System;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// 面向使用EX-GAS开发者的GameplayCue控制单位，可以理解为Cue面向对象开发的伪装类
    /// GameplayCue设计上允许作为一个独立系统被使用。因此给出了这个类，用于GAS外部使用。
    /// </summary>
    [Obsolete("GameplayCueUnit 的运行时 OOP facade 已退出 ECS Runtime 主链路。请通过 GameplayEffect Cue 配置或 CueRequestBuffer 驱动 Cue 表现。")]
    public class GameplayCueUnit
    {
        private readonly EntityManager _entityManager;
        private Entity _cueEntity;
        private Type _cueType;
        private XParam _xParam;
        private int[] _requiredAllTags;
        private int[] _requiredAnyTags;
        private int[] _requiredNoneTags;
        private int[] _immunityAllTags;
        private int[] _immunityAnyTags;
        private int[] _immunityNoneTags;
        public Type CueType => _cueType;
        public XParam Param => _xParam;
        /// <summary>
        /// GameplayCue独立控制单位
        /// </summary>
        /// <param name="cueType">Cue 类型</param>
        /// <param name="xParam">Cue 对应的自定义参数</param>
        /// <param name="requiredTags">可选：添加到ASC时，ASC播放需求的tag</param>
        /// <param name="immunityTags">>可选：添加到ASC时，ASC播放免疫的tag</param>
        public GameplayCueUnit(EntityManager entityManager, Type cueType,XParam xParam,int[] requiredTags = null, int[] immunityTags = null)
        {
            _entityManager = entityManager;
            _cueType = cueType;
            _xParam = xParam;
            _requiredAllTags = TagHelper.FilterInvalidTags(requiredTags);
            _requiredAnyTags = Array.Empty<int>();
            _requiredNoneTags = Array.Empty<int>();
            _immunityAllTags = Array.Empty<int>();
            _immunityAnyTags = TagHelper.FilterInvalidTags(immunityTags);
            _immunityNoneTags = Array.Empty<int>();
        }

        public GameplayCueUnit(EntityManager entityManager, GameplayCueConfig config)
        {
            _entityManager = entityManager;
            _cueType = config.CueType;
            _xParam = config.Param;
            _requiredAllTags = TagHelper.FilterInvalidTags(config.RequiredAllTags);
            _requiredAnyTags = TagHelper.FilterInvalidTags(config.RequiredAnyTags);
            _requiredNoneTags = TagHelper.FilterInvalidTags(config.RequiredNoneTags);
            _immunityAllTags = TagHelper.FilterInvalidTags(config.ImmunityAllTags);
            _immunityAnyTags = TagHelper.FilterInvalidTags(config.ImmunityAnyTags);
            _immunityNoneTags = TagHelper.FilterInvalidTags(config.ImmunityNoneTags);
        }

        private bool CheckCueEntity()
        {
            if (!IsRuntimeContextValid() || _cueEntity == Entity.Null || !_entityManager.Exists(_cueEntity))
            {
#if UNITY_EDITOR
                Debug.LogError($"[EX] cue运行用实例不存在，请先创建cue. 【 调用Create() 】");
#endif
                return false;
            }

            return true;
        }

        private bool EvaluateRequiredTags(Entity asc, int[] all, int[] any, int[] none)
        {
            if (!_entityManager.HasComponent<TagMaskComponent>(asc)) return false;

            var mask = _entityManager.GetComponentData<TagMaskComponent>(asc);
            bool passAll = all == null || all.Length == 0 || GameplayTagQuery.HasAllTags(mask, all);
            bool passAny = any == null || any.Length == 0 || GameplayTagQuery.HasAnyTag(mask, any);
            bool passNone = none == null || none.Length == 0 || !GameplayTagQuery.HasAnyTag(mask, none);
            return passAll && passAny && passNone;
        }

        private bool IsImmune(Entity asc)
        {
            if (!_entityManager.HasComponent<TagMaskComponent>(asc))
                return true;

            var requirement = TagHelper.BuildRequirementMask(
                _immunityAllTags,
                _immunityAnyTags,
                _immunityNoneTags);
            if (requirement.IsEmpty)
                return false;

            return !TagRequirementEvaluator.EvaluateImmunity(
                    _entityManager.GetComponentData<TagMaskComponent>(asc),
                    requirement,
                    asc)
                .Passed;
        }

        /// <summary>
        /// 创建GameplayCue运行用的实例
        /// </summary>
        public void Create()
        {
#if UNITY_EDITOR
            Debug.LogError("[EX] GameplayCueUnit.Create 已退出运行时架构：Cue 运行时实体必须由 ECS/配置链路创建，OOP facade 不能直接创建 Entity。");
#endif
            _cueEntity = Entity.Null;
        }

        /// <summary>
        ///  销毁GameplayCue运行用的实例
        /// </summary>
        public void Destroy()
        {
            if (!CheckCueEntity())
            {
#if UNITY_EDITOR
                Debug.LogError($"[EX] Cue没有创建过或已被销毁，不能重复销毁。");
#endif
                return;
            }

            RequestRemoveFromTarget();
            SetCueState<CueKillRequestTag>(true);
            _cueEntity = Entity.Null;
        }

        /// <summary>
        ///    添加GameplayCue到ASC
        /// </summary>
        /// <param name="asc"></param>
        /// <returns></returns>
        public bool AddToAsc(Entity asc)
        {
            if (!CheckCueEntity()) return false;

            if (!EvaluateRequiredTags(asc, _requiredAllTags, _requiredAnyTags, _requiredNoneTags)) return false;
            if (IsImmune(asc)) return false;

            if (!TryGetPresentationRequest(out var request))
                return false;

            request.TargetAsc = asc;
            request.AddTargetRequested = 1;
            SetPresentationRequest(request);
            return true;
        }


        /// <summary>
        ///     从ASC移除GameplayCue
        /// </summary>
        public void RemoveFromAsc()
        {
            if (!CheckCueEntity()) return;
            RequestRemoveFromTarget();
        }

        /// <summary>
        ///   播放GameplayCue
        /// </summary>
        public void Play()
        {
            if (!CheckCueEntity()) return;
            SetCueState<CuePlayableTag>(true);
        }

        /// <summary>
        ///     停止GameplayCue
        /// </summary>
        public void Stop()
        {
            if (!CheckCueEntity()) return;
            SetCueState<CuePlayableTag>(false);
        }

        /// <summary>
        ///   设置GameplayCue来源
        /// </summary>
        /// <param name="source"></param>
        /// <param name="sourceType"></param>
        public void SetSource(Entity source, CueSourceType sourceType)
        {
            if (!CheckCueEntity()) return;
            if (!TryGetPresentationRequest(out var request))
                return;

            request.SourceEntity = source;
            request.SourceType = sourceType;
            request.SourceUpdateRequested = 1;
            SetPresentationRequest(request);
        }

        private void RequestRemoveFromTarget()
        {
            if (!TryGetPresentationRequest(out var request))
                return;

            request.RemoveTargetRequested = 1;
            SetPresentationRequest(request);
        }

        private void SetCueState<T>(bool enabled)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            if (_cueEntity != Entity.Null
                && IsRuntimeContextValid()
                && _entityManager.Exists(_cueEntity)
                && _entityManager.HasComponent<CueRuntimeActiveTag>(_cueEntity)
                && _entityManager.IsComponentEnabled<CueRuntimeActiveTag>(_cueEntity)
                && _entityManager.HasComponent<T>(_cueEntity))
            {
                _entityManager.SetComponentEnabled<T>(_cueEntity, enabled);
            }
        }

        private bool TryGetPresentationRequest(out CuePresentationRequestComponent request)
        {
            request = default;
            if (_cueEntity == Entity.Null
                || !IsRuntimeContextValid()
                || !_entityManager.Exists(_cueEntity)
                || !_entityManager.HasComponent<CuePresentationRequestComponent>(_cueEntity))
            {
                return false;
            }

            request = _entityManager.GetComponentData<CuePresentationRequestComponent>(_cueEntity);
            return true;
        }

        private void SetPresentationRequest(in CuePresentationRequestComponent request)
        {
            if (_cueEntity != Entity.Null
                && IsRuntimeContextValid()
                && _entityManager.Exists(_cueEntity)
                && _entityManager.HasComponent<CuePresentationRequestComponent>(_cueEntity))
            {
                _entityManager.SetComponentData(_cueEntity, request);
            }
        }

        private bool IsRuntimeContextValid()
        {
            return _entityManager.World != null && _entityManager.World.IsCreated;
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
        public void OnPreview(GameObject target,int frame, int startFrame, int endFrame)
        {
            var cue = CueHelper.TryCreateCue(_cueType, _xParam);
            cue.OnPreview(target,frame, startFrame, endFrame);
        }
#endif
    }
}
