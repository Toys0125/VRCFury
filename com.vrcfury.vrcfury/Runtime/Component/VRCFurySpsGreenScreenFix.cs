using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.VrcfEditorOnly;

namespace VF.Component {
#if UNITY_6000_7_OR_NEWER
    // The editor callback must still be available when Start runs in play mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    [AddComponentMenu("")]
    internal class VRCFurySpsGreenScreenFix : VRCFuryPlayComponent, IVrcfEditorOnly {
        public static Action onCreated;

        private void Start() {
            if (!Application.isPlaying) return;

            try {
                EnsureCameraExists();
                onCreated?.Invoke();
            } finally {
                DestroyImmediate(this);
            }
        }

        private static void EnsureCameraExists() {
            var hasCamera = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.isLoaded)
                .SelectMany(scene => scene.GetRootGameObjects())
                .SelectMany(root => root.GetComponentsInChildren<Camera>())
                .Any(camera => camera.isActiveAndEnabled);
            if (hasCamera) return;

            var cameraObj = new GameObject("Scene Camera");
            SceneManager.MoveGameObjectToScene(cameraObj, SceneManager.GetActiveScene());
            cameraObj.AddComponent<Camera>();
        }

    }
}
