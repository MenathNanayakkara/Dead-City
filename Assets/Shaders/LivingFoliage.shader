Shader "Outbreak/Living Foliage"
{
 Properties {_Color("Tint",Color)=(1,1,1,1) _Wind("Wind displacement",Range(0,.1))=.018}
 SubShader {
  Tags {"RenderType"="Opaque"} Cull Off LOD 200
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
  #pragma target 3.0
  float4 _Color;float _Wind;
  struct Input {float4 color:COLOR;float2 leafUV;float facing:VFACE;};
  void vert(inout appdata_full v,out Input o){
   UNITY_INITIALIZE_OUTPUT(Input,o);
   o.leafUV=v.texcoord.xy;o.color=v.color;
   float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;
   // Alpha stores tip flexibility; roots stay fixed to their authored surface.
   v.vertex.x+=sin(p.x*1.3+p.z*.8+_Time.y*1.15)*_Wind*v.color.a;
   v.vertex.z+=cos(p.x*.7+p.z*1.9+_Time.y*.9)*_Wind*.4*v.color.a;
  }
  void surf(Input IN,inout SurfaceOutputStandard o){
   float2 uv=IN.leafUV;
   float midrib=1-smoothstep(0,.035,abs(uv.x-.5));
   float veins=pow(saturate(cos((uv.y+abs(uv.x-.5)*.65)*65)),18);
   float speckle=frac(sin(dot(floor(uv*160),float2(12.9898,78.233)))*43758.5453);
   float vein=(1-.16*midrib-.07*veins)*(.95+speckle*.1);
   o.Albedo=IN.color.rgb*_Color.rgb*vein;
   o.Normal=float3(0,0,IN.facing>=0?1:-1);
   o.Smoothness=.16;o.Metallic=0;o.Occlusion=.88;
   o.Alpha=1;
  }
  ENDCG
 } FallBack "Diffuse"
}
