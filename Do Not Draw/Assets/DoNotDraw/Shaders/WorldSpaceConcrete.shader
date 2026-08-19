Shader "DoNotDraw/WorldSpaceConcrete"
{
    Properties
    {
        _BaseMap("Concrete Albedo", 2D) = "white" {}
        [Normal] _BumpMap("Concrete Normal", 2D) = "bump" {}
        _BaseColor("Tint", Color) = (0.76, 0.75, 0.72, 1)
        _WorldTiling("World Tiling (Tiles Per Meter)", Range(0.1, 4.0)) = 0.62
        _BlendSharpness("Projection Blend Sharpness", Range(1.0, 16.0)) = 8.0
        _BumpScale("Normal Strength", Range(0.0, 2.0)) = 0.72
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.055
        _OcclusionStrength("Occlusion", Range(0.0, 1.0)) = 1.0

        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _Cutoff("__cutoff", Range(0.0, 1.0)) = 0.5
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _WorldTiling;
                float _BlendSharpness;
                half _BumpScale;
                half _Metallic;
                half _Smoothness;
                half _OcclusionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half3 vertexLighting : TEXCOORD3;
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct TriplanarCoordinates
            {
                float2 x;
                float2 y;
                float2 z;
                float3 weights;
                float3 axisSign;
            };

            TriplanarCoordinates GetTriplanarCoordinates(float3 positionWS, half3 normalWS)
            {
                TriplanarCoordinates coordinates;
                float3 absoluteNormal = max(abs(normalWS), 0.0001);
                coordinates.weights = pow(absoluteNormal, _BlendSharpness);
                coordinates.weights /= max(
                    coordinates.weights.x + coordinates.weights.y + coordinates.weights.z,
                    0.0001);
                coordinates.axisSign = step(0.0, normalWS) * 2.0 - 1.0;

                coordinates.x = float2(
                    -positionWS.z * coordinates.axisSign.x,
                    positionWS.y) * _WorldTiling;
                coordinates.y = float2(
                    positionWS.x,
                    -positionWS.z * coordinates.axisSign.y) * _WorldTiling;
                coordinates.z = float2(
                    positionWS.x * coordinates.axisSign.z,
                    positionWS.y) * _WorldTiling;
                return coordinates;
            }

            half3 SampleTriplanarAlbedo(TriplanarCoordinates coordinates)
            {
                half3 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, coordinates.x).rgb;
                half3 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, coordinates.y).rgb;
                half3 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, coordinates.z).rgb;
                return x * coordinates.weights.x
                    + y * coordinates.weights.y
                    + z * coordinates.weights.z;
            }

            half3 SampleTriplanarNormal(TriplanarCoordinates coordinates)
            {
                half3 x = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, coordinates.x),
                    _BumpScale);
                half3 y = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, coordinates.y),
                    _BumpScale);
                half3 z = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, coordinates.z),
                    _BumpScale);

                half3 worldX = half3(
                    coordinates.axisSign.x * x.z,
                    x.y,
                    -coordinates.axisSign.x * x.x);
                half3 worldY = half3(
                    y.x,
                    coordinates.axisSign.y * y.z,
                    -coordinates.axisSign.y * y.y);
                half3 worldZ = half3(
                    coordinates.axisSign.z * z.x,
                    z.y,
                    coordinates.axisSign.z * z.z);

                return normalize(
                    worldX * coordinates.weights.x
                    + worldY * coordinates.weights.y
                    + worldZ * coordinates.weights.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalWS;
                output.positionHCS = positionInputs.positionCS;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.vertexLighting = VertexLighting(positionInputs.positionWS, normalWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 geometryNormalWS = NormalizeNormalPerPixel(input.normalWS);
                TriplanarCoordinates coordinates = GetTriplanarCoordinates(
                    input.positionWS,
                    geometryNormalWS);
                half3 normalWS = SampleTriplanarNormal(coordinates);
                half3 albedo = SampleTriplanarAlbedo(coordinates) * _BaseColor.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionHCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = input.vertexLighting;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.specular = half3(0.0, 0.0, 0.0);
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0.0, 0.0, 1.0);
                surfaceData.emission = half3(0.0, 0.0, 0.0);
                surfaceData.occlusion = _OcclusionStrength;
                surfaceData.alpha = _BaseColor.a;
                surfaceData.clearCoatMask = 0.0;
                surfaceData.clearCoatSmoothness = 0.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
