Shader "Hidden/DoNotDraw/CRTMonitorPostProcess"
{
    Properties
    {
        _Curvature ("Screen Curvature", Range(0, 0.15)) = 0.035
        _ScanlineIntensity ("Scanline Intensity", Range(0, 0.5)) = 0.17
        _ScanlineDensity ("Scanline Density", Range(0.5, 2)) = 1
        _PhosphorMaskIntensity ("Phosphor Mask Intensity", Range(0, 0.5)) = 0.18
        _HorizontalJitter ("Horizontal Jitter", Range(0, 0.004)) = 0.00045
        _FlickerIntensity ("Flicker Intensity", Range(0, 0.1)) = 0.018
        _VignetteIntensity ("CRT Vignette", Range(0, 1)) = 0.2
        _Brightness ("Brightness Compensation", Range(0.5, 1.5)) = 1.035
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "CRT Monitor"

            ZTest Always
            ZWrite Off
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Curvature;
                float _ScanlineIntensity;
                float _ScanlineDensity;
                float _PhosphorMaskIntensity;
                float _HorizontalJitter;
                float _FlickerIntensity;
                float _VignetteIntensity;
                float _Brightness;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 345.45));
                value += dot(value, value + 34.345);
                return frac(value.x * value.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUv = input.texcoord;
                float2 centeredUv = screenUv * 2.0 - 1.0;
                float radiusSquared = dot(centeredUv, centeredUv);
                float2 curvedUv = centeredUv * (1.0 + _Curvature * radiusSquared);
                float2 sampleUv = curvedUv * 0.5 + 0.5;

                float2 sourceSize = max(_BlitTexture_TexelSize.zw, float2(1.0, 1.0));
                float frameIndex = floor(_Time.y * 45.0);
                float lineIndex = floor(sampleUv.y * sourceSize.y * 0.25);
                float lineJitter = Hash21(float2(lineIndex, frameIndex)) * 2.0 - 1.0;

                float sweepPosition = frac(_Time.y * 0.117);
                float sweepBand = exp2(-abs(sampleUv.y - sweepPosition) * 180.0);
                float sweepJitter =
                    (Hash21(float2(frameIndex, 7.13)) * 2.0 - 1.0) * sweepBand * 4.0;
                sampleUv.x += (lineJitter + sweepJitter) * _HorizontalJitter;

                float edgeDistance = min(
                    min(sampleUv.x, 1.0 - sampleUv.x),
                    min(sampleUv.y, 1.0 - sampleUv.y));
                float curvedEdgeMask = smoothstep(0.0, 0.008, edgeDistance);

                half4 source = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    sampleUv,
                    0.0);

                float pixelY = sampleUv.y * sourceSize.y;
                float scanline = 1.0 - _ScanlineIntensity *
                    (0.5 + 0.5 * sin(pixelY * PI * _ScanlineDensity));

                float pixelX = sampleUv.x * sourceSize.x;
                float3 phosphorMask = 1.0 + 0.22 * cos(
                    (pixelX + float3(0.0, 1.0, 2.0)) * 2.0943951);
                phosphorMask = lerp(1.0.xxx, phosphorMask, _PhosphorMaskIntensity);

                float flicker = 1.0 - _FlickerIntensity *
                    (0.5 + 0.5 * sin(_Time.y * 113.0));
                float rollingBand = 1.0 - _FlickerIntensity * 0.55 *
                    exp2(-abs(sampleUv.y - frac(_Time.y * 0.08)) * 18.0);

                float2 vignetteAxes = saturate(sampleUv * (1.0 - sampleUv) * 4.0);
                float crtVignette = pow(
                    max(vignetteAxes.x * vignetteAxes.y, 0.0001),
                    0.18);

                float3 color = source.rgb;
                color *= phosphorMask;
                color *= scanline * flicker * rollingBand * _Brightness;
                color *= lerp(1.0, crtVignette, _VignetteIntensity);
                color *= curvedEdgeMask;

                return half4(color, source.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
