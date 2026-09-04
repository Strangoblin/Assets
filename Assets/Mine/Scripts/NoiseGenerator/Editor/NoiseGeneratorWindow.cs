#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window for generating procedural noise textures through <see cref="NoiseGenerator"/>.
/// Thin GUI shell: per-mode settings, hash-cached texture previews and asset save.
/// </summary>
public sealed class NoiseGeneratorWindow : EditorWindow
{
    private enum OutputMode
    {
        Texture3D,
        Texture2D
    }

    [System.Serializable]
    private class Texture3DSettings
    {
        public int size = 32;
        public float scale = 4f;
        public bool seamless = true;
        public NoiseGenerator.NoiseType noiseType = NoiseGenerator.NoiseType.Perlin;
    }

    [System.Serializable]
    private class ChannelSettings
    {
        public int resolution = 32;
        public float scale = 4f;
        public bool seamless = true;
        public NoiseGenerator.NoiseType noiseType = NoiseGenerator.NoiseType.Perlin;
        public float randomSeed = 0f;
        public int previewHash;
        [System.NonSerialized] public Texture2D previewTexture;   // runtime cache, rebuilt on hash mismatch
    }

    // ── Serialized state ──────────────────────────────────────
    [SerializeField] private OutputMode _outputMode;
    [SerializeField] private Texture3DSettings _texture3DSettings = new Texture3DSettings();
    [SerializeField] private ChannelSettings[] _channelSettings = new ChannelSettings[4];
    [SerializeField] private int _activeChannelCount = 4;
    [SerializeField] private int[] _previewSlices3D = new int[3];
    [SerializeField] private int _selectedChannel2D;
    [SerializeField] private int _selectedAxis3D;
    [SerializeField] private Vector2 _scrollPos;
    [SerializeField] private string _outputPath3D = "Assets/Mine/Noises/Noise3D.asset";
    [SerializeField] private string _outputPath2D = "Assets/Mine/Noises/Noise2D.asset";

    // ── Runtime textures (released in OnDisable) ──────────────
    private Texture3D _noiseTexture3D;
    private Texture2D _packedTexture2D;
    private Texture2D[] _sliceTextures3D = new Texture2D[3];
    private int[] _sliceHashes3D = new int[3];
    private int _packedHash;

    private static readonly string[] AxisLabels = { "X", "Y", "Z" };
    private static readonly string[] ChannelLabels = { "R", "G", "B", "A" };

    [MenuItem("Tools/Noise Generator...")]
    public static void ShowWindow()
    {
        var window = GetWindow<NoiseGeneratorWindow>(false, "Noise Generator", true);
        window.minSize = new Vector2(440, 520);
        window.Show();
    }

    // ═══════════════════════════════════════════════════════════
    //  Window lifecycle
    // ═══════════════════════════════════════════════════════════

    private void OnEnable()
    {
        EnsureChannelSettings();
        EnsureSliceState();
    }

    private void OnDisable()
    {
        ReleaseTexture(ref _noiseTexture3D);
        ReleaseTexture(ref _packedTexture2D);
        ReleaseTexture(ref _sliceTextures3D);
        ReleaseChannelPreviews();
    }

    // ═══════════════════════════════════════════════════════════
    //  Window layout
    // ═══════════════════════════════════════════════════════════

