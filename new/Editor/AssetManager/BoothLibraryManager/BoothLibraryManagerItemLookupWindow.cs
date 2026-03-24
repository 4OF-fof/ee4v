using System;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.BoothLibraryManager
{
    internal sealed class BoothLibraryManagerItemLookupWindow : EditorWindow
    {
        private string _databasePath;
        private string _itemIdText = string.Empty;
        private string _statusMessage = string.Empty;
        private BoothLibraryManagerItemRecord _currentItem;
        private Vector2 _scrollPosition;

        [MenuItem("Debug/AssetManager/Booth Library Manager Item Lookup")]
        private static void ShowWindow()
        {
            var window = GetWindow<BoothLibraryManagerItemLookupWindow>();
            window.titleContent = new GUIContent("BLM Item Lookup");
            window.minSize = new Vector2(520f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(_databasePath))
            {
                _databasePath = BoothLibraryManagerApi.GetDefaultDatabasePath();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Booth Library Manager item lookup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Temporary verification window. Enter a Booth item ID and read the current data.db snapshot.",
                MessageType.Info);

            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Database Path");
            _databasePath = EditorGUILayout.TextField(_databasePath ?? string.Empty);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Default", GUILayout.Width(100f)))
                {
                    _databasePath = BoothLibraryManagerApi.GetDefaultDatabasePath();
                }

                var exists = BoothLibraryManagerApi.DatabaseExists(_databasePath);
                EditorGUILayout.LabelField(exists ? "Exists" : "Missing");
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Booth Item ID");
            _itemIdText = EditorGUILayout.TextField(_itemIdText ?? string.Empty);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load", GUILayout.Width(100f)))
                {
                    LoadItem();
                }

                if (GUILayout.Button("Clear", GUILayout.Width(100f)))
                {
                    _currentItem = null;
                    _statusMessage = string.Empty;
                }
            }

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(_statusMessage, _currentItem == null ? MessageType.Warning : MessageType.None);
            }

            EditorGUILayout.Space(8f);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_currentItem != null)
            {
                DrawReadOnlyField("Item ID", _currentItem.BoothItemId.ToString());
                DrawReadOnlyField("Name", _currentItem.Name);
                DrawReadOnlyField("Item URL", _currentItem.ItemUrl);
                DrawReadOnlyField("Description", _currentItem.Description, minHeight: 84f);
                DrawReadOnlyField("Thumbnail URL", _currentItem.ThumbnailUrl);
                DrawReadOnlyField("Shop Name", _currentItem.ShopName);
                DrawReadOnlyField("Shop URL", _currentItem.ShopUrl);
                DrawReadOnlyField("Shop Thumbnail URL", _currentItem.ShopThumbnailUrl);
                DrawReadOnlyField("Tags", string.Join(", ", _currentItem.Tags ?? Array.Empty<string>()), minHeight: 48f);
            }

            EditorGUILayout.EndScrollView();
        }

        private void LoadItem()
        {
            _currentItem = null;

            long boothItemId;
            if (!long.TryParse(_itemIdText, out boothItemId) || boothItemId <= 0)
            {
                _statusMessage = "Item ID must be a positive integer.";
                return;
            }

            try
            {
                var item = BoothLibraryManagerApi.GetItemById(boothItemId, _databasePath);
                if (item == null)
                {
                    _statusMessage = "Item was not found in the current database.";
                    return;
                }

                _currentItem = item;
                _statusMessage = "Loaded.";
            }
            catch (Exception exception)
            {
                _statusMessage = exception.Message;
            }
        }

        private static void DrawReadOnlyField(string label, string value, float minHeight = 34f)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            var style = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true
            };

            var content = string.IsNullOrEmpty(value) ? string.Empty : value;
            var requiredHeight = Mathf.Max(minHeight, style.CalcHeight(new GUIContent(content), EditorGUIUtility.currentViewWidth - 40f));
            EditorGUILayout.SelectableLabel(content, style, GUILayout.MinHeight(requiredHeight));
            EditorGUILayout.Space(4f);
        }
    }
}
