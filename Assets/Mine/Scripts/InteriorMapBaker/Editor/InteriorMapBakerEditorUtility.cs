#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared Editor-only operations for creating and initializing Interior Map frameworks.
/// </summary>
public static class InteriorMapBakerEditorUtility
{
    private const string FrameworkName = "InteriorMapBaker";
    private const string VolumeName = "BakeVolume";
    private const string DirectionName = "BakeDirection";
    private const string CameraName = "BakeCamera";

    /// <summary>
    /// Creates a complete standard framework in the active scene.
    /// </summary>
    public static InteriorMapBaker CreateStandardFramework()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Create Interior Map Baker Framework");
        int undoGroup = Undo.GetCurrentGroup();

        var root = new GameObject(FrameworkName);
        Undo.RegisterCreatedObjectUndo(root, "Create Interior Map Baker Framework");
        var baker = Undo.AddComponent<InteriorMapBaker>(root);
        BindFrameworkComponents(baker);

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = root;
        SceneView.RepaintAll();
        return baker;
    }

    /// <summary>
    /// Creates any missing framework children on an existing scene component.
    /// </summary>
    public static void InitializeFramework(InteriorMapBaker baker)
    {
        if (baker == null)
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Create Interior Map Baker Framework");
        int undoGroup = Undo.GetCurrentGroup();
        Undo.RecordObject(baker, "Create Interior Map Baker Framework");
        BindFrameworkComponents(baker);

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = baker.gameObject;
        SceneView.RepaintAll();
    }

    // ════════════════════════════════════════════════════════════
    //  Component binding — reuse references and direct children before creation
    // ════════════════════════════════════════════════════════════
    private static void BindFrameworkComponents(InteriorMapBaker baker)
    {
        var serializedBaker = new SerializedObject(baker);
        SerializedProperty volumeProperty = serializedBaker.FindProperty("_volume");
        SerializedProperty directionProperty = serializedBaker.FindProperty("_direction");
        SerializedProperty cameraProperty = serializedBaker.FindProperty("_camera");

        InteriorMapBakeVolume volume = GetBoundOrDirectChild<InteriorMapBakeVolume>(
            volumeProperty, baker.transform);
        if (volume == null)
            volume = CreateChild<InteriorMapBakeVolume>(baker.transform, VolumeName);

        InteriorMapBakeDirection direction = GetBoundOrDirectChild<InteriorMapBakeDirection>(
            directionProperty, baker.transform);
        if (direction == null)
            direction = CreateChild<InteriorMapBakeDirection>(baker.transform, DirectionName);

        Camera bakeCamera = GetBoundOrDirectChild<Camera>(cameraProperty, baker.transform);
        if (bakeCamera == null)
            bakeCamera = CreateChild<Camera>(baker.transform, CameraName);

        volumeProperty.objectReferenceValue = volume;
        directionProperty.objectReferenceValue = direction;
        cameraProperty.objectReferenceValue = bakeCamera;
        serializedBaker.ApplyModifiedProperties();

        ConfigureBakeCamera(bakeCamera);
        baker.SyncBakeCamera();
        EditorUtility.SetDirty(baker);
    }

    private static T GetBoundOrDirectChild<T>(SerializedProperty property, Transform parent)
        where T : Component
    {
        var bound = property.objectReferenceValue as T;
        if (bound != null)
            return bound;

        for (int i = 0; i < parent.childCount; i++)
        {
            T component = parent.GetChild(i).GetComponent<T>();
            if (component != null)
                return component;
        }

        return null;
    }

    // ════════════════════════════════════════════════════════════
    //  Child creation — add a default-pose bake marker or camera below the baker
    // ════════════════════════════════════════════════════════════
    private static T CreateChild<T>(Transform parent, string objectName)
        where T : Component
    {
        var child = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(child, "Create Interior Map Baker child");
        Undo.SetTransformParent(child.transform, parent, "Parent Interior Map Baker child");
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return Undo.AddComponent<T>(child);
    }

    private static void ConfigureBakeCamera(Camera bakeCamera)
    {
        bakeCamera.enabled = false;
        bakeCamera.cameraType = CameraType.Reflection;
        bakeCamera.tag = "Untagged";
        bakeCamera.clearFlags = CameraClearFlags.SolidColor;
        bakeCamera.backgroundColor = Color.clear;
        bakeCamera.nearClipPlane = 0.01f;
        bakeCamera.farClipPlane = 100f;
        bakeCamera.fieldOfView = 90f;
        bakeCamera.aspect = 1f;
    }
}
#endif
