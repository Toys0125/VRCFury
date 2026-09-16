using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Basis.Scripts.BasisSdk;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;

namespace VF.Integration.Basis.Shim {
    internal static class BasisVrcfuryAuthoringMenus {
        private const string ComponentRoot = "Component/VRCFury/";
        private const string ToolsRoot = "Tools/VRCFury/BasisVR/";
        // Keep the VRCFury submenu with Unity/package components instead of pinning it
        // to the top of Component. Unity's built-in component entries use the default
        // priority range, while the old 0-3 priorities forced this submenu first.
        private const int ComponentMenuPriority = 500;
        internal const string ArmatureLinkMenuPath = ComponentRoot + "Armature Link (VRCFury)";
        internal const string BlendshapeOptimizerMenuPath = ComponentRoot + "Blendshape Optimizer (VRCFury)";
        internal const string MmdCompatibilityMenuPath = ComponentRoot + "MMD Compatibility (VRCFury)";
        internal const string ApplyDuringUploadMenuPath = ComponentRoot + "Apply During Upload (VRCFury)";

        [MenuItem(ToolsRoot + "Status", priority = 0)]
        private static void Status() {
            EditorUtility.DisplayDialog(
                "VRCFury for BasisVR",
                "The BasisVR compatibility layer is active.\n\n" +
                "Supported VRCFury authoring in this build:\n" +
                "• Armature Link\n" +
                "• Blendshape Optimizer\n" +
                "• MMD Compatibility\n" +
                "• Apply During Upload\n\n" +
                "These are normal VRCFury feature components and are processed only on the temporary Basis build clone.",
                "OK"
            );
        }

        [MenuItem(ArmatureLinkMenuPath, false, ComponentMenuPriority)]
        private static void AddArmatureLink() {
            foreach (var selected in Selection.gameObjects) {
                if (selected == null) continue;
                var guessed = GuessLinkFrom(selected);
                var model = new ArmatureLink {
                    propBone = guessed,
                    recursive = false,
                    alignPosition = false,
                    alignRotation = false,
                    alignScale = false,
                    autoScaleFactor = true,
                    scalingFactorPowersOf10Only = true,
                    skinRewriteScalingFactor = 1
                };
                UpdateOnLinkFromChange(model, null, guessed);
                AddFeature(selected, model, "Add VRCFury Armature Link");
            }
        }

        [MenuItem(ArmatureLinkMenuPath, true)]
        private static bool ValidateAddArmatureLink() => Selection.gameObjects.Any(obj => obj != null);

        [MenuItem(BlendshapeOptimizerMenuPath, false, ComponentMenuPriority + 1)]
        private static void AddBlendshapeOptimizer() {
            foreach (var selected in Selection.gameObjects) {
                if (selected == null) continue;
                AddFeature(selected, new BlendshapeOptimizer(), "Add VRCFury Blendshape Optimizer");
            }
        }

        [MenuItem(BlendshapeOptimizerMenuPath, true)]
        private static bool ValidateAddBlendshapeOptimizer() => Selection.gameObjects.Any(obj => obj != null);

        [MenuItem(MmdCompatibilityMenuPath, false, ComponentMenuPriority + 2)]
        private static void AddMmdCompatibility() {
            foreach (var selected in Selection.gameObjects) {
                if (selected == null) continue;
                AddFeature(selected, new MmdCompatibility(), "Add VRCFury MMD Compatibility");
            }
        }

        [MenuItem(MmdCompatibilityMenuPath, true)]
        private static bool ValidateAddMmdCompatibility() => Selection.gameObjects.Any(obj => obj != null);

        [MenuItem(ApplyDuringUploadMenuPath, false, ComponentMenuPriority + 3)]
        private static void AddApplyDuringUpload() {
            foreach (var selected in Selection.gameObjects) {
                if (selected == null) continue;
                AddFeature(selected, new ApplyDuringUpload { action = new State() }, "Add VRCFury Apply During Upload");
            }
        }

        [MenuItem(ApplyDuringUploadMenuPath, true)]
        private static bool ValidateAddApplyDuringUpload() => Selection.gameObjects.Any(obj => obj != null);

        internal static VRCFury AddFeature(GameObject target, FeatureModel feature, string undoName) {
            if (target == null || feature == null) return null;
            var component = Undo.AddComponent<VRCFury>(target);
            var so = new SerializedObject(component);
            so.FindProperty("content").managedReferenceValue = feature;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            VRCFury.MarkDirty(component);
            Selection.activeObject = component;
            return component;
        }

        internal static GameObject GuessLinkFrom(GameObject componentObject) {
            if (componentObject == null) return null;

            var avatar = componentObject.GetComponentInParent<BasisAvatar>();
            if (avatar != null && avatar.gameObject == componentObject) return null;

            if (avatar != null && avatar.Animator != null) {
                var avatarHips = avatar.Animator.GetBoneTransform(HumanBodyBones.Hips);
                if (avatarHips != null) {
                    var path = GetPath(avatarHips, avatar.transform);
                    if (!string.IsNullOrEmpty(path)) {
                        var found = componentObject.transform.Find(path);
                        if (found != null) return found.gameObject;
                    }
                }
            }

            var possibleArmatures = new List<Transform>();
            var name = componentObject.name.ToLowerInvariant();
            if (name.Contains("armature") || name.Contains("skeleton")) possibleArmatures.Add(componentObject.transform);
            for (var i = 0; i < componentObject.transform.childCount; i++) {
                var child = componentObject.transform.GetChild(i);
                var childName = child.name.ToLowerInvariant();
                if (childName.Contains("armature") || childName.Contains("skeleton")) possibleArmatures.Add(child);
            }

            foreach (var armature in possibleArmatures) {
                for (var i = 0; i < armature.childCount; i++) {
                    var child = armature.GetChild(i);
                    if (child.name.IndexOf("hip", StringComparison.OrdinalIgnoreCase) >= 0) return child.gameObject;
                }
            }

            return componentObject;
        }

