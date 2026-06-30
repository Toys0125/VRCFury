using System;
using System.IO;
using NUnit.Framework;

[Category("VRCFury")]
public class BasisUrpIntegrationStaticTests {
    [Test]
    public void UrpMarkerShadersDoNotUseGrabPass() {
        var socket = ReadPackageFile("SPS/URP/sps_socket_urp.shader");
        var resolver = ReadPackageFile("SPS/URP/sps_resolver_urp.shader");

        Assert.That(socket, Does.Not.Contain("GrabPass"));
        Assert.That(resolver, Does.Not.Contain("GrabPass"));
        Assert.That(socket, Does.Contain("\"RenderPipeline\" = \"UniversalPipeline\""));
        Assert.That(resolver, Does.Contain("\"RenderPipeline\" = \"UniversalPipeline\""));
        Assert.That(socket, Does.Contain("\"LightMode\" = \"VRCFurySpsSocketMarker\""));
        Assert.That(resolver, Does.Contain("\"LightMode\" = \"VRCFurySpsResolver\""));
    }

    [Test]
    public void UrpAssemblyIsOptionalAndCoreAssembliesDoNotReferenceUrp() {
        var runtimeAsmdef = ReadPackageFile("Runtime/VRCFury.asmdef");
        var editorCommonAsmdef = ReadPackageFile("Editor-Common/VRCFury-Editor-Common.asmdef");
        var urpAsmdef = ReadPackageFile("SPS/URP/Runtime/VRCFury-SPS-URP.asmdef");

        Assert.That(runtimeAsmdef, Does.Not.Contain("Unity.RenderPipelines.Universal"));
        Assert.That(editorCommonAsmdef, Does.Not.Contain("Unity.RenderPipelines.Universal"));
        Assert.That(urpAsmdef, Does.Contain("com.unity.render-pipelines.universal"));
        Assert.That(urpAsmdef, Does.Contain("VRCF_URP"));
    }

    [Test]
    public void UrpRendererFeatureUsesDataTextureFormat() {
        var rendererFeature = ReadPackageFile("SPS/URP/Runtime/VrcfurySpsUrpRendererFeature.cs");
        Assert.That(rendererFeature, Does.Contain("GraphicsFormat.R8G8B8A8_UNorm"));
        Assert.That(rendererFeature, Does.Contain("all-zero grid is the valid"));
    }

    [Test]
    public void BasisAdapterIsReflectionOnly() {
        var adapter = ReadPackageFile("Runtime/Integration/Basis/BasisArmatureLinkAdapter.cs");
        Assert.That(adapter, Does.Not.Contain("using Basis"));
        Assert.That(adapter, Does.Not.Contain("using VRC"));
        Assert.That(adapter, Does.Not.Contain("VRC.SDK"));
        Assert.That(adapter, Does.Contain("Basis.Scripts.BasisSdk.BasisAvatar"));
        Assert.That(adapter, Does.Contain("Basis.Scripts.TransformBinders.BasisLockToBone"));
        Assert.That(adapter, Does.Contain("BasisVrcfuryArmatureLink"));
        Assert.That(adapter, Does.Contain("component as BasisVrcfuryArmatureLink"));
        Assert.That(adapter, Does.Not.Contain("BasisVR BasisLockToBone"));
    }

    private static string ReadPackageFile(string relativePath) {
        foreach (var root in CandidatePackageRoots()) {
            var path = Path.Combine(root, relativePath).Replace('\\', '/');
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("Could not locate VRCFury package file: " + relativePath);
    }

    private static string[] CandidatePackageRoots() {
        return new[] {
            "Packages/com.vrcfury.vrcfury",
            "../../com.vrcfury.vrcfury",
            "com.vrcfury.vrcfury"
        };
    }
}
