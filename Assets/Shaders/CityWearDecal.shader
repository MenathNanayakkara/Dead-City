Shader "Outbreak/City Wear Decal"
{
 Properties { _MainTex("Wear alpha",2D)="white"{} _Color("Tint",Color)=(.18,.17,.13,.5) }
 SubShader {
 Tags { "Queue"="Transparent-10" "RenderType"="Transparent" } Offset -1,-1
 ZWrite Off
 CGPROGRAM
 #pragma surface surf Standard alpha:fade
 sampler2D _MainTex;fixed4 _Color;
 struct Input { float2 uv_MainTex; };
 void surf(Input IN,inout SurfaceOutputStandard o){fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;o.Albedo=c.rgb;o.Alpha=c.a;o.Smoothness=.05;}
 ENDCG
 } FallBack "Transparent/Diffuse"
}
