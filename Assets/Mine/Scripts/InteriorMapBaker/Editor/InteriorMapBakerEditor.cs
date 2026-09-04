#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for <see cref="InteriorMapBaker"/>.
/// Creates the scene framework used by the Interior Map Baker window.
/// </summary>
[CustomEditor(typeof(InteriorMapBaker))]
public sealed class InteriorMapBakerEditor : Editor
{
    private SerializedProperty _volumeProperty;
    private SerializedProperty _directionProperty;
    private SerializedProperty _cameraProperty;

    // ════════════════════════════════════════════════════════════
    //  Inspector lifecycle — cache the generated component references
    // ════════════════════════════════════════════════════════════
    private void OnEnable()
    {
        _volumeProperty = serializedObject.FindProperty("_volume");
        _directionProperty = serializedObject.FindProperty("_direction");
        _cameraProperty = serializedObject.FindProperty("_camera");
    }

    // ════════════════════════════════════════════════════════════
    //  Inspector layout — expose framework creation and current bindings
    // ════════════════════════════════════════════════════════════
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Interior Map Baker", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Create Framework generates or reuses the volume, direction, and bake camera child objects.\n"
            + "Then select this framework in the Tools/Interior Map Baker window.",
            MessageType.Info);

        if (GUILayout.Button("Create Framework", GUILayout.Height(30f)))
            InteriorMapBakerEditorUtility.InitializeFramework((InteriorMapBaker)target);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Current Framework Components", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(_volumeProperty, new GUIContent("Volume Object"));
            EditorGUILayout.PropertyField(_directionProperty, new GUIContent("Direction Object"));
            EditorGUILayout.PropertyField(_cameraProperty, new GUIContent("Bake Camera"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
