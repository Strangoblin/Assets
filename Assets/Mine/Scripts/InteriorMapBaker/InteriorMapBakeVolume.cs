using UnityEngine;

/// <summary>
/// Defines the local-space volume that an InteriorMap baker should use as its room range.
/// The transform controls the volume pose; the serialized center and size describe its shape.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class InteriorMapBakeVolume : MonoBehaviour
{
    private const int HemisphereSegmentCount = 24;
    private const int HemisphereRingCount = 6;

    [Header("Range")]
    [SerializeField] private Vector3 _center = Vector3.zero;
    [SerializeField] private Vector3 _size = new Vector3(2f, 2f, 3f);
    [SerializeField] private InteriorMapBakeType _bakeType = InteriorMapBakeType.Box;

    [Header("Gizmos")]
    [SerializeField] private bool _drawGizmo = true;
    [SerializeField] private Color _gizmoColor = new Color(0.2f, 0.8f, 1f, 0.85f);

    public Vector3 Center => transform.TransformPoint(_center);
    public Vector3 LocalCenter => _center;
    public Vector3 Size => _size;
    public InteriorMapBakeType BakeType => _bakeType;
    public Matrix4x4 LocalToWorldMatrix => transform.localToWorldMatrix;

    /// <summary>
    /// Updates the visualization mode used by the selected baker workflow.
    /// </summary>
    public void SetBakeType(InteriorMapBakeType bakeType)
    {
        _bakeType = bakeType;
    }

    // ════════════════════════════════════════════════════════════
    //  Validation — keep the editable bake range valid
    // ════════════════════════════════════════════════════════════
    private void OnValidate()
    {
        _size.x = Mathf.Max(_size.x, 0.001f);
        _size.y = Mathf.Max(_size.y, 0.001f);
        _size.z = Mathf.Max(_size.z, 0.001f);
        _bakeType = (InteriorMapBakeType)Mathf.Clamp(
            (int)_bakeType,
            (int)InteriorMapBakeType.Box,
            (int)InteriorMapBakeType.Hemisphere);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos — draw the selected Box or rear Hemisphere bake range
    // ════════════════════════════════════════════════════════════
    private void OnDrawGizmos()
    {
        if (!_drawGizmo) return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = _gizmoColor;

        if (_bakeType == InteriorMapBakeType.Hemisphere)
            DrawHemisphereGizmo();
        else
            Gizmos.DrawWireCube(_center, _size);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private void DrawHemisphereGizmo()
    {
        float radius = Mathf.Max(_size.x, Mathf.Max(_size.y, _size.z)) * 0.5f;
        Vector3 pole = _center + Vector3.back * radius;
        float halfPi = Mathf.PI * 0.5f;

        for (int ringIndex = 1; ringIndex <= HemisphereRingCount; ringIndex++)
        {
            float angle = ringIndex / (float)HemisphereRingCount * halfPi;
            float ringRadius = Mathf.Sin(angle) * radius;
            float ringZ = -Mathf.Cos(angle) * radius;
            DrawCircle(_center + new Vector3(0f, 0f, ringZ), ringRadius);
        }

        for (int segmentIndex = 0; segmentIndex < HemisphereSegmentCount; segmentIndex++)
        {
            float angle = segmentIndex / (float)HemisphereSegmentCount * Mathf.PI * 2f;
            Vector3 equatorPoint = _center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f);
            Gizmos.DrawLine(pole, equatorPoint);
        }
    }

    private void DrawCircle(Vector3 center, float radius)
    {
        Vector3 previous = center + Vector3.right * radius;
        for (int segmentIndex = 1; segmentIndex <= HemisphereSegmentCount; segmentIndex++)
        {
            float angle = segmentIndex / (float)HemisphereSegmentCount * Mathf.PI * 2f;
            Vector3 current = center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }
}
