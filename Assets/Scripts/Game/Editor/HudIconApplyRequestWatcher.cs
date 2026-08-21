#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Soup.Game.Editor
{
    /// <summary>
    /// Polls Temp/RequestApplyHudIcons so external tools can bind + place HUD icons
    /// while the editor is already open.
    /// </summary>
    [InitializeOnLoad]
    public static class HudIconApplyRequestWatcher
    {
        private const string RequestRelative = "Temp/RequestApplyHudIcons";
        private static double _nextCheck;

        static HudIconApplyRequestWatcher()
        {
            _nextCheck = EditorApplication.timeSinceStartup + 1.0;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            if (EditorApplication.timeSinceStartup < _nextCheck)
                return;
            _nextCheck = EditorApplication.timeSinceStartup + 1.0;

            string path = Path.Combine(Directory.GetCurrentDirectory(), RequestRelative);
            if (!File.Exists(path))
                return;

            try { File.Delete(path); }
            catch { return; }

            Debug.Log("[HudIconApplyRequestWatcher] Binding art + applying HUD icons…");
            ArtAssetBinder.BindAll();
            Debug.Log(PlayAuthoredHudInstaller.ApplyResourceFlavorIcons());
        }
    }
}
#endif
