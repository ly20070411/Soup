using System.Collections.Generic;
using System.IO;
using System.Linq;
using Soup.Game;
using UnityEditor;
using UnityEngine;

namespace Soup.Events.Editor
{
    /// <summary>
    /// Editor hub for creating, editing and organizing narrative event assets.
    /// </summary>
    public class EventManagerWindow : EditorWindow
    {
        private const string DefaultDatabasePath = "Assets/Resources/EventDatabase.asset";
        private const string DefaultEventFolder = "Assets/Data/Events";
        private const string DefaultConfigPath = "Assets/Resources/GameConfig.asset";

        private EventDatabase _database;
        private GameConfig _config;
        private SerializedObject _configSerialized;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _search = string.Empty;
        private EventCategory? _categoryFilter;
        private EventItem _selectedItem;
        private SerializedObject _selectedSerialized;
        private bool _showSpawnSettings = true;

        [MenuItem("Soup/事件管理器 (Event Manager)")]
        public static void Open()
        {
            var window = GetWindow<EventManagerWindow>();
            window.titleContent = new GUIContent("事件管理器");
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
            DrawSpawnSettings();

            if (_database == null)
            {
                EditorGUILayout.HelpBox("未找到 EventDatabase。点击上方「创建/加载数据库」。", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawListPanel();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSpawnSettings()
        {
            _showSpawnSettings = EditorGUILayout.Foldout(_showSpawnSettings, "事件出现设置", true);
            if (!_showSpawnSettings) return;

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

                EditorGUILayout.LabelField("关卡通关事件（主流程）", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    _configSerialized.FindProperty("enableStageEndEvents"),
                    new GUIContent("通关后出现", "关卡达标通关后抽取事件。"));
                EditorGUILayout.PropertyField(
                    _configSerialized.FindProperty("stageEndEventCount"),
                    new GUIContent("每关事件数", "默认 2。其中至多 1 个进阶专属事件。"));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("回合随机（无关卡时）", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    _configSerialized.FindProperty("enableTurnEndEvents"),
                    new GUIContent("回合结束随机出现", "有关卡时不会触发。关闭后回合结束不再随机弹事件（调试触发仍可用）。"));
                EditorGUILayout.PropertyField(
                    _configSerialized.FindProperty("turnEndEventChance"),
                    new GUIContent("出现概率", "回合结束且不在冷却中时，触发随机事件的概率。"));
                EditorGUILayout.PropertyField(
                    _configSerialized.FindProperty("eventCooldownTurns"),
                    new GUIContent("冷却回合数", "一定回合数内最多一次：两次随机事件至少间隔这么多回合。1 = 每回合都可判定。"));

                if (_configSerialized.ApplyModifiedProperties())
                    EditorUtility.SetDirty(_config);

                EditorGUILayout.HelpBox(
                    "规则：每关通关后抽取 N 个事件；进阶专属需对应岗位已进阶≥1，权重为一般的 3 倍；每关至多 1 个进阶专属。有关卡时回合随机事件关闭。",
                    MessageType.Info);
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
                    EventDataSeeder.SeedSamples(openWindow: false);
                    LoadOrCreateDatabase();
                }

                GUILayout.Space(8);
                _search = GUILayout.TextField(
                    _search ?? string.Empty,
                    GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField,
                    GUILayout.MinWidth(160));

                var categoryLabel = _categoryFilter.HasValue
                    ? EventItem.CategoryLabel(_categoryFilter.Value)
                    : "全部类型";
                if (EditorGUILayout.DropdownButton(
                        new GUIContent(categoryLabel),
                        FocusType.Passive,
                        EditorStyles.toolbarDropDown,
                        GUILayout.Width(100)))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("全部类型"), !_categoryFilter.HasValue, () => { _categoryFilter = null; });
                    foreach (EventCategory category in System.Enum.GetValues(typeof(EventCategory)))
                    {
                        var captured = category;
                        menu.AddItem(
                            new GUIContent(EventItem.CategoryLabel(captured)),
                            _categoryFilter == captured,
                            () => { _categoryFilter = captured; });
                    }

                    menu.ShowAsContext();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+ 新建事件", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    CreateEvent();

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
                    $"事件列表 ({GetFiltered().Count}/{_database.Count})",
                    EditorStyles.boldLabel);
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

                    if (UnityEngine.Event.current.type == EventType.MouseDown
                        && rect.Contains(UnityEngine.Event.current.mousePosition))
                    {
                        SelectItem(item);
                        UnityEngine.Event.current.Use();
                    }

                    var iconRect = new Rect(rect.x + 4, rect.y + 4, 32, 32);
                    EditorGUI.DrawRect(iconRect, CategoryColor(item.Category));

                    var titleRect = new Rect(rect.x + 42, rect.y + 2, rect.width - 48, 18);
                    var subRect = new Rect(rect.x + 42, rect.y + 20, rect.width - 48, 16);
                    GUI.Label(
                        titleRect,
                        string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName,
                        EditorStyles.boldLabel);
                    GUI.Label(
                        subRect,
                        $"{EventItem.CategoryLabel(item.Category)}  |  选项 {item.Options?.Count ?? 0}  |  {item.Id}",
                        EditorStyles.miniLabel);
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
                    EditorGUILayout.HelpBox("从左侧选择事件，或点击「+ 新建事件」。", MessageType.Info);
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
                EditorGUILayout.LabelField("分类 / 触发", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("category"), new GUIContent("类型"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("triggerMoment"), new GUIContent("触发时机"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("weight"), new GUIContent("权重", "进阶专属抽取时会再 ×3。"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("canRepeat"), new GUIContent("可重复出现"));
                EditorGUILayout.PropertyField(
                    _selectedSerialized.FindProperty("requiredStageIndex"),
                    new GUIContent("限定关卡", "0 = 任意关；1/2/… = 仅在该关通关后进入抽取池。"));

                if (_selectedItem.Category == EventCategory.AdvancedExclusive)
                {
                    EditorGUILayout.PropertyField(
                        _selectedSerialized.FindProperty("relatedJob"),
                        new GUIContent("对应岗位", "该岗位进阶至少一次后，本事件才有概率进入通关事件抽取池。"));
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("选项（通常 3 个）", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("options"), new GUIContent("选项列表"), true);

                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(_selectedItem.GetOptionsSummary(), MessageType.Info);

                if (_selectedSerialized.ApplyModifiedProperties())
                {
                    _database.MarkDirty();
                    EditorUtility.SetDirty(_selectedItem);
                    EditorUtility.SetDirty(_database);
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "提示：通关事件请把触发时机设为 AfterStage。进阶专属需绑定对应岗位。",
                    MessageType.None);

                EditorGUILayout.EndScrollView();
            }
        }

        private static Color CategoryColor(EventCategory category)
        {
            switch (category)
            {
                case EventCategory.General: return new Color(0.30f, 0.45f, 0.70f, 0.85f);
                case EventCategory.AdvancedExclusive: return new Color(0.70f, 0.40f, 0.25f, 0.85f);
                default: return new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }
        }

        private List<EventItem> GetFiltered()
        {
            if (_database == null) return new List<EventItem>();

            IEnumerable<EventItem> query = _database.Events.Where(i => i != null);

            if (!string.IsNullOrWhiteSpace(_search))
            {
                var key = _search.Trim();
                query = query.Where(i =>
                    (!string.IsNullOrEmpty(i.DisplayName)
                     && i.DisplayName.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrEmpty(i.Id)
                        && i.Id.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || i.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (_categoryFilter.HasValue)
                query = query.Where(i => i.Category == _categoryFilter.Value);

            return query
                .OrderBy(i => (int)i.Category)
                .ThenBy(i => i.DisplayName)
                .ToList();
        }

        private void SelectItem(EventItem item)
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
                SelectItem(_database.Events.FirstOrDefault(i => i != null));
            else
                SelectItem(_selectedItem);
        }

        private void EnsureFolders()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Data");
            EnsureFolder(DefaultEventFolder);
        }

        private void LoadConfig()
        {
            EnsureFolder("Assets/Resources");
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>(DefaultConfigPath);
            if (_config == null)
            {
                var guids = AssetDatabase.FindAssets("t:GameConfig");
                if (guids.Length > 0)
                    _config = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (_config == null)
            {
                _config = CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(_config, DefaultConfigPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[事件管理器] 已创建 GameConfig: {DefaultConfigPath}");
            }

            _configSerialized = new SerializedObject(_config);
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

            _database = AssetDatabase.LoadAssetAtPath<EventDatabase>(DefaultDatabasePath);
            if (_database == null)
            {
                var guids = AssetDatabase.FindAssets("t:EventDatabase");
                if (guids.Length > 0)
                    _database = AssetDatabase.LoadAssetAtPath<EventDatabase>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (_database == null)
            {
                _database = CreateInstance<EventDatabase>();
                AssetDatabase.CreateAsset(_database, DefaultDatabasePath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[事件管理器] 已创建数据库: {DefaultDatabasePath}");
            }

            _database.RemoveNullEntries();
            _database.RebuildIndex();
            RefreshSelection();
            Repaint();
        }

        private void ScanAndSync()
        {
            if (_database == null) return;

            var guids = AssetDatabase.FindAssets("t:EventItem");
            int added = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<EventItem>(path);
                if (item == null) continue;
                if (_database.Add(item))
                    added++;
            }

            _database.RemoveNullEntries();
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[事件管理器] 同步完成，新增 {added} 个事件引用。当前总数 {_database.Count}。");
            RefreshSelection();
        }

        private void CreateEvent()
        {
            if (_database == null) return;
            EnsureFolders();

            string baseName = "新事件";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{DefaultEventFolder}/Event_{baseName}.asset");

            var item = CreateInstance<EventItem>();
            item.SetIdentity(EventItem.SanitizeId(Path.GetFileNameWithoutExtension(assetPath)), baseName);
            item.SetDescription("新事件描述");
            item.SetCategory(EventCategory.General);
            item.SetTriggerMoment(EventTriggerMoment.AfterStage);
            item.SetWeight(1f);

            for (int i = 0; i < 3; i++)
            {
                var option = new EventOption();
                option.SetLabel($"选项 {i + 1}");
                item.AddOption(option);
            }

            AssetDatabase.CreateAsset(item, assetPath);
            _database.Add(item);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SelectItem(item);
            Debug.Log($"[事件管理器] 已创建事件: {assetPath}");
        }

        private void DuplicateSelected()
        {
            if (_database == null || _selectedItem == null) return;

            string sourcePath = AssetDatabase.GetAssetPath(_selectedItem);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            if (!AssetDatabase.CopyAsset(sourcePath, newPath))
            {
                Debug.LogError("[事件管理器] 复制失败。");
                return;
            }

            var clone = AssetDatabase.LoadAssetAtPath<EventItem>(newPath);
            if (clone == null) return;

            string newDisplay = _selectedItem.DisplayName + " 副本";
            clone.SetIdentity(EventItem.SanitizeId(Path.GetFileNameWithoutExtension(newPath)), newDisplay);
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
            if (!EditorUtility.DisplayDialog(
                    "删除事件",
                    $"确定删除「{_selectedItem.DisplayName}」？\n{path}",
                    "删除",
                    "取消"))
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
            {
                _configSerialized.ApplyModifiedProperties();
                if (_config != null)
                    EditorUtility.SetDirty(_config);
            }

            if (_selectedItem != null)
                EditorUtility.SetDirty(_selectedItem);
            if (_database != null)
            {
                _database.MarkDirty();
                EditorUtility.SetDirty(_database);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[事件管理器] 已保存。");
        }
    }
}
