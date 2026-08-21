using UnityEngine;
using UnityEngine.SceneManagement;

namespace Soup.Game
{
    /// <summary>
    /// Boots the graphical play view: zones, camera, overlay HUD.
    /// Only runs in the play scene — the main menu is a separate scene.
    /// </summary>
    public class GameWorldBootstrap : MonoBehaviour
    {
        [SerializeField] private bool buildOnAwake = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsure()
        {
            if (!Application.isPlaying) return;
            if (!IsPlayScene()) return;
            if (FindObjectOfType<GameWorldBootstrap>() != null) return;

            var go = new GameObject(nameof(GameWorldBootstrap));
            go.AddComponent<GameWorldBootstrap>();
        }

        private static bool IsPlayScene()
        {
            var scene = SceneManager.GetActiveScene();
            return scene.name == GameScenes.Play;
        }

        private void Awake()
        {
            if (!IsPlayScene())
            {
                Destroy(gameObject);
                return;
            }

            if (buildOnAwake)
                EnsureWorld();

            // AssetRipper left 0×0 YaHei textures in SampleScene — repair before first frame draw.
            SafeUiFont.RepairAllInLoadedScenes();
        }

        [ContextMenu("Ensure World View")]
        public void EnsureWorld()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var zoneCam = cam.GetComponent<ZoneCameraController>();
                if (zoneCam == null)
                    zoneCam = cam.gameObject.AddComponent<ZoneCameraController>();
                zoneCam.SnapToZone(MapZoneType.Gather);
            }

            if (FindObjectOfType<JobWorldMap>() == null)
            {
                var mapGo = new GameObject("JobWorldMap");
                mapGo.AddComponent<JobWorldMap>();
            }

            if (FindObjectOfType<GameOverlayUI>() == null)
            {
                var uiGo = new GameObject("GameOverlayUI");
                uiGo.AddComponent<GameOverlayUI>();
            }

            var hud = FindObjectOfType<GamePlayHud>();
            if (hud != null)
                hud.SetPanelMode(false);

            var overlay = FindObjectOfType<GameOverlayUI>();
            overlay?.SetPlayHudVisible(true);
        }
    }
}
