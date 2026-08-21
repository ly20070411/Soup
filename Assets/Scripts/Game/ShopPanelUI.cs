using System;
using System.Collections;
using System.Collections.Generic;
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
        private Image _rightImage;
        private Text _rightImageHint;
        private readonly RelicSlot[] _slots = new RelicSlot[OfferCount];

        private readonly List<RelicItem> _offers = new List<RelicItem>(OfferCount);
        private bool _built;
        private bool _open;
        private bool _animating;
        private bool _purchasedThisVisit;
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
        public bool PurchasedThisVisit => _purchasedThisVisit;

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
            var relics = RelicManager.Instance;
            if (relics == null) return false;

            var offer = relics.CreateOffer(OfferCount, RelicAcquireStage.Shop, fillFromOtherStages: false);
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
                    string rules = relic.GetRulesSummary();
                    string desc = relic.Description ?? string.Empty;
                    slot.Body.text = string.IsNullOrWhiteSpace(rules) || rules == "无规则"
                        ? desc
                        : (string.IsNullOrWhiteSpace(desc) ? rules : rules + "\n" + desc);
                }

                if (slot.Icon != null)
                {
                    if (relic.Icon != null)
                    {
                        slot.Icon.sprite = relic.Icon;
                        slot.Icon.color = relic.Tint.a > 0.01f ? relic.Tint : Color.white;
                    }
                    else
                    {
                        slot.Icon.sprite = GameOverlayUI.SharedUiSprite();
                        slot.Icon.color = new Color(0.45f, 0.50f, 0.62f, 1f);
                    }
                }

                BindShopRelicHover(slot, relic);
            }

            if (_titleText != null)
                _titleText.text = "商店";
            if (_hintText != null)
                _hintText.text = "选择一件遗物（免费）";
            if (_rightImageHint != null)
                _rightImageHint.text = "图片（待定）";
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

            var relics = RelicManager.Instance;
            if (relics == null || !relics.Acquire(relic))
            {
                Toast("无法获得该遗物");
                return;
            }

            _purchasedThisVisit = true;
            Toast($"购得遗物：{relic.DisplayName}");
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

            var inter = FindObjectOfType<InterLevelUI>();
            inter?.ShowToast(message, 3f);
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            Build();
            _built = true;
            SetVisibleRoot(false);
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
            var shopBg = shopGo.AddComponent<Image>();
            shopBg.sprite = GameOverlayUI.SharedUiSprite();
            shopBg.color = new Color(0.12f, 0.14f, 0.20f, 0.98f);
            shopBg.raycastTarget = true;

            _titleText = CreateText(shopGo.transform, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -28f), new Vector2(600f, 48f),
                34, FontStyle.Bold, TextAnchor.MiddleCenter);
            _titleText.text = "商店";

            _hintText = CreateText(shopGo.transform, "Hint",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -72f), new Vector2(600f, 32f),
                18, FontStyle.Normal, TextAnchor.MiddleCenter);
            _hintText.color = new Color(0.78f, 0.82f, 0.90f, 1f);
            _hintText.text = "选择一件遗物（免费）";

            var listGo = new GameObject("RelicList");
            listGo.transform.SetParent(shopGo.transform, false);
            var listRect = listGo.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 0f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.offsetMin = new Vector2(24f, 24f);
            listRect.offsetMax = new Vector2(-24f, -110f);
            var layout = listGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            for (int i = 0; i < OfferCount; i++)
            {
                int index = i;
                _slots[i] = CreateRelicSlot(listGo.transform, index, () => OnRelicClicked(index));
            }

            // Right: image placeholder
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

            _rightImageHint = CreateText(rightGo.transform, "ImageHint",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(280f, 80f),
                24, FontStyle.Normal, TextAnchor.MiddleCenter);
            _rightImageHint.color = new Color(1f, 1f, 1f, 0.35f);
            _rightImageHint.text = "图片（待定）";
        }

        private RelicSlot CreateRelicSlot(Transform parent, int index, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Relic{index}");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 150f;
            le.preferredHeight = 160f;

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            GameOverlayUI.ApplyArtButtonStyle(image, button);
            button.onClick.AddListener(onClick);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(16f, 0f);
            iconRect.sizeDelta = new Vector2(96f, 96f);
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = GameOverlayUI.SharedUiSprite();
            icon.color = new Color(0.45f, 0.50f, 0.62f, 1f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var name = CreateText(go.transform, "Name",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(130f, -14f), new Vector2(-150f, 36f),
                22, FontStyle.Bold, TextAnchor.MiddleLeft);
            var nameRect = name.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.offsetMin = new Vector2(130f, -50f);
            nameRect.offsetMax = new Vector2(-16f, -12f);

            var body = CreateText(go.transform, "Body",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                Vector2.zero, Vector2.zero,
                16, FontStyle.Normal, TextAnchor.UpperLeft);
            body.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            var bodyRect = body.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(130f, 12f);
            bodyRect.offsetMax = new Vector2(-16f, -54f);

            return new RelicSlot
            {
                Root = go,
                Button = button,
                Icon = icon,
                Name = name,
                Body = body
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
