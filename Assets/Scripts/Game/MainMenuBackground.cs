using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// 主菜单背景：
    /// 1) 美术序列帧循环播放 —— 把帧图放入 Resources/UI/MainMenu/（文件名以 bg 开头，
    ///    按名称排序即播放顺序，如 bg_000 / bg_001 ...），导入后自动生效；
    /// 2) 素材未导入时 —— 程序化暖色渐变 + 缓慢漂浮的光斑兜底，不空白。
    /// 主菜单打开期间本背景是唯一的全屏画面（世界地图等游戏 UI 同期彻底隐藏，
    /// 不再调暗叠加）；局内（主菜单关闭）时整个 OnGUI 直接跳过。
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public class MainMenuBackground : MonoBehaviour
    {
        [Tooltip("序列帧播放速率（帧/秒）。")]
        [SerializeField] private float framesPerSecond = 8f;

        private Texture2D[] _frames;
        private Texture2D _gradient;

        private void Start()
        {
            LoadFrames();
        }

        private void LoadFrames()
        {
            // 兼容 Sprite 与 Default 两种导入方式：未跑过 ArtIconLinker 时也能直接显示。
            var all = Resources.LoadAll("UI/MainMenu", typeof(Object));
            var matched = new System.Collections.Generic.List<Texture2D>();
            for (int i = 0; i < all.Length; i++)
            {
                Texture2D texture = null;
                if (all[i] is Sprite sprite)
                    texture = sprite.texture;
                else if (all[i] is Texture2D direct)
                    texture = direct;

                if (texture != null && texture.name.StartsWith("bg", System.StringComparison.OrdinalIgnoreCase))
                    matched.Add(texture);
            }

            matched.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _frames = matched.ToArray();

            if (_frames.Length == 1)
                Debug.Log($"[MainMenuBackground] 单张背景图，静态显示：{_frames[0].name}。");
            else if (_frames.Length > 1)
                Debug.Log($"[MainMenuBackground] 发现 {_frames.Length} 帧背景动画。");
        }

        private void OnGUI()
        {
            // 主菜单关闭（局内 / 暂停）时绝不绘制，避免全屏背景盖住玩法按钮。
            var menu = MainMenuUI.Instance;
            if (menu != null && !menu.IsOpen) return;

            // IMGUI 层级：数值越小越在上层。背景应处于最底层，用最大值。
            GUI.depth = 10;

            if (_frames != null && _frames.Length > 0)
            {
                DrawAnimatedFrame();
                return;
            }

            DrawProceduralFallback();
        }

        /// <summary>
        /// IMGUI 逻辑坐标 = 物理像素 ÷ DPI 缩放。
        /// 打包后在 Windows 高 DPI（如 150%）下 Screen 返回物理像素，
        /// 直接用 Screen.width/height 画全屏 rect 会超出屏幕导致背景错位/露灰边。
        /// GUI.matrix.m00 即当前 GUI 坐标系的缩放因子（等价于 GUIUtility.pixelsPerPoint）。
        /// </summary>
        private static Rect FullScreenRect()
        {
            float scale = Mathf.Max(1f, GUI.matrix.m00);
            return new Rect(0, 0, Screen.width / scale, Screen.height / scale);
        }

        private void DrawAnimatedFrame()
        {
            int index = 0;
            if (_frames.Length > 1)
            {
                float fps = Mathf.Max(0.1f, framesPerSecond);
                index = (int)(Time.unscaledTime * fps) % _frames.Length;
            }

            GUI.DrawTexture(
                FullScreenRect(),
                _frames[index],
                ScaleMode.ScaleAndCrop);
        }

        // ------------------------------------------------------- procedural art

        /// <summary>深棕暖色渐变 + 三个缓慢漂浮的光斑，营造炉火氛围。</summary>
        private void DrawProceduralFallback()
        {
            EnsureGradient();
            if (_gradient != null)
            {
                GUI.DrawTexture(
                    FullScreenRect(),
                    _gradient,
                    ScaleMode.StretchToFill);
            }

            float t = Time.unscaledTime;
            DrawOrbit(t, 0.13f, 0.30f, 0.55f, 0.34f, new Color(0.85f, 0.64f, 0.25f, 0.05f));
            DrawOrbit(t * 0.7f + 40f, 0.80f, 0.62f, 0.42f, 0.26f, new Color(0.25f, 0.43f, 0.56f, 0.045f));
            DrawOrbit(t * 1.3f + 90f, 0.50f, 0.78f, 0.30f, 0.18f, new Color(0.95f, 0.55f, 0.20f, 0.035f));
        }

        private void DrawOrbit(
            float t,
            float centerX,
            float centerY,
            float radiusX,
            float radiusY,
            Color color)
        {
            float scale = Mathf.Max(1f, GUI.matrix.m00);
            float w = Screen.width / scale;
            float h = Screen.height / scale;
            float x = centerX + Mathf.Sin(t * 0.35f) * 0.06f;
            float y = centerY + Mathf.Cos(t * 0.27f) * 0.05f;
            var rect = new Rect(
                (x - radiusX * 0.5f) * w,
                (y - radiusY * 0.5f) * h,
                radiusX * w,
                radiusY * h);

            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        private void EnsureGradient()
        {
            if (_gradient != null) return;

            const int height = 64;
            _gradient = new Texture2D(2, height, TextureFormat.RGBA32, false);
            _gradient.wrapMode = TextureWrapMode.Clamp;

            // 顶部深棕 → 底部近黑（炉火熄灭后的厨房）。
            var top = new Color(0.18f, 0.13f, 0.08f);
            var bottom = new Color(0.05f, 0.035f, 0.02f);
            var pixels = new Color32[height * 2];
            for (int y = 0; y < height; y++)
            {
                var c = Color.Lerp(top, bottom, y / (float)(height - 1));
                pixels[y * 2] = c;
                pixels[y * 2 + 1] = c;
            }

            _gradient.SetPixels32(pixels);
            _gradient.Apply(false, true);
        }

        private void OnDestroy()
        {
            if (_gradient != null)
                Destroy(_gradient);
        }
    }
}
