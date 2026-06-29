using UnityEngine;
using UnityEngine.Rendering;

namespace VF.Builder.Haptics {
    internal enum SpsRenderPipelineMode {
        BuiltIn,
        Universal,
        OtherScriptableRenderPipeline
    }

    /// <summary>
    /// Centralized render-pipeline detection for the SPSv2 URP port. This file avoids
    /// direct URP assembly references so VRCFury continues to compile in projects where
    /// URP is not installed.
    /// </summary>
    internal static class SpsRenderPipelineSupport {
        public static SpsRenderPipelineMode GetCurrentMode() {
            var asset = GraphicsSettings.renderPipelineAsset;
            if (asset == null) return SpsRenderPipelineMode.BuiltIn;

            var type = asset.GetType();
            var fullName = type.FullName ?? "";
            var typeName = type.Name ?? "";
            if (fullName.Contains("UniversalRenderPipelineAsset") || typeName.Contains("UniversalRenderPipelineAsset")) {
                return SpsRenderPipelineMode.Universal;
            }

            return SpsRenderPipelineMode.OtherScriptableRenderPipeline;
        }

        public static string GetCurrentPipelineName() {
            var asset = GraphicsSettings.renderPipelineAsset;
            if (asset == null) return "Built-in Render Pipeline";
            return asset.GetType().FullName ?? asset.GetType().Name;
        }

        public static bool IsUrp() {
            return GetCurrentMode() == SpsRenderPipelineMode.Universal;
        }
    }
}
