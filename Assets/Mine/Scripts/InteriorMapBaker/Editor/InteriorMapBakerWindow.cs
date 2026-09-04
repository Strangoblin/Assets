#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window for selecting an InteriorMapBaker scene framework and baking its texture.
/// </summary>
public sealed class InteriorMapBakerWindow : EditorWindow
{
    [SerializeField] private InteriorMapBaker _framework;
    [SerializeField] private int _faceResolution = 256;
    [SerializeField] private InteriorMapBakeType _bakeType = InteriorMapBakeType.Box;
    [SerializeField] private int _cullingMask = -1;
    [SerializeField] private string _savePath = "Assets/Textures/InteriorMap.asset";
    [SerializeField] private string _loadPath = "Assets/Textures/InteriorMap.asset";

    private Texture2D _bakedTexture;
    private bool _isBaked;
    private Vector2 _scrollPosition;
    private InteriorMapBaker _lastFramework;

    [MenuItem("Tools/Interior Map Baker...")]
    public static void ShowWindow()
    {
        var window = GetWindow<InteriorMapBakerWindow>(false, "Interior Map Baker", true);
        window.minSize = new Vector2(380f, 520f);
        window.Show();
    }

    // ════════════════════════════════════════════════════════════
    //  Window lifecycle — release only textures created by this window
    // ════════════════════════════════════════════════════════════
    private void OnDisable()
    {
        ReleasePreviewTexture();
    }

