Shader "Custom/HologramShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0.7, 1, 1)
        _RimColor ("Rim Color", Color) = (0, 0.7, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _Alpha ("Alpha", Range(0, 1)) = 0.5
        _Brightness ("Brightness", Range(0.1, 3.0)) = 1.0
        _Speed ("Scan Line Speed", Range(0, 5)) = 1.0
        _LineWidth ("Scan Line Width", Range(0, 0.1)) = 0.005
        _LineFrequency ("Scan Line Frequency", Range(0, 100)) = 10
        _Glitch ("Glitch Intensity", Range(0, 1)) = 0.1
        _GlitchSpeed ("Glitch Speed", Range(0, 20)) = 5.0
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _RimColor;
            float _RimPower;
            float _Alpha;
            float _Brightness;
            float _Speed;
            float _LineWidth;
            float _LineFrequency;
            float _Glitch;
            float _GlitchSpeed;
            
            // Helper function for random noise
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                
                // Apply minor vertex displacement for "unstable hologram" effect
                float time = _Time.y * _GlitchSpeed;
                float noise = rand(v.vertex.xy + time) * 2.0 - 1.0;
                float glitchAmount = _Glitch * sin(time * 0.5) * 0.01;
                float3 glitchOffset = v.normal * noise * glitchAmount;
                
                o.vertex = UnityObjectToClipPos(v.vertex + float4(glitchOffset, 0));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Sample texture
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Calculate rim lighting effect (edges glow more)
                float rim = 1.0 - saturate(dot(i.viewDir, i.worldNormal));
                rim = pow(rim, _RimPower);
                
                // Moving scan lines effect
                float scanLine = 0.0;
                float worldPosY = i.worldPos.y;
                
                // Primary scan line (moving upward)
                float primaryLine = frac(worldPosY * _LineFrequency + _Time.y * _Speed);
                scanLine += smoothstep(0, _LineWidth, primaryLine) * smoothstep(_LineWidth * 2, _LineWidth, primaryLine);
                
                // Secondary scan lines (more subtle, different speeds)
                float secondaryLine = frac(worldPosY * _LineFrequency * 2 - _Time.y * _Speed * 0.3);
                scanLine += smoothstep(0, _LineWidth * 0.5, secondaryLine) * smoothstep(_LineWidth, _LineWidth * 0.5, secondaryLine) * 0.3;
                
                // Create horizontal lines for retro effect
                float horizontalLines = step(0.5, frac(i.uv.y * 50)) * 0.1;
                
                // Random glitch effect
                float timeGlitch = floor(_Time.y * _GlitchSpeed);
                float randomGlitch = rand(float2(timeGlitch, timeGlitch));
                float glitchLine = step(0.98, randomGlitch) * step(0.98, rand(i.uv + timeGlitch));
                
                // Combine effects
                float3 finalColor = _Color.rgb * _Brightness;
                finalColor += _RimColor.rgb * rim;
                finalColor += _Color.rgb * scanLine * 2.0;
                finalColor -= horizontalLines;
                finalColor += glitchLine * _RimColor.rgb * 2.0;
                
                // Calculate alpha with scan line and rim effects
                float alpha = _Alpha;
                alpha += scanLine * 0.3;
                alpha += rim * 0.3;
                alpha = saturate(alpha);
                
                // Add subtle wave distortion over time
                float waveDistortion = sin(i.uv.y * 20 + _Time.y * 2) * 0.01;
                float2 waveUV = i.uv + float2(waveDistortion, 0);
                float waveEffect = tex2D(_MainTex, waveUV).r * 0.1;
                
                finalColor += waveEffect;
                
                return float4(finalColor, alpha);
            }
            ENDCG
        }
    }
    FallBack "Transparent/VertexLit"
}
