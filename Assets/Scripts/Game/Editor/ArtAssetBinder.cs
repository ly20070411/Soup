#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Soup.Employees;
using Soup.Items;
using Soup.Jobs;
using UnityEditor;
using UnityEngine;

namespace Soup.Game.Editor
{
    /// <summary>
    /// Imports art under Assets/美术资产 and wires icons / UI chrome.
    /// Also strips solid opaque backgrounds (flood-fill from edges) so sprites render with alpha.
    /// </summary>
    public static class ArtAssetBinder
    {
        private const string ArtFolder = "Assets/美术资产/图片";
        private const string TitleButtonFolder = "Assets/美术资产/UI/UI/UI";
        private const string LibraryPath = "Assets/Resources/GameArtLibrary.asset";

        /// <summary>Corner / edge pixels with alpha above this are treated as possibly solid background.</summary>
        private const float OpaqueCornerAlpha = 0.5f;
        /// <summary>Max RGB channel delta (0–1) to treat a pixel as background color.</summary>
        private const float BackgroundColorTolerance = 0.08f;

        [MenuItem("Soup/Art/Bind Art Assets")]
        public static void BindAll()
        {
            int cleared = RemoveOpaqueBackgrounds();
            EnsureSprites();
            int jobs = BindJobsAndIngredients();
            int emps = BindEmployees();
            bool libOk = BindUiLibrary();
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[ArtAssetBinder] 完成：去背景 {cleared}，岗位/食材图标 {jobs}，员工 {emps}，UI库 {(libOk ? "OK" : "FAIL")}");
        }

        [MenuItem("Soup/Art/Remove Opaque Backgrounds")]
        public static void RemoveOpaqueBackgroundsMenu()
        {
            int cleared = RemoveOpaqueBackgrounds();
            EnsureSprites();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ArtAssetBinder] 去背景完成：处理 {cleared} 张。");
        }

        /// <summary>
        /// For PNGs whose corners are still opaque solid color, flood-fill the connected
        /// background from the edges and write alpha back into the source file.
        /// </summary>
        public static int RemoveOpaqueBackgrounds()
        {
            if (!Directory.Exists(ArtFolder))
            {
                Debug.LogWarning($"[ArtAssetBinder] 找不到目录：{ArtFolder}");
                return 0;
            }

            int cleared = 0;
            foreach (var file in Directory.GetFiles(ArtFolder, "*.png"))
            {
                var path = file.Replace('\\', '/');
                string fileName = Path.GetFileName(path);
                if (IsUiChromeFile(fileName))
                    continue;

                if (TryRemoveOpaqueBackground(path))
                    cleared++;
            }

            if (cleared > 0)
                AssetDatabase.Refresh();

            return cleared;
        }

