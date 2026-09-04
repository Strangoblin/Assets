using UnityEngine;

/// <summary>
/// Catmull-Rom spline: passes through all control points, C1 continuous.
/// Bake is a thin adapter over the shared <see cref="CurveBake.SampleUniform"/> skeleton;
/// only the segment math (index derivation + Position/Tangent) lives here.
/// </summary>
public static class CatmullRomCurve
{
    public static CurveBake.Result Bake(Vector3[] pts, int samples, bool closed)
        => CurveBake.SampleUniform(pts, samples, closed, SampleSegment);

    // ── Math ────────────────────────────────────────────────

    public static Vector3 Position(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (
            (2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    public static Vector3 Tangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        return (0.5f * (
            (-p0 + p2)
            + (4f * p0 - 10f * p1 + 8f * p2 - 2f * p3) * t
            + (-3f * p0 + 9f * p1 - 9f * p2 + 3f * p3) * t2)).normalized;
    }

    // ── Segment sampler (consumed by the shared skeleton) ───

    private static void SampleSegment(Vector3[] pts, int seg, bool closed, float t,
                                      out Vector3 position, out Vector3 tangent)
    {
        int p0, p1, p2, p3;
        if (closed)
        {
            p0 = (seg - 1 + pts.Length) % pts.Length;
            p1 = seg;
            p2 = (seg + 1) % pts.Length;
            p3 = (seg + 2) % pts.Length;
        }
        else
        {
            p0 = Mathf.Max(seg - 1, 0);
            p1 = seg;
            p2 = seg + 1;
            p3 = Mathf.Min(seg + 2, pts.Length - 1);
        }

        position = Position(pts[p0], pts[p1], pts[p2], pts[p3], t);
        tangent  = Tangent(pts[p0], pts[p1], pts[p2], pts[p3], t);
    }
}
