using System.Collections.Generic;
using System.IO;
using System.Linq;
using Soup.Game;
using Soup.Jobs;
using UnityEditor;
using UnityEngine;

namespace Soup.Employees.Editor
{
    /// <summary>
    /// Editor hub for creating and editing employee unit definitions.
    /// </summary>
    public class EmployeeManagerWindow : EditorWindow
    {
        private const string DefaultDatabasePath = "Assets/Resources/EmployeeDatabase.asset";
        private const string DefaultEmployeeFolder = "Assets/Data/Employees";
        private const string DefaultConfigPath = "Assets/Resources/GameConfig.asset";

        private EmployeeDatabase _database;
        private GameConfig _config;
        private SerializedObject _configSerialized;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _search = string.Empty;
        private EmployeeItem _selectedItem;
        private SerializedObject _selectedSerialized;
        private bool _showStartSettings = true;

        [MenuItem("Soup/员工管理器 (Employee Manager)")]
        public static void Open()
        {
            var window = GetWindow<EmployeeManagerWindow>();
            window.titleContent = new GUIContent("员工管理器");
            window.minSize = new Vector2(900, 520);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureFolders();
            LoadOrCreateDatabase();
            LoadConfig();
            RefreshSelection();
        }

        private void OnFocus()
        {
            if (_database == null)
                LoadOrCreateDatabase();
            else
                EditorUtility.SetDirty(_database);

            LoadConfig();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawStartSettings();

            if (_database == null)
            {
                EditorGUILayout.HelpBox("未找到 EmployeeDatabase。点击上方「创建/加载数据库」。", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawListPanel();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStartSettings()
        {
            _showStartSettings = EditorGUILayout.Foldout(_showStartSettings, "开局员工设置", true);
            if (!_showStartSettings) return;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                if (_config == null || _configSerialized == null)
                {
                    EditorGUILayout.HelpBox("未找到 GameConfig。点击下方按钮创建。", MessageType.Warning);
                    if (GUILayout.Button("创建/加载 GameConfig", GUILayout.Width(180)))
                        LoadConfig();
                    return;
                }

                _configSerialized.Update();
                EditorGUILayout.PropertyField(
                    _configSerialized.FindProperty("startingElfCount"),
                    new GUIContent("开局小精灵数量", "新开局 / 重置局时拥有的小精灵总数。"));
                if (_configSerialized.ApplyModifiedProperties())
                    EditorUtility.SetDirty(_config);

                EditorGUILayout.HelpBox(
                    "蘑菇人 / 幽灵默认开局为 0，可通过事件或调试按钮获得。",
                    MessageType.None);
            }
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
                    EmployeeDataSeeder.SeedSamples(openWindow: false);
                    LoadOrCreateDatabase();
                }

                GUILayout.Space(8);
                _search = GUILayout.TextField(
                    _search ?? string.Empty,
                    GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField,
                    GUILayout.MinWidth(160));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+ 新建员工", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    CreateEmployee();

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
                EditorGUILayout.LabelField(
                    $"员工列表 ({GetFiltered().Count}/{_database.Count})",
                    EditorStyles.boldLabel);
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll, "box");

                var filtered = GetFiltered();
                for (int i = 0; i < filtered.Count; i++)
                {
                    var item = filtered[i];
                    if (item == null) continue;

                    bool selected = item == _selectedItem;
                    var rect = GUILayoutUtility.GetRect(1, 44, GUILayout.ExpandWidth(true));
                    if (selected)
                        EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.90f, 0.35f));

                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        SelectItem(item);
                        Event.current.Use();
                    }

                    var iconRect = new Rect(rect.x + 4, rect.y + 6, 32, 32);
                    if (item.Icon != null)
                        GUI.DrawTexture(iconRect, item.Icon.texture, ScaleMode.ScaleToFit);
                    else
                        EditorGUI.DrawRect(iconRect, item.Tint);

