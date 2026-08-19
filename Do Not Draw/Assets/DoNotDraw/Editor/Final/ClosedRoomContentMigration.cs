using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoNotDraw.Narrative.Editor
{
    public static class ClosedRoomContentMigration
    {
        private const string AutoRunSessionKey =
            "DoNotDraw.ClosedRoomContentMigration.2026-08-19.v1";
        private const string TargetScenePath = "Assets/Scenes/ClosedRoom.unity";
        private const string SourceTitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string TitleRootName = "TITLE SCREEN";
        private const string CardRoot = "Assets/DoNotDraw/Narrative/Final/Cards";
        private const string CardArtRoot = "Assets/Art/Card";
        private const string PreplacedCardMaterialPath =
            "Assets/DoNotDraw/Materials/Final/PreplacedCardFour.mat";

        private static readonly (string cardAsset, string textureFile)[] CardArtMappings =
        {
            ("02_do_not_look_behind_early.asset", "Card1_DoNotLookBehindYou.png"),
            ("03_do_not_turn_off_light.asset", "Card2_DoNotTurnOffTheLight.png"),
            ("04_do_not_open_second_door.asset", "Card3_DoNotOpenTheSecondDoor.png"),
            ("05_do_not_enter.asset", "Card4_DoNotEnter.png"),
            ("07_do_not_look_at_door.asset", "Card5_DoNotLookAtTheDoor.png"),
            ("08_do_not_look_through_window.asset", "Card7_DoNotLookAtTheWindow.png"),
            ("09_you_already_did.asset", "Card7_YouAleadyDid.png"),
            ("10_do_not_draw_next_card.asset", "Card8_DoNotDrawTheNextCard.png"),
            ("11_do_not_draw_survival.asset", "Card9_DoNotDraw.png"),
            ("12_do_not_turn_around.asset", "Card10_DoNotTurnAround.png"),
            ("14_i_saw_you_look.asset", "Card11_ISawYouLook.png"),
            ("15_do_not_touch_door.asset", "Card12_DoNotTouchTheDoor.png"),
            ("16_why_did_you_open_it.asset", "Card13_WhyDidYouOpenIt.png"),
            ("17_do_not_blame_cards.asset", "Card14_DoNotBlameTheCards.png"),
            ("18_do_not_look_behind_door.asset", "Card15_DoNotLookBehindYou.png"),
            ("19_you_saw_it.asset", "Card16_YouSawIt.png"),
            ("20_do_not_leave.asset", "Card17_DoNotLeave.png")
        };

        [InitializeOnLoadMethod]
        private static void ScheduleAutoRunOnce()
        {
            if (Application.isBatchMode || SessionState.GetBool(AutoRunSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(AutoRunSessionKey, true);
            EditorApplication.delayCall += RunAutoMigration;
        }

        private static void RunAutoMigration()
        {
            if (EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RunAutoMigration;
                return;
            }

            try
            {
                if (!EditorSceneManager.SaveOpenScenes())
                {
                    throw new InvalidOperationException(
                        "Unity failed to preserve the currently open scenes before migration.");
                }

                Apply();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Do Not Draw/Migrate Title And Card Art To ClosedRoom")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before migrating ClosedRoom content.");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureAllCardTextures();
            ApplyCardArtToDefinitions();

            Scene targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            PatchCardPresenters(targetScene);
            PatchPreplacedCard(targetScene);

            Scene titleScene = EditorSceneManager.OpenScene(SourceTitleScenePath, OpenSceneMode.Additive);
            MigrateTitleScreen(titleScene, targetScene);
            EditorSceneManager.SetActiveScene(targetScene);
            EditorSceneManager.MarkSceneDirty(targetScene);
            if (!EditorSceneManager.SaveScene(targetScene))
            {
                throw new InvalidOperationException("Unity failed to save ClosedRoom after migration.");
            }

            EditorSceneManager.CloseScene(titleScene, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate(targetScene);
            Debug.Log("[ClosedRoom Migration] Title screen, card art, and card draw priority were applied successfully.");
        }

        private static void ConfigureAllCardTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CardArtRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                bool requiresReimport = importer.textureType != TextureImporterType.Default
                    || importer.wrapMode != TextureWrapMode.Clamp
                    || importer.npotScale != TextureImporterNPOTScale.None
                    || !importer.mipmapEnabled
                    || !importer.sRGBTexture
                    || importer.filterMode != FilterMode.Bilinear;
                if (!requiresReimport)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = true;
                importer.sRGBTexture = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        private static void ApplyCardArtToDefinitions()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:CardDefinition", new[] { CardRoot }))
            {
                CardDefinition card = AssetDatabase.LoadAssetAtPath<CardDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                SerializedObject serialized = new SerializedObject(card);
                serialized.FindProperty("faceTexture").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(card);
            }

            foreach ((string cardAsset, string textureFile) in CardArtMappings)
            {
                string cardPath = $"{CardRoot}/{cardAsset}";
                string texturePath = $"{CardArtRoot}/{textureFile}";
                CardDefinition card = AssetDatabase.LoadAssetAtPath<CardDefinition>(cardPath);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (card == null || texture == null)
                {
                    throw new InvalidOperationException(
                        $"Card art mapping is incomplete: '{cardPath}' -> '{texturePath}'.");
                }

                SerializedObject serialized = new SerializedObject(card);
                serialized.FindProperty("faceTexture").objectReferenceValue = texture;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(card);
            }
        }

        private static void PatchCardPresenters(Scene scene)
        {
            CardDeckPresenter[] presenters = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CardDeckPresenter>(true))
                .ToArray();
            if (presenters.Length < 2)
            {
                throw new InvalidOperationException("Both ClosedRoom card presenters must exist before migration.");
            }

            foreach (CardDeckPresenter presenter in presenters)
            {
                SerializedObject serialized = new SerializedObject(presenter);
                serialized.FindProperty("cardLayerSpacing").floatValue = 0.006f;
                serialized.FindProperty("sortingOrderStep").intValue = 10;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(presenter);
            }
        }

        private static void PatchPreplacedCard(Scene scene)
        {
            GameObject surface = FindSceneObject(scene, "Card Four Surface");
            if (surface == null)
            {
                throw new InvalidOperationException("The preplaced fourth card surface was not found.");
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{CardArtRoot}/Card3_DoNotOpenTheSecondDoor.png");
            Renderer renderer = surface.GetComponent<Renderer>();
            if (texture == null || renderer == null)
            {
                throw new InvalidOperationException("The preplaced fourth card could not receive its texture.");
            }

            Material material = GetOrCreatePreplacedCardMaterial(renderer.sharedMaterial, texture);
            renderer.sharedMaterial = material;
            GameObject fallbackText = FindSceneObject(scene, "Card Four Text");
            if (fallbackText != null)
            {
                UnityEngine.Object.DestroyImmediate(fallbackText);
            }
        }

        private static Material GetOrCreatePreplacedCardMaterial(Material fallback, Texture2D texture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(PreplacedCardMaterialPath);
            Shader shader = fallback != null
                ? fallback.shader
                : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, PreplacedCardMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", Vector2.one);
            }
            material.mainTexture = texture;
            material.mainTextureScale = Vector2.one;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.08f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void MigrateTitleScreen(Scene source, Scene target)
        {
            GameObject existing = target.GetRootGameObjects()
                .FirstOrDefault(root => root.name == TitleRootName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

            GameObject titleCanvas = FindRoot(source, "Title");
            GameObject eventSystem = FindRoot(source, "EventSystem");
            GameObject controllerObject = FindRoot(source, "TitleButtonClickEvent");
            TitleCameraMover cameraMover = source.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TitleCameraMover>(true))
                .SingleOrDefault();
            Camera titleCamera = cameraMover != null ? cameraMover.GetComponent<Camera>() : null;
            if (titleCanvas == null || eventSystem == null || controllerObject == null || titleCamera == null)
            {
                throw new InvalidOperationException("TitleScene is missing one or more title-only roots.");
            }

            GameObject cameraRoot = titleCamera.transform.root.gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(cameraRoot))
            {
                PrefabUtility.UnpackPrefabInstance(
                    cameraRoot,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            GameObject wrapper = new GameObject(TitleRootName);
            SceneManager.MoveGameObjectToScene(wrapper, source);
            wrapper.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            titleCanvas.transform.SetParent(wrapper.transform, true);
            eventSystem.transform.SetParent(wrapper.transform, true);
            controllerObject.transform.SetParent(wrapper.transform, true);
            cameraRoot.transform.SetParent(wrapper.transform, true);

            cameraRoot.name = "Title Camera";
            titleCamera.tag = "Untagged";
            titleCamera.depth = 0f;
            cameraRoot.SetActive(false);

            TitleButtonClickEvent controller = controllerObject.GetComponent<TitleButtonClickEvent>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("titlePresentationRoot").objectReferenceValue = wrapper;
            serialized.FindProperty("titleCamera").objectReferenceValue = titleCamera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            SceneManager.MoveGameObjectToScene(wrapper, target);
            wrapper.SetActive(true);
        }

        private static void Validate(Scene scene)
        {
            GameObject titleRoot = FindRoot(scene, TitleRootName);
            TitleButtonClickEvent controller = titleRoot != null
                ? titleRoot.GetComponentInChildren<TitleButtonClickEvent>(true)
                : null;
            TitleCameraMover titleCameraMover = titleRoot != null
                ? titleRoot.GetComponentInChildren<TitleCameraMover>(true)
                : null;
            if (titleRoot == null || controller == null || titleCameraMover == null)
            {
                throw new InvalidOperationException("ClosedRoom title hierarchy validation failed.");
            }
            if (titleCameraMover.CompareTag("MainCamera"))
            {
                throw new InvalidOperationException("The title camera must not replace the gameplay MainCamera tag.");
            }

            foreach ((string cardAsset, string _) in CardArtMappings)
            {
                CardDefinition card = AssetDatabase.LoadAssetAtPath<CardDefinition>($"{CardRoot}/{cardAsset}");
                if (card == null || card.FaceTexture == null)
                {
                    throw new InvalidOperationException($"Card art validation failed for '{cardAsset}'.");
                }
            }

            Renderer preplacedRenderer = FindSceneObject(scene, "Card Four Surface")?.GetComponent<Renderer>();
            if (preplacedRenderer == null || preplacedRenderer.sharedMaterial?.mainTexture == null)
            {
                throw new InvalidOperationException("Preplaced card art validation failed.");
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
        }

        private static GameObject FindSceneObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindChildRecursive(root.transform, name);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform found = FindChildRecursive(parent.GetChild(index), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
