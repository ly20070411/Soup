using Soup.Jobs;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Soup.Game
{
    /// <summary>
    /// Carries Start / Continue intent from the main-menu into the play scene.
    /// Advancement visit relies on <see cref="AdvancementVisit.IsActive"/> surviving the scene load
    /// (same as the working player build).
    /// </summary>
    public static class GameSessionLaunch
    {
        private enum Intent
        {
            None,
            NewGame,
            Continue
        }

        private static Intent _intent = Intent.None;
        private static string _pendingGatherStarterJobId;
        private static string _pendingProcessStarterJobId;
        private static bool _pendingCampaignVictory;
        private static bool _pendingLevelDefeat;
        private static string _defeatLevelName = string.Empty;
        private static int _defeatScore;
        private static int _defeatTargetScore;
        private static bool _hooked;

        public readonly struct PendingLevelDefeatInfo
        {
            public readonly string LevelName;
            public readonly int Score;
            public readonly int TargetScore;

            public PendingLevelDefeatInfo(string levelName, int score, int targetScore)
            {
                LevelName = levelName ?? string.Empty;
                Score = score;
                TargetScore = targetScore;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _intent = Intent.None;
            _pendingGatherStarterJobId = null;
            _pendingProcessStarterJobId = null;
            _pendingCampaignVictory = false;
            _pendingLevelDefeat = false;
            _defeatLevelName = string.Empty;
            _defeatScore = 0;
            _defeatTargetScore = 0;
            _hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static void RequestNewGame(
            string gatherStarterJobId = null,
            string processStarterJobId = null)
        {
            if (BlockDissolveTransition.IsBusy) return;

            AdvancementVisit.Clear();
            _intent = Intent.NewGame;
            _pendingGatherStarterJobId = gatherStarterJobId;
            _pendingProcessStarterJobId = processStarterJobId;
            BlockDissolveTransition.Play(() => SceneManager.LoadScene(GameScenes.Play));
        }

        public static void RequestContinue()
        {
            if (BlockDissolveTransition.IsBusy) return;

            AdvancementVisit.Clear();
            _intent = Intent.Continue;
            _pendingGatherStarterJobId = null;
            _pendingProcessStarterJobId = null;
            BlockDissolveTransition.Play(() => SceneManager.LoadScene(GameScenes.Play));
        }

        public static void ReturnToMainMenu()
        {
            if (BlockDissolveTransition.IsBusy) return;

            AdvancementVisit.Clear();
            _intent = Intent.None;
            _pendingGatherStarterJobId = null;
            _pendingProcessStarterJobId = null;
            BlockDissolveTransition.Play(() => SceneManager.LoadScene(GameScenes.MainMenu));
        }

        /// <summary>全战役通关：进入胜利结算场景（美术待定，当前为占位 UI）。</summary>
        public static void GoToVictorySettlement()
        {
            if (BlockDissolveTransition.IsBusy) return;

            if (!CampaignVictorySession.TryBeginFromLevelManager())
            {
                Debug.LogWarning("[GameSessionLaunch] 无法抓取胜利结算数据，回退主菜单弹窗。");
                FallbackCampaignVictoryToMainMenu();
                return;
            }

            if (!CanLoadScene(GameScenes.VictorySettlement))
            {
                Debug.LogWarning(
                    "[GameSessionLaunch] 未找到胜利结算场景，回退主菜单弹窗。请运行 Soup/场景/确保胜利结算场景。");
                CampaignVictorySession.Clear();
                FallbackCampaignVictoryToMainMenu();
                return;
            }

            AdvancementVisit.Clear();
            _intent = Intent.None;
            _pendingGatherStarterJobId = null;
            _pendingProcessStarterJobId = null;
            BlockDissolveTransition.Play(() => SceneManager.LoadScene(GameScenes.VictorySettlement));
        }

        /// <summary>最后一关通关入口（兼容旧调用）。</summary>
        public static void DeclareCampaignVictory() => GoToVictorySettlement();

        private static void FallbackCampaignVictoryToMainMenu()
        {
            AdvancementVisit.Clear();
            _pendingCampaignVictory = true;
            _intent = Intent.None;
            _pendingGatherStarterJobId = null;
            _pendingProcessStarterJobId = null;
            BlockDissolveTransition.Play(() => SceneManager.LoadScene(GameScenes.MainMenu));
        }

        private static bool CanLoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
                return false;
            return true;
        }

        public static bool ConsumePendingCampaignVictory()
        {
            if (!_pendingCampaignVictory) return false;
            _pendingCampaignVictory = false;
            return true;
        }

        /// <summary>关卡失败：回主菜单并弹出失败弹窗（不进关卡间）。</summary>
        public static void DeclareLevelDefeat()
        {
            if (BlockDissolveTransition.IsBusy) return;

            CapturePendingLevelDefeatFromManager();
            AdvancementVisit.Clear();
            _intent = Intent.None;
            _pendingGatherStarterJobId = null;
            _pendingProcessStarterJobId = null;
            BlockDissolveTransition.Play(() => SceneManager.LoadScene(GameScenes.MainMenu));
        }

        public static bool ConsumePendingLevelDefeat(out PendingLevelDefeatInfo info)
        {
            if (!_pendingLevelDefeat)
            {
                info = default;
                return false;
            }

            _pendingLevelDefeat = false;
            info = new PendingLevelDefeatInfo(_defeatLevelName, _defeatScore, _defeatTargetScore);
            _defeatLevelName = string.Empty;
            _defeatScore = 0;
            _defeatTargetScore = 0;
            return true;
        }

        private static void CapturePendingLevelDefeatFromManager()
        {
            var levels = Soup.Levels.LevelManager.Instance;
            if (levels != null)
            {
                _defeatLevelName = levels.Current != null ? levels.Current.DisplayName : "本关";
                _defeatScore = levels.LastFinishedScore;
                _defeatTargetScore = levels.TargetScore;
            }
            else
            {
                _defeatLevelName = "本关";
                _defeatScore = 0;
                _defeatTargetScore = 0;
            }

            _pendingLevelDefeat = true;
        }

        /// <summary>进入关卡间独立场景（通关 hub / 失败页）。</summary>
        public static void GoToInterLevel(bool useDissolve = true)
        {
            if (!Application.isPlaying) return;
            // Build + no-dissolve: never kick out of an active advancement visit.
            if (AdvancementVisit.IsActive) return;
            if (SceneManager.GetActiveScene().name == GameScenes.InterLevel) return;
            // Build: while dissolve is covering, do not start another InterLevel nav.
            if (BlockDissolveTransition.IsBusy) return;
            if (Object.FindObjectOfType<InterLevelNavRunner>() != null) return;

            AdvancementVisit.Clear();
            _intent = Intent.None;

            var go = new GameObject(nameof(InterLevelNavRunner));
            Object.DontDestroyOnLoad(go);
            var runner = go.AddComponent<InterLevelNavRunner>();
            runner.useDissolve = useDissolve;
        }

        /// <summary>从关卡间回到玩法场景（保留当前局进度）。</summary>
        public static void ReturnToPlay(bool useDissolve = true)
        {
            _intent = Intent.None;
            _pendingGatherStarterJobId = null;
            _pendingProcessStarterJobId = null;

            if (!useDissolve)
            {
                // 无画面转场，但仍占用 IsBusy（与 build 溶解窗口相同），
                // 防止 ClearRewards 在进阶 IsActive 生效前把人踢回关卡间。
                BlockDissolveTransition.HoldBusyForFrames(4);
                SceneManager.LoadScene(GameScenes.Play);
                return;
            }

            if (BlockDissolveTransition.IsBusy) return;

            BlockDissolveTransition.Play(() => SceneManager.LoadScene(GameScenes.Play));
        }

        /// <summary>关卡间点进阶：直切玩法，无溶解转场。</summary>
        public static void ReturnToPlayForAdvancement()
        {
            // Must already have AdvancementVisit.Begin(...) — same contract as the build.
            ReturnToPlay(useDissolve: false);
        }

        private sealed class InterLevelNavRunner : MonoBehaviour
        {
            public bool useDissolve = true;

            private System.Collections.IEnumerator Start()
            {
                // 等本帧回合结算栈结束后再截屏切场景，避免中途销毁对象。
                yield return null;
                if (SceneManager.GetActiveScene().name == GameScenes.InterLevel)
                {
                    Destroy(gameObject);
                    yield break;
                }

                if (!useDissolve)
                {
                    if (SceneManager.GetActiveScene().name != GameScenes.InterLevel)
                        SceneManager.LoadScene(GameScenes.InterLevel);
                    Destroy(gameObject);
                    yield break;
                }

                BlockDissolveTransition.Play(() =>
                {
                    if (SceneManager.GetActiveScene().name != GameScenes.InterLevel)
                        SceneManager.LoadScene(GameScenes.InterLevel);
                });
                Destroy(gameObject);
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == GameScenes.InterLevel
                || scene.name == GameScenes.VictorySettlement)
                return;

            if (scene.name != GameScenes.Play)
                return;

            var intent = _intent;
            var gatherStarterJobId = _pendingGatherStarterJobId;
            var processStarterJobId = _pendingProcessStarterJobId;
            _intent = Intent.None;
            _pendingGatherStarterJobId = null;
            _pendingProcessStarterJobId = null;

            var runner = new GameObject(nameof(GameSessionLaunch)).AddComponent<LaunchRunner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.Begin(intent, gatherStarterJobId, processStarterJobId);
        }

        private sealed class LaunchRunner : MonoBehaviour
        {
            private Intent _pending;
            private string _gatherStarterJobId;
            private string _processStarterJobId;

            public void Begin(
                Intent intent,
                string gatherStarterJobId,
                string processStarterJobId)
            {
                _pending = intent;
                _gatherStarterJobId = gatherStarterJobId;
                _processStarterJobId = processStarterJobId;
                StartCoroutine(ApplyNextFrame());
            }

            private System.Collections.IEnumerator ApplyNextFrame()
            {
                yield return null;

                TurnManager.Initialize();

                // Exact build order: advancement visit wins over ClearRewards → InterLevel bounce.
                if (AdvancementVisit.IsActive)
                {
                    EnterAdvancementVisitView();
                    Destroy(gameObject);
                    yield break;
                }

                if (_pending == Intent.Continue || _pending == Intent.None)
                {
                    var levels = Soup.Levels.LevelManager.Instance;
                    if (levels != null && levels.IsLost)
                    {
                        CapturePendingLevelDefeatFromManager();
                        AdvancementVisit.Clear();
                        _intent = Intent.None;
                        BlockDissolveTransition.Play(() => SceneManager.LoadScene(GameScenes.MainMenu));
                        Destroy(gameObject);
                        yield break;
                    }

                    if (levels != null && levels.HasActiveClearRewards)
                    {
                        GoToInterLevel();
                        Destroy(gameObject);
                        yield break;
                    }
                }

                if (_pending == Intent.None)
                {
                    Destroy(gameObject);
                    yield break;
                }

                if (_pending == Intent.NewGame)
                {
                    TurnManager.Instance?.ResetRun();

                    var overlay = FindObjectOfType<GameOverlayUI>();
                    string toast = string.Empty;

                    var progression = JobProgressionManager.Instance;
                    var jobs = JobManager.Instance;

                    if (!string.IsNullOrWhiteSpace(_gatherStarterJobId))
                    {
                        var job = jobs != null ? jobs.GetById(_gatherStarterJobId) : null;
                        if (progression != null && job != null && progression.TryPickGatherStarter(job))
                            toast = AppendToast(toast, $"采集：{job.DisplayName}");
                        else
                            Debug.LogWarning("[GameSessionLaunch] Failed to unlock gather starter: " + _gatherStarterJobId);
                    }

                    if (!string.IsNullOrWhiteSpace(_processStarterJobId))
                    {
                        var job = jobs != null ? jobs.GetById(_processStarterJobId) : null;
                        if (progression != null && job != null && progression.TryPickProcessStarter(job))
                            toast = AppendToast(toast, $"处理：{job.DisplayName}");
                        else
                            Debug.LogWarning("[GameSessionLaunch] Failed to unlock process starter: " + _processStarterJobId);
                    }

                    if (overlay != null && !string.IsNullOrEmpty(toast))
                        overlay.ShowToast(toast, 3.5f);
                }
                else if (_pending == Intent.Continue)
                {
                    if (!GameSaveService.TryLoad(out var message))
                        Debug.LogWarning("[GameSessionLaunch] Continue failed: " + message);
                }

                try
                {
                    var map = FindObjectOfType<JobWorldMap>();
                    if (map != null)
                    {
                        map.RebuildStations();
                        map.RefreshLabels();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[GameSessionLaunch] RebuildStations failed: " + e.Message);
                }

                var playOverlay = FindObjectOfType<GameOverlayUI>();
                playOverlay?.SetPlayHudVisible(true);

                var starterUi = StarterJobSelectUI.Ensure(playOverlay != null ? playOverlay.transform : null);
                starterUi.BeginIfNeeded();

                Destroy(gameObject);
            }

            private static void EnterAdvancementVisitView()
            {
                SafeUiFont.RepairAllInLoadedScenes();

                // Match build; also create JobWorldMap if restored scene never had one.
                if (FindObjectOfType<JobWorldMap>() == null)
                {
                    var mapGo = new GameObject("JobWorldMap");
                    mapGo.AddComponent<JobWorldMap>();
                }

                var map = FindObjectOfType<JobWorldMap>();
                if (map != null)
                {
                    map.RebuildStations();
                    map.RefreshLabels();
                    map.ApplyAdvancementVisitPresentation();
                }

                ZoneViewFraming.ApplyZoneCamera(
                    FindObjectOfType<ZoneCameraController>(),
                    AdvancementVisit.Zone,
                    snap: true);
                FindObjectOfType<GameOverlayUI>()?.SetPlayHudVisible(false);
                AdvancementVisitUI.Ensure();
                Debug.Log($"[GameSessionLaunch] Advancement visit ready zone={AdvancementVisit.Zone}");
            }

            private static string AppendToast(string current, string part)
            {
                if (string.IsNullOrEmpty(part)) return current;
                return string.IsNullOrEmpty(current) ? part : $"{current} · {part}";
            }
        }
    }
}
