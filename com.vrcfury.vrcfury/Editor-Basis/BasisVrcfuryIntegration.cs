using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Basis.Scripts.BasisSdk;
using UnityEditor;
using UnityEngine;
using VF;
using VF.Builder;
using VF.Component;
using VF.Model;
using VRC.SDK3.Avatars.Components;

namespace VF.Integration.Basis {
    [InitializeOnLoad]
    internal static class BasisVrcfuryIntegration {
        private static bool runningBuildHook;

        static BasisVrcfuryIntegration() {
            BasisAssetBundlePipeline.OnBeforeBuildPrefab -= BeforeBasisPrefabBuild;
            BasisAssetBundlePipeline.OnBeforeBuildPrefab += BeforeBasisPrefabBuild;
        }

        [MenuItem("Tools/VRCFury/BasisVR/Generate Compatibility For Selected Avatar")]
        private static void GenerateSelected() {
            var selected = Selection.activeGameObject;
            if (selected == null) {
                EditorUtility.DisplayDialog("VRCFury BasisVR", "Select a VRChat/Basis avatar or an object inside one.", "OK");
                return;
            }

            try {
                var avatar = ResolveOrCreateBasisAvatar(selected, out var created);
                if (avatar == null) {
                    EditorUtility.DisplayDialog("VRCFury BasisVR", "No BasisAvatar or VRCAvatarDescriptor could be found from the selection.", "OK");
                    return;
                }
                var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
                RefreshBasisAvatarMetadata(avatar, descriptor, overwriteExisting: created);
                var report = BasisVrcfuryConverter.Generate(avatar, buildClone: false);
                report.CreatedBasisAvatar = created;
                LogReport(avatar.gameObject, report);
                EditorUtility.DisplayDialog("VRCFury BasisVR", report.DialogSummary() +
                    "\n\nOriginal VRCFury and VRChat authoring components were kept. Only the generated Basis/Vixxy root is replaced on regeneration.", "OK");
            } catch (Exception e) {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("VRCFury BasisVR", "Compatibility generation failed. See the Console.\n\n" + e.Message, "OK");
            }
        }

        [MenuItem("Tools/VRCFury/BasisVR/Remove Generated Compatibility From Selected Avatar")]
        private static void RemoveSelected() {
            var selected = Selection.activeGameObject;
            if (selected == null) return;
            var avatar = selected.GetComponentInParent<BasisAvatar>(true) ?? selected.GetComponent<BasisAvatar>();
            if (avatar == null) return;
            var removed = BasisVrcfuryConverter.RemoveGenerated(avatar.gameObject);
            if (removed > 0) Debug.Log($"VRCFury BasisVR: removed {removed} generated compatibility root(s) from {avatar.name}.", avatar);
        }

        private static void BeforeBasisPrefabBuild(GameObject prefab, BasisAssetBundleObject settings) {
            if (runningBuildHook || prefab == null) return;
            var avatar = prefab.GetComponent<BasisAvatar>();
            if (avatar == null || prefab.GetComponentsInChildren<VRCFuryComponent>(true).Length == 0) return;

            runningBuildHook = true;
            try {
                var descriptor = prefab.GetComponent<VRCAvatarDescriptor>();
                if (descriptor == null) {
                    Debug.LogWarning("VRCFury BasisVR: VRCFury authoring exists, but this Basis build clone has no VRCAvatarDescriptor. " +
                                     "Vixxy-compatible features will be converted, but VRCFury's build-time processors cannot be reused.", prefab);
                }

                UpgradeCloneVrcfury(prefab);
                RefreshBasisAvatarMetadata(avatar, descriptor, overwriteExisting: false);
                var report = BasisVrcfuryConverter.Generate(avatar, buildClone: true);

                SuppressDeferredSpsAndHaptics(prefab, report);

                if (descriptor != null && VRCFuryBuilder.ShouldRun(prefab.asVf())) {
                    VRCFuryBuilder.RunMain(prefab.asVf());
                }

                descriptor = prefab.GetComponent<VRCAvatarDescriptor>();
                RefreshBasisAvatarMetadata(avatar, descriptor, overwriteExisting: true);
                TranslateHeadChopComponents(prefab, report);
                StripVrcfuryAndVrchatRuntimeComponents(prefab);
                LogReport(prefab, report);
            } finally {
                runningBuildHook = false;
            }
        }

        private static void UpgradeCloneVrcfury(GameObject root) {
            foreach (var component in root.GetComponentsInChildren<VRCFuryComponent>(true).ToArray()) {
                if (component != null) component.Upgrade();
            }
        }

