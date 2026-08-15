using System.Collections.Generic;
using Soup.Employees;
using Soup.Items;
using Soup.Jobs;
using UnityEditor;
using UnityEngine;

namespace Soup.Game.Editor
{
    /// <summary>
    /// Links hand-authored and generated art assets to gameplay icons:
    /// ingredient sprites, gather job icons, and employee icons.
    /// </summary>
    public static class ArtIconLinker
    {
        private const string CompletedArtFolder = "Assets/Docs/美术素材/完成后上传";
        private const string GeneratedArtFolder = "Assets/Art/Generated";
        private const string GeneratedIngredientFolder = GeneratedArtFolder + "/Ingredients";
        private const string GeneratedCharacterFolder = GeneratedArtFolder + "/Characters";
        private const string GeneratedUiFolder = GeneratedArtFolder + "/UI";
        private const string ResourcesUiGeneratedFolder = "Assets/Resources/UI/Generated";

        // 文件名 → 食材 id（爆炸果.png 对应设计文档的 爆辣果）
        private static readonly Dictionary<string, string> IngredientIconNames =
            new Dictionary<string, string>
            {
                { "蘑菇", "mushroom" },
                { "小甜果", "berry" },
                { "冰晶果", "ice_fruit" },
                { "爆炸果", "hot_fruit" },
                { "青酸果", "sour_fruit" },
                { "魔法叶", "magic_leaf" },
                { "新建画布1", null } // 小精灵立绘，绑定到员工
            };

        // 生成资产使用稳定英文文件名；这里显式映射，避免依赖显示名或系统语言。
        private static readonly Dictionary<string, string> GeneratedIngredientIconNames =
            new Dictionary<string, string>
            {
                { "ingredient_mushroom", "mushroom" },
                { "ingredient_sweet_berry", "berry" },
                { "ingredient_ice_fruit", "ice_fruit" },
                { "ingredient_hot_fruit", "hot_fruit" },
                { "ingredient_sour_fruit", "sour_fruit" },
                { "ingredient_magic_leaf", "magic_leaf" },
                { "ingredient_rush", "rush" },
                { "ingredient_daisy", "daisy" },
                { "ingredient_mutant_mushroom", "mutant_mushroom" },
                { "ingredient_fat_mushroom", "fat_mushroom" },
                { "ingredient_strange_mushroom", "strange_mushroom" },
                { "ingredient_sweet_bun", "sweet_bun" },
                { "ingredient_big_horn_beast", "big_horn_beast" },
                { "ingredient_sticky_crawler", "nian_papa" },
                { "ingredient_little_spiky_ball", "little_spiky_ball" },
                { "ingredient_silver_fish", "silver_fish" },
                { "ingredient_happy_blob", "happy_blob" },
                { "ingredient_twin_tail_snake", "twin_tail_snake" },
                { "ingredient_stick_bug", "stick_bug" }
            };

        private static readonly Dictionary<string, string> GeneratedEmployeeIconNames =
            new Dictionary<string, string>
            {
                { "employee_elf", EmployeeManager.ElfId },
                { "employee_mushroom_person", EmployeeManager.MushroomPersonId },
                { "employee_ghost", EmployeeManager.GhostId },
                { "employee_otherworld_hero", EmployeeManager.OtherworldHeroId },
                { "employee_zhizhi", EmployeeManager.ZhizhiId }
            };

        [MenuItem("Soup/Art Assets/Link Completed Icons")]
        public static void LinkMenu()
        {
            LinkCompletedIcons(quiet: false);
        }

        /// <summary>
        /// 素材放入 Docs/美术素材/完成后上传 后自动接入（每次域重载幂等执行；
        /// delayCall 避免与导入管线抢锁）。也可随时用菜单手动触发。
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        private static void AutoLinkOnLoad()
        {
            // 只有目录存在才挂回调，避免无关项目空跑。
            if (!AssetDatabase.IsValidFolder("Assets/Docs"))
                return;
            UnityEditor.EditorApplication.delayCall += () => LinkCompletedIcons(quiet: true);
        }

