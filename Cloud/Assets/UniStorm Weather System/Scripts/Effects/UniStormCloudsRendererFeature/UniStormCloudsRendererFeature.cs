using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UniStorm.Utility
{
    public class UniStormCloudsRendererFeature : ScriptableRendererFeature
    {
        class UniStormCloudsRenderPass : ScriptableRenderPass
        {
            private UniStormClouds m_UniStormClouds;
            private ProfilingSampler m_ProfilingSampler = new ProfilingSampler("UniStorm Clouds");

            public UniStormCloudsRenderPass(UniStormClouds uniStormClouds)
            {
                m_UniStormClouds = uniStormClouds;
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
            }

            public bool TargetIsDestroyed()
            {
                return m_UniStormClouds == null;
            }

            [System.Obsolete]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
#if UNITY_EDITOR
                if (UnityEditor.EditorApplication.isPaused) return;
#endif
                if (m_UniStormClouds == null || !m_UniStormClouds.enabled) return;

                CameraData cameraData = renderingData.cameraData;
                Camera camera = cameraData.camera;

                if (cameraData.cameraType == CameraType.SceneView || !Application.isPlaying) return;
                if (!UniStormSystem.Instance.UniStormInitialized) return;

                var cmd = CommandBufferPool.Get("UniStorm Clouds");
                using (new ProfilingScope(cmd, m_ProfilingSampler))
                {
                    // Access fields using reflection
                    var lowResCloudsBufferField = typeof(UniStormClouds).GetField("lowResCloudsBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
                    var fullCloudsBufferField = typeof(UniStormClouds).GetField("fullCloudsBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
                    var cloudShadowsBufferField = typeof(UniStormClouds).GetField("cloudShadowsBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
                    var fullBufferIndexField = typeof(UniStormClouds).GetField("fullBufferIndex", BindingFlags.NonPublic | BindingFlags.Instance);

                    var lowResCloudsBuffer = (RenderTexture)lowResCloudsBufferField.GetValue(m_UniStormClouds);
                    var fullCloudsBuffer = (RenderTexture[])fullCloudsBufferField.GetValue(m_UniStormClouds);
                    var cloudShadowsBuffer = (RenderTexture[])cloudShadowsBufferField.GetValue(m_UniStormClouds);
                    var fullBufferIndex = (int)fullBufferIndexField.GetValue(m_UniStormClouds);

                    //1. Render low-res
                    cmd.Blit(null, lowResCloudsBuffer, m_UniStormClouds.skyMaterial, 0);
                    //2. Blend buffers
                    cmd.SetGlobalTexture("_uLowresCloudTex", lowResCloudsBuffer);
                    cmd.SetGlobalTexture("_uPreviousCloudTex", fullCloudsBuffer[fullBufferIndex]);
                    cmd.Blit(
                        fullCloudsBuffer[fullBufferIndex],
                        fullCloudsBuffer[fullBufferIndex ^ 1],
                        m_UniStormClouds.skyMaterial, 1
                    );

                    //3. Cloud shadows
                    switch (m_UniStormClouds.CloudShadowsTypeRef)
                    {
                        case UniStormClouds.CloudShadowsType.Simulated:
                            var mat = m_UniStormClouds.shadowsBuildingMaterial;
                            mat.SetFloat("_uCloudsCoverage", m_UniStormClouds.skyMaterial.GetFloat("_uCloudsCoverage"));
                            mat.SetFloat("_uCloudsCoverageBias", m_UniStormClouds.skyMaterial.GetFloat("_uCloudsCoverageBias"));
                            mat.SetFloat("_uCloudsDensity", m_UniStormClouds.skyMaterial.GetFloat("_uCloudsDensity"));
                            mat.SetFloat("_uCloudsDetailStrength", m_UniStormClouds.skyMaterial.GetFloat("_uCloudsDetailStrength"));
                            mat.SetFloat("_uCloudsBaseEdgeSoftness", m_UniStormClouds.skyMaterial.GetFloat("_uCloudsBaseEdgeSoftness"));
                            mat.SetFloat("_uCloudsBottomSoftness", m_UniStormClouds.skyMaterial.GetFloat("_uCloudsBottomSoftness"));
                            mat.SetFloat("_uSimulatedCloudAlpha", m_UniStormClouds.cloudTransparency);
                            cmd.Blit(GenerateNoise.baseNoiseTexture, cloudShadowsBuffer[0], mat, 3);
                            m_UniStormClouds.PublicCloudShadowTexture = cloudShadowsBuffer[0];
                            break;

                        case UniStormClouds.CloudShadowsType.RealTime:
                            cmd.Blit(fullCloudsBuffer[fullBufferIndex ^ 1],
                                     cloudShadowsBuffer[0]);
                            for (int i = 0; i < m_UniStormClouds.shadowBlurIterations; i++)
                            {
                                cmd.Blit(cloudShadowsBuffer[0], cloudShadowsBuffer[1],
                                         m_UniStormClouds.shadowsBuildingMaterial, 1);
                                cmd.Blit(cloudShadowsBuffer[1], cloudShadowsBuffer[0],
                                         m_UniStormClouds.shadowsBuildingMaterial, 2);
                            }
                            break;
                        default: break;
                    }

                    cmd.SetGlobalFloat("_uLightning", 0.0f);
                    //4. Assign to sky material
                    m_UniStormClouds.cloudsMaterial.SetTexture(
                        "_MainTex",
                        fullCloudsBuffer[fullBufferIndex ^ 1]
                    );
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        UniStormCloudsRenderPass m_ScriptablePass;

        public override void Create()
        {

        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            //Find the active clouds in the scene
            var uniStormClouds = Object.FindFirstObjectByType<UniStormClouds>();
            if (uniStormClouds == null)
                return;

            //If the pass was never made, or its target was destroyed by a scene reload, recreate it
            if (m_ScriptablePass == null || m_ScriptablePass.TargetIsDestroyed())
                m_ScriptablePass = new UniStormCloudsRenderPass(uniStormClouds);

            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}