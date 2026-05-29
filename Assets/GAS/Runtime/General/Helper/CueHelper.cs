using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    public static class CueHelper
    {
        public static GameplayCueBase TryCreateCue(GameplayCueConfig param)
        {
            return TryCreateCue(param.CueType, param.Param);
        }

        public static GameplayCueBase TryCreateCue(string cueType, XParam param)
        {
            if (CueTypeMap.TryGetValue(cueType, out var type))
                return TryCreateCue(type, param);
#if UNITY_EDITOR
            Debug.LogError($"[EX] 创建Cue失败:Can't find Cue for cueType [{cueType}]. " +
                           "Cue的Type映射脚本错误，请重新生成。");
#endif
            return null;
        }

        public static GameplayCueBase TryCreateCue(Type type, XParam param)
        {
            try
            {
                if (Activator.CreateInstance(type) is GameplayCueBase cue)
                {
                    cue.InitParameters(param);
                    return cue;
                }
            }
            catch (MissingMethodException e)
            {
                Debug.LogError("[EX] 创建Cue失败: " +
                               $"请检查这个类【'{type.FullName}'】是否继承自NewGameplayCueBase;" +
                               "或者，GameplayCueBase的Type映射脚本是否更新，重新生成。" +
                               $"Error Exception:{e.Message}");
                throw;
            }
            return null;
        }

        public static XParam CreateCueParameter(string type, List<object> paramData = null)
        {
            var cueParamConfigType = GetCueLogicParamType(type);
            var cueParamEditor = (XParam)Activator.CreateInstance(cueParamConfigType);
#if UNITY_EDITOR
            if (paramData != null) cueParamEditor.DecodeExcelData(paramData);
#endif
            return cueParamEditor;
        }

        public static Type GetCueLogicParamType(string cueType)
        {
            return CueType2CueParamTypeMap.TryGetValue(cueType,out var cueParam) ? CueParamTypeMap[cueParam] : null;
        }

        public static Type GetCueLogicParamType(Type cueType)
        {
            var cueParam = CueType2CueParamTypeMap[cueType.Name];
            return CueParamTypeMap[cueParam];
        }

        #region Cue

        private static readonly Dictionary<string, Type> CueTypeMap = new();
        private static readonly Dictionary<string, Type> CueParamTypeMap = new();
        private static readonly Dictionary<string, string> CueType2CueParamTypeMap = new();

        public static void RegisterCue(string sType, Type logicType,Type cueParamType)
        {
            CueTypeMap[sType] = logicType;
            CueParamTypeMap[cueParamType.Name] = cueParamType;
            CueType2CueParamTypeMap[sType] = cueParamType.Name;
        }

        public static Type GetCueType(string sType)
        {
            if (CueTypeMap.TryGetValue(sType, out var type)) return type;
#if UNITY_EDITOR
            Debug.LogError($"[EX] CueTypeMap中没有找到类型: {sType}，请检查是否注册了该Cue类型。");
#endif
            return null;
        }

        public static void RegisterCue<T>(string sType,Type cueParam) where T : GameplayCueBase
        {
            RegisterCue(sType, typeof(T),cueParam);
        }

        public static List<string> GetCueTypeNames()
        {
            return CueTypeMap.Keys.ToList();
        }

        #endregion

        public static void StopCue(Entity cueEntity,EntityManager entityManager)
        {
            if (cueEntity != Entity.Null
                && entityManager.Exists(cueEntity)
                && entityManager.HasComponent<CuePlayingTag>(cueEntity)
                && entityManager.HasComponent<CuePlayableTag>(cueEntity)
                && entityManager.IsComponentEnabled<CuePlayingTag>(cueEntity))
            {
                entityManager.SetComponentEnabled<CuePlayableTag>(cueEntity,false);
            }
        }

        public static void PlayCue(Entity cueEntity,EntityManager entityManager)
        {
            if (cueEntity != Entity.Null
                && entityManager.Exists(cueEntity)
                && entityManager.HasComponent<CuePlayingTag>(cueEntity)
                && entityManager.HasComponent<CuePlayableTag>(cueEntity)
                && !entityManager.IsComponentEnabled<CuePlayingTag>(cueEntity))
            {
                entityManager.SetComponentEnabled<CuePlayableTag>(cueEntity,true);
            }
        }

        public static CueManagedInstanceComponent InitInstantCueFromGameplayEffect(CueManagedInstanceComponent cue,Entity cueEntity,Entity ge)
        {
            cue.Cue.SetSourceEntity(ge,CueSourceType.GameplayEffect);
            cue.Cue.SetCueEntity(cueEntity);
            return cue;
        }

        public static CueManagedInstanceComponent CopyCueComponent(CueManagedInstanceComponent cue)
        {
            return new CueManagedInstanceComponent()
            {
                Cue = cue.Cue
            };
        }

        #region 通用型工具接口

        public static void TryPlayCueOnAsc(EntityManager entityManager, Entity targetAsc, Entity cueEntity, Entity sourceGE)
        {
            TryPlayCueOnAsc(entityManager, targetAsc, cueEntity, sourceGE, CueSourceType.GameplayEffect);
        }

        public static void TryPlayCueOnAsc(
            EntityManager entityManager,
            Entity targetAsc,
            Entity cueEntity,
            Entity sourceEntity,
            CueSourceType sourceType)
        {
            // 1.先判断tag是否可以播放cue
            if (cueEntity == Entity.Null
                || targetAsc == Entity.Null
                || !entityManager.Exists(cueEntity)
                || !entityManager.Exists(targetAsc)
                || !entityManager.HasComponent<CueManagedInstanceComponent>(cueEntity))
            {
                return;
            }

            if (entityManager.HasComponent<CueRequiredTagsComponent>(cueEntity)
                && entityManager.IsComponentEnabled<CueRequiredTagsComponent>(cueEntity))
            {
                var requiredTags = entityManager.GetComponentData<CueRequiredTagsComponent>(cueEntity);
                if (!TagRequirementEvaluator
                    .EvaluateRequired(entityManager, targetAsc, requiredTags.requirement)
                    .Passed)
                {
                    return;
                }
            }
            if (entityManager.HasComponent<CueImmunityTagsComponent>(cueEntity)
                && entityManager.IsComponentEnabled<CueImmunityTagsComponent>(cueEntity))
            {
                var immunityTags = entityManager.GetComponentData<CueImmunityTagsComponent>(cueEntity);
                if (!TagRequirementEvaluator
                    .EvaluateImmunity(entityManager, targetAsc, immunityTags.requirement)
                    .Passed)
                {
                    return;
                }
            }
            // 2.重置Cue逻辑单元
            var cueLogic = entityManager.GetComponentData<CueManagedInstanceComponent>(cueEntity);
            cueLogic.Cue.Reset();
            cueLogic.Cue.SetSourceEntity(sourceEntity, sourceType);
            cueLogic.Cue.AddToTargetAsc(targetAsc);
            // 3.激活CuePlaying
            cueLogic.Cue.Play(true);
        }
        #endregion
    }
}
