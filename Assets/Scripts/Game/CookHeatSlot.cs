using Soup.Employees;
using Soup.Jobs;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// One cook heat station: flame art is on the backdrop; this hosts +/- and count.
    /// </summary>
    [ExecuteAlways]
    public sealed class CookHeatSlot : MonoBehaviour
    {
        [SerializeField] private string jobId;
        [SerializeField] private SpriteRenderer countFrame;
        [SerializeField] private TextMesh countMesh;
        [SerializeField] private Transform minusButton;
        [SerializeField] private Transform plusButton;

        private JobItem _job;
        private Collider2D _minusHit;
        private Collider2D _plusHit;
        private Collider2D _hoverHit;
        private Transform _clearButton;
        private Collider2D _clearHit;
        private CookZoneView _zone;

        public string JobId => jobId;
        public JobItem Job => _job;
        public bool IsBound => _job != null;

        public void Configure(string id) => jobId = id;

        public void BindJob(JobItem job)
        {
            _job = job;
            EnsureColliders();
            EnsureLabels();
            SetAssignPadsVisible(!AdvancementVisit.IsActive);
            LayoutAssignControls();
            RefreshCount();
        }

        public void SetAssignPadsVisible(bool visible)
        {
            bool show = visible && IsBound && !AdvancementVisit.IsActive;
            if (minusButton != null) minusButton.gameObject.SetActive(show);
            if (plusButton != null) plusButton.gameObject.SetActive(show);
            StationAssignClearPad.SetActive(_clearButton, show);
            if (countFrame != null) countFrame.gameObject.SetActive(IsBound);
            if (countMesh != null) countMesh.gameObject.SetActive(IsBound);
        }

        public void RefreshCount()
        {
            EnsureLabels();
            if (countMesh == null) return;

            if (!IsBound)
            {
                countMesh.text = Application.isPlaying ? "—" : "0";
                return;
            }

            var em = EmployeeManager.Instance;
            var assignType = EmployeeAssignSelection.Current;
            int assigned = em != null && assignType != null
                ? em.GetAssigned(assignType, _job)
                : 0;
            int capacity = em != null ? em.GetJobCapacity(_job) : _job.MaxWorkers;
            string cap = capacity == int.MaxValue || capacity <= 0 ? "∞" : capacity.ToString();
            countMesh.text = $"{assigned}/{cap}";
        }

        public void HandleHit(Collider2D hit)
        {
            if (!IsBound) return;
            if (TryAssignFromPoint())
                return;

            if (hit == null) return;
            if (StationAssignClearPad.IsHit(hit, _clearButton, _clearHit))
            {
                TryClearAll();
                return;
            }

            if (hit == _plusHit || hit.transform == plusButton || hit.name.Contains("Plus"))
                TryChange(+1);
            else if (hit == _minusHit || hit.transform == minusButton || hit.name.Contains("Minus"))
                TryChange(-1);
        }

        private bool TryAssignFromPoint()
        {
            if (Camera.main == null) return false;
            Vector2 p = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var hits = Physics2D.OverlapPointAll(p);
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null) continue;
                if (h == _plusHit || h.transform == plusButton || h.name.Contains("Plus"))
                {
                    TryChange(+1);
                    return true;
                }

                if (h == _minusHit || h.transform == minusButton || h.name.Contains("Minus"))
                {
                    TryChange(-1);
                    return true;
                }

                if (StationAssignClearPad.IsHit(h, _clearButton, _clearHit))
                {
                    TryClearAll();
                    return true;
                }
            }

            return false;
        }

        private void TryClearAll()
        {
            var em = EmployeeManager.Instance;
            if (em == null || _job == null) return;
            if (!em.TryClearJobAssignments(_job)) return;
            RefreshCount();
            ResolveZone()?.Refresh();
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
            ResolveZone()?.Refresh();
        }

        private CookZoneView ResolveZone()
        {
            if (_zone == null)
                _zone = GetComponentInParent<CookZoneView>();
            return _zone;
        }

        private void EnsureColliders()
        {
            _minusHit = EnsureButtonCollider(minusButton);
            _plusHit = EnsureButtonCollider(plusButton);
            _hoverHit = EnsureHoverCollider();
            int sorting = countFrame != null ? countFrame.sortingOrder + 4 : 32;
            _clearHit = StationAssignClearPad.Ensure(ref _clearButton, transform, sorting);
        }

        private void LayoutAssignControls()
        {
            if (!IsBound) return;
            StationAssignClearPad.LayoutBelowControls(
                _clearButton, minusButton, plusButton, countFrame);
        }

        private Collider2D EnsureHoverCollider()
        {
            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                // Flame sits on backdrop at this slot's origin; cover the painted flame area.
                box.size = new Vector2(2.4f, 2.8f);
                box.offset = new Vector2(0f, 0.4f);
                col = box;
            }

            return col;
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
            if (countFrame == null)
            {
                var frameTf = transform.Find("CountFrame");
                if (frameTf != null)
                    countFrame = frameTf.GetComponent<SpriteRenderer>();
            }

            if (countFrame == null) return;

            // Prefer existing Count (keeps user-tuned font/scale); only create once.
            if (countMesh == null)
            {
                var underFrame = GatherHudText.FindDirect(countFrame.transform, "Count");
                if (underFrame != null)
                    countMesh = underFrame.GetComponent<TextMesh>();
            }

            var stray = GatherHudText.FindDirect(transform, "Count");
            if (stray != null && (countMesh == null || stray != countMesh.transform))
            {
                if (countMesh == null)
                {
                    stray.SetParent(countFrame.transform, true);
                    countMesh = stray.GetComponent<TextMesh>();
                }
                else
                    GatherHudText.DestroyGo(stray.gameObject);
            }

            if (countMesh == null)
            {
                int sorting = countFrame.sortingOrder + 3;
                countMesh = GatherHudText.Ensure(
                    countFrame.transform,
                    "Count",
                    new Vector3(0f, 0f, -0.02f),
                    GatherHudText.LocalScaleForWorld(countFrame.transform, 0.22f),
                    sorting,
                    42);
            }

            if (countMesh != null)
            {
                GatherHudText.ApplyFont(countMesh, 42);
                countMesh.anchor = TextAnchor.MiddleCenter;
                countMesh.alignment = TextAlignment.Center;
                if (string.IsNullOrEmpty(countMesh.text))
                    countMesh.text = "0";
            }
        }

        private void OnEnable()
        {
            EnsureColliders();
            EnsureLabels();
            LayoutAssignControls();
        }
    }
}
