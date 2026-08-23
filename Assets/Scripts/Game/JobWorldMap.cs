using System.Collections.Generic;
using Soup.Jobs;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Soup.Game
{
    /// <summary>
    /// Builds three map zones.
    /// Gather / Process: fixed empty slots; unlocked jobs appear in slots only.
    /// Cook: all jobs shown; locked ones stay gray and inert.
    /// </summary>
    public class JobWorldMap : MonoBehaviour
    {
        private const int GatherSlotCount = JobProgressionRules.GatherMaxStations;
        private const int ProcessSlotCount = JobProgressionRules.ProcessMaxStations;

        [SerializeField] private ZoneCameraController cameraController;
        [SerializeField] private GatherZoneView gatherZone;
        [SerializeField] private ProcessZoneView processZone;
        [SerializeField] private CookZoneView cookZone;
        [SerializeField] private float refreshInterval = 0.25f;
        [SerializeField] private float gridCellX = 3.2f;
        [SerializeField] private float gridCellY = 3.0f;
        [SerializeField] private int gridColumns = 4;
        [SerializeField] private float slotSpacing = 3.6f;

        private readonly List<JobStationMarker> _markers = new List<JobStationMarker>();
        private readonly Dictionary<JobItem, JobStationMarker> _byJob = new Dictionary<JobItem, JobStationMarker>();

        private readonly JobItem[] _gatherSlotJobs = new JobItem[GatherSlotCount];
        private readonly bool[] _gatherSlotDead = new bool[GatherSlotCount];
        private readonly Transform[] _gatherEmptySlots = new Transform[GatherSlotCount];
        private Transform _gatherSlotsRoot;

        private readonly JobItem[] _processSlotJobs = new JobItem[ProcessSlotCount];
        private readonly Transform[] _processEmptySlots = new Transform[ProcessSlotCount];
        private Transform _processSlotsRoot;

        private Transform _root;
        private float _nextRefresh;
        private bool _groundBuilt;

        private static readonly Color GatherGround = new Color(0.22f, 0.42f, 0.28f, 1f);
        private static readonly Color ProcessGround = new Color(0.42f, 0.34f, 0.20f, 1f);
        private static readonly Color CookGround = new Color(0.42f, 0.22f, 0.22f, 1f);
        private static readonly Color GatherStation = new Color(0.45f, 0.85f, 0.50f, 1f);
        private static readonly Color ProcessStation = new Color(0.90f, 0.70f, 0.35f, 1f);
        private static readonly Color CookStation = new Color(0.95f, 0.45f, 0.40f, 1f);
        private static readonly Color LockedStation = new Color(0.38f, 0.38f, 0.40f, 0.85f);
        private static readonly Color GatherEmptySlotColor = new Color(0.18f, 0.28f, 0.20f, 0.55f);
        private static readonly Color ProcessEmptySlotColor = new Color(0.30f, 0.24f, 0.14f, 0.55f);

        private void Awake()
        {
            if (cameraController == null)
                cameraController = FindObjectOfType<ZoneCameraController>();
            if (gatherZone == null)
                gatherZone = FindObjectOfType<GatherZoneView>();
            if (processZone == null)
                processZone = FindObjectOfType<ProcessZoneView>();
            if (cookZone == null)
                cookZone = FindObjectOfType<CookZoneView>();

            if (gatherZone != null && cameraController != null)
            {
                float size = gatherZone.RecommendedOrthographicSize();
                float spacing = gatherZone.RecommendedZoneSpacing();
                cameraController.ConfigureView(spacing, size);
                cameraController.ConfigureZone(
                    MapZoneType.Gather,
                    size,
                    gatherZone.RecommendedCameraCenterY());
                cameraController.SnapToZone(MapZoneType.Gather);
                gatherZone.transform.position = cameraController.GetZoneCenter(MapZoneType.Gather);
            }

            if (processZone != null && cameraController != null)
            {
                processZone.transform.position = cameraController.GetZoneCenter(MapZoneType.Process);
                cameraController.ConfigureZone(
                    MapZoneType.Process,
                    processZone.RecommendedOrthographicSize(),
                    processZone.RecommendedCameraCenterY());
            }

            if (cookZone != null && cameraController != null)
            {
                cookZone.transform.position = cameraController.GetZoneCenter(MapZoneType.Cook);
                float cookSize = cookZone.RecommendedOrthographicSize();
                if (cookSize < 0.5f && gatherZone != null)
                    cookSize = gatherZone.RecommendedOrthographicSize();
                cameraController.ConfigureZone(
                    MapZoneType.Cook,
                    cookSize,
                    cookZone.RecommendedCameraCenterY());
            }

            EnsureRoot();
            BuildGround();
            BuildZoneDividers();
            if (GetComponent<StationHoverController>() == null)
                gameObject.AddComponent<StationHoverController>();
            if (gatherZone == null)
                EnsureSlotZone(JobType.Gather);
            if (processZone == null)
                EnsureSlotZone(JobType.Process);
        }

        private void Start()
        {
            cookZone?.RefreshCameraFraming();
            RebuildStations();
            cookZone?.Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + refreshInterval;
                RebuildStations();
                RefreshLabels();
            }

            HandleStationClicks();
        }

        private void HandleStationClicks()
        {
            if (Camera.main == null) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // 进阶巡视：仅当点在弹窗/返回等 UI 上时吞掉世界点击，避免误挡岗位点选。
            if (AdvancementVisit.IsActive)
            {
                if (IsPointerOverAdvancementUi())
                    return;
            }
            else if (JobStationMarker.IsPointerOverUi())
            {
                return;
            }

            Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var hit = Physics2D.OverlapPoint(new Vector2(world.x, world.y));
            if (hit == null) return;

            if (gatherZone != null && gatherZone.TryHandleZoneSwitch(hit))
                return;

            var gatherSlot = hit.GetComponent<GatherStationSlot>();
            if (gatherSlot == null)
                gatherSlot = hit.GetComponentInParent<GatherStationSlot>();
            if (gatherSlot != null)
            {
                HandleGatherSlotClick(gatherSlot, hit);
                return;
            }

            var processSlot = hit.GetComponent<ProcessStationSlot>();
            if (processSlot == null)
                processSlot = hit.GetComponentInParent<ProcessStationSlot>();
            if (processSlot != null)
            {
                HandleProcessSlotClick(processSlot, hit);
                return;
            }

            var heatSlot = hit.GetComponent<CookHeatSlot>();
            if (heatSlot == null)
                heatSlot = hit.GetComponentInParent<CookHeatSlot>();
            if (heatSlot != null)
            {
                if (AdvancementVisit.IsActive)
                {
                    var visitUi = FindObjectOfType<AdvancementVisitUI>();
                    if (heatSlot.IsBound)
                        visitUi?.TrySelectGatherJob(heatSlot.Job);
                    return;
                }

                heatSlot.HandleHit(hit);
                return;
            }

            var marker = hit.GetComponent<JobStationMarker>();
            if (marker == null)
                marker = hit.GetComponentInParent<JobStationMarker>();

            if (AdvancementVisit.IsActive)
            {
                var visitUi = FindObjectOfType<AdvancementVisitUI>();
                if (marker != null)
                {
                    visitUi?.TrySelectStation(marker);
                    return;
                }

                var empty = hit.GetComponent<EmptyStationSlot>();
                if (empty == null)
                    empty = hit.GetComponentInParent<EmptyStationSlot>();
                if (empty != null)
                    visitUi?.TrySelectEmptySlot(empty);
                return;
            }

            if (marker == null) return;
            marker.HandleHit(hit);
        }

        private static bool IsPointerOverAdvancementUi()
        {
            var es = EventSystem.current;
            if (es == null) return false;

            var ped = new PointerEventData(es) { position = Input.mousePosition };
            var results = new List<RaycastResult>(8);
            es.RaycastAll(ped, results);
            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null) continue;
                // Only AdvancementVisitUI canvas should block world station picks.
                if (go.GetComponentInParent<AdvancementVisitUI>() != null)
                    return true;
                var canvas = go.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.name == "AdvancementVisitCanvas")
                    return true;
            }

            return false;
        }

        /// <summary>进阶巡视时隐藏精灵 +/-，仅允许点选岗位本体或空位。</summary>
        public void ApplyAdvancementVisitPresentation()
        {
            bool visit = AdvancementVisit.IsActive;
            for (int i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                if (marker == null) continue;
                marker.SetAssignPadsVisible(!visit && marker.IsUnlocked);
            }

            HighlightEmptySlotsForVisit(visit);
            if (gatherZone != null)
            {
                var stations = gatherZone.Stations;
                if (stations != null)
                {
                    for (int i = 0; i < stations.Length; i++)
                        stations[i]?.SetAssignPadsVisible(!visit);
                }
            }

            if (processZone != null)
            {
                var stations = processZone.Stations;
                if (stations != null)
                {
                    for (int i = 0; i < stations.Length; i++)
                        stations[i]?.SetAssignPadsVisible(!visit);
                }
            }

            if (cookZone != null)
                cookZone.SetAssignPadsVisible(!visit);
        }

        private void HighlightEmptySlotsForVisit(bool visit)
        {
            HighlightEmptySlotsForVisit(EmptySlots(JobType.Gather), JobType.Gather, visit);
            HighlightEmptySlotsForVisit(EmptySlots(JobType.Process), JobType.Process, visit);
        }

        private void HighlightEmptySlotsForVisit(Transform[] emptySlots, JobType type, bool visit)
        {
            if (emptySlots == null) return;
            var slotJobs = SlotJobs(type);
            Color baseColor = EmptyColorFor(type);
            for (int i = 0; i < emptySlots.Length; i++)
            {
                var empty = emptySlots[i];
                if (empty == null) continue;
                bool occupied = slotJobs != null && i < slotJobs.Length && slotJobs[i] != null;
                var sr = empty.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                if (occupied)
                {
                    sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.22f);
                    continue;
                }

                // 空位在巡视中略提亮，提示可点击解锁。
                sr.color = visit
                    ? new Color(
                        Mathf.Min(1f, baseColor.r * 1.25f),
                        Mathf.Min(1f, baseColor.g * 1.25f),
                        Mathf.Min(1f, baseColor.b * 1.25f),
                        Mathf.Max(baseColor.a, 0.75f))
                    : baseColor;
            }
        }

        public bool IsEmptySlotFree(EmptyStationSlot slot)
        {
            if (slot == null) return false;
            var slotJobs = SlotJobs(slot.JobType);
            if (slotJobs == null) return false;
            int i = slot.SlotIndex;
            if (i < 0 || i >= slotJobs.Length) return false;
            if (slot.JobType == JobType.Gather && i < _gatherSlotDead.Length && _gatherSlotDead[i])
                return false;
            return slotJobs[i] == null;
        }

        public bool IsGatherSlotDestroyed(EmptyStationSlot slot)
        {
            if (slot == null || slot.JobType != JobType.Gather) return false;
            int i = slot.SlotIndex;
            return i >= 0 && i < _gatherSlotDead.Length && _gatherSlotDead[i];
        }

        public void RebuildStations()
        {
            EnsureRoot();
            if (gatherZone == null)
                EnsureSlotZone(JobType.Gather);
            if (processZone == null)
                EnsureSlotZone(JobType.Process);
            else
                ClearProceduralProcessSlots();

            var jobs = JobManager.Instance;
            if (jobs == null) return;

            var progression = JobProgressionManager.Instance;
            RebuildSlotStations(JobType.Gather, progression);
            RebuildSlotStations(JobType.Process, progression);
            RebuildCookStations(jobs, progression);
        }

        public void RefreshLabels()
        {
            for (int i = 0; i < _markers.Count; i++)
            {
                if (_markers[i] != null)
                    _markers[i].RefreshLabel();
            }

            gatherZone?.Refresh();
            processZone?.Refresh();
            cookZone?.Refresh();
        }

        private void HandleGatherSlotClick(GatherStationSlot slot, Collider2D hit)
        {
            if (slot == null || slot.IsDestroyed) return;

            if (AdvancementVisit.IsActive)
            {
                var visitUi = FindObjectOfType<AdvancementVisitUI>();
                if (slot.IsUnlocked)
                    visitUi?.TrySelectGatherJob(slot.Job);
                else
                    visitUi?.TrySelectEmptySlot(slot.EmptySlot);
                return;
            }

            slot.HandleHit(hit);
        }

        private void HandleProcessSlotClick(ProcessStationSlot slot, Collider2D hit)
        {
            if (slot == null) return;

            if (AdvancementVisit.IsActive)
            {
                var visitUi = FindObjectOfType<AdvancementVisitUI>();
                if (slot.IsUnlocked)
                    visitUi?.TrySelectGatherJob(slot.Job);
                else if (slot.EmptySlot != null)
                    visitUi?.TrySelectEmptySlot(slot.EmptySlot);
                return;
            }

            slot.HandleHit(hit);
        }

        private void RebuildSlotStations(JobType type, JobProgressionManager progression)
        {
            var slotJobs = SlotJobs(type);
            var emptySlots = EmptySlots(type);
            int slotCount = slotJobs.Length;
            Color stationColor = ColorFor(type);
            Color emptyColor = EmptyColorFor(type);
            bool gatherArt = type == JobType.Gather && gatherZone != null;
            bool processArt = type == JobType.Process && processZone != null;

            var unlocked = progression != null
                ? progression.GetUnlocked(type)
                : new List<JobItem>();

            for (int i = 0; i < slotCount; i++)
            {
                var assigned = slotJobs[i];
                if (assigned == null) continue;
                if (type == JobType.Gather && progression != null && progression.IsDestroyedGatherJob(assigned))
                {
                    _gatherSlotDead[i] = true;
                    slotJobs[i] = null;
                    continue;
                }

                if (!unlocked.Contains(assigned))
                    slotJobs[i] = null;
            }

            if (type == JobType.Gather)
            {
                int destroyed = progression != null ? progression.DestroyedGatherJobs.Count : 0;
                int deadCount = 0;
                for (int i = 0; i < _gatherSlotDead.Length; i++)
                {
                    if (_gatherSlotDead[i]) deadCount++;
                }

                for (int i = slotCount - 1; i >= 0 && deadCount < destroyed; i--)
                {
                    if (_gatherSlotDead[i] || slotJobs[i] != null) continue;
                    _gatherSlotDead[i] = true;
                    deadCount++;
                }
            }

            for (int i = 0; i < unlocked.Count && i < slotCount; i++)
            {
                var job = unlocked[i];
                if (job == null || FindSlot(slotJobs, job) >= 0) continue;
                int free = FindFreeGatherAwareSlot(type, slotJobs);
                if (free < 0) break;
                slotJobs[free] = job;
            }

            var keep = new HashSet<JobItem>();
            for (int slot = 0; slot < slotCount; slot++)
            {
                bool dead = type == JobType.Gather && _gatherSlotDead[slot];
                var job = dead ? null : slotJobs[slot];
                if (dead)
                    slotJobs[slot] = null;

                if (gatherArt)
                {
                    var artSlot = gatherZone.GetStation(slot);
                    if (artSlot == null) continue;
                    if (dead)
                        artSlot.BindDestroyed();
                    else if (job == null)
                        artSlot.BindEmpty();
                    else
                    {
                        artSlot.BindJob(job);
                        keep.Add(job);
                    }

                    continue;
                }

                if (processArt)
                {
                    var artSlot = processZone.GetStation(slot);
                    if (artSlot == null) continue;
                    if (job == null)
                        artSlot.BindEmpty();
                    else
                    {
                        artSlot.BindJob(job);
                        keep.Add(job);
                    }

                    continue;
                }

                Vector3 pos = GetSquareSlotPosition(type, slot, slotCount);
                var empty = emptySlots[slot];
                if (empty != null)
                    empty.position = pos;

                SetEmptySlotOccupied(empty, job != null || dead, emptyColor);
                var emptyCol = empty != null ? empty.GetComponent<CircleCollider2D>() : null;
                if (emptyCol != null)
                    emptyCol.enabled = job == null && !dead;

                if (job == null)
                    continue;

                keep.Add(job);
                if (!_byJob.TryGetValue(job, out var marker) || marker == null)
                {
                    marker = CreateMarker(job);
                    _byJob[job] = marker;
                    _markers.Add(marker);
                }

                marker.transform.position = pos;
                marker.gameObject.SetActive(true);
                marker.SetUnlocked(true, stationColor, LockedStation);
                if (AdvancementVisit.IsActive)
                    marker.SetAssignPadsVisible(false);
            }

            var remove = new List<JobItem>();
            foreach (var pair in _byJob)
            {
                if (pair.Key == null || pair.Key.JobType != type) continue;
                if (!keep.Contains(pair.Key))
                    remove.Add(pair.Key);
            }

            DestroyMarkers(remove);
            if (gatherArt)
                gatherZone.Refresh();
            if (processArt)
                processZone.Refresh();
        }

        private int FindFreeGatherAwareSlot(JobType type, JobItem[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) continue;
                if (type == JobType.Gather && i < _gatherSlotDead.Length && _gatherSlotDead[i])
                    continue;
                return i;
            }

            return -1;
        }

        private void RebuildCookStations(JobManager jobs, JobProgressionManager progression)
        {
            if (cookZone != null)
            {
                cookZone.BindHeatJobs(jobs);
                cookZone.Refresh();
                cookZone.SetAssignPadsVisible(!AdvancementVisit.IsActive);

                // Remove leftover procedural cook markers (heat jobs are authored now).
                var removeAuthored = new List<JobItem>();
                foreach (var pair in _byJob)
                {
                    if (pair.Key == null || pair.Key.JobType != JobType.Cook) continue;
                    removeAuthored.Add(pair.Key);
                }

                DestroyMarkers(removeAuthored);
                return;
            }

            var keep = new HashSet<JobItem>();
            var cookJobs = new List<JobItem>();

            var all = jobs.All;
            for (int i = 0; i < all.Count; i++)
            {
                var job = all[i];
                if (job == null || job.JobType != JobType.Cook) continue;
                keep.Add(job);
                cookJobs.Add(job);
            }

            cookJobs.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));

            for (int i = 0; i < cookJobs.Count; i++)
            {
                var job = cookJobs[i];
                bool unlocked = progression == null || progression.IsUnlocked(job);
                if (!_byJob.TryGetValue(job, out var marker) || marker == null)
                {
                    marker = CreateMarker(job);
                    _byJob[job] = marker;
                    _markers.Add(marker);
                }

                marker.transform.position = GetGridPosition(JobType.Cook, i, cookJobs.Count);
                marker.gameObject.SetActive(true);
                marker.SetUnlocked(unlocked, CookStation, LockedStation);
                if (AdvancementVisit.IsActive)
                    marker.SetAssignPadsVisible(false);
            }

            var remove = new List<JobItem>();
            foreach (var pair in _byJob)
            {
                if (pair.Key == null || pair.Key.JobType != JobType.Cook) continue;
                if (!keep.Contains(pair.Key))
                    remove.Add(pair.Key);
            }

            DestroyMarkers(remove);
        }

        private void DestroyMarkers(List<JobItem> remove)
        {
            for (int i = 0; i < remove.Count; i++)
            {
                var job = remove[i];
                if (!_byJob.TryGetValue(job, out var marker)) continue;
                _byJob.Remove(job);
                _markers.Remove(marker);
                if (marker != null)
                    Destroy(marker.gameObject);
            }
        }

        private static void SetEmptySlotOccupied(Transform empty, bool occupied, Color emptyColor)
        {
            if (empty == null) return;
            var label = empty.Find("Label");
            if (label != null)
                label.gameObject.SetActive(!occupied);

            var sr = empty.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = occupied
                    ? new Color(emptyColor.r, emptyColor.g, emptyColor.b, 0.22f)
                    : emptyColor;
        }

        private static int FindSlot(JobItem[] slots, JobItem job)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == job)
                    return i;
            }

            return -1;
        }

        private static int FindFreeSlot(JobItem[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                    return i;
            }

            return -1;
        }

        private JobItem[] SlotJobs(JobType type) =>
            type == JobType.Process ? _processSlotJobs : _gatherSlotJobs;

        private Transform[] EmptySlots(JobType type) =>
            type == JobType.Process ? _processEmptySlots : _gatherEmptySlots;

        private static Color EmptyColorFor(JobType type) =>
            type == JobType.Process ? ProcessEmptySlotColor : GatherEmptySlotColor;

        private void EnsureRoot()
        {
            if (_root != null) return;
            var existing = transform.Find("Zones");
            _root = existing != null ? existing : new GameObject("Zones").transform;
            _root.SetParent(transform, false);
        }

        /// <summary>
        /// Authored ProcessZone already draws warning signs for empty slots;
        /// remove leftover procedural gray rings if they were created earlier.
        /// </summary>
        private void ClearProceduralProcessSlots()
        {
            EnsureRoot();
            for (int i = 0; i < _processEmptySlots.Length; i++)
                _processEmptySlots[i] = null;

            if (_processSlotsRoot == null && _root != null)
            {
                var existing = _root.Find("ProcessSlots");
                if (existing != null)
                    _processSlotsRoot = existing;
            }

            if (_processSlotsRoot == null) return;

            if (Application.isPlaying)
                Destroy(_processSlotsRoot.gameObject);
            else
                DestroyImmediate(_processSlotsRoot.gameObject);
            _processSlotsRoot = null;
        }

        private void EnsureSlotZone(JobType type)
        {
            EnsureRoot();
            bool isProcess = type == JobType.Process;
            if (isProcess && processZone != null)
                return;
            if (!isProcess && gatherZone != null)
                return;

            string rootName = isProcess ? "ProcessSlots" : "GatherSlots";
            ref Transform slotsRoot = ref isProcess ? ref _processSlotsRoot : ref _gatherSlotsRoot;
            var emptySlots = EmptySlots(type);
            int slotCount = emptySlots.Length;
            Color emptyColor = EmptyColorFor(type);

            if (slotsRoot == null)
            {
                var existing = _root.Find(rootName);
                slotsRoot = existing != null
                    ? existing
                    : new GameObject(rootName).transform;
                slotsRoot.SetParent(_root, false);
            }

            for (int i = 0; i < slotCount; i++)
            {
                if (emptySlots[i] != null)
                {
                    EnsureEmptySlotInteractive(emptySlots[i].gameObject, type, i);
                    continue;
                }

                var go = new GameObject($"EmptySlot_{i}");
                go.transform.SetParent(slotsRoot, false);
                go.transform.position = GetSquareSlotPosition(type, i, slotCount);
                go.transform.localScale = Vector3.one * 1.35f;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = CreateRingSprite();
                sr.color = emptyColor;
                sr.sortingOrder = 1;

                EnsureEmptySlotInteractive(go, type, i);

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(go.transform, false);
                labelGo.transform.localPosition = Vector3.zero;
                labelGo.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
                var text = labelGo.AddComponent<TextMesh>();
                text.text = "空位";
                text.fontSize = 28;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = new Color(1f, 1f, 1f, 0.28f);
                var mr = labelGo.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = 2;

                emptySlots[i] = go.transform;
            }
        }

        private static void EnsureEmptySlotInteractive(GameObject go, JobType type, int index)
        {
            if (go == null) return;
            var col = go.GetComponent<CircleCollider2D>();
            if (col == null)
                col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.55f;

            var slot = go.GetComponent<EmptyStationSlot>();
            if (slot == null)
                slot = go.AddComponent<EmptyStationSlot>();
            slot.Configure(type, index);
        }

        private void BuildGround()
        {
            if (_groundBuilt) return;
            _groundBuilt = true;
            if (gatherZone == null)
                CreateZoneGround(MapZoneType.Gather, "采集区", GatherGround);
            if (processZone == null)
                CreateZoneGround(MapZoneType.Process, "处理区", ProcessGround);
            if (cookZone == null)
                CreateZoneGround(MapZoneType.Cook, "烹饪区", CookGround);
        }

        private void BuildZoneDividers()
        {
            EnsureRoot();
            var existing = _root.Find("ZoneDividers");
            if (existing != null)
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

            Sprite sprite = ResolveVerticalDividerSprite();
            if (sprite == null) return;

            float spacing = cameraController != null ? cameraController.ZoneSpacing : 22f;
            if (spacing < 1f) return;

            float height = 18f;
            float centerY = 0f;
            if (gatherZone != null && gatherZone.Background != null)
            {
                height = gatherZone.Background.bounds.size.y;
                centerY = gatherZone.Background.bounds.center.y;
            }
            else if (processZone != null && processZone.Background != null)
            {
                height = processZone.Background.bounds.size.y;
                centerY = processZone.Background.bounds.center.y;
            }
            else if (cookZone != null && cookZone.Background != null)
            {
                height = cookZone.Background.bounds.size.y;
                centerY = cookZone.Background.bounds.center.y;
            }

            var root = new GameObject("ZoneDividers").transform;
            root.SetParent(_root, false);

            // Shared edges Gather|Process and Process|Cook (zones abut edge-to-edge).
            // Divider is centered on the seam so each adjacent view sees about half of it.
            PlaceZoneDivider(root, sprite, new Vector3(-spacing * 0.5f, centerY, -0.05f), height);
            PlaceZoneDivider(root, sprite, new Vector3(spacing * 0.5f, centerY, -0.05f), height);
        }

        private static Sprite ResolveVerticalDividerSprite()
        {
            var art = GameArtLibrary.Load();
            if (art != null && art.DividerVertical != null)
                return art.DividerVertical;

            // Fallback if Art Library was not rebound after asset moves.
            var tex = Resources.Load<Texture2D>("UI/divider2");
            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

            return null;
        }

        private static void PlaceZoneDivider(Transform parent, Sprite sprite, Vector3 worldPos, float targetHeight)
        {
            var go = new GameObject("DividerVertical");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 40;
            sr.color = Color.white;

            Vector2 native = sprite.bounds.size;
            float sx = native.x > 0.01f ? GatherZoneView.DividerWorldWidth / native.x : 1f;
            float sy = native.y > 0.01f ? targetHeight / native.y : 1f;
            go.transform.localScale = new Vector3(sx, sy, 1f);
        }

        private void CreateZoneGround(MapZoneType zone, string title, Color color)
        {
            float spacing = cameraController != null ? cameraController.ZoneSpacing : 22f;
            Vector3 center = cameraController != null
                ? cameraController.GetZoneCenter(zone)
                : new Vector3(((int)zone - 1) * spacing, 0f, 0f);

            float width = Mathf.Max(1f, spacing);
            float height = 12f;
            if (gatherZone != null && gatherZone.Background != null)
                height = gatherZone.Background.bounds.size.y;
            else if (processZone != null && processZone.Background != null)
                height = processZone.Background.bounds.size.y;
            else if (Camera.main != null)
                height = Camera.main.orthographicSize * 2f;

            var ground = new GameObject($"Ground_{zone}");
            ground.transform.SetParent(_root, false);
            ground.transform.position = center + new Vector3(0f, 0f, 1f);

            var sr = ground.AddComponent<SpriteRenderer>();
            sr.sprite = CreateRectSprite();
            sr.color = color;
            sr.sortingOrder = 0;
            // CreateRectSprite is 1×1; scale to fill the zone panel.
            ground.transform.localScale = new Vector3(width, height, 1f);

            var labelGo = new GameObject("ZoneLabel");
            labelGo.transform.SetParent(ground.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.42f, -0.1f);
            labelGo.transform.localScale = new Vector3(0.012f, 0.012f, 0.012f);
            var text = labelGo.AddComponent<TextMesh>();
            text.text = title;
            text.fontSize = 64;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 1f, 1f, 0.35f);
            var mr = labelGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 1;
        }

        private JobStationMarker CreateMarker(JobItem job)
        {
            var go = new GameObject($"Station_{job.Id}");
            go.transform.SetParent(_root, false);
            go.transform.localScale = Vector3.one * 1.25f;

            if (go.GetComponent<CircleCollider2D>() == null)
                go.AddComponent<CircleCollider2D>().radius = 0.55f;

            var marker = go.AddComponent<JobStationMarker>();
            marker.Setup(job, ColorFor(job.JobType));
            return marker;
        }

        /// <summary>
        /// Square grid of empty slots (2 columns), same spacing as gather.
        /// Process (2 slots) occupies the top row; gather (4) fills 2×2.
        /// </summary>
        private Vector3 GetSquareSlotPosition(JobType type, int slotIndex, int slotCount)
        {
            var zoneCenter = GetZoneCenter(ZoneFor(type));
            int cols = Mathf.Min(2, Mathf.Max(1, slotCount));
            int rows = Mathf.CeilToInt(slotCount / (float)cols);
            int col = slotIndex % cols;
            int row = slotIndex / cols;

            float x = (col - (cols - 1) * 0.5f) * slotSpacing;
            float y = ((rows - 1) * 0.5f - row) * slotSpacing;
            return zoneCenter + new Vector3(x, y, 0f);
        }

        private Vector3 GetGridPosition(JobType type, int index, int total)
        {
            var zone = ZoneFor(type);
            Vector3 center = GetZoneCenter(zone);

            int cols = Mathf.Max(1, gridColumns);
            if (total <= 3) cols = Mathf.Max(1, total);
            else if (total <= 6) cols = 3;

            int row = index / cols;
            int col = index % cols;
            int rows = Mathf.CeilToInt(total / (float)cols);

            float width = (cols - 1) * gridCellX;
            float height = (rows - 1) * gridCellY;
            float x = col * gridCellX - width * 0.5f;
            float y = -row * gridCellY + height * 0.5f;
            return center + new Vector3(x, y, 0f);
        }

        private Vector3 GetZoneCenter(MapZoneType zone)
        {
            float spacing = cameraController != null ? cameraController.ZoneSpacing : 22f;
            return cameraController != null
                ? cameraController.GetZoneCenter(zone)
                : new Vector3(((int)zone - 1) * spacing, 0f, 0f);
        }

        private static MapZoneType ZoneFor(JobType type)
        {
            switch (type)
            {
                case JobType.Gather: return MapZoneType.Gather;
                case JobType.Process: return MapZoneType.Process;
                default: return MapZoneType.Cook;
            }
        }

        private static Color ColorFor(JobType type)
        {
            switch (type)
            {
                case JobType.Gather: return GatherStation;
                case JobType.Process: return ProcessStation;
                default: return CookStation;
            }
        }

        private static Sprite CreateRectSprite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        }

        private static Sprite CreateRingSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float outer = size * 0.48f;
            float inner = size * 0.34f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                    float a = 0f;
                    if (d <= outer && d >= inner)
                        a = Mathf.Clamp01(Mathf.Min(outer - d, d - inner) * 3f);
                    else if (d < inner)
                        a = 0.12f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
