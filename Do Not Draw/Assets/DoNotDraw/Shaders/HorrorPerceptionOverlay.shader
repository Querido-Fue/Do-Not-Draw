Shader "DoNotDraw/HorrorPerceptionOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex("UI Texture", 2D) = "white" {}
        _FaceTex("Unaltered Face", 2D) = "black" {}
        _FaceAlpha("Face Alpha", Range(0, 1)) = 0
        _FaceCenter("Face Center", Vector) = (0.5, 0.5, 0, 0)
        _FaceSize("Face Size", Vector) = (0.22, 0.32, 0, 0)
        _Chromatic("Chromatic Separation", Range(0, 1.5)) = 0
        _GlitchStrength("Glitch Strength", Range(0, 1)) = 0
        _VignetteIntensity("Blood Vignette", Range(0, 1)) = 0
        _RedPulse("Red Pulse", Range(0, 1)) = 0

        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            Name "Horror Perception"

            Cull Off
            ZWrite Off
            ZTest [unity_GUIZTestMode]
            Blend One OneMinusSrcAlpha
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FaceTex);
            SAMPLER(sampler_FaceTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FaceTex_ST;
                float4 _FaceCenter;
                float4 _FaceSize;
                half _FaceAlpha;
                half _Chromatic;
                half _GlitchStrength;
                half _VignetteIntensity;
                half _RedPulse;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 345.45));
                value += dot(value, value + 34.345);
                return frac(value.x * value.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUv = input.uv;
                float2 faceSize = max(_FaceSize.xy, float2(0.001, 0.001));
                float2 faceUv = (screenUv - _FaceCenter.xy) / faceSize + 0.5;

                float frameIndex = floor(_Time.y * 73.0);
                float sliceIndex = floor(faceUv.y * 47.0);
                float sliceNoise = Hash21(float2(sliceIndex, frameIndex));
                float tearGate = step(0.72, Hash21(float2(sliceIndex * 0.37, frameIndex * 0.19)));
                faceUv.x += (sliceNoise * 2.0 - 1.0)
                    * tearGate
                    * _GlitchStrength
                    * 0.085;

                float inside = step(0.0, faceUv.x)
                    * step(faceUv.x, 1.0)
                    * step(0.0, faceUv.y)
                    * step(faceUv.y, 1.0);
                float chromaticOffset = _Chromatic * 0.012;
                float2 redUv = faceUv + float2(chromaticOffset, 0.0);
                float2 blueUv = faceUv - float2(chromaticOffset, 0.0);
                half3 faceColor;
                faceColor.r = SAMPLE_TEXTURE2D(_FaceTex, sampler_FaceTex, redUv).r;
                faceColor.g = SAMPLE_TEXTURE2D(_FaceTex, sampler_FaceTex, faceUv).g;
                faceColor.b = SAMPLE_TEXTURE2D(_FaceTex, sampler_FaceTex, blueUv).b;

                half luminance = max(faceColor.r, max(faceColor.g, faceColor.b));
                half faceMask = inside
                    * smoothstep(0.018, 0.16, luminance)
                    * _FaceAlpha;

                float2 centered = screenUv * 2.0 - 1.0;
                centered.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float radius = length(centered);
                float edge = smoothstep(0.42, 1.22, radius);
                float angularVein = 0.5 + 0.5 * sin(
                    atan2(centered.y, centered.x) * 13.0
                    + radius * 31.0
                    - _Time.y * 1.7);
                angularVein = smoothstep(0.72, 1.0, angularVein);
                float redBreath = lerp(0.78, 1.18, _RedPulse);
                half vignetteAlpha = saturate(
                    edge
                    * _VignetteIntensity
                    * redBreath
                    + angularVein * edge * _VignetteIntensity * 0.09);

                float scanline = 0.5 + 0.5 * sin(screenUv.y * _ScreenParams.y * PI);
                half scanAlpha = scanline * _GlitchStrength * 0.085;
                float noise = Hash21(floor(screenUv * _ScreenParams.xy * 0.33) + frameIndex);
                half noiseAlpha = step(0.985 - _GlitchStrength * 0.04, noise)
                    * _GlitchStrength
                    * 0.16;

                half3 bloodColor = lerp(
                    half3(0.006, 0.0, 0.001),
                    half3(0.13, 0.002, 0.006),
                    saturate(edge + _RedPulse * 0.25));
                half overlayAlpha = saturate(max(faceMask, vignetteAlpha) + scanAlpha + noiseAlpha);
                half3 premultiplied = faceColor * faceMask;
                premultiplied += bloodColor * vignetteAlpha;
                premultiplied += half3(0.12, 0.004, 0.008) * noiseAlpha;
                return half4(premultiplied, overlayAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
