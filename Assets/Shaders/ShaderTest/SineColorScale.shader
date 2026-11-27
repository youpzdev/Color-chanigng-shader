Shader "Learning/SineColorScale"
{
    Properties
    {
        _ColorMin ("First Color", Color) = (0.62, 0.10, 1, 1)
        _ColorMax ("Second Color", Color) = (0, 0.95, 0.80, 1)
        _ColorSpeed ("Color Speed (rad/s)", Float) = 2
        _MinScale ("Min Scale", Range(0.05, 3)) = 0.8
        _MaxScale ("Max Scale", Range(0.05, 3)) = 1.2
        _ScaleSpeed ("Scale Speed (rad/s)", Float) = 3
        _Smoothness ("Smoothness", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _ColorMin;
            half4 _ColorMax;
            float _ColorSpeed;
            float _MinScale;
            float _MaxScale;
            float _ScaleSpeed;
            half _Smoothness;
        CBUFFER_END
        struct Attributes
        {
            float3 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };
        float Sine01(float speed) { return sin(_Time.y * speed) * 0.5 + 0.5; }
        float3 PulsePosition(float3 positionOS)
        {
            float scale = max(lerp(_MinScale, _MaxScale, Sine01(_ScaleSpeed)), 0.001);

            return positionOS * scale;
        }
        ENDHLSL
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(PulsePosition(input.positionOS));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = NormalizeNormalPerPixel(input.normalWS);
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI = SampleSH(lighting.normalWS);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1, 1, 1, 1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(_ColorMin.rgb, _ColorMax.rgb, Sine01(_ColorSpeed));
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;
                surface.alpha = 1;

                return UniversalFragmentPBR(lighting, surface);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            float4 shadowVert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(PulsePosition(input.positionOS));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - positionWS);
                #else
                    float3 direction = _LightDirection;
                #endif
                float4 clip = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, direction));

                return ApplyShadowClamping(clip);
            }
            half4 shadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            ZWrite On
            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            float4 depthVert(Attributes input) : SV_POSITION
            {
                return TransformObjectToHClip(PulsePosition(input.positionOS));
            }
            half4 depthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex normalVert
            #pragma fragment normalFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            struct NormalVaryings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            NormalVaryings normalVert(Attributes input)
            {
                NormalVaryings output;
                output.positionCS = TransformObjectToHClip(PulsePosition(input.positionOS));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                return output;
            }
            half4 normalFrag(NormalVaryings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
