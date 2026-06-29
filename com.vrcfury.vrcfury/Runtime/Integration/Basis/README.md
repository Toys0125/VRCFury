# BasisVR Armature Link Adapter

This optional adapter registers a `FuryArmatureLinkHooks.CollectArmatureLinks` collector from an Editor-only assembly. It uses reflection only: there are no compile-time references to BasisVR or VRCSDK types in the adapter source.

Current behavior:

1. Detect a Basis avatar by looking for `Basis.Scripts.BasisSdk.BasisAvatar` on the avatar root, its parents, or its children.
2. Scan child components with type name `Basis.Scripts.TransformBinders.BasisLockToBone`.
3. Read the component's `Role` field/property and map the Basis role name to `HumanBodyBones`.
4. Emit a normal VRCFury external armature-link request from the `BasisLockToBone` GameObject to the mapped humanoid bone.

Unsupported/special roles such as `CenterEye` are skipped until Basis provides explicit authoring metadata for the target transform.
