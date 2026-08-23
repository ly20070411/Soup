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
    /// 须选择选项后关闭（不可点空白处返回）。
    /// </summary>
    public sealed class EventPanelUI : MonoBehaviour
    {
        public const string CanvasName = "EventPanelCanvas";
        private const string AuthoredTemplateResourcePath = "UI/EventPanelAuthoredCanvas";

        private const int MaxOptions = 5;
        private const float AnimSeconds = 0.32f;
        private const float HiddenY = -1300f;
        private const float ShownY = 0f;
        private const int LayoutVersion = 17;
        // Near full-screen parchment; content sits inside with margin.
        private const float SheetWidth = 1912f;
        private const float SheetHeight = 1060f;
        // 羊皮 733x511 拉到 SheetHeight：书写区约从贴图顶 80px 起 → 约 166 sheet-px；再留余量。
        private const float ParchmentTopSafe = 220f;
        private const float ParchmentBottomSafe = 160f;
        // 描述每行提前约 1 个汉字换行（相对视口宽度内缩一格）。
        private const float DescWrapOneCharInset = 36f;
        private const float IllustrationColumnMaxX = 0.48f;
        private const float RightColumnMinX = 0.50f;
        /// <summary>事件插画相对布局参考区内缩（外框已移除，默认不内缩）。</summary>
        private const float IllustrationArtInset = 0f;
        /// <summary>场景里手调外框的最小有效边长；低于此值视为未配置，走自动布局。</summary>
        private const float MinAuthoredIllustrationSide = 64f;
        private const float OptionFontSize = 24;
        private const float OptionMinHeight = 68f;
        private const float OptionPreferredWidth = 380f;
        private const float OptionAnchorInsetX = 0.14f;

        [SerializeField]
        [Tooltip("勾选后使用场景里的 EventPanelCanvas，运行时不再强制改 Sheet 尺寸。")]
        private bool useAuthoredLayout;

        [SerializeField]
        private int authoredLayoutVersion;

        [SerializeField]
        [Tooltip("勾选后不再自动改插画外框位置/大小；在 EventPanelCanvas 里直接调 IllustrationFrame / Illustration 即可。")]
        private bool manualIllustrationLayout = true;

        private Canvas _canvas;
        private CanvasGroup _dimGroup;
        private RectTransform _sheetRect;
        private CanvasGroup _sheetGroup;
        private Image _sheetBackground;
        private Text _titleText;
        private Text _descText;
        private ScrollRect _descScroll;
        private Image _illustrationImage;
        private Image _illustrationFrame;
        private RectTransform _illustrationColumn;
        private Text _illustrationHint;
        private readonly OptionSlot[] _options = new OptionSlot[MaxOptions];
        private GameObject _tooltipRoot;
        private Text _tooltipTitle;
        private Text _tooltipBody;
        private RectTransform _tooltipRect;

        private bool _built;
        private int _builtLayoutVersion;
        private bool _open;
        private bool _animating;
        private Coroutine _animCo;
        private Coroutine _afterChoiceCo;
        private Coroutine _layoutCo;
        private Action<string> _toast;

        public bool UseAuthoredLayout => useAuthoredLayout;
        public bool ManualIllustrationLayout => manualIllustrationLayout;
        public RectTransform SheetRect => _sheetRect;
        public Canvas PanelCanvas => _canvas;

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
                if (_layoutCo != null)
                    StopCoroutine(_layoutCo);
                _layoutCo = StartCoroutine(ApplyLayoutAfterVisible());
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
            if (!IsUiBindHealthy())
            {
                TearDownBuiltUi();
                EnsureBuilt();
            }

            var pending = EventManager.Instance != null ? EventManager.Instance.PendingEvent : null;
            if (pending == null)
                return false;

            if (!IsUiBindHealthy())
                return false;

            Populate(pending);
            SetVisibleRoot(true);
            _open = true;

            if (_layoutCo != null)
                StopCoroutine(_layoutCo);
            _layoutCo = StartCoroutine(ApplyLayoutAfterVisible());

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
            {
                _descText.text = item.Description ?? string.Empty;
                RefreshDescriptionLayout();
            }

            if (_illustrationHint != null && !useAuthoredLayout)
                _illustrationHint.text = "插画（待定）";

            if (!ShouldUseManualIllustrationLayout())
            {
                NormalizeIllustrationHierarchy();
                ApplyEventIllustrationFrameArt();
                ApplyEventIllustration(item);
                LayoutIllustrationPort();
            }
            else
            {
                ApplyEventIllustration(item);
            }

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
            if (!useAuthoredLayout)
                ApplyOptionsPresentation();
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < MaxOptions; i++)
                _options[i]?.Hover?.RebuildHitAreas();
        }

        private IEnumerator ApplyLayoutAfterVisible()
        {
            yield return null;
            _layoutCo = null;
            Canvas.ForceUpdateCanvases();
            if (!ShouldUseManualIllustrationLayout())
            {
                NormalizeIllustrationHierarchy();
                ApplyEventIllustrationFrameArt();
                LayoutIllustrationPort();
            }

            var pending = EventManager.Instance != null ? EventManager.Instance.PendingEvent : null;
            if (pending != null)
                ApplyEventIllustration(pending);
            if (!useAuthoredLayout)
                ApplyOptionsPresentation();
            RefreshDescriptionLayout();
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
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            {
                HideTooltip();
                return;
            }

            DisableLegacyTooltip();
            HoverTooltipHub.Instance.ShowAtScreen(title, body, screenPos);
        }

        private void HideTooltip()
        {
            HoverTooltipHub.HideIfPresent();
            DisableLegacyTooltip();
        }

        private void DisableLegacyTooltip()
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
            EventIllustrationLayoutResolver.SetWorldPlaceholdersVisible(!visible);
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
            if (_built && _builtLayoutVersion != LayoutVersion)
            {
                useAuthoredLayout = false;
                TearDownBuiltUi();
            }

            if (_built && !IsUiBindHealthy())
            {
                useAuthoredLayout = false;
                TearDownBuiltUi();
            }

            if (FindBestAuthoredCanvasTransform() == null)
                TryInstantiateAuthoredTemplate();

            if (TryUseAuthoredCanvas())
                return;

            if (TryBuildInterLevelFormat())
                return;

            PurgeOrphanEventPanelCanvases();
            ClearBoundUiRefs();

            if (!_built)
            {
                Build();
                _built = true;
                _builtLayoutVersion = LayoutVersion;
                authoredLayoutVersion = LayoutVersion;
                useAuthoredLayout = false;
                manualIllustrationLayout = false;
                ConfigureDescriptionText();
                SetVisibleRoot(false);
                WireOptionClicks();
            }

            if (!useAuthoredLayout)
            {
                ApplyEventPanelArt();
                EnsureSheetFrameSize();
            }

            ApplyEventIllustrationFrameArt();
            NormalizeIllustrationHierarchy();
            LayoutIllustrationPort();
            if (!useAuthoredLayout)
                ApplyOptionsPresentation();
        }

        /// <summary>玩法场景：优先 Resources 预制体，失败时再程序化 Build。</summary>
        private bool TryBuildInterLevelFormat()
        {
            if (FindBestAuthoredCanvasTransform() == null)
                TryInstantiateAuthoredTemplate();

            if (TryBindAuthored() && IsUiBindHealthy())
            {
                FinishAuthoredEventPanelSetup();
                return true;
            }

            PurgeOrphanEventPanelCanvases();
            ClearBoundUiRefs();

            Build();
            _built = true;
            _builtLayoutVersion = LayoutVersion;
            authoredLayoutVersion = LayoutVersion;
            useAuthoredLayout = true;
            manualIllustrationLayout = true;

            ConfigureDescriptionText();
            WireOptionClicks();
            ApplyEventPanelArt();
            ApplyEventIllustrationFrameArt();
            NormalizeIllustrationHierarchy();
            LayoutIllustrationPort();
            SetVisibleRoot(false);
            return true;
        }

        /// <summary>绑定场景/预制体画布；运行时不改 Rect，只接事件数据。</summary>
        private void FinishAuthoredEventPanelSetup()
        {
            authoredLayoutVersion = LayoutVersion;
            _built = true;
            _builtLayoutVersion = LayoutVersion;
            ApplyAuthoredLayoutFlagsFromCanvas(_canvas != null ? _canvas.transform : null);

            ConfigureDescriptionText();
            WireOptionClicks();
            ApplyEventIllustrationFrameArt();
            if (!ShouldUseManualIllustrationLayout())
                NormalizeIllustrationHierarchy();
            LayoutIllustrationPort();
            if (!useAuthoredLayout)
                ApplyOptionsPresentation();
            EnsureEventPanelTextFonts();
            if (Application.isPlaying && !_open)
                SetVisibleRoot(false);
        }


        private void ApplyInterLevelLayoutData(EventPanelAuthoredLayoutData data)
        {
            if (data == null || _sheetRect == null)
                return;

            var body = _sheetRect.Find("Body") as RectTransform;
            if (body != null)
            {
                body.anchorMin = Vector2.zero;
                body.anchorMax = Vector2.one;
                body.pivot = new Vector2(0.5f, 0.5f);
                body.offsetMin = Vector2.zero;
                body.offsetMax = Vector2.zero;
                body.anchoredPosition = data.bodyAnchoredPosition;
                body.sizeDelta = data.bodySizeDelta;
            }

            NormalizeIllustrationHierarchy();
            ApplyAnchoredRect(_illustrationFrame?.rectTransform, data.illustrationFramePosition, data.illustrationFrameSize);
            ApplyAnchoredRect(_illustrationImage?.rectTransform, data.illustrationPosition, data.illustrationSize);
        }

        private static void ApplyAnchoredRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private bool IsUiBindHealthy()
        {
            return _canvas != null
                && _sheetRect != null
                && _titleText != null
                && _descText != null;
        }

        private void PurgeOrphanEventPanelCanvases()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name == CanvasName)
                    DestroyImmediateSafe(child.gameObject, immediate: true);
            }
        }

        /// <summary>场景里已有 EventPanelCanvas 时沿用编辑布局，避免运行时被程序化重建覆盖。</summary>
        private bool TryUseAuthoredCanvas()
        {
            if (!TryBindAuthored() || !IsUiBindHealthy())
            {
                ClearBoundUiRefs();
                return false;
            }

            useAuthoredLayout = true;
            FinishAuthoredEventPanelSetup();
            return true;
        }

        /// <summary>玩法场景无场景画布时，从 Resources 实例化关卡间同款 EventPanelCanvas。</summary>
        private bool TryInstantiateAuthoredTemplate()
        {
            if (FindBestAuthoredCanvasTransform() != null)
                return false;

            var prefab = Resources.Load<GameObject>(AuthoredTemplateResourcePath);
            if (prefab == null)
                return false;

            var instance = Instantiate(prefab, transform, false);
            instance.name = CanvasName;
            instance.transform.localScale = Vector3.one;

            if (instance.GetComponent<EventPanelAuthoredTemplateMarker>() == null)
                instance.gameObject.AddComponent<EventPanelAuthoredTemplateMarker>();

            return true;
        }

        private void ApplyAuthoredLayoutFlagsFromCanvas(Transform canvasTf)
        {
            if (canvasTf == null)
                return;

            var marker = canvasTf.GetComponent<EventPanelAuthoredTemplateMarker>();
            if (marker != null)
            {
                useAuthoredLayout = marker.UseAuthoredLayout;
                manualIllustrationLayout = marker.ManualIllustrationLayout;
                return;
            }

            manualIllustrationLayout = HasValidAuthoredIllustrationLayout();
        }

        private static void DestroyImmediateSafe(GameObject go, bool immediate = false)
        {
            if (go == null) return;
            if (immediate || !Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(go);
            else
                UnityEngine.Object.Destroy(go);
        }

        /// <summary>Editor：提交插画外框布局（自由画布或当前 IllustrationFrame）。</summary>
        public bool EditorCommitIllustrationLayout()
        {
            if (!TryBindAuthored() || !IsUiBindHealthy())
            {
                useAuthoredLayout = false;
                TearDownBuiltUi(immediate: true);
                Build();
                _built = true;
                _builtLayoutVersion = LayoutVersion;
            }

            NormalizeIllustrationHierarchy();
            if (_illustrationColumn == null || _illustrationFrame == null)
                return false;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_illustrationColumn);

            var frameRt = _illustrationFrame.rectTransform;
            if (frameRt.sizeDelta.x < 1f && frameRt.sizeDelta.y < 1f)
                return false;

            // 手动模式：只读 IllustrationFrame，绝不走自由画布解析，也不改外框/插画本身。
            if (manualIllustrationLayout)
            {
                WriteIllustrationLayoutMarker(frameRt.anchoredPosition, frameRt.sizeDelta);
                authoredLayoutVersion = LayoutVersion;
                useAuthoredLayout = true;
                ApplyEventIllustrationFrameArt();
                LayoutIllustrationPort();
                return true;
            }

            Vector2 center;
            float side;
            if (EventIllustrationLayoutResolver.TryResolve(
                    _sheetRect, _illustrationColumn, _canvas, out center, out side))
            {
                PlaceIllustrationSquare(frameRt, center.x, center.y, side, 0f);
                PlaceIllustrationSquare(_illustrationImage?.rectTransform, center.x, center.y, side, IllustrationArtInset);
                WriteIllustrationLayoutMarker(frameRt.anchoredPosition, frameRt.sizeDelta);
            }
            else
            {
                WriteIllustrationLayoutMarker(frameRt.anchoredPosition, frameRt.sizeDelta);
            }

            manualIllustrationLayout = true;
            authoredLayoutVersion = LayoutVersion;
            useAuthoredLayout = true;
            ApplyEventIllustrationFrameArt();
            LayoutIllustrationPort();
            return true;
        }

        private void WriteIllustrationLayoutMarker(Vector2 position, Vector2 size)
        {
            if (_illustrationColumn == null)
                return;

            var markerTf = FindDeepTransform(_illustrationColumn, EventIllustrationLayoutResolver.LayoutMarkerName);
            RectTransform marker;
            if (markerTf == null)
            {
                var go = new GameObject(EventIllustrationLayoutResolver.LayoutMarkerName, typeof(RectTransform));
                marker = go.GetComponent<RectTransform>();
                marker.SetParent(_illustrationColumn, false);
            }
            else
            {
                marker = markerTf as RectTransform;
            }

            if (marker == null)
                return;

            marker.anchorMin = Vector2.zero;
            marker.anchorMax = Vector2.zero;
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.anchoredPosition = position;
            marker.sizeDelta = size;
        }

        /// <summary>Editor：把自由画布里的「事件外框」烘焙为 EventIllustrationLayout 标记。</summary>
        public bool EditorBakeIllustrationLayoutFromFreeDraw()
        {
            // 保留旧入口；手动模式下仍只提交 IllustrationFrame。
            return EditorCommitIllustrationLayout();
        }

        /// <summary>Editor：重建场景内画布并展开预览。</summary>
        public void EditorRebuildAuthoredCanvas(bool showForEditing)
        {
            useAuthoredLayout = false;
            manualIllustrationLayout = false;
            TearDownBuiltUi(immediate: true);
            Build();
            _built = true;
            _builtLayoutVersion = LayoutVersion;
            authoredLayoutVersion = LayoutVersion;
            ConfigureDescriptionText();
            ApplyEventPanelArt();
            EnsureSheetFrameSize();
            WireOptionClicks();
            ApplyEventIllustrationFrameArt();
            NormalizeIllustrationHierarchy();
            LayoutIllustrationPort();
            var previewSprite = EventIllustrationLibrary.Load()?.Resolve("blessing_goddess_1");
            if (previewSprite != null && _illustrationImage != null)
            {
                _illustrationImage.sprite = previewSprite;
                _illustrationImage.color = Color.white;
                _illustrationImage.preserveAspect = false;
                if (_illustrationHint != null)
                    _illustrationHint.gameObject.SetActive(false);
            }
            ApplyOptionsPresentation();

            if (_titleText != null)
                _titleText.text = "祝福女神";
            if (_descText != null)
            {
                _descText.text = "预览：羊皮铺满屏幕，插画与选项已缩小收在框内。长描述会自动换行，超出区域可滚动，不会画出边框。";
                RefreshDescriptionLayout();
            }
            for (int i = 0; i < MaxOptions; i++)
            {
                var slot = _options[i];
                if (slot == null || slot.Root == null) continue;
                slot.Root.SetActive(i < 2);
                if (slot.Label != null && i < 2)
                    slot.Label.text = i == 0
                        ? "我希望能找到更多采集物——获得 丰饶祝福"
                        : "我希望手下人干劲满满——消除所有 疲倦，获得两个 激励";
            }

            if (showForEditing)
            {
                SetVisibleRoot(true);
                SetAnimState(0.72f, ShownY);
            }
            else
            {
                SetVisibleRoot(false);
                SetAnimState(0f, HiddenY);
            }

            useAuthoredLayout = true;
            manualIllustrationLayout = true;
        }

        private bool TryBindAuthored()
        {
            var canvasTf = FindBestAuthoredCanvasTransform();
            if (canvasTf == null) return false;

            PruneExtraEventPanelCanvases(canvasTf);
            canvasTf.localScale = Vector3.one;

            _canvas = canvasTf.GetComponent<Canvas>();
            if (_canvas == null) return false;

            var dim = canvasTf.Find("Dim");
            _dimGroup = dim != null ? dim.GetComponent<CanvasGroup>() : null;

            var sheet = canvasTf.Find("Sheet");
            if (sheet == null) return false;
            _sheetRect = sheet as RectTransform ?? sheet.GetComponent<RectTransform>();
            _sheetGroup = sheet.GetComponent<CanvasGroup>();
            _sheetBackground = sheet.GetComponent<Image>();

            _titleText = FindDeep<Text>(sheet, "Title");
            _descText = FindDeep<Text>(sheet, "Description");
            _descScroll = FindDeep<ScrollRect>(sheet, "DescViewport");
            if (_descScroll == null && _descText != null)
                _descScroll = _descText.GetComponentInParent<ScrollRect>();
            _illustrationImage = FindDeep<Image>(sheet, "Illustration");
            _illustrationFrame = FindDeep<Image>(sheet, "IllustrationFrame");
            _illustrationColumn = FindDeepTransform(sheet, "IllustrationColumn") as RectTransform;
            if (_illustrationColumn == null && _illustrationImage != null)
                _illustrationColumn = _illustrationImage.transform.parent as RectTransform;
            _illustrationHint = FindDeep<Text>(sheet, "IllustHint");

            var optionsRoot = FindDeepTransform(sheet, "Options");
            for (int i = 0; i < MaxOptions; i++)
            {
                Transform opt = optionsRoot != null ? optionsRoot.Find($"Option{i}") : FindDeepTransform(sheet, $"Option{i}");
                if (opt == null)
                {
                    _options[i] = null;
                    continue;
                }

                var label = opt.Find("Label") != null ? opt.Find("Label").GetComponent<Text>() : opt.GetComponentInChildren<Text>(true);
                var hover = label != null ? label.GetComponent<RichNameHover>() : null;
                if (label != null && hover == null)
                    hover = label.gameObject.AddComponent<RichNameHover>();

                _options[i] = new OptionSlot
                {
                    Root = opt.gameObject,
                    Button = opt.GetComponent<Button>(),
                    Label = label,
                    Hover = hover,
                    Index = i
                };
            }

            var tip = canvasTf.Find("Tooltip");
            if (tip != null)
            {
                _tooltipRoot = tip.gameObject;
                _tooltipRect = tip as RectTransform ?? tip.GetComponent<RectTransform>();
                _tooltipTitle = FindDeep<Text>(tip, "TipTitle");
                _tooltipBody = FindDeep<Text>(tip, "TipBody");
            }

            EnsureEventPanelTextFonts();

            if (dim != null)
            {
                var dimBtn = dim.GetComponent<Button>();
                if (dimBtn != null)
                {
                    dimBtn.onClick.RemoveAllListeners();
                    dimBtn.enabled = false;
                }
            }

            if (!IsUiBindHealthy())
            {
                ClearBoundUiRefs();
                return false;
            }

            return true;
        }

        private void ClearBoundUiRefs()
        {
            _canvas = null;
            _dimGroup = null;
            _sheetRect = null;
            _sheetGroup = null;
            _sheetBackground = null;
            _titleText = null;
            _descText = null;
            _descScroll = null;
            _illustrationImage = null;
            _illustrationFrame = null;
            _illustrationColumn = null;
            _illustrationHint = null;
            _tooltipRoot = null;
            _tooltipTitle = null;
            _tooltipBody = null;
            _tooltipRect = null;
            for (int i = 0; i < _options.Length; i++)
                _options[i] = default;
            _built = false;
            _open = false;
            _animating = false;
        }

        /// <summary>场景里可能残留多个 EventPanelCanvas，取当前可见、结构完整的那一个。</summary>
        private Transform FindBestAuthoredCanvasTransform()
        {
            Transform best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name != CanvasName) continue;

                int score = ScoreAuthoredCanvas(child);
                if (score <= bestScore) continue;
                bestScore = score;
                best = child;
            }

            return best;
        }

        private static int ScoreAuthoredCanvas(Transform canvasTf)
        {
            int score = canvasTf.GetSiblingIndex();
            if (canvasTf.gameObject.activeSelf) score += 1000;
            if (canvasTf.localScale.sqrMagnitude > 0.01f) score += 100;

            var sheet = canvasTf.Find("Sheet");
            if (sheet == null) return score;

            score += 50;
            if (FindDeep<Text>(sheet, "Title") != null) score += 20;
            if (FindDeepTransform(sheet, "IllustrationFrame") != null) score += 10;
            return score;
        }

        private void PruneExtraEventPanelCanvases(Transform keep)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != CanvasName || child == keep) continue;
                DestroyImmediateSafe(child.gameObject, immediate: true);
            }
        }

        /// <summary>Editor：删除重复画布，保留当前最佳并应用外框。</summary>
        public void EditorCleanupDuplicateCanvases()
        {
            if (!TryBindAuthored())
            {
                Debug.LogWarning("[事件] 未找到可用的 EventPanelCanvas。");
                return;
            }

            useAuthoredLayout = true;
            authoredLayoutVersion = LayoutVersion;
            _built = true;
            _builtLayoutVersion = LayoutVersion;
            ConfigureDescriptionText();
            WireOptionClicks();
            NormalizeIllustrationHierarchy();
            ApplyEventIllustrationFrameArt();
            LayoutIllustrationPort();
        }

        private void WireOptionClicks()
        {
            for (int i = 0; i < MaxOptions; i++)
            {
                var slot = _options[i];
                if (slot == null || slot.Button == null) continue;
                int index = i;
                slot.Button.onClick.RemoveAllListeners();
                slot.Button.onClick.AddListener(() => OnOptionClicked(index));
            }
        }

        private static T FindDeep<T>(Transform root, string name) where T : Component
        {
            var tf = FindDeepTransform(root, name);
            return tf != null ? tf.GetComponent<T>() : null;
        }

        private static Transform FindDeepTransform(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeepTransform(root.GetChild(i), name);
                if (hit != null) return hit;
            }

            return null;
        }

        private void TearDownBuiltUi(bool immediate = false)
        {
            if (_animCo != null)
            {
                StopCoroutine(_animCo);
                _animCo = null;
            }

            if (_layoutCo != null)
            {
                StopCoroutine(_layoutCo);
                _layoutCo = null;
            }

            if (_canvas != null)
            {
                if (immediate)
                    DestroyImmediate(_canvas.gameObject);
                else
                    Destroy(_canvas.gameObject);
            }

            ClearBoundUiRefs();
            _builtLayoutVersion = 0;
        }

        private void EnsureSheetFrameSize()
        {
            if (_sheetRect == null) return;
            // Centered near-fullscreen frame (compatible with slide-in Y animation).
            _sheetRect.anchorMin = new Vector2(0.5f, 0.5f);
            _sheetRect.anchorMax = new Vector2(0.5f, 0.5f);
            _sheetRect.pivot = new Vector2(0.5f, 0.5f);
            _sheetRect.sizeDelta = new Vector2(SheetWidth, SheetHeight);
        }

        private void ApplyEventPanelArt()
        {
            if (_sheetBackground == null) return;
            var art = GameArtLibrary.Load();
            if (art == null || art.EventPanelBackground == null) return;

            _sheetBackground.sprite = art.EventPanelBackground;
            _sheetBackground.color = Color.white;
            // Must stretch: parchment aspect would otherwise show as a thin strip.
            _sheetBackground.preserveAspect = false;
            _sheetBackground.type = Image.Type.Simple;
            _sheetBackground.raycastTarget = true;
        }

        private void ApplyEventIllustrationFrameArt()
        {
            HideIllustrationFrame();
        }

        private void HideIllustrationFrame()
        {
            if (_illustrationFrame == null) return;

            _illustrationFrame.sprite = null;
            _illustrationFrame.enabled = false;
            _illustrationFrame.raycastTarget = false;
        }

        private void NormalizeIllustrationHierarchy()
        {
            if (_sheetRect == null) return;

            _illustrationColumn = FindDeepTransform(_sheetRect, "IllustrationColumn") as RectTransform;
            if (_illustrationFrame == null)
                _illustrationFrame = FindDeep<Image>(_sheetRect, "IllustrationFrame");
            if (_illustrationImage == null)
                _illustrationImage = FindDeep<Image>(_sheetRect, "Illustration");
            _illustrationHint = FindDeep<Text>(_sheetRect, "IllustHint");

            if (_illustrationColumn == null) return;

            if (_illustrationFrame != null)
            {
                DestroyStraySpriteRendererChildren(_illustrationFrame.transform);
                if (_illustrationFrame.transform.parent != _illustrationColumn)
                    _illustrationFrame.transform.SetParent(_illustrationColumn, false);
                HideIllustrationFrame();
            }

            if (_illustrationImage != null && _illustrationImage != _illustrationFrame)
            {
                if (_illustrationImage.transform.parent != _illustrationColumn)
                    _illustrationImage.transform.SetParent(_illustrationColumn, false);
            }
        }

        private static void DestroyStraySpriteRendererChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child.GetComponent<SpriteRenderer>() != null)
                    DestroyImmediateSafe(child.gameObject);
            }
        }

        private void LayoutIllustrationPort()
        {
            if (_illustrationColumn == null) return;

            if (ShouldUseManualIllustrationLayout())
            {
                EnsureManualIllustrationUnmasked();
                ApplyIllustrationLayerOrder();
                return;
            }

            if (_illustrationColumn.GetComponent<RectMask2D>() == null)
                _illustrationColumn.gameObject.AddComponent<RectMask2D>();

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_illustrationColumn);

            float parentW = _illustrationColumn.rect.width;
            float parentH = _illustrationColumn.rect.height;
            if (parentW < 1f || parentH < 1f)
                return;

            float centerX;
            float centerY;
            float side;
            if (EventIllustrationLayoutResolver.TryResolve(
                    _sheetRect, _illustrationColumn, _canvas, out var authoredCenter, out side))
            {
                centerX = authoredCenter.x;
                centerY = authoredCenter.y;
            }
            else
            {
                side = Mathf.Max(parentW, parentH);
                centerX = parentW * 0.5f;
                centerY = parentH * 0.5f;
            }

            PlaceIllustrationSquare(_illustrationFrame?.rectTransform, centerX, centerY, side, 0f);
            PlaceIllustrationSquare(_illustrationImage?.rectTransform, centerX, centerY, side, IllustrationArtInset);
            ApplyIllustrationLayerOrder();
        }

        /// <summary>
        /// 手调外框常大于 IllustrationColumn（约 48% 宽），RectMask2D 会裁成左侧细条。
        /// 关卡间与玩法均需关闭裁剪，外框才能铺满羊皮区域。
        /// </summary>
        private void EnsureManualIllustrationUnmasked()
        {
            if (_illustrationColumn == null)
                return;

            var mask = _illustrationColumn.GetComponent<RectMask2D>();
            if (mask == null)
                return;

            if (Application.isPlaying)
                Destroy(mask);
            else
                DestroyImmediate(mask);
        }

        private bool ShouldUseManualIllustrationLayout()
        {
            return manualIllustrationLayout
                && useAuthoredLayout
                && HasValidAuthoredIllustrationLayout();
        }

        private bool HasValidAuthoredIllustrationLayout()
        {
            if (_illustrationFrame == null)
                return false;

            var size = _illustrationFrame.rectTransform.sizeDelta;
            return size.x >= MinAuthoredIllustrationSide && size.y >= MinAuthoredIllustrationSide;
        }

        private static void PlaceIllustrationSquare(
            RectTransform rect,
            float centerX,
            float centerY,
            float side,
            float inset)
        {
            if (rect == null) return;
            float s = Mathf.Max(8f, side - inset * 2f);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(centerX, centerY);
            rect.sizeDelta = new Vector2(s, s);
        }

        private void ApplyEventIllustration(EventItem item)
        {
            if (_illustrationImage == null) return;

            var sprite = EventIllustrationLibrary.Load()?.Resolve(item?.Id, item?.DisplayName);
            bool hasArt = sprite != null;
            if (hasArt)
            {
                _illustrationImage.sprite = sprite;
                _illustrationImage.color = Color.white;
                _illustrationImage.preserveAspect = false;
                _illustrationImage.type = Image.Type.Simple;
            }
            else
            {
                _illustrationImage.sprite = GameOverlayUI.SharedUiSprite();
                _illustrationImage.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
                _illustrationImage.preserveAspect = false;
                _illustrationImage.type = Image.Type.Simple;
            }

            _illustrationImage.raycastTarget = false;
            if (_illustrationHint != null)
                _illustrationHint.gameObject.SetActive(!hasArt);

            if (!ShouldUseManualIllustrationLayout())
                EnsureIllustrationImageFitsFrame();
        }

        /// <summary>自动布局时保证插画 Rect 与外框一致（避免链式弹事件时插画缩成精灵原尺寸）。</summary>
        private void EnsureIllustrationImageFitsFrame()
        {
            if (_illustrationFrame == null || _illustrationImage == null)
                return;

            var frameRt = _illustrationFrame.rectTransform;
            var imageRt = _illustrationImage.rectTransform;
            if (frameRt.sizeDelta.x < MinAuthoredIllustrationSide || frameRt.sizeDelta.y < MinAuthoredIllustrationSide)
                return;

            float side = Mathf.Max(frameRt.sizeDelta.x, frameRt.sizeDelta.y);
            PlaceIllustrationSquare(
                imageRt,
                frameRt.anchoredPosition.x,
                frameRt.anchoredPosition.y,
                side,
                IllustrationArtInset);
        }

        /// <summary>插画列置于文字列之上。</summary>
        private void ApplyIllustrationLayerOrder()
        {
            if (_illustrationColumn != null && _illustrationColumn.parent != null)
                _illustrationColumn.SetAsLastSibling();

            if (_illustrationImage != null)
                _illustrationImage.transform.SetAsLastSibling();

            if (_illustrationHint != null)
                _illustrationHint.transform.SetAsLastSibling();
        }

        private void ApplyOptionsPresentation()
        {
            if (useAuthoredLayout)
                return;

            RectTransform optionsRect = null;
            for (int i = 0; i < MaxOptions; i++)
            {
                var slot = _options[i];
                if (slot?.Root == null) continue;
                optionsRect = slot.Root.transform.parent as RectTransform;
                break;
            }

            if (optionsRect != null)
            {
                optionsRect.anchorMin = new Vector2(OptionAnchorInsetX, 0.02f);
                optionsRect.anchorMax = new Vector2(1f - OptionAnchorInsetX, 0.56f);
                optionsRect.offsetMin = Vector2.zero;
                optionsRect.offsetMax = Vector2.zero;

                var layout = optionsRect.GetComponent<VerticalLayoutGroup>();
                if (layout != null)
                {
                    layout.spacing = 20f;
                    layout.childAlignment = TextAnchor.UpperCenter;
                    layout.padding = new RectOffset(8, 8, 8, 8);
                }
            }

            if (_descScroll != null && _descScroll.viewport != null)
            {
                var viewportRect = _descScroll.viewport;
                viewportRect.anchorMin = new Vector2(0f, 0.58f);
                viewportRect.anchorMax = new Vector2(1f, 1f);
                viewportRect.offsetMin = new Vector2(4f, 4f);
                viewportRect.offsetMax = new Vector2(-4f, -8f);
            }

            var fitted = GameOverlayUI.FitArtButtonSize(OptionPreferredWidth, OptionMinHeight + 4f);
            for (int i = 0; i < MaxOptions; i++)
            {
                var slot = _options[i];
                if (slot?.Root == null) continue;

                var le = slot.Root.GetComponent<LayoutElement>();
                if (le == null)
                    le = slot.Root.AddComponent<LayoutElement>();
                le.minHeight = OptionMinHeight;
                le.preferredHeight = fitted.y;
                le.preferredWidth = OptionPreferredWidth;
                le.flexibleWidth = 0f;

                if (slot.Label != null)
                {
                    slot.Label.fontSize = Mathf.RoundToInt(OptionFontSize);
                    var labelRect = slot.Label.rectTransform;
                    labelRect.offsetMin = new Vector2(18f, 8f);
                    labelRect.offsetMax = new Vector2(-18f, -8f);
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

            var canvasGo = new GameObject(CanvasName);
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

            var sheetGo = new GameObject("Sheet");
            sheetGo.transform.SetParent(canvasGo.transform, false);
            _sheetRect = sheetGo.AddComponent<RectTransform>();
            EnsureSheetFrameSize();
            _sheetRect.anchoredPosition = new Vector2(0f, HiddenY);
            _sheetBackground = sheetGo.AddComponent<Image>();
            _sheetBackground.sprite = GameOverlayUI.SharedUiSprite();
            _sheetBackground.color = Color.white;
            _sheetBackground.preserveAspect = false;
            _sheetBackground.type = Image.Type.Simple;
            _sheetBackground.raycastTarget = true;
            _sheetGroup = sheetGo.AddComponent<CanvasGroup>();

            // Title sits below measured parchment rim (~166px writing start).
            _titleText = CreateText(sheetGo.transform, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(ParchmentTopSafe - 30f)), new Vector2(1100f, 40f),
                28, FontStyle.Bold, TextAnchor.MiddleCenter);
            _titleText.color = new Color(0.22f, 0.14f, 0.08f, 1f);

            var body = new GameObject("Body");
            body.transform.SetParent(sheetGo.transform, false);
            var bodyRect = body.AddComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            // Left/right past side rim; top clears title + rim so description is not covered.
            bodyRect.offsetMin = new Vector2(480f, ParchmentBottomSafe);
            bodyRect.offsetMax = new Vector2(-320f, -(ParchmentTopSafe + 36f));

            var illustColumnGo = new GameObject("IllustrationColumn");
            illustColumnGo.transform.SetParent(body.transform, false);
            _illustrationColumn = illustColumnGo.AddComponent<RectTransform>();
            _illustrationColumn.anchorMin = new Vector2(0f, 0f);
            _illustrationColumn.anchorMax = new Vector2(IllustrationColumnMaxX, 1f);
            _illustrationColumn.offsetMin = Vector2.zero;
            _illustrationColumn.offsetMax = Vector2.zero;

            var illust = new GameObject("Illustration");
            illust.transform.SetParent(illustColumnGo.transform, false);
            illust.AddComponent<RectTransform>();
            _illustrationImage = illust.AddComponent<Image>();
            _illustrationHint = CreateText(illust.transform, "IllustHint",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(240f, 72f),
                22, FontStyle.Normal, TextAnchor.MiddleCenter);
            _illustrationHint.text = "插画（待定）";

            var frameGo = new GameObject("IllustrationFrame");
            frameGo.transform.SetParent(illustColumnGo.transform, false);
            frameGo.AddComponent<RectTransform>();
            _illustrationFrame = frameGo.AddComponent<Image>();
            HideIllustrationFrame();

            var right = new GameObject("Right");
            right.transform.SetParent(body.transform, false);
            var rightRect = right.AddComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(RightColumnMinX, 0f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.offsetMin = new Vector2(12f, 0f);
            rightRect.offsetMax = Vector2.zero;

            illustColumnGo.transform.SetAsLastSibling();

            // 描述区：字号×2，自动换行；视口内滚动，绝不画出羊皮框。
            var descViewport = new GameObject("DescViewport");
            descViewport.transform.SetParent(right.transform, false);
            var viewportRect = descViewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 0.58f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = new Vector2(4f, 4f);
            viewportRect.offsetMax = new Vector2(-4f, -8f);
            var viewportBg = descViewport.AddComponent<Image>();
            viewportBg.color = new Color(1f, 1f, 1f, 0.01f);
            viewportBg.raycastTarget = true;
            descViewport.AddComponent<RectMask2D>();

            _descText = CreateText(descViewport.transform, "Description",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 40f),
                36, FontStyle.Normal, TextAnchor.UpperLeft);
            var descRect = _descText.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 1f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.pivot = new Vector2(0.5f, 1f);
            descRect.anchoredPosition = Vector2.zero;
            // sizeDelta.x 为负：换行宽度比视口少约一字。
            descRect.sizeDelta = new Vector2(-DescWrapOneCharInset, 40f);
            _descText.color = new Color(0.28f, 0.18f, 0.10f, 1f);
            ConfigureDescriptionText(_descText);
            var descFitter = _descText.gameObject.AddComponent<ContentSizeFitter>();
            descFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            descFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _descScroll = descViewport.AddComponent<ScrollRect>();
            _descScroll.content = descRect;
            _descScroll.viewport = viewportRect;
            _descScroll.horizontal = false;
            _descScroll.vertical = true;
            _descScroll.movementType = ScrollRect.MovementType.Clamped;
            _descScroll.scrollSensitivity = 28f;
            _descScroll.inertia = true;

            var optionsRoot = new GameObject("Options");
            optionsRoot.transform.SetParent(right.transform, false);
            var optionsRect = optionsRoot.AddComponent<RectTransform>();
            // 选项排在介绍文字正下方，居中略收窄。
            optionsRect.anchorMin = new Vector2(OptionAnchorInsetX, 0.02f);
            optionsRect.anchorMax = new Vector2(1f - OptionAnchorInsetX, 0.56f);
            optionsRect.offsetMin = Vector2.zero;
            optionsRect.offsetMax = Vector2.zero;
            var layout = optionsRoot.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(8, 8, 8, 8);

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
            le.minHeight = OptionMinHeight;
            var fitted = GameOverlayUI.FitArtButtonSize(OptionPreferredWidth, OptionMinHeight + 4f);
            le.preferredHeight = fitted.y;
            le.preferredWidth = OptionPreferredWidth;
            le.flexibleWidth = 0f;

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
            label.fontSize = Mathf.RoundToInt(OptionFontSize);
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

        private void EnsureEventPanelTextFonts()
        {
            DisableLegacyTooltip();
            var font = GameOverlayUI.SharedUiFont();
            if (font == null) return;

            for (int i = 0; i < MaxOptions; i++)
            {
                var label = _options[i]?.Label;
                if (label == null) continue;
                if (!SafeUiFont.IsUsable(label.font) || label.font.name == "Arial")
                    label.font = font;
                label.supportRichText = true;
            }
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
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void ConfigureDescriptionText() => ConfigureDescriptionText(_descText);

        private static void ConfigureDescriptionText(Text text)
        {
            if (text == null) return;
            text.fontSize = 36;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            // PreferredHeight 需要 Vertical Overflow；视觉裁剪由 Mask/ScrollRect 负责。
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.raycastTarget = false;
        }

        private void RefreshDescriptionLayout()
        {
            if (_descText == null) return;
            ConfigureDescriptionText(_descText);
            Canvas.ForceUpdateCanvases();
            var rect = _descText.rectTransform;
            float parentWidth = rect.rect.width + DescWrapOneCharInset;
            if (parentWidth < 1f && rect.parent is RectTransform parent)
                parentWidth = parent.rect.width;
            float wrapWidth = Mathf.Max(1f, parentWidth - DescWrapOneCharInset);
            float height = _descText.preferredHeight;
            if (wrapWidth > 1f)
            {
                var settings = _descText.GetGenerationSettings(new Vector2(wrapWidth, 0f));
                height = _descText.cachedTextGeneratorForLayout.GetPreferredHeight(_descText.text, settings)
                    / _descText.pixelsPerUnit;
            }

            rect.sizeDelta = new Vector2(-DescWrapOneCharInset, Mathf.Max(40f, height + 4f));
            if (_descScroll != null)
            {
                _descScroll.verticalNormalizedPosition = 1f;
                LayoutRebuilder.ForceRebuildLayoutImmediate(_descScroll.viewport);
            }
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
