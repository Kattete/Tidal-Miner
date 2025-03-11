Shader "Custom/OceanWater"
{
    Properties
    {
        _Color ("Shallow Color", Color) = (0.2, 0.5, 0.9, 0.8)
        _DeepColor ("Deep Color", Color) = (0.0, 0.2, 0.5, 0.9)
        _MainTex ("Water Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.9
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _WaveSpeed ("Wave Speed", Range(0,2)) = 0.5
        _WaveHeight ("Wave Height", Range(0,1)) = 0.1
        _WaveScale ("Wave Scale", Range(0.1,10)) = 1.0
        _FlowSpeed ("Flow Speed", Range(0,2)) = 0.5
        _RippleStrength ("Rain Ripple Strength", Range(0,1)) = 0.1
        _Transparency ("Transparency", Range(0,1)) = 0.7
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200
        
        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0
        
        sampler2D _MainTex;
        sampler2D _NormalMap;
        
        struct Input
        {
            float2 uv_MainTex;
            float2 uv_NormalMap;
            float3 worldPos;
        };
        
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _DeepColor;
        float _WaveSpeed;
        float _WaveHeight;
        float _WaveScale;
        float _FlowSpeed;
        float _RippleStrength;
        float _Transparency;
        
        // Add waves to the vertices
        void vert (inout appdata_full v) {
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            
            // Create waves based on position and time
            float wave = sin(worldPos.x * _WaveScale + _Time.y * _WaveSpeed) * 
                        cos(worldPos.z * _WaveScale + _Time.y * _WaveSpeed) * _WaveHeight;
            
            // Add rain ripples if set
            if (_RippleStrength > 0) {
                float rainRippleFreq = 20.0 + _RippleStrength * 30.0;
                float rainRipple = sin(worldPos.x * rainRippleFreq + _Time.y * 8.0) * 
                                 sin(worldPos.z * rainRippleFreq + _Time.y * 8.0) * (_RippleStrength * 0.05);
                wave += rainRipple;
            }
            
            v.vertex.y += wave;
        }
        
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Calculate flowing UV coordinates
            float2 flowingUV = IN.uv_MainTex + float2(_Time.y * _FlowSpeed, _Time.y * _FlowSpeed * 0.7);
            
            // Sample water texture with flowing coordinates
            fixed4 c = tex2D(_MainTex, flowingUV);
            
            // Blend between shallow and deep water colors
            fixed4 finalColor = lerp(_Color, _DeepColor, c.r);
            
            // Apply normal map with flowing coordinates
            float3 normal = UnpackNormal(tex2D(_NormalMap, flowingUV));
            o.Normal = normal;
            
            o.Albedo = finalColor.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = finalColor.a * _Transparency;
        }
        ENDCG
    }
    FallBack "Diffuse"
}