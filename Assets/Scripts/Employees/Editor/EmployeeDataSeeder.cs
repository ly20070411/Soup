using Soup.Jobs;
using UnityEditor;
using UnityEngine;

namespace Soup.Employees.Editor
{
    /// <summary>
    /// Seeds employee units from the design table「员工一览」:
    /// 小精灵 / 蘑菇人 / 幽灵 / 异世界勇者 / 吱吱.
    /// </summary>
    public static class EmployeeDataSeeder
    {
        private const string DatabasePath = "Assets/Resources/EmployeeDatabase.asset";
        private const string EmployeeFolder = "Assets/Data/Employees";

        [MenuItem("Soup/Employee Manager/Seed Sample Employees")]
        public static void SeedSamplesMenu()
        {
            SeedSamples(openWindow: true);
        }

        public static EmployeeDatabase SeedSamples(bool openWindow = false)
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Data");
            EnsureFolder(EmployeeFolder);

            var db = AssetDatabase.LoadAssetAtPath<EmployeeDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<EmployeeDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            var mushroomJob = FindMushroomJob();

            CreateOrUpdate(
                db,
                "elf",
                "小精灵",
                "初级员工单位，无特殊效果。",
                1f,
                occupiesJobSlot: true,
                canPlayerAssign: true,
                lockedJob: null);

            CreateOrUpdate(
                db,
                "mushroom_person",
                "蘑菇人",
                "蘑菇人会占用蘑菇岗位人口，一直生产蘑菇，玩家无法变更岗位。",
                1.5f,
                occupiesJobSlot: true,
                canPlayerAssign: false,
                lockedJob: mushroomJob);

            CreateOrUpdate(
                db,
                "ghost",
                "幽灵",
                "不占用工作岗位人口，可分配到任意已解锁岗位，工作效率 0.8。",
                0.8f,
                occupiesJobSlot: false,
                canPlayerAssign: true,
                lockedJob: null);

            CreateOrUpdate(
                db,
                "otherworld_hero",
                "异世界勇者",
                "无特殊效果，工作效率 3.0。",
                3f,
                occupiesJobSlot: true,
                canPlayerAssign: true,
                lockedJob: null);

            CreateOrUpdate(
                db,
                "zhizhi",
                "吱吱",
                "只能用于处理工作，会吃掉自身产出处理食材的 10%。",
                2.5f,
                occupiesJobSlot: true,
                canPlayerAssign: true,
                lockedJob: null,
                restrictToJobType: true,
                allowedJobType: JobType.Process,
                consumeOwnProcessedFraction: 0.1f);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openWindow)
                EmployeeManagerWindow.Open();

            Debug.Log("[员工管理器] 示例员工已填充，当前数量: " + db.Count);
            return db;
        }

        private static void CreateOrUpdate(
            EmployeeDatabase db,
            string id,
            string displayName,
            string description,
            float efficiency,
            bool occupiesJobSlot,
            bool canPlayerAssign,
            JobItem lockedJob,
            bool restrictToJobType = false,
            JobType allowedJobType = JobType.Process,
            float consumeOwnProcessedFraction = 0f)
        {
            string path = $"{EmployeeFolder}/Employee_{id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<EmployeeItem>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<EmployeeItem>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.SetIdentity(id, displayName);
            item.SetDescription(description);
            item.SetWorkEfficiency(efficiency);
            item.SetOccupiesJobSlot(occupiesJobSlot);
            item.SetCanPlayerAssign(canPlayerAssign);
            item.SetLockedJob(lockedJob);
            item.SetRestrictToJobType(restrictToJobType, allowedJobType);
            item.SetConsumeOwnProcessedFraction(consumeOwnProcessedFraction);
            item.SetTint(TintForId(id));
            EditorUtility.SetDirty(item);
            db.Add(item);
        }

        private static Color TintForId(string id)
        {
            switch (id)
            {
                case "elf":
                    return new Color(0.55f, 0.90f, 0.55f, 1f);
                case "mushroom_person":
                    return new Color(0.90f, 0.55f, 0.35f, 1f);
                case "ghost":
                    return new Color(0.70f, 0.80f, 0.95f, 0.85f);
                case "otherworld_hero":
                    return new Color(0.95f, 0.75f, 0.35f, 1f);
                case "zhizhi":
                    return new Color(0.75f, 0.55f, 0.40f, 1f);
                default:
                    return Color.white;
            }
        }

        private static JobItem FindMushroomJob()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:JobItem"))
            {
                var job = AssetDatabase.LoadAssetAtPath<JobItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (job == null) continue;
                if (job.Id == "mushroom" || job.DisplayName == "蘑菇")
                    return job;
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
