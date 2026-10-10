using UnityEngine;

/// <summary>Lightweight Built-in pipeline grading and quarter-resolution bloom. No gameplay dependencies.</summary>
[ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
public sealed class CityCameraAtmosphere : MonoBehaviour
{
    [SerializeField] private Shader atmosphereShader;
    [SerializeField, Range(.5f,2)] private float exposure=1.06f;
    [SerializeField, Range(0,1.5f)] private float saturation=.85f;
    [SerializeField, Range(.5f,1.5f)] private float contrast=1.035f;
    [SerializeField, Range(0,.5f)] private float vignette=.15f;
    [SerializeField, Range(0,.8f)] private float bloom=.16f;
    private Material effect;

    public void Configure(Shader shader) { atmosphereShader=shader; }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if(atmosphereShader==null || !atmosphereShader.isSupported) { Graphics.Blit(source,destination); return; }
        if(effect==null) effect=new Material(atmosphereShader) { hideFlags=HideFlags.HideAndDontSave };
        var small=RenderTexture.GetTemporary(Mathf.Max(1,source.width/4),Mathf.Max(1,source.height/4),0,source.format);
        var blurred=RenderTexture.GetTemporary(small.width,small.height,0,source.format);
        try
        {
            Graphics.Blit(source,small,effect,0);
            Graphics.Blit(small,blurred,effect,1);
            effect.SetTexture("_BloomTex",blurred);
            effect.SetVector("_Grade",new Vector4(exposure,saturation,contrast,vignette));
            effect.SetFloat("_Bloom",bloom);
            Graphics.Blit(source,destination,effect,2);
        }
        finally { RenderTexture.ReleaseTemporary(small); RenderTexture.ReleaseTemporary(blurred); }
    }

    private void OnDisable()
    {
        if(effect==null)return;
        if(Application.isPlaying)Destroy(effect);else DestroyImmediate(effect);
        effect=null;
    }
}
