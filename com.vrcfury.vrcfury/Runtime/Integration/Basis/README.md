# BasisVR Armature Link Adapter

This optional adapter registers a `FuryArmatureLinkHooks.CollectArmatureLinks` collector from an Editor-only assembly. It uses reflection only: there are no compile-time references to BasisVR or VRCSDK types in the adapter source.

Current behavior:

1. Detect a Basis avatar by looking for `Basis.Scripts.BasisSdk.BasisAvatar` on the avatar root, its parents, or its children.
2. Scan child objects for the explicit `BasisVrcfuryArmatureLink` opt-in marker.
3. If the marker has `explicitTarget`, link to that object. Otherwise, optionally read a `Basis.Scripts.TransformBinders.BasisLockToBone` component on the same GameObject and map its `Role` to `HumanBodyBones`.
4. Emit a normal VRCFury external armature-link request from the marked GameObject to the mapped humanoid bone or fallback bone.

Unsupported/special roles such as `CenterEye` fall back to the marker's configured fallback bone and log a warning. Unmarked `BasisLockToBone` components are intentionally ignored because they are runtime follow components, not explicit VRCFury armature-link authoring metadata.
