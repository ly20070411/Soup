using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Soup.Items.Editor
{
    /// <summary>
    /// Editor hub for creating, editing, tagging and organizing ingredient assets.
    /// </summary>
    public class IngredientManagerWindow : EditorWindow
    {
        private const string DefaultDatabasePath = "Assets/Resources/IngredientDatabase.asset";
        private const string DefaultIngredientFolder = "Assets/Data/Ingredients";

        private IngredientDatabase _database;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _search = string.Empty;
        private IngredientCategory? _categoryFilter;
        private string _tagFilter = string.Empty;
        private int _selectedIndex = -1;
        private SerializedObject _selectedSerialized;
        private IngredientItem _selectedItem;
        private string _newTagDraft = string.Empty;
        private string _newStatKey = string.Empty;
        private float _newStatValue;

        [MenuItem("Soup/物品管理器 (Ingredient Manager)")]
        public static void Open()
        {
            var window = GetWindow<IngredientManagerWindow>();
            window.titleContent = new GUIContent("物品管理器");
            window.minSize = new Vector2(820, 520);
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
                EditorGUILayout.HelpBox("未找到 IngredientDatabase。点击上方「创建/加载数据库」。", MessageType.Warning);
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
                    IngredientDataSeeder.SeedSamples(openWindow: false);
                    LoadOrCreateDatabase();
                }

                GUILayout.Space(8);
                _search = GUILayout.TextField(_search ?? string.Empty, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField, GUILayout.MinWidth(160));

                EditorGUI.BeginChangeCheck();
                var categoryLabel = _categoryFilter.HasValue ? _categoryFilter.Value.ToString() : "全部分类";
                if (EditorGUILayout.DropdownButton(new GUIContent(categoryLabel), FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(100)))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("全部分类"), !_categoryFilter.HasValue, () => { _categoryFilter = null; });
                    foreach (IngredientCategory cat in System.Enum.GetValues(typeof(IngredientCategory)))
                    {
                        var captured = cat;
                        menu.AddItem(new GUIContent(cat.ToString()), _categoryFilter == captured, () => { _categoryFilter = captured; });
                    }
                    menu.ShowAsContext();
                }

                _tagFilter = GUILayout.TextField(_tagFilter ?? string.Empty, EditorStyles.toolbarTextField, GUILayout.Width(120));
                GUILayout.Label("标签过滤", EditorStyles.miniLabel, GUILayout.Width(54));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+ 新建食材", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    CreateIngredient();

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
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(280)))
            {
                EditorGUILayout.LabelField($"食材列表 ({GetFiltered().Count}/{_database.Count})", EditorStyles.boldLabel);
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll, "box");

                var filtered = GetFiltered();
                for (int i = 0; i < filtered.Count; i++)
                {
                    var item = filtered[i];
                    if (item == null) continue;

                    bool selected = item == _selectedItem;
                    var rect = GUILayoutUtility.GetRect(1, 36, GUILayout.ExpandWidth(true));
                    if (selected)
                        EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.90f, 0.35f));

                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        SelectItem(item);
                        Event.current.Use();
                    }

                    var iconRect = new Rect(rect.x + 4, rect.y + 2, 32, 32);
                    if (item.Icon != null)
                        GUI.DrawTexture(iconRect, item.Icon.texture, ScaleMode.ScaleToFit);
                    else
                        EditorGUI.DrawRect(iconRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));

                    var titleRect = new Rect(rect.x + 42, rect.y + 2, rect.width - 48, 18);
                    var subRect = new Rect(rect.x + 42, rect.y + 18, rect.width - 48, 16);
                    GUI.Label(titleRect, string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName, EditorStyles.boldLabel);
                    GUI.Label(subRect, $"{item.Category}  |  {item.Id}", EditorStyles.miniLabel);
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
                    EditorGUILayout.HelpBox("从左侧选择食材，或点击「+ 新建食材」。", MessageType.Info);
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

                if (_selectedItem.Icon != null)
                {
                    var preview = GUILayoutUtility.GetRect(72, 72, GUILayout.ExpandWidth(false));
                    GUI.DrawTexture(preview, _selectedItem.Icon.texture, ScaleMode.ScaleToFit);
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("分类与标签", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("category"), new GUIContent("分类"));

                var tagsProp = _selectedSerialized.FindProperty("tags");
                EditorGUILayout.PropertyField(tagsProp, new GUIContent("标签列表"), true);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _newTagDraft = EditorGUILayout.TextField("快速添加标签", _newTagDraft);
                    if (GUILayout.Button("添加", GUILayout.Width(60)) && !string.IsNullOrWhiteSpace(_newTagDraft))
                    {
                        tagsProp.arraySize++;
                        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = _newTagDraft.Trim();
                        _newTagDraft = string.Empty;
                    }
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("核心数值", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("price"), new GUIContent("价格"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("rarity"), new GUIContent("稀有度 (0-5)"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("maxStack"), new GUIContent("最大堆叠"));
                EditorGUILayout.PropertyField(_selectedSerialized.FindProperty("weight"), new GUIContent("重量"));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("玩法数值 (可自由增删)", EditorStyles.boldLabel);
                var statsProp = _selectedSerialized.FindProperty("stats");
                EditorGUILayout.PropertyField(statsProp, new GUIContent("Stats"), true);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _newStatKey = EditorGUILayout.TextField(_newStatKey, GUILayout.MinWidth(120));
                    _newStatValue = EditorGUILayout.FloatField(_newStatValue, GUILayout.Width(80));
                    if (GUILayout.Button("添加数值", GUILayout.Width(80)) && !string.IsNullOrWhiteSpace(_newStatKey))
                    {
                        statsProp.arraySize++;
                        var element = statsProp.GetArrayElementAtIndex(statsProp.arraySize - 1);
                        element.FindPropertyRelative("key").stringValue = _newStatKey.Trim();
                        element.FindPropertyRelative("value").floatValue = _newStatValue;
                        _newStatKey = string.Empty;
                        _newStatValue = 0f;
                    }
                }

                if (_selectedSerialized.ApplyModifiedProperties())
                {
                    _database.MarkDirty();
                    EditorUtility.SetDirty(_selectedItem);
                    EditorUtility.SetDirty(_database);
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "提示：修改后点工具栏「保存」。也可在 Project 中直接编辑单个 Ingredient 资产。",
                    MessageType.None);

                EditorGUILayout.EndScrollView();
            }
        }

        private List<IngredientItem> GetFiltered()
        {
            if (_database == null) return new List<IngredientItem>();

            IEnumerable<IngredientItem> query = _database.Ingredients.Where(i => i != null);

            if (!string.IsNullOrWhiteSpace(_search))
            {
                var key = _search.Trim();
                query = query.Where(i =>
                    (!string.IsNullOrEmpty(i.DisplayName) && i.DisplayName.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(i.Id) && i.Id.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                    i.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (_categoryFilter.HasValue)
                query = query.Where(i => i.Category == _categoryFilter.Value);

            if (!string.IsNullOrWhiteSpace(_tagFilter))
                query = query.Where(i => i.HasTag(_tagFilter.Trim()));

            return query.OrderBy(i => i.DisplayName).ToList();
        }

        private void SelectItem(IngredientItem item)
        {
            _selectedItem = item;
            _selectedIndex = _database != null ? _database.Ingredients.ToList().IndexOf(item) : -1;
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
                SelectItem(_database.Ingredients.FirstOrDefault(i => i != null));
            else
                SelectItem(_selectedItem);
        }

        private void EnsureFolders()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Data");
            EnsureFolder(DefaultIngredientFolder);
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

            _database = AssetDatabase.LoadAssetAtPath<IngredientDatabase>(DefaultDatabasePath);
            if (_database == null)
            {
                var guids = AssetDatabase.FindAssets("t:IngredientDatabase");
                if (guids.Length > 0)
                    _database = AssetDatabase.LoadAssetAtPath<IngredientDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (_database == null)
            {
                _database = CreateInstance<IngredientDatabase>();
                AssetDatabase.CreateAsset(_database, DefaultDatabasePath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[物品管理器] 已创建数据库: {DefaultDatabasePath}");
            }

            _database.RemoveNullEntries();
            _database.RebuildIndex();
            RefreshSelection();
            Repaint();
        }

        private void ScanAndSync()
        {
            if (_database == null) return;

            var guids = AssetDatabase.FindAssets("t:IngredientItem");
            int added = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<IngredientItem>(path);
                if (item == null) continue;
                if (_database.Add(item))
                    added++;
            }

            _database.RemoveNullEntries();
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[物品管理器] 同步完成，新增 {added} 个食材引用。当前总数 {_database.Count}。");
            RefreshSelection();
        }

        private void CreateIngredient()
        {
            if (_database == null) return;
            EnsureFolders();

            string baseName = "新食材";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{DefaultIngredientFolder}/Ingredient_{baseName}.asset");

            var item = CreateInstance<IngredientItem>();
            item.SetIdentity(IngredientItem.SanitizeId(Path.GetFileNameWithoutExtension(assetPath)), baseName);
            item.SetTags(new[] { "食材" });

            AssetDatabase.CreateAsset(item, assetPath);
            _database.Add(item);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SelectItem(item);
            Debug.Log($"[物品管理器] 已创建食材: {assetPath}");
        }

        private void DuplicateSelected()
        {
            if (_database == null || _selectedItem == null) return;

            string sourcePath = AssetDatabase.GetAssetPath(_selectedItem);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            if (!AssetDatabase.CopyAsset(sourcePath, newPath))
            {
                Debug.LogError("[物品管理器] 复制失败。");
                return;
            }

            var clone = AssetDatabase.LoadAssetAtPath<IngredientItem>(newPath);
            if (clone == null) return;

            string newDisplay = _selectedItem.DisplayName + " 副本";
            clone.SetIdentity(IngredientItem.SanitizeId(Path.GetFileNameWithoutExtension(newPath)), newDisplay);
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
            if (!EditorUtility.DisplayDialog("删除食材", $"确定删除「{_selectedItem.DisplayName}」？\n{path}", "删除", "取消"))
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
            Debug.Log("[物品管理器] 已保存。");
        }
    }
}
