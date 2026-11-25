Shader "Outbreak/Survivor Surface"
{
 Properties {
  _Color("Tint",Color)=(1,1,1,1)
  _MainTex("Albedo",2D)="white"{}
  _BumpMap("Normal",2D)="bump"{}
  _Roughness("Roughness",2D)="white"{}
  _OcclusionMap("Occlusion",2D)="white"{}
  _BumpScale("Normal strength",Range(0,2))=1
  _RoughnessGamma("Restore gamma encoded source",Float)=1
  _RoughnessAmount("Roughness map weight",Range(0,1))=1
  _Smoothness("Fallback smoothness",Range(0,1))=.2
  _Dirt("Embedded dust",Range(0,1))=.25
  _DirtColor("Dirt",Color)=(.22,.16,.09,1)
  _Infection("Infection mottling",Range(0,1))=0
  _InfectionColor("Infection",Color)=(.27,.22,.13,1)
 }
 SubShader {
  Tags {"RenderType"="Opaque"} LOD 300
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.0
  sampler2D _MainTex,_BumpMap,_Roughness,_OcclusionMap;
  float4 _Color,_DirtColor,_InfectionColor;
  float _BumpScale,_RoughnessGamma,_RoughnessAmount,_Smoothness,_Dirt,_Infection;
  struct Input {float2 uv_MainTex;};
  float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
  void surf(Input IN,inout SurfaceOutputStandard o){
   float2 uv=IN.uv_MainTex;
   float3 a=tex2D(_MainTex,uv).rgb*_Color.rgb;
   float grime=noise(uv*17)*.65+noise(uv*53)*.35;
   float dirt=smoothstep(.46,.78,grime)*_Dirt;
   // UV noise follows the animated surface, unlike a swimming world-space overlay.
   a=lerp(a,a*.6+_DirtColor.rgb*.4,dirt);
   float mottling=smoothstep(.48,.76,noise(uv*26+3)*.7+noise(uv*93)*.3)*_Infection;
   o.Albedo=lerp(a,_InfectionColor.rgb,mottling*.55);
   o.Normal=UnpackNormal(tex2D(_BumpMap,uv));o.Normal.xy*=_BumpScale;o.Normal=normalize(o.Normal);
   float rough=pow(max(.001,tex2D(_Roughness,uv).r),_RoughnessGamma);
   o.Smoothness=clamp(lerp(_Smoothness,1-rough,_RoughnessAmount)*(1-dirt*.55),.035,.55);
   o.Occlusion=lerp(1,tex2D(_OcclusionMap,uv).g,.72);
   o.Metallic=0;o.Alpha=1;
  }
  ENDCG
 } FallBack "Standard"
}
