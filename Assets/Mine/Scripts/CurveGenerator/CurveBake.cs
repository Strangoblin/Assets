using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Curve baking dispatcher. Delegates to individual curve type implementations
/// in <see cref="CatmullRomCurve"/> / <see cref="BezierCurve"/>.
/// To add a new curve type: create a new file in Scripts/CurveGenerator/Curves/ with a
/// <c>public static CurveBake.Result Bake(Vector3[], int, bool)</c> method,
/// then register it in the switch below.
/// </summary>
public static class CurveBake
{
    public struct Result
    {
        public Vector3[] positions;
        public Vector3[] tangents;
        public float[]   arcLengths;
        public Vector3[] normals;       // curvature normal (points toward bend center)
        public float[]   curvatures;     // scalar curvature κ = 1/radius
        public float     totalLength;
        public int       sampleCount => positions?.Length ?? 0;
    }

    /// <summary>Per-segment evaluator used by <see cref="SampleUniform"/>. Curve types fill in their own Position/Tangent math.</summary>
    public delegate void SegmentSampler(Vector3[] pts, int segmentIndex, bool closed,
                                        float t, out Vector3 position, out Vector3 tangent);

    // ── Public ──────────────────────────────────────────────

    /// <summary>Bake control points into uniformly sampled curve data.</summary>
    public static Result Bake(Vector3[] controlPoints, CurveAsset.CurveType type,
                              CurveAsset.CurveDimension dimension, int sampleCount, bool loop)
    {
        Vector3[] pts = ProjectPositions(controlPoints, dimension);
        return type switch
        {
            CurveAsset.CurveType.CatmullRom => CatmullRomCurve.Bake(pts, sampleCount, loop),
            CurveAsset.CurveType.Bezier     => BezierCurve.Bake(pts, sampleCount, loop),
            _ => default,
        };
    }

    /// <summary>Project 3D positions onto a 2D plane (average depth).</summary>
    public static Vector3[] ProjectPositions(Vector3[] pts, CurveAsset.CurveDimension dim)
    {
        if (pts == null || pts.Length == 0) return pts;
        if (dim == CurveAsset.CurveDimension.XYZ) return (Vector3[])pts.Clone();

        float avgX = 0, avgY = 0, avgZ = 0;
        foreach (var p in pts) { avgX += p.x; avgY += p.y; avgZ += p.z; }
        avgX /= pts.Length; avgY /= pts.Length; avgZ /= pts.Length;

        var projected = new Vector3[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            projected[i] = dim switch
            {
                CurveAsset.CurveDimension.XY => new Vector3(pts[i].x, pts[i].y, avgZ),
                CurveAsset.CurveDimension.XZ => new Vector3(pts[i].x, avgY, pts[i].z),
                CurveAsset.CurveDimension.YZ => new Vector3(avgX, pts[i].y, pts[i].z),
                _ => pts[i],
            };
        }
        return projected;
    }

    /// <summary>
    /// Shared uniform-sampling skeleton: distributes the requested sample count across
    /// segments (remainder spread evenly, at least one sample per segment), accumulates
    /// arc length, appends the open-curve end point, then computes normals and curvatures.
    /// Keeps sampling behavior identical for every curve type.
    /// </summary>
    public static Result SampleUniform(Vector3[] pts, int samples, bool closed, SegmentSampler sampleSegment)
    {
        if (pts == null || pts.Length < 2) return default;

        int segCount = closed ? pts.Length : pts.Length - 1;
        if (segCount < 1) return default;

        int baseCount = samples / segCount;
        int extra     = samples % segCount;

        var positions  = new List<Vector3>();
        var tangents   = new List<Vector3>();
        var arcLengths = new List<float>();
        var normals    = new List<Vector3>();
        var curvatures = new List<float>();

        float totalLength = 0f;
        Vector3 lastPos = Vector3.zero;
        bool first = true;

        for (int seg = 0; seg < segCount; seg++)
        {
            int count = baseCount + (seg < extra ? 1 : 0);
            if (count < 1) count = 1;                 // guarantee ≥1 sample per segment

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                sampleSegment(pts, seg, closed, t, out Vector3 pos, out Vector3 tang);

                if (!first) totalLength += Vector3.Distance(lastPos, pos);
                lastPos = pos;
                first = false;

                positions.Add(pos);
                tangents.Add(tang);
                arcLengths.Add(totalLength);
            }
        }

        // Final point (open curves only)
        if (!closed)
        {
            Vector3 last = pts[pts.Length - 1];
            positions.Add(last);
            tangents.Add(tangents.Count > 0 ? tangents[tangents.Count - 1] : Vector3.forward);
            if (positions.Count > 1)
                totalLength += Vector3.Distance(positions[positions.Count - 2], last);
            arcLengths.Add(totalLength);
        }

        // ── Curvature computation ─────────────────────────────
        int n = positions.Count;
        for (int i = 0; i < n; i++)
        {
            if (n < 3 || totalLength < 0.001f)
            {
                normals.Add(Vector3.right);
                curvatures.Add(0f);
                continue;
            }

            int i0, i1;
            if (closed)
            { i0 = (i - 1 + n) % n; i1 = (i + 1) % n; }
            else
            { i0 = Mathf.Max(0, i - 1); i1 = Mathf.Min(n - 1, i + 1); }

            Vector3 dT = tangents[i1] - tangents[i0];
            float ds = arcLengths[i1] - arcLengths[i0];
            float rawK = dT.magnitude / Mathf.Max(ds, 0.0001f);
            float curveK = Mathf.Min(rawK, 100f); // cap at radius ≈ 1cm
            Vector3 curveN = dT.sqrMagnitude > 0.0001f ? dT.normalized : Vector3.right;

            normals.Add(curveN);
            curvatures.Add(curveK);
        }

        return new Result
        {
            positions   = positions.ToArray(),
            tangents    = tangents.ToArray(),
            arcLengths  = arcLengths.ToArray(),
            normals     = normals.ToArray(),
            curvatures  = curvatures.ToArray(),
            totalLength = totalLength,
        };
    }
}
