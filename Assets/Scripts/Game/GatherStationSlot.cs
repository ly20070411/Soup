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
        private const float PortraitFill = 0.88f;

        private JobItem _job;
        private bool _destroyed;
        private bool _unlocked;
        private Collider2D _minusHit;
        private Collider2D _plusHit;
        private Collider2D _boardHit;

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

            if (portrait != null)
            {
                bool showPortrait = showBoard && _job != null && _job.Icon != null;
                portrait.enabled = showPortrait;
                portrait.gameObject.SetActive(showBoard);
                if (showPortrait)
                {
                    portrait.sprite = _job.Icon;
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
            Vector2 art = portrait.sprite.bounds.size;
            if (art.x < 0.0001f || art.y < 0.0001f) return;

            float worldScale = Mathf.Min(
                (BoardSize.x * PortraitFill) / art.x,
                (BoardSize.y * PortraitFill) / art.y);
            var parentLossy = portrait.transform.parent != null
                ? portrait.transform.parent.lossyScale
                : Vector3.one;
            float sx = Mathf.Abs(parentLossy.x) > 0.0001f ? worldScale / parentLossy.x : worldScale;
            float sy = Mathf.Abs(parentLossy.y) > 0.0001f ? worldScale / parentLossy.y : worldScale;
            portrait.transform.localScale = new Vector3(sx, sy, 1f);
            portrait.transform.localPosition = BoardCenterLocal;
            portrait.sortingOrder = signboard.sortingOrder + 1;
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
