Shader "Hidden/Custom/VolumetricAdditivePass"
{
    HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

		#include "Core.hlsl"


        TEXTURE2D_X(_MainTex); 
        TEXTURE2D_X(_SampleMap);

        float3 _RayColor;



        float SampleTexture(float2 uv, float2 offset){
            return SAMPLE_TEXTURE2D_X(_SampleMap, sampler_LinearClamp, uv+_ScreenSize.zw * offset).r;
        }

        float4 Frag(PostProcessVaryings i) : SV_Target
        {
			

            float3 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, i.texcoord);

            float raySample = SampleTexture(i.texcoord ,float2(0,0)) ;
			
            raySample += SampleTexture( i.texcoord, float2(1,0) ) ;
            raySample += SampleTexture( i.texcoord ,float2(-1,0));
            raySample += SampleTexture( i.texcoord, float2(0,1));
            raySample += SampleTexture( i.texcoord, float2(0,-1));

            raySample += SampleTexture( i.texcoord ,float2(1,1));
            raySample += SampleTexture( i.texcoord, float2(1,-1));
            raySample += SampleTexture(i.texcoord ,float2(-1,1));
            raySample += SampleTexture( i.texcoord ,float2(-1,-1));

            raySample *= rcp(9);
            //return float4(raySample,raySample,raySample,1);

            return float4(raySample * _RayColor + color,1);
        }



    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM

                #pragma vertex FullScreenTrianglePostProcessVertexProgram
                #pragma fragment Frag

            ENDHLSL
        }
    }
}