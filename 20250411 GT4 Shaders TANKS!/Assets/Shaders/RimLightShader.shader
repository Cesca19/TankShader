Shader "Custom/RimLightShader"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0, 0.66, 0.73, 1)
        
        _RimColor ("Rim Light Color", Color) = (1, 1, 1, 1)
        _RimStrength ("Rim Strength", Float) = 1
        _EdgeShadow ("Edge Shadow", Float) = 1
        _ShadowPower ("Shadow Power", Float) = 0
        
    }
    
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                half4 _RimColor;
                half _RimStrength;
                half _EdgeShadow;
                half _ShadowPower;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

             struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 viewDirWS    : TEXCOORD3;
            };

            half3 GetMainLightDirection()
            {
                return GetMainLight().direction;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);

                output.uv = input.uv;
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                
                half NdotV = dot(input.normalWS, input.viewDirWS);

                half fresnel = pow(1.0 - saturate(NdotV), _EdgeShadow);
                
                half3 lightDirWS = GetMainLightDirection();
                half NdotL = dot(input.normalWS, lightDirWS);

                half shadowEdge = pow(1.0 - saturate(NdotL), _ShadowPower);

                half rimLight = fresnel * shadowEdge * _RimStrength;

                // Sample base texture
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                
               // Combine base texture with base color
                half3 baseColor = baseMap.rgb * _BaseColor.rgb;
                
                // Calculate lighting with more natural falloff
                Light mainLight = GetMainLight();
                half NdotL_Adjusted = max(0.2, saturate(NdotL));
                half3 litColor = baseColor * NdotL_Adjusted;
                
                // Add rim effect only at the edges, with controlled intensity
                // This will be the part that blooms
                half3 finalColor = litColor + (_RimColor.rgb * rimLight);
                
                // Control the overall brightness to manage bloom effect
                // This helps prevent the base color from appearing too emissive
                finalColor = finalColor * 1;
                
                return half4(finalColor, 1);
            }
            
            ENDHLSL
        }
    }
    
    Fallback "Standard"
}