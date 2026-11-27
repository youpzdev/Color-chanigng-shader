Shader "Learning/Stencil/XRayOutline"
{
    Properties
    {
        [HDR] _OutlineColor ("Hidden Outline Color", Color) = (0.1, 0.9, 1.2, 1)
        _OutlineWidth ("Outline Width (pixels)", Range(0, 12)) = 4
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings MaskVertex(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS);

            return output;
        }

        Varyings OutlineVertex(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            float3 normalVS = TransformWorldToViewDir(normalWS);
            float2 projectedNormal = mul((float2x2)UNITY_MATRIX_P, normalVS.xy);
            float2 pixelDirection = projectedNormal * _ScaledScreenParams.xy;
            pixelDirection *= rsqrt(max(dot(pixelDirection, pixelDirection), 0.000001));
            output.positionCS.xy += pixelDirection * (_OutlineWidth * 2 / _ScaledScreenParams.xy) * output.positionCS.w;

            return output;
        }

        half4 MaskFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            return 0;
        }

        half4 OutlineFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            return half4(_OutlineColor.rgb, 1);
        }
        ENDHLSL

        Pass
        {
            Name "SilhouetteMask"
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0

            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "OccludedOutline"
            Cull Front
            ZWrite Off
            ZTest Greater

            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 0
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "ClearSilhouetteMask"
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0

            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp Always
                Pass Zero
            }

            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }

    Fallback Off
}
