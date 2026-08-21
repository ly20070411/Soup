using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>Generic UI pointer hover → shared tooltip.</summary>
    public sealed class UiHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string title;
        [SerializeField] private string body;

        private Func<string> _titleProvider;
        private Func<string> _bodyProvider;
        private bool _inside;

        public void Bind(string tipTitle, string tipBody)
        {
            title = tipTitle ?? string.Empty;
            body = tipBody ?? string.Empty;
            _titleProvider = null;
            _bodyProvider = null;
            EnsureRaycast();
        }

        public void Bind(Func<string> tipTitle, Func<string> tipBody)
        {
            _titleProvider = tipTitle;
            _bodyProvider = tipBody;
            EnsureRaycast();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _inside = true;
            string t = _titleProvider != null ? _titleProvider() : title;
            string b = _bodyProvider != null ? _bodyProvider() : body;
            HoverTooltipHub.Instance.Show(t, b, transform as RectTransform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _inside = false;
            HoverTooltipHub.HideIfPresent();
        }

        private void OnDisable()
        {
            if (!_inside) return;
            _inside = false;
            HoverTooltipHub.HideIfPresent();
        }

        private void EnsureRaycast()
        {
            var graphic = GetComponent<Graphic>();
            if (graphic != null)
                graphic.raycastTarget = true;
        }
    }
}
