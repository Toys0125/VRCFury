using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.vrcfury.api.Integration {
    /// <summary>
    /// Integration point for non-VRChat avatar runtimes that want VRCFury's existing
    /// Armature Link builder to run without depending on VRCAvatarDescriptor-specific
    /// authoring UI.
    ///
    /// BasisVR should register a collector during editor initialization and return one
    /// request per object/armature that should be linked into the avatar skeleton.
    /// VRCFury consumes these requests during the avatar build before ArmatureLink runs.
    /// </summary>
    public static class FuryArmatureLinkHooks {
        public delegate IEnumerable<Request> CollectArmatureLinksDelegate(GameObject avatarRoot);

        /// <summary>
        /// Called by VRCFury during avatar processing. Implementations should be pure
        /// discovery: do not mutate hierarchy, materials, clips, or renderer state here.
        /// </summary>
        public static event CollectArmatureLinksDelegate CollectArmatureLinks;

        public sealed class Target {
            public bool useBone = true;
            public HumanBodyBones bone = HumanBodyBones.Hips;
            public bool useObject = false;
            public GameObject obj;
            public string offset = "";
        }

        public sealed class Request {
            /// <summary>Human-readable integration/source name for error messages.</summary>
            public string source = "External Armature Link Hook";

            /// <summary>
            /// Object that should own the generated VRCFury feature. Defaults to
            /// <see cref="linkFrom"/> when left null.
            /// </summary>
            public GameObject componentRoot;

            /// <summary>
            /// Prop/clothing root or armature bone to link from. This maps to VRCFury's
            /// Armature Link "Link From" field.
            /// </summary>
            public GameObject linkFrom;

            /// <summary>
            /// Ordered fallback targets. The first target that resolves on the avatar is used.
            /// </summary>
            public List<Target> linkTo = new List<Target>();

            public string removeBoneSuffix = "";
            public bool removeParentConstraints = true;
            public string forceMergedName = "";
            public bool forceOneWorldScale = false;
            public bool recursive = false;
            public bool alignPosition = false;
            public bool alignRotation = false;
            public bool alignScale = false;
            public bool autoScaleFactor = true;
            public bool scalingFactorPowersOf10Only = true;
            public float skinRewriteScalingFactor = 1;
        }

        public static IList<Request> InvokeCollectors(GameObject avatarRoot) {
            var output = new List<Request>();
            var collectors = CollectArmatureLinks;
            if (collectors == null) return output;

            foreach (CollectArmatureLinksDelegate collector in collectors.GetInvocationList()) {
                IEnumerable<Request> requests;
                try {
                    requests = collector(avatarRoot);
                } catch (Exception e) {
                    Debug.LogException(new Exception("VRCFury armature link hook collector failed: " + collector.Method.DeclaringType + "." + collector.Method.Name, e));
                    continue;
                }
                if (requests == null) continue;
                foreach (var request in requests) {
                    if (request != null) output.Add(request);
                }
            }

            return output;
        }
    }
}
