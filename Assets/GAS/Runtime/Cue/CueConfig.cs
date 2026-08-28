using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 保存一个 Cue 的可变 authoring 类型描述与参数快照；它是配置/表现边界 DTO，不拥有 Runtime Core 状态。
    /// </summary>
    [Serializable]
    public sealed class GameplayCueConfig
    {
        /// <summary>
        /// Cue 的表现逻辑类型。
        /// </summary>
        public Type CueType { get; private set; }

        /// <summary>
        /// Cue 的参数 authoring 值。
        /// </summary>
        public XParam Param { get; private set; }

        /// <summary>
        /// 配置表中的 required tags 字段语义，返回 All 集合。
        /// </summary>
        public int[] RequiredTags
        {
            get => RequiredAllTags;
            set => RequiredAllTags = value;
        }

        /// <summary>
        /// 配置表中的 immunity tags 字段语义，返回 Any 集合。
        /// </summary>
        public int[] ImmunityTags
        {
            get => ImmunityAnyTags;
            set => ImmunityAnyTags = value;
        }

        public int[] RequiredAllTags { get; private set; }
        public int[] RequiredAnyTags { get; private set; }
        public int[] RequiredNoneTags { get; private set; }
        public int[] ImmunityAllTags { get; private set; }
        public int[] ImmunityAnyTags { get; private set; }
        public int[] ImmunityNoneTags { get; private set; }

        /// <summary>
        /// 创建一个带简化 Tag 要求的 Cue 配置。
        /// </summary>
        public GameplayCueConfig(Type cueType, XParam param, int[] requiredTags = null, int[] immunityTags = null)
        {
            CueType = cueType;
            Param = param;
            SetRequiredTagRequirement(requiredTags, Array.Empty<int>(), Array.Empty<int>());
            SetImmunityTagRequirement(Array.Empty<int>(), immunityTags, Array.Empty<int>());
        }

        /// <summary>
        /// 创建一个带完整 Tag 要求的 Cue 配置。
        /// </summary>
        public GameplayCueConfig(
            Type cueType,
            XParam param,
            int[] requiredAllTags,
            int[] requiredAnyTags,
            int[] requiredNoneTags,
            int[] immunityAllTags,
            int[] immunityAnyTags,
            int[] immunityNoneTags)
        {
            CueType = cueType;
            Param = param;
            SetRequiredTagRequirement(requiredAllTags, requiredAnyTags, requiredNoneTags);
            SetImmunityTagRequirement(immunityAllTags, immunityAnyTags, immunityNoneTags);
        }

        /// <summary>
        /// 更新表现类型与参数快照。
        /// </summary>
        public void SetCueTypeAndParameter(Type cueType, XParam param)
        {
            CueType = cueType;
            Param = param;
        }

        /// <summary>
        /// 设置简化的 Required Tag 集合。
        /// </summary>
        public void SetRequiredTags(int[] tags)
        {
            SetRequiredTagRequirement(tags, Array.Empty<int>(), Array.Empty<int>());
        }

        /// <summary>
        /// 设置简化的 Immunity Tag 集合。
        /// </summary>
        public void SetImmunityTags(int[] tags)
        {
            SetImmunityTagRequirement(Array.Empty<int>(), tags, Array.Empty<int>());
        }

        /// <summary>
        /// 设置完整的 Required Tag 要求。
        /// </summary>
        public void SetRequiredTagRequirement(int[] all, int[] any, int[] none)
        {
            RequiredAllTags = all ?? Array.Empty<int>();
            RequiredAnyTags = any ?? Array.Empty<int>();
            RequiredNoneTags = none ?? Array.Empty<int>();
        }

        /// <summary>
        /// 设置完整的 Immunity Tag 要求。
        /// </summary>
        public void SetImmunityTagRequirement(int[] all, int[] any, int[] none)
        {
            ImmunityAllTags = all ?? Array.Empty<int>();
            ImmunityAnyTags = any ?? Array.Empty<int>();
            ImmunityNoneTags = none ?? Array.Empty<int>();
        }

        /// <summary>
        /// 根据当前配置创建纯托管 Cue 表现对象。
        /// </summary>
        public GameplayCueBase CreateCue()
        {
            return CueHelper.TryCreateCue(CueType, Param);
        }
    }
}
