Shader "Outbreak/Scanned Foliage"
{
    Properties
    {
        _Color ("Leaf tint", Color) = (0.85, 0.88, 0.80, 1)
        _MainTex ("Diffuse (sRGB)", 2D) = "white" {}
        [NoScaleOffset] _AlphaTex ("Opacity (linear, red channel)", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0.05, 0.95)) = 0.4
        [Normal] [NoScaleOffset] _BumpMap ("Normal (OpenGL)", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0, 2)) = 0.8
        [Toggle] _FlipNormalY ("Flip normal green channel", Float) = 0
        [NoScaleOffset] _RoughnessMap ("Roughness (linear)", 2D) = "white" {}
        _RoughnessScale ("Roughness multiplier", Range(0.1, 2)) = 1
        _MinimumRoughness ("Minimum roughness", Range(0, 1)) = 0.55
        [NoScaleOffset] _OcclusionMap ("Ambient occlusion (linear)", 2D) = "white" {}
        _OcclusionStrength ("Ambient occlusion strength", Range(0, 1)) = 1
        _Translucency ("Backlit leaf transmission", Range(0, 0.5)) = 0.12
        _WindStrength ("Wind displacement (world metres)", Range(0, 0.08)) = 0.012
        _WindSpeed ("Wind speed", Range(0, 3)) = 0.8
        _WindDirection ("Wind direction (world XZ)", Vector) = (1, 0, 0.35, 0)
        _WindBaseHeight ("Root height (mesh local Y)", Float) = 0
        _WindHeight ("Flexible height (mesh local metres)", Float) = 0.75
        _WindUVInfluence ("Multiply flexibility by UV V", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "IgnoreProjector" = "True"
            // The root mask uses the original mesh-space height. GPU instancing
            // is supported; static batching would destroy that coordinate space.
            "DisableBatching" = "True"
        }
        LOD 300
        Cull Off
        ZWrite On

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf ScannedFoliage fullforwardshadows addshadow vertex:vert alphatest:_Cutoff exclude_path:deferred
        #pragma multi_compile_instancing

        #include "UnityPBSLighting.cginc"
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex;
        sampler2D _AlphaTex;
        sampler2D _BumpMap;
        sampler2D _RoughnessMap;
        sampler2D _OcclusionMap;
        fixed4 _Color;
        half _BumpScale;
        half _FlipNormalY;
        half _RoughnessScale;
        half _MinimumRoughness;
        half _OcclusionStrength;
        half _Translucency;
        float _WindStrength;
        float _WindSpeed;
        float4 _WindDirection;
        float _WindBaseHeight;
        float _WindHeight;
        float _WindUVInfluence;

        struct Input
        {
            // Unity applies _MainTex_ST to UV0. Every map shares this UV set
            // and its tiling/offset, as in the source photogrammetry material.
            float2 uv_MainTex;
            float facing : VFACE;
        };

        void vert(inout appdata_full vertex)
        {
            float heightMask = saturate((vertex.vertex.y - _WindBaseHeight) /
                                        max(_WindHeight, 0.001));
            heightMask = heightMask * heightMask * (3.0 - 2.0 * heightMask);
            float flexibility = heightMask * lerp(1.0, saturate(vertex.texcoord.y),
                                                   _WindUVInfluence);
            float3 worldPosition = mul(unity_ObjectToWorld, vertex.vertex).xyz;
            float2 windDirection = _WindDirection.xz;
            windDirection /= max(length(windDirection), 0.001);
            float phase = dot(worldPosition.xz, float2(0.63, 0.41));
            float time = _Time.y * _WindSpeed;
            float sway = sin(time + phase) * 0.75 +
                         sin(time * 1.73 + phase * 1.91) * 0.25;
            float2 worldOffset = windDirection * sway * _WindStrength * flexibility;
            vertex.vertex.xyz += mul((float3x3)unity_WorldToObject,
                                     float3(worldOffset.x, 0.0, worldOffset.y));
        }

        // Retain Unity's physically based dielectric lighting and shadowed light
        // attenuation. Transmission only adds a small amount of light through
        // backlit leaves; it is not emission and is absent without a direct light.
        half4 LightingScannedFoliage(SurfaceOutputStandard surface, half3 viewDirection,
                                    UnityGI lighting)
        {
            half4 color = LightingStandard(surface, viewDirection, lighting);
            half backlit = saturate(dot(-surface.Normal, lighting.light.dir));
            half viewAlignment = saturate(dot(viewDirection, -lighting.light.dir));
            half transmission = backlit * lerp(0.35h, 1.0h,
                                               viewAlignment * viewAlignment);
            color.rgb += surface.Albedo * lighting.light.color *
                         (_Translucency * transmission * surface.Occlusion);
            return color;
        }

        void LightingScannedFoliage_GI(SurfaceOutputStandard surface, UnityGIInput data,
                                      inout UnityGI lighting)
        {
            LightingStandard_GI(surface, data, lighting);
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            fixed3 diffuse = tex2D(_MainTex, input.uv_MainTex).rgb;
            half opacity = tex2D(_AlphaTex, input.uv_MainTex).r;
            half3 tangentNormal = UnpackScaleNormal(tex2D(_BumpMap, input.uv_MainTex),
                                                   _BumpScale);
            tangentNormal.y *= lerp(1.0h, -1.0h, saturate(_FlipNormalY));
            // On the reverse side of a leaf, flip the tangent-space Z axis so
            // lighting uses the back surface instead of the unseen front normal.
            tangentNormal.z *= input.facing >= 0.0 ? 1.0h : -1.0h;
            half roughness = saturate(tex2D(_RoughnessMap, input.uv_MainTex).r *
                                      _RoughnessScale);
            roughness = max(roughness, _MinimumRoughness);
            half occlusion = tex2D(_OcclusionMap, input.uv_MainTex).r;

            output.Albedo = diffuse * _Color.rgb;
            output.Normal = normalize(tangentNormal);
            output.Metallic = 0;
            output.Smoothness = 1.0h - roughness;
            output.Occlusion = lerp(1.0h, occlusion, _OcclusionStrength);
            output.Alpha = opacity * _Color.a;
        }
        ENDCG
    }
    FallBack Off
}
