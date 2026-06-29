# BasisURP Integration Plan

Branch: `BasisURP`
Base: upstream `VRCFury/VRCFury` `beta`

This branch is intentionally structured so the Basis/URP work can remain easy to rebase or replay on top of upstream VRCFury beta changes.

## Goals

1. Support SPSv2 in URP-based Unity 6000.5.x projects.
2. Provide a general external armature-link hook so BasisVR can request VRCFury Armature Link work without forking VRCFury internals.
3. Keep the port agent-friendly: isolate changes, add acceptance criteria, and document how to replay upstream beta.
4. Track future conversion of VRCFury toggles to Vixxy as a later task, not part of the first URP/SPS bring-up.

## Phase 0 — Branch and fork hygiene

Status: verified.

- Local branch `BasisURP` starts from `origin/beta`.
- Upstream remote remains `origin=https://github.com/VRCFury/VRCFury.git`.
- User fork remote is `toys=https://github.com/Toys0125/VRCFury.git`.
- Keep Basis-specific work in clearly named files/folders so an agent can port upstream changes with low conflict risk.

## Phase 1 — General Armature Link hook for BasisVR

Status: implemented.

Added files:

- `com.vrcfury.vrcfury/Runtime/Integration/FuryArmatureLinkHooks.cs`
- `com.vrcfury.vrcfury/Editor-Avatars/Service/ExternalArmatureLinkHookService.cs`

Design:

- External packages register `FuryArmatureLinkHooks.CollectArmatureLinks`.
- The collector receives the avatar root and returns one or more `Request` objects.
- VRCFury converts those requests into normal internal `ArmatureLink` features before `ArmatureLinkService.Apply()` runs.
- BasisVR can implement a small adapter that scans for Basis-authored link metadata, `BasisLockToBone`, or a future explicit Basis authoring component and maps it into VRCFury Armature Link requests.

Acceptance criteria:

- A Basis adapter can create an armature-link request with no VRCSDK type references.
- VRCFury still uses its existing `ArmatureLinkService` implementation for merge, animation rewrite, cleanup, and debug info.
- If Basis is not installed, VRCFury compiles and behaves as upstream.

Recommended Basis-side collector shape:

```csharp
using System.Collections.Generic;
using com.vrcfury.api.Integration;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BasisVrcfuryArmatureLinkAdapter {
    static BasisVrcfuryArmatureLinkAdapter() {
        FuryArmatureLinkHooks.CollectArmatureLinks += Collect;
    }

    private static IEnumerable<FuryArmatureLinkHooks.Request> Collect(GameObject avatarRoot) {
        // TODO: scan BasisVR-specific authoring components.
        // Return one request per prop/clothing armature to merge into avatarRoot.
        yield break;
    }
}
```

## Phase 2 — URP SPSv2 render transport

Status: implemented.

Current SPSv2 uses Built-in `GrabPass` to move marker/resolver data through framebuffer textures:

- `_VFGrid56`
- `_VFGridFinal`

URP implementation must replace these `GrabPass` stages with explicit render textures managed by a URP-compatible bridge.

Target design:

1. Allocate per-camera/per-XR-eye SPS data textures with exact integer texel reads.
2. Render socket markers into the first texture.
3. Render resolver markers after socket data is available.
4. Publish `_VFGrid56`, `_VFGrid56_TexelSize`, `_VFGridFinal`, and `_VFGridFinal_TexelSize` globally before patched SPS deform shaders run.
5. Preserve stereo behavior by supporting `Texture2DArray` for single-pass instanced/multiview where required.

Implementation notes:

- Built-in remains on the existing `GrabPass` path.
- URP uses `SPS/URP/Runtime/VrcfurySpsUrpRendererFeature.cs`, an optional `ScriptableRendererFeature` in the `VRCFury-SPS-URP` assembly. The assembly is guarded by `com.unity.render-pipelines.universal` so projects without URP keep compiling.
- Add `VRCFury.SPS.URP.VrcfurySpsUrpRendererFeature` to every URP renderer data asset used by an SPS scene/avatar.
- `SpsMarkersService` selects URP marker materials and omits the Built-in data-grab material when the active render pipeline is URP.

Do not remove the Built-in path. The branch supports both:

- Built-in: current `GrabPass` path.
- URP: explicit render pass / render texture path.

## Phase 3 — URP-compatible SPS shaders

Status: implemented for desktop geometry-shader URP.

Implemented work:

- Added `SPS/URP/sps_socket_urp.shader` and `SPS/URP/sps_resolver_urp.shader` with no `GrabPass`.
- Added `RenderPipeline` tags and custom URP `LightMode` tags consumed by the renderer feature.
- Kept exact integer texture reads from `sps_texture.cginc`.
- Preserved the existing geometry shader path for desktop first.
- Later: investigate non-geometry fallback for Quest/mobile.

