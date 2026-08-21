using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Soup.Game.Editor
{
    /// <summary>
    /// Wires scene-authored process zone sprites into ProcessZoneView / ProcessStationSlot.
    /// </summary>
    public static class ProcessZoneInstaller
    {
        private const string SquareUiPath = "Assets/美术资产/UI/UI/UI/方形ui.png";
        private const float CountFrameWorldW = 2.15f;
        private const float CountFrameWorldH = 0.92f;
        private const float AssignButtonWorld = 0.72f;

        private static readonly (string objectName, string jobId)[] JobArtNames =
        {
            ("爆炸", "explosion"),
            ("电锯", "chainsaw"),
            ("钻头", "drill"),
            ("刀切", "knife_cut"),
        };

        [MenuItem("Soup/处理区/安装场景绑定", false, 41)]
        public static void InstallFromMenu()
        {
            Debug.Log(Install());
        }

        public static string Install()
        {
            if (!ProcessZoneDrawMenu.EnsurePlaySceneLoaded())
                return "SampleScene not loaded";

            var zone = ProcessZoneDrawMenu.EnsureProcessZone();
            var zoneTf = zone.transform;

            var explosion = FindRoot("爆炸");
            var warningSource = FindRoot("警告牌");
            var slot0Pos = explosion != null ? explosion.position : new Vector3(-8.86f, -4.51f, 0f);
            var slot1Pos = warningSource != null ? warningSource.position : new Vector3(8.94f, -4.14f, 0f);

            Reparent(zoneTf, "墙壁", setBackground: true);
            Reparent(zoneTf, "桌子");
            Reparent(zoneTf, "仓库", addWarehouseHud: true);

            var presetsRoot = EnsureChild(zoneTf, "ArtPresets");
            presetsRoot.gameObject.SetActive(false);

            int presetCount = 0;
            for (int i = 0; i < JobArtNames.Length; i++)
            {
                var (name, _) = JobArtNames[i];
                var src = FindRoot(name);
                if (src == null) continue;
                src.SetParent(presetsRoot, true);
                presetCount++;
            }

            if (warningSource != null)
                warningSource.SetParent(presetsRoot, true);

            RemoveChild(zoneTf, "画面范围");

            var station0 = EnsureStation(zoneTf, 0, slot0Pos, warningSource);
            var station1 = EnsureStation(zoneTf, 1, slot1Pos, warningSource);

            var warehouseTf = FindInZone(zoneTf, "仓库");
            var warehouseHud = EnsureWarehouse(warehouseTf);
            var bg = GetBackground(zone);

            var so = new SerializedObject(zone);
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("warehouse").objectReferenceValue = warehouseHud;
            so.FindProperty("warningTemplate").objectReferenceValue =
                warningSource != null ? warningSource.GetComponent<SpriteRenderer>() : null;

            var stationsProp = so.FindProperty("stations");
            stationsProp.arraySize = ProcessZoneView.StationCount;
            stationsProp.GetArrayElementAtIndex(0).objectReferenceValue = station0;
            stationsProp.GetArrayElementAtIndex(1).objectReferenceValue = station1;

            var presetsProp = so.FindProperty("jobArtPresets");
            presetsProp.arraySize = JobArtNames.Length;
            for (int i = 0; i < JobArtNames.Length; i++)
            {
                var (name, jobId) = JobArtNames[i];
                var src = presetsRoot.Find(name);
                var elem = presetsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("jobId").stringValue = jobId;
                elem.FindPropertyRelative("template").objectReferenceValue =
                    src != null ? src.GetComponent<SpriteRenderer>() : null;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            station0.BindEmpty();
            station1.BindEmpty();

            WireOverlayProcessZone(zone);

            ProcessZoneDrawMenu.FocusProcessZone(zone);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            return $"ProcessZone wired stations=2 presets={presetCount} warehouse={(warehouseTf != null)}";
        }

        private static void Reparent(Transform zone, string name, bool setBackground = false, bool addWarehouseHud = false)
        {
            var tf = FindRoot(name);
            if (tf == null) return;
            tf.SetParent(zone, true);
            if (setBackground && zone.GetComponent<ProcessZoneView>() is ProcessZoneView view)
            {
                var so = new SerializedObject(view);
                so.FindProperty("background").objectReferenceValue = tf.GetComponent<SpriteRenderer>();
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (addWarehouseHud && tf.GetComponent<GatherWarehouseHud>() == null)
                tf.gameObject.AddComponent<GatherWarehouseHud>();
        }

        private static Transform FindRoot(string name)
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                    return roots[i].transform;
            }

            var zone = Object.FindObjectOfType<ProcessZoneView>(true);
            return zone != null ? FindInZone(zone.transform, name) : null;
        }

        private static Transform FindInZone(Transform zone, string name)
        {
            if (zone == null) return null;
            for (int i = 0; i < zone.childCount; i++)
            {
                var child = zone.GetChild(i);
                if (child.name == name) return child;
            }

            return zone.Find(name);
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
        }

        private static ProcessStationSlot EnsureStation(
            Transform zone,
            int index,
            Vector3 worldPos,
            Transform warningSource)
        {
            string stationName = "Station_" + index;
            var existing = zone.Find(stationName);
            GameObject stationGo;
            if (existing != null)
                stationGo = existing.gameObject;
            else
            {
                stationGo = new GameObject(stationName);
                stationGo.transform.SetParent(zone, false);
            }

            stationGo.transform.position = worldPos;
            var slot = stationGo.GetComponent<ProcessStationSlot>();
            if (slot == null)
                slot = stationGo.AddComponent<ProcessStationSlot>();

            var warning = EnsureChildRenderer(stationGo.transform, "WarningSign", warningSource);
            var portrait = EnsureChildRenderer(stationGo.transform, "Portrait", null);
            portrait.enabled = false;
            portrait.gameObject.SetActive(false);

            var artLib = GameArtLibrary.Load();
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareUiPath);
            if (square == null && artLib != null)
                square = artLib.ButtonBackground;

            var frame = EnsureNamedRenderer(stationGo.transform, "CountFrame", square);
            Vector2 native = square != null ? square.bounds.size : new Vector2(5.73f, 2.28f);
            float parentX = Mathf.Abs(stationGo.transform.lossyScale.x);
            float parentY = Mathf.Abs(stationGo.transform.lossyScale.y);
            float sx = native.x > 0.01f ? CountFrameWorldW / (native.x * Mathf.Max(0.01f, parentX)) : 0.24f;
            float sy = native.y > 0.01f ? CountFrameWorldH / (native.y * Mathf.Max(0.01f, parentY)) : 0.25f;
            frame.transform.localPosition = new Vector3(0f, 2.8f, -0.01f);
            frame.transform.localScale = new Vector3(sx, sy, 1f);
            frame.sortingOrder = 10;
            frame.color = Color.white;
            frame.gameObject.SetActive(false);

            var minus = EnsureAssignButton(stationGo.transform, "MinusButton", true, artLib);
            var plus = EnsureAssignButton(stationGo.transform, "PlusButton", false, artLib);
            float btnScale = ScaleForWorldSprite(
                artLib != null ? artLib.ZoneSwitchLeft : null,
                AssignButtonWorld,
                parentX);
            minus.localPosition = new Vector3(-1.55f, 2.8f, 0f);
            plus.localPosition = new Vector3(1.55f, 2.8f, 0f);
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
                    frame.sortingOrder + 3,
                    42);
            }

            if (count != null)
            {
                if (string.IsNullOrEmpty(count.text))
                    count.text = "0/∞";
                count.color = GatherHudText.Ink;
                count.anchor = TextAnchor.MiddleCenter;
                count.alignment = TextAlignment.Center;
            }

            var stray = GatherHudText.FindDirect(stationGo.transform, "Count");
            if (stray != null && (count == null || stray != count.transform))
                Object.DestroyImmediate(stray.gameObject);

            var slotSo = new SerializedObject(slot);
            slotSo.FindProperty("slotIndex").intValue = index;
            slotSo.FindProperty("warningSign").objectReferenceValue = warning;
            slotSo.FindProperty("portrait").objectReferenceValue = portrait;
            slotSo.FindProperty("countFrame").objectReferenceValue = frame;
            slotSo.FindProperty("countMesh").objectReferenceValue = count;
            slotSo.FindProperty("minusButton").objectReferenceValue = minus;
            slotSo.FindProperty("plusButton").objectReferenceValue = plus;
            slotSo.ApplyModifiedPropertiesWithoutUndo();

            slot.ConfigureIndex(index);
            return slot;
        }

        private static SpriteRenderer EnsureNamedRenderer(Transform parent, string name, Sprite sprite)
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

        private static float ScaleForWorldSprite(Sprite sprite, float worldSize, float parentLossy)
        {
            float native = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 1f;
            float denom = native * Mathf.Max(0.01f, parentLossy);
            return worldSize / denom;
        }

        private static SpriteRenderer EnsureChildRenderer(Transform parent, string name, Transform copyFrom)
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

            if (copyFrom != null)
            {
                var src = copyFrom.GetComponent<SpriteRenderer>();
                if (src != null)
                {
                    sr.sprite = src.sprite;
                    sr.color = src.color;
                    sr.sortingOrder = src.sortingOrder + 1;
                    go.transform.localScale = Vector3.one;
                }
            }

            go.transform.localPosition = Vector3.zero;
            return sr;
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
            sr.sortingOrder = 12;

            if (go.GetComponent<Collider2D>() == null)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
            }

            return go.transform;
        }

        private static GatherWarehouseHud EnsureWarehouse(Transform warehouseTf)
        {
            if (warehouseTf == null) return null;
            var hud = warehouseTf.GetComponent<GatherWarehouseHud>();
            return hud != null ? hud : warehouseTf.gameObject.AddComponent<GatherWarehouseHud>();
        }

        private static SpriteRenderer GetBackground(ProcessZoneView zone)
        {
            var wall = zone.transform.Find("墙壁");
            return wall != null ? wall.GetComponent<SpriteRenderer>() : zone.Background;
        }

        private static void WireOverlayProcessZone(ProcessZoneView zone)
        {
            var overlay = Object.FindObjectOfType<GameOverlayUI>(true);
            if (overlay == null) return;

            var so = new SerializedObject(overlay);
            so.FindProperty("processZone").objectReferenceValue = zone;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
