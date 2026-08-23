using System;
using System.Collections;
using System.Collections.Generic;
using Soup.Events;
using Soup.Levels;
using Soup.Relics;
using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// 商店界面：场景变暗 + 商店面板与右侧配图从左侧滑入；
    /// 展示 3 个随机商店遗物；点外侧返回；选择或返回时向右滑出。
    /// </summary>
    public sealed class ShopPanelUI : MonoBehaviour
    {
        private const int OfferCount = 3;
        private const float AnimSeconds = 0.32f;
        private const float HiddenXLeft = -1600f;
        private const float ShownX = 0f;
        private const float HiddenXRight = 1600f;

        private Canvas _canvas;
        private CanvasGroup _dimGroup;
        private RectTransform _contentRect;
        private CanvasGroup _contentGroup;
        private Text _titleText;
        private Text _hintText;
        private Image _shopPanelBg;
        private Image _rightImage;
        private Text _rightImageHint;
        private readonly RelicSlot[] _slots = new RelicSlot[OfferCount];

        private readonly List<RelicItem> _offers = new List<RelicItem>(OfferCount);
        private bool _built;
        private bool _open;
        private bool _animating;
        private Coroutine _animCo;
        private Action<string> _toast;
        private Action _onClosed;

        private sealed class RelicSlot
        {
            public GameObject Root;
            public Button Button;
            public Image Icon;
            public Text Name;
            public Text Body;
            public RelicItem Relic;
        }

        public bool IsOpen => _open;

        public bool PurchasedThisVisit =>
            LevelManager.Instance?.ClearRewards?.ShopClaimed == true;

        public static ShopPanelUI Ensure(Transform parent = null)
        {
            var existing = FindObjectOfType<ShopPanelUI>();
            if (existing != null)
            {
                existing.EnsureBuilt();
                return existing;
            }

            if (parent == null)
            {
                var go = new GameObject(nameof(ShopPanelUI));
                return go.AddComponent<ShopPanelUI>();
            }

            var child = new GameObject(nameof(ShopPanelUI));
            child.transform.SetParent(parent, false);
            return child.AddComponent<ShopPanelUI>();
        }

        public void SetToastHandler(Action<string> handler) => _toast = handler;

        public void SetClosedHandler(Action handler) => _onClosed = handler;

        private void Awake() => EnsureBuilt();

        /// <summary>打开商店并刷新 3 个随机商店遗物（仅 Shop 来源；唯一遗物已拥有则不再出现）。</summary>
        public bool Show(bool animate)
        {
            EnsureBuilt();
            if (!RefreshOffers())
            {
                Toast("暂无可购遗物");
                return false;
            }

            PopulateSlots();
            SetVisibleRoot(true);
            _open = true;

            if (_animCo != null)
                StopCoroutine(_animCo);
            _animCo = StartCoroutine(AnimateIn(animate));
            return true;
        }

        public void Hide(bool animate)
        {
            if (!_built) return;
            if (!_open && !_animating)
            {
                SetVisibleRoot(false);
                return;
            }

            if (_animCo != null)
                StopCoroutine(_animCo);
            _animCo = StartCoroutine(AnimateOut(animate));
        }

        private bool RefreshOffers()
        {
            _offers.Clear();
            var session = LevelManager.Instance?.ClearRewards;
            if (session == null || !session.IsActive) return false;

            var offer = session.BuildShopOffers(OfferCount);
            for (int i = 0; i < offer.Count; i++)
            {
                if (offer[i] != null)
                    _offers.Add(offer[i]);
            }

            return _offers.Count > 0;
        }

        private void PopulateSlots()
        {
            for (int i = 0; i < OfferCount; i++)
            {
                var slot = _slots[i];
                if (slot == null || slot.Root == null) continue;

                bool has = i < _offers.Count && _offers[i] != null;
                slot.Root.SetActive(has);
                if (!has)
                {
                    slot.Relic = null;
                    continue;
                }

                var relic = _offers[i];
                slot.Relic = relic;
                if (slot.Name != null)
                    slot.Name.text = relic.DisplayName;
                if (slot.Body != null)
                {
                    slot.Body.text = string.Empty;
                    slot.Body.gameObject.SetActive(false);
                }

                if (slot.Icon != null)
                    slot.Icon.gameObject.SetActive(false);

                BindShopRelicHover(slot, relic);
            }

            if (_titleText != null)
                _titleText.text = "商店";
            if (_hintText != null)
                _hintText.text = "选择一件遗物（免费）";
            if (_rightImageHint != null)
                _rightImageHint.gameObject.SetActive(_rightImage == null || _rightImage.sprite == null);
        }

        private static void BindShopRelicHover(RelicSlot slot, RelicItem relic)
        {
            if (slot == null || slot.Root == null || relic == null) return;
            var tip = slot.Root.GetComponent<UiHoverTooltip>();
            if (tip == null)
                tip = slot.Root.AddComponent<UiHoverTooltip>();
            HoverTooltipText.Relic(relic, out string title, out string body);
            tip.Bind(title, body);
        }

        private void OnDimClicked()
        {
            if (_animating || !_open) return;
            Hide(animate: true);
        }

        private void OnRelicClicked(int index)
        {
            if (_animating || !_open) return;
            if (index < 0 || index >= _offers.Count) return;

            var relic = _offers[index];
            if (relic == null)
            {
                Toast("无效遗物");
                return;
            }

            var session = LevelManager.Instance?.ClearRewards;
            if (session == null)
            {
                Toast("无法获得该遗物");
                return;
            }

            var events = EventManager.Instance;
            if (events != null
                && (events.HasPendingEvent || events.HasStageEventBatch || events.QueuedEventCount > 0))
            {
                Toast("请先完成当前事件选择");
                return;
            }

            if (!session.TryPurchaseShopRelic(relic, out var msg))
            {
                Toast(string.IsNullOrEmpty(msg) ? "无法获得该遗物" : msg);
                return;
            }

            TurnManager.Instance?.ClearUndoSnapshot();
            Toast(msg);
            Hide(animate: true);
        }

        private IEnumerator AnimateIn(bool animate)
        {
            _animating = true;
            if (!animate)
            {
                SetAnimState(1f, ShownX);
                _animating = false;
                yield break;
            }

            SetAnimState(0f, HiddenXLeft);
            float t = 0f;
            while (t < AnimSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / AnimSeconds);
                float e = EaseOutCubic(u);
                SetAnimState(e, Mathf.Lerp(HiddenXLeft, ShownX, e));
                yield return null;
            }

            SetAnimState(1f, ShownX);
            _animating = false;
            _animCo = null;
        }

        private IEnumerator AnimateOut(bool animate)
        {
            _animating = true;
            float dimFrom = _dimGroup != null ? _dimGroup.alpha : 1f;
            float xFrom = _contentRect != null ? _contentRect.anchoredPosition.x : ShownX;
            if (!animate)
            {
                SetAnimState(0f, HiddenXRight);
                FinishClose();
                yield break;
            }

            float t = 0f;
            while (t < AnimSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / AnimSeconds);
                float e = EaseInCubic(u);
                SetAnimState(Mathf.Lerp(dimFrom, 0f, e), Mathf.Lerp(xFrom, HiddenXRight, e));
                yield return null;
            }

            SetAnimState(0f, HiddenXRight);
            FinishClose();
        }

        private void FinishClose()
        {
            _open = false;
            SetVisibleRoot(false);
            _animating = false;
            _animCo = null;
            _onClosed?.Invoke();
        }

        private void SetAnimState(float dimAlpha, float contentX)
        {
            if (_dimGroup != null)
                _dimGroup.alpha = dimAlpha;
            if (_contentGroup != null)
                _contentGroup.alpha = Mathf.Clamp01(dimAlpha * 1.15f);
            if (_contentRect != null)
            {
                var p = _contentRect.anchoredPosition;
                p.x = contentX;
                _contentRect.anchoredPosition = p;
            }
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float EaseInCubic(float t) => t * t * t;

        private void SetVisibleRoot(bool visible)
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(visible);
        }

        private void Toast(string message)
        {
            if (_toast != null)
            {
                _toast.Invoke(message);
                return;
            }

            var overlay = FindObjectOfType<GameOverlayUI>();
            if (overlay != null)
            {
                overlay.ShowToast(message, 3f);
                return;
            }

            var inter = FindObjectOfType<InterLevelUI>();
            inter?.ShowToast(message, 3f);
        }

        private void EnsureBuilt()
        {
            if (!_built)
            {
                Build();
                _built = true;
                SetVisibleRoot(false);
            }

            ApplyShopArt();
        }

        private void ApplyShopArt()
        {
            var art = GameArtLibrary.Load();
            if (_shopPanelBg != null)
            {
                if (art != null && art.ShopBackground != null)
                {
                    _shopPanelBg.sprite = art.ShopBackground;
                    _shopPanelBg.color = Color.white;
                    _shopPanelBg.preserveAspect = false;
                    _shopPanelBg.type = Image.Type.Simple;
                }
            }

            if (_rightImage != null)
            {
                if (art != null && art.ShopCatPortrait != null)
                {
                    _rightImage.sprite = art.ShopCatPortrait;
                    _rightImage.color = Color.white;
                    _rightImage.preserveAspect = true;
                    _rightImage.type = Image.Type.Simple;
                    if (_rightImageHint != null)
                        _rightImageHint.gameObject.SetActive(false);
                }
            }
        }

        private void Build()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            var canvasGo = new GameObject("ShopPanelCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var dimGo = new GameObject("Dim");
            dimGo.transform.SetParent(canvasGo.transform, false);
            var dimRect = dimGo.AddComponent<RectTransform>();
            StretchFull(dimRect);
            var dimImage = dimGo.AddComponent<Image>();
            dimImage.sprite = GameOverlayUI.SharedUiSprite();
            dimImage.color = new Color(0f, 0f, 0f, 0.72f);
            dimImage.raycastTarget = true;
            _dimGroup = dimGo.AddComponent<CanvasGroup>();
            _dimGroup.alpha = 0f;
            var dimBtn = dimGo.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(OnDimClicked);

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(canvasGo.transform, false);
            _contentRect = contentGo.AddComponent<RectTransform>();
            _contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            _contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            _contentRect.pivot = new Vector2(0.5f, 0.5f);
            _contentRect.sizeDelta = new Vector2(1280f, 720f);
            _contentRect.anchoredPosition = new Vector2(HiddenXLeft, 0f);
            _contentGroup = contentGo.AddComponent<CanvasGroup>();

            // Left: shop panel
            var shopGo = new GameObject("ShopPanel");
            shopGo.transform.SetParent(contentGo.transform, false);
            var shopRect = shopGo.AddComponent<RectTransform>();
            shopRect.anchorMin = new Vector2(0f, 0f);
            shopRect.anchorMax = new Vector2(0.58f, 1f);
            shopRect.offsetMin = Vector2.zero;
            shopRect.offsetMax = new Vector2(-16f, 0f);
            _shopPanelBg = shopGo.AddComponent<Image>();
            _shopPanelBg.sprite = GameOverlayUI.SharedUiSprite();
            _shopPanelBg.color = new Color(0.12f, 0.14f, 0.20f, 0.98f);
            _shopPanelBg.raycastTarget = true;

            _titleText = CreateText(shopGo.transform, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(460f, 30f),
                24, FontStyle.Bold, TextAnchor.MiddleCenter);
            _titleText.text = "商店";

            _hintText = CreateText(shopGo.transform, "Hint",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -48f), new Vector2(460f, 24f),
                14, FontStyle.Normal, TextAnchor.MiddleCenter);
            _hintText.color = new Color(0.78f, 0.82f, 0.90f, 1f);
            _hintText.text = "选择一件遗物（免费）";

            var listGo = new GameObject("RelicList");
            listGo.transform.SetParent(shopGo.transform, false);
            var listRect = listGo.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 0f);
            listRect.anchorMax = new Vector2(1f, 1f);
            // Keep cards inside the shop-background frame (art has thick borders).
            listRect.offsetMin = new Vector2(64f, 56f);
            listRect.offsetMax = new Vector2(-64f, -78f);
            var layout = listGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 0f;
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            // Even vertical rhythm: spacer · card · spacer · card · spacer · card · spacer
            CreateFlexSpacer(listGo.transform);
            for (int i = 0; i < OfferCount; i++)
            {
                int index = i;
                _slots[i] = CreateRelicSlot(listGo.transform, index, () => OnRelicClicked(index));
                CreateFlexSpacer(listGo.transform);
            }

            // Right: cat portrait
            var rightGo = new GameObject("RightImage");
            rightGo.transform.SetParent(contentGo.transform, false);
            var rightRect = rightGo.AddComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.58f, 0f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.offsetMin = new Vector2(16f, 0f);
            rightRect.offsetMax = Vector2.zero;
            _rightImage = rightGo.AddComponent<Image>();
            _rightImage.sprite = GameOverlayUI.SharedUiSprite();
            _rightImage.color = new Color(0.18f, 0.22f, 0.28f, 1f);
            _rightImage.raycastTarget = true;
            _rightImage.preserveAspect = true;

            _rightImageHint = CreateText(rightGo.transform, "ImageHint",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(280f, 80f),
                24, FontStyle.Normal, TextAnchor.MiddleCenter);
            _rightImageHint.color = new Color(1f, 1f, 1f, 0.35f);
            _rightImageHint.text = "图片（待定）";
        }

        private static void CreateFlexSpacer(Transform parent)
        {
            var go = new GameObject("Spacer");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 8f;
            le.preferredHeight = 8f;
            le.flexibleHeight = 1f;
        }

        private RelicSlot CreateRelicSlot(Transform parent, int index, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Relic{index}");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            // 原宽约铺满列表；现宽减半并居中。原高 64 → ×1.5 = 96。
            le.minWidth = 280f;
            le.preferredWidth = 300f;
            le.flexibleWidth = 0f;
            le.minHeight = 96f;
            le.preferredHeight = 96f;
            le.flexibleHeight = 0f;

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            GameOverlayUI.ApplyArtButtonStyle(image, button);
            button.onClick.AddListener(onClick);

            // 选项框内仅显示名称；描述走悬停提示。
            var name = CreateText(go.transform, "Name",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                28, FontStyle.Bold, TextAnchor.MiddleCenter);
            var nameRect = name.GetComponent<RectTransform>();
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(12f, 8f);
            nameRect.offsetMax = new Vector2(-12f, -8f);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 16;
            name.resizeTextMaxSize = 36;

            return new RelicSlot
            {
                Root = go,
                Button = button,
                Icon = null,
                Name = name,
                Body = null
            };
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            int fontSize,
            FontStyle style,
            TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
