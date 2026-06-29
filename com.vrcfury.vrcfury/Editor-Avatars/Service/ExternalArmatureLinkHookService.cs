using System;
using System.Linq;
using com.vrcfury.api.Integration;
using UnityEngine;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Service {
    /// <summary>
    /// Converts external armature-link hook requests into normal VRCFury ArmatureLink
    /// features. This keeps BasisVR and future runtimes out of ArmatureLinkService's
    /// core implementation while still sharing the existing merge/rewrite behavior.
    /// </summary>
    [VFService]
    internal class ExternalArmatureLinkHookService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly VFGameObject avatarObject;

        [FeatureBuilderAction(FeatureOrder.SecurityRestricted)]
        public void CollectExternalArmatureLinks() {
            var avatarRoot = (GameObject)avatarObject;
            var requests = FuryArmatureLinkHooks.InvokeCollectors(avatarRoot);
            foreach (var request in requests) {
                AddRequest(request);
            }
        }

        private void AddRequest(FuryArmatureLinkHooks.Request request) {
            if (request.linkFrom == null) {
                Debug.LogWarning("Skipping external VRCFury armature link hook request with no Link From object from " + request.source);
                return;
            }

            var linkTo = request.linkTo == null
                ? new FuryArmatureLinkHooks.Target[0]
                : request.linkTo.Where(t => t != null).ToArray();
            if (linkTo.Length == 0) {
                Debug.LogWarning("Skipping external VRCFury armature link hook request with no Link To targets from " + request.source + " on " + request.linkFrom.name);
                return;
            }

            var model = new ArmatureLink {
                propBone = request.linkFrom,
                removeBoneSuffix = request.removeBoneSuffix,
                removeParentConstraints = request.removeParentConstraints,
                forceMergedName = request.forceMergedName,
                forceOneWorldScale = request.forceOneWorldScale,
                recursive = request.recursive,
                alignPosition = request.alignPosition,
                alignRotation = request.alignRotation,
                alignScale = request.alignScale,
                autoScaleFactor = request.autoScaleFactor,
                scalingFactorPowersOf10Only = request.scalingFactorPowersOf10Only,
                skinRewriteScalingFactor = request.skinRewriteScalingFactor
            };
            model.linkTo.Clear();
            foreach (var target in linkTo) {
                model.linkTo.Add(new ArmatureLink.LinkTo {
                    useBone = target.useBone,
                    bone = target.bone,
                    useObj = target.useObject,
                    obj = target.obj,
                    offset = target.offset ?? ""
                });
            }

            var componentRoot = request.componentRoot != null ? request.componentRoot.asVf() : request.linkFrom.asVf();
            if (globals.addOtherFeatureAt != null) {
                globals.addOtherFeatureAt(model, componentRoot);
            } else {
                globals.addOtherFeature(model);
            }

            Debug.Log("Added external VRCFury Armature Link from " + request.source + " on " + componentRoot.GetPath(avatarObject, true));
        }
    }
}
