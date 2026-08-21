using System.Collections.Generic;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// IMGUI skin built from completed art assets under Resources/UI.
    /// Loaded lazily inside OnGUI (GUI.skin is only valid there).
    /// 生成素材（LOGO / 风味图标 / 岗位道具 / 9-Slice 面板与按钮）位于 Resources/UI/Generated，
    /// 由 ArtIconLinker 从 Assets/Art/Generated 部署；缺失时全部回退到旧皮肤或纯文本。
    /// </summary>
    public static class SoupUITheme
    {
        private const string GeneratedFolder = "UI/Generated";

        /// <summary>羊皮纸浅色面板上的正文色（深棕近黑，贴合素材的深棕描边风格）。</summary>
        public static readonly Color TextDark = new Color(0.21f, 0.15f, 0.09f);

        private static GUIStyle _button;
        private static GUIStyle _panelButton;
        private static GUIStyle _panelBox;
        private static GUIStyle _primaryButton;
        private static GUIStyle _label;
        private static GUIStyle _boldLabel;
        private static bool _initialized;

        private static readonly Dictionary<string, Texture2D> GeneratedTextures =
            new Dictionary<string, Texture2D>();

        /// <summary>主按钮皮肤（暗黄底、深棕描边，来自 ui.png）。</summary>
        public static GUIStyle Button
        {
            get
            {
                EnsureInit();
                return _button;
            }
        }

        /// <summary>小尺寸按钮（岗位 +/- 等紧凑控件）。</summary>
        public static GUIStyle PanelButton
        {
            get
            {
                EnsureInit();
                return _panelButton;
            }
        }

        /// <summary>
        /// 大面板皮肤（ui_panel_main 九宫格：木框 + 羊皮纸内区）。
        /// 素材缺失时回退到默认 box。
        /// </summary>
        public static GUIStyle PanelBox
        {
            get
            {
                EnsureInit();
                return _panelBox;
            }
        }

        /// <summary>
        /// 主操作按钮（ui_button_primary 九宫格：木框琥珀面 + 青色端点宝石）。
        /// 素材缺失时回退到 <see cref="Button"/>。
        /// </summary>
        public static GUIStyle PrimaryButton
        {
            get
            {
                EnsureInit();
                return _primaryButton != null ? _primaryButton : _button;
            }
        }

        /// <summary>
        /// 画自动换行的正文。GUILayout 对无空格的中文按整句计算最小宽度，会把固定宽度
        /// 容器横向撑爆（文字超出边框）；这里按给定宽度 CalcHeight 出真实高度，
        /// 再用固定矩形绘制，中文按字符断行。
        /// </summary>
        public static void DrawWrappedText(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text)) return;
            float height = style.CalcHeight(new GUIContent(text), width);
            var rect = GUILayoutUtility.GetRect(width, height);
            GUI.Label(rect, text, style);
        }

        /// <summary>
        /// 羊皮纸面板（<see cref="PanelBox"/>）上直接绘制的正文样式：深棕近黑。
        /// 面板内嵌套的默认深色 box 里仍用默认白色文字。
        /// </summary>
        public static GUIStyle Label
        {
            get
            {
                EnsureInit();
                return _label;
            }
        }

        /// <summary>羊皮纸面板上的加粗标题样式（深棕近黑）。</summary>
        public static GUIStyle BoldLabel
        {
            get
            {
                EnsureInit();
                return _boldLabel;
            }
        }

        /// <summary>
        /// 按文件名加载 Resources/UI/Generated 下的素材（缓存；兼容 Sprite 与 Default 导入）。
        /// 可在 OnGUI 之外调用；素材不存在时返回 null，由调用方回退。
        /// </summary>
        public static Texture2D GetGeneratedTexture(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (GeneratedTextures.TryGetValue(name, out var cached))
                return cached;

            string path = $"{GeneratedFolder}/{name}";
            var sprite = Resources.Load<Sprite>(path);
            var texture = sprite != null ? sprite.texture : Resources.Load<Texture2D>(path);
            GeneratedTextures[name] = texture;
            return texture;
        }

        private static void EnsureInit()
        {
            if (_initialized) return;

            var buttonTex = LoadTexture("UI/ui");
            _button = CreateButtonStyle(buttonTex, 16);
            _panelButton = CreateButtonStyle(buttonTex, 10);
            _panelBox = CreateSlicedBoxStyle(GetGeneratedTexture("ui_panel_main"));
            _primaryButton = CreateSlicedButtonStyle(GetGeneratedTexture("ui_button_primary"));
            _label = CreateDarkLabel(false);
            _boldLabel = CreateDarkLabel(true);

            _initialized = true;
        }

        private static GUIStyle CreateButtonStyle(Texture2D background, int border)
        {
            var style = new GUIStyle(GUI.skin.button);
            if (background != null)
            {
                style.normal.background = background;
                style.hover.background = background;
                style.active.background = background;
                style.focused.background = background;
                style.border = new RectOffset(border, border, border, border);
                style.padding = new RectOffset(border, border, border / 2, border / 2);
                style.normal.textColor = TextDark;
                style.hover.textColor = TextDark;
                style.active.textColor = TextDark;
                style.focused.textColor = TextDark;
            }

            return style;
        }

        /// <summary>
        /// 九宫格面板：素材的透明留边已在 ArtIconLinker 部署时裁掉，border 即为
        /// 屏幕上的木框厚度；中心羊皮纸区域拉伸铺满。
        /// </summary>
        private static GUIStyle CreateSlicedBoxStyle(Texture2D background)
        {
            var style = new GUIStyle(GUI.skin.box);
            if (background == null) return style;

            style.normal.background = background;
            style.hover.background = background;
            style.active.background = background;
            style.focused.background = background;
            style.border = new RectOffset(24, 24, 28, 30);
            style.padding = new RectOffset(30, 30, 10, 10);
            style.margin = new RectOffset(4, 4, 4, 4);
            return style;
        }

        /// <summary>
        /// 主操作按钮：素材透明留边已在部署时裁掉。按钮高度普遍只有 30–44px，
        /// 纵向不留切边，用横向三段切片保留两端的青色宝石端点。
        /// </summary>
        private static GUIStyle CreateSlicedButtonStyle(Texture2D background)
        {
            var style = new GUIStyle(GUI.skin.button);
            if (background == null) return null;

            style.normal.background = background;
            style.hover.background = background;
            style.active.background = background;
            style.focused.background = background;
            style.border = new RectOffset(18, 18, 0, 0);
            style.padding = new RectOffset(22, 22, 8, 8);
            // 琥珀色按钮面上用深棕文字（原白色与亮面冲突，且贴合素材描边风格）。
            style.normal.textColor = TextDark;
            style.hover.textColor = new Color(0.12f, 0.08f, 0.04f);
            style.active.textColor = new Color(0.3f, 0.22f, 0.13f);
            style.focused.textColor = TextDark;
            style.fontStyle = FontStyle.Bold;
            return style;
        }

        private static GUIStyle CreateDarkLabel(bool bold)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = true
            };
            ApplyTextColor(style, TextDark);
            return style;
        }

        /// <summary>
        /// 统一设置四个交互状态的文字颜色。IMGUI 的 GUIStyle 每个状态各有独立的
        /// textColor，只改 normal 的话，鼠标悬停会跳回皮肤默认的白色。
        /// </summary>
        public static void ApplyTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
        }

        private static Texture2D LoadTexture(string path)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite.texture;
            return Resources.Load<Texture2D>(path);
        }
    }
}
