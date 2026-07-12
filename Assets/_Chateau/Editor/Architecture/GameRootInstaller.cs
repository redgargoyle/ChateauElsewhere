#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Chateau.Architecture;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chateau.Editor.Architecture
{
    public static class GameRootInstaller
    {
        public const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
        public const string DatabaseAssetPath = "Assets/_Chateau/Data/GameDatabase.asset";
        private const string RootObjectName = "Chateau_GameRoot";

        [MenuItem("Tools/Chateau/Architecture/Install or Refresh Gameplay GameRoot")]
        public static void InstallGameplaySceneFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            InstallGameplayScene(saveScene: true);
        }

        [MenuItem("Tools/Chateau/Architecture/Validate Active Scene")]
        public static void ValidateActiveSceneFromMenu()
        {
            ValidationReport report = ValidateScene(SceneManager.GetActiveScene());
            report.LogToUnity($"Chateau architecture validation: {SceneManager.GetActiveScene().name}");

            if (!report.HasErrors)
            {
                Debug.Log("Active-scene architecture validation passed.");
            }
        }

        /// <summary>
        /// Batch-mode entry point for Codex/CI:
        /// Unity -batchmode -quit -projectPath ... -executeMethod Chateau.Editor.Architecture.GameRootInstaller.InstallGameplaySceneBatch
        /// </summary>
        public static void InstallGameplaySceneBatch()
        {
            try
            {
                InstallGameplayScene(saveScene: true);
                ValidationReport report = ValidateScene(SceneManager.GetActiveScene());
                report.LogToUnity("Chateau batch architecture validation");

                if (report.HasErrors)
                {
                    throw new InvalidOperationException($"Architecture validation failed with {report.ErrorCount} error(s).");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static GameRoot InstallGameplayScene(bool saveScene)
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            GameRoot gameRoot = FindOptionalSingleInScene<GameRoot>(scene);
            GameObject rootObject;

            if (gameRoot == null)
            {
                rootObject = new GameObject(RootObjectName);
                Undo.RegisterCreatedObjectUndo(rootObject, "Create Chateau GameRoot");
                gameRoot = Undo.AddComponent<GameRoot>(rootObject);
            }
            else
            {
                rootObject = gameRoot.gameObject;
                rootObject.name = RootObjectName;
            }

            RoomNavigationManager navigationManager = FindOptionalSingleInScene<RoomNavigationManager>(scene);
            if (navigationManager == null)
            {
                navigationManager = Undo.AddComponent<RoomNavigationManager>(rootObject);
            }

            DoorPromptSequenceController promptController = FindOptionalSingleInScene<DoorPromptSequenceController>(scene);
            if (promptController == null)
            {
                promptController = Undo.AddComponent<DoorPromptSequenceController>(rootObject);
            }

            SubtitleService subtitleService = FindOptionalSingleInScene<SubtitleService>(scene);
            if (subtitleService == null)
            {
                subtitleService = Undo.AddComponent<SubtitleService>(rootObject);
            }

            DialogueSpeechService dialogueService = FindOptionalSingleInScene<DialogueSpeechService>(scene);
            if (dialogueService == null)
            {
                dialogueService = Undo.AddComponent<DialogueSpeechService>(rootObject);
            }

            GameDatabase database = LoadOrCreateDatabase();
            GameServiceBase[] services = FindAllInScene<GameServiceBase>(scene);
            ChateauBehaviour[] behaviours = FindAllInScene<ChateauBehaviour>(scene);

            Array.Sort(services, CompareServices);
            Array.Sort(behaviours, CompareBehaviours);

            SerializedObject serializedRoot = new SerializedObject(gameRoot);
            serializedRoot.FindProperty("gameDatabase").objectReferenceValue = database;
            AssignObjectArray(serializedRoot.FindProperty("services"), services);
            AssignObjectArray(serializedRoot.FindProperty("sceneBehaviours"), FilterNonServiceBehaviours(behaviours));
            serializedRoot.FindProperty("initializeOnAwake").boolValue = true;
            serializedRoot.FindProperty("failStartupOnValidationErrors").boolValue = false;
            serializedRoot.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(gameRoot);
            EditorSceneManager.MarkSceneDirty(scene);

            ValidationReport report = ValidateScene(scene);
            report.LogToUnity("Chateau GameRoot installation validation");

            if (report.HasErrors)
            {
                throw new InvalidOperationException($"GameRoot installation produced {report.ErrorCount} validation error(s). The scene was not saved.");
            }

            if (saveScene && !EditorSceneManager.SaveScene(scene))
            {
                throw new IOException($"Unity could not save {GameplayScenePath}.");
            }

            Debug.Log($"Installed Chateau GameRoot with {services.Length} service(s) in {GameplayScenePath}.", gameRoot);
            Selection.activeObject = gameRoot;
            return gameRoot;
        }

        public static ValidationReport ValidateScene(Scene scene)
        {
            ValidationReport report = new ValidationReport();

            if (!scene.IsValid() || !scene.isLoaded)
            {
                report.AddError("The target scene is not loaded.");
                return report;
            }

            RequireExactlyOne<GameRoot>(scene, report, "composition root");
            RequireExactlyOne<ChapterManager>(scene, report, "game/chapter flow owner");
            RequireExactlyOne<ChapterClock>(scene, report, "game clock");
            RequireExactlyOne<ChapterEventScheduler>(scene, report, "chapter scheduler");
            RequireExactlyOne<CameraManager>(scene, report, "camera service");
            RequireExactlyOne<RoomNavigationManager>(scene, report, "room navigation owner");
            RequireExactlyOne<DoorPromptSequenceController>(scene, report, "door prompt presenter");
            RequireExactlyOne<SubtitleService>(scene, report, "subtitle service");
            RequireExactlyOne<DialogueSpeechService>(scene, report, "dialogue/voice service");
            RequireExactlyOne<Chateau.UI.GameTimeHUD>(scene, report, "global game-time HUD");
            ValidateMissingScripts(scene, report);

            GameRoot[] roots = FindAllInScene<GameRoot>(scene);
            if (roots.Length == 1)
            {
                roots[0].ValidateConfiguration(report);
            }

            return report;
        }

        private static GameDatabase LoadOrCreateDatabase()
        {
            GameDatabase database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabaseAssetPath);
            if (database != null)
            {
                return database;
            }

            string directory = Path.GetDirectoryName(DatabaseAssetPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                CreateFolders(directory);
            }

            database = ScriptableObject.CreateInstance<GameDatabase>();
            AssetDatabase.CreateAsset(database, DatabaseAssetPath);
            AssetDatabase.SaveAssets();
            return database;
        }

        private static void CreateFolders(string projectRelativeDirectory)
        {
            string[] parts = projectRelativeDirectory.Replace('\\', '/').Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static T FindOptionalSingleInScene<T>(Scene scene) where T : Component
        {
            T[] found = FindAllInScene<T>(scene);

            if (found.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Scene '{scene.name}' contains {found.Length} {typeof(T).Name} components. Resolve duplicates before installing the architecture root.");
            }

            return found.Length == 1 ? found[0] : null;
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<T> inScene = new List<T>();

            for (int i = 0; i < all.Length; i++)
            {
                T item = all[i];
                if (item != null && item.gameObject.scene == scene)
                {
                    inScene.Add(item);
                }
            }

            return inScene.ToArray();
        }

        private static void ValidateMissingScripts(Scene scene, ValidationReport report)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                ValidateMissingScriptsRecursive(roots[i], report);
            }
        }

        private static void ValidateMissingScriptsRecursive(GameObject gameObject, ValidationReport report)
        {
            if (gameObject == null)
            {
                return;
            }

            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            if (missingCount > 0)
            {
                report.AddError(
                    $"GameObject '{GetHierarchyPath(gameObject.transform)}' contains {missingCount} missing MonoBehaviour script reference(s).",
                    gameObject);
            }

            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                ValidateMissingScriptsRecursive(transform.GetChild(i).gameObject, report);
            }
        }

        private static void RequireExactlyOne<T>(Scene scene, ValidationReport report, string role) where T : Component
        {
            T[] found = FindAllInScene<T>(scene);
            if (found.Length != 1)
            {
                report.AddError($"Scene '{scene.name}' requires exactly one {typeof(T).Name} ({role}); found {found.Length}.");
            }
        }

        private static ChateauBehaviour[] FilterNonServiceBehaviours(ChateauBehaviour[] behaviours)
        {
            List<ChateauBehaviour> filtered = new List<ChateauBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && !(behaviours[i] is IGameService))
                {
                    filtered.Add(behaviours[i]);
                }
            }

            return filtered.ToArray();
        }

        private static void AssignObjectArray<T>(SerializedProperty arrayProperty, T[] values) where T : UnityEngine.Object
        {
            arrayProperty.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static int CompareServices(GameServiceBase left, GameServiceBase right)
        {
            int order = left.InitializationOrder.CompareTo(right.InitializationOrder);
            return order != 0 ? order : CompareBehaviours(left, right);
        }

        private static int CompareBehaviours(ChateauBehaviour left, ChateauBehaviour right)
        {
            string leftPath = left == null ? string.Empty : GetHierarchyPath(left.transform);
            string rightPath = right == null ? string.Empty : GetHierarchyPath(right.transform);
            int pathComparison = string.CompareOrdinal(leftPath, rightPath);
            return pathComparison != 0
                ? pathComparison
                : string.CompareOrdinal(left?.GetType().FullName, right?.GetType().FullName);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
#endif
