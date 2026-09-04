using UnityEngine;

/// <summary>
/// Renders a selected InteriorMapBaker framework into a projection-specific interior texture.
/// The output direction convention matches InteriorMappingFunction.hlsl.
/// </summary>
public static class InteriorMapTextureBaker
{
    private const float Pi = Mathf.PI;
    private const float MinimumClipPlane = 0.01f;
    private const float MinimumFarClipPlane = 1f;
    private const float HemisphereDiskEpsilon = 1e-5f;

    private static readonly Vector3[] CubemapFaceDirections =
    {
        Vector3.right,
        Vector3.left,
        Vector3.up,
        Vector3.down,
        Vector3.forward,
        Vector3.back
    };

    private static readonly Vector3[] CubemapFaceUps =
    {
        Vector3.down,
        Vector3.down,
        Vector3.forward,
        Vector3.back,
        Vector3.down,
        Vector3.down
    };

    /// <summary>
    /// Renders six explicit views and converts them to the selected output projection.
    /// </summary>
    public static Texture2D Bake(
        InteriorMapBaker framework,
        int faceResolution = 256,
        int cullingMask = -1,
        InteriorMapBakeType bakeType = InteriorMapBakeType.Box)
    {
        ValidateFramework(framework);
        faceResolution = Mathf.Clamp(faceResolution, 16, 2048);

        Cubemap cubemap = null;
        RenderTexture renderTarget = null;
        Texture2D faceTexture = null;
        RenderTexture previousActive = RenderTexture.active;
        Camera bakeCamera = framework.BakeCamera;
        Vector3 bakePosition = default;
        Quaternion bakeRotation = Quaternion.identity;
        RenderTexture previousTargetTexture = null;
        bool cameraStateCaptured = false;

        try
        {
            framework.SyncBakeCamera();
            bakePosition = bakeCamera.transform.position;
            bakeRotation = bakeCamera.transform.rotation;
            previousTargetTexture = bakeCamera.targetTexture;
            cameraStateCaptured = true;
            ConfigureBakeCamera(framework, bakeCamera, cullingMask);

            cubemap = new Cubemap(faceResolution, TextureFormat.RGBA32, false)
            {
                name = "InteriorMap_Cubemap"
            };
            renderTarget = new RenderTexture(
                faceResolution,
                faceResolution,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear)
            {
                name = "InteriorMap_BakeFace"
            };
            renderTarget.Create();
            faceTexture = new Texture2D(faceResolution, faceResolution, TextureFormat.RGBA32, false, false)
            {
                name = "InteriorMap_BakeFaceReadback"
            };

            bakeCamera.targetTexture = renderTarget;
            RenderCubemapFaces(
                bakeCamera,
                cubemap,
                faceTexture,
                bakePosition,
                bakeRotation);
            cubemap.Apply(false, false);

            return ConvertToOutputTexture(
                cubemap,
                bakeRotation,
                faceResolution,
                bakeType);
        }
        finally
        {
            RenderTexture.active = previousActive;
            if (bakeCamera != null)
            {
                if (cameraStateCaptured)
                    bakeCamera.transform.SetPositionAndRotation(bakePosition, bakeRotation);
                bakeCamera.targetTexture = previousTargetTexture;
                bakeCamera.cameraType = CameraType.Reflection;
                bakeCamera.enabled = false;
            }
            if (faceTexture != null)
                Object.DestroyImmediate(faceTexture);
            if (renderTarget != null)
            {
                renderTarget.Release();
                Object.DestroyImmediate(renderTarget);
            }
            if (cubemap != null)
                Object.DestroyImmediate(cubemap);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Validation — ensure the selected scene framework is complete
    // ════════════════════════════════════════════════════════════
    private static void ValidateFramework(InteriorMapBaker framework)
    {
        if (framework == null)
            throw new UnityException("InteriorMapBaker: No framework has been selected.");
        if (!framework.IsInitialized)
        {
            throw new UnityException(
                "InteriorMapBaker: The selected framework has not been initialized. "
                + "Click Create Framework on the scene component first.");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Camera setup — configure the persistent camera for isolated reflection-style rendering
    // ════════════════════════════════════════════════════════════
    private static void ConfigureBakeCamera(
        InteriorMapBaker framework,
        Camera bakeCamera,
        int cullingMask)
    {
        bakeCamera.cameraType = CameraType.Reflection;
        bakeCamera.cullingMask = cullingMask;
        bakeCamera.clearFlags = CameraClearFlags.SolidColor;
        bakeCamera.backgroundColor = Color.clear;
        bakeCamera.nearClipPlane = MinimumClipPlane;
        bakeCamera.farClipPlane = CalculateFarClipPlane(framework.Volume);
        bakeCamera.fieldOfView = 90f;
        bakeCamera.aspect = 1f;
        bakeCamera.enabled = false;
    }

    // ════════════════════════════════════════════════════════════
    //  Face rendering — ReadPixels and Cubemap.SetPixels share bottom-left coordinates
    // ════════════════════════════════════════════════════════════
    private static void RenderCubemapFaces(
        Camera bakeCamera,
        Cubemap cubemap,
        Texture2D faceTexture,
        Vector3 bakePosition,
        Quaternion bakeRotation)
    {
        for (int faceIndex = 0; faceIndex < CubemapFaceDirections.Length; faceIndex++)
        {
            Quaternion faceRotation = bakeRotation * Quaternion.LookRotation(
                CubemapFaceDirections[faceIndex],
                CubemapFaceUps[faceIndex]);
            bakeCamera.transform.SetPositionAndRotation(bakePosition, faceRotation);
            bakeCamera.Render();

            RenderTexture.active = bakeCamera.targetTexture;
            faceTexture.ReadPixels(
                new Rect(0, 0, bakeCamera.targetTexture.width, bakeCamera.targetTexture.height),
                0,
                0,
                false);
            faceTexture.Apply(false, false);
            cubemap.SetPixels(faceTexture.GetPixels(), (CubemapFace)faceIndex);
        }
    }

    private static float CalculateFarClipPlane(InteriorMapBakeVolume volume)
    {
        Vector3 worldSize = Vector3.Scale(volume.Size, Abs(volume.transform.lossyScale));
        return Mathf.Max(worldSize.magnitude * 2f, MinimumFarClipPlane);
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    // ════════════════════════════════════════════════════════════
    //  Output projection — select the texture layout that matches the shader projection
    // ════════════════════════════════════════════════════════════
    private static Texture2D ConvertToOutputTexture(
        Cubemap cubemap,
        Quaternion cameraRotation,
        int faceResolution,
        InteriorMapBakeType bakeType)
    {
        if (bakeType == InteriorMapBakeType.Hemisphere)
        {
            return ConvertToHemisphere(cubemap, cameraRotation, faceResolution);
        }

        return ConvertToEquirectangular(
            cubemap,
            cameraRotation,
            faceResolution * 2,
            faceResolution);
    }

    // ════════════════════════════════════════════════════════════
    //  Box projection — convert the cubemap to a 2:1 equirectangular map
    // ════════════════════════════════════════════════════════════
    private static Texture2D ConvertToEquirectangular(
        Cubemap cubemap,
        Quaternion cameraRotation,
        int width,
        int height)
    {
        var result = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = "InteriorMap_Baked",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat
        };
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float vertical = (y + 0.5f) / height;
            float latitude = (vertical - 0.5f) * Pi;
            float latitudeCosine = Mathf.Cos(latitude);
            float latitudeSine = Mathf.Sin(latitude);

            for (int x = 0; x < width; x++)
            {
                float horizontal = (x + 0.5f) / width;
                float longitude = (horizontal - 0.5f) * (2f * Pi);
                Vector3 localDirection = new Vector3(
                    Mathf.Sin(longitude) * latitudeCosine,
                    latitudeSine,
                    Mathf.Cos(longitude) * latitudeCosine);
                Vector3 worldDirection = cameraRotation * localDirection;
                pixels[y * width + x] = SampleCubemap(cubemap, worldDirection);
            }
        }

        result.SetPixels(pixels);
        result.Apply(false, false);
        return result;
    }

    // ════════════════════════════════════════════════════════════
    //  Hemisphere projection — pack the shader's rear hemisphere into a square disk
    // ════════════════════════════════════════════════════════════
    private static Texture2D ConvertToHemisphere(
        Cubemap cubemap,
        Quaternion cameraRotation,
        int resolution)
    {
        var result = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, false)
        {
            name = "InteriorMap_Baked",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            float vertical = ((y + 0.5f) / resolution) * 2f - 1f;
            for (int x = 0; x < resolution; x++)
            {
                float horizontal = ((x + 0.5f) / resolution) * 2f - 1f;
                float radiusSquared = horizontal * horizontal + vertical * vertical;
                int pixelIndex = y * resolution + x;
                if (radiusSquared >= 1f - HemisphereDiskEpsilon)
                {
                    pixels[pixelIndex] = Color.clear;
                    continue;
                }

                float backZ = -Mathf.Sqrt(Mathf.Max(1f - radiusSquared, 0f));
                Vector3 shaderDirection = new Vector3(horizontal, vertical, backZ);
                Vector3 cameraDirection = new Vector3(
                    shaderDirection.x,
                    shaderDirection.y,
                    -shaderDirection.z);
                Vector3 worldDirection = cameraRotation * cameraDirection;
                pixels[pixelIndex] = SampleCubemap(cubemap, worldDirection);
            }
        }

        result.SetPixels(pixels);
        result.Apply(false, false);
        return result;
    }

    // ════════════════════════════════════════════════════════════
    //  Cubemap sampling — map a world direction to a rendered cube face
    // ════════════════════════════════════════════════════════════
    private static Color SampleCubemap(Cubemap cubemap, Vector3 direction)
    {
        Vector3 absoluteDirection = Abs(direction);
        CubemapFace face;
        float u;
        float v;

        if (absoluteDirection.x >= absoluteDirection.y
            && absoluteDirection.x >= absoluteDirection.z)
        {
            float scale = 1f / Mathf.Max(absoluteDirection.x, 0.0001f);
            if (direction.x >= 0f)
            {
                face = CubemapFace.PositiveX;
                u = direction.z * scale;
            }
            else
            {
                face = CubemapFace.NegativeX;
                u = -direction.z * scale;
            }
            v = -direction.y * scale;
        }
        else if (absoluteDirection.y >= absoluteDirection.z)
        {
            float scale = 1f / Mathf.Max(absoluteDirection.y, 0.0001f);
            if (direction.y >= 0f)
            {
                face = CubemapFace.PositiveY;
                v = direction.z * scale;
            }
            else
            {
                face = CubemapFace.NegativeY;
                v = -direction.z * scale;
            }
            u = -direction.x * scale;
        }
        else
        {
            float scale = 1f / Mathf.Max(absoluteDirection.z, 0.0001f);
            if (direction.z >= 0f)
            {
                face = CubemapFace.PositiveZ;
                u = -direction.x * scale;
            }
            else
            {
                face = CubemapFace.NegativeZ;
                u = direction.x * scale;
            }
            v = -direction.y * scale;
        }

        int coordinate = cubemap.width - 1;
        int x = Mathf.Clamp(Mathf.RoundToInt((u * 0.5f + 0.5f) * coordinate), 0, coordinate);
        int y = Mathf.Clamp(Mathf.RoundToInt((v * 0.5f + 0.5f) * coordinate), 0, coordinate);
        return cubemap.GetPixel(face, x, y);
    }
}
