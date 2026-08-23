using Soup.Employees;
using Soup.Levels;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Cook-zone score chrome: stage score on the pot burst, four flavor readouts on the right.
    /// Green sour bar shows predicted sour score at the next stage settlement.
    /// </summary>
    [ExecuteAlways]
    public sealed class CookScoreHud : MonoBehaviour
    {
        private const float RefreshInterval = 0.15f;

        private static readonly Vector3 StageLocal = new Vector3(-1.34f, 2.39f, -0.02f);
        private static readonly Vector3 StageMultLocal = new Vector3(-2.12f, 2.39f, -0.02f);
        private static readonly Vector3 SpicyLocal = new Vector3(6.50f, 2.83f, -0.02f);
        private static readonly Vector3 ColdLocal = new Vector3(7.05f, 1.54f, -0.02f);
        private static readonly Vector3 SourLocal = new Vector3(7.37f, 0.39f, -0.02f);
        private static readonly Vector3 MagicLocal = new Vector3(7.36f, -0.80f, -0.02f);

        [SerializeField] private TextMesh stageScoreMesh;
        [SerializeField] private TextMesh stageMultMesh;
        [SerializeField] private TextMesh spicyMesh;
        [SerializeField] private TextMesh coldMesh;
        [SerializeField] private TextMesh sourMesh;
        [SerializeField] private TextMesh magicMesh;

        private float _nextRefresh;
        private EmployeeManager _boundEmployees;
        private TurnManager _boundTurns;
        private ResourceStore _boundStore;

        public void Refresh()
        {
            TryBindMeshes();
            EnsureTexts();

            int stageScore = 0;
            float spicyMult = 1f;
            float cold = 0f;
            float sour = 0f;
            float magic = 0f;
            TurnManager turns = Application.isPlaying ? TurnManager.Instance : null;

            if (Application.isPlaying)
            {
                var levels = LevelManager.Instance;
                stageScore = levels != null && levels.HasLevels
                    ? levels.ScoreGainedInLevel
                    : (turns != null ? turns.Score : 0);

                spicyMult = FlavorResolver.PreviewSpicyMultiplierForDisplay();
                if (spicyMult <= 0f)
                    spicyMult = 1f;

                if (turns != null)
                {
                    cold = turns.ScoreFromCold;
                    magic = turns.ScoreFromMagic;
                    sour = turns.ResolveSourPreviewForHud();
                }
            }

            if (stageScoreMesh != null)
                stageScoreMesh.text = stageScore.ToString("0");

            float finalMult = Application.isPlaying
                ? ScoreMultiplierResolver.PreviewNonSpicyCookMultiplier()
                : 1f;
            ApplyStageMultMesh(finalMult);
            if (spicyMesh != null)
                spicyMesh.text = $"×{spicyMult:0.##}";
            if (coldMesh != null)
                coldMesh.text = cold.ToString("0.0");
            ApplySourMesh(sour, turns);
            if (magicMesh != null)
                magicMesh.text = magic.ToString("0.0");
        }

        private void ApplyStageMultMesh(float mult)
        {
            if (stageMultMesh == null) return;

            bool show = mult > 0f && Mathf.Abs(mult - 1f) > 0.01f;
            stageMultMesh.gameObject.SetActive(show);
            if (!show)
            {
                stageMultMesh.text = string.Empty;
                return;
            }

            stageMultMesh.text = $"×{mult:0.##}";
            stageMultMesh.anchor = TextAnchor.MiddleRight;
            stageMultMesh.alignment = TextAlignment.Right;
        }

        private void ApplySourMesh(float sour, TurnManager turns)
        {
            if (sourMesh == null) return;

            sourMesh.gameObject.SetActive(true);
            bool flashing = turns != null && turns.IsSourSettleFlashing && sour > 0f;
            sourMesh.text = sour > 0f ? $"+{sour:0.0}" : "0.0";
            sourMesh.color = flashing ? new Color(0.15f, 0.45f, 0.12f, 1f) : GatherHudText.Ink;
            sourMesh.anchor = TextAnchor.MiddleCenter;
            sourMesh.alignment = TextAlignment.Center;
            GatherHudText.ApplyFont(sourMesh, 34);
            EnsureMeshOnTop(sourMesh, 40);
            EnsureSourHover(sourMesh);

            var mr = sourMesh.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.enabled = true;
        }

        public void EnsureTexts()
        {
            int sorting = 28;
            var scale = GatherHudText.LocalScaleForWorld(transform, 0.20f);
            var small = GatherHudText.LocalScaleForWorld(transform, 0.15f);

            stageScoreMesh = Ensure(stageScoreMesh, "StageScore", StageLocal, scale, sorting, 52);
            stageMultMesh = Ensure(stageMultMesh, "StageMult", StageMultLocal, small, sorting, 30);
            spicyMesh = Ensure(spicyMesh, "SpicyScore", SpicyLocal, small, sorting, 34);
            coldMesh = Ensure(coldMesh, "ColdScore", ColdLocal, small, sorting, 34);
            sourMesh = Ensure(sourMesh, "SourScore", SourLocal, small, sorting, 40);
            magicMesh = Ensure(magicMesh, "MagicScore", MagicLocal, small, sorting, 34);

            if (stageScoreMesh != null) stageScoreMesh.color = new Color(0.55f, 0.12f, 0.05f, 1f);
            if (stageMultMesh != null) stageMultMesh.color = new Color(0.55f, 0.12f, 0.05f, 0.92f);
            if (spicyMesh != null) spicyMesh.color = Color.white;
            if (coldMesh != null) coldMesh.color = Color.white;
            if (magicMesh != null) magicMesh.color = Color.white;
        }

        private void TryBindMeshes()
        {
            if (stageScoreMesh == null) stageScoreMesh = FindMesh("StageScore");
            if (stageMultMesh == null) stageMultMesh = FindMesh("StageMult");
            if (spicyMesh == null) spicyMesh = FindMesh("SpicyScore");
            if (coldMesh == null) coldMesh = FindMesh("ColdScore");
            if (sourMesh == null) sourMesh = FindMesh("SourScore");
            if (magicMesh == null) magicMesh = FindMesh("MagicScore");
        }

        private TextMesh FindMesh(string name)
        {
            var meshes = GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < meshes.Length; i++)
            {
                var mesh = meshes[i];
                if (mesh != null && mesh.name == name)
                    return mesh;
            }

            return null;
        }

        private TextMesh Ensure(TextMesh current, string name, Vector3 localPos, Vector3 scale, int sorting, int fontSize)
        {
            if (current == null)
                current = FindMesh(name);

            if (current != null)
            {
                GatherHudText.ApplyFont(current, fontSize);
                EnsureMeshOnTop(current, sorting);
                return current;
            }

            return GatherHudText.Ensure(transform, name, localPos, scale, sorting, fontSize);
        }

        private static void EnsureSourHover(TextMesh mesh)
        {
            if (mesh == null) return;
            if (mesh.GetComponent<CookSourScoreHover>() == null)
                mesh.gameObject.AddComponent<CookSourScoreHover>();
        }

        private static void EnsureMeshOnTop(TextMesh mesh, int sorting)
        {
            if (mesh == null) return;
            var mr = mesh.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sortingOrder = sorting;
        }

        private void OnEnable()
        {
            BindEmployees(true);
            BindTurns(true);
            BindStore(true);
            EnsureTexts();
            Refresh();
            _nextRefresh = 0f;
        }

        private void OnDisable()
        {
            BindEmployees(false);
            BindTurns(false);
            BindStore(false);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (EmployeeManager.Instance != _boundEmployees)
                BindEmployees(true);
            if (TurnManager.Instance != _boundTurns)
                BindTurns(true);
            if (ResourceStore.Instance != _boundStore)
                BindStore(true);

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            Refresh();
        }

        private void BindEmployees(bool bind)
        {
            if (_boundEmployees != null)
                _boundEmployees.Changed -= HandleEmployeesChanged;

            if (!bind)
            {
                _boundEmployees = null;
                return;
            }

            _boundEmployees = EmployeeManager.Instance;
            if (_boundEmployees != null)
                _boundEmployees.Changed += HandleEmployeesChanged;
        }

        private void BindTurns(bool bind)
        {
            if (_boundTurns != null)
            {
                _boundTurns.TurnResolved -= HandleTurnResolved;
                _boundTurns.UndoApplied -= HandleUndoApplied;
                _boundTurns.StageSettled -= HandleStageSettled;
            }

            if (!bind)
            {
                _boundTurns = null;
                return;
            }

            _boundTurns = TurnManager.Instance;
            if (_boundTurns != null)
            {
                _boundTurns.TurnResolved += HandleTurnResolved;
                _boundTurns.UndoApplied += HandleUndoApplied;
                _boundTurns.StageSettled += HandleStageSettled;
            }
        }

        private void BindStore(bool bind)
        {
            if (_boundStore != null)
                _boundStore.Changed -= HandleStoreChanged;

            if (!bind)
            {
                _boundStore = null;
                return;
            }

            _boundStore = ResourceStore.Instance;
            if (_boundStore != null)
                _boundStore.Changed += HandleStoreChanged;
        }

        private void HandleEmployeesChanged() => Refresh();

        private void HandleStoreChanged() => Refresh();

        private void HandleTurnResolved(TurnResult _) => Refresh();

        private void HandleUndoApplied() => Refresh();

        private void HandleStageSettled(StageSettlementResult _) => Refresh();
    }
}
