using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Soup.Game.Editor
{
    /// <summary>
    /// Converts the authored 1920×1080 world-space HUD sprites into OverlayCanvas widgets.
    /// </summary>
    public static class PlayAuthoredHudInstaller
    {
        [MenuItem("Soup/玩法界面/安装已绘制 HUD", false, 22)]
        public static void InstallFromMenu()
        {
            Debug.Log(Install());
        }

        [MenuItem("Soup/玩法界面/应用食材与风味图标", false, 23)]
        public static void ApplyResourceFlavorIconsFromMenu()
        {
            Debug.Log(ApplyResourceFlavorIcons());
        }

        private const string RequestFileRelative = "Temp/RequestApplyHudIcons";

        [InitializeOnLoadMethod]
        private static void WatchApplyRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), RequestFileRelative);
                if (!System.IO.File.Exists(path))
                    return;
                try { System.IO.File.Delete(path); }
                catch { return; }
                ArtAssetBinder.BindAll();
                Debug.Log(ApplyResourceFlavorIcons());
            };
        }

        /// <summary>
        /// Binds library sprites into existing AuthoredHud Circle_* slots (keeps layout).
        /// </summary>
        public static string ApplyResourceFlavorIcons()
        {
            var hud = Object.FindObjectOfType<PlayAuthoredHud>();
            if (hud == null)
                return "No PlayAuthoredHud in scene";

            var art = GameArtLibrary.Load();
            if (art == null)
                return "No GameArtLibrary";

            int hits = HudResourceIconApplier.ApplyAll(hud.transform, art);
            EditorUtility.SetDirty(hud.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            return $"Applied HUD resource/flavor icons: {hits}/{HudResourceIconApplier.Keys.Length}";
        }

        public static string Install()
        {
            var overlay = Object.FindObjectOfType<GameOverlayUI>();
            if (overlay == null)
                return "No GameOverlayUI";

            overlay.EnsureAuthoredCanvas(false);
            var freeDraw = overlay.FreeDrawRoot;
            if (freeDraw == null)
                return "No FreeDraw";

            var old = freeDraw.Find(PlayAuthoredHud.RootName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            var rootGo = new GameObject(PlayAuthoredHud.RootName, typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(freeDraw, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var font = GameOverlayUI.SharedUiFont();
            var sprites = CollectHudSprites();
            if (sprites.Brown == null || sprites.Gray == null)
                return "Missing brown/gray HUD sprites. circles=" + sprites.Circles.Count + " squares=" + sprites.Squares.Count;

            var brownImg = MakeImage("BrownBar", sprites.Brown, root, false);
            var grayImg = MakeImage("RelicBar", sprites.Gray, root, false);

            sprites.Circles.Sort(CompareByRowThenX);
            sprites.Squares.Sort(CompareByRowThenX);

            var topCircles = FilterY(sprites.Circles, 980f, 10000f);
            var topSquares = FilterY(sprites.Squares, 980f, 10000f);
            var midCircles = FilterY(sprites.Circles, 910f, 980f);
            var midSquares = FilterY(sprites.Squares, 910f, 980f);
            var lowCircles = FilterY(sprites.Circles, -10000f, 910f);
            var lowSquares = FilterY(sprites.Squares, -10000f, 910f);

            string[] topKeys = { "Soft", "Tough", "Solid", "Processed", "Cooked", "Spicy", "Cold" };
            var topTexts = new Dictionary<string, Text>();
            for (int i = 0; i < topKeys.Length; i++)
            {
                var circle = i < topCircles.Count ? topCircles[i] : null;
                var square = i < topSquares.Count ? topSquares[i] : null;
                var text = BindPair(topKeys[i], circle, square, root, font);
                if (text != null)
                    topTexts[topKeys[i]] = text;
            }

            var flavorCircles = FilterX(midCircles, 1300f, 10000f);
            var flavorSquares = FilterX(midSquares, 1300f, 10000f);
            string[] midFlavorKeys = { "Sour", "Magic" };
            for (int i = 0; i < midFlavorKeys.Length; i++)
            {
                var text = BindPair(
                    midFlavorKeys[i],
                    i < flavorCircles.Count ? flavorCircles[i] : null,
                    i < flavorSquares.Count ? flavorSquares[i] : null,
                    root,
                    font);
                if (text != null)
                    topTexts[midFlavorKeys[i]] = text;
            }

            var empCircle = lowCircles.Count > 0 ? lowCircles[0] : (midCircles.Count > 0 ? FirstX(midCircles, 300f) : null);
            var empSquare = lowSquares.Count > 0 ? lowSquares[0] : (midSquares.Count > 0 ? FirstX(midSquares, 300f) : null);
            Image employeeAvatar = null;
            Button employeeButton = null;
            Text employeeCount = null;
            RectTransform pickerRt = null;
            if (empCircle != null)
            {
                var frame = MakeImage("EmployeeFrame", empCircle, root, true);
                employeeButton = frame.gameObject.AddComponent<Button>();
                employeeButton.targetGraphic = frame.GetComponent<Image>();
                employeeAvatar = MakeChildImage("EmployeeAvatar", frame, new Vector2(48f, 48f), true);
                employeeAvatar.color = new Color(1f, 1f, 1f, 0f);
            }

            if (empSquare != null)
            {
                var sq = MakeImage("Square_Employee", empSquare, root, false);
                employeeCount = MakeValueText("Value_Employee", sq, font);
            }

            pickerRt = new GameObject("EmployeePicker", typeof(RectTransform)).GetComponent<RectTransform>();
            pickerRt.SetParent(root, false);
            pickerRt.anchorMin = Vector2.zero;
            pickerRt.anchorMax = Vector2.zero;
            pickerRt.pivot = new Vector2(0.5f, 1f);
            Vector2 empSize = empCircle != null
                ? new Vector2(empCircle.bounds.size.x, empCircle.bounds.size.y)
                : new Vector2(136f, 134f);
            pickerRt.sizeDelta = new Vector2(empSize.x, empSize.y * 4f);
            if (empCircle != null)
                pickerRt.anchoredPosition = (Vector2)empCircle.transform.position + new Vector2(0f, -empCircle.bounds.extents.y - 4f);
            else
                pickerRt.anchoredPosition = new Vector2(64f, 820f);
            pickerRt.gameObject.SetActive(false);

            var relicCircles = FilterX(midCircles, 450f, 1250f);
            var relicIcons = new List<Image>();
            for (int i = 0; i < relicCircles.Count; i++)
            {
                var frame = MakeImage("RelicSlot_" + i, relicCircles[i], root, true);
                frame.gameObject.AddComponent<RelicSlotHover>();
                var icon = MakeChildImage("Icon", frame, new Vector2(44f, 44f), false);
                icon.color = new Color(1f, 1f, 1f, 0f);
                relicIcons.Add(icon);
            }

            Button prevBtn = null;
            Button nextBtn = null;
            if (sprites.ArrowLeft != null)
            {
                var rt = MakeImage("RelicPrevBtn", sprites.ArrowLeft, root, true);
                prevBtn = rt.gameObject.AddComponent<Button>();
                prevBtn.targetGraphic = rt.GetComponent<Image>();
            }

            if (sprites.ArrowRight != null)
            {
                var rt = MakeImage("RelicNextBtn", sprites.ArrowRight, root, true);
                nextBtn = rt.gameObject.AddComponent<Button>();
                nextBtn.targetGraphic = rt.GetComponent<Image>();
            }

            var hud = rootGo.AddComponent<PlayAuthoredHud>();
            var so = new SerializedObject(hud);
            Assign(so, "softValue", FindText(topTexts, "Soft"));
            Assign(so, "toughValue", FindText(topTexts, "Tough"));
            Assign(so, "solidValue", FindText(topTexts, "Solid"));
            Assign(so, "processedValue", FindText(topTexts, "Processed"));
            Assign(so, "cookedValue", FindText(topTexts, "Cooked"));
            Assign(so, "spicyValue", FindText(topTexts, "Spicy"));
            Assign(so, "coldValue", FindText(topTexts, "Cold"));
            Assign(so, "sourValue", FindText(topTexts, "Sour"));
            Assign(so, "magicValue", FindText(topTexts, "Magic"));
            Assign(so, "employeeAvatar", employeeAvatar);
            Assign(so, "employeeCount", employeeCount);
            Assign(so, "employeeAvatarButton", employeeButton);
            Assign(so, "employeePickerRoot", pickerRt);
            Assign(so, "relicPrevButton", prevBtn);
            Assign(so, "relicNextButton", nextBtn);
            var slotsProp = so.FindProperty("relicSlots");
            slotsProp.arraySize = relicIcons.Count;
            for (int i = 0; i < relicIcons.Count; i++)
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = relicIcons[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var artLib = GameArtLibrary.Load();
            if (artLib != null)
                HudResourceIconApplier.ApplyAll(root, artLib);

            RelocateSystemButtons(overlay.transform);
            DisableWorldSprites(sprites);

            EditorUtility.SetDirty(overlay.gameObject);
            EditorUtility.SetDirty(rootGo);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            return "AuthoredHud children=" + root.childCount
                + " relics=" + relicIcons.Count
                + " topC=" + topCircles.Count
                + " midC=" + midCircles.Count
                + " lowC=" + lowCircles.Count
                + " brown=" + (brownImg != null)
                + " gray=" + (grayImg != null);
        }

        private static Text FindText(Dictionary<string, Text> map, string key)
        {
            Text text;
            return map.TryGetValue(key, out text) ? text : null;
        }

        private static void Assign(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null)
                prop.objectReferenceValue = value;
        }

        private static Text BindPair(string key, SpriteRenderer circle, SpriteRenderer square, Transform root, Font font)
        {
            if (circle != null)
                MakeImage("Circle_" + key, circle, root, false);
            if (square == null)
                return null;
            var sq = MakeImage("Square_" + key, square, root, false);
            return MakeValueText("Value_" + key, sq, font);
        }

        private static RectTransform MakeImage(string name, SpriteRenderer src, Transform parent, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = src.transform.position;
            rt.sizeDelta = new Vector2(src.bounds.size.x, src.bounds.size.y);
            var img = go.GetComponent<Image>();
            img.sprite = src.sprite;
            img.color = src.color;
            img.preserveAspect = false;
            img.raycastTarget = raycast;
            img.type = Image.Type.Simple;
            return rt;
        }

        private static Image MakeChildImage(string name, Transform parent, Vector2 size, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = raycast;
            return img;
        }

        private static Text MakeValueText(string name, Transform square, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(square, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 4f);
            rt.offsetMax = new Vector2(-8f, -4f);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.18f, 0.12f, 0.08f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 28;
            text.text = "0";
            return text;
        }

        private static int CompareByRowThenX(SpriteRenderer a, SpriteRenderer b)
        {
            int y = b.transform.position.y.CompareTo(a.transform.position.y);
            if (Mathf.Abs(a.transform.position.y - b.transform.position.y) > 40f)
                return y;
            return a.transform.position.x.CompareTo(b.transform.position.x);
        }

        private static List<SpriteRenderer> FilterY(List<SpriteRenderer> source, float minY, float maxY)
        {
            var list = new List<SpriteRenderer>();
            for (int i = 0; i < source.Count; i++)
            {
                float y = source[i].transform.position.y;
                if (y > minY && y <= maxY)
                    list.Add(source[i]);
            }

            return list;
        }

        private static List<SpriteRenderer> FilterX(List<SpriteRenderer> source, float minX, float maxX)
        {
            var list = new List<SpriteRenderer>();
            for (int i = 0; i < source.Count; i++)
            {
                float x = source[i].transform.position.x;
                if (x >= minX && x < maxX)
                    list.Add(source[i]);
            }

            return list;
        }

        private static SpriteRenderer FirstX(List<SpriteRenderer> source, float maxX)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].transform.position.x < maxX)
                    return source[i];
            }

            return null;
        }

        private static HudSprites CollectHudSprites()
        {
            var result = new HudSprites();
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                if (go.name.Contains("棕色"))
                    result.Brown = sr;
                else if (go.name.Contains("灰色"))
                    result.Gray = sr;
                else if (go.name.Contains("圆形"))
                    result.Circles.Add(sr);
                else if (go.name.Contains("方形"))
                    result.Squares.Add(sr);
                else if (go.name == "切换键左")
                    result.ArrowLeft = sr;
                else if (go.name == "切换键右")
                    result.ArrowRight = sr;
            }

            return result;
        }

        private static void DisableWorldSprites(HudSprites sprites)
        {
            var holderGo = GameObject.Find("HudWorldSource");
            if (holderGo == null)
                holderGo = new GameObject("HudWorldSource");
            holderGo.SetActive(false);

            DisableOne(sprites.Brown, holderGo.transform);
            DisableOne(sprites.Gray, holderGo.transform);
            DisableOne(sprites.ArrowLeft, holderGo.transform);
            DisableOne(sprites.ArrowRight, holderGo.transform);
            for (int i = 0; i < sprites.Circles.Count; i++)
                DisableOne(sprites.Circles[i], holderGo.transform);
            for (int i = 0; i < sprites.Squares.Count; i++)
                DisableOne(sprites.Squares[i], holderGo.transform);
        }

        private static void DisableOne(SpriteRenderer sr, Transform holder)
        {
            if (sr == null) return;
            sr.gameObject.transform.SetParent(holder, true);
            sr.gameObject.SetActive(false);
        }

        private static void RelocateSystemButtons(Transform overlayRoot)
        {
            HideHudButton(overlayRoot, "SettingsBtn");
            HideHudButton(overlayRoot, "ControlPanelBtn");
        }

        private static void HideHudButton(Transform root, string name)
        {
            var tf = FindNamed(root, name);
            if (tf != null)
                tf.gameObject.SetActive(false);
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindNamed(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private sealed class HudSprites
        {
            public SpriteRenderer Brown;
            public SpriteRenderer Gray;
            public SpriteRenderer ArrowLeft;
            public SpriteRenderer ArrowRight;
            public readonly List<SpriteRenderer> Circles = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> Squares = new List<SpriteRenderer>();
        }
    }
}
