Shader "DoNotDraw/ImpossibleWindowVoid"
{
    Properties
    {
        _VoidColor("Void Color", Color) = (0.0015, 0.002, 0.0018, 1)
        _FrameColor("Impossible Frame Color", Color) = (0.055, 0.075, 0.068, 1)
        _Parallax("View Parallax", Range(0, 0.2)) = 0.075
        _Twist("Impossible Twist", Range(0, 2)) = 1.0
        _DriftSpeed("Drift Speed", Range(0, 0.25)) = 0.045
        _FrameVisibility("Frame Visibility", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ImpossibleVoid"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _VoidColor;
                half4 _FrameColor;
                float _Parallax;
                float _Twist;
                float _DriftSpeed;
                float _FrameVisibility;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 viewDirectionOS : TEXCOORD1;
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 Rotate(float2 value, float angle)
            {
                float sineValue;
                float cosineValue;
                sincos(angle, sineValue, cosineValue);
                return float2(
                    cosineValue * value.x - sineValue * value.y,
                    sineValue * value.x + cosineValue * value.y);
            }

            float RectangleFrame(float2 samplePoint, float2 halfSize, float width)
            {
                float2 normalizedPoint = abs(samplePoint) / max(halfSize, 0.0001);
                float edgeDistance = abs(max(normalizedPoint.x, normalizedPoint.y) - 1.0);
                return 1.0 - smoothstep(width, width + 0.014, edgeDistance);
            }

            float Hash(float2 samplePoint)
            {
                return frac(sin(dot(samplePoint, float2(12.9898, 78.233))) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                output.positionHCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.viewDirectionOS = TransformWorldToObjectDir(viewDirectionWS, true);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 windowPoint = input.uv - 0.5;
                windowPoint.x *= 0.72;

                float3 viewDirection = normalize(input.viewDirectionOS);
                float2 viewOffset = viewDirection.xy
                    / max(abs(viewDirection.z), 0.18)
                    * _Parallax;
                float driftTime = _Time.y * _DriftSpeed;

                half3 color = _VoidColor.rgb;
                float edgeVignette = smoothstep(0.72, 0.16, length(windowPoint));
                color *= lerp(0.16, 1.0, edgeVignette);

                [unroll]
                for (int index = 0; index < 10; index++)
                {
                    float layer = index / 9.0;
                    float shrink = pow(0.765, index);
                    float impossibleTurn = (index - 4.5) * 0.031 * _Twist;
                    impossibleTurn += sin(driftTime + index * 1.41) * 0.015 * _Twist;

                    float2 wanderingCenter = viewOffset * layer;
                    wanderingCenter += float2(
                        sin(index * 1.93 + driftTime),
                        cos(index * 1.37 - driftTime * 0.73))
                        * (0.011 * layer * _Twist);

                    float2 layerPoint = Rotate(windowPoint - wanderingCenter, impossibleTurn);
                    float2 halfSize = float2(0.335, 0.465) * shrink;
                    float frame = RectangleFrame(
                        layerPoint,
                        halfSize,
                        lerp(0.012, 0.058, layer));
                    float layerBrightness = lerp(0.92, 0.16, layer);
                    color += _FrameColor.rgb
                        * frame
                        * layerBrightness
                        * _FrameVisibility;
                }

                float2 falseCenter = viewOffset * 0.72
                    + float2(sin(driftTime * 0.61), cos(driftTime * 0.47)) * 0.008;
                float deepDarkness = exp(-length(windowPoint - falseCenter) * 15.0);
                color *= lerp(1.0, 0.12, deepDarkness);

                float grain = Hash(floor(input.uv * 420.0) + floor(_Time.y * 3.0));
                color += (grain - 0.5) * 0.0015;
                return half4(max(color, 0.0), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
