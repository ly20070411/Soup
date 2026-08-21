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
        [SerializeField] private Button employeeAvatarButton;
        [SerializeField] private RectTransform employeePickerRoot;

        [Header("Relics")]
        [SerializeField] private Image[] relicSlots = new Image[0];
        [FormerlySerializedAs("zonePrevButton")]
        [SerializeField] private Button relicPrevButton;
        [FormerlySerializedAs("zoneNextButton")]
        [SerializeField] private Button relicNextButton;

        private readonly List<Button> _pickerButtons = new List<Button>();
        private bool _pickerOpen;
        private int _relicStart;
        private static Sprite _relicPlaceholderSprite;
        private RelicHudTooltip _relicTooltip;
        private bool _relicHoversReady;

        private void Awake()
        {
            AutoBindIfNeeded();
            WireButtons();
            EnsureRelicTooltipUi();
            EnsureRelicSlotHovers();
            EnsureFlavorAndEmployeeHovers();
            EnsureResourceFlavorIcons();
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
            int ownedCount = OwnedRelicCount;
            int maxStart = RelicMaxStart(ownedCount, slotCount);
            if (maxStart <= 0) return;

            _relicStart = Mathf.Clamp(_relicStart + direction, 0, maxStart);
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

        private static int OwnedRelicCount =>
            RelicManager.Instance != null ? RelicManager.Instance.Owned.Count : 0;

        private static int RelicMaxStart(int ownedCount, int slotCount) =>
            slotCount <= 0 ? 0 : Mathf.Max(0, ownedCount - slotCount);

        private void RefreshRelics()
        {
            if (relicSlots == null) return;
            var relics = RelicManager.Instance != null ? RelicManager.Instance.Owned : null;
            int ownedCount = relics != null ? relics.Count : 0;
            int slotCount = relicSlots.Length;
            int maxStart = RelicMaxStart(ownedCount, slotCount);
            _relicStart = Mathf.Clamp(_relicStart, 0, maxStart);

            if (relicPrevButton != null)
                relicPrevButton.interactable = _relicStart > 0;
            if (relicNextButton != null)
                relicNextButton.interactable = _relicStart < maxStart;

            for (int i = 0; i < slotCount; i++)
            {
                var slot = relicSlots[i];
                if (slot == null) continue;

                int relicIndex = _relicStart + i;
                RelicItem relic = relics != null && relicIndex < ownedCount ? relics[relicIndex] : null;
                if (relic != null)
                {
                    slot.sprite = relic.Icon != null ? relic.Icon : GetRelicPlaceholderSprite();
                    slot.color = Color.white;
                    slot.enabled = true;
                    slot.preserveAspect = true;
                }
                else
                {
                    slot.sprite = null;
                    slot.color = new Color(1f, 1f, 1f, 0f);
                    slot.enabled = true;
                }
            }
        }

        public void ShowRelicTooltip(int slotIndex, RectTransform anchor)
        {
            EnsureRelicTooltipUi();
            if (_relicTooltip == null || anchor == null) return;

            var relics = RelicManager.Instance != null ? RelicManager.Instance.Owned : null;
            int relicIndex = _relicStart + slotIndex;
            if (relics == null || relicIndex < 0 || relicIndex >= relics.Count)
            {
                HideRelicTooltip();
                return;
            }

            var relic = relics[relicIndex];
            if (relic == null)
            {
                HideRelicTooltip();
                return;
            }

            int stacks = RelicManager.Instance != null ? RelicManager.Instance.CountOwned(relic) : 1;
            string title = stacks > 1 ? $"{relic.DisplayName} ×{stacks}" : relic.DisplayName;
            string body = !string.IsNullOrWhiteSpace(relic.Description)
                ? relic.Description.Trim()
                : relic.GetRulesSummary();
            _relicTooltip.Show(title, body, anchor);
        }

        public void HideRelicTooltip() => _relicTooltip?.Hide();

        private void EnsureRelicTooltipUi()
        {
            if (_relicTooltip != null) return;
            _relicTooltip = GetComponent<RelicHudTooltip>();
            if (_relicTooltip == null)
                _relicTooltip = gameObject.AddComponent<RelicHudTooltip>();
            _relicTooltip.EnsureBuilt(transform, GameOverlayUI.SharedUiFont());
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

        private void EnsureFlavorAndEmployeeHovers()
        {
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

        private static void BindFlavorHover(Text value, FlavorType type)
        {
            if (value == null) return;
            Transform host = value.transform.parent != null ? value.transform.parent : value.transform;
            var tip = host.GetComponent<UiHoverTooltip>();
            if (tip == null)
                tip = host.gameObject.AddComponent<UiHoverTooltip>();
            HoverTooltipText.Flavor(type, out string title, out string body);
            tip.Bind(title, body);
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
