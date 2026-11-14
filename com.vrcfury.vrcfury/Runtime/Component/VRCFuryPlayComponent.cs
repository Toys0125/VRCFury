using System;
using UnityEngine;
using VF.VrcfEditorOnly;

namespace VF.Component {
#if UNITY_6000_7_OR_NEWER
    // Editor callbacks are registered on script load and must survive entering play mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal abstract class VRCFuryPlayComponent : MonoBehaviour, IVrcfEditorOnly {
        public static Action<VRCFuryPlayComponent> onValidate;

        private void OnValidate() {
            onValidate?.Invoke(this);
        }
    }
}
