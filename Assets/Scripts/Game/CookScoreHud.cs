using Soup.Levels;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Cook-zone score chrome: stage score on the pot burst, four flavor readouts on the right.
    /// Stage score is whole numbers; spicy multiplier and flavor scores use one decimal.
    /// Positions/scales are authored in the scene — Refresh only updates text content.
    /// </summary>
    [ExecuteAlways]
    public sealed class CookScoreHud : MonoBehaviour
    {
        private static readonly Vector3 StageLocal = new Vector3(-1.34f, 2.39f, -0.02f);
        private static readonly Vector3 SpicyLocal = new Vector3(6.50f, 2.83f, -0.02f);
        private static readonly Vector3 ColdLocal = new Vector3(7.05f, 1.54f, -0.02f);
        private static readonly Vector3 SourLocal = new Vector3(7.37f, 0.39f, -0.02f);
        private static readonly Vector3 MagicLocal = new Vector3(7.36f, -0.80f, -0.02f);

        [SerializeField] private TextMesh stageScoreMesh;
        [SerializeField] private TextMesh spicyMesh;
        [SerializeField] private TextMesh coldMesh;
        [SerializeField] private TextMesh sourMesh;
        [SerializeField] private TextMesh magicMesh;

        public void Refresh()
        {
            EnsureTexts();

            int stageScore = 0;
            float spicyMult = 1f;
            float cold = 0f;
            float sour = 0f;
            float magic = 0f;

            if (Application.isPlaying)
            {
                var levels = LevelManager.Instance;
                var turns = TurnManager.Instance;
                stageScore = levels != null && levels.HasLevels
                    ? levels.ScoreGainedInLevel
                    : (turns != null ? turns.Score : 0);

                var preview = FlavorResolver.PreviewScoresFromState();
                spicyMult = preview.SpicyMultiplier > 0f ? preview.SpicyMultiplier : 1f;

                if (turns != null)
                {
                    cold = turns.ScoreFromCold;
                    sour = turns.ScoreFromSour;
                    magic = turns.ScoreFromMagic;
                }
            }

            if (stageScoreMesh != null)
                stageScoreMesh.text = stageScore.ToString("0");
            if (spicyMesh != null)
                spicyMesh.text = $"×{spicyMult:0.0}";
            if (coldMesh != null)
                coldMesh.text = cold.ToString("0.0");
            if (sourMesh != null)
                sourMesh.text = sour.ToString("0.0");
            if (magicMesh != null)
                magicMesh.text = magic.ToString("0.0");
        }

        public void EnsureTexts()
        {
            int sorting = 28;
            var scale = GatherHudText.LocalScaleForWorld(transform, 0.20f);
            var small = GatherHudText.LocalScaleForWorld(transform, 0.15f);

            // Only create missing meshes — never move/resize authored ones.
            stageScoreMesh = Ensure(stageScoreMesh, "StageScore", StageLocal, scale, sorting, 52);
            spicyMesh = Ensure(spicyMesh, "SpicyScore", SpicyLocal, small, sorting, 34);
            coldMesh = Ensure(coldMesh, "ColdScore", ColdLocal, small, sorting, 34);
            sourMesh = Ensure(sourMesh, "SourScore", SourLocal, small, sorting, 34);
            magicMesh = Ensure(magicMesh, "MagicScore", MagicLocal, small, sorting, 34);

            if (stageScoreMesh != null) stageScoreMesh.color = new Color(0.55f, 0.12f, 0.05f, 1f);
            if (spicyMesh != null) spicyMesh.color = Color.white;
            if (coldMesh != null) coldMesh.color = Color.white;
            if (sourMesh != null) sourMesh.color = Color.white;
            if (magicMesh != null) magicMesh.color = Color.white;
        }

        private TextMesh Ensure(TextMesh current, string name, Vector3 localPos, Vector3 scale, int sorting, int fontSize)
        {
            if (current != null)
            {
                GatherHudText.ApplyFont(current, fontSize);
                return current;
            }

            return GatherHudText.Ensure(transform, name, localPos, scale, sorting, fontSize);
        }

        private void OnEnable()
        {
            EnsureTexts();
            Refresh();
        }
    }
}
