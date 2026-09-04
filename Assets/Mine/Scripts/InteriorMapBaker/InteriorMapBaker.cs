using UnityEngine;

/// <summary>
/// Selects the texture projection produced by the Interior Map Baker.
/// </summary>
public enum InteriorMapBakeType
{
    Box = 0,
    Hemisphere = 1
}

/// <summary>
/// Owns the scene references used by the Interior Map baking workflow.
/// The custom Inspector creates or reuses the range, direction, and bake camera child objects.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class InteriorMapBaker : MonoBehaviour
{
    [Header("Generated Components")]
    [SerializeField, HideInInspector] private InteriorMapBakeVolume _volume;
    [SerializeField, HideInInspector] private InteriorMapBakeDirection _direction;
    [SerializeField, HideInInspector] private Camera _camera;

    public InteriorMapBakeVolume Volume => _volume;
    public InteriorMapBakeDirection Direction => _direction;
    public Camera BakeCamera => _camera;

    public bool IsInitialized => _volume != null && _direction != null && _camera != null;

    // ════════════════════════════════════════════════════════════
    //  Camera synchronization — keep the owned bake camera aligned with the direction marker
    // ════════════════════════════════════════════════════════════
    public void SyncBakeCamera()
    {
        if (_camera == null || _direction == null)
            return;

        Vector3 forward = _direction.Direction.normalized;
        Vector3 up = _direction.transform.up;
        if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f)
            up = _direction.transform.right;

        _camera.transform.SetPositionAndRotation(
            _direction.Origin,
            Quaternion.LookRotation(forward, up));
        _camera.cameraType = CameraType.Reflection;
        _camera.enabled = false;
    }
}
