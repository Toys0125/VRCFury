using UnityEngine;

namespace com.vrcfury.integration.basis {
    /// <summary>
    /// Explicit opt-in marker for Basis-authored objects that should be converted into
    /// VRCFury Armature Link requests. BasisLockToBone may be used as a role data
    /// source on the same GameObject, but it is not consumed unless this marker exists.
    /// </summary>
    [AddComponentMenu("VRCFury/Basis Armature Link")]
    [DisallowMultipleComponent]
    public class BasisVrcfuryArmatureLink : MonoBehaviour {
        public bool useBasisLockToBoneRole = true;
        public HumanBodyBones fallbackBone = HumanBodyBones.Hips;
        public GameObject explicitTarget;
        public bool recursive = false;
        public bool alignPosition = true;
        public bool alignRotation = true;
        public bool alignScale = false;
        public bool removeParentConstraints = true;
    }
}
