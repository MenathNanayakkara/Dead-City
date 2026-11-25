using UnityEngine;

/// <summary>
/// Built-in renderer presentation: linear HDR bloom, restrained height haze,
/// filmic highlight roll-off and optional edge antialiasing. Does not affect gameplay.
/// Keep this after contact occlusion and disable other grading/tonemapping effects.
/// </summary>
[ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
public sealed class CinematicCityCamera : MonoBehaviour
{
    [SerializeField] private Shader presentationShader;
    [Header("Exposure and colour")]
    [SerializeField, Range(-3f, 3f)] private float exposureEV = .2f;
    [SerializeField, Range(0f, 1.5f)] private float saturation = .94f;
    [SerializeField, Range(.75f, 1.25f)] private float contrast = 1.02f;
    [SerializeField, Range(0f, .3f)] private float vignette = .035f;
    [Header("HDR bloom")]
    [SerializeField, Range(0f, .3f)] private float bloomStrength = .055f;
    [SerializeField, Range(.3f, 4f)] private float bloomThreshold = 1.1f;
    [SerializeField, Range(.05f, 1f)] private float bloomSoftKnee = .5f;
    [Header("Atmospheric depth")]
    [SerializeField] private Color hazeColour = new Color(.53f, .60f, .64f, 1f);
    [SerializeField, Range(0f, .04f)] private float hazeDensity = .0035f;
    [SerializeField] private float hazeBaseHeight = .6f;
    [SerializeField, Range(.01f, 1f)] private float hazeHeightFalloff = .12f;
    [SerializeField, Range(0f, .8f)] private float maximumHaze = .28f;
    [Header("Edges")]
    [SerializeField] private bool edgeAntialiasing = true;
    [SerializeField, Range(.03f, .2f)] private float edgeThreshold = .09f;

    private Camera attachedCamera;
    private Material effect;

    public void Configure(Shader shader) { presentationShader = shader; }

    private void OnEnable()
    {
        attachedCamera = GetComponent<Camera>();
        attachedCamera.depthTextureMode |= DepthTextureMode.Depth;
        if (presentationShader == null)
            presentationShader = Shader.Find("Hidden/Outbreak/Cinematic City");
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (presentationShader == null || !presentationShader.isSupported || attachedCamera == null)
        {
            Graphics.Blit(source, destination);
            return;
        }
        if (effect != null && effect.shader != presentationShader) ReleaseMaterial();
        if (effect == null)
            effect = new Material(presentationShader) { hideFlags = HideFlags.HideAndDontSave };

        effect.SetVector("_Grade", new Vector4(Mathf.Pow(2f, exposureEV), saturation, contrast, vignette));
        effect.SetVector("_BloomSettings", new Vector4(bloomThreshold, bloomSoftKnee, bloomStrength, 0f));
        // Use a vector to avoid any implicit shader colour-property conversion.
        // The fog colour enters the composition in linear space in either project mode.
        Color linearHaze = hazeColour.linear;
        effect.SetVector("_HazeColour", new Vector4(linearHaze.r, linearHaze.g, linearHaze.b, linearHaze.a));
        effect.SetVector("_HazeSettings", new Vector4(hazeDensity, hazeBaseHeight, hazeHeightFalloff, maximumHaze));
        effect.SetMatrix("_InverseProjection", attachedCamera.projectionMatrix.inverse);
        effect.SetMatrix("_CityViewToWorld", attachedCamera.cameraToWorldMatrix);
        effect.SetVector("_CameraPosition", attachedCamera.transform.position);
        effect.SetFloat("_EdgeThreshold", edgeThreshold);

        RenderTexture bloomA = null, bloomB = null, graded = null;
        RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
            ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.Default;
        try
        {
            if (bloomStrength > .0001f)
            {
                int width = Mathf.Max(1, source.width / 2), height = Mathf.Max(1, source.height / 2);
                bloomA = RenderTexture.GetTemporary(width, height, 0, format, RenderTextureReadWrite.Linear);
                bloomB = RenderTexture.GetTemporary(width, height, 0, format, RenderTextureReadWrite.Linear);
                bloomA.filterMode = bloomB.filterMode = FilterMode.Bilinear;
                bloomA.wrapMode = bloomB.wrapMode = TextureWrapMode.Clamp;
                Graphics.Blit(source, bloomA, effect, 0);
                Graphics.Blit(bloomA, bloomB, effect, 1);
                Graphics.Blit(bloomB, bloomA, effect, 2);
                effect.SetTexture("_BloomTex", bloomA);
            }
            else effect.SetTexture("_BloomTex", Texture2D.blackTexture);

            if (edgeAntialiasing)
            {
                graded = RenderTexture.GetTemporary(source.width, source.height, 0, format, RenderTextureReadWrite.Linear);
                graded.filterMode = FilterMode.Bilinear;
                graded.wrapMode = TextureWrapMode.Clamp;
                Graphics.Blit(source, graded, effect, 3);
                Graphics.Blit(graded, destination, effect, 4);
            }
            else Graphics.Blit(source, destination, effect, 3);
        }
        finally
        {
            if (bloomA != null) RenderTexture.ReleaseTemporary(bloomA);
            if (bloomB != null) RenderTexture.ReleaseTemporary(bloomB);
            if (graded != null) RenderTexture.ReleaseTemporary(graded);
        }
    }

    private void ReleaseMaterial()
    {
        if (effect == null) return;
        if (Application.isPlaying) Destroy(effect); else DestroyImmediate(effect);
        effect = null;
    }

    private void OnDisable()
    {
        ReleaseMaterial();
        // Other camera effects may share the depth texture; retain the shared request.
    }
}
