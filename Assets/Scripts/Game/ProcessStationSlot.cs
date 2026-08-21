using Soup.Employees;
using Soup.Jobs;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// One process station: warning sign when empty, preset job art when unlocked, +/- keys.
    /// </summary>
    [ExecuteAlways]
    public sealed class ProcessStationSlot : MonoBehaviour
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private SpriteRenderer warningSign;
        [SerializeField] private SpriteRenderer portrait;
        [SerializeField] private SpriteRenderer countFrame;
        [SerializeField] private TextMesh countMesh;
        [SerializeField] private Transform minusButton;
        [SerializeField] private Transform plusButton;
        [SerializeField] private EmptyStationSlot emptySlot;

        private JobItem _job;
        private bool _unlocked;
        private Collider2D _minusHit;
        private Collider2D _plusHit;
        private Collider2D _warningHit;
        private ProcessZoneView _zone;

        public int SlotIndex => slotIndex;
        public JobItem Job => _job;
        public bool IsUnlocked => _unlocked && _job != null;
        public EmptyStationSlot EmptySlot => emptySlot;

        public void ConfigureIndex(int index) => slotIndex = index;

        public void BindEmpty()
        {
            _job = null;
            _unlocked = false;
            ApplyVisuals();
        }

        public void BindJob(JobItem job)
        {
            _job = job;
            _unlocked = job != null;
            ApplyVisuals();
            RefreshCount();
        }

        public void SetAssignPadsVisible(bool visible)
        {
            bool show = visible && IsUnlocked && !AdvancementVisit.IsActive;
            if (minusButton != null) minusButton.gameObject.SetActive(show);
            if (plusButton != null) plusButton.gameObject.SetActive(show);
            if (countFrame != null) countFrame.gameObject.SetActive(IsUnlocked);
            if (countMesh != null) countMesh.gameObject.SetActive(IsUnlocked);
        }

        public void RefreshCount()
        {
            EnsureLabels();
            if (countMesh == null) return;

            if (!IsUnlocked)
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
            string cap = capacity == int.MaxValue ? "∞" : capacity.ToString();
            countMesh.text = $"{assigned}/{cap}";
        }

        public void HandleHit(Collider2D hit)
        {
            if (!IsUnlocked) return;
            if (TryAssignFromPoint())
                return;

            if (hit == null) return;
            if (hit == _plusHit || hit.transform == plusButton || hit.name.Contains("切换键右") || hit.name.Contains("Plus"))
                TryChange(+1);
            else if (hit == _minusHit || hit.transform == minusButton || hit.name.Contains("切换键左") || hit.name.Contains("Minus"))
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
                if (h == _plusHit || h.transform == plusButton || h.name.Contains("切换键右") || h.name.Contains("Plus"))
                {
                    TryChange(+1);
                    return true;
                }

                if (h == _minusHit || h.transform == minusButton || h.name.Contains("切换键左") || h.name.Contains("Minus"))
                {
                    TryChange(-1);
                    return true;
                }
            }

            return false;
        }

        public bool IsWarningHit(Collider2D hit)
        {
            if (hit == null) return false;
            if (hit == _warningHit) return true;
            return warningSign != null && hit.transform.IsChildOf(warningSign.transform);
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

        private ProcessZoneView ResolveZone()
        {
            if (_zone == null)
                _zone = GetComponentInParent<ProcessZoneView>();
            return _zone;
        }

        private void ApplyVisuals()
        {
            EnsureColliders();
            EnsureLabels();
            ResolveZone();

            bool showEmpty = _job == null;
            if (warningSign != null)
            {
                warningSign.gameObject.SetActive(showEmpty);
                warningSign.enabled = showEmpty;
            }

            if (portrait != null)
            {
                bool showPortrait = !showEmpty;
                portrait.gameObject.SetActive(showPortrait);
                portrait.enabled = showPortrait;
                if (showPortrait)
                    ApplyJobPortrait();
                EnsurePortraitCollider(showPortrait);
            }

            bool showPads = IsUnlocked || !Application.isPlaying;
            showPads &= !AdvancementVisit.IsActive;
            if (minusButton != null)
                minusButton.gameObject.SetActive(showPads && IsUnlocked);
            if (plusButton != null)
                plusButton.gameObject.SetActive(showPads && IsUnlocked);
            if (countFrame != null)
                countFrame.gameObject.SetActive(IsUnlocked);
            if (countMesh != null)
                countMesh.gameObject.SetActive(IsUnlocked);

            LayoutAssignControls();

            if (emptySlot != null)
            {
                emptySlot.Configure(JobType.Process, slotIndex);
                var col = emptySlot.GetComponent<Collider2D>();
                if (col == null && warningSign != null)
                    col = warningSign.GetComponent<Collider2D>();
                if (col != null)
                    col.enabled = showEmpty;
            }

            if (_warningHit != null)
                _warningHit.enabled = showEmpty;

            RefreshCount();
        }

        private void ApplyJobPortrait()
        {
            if (portrait == null || _job == null) return;

            // Presets may sit off-screen as drawing references; only their art/size
            // is used. The unlocked job always replaces the warning at this station.
            var preset = ResolveZone()?.FindArtPreset(_job);
            if (preset != null && preset.Template != null)
            {
                var template = preset.Template;
                portrait.sprite = template.sprite;
                portrait.flipX = template.flipX;
                portrait.flipY = template.flipY;
                portrait.sortingOrder = Mathf.Max(2, template.sortingOrder + 1);
                portrait.color = template.color;

                portrait.transform.localPosition = Vector3.zero;
                portrait.transform.localRotation = Quaternion.identity;
                CopyLossyScale(portrait.transform, template.transform);
                return;
            }

            if (_job.Icon != null)
            {
                portrait.sprite = _job.Icon;
                portrait.transform.localPosition = Vector3.zero;
                portrait.transform.localRotation = Quaternion.identity;
                portrait.transform.localScale = Vector3.one;
                portrait.color = Color.white;
            }
        }

        private void LayoutAssignControls()
        {
            if (!IsUnlocked || portrait == null || !portrait.enabled)
                return;

            // Sit centered on the station, snug just above the job art.
            float centerX = transform.position.x;
            float topY = portrait.bounds.max.y;
            float z = transform.position.z;
            float frameHalfH = countFrame != null && countFrame.sprite != null
                ? countFrame.bounds.extents.y
                : 0.46f;
            float boardY = topY + frameHalfH + 0.18f;

            if (countFrame != null)
                countFrame.transform.position = new Vector3(centerX, boardY, z - 0.01f);

            float halfW = countFrame != null ? countFrame.bounds.extents.x + 0.5f : 1.4f;
            if (minusButton != null)
                minusButton.position = new Vector3(centerX - halfW, boardY, z);
            if (plusButton != null)
                plusButton.position = new Vector3(centerX + halfW, boardY, z);

            if (countMesh != null && countFrame != null)
            {
                countMesh.transform.SetParent(countFrame.transform, false);
                countMesh.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            }
        }

        private static void CopyLossyScale(Transform target, Transform source)
        {
            if (target == null || source == null) return;
            Vector3 src = source.lossyScale;
            Transform parent = target.parent;
            if (parent == null)
            {
                target.localScale = src;
                return;
            }

            Vector3 p = parent.lossyScale;
            target.localScale = new Vector3(
                p.x > 0.0001f ? src.x / p.x : src.x,
                p.y > 0.0001f ? src.y / p.y : src.y,
                p.z > 0.0001f ? src.z / p.z : src.z);
        }

        private void EnsureColliders()
        {
            if (warningSign != null && _warningHit == null)
            {
                _warningHit = warningSign.GetComponent<Collider2D>();
                if (_warningHit == null)
                {
                    var box = warningSign.gameObject.AddComponent<BoxCollider2D>();
                    box.isTrigger = true;
                    _warningHit = box;
                }

                if (emptySlot == null)
                    emptySlot = warningSign.GetComponent<EmptyStationSlot>();
                if (emptySlot == null)
                    emptySlot = warningSign.gameObject.AddComponent<EmptyStationSlot>();
                emptySlot.Configure(JobType.Process, slotIndex);
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

        private void EnsurePortraitCollider(bool enabled)
        {
            if (portrait == null) return;
            var col = portrait.GetComponent<Collider2D>();
            if (col == null)
            {
                var box = portrait.gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                col = box;
            }

            col.enabled = enabled;
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
            if (!Application.isPlaying)
                ApplyVisuals();
        }
    }
}
