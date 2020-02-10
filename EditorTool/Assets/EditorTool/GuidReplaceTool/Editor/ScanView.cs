using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OriTool.GuidReplacement
{
    /// <summary>
    /// Display found items
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ScanView<T> : IScanView where T : Object
    {
        public string[] HighlightText { get; set; }

        /// <summary>
        /// Founds items check
        /// </summary>
        public bool HasResult => _scanResults?.Length > 0;

        /// <summary>
        /// Guid, Source, Replace
        /// </summary>
        protected (string, T, T)[] _scanResults;

        /// <summary>
        /// Path, Object, Ref count
        /// </summary>
        protected Dictionary<(string, Object), int> RefObjects = new Dictionary<(string, Object), int>();

        protected readonly List<(string, Object)> ProcessedGuid = new List<(string, Object)>();

        protected Vector2 ScanResultScrollPos;
        protected Vector2 RefResultScrollPos;
        protected int SelectedScanResultIndex = -1;


        private GUIStyle _buttonLabelStyle;

        protected GUIStyle ButtonLabelStyle => _buttonLabelStyle
                                               ?? (_buttonLabelStyle = new GUIStyle(GUI.skin.label)
                                               {
                                                   fontSize = 16,
                                                   alignment = TextAnchor.MiddleCenter,
                                                   border = {left = 0, top = 0, right = 0, bottom = 0}
                                               });

        /// <summary>
        /// Search
        /// </summary>
        /// <param name="o"></param>
        public virtual void ScanSources(Object o)
        {
            _scanResults = null;
            RefObjects.Clear();
            ProcessedGuid.Clear();
            SelectedScanResultIndex = -1;

            var assetPath = AssetDatabase.GetAssetPath(o);
            var filter = ReplacementUtil.GetFilter<T>();

            if (filter == null)
            {
                Debug.LogError($"Not support type {typeof(T)}");
                return;
            }

            //Folder
            if (o is DefaultAsset)
            {
                assetPath = assetPath.Replace("Assets/", string.Empty);
                var assetsPath = Directory
                    .EnumerateFiles($"{Application.dataPath}/{assetPath}", "*.*", SearchOption.AllDirectories)
                    .Where(filter);

                _scanResults = ReplacementUtil.Extract<T>(assetsPath).ToArray();
            }
            else Debug.LogError("Folder is required!!!");
        }

        #region Draw

        /// <summary>
        /// Display files in folder
        /// </summary>
        /// <param name="width"></param>
        void IScanView.DrawScanResult(float width) => DrawScanResult(width);

        protected virtual void DrawScanResult(float width)
        {
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField("Search result(s)", GUILayout.Width(width));
                ScanResultScrollPos =
                    EditorGUILayout.BeginScrollView(ScanResultScrollPos, GUILayout.Width(width));
                {
                    for (var i = 0; i < _scanResults.Length; i++)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.BeginVertical();
                            {
                                DrawScanItemHeader(i, _scanResults[i].Item1);
                                DrawScanItemObjectInfo(width, _scanResults[i].Item2, ref _scanResults[i].Item3);
                            }
                            EditorGUILayout.EndVertical();
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        protected virtual void DrawScanItemHeader(int index, string guid)
        {
            var normalColor = GUI.color;
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.TextField($"{guid}");

                GUI.color = index == SelectedScanResultIndex ? Color.red : normalColor;
                if (GUILayout.Button("Search Ref"))
                {
                    RefObjects.Clear();
                    ProcessedGuid.Clear();
                    SelectedScanResultIndex = index;
                    RefObjects = ReplacementUtil.FindRef<T>(guid);
                }

                GUI.color = normalColor;
            }
            EditorGUILayout.EndHorizontal();
        }

        protected virtual void DrawScanItemObjectInfo(float width, T source, ref T destination)
        {
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.ObjectField(source, typeof(T),
                    false, GUILayout.Height(80), GUILayout.Width(80));

                if (GUILayout.Button("Replace =>", ButtonLabelStyle, GUILayout.Height(80)))
                {
                }

                destination = EditorGUILayout.ObjectField(
                    destination, typeof(T),
                    false, GUILayout.Height(80), GUILayout.Width(80)) as T;
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Display Ref
        /// </summary>
        /// <param name="width"></param>
        void IScanView.DrawRefObjectList(float width) => DrawRefObjectList(width);

        protected virtual void DrawRefObjectList(float width)
        {
            if (_scanResults == null || _scanResults.Length == 0) return;
            if (RefObjects?.Count == 0) return;

            var normalColor = GUI.color;

            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField("Ref List", GUILayout.Width(width));
                RefResultScrollPos =
                    EditorGUILayout.BeginScrollView(RefResultScrollPos, GUILayout.Width(width));
                {
                    foreach (var kv in RefObjects)
                    {
                        EditorGUILayout.BeginVertical();
                        {
                            GUILayout.BeginHorizontal();
                            {
                                EditorGUILayout.ObjectField(kv.Key.Item2, typeof(Object), false);
                                if (ProcessedGuid.Contains(kv.Key))
                                {
                                    if (GUILayout.Button("Ref:0"))
                                    {
                                        Debug.LogError("Done!");
                                    }
                                }
                                else
                                {
                                    GUI.color = HighlightText.Any(text =>
                                        text.Trim().Length > 0 && kv.Key.Item1.Contains(text))
                                        ? Color.yellow
                                        : normalColor;

                                    if (GUILayout.Button($"Process (Ref:{kv.Value})"))
                                    {
                                        if (_scanResults[SelectedScanResultIndex].Item3 != null)
                                        {
                                            ReplaceGuid(_scanResults[SelectedScanResultIndex], kv.Key);
                                            ProcessedGuid.Add(kv.Key);
                                        }
                                        else
                                        {
                                            Debug.LogError("Need file to replace!");
                                        }
                                    }

                                    GUI.color = normalColor;
                                }
                            }
                            GUILayout.EndHorizontal();

                            EditorGUILayout.LabelField(kv.Key.Item1.Replace(Application.dataPath, string.Empty));
                        }
                        EditorGUILayout.EndVertical();
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        protected virtual void ReplaceGuid((string, T, T) scanObject, (string, Object) target)
        {
            var assetPath = AssetDatabase.GetAssetPath(scanObject.Item3);
            var toGuid = AssetDatabase.AssetPathToGUID(assetPath);
            ReplacementUtil.ReplaceGuid(scanObject.Item1, toGuid, target.Item1);
        }
    }
}