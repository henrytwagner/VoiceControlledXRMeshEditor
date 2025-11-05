Shader "URP/Grid"
{
    Properties
    {
        _GridColour ("Grid Colour", color) = (1, 1, 1, 1)
        _BaseColour ("Base Colour", color) = (1, 1, 1, 0)
        _GridSpacing ("Grid Spacing", float) = 1
        _LineThickness ("Line Thickness", float) = .3
        _ODistance ("Start Transparency Distance", float) = 5
        _TDistance ("Full Transparency Distance", float) = 10
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent-1"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            
            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            CBUFFER_START(UnityPerMaterial)
                half4 _GridColour;
                half4 _BaseColour;
                float _GridSpacing;
                float _LineThickness;
                float _ODistance;
                float _TDistance;
            CBUFFER_END
            
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.vertex.xyz);
                
                o.vertex = positionInputs.positionCS;
                o.worldPos = positionInputs.positionWS;
                o.uv = o.worldPos.xz / _GridSpacing;
                o.screenPos = ComputeScreenPos(o.vertex);
                o.fogCoord = ComputeFogFactor(o.vertex.z);
                
                return o;
            }
            
            half4 frag (v2f i) : SV_Target
            {
                // Check depth - discard if something is in front
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float sceneDepth = SampleSceneDepth(screenUV);
                float gridDepth = i.screenPos.z / i.screenPos.w;
                
                #if UNITY_REVERSED_Z
                    if (gridDepth > sceneDepth)
                        discard;
                #else
                    if (gridDepth < sceneDepth)
                        discard;
                #endif
                
                // Grid line generation
                float2 wrapped = frac(i.uv) - 0.5;
                float2 range = abs(wrapped);
                float2 speeds = fwidth(i.uv);
                float2 pixelRange = range / speeds;
                float lineWeight = saturate(min(pixelRange.x, pixelRange.y) - _LineThickness);
                half4 color = lerp(_GridColour, _BaseColour, lineWeight);
                
                // Distance falloff
                float3 viewDir = _WorldSpaceCameraPos - i.worldPos;
                float viewDist = length(viewDir);
                float falloff = saturate((viewDist - _ODistance) / (_TDistance - _ODistance));
                color.a *= (1.0 - falloff);
                
                // Apply fog
                color.rgb = MixFog(color.rgb, i.fogCoord);
                
                return color;
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

