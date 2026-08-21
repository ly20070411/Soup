using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Soup.Game.Editor
{
    /// <summary>Opens SampleScene on the process zone so world sprites can be drawn in place.</summary>
    public static class ProcessZoneDrawMenu
    {
        public const string ZoneName = "ProcessZone";

        [MenuItem("Soup/处理区/打开自由绘制", false, 40)]
        public static void OpenProcessZoneDraw()
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;

            if (!EnsurePlaySceneLoaded())
                return;

            var zone = EnsureProcessZone();
            FocusProcessZone(zone);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[处理区] 已打开 ProcessZone。把背景 / 岗位图拖到它下面即可，运行时不会覆盖。");
        }

        public static ProcessZoneView EnsureProcessZone()
        {
            var zone = Object.FindObjectOfType<ProcessZoneView>(true);
            if (zone != null)
                return zone;

            var go = new GameObject(ZoneName);
            Undo.RegisterCreatedObjectUndo(go, "Create ProcessZone");
            zone = go.AddComponent<ProcessZoneView>();
            PlaceAtProcessCenter(go.transform);
            return zone;
        }

        public static void FocusProcessZone(ProcessZoneView zone)
        {
            if (zone == null) return;

            PlaceAtProcessCenter(zone.transform);
            SnapEditorCameraToProcess();

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

        private static void PlaceAtProcessCenter(Transform zone)
        {
            if (zone == null) return;

            var gather = Object.FindObjectOfType<GatherZoneView>(true);
            var cam = Object.FindObjectOfType<ZoneCameraController>(true);
            if (cam != null && gather != null)
            {
                float size = gather.RecommendedOrthographicSize();
                float spacing = gather.RecommendedZoneSpacing();
                cam.ConfigureView(spacing, size);
                zone.position = cam.GetZoneCenter(MapZoneType.Process);
                return;
            }

            if (gather != null)
            {
                float spacing = gather.RecommendedZoneSpacing();
                if (spacing <= 1f) spacing = 35.768f;
                zone.position = gather.transform.position + new Vector3(spacing, 0f, 0f);
                return;
            }

            zone.position = Vector3.zero;
        }

        private static void SnapEditorCameraToProcess()
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

                zoneCam.SnapToZone(MapZoneType.Process);
            }

            if (main == null) return;

            var process = Object.FindObjectOfType<ProcessZoneView>(true);
            Vector3 center = process != null ? process.transform.position : Vector3.zero;
            main.orthographic = true;
            main.transform.position = new Vector3(center.x, center.y, -10f);
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
