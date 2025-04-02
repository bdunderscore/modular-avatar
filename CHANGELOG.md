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