        private static void SuppressDeferredSpsAndHaptics(GameObject root, BasisVrcfuryConversionReport report) {
            foreach (var fury in root.GetComponentsInChildren<VRCFury>(true).ToArray()) {
                if (fury == null || fury.content == null) continue;
                if (!BasisVrcfuryConverter.IsSpsOrHapticFeature(fury.content)) continue;
                report.DeferredFeature(fury.content.GetType().Name);
                UnityEngine.Object.DestroyImmediate(fury);
            }

            foreach (var component in root.GetComponentsInChildren<VRCFuryComponent>(true).ToArray()) {
                if (component == null || component is VRCFury) continue;
                var name = component.GetType().Name;
                if (name.Contains("Haptic", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Sps", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("GlobalCollider", StringComparison.OrdinalIgnoreCase)) {
                    report.DeferredFeature(name);
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
        }

        private static BasisAvatar ResolveOrCreateBasisAvatar(GameObject selected, out bool created) {
            created = false;
            var existing = selected.GetComponentInParent<BasisAvatar>(true) ?? selected.GetComponent<BasisAvatar>();
            if (existing != null) return existing;

            VRCAvatarDescriptor descriptor = null;
            for (var t = selected.transform; t != null; t = t.parent) {
                descriptor = t.GetComponent<VRCAvatarDescriptor>();
                if (descriptor != null) break;
            }
            if (descriptor == null) {
                descriptor = selected.transform.root.GetComponentInChildren<VRCAvatarDescriptor>(true);
            }
            if (descriptor == null) return null;

            var avatar = Undo.AddComponent<BasisAvatar>(descriptor.gameObject);
            created = true;
            return avatar;
        }

        private static void RefreshBasisAvatarMetadata(BasisAvatar avatar, VRCAvatarDescriptor descriptor, bool overwriteExisting) {
            if (avatar == null) return;
            if (avatar.Animator == null) avatar.Animator = avatar.GetComponent<Animator>();
            if (avatar.Renders == null || avatar.Renders.Length == 0 || overwriteExisting) {
                avatar.Renders = avatar.GetComponentsInChildren<Renderer>(true);
            }
            if (avatar.Animator != null && avatar.Animator.isHuman) {
                if (overwriteExisting || avatar.TransformStorage == null || !avatar.TransformStorage.HasData) {
                    avatar.TransformStorage = BasisAvatarTransformStorage.CaptureFrom(avatar.Animator);
                }
                if (overwriteExisting || avatar.HumanScale <= 0f) avatar.HumanScale = avatar.Animator.humanScale;
            }
            if (descriptor == null) {
                EditorUtility.SetDirty(avatar);
                return;
            }

            if (overwriteExisting || avatar.AvatarEyePosition == Vector2.zero) {
                var view = descriptor.ViewPosition;
                avatar.AvatarEyePosition = new Vector2(view.y, view.z);
            }

            if (descriptor.VisemeSkinnedMesh != null && (overwriteExisting || avatar.FaceVisemeMesh == null)) {
                avatar.FaceVisemeMesh = descriptor.VisemeSkinnedMesh;
            }
            if (avatar.FaceVisemeMesh != null && avatar.FaceVisemeMesh.sharedMesh != null && descriptor.VisemeBlendShapes != null) {
                if (overwriteExisting || avatar.FaceVisemeMovement == null || avatar.FaceVisemeMovement.Length != 15 || avatar.FaceVisemeMovement.All(i => i < 0)) {
                    avatar.FaceVisemeMovement = Enumerable.Repeat(-1, 15).ToArray();
                    for (var i = 0; i < Math.Min(15, descriptor.VisemeBlendShapes.Length); i++) {
                        var name = descriptor.VisemeBlendShapes[i];
                        if (!string.IsNullOrWhiteSpace(name)) avatar.FaceVisemeMovement[i] = avatar.FaceVisemeMesh.sharedMesh.GetBlendShapeIndex(name);
                    }
                }
            }

            // Use reflection for eyelid metadata because the field names have changed across supported VRCSDK versions.
            if (BasisVrcfuryUtil.TryReadMember(descriptor, "eyelidsSkinnedMesh", out SkinnedMeshRenderer blinkMesh) && blinkMesh != null &&
                BasisVrcfuryUtil.TryReadMember(descriptor, "eyelidsBlendshapes", out int[] blinkShapes) && blinkShapes != null && blinkShapes.Length > 0) {
                if (overwriteExisting || avatar.FaceBlinkMesh == null) avatar.FaceBlinkMesh = blinkMesh;
                if (overwriteExisting || avatar.BlinkViseme == null || avatar.BlinkViseme.All(i => i < 0)) avatar.BlinkViseme = (int[])blinkShapes.Clone();
            }
            EditorUtility.SetDirty(avatar);
        }

        private static void TranslateHeadChopComponents(GameObject root, BasisVrcfuryConversionReport report) {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true).ToArray()) {
                if (behaviour == null || behaviour.GetType().FullName != "VRC.SDK3.Avatars.Components.VRCHeadChop") continue;
                if (!BasisVrcfuryUtil.TryReadMember(behaviour, "targetBones", out Array targetBones) || targetBones == null) continue;

                var targets = new List<BasisHeadChop.HeadChopTarget>();
                foreach (var entry in targetBones) {
                    if (entry == null || !BasisVrcfuryUtil.TryReadMember(entry, "transform", out Transform target) || target == null) continue;
                    var scale = 0f;
                    BasisVrcfuryUtil.TryReadMember(entry, "scaleFactor", out scale);
                    targets.Add(new BasisHeadChop.HeadChopTarget { Target = target, Scale = scale });
                }
                if (targets.Count == 0) continue;
                var basis = behaviour.GetComponent<BasisHeadChop>() ?? behaviour.gameObject.AddComponent<BasisHeadChop>();
                basis.Targets = (basis.Targets ?? Array.Empty<BasisHeadChop.HeadChopTarget>()).Concat(targets).ToArray();
                report.ConvertedBindings += targets.Count;
            }
        }

        private static void StripVrcfuryAndVrchatRuntimeComponents(GameObject root) {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true).ToArray()) {
                if (behaviour == null || behaviour is BasisAvatar) continue;
                var type = behaviour.GetType();
                var assemblyName = type.Assembly.GetName().Name ?? string.Empty;
                var ns = type.Namespace ?? string.Empty;
                if (assemblyName == "VRCFury" || ns.StartsWith("VRC.", StringComparison.Ordinal) || ns.StartsWith("VRCSDK", StringComparison.Ordinal)) {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
            }
        }

        private static void LogReport(GameObject avatar, BasisVrcfuryConversionReport report) {
            Debug.Log(report.Summary(avatar), avatar);
            foreach (var warning in report.Warnings) Debug.LogWarning("VRCFury BasisVR: " + warning, avatar);
        }
    }
}
