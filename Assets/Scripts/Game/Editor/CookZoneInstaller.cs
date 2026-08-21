using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Soup.Game.Editor
{
    /// <summary>Wires authored cook backdrop into CookZoneView / heat slots / score HUD.</summary>
    public static class CookZoneInstaller
    {
        // Sprite-local positions (art scale applied by parenting under the backdrop).
        private static readonly Vector3[] HeatLocalPositions =
        {
            new Vector3(-8.16f, -4.05f, 0f),
            new Vector3(-3.28f, -3.85f, 0f),
            new Vector3(1.28f, -3.75f, 0f),
        };

        private const string SquareUiPath = "Assets/美术资产/UI/UI/UI/方形ui.png";

        // World-ish sizes after art scale 1.6 — keep compact beside each flame.
        private const float CountFrameWorldW = 2.15f;
        private const float CountFrameWorldH = 0.92f;
        private const float AssignButtonWorld = 0.72f;

        private static readonly Vector3 ScoreBurstLocal = new Vector3(-1.34f, 2.39f, -0.02f);
        private static readonly Vector3 SpicyLocal = new Vector3(6.50f, 2.83f, -0.02f);
        private static readonly Vector3 ColdLocal = new Vector3(7.05f, 1.54f, -0.02f);
        private static readonly Vector3 SourLocal = new Vector3(7.37f, 0.39f, -0.02f);
        private static readonly Vector3 MagicLocal = new Vector3(7.36f, -0.80f, -0.02f);

        [MenuItem("Soup/烹饪区/安装场景绑定", false, 51)]
        public static void InstallFromMenu()
        {
            Debug.Log(Install());
        }

        public static string Install()
        {
            if (!CookZoneDrawMenu.EnsurePlaySceneLoaded())
                return "SampleScene not loaded";

            var zone = CookZoneDrawMenu.EnsureCookZone();
            var art = FindCookArt();
            if (art == null)
                return "Missing 烹饪 art";

            art.SetParent(zone.transform, true);
            art.name = "烹饪";
            RemoveChild(zone.transform, "画面范围");

            var artSr = art.GetComponent<SpriteRenderer>();
            var scoreHud = EnsureScoreHud(art);
            var heats = new CookHeatSlot[CookZoneView.HeatStationCount];
            for (int i = 0; i < heats.Length; i++)
                heats[i] = EnsureHeatSlot(art, i);

            var so = new SerializedObject(zone);
            so.FindProperty("background").objectReferenceValue = artSr;
            so.FindProperty("artRoot").objectReferenceValue = art;
            so.FindProperty("scoreHud").objectReferenceValue = scoreHud;
            var heatProp = so.FindProperty("heatStations");
            heatProp.arraySize = heats.Length;
            for (int i = 0; i < heats.Length; i++)
                heatProp.GetArrayElementAtIndex(i).objectReferenceValue = heats[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            WireOverlay(zone);
            CookZoneDrawMenu.FocusCookZone(zone);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            return $"CookZone wired heats={heats.Length} scoreHud=True art={art.name}";
        }

        private static Transform FindCookArt()
        {
            var zone = Object.FindObjectOfType<CookZoneView>(true);
            if (zone != null)
            {
                for (int i = 0; i < zone.transform.childCount; i++)
                {
                    var child = zone.transform.GetChild(i);
                    if (child.name.Contains("烹饪") || child.name.Contains("Cook"))
                        return child;
                }
            }

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name.Contains("烹饪"))
                    return roots[i].transform;
            }

            return null;
        }

        private static CookScoreHud EnsureScoreHud(Transform art)
        {
            var existing = art.GetComponent<CookScoreHud>();
            if (existing == null)
                existing = art.gameObject.AddComponent<CookScoreHud>();

            existing.EnsureTexts();
            PlaceText(art, "StageScore", ScoreBurstLocal, 0.20f, 52, new Color(0.55f, 0.12f, 0.05f, 1f));
            PlaceText(art, "SpicyScore", SpicyLocal, 0.15f, 34, Color.white);
            PlaceText(art, "ColdScore", ColdLocal, 0.15f, 34, Color.white);
            PlaceText(art, "SourScore", SourLocal, 0.15f, 34, Color.white);
            PlaceText(art, "MagicScore", MagicLocal, 0.15f, 34, Color.white);

            var so = new SerializedObject(existing);
            so.FindProperty("stageScoreMesh").objectReferenceValue = FindText(art, "StageScore");
            so.FindProperty("spicyMesh").objectReferenceValue = FindText(art, "SpicyScore");
            so.FindProperty("coldMesh").objectReferenceValue = FindText(art, "ColdScore");
            so.FindProperty("sourMesh").objectReferenceValue = FindText(art, "SourScore");
            so.FindProperty("magicMesh").objectReferenceValue = FindText(art, "MagicScore");
            so.ApplyModifiedPropertiesWithoutUndo();
            existing.Refresh();
            return existing;
        }

        private static CookHeatSlot EnsureHeatSlot(Transform art, int index)
        {
            string name = "Heat_" + index;
            var existing = art.Find(name);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(art, false);
            }

            go.transform.localPosition = HeatLocalPositions[index];
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var slot = go.GetComponent<CookHeatSlot>();
            if (slot == null)
                slot = go.AddComponent<CookHeatSlot>();

            var artLib = GameArtLibrary.Load();
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareUiPath);
            if (square == null && artLib != null)
                square = artLib.ButtonBackground;

            var frame = EnsureChildRenderer(go.transform, "CountFrame", square);
            float parentX = Mathf.Abs(art.lossyScale.x);
            float parentY = Mathf.Abs(art.lossyScale.y);
            Vector2 native = square != null ? square.bounds.size : new Vector2(5.73f, 2.28f);
            float sx = native.x > 0.01f ? CountFrameWorldW / (native.x * Mathf.Max(0.01f, parentX)) : 0.24f;
            float sy = native.y > 0.01f ? CountFrameWorldH / (native.y * Mathf.Max(0.01f, parentY)) : 0.25f;
            frame.transform.localPosition = new Vector3(1.45f, 0.12f, -0.01f);
            frame.transform.localScale = new Vector3(sx, sy, 1f);
            frame.sortingOrder = 26;
            frame.color = Color.white;

            var minus = EnsureAssignButton(go.transform, "MinusButton", true, artLib);
            var plus = EnsureAssignButton(go.transform, "PlusButton", false, artLib);
            float btnScale = ScaleForWorldSprite(
                AssignSprite(artLib, true),
                AssignButtonWorld,
                parentX);
            minus.localPosition = new Vector3(0.55f, 0.12f, -0.01f);
            plus.localPosition = new Vector3(2.35f, 0.12f, -0.01f);
            minus.localScale = new Vector3(btnScale, btnScale, 1f);
            plus.localScale = new Vector3(btnScale, btnScale, 1f);

            var count = GatherHudText.FindDirect(frame.transform, "Count")?.GetComponent<TextMesh>();
            if (count == null)
            {
                count = GatherHudText.Ensure(
                    frame.transform,
                    "Count",
                    new Vector3(0f, 0f, -0.02f),
                    GatherHudText.LocalScaleForWorld(frame.transform, 0.22f),
                    42,
                    42);
            }

            if (count != null)
            {
                if (string.IsNullOrEmpty(count.text))
                    count.text = "0/∞";
                count.color = GatherHudText.Ink;
                count.anchor = TextAnchor.MiddleCenter;
                count.alignment = TextAlignment.Center;
                // Do not move/scale — leave authored Transform for Scene editing.
            }

            // Drop any leftover Count sibling under Heat_* (pre-square-ui layout).
            var stray = GatherHudText.FindDirect(go.transform, "Count");
            if (stray != null && (count == null || stray != count.transform))
                Object.DestroyImmediate(stray.gameObject);

            var so = new SerializedObject(slot);
            so.FindProperty("jobId").stringValue = CookZoneView.HeatJobIds[index];
            so.FindProperty("countFrame").objectReferenceValue = frame;
            so.FindProperty("countMesh").objectReferenceValue = count;
            so.FindProperty("minusButton").objectReferenceValue = minus;
            so.FindProperty("plusButton").objectReferenceValue = plus;
            so.ApplyModifiedPropertiesWithoutUndo();
            slot.Configure(CookZoneView.HeatJobIds[index]);
            return slot;
        }

        private static SpriteRenderer EnsureChildRenderer(Transform parent, string name, Sprite sprite)
        {
            var tf = parent.Find(name);
            GameObject go;
            if (tf == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
            }
            else go = tf.gameObject;

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = go.AddComponent<SpriteRenderer>();
            if (sprite != null)
                sr.sprite = sprite;
            return sr;
        }

        private static Sprite AssignSprite(GameArtLibrary art, bool isMinus)
        {
            if (art == null) return null;
            return isMinus ? art.ZoneSwitchLeft : art.ZoneSwitchRight;
        }

        private static float ScaleForWorldSprite(Sprite sprite, float worldSize, float parentLossy)
        {
            float native = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 1f;
            float denom = native * Mathf.Max(0.01f, parentLossy);
            return worldSize / denom;
        }

        private static Transform EnsureAssignButton(Transform parent, string name, bool isMinus, GameArtLibrary art)
        {
            var tf = parent.Find(name);
            GameObject go;
            if (tf == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
            }
            else go = tf.gameObject;

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = isMinus
                ? (art != null ? art.ZoneSwitchLeft : null)
                : (art != null ? art.ZoneSwitchRight : null);
            sr.sortingOrder = 28;

            if (go.GetComponent<Collider2D>() == null)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
            }

            return go.transform;
        }

        private static void PlaceText(
            Transform art,
            string name,
            Vector3 localPos,
            float worldScale,
            int fontSize,
            Color color)
        {
            var mesh = FindText(art, name);
            if (mesh == null)
            {
                mesh = GatherHudText.Ensure(
                    art,
                    name,
                    localPos,
                    GatherHudText.LocalScaleForWorld(art, worldScale),
                    28,
                    fontSize);
            }

            if (mesh == null) return;

            mesh.color = color;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            if (string.IsNullOrEmpty(mesh.text))
                mesh.text = name.Contains("Stage") ? "0" : (name.Contains("Spicy") ? "×1.0" : "0.0");
            // Existing objects keep their authored Transform; only brand-new ones use localPos.
        }

        private static TextMesh FindText(Transform art, string name)
        {
            var t = GatherHudText.FindDirect(art, name);
            return t != null ? t.GetComponent<TextMesh>() : null;
        }

        private static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
        }

        private static void WireOverlay(CookZoneView zone)
        {
            var overlay = Object.FindObjectOfType<GameOverlayUI>(true);
            if (overlay == null) return;
            var so = new SerializedObject(overlay);
            so.FindProperty("cookZone").objectReferenceValue = zone;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
