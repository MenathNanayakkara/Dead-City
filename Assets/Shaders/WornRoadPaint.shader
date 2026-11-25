Shader "Outbreak/Worn Road Paint"
{
 Properties { _Color("Paint",Color)=(.56,.53,.42,1) _BumpMap("Road normal",2D)="bump"{} _Cutoff("Wear threshold",Range(0,1))=.38 _SurfaceY("Road surface height",Float)=.033 }
 SubShader {
  Tags {"Queue"="AlphaTest+2" "RenderType"="TransparentCutout"} Cull Off Offset -1,-1
  CGPROGRAM
  #pragma surface surf Standard alphatest:_Cutoff vertex:vert addshadow
  #pragma target 3.0
  #include "UnityCG.cginc"
  float4 _Color;float _SurfaceY;sampler2D _BumpMap;
  struct Input {float3 worldPos;};
  void vert(inout appdata_full v){float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;world.y=_SurfaceY;v.vertex=mul(unity_WorldToObject,float4(world,1));v.normal=normalize(mul((float3x3)unity_WorldToObject,float3(0,1,0)));v.tangent=float4(normalize(mul((float3x3)unity_WorldToObject,float3(1,0,0))),1);}
  float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
  void surf(Input IN,inout SurfaceOutputStandard o){float2 p=IN.worldPos.xz;float coarse=noise(p*8),fine=noise(p*135);o.Albedo=_Color.rgb*(.65+fine*.35);o.Alpha=coarse*.45+fine*.55;o.Normal=UnpackNormal(tex2D(_BumpMap,p*.5));o.Normal.xy*=.45;o.Smoothness=.12;o.Metallic=0;}
  ENDCG
 } FallBack "Transparent/Cutout/Diffuse"
}
