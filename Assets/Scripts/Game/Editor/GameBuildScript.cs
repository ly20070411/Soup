using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Soup.Game.Editor
{
    /// <summary>
    /// 一键打包 Windows x64 exe。场景列表：MainMenu → SampleScene → InterLevel → VictorySettlement。
    /// </summary>
    public static class GameBuildScript
    {
        private static readonly string[] RequiredScenes =
        {
            GameScenes.MainMenuPath,
            GameScenes.PlayPath,
            GameScenes.InterLevelPath,
            GameScenes.VictorySettlementPath
        };

        [MenuItem("Soup/Build/打包 Windows EXE")]
        public static void BuildWindows()
        {
            if (!EnsureBuildScenes(writeSettings: true))
                return;

            // 产品名 / 公司名只在为空时兜底，正式名称请在 PlayerSettings 里改。
            if (string.IsNullOrEmpty(PlayerSettings.productName))
                PlayerSettings.productName = "汤灵纪行";
            if (string.IsNullOrEmpty(PlayerSettings.companyName))
                PlayerSettings.companyName = "Soup Kitchen";

            string folder = EditorUtility.OpenFolderPanel(
                "选择打包输出文件夹（建议空文件夹，如 Builds/Win64）", "Builds", "");
            if (string.IsNullOrEmpty(folder))
                return;

            string exePath = Path.Combine(folder, $"{PlayerSettings.productName}.exe");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = RequiredScenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog(
                    "打包失败",
                    $"结果：{report.summary.result}\n错误 {report.summary.totalErrors} 个，详情见 Console。",
                    "确定");
                Debug.LogError($"[Build] 打包失败：{report.summary.result}，错误 {report.summary.totalErrors} 个。");
                return;
            }

            float sizeMb = report.summary.totalSize / (1024f * 1024f);
            Debug.Log($"[Build] 打包完成：{exePath}（{sizeMb:F1} MB）。分发时请压缩整个输出文件夹。");
            EditorUtility.RevealInFinder(exePath);
        }

        [MenuItem("Soup/Build/同步 Build Settings 场景")]
        public static void SyncBuildScenesMenu()
        {
            if (EnsureBuildScenes(writeSettings: true))
                Debug.Log("[Build] Build Settings 已同步：MainMenu / SampleScene / InterLevel / VictorySettlement");
        }

        [InitializeOnLoadMethod]
        private static void EnsureBuildScenesOnLoad()
        {
            // Keep InterLevel loadable in Editor Play Mode after scripts recompile.
            EnsureBuildScenes(writeSettings: true);
        }

        private static bool EnsureBuildScenes(bool writeSettings)
        {
            if (!System.IO.File.Exists(GameScenes.VictorySettlementPath))
                VictorySettlementSceneMenu.EnsureSceneAsset();

            for (int i = 0; i < RequiredScenes.Length; i++)
            {
                if (!File.Exists(RequiredScenes[i]))
                {
                    Debug.LogError($"[Build] 找不到场景：{RequiredScenes[i]}");
                    return false;
                }
            }

            if (!writeSettings)
                return true;

            var scenes = new EditorBuildSettingsScene[RequiredScenes.Length];
            for (int i = 0; i < RequiredScenes.Length; i++)
                scenes[i] = new EditorBuildSettingsScene(RequiredScenes[i], true);
            EditorBuildSettings.scenes = scenes;
            return true;
        }
    }
}
