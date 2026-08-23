using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Runtime art references (UI chrome + shared icons). Lives in Resources.
    /// </summary>
    [CreateAssetMenu(fileName = "GameArtLibrary", menuName = "Soup/Game/Art Library", order = 50)]
    public class GameArtLibrary : ScriptableObject
    {
        public const string ResourcesPath = "GameArtLibrary";

        [Header("Zone Switch")]
        [SerializeField] private Sprite zoneSwitchLeft;
        [SerializeField] private Sprite zoneSwitchRight;

        [Header("Frame Dividers")]
        [SerializeField] private Sprite dividerHorizontal;
        [SerializeField] private Sprite dividerVertical;

        [Header("Shared UI")]
        [SerializeField] private Sprite buttonBackground;
        [SerializeField] private Sprite circleFrame;
        [SerializeField] private Sprite relicPlaceholder;

        [Header("HUD Resource Counters")]
        [SerializeField] private Sprite softIcon;
        [SerializeField] private Sprite toughIcon;
        [SerializeField] private Sprite solidIcon;
        [SerializeField] private Sprite processedIcon;
        [SerializeField] private Sprite cookedIcon;

        [Header("HUD Flavor Counters")]
        [SerializeField] private Sprite spicyIcon;
        [SerializeField] private Sprite coldIcon;
        [SerializeField] private Sprite sourIcon;
        [SerializeField] private Sprite magicIcon;

        [Header("Title Screen")]
        [SerializeField] private Sprite titleBackground;
        [SerializeField] private Sprite titleStartButton;
        [SerializeField] private Sprite titleContinueButton;
        [SerializeField] private Sprite titleQuitButton;

        [Header("Shop")]
        [SerializeField] private Sprite shopBackground;
        [SerializeField] private Sprite shopCatPortrait;

        [Header("Event Panel")]
        [SerializeField] private Sprite eventPanelBackground;
        [SerializeField] private Sprite eventIllustrationFrame;

        [Header("Victory Settlement")]
        [SerializeField] private Sprite victorySettlementBackground;

        public Sprite ZoneSwitchLeft => zoneSwitchLeft;
        public Sprite ZoneSwitchRight => zoneSwitchRight;
        public Sprite DividerHorizontal => dividerHorizontal;
        public Sprite DividerVertical => dividerVertical;
        public Sprite ButtonBackground => buttonBackground;
        public Sprite CircleFrame => circleFrame;
        public Sprite RelicPlaceholder => relicPlaceholder;
        public Sprite SoftIcon => softIcon;
        public Sprite ToughIcon => toughIcon;
        public Sprite SolidIcon => solidIcon;
        public Sprite ProcessedIcon => processedIcon;
        public Sprite CookedIcon => cookedIcon;
        public Sprite SpicyIcon => spicyIcon;
        public Sprite ColdIcon => coldIcon;
        public Sprite SourIcon => sourIcon;
        public Sprite MagicIcon => magicIcon;
        public Sprite TitleBackground => titleBackground;
        public Sprite TitleStartButton => titleStartButton;
        public Sprite TitleContinueButton => titleContinueButton;
        public Sprite TitleQuitButton => titleQuitButton;
        public Sprite ShopBackground => shopBackground;
        public Sprite ShopCatPortrait => shopCatPortrait;
        public Sprite EventPanelBackground => eventPanelBackground;
        public Sprite EventIllustrationFrame => eventIllustrationFrame;
        public Sprite VictorySettlementBackground => victorySettlementBackground;

        public void SetZoneSwitch(Sprite left, Sprite right)
        {
            zoneSwitchLeft = left;
            zoneSwitchRight = right;
        }

        public void SetDividers(Sprite horizontal, Sprite vertical)
        {
            dividerHorizontal = horizontal;
            dividerVertical = vertical;
        }

        public void SetButtonBackground(Sprite sprite) => buttonBackground = sprite;

        public void SetCircleFrame(Sprite sprite) => circleFrame = sprite;

        public void SetRelicPlaceholder(Sprite sprite) => relicPlaceholder = sprite;

        public void SetResourceIcons(Sprite soft, Sprite tough, Sprite solid, Sprite processed, Sprite cooked)
        {
            softIcon = soft;
            toughIcon = tough;
            solidIcon = solid;
            processedIcon = processed;
            cookedIcon = cooked;
        }

        public void SetFlavorIcons(Sprite spicy, Sprite cold, Sprite sour, Sprite magic)
        {
            spicyIcon = spicy;
            coldIcon = cold;
            sourIcon = sour;
            magicIcon = magic;
        }

        public Sprite GetHudCounterIcon(string key)
        {
            switch (key)
            {
                case "Soft": return softIcon;
                case "Tough": return toughIcon;
                case "Solid": return solidIcon;
                case "Processed": return processedIcon;
                case "Cooked": return cookedIcon;
                case "Spicy": return spicyIcon;
                case "Cold": return coldIcon;
                case "Sour": return sourIcon;
                case "Magic": return magicIcon;
                default: return null;
            }
        }

        public void SetTitleScreen(Sprite background, Sprite startButton, Sprite continueButton, Sprite quitButton)
        {
            titleBackground = background;
            titleStartButton = startButton;
            titleContinueButton = continueButton;
            titleQuitButton = quitButton;
        }

        public void SetShopArt(Sprite background, Sprite catPortrait)
        {
            shopBackground = background;
            shopCatPortrait = catPortrait;
        }

        public void SetEventPanelBackground(Sprite background) => eventPanelBackground = background;

        public void SetEventIllustrationFrame(Sprite frame) => eventIllustrationFrame = frame;

        public void SetVictorySettlementBackground(Sprite background) =>
            victorySettlementBackground = background;

        public static GameArtLibrary Load()
        {
            return Resources.Load<GameArtLibrary>(ResourcesPath);
        }
    }
}
