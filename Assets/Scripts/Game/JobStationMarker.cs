using Soup.Employees;
using Soup.Jobs;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Soup.Game
{
    /// <summary>
    /// World marker for one job station. Locked = gray/inert; unlocked = lit with +/−.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class JobStationMarker : MonoBehaviour
    {
        private const float IconLocalSize = 1f;

        [SerializeField] private SpriteRenderer body;
        [SerializeField] private TextMesh nameMesh;
        [SerializeField] private TextMesh countMesh;
        [SerializeField] private TextMesh efficiencyMesh;

        private JobItem _job;
        private Color _litColor = Color.white;
        private bool _unlocked;
        private GameObject _plusGo;
        private GameObject _minusGo;
        private Transform _clearButton;
        private Collider2D _plusHit;
        private Collider2D _minusHit;
        private Collider2D _clearHit;
        private SpriteRenderer _plusSr;
        private SpriteRenderer _minusSr;
        private SpriteRenderer _iconSr;
        private static Font _font;
        private static Sprite _circleSprite;

        public JobItem Job => _job;
        public bool IsUnlocked => _unlocked;

        public void Setup(JobItem job, Color litColor)
        {
            _job = job;
            _litColor = litColor;
            EnsureVisuals();
            ApplyJobIcon();
            SetUnlocked(false, litColor, new Color(0.38f, 0.38f, 0.40f, 0.85f));
            RefreshLabel();
        }

        public void SetUnlocked(bool unlocked, Color litColor, Color lockedColor)
        {
            _unlocked = unlocked;
            _litColor = litColor;
            EnsureVisuals();

            if (body != null)
                body.color = unlocked ? litColor : lockedColor;
            if (_iconSr != null)
                _iconSr.color = unlocked ? litColor : lockedColor;

            if (nameMesh != null)
                nameMesh.color = unlocked ? Color.white : new Color(0.75f, 0.75f, 0.75f, 0.85f);
            if (countMesh != null)
            {
                countMesh.gameObject.SetActive(unlocked);
                countMesh.color = Color.white;
            }

            SetPadActive(_plusGo, _plusHit, _plusSr, unlocked);
            SetPadActive(_minusGo, _minusHit, _minusSr, unlocked);
            StationAssignClearPad.SetActive(_clearButton, unlocked);
            RefreshLabel();
        }

        /// <summary>进阶巡视等模式可临时关掉精灵分配按钮。</summary>
        public void SetAssignPadsVisible(bool visible)
        {
            EnsureVisuals();
            bool show = visible && _unlocked;
            SetPadActive(_plusGo, _plusHit, _plusSr, show);
            SetPadActive(_minusGo, _minusHit, _minusSr, show);
            StationAssignClearPad.SetActive(_clearButton, show);
        }

        public void RefreshLabel()
        {
            if (_job == null) return;

            var em = EmployeeManager.Instance;
            var assignType = EmployeeAssignSelection.Current;
            var progression = JobProgressionManager.Instance;
            int assigned = em != null && assignType != null
                ? em.GetAssigned(assignType, _job)
                : 0;
            int capacity = em != null ? em.GetJobCapacity(_job) : _job.MaxWorkers;
            string cap = capacity == int.MaxValue ? "∞" : capacity.ToString();
            int level = progression != null ? progression.GetUpgradeLevel(_job) : 0;
            var path = progression != null ? progression.GetAdvancePath(_job) : JobAdvanceNodeId.None;
            string levelTag = path != JobAdvanceNodeId.None
                ? $" [{JobAdvancePath.ToLabel(path)}]"
                : (level > 0 ? $" Lv{level}" : string.Empty);
            string lockTag = _unlocked ? string.Empty : "（未解锁）";

            if (nameMesh != null)
            {
                string numberTag = string.Empty;
                if (_job.JobType == JobType.Gather)
                {
                    int number = TurnManager.GetGatherJobNumber(_job);
                    if (number > 0)
                        numberTag = $"#{number} ";
                }

                nameMesh.text = numberTag + _job.DisplayName + levelTag + lockTag;
            }
            if (countMesh != null)
            {
                if (!_unlocked)
                {
                    countMesh.text = string.Empty;
                }
                else
                {
                    countMesh.text = $"{assigned}/{cap}";
                }
            }

            RefreshEfficiencyLabel();
        }

        private void RefreshEfficiencyLabel()
        {
            if (efficiencyMesh == null) return;

            if (!_unlocked || _job == null)
            {
                efficiencyMesh.text = string.Empty;
                efficiencyMesh.gameObject.SetActive(false);
                return;
            }

            float mult = WorkEfficiencyResolver.ResolveStationDisplayMultiplier(_job);
            bool show = mult > 0f && Mathf.Abs(mult - 1f) > 0.01f;
            efficiencyMesh.gameObject.SetActive(show);
            if (!show)
            {
                efficiencyMesh.text = string.Empty;
                return;
            }

            efficiencyMesh.text = $"×{mult:0.##}";
        }

        public void HandleHit(Collider2D hit)
        {
            if (!_unlocked || hit == null) return;
            if (StationAssignClearPad.IsHit(hit, _clearButton, _clearHit))
            {
                TryClearAll();
                return;
            }

            if (hit == _plusHit || hit.name == "Plus")
                TryChange(+1);
            else if (hit == _minusHit || hit.name == "Minus")
                TryChange(-1);
        }

        private void TryClearAll()
        {
            if (!_unlocked) return;
            var em = EmployeeManager.Instance;
            if (em == null || _job == null) return;
            if (!em.TryClearJobAssignments(_job)) return;

            RefreshLabel();
            var zone = FindObjectOfType<GatherZoneView>();
            zone?.Refresh();
            var process = FindObjectOfType<ProcessZoneView>();
            process?.Refresh();
            var cook = FindObjectOfType<CookZoneView>();
            cook?.Refresh();
        }

        private void TryChange(int delta)
        {
            if (!_unlocked) return;
            var em = EmployeeManager.Instance;
            var assignType = EmployeeAssignSelection.Current;
            if (em == null || assignType == null || _job == null) return;

            if (delta > 0)
                em.TryAssign(assignType, _job, 1);
            else
                em.TryUnassign(assignType, _job, 1);

            RefreshLabel();
            var zone = FindObjectOfType<GatherZoneView>();
            zone?.Refresh();
            var process = FindObjectOfType<ProcessZoneView>();
            process?.Refresh();
            var cook = FindObjectOfType<CookZoneView>();
            cook?.Refresh();
        }

        private void EnsureVisuals()
        {
            var col = gameObject.GetComponent<CircleCollider2D>();
            if (col == null)
                col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.55f;

            if (body == null)
            {
                body = gameObject.GetComponent<SpriteRenderer>();
                if (body == null)
                    body = gameObject.AddComponent<SpriteRenderer>();
                body.sprite = GetCircleSprite();
                body.sortingOrder = 2;
            }

            if (nameMesh == null)
                nameMesh = CreateTextChild("Name", new Vector3(0f, 0.95f, 0f), 28, TextAnchor.LowerCenter);
            if (countMesh == null)
                countMesh = CreateTextChild("Count", new Vector3(0f, -0.95f, 0f), 32, TextAnchor.UpperCenter);
            if (efficiencyMesh == null)
            {
                efficiencyMesh = CreateTextChild(
                    "Efficiency",
                    new Vector3(1.05f, 0.12f, 0f),
                    22,
                    TextAnchor.MiddleLeft);
                efficiencyMesh.color = new Color(0.92f, 0.96f, 1f, 0.95f);
            }

            if (_minusGo == null)
            {
                _minusHit = CreatePad("Minus", new Vector3(-0.85f, 0f, 0f), new Color(0.75f, 0.30f, 0.30f), "-",
                    out _minusGo, out _minusSr);
            }

            if (_plusGo == null)
            {
                _plusHit = CreatePad("Plus", new Vector3(0.85f, 0f, 0f), new Color(0.30f, 0.70f, 0.40f), "+",
                    out _plusGo, out _plusSr);
            }

            if (_clearButton == null)
            {
                _clearHit = StationAssignClearPad.Ensure(ref _clearButton, transform, 5);
                StationAssignClearPad.LayoutLocalBelow(_clearButton, -1.35f);
            }
        }

        private void ApplyJobIcon()
        {
            if (_job == null) return;
            EnsureVisuals();

            if (_job.Icon != null)
            {
                EnsureIconRenderer();
                _iconSr.sprite = _job.Icon;
                _iconSr.enabled = true;
                FitSpriteRenderer(_iconSr, IconLocalSize * JobIconLayout.ResolveStationIconScaleMultiplier(_job));
                if (body != null)
                    body.enabled = false;
            }
            else
            {
                if (_iconSr != null)
                    _iconSr.enabled = false;
                if (body != null)
                {
                    body.enabled = true;
                    body.sprite = GetCircleSprite();
                }
            }
        }

        private void EnsureIconRenderer()
        {
            if (_iconSr != null) return;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(transform, false);
            iconGo.transform.localPosition = Vector3.zero;
            iconGo.transform.localScale = Vector3.one;
            _iconSr = iconGo.AddComponent<SpriteRenderer>();
            _iconSr.sortingOrder = body != null ? body.sortingOrder : 2;
        }

        private static void FitSpriteRenderer(SpriteRenderer sr, float targetLocalSize)
        {
            if (sr == null || sr.sprite == null) return;
            Vector2 size = sr.sprite.bounds.size;
            float max = Mathf.Max(size.x, size.y);
            if (max < 0.0001f)
            {
                sr.transform.localScale = Vector3.one;
                return;
            }

            float scale = targetLocalSize / max;
            sr.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private static void SetPadActive(GameObject go, Collider2D hit, SpriteRenderer sr, bool active)
        {
            if (go != null)
                go.SetActive(active);
            if (hit != null)
                hit.enabled = active;
            if (sr != null)
                sr.enabled = active;
        }

        private Collider2D CreatePad(
            string childName,
            Vector3 localPos,
            Color color,
            string glyph,
            out GameObject go,
            out SpriteRenderer sr)
        {
            go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.55f;

            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = color;
            sr.sortingOrder = 3;

            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = 0.55f;

            var labelGo = new GameObject(childName + "Glyph");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = Vector3.zero;
            labelGo.transform.localScale = Vector3.one * 0.14f;
            var mesh = labelGo.AddComponent<TextMesh>();
            mesh.font = GetDefaultFont();
            mesh.fontSize = 48;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            mesh.characterSize = 0.5f;
            mesh.text = glyph;
            var mr = labelGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 6;

            return circle;
        }

        private TextMesh CreateTextChild(string childName, Vector3 localPos, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.08f;

            var mesh = go.AddComponent<TextMesh>();
            mesh.font = GetDefaultFont();
            mesh.fontSize = fontSize;
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            mesh.characterSize = 0.5f;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sortingOrder = 5;

            return mesh;
        }

        private static Font GetDefaultFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_font == null)
                _font = SafeUiFont.Get(32);
            return _font;
        }

        private static Sprite GetCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f - 1f;
            var center = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float a = Mathf.Clamp01((r - d) * 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
            return _circleSprite;
        }

        public static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
