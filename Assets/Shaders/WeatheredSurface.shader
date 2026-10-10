Shader "Outbreak/Weathered Surface"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Photographic albedo", 2D) = "white" {}
        _BumpMap ("OpenGL normal", 2D) = "bump" {}
        _Roughness ("Linear roughness", 2D) = "white" {}
        _Scale ("Tiles per metre", Float) = 0.5
        _Triplanar ("World projection", Float) = 1
        _Metallic ("Metallic", Range(0,1)) = 0
        _Wetness ("Surface dampness", Range(0,1)) = 0.1
        _NormalStrength ("Normal strength", Range(0,2)) = 0.75
        _SmoothnessFloor ("Minimum smoothness", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap, _Roughness;
        fixed4 _Color;
        float _Scale, _Triplanar, _Metallic, _Wetness, _NormalStrength, _SmoothnessFloor;
        struct Input { float2 uv_MainTex; float3 worldPos; float3 worldNormal; INTERNAL_DATA };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 baseN = normalize(WorldNormalVector(IN, float3(0,0,1)));
            float3 weights = pow(abs(baseN), 6);
            weights /= max(dot(weights, float3(1,1,1)), 0.001);
            float3 p = IN.worldPos * _Scale;
            fixed3 albedo;
            half rough;
            float3 tangentN;
            if (_Triplanar > 0.5)
            {
                albedo = tex2D(_MainTex,p.zy).rgb*weights.x + tex2D(_MainTex,p.xz).rgb*weights.y + tex2D(_MainTex,p.xy).rgb*weights.z;
                rough = tex2D(_Roughness,p.zy).r*weights.x + tex2D(_Roughness,p.xz).r*weights.y + tex2D(_Roughness,p.xy).r*weights.z;
                float3 nx = UnpackNormal(tex2D(_BumpMap,p.zy));
                float3 ny = UnpackNormal(tex2D(_BumpMap,p.xz));
                float3 nz = UnpackNormal(tex2D(_BumpMap,p.xy));
                float3 worldN = normalize(baseN + _NormalStrength * (
                    float3(0,nx.y,nx.x)*weights.x + float3(ny.x,0,ny.y)*weights.y + float3(nz.x,nz.y,0)*weights.z));
                tangentN = float3(dot(worldN, WorldNormalVector(IN,float3(1,0,0))),
                    dot(worldN, WorldNormalVector(IN,float3(0,1,0))), dot(worldN,baseN));
            }
            else
            {
                albedo = tex2D(_MainTex,IN.uv_MainTex).rgb;
                rough = tex2D(_Roughness,IN.uv_MainTex).r;
                tangentN = UnpackNormal(tex2D(_BumpMap,IN.uv_MainTex));
                tangentN.xy *= _NormalStrength;
            }
            float macro = 0.96 + 0.04*sin(IN.worldPos.x*0.61+sin(IN.worldPos.z*0.41))*sin(IN.worldPos.y*0.72+IN.worldPos.z*0.18);
            o.Albedo = albedo * _Color.rgb * macro * (1-_Wetness*0.2);
            o.Metallic = _Metallic;
            o.Smoothness = max(_SmoothnessFloor, saturate(1-rough + _Wetness*0.28));
            o.Normal = normalize(tangentN);
            o.Occlusion = 1;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
