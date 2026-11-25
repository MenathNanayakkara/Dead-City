Shader "Outbreak/Reclaimed Surface"
{
 Properties {
  _Color("Tint",Color)=(1,1,1,1)
  _MainTex("Photographic albedo",2D)="white"{}
  _BumpMap("Normal",2D)="bump"{}
  _Roughness("Roughness",2D)="white"{}
  _Scale("Tiles per metre",Float)=.5
  _Triplanar("World projection",Float)=1
  _Metallic("Metallic",Range(0,1))=0
  _Wetness("Dampness",Range(0,1))=.1
  _NormalStrength("Normal strength",Range(0,2))=.8
  _SmoothnessFloor("Minimum smoothness",Range(0,1))=0
  _Moss("Moss and rain staining",Range(0,1))=.5
  _MossColor("Moss color",Color)=(.16,.21,.075,1)
 }
 SubShader {
  Tags {"RenderType"="Opaque"} LOD 300
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.0
  sampler2D _MainTex,_BumpMap,_Roughness;
  float4 _Color,_MossColor;
  float _Scale,_Triplanar,_Metallic,_Wetness,_NormalStrength,_SmoothnessFloor,_Moss;
  struct Input {float2 uv_MainTex;float3 worldPos;float3 worldNormal;INTERNAL_DATA};
  float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
  float fbm(float2 p){return noise(p)*.57+noise(p*2.13+7)*.28+noise(p*4.21+19)*.15;}
  void surf(Input IN,inout SurfaceOutputStandard o){
   float3 n=normalize(WorldNormalVector(IN,float3(0,0,1)));
   float3 w=pow(abs(n),6);w/=max(dot(w,1),.001);
   float3 p=IN.worldPos*_Scale;
   float3 a;float r;float3 tn;
   if(_Triplanar>.5){
    a=tex2D(_MainTex,p.zy).rgb*w.x+tex2D(_MainTex,p.xz).rgb*w.y+tex2D(_MainTex,p.xy).rgb*w.z;
    r=tex2D(_Roughness,p.zy).r*w.x+tex2D(_Roughness,p.xz).r*w.y+tex2D(_Roughness,p.xy).r*w.z;
    float3 nx=UnpackNormal(tex2D(_BumpMap,p.zy)),ny=UnpackNormal(tex2D(_BumpMap,p.xz)),nz=UnpackNormal(tex2D(_BumpMap,p.xy));
    float3 wn=normalize(n+_NormalStrength*(float3(0,nx.y,nx.x)*w.x+float3(ny.x,0,ny.y)*w.y+float3(nz.x,nz.y,0)*w.z));
    tn=float3(dot(wn,WorldNormalVector(IN,float3(1,0,0))),dot(wn,WorldNormalVector(IN,float3(0,1,0))),dot(wn,n));
   }else{a=tex2D(_MainTex,IN.uv_MainTex).rgb;r=tex2D(_Roughness,IN.uv_MainTex).r;tn=UnpackNormal(tex2D(_BumpMap,IN.uv_MainTex));tn.xy*=_NormalStrength;}
   float3 wp=IN.worldPos;
   float macro=fbm(wp.xz*.38+wp.y*.21);
   float streak=fbm(float2(wp.x*3.7+wp.z*2.3,wp.y*.16));
   float ground=saturate(1-wp.y*.35);
   // Keep the centre of the main roadway readable; growth favors damp curbs and walls.
   float curb=smoothstep(3.5,7,abs(wp.x));
   float moss=smoothstep(.46,.75,macro)*_Moss*(ground*lerp(.1,1,curb)+.2*(1-saturate(n.y)));
   moss=saturate(moss);
   float stain=smoothstep(.48,.78,streak)*_Moss*(1-saturate(n.y))*.3;
   float wet=_Wetness*smoothstep(.3,.66,macro);
   o.Albedo=lerp(a*_Color.rgb*(.88+macro*.22)*(1-stain),_MossColor.rgb*(.65+noise(wp.xz*32)*.5),moss)*(1-wet*.25);
   o.Normal=normalize(tn);o.Metallic=_Metallic*(1-moss);
   o.Smoothness=lerp(max(_SmoothnessFloor,saturate(1-r+wet*.38)),.08,moss);
   o.Occlusion=1-stain*.22;o.Alpha=1;
  }
  ENDCG
 } FallBack "Standard"
}
