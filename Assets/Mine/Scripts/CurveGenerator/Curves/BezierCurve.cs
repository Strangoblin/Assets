using UnityEngine;

/// <summary>
/// Cubic Bezier spline with auto-computed handles (Catmull-Rom 1/6 factor).
/// Passes through all control points using central-difference tangent estimation.
/// Bake is a thin adapter over the shared <see cref="CurveBake.SampleUniform"/> skeleton;
/// only the segment math (auto-handle derivation + Position/Tangent) lives here.
/// </summary>
public static class BezierCurve
{
    public static CurveBake.Result Bake(Vector3[] pts, int samples, bool closed)
        => CurveBake.SampleUniform(pts, samples, closed, SampleSegment);

    // ── Math ────────────────────────────────────────────────

    public static Vector3 Position(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        float uu = u * u, uuu = uu * u;
        float tt = t * t, ttt = tt * t;
        return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
    }

    public static Vector3 Tangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return (3f * u * u * (p1 - p0)
              + 6f * u * t * (p2 - p1)
              + 3f * t * t * (p3 - p2)).normalized;
    }

    // ── Auto-handle helpers ─────────────────────────────────

    private static Vector3 TangentAt(Vector3[] pts, int i, bool closed)
    {
        if (closed)
        {
            Vector3 prev = pts[(i - 1 + pts.Length) % pts.Length];
            Vector3 next = pts[(i + 1) % pts.Length];
            return next - prev;
        }
        if (i <= 0)              return pts[i + 1] - pts[i];
        if (i >= pts.Length - 1) return pts[i] - pts[i - 1];
        return pts[i + 1] - pts[i - 1];
    }

    // ── Segment sampler (consumed by the shared skeleton) ───

    private static void SampleSegment(Vector3[] pts, int seg, bool closed, float t,
                                      out Vector3 position, out Vector3 tangent)
    {
        int i0 = seg;
        int i1 = closed ? (seg + 1) % pts.Length : seg + 1;

        // Auto-handles from central-difference tangents
        Vector3 B0 = pts[i0];
        Vector3 B3 = pts[i1];
        Vector3 t0 = TangentAt(pts, i0, closed);
        Vector3 t1 = TangentAt(pts, i1, closed);
        Vector3 B1 = B0 + t0 / 6f;
        Vector3 B2 = B3 - t1 / 6f;

        position = Position(B0, B1, B2, B3, t);
        tangent  = Tangent(B0, B1, B2, B3, t);
    }
}
