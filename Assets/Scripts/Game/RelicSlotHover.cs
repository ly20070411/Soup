using Soup.Relics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Soup.Game
{
    /// <summary>
    /// Relic bar slot hover → tooltip on the owning HUD.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class RelicSlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private PlayAuthoredHud hud;

        public int SlotIndex
        {
            get => slotIndex;
            set => slotIndex = value;
        }

        public void Bind(PlayAuthoredHud owner, int index)
        {
            hud = owner;
            slotIndex = index;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hud != null)
                hud.ShowRelicTooltip(slotIndex, transform as RectTransform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (hud != null)
                hud.HideRelicTooltip();
        }
    }
}
