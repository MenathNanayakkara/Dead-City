Shader "Outbreak/Dirty City Glass"
{
    Properties
    {
        // Opaque panes represent a dark room behind glass without introducing
        // transparency sorting or requiring changes to the existing buildings.
        _Color ("Dark interior tint", Color) = (.045, .055, .055, 1)
        _DustColor ("Dry dust colour", Color) = (.16, .15, .125, 1)
        _Smoothness ("Clean glass smoothness", Range(.3, .98)) = .82
        _DustSmoothness ("Dust smoothness", Range(0, .7)) = .24
        _Reflectance ("Dielectric reflectance at normal incidence", Range(.02, .08)) = .04
        _DustAmount ("Dust coverage", Range(0, 1)) = .38
        _StreakStrength ("Vertical rain streaks", Range(0, 1)) = .45
        _PaneVariation ("Broad glass variation", Range(0, 1)) = .32
        _DetailScale ("Dirt scale per world metre", Range(.25, 4)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 250

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf StandardSpecular fullforwardshadows addshadow
        #pragma multi_compile_instancing

        fixed4 _Color, _DustColor;
        half _Smoothness, _DustSmoothness, _Reflectance;
        half _DustAmount, _StreakStrength, _PaneVariation;
        float _DetailScale;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        float Hash(float2 position)
        {
            return frac(sin(dot(position, float2(127.1, 311.7))) * 43758.5453);
        }

        float Noise(float2 position)
        {
            float2 cell = floor(position), blend = frac(position);
            blend = blend * blend * (3.0 - 2.0 * blend);
            return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), blend.x),
                        lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), blend.x), blend.y);
        }

        float Grime(float2 position)
        {
            return Noise(position) * .65 + Noise(position * 2.07 + 11.3) * .25 +
                   Noise(position * 4.13 + 31.7) * .10;
        }

        void surf(Input input, inout SurfaceOutputStandardSpecular output)
        {
            float3 n = normalize(input.worldNormal);
            float3 weights = pow(abs(n), 8);
            weights /= max(weights.x + weights.y + weights.z, .001);
            float3 p = input.worldPos * _DetailScale;

            // All masks are smoothly interpolated world-space noise: no hard
            // per-cell tint changes cutting across a pane or building facade.
            float broad = Grime(p.zy * .47) * weights.x +
                          Grime(p.xz * .47) * weights.y +
                          Grime(p.xy * .47) * weights.z;
            float grime = Grime(p.zy * 2.1 + 7) * weights.x +
                          Grime(p.xz * 2.1 + 7) * weights.y +
                          Grime(p.xy * 2.1 + 7) * weights.z;

            // Fast horizontal variation and slow vertical variation create
            // narrow rainfall tracks. The mask modulates roughness most strongly.
            float streak = Noise(float2(p.z * 15.0, p.y * .34)) * weights.x +
                           Noise(p.xz * 5.0) * weights.y +
                           Noise(float2(p.x * 15.0, p.y * .34)) * weights.z;
            float rainTracks = smoothstep(.46, .79, streak) * _StreakStrength;
            float dust = saturate(_DustAmount * (.35 + .9 * grime) + rainTracks * .20);
            float tintVariation = lerp(1.0, .78 + broad * .44, _PaneVariation);

            // Dielectric F0 remains low and neutral. Unity's Standard Specular
            // lighting supplies view-dependent Fresnel and the reflection probe.
            // A glass pane is not metallic; dust reduces its coherent reflection.
            output.Albedo = lerp(_Color.rgb * tintVariation, _DustColor.rgb, dust * .48);
            output.Specular = lerp(_Reflectance, .025, dust).xxx;
            output.Smoothness = saturate(lerp(_Smoothness, _DustSmoothness, dust) -
                                        rainTracks * .08 + (broad - .5) * .055);
            output.Occlusion = 1;
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard (Specular setup)"
}
