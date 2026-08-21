using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Soup.Game.Editor
{
    /// <summary>
    /// 一键打包 Windows x64 exe：
    /// 固定把唯一场景 SampleScene 写入构建列表（所有管理器由 RuntimeInitializeOnLoad
    /// 在运行时自建，场景只承载 GamePlayHud 等少量对象），弹出文件夹选择框，
    /// 输出“产品名.exe + Data 目录 + UnityPlayer.dll”的完整游戏包文件夹。
    /// 分发时压缩整个输出文件夹即可（或后续用 Inno Setup 做安装器）。
    /// </summary>
    public static class GameBuildScript
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Soup/Build/打包 Windows EXE")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[Build] 找不到主场景：{ScenePath}");
                return;
            }

            // 产品名 / 公司名只在为空时兜底，正式名称请在 PlayerSettings 里改。
            if (string.IsNullOrEmpty(PlayerSettings.productName))
                PlayerSettings.productName = "汤灵纪行";
            if (string.IsNullOrEmpty(PlayerSettings.companyName))
                PlayerSettings.companyName = "Soup Kitchen";

            // 构建场景列表固定为唯一场景，防止面板里勾选状态漂移。
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            string folder = EditorUtility.OpenFolderPanel(
                "选择打包输出文件夹（建议空文件夹，如 Builds/Win64）", "Builds", "");
            if (string.IsNullOrEmpty(folder))
                return;

            string exePath = Path.Combine(folder, $"{PlayerSettings.productName}.exe");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
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
    }
}
