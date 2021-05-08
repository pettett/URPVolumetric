using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public class VolumetricLightingRenderFeature : ScriptableRendererFeature
{
	public class VolumetricSampleMapPass : ScriptableRenderPass
	{
		// used to label this pass in Unity's Frame Debug utility
		string profilerTag;

		RenderTargetHandle sampleMap;
		RenderTargetHandle tempRenderTex;

		RenderTargetIdentifier m_Source;
		RenderTargetIdentifier m_Destination;

		RenderTextureDescriptor m_IntermediateDesc;

		readonly VolumetricLightingSettings settings;

		// The postprocessing materials
		private Material m_VolumetricAdditivePass;
		private Material m_VolumetricPostProcess;
		// This isn't part of the ScriptableRenderPass class and is our own addition.
		// For this custom pass we need the camera's color target, so that gets passed in.

		public VolumetricSampleMapPass(string profilerTag, RenderPassEvent renderPassEvent, VolumetricLightingSettings settings)
		{
			this.profilerTag = profilerTag;
			this.settings = settings;
			this.renderPassEvent = renderPassEvent;


			sampleMap = new RenderTargetHandle();
			tempRenderTex = new RenderTargetHandle();

			sampleMap.Init("_SampleMap");
			tempRenderTex.Init("Temp RT");
		}
		public void Setup(RenderTargetIdentifier source, RenderTargetIdentifier destination)
		{
			m_Source = source;
			m_Destination = destination;

			m_VolumetricAdditivePass = CoreUtils.CreateEngineMaterial("Hidden/Custom/VolumetricAdditivePass");
			m_VolumetricPostProcess = CoreUtils.CreateEngineMaterial("Hidden/Custom/VolumetricPostProcess");
		}
		// called each frame before Execute, use it to set up things the pass will need
		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
		{

			m_IntermediateDesc = cameraTextureDescriptor;
			m_IntermediateDesc.msaaSamples = 1;
			m_IntermediateDesc.depthBufferBits = 0;
			//m_IntermediateDesc.textureFormat = RenderTextureFormat.R8;

			cmd.GetTemporaryRT(tempRenderTex.id, m_IntermediateDesc);

			m_IntermediateDesc.width /= settings.textureDownscale;
			m_IntermediateDesc.height /= settings.textureDownscale;


			// create a temporary render texture that matches the camera
			cmd.GetTemporaryRT(sampleMap.id, m_IntermediateDesc.width, m_IntermediateDesc.height, 0, FilterMode.Bilinear, RenderTextureFormat.R16);


			//ConfigureTarget(sampleMap.Identifier());

		}


		// Execute is called for every eligible camera every frame. It's not called at the moment that
		// rendering is actually taking place, so don't directly execute rendering commands here.
		// Instead use the methods on ScriptableRenderContext to set up instructions.
		// RenderingData provides a bunch of (not very well documented) information about the scene
		// and what's being rendered.
		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{



			// // the actual content of our custom render pass!
			// // we apply our material while blitting to a temporary texture
			// cmd.Blit(cameraColorTargetIdent, sampleMap.Identifier(), materialToBlit, 0);

			// // ...then blit it back again 
			// cmd.Blit(sampleMap.Identifier(), cameraColorTargetIdent);


			ref CameraData cameraData = ref renderingData.cameraData;


			// fetch a command buffer to use
			CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

			cmd.Clear();

			int width = m_IntermediateDesc.width * settings.textureDownscale;
			int height = m_IntermediateDesc.height * settings.textureDownscale;

			cmd.SetGlobalVector("_ScreenSize", new Vector4(width, height, 1.0f / width, 1.0f / height));

			// set source texture

			//cmd.SetGlobalTexture(ShaderIDs.Input, source);


			//CoreUtils.DrawFullScreen(cmd, m_Material, sampleMap.id);

			// set source texture

			m_VolumetricPostProcess.SetVector("_VolumetricLight", new Vector4(settings.scattering, settings.extinction, 0, settings.skyboxExtinction));

			m_VolumetricPostProcess.SetVector("_NoiseSettings", new Vector4(settings.noiseOffset.x, settings.noiseOffset.y, settings.noiseOffset.z, settings.noiseScale));

			if (settings.anisotropy)
			{
				m_VolumetricPostProcess.EnableKeyword("ANISOTROPY");
				m_VolumetricPostProcess.SetFloat("_Anisotropy", settings.anisotropyScalar);
				m_VolumetricPostProcess.SetVector("_DirLightDir", -RenderSettings.sun.transform.forward);
			}

			m_VolumetricPostProcess.SetInt("samples", (int)settings.pixelSamples);
			m_VolumetricPostProcess.SetFloat("inverseSamples", 1f / settings.pixelSamples);

			m_VolumetricPostProcess.SetVector("dithering", new Vector2(settings.ditherScale, settings.ditherStrength));
			m_VolumetricPostProcess.SetTexture("_BlueNoise", settings.ditherTexture);

			// draw a fullscreen triangle to the destination


			//set source texture
			Blit(cmd, m_Source, tempRenderTex.Identifier());


			CoreUtils.DrawFullScreen(cmd, m_VolumetricPostProcess, sampleMap.Identifier());

			cmd.SetGlobalTexture("_MainTex", tempRenderTex.Identifier());
			cmd.SetGlobalTexture("_SampleMap", sampleMap.Identifier());

			m_VolumetricAdditivePass.SetColor("_RayColor", RenderSettings.sun.color);

			CoreUtils.DrawFullScreen(cmd, m_VolumetricAdditivePass, m_Destination);




			// don't forget to tell ScriptableRenderContext to actually execute the commands
			context.ExecuteCommandBuffer(cmd);

			// tidy up after ourselves
			cmd.Clear();
			CommandBufferPool.Release(cmd);
		}

		// called after Execute, use it to clean up anything allocated in Configure
		public override void FrameCleanup(CommandBuffer cmd)
		{
			cmd.ReleaseTemporaryRT(sampleMap.id);
			cmd.ReleaseTemporaryRT(tempRenderTex.id);
		}
	}



	[System.Serializable]
	public class VolumetricLightingSettings
	{
		// we're free to put whatever we want here, public fields will be exposed in the inspector
		public bool IsEnabled = true;
		public uint pixelSamples = 32;
		public Vector3 noiseOffset;
		public float noiseScale;
		[Range(0, 1)]
		public float scattering = 0.07f;
		public float extinction = 0.22f;
		public float skyboxExtinction = 0;
		[Range(1, 5)]
		public int textureDownscale = 1;
		public bool anisotropy = true;
		[Range(0, 1)]
		public float anisotropyScalar = 0.1f;
		public float ditherStrength = 0.5f;
		public float ditherScale = 1.27f;
		public Texture2D ditherTexture;
	}


	// MUST be named "settings" (lowercase) to be shown in the Render Features inspector
	public VolumetricLightingSettings settings = new VolumetricLightingSettings();

	VolumetricSampleMapPass fillSampleMapPass;

	public override void Create()
	{
		fillSampleMapPass = new VolumetricSampleMapPass(
		  "Volumetric Lighting", RenderPassEvent.AfterRenderingTransparents, settings
		);
	}

	// called every frame once per camera
	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		if (!settings.IsEnabled || !RenderSettings.sun.enabled || !RenderSettings.sun.gameObject.activeSelf)
		{
			// we can do nothing this frame if we want
			return;
		}

		// Gather up and pass any extra information our pass will need.
		// In this case we're getting the camera's color buffer target

		fillSampleMapPass.Setup(renderer.cameraColorTarget, renderer.cameraColorTarget);

		// Ask the renderer to add our pass.
		// Could queue up multiple passes and/or pick passes to use
		renderer.EnqueuePass(fillSampleMapPass);
	}
}
