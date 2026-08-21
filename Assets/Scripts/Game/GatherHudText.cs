using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// Safe CJK-capable font for UI / TextMesh. Avoids AssetRipper-recovered
    /// YaHei assets whose texture is 0×0 (spam "Font texture … is missing").
    /// </summary>
    public static class SafeUiFont
    {
        private static Font _cached;
        private static bool _resolved;

        private static readonly string[] OsCandidates =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "微软雅黑",
            "SimHei",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "Arial Unicode MS",
            "Arial"
        };

        public static Font Get(int size = 24)
        {
            if (_resolved && IsUsable(_cached))
                return _cached;

            _cached = null;
            _resolved = false;

            for (int i = 0; i < OsCandidates.Length; i++)
            {
                try
                {
                    var font = Font.CreateDynamicFontFromOSFont(OsCandidates[i], size);
                    if (IsUsable(font))
                    {
                        _cached = font;
                        _resolved = true;
                        return _cached;
                    }
                }
                catch
                {
                    // try next
                }
            }

            _cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!IsUsable(_cached))
                _cached = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _resolved = true;
            return _cached;
        }

        public static bool IsUsable(Font font)
        {
            if (font == null) return false;
            try
            {
                var mat = font.material;
                if (mat == null) return false;
                var tex = mat.mainTexture;
                if (tex == null) return false;
                if (tex.width <= 0 || tex.height <= 0) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void RepairAllInLoadedScenes(int textMeshSize = 42)
        {
            var font = Get(textMeshSize);
            if (font == null) return;

            var meshes = Object.FindObjectsOfType<TextMesh>();
            for (int i = 0; i < meshes.Length; i++)
            {
                var mesh = meshes[i];
                if (mesh == null) continue;
                if (mesh.font == font && IsUsable(mesh.font)) continue;
                mesh.font = font;
                var mr = mesh.GetComponent<MeshRenderer>();
                if (mr != null && font.material != null)
                    mr.sharedMaterial = font.material;
            }

            var texts = Object.FindObjectsOfType<Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == null) continue;
                if (text.font == font) continue;
                if (text.font != null && IsUsable(text.font) && text.font.name != "Arial")
                    continue;
                text.font = Get(Mathf.Max(16, text.fontSize));
            }
        }
    }

    /// <summary>Shared TextMesh styling for gather-zone chrome (dark ink on yellow wood).</summary>
    public static class GatherHudText
    {
        public static readonly Color Ink = new Color(0.27f, 0.13f, 0.05f, 1f);
        public static readonly Color Muted = new Color(0.42f, 0.28f, 0.14f, 0.85f);

        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font != null && SafeUiFont.IsUsable(_font))
                    return _font;
                _font = SafeUiFont.Get(42);
                return _font;
            }
        }

        public static TextMesh Ensure(
            Transform parent,
            string name,
            Vector3 localPos,
            Vector3 localScale,
            int sorting,
            int fontSize = 48)
        {
            if (parent == null) return null;

            TextMesh mesh = null;
            var existing = FindDirect(parent, name);
            if (existing != null)
                mesh = existing.GetComponent<TextMesh>();

            if (mesh == null)
            {
                var go = existing != null ? existing.gameObject : new GameObject(name);
                go.name = name;
                go.transform.SetParent(parent, false);
                mesh = go.GetComponent<TextMesh>();
                if (mesh == null)
                    mesh = go.AddComponent<TextMesh>();
            }

            DestroyExtras(parent, name, mesh.transform);

            mesh.transform.localPosition = localPos;
            mesh.transform.localRotation = Quaternion.identity;
            mesh.transform.localScale = localScale;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Ink;
            mesh.characterSize = 0.05f;
            mesh.lineSpacing = 1f;
            mesh.tabSize = 4;
            mesh.richText = false;

            ApplyFont(mesh, fontSize);

            var mr = mesh.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = sorting;
                if (Font != null && Font.material != null)
                    mr.sharedMaterial = Font.material;
            }

            return mesh;
        }

        /// <summary>
        /// Force a working OS/dynamic font. Recovered AssetRipper font assets often
        /// fail to render (zero-width meshes) and must be replaced at runtime.
        /// </summary>
        public static void ApplyFont(TextMesh mesh, int fontSize = -1)
        {
            if (mesh == null) return;
            var font = Font;
            if (font == null) return;

            mesh.font = font;
            if (fontSize > 0)
                mesh.fontSize = fontSize;

            var mr = mesh.GetComponent<MeshRenderer>();
            if (mr != null && font.material != null)
                mr.sharedMaterial = font.material;
        }

        public static Transform FindDirect(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null && child.name == name)
                    return child;
            }

            return null;
        }

        public static void DestroyExtras(Transform parent, string name, Transform keep)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child == null || child.name != name || child == keep)
                    continue;
                DestroyGo(child.gameObject);
            }
        }

        public static void DestroyGo(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }

        public static Vector3 LocalScaleForWorld(Transform parent, float worldSize)
        {
            if (parent == null) return Vector3.one * worldSize;
            var lossy = parent.lossyScale;
            float sx = Mathf.Abs(lossy.x) < 0.0001f ? 1f : lossy.x;
            float sy = Mathf.Abs(lossy.y) < 0.0001f ? 1f : lossy.y;
            float sz = Mathf.Abs(lossy.z) < 0.0001f ? 1f : lossy.z;
            return new Vector3(worldSize / sx, worldSize / sy, worldSize / sz);
        }

        public static void FitInside(TextMesh mesh, Vector2 worldBox, float fill = 0.85f)
        {
            if (mesh == null) return;
            var mr = mesh.GetComponent<MeshRenderer>();
            if (mr == null) return;

            var bounds = mr.bounds;
            if (bounds.size.x < 0.0001f || bounds.size.y < 0.0001f) return;

            float targetW = worldBox.x * fill;
            float targetH = worldBox.y * fill;
            float scaleX = targetW / bounds.size.x;
            float scaleY = targetH / bounds.size.y;
            float s = Mathf.Min(scaleX, scaleY);
            if (s <= 0f || float.IsNaN(s) || float.IsInfinity(s)) return;

            var t = mesh.transform;
            t.localScale = new Vector3(
                t.localScale.x * s,
                t.localScale.y * s,
                t.localScale.z);
        }

        public static void FitInside(TextMesh mesh, Vector3 worldBox, float fill = 0.85f)
        {
            FitInside(mesh, new Vector2(worldBox.x, worldBox.y), fill);
        }

        public static void SnapCenter(TextMesh mesh, Vector3 worldCenter, float zOffset = 0f)
        {
            if (mesh == null) return;
            var p = worldCenter;
            p.z += zOffset;
            mesh.transform.position = p;
        }

        public static void SnapGroupCenter(TextMesh a, TextMesh b, Vector3 worldCenter)
        {
            if (a == null && b == null) return;
            if (a == null)
            {
                SnapCenter(b, worldCenter);
                return;
            }

            if (b == null)
            {
                SnapCenter(a, worldCenter);
                return;
            }

            var ra = a.GetComponent<MeshRenderer>();
            var rb = b.GetComponent<MeshRenderer>();
            if (ra == null || rb == null)
            {
                SnapCenter(a, worldCenter);
                SnapCenter(b, worldCenter);
                return;
            }

            var mid = (ra.bounds.center + rb.bounds.center) * 0.5f;
            var delta = worldCenter - mid;
            a.transform.position += delta;
            b.transform.position += delta;
        }
    }
}
