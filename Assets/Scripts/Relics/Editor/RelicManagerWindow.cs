using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Soup.Relics.Editor
{
    /// <summary>
    /// Editor hub for creating, editing and organizing relic assets.
    /// </summary>
    public class RelicManagerWindow : EditorWindow
    {
        private const string DefaultDatabasePath = "Assets/Resources/RelicDatabase.asset";
        private const string DefaultRelicFolder = "Assets/Data/Relics";

        private RelicDatabase _database;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _search = string.Empty;
        private RelicAcquireStage? _stageFilter;
        private RelicItem _selectedItem;
        private SerializedObject _selectedSerialized;

        [MenuItem("Soup/遗物管理器 (Relic Manager)")]
        public static void Open()
        {
            var window = GetWindow<RelicManagerWindow>();
            window.titleContent = new GUIContent("遗物管理器");
            window.minSize = new Vector2(900, 520);
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
                EditorGUILayout.HelpBox("未找到 RelicDatabase。点击上方「创建/加载数据库」。", MessageType.Warning);
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

                if (GUILayout.Button("填充正式遗物", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    RelicDataSeeder.SeedAll(openWindow: false);
                    LoadOrCreateDatabase();
                }

                GUILayout.Space(8);
                _search = GUILayout.TextField(
                    _search ?? string.Empty,
                    GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField,
                    GUILayout.MinWidth(160));

                var stageLabel = _stageFilter.HasValue
                    ? RelicItem.StageLabel(_stageFilter.Value)
                    : "全部阶段";
                if (EditorGUILayout.DropdownButton(new GUIContent(stageLabel), FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(100)))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("全部阶段"), !_stageFilter.HasValue, () => { _stageFilter = null; });
                    foreach (RelicAcquireStage stage in System.Enum.GetValues(typeof(RelicAcquireStage)))
                    {
                        var captured = stage;
                        menu.AddItem(
                            new GUIContent(RelicItem.StageLabel(captured)),
                            _stageFilter == captured,
                            () => { _stageFilter = captured; });
                    }

                    menu.ShowAsContext();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+ 新建遗物", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    CreateRelic();

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
                EditorGUILayout.LabelField($"遗物列表 ({GetFiltered().Count}/{_database.Count})", EditorStyles.boldLabel);
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
                        EditorGUI.DrawRect(iconRect, StageColor(item.AcquireStage));

                    var titleRect = new Rect(rect.x + 42, rect.y + 2, rect.width - 48, 18);
                    var subRect = new Rect(rect.x + 42, rect.y + 20, rect.width - 48, 16);
                    GUI.Label(titleRect, string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName, EditorStyles.boldLabel);
                    GUI.Label(subRect, $"{RelicItem.StageLabel(item.AcquireStage)}  |  {item.Id}", EditorStyles.miniLabel);
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
                    EditorGUILayout.HelpBox("从左侧选择遗物，或点击「+ 新建遗物」。", MessageType.Info);
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
                EditorGUILayout.LabelField("获取阶段", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("acquireStage"), new GUIContent("阶段"));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("因果关系规则", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("rules"), new GUIContent("规则列表"), true);

                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(_selectedItem.GetRulesSummary(), MessageType.Info);

                if (_selectedSerialized.ApplyModifiedProperties())
                {
                    _database.MarkDirty();
                    EditorUtility.SetDirty(_selectedItem);
                    EditorUtility.SetDirty(_database);
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "提示：每条规则 = 触发时机 + 条件 + 效果。新增效果类型需改枚举并在 RelicEffectRunner 中写 handler。",
                    MessageType.None);

                EditorGUILayout.EndScrollView();
            }
        }

        private static Color StageColor(RelicAcquireStage stage)
        {
            switch (stage)
            {
                case RelicAcquireStage.Starting: return new Color(0.25f, 0.55f, 0.30f, 0.8f);
                case RelicAcquireStage.Event: return new Color(0.30f, 0.35f, 0.70f, 0.8f);
                default: return new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }
        }

        private List<RelicItem> GetFiltered()
        {
            if (_database == null) return new List<RelicItem>();

            IEnumerable<RelicItem> query = _database.Relics.Where(i => i != null);

            if (!string.IsNullOrWhiteSpace(_search))
            {
                var key = _search.Trim();
                query = query.Where(i =>
                    (!string.IsNullOrEmpty(i.DisplayName) && i.DisplayName.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(i.Id) && i.Id.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                    i.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (_stageFilter.HasValue)
                query = query.Where(i => i.AcquireStage == _stageFilter.Value);

            return query
                .OrderBy(i => (int)i.AcquireStage)
                .ThenBy(i => i.DisplayName)
                .ToList();
        }

        private void SelectItem(RelicItem item)
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
                SelectItem(_database.Relics.FirstOrDefault(i => i != null));
            else
                SelectItem(_selectedItem);
        }

        private void EnsureFolders()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Data");
            EnsureFolder(DefaultRelicFolder);
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

            _database = AssetDatabase.LoadAssetAtPath<RelicDatabase>(DefaultDatabasePath);
            if (_database == null)
            {
                var guids = AssetDatabase.FindAssets("t:RelicDatabase");
                if (guids.Length > 0)
                    _database = AssetDatabase.LoadAssetAtPath<RelicDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (_database == null)
            {
                _database = CreateInstance<RelicDatabase>();
                AssetDatabase.CreateAsset(_database, DefaultDatabasePath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[遗物管理器] 已创建数据库: {DefaultDatabasePath}");
            }

            _database.RemoveNullEntries();
            _database.RebuildIndex();
            RefreshSelection();
            Repaint();
        }

        private void ScanAndSync()
        {
            if (_database == null) return;

            var guids = AssetDatabase.FindAssets("t:RelicItem");
            int added = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<RelicItem>(path);
                if (item == null) continue;
                if (_database.Add(item))
                    added++;
            }

            _database.RemoveNullEntries();
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[遗物管理器] 同步完成，新增 {added} 个遗物引用。当前总数 {_database.Count}。");
            RefreshSelection();
        }

        private void CreateRelic()
        {
            if (_database == null) return;
            EnsureFolders();

            string baseName = "新遗物";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{DefaultRelicFolder}/Relic_{baseName}.asset");

            var item = CreateInstance<RelicItem>();
            item.SetIdentity(RelicItem.SanitizeId(Path.GetFileNameWithoutExtension(assetPath)), baseName);
            item.SetDescription("新遗物");
            item.AddRule(new RelicRule());

            AssetDatabase.CreateAsset(item, assetPath);
            _database.Add(item);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SelectItem(item);
            Debug.Log($"[遗物管理器] 已创建遗物: {assetPath}");
        }

        private void DuplicateSelected()
        {
            if (_database == null || _selectedItem == null) return;

            string sourcePath = AssetDatabase.GetAssetPath(_selectedItem);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            if (!AssetDatabase.CopyAsset(sourcePath, newPath))
            {
                Debug.LogError("[遗物管理器] 复制失败。");
                return;
            }

            var clone = AssetDatabase.LoadAssetAtPath<RelicItem>(newPath);
            if (clone == null) return;

            string newDisplay = _selectedItem.DisplayName + " 副本";
            clone.SetIdentity(RelicItem.SanitizeId(Path.GetFileNameWithoutExtension(newPath)), newDisplay);
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
            if (!EditorUtility.DisplayDialog("删除遗物", $"确定删除「{_selectedItem.DisplayName}」？\n{path}", "删除", "取消"))
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
            Debug.Log("[遗物管理器] 已保存。");
        }
    }
}
