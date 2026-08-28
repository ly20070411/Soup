using System.Collections.Generic;
using Soup.Employees;
using Soup.Relics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// Binds SampleScene FreeDraw chrome: resource counters, employee picker, relic bar.
    /// Gray-bar triangles page owned relics; zone switching stays on the world-space zone keys.
    /// </summary>
    public sealed class PlayAuthoredHud : MonoBehaviour
    {
        public const string RootName = "AuthoredHud";

        [Header("Resources (left)")]
        [SerializeField] private Text softValue;
        [SerializeField] private Text toughValue;
        [SerializeField] private Text solidValue;
        [SerializeField] private Text processedValue;
        [SerializeField] private Text cookedValue;

        [Header("Flavors (right)")]
        [SerializeField] private Text spicyValue;
        [SerializeField] private Text coldValue;
        [SerializeField] private Text sourValue;
        [SerializeField] private Text magicValue;

        [Header("Employee")]
        [SerializeField] private Image employeeAvatar;
        [SerializeField] private Text employeeCount;
        [SerializeField] private Text employeeSwitchHint;
        [SerializeField] private Button employeeAvatarButton;
        [SerializeField] private RectTransform employeePickerRoot;

        [Header("Relics")]
        [SerializeField] private Image[] relicSlots = new Image[0];
        [FormerlySerializedAs("zonePrevButton")]
        [SerializeField] private Button relicPrevButton;
        [FormerlySerializedAs("zoneNextButton")]
        [SerializeField] private Button relicNextButton;

        private readonly List<Button> _pickerButtons = new List<Button>();
        private readonly List<RelicItem> _uniqueRelics = new List<RelicItem>(16);
        private readonly List<Text> _relicStackLabels = new List<Text>(16);
        private bool _pickerOpen;
        private int _relicStart;
        private static Sprite _relicPlaceholderSprite;
        private RelicHudTooltip _relicTooltip;
        private bool _relicHoversReady;
        private bool _relicClicksReady;

        private float _designScreenHeight = 1080f;

        private void Awake()
        {
            AutoBindIfNeeded();
            NormalizeTopAnchoredHud();
            WireButtons();
            EnsureRelicTooltipUi();
            EnsureRelicSlotHovers();
            EnsureRelicSlotClicks();
            EnsureFlavorAndEmployeeHovers();
            EnsureResourceFlavorIcons();
            EnsureEmployeeSwitchHint();
        }

        /// <summary>
        /// 打包后在 Windows 高 DPI / 非 16:9 分辨率下，场景里所有 HUD 元素都锚定 (0,0)
        /// 左下角 + 1920×1080 绝对坐标，会整体下移/偏移。这里把它们从「左下角绝对坐标」
        /// 改为「顶部锚定 + 相对屏幕顶部的偏移」，使顶部资源栏贴合屏幕顶部，不随分辨率漂移。
        /// 保持水平坐标不变（横向铺满的设计），只校正垂直方向。
        /// </summary>
        private void NormalizeTopAnchoredHud()
        {
            var hudRt = transform as RectTransform;
            if (hudRt == null) return;
            if (hudRt.parent == null) return;

            // 设计分辨率高度。元素 y 坐标从 1920×1080 设计左下角量起。
            float designH = _designScreenHeight;
            // 保护：若某元素已在运行时被改为自适应锚点则跳过。
            foreach (Transform child in hudRt)
            {
                var rt = child as RectTransform;
                if (rt == null) continue;
                // 只处理仍锚定左下角 (0,0) 的 HUD 元素。
                if (rt.anchorMin != Vector2.zero || rt.anchorMax != Vector2.zero)
                    continue;

                // 设计坐标：y 从容器底部量起。改为顶部锚定后，anchoredPosition.y 的基准是
                // 容器顶部；正值表示在顶部「上方」（屏幕外），负值才是顶部「下方」。
                // 因此新 y = 原始y - designH（负值）。
                float anchorY = rt.anchoredPosition.y - designH;
                // 顶部锚定但保留水平锚点 (0)。x 保持不变。
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, anchorY);
            }
        }

        private void OnEnable()
        {
            EmployeeAssignSelection.Changed -= OnEmployeeSelectionChanged;
            EmployeeAssignSelection.Changed += OnEmployeeSelectionChanged;
        }

        private void OnDisable()
        {
            EmployeeAssignSelection.Changed -= OnEmployeeSelectionChanged;
            HideRelicTooltip();
            if (FindObjectOfType<RelicPreviewOverlayUI>() != null)
                RelicPreviewOverlayUI.Ensure().Hide();
        }

        private void Start()
        {
            EmployeeAssignSelection.EnsureValid();
            SetPickerOpen(false);
            RefreshAll();
        }

        private void Update()
        {
            RefreshResources();
            RefreshEmployeeCount();
            RefreshRelics();
        }

        public void RefreshAll()
        {
            RefreshResources();
            RefreshEmployee();
            RefreshRelics();
        }

        private void OnEmployeeSelectionChanged()
        {
            RefreshEmployee();
            RefreshStationLabels();
        }

        private void WireButtons()
        {
            if (relicPrevButton != null)
            {
                relicPrevButton.onClick.RemoveListener(OnRelicPrev);
                relicPrevButton.onClick.AddListener(OnRelicPrev);
            }

            if (relicNextButton != null)
            {
                relicNextButton.onClick.RemoveListener(OnRelicNext);
                relicNextButton.onClick.AddListener(OnRelicNext);
            }

            if (employeeAvatarButton != null)
            {
                employeeAvatarButton.onClick.RemoveListener(TogglePicker);
                employeeAvatarButton.onClick.AddListener(TogglePicker);
            }
        }

        private void OnRelicPrev() => CycleRelics(-1);

        private void OnRelicNext() => CycleRelics(+1);

        private void CycleRelics(int direction)
        {
            int slotCount = RelicSlotCount;
            int ownedCount = OwnedRelicTypeCount;
            int maxStart = RelicMaxStart(ownedCount, slotCount);
            if (maxStart <= 0 || slotCount <= 0) return;

            // 一次翻一整排（当前可见格数），不是逐个挪。
            int step = RelicPageStep(slotCount);
            int next = _relicStart + direction * step;
            // 对齐到排首，避免停在半排。
            next = Mathf.FloorToInt(next / (float)step) * step;
            _relicStart = Mathf.Clamp(next, 0, maxStart);
            RefreshRelics();
        }

        private void TogglePicker() => SetPickerOpen(!_pickerOpen);

        private void SetPickerOpen(bool open)
        {
            _pickerOpen = open;
            if (employeePickerRoot != null)
            {
                employeePickerRoot.gameObject.SetActive(open);
                if (open)
                {
                    EnsurePickerDrawsAboveZoneSwitch();
                    employeePickerRoot.SetAsLastSibling();
                }
            }

            if (open)
                RebuildPicker();
        }

        /// <summary>
        /// Zone switch lives on SystemHud (later sibling of FreeDraw). Give the picker
        /// its own canvas so it paints / receives clicks above those side buttons.
        /// </summary>
        private void EnsurePickerDrawsAboveZoneSwitch()
        {
            if (employeePickerRoot == null) return;

            var canvas = employeePickerRoot.GetComponent<Canvas>();
            if (canvas == null)
                canvas = employeePickerRoot.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 150;

            if (employeePickerRoot.GetComponent<GraphicRaycaster>() == null)
                employeePickerRoot.gameObject.AddComponent<GraphicRaycaster>();
        }

        private void RebuildPicker()
        {
            if (employeePickerRoot == null) return;

            for (int i = employeePickerRoot.childCount - 1; i >= 0; i--)
            {
                var child = employeePickerRoot.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            _pickerButtons.Clear();
            var em = EmployeeManager.Instance;
            if (em == null) return;

            // Match the main employee circle on the HUD (EmployeeFrame / Avatar).
            Vector2 frameSize = new Vector2(136f, 134f);
            Vector2 iconSize = new Vector2(48f, 48f);
            Sprite frameSprite = null;
            var frame = FindNamed(transform, "EmployeeFrame");
            if (frame != null)
            {
                var frameRt = frame as RectTransform ?? frame.GetComponent<RectTransform>();
                if (frameRt != null && frameRt.sizeDelta.x > 1f && frameRt.sizeDelta.y > 1f)
                    frameSize = frameRt.sizeDelta;

                var frameImage = frame.GetComponent<Image>();
                if (frameImage != null && frameImage.sprite != null)
                    frameSprite = frameImage.sprite;
            }

            if (employeeAvatar != null)
            {
                var avatarRt = employeeAvatar.rectTransform;
                if (avatarRt != null && avatarRt.sizeDelta.x > 1f && avatarRt.sizeDelta.y > 1f)
                    iconSize = avatarRt.sizeDelta;
            }

            if (frameSprite == null)
            {
                var art = GameArtLibrary.Load();
                if (art != null && art.CircleFrame != null)
                    frameSprite = art.CircleFrame;
            }

            // Compact: tiny gap between stacked circles (was ~8px on 64px slots).
            const float gap = 4f;
            float slotStep = Mathf.Max(frameSize.y, frameSize.x) + gap;
            float y = 0f;
            int count = 0;
            for (int i = 0; i < em.All.Count; i++)
            {
                var type = em.All[i];
                if (type == null || !type.CanPlayerAssign) continue;
                if (EmployeeAssignSelection.Current != null && type.Id == EmployeeAssignSelection.Current.Id)
                    continue;

                var go = new GameObject($"Pick_{type.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(employeePickerRoot, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = frameSize;
                rect.anchoredPosition = new Vector2(0f, y);

                var image = go.GetComponent<Image>();
                image.sprite = frameSprite != null ? frameSprite : GetWhiteSprite();
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = true;

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = iconSize;
                var iconImage = iconGo.GetComponent<Image>();
                iconImage.raycastTarget = false;
                iconImage.preserveAspect = true;
                if (type.Icon != null)
                {
                    iconImage.sprite = type.Icon;
                    // Authored employee art is already colored — do not multiply by Tint.
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = new Color(1f, 1f, 1f, 0f);
                }

                var button = go.GetComponent<Button>();
                var captured = type;
                button.onClick.AddListener(() =>
                {
                    EmployeeAssignSelection.Select(captured);
                    SetPickerOpen(false);
                });

                var tip = go.GetComponent<UiHoverTooltip>();
                if (tip == null)
                    tip = go.AddComponent<UiHoverTooltip>();
                HoverTooltipText.Employee(captured, out string tipTitle, out string tipBody);
                tip.Bind(tipTitle, tipBody);

                _pickerButtons.Add(button);
                y -= slotStep;
                count++;
            }

            float width = Mathf.Max(frameSize.x, 80f);
            float height = count > 0 ? count * slotStep : frameSize.y;
            employeePickerRoot.sizeDelta = new Vector2(width, height);
        }

        private void RefreshResources()
        {
            var store = ResourceStore.Instance;
            if (store == null) return;

            SetCount(softValue, store.Soft);
            SetCount(toughValue, store.Tough);
            SetCount(solidValue, store.Solid);
            SetCount(processedValue, store.Processed);
            SetCount(cookedValue, store.Cooked);
            SetCount(spicyValue, store.Spicy);
            SetCount(coldValue, store.Cold);
            SetCount(sourValue, store.Sour);
            SetCount(magicValue, store.Magic);
        }

        private void RefreshEmployee()
        {
            EmployeeAssignSelection.EnsureValid();
            var type = EmployeeAssignSelection.Current;
            if (employeeAvatar != null)
            {
                if (type != null && type.Icon != null)
                {
                    employeeAvatar.sprite = type.Icon;
                    // Authored employee art is already colored — do not multiply by Tint.
                    employeeAvatar.color = Color.white;
                    employeeAvatar.enabled = true;
                }
                else
                {
                    employeeAvatar.sprite = null;
                    employeeAvatar.color = new Color(1f, 1f, 1f, 0f);
                    employeeAvatar.enabled = true;
                }
            }

            RefreshEmployeeCount();
            if (_pickerOpen)
                RebuildPicker();
        }

        private void RefreshEmployeeCount()
        {
            var em = EmployeeManager.Instance;
            var type = EmployeeAssignSelection.Current;
            if (employeeCount == null) return;
            if (em == null || type == null)
            {
                employeeCount.text = "0/0";
                return;
            }

            employeeCount.text = $"{FormatCount(em.GetFree(type))}/{FormatCount(em.GetOwned(type))}";
        }

        private static string FormatCount(int value) =>
            Mathf.Clamp(value, 0, 999999).ToString();

        private int RelicSlotCount => relicSlots != null ? relicSlots.Length : 0;

        /// <summary>Unique relic types owned (duplicates stack into ×N on one slot).</summary>
        private int OwnedRelicTypeCount
        {
            get
            {
                RebuildUniqueRelics();
                return _uniqueRelics.Count;
            }
        }

        private static int RelicPageStep(int slotCount) => Mathf.Max(1, slotCount);

        /// <summary>最后一页起点：按整排对齐（例如 6 格时为 0, 6, 12…）。</summary>
        private static int RelicMaxStart(int ownedTypeCount, int slotCount)
        {
            if (slotCount <= 0 || ownedTypeCount <= slotCount)
                return 0;
            int pages = Mathf.CeilToInt(ownedTypeCount / (float)slotCount);
            return (pages - 1) * slotCount;
        }

        private void RebuildUniqueRelics()
        {
            _uniqueRelics.Clear();
            RelicManager.Instance?.CopyOwnedUnique(_uniqueRelics);
        }

        private void RefreshRelics()
        {
            if (relicSlots == null) return;
            EnsureRelicStackLabels();
            RebuildUniqueRelics();

            int ownedTypeCount = _uniqueRelics.Count;
            int slotCount = relicSlots.Length;
            int maxStart = RelicMaxStart(ownedTypeCount, slotCount);
            int step = RelicPageStep(slotCount);
            // 持有变化后仍对齐到整排。
            _relicStart = Mathf.Clamp((_relicStart / step) * step, 0, maxStart);

            if (relicPrevButton != null)
                relicPrevButton.interactable = _relicStart > 0;
            if (relicNextButton != null)
                relicNextButton.interactable = _relicStart < maxStart;

            for (int i = 0; i < slotCount; i++)
            {
                var slot = relicSlots[i];
                if (slot == null) continue;

                int relicIndex = _relicStart + i;
                RelicItem relic = relicIndex < ownedTypeCount ? _uniqueRelics[relicIndex] : null;
                Text stackLabel = i < _relicStackLabels.Count ? _relicStackLabels[i] : null;

                if (relic != null)
                {
                    slot.sprite = relic.Icon != null ? relic.Icon : GetRelicPlaceholderSprite();
                    slot.color = Color.white;
                    slot.enabled = true;
                    slot.preserveAspect = true;

                    int stacks = RelicManager.Instance != null
                        ? RelicManager.Instance.CountOwned(relic)
                        : 1;
                    if (stackLabel != null)
                    {
                        stackLabel.gameObject.SetActive(stacks > 1);
                        stackLabel.text = stacks > 1 ? $"×{stacks}" : string.Empty;
                    }
                }
                else
                {
                    slot.sprite = null;
                    slot.color = new Color(1f, 1f, 1f, 0f);
                    slot.enabled = true;
                    if (stackLabel != null)
                    {
                        stackLabel.text = string.Empty;
                        stackLabel.gameObject.SetActive(false);
                    }
                }
            }
        }

        public void ShowRelicTooltip(int slotIndex, RectTransform anchor)
        {
            EnsureRelicTooltipUi();
            if (_relicTooltip == null || anchor == null) return;

            RebuildUniqueRelics();
            int relicIndex = _relicStart + slotIndex;
            if (relicIndex < 0 || relicIndex >= _uniqueRelics.Count)
            {
                HideRelicTooltip();
                return;
            }

            var relic = _uniqueRelics[relicIndex];
            if (relic == null)
            {
                HideRelicTooltip();
                return;
            }

            int stacks = RelicManager.Instance != null ? RelicManager.Instance.CountOwned(relic) : 1;
            string title = stacks > 1 ? $"{relic.DisplayName} ×{stacks}" : relic.DisplayName;
            string body = relic.GetEffectDisplayText(stacks);
            _relicTooltip.Show(title, body, anchor);
        }

        public void HideRelicTooltip() => _relicTooltip?.Hide();

        public void OnRelicSlotClicked(int slotIndex)
        {
            RebuildUniqueRelics();
            int relicIndex = _relicStart + slotIndex;
            if (relicIndex < 0 || relicIndex >= _uniqueRelics.Count)
                return;

            var relic = _uniqueRelics[relicIndex];
            if (relic == null)
                return;

            HideRelicTooltip();
            HoverTooltipHub.HideIfPresent();
            RelicPreviewOverlayUI.Ensure().Show(relic);
        }

        private void EnsureRelicTooltipUi()
        {
            if (_relicTooltip != null) return;
            _relicTooltip = GetComponent<RelicHudTooltip>();
            if (_relicTooltip == null)
                _relicTooltip = gameObject.AddComponent<RelicHudTooltip>();
            _relicTooltip.EnsureBuilt(transform, GameOverlayUI.SharedUiFont());
            _relicTooltip.EnsureTopMost(5000);
        }

        private void EnsureRelicStackLabels()
        {
            if (relicSlots == null) return;
            while (_relicStackLabels.Count < relicSlots.Length)
                _relicStackLabels.Add(null);

            var relicBar = FindNamed(transform, "RelicBar") as RectTransform;
            // Bright amber so ×N pops on the gray relic strip.
            var stackColor = new Color(1f, 0.85f, 0.12f, 1f);

            // Slots are siblings of RelicBar and draw on top of it — reparent under RelicBar
            // so stack labels parented there can sit above the circles and still on the gray strip.
            if (relicBar != null)
            {
                for (int s = 0; s < relicSlots.Length; s++)
                {
                    var icon = relicSlots[s];
                    if (icon == null) continue;
                    var frame = icon.transform.parent as RectTransform;
                    if (frame == null) continue;
                    if (frame.parent != relicBar)
                        frame.SetParent(relicBar, true);
                }
            }

            for (int i = 0; i < relicSlots.Length; i++)
            {
                var icon = relicSlots[i];
                if (icon == null) continue;

                var frame = icon.transform.parent != null ? icon.transform.parent : icon.transform;
                var frameRect = frame as RectTransform;
                if (frameRect == null)
                    frameRect = icon.rectTransform;

                // Remove legacy labels stuck on the circle frame.
                var legacy = frame.Find("StackCount");
                if (legacy != null)
                    Destroy(legacy.gameObject);

                Text label = _relicStackLabels[i];
                Transform host = relicBar != null ? (Transform)relicBar : frame;
                if (label == null)
                {
                    var existing = host.Find($"StackCount_{i}");
                    label = existing != null ? existing.GetComponent<Text>() : null;
                    if (label == null)
                    {
                        var go = new GameObject($"StackCount_{i}", typeof(RectTransform));
                        go.transform.SetParent(host, false);
                        label = go.AddComponent<Text>();
                        label.raycastTarget = false;
                        label.horizontalOverflow = HorizontalWrapMode.Overflow;
                        label.verticalOverflow = VerticalWrapMode.Overflow;

                        var outline = go.AddComponent<Outline>();
                        outline.effectColor = new Color(0.05f, 0.04f, 0.02f, 0.95f);
                        outline.effectDistance = new Vector2(1.5f, -1.5f);
                    }

                    _relicStackLabels[i] = label;
                }

                label.font = GameOverlayUI.SharedUiFont();
                label.fontSize = 20;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.LowerRight;
                label.color = stackColor;

                // 独立 Canvas，排序高于 HUD，保证画在灰色 RelicBar 与图标之上。
                var labelCanvas = label.GetComponent<Canvas>();
                if (labelCanvas == null)
                    labelCanvas = label.gameObject.AddComponent<Canvas>();
                labelCanvas.overrideSorting = true;
                int hudOrder = 150;
                // GetComponentInParent 会先命中自身，取真正的上级 HUD Canvas。
                var canvases = label.GetComponentsInParent<Canvas>(true);
                for (int c = 0; c < canvases.Length; c++)
                {
                    if (canvases[c] != null && canvases[c] != labelCanvas)
                    {
                        hudOrder = canvases[c].sortingOrder;
                        break;
                    }
                }
                labelCanvas.sortingOrder = hudOrder + 25;

                var rt = label.rectTransform;
                if (label.transform.parent != host)
                    label.transform.SetParent(host, false);

                if (relicBar != null)
                {
                    var canvas = relicBar.GetComponentInParent<Canvas>();
                    Camera cam = null;
                    if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        cam = canvas.worldCamera;

                    // 锚在图标右下内侧，再上移，落在灰框正面内。
                    Vector3 world = frameRect.TransformPoint(
                        new Vector3(frameRect.rect.xMax - 2f, frameRect.rect.yMin + 22f, 0f));
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
                    Vector2 local;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(relicBar, screen, cam, out local);

                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(1f, 0f);
                    rt.sizeDelta = new Vector2(48f, 26f);

                    float y = local.y + 10f;
                    float barMin = relicBar.rect.yMin + 4f;
                    float barMax = relicBar.rect.yMax - 2f;
                    y = Mathf.Clamp(y, barMin, barMax - 10f);
                    rt.anchoredPosition = new Vector2(local.x + 2f, y);
                }
                else
                {
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(1f, 0f);
                    rt.anchoredPosition = new Vector2(-2f, 10f);
                    rt.sizeDelta = new Vector2(48f, 26f);
                }

                label.transform.SetAsLastSibling();
                label.gameObject.SetActive(false);
            }
        }

        private void EnsureRelicSlotHovers()
        {
            if (_relicHoversReady || relicSlots == null) return;

            for (int i = 0; i < relicSlots.Length; i++)
            {
                var icon = relicSlots[i];
                if (icon == null) continue;

                var frame = icon.transform.parent;
                if (frame == null) continue;

                var frameImage = frame.GetComponent<Image>();
                if (frameImage != null)
                    frameImage.raycastTarget = true;

                var hover = frame.GetComponent<RelicSlotHover>();
                if (hover == null)
                    hover = frame.gameObject.AddComponent<RelicSlotHover>();
                hover.Bind(this, i);
            }

            _relicHoversReady = true;
        }

        private void EnsureRelicSlotClicks()
        {
            if (_relicClicksReady || relicSlots == null) return;

            for (int i = 0; i < relicSlots.Length; i++)
            {
                var icon = relicSlots[i];
                if (icon == null) continue;

                var frame = icon.transform.parent;
                if (frame == null) continue;

                var frameImage = frame.GetComponent<Image>();
                if (frameImage != null)
                    frameImage.raycastTarget = true;

                var click = frame.GetComponent<RelicSlotClick>();
                if (click == null)
                    click = frame.gameObject.AddComponent<RelicSlotClick>();
                click.Bind(this, i);
            }

            _relicClicksReady = true;
        }

        private void EnsureFlavorAndEmployeeHovers()
        {
            BindResourceHover(softValue, "Soft");
            BindResourceHover(toughValue, "Tough");
            BindResourceHover(solidValue, "Solid");
            BindResourceHover(processedValue, "Processed");
            BindResourceHover(cookedValue, "Cooked");

            BindFlavorHover(spicyValue, FlavorType.Spicy);
            BindFlavorHover(coldValue, FlavorType.Cold);
            BindFlavorHover(sourValue, FlavorType.Sour);
            BindFlavorHover(magicValue, FlavorType.Magic);

            Transform employeeHost = null;
            var frameTf = FindNamed(transform, "EmployeeFrame");
            if (frameTf != null)
                employeeHost = frameTf;
            else if (employeeAvatar != null)
                employeeHost = employeeAvatar.transform;

            if (employeeHost != null)
            {
                var tip = employeeHost.GetComponent<UiHoverTooltip>();
                if (tip == null)
                    tip = employeeHost.gameObject.AddComponent<UiHoverTooltip>();
                tip.Bind(
                    () =>
                    {
                        var cur = EmployeeAssignSelection.Current;
                        return cur != null ? cur.DisplayName : "员工";
                    },
                    () =>
                    {
                        HoverTooltipText.Employee(EmployeeAssignSelection.Current, out _, out string body);
                        return string.IsNullOrWhiteSpace(body) ? "未选择员工" : body;
                    });
            }
        }

        private void EnsureResourceFlavorIcons()
        {
            var art = GameArtLibrary.Load();
            if (art == null) return;
            HudResourceIconApplier.ApplyAll(transform, art);
        }

        private void EnsureEmployeeSwitchHint()
        {
            if (employeeSwitchHint == null)
                employeeSwitchHint = FindText(transform, "EmployeeSwitchHint");

            var frameTf = FindNamed(transform, "EmployeeFrame") as RectTransform;
            if (frameTf == null)
                return;

            if (employeeSwitchHint == null)
            {
                var go = new GameObject("EmployeeSwitchHint", typeof(RectTransform));
                go.transform.SetParent(frameTf.parent, false);
                employeeSwitchHint = go.AddComponent<Text>();
            }

            LayoutEmployeeSwitchHint(employeeSwitchHint.rectTransform, frameTf);
            StyleEmployeeSwitchHint(employeeSwitchHint);
            employeeSwitchHint.transform.SetAsLastSibling();
        }

        private static void LayoutEmployeeSwitchHint(RectTransform hintRt, RectTransform frameTf)
        {
            if (hintRt == null || frameTf == null) return;

            if (hintRt.parent != frameTf.parent)
                hintRt.SetParent(frameTf.parent, false);

            hintRt.anchorMin = Vector2.zero;
            hintRt.anchorMax = Vector2.zero;
            hintRt.pivot = new Vector2(0.5f, 0f);
            float topY = frameTf.anchoredPosition.y + frameTf.sizeDelta.y * 0.5f;
            hintRt.anchoredPosition = new Vector2(frameTf.anchoredPosition.x, topY - 10f);
            hintRt.sizeDelta = new Vector2(200f, 32f);
        }

        private static void StyleEmployeeSwitchHint(Text text)
        {
            if (text == null) return;

            text.text = "点击切换员工类型";
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = 14;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.LowerCenter;
            text.color = Color.black;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static void BindResourceHover(Text value, string key)
        {
            if (value == null) return;
            BindNameOnlyHover(ResolveCounterHoverHost(value), HoverTooltipText.HudResourceTitle(key));
        }

        private static void BindFlavorHover(Text value, FlavorType type)
        {
            if (value == null) return;
            BindHover(
                ResolveCounterHoverHost(value),
                () =>
                {
                    HoverTooltipText.Flavor(type, out string title, out string body);
                    return (title, body);
                });
        }

        private static Transform ResolveCounterHoverHost(Text value)
        {
            var parent = value.transform.parent;
            if (parent != null
                && (parent.name.StartsWith("Circle_") || parent.name.StartsWith("Square_")))
                return parent;
            return parent != null ? parent : value.transform;
        }

        private static void BindHover(Transform host, System.Func<(string title, string body)> provider)
        {
            if (host == null || provider == null) return;

            var graphic = host.GetComponent<Graphic>();
            if (graphic != null)
                graphic.raycastTarget = true;

            var tip = host.GetComponent<UiHoverTooltip>();
            if (tip == null)
                tip = host.gameObject.AddComponent<UiHoverTooltip>();
            tip.Bind(() => provider().title, () => provider().body);
        }

        private static void BindNameOnlyHover(Transform host, string title)
        {
            if (host == null || string.IsNullOrWhiteSpace(title)) return;

            var graphic = host.GetComponent<Graphic>();
            if (graphic != null)
                graphic.raycastTarget = true;

            var tip = host.GetComponent<UiHoverTooltip>();
            if (tip == null)
                tip = host.gameObject.AddComponent<UiHoverTooltip>();
            tip.Bind(title, string.Empty);
        }

        private static void RefreshStationLabels()
        {
            var gather = FindObjectsOfType<GatherStationSlot>();
            for (int i = 0; i < gather.Length; i++)
                gather[i]?.RefreshCount();

            var stations = FindObjectsOfType<JobStationMarker>();
            for (int i = 0; i < stations.Length; i++)
                stations[i]?.RefreshLabel();
        }

        private static void SetCount(Text text, int value)
        {
            if (text == null) return;
            int clamped = Mathf.Clamp(value, 0, 999999);
            text.text = clamped.ToString();
        }

        private static Sprite GetRelicPlaceholderSprite()
        {
            if (_relicPlaceholderSprite != null) return _relicPlaceholderSprite;

            var art = GameArtLibrary.Load();
            if (art != null && art.RelicPlaceholder != null)
                _relicPlaceholderSprite = art.RelicPlaceholder;
            else
                _relicPlaceholderSprite = Resources.Load<Sprite>("RelicPlaceholder");

            if (_relicPlaceholderSprite == null)
                _relicPlaceholderSprite = GetWhiteSprite();

            return _relicPlaceholderSprite;
        }

        private static Sprite _whiteSprite;

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return _whiteSprite;
        }

        private void AutoBindIfNeeded()
        {
            if (softValue != null) return;
            var root = transform;
            softValue = FindText(root, "Value_Soft");
            toughValue = FindText(root, "Value_Tough");
            solidValue = FindText(root, "Value_Solid");
            processedValue = FindText(root, "Value_Processed");
            cookedValue = FindText(root, "Value_Cooked");
            spicyValue = FindText(root, "Value_Spicy");
            coldValue = FindText(root, "Value_Cold");
            sourValue = FindText(root, "Value_Sour");
            magicValue = FindText(root, "Value_Magic");

            employeeCount = FindText(root, "Value_Employee");
            employeeSwitchHint = FindText(root, "EmployeeSwitchHint");
            var avatarTf = FindNamed(root, "EmployeeAvatar");
            var frameTf = FindNamed(root, "EmployeeFrame");
            if (avatarTf != null)
                employeeAvatar = avatarTf.GetComponent<Image>();
            if (avatarTf != null)
                employeeAvatarButton = avatarTf.GetComponent<Button>();
            if (employeeAvatarButton == null && frameTf != null)
                employeeAvatarButton = frameTf.GetComponent<Button>();
            if (employeeAvatarButton == null && avatarTf != null)
                employeeAvatarButton = avatarTf.gameObject.AddComponent<Button>();

            var picker = FindNamed(root, "EmployeePicker");
            if (picker != null)
                employeePickerRoot = picker as RectTransform ?? picker.GetComponent<RectTransform>();

            relicPrevButton = FindButton(root, "RelicPrevBtn") ?? FindButton(root, "ZonePrevBtn");
            relicNextButton = FindButton(root, "RelicNextBtn") ?? FindButton(root, "ZoneNextBtn");

            var slots = new List<Image>();
            for (int i = 0; i < 8; i++)
            {
                var slot = FindNamed(root, $"RelicSlot_{i}");
                if (slot == null) continue;
                var icon = FindNamed(slot, "Icon");
                var image = icon != null ? icon.GetComponent<Image>() : slot.GetComponent<Image>();
                if (image != null)
                    slots.Add(image);
            }

            relicSlots = slots.ToArray();
        }

        private static Text FindText(Transform root, string name)
        {
            var t = FindNamed(root, name);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private static Button FindButton(Transform root, string name)
        {
            var t = FindNamed(root, name);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindNamed(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
