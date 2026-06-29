using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using com.vrcfury.api.Integration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[Category("VRCFury")]
public class FuryArmatureLinkHooksTests {
    [Test]
    public void InvokeCollectorsCatchesIteratorExceptions() {
        var root = new GameObject("VRCFury Hook Test Avatar");
        FuryArmatureLinkHooks.CollectArmatureLinks += ThrowingIteratorCollector;
        try {
            LogAssert.Expect(LogType.Exception, new Regex("VRCFury armature link hook collector failed while enumerating requests"));
            var requests = FuryArmatureLinkHooks.InvokeCollectors(root);
            Assert.That(requests.Count(r => r.source == "throwing iterator test"), Is.EqualTo(1));
        } finally {
            FuryArmatureLinkHooks.CollectArmatureLinks -= ThrowingIteratorCollector;
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static IEnumerable<FuryArmatureLinkHooks.Request> ThrowingIteratorCollector(GameObject avatarRoot) {
        yield return new FuryArmatureLinkHooks.Request {
            source = "throwing iterator test",
            linkFrom = avatarRoot,
            linkTo = new List<FuryArmatureLinkHooks.Target> {
                new FuryArmatureLinkHooks.Target { bone = HumanBodyBones.Hips }
            }
        };
        throw new Exception("collector iterator failure");
    }
}
