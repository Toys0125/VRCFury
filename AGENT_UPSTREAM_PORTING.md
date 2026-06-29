# Agent Guide: Porting Upstream VRCFury Beta into BasisURP

This branch is intended to stay close to upstream `VRCFury/VRCFury:beta` while carrying Basis/URP integration work.

## Remotes

Expected remotes:

```bash
git remote -v
# origin  https://github.com/VRCFury/VRCFury.git
# toys    https://github.com/Toys0125/VRCFury.git
```

## Update workflow

Use this flow when upstream beta changes:

```bash
git fetch origin beta
git checkout BasisURP
git rebase origin/beta
```

If the branch has already been published and shared, use a merge instead:

```bash
git fetch origin beta
git checkout BasisURP
git merge origin/beta
```

## Conflict priority

When resolving conflicts, preserve upstream behavior first unless the conflict is in a clearly BasisURP-owned file.

BasisURP-owned files currently include:

- `BASIS_URP_INTEGRATION.md`
- `AGENT_UPSTREAM_PORTING.md`
- `com.vrcfury.vrcfury/Runtime/Integration/FuryArmatureLinkHooks.cs`
- `com.vrcfury.vrcfury/Runtime/Integration/Basis/`
- `com.vrcfury.vrcfury/Editor-Avatars/Service/ExternalArmatureLinkHookService.cs`
- `com.vrcfury.vrcfury/SPS/URP/`
- URP-aware additions in `com.vrcfury.vrcfury/Editor-Common/Builder/Haptics/SpsMarkersService.cs`, `SpsPatcher.cs`, and `SpsRenderPipelineSupport.cs`

## Required post-port checks

After replaying upstream changes, run these checks manually or through an agent:

1. `git status --short --branch`
2. Search for SPS transport changes:
   - `GrabPass`
   - `_VFGrid56`
   - `_VFGridFinal`
   - `SpsPatcher`
   - `SpsMarkersService`
3. Search for Armature Link changes:
   - `ArmatureLinkService`
   - `ArmatureLinkBuilder`
   - `ArmatureLink`
   - `FuryArmatureLink`
4. Re-check that `ExternalArmatureLinkHookService` still runs before `FeatureOrder.ArmatureLink`.
5. Compile a project without BasisVR installed.
6. Compile a project without URP installed.
7. Compile a Unity 6000.5.x URP project once URP implementation files are active.

## Commit style

Keep commits small and separated by concern:

- `basis: add external armature link hook`
- `sps-urp: add render texture transport skeleton`
- `sps-urp: port resolver marker shader`
- `docs: update BasisURP porting checklist`

## Do not do this during upstream ports

- Do not rewrite upstream VRCFury systems unnecessarily.
- Do not add a hard BasisVR dependency to VRCFury core assemblies.
- Do not add a hard URP dependency to assemblies that must compile in non-URP projects.
- Do not mix Vixxy toggle conversion into SPS/URP porting commits.
