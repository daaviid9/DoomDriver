Shader "DoomDriver/FinishZoneGradient"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color (HDR)", Color) = (0.15, 1.0, 0.45, 0.65)
        _FadePower ("Upward Fade Power", Range(0.2, 5.0)) = 1.5
        [Toggle] _InvertVertical ("Invert Vertical Direction", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _FadePower;
                half _InvertVertical;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half v = _InvertVertical > 0.5 ? IN.uv.y : (1.0h - IN.uv.y);
                half gradient = pow(saturate(v), _FadePower);
                half4 col = _BaseColor;
                col.a *= gradient;
                return col;
            }
            ENDHLSL
        }
    }
}

