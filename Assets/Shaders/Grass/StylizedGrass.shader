Shader "Custom/URP/StylizedGrass"
{
    Properties
    {
        [Header(Color)]
        _RootColor ("Root Color", Color) = (0.09, 0.30, 0.07, 1)
        _TipColor ("Tip Color", Color) = (0.62, 0.78, 0.22, 1)
        _GradientPower ("Gradient Power", Range(0.3, 3)) = 1.2
        _HueVariation ("Hue Variation", Range(0, 1)) = 0.3

        [Header(Lighting)]
        _ShadowAmbient ("Min Ambient Light (shadow floor)", Color) = (0.50, 0.58, 0.72, 1)
        _Translucency ("Translucency", Range(0, 1)) = 0.35
        _ShadowReceiveBias ("Shadow Receive Bias (m)", Range(0, 0.1)) = 0.01

        [Header(Wind)]
        _WindDirection ("Wind Direction (world X, Z in XY fields)", Vector) = (1, 0.4, 0, 0)
        _WindStrength ("Wind Strength (tip offset, m)", Range(0, 1)) = 0.12
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.2
        _WindScale ("Wind Wave Scale (rad/m)", Range(0.05, 4)) = 0.9
        _BladeHeight ("Approx Blade Height (m)", Range(0.05, 2)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
            float4 _RootColor;
            float4 _TipColor;
            float4 _ShadowAmbient;
            float4 _WindDirection;
            float _GradientPower;
            float _HueVariation;
            float _Translucency;
            float _ShadowReceiveBias;
            float _WindStrength;
            float _WindSpeed;
            float _WindScale;
            float _BladeHeight;
        CBUFFER_END

        struct GrassAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv         : TEXCOORD0;
            float4 color      : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

void GrassDeform(inout float3 positionWS, inout float3 normalWS, float height01)
        {
            float h = saturate(height01);
            float bend = h * h;                       

            float2 dir = _WindDirection.xy;
            float dirLen = length(dir);
            dir = dirLen > 1e-4 ? dir / dirLen : float2(1.0, 0.0);
            float2 perp = float2(-dir.y, dir.x);

            float2 p = positionWS.xz;                 
            float along  = dot(p, dir);
            float across = dot(p, perp);
            float k = _WindScale;
            float t = _Time.y * _WindSpeed;

float gust = 0.5 + 0.5 * sin(along * k * 0.35 + across * k * 0.11 - t * 0.4 + 1.3);

float phaseMain = along * k - t + 1.3 * sin(across * k * 0.63 + t * 0.27);
            float lean = (0.55 + 0.45 * sin(phaseMain)) * lerp(0.55, 1.0, gust);

float flutterAlong = 0.18 * sin(along * k * 2.3 + across * k * 1.7 - t * 2.7 + 2.1);
            float flutterSide  = 0.25 * sin(across * k * 1.9 + along * k * 0.8 - t * 1.9 + 0.7);

            float2 offFull = (dir * (lean + flutterAlong) + perp * flutterSide) * _WindStrength;
            float2 off = offFull * bend;

            positionWS.xz += off;
            
            float H = max(_BladeHeight, 1e-3);
            positionWS.y -= dot(off, off) / (2.0 * H);

float lenFull = length(offFull);
            float2 leanDir = offFull / max(lenFull, 1e-5);
            float tanA = lenFull * 2.0 * h / H;
            float sinA = tanA * rsqrt(1.0 + tanA * tanA);
            normalWS.y -= dot(normalWS.xz, leanDir) * sinA;
            normalWS = normalize(normalWS);
        }

float3 GrassShadeNormal(float3 n)
        {
            return normalize(lerp(n, float3(0.0, 1.0, 0.0), 0.45));
        }
        ENDHLSL

Pass
        {
            Name "ForwardLit"
            
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 grass      : TEXCOORD2; 
                float  fogFactor  : TEXCOORD3;
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord : TEXCOORD4;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(GrassAttributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                GrassDeform(positionWS, normalWS, v.uv.y);

                o.positionWS = positionWS;
                o.normalWS = normalWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.grass = float3(v.uv.x, v.uv.y, v.color.r);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                o.shadowCoord = ComputeScreenPos(o.positionCS);
            #endif
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float h = saturate(i.grass.y);

float gradT = pow(h, _GradientPower);
                float3 albedo = lerp(_RootColor.rgb, _TipColor.rgb, gradT);

float patch = sin(i.positionWS.x * 0.31 + 1.7) * sin(i.positionWS.z * 0.27 + 0.4);
                float v = clamp(((i.grass.z - 0.5) * 2.0 + patch * 0.35) * _HueVariation, -1.0, 1.0);
                float3 warm = float3(1.18, 1.04, 0.70);
                float3 cool = float3(0.82, 1.02, 1.12);
                albedo *= lerp(1.0.xxx, warm, saturate(v)) * lerp(1.0.xxx, cool, saturate(-v));
                albedo *= 1.0 + v * 0.12;

float sgn = IS_FRONT_VFACE(facing, 1.0, -1.0);
                float3 n = normalize(i.normalWS) * sgn;
                float3 nShade = GrassShadeNormal(n);

#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord = i.shadowCoord;
            #else
                float3 shadowPosWS = i.positionWS + _MainLightPosition.xyz * _ShadowReceiveBias;
                float4 shadowCoord = TransformWorldToShadowCoord(shadowPosWS);
            #endif
                Light mainLight = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));
                float atten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

float ndl = dot(nShade, mainLight.direction);
                float wrap = saturate((ndl + 0.4) / 1.4);
                wrap = wrap * wrap * (3.0 - 2.0 * wrap);

float3 ambient = max(SampleSH(nShade), _ShadowAmbient.rgb);

float edge = abs(i.grass.x * 2.0 - 1.0);
                float ao = lerp(0.72, 1.0, smoothstep(0.0, 0.55, h)) * (1.0 - 0.15 * edge * edge);

                float3 col = albedo * (mainLight.color * (wrap * atten) + ambient) * ao;

float3 viewDirWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float backlit = pow(saturate(dot(viewDirWS, -mainLight.direction)), 4.0);
                float tipMask = smoothstep(0.2, 1.0, h);
                col += albedo * mainLight.color * atten * (backlit * tipMask * _Translucency * 1.2);

                col = MixFog(col, i.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertShadow
            #pragma fragment fragShadow

            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 GrassShadowPositionCS(float3 positionWS, float3 normalWS)
            {
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                
                normalWS = dot(normalWS, lightDirectionWS) < 0.0 ? -normalWS : normalWS;

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            positionCS = ApplyShadowClamping(positionCS);
                return positionCS;
            }

            ShadowVaryings vertShadow(GrassAttributes v)
            {
                ShadowVaryings o = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                GrassDeform(positionWS, normalWS, v.uv.y);

                o.positionCS = GrassShadowPositionCS(positionWS, normalWS);
                return o;
            }

            half4 fragShadow(ShadowVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return 0;
            }
            ENDHLSL
        }

Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings vertDepth(GrassAttributes v)
            {
                DepthVaryings o = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                GrassDeform(positionWS, normalWS, v.uv.y);

                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }

            float fragDepth(DepthVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return i.positionCS.z;
            }
            ENDHLSL
        }

Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DNVaryings vertDN(GrassAttributes v)
            {
                DNVaryings o = (DNVaryings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                GrassDeform(positionWS, normalWS, v.uv.y);

                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = normalWS;
                return o;
            }

            half4 fragDN(DNVaryings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float sgn = IS_FRONT_VFACE(facing, 1.0, -1.0);
                float3 n = GrassShadeNormal(normalize(i.normalWS) * sgn);

            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octN = PackNormalOctQuadEncode(n);
                float2 remapped = saturate(octN * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
            #else
                return half4(n, 0.0);
            #endif
            }
            ENDHLSL
        }
    }
}