        private static bool TryRemoveOpaqueBackground(string assetPath)
        {
            if (!File.Exists(assetPath))
                return false;

            byte[] bytes = File.ReadAllBytes(assetPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                Object.DestroyImmediate(tex);
                return false;
            }

            int w = tex.width;
            int h = tex.height;
            if (w < 2 || h < 2)
            {
                Object.DestroyImmediate(tex);
                return false;
            }

            Color32[] pixels = tex.GetPixels32();
            Color32 c00 = pixels[0];
            Color32 c10 = pixels[w - 1];
            Color32 c01 = pixels[(h - 1) * w];
            Color32 c11 = pixels[(h - 1) * w + (w - 1)];

            float avgA = (c00.a + c10.a + c01.a + c11.a) * (1f / 4f) / 255f;
            if (avgA < OpaqueCornerAlpha)
            {
                Object.DestroyImmediate(tex);
                return false;
            }

            if (!CornersMatch(c00, c10, c01, c11, BackgroundColorTolerance))
            {
                Object.DestroyImmediate(tex);
                Debug.LogWarning($"[ArtAssetBinder] 跳过（四角颜色不一致）：{Path.GetFileName(assetPath)}");
                return false;
            }

            Color32 bg = AverageColor(c00, c10, c01, c11);
            int removed = FloodFillClearBackground(pixels, w, h, bg, BackgroundColorTolerance);
            SoftenBackgroundFringe(pixels, w, h, bg, BackgroundColorTolerance * 2f);

            if (removed <= 0)
            {
                Object.DestroyImmediate(tex);
                return false;
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            if (png == null || png.Length == 0)
                return false;

            File.WriteAllBytes(assetPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[ArtAssetBinder] 已去背景：{Path.GetFileName(assetPath)}（清除 {removed} 像素）");
            return true;
        }

        private static bool CornersMatch(Color32 a, Color32 b, Color32 c, Color32 d, float tol)
        {
            return ColorsClose(a, b, tol) && ColorsClose(a, c, tol) && ColorsClose(a, d, tol);
        }

        private static Color32 AverageColor(Color32 a, Color32 b, Color32 c, Color32 d)
        {
            return new Color32(
                (byte)((a.r + b.r + c.r + d.r) / 4),
                (byte)((a.g + b.g + c.g + d.g) / 4),
                (byte)((a.b + b.b + c.b + d.b) / 4),
                255);
        }

        private static bool ColorsClose(Color32 a, Color32 b, float tol)
        {
            float t = tol * 255f;
            return Mathf.Abs(a.r - b.r) <= t
                   && Mathf.Abs(a.g - b.g) <= t
                   && Mathf.Abs(a.b - b.b) <= t;
        }

        private static bool IsBackgroundPixel(Color32 pixel, Color32 bg, float tol)
        {
            if (pixel.a < 8)
                return false;
            return ColorsClose(pixel, bg, tol);
        }

        private static int FloodFillClearBackground(Color32[] pixels, int w, int h, Color32 bg, float tol)
        {
            int n = w * h;
            var visited = new bool[n];
            var queue = new Queue<int>(Mathf.Max(64, n / 8));
            int removed = 0;

            for (int x = 0; x < w; x++)
            {
                TryEnqueueBg(pixels, visited, queue, w, h, x, 0, bg, tol);
                TryEnqueueBg(pixels, visited, queue, w, h, x, h - 1, bg, tol);
            }

            for (int y = 0; y < h; y++)
            {
                TryEnqueueBg(pixels, visited, queue, w, h, 0, y, bg, tol);
                TryEnqueueBg(pixels, visited, queue, w, h, w - 1, y, bg, tol);
            }

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                pixels[i] = new Color32(pixels[i].r, pixels[i].g, pixels[i].b, 0);
                removed++;

                int x = i % w;
                int y = i / w;
                TryEnqueueBg(pixels, visited, queue, w, h, x - 1, y, bg, tol);
                TryEnqueueBg(pixels, visited, queue, w, h, x + 1, y, bg, tol);
                TryEnqueueBg(pixels, visited, queue, w, h, x, y - 1, bg, tol);
                TryEnqueueBg(pixels, visited, queue, w, h, x, y + 1, bg, tol);
            }

            return removed;
        }

        private static void TryEnqueueBg(
            Color32[] pixels,
            bool[] visited,
            Queue<int> queue,
            int w,
            int h,
            int x,
            int y,
            Color32 bg,
            float tol)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            if (visited[i]) return;
            if (!IsBackgroundPixel(pixels[i], bg, tol)) return;
            visited[i] = true;
            queue.Enqueue(i);
        }

        /// <summary>
        /// Fade near-background fringe pixels that sit next to cleared alpha (anti-halo).
        /// </summary>
        private static void SoftenBackgroundFringe(Color32[] pixels, int w, int h, Color32 bg, float tol)
        {
            int n = w * h;
            var nextAlpha = new byte[n];
            for (int i = 0; i < n; i++)
                nextAlpha[i] = pixels[i].a;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (pixels[i].a < 8) continue;
                if (!HasTransparentNeighbor(pixels, w, h, x, y)) continue;
                if (!ColorsClose(pixels[i], bg, tol)) continue;

                float dist = ColorDistance01(pixels[i], bg);
                float keep = Mathf.Clamp01(dist / Mathf.Max(0.001f, tol));
                nextAlpha[i] = (byte)Mathf.RoundToInt(pixels[i].a * keep);
            }

