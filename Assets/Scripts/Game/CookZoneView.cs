using Soup.Employees;
using Soup.Jobs;
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

        public SpriteRenderer Background => background;
        public Transform ArtRoot => artRoot != null ? artRoot : transform;
        public CookHeatSlot[] HeatStations => heatStations;
        public CookScoreHud ScoreHud => scoreHud;

        public void SetBackground(SpriteRenderer value) => background = value;
        public void SetArtRoot(Transform value) => artRoot = value;
        public void SetScoreHud(CookScoreHud value) => scoreHud = value;
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
            if (heatStations != null)
            {
                for (int i = 0; i < heatStations.Length; i++)
                    heatStations[i]?.RefreshCount();
            }

            scoreHud?.Refresh();
        }

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

        private void OnEnable()
        {
            var em = EmployeeManager.Instance;
            if (em != null)
                em.Changed += HandleChanged;
        }

        private void OnDisable()
        {
            var em = EmployeeManager.Instance;
            if (em != null)
                em.Changed -= HandleChanged;
        }

        private void HandleChanged() => Refresh();
    }
}
