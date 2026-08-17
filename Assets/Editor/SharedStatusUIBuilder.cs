using System;
using System.IO;
using System.Linq;
using Dungeon.Core;
using Dungeon.Player;
using Dungeon.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SharedStatusUIBuilder
{
    private const string SourceScenePath = "Assets/Scenes/DUNGEON_001.unity";
    private const string StatusPrefabPath = "Assets/Prefabs/UI/SharedStatus.prefab";
    private const string DatabasePrefabPath = "Assets/Prefabs/System/GameDatabase.prefab";

    private static readonly string[] StatusScenePaths =
    {
        SourceScenePath,
        "Assets/Scenes/Guild.unity",
        "Assets/Scenes/Module.unity",
        "Assets/Scenes/WorldMap.unity",
        "Assets/Scenes/Shop.unity"
    };

    private static readonly string[] DatabaseScenePaths =
    {
        "Assets/Scenes/Guild.unity",
        "Assets/Scenes/Module.unity",
        "Assets/Scenes/WorldMap.unity",
        "Assets/Scenes/Shop.unity"
    };

    [MenuItem("Tools/Dungeon/Rebuild Shared Status UI")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            BuildAll();
            Debug.Log("[SharedStatusUIBuilder] Shared Status UI rebuilt in five scenes.");
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    public static void BuildFromCommandLine()
    {
        try
        {
            BuildAll();
            Debug.Log("[SharedStatusUIBuilder] Command-line build completed.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void BuildAll()
    {
        EnsureFolder("Assets/Prefabs/UI");
        EnsureFolder("Assets/Prefabs/System");

        GameObject statusPrefab = BuildStatusPrefabFromDungeon();
        GameObject databasePrefab = BuildDatabasePrefab();

        foreach (string scenePath in StatusScenePaths)
        {
            bool installDatabase = DatabaseScenePaths.Contains(scenePath);
            InstallInScene(
                scenePath,
                statusPrefab,
                installDatabase ? databasePrefab : null);
        }

        AssetDatabase.SaveAssets();
    }

    private static GameObject BuildStatusPrefabFromDungeon()
    {
        Scene sourceScene = EditorSceneManager.OpenScene(
            SourceScenePath,
            OpenSceneMode.Single);
        GameObject sourceStatus = FindRoot(sourceScene, "Status");

        if (sourceStatus == null)
            throw new InvalidOperationException("DUNGEON_001/Status was not found.");

        GameObject existingPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(StatusPrefabPath);
        GameObject sourceAsset =
            PrefabUtility.GetCorrespondingObjectFromSource(sourceStatus);

        if (existingPrefab != null && sourceAsset == existingPrefab)
        {
            // Once DUNGEON_001 is linked, the shared prefab is the source of
            // truth. Applying an otherwise unchanged scene instance causes
            // Unity to regenerate every prefab local file ID on each rebuild.
            // Edit SharedStatus.prefab directly for later shared UI changes.
            ValidateStatusPrefab(existingPrefab);
            return existingPrefab;
        }

        GameObject clone = UnityEngine.Object.Instantiate(sourceStatus);
        clone.name = "SharedStatus";

        try
        {
            ClearExternalSceneReferences(clone);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                clone,
                StatusPrefabPath);

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Failed to save Status prefab: {StatusPrefabPath}");
            }

            ValidateStatusPrefab(prefab);
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clone);
        }
    }

    private static GameObject BuildDatabasePrefab()
    {
        GameObject temporary = new GameObject("GameDatabase");

        try
        {
            GameDatabase database = temporary.AddComponent<GameDatabase>();
            SerializedObject serializedDatabase = new SerializedObject(database);
            serializedDatabase.FindProperty("catalogResourcePath").stringValue = "Data/db";
            serializedDatabase.FindProperty("databaseJsonOverride").objectReferenceValue = null;
            serializedDatabase.FindProperty("itemIconResourceRoot").stringValue = "UI/ItemsIcons";
            serializedDatabase.FindProperty("equipmentIconResourceRoot").stringValue = "UI/EquipIcons";
            serializedDatabase.FindProperty("moduleIconResourceRoot").stringValue = "UI/ModuleIcons";
            serializedDatabase.FindProperty("iconOverrides").arraySize = 0;
            serializedDatabase.FindProperty("headFallbackIcon").objectReferenceValue = null;
            serializedDatabase.FindProperty("bodyFallbackIcon").objectReferenceValue = null;
            serializedDatabase.FindProperty("legsFallbackIcon").objectReferenceValue = null;
            serializedDatabase.FindProperty("unknownFallbackIcon").objectReferenceValue = null;
            serializedDatabase.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                temporary,
                DatabasePrefabPath);

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Failed to save GameDatabase prefab: {DatabasePrefabPath}");
            }

            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temporary);
        }
    }

    private static void InstallInScene(
        string scenePath,
        GameObject statusPrefab,
        GameObject databasePrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject existingStatus = FindRoot(scene, "Status");
        int siblingIndex = existingStatus != null
            ? existingStatus.transform.GetSiblingIndex()
            : scene.rootCount;

        GameObject statusInstance;
        GameObject existingSource = existingStatus != null
            ? PrefabUtility.GetCorrespondingObjectFromSource(existingStatus)
            : null;

        if (existingStatus != null && existingSource == statusPrefab)
        {
            PrefabUtility.RevertPrefabInstance(
                existingStatus,
                InteractionMode.AutomatedAction);
            statusInstance = existingStatus;
        }
        else
        {
            if (existingStatus != null)
                UnityEngine.Object.DestroyImmediate(existingStatus);

            statusInstance = PrefabUtility.InstantiatePrefab(
                statusPrefab,
                scene) as GameObject;
        }

        if (statusInstance == null)
            throw new InvalidOperationException($"Failed to install Status: {scenePath}");

        statusInstance.name = "Status";
        statusInstance.transform.SetSiblingIndex(
            Mathf.Clamp(siblingIndex, 0, scene.rootCount - 1));

        if (databasePrefab != null)
            InstallDatabase(scene, databasePrefab);

        ValidateScene(scene, statusPrefab, databasePrefab != null);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void InstallDatabase(Scene scene, GameObject databasePrefab)
    {
        GameDatabase[] databases = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<GameDatabase>(true))
            .ToArray();
        GameObject retained = null;

        foreach (GameDatabase database in databases)
        {
            GameObject candidate = database.gameObject;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(candidate);

            if (retained == null && source == databasePrefab)
            {
                retained = candidate;
                PrefabUtility.RevertPrefabInstance(
                    candidate,
                    InteractionMode.AutomatedAction);
                continue;
            }

            UnityEngine.Object.DestroyImmediate(candidate);
        }

        if (retained == null)
        {
            retained = PrefabUtility.InstantiatePrefab(
                databasePrefab,
                scene) as GameObject;
        }

        if (retained == null)
            throw new InvalidOperationException($"Failed to install GameDatabase: {scene.path}");

        retained.name = "GameDatabase";
    }

    private static void ClearExternalSceneReferences(GameObject root)
    {
        Component[] components = root.GetComponentsInChildren<Component>(true);

        foreach (Component component in components)
        {
            if (component == null)
                continue;

            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            bool changed = false;

            while (property.Next(enterChildren))
            {
                enterChildren = false;

                if (property.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                UnityEngine.Object reference = property.objectReferenceValue;

                if (reference == null || AssetDatabase.Contains(reference))
                    continue;

                if (IsInsideHierarchy(reference, root.transform))
                    continue;

                property.objectReferenceValue = null;
                changed = true;
            }

            if (changed)
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static bool IsInsideHierarchy(
        UnityEngine.Object reference,
        Transform root)
    {
        Transform referencedTransform = reference switch
        {
            GameObject gameObject => gameObject.transform,
            Component component => component.transform,
            _ => null
        };

        return referencedTransform != null &&
               (referencedTransform == root || referencedTransform.IsChildOf(root));
    }

    private static void ValidateStatusPrefab(GameObject prefab)
    {
        RequireComponent<StatusWindowController>(prefab, "StatusWindowController");
        RequireComponent<StatusTabSwitcher>(prefab, "StatusTabSwitcher");
        RequireComponent<StatusAdventureRecordPageUI>(prefab, "StatusAdventureRecordPageUI");
        RequireComponent<StatusPageUI>(prefab, "StatusPageUI");
        RequireComponent<EquipmentPageUI>(prefab, "EquipmentPageUI");
        RequireComponent<ConsumablePageUI>(prefab, "ConsumablePageUI");
        RequireComponent<ExpendablesPageUI>(prefab, "ExpendablesPageUI");
        RequireComponent<ItemEquipmentInfoPopupUI>(prefab, "ItemEquipmentInfoPopupUI");

        if (prefab.GetComponentsInChildren<QuickSlotUI>(true).Length < 5)
            throw new InvalidOperationException("Shared Status prefab has fewer than five quick slots.");
    }

    private static void ValidateScene(
        Scene scene,
        GameObject expectedStatusPrefab,
        bool requireDatabase)
    {
        GameObject status = FindRoot(scene, "Status");

        if (status == null ||
            PrefabUtility.GetCorrespondingObjectFromSource(status) != expectedStatusPrefab)
        {
            throw new InvalidOperationException($"Shared Status prefab is not linked: {scene.path}");
        }

        ValidateStatusPrefab(status);

        bool hasSaveFile = scene.GetRootGameObjects()
            .Any(root => root.GetComponentInChildren<StatusSaveFile>(true) != null);
        bool hasHealth = scene.GetRootGameObjects()
            .Any(root => root.GetComponentInChildren<Health>(true) != null);

        if (!hasSaveFile)
            throw new InvalidOperationException($"StatusSaveFile is missing: {scene.path}");

        if (!hasHealth)
            throw new InvalidOperationException($"Health is missing: {scene.path}");

        if (requireDatabase)
        {
            bool hasDatabase = scene.GetRootGameObjects()
                .Any(root => root.GetComponentInChildren<GameDatabase>(true) != null);

            if (!hasDatabase)
                throw new InvalidOperationException($"GameDatabase is missing: {scene.path}");
        }
    }

    private static void RequireComponent<T>(GameObject root, string label)
        where T : Component
    {
        if (root.GetComponentInChildren<T>(true) == null)
            throw new InvalidOperationException($"{label} is missing from Shared Status prefab.");
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == name);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string leaf = Path.GetFileName(folderPath);

        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(leaf))
            throw new InvalidOperationException($"Invalid folder path: {folderPath}");

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
