using Soup.Employees;
using Soup.Jobs;
using Soup.Levels;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Scene-authored cook zone: backdrop art, three heat stations, score HUD.
    /// </summary>
    public sealed class CookZoneView : MonoBehaviour
    {
        public const int HeatStationCount = 3;

        public static readonly string[] HeatJobIds =
        {
            "low_heat",
            "medium_heat",
            "high_heat",
        };

        [SerializeField] private SpriteRenderer background;
        [SerializeField] private Transform artRoot;
        [SerializeField] private CookHeatSlot[] heatStations = new CookHeatSlot[HeatStationCount];
        [SerializeField] private CookScoreHud scoreHud;
        [SerializeField] private CookProcessedDeltaHud processedDeltaHud;
        [SerializeField] private CookLevelGoalHud levelGoalHud;

        public SpriteRenderer Background => background;
        public Transform ArtRoot => artRoot != null ? artRoot : transform;
        public CookHeatSlot[] HeatStations => heatStations;
        public CookScoreHud ScoreHud => scoreHud;
        public CookProcessedDeltaHud ProcessedDeltaHud => processedDeltaHud;
        public CookLevelGoalHud LevelGoalHud => levelGoalHud;

        public void SetBackground(SpriteRenderer value) => background = value;
        public void SetArtRoot(Transform value) => artRoot = value;
        public void SetScoreHud(CookScoreHud value) => scoreHud = value;
        public void SetProcessedDeltaHud(CookProcessedDeltaHud value) => processedDeltaHud = value;
        public void SetLevelGoalHud(CookLevelGoalHud value) => levelGoalHud = value;
        public void SetHeatStations(CookHeatSlot[] value) => heatStations = value;

        public CookHeatSlot GetHeatStation(int index)
        {
            if (heatStations == null || index < 0 || index >= heatStations.Length)
                return null;
            return heatStations[index];
        }

        public void BindHeatJobs(JobManager jobs)
        {
            if (heatStations == null) return;
            for (int i = 0; i < heatStations.Length && i < HeatJobIds.Length; i++)
            {
                var slot = heatStations[i];
                if (slot == null) continue;
                JobItem job = jobs != null ? jobs.GetById(HeatJobIds[i]) : null;
                slot.BindJob(job);
            }
        }

        public void SetAssignPadsVisible(bool visible)
        {
            if (heatStations == null) return;
            for (int i = 0; i < heatStations.Length; i++)
                heatStations[i]?.SetAssignPadsVisible(visible);
        }

        public void Refresh()
        {
            if (scoreHud == null)
            {
                var root = ArtRoot;
                if (root != null)
                    scoreHud = root.GetComponent<CookScoreHud>();
            }

            if (heatStations != null)
            {
                for (int i = 0; i < heatStations.Length; i++)
                    heatStations[i]?.RefreshCount();
            }

            scoreHud?.Refresh();
            if (processedDeltaHud == null && artRoot != null)
                processedDeltaHud = artRoot.GetComponent<CookProcessedDeltaHud>();
            if (processedDeltaHud == null)
                processedDeltaHud = GetComponentInChildren<CookProcessedDeltaHud>(true);
            processedDeltaHud?.Refresh();
            EnsureLevelGoalHud();
            levelGoalHud?.Refresh();
        }

        private void EnsureLevelGoalHud()
        {
            var root = ArtRoot;
            if (root == null) return;
            if (levelGoalHud == null)
                levelGoalHud = root.GetComponent<CookLevelGoalHud>();
            if (levelGoalHud == null)
                levelGoalHud = root.gameObject.AddComponent<CookLevelGoalHud>();
            levelGoalHud.EnsureTexts();
        }

        public float RecommendedOrthographicSize()
        {
            return ZoneViewFraming.CoverOrthographicSize(background);
        }

        public float RecommendedCameraCenterY()
        {
            return background != null ? background.bounds.center.y : 0f;
        }

        public float RecommendedZoneSpacing()
        {
            if (background == null) return 0f;
            float width = background.bounds.size.x;
            return width > 0.1f ? width : 0f;
        }

        private void Awake()
        {
            MatchBackdropSizeToGather();
            EnsureProcessedDeltaHud();
            EnsureLevelGoalHud();
        }

        private void OnEnable()
        {
            BindEmployees(true);
            MatchBackdropSizeToGather();
            EnsureProcessedDeltaHud();
            EnsureLevelGoalHud();
            BindRuntime(true);
        }

        private void EnsureProcessedDeltaHud()
        {
            var root = ArtRoot;
            if (root == null) return;
            if (processedDeltaHud == null)
                processedDeltaHud = root.GetComponent<CookProcessedDeltaHud>();
            if (processedDeltaHud == null)
                processedDeltaHud = root.gameObject.AddComponent<CookProcessedDeltaHud>();
            processedDeltaHud.EnsureSign();
        }

        /// <summary>
        /// Bind Art Assets used to reimport 烹饪 (1).png as a 1-unit icon (PPU≈2048),
        /// shrinking the zone until the next manual fix. Always match gather world size.
        /// </summary>
        private void MatchBackdropSizeToGather()
        {
            if (background == null || background.sprite == null) return;
            var gather = FindObjectOfType<GatherZoneView>();
            if (gather == null || gather.Background == null) return;

            Vector2 target = gather.Background.bounds.size;
            Vector2 local = background.sprite.bounds.size;
            if (local.x < 0.01f || local.y < 0.01f || target.x < 0.1f || target.y < 0.1f)
                return;

            var root = artRoot != null ? artRoot : background.transform;
            var next = new Vector3(target.x / local.x, target.y / local.y, 1f);
            if ((root.localScale - next).sqrMagnitude > 0.0001f)
                root.localScale = next;

            RefreshCameraFraming();
        }

        /// <summary>
        /// 背景缩放后刷新烹饪区相机 ortho，避免仍使用缩放前的错误视野。
        /// </summary>
        public void RefreshCameraFraming()
        {
            var cam = FindObjectOfType<ZoneCameraController>();
            if (cam == null) return;

            var gather = FindObjectOfType<GatherZoneView>();
            float size = ZoneViewFraming.ResolveCookOrthographicSize(this, gather);
            if (size > 0.5f)
                cam.ConfigureZone(MapZoneType.Cook, size, RecommendedCameraCenterY());
        }

        private void OnDisable()
        {
            BindEmployees(false);
            BindRuntime(false);
        }

        private TurnManager _boundTurns;
        private LevelManager _boundLevels;
        private ResourceStore _boundStore;
        private EmployeeManager _boundEmployees;

        private void Update()
        {
            if (!Application.isPlaying) return;

            bool rebound = false;
            if (EmployeeManager.Instance != _boundEmployees)
            {
                BindEmployees(true);
                rebound = true;
            }

            if (TurnManager.Instance != _boundTurns
                || LevelManager.Instance != _boundLevels
                || ResourceStore.Instance != _boundStore)
            {
                BindRuntime(true);
                rebound = true;
            }

            if (rebound)
                Refresh();
        }

        private void BindEmployees(bool bind)
        {
            if (_boundEmployees != null)
                _boundEmployees.Changed -= HandleChanged;

            if (!bind)
            {
                _boundEmployees = null;
                return;
            }

            _boundEmployees = EmployeeManager.Instance;
            if (_boundEmployees != null)
                _boundEmployees.Changed += HandleChanged;
        }

        private void BindRuntime(bool bind)
        {
            if (_boundTurns != null)
            {
                _boundTurns.TurnResolved -= HandleTurnResolved;
                _boundTurns.StageSettled -= HandleStageSettled;
                _boundTurns.UndoApplied -= HandleChanged;
            }

            if (_boundLevels != null)
                _boundLevels.Changed -= HandleChanged;

            if (_boundStore != null)
                _boundStore.Changed -= HandleChanged;

            if (!bind)
            {
                _boundTurns = null;
                _boundLevels = null;
                _boundStore = null;
                return;
            }

            _boundTurns = TurnManager.Instance;
            if (_boundTurns != null)
            {
                _boundTurns.TurnResolved += HandleTurnResolved;
                _boundTurns.StageSettled += HandleStageSettled;
                _boundTurns.UndoApplied += HandleChanged;
            }

            _boundLevels = LevelManager.Instance;
            if (_boundLevels != null)
                _boundLevels.Changed += HandleChanged;

            _boundStore = ResourceStore.Instance;
            if (_boundStore != null)
                _boundStore.Changed += HandleChanged;
        }

        private void HandleTurnResolved(TurnResult _) => Refresh();

        private void HandleStageSettled(StageSettlementResult _) => Refresh();

        private void HandleChanged() => Refresh();
    }
}
