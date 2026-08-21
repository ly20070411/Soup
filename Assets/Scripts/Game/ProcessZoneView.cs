using Soup.Employees;
using Soup.Jobs;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Scene-authored process zone: two stations, shared warehouse HUD, job art presets.
    /// </summary>
    public sealed class ProcessZoneView : MonoBehaviour
    {
        public const int StationCount = 2;

        [SerializeField] private SpriteRenderer background;
        [SerializeField] private ProcessStationSlot[] stations = new ProcessStationSlot[StationCount];
        [SerializeField] private GatherWarehouseHud warehouse;
        [SerializeField] private SpriteRenderer warningTemplate;
        [SerializeField] private ProcessJobArtPreset[] jobArtPresets = new ProcessJobArtPreset[0];

        public ProcessStationSlot[] Stations => stations;
        public GatherWarehouseHud Warehouse => warehouse;
        public SpriteRenderer Background => background;

        public ProcessStationSlot GetStation(int index)
        {
            if (stations == null || index < 0 || index >= stations.Length)
                return null;
            return stations[index];
        }

        public ProcessJobArtPreset FindArtPreset(JobItem job)
        {
            if (job == null || jobArtPresets == null) return null;
            for (int i = 0; i < jobArtPresets.Length; i++)
            {
                var preset = jobArtPresets[i];
                if (preset != null && preset.Matches(job))
                    return preset;
            }

            return null;
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

        public float RecommendedOrthographicSize()
        {
            if (background == null) return 0f;
            float height = background.bounds.size.y;
            float width = background.bounds.size.x;
            if (height < 0.1f) return 0f;
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
            return width > 0.1f ? width : 0f;
        }

        private void Awake()
        {
            HideEditorTemplates();
        }

        private void HideEditorTemplates()
        {
            if (warningTemplate != null)
                warningTemplate.gameObject.SetActive(false);

            if (jobArtPresets == null) return;
            for (int i = 0; i < jobArtPresets.Length; i++)
            {
                var preset = jobArtPresets[i];
                if (preset?.Template != null)
                    preset.Template.gameObject.SetActive(false);
            }
        }
    }
}
