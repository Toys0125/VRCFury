# SPSv2 URP Port Notes

This folder is reserved for URP-specific SPSv2 shader/render-transport assets.

The current upstream SPSv2 path uses ShaderLab `GrabPass` textures:

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

## Proposed render order

1. Clear SPS intermediate textures.
2. Render socket markers into `_VFGrid56` equivalent.
3. Render resolver markers after `_VFGrid56` is available.
4. Render/finalize `_VFGridFinal` before plug meshes using SPS-patched materials render.
5. Let patched plug vertex shaders read `_VFGridFinal` and deform.

## Desktop-first assumptions

The first URP implementation may keep the existing geometry shader approach. A later mobile/Quest pass should replace geometry shader usage with a mesh/compute/instancing strategy.

## Files to add in this folder

- URP socket marker shader without `GrabPass`.
- URP resolver shader without `GrabPass`.
- Optional URP data-finalization shader if `_VFGridFinal` cannot be generated directly by the render pass.
- Test-only debug shader that visualizes the SPS grid texture.
