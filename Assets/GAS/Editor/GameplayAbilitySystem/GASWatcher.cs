using System;
using System.Collections.Generic;
using GAS.Runtime;
using Unity.Entities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
namespace GAS.Editor
{
    public class GASWatcher : EditorWindow
    {
        private const string OpenWindow_MenuItemName = "EXTool/EX-GAS/监测台";
#if EX_GAS_ENABLE_HOT_KEYS
        private const string OpenWindow_MenuItemNameEnh = OpenWindow_MenuItemName + " %F11";
#else
        private const string OpenWindow_MenuItemNameEnh = OpenWindow_MenuItemName;
#endif

        private static readonly List<(string Name, Entity Entity)> _cachedAscEntities = new();
        private static readonly Dictionary<int, string> _abilityNameCache = new();
        private static readonly Dictionary<int, string> _attributeNameCache = new();
        private static readonly Dictionary<int, string> _attrSetNameCache = new();

        private readonly List<string> _ascAttributes = new();
        private readonly List<string> _ascTags = new();
        private readonly List<string> _ascAbilities = new();
        private readonly List<string> _ascGameplayEffects = new();

        private Entity _entityWatching = Entity.Null;
        private string _globalInfoText = string.Empty;
        private double _lastRefreshTime;

        private Label _countLabel;
        private Label _ascNameLabel;
        private Label _globalInfoLabel;
        private VisualElement _bodyHost;
        private VisualElement _selectorHost;
        private Foldout _attributesFoldout;
        private Foldout _tagsFoldout;
        private Foldout _abilitiesFoldout;
        private Foldout _gameplayEffectsFoldout;

        private const double RefreshInterval = 0.1;

