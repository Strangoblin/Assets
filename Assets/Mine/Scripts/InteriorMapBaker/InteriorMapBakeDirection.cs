using UnityEngine;

/// <summary>
/// Defines the forward direction used by an InteriorMap baker and visualizes it as a Scene view arrow.
/// The component transform supplies the orientation; the local offset supplies the arrow origin.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class InteriorMapBakeDirection : MonoBehaviour
{
    [Header("Direction")]
    [SerializeField] private Vector3 _originOffset = Vector3.zero;
    [SerializeField] private float _length = 1.5f;

    [Header("Arrow")]
    [SerializeField] private float _headLength = 0.3f;
    [SerializeField] private float _headWidth = 0.15f;

    [Header("Gizmos")]
    [SerializeField] private bool _drawGizmo = true;
    [SerializeField] private Color _gizmoColor = new Color(1f, 0.75f, 0.15f, 0.95f);

    public Vector3 Origin => transform.TransformPoint(_originOffset);
    public Vector3 Direction => transform.forward;
    public Vector3 EndPoint => Origin + Direction * _length;
    public float Length => _length;

    // ════════════════════════════════════════════════════════════
    //  Validation — keep the direction marker readable
    // ════════════════════════════════════════════════════════════
    private void OnValidate()
    {
        _length = Mathf.Max(_length, 0.001f);
        _headLength = Mathf.Clamp(_headLength, 0.001f, _length);
        _headWidth = Mathf.Max(_headWidth, 0.001f);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos — draw the bake view direction in the Scene view
    // ════════════════════════════════════════════════════════════
    private void OnDrawGizmos()
    {
        if (!_drawGizmo) return;

        Vector3 origin = Origin;
        Vector3 direction = Direction.normalized;
        Vector3 endPoint = origin + direction * _length;
        Vector3 up = transform.up;
        if (Mathf.Abs(Vector3.Dot(direction, up)) > 0.98f)
            up = transform.right;

        Vector3 side = Vector3.Cross(direction, up).normalized;
        Vector3 headBase = endPoint - direction * _headLength;
        Vector3 sideLeft = headBase + side * _headWidth;
        Vector3 sideRight = headBase - side * _headWidth;
        Vector3 upLeft = headBase + up * _headWidth;
        Vector3 upRight = headBase - up * _headWidth;

        Color previousColor = Gizmos.color;
        Gizmos.color = _gizmoColor;
        Gizmos.DrawLine(origin, endPoint);
        Gizmos.DrawLine(endPoint, sideLeft);
        Gizmos.DrawLine(endPoint, sideRight);
        Gizmos.DrawLine(endPoint, upLeft);
        Gizmos.DrawLine(endPoint, upRight);
        Gizmos.DrawWireSphere(origin, _headWidth * 0.35f);
        Gizmos.color = previousColor;
    }
}
