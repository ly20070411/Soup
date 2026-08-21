using UnityEngine;

namespace Soup.Game
{
    /// <summary>主菜单可调整并持久化的轻量设置。</summary>
    public static class GameSettings
    {
        private const string VolumeKey = "Soup.Settings.MasterVolume";
        private const string FullscreenKey = "Soup.Settings.Fullscreen";
        private const string TutorialKey = "Soup.Settings.TutorialTips";

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            set
            {
                float clamped = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, clamped);
                AudioListener.volume = clamped;
                PlayerPrefs.Save();
            }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            set
            {
                PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
                Screen.fullScreen = value;
                PlayerPrefs.Save();
            }
        }

        public static bool TutorialTips
        {
            get => PlayerPrefs.GetInt(TutorialKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(TutorialKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyStoredSettings()
        {
            AudioListener.volume = MasterVolume;
            if (PlayerPrefs.HasKey(FullscreenKey))
                Screen.fullScreen = Fullscreen;
        }
    }
}
