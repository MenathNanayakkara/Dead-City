Shader "Outbreak/Overcast City Sky"
{
 Properties { _SkyColor("Cloud light",Color)=(.66,.66,.63,1) _Horizon("Horizon",Color)=(.49,.5,.47,1) _CloudDark("Cloud shade",Color)=(.35,.37,.36,1) }
 SubShader {
 Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
 Cull Off ZWrite Off
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct v2f { float4 pos:SV_POSITION; float3 direction:TEXCOORD0; };
 float4 _SkyColor,_Horizon,_CloudDark;
 v2f vert(appdata_base v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o; }
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 float fbm(float2 p){float s=0,a=.5;for(int k=0;k<5;k++){s+=noise(p)*a;p=p*2.03+17.2;a*=.5;}return s;}
 float4 frag(v2f i):SV_Target {
 float3 d=normalize(i.direction);float elevation=saturate(d.y);
 float2 uv=d.xz/(max(.12,d.y)+.65)*3.2;
 float clouds=fbm(uv)+fbm(uv*2.4)*.16;
 float3 c=lerp(_SkyColor.rgb,_CloudDark.rgb,smoothstep(.3,.79,clouds));
 c=lerp(_Horizon.rgb,c,smoothstep(0,.35,elevation));
 float glow=pow(saturate(dot(d,normalize(float3(-.5,.6,.35)))),22);
 c+=glow*float3(.28,.25,.19);return float4(c,1);
 }
 ENDCG
 }
 } FallBack Off
}