            for (int i = 0; i < n; i++)
            {
                if (nextAlpha[i] == pixels[i].a) continue;
                pixels[i] = new Color32(pixels[i].r, pixels[i].g, pixels[i].b, nextAlpha[i]);
            }
        }

        private static bool HasTransparentNeighbor(Color32[] pixels, int w, int h, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                if (pixels[ny * w + nx].a < 8)
                    return true;
            }

            return false;
        }

        private static float ColorDistance01(Color32 a, Color32 b)
        {
            float dr = (a.r - b.r) / 255f;
            float dg = (a.g - b.g) / 255f;
            float db = (a.b - b.b) / 255f;
            return Mathf.Max(Mathf.Abs(dr), Mathf.Max(Mathf.Abs(dg), Mathf.Abs(db)));
        }

        private static void EnsureSprites()
        {
            if (!Directory.Exists(ArtFolder))
            {
                Debug.LogWarning($"[ArtAssetBinder] 找不到目录：{ArtFolder}");
                return;
            }

            foreach (var file in Directory.GetFiles(ArtFolder, "*.png"))
                EnsureSpriteFile(file.Replace('\\', '/'));

            EnsureSpriteFile($"{TitleButtonFolder}/按钮1.png");
            EnsureSpriteFile($"{TitleButtonFolder}/按钮2.png");
            EnsureSpriteFile($"{TitleButtonFolder}/按钮3.png");
        }

        private static void EnsureSpriteFile(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            string fileName = Path.GetFileName(path);
            bool isUiChrome = IsUiChromeFile(fileName);
            bool isWorldProp = IsWorldPropFile(fileName);
            bool isHudCounter = IsHudCounterIconFile(fileName);
            // UI chrome / HUD counters / scene props: keep source PPU 100 and full res.
            // World icons: max 512 + PPU = source max edge → ≈1 world unit.
            int maxSize = (isUiChrome || isWorldProp || isHudCounter) ? 2048 : 512;
            importer.GetSourceTextureWidthAndHeight(out int srcW, out int srcH);
            int larger = Mathf.Max(1, Mathf.Max(srcW, srcH));
            float targetPpu = (isUiChrome || isWorldProp || isHudCounter) ? 100f : larger;
            Vector4 targetBorder = GetUiChromeBorder(fileName);

            bool dirty = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            if (settings.textureType != TextureImporterType.Sprite)
            {
                settings.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (settings.spriteMode != (int)SpriteImportMode.Single)
            {
                settings.spriteMode = (int)SpriteImportMode.Single;
                dirty = true;
            }

            if (settings.mipmapEnabled)
            {
                settings.mipmapEnabled = false;
                dirty = true;
            }

            if (!settings.alphaIsTransparency)
            {
                settings.alphaIsTransparency = true;
                dirty = true;
            }

            if (!Mathf.Approximately(settings.spritePixelsPerUnit, targetPpu))
            {
                settings.spritePixelsPerUnit = targetPpu;
                dirty = true;
            }

            if (settings.filterMode != FilterMode.Bilinear)
            {
                settings.filterMode = FilterMode.Bilinear;
                dirty = true;
            }

            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                dirty = true;
            }

            if ((settings.spriteBorder - targetBorder).sqrMagnitude > 0.01f)
            {
                settings.spriteBorder = targetBorder;
                dirty = true;
            }

            if (importer.maxTextureSize != maxSize)
            {
                importer.maxTextureSize = maxSize;
                dirty = true;
            }

            var platform = importer.GetDefaultPlatformTextureSettings();
            if (platform.maxTextureSize != maxSize)
            {
                platform.maxTextureSize = maxSize;
                importer.SetPlatformTextureSettings(platform);
                dirty = true;
            }

            // TextureImporter.spritePixelsPerUnit can desync from settings; set both.
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, targetPpu))
            {
                importer.spritePixelsPerUnit = targetPpu;
                dirty = true;
            }

            if (dirty)
            {
                importer.SetTextureSettings(settings);
                importer.spritePixelsPerUnit = targetPpu;
                importer.SaveAndReimport();
            }
        }

        private static bool IsUiChromeFile(string fileName)
        {
            return fileName == "按钮.png"
                   || fileName == "按钮1.png"
                   || fileName == "按钮2.png"
                   || fileName == "按钮3.png"
                   || fileName == "开始.png"
                   || fileName == "切换键左.png"
                   || fileName == "切换键右.png"
                   || fileName == "分隔线横.png"
                   || fileName == "分隔线竖.png";
        }

        /// <summary>HUD soft/tough/…/flavor icons — UI import settings, but still strip black backgrounds.</summary>
        private static bool IsHudCounterIconFile(string fileName)
        {
            return fileName == "柔软食材.png"
                   || fileName == "强韧食材.png"
                   || fileName == "坚固食材.png"
                   || fileName == "处理食材.png"
                   || fileName == "烹饪食材.png"
                   || fileName == "热辣.png"
                   || fileName == "寒冷.png"
                   || fileName == "酸涩.png"
                   || fileName == "鲜美.png";
        }

        /// <summary>Scene props authored at PPU 100 (not 1-unit job icons).</summary>
        private static bool IsWorldPropFile(string fileName)
        {
            return fileName == "仓库.png";
        }

        private static Vector4 GetUiChromeBorder(string fileName)
        {
            // L,B,R,T — full-res bevel depth so Image.Type.Sliced keeps corners.
            switch (fileName)
            {
                case "按钮.png":
                    return new Vector4(80f, 80f, 80f, 80f);
                case "分隔线横.png":
                    return new Vector4(48f, 0f, 48f, 0f);
                case "分隔线竖.png":
                    return new Vector4(0f, 48f, 0f, 48f);
                default:
                    return Vector4.zero;
            }
        }

        private static Sprite Load(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{fileName}");
        }

        private static Sprite LoadUi(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{TitleButtonFolder}/{fileName}");
        }

        private static int BindJobsAndIngredients()
        {
            // art file name → job id / ingredient matching keys
            var pairs = new Dictionary<string, string>
            {
                { "蘑菇.png", "mushroom" },
                { "小甜果.png", "berry" },
                { "冰晶果.png", "ice_fruit" },
                { "青酸果.png", "sour_fruit" },
                { "魔法叶.png", "magic_leaf" },
                { "爆炸果.png", "hot_fruit" },
                { "灯心草.png", "lampwick_grass" },
                { "小白花.png", "little_white_flower" },
                { "小银鱼.png", "little_silver_fish" },
                { "快乐坨坨.png", "happy_tuotuo" },
                { "双尾蛇.png", "double_tail_snake" },
                { "棍棍虫.png", "stick_bug" },
                { "甜团团.png", "sweet_bun" },
                { "黏爬爬.png", "nian_papa" },
                { "小刺球.png", "little_spiky_ball" },
                { "大角兽.png", "big_horn_beast" }
            };

            int hits = 0;
            foreach (var pair in pairs)
            {
                var sprite = Load(pair.Key);
                if (sprite == null)
                {
                    Debug.LogWarning($"[ArtAssetBinder] 缺少 Sprite：{pair.Key}");
                    continue;
                }

                string artName = Path.GetFileNameWithoutExtension(pair.Key);
                foreach (var guid in AssetDatabase.FindAssets("t:JobItem"))
                {
                    var job = AssetDatabase.LoadAssetAtPath<JobItem>(AssetDatabase.GUIDToAssetPath(guid));
                    if (job == null || job.Id != pair.Value) continue;
                    job.SetIcon(sprite);
                    EditorUtility.SetDirty(job);
                    hits++;
                }

                foreach (var guid in AssetDatabase.FindAssets("t:IngredientItem"))
                {
                    var item = AssetDatabase.LoadAssetAtPath<IngredientItem>(AssetDatabase.GUIDToAssetPath(guid));
                    if (item == null || !IngredientMatches(item, pair.Value, artName)) continue;
                    item.SetIcon(sprite);
                    EditorUtility.SetDirty(item);
                    hits++;
                }
            }

            return hits;
        }

        private static bool IngredientMatches(IngredientItem item, string jobId, string artName)
        {
            if (item.DisplayName == artName) return true;
            string id = (item.Id ?? string.Empty).Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
            if (id == jobId) return true;

            switch (jobId)
            {
                case "hot_fruit":
                    return item.DisplayName == "爆辣果" || id.Contains("hot");
                case "sour_fruit":
                    return item.DisplayName == "青酸果" || id.Contains("sour");
                case "ice_fruit":
                    return item.DisplayName == "冰晶果" || id.Contains("ice");
                case "magic_leaf":
                    return item.DisplayName == "魔法叶" || id.Contains("magic");
                case "berry":
                    return item.DisplayName == "小甜果" || id == "berry";
                case "mushroom":
                    return item.DisplayName == "蘑菇" || id == "mushroom";
                case "lampwick_grass":
                    return item.DisplayName == "灯芯草" || item.DisplayName == "灯心草" ||
                           id.Contains("lampwick") || id.Contains("dengxin");
                case "little_white_flower":
                    return item.DisplayName == "小白花" || id.Contains("white_flower");
                case "little_silver_fish":
                    return item.DisplayName == "小银鱼" || id.Contains("silver_fish");
                case "happy_tuotuo":
                    return item.DisplayName == "快乐坨坨" || id.Contains("tuotuo") || id.Contains("happy");
                case "double_tail_snake":
                    return item.DisplayName == "双尾蛇" || id.Contains("double_tail") || id.Contains("snake");
                case "stick_bug":
                    return item.DisplayName == "棍棍虫" || id.Contains("stick_bug");
                case "sweet_bun":
                    return item.DisplayName == "甜团团" || id.Contains("sweet_bun") || id.Contains("sweet");
                case "nian_papa":
                    return item.DisplayName == "黏爬爬" || id.Contains("nian");
                case "little_spiky_ball":
                    return item.DisplayName == "小刺球" || id.Contains("spiky");
                case "big_horn_beast":
                    return item.DisplayName == "大角兽" || id.Contains("big_horn") || id.Contains("horn");
                default:
                    return false;
            }
        }

        private static int BindEmployees()
        {
            // art file → employee id / display name
            var pairs = new Dictionary<string, string[]>
            {
                { "小精灵.png", new[] { "elf", "小精灵" } },
                { "蘑菇人.png", new[] { "mushroom_person", "蘑菇人" } },
                { "幽灵.png", new[] { "ghost", "幽灵" } },
                { "异世界勇者.png", new[] { "otherworld_hero", "异世界勇者" } },
                { "吱吱.png", new[] { "zhizhi", "吱吱" } }
            };

            var byId = new Dictionary<string, Sprite>();
            var byName = new Dictionary<string, Sprite>();
            foreach (var pair in pairs)
            {
                var sprite = Load(pair.Key);
                if (sprite == null)
                {
                    Debug.LogWarning($"[ArtAssetBinder] 缺少员工 Sprite：{pair.Key}");
                    continue;
                }

                byId[pair.Value[0]] = sprite;
                byName[pair.Value[1]] = sprite;
            }

            if (byId.Count == 0) return 0;

            int hits = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:EmployeeItem"))
            {
                var emp = AssetDatabase.LoadAssetAtPath<EmployeeItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (emp == null) continue;

                Sprite sprite = null;
                if (!string.IsNullOrEmpty(emp.Id) && byId.TryGetValue(emp.Id, out var byIdSprite))
                    sprite = byIdSprite;
                else if (!string.IsNullOrEmpty(emp.DisplayName)
                         && byName.TryGetValue(emp.DisplayName, out var byNameSprite))
                    sprite = byNameSprite;

                if (sprite == null) continue;

                emp.SetIcon(sprite);
                EditorUtility.SetDirty(emp);
                hits++;
            }

            return hits;
        }

        private static bool BindUiLibrary()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var lib = AssetDatabase.LoadAssetAtPath<GameArtLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<GameArtLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            lib.SetZoneSwitch(Load("切换键左.png"), Load("切换键右.png"));
            lib.SetDividers(Load("分隔线横.png"), Load("分隔线竖.png"));
            lib.SetButtonBackground(Load("按钮.png"));
            lib.SetCircleFrame(LoadUi("圆形ui.png"));
            lib.SetResourceIcons(
                Load("柔软食材.png"),
                Load("强韧食材.png"),
                Load("坚固食材.png"),
                Load("处理食材.png"),
                Load("烹饪食材.png"));
            lib.SetFlavorIcons(
                Load("热辣.png"),
                Load("寒冷.png"),
                Load("酸涩.png"),
                Load("鲜美.png"));
            lib.SetTitleScreen(
                Load("开始.png"),
                LoadUi("按钮1.png"),
                LoadUi("按钮2.png"),
                LoadUi("按钮3.png"));
            EditorUtility.SetDirty(lib);
            return lib.ZoneSwitchLeft != null
                   && lib.ZoneSwitchRight != null
                   && lib.DividerHorizontal != null
                   && lib.DividerVertical != null
                   && lib.ButtonBackground != null
                   && lib.CircleFrame != null
                   && lib.SoftIcon != null
                   && lib.ToughIcon != null
                   && lib.SolidIcon != null
                   && lib.ProcessedIcon != null
                   && lib.CookedIcon != null
                   && lib.SpicyIcon != null
                   && lib.ColdIcon != null
                   && lib.SourIcon != null
                   && lib.MagicIcon != null
                   && lib.TitleBackground != null
                   && lib.TitleStartButton != null
                   && lib.TitleContinueButton != null
                   && lib.TitleQuitButton != null;
        }
    }
}
#endif
