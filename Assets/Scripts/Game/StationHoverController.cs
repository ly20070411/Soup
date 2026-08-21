using Soup.Jobs;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Soup.Game
{
    /// <summary>
    /// World-space hover for gather / process / cook station art.
    /// </summary>
    public sealed class StationHoverController : MonoBehaviour
    {
        private JobItem _current;
        private bool _showing;

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (Camera.main == null) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                Clear();
                return;
            }

            Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var hits = Physics2D.OverlapPointAll(new Vector2(world.x, world.y));
            if (hits == null || hits.Length == 0)
            {
                Clear();
                return;
            }

            JobItem job = null;
            for (int i = 0; i < hits.Length; i++)
            {
                if (TryResolveJob(hits[i], out job) && job != null)
                    break;
            }

            if (job == null)
            {
                Clear();
                return;
            }

            if (_showing && ReferenceEquals(_current, job))
            {
                if (HoverTooltipHub.HasInstance)
                    HoverTooltipHub.Instance.MoveToScreen(Input.mousePosition);
                return;
            }

            HoverTooltipText.JobStation(job, out string title, out string body);
            var hub = HoverTooltipHub.Instance;
            if (hub == null) return;
            hub.ShowAtScreen(title, body, Input.mousePosition);
            _current = job;
            _showing = true;
        }

        private void OnDisable() => Clear();

        private void Clear()
        {
            if (!_showing) return;
            _showing = false;
            _current = null;
            HoverTooltipHub.HideIfPresent();
        }

        private static bool TryResolveJob(Collider2D hit, out JobItem job)
        {
            job = null;
            if (hit == null) return false;

            var gather = hit.GetComponentInParent<GatherStationSlot>();
            if (gather != null && gather.IsUnlocked && gather.IsBoardHit(hit))
            {
                job = gather.Job;
                return job != null;
            }

            var process = hit.GetComponentInParent<ProcessStationSlot>();
            if (process != null && process.IsUnlocked)
            {
                // Portrait / station body / assign pads all count.
                job = process.Job;
                return job != null;
            }

            var cook = hit.GetComponentInParent<CookHeatSlot>();
            if (cook != null && cook.IsBound)
            {
                job = cook.Job;
                return job != null;
            }

            var marker = hit.GetComponentInParent<JobStationMarker>();
            if (marker != null && marker.Job != null)
            {
                job = marker.Job;
                return true;
            }

            return false;
        }
    }
}
