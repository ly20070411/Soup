using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Soup.Game.Editor
{
    /// <summary>Puts an editable main-menu canvas into MainMenu and selects the FreeDraw layer.</summary>
    public static class MainMenuCanvasMenu
    {
        [MenuItem("Soup/主菜单/打开自由绘制画布", false, 10)]
        public static void OpenFreeDrawCanvas()
        {
            if (!EnsureMainMenuSceneLoaded())
                return;

            var menu = EnsureMainMenu();
            menu.EnsureAuthoredCanvas(false);

            var freeDraw = menu.FreeDrawRoot;
            if (freeDraw == null)
            {
                Debug.LogError("[主菜单] 未能创建 FreeDraw 层。");
                return;
            }

            Selection.activeTransform = freeDraw;
            EditorGUIUtility.PingObject(freeDraw.gameObject);
            SceneView.FrameLastActiveSceneView();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[主菜单] 已选中 FreeDraw。在其下添加 Image / Text / 按钮即可，运行时不会被覆盖。");
        }

        [MenuItem("Soup/主菜单/确保主菜单画布", false, 11)]
        public static void EnsureCanvas()
        {
            if (!EnsureMainMenuSceneLoaded())
                return;

            var menu = EnsureMainMenu();
            menu.EnsureAuthoredCanvas(false);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[主菜单] MainMenu 场景已写入 MainMenuCanvas / FreeDraw。");
        }

        private static bool EnsureMainMenuSceneLoaded()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path == GameScenes.MainMenuPath || scene.name == GameScenes.MainMenu)
                return true;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            EditorSceneManager.OpenScene(GameScenes.MainMenuPath);
            return true;
        }

        private static MainMenuUI EnsureMainMenu()
        {
            var menu = Object.FindObjectOfType<MainMenuUI>();
            if (menu != null)
                return menu;

            var go = new GameObject("MainMenuUI");
            Undo.RegisterCreatedObjectUndo(go, "Create MainMenuUI");
            return go.AddComponent<MainMenuUI>();
        }
    }
}
