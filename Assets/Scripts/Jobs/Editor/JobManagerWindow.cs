using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Soup.Jobs.Editor
{
    /// <summary>
    /// Editor hub for creating, editing and organizing job/station assets.
    /// </summary>
    public class JobManagerWindow : EditorWindow
    {
        private const string DefaultDatabasePath = "Assets/Resources/JobDatabase.asset";
        private const string DefaultJobFolder = "Assets/Data/Jobs";

        private JobDatabase _database;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _search = string.Empty;
        private JobType? _typeFilter;
        private int _selectedIndex = -1;
        private SerializedObject _selectedSerialized;
        private JobItem _selectedItem;

        [MenuItem("Soup/岗位管理器 (Job Manager)")]
        public static void Open()
        {
            var window = GetWindow<JobManagerWindow>();
            window.titleContent = new GUIContent("岗位管理器");
            window.minSize = new Vector2(860, 520);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureFolders();
            LoadOrCreateDatabase();
            RefreshSelection();
        }

        private void OnFocus()
        {
            if (_database == null)
                LoadOrCreateDatabase();
            else
                EditorUtility.SetDirty(_database);
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_database == null)
            {
                EditorGUILayout.HelpBox("未找到 JobDatabase。点击上方「创建/加载数据库」。", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawListPanel();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("创建/加载数据库", EditorStyles.toolbarButton, GUILayout.Width(110)))
                    LoadOrCreateDatabase();

                if (GUILayout.Button("扫描并同步", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    ScanAndSync();

                if (GUILayout.Button("填充示例", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    JobDataSeeder.SeedSamples(openWindow: false);
                    LoadOrCreateDatabase();
                }

                if (GUILayout.Button("关联采集食材", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    JobDataSeeder.LinkGatherJobsByIngredientName();
                    LoadOrCreateDatabase();
                }

                GUILayout.Space(8);
                _search = GUILayout.TextField(_search ?? string.Empty, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField, GUILayout.MinWidth(160));

                var typeLabel = _typeFilter.HasValue ? JobItem.JobTypeLabel(_typeFilter.Value) : "全部类型";
                if (EditorGUILayout.DropdownButton(new GUIContent(typeLabel), FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(100)))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("全部类型"), !_typeFilter.HasValue, () => { _typeFilter = null; });
                    foreach (JobType type in System.Enum.GetValues(typeof(JobType)))
                    {
                        var captured = type;
                        menu.AddItem(new GUIContent(JobItem.JobTypeLabel(captured)), _typeFilter == captured, () => { _typeFilter = captured; });
                    }
                    menu.ShowAsContext();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+ 新建岗位", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    CreateJob();

                using (new EditorGUI.DisabledScope(_selectedItem == null))
                {
                    if (GUILayout.Button("复制", EditorStyles.toolbarButton, GUILayout.Width(50)))
                        DuplicateSelected();

                    if (GUILayout.Button("删除", EditorStyles.toolbarButton, GUILayout.Width(50)))
                        DeleteSelected();
                }

                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    SaveAll();
            }
        }

        private void DrawListPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
            {
                EditorGUILayout.LabelField($"岗位列表 ({GetFiltered().Count}/{_database.Count})", EditorStyles.boldLabel);
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll, "box");

                var filtered = GetFiltered();
                for (int i = 0; i < filtered.Count; i++)
                {
                    var item = filtered[i];
                    if (item == null) continue;

                    bool selected = item == _selectedItem;
                    var rect = GUILayoutUtility.GetRect(1, 40, GUILayout.ExpandWidth(true));
                    if (selected)
                        EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.90f, 0.35f));

                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        SelectItem(item);
                        Event.current.Use();
                    }

                    var iconRect = new Rect(rect.x + 4, rect.y + 4, 32, 32);
                    if (item.Icon != null)
                        GUI.DrawTexture(iconRect, item.Icon.texture, ScaleMode.ScaleToFit);
                    else
                        EditorGUI.DrawRect(iconRect, TypeColor(item.JobType));

                    var titleRect = new Rect(rect.x + 42, rect.y + 2, rect.width - 48, 18);
                    var subRect = new Rect(rect.x + 42, rect.y + 20, rect.width - 48, 16);
                    GUI.Label(titleRect, string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName, EditorStyles.boldLabel);
                    GUI.Label(subRect, $"{JobItem.JobTypeLabel(item.JobType)}  |  {item.Id}", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDetailPanel()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (_selectedItem == null)
                {
                    EditorGUILayout.HelpBox("从左侧选择岗位，或点击「+ 新建岗位」。", MessageType.Info);
                    return;
                }

                if (_selectedSerialized == null || _selectedSerialized.targetObject != _selectedItem)
                    _selectedSerialized = new SerializedObject(_selectedItem);

                _selectedSerialized.Update();
                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

                EditorGUILayout.LabelField("基础信息", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("id"), new GUIContent("ID"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("displayName"), new GUIContent("名称"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("description"), new GUIContent("描述"));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("图像 / 外观", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("icon"), new GUIContent("图标 Sprite"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("tint"), new GUIContent("着色"));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("岗位类型与容量", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("jobType"), new GUIContent("岗位类型"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("maxWorkers"), new GUIContent("人数上限 (0=不限)"));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("岗位进阶", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    $"最多升级 {JobProgressionRules.MaxUpgradesPerJob(_selectedItem.JobType)} 次。" +
                    (JobProgressionRules.UsesPopulationCap(_selectedItem.JobType)
                        ? " 默认每级 +5 人口；额外效果按「岗位及效果一览」填写。"
                        : " 烹饪进阶效果暂留空，勿擅自填写。"),
                    MessageType.None);
                if (GUILayout.Button("按规则补齐进阶层", GUILayout.Width(140)))
                {
                    _selectedItem.SeedDefaultUpgradeTiers();
                    EditorUtility.SetDirty(_selectedItem);
                    _selectedSerialized.Update();
                }
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("upgradeTiers"), new GUIContent("进阶层"), true);
                EditorGUILayout.HelpBox(_selectedItem.GetUpgradeSummary(), MessageType.Info);

                EditorGUILayout.Space(6);
                switch (_selectedItem.JobType)
                {
                    case JobType.Gather:
                        EditorGUILayout.LabelField("采集效果", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("gatherAmountPerWorker"), new GUIContent("每精灵产出量"));
                        EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("outputIngredient"), new GUIContent("产出食材"));
                        if (_selectedItem.OutputIngredient != null)
                        {
                            EditorGUILayout.HelpBox(
                                $"已关联物品：{_selectedItem.OutputIngredient.DisplayName}（id: {_selectedItem.OutputIngredient.Id}）",
                                MessageType.None);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("未关联食材。可点工具栏「关联采集食材」，按岗位名称自动匹配。", MessageType.Warning);
                        }
                        break;
                    case JobType.Process:
                        EditorGUILayout.LabelField("处理效果", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("processAmountPerWorker"), new GUIContent("每精灵处理量"));
                        EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("preferredMaterial"), new GUIContent("优先材质"));
                        EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("otherMaterialEfficiency"), new GUIContent("其他材质效率"));
                        EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("processRandom"), new GUIContent("随机处理任意材质"));
                        EditorGUILayout.PropertyField(
                            _selectedSerialized.FindProperty("processPriority"),
                            new GUIContent("结算优先级", "数值越大越先结算。爆炸应为 0（最低），刀切/电锯/钻头建议 100。"));
                        break;
                    case JobType.Cook:
                        EditorGUILayout.LabelField("烹饪效果", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("cookAmountPerWorker"), new GUIContent("每精灵烹饪量"));
                        EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("scoreMultiplier"), new GUIContent("分数倍率"));
                        break;
                }

                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(_selectedItem.GetEffectSummary(), MessageType.Info);

                if (_selectedSerialized.ApplyModifiedProperties())
                {
                    _database.MarkDirty();
                    EditorUtility.SetDirty(_selectedItem);
                    EditorUtility.SetDirty(_database);
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "提示：修改后点工具栏「保存」。也可在 Project 中直接编辑单个 Job 资产。",
                    MessageType.None);

                EditorGUILayout.EndScrollView();
            }
        }

        private static Color TypeColor(JobType type)
        {
            switch (type)
            {
                case JobType.Gather: return new Color(0.25f, 0.55f, 0.30f, 0.8f);
                case JobType.Process: return new Color(0.55f, 0.40f, 0.20f, 0.8f);
                case JobType.Cook: return new Color(0.70f, 0.25f, 0.25f, 0.8f);
                default: return new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }
        }

        private List<JobItem> GetFiltered()
        {
            if (_database == null) return new List<JobItem>();

            IEnumerable<JobItem> query = _database.Jobs.Where(i => i != null);

            if (!string.IsNullOrWhiteSpace(_search))
            {
                var key = _search.Trim();
                query = query.Where(i =>
                    (!string.IsNullOrEmpty(i.DisplayName) && i.DisplayName.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(i.Id) && i.Id.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                    i.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (_typeFilter.HasValue)
                query = query.Where(i => i.JobType == _typeFilter.Value);

            return query
                .OrderBy(i => (int)i.JobType)
                .ThenBy(i => i.DisplayName)
                .ToList();
        }

        private void SelectItem(JobItem item)
        {
            _selectedItem = item;
            _selectedIndex = _database != null ? _database.Jobs.ToList().IndexOf(item) : -1;
            _selectedSerialized = item != null ? new SerializedObject(item) : null;
            GUI.FocusControl(null);
            Repaint();
        }

        private void RefreshSelection()
        {
            if (_database == null || _database.Count == 0)
            {
                SelectItem(null);
                return;
            }

            if (_selectedItem == null || !_database.Contains(_selectedItem))
                SelectItem(_database.Jobs.FirstOrDefault(i => i != null));
            else
                SelectItem(_selectedItem);
        }

        private void EnsureFolders()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Data");
            EnsureFolder(DefaultJobFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                AssetDatabase.CreateFolder(parent, name);
        }

        private void LoadOrCreateDatabase()
        {
            EnsureFolders();

            _database = AssetDatabase.LoadAssetAtPath<JobDatabase>(DefaultDatabasePath);
            if (_database == null)
            {
                var guids = AssetDatabase.FindAssets("t:JobDatabase");
                if (guids.Length > 0)
                    _database = AssetDatabase.LoadAssetAtPath<JobDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (_database == null)
            {
                _database = CreateInstance<JobDatabase>();
                AssetDatabase.CreateAsset(_database, DefaultDatabasePath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[岗位管理器] 已创建数据库: {DefaultDatabasePath}");
            }

            _database.RemoveNullEntries();
            _database.RebuildIndex();
            RefreshSelection();
            Repaint();
        }

        private void ScanAndSync()
        {
            if (_database == null) return;

            var guids = AssetDatabase.FindAssets("t:JobItem");
            int added = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
                if (item == null) continue;
                if (_database.Add(item))
                    added++;
            }

            _database.RemoveNullEntries();
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[岗位管理器] 同步完成，新增 {added} 个岗位引用。当前总数 {_database.Count}。");
            RefreshSelection();
        }

        private void CreateJob()
        {
            if (_database == null) return;
            EnsureFolders();

            string baseName = "新岗位";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{DefaultJobFolder}/Job_{baseName}.asset");

            var item = CreateInstance<JobItem>();
            item.SetIdentity(JobItem.SanitizeId(Path.GetFileNameWithoutExtension(assetPath)), baseName);
            item.SetDescription("新岗位");

            AssetDatabase.CreateAsset(item, assetPath);
            _database.Add(item);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SelectItem(item);
            Debug.Log($"[岗位管理器] 已创建岗位: {assetPath}");
        }

        private void DuplicateSelected()
        {
            if (_database == null || _selectedItem == null) return;

            string sourcePath = AssetDatabase.GetAssetPath(_selectedItem);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            if (!AssetDatabase.CopyAsset(sourcePath, newPath))
            {
                Debug.LogError("[岗位管理器] 复制失败。");
                return;
            }

            var clone = AssetDatabase.LoadAssetAtPath<JobItem>(newPath);
            if (clone == null) return;

            string newDisplay = _selectedItem.DisplayName + " 副本";
            clone.SetIdentity(JobItem.SanitizeId(Path.GetFileNameWithoutExtension(newPath)), newDisplay);
            EditorUtility.SetDirty(clone);

            _database.Add(clone);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            SelectItem(clone);
        }

        private void DeleteSelected()
        {
            if (_database == null || _selectedItem == null) return;

            string path = AssetDatabase.GetAssetPath(_selectedItem);
            if (!EditorUtility.DisplayDialog("删除岗位", $"确定删除「{_selectedItem.DisplayName}」？\n{path}", "删除", "取消"))
                return;

            _database.Remove(_selectedItem);
            EditorUtility.SetDirty(_database);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            _selectedItem = null;
            RefreshSelection();
        }

        private void SaveAll()
        {
            if (_selectedSerialized != null)
                _selectedSerialized.ApplyModifiedProperties();

            if (_selectedItem != null)
                EditorUtility.SetDirty(_selectedItem);
            if (_database != null)
            {
                _database.MarkDirty();
                EditorUtility.SetDirty(_database);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[岗位管理器] 已保存。");
        }
    }
}
