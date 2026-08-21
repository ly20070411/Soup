using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Soup.Game.Editor
{
    /// <summary>
    /// 无论当前打开的是哪个场景，点 Play 都从主菜单进入，
    /// 避免直接进 SampleScene 时看不到开始页。
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeStartScene
    {
        static PlayModeStartScene()
        {
            Apply();
        }

        [MenuItem("Soup/Play From Main Menu", priority = 10)]
        private static void ApplyMenu()
        {
            Apply();
            Debug.Log("[Soup] Play Mode 起始场景已设为 MainMenu。");
        }

        [MenuItem("Soup/Play From Current Scene", priority = 11)]
        private static void ClearMenu()
        {
            EditorSceneManager.playModeStartScene = null;
            Debug.Log("[Soup] 已清除 Play Mode 起始场景（将从当前打开的场景进入）。");
        }

        private static void Apply()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenes.MainMenuPath);
            if (scene == null)
            {
                Debug.LogWarning("[Soup] 找不到主菜单场景: " + GameScenes.MainMenuPath);
                return;
            }

            EditorSceneManager.playModeStartScene = scene;
        }
    }
}