        public static void LinkCompletedIcons(bool quiet)
        {
            int linkedIngredients = 0;
            int linkedJobs = 0;
            int linkedEmployees = 0;

            CopyUiAssets();
            CopyMainMenuBackgrounds();
            DeployGeneratedUiArt();

            var sprites = LoadCompletedSprites();

            foreach (var pair in sprites)
            {
                string fileName = pair.Key;
                var sprite = pair.Value;
                if (sprite == null) continue;

                if (fileName == "新建画布1")
                {
                    // 小精灵
                    var employees = AssetDatabase.LoadAssetAtPath<EmployeeDatabase>(
                        "Assets/Resources/EmployeeDatabase.asset");
                    var elf = employees != null ? employees.GetById(EmployeeManager.ElfId) : null;
                    if (elf != null)
                    {
                        elf.SetIcon(sprite);
                        EditorUtility.SetDirty(elf);
                        linkedEmployees++;
                    }

                    continue;
                }

                if (!IngredientIconNames.TryGetValue(fileName, out string ingredientId)
                    || string.IsNullOrEmpty(ingredientId))
                    continue;

                // 食材图标（按显示名兜底）
                var ingredient = FindIngredient(ingredientId, fileName);
                if (ingredient != null)
                {
                    ingredient.SetIcon(sprite);
                    EditorUtility.SetDirty(ingredient);
                    linkedIngredients++;

                    // 同名采集岗位共用食材图标
                    var job = FindGatherJob(ingredient.DisplayName);
                    if (job != null && job.Icon == null)
                    {
                        job.SetIcon(sprite);
                        EditorUtility.SetDirty(job);
                        linkedJobs++;
                    }
                }
            }

            // 正式生成图位于项目美术目录，存在时优先覆盖 Docs 中的早期草图。
            LinkGeneratedIcons(ref linkedIngredients, ref linkedJobs, ref linkedEmployees);

            AssetDatabase.SaveAssets();
            if (!quiet || linkedIngredients + linkedJobs + linkedEmployees > 0)
            {
                Debug.Log(
                    $"[ArtIconLinker] 图标绑定完成：食材 {linkedIngredients}，岗位 {linkedJobs}，员工 {linkedEmployees}。" +
                    $"素材目录：{CompletedArtFolder}；{GeneratedArtFolder}");
            }
        }

        private static void LinkGeneratedIcons(
            ref int linkedIngredients,
            ref int linkedJobs,
            ref int linkedEmployees)
        {
            foreach (var pair in LoadSpritesFromFolder(GeneratedIngredientFolder))
            {
                if (!GeneratedIngredientIconNames.TryGetValue(pair.Key, out string ingredientId))
                    continue;

                var ingredient = FindIngredient(ingredientId, string.Empty);
                if (ingredient == null) continue;

                ingredient.SetIcon(pair.Value);
                EditorUtility.SetDirty(ingredient);
                linkedIngredients++;

                var job = FindGatherJob(ingredient.DisplayName);
                if (job != null)
                {
                    job.SetIcon(pair.Value);
                    EditorUtility.SetDirty(job);
                    linkedJobs++;
                }
            }

            var employees = AssetDatabase.LoadAssetAtPath<EmployeeDatabase>(
                "Assets/Resources/EmployeeDatabase.asset");
            if (employees == null) return;

            employees.RebuildIndex();
            foreach (var pair in LoadSpritesFromFolder(GeneratedCharacterFolder))
            {
                if (!GeneratedEmployeeIconNames.TryGetValue(pair.Key, out string employeeId))
                    continue;

                var employee = employees.GetById(employeeId);
                if (employee == null) continue;

                employee.SetIcon(pair.Value);
                EditorUtility.SetDirty(employee);
                linkedEmployees++;
            }
        }

        // UI 素材：完成后上传 → Resources/UI（运行时只能读 Resources）
        // 文件名映射：ui.png 按钮 / 切换ui(.2) 切换按钮 / 铁棍(.2) 分隔线
        private static readonly (string source, string target)[] UiAssetCopies =
        {
            ("ui", "ui"),
            ("切换ui", "switch_left"),
            ("切换ui2", "switch_right"),
            ("铁棍", "divider"),
            ("铁棍2", "divider2")
        };

        private static void CopyUiAssets()
        {
            const string targetFolder = "Assets/Resources/UI";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(targetFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "UI");

            int copied = 0;
            foreach (var (source, target) in UiAssetCopies)
            {
                string sourcePath = $"{CompletedArtFolder}/{source}.png";
                string targetPath = $"{targetFolder}/{target}.png";
                if (!AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath))
                {
                    Debug.LogWarning($"[ArtIconLinker] 缺少 UI 素材：{sourcePath}");
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath) != null)
                {
                    EnsureSpriteImport(targetPath);
                    continue;
                }

                if (AssetDatabase.CopyAsset(sourcePath, targetPath))
                {
                    EnsureSpriteImport(targetPath);
                    copied++;
                }
            }

            AssetDatabase.SaveAssets();
            if (copied > 0)
                Debug.Log($"[ArtIconLinker] 已复制 {copied} 个 UI 素材到 {targetFolder}。");

