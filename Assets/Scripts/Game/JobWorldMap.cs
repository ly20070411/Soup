using Soup.Events;
using Soup.Levels;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Left-side slim panel: resources / turn controls / level & event flow.
    /// 岗位分配已移至底部生产条（ProductionBar），本面板不再包含岗位列表。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class JobWorldMap : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad = true;

        private GUIStyle _boldLabel;

        public static JobWorldMap Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(JobWorldMap));
            Instance = go.AddComponent<JobWorldMap>();
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
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

        /// <summary>External hint that assignment state changed (IMGUI repaints every frame anyway).</summary>
        public void RefreshLabels()
        {
        }

        private void OnGUI()
        {
            if (PauseMenuUI.IsOpen) return;

            var menu = MainMenuUI.Instance;
            if (menu != null && menu.IsOpen) return;

            // IMGUI 层级：背景 10 → 世界地图 5 → 操控面板 1 → 菜单 0（越小越上层）。
            // depth 每帧只设置一次；本脚本内部先画背景再画面板，顺序天然正确。
            // 主菜单打开时彻底隐藏：主视觉背景已足够，不再把游戏画面调暗叠加。
            GUI.depth = 5;
            DrawLevelBackground();
            DrawPanels();
        }

        private static void DrawLevelBackground()
        {
            var level = LevelManager.Instance?.Current;
            if (level == null || level.Background == null) return;
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                level.Background.texture,
                ScaleMode.ScaleAndCrop);
        }

        private void DrawPanels()
        {
            // 内容密集的列表栏保持默认 box：九宫格木框的切边会占掉约 140px 列宽。
            // 高度为底部生产条（ProductionBar）避让。
            float width = 460f;
            float height = Mathf.Max(400f, Screen.height - ProductionBar.ReservedHeight - 24f);
            var area = new Rect(8f, 8f, width, height);
            GUILayout.BeginArea(area, "box");

            DrawHeader();
            DrawLevelGate();
            DrawEventPopup();

            GUILayout.EndArea();
        }

        private void DrawHeader()
        {
            var store = ResourceStore.Instance;
            var turns = TurnManager.Instance;
            var elves = ElfManager.Instance;

            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            DrawIcon(SoupUITheme.GetGeneratedTexture("prop_world_signpost"), 24f);
            GUILayout.Label("世界地图", BoldLabel());
            GUILayout.EndHorizontal();
            if (store != null)
            {
                GUILayout.BeginHorizontal();
                Label($"柔软 {store.Soft}");
                Label($"强韧 {store.Tough}");
                Label($"坚固 {store.Solid}");
                DrawIconStat(SoupUITheme.GetGeneratedTexture("prop_warehouse"), $"仓库 {store.TotalRaw}/{CapLabel(store.WarehouseCapacity)}");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                Label($"已处理 {store.Processed}");
                DrawIconStat(SoupUITheme.GetGeneratedTexture("prop_magic_cauldron"), $"已烹饪 {store.Cooked}");
                if (turns != null)
                {
                    Label($"回合 {turns.TurnIndex}");
                    Label($"总分 {turns.Score}");
                }

                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                DrawFlavorStat("flavor_spicy", "🔥", "热辣", store.Spicy, 62f);
                DrawFlavorStat("flavor_sour", "🍋", "酸涩", store.Sour, 62f);
                DrawFlavorStat("flavor_cold", "❄", "寒冷", store.Cold, 62f);
                DrawFlavorStat("flavor_magic", "✦", "鲜美", store.Magic, 62f);
                GUILayout.EndHorizontal();
            }

            if (elves != null)
                Label($"精灵 闲{elves.FreeCount}/总{elves.TotalCount}");

            if (turns != null)
                GUILayout.Label(turns.PreviewNextTurnCapacity().ToString());

            var levels = LevelManager.Instance;
            bool canTurn = levels == null || levels.CanAdvanceTurn;
            GUILayout.BeginHorizontal();
            GUI.enabled = canTurn && turns != null;
            if (GUILayout.Button("下一回合", SoupUITheme.PrimaryButton, GUILayout.Height(36f)))
                turns?.NextTurn();
            GUI.enabled = turns != null && turns.CanUndo && (levels == null || levels.Outcome == LevelOutcome.InProgress);
            if (GUILayout.Button("撤回", SoupUITheme.PanelButton, GUILayout.Width(76f), GUILayout.Height(36f)))
                turns?.TryUndoPreviousTurn();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            // 通关条件实时提示：还差多少分 / 剩多少回合。
            if (levels != null && levels.HasLevels && levels.Current != null)
            {
                var level = levels.Current;
                int remainScore = Mathf.Max(0, level.TargetScore - levels.ScoreGainedInLevel);
                int remainTurns = Mathf.Max(0, level.MaxTurns - levels.LevelTurnIndex);
                GUILayout.Label(
                    $"🎯 {level.DisplayName} [{(levels.IsPracticeMode ? "练习" : "战役")}] — 还差 {remainScore} 分 / 剩 {remainTurns} 回合");
                if (store != null && turns != null && store.Sour > 0)
                {
                    int sourUsed = Mathf.Min(store.Sour, turns.StageCooked);
                    int sourScore = FlavorResolver.ScoreSour(sourUsed, turns.StageCooked);
                    GUILayout.Label($"关底酸涩预估：消耗 {sourUsed}，获得约 {sourScore} 分");
                }

                if (GameSettings.TutorialTips && levels.LevelIndex == 0 && levels.LevelTurnIndex < 3)
                {
                    string tip = levels.LevelTurnIndex switch
                    {
                        0 => "锅长提示：先给采集岗分人；只采不处理会堵仓。",
                        1 => "锅长提示：把一部分小精灵调到处理岗，原料才能入锅。",
                        _ => "锅长提示：处理食材准备好后，再给小火分人形成得分。"
                    };
                    GUILayout.Label(tip, BoldLabel());
                }
            }

            GUILayout.EndVertical();
        }

        /// <summary>Between-levels page / retry gate when a level is decided.</summary>
        private void DrawLevelGate()
        {
            var levels = LevelManager.Instance;
            if (levels == null || !levels.HasLevels) return;

            // 开场剧情与规则讲解、通关收尾剧情均由 StoryDialogueUI 演出，
            // 这里只保留关卡结算 / 奖励 / 重试逻辑。

            if (levels.HasActiveClearRewards)
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label(
                    levels.IsRunComplete
                        ? (levels.IsPracticeMode ? "练习完成" : "🎉 三关 DEMO 通关！")
                        : $"关卡完成：{levels.Current?.DisplayName}",
                    BoldLabel());
                GUILayout.Label($"本关得分 {levels.ScoreGainedInLevel} / 目标 {levels.Current?.TargetScore}");
                if (levels.LastSourUsed > 0)
                    GUILayout.Label($"酸涩结算：消耗 {levels.LastSourUsed}，获得 {levels.LastSourScore} 分");

                if (levels.IsOutroActive)
                {
                    GUILayout.Label("长老正在讲述本关的后续剧情……（见屏幕下方对话）");
                }
                else if (levels.IsPracticeMode)
                {
                    if (GUILayout.Button("返回主菜单", SoupUITheme.Button, GUILayout.Height(34f)))
                        MainMenuUI.Reopen();
                }
                else if (!levels.RewardClaimed)
                {
                    GUILayout.Label("关间奖励 · 三选一", BoldLabel());
                    var offers = levels.RewardOffers;
                    for (int i = 0; i < offers.Count; i++)
                    {
                        int index = i;
                        var offer = offers[i];
                        GUILayout.BeginHorizontal("box");
                        GUILayout.BeginVertical();
                        GUILayout.Label(offer.Title, BoldLabel());
                        GUILayout.Label(offer.Description);
                        GUILayout.EndVertical();
                        if (GUILayout.Button("选择", SoupUITheme.Button, GUILayout.Width(64f), GUILayout.Height(44f)))
                            levels.TryClaimReward(index);
                        GUILayout.EndHorizontal();
                    }
                }
                else if (EventManager.Instance?.HasPendingEventSequence == true)
                {
                    GUILayout.Label("请先处理下方关卡事件。事件结束后即可继续。", BoldLabel());
                }
                else if (levels.IsCampaignComplete)
                {
                    GUILayout.Label("饥肠王揭开了通往世界之釜的裂缝。公开 DEMO 至此结束。", BoldLabel());
                    if (GUILayout.Button("返回主菜单", SoupUITheme.Button, GUILayout.Height(34f)))
                        MainMenuUI.Reopen();
                }
                else
                {
                    GUI.enabled = levels.CanAdvanceToNextLevel;
                    if (GUILayout.Button("进入下一关", SoupUITheme.Button, GUILayout.Height(34f))
                        && levels.AdvanceToNextLevel())
                        RefreshLabels();
                    GUI.enabled = true;
                }

                GUILayout.EndVertical();
            }
            else if (levels.Outcome == LevelOutcome.Lost)
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label($"关卡失败：{levels.Current?.DisplayName}", BoldLabel());
                GUILayout.Label($"得分 {levels.ScoreGainedInLevel} / 目标 {levels.Current?.TargetScore}");
                GUILayout.Label(DescribeFailureBottleneck());
                if (GUILayout.Button("从本关起点重试", SoupUITheme.Button, GUILayout.Height(34f)) && levels.RetryCurrentLevel())
                    RefreshLabels();
                if (GUILayout.Button("结束本局", SoupUITheme.PanelButton, GUILayout.Height(30f)))
                    MainMenuUI.Reopen();
                GUILayout.EndVertical();
            }
        }

        private static string DescribeFailureBottleneck()
        {
            var store = ResourceStore.Instance;
            var elves = ElfManager.Instance;
            if (store == null) return "未能在回合限制内完成订单。";
            if (elves != null && elves.TotalCount <= 0)
                return "瓶颈：没有可用小精灵，生产链已经停摆。";
            if (store.TotalRaw > 0 && store.Processed == 0)
                return "瓶颈：原料积压，但处理产能不足。";
            if (store.Processed > 0)
                return "瓶颈：仍有处理食材未烹饪，请提高火力岗位劳动力。";
            if (store.Sour > 0)
                return "瓶颈：酸涩储备超过本关熟食量，部分酸涩无法换分。";
            return "瓶颈：采集、处理与烹饪的劳动力比例没有形成连续流水线。";
        }

        /// <summary>Pending event card: description + options that adjust chief incentive.</summary>
        private void DrawEventPopup()
        {
            var events = EventManager.Instance;
            if (events == null || !events.HasPendingEvent) return;

            var pending = events.PendingEvent;
            GUILayout.BeginVertical("box");
            GUILayout.Label($"突发事件：{pending.DisplayName}", BoldLabel());
            SoupUITheme.DrawWrappedText(pending.Description, GUI.skin.label, 430f);
            var options = pending.Options;
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                if (GUILayout.Button(options[i].Text, SoupUITheme.Button, GUILayout.Height(30f)))
                    events.ResolvePendingOption(index);
            }

            GUILayout.EndVertical();
        }

        private static void Label(string text) => GUILayout.Label(text, GUILayout.MinWidth(80f));

        /// <summary>画一个固定尺寸的正方形图标（素材缺失时跳过，不占位）。</summary>
        private static void DrawIcon(Texture2D icon, float size)
        {
            if (icon == null) return;
            GUILayout.Box(icon, GUIStyle.none, GUILayout.Width(size), GUILayout.Height(size));
        }

        /// <summary>图标 + 数值统计；无素材时退回纯文本。</summary>
        private static void DrawIconStat(Texture2D icon, string text, float minWidth = 80f)
        {
            if (icon == null)
            {
                Label(text);
                return;
            }

            GUILayout.Box(icon, GUIStyle.none, GUILayout.Width(20f), GUILayout.Height(20f));
            GUILayout.Label(text, GUILayout.MinWidth(minWidth));
        }

        /// <summary>风味统计（素材清单建议 HUD 显示 32–64 px，此处行高取 22）。</summary>
        private static void DrawFlavorStat(string iconAsset, string emoji, string name, int value, float minWidth)
        {
            var icon = SoupUITheme.GetGeneratedTexture(iconAsset);
            if (icon == null)
            {
                GUILayout.Label($"{emoji}{name} {value}", GUILayout.MinWidth(minWidth));
                return;
            }

            GUILayout.Box(icon, GUIStyle.none, GUILayout.Width(22f), GUILayout.Height(22f));
            GUILayout.Label($"{name} {value}", GUILayout.MinWidth(minWidth));
        }

        private static string CapLabel(int capacity) => capacity <= 0 ? "∞" : capacity.ToString();

        private GUIStyle BoldLabel()
        {
            if (_boldLabel == null)
                _boldLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            return _boldLabel;
        }
    }
}
