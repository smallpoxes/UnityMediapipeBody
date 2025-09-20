Shader "Custom/DistanceMetallicBodyPart"
{
    Properties
    {
        [Header(Base Settings)]
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseMap ("Base Map", 2D) = "white" {}

        [Header(Metallic Settings)]
        _NearMetallic ("Near Metallic", Range(0, 1)) = 1.0
        _FarMetallic ("Far Metallic", Range(0, 1)) = 0.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _SmoothnessMap ("Smoothness Map", 2D) = "white" {}

        // ★ プロパティを追加
        [Header(Alpha Settings)]
        _NearAlpha ("Near Alpha", Range(0, 1)) = 1.0
        _FarAlpha ("Far Alpha", Range(0, 1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ LIGHTMAP_ON
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _NearMetallic;
                float _FarMetallic;
                float _Smoothness;
                // ★ シェーダー変数を追加
                float _NearAlpha;
                float _FarAlpha;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerFrame)
                float _BodyMinDistance;
                float _BodyMaxDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                float2 lightmapUV   : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float2 lightmapUV   : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.lightmapUV = input.lightmapUV;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float currentPixelDistance = distance(input.positionWS, _WorldSpaceCameraPos);
                float distanceRange = _BodyMaxDistance - _BodyMinDistance;
                float t = (distanceRange > 0.001) ? saturate((currentPixelDistance - _BodyMinDistance) / distanceRange) : 0.0;
                
                float customMetallic = lerp(_NearMetallic, _FarMetallic, t);
                // ★ 距離に応じたアルファ値を計算
                float customAlpha = lerp(_NearAlpha, _FarAlpha, t);

                half4 albedoMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = albedoMap.rgb * _BaseColor.rgb;
                half baseAlpha = albedoMap.a * _BaseColor.a;

                SurfaceData surfaceData;
                ZERO_INITIALIZE(SurfaceData, surfaceData);
                surfaceData.albedo = albedo;
                surfaceData.metallic = customMetallic;
                surfaceData.smoothness = _Smoothness;
                // ★ 計算したカスタムアルファを適用
                surfaceData.alpha = baseAlpha * customAlpha;
                surfaceData.occlusion = 1.0;

                InputData inputData;
                ZERO_INITIALIZE(InputData, inputData);
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = ComputeFogFactor(input.positionCS.z);

                #if defined(LIGHTMAP_ON)
                    inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.positionWS, inputData.normalWS);
                #else
                    inputData.bakedGI = SampleSH(inputData.normalWS);
                #endif

                half4 finalColor = UniversalFragmentPBR(inputData, surfaceData);
                finalColor.rgb = MixFog(finalColor.rgb, inputData.fogCoord);

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}