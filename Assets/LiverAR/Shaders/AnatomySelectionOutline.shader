Shader "LiverAR/Anatomy Selection Outline"
{
    Properties
    {
        _BaseColor("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineCenter("Mesh Center", Vector) = (0, 0, 0, 0)
        _OutlineScale("Outline Scale", Float) = 1.015
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry-1" }

        Pass
        {
            Name "SelectionOutline"
            Tags { "LightMode" = "UniversalForward" }

            // Draw only the back-facing expanded shell. The normal coloured mesh
            // renders afterwards and covers the shell's interior.
            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _OutlineCenter;
                float _OutlineScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 expandedPosition = _OutlineCenter.xyz +
                    (input.positionOS.xyz - _OutlineCenter.xyz) * _OutlineScale;
                output.positionHCS = TransformObjectToHClip(expandedPosition);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
