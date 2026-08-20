using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DoNotDraw.Narrative.Editor
{
    public sealed class CardImageBindingRepairResult
    {
        public readonly List<string> Errors = new List<string>();
        public int RepairedCards;
        public int ReimportedTextures;
        public bool IsValid => Errors.Count == 0;
    }

    public static class CardImageBindingEditor
    {
        public const string FinalCardRoot = "Assets/DoNotDraw/Narrative/Final/Cards";
        public const string CardArtRoot = "Assets/Art/Card";

        private static bool repairScheduled;
        private static bool repairInProgress;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            ScheduleRepair();
        }

        [MenuItem("Tools/Do Not Draw/Repair Card Image Bindings")]
        public static void RepairAllAndLog()
        {
            CardImageBindingRepairResult result = RepairAll(true);
            LogResult(result, true);
        }

        public static CardImageBindingRepairResult RepairAll(bool saveAssets)
        {
            CardImageBindingRepairResult result = new CardImageBindingRepairResult();
            if (repairInProgress)
            {
                return result;
            }

            repairInProgress = true;
            try
            {
                string[] guids = AssetDatabase.FindAssets(
                    "t:CardDefinition",
                    new[] { FinalCardRoot });
                foreach (string guid in guids)
                {
                    string cardPath = AssetDatabase.GUIDToAssetPath(guid);
                    CardDefinition card = AssetDatabase.LoadAssetAtPath<CardDefinition>(cardPath);
                    if (card == null)
                    {
                        result.Errors.Add($"Could not load CardDefinition '{cardPath}'.");
                        continue;
                    }

                    RepairCard(card, cardPath, result);
                }

                if (saveAssets && result.RepairedCards > 0)
                {
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                repairInProgress = false;
            }

            return result;
        }

        internal static void ScheduleRepair()
        {
            if (repairScheduled)
            {
                return;
            }

            repairScheduled = true;
            EditorApplication.delayCall += RepairAfterImport;
        }

        private static void RepairAfterImport()
        {
            repairScheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleRepair();
                return;
            }

            CardImageBindingRepairResult result = RepairAll(true);
            LogResult(result, result.RepairedCards > 0 || result.ReimportedTextures > 0);
        }

        private static void RepairCard(
            CardDefinition card,
            string cardPath,
            CardImageBindingRepairResult result)
        {
            SerializedObject serialized = new SerializedObject(card);
            SerializedProperty textureProperty = serialized.FindProperty("faceTexture");
            SerializedProperty pathProperty = serialized.FindProperty("faceTextureAssetPath");
            if (textureProperty == null || pathProperty == null)
            {
                result.Errors.Add($"Card image fields are missing from '{cardPath}'.");
                return;
            }

            Texture2D currentTexture = textureProperty.objectReferenceValue as Texture2D;
            string storedPath = NormalizeAssetPath(pathProperty.stringValue);
            if (string.IsNullOrEmpty(storedPath) && currentTexture != null)
            {
                storedPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(currentTexture));
                pathProperty.stringValue = storedPath;
            }

            if (string.IsNullOrEmpty(storedPath))
            {
                result.Errors.Add(
                    $"Card '{card.StableId}' has neither a face texture nor a stable asset path ({cardPath}).");
                return;
            }

            if (!storedPath.StartsWith(CardArtRoot + "/", StringComparison.Ordinal))
            {
                result.Errors.Add(
                    $"Card '{card.StableId}' points outside the authoritative card art folder: '{storedPath}'.");
                return;
            }

            if (ConfigureTextureImporter(storedPath))
            {
                result.ReimportedTextures++;
            }

            Texture2D expectedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(storedPath);
            if (expectedTexture == null)
            {
                result.Errors.Add(
                    $"Card '{card.StableId}' cannot resolve its face texture at '{storedPath}'.");
                return;
            }

            if (currentTexture != expectedTexture)
            {
                textureProperty.objectReferenceValue = expectedTexture;
            }

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(card);
                result.RepairedCards++;
            }
        }

        private static bool ConfigureTextureImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return false;
            }

            bool requiresReimport = importer.textureType != TextureImporterType.Default
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.npotScale != TextureImporterNPOTScale.None
                || !importer.mipmapEnabled
                || !importer.sRGBTexture
                || importer.filterMode != FilterMode.Bilinear;
            if (!requiresReimport)
            {
                return false;
            }

            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return true;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/');
        }

        private static void LogResult(CardImageBindingRepairResult result, bool logSuccess)
        {
            foreach (string error in result.Errors)
            {
                Debug.LogError($"[Card Image Binding] {error}");
            }

            if (result.IsValid && logSuccess)
            {
                Debug.Log(
                    $"[Card Image Binding] PASS - repaired {result.RepairedCards} card assets, "
                    + $"reimported {result.ReimportedTextures} textures.");
            }
        }
    }

    public sealed class CardImageBindingAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (TouchesCardBindings(importedAssets)
                || TouchesCardBindings(deletedAssets)
                || TouchesCardBindings(movedAssets)
                || TouchesCardBindings(movedFromAssetPaths))
            {
                CardImageBindingEditor.ScheduleRepair();
            }
        }

        private static bool TouchesCardBindings(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return false;
            }

            foreach (string path in paths)
            {
                string normalized = path?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(normalized)
                    && (normalized.StartsWith(CardImageBindingEditor.CardArtRoot + "/", StringComparison.Ordinal)
                        || normalized.StartsWith(CardImageBindingEditor.FinalCardRoot + "/", StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class CardImageBindingBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            CardImageBindingRepairResult result = CardImageBindingEditor.RepairAll(true);
            if (!result.IsValid)
            {
                throw new BuildFailedException(
                    "Card image binding validation failed:\n" + string.Join("\n", result.Errors));
            }
        }
    }
}
