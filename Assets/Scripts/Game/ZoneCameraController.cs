using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Moves the main camera between gather / process / cook map zones.
    /// </summary>
    public class ZoneCameraController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float zoneSpacing = 22f;
        [SerializeField] private float cameraY;
        [SerializeField] private float cameraZ = -10f;
        [SerializeField] private float orthographicSize = 5.5f;
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float zoomSpeed = 8f;

        private MapZoneType _zone = MapZoneType.Gather;
        private Vector3 _targetPos;
        private float _targetOrthoSize;
        private readonly float[] _zoneOrthoSize = new float[3];
        private readonly float[] _zoneCenterY = new float[3];

        public MapZoneType CurrentZone => _zone;
        public float ZoneSpacing => zoneSpacing;
        public bool IsSliding
        {
            get
            {
                if (targetCamera == null) return false;
                float posDist = Vector3.Distance(targetCamera.transform.position, _targetPos);
                float zoomDist = Mathf.Abs(targetCamera.orthographicSize - _targetOrthoSize);
                return posDist > 0.05f || zoomDist > 0.05f;
            }
        }

        public void ConfigureView(float spacing, float size)
        {
            if (spacing > 1f)
                zoneSpacing = spacing;
            if (size > 0.5f)
            {
                orthographicSize = size;
                _targetOrthoSize = size;
                SetZoneOrtho(MapZoneType.Gather, size);
            }

            if (targetCamera != null)
            {
                targetCamera.orthographic = true;
                targetCamera.orthographicSize = orthographicSize;
            }
        }

        public void ConfigureZone(MapZoneType zone, float size, float centerY)
        {
            if (size > 0.5f)
                SetZoneOrtho(zone, size);
            SetZoneCenterY(zone, centerY);
            if (_zone == zone)
                ApplyZoneView(zone, snap: false);
        }

        public Vector3 GetZoneCenter(MapZoneType zone)
        {
            // Gather=0 sits one spacing left of origin so Process stays at x=0.
            float x = ((int)zone - 1) * zoneSpacing;
            return new Vector3(x, GetZoneCenterY(zone), 0f);
        }

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            for (int i = 0; i < _zoneOrthoSize.Length; i++)
                _zoneOrthoSize[i] = orthographicSize;
            _targetOrthoSize = orthographicSize;

            if (targetCamera != null)
            {
                targetCamera.orthographic = true;
                targetCamera.orthographicSize = orthographicSize;
            }

            SnapToZone(MapZoneType.Gather);
        }

        private void Update()
        {
            if (targetCamera == null) return;

            float dt = Time.unscaledDeltaTime;
            var cam = targetCamera.transform;
            cam.position = Vector3.Lerp(
                cam.position,
                _targetPos,
                1f - Mathf.Exp(-moveSpeed * dt));

            float nextSize = Mathf.Lerp(
                targetCamera.orthographicSize,
                _targetOrthoSize,
                1f - Mathf.Exp(-zoomSpeed * dt));
            targetCamera.orthographicSize = nextSize;
            orthographicSize = nextSize;
        }

        public void SetZone(MapZoneType zone)
        {
            _zone = zone;
            ApplyZoneView(zone, snap: false);
        }

        public void SnapToZone(MapZoneType zone)
        {
            _zone = zone;
            ApplyZoneView(zone, snap: true);
            if (targetCamera != null)
            {
                targetCamera.transform.position = _targetPos;
                targetCamera.orthographicSize = _targetOrthoSize;
                orthographicSize = _targetOrthoSize;
            }
        }

        public void CycleZone(int direction)
        {
            int next = (int)_zone + direction;
            if (next < 0 || next > 2)
                return;
            SetZone((MapZoneType)next);
        }

        private void ApplyZoneView(MapZoneType zone, bool snap)
        {
            float size = GetZoneOrtho(zone);
            if (size > 0.5f)
            {
                _targetOrthoSize = size;
                if (snap)
                    orthographicSize = size;
            }

            cameraY = GetZoneCenterY(zone);

            if (targetCamera != null)
                targetCamera.orthographic = true;

            var center = GetZoneCenter(zone);
            _targetPos = new Vector3(center.x, center.y, cameraZ);
        }

        private static int ZoneIndex(MapZoneType zone)
        {
            int index = (int)zone;
            return index >= 0 && index <= 2 ? index : 0;
        }

        private float GetZoneOrtho(MapZoneType zone) => _zoneOrthoSize[ZoneIndex(zone)];

        private void SetZoneOrtho(MapZoneType zone, float size) =>
            _zoneOrthoSize[ZoneIndex(zone)] = size;

        private float GetZoneCenterY(MapZoneType zone) => _zoneCenterY[ZoneIndex(zone)];

        private void SetZoneCenterY(MapZoneType zone, float y) =>
            _zoneCenterY[ZoneIndex(zone)] = y;
    }
}
