using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// 退出游戏的统一入口。
    /// Application.Quit() 在 Editor 播放模式下是空操作，需改停止播放；
    /// 打包后走真正的进程退出。
    /// </summary>
    public static class GameExit
    {
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
