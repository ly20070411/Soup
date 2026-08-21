using System.Collections.Generic;
using Soup.Jobs;
using Soup.Levels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// 进阶巡视 UI：点已有岗位 → 进阶弹窗；点空位 → 解锁未拥有岗位弹窗。
    /// 解锁与进阶共用本区 1 次机会，确认后回关卡间且该区不可再进。
    /// </summary>
    public sealed class AdvancementVisitUI : MonoBehaviour
    {
        private static readonly JobAdvanceNodeId[] TreeNodeOrder =
        {
            JobAdvanceNodeId.Path1,
            JobAdvanceNodeId.Path2,
            JobAdvanceNodeId.Path1_1,
            JobAdvanceNodeId.Path1_2,
            JobAdvanceNodeId.Path2_1,
            JobAdvanceNodeId.Path2_2
        };

        private GameObject _root;

        private GameObject _advancePopupRoot;
        private RectTransform _advanceBoxRect;
        private Text _advanceTitle;
        private Text _advanceHint;
        private RectTransform _treeHost;
        private readonly Dictionary<JobAdvanceNodeId, Button> _treeButtons =
            new Dictionary<JobAdvanceNodeId, Button>(6);
        private readonly Dictionary<JobAdvanceNodeId, Image> _treeButtonImages =
            new Dictionary<JobAdvanceNodeId, Image>(6);
        private readonly List<Image> _treeLines = new List<Image>(8);
        private GameObject _tooltipRoot;
        private RectTransform _tooltipRect;
        private Text _tooltipText;
        private Button _advanceConfirmButton;
        private Text _advanceConfirmLabel;

        private GameObject _unlockPopupRoot;
        private Text _unlockTitle;
        private Text _unlockHint;
        private Transform _unlockListContent;
        private Button _unlockConfirmButton;
        private Text _unlockConfirmLabel;
        private readonly List<Button> _unlockRowButtons = new List<Button>(12);

        private GameObject _designatePopupRoot;
        private Text _designateTitle;
        private Text _designateHint;
        private Transform _designateListContent;
        private Button _designateConfirmButton;
        private Text _designateConfirmLabel;
        private readonly List<Button> _designateRowButtons = new List<Button>(12);
        private readonly List<JobItem> _designateCandidates = new List<JobItem>(16);
        private JobItem _pendingDesignate;

        private Text _toastText;
        private string _toast = string.Empty;
        private float _toastUntil;

        private JobItem _selectedJob;
        private readonly List<JobAdvanceNodeId> _choices = new List<JobAdvanceNodeId>(2);
        private JobAdvanceNodeId _pendingChoice = JobAdvanceNodeId.None;
        private JobAdvanceNodeId _hoveredNode = JobAdvanceNodeId.None;

        private JobType _unlockJobType = JobType.Gather;
        private readonly List<JobItem> _unlockCandidates = new List<JobItem>(16);
        private JobItem _pendingUnlock;

        private bool _leaving;
        private float _ignoreBackUntil;

        public static AdvancementVisitUI Ensure()
        {
            var existing = FindObjectOfType<AdvancementVisitUI>();
            if (existing != null)
            {
                existing.ActivateVisit();
                return existing;
            }

            var go = new GameObject(nameof(AdvancementVisitUI));
            return go.AddComponent<AdvancementVisitUI>();
        }

        private void Awake()
        {
            Build();
            ActivateVisit();
        }

        private void Update()
        {
            if (_toastText != null)
                _toastText.text = Time.unscaledTime <= _toastUntil ? _toast : string.Empty;
        }

        public void ActivateVisit()
        {
            if (!AdvancementVisit.IsActive)
            {
                if (_root != null)
                    _root.SetActive(false);
                return;
            }

            _leaving = false;
            // 防止关卡间按钮抬起穿透，误点「返回」立刻弹回。
            _ignoreBackUntil = Time.unscaledTime + 0.4f;
            if (_root != null)
                _root.SetActive(true);
            CloseAllPopups();

            var cam = FindObjectOfType<ZoneCameraController>();
            cam?.SnapToZone(AdvancementVisit.Zone);

            var overlay = FindObjectOfType<GameOverlayUI>();
            overlay?.SetPlayHudVisible(false);

            var hud = FindObjectOfType<GamePlayHud>();
            hud?.SetPanelMode(false);

            RefreshStationPads();
        }

        public void TrySelectStation(JobStationMarker marker)
        {
            if (_leaving || !AdvancementVisit.IsActive) return;
            if (marker == null || !marker.IsUnlocked) return;

            var job = marker.Job;
            if (!AdvancementVisit.MatchesZone(job))
            {
                ShowToast("请选择本区已激活的岗位");
                return;
            }

            OpenAdvancePopup(job);
        }

        public void TrySelectGatherJob(JobItem job)
        {
            if (_leaving || !AdvancementVisit.IsActive) return;
            if (job == null || !AdvancementVisit.MatchesZone(job))
            {
                ShowToast("请选择本区已激活的岗位");
                return;
            }

            OpenAdvancePopup(job);
        }

        public void TrySelectEmptySlot(EmptyStationSlot slot)
        {
            if (_leaving || !AdvancementVisit.IsActive || slot == null) return;

            var expected = AdvancementVisit.JobTypeFor(AdvancementVisit.Zone);
            if (slot.JobType != expected)
            {
                ShowToast("请选择本区空位");
                return;
            }

            var map = FindObjectOfType<JobWorldMap>();
            if (map != null && !map.IsEmptySlotFree(slot))
            {
                ShowToast(map.IsGatherSlotDestroyed(slot)
                    ? "该岗位已被摧毁，无法再解锁"
                    : "该位置已有岗位");
                return;
            }

            var session = LevelManager.Instance?.ClearRewards;
            if (session == null || !session.IsActive)
            {
                ShowToast("进阶会话已结束");
                return;
            }

            if (AdvancementVisit.ChargeFor(AdvancementVisit.Zone, session) <= 0)
            {
                ShowToast("本区进阶次数已用完");
                return;
            }

            var candidates = session.GetUnlockCandidates(slot.JobType);
            if (candidates == null || candidates.Count == 0)
            {
                ShowToast(JobProgressionManager.Instance != null
                          && !JobProgressionManager.Instance.CanUnlockMore(slot.JobType)
                    ? "本区岗位已满，无法再解锁"
                    : "没有可解锁的岗位");
                return;
            }

            OpenUnlockPopup(slot.JobType, candidates);
        }

        private void OpenAdvancePopup(JobItem job)
        {
            CloseUnlockPopup();
            _selectedJob = job;
            _pendingChoice = JobAdvanceNodeId.None;
            if (_advancePopupRoot != null)
                _advancePopupRoot.SetActive(true);
            RefreshAdvancePopupContent();
        }

        private void OpenUnlockPopup(JobType type, List<JobItem> candidates)
        {
            CloseAdvancePopup();
            _unlockJobType = type;
            _pendingUnlock = null;
            _unlockCandidates.Clear();
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i] != null)
                        _unlockCandidates.Add(candidates[i]);
                }
            }

            if (_unlockPopupRoot != null)
                _unlockPopupRoot.SetActive(true);
            RebuildUnlockRows();
            RefreshUnlockPopupContent();
        }

        private void RefreshAdvancePopupContent()
        {
            var job = _selectedJob;
            if (job == null) return;

            var progression = JobProgressionManager.Instance;
            var path = progression != null ? progression.GetAdvancePath(job) : JobAdvanceNodeId.None;
            int depth = JobAdvancePath.Depth(path);
            int max = JobProgressionRules.MaxUpgradesPerJob(job.JobType);
            bool canUpgrade = progression != null && progression.CanUpgrade(job);

            _choices.Clear();
            progression?.GetAvailableAdvanceChoices(job, _choices);

            if (_pendingChoice != JobAdvanceNodeId.None && !_choices.Contains(_pendingChoice))
                _pendingChoice = JobAdvanceNodeId.None;

            if (_advanceTitle != null)
                _advanceTitle.text = job.DisplayName;

            if (_advanceHint != null)
            {
                string current = progression != null
                    ? progression.DescribeCurrentPath(job)
                    : "未进阶";
                _advanceHint.text = canUpgrade
                    ? $"当前路径：{current}（深度 {depth}/{max}）\n点击可选节点进阶；鼠标悬停查看效果。确认后消耗本区次数并返回关卡间。"
                    : $"当前路径：{current}（深度 {depth}/{max}）\n该岗位已满级，或当前没有可进阶选项。";
            }

            RefreshAdvanceTree(job, path);

            bool hasSelection = _pendingChoice != JobAdvanceNodeId.None
                                && progression != null
                                && progression.CanAdvance(job, _pendingChoice);
            bool needsDesignate = hasSelection && NeedsDesignatedTarget(job, _pendingChoice);
            if (_advanceConfirmButton != null)
                _advanceConfirmButton.interactable = hasSelection && !_leaving;
            if (_advanceConfirmLabel != null)
            {
                if (!hasSelection)
                    _advanceConfirmLabel.text = canUpgrade ? "请先选择分支" : "已满级 / 不可进阶";
                else if (needsDesignate)
                    _advanceConfirmLabel.text = $"下一步：选择其它岗位 [{JobAdvancePath.ToLabel(_pendingChoice)}]";
                else
                    _advanceConfirmLabel.text = $"确认进阶 [{JobAdvancePath.ToLabel(_pendingChoice)}]";
            }
        }

        private void RefreshAdvanceTree(JobItem job, JobAdvanceNodeId currentPath)
        {
            var progression = JobProgressionManager.Instance;
            for (int i = 0; i < TreeNodeOrder.Length; i++)
            {
                var nodeId = TreeNodeOrder[i];
                if (!_treeButtons.TryGetValue(nodeId, out var button) || button == null)
                    continue;

                bool taken = JobAdvancePath.HasTaken(currentPath, nodeId);
                job.EnsureAdvanceTreeDefaults();
                var node = job.GetAdvanceNode(nodeId);
                bool isNone = node != null && node.IsNoneAdvanceNode();
                bool selectable = _choices.Contains(nodeId) && !_leaving;
                bool lockedNone = isNone && !taken;
                bool pending = _pendingChoice == nodeId;
                button.interactable = selectable;

                if (_treeButtonImages.TryGetValue(nodeId, out var image) && image != null)
                {
                    if (pending)
                        image.color = new Color(0.75f, 1f, 0.78f, 1f);
                    else if (taken)
                        image.color = new Color(0.45f, 0.78f, 0.55f, 1f);
                    else if (selectable)
                        image.color = Color.white;
                    else if (lockedNone)
                        image.color = new Color(0.32f, 0.33f, 0.36f, 0.65f);
                    else
                        image.color = new Color(0.55f, 0.56f, 0.60f, 0.85f);
                }
            }

            RefreshTreeLines(currentPath);
            if (_hoveredNode != JobAdvanceNodeId.None)
                ShowNodeTooltip(_hoveredNode);
            else
                HideNodeTooltip();
        }

        private void RefreshTreeLines(JobAdvanceNodeId currentPath)
        {
            for (int i = 0; i < _treeLines.Count; i++)
            {
                var line = _treeLines[i];
                if (line == null) continue;
                // Lines are tagged via name: "Line_1_1-1" etc. Active path uses brighter color.
                bool lit = false;
                string name = line.gameObject.name;
                if (name.StartsWith("Line_"))
                {
                    // Line_{parent}_{child}
                    var parts = name.Split('_');
                    if (parts.Length >= 3)
                    {
                        var child = ParseNodeLabel(parts[parts.Length - 1]);
                        lit = JobAdvancePath.HasTaken(currentPath, child);
                    }
                }

                line.color = lit
                    ? new Color(0.55f, 0.85f, 0.65f, 0.95f)
                    : new Color(0.55f, 0.58f, 0.64f, 0.55f);
            }
        }

        private static JobAdvanceNodeId ParseNodeLabel(string label)
        {
            switch (label)
            {
                case "1": return JobAdvanceNodeId.Path1;
                case "2": return JobAdvanceNodeId.Path2;
                case "1-1": return JobAdvanceNodeId.Path1_1;
                case "1-2": return JobAdvanceNodeId.Path1_2;
                case "2-1": return JobAdvanceNodeId.Path2_1;
                case "2-2": return JobAdvanceNodeId.Path2_2;
                default: return JobAdvanceNodeId.None;
            }
        }

        private void OnTreeNodeClicked(JobAdvanceNodeId nodeId)
        {
            if (_leaving || _selectedJob == null) return;
            if (!_choices.Contains(nodeId)) return;
            _pendingChoice = nodeId;
            RefreshAdvancePopupContent();
        }

        private void OnTreeNodeHoverEnter(JobAdvanceNodeId nodeId)
        {
            _hoveredNode = nodeId;
            ShowNodeTooltip(nodeId);
        }

        private void OnTreeNodeHoverExit(JobAdvanceNodeId nodeId)
        {
            if (_hoveredNode == nodeId)
                _hoveredNode = JobAdvanceNodeId.None;
            HideNodeTooltip();
        }

        private void ShowNodeTooltip(JobAdvanceNodeId nodeId)
        {
            if (_tooltipRoot == null || _tooltipText == null || _selectedJob == null)
                return;
            if (!_treeButtons.TryGetValue(nodeId, out var button) || button == null)
                return;

            _selectedJob.EnsureAdvanceTreeDefaults();
            var node = _selectedJob.GetAdvanceNode(nodeId);
            if (node != null && node.IsNoneAdvanceNode())
            {
                _tooltipText.text =
                    $"[{JobAdvancePath.ToLabel(nodeId)}] 无\n无效分支，无法选择（防止空进阶）。";
                _tooltipRoot.SetActive(true);
                LayoutTooltipNear(button.GetComponent<RectTransform>());
                return;
            }

            string title = node != null && !string.IsNullOrWhiteSpace(node.DisplayName)
                ? node.DisplayName.Trim()
                : $"路径 {JobAdvancePath.ToLabel(nodeId)}";
            string effect = node != null && !string.IsNullOrWhiteSpace(node.EffectDescription)
                ? node.EffectDescription.Trim()
                : "（暂无效果说明）";
            string pop = node != null && node.MaxWorkersBonus > 0
                ? $"+{node.MaxWorkersBonus} 人口"
                : "无人口加成";

            _tooltipText.text = $"[{JobAdvancePath.ToLabel(nodeId)}] {title}\n{pop}\n{effect}";
            _tooltipRoot.SetActive(true);
            LayoutTooltipNear(button.GetComponent<RectTransform>());
        }

        private void HideNodeTooltip()
        {
            if (_tooltipRoot != null)
                _tooltipRoot.SetActive(false);
        }

        private void LayoutTooltipNear(RectTransform nodeRect)
        {
            if (_tooltipRect == null || _advanceBoxRect == null || nodeRect == null) return;

            Canvas.ForceUpdateCanvases();
            float preferred = _tooltipText != null
                ? Mathf.Clamp(_tooltipText.preferredHeight + 28f, 72f, 220f)
                : 120f;
            _tooltipRect.sizeDelta = new Vector2(300f, preferred);

            var box = _advanceBoxRect.rect;
            Vector2 nodeLocal = _advanceBoxRect.InverseTransformPoint(nodeRect.TransformPoint(nodeRect.rect.center));
            float tipW = _tooltipRect.sizeDelta.x;
            float tipH = _tooltipRect.sizeDelta.y;
            const float gap = 14f;
            const float pad = 16f;

            // Prefer above the node; if clipped, place below.
            float x = nodeLocal.x;
            float y = nodeLocal.y + nodeRect.rect.height * 0.5f + gap + tipH * 0.5f;
            float topLimit = box.yMax - pad - tipH * 0.5f;
            float bottomLimit = box.yMin + pad + tipH * 0.5f;
            if (y > topLimit)
                y = nodeLocal.y - nodeRect.rect.height * 0.5f - gap - tipH * 0.5f;
            y = Mathf.Clamp(y, bottomLimit, topLimit);

            float leftLimit = box.xMin + pad + tipW * 0.5f;
            float rightLimit = box.xMax - pad - tipW * 0.5f;
            x = Mathf.Clamp(x, leftLimit, rightLimit);

            // Keep clear of the hovered button vertically when possible.
            float nodeTop = nodeLocal.y + nodeRect.rect.height * 0.5f;
            float nodeBottom = nodeLocal.y - nodeRect.rect.height * 0.5f;
            float tipBottom = y - tipH * 0.5f;
            float tipTop = y + tipH * 0.5f;
            if (tipBottom < nodeTop + 4f && tipTop > nodeBottom - 4f)
            {
                // Overlap horizontally aligned — push fully above or below.
                float above = nodeTop + gap + tipH * 0.5f;
                float below = nodeBottom - gap - tipH * 0.5f;
                if (above <= topLimit)
                    y = above;
                else if (below >= bottomLimit)
                    y = below;
            }

            y = Mathf.Clamp(y, bottomLimit, topLimit);
            _tooltipRect.anchoredPosition = new Vector2(x, y);
        }

        private void CloseAdvancePopup()
        {
            CloseDesignatePopup();
            HideNodeTooltip();
            _hoveredNode = JobAdvanceNodeId.None;
            _selectedJob = null;
            _pendingChoice = JobAdvanceNodeId.None;
            _choices.Clear();
            if (_advancePopupRoot != null)
                _advancePopupRoot.SetActive(false);
        }

        private void RebuildUnlockRows()
        {
            for (int i = 0; i < _unlockRowButtons.Count; i++)
            {
                if (_unlockRowButtons[i] != null)
                    Destroy(_unlockRowButtons[i].gameObject);
            }

            _unlockRowButtons.Clear();
            if (_unlockListContent == null) return;

            for (int i = 0; i < _unlockCandidates.Count; i++)
            {
                var job = _unlockCandidates[i];
                if (job == null) continue;
                int index = i;
                var button = CreateAnchoredButton(
                    _unlockListContent,
                    $"UnlockRow_{i}",
                    job.DisplayName,
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -8f - i * 76f),
                    new Vector2(600f, 68f),
                    () => OnUnlockRowClicked(index));
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(12f, rect.offsetMin.y);
                rect.offsetMax = new Vector2(-12f, rect.offsetMax.y);
                rect.sizeDelta = new Vector2(0f, 68f);
                rect.anchoredPosition = new Vector2(0f, -8f - i * 76f);

                _unlockRowButtons.Add(button);
            }

            var contentRect = _unlockListContent as RectTransform;
            if (contentRect != null)
                contentRect.sizeDelta = new Vector2(0f, Mathf.Max(120f, _unlockCandidates.Count * 76f + 16f));
        }

        private void RefreshUnlockPopupContent()
        {
            string zone = AdvancementVisit.ZoneDisplayName(AdvancementVisit.Zone);
            if (_unlockTitle != null)
                _unlockTitle.text = $"{zone} · 解锁新岗位";

            if (_unlockHint != null)
            {
                _unlockHint.text = _unlockCandidates.Count > 0
                    ? "从随机至多三个岗位中选一个。未选中的下次仍可能出现；确认后消耗本区次数并返回关卡间。"
                    : "没有可解锁的岗位。";
            }

            for (int i = 0; i < _unlockRowButtons.Count; i++)
            {
                var button = _unlockRowButtons[i];
                if (button == null) continue;
                bool selected = i < _unlockCandidates.Count && _unlockCandidates[i] == _pendingUnlock;
                var image = button.targetGraphic as Image;
                if (image != null)
                    image.color = selected
                        ? new Color(0.75f, 1f, 0.78f, 1f)
                        : Color.white;
            }

            bool canConfirm = _pendingUnlock != null && !_leaving;
            if (_unlockConfirmButton != null)
                _unlockConfirmButton.interactable = canConfirm;
            if (_unlockConfirmLabel != null)
                _unlockConfirmLabel.text = canConfirm
                    ? $"确认解锁 {_pendingUnlock.DisplayName}"
                    : "请先选择岗位";
        }

        private void CloseAllPopups()
        {
            CloseDesignatePopup();
            CloseAdvancePopup();
            CloseUnlockPopup();
        }

        private void CloseUnlockPopup()
        {
            _pendingUnlock = null;
            _unlockCandidates.Clear();
            if (_unlockPopupRoot != null)
                _unlockPopupRoot.SetActive(false);
        }

        private void CloseDesignatePopup()
        {
            _pendingDesignate = null;
            _designateCandidates.Clear();
            if (_designatePopupRoot != null)
                _designatePopupRoot.SetActive(false);
        }

        private void OnUnlockRowClicked(int index)
        {
            if (_leaving) return;
            if (index < 0 || index >= _unlockCandidates.Count) return;
            _pendingUnlock = _unlockCandidates[index];
            RefreshUnlockPopupContent();
        }

        private void OnAdvanceConfirmClicked()
        {
            if (_leaving || _selectedJob == null) return;
            if (_pendingChoice == JobAdvanceNodeId.None)
            {
                ShowToast("请先选择进阶分支");
                return;
            }

            var session = LevelManager.Instance?.ClearRewards;
            if (session == null || !session.IsActive)
            {
                ShowToast("进阶会话已结束");
                return;
            }

            if (NeedsDesignatedTarget(_selectedJob, _pendingChoice))
            {
                CollectOtherGatherCandidates(_selectedJob, _designateCandidates);
                if (_designateCandidates.Count > 0)
                {
                    OpenDesignatePopup(_selectedJob);
                    return;
                }

                // 尚无其它已解锁采集岗：仍可进阶，本岗半边效果生效；之后解锁新岗时会自动补绑。
                ShowToast("当前没有其它采集岗位可指定，将仅对本岗生效", 2.2f);
            }

            CommitAdvance(null);
        }

        private void OpenDesignatePopup(JobItem sourceJob)
        {
            _pendingDesignate = null;
            CollectOtherGatherCandidates(sourceJob, _designateCandidates);

            var progression = JobProgressionManager.Instance;
            if (progression != null)
            {
                var existing = progression.GetDesignatedGatherAuraTarget(sourceJob);
                if (existing != null && _designateCandidates.Contains(existing))
                    _pendingDesignate = existing;
                else if (_designateCandidates.Count > 0)
                    _pendingDesignate = _designateCandidates[0];
            }

            if (_designatePopupRoot != null)
                _designatePopupRoot.SetActive(true);
            RebuildDesignateRows();
            RefreshDesignatePopupContent();
        }

        /// <summary>已解锁的其它采集岗（不含本岗），供双尾蛇 / 灯芯草等「指定目标」选用。</summary>
        private static void CollectOtherGatherCandidates(JobItem sourceJob, List<JobItem> into)
        {
            into.Clear();
            var progression = JobProgressionManager.Instance;
            if (progression == null) return;

            var unlocked = progression.GetUnlocked(JobType.Gather);
            for (int i = 0; i < unlocked.Count; i++)
            {
                var job = unlocked[i];
                if (job == null || ReferenceEquals(job, sourceJob)) continue;
                into.Add(job);
            }

            into.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
        }

        private void RebuildDesignateRows()
        {
            for (int i = 0; i < _designateRowButtons.Count; i++)
            {
                if (_designateRowButtons[i] != null)
                    Destroy(_designateRowButtons[i].gameObject);
            }

            _designateRowButtons.Clear();
            if (_designateListContent == null) return;

            for (int i = 0; i < _designateCandidates.Count; i++)
            {
                var job = _designateCandidates[i];
                if (job == null) continue;
                int index = i;
                var button = CreateAnchoredButton(
                    _designateListContent,
                    $"DesignateRow_{i}",
                    job.DisplayName,
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -8f - i * 76f),
                    new Vector2(600f, 68f),
                    () => OnDesignateRowClicked(index));
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(12f, rect.offsetMin.y);
                rect.offsetMax = new Vector2(-12f, rect.offsetMax.y);
                rect.sizeDelta = new Vector2(0f, 68f);
                rect.anchoredPosition = new Vector2(0f, -8f - i * 76f);
                _designateRowButtons.Add(button);
            }

            var contentRect = _designateListContent as RectTransform;
            if (contentRect != null)
                contentRect.sizeDelta = new Vector2(0f, Mathf.Max(120f, _designateCandidates.Count * 76f + 16f));
        }

        private void RefreshDesignatePopupContent()
        {
            string sourceName = _selectedJob != null ? _selectedJob.DisplayName : "本岗";
            if (_designateTitle != null)
                _designateTitle.text = "选择其它采集岗位";

            if (_designateHint != null)
            {
                _designateHint.text = _designateCandidates.Count > 0
                    ? $"「{sourceName}」路径 {JobAdvancePath.ToLabel(_pendingChoice)}：指定一个其它已解锁的采集岗位，与本岗一同获得加成。"
                    : "当前没有其它已解锁的采集岗位可选。";
            }

            for (int i = 0; i < _designateRowButtons.Count; i++)
            {
                var button = _designateRowButtons[i];
                if (button == null) continue;
                bool selected = i < _designateCandidates.Count && _designateCandidates[i] == _pendingDesignate;
                var image = button.targetGraphic as Image;
                if (image != null)
                    image.color = selected
                        ? new Color(0.75f, 1f, 0.78f, 1f)
                        : Color.white;
            }

            bool canConfirm = _pendingDesignate != null && !_leaving;
            if (_designateConfirmButton != null)
                _designateConfirmButton.interactable = canConfirm;
            if (_designateConfirmLabel != null)
                _designateConfirmLabel.text = canConfirm
                    ? $"确认并进阶 → {_pendingDesignate.DisplayName}"
                    : "请先选择目标岗位";
        }

        private void OnDesignateRowClicked(int index)
        {
            if (_leaving) return;
            if (index < 0 || index >= _designateCandidates.Count) return;
            _pendingDesignate = _designateCandidates[index];
            RefreshDesignatePopupContent();
        }

        private void OnDesignateConfirmClicked()
        {
            if (_leaving || _selectedJob == null || _pendingDesignate == null) return;
            CommitAdvance(_pendingDesignate);
        }

        private void OnDesignateCancelClicked()
        {
            CloseDesignatePopup();
            RefreshAdvancePopupContent();
        }

        private void CommitAdvance(JobItem designatedTarget)
        {
            if (_leaving || _selectedJob == null) return;
            if (_pendingChoice == JobAdvanceNodeId.None)
            {
                ShowToast("请先选择进阶分支");
                return;
            }

            var session = LevelManager.Instance?.ClearRewards;
            if (session == null || !session.IsActive)
            {
                ShowToast("进阶会话已结束");
                return;
            }

            bool needsDesignate = NeedsDesignatedTarget(_selectedJob, _pendingChoice);
            if (needsDesignate && designatedTarget != null)
            {
                var progression = JobProgressionManager.Instance;
                if (progression == null ||
                    !progression.SetDesignatedGatherAuraTarget(_selectedJob, designatedTarget))
                {
                    ShowToast("无法绑定该目标岗位（须为其它已解锁采集岗）");
                    RefreshDesignatePopupContent();
                    return;
                }
            }
            else if (needsDesignate
                     && designatedTarget == null
                     && (_designatePopupRoot != null && _designatePopupRoot.activeSelf))
            {
                ShowToast("请先选择其它采集岗位");
                return;
            }

            if (!session.TryUpgradeJob(_selectedJob, _pendingChoice, out var msg))
            {
                ShowToast(msg);
                if (needsDesignate)
                    RefreshDesignatePopupContent();
                else
                    RefreshAdvancePopupContent();
                return;
            }

            if (needsDesignate && designatedTarget != null)
                msg = $"{msg}；指定目标：{designatedTarget.DisplayName}";

            ShowToast(msg, 2.5f);
            LeaveToInterLevel();
        }

        private static bool NeedsDesignatedTarget(JobItem job, JobAdvanceNodeId choice)
        {
            if (job == null || choice == JobAdvanceNodeId.None) return false;
            job.EnsureAdvanceTreeDefaults();
            var node = job.GetAdvanceNode(choice);
            return node != null && node.NeedsDesignatedGatherTarget;
        }

        private void OnUnlockConfirmClicked()
        {
            if (_leaving || _pendingUnlock == null) return;

            var session = LevelManager.Instance?.ClearRewards;
            if (session == null || !session.IsActive)
            {
                ShowToast("进阶会话已结束");
                return;
            }

            bool ok = _unlockJobType == JobType.Process
                ? session.TryUnlockProcess(_pendingUnlock, out var msg)
                : session.TryUnlockGather(_pendingUnlock, out msg);

            if (!ok)
            {
                ShowToast(msg);
                RefreshUnlockPopupContent();
                return;
            }

            ShowToast(msg, 2f);
            LeaveToInterLevel();
        }

        private void OnBackClicked()
        {
            if (_leaving) return;
            if (Time.unscaledTime < _ignoreBackUntil) return;
            if (_designatePopupRoot != null && _designatePopupRoot.activeSelf)
            {
                OnDesignateCancelClicked();
                return;
            }

            if (_advancePopupRoot != null && _advancePopupRoot.activeSelf)
            {
                CloseAdvancePopup();
                return;
            }

            if (_unlockPopupRoot != null && _unlockPopupRoot.activeSelf)
            {
                CloseUnlockPopup();
                return;
            }

            LeaveToInterLevel();
        }

        private void LeaveToInterLevel()
        {
            if (_leaving) return;
            _leaving = true;
            AdvancementVisit.Clear();
            // 从进阶返回关卡间：无溶解（与进阶入口一致）。
            GameSessionLaunch.GoToInterLevel(useDissolve: false);
        }

        private void ShowToast(string message, float seconds = 2.5f)
        {
            _toast = message ?? string.Empty;
            _toastUntil = Time.unscaledTime + seconds;
        }

        private static void RefreshStationPads()
        {
            var map = FindObjectOfType<JobWorldMap>();
            map?.RebuildStations();
            map?.RefreshLabels();
            map?.ApplyAdvancementVisitPresentation();
        }

        private void Build()
        {
            var canvasGo = new GameObject("AdvancementVisitCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            _root = canvasGo;

            CreateAnchoredButton(canvasGo.transform, "BackBtn", "返回",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(48f, -36f), new Vector2(160f, 56f),
                OnBackClicked);

            _toastText = CreateLabel(canvasGo.transform, "Toast",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 100f), new Vector2(800f, 60f),
                22, FontStyle.Normal, TextAnchor.MiddleCenter);
            _toastText.color = new Color(1f, 0.92f, 0.55f, 1f);

            BuildAdvancePopup(canvasGo.transform);
            BuildUnlockPopup(canvasGo.transform);
            BuildDesignatePopup(canvasGo.transform);
        }

        private void BuildAdvancePopup(Transform parent)
        {
            var panelGo = new GameObject("AdvanceChainPopup");
            panelGo.transform.SetParent(parent, false);
            _advancePopupRoot = panelGo;

            var panelRect = panelGo.AddComponent<RectTransform>();
            StretchFull(panelRect);

            var dim = panelGo.AddComponent<Image>();
            dim.sprite = GameOverlayUI.SharedUiSprite();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            dim.raycastTarget = true;

            var box = CreatePopupBox(panelGo.transform, "Box", new Vector2(920f, 700f));
            _advanceBoxRect = box.GetComponent<RectTransform>();

            _advanceTitle = CreateLabel(box.transform, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -34f), new Vector2(840f, 44f),
                32, FontStyle.Bold, TextAnchor.MiddleCenter);

            _advanceHint = CreateLabel(box.transform, "Hint",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -86f), new Vector2(840f, 64f),
                18, FontStyle.Normal, TextAnchor.UpperCenter);
            _advanceHint.color = new Color(0.85f, 0.88f, 0.92f, 1f);

            var treeGo = new GameObject("TreeHost");
            treeGo.transform.SetParent(box.transform, false);
            _treeHost = treeGo.AddComponent<RectTransform>();
            _treeHost.anchorMin = new Vector2(0.5f, 0.5f);
            _treeHost.anchorMax = new Vector2(0.5f, 0.5f);
            _treeHost.pivot = new Vector2(0.5f, 0.5f);
            _treeHost.anchoredPosition = new Vector2(0f, 18f);
            _treeHost.sizeDelta = new Vector2(760f, 420f);

            // Bottom → top layout positions (local to tree host).
            var positions = new Dictionary<JobAdvanceNodeId, Vector2>
            {
                { JobAdvanceNodeId.Path1, new Vector2(-170f, -20f) },
                { JobAdvanceNodeId.Path2, new Vector2(170f, -20f) },
                { JobAdvanceNodeId.Path1_1, new Vector2(-255f, 130f) },
                { JobAdvanceNodeId.Path1_2, new Vector2(-85f, 130f) },
                { JobAdvanceNodeId.Path2_1, new Vector2(85f, 130f) },
                { JobAdvanceNodeId.Path2_2, new Vector2(255f, 130f) }
            };

            var rootPos = new Vector2(0f, -160f);
            CreateTreeRootMarker(_treeHost, rootPos);

            CreateTreeLine(_treeHost, "Line_root_1", rootPos, positions[JobAdvanceNodeId.Path1]);
            CreateTreeLine(_treeHost, "Line_root_2", rootPos, positions[JobAdvanceNodeId.Path2]);
            CreateTreeLine(_treeHost, "Line_1_1-1", positions[JobAdvanceNodeId.Path1], positions[JobAdvanceNodeId.Path1_1]);
            CreateTreeLine(_treeHost, "Line_1_1-2", positions[JobAdvanceNodeId.Path1], positions[JobAdvanceNodeId.Path1_2]);
            CreateTreeLine(_treeHost, "Line_2_2-1", positions[JobAdvanceNodeId.Path2], positions[JobAdvanceNodeId.Path2_1]);
            CreateTreeLine(_treeHost, "Line_2_2-2", positions[JobAdvanceNodeId.Path2], positions[JobAdvanceNodeId.Path2_2]);

            _treeButtons.Clear();
            _treeButtonImages.Clear();
            foreach (var pair in positions)
                CreateTreeNodeButton(_treeHost, pair.Key, pair.Value);

            BuildAdvanceTooltip(box.transform);

            _advanceConfirmButton = CreateAnchoredButton(box.transform, "ConfirmBtn", "确认进阶",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-130f, 42f), new Vector2(220f, 56f),
                OnAdvanceConfirmClicked);
            _advanceConfirmLabel = _advanceConfirmButton.transform.Find("Label")?.GetComponent<Text>();

            CreateAnchoredButton(box.transform, "CancelBtn", "取消",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(130f, 42f), new Vector2(220f, 56f),
                CloseAdvancePopup);

            _advancePopupRoot.SetActive(false);
        }

        private void CreateTreeRootMarker(RectTransform host, Vector2 pos)
        {
            var go = new GameObject("Root");
            go.transform.SetParent(host, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(96f, 40f);
            var image = go.AddComponent<Image>();
            image.sprite = GameOverlayUI.SharedUiSprite();
            image.color = new Color(0.28f, 0.32f, 0.40f, 1f);
            image.raycastTarget = false;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            StretchFull(labelRect);
            var text = labelGo.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "起点";
            text.raycastTarget = false;
        }

        private void CreateTreeLine(RectTransform host, string name, Vector2 from, Vector2 to)
        {
            var go = new GameObject(name);
            go.transform.SetParent(host, false);
            go.transform.SetAsFirstSibling();
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Vector2 delta = to - from;
            float length = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(Mathf.Max(8f, length - 36f), 4f);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);

            var image = go.AddComponent<Image>();
            image.sprite = GameOverlayUI.SharedUiSprite();
            image.color = new Color(0.55f, 0.58f, 0.64f, 0.55f);
            image.raycastTarget = false;
            _treeLines.Add(image);
        }

        private void CreateTreeNodeButton(RectTransform host, JobAdvanceNodeId nodeId, Vector2 pos)
        {
            string label = JobAdvancePath.ToLabel(nodeId);
            var button = CreateAnchoredButton(
                host,
                $"Node_{label}",
                label,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos,
                new Vector2(84f, 48f),
                () => OnTreeNodeClicked(nodeId));

            var labelText = button.transform.Find("Label")?.GetComponent<Text>();
            if (labelText != null)
            {
                labelText.fontSize = 22;
                labelText.fontStyle = FontStyle.Bold;
                labelText.alignment = TextAnchor.MiddleCenter;
            }

            var image = button.targetGraphic as Image;
            _treeButtons[nodeId] = button;
            if (image != null)
                _treeButtonImages[nodeId] = image;

            var trigger = button.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => OnTreeNodeHoverEnter(nodeId));
            trigger.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => OnTreeNodeHoverExit(nodeId));
            trigger.triggers.Add(exit);
        }

        private void BuildAdvanceTooltip(Transform box)
        {
            var tipGo = new GameObject("NodeTooltip");
            tipGo.transform.SetParent(box, false);
            _tooltipRoot = tipGo;
            _tooltipRect = tipGo.AddComponent<RectTransform>();
            _tooltipRect.anchorMin = _tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltipRect.pivot = new Vector2(0.5f, 0.5f);
            _tooltipRect.sizeDelta = new Vector2(300f, 120f);

            var bg = tipGo.AddComponent<Image>();
            bg.sprite = GameOverlayUI.SharedUiSprite();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);
            bg.raycastTarget = false;

            var outline = tipGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.75f, 0.82f, 0.55f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(tipGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 12f);
            textRect.offsetMax = new Vector2(-14f, -12f);
            _tooltipText = textGo.AddComponent<Text>();
            _tooltipText.font = GameOverlayUI.SharedUiFont();
            _tooltipText.fontSize = 17;
            _tooltipText.alignment = TextAnchor.UpperLeft;
            _tooltipText.color = new Color(0.95f, 0.96f, 0.90f, 1f);
            _tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            _tooltipText.raycastTarget = false;

            tipGo.transform.SetAsLastSibling();
            tipGo.SetActive(false);
        }

        private void BuildUnlockPopup(Transform parent)
        {
            var panelGo = new GameObject("UnlockJobPopup");
            panelGo.transform.SetParent(parent, false);
            _unlockPopupRoot = panelGo;

            var panelRect = panelGo.AddComponent<RectTransform>();
            StretchFull(panelRect);

            var dim = panelGo.AddComponent<Image>();
            dim.sprite = GameOverlayUI.SharedUiSprite();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            dim.raycastTarget = true;

            var box = CreatePopupBox(panelGo.transform, "Box", new Vector2(720f, 680f));

            _unlockTitle = CreateLabel(box.transform, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -32f), new Vector2(640f, 44f),
                30, FontStyle.Bold, TextAnchor.MiddleCenter);

            _unlockHint = CreateLabel(box.transform, "Hint",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -78f), new Vector2(640f, 56f),
                18, FontStyle.Normal, TextAnchor.UpperCenter);
            _unlockHint.color = new Color(0.85f, 0.88f, 0.92f, 1f);

            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(box.transform, false);
            var scrollRectTransform = scrollGo.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.5f, 0f);
            scrollRectTransform.anchorMax = new Vector2(0.5f, 1f);
            scrollRectTransform.pivot = new Vector2(0.5f, 0.5f);
            scrollRectTransform.anchoredPosition = new Vector2(0f, 20f);
            scrollRectTransform.sizeDelta = new Vector2(640f, -200f);
            var scrollImage = scrollGo.AddComponent<Image>();
            scrollImage.sprite = GameOverlayUI.SharedUiSprite();
            scrollImage.color = new Color(0.08f, 0.10f, 0.14f, 0.9f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            StretchFull(viewportRect);
            viewport.AddComponent<RectMask2D>();
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.sprite = GameOverlayUI.SharedUiSprite();
            viewportImage.color = new Color(1f, 1f, 1f, 0.02f);

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 200f);
            _unlockListContent = content.transform;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            _unlockConfirmButton = CreateAnchoredButton(box.transform, "ConfirmBtn", "确认解锁",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-130f, 40f), new Vector2(220f, 56f),
                OnUnlockConfirmClicked);
            _unlockConfirmLabel = _unlockConfirmButton.transform.Find("Label")?.GetComponent<Text>();

            CreateAnchoredButton(box.transform, "CancelBtn", "取消",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(130f, 40f), new Vector2(220f, 56f),
                CloseUnlockPopup);

            _unlockPopupRoot.SetActive(false);
        }

        private void BuildDesignatePopup(Transform parent)
        {
            var panelGo = new GameObject("DesignateGatherPopup");
            panelGo.transform.SetParent(parent, false);
            _designatePopupRoot = panelGo;

            var panelRect = panelGo.AddComponent<RectTransform>();
            StretchFull(panelRect);

            var dim = panelGo.AddComponent<Image>();
            dim.sprite = GameOverlayUI.SharedUiSprite();
            dim.color = new Color(0f, 0f, 0f, 0.7f);
            dim.raycastTarget = true;

            var box = CreatePopupBox(panelGo.transform, "Box", new Vector2(720f, 680f));

            _designateTitle = CreateLabel(box.transform, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -32f), new Vector2(640f, 44f),
                30, FontStyle.Bold, TextAnchor.MiddleCenter);

            _designateHint = CreateLabel(box.transform, "Hint",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -78f), new Vector2(640f, 72f),
                18, FontStyle.Normal, TextAnchor.UpperCenter);
            _designateHint.color = new Color(0.85f, 0.88f, 0.92f, 1f);

            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(box.transform, false);
            var scrollRectTransform = scrollGo.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.5f, 0f);
            scrollRectTransform.anchorMax = new Vector2(0.5f, 1f);
            scrollRectTransform.pivot = new Vector2(0.5f, 0.5f);
            scrollRectTransform.anchoredPosition = new Vector2(0f, 20f);
            scrollRectTransform.sizeDelta = new Vector2(640f, -210f);
            var scrollImage = scrollGo.AddComponent<Image>();
            scrollImage.sprite = GameOverlayUI.SharedUiSprite();
            scrollImage.color = new Color(0.08f, 0.10f, 0.14f, 0.9f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            StretchFull(viewportRect);
            viewport.AddComponent<RectMask2D>();
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.sprite = GameOverlayUI.SharedUiSprite();
            viewportImage.color = new Color(1f, 1f, 1f, 0.02f);

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 200f);
            _designateListContent = content.transform;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            _designateConfirmButton = CreateAnchoredButton(box.transform, "ConfirmBtn", "确认并进阶",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-130f, 40f), new Vector2(240f, 56f),
                OnDesignateConfirmClicked);
            _designateConfirmLabel = _designateConfirmButton.transform.Find("Label")?.GetComponent<Text>();

            CreateAnchoredButton(box.transform, "CancelBtn", "返回",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(130f, 40f), new Vector2(220f, 56f),
                OnDesignateCancelClicked);

            _designatePopupRoot.SetActive(false);
        }

        private static GameObject CreatePopupBox(Transform parent, string name, Vector2 size)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent, false);
            var boxRect = box.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = size;
            var boxImage = box.AddComponent<Image>();
            boxImage.sprite = GameOverlayUI.SharedUiSprite();
            boxImage.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);
            return box;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text CreateLabel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            int fontSize,
            FontStyle style,
            TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateAnchoredButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = GameOverlayUI.FitArtButtonSize(size.x, size.y);

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            GameOverlayUI.ApplyArtButtonStyle(image, button);
            if (onClick != null)
                button.onClick.AddListener(onClick);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 6f);
            labelRect.offsetMax = new Vector2(-12f, -6f);
            var text = labelGo.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            text.raycastTarget = false;
            return button;
        }
    }
}
