Shader "Hidden/Outbreak/Atmosphere"
{
    Properties { _MainTex("Source",2D)="white"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BloomTex;
        float4 _MainTex_TexelSize, _Grade;
        float _Bloom;
        half4 bright(v2f_img i):SV_Target
        {
            half3 c=tex2D(_MainTex,i.uv).rgb;
            half peak=max(c.r,max(c.g,c.b));
            return half4(c*saturate((peak-.8)/max(peak,.01)),1);
        }
        half4 blur(v2f_img i):SV_Target
        {
            float2 d=_MainTex_TexelSize.xy*2;
            half3 c=tex2D(_MainTex,i.uv).rgb*.2;
            c+=tex2D(_MainTex,i.uv+float2(d.x,d.y)).rgb*.2;
            c+=tex2D(_MainTex,i.uv+float2(-d.x,d.y)).rgb*.2;
            c+=tex2D(_MainTex,i.uv+float2(d.x,-d.y)).rgb*.2;
            c+=tex2D(_MainTex,i.uv-d).rgb*.2;
            return half4(c,1);
        }
        half4 grade(v2f_img i):SV_Target
        {
            half3 c=(tex2D(_MainTex,i.uv).rgb+tex2D(_BloomTex,i.uv).rgb*_Bloom)*_Grade.x;
            half l=dot(c,half3(.2126,.7152,.0722));
            c=lerp(l.xxx,c,_Grade.y);
            c=(c-.18)*_Grade.z+.18;
            float2 p=i.uv*2-1;
            c*=1-_Grade.w*smoothstep(.3,1.5,dot(p,p));
            return half4(max(c,0),1);
        }
        ENDCG
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment bright
        ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
        ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment grade
        ENDCG }
    }
}
