Shader "Hidden/Outbreak/Contact Shadows"
{
 Properties {_MainTex("Source",2D)="white"{}}
 SubShader {
  Cull Off ZWrite Off ZTest Always
  CGINCLUDE
  #include "UnityCG.cginc"
  sampler2D _MainTex,_CameraDepthNormalsTexture,_OcclusionTex;
  float4 _MainTex_TexelSize,_Settings;
  float4x4 _InverseProjection;
  void sampleDepth(float2 uv,out float depth,out float3 normal){DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture,uv),depth,normal);depth*=_Settings.z;}
  float3 position(float2 uv,float depth){float4 p=mul(_InverseProjection,float4(uv*2-1,1,1));return p.xyz*(depth/max(.001,-p.z));}
  half4 ao(v2f_img i):SV_Target {
   float depth;float3 normal;sampleDepth(i.uv,depth,normal);
   if(depth>_Settings.z*.98)return 1;
   float3 p=position(i.uv,depth);
   float radius=_Settings.x;
   float2 extent=radius/(2*max(depth,.2)*_Settings.w)*float2(_ScreenParams.y/_ScreenParams.x,1);
   float sum=0;
   [unroll] for(int k=0;k<12;k++){
    float angle=(k+.5)*2.399963;float reach=sqrt((k+.5)/12.0);
    float2 uv=i.uv+float2(cos(angle),sin(angle))*extent*reach;
    float d;float3 n;sampleDepth(uv,d,n);
    float3 delta=position(uv,d)-p;float len=length(delta);
    float valid=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
    sum+=max(0,dot(normal,delta)/max(.001,len)-.09)*saturate(1-len/radius)*valid;
   }
   float value=saturate(1-sum/12*_Settings.y*5);
   return float4(value,value,value,1);
  }
  half4 blur(v2f_img i):SV_Target {
   float d;float3 n;sampleDepth(i.uv,d,n);
   float total=0,weight=0;
   [unroll] for(int x=-1;x<=1;x++)[unroll] for(int y=-1;y<=1;y++){
    float2 uv=i.uv+float2(x,y)*_MainTex_TexelSize.xy;
    float sd;float3 sn;sampleDepth(uv,sd,sn);
    float w=exp(-abs(sd-d)*24)*pow(saturate(dot(n,sn)),8)+.0001;
    total+=tex2D(_MainTex,uv).r*w;weight+=w;
   }
   return total/max(weight,.0001);
  }
  half4 composite(v2f_img i):SV_Target {half4 c=tex2D(_MainTex,i.uv);c.rgb*=lerp(1,tex2D(_OcclusionTex,i.uv).r,.8);return c;}
  ENDCG
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment ao
   #pragma target 3.0
  ENDCG }
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment blur
   #pragma target 3.0
  ENDCG }
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment composite
  ENDCG }
 } FallBack Off
}