        internal static void UpdateOnLinkFromChange(ArmatureLink model, GameObject before, GameObject after) {
            if (model == null || after == null) return;
            var skinAfter = HasExternalSkinBoneReference(after.transform);
            if (before == null || HasExternalSkinBoneReference(before.transform) != skinAfter) {
                model.alignPosition = model.alignRotation = model.alignScale = skinAfter;
                model.recursive = skinAfter;
                model.autoScaleFactor = true;
                model.scalingFactorPowersOf10Only = true;
                model.skinRewriteScalingFactor = 1;
            }
        }

        private static bool HasExternalSkinBoneReference(Transform obj) {
            if (obj == null) return false;
            var avatar = obj.GetComponentInParent<BasisAvatar>();
            var root = avatar != null ? avatar.transform : obj.root;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if (skin == null || skin.transform.IsChildOf(obj)) continue;
                if (skin.rootBone != null && skin.rootBone.IsChildOf(obj)) return true;
                if (skin.bones != null && skin.bones.Any(bone => bone != null && bone.IsChildOf(obj))) return true;
            }
            return false;
        }

        internal static string GetPath(Transform child, Transform root) {
            if (child == null || root == null || child == root) return string.Empty;
            var names = new Stack<string>();
            var current = child;
            while (current != null && current != root) {
                names.Push(current.name);
                current = current.parent;
            }
            return current == root ? string.Join("/", names) : string.Empty;
        }
    }

    // AddComponentMenu does not expose a priority, so Unity registers the runtime
    // VRCFury components before the authored feature entries and places the whole
    // VRCFury folder first. Re-register those Basis-visible runtime entries through
    // Unity's editor menu API at the normal component priority.
    [InitializeOnLoad]
    internal static class BasisVrcfuryComponentMenuOrdering {
        private const int ComponentMenuPriority = 500;
        private static readonly MethodInfo AddMenuItem = FindMenuMethod("AddMenuItem", 6);
        private static readonly MethodInfo RemoveMenuItem = FindMenuMethod("RemoveMenuItem", 1);
        private static bool reordering;

        static BasisVrcfuryComponentMenuOrdering() {
            EditorApplication.delayCall += ReorderRuntimeMenus;
        }

        private static MethodInfo FindMenuMethod(string name, int parameterCount) {
            return typeof(UnityEditor.Menu)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method => method.Name == name && method.GetParameters().Length == parameterCount);
        }

        private static void ReorderRuntimeMenus() {
            if (reordering || AddMenuItem == null || RemoveMenuItem == null) return;
            reordering = true;
            try {
                RegisterComponent<VF.Component.VRCFuryGlobalCollider>("Component/VRCFury/Global Collider (VRCFury)");
                RegisterComponent<VF.Component.VRCFuryHapticPlug>("Component/VRCFury/SPS Plug (VRCFury)");
                RegisterComponent<VF.Component.VRCFuryHapticSocket>("Component/VRCFury/SPS Socket (VRCFury)");
                RegisterComponent<VF.Component.VRCFuryHapticTouchReceiver>("Component/VRCFury/SPS Touch Zone (VRCFury)");
                RegisterComponent<VF.Component.VRCFuryHapticTouchSender>("Component/VRCFury/Touch Sender (VRCFury)");
            } catch (Exception error) {
                Debug.LogWarning($"VRCFury Basis component menu ordering could not be applied: {error.Message}");
            } finally {
                reordering = false;
            }
        }

        private static void RegisterComponent<T>(string path) where T : UnityEngine.Component {
            RemoveMenuItem.Invoke(null, new object[] { path });
            AddMenuItem.Invoke(null, new object[] {
                path,
                "",
                false,
                ComponentMenuPriority,
                AddComponent<T>(),
                new Func<bool>(HasSelection)
            });
        }

        private static System.Action AddComponent<T>() where T : UnityEngine.Component {
            return () => {
                foreach (var selected in Selection.gameObjects) {
                    if (selected != null) Undo.AddComponent<T>(selected);
                }
            };
        }

        private static bool HasSelection() => Selection.gameObjects.Any(obj => obj != null);

    }

    [CustomEditor(typeof(VRCFury), true)]
    internal sealed class BasisVrcfuryAuthoringEditor : UnityEditor.Editor {
        private bool advancedOptions;
        private bool superAdvancedOptions;
        private bool forceAdvancedLinkTargets;
        private GameObject lastPropBone;

        private void OnEnable() {
            if (target is VRCFury fury && fury.content is ArmatureLink model) lastPropBone = model.propBone;
        }

        public override VisualElement CreateInspectorGUI() {
            var root = new VisualElement();
            root.Add(BasisVrcfuryHeader.Create(GetFeatureTitle()));
            root.Add(new IMGUIContainer(DrawInspector));
            return root;
        }

        private string GetFeatureTitle() {
            if (!(target is VRCFury fury) || fury.content == null) return "VRCFury";
            if (fury.content is ArmatureLink) return "Armature Link";
            if (fury.content is BlendshapeOptimizer) return "Blendshape Optimizer";
            if (fury.content is MmdCompatibility) return "MMD Compatibility";
            if (fury.content is ApplyDuringUpload) return "Apply During Upload";
            return ObjectNames.NicifyVariableName(fury.content.GetType().Name);
        }

        private void DrawInspector() {
            serializedObject.Update();
            var fury = (VRCFury)target;
            var content = serializedObject.FindProperty("content");

            if (fury.content == null) {
                EditorGUILayout.HelpBox(
                    "This VRCFury component has no feature configured. VRCFury normally creates feature components from Add Component > VRCFury.",
                    MessageType.Error
                );
                if (GUILayout.Button("Armature Link")) SetFeature(new ArmatureLink { propBone = fury.gameObject });
                if (GUILayout.Button("Blendshape Optimizer")) SetFeature(new BlendshapeOptimizer());
                if (GUILayout.Button("MMD Compatibility")) SetFeature(new MmdCompatibility());
                if (GUILayout.Button("Apply During Upload")) SetFeature(new ApplyDuringUpload { action = new State() });
                serializedObject.ApplyModifiedProperties();
                return;
            }

            if (fury.content is ArmatureLink armatureLink) {
                DrawArmatureLink(content, armatureLink);
            } else if (fury.content is BlendshapeOptimizer) {
                DrawBlendshapeOptimizer();
            } else if (fury.content is MmdCompatibility) {
                DrawMmdCompatibility();
            } else if (fury.content is ApplyDuringUpload) {
                DrawApplyDuringUpload(content);
            } else {
                EditorGUILayout.HelpBox(
                    "This VRCFury feature is preserved for source-avatar compatibility, but the Basis compatibility layer does not currently provide its original custom editor/build implementation.",
                    MessageType.Warning
                );
                EditorGUILayout.PropertyField(content, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void SetFeature(FeatureModel feature) {
            Undo.RecordObject(target, "Set VRCFury Feature");
            var property = serializedObject.FindProperty("content");
            property.managedReferenceValue = feature;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            VRCFury.MarkDirty((VRCFury)target);
        }

        private void DrawArmatureLink(SerializedProperty prop, ArmatureLink model) {
            EditorGUILayout.HelpBox(
                "This feature will attach a prop (with or without an armature) to the avatar. If 'Link From' is an armature matching the avatar's, the armatures will be merged and the extra bones will not count toward performance rank.",
                MessageType.Info
            );

            var propBoneProp = prop.FindPropertyRelative("propBone");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                propBoneProp,
                new GUIContent(
                    "Link From (Prop / Clothing)",
                    "For clothing, this should be the Hips bone in the clothing's Armature (or the main bone if it doesn't have Hips). For non-clothing objects, this should be the object you want moved."
                )
            );
            if (EditorGUI.EndChangeCheck()) {
                serializedObject.ApplyModifiedProperties();
                var newValue = propBoneProp.objectReferenceValue as GameObject;
                if (lastPropBone != newValue) {
                    BasisVrcfuryAuthoringMenus.UpdateOnLinkFromChange(model, lastPropBone, newValue);
                    EditorUtility.SetDirty(target);
                    serializedObject.Update();
                    lastPropBone = newValue;
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Link To (Avatar):", EditorStyles.boldLabel);
            var linkTo = prop.FindPropertyRelative("linkTo");
            var simpleLinkToMode = IsSimpleLinkTo(linkTo) && !forceAdvancedLinkTargets;
            if (simpleLinkToMode) {
                EditorGUILayout.PropertyField(linkTo.GetArrayElementAtIndex(0).FindPropertyRelative("bone"), GUIContent.none);
            } else {
                DrawAdvancedLinkTargets(linkTo);
            }

            EditorGUILayout.Space(2);
            advancedOptions = EditorGUILayout.Foldout(advancedOptions, "Advanced Options", true);
            if (advancedOptions) {
                EditorGUI.indentLevel++;
            DrawSectionHeader("Search / Matching");
            if (IsSimpleLinkTo(linkTo) && !forceAdvancedLinkTargets) {
                if (GUILayout.Button("Enable Advanced Link Target Mode")) forceAdvancedLinkTargets = true;
            }

            EditorGUILayout.PropertyField(
                prop.FindPropertyRelative("recursive"),
                new GUIContent("Recursive", "If enabled, child objects with matching object names on the avatar will also be linked")
            );
            EditorGUILayout.PropertyField(
                prop.FindPropertyRelative("removeBoneSuffix"),
                new GUIContent(
                    "Ignore name suffix/prefix",
                    "If set, this substring will be ignored when matching object names against the avatar. If empty, the suffix is predicted from the difference between the root bone names."
                )
            );

            DrawSectionHeader("Transform Alignment", "Snap merged objects to the existing transform on the avatar");
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("alignPosition"), new GUIContent("Align Position"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("alignRotation"), new GUIContent("Align Rotation"));
            var alignScale = prop.FindPropertyRelative("alignScale");
            EditorGUILayout.PropertyField(alignScale, new GUIContent("Align Scale"));

            if (alignScale.boolValue) {
                EditorGUI.indentLevel++;
                var recursive = prop.FindPropertyRelative("recursive").boolValue;
                var autoScale = prop.FindPropertyRelative("autoScaleFactor");
                if (recursive) {
                    EditorGUILayout.PropertyField(
                        autoScale,
                        new GUIContent(
                            "Automatic Scale Multiplier",
                            "Uses the Link From object's world scale divided by the Link To object's world scale."
                        )
                    );
                    if (autoScale.boolValue) {
                        EditorGUILayout.PropertyField(
                            prop.FindPropertyRelative("scalingFactorPowersOf10Only"),
                            new GUIContent("Restrict multiplier to powers of 10")
                        );
                    } else {
                        EditorGUILayout.PropertyField(prop.FindPropertyRelative("skinRewriteScalingFactor"), new GUIContent("Multiplier"));
                    }
                } else {
                    EditorGUILayout.PropertyField(prop.FindPropertyRelative("skinRewriteScalingFactor"), new GUIContent("Multiplier"));
                }
                EditorGUI.indentLevel--;
            }

            superAdvancedOptions = EditorGUILayout.Foldout(superAdvancedOptions, "Super Advanced Options", true);
            if (superAdvancedOptions) {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Danger, changing these options may break things.", MessageType.Warning);
                EditorGUILayout.PropertyField(
                    prop.FindPropertyRelative("removeParentConstraints"),
                    new GUIContent("Remove parent constraints from merged objects")
                );
                EditorGUILayout.PropertyField(
                    prop.FindPropertyRelative("forceMergedName"),
                    new GUIContent(
                        "Force Merged Name",
                        "Force the name of the object at the merged target location. Offset animations and toggles for the merged object may not work when this is used."
                    )
                );
                EditorGUILayout.PropertyField(
                    prop.FindPropertyRelative("forceOneWorldScale"),
                    new GUIContent("Force world scale to 1,1,1")
                );
                EditorGUI.indentLevel--;
            }
                EditorGUI.indentLevel--;
            }

            DrawArmatureWarnings(model);
        }

        private static bool IsSimpleLinkTo(SerializedProperty linkTo) {
            if (linkTo == null || !linkTo.isArray || linkTo.arraySize != 1) return false;
            var entry = linkTo.GetArrayElementAtIndex(0);
            return entry.FindPropertyRelative("useBone").boolValue
                   && !entry.FindPropertyRelative("useObj").boolValue
                   && string.IsNullOrWhiteSpace(entry.FindPropertyRelative("offset").stringValue);
        }

        private static void DrawAdvancedLinkTargets(SerializedProperty linkTo) {
            EditorGUILayout.HelpBox("If multiple targets are provided, the first valid target found on the avatar will be used.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField("Target Object", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("Offset Path", EditorStyles.miniBoldLabel);
                GUILayout.Space(22);
            }

            for (var i = 0; i < linkTo.arraySize; i++) {
                var entry = linkTo.GetArrayElementAtIndex(i);
                var useBone = entry.FindPropertyRelative("useBone");
                var useObj = entry.FindPropertyRelative("useObj");
                var bone = entry.FindPropertyRelative("bone");
                var obj = entry.FindPropertyRelative("obj");
                var offset = entry.FindPropertyRelative("offset");

                using (new EditorGUILayout.HorizontalScope()) {
                    if (useObj.boolValue) {
                        EditorGUILayout.PropertyField(obj, GUIContent.none);
                    } else if (useBone.boolValue) {
                        EditorGUILayout.PropertyField(bone, GUIContent.none);
                    } else {
                        EditorGUILayout.LabelField("Avatar Root");
                    }
                    EditorGUILayout.PropertyField(offset, GUIContent.none);
                    if (GUILayout.Button("−", GUILayout.Width(22))) {
                        linkTo.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            if (GUILayout.Button("+", GUILayout.Width(28))) {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Bone"), false, () => AddLinkTarget(linkTo, true, false));
                menu.AddItem(new GUIContent("GameObject"), false, () => AddLinkTarget(linkTo, false, true));
                menu.AddItem(new GUIContent("Avatar Root"), false, () => AddLinkTarget(linkTo, false, false));
                menu.ShowAsContext();
            }
        }

        private static void AddLinkTarget(SerializedProperty linkTo, bool useBone, bool useObj) {
            linkTo.serializedObject.Update();
            var index = linkTo.arraySize;
            linkTo.InsertArrayElementAtIndex(index);
            var entry = linkTo.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("useBone").boolValue = useBone;
            entry.FindPropertyRelative("bone").enumValueIndex = (int)HumanBodyBones.Hips;
            entry.FindPropertyRelative("useObj").boolValue = useObj;
            entry.FindPropertyRelative("obj").objectReferenceValue = null;
            entry.FindPropertyRelative("offset").stringValue = string.Empty;
            linkTo.serializedObject.ApplyModifiedProperties();
        }

        private void DrawArmatureWarnings(ArmatureLink model) {
            if (model.propBone == null) return;
            var guess = BasisVrcfuryAuthoringMenus.GuessLinkFrom(model.propBone);
            if (guess != null && guess != model.propBone) {
                EditorGUILayout.HelpBox(
                    "It appears this object contains clothing with an Armature and Hips bone. If you are linking clothing, Link From should usually be that Hips object rather than the main clothing object.",
                    MessageType.Warning
                );
            }
        }

        private static void DrawBlendshapeOptimizer() {
            EditorGUILayout.HelpBox(
                "This feature will automatically bake all non-animated blendshapes into the mesh, saving VRAM for free!",
                MessageType.Info
            );
        }

        private static void DrawMmdCompatibility() {
            EditorGUILayout.HelpBox(
                "This component improves MMD compatibility by preserving VRCFury's known MMD blendshapes when Blendshape Optimizer runs.",
                MessageType.Info
            );
            EditorGUILayout.HelpBox(
                "VRCFury's advanced MMD layer-detection settings control VRChat FX animator layers. BasisVR does not use those VRChat layers, so those settings are preserved in the component data but are not applied by the Basis backend.",
                MessageType.None
            );
        }

        private void DrawApplyDuringUpload(SerializedProperty prop) {
            EditorGUILayout.LabelField(
                "The following actions will be applied and baked into the avatar during the upload process. This is useful if you want to enforce a specific upload state of your prop, even if the user has messed with it in the editor.",
                EditorStyles.wordWrappedLabel
            );
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                "Note: 'Turn On' toggles automatically turn on their objects, and thus do not need to be included here.",
                EditorStyles.wordWrappedLabel
            );

            var state = prop.FindPropertyRelative("action");
            var actions = state?.FindPropertyRelative("actions");
            if (actions == null) {
                EditorGUILayout.HelpBox("Apply During Upload state data is missing.", MessageType.Error);
                return;
            }

            var avatarRoot = GetAvatarRoot();
            for (var i = 0; i < actions.arraySize; i++) {
                var action = actions.GetArrayElementAtIndex(i);
                var value = action.managedReferenceValue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.LabelField("= " + GetActionTitle(value), EditorStyles.boldLabel);
                        if (GUILayout.Button("Remove", GUILayout.Width(64))) {
                            actions.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }
                    DrawAction(action, value, avatarRoot);
                }
            }

            if (GUILayout.Button("Add Action +")) {
                var menu = new GenericMenu();
                AddActionMenuItem<ObjectToggleAction>(menu, actions, "Object Toggle");
                AddActionMenuItem<BlendShapeAction>(menu, actions, "BlendShape");
                AddActionMenuItem<ScaleAction>(menu, actions, "Scale");
                AddActionMenuItem<MaterialAction>(menu, actions, "Material Swap");
                AddActionMenuItem<MaterialPropertyAction>(menu, actions, "Material Property");
                AddActionMenuItem<AnimationClipAction>(menu, actions, "Animation Clip");
                AddActionMenuItem<FlipbookAction>(menu, actions, "Poiyomi Flipbook Frame");
                AddActionMenuItem<PoiyomiUVTileAction>(menu, actions, "Poiyomi UV Tile");
                AddActionMenuItem<ShaderInventoryAction>(menu, actions, "SCSS Shader Inventory");
                menu.ShowAsContext();
            }
        }

        private GameObject GetAvatarRoot() {
            var fury = target as VRCFury;
            if (fury == null) return null;
            var avatar = fury.GetComponentInParent<BasisAvatar>();
            return avatar != null ? avatar.gameObject : fury.transform.root.gameObject;
        }

        private static string GetActionTitle(object value) {
            if (value == null) return "Missing Action";
            var name = value.GetType().Name;
            if (name.EndsWith("Action", StringComparison.Ordinal)) {
                name = name.Substring(0, name.Length - "Action".Length);
            }
            return ObjectNames.NicifyVariableName(name);
        }

        private static void DrawAction(SerializedProperty prop, object value, GameObject avatarRoot) {
            switch (value) {
                case ObjectToggleAction:
                    DrawObjectToggleAction(prop);
                    break;
                case BlendShapeAction:
                    DrawBlendShapeAction(prop);
                    break;
                case ScaleAction:
                    DrawScaleAction(prop);
                    break;
                case MaterialAction:
                    DrawMaterialAction(prop);
                    break;
                case MaterialPropertyAction:
                    DrawMaterialPropertyAction(prop, avatarRoot);
                    break;
                case AnimationClipAction:
                    DrawAnimationClipAction(prop);
                    break;
                case FlipbookAction:
                    DrawFlipbookAction(prop);
                    break;
                case PoiyomiUVTileAction:
                    DrawPoiyomiUVTileAction(prop);
                    break;
                case ShaderInventoryAction:
                    DrawShaderInventoryAction(prop);
                    break;
                default:
                    EditorGUILayout.HelpBox(
                        "This action is preserved for source-avatar compatibility, but Basis does not apply it during upload.",
                        MessageType.Warning
                    );
                    EditorGUILayout.PropertyField(prop, GUIContent.none, true);
                    break;
            }
        }

        private static void DrawObjectToggleAction(SerializedProperty prop) {
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("obj"), new GUIContent("Object"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("mode"), new GUIContent("Mode"));
        }

        private static void DrawBlendShapeAction(SerializedProperty prop) {
            var allRenderers = prop.FindPropertyRelative("allRenderers");
            EditorGUILayout.PropertyField(allRenderers, new GUIContent("Apply to all renderers"));
            if (!allRenderers.boolValue) {
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("renderer"), new GUIContent("Renderer"));
            }
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("blendShape"), new GUIContent("Blendshape"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("blendShapeValue"), new GUIContent("Value"));
        }

        private static void DrawScaleAction(SerializedProperty prop) {
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("obj"), new GUIContent("Object"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("scale"), new GUIContent("Scale"));
        }

        private static void DrawMaterialAction(SerializedProperty prop) {
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("renderer"), new GUIContent("Renderer"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("materialIndex"), new GUIContent("Material Slot"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("mat"), new GUIContent("Material"), true);
        }

        private static void DrawAnimationClipAction(SerializedProperty prop) {
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("clip"), new GUIContent("Animation Clip"), true);
        }

        private static void DrawFlipbookAction(SerializedProperty prop) {
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("renderer"), new GUIContent("Renderer"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("frame"), new GUIContent("Frame"));
        }

        private static void DrawPoiyomiUVTileAction(SerializedProperty prop) {
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("renderer"), new GUIContent("Renderer"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("row"), new GUIContent("Row"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("column"), new GUIContent("Column"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("dissolve"), new GUIContent("Dissolve"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("renamedMaterial"), new GUIContent("Renamed Material"));
        }

        private static void DrawShaderInventoryAction(SerializedProperty prop) {
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("renderer"), new GUIContent("Renderer"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("slot"), new GUIContent("Slot"));
        }

        private static void DrawMaterialPropertyAction(SerializedProperty prop, GameObject avatarRoot) {
            var allRenderersProp = prop.FindPropertyRelative("affectAllMeshes");
            var rendererProp = prop.FindPropertyRelative("renderer2");
            EditorGUILayout.PropertyField(allRenderersProp, new GUIContent("Apply to all renderers"));

            if (allRenderersProp.boolValue) {
                if (rendererProp.objectReferenceValue != null) rendererProp.objectReferenceValue = null;
            } else {
                DrawRendererGameObjectField(rendererProp, "Renderer");
            }

            var propertyNameProp = prop.FindPropertyRelative("propertyName");
            var propertyTypeProp = prop.FindPropertyRelative("propertyType");
            var selectedRenderers = FindMaterialPropertyRenderers(
                avatarRoot,
                allRenderersProp.boolValue,
                rendererProp.objectReferenceValue as GameObject
            );

            var propertyChanged = false;
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUI.BeginChangeCheck();
                var propertyName = EditorGUILayout.TextField(new GUIContent("Property"), propertyNameProp.stringValue);
                propertyChanged = EditorGUI.EndChangeCheck();
                if (propertyChanged) propertyNameProp.stringValue = propertyName;

                if (GUILayout.Button("Search", GUILayout.Width(58))) {
                    ShowMaterialPropertyMenu(
                        propertyNameProp,
                        propertyTypeProp,
                        avatarRoot,
                        allRenderersProp.boolValue,
                        rendererProp.objectReferenceValue as GameObject
                    );
                }
            }

            var configuredType = (MaterialPropertyAction.Type)propertyTypeProp.enumValueIndex;
            if (propertyChanged || configuredType == MaterialPropertyAction.Type.LegacyAuto) {
                var detected = DetectMaterialPropertyType(selectedRenderers, propertyNameProp.stringValue);
                if (detected.HasValue) {
                    propertyTypeProp.enumValueIndex = (int)detected.Value;
                    configuredType = detected.Value;
                }
            }
            if (configuredType == MaterialPropertyAction.Type.LegacyAuto) {
                configuredType = MaterialPropertyAction.Type.Float;
            }

            switch (configuredType) {
                case MaterialPropertyAction.Type.Color:
                    EditorGUILayout.PropertyField(prop.FindPropertyRelative("valueColor"), new GUIContent("Value"));
                    break;
                case MaterialPropertyAction.Type.Vector:
                    EditorGUILayout.PropertyField(prop.FindPropertyRelative("valueVector"), new GUIContent("Value"));
                    break;
                case MaterialPropertyAction.Type.St:
                    var vector = prop.FindPropertyRelative("valueVector");
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.PrefixLabel("Scale");
                        var scaleX = vector.FindPropertyRelative("x");
                        var scaleY = vector.FindPropertyRelative("y");
                        scaleX.floatValue = EditorGUILayout.FloatField("X", scaleX.floatValue);
                        scaleY.floatValue = EditorGUILayout.FloatField("Y", scaleY.floatValue);
                    }
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.PrefixLabel("Offset");
                        var offsetX = vector.FindPropertyRelative("z");
                        var offsetY = vector.FindPropertyRelative("w");
                        offsetX.floatValue = EditorGUILayout.FloatField("X", offsetX.floatValue);
                        offsetY.floatValue = EditorGUILayout.FloatField("Y", offsetY.floatValue);
                    }
                    break;
                default:
                    EditorGUILayout.PropertyField(prop.FindPropertyRelative("value"), new GUIContent("Value"));
                    break;
            }
        }

        private static void DrawRendererGameObjectField(SerializedProperty prop, string label) {
            var currentObject = prop.objectReferenceValue as GameObject;
            var currentRenderer = currentObject != null ? currentObject.GetComponent<Renderer>() : null;
            var nextRenderer = EditorGUILayout.ObjectField(new GUIContent(label), currentRenderer, typeof(Renderer), true) as Renderer;
            if (nextRenderer != currentRenderer) prop.objectReferenceValue = nextRenderer != null ? nextRenderer.gameObject : null;
        }

        private static List<Renderer> FindMaterialPropertyRenderers(GameObject avatarRoot, bool allRenderers, GameObject rendererObject) {
            var result = new List<Renderer>();
            if (allRenderers) {
                if (avatarRoot != null) result.AddRange(avatarRoot.GetComponentsInChildren<Renderer>(true));
            } else {
                var renderer = rendererObject != null ? rendererObject.GetComponent<Renderer>() : null;
                if (renderer != null) result.Add(renderer);
            }
            return result;
        }

        private static MaterialPropertyAction.Type? DetectMaterialPropertyType(IList<Renderer> renderers, string propertyName) {
            if (string.IsNullOrWhiteSpace(propertyName)) return null;
            foreach (var renderer in renderers) {
                if (renderer == null) continue;
                foreach (var material in renderer.sharedMaterials) {
                    if (material == null || material.shader == null) continue;
                    var shader = material.shader;
                    for (var i = 0; i < shader.GetPropertyCount(); i++) {
                        var shaderName = shader.GetPropertyName(i);
                        var shaderType = shader.GetPropertyType(i);
                        if (shaderName == propertyName) return ToMaterialPropertyType(shaderType);
                        if (shaderType == ShaderPropertyType.Texture && propertyName == shaderName + "_ST") {
                            return MaterialPropertyAction.Type.St;
                        }
                    }
                }
            }
            return null;
        }

        private static MaterialPropertyAction.Type ToMaterialPropertyType(ShaderPropertyType type) {
            switch (type) {
                case ShaderPropertyType.Color:
                    return MaterialPropertyAction.Type.Color;
                case ShaderPropertyType.Vector:
                    return MaterialPropertyAction.Type.Vector;
                default:
                    return MaterialPropertyAction.Type.Float;
            }
        }

        private static void ShowMaterialPropertyMenu(
            SerializedProperty propertyNameProp,
            SerializedProperty propertyTypeProp,
            GameObject avatarRoot,
            bool allRenderers,
            GameObject rendererObject
        ) {
            var renderers = FindMaterialPropertyRenderers(avatarRoot, allRenderers, rendererObject);
            var provider = ScriptableObject.CreateInstance<BasisMaterialPropertySearchProvider>();
            provider.Initialize(
                BuildMaterialPropertySearchTree(avatarRoot, renderers),
                selectedName => {
                    propertyNameProp.stringValue = selectedName;
                    var detected = DetectMaterialPropertyType(renderers, selectedName);
                    if (detected.HasValue) propertyTypeProp.enumValueIndex = (int)detected.Value;
                    propertyNameProp.serializedObject.ApplyModifiedProperties();
                    GUI.changed = true;
                }
            );

            var position = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            SearchWindow.Open(
                new SearchWindowContext(GUIUtility.GUIToScreenPoint(position), 500, 300),
                provider
            );
        }

        private static List<SearchTreeEntry> BuildMaterialPropertySearchTree(GameObject avatarRoot, IList<Renderer> renderers) {
            var entries = new List<SearchTreeEntry> {
                new SearchTreeGroupEntry(new GUIContent("Material Properties"), 0)
            };
            if (renderers == null || renderers.Count == 0) return entries;

            foreach (var renderer in renderers) {
                if (renderer == null) continue;
                var sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials == null || sharedMaterials.Length == 0) continue;

                var rendererLevel = 1;
                if (renderers.Count > 1) {
                    entries.Add(new SearchTreeGroupEntry(
                        new GUIContent("Mesh: " + BasisVrcfuryAuthoringMenus.GetPath(renderer.transform, avatarRoot != null ? avatarRoot.transform : null)),
                        rendererLevel
                    ));
                    rendererLevel++;
                }

                foreach (var material in sharedMaterials) {
                    if (material == null || material.shader == null) continue;
                    var materialLevel = rendererLevel;
                    if (sharedMaterials.Length > 1) {
                        entries.Add(new SearchTreeGroupEntry(new GUIContent("Material: " + material.name), materialLevel));
                        materialLevel++;
                    }

                    var shader = material.shader;
                    var sectionStack = new Stack<string>();
                    for (var i = 0; i < shader.GetPropertyCount(); i++) {
                        var propertyName = shader.GetPropertyName(i);
                        if (propertyName == "_DummyProperty") continue;

                        var description = shader.GetPropertyDescription(i) ?? string.Empty;
                        if (description.Contains("{condition_showS:(0==1)}")) continue;

                        var readableName = description;
                        var separator = readableName.IndexOf("--", StringComparison.Ordinal);
                        if (separator >= 0) readableName = readableName.Substring(0, separator);
                        readableName = NormalizeSearchLabel(readableName);

                        if (propertyName.StartsWith("m_start", StringComparison.Ordinal)) {
                            sectionStack.Push(readableName);
                        } else if (propertyName.StartsWith("m_end", StringComparison.Ordinal)) {
                            if (sectionStack.Count > 0) sectionStack.Pop();
                        } else if (propertyName.StartsWith("m_", StringComparison.Ordinal)) {
                            sectionStack.Clear();
                            if (!string.IsNullOrEmpty(readableName)) sectionStack.Push(readableName);
                        }

                        var type = shader.GetPropertyType(i);
                        if (type != ShaderPropertyType.Float && type != ShaderPropertyType.Range &&
                            type != ShaderPropertyType.Color && type != ShaderPropertyType.Vector &&
                            type != ShaderPropertyType.Texture) continue;
                        if ((shader.GetPropertyFlags(i) & ShaderPropertyFlags.HideInInspector) != 0) continue;

                        var attributes = shader.GetPropertyAttributes(i);
                        if (attributes != null && attributes.Any(attribute =>
                                attribute.StartsWith("ThryToggle", StringComparison.Ordinal) ||
                                attribute.StartsWith("DoNotAnimate", StringComparison.Ordinal) ||
                                attribute.StartsWith("NoAnimate", StringComparison.Ordinal) ||
                                attribute.StartsWith("Helpbox", StringComparison.Ordinal) ||
                                attribute.StartsWith("ThryShaderOptimizerLockButton", StringComparison.Ordinal) ||
                                attribute.StartsWith("ThryWideEnum", StringComparison.Ordinal))) continue;

                        if (string.IsNullOrWhiteSpace(readableName)) readableName = propertyName;
                        if (sectionStack.Count > 0) readableName = string.Join(" > ", sectionStack.Reverse()) + " > " + readableName;

                        var selectionName = propertyName;
                        var displayName = readableName;
                        if (type == ShaderPropertyType.Texture) {
                            selectionName += "_ST";
                            displayName += " (Scale+Offset)";
                        }
                        if (displayName != selectionName) displayName += " (" + selectionName + ")";
                        if (renderers.Count > 1) {
                            displayName += " (Mesh: " + BasisVrcfuryAuthoringMenus.GetPath(renderer.transform, avatarRoot != null ? avatarRoot.transform : null) + ")";
                        }
                        if (sharedMaterials.Length > 1) displayName += " (Mat: " + material.name + ")";

                        entries.Add(new SearchTreeEntry(new GUIContent(displayName)) {
                            level = materialLevel,
                            userData = selectionName
                        });
                    }
                }
            }
            return entries;
        }

        private static string NormalizeSearchLabel(string value) {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var chars = value.Where(character => character != '<' && character != '>').ToArray();
            return string.Join(" ", new string(chars).Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries
            ));
        }

        private sealed class BasisMaterialPropertySearchProvider : ScriptableObject, ISearchWindowProvider {
            private List<SearchTreeEntry> entries;
            private Action<string> onSelect;

            internal void Initialize(List<SearchTreeEntry> searchEntries, Action<string> select) {
                entries = searchEntries;
                onSelect = select;
            }

            public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context) => entries ?? new List<SearchTreeEntry> {
                new SearchTreeGroupEntry(new GUIContent("Material Properties"), 0)
            };

            public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context) {
                if (entry == null || !(entry.userData is string selectedName)) return false;
                onSelect?.Invoke(selectedName);
                return true;
            }
        }

        private static void AddActionMenuItem<T>(GenericMenu menu, SerializedProperty actions, string label)
            where T : VF.Model.StateAction.Action, new() {
            menu.AddItem(new GUIContent(label), false, () => {
                actions.serializedObject.Update();
                var index = actions.arraySize;
                actions.InsertArrayElementAtIndex(index);
                actions.GetArrayElementAtIndex(index).managedReferenceValue = new T();
                actions.serializedObject.ApplyModifiedProperties();
            });
        }

        private static void DrawSectionHeader(string title, string subtitle = null) {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrWhiteSpace(subtitle)) EditorGUILayout.LabelField(subtitle, EditorStyles.wordWrappedMiniLabel);
        }
    }

    internal static class BasisVrcfuryHeader {
        internal static VisualElement Create(string title) => CreateHeaderOverlay(title);

        private static VisualElement FindEditor(VisualElement element) {
            if (element == null) return null;
            if (element is InspectorElement) return element.parent;
            return FindEditor(element.parent);
        }

        private static bool HasMultipleHeaders(VisualElement root) {
            if (root == null) return false;
            if (root.ClassListContains("vrcfMultipleHeaders")) return true;
            return HasMultipleHeaders(root.parent);
        }

        private static void AttachHeaderOverlay(VisualElement body, string title) {
            var inspectorRoot = FindEditor(body);
            if (HasMultipleHeaders(body) || inspectorRoot == null) {
                body.Add(CreateInlineHeader(title));
                return;
            }

            var headerIndex = inspectorRoot.Children()
                .Select((element, index) => new { element, index })
                .Where(item => !string.IsNullOrEmpty(item.element.name) && item.element.name.EndsWith("Header", StringComparison.Ordinal))
                .Select(item => item.index)
                .DefaultIfEmpty(-1)
                .First();
            if (headerIndex < 0) {
                body.Add(CreateInlineHeader(title));
                return;
            }

            var headerArea = CreateOverlayHeader(title);
            headerArea.AddToClassList("vrcfHeaderOverlay");
            inspectorRoot.Insert(headerIndex + 1, headerArea);
            body.RegisterCallback<DetachFromPanelEvent>(_ => headerArea.parent?.Remove(headerArea));
        }

        internal static VisualElement CreateHeaderOverlay(string title) {
            var element = new VisualElement();
            element.AddToClassList("vrcfHeader");
            element.RegisterCallback<AttachToPanelEvent>(_ => AttachHeaderOverlay(element, title));
            return element;
        }

        private static VisualElement CreateInlineHeader(string title) => CreateHeaderRow(title);

        internal static VisualElement CreateOverlayHeader(string title) {
            var headerArea = new VisualElement {
                style = {
                    height = 20,
                    width = Length.Percent(100),
                    top = -21,
                    position = Position.Absolute
                },
                pickingMode = PickingMode.Ignore
            };
            var row = CreateHeaderRow(title);
            row.style.marginLeft = 18;
            row.style.marginRight = 60;
            headerArea.Add(row);
            var wrapper = new VisualElement();
            wrapper.Add(headerArea);
            return wrapper;
        }

        private static VisualElement CreateHeaderRow(string title) {
            Color background = EditorGUIUtility.isProSkin
                ? new Color32(61, 61, 61, 255)
                : new Color32(194, 194, 194, 255);
            var row = new VisualElement {
                pickingMode = PickingMode.Ignore,
                style = {
                    flexDirection = FlexDirection.Row,
                    height = 20,
                    backgroundColor = background
                }
            };
            var normalLabelColor = EditorGUIUtility.isProSkin
                ? new Color(0.05f, 0.05f, 0.05f)
                : new Color(0.05f, 0.05f, 0.05f);
            row.Add(new VisualElement {
                style = {
                    borderRightColor = normalLabelColor,
                    borderBottomColor = normalLabelColor,
                    borderLeftWidth = 5,
                    borderTopWidth = 10,
                    borderRightWidth = 5,
                    borderBottomWidth = 10
                },
                pickingMode = PickingMode.Ignore
            });
            var badge = new Label("VRCFury") {
                pickingMode = PickingMode.Ignore,
                style = {
                    color = new Color(0.8f, 0.4f, 0f),
                    backgroundColor = new Color(0.05f, 0.05f, 0.05f),
                    paddingLeft = 3,
                    paddingRight = 3,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexShrink = 1
                }
            };
            row.Add(badge);
            row.Add(new VisualElement {
                style = {
                    borderLeftColor = normalLabelColor,
                    borderTopColor = normalLabelColor,
                    borderLeftWidth = 5,
                    borderTopWidth = 10,
                    borderRightWidth = 5,
                    borderBottomWidth = 10
                },
                pickingMode = PickingMode.Ignore
            });
            var name = new Label(title) {
                pickingMode = PickingMode.Ignore,
                style = {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 3,
                    flexGrow = 1
                }
            };
            row.Add(name);
            return row;
        }
    }
}
