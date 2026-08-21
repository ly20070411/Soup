using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Soup.Game.Editor
{
    /// <summary>Puts an editable play HUD canvas into SampleScene and selects the FreeDraw layer.</summary>
    public static class PlayHudCanvasMenu
    {
        [MenuItem("Soup/玩法界面/打开自由绘制画布", false, 20)]
        public static void OpenFreeDrawCanvas()
        {
            if (!EnsurePlaySceneLoaded())
                return;

            var overlay = EnsureOverlay();
            overlay.EnsureAuthoredCanvas(false);

            var freeDraw = overlay.FreeDrawRoot;
            if (freeDraw == null)
            {
                Debug.LogError("[玩法界面] 未能创建 FreeDraw 层。");
                return;
            }

            Selection.activeTransform = freeDraw;
            EditorGUIUtility.PingObject(freeDraw.gameObject);
            SceneView.FrameLastActiveSceneView();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[玩法界面] 已选中 FreeDraw。在其下添加 Image / Text / 按钮即可，运行时不会被覆盖。");
        }

        [MenuItem("Soup/玩法界面/确保玩法 HUD 画布", false, 21)]
        public static void EnsureCanvas()
        {
            if (!EnsurePlaySceneLoaded())
                return;

            var overlay = EnsureOverlay();
            overlay.EnsureAuthoredCanvas(false);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[玩法界面] SampleScene 已写入 OverlayCanvas / FreeDraw / 系统按钮。");
        }

        private static bool EnsurePlaySceneLoaded()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path == GameScenes.PlayPath || scene.name == GameScenes.Play)
                return true;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            EditorSceneManager.OpenScene(GameScenes.PlayPath);
            return true;
        }

        private static GameOverlayUI EnsureOverlay()
        {
            var overlay = Object.FindObjectOfType<GameOverlayUI>();
            if (overlay != null)
                return overlay;

            var go = new GameObject("GameOverlayUI");
            Undo.RegisterCreatedObjectUndo(go, "Create GameOverlayUI");
            overlay = go.AddComponent<GameOverlayUI>();
            return overlay;
        }
    }
}
