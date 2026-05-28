using System;
using System.Collections.Generic;
using UnityEngine;

namespace GAS.Runtime
{
    public static class TagHelper
    {
        private static Dictionary<int, GameplayTag> _tagMap;
        private static Dictionary<int, string> _tagCode2TagName;
        private static Dictionary<int, int> _tagCodeToDenseIndex;
        private static Dictionary<int, int> _tagDenseIndexToCode;
        
        /// <summary>
        ///     初始化TagMap
        /// </summary>
        /// <param name="tagMap"></param>
        /// <param name="tagCode2TagName"></param>
        public static void InitTagMap(Dictionary<int, GameplayTag> tagMap,Dictionary<int, string> tagCode2TagName)
        {
            _tagMap = tagMap;
            _tagCode2TagName = tagCode2TagName;
            _tagCodeToDenseIndex = new Dictionary<int, int>(tagMap.Count);
            _tagDenseIndexToCode = new Dictionary<int, int>(tagMap.Count);

            var denseIndex = 0;
            foreach (var tagCode in tagMap.Keys)
            {
                if (denseIndex >= 256)
                {
                    Debug.LogError("[EX-GAS] TagMaskComponent currently supports at most 256 gameplay tags. Regenerate a wider mask or reduce tag count.");
                    break;
                }

                _tagCodeToDenseIndex[tagCode] = denseIndex;
                _tagDenseIndexToCode[denseIndex] = tagCode;
                denseIndex++;
            }
        }

        /// <summary>
        ///     TagA是否含有TagB
        /// </summary>
        /// <param name="tagA"></param>
        /// <param name="tagB"></param>
        /// <returns></returns>
        public static bool HasTag(int tagA, int tagB)
        {
            if (_tagMap.ContainsKey(tagA) && _tagMap.ContainsKey(tagB))
                return _tagMap[tagA].HasTag(_tagMap[tagB]);
            return false;
        }

        public static bool TryGetDenseIndex(int tagCode, out int denseIndex)
        {
            denseIndex = -1;
            return _tagCodeToDenseIndex != null && _tagCodeToDenseIndex.TryGetValue(tagCode, out denseIndex);
        }

        public static bool TryGetTagCode(int denseIndex, out int tagCode)
        {
            tagCode = -1;
            return _tagDenseIndexToCode != null && _tagDenseIndexToCode.TryGetValue(denseIndex, out tagCode);
        }

        public static bool TryAddTagToMask(ref TagMaskComponent mask, int tagCode, bool includeParents = true)
        {
            if (!TryGetDenseIndex(tagCode, out var denseIndex))
                return false;

            mask.AddTag(denseIndex);

            if (includeParents && _tagMap != null && _tagMap.TryGetValue(tagCode, out var tag))
            {
                foreach (var parentCode in tag.Parents)
                    if (TryGetDenseIndex(parentCode, out var parentIndex))
                        mask.AddTag(parentIndex);
            }

            return true;
        }

        public static TagMaskComponent BuildMask(IEnumerable<int> tagCodes, bool includeParents = true)
        {
            var mask = new TagMaskComponent();
            if (tagCodes == null) return mask;

            foreach (var tagCode in tagCodes)
                TryAddTagToMask(ref mask, tagCode, includeParents);

            return mask;
        }

        public static TagRequirementMask BuildRequirementMask(
            IEnumerable<int> all,
            IEnumerable<int> any,
            IEnumerable<int> none,
            bool includeParents = true)
        {
            return new TagRequirementMask
            {
                All = BuildMask(all, includeParents),
                Any = BuildMask(any, includeParents),
                None = BuildMask(none, includeParents),
            };
        }

        public static int[] ToDenseIndices(IEnumerable<int> tagCodes, bool includeParents = false)
        {
            if (tagCodes == null) return Array.Empty<int>();

            var result = new List<int>();
            foreach (var tagCode in tagCodes)
            {
                if (TryGetDenseIndex(tagCode, out var denseIndex) && !result.Contains(denseIndex))
                    result.Add(denseIndex);

                if (!includeParents || _tagMap == null || !_tagMap.TryGetValue(tagCode, out var tag))
                    continue;

                foreach (var parentCode in tag.Parents)
                    if (TryGetDenseIndex(parentCode, out var parentIndex) && !result.Contains(parentIndex))
                        result.Add(parentIndex);
            }

            return result.ToArray();
        }

        public static int[] ToDenseIndices(in TagMaskComponent mask)
        {
            if (mask.IsEmpty) return Array.Empty<int>();

            var result = new List<int>();
            for (var i = 0; i < TagMaskComponent.Capacity; i++)
                if (mask.HasTag(i))
                    result.Add(i);
            return result.ToArray();
        }
        
        public static string GetTagFullName(int tagCode)
        {
            if (_tagCode2TagName.TryGetValue(tagCode, out var tagName))
            {
                return tagName;
            }
            
#if UNITY_EDITOR
            Debug.LogError($"[GEN] 标签码[{tagCode}]不存在!   请检查代码生成器是否正确生成了标签码!");
#endif
            return null;
        }
        
        /// <summary>
        /// 过滤掉无效的标签，在当前注册map里不存在的就认为是无效的标签。
        /// </summary>
        /// <param name="tags"></param>
        /// <param name="filterTag"></param>
        /// <returns></returns>
        public static int[] FilterInvalidTags(int[] tags)
        {
            if (_tagMap== null) return tags;
 
            var validTags = new List<int>();
            foreach (var tag in tags)
                if (_tagMap.ContainsKey(tag))
                    validTags.Add(tag);
            return validTags.ToArray();
        }
        
        public static List<int> FilterInvalidTags(List<int> tags)
        {
            if (_tagMap== null) return tags;
            
            var validTags = new List<int>();
            foreach (var tag in tags)
                if (_tagMap.ContainsKey(tag))
                    validTags.Add(tag);
            return validTags;
        }
    }
}
