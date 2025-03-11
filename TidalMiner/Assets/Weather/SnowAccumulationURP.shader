Shader "Custom/SnowAccumulationURP"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _BaseMap("Base Texture", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
        
        [Space(10)]
        [Header(Snow Properties)]
        _SnowColor("Snow Color", Color) = (1,1,1,1)
        _SnowMap("Snow Texture", 2D) = "white" {}
        _SnowNormalMap("Snow Normal Map", 2D) = "bump" {}
        _SnowAmount("Snow Amount", Range(0,1)) = 0.0
        _SnowDirection("Snow Direction", Vector) = (0,1,0,0)
        _SnowAngleThreshold("Snow Angle Threshold", Range(0,1)) = 0.6
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Unity included shader functionality
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            
            // URP requires this specifically for lighting
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float4 tangentWS    : TEXCOORD3;
            };
            
            // Texture samplers
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_SnowMap);
            SAMPLER(sampler_SnowMap);
            TEXTURE2D(_SnowNormalMap);
            SAMPLER(sampler_SnowNormalMap);
            
            // Shader properties
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _SnowMap_ST;
                float4 _SnowColor;
                float _Smoothness;
                float _Metallic;
                float _SnowAmount;
                float3 _SnowDirection;
                float _SnowAngleThreshold;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // Calculate position, normal, and tangent in world space
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
                
                // Transform to clip space
                output.positionCS = TransformWorldToHClip(output.positionWS);
                
                // Pass through UVs
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                
                return output;
            }
            
            // Normal mapping function
            float3 GetNormalFromMap(float3 normalSample, float3 normalWS, float4 tangentWS)
            {
                // Unpack normal sample from texture
                float3 normalTS = UnpackNormal(float4(normalSample, 1.0));
                
                // Calculate bitangent
                float3 bitangentWS = cross(normalWS, tangentWS.xyz) * tangentWS.w;
                
                // Create TBN matrix
                float3x3 tangentToWorld = float3x3(
                    tangentWS.xyz,
                    bitangentWS,
                    normalWS
                );
                
                // Transform normal from tangent to world space
                return normalize(mul(normalTS, tangentToWorld));
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Get the world normal for snow angle calculation
                float3 normalWS = normalize(input.normalWS);
                
                // Sample normal maps
                float3 baseNormalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv).rgb;
                float3 snowNormalSample = SAMPLE_TEXTURE2D(_SnowNormalMap, sampler_SnowNormalMap, input.uv).rgb;
                
                // Convert normal samples to world space normals
                float3 baseNormalWS = GetNormalFromMap(baseNormalSample, normalWS, input.tangentWS);
                float3 snowNormalWS = GetNormalFromMap(snowNormalSample, normalWS, input.tangentWS);
                
                // Calculate snow coverage based on surface angle relative to snow direction
                float snowCoverage = max(0, dot(normalWS, normalize(_SnowDirection)));
                
                // Apply threshold with a smooth transition
                float snowMask = smoothstep(_SnowAngleThreshold - 0.1, _SnowAngleThreshold + 0.1, snowCoverage);
                
                // Calculate final snow amount based on global snow amount and local angle
                float finalSnowAmount = _SnowAmount * snowMask;
                
                // Sample base texture and snow texture
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half4 snowColor = SAMPLE_TEXTURE2D(_SnowMap, sampler_SnowMap, input.uv) * _SnowColor;
                
                // Blend between base and snow based on snow amount
                half4 albedo = lerp(baseColor, snowColor, finalSnowAmount);
                float3 blendedNormalWS = lerp(baseNormalWS, snowNormalWS, finalSnowAmount);
                float smoothness = lerp(_Smoothness, 0.8, finalSnowAmount); // Snow is usually more smooth
                float metallic = lerp(_Metallic, 0.0, finalSnowAmount); // Snow isn't metallic
                
                // Set up lighting data
                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = input.positionWS;
                lightingInput.normalWS = blendedNormalWS;
                lightingInput.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lightingInput.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                
                // Standard URP PBS lighting calculations
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = albedo.a;
                surfaceData.metallic = metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = float3(0, 0, 1); // Not used because we're working in world space
                surfaceData.emission = 0;
                surfaceData.occlusion = 1;
                
                // Apply URP standard lighting
                return UniversalFragmentPBR(lightingInput, surfaceData);
            }
            ENDHLSL
        }
        
        // Shadow casting support
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
            
            // Shadow caster specific input
            float3 _LightDirection;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _SnowMap_ST;
                float4 _SnowColor;
                float _Smoothness;
                float _Metallic;
                float _SnowAmount;
                float3 _SnowDirection;
                float _SnowAngleThreshold;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 texcoord     : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
            
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
        
        // Depth prepass
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _SnowMap_ST;
                float4 _SnowColor;
                float _Smoothness;
                float _Metallic;
                float _SnowAmount;
                float3 _SnowDirection;
                float _SnowAngleThreshold;
            CBUFFER_END

            struct Attributes
            {
                float4 position     : POSITION;
                float2 texcoord     : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}