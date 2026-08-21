using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Soup.Game.Editor
{
    /// <summary>
    /// Converts scene-authored title sprites into OverlayCanvas widgets
    /// and fills leftover screen area with the illustration paper color.
    /// </summary>
    public static class MainMenuAuthoredInstaller
    {
        private const string WorldHolderName = "TitleWorldSource";
        private const string AuthoredRootName = "AuthoredTitle";

        [MenuItem("Soup/主菜单/安装已绘制界面", false, 12)]
        public static void InstallFromMenu()
        {
            Debug.Log(Install());
        }

        public static string Install()
        {
            var menu = Object.FindObjectOfType<MainMenuUI>();
            if (menu == null)
                return "No MainMenuUI";

            menu.EnsureAuthoredCanvas(false);
            var freeDraw = menu.FreeDrawRoot;
            if (freeDraw == null)
                return "No FreeDraw";

            var title = FindWorldSprite("开始");
            var startBtn = FindWorldSprite("按钮1");
            var continueBtn = FindWorldSprite("按钮2");
            var quitBtn = FindWorldSprite("按钮3");
            if (title == null)
                return "Missing 开始 sprite";

            var old = freeDraw.Find(AuthoredRootName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            var oldBg = freeDraw.Find("Background");
            if (oldBg != null)
                Object.DestroyImmediate(oldBg.gameObject);

            EnsurePaperBackground(freeDraw);
            MatchCameraPaperColor();

            var rootGo = new GameObject(AuthoredRootName, typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(freeDraw, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.SetAsLastSibling();

            MakeImage("TitleArt", title, root, false);

            if (startBtn != null)
            {
                var rt = MakeImage("StartBtn", startBtn, root, true);
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = rt.GetComponent<Image>();
            }

            if (continueBtn != null)
            {
                var rt = MakeImage("ContinueBtn", continueBtn, root, true);
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = rt.GetComponent<Image>();
            }

            if (quitBtn != null)
            {
                var rt = MakeImage("QuitBtn", quitBtn, root, true);
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = rt.GetComponent<Image>();
            }

            DisableWorldSprites(title, startBtn, continueBtn, quitBtn);

            EditorUtility.SetDirty(menu.gameObject);
            EditorUtility.SetDirty(rootGo);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            return "AuthoredTitle installed. paper=#FFD293 sides covered.";
        }

        private static void EnsurePaperBackground(Transform freeDraw)
        {
            var go = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(freeDraw, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling();

            var image = go.GetComponent<Image>();
            image.sprite = GameOverlayUI.SharedUiSprite();
            image.color = MainMenuUI.TitlePaperColor;
            image.raycastTarget = true;
            image.type = Image.Type.Simple;
        }

        private static void MatchCameraPaperColor()
        {
            var camera = Camera.main;
            if (camera == null)
                camera = Object.FindObjectOfType<Camera>();
            if (camera == null)
                return;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = MainMenuUI.TitlePaperColor;
            EditorUtility.SetDirty(camera);
        }

        private static RectTransform MakeImage(string name, SpriteRenderer src, Transform parent, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = src.transform.position;
            rt.sizeDelta = new Vector2(src.bounds.size.x, src.bounds.size.y);
            var img = go.GetComponent<Image>();
            img.sprite = src.sprite;
            img.color = src.color;
            img.preserveAspect = true;
            img.raycastTarget = raycast;
            img.type = Image.Type.Simple;
            return rt;
        }

        private static SpriteRenderer FindWorldSprite(string name)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != name)
                    continue;
                var sr = roots[i].GetComponent<SpriteRenderer>();
                if (sr != null)
                    return sr;
            }

            var all = Object.FindObjectsOfType<SpriteRenderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }

            return null;
        }

        private static void DisableWorldSprites(params SpriteRenderer[] sprites)
        {
            var holderGo = GameObject.Find(WorldHolderName);
            if (holderGo == null)
                holderGo = new GameObject(WorldHolderName);
            holderGo.SetActive(false);

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                sprites[i].transform.SetParent(holderGo.transform, true);
                sprites[i].gameObject.SetActive(false);
            }
        }
    }
}