    // ════════════════════════════════════════════════════════════
    //  Window layout — select a framework and expose bake controls
    // ════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        EditorGUILayout.LabelField("Interior Map Baker", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        _framework = (InteriorMapBaker)EditorGUILayout.ObjectField(
            "Framework",
            _framework,
            typeof(InteriorMapBaker),
            true);

        if (GUILayout.Button("Create Standard Framework", GUILayout.Height(26f)))
        {
            _framework = InteriorMapBakerEditorUtility.CreateStandardFramework();
            Repaint();
        }

        SyncFrameworkSelection();
        DrawFrameworkStatus();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Texture Settings", EditorStyles.boldLabel);
        _faceResolution = EditorGUILayout.IntSlider("Face Resolution", _faceResolution, 16, 1024);
        _bakeType = (InteriorMapBakeType)EditorGUILayout.EnumPopup("Bake Type", _bakeType);
        ApplyBakeTypeToVolume();
        _cullingMask = DrawLayerMaskField("Render Layers", _cullingMask);
        string layoutDescription = _bakeType == InteriorMapBakeType.Hemisphere
            ? $"Output texture uses a square hemisphere disk layout: {_faceResolution} × {_faceResolution}."
            : $"Output texture uses a 2:1 equirectangular layout: {_faceResolution * 2} × {_faceResolution}.";
        EditorGUILayout.HelpBox(layoutDescription, MessageType.None);

        EditorGUILayout.Space(4f);
        DrawPathField("Save Path", ref _savePath);
        DrawPathField("Load Path", ref _loadPath);

        EditorGUILayout.Space(4f);
        DrawActionButtons();
        DrawPreview();

        EditorGUILayout.EndScrollView();
    }

    // ════════════════════════════════════════════════════════════
    //  Framework synchronization — keep the selected Volume shape aligned with Bake Type
    // ════════════════════════════════════════════════════════════
    private void SyncFrameworkSelection()
    {
        if (_framework == _lastFramework)
            return;

        _lastFramework = _framework;
        if (_framework != null && _framework.IsInitialized)
            _bakeType = _framework.Volume.BakeType;
    }

    private void ApplyBakeTypeToVolume()
    {
        if (_framework == null || !_framework.IsInitialized)
            return;
        if (_framework.Volume.BakeType == _bakeType)
            return;

        Undo.RecordObject(_framework.Volume, "Change Interior Map Bake Type");
        _framework.Volume.SetBakeType(_bakeType);
        EditorUtility.SetDirty(_framework.Volume);
        SceneView.RepaintAll();
    }

    // ════════════════════════════════════════════════════════════
    //  Framework status — explain the required scene setup
    // ════════════════════════════════════════════════════════════
    private void DrawFrameworkStatus()
    {
        if (_framework == null)
        {
            EditorGUILayout.HelpBox(
                "Assign an existing framework here, or click Create Standard Framework above.",
                MessageType.Info);
            return;
        }

        if (!_framework.IsInitialized)
        {
            EditorGUILayout.HelpBox(
                "The selected framework is missing a volume, direction, or bake camera. Click Initialize Framework to repair it.",
                MessageType.Warning);
            if (GUILayout.Button("Initialize Framework"))
            {
                InteriorMapBakerEditorUtility.InitializeFramework(_framework);
                Repaint();
            }
            return;
        }

        EditorGUILayout.HelpBox(
            $"Volume: {_framework.Volume.name}    Direction: {_framework.Direction.name}    Camera: {_framework.BakeCamera.name}",
            MessageType.Info);
    }

    // ════════════════════════════════════════════════════════════
    //  Action buttons — bake, load, and save the selected texture
    // ════════════════════════════════════════════════════════════
    private void DrawActionButtons()
    {
        bool canBake = _framework != null && _framework.IsInitialized;

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = canBake;
        if (GUILayout.Button("Bake", GUILayout.Height(30f))) Bake();
        GUI.enabled = true;

        if (GUILayout.Button("Load", GUILayout.Height(30f))) LoadAsset();

        GUI.enabled = _bakedTexture != null;
        if (GUILayout.Button("Save", GUILayout.Height(30f))) SaveAsset();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    // ════════════════════════════════════════════════════════════
    //  Preview — show the generated 2:1 texture in the tool window
    // ════════════════════════════════════════════════════════════
    private void DrawPreview()
    {
        EditorGUILayout.Space(6f);
        if (_bakedTexture == null)
        {
            EditorGUILayout.HelpBox("Bake or load an Interior Map texture to preview.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        float previewAspect = _bakeType == InteriorMapBakeType.Hemisphere ? 1f : 2f;
        Rect previewRect = GUILayoutUtility.GetAspectRect(previewAspect, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawPreviewTexture(previewRect, _bakedTexture, null, ScaleMode.ScaleToFit);
    }

    // ════════════════════════════════════════════════════════════
    //  Baking — invoke the reusable Cubemap-to-equirectangular baker
    // ════════════════════════════════════════════════════════════
    private void Bake()
    {
        ApplyBakeTypeToVolume();
        ReleasePreviewTexture();
        EditorUtility.DisplayProgressBar("Interior Map Baker", "Rendering room panorama...", 0.5f);
        try
        {
            _bakedTexture = InteriorMapTextureBaker.Bake(
                _framework,
                _faceResolution,
                _cullingMask,
                _bakeType);
            _isBaked = _bakedTexture != null;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            _bakedTexture = null;
            _isBaked = false;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Repaint();
    }

    // ════════════════════════════════════════════════════════════
    //  Asset loading — load an existing texture without owning its lifetime
    // ════════════════════════════════════════════════════════════
    private void LoadAsset()
    {
        string path = NormalizeAssetPath(_loadPath);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogWarning($"Interior Map Baker: No texture at \"{path}\"");
            return;
        }

        ReleasePreviewTexture();
        _bakedTexture = texture;
        _isBaked = false;
        Repaint();
    }

    // ════════════════════════════════════════════════════════════
    //  Asset saving — persist a generated texture under the selected path
    // ════════════════════════════════════════════════════════════
    private void SaveAsset()
    {
        if (_bakedTexture == null)
            return;

        string path = NormalizeAssetPath(_savePath);
        if (!path.StartsWith("Assets/", StringComparison.Ordinal))
        {
            Debug.LogWarning("Interior Map Baker: Save path must be inside the Assets folder.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null
            && !EditorUtility.DisplayDialog(
                "Overwrite Texture",
                $"The file already exists. Overwrite it?\n{path}",
                "Overwrite",
                "Cancel"))
        {
            return;
        }

        string directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(directory))
            EnsureFolders(directory);

        Texture2D copy = Instantiate(_bakedTexture);
        copy.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(copy, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ReleasePreviewTexture();
        _bakedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        _isBaked = false;
        _loadPath = path;
        EditorGUIUtility.PingObject(_bakedTexture);
        Debug.Log($"Interior Map saved to {path}");
        Repaint();
    }

    // ════════════════════════════════════════════════════════════
    //  Preview cleanup — destroy only unsaved textures owned by the window
    // ════════════════════════════════════════════════════════════
    private void ReleasePreviewTexture()
    {
        if (_bakedTexture != null && _isBaked)
            DestroyImmediate(_bakedTexture);
        _bakedTexture = null;
        _isBaked = false;
    }

    private static void DrawPathField(string label, ref string path)
    {
        EditorGUILayout.BeginHorizontal();
        path = EditorGUILayout.TextField(label, path);
        if (GUILayout.Button("...", GUILayout.Width(32f)))
        {
            string directory = Path.GetDirectoryName(path) ?? "Assets";
            string fileName = Path.GetFileName(path);
            string chosen = EditorUtility.SaveFilePanelInProject(
                label,
                fileName,
                "asset",
                "",
                directory);
            if (!string.IsNullOrEmpty(chosen))
                path = chosen;
        }
        EditorGUILayout.EndHorizontal();
    }

    private static int DrawLayerMaskField(string label, int mask)
    {
        string[] layerNames = UnityEditorInternal.InternalEditorUtility.layers;
        int compactMask = 0;
        for (int i = 0; i < layerNames.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layerNames[i]);
            if ((mask & (1 << layer)) != 0)
                compactMask |= 1 << i;
        }

        compactMask = EditorGUILayout.MaskField(label, compactMask, layerNames);
        int expandedMask = 0;
        for (int i = 0; i < layerNames.Length; i++)
        {
            if ((compactMask & (1 << i)) != 0)
            {
                int layer = LayerMask.NameToLayer(layerNames[i]);
                expandedMask |= 1 << layer;
            }
        }
        return expandedMask;
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }

    private static void EnsureFolders(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
                continue;

            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
