
Shader "Hidden/Outbreak/Cinematic City"
{
    Properties { _MainTex("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BloomTex;
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        float4 _MainTex_TexelSize, _Grade, _BloomSettings, _HazeSettings, _HazeColour;
        float4x4 _InverseProjection, _CityViewToWorld;
        float3 _CameraPosition;
        float _EdgeThreshold;

        float3 WorkingLinear(float3 colour)
        {
            #if defined(UNITY_COLORSPACE_GAMMA)
                return GammaToLinearSpace(max(colour, 0));
            #else
                return max(colour, 0);
            #endif
        }

        float3 OutputColour(float3 colour)
        {
            #if defined(UNITY_COLORSPACE_GAMMA)
                return LinearToGammaSpace(max(colour, 0));
            #else
                return max(colour, 0);
            #endif
        }

        half4 Prefilter(v2f_img i) : SV_Target
        {
            // All bloom intermediates stay linear, including in a Gamma project.
            float2 d = abs(_MainTex_TexelSize.xy) * .5;
            float3 c = WorkingLinear(tex2D(_MainTex, i.uv + float2(-d.x, -d.y)).rgb);
            c += WorkingLinear(tex2D(_MainTex, i.uv + float2(d.x, -d.y)).rgb);
            c += WorkingLinear(tex2D(_MainTex, i.uv + float2(-d.x, d.y)).rgb);
            c += WorkingLinear(tex2D(_MainTex, i.uv + d).rgb);
            c = min(c * .25, 64);
            float peak = max(c.r, max(c.g, c.b));
            float knee = max(.0001, _BloomSettings.x * _BloomSettings.y);
            float soft = clamp(peak - _BloomSettings.x + knee, 0, knee * 2);
            soft = soft * soft / (4 * knee + .0001);
            c *= max(soft, peak - _BloomSettings.x) / max(peak, .0001);
            return half4(c, 1);
        }

        half4 Gaussian(float2 uv, float2 direction)
        {
            float3 c = tex2D(_MainTex, uv).rgb * .227027;
            c += tex2D(_MainTex, uv + direction * 1.384615).rgb * .316216;
            c += tex2D(_MainTex, uv - direction * 1.384615).rgb * .316216;
            c += tex2D(_MainTex, uv + direction * 3.230769).rgb * .070270;
            c += tex2D(_MainTex, uv - direction * 3.230769).rgb * .070270;
            return half4(c, 1);
        }
        half4 BlurHorizontal(v2f_img i) : SV_Target { return Gaussian(i.uv, float2(abs(_MainTex_TexelSize.x) * 1.5, 0)); }
        half4 BlurVertical(v2f_img i) : SV_Target { return Gaussian(i.uv, float2(0, abs(_MainTex_TexelSize.y) * 1.5)); }

        float3 Filmic(float3 x)
        {
            // A fitted filmic curve rolls off HDR highlights without a hard clamp.
            x = max(0, x);
            return saturate((x * (2.51 * x + .03)) / (x * (2.43 * x + .59) + .14));
        }

        half4 Composite(v2f_img i) : SV_Target
        {
            float4 original = tex2D(_MainTex, i.uv);
            float3 c = WorkingLinear(original.rgb);
            float2 depthUV = i.uv;
            #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) depthUV.y = 1 - depthUV.y;
            #endif
            float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, depthUV);
            float depth01 = Linear01Depth(rawDepth);
            if (depth01 < .9999 && _HazeSettings.x > 0)
            {
                float4 ray = mul(_InverseProjection, float4(depthUV * 2 - 1, 1, 1));
                float3 viewRay = ray.xyz / max(abs(ray.z), .0001);
                float eyeDepth = LinearEyeDepth(rawDepth);
                float3 viewPosition = viewRay * eyeDepth;
                float3 worldPosition = mul(_CityViewToWorld, float4(viewPosition, 1)).xyz;
                float distanceToPoint = length(viewPosition);
                float midpointHeight = (_CameraPosition.y + worldPosition.y) * .5;
                float density0 = exp(-max(0, _CameraPosition.y - _HazeSettings.y) * _HazeSettings.z);
                float density1 = exp(-max(0, worldPosition.y - _HazeSettings.y) * _HazeSettings.z);
                float densityM = exp(-max(0, midpointHeight - _HazeSettings.y) * _HazeSettings.z);
                float heightDensity = (density0 + density1 + 2 * densityM) * .25;
                float haze = min(_HazeSettings.w, 1 - exp(-distanceToPoint * _HazeSettings.x * heightDensity));
                c = lerp(c, _HazeColour.rgb, haze);
            }
            c = (c + tex2D(_BloomTex, i.uv).rgb * _BloomSettings.z) * _Grade.x;
            float luma = dot(c, float3(.2126, .7152, .0722));
            c = lerp(luma.xxx, c, _Grade.y);
            c = max(0, (c - .18) * _Grade.z + .18);
            c = Filmic(c);
            float2 p = i.uv * 2 - 1;
            c *= 1 - _Grade.w * smoothstep(.4, 1.7, dot(p, p));
            return half4(OutputColour(c), original.a);
        }

        float PerceptualLuma(float3 c)
        {
            #if !defined(UNITY_COLORSPACE_GAMMA)
                c = sqrt(max(c, 0));
            #endif
            return dot(c, float3(.299, .587, .114));
        }

        half4 Antialias(v2f_img i) : SV_Target
        {
            float2 stepUV = abs(_MainTex_TexelSize.xy);
            float4 centre = tex2D(_MainTex, i.uv);
            float3 nw = tex2D(_MainTex, i.uv + float2(-1, -1) * stepUV).rgb;
            float3 ne = tex2D(_MainTex, i.uv + float2(1, -1) * stepUV).rgb;
            float3 sw = tex2D(_MainTex, i.uv + float2(-1, 1) * stepUV).rgb;
            float3 se = tex2D(_MainTex, i.uv + stepUV).rgb;
            float lnw = PerceptualLuma(nw), lne = PerceptualLuma(ne);
            float lsw = PerceptualLuma(sw), lse = PerceptualLuma(se), lc = PerceptualLuma(centre.rgb);
            float low = min(lc, min(min(lnw, lne), min(lsw, lse)));
            float high = max(lc, max(max(lnw, lne), max(lsw, lse)));
            if (high - low < max(.025, high * _EdgeThreshold)) return centre;
            float2 direction = float2(-((lnw + lne) - (lsw + lse)), (lnw + lsw) - (lne + lse));
            float reduction = max((lnw + lne + lsw + lse) * .03125, .0078125);
            float inverseMinimum = 1 / (min(abs(direction.x), abs(direction.y)) + reduction);
            direction = clamp(direction * inverseMinimum, -6, 6) * stepUV;
            float3 a = .5 * (tex2D(_MainTex, i.uv + direction * (-1.0 / 6.0)).rgb + tex2D(_MainTex, i.uv + direction * (1.0 / 6.0)).rgb);
            float3 b = a * .5 + .25 * (tex2D(_MainTex, i.uv - direction * .5).rgb + tex2D(_MainTex, i.uv + direction * .5).rgb);
            float lb = PerceptualLuma(b);
            return half4(lb < low || lb > high ? a : b, centre.a);
        }
        ENDCG
        Pass { CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Prefilter
        ENDCG }
        Pass { CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment BlurHorizontal
        ENDCG }
        Pass { CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment BlurVertical
        ENDCG }
        Pass { CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Composite
        ENDCG }
        Pass { CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Antialias
        ENDCG }
    }
    Fallback Off
}
