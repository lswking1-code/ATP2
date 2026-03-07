Shader "PostProcess/VHSPostProcessEffectURP"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "VHSPostProcessPass"
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_VHSTex);
            SAMPLER(sampler_VHSTex);

            float _yScanline;
            float _xScanline;
            float _Intensity;

            float rand(float3 co)
            {
                return frac(sin(dot(co.xyz, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                half4 cleanColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearRepeat, uv);
                half4 vhs = SAMPLE_TEXTURE2D(_VHSTex, sampler_VHSTex, uv);

                float bleed = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearRepeat, uv + float2(0.01, 0)).r;
                bleed += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearRepeat, uv + float2(0.02, 0)).r;
                bleed += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearRepeat, uv + float2(0.01, 0.01)).r;
                bleed += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearRepeat, uv + float2(0.02, 0.02)).r;
                bleed /= 6;

                if (bleed > 0.1)
                    vhs += half4(bleed * _xScanline, 0, 0, 0);

                float x = floor(uv.x * 320) / 320.0;
                float y = floor(uv.y * 240) / 240.0;
                half4 c = cleanColor - rand(float3(x, y, _xScanline)) * _xScanline / 5;
                half4 effectColor = c + vhs;
                return lerp(cleanColor, effectColor, _Intensity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