Acceptance criteria:

- Socket marker writes match Built-in SPSv2 cell layout.
- Resolver writes match Built-in SPSv2 cell layout.
- Patched plug deformation reads the same payload format regardless of pipeline.
- Desktop URP renders a known SPS plug/socket test scene with same deformation behavior as Built-in.

## Phase 4 — URP-aware shader patcher

Status: implemented initial URP-aware pass filtering.

`SpsPatcher` currently assumes Built-in-style shader conventions in several places, especially pass tags. URP support needs a patcher mode that understands:

- URP `LightMode` tags such as `UniversalForward`, `UniversalForwardOnly`, `SRPDefaultUnlit`, `DepthOnly`, and shadow/meta passes.
- Shader Graph generated shader source where available.
- Custom hand-written HLSL URP shaders.

Implementation notes:

- Existing Built-in shader patching keeps the Built-in `ForwardBase` injection behavior.
- URP mode allows visible deformation passes (`UniversalForward`, `UniversalForwardOnly`, `UniversalGBuffer`, `SRPDefaultUnlit`, `LightweightForward`) and explicitly skips shadow/depth/depth-normal/meta/selection/picking/motion-vector passes.
- URP mode avoids Built-in `ForwardBase` injection and avoids treating non-surface `HLSLINCLUDE`/`CGINCLUDE` blocks as Built-in surface shaders.

Acceptance criteria:

- Existing Built-in shader patching remains unchanged.
- URP shaders are patched only for render passes where vertex deformation is safe.
- Shadow/depth/meta handling is explicit in code; Unity project validation is still required for final scene coverage.

## Phase 5 — BasisVR integration package

Status: implemented reflection-only optional adapter.

Added `Runtime/Integration/Basis/` optional adapter folder that:

- Detects Basis avatar roots by reflection (`Basis.Scripts.BasisSdk.BasisAvatar`).
- Detects `Basis.Scripts.TransformBinders.BasisLockToBone` by reflection as an initial armature-link authoring hint.
- Registers a `FuryArmatureLinkHooks.CollectArmatureLinks` collector.
- Maps Basis bone role names to `HumanBodyBones`; `Mouth` maps to `Jaw`, unsupported roles such as `CenterEye` are skipped.
- Does not reference BasisVR or VRCSDK types in adapter source.

Suggested mapping sources from current Basis repository:

- `Basis.Scripts.Avatar.BasisAvatar`
- `Basis.Scripts.TransformBinders.BasisLockToBone`
- `Basis.Scripts.TransformBinders.BoneControl.BasisBoneTrackedRole`
- `Basis.Scripts.TransformBinders.BoneControl.BasisFallBackBoneData`

## Phase 6 — Tests and validation

Status: static/editor test coverage added; full Unity scene matrix still needs to be run in Unity installations.

Required test projects:

1. Unity 2019.4 Built-in legacy VRCFury test project: must remain green.
2. Unity 6000.5.x Built-in smoke scene: current SPS path still works.
3. Unity 6000.5.x URP desktop smoke scene: SPSv2 plug/socket works.
4. Unity 6000.5.x URP Basis scene: Basis armature-link hook creates expected links.

Suggested automated checks:

- Compile with no URP package installed.
- Compile with URP installed.
- Build SPS test avatar/scene and compare generated materials/shaders.
- Verify generated `_VFGrid*` textures are present and have expected texel size properties.

Implemented automated/static checks in this branch:

- `Tests/SpsPatcherUrpTests.cs` covers URP pass filtering for visible, shadow, depth, meta, selection, motion-vector, and unknown passes while asserting Built-in filtering behavior remains legacy-compatible.
- Local non-Unity checks validate asmdef JSON, unique Unity meta GUIDs, absence of direct URP references in core asmdefs, and absence of direct Basis/VRCSDK type references in the Basis adapter source.

## Later TODO — toggle conversion to Vixxy

Do not mix this into the first SPS/URP bring-up.

Later work item:

- Convert VRCFury toggle output to Vixxy-compatible toggle representation where possible.
- Preserve existing VRCFury toggle behavior as fallback.
- Add migration notes for VRCFury-only toggles that cannot be represented in Vixxy.

## Non-goals for first pass

- Quest/mobile SPS support without geometry shaders.
- Full Shader Graph rewrite support without generated-source validation.
- Removing the Built-in `GrabPass` path.
- Hard dependency on BasisVR or URP packages in the core VRCFury assembly.
