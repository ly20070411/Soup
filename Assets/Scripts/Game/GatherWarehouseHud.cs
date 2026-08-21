using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Numbers on the warehouse art: remaining capacity on the upper plaque,
    /// predicted this-turn raw delta on the lower plaque. Overflow turns the ink red.
    /// </summary>
    [ExecuteAlways]
    public sealed class GatherWarehouseHud : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer capacityFrame;
        [SerializeField] private TextMesh capacityMesh;
        [SerializeField] private TextMesh deltaMesh;
        [SerializeField] private TextMesh capacityCaption;
        [SerializeField] private TextMesh deltaCaption;

        // Pixel-sampled yellow plaques on 仓库.png (pivot 1024,1024, ppu 100).
        private static readonly Vector3 UpperPlaqueLocal = new Vector3(-0.63f, -2.12f, -0.05f);
        private static readonly Vector3 LowerPlaqueLocal = new Vector3(-0.58f, -6.08f, -0.05f);
        private static readonly Vector2 UpperPlaqueSize = new Vector2(8.09f, 2.81f);
        private static readonly Vector2 LowerPlaqueSize = new Vector2(8.11f, 2.53f);
        private static readonly Vector3 CaptionOffset = new Vector3(0f, 0.52f, 0f);
        private static readonly Vector3 NumberOffset = new Vector3(0f, -0.28f, 0f);

        private static readonly Color Overflow = new Color(0.78f, 0.12f, 0.08f, 1f);

        public void Refresh()
        {
            EnsureTexts();
            var store = ResourceStore.Instance;
            var turns = TurnManager.Instance;
            var ink = GatherHudText.Ink;
            bool previewOverflow = false;
            int previewDelta = 0;

            if (Application.isPlaying && turns != null)
                previewDelta = turns.PreviewWarehouseDelta(out previewOverflow);

            if (capacityMesh != null)
            {
                if (store != null)
                {
                    capacityMesh.text = store.WarehouseCapacity <= 0
                        ? "∞"
                        : store.WarehouseSpace.ToString();
                }
                else if (!Application.isPlaying)
                {
                    var cfg = Resources.Load<GameConfig>(ElfManager.ResourcesConfigPath);
                    capacityMesh.text = cfg != null ? cfg.WarehouseCapacity.ToString() : "0";
                }
                else
                {
                    capacityMesh.text = string.Empty;
                }

                capacityMesh.color = ink;
            }

            if (deltaMesh != null)
            {
                int delta = Application.isPlaying && turns != null ? previewDelta : 0;
                if (delta > 0)
                    deltaMesh.text = $"+{delta}";
                else
                    deltaMesh.text = delta.ToString();
                deltaMesh.color = previewOverflow ? Overflow : ink;
            }

            if (capacityCaption != null)
                capacityCaption.color = GatherHudText.Muted;
            if (deltaCaption != null)
                deltaCaption.color = GatherHudText.Muted;

            LayoutPlaque(capacityCaption, capacityMesh, UpperPlaqueLocal, UpperPlaqueSize);
            LayoutPlaque(deltaCaption, deltaMesh, LowerPlaqueLocal, LowerPlaqueSize);
        }

        public void EnsureTexts()
        {
            if (capacityFrame != null)
            {
                capacityFrame.enabled = false;
                if (capacityFrame.gameObject != gameObject)
                    capacityFrame.gameObject.SetActive(false);
            }

            var warehouseSr = GetComponent<SpriteRenderer>();
            int sorting = warehouseSr != null ? warehouseSr.sortingOrder + 4 : 8;
            var numberScale = GatherHudText.LocalScaleForWorld(transform, 0.26f);
            var captionScale = GatherHudText.LocalScaleForWorld(transform, 0.16f);

            capacityMesh = GatherHudText.Ensure(
                transform, "Capacity", UpperPlaqueLocal + NumberOffset, numberScale, sorting, 52);
            deltaMesh = GatherHudText.Ensure(
                transform, "TurnDelta", LowerPlaqueLocal + NumberOffset, numberScale, sorting, 52);
            capacityCaption = GatherHudText.Ensure(
                transform, "CapacityCaption", UpperPlaqueLocal + CaptionOffset, captionScale, sorting, 36);
            deltaCaption = GatherHudText.Ensure(
                transform, "DeltaCaption", LowerPlaqueLocal + CaptionOffset, captionScale, sorting, 36);

            if (capacityCaption != null)
            {
                capacityCaption.text = "余量";
                capacityCaption.color = GatherHudText.Muted;
            }

            if (deltaCaption != null)
            {
                deltaCaption.text = "本回合";
                deltaCaption.color = GatherHudText.Muted;
            }

            if (capacityMesh != null && string.IsNullOrEmpty(capacityMesh.text))
                capacityMesh.text = "0";
            if (deltaMesh != null && string.IsNullOrEmpty(deltaMesh.text))
                deltaMesh.text = "+0";

            LayoutPlaque(capacityCaption, capacityMesh, UpperPlaqueLocal, UpperPlaqueSize);
            LayoutPlaque(deltaCaption, deltaMesh, LowerPlaqueLocal, LowerPlaqueSize);
        }

        private void LayoutPlaque(TextMesh caption, TextMesh number, Vector3 plaqueLocal, Vector2 plaqueLocalSize)
        {
            Vector2 plaqueWorld = new Vector2(
                plaqueLocalSize.x * Mathf.Abs(transform.lossyScale.x),
                plaqueLocalSize.y * Mathf.Abs(transform.lossyScale.y));

            if (caption != null)
            {
                caption.transform.localScale = GatherHudText.LocalScaleForWorld(transform, 0.16f);
                caption.transform.localPosition = plaqueLocal + CaptionOffset;
                GatherHudText.FitInside(caption, new Vector2(plaqueWorld.x, plaqueWorld.y * 0.32f), 0.86f);
                GatherHudText.SnapCenter(caption, transform.TransformPoint(plaqueLocal + CaptionOffset), 0f);
            }

            if (number != null)
            {
                number.transform.localScale = GatherHudText.LocalScaleForWorld(transform, 0.26f);
                number.transform.localPosition = plaqueLocal + NumberOffset;
                GatherHudText.FitInside(number, new Vector2(plaqueWorld.x, plaqueWorld.y * 0.52f), 0.86f);
                GatherHudText.SnapCenter(number, transform.TransformPoint(plaqueLocal + NumberOffset), 0f);
            }

            GatherHudText.SnapGroupCenter(caption, number, transform.TransformPoint(plaqueLocal));
        }

        private void OnEnable()
        {
            EnsureTexts();
            Refresh();
        }
    }
}
