using Soup.Jobs;
using UnityEditor;
using UnityEngine;

namespace Soup.Employees.Editor
{
    /// <summary>
    /// Seeds the design-doc employee catalog (员工一览) into EmployeeDatabase.
    /// </summary>
    public static class EmployeeDataSeeder
    {
        private const string DatabasePath = "Assets/Resources/EmployeeDatabase.asset";
        private const string EmployeeFolder = "Assets/Data/Employees";

        [MenuItem("Soup/Employee Manager/Seed Employees")]
        public static void SeedAllMenu()
        {
            SeedAll();
            Debug.Log("[EmployeeDataSeeder] 设计文档员工已填充。");
        }

        public static EmployeeDatabase SeedAll()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(EmployeeFolder);

            var db = AssetDatabase.LoadAssetAtPath<EmployeeDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<EmployeeDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            CreateOrUpdate(db, EmployeeManager.ElfId, "小精灵", 1f,
                true, true, string.Empty, null, 0f, "勤劳的基础员工，可以分配到任意岗位。");
            CreateOrUpdate(db, EmployeeManager.MushroomPersonId, "蘑菇人", 1.5f,
                true, false, "mushroom", null, 0f,
                "占用蘑菇岗位人口，一直生产蘑菇，玩家无法变更岗位。");
            CreateOrUpdate(db, EmployeeManager.GhostId, "幽灵", 0.8f,
                false, true, string.Empty, null, 0f, "不占用工作岗位。");
            CreateOrUpdate(db, EmployeeManager.OtherworldHeroId, "异世界勇者", 3f,
                true, true, string.Empty, null, 0f, "来自异世界的勇者，工作效率极高。");
            CreateOrUpdate(db, EmployeeManager.ZhizhiId, "吱吱", 2.5f,
                true, true, string.Empty, JobType.Process, 0.1f,
                "只能用于处理工作，会吃掉自身产出处理食材的 10%。");

            db.RebuildIndex();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            // 美术素材已放入 Docs 时立即绑定小精灵等图标（幂等）。
            Soup.Game.Editor.ArtIconLinker.LinkCompletedIcons(quiet: true);
            return db;
        }

        private static void CreateOrUpdate(
            EmployeeDatabase db,
            string id,
            string displayName,
            float efficiency,
            bool occupiesSlot,
            bool playerAssignable,
            string lockedJob,
            JobType? allowedType,
            float eatShare,
            string description)
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
            item.SetLaborEfficiency(efficiency);
            item.SetAssignmentRules(occupiesSlot, playerAssignable, lockedJob, allowedType);
            item.SetEatProcessedShare(eatShare);

            EditorUtility.SetDirty(item);
            db.EditorAdd(item);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
