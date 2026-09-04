using UnityEngine;

/// <summary>
/// Voronoi / Worley noise implementation. Provides 3D Voronoi noise.
/// Call via NoiseGenerator — not intended for direct use.
/// </summary>
public static class VoronoiNoise
{
    private static float Hash01(int x, int y, int z, int seed)
    {
        int h = x * 374761393 ^ y * 668265263 ^ z * 2147483647 ^ seed * 1274126177;
        h = (h ^ (h >> 13)) * 1274126177;
        h ^= (h >> 16);
        uint uh = (uint)h;
        return (uh & 0x00FFFFFF) / 16777215f;
    }

    private static Vector3 FeaturePoint(int cx, int cy, int cz)
    {
        return new Vector3(
            Hash01(cx, cy, cz, 17),
            Hash01(cx, cy, cz, 31),
            Hash01(cx, cy, cz, 47)
        );
    }

    /// <summary>3D Voronoi noise. Returns a value in [0, 1] (1 = far from feature points, 0 = near).</summary>
    public static float Sample(float x, float y, float z, float scale)
    {
        float px = x * scale;
        float py = y * scale;
        float pz = z * scale;

        int ix = Mathf.FloorToInt(px);
        int iy = Mathf.FloorToInt(py);
        int iz = Mathf.FloorToInt(pz);

        float minDist = float.MaxValue;
        Vector3 p = new Vector3(px, py, pz);

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = ix + dx;
                    int cy = iy + dy;
                    int cz = iz + dz;
                    Vector3 fp = FeaturePoint(cx, cy, cz);
                    Vector3 featurePos = new Vector3(cx + fp.x, cy + fp.y, cz + fp.z);
                    float dist = Vector3.Distance(p, featurePos);
                    if (dist < minDist) minDist = dist;
                }
            }
        }

        return 1f - Mathf.Clamp01(minDist);
    }
}