                    var titleRect = new Rect(rect.x + 42, rect.y + 4, rect.width - 48, 18);
                    var subRect = new Rect(rect.x + 42, rect.y + 22, rect.width - 48, 16);
                    GUI.Label(titleRect, string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName, EditorStyles.boldLabel);
                    string locked = item.HasLockedJob && item.LockedJob != null
                        ? item.LockedJob.DisplayName
                        : (item.CanPlayerAssign ? "可调" : "锁定");
                    string flags =
                        $"效率 {item.WorkEfficiency:0.##}  |  " +
                        (item.OccupiesJobSlot ? "占岗" : "不占岗") + "  |  " +
                        locked;
                    GUI.Label(subRect, flags, EditorStyles.miniLabel);
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
                    EditorGUILayout.HelpBox("从左侧选择员工，或点击「+ 新建员工」。", MessageType.Info);
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
                EditorGUILayout.LabelField("外观", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("icon"), new GUIContent("图标"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("tint"), new GUIContent("着色"));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("工作规则", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("workEfficiency"), new GUIContent("工作效率"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("occupiesJobSlot"), new GUIContent("占用岗位人口"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("canPlayerAssign"), new GUIContent("玩家可分配"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("lockedJob"), new GUIContent("锁定岗位"));
                EditorGUILayout.PropertyField(
                    _selectedSerialized.FindProperty("restrictToJobType"),
                    new GUIContent("限制岗位类型", "开启后只能分配到下方类型（吱吱=处理）。"));
                using (new EditorGUI.DisabledScope(!_selectedSerialized.FindProperty("restrictToJobType").boolValue))
                {
                    EditorGUILayout.PropertyField(
                        _selectedSerialized.FindProperty("allowedJobType"),
                        new GUIContent("允许的岗位类型"));
                }

                EditorGUILayout.PropertyField(
                    _selectedSerialized.FindProperty("consumeOwnProcessedFraction"),
                    new GUIContent("吃掉自身处理产出", "0~1。吱吱为 0.1（吃掉自身产出处理食材的 10%）。"));

                if (_selectedItem.HasLockedJob && _selectedItem.LockedJob != null)
                {
                    EditorGUILayout.HelpBox(
                        $"拥有后将始终占用「{_selectedItem.LockedJob.DisplayName}」，玩家无法手动调岗。",
                        MessageType.Info);
                }
                else if (_selectedItem.RestrictToJobType)
                {
                    EditorGUILayout.HelpBox(
                        $"只能分配到「{JobItem.JobTypeLabel(_selectedItem.AllowedJobType)}」岗位。",
                        MessageType.Info);
                }

                if (_selectedSerialized.ApplyModifiedProperties())
                {
                    _database.MarkDirty();
                    EditorUtility.SetDirty(_selectedItem);
                    EditorUtility.SetDirty(_database);
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "小精灵：效率1，占岗，可分配。\n" +
                    "蘑菇人：效率1，占蘑菇岗，锁定不可调。\n" +
                    "幽灵：效率0.8，不占岗，可分配。\n" +
                    "吱吱：仅处理岗，吃掉自身处理产出的 10%。",
                    MessageType.Info);

                EditorGUILayout.EndScrollView();
            }
        }

        private List<EmployeeItem> GetFiltered()
        {
            if (_database == null) return new List<EmployeeItem>();
            IEnumerable<EmployeeItem> query = _database.Employees.Where(i => i != null);
            if (!string.IsNullOrWhiteSpace(_search))
            {
                var key = _search.Trim();
                query = query.Where(i =>
                    (!string.IsNullOrEmpty(i.DisplayName) && i.DisplayName.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrEmpty(i.Id) && i.Id.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || i.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return query.OrderBy(i => i.DisplayName).ToList();
        }

        private void SelectItem(EmployeeItem item)
        {
            _selectedItem = item;
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
                SelectItem(_database.Employees.FirstOrDefault(i => i != null));
            else
                SelectItem(_selectedItem);
        }

        private void EnsureFolders()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Data");
            EnsureFolder(DefaultEmployeeFolder);
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
            _database = AssetDatabase.LoadAssetAtPath<EmployeeDatabase>(DefaultDatabasePath);
            if (_database == null)
            {
                var guids = AssetDatabase.FindAssets("t:EmployeeDatabase");
                if (guids.Length > 0)
                    _database = AssetDatabase.LoadAssetAtPath<EmployeeDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (_database == null)
            {
                _database = CreateInstance<EmployeeDatabase>();
                AssetDatabase.CreateAsset(_database, DefaultDatabasePath);
                AssetDatabase.SaveAssets();
            }

            _database.RemoveNullEntries();
            _database.RebuildIndex();
            RefreshSelection();
            Repaint();
        }

        private void LoadConfig()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>(DefaultConfigPath);
            if (_config == null)
            {
                var guids = AssetDatabase.FindAssets("t:GameConfig");
                if (guids.Length > 0)
                    _config = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (_config == null)
            {
                EnsureFolder("Assets/Resources");
                _config = CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(_config, DefaultConfigPath);
                AssetDatabase.SaveAssets();
            }

            _configSerialized = new SerializedObject(_config);
        }

        private void ScanAndSync()
        {
            if (_database == null) return;
            int added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:EmployeeItem"))
            {
                var item = AssetDatabase.LoadAssetAtPath<EmployeeItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && _database.Add(item))
                    added++;
            }

            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[员工管理器] 同步完成，新增 {added}。当前 {_database.Count}。");
            RefreshSelection();
        }

        private void CreateEmployee()
        {
            if (_database == null) return;
            EnsureFolders();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultEmployeeFolder}/Employee_新员工.asset");
            var item = CreateInstance<EmployeeItem>();
            item.SetIdentity(EmployeeItem.SanitizeId(Path.GetFileNameWithoutExtension(path)), "新员工");
            item.SetDescription("新员工单位");
            item.SetWorkEfficiency(1f);
            item.SetOccupiesJobSlot(true);
            item.SetCanPlayerAssign(true);
            AssetDatabase.CreateAsset(item, path);
            _database.Add(item);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            SelectItem(item);
        }

        private void DuplicateSelected()
        {
            if (_database == null || _selectedItem == null) return;
            string sourcePath = AssetDatabase.GetAssetPath(_selectedItem);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            if (!AssetDatabase.CopyAsset(sourcePath, newPath)) return;
            var clone = AssetDatabase.LoadAssetAtPath<EmployeeItem>(newPath);
            if (clone == null) return;
            clone.SetIdentity(EmployeeItem.SanitizeId(Path.GetFileNameWithoutExtension(newPath)), _selectedItem.DisplayName + " 副本");
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
            if (!EditorUtility.DisplayDialog("删除员工", $"确定删除「{_selectedItem.DisplayName}」？\n{path}", "删除", "取消"))
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
            if (_configSerialized != null)
                _configSerialized.ApplyModifiedProperties();
            if (_selectedItem != null)
                EditorUtility.SetDirty(_selectedItem);
            if (_config != null)
                EditorUtility.SetDirty(_config);
            if (_database != null)
            {
                _database.MarkDirty();
                EditorUtility.SetDirty(_database);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[员工管理器] 已保存。");
        }
    }
}
