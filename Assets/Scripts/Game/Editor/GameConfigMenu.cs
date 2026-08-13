using UnityEditor;
using UnityEngine;

namespace Soup.Game.Editor
{
    /// <summary>
    /// Ensures GameConfig exists and opens it for editing.
    /// </summary>
    public static class GameConfigMenu
    {
        private const string ConfigPath = "Assets/Resources/GameConfig.asset";

        [MenuItem("Soup/游戏配置 (Game Config)")]
        public static void OpenOrCreate()
        {
            EnsureFolder("Assets/Resources");

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                config.SetStartingElfCount(10);
                config.SetWarehouseCapacity(1000);
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[游戏配置] 已创建: " + ConfigPath);
            }

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
