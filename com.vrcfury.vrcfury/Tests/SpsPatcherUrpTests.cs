using NUnit.Framework;
using VF.Builder.Haptics;

[Category("VRCFury")]
public class SpsPatcherUrpTests {
    [Test]
    public void UrpPassFilteringPatchesOnlyVisibleDeformationPasses() {
        var urp = SpsRenderPipelineMode.Universal;

        Assert.That(ShouldPatch("UniversalForward", urp), Is.True);
        Assert.That(ShouldPatch("UniversalForwardOnly", urp), Is.True);
        Assert.That(ShouldPatch("UniversalGBuffer", urp), Is.True);
        Assert.That(ShouldPatch("SRPDefaultUnlit", urp), Is.True);
        Assert.That(ShouldPatch(null, urp), Is.True);

        Assert.That(ShouldPatch("ShadowCaster", urp), Is.False);
        Assert.That(ShouldPatch("DepthOnly", urp), Is.False);
        Assert.That(ShouldPatch("DepthNormals", urp), Is.False);
        Assert.That(ShouldPatch("Meta", urp), Is.False);
        Assert.That(ShouldPatch("SceneSelectionPass", urp), Is.False);
        Assert.That(ShouldPatch("MotionVectors", urp), Is.False);
        Assert.That(ShouldPatch("CustomUnsafePass", urp), Is.False);
    }

    [Test]
    public void NonUrpPassFilteringKeepsUpstreamBetaBehavior() {
        var builtIn = SpsRenderPipelineMode.BuiltIn;

        Assert.That(ShouldPatch("ShadowCaster", builtIn), Is.True);
        Assert.That(ShouldPatch("DepthOnly", builtIn), Is.True);
        Assert.That(ShouldPatch("Meta", builtIn), Is.True);
        Assert.That(ShouldPatch(null, builtIn), Is.True);
    }

    [Test]
    public void UrpUsePassFilteringPreservesNonDeformingUtilityPasses() {
        var urp = SpsRenderPipelineMode.Universal;
        var builtIn = SpsRenderPipelineMode.BuiltIn;

        Assert.That(SpsPatcher.ShouldPatchUsePassForSps("ShadowCaster", urp), Is.False);
        Assert.That(SpsPatcher.ShouldPatchUsePassForSps("DepthOnly", urp), Is.False);
        Assert.That(SpsPatcher.ShouldPatchUsePassForSps("UniversalForward", urp), Is.True);
        Assert.That(SpsPatcher.ShouldPatchUsePassForSps("ShadowCaster", builtIn), Is.True);
        Assert.That(SpsPatcher.ShouldPatchUsePassForSps("DepthOnly", builtIn), Is.True);
    }

    private static bool ShouldPatch(string lightMode, SpsRenderPipelineMode mode) {
        var pass = lightMode == null ? "Pass { }" : "Pass { Tags { \"LightMode\" = \"" + lightMode + "\" } }";
        return SpsPatcher.ShouldPatchPassForSps(pass, mode);
    }
}
