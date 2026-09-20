Shader "Eric/URP_AdditiveFlow"
{
    Properties
    {
        [MainTexture] _MainTex("Main Texture", 2D) = "white" {}
        [HDR] _TintColor("Tint Color", Color) = (1, 1, 1, 1)
        _FlowSpeed("MainTex Flow Speed", Vector) = (0, 0, 0, 0)
        _Brightness("Brightness Boost", Float) = 1.0
        
        [Header(Soft Particles)]
        [Toggle(_SOFTPARTICLES_ON)] _SoftParticlesEnabled("Enable Soft Particles", Float) = 0
        _SoftParticlesFadeDistance("Fade Distance", Range(0.01, 10.0)) = 1.0

        [Header(UV Distortion)]
        _DistortionMap("Distortion Map", 2D) = "white" {}
        _DistortionPower("Distortion Power", Range(0, 1)) = 0
        _DistortionSpeed("Distortion Speed", Vector) = (0, 0, 0, 0)

        [Header(Fresnel Rim Light)]
        [Toggle(_FRESNEL_ON)] _UseFresnel("Enable Fresnel", Float) = 0
        [Enum(Normal Based, 0, UV Based, 1)] _FresnelType("Fresnel Type", Float) = 0
        [HDR] _FresnelColor("Fresnel Color", Color) = (1, 1, 0, 1)
        _FresnelIntensity("Fresnel Intensity", Float) = 5.0
        _FresnelWidth("Fresnel Width", Range(0.1, 15.0)) = 5.0
        [Toggle(_FRESNEL_MASK_ON)] _FresnelMasksTexture("Use Fresnel as Texture Mask", Float) = 0

        [Header(Dissolve)]
        [Toggle(_USEPARTICLEALPHADISSOLVE_ON)] _UseAlphaDissolve("Use Alpha as Dissolve", Float) = 0
        _DissolveTex("Dissolve Texture", 2D) = "white" {}
        _DissolveAmount("Dissolve Amount", Range(0, 1)) = 0.0
        _VertexAlphaRef("Alpha Ref", Range(0.01, 1.0)) = 1.0
        [HDR] _DissolveEdgeColor("Dissolve Edge Color", Color) = (1, 0, 0, 1)
        _DissolveEdgeWidth("Dissolve Edge Width", Range(0, 0.2)) = 0.05

        [Header(Overlay)]
        _OverlayTex("Overlay Texture", 2D) = "white" {}
        [HDR] _OverlayColor("Overlay Color", Color) = (1, 1, 1, 1)
        _OverlayFlowSpeed("Overlay Flow Speed", Vector) = (0, 0, 0, 0)

        [Header(Vertex Distort)]
        [Toggle(_VERTEX_DISTORT_ON)] _UseVertexDistort("Enable Vertex Distort", Float) = 0
        _DistortStrength("Vertex Strength", Range(0, 0.5)) = 0.0
        _DistortSpeed("Vertex Speed", Float) = 1.0
        _DistortFrequency("Vertex Frequency", Float) = 2.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "PreviewType"="Plane" }
        Cull Off ZWrite Off Blend One One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _SOFTPARTICLES_ON
            #pragma shader_feature _FRESNEL_ON
            #pragma shader_feature _FRESNEL_MASK_ON
            #pragma shader_feature _USEPARTICLEALPHADISSOLVE_ON
            #pragma shader_feature _VERTEX_DISTORT_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screenPos : TEXCOORD1; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD3; float3 viewDirWS : TEXCOORD4; float4 color : COLOR; };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _DissolveTex_ST, _DistortionMap_ST, _OverlayTex_ST;
                float4 _TintColor, _FlowSpeed, _DistortionSpeed, _FresnelColor, _DissolveEdgeColor, _OverlayColor, _OverlayFlowSpeed;
                float _Brightness, _FresnelWidth, _FresnelIntensity, _DistortStrength, _DistortSpeed, _DistortFrequency, _DistortionPower, _DissolveAmount, _VertexAlphaRef, _DissolveEdgeWidth, _FresnelType, _SoftParticlesFadeDistance;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_DissolveTex); SAMPLER(sampler_DissolveTex);
            TEXTURE2D(_DistortionMap); SAMPLER(sampler_DistortionMap);
            TEXTURE2D(_OverlayTex); SAMPLER(sampler_OverlayTex);

            Varyings vert(Attributes input) {
                Varyings output;
                float3 wPos = TransformObjectToWorld(input.positionOS.xyz);
                #ifdef _VERTEX_DISTORT_ON
                    input.positionOS.xyz += input.normalOS * sin(_Time.y * _DistortSpeed + (wPos.x + wPos.y + wPos.z) * _DistortFrequency) * _DistortStrength;
                #endif
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv; 
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(wPos);
                return output;
            }

            float4 frag(Varyings input) : SV_Target {
                // --- 自動偵測相機模式的 Soft Particles 計算 ---
                float softFade = 1.0;
                #ifdef _SOFTPARTICLES_ON
                    float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);
                    float rawDepth = SampleSceneDepth(screenUV);
                    
                    float sceneZ, partZ;
                    if (IsPerspectiveProjection()) {
                        // 透視相機邏輯
                        sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);
                        partZ = input.screenPos.w;
                    } else {
                        // 正交相機邏輯：還原線性深度
                        #if UNITY_REVERSED_Z
                            sceneZ = _ProjectionParams.y + (1.0 - rawDepth) * (_ProjectionParams.z - _ProjectionParams.y);
                        #else
                            sceneZ = _ProjectionParams.y + rawDepth * (_ProjectionParams.z - _ProjectionParams.y);
                        #endif
                        // 在正交模式下，ClipSpace 的 W 永遠是 1，需使用 Z 取代
                        partZ = input.positionCS.z / input.positionCS.w; 
                        // 將 0-1 的 Z 轉回線性距離
                        #if UNITY_REVERSED_Z
                            partZ = _ProjectionParams.y + (1.0 - partZ) * (_ProjectionParams.z - _ProjectionParams.y);
                        #else
                            partZ = _ProjectionParams.y + partZ * (_ProjectionParams.z - _ProjectionParams.y);
                        #endif
                    }
                    
                    softFade = saturate((sceneZ - partZ) / max(0.01, _SoftParticlesFadeDistance));
                #endif

                // UV 扭曲
                float2 uvOff = (SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, input.uv * _DistortionMap_ST.xy + _DistortionMap_ST.zw + _DistortionSpeed.xy * _Time.y).rg * 2.0 - 1.0) * _DistortionPower;
                
                // 貼圖與 Overlay
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv * _MainTex_ST.xy + _MainTex_ST.zw + _FlowSpeed.xy * _Time.y + uvOff);
                float4 over = SAMPLE_TEXTURE2D(_OverlayTex, sampler_OverlayTex, input.uv * _OverlayTex_ST.xy + _OverlayTex_ST.zw + _OverlayFlowSpeed.xy * _Time.y + uvOff) * _OverlayColor;

                // 溶解與頂點 Alpha 連動
                float dAmt = _DissolveAmount;
                #ifdef _USEPARTICLEALPHADISSOLVE_ON
                    dAmt = saturate(_DissolveAmount + (1.0 - saturate(input.color.a / max(0.001, _VertexAlphaRef))));
                #endif
                
                float dVal = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, input.uv * _DissolveTex_ST.xy + _DissolveTex_ST.zw).r;
                float bAC = step(0.001, tex.a);
                float dMask = step(dAmt, dVal) * bAC;
                float eMask = saturate(step(dAmt - _DissolveEdgeWidth, dVal) - step(dAmt, dVal)) * bAC * saturate(dAmt * 100.0);

                // 顏色與 Fresnel
                float3 rgb = tex.rgb * _TintColor.rgb * input.color.rgb * over.rgb * _Brightness;
                float alpha = tex.a * _TintColor.a * input.color.a * over.a * softFade;

                #ifdef _FRESNEL_ON
                    float fPower = max(0.1, 15.1 - _FresnelWidth);
                    float fNormal = pow(1.0 - saturate(abs(dot(normalize(input.normalWS), normalize(input.viewDirWS)))), fPower);
                    float2 uvD = min(input.uv, 1.0 - input.uv);
                    float fEff = lerp(fNormal, pow(saturate(1.0 - min(uvD.x, uvD.y) * 2.0), fPower), step(0.5, _FresnelType));
                    #ifdef _FRESNEL_MASK_ON
                        rgb *= fEff; alpha *= fEff;
                    #endif
                    rgb += _FresnelColor.rgb * fEff * _FresnelIntensity;
                #endif
                
                float3 finalRGB = (rgb * dMask + _DissolveEdgeColor.rgb * eMask) * alpha;
                return float4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
}
