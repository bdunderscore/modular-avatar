# Changelog

All notable changes to this project will be documented in this file.
Changes between prerelease versions will be documented in `CHANGELOG-PRERELEASE.md` instead.

[日本語版はこちらです。](CHANGELOG-jp.md)

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Added CHANGELOG files
- [#1482] Added support for replacing pre-existing animator controllers to `Merge Animator`
- [#1481] Added [World Scale Object](https://m-a.nadena.dev/docs/reference/world-scale-object)
- [#1489] Added [`MA MMD Layer Control`](https://modular-avatar.nadena.dev/docs/general-behavior/mmd)

### Fixed
- [#1460] When importing parameter assets in MA Parameters, "local only" parameters were incorrectly treated as
  "animator only"
- [#1489] Fixed compatibility issues between `Merge Blend Tree` or reactive components and MMD worlds.
  See [documentation](https://modular-avatar.nadena.dev/docs/general-behavior/mmd) for details on the new handling. 

### Changed
- [#1476] Switch ModularAvatarMergeAnimator and ModularAvatarMergeParameter to use new NDMF APIs (`IVirtualizeMotion` and `IVirtualizeAnimatorController`)
- [#1483] The Merge Animator "Match Avatar Write Defaults" option will no longer adjust write defaults on states in
  additive layers, or layers with only one state and no transitions.
- [#1429] Merge Armature will now allow you to merge humanoid bones with PhysBones attached in certain cases.
  - Specifically, child humanoid bones (if there are any) must be excluded from all attached Physbones. 
- [#1437] Create Toggle for Selection now creates submenus as necessary when multiple items are selected, and creates toggles as children.
- [#1499] When an audio source is controlled by an Object Toggle, disable the audio source when animations are blocked
  to avoid it unintentionally being constantly active.

### Removed

### Security

### Deprecated

## Older versions

Please see the github releases page at https://github.com/bdunderscore/modular-avatar/releases
