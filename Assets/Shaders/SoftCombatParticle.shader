Shader "ZombieOutbreak/Soft Combat Particle"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Intensity ("Brightness", Range(0,4)) = 1
        _Flare ("Flame Shape", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha [_DstBlend]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Tint;
            float _Intensity, _Flare;
            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Tint;
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float r = length(p);
                // The entire quad boundary is transparent, even without a bitmap.
                float soft = saturate(1 - r * r);
                soft *= soft;
                float grain = .78 + .12 * sin(p.x * 13 + sin(p.y * 9))
                                  + .10 * sin(p.y * 17 - p.x * 6);
                float angle = atan2(p.y, p.x);
                float lobes = pow(abs(sin(angle * 3 + .4)), 8);
                float flame = saturate((.28 + lobes * .65 - r) * 4);
                flame = max(flame, exp(-r * r * 25) * .85) * soft;
                fixed4 result = i.color;
                result.rgb *= _Intensity;
                result.a *= lerp(soft * grain, flame, _Flare);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
    Fallback Off
}
