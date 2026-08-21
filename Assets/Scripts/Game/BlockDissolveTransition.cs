using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// Scene wipe: brief gray hold, then the outgoing frame is split into square tiles
    /// that vanish in random order to reveal the next screen.
    /// </summary>
    public static class BlockDissolveTransition
    {
        public const float DefaultGrayHold = 0.18f;
        public const float DefaultDissolveDuration = 0.85f;
        public const int DefaultColumns = 14;
        private const int OverlaySortOrder = 20000;

        private static bool _busy;
        private static GameObject _activeRoot;

        public static bool IsBusy => _busy;

        /// <summary>
        /// Invisible busy lock matching the build's dissolve IsBusy window —
        /// blocks GoToInterLevel during InterLevel→Play advancement loads without FX.
        /// </summary>
        public static void HoldBusyForFrames(int frames = 4)
        {
            if (!Application.isPlaying)
            {
                _busy = false;
                return;
            }

            ForceReset();
            _busy = true;
            var go = new GameObject("BlockDissolveBusyHold");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _activeRoot = go;
            var hold = go.AddComponent<BusyHoldRunner>();
            hold.Begin(Mathf.Max(1, frames));
        }

        /// <summary>
        /// Clear stuck busy flag AND destroy any leftover DDOL dissolve overlay
        /// (full-screen raycast canvas would otherwise block all clicks).
        /// </summary>
        public static void ForceReset()
        {
            _busy = false;
            if (_activeRoot != null)
            {
                UnityEngine.Object.Destroy(_activeRoot);
                _activeRoot = null;
            }

            if (!Application.isPlaying) return;

            var roots = UnityEngine.Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < roots.Length; i++)
            {
                var t = roots[i];
                if (t == null || t.parent != null) continue;
                if (t.name != nameof(BlockDissolveTransition) && t.name != "BlockDissolveBusyHold")
                    continue;
                UnityEngine.Object.Destroy(t.gameObject);
            }
        }

        /// <summary>
        /// Capture the current frame, run the dissolve, and invoke <paramref name="loadNext"/>
        /// while the tiles still cover the screen.
        /// </summary>
        public static void Play(Action loadNext, float grayHold = DefaultGrayHold, float dissolveDuration = DefaultDissolveDuration)
        {
            if (!Application.isPlaying)
            {
                loadNext?.Invoke();
                return;
            }

            // Match player build: do not stack / interrupt an in-flight dissolve.
            if (_busy)
                return;

            var go = new GameObject(nameof(BlockDissolveTransition));
            UnityEngine.Object.DontDestroyOnLoad(go);
            _activeRoot = go;
            var runner = go.AddComponent<Runner>();
            runner.Begin(loadNext, grayHold, dissolveDuration);
        }

        private sealed class BusyHoldRunner : MonoBehaviour
        {
            private int _frames;

            public void Begin(int frames)
            {
                _frames = frames;
                StartCoroutine(Run());
            }

            private IEnumerator Run()
            {
                for (int i = 0; i < _frames; i++)
                    yield return null;

                if (_activeRoot == gameObject)
                    _activeRoot = null;
                _busy = false;
                Destroy(gameObject);
            }
        }

        private sealed class Runner : MonoBehaviour
        {
            private Action _loadNext;
            private float _grayHold;
            private float _dissolveDuration;
            private Texture2D _capture;
            private readonly List<Tile> _tiles = new List<Tile>();

            private sealed class Tile
            {
                public RectTransform Rect;
                public CanvasGroup Group;
                public float Delay;
                public float Duration;
            }

            public void Begin(Action loadNext, float grayHold, float dissolveDuration)
            {
                _loadNext = loadNext;
                _grayHold = Mathf.Max(0.05f, grayHold);
                _dissolveDuration = Mathf.Max(0.2f, dissolveDuration);
                _busy = true;
                StartCoroutine(Run());
            }

            private IEnumerator Run()
            {
                // Let settle / UI finish the current frame before grabbing pixels.
                yield return new WaitForEndOfFrame();

                try
                {
                    _capture = ScreenCapture.CaptureScreenshotAsTexture();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BlockDissolveTransition] Capture failed: " + e.Message);
                    FinishWithoutFx();
                    yield break;
                }

                if (_capture == null)
                {
                    FinishWithoutFx();
                    yield break;
                }

                var canvasGo = new GameObject("BlockDissolveCanvas");
                canvasGo.transform.SetParent(transform, false);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = OverlaySortOrder;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                canvasGo.AddComponent<GraphicRaycaster>();

                var gray = CreateFullscreen(canvasGo.transform, "Gray");
                var grayImage = gray.gameObject.AddComponent<Image>();
                grayImage.color = new Color(0.45f, 0.45f, 0.47f, 1f);
                grayImage.raycastTarget = true;

                var tilesRoot = CreateFullscreen(canvasGo.transform, "Tiles");
                tilesRoot.gameObject.SetActive(false);
                BuildTiles(tilesRoot, _capture);

                yield return new WaitForSecondsRealtime(_grayHold);

                gray.gameObject.SetActive(false);
                tilesRoot.gameObject.SetActive(true);

                // Next scene loads underneath the covering tiles.
                try
                {
                    _loadNext?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BlockDissolveTransition] loadNext failed: " + e.Message);
                }

                _loadNext = null;
                yield return null;
                yield return new WaitForEndOfFrame();

                float start = Time.unscaledTime;
                float end = start + _dissolveDuration;
                while (Time.unscaledTime < end)
                {
                    float now = Time.unscaledTime;
                    for (int i = 0; i < _tiles.Count; i++)
                    {
                        var tile = _tiles[i];
                        if (tile.Group == null) continue;
                        float localT = (now - start - tile.Delay) / Mathf.Max(0.01f, tile.Duration);
                        if (localT <= 0f)
                        {
                            tile.Group.alpha = 1f;
                            continue;
                        }

                        if (localT >= 1f)
                        {
                            tile.Group.alpha = 0f;
                            if (tile.Rect != null && tile.Rect.gameObject.activeSelf)
                                tile.Rect.gameObject.SetActive(false);
                            continue;
                        }

                        float ease = 1f - (1f - localT) * (1f - localT);
                        tile.Group.alpha = 1f - ease;
                        float scale = Mathf.Lerp(1f, 0.72f, ease);
                        tile.Rect.localScale = new Vector3(scale, scale, 1f);
                    }

                    yield return null;
                }

                Cleanup();
            }

            private void FinishWithoutFx()
            {
                try
                {
                    _loadNext?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BlockDissolveTransition] loadNext failed: " + e.Message);
                }

                Cleanup();
            }

            private void BuildTiles(RectTransform parent, Texture2D tex)
            {
                _tiles.Clear();
                int texW = Mathf.Max(1, tex.width);
                int texH = Mathf.Max(1, tex.height);
                float aspect = (float)texW / texH;
                int cols = DefaultColumns;
                int rows = Mathf.Max(6, Mathf.RoundToInt(cols / aspect));

                float cellW = 1f / cols;
                float cellH = 1f / rows;
                var order = new List<int>(cols * rows);
                for (int i = 0; i < cols * rows; i++)
                    order.Add(i);
                Shuffle(order);

                float staggerWindow = _dissolveDuration * 0.62f;
                float tileLife = Mathf.Max(0.08f, _dissolveDuration * 0.28f);

                for (int i = 0; i < order.Count; i++)
                {
                    int index = order[i];
                    int x = index % cols;
                    int y = index / cols;

                    var go = new GameObject($"Tile_{x}_{y}");
                    go.transform.SetParent(parent, false);
                    var rect = go.AddComponent<RectTransform>();
                    rect.anchorMin = new Vector2(x * cellW, 1f - (y + 1) * cellH);
                    rect.anchorMax = new Vector2((x + 1) * cellW, 1f - y * cellH);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    rect.pivot = new Vector2(0.5f, 0.5f);

                    var raw = go.AddComponent<RawImage>();
                    raw.texture = tex;
                    raw.uvRect = new Rect(x * cellW, 1f - (y + 1) * cellH, cellW, cellH);
                    raw.raycastTarget = true;
                    raw.color = Color.white;

                    var group = go.AddComponent<CanvasGroup>();
                    group.alpha = 1f;
                    group.blocksRaycasts = true;

                    _tiles.Add(new Tile
                    {
                        Rect = rect,
                        Group = group,
                        Delay = (i / (float)Mathf.Max(1, order.Count - 1)) * staggerWindow,
                        Duration = tileLife,
                    });
                }
            }

            private static RectTransform CreateFullscreen(Transform parent, string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var rect = go.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                return rect;
            }

            private static void Shuffle(List<int> list)
            {
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    (list[i], list[j]) = (list[j], list[i]);
                }
            }

            private void Cleanup()
            {
                _busy = false;
                if (_activeRoot == gameObject)
                    _activeRoot = null;
                if (_capture != null)
                {
                    Destroy(_capture);
                    _capture = null;
                }

                Destroy(gameObject);
            }

            private void OnDestroy()
            {
                _busy = false;
                if (_activeRoot == gameObject)
                    _activeRoot = null;
                if (_capture != null)
                {
                    Destroy(_capture);
                    _capture = null;
                }
            }
        }
    }
}
