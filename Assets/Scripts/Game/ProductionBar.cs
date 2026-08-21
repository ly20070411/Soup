using Soup.Employees;
using Soup.Jobs;
using Soup.Levels;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// 底部生产条：
    /// 上层员工栏——展示已拥有员工（头像 + 闲/总），点选“分配单位”；
    /// 下层流水线——采集 ➜ 处理 ➜ 烹饪 ➜ 通关目标 四段节点：
    /// 各阶段节点用 +/− 分配所选类型员工（烹饪火力可同时组合，引擎按各火力
    /// 劳动力分别结算）；每阶段下方展示该阶段产物；最右的目标节点以进度条
    /// 直观呈现本关得分进度与剩余回合。
    /// 取代世界地图左侧的岗位列表（F1 操控面板中仍保留完整列表供调试）。
    /// </summary>
    [DefaultExecutionOrder(-48)]
    public class ProductionBar : MonoBehaviour
    {
        /// <summary>底部条预留高度（员工栏 + 流水线），供左侧面板等 UI 避让。</summary>
        public const float ReservedHeight = 320f;

        private string _selectedTypeId = EmployeeManager.ElfId;
        private GUIStyle _stageLabel;
        private GUIStyle _arrowLabel;

        public static ProductionBar Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(ProductionBar));
            Instance = go.AddComponent<ProductionBar>();
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

        private void OnGUI()
        {
            if (PauseMenuUI.IsOpen) return;
            if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsOpen) return;

            var elves = ElfManager.Instance;
            var em = EmployeeManager.Instance;
            var jobs = JobManager.Instance;
            if (elves == null || em == null || jobs == null) return;

            // 独立层级 4：在世界地图与关卡背景（5）之上、F1/对话/菜单（1/0）之下。
            // 不与世界地图共用 5——相同 depth 的脚本间排序不保证按执行顺序，
            // 全屏关卡背景可能反过来盖住生产条。
            GUI.depth = 4;

            var assignType = ResolveAssignType(em);
            var area = new Rect(
                8f,
                Screen.height - ReservedHeight,
                Screen.width - 16f,
                ReservedHeight - 8f);
            GUILayout.BeginArea(area);

            DrawEmployeeBar(em, assignType);
            GUILayout.Space(6f);
            DrawPipeline(elves, em, jobs, assignType);

            GUILayout.EndArea();
        }

        private EmployeeItem ResolveAssignType(EmployeeManager em)
        {
            var type = em.GetById(_selectedTypeId);
            if (type == null || !type.CanPlayerAssign)
                type = em.ElfType;
            return type != null ? type : em.GetById(EmployeeManager.ElfId);
        }

        // ------------------------------------------------------------ employee bar

        /// <summary>员工栏：已拥有的员工类型，头像 + 闲/总；点击选择分配单位。</summary>
        private void DrawEmployeeBar(EmployeeManager em, EmployeeItem assignType)
        {
            GUILayout.BeginHorizontal("box", GUILayout.Height(56f));
            GUILayout.Label("分配单位", StageLabel(), GUILayout.Width(64f));

            for (int i = 0; i < em.All.Count; i++)
            {
                var type = em.All[i];
                if (type == null) continue;

                int owned = em.GetOwned(type);
                if (owned <= 0 && !ReferenceEquals(type, em.ElfType)) continue;

                bool selected = ReferenceEquals(type, assignType);
                string label = $"{type.DisplayName} 闲{em.GetFree(type)}/总{owned}";
                if (type.HasLockedJob) label += "（锁岗）";

                // 锁定岗位的类型（蘑菇人）不可手动分配，只作展示。
                GUI.enabled = type.CanPlayerAssign;
                var previous = GUI.backgroundColor;
                if (selected)
                    GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);

                GUILayout.BeginHorizontal();
                if (type.Icon != null)
                    GUILayout.Box(type.Icon.texture, GUIStyle.none, GUILayout.Width(36f), GUILayout.Height(36f));

                if (GUILayout.Button(label, SoupUITheme.Button, GUILayout.Height(40f), GUILayout.MinWidth(130f))
                    && type.CanPlayerAssign)
                {
                    _selectedTypeId = type.Id;
                }

                GUILayout.EndHorizontal();
                GUI.backgroundColor = previous;
                GUI.enabled = true;
                GUILayout.Space(8f);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- pipeline

        /// <summary>采集 ➜ 处理 ➜ 烹饪 ➜ 通关目标 四段节点。</summary>
        private void DrawPipeline(ElfManager elves, EmployeeManager em, JobManager jobs, EmployeeItem assignType)
        {
            GUILayout.BeginHorizontal("box", GUILayout.ExpandHeight(true));

            DrawStageColumn("采集", JobType.Gather, "prop_gather_patch", elves, em, jobs, assignType, 200f);
            DrawArrow();
            DrawStageColumn("处理", JobType.Process, "prop_processing_table", elves, em, jobs, assignType, 230f);
            DrawArrow();
            DrawStageColumn("烹饪", JobType.Cook, "prop_cooking_stove", elves, em, jobs, assignType, 200f);
            DrawArrow();
            DrawGoalColumn();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawStageColumn(
            string title,
            JobType type,
            string iconAsset,
            ElfManager elves,
            EmployeeManager em,
            JobManager jobs,
            EmployeeItem assignType,
            float minWidth)
        {
            GUILayout.BeginVertical(GUILayout.MinWidth(minWidth), GUILayout.ExpandWidth(true));

            GUILayout.BeginHorizontal();
            if (SoupUITheme.GetGeneratedTexture(iconAsset) is { } icon)
                GUILayout.Box(icon, GUIStyle.none, GUILayout.Width(20f), GUILayout.Height(20f));
            GUILayout.Label(title, StageLabel());
            GUILayout.EndHorizontal();

            var progression = JobProgressionManager.Instance;
            var modifiers = JobModifierManager.Instance;

            var all = jobs.All;
            for (int i = 0; i < all.Count; i++)
            {
                var job = all[i];
                if (job == null || job.JobType != type) continue;
                if (progression != null && !progression.IsUnlocked(job)) continue;
                if (modifiers != null && modifiers.IsDisabled(job)) continue;

                DrawNode(job, elves, em, assignType);
            }

            DrawStageFooter(type);
            GUILayout.EndVertical();
        }

        /// <summary>
        /// 阶段产物，展示在该阶段所有岗位节点正下方：
        /// 采集 → 三类原料 + 仓库余量；处理 → 已处理；烹饪 → 已烹饪。
        /// </summary>
        private static void DrawStageFooter(JobType type)
        {
            var store = ResourceStore.Instance;
            if (store == null) return;

            GUILayout.FlexibleSpace();
            GUILayout.Space(4f);
            switch (type)
            {
                case JobType.Gather:
                    GUILayout.Label($"产物：柔软 {store.Soft} · 强韧 {store.Tough} · 坚固 {store.Solid}");
                    int cap = store.WarehouseCapacity;
                    bool full = cap > 0 && store.TotalRaw >= cap;
                    var previous = GUI.color;
                    if (full)
                        GUI.color = new Color(1f, 0.45f, 0.4f);
                    GUILayout.Label(
                        $"仓库 {store.TotalRaw}/{(cap > 0 ? cap.ToString() : "∞")}{(full ? "（已满，将溢出丢弃）" : "")}");
                    GUI.color = previous;
                    break;

                case JobType.Process:
                    GUILayout.Label($"产物：已处理 {store.Processed}");
                    break;

                case JobType.Cook:
                    GUILayout.Label($"产物：已烹饪 {store.Cooked}");
                    break;
            }
        }

        /// <summary>
        /// 通关目标节点（烹饪右侧）：本关得分进度条 + 目标分 + 剩余回合。
        /// 无关卡数据时退化为总分展示。
        /// </summary>
        private void DrawGoalColumn()
        {
            GUILayout.BeginVertical(GUILayout.MinWidth(190f), GUILayout.ExpandWidth(true));

            GUILayout.BeginHorizontal();
            GUILayout.Label("🎯 通关目标", StageLabel());
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);

            var levels = LevelManager.Instance;
            var level = levels != null && levels.HasLevels ? levels.Current : null;
            var turns = TurnManager.Instance;

            if (level != null && levels != null)
            {
                int gained = levels.ScoreGainedInLevel;
                int target = Mathf.Max(1, level.TargetScore);
                bool reached = gained >= target;

                GUILayout.Label($"本关得分 {gained} / {target}");
                DrawProgressBar(
                    gained / (float)target,
                    reached ? new Color(0.45f, 0.85f, 0.5f) : new Color(0.85f, 0.65f, 0.25f));

                int remainTurns = Mathf.Max(0, level.MaxTurns - levels.LevelTurnIndex);
                var previous = GUI.color;
                if (remainTurns <= 2)
                    GUI.color = new Color(1f, 0.55f, 0.45f);
                GUILayout.Label($"剩余回合 {remainTurns}");
                GUI.color = previous;

                GUILayout.Label(levels.IsPracticeMode ? "练习模式" : "战役模式");
            }
            else if (turns != null)
            {
                GUILayout.Label($"总分 {turns.Score}");
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        /// <summary>简单双色进度条（暗底 + 填充色块）。</summary>
        private static void DrawProgressBar(float ratio, Color fill)
        {
            var rect = GUILayoutUtility.GetRect(120f, 12f);
            var previous = GUI.color;

            GUI.color = new Color(0.15f, 0.12f, 0.1f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = fill;
            var fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

            GUI.color = previous;
        }

        /// <summary>岗位节点（采集 / 处理 / 烹饪通用）：图标 + 名称 + 占用/容量 + 所选类型 +/−。</summary>
        private void DrawNode(JobItem job, ElfManager elves, EmployeeManager em, EmployeeItem assignType)
        {
            int occupying = elves.GetAssigned(job);
            int capacity = elves.GetJobCapacity(job);
            string cap = capacity == int.MaxValue ? "∞" : capacity.ToString();
            bool working = occupying > 0;

            GUILayout.BeginHorizontal();
            if (job.Icon != null)
                GUILayout.Box(job.Icon.texture, GUIStyle.none, GUILayout.Width(20f), GUILayout.Height(20f));

            GUILayout.Label($"{(working ? "✓ " : "")}{job.DisplayName}", GUILayout.Width(92f));
            GUILayout.Label($"{occupying}/{cap}", GUILayout.Width(48f));

            int assigned = em.GetAssigned(assignType, job);
            GUI.enabled = assigned > 0;
            if (GUILayout.Button("−", GUILayout.Width(26f)))
                em.TryUnassign(assignType, job, 1);

            GUI.enabled = CanAssign(em, assignType, job);
            if (GUILayout.Button("+", GUILayout.Width(26f)))
                em.TryAssign(assignType, job, 1);

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private static bool CanAssign(EmployeeManager em, EmployeeItem type, JobItem job)
        {
            if (em.GetFree(type) <= 0) return false;
            if (type.AllowedJobType != null && type.AllowedJobType != job.JobType) return false;
            if (!type.OccupiesJobSlot) return true;

            int remaining = em.GetRemainingOccupyingCapacity(job);
            return remaining > 0;
        }

        // ------------------------------------------------------------------ misc

        private void DrawArrow()
        {
            GUILayout.Label("➜", ArrowLabel(), GUILayout.Width(30f), GUILayout.ExpandHeight(true));
        }

        private GUIStyle StageLabel()
        {
            if (_stageLabel == null)
                _stageLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            return _stageLabel;
        }

        private GUIStyle ArrowLabel()
        {
            if (_arrowLabel == null)
            {
                _arrowLabel = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            return _arrowLabel;
        }
    }
}
