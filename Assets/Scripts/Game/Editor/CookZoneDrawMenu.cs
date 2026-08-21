using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Soup.Game.Editor
{
    /// <summary>Opens SampleScene on the cook zone so world sprites can be drawn in place.</summary>
    public static class CookZoneDrawMenu
    {
        public const string ZoneName = "CookZone";
        private const string FrameName = "画面范围";

        [MenuItem("Soup/烹饪区/打开自由绘制", false, 50)]
        public static void OpenCookZoneDraw()
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;

            if (!EnsurePlaySceneLoaded())
                return;

            var zone = EnsureCookZone();
            EnsureViewFrame(zone);
            FocusCookZone(zone);
            HideProceduralCookGround();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[烹饪区] 已打开 CookZone。把背景 / 岗位图拖到它下面即可，运行时不会覆盖。");
        }

        public static CookZoneView EnsureCookZone()
        {
            var zone = Object.FindObjectOfType<CookZoneView>(true);
            if (zone != null)
                return zone;

            var go = new GameObject(ZoneName);
            Undo.RegisterCreatedObjectUndo(go, "Create CookZone");
            zone = go.AddComponent<CookZoneView>();
            PlaceAtCookCenter(go.transform);
            return zone;
        }

        public static void FocusCookZone(CookZoneView zone)
        {
            if (zone == null) return;

            PlaceAtCookCenter(zone.transform);
            SnapEditorCameraToCook();

            Selection.activeTransform = zone.transform;
            EditorGUIUtility.PingObject(zone.gameObject);

            var view = SceneView.lastActiveSceneView;
            if (view == null && SceneView.sceneViews.Count > 0)
                view = SceneView.sceneViews[0] as SceneView;
            if (view != null)
            {
                view.in2DMode = true;
                view.orthographic = true;
                float size = 9.216f;
                var gather = Object.FindObjectOfType<GatherZoneView>(true);
                if (gather != null)
                {
                    float rec = gather.RecommendedOrthographicSize();
                    if (rec > 0.5f) size = rec;
                }

                view.LookAt(zone.transform.position, Quaternion.identity, size);
                view.Repaint();
            }
        }

        private static void PlaceAtCookCenter(Transform zone)
        {
            if (zone == null) return;

            var gather = Object.FindObjectOfType<GatherZoneView>(true);
            var cam = Object.FindObjectOfType<ZoneCameraController>(true);
            if (cam != null && gather != null)
            {
                float size = gather.RecommendedOrthographicSize();
                float spacing = gather.RecommendedZoneSpacing();
                cam.ConfigureView(spacing, size);
                zone.position = cam.GetZoneCenter(MapZoneType.Cook);
                return;
            }

            if (gather != null)
            {
                float spacing = gather.RecommendedZoneSpacing();
                if (spacing <= 1f) spacing = 35.768f;
                zone.position = gather.transform.position + new Vector3(spacing * 2f, 0f, 0f);
                return;
            }

            zone.position = new Vector3(35.768f, 0f, 0f);
        }

        private static void SnapEditorCameraToCook()
        {
            var main = Camera.main;
            var zoneCam = Object.FindObjectOfType<ZoneCameraController>(true);
            if (zoneCam != null)
            {
                if (main != null)
                {
                    var so = new SerializedObject(zoneCam);
                    var prop = so.FindProperty("targetCamera");
                    if (prop != null && prop.objectReferenceValue == null)
                    {
                        prop.objectReferenceValue = main;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                zoneCam.SnapToZone(MapZoneType.Cook);
            }

            if (main == null) return;

            var cook = Object.FindObjectOfType<CookZoneView>(true);
            Vector3 center = cook != null ? cook.transform.position : Vector3.zero;
            main.orthographic = true;
            float size = 9.216f;
            var gather = Object.FindObjectOfType<GatherZoneView>(true);
            if (gather != null)
            {
                float rec = gather.RecommendedOrthographicSize();
                if (rec > 0.5f) size = rec;
            }

            main.orthographicSize = size;
            main.transform.position = new Vector3(center.x, center.y, -10f);
        }

        private static void EnsureViewFrame(CookZoneView zone)
        {
            if (zone == null) return;

            var existing = zone.transform.Find(FrameName);
            if (existing != null) return;

            float width = 32.768f;
            float height = 18.432f;
            var gather = Object.FindObjectOfType<GatherZoneView>(true);
            if (gather != null && gather.Background != null)
            {
                width = gather.Background.bounds.size.x;
                height = gather.Background.bounds.size.y;
            }

            var go = new GameObject(FrameName);
            Undo.RegisterCreatedObjectUndo(go, "Create Cook Frame");
            go.transform.SetParent(zone.transform, false);
            go.transform.localPosition = Vector3.zero;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateRectSprite();
            sr.color = new Color(0.95f, 0.45f, 0.40f, 0.18f);
            sr.sortingOrder = -5;
            go.transform.localScale = new Vector3(width, height, 1f);
        }

        private static void HideProceduralCookGround()
        {
            var map = Object.FindObjectOfType<JobWorldMap>(true);
            if (map == null) return;
            var root = map.transform.Find("Zones");
            if (root == null) return;
            var ground = root.Find("Ground_Cook");
            if (ground != null)
                ground.gameObject.SetActive(false);
        }

        private static Sprite CreateRectSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                1f);
        }

        public static bool EnsurePlaySceneLoaded()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path == GameScenes.PlayPath || scene.name == GameScenes.Play)
                return true;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            EditorSceneManager.OpenScene(GameScenes.PlayPath);
            return true;
        }
    }
}
