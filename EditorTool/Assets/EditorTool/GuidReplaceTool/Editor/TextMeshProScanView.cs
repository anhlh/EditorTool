using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace OriTool.GuidReplacement
{
    public class TextMeshProScanView : ScanView<TMP_FontAsset>
    {
        /// <summary>
        /// Key: FontAsset's Guid
        /// Value: (Material's Guid, Material)
        /// </summary>
        private readonly Dictionary<TMP_FontAsset, (string, Material)[]> _presetMaterials =
            new Dictionary<TMP_FontAsset, (string, Material)[]>();

        public override void ScanSources(Object o)
        {
            base.ScanSources(o);
            _presetMaterials.Clear();
            UpdateScanMaterial();
        }

        private void UpdateScanMaterial()
        {
            if (_scanResults == null || _scanResults.Length == 0) return;
            foreach (var scan in _scanResults)
                if (!_presetMaterials.ContainsKey(scan.Item2))
                    FindPresetMaterials(scan.Item2);
        }

        #region Draw

        protected override void DrawScanItemObjectInfo(float width, TMP_FontAsset source, ref TMP_FontAsset destination)
        {
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.ObjectField(source, typeof(TMP_FontAsset),
                    false, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(width / 2 - 50));

                if (GUILayout.Button("Replace =>", ButtonLabelStyle,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                {
                }

                destination = EditorGUILayout.ObjectField(
                        destination, typeof(TMP_FontAsset),
                        false, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(width / 2 - 50)) as
                    TMP_FontAsset;

                if (destination != null &&
                    !_presetMaterials.ContainsKey(destination))
                {
                    FindPresetMaterials(destination);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
                DrawFontAssetPresetMaterials(width / 2, source);
                DrawFontAssetPresetMaterials(width / 2, destination);
            }
            EditorGUILayout.EndHorizontal();
        }


        private void DrawFontAssetPresetMaterials(float width, TMP_FontAsset fontAsset)
        {
            if (fontAsset == null) return;
            (string, Material)[] mats;
            _presetMaterials.TryGetValue(fontAsset, out mats);
            if (mats == null) return;

            EditorGUILayout.BeginVertical();
            {
                for (var i = 0; i < mats.Length; i++)
                {
                    EditorGUILayout.TextField($"{mats[i].Item1}", GUILayout.Height(EditorGUIUtility.singleLineHeight),
                        GUILayout.Width(width));
                    EditorGUILayout.ObjectField(mats[i].Item2, typeof(Material),
                        false, GUILayout.Height(EditorGUIUtility.singleLineHeight),
                        GUILayout.Width(width));
                }
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        protected override void ReplaceGuid((string, TMP_FontAsset, TMP_FontAsset) scanObject, (string, Object) target)
        {
            var text = File.ReadAllText(target.Item1);

            var assetPath = AssetDatabase.GetAssetPath(scanObject.Item3);
            var toGuid = AssetDatabase.AssetPathToGUID(assetPath);

            //FontAsset replace
            text = text.Replace(scanObject.Item1, toGuid);

            //Material replace
            var sourceMaterials = _presetMaterials[scanObject.Item2];
            foreach (var source in sourceMaterials)
            {
                var suffix = source.Item2.name.Replace(scanObject.Item2.name, string.Empty);

                var desMat = GetMatchSuffixMaterial(suffix, _presetMaterials[scanObject.Item3]);
                if (desMat == null) continue;

                //Change material Guid
                text = text.Replace(source.Item1, desMat.Value.Item1);
            }

            File.WriteAllText(target.Item1, text);
        }

        private (string, Material)? GetMatchSuffixMaterial(string suffix, (string, Material)[] desMaterials)
        {
            foreach (var val in desMaterials)
            {
                if (val.Item2.name.Contains(suffix)) return val;
            }

            return null;
        }

        private void FindPresetMaterials(TMP_FontAsset fontAsset)
        {
            var allPathToAssetsList = new List<string>();
            var allMats = Directory.GetFiles(Application.dataPath, "*.mat", SearchOption.AllDirectories)
                .Where(_ => _.Contains(fontAsset.name));
            allPathToAssetsList.AddRange(allMats);

            var lstMat = new List<(string, Material)>();
            for (var i = 0; i < allPathToAssetsList.Count; i++)
            {
                var fullPath = allPathToAssetsList[i];
                var assetPath = fullPath.Replace(Application.dataPath, string.Empty);
                var path = "Assets" + assetPath;
                path = path.Replace(@"\", "/");
                var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
                var guid = AssetDatabase.AssetPathToGUID(path);
                lstMat.Add((guid, asset));
            }

            _presetMaterials.Add(fontAsset, lstMat.ToArray());
        }
    }
}