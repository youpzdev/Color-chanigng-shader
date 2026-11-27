Shader "Learning/Stencil/PortalMask"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry-10" }

        Pass
        {
            Name "PortalMask"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZTest LEqual
            ZWrite Off
            ColorMask 0

            Stencil
            {
                Ref 2
                ReadMask 2
                WriteMask 2
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
