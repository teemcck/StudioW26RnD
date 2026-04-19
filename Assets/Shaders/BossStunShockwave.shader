Shader "Boss/StunShockwaveDistort"
{
    Properties
    {
        [HDR] _BaseColor ("Energy Tint", Color) = (0.35, 0.92, 1.0, 1)
        _Distortion ("Screen Distortion", Range(0, 0.08)) = 0.028
        _WaveProgress ("Wave Progress", Range(0, 1.5)) = 0
        _RingSharpness ("Ring Sharpness", Range(0.015, 0.25)) = 0.075
        _GlowBoost ("Glow", Range(0, 5)) = 2.0
        _FalloffEdge ("Wave Reach", Range(0.6, 2)) = 1.2
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Distortion;
                float _WaveProgress;
                float _RingSharpness;
                float _GlowBoost;
                float _FalloffEdge;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 d = i.uv - 0.5;
                float r = length(d) * 2.0;
                float2 dir = length(d) > 1e-4 ? normalize(d) : float2(1, 0);

                float waveR = _WaveProgress * _FalloffEdge;
                float band = abs(r - waveR);
                float sigma = max(_RingSharpness, 0.001);
                float ring = exp(-(band * band) / (sigma * sigma));
                ring *= saturate(1.0 - r * 0.12);

                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float2 distort = dir * ring * _Distortion;

                half3 baseCol = SampleSceneColor(screenUV);
                half3 refrCol = SampleSceneColor(screenUV + distort);
                half3 glow = _BaseColor.rgb * ring * _GlowBoost;
                half3 rgb = lerp(baseCol, refrCol, ring * 0.82) + glow * ring;
                half a = saturate(ring * 1.35);

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
