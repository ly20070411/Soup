using System.Collections.Generic;
using Soup.Employees;
using Soup.Jobs;
using Soup.Levels;
using Soup.Relics;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Main menu + new-game launch flow (开局采集二选一 / 处理四选一 / 遗物三选一).
    /// Keeps itself open until the setup picks are done, then hands over to gameplay.
    /// </summary>
    [DefaultExecutionOrder(-110)]
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad = true;

        private enum SetupPhase
        {
            Title,
            LevelPick,
            Settings,
            LoadPick,
            GatherPick,
            ProcessPick,
            RelicPick
        }

        private SetupPhase _phase = SetupPhase.Title;
        private bool _confirmExit;
        private readonly List<JobItem> _gatherCandidates = new List<JobItem>();
        private readonly List<JobItem> _processCandidates = new List<JobItem>();
        private readonly List<RelicItem> _relicCandidates = new List<RelicItem>();
        private bool _sessionLaunched;
        private Vector2 _scroll;
        private GUIStyle _boldLabel;
        private GUIStyle _titleLabel;
        private GUIStyle _subTitleLabel;
        private GUIStyle _footerLabel;

        public static MainMenuUI Instance { get; private set; }

        public bool IsOpen => _phase != SetupPhase.Title || !_sessionLaunched;

        /// <summary>回到主菜单（游戏中 ESC → 返回主菜单）。</summary>
        public static void Reopen()
        {
            if (Instance == null) return;
            Instance.enabled = true;
            Instance._phase = SetupPhase.Title;
            Instance._sessionLaunched = false;
            Instance._confirmExit = false;
            Instance._scroll = Vector2.zero;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(MainMenuUI));
            Instance = go.AddComponent<MainMenuUI>();
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            if (go.GetComponent<MainMenuBackground>() == null)
                go.AddComponent<MainMenuBackground>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (_sessionLaunched && _phase == SetupPhase.Title)
                enabled = false;
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            // IMGUI 层级：越小越上层；主菜单必须盖住背景与世界地图。
            GUI.depth = 0;

            if (_phase == SetupPhase.Title)
            {
                DrawTitleScreen();
                return;
            }

            // 选择 / 开局页保持居中面板。
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = Mathf.Min(720f, Screen.width - 48f);
            float height = Mathf.Min(640f, Screen.height - 48f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(area, SoupUITheme.PanelBox);

            switch (_phase)
            {
                case SetupPhase.LevelPick: DrawLevelPick(); break;
                case SetupPhase.Settings: DrawSettings(); break;
                case SetupPhase.LoadPick: DrawLoadPick(); break;
                case SetupPhase.GatherPick: DrawGatherPick(); break;
                case SetupPhase.ProcessPick: DrawProcessPick(); break;
                case SetupPhase.RelicPick: DrawRelicPick(); break;
            }

            GUILayout.EndArea();
        }

        /// <summary>主标题页：标题与按钮整体垂直/水平居中，背景透出（动画或暗化的游戏界面）。</summary>
        private void DrawTitleScreen()
        {
            // 轻微暗化保证文字可读（背景在最底层）。
            GUI.color = new Color(0f, 0f, 0f, 0.32f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(0f, 0f, Screen.width, Screen.height));
            GUILayout.FlexibleSpace();

            DrawTitleLogo();
            GUILayout.Label("一锅好汤，从赣地食材开始。", SubTitleLabel());
            GUILayout.Space(36f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(280f));
            DrawTitleButtons();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.Space(16f);
            GUILayout.Label("F1 操控面板 · 世界地图在左侧 · [Esc] 暂停菜单", FooterLabel());
            GUILayout.Space(20f);
            GUILayout.EndArea();
        }

        /// <summary>
        /// 星饰文字双语 LOGO（logo_title_text_only，素材清单建议显示宽度 420–680 px）。
        /// 素材未部署时回退到原文字标题。
        /// </summary>
        private void DrawTitleLogo()
        {
            var logo = SoupUITheme.GetGeneratedTexture("logo_title_text_only");
            if (logo == null)
            {
                GUILayout.Label("赣什么 · 熬汤记", TitleLabel());
                GUILayout.Space(6f);
                return;
            }

            float width = Mathf.Min(680f, Screen.width * 0.62f);
            float height = width * (logo.height / (float)logo.width);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Box(logo, GUIStyle.none, GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
        }

        private void DrawTitleButtons()
        {
            if (GUILayout.Button("开始战役", SoupUITheme.PrimaryButton, GUILayout.Height(44f), GUILayout.ExpandWidth(true)))
                StartNewGame(0, LevelRunMode.Campaign);

            int latestSlot = GameSaveService.FindLatestSlot();
            GUI.enabled = latestSlot > 0;
            if (GUILayout.Button("继续游戏", SoupUITheme.PrimaryButton, GUILayout.Height(44f), GUILayout.ExpandWidth(true)))
            {
                var data = GameSaveService.LoadFromDisk(latestSlot);
                if (data != null && GameSaveService.StartRunFromSave(data))
                    FinishSetup(commitLevelSnapshot: false);
            }

            GUI.enabled = GameSaveService.FindLatestSlot() > 0;
            if (GUILayout.Button("读取游戏", SoupUITheme.PrimaryButton, GUILayout.Height(44f), GUILayout.ExpandWidth(true)))
            {
                _scroll = Vector2.zero;
                _phase = SetupPhase.LoadPick;
            }

            GUI.enabled = true;
            if (GUILayout.Button("选择关卡", SoupUITheme.PrimaryButton, GUILayout.Height(44f), GUILayout.ExpandWidth(true)))
            {
                _scroll = Vector2.zero;
                _phase = SetupPhase.LevelPick;
            }

            if (GUILayout.Button("设置", SoupUITheme.PrimaryButton, GUILayout.Height(44f), GUILayout.ExpandWidth(true)))
                _phase = SetupPhase.Settings;

            GUILayout.Space(12f);
            if (_confirmExit)
            {
                GUILayout.Label("确定要退出游戏吗？", BoldLabel());
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("确定退出", SoupUITheme.PrimaryButton, GUILayout.Height(36f)))
                    GameExit.Quit();
                if (GUILayout.Button("取消", SoupUITheme.PrimaryButton, GUILayout.Height(36f)))
                    _confirmExit = false;
                GUILayout.EndHorizontal();
            }
            else if (GUILayout.Button("退出游戏", SoupUITheme.PrimaryButton, GUILayout.Height(44f), GUILayout.ExpandWidth(true)))
            {
                _confirmExit = true;
            }
        }

        /// <summary>关卡选择：已解锁关卡可作为单关练习，不推进连续战役。</summary>
        private void DrawLevelPick()
        {
            GUILayout.Label("选择关卡", TitleLabel());

            var levels = LevelManager.Instance;
            if (levels == null || !levels.HasLevels)
            {
                GUILayout.Label("无关卡数据。请先执行菜单 Soup/Level Manager/Seed Design Levels。", SoupUITheme.Label);
                if (GUILayout.Button("返回", GUILayout.Height(36f), GUILayout.Width(120f)))
                    _phase = SetupPhase.Title;
                return;
            }

            int unlocked = LevelManager.UnlockedLevelIndex;
            var db = levels.Database;
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            for (int i = 0; i < db.Count; i++)
            {
                var level = db.Levels[i];
                if (level == null) continue;
                bool locked = i > unlocked;

                GUILayout.BeginHorizontal("box");
                if (level.Background != null)
                    GUILayout.Box(level.Background.texture, GUILayout.Width(128f), GUILayout.Height(72f));
                GUILayout.Label(level.DisplayName, BoldLabel(), GUILayout.Width(170f));
                GUILayout.Label(
                    $"通关条件：{level.MaxTurns} 回合内累计 {level.TargetScore} 分\n{level.Description}\n{level.SecretGoal}",
                    GUILayout.MinWidth(260f));

                GUI.enabled = !locked;
                if (GUILayout.Button(locked ? "未解锁" : "练习", SoupUITheme.Button, GUILayout.Width(80f), GUILayout.Height(44f)))
                {
                    _scroll = Vector2.zero;
                    StartNewGame(i, LevelRunMode.Practice);
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.Label("练习模式只运行所选关卡，不发放关间奖励、不推进战役；“开始战役”始终从第一关出发。", SoupUITheme.Label);
            if (GUILayout.Button("返回", GUILayout.Height(36f), GUILayout.Width(120f)))
                _phase = SetupPhase.Title;
        }

        private void DrawSettings()
        {
            GUILayout.Label("设置", TitleLabel());
            GUILayout.Space(18f);

            GUILayout.BeginVertical("box");
            GUILayout.Label($"主音量：{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%", BoldLabel());
            float volume = GUILayout.HorizontalSlider(GameSettings.MasterVolume, 0f, 1f, GUILayout.Height(28f));
            if (!Mathf.Approximately(volume, GameSettings.MasterVolume))
                GameSettings.MasterVolume = volume;

            bool fullscreen = GUILayout.Toggle(GameSettings.Fullscreen, "全屏显示");
            if (fullscreen != GameSettings.Fullscreen)
                GameSettings.Fullscreen = fullscreen;

            bool tutorial = GUILayout.Toggle(GameSettings.TutorialTips, "显示前三回合锅长提示");
            if (tutorial != GameSettings.TutorialTips)
                GameSettings.TutorialTips = tutorial;
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("返回", SoupUITheme.Button, GUILayout.Height(36f), GUILayout.Width(120f)))
                _phase = SetupPhase.Title;
        }

        /// <summary>主菜单读档：选择槽位直接进入对应进度。</summary>
        private void DrawLoadPick()
        {
            GUILayout.Label("读取存档", TitleLabel());

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            int existing = 0;
            for (int slot = 1; slot <= GameSaveService.SlotCount; slot++)
            {
                var info = GameSaveService.GetSlotInfo(slot);
                if (!info.Exists) continue;
                existing++;

                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"槽位 {slot}", BoldLabel(), GUILayout.Width(60f));
                GUILayout.Label(
                    $"{info.LevelDisplayName} · 总分 {info.TotalScore} · 回合 {info.TurnIndex}\n保存时间 {info.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}",
                    GUILayout.MinWidth(240f));

                if (GUILayout.Button("读取", SoupUITheme.Button, GUILayout.Width(80f), GUILayout.Height(44f)))
                {
                    var data = GameSaveService.LoadFromDisk(slot);
                    if (data != null && GameSaveService.StartRunFromSave(data))
                    {
                        _scroll = Vector2.zero;
                        FinishSetup(commitLevelSnapshot: false);
                        return;
                    }
                }

                if (GUILayout.Button("删除", SoupUITheme.PanelButton, GUILayout.Width(60f), GUILayout.Height(44f)))
                    GameSaveService.DeleteSlot(slot);

                GUILayout.EndHorizontal();
            }

            if (existing == 0)
                GUILayout.Label("（没有存档。游戏中按 [Esc] → 保存游戏。）", SoupUITheme.Label);

            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            if (GUILayout.Button("返回", SoupUITheme.Button, GUILayout.Height(36f), GUILayout.Width(120f)))
            {
                _scroll = Vector2.zero;
                _phase = SetupPhase.Title;
            }
        }

        private void StartNewGame(int startLevelIndex, LevelRunMode runMode)
        {
            // 1. Core managers (ResourceStore has no auto-init).
            var config = Resources.Load<GameConfig>(ResourceStore.ResourcesConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                config.hideFlags = HideFlags.HideAndDontSave;
            }

            ResourceStore.Initialize(config);
            ElfManager.Initialize(config);
            TurnManager.Initialize();
            EmployeeManager.Instance?.ResetFromConfig();

            // 2. Fresh run state. 连续战役固定从第一关开始；关卡选择进入练习模式。
            TurnManager.Instance?.ResetRun(restartLevel: false);
            LevelManager.Initialize();
            LevelManager.Instance?.BeginRun(startLevelIndex, runMode);

            // 3. Starter picks.
            _gatherCandidates.Clear();
            _processCandidates.Clear();
            _relicCandidates.Clear();

            var progression = JobProgressionManager.Instance;
            if (progression != null)
            {
                if (progression.NeedsGatherStarterPick)
                    PickRandom(progression.GetLocked(JobType.Gather), 2, _gatherCandidates);
                if (progression.NeedsProcessStarterPick)
                    PickRandom(progression.GetLocked(JobType.Process), 4, _processCandidates);
            }

            var relics = RelicManager.Instance;
            if (relics != null)
                _relicCandidates.AddRange(relics.CreateOffer(3, RelicAcquireStage.Starting));

            if (_gatherCandidates.Count > 0)
                _phase = SetupPhase.GatherPick;
            else if (_processCandidates.Count > 0)
                _phase = SetupPhase.ProcessPick;
            else if (_relicCandidates.Count > 0)
                _phase = SetupPhase.RelicPick;
            else
                FinishSetup();
        }

        private void DrawGatherPick()
        {
            GUILayout.Label("开局采集岗：二选一", SoupUITheme.BoldLabel);
            GUILayout.Label("选择一个采集岗位作为起点：", SoupUITheme.Label);
            DrawJobPick(_gatherCandidates, job =>
            {
                if (JobProgressionManager.Instance?.TryPickGatherStarter(job) == true)
                    NextPhase(SetupPhase.ProcessPick);
            });
            DrawBackToTitleButton();
        }

        private void DrawProcessPick()
        {
            GUILayout.Label("开局处理岗：四选一", SoupUITheme.BoldLabel);
            GUILayout.Label("选择一个处理岗位作为起点：", SoupUITheme.Label);
            DrawJobPick(_processCandidates, job =>
            {
                if (JobProgressionManager.Instance?.TryPickProcessStarter(job) == true)
                    NextPhase(SetupPhase.RelicPick);
            });
            DrawBackToTitleButton();
        }

        private void DrawJobPick(List<JobItem> candidates, System.Action<JobItem> onPick)
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < candidates.Count; i++)
            {
                var job = candidates[i];
                if (job == null) continue;
                GUILayout.BeginHorizontal("box");
                GUILayout.Label(job.DisplayName, BoldLabel(), GUILayout.Width(140f));
                GUILayout.Label(job.GetEffectSummary(), GUILayout.MinWidth(240f));
                if (GUILayout.Button("选择", GUILayout.Width(70f), GUILayout.Height(34f)))
                    onPick?.Invoke(job);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private void DrawRelicPick()
        {
            GUILayout.Label("开局遗物：三选一", SoupUITheme.BoldLabel);
            GUILayout.Label("选择一件初始遗物：", SoupUITheme.Label);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            if (_relicCandidates.Count == 0)
            {
                GUILayout.Label("（无可用遗物，直接开始）", SoupUITheme.Label);
                if (GUILayout.Button("开始", GUILayout.Width(120f), GUILayout.Height(36f)))
                    FinishSetup();
            }
            else
            {
                for (int i = 0; i < _relicCandidates.Count; i++)
                {
                    var relic = _relicCandidates[i];
                    if (relic == null) continue;
                    GUILayout.BeginHorizontal("box");
                    GUILayout.Label(relic.DisplayName, BoldLabel(), GUILayout.Width(140f));
                    GUILayout.Label(relic.Description, GUILayout.MinWidth(280f));
                    if (GUILayout.Button("携带", SoupUITheme.Button, GUILayout.Width(70f), GUILayout.Height(34f)))
                    {
                        RelicManager.Instance?.Acquire(relic);
                        FinishSetup();
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
            DrawBackToTitleButton();
        }

        /// <summary>开局选择页底部的返回出口：放弃本次开局，回主菜单。</summary>
        private void DrawBackToTitleButton()
        {
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("返回主菜单", SoupUITheme.Button, GUILayout.Height(32f), GUILayout.Width(140f)))
            {
                _scroll = Vector2.zero;
                _phase = SetupPhase.Title;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void NextPhase(SetupPhase next)
        {
            if ((next == SetupPhase.ProcessPick && _processCandidates.Count == 0)
                || (next == SetupPhase.RelicPick && _relicCandidates.Count == 0))
            {
                FinishSetup();
                return;
            }

            _phase = next;
            _scroll = Vector2.zero;
        }

        private void FinishSetup(bool commitLevelSnapshot = true)
        {
            if (commitLevelSnapshot)
                LevelManager.Instance?.CommitLevelStartSnapshot();
            _phase = SetupPhase.Title;
            _sessionLaunched = true;
        }

        private static void PickRandom(List<JobItem> source, int count, List<JobItem> target)
        {
            target.Clear();
            if (source == null || source.Count == 0) return;

            var pool = new List<JobItem>(source);
            while (pool.Count > 0 && target.Count < count)
            {
                int index = Random.Range(0, pool.Count);
                if (pool[index] != null)
                    target.Add(pool[index]);
                pool.RemoveAt(index);
            }
        }

        private GUIStyle BoldLabel()
        {
            if (_boldLabel == null)
                _boldLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            return _boldLabel;
        }

        private GUIStyle TitleLabel()
        {
            if (_titleLabel == null)
            {
                _titleLabel = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 34,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                SoupUITheme.ApplyTextColor(_titleLabel, SoupUITheme.TextDark);
            }

            return _titleLabel;
        }

        private GUIStyle SubTitleLabel()
        {
            if (_subTitleLabel == null)
            {
                _subTitleLabel = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter
                };
                SoupUITheme.ApplyTextColor(_subTitleLabel, new Color(0.92f, 0.88f, 0.80f));
            }

            return _subTitleLabel;
        }

        private GUIStyle FooterLabel()
        {
            if (_footerLabel == null)
            {
                _footerLabel = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.LowerCenter,
                    fontSize = 11
                };
                SoupUITheme.ApplyTextColor(_footerLabel, new Color(0.85f, 0.82f, 0.75f, 0.7f));
            }

            return _footerLabel;
        }
    }
}
