using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OriTool.GuidReplacement
{
    /// <summary>
    /// Search and replace by guid Window
    /// </summary>
    public class GuidReplacementWindow : EditorWindow
    {
        private Object _objectFolder;

        private int _selected;

        private readonly string[] _options =
        {
            "Sprite", "Texture2D", "TMP_FontAsset", "Material", "Font (.otf)", "Prefab"
        };

        private string _highlightText = string.Empty;

        private IScanView _scanView;
        private const float MenuBarHeight = 20f;
        private bool _clickedScan = false;

        [MenuItem("OriTool/Open GuidReplacementWindow")]
        static void Init()
        {
            var window = (GuidReplacementWindow) GetWindow(typeof(GuidReplacementWindow));
            window.Show();
        }

        void OnGUI()
        {
            DrawMenuBar();
            DrawHighlightText();
            InitSwap();
            DrawScanSourceInput();
            DrawResult();
        }

        private void DrawMenuBar()
        {
            var rect = new Rect(0, 0, position.width, MenuBarHeight);

            GUILayout.BeginArea(rect, EditorStyles.toolbar);
            {
                GUILayout.BeginHorizontal();
                {
                    _selected = EditorGUILayout.Popup("Select", _selected, _options);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void DrawHighlightText()
        {
            EditorGUILayout.LabelField("", GUILayout.Height(MenuBarHeight));

            EditorGUILayout.LabelField("Highlight Text (Split by ',')", GUILayout.Height(MenuBarHeight));
            _highlightText = EditorGUILayout.TextArea(_highlightText, GUILayout.Height(MenuBarHeight));

            if (_scanView != null)
                _scanView.HighlightText = _highlightText.Length > 0 ? _highlightText.Split(',') : Array.Empty<string>();
        }

        private void DrawScanSourceInput()
        {
            GUILayout.BeginHorizontal();
            {
                _objectFolder = EditorGUILayout.ObjectField("Folder", _objectFolder, typeof(Object), false,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Search"))
                {
                    _clickedScan = true;
                    _scanView.ScanSources(_objectFolder);
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawResult()
        {
            if (!_scanView.HasResult)
            {
                if (!_clickedScan) return;
                EditorGUILayout.LabelField("Not found!");
                return;
            }

            _clickedScan = false;
            EditorGUILayout.BeginHorizontal();
            {
                _scanView?.DrawScanResult(position.width / 2f);
                _scanView.DrawRefObjectList(position.width / 2f);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void InitSwap()
        {
            switch (_selected)
            {
                case 0:
                    if (_scanView == null || _scanView.GetType() != typeof(ScanView<Sprite>))
                        _scanView = new ScanView<Sprite>();
                    break;
                case 1:
                    if (_scanView == null || _scanView.GetType() != typeof(ScanView<Texture2D>))
                        _scanView = new ScanView<Texture2D>();
                    break;
                case 2:
                    if (_scanView == null || _scanView.GetType() != typeof(TextMeshProScanView))
                        _scanView = new TextMeshProScanView();
                    break;
                case 3:
                    if (_scanView == null || _scanView.GetType() != typeof(ScanView<Material>))
                        _scanView = new ScanView<Material>();
                    break;
                case 4:
                    if (_scanView == null || _scanView.GetType() != typeof(ScanView<Font>))
                        _scanView = new ScanView<Font>();
                    break;

                case 5:
                    if (_scanView == null || _scanView.GetType() != typeof(ScanView<GameObject>))
                        _scanView = new ScanView<GameObject>();
                    break;
            }
        }
    }

    public static class ReplacementUtil
    {
        /// <summary>
        /// Folder
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Func<string, bool> GetFilter<T>() where T : Object
        {
            if (typeof(T) == typeof(Sprite) || typeof(T) == typeof(Texture2D))
            {
                return s => s.EndsWith(".png") || s.EndsWith(".jpg");
            }

            if (typeof(T) == typeof(TMP_FontAsset))
            {
                return s => s.EndsWith(".asset");
            }

            if (typeof(T) == typeof(Material))
            {
                return s => s.EndsWith(".mat");
            }

            if (typeof(T) == typeof(Font))
            {
                return s => s.EndsWith(".otf");
            }

            if (typeof(T) == typeof(GameObject))
            {
                return s => s.EndsWith(".prefab");
            }

            Debug.LogError($"Need add filter for type {typeof(T)}");
            return null;
        }

        public static string[] GetFilterPath<T>()
        {
            var allPathToAssetsList = new System.Collections.Generic.List<string>();

            var allPrefabs = Directory.GetFiles(Application.dataPath, "*.prefab", SearchOption.AllDirectories);
            allPathToAssetsList.AddRange(allPrefabs);
            var allScenes = Directory.GetFiles(Application.dataPath, "*.unity", SearchOption.AllDirectories);
            allPathToAssetsList.AddRange(allScenes);

            if (typeof(T) == typeof(Sprite) || typeof(T) == typeof(Texture2D))
            {
                var allAnimator = Directory.GetFiles(Application.dataPath, "*.controller", SearchOption.AllDirectories);
                allPathToAssetsList.AddRange(allAnimator);
                var allAnimationClip = Directory.GetFiles(Application.dataPath, "*.anim", SearchOption.AllDirectories);
                allPathToAssetsList.AddRange(allAnimationClip);
                var allMats = Directory.GetFiles(Application.dataPath, "*.mat", SearchOption.AllDirectories);
                allPathToAssetsList.AddRange(allMats);
                var spritePack = Directory.GetFiles(Application.dataPath, "*Pack.asset", SearchOption.AllDirectories);
                allPathToAssetsList.AddRange(spritePack);
            }

            return allPathToAssetsList.ToArray();
        }

        /// <summary>
        /// Find Ref
        /// </summary>
        /// <param name="guidToFind"></param>
        /// <returns></returns>
        public static Dictionary<(string, Object), int> FindRef<T>(string guidToFind)
        {
            var refObjs = new Dictionary<(string, Object), int>();
            var allPathToAssetsList = GetFilterPath<T>();

            for (var i = 0; i < allPathToAssetsList.Length; i++)
            {
                var assetPath = allPathToAssetsList[i];
                var text = File.ReadAllText(assetPath);
                var lines = text.Split('\n');
                for (var j = 0; j < lines.Length; j++)
                {
                    var line = lines[j];
                    if (!line.Contains("guid:")) continue;
                    if (!line.Contains(guidToFind)) continue;

                    var pathToRefAsset = assetPath.Replace(Application.dataPath, string.Empty);
                    pathToRefAsset = pathToRefAsset.Replace(".meta", string.Empty);
                    var path = "Assets" + pathToRefAsset;
                    path = path.Replace(@"\", "/");
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (asset != null)
                    {
                        if (!refObjs.ContainsKey((assetPath, asset)))
                        {
                            refObjs.Add((assetPath, asset), 1);
                        }
                        else
                        {
                            refObjs[(assetPath, asset)]++;
                        }
                    }
                    else
                    {
                        Debug.LogError(path + " could not be loaded");
                    }
                }
            }

            return refObjs;
        }

        /// <summary>
        /// Get all files in folder
        /// </summary>
        /// <param name="assetsFullPath"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEnumerable<(string, T, T)> Extract<T>(IEnumerable<string> assetsFullPath) where T : Object
        {
            if (typeof(T) == typeof(Sprite))
            {
                foreach (var path in assetsFullPath)
                {
                    var index = path.IndexOf("/Assets", StringComparison.Ordinal) + 1;
                    var items = AssetDatabase.LoadAllAssetsAtPath(path.Substring(index));
                    foreach (var item in items)
                    {
                        if (!(item is Sprite)) continue;
                        var spr = item as T;
                        var assetPath = AssetDatabase.GetAssetPath(spr);
                        yield return (AssetDatabase.AssetPathToGUID(assetPath), spr, null);
                    }
                }
            }
            else
            {
                foreach (var path in assetsFullPath)
                {
                    var index = path.IndexOf("/Assets", StringComparison.Ordinal) + 1;
                    var assetObject = AssetDatabase.LoadAssetAtPath(path.Substring(index), typeof(T)) as T;
                    var assetPath = AssetDatabase.GetAssetPath(assetObject);
                    yield return (AssetDatabase.AssetPathToGUID(assetPath), assetObject, null);
                }
            }
        }

        /// <summary>
        /// Replace guid 
        /// </summary>
        /// <param name="fromGuid"></param>
        /// <param name="toGuid"></param>
        /// <param name="targetPath"></param>
        public static void ReplaceGuid(string fromGuid, string toGuid, string targetPath)
        {
            var text = File.ReadAllText(targetPath);
            text = text.Replace(fromGuid, toGuid);
            File.WriteAllText(targetPath, text);
        }
    }

    public interface IScanView
    {
        string[] HighlightText { get; set; }
        void ScanSources(Object o);

        bool HasResult { get; }
        void DrawScanResult(float width);
        void DrawRefObjectList(float width);
    }
}