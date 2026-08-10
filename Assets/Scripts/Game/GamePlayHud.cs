using System.Collections.Generic;
using Soup.Jobs;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Prototype play HUD: resource panel, job assignment, next-turn button.
    /// </summary>
    public class GamePlayHud : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        private Vector2 _jobScroll;
        private string _lastResult = string.Empty;
        private readonly List<JobItem> _jobsCache = new List<JobItem>();
        private GUIStyle _boldLabel;

        private void OnEnable()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.TurnResolved += OnTurnResolved;
        }

        private void OnDisable()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.TurnResolved -= OnTurnResolved;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;
        }

        private void OnTurnResolved(TurnResult result)
        {
            _lastResult = result != null ? result.ToString() : string.Empty;
        }

        private void OnGUI()
        {
            if (!visible) return;

            const float pad = 10f;
            float width = Mathf.Min(920f, Screen.width - pad * 2f);
            GUILayout.BeginArea(new Rect(pad, pad, width, Screen.height - pad * 2f));

            DrawResourceBar();
            GUILayout.Space(8f);
            DrawControls();
            GUILayout.Space(8f);
            DrawJobPanel();
            GUILayout.Space(8f);
            DrawLastResult();

            GUILayout.EndArea();
        }

        private void DrawResourceBar()
        {
            var store = ResourceStore.Instance;
            var elves = ElfManager.Instance;
            var turns = TurnManager.Instance;

            GUILayout.BeginVertical("box");
            GUILayout.Label("资源面板");

            if (store == null)
            {
                GUILayout.Label("ResourceStore 未就绪");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            Stat("柔软", store.Soft);
            Stat("强韧", store.Tough);
            Stat("坚固", store.Solid);
            Stat($"仓库 {store.TotalRaw}/{CapLabel(store.WarehouseCapacity)}", -1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Stat("热辣", store.Spicy);
            Stat("酸涩", store.Sour);
            Stat("寒冷", store.Cold);
            Stat("鲜美", store.Magic);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Stat("已处理", store.Processed);
            Stat("已烹饪", store.Cooked);
            if (turns != null)
            {
                Stat("回合", turns.TurnIndex);
                Stat("总分", turns.Score);
            }
            if (elves != null)
                Stat($"精灵 闲{elves.FreeCount}/总{elves.TotalCount}", -1);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("下一回合", GUILayout.Height(36f), GUILayout.Width(120f)))
            {
                if (TurnManager.Instance != null)
                    TurnManager.Instance.NextTurn();
            }

            if (GUILayout.Button("重置局", GUILayout.Height(36f), GUILayout.Width(100f)))
            {
                TurnManager.Instance?.ResetRun();
                _lastResult = "已重置";
            }

            if (GUILayout.Button("清空分配", GUILayout.Height(36f), GUILayout.Width(100f)))
                ElfManager.Instance?.ClearAssignments();

            GUILayout.FlexibleSpace();
            GUILayout.Label($"[{toggleKey}] 显隐");
            GUILayout.EndHorizontal();
        }

        private void DrawJobPanel()
        {
            var elves = ElfManager.Instance;
            var jobs = JobManager.Instance;
            if (elves == null || jobs == null)
            {
                GUILayout.Label("ElfManager / JobManager 未就绪");
                return;
            }

            RefreshJobsCache(jobs);

            GUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));
            GUILayout.Label("岗位分配");
            _jobScroll = GUILayout.BeginScrollView(_jobScroll);

            DrawJobGroup("采集", JobType.Gather, elves);
            DrawJobGroup("处理", JobType.Process, elves);

            var activeCook = elves.GetActiveCookJob();
            string cookTitle = activeCook != null
                ? $"烹饪（当前：{activeCook.DisplayName}，三选一）"
                : "烹饪（小火/中火/大火 三选一）";
            DrawJobGroup(cookTitle, JobType.Cook, elves);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawJobGroup(string title, JobType type, ElfManager elves)
        {
            GUILayout.Space(4f);
            GUILayout.Label(title, BoldLabel());

            for (int i = 0; i < _jobsCache.Count; i++)
            {
                var job = _jobsCache[i];
                if (job == null || job.JobType != type) continue;

                int assigned = elves.GetAssigned(job);
                int remain = elves.GetRemainingCapacity(job);
                string cap = job.HasWorkerLimit ? job.MaxWorkers.ToString() : "∞";

                bool isCook = job.JobType == JobType.Cook;
                var activeCook = isCook ? elves.GetActiveCookJob() : null;
                bool otherCookActive = isCook && activeCook != null && !ReferenceEquals(activeCook, job);

                GUILayout.BeginHorizontal();
                string mark = otherCookActive ? "·" : (isCook && assigned > 0 ? "✓" : " ");
                GUILayout.Label($"{mark} {job.DisplayName}  {assigned}/{cap}", GUILayout.Width(230f));
                GUILayout.Label(job.GetEffectSummary(), GUILayout.MinWidth(240f));

                GUI.enabled = assigned > 0;
                if (GUILayout.Button("-", GUILayout.Width(28f)))
                    elves.TryUnassign(job, 1);
                // Other cook selected: + switches the whole group over.
                GUI.enabled = remain > 0 && (elves.FreeCount > 0 || otherCookActive);
                if (GUILayout.Button(otherCookActive ? "选" : "+", GUILayout.Width(28f)))
                    elves.TryAssign(job, 1);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawLastResult()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("上回合结算");
            GUILayout.Label(string.IsNullOrEmpty(_lastResult) ? "—" : _lastResult);
            GUILayout.EndVertical();
        }

        private void RefreshJobsCache(JobManager jobs)
        {
            _jobsCache.Clear();
            var all = jobs.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null)
                    _jobsCache.Add(all[i]);
            }

            _jobsCache.Sort((a, b) =>
            {
                int typeCmp = a.JobType.CompareTo(b.JobType);
                if (typeCmp != 0) return typeCmp;
                return string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });
        }

        private static void Stat(string label, int value)
        {
            string text = value < 0 ? label : $"{label} {value}";
            GUILayout.Label(text, GUILayout.MinWidth(90f));
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
