﻿using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using Object = UnityEngine.Object;

namespace VF.Feature {

    public class ArmatureLinkBuilder : FeatureBuilder<ArmatureLink> {
        private Dictionary<string, string> clipMappings = new Dictionary<string, string>();
        
        [FeatureBuilderAction(-1)]
        public void CollectClipMappings() {
            var links = GetLinks();
            foreach (var mergeBone in links.mergeBones) {
                var propBone = mergeBone.Item1;
                var avatarBone = mergeBone.Item2;
                var oldPath = motions.GetPath(propBone);
                var newPath = ClipBuilder.Join(motions.GetPath(avatarBone), "vrcf_" + uniqueModelNum + "_" + propBone.name);
                clipMappings.Add(oldPath, newPath);
            }
        }
        
        private string RewriteClipPath(string path) {
            foreach (var pair in clipMappings) {
                if (path.StartsWith(pair.Key + "/") || path == pair.Key) {
                    return pair.Value + path.Substring(pair.Key.Length);
                }
            }
            return null;
        }
        
        [FeatureBuilderAction]
        public void Link() {
            var links = GetLinks();
            foreach (var mergeBone in links.mergeBones) {
                var propBone = mergeBone.Item1;
                var avatarBone = mergeBone.Item2;
                var p = propBone.GetComponent<ParentConstraint>();
                if (p != null) Object.DestroyImmediate(p);
                p = propBone.AddComponent<ParentConstraint>();
                p.AddSource(new ConstraintSource() {
                    sourceTransform = avatarBone.transform,
                    weight = 1
                });
                p.weight = 1;
                p.constraintActive = true;
                p.locked = true;
                if (model.keepBoneOffsets) {
                    Matrix4x4 inverse = Matrix4x4.TRS(avatarBone.transform.position, avatarBone.transform.rotation, new Vector3(1,1,1)).inverse;
                    p.SetTranslationOffset(0, inverse.MultiplyPoint3x4(p.transform.position));
                    p.SetRotationOffset(0, (Quaternion.Inverse(avatarBone.transform.rotation) * p.transform.rotation).eulerAngles);
                }
            }
        }
        
        [FeatureBuilderAction(applyToVrcClone:true)]
        public void LinkOnVrcClone() {
            var links = GetLinks();
            foreach (var mergeBone in links.mergeBones) {
                var propBone = mergeBone.Item1;
                var avatarBone = mergeBone.Item2;
                var p = propBone.GetComponent<ParentConstraint>();
                if (p != null) Object.DestroyImmediate(p);
                propBone.name = "vrcf_" + uniqueModelNum + "_" + propBone.name;
                propBone.transform.SetParent(avatarBone.transform);
                if (!model.keepBoneOffsets) {
                    propBone.transform.localPosition = Vector3.zero;
                    propBone.transform.localRotation = Quaternion.identity;
                }
            }
        }
        
        [FeatureBuilderAction(100)]
        public void FixAnimations() {
            foreach (var layer in controller.GetManagedLayers()) {
                DefaultClipBuilder.ForEachClip(layer, clip => {
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
                        var newPath = RewriteClipPath(binding.path);
                        if (newPath != null) {
                            var b = binding;
                            b.path = newPath;
                            AnimationUtility.SetEditorCurve(clip, b, AnimationUtility.GetEditorCurve(clip, binding));
                        }
                    }
                    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
                        var newPath = RewriteClipPath(binding.path);
                        if (newPath != null) {
                            var b = binding;
                            b.path = newPath;
                            AnimationUtility.SetObjectReferenceCurve(clip, b, AnimationUtility.GetObjectReferenceCurve(clip, binding));
                        }
                    }
                });
            }
        }

        private class Links {
            // These are stacks, because it's convenient, and we want to iterate over them in reverse order anyways
            // because when operating on the vrc clone, we delete game objects as we process them, and we want to
            // delete the children first.
            // left=bone in prop | right=bone in avatar
            public readonly Stack<Tuple<GameObject, GameObject>> mergeBones
                = new Stack<Tuple<GameObject, GameObject>>();
        }

        private Links GetLinks() {
            var links = new Links();

            if (model.propBone == null) {
                Debug.LogWarning("Root bone is null on armature link.");
                return links;
            }
            var propBone = model.propBone;

            GameObject avatarBone = null;
            if (string.IsNullOrWhiteSpace(model.bonePathOnAvatar)) {
                foreach (Transform child in avatarObject.transform) {
                    var skin = child.gameObject.GetComponent<SkinnedMeshRenderer>();
                    if (skin != null) {
                        avatarBone = skin.rootBone.gameObject;
                        break;
                    }
                }

                if (avatarBone == null) {
                    Debug.LogError("Failed to find root bone on avatar. Skipping armature link.");
                    return links;
                }
            } else {
                avatarBone = avatarObject.transform.Find(model.bonePathOnAvatar).gameObject;
                if (avatarBone == null) {
                    Debug.LogError("Failed to find " + model.bonePathOnAvatar + " bone on avatar. Skipping armature link.");
                    return links;
                }
            }

            var checkStack = new Stack<Tuple<GameObject, GameObject>>();
            checkStack.Push(Tuple.Create(propBone, avatarBone));
            links.mergeBones.Push(Tuple.Create(propBone, avatarBone));

            while (checkStack.Count > 0) {
                var check = checkStack.Pop();
                foreach (Transform child in check.Item1.transform) {
                    var childPropBone = child.gameObject;
                    var childAvatarBone = check.Item2.transform.Find(childPropBone.name)?.gameObject;
                    if (childAvatarBone != null) {
                        links.mergeBones.Push(Tuple.Create(childPropBone, childAvatarBone));
                        checkStack.Push(Tuple.Create(childPropBone, childAvatarBone));
                    }
                }
            }

            return links;
        }

        public override string GetEditorTitle() {
            return "Armature Link";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var container = new VisualElement();
            container.Add(new Label("Root bone in the prop:"));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("propBone")));

            container.Add(new Label("Path to corresponding root bone from root of avatar:") {
                style = { paddingTop = 10 }
            });
            container.Add(new Label("Leave empty to default to avatar's skin root bone (usually hips)"));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("bonePathOnAvatar")));

            container.Add(new Label("Keep bone offsets:") {
                style = { paddingTop = 10 }
            });
            container.Add(VRCFuryEditorUtils.WrappedLabel("If false, linked bones will be rigidly locked to the transform of the corresponding avatar bone." +
                                             " If true, prop bones will maintain their initial offset to the corresponding avatar bone. This is unusual."));
            container.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("keepBoneOffsets")));
            
            return container;
        }
    }
}
