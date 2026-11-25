using UnityEngine;

/// <summary>Half-resolution contact occlusion with depth-aware blur for the Built-in pipeline.</summary>
[ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
public sealed class CityContactShadows : MonoBehaviour
{
    [SerializeField] private Shader shader;
    [SerializeField, Range(.1f,1.5f)] private float radius=.55f;
    [SerializeField, Range(0,3)] private float strength=1.15f;
    private Camera cameraComponent;
    private Material material;
    public void Configure(Shader value){shader=value;}
    private void OnEnable()
    {
        cameraComponent=GetComponent<Camera>();
        cameraComponent.depthTextureMode|=DepthTextureMode.DepthNormals;
    }
    private void OnRenderImage(RenderTexture source,RenderTexture destination)
    {
        if(shader==null||!shader.isSupported||SystemInfo.graphicsShaderLevel<30){Graphics.Blit(source,destination);return;}
        if(material==null)material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        material.SetMatrix("_InverseProjection",cameraComponent.projectionMatrix.inverse);
        material.SetVector("_Settings",new Vector4(radius,strength,cameraComponent.farClipPlane,Mathf.Tan(cameraComponent.fieldOfView*Mathf.Deg2Rad*.5f)));
        int w=Mathf.Max(1,source.width/2),h=Mathf.Max(1,source.height/2);
        var raw=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var smooth=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        try
        {
            raw.filterMode=smooth.filterMode=FilterMode.Bilinear;
            Graphics.Blit(source,raw,material,0);
            Graphics.Blit(raw,smooth,material,1);
            material.SetTexture("_OcclusionTex",smooth);
            Graphics.Blit(source,destination,material,2);
        }
        finally{RenderTexture.ReleaseTemporary(raw);RenderTexture.ReleaseTemporary(smooth);}
    }
    private void OnDisable()
    {
        if(material==null)return;
        if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);
        material=null;
        // Another camera effect may also require depth normals; leave the shared flag intact.
    }
}
