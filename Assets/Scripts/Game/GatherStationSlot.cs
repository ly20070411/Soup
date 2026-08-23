using Soup.Employees;
using Soup.Jobs;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// One gather station built from scene art: signboard portrait, worker count, +/- keys.
    /// </summary>
    [ExecuteAlways]
    public sealed class GatherStationSlot : MonoBehaviour
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private SpriteRenderer signboard;
        [SerializeField] private SpriteRenderer portrait;
        [SerializeField] private SpriteRenderer countFrame;
        [SerializeField] private TextMesh countMesh;
        [SerializeField] private TextMesh nameMesh;
        [SerializeField] private Transform minusButton;
        [SerializeField] private Transform plusButton;
        [SerializeField] private Transform bush;
        [SerializeField] private EmptyStationSlot emptySlot;

        // Inner wooden face on 告示牌.png (pivot 184.5,181, ppu 100) — not the pole or outline.
        private static readonly Vector3 BoardCenterLocal = new Vector3(0f, 0.275f, -0.02f);
        private static readonly Vector2 BoardSize = new Vector2(2.46f, 1.20f);
        // 等比放大到刚好撑满木板内框，略留边以免贴边溢出。
        private const float PortraitFill = 0.98f;

        private JobItem _job;
        private bool _destroyed;
        private bool _unlocked;
        private Collider2D _minusHit;
        private Collider2D _plusHit;
        private Collider2D _boardHit;
        private Transform _clearButton;
        private Collider2D _clearHit;

        public SpriteRenderer Signboard => signboard;
        public SpriteRenderer Portrait => portrait;
        public int SlotIndex => slotIndex;
        public JobItem Job => _job;
        public bool IsDestroyed => _destroyed;
        public bool IsUnlocked => _unlocked && _job != null && !_destroyed;
        public EmptyStationSlot EmptySlot => emptySlot;

        public void ConfigureIndex(int index) => slotIndex = index;

        public void BindDestroyed()
        {
            _destroyed = true;
            _job = null;
            _unlocked = false;
            ApplyVisuals();
        }

        public void BindEmpty()
        {
            _destroyed = false;
            _job = null;
            _unlocked = false;
            ApplyVisuals();
        }

        public void BindJob(JobItem job)
        {
            _destroyed = false;
            _job = job;
            _unlocked = job != null;
            ApplyVisuals();
            RefreshCount();
        }

        public void SetAssignPadsVisible(bool visible)
        {
            bool show = visible && !_destroyed && (IsUnlocked || !Application.isPlaying);
            if (minusButton != null) minusButton.gameObject.SetActive(show);
            if (plusButton != null) plusButton.gameObject.SetActive(show);
            StationAssignClearPad.SetActive(_clearButton, show && IsUnlocked);
        }

        public void RefreshCount()
        {
            EnsureLabels();
            if (countMesh == null) return;
            if (_destroyed)
            {
                countMesh.text = string.Empty;
                return;
            }

            if (!IsUnlocked)
            {
                countMesh.text = Application.isPlaying ? "—" : "0";
            }
            else
            {
                var em = EmployeeManager.Instance;
                var assignType = EmployeeAssignSelection.Current;
                int assigned = em != null && assignType != null
                    ? em.GetAssigned(assignType, _job)
                    : 0;
                int capacity = em != null ? em.GetJobCapacity(_job) : _job.MaxWorkers;
                string cap = capacity == int.MaxValue ? "∞" : capacity.ToString();
                countMesh.text = $"{assigned}/{cap}";
            }

            LayoutCount();
        }

        public void HandleHit(Collider2D hit)
        {
            if (!IsUnlocked || hit == null) return;
            if (StationAssignClearPad.IsHit(hit, _clearButton, _clearHit))
            {
                TryClearAll();
                return;
            }

            if (hit == _plusHit || hit.transform == plusButton || hit.name.Contains("切换键右"))
                TryChange(+1);
            else if (hit == _minusHit || hit.transform == minusButton || hit.name.Contains("切换键左"))
                TryChange(-1);
        }

        public bool IsBoardHit(Collider2D hit)
        {
            if (hit == null) return false;
            if (hit == _boardHit) return true;
            return signboard != null && hit.transform.IsChildOf(signboard.transform);
        }

        private void TryClearAll()
        {
            var em = EmployeeManager.Instance;
            if (em == null || _job == null) return;
            if (!em.TryClearJobAssignments(_job)) return;
            RefreshCount();
            var zone = GetComponentInParent<GatherZoneView>();
            zone?.Refresh();
            FindObjectOfType<CookZoneView>()?.Refresh();
        }

        private void TryChange(int delta)
        {
            var em = EmployeeManager.Instance;
            var assignType = EmployeeAssignSelection.Current;
            if (em == null || assignType == null || _job == null) return;
            if (delta > 0)
                em.TryAssign(assignType, _job, 1);
            else
                em.TryUnassign(assignType, _job, 1);
            RefreshCount();
            var zone = GetComponentInParent<GatherZoneView>();
            zone?.Refresh();
            FindObjectOfType<CookZoneView>()?.Refresh();
        }

        private void ApplyVisuals()
        {
            EnsureColliders();
            EnsureLabels();

            bool showBoard = !_destroyed;
            if (signboard != null)
                signboard.gameObject.SetActive(showBoard);
            if (countFrame != null)
                countFrame.gameObject.SetActive(showBoard);
            if (countMesh != null)
                countMesh.gameObject.SetActive(showBoard);

            bool showPads = showBoard && (IsUnlocked || !Application.isPlaying) && !AdvancementVisit.IsActive;
            if (minusButton != null)
                minusButton.gameObject.SetActive(showPads);
            if (plusButton != null)
                plusButton.gameObject.SetActive(showPads);
            StationAssignClearPad.SetActive(_clearButton, showPads && IsUnlocked);
            LayoutAssignControls();

            if (portrait != null)
            {
                var visualJob = _job;
                if (visualJob != null && visualJob.JobType == JobType.Gather)
                {
                    var progression = JobProgressionManager.Instance;
                    if (progression != null)
                        visualJob = progression.ResolveGatherDefinition(visualJob);
                }

                bool showPortrait = showBoard && visualJob != null && visualJob.Icon != null;
                portrait.enabled = showPortrait;
                portrait.gameObject.SetActive(showBoard);
                if (showPortrait)
                {
                    portrait.sprite = visualJob.Icon;
                    FitPortrait();
                }
            }

            HideNameLabels();

            if (emptySlot != null)
            {
                emptySlot.Configure(JobType.Gather, slotIndex);
                var col = emptySlot.GetComponent<Collider2D>();
                if (col != null)
                    col.enabled = showBoard && _job == null;
            }

            if (_boardHit != null)
                _boardHit.enabled = showBoard;

            RefreshCount();
        }

        private void FitPortrait()
        {
            if (portrait == null || portrait.sprite == null || signboard == null) return;
            Vector2 art = GetPortraitContentSize(portrait.sprite);
            if (art.x < 0.0001f || art.y < 0.0001f) return;

            // Contain：短边撑满木板，长边不超出。
            float worldScale = Mathf.Min(
                (BoardSize.x * PortraitFill) / art.x,
                (BoardSize.y * PortraitFill) / art.y);
            worldScale *= JobIconLayout.ResolveStationIconScaleMultiplier(_job);
            var parentLossy = portrait.transform.parent != null
                ? portrait.transform.parent.lossyScale
                : Vector3.one;
            float sx = Mathf.Abs(parentLossy.x) > 0.0001f ? worldScale / parentLossy.x : worldScale;
            float sy = Mathf.Abs(parentLossy.y) > 0.0001f ? worldScale / parentLossy.y : worldScale;
            portrait.transform.localScale = new Vector3(sx, sy, 1f);
            portrait.transform.localPosition = BoardCenterLocal;
            portrait.sortingOrder = signboard.sortingOrder + 1;
        }

        /// <summary>
        /// 优先用 sprite mesh 顶点范围（紧贴不透明像素），避免透明留白导致图标看起来偏小。
        /// </summary>
        private static Vector2 GetPortraitContentSize(Sprite sprite)
        {
            if (sprite == null) return Vector2.zero;

            var verts = sprite.vertices;
            if (verts != null && verts.Length >= 3)
            {
                float minX = verts[0].x;
                float maxX = verts[0].x;
                float minY = verts[0].y;
                float maxY = verts[0].y;
                for (int i = 1; i < verts.Length; i++)
                {
                    Vector2 v = verts[i];
                    if (v.x < minX) minX = v.x;
                    if (v.x > maxX) maxX = v.x;
                    if (v.y < minY) minY = v.y;
                    if (v.y > maxY) maxY = v.y;
                }

                float w = maxX - minX;
                float h = maxY - minY;
                if (w > 0.0001f && h > 0.0001f)
                    return new Vector2(w, h);
            }

            return sprite.bounds.size;
        }

        private void HideNameLabels()
        {
            if (nameMesh != null)
            {
                nameMesh.text = string.Empty;
                nameMesh.gameObject.SetActive(false);
            }

            Transform board = signboard != null ? signboard.transform : transform;
            var stray = GatherHudText.FindDirect(board, "JobName");
            if (stray != null)
                stray.gameObject.SetActive(false);
        }

        private void EnsureColliders()
        {
            if (signboard != null && _boardHit == null)
            {
                _boardHit = signboard.GetComponent<Collider2D>();
                if (_boardHit == null)
                {
                    var box = signboard.gameObject.AddComponent<BoxCollider2D>();
                    box.isTrigger = true;
                    _boardHit = box;
                }

                if (emptySlot == null)
                    emptySlot = signboard.GetComponent<EmptyStationSlot>();
                if (emptySlot == null)
                    emptySlot = signboard.gameObject.AddComponent<EmptyStationSlot>();
                emptySlot.Configure(JobType.Gather, slotIndex);
            }

            _minusHit = EnsureButtonCollider(minusButton);
            _plusHit = EnsureButtonCollider(plusButton);
            int sorting = countFrame != null ? countFrame.sortingOrder + 4 : 32;
            _clearHit = StationAssignClearPad.Ensure(ref _clearButton, transform, sorting);
        }

        private void LayoutAssignControls()
        {
            if (!IsUnlocked || _destroyed) return;
            StationAssignClearPad.LayoutBelowControls(
                _clearButton, minusButton, plusButton, countFrame);
        }

        private static Collider2D EnsureButtonCollider(Transform button)
        {
            if (button == null) return null;
            var col = button.GetComponent<Collider2D>();
            if (col == null)
            {
                var box = button.gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                col = box;
            }

            return col;
        }

        private void EnsureLabels()
        {
            var strayCount = GatherHudText.FindDirect(transform, "Count");
            if (strayCount != null)
                GatherHudText.DestroyGo(strayCount.gameObject);

            if (countFrame != null)
            {
                int sorting = countFrame.sortingOrder + 3;
                var scale = GatherHudText.LocalScaleForWorld(countFrame.transform, 0.24f);
                countMesh = GatherHudText.Ensure(
                    countFrame.transform, "Count", new Vector3(0f, 0f, -0.02f), scale, sorting, 48);
                if (countMesh != null && string.IsNullOrEmpty(countMesh.text))
                    countMesh.text = "0";
                LayoutCount();
            }

            HideNameLabels();
        }

        private void LayoutCount()
        {
            if (countMesh == null || countFrame == null) return;
            countMesh.transform.localScale = GatherHudText.LocalScaleForWorld(countFrame.transform, 0.24f);
            GatherHudText.FitInside(countMesh, countFrame.bounds.size, 0.76f);
            GatherHudText.SnapCenter(countMesh, countFrame.bounds.center, 0.02f);
        }

        private void OnEnable()
        {
            EnsureColliders();
            EnsureLabels();
            if (!Application.isPlaying)
            {
                TryEditorPreview();
                if (_job == null)
                    ApplyVisuals();
                RefreshCount();
            }
        }

        private void TryEditorPreview()
        {
            if (Application.isPlaying || slotIndex != 0 || _job != null)
                return;

            var db = Resources.Load<JobDatabase>(JobManager.ResourcesDatabasePath);
            var job = db != null ? db.GetById("mushroom") : null;
            if (job == null && db != null)
                job = db.FindByName("蘑菇");
            if (job != null)
                BindJob(job);
            else
                ApplyVisuals();
        }
    }
}
