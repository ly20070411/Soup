using Soup.Levels;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// 关卡剧情演出：
    /// 1) 开局——锅长立绘 + 屏幕底部对话讲述本关剧情；
    /// 2) 剧情完毕——居中弹窗讲解操作规则，“开始排班”后进入游戏；
    /// 3) 通关——长老立绘 + 底部对话讲述后续剧情，确认后进入关间奖励流程。
    /// 流程门控仍由 LevelManager 的 Briefing/Outro 状态负责，本类只做呈现。
    /// </summary>
    [DefaultExecutionOrder(-44)]
    public class StoryDialogueUI : MonoBehaviour
    {
        private enum Phase
        {
            None,
            IntroDialogue,
            Rules,
            OutroDialogue
        }

        private Phase _phase = Phase.None;
        private GUIStyle _nameLabel;
        private GUIStyle _rulesTitleLabel;

        public static StoryDialogueUI Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(StoryDialogueUI));
            Instance = go.AddComponent<StoryDialogueUI>();
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
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (_phase != Phase.IntroDialogue && _phase != Phase.OutroDialogue) return;
            if (PauseMenuUI.IsOpen) return;
            if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsOpen) return;
            if (!Input.GetKeyDown(KeyCode.Space)
                && !Input.GetKeyDown(KeyCode.Return)
                && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;

            if (_phase == Phase.IntroDialogue)
                _phase = Phase.Rules;
            else
                LevelManager.Instance?.AcknowledgeOutro();
        }

        private void OnGUI()
        {
            if (PauseMenuUI.IsOpen) return;
            if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsOpen) return;

            var levels = LevelManager.Instance;
            var level = levels != null && levels.HasLevels ? levels.Current : null;
            if (level == null)
            {
                _phase = Phase.None;
                return;
            }

            // 与关卡状态机同步：简报未确认 → 开场演出；胜利且 outro 未确认 → 收尾演出。
            if (levels.IsBriefingActive && levels.Outcome == LevelOutcome.InProgress)
            {
                if (_phase != Phase.Rules)
                    _phase = Phase.IntroDialogue;
            }
            else if (levels.IsOutroActive)
            {
                _phase = Phase.OutroDialogue;
            }
            else
            {
                _phase = Phase.None;
                return;
            }

            // 与 F1 面板同层（1）：盖住世界地图（5），让位给各级菜单（0）。
            GUI.depth = 1;

            switch (_phase)
            {
                case Phase.IntroDialogue:
                    DrawDialogue("锅长", ChiefPortrait(), level.StoryIntro, "继续 ▶", () => _phase = Phase.Rules);
                    break;
                case Phase.Rules:
                    DrawRulesCard(levels, level);
                    break;
                case Phase.OutroDialogue:
                    DrawDialogue("长老", ElderPortrait(), level.StoryOutro, "继续 ▶",
                        () => levels.AcknowledgeOutro());
                    break;
            }
        }

        // ------------------------------------------------------------- dialogue

        /// <summary>屏幕底部的对话条：立绘 + 名字 + 正文 + 右下角继续按钮。高度随正文自适应。</summary>
        private void DrawDialogue(
            string npcName,
            Texture2D portrait,
            string text,
            string buttonText,
            System.Action onAdvance)
        {
            DimScreen(0.3f);

            float width = Mathf.Min(920f, Screen.width * 0.86f);

            // 高度按真实换行高度预量：GUILayout 会把无空格的中文整句当成一个
            // 超长“单词”，直接放进固定尺寸容器会把边框撑破（见 SoupUITheme.DrawWrappedText）。
            bool hasPortrait = portrait != null;
            float textWidth = width - 60f - (hasPortrait ? 140f + 12f : 0f);
            string body = string.IsNullOrWhiteSpace(text) ? "……" : text;
            float textHeight = SoupUITheme.Label.CalcHeight(new GUIContent(body), textWidth);
            float columnHeight = 26f + 4f + textHeight + 6f + 36f;
            float height = Mathf.Clamp(
                Mathf.Max(hasPortrait ? 206f : 132f, columnHeight + 20f),
                180f,
                Screen.height * 0.55f);

            var box = new Rect(
                (Screen.width - width) * 0.5f,
                Screen.height - height - 36f,
                width,
                height);

            GUILayout.BeginArea(box, SoupUITheme.PanelBox);
            GUILayout.BeginHorizontal();

            if (hasPortrait)
            {
                GUILayout.Box(portrait, GUIStyle.none, GUILayout.Width(140f), GUILayout.Height(180f));
                GUILayout.Space(12f);
            }

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(npcName, NameLabel());
            GUILayout.Space(4f);
            SoupUITheme.DrawWrappedText(body, SoupUITheme.Label, textWidth);
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(buttonText, SoupUITheme.PrimaryButton, GUILayout.Width(140f), GUILayout.Height(36f)))
                onAdvance?.Invoke();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>剧情之后的操作规则卡：目标 + 玩法 + 按键，确认后开始排班。</summary>
        private void DrawRulesCard(LevelManager levels, LevelItem level)
        {
            DimScreen(0.5f);

            float width = Mathf.Min(660f, Screen.width - 64f);
            float height = Mathf.Min(560f, Screen.height - 96f);
            var area = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(area, SoupUITheme.PanelBox);

            float contentWidth = width - 60f;
            GUILayout.Label($"关卡简报 · {level.DisplayName}", RulesTitleLabel());
            GUILayout.Space(8f);
            GUILayout.Label($"目标：{level.MaxTurns} 回合内累计 {level.TargetScore} 分", SoupUITheme.Label);
            SoupUITheme.DrawWrappedText(level.Description, SoupUITheme.Label, contentWidth);
            if (!string.IsNullOrWhiteSpace(level.SecretGoal))
                SoupUITheme.DrawWrappedText(level.SecretGoal, SoupUITheme.Label, contentWidth);

            GUILayout.Space(12f);
            GUILayout.Label("操作规则", SoupUITheme.BoldLabel);
            SoupUITheme.DrawWrappedText(
                "采集岗产出原料，处理岗把原料加工为处理食材，烹饪岗消耗处理食材换取分数。\n" +
                "热辣 / 寒冷 / 鲜味即时生效并乘算本回合得分；酸涩保留到关底自动结算换分。\n" +
                "在左侧世界地图用 +/- 分配员工：[F1] 操控面板，[Esc] 暂停菜单。\n" +
                "“下一回合”推进生产；“撤回”可以回退上一回合。",
                SoupUITheme.Label,
                contentWidth);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("开始排班", SoupUITheme.PrimaryButton, GUILayout.Width(180f), GUILayout.Height(42f)))
            {
                _phase = Phase.None;
                levels.AcknowledgeBriefing();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static void DimScreen(float alpha)
        {
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // -------------------------------------------------------------- assets

        private static Texture2D ChiefPortrait() => SoupUITheme.GetGeneratedTexture("character_pot_chief");

        private static Texture2D ElderPortrait() => SoupUITheme.GetGeneratedTexture("character_elder");

        // --------------------------------------------------------------- style

        private GUIStyle NameLabel()
        {
            if (_nameLabel == null)
            {
                _nameLabel = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
                SoupUITheme.ApplyTextColor(_nameLabel, SoupUITheme.TextDark);
            }

            return _nameLabel;
        }

        private GUIStyle RulesTitleLabel()
        {
            if (_rulesTitleLabel == null)
            {
                _rulesTitleLabel = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                SoupUITheme.ApplyTextColor(_rulesTitleLabel, SoupUITheme.TextDark);
            }

            return _rulesTitleLabel;
        }
    }
}