        [MenuItem(OpenWindow_MenuItemNameEnh, priority = 3)]
        private static void OpenWindow()
        {
            var window = GetWindow<GASWatcher>();
            window.titleContent = new GUIContent("EX-GAS监测台");
            window.minSize = new Vector2(560, 460);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() =>
            {
                RefreshAscEntitiesCache();
                if (_entityWatching != Entity.Null && !ContainsCachedEntity(_entityWatching))
                    _entityWatching = Entity.Null;
                RebuildBody();
            })
            {
                text = "刷新当前ASC列表"
            });

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);

            _countLabel = new Label();
            _countLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _countLabel.style.minWidth = 80;
            toolbar.Add(_countLabel);
            rootVisualElement.Add(toolbar);

            _bodyHost = new VisualElement();
            _bodyHost.style.flexGrow = 1;
            _bodyHost.style.marginTop = 8;
            rootVisualElement.Add(_bodyHost);

            RebuildBody();
        }

        private void Update()
        {
            if (!IsEntityValid())
                return;

            var now = EditorApplication.timeSinceStartup;
            if (now - _lastRefreshTime < RefreshInterval)
                return;

            _lastRefreshTime = now;
            RefreshASCContent();
            UpdateDataViews();
        }

        private void RebuildBody()
        {
            if (_bodyHost == null)
                return;

            _bodyHost.Clear();
            UpdateCountLabel();

            if (!Application.isPlaying)
            {
                _bodyHost.Add(new HelpBox("EX-GAS监测台仅在游戏运行时生效。", HelpBoxMessageType.Info));
                return;
            }

            _selectorHost = new VisualElement();
            _selectorHost.style.marginBottom = 8;
            _bodyHost.Add(_selectorHost);
            RebuildAscSelector();

            _ascNameLabel = RichLabel(string.Empty);
            _ascNameLabel.style.marginBottom = 8;
            _bodyHost.Add(_ascNameLabel);

            if (!IsEntityValid())
            {
                UpdateAscNameLabel();
                _bodyHost.Add(new HelpBox("请选择一个有效的 ASC Entity。", HelpBoxMessageType.Info));
                return;
            }

            RefreshASCContent();

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            _bodyHost.Add(scrollView);

            _globalInfoLabel = RichLabel(string.Empty);
            _globalInfoLabel.style.marginBottom = 8;
            scrollView.Add(_globalInfoLabel);

            _attributesFoldout = CreateFoldout("属性");
            _tagsFoldout = CreateFoldout("标签");
            _abilitiesFoldout = CreateFoldout("能力");
            _gameplayEffectsFoldout = CreateFoldout("GE效果(Buff)");

            scrollView.Add(_attributesFoldout);
            scrollView.Add(_tagsFoldout);
            scrollView.Add(_abilitiesFoldout);
            scrollView.Add(_gameplayEffectsFoldout);

            UpdateDataViews();
        }

        private void RebuildAscSelector()
        {
            _selectorHost.Clear();

            if (_cachedAscEntities.Count == 0)
            {
                _selectorHost.Add(new HelpBox("当前没有缓存 ASC Entity，点击刷新当前ASC列表。", HelpBoxMessageType.Warning));
                return;
            }

            var choices = new List<string> { "<未选择>" };
            foreach (var item in _cachedAscEntities)
                choices.Add(item.Name);

            var selectedIndex = 0;
            for (var i = 0; i < _cachedAscEntities.Count; i++)
            {
                if (_cachedAscEntities[i].Entity == _entityWatching)
                {
                    selectedIndex = i + 1;
                    break;
                }
            }

            var popup = new PopupField<string>("当前 ASC", choices, selectedIndex);
            popup.RegisterValueChangedCallback(evt =>
            {
                var index = choices.IndexOf(evt.newValue);
                _entityWatching = index <= 0 ? Entity.Null : _cachedAscEntities[index - 1].Entity;
                OnWatchEntityChanged();
                RebuildBody();
            });
            _selectorHost.Add(popup);
        }

        private static Foldout CreateFoldout(string title)
        {
            var foldout = new Foldout
            {
                text = title,
                value = true
            };
            foldout.style.marginTop = 6;
            return foldout;
        }

        private void UpdateDataViews()
        {
            UpdateCountLabel();
            UpdateAscNameLabel();

            if (_globalInfoLabel == null)
                return;

            _globalInfoLabel.text = _globalInfoText;
            FillFoldout(_attributesFoldout, _ascAttributes);
            FillFoldout(_tagsFoldout, _ascTags);
            FillFoldout(_abilitiesFoldout, _ascAbilities);
            FillFoldout(_gameplayEffectsFoldout, _ascGameplayEffects);
        }

        private void UpdateCountLabel()
        {
            if (_countLabel != null)
                _countLabel.text = $"ASC: {_cachedAscEntities.Count}";
        }

        private void UpdateAscNameLabel()
        {
            if (_ascNameLabel == null)
                return;

            _ascNameLabel.text =
                $"<b><color=yellow>{(_entityWatching == Entity.Null ? "NULL" : EntityHelper.GetEntityName(_entityWatching))}</color></b>";
        }

        private static void FillFoldout(Foldout foldout, List<string> lines)
        {
            if (foldout == null)
                return;

            foldout.Clear();
            if (lines.Count == 0)
            {
                foldout.Add(RichLabel("<color=#888>无</color>"));
                return;
            }

            foreach (var line in lines)
                foldout.Add(RichLabel(line));
        }

        private static Label RichLabel(string text)
        {
            var label = new Label(text);
            label.enableRichText = true;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 2;
            return label;
        }

        private bool IsEntityValid()
        {
            return Application.isPlaying
                   && _entityWatching != Entity.Null
                   && GASManager.EntityManager.Exists(_entityWatching);
        }

        private void OnWatchEntityChanged()
        {
            ClearAllCaches();
            if (IsEntityValid())
                RefreshASCContent();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode &&
                state != PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            _cachedAscEntities.Clear();
            _entityWatching = Entity.Null;
            ClearAllCaches();
            RebuildBody();
        }

        private static bool ContainsCachedEntity(Entity entity)
        {
            foreach (var item in _cachedAscEntities)
            {
                if (item.Entity == entity)
                    return true;
            }

            return false;
        }

        private static void ClearAllCaches()
        {
            _abilityNameCache.Clear();
            _attributeNameCache.Clear();
            _attrSetNameCache.Clear();
        }

        private static void RefreshAscEntitiesCache()
        {
            _cachedAscEntities.Clear();
            var ascEntities = EntityQueryHelper.GetAllEntitiesWithComponent<ASCIdentityComponent>();
            foreach (var ascEntity in ascEntities)
                _cachedAscEntities.Add((GASManager.EntityManager.GetName(ascEntity), ascEntity));
        }

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

        private static string OpName(EModifierOp op) => op switch
        {
            EModifierOp.Add => "+",
            EModifierOp.Subtract => "-",
            EModifierOp.Multiply => "×",
            EModifierOp.Divide => "÷",
            EModifierOp.Override => "=",
            _ => op.ToString()
        };

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
        }

        private void RefreshGlobalInfo()
        {
            var em = GASManager.EntityManager;
            var gt = em.GetComponentData<GlobalTimer>(GASManager.EntityGlobalTimer);
            var lvl = em.HasComponent<ASCIdentityComponent>(_entityWatching)
                ? em.GetComponentData<ASCIdentityComponent>(_entityWatching).Level
                : 0;
            _globalInfoText =
                $"<b>Frame</b>:{gt.Frame}  <b>Turn</b>:{gt.Turn}  <b>ASC Lv</b>:{lvl}  <b>Entity</b>:{EntityHelper.GetEntityName(_entityWatching)}";
        }

        private void RefreshAttributes()
        {
            _ascAttributes.Clear();
            var em = GASManager.EntityManager;
            if (!em.HasBuffer<AttributeValueBuffer>(_entityWatching))
            {
                _ascAttributes.Add("<color=#888>无</color>");
                return;
            }

            var buf = em.GetBuffer<AttributeValueBuffer>(_entityWatching);
            foreach (var a in buf)
            {
                var diff = Math.Abs(a.CurrentValue - a.BaseValue) > 0.001f;
                var cc = diff ? "<color=orange>" : "<color=white>";
                var dirty = a.Dirty ? " <color=red>●</color>" : "";
                _ascAttributes.Add(
                    $"  {GetAttributeNameByCode(a.Code)}: {cc}{a.CurrentValue:F2}</color> (Base:{a.BaseValue:F2}){dirty}");
            }
        }

        private void RefreshTags()
        {
            _ascTags.Clear();
            var em = GASManager.EntityManager;
            var fixedMask = em.HasComponent<TagFixedMaskComponent>(_entityWatching)
                ? em.GetComponentData<TagFixedMaskComponent>(_entityWatching).Mask
                : default;

            _ascTags.Add($"<b>固有({CountDenseTags(fixedMask)})</b>");
            AppendMaskTags(_ascTags, fixedMask);
            if (!em.HasBuffer<TagTemporarySourceBuffer>(_entityWatching))
            {
                _ascTags.Add("<b>临时(0)</b>");
                return;
            }

            var tmpBuf = em.GetBuffer<TagTemporarySourceBuffer>(_entityWatching);
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
            {
                if (mask.HasTag(i))
                    count++;
            }

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

        private void RefreshAbilities()
        {
            _ascAbilities.Clear();
            var em = GASManager.EntityManager;
            var buf = em.GetBuffer<AbilitySlotBuffer>(_entityWatching);
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

        private void RefreshGameplayEffects()
        {
            _ascGameplayEffects.Clear();
            var em = GASManager.EntityManager;
            var gt = em.GetComponentData<GlobalTimer>(GASManager.EntityGlobalTimer);
            var geBuf = em.GetBuffer<LegacyGameplayEffectEntityBuffer>(_entityWatching);

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
                    _ascGameplayEffects.Add("<color=red>ERROR: GE已被销毁，但未被移出容器！</color>");
                    continue;
                }

                var context = em.GetComponentData<GEContextComponent>(geEntity);
                var source = EntityHelper.GetEntityName(context.SourceAsc);
                var level = em.HasComponent<GEEffectSpecComponent>(geEntity)
                    ? em.GetComponentData<GEEffectSpecComponent>(geEntity).Level
                    : 0;
                _ascGameplayEffects.Add(
                    $"<b><color=#ffcc44>[{i}] {geName}</color></b>  Lv.{level}  <color=#888>来源:{source}</color>");

                if (em.HasComponent<GEDurationRuntimeComponent>(geEntity))
                {
                    var dur = em.GetComponentData<GEDurationRuntimeComponent>(geEntity);
                    var definition = em.HasComponent<GEDurationDefinitionComponent>(geEntity)
                        ? em.GetComponentData<GEDurationDefinitionComponent>(geEntity)
                        : default;
                    var actStr = dur.Active ? "<color=lime>[激活]</color>" : "<color=red>[失活]</color>";
                    var unit = dur.ResolvedTimeUnit == GAS.Runtime.TimeUnit.Frame ? "帧" : "回合";
                    string durStr;
                    if (dur.ResolvedDuration <= 0)
                    {
                        durStr = $"无限({unit})";
                    }
                    else
                    {
                        var cur = dur.ResolvedTimeUnit == GAS.Runtime.TimeUnit.Frame ? gt.Frame : gt.Turn;
                        int rem;
                        if (definition.StopTickWhenDeactivated && !dur.Active)
                            rem = dur.RemainingTime;
                        else
                            rem = Math.Max(0, dur.ResolvedDuration - (cur - dur.ActiveTime));

                        durStr = $"剩余:{rem}/{dur.ResolvedDuration}{unit}";
                    }

                    _ascGameplayEffects.Add($"    {actStr} {durStr}");
                }

                if (em.HasComponent<GEStackingDefinitionComponent>(geEntity))
                {
                    var definition = em.GetComponentData<GEStackingDefinitionComponent>(geEntity);
                    var runtime = em.HasComponent<GEStackingRuntimeComponent>(geEntity)
                        ? em.GetComponentData<GEStackingRuntimeComponent>(geEntity)
                        : default;
                    var stackCount = runtime.StackCount > 0 ? runtime.StackCount : 1;
                    var stType = definition.StackType == EffectStackType.AggregateBySource ? "BySource" : "ByTarget";
                    _ascGameplayEffects.Add(
                        $"    <color=#cc99ff>层数:{stackCount}/{definition.LimitCount} ({stType})</color>");
                }

                if (em.HasComponent<GEPeriodDefinitionComponent>(geEntity))
                {
                    var per = em.GetComponentData<GEPeriodDefinitionComponent>(geEntity);
                    var unit = "帧";
                    if (em.HasComponent<GEDurationRuntimeComponent>(geEntity))
                    {
                        var dur = em.GetComponentData<GEDurationRuntimeComponent>(geEntity);
                        unit = dur.ResolvedTimeUnit == GAS.Runtime.TimeUnit.Frame ? "帧" : "回合";
                    }

                    _ascGameplayEffects.Add($"    <color=#99cccc>周期:{per.Period}{unit}</color>");
                }

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

                if (i < geBuf.Length - 1)
                    _ascGameplayEffects.Add("<color=#444>────────────────────────────────</color>");
            }
        }
    }
}
#endif
