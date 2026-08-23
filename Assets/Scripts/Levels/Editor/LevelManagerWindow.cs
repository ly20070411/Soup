using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Soup.Levels.Editor
{
    /// <summary>
    /// Editor hub for creating and editing campaign level victory conditions.
    /// </summary>
    public class LevelManagerWindow : EditorWindow
    {
        private const string DefaultDatabasePath = "Assets/Resources/LevelDatabase.asset";
        private const string DefaultLevelFolder = "Assets/Data/Levels";

        private LevelDatabase _database;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _search = string.Empty;
        private LevelItem _selectedItem;
        private SerializedObject _selectedSerialized;

        [MenuItem("Soup/关卡管理器 (Level Manager)")]
        public static void Open()
        {
            var window = GetWindow<LevelManagerWindow>();
            window.titleContent = new GUIContent("关卡管理器");
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
            DrawHelp();

            if (_database == null)
            {
                EditorGUILayout.HelpBox("未找到 LevelDatabase。点击上方「创建/加载数据库」。", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawListPanel();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHelp()
        {
            EditorGUILayout.HelpBox(
                "每个关卡设定「目标分数」「挑战分数（可选）」与「最大回合」。\n" +
                "挑战分数仅作额外目标展示，不影响通关判定。\n" +
                "分数按本关开始后的增量计算（含回合用尽后的酸涩结算分）。\n" +
                "流程：回合用尽 → 结算酸涩 → 达标则关卡间奖励 → 下一关；未达标则失败。\n" +
                "通关全部关卡即为游戏胜利。列表按「顺序」升序排列。",
                MessageType.Info);
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
                    LevelDataSeeder.SeedSamples(openWindow: false);
                    LoadOrCreateDatabase();
                }

                GUILayout.Space(8);
                _search = GUILayout.TextField(
                    _search ?? string.Empty,
                    GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField,
                    GUILayout.MinWidth(160));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+ 新建关卡", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    CreateLevel();

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
                    $"关卡列表 ({GetFiltered().Count}/{_database.Count})",
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

                    var badgeRect = new Rect(rect.x + 4, rect.y + 6, 32, 32);
                    EditorGUI.DrawRect(badgeRect, new Color(0.35f, 0.55f, 0.40f, 1f));
                    GUI.Label(badgeRect, item.OrderIndex.ToString(), new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white }
                    });

                    var titleRect = new Rect(rect.x + 42, rect.y + 4, rect.width - 48, 18);
                    var subRect = new Rect(rect.x + 42, rect.y + 22, rect.width - 48, 16);
                    GUI.Label(
                        titleRect,
                        string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName,
                        EditorStyles.boldLabel);
                    GUI.Label(
                        subRect,
                        FormatLevelListSubtitle(item),
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
                    EditorGUILayout.HelpBox("从左侧选择关卡，或点击「+ 新建关卡」。", MessageType.Info);
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
                EditorGUILayout.LabelField("顺序", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    _selectedSerialized.FindProperty("orderIndex"),
                    new GUIContent("顺序", "越小越靠前。建议与关卡序号一致：1、2、3…"));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("胜利条件", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    _selectedSerialized.FindProperty("targetScore"),
                    new GUIContent("目标分数", "本关开始后需要达到的分数增量。"));
                EditorGUILayout.PropertyField(
                    _selectedSerialized.FindProperty("maxTurns"),
                    new GUIContent("最大回合", "本关允许的回合数；用尽未达标则失败。"));
                EditorGUILayout.PropertyField(
                    _selectedSerialized.FindProperty("challengeScore"),
                    new GUIContent("挑战分数", "可选。不影响通关，仅作额外目标展示。"));
                EditorGUILayout.PropertyField(
                    _selectedSerialized.FindProperty("ultimateChallengeScore"),
                    new GUIContent("终极挑战分数", "可选（如第五关）。不影响通关，仅作额外目标展示。"));

                if (_selectedSerialized.ApplyModifiedProperties())
                {
                    _database.MarkDirty();
                    EditorUtility.SetDirty(_selectedItem);
                    EditorUtility.SetDirty(_database);
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    $"摘要：在 {_selectedItem.MaxTurns} 回合内获得 {_selectedItem.TargetScore} 分即可通关。" +
                    (_selectedItem.HasChallengeScore
                        ? $"\n挑战分数 {_selectedItem.ChallengeScore}（不影响通关判定）。"
                        : string.Empty) +
                    (_selectedItem.HasUltimateChallengeScore
                        ? $"\n终极挑战分数 {_selectedItem.UltimateChallengeScore}（不影响通关判定）。"
                        : string.Empty) +
                    "\n第 MaxTurns 回合结束后先结算酸涩，再按热辣倍率乘总分，最后用最终得分判定是否达标；达标后进入关卡间。",
                    MessageType.None);

                EditorGUILayout.EndScrollView();
            }
        }

        private static string FormatLevelListSubtitle(LevelItem item)
        {
            if (item == null)
                return string.Empty;

            string line = $"目标 {item.TargetScore}";
            if (item.HasChallengeScore)
                line += $" / 挑战 {item.ChallengeScore}";
            if (item.HasUltimateChallengeScore)
                line += $" / 终极 {item.UltimateChallengeScore}";
            return $"{line} · {item.MaxTurns} 回合  |  {item.Id}";
        }

        private List<LevelItem> GetFiltered()
        {
            if (_database == null) return new List<LevelItem>();
            IEnumerable<LevelItem> query = _database.GetOrdered().Where(i => i != null);
            if (!string.IsNullOrWhiteSpace(_search))
            {
                var key = _search.Trim();
                query = query.Where(i =>
                    (!string.IsNullOrEmpty(i.DisplayName) && i.DisplayName.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrEmpty(i.Id) && i.Id.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || i.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return query.ToList();
        }

        private void SelectItem(LevelItem item)
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
                SelectItem(_database.GetOrdered().FirstOrDefault(i => i != null));
            else
                SelectItem(_selectedItem);
        }

        private void EnsureFolders()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Data");
            EnsureFolder(DefaultLevelFolder);
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
            _database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DefaultDatabasePath);
            if (_database == null)
            {
                var guids = AssetDatabase.FindAssets("t:LevelDatabase");
                if (guids.Length > 0)
                    _database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (_database == null)
            {
                _database = CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(_database, DefaultDatabasePath);
                AssetDatabase.SaveAssets();
            }

            _database.RemoveNullEntries();
            _database.RebuildIndex();
            RefreshSelection();
            Repaint();
        }

        private void ScanAndSync()
        {
            if (_database == null) return;
            int added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:LevelItem"))
            {
                var item = AssetDatabase.LoadAssetAtPath<LevelItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && _database.Add(item))
                    added++;
            }

            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[关卡管理器] 同步完成，新增 {added}。当前 {_database.Count}。");
            RefreshSelection();
        }

        private void CreateLevel()
        {
            if (_database == null) return;
            EnsureFolders();

            int nextOrder = 1;
            var ordered = _database.GetOrdered();
            if (ordered.Count > 0 && ordered[ordered.Count - 1] != null)
                nextOrder = ordered[ordered.Count - 1].OrderIndex + 1;

            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultLevelFolder}/Level_stage_{nextOrder}.asset");
            var item = CreateInstance<LevelItem>();
            item.SetIdentity($"stage_{nextOrder}", $"第{nextOrder}关");
            item.SetDescription("在限定回合内达到目标分数。");
            item.SetOrderIndex(nextOrder);
            item.SetVictory(50, 10);
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
            var clone = AssetDatabase.LoadAssetAtPath<LevelItem>(newPath);
            if (clone == null) return;

            int nextOrder = _selectedItem.OrderIndex + 1;
            clone.SetIdentity(
                LevelItem.SanitizeId(Path.GetFileNameWithoutExtension(newPath)),
                _selectedItem.DisplayName + " 副本");
            clone.SetOrderIndex(nextOrder);
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
            if (!EditorUtility.DisplayDialog("删除关卡", $"确定删除「{_selectedItem.DisplayName}」？\n{path}", "删除", "取消"))
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
            Debug.Log("[关卡管理器] 已保存。");
        }
    }
}