    private void OnGUI()
    {
        EnsureChannelSettings();
        EnsureSliceState();

        EditorGUILayout.LabelField("Noise Generator", EditorStyles.boldLabel);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        _outputMode = (OutputMode)GUILayout.Toolbar((int)_outputMode, new[] { "Texture3D", "Texture2D" });

        EditorGUILayout.Space(6);
        if (_outputMode == OutputMode.Texture3D)
        {
            DrawTexture3DSettings();
        }
        else
        {
            DrawTexture2DSettings();
        }

        EditorGUILayout.Space(8);
        DrawOutputSection();

        EditorGUILayout.Space(8);
        DrawPreviewSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawTexture3DSettings()
    {
        EditorGUILayout.LabelField("Texture3D Settings", EditorStyles.boldLabel);
        _texture3DSettings.size = EditorGUILayout.IntSlider("Size", Mathf.Clamp(_texture3DSettings.size, 4, 256), 4, 256);
        _texture3DSettings.scale = EditorGUILayout.FloatField("Scale", Mathf.Max(0.0001f, _texture3DSettings.scale));
        _texture3DSettings.seamless = EditorGUILayout.Toggle("Seamless", _texture3DSettings.seamless);
        _texture3DSettings.noiseType = (NoiseGenerator.NoiseType)EditorGUILayout.EnumPopup("Noise Type", _texture3DSettings.noiseType);
    }

    private void DrawTexture2DSettings()
    {
        EditorGUILayout.LabelField("Texture2D Settings", EditorStyles.boldLabel);
        _activeChannelCount = EditorGUILayout.IntSlider("Channels", Mathf.Clamp(_activeChannelCount, 1, 4), 1, 4);
        EditorGUILayout.HelpBox("Final packed output uses the max resolution of active channels.", MessageType.Info);

        _selectedChannel2D = DrawChannelSelector(_selectedChannel2D, _activeChannelCount);
        DrawChannelSettings(_selectedChannel2D);
    }

    private void DrawChannelSettings(int index)
    {
        ChannelSettings settings = _channelSettings[index];

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Channel {ChannelLabels[index]}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Randomize", GUILayout.Width(88)))
        {
            settings.randomSeed = Random.value * 100000f;
        }
        EditorGUILayout.EndHorizontal();

        settings.resolution = EditorGUILayout.IntSlider("Resolution", Mathf.Clamp(settings.resolution, 8, 2048), 8, 2048);
        settings.scale = EditorGUILayout.FloatField("Scale", Mathf.Max(0.0001f, settings.scale));
        settings.seamless = EditorGUILayout.Toggle("Seamless", settings.seamless);
        settings.noiseType = (NoiseGenerator.NoiseType)EditorGUILayout.EnumPopup("Noise Type", settings.noiseType);
        settings.randomSeed = EditorGUILayout.FloatField("Random Seed", settings.randomSeed);

        UpdateChannelPreview(index);
        DrawTexturePreview(settings.previewTexture);

        EditorGUILayout.EndVertical();
    }

    private int DrawChannelSelector(int currentIndex, int channelCount)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Channel", GUILayout.Width(52));

        int selected = currentIndex;
        for (int i = 0; i < 4; i++)
        {
            using (new EditorGUI.DisabledScope(i >= channelCount))
            {
                bool isSelected = selected == i;
                if (GUILayout.Toggle(isSelected, ChannelLabels[i], "Button", GUILayout.Height(22)))
                {
                    selected = i;
                }
            }
        }

        EditorGUILayout.EndHorizontal();
        return Mathf.Clamp(selected, 0, Mathf.Max(0, channelCount - 1));
    }

    private void DrawPreviewSection()
    {
        if (_outputMode == OutputMode.Texture3D)
        {
            DrawTexture3DPreviewSection();
        }
        else
        {
            DrawTexture2DPreviewSection();
        }
    }

