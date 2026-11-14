using System;
using UnityEngine;
using VF.Upgradeable;
using VF.VrcfEditorOnly;

namespace VF.Component {
#if UNITY_6000_7_OR_NEWER
    // Editor callbacks are registered on script load and must survive entering play mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal abstract class VRCFuryComponent : VrcfUpgradeableMonoBehaviour, IVrcfEditorOnly {
        [NonSerialized] public GameObject gameObjectOverride;
        public new GameObject gameObject {
            get {
                if (gameObjectOverride != null) return gameObjectOverride;
                return base.gameObject;
            }
        }
        public new Transform transform {
            get {
                if (gameObjectOverride != null) return gameObjectOverride.transform;
                return base.transform;
            }
        }

        public static Action _OnValidate;

        private void OnValidate() {
            _OnValidate?.Invoke();
        }

        public override int GetLatestVersion() {
            return 1;
        }
    }
}
