using System;
using System.Collections.Generic;
using System.Reflection;
using com.vrcfury.api.Integration;
using UnityEditor;
using UnityEngine;

namespace com.vrcfury.integration.basis {
    /// <summary>
    /// Reflection-only BasisVR adapter for the general VRCFury armature-link hook.
    /// This assembly intentionally does not reference Basis or VRCSDK types. Objects
    /// must opt in with <see cref="BasisVrcfuryArmatureLink"/>; BasisLockToBone is
    /// only used as a role data source on explicitly marked objects.
    /// </summary>
    [InitializeOnLoad]
    internal static class BasisArmatureLinkAdapter {
        private const string BasisAvatarTypeName = "Basis.Scripts.BasisSdk.BasisAvatar";
        private const string BasisLockToBoneTypeName = "Basis.Scripts.TransformBinders.BasisLockToBone";

        static BasisArmatureLinkAdapter() {
            FuryArmatureLinkHooks.CollectArmatureLinks -= Collect;
            FuryArmatureLinkHooks.CollectArmatureLinks += Collect;
        }

        private static IEnumerable<FuryArmatureLinkHooks.Request> Collect(GameObject avatarRoot) {
            if (avatarRoot == null) yield break;

            var childComponents = avatarRoot.GetComponentsInChildren<Component>(true);
            if (!HasBasisAvatar(avatarRoot, childComponents)) yield break;

            var seen = new HashSet<GameObject>();
            foreach (var component in childComponents) {
                var marker = component as BasisVrcfuryArmatureLink;
                if (marker == null) continue;
                if (!seen.Add(marker.gameObject)) continue;

                yield return CreateRequest(marker);
            }
        }

        private static FuryArmatureLinkHooks.Request CreateRequest(BasisVrcfuryArmatureLink marker) {
            return new FuryArmatureLinkHooks.Request {
                source = "BasisVR VRCFury Armature Link",
                componentRoot = marker.gameObject,
                linkFrom = marker.gameObject,
                linkTo = CreateTargets(marker),
                recursive = marker.recursive,
                alignPosition = marker.alignPosition,
                alignRotation = marker.alignRotation,
                alignScale = marker.alignScale,
                removeParentConstraints = marker.removeParentConstraints
            };
        }

        private static List<FuryArmatureLinkHooks.Target> CreateTargets(BasisVrcfuryArmatureLink marker) {
            if (marker.explicitTarget != null) {
                return new List<FuryArmatureLinkHooks.Target> {
                    new FuryArmatureLinkHooks.Target {
                        useBone = false,
                        useObject = true,
                        obj = marker.explicitTarget,
                        offset = ""
                    }
                };
            }

            var bone = marker.fallbackBone;
            string roleName;
            if (marker.useBasisLockToBoneRole && TryGetBasisLockToBoneRoleName(marker.gameObject, out roleName)) {
                if (!TryMapRole(roleName, out bone)) {
                    Debug.LogWarning("Basis VRCFury Armature Link on " + marker.gameObject.name + " has unsupported BasisLockToBone role '" + roleName + "'. Falling back to " + marker.fallbackBone + ".");
                    bone = marker.fallbackBone;
                }
            }

            return new List<FuryArmatureLinkHooks.Target> {
                new FuryArmatureLinkHooks.Target {
                    useBone = true,
                    bone = bone,
                    useObject = false,
                    offset = ""
                }
            };
        }

        private static bool HasBasisAvatar(GameObject avatarRoot, IEnumerable<Component> childComponents) {
            foreach (var component in avatarRoot.GetComponentsInParent<Component>(true)) {
                if (IsBasisAvatar(component)) return true;
            }
            foreach (var component in childComponents) {
                if (IsBasisAvatar(component)) return true;
            }
            return false;
        }

        private static bool IsBasisAvatar(Component component) {
            return component != null && (component.GetType().FullName ?? "") == BasisAvatarTypeName;
        }

        private static bool TryGetBasisLockToBoneRoleName(GameObject obj, out string roleName) {
            roleName = null;
            foreach (var component in obj.GetComponents<Component>()) {
                if (component == null) continue;
                if ((component.GetType().FullName ?? "") != BasisLockToBoneTypeName) continue;
                return TryGetRoleName(component, out roleName);
            }
            return false;
        }

        private static bool TryGetRoleName(Component component, out string roleName) {
            roleName = null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = component.GetType();
            var field = type.GetField("Role", flags);
            var value = field != null ? field.GetValue(component) : null;
            if (value == null) {
                var property = type.GetProperty("Role", flags);
                if (property != null && property.GetIndexParameters().Length == 0) {
                    value = property.GetValue(component, null);
                }
            }
            if (value == null) return false;
            roleName = value.ToString();
            return !string.IsNullOrEmpty(roleName);
        }

        private static bool TryMapRole(string roleName, out HumanBodyBones bone) {
            switch (roleName) {
                case "Head": bone = HumanBodyBones.Head; return true;
                case "Neck": bone = HumanBodyBones.Neck; return true;
                case "Chest": bone = HumanBodyBones.Chest; return true;
                case "Hips": bone = HumanBodyBones.Hips; return true;
                case "Spine": bone = HumanBodyBones.Spine; return true;
                case "LeftUpperLeg": bone = HumanBodyBones.LeftUpperLeg; return true;
                case "RightUpperLeg": bone = HumanBodyBones.RightUpperLeg; return true;
                case "LeftLowerLeg": bone = HumanBodyBones.LeftLowerLeg; return true;
                case "RightLowerLeg": bone = HumanBodyBones.RightLowerLeg; return true;
                case "LeftFoot": bone = HumanBodyBones.LeftFoot; return true;
                case "RightFoot": bone = HumanBodyBones.RightFoot; return true;
                case "LeftShoulder": bone = HumanBodyBones.LeftShoulder; return true;
                case "RightShoulder": bone = HumanBodyBones.RightShoulder; return true;
                case "LeftUpperArm": bone = HumanBodyBones.LeftUpperArm; return true;
                case "RightUpperArm": bone = HumanBodyBones.RightUpperArm; return true;
                case "LeftLowerArm": bone = HumanBodyBones.LeftLowerArm; return true;
                case "RightLowerArm": bone = HumanBodyBones.RightLowerArm; return true;
                case "LeftHand": bone = HumanBodyBones.LeftHand; return true;
                case "RightHand": bone = HumanBodyBones.RightHand; return true;
                case "LeftToes": bone = HumanBodyBones.LeftToes; return true;
                case "RightToes": bone = HumanBodyBones.RightToes; return true;
                case "Mouth": bone = HumanBodyBones.Jaw; return true;
                default:
                    bone = HumanBodyBones.Hips;
                    return false;
            }
        }
    }
}
