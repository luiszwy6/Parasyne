Shader "Hidden/FullscreenBlackout"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }

        Pass
        {
            Name "FullscreenBlackout"
            ZWrite Off
            ZTest Always
            Cull Off

            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.positionHCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                o.uv = uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                return half4(0,0,0,1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
