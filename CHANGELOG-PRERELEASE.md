# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Fixed
- [#1587] The Mesh Settings gizmo was not shown when in `SetOrInherit` mode

### Changed

### Removed

### Security

### Deprecated

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