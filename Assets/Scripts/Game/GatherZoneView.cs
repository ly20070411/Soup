using Soup.Employees;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Scene-authored gather zone: four stations + warehouse + zone switches.
    /// </summary>
    public sealed class GatherZoneView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private GatherStationSlot[] stations = new GatherStationSlot[4];
        [SerializeField] private GatherWarehouseHud warehouse;
        [SerializeField] private Collider2D zonePrevButton;
        [SerializeField] private Collider2D zoneNextButton;

        public GatherStationSlot[] Stations => stations;
        public GatherWarehouseHud Warehouse => warehouse;
        public SpriteRenderer Background => background;

        public void SetWorldSwitchVisible(bool visible)
        {
            // Gather is the leftmost zone — never show a left dead-end switch.
            if (zonePrevButton != null)
                zonePrevButton.gameObject.SetActive(false);
            if (zoneNextButton != null)
                zoneNextButton.gameObject.SetActive(visible);
        }

        public GatherStationSlot GetStation(int index)
        {
            if (stations == null || index < 0 || index >= stations.Length)
                return null;
            return stations[index];
        }

        public void Refresh()
        {
            if (stations != null)
            {
                for (int i = 0; i < stations.Length; i++)
                    stations[i]?.RefreshCount();
            }

            warehouse?.Refresh();
        }

        private void OnEnable()
        {
            var em = EmployeeManager.Instance;
            if (em != null)
                em.Changed += HandleAssignmentsChanged;
        }

        private void OnDisable()
        {
            var em = EmployeeManager.Instance;
            if (em != null)
                em.Changed -= HandleAssignmentsChanged;
        }

        private void HandleAssignmentsChanged() => Refresh();

        public bool TryHandleZoneSwitch(Collider2D hit)
        {
            if (hit == null) return false;

            if (IsHit(zonePrevButton, hit))
            {
                var cam = FindObjectOfType<ZoneCameraController>();
                cam?.CycleZone(-1);
                return true;
            }

            if (IsHit(zoneNextButton, hit))
            {
                var cam = FindObjectOfType<ZoneCameraController>();
                cam?.CycleZone(+1);
                return true;
            }

            return false;
        }

        private static bool IsHit(Collider2D button, Collider2D hit)
        {
            if (button == null || hit == null) return false;
            if (hit == button) return true;
            return hit.transform == button.transform || hit.transform.IsChildOf(button.transform);
        }

        public float RecommendedOrthographicSize()
        {
            if (background == null) return 0f;
            float height = background.bounds.size.y;
            float width = background.bounds.size.x;
            if (height < 0.1f) return 0f;

            // Height fit, but never crop the zone edges — half the divider sits on the
            // shared boundary and must stay inside the camera view.
            float fromHeight = height * 0.5f;
            float aspect = Camera.main != null ? Mathf.Max(0.1f, Camera.main.aspect) : (16f / 9f);
            float fromWidth = (width * 0.5f) / aspect;
            return Mathf.Max(fromHeight, fromWidth);
        }

        public float RecommendedCameraCenterY()
        {
            return background != null ? background.bounds.center.y : 0f;
        }

        public float RecommendedZoneSpacing()
        {
            if (background == null) return 0f;
            float width = background.bounds.size.x;
            // Edge-to-edge abut; divider is centered on the shared boundary.
            return width > 0.1f ? width : 0f;
        }

        /// <summary>World-space width of the vertical divider (centered on the zone edge).</summary>
        public const float DividerWorldWidth = 0.55f;

        /// <summary>Kept for call sites; zones abut with no gap.</summary>
        public const float ZoneSeam = 0f;
    }
}
