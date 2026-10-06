Shader "Custom/URP/TerrainAntiTiling"
{
    Properties
    {
        [MainTexture] _MainTex ("Terrain Texture", 2D) = "white" {}

        _Tiling ("Texture Tiling", Float) = 0.1
        _VariationScale ("Variation Scale", Range(0, 1)) = 0.15
        _NoiseScale ("Variation Noise Scale", Float) = 0.08
        _BlendSharpness ("Blend Sharpness", Range(0.1, 10)) = 2
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
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            // URP lighting
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN

            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS

            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)

                float4 _MainTex_ST;

                float _Tiling;
                float _VariationScale;
                float _NoiseScale;
                float _BlendSharpness;

            CBUFFER_END


            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;

                float2 uv : TEXCOORD2;

                float4 shadowCoord : TEXCOORD3;

                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);

                half fogFactor : TEXCOORD5;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };


            // ------------------------------------------------------------
            // Hash
            // ------------------------------------------------------------

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);

                return frac(p.x * p.y);
            }


            // ------------------------------------------------------------
            // Smooth procedural noise
            // ------------------------------------------------------------

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                return lerp(
                    lerp(a, b, f.x),
                    lerp(c, d, f.x),
                    f.y
                );
            }


            // ------------------------------------------------------------
            // Vertex
            // ------------------------------------------------------------

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                output.shadowCoord =
                    GetShadowCoord(positionInputs);

                output.fogFactor =
                    ComputeFogFactor(positionInputs.positionCS.z);

                OUTPUT_LIGHTMAP_UV(
                    input.uv,
                    unity_LightmapST,
                    output.lightmapUV
                );

                OUTPUT_SH(output.normalWS, output.vertexSH);

                return output;
            }


            // ------------------------------------------------------------
            // Fragment
            // ------------------------------------------------------------

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = input.positionWS;

                // --------------------------------------------------------
                // World-space terrain UV
                // --------------------------------------------------------

                float2 uv = positionWS.xz * _Tiling;


                // --------------------------------------------------------
                // First texture sample
                // --------------------------------------------------------

                float4 texA =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        uv
                    );


                // --------------------------------------------------------
                // Second version of the same texture
                // --------------------------------------------------------

                float2 uvB =
                    uv * (1.0 + _VariationScale);

                uvB += float2(17.31, 43.17);

                float4 texB =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        uvB
                    );


                // --------------------------------------------------------
                // Large scale noise controls the blend
                // --------------------------------------------------------

                float noise =
                    Noise(uv * _NoiseScale);

                noise = smoothstep(
                    0.25,
                    0.75,
                    noise
                );

                noise = pow(
                    noise,
                    _BlendSharpness
                );


                // --------------------------------------------------------
                // Anti-tiling texture
                // --------------------------------------------------------

                float3 albedo =
                    lerp(
                        texA.rgb,
                        texB.rgb,
                        noise
                    );


                // --------------------------------------------------------
                // Lighting
                // --------------------------------------------------------

                float3 normalWS =
                    normalize(input.normalWS);

                float3 viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(positionWS);


                // Baked GI / light probes
                float3 bakedGI =
                    SAMPLE_GI(
                        input.lightmapUV,
                        input.vertexSH,
                        normalWS
                    );


                // Main directional light
                Light mainLight =
                    GetMainLight(input.shadowCoord);

                float3 directLighting =
                    LightingLambert(
                        mainLight.color * mainLight.distanceAttenuation *
                        mainLight.shadowAttenuation,
                        mainLight.direction,
                        normalWS
                    );


                // Ambient + main light
                float3 color =
                    albedo *
                    (bakedGI + directLighting);


                // --------------------------------------------------------
                // Additional lights
                // --------------------------------------------------------

                #ifdef _ADDITIONAL_LIGHTS

                uint pixelLightCount =
                    GetAdditionalLightsCount();

                for (uint i = 0; i < pixelLightCount; i++)
                {
                    Light light =
                        GetAdditionalLight(
                            i,
                            positionWS
                        );

                    float3 additional =
                        LightingLambert(
                            light.color *
                            light.distanceAttenuation *
                            light.shadowAttenuation,
                            light.direction,
                            normalWS
                        );

                    color += albedo * additional;
                }

                #endif


                // --------------------------------------------------------
                // Fog
                // --------------------------------------------------------

                color =
                    MixFog(
                        color,
                        input.fogFactor
                    );


                return half4(color, 1);
            }

            ENDHLSL
        }


        // ------------------------------------------------------------
        // Shadow caster
        // ------------------------------------------------------------

        Pass
        {
            Name "ShadowCaster"

            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            ENDHLSL
        }


        // ------------------------------------------------------------
        // Depth
        // ------------------------------------------------------------

        Pass
        {
            Name "DepthOnly"

            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

            ENDHLSL
        }
    }
}