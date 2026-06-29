using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VRCFury.SPS.URP {
    /// <summary>
    /// Replaces the Built-in Render Pipeline GrabPass transport used by SPSv2 with
    /// explicit URP render textures. Add this renderer feature to each URP renderer
    /// data asset used by avatars/scenes that contain VRCFury SPS plugs or sockets.
    /// </summary>
    public class VrcfurySpsUrpRendererFeature : ScriptableRendererFeature {
        [Serializable]
        public class Settings {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            public LayerMask layerMask = ~0;
            public bool skipPreviewCameras = true;
        }

        [SerializeField] private Settings settings = new Settings();
        private SpsTransportPass pass;

        public override void Create() {
            pass = new SpsTransportPass(settings) {
                renderPassEvent = settings.renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (pass == null) Create();
            pass.UpdateSettings(settings);
            renderer.EnqueuePass(pass);
        }

        private class SpsTransportPass : ScriptableRenderPass {
            private static readonly ShaderTagId SocketPass = new ShaderTagId("VRCFurySpsSocketMarker");
            private static readonly ShaderTagId ResolverPass = new ShaderTagId("VRCFurySpsResolver");
            private static readonly int Grid56 = Shader.PropertyToID("_VFGrid56");
            private static readonly int Grid56TexelSize = Shader.PropertyToID("_VFGrid56_TexelSize");
            private static readonly int GridFinal = Shader.PropertyToID("_VFGridFinal");
            private static readonly int GridFinalTexelSize = Shader.PropertyToID("_VFGridFinal_TexelSize");
            private static readonly RenderTargetIdentifier Grid56Target = new RenderTargetIdentifier(Grid56);
            private static readonly RenderTargetIdentifier GridFinalTarget = new RenderTargetIdentifier(GridFinal);

            private Settings settings;
            private FilteringSettings filteringSettings;

            public SpsTransportPass(Settings settings) {
                UpdateSettings(settings);
            }

            public void UpdateSettings(Settings newSettings) {
                settings = newSettings ?? new Settings();
                renderPassEvent = settings.renderPassEvent;
                filteringSettings = new FilteringSettings(RenderQueueRange.all, settings.layerMask.value);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
                var camera = renderingData.cameraData.camera;
                if (camera == null) return;
                if (settings.skipPreviewCameras && camera.cameraType == CameraType.Preview) return;

                var descriptor = GetDescriptor(renderingData.cameraData.cameraTargetDescriptor);
                if (descriptor.width <= 0 || descriptor.height <= 0) return;

                var cmd = CommandBufferPool.Get("VRCFury SPS URP Transport");
                try {
                    cmd.GetTemporaryRT(Grid56, descriptor, FilterMode.Point);
                    cmd.GetTemporaryRT(GridFinal, descriptor, FilterMode.Point);

                    CoreUtils.SetRenderTarget(cmd, Grid56Target, ClearFlag.Color, Color.clear);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    Draw(context, ref renderingData, SocketPass);

                    PublishTexture(cmd, Grid56, Grid56TexelSize, Grid56Target, descriptor);
                    CoreUtils.SetRenderTarget(cmd, GridFinalTarget, ClearFlag.Color, Color.clear);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    Draw(context, ref renderingData, ResolverPass);

                    PublishTexture(cmd, GridFinal, GridFinalTexelSize, GridFinalTarget, descriptor);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                } finally {
                    CommandBufferPool.Release(cmd);
                }
            }

            public override void OnCameraCleanup(CommandBuffer cmd) {
                if (cmd == null) return;
                cmd.ReleaseTemporaryRT(Grid56);
                cmd.ReleaseTemporaryRT(GridFinal);
            }

            private static RenderTextureDescriptor GetDescriptor(RenderTextureDescriptor cameraDescriptor) {
                var descriptor = cameraDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;
                descriptor.colorFormat = RenderTextureFormat.ARGB32;
                if (descriptor.dimension != TextureDimension.Tex2DArray) {
                    descriptor.dimension = TextureDimension.Tex2D;
                    descriptor.volumeDepth = 1;
                } else if (descriptor.volumeDepth < 1) {
                    descriptor.volumeDepth = 1;
                }
                return descriptor;
            }

            private static void PublishTexture(CommandBuffer cmd, int textureId, int texelSizeId, RenderTargetIdentifier target, RenderTextureDescriptor descriptor) {
                cmd.SetGlobalTexture(textureId, target);
                cmd.SetGlobalVector(texelSizeId, new Vector4(
                    1.0f / descriptor.width,
                    1.0f / descriptor.height,
                    descriptor.width,
                    descriptor.height
                ));
            }

            private void Draw(ScriptableRenderContext context, ref RenderingData renderingData, ShaderTagId shaderTagId) {
                var sortingSettings = new SortingSettings(renderingData.cameraData.camera) {
                    criteria = SortingCriteria.None
                };
                var drawingSettings = new DrawingSettings(shaderTagId, sortingSettings) {
                    enableDynamicBatching = false,
                    enableInstancing = true,
                    perObjectData = PerObjectData.None
                };
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
            }
        }
    }
}
