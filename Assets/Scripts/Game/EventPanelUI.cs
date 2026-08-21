using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Soup.Employees;
using Soup.Events;
using Soup.Relics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// 事件界面：场景变暗 + 面板上滑进入；选项内遗物/员工名加粗并可悬停看效果；
    /// 点外侧返回；选择或返回时下滑移出。
    /// </summary>
    public sealed class EventPanelUI : MonoBehaviour
    {
        private const int MaxOptions = 5;
        private const float AnimSeconds = 0.32f;
        private const float HiddenY = -920f;
        private const float ShownY = 0f;

        private Canvas _canvas;
        private CanvasGroup _dimGroup;
        private RectTransform _sheetRect;
        private CanvasGroup _sheetGroup;
        private Text _titleText;
        private Text _descText;
        private Image _illustrationImage;
        private Text _illustrationHint;
        private readonly OptionSlot[] _options = new OptionSlot[MaxOptions];
        private GameObject _tooltipRoot;
        private Text _tooltipTitle;
        private Text _tooltipBody;
        private RectTransform _tooltipRect;

        private bool _built;
        private bool _open;
        private bool _animating;
        private Coroutine _animCo;
        private Coroutine _afterChoiceCo;
        private Action<string> _toast;

        private struct NameSpan
        {
            public int Start;
            public int Length;
            public string Title;
            public string Body;
        }

        private sealed class OptionSlot
        {
            public GameObject Root;
            public Button Button;
            public Text Label;
            public RichNameHover Hover;
            public int Index;
        }

        public bool IsOpen => _open;

        public static EventPanelUI Ensure(Transform parent = null)
        {
            var existing = FindObjectOfType<EventPanelUI>();
            if (existing != null)
            {
                existing.EnsureBuilt();
                existing.BindEvents(true);
                return existing;
            }

            var host = parent;
            if (host == null)
            {
                var go = new GameObject(nameof(EventPanelUI));
                return go.AddComponent<EventPanelUI>();
            }

            var child = new GameObject(nameof(EventPanelUI));
            child.transform.SetParent(host, false);
            return child.AddComponent<EventPanelUI>();
        }

        public void SetToastHandler(Action<string> handler) => _toast = handler;

        private void Awake()
        {
            EnsureBuilt();
            BindEvents(true);
        }

        private void OnEnable() => BindEvents(true);

        private void OnDisable() => BindEvents(false);

        private void OnDestroy() => BindEvents(false);

        private void BindEvents(bool bind)
        {
            var events = EventManager.Instance;
            if (events == null) return;
            events.EventPresented -= OnEventPresented;
            events.EventResolved -= OnEventResolved;
            events.PendingCleared -= OnPendingCleared;
            if (!bind) return;
            events.EventPresented += OnEventPresented;
            events.EventResolved += OnEventResolved;
            events.PendingCleared += OnPendingCleared;
        }

        private void OnEventPresented(EventItem item)
        {
            if (_open && !_animating)
            {
                Populate(item);
                return;
            }

            Show(animate: true);
        }

        private void OnEventResolved(EventItem _, int __) => QueueAfterChoiceRefresh();

        private void OnPendingCleared() => QueueAfterChoiceRefresh();

        private void QueueAfterChoiceRefresh()
        {
            if (_afterChoiceCo != null)
                StopCoroutine(_afterChoiceCo);
            _afterChoiceCo = StartCoroutine(RefreshAfterChoice());
        }

        private IEnumerator RefreshAfterChoice()
        {
            // TryChooseOption 会先清空 pending 再入队下一条，等一帧再刷新。
            yield return null;
            _afterChoiceCo = null;

            var events = EventManager.Instance;
            if (events != null && events.HasPendingEvent)
            {
                if (_open)
                    Populate(events.PendingEvent);
                else
                    Show(animate: true);
            }
            else if (_open)
            {
                Hide(animate: true);
            }
        }

        /// <summary>打开当前待选事件；若无 pending 则无效。</summary>
        public bool Show(bool animate)
        {
            EnsureBuilt();
            var pending = EventManager.Instance != null ? EventManager.Instance.PendingEvent : null;
            if (pending == null)
                return false;

            Populate(pending);
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

        private void OnDimClicked()
        {
            if (_animating || !_open) return;
            Hide(animate: true);
        }

        private void OnOptionClicked(int index)
        {
            if (_animating || !_open) return;
            var events = EventManager.Instance;
            if (events == null)
            {
                Toast("EventManager 未就绪");
                return;
            }

            if (!events.TryChooseOption(index, out var message))
            {
                Toast(message);
                return;
            }

            Toast(message);
            HideTooltip();
            // 下一条 / 关闭由 OnEventResolved / RefreshAfterChoice 处理。
        }

        private void Populate(EventItem item)
        {
            if (item == null) return;
            if (_titleText != null)
                _titleText.text = item.DisplayName;
            if (_descText != null)
                _descText.text = item.Description ?? string.Empty;

            if (_illustrationHint != null)
                _illustrationHint.text = "插画（待定）";

            var options = item.Options;
            for (int i = 0; i < MaxOptions; i++)
            {
                var slot = _options[i];
                if (slot == null || slot.Root == null) continue;

                bool has = options != null && i < options.Count && options[i] != null;
                slot.Root.SetActive(has);
                if (!has) continue;

                var option = options[i];
                var spans = BuildNameSpans(option, out var rich);
                if (slot.Label != null)
                {
                    slot.Label.supportRichText = true;
                    slot.Label.text = rich;
                }

                if (slot.Hover != null)
                    slot.Hover.SetSpans(spans, ShowTooltip, HideTooltip);
            }

            HideTooltip();
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < MaxOptions; i++)
                _options[i]?.Hover?.RebuildHitAreas();
        }

        private static List<NameSpan> BuildNameSpans(EventOption option, out string richLabel)
        {
            var spans = new List<NameSpan>();
            string plain = option != null ? option.Label ?? string.Empty : string.Empty;
            if (option == null || option.Effects == null)
            {
                richLabel = plain;
                return spans;
            }

            // 收集名称（长名优先，避免短名抢匹配）
            var names = new List<(string name, string title, string body)>();
            for (int i = 0; i < option.Effects.Count; i++)
            {
                var fx = option.Effects[i];
                if (fx == null) continue;
                if (fx.RelicReference != null)
                {
                    var relic = fx.RelicReference;
                    HoverTooltipText.Relic(relic, out string tipTitle, out string tipBody);
                    names.Add((relic.DisplayName, tipTitle, tipBody));
                }
                else if (fx.EffectType == EventEffectType.AddChiefIncentive)
                {
                    // Legacy "族长的激励" options → still highlight 激励 relic tooltip.
                    var incentive = EventEffectRunner.ResolveIncentiveRelic(fx);
                    if (incentive != null)
                    {
                        HoverTooltipText.Relic(incentive, out string tipTitle, out string tipBody);
                        names.Add((incentive.DisplayName, tipTitle, tipBody));
                        names.Add(("族长的激励", tipTitle, tipBody));
                        names.Add(("组长的激励", tipTitle, tipBody));
                    }
                }

                if (fx.EmployeeReference != null)
                {
                    var emp = fx.EmployeeReference;
                    HoverTooltipText.Employee(emp, out string tipTitle, out string tipBody);
                    names.Add((emp.DisplayName, tipTitle, tipBody));
                }
            }

            names.Sort((a, b) => b.name.Length.CompareTo(a.name.Length));

            var used = new bool[plain.Length];
            var boldRanges = new List<(int start, int length)>();
            for (int n = 0; n < names.Count; n++)
            {
                var entry = names[n];
                if (string.IsNullOrEmpty(entry.name)) continue;
                int searchFrom = 0;
                while (searchFrom < plain.Length)
                {
                    int idx = plain.IndexOf(entry.name, searchFrom, StringComparison.Ordinal);
                    if (idx < 0) break;
                    if (!RangeTaken(used, idx, entry.name.Length))
                    {
                        MarkRange(used, idx, entry.name.Length);
                        spans.Add(new NameSpan
                        {
                            Start = idx,
                            Length = entry.name.Length,
                            Title = entry.title,
                            Body = entry.body
                        });
                        boldRanges.Add((idx, entry.name.Length));
                    }

                    searchFrom = idx + Math.Max(1, entry.name.Length);
                }
            }

            boldRanges.Sort((a, b) => a.start.CompareTo(b.start));
            richLabel = ApplyBoldTags(plain, boldRanges);
            return spans;
        }

        private static bool RangeTaken(bool[] used, int start, int length)
        {
            int end = Math.Min(used.Length, start + length);
            for (int i = start; i < end; i++)
                if (used[i]) return true;
            return false;
        }

        private static void MarkRange(bool[] used, int start, int length)
        {
            int end = Math.Min(used.Length, start + length);
            for (int i = start; i < end; i++)
                used[i] = true;
        }

        private static string ApplyBoldTags(string plain, List<(int start, int length)> ranges)
        {
            if (ranges == null || ranges.Count == 0)
                return plain;

            var sb = new StringBuilder(plain.Length + ranges.Count * 8);
            int cursor = 0;
            for (int i = 0; i < ranges.Count; i++)
            {
                var r = ranges[i];
                if (r.start < cursor) continue;
                sb.Append(plain, cursor, r.start - cursor);
                sb.Append("<b>");
                sb.Append(plain, r.start, r.length);
                sb.Append("</b>");
                cursor = r.start + r.length;
            }

            if (cursor < plain.Length)
                sb.Append(plain, cursor, plain.Length - cursor);
            return sb.ToString();
        }

        private void ShowTooltip(string title, string body, Vector2 screenPos)
        {
            if (_tooltipRoot == null) return;
            _tooltipRoot.SetActive(true);
            if (_tooltipTitle != null)
                _tooltipTitle.text = title ?? string.Empty;
            if (_tooltipBody != null)
                _tooltipBody.text = body ?? string.Empty;

            Canvas.ForceUpdateCanvases();
            if (_tooltipRect != null && _canvas != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas.transform as RectTransform,
                    screenPos,
                    _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
                    out var local);
                var size = _tooltipRect.sizeDelta;
                local += new Vector2(18f, 18f);
                // Keep on screen roughly.
                var canvasRect = (_canvas.transform as RectTransform).rect;
                local.x = Mathf.Clamp(local.x, canvasRect.xMin + size.x * 0.5f + 8f, canvasRect.xMax - size.x * 0.5f - 8f);
                local.y = Mathf.Clamp(local.y, canvasRect.yMin + size.y * 0.5f + 8f, canvasRect.yMax - size.y * 0.5f - 8f);
                _tooltipRect.anchoredPosition = local;
            }
        }

        private void HideTooltip()
        {
            if (_tooltipRoot != null)
                _tooltipRoot.SetActive(false);
        }

        private IEnumerator AnimateIn(bool animate)
        {
            _animating = true;
            float dimFrom = _dimGroup != null ? _dimGroup.alpha : 0f;
            float yFrom = _sheetRect != null ? _sheetRect.anchoredPosition.y : HiddenY;
            if (!animate)
            {
                SetAnimState(1f, ShownY);
                _animating = false;
                yield break;
            }

            SetAnimState(dimFrom, yFrom);
            float t = 0f;
            while (t < AnimSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / AnimSeconds);
                float e = EaseOutCubic(u);
                SetAnimState(Mathf.Lerp(0f, 1f, e), Mathf.Lerp(HiddenY, ShownY, e));
                yield return null;
            }

            SetAnimState(1f, ShownY);
            _animating = false;
            _animCo = null;
        }

        private IEnumerator AnimateOut(bool animate)
        {
            _animating = true;
            HideTooltip();
            float dimFrom = _dimGroup != null ? _dimGroup.alpha : 1f;
            float yFrom = _sheetRect != null ? _sheetRect.anchoredPosition.y : ShownY;
            if (!animate)
            {
                SetAnimState(0f, HiddenY);
                _open = false;
                SetVisibleRoot(false);
                _animating = false;
                yield break;
            }

            float t = 0f;
            while (t < AnimSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / AnimSeconds);
                float e = EaseInCubic(u);
                SetAnimState(Mathf.Lerp(dimFrom, 0f, e), Mathf.Lerp(yFrom, HiddenY, e));
                yield return null;
            }

            SetAnimState(0f, HiddenY);
            _open = false;
            SetVisibleRoot(false);
            _animating = false;
            _animCo = null;
        }

        private void SetAnimState(float dimAlpha, float sheetY)
        {
            if (_dimGroup != null)
                _dimGroup.alpha = dimAlpha;
            if (_sheetGroup != null)
                _sheetGroup.alpha = Mathf.Clamp01(dimAlpha * 1.15f);
            if (_sheetRect != null)
            {
                var p = _sheetRect.anchoredPosition;
                p.y = sheetY;
                _sheetRect.anchoredPosition = p;
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
                overlay.ShowToast(message, 3f);
            var inter = FindObjectOfType<InterLevelUI>();
            if (inter != null)
                inter.ShowToast(message, 3f);
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

            var canvasGo = new GameObject("EventPanelCanvas");
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

            var sheetGo = new GameObject("Sheet");
            sheetGo.transform.SetParent(canvasGo.transform, false);
            _sheetRect = sheetGo.AddComponent<RectTransform>();
            _sheetRect.anchorMin = new Vector2(0.5f, 0.5f);
            _sheetRect.anchorMax = new Vector2(0.5f, 0.5f);
            _sheetRect.pivot = new Vector2(0.5f, 0.5f);
            _sheetRect.sizeDelta = new Vector2(1280f, 720f);
            _sheetRect.anchoredPosition = new Vector2(0f, HiddenY);
            var sheetImage = sheetGo.AddComponent<Image>();
            sheetImage.sprite = GameOverlayUI.SharedUiSprite();
            sheetImage.color = new Color(0.11f, 0.13f, 0.18f, 0.98f);
            sheetImage.raycastTarget = true;
            _sheetGroup = sheetGo.AddComponent<CanvasGroup>();

            _titleText = CreateText(sheetGo.transform, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(1100f, 52f),
                36, FontStyle.Bold, TextAnchor.MiddleCenter);

            var body = new GameObject("Body");
            body.transform.SetParent(sheetGo.transform, false);
            var bodyRect = body.AddComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(36f, 36f);
            bodyRect.offsetMax = new Vector2(-36f, -100f);

            var illust = new GameObject("Illustration");
            illust.transform.SetParent(body.transform, false);
            var illustRect = illust.AddComponent<RectTransform>();
            illustRect.anchorMin = new Vector2(0f, 0f);
            illustRect.anchorMax = new Vector2(0.42f, 1f);
            illustRect.offsetMin = Vector2.zero;
            illustRect.offsetMax = new Vector2(-12f, 0f);
            _illustrationImage = illust.AddComponent<Image>();
            _illustrationImage.sprite = GameOverlayUI.SharedUiSprite();
            _illustrationImage.color = new Color(0.18f, 0.22f, 0.28f, 1f);
            _illustrationHint = CreateText(illust.transform, "IllustHint",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(280f, 80f),
                24, FontStyle.Normal, TextAnchor.MiddleCenter);
            _illustrationHint.color = new Color(1f, 1f, 1f, 0.35f);
            _illustrationHint.text = "插画（待定）";

            var right = new GameObject("Right");
            right.transform.SetParent(body.transform, false);
            var rightRect = right.AddComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.42f, 0f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.offsetMin = new Vector2(12f, 0f);
            rightRect.offsetMax = Vector2.zero;

            _descText = CreateText(right.transform, "Description",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 180f),
                22, FontStyle.Normal, TextAnchor.UpperLeft);
            var descRect = _descText.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 1f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.pivot = new Vector2(0.5f, 1f);
            descRect.anchoredPosition = Vector2.zero;
            descRect.sizeDelta = new Vector2(0f, 180f);
            _descText.color = new Color(0.88f, 0.90f, 0.94f, 1f);
            _descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _descText.verticalOverflow = VerticalWrapMode.Overflow;

            var optionsRoot = new GameObject("Options");
            optionsRoot.transform.SetParent(right.transform, false);
            var optionsRect = optionsRoot.AddComponent<RectTransform>();
            optionsRect.anchorMin = new Vector2(0f, 0f);
            optionsRect.anchorMax = new Vector2(1f, 1f);
            optionsRect.offsetMin = Vector2.zero;
            optionsRect.offsetMax = new Vector2(0f, -196f);
            var layout = optionsRoot.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(0, 0, 0, 0);

            for (int i = 0; i < MaxOptions; i++)
            {
                int index = i;
                var slot = CreateOptionSlot(optionsRoot.transform, index, () => OnOptionClicked(index));
                _options[i] = slot;
            }

            BuildTooltip(canvasGo.transform);
        }

        private OptionSlot CreateOptionSlot(Transform parent, int index, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Option{index}");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 72f;
            var fitted = GameOverlayUI.FitArtButtonSize(600f, 78f);
            le.preferredHeight = fitted.y;

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            GameOverlayUI.ApplyArtButtonStyle(image, button);
            button.onClick.AddListener(onClick);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            StretchFull(labelRect);
            labelRect.offsetMin = new Vector2(18f, 8f);
            labelRect.offsetMax = new Vector2(-18f, -8f);
            var label = labelGo.AddComponent<Text>();
            label.font = GameOverlayUI.SharedUiFont();
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = true;

            var hover = labelGo.AddComponent<RichNameHover>();

            return new OptionSlot
            {
                Root = go,
                Button = button,
                Label = label,
                Hover = hover,
                Index = index
            };
        }

        private void BuildTooltip(Transform parent)
        {
            var tip = new GameObject("Tooltip");
            tip.transform.SetParent(parent, false);
            _tooltipRoot = tip;
            _tooltipRect = tip.AddComponent<RectTransform>();
            _tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
            _tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltipRect.pivot = new Vector2(0f, 0f);
            _tooltipRect.sizeDelta = new Vector2(360f, 160f);

            var bg = tip.AddComponent<Image>();
            bg.sprite = GameOverlayUI.SharedUiSprite();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);
            bg.raycastTarget = false;

            var fitter = tip.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var v = tip.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(14, 14, 12, 12);
            v.spacing = 6f;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;

            _tooltipTitle = CreateLayoutText(tip.transform, "TipTitle", 20, FontStyle.Bold);
            _tooltipBody = CreateLayoutText(tip.transform, "TipBody", 18, FontStyle.Normal);
            _tooltipBody.color = new Color(0.85f, 0.88f, 0.92f, 1f);

            tip.SetActive(false);
        }

        private static Text CreateLayoutText(Transform parent, string name, int size, FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 320f;
            var text = go.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
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
            text.supportRichText = true;
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

        /// <summary>
        /// 在选项文本上按字符区间检测悬停，弹出遗物/员工效果。
        /// </summary>
        private sealed class RichNameHover : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
        {
            private Text _text;
            private readonly List<NameSpan> _spans = new List<NameSpan>();
            private Action<string, string, Vector2> _show;
            private Action _hide;
            private int _active = -1;

            private void Awake() => _text = GetComponent<Text>();

            public void SetSpans(
                List<NameSpan> spans,
                Action<string, string, Vector2> show,
                Action hide)
            {
                _spans.Clear();
                if (spans != null)
                    _spans.AddRange(spans);
                _show = show;
                _hide = hide;
                _active = -1;
            }

            public void RebuildHitAreas()
            {
                // 依赖 Text 已刷新；指针移动时再取 generator。
            }

            public void OnPointerMove(PointerEventData eventData)
            {
                if (_text == null || _spans.Count == 0)
                {
                    ClearActive();
                    return;
                }

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _text.rectTransform,
                        eventData.position,
                        eventData.pressEventCamera,
                        out var local))
                {
                    ClearActive();
                    return;
                }

                int charIndex = GetCharIndex(local);
                int hit = -1;
                for (int i = 0; i < _spans.Count; i++)
                {
                    var s = _spans[i];
                    if (charIndex >= s.Start && charIndex < s.Start + s.Length)
                    {
                        hit = i;
                        break;
                    }
                }

                // 未点在加粗名字上时，仍用第一条遗物/员工效果作为整行悬停说明。
                if (hit < 0)
                    hit = 0;

                if (_active == hit)
                {
                    _show?.Invoke(_spans[hit].Title, _spans[hit].Body, eventData.position);
                    return;
                }

                _active = hit;
                var span = _spans[hit];
                _show?.Invoke(span.Title, span.Body, eventData.position);
            }

            public void OnPointerExit(PointerEventData eventData) => ClearActive();

            private void ClearActive()
            {
                if (_active < 0) return;
                _active = -1;
                _hide?.Invoke();
            }

            private int GetCharIndex(Vector2 localPoint)
            {
                var gen = _text.cachedTextGenerator;
                if (gen == null || gen.characterCountVisible <= 0)
                    return -1;

                // Text generator uses its own space; convert using extents.
                var settings = _text.GetGenerationSettings(_text.rectTransform.rect.size);
                _text.cachedTextGenerator.Populate(_text.text, settings);
                gen = _text.cachedTextGenerator;

                var chars = gen.characters;
                // UICharInfo.cursorPos is top-left oriented in generator space.
                float unitsPerPixel = 1f / _text.pixelsPerUnit;
                var rect = _text.rectTransform.rect;
                // Align localPoint (pivot-relative) to generator coords (top-left of rect).
                Vector2 point = localPoint;
                point.x -= rect.xMin;
                point.y = rect.yMax - localPoint.y;

                int best = -1;
                float bestDist = float.MaxValue;
                int count = Mathf.Min(chars.Count, gen.characterCountVisible);
                for (int i = 0; i < count; i++)
                {
                    var c = chars[i];
                    float w = c.charWidth;
                    float h = settings.fontSize;
                    var r = new Rect(c.cursorPos.x * unitsPerPixel, (c.cursorPos.y - h) * unitsPerPixel, w * unitsPerPixel, h * unitsPerPixel);
                    if (r.Contains(point))
                        return i;

                    float cx = r.xMin + r.width * 0.5f;
                    float cy = r.yMin + r.height * 0.5f;
                    float d = (point.x - cx) * (point.x - cx) + (point.y - cy) * (point.y - cy);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = i;
                    }
                }

                // Only accept near hits so empty padding doesn't keep tooltips.
                return bestDist < 22f * 22f ? best : -1;
            }
        }
    }
}
