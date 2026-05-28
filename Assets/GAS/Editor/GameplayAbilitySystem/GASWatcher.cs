using System;
using System.Collections.Generic;
using GAS.Runtime;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace GAS.Editor
{
    public class GASWatcher : OdinEditorWindow
    {
        private const string OpenWindow_MenuItemName = "EXTool/EX-GAS/监测台";
#if EX_GAS_ENABLE_HOT_KEYS
        private const string OpenWindow_MenuItemNameEnh = OpenWindow_MenuItemName + " %F11";
#else
        private const string OpenWindow_MenuItemNameEnh = OpenWindow_MenuItemName;
#endif

        [MenuItem(OpenWindow_MenuItemNameEnh, priority = 3)]
        private static void OpenWindow()
        {
            var window = GetWindow<GASWatcher>();
            window.titleContent = new GUIContent("EX-GAS监测台");
            window.Show();
        }

        // ======================== 提示 ========================
        [BoxGroup("Tip")]
        [HideIf(nameof(IsEditorPlaying))]
        [DisplayAsString(false, 16, TextAlignment.Center, true)]
        [ShowInInspector]
        [HideLabel]
        public string Tip = "<b><color=#ff6988>EX-GAS监测台仅在游戏运行时生效。</color></b>";

        // ======================== ASC 选择器 ========================
        [VerticalGroup("ASC")]
        [HorizontalGroup("ASC/Top")]
        [ValueDropdown(nameof(AscEntityChoices), IsUniqueList = true, HideChildProperties = true)]
        [ShowIf(nameof(IsEditorPlaying))]
        [ShowInInspector]
        [HideLabel]
        [OnValueChanged(nameof(OnWatchEntityChanged))]
        public Entity entityWatching = Entity.Null;

        [HorizontalGroup("ASC/Top", order: 0)]
        [ShowIf(nameof(IsEditorPlaying))]
        [DisplayAsString(EnableRichText = true)]
        [ShowInInspector]
        [HideLabel]
        public string ascName =>
            $"<b><color=yellow>{(entityWatching == Entity.Null ? "NULL" : EntityHelper.GetEntityName(entityWatching))}</color></b>";

        // ======================== 全局信息栏 ========================
        [VerticalGroup("ASC")]
        [ShowIf(nameof(IsEntityValid))]
        [ShowInInspector]
        [DisplayAsString(EnableRichText = true)]
        [HideLabel]
        public string GlobalInfo => _globalInfoText;
        private string _globalInfoText = "";

        // ======================== 属性区块 ========================
        [FoldoutGroup("ASC/属性", expanded: true)]
        [ShowIf(nameof(IsEntityValid))]
        [ShowInInspector]
        [DisplayAsString(EnableRichText = true)]
        [HideLabel]
        [ListDrawerSettings(IsReadOnly = true, Expanded = true)]
        private List<string> _ascAttributes = new();

        // ======================== 标签区块 ========================
        [FoldoutGroup("ASC/标签", expanded: true)]
        [ShowIf(nameof(IsEntityValid))]
        [ShowInInspector]
        [DisplayAsString(EnableRichText = true)]
        [HideLabel]
        [ListDrawerSettings(IsReadOnly = true, Expanded = true)]
        private List<string> _ascTags = new();

        // ======================== 能力区块 ========================
        [FoldoutGroup("ASC/能力", expanded: true)]
        [ShowIf(nameof(IsEntityValid))]
        [ShowInInspector]
        [DisplayAsString(EnableRichText = true)]
        [HideLabel]
        [ListDrawerSettings(IsReadOnly = true, Expanded = true)]
        private List<string> _ascAbilities = new();

        // ======================== GE效果区块 ========================
        [FoldoutGroup("ASC/GE效果(Buff)", expanded: true)]
        [ShowIf(nameof(IsEntityValid))]
        [ShowInInspector]
        [DisplayAsString(EnableRichText = true)]
        [HideLabel]
        [ListDrawerSettings(IsReadOnly = true, Expanded = true)]
        private List<string> _ascGameplayEffects = new();

        // ======================== 刷新控制 ========================
        private double _lastRepaintTime;
        private const double RepaintInterval = 0.1; // 100ms

        private void Update()
        {
            if (!IsEntityValid()) return;

            var now = EditorApplication.timeSinceStartup;
            if (now - _lastRepaintTime < RepaintInterval) return;
            _lastRepaintTime = now;

            RefreshASCContent();
        }

        public bool IsEditorPlaying() => Application.isPlaying;

        public bool IsEntityValid()
        {
            return IsEditorPlaying()
                   && entityWatching != Entity.Null
                   && GASManager.EntityManager.Exists(entityWatching);
        }

        private void OnWatchEntityChanged()
        {
            ClearAllCaches();
            if (IsEntityValid()) RefreshASCContent();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode &&
                state != PlayModeStateChange.ExitingPlayMode) return;
            _cachedAscEntities.Clear();
            entityWatching = Entity.Null;
            ClearAllCaches();
        }

        private static void ClearAllCaches()
        {
            _abilityNameCache.Clear();
            _attributeNameCache.Clear();
            _attrSetNameCache.Clear();
        }

        #region ASC列表 & 名称缓存

        private static readonly List<(string, Entity)> _cachedAscEntities = new();

        [HorizontalGroup("ASC/Top", width: 200)]
        [Button("刷新当前ASC列表")]
        [ShowIf(nameof(IsEditorPlaying))]
        private static void RefreshAscEntitiesCache()
        {
            _cachedAscEntities.Clear();
            var ascEntities = EntityQueryHelper.GetAllEntitiesWithComponent<ASCIdentityComponent>();
            foreach (var ascEntity in ascEntities)
                _cachedAscEntities.Add((GASManager.EntityManager.GetName(ascEntity), ascEntity));
            _ascEntityChoices = new ValueDropdownItem[_cachedAscEntities.Count];
            for (var i = 0; i < _cachedAscEntities.Count; i++)
            {
                var (name, entity) = _cachedAscEntities[i];
                _ascEntityChoices[i] = new ValueDropdownItem(name, entity);
            }
        }

        private static ValueDropdownItem[] _ascEntityChoices;
        private static IEnumerable<ValueDropdownItem> AscEntityChoices =>
            _ascEntityChoices ?? new ValueDropdownItem[] { };

        // ---------- 名称缓存 ----------
        private static readonly Dictionary<int, string> _abilityNameCache = new();
        private static readonly Dictionary<int, string> _attributeNameCache = new();
        private static readonly Dictionary<int, string> _attrSetNameCache = new();

        private static string GetAbilityNameByCode(int code)
        {
            if (_abilityNameCache.TryGetValue(code, out var c)) return c;
            var name = EXEditorHelper.InvokeStaticXLubanMethod("GetAbilityNameByCode", code);
            var r = name != null ? name as string : "未知技能";
            _abilityNameCache[code] = r;
            return r;
        }

        private static string GetAttributeNameByCode(int code)
        {
            if (_attributeNameCache.TryGetValue(code, out var c)) return c;
            var name = EXEditorHelper.InvokeStaticXLubanMethod("GetAttributeNameByCode", code);
            var r = name != null ? name as string : "未知属性";
            _attributeNameCache[code] = r;
            return r;
        }

        private static string GetAttrSetNameByCode(int code)
        {
            if (_attrSetNameCache.TryGetValue(code, out var c)) return c;
            var name = EXEditorHelper.InvokeStaticXLubanMethod("GetAttrSetNameByCode", code);
            var r = name != null ? name as string : "未知属性集";
            _attrSetNameCache[code] = r;
            return r;
        }

        private static string GetTagName(int tagCode) =>
            TagHelper.GetTagFullName(tagCode) ?? $"Tag({tagCode})";

        private static string OpName(EModifierOp op) => op switch
        {
            EModifierOp.Add => "+",
            EModifierOp.Subtract => "-",
            EModifierOp.Multiply => "×",
            EModifierOp.Divide => "÷",
            EModifierOp.Override => "=",
            _ => op.ToString()
        };

        #endregion

        #region 数据刷新

        private void RefreshASCContent()
        {
            if (!IsEntityValid()) return;
            try
            {
                RefreshGlobalInfo();
                RefreshAttributes();
                RefreshTags();
                RefreshAbilities();
                RefreshGameplayEffects();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GASWatcher] Refresh: {e.Message}");
            }
            Repaint();
        }

        // ---------- 全局信息 ----------
        private void RefreshGlobalInfo()
        {
            var em = GASManager.EntityManager;
            var gt = em.GetComponentData<GlobalTimer>(GASManager.EntityGlobalTimer);
            var lvl = em.HasComponent<ASCIdentityComponent>(entityWatching)
                ? em.GetComponentData<ASCIdentityComponent>(entityWatching).Level
                : 0;
            _globalInfoText =
                $"<b>Frame</b>:{gt.Frame}  <b>Turn</b>:{gt.Turn}  <b>ASC Lv</b>:{lvl}  <b>Entity</b>:{EntityHelper.GetEntityName(entityWatching)}";
        }

        // ---------- 属性 ----------
        private void RefreshAttributes()
        {
            _ascAttributes.Clear();
            var em = GASManager.EntityManager;
            if (!em.HasBuffer<AttributeValueBuffer>(entityWatching))
            {
                _ascAttributes.Add("<color=#888>无</color>");
                return;
            }

            var buf = em.GetBuffer<AttributeValueBuffer>(entityWatching);
            foreach (var a in buf)
            {
                var diff = Math.Abs(a.CurrentValue - a.BaseValue) > 0.001f;
                var cc = diff ? "<color=orange>" : "<color=white>";
                var dirty = a.Dirty ? " <color=red>●</color>" : "";
                _ascAttributes.Add(
                    $"  {GetAttributeNameByCode(a.Code)}: {cc}{a.CurrentValue:F2}</color> (Base:{a.BaseValue:F2}){dirty}");
            }
        }

        // ---------- 标签 ----------
        private void RefreshTags()
        {
            _ascTags.Clear();
            var em = GASManager.EntityManager;
            var fixedMask = em.HasComponent<TagFixedMaskComponent>(entityWatching)
                ? em.GetComponentData<TagFixedMaskComponent>(entityWatching).Mask
                : default;

            _ascTags.Add($"<b>固有({CountDenseTags(fixedMask)})</b>");
            AppendMaskTags(_ascTags, fixedMask);
            if (!em.HasBuffer<TagTemporarySourceBuffer>(entityWatching))
            {
                _ascTags.Add("<b>临时(0)</b>");
                return;
            }

            var tmpBuf = em.GetBuffer<TagTemporarySourceBuffer>(entityWatching);
            _ascTags.Add($"<b>临时({tmpBuf.Length})</b>");
            foreach (var t in tmpBuf)
            {
                var n = GetDenseTagName(t.TagIndex);
                var src = EntityHelper.GetEntityName(t.Source);
                if (n != null) _ascTags.Add($"  {n} <color=#888>← {src}</color>");
            }
        }

        private static int CountDenseTags(in TagMaskComponent mask)
        {
            var count = 0;
            for (var i = 0; i < 256; i++)
                if (mask.HasTag(i))
                    count++;
            return count;
        }

        private static void AppendMaskTags(List<string> output, in TagMaskComponent mask)
        {
            for (var i = 0; i < 256; i++)
            {
                if (!mask.HasTag(i)) continue;
                var n = GetDenseTagName(i);
                if (n != null) output.Add($"  {n}");
            }
        }

        private static string GetDenseTagName(int denseIndex)
        {
            return TagHelper.TryGetTagCode(denseIndex, out var tagCode)
                ? TagHelper.GetTagFullName(tagCode)
                : $"TagIndex({denseIndex})";
        }

        // ---------- 能力 ----------
        private void RefreshAbilities()
        {
            _ascAbilities.Clear();
            var em = GASManager.EntityManager;
            var buf = em.GetBuffer<AbilitySlotBuffer>(entityWatching);
            if (buf.Length == 0)
            {
                _ascAbilities.Add("<color=#888>无</color>");
                return;
            }

            foreach (var ab in buf)
            {
                var ent = ab.AbilityEntity;
                if (!em.Exists(ent) || !em.HasComponent<AbilityStateComponent>(ent)) continue;
                var info = em.GetComponentData<AbilityStateComponent>(ent);
                var aName = GetAbilityNameByCode(info.Code);
                var eName = EntityHelper.GetEntityName(ent);
                var phase = em.HasComponent<AbilityStateComponent>(ent)
                    ? em.GetComponentData<AbilityStateComponent>(ent).Phase
                    : EAbilityPhase.Ready;
                var active = phase is EAbilityPhase.Activating or EAbilityPhase.Active;
                var actStr = active ? $" <color=lime>[{phase}]</color>" : $" <color=#888>[{phase}]</color>";
                _ascAbilities.Add(
                    $"<b><color=#ffcc44>{aName}</color></b> Lv.{info.Level} [{eName}]{actStr}");

                // --- ECS 执行配置 ---
                if (em.HasBuffer<AbilityOwnerEffectOnActivateBuffer>(ent))
                {
                    var effects = em.GetBuffer<AbilityOwnerEffectOnActivateBuffer>(ent);
                    var effectCodes = new List<string>();
                    for (var ei = 0; ei < effects.Length; ei++)
                        effectCodes.Add(effects[ei].EffectCode.ToString());
                    _ascAbilities.Add($"  <color=#aaaaaa>激活效果: {string.Join(", ", effectCodes)}</color>");
                }

                if (em.HasComponent<AbilityMoveInputComponent>(ent))
                {
                    var move = em.GetComponentData<AbilityMoveInputComponent>(ent);
                    _ascAbilities.Add($"  <color=#aaaaaa>MoveInput.RotationOffset: {move.RotationOffset}</color>");
                }
            }
        }

        private static List<string> GetDenseTagNames(in TagMaskComponent mask)
        {
            var names = new List<string>();
            for (var tagIndex = 0; tagIndex < TagMaskComponent.Capacity; tagIndex++)
            {
                if (mask.HasTag(tagIndex))
                    names.Add(GetDenseTagName(tagIndex));
            }

            return names;
        }

        private static bool HasAnyDenseTag(EntityManager em, Entity asc, in TagMaskComponent denseTags)
        {
            if (!em.Exists(asc) || !em.HasComponent<TagMaskComponent>(asc)) return false;
            var mask = em.GetComponentData<TagMaskComponent>(asc);
            return mask.HasAnyTag(denseTags);
        }

        private static bool HasAnyDenseTag(DynamicBuffer<GEGrantedTagConfigBuffer> grantedTags, in TagMaskComponent denseTags)
        {
            for (var i = 0; i < grantedTags.Length; i++)
            {
                if (denseTags.HasTag(grantedTags[i].TagIndex))
                    return true;
            }
            return false;
        }

        // ---------- GE效果 ----------
        private void RefreshGameplayEffects()
        {
            _ascGameplayEffects.Clear();
            var em = GASManager.EntityManager;
            var gt = em.GetComponentData<GlobalTimer>(GASManager.EntityGlobalTimer);
            var geBuf = em.GetBuffer<LegacyGameplayEffectEntityBuffer>(entityWatching);

            if (geBuf.Length == 0)
            {
                _ascGameplayEffects.Add("<color=#888>无</color>");
                return;
            }

            for (var i = 0; i < geBuf.Length; i++)
            {
                var geEntity = geBuf[i].GameplayEffect;
                var geName = em.GetName(geEntity);

                if (geName == null || geName == "ENTITY_NOT_FOUND")
                {
                    _ascGameplayEffects.Add(
                        "<color=red>ERROR: GE已被销毁，但未被移出容器！</color>");
                    continue;
                }

                // 基本信息
                var context = em.GetComponentData<GEContextComponent>(geEntity);
                var source = EntityHelper.GetEntityName(context.SourceAsc);
                var level = em.HasComponent<GEEffectSpecComponent>(geEntity)
                    ? em.GetComponentData<GEEffectSpecComponent>(geEntity).Level
                    : 0;
                _ascGameplayEffects.Add(
                    $"<b><color=#ffcc44>[{i}] {geName}</color></b>  Lv.{level}  <color=#888>来源:{source}</color>");

                // Duration
                if (em.HasComponent<GEDurationRuntimeComponent>(geEntity))
                {
                    var dur = em.GetComponentData<GEDurationRuntimeComponent>(geEntity);
                    var definition = em.HasComponent<GEDurationDefinitionComponent>(geEntity)
                        ? em.GetComponentData<GEDurationDefinitionComponent>(geEntity)
                        : default;
                    var actStr = dur.Active
                        ? "<color=lime>[激活]</color>"
                        : "<color=red>[失活]</color>";
                    var unit = dur.ResolvedTimeUnit == TimeUnit.Frame ? "帧" : "回合";
                    string durStr;
                    if (dur.ResolvedDuration <= 0)
                    {
                        durStr = $"无限({unit})";
                    }
                    else
                    {
                        var cur = dur.ResolvedTimeUnit == TimeUnit.Frame ? gt.Frame : gt.Turn;
                        int rem;
                        if (definition.StopTickWhenDeactivated && !dur.Active)
                            rem = dur.RemainingTime;
                        else
                            rem = Math.Max(0, dur.ResolvedDuration - (cur - dur.ActiveTime));
                        durStr = $"剩余:{rem}/{dur.ResolvedDuration}{unit}";
                    }
                    _ascGameplayEffects.Add($"    {actStr} {durStr}");
                }

                // Stacking
                if (em.HasComponent<GEStackingDefinitionComponent>(geEntity))
                {
                    var definition = em.GetComponentData<GEStackingDefinitionComponent>(geEntity);
                    var runtime = em.HasComponent<GEStackingRuntimeComponent>(geEntity)
                        ? em.GetComponentData<GEStackingRuntimeComponent>(geEntity)
                        : default;
                    var stackCount = runtime.StackCount > 0 ? runtime.StackCount : 1;
                    var stType = definition.StackType == EffectStackType.AggregateBySource
                        ? "BySource"
                        : "ByTarget";
                    _ascGameplayEffects.Add(
                        $"    <color=#cc99ff>层数:{stackCount}/{definition.LimitCount} ({stType})</color>");
                }

                // Period
                if (em.HasComponent<GEPeriodDefinitionComponent>(geEntity))
                {
                    var per = em.GetComponentData<GEPeriodDefinitionComponent>(geEntity);
                    var unit = "帧";
                    if (em.HasComponent<GEDurationRuntimeComponent>(geEntity))
                    {
                        var dur = em.GetComponentData<GEDurationRuntimeComponent>(geEntity);
                        unit = dur.ResolvedTimeUnit == TimeUnit.Frame ? "帧" : "回合";
                    }
                    _ascGameplayEffects.Add(
                        $"    <color=#99cccc>周期:{per.Period}{unit}</color>");
                }

                // GrantedTags
                if (em.HasComponent<GEGrantedTagsComponent>(geEntity))
                {
                    var tags = em.GetComponentData<GEGrantedTagsComponent>(geEntity);
                    if (!tags.Tags.IsEmpty)
                    {
                        var names = GetDenseTagNames(tags.Tags);
                        _ascGameplayEffects.Add(
                            $"    <color=#88dd88>GrantedTags: {string.Join(", ", names)}</color>");
                    }
                }

                // AssetTags
                if (em.HasComponent<GEAssetTagsComponent>(geEntity))
                {
                    var tags = em.GetComponentData<GEAssetTagsComponent>(geEntity);
                    if (!tags.Tags.IsEmpty)
                    {
                        var names = GetDenseTagNames(tags.Tags);
                        _ascGameplayEffects.Add(
                            $"    <color=#aaaaaa>AssetTags: {string.Join(", ", names)}</color>");
                    }
                }

                // Modifiers
                if (em.HasBuffer<GEModifierConfigBuffer>(geEntity))
                {
                    var mods = em.GetBuffer<GEModifierConfigBuffer>(geEntity);
                    if (mods.Length > 0)
                    {
                        var parts = new List<string>();
                        foreach (var m in mods)
                        {
                            parts.Add(
                                $"{GetAttrSetNameByCode(m.AttrSetCode)}.{GetAttributeNameByCode(m.AttributeCode)} [{OpName(m.Op)}] {m.Magnitude:F2}");
                        }
                        _ascGameplayEffects.Add(
                            $"    <color=#ddaa66>Modifiers: {string.Join(" | ", parts)}</color>");
                    }
                }

                // 分隔线
                if (i < geBuf.Length - 1)
                    _ascGameplayEffects.Add(
                        "<color=#444>────────────────────────────────</color>");
            }
        }

        #endregion
    }
}
#endif

