# SPSv2 URP Port Notes

This folder contains the URP-specific SPSv2 shader and render-transport assets.

The Built-in Render Pipeline SPSv2 path still uses ShaderLab `GrabPass` textures:

- `_VFGrid56`
- `_VFGridFinal`

URP does not support ShaderLab `GrabPass`, so the URP path must generate and publish those textures explicitly.

## Required texture contract

URP code must publish these globals before patched SPS deform shaders run:

```hlsl
Texture2D or Texture2DArray _VFGrid56;
float4 _VFGrid56_TexelSize;
Texture2D or Texture2DArray _VFGridFinal;
float4 _VFGridFinal_TexelSize;
```

The texture type must match the existing `SPS_INIT_TEX` behavior in `SPS/common/sps_texture.cginc`:

- non-stereo / multipass: `Texture2D`
- single-pass instanced / multiview: `Texture2DArray`

## Implemented render order

`Runtime/VrcfurySpsUrpRendererFeature.cs` implements the URP bridge as a `ScriptableRendererFeature`:

1. Allocate and clear `_VFGrid56` and `_VFGridFinal` temporary render textures for the current camera descriptor.
2. Render `VRCFurySpsSocketMarker` passes into `_VFGrid56`.
3. Publish `_VFGrid56` and `_VFGrid56_TexelSize` globally.
4. Render `VRCFurySpsResolver` passes into `_VFGridFinal` while reading `_VFGrid56`.
5. Publish `_VFGridFinal` and `_VFGridFinal_TexelSize` globally before normal opaque/transparent avatar rendering.
6. Let patched plug vertex shaders read `_VFGridFinal` and deform.

Add **VRCFury SPS URP Renderer Feature** (`VRCFury.SPS.URP.VrcfurySpsUrpRendererFeature`) to every URP renderer data asset used by the scene/avatar. The optional `VRCFury-SPS-URP` assembly is guarded by `com.unity.render-pipelines.universal` so projects without URP keep compiling.

## Desktop-first assumptions

The first URP implementation may keep the existing geometry shader approach. A later mobile/Quest pass should replace geometry shader usage with a mesh/compute/instancing strategy.

## Files in this folder

- `sps_socket_urp.shader`: URP socket marker shader without `GrabPass`.
- `sps_resolver_urp.shader`: URP resolver shader without `GrabPass`; reads the explicitly published `_VFGrid56` texture.
- `Runtime/VrcfurySpsUrpRendererFeature.cs`: URP render pass that publishes the `_VFGrid*` globals.

No URP data-finalization shader is currently needed because the render pass renders resolver markers directly into `_VFGridFinal`.
