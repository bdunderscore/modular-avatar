# Changelog

All notable changes to this project will be documented in this file.
Changes between prerelease versions will be documented in `CHANGELOG-PRERELEASE.md` instead.

[日本語版はこちらです。](CHANGELOG-jp.md)

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Fixed

### Changed

### Removed

### Security

### Deprecated

## [1.15.1] - [2025-12-06]

### Fixed
- [#1826] Fixed an issue where `MA Menu Item`s whose parameter is defined in a `MA Parameters` component could end up
  with a value of zero.
- [#1828] Fixed an issue where using `MA Scale Adjuster` on a non-humanoid avatar would result in a build error.

## [1.15.0] - [2025-12-03]

### Added
- Added `MA Fit Preview`, a new feature which allows you to pose a copy of your avatar, with physbones moving in edit mode.
  - You can adjust constraint and physbone settings, and have them reflected in the preview copy in real time.
- [#1786] Implement `MA Global Collider`
  - This feature was contributed by @ZenithVal!
- [#1769] Added warning when VRCFury version < 1.1250.0 is present with Mesh Cutter or Shape Changer delete mode components, as these combinations are incompatible.
- [#1804] Added support for the `IsAnimatorEnabled` VRChat built-in parameter

### Fixed
- [#1791] `World Fixed Object` would double the scale of objects it's attached to
- [#1778] `Vertex Filter - By Axis` would leave the transform handle disabled when switching away from the object while
  in edit mode.
- [#1799] `Convert Constraints` would fail to fix animations if a constraint was replaced by `Replace Object` 
- [#1808] Improved performance of preview logic, particularly when there are a very large number of disabled avatars
- [#1812] `Scale Adjuster` can now adjust the length of humanoid bones
- [#1818] Fixed compile errors when VRCSDK is not present in the project
- [#1823] Reaction Debugger may throw `MissingReferenceException`

### Changed
- [#1784] Removed dependency on `com.vrchat.avatars`
- [#1776] Mesh Cutter components parented underneath their target mesh, will ignore that mesh and its parents when
  considering
  whether they are constant-on.
    - This improves performance, and improves compatibility with non-VRChat platforms.
- [#1815] Modular Avatar will no longer garbage collect unused objects when they are referenced by an animation

## [1.14.3] - [2025-09-18]

### Fixed
- [#1767] Fixed an issue where, when closing the `Vertex Filter by Axis` inspector in edit mode, the normal tool handle
would remain invisible.
- [#1766] `Vertex Filter By Axis` would show incorrect results in preview, when the avatar or renderer had a non-default scale or
otherwise modified transform.

## [1.14.2] - [2025-09-17]

### Fixed
- [#1765] Fixed an issue where, when adjusting parameter types to Float, some parameter drivers might not behave as expected.

## [1.14.1] - [2025-09-15]

### Fixed

- [#1761] Certain generic avatars failed to upload

## [1.14.0] - [2025-09-13]

### Added
- [#1667] Implement `Mesh Cutter` - a component which can be used to delete or toggle portions of a mesh.
    - Implement vertex filters `By Bone`, `By Blendshape`, By Axis` and (#1651) `By Mask`
- [#1697] Exposed `ModularAvatarMergeArmature.GetBonesMapping` API
- [#1601] Add warning when using MA MMD Layer Control with layers that have WriteDefaults OFF states

### Fixed
- [#1675] MMD Layer Control did not work to opt-in a layer when that layer became layer #0
- [#1670] Fixed an issue where generated meshes might not be registered in ObjectRegistry in some cases
- [#1704] An exception could occur when deleting vertices in a mesh with a 16-bit index format
- [#1713] Fixed an issue where certain meshes might be incorrectly processed by `Shape Changer`'s delete mode
- [#1715] Fixed an issue where `Shape Changer` or `Mesh Cutter` could incorrectly delete all vertex color data in a mesh
- [#1719] `Scale Adjuster` did not update positions of child objects when editing directly using the inspector fields
- [#1726] Parameter drivers did not work properly when parameter types were adjusted after merging animators
- [#1728] Menu Items that did not have a reactive component attached to their game object, but did have a child object 
with a reactive component, would not function properly.
- [#1732] Static (always-on) reactive components would have had lower priority than the FX animator
- [#1750] Modular Avatar would generate too many Head Chop components in some cases, breaking the build

### Changed
- [#1705] Reactive Component initial states are now applied on non-VRChat platforms
- [#1729] `Shape Changer` components in `Set` mode will now override prior `Delete` mode settings. This reverses an
  accidental breaking change in 1.13.x.
- [#1732] In previous versions of Modular Avatar, static (always-on) reactive components would have had
  lower priority than the FX animator. This was a bug and has been fixed in this version; however, it's possible that this
  fix might cause some existing gimmicks to have different behavior.

## [1.13.4] - [2025-08-15]

### Fixed
- [#1682] Fixed a potential `NullReferenceException` when operating `ModularAvatarMenuItem`.
  (Fix provided by @Tliks)
- [#1683] Fixed an issue where renderers with a root bone under the head might end up with incorrect mesh bounds.
  (Fix provided by @ReinaS-64892)

## [1.13.3] - [2025-08-14]

### Fixed

- [#1679] Fix issues where meshes with a root bone under the head could be invisible in first person, when affected by
  a `MA Shape Changer` in delete mode.

## [1.13.2] - [2025-08-09]

### Fixed
- [#1671] Shape changer could cause VRChat crashes in certain worlds

## [1.13.1] - [2025-08-02]

### Fixed
- [#1653] Scene is always updated by `Blendshape Sync`
- [#1660] Deleted shapes were not applied when animations are blocked by VRChat safety settings.

## [1.13.0] - [2025-07-12]

**Note**: This release contains new, portable APIs in `ModularAvatarMenuItem` to allow (in the future) menu items to be
used in other platforms than VRChat. Plugins which generate menu items should be updated to use the new APIs; the old
APIs will be deprecated in a future release, and removed in a future 2.0 release.

### Added
- (Experimental feature) Enabled support for non-VRC platforms
- [#1594] Display the exceeded parameter count in the MA Information
- [#1604] Implement `MA Material Swap`
- [#1623] Implement `MA Platform Filter`
- [#1610] Added threshold setting to `Shape Changer`
- [#1629] Report a nonfatal error when an animator being merged has a broken layer (missing state machine)
- [#1635] Added `ModularAvatarMenuItem` APIs to allow menu items components to be created without a dependency on VRCSDK.
- [#1596] Added `MA Rename Collision Tags` component
  - This allows renaming of collision tags (Contacts) to unique names, similar to the auto-rename feature in MA Parameters

### Fixed
- [#1587] The Mesh Settings gizmo was not shown when in `SetOrInherit` mode
- [#1589] A `KeyNotFoundException` could occur when the target of a Merge Animator or Merge Motion component was null
- [#1605] Fixed an issue where the preview differed from the build result when multiple Material Setters conflicted
- [#1632] `Blendshape Sync` would not work in the editor when on a disabled object
- [#1633] `Blendshape Sync` would not be properly applied to the initial state of the avatar on build
- [#1634] Fixed compile errors when VRCSDK is not present in the project

### Changed
- [#1608] [#1610] Shape Changer will now delete shapekeys fully, even if animated
- [#1636] In `Match Write Defaults` mode, `Merge Animator` will no longer force layers to be write defaults ON when they
  contain only blend trees, if none of those blend trees are Direct Blend Trees.

## [1.12.5] - [2025-04-14]

### Fixed
- [#1555] Fixed compatibility regression from 1.11.x: VRC Animator Play Audio, when configured with an absolute path
  but merged with a relative-path merge animator component, will now detect that the indicated object does not
  exist, and treat the reference as an absolute path.
  - Note that if there is an object in the target path, then it will be treated as a relative path. Using
    addressing for Play Audio behaviors consistent with Merge Animator settings is therefore recommended as it will be
    more robust.
- [#1558] Fixed an issue where Merge Animators animating transforms in the base avatar's armature would break.
- Update NDMF dependency
  - Fixed an issue where duplicate layer entries in the VRChat Avatar Descriptor would cause all animator contents
    to be ignored.
  - Fixed a benign `NullReferenceException` at initialization
  - Fixed a NullReferenceException in AnimationIndex
  - Fixed an issue where animation curve paths being rewritten multiple times might be deleted

## [1.12.4] - [2025-04-10]

### Fixed
- [#1552] Merge Blend Tree failed to correct parameter types when the main avatar FX layer contained an int or bool
  parameter with the same name as one used in the blend tree.
- [#1553] Reactive components might generate states with incorrect write default settings

### Changed
- [#1551] Merge Animator will always set WD ON for single-state blendtree layers with no any state transitions.
  - This fixes compatibility issues with assets which relied on the prior behavior.

## [1.12.3] - [2025-04-05]

### Fixed
- Fixed issues with additive layers (via NDMF version upgrade)

### Changed
- [#1542] Merge Animator now will match WD settings for layers with a single state containing an animation clip,
  but not if it contains a blend tree. This fixes some compatibility issues introduced in 1.12 (where the behavior
  was changed to not match WD settings for single-state animation clips).
- [#1551] Merge Animator will always set WD ON for single-state blendtree layers with no any state transitions.

## [1.12.2] - [2025-04-03]

### Fixed
- [#1537] Curves which animated animator parameters, when added using a `Merge Motion` component, would not be updated by
  `Rename Parameters`

## [1.12.1] - [2025-04-02]

### Fixed
- [#1532] Modular Avatar has compiler errors in a newly created project

## [1.12.0] - [2025-04-01]

### Added
- Added CHANGELOG files
- [#1482] Added support for replacing pre-existing animator controllers to `Merge Animator`
- [#1481] Added [World Scale Object](https://m-a.nadena.dev/docs/reference/world-scale-object)
- [#1489] Added [`MA MMD Layer Control`](https://modular-avatar.nadena.dev/docs/general-behavior/mmd)

### Fixed
- [#1460] When importing parameter assets in MA Parameters, "local only" parameters were incorrectly treated as
  "animator only"
- [#1489] Fixed compatibility issues between `Merge Blend Tree` or reactive components and MMD worlds.
  - See [documentation](https://modular-avatar.nadena.dev/docs/general-behavior/mmd) for details on the new handling.
  - To disable this behavior, attach the new `MA VRChat Settings` component to any object on your avatar and disable the appropriate setting.
- [#1501] Unity keyboard shortcuts don't work when editing text fields on the MA Parameters component 
- [#1410] Motion overrides on synced layers are not updated for Bone Proxy/Merge Armature object movement
- [#1504] The internal `DelayDisable` layer no longer references unnecessary objects in some situations
  - This helps improve compatibility with AAO and other tools that track whether objects are animated 
- [#1508] Fix an issue where automatic compression of expressions menu icons would fail when the texture dimensions were
  not divisible by four.
- [#1513] Expression menu icon compression broke on iOS builds

### Changed
- [#1529] `MA Parameters` auto-rename and `MA Menu Item`'s automatic parameter feature now assign names based on the
  path of the object. This should improve compatibility with `MA Sync Parameter Sequence`
  - If you are using `MA Sync Parameter Sequence`, it's a good idea to empty your SyncedParams asset and reupload all
    platforms after updating to this version.
- [#1514] `Merge Blend Tree` is now `Merge Motion (Blend Tree)` and supports merging animation clips as well as blend trees
- [#1476] Switch ModularAvatarMergeAnimator and ModularAvatarMergeParameter to use new NDMF APIs (`IVirtualizeMotion` and `IVirtualizeAnimatorController`)
- [#1483] The Merge Animator "Match Avatar Write Defaults" option will no longer adjust write defaults on states in
  additive layers, or layers with only one state and no transitions.
- [#1429] Merge Armature will now allow you to merge humanoid bones with PhysBones attached in certain cases.
  - Specifically, child humanoid bones (if there are any) must be excluded from all attached Physbones. 
- [#1437] Create Toggle for Selection now creates submenus as necessary when multiple items are selected, and creates toggles as children.
- [#1499] When an audio source is controlled by an Object Toggle, disable the audio source when animations are blocked
  to avoid it unintentionally being constantly active.
- [#1502] `World Fixed Object` now uses `VRCParentConstraint` and is therefore compatible with Android builds 

## Older versions

Please see the github releases page at https://github.com/bdunderscore/modular-avatar/releases
