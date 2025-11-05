Shader "Custom/Axis"
{
    Properties
    {
        _XAxisColour ("X Axis Colour", color) = (1, 1, 1, 1)
        _ZAxisColour ("Z Axis Colour", color) = (1, 1, 1, 1)
        _BaseColour ("Base Colour", color) = (1, 1, 1, 1)
        _LineThickness ("Line Thickness", float) = .3
        _ODistance ("Start Transparency Distance", float) = 5
        _TDistance ("Full Transparency Distance", float) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-1" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
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
                UNITY_VERTEX_OUTPUT_STEREO
            };
            fixed4 _XAxisColour;
            fixed4 _ZAxisColour;
            // fixed4 _ZAxisColour;
            fixed4 _BaseColour;
            float _LineThickness;
            float _ODistance;
            float _TDistance;

            v2f vert (appdata_full v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.uv = o.worldPos.xz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 distFromAxes = abs(i.worldPos.xz);

                float2 speeds = fwidth(i.worldPos.xz);
                float2 pixelDist = distFromAxes / speeds;

                float xAxisWeight = saturate(pixelDist.y - _LineThickness);
                float zAxisWeight = saturate(pixelDist.x - _LineThickness);

                half4 param = _BaseColour;

                if (xAxisWeight < 1.0) {
                    param = lerp(_XAxisColour, param, xAxisWeight);
                }

                if (zAxisWeight < 1.0) {
                    param = lerp(_ZAxisColour, param, zAxisWeight);
                }

                //distance falloff
                half3 viewDirW = _WorldSpaceCameraPos - i.worldPos;
                half viewDist = length(viewDirW);
                half falloff = saturate((viewDist - _ODistance) / (_TDistance - _ODistance) );
                param.a *= (1.0f - falloff);
                return param;
            }
            ENDCG
        }
    }
}
