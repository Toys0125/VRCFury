using System;
using System.Reflection;
using NUnit.Framework;

[Category("VRCFury")]
public class SpsPatcherUrpTests {
    private static readonly Type PatcherType = Type.GetType("VF.Builder.Haptics.SpsPatcher, VRCFury-Editor-Common", true);
    private static readonly Type ModeType = Type.GetType("VF.Builder.Haptics.SpsRenderPipelineMode, VRCFury-Editor-Common", true);
    private static readonly MethodInfo ShouldSkipPass = PatcherType.GetMethod("ShouldSkipPassForSps", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly MethodInfo ShouldSkipUsePass = PatcherType.GetMethod("ShouldSkipUsePassForSps", BindingFlags.NonPublic | BindingFlags.Static);

    [Test]
    public void UrpPassFilteringAllowsOnlyVisibleDeformationPasses() {
        var urp = Enum.Parse(ModeType, "Universal");

        Assert.That(ShouldSkip("UniversalForward", urp), Is.False);
        Assert.That(ShouldSkip("UniversalForwardOnly", urp), Is.False);
        Assert.That(ShouldSkip("UniversalGBuffer", urp), Is.False);
        Assert.That(ShouldSkip("SRPDefaultUnlit", urp), Is.False);
        Assert.That(ShouldSkip(null, urp), Is.False);

        Assert.That(ShouldSkip("ShadowCaster", urp), Is.True);
        Assert.That(ShouldSkip("DepthOnly", urp), Is.True);
        Assert.That(ShouldSkip("DepthNormals", urp), Is.True);
        Assert.That(ShouldSkip("Meta", urp), Is.True);
        Assert.That(ShouldSkip("SceneSelectionPass", urp), Is.True);
        Assert.That(ShouldSkip("MotionVectors", urp), Is.True);
        Assert.That(ShouldSkip("CustomUnsafePass", urp), Is.True);
    }

    [Test]
    public void BuiltInPassFilteringPreservesLegacyBehavior() {
        var builtIn = Enum.Parse(ModeType, "BuiltIn");

        Assert.That(ShouldSkip("ShadowCaster", builtIn), Is.True);
        Assert.That(ShouldSkip("DepthOnly", builtIn), Is.False);
        Assert.That(ShouldSkip(null, builtIn), Is.False);
    }

    [Test]
    public void UrpUsePassFilteringSkipsNonDeformingUtilityPasses() {
        var urp = Enum.Parse(ModeType, "Universal");
        var builtIn = Enum.Parse(ModeType, "BuiltIn");

        Assert.That(ShouldSkipUsePass.Invoke(null, new[] { "ShadowCaster", urp }), Is.True);
        Assert.That(ShouldSkipUsePass.Invoke(null, new[] { "DepthOnly", urp }), Is.True);
        Assert.That(ShouldSkipUsePass.Invoke(null, new[] { "UniversalForward", urp }), Is.False);
        Assert.That(ShouldSkipUsePass.Invoke(null, new[] { "DepthOnly", builtIn }), Is.False);
    }

    private static bool ShouldSkip(string lightMode, object mode) {
        var pass = lightMode == null ? "Pass { }" : "Pass { Tags { \"LightMode\" = \"" + lightMode + "\" } }";
        return (bool)ShouldSkipPass.Invoke(null, new[] { pass, mode });
    }
}
