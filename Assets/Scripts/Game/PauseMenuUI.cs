using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// In-game ESC menu: continue / save-load (3 slots) / back to main menu / quit.
    /// Destructive actions (load / to main menu / quit) confirm unsaved progress first.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public class PauseMenuUI : MonoBehaviour
    {
        private enum Page
        {
            Closed,
            Main,
            Save,
            Load,
            Confirm
        }

        /// <summary>挂起的破坏性操作（确认或保存完成后执行）。</summary>
        private enum PendingAction
        {
            None,
            LoadSlot,
            ToMainMenu,
            Quit
        }

        private Page _page = Page.Closed;
        private PendingAction _pendingAction = PendingAction.None;
        private int _pendingLoadSlot;
        private string _message = string.Empty;
        private GUIStyle _boldLabel;
        private GUIStyle _titleLabel;

        public static PauseMenuUI Instance { get; private set; }

        public static bool IsOpen => Instance != null && Instance._page != Page.Closed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(PauseMenuUI));
            Instance = go.AddComponent<PauseMenuUI>();
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
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // 主菜单打开时不响应（未开局）。
            if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsOpen) return;

            if (_page == Page.Closed)
                Open(Page.Main);
            else
                Close();
        }

        private void Open(Page page)
        {
            _page = page;
            _message = string.Empty;
        }

        private void Close()
        {
            _page = Page.Closed;
            _pendingAction = PendingAction.None;
            _message = string.Empty;
        }

        private void OnGUI()
        {
            if (_page == Page.Closed) return;

            // IMGUI 层级：越小越上层；暂停菜单必须处于最上层。
            GUI.depth = 0;

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = Mathf.Min(560f, Screen.width - 48f);
            float height = Mathf.Min(600f, Screen.height - 48f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(area, SoupUITheme.PanelBox);

            switch (_page)
            {
                case Page.Main: DrawMain(); break;
                case Page.Save:
                case Page.Load: DrawSlots(saveMode: _page == Page.Save); break;
                case Page.Confirm: DrawConfirm(); break;
            }

            GUILayout.EndArea();
        }

        private void DrawMain()
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("暂停", TitleLabel());

            if (GUILayout.Button("继续游戏", SoupUITheme.PrimaryButton, GUILayout.Height(40f)))
                Close();

            if (GUILayout.Button("保存游戏", SoupUITheme.PrimaryButton, GUILayout.Height(40f)))
            {
                _pendingAction = PendingAction.None;
                Open(Page.Save);
            }

            GUI.enabled = GameSaveService.FindLatestSlot() > 0;
            if (GUILayout.Button("读取游戏", SoupUITheme.PrimaryButton, GUILayout.Height(40f)))
                Open(Page.Load);
            GUI.enabled = true;

            if (GUILayout.Button("返回主菜单", SoupUITheme.PrimaryButton, GUILayout.Height(40f)))
                ConfirmBefore(PendingAction.ToMainMenu);

            if (GUILayout.Button("退出游戏", SoupUITheme.PrimaryButton, GUILayout.Height(40f)))
                ConfirmBefore(PendingAction.Quit);

            if (!string.IsNullOrEmpty(_message))
                GUILayout.Label(_message, SoupUITheme.Label);

            GUILayout.Label("[Esc] 关闭菜单", SoupUITheme.Label);
            GUILayout.FlexibleSpace();
        }

        // -------------------------------------------------------------- confirm

        /// <summary>Destructive action: confirm unsaved progress before executing.</summary>
        private void ConfirmBefore(PendingAction action, int loadSlot = 0)
        {
            _pendingAction = action;
            _pendingLoadSlot = loadSlot;
            Open(Page.Confirm);
        }

        private void DrawConfirm()
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label(PendingActionTitle(), TitleLabel());
            GUILayout.Label("当前进度尚未保存，是否先保存？", SoupUITheme.Label);

            if (GUILayout.Button("保存当前进度", SoupUITheme.Button, GUILayout.Height(40f)))
                Open(Page.Save); // 选完槽位保存后自动执行挂起操作

            if (GUILayout.Button("不保存，" + PendingActionLabel(), SoupUITheme.Button, GUILayout.Height(40f)))
                ExecutePending();

            if (GUILayout.Button("取消", SoupUITheme.Button, GUILayout.Height(40f)))
                BackFromConfirm();

            GUILayout.FlexibleSpace();
        }

        private string PendingActionTitle()
        {
            switch (_pendingAction)
            {
                case PendingAction.LoadSlot: return "读取存档";
                case PendingAction.ToMainMenu: return "返回主菜单";
                case PendingAction.Quit: return "退出游戏";
                default: return "确认";
            }
        }

        private string PendingActionLabel()
        {
            switch (_pendingAction)
            {
                case PendingAction.LoadSlot: return "直接读取";
                case PendingAction.ToMainMenu: return "直接返回";
                case PendingAction.Quit: return "直接退出";
                default: return "继续";
            }
        }

        /// <summary>Confirm → Save 槽位页的返回目标：回确认页而非主菜单页。</summary>
        private void BackFromConfirm()
        {
            _pendingAction = PendingAction.None;
            Open(Page.Main);
        }

        private void ExecutePending()
        {
            var action = _pendingAction;
            _pendingAction = PendingAction.None;

            switch (action)
            {
                case PendingAction.LoadSlot:
                {
                    var data = GameSaveService.LoadFromDisk(_pendingLoadSlot);
                    if (data != null && GameSaveService.StartRunFromSave(data))
                    {
                        Close();
                        return;
                    }

                    _message = $"读取失败（槽位 {_pendingLoadSlot}）";
                    Open(Page.Load);
                    break;
                }

                case PendingAction.ToMainMenu:
                    Close();
                    MainMenuUI.Reopen();
                    break;

                case PendingAction.Quit:
                    GameExit.Quit();
                    break;
            }
        }

        // ----------------------------------------------------------------- slots

        private void DrawSlots(bool saveMode)
        {
            bool savingForPending = saveMode && _pendingAction != PendingAction.None;
            GUILayout.Label(
                savingForPending
                    ? $"选择保存槽位（保存后{PendingActionLabel()}）"
                    : saveMode ? "保存到槽位" : "读取存档",
                TitleLabel());

            for (int slot = 1; slot <= GameSaveService.SlotCount; slot++)
            {
                var info = GameSaveService.GetSlotInfo(slot);
                string summary = info.Exists
                    ? $"{info.LevelDisplayName} · 总分 {info.TotalScore} · 回合 {info.TurnIndex}"
                    : "（空）";

                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"槽位 {slot}", BoldLabel(), GUILayout.Width(60f));
                GUILayout.Label(summary, GUILayout.MinWidth(180f));

                if (saveMode)
                {
                    if (GUILayout.Button(info.Exists ? "覆盖保存" : "保存", SoupUITheme.PanelButton, GUILayout.Width(90f), GUILayout.Height(32f)))
                    {
                        var data = GameSaveService.Capture();
                        bool saved = GameSaveService.SaveToDisk(slot, data);
                        _message = saved ? $"已保存到槽位 {slot}" : $"保存失败（槽位 {slot}）";
                        if (saved && _pendingAction != PendingAction.None)
                        {
                            ExecutePending();
                            return;
                        }
                    }

                    if (info.Exists && GUILayout.Button("删除", SoupUITheme.PanelButton, GUILayout.Width(60f), GUILayout.Height(32f)))
                    {
                        GameSaveService.DeleteSlot(slot);
                        _message = $"已删除槽位 {slot}";
                    }
                }
                else
                {
                    GUI.enabled = info.Exists;
                    if (GUILayout.Button("读取", SoupUITheme.PanelButton, GUILayout.Width(90f), GUILayout.Height(32f)))
                        ConfirmBefore(PendingAction.LoadSlot, slot);
                    GUI.enabled = true;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8f);
            if (GUILayout.Button(
                    savingForPending ? "取消（返回确认）" : "返回",
                    SoupUITheme.Button,
                    GUILayout.Height(36f)))
            {
                Open(savingForPending ? Page.Confirm : Page.Main);
            }

            if (!string.IsNullOrEmpty(_message))
                GUILayout.Label(_message, SoupUITheme.Label);
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
                    fontSize = 26,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                SoupUITheme.ApplyTextColor(_titleLabel, SoupUITheme.TextDark);
            }

            return _titleLabel;
        }
    }
}
