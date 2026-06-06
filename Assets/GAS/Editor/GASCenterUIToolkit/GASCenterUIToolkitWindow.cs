using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GAS.Editor
{
    public sealed class GASCenterUIToolkitWindow : EditorWindow
    {
        private const string StyleSheetPath = "Assets/GAS/Editor/GASCenterUIToolkit/GASCenterUIToolkitWindow.uss";

        private readonly List<IGASCenterPage> _pages = new();
        private readonly Dictionary<string, Button> _navigationButtons = new();

        private GASCenterContext _context;
        private VisualElement _contentHost;
        private Label _pageTitle;
        private Label _pageDescription;
        private IGASCenterPage _currentPage;

        [MenuItem("EXTool/EX-GAS/GAS中心管理器", priority = -100)]
        public static void OpenWindow()
        {
            var window = GetWindow<GASCenterUIToolkitWindow>();
            window.titleContent = new GUIContent("EX-GAS Center");
            window.minSize = new Vector2(980, 560);
            window.position = new Rect(window.position.x, window.position.y, 1200, 680);
            window.Focus();
        }

        public void CreateGUI()
        {
            _context = new GASCenterContext(message => ShowNotification(new GUIContent(message)));
            _context.WarmCaches();

            RegisterPages();
            BuildWindow();
            SelectPage(_pages[0]);
        }

        private void RegisterPages()
        {
            _pages.Clear();
            _pages.Add(new GASCenterSettingPage());
            _pages.Add(new GASCenterTagPage());
            _pages.Add(new GASCenterJsonTablePage(
                "attribute",
                "Attribute 属性",
                "浏览 Attribute JSON，按 ID、名称或描述检索属性配置。",
                setting => setting.PathOfJsonAttr,
                setting => setting.PathOfExcelAttr));
            _pages.Add(new GASCenterJsonTablePage(
                "attribute-set",
                "Attribute Set 属性集",
                "浏览 AttributeSet JSON，查看属性集内属性初始值和上下限约束。",
                setting => setting.PathOfJsonAttrSet,
                setting => setting.PathOfExcelAttrSet));
            _pages.Add(new GASCenterCuePage());
            _pages.Add(new GASCenterEffectPage());
            _pages.Add(new GASCenterAbilityPage());
            _pages.Add(new GASCenterAscPage());
        }

        private void BuildWindow()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("gas-root");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            rootVisualElement.Add(BuildHeader());

            var splitView = new TwoPaneSplitView(0, 220, TwoPaneSplitViewOrientation.Horizontal);
            splitView.AddToClassList("gas-body");
            splitView.Add(BuildNavigation());

            var contentPanel = new VisualElement();
            contentPanel.AddToClassList("gas-content-panel");
            _pageTitle = new Label();
            _pageTitle.AddToClassList("gas-page-title");
            _pageDescription = new Label();
            _pageDescription.AddToClassList("gas-page-description");
            _contentHost = new VisualElement();
            _contentHost.AddToClassList("gas-content-host");

            contentPanel.Add(_pageTitle);
            contentPanel.Add(_pageDescription);
            contentPanel.Add(_contentHost);
            splitView.Add(contentPanel);

            rootVisualElement.Add(splitView);
        }

        private VisualElement BuildHeader()
        {
            var header = new Toolbar();
            header.AddToClassList("gas-header");

            var title = new Label("EX-GAS Center");
            title.AddToClassList("gas-header-title");
            header.Add(title);

            var spacer = new VisualElement();
            spacer.AddToClassList("gas-spacer");
            header.Add(spacer);

            header.Add(new ToolbarButton(RefreshCurrentPage) { text = "刷新当前页" });
            header.Add(new ToolbarButton(RefreshAll) { text = "刷新缓存" });

            return header;
        }

        private VisualElement BuildNavigation()
        {
            _navigationButtons.Clear();

            var navigation = new ScrollView();
            navigation.AddToClassList("gas-navigation");

            foreach (var page in _pages)
            {
                var localPage = page;
                var button = new Button(() => SelectPage(localPage))
                {
                    text = page.Title
                };
                button.AddToClassList("gas-nav-button");
                navigation.Add(button);
                _navigationButtons[page.Id] = button;
            }

            return navigation;
        }

        private void SelectPage(IGASCenterPage page)
        {
            if (page == null)
            {
                return;
            }

            _currentPage = page;
            _pageTitle.text = page.Title;
            _pageDescription.text = page.Description;

            foreach (var pair in _navigationButtons)
            {
                pair.Value.EnableInClassList("gas-nav-button-selected", pair.Key == page.Id);
            }

            _contentHost.Clear();
            _contentHost.Add(page.CreateView(_context));
            page.Refresh();
        }

        private void RefreshCurrentPage()
        {
            _currentPage?.Refresh();
            _context.Notify("当前页已刷新");
        }

        private void RefreshAll()
        {
            _context.ReloadSettings();
            _context.WarmCaches();
            SelectPage(_currentPage ?? _pages[0]);
            _context.Notify("GAS Center 缓存已刷新");
        }
    }
}
