using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Soup.Game.Editor
{
    /// <summary>
    /// Builds a Windows standalone player. Trigger via menu, -executeMethod,
    /// or by writing an empty file at Temp/RequestWindowsBuild.
    /// </summary>
    public static class WindowsPlayerBuilder
    {
        public const string RequestFileRelative = "Temp/RequestWindowsBuild";
        public const string DefaultRelativeOutput = "Builds/Windows/soup.exe";

        [MenuItem("Soup/Build/Windows x64 Player")]
        public static void BuildFromMenu() => Build(exitEditor: false);

        /// <summary>Unity batchmode: -executeMethod Soup.Game.Editor.WindowsPlayerBuilder.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine() => Build(exitEditor: true);

        [InitializeOnLoadMethod]
        private static void WatchRequestFile()
        {
            EditorApplication.delayCall += TryBuildFromRequestFile;
            EditorApplication.update += OnEditorUpdate;
        }

        private static double _nextRequestCheck;

        private static void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextRequestCheck)
                return;

            _nextRequestCheck = EditorApplication.timeSinceStartup + 1.0;
            TryBuildFromRequestFile();
        }

        private static void TryBuildFromRequestFile()
        {
                string requestPath = Path.Combine(Directory.GetCurrentDirectory(), RequestFileRelative);
                if (!File.Exists(requestPath))
                    return;

                try { File.Delete(requestPath); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[WindowsPlayerBuilder] Could not delete request file: {ex.Message}");
                    return;
                }

                Debug.Log("[WindowsPlayerBuilder] Detected Temp/RequestWindowsBuild — starting Windows build.");
                Build(exitEditor: false);
        }

        public static void Build(bool exitEditor)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string outputPath = Path.Combine(projectRoot, DefaultRelativeOutput);
            string outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            string[] scenes = EditorBuildSettings.scenes != null
                ? Array.FindAll(
                    Array.ConvertAll(EditorBuildSettings.scenes, s => s.enabled ? s.path : null),
                    p => !string.IsNullOrEmpty(p))
                : Array.Empty<string>();

            if (scenes.Length == 0)
            {
                Debug.LogError("[WindowsPlayerBuilder] No enabled scenes in Build Settings.");
                if (exitEditor)
                    EditorApplication.Exit(1);
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"[WindowsPlayerBuilder] Building → {outputPath}\nScenes: {string.Join(", ", scenes)}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WindowsPlayerBuilder] SUCCEEDED in {summary.totalTime}. Size={summary.totalSize} bytes. Output={outputPath}");
                if (exitEditor)
                    EditorApplication.Exit(0);
                return;
            }

            Debug.LogError($"[WindowsPlayerBuilder] FAILED: {summary.result}. Errors={summary.totalErrors}");
            if (exitEditor)
                EditorApplication.Exit(1);
        }
    }
}
