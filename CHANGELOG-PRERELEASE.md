# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Fixed

### Changed

### Removed

### Security

### Deprecated

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

## [1.14.0-rc.2] - [2025-09-11]

### Added
- [#1743] `VF By Mask` now supports non-read-write mask textures
- [#1755] Added hint box to the `VF By Mask` inspector when Mask Texture Editor is missing

## [1.14.0-rc.1] - [2025-09-09]

### Added
- [#1743] `VF By Mask` now supports non-read-write mask textures

### Fixed

- [#1753] When using `Vertex Filter By Axis` and `Scale Adjuster` together, the preview and build result might differ
- [#1750] Modular Avatar would generate too many Head Chop components in some cases, breaking the build
- [#1751] When scaling a bone to zero using `Scale Adjuster`, an exception would be thrown, and the relative position
  of child bones might not be preserved.

## [1.14.0-rc.0] - [2025-09-07]

### Added
- [#1740] `VF By Shape` can now select multiple blendshapes in a single component
  - Note: Data created in prior betas for this component is not compatible with this version. 
- [#1738] Mesh Cutter can now be configured to combine the selections from all vertex filters, instead of taking their
intersection.

### Fixed
- [#1739] Improved performance of Mesh Cutter preview processing

## [1.14.0-beta.2] - [2025-09-02]

### Fixed
- [#1726] Parameter drivers did not work properly when parameter types were adjusted after merging animators
- [#1728] Menu Items that did not have a reactive component attached to their game object, but did have a child object
  with a reactive component, would not function properly.
- [#1732] Static (always-on) reactive components would have had lower priority than the FX animator

### Changed
- [#1729] `Shape Changer` components in `Set` mode will now override prior `Delete` mode settings. This reverses an
  accidental breaking change in 1.13.x.
- [#1732] In previous versions of Modular Avatar, static (always-on) reactive components would have had
  lower priority than the FX animator. This was a bug and has been fixed in this version; however, it's possible that this
  fix might cause some existing gimmicks to have different behavior.

## [1.14.0-beta.1] - [2025-08-30]

### Fixed
- [#1721] Fixed an issue where multiple Shape Changers or Mesh Cutters affecting the same mesh might result in some
  vertices not being deleted.
- [#1719] `Scale Adjuster` did not update positions of child objects when editing directly using the inspector fields

## [1.14.0-beta.0] - [2025-08-29]

### Added
- [#1667] Implement `Mesh Cutter` - a component which can be used to delete or toggle portions of a mesh.
    - Implement vertex filters `By Bone`, `By Blendshape`, `By Axis` and (#1651) `By Mask`
- [#1697] Exposed `ModularAvatarMergeArmature.GetBonesMapping` API
- [#1601] Add warning when using MA MMD Layer Control with layers that have WriteDefaults OFF states

### Fixed
- [#1670] Fixed an issue where generated meshes might not be registered in ObjectRegistry in some cases
- [#1671] Shape changer could cause VRChat crashes in certain worlds
- [#1679] Fix issues where meshes with a root bone under the head could be invisible in first person, when affected by
  a `MA Shape Changer` in delete mode.
- [#1682] Fixed a potential `NullReferenceException` when operating `ModularAvatarMenuItem`.
  (Fix provided by @Tliks)
- [#1683] Fixed an issue where renderers with a root bone under the head might end up with incorrect mesh bounds.
  (Fix provided by @ReinaS-64892)
- [#1675] MMD Layer Control did not work to opt-in a layer when that layer became layer #0
- [#1704] An exception could occur when deleting vertices in a mesh with a 16-bit index format
- [#1713] Fixed an issue where certain meshes might be incorrectly processed by `Shape Changer`'s delete mode
- [#1715] Fixed an issue where `Shape Changer` or `Mesh Cutter` could incorrectly delete all vertex color data in a mesh

### Changed
- [#1705] Reactive Component initial states are now applied on non-VRChat platforms

## [1.13.1] - [2025-08-02]

### Fixed
- [#1653] Scene is always updated by `Blendshape Sync`
- [#1660] Deleted shapes were not applied when animations are blocked by VRChat safety settings.

## [1.13.0] - [2025-07-12]

## [1.13.0-rc.1] - [2025-07-10]

### Added
- [#1642] Added quick swap mode to Material Swap
- [#1635] Added `ModularAvatarMenuItem` APIs to allow menu items components to be created without a dependency on VRCSDK.

### Fixed
- [#1640] `MA Material Swap` would not work in some situations.
- [#1641] Fixed Material Swap observing
- [#1641] Fixed that MatSwap target is empty when dragging material into Material Swap

## [1.13.0-rc.0] - [2025-07-06]

### Added
- [#1596] Added `MA Rename Collision Tags` component
  - This allows renaming of collision tags (Contacts) to unique names, similar to the auto-rename feature in MA Parameters

### Fixed
- [#1634] Fixed compile errors when VRCSDK is not present in the project

### Changed
- [#1636] In `Match Write Defaults` mode, `Merge Animator` will no longer force layers to be write defaults ON when they
  contain only blend trees, if none of those blend trees are Direct Blend Trees.

## [1.13.0-beta.1] - [2025-07-03]

### Added
- [#1610] Added threshold setting to `Shape Changer`
- [#1629] Report a nonfatal error when an animator being merged has a broken layer (missing state machine)

### Fixed
- [#1632] `Blendshape Sync` would not work in the editor when on a disabled object
- [#1633] `Blendshape Sync` would not be properly applied to the initial state of the avatar on build

### Changed
- [#1608] [#1610] Shape Changer will now delete shapekeys fully, even if animated

## [1.13.0-beta.0] - [2025-06-21]

### Added
- [#1594] Display the exceeded parameter count in the MA Information
- [#1604] Implement `MA Material Swap`
- [#1620] Add material selector to `MA Material Swap` Inspector
- [#1623] Implement `MA Platform Filter`

### Fixed
- [#1587] The Mesh Settings gizmo was not shown when in `SetOrInherit` mode
- [#1608] [#1610] Shape Changer will now delete shapekeys fully, even if animated
- [#1589] A `KeyNotFoundException` could occur when the target of a Merge Animator or Merge Motion component was null
- [#1605] Fixed an issue where the preview differed from the build result when multiple Material Setters conflicted

## [1.13.0-alpha.2] - [2025-04-14]

### Fixed
- [#1558] Fixed an issue where Merge Animators animating transforms in the base avatar's armature would break.

## [1.13.0-alpha.1] - [2025-04-10]

### Fixed
- [#1552] Merge Blend Tree failed to correct parameter types when the main avatar FX layer contained an int or bool
  parameter with the same name as one used in the blend tree.
- [#1553] Reactive components might generate states with incorrect write default settings
- [#1555] Fixed compatibility regression from 1.11.x: VRC Animator Play Audio, when configured with an absolute path
  but merged with a relative-path merge animator component, will now detect that the indicated object does not
  exist, and treat the reference as an absolute path.
  - Note that if there is an object in the target path, then it will be treated as a relative path. Using
    addressing for Play Audio behaviors consistent with Merge Animator settings is therefore recommended as it will be
    more robust.

### Changed
- [#1551] Merge Animator will always set WD ON for single-state blendtree layers with no any state transitions.
  - This fixes compatibility issues with assets which relied on the prior behavior.

## [1.13.0-alpha.0] - [2025-04-08]

### Added
- (Experimental feature) Enabled support for non-VRC platforms

## [1.12.3] - [2025-04-05]

### Fixed
- Fixed issues with additive layers (via NDMF version upgrade)

### Changed
- [#1542] Merge Animator now will match WD settings for layers with a single state containing an animation clip,
  but not if it contains a blend tree. This fixes some compatibility issues introduced in 1.12 (where the behavior
  was changed to not match WD settings for single-state animation clips).

## [1.12.2] - [2025-04-03]

### Fixed
- [#1537] Curves which animated animator parameters, when added using a `Merge Motion` component, would not be updated by
  `Rename Parameters`

## [1.12.1] - [2025-04-02]

### Fixed
- [#1532] Modular Avatar has compiler errors in a newly created project

## [1.12.0] - [2025-04-01]

### Fixed
- [#1531] Fix compatibility issue with lylicalInventory

### Changed
- [#1530] `MA Menu Item` auto parameters now also assign names based on object paths

## [1.12.0-rc.1] - [2025-03-28]

### Added
- [#1524] Added support for disabling MMD world handling at an avatar level

### Fixed
- [#1522] `Convert Constraints` failed to convert animation references
- [#1528] `Merge Animator` ignored the `Match Avatar Write Defaults` setting and always matched

### Changed
- [#1529] `MA Parameters` auto-rename now assigns new names based on the path of the object. This should improve
  compatibility with `MA Sync Parameter Sequence`
  - If you are using `MA Sync Parameter Sequence`, it's a good idea to empty your SyncedParams asset and reupload all
    platforms after updating to this version.

## [1.12.0-rc.0] - [2025-03-22]

### Fixed
- [#1508] Fix an issue where automatic compression of expressions menu icons would fail when the texture dimensions were
  not divisible by four.
- [#1513] Expression menu icon compression broke on iOS builds

### Changed
- [#1514] `Merge Blend Tree` is now `Merge Motion (Blend Tree)` and supports merging animation clips as well as blend trees

## [1.12.0-beta.0] - [2025-03-17]

### Added
- [#1497] Added changelog to docs site
- [#1482] Added support for replacing pre-existing animator controllers to `Merge Animator`
- [#1481] Added [World Scale Object](https://m-a.nadena.dev/dev/docs/reference/world-scale-object)
- [#1489] Added [`MA MMD Layer Control`](https://modular-avatar.nadena.dev/docs/general-behavior/mmd)

### Fixed
- [#1492] Fixed incorrect icon and logo assets in prior prerelease
- [#1489] Fixed compatibility issues between `Merge Blend Tree` or reactive components and MMD worlds.
  See [documentation](https://modular-avatar.nadena.dev/docs/general-behavior/mmd) for details on the new handling.
- [#1501] Unity keyboard shortcuts don't work when editing text fields on the MA Parameters component
- [#1410] Motion overrides on synced layers are not updated for Bone Proxy/Merge Armature object movement
- [#1504] The internal `DelayDisable` layer no longer references unnecessary objects in some situations
  - This helps improve compatibility with AAO and other tools that track whether objects are animated

### Changed
- [#1483] The Merge Animator "Match Avatar Write Defaults" option will no longer adjust write defaults on states in
  additive layers, or layers with only one state and no transitions.
- [#1429] Merge Armature will now allow you to merge humanoid bones with PhysBones attached in certain cases.
    - Specifically, child humanoid bones (if there are any) must be excluded from all attached Physbones.
- [#1437] Create Toggle for Selection now creates submenus as necessary when multiple items are selected, and creates toggles as children.
- [#1499] When an audio source is controlled by an Object Toggle, disable the audio source when animations are blocked
  to avoid it unintentionally being constantly active.
- [#1502] `World Fixed Object` now uses `VRCParentConstraint` and is therefore compatible with Android builds

## [1.12.0-alpha.2] - [2025-03-10]

### Added
- Added CHANGELOG files

### Changed
- [#1476] Switch ModularAvatarMergeAnimator and ModularAvatarMergeParameter to use new NDMF APIs (`IVirtualizeMotion` and `IVirtualizeAnimatorController`)

## Older versions

Please see CHANGELOG.md