    private void DrawTexture3DPreviewSection()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        _selectedAxis3D = GUILayout.Toolbar(_selectedAxis3D, AxisLabels);
        EditorGUILayout.Space(6);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"Axis {AxisLabels[_selectedAxis3D]} Preview", EditorStyles.boldLabel);

        int maxSlice = Mathf.Max(0, _texture3DSettings.size - 1);
        _previewSlices3D[_selectedAxis3D] = EditorGUILayout.IntSlider(
            "Slice", Mathf.Clamp(_previewSlices3D[_selectedAxis3D], 0, maxSlice), 0, maxSlice);

        UpdateSlicePreview3D(_selectedAxis3D);
        DrawTexturePreview(_sliceTextures3D[_selectedAxis3D]);

        EditorGUILayout.EndVertical();
    }

    private void DrawTexture2DPreviewSection()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Packed RGBA Preview", EditorStyles.boldLabel);
        UpdatePackedPreviewTexture();
        DrawTexturePreview(_packedTexture2D);
        EditorGUILayout.EndVertical();
    }

    private void DrawTexturePreview(Texture2D texture)
    {
        if (texture == null)
        {
            EditorGUILayout.HelpBox("Preview is not ready.", MessageType.None);
            return;
        }

        float width = EditorGUIUtility.currentViewWidth - 40f;
        float height = width;
        Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Output section
    // ═══════════════════════════════════════════════════════════

    private void DrawOutputSection()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        string currentPath = GetCurrentOutputPath();
        currentPath = EditorGUILayout.TextField("Asset Path", currentPath);
        SetCurrentOutputPath(currentPath);

        if (GUILayout.Button("Browse...", GUILayout.MaxWidth(90)))
        {
            string directory = "Assets";
            string filename = _outputMode == OutputMode.Texture3D ? "Noise3D.asset" : "Noise2D.asset";
            string activePath = GetCurrentOutputPath();
            if (!string.IsNullOrEmpty(activePath))
            {
                directory = Path.GetDirectoryName(activePath)?.Replace('\\', '/') ?? "Assets";
                string fileName = Path.GetFileName(activePath);
                if (!string.IsNullOrEmpty(fileName)) filename = fileName;
            }

            bool is3D = _outputMode == OutputMode.Texture3D;
            string newPath = EditorUtility.SaveFilePanelInProject(
                is3D ? "Save Texture3D" : "Save Texture2D",
                filename, "asset",
                is3D ? "Choose where to save the Texture3D asset." : "Choose where to save the Texture2D asset.",
                directory);

            if (!string.IsNullOrEmpty(newPath))
            {
                SetCurrentOutputPath(newPath);
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(_outputMode == OutputMode.Texture3D ? "Generate Texture3D" : "Generate Texture2D", GUILayout.Height(28)))
        {
            if (_outputMode == OutputMode.Texture3D)
            {
                Generate3DNoise();
            }
            else
            {
                Generate2DNoise();
            }
        }

        using (new EditorGUI.DisabledScope(!HasCurrentTexture()))
        {
            if (GUILayout.Button(_outputMode == OutputMode.Texture3D ? "Save Texture3D" : "Save Texture2D", GUILayout.Height(28)))
            {
                SaveCurrentNoise();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    // ═══════════════════════════════════════════════════════════
    //  Cache updates
    // ═══════════════════════════════════════════════════════════

    private void UpdateSlicePreview3D(int axis)
    {
        int size = Mathf.Clamp(_texture3DSettings.size, 4, 256);
        int maxSlice = Mathf.Max(0, size - 1);
        _previewSlices3D[axis] = Mathf.Clamp(_previewSlices3D[axis], 0, maxSlice);

        int hash = GetSlicePreviewHash(axis, size);
        if (_sliceTextures3D[axis] != null && _sliceTextures3D[axis].width == size
            && _sliceHashes3D[axis] == hash)
        {
            return;
        }

        ReleaseTexture(ref _sliceTextures3D[axis]);
        _sliceTextures3D[axis] = new Texture2D(size, size, TextureFormat.R8, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        Color[] colors = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float value = Sample3DSlice(axis, x, y, _previewSlices3D[axis], size);
                colors[x + y * size] = new Color(value, value, value, 1f);
            }
        }

        _sliceTextures3D[axis].SetPixels(colors);
        _sliceTextures3D[axis].Apply(false, false);
        _sliceHashes3D[axis] = hash;
    }

    private void UpdateChannelPreview(int index)
    {
        ChannelSettings settings = _channelSettings[index];
        int resolution = Mathf.Clamp(settings.resolution, 8, 2048);
        int hash = GetChannelPreviewHash(settings, resolution);

        if (settings.previewTexture != null && settings.previewTexture.width == resolution
            && settings.previewTexture.height == resolution && settings.previewHash == hash)
        {
            return;
        }

        ReleaseTexture(ref settings.previewTexture);
        settings.previewTexture = NoiseGenerator.GenerateChannelTexture(
            resolution, settings.scale, settings.seamless, settings.noiseType, settings.randomSeed);
        settings.previewHash = hash;
    }

    private void UpdatePackedPreviewTexture()
    {
        int activeCount = Mathf.Clamp(_activeChannelCount, 1, 4);
        int outputResolution = GetPackedResolution(activeCount);
        int hash = GetPackedPreviewHash(activeCount, outputResolution);

        if (_packedTexture2D != null && _packedTexture2D.width == outputResolution
            && _packedHash == hash)
        {
            return;
        }

        ReleaseTexture(ref _packedTexture2D);
        var sources = new Texture2D[activeCount];
        for (int i = 0; i < activeCount; i++)
        {
            sources[i] = _channelSettings[i].previewTexture;
        }

        _packedTexture2D = NoiseGenerator.PackChannels(sources, outputResolution);
        _packedHash = hash;
    }

    private float Sample3DSlice(int axis, int x, int y, int slice, int size)
    {
        int xi = 0;
        int yi = 0;
        int zi = 0;

        switch (axis)
        {
            case 0:
                xi = slice;
                yi = y;
                zi = x;
                break;
            case 1:
                xi = x;
                yi = slice;
                zi = y;
                break;
            default:
                xi = x;
                yi = y;
                zi = slice;
                break;
        }

        float wx = (float)xi / size;
        float wy = (float)yi / size;
        float wz = (float)zi / size;
        return NoiseGenerator.Sample3D(wx, wy, wz, _texture3DSettings.scale,
            _texture3DSettings.seamless, _texture3DSettings.noiseType);
    }

    private int GetPackedResolution(int activeCount)
    {
        int outputResolution = 8;
        for (int i = 0; i < activeCount; i++)
        {
            outputResolution = Mathf.Max(outputResolution, Mathf.Clamp(_channelSettings[i].resolution, 8, 2048));
        }
        return outputResolution;
    }

    // ═══════════════════════════════════════════════════════════
    //  Preview hashes
    // ═══════════════════════════════════════════════════════════

    private int GetSlicePreviewHash(int axis, int size)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + axis;
            hash = hash * 31 + size;
            hash = hash * 31 + _texture3DSettings.scale.GetHashCode();
            hash = hash * 31 + _texture3DSettings.seamless.GetHashCode();
            hash = hash * 31 + _texture3DSettings.noiseType.GetHashCode();
            hash = hash * 31 + _previewSlices3D[axis];
            return hash;
        }
    }

    private int GetChannelPreviewHash(ChannelSettings settings, int resolution)
    {
        unchecked
        {
            int hash = 23;
            hash = hash * 31 + resolution;
            hash = hash * 31 + settings.scale.GetHashCode();
            hash = hash * 31 + settings.seamless.GetHashCode();
            hash = hash * 31 + settings.noiseType.GetHashCode();
            hash = hash * 31 + settings.randomSeed.GetHashCode();
            return hash;
        }
    }

    private int GetPackedPreviewHash(int activeCount, int resolution)
    {
        unchecked
        {
            int hash = 29;
            hash = hash * 31 + activeCount;
            hash = hash * 31 + resolution;
            for (int i = 0; i < activeCount; i++)
            {
                hash = hash * 31 + _channelSettings[i].previewHash;
            }
            return hash;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Actions
    // ═══════════════════════════════════════════════════════════

    private void Generate3DNoise()
    {
        ReleaseTexture(ref _noiseTexture3D);
        _noiseTexture3D = NoiseGenerator.Generate3DTexture(
            Mathf.Clamp(_texture3DSettings.size, 4, 256),
            _texture3DSettings.scale,
            _texture3DSettings.seamless,
            _texture3DSettings.noiseType);

        Repaint();
        Debug.Log($"3D {_texture3DSettings.noiseType} generated. Size: {_texture3DSettings.size}");
    }

    private void Generate2DNoise()
    {
        EnsureChannelSettings();

        int activeCount = Mathf.Clamp(_activeChannelCount, 1, 4);
        for (int i = 0; i < activeCount; i++)
        {
            UpdateChannelPreview(i);
        }

        UpdatePackedPreviewTexture();
        Repaint();
        Debug.Log($"2D packed noise generated. Channels: {activeCount}, " +
                  $"Resolution: {_packedTexture2D.width}x{_packedTexture2D.height}");
    }

    private bool HasCurrentTexture()
    {
        return _outputMode == OutputMode.Texture3D ? _noiseTexture3D != null : _packedTexture2D != null;
    }

    // ═══════════════════════════════════════════════════════════
    //  Save
    // ═══════════════════════════════════════════════════════════

    private string GetCurrentOutputPath()
    {
        return _outputMode == OutputMode.Texture3D ? _outputPath3D : _outputPath2D;
    }

    private void SetCurrentOutputPath(string path)
    {
        if (_outputMode == OutputMode.Texture3D)
        {
            _outputPath3D = path;
        }
        else
        {
            _outputPath2D = path;
        }
    }

    private void SaveCurrentNoise()
    {
        if (_outputMode == OutputMode.Texture3D)
        {
            SaveTexture3D();
        }
        else
        {
            SaveTexture2D();
        }
    }

    private void SaveTexture3D()
    {
        if (_noiseTexture3D == null)
        {
            Debug.LogWarning("Noise texture is null. Generate first.");
            return;
        }
        SaveTextureAsset(_outputPath3D, _noiseTexture3D, "3D Noise");
    }

    private void SaveTexture2D()
    {
        if (_packedTexture2D == null)
        {
            Debug.LogWarning("Noise texture is null. Generate first.");
            return;
        }
        SaveTextureAsset(_outputPath2D, _packedTexture2D, "2D Noise");
    }

    private void SaveTextureAsset(string path, Object texture, string label)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("Output path is empty. Set a valid project-relative path like 'Assets/Mine/Noises/Noise3D.asset'.");
            return;
        }

        path = path.Replace('\\', '/');
        string dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(dir) || !path.StartsWith("Assets"))
        {
            Debug.LogError("Output path must be inside project, e.g., 'Assets/Mine/Noises/Noise3D.asset'.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
        {
            bool overwrite = EditorUtility.DisplayDialog("Overwrite Asset",
                $"The file already exists. Overwrite it?\n{path}", "Overwrite", "Cancel");
            if (!overwrite) return;
        }

        EnsureFolders(dir);

        var assetCopy = Object.Instantiate(texture);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(assetCopy, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(assetCopy);

        Debug.Log($"{label} saved to: {path}");
    }

    // ═══════════════════════════════════════════════════════════
    //  Utility
    // ═══════════════════════════════════════════════════════════

    private void EnsureChannelSettings()
    {
        if (_channelSettings == null || _channelSettings.Length != 4)
        {
            _channelSettings = new ChannelSettings[4];
        }

        for (int i = 0; i < _channelSettings.Length; i++)
        {
            if (_channelSettings[i] == null)
            {
                _channelSettings[i] = new ChannelSettings();
            }
        }

        _activeChannelCount = Mathf.Clamp(_activeChannelCount, 1, 4);
    }

    private void EnsureSliceState()
    {
        if (_previewSlices3D == null || _previewSlices3D.Length != 3)
        {
            _previewSlices3D = new int[3];
        }
        if (_sliceTextures3D == null || _sliceTextures3D.Length != 3)
        {
            _sliceTextures3D = new Texture2D[3];
        }
        if (_sliceHashes3D == null || _sliceHashes3D.Length != 3)
        {
            _sliceHashes3D = new int[3];
        }

        _selectedChannel2D = Mathf.Clamp(_selectedChannel2D, 0, 3);
        _selectedAxis3D = Mathf.Clamp(_selectedAxis3D, 0, 2);
    }

    private void ReleaseChannelPreviews()
    {
        if (_channelSettings == null) return;
        for (int i = 0; i < _channelSettings.Length; i++)
        {
            if (_channelSettings[i] != null)
            {
                ReleaseTexture(ref _channelSettings[i].previewTexture);
            }
        }
    }

    private static void ReleaseTexture(ref Texture2D texture)
    {
        if (texture != null)
        {
            DestroyImmediate(texture);
            texture = null;
        }
    }

    private static void ReleaseTexture(ref Texture3D texture)
    {
        if (texture != null)
        {
            DestroyImmediate(texture);
            texture = null;
        }
    }

    private static void ReleaseTexture(ref Texture2D[] textures)
    {
        if (textures == null) return;
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] != null)
            {
                DestroyImmediate(textures[i]);
                textures[i] = null;
            }
        }
    }

    private static void EnsureFolders(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
        {
            return;
        }

        fullPath = fullPath.Replace('\\', '/');
        if (!fullPath.StartsWith("Assets"))
        {
            return;
        }

        string[] parts = fullPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = parts[i];
            if (string.IsNullOrEmpty(next))
            {
                continue;
            }

            string combined = current + "/" + next;
            if (!AssetDatabase.IsValidFolder(combined))
            {
                AssetDatabase.CreateFolder(current, next);
            }

            current = combined;
        }
    }
}
#endif
