Shader "Hidden/Custom/VolumetricPostProcess"
{
    HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    
        #include "noiseSimplex.hlsl"

		#include "Core.hlsl"

        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
        #pragma multi_compile _ ANISOTROPY


        // x: scattering coef, y: extinction coef, z: range w: skybox extinction coef
		uniform float4 _VolumetricLight;

        // x: 1 - g^2, y: 1 + g^2, z: 2*g, w: 1/4pi
        uniform float4 _MieG;

		#ifdef ANISOTROPY

        uniform float _Anisotropy;
		uniform float3 _DirLightDir;

		#endif

        //XYZ, offset, W - scale
        uniform float4 _NoiseSettings;



		Texture2D _BlueNoise;
		sampler sampler_BlueNoise;

		//X = ditherScale, y = ditherStrength
		uniform float2 dithering;

		//https://github.com/Unity-Technologies/VolumetricLighting/blob/master/Assets/VolumetricFog/Shaders/InjectLightingAndDensity.compute
		#ifdef ANISOTROPY
		float anisotropy(float costheta)
		{
			float g = _Anisotropy;
			float gsq = g*g;
			float denom = 1 + gsq - 2.0 * g * costheta;
			denom = denom * denom * denom;
			denom = sqrt(max(0, denom));
			return (1 - gsq) / denom;
		}
		#endif
		//Dithering with blue noise
		//https://github.com/SebLague/Solar-System/blob/0c60882be69b8e96d6660c28405b9d19caee76d5/Assets/Scripts/Celestial/Shaders/PostProcessing/Atmosphere.shader

		uniform uint samples = 32; //X : Samples
      	uniform float inverseSamples = 1/32; //Y : recripical samples

		float MieScattering(float cosAngle, float4 g)
		{
            return g.w * (g.x / (pow(abs(g.y - g.z * cosAngle), 1.5)));			
		}

        float GetDensity(float3 wPos){
            return 1;// saturate(1-wPos.y*0.1);
        }

        float Frag(PostProcessVaryings i) : SV_Target
        {

           // float depth = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord).r;
			float blueNoise = SAMPLE_TEXTURE2D_X(_BlueNoise, sampler_BlueNoise, i.texcoord * dithering.x);

			blueNoise = ((blueNoise - 0.5) * dithering.y) * 0.01;



            // Transform the camera origin to world space
            float3 rayOrigin = _WorldSpaceCameraPos.xyz;
			
			float3 viewDirCS = mul(unity_CameraInvProjection, float4(-i.texcoord*2+1,0,1)).xyz;

			float3 viewDirWS = mul(unity_CameraToWorld , float4(viewDirCS,0)).xyz;

        	float3 rayDir = -normalize(viewDirWS);

            float depth = SampleSceneDepth(i.texcoord);
           //return float4(depth,0,0,0);

    
            //return float4(rayDir* 0.5f + 0.5f,1); //DEBUG - show world space directions


            float rayLength = LinearEyeDepth(depth,_ZBufferParams);


			float4 sampleShadowCoord =  TransformWorldToShadowCoord(rayOrigin + rayDir *  rayLength);
			float atten = MainLightRealtimeShadow(sampleShadowCoord);


            float totalAtt = 0;
            float extinction = 0;


            //float cosAngle = dot(light.direction.xyz, -rayDir);

  

            for (uint index = 0; index < samples; index++){
                float distance = saturate(index * inverseSamples) * rayLength;

                float3 samplePosWS = rayOrigin + rayDir * distance;


                float4 sampleShadowCoord =  TransformWorldToShadowCoord(samplePosWS);
                float atten = MainLightRealtimeShadow(sampleShadowCoord);

                float density =  GetDensity(samplePosWS);

                float scattering = _VolumetricLight.x * inverseSamples * density;
				extinction += _VolumetricLight.y * inverseSamples * density;

                totalAtt += atten * scattering * exp(-extinction);
            }

            totalAtt = lerp(totalAtt,0,_VolumetricLight.w * (depth < 0.0001));

            //total *= MieScattering(abs( cosAngle), _MieG);


			#if ANISOTROPY
			float costheta = dot(rayDir, _DirLightDir);
			totalAtt *= anisotropy(costheta);
			#endif

			//Dither to stop colour banding
			totalAtt += blueNoise;

            return totalAtt;


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