            // 按钮底图与横向铁棍同样要裁掉透明留边（原文件四周留白 30–80px）。
            CropTransparentMargins($"{targetFolder}/ui.png");
            CropTransparentMargins($"{targetFolder}/divider2.png");
        }

        private static void EnsureSpriteImport(string path, int maxSize = 0)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (maxSize > 0 && importer.maxTextureSize != maxSize)
            {
                importer.maxTextureSize = maxSize;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        /// <summary>
        /// 主菜单背景序列帧：完成后上传中文件名以 bg 开头（或含“主菜单”）的图片，
        /// 按文件名排序复制为 Resources/UI/MainMenu/bg_###.png，运行时循环播放。
        /// </summary>
        private static void CopyMainMenuBackgrounds()
        {
            const string targetFolder = "Assets/Resources/UI/MainMenu";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
                AssetDatabase.CreateFolder("Assets/Resources", "UI");
            if (!AssetDatabase.IsValidFolder(targetFolder))
                AssetDatabase.CreateFolder("Assets/Resources/UI", "MainMenu");

            var sources = new System.Collections.Generic.List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CompletedArtFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName.StartsWith("bg", System.StringComparison.OrdinalIgnoreCase)
                    || fileName.Contains("主菜单"))
                    sources.Add(path);
            }

            if (sources.Count == 0) return;

            sources.Sort(System.StringComparer.OrdinalIgnoreCase);
            int copied = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                string target = $"{targetFolder}/bg_{i:D3}.png";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(target) != null) continue;
                if (AssetDatabase.CopyAsset(sources[i], target))
                {
                    EnsureSpriteImport(target);
                    copied++;
                }
            }

            AssetDatabase.SaveAssets();
            if (copied > 0)
                Debug.Log($"[ArtIconLinker] 已导入 {copied} 张主菜单背景帧到 {targetFolder}。");
        }

        // 生成 UI 素材 → Resources/UI/Generated（运行时 IMGUI 只能读 Resources）。
        // maxSize：IMGUI 显示尺寸远小于源图，缩小导入既省包体，也让九宫格
        // GUIStyle.border（1:1 屏幕像素）避开素材四周的透明留白。
        // 主视觉单独复制到 UI/MainMenu（文件名以 bg 开头即被 MainMenuBackground 识别）。
        private static readonly (string source, string target, int maxSize)[] GeneratedUiCopies =
        {
            ("UI/logo_title_text_only", "Generated/logo_title_text_only", 1024),
            ("UI/logo_soups_and_sprites", "Generated/logo_soups_and_sprites", 1024),
            ("UI/flavor_cold", "Generated/flavor_cold", 512),
            ("UI/flavor_spicy", "Generated/flavor_spicy", 512),
            ("UI/flavor_sour", "Generated/flavor_sour", 512),
            ("UI/flavor_magic", "Generated/flavor_magic", 512),
            ("UI/ui_panel_main", "Generated/ui_panel_main", 512),
            ("UI/ui_button_primary", "Generated/ui_button_primary", 256),
            ("Props/prop_gather_patch", "Generated/prop_gather_patch", 512),
            ("Props/prop_world_signpost", "Generated/prop_world_signpost", 512),
            ("Props/prop_warehouse", "Generated/prop_warehouse", 512),
            ("Props/prop_processing_table", "Generated/prop_processing_table", 512),
            ("Props/prop_magic_cauldron", "Generated/prop_magic_cauldron", 512),
            ("Props/prop_cooking_stove", "Generated/prop_cooking_stove", 512),
            ("Characters/employee_elf", "Generated/employee_elf", 512),
            ("Characters/employee_mushroom_person", "Generated/employee_mushroom_person", 512),
            ("Characters/employee_ghost", "Generated/employee_ghost", 512),
            ("Characters/employee_otherworld_hero", "Generated/employee_otherworld_hero", 512),
            ("Characters/employee_zhizhi", "Generated/employee_zhizhi", 512),
            ("Characters/character_pot_chief", "Generated/character_pot_chief", 512),
            ("Characters/character_elder", "Generated/character_elder", 512),
            ("Environments/environment_title_keyart", "MainMenu/bg_title_keyart", 2048)
        };

        /// <summary>
        /// 把正式生成素材复制到 Resources/UI 供运行时使用，并设置应用图标。
        /// 源文件保留在 Assets/Art/Generated，只有实际用到的副本进入构建。
        /// </summary>
        private static void DeployGeneratedUiArt()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedArtFolder)) return;

            EnsureFolderChain("Assets/Resources/UI");
            if (!AssetDatabase.IsValidFolder(ResourcesUiGeneratedFolder))
                AssetDatabase.CreateFolder("Assets/Resources/UI", "Generated");

            int copied = 0;
            foreach (var (source, target, maxSize) in GeneratedUiCopies)
            {
                string sourcePath = $"{GeneratedArtFolder}/{source}.png";
                string targetPath = $"Assets/Resources/UI/{target}.png";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath) == null)
                {
                    Debug.LogWarning($"[ArtIconLinker] 缺少生成素材：{sourcePath}");
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath) != null)
                {
                    EnsureSpriteImport(targetPath, maxSize);
                    continue;
                }

                if (AssetDatabase.CopyAsset(sourcePath, targetPath))
                {
                    EnsureSpriteImport(targetPath, maxSize);
                    copied++;
                }
            }

            ApplyAppIcon();
            AssetDatabase.SaveAssets();
            if (copied > 0)
                Debug.Log($"[ArtIconLinker] 已部署 {copied} 个生成 UI 素材到 Assets/Resources/UI。");

            // 九宫格皮肤素材必须裁掉透明留边，切边才能落在木框上。
            CropTransparentMargins($"{ResourcesUiGeneratedFolder}/ui_panel_main.png");
            CropTransparentMargins($"{ResourcesUiGeneratedFolder}/ui_button_primary.png");
        }

        /// <summary>
        /// 裁掉纹理四周完全透明的留白。IMGUI 的 GUIStyle 九宫格切边按 1:1 像素取样，
        /// 无法跳过透明边——不裁掉的话，按钮/面板的木框永远切在透明区上，
        /// 只剩中间压扁的一条，看起来像一根分割棍。幂等：没有留边时不改动。
        /// </summary>
        private static void CropTransparentMargins(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (source == null) return;

            int w = source.width;
            int h = source.height;
            var pixels = source.GetPixels32();
            int minX = w, maxX = -1, minY = h, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                int rowBase = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (pixels[rowBase + x].a <= 8) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0 || (minX == 0 && minY == 0 && maxX == w - 1 && maxY == h - 1))
            {
                RestoreUnreadable(path);
                return;
            }

            int cropW = maxX - minX + 1;
            int cropH = maxY - minY + 1;
            var region = source.GetPixels(minX, minY, cropW, cropH);
            var cropped = new Texture2D(cropW, cropH, TextureFormat.RGBA32, false);
            cropped.SetPixels(region);
            cropped.Apply(false, false);
            System.IO.File.WriteAllBytes(path, cropped.EncodeToPNG());
            Object.DestroyImmediate(cropped);
            AssetDatabase.ImportAsset(path);
            RestoreUnreadable(path);
            Debug.Log($"[ArtIconLinker] 已裁剪透明留边：{path} → {cropW}x{cropH}。");
        }

        private static void RestoreUnreadable(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            if (!importer.isReadable) return;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        /// <summary>启动图标：icon_app.png 作为独立平台默认图标（素材清单 7.2）。</summary>
        private static void ApplyAppIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GeneratedUiFolder}/icon_app.png");
            if (icon == null) return;

            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            int[] sizes = PlayerSettings.GetIconSizes(target, UnityEditor.IconKind.Any);
            var icons = new Texture2D[sizes.Length > 0 ? sizes.Length : 1];
            for (int i = 0; i < icons.Length; i++)
                icons[i] = icon;
            PlayerSettings.SetIcons(target, icons, UnityEditor.IconKind.Any);
        }

        private static void EnsureFolderChain(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolderChain(parent);
            if (!string.IsNullOrEmpty(parent))
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }

        private static Dictionary<string, Sprite> LoadCompletedSprites()
        {
            if (!AssetDatabase.IsValidFolder(CompletedArtFolder))
            {
                Debug.LogWarning($"[ArtIconLinker] 未找到素材目录：{CompletedArtFolder}");
                return new Dictionary<string, Sprite>();
            }

            return LoadSpritesFromFolder(CompletedArtFolder);
        }

        private static Dictionary<string, Sprite> LoadSpritesFromFolder(string folder)
        {
            var result = new Dictionary<string, Sprite>();
            if (!AssetDatabase.IsValidFolder(folder))
                return result;

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

                // 确保以透明 Sprite 类型导入，关闭图标不需要的 mipmap。
                EnsureSpriteImport(path);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    result[fileName] = sprite;
            }

            return result;
        }

        private static IngredientItem FindIngredient(string id, string displayName)
        {
            var db = AssetDatabase.LoadAssetAtPath<IngredientDatabase>(
                "Assets/Resources/IngredientDatabase.asset");
            db?.RebuildIndex();
            var byId = db != null ? db.GetById(id) : null;
            if (byId != null) return byId;

            foreach (var guid in AssetDatabase.FindAssets("t:IngredientItem"))
            {
                var item = AssetDatabase.LoadAssetAtPath<IngredientItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && item.DisplayName == displayName)
                    return item;
            }

            return null;
        }

        private static JobItem FindGatherJob(string ingredientDisplayName)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:JobItem"))
            {
                var job = AssetDatabase.LoadAssetAtPath<JobItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (job != null && job.JobType == JobType.Gather
                    && job.DisplayName == ingredientDisplayName)
                    return job;
            }

            return null;
        }
    }
}
